using System.Globalization;

namespace Origo.Core.DataSource.Converters;

internal sealed class StringDataSourceConverter : DataSourceConverter<string>
{
    public override string Read(DataSourceNode node) => node.AsString();

    public override DataSourceNode Write(string value) => DataSourceNode.CreateString(value);
}

internal sealed class ByteDataSourceConverter : DataSourceConverter<byte>
{
    public override byte Read(DataSourceNode node) => node.AsByte();

    public override DataSourceNode Write(byte value) =>
        DataSourceNode.CreateNumber(value.ToString(CultureInfo.InvariantCulture));
}

internal sealed class SByteDataSourceConverter : DataSourceConverter<sbyte>
{
    public override sbyte Read(DataSourceNode node) => node.AsSByte();

    public override DataSourceNode Write(sbyte value) =>
        DataSourceNode.CreateNumber(value.ToString(CultureInfo.InvariantCulture));
}

internal sealed class Int16DataSourceConverter : DataSourceConverter<short>
{
    public override short Read(DataSourceNode node) => node.AsShort();

    public override DataSourceNode Write(short value) =>
        DataSourceNode.CreateNumber(value.ToString(CultureInfo.InvariantCulture));
}

internal sealed class UInt16DataSourceConverter : DataSourceConverter<ushort>
{
    public override ushort Read(DataSourceNode node) => node.AsUShort();

    public override DataSourceNode Write(ushort value) =>
        DataSourceNode.CreateNumber(value.ToString(CultureInfo.InvariantCulture));
}

internal sealed class Int32DataSourceConverter : DataSourceConverter<int>
{
    public override int Read(DataSourceNode node) => node.AsInt();

    public override DataSourceNode Write(int value) => DataSourceNode.CreateNumber(value);
}

internal sealed class UInt32DataSourceConverter : DataSourceConverter<uint>
{
    public override uint Read(DataSourceNode node) => node.AsUInt();

    public override DataSourceNode Write(uint value) =>
        DataSourceNode.CreateNumber(value.ToString(CultureInfo.InvariantCulture));
}

internal sealed class Int64DataSourceConverter : DataSourceConverter<long>
{
    public override long Read(DataSourceNode node) => node.AsLong();

    public override DataSourceNode Write(long value) => DataSourceNode.CreateNumber(value);
}

internal sealed class UInt64DataSourceConverter : DataSourceConverter<ulong>
{
    public override ulong Read(DataSourceNode node) => node.AsULong();

    public override DataSourceNode Write(ulong value) =>
        DataSourceNode.CreateNumber(value.ToString(CultureInfo.InvariantCulture));
}

internal sealed class SingleDataSourceConverter : DataSourceConverter<float>
{
    public override float Read(DataSourceNode node) => node.AsFloat();

    public override DataSourceNode Write(float value) => DataSourceNode.CreateNumber(value);
}

internal sealed class DoubleDataSourceConverter : DataSourceConverter<double>
{
    public override double Read(DataSourceNode node) => node.AsDouble();

    public override DataSourceNode Write(double value) => DataSourceNode.CreateNumber(value);
}

internal sealed class DecimalDataSourceConverter : DataSourceConverter<decimal>
{
    public override decimal Read(DataSourceNode node) => node.AsDecimal();

    public override DataSourceNode Write(decimal value) =>
        DataSourceNode.CreateNumber(value.ToString(CultureInfo.InvariantCulture));
}

internal sealed class CharDataSourceConverter : DataSourceConverter<char>
{
    public override char Read(DataSourceNode node) => node.AsChar();

    public override DataSourceNode Write(char value) => DataSourceNode.CreateString(value.ToString());
}

internal sealed class BooleanDataSourceConverter : DataSourceConverter<bool>
{
    public override bool Read(DataSourceNode node) => node.AsBool();

    public override DataSourceNode Write(bool value) => DataSourceNode.CreateBoolean(value);
}
