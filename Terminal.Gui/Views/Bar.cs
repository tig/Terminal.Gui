﻿using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using static Unix.Terminal.Delegates;

namespace Terminal.Gui;

/// <summary>
/// Like a <see cref="Label"/>, but where the <see cref="View.Text"/> is formatted to highlight
/// the <see cref="Shortcut"/>.
/// <code>
/// 
/// </code>
/// </summary>
public class Shortcut : View {
	Key _key;
	KeyBindingScope _keyBindingScope;
	Command? _command;

	internal readonly View _container;
	View _commandView;
	bool _autoSize;

    public Shortcut ()
    {
        CanFocus = true;
        Height = 1;
        //AutoSize = true;

		AddCommand (Gui.Command.Default, () => {
			//SetFocus ();
			//SuperView?.FocusNext ();
			return true;
		});
		AddCommand (Gui.Command.Accept, () => OnAccept ());
		KeyBindings.Add (KeyCode.Space, Gui.Command.Accept);
		KeyBindings.Add (KeyCode.Enter, Gui.Command.Accept);

		_container = new View () { Id = "_container", CanFocus = true, Width = Dim.Fill (), Height = Dim.Fill () };
		_container.MouseClick += Container_MouseClick;

        CommandView = new View
        {
            Id = "_commandView", CanFocus = false, AutoSize = true, X = 0, Y = Pos.Center (), HotKeySpecifier = new Rune ('_')
        };

		HelpView = new View () { Id = "_helpView", CanFocus = false, AutoSize = true, Y = Pos.Center () };
		HelpView.TextAlignment = TextAlignment.Right;
		HelpView.MouseClick += SubView_MouseClick;

		KeyView = new View () { Id = "_keyView", CanFocus = false, AutoSize = true, Y = Pos.Center () };
		KeyView.MouseClick += SubView_MouseClick;

		CommandView.Margin.Thickness = new Thickness (1, 0, 1, 0);
		HelpView.Margin.Thickness = new Thickness (1, 0, 1, 0);
		KeyView.Margin.Thickness = new Thickness (1, 0, 1, 0);

		//CommandView.CanFocus = CanFocus;
		//HelpView.CanFocus = CanFocus;
		//KeyView.CanFocus = CanFocus;

		//_commandView.MouseClick += SubView_MouseClick;

		LayoutStarted += Shortcut_LayoutStarted;
		TitleChanged += Shortcut_TitleChanged;
		Initialized += Shortcut_Initialized;

        _container.Add (HelpView, KeyView);
        Add (_container);
    }

	private void Shortcut_Initialized (object sender, EventArgs e)
	{
		if (ColorScheme != null) {
			var cs = new ColorScheme (ColorScheme) {
				Normal = ColorScheme.HotNormal,
				HotNormal = ColorScheme.Normal
			};
			KeyView.ColorScheme = cs;
		}
	}
	private void Shortcut_LayoutStarted (object sender, LayoutEventArgs e) => SetSubViewLayout ();

	void SetSubViewLayout ()
	{
		if (!IsInitialized) {
			return;
		}

		if (AutoSize) {
			CommandView.SetRelativeLayout (Driver.Bounds);
			HelpView.SetRelativeLayout (Driver.Bounds);
			KeyView.SetRelativeLayout (Driver.Bounds);
			_container.Width = _commandView.Frame.Width +
			                   (HelpView.Visible && HelpView.Text.Length > 0 ? HelpView.Frame.Width + 2 : 0) +
			                   (KeyView.Visible && KeyView.Text.Length > 0 ? KeyView.Frame.Width + 2 : 0);
			Width = _container.Width + thickness.Horizontal;
		} else {
			//Width = Dim.Fill ();
			//Height = 1;
		}

		HelpView.X = Pos.AnchorEnd (KeyView.Text.GetColumns () + 1 + HelpView.Text.GetColumns () + 1) - 1;
		KeyView.X = Pos.AnchorEnd (KeyView.Text.GetColumns () + 1) - 1;

	}

	private void Shortcut_TitleChanged (object sender, TitleEventArgs e)
	{
		_commandView.Text = Title;
	}

	private void Container_MouseClick (object sender, MouseEventEventArgs e)
	{
		e.Handled = OnAccept ();
	}

	private void SubView_MouseClick (object sender, MouseEventEventArgs e)
	{
		e.Handled = OnAccept ();
		if (CanFocus) {
			SetFocus ();
		}
	}

