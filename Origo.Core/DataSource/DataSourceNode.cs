using System;
using System.Collections.Generic;
using System.Globalization;

namespace Origo.Core.DataSource;

/// <summary>
///     数据源树中的单个节点，支持延迟展开。
///     实现 <see cref="IDisposable" /> 以显式释放节点树所持有的资源（子节点、延迟展开闭包等），
///     防止大型节点树在不再需要时继续占用内存。
/// </summary>
public sealed class DataSourceNode : IDisposable
{
    private readonly List<DataSourceNode> _arrayChildren = [];
    private readonly Dictionary<string, DataSourceNode> _objectChildren = new(StringComparer.Ordinal);
    private readonly List<string> _orderedKeys = [];
    private bool _disposed;
    private bool _expanded;
    private Func<string, DataSourceNode>? _expander;

    private DataSourceNodeKind _kind;

    // Lazy loading support
    private string? _rawText;
    private string? _value;

    private DataSourceNode(DataSourceNodeKind kind, string? value = null)
    {
        _kind = kind;
        _value = value;
        _expanded = true;
    }

    private DataSourceNode(string rawText, Func<string, DataSourceNode> expander)
    {
        _rawText = rawText;
        _expander = expander;
        _expanded = false;
    }

    /// <summary>
    ///     节点类型，访问时触发延迟展开。
    /// </summary>
    public DataSourceNodeKind Kind
    {
        get
        {
            EnsureExpanded();
            return _kind;
        }
    }

    public bool IsNull
    {
        get
        {
            EnsureExpanded();
            return _kind == DataSourceNodeKind.Null;
        }
    }

    // ── Object access ──

    public DataSourceNode this[string key]
    {
        get
        {
            EnsureExpanded();
            if (_objectChildren.TryGetValue(key, out var child))
                return child;
            throw new KeyNotFoundException($"Key '{key}' not found in DataSourceNode.");
        }
    }

    public IEnumerable<string> Keys
    {
        get
        {
            EnsureExpanded();
            return _orderedKeys;
        }
    }

    // ── Array access ──

    public DataSourceNode this[int index]
    {
        get
        {
            EnsureExpanded();
            return _arrayChildren[index];
        }
    }

    public int Count
    {
        get
        {
            EnsureExpanded();
            return _arrayChildren.Count;
        }
    }

    public IEnumerable<DataSourceNode> Elements
    {
        get
        {
            EnsureExpanded();
            return _arrayChildren;
        }
    }

    /// <summary>
    ///     释放此节点及其所有子节点所持有的资源。
    ///     释放后任何访问操作将抛出 <see cref="ObjectDisposedException" />。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var child in _arrayChildren)
            child.Dispose();
        _arrayChildren.Clear();

        foreach (var child in _objectChildren.Values)
            child.Dispose();
        _objectChildren.Clear();
        _orderedKeys.Clear();

