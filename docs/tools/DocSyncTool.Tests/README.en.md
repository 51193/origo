<!-- docsync-pair: tools/DocSyncTool.Tests/README -->
<!-- docsync-revision: 6 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# DocSyncTool Tests

> [↑ Back to Origo Manual](../../README.en.md)

Test project for the `DocSyncTool` tool (`tools/DocSyncTool/`), located at
`tools/DocSyncTool.Tests/`. It verifies the tool's two commands —
`generate` and `validate` — plus config loading against isolated
temporary repo scaffolds.

## Capabilities

| Unit under test | Covered behavior |
|-----------------|------------------|
| `Validator` | Bilingual pair revision consistency, missing language files, cross-language / bare `.md` / broken links, missing metadata headers and managed-revision reminder comments, pair id mismatching the file path, invalid revision values; code blocks / inline code and external URL links are exempt; source-directory → doc-directory structural mirror and file-list checks (including SourceDocOverrides exception mappings) |
| `Generator` | Per-directory `README.md` navigation hub generation, idempotency (no rewrite when unchanged), `.sync-status.json` status determination (`synced` / `zh-ahead` / `missing-en`), recursive subdirectory hubs, skipping doc-less directories, derived defaults for files without metadata, and git-derived `docsync-revision` planning (multi-commit pushes, translation catch-up, metadata-only commits, uncommitted local edits) |
| `Config` | Config parsing (case-insensitive keys), language code validation (rejects whitespace / slashes / backslashes), missing config file and invalid JSON failure modes |
| `DocFile` | Language suffix extraction and pair id derivation |
| `Program` | Command dispatch and exit codes, unknown-command usage, FATAL when no repo root is found (CWD-sensitive tests run in a serialized collection) |

## Conventions

- Flat namespace `DocSyncTool.Tests`, matching the repository's other test
  projects (`.editorconfig` exempts this path from IDE0130/CA1062).
- Reaches the tool's `internal` types via `InternalsVisibleTo`.
- **Test helper `ConsoleOutputCapture`**: redirects `Console.Out`/`Console.Error`
  to silent writers so the tool's expected output (the negative tests'
  "Validation FAILED" diagnostics and generate progress lines)
  does not pollute the test-runner log — "Validation FAILED" looks like a
  build failure in CI logs. Because redirecting the process-global console
  streams affects all tests, the capturing test classes
  (`ProgramTests`/`ValidatorTests`/`GeneratorTests`/
  `GitRevisionTests`/`GitRevisionAdvancedTests`) run in the serialized
  `DocSyncToolConsoleCapture` collection.
- Every test builds a repo scaffold in its own temp directory (with
  `AGENTS.md`, `docs/` and `tools/DocSyncTool/docsync-config.json`); the
  real repository is never touched. Git-revision tests additionally `git
  init` their scaffold and exercise the same commit/replay logic used by
  local `generate` and CI.
- Coverage gate: line coverage ≥ 90% (`ThresholdStat=total`), matching the
  other test projects.

[↑ Back to Origo Manual](../../README.en.md)
