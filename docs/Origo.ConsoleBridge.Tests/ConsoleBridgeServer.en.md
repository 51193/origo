<!-- docsync-pair: Origo.ConsoleBridge.Tests/ConsoleBridgeServer -->
<!-- docsync-revision: 11 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Console Bridge Server Tests

> [↑ Back to Origo.ConsoleBridge.Tests](README.en.md)
> [↔ Module under test: Origo.ConsoleBridge](../Origo.ConsoleBridge/README.en.md)
> [↔ Behavior under test: usage/console-commands](../usage/console-commands.en.md)

## Behavior Under Test Overview

Verifies the complete TCP bridge behavior of ConsoleBridgeServer. All tests use real `TcpClient` connections.

## Test File List

| File | Verification Focus |
|------|-------------------|
| `ConsoleBridgeServerLifecycleTests.cs` | Server lifecycle (Start / Stop / Dispose / double Dispose / ActualPort) and connection management (dual connection rejection, disconnect reconnect, hard disconnect recovery) |
| `ConsoleBridgeServerCommunicationTests.cs` | Client input command delivery (FIFO order, Unicode, long lines, blank line filtering) and output channel distribution (multi-line, null, large volume, concurrent publish, buffer overflow) |
| `ConsoleBridgeServerTests.cs` | Thread safety (no deadlock under concurrent read/write), regression tests (connect-time flush vs concurrent publish), short round-trip, Agent workflow integration (output arrival, multi-line output, full reconnect flow) |
| `ConsoleBridgeServerErrorPathTests.cs` | Accept-loop fault observability, Dispose semantics, Start rollback and retry, output-side isolation — write/read failure recovery |
| `ConsoleBridgeOptionsTests.cs` | Option configuration (custom port, etc.) |

## ConsoleBridgeServerTests Test Details

### Server Lifecycle

| Test Method | Verified Behavior | Doc Reference |
|------------|-------------------|---------------|
| `Start_Stop_NoExceptions` | Start→Dispose without exception | ConsoleBridge |
| `Start_AfterDispose_Throws` | Start after Dispose throws ObjectDisposedException | ConsoleBridge |
| `DoubleDispose_DoesNotThrow` | Double Dispose is idempotent | ConsoleBridge |
| `Dispose_StopsAcceptingNewConnections` | New connections rejected after Dispose | ConsoleBridge |
| `Dispose_WhileClientConnected_NoHang` | Dispose does not hang while client is connected | ConsoleBridge |
| `ActualPort_ReflectsAssignedPort` | ActualPort > 0 | ConsoleBridge |
| `Start_CalledTwice_DoesNotThrow` | Double Start is idempotent | ConsoleBridge |
| `Start_CalledTwice_PortRemainsSame` | Port unchanged after double Start | ConsoleBridge |
| `Dispose_BeforeStart_DoesNotThrow` | Dispose before Start is safe | ConsoleBridge |

### Command Input

| Test Method | Verified Behavior | Doc Reference |
|------------|-------------------|---------------|
| `ClientSendCommand_ArrivesInInputQueue` | Client sends "help" → input queue can Dequeue "help" | console-commands: TCP remote console |
| `ClientSendMultipleCommands_ArriveInFifoOrder` | Three commands arrive in FIFO order | console-commands |
| `ClientSendCommand_ManyCommands_StressTest` | 100 commands all arrive | console-commands |
| `ClientSendCommand_LongLine_Arrives` | 4096-character long command arrives correctly | console-commands |
| `ClientSendCommand_Unicode_Arrives` | "héllo 世界 🌍" Unicode command arrives | console-commands |
| `ClientSendCommand_LeadingAndTrailingWhitespace_Trimmed` | "  \t  hello  \t  " → "hello" | console-commands |
| `BlankLines_AreNotEnqueued` | Blank lines not enqueued | console-commands |
| `ClientSendCommand_OnlyWhitespace_NothingEnqueued` | Whitespace-only line → input queue empty | console-commands |

### Console Output

| Test Method | Verified Behavior | Doc Reference |
|------------|-------------------|---------------|
| `OutputChannel_Publish_ArrivesAtClient` | Publish("hello") → client reads "hello" | ConsoleBridge |
| `OutputChannel_MultiplePublishes_AllDelivered` | Three Publish calls → client reads all three | ConsoleBridge |
| `OutputChannel_PublishNullString_Throws` | Publish(null) | ArgumentNullException |
| `OutputChannel_LargeVolume_ManyLines_AllDelivered` | 100 lines all delivered | ConsoleBridge |
| `OutputChannel_ConcurrentPublish_AllDelivered` | 10-thread concurrent publish, all delivered | ConsoleBridge |
| `PendingOutput_WithinLimit_AllDeliveredOnConnect` | 500 pending output lines published with no client connected are all delivered in order after connection | ConsoleBridge |
| `PendingOutput_BufferOverflow_DropsOldestLines` | Pending buffer exceeds the limit (1000+1 lines): overflow notice line plus the retained newest lines are received, the oldest line is dropped | ConsoleBridge |

