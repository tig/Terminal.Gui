using System.Linq;
using Terminal.Gui;

namespace UICatalog.Scenarios {
	[ScenarioMetadata (Name: "Generic", Description: "Generic sample - A template for creating new Scenarios")]
	[ScenarioCategory ("Controls")]
	public class MyScenario : Scenario {
		public override void Init ()
		{
			// The base `Scenario.Init` implementation:
			//  - Calls `Application.Init ()`
			//  - Adds a full-screen Window to Application.Top with a title
			//    that reads "Press <hotkey> to Quit". Access this Window with `this.Win`.
			//  - Sets the Theme & the ColorScheme property of `this.Win` to `colorScheme`.
			// To override this, implement an override of `Init`.

			//base.Init ();

			// A common, alternate, implementation where `this.Win` is not used is below. This code
			// leverages ConfigurationManager to borrow the color scheme settings from UICatalog:

			Application.Init ();
			ConfigurationManager.Themes.Theme = Theme;
			ConfigurationManager.Apply ();
			Application.Top.ColorScheme = Colors.ColorSchemes [TopLevelColorScheme];
		}

		public override void Setup ()
		{
			// Put scenario code here (in a real app, this would be the code
			// that would setup the app before `Application.Run` is called`).
			// With a Scenario, after UI Catalog calls `Scenario.Setup` it calls
			// `Scenario.Run` which calls `Application.Run`. Example:

			//var button = new Button ("Press me!") {
			//	AutoSize = false,
			//	X = Pos.Center (),
			//	Y = Pos.Center (),
			//};
			//Application.Top.Add (button);

			var textField = new TextField () { X = 50, Y = 1, Width = 10 };

			MenuBarItem menuItem = null;
			MenuItem CreateMenuItem (int i)
			{
				return new MenuItem ($"Item {i}", null, () => {
					textField.Text = menuItem.Children [i - 1].Title;
				});
			}
			menuItem = new MenuBarItem ($"{Application.Driver.DownArrow}",
				Enumerable.Range (1, 5).Select (
						(a) => CreateMenuItem (a))
					.ToArray ());

			var menuBar = new MenuBar (new [] { menuItem }) {
				CanFocus = true,
				TextAlignment =	TextAlignment.Right,
				Width = 1, 
				Y = Pos.Top (textField),
				X = Pos.Right (textField)
			};
			// HACK: We want the drop-down to be left-aligned with the textField. But we want the down arrow to be
			// to the right of the textField. Currently MenuBar does not honor `TextAlignment`.
			// This hack works around this to use Margin to push the rendering of the down arrow to be to
			// the right of the textField. The hardcoded 10 breaks Computed Layout though. 
			//menuBar.Margin.Thickness = new Thickness (10, 0, 0, 0);

			// HACK: required to make this work:
			menuBar.Enter += (s, e) => {
				// BUG: This does not select menu item 0
				// Instead what happens is the first keystroke the user presses
				// gets swallowed and focus is moved to 0.  Result is that you have
				// to press down arrow twice to select first menu item and/or have to
				// press Tab twice to move focus back to TextField
				menuBar.OpenMenu ();
			};

			Application.Top.Add (textField);
			Application.Top.Add (menuBar);

		}
	}
}