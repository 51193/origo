using System;
using System.Collections.Generic;

namespace Origo.Core.Save.Meta;

internal sealed class DelegateSaveMetaContributor : ISaveMetaContributor
{
    private readonly Func<SaveMetaBuildContext, IReadOnlyDictionary<string, string>> _contribute;

    public DelegateSaveMetaContributor(Func<SaveMetaBuildContext, IReadOnlyDictionary<string, string>> contribute)
    {
        ArgumentNullException.ThrowIfNull(contribute);
        _contribute = contribute;
    }

    public IReadOnlyDictionary<string, string> Contribute(in SaveMetaBuildContext context) =>
        _contribute(context);
}
