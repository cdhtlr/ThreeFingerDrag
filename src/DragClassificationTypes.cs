namespace ThreeFingerDrag;

/// <summary>
/// Result of inspecting whatever is directly under the cursor at the start of a three-finger drag gesture.
/// </summary>
internal enum DragTargetClassification
{
	/// <summary>No conclusion reached; the next classifier in the chain should be tried.</summary>
	Undetermined,

	/// <summary>The cursor is over empty window chrome/background; the window may be moved.</summary>
	EmptyBackground,

	/// <summary>The element is known not to be draggable; move the window.</summary>
	InteractiveElement,

	/// <summary>The element explicitly reports support for drag; pass the gesture through.</summary>
	DraggableElement,
}

/// <summary>A classification result and the method that produced it, if any.</summary>
internal readonly record struct DragClassificationOutcome(DragTargetClassification Classification, string? Method)
{
	/// <summary>An outcome with no decisive result. Callers treat it as not draggable chrome.</summary>
	public static DragClassificationOutcome Undetermined { get; } = new(DragTargetClassification.Undetermined, null);
}
