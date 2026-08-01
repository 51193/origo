<!-- docsync-pair: Origo.ConsoleBridge.Tests/Architecture -->
<!-- docsync-revision: 2 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Assembly Architecture Guardrail Tests

> [↑ Back to Origo.ConsoleBridge.Tests](README.en.md)
> [↔ Module under test: Origo.ConsoleBridge](../Origo.ConsoleBridge/README.en.md)

## Behavior Under Test Overview

Uses reflection to verify that the ConsoleBridge assembly does not depend on
the Godot engine or Origo.GodotAdapter, ensuring the TCP remote console bridge
can be used independently in environments without a Godot runtime.

## Test Files

| File | Verification Focus |
|------|-------------------|
| `Architecture/ConsoleBridgeArchitectureGuardrailTests.cs` | Assembly dependency direction and encapsulation integrity |

## Correct Paths

| Test Method | Behavior Verified | Doc Reference |
|------------|-------------------|---------------|
| `ConsoleBridge_ShouldNotReferenceGodot` | Does not reference any assembly with a `Godot*` prefix | Origo.ConsoleBridge |
| `ConsoleBridge_ShouldNotReferenceGodotAdapter` | Does not reference the `Origo.GodotAdapter` assembly | Origo.ConsoleBridge |
| `ConsoleBridge_ShouldOnlyReferenceCore` | Only depends on `Origo.Core` + BCL (`System.*`/`Microsoft.*`/`netstandard`/`System.Runtime`), no unexpected assembly references | Origo.ConsoleBridge |

## Known Coverage Gaps

| Gap Description | Impact | Doc Basis |
|----------------|--------|-----------|
| Specific dependency version ranges not validated | Assembly version compatibility | Origo.ConsoleBridge |

---

[↑ Back to Origo.ConsoleBridge.Tests](README.en.md)
