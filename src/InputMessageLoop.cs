using System.Diagnostics;
using System.Runtime.InteropServices;
using static ThreeFingerDrag.NativeMethods;

namespace ThreeFingerDrag;

/// <summary>Holds the native window and message pump that receives WM_INPUT on a dedicated thread.</summary>
public sealed class InputMessageLoop : IDisposable
{
	/// <summary>Delegate for the callback that receives each WM_INPUT lParam.</summary>
	public delegate void InputMessageEvent(IntPtr lParam);

	// Held in a field so the native window class keeps a live callback for the window's lifetime.
	private readonly WndProcDelegate windowProc;

	/// <summary>Creates the loop and subscribes <paramref name="handler"/> to raw-input messages. The window is not created yet.</summary>
	/// <param name="handler">Called on the message-loop thread for each WM_INPUT message.</param>
	public InputMessageLoop(InputMessageEvent handler)
	{
		// A unique class name avoids clashes when more than one loop exists in the process.
		WindowId = "TFD_" + Guid.NewGuid();
		windowProc = WndProc;
		InputMessageReceived += handler;
	}

	/// <summary>Unique window class name for this loop.</summary>
	internal string WindowId { get; }
	/// <summary>The thread that runs the message loop, or null before <see cref="Create"/>.</summary>
	public Thread? HwndThread { get; private set; }
	/// <summary>True once the native window handle has been created.</summary>
	public bool IsCreated => Handle != 0;
	/// <summary>The native window handle, or zero after the loop is stopped.</summary>
	public nint Handle { get; private set; }
	/// <summary>True after <see cref="Dispose"/> has run.</summary>
	public bool IsDisposed { get; private set; }

	/// <summary>Raised on the message-loop thread for every WM_INPUT message.</summary>
	public event InputMessageEvent InputMessageReceived;

	/// <summary>Raised on the message-loop thread just before it starts pumping messages.</summary>
	public event Action? MessageLoopThreadStarted;

	/// <summary>Raised on the message-loop thread after pumping stops, for same-thread cleanup.</summary>
	public event Action? MessageLoopThreadStopping;

	/// <summary>Default window procedure. Raw input is read from WM_INPUT in the loop, so nothing special is handled here.</summary>
	private nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam)
	{
		return DefWindowProc(hwnd, msg, wParam, lParam);
	}

	/// <summary>Starts the message-loop thread, creates the window there, and waits until the handle exists.</summary>
	/// <returns>The window handle, which is zero if creation failed.</returns>
	public nint Create()
	{
		using ManualResetEvent windowCreated = new(false);

		// The window must be created on the thread that pumps its messages, so the whole setup runs in this thread.
		HwndThread = new Thread(() =>
		{
			WNDCLASSEX wndClass = new()
			{
				cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
				lpfnWndProc = windowProc,
				lpszClassName = WindowId,
			};

			// A failed registration or window creation throws, and the message is taken from the last Win32 error.
			var result = RegisterClassEx(ref wndClass);
			if (result == 0)
			{
				var message = Marshal.GetLastPInvokeErrorMessage();
				CursorDiagnosticLogger.Record("input_message_loop", "exception", details: new { exceptionType = "RegisterClassEx", message, windowId = WindowId });
				throw new Exception(message);
			}

			var hwnd = CreateWindowEx(0, WindowId, null, 0, 0, 0, 0, 0, nint.Zero, nint.Zero, nint.Zero, nint.Zero);
			if (hwnd == 0)
			{
				var message = Marshal.GetLastPInvokeErrorMessage();
				CursorDiagnosticLogger.Record("input_message_loop", "exception", details: new { exceptionType = "CreateWindowEx", message, windowId = WindowId });
				throw new Exception(message);
			}

			// Signal the caller only after the handle exists, so Create() never returns a half-built loop.
			Handle = hwnd;
			CursorDiagnosticLogger.Record("input_message_loop", "window_created", details: new { windowId = WindowId, hwnd });
			_ = windowCreated.Set();

			// Cleanup runs on the same thread that created the window, as DestroyWindow requires.
			MessageLoopThreadStarted?.Invoke();
			RunMessageLoop();
			MessageLoopThreadStopping?.Invoke();
			_ = DestroyWindow(hwnd);
			CursorDiagnosticLogger.Record("input_message_loop", "window_destroyed", details: new { windowId = WindowId });
		});

		CursorDiagnosticLogger.Record("input_message_loop", "thread_starting", details: new { windowId = WindowId });
		HwndThread.Start();
		_ = windowCreated.WaitOne();
		return Handle;
	}

	/// <summary>Posts WM_QUIT to the loop window so the message thread exits. Safe to call more than once.</summary>
	public void StopMessageLoop()
	{
		if (Handle == 0) return;

		CursorDiagnosticLogger.Record("input_message_loop", "stop_requested", details: new { windowId = WindowId });
		Debug.WriteLine("sent quit");
		_ = PostMessage(Handle, WM_QUIT, 0, 0);
		Handle = nint.Zero;
	}

	/// <summary>Stops the loop and marks the object disposed. Also called from the finalizer.</summary>
	public void Dispose()
	{
		if (IsDisposed) return;

		StopMessageLoop();
		IsDisposed = true;
		CursorDiagnosticLogger.Record("input_message_loop", "disposed", details: new { windowId = WindowId });
		GC.SuppressFinalize(this);
	}

	~InputMessageLoop() => Dispose();

	/// <summary>Pumps messages until WM_QUIT or an error. WM_INPUT goes to the handler before dispatch.</summary>
	private void RunMessageLoop()
	{
		Debug.WriteLine("InputHwnd: message loop started");
		var quit = false;
		while (!quit)
		{
			// GetMessage returns 0 on WM_QUIT and -1 on error. Both end the loop.
			var result = GetMessage(out MSG msg, IntPtr.Zero, 0, 0);
			if (result is 0 or -1)
			{
				if (result == -1) CursorDiagnosticLogger.Record("input_message_loop", "get_message_failed", details: new { lastError = Marshal.GetLastPInvokeError() });
				break;
			}

			if (msg.message == WM_QUIT)
			{
				Debug.WriteLine("InputHwnd: got WM_QUIT");
				CursorDiagnosticLogger.Record("input_message_loop", "quit_received");
				quit = true;
			}

			if (msg.message == WM_INPUT) InputMessageReceived.Invoke(msg.lParam);

			_ = TranslateMessage(ref msg);
			_ = DispatchMessage(ref msg);
		}

		Debug.WriteLine("InputHwnd: message loop exited");
	}
}
