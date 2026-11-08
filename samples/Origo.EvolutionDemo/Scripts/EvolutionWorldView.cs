using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using Origo.Core.Abstractions.Entity;
using Origo.EvolutionDemo.Logic;

namespace Origo.EvolutionDemo;

/// <summary>
///     Draws the pond, creatures, and food directly from SND entity data. The
///     simulation owns positions; per-entity Godot scenes stay empty placeholders
///     so 100+ creatures do not require a second animation system.
/// </summary>
public partial class EvolutionWorldView : Node2D
{
    public static readonly Vector2 PondCenter = new(960f, 540f);
    private const float DefaultZoomFactor = 3.6f;
    private readonly DemoEntry _entry;
    private Vector2 _viewportSize;

    /// <summary>Pixels per world unit at which the whole pond exactly fits.</summary>
    public float FitViewScale { get; private set; } = 1f;

    /// <summary>Current camera scale in screen pixels per pond world unit.</summary>
    public float ViewScale { get; private set; } = 1f;

    /// <summary>World point displayed at the viewport center.</summary>
    public Vector2 Focus { get; private set; }

    /// <summary>Currently selected creature name; drawn with a highlight.</summary>
    public string? SelectedName { get; set; }

    private int _lastCreatureCount;
    private readonly Dictionary<string, CreatureAppearance> _appearanceCache = new(StringComparer.Ordinal);

    public EvolutionWorldView(DemoEntry entry)
    {
        _entry = entry;
    }

    public override void _Ready()
    {
        ZIndex = -10;
        UpdateViewportMetrics();
        Focus = Vector2.Zero;
        ViewScale = Mathf.Min(FitViewScale * DefaultZoomFactor, MaxViewScale);
        ApplyTransform();
        SetProcess(true);
    }

    /// <summary>Maps a viewport-space point to pond-local world coordinates.</summary>
    public Vector2 ScreenToWorld(Vector2 screen) =>
        Focus + ((screen - PondCenter) / ViewScale);

    /// <summary>Maps a pond-local world point to viewport coordinates.</summary>
    public Vector2 WorldToScreen(Vector2 world) =>
        PondCenter + ((world - Focus) * ViewScale);

    /// <summary>Resets the camera to the whole pond, centered.</summary>
    public void ResetView()
    {
        Focus = Vector2.Zero;
        ViewScale = FitViewScale;
        ApplyTransform();
    }

    /// <summary>Restores the default local view used when a pond starts.</summary>
    public void RestoreDefaultView()
    {
        Focus = Vector2.Zero;
        ViewScale = Mathf.Min(FitViewScale * DefaultZoomFactor, MaxViewScale);
        ApplyTransform();
    }

    /// <summary>Zooms around a viewport-space point.</summary>
    public void ZoomAt(Vector2 screenPoint, float factor)
    {
        var world = ScreenToWorld(screenPoint);
        var next = Mathf.Clamp(ViewScale * factor, FitViewScale, MaxViewScale);
        if (Mathf.Abs(next - ViewScale) <= 0.0001f)
            return;
        ViewScale = next;
        Focus = world - ((screenPoint - PondCenter) / ViewScale);
        ClampFocus();
        ApplyTransform();
    }

    /// <summary>Pans by a viewport-space delta.</summary>
    public void Pan(Vector2 screenDelta)
    {
        Focus -= screenDelta / ViewScale;
        ClampFocus();
        ApplyTransform();
    }

    /// <summary>Largest practical zoom: enough to inspect individual bodies and patterns.</summary>
    public float MaxViewScale => Mathf.Max(1.5f, FitViewScale * 30f);

    /// <summary>Re-reads world settings after a pond is created or loaded.</summary>
    public void RefreshWorldBounds()
    {
        UpdateViewportMetrics();
        RestoreDefaultView();
    }

    private void UpdateViewportMetrics()
    {
        _viewportSize = GetViewport().GetVisibleRect().Size;
        FitViewScale = Mathf.Max(0.02f, Mathf.Min(
            (_viewportSize.X - 180f) / (Tuning.PondRadius * 2f),
            (_viewportSize.Y - 160f) / (Tuning.PondRadius * 2f)));
    }

    private void ClampFocus()
    {
        var half = _viewportSize / (2f * ViewScale);
        var maxX = Mathf.Max(0f, Tuning.PondRadius - half.X);
        var maxY = Mathf.Max(0f, Tuning.PondRadius - half.Y);
        Focus = new Vector2(
            Mathf.Clamp(Focus.X, -maxX, maxX),
            Mathf.Clamp(Focus.Y, -maxY, maxY));
    }

    private void ApplyTransform()
    {
        Position = PondCenter - (Focus * ViewScale);
        Scale = new Vector2(ViewScale, ViewScale);
        QueueRedraw();
    }

