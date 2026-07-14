using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Origo.SourceGeneration.Tests;

/// <summary>
///     Behavioral tests for <see cref="TypedDataGenerator" />: home/adapter mode
///     output, the two-store (inline vs _ref) model, fail-fast diagnostics, and
///     generation determinism. Each case drives the generator over an in-memory
///     compilation and asserts on generated text, diagnostics, and compilability.
/// </summary>
public class TypedDataGeneratorTests
{
    // Minimal scaffold providing the types the generator looks up by metadata name.
    // The IVT lets a separate adapter assembly access TypedData's internal members,
    // mirroring Origo.Core's real [assembly: InternalsVisibleTo("Origo.GodotAdapter")].
    // Header (usings + assembly attributes) is kept separate from the type body so a
    // [assembly: SndInlineTypes(...)] attribute can be inserted before any type
    // declaration, as C# requires.
    private const string _scaffoldHeader = """
        using System;
        using System.Runtime.CompilerServices;

        [assembly: InternalsVisibleTo("Origo.AdapterUnderTest")]
        """;

    private const string _scaffoldBody = """
        namespace Origo.Core.Snd.Metadata
        {
            [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
            public sealed class SndInlineTypesAttribute : Attribute
            {
                public Type[] Types { get; }
                public int StartKind { get; }
                public SndInlineTypesAttribute(params Type[] types) { Types = types; StartKind = 1; }
                public SndInlineTypesAttribute(int startKind, params Type[] types) { Types = types; StartKind = startKind; }
            }

            internal static class TypedDataLayeredRegistry
            {
                public static byte ResolveKind(Type type) => 0;
                public static object? ResolveToObject(TypedData td) => null;
                public static (long inlineBits, object? refValue)? ResolveFromObject(byte kind, object value) => null;
                public static void RegisterKindResolver(Func<Type, byte> resolver) { }
                public static void RegisterFromObjectFallback(Func<byte, object, (long inlineBits, object? refValue)?> fallback) { }
                public static void RegisterToObjectFallback(Func<TypedData, object?> fallback) { }
            }

            public readonly partial struct TypedData
            {
                internal static readonly Type?[] KindTypeMap = new Type?[256];
                public const byte UnregisteredKind = 255;
                internal readonly byte _kind;
                internal readonly long _inlineBits;
                internal readonly object? _ref;
                internal TypedData(byte kind, long inlineBits, object? refValue)
                {
                    _kind = kind;
                    _inlineBits = inlineBits;
                    _ref = refValue;
                }
                internal static void RegisterKind(byte kind, Type type)
                {
                    if (kind != 0) KindTypeMap[kind] = type ?? typeof(object);
                }
            }
        }
        """;

    private const string _homePrimitivesAttribute = """
        [assembly: Origo.Core.Snd.Metadata.SndInlineTypes(
            typeof(byte), typeof(sbyte), typeof(short), typeof(ushort),
            typeof(int), typeof(uint), typeof(long), typeof(ulong),
            typeof(float), typeof(double), typeof(bool), typeof(char), typeof(string))]
        """;

    private const string _adapterTypes = """
        [assembly: Origo.Core.Snd.Metadata.SndInlineTypes(128, typeof(StubVec3), typeof(StubRef))]
        public struct StubVec3 { public float X; public float Y; public float Z; }
        public sealed class StubRef { public int Value; }
        """;

    private static GeneratorOutput RunHome(string attribute) =>
        GeneratorTestHarness.Run("Origo.HomeUnderTest", _scaffoldHeader + "\n" + attribute + "\n" + _scaffoldBody);

    private static GeneratorOutput RunAdapter(string adapterSource)
    {
        var homeCompilation = GeneratorTestHarness.CreateCompilation(
            "Origo.CoreUnderTest", _scaffoldHeader + "\n" + _scaffoldBody);
        var homeRef = homeCompilation.ToMetadataReference();
        return GeneratorTestHarness.Run("Origo.AdapterUnderTest", adapterSource, [homeRef]);
    }

    // ─── Home mode ─────────────────────────────────────────────────

