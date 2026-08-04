#!/usr/bin/env bash
# Origo performance benchmarks + regression gate.
#
# Runs TypedData micro-benchmarks (SG-generated inline vs boxing), core subsystem
# benchmarks (entity lifecycle, observer topology, DataSourceNode, Blackboard, save
# persistence, concurrent queue, random, strategy performance), and Godot adapter
# throughput benchmarks.
#
# Tagged [Trait("Category","Benchmark")], these run here only: they are excluded
# from the regular test run (scripts/test.sh filters them out) and executed once
# by this dedicated CI step, which scripts/ci.sh invokes.
#
# Every measurement is emitted as a machine-readable BENCH|... line by
# PerfReporter.EmitMetric and compared against docs/benchmarks/baseline.json.
# Comparison is meaningful only on the machine that captured the baseline
# (scripts/ci.sh on a developer machine): ops/s depends on CPU frequency scaling
# and tiered-JIT state, and even allocation counts vary across machines/runtime
# builds because JIT inlining decisions change per-instruction allocation.
# GitHub Actions runners are random VMs with fresh machine ids, so on CI the
# comparison is skipped entirely and the step acts as a smoke test.
# Pass --update-baseline to refresh baseline.json from the current run
# (e.g. after a confirmed improvement or an environment change).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

BASELINE="docs/benchmarks/baseline.json"
OPS_THRESHOLD=0.50
ALLOC_THRESHOLD=0.20
UPDATE_BASELINE=0
for arg in "$@"; do
  if [[ "$arg" == "--update-baseline" ]]; then
    UPDATE_BASELINE=1
  else
    echo "Unknown argument: $arg" >&2
    echo "Usage: $0 [--update-baseline]" >&2
    exit 2
  fi
done

MACHINE_ID="$(cat /etc/machine-id 2>/dev/null || hostname || echo unknown)"

EXIT_CODE=0
RUN_LOG="$(mktemp)"

run_benchmark() {
  local label="$1"
  local project="$2"
  echo ""
  echo ">>> $label"
  echo ""
  dotnet test "$project" \
    --configuration Release \
    --filter "Category=Benchmark" \
    --logger "console;verbosity=detailed" \
    -p:CollectCoverage=false 2>&1 | tee -a "$RUN_LOG" || EXIT_CODE=$?
}

run_benchmark \
  "SourceGeneration micro-benchmarks — TypedData inline vs boxing (value + reference types)" \
  "Origo.SourceGeneration.Tests/Origo.SourceGeneration.Tests.csproj"

run_benchmark \
  "Core real-world benchmarks — dictionary-backed, observer, serialization simulations" \
  "Origo.Core.Tests/Origo.Core.Tests.csproj"

run_benchmark \
  "GodotAdapter benchmarks — Godot-typed TypedData write/read/convert throughput" \
  "Origo.GodotAdapter.Tests/Origo.GodotAdapter.Tests.csproj"

echo ""
echo ">>> Comparing against baseline: $BASELINE"

CURRENT_JSON="$(mktemp)"
python3 - "$RUN_LOG" "$CURRENT_JSON" <<'EOF'
import json, re, sys
log, out = sys.argv[1], sys.argv[2]
metrics = {}
with open(log, encoding='utf-8', errors='replace') as f:
    for line in f:
        m = re.match(r'^\s*BENCH\|([^|]+)\|([^|]*)\|([^|]*)\|([0-9.]+)\|(-?[0-9]+)\s*$', line)
        if not m:
            continue
        kind, label, side, ops, alloc = m.groups()
        key = f"{kind}|{label}|{side}" if side else f"{kind}|{label}"
        metrics[key] = {"ops": float(ops), "alloc": int(alloc)}
json.dump(metrics, open(out, 'w'), indent=2, sort_keys=True)
EOF

if [[ ! -f "$BASELINE" ]]; then
  echo ""
  echo "No baseline found ($BASELINE). Capturing current run as the baseline."
  python3 - "$CURRENT_JSON" "$BASELINE" "$MACHINE_ID" <<'EOF'
