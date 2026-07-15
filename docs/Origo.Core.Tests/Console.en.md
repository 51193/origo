<!-- docsync-pair: Origo.Core.Tests/Console -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Console System Tests

> [↑ Back to Origo.Core.Tests](README.en.md)
> [↔ Module under test: Origo.Core/Runtime/Console](../Origo.Core/Runtime/Console/README.en.md)
> [↔ Behavior under test: usage/console-commands](../usage/console-commands.en.md)

## Behavior Overview

Validates the full chain of the console command system: command parsing (positional/named/mixed arguments), command routing (register/dispatch/not found/case-insensitive/duplicate overwrite),
input queue (polling dequeue, FIFO, trim, clear), `IConsoleInputSource` interface contract, output channel (publish-subscribe, exception propagation),
13 built-in command handlers (11 Core + 2 GodotAdapter), type inference (bb_set/entity_set_data), console logging (level/order/Tag/content integrity).

## Test File List

| File | Verification Focus |
|------|-------------------|
| `ConsoleCommandParserTests.cs` | Command parsing: empty/whitespace lines, single commands, positional args, named args, invalid named args |
| `ConsoleCommandRouterTests.cs` | Command routing: register/dispatch/unregistered commands/case-insensitive/duplicate overwrites previous/null handler |
| `ConsoleInputBufferTests.cs` | Input queue: Enqueue/Dequeue/FIFO/trim/whitespace ignored/clear |
| `ConsoleOutputChannelTests.cs` | Output channel: Subscribe/Publish/Unsubscribe/multiple subscribers/null broadcast/exception propagation |
| `ConsoleCommandExtendedTests.cs` | Built-in command end-to-end: help/find_entity/kill_all/bb_*, late registration, parameter validation, spawn |
| `ConsoleTypeInferenceTests.cs` | Type inference: bb_set Int32/Single/Boolean/String, entity_set_data new key inference + existing key type preservation |
| `OrigoConsoleLoggingTests.cs` | Console logging: correct log level, message order, Tag consistency, content integrity (behavioral verification, no coupling to format strings) |
| `ConsoleInputSourceContractTests.cs` | IConsoleInputSource interface contract: round-trip/FIFO/trim/whitespace ignored/clear/null ignored |
| `EntityDataCommandHandlerTests.cs` | entity_get_data / entity_set_data commands |
| `InvokeStrategyCommandHandlerTests.cs` | invoke_strategy command |
| `SndCountCommandHandlerTests.cs` | snd_count command |
| `SpawnTemplateCommandHandlerTests.cs` | spawn command error paths: mixed argument format, missing name parameter |

## ConsoleCommandParserTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `ConsoleCommandParser_TryParse_SingleCommand` | "help" parsed as command name + empty args | console-commands |
| `ConsoleCommandParser_TryParse_PositionalArgs` | "spawn myName myTemplate" parses 2 positional args | console-commands |
| `ConsoleCommandParser_TryParse_NamedArgs` | "spawn name=myName template=myTpl" parses 2 named args | console-commands |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `ConsoleCommandParser_TryParse_EmptyLine_Fails` | Empty line | returns false + error |
| `ConsoleCommandParser_TryParse_WhitespaceLine_Fails` | Whitespace-only line | returns false + error |
| `ConsoleCommandParser_TryParse_InvalidNamedArg_Fails` | "cmd =value" (no key) | returns false + error |
| `ConsoleCommandParser_TryParse_NamedArgMissingValue_Fails` | "cmd key=" (no value) | returns false + error |

## ConsoleCommandRouterTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `ConsoleCommandRouter_Register_And_TryExecute_Success` | Register handler then dispatch executes successfully | console-commands |
| `ConsoleCommandRouter_Register_CaseInsensitive` | Command names are case-insensitive ("TEST" matches "Test") | console-commands |
| `ConsoleCommandRouter_Register_DuplicateName_OverridesPreviousHandler` | Re-registering the same command name overwrites the previous handler | console-commands |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `ConsoleCommandRouter_TryExecute_UnknownCommand_ReturnsFalse` | Unregistered command | returns false + error containing "Unknown command" |
| `ConsoleCommandRouter_Register_NullHandler_Throws` | Register(null) | ArgumentNullException |

## ConsoleInputBufferTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `ConsoleInputBuffer_Enqueue_And_TryDequeue` | Enqueue then TryDequeueCommand retrieves original value | console-commands |
| `ConsoleInputBuffer_Enqueue_TrimsInput` | "  hello  " is trimmed to "hello" on enqueue | console-commands |
| `ConsoleInputBuffer_FIFO_Order` | Multiple commands dequeued in FIFO order | console-commands |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `ConsoleInputBuffer_TryDequeue_EmptyQueue_ReturnsFalse` | Dequeue from empty queue | returns false, line is null |
| `ConsoleInputBuffer_Enqueue_WhitespaceIgnored` | Enqueue whitespace/empty string | not enqueued, dequeue returns false |
| `ConsoleInputBuffer_Clear_EmptiesQueue` | Dequeue after Clear | returns false |

## ConsoleOutputChannelTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `ConsoleOutputChannel_Subscribe_And_Publish` | After subscribing, Publish delivers the message | console-commands |
| `ConsoleOutputChannel_Unsubscribe_StopsReceiving` | After Unsubscribe, no further messages received | console-commands |
| `ConsoleOutputChannel_MultipleSubscribers` | Multiple subscribers all receive the broadcast | console-commands |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `ConsoleOutputChannel_Subscribe_ThrowsOnNull` | Subscribe(null) | ArgumentNullException |
| `ConsoleOutputChannel_Publish_FirstListenerThrows_SecondStillReceives` | First subscriber throws exception | Exception propagates, but subsequent subscribers still receive the message |
| `ConsoleOutputChannel_Publish_FirstListenerThrows_ExceptionPropagates` | Multiple subscribers throw | First exception ("e1") propagates |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `ConsoleOutputChannel_Unsubscribe_InvalidId_ReturnsFalse` | Unsubscribe non-existent id | returns false |
| `ConsoleOutputChannel_Publish_NullBroadcastsEmpty` | Publish(null) | subscriber receives empty string |

## ConsoleCommandExtendedTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `HelpCommand_ListsAllRegisteredCommands` | help lists all registered commands (including spawn/snd_count) | console-commands: help |
| `ClearEntitiesCommand_ClearsAll` | kill_all command outputs the count of entities marked this frame | console-commands: kill_all |
| `BlackboardSetGet_RoundTrip` | bb_set then bb_get retrieves the int value | console-commands: bb_set/bb_get |
| `BlackboardSetGet_StringValue` | bb_set/bb_get string value round-trip | console-commands |
| `BlackboardSetGet_BoolValue` | bb_set/bb_get bool value round-trip, type is Boolean | console-commands |
| `BlackboardKeys_ListsKeys` | bb_keys lists all set keys | console-commands: bb_keys |
| `RegisterHandler_LateRegistration_CommandAvailable` | Handler registered late at runtime is immediately available and appears in help | console-commands |
| `GetRegisteredNames_ReturnsSortedNames` | GetRegisteredNames returns list including registered command names | console-commands |
| `ConsoleCommandHandlerBase_ExactArgs_Succeeds` | Execution succeeds when arg count matches the required count exactly | console-commands |
| `HelpCommand_ShowsHelpTextForEachCommand` | help displays the HelpText for each command | console-commands: help |
| `SpawnCommand_NamedArgs_SpawnsEntity` | spawn name=.. template=.. successfully spawns entity | console-commands: spawn |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `FindEntityCommand_NotFound_ReportsNotFound` | find_entity for non-existent entity | output contains "not found" |
| `FindEntityCommand_MissingArg_ReportsUsage` | find_entity missing argument | output contains "Invalid argument count." |
| `BlackboardGet_MissingKey_ReportsNotFound` | bb_get for non-existent key | output contains "not found" |
| `BlackboardSet_InvalidLayer_ReportsError` | bb_set with invalid layer name | output contains "Unknown" |
| `BlackboardGet_MissingArgs_ReportsUsage` | bb_get missing arguments | output contains "Invalid argument count." |
| `BlackboardSet_MissingArgs_ReportsUsage` | bb_set missing arguments | output contains "Invalid argument count." |
| `BlackboardKeys_MissingArgs_ReportsUsage` | bb_keys missing arguments | output contains "Invalid argument count." |
| `ConsoleCommandHandlerBase_TooFewArgs_ReturnsErrorWithHelpText` | Positional args fewer than Min | returns false + error containing "Invalid argument count." and HelpText |
| `ConsoleCommandHandlerBase_TooManyArgs_ReturnsErrorWithHelpText` | Positional args more than Max | returns false + error containing "Invalid argument count." |
| `SpawnCommand_NamedMissingTemplate_ReportsError` | spawn named args missing template | output contains "template" |
| `SpawnCommand_PositionalWrongCount_ReportsUsage` | spawn with wrong positional arg count | output contains "Usage" |
| `SpawnCommand_PositionalSingleArg_ReportsUsage` | spawn with only one positional arg | output contains "Usage" |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `BlackboardKeys_EmptyBlackboard` | bb_keys on empty blackboard | output contains "empty" |
| `ConsoleCommandHandlerBase_UnlimitedMax_AcceptsAnyCount` | MaxPositionalArgs = -1 | accepts any number of positional args and succeeds |

## ConsoleTypeInferenceTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `BlackboardSet_IntLiteral_StoredAsInt32` | bb_set system score 42 → TryGet<int> returns (true, 42) | console-commands: bb_set |
| `BlackboardSet_NegativeInt_StoredAsInt32` | bb_set system neg -5 → Int32(-5) | console-commands |
| `BlackboardSet_FloatLiteral_StoredAsSingle` | bb_set system pi 3.14 → Single(3.14) | console-commands |
| `BlackboardSet_TrueLiteral_StoredAsBoolean` | bb_set system flag true → Boolean(true) | console-commands |
| `BlackboardSet_FalseLiteral_StoredAsBoolean` | bb_set system flag2 false → Boolean(false) | console-commands |
| `BlackboardSet_NonNumericLiteral_StoredAsString` | bb_set system msg hello_world → String | console-commands |
| `EntitySetData_NewKey_IntLiteral_StoredAsInt32` | entity_set_data player hp 100 → Int32 | console-commands: entity_set_data |
| `EntitySetData_NewKey_FloatLiteral_StoredAsSingle` | entity_set_data player speed 1.5 → Single | console-commands |
| `EntitySetData_NewKey_BoolLiteral_StoredAsBoolean` | entity_set_data player alive true → Boolean | console-commands |
| `EntitySetData_NewKey_StringLiteral_StoredAsString` | entity_set_data player tag hero → String | console-commands |
| `EntitySetData_ExistingKey_PreservesType` | Existing float-typed "hunger" key, write 15 → preserved as Single(15.0f) | console-commands: entity_set_data |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `BlackboardSet_UnknownLayer_ReturnsError` | bb_set unknown key 42 | returns false + error containing "layer" |
| `EntitySetData_EntityNotFound_ReturnsError` | entity_set_data nonexistent hp 50 | returns false + error containing "not found" |

## OrigoConsoleLoggingTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `ProcessPending_SimpleCommand_LogsThreeDebugMessagesAndNoWarnings` | Single command produces 3 Debug log entries and no Warnings | console-commands |
| `ProcessPending_MultipleCommands_LogsThreePerCommand` | Each command produces 3 Debug entries, correctly distributed by command name | console-commands |
| `ProcessPending_UnknownCommand_LogsFailureAtDebugLevel` | Unknown command failure logged as Debug only, no Warning | console-commands |
| `ProcessPending_HandlerReturnsError_LogsFailureAtDebugLevel` | Handler returning error logged as Debug only | console-commands |
| `ProcessPending_MixedSuccessAndFailure_LogLevelsCorrect` | Mixed success and failure all logged at Debug level | console-commands |
| `ProcessPending_ReceiveBeforeExecuteBeforeResult_OrderCorrect` | Receive→Execute→Result log entries appear in order | console-commands |
| `ProcessPending_HandlerReturnsErrorWithNullMessage_LogsFailureAtDebugLevel` | Handler returning null error message still logged as Debug | console-commands |
| `ProcessPending_ParseError_LoggedAtDebugLevel` | Parse error logged as Debug containing "Parse error" | console-commands |
| `ProcessPending_AllDebugMessages_HaveCorrectTag` | All Debug messages start with "OrigoConsole: " | console-commands |
| `ProcessPending_NormalOperation_ProducesNoWarnings` | Normal operation produces no Warnings | console-commands |
| `ProcessPending_PositionalArgs_AppearInLog` | Positional args appear in log | console-commands |
| `ProcessPending_NamedArgs_AppearInLog` | Named args appear in log | console-commands |
| `ProcessPending_SuccessCommand_IncludesElapsedTime` | Successful command log includes elapsed time in "ms" | console-commands |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `ProcessPending_HandlerThrowsException_ThrowsUnhandledToCaller` | Handler throws exception | Exception propagates to caller (InvalidOperationException) |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `ProcessPending_EmptyQueue_ProducesNoLogMessages` | ProcessPending on empty queue | No log entries |
| `ProcessPending_TrimmedCommand_StillProcessed` | Command with surrounding whitespace | Trimmed and processed normally |
| `ProcessPending_EmptyAfterTrim_Skipped` | Command becomes empty after trim | Skipped, no log |
| `ProcessPending_LongCommandLine_FullContentLogged` | 500-character extra-long command | Full content logged |
| `ProcessPending_UnicodeCommand_CharactersPreserved` | Command containing Unicode characters | Characters fully preserved |
| `ProcessPending_CommandWithEmbeddedQuotes_LoggedCorrectly` | Command with embedded quotes | Logged correctly |

## ConsoleInputSourceContractTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `Enqueue_And_TryDequeue_RoundTrip` | Enqueue then TryDequeueCommand retrieves original value | console-commands |
| `Enqueue_FifoOrder_Preserved` | Multiple commands dequeued in FIFO order | console-commands |
| `Enqueue_TrimsWhitespaceAroundContent` | Enqueue trims leading/trailing whitespace, preserves inner content | console-commands |
| `Enqueue_AfterClear_WorksNormally` | Enqueue after Clear works normally | console-commands |
| `Clear_EmptiesAllPendingCommands` | Clear empties all pending commands | console-commands |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `TryDequeue_EmptyQueue_ReturnsFalse` | Dequeue from empty queue | returns false, cmd is null |
| `TryDequeue_AfterExhausting_ReturnsFalse` | Dequeue again after exhausting | returns false |
| `Enqueue_EmptyString_Ignored` | Enqueue empty string | Ignored |
| `Enqueue_WhitespaceOnly_Ignored` | Enqueue whitespace only | Ignored |
| `Clear_OnAlreadyEmpty_DoesNotThrow` | Clear on empty queue | Does not throw |
| `Enqueue_Null_Ignored` | Enqueue null | Ignored |

## EntityDataCommandHandlerTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `EntitySetData_IntValue_StoresCorrectly` | entity_set_data writes int and outputs confirmation | console-commands: entity_set_data |
| `EntitySetData_FloatValue_StoresCorrectly` | entity_set_data writes float | console-commands |
| `EntitySetData_BoolValue_StoresCorrectly` | entity_set_data writes bool | console-commands |
| `EntitySetData_StringValue_StoresCorrectly` | entity_set_data writes string | console-commands |
| `EntitySetData_PreservesExistingIntType` | Rewriting an existing int key preserves int type | console-commands |
| `EntitySetData_PreservesExistingFloatType` | Rewriting an existing float key preserves float type | console-commands |
| `EntitySetData_PreservesExistingBoolType` | Rewriting an existing bool key preserves bool type | console-commands |
| `EntitySetData_PreservesExistingStringType` | Rewriting an existing string key preserves string type | console-commands |
| `EntityGetData_Found_ReportsValueAndType` | entity_get_data outputs value and type (Int32) | console-commands: entity_get_data |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `EntitySetData_EntityNotFound_ReportsError` | entity_set_data entity not found | output contains "not found" |
| `EntityGetData_EntityNotFound_ReportsError` | entity_get_data entity not found | output contains "not found" |
| `EntityGetData_NotFound_ReportsNotFound` | entity_get_data key not found | output contains "not found on entity" |
| `EntityGetData_MissingArgs_ReportsUsage` | entity_get_data missing args | output contains "Invalid argument count." |
| `EntitySetData_MissingArgs_ReportsUsage` | entity_set_data missing args | output contains "Invalid argument count." |

## InvokeStrategyCommandHandlerTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `InvokeStrategy_NoInput_ReturnsResult` | invoke_strategy calls active strategy without input and outputs result | console-commands: invoke_strategy |
| `InvokeStrategy_WithInput_PassesToStrategy` | invoke_strategy passes JSON input to the strategy | console-commands: invoke_strategy |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `InvokeStrategy_MissingEntity_OutputsError` | invoke_strategy target entity not found | returns false + error containing entity name |
| `InvokeStrategy_NotActiveStrategy_OutputsError` | Calling unregistered active strategy index | returns false + error containing strategy index |

## SndCountCommandHandlerTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `SndCount_PublishesEntityCount` | snd_count outputs current entity count "Snd count: 2" | console-commands: snd_count |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `SndCount_WithNoEntities_PublishesZero` | No entities (no foreground session) | outputs "Snd count: 0" |

## SpawnTemplateCommandHandlerTests Details

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `SpawnTemplateCommandHandler_MixNamedAndPositional_ReturnsError` | Mixed positional and named args | returns false + error containing "mix" |
| `SpawnTemplateCommandHandler_NamedMissingName_ReturnsError` | Named args missing name | returns false + error containing "name" |

## Test Helper Strategies

| Strategy Class | Defined In | Purpose |
|---------------|-----------|---------|
| `StubHandler` | ConsoleCommandRouterTests.cs | IConsoleCommandHandler stub, records whether it was executed, verifying route dispatch |
| `TestPingHandler` | ConsoleCommandExtendedTests.cs | IConsoleCommandHandler stub, "ping"→publishes "pong", verifying late registration |
| `TestMinMaxHandler` | ConsoleCommandExtendedTests.cs | ConsoleCommandHandlerBase stub, configurable Min/Max arg counts, verifying parameter validation |
| `FailingHandler` | OrigoConsoleLoggingTests.cs | IConsoleCommandHandler stub, always returns failure, verifying failure logging |
| `NullErrorHandler` | OrigoConsoleLoggingTests.cs | IConsoleCommandHandler stub, returns false with null error message, verifying null error logging |
| `ThrowingHandler` | OrigoConsoleLoggingTests.cs | IConsoleCommandHandler stub, TryExecute throws, verifying exception propagation |
| `QueryNameStrategy` | InvokeStrategyCommandHandlerTests.cs | ActiveStrategyBase stub, Invoke returns entity name, verifying invoke_strategy without input |
| `CmdWithInputStrategy` | InvokeStrategyCommandHandlerTests.cs | ActiveStrategyBase stub, Invoke echoes input, verifying invoke_strategy with input |
| `CollectingConsoleOutputChannel` | InvokeStrategyCommandHandlerTests.cs | IConsoleOutputChannel stub, collects output lines for assertions |

## Known Coverage Gaps

| Gap Description | Impact | Reference |
|----------------|--------|-----------|
| Behavior after removing a registered handler from ConsoleCommandRouter | Dynamic command unloading | — |
| Thread safety of concurrent TryDequeueCommand and Enqueue | Multi-threaded input | ConsoleInputBuffer |
| Output buffering on TCP remote console disconnect/reconnect | Whether historical output is pushed on reconnect | Origo.ConsoleBridge |

---

[↑ Back to Origo.Core.Tests](README.en.md)
