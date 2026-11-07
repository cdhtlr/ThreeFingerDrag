# Referensi konfigurasi

[Kembali ke panduan Bahasa Indonesia](panduan.md).


Nama kunci dan nilai default tercantum di bawah ini.

## global

| Kunci | Default | Rentang | Keterangan |
|---|---|---|---|
| `cursorSpeed` | 20 | 1–20 | Kecepatan pointer Windows yang diterapkan saat seret (diatur lewat pengaturan pointer sistem). |
| `windowPositionSpeed` | 5 | 1–100 | Pengali kecepatan forced window drag. Terpisah dari kecepatan kursor. |
| `cursorPositionPollingIntervalMs` | 1 | 1–1000 | Interval timer posisi kursor. Hanya untuk diagnostik. |
| `bringDraggedWindowToForeground` | true | true/false | Membawa jendela yang diseret ke depan saat seret jendela dimulai. |
| `keepWindowWithinScreen` | true | true/false | Menjaga jendela tetap di area layar yang terlihat. |
| `dragHoldDurationMs` | 1000 | 500–10000 | Waktu tahan diam sebelum seret jendela dipaksa. |
| `dragCancelKey` | Escape | nama tombol | Tombol untuk membatalkan seret. |
| `forceWindowDragKey` | LControl | nama tombol | Tahan untuk memaksa seret jendela. |
| `forceNonWindowDragKey` | RControl | nama tombol | Tahan untuk memaksa seret elemen. |
| `dragModeSwitcherShortcut` | LControl+LAlt+W | pintasan | Pintasan untuk berganti override: mati → pindah jendela → seret elemen → mati. |
| `enableTouchFlickToDrop` | true | true/false | Mengaktifkan lepas dengan flick pada touchpad dan layar sentuh. |
| `touchFlickToDropVelocityThreshold` | 18 | 6–100 | Kecepatan minimum agar flick melepas item. |
| `touchpadNaturalDragReleaseThreshold` | 5 | 2–30 | Ambang gerak untuk lepas alami pada touchpad. |
| `touchpadDragDropAnchorHoldMs` | 150 | 50–1000 | Waktu tahan jangkar lepas pada touchpad. |
| `touchpadPostLiftAnchorGraceMs` | 180 | 50–1000 | Waktu setelah jari diangkat ketika jangkar lepas masih berlaku (touchpad). |
| `enableDragReleaseSound` | true | true/false | Memutar suara saat seretan dilepas. |
| `enableForcedModeToggleSound` | true | true/false | Memutar suara saat override berubah. |
| `enableDiagnosticLog` | false | true/false | Menulis log ke `%LOCALAPPDATA%\ThreeFingerDrag\logs\logs.jsonl` (atau file per bagian jika `enableSplitDiagnosticLogs` aktif). Berada di seksi `logging`. |
| `enableSplitDiagnosticLogs` | false | true/false | Memisahkan log menjadi satu file per bagian program (misalnya `cursor-diagnostics.jsonl`, `clipboard-writer.jsonl` di folder logs). Nonaktif menulis semua ke `logs.jsonl`. |
| `dragFallbackPollIntervalMs` | 25 | 1-1000 | Seberapa sering mesin seret memeriksa kursor sebagai cadangan (ms). |
| `windowDragSmoothingFrameMs` | 8 | 1-100 | Panjang frame untuk menghaluskan gerakan jendela (ms). |
| `windowDragSmoothingMinTimeConstantMs` | 40 | 1-1000 | Konstanta waktu penghalusan terpendek (ms). |
| `windowDragSmoothingMaxTimeConstantMs` | 250 | 1-5000 | Konstanta waktu penghalusan terpanjang (ms). |
| `windowDragSmoothingMaxElapsedMs` | 50 | 1-1000 | Jeda terpanjang antar pembaruan yang masih dihaluskan (ms). |
| `settingsInputDebounceMs` | 400 | 0-5000 | Jeda setelah mengetik angka sebelum diterapkan (ms). |
| `settingsStatIntervalMs` | 250 | 10-5000 | Seberapa sering file konfigurasi dicek untuk perubahan (ms). |
| `notificationSafetyTimeoutMs` | 7000 | 1000-60000 | Batas aman untuk loop pesan notifikasi balon tray (ms). |
| `notificationBalloonTimeoutMs` | 5000 | 1000-60000 | Berapa lama notifikasi balon tray ditampilkan (ms). |
| `settingsEventPollIntervalMs` | 100 | 10-5000 | Seberapa sering aplikasi pengaturan memeriksa permintaan untuk menampilkan jendelanya (ms). |
| `settingsCaptureRefreshIntervalMs` | 25 | 5-1000 | Seberapa sering jendela pengaturan menyegarkan status tangkapan tombol (ms). |