    [Fact]
    public void Home_Primitives_GeneratesExpectedMembers_AndCompiles()
    {
        var output = RunHome(_homePrimitivesAttribute);

        Assert.Empty(output.GeneratorDiagnostics);
        Assert.Empty(output.CompileErrors);

        var text = output.AllGeneratedText;
        Assert.Contains("internal static class KindMap", text);
        Assert.Contains("public const byte Int32 = 5;", text);
        Assert.Contains("public readonly bool TryGetInt32(out int value)", text);
        Assert.Contains("internal readonly int AsInt32()", text);
        Assert.Contains("public static explicit operator TypedData(int value)", text);
        Assert.Contains("internal static class TypedDataFactory<T>", text);
        Assert.Contains("internal static class TypedDataHomeKindRegistration", text);
        Assert.Contains("[ModuleInitializer]", text);
    }

    [Fact]
    public void Home_StringStoredViaRefSlot()
    {
        var output = RunHome(_homePrimitivesAttribute);
        var text = output.AllGeneratedText;

        Assert.Contains("public readonly string? AsString() => (string?)_ref;", text);
        Assert.Contains("case 13: return td._ref;", text);
    }

    // Regression: the abandoned non-system inline path emitted accessors that
    // silently returned 0 / default. None of that machinery may ever be generated.
    [Fact]
    public void Home_DoesNotEmitSilentStubHelpers()
    {
        var text = RunHome(_homePrimitivesAttribute).AllGeneratedText;

        Assert.DoesNotContain("BitsFrom", text);
        Assert.DoesNotContain("ReadBitsAs", text);
        Assert.DoesNotContain("Pack", text);
        Assert.DoesNotContain("return default;", text);
    }

    [Fact]
    public void Home_NoAttribute_ProducesNoOutput()
    {
        var output = RunHome("");

        Assert.Empty(output.GeneratedSources);
        Assert.Empty(output.GeneratorDiagnostics);
    }

    [Fact]
    public void Home_UnsupportedValueType_ReportsORIGOSG002_ButStillGeneratesValidTypes()
    {
        var attribute = "[assembly: Origo.Core.Snd.Metadata.SndInlineTypes(typeof(string), typeof(int), typeof(decimal))]";
        var output = RunHome(attribute);

        Assert.True(output.HasGeneratorDiagnostic("ORIGOSG002"));
        Assert.All(
            output.GeneratorDiagnostics.Where(d => d.Id == "ORIGOSG002"),
            d => Assert.Equal(DiagnosticSeverity.Error, d.Severity));
        // The valid type (int) is still generated; only the unsupported one is dropped.
        Assert.Contains("public readonly bool TryGetInt32(out int value)", output.AllGeneratedText);
        Assert.DoesNotContain("Decimal", output.AllGeneratedText);
        Assert.Empty(output.CompileErrors);
    }

    // ─── Adapter mode ──────────────────────────────────────────────

    [Fact]
    public void Adapter_ValueAndRefTypes_UseRefSlot_AndCompiles()
    {
        var output = RunAdapter(_adapterTypes);

        Assert.Empty(output.GeneratorDiagnostics);
        Assert.Empty(output.CompileErrors);

        var text = output.AllGeneratedText;
        Assert.Contains("public static class TypedDataLayeredExtensions", text);
        // Non-system value type: read through _ref with a type check.
        Assert.Contains("public static StubVec3 AsStubVec3(this TypedData td)", text);
        Assert.Contains("return (StubVec3)td._ref!;", text);
        Assert.Contains("if (td._kind == 128 && td._ref is StubVec3 v)", text);
        // Reference type: nullable cast of _ref.
        Assert.Contains("public static StubRef? AsStubRef(this TypedData td)", text);
        Assert.Contains("return (StubRef?)td._ref;", text);
        // Converter + kind + typemap module initializers.
        Assert.Contains("TypedData.RegisterKind(128, typeof(StubVec3));", text);
        Assert.Contains("case 128: return (0, value);", text);
        Assert.Contains("case 128: return td._ref;", text);
        Assert.Contains("if (type == typeof(StubVec3)) return 128;", text);
    }

