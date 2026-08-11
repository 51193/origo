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

    public static TestRepo Create() => Create(["zh", "en"]);

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

    public string Read(string relativePath) => File.ReadAllText(Full(relativePath));

    public bool Exists(string relativePath) => File.Exists(Full(relativePath));

    public string Full(string relativePath) => Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    public Config LoadConfig() => Config.Load(Root);

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

/// <summary>
///     Runs an action with both console streams redirected to silent writers,
///     so the DocSyncTool tools' output during tests (the expected
///     "Validation FAILED" diagnostics of the negative tests, the generate
///     progress lines, the migration banners) does not pollute the
///     test-runner log — where "Validation FAILED" in particular looks like a
///     CI failure. The captured text is returned alongside the result so
///     tests can assert on it or surface it when a test fails. Redirecting
///     the process-global console streams requires callers to run in
///     serialized xUnit collections.
/// </summary>
internal static class ConsoleOutputCapture
{
    public static (T Result, string CapturedOut, string CapturedError) Run<T>(Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var previousOut = Console.Out;
        var previousError = Console.Error;
        using var capturedOut = new StringWriter();
        using var capturedError = new StringWriter();
        try
        {
            Console.SetOut(capturedOut);
            Console.SetError(capturedError);
            return (action(), capturedOut.ToString(), capturedError.ToString());
        }
        finally
        {
            Console.SetOut(previousOut);
            Console.SetError(previousError);
        }
    }

    /// <summary>Void-action overload of <see cref="Run{T}(Func{T})" />.</summary>
    public static (string CapturedOut, string CapturedError) Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var (_, capturedOut, capturedError) = Run(() =>
        {
            action();
            return 0;
        });
        return (capturedOut, capturedError);
    }
}
