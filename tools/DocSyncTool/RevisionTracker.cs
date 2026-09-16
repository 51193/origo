using System;
using System.Collections.Generic;
using System.Linq;

namespace DocSyncTool;

internal sealed record DocContentEvent(string? CommitSha, long Timestamp);

/// <summary>
///     Computes the git-derived <c>docsync-revision</c> for a file. The
///     revision is advanced by real content changes only (the DocSync metadata
///     block is excluded from the hash), which makes generation idempotent:
///     an auto-commit that only rewrites headers and
///     <c>.sync-status.json</c> is invisible to the next planning run.
/// </summary>
internal sealed class RevisionTracker(GitRepository git)
{
    private readonly GitRepository _git = git;

    /// <summary>
    ///     Returns content-change events strictly newer than the commit whose
    ///     normalized content hash equals <paramref name="oldHash" />.
    ///     Uncommitted working-tree changes are appended as one synthetic
    ///     event. If <paramref name="oldHash" /> cannot be located in history
    ///     (for example after a local generate that has not been committed
    ///     yet), a differing working tree is planned as exactly one pending
    ///     change; equal content still yields no event, keeping the run
    ///     idempotent.
    /// </summary>
    public List<DocContentEvent> GetContentEvents(
        string repoRelativePath,
        string currentContent,
        string oldHash)
    {
        var history = _git.GetPathHistory(repoRelativePath);
        if (history.Count == 0)
            return [];

        var blobSpecs = new List<(string Sha, string Path)>();
        foreach (var entry in history)
        {
            if (entry.AfterPath is not null)
                blobSpecs.Add((entry.Sha, entry.AfterPath));
        }

        var blobs = _git.ReadBlobTexts(blobSpecs);

        var blobIndex = 0;
        var hashes = new Dictionary<int, string?>();
        for (var i = 0; i < history.Count; i++)
        {
            if (history[i].AfterPath is null)
            {
                hashes[i] = null;
                continue;
            }

            var blob = blobs[blobIndex];
            blobIndex++;
            hashes[i] = blob is null ? null : ContentHash.Compute(blob);
        }

        var anchorIndex = -1;
        for (var i = 0; i < history.Count; i++)
        {
            if (hashes[i] is not null
                && string.Equals(hashes[i], oldHash, StringComparison.Ordinal))
            {
                anchorIndex = i;
                break;
            }
        }

        if (anchorIndex < 0)
        {
            // The old snapshot content is not committed yet (local generate
            // before commit, followed by another edit and generate). Count
            // the latest uncommitted delta once; if the content is unchanged
            // the result is empty and the run stays idempotent.
            var pendingHash = ContentHash.Compute(currentContent);
            return string.Equals(pendingHash, oldHash, StringComparison.Ordinal)
                ? []
                : [new DocContentEvent(null, long.MaxValue)];
        }

        var events = new List<DocContentEvent>();
        var previousHash = oldHash;

        // history is newest-first. Walk from just below the anchor back to
        // index 0 so the returned events are oldest-first.
        for (var i = anchorIndex - 1; i >= 0; i--)
        {
            var hash = hashes[i];
            if (hash is null)
                continue;

            if (!string.Equals(hash, previousHash, StringComparison.Ordinal))
            {
                events.Add(new DocContentEvent(history[i].Sha, history[i].Timestamp));
                previousHash = hash;
            }
        }

        var currentHash = ContentHash.Compute(currentContent);
        if (!string.Equals(currentHash, previousHash, StringComparison.Ordinal))
            events.Add(new DocContentEvent(null, long.MaxValue));

        return events;
    }
}
