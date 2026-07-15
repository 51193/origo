using Origo.Core.Snd.Metadata;

namespace Origo.Core.Abstractions.Snd;

/// <summary>
///     Template cloning capability. Obtains a deep copy of metadata by template
///     key, making it easy to batch-create entities from templates.
/// </summary>
public interface ISndTemplateAccess
{
    /// <summary>Clone a template and optionally override the name, for batch entity creation.</summary>
    SndMetaData CloneTemplate(string templateKey, string? overrideName = null);
}
