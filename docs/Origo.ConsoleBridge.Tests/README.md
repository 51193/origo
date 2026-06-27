# Origo.ConsoleBridge.Tests

> [↑ 回到 Origo.manual](../README.md)
> [↔ 被测模块: Origo.ConsoleBridge](../Origo.ConsoleBridge/README.md)

## 测试策略概述

Origo.ConsoleBridge 的测试验证 TCP 远程控制台桥接服务器的完整行为：
服务器生命周期（Start/Stop/Dispose）、命令输入（客户端发送→输入队列 FIFO）、
控制台输出（Publish→客户端接收）、连接管理（单连接模式、断开/重连）、
线程安全（并发发布+读取不死锁）、Agent 工作流集成（命令-响应往返）。

所有测试通过真实 `TcpClient` 连接进行集成测试（非 mock），确保桥接服务器在真实网络环境中工作。

## 能力文档索引

| 能力 | 文档 | 验证重点 |
|------|------|---------|
| 桥接服务器 | [ConsoleBridgeServer.md](ConsoleBridgeServer.md) | 生命周期/输入/输出/连接管理/线程安全/Agent 工作流 |

---

[↑ 回到 Origo.manual](../README.md)
