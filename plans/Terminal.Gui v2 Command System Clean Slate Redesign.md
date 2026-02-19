# Terminal.Gui v2 Command System Clean Slate Redesign

## Problem Statement

The current v2 command system works, but it is hard to reason about and hard to evolve safely. Behavior is distributed across:

- `View.Command` default handlers
- View-specific overrides (`Shortcut`, `OptionSelector`, `FlagSelector`, `MenuBar`, `PopoverMenu`, etc.)
- Keyboard and mouse binding layers
- Special-case routing (`CommandsToBubbleUp`, `DefaultAcceptView`, `BubbleDown`, boundary bridging)

The result is high coupling, subtle ordering dependencies, and fragile composite-view behavior.

This redesign is clean-slate. v2 is alpha, so compatibility is negotiable. Correctness, clarity, and architectural durability take priority.

## Requirements

### 1. Core Intent Model

R-001. The system shall define a small, explicit set of command intents with strict semantics (for example: `Activate`, `Accept`, `HotKey`, navigation, edit, system).

R-002. Each intent shall have a normative contract: what state changes are allowed, what side effects are allowed, and what is forbidden.

R-003. Intent semantics shall not depend on input source by default. Source-specific behavior must be explicit policy.

R-004. `Activate` and `Accept` shall remain semantically distinct. `Accept` must never implicitly become `Activate` unless a control explicitly declares that policy.

R-005. `HotKey` shall be treated as an input trigger policy, not a special-case side-effect bundle hidden inside control code.

### 2. Command Invocation Contract

R-006. Replace ambiguous `bool?` command results with a strongly typed outcome model (for example: `NotHandled`, `HandledContinue`, `HandledStop`, `Error`).

R-007. Command invocation context shall be immutable for the full route.

R-008. Context shall contain normalized origin metadata: source view identity, input origin, gesture details, timestamp, and routing flags.

R-009. Context shall preserve source and binding provenance end-to-end; no silent source rewrites.

R-010. Command handlers shall not mutate routing metadata directly. Routing decisions must be via explicit return/outcome.

### 3. Routing and Propagation

R-011. Introduce a first-class command router with deterministic phases (for example: preview, execute, commit, post).

R-012. Routing shall support explicit policies: local-only, bubble-up, delegate-down, broadcast, bridge-across-boundary.

R-013. Recursion protection shall be structural (route graph + visited markers), not ad-hoc boolean flags passed between controls.

R-014. A command route shall be inspectable and replayable in tests.

R-015. Out-of-hierarchy boundaries (for example Popover/MenuBar ownership boundaries) shall use explicit bridge nodes in the route model.

R-016. Parent/ancestor participation shall be opt-in by policy, with no hidden defaults.

R-017. Consumption behavior shall be explicit: notification vs consumption must be represented in outcome, not inferred from side effects.

### 4. Input Normalization (Keyboard and Mouse)

R-018. Keyboard and mouse shall both feed a unified gesture-to-intent pipeline before control dispatch.

R-019. Default mouse activation behavior must preserve current tested UX invariant: activation on release, not press, with release-outside cancellation support.

R-020. Mouse grab semantics shall remain supported but must be represented as routing policy, not scattered per-control special logic.

R-021. Key handling order must be deterministic and documented from application scope to focused view scope to fallback scope.

R-022. Hotkey resolution shall be deterministic across focus scopes and app scope, with clear precedence rules.

### 5. Composite and Wrapper Controls

R-023. Composite controls (for example `Shortcut`, selectors, menus) shall declare command delegation strategy declaratively.

R-024. A composite control shall be able to guarantee "single user gesture => single state mutation" without control-specific hacks.

R-025. Command forwarding from wrapper views to internal views shall be a router primitive, not control-local custom logic.

R-026. Event ordering for composite controls shall be deterministic and testable.

R-027. Composite controls shall not need to infer behavior from raw binding source identity to avoid duplicate activation.

### 6. Accept Target and Default Action Model

R-028. Replace implicit `DefaultAcceptView` redirection heuristics with an explicit default-action policy.

R-029. Default action dispatch shall not create dual-path execution (no duplicate accept chains).

