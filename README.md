# Origo

Origo is a lightweight, engine-agnostic C# game framework. Its goal is to make
engine integration as simple as an RPG Maker-style workflow: describe game
content and scenes as data and strategies, and let Origo own the surrounding
orchestration and persistence.

## Goal

Developers and agents should not need to design entity containers, lifecycle
ordering, scene startup, session orchestration, or save/load plumbing. They
define the game's strategies, scenes, and data; Origo provides the frame loop,
entity lifecycle, sessions, persistence, console access, and the engine adapter.

The framework keeps those responsibilities behind stable contracts, so a game
can focus on what the game *is* rather than how its runtime is wired.

## Documentation

- [Manual](docs/README.en.md) — audience paths, module index, and documentation rules.
- [Capabilities](docs/usage/capabilities.en.md) — what the framework provides.
- [Architecture](docs/architecture/overview.en.md) — runtime and layer design.
- [Agent workflow](AGENTS.md) — repository development rules for agents.
- [Contributing](CONTRIBUTING.md) — development setup and contribution loop.

## License

MIT. See [LICENSE](LICENSE).
