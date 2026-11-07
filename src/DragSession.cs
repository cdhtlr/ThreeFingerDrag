namespace ThreeFingerDrag;

internal static partial class WindowDragController
{
	/// <summary>
	/// State for one window drag. It holds the start position, the smoothing target and applied
	/// offsets, and the fallback bookkeeping. Accessed under <see cref="SmoothingLock"/> where noted.
	/// </summary>
	internal sealed class DragSession
	{
		public required nint WindowHandle { get; init; }
		public required Rectangle InitialBounds { get; set; }
		// True when the window is moved by position only, without activating it.
		public required bool MoveWithoutForeground { get; init; }
		// True after the title-bar drag was judged unresponsive and SetWindowPos took over.
		public bool UsingWindowPositionFallback { get; set; }

		/// <summary>Position last sent to SetWindowPos. Used only to detect moves made by something else.</summary>
		public Point? LastRequestedPosition { get; set; }
		// True once the title-bar drag has moved the window in this session. Later stalls, such as the cursor reaching a screen edge, do not trigger the fallback.
		public bool HasMovedNatively { get; set; }
		public bool FallbackCheckPending { get; set; }
		// Earliest time the fallback decision may be made, giving the title-bar drag time to respond.
		public DateTime FallbackCheckDeadlineUtc { get; set; }
		public double PendingFallbackHorizontalMovement { get; set; }
		public double PendingFallbackVerticalMovement { get; set; }
		// Window position when the pending movement started. Used to subtract what the title-bar drag already moved.
		public Point PendingFallbackStartPosition { get; set; }
		public Rectangle LastObservedBounds { get; set; }
		public double PendingHorizontalMovement { get; set; }
		public double PendingVerticalMovement { get; set; }
		// Offset from InitialBounds that the window has actually reached.
		public int AppliedHorizontalOffset { get; set; }
		public int AppliedVerticalOffset { get; set; }
		// Offset the smoothing timer is easing toward.
		public int TargetHorizontalOffset { get; set; }
		public int TargetVerticalOffset { get; set; }
		public double InputSpeedPxPerTick { get; set; }
		public long LastInputTimestamp { get; set; }
		public long LastSmoothingTimestamp { get; set; }
		public System.Timers.Timer? SmoothingTimer { get; set; }
		public object SmoothingLock { get; } = new();
		// True when the cursor was placed on a real caption area. False means the window was positioned directly.
		public bool TitleBarPointConfirmed { get; init; }
	}
}
