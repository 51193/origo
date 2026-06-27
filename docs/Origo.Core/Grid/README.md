# Grid

> [↑ 回到 Origo.Core](../README.md)

## 概述

通用网格工具集。提供网格坐标类型、世界坐标双向转换、A* 寻路、坐标解析等能力。任何基于方形网格的游戏（如战棋、Roguelike、沙盒）都可使用。

## 包含文件

| 文件 | 职责 |
|------|------|
| `GridPos.cs` | `readonly record struct`，表示 2D 整数网格坐标 |
| `GridCoordinateSystem.cs` | 单轴/双轴 GridToWorld / WorldToGrid 坐标转换 |
| `Astar.cs` | 通用 A* 寻路，接受 `Func<GridPos, bool>` 阻塞检测委托 |
| `GridParser.cs` | 坐标字符串解析（`"x,z"` 格式 + `JsonElement`） |

## 实现详解

### GridPos

```csharp
public readonly record struct GridPos(int X, int Z);
```

值类型，自动获得相等性比较、解构、`ToString()`。作为框架内所有网格坐标的统一载体。

### GridCoordinateSystem

全部为静态方法，无状态：

- **GridToWorld(int gridCoord, float cellSize, int gridSize)**：单轴转换，将网格整型坐标转为世界中心坐标
- **WorldToGrid(float worldCoord, float cellSize, int gridSize, out bool outOfBounds)**：单轴逆转换，向下取整
- **GridToWorld(GridPos pos, float cellSize, int gridSize)**：2D 便捷重载，返回 `(float X, float Z)` 元组
- **WorldToGrid(float worldX, float worldZ, float cellSize, int gridSize, out bool outOfBounds)**：2D 逆转换，返回 `GridPos`

公式（单轴）：`worldCoord = gridCoord * cellSize - (gridSize * cellSize) / 2 + cellSize * 0.5f`。

### Astar

```csharp
public static List<GridPos>? FindPath(GridPos start, GridPos end, int gridSize, Func<GridPos, bool> isBlocked)
```

标准 A* 搜索，使用欧几里得距离启发式。返回不含起点的路径列表，无可行路径时返回 `null`。

- `isBlocked` 是 `Func<GridPos, bool>` 委托，由调用方组装阻塞检测逻辑（如合并地形阻挡 + 动态阻挡）
- 自动校验终点是否越界或被阻挡

### GridParser

```csharp
public static (int X, int Z)? ParseCoords(object? input)
```

解析 `"x,z"` 格式的坐标字符串。支持 `string` 和 `JsonElement` 输入，容错空格，非法输入返回 `null`。

## 设计决策

### 为什么拆为单轴而非双轴接口

单轴转换（`GridToWorld(int, float, int)`）比双轴（`GridToWorld(int, int, int, float, out float, out float)`）更灵活：不同轴的cellSize可以不同（如非正方形网格），且不需要out参数，返回值为单一float可直接参与Godot Vector3构造。

### 为什么同时提供 2D 便捷重载

在实际使用中，绝大多数调用点使用相同的 cellSize 和 gridSize 且同时转换 X 和 Z。提供 `GridToWorld(GridPos, float, int)` 重载可消除 `GridToWorld(gx, ...)` + `GridToWorld(gz, ...)` 的重复样板。单轴方法仍然保留作为主 API，2D 重载是纯便捷层。

### 为什么 isBlocked 使用委托而非集合

`Func<GridPos, bool>` 比 `HashSet<GridPos>` 更通用：调用方可组合多个阻塞源（地形数据 + 动态阻挡），无需预先合并为单一集合。迁移成本低——现有 `HashSet<GridPos>` 用户只需传入 `p => set.Contains(p)`。

### 为什么不放在Snd命名空间

坐标转换和寻路是通用几何工具，不依赖SND实体模型。独立命名空间使其可用于任何游戏逻辑（包括非Snd系统）。

---

[↑ 回到 Origo.Core](../README.md)
