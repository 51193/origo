using System;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.DataSource.Converters;

internal sealed class DataMetaDataConverter : DataSourceConverter<DataMetaData>
{
    private readonly TypedDataConverter _typedDataConverter;

    public DataMetaDataConverter(TypedDataConverter typedDataConverter)
    {
        ArgumentNullException.ThrowIfNull(typedDataConverter);
        _typedDataConverter = typedDataConverter;
    }

    public override DataMetaData Read(DataSourceNode node)
    {
        MetaDataPairsCodec.ValidateMetaNode(node, "Data metadata");

        var meta = new DataMetaData();
        MetaDataPairsCodec.ReadPairs(node, "Data metadata", meta.Pairs, _typedDataConverter.Read);
        return meta;
    }

    public override DataSourceNode Write(DataMetaData value) =>
        MetaDataPairsCodec.WritePairs(value.Pairs, _typedDataConverter.Write);
}
