<!-- docsync-pair: architecture/agent-friendly/api-inventory -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Machine API inventory: navigate from compilable facts to design contracts

> [↑ Back to the Agent Friendly investigation](README.en.md)

Investigation date: 2026-09-18; repository observation baseline: `cdba5e4`. This report proposes a machine-readable inventory and query tool. Its schema, commands, and generated-artifact gates are unimplemented examples, not current API promises.

## 1. What Origo specifically lacks

**Observation:** [Agent Reference](../../usage/agent-reference.en.md) manually reproduces complete C# signatures for `ISndEntity`, narrow interfaces, `ISndContext`, sessions, and state machines to help game-development agents. [Abstractions/Snd](../../Origo.Core/Abstractions/Snd/README.en.md) separately maintains member counts, property types, and responsibilities. DocSync validates language pairs, revisions, links, and mirror file lists, but does not compile these code blocks or compare descriptions with the effective public member set. See [DocSync test capabilities](../../tools/DocSyncTool.Tests/README.en.md) and [Validator implementation](../../../tools/DocSyncTool/Validator.cs).

A concrete example must not be mislabeled a defect: the capability list mentions nine narrow roles while the architecture overview mentions ten companions. The narrow-interface documentation explicitly defines **nine Snd roles plus `IStateMachineContext`, giving ten companion properties**. Path properties and `Bootstrap` also exist, so counting every property is not the companion count. An agent reading one summary may confuse the categories; that does not establish an interface-design error. A machine inventory can list exact properties while a human capability classification identifies companions, avoiding repeatedly copied counts. See [Capabilities](../../usage/capabilities.en.md), [Architecture overview](../overview.en.md), and [ISndContext source](../../../Origo.Core/Snd/ISndContext.cs).

Another example is [TypedData](../../Origo.Core/Snd/Metadata/README.en.md): public accessors such as `TryGetInt32` and conversion operators are generator outputs. Searching handwritten `.cs` files for `public` misses them. Conversely, [generator documentation](../../Origo.SourceGeneration/README.en.md) states that Adapter's entire `TypedDataLayeredExtensions` class is internal; public methods inside it are not game-facing public API. The tool must determine **effective external accessibility**.

Agent Reference uses `[Test]` templates while this repository uses xUnit `[Fact]`. The reference targets game developers, and the strategy test framework can work with different assertion frameworks; this alone does not prove a repository testing error. Examples should identify consumer/maintainer audiences and runners, with compilable checks for the corresponding combinations. Inventories establish signatures; example tests establish usage.

## 2. What to generate and what to preserve

Propose three layers: compiler-derived `api.json` for type and signature facts; a small human-maintained `capabilities.json` for business capabilities, preferred entry points, scope, orchestration rationale, and documentation links; and a query tool joining both to return task-specific excerpts. Human capability bindings must resolve to effective public symbols or fail immediately.

The inventory does not replace `<summary>`, `<inheritdoc />`, exception contracts, or lifecycle explanations. It cannot establish that a method is the correct access path. Entity destruction, for example, belongs through `entity.OwningSession.RequestKillEntity(name)`. Listing internal scene-host removal operations more prominently could encourage agents to bypass hooks and resource lifecycles. Cross-module allowlists, interface segregation, deferred timing, and save-integrity rationale remain in human documentation. Machines can provide facts; semantics and design reasoning must remain readable and searchable.

[AGENTS.md](../../../AGENTS.md) prohibits DocFX/Sandcastle-style API websites. This proposal is a JSON tool fact source and manual-validation facility, not a second human API website. Before implementation, record its scope and governance in AGENTS/META to prevent tool output and `docs/` becoming competing authorities.

## 3. Why the effective compiled surface matters