    [Fact]
    public void Adapter_DoesNotEmitInlineHelpers()
    {
        var text = RunAdapter(_adapterTypes).AllGeneratedText;

        Assert.DoesNotContain("ReadBitsAs", text);
        Assert.DoesNotContain("BitsFrom", text);
        Assert.DoesNotContain("_inlineBits", text);
    }

    [Fact]
    public void Adapter_SystemPrimitive_ReportsORIGOSG001()
    {
        var adapterSource = "[assembly: Origo.Core.Snd.Metadata.SndInlineTypes(128, typeof(int))]";
        var output = RunAdapter(adapterSource);

        Assert.True(output.HasGeneratorDiagnostic("ORIGOSG001"));
        Assert.All(
            output.GeneratorDiagnostics.Where(d => d.Id == "ORIGOSG001"),
            d => Assert.Equal(DiagnosticSeverity.Error, d.Severity));
        // The offending primitive is dropped, so no inline accessor is produced.
        Assert.DoesNotContain("AsInt32", output.AllGeneratedText);
    }

    // ─── Cross-cutting ─────────────────────────────────────────────

    [Fact]
    public void Generation_IsDeterministic()
    {
        var first = RunHome(_homePrimitivesAttribute).AllGeneratedText;
        var second = RunHome(_homePrimitivesAttribute).AllGeneratedText;

        Assert.Equal(first, second);
    }

    [Fact]
    public void StartKind_OffsetIsHonored_AndNumberingIsSequential()
    {
        var text = RunAdapter(_adapterTypes).AllGeneratedText;

        Assert.Contains("TypedData.RegisterKind(128, typeof(StubVec3));", text);
        Assert.Contains("TypedData.RegisterKind(129, typeof(StubRef));", text);
    }

    [Fact]
    public void KindPastByteRange_ReportsORIGOSG003_IncludingWrapToNonZero()
    {
        // startKind 255 + 3 primitives: byte->255 (valid), sbyte->256, short->257.
        // Both 256 and 257 exceed the byte range. 257 in particular would wrap to
        // byte 1 and silently collide with another type — it must be reported.
        var attribute =
            "[assembly: Origo.Core.Snd.Metadata.SndInlineTypes(255, typeof(byte), typeof(sbyte), typeof(short))]";
        var output = RunHome(attribute);

        Assert.True(output.HasGeneratorDiagnostic("ORIGOSG003"));
        Assert.All(
            output.GeneratorDiagnostics.Where(d => d.Id == "ORIGOSG003"),
            d => Assert.Equal(DiagnosticSeverity.Error, d.Severity));

        var text = output.AllGeneratedText;
        // The in-range type is still generated; both out-of-range types are dropped.
        Assert.Contains("public const byte Byte = 255;", text);
        Assert.DoesNotContain("AsSByte", text);
        Assert.DoesNotContain("AsInt16", text);
    }

    [Fact]
    public void Home_OnlyReferenceTypes_NoInlineMethods()
    {
        var attribute = "[assembly: Origo.Core.Snd.Metadata.SndInlineTypes(typeof(string))]";
        var output = RunHome(attribute);

        Assert.Empty(output.GeneratorDiagnostics);
        Assert.Empty(output.CompileErrors);

        var text = output.AllGeneratedText;
        Assert.Contains("KindMap", text);
        Assert.Contains("AsString(", text);
        Assert.DoesNotContain("public static explicit operator TypedData(int", text);
        Assert.DoesNotContain("_inlineBits", text);
    }

    [Fact]
    public void Home_WithoutString_GeneratesCompilableCode()
    {
        var attribute = "[assembly: Origo.Core.Snd.Metadata.SndInlineTypes(typeof(int), typeof(float))]";
        var output = RunHome(attribute);

        Assert.Empty(output.CompileErrors);
        Assert.DoesNotContain("KindMap.String", output.AllGeneratedText);
        Assert.DoesNotContain("IsString", output.AllGeneratedText);
        Assert.DoesNotContain("AsString(", output.AllGeneratedText);
        Assert.DoesNotContain("TryGetString(", output.AllGeneratedText);
    }