## detection

| Kunci | Default | Rentang | Keterangan |
|---|---|---|---|
| `enableSolidColorDetection` | true | true/false | Menggunakan detektor warna solid. |
| `enableUiAutomationDetection` | true | true/false | Menggunakan detektor UI Automation. |
| `enableOcrCapture` | true | true/false | Mengizinkan penangkapan teks OCR. |
| `enableCursorShapeDetection` | true | true/false | Menggunakan detektor bentuk kursor. |
| `enableEmptyIBeamWindowDrag` | true | true/false | Area I-beam kosong dianggap seret jendela. |
| `enableControlClassNameDetection` | true | true/false | Menggunakan detektor nama kelas kontrol. |
| `enableHitTestDetection` | true | true/false | Menggunakan detektor hit-test. |
| `iBeamTextDetectionAreaSize` | 51 | 1–501 | Ukuran contoh (piksel) untuk pemeriksaan teks I-beam. |
| `solidColorDetectionRectangleSize` | 5 | 1–51 | Ukuran contoh (piksel) untuk pemeriksaan warna solid. |
| `hitTestTimeoutMs` | 150 | 10-5000 | Waktu tunggu maksimum jendela menjawab hit-test (ms). Melindungi dari aplikasi yang macet. |
| `uiAutomationClassifyTimeoutMs` | 200 | 10-5000 | Waktu tunggu maksimum UI Automation mengklasifikasikan elemen (ms). |
| `titleBarHitTestTimeoutMs` | 20 | 1-1000 | Waktu tunggu maksimum pemeriksaan title bar (ms). |

## indicators

| Kunci | Default | Rentang | Keterangan |
|---|---|---|---|
| `showTouchHoldForceWindowDragIndicator` | false | true/false | Menampilkan indikator saat tahan sentuh memaksa seret jendela (touchpad dan layar sentuh saja). |
| `touchHoldForceWindowDragIndicatorDisplayMs` | 900 | 100–5000 | Lama indikator tersebut tampil. |
| `showDragModeIndicator` | false | true/false | Menampilkan mode seret saat ini. |
| `dragModeIndicatorDiameter` | 14 | 6–80 | Diameter indikator mode seret. |
| `dragModeIndicatorDisplayMs` | 900 | 100–5000 | Lama indikator mode seret tampil. |
| `windowDragIndicatorColor` | #4060FF | warna | Warna untuk seret jendela. |
| `otherDragIndicatorColor` | #FF4040 | warna | Warna untuk seret elemen. |
| `activeWindowDragIndicatorColor` | #FF00FF | warna | Warna untuk seret jendela aktif. |
| `windowPositionFallbackIndicatorColor` | #800080 | warna | Warna untuk forced window drag mode (set window position), baik yang langsung forced maupun yang berpindah dari normal. |
| `touchHoldForceWindowDragIndicatorColor` | #00C8C8 | warna | Warna untuk seret jendela paksa tahan sentuh. |
| `showSolidColorDetectionRectangle` | false | true/false | Menampilkan persegi contoh. |
| `solidColorUniformBorderColor` | #40C878 | warna | Warna tepi saat contoh seragam. |
| `solidColorMixedBorderColor` | #FFB020 | warna | Warna tepi saat contoh campuran. |
| `solidColorDetectionRectangleDisplayMs` | 500 | 100–5000 | Lama persegi contoh tampil. |
| `showDragModeIndicatorDetails` | false | true/false | Menampilkan detail tambahan pada indikator mode seret. |
| `indicatorPanelBackgroundColor` | #202020 | warna | Warna latar panel indikator. |
| `indicatorPanelTextColor` | #FFFFFF | warna | Warna teks panel indikator. |

## ocr

