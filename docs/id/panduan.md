# Bagian Bahasa Indonesia

ThreeFingerDrag adalah utilitas Windows untuk menyeret jendela dan elemen UI
dengan tiga jari di touchpad atau layar sentuh, serta dengan mouse dan stylus
bila diaktifkan. Dibuat dengan C# untuk .NET 8 (Windows Forms).

## Kebutuhan

- Windows 10 versi 1607 (build 14393) atau yang lebih baru.
- Build rilis sesuai CPU: `win-x64`, `win-x86`, atau `win-arm64`.

## Instalasi

1. Unduh dan jalankan installer untuk CPU Anda (`win-x64`, `win-x86`, atau
   `win-arm64`).
2. Setujui UAC untuk memasang aplikasi dan mendaftarkan tugas startup elevated.
3. Setelah memilih folder, instalasi langsung berjalan dan jendela pengaturan
   terbuka otomatis. Menutupnya hanya
   menyembunyikan jendela, program tetap berjalan di latar belakang.

Installer meminta persetujuan UAC untuk memasang program dan mendaftarkan
tugas startup elevated bagi akun administrator yang memasangnya. Setelah itu,
program dapat dijalankan dari Start Menu dan saat
akun tersebut masuk Windows tanpa konfirmasi UAC berulang.
Menjalankan `ThreeFingerDrag.exe` secara langsung
tetap memunculkan UAC. Uninstaller menghapus tugas startup dan dapat meminta
UAC. Installer yang dibuat oleh skrip build belum
ditandatangani, Windows dapat menampilkan peringatan SmartScreen atau Defender.

Untuk memasang ulang atau menghapus program, jalankan installer lagi dan pilih
opsi yang diinginkan. Uninstall menghapus pengaturan, terjemahan, dan log di
`%LOCALAPPDATA%\ThreeFingerDrag` milik akun administrator yang menjalankan
uninstaller. Cadangkan folder tersebut bila datanya ingin disimpan.

Program tidak memiliki ikon tray permanen. Balon notifikasi hanya muncul jika
startup gagal. Untuk membuka lagi jendela pengaturan, gunakan pintasan
ThreeFingerDrag di Start Menu. Peluncuran kedua memberi sinyal ke instance
yang sedang berjalan untuk menampilkan pengaturan, lalu keluar.

## Cara penggunaan

### Gestur dasar: seret dengan tiga jari

