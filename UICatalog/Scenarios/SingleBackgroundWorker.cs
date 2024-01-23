﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using Terminal.Gui;

namespace UICatalog.Scenarios;

[ScenarioMetadata (Name: "Single BackgroundWorker", Description: "A single BackgroundWorker threading opening another Toplevel")]
[ScenarioCategory ("Threading")]
[ScenarioCategory ("Top Level Windows")]
public class SingleBackgroundWorker : Scenario {
	public override void Run ()
	{
		Application.Top.Dispose ();

		Application.Run<MainApp> ();

		Application.Top.Dispose ();
	}

	public class MainApp : Toplevel {
		private BackgroundWorker _worker;
		private List<string> _log = new List<string> ();
		private DateTime? _startStaging;
		private ListView _listLog;

		public MainApp ()
		{
			var menu = new MenuBar (new MenuBarItem [] {
					new MenuBarItem ("_Options", new MenuItem [] {
						new MenuItem ("_Run Worker", "", () => RunWorker(), null, null, KeyCode.CtrlMask | KeyCode.R),
						null,
						new MenuItem ("_Quit", "", () => Application.RequestStop(), null, null, KeyCode.CtrlMask | KeyCode.Q)
					})
				});
			Add (menu);

			var statusBar = new StatusBar (new [] {
					new StatusItem(Application.QuitKey, $"{Application.QuitKey} to Quit", () => Application.RequestStop()),
					new StatusItem(KeyCode.CtrlMask | KeyCode.P, "~^R~ Run Worker", () => RunWorker())
				});
			Add (statusBar);

			var top = new Toplevel ();

			top.Add (new Label {
				X = Pos.Center (),
				Y = 0,
				Text = "Worker Log"
			});

			_listLog = new ListView (_log) {
				X = 0,
				Y = 2,
				Width = Dim.Fill (),
				Height = Dim.Fill ()
			};
			top.Add (_listLog);
			Add (top);
		}

		private void RunWorker ()
		{
			_worker = new BackgroundWorker () { WorkerSupportsCancellation = true };

			var cancel = new Button { Text = "Cancel Worker" };
			cancel.Clicked += (s, e) => {
				if (_worker == null) {
					_log.Add ($"Worker is not running at {DateTime.Now}!");
					_listLog.SetNeedsDisplay ();
					return;
				}

				_log.Add ($"Worker {_startStaging}.{_startStaging:fff} is canceling at {DateTime.Now}!");
				_listLog.SetNeedsDisplay ();
				_worker.CancelAsync ();
			};

			_startStaging = DateTime.Now;
			_log.Add ($"Worker is started at {_startStaging}.{_startStaging:fff}");
			_listLog.SetNeedsDisplay ();

			var md = new Dialog (cancel) { Title = $"Running Worker started at {_startStaging}.{_startStaging:fff}" };
			md.Add (new Label {
				X = Pos.Center (),
				Y = Pos.Center (),
				Text = "Wait for worker to finish..."
			});

			_worker.DoWork += (s, e) => {
				var stageResult = new List<string> ();
				for (int i = 0; i < 500; i++) {
					stageResult.Add ($"Worker {i} started at {DateTime.Now}");
					e.Result = stageResult;
					Thread.Sleep (1);
					if (_worker.CancellationPending) {
						e.Cancel = true;
						return;
					}
				}
			};

			_worker.RunWorkerCompleted += (s, e) => {
				if (md.IsCurrentTop) {
					//Close the dialog
					Application.RequestStop ();
				}

				if (e.Error != null) {
					// Failed
					_log.Add ($"Exception occurred {e.Error.Message} on Worker {_startStaging}.{_startStaging:fff} at {DateTime.Now}");
					_listLog.SetNeedsDisplay ();
				} else if (e.Cancelled) {
					// Canceled
					_log.Add ($"Worker {_startStaging}.{_startStaging:fff} was canceled at {DateTime.Now}!");
					_listLog.SetNeedsDisplay ();
				} else {
					// Passed
					_log.Add ($"Worker {_startStaging}.{_startStaging:fff} was completed at {DateTime.Now}.");
					_listLog.SetNeedsDisplay ();
					Application.Refresh ();
					var builderUI = new StagingUIController (_startStaging, e.Result as List<string>);
					builderUI.Load ();
				}
				_worker = null;
			};
			_worker.RunWorkerAsync ();
			Application.Run (md);
		}
	}

	public class StagingUIController : Window {
		Toplevel top;

		public StagingUIController (DateTime? start, List<string> list)
		{
			var frame = Application.Top.Frame;
			top = new Toplevel () { X = frame.X, Y = frame.Y, Width = frame.Width, Height = frame.Height };
			top.KeyDown += (s, e) => {
				// Prevents Ctrl+Q from closing this.
				// Only Ctrl+C is allowed.
				if (e == Application.QuitKey) {
					e.Handled = true;
				}
			};

			bool Close ()
			{
				var n = MessageBox.Query (50, 7, "Close Window.", "Are you sure you want to close this window?", "Yes", "No");
				return n == 0;
			}

			var menu = new MenuBar (new MenuBarItem [] {
					new MenuBarItem ("_Stage", new MenuItem [] {
						new MenuItem ("_Close", "", () => { if (Close()) { Application.RequestStop(); } }, null, null, KeyCode.CtrlMask | KeyCode.C)
					})
				});
			top.Add (menu);

			var statusBar = new StatusBar (new [] {
					new StatusItem(KeyCode.CtrlMask | KeyCode.C, "~^C~ Close", () => { if (Close()) { Application.RequestStop(); } }),
				});
			top.Add (statusBar);

				Title = $"Worker started at {start}.{start:fff}";
				ColorScheme = Colors.ColorSchemes ["Base"];

			Add (new ListView (list) {
				X = 0,
				Y = 0,
				Width = Dim.Fill (),
				Height = Dim.Fill ()
			});

			top.Add (this);
		}

		public void Load ()
		{
			Application.Run (top);
		}
	}
}
