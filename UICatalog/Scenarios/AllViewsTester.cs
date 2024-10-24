using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Terminal.Gui;

namespace UICatalog.Scenarios;

[ScenarioMetadata ("All Views Tester", "Provides a test UI for all classes derived from View.")]
[ScenarioCategory ("Layout")]
[ScenarioCategory ("Tests")]
[ScenarioCategory ("Controls")]
[ScenarioCategory ("Adornments")]
public class AllViewsTester : Scenario
{

    private Dictionary<string, Type> _viewClasses;
    private ListView _classListView;
    private AdornmentsEditor _adornmentsEditor;

    private LayoutEditor _layoutEditor;
    private FrameView _settingsPane;
    private RadioGroup _orientation;
    private string _demoText = "This, that, and the other thing.";
    private TextView _demoTextView;

    private FrameView _hostPane;
    private View _curView;
    private EventLog _eventLog;

    public override void Main ()
    {
        // Don't create a sub-win (Scenario.Win); just use Application.Top
        Application.Init ();

        var app = new Window
        {
            Title = GetQuitKeyAndName (),
            ColorScheme = Colors.ColorSchemes ["TopLevel"],
            BorderStyle = LineStyle.None
        };

        _viewClasses = GetAllViewClassesCollection ()
                       .OrderBy (t => t.Name)
                       .Select (t => new KeyValuePair<string, Type> (t.Name, t))
                       .ToDictionary (t => t.Key, t => t.Value);

        _classListView = new ()
        {
            Title = "Classes [_1]",
            X = 0,
            Y = 0,
            Width = Dim.Auto (),
            Height = Dim.Fill (),
            AllowsMarking = false,
            ColorScheme = Colors.ColorSchemes ["TopLevel"],
            SelectedItem = 0,
            Source = new ListWrapper<string> (new (_viewClasses.Keys.ToList ())),
            BorderStyle = LineStyle.Rounded
        };

        _classListView.SelectedItemChanged += (s, args) =>
                                              {
                                                  // Dispose existing current View, if any
                                                  DisposeCurrentView ();

                                                  CreateCurrentView (_viewClasses.Values.ToArray () [_classListView.SelectedItem]);

                                                  // Force ViewToEdit to be the view and not a subview
                                                  if (_adornmentsEditor is { })
                                                  {
                                                      _adornmentsEditor.AutoSelectSuperView = _curView;

                                                      _adornmentsEditor.ViewToEdit = _curView;
                                                  }
                                              };

        _classListView.Accepting += (sender, args) =>
                                    {
                                        _curView.SetFocus ();
                                        args.Cancel = true;
                                    };

        _adornmentsEditor = new ()
        {
            Title = "Adornments [_2]",
            X = Pos.Right (_classListView),
            Y = 0,
            Width = Dim.Auto (),
            Height = Dim.Fill (),
            ColorScheme = Colors.ColorSchemes ["TopLevel"],
            BorderStyle = LineStyle.Single,
            AutoSelectViewToEdit = false,
            AutoSelectAdornments = false,
        };

        var expandButton = new ExpanderButton
        {
            CanFocus = false,
            Orientation = Orientation.Horizontal
        };
        _adornmentsEditor.Border.Add (expandButton);

        _layoutEditor = new ()
        {
            Title = "Layout [_3]",
            X = Pos.Right (_adornmentsEditor),
            Y = 0,
            //Width = Dim.Fill (), // set below
            Height = Dim.Auto (),
            CanFocus = true,
            ColorScheme = Colors.ColorSchemes ["TopLevel"],
            BorderStyle = LineStyle.Rounded
        };

        _settingsPane = new ()
        {
            Title = "Settings [_4]",
            X = Pos.Right (_adornmentsEditor),
            Y = Pos.Bottom (_layoutEditor),
            Width = Dim.Width(_layoutEditor),
            Height = Dim.Auto (),
            CanFocus = true,
            ColorScheme = Colors.ColorSchemes ["TopLevel"],
            BorderStyle = LineStyle.Rounded
        };

        Label label = new () { X = 0, Y = 0, Text = "_Orientation:" };

        _orientation = new ()
        {
            X = Pos.Right (label) + 1,
            Y = Pos.Top (label),
            RadioLabels = new [] { "Horizontal", "Vertical" },
            Orientation = Orientation.Horizontal
        };

        _orientation.SelectedItemChanged += (s, selected) =>
                                            {
                                                if (_curView is IOrientation orientatedView)
                                                {
                                                    orientatedView.Orientation = (Orientation)_orientation.SelectedItem;
                                                }
                                            };
        _settingsPane.Add (label, _orientation);

        label = new () { X = 0, Y = Pos.Bottom (_orientation), Text = "_Text:" };

        _demoTextView = new ()
        {
            X = Pos.Right (label) + 1,
            Y = Pos.Top (label),
            Width = Dim.Fill (),
            Height = Dim.Auto (minimumContentDim: 2),
            Text = _demoText
        };

        _demoTextView.ContentsChanged += (s, e) =>
                                         {
                                             _demoText = _demoTextView.Text;

                                             if (_curView is { })
                                             {
                                                 _curView.Text = _demoText;
                                             }
                                         };

        _settingsPane.Add (label, _demoTextView);

        _eventLog = new EventLog ()
        {
           // X = Pos.Right(_layoutEditor)
        };
        _eventLog.X = Pos.AnchorEnd ();
        _eventLog.Y = 0;

        _eventLog.Height = Dim.Height (_classListView);
        //_eventLog.Width = 30;

        _layoutEditor.Width = Dim.Fill (Dim.Func ((() =>
                                                   {
                                                       //if (_eventLog.NeedsLayout)
                                                       //{
                                                       //    // We have two choices:
                                                       //    // 1) Call Layout explicitly
                                                       //    // 2) Throw LayoutException so Layout tries again
                                                       //    _eventLog.Layout ();
                                                       //    //throw new LayoutException ("_eventLog");
                                                       //}
                                                       return _eventLog.Frame.Width;
                                                   })));

        _hostPane = new ()
        {
            X = Pos.Right (_adornmentsEditor),
            Y = Pos.Bottom (_settingsPane),
            Width = Dim.Width (_layoutEditor),
            Height = Dim.Fill (),
            CanFocus = true,
            TabStop = TabBehavior.TabStop,
            ColorScheme = Colors.ColorSchemes ["Base"],
            Arrangement = ViewArrangement.Resizable,
            BorderStyle = LineStyle.RoundedDotted
        };
        _hostPane.Border.ColorScheme = app.ColorScheme;
        _hostPane.Padding.Thickness = new (1);
        //_hostPane.Padding.Diagnostics = ViewDiagnosticFlags.Ruler;
        _hostPane.Padding.ColorScheme = app.ColorScheme;

        app.Add (_classListView, _adornmentsEditor, _layoutEditor, _settingsPane, _eventLog, _hostPane);

        app.Initialized += App_Initialized;

        Application.Run (app);
        app.Dispose ();
        Application.Shutdown ();
    }

