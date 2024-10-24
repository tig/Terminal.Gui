#nullable enable
using System;
using Terminal.Gui;

namespace UICatalog.Scenarios;

/// <summary>
///     Provides an editor UI for the Margin, Border, and Padding of a View.
/// </summary>
public class AdornmentsEditor : View
{
    public AdornmentsEditor ()
    {
        Title = "AdornmentsEditor";

        Width = Dim.Auto (DimAutoStyle.Content);
        Height = Dim.Auto (DimAutoStyle.Content);

        CanFocus = true;

        TabStop = TabBehavior.TabGroup;

        ExpandButton = new ()
        {
            Orientation = Orientation.Horizontal
        };

        Initialized += AdornmentsEditor_Initialized;
    }

    private View? _viewToEdit;

    private MarginEditor? _marginEditor;
    private BorderEditor? _borderEditor;
    private PaddingEditor? _paddingEditor;

    /// <summary>
    ///     Gets or sets whether the AdornmentsEditor should automatically select the View to edit
    ///     based on the values of <see cref="AutoSelectSuperView"/> and <see cref="AutoSelectAdornments"/>.
    /// </summary>
    public bool AutoSelectViewToEdit { get; set; }

    /// <summary>
    ///     Gets or sets the View that will scope the behavior of <see cref="AutoSelectViewToEdit"/>.
    /// </summary>
    public View? AutoSelectSuperView { get; set; }

    /// <summary>
    ///     Gets or sets whether auto select with the mouse will select Adornments or just Views.
    /// </summary>
    public bool AutoSelectAdornments { get; set; }

    public View? ViewToEdit
    {
        get => _viewToEdit;
        set
        {
            if (_viewToEdit == value)
            {
                return;
            }

            _viewToEdit = value;

            if (_marginEditor is { })
            {
                _marginEditor.AdornmentToEdit = _viewToEdit?.Margin ?? null;
            }

            if (_borderEditor is { })
            {
                _borderEditor.AdornmentToEdit = _viewToEdit?.Border ?? null;
            }

            if (_paddingEditor is { })
            {
                _paddingEditor.AdornmentToEdit = _viewToEdit?.Padding ?? null;
            }

            if (_viewToEdit is not Adornment)
            {
                Enabled = true;
            }
            else
            {
                Enabled = false;
            }

            Padding.Text = $"View: {GetIdentifyingString(_viewToEdit)}";
        }
    }

    private string GetIdentifyingString (View? view)
    {
        if (view is null)
        {
            return "null";
        }

        if (!string.IsNullOrEmpty (view.Id))
        {
            return view.Id;
        }

        if (!string.IsNullOrEmpty (view.Title))
        {
            return view.Title;
        }

        if (!string.IsNullOrEmpty (view.Text))
        {
            return view.Text;
        }

        return view.GetType ().Name;
    }

    private void NavigationOnFocusedChanged (object? sender, EventArgs e)
    {
        if (AutoSelectSuperView is null)
        {
            return;
        }

        if (ApplicationNavigation.IsInHierarchy (this, Application.Navigation!.GetFocused ()))
        {
            return;
        }

        if (!ApplicationNavigation.IsInHierarchy (AutoSelectSuperView, Application.Navigation!.GetFocused ()))
        {
            return;
        }

        ViewToEdit = Application.Navigation!.GetFocused ();
    }

    private void ApplicationOnMouseEvent (object? sender, MouseEventArgs e)
    {
        if (e.Flags != MouseFlags.Button1Clicked || !AutoSelectViewToEdit)
        {
            return;
        }

        if ((AutoSelectSuperView is { } && !AutoSelectSuperView.FrameToScreen ().Contains (e.Position))
            || FrameToScreen ().Contains (e.Position))
        {
            return;
        }

        View view = e.View;

        if (view is { })
        {
            if (view is Adornment adornment)
            {
                ViewToEdit = AutoSelectAdornments ? adornment : adornment.Parent;
            }
            else
            {
                ViewToEdit = view;
            }
        }
    }

    /// <inheritdoc/>
    protected override void Dispose (bool disposing) { base.Dispose (disposing); }

    public ExpanderButton? ExpandButton { get; }

    public bool ShowViewIdentifier
    {
        get => Padding.Thickness != Thickness.Empty;
        set
        {
            if (value)
            {
                Padding.Thickness = new (0, 2, 0, 0);
            }
            else
            {
                Padding.Thickness =Thickness.Empty;
            }
        }
    }

    private void AdornmentsEditor_Initialized (object? sender, EventArgs e)
    {
        BorderStyle = LineStyle.Dotted;

        Border.Add (ExpandButton!);

        _marginEditor = new ()
        {
            X = 0,
            Y = 0,
            SuperViewRendersLineCanvas = true
        };
        Add (_marginEditor);

        _borderEditor = new ()
        {
            X = Pos.Left (_marginEditor),
            Y = Pos.Bottom (_marginEditor),
            SuperViewRendersLineCanvas = true
        };
        Add (_borderEditor);

        _paddingEditor = new ()
        {
            X = Pos.Left (_borderEditor),
            Y = Pos.Bottom (_borderEditor),
            SuperViewRendersLineCanvas = true
        };
        Add (_paddingEditor);


        _marginEditor.AdornmentToEdit = _viewToEdit?.Margin ?? null;
        _borderEditor.AdornmentToEdit = _viewToEdit?.Border ?? null;
        _paddingEditor.AdornmentToEdit = _viewToEdit?.Padding ?? null;

        Application.MouseEvent += ApplicationOnMouseEvent;
        Application.Navigation!.FocusedChanged += NavigationOnFocusedChanged;
    }
}
