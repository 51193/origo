<!-- docsync-pair: adr/0001-strategy-ordering -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Lifecycle strategy ordering constraints

> [↑ Back to Origo Manual](../README.en.md)

Lifecycle strategies declare relative Before / After ordering constraints. Constraints order lifecycle strategies on the same entity without requiring the referenced strategy to be mounted. Active, observer, and state machine strategies retain their own scheduling contracts. A single relative ordering model expresses ordering intent directly and avoids conflicting numeric and relational ordering semantics.

Constraints hold over the complete registration table. Entity ordering preserves transitive relations: if A precedes B and B precedes C, an entity mounting only A and C still executes A first. Unconstrained candidates are selected by ordinal index order so registration, mounting, and recovery input order do not affect the result. Process and bulk lifecycle hooks use the same direction; cleanup hooks are not automatically reversed.

Relations are declared only on strategy type attributes, with no per-entity overrides. Registration is limited to startup. After automatic discovery, or before the first lifecycle-strategy entity is created through direct SndWorld usage, references and cycles are validated and the registry is sealed. Later registrations fail explicitly. Unknown indices, non-lifecycle references, self-references, and cycles fail explicitly; cycle diagnostics include an actual cycle path. Saves contain mounted indices and recovery uses the same registered relations. Sealing prevents runtime registration from changing ordering across all entities.
