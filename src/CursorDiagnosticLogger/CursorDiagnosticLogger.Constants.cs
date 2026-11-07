namespace ThreeFingerDrag;

internal static partial class CursorDiagnosticLogger
{
	// GetAncestor flag GA_ROOT: return the top-level window that owns the given window.
	private const uint GaRoot = 2;
	// File name used when split logging is off. Split mode uses <section>.jsonl instead.
	private const string SingleLogFileName = "logs.jsonl";
}