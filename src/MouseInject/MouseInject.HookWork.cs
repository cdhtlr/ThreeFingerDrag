using System.Collections.Concurrent;
using static ThreeFingerDrag.NativeMethods;

namespace ThreeFingerDrag;

internal static partial class MouseInject
{
	private static readonly BlockingCollection<Action> HookWork = [];
	private static int hookWorkerStarted;

	/// <summary>
	/// Windows silently removes a low-level hook whose callback does not return within LowLevelHooksTimeout,
	/// so the callback only queues work. Handlers run in order on a single worker thread.
	/// </summary>
	private static void EnqueueHookWork(Action work)
	{
		if (Interlocked.CompareExchange(ref hookWorkerStarted, 1, 0) == 0)
		{
			new Thread(RunHookWork) { IsBackground = true, Name = "Mouse hook dispatcher" }.Start();
			CursorDiagnosticLogger.Record("mouse_hook_worker", "started");
		}
		HookWork.Add(work);
	}

	/// <summary>Body of the low-level hook thread. It pumps messages until the hook is removed.</summary>
	private static void RunHookWork()
	{
		foreach (var work in HookWork.GetConsumingEnumerable())
		{
			try
			{
				work();
			}
			catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
			{
				// One failed handler must not stop mouse processing for the rest of the session.
				CursorDiagnosticLogger.Record("mouse_hook_worker", "exception", details: new { exceptionType = ex.GetType().FullName, message = ex.Message });
			}
		}
	}

	/// <summary>Maps every physical mouse-button message to a button name and state, for click diagnostics.</summary>
	private static bool TryGetAnyButton(int message, uint mouseData, out string button, out bool pressed)
	{
		button = "";
		pressed = false;
		switch (message)
		{
			case 0x0201:
				button = "Left";
				pressed = true;
				return true;
			case 0x0202:
				button = "Left";
				pressed = false;
				return true;
			case 0x0204:
				button = "Right";
				pressed = true;
				return true;
			case 0x0205:
				button = "Right";
				pressed = false;
				return true;
			case 0x0207:
				button = "Middle";
				pressed = true;
				return true;
			case 0x0208:
				button = "Middle";
				pressed = false;
				return true;
			case 0x020B:
				button = (mouseData >> 16) == 2 ? "X2" : "X1";
				pressed = true;
				return true;
			case 0x020C:
				button = (mouseData >> 16) == 2 ? "X2" : "X1";
				pressed = false;
				return true;
			default:
				return false;
		}
	}

	/// <summary>Maps a raw mouse message to a button name, such as Left, Right, or X1. Returns false for other messages.</summary>
	private static bool TryGetTriggerButton(int message, uint mouseData, out string button, out bool pressed)
	{
		button = "";
		pressed = false;
		var configured = PointerSettings.GetMouseDragTriggerButton();
		if (configured == "Secondary") configured = GetSystemMetrics(23) != 0 ? "Left" : "Right";

		(string Button, int Down, int Up)[] messages =
		[
			("Left", 0x0201, 0x0202), ("Right", 0x0204, 0x0205),
			("Middle", 0x0207, 0x0208), ("X1", 0x020B, 0x020C), ("X2", 0x020B, 0x020C),
		];
		foreach (var candidate in messages)
		{
			if (!string.Equals(candidate.Button, configured, StringComparison.OrdinalIgnoreCase)) continue;

			if (candidate.Button is "X1" or "X2")
			{
				var xButton = (mouseData >> 16) & 0xFFFF;
				if (xButton != (candidate.Button == "X1" ? 1u : 2u)) continue;
			}
			if (message == candidate.Down)
			{ button = candidate.Button; pressed = true; return true; }
			if (message == candidate.Up)
			{ button = candidate.Button; pressed = false; return true; }
		}
		return false;
	}
}
