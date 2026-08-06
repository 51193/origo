<!-- docsync-pair: Origo.ConsoleBridge.Tests/ConsoleBridgeServer -->
<!-- docsync-revision: 3 -->
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
