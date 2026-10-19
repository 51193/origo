# Triage Labels

Matt Pocock's engineering skills use these five state roles. The
`51193/origo` GitHub labels and local `Status:` values are identical.

| Role | Label / Status | Meaning |
|------|----------------|---------|
| `needs-triage` | `needs-triage` | Maintainer evaluation needed |
| `needs-info` | `needs-info` | Waiting for reporter information |
| `ready-for-agent` | `ready-for-agent` | Fully specified agent work |
| `ready-for-human` | `ready-for-human` | Human implementation needed |
| `wontfix` | `wontfix` | Will not be actioned |

The design-task template uses `State` for these five roles and a separate
`Gate` line for the design gate (`blocked-design` / `waiting-signal`); they
are different axes and both are updated when a skill moves an issue.

When a skill changes a role, update the GitHub label and the local
counterpart together when a local counterpart exists. Existing issue category
and planning labels stay independent of these state roles.
