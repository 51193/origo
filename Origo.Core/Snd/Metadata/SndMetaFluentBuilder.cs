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

    public SndMetaFluentBuilder(string name) : this(new SndMetaData { Name = name })
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
    }

    private SndMetaFluentBuilder(SndMetaData meta)
    {
        _meta = meta ?? throw new ArgumentNullException(nameof(meta));
    }

    /// <summary>
    ///     Create a builder wrapping an existing <see cref="SndMetaData" /> (e.g. from
    ///     <c>ctx.CloneTemplate</c>), allowing additional data to be set fluently.
    /// </summary>
    public static SndMetaFluentBuilder From(SndMetaData meta) => new(meta);

    public SndMetaFluentBuilder SetNode(string key, string value)
    {
        _meta.NodeMetaData ??= new NodeMetaData();
        _meta.NodeMetaData.Pairs[key] = value;
        return this;
    }

    public SndMetaFluentBuilder AddEntityStrategy(string index)
    {
        _meta.StrategyMetaData ??= new StrategyMetaData();
        _meta.StrategyMetaData.EntityIndices.Add(index);
        return this;
    }

    public SndMetaFluentBuilder AddActiveStrategy(string index)
    {
        _meta.StrategyMetaData ??= new StrategyMetaData();
        _meta.StrategyMetaData.ActiveIndices.Add(index);
        return this;
    }

    public SndMetaFluentBuilder SetInt(string key, int value)
    {
        EnsureDataMetaData();
        _meta.DataMetaData!.Pairs[key] = new TypedData(typeof(int), value);
        return this;
    }

    public SndMetaFluentBuilder SetFloat(string key, float value)
    {
        EnsureDataMetaData();
        _meta.DataMetaData!.Pairs[key] = new TypedData(typeof(float), value);
        return this;
    }

    public SndMetaFluentBuilder SetDouble(string key, double value)
    {
        EnsureDataMetaData();
        _meta.DataMetaData!.Pairs[key] = new TypedData(typeof(double), value);
        return this;
    }

    public SndMetaFluentBuilder SetLong(string key, long value)
    {
        EnsureDataMetaData();
        _meta.DataMetaData!.Pairs[key] = new TypedData(typeof(long), value);
        return this;
    }

    public SndMetaFluentBuilder SetBool(string key, bool value)
    {
        EnsureDataMetaData();
        _meta.DataMetaData!.Pairs[key] = new TypedData(typeof(bool), value);
        return this;
    }

    public SndMetaFluentBuilder SetString(string key, string value)
    {
        EnsureDataMetaData();
        _meta.DataMetaData!.Pairs[key] = new TypedData(typeof(string), value);
        return this;
    }

    public SndMetaFluentBuilder SetBytes(string key, byte[] value)
    {
        EnsureDataMetaData();
        _meta.DataMetaData!.Pairs[key] = new TypedData(typeof(byte[]), value);
        return this;
    }

    public SndMetaData Build()
    {
        return _meta;
    }

    private void EnsureDataMetaData()
    {
        _meta.DataMetaData ??= new DataMetaData();
    }
}