import json, sys
metrics = json.load(open(sys.argv[1]))
doc = {"schema_version": 1, "machine_id": sys.argv[3], "metrics": metrics}
json.dump(doc, open(sys.argv[2], 'w'), indent=2, sort_keys=True)
EOF
  echo "Baseline written with $(python3 -c "import json,sys; print(len(json.load(open('$CURRENT_JSON'))))") metric(s) on machine '$MACHINE_ID'."
  echo "NOTE: verify docs/benchmarks/baseline.json reflects a representative machine,"
  echo "      and commit it together with benchmark-related changes."
  rm -f "$RUN_LOG" "$CURRENT_JSON"
  exit $EXIT_CODE
fi

if [[ "$UPDATE_BASELINE" == "1" ]]; then
  python3 - "$CURRENT_JSON" "$BASELINE" "$MACHINE_ID" <<'EOF'
import json, sys
metrics = json.load(open(sys.argv[1]))
doc = {"schema_version": 1, "machine_id": sys.argv[3], "metrics": metrics}
json.dump(doc, open(sys.argv[2], 'w'), indent=2, sort_keys=True)
EOF
  echo "Baseline updated with $(python3 -c "import json,sys; print(len(json.load(open('$CURRENT_JSON'))))") metric(s) on machine '$MACHINE_ID'."
  rm -f "$RUN_LOG" "$CURRENT_JSON"
  exit $EXIT_CODE
fi

python3 - "$CURRENT_JSON" "$BASELINE" "$OPS_THRESHOLD" "$ALLOC_THRESHOLD" "$MACHINE_ID" <<'EOF'
import json, sys
current_path, baseline_path = sys.argv[1], sys.argv[2]
ops_th, alloc_th = float(sys.argv[3]), float(sys.argv[4])
machine_id = sys.argv[5]
current = json.load(open(current_path))
baseline = json.load(open(baseline_path))

# Regression gates only apply on the machine the baseline was captured on.
# CI runners are random machines: neither throughput (CPU frequency scaling,
# tiered JIT state) nor allocation (JIT inlining/GC behavior varies per
# machine and runtime build) is comparable across machines, so every gate is
# skipped on a machine mismatch. Local runs of scripts/ci.sh on the
# baseline machine get the full gate.
same_machine = baseline.get("machine_id") == machine_id
if not same_machine:
    print(f"  Baseline machine '{baseline.get('machine_id')}' != current '{machine_id}':")
    print("  regression gates SKIPPED (metrics are not comparable across machines);")
    print("  benchmark run still acts as a smoke test.")

fails = []
skipped = 0
# Throughput gates only apply to min-of-rounds measurements (CompareTable /
# Compare / Report kinds); single-shot ReportTable rows swing with CPU
# frequency scaling, so only their allocation is gated.
THROUGHPUT_GATED_KINDS = {"CompareTable", "Compare", "Report"}
for key, cur in sorted(current.items()):
    base = baseline.get("metrics", {}).get(key)
    if base is None:
        skipped += 1
        print(f"  NEW metric (no baseline): {key}")
        continue
    if not same_machine:
        continue
    alloc_ratio = cur["alloc"] / base["alloc"] if base["alloc"] > 0 else 1.0
    if alloc_ratio > 1.0 + alloc_th:
        fails.append(f"  FAIL allocation: {key} {cur['alloc']} B vs baseline {base['alloc']} B ({alloc_ratio*100:.0f}%)")
    kind = key.split("|", 1)[0]
    if kind in THROUGHPUT_GATED_KINDS:
        ops_ratio = cur["ops"] / base["ops"] if base["ops"] > 0 else 1.0
        if ops_ratio < 1.0 - ops_th:
            fails.append(f"  FAIL throughput: {key} {cur['ops']:.0f} ops/s vs baseline {base['ops']:.0f} ({ops_ratio*100:.0f}%)")

if skipped:
    print(f"  {skipped} metric(s) without baseline entries (new benchmarks).")

if fails:
    print("")
    print(f"BENCHMARK REGRESSION DETECTED — {len(fails)} violation(s):")
    for fail in fails:
        print(fail)
    print("")
    print("If the change is a real improvement or the environment changed,")
    print("re-run with: bash scripts/benchmark.sh --update-baseline")
    print("and commit the refreshed docs/benchmarks/baseline.json.")
    sys.exit(1)

print("All benchmark metrics within thresholds.")
EOF
EXIT_CODE=$?

rm -f "$RUN_LOG" "$CURRENT_JSON"
exit $EXIT_CODE
