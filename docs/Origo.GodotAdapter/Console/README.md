# Console

> [↑ 回到 Origo.GodotAdapter](../README.md) · [↔ Core: Runtime/Console](../../Origo.Core/Runtime/Console/README.md)

## 概述

Godot 适配层的控制台命令扩展。一个适配层的命令处理器基类（提供 `OrigoRuntime` 引用），以及两个 Godot 特有的命令——`press_button` 模拟 Button 点击，`tree_debug` 打印实体节点树。

## 包含文件

| 文件 | 职责 |
|------|------|
| `CommandHandlerBase.cs` | 适配层命令处理器基类，持有 `OrigoRuntime` 引用，校验参数数量 |
| `PressButtonCommandHandler.cs` | Godot 特有命令：按 entity + path 查找并发射 Button.Pressed 信号 |
| `TreeDebugCommandHandler.cs` | Godot 特有命令：打印实体的完整 Godot 节点树（含动态创建的子节点） |
| `CameraViewCommandHandler.cs` | Godot 特有命令：显示活跃摄像头视角下所有实体节点的屏幕坐标和深度 |
| `ProjectionHelper.cs` | 摄像头投影数学工具：世界坐标 → 屏幕像素坐标（视锥体裁剪） |

## 模块详解

### CommandHandlerBase

继承自 Core 的 `IConsoleCommandHandler`，提供与 Core `ConsoleCommandHandlerBase` 相同的参数校验逻辑。区别在于此基类直接持有 `OrigoRuntime` 引用（而非子类注入），简化 Godot 侧的命令实现。

### PressButtonCommandHandler

```
press_button <entity> <path>
```

流程：
1. `Runtime.Snd.FindByName(entity)` 找到实体
2. 检查实体是否为 `GodotSndEntity` 类型
3. 通过 `godotEntity.GetNodeOrNull<Button>(path)` 查找 Button 节点
4. `button.EmitSignal(BaseButton.SignalName.Pressed)` 模拟按下

### TreeDebugCommandHandler

```
tree_debug <entity>
```

流程：
1. `Runtime.Snd.FindByName(entity)` 找到实体
2. 检查实体是否为 `GodotSndEntity` 类型
3. 递归遍历实体的 Godot 节点树，打印每个节点的 `[类型] "名称"`
4. 输出完整节点树信息，用于调试路径解析问题

### CameraViewCommandHandler

```
camera_view
```

流程：
1. 通过 `Engine.GetMainLoop()` 获取 SceneTree → Root Viewport
2. `viewport.GetCamera3D()` 获取活跃摄像头
3. 遍历 foreground session 中的所有 `GodotSndEntity`
4. 对每个实体的子节点递归遍历：
   - `Node3D` → 通过 `ProjectionHelper.ProjectWorldToScreen` 计算 2D 屏幕坐标和深度
   - `Control` → 直接读取 `GlobalPosition`（UI 空间）
5. 输出格式：`entity / node [类型] screen=(X, Y) depth=D`

> 遮盖/遮挡检测暂未实现，当前显示视锥体内所有可见节点。深度值可用于手动排序判断前后关系。

## 设计决策

### 为什么适配层还需要自己的 CommandHandlerBase

Core 的 `ConsoleCommandHandlerBase` 要求子类持有对 `OrigoRuntime` 的引用。适配层基类提供一致的 `Runtime` 属性访问方式，避免每个 Godot 命令处理器重复相同的注入模式。

### 为什么 PressButton 需要 Godot 实体类型检查

`Runtime.Snd.FindByName` 返回 `ISndEntity` 抽象接口，但 `GetNodeOrNull<Button>` 是 Godot.Node 的方法。运行时检查确保类型安全——如果实体是纯内存实体（如 `StubSndEntity`），提前用清晰错误信息告知而非 NullReferenceException。

---
[↑ 回到 Origo.GodotAdapter](../README.md)
