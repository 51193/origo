using System;
using System.Collections.Generic;

namespace Origo.Core.Save.Meta;

/// <summary>
///     合并已注册贡献者与存档入参：先贡献者（按顺序，同名键后者覆盖前者），再 <paramref name="overrides" />。
/// </summary>
internal static class SaveMetaMerger
{
    /// <summary>
    ///     返回合并后的字典；若无任何键则返回 <c>null</c>，与未提供自定义 meta 的语义一致。
    /// </summary>
    public static IReadOnlyDictionary<string, string>? Merge(
        IReadOnlyList<ISaveMetaContributor> contributors,
        in SaveMetaBuildContext context,
        IReadOnlyDictionary<string, string>? overrides)
    {
        ArgumentNullException.ThrowIfNull(contributors);

        var merged = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var c in contributors)
        {
            ArgumentNullException.ThrowIfNull(c);
            c.Contribute(in context, merged);
        }

        ApplyOverrides(merged, overrides);
        return merged.Count == 0 ? null : merged;
    }

    private static void ApplyOverrides(
        Dictionary<string, string> merged,
        IReadOnlyDictionary<string, string>? overrides)
    {
        if (overrides is null)
            return;

        foreach (var kv in overrides)
        {
            if (string.IsNullOrEmpty(kv.Key))
                continue;
            if (kv.Value is null)
                continue;
            merged[kv.Key] = kv.Value;
        }
    }
}
