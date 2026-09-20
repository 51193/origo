# Domain Docs

Origo is a single-context repository. Its bilingual manual is the
domain documentation. `AGENTS.md` is the mandatory entry point and
takes precedence over this skill configuration.

## Before exploring code or writing a ticket

1. Read `AGENTS.md`, `docs/META.zh.md` or `.en.md`, and
   `docs/release-process.zh.md` or `.en.md` as required there.
2. Start at `docs/README.zh.md` or `.en.md` and follow the relevant
   module README chain, including upstream, downstream, and related
   facilities.
3. Read `docs/architecture/overview.zh.md` or `.en.md` for the system
   model, plus relevant decisions under `docs/architecture/`.
4. Use `docs/usage/` for consumer behavior and the matching module
   and test documents for implementation and verification.

## Recording domain language and decisions

Use terminology from the closest module and architecture documents.
When a term is settled, update its relevant bilingual pair in `docs/`;
put framework-wide model terms in the architecture overview. Record
durable architectural decisions under `docs/architecture/` as
`.zh.md`/`.en.md` pairs and link them from that directory's README pair.
Apply the DocSync and full development loop in `AGENTS.md`.

Origo intentionally uses neither root `CONTEXT.md` nor `docs/adr/`:
they would duplicate the current manual and architecture location.
`CONTEXT-MAP.md` and per-context layouts are not used. When a skill
suggests these default paths, use this repository layout instead.