    private void App_Initialized (object sender, EventArgs e)
    {
        _classListView.SelectedItem = 0;
        _classListView.SetFocus ();
    }

    // TODO: Add Command.HotKey handler (pop a message box?)
    private void CreateCurrentView (Type type)
    {
        Debug.Assert (_curView is null);

        // If we are to create a generic Type
        if (type.IsGenericType)
        {
            // For each of the <T> arguments
            List<Type> typeArguments = new ();

            // use <object>
            foreach (Type arg in type.GetGenericArguments ())
            {
                typeArguments.Add (typeof (object));
            }

            // And change what type we are instantiating from MyClass<T> to MyClass<object>
            type = type.MakeGenericType (typeArguments.ToArray ());
        }

        // Instantiate view
        var view = (View)Activator.CreateInstance (type);
        if (view is IDesignable designable)
        {
            designable.EnableForDesign (ref _demoText);
        }
        else
        {
            view.Text = _demoText;
            view.Title = "_Test Title";
        }

        if (view is IOrientation orientatedView)
        {
            _orientation.SelectedItem = (int)orientatedView.Orientation;
            _orientation.Enabled = true;
        }
        else
        {
            _orientation.Enabled = false;
        }

        view.Initialized += CurrentView_Initialized;
        view.LayoutComplete += CurrentView_LayoutComplete;

        view.Id = "_curView";
        _curView = view;

        _eventLog.ViewToLog = _curView;
        _hostPane.Add (_curView);
        _layoutEditor.ViewToEdit = _curView;
        //_curView.SetNeedsLayout ();
    }

    private void DisposeCurrentView ()
    {
        if (_curView != null)
        {
            _curView.Initialized -= CurrentView_Initialized;
            _curView.LayoutComplete -= CurrentView_LayoutComplete;
            _hostPane.Remove (_curView);
            _layoutEditor.ViewToEdit = null;

            _curView.Dispose ();
            _curView = null;
        }
    }

    private static List<Type> GetAllViewClassesCollection ()
    {
        List<Type> types = new ();

        foreach (Type type in typeof (View).Assembly.GetTypes ()
                                           .Where (
                                                   myType =>
                                                       myType.IsClass && !myType.IsAbstract && myType.IsPublic && myType.IsSubclassOf (typeof (View))
                                                  ))
        {
            types.Add (type);
        }

        types.Add (typeof (View));

        return types;
    }

    private void CurrentView_LayoutComplete (object sender, LayoutEventArgs args)
    {
        UpdateHostTitle (_curView);
    }

    private void UpdateHostTitle (View view) { _hostPane.Title = $"{view.GetType ().Name} [_0]"; }

    private void CurrentView_Initialized (object sender, EventArgs e)
    {
        if (sender is not View view)
        {
            return;
        }

        if (!view.Width!.Has<DimAuto> (out _) || (view.Width is null))
        {
            view.Width = Dim.Fill ();
        }

        if (!view.Height!.Has<DimAuto> (out _) || (view.Height is null))
        {
            view.Height = Dim.Fill ();
        }

        UpdateHostTitle (view);
    }
}
