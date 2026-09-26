namespace Origo.Core.Snd.Strategy;

/// <summary>
///     Conventional return values for active strategies. Subclasses of
///     <see cref="ActiveStrategyJsonBase{TInput}" /> return these as plain
///     strings; the base class serializes them so callers deserialize
///     "ok" / "err:&lt;message&gt;" reliably.
/// </summary>
public static class ActiveStrategyResults
{
    /// <summary>The conventional success marker.</summary>
    public static string Ok() => "ok";

    /// <summary>The conventional error marker, prefixed for caller parsing.</summary>
    public static string Err(string message) => $"err:{message}";
}
