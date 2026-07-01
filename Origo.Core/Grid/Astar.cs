using System;
using System.Collections.Generic;

namespace Origo.Core.Grid;

public static class Astar
{
    private static readonly (int X, int Z)[] _neighbors = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    public static List<GridPos>? FindPath(GridPos start, GridPos end, int gridSize,
        Func<GridPos, bool> isBlocked)
    {
        ArgumentNullException.ThrowIfNull(isBlocked);
        if (!IsInBounds(start, gridSize))
            return null;
        if (!IsInBounds(end, gridSize))
            return null;
        if (isBlocked(end))
            return null;
        if (start == end)
            return [];

        var maxSteps = gridSize * gridSize;
        var openSet = new PriorityQueue<GridPos, float>();
        var gScore = new Dictionary<GridPos, float> { [start] = 0f };
        var cameFrom = new Dictionary<GridPos, GridPos>();
        var closedSet = new HashSet<GridPos>();

        openSet.Enqueue(start, Heuristic(start, end));

        while (openSet.Count > 0 && closedSet.Count < maxSteps)
        {
            var current = openSet.Dequeue();
            if (current == end)
                return ReconstructPath(cameFrom, current);

            closedSet.Add(current);

            foreach (var (dx, dz) in _neighbors)
            {
                var neighbor = new GridPos(current.X + dx, current.Z + dz);

                if (!IsInBounds(neighbor, gridSize))
                    continue;
                if (closedSet.Contains(neighbor))
                    continue;
                if (isBlocked(neighbor))
                    continue;

                var tentativeG = gScore[current] + 1f;
                var existingG = gScore.GetValueOrDefault(neighbor, float.MaxValue);
                if (tentativeG >= existingG)
                    continue;

                cameFrom[neighbor] = current;
                gScore[neighbor] = tentativeG;
                var f = tentativeG + Heuristic(neighbor, end);
                openSet.Enqueue(neighbor, f);
            }
        }

        return null;
    }

    private static bool IsInBounds(GridPos p, int gridSize) => p.X >= 0 && p.X < gridSize && p.Z >= 0 && p.Z < gridSize;

    private static float Heuristic(GridPos a, GridPos b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    private static List<GridPos> ReconstructPath(Dictionary<GridPos, GridPos> cameFrom, GridPos current)
    {
        var totalPath = new List<GridPos> { current };
        while (cameFrom.TryGetValue(current, out var prev))
        {
            current = prev;
            totalPath.Add(current);
        }

        totalPath.Reverse();
        totalPath.RemoveAt(0);
        return totalPath;
    }
}
