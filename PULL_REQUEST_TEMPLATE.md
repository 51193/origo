## Summary

<!-- Brief description of the change -->

## Type of change

- [ ] Added
- [ ] Changed (includes BREAKING)
- [ ] Deprecated
- [ ] Fixed
- [ ] Removed
- [ ] Security
- [ ] Internal (refactor, chore, test, docs — no user-facing change)

## AI Agent Usage

- [ ] This PR was created without AI agent assistance
- [ ] This PR was created with AI agent assistance

<!-- If yes, paste the original prompts below (no translation needed).
     Multi-round: all prompts or key prompts only. -->

## Checklist

- [ ] `bash scripts/bash/check.sh quick` or `affected` used for
      iteration; `bash scripts/bash/test.sh` passes before commit
- [ ] `bash scripts/bash/ci.sh` passes after the final commit:
      lint-scripts + format + API baseline + tests + benchmarks +
      Godot integration + package consumer smoke
- [ ] Post-commit `bash scripts/bash/lint-commits.sh` passes (the
      pre-push hook also enforces it; remote CI does not)
- [ ] New public API has corresponding behavior tests
- [ ] Bug fix has regression test (red → green)
- [ ] English documentation updated where interface lists, design
      decisions, or usage docs changed
- [ ] `CHANGELOG.md` updated under `[Unreleased]` for every
      user-visible change
- [ ] Breaking changes are prefixed `BREAKING:` and the migration path
      is documented
