namespace Origo.Core.Grid;

/// <summary>
///     Two-dimensional grid cell coordinate on the X/Z plane (X = column, Z = row).
///     Immutable value type with structural equality.
/// </summary>
public readonly record struct GridPos(int X, int Z);
