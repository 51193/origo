using System;
using System.Collections.Generic;
using Origo.EvolutionDemo.Logic;

namespace Origo.EvolutionDemo;

/// <summary>
///     Uniform spatial buckets for food/corpse and creature sensor points,
///     rebuilt once per fixed simulation tick. The food buckets are shared with
///     the food-source scan, which counts patch food by querying the tick's grid
///     instead of walking every session entity.
/// </summary>
internal sealed class SensorGrid
{
    private readonly List<SensorPoint>[] _food;
    private readonly List<SensorPoint>[] _creatures;
    private readonly int _dimension;
    private readonly float _cellSize;

    private SensorGrid(
        List<SensorPoint>[] food,
        List<SensorPoint>[] creatures,
        int dimension,
        float cellSize)
    {
        _food = food;
        _creatures = creatures;
        _dimension = dimension;
        _cellSize = cellSize;
    }

    /// <summary>Food buckets, addressed by the grid's cell index.</summary>
    public List<SensorPoint>[] Food => _food;

    /// <summary>Creature buckets, addressed by the grid's cell index.</summary>
    public List<SensorPoint>[] Creatures => _creatures;

    private static SensorGrid? GridScratch;

    public static SensorGrid Build(
        SensorPoint[] food,
        int foodCount,
        SensorPoint[] creatures,
        int creatureCount)
    {
        var cellSize = MathF.Max(Tuning.SenseRange, Tuning.PondRadius / 10f);
        var dimension = Math.Max(1, (int)MathF.Ceiling((Tuning.PondRadius * 2f) / cellSize) + 1);
        var grid = GridScratch;
        if (grid is null
            || grid._dimension != dimension
            || MathF.Abs(grid._cellSize - cellSize) > 0.001f)
        {
            var foodBuckets = new List<SensorPoint>[dimension * dimension];
            var creatureBuckets = new List<SensorPoint>[dimension * dimension];
            for (var i = 0; i < foodBuckets.Length; i++)
            {
                foodBuckets[i] = [];
                creatureBuckets[i] = [];
            }

            grid = new SensorGrid(foodBuckets, creatureBuckets, dimension, cellSize);
            GridScratch = grid;
        }
        else
        {
            for (var i = 0; i < grid._food.Length; i++)
            {
                grid._food[i].Clear();
                grid._creatures[i].Clear();
            }
        }

        for (var i = 0; i < foodCount; i++)
            grid._food[grid.CellIndex(food[i].Position.X, food[i].Position.Y)].Add(food[i]);
        for (var i = 0; i < creatureCount; i++)
            grid._creatures[grid.CellIndex(creatures[i].Position.X, creatures[i].Position.Y)].Add(creatures[i]);
        return grid;
    }

    public void Query(float x, float y, List<SensorPoint>[] buckets, List<SensorPoint> target) =>
        Query(x, y, Tuning.SenseRange, buckets, target);

    public void Query(
        float x,
        float y,
        float radius,
        List<SensorPoint>[] buckets,
        List<SensorPoint> target)
    {
        target.Clear();
        var cellX = CellCoordinate(x);
        var cellY = CellCoordinate(y);
        var cells = Math.Max(1, (int)MathF.Ceiling(radius / _cellSize));
        for (var dy = -cells; dy <= cells; dy++)
        {
            var row = cellY + dy;
            if (row < 0 || row >= _dimension)
                continue;
            for (var dx = -cells; dx <= cells; dx++)
            {
                var column = cellX + dx;
                if (column < 0 || column >= _dimension)
                    continue;
                target.AddRange(buckets[(row * _dimension) + column]);
            }
        }
    }

    private int CellIndex(float x, float y) => (CellCoordinate(y) * _dimension) + CellCoordinate(x);

    private int CellCoordinate(float value)
    {
        var cell = (int)((value + Tuning.PondRadius) / _cellSize);
        return Math.Clamp(cell, 0, _dimension - 1);
    }
}
