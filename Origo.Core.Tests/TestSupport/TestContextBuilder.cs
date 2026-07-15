using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Scene;
using Origo.Core.DataSource;
using Origo.Core.Serialization;
using Origo.Core.Snd;

namespace Origo.Core.Tests;

/// <summary>
///     Fluent builder for constructing <see cref="SndContext" /> instances
///     in integration tests. Provides sensible defaults and optional overrides,
///     replacing the repetitive 10-line constructor pattern.
/// </summary>
public sealed class TestContextBuilder
{
    private ILogger _logger = new TestLogger();
    private ISndSceneHost _sceneHost = new TestSndSceneHost();
    private IBlackboard _systemBlackboard = new Blackboard.Blackboard();
    private string _saveRootPath = "root";
    private readonly string _initialSaveRootPath = "res://initial";
    private string _entryConfigPath = "entry.json";

    public TestContextBuilder WithLogger(ILogger logger) { _logger = logger; return this; }

    public TestContextBuilder WithSceneHost(ISndSceneHost sceneHost) { _sceneHost = sceneHost; return this; }

    public TestContextBuilder WithBlackboard(IBlackboard blackboard) { _systemBlackboard = blackboard; return this; }

    public TestContextBuilder WithSaveRootPath(string path) { _saveRootPath = path; return this; }

    public TestContextBuilder WithEntryConfigPath(string path) { _entryConfigPath = path; return this; }

    public SndContext Build()
    {
        var fileSystem = new TestMemoryFileSystem();
        var runtime = TestFactory.CreateRuntime(
            _logger, _sceneHost, new TypeStringMapping(), _systemBlackboard, fileSystem);
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fileSystem);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fileSystem);
        var pathResolver = DataSourceFactory.CreatePathResolver(fileSystem);

        return new SndContext(new SndContextParameters(
            runtime, dataSourceIo, metaAccess, pathResolver,
            _saveRootPath, _initialSaveRootPath, _entryConfigPath));
    }
}
