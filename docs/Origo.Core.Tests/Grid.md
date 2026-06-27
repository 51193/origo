# 网格 测试

> [↑ 回到 Origo.Core.Tests](README.md)
> [↔ 被测模块: Origo.Core/Grid](../Origo.Core/Grid/README.md)

## 被测行为概览

验证网格坐标系的单轴/双轴坐标转换（GridToWorld / WorldToGrid）、GridPos 值语义、A* 寻路算法、GridParser 坐标字符串解析。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `GridCoordinateSystemTests.cs` | 单轴 GridToWorld / WorldToGrid 正确性与往返一致性 |
| `GridPosTests.cs` | record struct 相等性、解构 |
| `GridCoordinateSystem2DTests.cs` | 2D 便捷重载：GridToWorld(GridPos) / WorldToGrid 2D 往返 |
| `AstarTests.cs` | A* 寻路：直达、绕障碍、不可达、越界、空路径 |
| `GridParserTests.cs` | 坐标解析：有效输入、无效格式、null、JsonElement |

## GridCoordinateSystemTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `GridToWorld_Origin_CellCenter` | 原点网格坐标转换到世界单元格中心 | Grid |
| `GridToWorld_MaxCoord_CellCenter` | 最大网格坐标转换 | Grid |
| `GridToWorld_Center_CellCenter` | 中心网格坐标转换 | Grid |
| `WorldToGrid_OriginMapsToZero` | 世界原点边界映射到网格 0 | Grid |
| `GridToWorld_WorldToGrid_RoundTrip` | 全网格往返一致性 | Grid |
| `NonUnitCellSize_CorrectOffset` | 非单位 CellSize 偏移正确 | Grid |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `WorldToGrid_OutOfBounds_ReportsTrue` | 世界坐标远超网格边界 | `outOfBounds = true` |

## GridPosTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Equals_SameValues_True` | 相同坐标值判等 | Grid |
| `Deconstruct_ReturnsComponents` | 解构返回 X / Z | Grid |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `Equals_DifferentValues_False` | 不同坐标值 | `== false` |

## GridCoordinateSystem2DTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `GridToWorld2D_Origin_CellCenter` | 2D 原点转换 | Grid |
| `GridToWorld2D_DifferentAxes_Independent` | 不同轴独立转换 | Grid |
| `WorldToGrid2D_RoundTrip` | 2D 全网格往返 | Grid |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `WorldToGrid2D_OutOfBounds_ReportsTrue` | 单轴越界 | `outOfBounds = true` |
| `WorldToGrid2D_PartialOutOfBounds_ReportsTrue` | 单轴正常单轴越界 | `outOfBounds = true` |

## AstarTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `FindPath_StartEqualsEnd_ReturnsEmptyPath` | 起终点重合返回空路径 | Grid/Astar |
| `FindPath_StraightLine_ReturnsDirectPath` | 直线无阻挡返回直接路径 | Grid/Astar |
| `FindPath_Diagonal_ReturnsCorrectLength` | 对角线路径长度正确 | Grid/Astar |
| `FindPath_AroundObstacle_FindsPath` | 绕过障碍物找到路径 | Grid/Astar |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `FindPath_BlockedEndpoint_ReturnsNull` | 终点被阻挡 | `null` |
| `FindPath_CompletelyBlocked_ReturnsNull` | 全图阻挡 | `null` |
| `FindPath_OutOfBounds_ReturnsNull` | 终点越界 | `null` |
| `FindPath_NoPathExists_ReturnsNull` | 四周完全围堵 | `null` |

## GridParserTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `ParseCoords_ValidString_ReturnsCoords` | `"3,5"` 解析为 (3, 5) | Grid/GridParser |
| `ParseCoords_WithSpaces_Trims` | 空格容错 | Grid/GridParser |
| `ParseCoords_NegativeValues_Works` | 负数坐标 | Grid/GridParser |
| `ParseCoords_JsonElement_Works` | JsonElement 输入 | Grid/GridParser |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `ParseCoords_InvalidFormat_ReturnsNull` | `"abc"`, `"3"`, 空串 | `null` |
| `ParseCoords_NullInput_ReturnsNull` | null 输入 | `null` |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| A* 在大网格（10000×10000）上的性能边界 | 极端尺寸性能 | Grid/Astar |
| WorldToGrid 2D 各轴独立非正方形网格 | 非正方形网格 | Grid |
| GridCoordinateSystem 整数溢出防护 | 极大 gridSize × cellSize | Grid |

---

[↑ 回到 Origo.Core.Tests](README.md)
