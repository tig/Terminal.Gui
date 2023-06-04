using System;
using System.Threading;
using System.Threading.Tasks;

namespace Terminal.Gui;
public class FakeMainLoop : IMainLoopDriver {
	private MainLoop _mainLoop;

	public Action<ConsoleKeyInfo> KeyPressed;

	public FakeMainLoop (ConsoleDriver consoleDriver = null)
	{
		// consoleDriver is not needed/used in FakeConsole
	}
	
	public void Setup (MainLoop mainLoop)
	{
		_mainLoop = mainLoop;
	}

	public void Wakeup ()
	{
		// No implementation needed for FakeMainLoop
	}

	public bool EventsPending (bool wait)
	{
		//if (CheckTimers (wait, out var waitTimeout)) {
		//	return true;
		//}

		// Always return true for FakeMainLoop
		return true;
	}

	public void Iteration()
	{
		if (FakeConsole.MockKeyPresses.Count > 0) {
			KeyPressed?.Invoke(FakeConsole.MockKeyPresses.Pop());
		}
	}

	void Stop ()
	{
        // No implementation needed for FakeMainLoop
    }
}
