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
    /// <summary>The types registered as inline-storable in <see cref="TypedData" />.</summary>
    public Type[] Types { get; }

    /// <summary>The starting kind value assigned to the declared types.</summary>
    public int StartKind { get; }

    /// <summary>Declares inline types starting at kind 1.</summary>
    public SndInlineTypesAttribute(params Type[] types)
    {
        Types = types;
        StartKind = 1;
    }

    /// <summary>Declares inline types starting at the given kind value.</summary>
    public SndInlineTypesAttribute(int startKind, params Type[] types)
    {
        Types = types;
        StartKind = startKind;
    }
}