R-030. Accept-target behavior shall be modeled uniformly for direct invokes, bubbled invokes, and bridged invokes.

### 7. API and Configuration

R-031. Public API shall cleanly separate:
- command definition
- gesture binding
- routing policy
- execution handlers

R-032. Runtime rebinding must support app-level, view-level, and hotkey scopes without semantic ambiguity.

R-033. Command configuration shall support static defaults and runtime overrides with deterministic merge precedence.

R-034. The system shall provide a compatibility adapter layer for legacy `AddCommand`/`KeyBindings`/`MouseBindings` during migration.

### 8. Observability and Diagnostics

R-035. Add command tracing with per-invocation route logs: source, route nodes, handler outcomes, and stop reason.

R-036. Diagnostics shall include duplicate-dispatch detection and route-cycle detection.

R-037. Traces shall be usable in unit tests and integration tests without requiring terminal UI rendering.

### 9. Reliability and Performance

R-038. Command routing must be allocation-conscious and stable under high-frequency input.

R-039. Router behavior must be thread-safe with clear threading rules for invocation and UI mutation.

R-040. Error handling shall isolate handler failures and emit structured diagnostics without corrupting route state.

### 10. Testability and Verification Requirements

R-041. All command behavior must be verifiable with deterministic tests using virtual time and input injection.

R-042. Golden-path tests must cover:
- keyboard gesture to intent mapping
- mouse gesture to intent mapping
- bubbling/delegation/bridging
- composite controls with internal command views
- default-action dispatch

R-043. Regression tests must preserve key currently-tested invariants unless intentionally replaced by new spec.

R-044. Skipped command-propagation tests in this branch/PR shall either pass under the new architecture or be replaced with equivalent, clearer tests.

### 11. Migration and Rollout (Alpha-Appropriate)

R-045. Backward compatibility is not required by default, but migration paths must be explicit and bounded.

R-046. Deliver migration docs mapping old concepts (`CommandsToBubbleUp`, `BubbleDown`, `DefaultAcceptView`, legacy return semantics) to new concepts.

R-047. Ship behind a feature flag for staged adoption during alpha, with side-by-side comparison tooling.

R-048. Final cutover requires parity on prioritized behaviors plus measurable reduction in command-system complexity.

### 12. Invocation Threading and Asynchrony (Decision Locked)

R-049. `InvokeCommand` shall remain synchronous and UI-thread-affine; command completion and handled outcome must be known before the current input route continues.

R-050. `InvokeCommand` shall not internally dispatch via `app.Invoke()` or any deferred queueing mechanism.

R-051. Cross-thread producers shall use an explicit posted-command API (for example `PostCommand`) that is semantically distinct from `InvokeCommand`.

R-052. Posted commands shall execute at an explicit main-loop drain point and shall not participate in the initiating key/mouse route's synchronous handled decision.

### 13. Behavior Parity Requirements Derived from Key Controls

#### Dialog<TResult> and Dialog

R-053. `Dialog<TResult>` shall bubble `Command.Accept` from descendants to the dialog-level accept pipeline.

R-054. `Dialog<TResult>.OnAccepting` semantics shall explicitly support "source-driven stop": when an accept source exists, the dialog requests stop.

R-055. Non-default `IAcceptTarget` sources (for example non-default buttons) shall have an explicit handled path at accept-preview time.

R-056. `AddButton` semantics shall preserve deterministic default-action assignment: the last added button becomes default.

R-057. The default-action model shall preserve dialog button index determinism (`Buttons.IndexOf`) for result mapping.

R-058. `Dialog` shall set `Result` from button source on activation path (button-driven `Activate`).

R-059. `Dialog` shall set `Result` before base accept handling for non-default dialog-button sources.

R-060. `Dialog` shall set `Result` to the default-button index when accept comes from non-button sources and default action is chosen.

R-061. The design shall preserve explicit cancel-style semantics where non-default button accept can stop the dialog without requiring container `Accepted` side effects.

R-062. Enter/Accept behavior in modal dialogs shall remain deterministic with respect to focused button and resulting `Result`.

#### Prompt<TView, TResult>

