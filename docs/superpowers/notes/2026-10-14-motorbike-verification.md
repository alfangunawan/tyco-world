# Motor yang bisa dikendarai — verifikasi (2026-10-14)

Spec: `docs/superpowers/specs/2026-10-14-motorbike-design.md`. Plan: `docs/superpowers/plans/2026-10-14-motorbike.md`.

## Tes otomatis
- `McpTestRunner.Run(".")`: 832 lulus, 0 gagal (97 tes Vehicles), setelah gelombang perbaikan akhir. Sebelumnya 828 lulus (94 tes Vehicles).

## Play lewat MCP (input skrip)
| Cek | Hasil |
|---|---|
| Naik di kos (E lewat Interactor) | mounted, seated=True, moveFree=False, lookFree=True, engine=True, HUD kendaraan tampil, crosshair hilang, kamera ThirdPerson, map Vehicle aktif (False saat jalan kaki) |
| Keluar lewat pagar | LULUS (gelombang perbaikan akhir). Pagar dibuka (`OpenGate`, Open, sudut -100°). Motor diletakkan 3 m di dalam bukaan, lurus menghadap ke luar (tengah pagar (-321,22, -617,44), arah keluar = forward pagar, yaw -179,2). Gas 0,3 selama 3 s lalu rem 2 s: akar motor berhenti 2,77 m di luar garis pagar (ekor ±1,8 m di luar), geser samping 0,00, bumps tetap 0. Gas 4 s penuh tidak dipakai karena motor akan menabrak bangunan di seberang gang (box cast: 6,96 m di depan). Screenshot `fix-gate-out-third.png`. Sebelumnya `AimAtGate` dari parkiran masuk bukaan secara miring dan tertahan di garis pagar (bumps +1) |
| Kecepatan setelah 10 s gas penuh | 59,0 km/j, jarak 127,1 m, HUD 59. Dijalankan di lintasan uji datar (7000, 1000, 7000), bukan di Jalan Sukabirus |
| Tembok lurus 60 km/j | lintasan uji: berhenti di z 7149,00 (muka tembok 7150, ujung depan 7149,95), kmh 0, bumps +1 |
| Pause saat melaju | timeScale 0, audioPaused True, pos dan kmh identik antar dua status; lanjut: timeScale 1 dan pos berubah; lookFree False hanya selama menu pause (dikunci menu pause, bukan motor), True setelah lanjut |
| Kamera V | ThirdPerson -> FirstPerson -> ThirdPerson; camDist orang ketiga di jalan terbuka 4,55 m |
| Lampu depan malam (intensitas 60) | LULUS (gelombang perbaikan akhir). Jalan Sukabirus 21:00 di (-310, -726), yaw 10 (lurus sepanjang jalan, 24 m aspal di depan), lampu jalan menyala 4 m di samping. Luminansi tengah: orang ketiga off 0,0069, on 0,0107; orang pertama off 0,0072, on 0,0175. Aspal 3 m di depan motor (orang pertama): off 0,004, on 0,091 / 0,121 / 0,180 untuk intensitas 30 / 40 / 60; 6 m di depan: 0,004 → 0,024. Bercak dekat tidak pecah (maks 0,21). Lintasan uji: luminansi tengah off 0,0658 → on 0,0739 (orang ketiga), 0,0676 → 0,0919 (orang pertama); lantai 3 m di depan 0,057 → 0,43 (maks 0,46). Dipilih 60, nilai tertinggi di rentang 30–60 yang belum pecah. Tembok yang sangat dekat (1,5 m) tetap putih pada 30, 40, maupun 60 (maks 0,917 untuk ketiganya), jadi tidak bergantung pada pilihan ini. Tidak ada lampu depan yang hilang di dekat lampu jalan: renderer PC memakai Forward+ (`m_RenderingMode: 2`), dan walau 28 lampu jalan menyala bersinggungan dengan bounds Jalan Sukabirus (satu renderer 431 × 563 m), aspal tetap terang di dua titik uji. Karena itu `renderMode` tetap Auto. Screenshot: `fix-road-night-off-third.png`, `fix-road-night-on-third.png`, `fix-road-night-off-first.png`, `fix-road-night-on-first.png`, `fix-track-night-on-third.png`, `fix-track-night-on-first.png` (pembanding `-i30`/`-i40`) |
| Lampu depan malam (intensitas 4, lama) | KURANG/INKONKLUSIF pada intensitas 4. Lintasan uji (21:00, lantai datar, tanpa lampu lain): luminansi tengah off 0,0658, on 0,0665 (orang ketiga), 0,0691 (orang pertama); naik 1-5% dan tidak ada bercak terang. Lampu aktif (intensity 4, range 30, spot 60, Realtime, URP PerPixel). Penyebab yang paling mungkin: intensitas 4 terlalu redup; uji diagnostik runtime intensitas 60 memberi bercak terang jelas (0,0917). Screenshot: `track-night-off.png`, `track-night-on.png`, `track-night-first-on.png`, `track-night-diag-x15.png`. Intensitas lampu depan akan dinaikkan di gelombang perbaikan akhir |
| Performa Sedang, malam, melaju | 164,2 fps (6,09 ms, terburuk 113,7 ms), lampu aktif 18, batches 1416. Sekitar 20% di bawah catatan #4 (203,9 fps, jalan kaki) pada kondisi berbeda; ada satu frame terburuk 113,7 ms. Sebagian jendela sampling motor berhenti (lihat catatan Sukabirus) |
| Turun saat berhenti | kaki mendarat di sisi kanan motor, 1,1 m dari motor; mounted=False, moveFree=True, crosshair True, mesin dan lampu mati, bisa berjalan lagi |
| Teleport saat naik | otomatis turun (mounted=False, seated=False, moveFree=True, HUD kendaraan hilang, engine=False). Sejak gelombang perbaikan akhir motor tanpa pengendara mengerem sendiri (dipaku tes EditMode) |
| Motor dihancurkan saat dinaiki | LULUS. `Object.Destroy` pada akar Motorbike saat duduk; pada frame berikutnya motor hilang, seated=False, referensi kursi kosong, moveFree=True (semua kunci lepas), CharacterController aktif, hudVehicle=False, crosshair=True, map Vehicle=False. Kamera = View, pose lokal di bawah pivot persis identitas ((0, 0, 0), (0, 0, 0, 1)). Kaki di (-320,16, 0,13, -620,22), 1,1 m di kiri motor. 0 error/warning |
| Stop Play saat naik | LULUS. Masuk Play lagi (motor kembali dari scene; lampu depan 60 dari `BikeTuning`), naik lewat Interactor, melaju 7,7 km/j (hudExit=False), lalu keluar Play. Console 0 error dan 0 warning; `git status` dan hash ProjectSettings sama |
| Console bersih | 0 error dan 0 warning sebelum dan sesudah Play (juga pada dua sesi Play gelombang perbaikan akhir) |
| `git status` sebelum/sesudah Play | sama; hash `git diff ProjectSettings/ProjectSettings.asset` sama (e1b0e7a9...). Gelombang perbaikan akhir: sama pada kedua sesi Play, termasuk hash seluruh `git diff` |

