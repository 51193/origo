# Issue tracker: GitHub and local Markdown

Published Origo issues live in GitHub Issues at `51193/origo`. Skills that
need offline preparation can keep a local Markdown counterpart at
`.scratch/issues/<number>.md`; local counterparts are working data, not
repository state, so a fresh clone starts without them. The GitHub number is
the stable identity;
use the explicit `-R 51193/origo` option with every `gh` command so the
target repository never depends on the clone's remote configuration.

## Authority and synchronization

GitHub is authoritative for the published title, body, open/closed state,
labels, and comments. When a skill keeps local files, they provide a working
copy and space to stage changes. Keep the local `GitHub-Updated-At` value from the
last fetch. Before publishing local edits, compare it with the current
remote value; reconcile concurrent changes before writing. After a
GitHub write, refresh the counterpart when one exists. Do not silently replace a local
file marked `Sync: pending`.

- **Fetch/list**: `gh issue list -R 51193/origo --state all --limit 500
  --json number,title,state,labels,updatedAt,url`; use `gh issue view
  <number> -R 51193/origo --json
  number,title,body,state,labels,comments,updatedAt,url` for full detail.
  Refresh local counterparts that exist, including closed issues. A local issue
  without a GitHub number is a draft, not a published issue.
- **Create**: draft under `.scratch/issues/drafts/<slug>.md`. Once the
  title and body are reviewable, create with `gh issue create -R
  51193/origo --title "..." --body-file <file>`, then move the draft to
  `.scratch/issues/<number>.md` and record the returned URL.
- **Update**: when a local counterpart exists, edit it first and mark `Sync: pending`.
  Compare the remote `updatedAt`, apply the change with `gh issue edit
  <number> -R 51193/origo` or `gh issue comment <number> -R 51193/origo
  --body-file <file>`, then refresh the local file and clear pending.
- **Triage**: use the roles in `docs/agents/triage-labels.md`. Apply
  labels with `gh issue edit <number> -R 51193/origo --add-label "..."`
  or `--remove-label "..."`, then update the local label list and
  `Status:` field when a counterpart exists.
- **Close**: `gh issue close <number> -R 51193/origo`, then refresh the
  local counterpart when it exists.

Local issue files stay in a dedicated `.scratch/issues/` subtree.
They are local working data and are excluded from commits. The separate
`_origo_local/` single-book buffer retains its AGENTS-defined handoff
purpose; it is not an issue tracker.

## Pull requests as a triage surface

**PRs as a request surface: no.**

## When a skill says "publish to the issue tracker"

Create or update the GitHub issue in `51193/origo` and synchronize its
local counterpart when one exists. For offline work, keep a draft or pending file and
report that publication remains outstanding.

## When a skill says "fetch the relevant ticket"

Read the GitHub issue and, when present, its local counterpart. Refresh the local copy
if the remote changed and no local update is pending.

## Issue body hygiene

Issue acceptance text is executable guidance for future agents, so it must
match the current repository rules rather than a superseded process:

- Documentation is English-only. Do not require "bilingual docs" or DocSync in
  acceptance criteria; name the relevant `docs/**/README.en.md` entry and the
  current documentation gate instead.
- Use current gate names (`scripts/test.sh`, post-commit `scripts/ci.sh`,
  `scripts/lint-commits.sh`, and `scripts/validate-agent-docs.py` for
  `AGENTS.md`/agent-doc changes). A removed script is not a valid acceptance
  gate.
- A `Blocked by` edge to a closed issue is stale. Re-triage the issue and
  remove or replace the edge; do not leave a closed blocker in the body.
- Keep evidence paths current. When a source tree moves, update the issue's
  evidence links in the same hygiene pass.
- Waiting-signal trackers must not claim an active milestone or unchecked
  deliverables. Record the gate, the activation signal, and the re-triage
  condition; do not start items without that signal.

## Wayfinding operations

A map is one GitHub issue labelled `wayfinder:map`, with child issues
linked as sub-issues. Where sub-issues are unavailable, list children
in the map body and put `Part of #<map>` in each child. Use
`wayfinder:<type>` labels for `research`, `prototype`, `grilling`, and
`task`. Prefer native issue dependencies; otherwise record
`Blocked by: #<n>, #<n>` in the child body. Claim by assignment, resolve
with an answer comment and close, then update the map's decisions.
Keep local counterparts in sync when the workflow uses them.
