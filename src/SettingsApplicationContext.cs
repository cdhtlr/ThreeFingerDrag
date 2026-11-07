namespace ThreeFingerDrag;

/// <summary>Keeps the settings UI message loop alive and shows the settings window when another launch requests it.</summary>
internal sealed class SettingsApplicationContext : ApplicationContext
{
	// The context retains the form and event for as long as the UI message loop runs.
	private readonly SettingsWindow settingsWindow;
	private readonly EventWaitHandle showSettingsEvent;
	private readonly System.Windows.Forms.Timer eventPoller;

	/// <summary>Stores the settings window and event, and starts polling the event on the UI thread.</summary>
	public SettingsApplicationContext(SettingsWindow settingsWindow, EventWaitHandle showSettingsEvent)
	{
		this.settingsWindow = settingsWindow;
		this.showSettingsEvent = showSettingsEvent;
		eventPoller = new System.Windows.Forms.Timer { Interval = PointerSettings.GetSettingsEventPollIntervalMs() };
		// Polling lets a second process request activation without cross-thread form access.
		eventPoller.Tick += (_, _) =>
		{
			if (!this.showSettingsEvent.WaitOne(0)) return;
			CursorDiagnosticLogger.Record("settings_application", "show_requested");
			this.settingsWindow.ShowAndActivate();
		};
		eventPoller.Start();
		CursorDiagnosticLogger.Record("settings_application", "poller_started", details: new { intervalMs = eventPoller.Interval });
	}
}