	public override ColorScheme ColorScheme {
		get {
			if (base.ColorScheme == null) {
				return SuperView?.ColorScheme ?? base.ColorScheme;
			}
			return base.ColorScheme;
		} 
		set {
			base.ColorScheme = value;
			if (ColorScheme != null) {
				var cs = new ColorScheme (ColorScheme) {
					Normal = ColorScheme.HotNormal,
					HotNormal = ColorScheme.Normal
				};
				KeyView.ColorScheme = cs;
			}
		}
	}

	/// <summary>
	/// If <see langword="true"/> the Shortcut will be sized to fit the available space (the Bounds of the
	/// the SuperView).
	/// </summary>
	/// <remarks>
	/// </remarks>
	public override bool AutoSize {
		get => _autoSize;
		set {
			_autoSize = value;
			SetSubViewLayout ();
		}
	}

    public View CommandView
    {
        get => _commandView;
        set
        {
            if (value == null)
            {
                throw new ArgumentNullException ();
            }

            if (_commandView != null)
            {
                _container.Remove (_commandView);
                _commandView?.Dispose ();
            }

            _commandView = value;
            _commandView.Id = "_commandView";
            _commandView.AutoSize = true;
            _commandView.X = 0;
            _commandView.Y = Pos.Center ();
            _commandView.MouseClick += SubView_MouseClick;
            _commandView.DefaultCommand += SubView_DefaultCommand;
            _commandView.Margin.Thickness = new Thickness (1, 0, 1, 0);

            _commandView.HotKeyChanged += (s, e) =>
                                          {
                                              if (e.NewKey != Key.Empty)
                                              {
                                                  // Add it 
                                                  AddKeyBindingsForHotKey (e.OldKey, e.NewKey);
                                              }
                                          };

            _commandView.HotKeySpecifier = new Rune ('_');
            _container.Add (_commandView);
            SetSubViewLayout ();
        }
    }

	/// <summary>
	/// The shortcut key.
	/// </summary>
	public Key Key {
		get => _key;
		set {
			if (value == null) {
				throw new ArgumentNullException ();
			}
			_key = value;
			if (Command != null) {
				UpdateKeyBinding ();
			}
			KeyView.Text = $"{Key}";
			KeyView.Visible = Key != Key.Empty;
		}
	}

	public KeyBindingScope KeyBindingScope {
		get => _keyBindingScope;
		set {
			_keyBindingScope = value;
			if (Command != null) {
				UpdateKeyBinding ();
			}
		}
	}

	public Command? Command {
		get => _command;
		set {
			if (value != null) {
				_command = value.Value;
				UpdateKeyBinding ();
			}
		}
	}

	void UpdateKeyBinding ()
	{
		if (this.KeyBindingScope == KeyBindingScope.Application) {
			return;
		}

		if (Command != null && Key != null && Key != Key.Empty) {
			// Add a command and key binding for this command to this Shortcut
			if (!GetSupportedCommands ().Contains (Command.Value)) {
				// The action that will be taken will be to fire the OnClicked
				// event. 
				AddCommand (Command.Value, () => OnAccept ());
			}
			KeyBindings.Remove (Key);
			KeyBindings.Add (Key, this.KeyBindingScope, Command.Value);
		}

	}


	/// <summary>
	/// The event fired when the <see cref="Command.Accept"/> command is received. This
	/// occurs if the user clicks on the Bar with the mouse or presses the key bound to
	/// Command.Accept (Space by default).
	/// </summary>
	/// <remarks>
	/// Client code can hook up to this event, it is
	/// raised when the button is activated either with
	/// the mouse or the keyboard.
	/// </remarks>
	public event EventHandler<HandledEventArgs> Accept;

	/// <summary>
	/// Called when the <see cref="Command.Accept"/> command is received. This
	/// occurs if the user clicks on the Bar with the mouse or presses the key bound to
	/// Command.Accept (Space by default).
	/// </summary>
	public virtual bool OnAccept ()
	{
		if (Key == null || Key == Key.Empty) {
			return false;
		}

		bool handled = false;
		var keyCopy = new Key (Key);

		switch (KeyBindingScope) {
		case KeyBindingScope.Application:
			// Simulate a key down to invoke the Application scoped key binding
			handled = Application.OnKeyDown (keyCopy);
			break;
		case KeyBindingScope.Focused:
			//throw new InvalidOperationException ();
			handled = false;
			break;
		case KeyBindingScope.HotKey:
			handled = _commandView.InvokeCommand (Gui.Command.Accept) == true;
			break;
		}
		if (handled == false) {
			var args = new HandledEventArgs ();
			Accept?.Invoke (this, args);
			handled = args.Handled;
		}
		return handled;
	}

