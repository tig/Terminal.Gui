namespace Terminal.Gui.Input;

/// <summary>
///     Describes the outcome of a command handler.
///     Replaces the previous three-valued <see langword="bool"/>? return type (null = not found, false = not handled, true = handled).
/// </summary>
/// <remarks>
///     <para>
///         This enum provides explicit semantics for command handling outcomes, making the routing logic self-documenting.
///     </para>
/// </remarks>
public enum CommandOutcome
{
    /// <summary>
    ///     The command was not handled; routing continues to the next handler or bubbles up the view hierarchy.
    /// </summary>
    NotHandled,

    /// <summary>
    ///     The command was handled successfully; routing stops immediately.
    /// </summary>
    HandledStop,

    /// <summary>
    ///     The command was handled but routing may continue (notification semantics).
    ///     This allows multiple handlers to observe the same command without blocking each other.
    /// </summary>
    HandledContinue
}
