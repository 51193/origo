<!-- docsync-pair: Origo.GodotAdapter.Tests/Bootstrap -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Bootstrap Tests (Adapter Layer)

> [↑ Back to Origo.GodotAdapter.Tests](README.en.md)
> [↔ Module under test: Origo.GodotAdapter/Bootstrap](../Origo.GodotAdapter/Bootstrap/README.en.md)

## Behavior Under Test Overview

Verifies the contract of the GodotAdapter bootstrap entry point `GodotSndBootstrap.BindRuntimeAndContext`:
null manager guardrail and four-parameter signature (manager / world / logger / context).

## Test File List

| File | Verification Focus |
|------|-------------------|
| `GodotSndBootstrapTests.cs` | Guardrails and parameter contract of `GodotSndBootstrap.BindRuntimeAndContext` |

## GodotSndBootstrapTests Test Details

### Happy Path

| Test Method | Verified Behavior | Doc Reference |
|------------|-------------------|---------------|
| `BindRuntimeAndContext_HasExpectedFourParameterContract` | `BindRuntimeAndContext` has exactly 4 parameters, named manager / world / logger / context in order | Origo.GodotAdapter/Bootstrap |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|------------|-----------------|-------------------|
| `BindRuntimeAndContext_WithNullManager_Throws` | manager (and remaining parameters) is null | ArgumentNullException |

## Test Support Strategy

| Strategy Class | Defined In | Purpose |
|---------------|-----------|---------|
| None | — | This test file defines no support strategies; pure reflection / contract guardrail tests |

## Known Coverage Gaps

| Gap Description | Impact | Doc Basis |
|----------------|--------|-----------|
| Production files under test (`OrigoAutoHost.cs` / `OrigoDefaultEntry.cs` and `GodotSndEntity.cs`, etc.) that depend on the Godot engine runtime are excluded by coverlet (`ExcludeByFile`) and cannot be covered in tests | Godot engine-level logic of bootstrap orchestration not directly verified by tests | Origo.GodotAdapter bootstrap documentation |

---

[↑ Back to Origo.GodotAdapter.Tests](README.en.md)
