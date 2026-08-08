using System.Collections.Generic;

namespace Origo.Core.Save.Meta;

/// <summary>
///     Contributes key-value pairs to the display <c>meta.map</c> during each
///     actual save execution corresponding to <c>RequestSaveGame</c>.
///     Each contributor returns its own dictionary of produced key-value
///     pairs (must not modify others' contributions); the framework merger
///     combines them all. Multiple contributors execute in registration order;
///     same-name keys from later contributors override earlier ones. Finally,
///     <c>customMeta</c> provided by the caller performs key-level overrides
///     once more.
/// </summary>
public interface ISaveMetaContributor
{
    /// <summary>
    ///     Produces this contributor's key-value pairs for the save's display
    ///     metadata. Implementations must return a fresh dictionary and must
    ///     not mutate the <paramref name="context" /> beyond reading it.
    /// </summary>
    IReadOnlyDictionary<string, string> Contribute(in SaveMetaBuildContext context);
}
