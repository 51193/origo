using System.Globalization;

namespace Origo.Core.DataSource.Converters;

internal sealed class ByteArrayDataSourceConverter : DataSourceConverter<byte[]>
{
    public override byte[] Read(DataSourceNode node)
    {
        var result = new byte[node.Count];
        var i = 0;
        foreach (var element in node.Elements)
            result[i++] = element.As<byte>();
        return result;
    }

    public override DataSourceNode Write(byte[] value)
    {
        var array = DataSourceNode.CreateArray();
        foreach (var item in value)
            array.Add(DataSourceNode.CreateNumber(item.ToString(CultureInfo.InvariantCulture)));
        return array;
    }
}

internal sealed class SByteArrayDataSourceConverter : DataSourceConverter<sbyte[]>
{
    public override sbyte[] Read(DataSourceNode node)
    {
        var result = new sbyte[node.Count];
        var i = 0;
        foreach (var element in node.Elements)
            result[i++] = element.As<sbyte>();
        return result;
    }

    public override DataSourceNode Write(sbyte[] value)
    {
        var array = DataSourceNode.CreateArray();
        foreach (var item in value)
            array.Add(DataSourceNode.CreateNumber(item.ToString(CultureInfo.InvariantCulture)));
        return array;
    }
}

internal sealed class Int16ArrayDataSourceConverter : DataSourceConverter<short[]>
{
    public override short[] Read(DataSourceNode node)
    {
        var result = new short[node.Count];
        var i = 0;
        foreach (var element in node.Elements)
            result[i++] = element.As<short>();
        return result;
    }

    public override DataSourceNode Write(short[] value)
    {
        var array = DataSourceNode.CreateArray();
        foreach (var item in value)
            array.Add(DataSourceNode.CreateNumber(item.ToString(CultureInfo.InvariantCulture)));
        return array;
    }
}

internal sealed class UInt16ArrayDataSourceConverter : DataSourceConverter<ushort[]>
{
    public override ushort[] Read(DataSourceNode node)
    {
        var result = new ushort[node.Count];
        var i = 0;
        foreach (var element in node.Elements)
            result[i++] = element.As<ushort>();
        return result;
    }

    public override DataSourceNode Write(ushort[] value)
    {
        var array = DataSourceNode.CreateArray();
        foreach (var item in value)
            array.Add(DataSourceNode.CreateNumber(item.ToString(CultureInfo.InvariantCulture)));
        return array;
    }
}

internal sealed class Int32ArrayDataSourceConverter : DataSourceConverter<int[]>
{
    public override int[] Read(DataSourceNode node)
    {
        var result = new int[node.Count];
        var i = 0;
        foreach (var element in node.Elements)
            result[i++] = element.As<int>();
        return result;
    }

    public override DataSourceNode Write(int[] value)
    {
        var array = DataSourceNode.CreateArray();
        foreach (var item in value)
            array.Add(DataSourceNode.CreateNumber(item));
        return array;
    }
}

internal sealed class UInt32ArrayDataSourceConverter : DataSourceConverter<uint[]>
{
    public override uint[] Read(DataSourceNode node)
    {
        var result = new uint[node.Count];
        var i = 0;
        foreach (var element in node.Elements)
            result[i++] = element.As<uint>();
        return result;
    }

    public override DataSourceNode Write(uint[] value)
    {
        var array = DataSourceNode.CreateArray();
        foreach (var item in value)
            array.Add(DataSourceNode.CreateNumber(item.ToString(CultureInfo.InvariantCulture)));
        return array;
    }
}

internal sealed class Int64ArrayDataSourceConverter : DataSourceConverter<long[]>
{
    public override long[] Read(DataSourceNode node)
    {
        var result = new long[node.Count];
        var i = 0;
        foreach (var element in node.Elements)
            result[i++] = element.As<long>();
        return result;
    }

