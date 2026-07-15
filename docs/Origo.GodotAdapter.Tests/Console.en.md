<!-- docsync-pair: Origo.GodotAdapter.Tests/Console -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Console Tests (Adapter Layer)

> [↑ Back to Origo.GodotAdapter.Tests](README.en.md)
> [↔ Module under test: Origo.GodotAdapter/Console](../Origo.GodotAdapter/Console/README.en.md)
> [↔ Behavior under test: usage/console-commands](../usage/console-commands.en.md)

## Behavior Under Test Overview

Verifies Godot adapter-layer extended console commands and base class: `press_button` (finds Godot Button
by entity name and node path and emits Pressed signal), `camera_view` (prints screen coordinates and depth
of entity under current camera), adapter-layer `CommandHandlerBase` argument count validation, null guards,
and execution orchestration; plus the `ProjectionHelper` world→screen coordinate projection math used by `camera_view`.

## Test File List

| File | Verification Focus |
|------|-------------------|
| `CommandHandlerBaseTests.cs` | Adapter-layer CommandHandlerBase: constructor guards, null guards, argument count lower/upper bound checks, execution success |
| `PressButtonCommandHandlerTests.cs` | press_button command: property contract, insufficient arguments, entity not found, entity is not Godot entity |
| `CameraViewCommandHandlerTests.cs` | camera_view command: property contract (Name / HelpText / argument bounds) |
| `ProjectionHelperTests.cs` | ProjectionHelper.ProjectWorldToScreen: center / four boundaries / behind camera / outside frustum / depth increase / symmetry / camera not at origin |

## CommandHandlerBaseTests Test Details

### Happy Path

| Test Method | Verified Behavior | Doc Reference |
|------------|-------------------|---------------|
| `TryExecute_ExactArgs_Succeeds` | Succeeds when argument count exactly meets requirement, outputs "ok", error is null | console-commands |
| `TryExecute_UnlimitedMax_AcceptsManyArgs` | MaxPositionalArgs=-1 (unlimited) accepts arbitrarily many arguments and succeeds | console-commands |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|------------|-----------------|-------------------|
| `Constructor_NullRuntime_Throws` | runtime is null at construction | ArgumentNullException |
| `TryExecute_NullInvocation_Throws` | invocation is null | ArgumentNullException |
| `TryExecute_NullOutputChannel_Throws` | outputChannel is null | ArgumentNullException |
| `TryExecute_TooFewArgs_ReturnsErrorWithHelpText` | Argument count less than MinPositionalArgs | Returns false, error contains "Invalid argument count." and HelpText |
| `TryExecute_TooManyArgs_ReturnsErrorWithHelpText` | Argument count exceeds MaxPositionalArgs | Returns false, error contains "Invalid argument count." |

## PressButtonCommandHandlerTests Test Details

### Happy Path

| Test Method | Verified Behavior | Doc Reference |
|------------|-------------------|---------------|
| `Properties_HaveExpectedValues` | Name="press_button", HelpText contains `<entity>` / `<path>`, Min / Max both 2 | console-commands |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|------------|-----------------|-------------------|
| `TryExecute_TooFewArgs_ReturnsError` | Only 1 argument provided (2 required) | Returns false, error contains "Invalid argument count." |
| `TryExecute_EntityNotFound_ReturnsError` | Entity name does not exist | Returns false, error contains "Entity 'NonExistent' not found" |
| `TryExecute_EntityNotGodot_ReturnsError` | Entity exists but is not a Godot entity | Returns false, error contains "is not a Godot entity" |

## CameraViewCommandHandlerTests Test Details

### Happy Path

| Test Method | Verified Behavior | Doc Reference |
|------------|-------------------|---------------|
| `Properties_HaveExpectedValues` | Name="camera_view", HelpText contains "screen coordinates" / "depth", Min / Max both 0 | console-commands |

## ProjectionHelperTests Test Details

### Happy Path

| Test Method | Verified Behavior | Doc Reference |
|------------|-------------------|---------------|
| `ProjectWorldToScreen_Center_ReturnsScreenCenter` | Point directly ahead of camera projects to screen center (400,300), depth is distance | console-commands: camera_view |
| `ProjectWorldToScreen_RightEdge_ReturnsRightBoundary` | Point to the right projects to right boundary X=800 under 90° FOV | console-commands |
| `ProjectWorldToScreen_LeftEdge_ReturnsLeftBoundary` | Point to the left projects to left boundary X=0 | console-commands |
| `ProjectWorldToScreen_TopEdge_ReturnsTopBoundary` | Point above projects to top boundary Y=0 | console-commands |
| `ProjectWorldToScreen_BottomEdge_ReturnsBottomBoundary` | Point below projects to bottom boundary Y=600 | console-commands |
| `ProjectWorldToScreen_DepthIncreasesWithDistance` | Farther points return larger depth values | console-commands |
| `ProjectWorldToScreen_SymmetricPositions_HaveSymmetricScreenX` | Points symmetric about center have symmetric screen X offsets | console-commands |
| `ProjectWorldToScreen_CameraNotAtOrigin_ProjectsCorrectly` | Correct projection when camera is not at origin | console-commands |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|------------|-------------------|-------------------|
| `ProjectWorldToScreen_BehindCamera_ReturnsNull` | Point is behind the camera | Returns null |
| `ProjectWorldToScreen_OutsideFrustum_ReturnsNull` | Point is outside the frustum | Returns null |

## Test Support Strategy

| Strategy Class | Defined In | Purpose |
|---------------|-----------|---------|
| `TestHandler` | `CommandHandlerBaseTests.cs` | Test stub derived from `CommandHandlerBase`, configurable Min / Max argument bounds, `ExecuteCore` outputs "ok", used to drive base class argument validation logic |

> Shared support: `TestRuntimeHelper` / `TestSndSceneHost` / `InMemorySndEntity` — see [TestSupport.md](TestSupport.en.md).

## Known Coverage Gaps

| Gap Description | Impact | Doc Basis |
|----------------|--------|-----------|
| Happy path of `press_button` successfully emitting Pressed signal on a real Godot entity not covered (depends on Godot engine runtime, related production files excluded by coverlet) | Command happy path not directly verified in tests | Origo.GodotAdapter/Console |
| Happy path of `camera_view` command execution on a real camera / entity not covered (depends on Godot engine runtime, command handler file excluded by coverlet; only ProjectionHelper projection math covered by unit tests) | Command happy path not directly verified in tests | Origo.GodotAdapter/Console |
| `CommandHandlerBase` named argument (NamedArgs) parsing and validation not covered | Named argument path not verified | console-commands |

---

[↑ Back to Origo.GodotAdapter.Tests](README.en.md)
