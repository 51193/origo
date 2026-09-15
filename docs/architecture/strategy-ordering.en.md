<!-- docsync-pair: architecture/strategy-ordering -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Lifecycle strategy ordering constraints

> [↑ Back to Architecture](README.en.md)

Lifecycle strategies declare relative Before / After ordering constraints. Constraints order lifecycle strategies on the same entity without requiring the referenced strategy to be mounted. Active, observer, and state machine strategies retain their own scheduling contracts. A single relative ordering model expresses ordering intent directly and avoids conflicting numeric and relational ordering semantics.

Constraints hold over the complete registration table. Entity ordering preserves transitive relations: if A precedes B and B precedes C, an entity mounting only A and C still executes A first. Unconstrained candidates are selected by ordinal index order so registration, mounting, and recovery input order do not affect the result. Process and bulk lifecycle hooks use the same direction; cleanup hooks are not automatically reversed. Ordering constraints express dependency direction, not resource-stack acquisition/release; keeping every batch phase in one direction avoids two order semantics across hooks and keeps save, recovery, and quit cleanup on one deterministic order.

Relations are declared only on strategy type attributes, with no per-entity overrides: per-entity constraints would have to be persisted and rebuilt on recovery, adding significant complexity and mismatch risk for no current benefit. Registration is limited to startup. After automatic discovery, or before the first lifecycle-strategy entity is created through direct SndWorld usage, references and cycles are validated and the registry is sealed. Later registrations fail explicitly. Unknown indices, non-lifecycle references, self-references, and cycles fail explicitly; cycle diagnostics include an actual cycle path. Saves contain mounted indices and recovery uses the same registered relations. Sealing prevents runtime registration from changing ordering across all entities.

## Known Boundaries and Evolution Options

### Complete registry includes unmounted strategies

Current behavior: `SealRegistration()` validates `Before` / `After` over the complete registration table, so a target strategy must be registered. The target does not need to be mounted, but a missing target fails startup even when the strategy declaring the constraint is never mounted on any entity.

Rationale: one validation and freeze gives every entity the same deterministic order, supports forward references, and avoids rebuilding a graph per entity during recovery.

Known issues:
- Optional strategy packs become coupled at startup: if plugin A references an index owned by plugin B, a missing B fails the whole game even when A is never mounted.
- The failure happens during registration, far from "which entity actually needs this relation", making diagnosis harder.
- Removing an optional plugin requires removing or rewriting every constraint that referenced it.

Fast evolution options:
- Optional targets: give constraint targets explicit optional semantics or a separate resolution policy; a missing target must have a deterministic, observable fallback, never a silent ignore.
- Per-entity closure validation: record declarations at registration, then validate and sort only the transitive closure of the actually mounted set during entity recovery/mounting; the save format can stay unchanged, but per-entity graphs and caches are required.
- Scoped constraints: restrict constraint targets to one plugin/namespace scope and require cross-scope references to be explicit.
- Keep the current behavior and add diagnostics: report the strategy, edge, and potentially affected entities on registration failure, and surface optional-dependency risk through analyzers/documentation.

Re-evaluation signal: optional strategy packs or third-party extensions hit constraint-coupling failures that cannot be resolved by adjusting the registration set or declarations.

### Global topological rank projection and Ordinal semantics

Current behavior: the complete registry is Kahn-topologically sorted into one global total order, with candidates selected by the smallest `StringComparer.Ordinal` key; entity order is that total order restricted to the mounted set. Direct and transitive `Before` / `After` constraints are preserved consistently across all entities.

Known issues:
- "Unconstrained strategies use ordinal order" describes global candidate selection only; it does not guarantee that two incomparable mounted strategies keep ordinal order on every entity.
- Any constraint that involves a registered but unmounted strategy can change the relative order of two mounted strategies; adding or removing an unrelated registration can also change entity order.
- Entity order therefore must not be read as "local insertion order plus local constraints"; it is a projection of a globally frozen order.

Fast evolution options:
- Per-entity constraint projection: replay the complete partial order (including transitive closure) over the mounted set during recovery, then run a stable topological sort; the ordering objective for incomparable nodes must be defined explicitly (for example, minimizing inversions against ordinal order).
- Per-entity ordering configuration: persist explicit order overrides or groups in entity metadata; the cost is higher save-format, recovery, and validation complexity.
- Keep the global total order: make it explicit that ordinal is only a global candidate tie-break, and require order-sensitive entities to declare constraints instead of relying on projection coincidence.

Re-evaluation signal: ordering needs that cannot be expressed stably with explicit constraints keep recurring, or entity-order audits show systematic disagreement between projected order and team expectations.
