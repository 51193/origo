using System;

namespace Origo.EvolutionDemo.Logic;

/// <summary>Mutable body position used by the collision resolver.</summary>
public struct CollisionBody
{
    /// <summary>Horizontal pond-local position.</summary>
    public float X { get; set; }

    /// <summary>Vertical pond-local position.</summary>
    public float Y { get; set; }

    /// <summary>Per-creature body radius for the body-aware resolver.</summary>
    public float Radius { get; set; }
}

/// <summary>
///     Soft separation pass for creatures. Overlapping circles are pushed apart
///     symmetrically; the resolver is deliberately positional rather than
///     velocity-based so it cannot inject unstable motion into the simulation.
/// </summary>
public static class CreatureCollision
{
    private const float MinimumDistanceSquared = 0.0001f;

    /// <summary>
    ///     Sweep variant for larger populations. Bodies are visited in X order
    ///     and only pairs whose X delta can overlap are tested; this keeps the
    ///     collision pass near-linear for the 2k–5k target range.
    /// </summary>
    public static int ResolveSweep(
        CollisionBody[] bodies,
        float bodyRadius,
        float pondRadius)
    {
        ArgumentNullException.ThrowIfNull(bodies);
        if (bodyRadius <= 0f)
            throw new ArgumentOutOfRangeException(nameof(bodyRadius), "Body radius must be positive.");
        if (pondRadius <= bodyRadius)
            throw new ArgumentOutOfRangeException(nameof(pondRadius), "Pond radius must exceed body radius.");
        if (bodies.Length <= 1)
            return 0;

        var order = new int[bodies.Length];
        for (var i = 0; i < order.Length; i++)
            order[i] = i;
        Array.Sort(order, (left, right) => bodies[left].X.CompareTo(bodies[right].X));

        var minDistance = bodyRadius * 2f;
        var minDistanceSquared = minDistance * minDistance;
        var overlaps = 0;
        for (var leftIndex = 0; leftIndex < order.Length; leftIndex++)
        {
            var left = order[leftIndex];
            for (var rightIndex = leftIndex + 1; rightIndex < order.Length; rightIndex++)
            {
                var right = order[rightIndex];
                var dx = bodies[right].X - bodies[left].X;
                if (dx > minDistance)
                    break;
                var dy = bodies[right].Y - bodies[left].Y;
                var distanceSquared = (dx * dx) + (dy * dy);
                if (distanceSquared >= minDistanceSquared)
                    continue;

                overlaps++;
                float distance;
                float normalX;
                float normalY;
                if (distanceSquared < MinimumDistanceSquared)
                {
                    var angle = (leftIndex * 0.7548777f) + (rightIndex * 0.5698403f);
                    distance = 0f;
                    normalX = MathF.Cos(angle * MathF.Tau);
                    normalY = MathF.Sin(angle * MathF.Tau);
                }
                else
                {
                    distance = MathF.Sqrt(distanceSquared);
                    normalX = dx / distance;
                    normalY = dy / distance;
                }

                var push = (minDistance - distance) * 0.5f;
                bodies[left].X -= normalX * push;
                bodies[left].Y -= normalY * push;
                bodies[right].X += normalX * push;
                bodies[right].Y += normalY * push;
            }
        }

        var limit = pondRadius - bodyRadius;
        var limitSquared = limit * limit;
        for (var i = 0; i < bodies.Length; i++)
        {
            var distanceSquared = (bodies[i].X * bodies[i].X) + (bodies[i].Y * bodies[i].Y);
            if (distanceSquared <= limitSquared)
                continue;
            var distance = MathF.Sqrt(distanceSquared);
            if (distance <= 0.0001f)
            {
                bodies[i].X = 0f;
                bodies[i].Y = 0f;
                continue;
            }

            var scale = limit / distance;
            bodies[i].X *= scale;
            bodies[i].Y *= scale;
        }

        return overlaps;
    }

