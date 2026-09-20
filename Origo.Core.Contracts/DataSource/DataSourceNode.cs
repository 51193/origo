using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Origo.Core.DataSource;

/// <summary>
///     A single node in the data source tree, supporting lazy expansion.
///     Implements <see cref="IDisposable" /> to explicitly release resources held by the node tree
///     (child nodes, lazy expansion closures, etc.), preventing large node trees from continuing
///     to occupy memory when no longer needed.
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
    ///     The node kind. Triggers lazy expansion on access.
    /// </summary>
    public DataSourceNodeKind Kind
    {
        get
        {
            EnsureExpanded();
            return _kind;
        }
    }

    /// <summary>True when the node is a null (Nil) node.</summary>
    public bool IsNull
    {
        get
        {
            EnsureExpanded();
            return _kind == DataSourceNodeKind.Null;
        }
    }

    // ── Object access ──

    /// <summary>Gets a child node by key on a map node.</summary>
    /// <exception cref="KeyNotFoundException">Thrown when the key is absent.</exception>
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

    /// <summary>
    ///     Enumerates the keys of a map node through a read-only view; the
    ///     underlying key storage is not exposed for mutation.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when this node is not a map.</exception>
    public IEnumerable<string> Keys
    {
        get
        {
            EnsureExpanded();
            if (_kind != DataSourceNodeKind.Map)
                throw new InvalidOperationException(
                    $"Cannot enumerate keys of a {_kind} DataSourceNode; expected Map.");
            return _orderedKeys.AsReadOnly();
        }
    }

    // ── Array access ──

    /// <summary>Gets a child node by index on an array node.</summary>
    public DataSourceNode this[int index]
    {
        get
        {
            EnsureExpanded();
            return _arrayChildren[index];
        }
    }

    /// <summary>Gets the element count of an array node.</summary>
    /// <exception cref="InvalidOperationException">Thrown when this node is not an array.</exception>
    public int Count
    {
        get
        {
            EnsureExpanded();
            ThrowIfNotKind(DataSourceNodeKind.Array, "count elements of");
            return _arrayChildren.Count;
        }
    }

    /// <summary>
    ///     Enumerates the child nodes of an array node through a read-only
    ///     view; the underlying child storage is not exposed for mutation.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when this node is not an array.</exception>
    public IEnumerable<DataSourceNode> Elements
    {
        get
        {
            EnsureExpanded();
            ThrowIfNotKind(DataSourceNodeKind.Array, "enumerate elements of");
            return _arrayChildren.AsReadOnly();
        }
    }

    /// <summary>
    ///     Releases resources held by this node and all its child nodes.
    ///     After disposal, any access operation will throw <see cref="ObjectDisposedException" />.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        var stack = new Stack<DataSourceNode>();
        stack.Push(this);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node._disposed) continue;
            node._disposed = true;

            foreach (var child in node._arrayChildren)
                stack.Push(child);
            node._arrayChildren.Clear();

            foreach (var child in node._objectChildren.Values)
                stack.Push(child);
            node._objectChildren.Clear();
            node._orderedKeys.Clear();

            node._rawText = null;
            node._expander = null;
            node._value = null;
        }
    }

    /// <summary>Attempts to get a child node by key; returns false when the key is absent.</summary>
    public bool TryGetValue(string key, out DataSourceNode? node)
    {
        EnsureExpanded();
        return _objectChildren.TryGetValue(key, out node);
    }

    /// <summary>Checks whether a map node contains the given key.</summary>
    public bool ContainsKey(string key)
    {
        EnsureExpanded();
        return _objectChildren.ContainsKey(key);
    }

    // ── Value access ──

    /// <summary>Gets the raw string value of a text/number/bool/null node.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the node is a map or array.</exception>
    public string AsString()
    {
        EnsureExpanded();
        if (_kind is DataSourceNodeKind.Map or DataSourceNodeKind.Array)
            throw new InvalidOperationException(
                $"Cannot get string value from a {_kind} DataSourceNode.");
        return _value ?? string.Empty;
    }

    /// <summary>Gets the first character of the node's raw string value.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the node is a map/array, or its value is empty.</exception>
    public char AsChar()
    {
        EnsureExpanded();
        if (_kind is not DataSourceNodeKind.Text
            and not DataSourceNodeKind.Number
            and not DataSourceNodeKind.Bool
            and not DataSourceNodeKind.Null)
        {
            throw new InvalidOperationException(
                $"Cannot get char value from a {_kind} DataSourceNode.");
        }
        if (_value is null || _value.Length == 0)
            throw new InvalidOperationException(
                "Cannot get char value from an empty DataSourceNode.");
        return _value[0];
    }

    /// <summary>
    ///     Generic typed value accessor. Supported types: <see cref="string" />,
    ///     <see cref="byte" />, <see cref="sbyte" />, <see cref="short" />,
    ///     <see cref="ushort" />, <see cref="int" />, <see cref="uint" />,
    ///     <see cref="long" />, <see cref="ulong" />, <see cref="float" />,
    ///     <see cref="double" />, <see cref="decimal" />, <see cref="char" />,
    ///     <see cref="bool" />.
    ///     For complex types (arrays, domain objects), use
    ///     <see cref="DataSourceConverterRegistry.Read{T}" />.
    /// </summary>
    public T As<T>()
    {
        EnsureExpanded();

        if (typeof(T) == typeof(string))
            return (T)(object)AsString();

        if (typeof(T) == typeof(char))
            return (T)(object)AsChar();

        ThrowIfValueMissing();

        if (typeof(T) == typeof(byte)) return (T)(object)byte.Parse(_value!, CultureInfo.InvariantCulture);
        if (typeof(T) == typeof(sbyte)) return (T)(object)sbyte.Parse(_value!, CultureInfo.InvariantCulture);
        if (typeof(T) == typeof(short)) return (T)(object)short.Parse(_value!, CultureInfo.InvariantCulture);
        if (typeof(T) == typeof(ushort)) return (T)(object)ushort.Parse(_value!, CultureInfo.InvariantCulture);
        if (typeof(T) == typeof(int)) return (T)(object)int.Parse(_value!, CultureInfo.InvariantCulture);
        if (typeof(T) == typeof(uint)) return (T)(object)uint.Parse(_value!, CultureInfo.InvariantCulture);
        if (typeof(T) == typeof(long)) return (T)(object)long.Parse(_value!, CultureInfo.InvariantCulture);
        if (typeof(T) == typeof(ulong)) return (T)(object)ulong.Parse(_value!, CultureInfo.InvariantCulture);
        if (typeof(T) == typeof(float)) return (T)(object)float.Parse(_value!, CultureInfo.InvariantCulture);
        if (typeof(T) == typeof(double)) return (T)(object)double.Parse(_value!, CultureInfo.InvariantCulture);
        if (typeof(T) == typeof(decimal)) return (T)(object)decimal.Parse(_value!, CultureInfo.InvariantCulture);
        if (typeof(T) == typeof(bool)) return (T)(object)bool.Parse(_value!);

        throw new NotSupportedException(
            $"As<{typeof(T).Name}> is not supported. " +
            "Only primitive types are supported. " +
            "For complex types, use DataSourceConverterRegistry.Read<T>.");
    }

    private static bool IsValidJsonNumberLiteral(string value)
    {
        if (value.Length == 0)
            return false;

        var index = 0;
        if (value[index] == '-')
        {
            index++;
            if (index >= value.Length)
                return false;
        }

        if (value[index] == '0')
        {
            index++;
            if (index < value.Length && value[index] is >= '0' and <= '9')
                return false;
        }
        else if (value[index] is >= '1' and <= '9')
        {
            do
            {
                index++;
            } while (index < value.Length && value[index] is >= '0' and <= '9');
        }
        else
        {
            return false;
        }

        if (index < value.Length && value[index] == '.')
        {
            index++;
            if (index >= value.Length || value[index] is not (>= '0' and <= '9'))
                return false;
            do
            {
                index++;
            } while (index < value.Length && value[index] is >= '0' and <= '9');
        }

        if (index < value.Length && value[index] is 'e' or 'E')
        {
            index++;
            if (index < value.Length && value[index] is '+' or '-')
                index++;
            if (index >= value.Length || value[index] is not (>= '0' and <= '9'))
                return false;
            do
            {
                index++;
            } while (index < value.Length && value[index] is >= '0' and <= '9');
        }

        return index == value.Length;
    }

    private void ThrowIfNotKind(DataSourceNodeKind expected, string operation)
    {
        if (_kind != expected)
            throw new InvalidOperationException(
                $"Cannot {operation} a {_kind} DataSourceNode; expected {expected}.");
    }

    private void ThrowIfValueMissing()
    {
        if (_value is null)
            throw new InvalidOperationException(
                $"Cannot parse value of DataSourceNode (Kind={Kind}): node resolved to null.");
    }

    /// <summary>
    ///     Computes the SHA-256 hash of the entire node tree (lowercase hexadecimal).
    ///     Used for write idempotency verification — the same data tree produces the same hash.
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

    /// <summary>Adds a child under the given key on a map node and returns this node.</summary>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when this node is not a map: children added to a scalar node
    ///     would be silently dropped by every codec (encode only visits
    ///     Map/Array children).
    /// </exception>
    public DataSourceNode Add(string key, DataSourceNode child)
    {
        EnsureExpanded();
        ArgumentNullException.ThrowIfNull(child);
        if (_kind != DataSourceNodeKind.Map)
            throw new InvalidOperationException(
                $"Cannot add a child to a {_kind} DataSourceNode: children are " +
                "only representable on Map nodes. Use CreateObject() for object " +
                "trees or the single-argument Add overload on array nodes.");
        _objectChildren[key] = child;
        if (!_orderedKeys.Contains(key))
            _orderedKeys.Add(key);
        return this;
    }

    /// <summary>Appends a child to an array node and returns this node.</summary>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when this node is not an array: children added to a scalar
    ///     node would be silently dropped by every codec (encode only visits
    ///     Map/Array children).
    /// </exception>
    public DataSourceNode Add(DataSourceNode child)
    {
        EnsureExpanded();
        ArgumentNullException.ThrowIfNull(child);
        if (_kind != DataSourceNodeKind.Array)
            throw new InvalidOperationException(
                $"Cannot append a child to a {_kind} DataSourceNode: children are " +
                "only representable on Array nodes. Use CreateArray() for array " +
                "trees or the keyed Add overload on map nodes.");
        _arrayChildren.Add(child);
        return this;
    }

    // ── Factory methods ──

    /// <summary>Creates an empty map (object) node.</summary>
    public static DataSourceNode CreateObject() => new(DataSourceNodeKind.Map);

    /// <summary>Creates an empty array node.</summary>
    public static DataSourceNode CreateArray() => new(DataSourceNodeKind.Array);

    /// <summary>Creates a text node carrying the given string value.</summary>
    public static DataSourceNode CreateString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new DataSourceNode(DataSourceNodeKind.Text, value);
    }

    /// <summary>
    ///     Creates a number node from its invariant-culture string
    ///     representation. The value must be a valid JSON number literal:
    ///     number nodes are encoded verbatim into JSON, and an invalid
    ///     literal would produce non-portable output or fail later at
    ///     encode time.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value" /> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value" /> is not a valid JSON number literal.</exception>
    public static DataSourceNode CreateNumber(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!IsValidJsonNumberLiteral(value))
            throw new ArgumentException(
                $"'{value}' is not a valid JSON number literal. Number nodes are encoded " +
                "verbatim into JSON and must follow the JSON number grammar.",
                nameof(value));
        return new DataSourceNode(DataSourceNodeKind.Number, value);
    }

    /// <summary>Creates a number node from an <see cref="int" /> value.</summary>
    public static DataSourceNode CreateNumber(int value) =>
        new(DataSourceNodeKind.Number, value.ToString(CultureInfo.InvariantCulture));

    /// <summary>Creates a number node from a <see cref="long" /> value.</summary>
    public static DataSourceNode CreateNumber(long value) =>
        new(DataSourceNodeKind.Number, value.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    ///     Creates a number node from a <see cref="float" /> value. Non-finite
    ///     values are rejected because JSON has no representation for NaN or
    ///     infinity.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value" /> is NaN or infinity.</exception>
    public static DataSourceNode CreateNumber(float value)
    {
        if (!float.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(value), value,
                "Number node values must be finite; JSON has no NaN or infinity literal.");
        return CreateNumber(value.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    ///     Creates a number node from a <see cref="double" /> value. Non-finite
    ///     values are rejected because JSON has no representation for NaN or
    ///     infinity.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value" /> is NaN or infinity.</exception>
    public static DataSourceNode CreateNumber(double value)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(value), value,
                "Number node values must be finite; JSON has no NaN or infinity literal.");
        return CreateNumber(value.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>Creates a boolean node.</summary>
    public static DataSourceNode CreateBoolean(bool value) => new(DataSourceNodeKind.Bool, value ? "true" : "false");

    /// <summary>Creates a null node.</summary>
    public static DataSourceNode CreateNull() => new(DataSourceNodeKind.Null);

    /// <summary>
    ///     Creates a lazy-expansion node, for internal codec use only.
    /// </summary>
    internal static DataSourceNode CreateLazy(string rawText, Func<string, DataSourceNode> expander) =>
        new(rawText, expander);

    // ── Private ──

    private void EnsureNotDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private string BuildCanonicalString()
    {
        var resultMap = new Dictionary<DataSourceNode, string>();
        var order = new List<DataSourceNode>();
        var stack = new Stack<DataSourceNode>();
        stack.Push(this);

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            node.EnsureExpanded();
            order.Add(node);

            if (node._kind == DataSourceNodeKind.Map)
            {
                for (var i = node._orderedKeys.Count - 1; i >= 0; i--)
                {
                    var key = node._orderedKeys[i];
                    stack.Push(node._objectChildren[key]);
                }
            }
            else if (node._kind == DataSourceNodeKind.Array)
            {
                for (var i = node._arrayChildren.Count - 1; i >= 0; i--)
                    stack.Push(node._arrayChildren[i]);
            }
        }

        for (var i = order.Count - 1; i >= 0; i--)
        {
            var node = order[i];
            resultMap[node] = node._kind switch
            {
                DataSourceNodeKind.Map => "O{" + string.Join(",",
                    node._orderedKeys.OrderBy(k => k, StringComparer.Ordinal)
                        .Select(k => EscapeCanonicalKey(k) + "=" + resultMap[node._objectChildren[k]])) + "}",
                DataSourceNodeKind.Array => "A[" + string.Join(",",
                    node._arrayChildren.Select(c => resultMap[c])) + "]",
                DataSourceNodeKind.Text => "S\"" + EscapeCanonical(node._value) + "\"",
                DataSourceNodeKind.Number => "N" + node._value,
                DataSourceNodeKind.Bool => "B" + node._value,
                DataSourceNodeKind.Null => "X",
                _ => "X"
            };
        }

        return resultMap[this];
    }

    private static string EscapeCanonical(string? value)
    {
        if (value is null) return string.Empty;
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    // Map keys are embedded raw into the canonical encoding between structural
    // delimiters; escaping the delimiters and quote/backslash keeps the encoding
    // unambiguous for keys containing any of those characters (an unescaped
    // `=`, `,`, `{`, `}`, `[`, `]`, `"`, or `\` in a key could otherwise blend
    // into the surrounding structure).
    private static string EscapeCanonicalKey(string key)
    {
        var sb = new StringBuilder(key.Length + 8);
        foreach (var ch in key)
        {
            switch (ch)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '=': sb.Append("\\="); break;
                case ',': sb.Append("\\,"); break;
                case '{': sb.Append("\\{"); break;
                case '}': sb.Append("\\}"); break;
                case '[': sb.Append("\\["); break;
                case ']': sb.Append("\\]"); break;
                default: sb.Append(ch); break;
            }
        }

        return sb.ToString();
    }

    private void EnsureExpanded()
    {
        EnsureNotDisposed();

        if (_expanded)
            return;

        if (_expander is null || _rawText is null)
            throw new InvalidOperationException(
                "DataSourceNode cannot be expanded: this node was not created as a lazy node (CreateLazy).");
        // Expand first, then mark as expanded. If the expander throws,
        // the node stays in the lazy state and can be retried or disposed safely.
        var expanded = _expander(_rawText);
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
