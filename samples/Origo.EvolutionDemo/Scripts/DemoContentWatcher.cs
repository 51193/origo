using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Godot;
using Origo.EvolutionDemo.Logic.Gameplay;

namespace Origo.EvolutionDemo;

/// <summary>
///     Polls the content directory and atomically applies JSON content packs.
///     Content is reloaded only; C# strategies/modules remain startup-loaded.
/// </summary>
public partial class DemoContentWatcher : Node
{
    private readonly string _directory;
    private double _elapsed;
    private string _lastHash = string.Empty;
    private string _failedHash = string.Empty;
    private bool _loaded;

    public DemoContentWatcher()
    {
        _directory = ResolveDirectory();
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        if (_elapsed < 0.5d)
            return;
        _elapsed = 0d;
        try
        {
            ReloadIfChanged();
        }
        catch (Exception ex)
        {
            GD.PushWarning($"CONTENT_RELOAD_FAILED {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void ReloadIfChanged()
    {
        if (!Directory.Exists(_directory))
            return;
        var files = Directory.GetFiles(_directory, "*.json")
            .OrderBy(static file => file, StringComparer.Ordinal)
            .ToArray();
        var texts = new List<string>(files.Length);
        var hashBuilder = new StringBuilder();
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            texts.Add(text);
            hashBuilder.Append(file).Append('|').Append(text).Append('\n');
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashBuilder.ToString())));
        if ((_loaded && string.Equals(hash, _lastHash, StringComparison.Ordinal))
            || string.Equals(hash, _failedHash, StringComparison.Ordinal))
            return;
        try
        {
            var runtime = GameplayRuntime.EnsureCurrent();
            runtime.ApplyExternalPacks(texts);
            DemoMechanicHandlers.Validate(runtime);
        }
        catch
        {
            _failedHash = hash;
            throw;
        }

        _lastHash = hash;
        _failedHash = string.Empty;
        _loaded = true;
        GD.Print($"CONTENT_RELOAD_OK files={files.Length} hash={hash[..12]}");
    }

    private static string ResolveDirectory()
    {
        var overrideDir = OS.GetEnvironment("ORIGO_DEMO_CONTENT_DIR");
        if (!string.IsNullOrWhiteSpace(overrideDir))
            return overrideDir;
        return ProjectSettings.GlobalizePath("res://Content");
    }
}
