using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Xunit;

namespace Origo.Core.Tests;

// ── BlackboardSerializer ───────────────────────────────────────────

public class TypeStringMappingExtendedTests
{
    [Fact]
    public void TypeStringMapping_GetTypeByName_UnregisteredType_Throws()
    {
        var mapping = new TypeStringMapping();
        Assert.Throws<InvalidOperationException>(() => mapping.GetTypeByName("nonexistent"));
    }

    [Fact]
    public void TypeStringMapping_GetNameByType_UnregisteredType_Throws()
    {
        var mapping = new TypeStringMapping();
        Assert.Throws<InvalidOperationException>(() => mapping.GetNameByType(typeof(DateTime)));
    }

    [Fact]
    public void TypeStringMapping_RegisterType_DuplicateSameType_NoThrow()
    {
        var mapping = new TypeStringMapping();
        var ex = Record.Exception(() => mapping.RegisterType<int>(BclTypeNames.Int32));
        Assert.Null(ex);
        Assert.Equal(typeof(int), mapping.GetTypeByName(BclTypeNames.Int32));
    }

    [Fact]
    public void TypeStringMapping_RegisterType_ConflictingNameToType_Throws()
    {
        var mapping = new TypeStringMapping();
        // "Int32" is already mapped to int; trying to map it to long should throw
        Assert.Throws<InvalidOperationException>(() => mapping.RegisterType<long>(BclTypeNames.Int32));
    }

    [Fact]
    public void TypeStringMapping_RegisterType_ConflictingTypeToName_Throws()
    {
        var mapping = new TypeStringMapping();
        // int is already mapped to "Int32"; trying to map it to "MyInt" should throw
        Assert.Throws<InvalidOperationException>(() => mapping.RegisterType<int>("MyInt"));
    }

    [Fact]
    public void TypeStringMapping_RegisterType_WhitespaceName_Throws()
    {
        var mapping = new TypeStringMapping();
        Assert.Throws<ArgumentException>(() => mapping.RegisterType<Guid>(""));
        Assert.Throws<ArgumentException>(() => mapping.RegisterType<Guid>("  "));
    }

    [Fact]
    public void TypeStringMapping_BclTypes_AllPreregistered()
    {
        var mapping = new TypeStringMapping();
        Assert.Equal(typeof(int), mapping.GetTypeByName(BclTypeNames.Int32));
        Assert.Equal(typeof(string), mapping.GetTypeByName(BclTypeNames.String));
        Assert.Equal(typeof(bool), mapping.GetTypeByName(BclTypeNames.Boolean));
        Assert.Equal(typeof(float), mapping.GetTypeByName(BclTypeNames.Single));
        Assert.Equal(typeof(double), mapping.GetTypeByName(BclTypeNames.Double));
        Assert.Equal(typeof(long), mapping.GetTypeByName(BclTypeNames.Int64));
        Assert.Equal(typeof(short), mapping.GetTypeByName(BclTypeNames.Int16));
        Assert.Equal(typeof(byte), mapping.GetTypeByName(BclTypeNames.Byte));
        Assert.Equal(typeof(string[]), mapping.GetTypeByName(BclTypeNames.ArrayString));
    }

    [Fact]
    public void TypeStringMapping_RegisterCustomType_RoundTrips()
    {
        var mapping = new TypeStringMapping();
        mapping.RegisterType<Guid>("Guid");
        Assert.Equal(typeof(Guid), mapping.GetTypeByName("Guid"));
        Assert.Equal("Guid", mapping.GetNameByType(typeof(Guid)));
    }

    [Fact]
    public void TypeStringMapping_ReadOnlyDictionaryTypes_Preregistered()
    {
        var mapping = new TypeStringMapping();
        Assert.Equal(typeof(IReadOnlyDictionary<string, string>),
            mapping.GetTypeByName(BclTypeNames.IReadOnlyDictionaryStringString));
        Assert.Equal(typeof(ReadOnlyDictionary<string, string>),
            mapping.GetTypeByName(BclTypeNames.ReadOnlyDictionaryStringString));
        Assert.Equal(BclTypeNames.ReadOnlyDictionaryStringString,
            mapping.GetNameByType(typeof(ReadOnlyDictionary<string, string>)));
        Assert.Equal(BclTypeNames.IReadOnlyDictionaryStringString,
            mapping.GetNameByType(typeof(IReadOnlyDictionary<string, string>)));
    }

    [Fact]
    public void TypeStringMapping_RegisterManyCustomTypes_AllResolvable()
    {
        var mapping = new TypeStringMapping();
        mapping.RegisterType<DateTime>("DateTime");
        mapping.RegisterType<Uri>("Uri");
        mapping.RegisterType<Version>("Version");
        mapping.RegisterType<TimeSpan>("TimeSpan");

        Assert.Equal(typeof(DateTime), mapping.GetTypeByName("DateTime"));
        Assert.Equal(typeof(Uri), mapping.GetTypeByName("Uri"));
        Assert.Equal(typeof(Version), mapping.GetTypeByName("Version"));
        Assert.Equal(typeof(TimeSpan), mapping.GetTypeByName("TimeSpan"));

        Assert.Equal("DateTime", mapping.GetNameByType(typeof(DateTime)));
        Assert.Equal("Uri", mapping.GetNameByType(typeof(Uri)));
        Assert.Equal("Version", mapping.GetNameByType(typeof(Version)));
        Assert.Equal("TimeSpan", mapping.GetNameByType(typeof(TimeSpan)));
    }

    [Fact]
    public void TypeStringMapping_RegisterType_NullName_Throws()
    {
        var mapping = new TypeStringMapping();
        Assert.Throws<ArgumentNullException>(() => mapping.RegisterType<Guid>(null!));
    }

    [Fact]
    public void TypeStringMapping_GetTypeByName_NullName_Throws()
    {
        var mapping = new TypeStringMapping();
        Assert.Throws<ArgumentNullException>(() => mapping.GetTypeByName(null!));
    }

    [Fact]
    public void TypeStringMapping_GetNameByType_NullType_Throws()
    {
        var mapping = new TypeStringMapping();
        Assert.Throws<ArgumentNullException>(() => mapping.GetNameByType(null!));
    }
}

// ── RandomNumberGenerator additional tests ─────────────────────────────
