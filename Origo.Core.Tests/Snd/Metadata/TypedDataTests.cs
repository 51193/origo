using Origo.Core.Snd.Metadata;
using Xunit;

namespace Origo.Core.Tests;

// ── BlackboardSerializer ───────────────────────────────────────────

public class TypedDataTests
{
    [Fact]
    public void TypedData_Constructor_StoresTypeAndValue()
    {
        var td = new TypedData(typeof(int), 42);
        Assert.Equal(typeof(int), td.DataType);
        Assert.Equal(42, td.Data);
    }

    [Fact]
    public void TypedData_NullValue_Allowed()
    {
        var td = new TypedData(typeof(string), null);
        Assert.Equal(typeof(string), td.DataType);
        Assert.Null(td.Data);
    }
}

// ── SndMetaData ────────────────────────────────────────────────────────
