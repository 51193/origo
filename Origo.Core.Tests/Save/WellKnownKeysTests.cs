using Origo.Core.Save;
using Xunit;

namespace Origo.Core.Tests;

// ── KeyValueFileParser ─────────────────────────────────────────────────

public class WellKnownKeysTests
{
    [Fact]
    public void WellKnownKeys_ActiveSaveId_HasExpectedValue() =>
        Assert.Equal("origo.active_save_id", WellKnownKeys.ActiveSaveId);

    [Fact]
    public void WellKnownKeys_SessionTopology_HasExpectedValue() =>
        Assert.Equal("origo.session_topology", WellKnownKeys.SessionTopology);
}

// ── TestFileSystem coverage ────────────────────────────────────────────
