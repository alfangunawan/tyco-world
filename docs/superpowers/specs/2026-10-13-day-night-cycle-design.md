# Siklus Pagi/Malam — Design Spec

- **Tanggal:** 2026-10-13
- **Status:** Disetujui (plan: docs/superpowers/plans/2026-10-13-day-night-cycle.md)
- **Proyek:** Tyco World (Unity 6000.6.5f1, URP 17.6, Forward+)
- **Sub-proyek:** #3. Dibangun di atas World Generator (#1) dan First-Person Player (#2), keduanya sudah ada di `main`.

## 1. Latar dan tujuan

Visi game: **pagi = hidup sebagai mahasiswa, malam = horor**. Saat ini dunia selalu malam, karena pencahayaannya dipasang tetap oleh "Setup Preview Scene":
- bulan 0,15, ambient flat #121826, kabut 0,02;
- satu Volume `Preview_NightVHS`;
- kamera dengan background warna solid;
- 876 lampu jalan yang selalu menyala.

Sub-proyek ini membuat **jam dunia yang berjalan** dengan transisi cahaya halus subuh → siang → senja → malam, **mode horor** yang aktif di malam hari (visual makin seram, ambience malam, lampu berkedip), dan **tombol debug** untuk loncat waktu.

### Keputusan yang sudah disepakati

