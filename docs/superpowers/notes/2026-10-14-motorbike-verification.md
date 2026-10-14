# Motor yang bisa dikendarai — verifikasi (2026-10-14)

Spec: `docs/superpowers/specs/2026-10-14-motorbike-design.md`. Plan: `docs/superpowers/plans/2026-10-14-motorbike.md`.

## Tes otomatis
- `McpTestRunner.Run(".")`: 828 lulus, 0 gagal (94 tes Vehicles).

## Play lewat MCP (input skrip)
| Cek | Hasil |
|---|---|
| Naik di kos (E lewat Interactor) | mounted, seated=True, moveFree=False, lookFree=True, engine=True, HUD kendaraan tampil, crosshair hilang, kamera ThirdPerson, map Vehicle aktif (False saat jalan kaki) |
| Keluar lewat pagar | tertahan di (-321.22, -617.44) = titik pivot pagar (tengah sasaran AimAtGate), bumps +1; pagar sudah Open. Bukan syarat lulus; cek manual dengan keyboard asli |
| Kecepatan setelah 10 s gas penuh | 59,0 km/j, jarak 127,1 m, HUD 59. Dijalankan di lintasan uji datar (7000, 1000, 7000), bukan di Jalan Sukabirus |
| Tembok lurus 60 km/j | lintasan uji: berhenti di z 7149,00 (muka tembok 7150, ujung depan 7149,95), kmh 0, bumps +1 |
| Pause saat melaju | timeScale 0, audioPaused True, pos dan kmh identik antar dua status; lanjut: timeScale 1 dan pos berubah; lookFree False hanya selama menu pause (dikunci menu pause, bukan motor), True setelah lanjut |
| Kamera V | ThirdPerson -> FirstPerson -> ThirdPerson; camDist orang ketiga di jalan terbuka 4,55 m |
| Lampu depan malam | KURANG/INKONKLUSIF pada intensitas 4. Lintasan uji (21:00, lantai datar, tanpa lampu lain): luminansi tengah off 0,0658, on 0,0665 (orang ketiga), 0,0691 (orang pertama); naik 1-5% dan tidak ada bercak terang. Lampu aktif (intensity 4, range 30, spot 60, Realtime, URP PerPixel). Penyebab yang paling mungkin: intensitas 4 terlalu redup; uji diagnostik runtime intensitas 60 memberi bercak terang jelas (0,0917). Screenshot: `track-night-off.png`, `track-night-on.png`, `track-night-first-on.png`, `track-night-diag-x15.png`. Intensitas lampu depan akan dinaikkan di gelombang perbaikan akhir |
| Performa Sedang, malam, melaju | 164,2 fps (6,09 ms, terburuk 113,7 ms), lampu aktif 18, batches 1416. Sekitar 20% di bawah catatan #4 (203,9 fps, jalan kaki) pada kondisi berbeda; ada satu frame terburuk 113,7 ms. Sebagian jendela sampling motor berhenti (lihat catatan Sukabirus) |
| Turun saat berhenti | kaki mendarat di sisi kanan motor, 1,1 m dari motor; mounted=False, moveFree=True, crosshair True, mesin dan lampu mati, bisa berjalan lagi |
| Teleport saat naik | otomatis turun (mounted=False, seated=False, moveFree=True, HUD kendaraan hilang, engine=False) |
| Console bersih | 0 error dan 0 warning sebelum dan sesudah Play |
| `git status` sebelum/sesudah Play | sama; hash `git diff ProjectSettings/ProjectSettings.asset` sama (e1b0e7a9...) |

Screenshot: `kos-third.png`, `road-third.png`, `road-first.png`, `night-headlight.png` (dikirim ke user, tidak di-commit).

## Catatan
- Setelah "Tyco World/Re-place Kos Frontyard", jalankan "Tyco World/Vehicle Setup" lagi supaya motor pindah ke halaman yang baru.
- Audio: sintesis (lihat `Assets/Vehicles/Audio/SOURCES.md`).
- Dengan sumber input skrip terpasang (Install), map "Vehicle" tidak diaktifkan; dengan input asli (setelah Release) aktif satu frame kemudian. Itu perilaku yang diharapkan untuk uji skrip.
- Di jalan Sukabirus motor berhenti (bumps +1) di (-262,15, -616,71) pada setiap lintasan. Penyebab yang paling mungkin, berdasarkan `road-third.png`: titik awal skrip (-287,7, -633,3) dengan yaw 57 membuat motor keluar dari aspal ke rumput dan menabrak sudut bangunan di kiri, jadi bukan hambatan di jalan, melainkan arah skrip yang salah.
- Belok tidak diskrip di Play. Ditutup oleh tes EditMode `MotorbikeTests.Steering_TurnsTheBike` dan checklist QA manual.

## Checklist QA manual (input asli tidak bisa diotomasi)
- [ ] Jalan ke motor, "[E] Naik motor" muncul; E → duduk, mesin starter lalu langsam, HUD speedometer tampil.
- [ ] W/S/A/D dan panah: gas, rem, mundur pelan saat diam, belok terasa wajar; Space rem keras.
- [ ] Mouse memutar kamera orang ketiga; setelah ±1,5 s kamera kembali ke belakang saat melaju.
- [ ] V: orang pertama (bisa menoleh, ada roll saat belok) ↔ orang ketiga.
- [ ] Gang sempit: kamera maju mendekat, tidak menembus tembok.
- [ ] Lewat pagar kos, naik turun undakan jalan, tanjakan.
- [ ] Menabrak tembok: berhenti + bunyi benturan; menyerong: meluncur di sepanjang tembok.
- [ ] H klakson ("tin-tin"), tidak bisa di-spam lebih cepat dari 0,4 s.
- [ ] F lampu depan; "LAMPU" di HUD terang/redup; malam jalan di depan terang.
- [ ] Rem: lampu belakang lebih terang.
- [ ] E saat melaju → "Pelankan motor dulu."; E saat diam → turun di kiri (atau kanan), "[E] Turun" hanya tampil saat boleh turun.
- [ ] Esc saat melaju → pause (motor diam, suara berhenti); lanjut → kursor terkunci lagi dan kontrol normal.
- [ ] Slider Efek mengubah volume mesin, klakson, dan benturan; Master mengubah semuanya.
- [ ] Gamepad (kalau ada): trigger gas/rem, stik kiri belok, stik kanan kamera.