R-063. `Prompt<TView, TResult>` shall preserve default button set/order semantics: Cancel then Ok, with Ok as default.

R-064. Prompt cancellation shall keep result null/unset in runnable terms.

R-065. On acceptance, result extraction precedence shall be explicit and deterministic: `ResultExtractor`, then `IValue<TResult>`, then `string` fallback from wrapped view text.

R-066. Prompt acceptance shall reuse dialog accept lifecycle (same stop and accept routing model), not a parallel special path.

#### SelectorBase, OptionSelector, FlagSelector

R-067. Selector controls shall preserve "Enter means Activate + Accept" semantics for focused item paths (keyboard and direct accept invocation).

R-068. Selector accept handling shall preserve double-click policy (`DoubleClickAccepts`) as an explicit command policy.

R-069. Selector navigation commands (`Up/Down/Left/Right`) shall remain orientation-aware and tab-behavior-aware.

R-070. `OptionSelector` shall preserve single-selection invariants (at most one checked option at a time).

R-071. `OptionSelector` shall consume bubbled-up checkbox activation and apply exactly one value mutation for the gesture.

R-072. `OptionSelector` direct activation (non-bubbling invoke path) shall still apply value change deterministically.

R-073. `OptionSelector` shall preserve space-key behavior: active item cycles to next; inactive focused item becomes active.

R-074. `OptionSelector` hotkey behavior shall preserve focus restore/advance-active semantics without implicit accept.

R-075. `FlagSelector` shall preserve focused-hotkey no-op semantics (no value mutation, no activate side effect).

R-076. `FlagSelector` not-focused hotkey path shall restore focus while suppressing unintended follow-on activate/toggle.

R-077. `FlagSelector` bubbled-up checkbox activation shall toggle exactly once and be consumed to prevent duplicate toggles.

R-078. `FlagSelector` direct activation shall delegate to focused checkbox activation path (bubble-down) when applicable.

R-079. `FlagSelector` shall preserve no-accept-on-hotkey behavior.

R-080. `FlagSelector` shall preserve none-flag semantics (bit value `0`) including "none active implies all other flags inactive" behavior.

R-081. `FlagSelector` value sync between checkbox state and aggregate bitset shall be deterministic and non-recursive.

R-082. `FlagSelector` double-click path shall preserve combined behavior where toggle and accept occur without duplicate mutations.

#### Shortcut

R-083. `Shortcut` shall preserve bubbling contract for both `Activate` and `Accept` from command subviews.

R-084. `Shortcut` direct `InvokeCommand(Command.Activate)` without binding context shall not implicitly forward to `CommandView`.

R-085. `Shortcut` direct `InvokeCommand(Command.Accept)` without binding context shall not implicitly force command-view activate side effects.

R-086. `Shortcut` shall preserve source-aware forwarding rules: bubble-down to `CommandView` only for qualifying user-origin bindings outside `CommandView`.

R-087. `Shortcut` bubbled-up activate path shall preserve deferred activation completion semantics tied to `CommandView.Activated`.

R-088. `Shortcut` shall preserve event ordering guarantees where command-view state mutation completes before shortcut-level activated observers consume state.

R-089. `Shortcut` shall preserve no-duplicate-activation behavior for embedded complex command views (`FlagSelector`, `OptionSelector`).

R-090. `Shortcut` shall preserve strict separation of `Accept` and `Activate`; accept must not imply activate.

R-091. `Shortcut` hotkey command path shall preserve `HandlingHotKey` followed by activate behavior.

R-092. `Shortcut` action delegate semantics shall remain explicit: action invoked on both activate and accept completion paths.

R-093. `Shortcut` target dispatch semantics shall remain explicit: when configured with `TargetView` + bound `Command`, dispatch command on both activate and accept completion paths.

R-094. `Shortcut` shall preserve app-level fallback dispatch semantics when no `TargetView` exists and key/command binding is valid.

R-095. `Shortcut` shall preserve no-target/no-command behavior (action-only path, no dispatch exceptions).

R-096. `Shortcut` shall preserve context provenance (`TryGetSource`) so handlers can branch by true activation origin.