        _rawText = null;
        _expander = null;
        _value = null;
    }

    public bool TryGetValue(string key, out DataSourceNode? node)
    {
        EnsureExpanded();
        return _objectChildren.TryGetValue(key, out node);
    }

    public bool ContainsKey(string key)
    {
        EnsureExpanded();
        return _objectChildren.ContainsKey(key);
    }

    // ── Value access ──

    public string AsString()
    {
        EnsureExpanded();
        return _kind switch
        {
            DataSourceNodeKind.String => _value ?? string.Empty,
            DataSourceNodeKind.Number => _value ?? string.Empty,
            DataSourceNodeKind.Boolean => _value ?? string.Empty,
            DataSourceNodeKind.Null => string.Empty,
            _ => _value ?? string.Empty
        };
    }

    public byte AsByte()
    {
        EnsureExpanded();
        return byte.Parse(_value!, CultureInfo.InvariantCulture);
    }

    public sbyte AsSByte()
    {
        EnsureExpanded();
        return sbyte.Parse(_value!, CultureInfo.InvariantCulture);
    }

    public short AsShort()
    {
        EnsureExpanded();
        return short.Parse(_value!, CultureInfo.InvariantCulture);
    }

    public ushort AsUShort()
    {
        EnsureExpanded();
        return ushort.Parse(_value!, CultureInfo.InvariantCulture);
    }

    public int AsInt()
    {
        EnsureExpanded();
        return int.Parse(_value!, CultureInfo.InvariantCulture);
    }

    public uint AsUInt()
    {
        EnsureExpanded();
        return uint.Parse(_value!, CultureInfo.InvariantCulture);
    }

    public long AsLong()
    {
        EnsureExpanded();
        return long.Parse(_value!, CultureInfo.InvariantCulture);
    }

    public ulong AsULong()
    {
        EnsureExpanded();
        return ulong.Parse(_value!, CultureInfo.InvariantCulture);
    }

    public float AsFloat()
    {
        EnsureExpanded();
        return float.Parse(_value!, CultureInfo.InvariantCulture);
    }

    public double AsDouble()
    {
        EnsureExpanded();
        return double.Parse(_value!, CultureInfo.InvariantCulture);
    }

    public decimal AsDecimal()
    {
        EnsureExpanded();
        return decimal.Parse(_value!, CultureInfo.InvariantCulture);
    }

    public char AsChar()
    {
        EnsureExpanded();
        return _value is not null && _value.Length > 0 ? _value[0] : '\0';
    }

    public bool AsBool()
    {
        EnsureExpanded();
        return bool.Parse(_value!);
    }

    // ── Builder methods ──

    public DataSourceNode Add(string key, DataSourceNode child)
    {
        EnsureExpanded();
        _objectChildren[key] = child;
        if (!_orderedKeys.Contains(key))
            _orderedKeys.Add(key);
        return this;
    }

    public DataSourceNode Add(DataSourceNode child)
    {
        EnsureExpanded();
        _arrayChildren.Add(child);
        return this;
    }

    // ── Factory methods ──

    public static DataSourceNode CreateObject() => new(DataSourceNodeKind.Object);

    public static DataSourceNode CreateArray() => new(DataSourceNodeKind.Array);

    public static DataSourceNode CreateString(string value) => new(DataSourceNodeKind.String, value);

    public static DataSourceNode CreateNumber(string value) => new(DataSourceNodeKind.Number, value);

    public static DataSourceNode CreateNumber(int value) =>
        new(DataSourceNodeKind.Number, value.ToString(CultureInfo.InvariantCulture));

    public static DataSourceNode CreateNumber(long value) =>
        new(DataSourceNodeKind.Number, value.ToString(CultureInfo.InvariantCulture));

    public static DataSourceNode CreateNumber(float value) =>
        new(DataSourceNodeKind.Number, value.ToString(CultureInfo.InvariantCulture));

    public static DataSourceNode CreateNumber(double value) =>
        new(DataSourceNodeKind.Number, value.ToString(CultureInfo.InvariantCulture));

    public static DataSourceNode CreateBoolean(bool value) => new(DataSourceNodeKind.Boolean, value ? "true" : "false");

    public static DataSourceNode CreateNull() => new(DataSourceNodeKind.Null);

    /// <summary>
    ///     创建延迟展开节点，仅供编解码器内部使用。
    /// </summary>
    internal static DataSourceNode CreateLazy(string rawText, Func<string, DataSourceNode> expander) =>
        new(rawText, expander);

    // ── Private ──

    private void EnsureNotDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private void EnsureExpanded()
    {
        EnsureNotDisposed();

        if (_expanded)
            return;

        // Expand first, then mark as expanded. If the expander throws,
        // the node stays in the lazy state and can be retried or disposed safely.
        var expanded = _expander!(_rawText!);
        var nextOrderedKeys = new List<string>(expanded._orderedKeys.Count);
        var nextObjectChildren =
            new Dictionary<string, DataSourceNode>(expanded._orderedKeys.Count, StringComparer.Ordinal);
        var nextArrayChildren = new List<DataSourceNode>(expanded._arrayChildren.Count);

        foreach (var key in expanded._orderedKeys)
        {
            nextObjectChildren[key] = expanded._objectChildren[key];
            nextOrderedKeys.Add(key);
        }

        nextArrayChildren.AddRange(expanded._arrayChildren);

        _kind = expanded._kind;
        _value = expanded._value;
        _objectChildren.Clear();
        _orderedKeys.Clear();
        _arrayChildren.Clear();

        foreach (var key in nextOrderedKeys)
        {
            _objectChildren[key] = nextObjectChildren[key];
            _orderedKeys.Add(key);
        }

        _arrayChildren.AddRange(nextArrayChildren);

        // Mark expanded only after all state has been committed successfully.
        _expanded = true;

        // Release references for GC
        _rawText = null;
        _expander = null;
    }
}