    /// <summary>
    ///     Resolves pairwise circle overlaps in place. Returns the number of
    ///     overlapping pairs detected during the final pass.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for invalid geometry.</exception>
    public static int Resolve(
        Span<CollisionBody> bodies,
        float bodyRadius,
        float pondRadius,
        int iterations = 2)
    {
        if (bodyRadius <= 0f)
            throw new ArgumentOutOfRangeException(nameof(bodyRadius), "Body radius must be positive.");
        if (pondRadius <= bodyRadius)
            throw new ArgumentOutOfRangeException(nameof(pondRadius), "Pond radius must exceed body radius.");
        if (iterations <= 0)
            throw new ArgumentOutOfRangeException(nameof(iterations), "At least one iteration is required.");

        var minDistance = bodyRadius * 2f;
        var minDistanceSquared = minDistance * minDistance;
        var overlapCount = 0;
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            overlapCount = 0;
            for (var i = 0; i < bodies.Length; i++)
            {
                for (var j = i + 1; j < bodies.Length; j++)
                {
                    var dx = bodies[j].X - bodies[i].X;
                    var dy = bodies[j].Y - bodies[i].Y;
                    var distanceSquared = (dx * dx) + (dy * dy);
                    if (distanceSquared >= minDistanceSquared)
                        continue;

                    overlapCount++;
                    float distance;
                    float normalX;
                    float normalY;
                    if (distanceSquared < MinimumDistanceSquared)
                    {
                        var angle = (i * 0.7548777f) + (j * 0.5698403f);
                        distance = 0f;
                        normalX = MathF.Cos(angle * MathF.Tau);
                        normalY = MathF.Sin(angle * MathF.Tau);
                    }
                    else
                    {
                        distance = MathF.Sqrt(distanceSquared);
                        normalX = dx / distance;
                        normalY = dy / distance;
                    }

                    var push = (minDistance - distance) * 0.5f;
                    bodies[i].X -= normalX * push;
                    bodies[i].Y -= normalY * push;
                    bodies[j].X += normalX * push;
                    bodies[j].Y += normalY * push;
                }
            }

            var limit = pondRadius - bodyRadius;
            var limitSquared = limit * limit;
            for (var i = 0; i < bodies.Length; i++)
            {
                var distanceSquared = (bodies[i].X * bodies[i].X) + (bodies[i].Y * bodies[i].Y);
                if (distanceSquared <= limitSquared)
                    continue;
                var distance = MathF.Sqrt(distanceSquared);
                if (distance <= 0.0001f)
                {
                    bodies[i].X = 0f;
                    bodies[i].Y = 0f;
                    continue;
                }

                var scale = limit / distance;
                bodies[i].X *= scale;
                bodies[i].Y *= scale;
            }
        }

