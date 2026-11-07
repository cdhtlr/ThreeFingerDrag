# ThreeFingerDrag

ThreeFingerDrag is a Windows utility that lets you drag windows and UI
elements with three fingers on a touchpad or touchscreen, or with the mouse
and stylus when enabled. It is written in C# for .NET 8 (Windows Forms).

For Indonesian, see the [panduan Bahasa Indonesia](../id/panduan.md).

## Requirements

- Windows 10 version 1607 (build 14393) or later.
- The matching release build for your CPU: `win-x64`, `win-x86`, or `win-arm64`.

## Installation

1. Download and run the setup executable for your CPU (`win-x64`, `win-x86`,
   or `win-arm64`).
2. Approve UAC to install the app and register its elevated startup task.
3. After you choose the installation directory, setup installs immediately and
   opens the settings window automatically. Closing it hides the window.
   The program keeps running in the background.

The installer requests UAC approval to register a scheduled task for the
administrator account that installed it. The task starts the app with highest
privileges at that account's sign-in without another UAC prompt.
Launching `ThreeFingerDrag.exe` directly still prompts for UAC, and uninstalling
removes the task and may request UAC. The installers produced by the build script are unsigned,
Windows may show SmartScreen or Defender warnings.

Run setup again to choose reinstall/update or uninstall. Uninstall deletes
the current administrator account's `%LOCALAPPDATA%\ThreeFingerDrag` settings,
translations, and logs. Back them up first if needed.

The program has no persistent tray icon. It only shows a balloon notification
when startup fails. To open the settings window again, use the ThreeFingerDrag
Start menu shortcut. The second launch signals the running instance to show
its settings and then exits.

## How to use

### Basic gesture: drag with three fingers

