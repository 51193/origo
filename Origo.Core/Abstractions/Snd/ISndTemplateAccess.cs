using Origo.Core.Snd.Metadata;

namespace Origo.Core.Abstractions.Snd;

/// <summary>
///     提供模板克隆能力。通过模板键获取元数据深拷贝，便于按模板批量创建实体。
/// </summary>
public interface ISndTemplateAccess
{
    /// <summary>克隆指定模板并可选地覆盖名称，便于按模板批量创建实体。</summary>
    SndMetaData CloneTemplate(string templateKey, string? overrideName = null);
}