Use the same evaluated inputs as the actual MSBuild build: TFM, Configuration, conditional constants, references, AdditionalFiles, analyzer configuration, and Source Generators. Roslyn can expose the Compilation updated by generators and serialize its symbol model. The official `GeneratorDriver.RunGeneratorsAndUpdateCompilation` adds generated trees to its output compilation. A design-time view without generators cannot stand in for final output. [Roslyn GeneratorDriver](https://github.com/dotnet/roslyn/blob/main/src/Compilers/Core/Portable/SourceGeneration/GeneratorDriver.cs)

Build normally first, then extract semantics and source locations from a **final Compilation matching that build**, cross-checking signatures against the actual reference assembly. Reference assemblies retain metadata needed for compilation against an API without method implementations. Do not execute production DLL `ModuleInitializer` methods merely to discover API, especially when Godot registrations or native dependencies could enter the inventory process. [Reference assemblies](https://learn.microsoft.com/en-us/dotnet/standard/assembly/reference-assemblies)

The effective external set includes public members of externally accessible types and protected/protected-internal members available to external derived types. Check accessibility of all containing types, sealed types, and construction/derivation boundaries. Private-protected members are limited to same-assembly derived types and must not masquerade as cross-assembly consumer API. Merge partial declarations into one symbol; retain interface and base inheritance, distinguishing declared and inherited members in queries. Preserve operators, extension methods, individual accessor visibility, nullable annotations, generic constraints, ref/out/in, and optional defaults. Roslyn `ISymbol` supplies `DeclaredAccessibility`, `ContainingSymbol`, and source locations; a single public flag does not establish complete external usability. [Roslyn ISymbol](https://github.com/dotnet/roslyn/blob/main/src/Compilers/Core/Portable/Symbols/ISymbol.cs)

Output Core, ConsoleBridge, and GodotAdapter separately with a declared configuration matrix. The generator project currently targets netstandard2.0, and its compiler API version moves together with the SDK; an inventory tool must not arbitrarily upgrade Roslyn alone. Extract Adapter in a matching Godot SDK build environment. Core output must contain no Godot types. Failed builds, generator diagnostics, and missing configurations must produce explicit errors, not half-complete inventories marked successful.

## 4. Schema and query examples

The following JSON is a proposed excerpt illustrating a contract, not actual tool output:

```json
{
  "schemaVersion": 1,
  "assembly": "Origo.Core",
  "build": {"tfm": "net10.0", "configuration": "Release"},
  "symbols": [
    {
      "id": "P:Origo.Core.Snd.ISndContext.Save",
      "kind": "property",
      "containingType": "Origo.Core.Snd.ISndContext",
      "accessibility": "public",
      "effectiveExternalAccessibility": "public",
      "type": "Origo.Core.Abstractions.Snd.ISndSaveOperations",
      "accessors": {"get": "public"},
      "origin": "source",
      "source": "Origo.Core/Snd/ISndContext.cs",
      "documentation": "docs/Origo.Core/Abstractions/Snd/README.en.md"
    }
  ]
}
```

Required complete fields also include stable overload identity, namespaces, bases/interfaces, generics and constraints, parameter/return types and nullable annotations, static/virtual/abstract/override modifiers, extension receivers, defaults, properties/events/indexers, operators, applicable configurations, generator identity and hint name, original summaries or references to them, and source/documentation associations. Preserve both symbol ID and canonical signature; method names cannot identify overloads. Bind language-specific documentation independently. Generated-source locations use generator plus hint name rather than machine-specific absolute `obj` paths.

Proposed consumer queries:

```bash
# Unimplemented commands
origo api query --capability save.load --audience game --language en
origo api query --type Origo.Core.Snd.ISndContext --declared-only --json
origo api diff --base <inventory> --current <inventory> --json
```

`save.load` should return the actual entry signature, suggested usage, deferred-request timing, load-failure semantics, runtime observation, real examples, and test entry points. The latter come from human capability bindings and documentation; Roslyn cannot infer them. Symbol existence alone does not establish thread safety, correct persistence, or correct gameplay rules.

## 5. Determinism and gates

Pin SDK/Roslyn/Godot, package resolution, and configuration. Sort symbols, normalize separators and encoding, and exclude timestamps and machine-specific paths. Record input hashes, versions, and baseline; store commit/dirty state as execution metadata rather than mixing every commit change into canonical API signature diffs. Identical inputs must generate identical bytes. Compiler determinism is a foundation, while references, analyzers, and directories are also inputs; the inventory tool must implement JSON normalization itself. [C# deterministic compilation](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-options/code-generation)

Proposed CI runs generate, validate, and verify committed output. Define ownership when integrating with DocSync state, without manually editing hubs or revisions. Public-surface changes require aligned capability bindings, human design contracts, usage examples, and behavior tests. Breaking changes enter the existing release process with `BREAKING:` classification. API diffs prompt and enforce alignment; they do not justify compatibility layers. Consumer examples compile with their declared runners, maintainer templates use repository xUnit, and Godot examples use the corresponding SDK/headless environment.

## 6. Phased acceptance and product value

First emit Core/ConsoleBridge JSON and query one real capability; then cover generators, Adapter, and the configuration matrix; finally integrate DocSync and compilable examples. Acceptance samples must include generated `TryGetXxx`, partials, internal containing types, protected members, overloads, nullable, generic constraints, conditional compilation, and invalid human bindings. Verify no Godot leakage into Core, idempotence, explicit failures, and traceable signature changes. Costs include MSBuild/Roslyn coupling, configuration matrices, generated-output review, and capability maintenance. Avoid building an oversized platform merely to reduce a few dozen table rows.

For maintainer agents, the value is preventing wrong signatures and manual-list drift. For game-development agents, the value is receiving a **compilable entry point plus business contract** for tasks such as spawning entities, saving/restoring, or observing data changes. The latter better matches Origo's product goal. Measure first-compilation success, incorrect API calls, real game-task completion, and query context volume before extending the inventory. Combining it with [Affected checks](affected-checks.en.md) provides a capability-discovery, compilation, and verification loop; runtime observability, gameplay acceptance, and scene operations still require separate work.
