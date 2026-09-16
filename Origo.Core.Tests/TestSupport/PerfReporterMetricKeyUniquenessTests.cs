using System;
using System.IO;
using Origo.TestSupport;
using Xunit;

namespace Origo.Core.Tests;

public class PerfReporterMetricKeyUniquenessTests
{
    [Fact]
    public void EmitMetric_DuplicateKey_Throws()
    {
        const string key = "__perf_reporter_duplicate_probe__";
        var reporter = new PerfReporter(TextWriter.Null);

        reporter.EmitMetric("Report", key, "", 1.0, 0);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            reporter.EmitMetric("Report", key, "", 2.0, 0));
        Assert.Contains("duplicate", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