R-097. `Shortcut` mouse interaction shall preserve full-hit-area activation behavior (not just visible subview glyph regions).

R-098. `Shortcut` key behavior shall preserve focused-space activate path and focused-enter accept path.

R-099. `Shortcut` key-binding scope behavior shall preserve deterministic rebinding between view hotkey scope and application scope (`BindKeyToApplication`).

#### CheckBox

R-100. `CheckBox` activate semantics shall remain "toggle state" (single mutation per activation).

R-101. `CheckBox` accept semantics shall remain "confirm without toggle."

R-102. `CheckBox` keyboard mappings shall preserve `Space -> Activate` and `Enter -> Accept`.

R-103. `CheckBox` mouse mappings shall preserve single-click activation and double-click accept behavior.

R-104. `CheckBox` shall preserve the no-double-toggle invariant on double-click gesture sequences.

R-105. `CheckBox` hotkey semantics shall be explicit in the new spec (currently routed through activate/toggle behavior); redesign must either preserve or intentionally replace with migration note.

R-106. `CheckBox` value-changing cancellation semantics shall remain first-class and testable.

#### Button

R-107. `Button` shall preserve accept-centric behavior: `Enter`, `Space`, and click gestures route to accept.

R-108. `Button` hotkey command shall route to accept semantics (not a distinct command implementation path).

R-109. `Button` accept and click paths shall not require activate semantics.

R-110. Current hotkey-path activate side effects in tests shall be explicitly specified (preserve or intentionally eliminate), never left implicit.

R-111. `Button` mouse hold-repeat mode shall preserve deterministic one-accept-per-repeat-event semantics and avoid synthesized multi-click duplication effects.

R-112. `Button` dynamic mouse-binding rewrites when `MouseHoldRepeat` changes shall remain deterministic and idempotent.

R-113. Default-action participation (`IAcceptTarget.IsDefault`) shall remain compatible with dialog default action policy.

#### ListView

R-114. `ListView` navigation commands (`Up/Down` and extend variants) shall preserve activating-preview cancel opportunity before state mutation.

R-115. `ListView` hotkey command shall preserve focus acquisition behavior and `SelectedItem ??= 0` initialization behavior.

R-116. `ListView` accept command shall preserve cancellable accept pipeline semantics.

R-117. `ListView` shall preserve composite binding semantics where `Shift+Space` runs activate then move-down in sequence.

R-118. `ListView` mouse bindings shall preserve: single-click activate, double-click accept, wheel-to-scroll command mapping.

R-119. `ListView` shall preserve mode-dependent marking behavior across standard, radio-like, and multi-mark modes.

R-120. `ListView` ctrl/shift mouse gesture semantics shall remain explicit: ctrl-toggle, shift-range, ctrl-right-click range extension fallback.

R-121. `ListView` non-multi mode shall explicitly reject/ignore multi-mark gesture semantics.

R-122. `ListView` marking and selection mutation shall remain single-gesture/single-deterministic-state-transition per command route.

#### Bar

R-123. `Bar` shall preserve bubbling contract for `Activate` and `Accept` from contained shortcuts.

R-124. Direct `Bar` command invocation shall not implicitly bubble down to contained shortcuts.

R-125. Bubbling across nested `Bar` hierarchies shall remain deterministic and stoppable by handled state.

R-126. Clearing/overriding `CommandsToBubbleUp` shall deterministically disable bubble behavior.

R-127. Bar mouse-wheel behavior shall preserve focus navigation semantics.

#### MenuItem and Menu

R-128. `MenuItem` shall preserve shortcut-derived command semantics (`Accept`/`HotKey` action and command dispatch behavior).

R-129. `MenuItem` mouse-enter behavior shall preserve focus transfer to the hovered item.

R-130. `MenuItem` submenu attachment shall preserve submenu ownership metadata and right-arrow affordance semantics.

R-131. `MenuItem` activate/accept bubbling to `Menu` shall occur at most once per gesture (no double-fire).

R-132. `Menu` shall preserve bubbling contract for `Accept` and `Activate` toward superviews (for example `Bar`) when unhandled.

R-133. `Menu` shall preserve handled-state stop semantics to prevent upward propagation when menu-level handlers consume events.