        return overlapCount;
    }

    /// <summary>
    ///     Sweep resolver that uses each body's own radius. Bodies must carry a
    ///     positive Radius; the pond clamp also uses that radius.
    /// </summary>
    public static int ResolveSweep(CollisionBody[] bodies, float pondRadius)
    {
        ArgumentNullException.ThrowIfNull(bodies);
        var maxRadius = ValidateBodyRadii(bodies, pondRadius);
        if (bodies.Length <= 1)
            return 0;

        var order = new int[bodies.Length];
        for (var i = 0; i < order.Length; i++)
            order[i] = i;
        Array.Sort(order, (left, right) => bodies[left].X.CompareTo(bodies[right].X));

        var overlaps = 0;
        for (var leftIndex = 0; leftIndex < order.Length; leftIndex++)
        {
            var left = order[leftIndex];
            for (var rightIndex = leftIndex + 1; rightIndex < order.Length; rightIndex++)
            {
                var right = order[rightIndex];
                var dx = bodies[right].X - bodies[left].X;
                if (dx > bodies[left].Radius + maxRadius)
                    break;
                var minDistance = bodies[left].Radius + bodies[right].Radius;
                var dy = bodies[right].Y - bodies[left].Y;
                var distanceSquared = (dx * dx) + (dy * dy);
                if (distanceSquared >= minDistance * minDistance)
                    continue;

                overlaps++;
                var (distance, normalX, normalY) = Separation(dx, dy, distanceSquared, leftIndex, rightIndex);
                var push = (minDistance - distance) * 0.5f;
                bodies[left].X -= normalX * push;
                bodies[left].Y -= normalY * push;
                bodies[right].X += normalX * push;
                bodies[right].Y += normalY * push;
            }
        }

        ClampBodies(bodies, pondRadius);
        return overlaps;
    }

    /// <summary>
    ///     Pairwise resolver that uses each body's own radius. Body changes can
    ///     therefore alter collision spacing without changing the solver.
    /// </summary>
    public static int Resolve(Span<CollisionBody> bodies, float pondRadius, int iterations = 2)
    {
        foreach (var body in bodies)
        {
            if (body.Radius <= 0f)
                throw new ArgumentOutOfRangeException(nameof(bodies), "Every body radius must be positive.");
            if (pondRadius <= body.Radius)
                throw new ArgumentOutOfRangeException(nameof(pondRadius), "Pond radius must exceed every body radius.");
        }

        if (iterations <= 0)
            throw new ArgumentOutOfRangeException(nameof(iterations), "At least one iteration is required.");

        var overlapCount = 0;
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            overlapCount = 0;
            for (var i = 0; i < bodies.Length; i++)
            {
                for (var j = i + 1; j < bodies.Length; j++)
                {
                    var dx = bodies[j].X - bodies[i].X;
                    var dy = bodies[j].Y - bodies[i].Y;
                    var minDistance = bodies[i].Radius + bodies[j].Radius;
                    var distanceSquared = (dx * dx) + (dy * dy);
                    if (distanceSquared >= minDistance * minDistance)
                        continue;

                    overlapCount++;
                    var (distance, normalX, normalY) = Separation(dx, dy, distanceSquared, i, j);
                    var push = (minDistance - distance) * 0.5f;
                    bodies[i].X -= normalX * push;
                    bodies[i].Y -= normalY * push;
                    bodies[j].X += normalX * push;
                    bodies[j].Y += normalY * push;
                }
            }

            ClampBodies(bodies, pondRadius);
        }

        return overlapCount;
    }

    private static float ValidateBodyRadii(CollisionBody[] bodies, float pondRadius)
    {
        var maxRadius = 0f;
        foreach (var body in bodies)
        {
            if (body.Radius <= 0f)
                throw new ArgumentOutOfRangeException(nameof(bodies), "Every body radius must be positive.");
            if (pondRadius <= body.Radius)
                throw new ArgumentOutOfRangeException(nameof(pondRadius), "Pond radius must exceed every body radius.");
            maxRadius = MathF.Max(maxRadius, body.Radius);
        }

        return maxRadius;
    }

    private static void ClampBodies(Span<CollisionBody> bodies, float pondRadius)
    {
        for (var i = 0; i < bodies.Length; i++)
        {
            var limit = pondRadius - bodies[i].Radius;
            var limitSquared = limit * limit;
            var distanceSquared = (bodies[i].X * bodies[i].X) + (bodies[i].Y * bodies[i].Y);
            if (distanceSquared <= limitSquared)
                continue;
            var distance = MathF.Sqrt(distanceSquared);
            if (distance <= 0.0001f)
            {
                bodies[i].X = 0f;
                bodies[i].Y = 0f;
                continue;
            }

            var scale = limit / distance;
            bodies[i].X *= scale;
            bodies[i].Y *= scale;
        }
    }

    private static (float Distance, float NormalX, float NormalY) Separation(
        float dx,
        float dy,
        float distanceSquared,
        int leftIndex,
        int rightIndex)
    {
        if (distanceSquared < MinimumDistanceSquared)
        {
            var angle = (leftIndex * 0.7548777f) + (rightIndex * 0.5698403f);
            return (0f, MathF.Cos(angle * MathF.Tau), MathF.Sin(angle * MathF.Tau));
        }

        var distance = MathF.Sqrt(distanceSquared);
        return (distance, dx / distance, dy / distance);
    }

}
