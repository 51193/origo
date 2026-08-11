<!-- docsync-pair: Origo.ConsoleBridge.Tests/ConsoleBridgeServer -->
<!-- docsync-revision: 8 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# 控制台桥接服务器 测试

> [↑ 回到 Origo.ConsoleBridge.Tests](README.zh.md)
> [↔ 被测模块: Origo.ConsoleBridge](../Origo.ConsoleBridge/README.zh.md)
> [↔ 被测行为: usage/console-commands](../usage/console-commands.zh.md)

## 被测行为概览

验证 ConsoleBridgeServer 的完整 TCP 桥接行为。所有测试使用真实 `TcpClient` 连接。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `ConsoleBridgeServerLifecycleTests.cs` | 服务器生命周期（Start/Stop/Dispose/双 Dispose/ActualPort）和连接管理（双连接拒绝、断开重连、硬断开恢复） |
| `ConsoleBridgeServerCommunicationTests.cs` | 客户端输入命令传递（FIFO 顺序、Unicode、长行、空白行过滤）和输出通道分发（多行、null、大容量、并发发布、缓冲溢出） |
| `ConsoleBridgeServerTests.cs` | 线程安全（并发读写无死锁）、回归测试（connect-time flush vs 并发发布）、短往返、Agent 工作流集成（输出到达、多行输出、重连全流程） |
| `ConsoleBridgeServerErrorPathTests.cs` | 接受循环故障可观察性、Dispose 语义、Start 回滚与重试、输出侧隔离——写/读失败恢复 |
| `ConsoleBridgeOptionsTests.cs` | 选项配置（自定义端口等） |

## ConsoleBridgeServerTests 测试详情