    [Fact]
    public void OverlappingStartKinds_SameType_Deduplicated()
    {
        var attribute = """
            [assembly: Origo.Core.Snd.Metadata.SndInlineTypes(1, typeof(int), typeof(string), typeof(float))]
            [assembly: Origo.Core.Snd.Metadata.SndInlineTypes(1, typeof(int), typeof(string))]
            """;
        var output = RunHome(attribute);

        Assert.Empty(output.GeneratorDiagnostics);
        Assert.Empty(output.CompileErrors);

        var text = output.AllGeneratedText;
        Assert.Contains("Int32", text);
        Assert.Contains("Single", text);
    }

    [Fact]
    public void OverlappingStartKindRanges_ReportORIGOSG004_AndDropCollidingTypes()
    {
        // Two SndInlineTypes groups assign kind 1 to different types.
        var attribute = """
            [assembly: Origo.Core.Snd.Metadata.SndInlineTypes(1, typeof(int))]
            [assembly: Origo.Core.Snd.Metadata.SndInlineTypes(1, typeof(long))]
            """;
        var output = RunHome(attribute);

        Assert.True(output.HasGeneratorDiagnostic("ORIGOSG004"));
        Assert.All(
            output.GeneratorDiagnostics.Where(d => d.Id == "ORIGOSG004"),
            d => Assert.Equal(DiagnosticSeverity.Error, d.Severity));

        var text = output.AllGeneratedText;
        Assert.DoesNotContain("AsInt32", text);
        Assert.DoesNotContain("AsInt64", text);
    }

    // ─── Incremental generator behaviour ────────────────────────────

    [Fact]
    public void Incremental_SameInputTwice_ProducesIdenticalOutput()
    {
        var source = _scaffoldHeader + "\n" + _homePrimitivesAttribute + "\n" + _scaffoldBody;
        var compilation = GeneratorTestHarness.CreateCompilation("Origo.HomeUnderTest", source);
        var driver = GeneratorTestHarness.CreateTrackedDriver();

        var (first, driver2) = GeneratorTestHarness.RunIncremental(driver, compilation);
        var (second, _) = GeneratorTestHarness.RunIncremental(driver2, compilation);

        Assert.Equal(first.GeneratedSources.Length, second.GeneratedSources.Length);
        for (var i = 0; i < first.GeneratedSources.Length; i++)
            Assert.Equal(first.GeneratedSources[i], second.GeneratedSources[i]);
    }

    [Fact]
    public void Incremental_SameInputTwice_TrackedStepsPresent()
    {
        var source = _scaffoldHeader + "\n" + _homePrimitivesAttribute + "\n" + _scaffoldBody;
        var compilation = GeneratorTestHarness.CreateCompilation("Origo.HomeUnderTest", source);
        var driver = GeneratorTestHarness.CreateTrackedDriver();

        var (_, driver2) = GeneratorTestHarness.RunIncremental(driver, compilation);
        var (_, driver3) = GeneratorTestHarness.RunIncremental(driver2, compilation);

        var runResult = driver3.GetRunResult();
        Assert.True(runResult.Results.Length > 0);

        var trackedSteps = runResult.Results[0].TrackedSteps;
        Assert.NotEmpty(trackedSteps);
    }

    [Fact]
    public void Incremental_SameInputTwice_NoAdditionalOutputs()
    {
        var source = _scaffoldHeader + "\n" + _homePrimitivesAttribute + "\n" + _scaffoldBody;
        var compilation = GeneratorTestHarness.CreateCompilation("Origo.HomeUnderTest", source);
        var driver = GeneratorTestHarness.CreateTrackedDriver();

        var (first, driver2) = GeneratorTestHarness.RunIncremental(driver, compilation);
        var (_, driver3) = GeneratorTestHarness.RunIncremental(driver2, compilation);
        var (third, _) = GeneratorTestHarness.RunIncremental(driver3, compilation);

        Assert.Equal(first.GeneratedSources.Length, third.GeneratedSources.Length);
        for (var i = 0; i < first.GeneratedSources.Length; i++)
            Assert.Equal(first.GeneratedSources[i], third.GeneratedSources[i]);
    }