R-134. `Menu` selected-item model shall remain focus-tracked and evented (`SelectedMenuItemChanged`).

R-135. `Menu` visibility/focus behaviors shall preserve deterministic initial selection semantics when shown.

R-136. `Menu` shall preserve "menu item activated implies menu accepted" behavior needed for popover closing logic.

#### MenuBarItem and MenuBar

R-137. `MenuBarItem` shall preserve custom hotkey path that avoids focus-side-effect races before activate-toggle logic.

R-138. `MenuBarItem` shall preserve "press hotkey again while open closes the active item" behavior.

R-139. `MenuBarItem` shall continue to surface popover acceptance by raising `Accepted` on the owning menu-bar item.

R-140. `MenuBarItem` shall preserve unsupported-submenu contract (must use `PopoverMenu`).

R-141. `MenuBar` shall preserve default-key toggle semantics (open/close activation).

R-142. `MenuBar` quit command/key shall preserve deterministic close-and-deactivate behavior.

R-143. `MenuBar` left/right command navigation shall preserve deterministic focus traversal among menu-bar items.

R-144. `MenuBar` activation from menu-bar-item or descendant source shall preserve open/close toggle semantics for that owning item.

R-145. `MenuBar` accept from menu-bar-item source shall preserve open/show semantics and handled behavior.

R-146. `MenuBar` accepted handling shall preserve deterministic deactivation rules for non-top-level sources.

R-147. `MenuBar.Active` shall remain the authoritative focusability/open-state gate and shall drive popover hide behavior on deactivation.

R-148. `MenuBar` mouse-enter behavior shall preserve "activate without opening" semantics; explicit click/hotkey drives opening.

R-149. `MenuBar` shall preserve switching behavior where activating a different item closes previously open item and opens new one.

R-150. Visibility/enabled guardrails shall remain explicit and deterministic (hidden/disabled menu bar or menu-bar-item does not activate/open).

R-151. Menu-item key actions reachable through menu infrastructure shall remain invocable when menu bar itself is hidden, if explicitly bound.

#### PopoverMenu

R-152. `PopoverMenu` shall preserve constructor-time command policy: left/right navigation support, quit handling, and hidden-by-default state.

R-153. `PopoverMenu.Root` reassignment shall unsubscribe old hierarchy handlers and avoid event-leak duplicate dispatch.

R-154. `PopoverMenu.MakeVisible` flow shall preserve deterministic pre-show key-binding refresh and placement logic.

R-155. Popover key-binding synthesis shall preserve command-to-key derivation precedence: target-view hotkeys first, otherwise app-level command key bindings.

R-156. `PopoverMenu.OnKeyDownNotHandled` shall preserve menu-item-key dispatch across full submenu hierarchy.

R-157. When not visible, popover accept handling shall process only keys that match known menu-item keys.

R-158. `MenuOnAccepting` behavior shall preserve "non-hotkey accept hides popover" semantics.

R-159. `MenuAccepted` behavior shall preserve leaf-item close vs submenu-open branching and then raise popover accepted.

R-160. Popover accept handling shall preserve continued propagation behavior where appropriate (non-consumptive return when unhandled).

R-161. Source/binding provenance shall remain preserved across popover/menu/menu-bar boundary routes.

R-162. Popover hide/remove submenu logic shall remain recursion-safe and reentrancy-safe.

R-163. Submenu focus-color forcing (`ForceFocusColors`) shall be reset reliably when popover/submenus are hidden.

R-164. Composite controls inside popovers (for example `OptionSelector`) shall preserve single-mutation semantics through boundary routing (no duplicate value changes).

#### NumericUpDown

R-165. `NumericUpDown` command semantics shall preserve `Command.Up`/`Command.Down` as primary increment/decrement operations.

R-166. Up/Down command handlers shall preserve activate-then-mutate behavior ordering.

R-167. Keyboard mappings shall preserve `CursorUp -> Command.Up` and `CursorDown -> Command.Down`.

R-168. Internal button accept events shall preserve delegation to corresponding up/down commands and mark the button accept as handled.

R-169. Unsupported numeric mode (`T == object`) shall preserve no-op command behavior (`false` outcome for Up/Down).

