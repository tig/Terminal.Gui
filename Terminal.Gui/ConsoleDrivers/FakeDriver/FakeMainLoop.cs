using System;
using System.Threading;

namespace Terminal.Gui {
	/// <summary>
	/// Mainloop intended to be used with the .NET System.Console API, and can
	/// be used on Windows and Unix, it is cross platform but lacks things like
	/// file descriptor monitoring.
	/// </summary>
	/// <remarks>
	/// This implementation is used for FakeDriver.
	/// </remarks>
	public class FakeMainLoop : IMainLoopDriver {
		bool _running;
		AutoResetEvent keyReady = new AutoResetEvent (false);
		AutoResetEvent waitForProbe = new AutoResetEvent (false);
		ConsoleKeyInfo? keyResult = null;
		MainLoop mainLoop;
		//Func<ConsoleKeyInfo> consoleKeyReaderFn = () => ;

		/// <summary>
		/// Invoked when a Key is pressed.
		/// </summary>
		public Action<ConsoleKeyInfo> KeyPressed;

		/// <summary>
		/// Creates an instance of the FakeMainLoop. <paramref name="consoleDriver"/> is not used.
		/// </summary>
		/// <param name="consoleDriver"></param>
		public FakeMainLoop (ConsoleDriver consoleDriver = null)
		{
			// consoleDriver is not needed/used in FakeConsole
		}

		void MockKeyReader ()
		{
			while (_running) {
				waitForProbe.WaitOne ();
				keyResult = FakeConsole.ReadKey (true);
				if (_running) {
					keyReady.Set ();
				}
			}
		}

		void IMainLoopDriver.Setup (MainLoop mainLoop)
		{
			_running = true;
			this.mainLoop = mainLoop;
			Thread readThread = new Thread (MockKeyReader);
			readThread.Start ();
		}

		void IMainLoopDriver.Wakeup ()
		{
		}

		bool IMainLoopDriver.EventsPending (bool wait)
		{
			keyResult = null;
			waitForProbe.Set ();

			if (mainLoop.CheckTimers (wait, out var waitTimeout)) {
				return true;
			}

			keyReady.WaitOne (waitTimeout);
			return keyResult.HasValue || mainLoop.CheckTimers (wait, out _);
		}

		void IMainLoopDriver.Iteration ()
		{
			if (keyResult.HasValue) {
				KeyPressed?.Invoke (keyResult.Value);
				keyResult = null;
			}
		}

		void IMainLoopDriver.Stop ()
		{
			_running = false;
			keyReady.Dispose ();
			keyReady = null;
			waitForProbe.Dispose ();
			waitForProbe = null;
		}
	}
}