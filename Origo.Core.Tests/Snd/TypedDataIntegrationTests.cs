using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Entity;
using Origo.Core.DataSource;
using Origo.Core.DataSource.Converters;
using Origo.Core.Serialization;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Scene;
using Xunit;

namespace Origo.Core.Tests;

[Collection("TypedData")]
public class TypedDataIntegrationTests
{
    public TypedDataIntegrationTests()
    {
        TypedData.ResetForTesting();
    }

    [Fact]
    public void Entity_SetData_GetData_RoundTrip_AllRegisteredTypes()
    {
        var entity = new StubSndEntity("test");
        entity.SetData("hp", 100);
        entity.SetData("speed", 3.14f);
        entity.SetData("name", "hero");
        entity.SetData("alive", true);
        entity.SetData("ratio", 0.5);

        Assert.Equal(100, entity.GetData<int>("hp"));
        Assert.Equal(3.14f, entity.GetData<float>("speed"), 0.0001f);
        Assert.Equal("hero", entity.GetData<string>("name"));
        Assert.True(entity.GetData<bool>("alive"));
        Assert.Equal(0.5, entity.GetData<double>("ratio"));
    }

    [Fact]
    public void Entity_TryGetData_WrongType_ReturnsFalse()
    {
        var entity = new StubSndEntity("test");
        entity.SetData("val", 42);

        Assert.False(entity.TryGetData<string>("val").found);
        Assert.False(entity.TryGetData<float>("val").found);
        Assert.False(entity.TryGetData<bool>("val").found);
    }

    [Fact]
    public void Entity_TryGetData_Found_ReturnsTrue()
    {
        var entity = new StubSndEntity("test");
        entity.SetData("score", 999);

        var (found, value) = entity.TryGetData<int>("score");
        Assert.True(found);
        Assert.Equal(999, value);
    }

    [Fact]
    public void Entity_SetData_DifferentTypes_SameKey()
    {
        var entity = new StubSndEntity("test");
        entity.SetData("val", 100);
        entity.SetData("val", "now_a_string");

        Assert.Equal("now_a_string", entity.GetData<string>("val"));
        Assert.False(entity.TryGetData<int>("val").found);
    }

    [Fact]
    public void Direct_Observer_Subscribe_And_Notify()
    {
        var mgr = new Origo.Core.Snd.Entity.DataObserverManager();
        var calls = new List<(TypedData oldValue, TypedData newValue)>();

        mgr.Subscribe("hp", (ov, nv) => calls.Add((ov, nv)));
        mgr.NotifyObservers("hp", (TypedData)100, (TypedData)50);
        mgr.NotifyObservers("hp", (TypedData)50, (TypedData)0);

        Assert.Equal(2, calls.Count);
        Assert.Equal(100, calls[0].oldValue.AsInt32());
        Assert.Equal(50, calls[0].newValue.AsInt32());
        Assert.Equal(50, calls[1].oldValue.AsInt32());
        Assert.Equal(0, calls[1].newValue.AsInt32());
    }

    [Fact]
    public void Blackboard_Set_TryGet_RoundTrip_AllTypes()
    {
        var bb = new Origo.Core.Blackboard.Blackboard();
        bb.SetValue("intVal", 42);
        bb.SetValue("floatVal", 3.14f);
        bb.SetValue("stringVal", "hello");
        bb.SetValue("boolVal", true);
        bb.SetValue("doubleVal", 2.718281828);

        (var fi, var iv) = bb.TryGet<int>("intVal");
        Assert.True(fi);
        Assert.Equal(42, iv);

        (var ff, var fv) = bb.TryGet<float>("floatVal");
        Assert.True(ff);
        Assert.Equal(3.14f, fv, 0.0001f);

        (var fs, var sv) = bb.TryGet<string>("stringVal");
        Assert.True(fs);
        Assert.Equal("hello", sv);

        (var fb, var bv) = bb.TryGet<bool>("boolVal");
        Assert.True(fb);
        Assert.True(bv);

        (var fd, var dv) = bb.TryGet<double>("doubleVal");
        Assert.True(fd);
        Assert.Equal(2.718281828, dv);
    }

    [Fact]
    public void Blackboard_SerializeAll_DeserializeAll_RoundTrip()
    {
        var bb1 = new Origo.Core.Blackboard.Blackboard();
        bb1.SetValue("hp", 100);
        bb1.SetValue("name", "player");
        bb1.SetValue("alive", true);

        var serialized = bb1.SerializeAll();

        var bb2 = new Origo.Core.Blackboard.Blackboard();
        bb2.DeserializeAll(serialized);

        (var f1, var v1) = bb2.TryGet<int>("hp");
        Assert.True(f1);
        Assert.Equal(100, v1);

        (var f2, var v2) = bb2.TryGet<string>("name");
        Assert.True(f2);
        Assert.Equal("player", v2);

        (var f3, var v3) = bb2.TryGet<bool>("alive");
        Assert.True(f3);
        Assert.True(v3);
    }
}
