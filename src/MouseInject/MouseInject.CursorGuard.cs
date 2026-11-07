using System.Runtime.InteropServices;
using static ThreeFingerDrag.NativeMethods;

namespace ThreeFingerDrag;

internal static partial class MouseInject
{
	/// <summary>
	/// Optional predicate used by the hook to suppress external cursor movement during a drag.
	/// <see cref="TouchProcessor"/> sets it while touchpad movement could conflict with the drag.
	/// </summary>
	public static Func<bool>? ShouldSuppressExternalCursorMovement { get; set; }
	public static Action<string, bool, Point>? PhysicalMouseTriggerChanged { get; set; }
	public static Action<Point>? PhysicalMouseMoved { get; set; }
	/// <summary>Receives relative mouse deltas from raw input. These keep counting while the cursor is frozen.</summary>
	public static Action<int, int>? PhysicalMouseRawDelta { get; set; }
	/// <summary>When true, physical mouse movement is blocked so the cursor stays where it is.</summary>
	public static Func<bool>? ShouldHoldExternalCursorMovement { get; set; }

	/// <summary>True while the trigger button is physically down, as seen by the hook.</summary>
	private static volatile bool PhysicalTriggerDown;

	private static nint cursorMovementGuardHook;
	private static HookProc? cursorMovementGuardProc;
	private static long lastExternalCursorMovementUtcTicks;

	/// <summary>Most recent non-injected mouse movement, used to detect concurrent cursor ownership.</summary>
	public static DateTime LastExternalCursorMovementUtc => new(Interlocked.Read(ref lastExternalCursorMovementUtcTicks), DateTimeKind.Utc);

	/// <summary>
	/// Installs a low-level hook that can block external movement during a synthetic drag. Call from
	/// a thread with a live message loop; repeated calls are ignored.
	/// </summary>
	public static void InstallCursorMovementGuard()
	{
		if (cursorMovementGuardHook != nint.Zero)
		{
			CursorDiagnosticLogger.Record("mouse_guard_install", "already_installed");
			return;
		}

		cursorMovementGuardProc = CursorMovementGuardProc;
		var hook = SetWindowsHookEx(WH_MOUSE_LL, cursorMovementGuardProc, nint.Zero, 0);
		if (hook == nint.Zero)
		{
			cursorMovementGuardProc = null;
			CursorDiagnosticLogger.Record("mouse_guard_install", "hook_failed");
			return;
		}

		cursorMovementGuardHook = hook;
		CursorDiagnosticLogger.Record("mouse_guard_install", "installed");
	}

	/// <summary>Removes the cursor movement hook, if installed.</summary>
	public static void UninstallCursorMovementGuard()
	{
		var wasInstalled = cursorMovementGuardHook != nint.Zero;
		if (wasInstalled) _ = UnhookWindowsHookEx(cursorMovementGuardHook);

		cursorMovementGuardHook = nint.Zero;
		cursorMovementGuardProc = null;
		CursorDiagnosticLogger.Record("mouse_guard_uninstall", wasInstalled ? "uninstalled" : "not_installed");
	}

	/// <summary>Blocks external mouse movement when the configured predicate requests it.</summary>
	private static IntPtr CursorMovementGuardProc(int nCode, IntPtr wParam, IntPtr lParam)
	{
		if (nCode == HC_ACTION)
		{
			var hookData = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
			var injected = (hookData.flags & LlmhfInjected) != 0;
			Point point = new(hookData.pt.X, hookData.pt.Y);
			var message = unchecked((int)wParam.ToInt64());
			if (!injected)
			{
				if (message == 0x0200)
				{
					_ = Interlocked.Exchange(ref lastExternalCursorMovementUtcTicks, DateTime.UtcNow.Ticks);
					var moved = PhysicalMouseMoved;
					if (moved is not null) EnqueueHookWork(() => moved(point)); // WM_MOUSEMOVE
				}
				if (TryGetAnyButton(message, hookData.mouseData, out var anyButton, out var anyPressed))
				{
					var mouseData = hookData.mouseData;
					EnqueueHookWork(() => CursorDiagnosticLogger.RecordClick(anyButton, anyPressed, point, mouseData));
				}
				if (TryGetTriggerButton(message, hookData.mouseData, out var button, out var pressed))
				{
					// Updated here, not on the worker thread, so the freeze ends the moment the button is released.
					PhysicalTriggerDown = pressed;
					var triggerChanged = PhysicalMouseTriggerChanged;
					EnqueueHookWork(() =>
					{
						CursorDiagnosticLogger.Record("mouse_guard_trigger", pressed ? "down_consumed" : "up_consumed", point, point, new { button });
						triggerChanged?.Invoke(button, pressed, point);
					});
					return (IntPtr)1;
				}
			}

			if (message == (int)WM_MOUSEMOVE)
			{
				var holdCursor = PhysicalTriggerDown && ShouldHoldExternalCursorMovement?.Invoke() == true;
				var suppress = !injected && (ShouldSuppressExternalCursorMovement?.Invoke() == true || holdCursor);
				var source = injected ? "injected mouse event" : "physical device, touchpad driver, or program";
				var action = injected ? "injected_mouse_move" : "external_mouse_move";
				EnqueueHookWork(() => CursorDiagnosticLogger.RecordCursorEvent(point, source, action, suppress, movedByProgram: injected));
				if (suppress) return (IntPtr)1;
			}
		}

		// Low-level hooks ignore hhk, so NULL is sufficient when forwarding the event.
		return CallNextHookEx(nint.Zero, nCode, wParam, lParam);
	}
}
