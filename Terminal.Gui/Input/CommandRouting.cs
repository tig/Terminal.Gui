namespace Terminal.Gui.Input;

/// <summary>
///     Describes the routing mode of a command invocation.
///     Replaces the previous two boolean flags (<see cref="ICommandContext.IsBubblingUp"/> and <see cref="ICommandContext.IsBubblingDown"/>)
///     with a single enum that includes an explicit state for cross-boundary routing via <see cref="CommandBridge"/>.
/// </summary>
/// <remarks>
///     <para>
///         This enum enables structural recursion protection by making the routing direction explicit in the command context.
///     </para>
/// </remarks>
public enum CommandRouting
{
    /// <summary>
    ///     Direct invocation (programmatic or from this view's own bindings).
    ///     This is the default routing mode when a command is invoked directly via <see cref="View.InvokeCommand"/>.
    /// </summary>
    Direct,

    /// <summary>
    ///     Command is propagating upward through the SuperView chain.
    ///     Used during bubbling to prevent infinite loops and re-entry.
    /// </summary>
    BubblingUp,

    /// <summary>
    ///     A SuperView is dispatching downward to a specific SubView.
    ///     Used by composite controls to route commands to their primary interactive subview.
    /// </summary>
    DispatchingDown,

    /// <summary>
    ///     Command is crossing a non-containment boundary via CommandBridge.
    ///     Used when routing commands between views that are not in a SuperView/SubView relationship
    ///     (e.g., MenuItem to its detached SubMenu, MenuBarItem to its PopoverMenu).
    /// </summary>
    Bridged
}
