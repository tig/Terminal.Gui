﻿using System;
using System.Collections.Generic;
using System.Linq;
using Terminal.Gui;

namespace UICatalog;

class KeyBindingsDialog : Dialog {
	// TODO: Update to use Key instead of KeyCode
	static readonly Dictionary<Command, KeyCode> CurrentBindings = new ();
	readonly Command [] _commands;
	readonly ListView _commandsListView;
	readonly Label _keyLabel;

	public KeyBindingsDialog ()
	{
		Title = "Keybindings";
		//Height = 50;
		//Width = 10;
		if (ViewTracker.Instance == null) {
			ViewTracker.Initialize ();
		}

		// known commands that views can support
		_commands = Enum.GetValues (typeof (Command)).Cast<Command> ().ToArray ();

		_commandsListView = new ListView {
			Width = Dim.Percent (50),
			Height = Dim.Percent (100) - 1,
			Source = new ListWrapper (_commands),
			SelectedItem = 0
		};

		Add (_commandsListView);

		_keyLabel = new Label {
			Text = "Key: None",
			Width = Dim.Fill (),
			X = Pos.Percent (50),
			Y = 0
		};
		Add (_keyLabel);

		var btnChange = new Button {
			X = Pos.Percent (50),
			Y = 1,
			Text = "Ch_ange"
		};
		Add (btnChange);
		btnChange.Clicked += RemapKey;

		var close = new Button { Text = "Ok" };
		close.Clicked += (s, e) => {
			Application.RequestStop ();
			ViewTracker.Instance.StartUsingNewKeyMap (CurrentBindings);
		};
		AddButton (close);

		var cancel = new Button { Text = "Cancel" };
		cancel.Clicked += (s, e) => Application.RequestStop ();
		AddButton (cancel);

		// Register event handler as the last thing in constructor to prevent early calls
		// before it is even shown (e.g. OnEnter)
		_commandsListView.SelectedItemChanged += CommandsListView_SelectedItemChanged;

		// Setup to show first ListView entry
		SetTextBoxToShowBinding (_commands.First ());
	}

	void RemapKey (object sender, EventArgs e)
	{
		var cmd = _commands [_commandsListView.SelectedItem];
		KeyCode? key = null;

		// prompt user to hit a key
		var dlg = new Dialog { Title = "Enter Key" };
		dlg.KeyDown += (s, k) => {
			key = k.KeyCode;
			Application.RequestStop ();
		};
		Application.Run (dlg);

		if (key.HasValue) {
			CurrentBindings [cmd] = key.Value;
			SetTextBoxToShowBinding (cmd);
		}
	}

	void SetTextBoxToShowBinding (Command cmd)
	{
		if (CurrentBindings.ContainsKey (cmd)) {
			_keyLabel.Text = "Key: " + CurrentBindings [cmd];
		} else {
			_keyLabel.Text = "Key: None";
		}

		SetNeedsDisplay ();
	}

	void CommandsListView_SelectedItemChanged (object sender, ListViewItemEventArgs obj) =>
		SetTextBoxToShowBinding ((Command)obj.Value);

	/// <summary>
	///         Tracks views as they are created in UICatalog so that their keybindings can
	///         be managed.
	/// </summary>
	class ViewTracker {
		/// <summary>
		///         All views seen so far and a bool to indicate if we have applied keybindings to them
		/// </summary>
		readonly Dictionary<View, bool> _knownViews = new ();

		readonly object _lockKnownViews = new ();
		Dictionary<Command, KeyCode> _keybindings;

		ViewTracker (View top)
		{
			RecordView (top);

			// Refresh known windows
			Application.AddTimeout (TimeSpan.FromMilliseconds (100), () => {
				lock (_lockKnownViews) {
					RecordView (Application.Top);

					ApplyKeyBindingsToAllKnownViews ();
				}

				return true;
			});
		}

		public static ViewTracker Instance { get; private set; }

		void RecordView (View view)
		{
			if (!_knownViews.ContainsKey (view)) {
				_knownViews.Add (view, false);
			}

			// may already have subviews that were added to it
			// before we got to it
			foreach (var sub in view.Subviews) {
				RecordView (sub);
			}
			// TODO: BUG: Based on my new understanding of Added event I think this is wrong
			// (and always was wrong). Parents don't get to be told when new views are added
			// to them

			view.Added += (s, e) => RecordView (e.Child);
		}

		internal static void Initialize () => Instance = new ViewTracker (Application.Top);

		internal void StartUsingNewKeyMap (Dictionary<Command, KeyCode> currentBindings)
		{
			lock (_lockKnownViews) {
				// change our knowledge of what keys to bind
				_keybindings = currentBindings;

				// Mark that we have not applied the key bindings yet to any views
				foreach (var view in _knownViews.Keys) {
					_knownViews [view] = false;
				}
			}
		}

		void ApplyKeyBindingsToAllKnownViews ()
		{
			if (_keybindings == null) {
				return;
			}

			// Key is the view Value is whether we have already done it
			foreach (var viewDone in _knownViews) {
				var view = viewDone.Key;
				var done = viewDone.Value;

				if (done) {
					// we have already applied keybindings to this view
					continue;
				}

				var supported = new HashSet<Command> (view.GetSupportedCommands ());

				foreach (var kvp in _keybindings) {
					// if the view supports the keybinding
					if (supported.Contains (kvp.Key)) {
						// if the key was bound to any other commands clear that
						view.KeyBindings.Remove (kvp.Value);
						view.KeyBindings.Add (kvp.Value, kvp.Key);
					}

					// mark that we have done this view so don't need to set keybindings again on it
					_knownViews [view] = true;
				}
			}
		}
	}
}