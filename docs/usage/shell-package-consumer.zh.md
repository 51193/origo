<!-- docsync-pair: usage/shell-package-consumer -->
<!-- docsync-revision: 7 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# Shell-only 包消费验证

> [↑ 回到 usage](README.zh.md) · [↔ GodotAdapter Bootstrap](../Origo.GodotAdapter/Bootstrap/README.zh.md) · [↔ shell/kernel 边界](../architecture/shell-kernel-boundary.zh.md)

`scripts/package-consumer-smoke.sh` 在隔离的临时目录与本次运行独占的临时 NuGet
包缓存中验证两个全新消费者：一个只通过 NuGet 包恢复 `Origo.Core` 与
`Origo.GodotAdapter`，并以真实 Godot `Node` 入口启动；另一个只恢复
`Origo.ConsoleBridge`，通过公开桥接路径完成命令输入与输出回环。两者都不引用
任何 Origo 项目或测试辅助。

## 固定版本

| 项 | 值 | 权威来源 |
|----|----|----------|
| 包版本 | 当前仓库版本（如 `0.1.0`） | `Directory.Build.props` 的 `<Version>` |
| .NET | `net10.0` | `global.json` 的 SDK |
| Godot.NET.Sdk | `4.7.2` | `Origo.GodotAdapter.csproj`；消费者 fixture 必须一致，脚本会对比 |
| 包引用 | `Origo.Core` + `Origo.GodotAdapter` | `tools/ShellPackageConsumer/OrigoShellPackageConsumer.csproj` |
| ConsoleBridge 包引用 | `Origo.ConsoleBridge` | `tools/ConsoleBridgePackageConsumer/OrigoConsoleBridgePackageConsumer.csproj` |

消费者 fixture 不在 `Origo.sln` 中，也不含 `ProjectReference`；脚本每次把它复制到
临时目录后再恢复。

## 执行流程

`bash scripts/package-consumer-smoke.sh`：

1. 在临时本地 feed 中打包 `Origo.Core.Contracts`、`Origo.Core.Kernel`、
   `Origo.Core`、`Origo.GodotAdapter`、`Origo.ConsoleBridge` 五个包。
2. 检查 `Origo.Core.Contracts` 包包含 analyzer 资产
   `analyzers/dotnet/cs/Origo.SourceGeneration.dll`；缺失立即失败。随后调用
   `scripts/validate-release-packages.sh`，对同一临时 feed 校验五个包的身份、版本、
   精确 shell/kernel 配对、依赖方向、analyzer 资产与 kernel 隔离。
3. 把 Core/Adapter fixture 复制到仓库外的临时目录，为消费者 restore/build 切换
   到本次运行独占的临时 `NUGET_PACKAGES`，生成只含本地 feed 与 NuGet.org 的
   `nuget.config`，执行 `dotnet restore`。
4. 校验 `project.assets.json` 同时解析出 Core、Adapter、Contracts、Kernel 包，
   且源码中没有 `<ProjectReference>`。
5. 以 `-warnaserror` 构建 Core/Adapter 消费者；kernel runtime 程序集必须出现在
   构建输出中。
6. 复制 `KernelLeakProbe.cs.template` 为 `KernelLeakProbe.cs` 后重新构建；必须因
   `Origo.Core.Runtime.OrigoRuntime` 不可访问而失败（CS0246）。
7. 把 `tools/ConsoleBridgePackageConsumer` 复制到独立临时目录，只通过
   `PackageReference` 引用 `Origo.ConsoleBridge`，以 `-warnaserror` 完成
   restore/build；`project.assets.json` 必须解析出 `Origo.ConsoleBridge` 与
   `Origo.Core.Contracts`，不得解析出 `Origo.Core` 或 `Origo.Core.Kernel`，
   且源码中没有 `<ProjectReference>`；构建输出不得包含 Core shell 或 kernel
   runtime 程序集。
8. 运行 ConsoleBridge 消费者：它使用真实 `TcpClient` 连接 loopback，验证命令进入
   `IConsoleInputSource` 且 `IConsoleOutputChannel` 的输出回送到客户端，最后输出
   `CONSOLE_BRIDGE_PACKAGE_CONSUMER_OK`。
9. 复制 `tools/ConsoleBridgePackageConsumer/KernelLeakProbe.cs.template` 后重新构建
   ConsoleBridge 消费者；必须因 `Origo.Core.Runtime.OrigoRuntime` 不可访问而失败
   （CS0246），证明 kernel 编译资产也没有通过 ConsoleBridge 包泄漏。
10. 使用 `scripts/download-godot.sh` 取得 Godot 4.7.2，headless 运行 Core/Adapter
   fixture；标准输出必须包含 `SHELL_CONSUMER_STARTUP_OK kernel=True foreground=True`，
   且进程退出码为 0。

## 消费者入口

`tools/ShellPackageConsumer/ConsumerEntry.cs` 继承 `OrigoDefaultEntry`，只使用稳定
shell API：

- 在 `base._Ready()` 前设置入口配置、map、初始存档与保存根路径；
- 调用 `default(TypedData).TryGetString(...)`，证明 Contracts 包携带生成成员；
- 通过 `Runtime.DriveFrame` 驱动公开启动流水线；
- 检查 `Origo.Core.Kernel` 已作为运行期程序集加载、前台会话已建立；
- 调用 `Context.Save.ListSaves()` 后输出启动标记并退出。

`tools/ConsoleBridgePackageConsumer/Program.cs` 只引用 ConsoleBridge 包，使用真实
`TcpClient` 连接 loopback，验证命令进入 `IConsoleInputSource`、输出从
`IConsoleOutputChannel` 回到客户端，并输出 `CONSOLE_BRIDGE_PACKAGE_CONSUMER_OK`。

## CI 与失败语义

- 普通 CI 的 `godot-integration-tests` job 在 Godot integration 测试之后运行该
  脚本；本地 `scripts/ci.sh` 同样在最后执行。
- 消费者 restore/build 使用本次运行独占的临时 NuGet 包缓存；脚本断言
  `project.assets.json` 指向该缓存，宿主全局缓存中的同名同版本 Origo 包不能
  绕过本地 feed 让 smoke 验证旧制品。
- 包缺失、release package validator 失败、analyzer 资产缺失、任一消费者出现
  `ProjectReference`、NuGet/编译警告、kernel 类型可编译、Core/Adapter 消费者的
  kernel runtime 未加载、ConsoleBridge 消费者意外解析出 Core/Kernel 或构建输出
  含 Core shell/kernel runtime 程序集、ConsoleBridge 回环失败或缺少
  `CONSOLE_BRIDGE_PACKAGE_CONSUMER_OK`、Godot 启动失败或缺少
  `SHELL_CONSUMER_STARTUP_OK` 都会使 job 失败。
- fixture 与脚本的版本 pin 由 `scripts/package-consumer-smoke.sh` 与
  `OrigoShellPackageConsumer.csproj` 共同维护；升级 Godot SDK 时必须在同一变更
  中同步 Adapter 与消费者。

---
[↑ 回到 Origo 手册](../README.zh.md)