	public View CommandView {
		get => _commandView;
		set {
			if (value == null) {
				throw new ArgumentNullException ();
			}
			if (_commandView != null) {
				_container.Remove (_commandView);
				_commandView?.Dispose ();
			}
			_commandView = value;
			_commandView.Id = "_commandView";
			_commandView.AutoSize = true;
			_commandView.X = 0;
			_commandView.Y = Pos.Center ();
			_commandView.MouseClick += SubView_MouseClick;
			_commandView.Margin.Thickness = new Thickness (1, 0, 1, 0);

			_commandView.HotKeyChanged += (s, e) => {
				if (_commandView.HotKey != Key.Empty) {
					// Add it 
					AddKeyBindingsForHotKey (Key.Empty, _commandView.HotKey);
				}
			};

			_commandView.HotKeySpecifier = new Rune ('_');
			_container.Add (_commandView);
			SetSubViewLayout ();
		}
	}

	public override bool CanFocus {
		get {
			return base.CanFocus;
		}
		set {
			//if (IsInitialized) {
			//	CommandView.CanFocus = value;
			//	HelpView.CanFocus = value;
			//	KeyView.CanFocus = value;
			//}
			base.CanFocus = value;
		}
	}

	public override bool OnEnter (View view)
	{
		Application.Driver.SetCursorVisibility (CursorVisibility.Invisible);

		var cs = new ColorScheme (ColorScheme) {
			Normal = ColorScheme.Focus,
			HotNormal = ColorScheme.HotFocus
		};

		_container.ColorScheme = cs;

		cs = new ColorScheme (ColorScheme) {
			Normal = ColorScheme.HotFocus,
			HotNormal = ColorScheme.Focus
		};
		KeyView.ColorScheme = cs;

		return base.OnEnter (view);
	}

	public override bool OnLeave (View view)
	{
		var cs = new ColorScheme (ColorScheme) {
			Normal = ColorScheme.Normal,
			HotNormal = ColorScheme.HotNormal
		};

		_container.ColorScheme = cs;

		cs = new ColorScheme (ColorScheme) {
			Normal = ColorScheme.HotNormal,
			HotNormal = ColorScheme.Normal
		};
		KeyView.ColorScheme = cs;

        return base.OnLeave (view);
    }

    private void Container_MouseClick (object sender, MouseEventEventArgs e) { e.Handled = OnAccept (); }

    public int GetNaturalWidth ()
    {
        CommandView.SetRelativeLayout (Driver.Bounds);
        HelpView.SetRelativeLayout (Driver.Bounds);
        KeyView.SetRelativeLayout (Driver.Bounds);

        return CommandView.Frame.Width
               + (HelpView.Visible && HelpView.Text.Length > 0 ? HelpView.Frame.Width : 0)
               + (KeyView.Visible && KeyView.Text.Length > 0 ? KeyView.Frame.Width : 0)
               + GetAdornmentsThickness ().Horizontal;
    }

    private void SetSubViewLayout ()
    {
        if (!IsInitialized)
        {
            return;
        }

        if (Width is not Dim.DimFill)
        {
            Width = GetNaturalWidth ();
        }

        HelpView.X = Pos.AnchorEnd (KeyView.Text.GetColumns () + 1 + HelpView.Text.GetColumns () + 1) - 2;
        KeyView.X = Pos.AnchorEnd (KeyView.Text.GetColumns () + 1) - 1;
    }

    private void Shortcut_Initialized (object sender, EventArgs e)
    {
        if (ColorScheme != null)
        {
            var cs = new ColorScheme (ColorScheme)
            {
                Normal = ColorScheme.HotNormal,
                HotNormal = ColorScheme.Normal
            };
            KeyView.ColorScheme = cs;
        }
    }

    private void Shortcut_LayoutStarted (object sender, LayoutEventArgs e) { SetSubViewLayout (); }

    private void Shortcut_TitleChanged (object sender, TitleEventArgs e) { _commandView.Text = Title; }

    private void SubView_MouseClick (object sender, MouseEventEventArgs e)
    {
        e.Handled = OnAccept ();

        if (!e.Handled && CanFocus)
        {
            SetFocus ();
        }
    }

    private void SubView_DefaultCommand (object sender, CancelEventArgs e)
    {
        e.Cancel = OnAccept ();

        if (!e.Cancel && CanFocus)
        {
            SetFocus ();
        }
    }

    private void UpdateKeyBinding ()
    {
        if (KeyBindingScope == KeyBindingScope.Application)
        {
            return;
        }

        if (Command != null && Key != null && Key != Key.Empty)
        {
            // Add a command and key binding for this command to this Shortcut
            if (!GetSupportedCommands ().Contains (Command.Value))
            {
                // The action that will be taken will be to fire the OnClicked
                // event. 
                AddCommand (Command.Value, () => OnAccept ());
            }

            KeyBindings.Remove (Key);
            KeyBindings.Add (Key, KeyBindingScope, Command.Value);
        }
    }
}

