using Origo.Core.Abstractions.Snd;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Snd.Companions;

/// <summary>Template cloning and metadata resolution for <see cref="SndContext" />.</summary>
internal sealed class SndContextTemplateAccess(SndContext owner) : ISndTemplateAccess
{
    public SndMetaData CloneTemplate(string templateKey, string? overrideName = null)
    {
        var cloned = owner.Runtime.SndWorld.ResolveTemplate(templateKey);
        if (!string.IsNullOrWhiteSpace(overrideName))
            cloned.Name = overrideName;
        return cloned;
    }
}
