using System;
using System.Collections.Generic;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.DataSource.Converters;

internal sealed class SndMetaDataListConverter : DataSourceConverter<IReadOnlyList<SndMetaData>>
{
    private readonly SndMetaDataConverter _sndMetaDataConverter;

    public SndMetaDataListConverter(SndMetaDataConverter sndMetaDataConverter)
    {
        ArgumentNullException.ThrowIfNull(sndMetaDataConverter);
        _sndMetaDataConverter = sndMetaDataConverter;
    }

    public override IReadOnlyList<SndMetaData> Read(DataSourceNode node)
    {
        var list = new List<SndMetaData>();
        foreach (var element in node.Elements)
            list.Add(_sndMetaDataConverter.Read(element));
        return list;
    }

    public override DataSourceNode Write(IReadOnlyList<SndMetaData> value)
    {
        var array = DataSourceNode.CreateArray();
        foreach (var item in value)
            array.Add(_sndMetaDataConverter.Write(item));
        return array;
    }
}