| Topik | Keputusan |
|---|---|
| Durasi hari | 24 menit nyata = 24 jam game (1 menit = 1 jam) |
| Jam mulai | 06:30 setiap kali Play |
| Jam horor | 19:00 – 05:00 |
| Isi mode horor | Visual makin seram + ambience siang/malam + lampu jalan berkedip/mati |
| Debug | T loncat 06:30 ↔ 19:30; `[`/`]` kecepatan x1/x10/x60; F3 tampilkan jam |
| Pendekatan | Keyframe data (`DayCycleProfile`) + blending oleh satu director |
| Branch | `feature/day-night-cycle` dari `main` (PR #2 sudah di-merge) |

## 2. Ruang lingkup

### Termasuk
- Jam dunia, fase hari, dan event `HorrorStarted`/`HorrorEnded`.
- Matahari baru (directional, soft shadow) dan bulan yang sudah ada, dengan arah, warna, dan intensitas mengikuti jam.
- Skybox procedural, ambient Trilight, dan kabut yang mengikuti jam.
- Tiga Volume global (Siang, Malam, Horor) yang dicampur lewat bobot.
- Lampu jalan nyala/mati otomatis, emisi kepala lampu, serta kedip dan mati saat horor.
- Ambience siang, malam, dan dengung horor (crossfade).
- Label jam di HUD (saat debug) dan pesan HUD saat horor mulai/berakhir.
- Tombol debug (Editor dan Development Build).
- Menu editor "Tyco World/Day Cycle Setup".
- Komponen penanda `StreetLamp` di prefab lampu (WorldGen).

### Tidak termasuk (non-goals)
- Musuh, event cerita, dan quest (sub-proyek cerita).
- Tidur atau melewati malam lewat interaksi; interior kos.
- Save/load jam, cuaca/hujan, dan fase bulan.
- Jendela bangunan yang menyala di malam hari.
- Baked GI dan reflection probe.

## 3. Arsitektur

### Assembly

| Assembly | Folder | Platform | Referensi |
|---|---|---|---|
| `TycoWorld.DayCycle` | `Assets/DayCycle/Runtime` | semua | `TycoWorld.WorldGen`, `TycoWorld.Player`, `Unity.InputSystem`, URP Core/Universal Runtime |
| `TycoWorld.DayCycle.Editor` (namespace `TycoWorld.DayCycle.Authoring`) | `Assets/DayCycle/Editor` | Editor | DayCycle, Player, Player.Editor, WorldGen, WorldGen.Editor, URP |
| `TycoWorld.DayCycle.Tests` | `Assets/DayCycle/Tests` | Editor | semua di atas + TestRunner; `overrideReferences` nunit; `UNITY_INCLUDE_TESTS` |

- Arah dependensi: DayCycle → Player → WorldGen. Tidak ada yang mengarah ke DayCycle.
- `McpTestRunner` diubah supaya menjalankan ketiga assembly tes.

### Unit

| Unit | Jenis | Tugas |
|---|---|---|
| `WorldClock` | C# murni | Jam game `Hours` (0–24, float), `Day` (dimulai dari 1). `Tick(dt)`: `Hours += dt / 60 × SpeedMultiplier` (1 menit nyata = 1 jam). Lewat 24 → `Day++`. `SetTime(hours)`, `SpeedMultiplier` (1/10/60). |
| `DayPhase` | C# murni | `Phase` = Subuh (05:00–06:30), Siang (06:30–17:30), Senja (17:30–19:00), Malam (19:00–05:00). `IsHorror` = 19:00 ≤ jam atau jam < 05:00. `Update(hours)` mengembalikan transisi; event `HorrorStarted`/`HorrorEnded` tepat sekali saat status berubah, termasuk saat loncat. |
| `DayCycleProfile` | ScriptableObject | Kurva dan gradien per jam: elevasi dan azimut matahari, warna/intensitas matahari dan bulan, ambient langit/horizon/tanah, warna dan densitas kabut, tint dan exposure skybox, bobot Volume Siang, intensitas lampu, volume ambience siang/malam. Bagian horor: kurva bobot Horor (terhadap waktu sejak 19:00), pengali kabut, rasio lampu berkedip/mati. |
| `LightingState` | struct murni | Hasil `DayCycleProfile.Evaluate(hours, horrorWeight)`, berisi semua nilai di atas. Dites tanpa scene. |
| `DayCycleDirector` | MonoBehaviour (World.unity) | Satu-satunya `Update`. Urutannya: clock → phase → `Evaluate` → apply (matahari/bulan, `RenderSettings`, skybox, 3 Volume, lampu, ambience, HUD) → event. Ekspos `Clock`, `Phase`, `IsHorror`, event `HorrorStarted`/`HorrorEnded`. `Initialize()`/`Tick(dt)` eksplisit untuk tes. |
| `StreetLamp` | MonoBehaviour runtime (`TycoWorld.WorldGen`) | Penanda di prefab lampu: `Light` dan renderer kepala lampu. Dipasang oleh `PlaceholderPrefabFactory`. |
| `StreetLampSystem` | C# + MonoBehaviour | Mengumpulkan semua `StreetLamp` saat dunia siap. `Apply(intensity, horrorWeight, time)`: nyala/mati light, emisi kepala lampu lewat **salinan material runtime** (asset tidak berubah), dan pola kedip deterministik. |
| `LampFlicker` | C# murni | Pemilihan lampu per posisi (hash): sekitar 8% berkedip, sekitar 3% mati, sama tiap malam. Pola kedip dari noise per lampu: padam 0,05–0,3 s. |
| `AmbienceAudio` | MonoBehaviour | Tiga AudioSource 2D loop (siang, malam, dengung). Volume dari `LightingState` dan bobot horor. |
| `DayCycleDebug` | MonoBehaviour + logika murni `DebugTimeControls` | T, `[`, `]`, F3 lewat `Keyboard.current`. Aktif hanya kalau `Application.isEditor \|\| Debug.isDebugBuild`, dan tetap jalan walaupun kontrol pemain sedang dikunci. |

**Perubahan kecil di kode lama:**
- `HudModel`/`HudView`: tambah label jam di pojok kanan atas (`Clock`, `ClockVisible`).
- `PlaceholderPrefabFactory`: pasang `StreetLamp` di prefab lampu.
- `PreviewSceneSetup`: tidak menimpa pencahayaan kalau `DayCycleDirector` ada di World.unity.
- Kamera pemain: background Skybox (diatur oleh Day Cycle Setup).

### Alur saat Play
1. `DayCycleDirector.Awake → Initialize()`: clock = 06:30, phase dihitung tanpa memicu event, pencahayaan diterapkan di frame pertama (tidak ada kedip malam→pagi).
2. `World_Generated` dimuat oleh `WorldSceneLoader` → `StreetLampSystem` mengambil semua `StreetLamp` begitu `GeneratedRoot` ada (polling ringan sampai ketemu).
3. Setiap frame: `Tick(Time.deltaTime)`. Saat 19:00 terlewati, `HorrorStarted` → HUD *"Malam semakin larut…"*. Saat 05:00, `HorrorEnded` → *"Subuh. Malam sudah lewat."*
4. Sistem cerita nanti mencari `DayCycleDirector` dan berlangganan event-nya. Tidak ada state static.

## 4. Nilai visual awal (`DayCycleProfile`, bisa di-tuning)

**Matahari dan bulan**

| Elemen | Nilai |
|---|---|
| Matahari | Terbit ±05:45, terbenam ±17:45, dari timur ke barat, elevasi puncak ±75° pukul 12:00. Warna #FFB070 (terbit/terbenam) → #FFF4E0 (siang). Intensitas 0 → 1,3 → 0. Soft shadow; light dimatikan kalau elevasi < 0. |
| Bulan | `Moon` yang sudah ada (#9DB4FF, 0,15), aktif saat matahari di bawah horizon. `RenderSettings.sun` mengikuti sumber yang aktif. |

**Langit dan ambient**

| Elemen | Nilai |
|---|---|
| Skybox | Material `Skybox/Procedural` (salinan runtime). Exposure: siang 1,3, senja 0,8, malam 0,08. |
| Ambient (Trilight) | Siang: #9FB8D8 / #8A9AA8 / #5A5040. Senja: jingga keunguan. Malam: #121826 (sama dengan sekarang). |

**Kabut** (eksponensial)

| Waktu | Densitas | Warna |
|---|---|---|
| Siang | 0,004 | #B8C4CC |
| Senja | 0,008 | #C08A6A |
| Malam | 0,02 | #0E1218 |
| Horor | malam × 1,6 | lebih gelap |

**Efek layar** (3 Volume global)

| Volume | Isi |
|---|---|
| Siang | Profil baru: grain 0,15, vignette 0,15, saturasi +5, bloom 0,3, tanpa CA. |
| Malam | `Preview_NightVHS` (sudah ada). Bobot = 1 − bobot Siang. |
| Horor | Profil baru di atas Malam (prioritas lebih tinggi): grain 0,8, vignette 0,5, CA 0,3, saturasi −45, kontras +20. Bobot naik 0 → 1 dalam 30 menit game setelah 19:00 dan turun ke 0 dalam 30 menit menjelang 05:00. |

**Lampu jalan**

| Kondisi | Perilaku |
|---|---|
| Elevasi matahari < 5° (±17:50–06:00) | Menyala. Intensitas 0 → 3 memudar dalam ±20 menit game. Emisi kepala lampu ikut. |
| Siang | 876 light dimatikan (lebih ringan). |
| Saat horor | ±8% berkedip, ±3% mati total (deterministik per lampu). |

**Ambience**

| Lapis | Kapan |
|---|---|
| Siang (burung, angin, motor jauh) | penuh 06:00–17:30 |
| Malam (jangkrik, angin) | penuh 18:30–05:00; crossfade dengan siang pada 17:30–18:30 dan 05:00–06:00 |
| Dengung horor | volume = bobot horor |

## 5. Audio

- Dicari dulu loop CC0 dengan lisensi yang jelas (OpenGameArt dan sejenisnya), lalu dicatat di `Assets/DayCycle/Audio/SOURCES.md`.
- Kalau tidak ada yang cocok, `AmbienceSynth` (editor) menyintesis WAV deterministik dari seed tetap: angin (noise tersaring), jangkrik (chirp sinus termodulasi), burung (sapuan frekuensi pendek), dan dengung (sinus rendah + noise).
- Klip disimpan di `DayCycleProfile` dan bisa diganti kapan saja.
- Import mono kecuali loop stereo hasil unduhan; pilih Vorbis dan *Compressed In Memory* untuk loop panjang.

## 6. Penanganan error

| Kondisi | Perilaku |
|---|---|
| Profil kosong | Pakai `DayCycleProfile.CreateDefault()` dan satu warning. |
| Dunia lama tanpa `StreetLamp` | Lampu dibiarkan seperti apa adanya, dan satu warning: *"Generate ulang untuk lampu dinamis"*. |
| Klip audio, Volume, atau matahari kosong | Bagian itu dilewati tanpa error. |
| Release build | `DayCycleDebug` nonaktif. |
| Day Cycle Setup dijalankan ulang | Idempoten. Profil, profil Volume, dan klip yang sudah ada tidak ditimpa (hanya dibuat kalau belum ada). |

## 7. Pengetesan

### EditMode (`TycoWorld.DayCycle.Tests`)
- **`WorldClock`**: laju 1 menit = 1 jam; x10/x60; lewat 24 → `Day++`; `SetTime`.
- **`DayPhase`**:
  - batas fase;
  - `HorrorStarted` tepat sekali saat melewati 19:00, `HorrorEnded` tepat sekali di 05:00;
  - loncat 06:30 → 19:30 memicu `HorrorStarted`, dan loncat balik memicu `HorrorEnded`;
  - inisialisasi tidak memicu event.
- **`DayCycleProfile.Evaluate`**:
  - elevasi matahari jam 06:00 rendah, jam 12:00 sekitar 75°, jam 18:30 di bawah 0;
  - kabut dan bobot Volume di siang, senja, malam;
  - bobot Horor 0 jam 18:59, naik setelah 19:00, 1 jam 19:30 ke atas, 0 jam 05:00.
- **`LampFlicker`**: deterministik; rasio ±8%/±3% pada 1.000 posisi acak (toleransi); pola kedip berulang sama.
- **`DebugTimeControls`**: T bolak-balik 06:30/19:30; urutan kecepatan 1 → 10 → 60 → 1; F3 toggle.
- **Komponen**:
  - `DayCycleDirector.Tick` menerapkan ambient, kabut, matahari/bulan, dan bobot Volume (RenderSettings disimpan dan dipulihkan di TearDown);
  - `StreetLampSystem` memakai salinan material (warna emisi asset tidak berubah);
  - volume `AmbienceAudio` mengikuti jam;
  - `HudView` merender label jam.
- **WorldGen**: prefab lampu punya `StreetLamp`.

### Integrasi (MCP)
- Generate ulang: hash chunk identik dengan sebelumnya (komponen tidak di-hash), dan semua lampu punya `StreetLamp`.
- Day Cycle Setup idempoten, dan Setup Preview Scene tidak menimpa pencahayaan.
- Play (runtime `runInBackground`, sama seperti sub-proyek #2):
  - screenshot 06:30 terang;
  - T → 19:30: gelap, lampu menyala, event horor dan pesan HUD;
  - x60 sampai 05:00 → `HorrorEnded`;
  - lampu berkedip saat horor;
  - Console bersih.
  - Screenshot dikirim ke user.

## 8. Kriteria selesai
- Semua tes lama dan baru lulus, dan generate tetap deterministik.
- Play di World.unity mulai 06:30 dengan dunia terang, lalu berangsur malam. Horor 19:00–05:00 sesuai §4. Tombol debug bekerja.
- Console bersih. Catatan verifikasi ditulis di `docs/superpowers/notes/`.

## 9. Risiko
- **Skybox procedural dan kabut tidak serasi di horizon.** Warna kabut diambil dari profil per jam; di-tuning lewat screenshot.
- **Batas light Forward+ di malam hari** (876 lampu + senter). Sudah aman di sub-proyek #2; kedip tidak menambah light.
- **Kualitas suara sintetis.** Placeholder, bisa diganti klip asli lewat profil.
- **Bayangan matahari pada 5.700+ collider/mesh statis.** Ukur frame time siang di Play; shadow distance tetap 50.

## 10. Iterasi berikutnya
1. Sistem cerita/quest: berlangganan `HorrorStarted`, jadwal kuliah per jam.
2. Musuh malam, memakai `IsHorror`.
3. Tidur di kos untuk melewati malam (setelah interior ada).
4. Jendela bangunan menyala di malam hari.

## 11. Catatan implementasi (dari plan)

- **Lapisan Volume.** URP mencampur Volume global urut prioritas.
  - Malam (`Global Volume`, `Preview_NightVHS`, prioritas 0) jadi dasar dengan bobot tetap 1.
  - Siang (prioritas 1, bobot = bobot Siang) meng-override semua parameter yang di-override Malam, sehingga hasilnya sama dengan lerp(Malam, Siang). Ini menggantikan rumus "Malam = 1 − Siang" di §4.
  - Horor (prioritas 2, bobot = bobot Horor) di atas keduanya.
- **`DayCycleProfile.Evaluate(hours)`** menghitung bobot horor sendiri dari jam. Tidak ada parameter `horrorWeight`.
- **`StreetLampSystem`** adalah kelas C# milik director, bukan MonoBehaviour. Kepala lampu memakai dua salinan material runtime ("On" dengan emisi × intensitas, "Off" hitam) yang dipakai bersama semua lampu. `sharedMaterials` hanya diganti saat status nyala berubah.
- **Kamera pemain** memakai `CameraClearFlags.Skybox` dari `PlayerRigBuilder`. Day Cycle Setup memberi warning kalau kamera di scene belum skybox, yang berarti Player Setup perlu dijalankan ulang.
- **Matahari** dimatikan di Edit mode; director menyalakannya saat Play. Di editor, skybox scene = `Sky_Procedural.mat`; saat Play dipakai salinannya.
- **Bulan saat senja dan subuh.** Begitu matahari turun di bawah horizon, `RenderSettings.sun` pindah ke bulan. Supaya langit procedural tidak meloncat, rotasi bulan di-slerp dari arah matahari (di horizon) ke pose scene-nya. Bobotnya smoothstep dari elevasi 0° sampai −12° (batas twilight profil). Di malam yang lebih dalam, bulan memakai pose scene yang disimpan saat `Initialize`.
- **Audio.**
  - Malam: "Crickets Ambient Noise – loopable" (Wolfgang_, OpenGameArt, CC0).
  - Siang dan dengung: sintesis `AmbienceSynth`.
  - Kalau unduhan jangkrik hilang, `ambience_night.wav` hasil sintesis dipakai.
- **`DayCycleDirector.FindWorldRoot`** adalah hook `Func<GeneratedRoot>`, karena dunia ada di scene lain dan tidak bisa diserialisasi. Lampu dicari tiap frame sampai `GeneratedRoot` ada, lalu pencarian berhenti.
