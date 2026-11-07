namespace ThreeFingerDrag;

internal static partial class PointerSettings
{
	// Touch, mouse, pen, and drag-timing accessors. Each returns one configured value from the cached snapshot.
	public static bool IsTouchHoldForceWindowDragEnabled() => GetConfiguredSettings().EnableTouchHoldForceWindowDrag;
	public static bool IsClickHoldForceWindowDragEnabled() => GetConfiguredSettings().EnableClickHoldForceWindowDrag;
	public static bool IsStylusEnabled() => GetConfiguredSettings().EnableStylus;
	public static bool IsStylusHoldForceWindowDragEnabled() => GetConfiguredSettings().EnableStylusHoldForceWindowDrag;
	public static int GetDragHoldDurationMs() => GetConfiguredSettings().DragHoldDurationMs;
	public static string GetDragCancelKey() => GetConfiguredSettings().DragCancelKey;
	public static int GetTouchpadDragDropAnchorHoldMs() => GetConfiguredSettings().TouchpadDragDropAnchorHoldMs;
	public static int GetTouchpadPostLiftAnchorGraceMs() => GetConfiguredSettings().TouchpadPostLiftAnchorGraceMs;
	public static bool IsTouchFlickToDropEnabled() => GetConfiguredSettings().EnableTouchFlickToDrop;
	public static int GetTouchFlickToDropVelocityThreshold() => GetConfiguredSettings().TouchFlickToDropVelocityThreshold;
	public static int GetTouchpadNaturalDragReleaseThreshold() => GetConfiguredSettings().TouchpadNaturalDragReleaseThreshold;
	public static bool IsDragReleaseSoundEnabled() => GetConfiguredSettings().EnableDragReleaseSound;
	public static int GetCursorPositionPollingIntervalMs() => GetConfiguredSettings().CursorPositionPollingIntervalMs;
	public static bool IsDiagnosticLogEnabled() => GetConfiguredSettings().EnableDiagnosticLog;

	public static bool IsSplitDiagnosticLogEnabled() => GetConfiguredSettings().EnableSplitDiagnosticLogs;
}