R-170. Value mutation lifecycle shall remain cancellable (`ValueChanging`) and observable (`ValueChanged`, `ValueChangedUntyped`).

R-171. Display text shall remain synchronized with value and format semantics after command-driven mutations.

R-172. Focus/default interaction model shall preserve focusability of the control while using non-focusable internal arrow buttons.

## Baseline Behaviors to Preserve or Replace Explicitly

These are currently validated in this branch and must be either preserved or intentionally superseded in the redesign spec:

- Release-based default mouse activation and release-outside cancellation behavior.
- Distinct `Activate` vs `Accept` behavior in `Shortcut` and selector flows.
- Context provenance (source and binding) surviving propagation.
- Prevention of re-entry loops during downward dispatch.
- Composite control single-toggle behavior (no duplicate state mutation).
- Deterministic hotkey behavior across focused and non-focused paths.

## Proposed Design (High Level)

1. Introduce a first-class `CommandRouter` owned by `IApplication`.
2. Keep `InvokeCommand` synchronous and immediate; add `PostCommand` for deferred cross-thread producers.
3. Split concerns into explicit primitives:
- `Command` (semantic command identity, lower churn from current API)
- `Gesture` (normalized keyboard/mouse trigger)
- `CommandBinding` (gesture -> command mapping with scope)
- `RoutePolicy` (local, bubble-up, delegate-down, bridge)
- `CommandOutcome` (typed handled result)
- `CommandContext` (immutable invocation metadata)
4. Replace ad-hoc bubbling flags with a compiled `RoutePlan` containing deterministic nodes and explicit bridge nodes.
5. Process every command through fixed phases:
- `Preview` (cancel/handle gate)
- `Execute` (state mutation)
- `Commit` (finalization/default action)
- `Notify` (post events/telemetry)
6. Move composite behavior to declarative policies instead of per-control hacks:
- Wrappers (for example `Shortcut`) declare forward/defer rules.
- Selectors declare single-mutation ownership rules.
- Menu/popover types declare explicit boundary bridges.
7. Replace implicit default accept heuristics with an explicit `DefaultActionPolicy`:
- clear selection rules
- single dispatch path
- no duplicate accept chains
8. Make tracing a built-in router feature:
- full route log
- node outcomes
- cycle/duplicate dispatch diagnostics
9. Keep compatibility behind adapters:
- legacy `AddCommand`, `KeyBindings`, and `MouseBindings` map into the new primitives.

This design keeps current behavior contracts but makes routing, mutation ownership, and boundary handling explicit and testable.

## Samples (Design Sketches)

These are shape-of-code examples, not final compile-ready API.

### Sample: `CheckBox.cs` with new command model

```csharp
namespace Terminal.Gui.Views;

public class CheckBox : View, IValue<CheckState>
{
    public CheckBox ()
    {
        CanFocus = true;
        Width = Dim.Auto (DimAutoStyle.Text);
        Height = Dim.Auto (DimAutoStyle.Text, 1);
    }

    protected override void ConfigureCommands (CommandModelBuilder builder)
    {
        // Gesture -> Command
        builder.BindKey (Key.Space, Command.Activate);
        builder.BindKey (Key.Enter, Command.Accept);
        builder.BindMouse (MouseGesture.LeftClick, Command.Activate);
        builder.BindMouse (MouseGesture.LeftDoubleClick, Command.Accept);

        // Command -> Handler
        builder.Handle (Command.Activate, RoutePolicy.LocalOnly, HandleActivate);
        builder.Handle (Command.Accept, RoutePolicy.LocalOnly, HandleAccept);
    }

    private CommandOutcome HandleActivate (CommandContext ctx)
    {
        // Activate mutates state (toggle), never auto-accepts.
        CheckState next = GetNextState (Value, AllowCheckStateNone);

        if (!TrySetValue (next))
        {
            return CommandOutcome.HandledStop;
        }

        RaiseActivated (ctx);
        return CommandOutcome.HandledStop;
    }

    private CommandOutcome HandleAccept (CommandContext ctx)
    {
        // Accept confirms only; no toggle.
        if (RaiseAccepting (ctx) is true)
        {
            return CommandOutcome.HandledStop;
        }

        RaiseAccepted (ctx);
        return CommandOutcome.HandledStop;
    }

    private bool TrySetValue (CheckState next)
    {
        ValueChangingEventArgs<CheckState> changingArgs = new (Value, next);
        ValueChanging?.Invoke (this, changingArgs);

        if (changingArgs.Handled)
        {
            return false;
        }

        Value = changingArgs.NewValue;
        ValueChanged?.Invoke (this, new ValueChangedEventArgs<CheckState> (changingArgs.CurrentValue, Value));
        return true;
    }
}
```

