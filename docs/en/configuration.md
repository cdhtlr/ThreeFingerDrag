# Configuration reference

[Back to the English guide](guide.md).


## global

| Key | Default | Range | Description |
|---|---|---|---|
| `cursorSpeed` | 20 | 1–20 | Windows pointer speed applied during drag (set through the system pointer setting). |
| `windowPositionSpeed` | 5 | 1–100 | Forced window drag speed multiplier. Independent of cursor speed. |
| `cursorPositionPollingIntervalMs` | 1 | 1–1000 | Interval for the cursor position timer. Used for diagnostics only. |
| `bringDraggedWindowToForeground` | true | true/false | Bring the dragged window to the front when a window drag starts. |
| `keepWindowWithinScreen` | true | true/false | Keep the dragged window inside the visible screen area. |
| `dragHoldDurationMs` | 1000 | 500–10000 | Time to hold still before a window drag is forced. |
| `dragCancelKey` | Escape | key name | Key that cancels a drag. |
| `forceWindowDragKey` | LControl | key name | Hold to force a window drag. |
| `forceNonWindowDragKey` | RControl | key name | Hold to force an element drag. |
| `dragModeSwitcherShortcut` | LControl+LAlt+W | shortcut | Shortcut that cycles the override: off → move-window → element-drag → off. |
| `enableTouchFlickToDrop` | true | true/false | Enable flick-to-drop on touchpad and touchscreen. |
| `touchFlickToDropVelocityThreshold` | 18 | 6–100 | Velocity needed for a flick to drop. |
| `touchpadNaturalDragReleaseThreshold` | 5 | 2–30 | Movement threshold for a natural release on the touchpad. |
| `touchpadDragDropAnchorHoldMs` | 150 | 50–1000 | Time to hold the drop anchor on the touchpad. |
| `touchpadPostLiftAnchorGraceMs` | 180 | 50–1000 | Time after lifting the fingers during which the drop anchor still applies (touchpad). |
| `enableDragReleaseSound` | true | true/false | Play a sound when a drag is released. |
| `enableForcedModeToggleSound` | true | true/false | Play a sound when the forced mode changes. |
| `enableDiagnosticLog` | false | true/false | Write logs to `%LOCALAPPDATA%\ThreeFingerDrag\logs\logs.jsonl` (or per-section files when `enableSplitDiagnosticLogs` is on). Lives in the `logging` section. |
| `enableSplitDiagnosticLogs` | false | true/false | Split logs into one file per program section (for example `cursor-diagnostics.jsonl`, `clipboard-writer.jsonl` in the logs folder). Off writes everything to `logs.jsonl`. |
| `dragFallbackPollIntervalMs` | 25 | 1-1000 | How often the drag engine checks the cursor as a fallback (ms). |
| `windowDragSmoothingFrameMs` | 8 | 1-100 | Frame length used to smooth window movement (ms). |
| `windowDragSmoothingMinTimeConstantMs` | 40 | 1-1000 | Shortest smoothing time constant (ms). |
| `windowDragSmoothingMaxTimeConstantMs` | 250 | 1-5000 | Longest smoothing time constant (ms). |
| `windowDragSmoothingMaxElapsedMs` | 50 | 1-1000 | Longest gap between updates that is still smoothed (ms). |
| `settingsInputDebounceMs` | 400 | 0-5000 | Delay after typing a number before it is applied (ms). |
| `settingsStatIntervalMs` | 250 | 10-5000 | How often the config file is checked for changes (ms). |
| `notificationSafetyTimeoutMs` | 7000 | 1000-60000 | Safety limit for the tray balloon notification message loop (ms). |
| `notificationBalloonTimeoutMs` | 5000 | 1000-60000 | How long the tray balloon notification is shown (ms). |
| `settingsEventPollIntervalMs` | 100 | 10-5000 | How often the settings app checks for a request to show its window (ms). |
| `settingsCaptureRefreshIntervalMs` | 25 | 5-1000 | How often the settings window refreshes the button capture state (ms). |

## detection

| Key | Default | Range | Description |
|---|---|---|---|
| `enableSolidColorDetection` | true | true/false | Use the solid color detector. |
| `enableUiAutomationDetection` | true | true/false | Use the UI Automation detector. |
| `enableOcrCapture` | true | true/false | Allow OCR text capture. |
| `enableCursorShapeDetection` | true | true/false | Use the cursor shape detector. |
| `enableEmptyIBeamWindowDrag` | true | true/false | Treat an empty I-beam area as a window drag. |
| `enableControlClassNameDetection` | true | true/false | Use the control class name detector. |
| `enableHitTestDetection` | true | true/false | Use the hit-test detector. |
| `iBeamTextDetectionAreaSize` | 51 | 1–501 | Sample size (pixels) for the I-beam text check. |
| `solidColorDetectionRectangleSize` | 5 | 1–51 | Sample size (pixels) for the solid color check. |
| `hitTestTimeoutMs` | 150 | 10-5000 | Maximum wait for a window to answer a hit test (ms). Protects against hung apps. |
| `uiAutomationClassifyTimeoutMs` | 200 | 10-5000 | Maximum wait for UI Automation to classify an element (ms). |
| `titleBarHitTestTimeoutMs` | 20 | 1-1000 | Maximum wait for the title bar check (ms). |

## indicators

