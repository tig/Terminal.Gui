namespace Terminal.Gui;

/// <summary>
///     Provides a horizontally or vertically oriented container for other views to be used as a menu, toolbar, or status bar.
/// </summary>
/// <remarks>
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
