using System.Runtime.CompilerServices;
using Origo.Core.Snd.Metadata;
using Origo.GodotAdapter.Snd;
using Xunit;

namespace Origo.GodotAdapter.Tests;

public class TypedDataAssemblyLoadTests
{
    [Fact]
    public void GodotAdapterAssemblyLoad_RegistersTypedDataKinds()
    {
        // Run the adapter assembly's generated [ModuleInitializer] methods,
        // then verify they registered the adapter's TypedData kind range.
        RuntimeHelpers.RunModuleConstructor(typeof(GodotSndManager).Module.ModuleHandle);

        Assert.Equal((byte)128, TypedDataTypeMap.GetKindForType(typeof(Godot.Vector2)));
    }
}