    private static bool IsVisible(Vector2 worldPosition, float margin, Vector2 viewMin, Vector2 viewMax) =>
        worldPosition.X >= viewMin.X - margin
        && worldPosition.X <= viewMax.X + margin
        && worldPosition.Y >= viewMin.Y - margin
        && worldPosition.Y <= viewMax.Y + margin;

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        DrawCircle(Vector2.Zero, Tuning.PondRadius + 12f, new Color(0.02f, 0.055f, 0.09f, 1f));
        DrawCircle(Vector2.Zero, Tuning.PondRadius, new Color(0.035f, 0.09f, 0.125f, 1f));
        DrawArc(Vector2.Zero, Tuning.PondRadius, 0f, Mathf.Tau, 96, new Color(0.16f, 0.5f, 0.55f, 0.8f), 3f, true);
        DrawArc(Vector2.Zero, Tuning.PondRadius * 0.66f, 0f, Mathf.Tau, 80, new Color(0.1f, 0.25f, 0.3f, 0.4f), 1f, true);

        var session = _entry.Runtime.SessionManager.ForegroundSession;
        if (session is null)
            return;

        var controller = session.FindByName(DemoEntry.ControllerName);
        var pondTime = controller is null ? 0f : ReadFloat(controller, DemoKeys.Time, 0f);
        var interpolation = 1f;
        if (controller is not null
            && string.Equals(
                ReadString(controller, DemoKeys.Phase, DemoKeys.PhaseMenu),
                DemoKeys.PhaseRunning,
                StringComparison.Ordinal))
        {
            interpolation = Mathf.Clamp(
                ReadFloat(controller, DemoKeys.Accumulator, 0f) / Tuning.SimulationTickSeconds,
                0f, 1f);
        }

