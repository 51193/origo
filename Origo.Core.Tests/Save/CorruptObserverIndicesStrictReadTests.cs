using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Logging;
using Origo.Core.DataSource;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Save;
using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     Regression tests: the strict-read contract applies to observer binding
///     metadata too. A save whose <c>observer_indices</c> array contains a
///     non-object element is corrupt (the writer only ever emits object
///     elements) and must fail the load instead of silently dropping the
///     damaged binding.
/// </summary>
public class CorruptObserverIndicesStrictReadTests
{
    [Fact]
    public void LoadFromPayload_WhenObserverIndicesEntryIsNotAnObject_Throws()
    {
        var (_, progressRun) = CreateContext();

        var payload = new SaveGamePayload
        {
            SaveId = "005",
            ActiveLevelId = "target",
            ProgressNode = TestFactory.NodeFromJson(
                """{"origo.session_topology":{"type":"String","data":"__foreground__=target=false"}}"""),
            ProgressStateMachinesNode = TestFactory.NodeFromJson("{\"machines\":[]}"),
            Levels = new Dictionary<string, LevelPayload>
            {
                ["target"] = new()
                {
                    LevelId = "target",
                    SndSceneNode = TestFactory.NodeFromJson(
                        """
                        [
                          {
                            "name": "OBS",
                            "node": { "pairs": {} },
                            "strategy": {
                              "lifecycle_indices": [],
                              "active_indices": [],
                              "observer_indices": [ "corrupt_element" ]
                            },
                            "data": { "pairs": {} }
                          }
                        ]
                        """),
                    SessionNode = TestFactory.NodeFromJson("{}"),
                    SessionStateMachinesNode = TestFactory.NodeFromJson("{\"machines\":[]}")
                }
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() => progressRun.LoadFromPayload(payload));
        Assert.Contains("observer_indices", ex.Message, StringComparison.Ordinal);
    }

    private static (SndContext ctx, ProgressRun progressRun) CreateContext()
    {
        var logger = new TestLogger();
        var fs = new TestMemoryFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost(), new TypeStringMapping(), new Blackboard.Blackboard(), dataSourceIo);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver,
            "root", "initial", "entry.json"));
        var progressRun = TestFactory.CreateProgressRun(
            "005", logger, metaAccess, pathResolver, "root", runtime, ctx, sharedDataSourceIo: dataSourceIo);
        return (ctx, progressRun);
    }
}
