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

## Category labels

Category labels are independent of the five state roles and are applied with a
state role.

| Category | Label | Meaning |
|----------|-------|---------|
| Tech debt | `tech-debt` | Confirmed pre-existing finding whose behavior is correct but whose change costs more than it returns |
| Bug | `bug` | Incorrect behavior report, including pre-existing or out-of-scope findings that are deferred or tracked rather than fixed in the current change |

Deferred-finding lifecycle and the rules for applying `tech-debt` are owned by
[review-process](../review-process.en.md#deferred-findings-security-and-labels);
issue-filing mechanics are in [issue-tracker](issue-tracker.md).
