# 控制台桥接服务器 测试

> [↑ 回到 Origo.ConsoleBridge.Tests](README.md)
> [↔ 被测模块: Origo.ConsoleBridge](../Origo.ConsoleBridge/README.md)
> [↔ 被测行为: usage/console-commands](../usage/console-commands.md)

## 被测行为概览

验证 ConsoleBridgeServer 的完整 TCP 桥接行为。所有测试使用真实 `TcpClient` 连接。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `ConsoleBridgeServerTests.cs` | 服务器完整行为 |
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
| `OutputChannel_PublishNullString_ArrivesAsEmpty` | Publish(null) → 客户端读到 "" | ConsoleBridge |
| `OutputChannel_LargeVolume_ManyLines_AllDelivered` | 100 行全部递送 | ConsoleBridge |
| `OutputChannel_ConcurrentPublish_AllDelivered` | 10 线程并发发布，全部递送 | ConsoleBridge |

### 连接管理

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `SecondConnection_IsRejected` | 第二个连接被拒绝，第一个连接不受影响 | ConsoleBridge: 单连接模式 |
| `SecondConnection_CommandDoesNotArrive` | 第二个连接的命令不被处理 | ConsoleBridge |
| `ClientDisconnect_ServerAcceptsNewConnection` | 断开后新连接可建立 | ConsoleBridge |
| `ClientDisconnect_ThenThirdAccepted` | 多次断开→重连都正常 | ConsoleBridge |
| `ClientImmediateDisconnect_ServerRecovers` | 立即断开后服务器恢复正常 | ConsoleBridge |
| `MidSession_ClientHardDisconnect_ServerRecovers` | 客户端强制关闭 socket（非正常 Dispose），服务器恢复 | ConsoleBridge |
| `MidSession_ClientAbort_NextConnectionAccepted` | 会话中客户端中断后新连接可建立 | ConsoleBridge |

### 线程安全

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Concurrent_PublishWhileReading_NoDeadlock` | 发布+读取并发执行不死锁 | ConsoleBridge |

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

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| 极高并发（100+ 并发客户端尝试连接）时的拒绝行为 | 连接风暴 | ConsoleBridge: 单连接模式 |

---

[↑ 回到 Origo.ConsoleBridge.Tests](README.md)
