using System;
using System.Collections.Generic;
using Godot;

namespace Origo.EvolutionDemo;

/// <summary>Small self-drawn line chart for the diversity board.</summary>
public partial class DiversitySparkline : Control
{
    private const int Capacity = 120;
    private readonly List<float> _values = [];

    /// <summary>Appends a normalized 0..1 sample.</summary>
    public void Push(float value)
    {
        _values.Add(Mathf.Clamp(value, 0f, 1f));
        if (_values.Count > Capacity)
            _values.RemoveAt(0);
        QueueRedraw();
    }

    /// <summary>Clears the history when a new pond is shown.</summary>
    public void Clear()
    {
        _values.Clear();
        QueueRedraw();
    }

    /// <inheritdoc />
    public override void _Draw()
    {
        var size = Size;
        if (size.X <= 1f || size.Y <= 1f)
            return;

        DrawRect(new Rect2(Vector2.Zero, size), new Color(0.02f, 0.045f, 0.06f, 0.9f));
        for (var i = 1; i < 4; i++)
        {
            var y = size.Y * i / 4f;
            DrawLine(new Vector2(0f, y), new Vector2(size.X, y), new Color(0.1f, 0.2f, 0.24f, 0.5f), 1f);
        }

        if (_values.Count < 2)
        {
            DrawLine(new Vector2(0f, size.Y * 0.5f), new Vector2(size.X, size.Y * 0.5f),
                new Color(0.3f, 0.45f, 0.5f, 0.5f), 1f);
            return;
        }

        var points = new Vector2[_values.Count];
        for (var i = 0; i < _values.Count; i++)
        {
            var x = _values.Count == 1 ? 0f : size.X * i / (_values.Count - 1f);
            var y = size.Y - (_values[i] * size.Y);
            points[i] = new Vector2(x, y);
        }

        DrawPolyline(points, new Color(0.36f, 0.9f, 0.76f, 0.95f), 2f, true);
        var last = points[^1];
        DrawCircle(last, 3.5f, new Color(0.8f, 1f, 0.9f, 1f));
    }
}
