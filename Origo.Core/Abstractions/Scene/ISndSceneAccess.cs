using System.Collections.Generic;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Abstractions.Scene;

/// <summary>
///     抽象 SND 场景访问能力，供 Core 层编排存读档流程。
/// </summary>
public interface ISndSceneAccess
{
    IReadOnlyList<SndMetaData> SerializeMetaList();

    void LoadFromMetaList(IEnumerable<SndMetaData> metaList);

    void ClearAll();
}