/// <summary>
/// The Bar <see cref="View"/> provides a container for other views to be used as a toolbar or status bar.
/// </summary>
/// <remarks>
/// Views added to a Bar will be positioned horizontally from left to right.
/// </remarks>
public class Bar : Toplevel
{
    private bool _autoSize;

    /// <inheritdoc/>
    public Bar () { SetInitialProperties (); }

    /// <summary>
    ///     If <see langword="true"/> the Shortcut will be sized to fit the available space (the Bounds of the
    ///     the SuperView).
    /// </summary>
    /// <remarks>
    /// </remarks>
    public override bool AutoSize
    {
        get => _autoSize;
        set
        {
            _autoSize = value;
            Bar_LayoutComplete (null, null);
        }
    }

	public bool StatusBarStyle { get; set; } = true;

    public override void Add (View view)
    {
        if (Orientation == Orientation.Horizontal)
        {
            //view.AutoSize = true;
        }

        if (StatusBarStyle)
        {
            // Light up right border
            view.BorderStyle = LineStyle.Single;
            view.Border.Thickness = new Thickness (0, 0, 1, 0);
        }

		LayoutComplete += Bar_LayoutComplete;
	}

            view.Margin.Thickness = new Thickness (1, 0, 0, 0);
        }

        //view.ColorScheme = ColorScheme;

        // Add any HotKey keybindings to our bindings
        IEnumerable<KeyValuePair<Key, KeyBinding>> bindings = view.KeyBindings.Bindings.Where (b => b.Value.Scope == KeyBindingScope.HotKey);

        foreach (KeyValuePair<Key, KeyBinding> binding in bindings)
        {
            AddCommand (
                        binding.Value.Commands [0],
                        () =>
                        {
                            if (view is Shortcut shortcut)
                            {
                                return shortcut.CommandView.InvokeCommands (binding.Value.Commands);
                            }

                            return false;
                        });
            KeyBindings.Add (binding.Key, binding.Value);
        }

        base.Add (view);
    }

    private void Bar_LayoutComplete (object sender, LayoutEventArgs e)
    {
        View prevBarItem = null;

        switch (Orientation)
        {
            case Orientation.Horizontal:
                for (var index = 0; index < Subviews.Count; index++)
                {
                    View barItem = Subviews [index];

                    if (!barItem.Visible)
                    {
                        continue;
                    }

                    if (prevBarItem == null)
                    {
                        barItem.X = 0;
                    }
                    else
                    {
                        // Make view to right be autosize
                        //Subviews [^1].AutoSize = true;

                        // Align the view to the right of the previous view
                        barItem.X = Pos.Right (prevBarItem);
                    }

                    barItem.Y = Pos.Center ();
                    prevBarItem = barItem;
                }

                break;

            case Orientation.Vertical:
                var maxBarItemWidth = 0;

                for (var index = 0; index < Subviews.Count; index++)
                {
                    View barItem = Subviews [index];

                    if (!barItem.Visible)
                    {
                        continue;
                    }

                    if (prevBarItem == null)
                    {
                        barItem.Y = 0;
                    }
                    else
                    {
                        // Make view to right be autosize
                        //Subviews [^1].AutoSize = true;

                        // Align the view to the bottom of the previous view
                        barItem.Y = Pos.Bottom (prevBarItem);
                    }

                    prevBarItem = barItem;

                    if (barItem is Shortcut shortcut)
                    {
                        maxBarItemWidth = Math.Max (maxBarItemWidth, shortcut.GetNaturalWidth ());
                    }
                    else
                    {
                        maxBarItemWidth = Math.Max (maxBarItemWidth, barItem.Frame.Width);
                    }

				barItem.X = 0;

                for (var index = 0; index < Subviews.Count; index++)
                {
                    var shortcut = Subviews [index] as Shortcut;

                    if (shortcut is { Visible: false })
                    {
                        continue;
                    }

                    shortcut._container.Width = Dim.Fill ();
                    shortcut.Width = Dim.Fill ();
                }

                Width = maxBarItemWidth + GetAdornmentsThickness ().Horizontal;
                Height = Subviews.Count + GetAdornmentsThickness ().Vertical;

                break;
        }
    }

    private void SetInitialProperties ()
    {
        //AutoSize = false;
        ColorScheme = Colors.ColorSchemes ["Menu"];
        CanFocus = true;

        LayoutStarted += Bar_LayoutComplete;
    }
}
