namespace ThreeFingerDrag;

/// <summary>Starts the single application instance, settings window, and device input services.</summary>
internal static partial class Program
{
	// DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 (-4)
	private static readonly nint PerMonitorAwareV2Context = -4;

	/// <summary>Entry point. Enables DPI awareness, then runs startup and reports any failure to the user.</summary>
	[STAThread]
	private static void Main(string[] args)
	{
		// Must run before any window, cursor, or screen query so coordinates are physical pixels on scaled displays.
		CursorDiagnosticLogger.Record("app_startup", "main_start");
		EnableDpiAwareness();
		try
		{
			RunStartup(args.Contains("--show-settings", StringComparer.Ordinal));
			CursorDiagnosticLogger.Record("app_startup", "main_end");
		}
		catch (Exception ex)
		{
			CursorDiagnosticLogger.Record("app_startup", "exception", details: new { exceptionType = ex.GetType().FullName, message = ex.Message });
			ShowStartupFailedNotification(ex);
		}
	}

	/// <summary>Opts the process into DPI awareness so Win32 coordinates are not scaled by Windows.</summary>
	private static void EnableDpiAwareness()
	{
		// Per-monitor V2 needs Windows 10 1703+; older builds fall back to system-DPI awareness.
		if (!NativeMethods.SetProcessDpiAwarenessContext(PerMonitorAwareV2Context))
		{
			var systemAwareResult = NativeMethods.SetProcessDPIAware();
			CursorDiagnosticLogger.Record("app_dpi_awareness", "fallback_system_dpi", details: new { systemAwareResult });
		}
		else
		{
			CursorDiagnosticLogger.Record("app_dpi_awareness", "per_monitor_v2_enabled");
		}
	}

	/// <summary>Runs the single-instance check, then starts the settings UI and the raw-input services in order.</summary>
	private static void RunStartup(bool showSettings)
	{
		// Load settings first so every component created below reads the same configuration.
		PointerSettings.Initialize();

		// Create/open the event before checking the mutex so a second launch can always
		// signal the first instance, even while that instance is still starting up.
		using EventWaitHandle showSettingsEvent = new(false, EventResetMode.AutoReset, ShowSettingsEventName);
		// Only the process that creates the mutex owns the input services.
		using Mutex singleInstanceMutex = new(true, InstanceMutexName, out var createdNew);
		if (!createdNew)
		{
			// A second launch is a request to open the settings UI, not a startup failure.
			var signaled = showSettingsEvent.Set();
			CursorDiagnosticLogger.Record("app_single_instance", "secondary_launch_forwarded", details: new { signaled });
			return;
		}
		CursorDiagnosticLogger.Record("app_single_instance", "primary_instance");
		if (showSettings)
			showSettingsEvent.Set();

		// Start shortcut monitoring before any gesture can request a forced mode.
		ForcedDragModeService.Initialize();
		CursorDiagnosticLogger.StartSession();
		CursorDiagnosticLogger.Record("app_startup", "services_initialized");

		// WinForms owns its message loop on a dedicated STA thread; raw input runs separately.
		Thread settingsThread = new(() =>
		{
			try
			{
				CursorDiagnosticLogger.Record("app_settings_ui", "thread_start");
				ApplicationConfiguration.Initialize();
				SettingsWindow settingsWindow = new();
				Application.Run(new SettingsApplicationContext(settingsWindow, showSettingsEvent));
				CursorDiagnosticLogger.Record("app_settings_ui", "message_loop_exited");
			}
			catch (Exception ex)
			{
				// Without the settings UI the app is not usable, so stop the whole process after notifying.
				CursorDiagnosticLogger.Record("app_settings_ui", "exception", details: new { exceptionType = ex.GetType().FullName, message = ex.Message });
				ShowStartupFailedNotification(ex);
				CursorDiagnosticLogger.Record("app_settings_ui", "exit_process", details: new { exitCode = 1 });
				Environment.Exit(1);
			}
		})
		{ IsBackground = true, Name = "Settings GUI" };
		settingsThread.SetApartmentState(ApartmentState.STA);
		settingsThread.Start();

		// Keep input processors alive for the lifetime of the message loop. The shared drag engine
		// is created first, and each processor gets it so all input sources drive the same drag state.
		using DragEngine dragEngine = new();
		using TouchProcessor touchProcessor = new(dragEngine);
		using MouseDragProcessor mouseDragProcessor = new(dragEngine);
		using StylusProcessor stylusProcessor = new(dragEngine);
		using TouchscreenProcessor touchscreenProcessor = new(dragEngine);
		using InputMessageLoop inputMessageLoop = new(lParam => OnInputEvent(touchProcessor, stylusProcessor, touchscreenProcessor, lParam));
		// Samples the cursor position on a timer for diagnostic logging only. It starts immediately (due time 0).
		using System.Threading.Timer cursorPositionPoller = new(_ => CursorDiagnosticLogger.PollCursorPosition(), null, 0, PointerSettings.GetCursorPositionPollingIntervalMs());
		// The cursor-movement guard hook must be installed from (and torn down on) the message-loop
		// thread itself - see MessageLoopThreadStarted/Stopping - since low-level Win32 hooks are only
		// ever delivered to the thread that installed them.
		inputMessageLoop.MessageLoopThreadStarted += MouseInject.InstallCursorMovementGuard;
		inputMessageLoop.MessageLoopThreadStopping += MouseInject.UninstallCursorMovementGuard;

		// Create the message-only window first. Its handle is then used to register each device
		// class for raw input, so WM_INPUT messages are routed to the loop.
		var hwnd = inputMessageLoop.Create();
		TouchProcessor.Register(hwnd);
		StylusProcessor.Register(hwnd);
		TouchscreenProcessor.Register(hwnd);
		CursorDiagnosticLogger.Record("app_input_services", "raw_input_registered", details: new { hwnd });

		// Block the main thread until the input loop exits. Otherwise the using blocks would dispose
		// the processors while they are still in use.
		inputMessageLoop.HwndThread?.Join();
		CursorDiagnosticLogger.Record("app_input_services", "stopped");
	}
}
