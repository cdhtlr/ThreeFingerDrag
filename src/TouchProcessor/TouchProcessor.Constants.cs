namespace ThreeFingerDrag;

public sealed partial class TouchProcessor
{
	/// <summary>Cursor movement, in pixels, that releases the drop anchor as real user input.</summary>
	private const int DragDropAnchorMovementResetThresholdPx = 3;
	/// <summary>Declared for the drop anchor grace window. Not referenced by the current code.</summary>
	private static int DragDropAnchorGraceWindowMs => PointerSettings.GetTouchDragDropAnchorGraceMs();

	// Grace period for an accidental finger lift during a drag. The drag ends only if the
	// fingers stay off the pad for FingerLiftGraceMs.
	private static int FingerLiftGraceMs => PointerSettings.GetTouchFingerLiftGraceMs();
}