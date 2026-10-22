using System;
using System.Collections.Generic;
using System.IO;
using Origo.Core.DataSource;
using Origo.Core.Save.Storage;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     Regression coverage for strict-read failure ownership: partial
///     <see cref="DataSourceNode" /> trees must be disposed when a later read
///     or validation step fails.
/// </summary>
public class SavePayloadReaderDisposalTests
{
    [Fact]
    public void ReadFromCurrent_MissingProgressStateMachines_DisposesProgressNode()
    {
        var fs = new TestMemoryFileSystem();
        fs.SeedFile("root/current/progress.json", "{}");
        var gateway = new TrackingIoGateway(TestFactory.CreateIoGateway(fs));
        var handle = new SaveFileHandle(
            TestFactory.CreateFileMetaAccess(fs), gateway, TestFactory.CreatePathResolver(fs), "root");

        Assert.Throws<InvalidOperationException>(
            () => SavePayloadReader.ReadFromCurrent(handle, "slot", "default"));

        Assert.True(gateway.Nodes.TryGetValue("root/current/progress.json", out var progress));
        Assert.Throws<ObjectDisposedException>(() => _ = progress!.Kind);
    }

    [Fact]
    public void ReadFromCurrent_FutureFormatVersion_DisposesProgressTrees()
    {
        var fs = new TestMemoryFileSystem();
        fs.SeedFile("root/current/progress.json", "{}");
        fs.SeedFile("root/current/progress_state_machines.json", """{"machines":[]}""");
        fs.SeedFile("root/current/meta.map", "origo.format_version: 99");
        var gateway = new TrackingIoGateway(TestFactory.CreateIoGateway(fs));
        var handle = new SaveFileHandle(
            TestFactory.CreateFileMetaAccess(fs), gateway, TestFactory.CreatePathResolver(fs), "root");

        Assert.Throws<InvalidOperationException>(
            () => SavePayloadReader.ReadFromCurrent(handle, "slot", "default"));

        Assert.True(gateway.Nodes.TryGetValue("root/current/progress.json", out var progress));
        Assert.True(gateway.Nodes.TryGetValue(
            "root/current/progress_state_machines.json", out var stateMachines));
        Assert.Throws<ObjectDisposedException>(() => _ = progress!.Kind);
        Assert.Throws<ObjectDisposedException>(() => _ = stateMachines!.Kind);
    }

    [Fact]
    public void ReadFromCurrent_CorruptLevelStateMachines_DisposesLevelTrees()
    {
        var fs = new TestMemoryFileSystem();
        fs.SeedFile("root/current/progress.json", "{}");
        fs.SeedFile("root/current/progress_state_machines.json", """{"machines":[]}""");
        fs.SeedFile("root/current/level_default/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_default/session.json", "{}");
        fs.SeedFile("root/current/level_default/session_state_machines.json", "{}");
        var gateway = new TrackingIoGateway(
            TestFactory.CreateIoGateway(fs),
            "root/current/level_default/session_state_machines.json");
        var handle = new SaveFileHandle(
            TestFactory.CreateFileMetaAccess(fs), gateway, TestFactory.CreatePathResolver(fs), "root");

        Assert.Throws<InvalidDataException>(
            () => SavePayloadReader.ReadFromCurrent(handle, "slot", "default"));

        Assert.True(gateway.Nodes.TryGetValue("root/current/level_default/snd_scene.json", out var scene));
        Assert.True(gateway.Nodes.TryGetValue("root/current/level_default/session.json", out var session));
        Assert.Throws<ObjectDisposedException>(() => _ = scene!.Kind);
        Assert.Throws<ObjectDisposedException>(() => _ = session!.Kind);
    }

    private sealed class TrackingIoGateway(
        IDataSourceIoGateway inner,
        string? failingPath = null) : IDataSourceIoGateway
    {
        public Dictionary<string, DataSourceNode> Nodes { get; } = new(StringComparer.Ordinal);

        public DataSourceNode ReadTree(string filePath)
        {
            if (string.Equals(filePath, failingPath, StringComparison.Ordinal))
                throw new InvalidDataException($"Intentional read failure for '{filePath}'.");
            var node = inner.ReadTree(filePath);
            Nodes[filePath] = node;
            return node;
        }

        public void WriteTree(string filePath, DataSourceNode node, bool overwrite = true) =>
            inner.WriteTree(filePath, node, overwrite);
    }
}
