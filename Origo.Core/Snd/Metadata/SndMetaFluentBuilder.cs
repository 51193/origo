using System;
using System.Collections.Generic;

namespace Origo.Core.Snd.Metadata;

/// <summary>
///     Fluent builder for <see cref="SndMetaData" />, eliminating repetitive
///     <c>??= new DataMetaData()</c> and manual <see cref="TypedData" /> construction.
/// </summary>
public sealed class SndMetaFluentBuilder
{
    private readonly SndMetaData _meta;

    /// <summary>Creates a builder for a new entity metadata named <paramref name="name" />.</summary>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name" /> is null or whitespace.</exception>
    public SndMetaFluentBuilder(string name) : this(new SndMetaData())
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _meta.Name = name;
    }

    private SndMetaFluentBuilder(SndMetaData meta)
    {
        _meta = meta ?? throw new ArgumentNullException(nameof(meta));
    }

    /// <summary>
    ///     Create a builder wrapping an existing <see cref="SndMetaData" /> (e.g. from
    ///     <c>ctx.Template.CloneTemplate</c>), allowing additional data to be set fluently.
    /// </summary>
    public static SndMetaFluentBuilder From(SndMetaData meta) => new(meta);

    /// <summary>Sets a node handle value under the given key.</summary>
    public SndMetaFluentBuilder SetNode(string key, string value)
    {
        _meta.NodeMetaData ??= new NodeMetaData();
        _meta.NodeMetaData.Pairs[key] = value;
        return this;
    }

    /// <summary>Adds a passive lifecycle strategy index to the metadata.</summary>
    public SndMetaFluentBuilder AddLifecycleStrategy(string index)
    {
        _meta.StrategyMetaData ??= new StrategyMetaData();
        _meta.StrategyMetaData.LifecycleIndices.Add(index);
        return this;
    }

    /// <summary>Adds an active strategy index to the metadata.</summary>
    public SndMetaFluentBuilder AddActiveStrategy(string index)
    {
        _meta.StrategyMetaData ??= new StrategyMetaData();
        _meta.StrategyMetaData.ActiveIndices.Add(index);
        return this;
    }

    /// <summary>Sets an <see cref="int" /> data value under the given key.</summary>
    public SndMetaFluentBuilder SetInt(string key, int value)
    {
        EnsureDataMetaData();
        _meta.DataMetaData!.Pairs[key] = (TypedData)value;
        return this;
    }

    /// <summary>Sets a <see cref="float" /> data value under the given key.</summary>
    public SndMetaFluentBuilder SetFloat(string key, float value)
    {
        EnsureDataMetaData();
        _meta.DataMetaData!.Pairs[key] = (TypedData)value;
        return this;
    }

    /// <summary>Sets a <see cref="double" /> data value under the given key.</summary>
    public SndMetaFluentBuilder SetDouble(string key, double value)
    {
        EnsureDataMetaData();
        _meta.DataMetaData!.Pairs[key] = (TypedData)value;
        return this;
    }

    /// <summary>Sets a <see cref="long" /> data value under the given key.</summary>
    public SndMetaFluentBuilder SetLong(string key, long value)
    {
        EnsureDataMetaData();
        _meta.DataMetaData!.Pairs[key] = (TypedData)value;
        return this;
    }

    /// <summary>Sets a <see cref="bool" /> data value under the given key.</summary>
    public SndMetaFluentBuilder SetBool(string key, bool value)
    {
        EnsureDataMetaData();
        _meta.DataMetaData!.Pairs[key] = (TypedData)value;
        return this;
    }

    /// <summary>Sets a <see cref="string" /> data value under the given key.</summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value" /> is null.</exception>
    public SndMetaFluentBuilder SetString(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        EnsureDataMetaData();
        _meta.DataMetaData!.Pairs[key] = new TypedData(TypedData.KindMap.String, 0, value);
        return this;
    }

    /// <summary>Sets a raw <see cref="byte" /> array data value under the given key.</summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value" /> is null.</exception>
    public SndMetaFluentBuilder SetBytes(string key, byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        EnsureDataMetaData();
        _meta.DataMetaData!.Pairs[key] = new TypedData(TypedData.UnregisteredKind, 0, value);
        return this;
    }

    /// <summary>Returns the fully-built <see cref="SndMetaData" />.</summary>
    public SndMetaData Build() => _meta;

    private void EnsureDataMetaData() => _meta.DataMetaData ??= new DataMetaData();
}