### 服务器生命周期

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Start_Stop_NoExceptions` | Start→Dispose 无异常 | ConsoleBridge |
| `Start_AfterDispose_Throws` | Dispose 后 Start 抛 ObjectDisposedException | ConsoleBridge |
| `DoubleDispose_DoesNotThrow` | 两次 Dispose 幂等 | ConsoleBridge |
| `Dispose_StopsAcceptingNewConnections` | Dispose 后新连接被拒绝 | ConsoleBridge |
| `Dispose_WhileClientConnected_NoHang` | 有客户端连接时 Dispose 不挂起 | ConsoleBridge |
| `ActualPort_ReflectsAssignedPort` | ActualPort > 0 | ConsoleBridge |
| `Start_CalledTwice_DoesNotThrow` | 两次 Start 幂等 | ConsoleBridge |
| `Start_CalledTwice_PortRemainsSame` | 两次 Start 端口不变 | ConsoleBridge |
| `Dispose_BeforeStart_DoesNotThrow` | Start 前 Dispose 安全 | ConsoleBridge |

### 命令输入

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `ClientSendCommand_ArrivesInInputQueue` | 客户端发 "help" → 输入队列可 Dequeue "help" | console-commands: TCP 远程控制台 |
| `ClientSendMultipleCommands_ArriveInFifoOrder` | 三条命令以 FIFO 顺序到达 | console-commands |
| `ClientSendCommand_ManyCommands_StressTest` | 100 条命令全部到达 | console-commands |
| `ClientSendCommand_LongLine_Arrives` | 4096 字符长命令正确到达 | console-commands |
| `ClientSendCommand_Unicode_Arrives` | "héllo 世界 🌍" Unicode 命令到达 | console-commands |
| `ClientSendCommand_LeadingAndTrailingWhitespace_Trimmed` | "  \t  hello  \t  " → "hello" | console-commands |
| `BlankLines_AreNotEnqueued` | 空白行不入队 | console-commands |
| `ClientSendCommand_OnlyWhitespace_NothingEnqueued` | 仅空白行 → 输入队列空 | console-commands |

### 控制台输出

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `OutputChannel_Publish_ArrivesAtClient` | Publish("hello") → 客户端读到 "hello" | ConsoleBridge |
| `OutputChannel_MultiplePublishes_AllDelivered` | 三次 Publish → 客户端读到全部三条 | ConsoleBridge |
| `OutputChannel_PublishNullString_Throws` | Publish(null) | ArgumentNullException |
| `OutputChannel_LargeVolume_ManyLines_AllDelivered` | 100 行全部递送 | ConsoleBridge |
| `OutputChannel_ConcurrentPublish_AllDelivered` | 10 线程并发发布，全部递送 | ConsoleBridge |
| `PendingOutput_WithinLimit_AllDeliveredOnConnect` | 无客户端连接时发布 500 行待发输出，连接后全部按序递送 | ConsoleBridge |
| `PendingOutput_BufferOverflow_DropsOldestLines` | 待发缓冲超过上限（1000+1 行）：收到溢出通知行与保留的最新行，最旧行被丢弃 | ConsoleBridge |

### 连接管理

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `SecondConnection_WhileFirstActive_FirstClientStillWorks` | 第一个连接活跃时第二个连接进入 backlog 等待，第一个连接不受影响 | ConsoleBridge: 单连接模式 |
| `SecondConnection_WhileFirstActive_CommandNotServiced` | 第一个连接活跃时第二个连接的命令不被处理（以 sentinel 顺序确定性验证） | ConsoleBridge |
| `ClientDisconnect_ServerAcceptsNewConnection` | 断开后新连接可建立 | ConsoleBridge |
| `ClientDisconnect_ThenThirdAccepted` | 多次断开→重连都正常 | ConsoleBridge |
| `ClientImmediateDisconnect_ServerRecovers` | 立即断开后服务器恢复正常 | ConsoleBridge |
| `MidSession_ClientHardDisconnect_ServerRecovers` | 客户端强制关闭 socket（非正常 Dispose），服务器恢复 | ConsoleBridge |
| `MidSession_ClientAbort_NextConnectionAccepted` | 会话中客户端中断后新连接可建立 | ConsoleBridge |
| `ClientDisconnect_OutputLineBufferedForNextConnection` | 断开后发布的输出行被缓冲，并在下一连接投递（客户端以 FIN 优雅断开并读到服务器 EOF 确认断开已处理，确定性验证缓冲契约） | ConsoleBridge |
| `DeadNonReadingClient_IsClosed_NextClientConnectsAndReplaysBacklog` | 连接建立时积压 flush 失败（发送超时）后死连接被关闭（单连接槽位释放），下一个客户端可接入并收到缓冲回放（以服务端 detach 日志 + 新连接接入为平台无关信号） | ConsoleBridge: 发送超时 detach |
| `DeadClientAfterEstablishedConnection_IsClosed_NextClientConnectsAndReplaysBacklog` | 连接已建立（flush 成功）后客户端停止读取，后续输出写失败（OnConsoleOutput 发送超时）→ 死连接被关闭、槽位释放，下一个客户端可接入并收到缓冲回放（回归：原始缺陷只摘除 writer 不关闭连接，槽位永久占用） | ConsoleBridge: 发送超时 detach |
| `BacklogReplayToSlowClient_AbortsAtBudget_RemainingLinesReplayOnNextConnection` | 慢速但持续读取的客户端使回放每行阻塞但低于发送超时：回放在时间预算处中止（日志含 "time budget"），剩余行在下一次连接完整回放（无重复） | ConsoleBridge: 回放时间预算 |

### 线程安全

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Concurrent_PublishWhileReading_NoDeadlock` | 发布+读取并发执行不死锁 | ConsoleBridge |
| `PendingFlushDuringConcurrentPublish_DeliversIntactLines` | 连接建立时的待发缓冲 flush 与另一线程的并发 Publish 竞争，写入锁保证两条路径互斥，递送的每一行均为未损坏的完整 token（backlog 与实时行各自完整到达） | ConsoleBridge |

### Agent 工作流集成

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `FullRoundTrip_CommandResponsePattern` | cmd1→response1→cmd2→response2 往返 | console-commands |
| `AgentLoop_OutputArrivesDuringReadWait` | ReadLine 等待时 Publish 立即到达 | ConsoleBridge |
| `AgentLoop_SendRead_SendRead_NoTriggerNeeded` | 5 轮发送-读取往返正常 | ConsoleBridge |
| `AgentLoop_MultipleOutputLines_PerCommand` | 一条命令产生多行输出 | ConsoleBridge |
| `AgentLoop_OutputBeforeConnect_DeliveredOnConnect` | 连接前产生的输出在连接后递送 | ConsoleBridge |
| `AgentLoop_Disconnect_Reconnect_FullFlow` | 断开→重连→新命令往返正常 | ConsoleBridge |
| `AgentLoop_ConcurrentPublish_DuringReadWait` | 并发发布过程中读取正常 | ConsoleBridge |
| `AgentLoop_Stress_50Rounds_NoDeadlock` | 50 轮往返不挂死 | ConsoleBridge |
| `AgentLoop_Dispose_WhileAgentWaitingForOutput` | Agent 等待时 Dispose 不挂 | ConsoleBridge |

