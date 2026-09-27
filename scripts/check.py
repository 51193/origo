#!/usr/bin/env python3
"""Plan and run Origo's layered verification checks.

The planner deliberately stays in Python's standard library.  The shell
wrapper owns SDK bootstrapping; this module owns change classification,
project-graph evaluation, and machine-readable evidence.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import subprocess
import sys
import tempfile
import time
from dataclasses import dataclass
from pathlib import Path


TEST_PROJECT_MARKER = ".Tests"
INTEGRATION_MARKER = ".Integration.Tests"
FULL_PATHS = {
    ".editorconfig",
    "Directory.Build.props",
    "Directory.Packages.props",
    "global.json",
    "Origo.sln",
    "scripts/ci.sh",
    "scripts/test.sh",
    "scripts/check.py",
    "scripts/check.sh",
    "scripts/benchmark.sh",
    "scripts/godot-test.sh",
    "scripts/dotnet-env.sh",
    "scripts/install-dotnet.sh",
    "AGENTS.md",
}
DOC_PREFIXES = ("docs/",)
GODOT_MARKERS = (".godot/", "project.godot", ".tscn", ".tres", ".gd")
@dataclass(frozen=True)
class Change:
    status: str
    path: str


def run_git(root: Path, *args: str, check: bool = True) -> str:
    result = subprocess.run(
        ["git", *args], cwd=root, text=True, capture_output=True, check=False
    )
    if check and result.returncode:
        raise RuntimeError(result.stderr.strip() or f"git {' '.join(args)} failed")
    return result.stdout


def repository_root(repository: str | None) -> Path:
    cwd = Path(repository).resolve() if repository else Path.cwd()
    output = subprocess.run(
        ["git", "rev-parse", "--show-toplevel"],
        cwd=cwd,
        text=True,
        capture_output=True,
        check=False,
    )
    if output.returncode:
        raise RuntimeError("not inside a Git repository")
    return Path(output.stdout.strip()).resolve()


def parse_name_status(output: str) -> list[Change]:
    changes: list[Change] = []
    for line in output.splitlines():
        fields = line.split("\t")
        if not fields:
            continue
        status = fields[0]
        if status.startswith("R") or status.startswith("C"):
            if len(fields) >= 3:
                changes.extend((Change(status, fields[1]), Change(status, fields[2])))
            continue
        if len(fields) >= 2:
            changes.append(Change(status, fields[1]))
    return changes


def changed_paths(root: Path, base: str) -> list[Change]:
    changes: dict[str, Change] = {}
    for args in (
        ("diff", "--name-status", "--find-renames", f"{base}...HEAD"),
        ("diff", "--cached", "--name-status", "--find-renames"),
        ("diff", "--name-status", "--find-renames"),
    ):
        for change in parse_name_status(run_git(root, *args)):
            changes[change.path] = change
    for path in run_git(root, "ls-files", "--others", "--exclude-standard").splitlines():
        changes[path] = Change("?", path)
    return [changes[path] for path in sorted(changes)]


def fingerprint(root: Path, base: str, changes: list[Change]) -> str:
    digest = hashlib.sha256()
    for args in (
        ("diff", "--binary", f"{base}...HEAD"),
        ("diff", "--cached", "--binary"),
        ("diff", "--binary"),
    ):
        digest.update(run_git(root, *args).encode())
    for change in changes:
        path = root / change.path
        digest.update(change.path.encode())
        if path.is_file():
            digest.update(path.read_bytes())
    return digest.hexdigest()


def default_base(root: Path) -> str:
    result = subprocess.run(
        ["git", "merge-base", "origin/main", "HEAD"],
        cwd=root,
        text=True,
        capture_output=True,
        check=False,
    )
    if result.returncode:
        raise RuntimeError("cannot compute default base; pass --base explicitly")
    return result.stdout.strip()


def project_files(root: Path) -> dict[str, Path]:
    return {
        path.relative_to(root).as_posix(): path
        for path in root.rglob("*.csproj")
        if "bin" not in path.parts and "obj" not in path.parts
    }


def evaluated_references(root: Path, projects: dict[str, Path]) -> dict[str, set[str]]:
    graph = {name: set() for name in projects}
    for name, path in projects.items():
        result = subprocess.run(
            ["dotnet", "msbuild", str(path), "-getItem:ProjectReference", "-nologo", "-v:q"],
            cwd=root,
            text=True,
            capture_output=True,
            check=False,
        )
        if result.returncode:
            raise RuntimeError(
                f"MSBuild project graph failed for {name}: {result.stderr.strip()}; "
                "run scripts/check.sh full explicitly"
            )
        try:
            payload = json.loads(result.stdout)
        except json.JSONDecodeError as exc:
            raise RuntimeError(
                f"MSBuild returned invalid graph JSON for {name}; run scripts/check.sh full explicitly"
            ) from exc
        for item in payload.get("Items", {}).get("ProjectReference", []):
            identity = item.get("FullPath")
            if identity:
                target = Path(identity).resolve()
                for candidate, candidate_path in projects.items():
                    if target == candidate_path.resolve():
                        graph[name].add(candidate)
                        break
    return graph


def test_project(name: str) -> bool:
    return (TEST_PROJECT_MARKER in name or name.endswith(".Tests.csproj")) and INTEGRATION_MARKER not in name


def project_for_path(path: str, projects: dict[str, Path]) -> str | None:
    candidate = Path(path)
    for name in projects:
        if name == path or name.startswith(candidate.as_posix().rstrip("/") + "/"):
            return name
    for name in projects:
        parent = Path(name).parent
        try:
            candidate.relative_to(parent)
        except ValueError:
            continue
        return name
    return None


def reverse_test_closure(graph: dict[str, set[str]], seeds: set[str]) -> set[str]:
    selected = set(seeds)
    changed = True
    while changed:
        changed = False
        for consumer, references in graph.items():
            if references & selected and consumer not in selected:
                selected.add(consumer)
                changed = True
    return {name for name in selected if test_project(name)}


def classify(root: Path, changes: list[Change], base: str) -> dict:
    projects = project_files(root)
    graph = evaluated_references(root, projects)
    paths = [change.path for change in changes]
    reasons: list[str] = []
    selected: set[str] = set()
    additional: set[str] = set()
    requires_full = False
    unknown: list[str] = []

    for path in paths:
        normalized = path.replace("\\", "/")
        if normalized in FULL_PATHS or normalized.startswith((".github/", "scripts/")):
            requires_full = True
            reasons.append(f"{normalized} changes the repository-wide contract")
        if normalized.startswith("Origo.SourceGeneration/"):
            additional.add("godot")
            generator_test_suffixes = (
                "/Origo.SourceGeneration.Tests.csproj",
                "/Origo.Core.Tests.csproj",
                "/Origo.GodotAdapter.Tests.csproj",
            )
            selected.update(
                name for name in projects if name.endswith(generator_test_suffixes)
            )
            reasons.append(
                f"{normalized} changes generated output; generator and consumer suites are selected"
            )
        if normalized.startswith(("Origo.GodotAdapter/", "Origo.GodotAdapter.Integration.Tests/")) or any(
            marker in normalized for marker in GODOT_MARKERS
        ):
            additional.add("godot")
        owner = project_for_path(normalized, projects)
        if owner:
            selected.add(owner)
            selected.update(reverse_test_closure(graph, {owner}))
        elif not normalized.startswith(("scripts/", "docs/", ".github/", ".scratch/")):
            unknown.append(normalized)

    if unknown:
        requires_full = True
        reasons.append("unclassified paths require the explicit full check: " + ", ".join(unknown))
    if any(path.startswith("Origo.TestSupport/") for path in paths):
        selected.update(name for name in projects if test_project(name))
        reasons.append("TestSupport is shared by test projects")
    selected = {name for name in selected if test_project(name)}
    if not paths:
        reasons.append("no tracked or working-tree changes; run scripts/check.sh full explicitly")
        requires_full = True
    if not selected and paths:
        reasons.append("no test project is attributable; use full for a complete answer")
        requires_full = True
    return {
        "schemaVersion": 1,
        "baseCommit": base,
        "configuration": "Release",
        "changedPaths": paths,
        "worktreeFingerprint": fingerprint(root, base, changes),
        "selectedProjects": sorted(selected),
        "additionalGates": sorted(additional),
        "reasons": reasons,
        "requiresFull": requires_full,
        "coverage": "existing-project-gates" if selected else "not-measured",
        "finalGateRequired": True,
    }


def print_plan(plan: dict, output: str | None) -> None:
    text = json.dumps(plan, ensure_ascii=False, indent=2) + "\n"
    if output:
        Path(output).write_text(text, encoding="utf-8")
    print(text, end="", flush=True)


def validate_saved_plan(saved: dict, current: dict) -> None:
    if saved.get("baseCommit") != current["baseCommit"] or saved.get("worktreeFingerprint") != current["worktreeFingerprint"]:
        raise RuntimeError("saved plan fingerprint is stale; rerun plan before executing checks")


def validate_quick_filter(test_filter: str) -> None:
    if (
        "Category=Benchmark" in test_filter
        or "Category~Benchmark" in test_filter
        or test_filter.strip() == "Benchmark"
    ):
        raise RuntimeError("quick filters must exclude Benchmark tests")


def validate_quick_project(project: str, projects: dict[str, Path]) -> None:
    if project not in projects or not test_project(project):
        raise RuntimeError(f"quick project must be a repository test project: {project}")


def run_command(root: Path, command: list[str]) -> None:
    print("$ " + " ".join(command), file=sys.stderr)
    result = subprocess.run(command, cwd=root, text=True, capture_output=True)
    if result.stdout:
        print(result.stdout, file=sys.stderr, end="")
    if result.stderr:
        print(result.stderr, file=sys.stderr, end="")
    if result.returncode:
        raise RuntimeError(f"command failed with exit code {result.returncode}")


def test_count(results_dir: Path) -> int:
    total = 0
    for trx in results_dir.glob("*.trx"):
        text = trx.read_text(encoding="utf-8", errors="replace")
        outcomes = re.findall(r"<UnitTestResult\b[^>]*\boutcome=\"([^\"]+)\"", text)
        total += sum(outcome not in {"NotExecuted", "Skipped"} for outcome in outcomes)
    return total


def run_test_project(root: Path, project: str, test_filter: str | None, coverage: bool) -> None:
    with tempfile.TemporaryDirectory(prefix="origo-check-") as temp:
        effective_filter = test_filter or "Category!=Benchmark"
        if "Category!=Benchmark" not in effective_filter:
            effective_filter = f"({effective_filter})&Category!=Benchmark"
        command = ["dotnet", "test", project, "--configuration", "Release", "--filter", effective_filter, "--logger", f"trx;LogFileName={project.replace('/', '_')}.trx", "--results-directory", temp, "-m:1"]
        if not coverage:
            command.extend(["-p:CollectCoverage=false"])
        started = time.monotonic()
        run_command(root, command)
        count = test_count(Path(temp))
        print(f"executed-tests: {count}", file=sys.stderr)
        print(f"elapsed-seconds: {time.monotonic() - started:.3f}", file=sys.stderr)
        if count == 0:
            raise RuntimeError(f"zero tests executed for {project}; check the explicit filter")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("plan", "quick", "affected", "full"))
    parser.add_argument("--repository")
    parser.add_argument("--base")
    parser.add_argument("--plan", help="previous plan JSON to validate before execution")
    parser.add_argument("--output")
    parser.add_argument("--project", required=False)
    parser.add_argument("--filter", dest="test_filter")
    args = parser.parse_args(argv)
    root = repository_root(args.repository)

    if args.mode == "full":
        run_command(root, ["bash", "scripts/ci.sh"])
        run_command(root, ["bash", "scripts/lint-commits.sh"])
        return 0

    if args.mode == "quick" and (not args.project or not args.test_filter):
        raise RuntimeError("quick requires explicit --project and --filter")
    if args.mode == "quick":
        validate_quick_filter(args.test_filter)

    base = args.base or default_base(root)
    changes = changed_paths(root, base)
    plan = classify(root, changes, base)
    if args.plan:
        try:
            saved = json.loads(Path(args.plan).read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError) as error:
            raise RuntimeError(f"cannot read plan {args.plan}: {error}") from error
        validate_saved_plan(saved, plan)
    print_plan(plan, args.output)

    if args.mode == "plan":
        return 0
    if args.mode == "quick":
        validate_quick_project(args.project, project_files(root))
        run_test_project(root, args.project, args.test_filter, coverage=False)
        print("coverage: not-measured", file=sys.stderr)
        return 0
    if plan["requiresFull"]:
        raise RuntimeError("plan is stale, unknown, or repository-wide; stop and run scripts/check.sh full explicitly")
    if not plan["selectedProjects"]:
        raise RuntimeError("affected found no test projects; run scripts/check.sh full explicitly")
    for project in plan["selectedProjects"]:
        run_test_project(root, project, "Category!=Benchmark", coverage=True)
    if "godot" in plan["additionalGates"]:
        run_command(root, ["bash", "scripts/godot-test.sh"])
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (RuntimeError, subprocess.CalledProcessError) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        raise SystemExit(1)
