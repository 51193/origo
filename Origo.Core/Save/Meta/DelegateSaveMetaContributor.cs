using System;
using System.Collections.Generic;

namespace Origo.Core.Save.Meta;

internal sealed class DelegateSaveMetaContributor : ISaveMetaContributor
{
    private readonly Action<SaveMetaBuildContext, IDictionary<string, string>> _contribute;

    public DelegateSaveMetaContributor(Action<SaveMetaBuildContext, IDictionary<string, string>> contribute)
    {
        ArgumentNullException.ThrowIfNull(contribute);
        _contribute = contribute;
    }

    public void Contribute(in SaveMetaBuildContext context, IDictionary<string, string> target) =>
        _contribute(context, target);
}