### 构造器校验

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `Constructor_NullInput_Throws` | null input | ArgumentNullException |
| `Constructor_NullOutput_Throws` | null output | ArgumentNullException |
| `Constructor_DefaultOptions_HasExpectedPort` | 默认选项 | ActualPort > 0 |
| `Constructor_CustomPort_StoredInOptions` | Port=9876 | ActualPort=9876 |

## ConsoleBridgeServerErrorPathTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `HardClientRst_TriggersIOException_AndRecovers` | 客户端 RST 触发服务器读取 IOException，服务器恢复并接受新连接 | ConsoleBridge |
| `HardSocketClose_TriggersIOException_AndRecovers` | 客户端硬关闭 socket 触发 IOException，服务器恢复并接受新连接 | ConsoleBridge |
| `StreamShutdown_TriggersSocketException_AndRecovers` | 客户端 Shutdown 触发 SocketException，服务器恢复并接受新连接 | ConsoleBridge |
| `PendingFlush_BrokenClient_ServerRecovers` | 客户端连接后立即断开，待发输出 flush 命中已关闭流，异常处理后服务器恢复 | ConsoleBridge |
| `WriteFailure_LogsWarning_AndRecovers` | 输入 Enqueue 抛异常：记录 "Connection handler failed" 警告，服务器恢复并接受新连接 | ConsoleBridge |
| `Dispose_FaultedAcceptTask_LogsErrorInsteadOfSwallowing` | 已故障的 accept task 在 Dispose 时记录 "Accept loop faulted" 错误而非吞掉（回归守卫） | ConsoleBridge |
| `Dispose_AcceptTaskStillRunning_LogsTimeoutWarning` | accept task 未在 join 超时内停止时记录超时警告，Dispose 不等待其完整生命周期 | ConsoleBridge |
| `AcceptLoop_NonCancellationListenerError_LogsErrorAndStops` | 非取消监听错误记录 "Accept loop stopped" 且不误报 "Accept loop faulted" | ConsoleBridge |
| `AcceptLoop_NonCancellationError_StopsListenerAndAllowsRestart` | 非取消 accept 错误停止监听、`_started` 回滚，同一实例可重新 Start 绑定新端口并接受连接 | ConsoleBridge |
| `OnConsoleOutput_BrokenWriter_DoesNotThrowToCaller` | 客户端流已死时输出回调不向调用方抛异常，记录警告并清除 writer | ConsoleBridge |
| `Publish_BrokenClientWriter_DoesNotThrowToCaller` | 客户端 RST 后 `Publish` 不向游戏侧抛异常（输出侧隔离），服务器随后仍可接受新连接 | ConsoleBridge |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `Start_AfterDispose_ThrowsObjectDisposed` | Dispose 后调用 Start | ObjectDisposedException |
| `Start_Failure_RollsBackAndAllowsRetry` | 非法端口（-1）导致 Start 失败 | 抛异常且 started 标志回滚，修正端口后重试成功 |
| `Start_PortInUse_RollsBackListenerAndAllowsRetryAfterRelease` | 端口被占用导致 Start 失败 | SocketException，listener 与输出订阅完全回滚，端口释放后重试成功 |

## ConsoleBridgeOptionsTests 测试详情

### 选项配置

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `DefaultPort_IsExpectedValue` | 默认端口为 9876 | ConsoleBridge |
| `DefaultOptions_HasCorrectDefaults` | `ConsoleBridgeOptions` 默认值正确 | ConsoleBridge |
| `Options_CustomPort_Assigned` | 自定义端口被正确存储 | ConsoleBridge |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| 极高并发（100+ 并发客户端尝试连接）时的拒绝行为 | 连接风暴 | ConsoleBridge: 单连接模式 |
| 输出写失败路径（RST 竞态窗口内 `WriteLine` 抛异常→行入缓冲）无法黑盒确定性触发 | 兜底行为与分离路径共享缓冲代码，缓冲契约由确定性测试覆盖；RST 窗口内"写入虚空"丢失行是 TCP 固有竞态 | ConsoleBridge: 异常传播策略 |

---

[↑ 回到 Origo.ConsoleBridge.Tests](README.zh.md)