### Sample: `Shortcut.cs` with new command model

```csharp
namespace Terminal.Gui.Views;

public class Shortcut : View
{
    public View CommandView { get; set; } = new ();
    public View? TargetView { get; set; }
    public Command TargetCommand { get; set; } = Command.NotBound;
    public Action? Action { get; set; }
    public Key Key { get; set; } = Key.Empty;
    public bool BindKeyToApplication { get; set; }

    protected override void ConfigureCommands (CommandModelBuilder builder)
    {
        // Key routing for the shortcut itself.
        builder.BindKey (Key.Enter, Command.Accept);
        builder.BindKey (Key.Space, Command.Activate);
        builder.BindHotKey (Key, Command.HotKey, BindKeyToApplication ? BindingScope.Application : BindingScope.ViewHotKey);

        // Composite routing contract:
        // - Bubble Activate/Accept from CommandView.
        // - Delegate user gestures from Shortcut surface down to CommandView.
        // - Prevent duplicate mutation on round-trips.
        builder.Composite (composite =>
                           composite.BubbleFrom (CommandView, [Command.Activate, Command.Accept])
                                    .DelegateDownTo (CommandView,
                                                     [Command.Activate, Command.Accept],
                                                     ctx => ctx.IsUserGesture && !ctx.SourceIsWithin (CommandView))
                                    .SingleMutationPerGesture ());

        builder.Handle (Command.HotKey, RoutePolicy.LocalOnly, HandleHotKey);
        builder.Handle (Command.Activate, RoutePolicy.CompositeAware, HandleActivate);
        builder.Handle (Command.Accept, RoutePolicy.CompositeAware, HandleAccept);
    }

    private CommandOutcome HandleHotKey (CommandContext ctx)
    {
        if (RaiseHandlingHotKey (ctx) is true)
        {
            return CommandOutcome.HandledStop;
        }

        return DispatchCommand (Command.Activate, ctx.WithCommand (Command.Activate));
    }

    private CommandOutcome HandleActivate (CommandContext ctx)
    {
        if (RaiseActivating (ctx) is true)
        {
            return CommandOutcome.HandledStop;
        }

        RaiseActivated (ctx);
        ExecuteActionAndTarget (ctx);
        return CommandOutcome.HandledStop;
    }

    private CommandOutcome HandleAccept (CommandContext ctx)
    {
        if (RaiseAccepting (ctx) is true)
        {
            return CommandOutcome.HandledStop;
        }

        RaiseAccepted (ctx);
        ExecuteActionAndTarget (ctx);
        return CommandOutcome.HandledStop;
    }

    private void ExecuteActionAndTarget (CommandContext ctx)
    {
        Action?.Invoke ();

        if (TargetView is { } target && TargetCommand != Command.NotBound)
        {
            ctx = ctx.WithCommand (TargetCommand);
            target.DispatchCommand (TargetCommand, ctx);
        }
    }
}
```

### Sample: `OptionSelector.cs` with new command model