        var halfView = _viewportSize / (2f * ViewScale);
        var viewMin = Focus - halfView;
        var viewMax = Focus + halfView;
        var highDensity = _lastCreatureCount > 900;
        var creatureCount = 0;
        foreach (var entity in session.GetEntities())
        {
            var (isKind, kind) = entity.TryGetData<string>(DemoKeys.Kind);
            if (!isKind)
                continue;

            if (string.Equals(kind, DemoKeys.FoodSourceKind, StringComparison.Ordinal))
            {
                var position = ReadPosition(entity);
                if (IsVisible(position, Tuning.FoodPatchRadius, viewMin, viewMax))
                {
                    // No center icon: it reads as a creature at normal viewing
                    // scales. The source is represented only by a faint wash
                    // over its production radius, skipped when zoomed so far in
                    // that the wash would tint the whole viewport.
                    var washRadiusOnScreen = Tuning.FoodPatchRadius * ViewScale;
                    if (washRadiusOnScreen <= _viewportSize.Y * 0.75f)
                        DrawCircle(position, Tuning.FoodPatchRadius,
                            new Color(0.24f, 0.86f, 0.58f, 0.035f));
                }

                continue;
            }

            if (string.Equals(kind, DemoKeys.FoodKind, StringComparison.Ordinal))
            {
                var position = ReadPosition(entity);
                if (IsVisible(position, 8f, viewMin, viewMax))
                    DrawCircle(position, 3.5f, new Color(0.98f, 0.78f, 0.3f, 0.95f));
                continue;
            }

            if (string.Equals(kind, DemoKeys.CorpseKind, StringComparison.Ordinal))
            {
                var position = ReadPosition(entity);
                if (IsVisible(position, 14f, viewMin, viewMax))
                {
                    var remaining = ReadInt(entity, DemoKeys.CorpseFoodRemaining, 1);
                    var total = Math.Max(1, ReadInt(entity, DemoKeys.CorpseFoodTotal, 1));
                    var fade = Mathf.Clamp(remaining / (float)total, 0f, 1f);
                    DrawCircle(position, 5f, new Color(0.32f, 0.12f, 0.09f, 0.55f + (0.3f * fade)));
                    DrawArc(position, 6.5f + (2f * fade), 0f, Mathf.Tau, 14,
                        new Color(0.78f, 0.28f, 0.18f, 0.55f), 1.2f / ViewScale, true);
                }

                continue;
            }

            if (!string.Equals(kind, DemoKeys.CreatureKind, StringComparison.Ordinal))
                continue;
            creatureCount++;

            var currentPosition = ReadPosition(entity);
            var bodyRadius = ReadFloat(entity, DemoKeys.BodyRadius, Tuning.BodyRadius);
            if (!IsVisible(currentPosition, bodyRadius + 32f, viewMin, viewMax))
                continue;
            var currentHeading = ReadFloat(entity, DemoKeys.Heading, 0f);
            var previousPosition = new Vector2(
                ReadFloat(entity, DemoKeys.PrevX, currentPosition.X),
                ReadFloat(entity, DemoKeys.PrevY, currentPosition.Y));
            var previousHeading = ReadFloat(entity, DemoKeys.PrevHeading, currentHeading);
            var creaturePosition = previousPosition.Lerp(currentPosition, interpolation);
            var heading = LerpAngle(previousHeading, currentHeading, interpolation);
            var bodyGenome = ReadString(entity, DemoKeys.BodyGenome, string.Empty);
            if (!_appearanceCache.TryGetValue(bodyGenome, out var look))
            {
                if (_appearanceCache.Count >= 8192)
                    _appearanceCache.Clear();
                look = GenomeAppearance.Derive(bodyGenome);
                _appearanceCache[bodyGenome] = look;
            }
            var body = ToColor(look.Body);
            var accent = ToColor(look.Accent);
            var energy = ReadFloat(entity, DemoKeys.Energy, 0f);
            var maxEnergy = MathF.Max(1f, ReadFloat(entity, DemoKeys.MaxEnergy, 200f));
            var screenPosition = creaturePosition;
            var forward = new Vector2(Mathf.Cos(heading), Mathf.Sin(heading));

            var detailed = (ViewScale * bodyRadius) >= 4f;
            DrawCircle(screenPosition, bodyRadius, new Color(body, 0.95f));
            if (detailed)
                DrawPattern(screenPosition, look, accent, heading, bodyRadius);

            // Judgment-radius rim. A screen-constant white line keeps the body
            // legible against food, source washes, and similarly colored bodies;
            // at extreme zoom-out only this minimum-width ring is inflated by
            // sub-pixel amounts, never the filled body itself.
            var rimRadius = MathF.Max(bodyRadius, 1.4f / ViewScale);
            DrawArc(screenPosition, rimRadius, 0f, Mathf.Tau, 20,
                new Color(1f, 1f, 1f, 0.66f), 1.15f / ViewScale, true);

            var spikeFlash = ReadFloat(entity, DemoKeys.SpikeFlash, -1f);
            if (spikeFlash >= 0f && pondTime - spikeFlash < 0.55f)
            {
                var pulse = Mathf.Clamp(1f - ((pondTime - spikeFlash) / 0.55f), 0f, 1f);
                var spikeColor = new Color(1f, 0.32f, 0.22f, 0.35f + (0.55f * pulse));
                var fan = ReadString(entity, DemoKeys.SpikeFan, string.Empty);
                if (fan.Length > 0)
                {
                    foreach (var part in fan.Split(';', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var pair = part.Split(',', StringSplitOptions.RemoveEmptyEntries);
                        if (pair.Length != 2
                            || !float.TryParse(pair[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var endX)
                            || !float.TryParse(pair[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var endY))
                            continue;
                        var end = new Vector2(endX, endY);
                        DrawLine(screenPosition, end, spikeColor, (1.2f + (1.2f * pulse)) / ViewScale);
                        DrawCircle(end, 2.5f + (2.5f * pulse), spikeColor);
                    }
                }
                else
                {
                    var end = new Vector2(
                        ReadFloat(entity, DemoKeys.SpikeEndX, currentPosition.X),
                        ReadFloat(entity, DemoKeys.SpikeEndY, currentPosition.Y));
                    DrawLine(screenPosition, end, spikeColor, (1.5f + (1.5f * pulse)) / ViewScale);
                    DrawCircle(end, 3f + (3f * pulse), spikeColor);
                }
            }
            var isSelected = SelectedName is not null
                && string.Equals(SelectedName, entity.Name, StringComparison.Ordinal);
            if (!highDensity || isSelected)
            {
                DrawLine(screenPosition, screenPosition + (forward * (bodyRadius * 2.6f)),
                    new Color(0.95f, 0.98f, 1f, 0.9f), 2f / ViewScale);
                var energyRatio = Mathf.Clamp(energy / maxEnergy, 0f, 1f);
                DrawArc(screenPosition, bodyRadius + 4f, -Mathf.Pi / 2f,
                    -Mathf.Pi / 2f + (Mathf.Tau * energyRatio), 24, accent, 2f, true);
            }

            if (isSelected)
                DrawArc(screenPosition, bodyRadius + 9f, 0f, Mathf.Tau, 32,
                    new Color(1f, 1f, 1f, 0.95f), 2.5f / ViewScale, true);
        }

        _lastCreatureCount = creatureCount;
    }

    private static Color ToColor(AppearanceColor color) => new(color.R, color.G, color.B);

    private void DrawPattern(
        Vector2 center,
        CreatureAppearance look,
        Color accent,
        float heading,
        float radius)
    {
        var angle = look.PatternAngle + heading;
        var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        var perpendicular = new Vector2(-direction.Y, direction.X);
        var color = new Color(accent, 0.82f);

        switch (look.Pattern)
        {
            case CreaturePattern.Ring:
                DrawArc(center, radius * 0.52f, 0f, Mathf.Tau, 18, color, 1.3f, true);
                break;
            case CreaturePattern.DoubleRing:
                DrawArc(center, radius * 0.34f, 0f, Mathf.Tau, 16, color, 1.1f, true);
                DrawArc(center, radius * 0.64f, 0f, Mathf.Tau, 20, color, 1.1f, true);
                break;
            case CreaturePattern.Stripes:
                for (var i = -2; i <= 2; i++)
                {
                    var offset = i * radius * 0.3f;
                    var half = MathF.Sqrt(MathF.Max(0f, (radius * radius) - (offset * offset)));
                    var start = center + (perpendicular * offset) - (direction * half);
                    var end = center + (perpendicular * offset) + (direction * half);
                    DrawLine(start, end, color, 1.2f);
                }

                break;
            case CreaturePattern.Spots:
                for (var i = 0; i < look.PatternFrequency; i++)
                {
                    var spotAngle = angle + (i * Mathf.Tau / look.PatternFrequency);
                    var spot = center + (new Vector2(Mathf.Cos(spotAngle), Mathf.Sin(spotAngle)) * radius * 0.44f);
                    DrawCircle(spot, radius * 0.13f, color);
                }

                break;
            case CreaturePattern.Rays:
                for (var i = 0; i < look.PatternFrequency; i++)
                {
                    var rayAngle = angle + (i * Mathf.Tau / look.PatternFrequency);
                    var ray = new Vector2(Mathf.Cos(rayAngle), Mathf.Sin(rayAngle));
                    DrawLine(center + (ray * radius * 0.18f), center + (ray * radius * 0.78f), color, 1.1f);
                }

                break;
            case CreaturePattern.Spiral:
                var points = new Vector2[16];
                for (var i = 0; i < points.Length; i++)
                {
                    var t = i / (float)(points.Length - 1);
                    var spiralAngle = angle + (t * Mathf.Tau * 1.6f);
                    points[i] = center + (new Vector2(Mathf.Cos(spiralAngle), Mathf.Sin(spiralAngle))
                        * radius * (0.16f + (t * 0.54f)));
                }

                DrawPolyline(points, color, 1.1f, true);
                break;
            case CreaturePattern.Chevron:
                var tip = center + (direction * radius * 0.55f);
                DrawPolyline(
                    [
                        tip,
                        center - (perpendicular * radius * 0.38f) - (direction * radius * 0.22f),
                        tip - (direction * radius * 0.16f),
                        center + (perpendicular * radius * 0.38f) - (direction * radius * 0.22f),
                        tip,
                    ],
                    color,
                    1.2f,
                    true);
                break;
            case CreaturePattern.Segments:
                for (var i = 0; i < 4; i++)
                {
                    var startAngle = angle + (i * Mathf.Tau / 4f);
                    DrawArc(center, radius * 0.68f, startAngle, startAngle + 0.72f, 8, color, 1.8f, true);
                }

                break;
            case CreaturePattern.Cross:
                DrawLine(center - (direction * radius * 0.58f), center + (direction * radius * 0.58f), color, 1.2f);
                DrawLine(center - (perpendicular * radius * 0.58f), center + (perpendicular * radius * 0.58f), color, 1.2f);
                break;
            case CreaturePattern.Halo:
                DrawArc(center, radius * 0.82f, 0f, Mathf.Tau, 20, color, 1.5f, true);
                break;
            case CreaturePattern.TriDots:
                for (var i = 0; i < 3; i++)
                {
                    var dotAngle = angle + (i * Mathf.Tau / 3f);
                    var dot = center + (new Vector2(Mathf.Cos(dotAngle), Mathf.Sin(dotAngle)) * radius * 0.42f);
                    DrawCircle(dot, radius * 0.15f, color);
                }

                break;
            case CreaturePattern.Plain:
            default:
                DrawCircle(center, radius * 0.24f, new Color(accent, 0.45f));
                break;
        }
    }

    private static Vector2 ReadPosition(ISndEntity entity) => new(
        ReadFloat(entity, DemoKeys.X, 0f),
        ReadFloat(entity, DemoKeys.Y, 0f));

    private static float LerpAngle(float from, float to, float weight)
    {
        var delta = Mathf.Wrap(to - from, -Mathf.Pi, Mathf.Pi);
        return from + (delta * weight);
    }

    private static string ReadString(ISndEntity entity, string key, string fallback)
    {
        var (found, value) = entity.TryGetData<string>(key);
        return found && value is not null ? value : fallback;
    }

    private static int ReadInt(ISndEntity entity, string key, int fallback)
    {
        var (found, value) = entity.TryGetData<int>(key);
        return found ? value : fallback;
    }

    private static float ReadFloat(ISndEntity entity, string key, float fallback)
    {
        var (found, value) = entity.TryGetData<float>(key);
        return found ? value : fallback;
    }

}
