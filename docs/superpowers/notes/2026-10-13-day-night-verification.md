# Siklus pagi/malam — verifikasi (2026-10-13)

- **Tes EditMode:** 542 lulus (WorldGen + Player + DayCycle), failed=0. Rinciannya 428 tes lama dan 114 tes baru.
- **Generate ulang (Task 6):** `IDENTICAL chunks=278`, karena penanda `StreetLamp` tidak ikut di-hash. Hasil hitung: lampsPlaced=876, marked=876, pointLights=876.
- **Day Cycle Setup (Task 13):**
  - Dijalankan dua kali tanpa duplikat: `roots DayCycle=1 Sun=1 Volume_Day=1 Volume_Horror=1`.
  - Tuning profil dipertahankan: `profile.dayVolume=0.33` setelah Setup kedua.
  - Klip audio, Volume, skybox, ambience, dan Player semuanya terpasang di director.
  - Setup Preview Scene tidak menimpa pencahayaan: `skybox=Sky_Procedural`.
  - Kamera pemain memakai `Skybox`.
- **Play (MCP, `runInBackground` hanya di runtime):**
  - Editor masuk Play dalam keadaan **Pause** (tombol Pause aktif). Pause dilepas lewat `EditorApplication.isPaused = false`, lalu jam berjalan normal sekitar 110–135 fps.
  - Hasil per langkah:
    1. **06:30** — `sunElev=14.6 sun=True fog=0.0044 dayW=0.98 lamps=876 lit=0 flicker=63 dead=20 amb=0.35/0.00/0.00`. Langit biru terang dan ada bayangan matahari (shot_0630).
    2. **12:04** — `sunElev=74.7 fog=0.0040 dayW=1.00 lit=0`, 136 fps. Bayangan pagar jatuh tepat di bawah (shot_1200).
    3. **18:04** — `phase=Dusk sunElev=-4.8 sun=False moon=True fog=0.0149 lit=876 amb=0.15/0.23/0.00`. Langit biru tua, ambient jingga, lampu jalan menyala (shot_1800).
    4. **T dari 12:00 → 19:30** — langsung tampil pesan `Lompat ke 19:30`. Frame berikutnya: `horror=True started=1 event='Malam semakin larut…' fog=0.0320 dayW=0.00 horrorW=1.00 lit=831 amb=0.00/0.40/0.30` (shot_1930). Hasilnya sangat gelap; yard hanya terlihat samar, dan gedung kiri diterangi lampu jalan.
    5. **Kedip** — `lit` berturut-turut 854, 838, 831. Nilai `started` tetap 1. Ada 63 lampu berkedip (7,2%) dan 20 lampu mati (2,3%).
    6. **x60 dari 04:57** — kecepatan diubah dengan pesan `Kecepatan waktu x60`. Status berikutnya: `t=08:00 horror=False ended=1 event='Subuh. Malam sudah lewat.' sun=True lit=0`. Catatan: pada x60, satu detik nyata sama dengan satu jam game.
    7. **F3** — jam tampil `clock='16:28'`. Dua kali `[` mengembalikan kecepatan ke x1 dengan pesan `Kecepatan waktu x1`.
- **Console setelah Play:** tidak ada error maupun warning.
- **git status sebelum/sesudah Play:** identik, jadi tidak ada material, profil, atau scene yang berubah karena Play.
- **Lingkungan:** drive C: penuh (0 byte). Akibatnya Unity gagal menyimpan `TestResults.xml` (IOException 112) di setiap run tes. Error ini tidak memengaruhi hasil tes, tapi bisa mengganggu log atau temp Unity.

## Catatan tuning
- Malam horor (19:30) sangat gelap. Kalau terlalu gelap, beberapa nilai bisa dinaikkan:
  - `ambientSkyNight` / `ambientGroundNight` dan `moonIntensity` di `DayCycleProfile.asset`;
  - atau turunkan `horrorFogMultiplier`, dan kontras di `Volume_Horror.asset`.
- Senter (F) tetap jadi sumber cahaya utama di halaman kos saat horor.

## Checklist QA manual (input asli tidak bisa diotomasi)
- [ ] Play di World.unity: mulai 06:30 terang, tanpa kedip malam→pagi di frame pertama.
- [ ] T bolak-balik 06:30 ↔ 19:30; pesan "Lompat ke …" muncul.
- [ ] `]` / `[` mengubah kecepatan (x1, x10, x60) dengan pesan; F3 menampilkan/menyembunyikan jam kanan atas.
- [ ] T tetap bekerja saat kursor dilepas (Esc).
- [ ] Ambience: burung/angin siang, jangkrik malam, dengung horor; volume seimbang dengan langkah kaki.
- [ ] Senja (x60 dari 17:00): langit, kabut, dan lampu memudar halus, tanpa loncatan.
- [ ] Lampu berkedip terasa seram tapi tidak mengganggu; lampu mati selalu yang sama tiap malam.
- [ ] Tuning bisa di `Assets/DayCycle/Config/DayCycleProfile.asset`, `Volume_Day.asset`, dan `Volume_Horror.asset`.
- [ ] Kalau Play terlihat beku: cek tombol Pause di toolbar (sempat aktif saat verifikasi) dan "Run In Background".