    public override DataSourceNode Write(long[] value)
    {
        var array = DataSourceNode.CreateArray();
        foreach (var item in value)
            array.Add(DataSourceNode.CreateNumber(item));
        return array;
    }
}

internal sealed class UInt64ArrayDataSourceConverter : DataSourceConverter<ulong[]>
{
    public override ulong[] Read(DataSourceNode node)
    {
        var result = new ulong[node.Count];
        var i = 0;
        foreach (var element in node.Elements)
            result[i++] = element.As<ulong>();
        return result;
    }

    public override DataSourceNode Write(ulong[] value)
    {
        var array = DataSourceNode.CreateArray();
        foreach (var item in value)
            array.Add(DataSourceNode.CreateNumber(item.ToString(CultureInfo.InvariantCulture)));
        return array;
    }
}

internal sealed class SingleArrayDataSourceConverter : DataSourceConverter<float[]>
{
    public override float[] Read(DataSourceNode node)
    {
        var result = new float[node.Count];
        var i = 0;
        foreach (var element in node.Elements)
            result[i++] = element.As<float>();
        return result;
    }

    public override DataSourceNode Write(float[] value)
    {
        var array = DataSourceNode.CreateArray();
        foreach (var item in value)
            array.Add(DataSourceNode.CreateNumber(item));
        return array;
    }
}

internal sealed class DoubleArrayDataSourceConverter : DataSourceConverter<double[]>
{
    public override double[] Read(DataSourceNode node)
    {
        var result = new double[node.Count];
        var i = 0;
        foreach (var element in node.Elements)
            result[i++] = element.As<double>();
        return result;
    }

    public override DataSourceNode Write(double[] value)
    {
        var array = DataSourceNode.CreateArray();
        foreach (var item in value)
            array.Add(DataSourceNode.CreateNumber(item));
        return array;
    }
}

internal sealed class DecimalArrayDataSourceConverter : DataSourceConverter<decimal[]>
{
    public override decimal[] Read(DataSourceNode node)
    {
        var result = new decimal[node.Count];
        var i = 0;
        foreach (var element in node.Elements)
            result[i++] = element.As<decimal>();
        return result;
    }

    public override DataSourceNode Write(decimal[] value)
    {
        var array = DataSourceNode.CreateArray();
        foreach (var item in value)
            array.Add(DataSourceNode.CreateNumber(item.ToString(CultureInfo.InvariantCulture)));
        return array;
    }
}

internal sealed class BooleanArrayDataSourceConverter : DataSourceConverter<bool[]>
{
    public override bool[] Read(DataSourceNode node)
    {
        var result = new bool[node.Count];
        var i = 0;
        foreach (var element in node.Elements)
            result[i++] = element.As<bool>();
        return result;
    }

    public override DataSourceNode Write(bool[] value)
    {
        var array = DataSourceNode.CreateArray();
        foreach (var item in value)
            array.Add(DataSourceNode.CreateBoolean(item));
        return array;
    }
}

internal sealed class CharArrayDataSourceConverter : DataSourceConverter<char[]>
{
    public override char[] Read(DataSourceNode node)
    {
        var result = new char[node.Count];
        var i = 0;
        foreach (var element in node.Elements)
            result[i++] = element.AsChar();
        return result;
    }

    public override DataSourceNode Write(char[] value)
    {
        var array = DataSourceNode.CreateArray();
        foreach (var item in value)
            array.Add(DataSourceNode.CreateString(item.ToString()));
        return array;
    }
}

internal sealed class StringArrayDataSourceConverter : DataSourceConverter<string[]>
{
    public override string[] Read(DataSourceNode node)
    {
        var result = new string[node.Count];
        var i = 0;
        foreach (var element in node.Elements)
            result[i++] = element.AsString();
        return result;
    }

    public override DataSourceNode Write(string[] value)
    {
        var array = DataSourceNode.CreateArray();
        foreach (var item in value)
            array.Add(DataSourceNode.CreateString(item));
        return array;
    }
}
