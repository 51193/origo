# Review and Pull-Request Process

> [↑ Back to Origo Manual](README.en.md)

This file is the process authority for review rounds, remediation delivery, and
pull-request units. [AGENTS.md](../AGENTS.md) remains the mandatory gate;
release rules live in [release-process](release-process.en.md), documentation
rules in [META](META.en.md), and issue handling in
[issue-tracker](agents/issue-tracker.md).

## Review rounds

- A review round is any audit or review of a change — a full-project review, an
  architecture audit, or a pull-request review — that produces findings.
- Every finding records severity, `file:line`, the trigger or entry path, the
  impact, the contract or documentation it violates, the existing tests checked,
  and the relevant git history and design intent. A claim without a concrete
  trigger is not a finding.
- Severity is evidence-based: P1 is data loss, a security-boundary breach, or a
  crash on a reachable path; P2 is incorrect reachable behavior or a contract
  violation; P3 is a real but non-blocking defect with no data loss, security
  breach, crash, or incorrect reachable behavior. Pre-existing lower-severity
  findings are fixed when cheap and otherwise tracked.
- Apply the full-chain reading rule before recording a defect; deliberate
  cross-module design is not debt.
- Findings and blind-audit verdicts stay in a local review record (a
  `.scratch/review-<topic>/` directory or the open `_origo_local` chapter) until
  they become durable. Each verdict records the audited commit SHA. Fixed
  findings move into the change's tests and docs; deferred findings link to their
  issue from the record.

## Blind audit

- P1/P2 findings from a review round, and the fixes those findings produce, are
  independently blind-audited before merge.
- The blind auditor is the maintainer or a fresh session that receives only the
  branch or diff at a recorded SHA, the recorded findings, and the acceptance
  criteria. The authoring session does not audit its own findings.
- The auditor records confirm, refute, or partial with evidence derived from the
  repository, together with the merge-tip SHA and their identity, and the GitHub
  approval or a maintainer attestation for the verdict. A record without an
  independent approval or attestation is not a verdict. False positives are
  downgraded or dropped, never fixed on speculation; a refuted finding is noted
  in the record.
- Any change to an audited branch or diff after its verdict requires blind
  re-audit of the new tip and a new record; the record names the merge-tip SHA.
  Re-checking the pull-request body is not enough.
- A change with no P1/P2 finding, including a docs-only or test-only change, uses
  one independent reviewer. A multi-module exception under "Delivery units"
  requires the same independent reviewer's recorded scope confirmation.

## Delivery units: one module or one cohesive behavior per pull request

- One pull request carries one module or one cohesive behavior. A review
  remediation that spans unrelated modules is split into separate pull requests
  in merge order; each is independently reviewable and verifiable (source, tests,
  docs, and Changelog for its scope). Stack a child on the parent only when it
  cannot build on `main`.
- Here a module is a package or area that owns its documentation entry and tests,
  such as `Origo.Core.Kernel/Runtime`, `Origo.GodotAdapter`,
  `tools/ApiInventoryTool`, or `scripts/` with its workflows. For a
  documentation change, the topic and target manual entry are the module;
  unrelated documentation is split.
- A change that mixes unrelated areas (for example runtime behavior plus an
  unrelated release-tooling change, or unrelated meta documentation) is split.
  The module documentation and `CHANGELOG.md` entry for the changed behavior stay
  in the same pull request. Shared indexes and generated pages are touched
  minimally and resolved in merge order.
- A stacked or dependent pull request names its parent, child, and merge order in
  the body. Base a child on the parent only when it cannot build on `main`;
  after the parent squash-merges, retarget the children to `main` and rebase;
  delete the parent branch only after no child still depends on it.
- A pull request that grows across modules before merge is split and the new tip
  is re-audited.
- A behavior that genuinely spans shell and kernel contracts may stay in one pull
  request only when it is not separable; the body states the modules and why one
  review unit is required, and an independent reviewer records the scope
  confirmation before merge.
- Pull requests deliver accepted work; they are not the backlog or triage
  surface.

## Fix verification and test expansion

- Every bug fix has a red-first real-path regression for the reported symptom;
  every behavior change updates the existing behavior tests.
- Expand coverage around the changed behavior: adjacent boundaries, error
  branches, and cross-platform paths, proportional to the change. Avoid
  tautological assertions, mock-only checks, and tests that pass on the
  unmodified code for the bug's own symptom.
- An introduced finding is fixed before merge, including P1/P2 findings.
  Deferral is only for pre-existing findings, including those in a module
  explicitly excluded from the change's scope; an introduced finding is never
  deferred.
- The full local gate (`scripts/bash/ci.sh`) runs after the final commit and the
  remote required checks stay green; a green partial gate does not replace it.
- After any amend, rebase, or force-push, re-verify the pull-request body's
  commands, coverage numbers, test counts, and file lists against the final
  commit, and re-audit any audited fix that changed.
- A stale claim is a defect in the change record.

## Deferred findings, security, and labels

- A confirmed pre-existing finding that is not implemented becomes an issue
  before the review closes. Include the evidence, current behavior, acceptance
  criteria, and the current state role.
- Introduced findings are fixed before merge, not deferred.
- A pre-existing incorrect behavior, including one in a module explicitly
  excluded from the change's scope, uses the `bug` category label; a
  pre-existing behavior-preserving cost deferral uses `tech-debt`.
- A missing design decision uses the Design-Gated Task template and its
  `blocked-design` gate.
- A security-sensitive finding stays in private Security Advisory tracking until
  coordinated disclosure; it becomes a public issue only after disclosure. See
  [SECURITY.md](../SECURITY.md).
- The `tech-debt` label definition lives in
  [triage-labels](agents/triage-labels.md); issue-filing mechanics live in
  [issue-tracker](agents/issue-tracker.md).

## Pull-request text and AI assistance

- The title follows the Conventional Commit subject rules; the squash body is the
  pull-request body and follows the commit-message body rules in
  [META](META.en.md#git-commit-message-format).
- Describe the change for readers who did not follow the work: current behavior,
  impact, compatibility and migration, and the verification evidence.
- Do not include internal review-round names, severity identifiers, agent
  conversation, prompts or prompt content (public or private), local work-buffer
  content, or other internal process metadata. Those belong in the review record.
  The audited-SHA/verdict record required by this process is repository-facing
  evidence and is allowed.
- AI assistance may be disclosed at a high level (tools or models).

## Merge and closeout

- Merge only when the required checks are green, introduced findings are fixed,
  pre-existing remaining findings are fixed or tracked as issues, the
  pull-request body matches the final commit, and every audit or scope verdict
  records the merge-tip SHA with an independent approval or maintainer
  attestation.
- Origo uses squash merge. Delete the branch after merge once no child still
  depends on it.
- If an `_origo_local` book was opened, apply the local work-buffer rule in
  `AGENTS.md` after merge; close issues the change resolves.

---
[↑ Back to Origo Manual](README.en.md)
