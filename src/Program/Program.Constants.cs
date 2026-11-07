namespace ThreeFingerDrag;

internal static partial class Program
{
	// Named mutex that marks the app as running. Only the first process that creates it starts the input services.
	private const string InstanceMutexName = "three-finger-drag";
	// Auto-reset event. A later launch sets it, and the settings window polls it and shows itself.
	private const string ShowSettingsEventName = "Local\\three-finger-drag-show-settings";
}