## Summary

<!-- Brief description of the change -->

## Scope

- Primary module / area: <!-- e.g. Origo.Core.Kernel/Runtime -->
- [ ] One module or one cohesive behavior; unrelated changes were split
- [ ] Multi-module exception: an independent reviewer recorded why
      splitting is impossible and the merge-tip SHA (required when more
      than one module/area is touched)
- Why this is one review unit: <!-- required for the exception; the
  reviewer records the scope confirmation and merge-tip SHA -->
- Parent/child PR when stacked: <!-- #... (merge order) -->

## Type of change

- [ ] Added
- [ ] Changed (includes BREAKING)
- [ ] Deprecated
- [ ] Fixed
- [ ] Removed
- [ ] Security
- [ ] Internal (refactor, chore, test, docs - no user-facing change)

## Verification

<!-- Commands run, results (tests/coverage/benchmarks), and the affected
     files or areas. These claims must match the final commit. -->

- Blind-audit record (reviewer identity, merge-tip SHA, verdict with
  independent approval or maintainer attestation): <!-- required when a
  review round required blind audit -->

## AI Agent Usage

- [ ] This PR was created without AI agent assistance
- [ ] This PR was created with AI agent assistance (optional:
      tools/models at a high level)

<!-- Do not paste prompts or prompt content (public or private),
     internal review-round names, severity IDs, agent conversation, or
     local work-buffer content. Describe the change for reviewers; AI
     assistance may be disclosed at a high level. -->

## Checklist

- [ ] `bash scripts/bash/check.sh quick` or `affected` used for
      iteration; `bash scripts/bash/test.sh` passes before commit
- [ ] `bash scripts/bash/ci.sh` passes after the final commit:
      lint-scripts + format + API baseline + tests + benchmarks +
      Godot integration + package consumer smoke
- [ ] Post-commit `bash scripts/bash/lint-commits.sh` passes (the
      pre-push hook also enforces it; remote CI does not)
- [ ] New public API has corresponding behavior tests
- [ ] Bug fix has regression test (red to green)
- [ ] English documentation updated where interface lists, design
      decisions, or usage docs changed
- [ ] `CHANGELOG.md` updated under `[Unreleased]` for every
      user-visible change
- [ ] Breaking changes are prefixed `BREAKING:` and the migration path
      is documented
- [ ] PR title/body describe repository-facing behavior/impact; evidence
      matches the final commit
- [ ] PR body contains no internal review-round, severity,
      agent-dialogue, prompt, or local work-buffer content (the audited
      SHA/verdict record is allowed)
- [ ] Independent reviewer identity, approval/attestation, and merge-tip
      SHA recorded (required when blind audit is required or a
      multi-module exception applies)
