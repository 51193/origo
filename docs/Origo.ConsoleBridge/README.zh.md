<!-- docsync-pair: Origo.ConsoleBridge/README -->
<!-- docsync-revision: 7 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# Origo.ConsoleBridge

> [↑ 回到 Origo.manual](../README.zh.md) · [↔ Core: Runtime/Console](../Origo.Core/Runtime/Console/README.zh.md)

## 概述

TCP 远程控制台桥接服务器。允许通过 telnet/nc 连接（默认端口 9876），远程执行 Origo 控制台命令并接收输出。单连接模式：同时只允许一个客户端连接。

## 包含文件

| 文件 | 职责 |
|------|------|
| `ConsoleBridgeOptions.cs` | 配置选项：端口号（默认 9876）|
| `ConsoleBridgeServer.cs` | TCP 控制台桥接器：内部异步 I/O 接受连接与读取命令 |

## 架构

```
telnet client ──TCP:9876──> ConsoleBridgeServer
                                ├── input:  ConsoleInputBuffer.Enqueue(line)
                                └── output: ConsoleOutputChannel.Subscribe(OnConsoleOutput)
                                              → StreamWriter.WriteLine
```

**线程模型**：
- **异步 I/O**：`AcceptTcpClientAsync` 和 `ReadLineAsync` 在 ThreadPool 上运行，不占用专用线程。CancellationToken 替代 `Monitor.Wait` 轮询和 `ReceiveTimeout`，取消操作立即响应。
- **输出路径同步**：`OnConsoleOutput` 回调中的 `StreamWriter.WriteLine` 保持同步——控制台输出是短 kernel 调用，TCP 发送缓冲区在实际使用中不会满，异步化得不偿失。

## 使用方式

```bash
# 启动服务器（在 Godot 项目代码中）
var server = new ConsoleBridgeServer(consoleInput, consoleOutput);
server.Start();

# 客户端连接
nc localhost 9876
> help
> spawn my_entity template_basic
> snd_count
```

## 安全边界

> ⚠️ **无认证、无加密**。`ConsoleBridgeServer` 是明文 TCP 协议：不校验客户端身份、不加密命令与输出内容，且单连接模式意味着先到先得（占用连接的客户端可执行任意控制台命令）。

- **适用场景**：本机（`localhost`）开发调试、Agent 驱动开发与自动化测试。监听器仅绑定 `IPAddress.Loopback`，故局域网直连不可行——远程访问需经 SSH 隧道等既有安全通道（见下）。
- **禁止**：将端口直接暴露到公网或不可信网络。
- **如需远程安全访问**：通过 SSH 隧道（`ssh -L 9876:localhost:9876 ...`）、VPN 或反向代理等既有安全通道接入，由外层通道提供认证与加密。
- 若产品化需要认证/加密，应作为独立的安全层实现，不改变本模块的调试定位。

## 设计决策

### 为什么单连接模式

Origo 的游戏帧循环是单线程的。多连接意味着多条命令流并发进入 `ConsoleInputBuffer`，但命令执行是帧内串行的——多客户端场景下无法确定哪条命令先执行，导致不可预期的结果。

### 为什么输出采用 publish-subscribe 而非直接写入 socket

`ConsoleBridgeServer` 订阅 `IConsoleOutputChannel`，所有日志和控制台输出（不只是 Bridge 连接的客户端的命令输出）都推到连接。这让远程连接者可以看到完整的游戏日志流，便于调试。

### 为什么采用异步 I/O 而非手动线程

`AcceptTcpClientAsync` 和 `ReadLineAsync` 在操作系统层面通过 IOCP 等待，不占用专用线程栈：

- 空闲时零 CPU 开销（无需每 100ms 唤醒检查状态）
- `Dispose()` 关闭延迟从最坏 6 秒（两个 `Thread.Join(3000)`）大幅降低（`CancellationToken` 立即中断异步 I/O）；`Dispose` 会等待 accept 循环加入，最坏超时上限为 3 秒（`_disposeJoinTimeoutMs`），超时仅记录警告日志
- 无需 `ReceiveTimeout` 读超时兜底——取消令牌直接中断 `ReadLineAsync`

输出路径保持同步 `StreamWriter.WriteLine`，避免将 `Action<string>` 回调改为 `Func<string, Task>`（会级联污染 `IConsoleOutputChannel` 接口）。

### 异常传播策略（fail-fast）

`ConsoleBridgeServer` 不捕获 I/O 异常做静默降级，但区分故障层级：连接级异常（客户端断连、`ReadLineAsync` 的 I/O 错误）被隔离并记录 `Warning` 日志（含完整堆栈）后继续接受新连接；accept 循环遇到非取消的系统级 socket 错误时记录 `Error` 日志并停止监听（由宿主进程重启）；`Dispose` 会检查 accept 任务状态，fault 时记录 `Error` 日志而非吞掉。 `Start` 失败（如端口被占用）会回滚内部状态，可在释放资源后重试同一实例。

### 输出缓冲区溢出

当没有客户端连接时，控制台输出缓冲在内存队列中（上限 1000 行）。超出上限时最旧的行被丢弃，并在客户端下次连接时在输出流最前面写入一条警告消息（`[ConsoleBridge] Warning: N output line(s) were dropped due to buffer overflow.`），以确保数据丢失可被观察到。

---
[↑ 回到 Origo.manual](../README.zh.md)