| Key | Default | Range | Description |
|---|---|---|---|
| `showTouchHoldForceWindowDragIndicator` | false | true/false | Show an indicator when a touch hold forces a window drag (touchpad and touchscreen only). |
| `touchHoldForceWindowDragIndicatorDisplayMs` | 900 | 100–5000 | How long that indicator stays visible. |
| `showDragModeIndicator` | false | true/false | Show the current drag mode. |
| `dragModeIndicatorDiameter` | 14 | 6–80 | Diameter of the drag mode indicator. |
| `dragModeIndicatorDisplayMs` | 900 | 100–5000 | How long the drag mode indicator stays visible. |
| `windowDragIndicatorColor` | #4060FF | color | Color for window drags. |
| `otherDragIndicatorColor` | #FF4040 | color | Color for element drags. |
| `windowPositionFallbackIndicatorColor` | #800080 | color | Color for forced window drag mode (set window position), both when forced from the start and when switched from normal. |
| `touchHoldForceWindowDragIndicatorColor` | #00C8C8 | color | Color for touch-hold forced window drags. |
| `showSolidColorDetectionRectangle` | false | true/false | Show the sampled rectangle. |
| `solidColorUniformBorderColor` | #40C878 | color | Border color when the sample is uniform. |
| `solidColorMixedBorderColor` | #FFB020 | color | Border color when the sample is mixed. |
| `solidColorDetectionRectangleDisplayMs` | 500 | 100–5000 | How long the sample rectangle stays visible. |
| `showDragModeIndicatorDetails` | false | true/false | Show extra detail in the drag mode indicator. |
| `indicatorPanelBackgroundColor` | #202020 | color | Background color of the indicator panel. |
| `indicatorPanelTextColor` | #FFFFFF | color | Text color of the indicator panel. |

## ocr

| Key | Default | Range | Description |
|---|---|---|---|
| `ocrHoldDurationMs` | 500 | 150–3000 | Hold time before OCR capture starts. |
| `ocrCaptureRadius` | 250 | 50–1000 | Capture radius in pixels. |
| `ocrLanguageTag` | (empty) | BCP-47 tag | OCR language. Empty uses the Windows OCR default. |
| `ocrMaximumWordGapPercent` | 65 | 10–200 | Maximum gap between words, as a percent of word size. |
| `ocrCopyToClipboard` | false | true/false | Copy the captured text to the clipboard. |
| `ocrActivationDelayMs` | 75 | 0-2000 | Wait after activating the target window before clicking the drop point (ms). |
| `ocrClickDelayMs` | 50 | 0-2000 | Wait after the target click before pasting (ms). |
| `ocrClipboardRestoreDelayMs` | 100 | 0-2000 | Wait after paste before restoring the previous clipboard (ms). Increase if the target pastes the wrong content. |

## touch

| Key | Default | Range | Description |
|---|---|---|---|
| `touchpadDragFingerCount` | 3 | 2–10 | Fingers needed to drag on the touchpad. |
| `touchscreenDragFingerCount` | 3 | 1–10 | Fingers needed to drag on the touchscreen. |
| `enableTouchHoldForceWindowDrag` | true | true/false | Hold still to force a window drag (touchpad and touchscreen). |
| `touchDragButtons` | Left | button name | Mouse button(s) that the touch drag presses while dragging. |
| `touchCancelDragPollIntervalMs` | 75 | 10-1000 | How often the touchpad drag checks whether the cancel key is pressed (ms). |
| `touchStationaryDragDropHoldMs` | 150 | 0-2000 | How long the finger must stay still before a drop is armed (ms). |
| `touchDragDropAnchorGraceMs` | 50 | 0-1000 | Grace period when the anchor finger moves slightly (ms). |
| `touchFingerLiftGraceMs` | 300 | 0-2000 | Time allowed after a finger lifts before the gesture ends (ms). |
| `touchscreenCancelDragPollIntervalMs` | 75 | 10-1000 | How often the touchscreen drag checks whether the cancel key is pressed (ms). |
| `touchscreenContactLostGraceMs` | 300 | 0-2000 | Time allowed after contact is lost before the drag ends (ms). |
| `touchscreenRecentExternalCursorMovementMs` | 180 | 0-2000 | How long external cursor movement still counts as recent (ms). |

## mouse

| Key | Default | Range | Description |
|---|---|---|---|
| `mouseDragTriggerButton` | Secondary | button name | Button that starts a mouse drag. |
| `enableClickHoldForceWindowDrag` | true | true/false | Hold still to force a window drag with the mouse. |
| `mouseCancelDragPollIntervalMs` | 75 | 10-1000 | How often the mouse drag checks whether the cancel key is pressed (ms). |

## stylus

| Key | Default | Range | Description |
|---|---|---|---|
| `enableStylus` | false | true/false | Enable stylus drag. |
| `enableStylusHoldForceWindowDrag` | true | true/false | Hold still to force a window drag with the stylus. |

## keyboard

| Key | Default | Range | Description |
|---|---|---|---|
| `windowMoveModifierKey` | LAlt | key name | Hold with the arrow keys to move the foreground window. |
| `windowMoveStepPixels` | 24 | 1–500 | Pixels moved per arrow press. |
| `forcedDragModePollIntervalMs` | 10 | 1-1000 | How often forced drag mode checks the keys (ms). |
| `windowMoveArrowSequenceTimeoutMs` | 500 | 50-5000 | Time allowed between arrow presses in one move (ms). |
| `windowMoveNudgeIntervalMs` | 45 | 1-1000 | Minimum time between keyboard window moves (ms). |
