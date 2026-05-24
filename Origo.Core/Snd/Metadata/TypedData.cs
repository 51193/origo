using System;

namespace Origo.Core.Snd.Metadata;

/// <summary>
///     保存一条带有类型信息的 SND 数据。
/// </summary>
public sealed class TypedData
{
    public TypedData(Type dataType, object? data)
    {
        DataType = dataType;
        Data = data;
    }

    public Type DataType { get; }

    public object? Data { get; }
}
