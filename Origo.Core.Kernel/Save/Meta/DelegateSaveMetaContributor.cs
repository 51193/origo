using System;
using System.Collections.Generic;

namespace Origo.Core.Save.Meta;

/// <summary>
///     Adapts a delegate to the <see cref="ISaveMetaContributor" /> contract
///     so callers can register a metadata contributor without declaring a
///     dedicated type.
/// </summary>
internal sealed class DelegateSaveMetaContributor : ISaveMetaContributor
{
    private readonly Func<SaveMetaBuildContext, IReadOnlyDictionary<string, string>> _contribute;

    public DelegateSaveMetaContributor(Func<SaveMetaBuildContext, IReadOnlyDictionary<string, string>> contribute)
    {
        ArgumentNullException.ThrowIfNull(contribute);
        _contribute = contribute;
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string> Contribute(in SaveMetaBuildContext context) =>
        _contribute(context);
}
