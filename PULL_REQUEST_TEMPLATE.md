## Summary

<!-- Brief description of the change -->

## Type of change

- [ ] Added
- [ ] Changed (includes BREAKING)
- [ ] Fixed
- [ ] Removed
- [ ] Internal (refactor, chore, test, docs — no user-facing change)

## AI Agent Usage

- [ ] This PR was created without AI agent assistance
- [ ] This PR was created with AI agent assistance

<!-- If yes, paste the original prompts below (no translation needed).
     Multi-round: all prompts or key prompts only. -->

## Checklist

- [ ] `bash scripts/test.sh` passes during iteration
- [ ] `bash scripts/ci.sh` passes after the final commit (lint-scripts + format + doc-sync + build/test + coverage + benchmarks + Godot integration)
- [ ] Post-commit `bash scripts/lint-commits.sh` passes
- [ ] New public API has corresponding behavior tests
- [ ] Bug fix has regression test (red → green)
- [ ] `docs/` mirror updated (interface list, design decisions, usage docs, and bilingual mirror README file lists for any `.cs` under `SourceMirrorRoots`)
- [ ] `DocSyncTool generate` run; generated hubs and `.sync-status.json` committed
- [ ] `CHANGELOG.md` updated under `[Unreleased]`
- [ ] Breaking changes are prefixed `BREAKING:` and migration path is documented
