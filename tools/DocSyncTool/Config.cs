using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocSyncTool;

internal sealed class Config
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public List<string> Languages { get; set; } = ["zh"];
    public string DocsRoot { get; set; } = "docs";

    [JsonIgnore]
    public string RepoRoot { get; private set; } = "";

    public string DocsFullPath => Path.GetFullPath(Path.Combine(RepoRoot, DocsRoot));

    public static Config Load(string repoRoot)
    {
        var configPath = Path.Combine(repoRoot, "tools", "DocSyncTool", "docsync-config.json");
        if (!File.Exists(configPath))
            throw new InvalidOperationException($"Config file not found: {configPath}");

        var json = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<Config>(json, _jsonOptions)
            ?? throw new InvalidOperationException("Failed to parse docsync-config.json");

        config.RepoRoot = repoRoot;

        foreach (var lang in config.Languages)
        {
            if (string.IsNullOrWhiteSpace(lang) || lang.Contains(' ') || lang.Contains('/') || lang.Contains('\\'))
                throw new InvalidOperationException($"Invalid language code '{lang}' in config. Language codes must be simple identifiers.");
        }

        return config;
    }
}
