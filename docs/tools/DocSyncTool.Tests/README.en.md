<!-- docsync-pair: tools/DocSyncTool.Tests/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# DocSyncTool Tests

> [↑ Back to Origo Manual](../../README.en.md)

Test project for the `DocSyncTool` tool (`tools/DocSyncTool/`), located at
`tools/DocSyncTool.Tests/`. It verifies the tool's four core commands —
`generate`, `validate`, `init` — and config loading against isolated
temporary repo scaffolds.

## Capabilities

| Unit under test | Covered behavior |
|-----------------|------------------|
| `Validator` | Bilingual pair revision consistency, missing language files, cross-language / bare `.md` / broken links, missing metadata headers and revision-bump reminder comments, pair id mismatching the file path, invalid revision values; code blocks / inline code and external URL links are exempt |
| `Generator` | Per-directory `README.md` navigation hub generation, idempotency (no rewrite when unchanged), `.sync-status.json` status determination (`synced` / `zh-ahead` / `missing-en`), recursive subdirectory hubs, skipping doc-less directories, derived defaults for files without metadata |
| `Migrator` | `.md` → `.zh.md` rename with metadata injection, bare `.md` link rewrite to `.zh.md`, external URL links are never rewritten, skipping already-suffixed / already-migrated / conflicting-target files, nested-directory pair derivation |
| `Config` | Config parsing (case-insensitive keys), language code validation (rejects whitespace / slashes / backslashes), missing config file and invalid JSON failure modes |
| `DocFile` | Language suffix extraction and pair id derivation |
| `Program` | Command dispatch and exit codes, unknown-command usage, FATAL when no repo root is found (CWD-sensitive tests run in a serialized collection) |

## Conventions

- Flat namespace `DocSyncTool.Tests`, matching the repository's other test
  projects (`.editorconfig` exempts this path from IDE0130/CA1062).
- Reaches the tool's `internal` types via `InternalsVisibleTo`.
- Every test builds a repo scaffold in its own temp directory (with
  `AGENTS.md`, `docs/` and `tools/DocSyncTool/docsync-config.json`); the
  real repository is never touched.
- Coverage gate: line coverage ≥ 90% (`ThresholdStat=total`), matching the
  other test projects.

[↑ Back to Origo Manual](../../README.en.md)
