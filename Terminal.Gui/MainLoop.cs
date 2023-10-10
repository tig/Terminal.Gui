﻿//
// MainLoop.cs: IMainLoopDriver and MainLoop for Terminal.Gui
//
// Authors:
//   Miguel de Icaza (miguel@gnome.org)
//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Terminal.Gui {
	/// <summary>
	/// Public interface to create a platform specific <see cref="MainLoop"/> driver.
	/// </summary>
	public interface IMainLoopDriver {
		/// <summary>
		/// Initializes the <see cref="MainLoop"/>, gets the calling main loop for the initialization.
		/// </summary>
		/// <remarks>
		/// Call <see cref="TearDown"/> to release resources.
		/// </remarks>
		/// <param name="mainLoop">Main loop.</param>
		void Setup (MainLoop mainLoop);

		/// <summary>
		/// Wakes up the <see cref="MainLoop"/> that might be waiting on input, must be thread safe.
		/// </summary>
		void Wakeup ();

		/// <summary>
		/// Must report whether there are any events pending, or even block waiting for events.
		/// </summary>
		/// <returns><c>true</c>, if there were pending events, <c>false</c> otherwise.</returns>
		bool EventsPending ();

		/// <summary>
		/// The iteration function.
		/// </summary>
		void Iteration ();

		/// <summary>
		/// Tears down the <see cref="MainLoop"/> driver. Releases resources created in <see cref="Setup"/>.
		/// </summary>
		void TearDown ();
	}

	/// <summary>
	///   The MainLoop monitors timers and idle handlers.
	/// </summary>
	/// <remarks>
	///   Monitoring of file descriptors is only available on Unix, there
	///   does not seem to be a way of supporting this on Windows.
	/// </remarks>
	public class MainLoop : IDisposable {

		internal SortedList<long, Timeout> _timeouts = new SortedList<long, Timeout> ();
		readonly object _timeoutsLockToken = new object ();

		/// <summary>
		/// The idle handlers and lock that must be held while manipulating them
		/// </summary>
		readonly object _idleHandlersLock = new object ();
		internal List<Func<bool>> _idleHandlers = new List<Func<bool>> ();

		/// <summary>
		/// Gets the list of all timeouts sorted by the <see cref="TimeSpan"/> time ticks.
		/// A shorter limit time can be added at the end, but it will be called before an
		///  earlier addition that has a longer limit time.
		/// </summary>
		public SortedList<long, Timeout> Timeouts => _timeouts;

		/// <summary>
		/// Gets a copy of the list of all idle handlers.
		/// </summary>
		public ReadOnlyCollection<Func<bool>> IdleHandlers {
			get {
				lock (_idleHandlersLock) {
					return new List<Func<bool>> (_idleHandlers).AsReadOnly ();
				}
			}
		}

		/// <summary>
		/// The current <see cref="IMainLoopDriver"/> in use.
		/// </summary>
		/// <value>The main loop driver.</value>
		public IMainLoopDriver MainLoopDriver { get; private set; }

		/// <summary>
		/// Invoked when a new timeout is added. To be used in the case
		/// when <see cref="Application.ExitRunLoopAfterFirstIteration"/> is <see langword="true"/>.
		/// </summary>
		public event EventHandler<TimeoutEventArgs> TimeoutAdded;

		/// <summary>
		///  Creates a new MainLoop. 
		/// </summary>
		/// <remarks>
		/// Use <see cref="Dispose"/> to release resources.
		/// </remarks>
		/// <param name="driver">The <see cref="ConsoleDriver"/> instance
		/// (one of the implementations FakeMainLoop, UnixMainLoop, NetMainLoop or WindowsMainLoop).</param>
		public MainLoop (IMainLoopDriver driver)
		{
			MainLoopDriver = driver;
			driver.Setup (this);
		}

		/// <summary>
		///   Runs <paramref name="action"/> on the thread that is processing events
		/// </summary>
		/// <param name="action">the action to be invoked on the main processing thread.</param>
		public void Invoke (Action action)
		{
			AddIdle (() => {
				action ();
				return false;
			});
		}

		/// <summary>
		///   Adds specified idle handler function to <see cref="MainLoop"/> processing. 
		///   The handler function will be called once per iteration of the main loop after other events have been handled.
		/// </summary>
		/// <remarks>
		/// <para>
		///   Remove an idle handler by calling <see cref="RemoveIdle(Func{bool})"/> with the token this method returns.
		/// </para>
		/// <para>
		///   If the <paramref name="idleHandler"/> returns  <see langword="false"/> it will be removed and not called subsequently.
		/// </para>
		/// </remarks>
		/// <param name="idleHandler">Token that can be used to remove the idle handler with <see cref="RemoveIdle(Func{bool})"/> .</param>
		public Func<bool> AddIdle (Func<bool> idleHandler)
		{
			lock (_idleHandlersLock) {
				_idleHandlers.Add (idleHandler);
			}

			MainLoopDriver.Wakeup ();
			return idleHandler;
		}

		/// <summary>
		///   Removes an idle handler added with <see cref="AddIdle(Func{bool})"/> from processing.
		/// </summary>
		/// <param name="token">A token returned by <see cref="AddIdle(Func{bool})"/></param>
		/// Returns <c>true</c>if the idle handler is successfully removed; otherwise, <c>false</c>.
		///  This method also returns <c>false</c> if the idle handler is not found.
		public bool RemoveIdle (Func<bool> token)
		{
			lock (_idleHandlersLock) {
				return _idleHandlers.Remove (token);
			}
		}

		void AddTimeout (TimeSpan time, Timeout timeout)
		{
			lock (_timeoutsLockToken) {
				var k = (DateTime.UtcNow + time).Ticks;
				_timeouts.Add (NudgeToUniqueKey (k), timeout);
				TimeoutAdded?.Invoke (this, new TimeoutEventArgs (timeout, k));
			}
		}

		/// <summary>
		///   Adds a timeout to the <see cref="MainLoop"/>.
		/// </summary>
		/// <remarks>
		///   When time specified passes, the callback will be invoked.
		///   If the callback returns true, the timeout will be reset, repeating
		///   the invocation. If it returns false, the timeout will stop and be removed.
		///
		///   The returned value is a token that can be used to stop the timeout
		///   by calling <see cref="RemoveTimeout(object)"/>.
		/// </remarks>
		public object AddTimeout (TimeSpan time, Func<MainLoop, bool> callback)
		{
			if (callback == null) {
				throw new ArgumentNullException (nameof (callback));
			}
			var timeout = new Timeout () {
				Span = time,
				Callback = callback
			};
			AddTimeout (time, timeout);
			return timeout;
		}

		/// <summary>
		///   Removes a previously scheduled timeout
		/// </summary>
		/// <remarks>
		///   The token parameter is the value returned by AddTimeout.
		/// </remarks>
		/// Returns <c>true</c>if the timeout is successfully removed; otherwise, <c>false</c>.
		/// This method also returns <c>false</c> if the timeout is not found.
		public bool RemoveTimeout (object token)
		{
			lock (_timeoutsLockToken) {
				var idx = _timeouts.IndexOfValue (token as Timeout);
				if (idx == -1) {
					return false;
				}
				_timeouts.RemoveAt (idx);
			}
			return true;
		}

		void RunTimers ()
		{
			var now = DateTime.UtcNow.Ticks;
			SortedList<long, Timeout> copy;

			// lock prevents new timeouts being added
			// after we have taken the copy but before
			// we have allocated a new list (which would
			// result in lost timeouts or errors during enumeration)
			lock (_timeoutsLockToken) {
				copy = _timeouts;
				_timeouts = new SortedList<long, Timeout> ();
			}

			foreach ((var k, var timeout) in copy) {
				if (k < now) {
					if (timeout.Callback (this)) {
						AddTimeout (timeout.Span, timeout);
					}
				} else {
					lock (_timeoutsLockToken) {
						_timeouts.Add (NudgeToUniqueKey (k), timeout);
					}
				}
			}
		}

		/// <summary>
		/// Called from <see cref="IMainLoopDriver.EventsPending"/> to check if there are any outstanding timers or idle handlers.
		/// </summary>
		/// <param name="waitTimeout">Returns the number of milliseconds remaining in the current timer (if any). Will be -1 if
		/// there are no active timers.</param>
		/// <returns><see langword="true"/> if there is a timer or idle handler active.</returns>
		public bool CheckTimersAndIdleHandlers (out int waitTimeout)
		{
			var now = DateTime.UtcNow.Ticks;

			waitTimeout = 0;

			lock (_timeouts) {
				if (_timeouts.Count > 0) {
					waitTimeout = (int)((_timeouts.Keys [0] - now) / TimeSpan.TicksPerMillisecond);
					if (waitTimeout < 0) {
						// This avoids 'poll' waiting infinitely if 'waitTimeout < 0' until some action is detected
						// This can occur after IMainLoopDriver.Wakeup is executed where the pollTimeout is less than 0
						// and no event occurred in elapsed time when the 'poll' is start running again.
						waitTimeout = 0;
					}
					return true;
				}
				// ManualResetEventSlim.Wait, which is called by IMainLoopDriver.EventsPending, will wait indefinitely if
				// the timeout is -1.
				waitTimeout = -1;
			}

			// There are no timers set, check if there are any idle handlers

			lock (_idleHandlers) {
				return _idleHandlers.Count > 0;
			}
		}

		/// <summary>
		/// Finds the closest number to <paramref name="k"/> that is not
		/// present in <see cref="_timeouts"/> (incrementally).
		/// </summary>
		/// <param name="k"></param>
		/// <returns></returns>
		private long NudgeToUniqueKey (long k)
		{
			lock (_timeoutsLockToken) {
				while (_timeouts.ContainsKey (k)) {
					k++;
				}
			}

			return k;
		}

		void RunIdle ()
		{
			List<Func<bool>> iterate;
			lock (_idleHandlersLock) {
				iterate = _idleHandlers;
				_idleHandlers = new List<Func<bool>> ();
			}

			foreach (var idle in iterate) {
				if (idle ()) {
					lock (_idleHandlersLock) {
						_idleHandlers.Add (idle);
					}
				}
			}
		}

		bool _running;

		/// <summary>
		///   Determines whether there are pending events to be processed.
		/// </summary>
		/// <remarks>
		///   You can use this method if you want to probe if events are pending.
		///   Typically used if you need to flush the input queue while still
		///   running some of your own code in your main thread.
		/// </remarks>
		public bool EventsPending ()
		{
			return MainLoopDriver.EventsPending ();
		}

		/// <summary>
		///   Runs one iteration of timers and file watches
		/// </summary>
		/// <remarks>
		///   Use this to process all pending events (timers, idle handlers and file watches).
		///
		///   <code>
		///     while (main.EventsPending ()) RunIteration ();
		///   </code>
		/// </remarks>
		public void RunIteration ()
		{
			lock (_timeouts) {
				if (_timeouts.Count > 0) {
					RunTimers ();
				}
			}
			
			MainLoopDriver.Iteration ();

			var runIdle = false;
			lock (_idleHandlersLock) {
				runIdle = _idleHandlers.Count > 0;
			}

			if (runIdle) {
				RunIdle ();
			}
		}

		/// <summary>
		///   Runs the <see cref="MainLoop"/>.
		/// </summary>
		public void Run ()
		{
			var prev = _running;
			_running = true;
			while (_running) {
				EventsPending ();
				RunIteration ();
			}
			_running = prev;
		}

		/// <summary>
		/// Stops the main loop driver and calls <see cref="IMainLoopDriver.Wakeup"/>.
		/// </summary>
		public void Stop ()
		{
			_running = false;
			MainLoopDriver.Wakeup ();
		}
		
		/// <inheritdoc/>
		public void Dispose ()
		{
			GC.SuppressFinalize (this);
			Stop ();
			_running = false;
			MainLoopDriver?.TearDown ();
			MainLoopDriver = null;
		}
	}
}