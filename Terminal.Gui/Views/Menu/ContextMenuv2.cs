﻿#nullable enable

using System.Diagnostics;

namespace Terminal.Gui;

/// <summary>
///     ContextMenuv2 provides a Popover menu that can be positioned anywhere within a <see cref="View"/>.
///     <para>
///         To show the ContextMenu, set <see cref="Application.Popover"/> to the ContextMenu object and set
///         <see cref="View.Visible"/> property to <see langword="true"/>.
///     </para>
///     <para>
///         The menu will be hidden when the user clicks outside the menu or when the user presses <see cref="Application.QuitKey"/>.
///     </para>
///     <para>
///         To explicitly hide the menu, set <see cref="View.Visible"/> property to <see langword="false"/>.
///     </para>
///     <para>
///         <see cref="Key"/> is the key used to activate the ContextMenus (<c>Shift+F10</c> by default). Callers can use this in
///         their keyboard handling code.
///     </para>
///     <para>The menu will be displayed at the current mouse coordinates.</para>
/// </summary>
public class ContextMenuv2 : Menuv2
{
    private Key _key = DefaultKey;

    private MouseFlags _mouseFlags = MouseFlags.Button3Clicked;

    public MouseFlags MouseFlags
    {
        get => _mouseFlags;
        set
        {
            _mouseFlags = value;
        }
    }

    /// <summary>Initializes a context menu with no menu items.</summary>
    public ContextMenuv2 () : this ([]) { }

    /// <inheritdoc/>
    public ContextMenuv2 (IEnumerable<Shortcut> shortcuts) : base(shortcuts)
    {
        Visible = false;
        VisibleChanging += OnVisibleChanging;
        Key = DefaultKey;
    }

    private void OnVisibleChanging (object? sender, CancelEventArgs<bool> args)
    {
        if (args.NewValue)
        {

        }
    }

    /// <summary>The default key for activating the context menu.</summary>
    [SerializableConfigurationProperty (Scope = typeof (SettingsScope))]
    public static Key DefaultKey { get; set; } = Key.F10.WithShift;

    /// <summary>Specifies the key that will activate the context menu.</summary>
    public Key Key
    {
        get => _key;
        set
        {
            Key oldKey = _key;
            _key = value;
            KeyChanged?.Invoke (this, new KeyChangedEventArgs (oldKey, _key));
        }
    }

    /// <summary>Event raised when the <see cref="ContextMenu.Key"/> is changed.</summary>
    public event EventHandler<KeyChangedEventArgs>? KeyChanged;
}
