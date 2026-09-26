namespace Origo.Core.Runtime.Console;

/// <summary>
///     Shared user-facing message constants for console command handling.
///     Both production handlers and their tests reference these constants so
///     assertions do not hard-code message literals.
/// </summary>
internal static class ConsoleMessages
{
    /// <summary>Emitted when a command receives too few or too many positional arguments.</summary>
    public const string InvalidArgumentCount = "Invalid argument count.";
}
