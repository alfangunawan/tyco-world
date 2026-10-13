# Player first-person — verifikasi (2026-10-13)

- **Tes EditMode:** 416 lulus (WorldGen + Player), failed=0, skipped=0. Tes 223 lama tetap lulus.
  - Uji coba (spike) `CharacterController.Move` di EditMode berhasil, jadi semua tes gerak tetap berjalan di EditMode.
- **Generate ulang:**
  - Collider, `GroundSurface`, dan gedung kunci saja tidak mengubah hash: 278 chunk IDENTICAL.
  - PhysX lalu memperingatkan segitiga > 500 m pada `Areas_557649490`, `Chunk_-8_-7`, dan `Chunk_-5_5`. Karena itu segitiga permukaan pijak dipecah sampai sisinya ≤ 100 m. Akibatnya 52 chunk berubah hash dengan sengaja; generate berikutnya IDENTICAL lagi.
  - KeyBuilding=6, Building=4915, MeshCollider 5741 (sesuai perhitungan), WorldEdge 4, tanpa warning.
  - Raycast ke bawah di kos mengenai "Kos Raflesia 3". Di Jl. Sukabirus mengenai Asphalt way 95529338 pada y=0,090.
- **Halaman kos:**
  - Output planner: `edge=0 outward=(-0.01,-1.00) yard=6.00 clearance=7.19 road=689899501 door=(-321.15,-611.44) gate=(-321.22,-617.44) spawn=(-321.16,-612.64) yaw=180.8 motor=(-325.89,-614.08) motorYaw=90.8`.
  - Arah gerbang (selatan) sudah dikonfirmasi user lewat screenshot.
  - Player Setup idempoten: halaman yang digeser user tidak dipindah, objek tidak terduplikasi. Menu Re-place mengembalikan posisi. "Setup Preview Scene" tidak menyalakan PreviewCam lagi. Hasilnya 1 MainCamera dan 1 AudioListener.
- **Play (MCP, intent berskrip):**
  - Editor yang tidak fokus membekukan loop Play di frame 2. `Application.runInBackground = true` diset hanya di runtime sesi itu; PlayerSettings tidak berubah.
  - Hasil tiap langkah:
    1. Spawn dalam 0,02 s di (-321,16; 0,17; -612,64): menapak lantai, Concrete, HUD terbangun, pesan "Kos Raflesia 3".
    2. Jalan ke pagar tertutup: pemain berhenti di z -617,01, sedangkan garis pagar -617,44. Fokus Gate, prompt "[E] Buka pagar", 5 langkah di beton.
    3. E membuka pagar ke luar (-100°) dengan 2 bunyi (gerendel dan derit).
    4. Keluar ke jalan: Asphalt, `feature=689899501` (Road), 13 langkah, langkah terakhir terbaca Asphalt.
    5. Lari: stamina 0,46 setelah 3 s dan bar stamina tampil. Lari terus sampai stamina 0,08, lalu pulih setelah berhenti. Keluar jalan, langkah terbaca Dirt.
    6. Pintu kos: prompt "[E] Buka pintu", pesan "Pintunya terkunci.", bunyi gagang 1×.
    7. Motor: prompt "[E] Naik motor", pesan "Belum bisa naik motor.", bunyi kunci 1×.
    8. Jatuh ke y=-30: kembali ke spawn dengan pesan "Kamu kembali ke kos.".
    9. Jongkok: tinggi 1,8→1,0, mata 1,6→0,85. Berdiri lagi di halaman terbuka.
    10. Dinding batas dunia barat: lari 3 s, pemain berhenti di x -938,86 (batas -939,26).
- **Senter:** luminansi tengah L0=0,0000 → L1=0,1432 saat menghadap dinding kos. Sorot terlihat di screenshot. Tidak ada masalah batas light Forward+, jadi tidak perlu `LampLightCuller`. Lampu jalan terlihat menerangi fasad bangunan.
- **Console setelah Play:** tidak ada error maupun warning.

## Setelah final review (4 perbaikan Important)
- **Tes:** 428 lulus (416 + 12 tes baru), failed=0.
- **Spawn terhalang atau di dalam bangunan:** pemain tetap beku, HUD memberi tahu, dan satu error tercatat; pemain tidak lagi diteleport ke dalam collider. Posisi di bawah atap bangunan ditolak.
- **Pagar menolak ditutup saat pemain berada di jalur ayunannya.** Dicek di Play:
  - saat lewat gerbang lalu berbalik, fokus ke Gate tapi prompt kosong, dan E tidak menutup pagar;
  - dari dalam halaman, prompt "[E] Tutup pagar" muncul, pagar menutup, dan bunyi gerendel terdengar.
- **Play sebelum Generate:** loader langsung menandai gagal dengan satu error yang jelas, dan HUD langsung menampilkan "Dunia belum di-generate …" tanpa menunggu 15 s.
- **Player Setup bisa dijalankan ulang tanpa menimpa editan.** Isi FootstepBank, warna material, dan PanelSettings yang sudah diubah user tetap dipertahankan. Prefab dan mesh placeholder tetap dibangun ulang tiap Setup.
- **Play dengan hanya World.unity terbuka:** loader memuat World_Generated dan pemain spawn di frame 1. Waktu dari Enter Play sampai poll pertama sekitar 3,1 s, termasuk cooking MeshCollider dan jeda MCP. Console bersih.
- **Setelah merge di checkout lain:** jalankan Generate ulang, karena scene hasil generate yang lama belum punya collider jalan dan dinding batas.

## Checklist QA manual (perlu dicoba sendiri, input asli tidak bisa diotomasi)
- [ ] Klik Game view → kursor terkunci; Esc melepas; klik kiri mengunci lagi; kamera tidak berputar saat kursor lepas.
- [ ] WASD terasa pas (jalan 3 m/s), Shift lari, stamina habis sekitar 5–6 detik, bar stamina muncul/hilang.
- [ ] Spasi lompat (tidak bisa melompati pagar), C jongkok/berdiri, tidak bisa berdiri di bawah atap rendah.
- [ ] Sensitivitas mouse (0.1°/px) dan head-bob nyaman; ubah di `Assets/Player/Config/PlayerTuning.asset` kalau perlu.
- [ ] F senter: klik terdengar, sorotan terlihat di malam hari.
- [ ] E di pagar (derit + gerendel), pintu kos (terkunci), motor (belum bisa).
- [ ] Bunyi langkah berbeda di halaman (beton), jalan (aspal), rumput, dan tanah; volume seimbang.
- [ ] Berjalan ke tepi dunia → tertahan dinding tak terlihat.
- [ ] Kalau Play terasa beku saat jendela Unity tidak aktif: itu karena "Run In Background" di Player Settings mati (bawaan proyek), bukan bug pemain.
