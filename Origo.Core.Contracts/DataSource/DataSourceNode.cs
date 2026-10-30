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
    // Canonical hashing must reject unpaired UTF-16 surrogates instead of
    // letting a replacement-fallback encoder collapse them into U+FFFD: that
    // collapse would make distinct node trees hash-equal and hide real content
    // changes from the save idempotency check.
    private static readonly UTF8Encoding _strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

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
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the node carries no value, or when a floating-point
    ///     literal parses to a non-finite value (a JSON number literal such as
    ///     <c>1e999</c> overflows the target type; JSON has no NaN or infinity
    ///     literal). The stored raw text is preserved verbatim and number nodes
    ///     are encoded verbatim, so the overflow surfaces only in
    ///     <c>As&lt;float&gt;</c>/<c>As&lt;double&gt;</c> reads (integer and
    ///     decimal reads report their own parse/overflow exceptions).
    /// </exception>
    /// <exception cref="FormatException">
    ///     Thrown when the stored raw text is not a valid literal for
    ///     <typeparamref name="T" />. Numeric reads parse the stored raw text
    ///     regardless of the node kind, so a numeric text node parses and only
    ///     non-numeric text (including a numeric read from a bool node, or
    ///     <c>As&lt;int&gt;</c> on <c>"3.14"</c>) throws.
    /// </exception>
    /// <exception cref="OverflowException">
    ///     Thrown when the stored integer literal does not fit the requested
    ///     integer type.
    /// </exception>
    /// <exception cref="NotSupportedException">
    ///     Thrown when <typeparamref name="T" /> is not one of the supported
    ///     primitive types.
    /// </exception>
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
        if (typeof(T) == typeof(float)) return (T)(object)ParseFiniteSingle(_value!);
        if (typeof(T) == typeof(double)) return (T)(object)ParseFiniteDouble(_value!);
        if (typeof(T) == typeof(decimal)) return (T)(object)decimal.Parse(_value!, CultureInfo.InvariantCulture);
        if (typeof(T) == typeof(bool)) return (T)(object)bool.Parse(_value!);

        throw new NotSupportedException(
            $"As<{typeof(T).Name}> is not supported. " +
            "Only primitive types are supported. " +
            "For complex types, use DataSourceConverterRegistry.Read<T>.");
    }

    /// <summary>
    ///     Parses a stored floating-point literal and rejects non-finite
    ///     results. <c>float.Parse</c> returns
    ///     ±infinity for an overflowing literal instead of throwing, while JSON
    ///     has no NaN or infinity literal; accepting the value here would
    ///     silently propagate a value that the save writer rejects.
    /// </summary>
    private static float ParseFiniteSingle(string value)
    {
        var parsed = float.Parse(value, CultureInfo.InvariantCulture);
        return float.IsFinite(parsed)
            ? parsed
            : throw new InvalidOperationException(
                $"Cannot parse value '{value}' as Single: the literal overflows the type " +
                "and JSON number nodes cannot represent a non-finite value.");
    }

    /// <summary>Double counterpart of <see cref="ParseFiniteSingle" />.</summary>
    private static double ParseFiniteDouble(string value)
    {
        var parsed = double.Parse(value, CultureInfo.InvariantCulture);
        return double.IsFinite(parsed)
            ? parsed
            : throw new InvalidOperationException(
                $"Cannot parse value '{value}' as Double: the literal overflows the type " +
                "and JSON number nodes cannot represent a non-finite value.");
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
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the graph contains a cycle, or when a map key or text
    ///     value contains an unpaired UTF-16 surrogate. The canonical text
    ///     cannot be encoded losslessly in that case (a replacement-fallback
    ///     encoder would collapse distinct strings into U+FFFD), so hashing it
    ///     would let two different trees share one digest.
    /// </exception>
    public string ComputeSha256Hash()
    {
        EnsureExpanded();
        var canonical = BuildCanonicalString();
        Utf16Text.ThrowIfNotWellFormed(canonical, "DataSourceNode canonical text");
        var bytes = _strictUtf8.GetBytes(canonical);
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
        var parentUseCount = new Dictionary<DataSourceNode, int>();
        var order = new List<DataSourceNode>();
        var active = new HashSet<DataSourceNode>();
        var done = new HashSet<DataSourceNode>();
        var stack = new Stack<(DataSourceNode Node, bool Expanded)>();
        stack.Push((this, false));

        while (stack.Count > 0)
        {
            var (node, expanded) = stack.Pop();
            if (expanded)
            {
                active.Remove(node);
                if (done.Add(node))
                    order.Add(node);
                continue;
            }

            if (done.Contains(node))
                continue;
            if (!active.Add(node))
                throw new InvalidOperationException(
                    "DataSourceNode graph contains a cycle; a node tree must be acyclic. " +
                    "Cycle detection stops canonical hashing instead of looping or overflowing.");

            node.EnsureExpanded();
            stack.Push((node, true));
            if (node._kind == DataSourceNodeKind.Map)
            {
                for (var i = node._orderedKeys.Count - 1; i >= 0; i--)
                {
                    var key = node._orderedKeys[i];
                    var child = node._objectChildren[key];
                    parentUseCount[child] = parentUseCount.GetValueOrDefault(child) + 1;
                    stack.Push((child, false));
                }
            }
            else if (node._kind == DataSourceNodeKind.Array)
            {
                for (var i = node._arrayChildren.Count - 1; i >= 0; i--)
                {
                    var child = node._arrayChildren[i];
                    parentUseCount[child] = parentUseCount.GetValueOrDefault(child) + 1;
                    stack.Push((child, false));
                }
            }
        }

        // Post-order: every child is hashed before its parent. A shared DAG
        // child is hashed once and reused; only a cycle is rejected.
        foreach (var node in order)
        {
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

            // Release child strings once their last parent has consumed them.
            // Keeping every postorder string alive is quadratic in depth for a
            // linear tree (the 10k-depth regression otherwise peaks near 1.6 GB).
            if (node._kind == DataSourceNodeKind.Map)
            {
                foreach (var key in node._orderedKeys)
                    ReleaseChild(node._objectChildren[key], resultMap, parentUseCount);
            }
            else if (node._kind == DataSourceNodeKind.Array)
            {
                foreach (var child in node._arrayChildren)
                    ReleaseChild(child, resultMap, parentUseCount);
            }
        }

        return resultMap[this];
    }

    private static void ReleaseChild(
        DataSourceNode child,
        Dictionary<DataSourceNode, string> resultMap,
        Dictionary<DataSourceNode, int> parentUseCount)
    {
        var remaining = parentUseCount[child] - 1;
        parentUseCount[child] = remaining;
        if (remaining == 0)
            resultMap.Remove(child);
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

/// <summary>
///     UTF-16 well-formedness guard shared by the canonical hashing of
///     <see cref="DataSourceNode" /> and the kernel text codecs. JSON strings
///     and the line-based <c>.map</c> format cannot round-trip an unpaired
///     surrogate: the BCL encoders replace it with U+FFFD, so a save would
///     silently persist a different value (and two distinct trees would hash
///     equally). Callers reject the text before encoding/hashing it.
/// </summary>
internal static class Utf16Text
{
    /// <summary>
    ///     Returns true when the string contains no unpaired UTF-16 surrogate
    ///     (high surrogate must be followed by a low surrogate, and vice versa).
    /// </summary>
    internal static bool IsWellFormed(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (!char.IsSurrogate(character))
                continue;
            if (char.IsHighSurrogate(character)
                && index + 1 < value.Length
                && char.IsLowSurrogate(value[index + 1]))
            {
                index++;
                continue;
            }

            return false;
        }

        return true;
    }

    /// <summary>
    ///     Throws an actionable <see cref="InvalidOperationException" /> when
    ///     the string is not well-formed UTF-16; <paramref name="context" />
    ///     names the value being validated (for example the file operation).
    /// </summary>
    internal static void ThrowIfNotWellFormed(string value, string context)
    {
        if (IsWellFormed(value))
            return;

        throw new InvalidOperationException(
            $"{context} contains an unpaired UTF-16 surrogate. The value cannot be " +
            "encoded losslessly; split or repair the text instead of writing a " +
            "replacement character.");
    }
}