| Kunci | Default | Rentang | Keterangan |
|---|---|---|---|
| `ocrHoldDurationMs` | 500 | 150–3000 | Waktu tahan sebelum penangkapan OCR dimulai. |
| `ocrCaptureRadius` | 250 | 50–1000 | Radius penangkapan dalam piksel. |
| `ocrLanguageTag` | (kosong) | tag BCP-47 | Bahasa OCR. Kosong memakai default OCR Windows. |
| `ocrMaximumWordGapPercent` | 65 | 10–200 | Jarak maksimum antar kata, dalam persen ukuran kata. |
| `ocrCopyToClipboard` | false | true/false | Menyalin teks hasil tangkapan ke clipboard. |
| `ocrActivationDelayMs` | 75 | 0-2000 | Jeda setelah mengaktifkan jendela target sebelum klik titik jatuh (ms). |
| `ocrClickDelayMs` | 50 | 0-2000 | Jeda setelah klik target sebelum paste (ms). |
| `ocrClipboardRestoreDelayMs` | 100 | 0-2000 | Jeda setelah paste sebelum clipboard sebelumnya dikembalikan (ms). Naikkan jika target menempel isi yang salah. |

## touch

| Kunci | Default | Rentang | Keterangan |
|---|---|---|---|
| `touchpadDragFingerCount` | 3 | 2–10 | Jumlah jari untuk seret di touchpad. |
| `touchscreenDragFingerCount` | 3 | 1–10 | Jumlah jari untuk seret di layar sentuh. |
| `enableTouchHoldForceWindowDrag` | true | true/false | Tahan diam untuk memaksa seret jendela (touchpad dan layar sentuh). |
| `touchDragButtons` | Left | nama tombol | Tombol mouse yang ditekan program saat seret berbasis sentuh. |
| `touchCancelDragPollIntervalMs` | 75 | 10-1000 | Seberapa sering seret touchpad memeriksa tombol pembatalan (ms). |
| `touchStationaryDragDropHoldMs` | 150 | 0-2000 | Lama jari harus diam sebelum lepas seret aktif (ms). |
| `touchDragDropAnchorGraceMs` | 50 | 0-1000 | Masa toleransi saat jari jangkar bergerak sedikit (ms). |
| `touchFingerLiftGraceMs` | 300 | 0-2000 | Waktu yang diizinkan setelah jari terangkat sebelum gestur berakhir (ms). |
| `touchscreenCancelDragPollIntervalMs` | 75 | 10-1000 | Seberapa sering seret layar sentuh memeriksa tombol pembatalan (ms). |
| `touchscreenContactLostGraceMs` | 300 | 0-2000 | Waktu yang diizinkan setelah kontak hilang sebelum seretan berakhir (ms). |
| `touchscreenRecentExternalCursorMovementMs` | 180 | 0-2000 | Lama gerak kursor dari luar masih dianggap baru (ms). |

## mouse

| Kunci | Default | Rentang | Keterangan |
|---|---|---|---|
| `mouseDragTriggerButton` | Secondary | nama tombol | Tombol yang memulai seret mouse. |
| `enableClickHoldForceWindowDrag` | true | true/false | Tahan diam untuk memaksa seret jendela dengan mouse. |
| `mouseCancelDragPollIntervalMs` | 75 | 10-1000 | Seberapa sering seret mouse memeriksa tombol pembatalan (ms). |

## stylus

| Kunci | Default | Rentang | Keterangan |
|---|---|---|---|
| `enableStylus` | false | true/false | Mengaktifkan seret stylus. |
| `enableStylusHoldForceWindowDrag` | true | true/false | Tahan diam untuk memaksa seret jendela dengan stylus. |

## keyboard

| Kunci | Default | Rentang | Keterangan |
|---|---|---|---|
| `windowMoveModifierKey` | LAlt | nama tombol | Ditahan bersama tombol panah untuk memindahkan jendela aktif. |
| `windowMoveStepPixels` | 24 | 1–500 | Piksel per tekanan panah. |
| `forcedDragModePollIntervalMs` | 10 | 1-1000 | Seberapa sering mode seret paksa memeriksa tombol (ms). |
| `windowMoveArrowSequenceTimeoutMs` | 500 | 50-5000 | Waktu yang diizinkan antar tekanan panah dalam satu gerakan (ms). |
| `windowMoveNudgeIntervalMs` | 45 | 1-1000 | Jeda minimum antar pergeseran jendela lewat keyboard (ms). |
