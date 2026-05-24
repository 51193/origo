using System.Collections.Generic;

namespace Origo.Core.Save.Meta;

/// <summary>
///     在每次 <c>RequestSaveGame</c> 对应的实际保存执行时向展示用 <c>meta.map</c> 贡献键值；
///     多个贡献者按注册顺序执行，同名键后者覆盖前者；
///     最后由调用方传入的 <c>customMeta</c> 再次键级覆盖。
/// </summary>
public interface ISaveMetaContributor
{
    void Contribute(in SaveMetaBuildContext context, IDictionary<string, string> target);
}
