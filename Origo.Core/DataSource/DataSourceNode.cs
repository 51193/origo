using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

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
            DataSourceNodeKind.Text => _value ?? string.Empty,
            DataSourceNodeKind.Number => _value ?? string.Empty,
            DataSourceNodeKind.Bool => _value ?? string.Empty,
            DataSourceNodeKind.Null => string.Empty,
            _ => _value ?? string.Empty
        };
    }

    public byte AsByte()
    {
        EnsureExpanded();
        ThrowIfValueMissing();
        return byte.Parse(_value!, CultureInfo.InvariantCulture);
    }

    public sbyte AsSByte()
    {
        EnsureExpanded();
        ThrowIfValueMissing();
        return sbyte.Parse(_value!, CultureInfo.InvariantCulture);
    }

    public short AsShort()
    {
        EnsureExpanded();
        ThrowIfValueMissing();
        return short.Parse(_value!, CultureInfo.InvariantCulture);
    }

    public ushort AsUShort()
    {
        EnsureExpanded();
        ThrowIfValueMissing();
        return ushort.Parse(_value!, CultureInfo.InvariantCulture);
    }

    public int AsInt()
    {
        EnsureExpanded();
        ThrowIfValueMissing();
        return int.Parse(_value!, CultureInfo.InvariantCulture);
    }

    public uint AsUInt()
    {
        EnsureExpanded();
        ThrowIfValueMissing();
        return uint.Parse(_value!, CultureInfo.InvariantCulture);
    }

    public long AsLong()
    {
        EnsureExpanded();
        ThrowIfValueMissing();
        return long.Parse(_value!, CultureInfo.InvariantCulture);
    }

    public ulong AsULong()
    {
        EnsureExpanded();
        ThrowIfValueMissing();
        return ulong.Parse(_value!, CultureInfo.InvariantCulture);
    }

    public float AsFloat()
    {
        EnsureExpanded();
        ThrowIfValueMissing();
        return float.Parse(_value!, CultureInfo.InvariantCulture);
    }

    public double AsDouble()
    {
        EnsureExpanded();
        ThrowIfValueMissing();
        return double.Parse(_value!, CultureInfo.InvariantCulture);
    }

    public decimal AsDecimal()
    {
        EnsureExpanded();
        ThrowIfValueMissing();
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
        ThrowIfValueMissing();
        return bool.Parse(_value!);
    }

    private void ThrowIfValueMissing()
    {
        if (_value is null)
            throw new InvalidOperationException(
                $"Cannot parse value of DataSourceNode (Kind={Kind}): node resolved to null.");
    }

    /// <summary>
    ///     计算整个节点树的 SHA-256 哈希（十六进制小写）。
    ///     用于写入幂等性校验——同一数据树产生相同 hash。
    /// </summary>
    public string ComputeSha256Hash()
    {
        EnsureExpanded();
        var canonical = BuildCanonicalString();
        var bytes = Encoding.UTF8.GetBytes(canonical);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
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

    public static DataSourceNode CreateObject() => new(DataSourceNodeKind.Map);

    public static DataSourceNode CreateArray() => new(DataSourceNodeKind.Array);

    public static DataSourceNode CreateString(string value) => new(DataSourceNodeKind.Text, value);

    public static DataSourceNode CreateNumber(string value) => new(DataSourceNodeKind.Number, value);

    public static DataSourceNode CreateNumber(int value) =>
        new(DataSourceNodeKind.Number, value.ToString(CultureInfo.InvariantCulture));

    public static DataSourceNode CreateNumber(long value) =>
        new(DataSourceNodeKind.Number, value.ToString(CultureInfo.InvariantCulture));

    public static DataSourceNode CreateNumber(float value) =>
        new(DataSourceNodeKind.Number, value.ToString(CultureInfo.InvariantCulture));

    public static DataSourceNode CreateNumber(double value) =>
        new(DataSourceNodeKind.Number, value.ToString(CultureInfo.InvariantCulture));

    public static DataSourceNode CreateBoolean(bool value) => new(DataSourceNodeKind.Bool, value ? "true" : "false");

    public static DataSourceNode CreateNull() => new(DataSourceNodeKind.Null);

    /// <summary>
    ///     创建延迟展开节点，仅供编解码器内部使用。
    /// </summary>
    internal static DataSourceNode CreateLazy(string rawText, Func<string, DataSourceNode> expander) =>
        new(rawText, expander);

    // ── Private ──

    private void EnsureNotDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private string BuildCanonicalString()
    {
        return _kind switch
        {
            DataSourceNodeKind.Map => "O{" + string.Join(",",
                _orderedKeys.OrderBy(k => k, StringComparer.Ordinal)
                    .Select(k => k + "=" + _objectChildren[k].BuildCanonicalString())) + "}",
            DataSourceNodeKind.Array => "A[" + string.Join(",",
                _arrayChildren.Select(c => c.BuildCanonicalString())) + "]",
            DataSourceNodeKind.Text => "S\"" + EscapeCanonical(_value) + "\"",
            DataSourceNodeKind.Number => "N" + _value,
            DataSourceNodeKind.Bool => "B" + _value,
            DataSourceNodeKind.Null => "X",
            _ => "X"
        };
    }

    private static string EscapeCanonical(string? value)
    {
        if (value is null) return string.Empty;
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

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