Screenshot: `kos-third.png`, `road-third.png`, `road-first.png`, `night-headlight.png`, dan `fix-*.png` dari gelombang perbaikan akhir (dikirim ke user, tidak di-commit).

## Catatan
- Setelah "Tyco World/Re-place Kos Frontyard", jalankan "Tyco World/Vehicle Setup" lagi supaya motor pindah ke halaman yang baru.
- Audio: sintesis (lihat `Assets/Vehicles/Audio/SOURCES.md`).
- Dengan sumber input skrip terpasang (Install), map "Vehicle" tidak diaktifkan; dengan input asli (setelah Release) aktif satu frame kemudian. Itu perilaku yang diharapkan untuk uji skrip.
- Di jalan Sukabirus motor berhenti (bumps +1) di (-262,15, -616,71) pada setiap lintasan. Penyebab yang paling mungkin, berdasarkan `road-third.png`: titik awal skrip (-287,7, -633,3) dengan yaw 57 membuat motor keluar dari aspal ke rumput dan menabrak sudut bangunan di kiri, jadi bukan hambatan di jalan, melainkan arah skrip yang salah.
- Belok tidak diskrip di Play. Ditutup oleh tes EditMode `MotorbikeTests.Steering_TurnsTheBike` dan checklist QA manual.

## Gelombang perbaikan akhir
- **Lampu depan:** intensitas 4 → 60 (spec §4, `BikeTuning`, asset, dan prefab). `MotorbikeVisuals.ConfigureHeadlight` dipakai builder prefab dan `Motorbike.Initialize`, jadi asset `BikeTuning` menjadi satu-satunya sumber nilai dan bisa di-tuning tanpa rebuild. Yang berubah saat runtime hanya Light milik instance di scene, bukan asset.
- **Motor hancur atau Play berhenti saat dinaiki:** `FirstPersonPlayer` membandingkan kursi lewat referensi. Kursi yang sudah hancur tetap dilepas, rig kamera dikembalikan, dan `Unseated` dipicu satu kali. `Tick` menurunkan pemain di tempat kalau kursinya hancur, dan `Motorbike.Shutdown` juga menurunkan pengendara yang duduk di kursinya walau kursi itu sudah hancur.
- **Motor tanpa pengendara mengerem sendiri (rem parkir).** Setelah pengendara di-teleport saat motor melaju, motor berhenti dalam 2 s dengan jarak di bawah 15 m (tes EditMode), tidak lagi menggelinding ±170 m.
- **"[E] Turun" tidak tampil selama jeda 0,3 s setelah naik.**
- **Tes Review Focus 4 dipertajam:**
  - tembok lurus 120 m di depan ditabrak pada 60,01 km/j dengan dt 0,1; ujung depan berhenti di 119,95 (muka tembok 120);
  - tembok miring: motor terdorong ke x -4,61 dan tetap melaju (14,35 m/s, bumps 0).
- **Catatan proses:** snippet masuk Play di Procedure P menyalakan `Application.runInBackground` sebelum Play benar-benar mulai. Di editor nilai ini ikut `PlayerSettings`, jadi nilai `true` tertinggal di memori setelah keluar Play dan sempat ikut tersimpan ke ProjectSettings.asset oleh `AssetDatabase.SaveAssets()`. Nilainya dikembalikan ke false dan hash kembali e1b0e7a9. Untuk sesi berikutnya, nyalakan `runInBackground` hanya setelah `isPlaying` true.

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