    [Fact]
    public void Incremental_UnrelatedCodeChange_GeneratedOutputUnchanged()
    {
        var sourceA = _scaffoldHeader + "\n" + _homePrimitivesAttribute + "\n" + _scaffoldBody;
        var sourceB = sourceA + "\n// unrelated comment";
        var compilationA = GeneratorTestHarness.CreateCompilation("Origo.HomeUnderTest", sourceA);
        var compilationB = GeneratorTestHarness.CreateCompilation("Origo.HomeUnderTest", sourceB);
        var driver = GeneratorTestHarness.CreateTrackedDriver();

        var (first, driver2) = GeneratorTestHarness.RunIncremental(driver, compilationA);
        var (second, _) = GeneratorTestHarness.RunIncremental(driver2, compilationB);

        Assert.Equal(first.GeneratedSources.Length, second.GeneratedSources.Length);
        for (var i = 0; i < first.GeneratedSources.Length; i++)
            Assert.Equal(first.GeneratedSources[i], second.GeneratedSources[i]);
    }

    [Fact]
    public void Incremental_NoAttribute_ThenAddAttribute_ProducesNewOutput()
    {
        var noAttrSource = _scaffoldHeader + "\n" + _scaffoldBody;
        var withAttrSource = _scaffoldHeader + "\n" + _homePrimitivesAttribute + "\n" + _scaffoldBody;
        var compilationA = GeneratorTestHarness.CreateCompilation("Origo.HomeUnderTest", noAttrSource);
        var compilationB = GeneratorTestHarness.CreateCompilation("Origo.HomeUnderTest", withAttrSource);
        var driver = GeneratorTestHarness.CreateTrackedDriver();

        var (first, driver2) = GeneratorTestHarness.RunIncremental(driver, compilationA);
        var (second, _) = GeneratorTestHarness.RunIncremental(driver2, compilationB);

        Assert.Empty(first.GeneratedSources);
        Assert.NotEmpty(second.GeneratedSources);
    }

    [Fact]
    public void Incremental_HasAttribute_ThenRemoveAttribute_OutputDisappears()
    {
        var withAttrSource = _scaffoldHeader + "\n" + _homePrimitivesAttribute + "\n" + _scaffoldBody;
        var noAttrSource = _scaffoldHeader + "\n" + _scaffoldBody;
        var compilationA = GeneratorTestHarness.CreateCompilation("Origo.HomeUnderTest", withAttrSource);
        var compilationB = GeneratorTestHarness.CreateCompilation("Origo.HomeUnderTest", noAttrSource);
        var driver = GeneratorTestHarness.CreateTrackedDriver();

        var (first, driver2) = GeneratorTestHarness.RunIncremental(driver, compilationA);
        var (second, _) = GeneratorTestHarness.RunIncremental(driver2, compilationB);

        Assert.NotEmpty(first.GeneratedSources);
        Assert.Empty(second.GeneratedSources);
    }

    [Fact]
    public void Incremental_AddTypeToExistingAttribute_OutputChanges()
    {
        var prefix = _scaffoldHeader + "\n";
        var suffix = "\n" + _scaffoldBody;
        var oneTypeAttr = "[assembly: Origo.Core.Snd.Metadata.SndInlineTypes(typeof(int))]";
        var twoTypeAttr = "[assembly: Origo.Core.Snd.Metadata.SndInlineTypes(typeof(int), typeof(float))]";
        var sourceA = prefix + oneTypeAttr + suffix;
        var sourceB = prefix + twoTypeAttr + suffix;
        var compilationA = GeneratorTestHarness.CreateCompilation("Origo.HomeUnderTest", sourceA);
        var compilationB = GeneratorTestHarness.CreateCompilation("Origo.HomeUnderTest", sourceB);
        var driver = GeneratorTestHarness.CreateTrackedDriver();

        var (first, driver2) = GeneratorTestHarness.RunIncremental(driver, compilationA);
        var (second, _) = GeneratorTestHarness.RunIncremental(driver2, compilationB);

        Assert.DoesNotContain("Single", first.AllGeneratedText);
        Assert.Contains("Int32", first.AllGeneratedText);
        Assert.Contains("Int32", second.AllGeneratedText);
        Assert.Contains("Single", second.AllGeneratedText);
    }
}