1. Place three fingers on a touchpad (or touchscreen) and move them.
2. The program checks the item under the fingers and decides whether to move
   the **window** or the **element** (see
   [How the target is chosen](#how-the-target-is-chosen)).
3. Lift the fingers to drop the item.

The number of fingers is set with `touchpadDragFingerCount` and
`touchscreenDragFingerCount` (default: 3).

If you add more fingers than the configured count while dragging, the drag
pauses. It resumes when the count returns to the configured number. If the
count drops below the configured number while paused, the drag ends.

### Window drag vs element drag

- **Window drag**: moves the whole window. Use it on window background, title
  bars, borders, and empty areas.
- **Element drag**: sends the drag to the content under the cursor, for example
  to move a file in Explorer or a tab in a browser.

### Trigger a window drag on a one-color area

If the area under the cursor has a single solid color (for example, an empty
panel or a plain background), the program treats it as window background and
starts a window drag. If the sampled area contains mixed colors (text, icons,
images), it treats it as interactive content and starts an element drag.
Turn this check on or off with `enableSolidColorDetection`.

### Force a window drag by holding still

Hold still on an element with three fingers to force a window drag. The
`showTouchHoldForceWindowDragIndicator` setting shows a short indicator when
this happens. It applies to touchpad and touchscreen only.

### Force a drag mode with a key

- Hold `forceWindowDragKey` (default: left Ctrl) when the drag starts to force a
  window drag.
- Hold `forceNonWindowDragKey` (default: right Ctrl) when the drag starts to
  force an element drag.

Priority: the window key wins over the element key. Hold keys are checked
before the touch/mouse/stylus hold timer.

### Cycle the drag override with a shortcut

Press `dragModeSwitcherShortcut` (default: `LControl+LAlt+W`) to cycle the
override in this order: off, then move-window, then element-drag, then off. Each
press is detected once. Holding the shortcut does not repeat. Turn the sound
with `enableForcedModeToggleSound`.

### Cancel a drag

Press `dragCancelKey` (default: Escape) while dragging to cancel. Cancel is
supported for touchpad, touchscreen, and mouse. It is not checked for stylus.

### Move the foreground window with the keyboard

Hold `windowMoveModifierKey` (default: left Alt) and press the arrow keys to
move the foreground window. Each press moves the window by
`windowMoveStepPixels` (default: 24 px). Quick repeated presses increase the
step size.

### Flick to drop

On touchpad and touchscreen, a quick flick of the three fingers while dragging
drops the item with momentum. Control the speed needed with
`touchFlickToDropVelocityThreshold`, or disable it with
`enableTouchFlickToDrop`.

### Mouse drag

Hold `mouseDragTriggerButton` (default: `Secondary`, the right button) and move
the mouse to drag. Holding the button still for `dragHoldDurationMs` forces a
window drag when `enableClickHoldForceWindowDrag` is on.

### Stylus

The stylus is off by default. Set `enableStylus` to `true` to use it.
Holding still forces a window drag when `enableStylusHoldForceWindowDrag` is on.

### OCR text capture

Hold still on text with three fingers (touchpad or touchscreen), or hold the
mouse button or stylus still near the press point, to start OCR capture. The
hold time for all four input types is `ocrHoldDurationMs` (default: 500 ms).
The capture covers a radius of `ocrCaptureRadius` pixels (default: 250). Words
are grouped using `ocrMaximumWordGapPercent`. Set `ocrCopyToClipboard` to `true` to copy the text
to the clipboard. The OCR language is chosen with `ocrLanguageTag`. When empty,
the Windows OCR default is used.

## Input behavior by device

| Behavior | Touchpad | Touchscreen | Mouse | Stylus |
|---|---|---|---|---|
| Three-finger drag | Yes (`touchpadDragFingerCount`) | Yes (`touchscreenDragFingerCount`) | No | No |
| Hold to force window drag | Yes (`enableTouchHoldForceWindowDrag`) | Yes (`enableTouchHoldForceWindowDrag`) | Yes (`enableClickHoldForceWindowDrag`) | Yes (`enableStylusHoldForceWindowDrag`) |
| Flick to drop | Yes | Yes | No | No |
| Drop anchor after hold (`touchpadDragDropAnchorHoldMs`) | Yes | No | No | No |
| Post-lift anchor grace (`touchpadPostLiftAnchorGraceMs`) | Yes | No | No | No |
| Cancel with `dragCancelKey` | Yes | Yes | Yes | No |
| Requires `enableStylus` | No | No | No | Yes |

Mouse drag starts from `mouseDragTriggerButton`, not from finger count.

## How the target is chosen

The program checks the item under the cursor with these detectors. The first
detector that gives a decisive answer wins. If none does, the item is treated as
a non-window (element) drag.

1. **Hit test**: the system hit-test result. Window chrome (caption, borders,
   sizing edges) means window, control results mean element.
2. **Cursor shape**: an arrow or resize cursor means window, an I-beam means
   text (element), unless `enableEmptyIBeamWindowDrag` treats an empty I-beam
   area as window.
3. **UI Automation**: the accessibility tree is queried for the element type.
4. **Control class name**: known window-class names are matched.
5. **Solid color**: a uniform sample means window background, a mixed sample
   means element content.

Turn each detector on or off with the `enable...Detection` keys in the
`detection` section.

## Settings

Settings are saved in `%LOCALAPPDATA%\ThreeFingerDrag\ThreeFingerDrag.config.json`.
On first launch, an existing config next to the executable is copied there when
available. Use the settings window to edit it, or edit the file directly. The
program reloads the file while it runs (checked at most every 250 ms). Editable
language files are stored in the `Languages` subfolder at the same location.

- Missing keys use the default values.
- Numeric values outside the allowed range are clamped to it.
- The `language` key selects the UI language. When empty, the Windows UI
  language is used. Missing translation keys fall back to English.

### Configuration reference

See the [configuration reference](configuration.md).

## Logs

When `enableDiagnosticLog` is `true`, the program writes
`%LOCALAPPDATA%\ThreeFingerDrag\logs\logs.jsonl`. When
`enableSplitDiagnosticLogs` is also `true`, each program section writes its own
file instead, for example `cursor-diagnostics.jsonl` or
`clipboard-writer.jsonl` in that `logs` folder. Every entry has a `section`
field that names its source. A `session.running` marker is created in the
`logs` folder at
startup and removed on a clean exit. If the marker is still present on the
next start, the previous session ended unexpectedly.

## Languages

Translations are stored in `src\Languages\en.json` and `src\Languages\id.json`. They
are embedded in the executable. The dropdown in the settings window lists the
available languages. Each file uses the `languageName` key as its display name.

## Building

Requirements: .NET 8 SDK and Windows.

```powershell
dotnet build src\ThreeFingerDrag.csproj -c Release -p:Platform=x64
```

To publish all three CPU targets into `publish\`:

```powershell
.\publish-all.ps1
```

The project creates `Logo.ico` from `src\Logo.png` during the build.

## License

See [LICENSE](../../LICENSE). Third-party components are listed in
[THIRD-PARTY-NOTICES.md](../../THIRD-PARTY-NOTICES.md).
