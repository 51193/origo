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

- [ ] `bash scripts/check.sh quick` or `affected` used for iteration, and `bash scripts/test.sh` passes before commit
- [ ] `bash scripts/ci.sh` passes after the final commit (lint-scripts + format + build/test + coverage + benchmarks + Godot integration)
- [ ] Post-commit `bash scripts/lint-commits.sh` passes (the local pre-push hook also enforces it; remote CI does not enforce commit lint)
- [ ] New public API has corresponding behavior tests
- [ ] Bug fix has regression test (red → green)
- [ ] English documentation updated where interface lists, design decisions, or usage docs changed
- [ ] `CHANGELOG.md` updated under `[Unreleased]`
- [ ] Breaking changes are prefixed `BREAKING:` and migration path is documented
