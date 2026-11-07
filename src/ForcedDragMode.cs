namespace ThreeFingerDrag;

/// <summary>Overrides available for the next three-finger drag.</summary>
internal enum ForcedDragMode
{
	/// <summary>No override active; run the normal classifier chain.</summary>
	None,

	/// <summary>Always move the window, regardless of what is under the cursor.</summary>
	Window,

	/// <summary>Always pass the gesture through as a normal element/mouse drag.</summary>
	NonWindow,
}

/// <summary>A forced mode and its display label, if one is active.</summary>
internal readonly record struct ForcedDragModeResult(ForcedDragMode Mode, string? Reason, bool FromHold = false)
{
	public static readonly ForcedDragModeResult None = new(ForcedDragMode.None, null);
}
