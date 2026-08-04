using System;
using System.IO;
using System.Linq;

namespace DocSyncTool.Tests;

/// <summary>
/// Isolated temporary repo scaffold for DocSyncTool tests. Creates a repo
/// root containing the marker files FindRepoRoot requires (AGENTS.md, docs/)
/// plus a docsync-config.json under tools/DocSyncTool, so Config.Load works
/// against it exactly like the real repository layout.
/// </summary>
internal sealed class TestRepo : IDisposable
{
    public string Root { get; }

    private TestRepo(string root)
    {
        Root = root;
    }

    public static TestRepo Create()
    {
        return Create(["zh", "en"]);
    }

    public static TestRepo Create(string[] languages)
    {
        var languagesJson = string.Join(", ", languages.Select(l => $"\"{l}\""));
        var root = Path.Combine(Path.GetTempPath(), "docsync-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "tools", "DocSyncTool"));
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        File.WriteAllText(Path.Combine(root, "AGENTS.md"), "");
        File.WriteAllText(
            Path.Combine(root, "tools", "DocSyncTool", "docsync-config.json"),
            $$"""{"languages": [{{languagesJson}}], "docs_root": "docs"}""");
        return new TestRepo(root);
    }

    public static string Header(string pairId, int revision = 1)
    {
        return $"<!-- docsync-pair: {pairId} -->\n" +
               $"<!-- docsync-revision: {revision} -->\n" +
               "<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->\n";
    }

    public void Write(string relativePath, string content)
    {
        var full = Full(relativePath);
        var dir = Path.GetDirectoryName(full);
        if (dir is not null)
            Directory.CreateDirectory(dir);
        File.WriteAllText(full, content);
    }

    public string Read(string relativePath)
    {
        return File.ReadAllText(Full(relativePath));
    }

    public bool Exists(string relativePath)
    {
        return File.Exists(Full(relativePath));
    }

    public string Full(string relativePath)
    {
        return Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    public Config LoadConfig()
    {
        return Config.Load(Root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, true);
        }
        catch
        {
            // best-effort cleanup; failure must not mask the test result
        }
    }
}
