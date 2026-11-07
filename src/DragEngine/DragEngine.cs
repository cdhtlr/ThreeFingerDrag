namespace ThreeFingerDrag;

/// <summary>Shared drag lifecycle used by touchpad and mouse input adapters.</summary>
/// <remarks>Split across DragEngine.Lifecycle.cs, DragEngine.Movement.cs, and DragEngine.Diagnostics.cs.</remarks>
internal sealed partial class DragEngine : IDisposable
{
	// Immutable snapshot of the active drag. Readers use it without locking; only writers hold sync.
	private sealed record DragState(object Owner, WindowDragController.DragSession? Session);

	// Current drag, or null when idle. Set and cleared only under sync.
	private volatile DragState? state;
	// True while TryStartDrag is classifying the target outside the lock. Blocks a second start.
	private bool starting;
	// True when this engine pressed the synthetic drag buttons and must release them.
	private bool syntheticButtonsDown;
	// Repeats every 25 ms to check whether a pending window-position fallback is due.
	private readonly System.Timers.Timer fallbackTimer = new(PointerSettings.GetDragFallbackPollIntervalMs()) { AutoReset = true };
	private readonly object sync = new();

	/// <summary>Creates the engine and wires the fallback timer.</summary>
	public DragEngine()
	{
		// Each tick checks the fallback. If the title-bar drag still did not respond, the timer
		// stops and the fallback indicator is shown.
		fallbackTimer.Elapsed += (_, _) =>
		{
			lock (sync)
			{
				var current = state?.Session;
				if (current is not { MoveWithoutForeground: false, UsingWindowPositionFallback: false } || !WindowDragController.TryCompleteWindowPositionFallback(current)) return;
				fallbackTimer.Stop();
				ShowForcedWindowModeIndicator();
				CursorDiagnosticLogger.Record("drag_engine", "fallback_indicator_shown_by_timer", details: new { windowHandle = current.WindowHandle.ToInt64() });
			}
		};
	}

	/// <summary>Returns the active session if <paramref name="owner"/> owns the drag; otherwise null.</summary>
	public WindowDragController.DragSession? GetSession(object owner)
	{
		var current = state;
		return current is not null && ReferenceEquals(current.Owner, owner) ? current.Session : null;
	}

	/// <summary>True while any owner holds a drag, including one still being classified.</summary>
	public bool HasActiveDrag => state is not null;

	/// <summary>True when a drag exists and belongs to a different owner.</summary>
	public bool HasActiveDragFromOther(object owner)
	{
		var current = state;
		return current is not null && !ReferenceEquals(current.Owner, owner);
	}

	/// <summary>Ends any active drag and stops the fallback timer.</summary>
	public void Dispose()
	{
		var hadActiveDrag = HasActiveDrag;
		EndCurrentDrag("engine_disposed");
		fallbackTimer.Dispose();
		CursorDiagnosticLogger.Record("drag_engine", "disposed", details: new { hadActiveDrag });
	}
}
