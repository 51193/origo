using System;

namespace Origo.Core.Snd.Metadata;

/// <summary>
///     Assembly-level attribute that declares which types should be stored inline
///     (zero-boxing) in <see cref="TypedData" />. The Source Generator reads this
///     attribute and generates type-specific accessor methods on the partial struct.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class SndInlineTypesAttribute : Attribute
{
    public Type[] Types { get; }

    public SndInlineTypesAttribute(params Type[] types)
    {
        Types = types;
    }
}
