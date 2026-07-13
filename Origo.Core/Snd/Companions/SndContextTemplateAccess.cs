using Origo.Core.Abstractions.Snd;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Snd.Companions;

internal sealed class SndContextTemplateAccess(SndContext owner) : ISndTemplateAccess
{
    public SndMetaData CloneTemplate(string templateKey, string? overrideName = null)
    {
        var template = owner.Runtime.SndWorld.ResolveTemplate(templateKey);
        var cloned = SndWorld.CloneMetaData(template);
        if (!string.IsNullOrWhiteSpace(overrideName))
            cloned.Name = overrideName;
        return cloned;
    }
}