### Connection Management

| Test Method | Verified Behavior | Doc Reference |
|------------|-------------------|---------------|
| `SecondConnection_WhileFirstActive_FirstClientStillWorks` | Second connection waits in backlog while first active, first connection unaffected | ConsoleBridge: single-connection mode |
| `SecondConnection_WhileFirstActive_CommandNotServiced` | Second connection commands not processed while first active (verified via sentinel ordering determinism) | ConsoleBridge |
| `ClientDisconnect_ServerAcceptsNewConnection` | New connection can be established after disconnect | ConsoleBridge |
| `ClientDisconnect_ThenThirdAccepted` | Multiple disconnect→reconnect cycles all work | ConsoleBridge |
| `ClientImmediateDisconnect_ServerRecovers` | Server recovers after immediate disconnect | ConsoleBridge |
| `MidSession_ClientHardDisconnect_ServerRecovers` | Client forcibly closes socket (not graceful Dispose), server recovers | ConsoleBridge |
| `MidSession_ClientAbort_NextConnectionAccepted` | New connection established after client abort mid-session | ConsoleBridge |
| `ClientDisconnect_OutputLineBufferedForNextConnection` | Output lines published after a disconnect are buffered and delivered on the next connection (client disconnects gracefully with FIN and reads the server's EOF to confirm the disconnect is fully processed, verifying the buffering contract deterministically) | ConsoleBridge |
| `DeadNonReadingClient_IsClosed_NextClientConnectsAndReplaysBacklog` | After the connection-time backlog flush fails on the send timeout, the dead connection is closed (single-connection slot released); the next client can connect and receives the buffered replay (verified through the platform-independent server detach log + the next connection being serviced) | ConsoleBridge: send-timeout detach |
| `DeadClientAfterEstablishedConnection_IsClosed_NextClientConnectsAndReplaysBacklog` | After the connection is established (flush succeeded), the client stops reading and a later output write fails (OnConsoleOutput send timeout) — the dead connection is closed and the slot released; the next client can connect and receives the buffered replay (verifies a failed output write closes the dead connection and releases the slot) | ConsoleBridge: send-timeout detach |
| `BacklogReplayToSlowClient_AbortsAtBudget_RemainingLinesReplayOnNextConnection` | A slow but continuously reading client makes replay block per line but stay below the send timeout: replay aborts at the time budget (log contains "time budget"), remaining lines replay in full on the next connection (no duplicates) | ConsoleBridge: replay time budget |

### Thread Safety

| Test Method | Verified Behavior | Doc Reference |
|------------|-------------------|---------------|
| `Concurrent_PublishWhileReading_NoDeadlock` | Concurrent publish + read does not deadlock | ConsoleBridge |
| `PendingFlushDuringConcurrentPublish_DeliversIntactLines` | Pending buffer flush at connection time races with concurrent Publish on another thread; write lock ensures mutual exclusion between the two paths; every line delivered is an undamaged intact token (backlog and real-time lines each arrive intact) | ConsoleBridge |

### Agent Workflow Integration

| Test Method | Verified Behavior | Doc Reference |
|------------|-------------------|---------------|
| `FullRoundTrip_CommandResponsePattern` | cmd1→response1→cmd2→response2 round-trip | console-commands |
| `AgentLoop_OutputArrivesDuringReadWait` | Publish arrives immediately during ReadLine wait | ConsoleBridge |
| `AgentLoop_SendRead_SendRead_NoTriggerNeeded` | 5 rounds of send-read round-trip work | ConsoleBridge |
| `AgentLoop_MultipleOutputLines_PerCommand` | One command produces multiple lines of output | ConsoleBridge |
| `AgentLoop_OutputBeforeConnect_DeliveredOnConnect` | Output produced before connection delivered after connection | ConsoleBridge |
| `AgentLoop_Disconnect_Reconnect_FullFlow` | Disconnect→reconnect→new command round-trip works | ConsoleBridge |
| `AgentLoop_ConcurrentPublish_DuringReadWait` | Reading works during concurrent publish | ConsoleBridge |
| `AgentLoop_Stress_50Rounds_NoDeadlock` | 50 round-trips without hanging | ConsoleBridge |
| `AgentLoop_Dispose_WhileAgentWaitingForOutput` | Dispose does not hang while agent is waiting for output | ConsoleBridge |

### Constructor Validation

| Test Method | Triggered Error | Expected Behavior |
|------------|-----------------|-------------------|
| `Constructor_NullInput_Throws` | null input | ArgumentNullException |
| `Constructor_NullOutput_Throws` | null output | ArgumentNullException |
| `Constructor_DefaultOptions_HasExpectedPort` | Default options | ActualPort > 0 |
| `Constructor_CustomPort_StoredInOptions` | Port=9876 | ActualPort=9876 |

## ConsoleBridgeServerErrorPathTests Test Details

### Happy Path

| Test Method | Verified Behavior | Doc Reference |
|------------|-------------------|---------------|
| `HardClientRst_TriggersIOException_AndRecovers` | Client RST triggers an IOException on the server's read, server recovers and accepts new connections | ConsoleBridge |
| `HardSocketClose_TriggersIOException_AndRecovers` | Client hard socket close triggers an IOException, server recovers and accepts new connections | ConsoleBridge |
| `StreamShutdown_TriggersSocketException_AndRecovers` | Client Shutdown triggers a SocketException, server recovers and accepts new connections | ConsoleBridge |
| `PendingFlush_BrokenClient_ServerRecovers` | Client connects and immediately disconnects; pending-output flush hits a closed stream, server recovers after exception handling | ConsoleBridge |
| `WriteFailure_LogsWarning_AndRecovers` | Input Enqueue throws: logs a "Connection handler failed" warning, server recovers and accepts new connections | ConsoleBridge |
| `Dispose_FaultedAcceptTask_LogsErrorInsteadOfSwallowing` | A faulted accept task is logged as "Accept loop faulted" on Dispose instead of being swallowed (regression guard) | ConsoleBridge |
| `Dispose_AcceptTaskStillRunning_LogsTimeoutWarning` | Accept task not stopped within the join timeout logs a timeout warning; Dispose does not wait for its full lifetime | ConsoleBridge |
| `AcceptLoop_NonCancellationListenerError_LogsErrorAndStops` | Non-cancellation listener error logs "Accept loop stopped" without misreporting "Accept loop faulted" | ConsoleBridge |
| `AcceptLoop_NonCancellationError_StopsListenerAndAllowsRestart` | Non-cancellation accept error stops the listener and rolls back `_started`; the same instance can Start again on a fresh port and accept connections | ConsoleBridge |
| `OnConsoleOutput_BrokenWriter_DoesNotThrowToCaller` | With a dead client stream, the output callback does not throw to the caller, logs a warning and clears the writer | ConsoleBridge |
| `Publish_BrokenClientWriter_DoesNotThrowToCaller` | After client RST, `Publish` does not throw to the game side (output-side isolation); the server still accepts new connections afterwards | ConsoleBridge |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|------------|-----------------|-------------------|
| `Start_AfterDispose_ThrowsObjectDisposed` | Start called after Dispose | ObjectDisposedException |
| `Start_Failure_RollsBackAndAllowsRetry` | Invalid port (-1) makes Start fail | Throws and rolls back the started flag; retry succeeds after fixing the port |
| `Start_PortInUse_RollsBackListenerAndAllowsRetryAfterRelease` | Port in use makes Start fail | SocketException, listener and output subscription fully rolled back; retry succeeds after the port is released |

## ConsoleBridgeOptionsTests Test Details

### Option Configuration

| Test Method | Verified Behavior | Doc Reference |
|------------|-------------------|---------------|
| `DefaultPort_IsExpectedValue` | Default port is 9876 | ConsoleBridge |
| `DefaultOptions_HasCorrectDefaults` | `ConsoleBridgeOptions` defaults are correct | ConsoleBridge |
| `Options_CustomPort_Assigned` | Custom port stored correctly | ConsoleBridge |

## Known Coverage Gaps

| Gap Description | Impact | Doc Basis |
|----------------|--------|-----------|
| Rejection behavior under extremely high concurrency (100+ concurrent connection attempts) | Connection storm | ConsoleBridge: single-connection mode |
| Output write-failure path (`WriteLine` throwing inside the RST race window → line buffered) cannot be triggered deterministically from a black box | The fallback shares the buffering code with the detach path, which the deterministic test covers; losing a line written "into the void" inside the RST window is an inherent TCP race | ConsoleBridge: exception propagation strategy |

---

[↑ Back to Origo.ConsoleBridge.Tests](README.en.md)