1. Letakkan tiga jari di touchpad (atau layar sentuh) lalu gerakkan.
2. Program memeriksa item di bawah jari dan menentukan apakah yang digeser
   adalah **jendela** atau **elemen** (lihat
   [Cara menentukan target](#cara-menentukan-target)).
3. Angkat jari untuk melepas item.

Jumlah jari diatur dengan `touchpadDragFingerCount` dan
`touchscreenDragFingerCount` (default: 3).

Jika jari ditambah melebihi jumlah yang diatur saat menyeret, seretan dijeda.
Seretan dilanjutkan saat jumlah jari kembali ke angka yang diatur. Jika jumlah
jari turun di bawah angka yang diatur saat dijeda, seretan berakhir.

### Seret jendela vs seret elemen

- **Seret jendela**: memindahkan seluruh jendela. Dipakai pada latar jendela,
  bilah judul, batas jendela, dan area kosong.
- **Seret elemen**: mengirim seretan ke konten di bawah kursor, misalnya
  memindahkan file di Explorer atau tab di browser.

### Memicu seret jendela pada area satu warna

Jika area di bawah kursor memiliki satu warna solid (misalnya panel kosong atau
latar polos), program menganggapnya sebagai latar jendela dan memulai seret
jendela. Jika area contoh berisi banyak warna (teks, ikon, gambar), program
menganggapnya sebagai konten interaktif dan memulai seret elemen. Pemeriksaan
ini dapat dimatikan dengan `enableSolidColorDetection`.

### Memaksa seret jendela dengan menahan diam

Tahan diam pada elemen dengan tiga jari untuk memaksa seret jendela. Pengaturan
`showTouchHoldForceWindowDragIndicator` menampilkan indikator singkat saat hal
ini terjadi. Hanya berlaku untuk touchpad dan layar sentuh.

### Memaksa mode seret dengan tombol

- Tahan `forceWindowDragKey` (default: Ctrl kiri) saat seretan dimulai untuk
  memaksa seret jendela.
- Tahan `forceNonWindowDragKey` (default: Ctrl kanan) saat seretan dimulai untuk
  memaksa seret elemen.

Tombol jendela lebih diprioritaskan daripada tombol elemen. Tombol tahan
diperiksa sebelum timer tahan untuk touch/mouse/stylus.

### Mengganti override seret dengan pintasan

Tekan `dragModeSwitcherShortcut` (default: `LControl+LAlt+W`) untuk
berganti override dengan urutan: mati, lalu pindah jendela, lalu seret elemen,
lalu mati lagi. Setiap tekanan dihitung sekali, menahan pintasan tidak
mengulang. Suara dapat dimatikan dengan `enableForcedModeToggleSound`.

### Membatalkan seretan

Tekan `dragCancelKey` (default: Escape) saat menyeret untuk membatalkan.
Pembatalan didukung untuk touchpad, layar sentuh, dan mouse. Stylus tidak
memeriksa tombol ini.

### Memindahkan jendela aktif dengan keyboard

Tahan `windowMoveModifierKey` (default: Alt kiri) lalu tekan tombol panah untuk
memindahkan jendela aktif. Setiap tekanan memindahkan jendela sejauh
`windowMoveStepPixels` (default: 24 px). Tekanan berulang yang cepat
memperbesar langkah.

### Lepas dengan flick

Pada touchpad dan layar sentuh, gerakan cepat tiga jari saat menyeret akan
melepas item dengan momentum. Kecepatan yang dibutuhkan diatur dengan
`touchFlickToDropVelocityThreshold`, atau fitur dimatikan dengan
`enableTouchFlickToDrop`.

### Seret dengan mouse

Tahan `mouseDragTriggerButton` (default: `Secondary`, tombol kanan) lalu
gerakkan mouse untuk menyeret. Menahan tombol diam selama `dragHoldDurationMs`
memaksa seret jendela jika `enableClickHoldForceWindowDrag` aktif.

### Stylus

Stylus nonaktif secara default. Set `enableStylus` ke `true` untuk
menggunakannya. Menahan diam memaksa seret jendela jika
`enableStylusHoldForceWindowDrag` aktif.

### Penangkapan teks OCR

Tahan diam pada teks dengan tiga jari (touchpad atau layar sentuh), atau tahan
tombol mouse atau stylus diam di dekat titik tekan, untuk memulai penangkapan
OCR. Lama tahan untuk keempat jenis input diatur oleh `ocrHoldDurationMs`
(default: 500 ms). Penangkapan mencakup radius `ocrCaptureRadius` piksel
(default: 250). Kata dikelompokkan dengan `ocrMaximumWordGapPercent`. Set `ocrCopyToClipboard` ke `true` untuk
menyalin teks ke clipboard. Bahasa OCR dipilih dengan `ocrLanguageTag`. Jika
kosong, digunakan bahasa default OCR Windows.

## Perilaku input per perangkat

| Perilaku | Touchpad | Layar sentuh | Mouse | Stylus |
|---|---|---|---|---|
| Seret dengan tiga jari | Ya (`touchpadDragFingerCount`) | Ya (`touchscreenDragFingerCount`) | Tidak | Tidak |
| Tahan untuk memaksa seret jendela | Ya (`enableTouchHoldForceWindowDrag`) | Ya (`enableTouchHoldForceWindowDrag`) | Ya (`enableClickHoldForceWindowDrag`) | Ya (`enableStylusHoldForceWindowDrag`) |
| Lepas dengan flick | Ya | Ya | Tidak | Tidak |
| Jangkar lepas setelah tahan (`touchpadDragDropAnchorHoldMs`) | Ya | Tidak | Tidak | Tidak |
| Masa tenggang jangkar setelah angkat jari (`touchpadPostLiftAnchorGraceMs`) | Ya | Tidak | Tidak | Tidak |
| Batalkan dengan `dragCancelKey` | Ya | Ya | Ya | Tidak |
| Perlu `enableStylus` | Tidak | Tidak | Tidak | Ya |

Seret mouse dimulai dari `mouseDragTriggerButton`, bukan dari jumlah jari.

## Cara menentukan target

Program memeriksa item di bawah kursor dengan detektor berikut. Detektor
pertama yang memberi jawaban pasti akan dipakai. Jika tidak ada yang pasti,
item dianggap seret elemen (non-jendela).

1. **Hit test**: hasil hit-test sistem. Bagian chrome jendela (judul, batas,
   tepi ukuran) berarti jendela, hasil kontrol berarti elemen.
2. **Bentuk kursor**: panah atau kursor ukuran berarti jendela, I-beam berarti
   teks (elemen), kecuali `enableEmptyIBeamWindowDrag` menganggap area I-beam
   kosong sebagai jendela.
3. **UI Automation**: pohon aksesibilitas diperiksa untuk jenis elemen.
4. **Nama kelas kontrol**: nama kelas jendela yang dikenal dicocokkan.
5. **Warna solid**: contoh seragam berarti latar jendela, contoh campuran berarti
   konten elemen.

Setiap detektor dapat dinyalakan atau dimatikan dengan kunci `enable...Detection`
pada bagian `detection`.

## Pengaturan

Pengaturan disimpan di `%LOCALAPPDATA%\ThreeFingerDrag\ThreeFingerDrag.config.json`.
Saat pertama kali dijalankan, konfigurasi lama di sebelah executable disalin
otomatis jika tersedia. Gunakan jendela pengaturan atau edit file tersebut
secara langsung. Program memuat ulang file saat berjalan (diperiksa paling
sering setiap 250 ms). File bahasa yang dapat diedit disimpan di subfolder
`Languages` pada lokasi yang sama.

- Kunci yang tidak ada memakai nilai default.
- Nilai numerik di luar rentang akan dibatasi (clamp) ke rentang yang diizinkan.
- Kunci `language` memilih bahasa antarmuka. Jika kosong, bahasa UI Windows
  digunakan. Kunci terjemahan yang hilang memakai bahasa Inggris.


### Referensi konfigurasi

Lihat [referensi konfigurasi](konfigurasi.md).

## Log

Jika `enableDiagnosticLog` bernilai `true`, program menulis
`%LOCALAPPDATA%\ThreeFingerDrag\logs\logs.jsonl`. Jika
`enableSplitDiagnosticLogs` juga `true`, setiap bagian program menulis file
sendiri, misalnya `cursor-diagnostics.jsonl` atau `clipboard-writer.jsonl`
di folder `logs` tersebut. Setiap entri memiliki field `section` yang
menunjukkan sumbernya. Penanda `session.running` dibuat di folder `logs` saat
startup dan dihapus saat keluar dengan normal. Jika penanda masih ada pada
startup berikutnya, sesi sebelumnya berakhir tidak normal.

## Bahasa

Terjemahan ada di `src\Languages\en.json` dan `src\Languages\id.json`, tertanam di
dalam executable. Dropdown di jendela pengaturan menampilkan bahasa yang
tersedia, memakai kunci `languageName` sebagai nama tampilan.

## Membangun

Kebutuhan: .NET 8 SDK dan Windows.

```powershell
dotnet build src\ThreeFingerDrag.csproj -c Release -p:Platform=x64
```

Untuk mempublikasikan ketiga target CPU ke `publish\`:

```powershell
.\publish-all.ps1
```

Proyek membuat `Logo.ico` dari `src\Logo.png` saat build.

## Lisensi

Lihat [LICENSE](../../LICENSE). Komponen pihak ketiga tercantum di
[THIRD-PARTY-NOTICES.md](../../THIRD-PARTY-NOTICES.md).