```csharp
namespace Terminal.Gui.Views;

public class OptionSelector : SelectorBase
{
    protected override void ConfigureCommands (CommandModelBuilder builder)
    {
        base.ConfigureCommands (builder);

        // OptionSelector owns selection state, so it owns Activate/Accept handling.
        builder.Handle (Command.Activate, RoutePolicy.CompositeOwner, HandleActivate);
        builder.Handle (Command.Accept, RoutePolicy.CompositeOwner, HandleAccept);
    }

    private CommandOutcome HandleActivate (CommandContext ctx)
    {
        if (ctx.TryGetSource (out View? source) && source is CheckBox checkBox)
        {
            bool spaceOnAlreadySelected = ctx.Binding is KeyBinding { Key: { } key }
                                          && key == Key.Space
                                          && Value == (int)checkBox.Data!;

            Value = spaceOnAlreadySelected ? GetNextValue () : (int)checkBox.Data!;
        }
        else
        {
            // Direct activation cycles selection.
            Value = GetNextValue ();
        }

        RaiseActivated (ctx);
        return CommandOutcome.HandledStop; // Consume to prevent duplicate checkbox mutation.
    }

    private CommandOutcome HandleAccept (CommandContext ctx)
    {
        if (RaiseAccepting (ctx) is true)
        {
            return CommandOutcome.HandledStop;
        }

        // Enter/direct Accept should also activate focused option first.
        if (Focused is CheckBox focused)
        {
            CommandContext activateCtx = ctx.WithCommand (Command.Activate).WithSource (focused);
            DispatchCommand (Command.Activate, activateCtx);
        }

        RaiseAccepted (ctx);
        return CommandOutcome.HandledStop;
    }

    private int GetNextValue ()
    {
        int index = Values.IndexOf (v => v == Value);
        return index == Values.Count - 1 ? Values [0] : Values [index + 1];
    }
}
```

### Sample: `FlagSelector.cs` with new command model

```csharp
namespace Terminal.Gui.Views;

public class FlagSelector : SelectorBase
{
    private bool _suppressHotKeyActivate;

    protected override void ConfigureCommands (CommandModelBuilder builder)
    {
        base.ConfigureCommands (builder);

        // Preserve current FlagSelector behavior differences.
        builder.UnbindKey (Key.Space);
        builder.UnbindKey (Key.Enter);
        builder.ClearMouseBindings ();

        builder.Handle (Command.HotKey, RoutePolicy.LocalOnly, HandleHotKey);
        builder.Handle (Command.Activate, RoutePolicy.CompositeOwner, HandleActivate);
        builder.Handle (Command.Accept, RoutePolicy.CompositeOwner, HandleAccept);
    }

    private CommandOutcome HandleHotKey (CommandContext ctx)
    {
        // Focused hotkey is a no-op.
        if (HasFocus)
        {
            return CommandOutcome.HandledStop;
        }

        _suppressHotKeyActivate = true;

        if (CanFocus)
        {
            SetFocus ();
        }

        // Allow HandlingHotKey observers to run without forcing toggle.
        RaiseHandlingHotKey (ctx);
        return CommandOutcome.HandledContinue;
    }

    private CommandOutcome HandleActivate (CommandContext ctx)
    {
        if (_suppressHotKeyActivate)
        {
            _suppressHotKeyActivate = false;
            return CommandOutcome.HandledStop;
        }

        if (ctx.TryGetSource (out View? source) && source is CheckBox sourceCheckBox)
        {
            ToggleCheckBox (sourceCheckBox);
            RaiseActivated (ctx);
            return CommandOutcome.HandledStop; // Prevent duplicate toggle.
        }

        if (Focused is CheckBox focusedCheckBox)
        {
            BubbleDownTo (focusedCheckBox, Command.Activate, ctx);
            return CommandOutcome.HandledStop;
        }

        return CommandOutcome.NotHandled;
    }

    private CommandOutcome HandleAccept (CommandContext ctx)
    {
        if (RaiseAccepting (ctx) is true)
        {
            return CommandOutcome.HandledStop;
        }

        // Enter/direct Accept should activate focused item before accept completes.
        if (Focused is CheckBox focusedCheckBox)
        {
            CommandContext activateCtx = ctx.WithCommand (Command.Activate).WithSource (focusedCheckBox);
            DispatchCommand (Command.Activate, activateCtx);
        }

        RaiseAccepted (ctx);
        return CommandOutcome.HandledStop;
    }

    private void ToggleCheckBox (CheckBox checkBox)
    {
        checkBox.Value = checkBox.Value == CheckState.Checked ? CheckState.UnChecked : CheckState.Checked;
    }
}
```
