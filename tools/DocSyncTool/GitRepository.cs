using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace DocSyncTool;

/// <summary>
///     Minimal git process wrapper used by the automatic revision tracker.
///     Every process starts in <see cref="RepoRoot" />, so callers never have
///     to mutate the process-wide current directory.
/// </summary>
internal sealed class GitRepository
{
    private GitRepository(string repoRoot, string headSha)
    {
        RepoRoot = repoRoot;
        HeadSha = headSha;
    }

    public string RepoRoot { get; }
    public string HeadSha { get; }

    public static GitRepository? TryCreate(string repoRoot)
    {
        try
        {
            var head = Run(repoRoot, "rev-parse", "HEAD");
            if (head is null)
                return null;

            return new GitRepository(repoRoot, head.Trim());
        }
        catch
        {
            return null;
        }
    }

    public static string? Run(string repoRoot, params string[] arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

            using var process = Process.Start(startInfo);
            if (process is null)
                return null;

            var standardOutput = process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            if (!process.WaitForExit(30_000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best effort only.
                }

                return null;
            }

            return process.ExitCode == 0 ? standardOutput : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     Returns every commit reachable from HEAD in topological order,
    ///     oldest first. Used to order pair events from different file
    ///     histories when commit timestamps collide.
    /// </summary>
    public Dictionary<string, int> GetCommitTopologyOrder()
    {
        var order = new Dictionary<string, int>(StringComparer.Ordinal);
        var output = Run(RepoRoot, "rev-list", "--topo-order", "--reverse", "HEAD");
        if (string.IsNullOrWhiteSpace(output))
            return order;

        var index = 0;
        foreach (var line in output.Split('\n'))
        {
            var sha = line.Trim();
            if (sha.Length > 0)
                order[sha] = index++;
        }

        return order;
    }

    /// <summary>
    ///     Returns the commits that touched <paramref name="repoRelativePath" />,
    ///     newest first. <c>--follow</c> makes the history survive renames, and
    ///     <c>--name-status</c> supplies the path as it existed at each commit.
    /// </summary>
    public List<PathHistoryEntry> GetPathHistory(string repoRelativePath)
    {
        var output = Run(
            RepoRoot,
            "log",
            "--follow",
            "--format=%H%x09%ct",
            "--name-status",
            "--",
            repoRelativePath);

        var entries = new List<PathHistoryEntry>();
        if (string.IsNullOrWhiteSpace(output))
            return entries;

        PathHistoryEntry? current = null;
        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length == 0)
                continue;

            var tab = line.IndexOf('\t');
            if (tab > 0 && IsFullHexSha(line[..tab])
                && long.TryParse(line[(tab + 1)..], out var timestamp))
            {
                current = new PathHistoryEntry(line[..tab], timestamp, null);
                entries.Add(current);
                continue;
            }

            if (current is null)
                continue;

            var parts = line.Split('\t');
            var status = parts[0].Trim();
            if (status.StartsWith('D'))
            {
                // The file no longer exists at this commit; there is no blob
                // to hash. A later re-add appears as a separate A entry.
                continue;
            }

            if (status.StartsWith('R') || status.StartsWith('C'))
            {
                if (parts.Length >= 3)
                    current.BeforePath = parts[1];
                current.AfterPath = parts[^1];
            }
            else if (status.StartsWith('M') || status.StartsWith('A'))
            {
                current.AfterPath = parts[^1];
            }
        }

        return entries;
    }

    /// <summary>
    ///     Reads many <c>sha:path</c> blobs with a single
    ///     <c>git cat-file --batch</c> process. The returned list is aligned
    ///     with <paramref name="specs" />; missing objects map to null.
    /// </summary>
    public List<string?> ReadBlobTexts(IReadOnlyList<(string Sha, string Path)> specs)
    {
        var result = Enumerable.Repeat<string?>(null, specs.Count).ToList();
        if (specs.Count == 0)
            return result;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = RepoRoot,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("cat-file");
            startInfo.ArgumentList.Add("--batch");

            using var process = Process.Start(startInfo);
            if (process is null)
                return result;

            using (var writer = process.StandardInput)
            {
                foreach (var (sha, path) in specs)
                    writer.WriteLine($"{sha}:{path}");
            }

            using var stdout = process.StandardOutput.BaseStream;
            using var buffer = new MemoryStream();
            stdout.CopyTo(buffer);
            process.StandardError.ReadToEnd();
            if (!process.WaitForExit(30_000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best effort only.
                }

                return result;
            }

            if (process.ExitCode != 0)
                return result;

            ParseBatchOutput(buffer.ToArray(), specs.Count, result);
        }
        catch
        {
            // Fall back to nulls; the revision planner will keep the current
            // revision instead of guessing from partial data.
        }

        return result;
    }

    private static void ParseBatchOutput(byte[] data, int expectedCount, List<string?> result)
    {
        var position = 0;
        for (var i = 0; i < expectedCount; i++)
        {
            var lineEnd = IndexOf(data, (byte)'\n', position);
            if (lineEnd < 0)
                return;

            var header = Encoding.ASCII.GetString(data, position, lineEnd - position);
            position = lineEnd + 1;

            var parts = header.Split(' ');
            if (parts.Length >= 2 && parts[1] == "missing")
                continue;

            if (parts.Length < 3 || parts[1] != "blob"
                || !int.TryParse(parts[2], out var size) || size < 0)
                return;

            if (position + size > data.Length)
                return;

            result[i] = Encoding.UTF8.GetString(data, position, size);
            position += size;

            if (position < data.Length && data[position] == '\n')
                position++;
        }
    }

    private static int IndexOf(byte[] data, byte value, int start)
    {
        for (var i = start; i < data.Length; i++)
        {
            if (data[i] == value)
                return i;
        }

        return -1;
    }

    private static bool IsFullHexSha(string value)
    {
        if (value.Length != 40 && value.Length != 64)
            return false;

        foreach (var c in value)
        {
            if (!Uri.IsHexDigit(c))
                return false;
        }

        return true;
    }
}

internal sealed class PathHistoryEntry(string sha, long timestamp, string? afterPath)
{
    public string Sha { get; } = sha;
    public long Timestamp { get; } = timestamp;
    public string? BeforePath { get; set; }
    public string? AfterPath { get; set; } = afterPath;
}
