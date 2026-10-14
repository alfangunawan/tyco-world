# Performa + Pause/Settings — verifikasi (2026-10-14)

- **Tes EditMode:** `group=. passed=713 failed=0`.
- **Spike input UI:** lulus tanpa EventSystem (UI Toolkit menerima klik lewat InputSystemProvider; Task 13).
- **Pause:** jam game berhenti (t=12:13) selama menu terbuka, `timeScale=0`, `audioPaused=True` (dicek dua kali, selang beberapa detik, `t` tetap 12:13); Lanjutkan (Esc lagi) → `page=Closed timeScale=1.00`, `t` maju ke 12:22.
- **git status sebelum/sesudah Play:** identik. Console tanpa error/warning setelah sesi Play.

## Pengukuran (editor, batas FPS dimatikan)

Format: fps · draw call · light. "Draw call" = `UnityStats.drawCalls`.

| Titik | Baseline | Rendah | Sedang | Tinggi |
|---|---|---|---|---|
| kos siang | 161.5 fps · 6479 · 1 | 179.6 fps · 2523 · 1 | 210.0 fps · 4319 · 1 | 147.4 fps · 11128 · 1 |
| jalan siang | 180.2 fps · 5974 · 1 | 270.4 fps · 1113 · 1 | 237.8 fps · 2425 · 1 | 163.7 fps · 8276 · 1 |
| jalan malam 21:00 | 154.5 fps · 6032 · 831 | 247.7 fps · 1131 · 9 | 203.9 fps · 2457 · 16 | 157.1 fps · 8342 · 33 |
| atas 30 m | 174.7 fps · 5599 · 1 | 267.2 fps · 1049 · 1 | 231.1 fps · 1722 · 1 | 195.6 fps · 5560 · 1 |

- Kriteria spec §8 (Sedang, jalan malam): light 16 (batas 16 + 3) dan 203.9 fps > baseline 154.5 fps. Terpenuhi.
- Renderer aktif (jalan siang): baseline 7905/7905 → Rendah 772/7905, Sedang 1238/7905, Tinggi 2581/7905.
- Preset Tinggi lebih lambat dari baseline di kos siang (147.4 vs 161.5) dan jalan siang (163.7 vs 180.2), karena bayangan/SSAO dan jarak pandang 500 m lebih berat dari baseline (jarak 400 m tanpa culling per grup); di jalan malam (157.1 vs 154.5) dan atas 30 m (195.6 vs 174.7) lebih cepat.
- GPU occlusion (Sedang): jalan siang nyala 237.8 fps vs mati 235.5 fps (mati 1 % lebih lambat); atas 30 m nyala 231.1 fps vs mati 268.8 fps (mati 16 % lebih cepat). Tidak ≥ 5 % lebih cepat di kedua titik → keputusan: tetap nyala. Catatan: pengukuran 5 detik di editor cukup berisik (worst 19.7–23.7 ms pada kedua sampel tanpa occlusion).
- Screenshot `shot_view150.png` dan `shot_view800.png` (jalan, 12:00): pada 150 m gedung di kejauhan tampak tertutup kabut dan lebih sedikit; pada 800 m gedung jauh tampak lebih jelas dan kabut tipis. Bangunan dekat identik.
- Angka editor tidak sama dengan build. Penghitung FPS (Tampilan) dipakai untuk cek di build.

## Checklist QA manual (input asli tidak bisa diotomasi)
- [ ] Esc membuka menu Jeda: jam, langkah, dan suara berhenti. Esc lagi atau Lanjutkan → kursor terkunci lagi.
- [ ] Klik semua tombol dan tab, geser slider, buka dan pilih dropdown dengan mouse asli.
- [ ] View distance 100 ↔ 800: batas pandang tertutup kabut, *pop-in* gedung tidak mencolok.
- [ ] Preset Rendah (70 % + STP) masih enak dilihat; Tinggi tampak lebih tajam dengan SSAO.
- [ ] Sensitivitas mouse, invert Y, dan FOV terasa sesuai.
- [ ] Volume Master, Efek (langkah, pagar), dan Ambience (jangkrik malam) berubah sesuai slider.
- [ ] Tampilan: penghitung FPS dan jam bisa dinyalakan/dimatikan.
- [ ] Stop Play lalu Play lagi: pengaturan masih tersimpan.
- [ ] Malam: lampu jalan di sekitar pemain menyala halus saat berjalan; lampu jauh hanya kepalanya yang menyala.
- [ ] Keluar → konfirmasi "Keluar dari game?" → Play berhenti.
- [ ] Build Windows: mode layar, resolusi, dan penghitung FPS.
