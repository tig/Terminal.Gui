#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Terminal.Gui;

namespace UICatalog.Scenarios;

/// <summary>
///     Provides an editor UI for the Margin, Border, and Padding of a View.
/// </summary>
public class LayoutEditor : View
{
    public LayoutEditor ()
    {
        Title = "_LayoutEditor";

        Width = Dim.Auto (DimAutoStyle.Content);
        Height = Dim.Auto (DimAutoStyle.Content);

        CanFocus = true;

        TabStop = TabBehavior.TabGroup;

        _expandButton = new ()
        {
            Orientation = Orientation.Vertical
        };

        Initialized += LayoutEditor_Initialized;

        AddCommand (Command.Accept, () => true);
    }

    private View? _viewToEdit;

    private readonly List<string> _dimNames = ["Auto", "Percent", "Fill", "Absolute"];

    private PosEditor? _xEditor;
    private PosEditor? _yEditor;

    private DimEditor? _widthEditor;
    private DimEditor? _heightEditor;

    /// <summary>
    ///     Gets or sets whether the LayoutEditor should automatically select the View to edit
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

            if (value is null && _viewToEdit is { })
            {
                _viewToEdit.LayoutComplete -= View_LayoutComplete;
            }

            _viewToEdit = value;

            if (_viewToEdit is { })
            {
                _viewToEdit.LayoutComplete += View_LayoutComplete;
            }

            if (_xEditor is { })
            {
                _xEditor.ViewToEdit = _viewToEdit;
            }

            if (_yEditor is { })
            {
                _yEditor.ViewToEdit = _viewToEdit;
            }

            if (_widthEditor is { })
            {
                _widthEditor.ViewToEdit = _viewToEdit;
            }

            if (_heightEditor is { })
            {
                _heightEditor.ViewToEdit = _viewToEdit;
            }

        }
    }

    private void View_LayoutComplete (object? sender, LayoutEventArgs args)
    {
        UpdateSettings ();
    }

    private bool _updatingSettings = false;

    private void UpdateSettings ()
    {
        if (ViewToEdit is null)
        {
            return;
        }

        _updatingSettings = true;

        _updatingSettings = false;
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

        View? view = e.View;

        if (view is null)
        {
            return;
        }

        if (view is Adornment adornment)
        {
            ViewToEdit = AutoSelectAdornments ? adornment : adornment.Parent;
        }
        else
        {
            ViewToEdit = view;
        }
    }

    /// <inheritdoc/>
    protected override void Dispose (bool disposing)
    {
        base.Dispose (disposing);
    }

    private readonly ExpanderButton? _expandButton;

    public ExpanderButton? ExpandButton => _expandButton;

    private void LayoutEditor_Initialized (object? sender, EventArgs e)
    {
        BorderStyle = LineStyle.Rounded;

        Border.Add (_expandButton!);

        _xEditor = new ()
        {
            Title = "_X",
            BorderStyle = LineStyle.None,
            Dimension = Dimension.Width
        };

        _yEditor = new ()
        {
            Title = "_Y",
            BorderStyle = LineStyle.None,
            Dimension = Dimension.Height,
            X = Pos.Right(_xEditor) + 1
        };


        _widthEditor = new ()
        {
            Title = "_Width",
            BorderStyle = LineStyle.None,
            Dimension = Dimension.Width,
            X = Pos.Right(_yEditor) + 1
        };

        _heightEditor = new ()
        {
            Title = "_Height",
            BorderStyle = LineStyle.None,
            Dimension = Dimension.Height,
            X = Pos.Right (_widthEditor) + 1
        };

        Add (_xEditor, _yEditor, _widthEditor, _heightEditor);

        Application.MouseEvent += ApplicationOnMouseEvent;
        Application.Navigation!.FocusedChanged += NavigationOnFocusedChanged;
    }
}
