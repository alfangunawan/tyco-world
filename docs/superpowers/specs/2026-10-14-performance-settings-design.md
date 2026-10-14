# Performa + Menu Pause/Settings — Design Spec

- **Tanggal:** 2026-10-14
- **Status:** Draft, menunggu review user
- **Proyek:** Tyco World (Unity 6000.6.5f1, URP 17.6, Forward+, GPU Resident Drawer)
- **Sub-proyek:** #4. Dibangun di atas World Generator (#1), First-Person Player (#2), dan Siklus Pagi/Malam (#3). Ketiganya sudah ada di `main` sebelum sub-proyek ini mulai.

## 1. Latar dan tujuan

Dunia hasil generate cukup berat. Dari hasil hitung di `World_Generated.unity`:
- 5.741 MeshRenderer, satu per fitur, tanpa penggabungan mesh;
- 2.154 prefab instance (1.278 pohon dan 876 lampu jalan);
- tidak ada LOD dan tidak ada data occlusion;
- *far clip* kamera tetap 400 m;
- **malam hari: 876 point light realtime menyala sekaligus di seluruh peta**, dan Forward+ meng-cluster semuanya.

Game juga belum punya menu: Esc hanya melepas kursor, dan belum ada pause atau pengaturan.

Sub-proyek ini membuat game **lebih ringan**: objek dirender sesuai jarak ke pemain, lampu jalan dijatah, dan ada preset kualitas plus upscaler. Sub-proyek ini juga membuat **menu Pause + halaman Pengaturan**, yang nanti jadi wadah toggle fitur berikutnya (minimap, HUD).

### Keputusan yang sudah disepakati

| Topik | Keputusan |
|---|---|
| Urutan roadmap | Fix day/night + merge → **#4 Performa + Settings** → #5 Motor → #6 Minimap + HUD |
| Render sesuai jarak | Pendekatan A: runtime saja, tanpa generate ulang. *Far clip* = view distance, kabut menutup tepi pandang, renderer dimatikan per chunk × kategori |
| View distance | Slider 100–800 m dengan langkah 50 m. Nilai awal ikut preset (Sedang = 300 m) |
| Optimasi tambahan | Jatah lampu jalan terdekat, jarak per kategori + kabut, GPU occlusion culling, upscaler STP + preset kualitas |
| Isi Settings | Tab Grafis, Kontrol, Audio, Tampilan |
| Esc | Membuka menu pause. Waktu game berhenti, audio di-pause, kursor dilepas |
| Branch | `feature/performance-settings` dari `main`, setelah day/night di-merge |

## 2. Ruang lingkup

### Termasuk
- Menu Pause (Lanjutkan / Pengaturan / Keluar) dan halaman Pengaturan dengan 4 tab.
- Model pengaturan, preset Rendah/Sedang/Tinggi/Kustom, dan penyimpanan di PlayerPrefs.
- Penerapan langsung (live) setiap kali nilai berubah.
- View distance: *far clip* kamera, kabut minimum, dan culling renderer per kategori.
- Jatah lampu jalan: hanya N lampu terdekat yang `Light`-nya menyala.
- Pengaturan kualitas lewat **salinan runtime** URP asset: bayangan, render scale + STP, MSAA, GPU occlusion, dan small-mesh culling. SSAO memakai renderer kedua.
- Batas FPS, VSync, mode layar, dan resolusi.
- Sensitivitas mouse, invert Y, dan FOV.
- Volume Master, Efek, dan Ambience.
- Penghitung FPS dan toggle jam.
- Menu editor "Tyco World/Game Settings Setup".
- Pengukuran performa sebelum/sesudah, dengan tabel di catatan verifikasi.

### Tidak termasuk (non-goals)
- Menggabungkan mesh per chunk, LOD, dan impostor. Ini perubahan generator, baru dipertimbangkan kalau hasil ukur masih kurang.
- Baked occlusion culling (Umbra), streaming scene async, dan memuat/membuang chunk.
- Main menu sebelum masuk game, save game, remap tombol, navigasi menu pakai gamepad, dan bahasa selain Indonesia.
- Minimap, HUD baru, dan motor (sub-proyek #5 dan #6).

## 3. Arsitektur

### Assembly

| Assembly | Folder | Platform | Referensi |
|---|---|---|---|
| `TycoWorld.GameSettings` | `Assets/GameSettings/Runtime` | semua | `TycoWorld.Player`, `TycoWorld.DayCycle`, `TycoWorld.WorldGen`, `Unity.InputSystem`, URP Core/Universal Runtime |
| `TycoWorld.GameSettings.Editor` (namespace `TycoWorld.GameSettings.Authoring`) | `Assets/GameSettings/Editor` | Editor | GameSettings, Player, Player.Editor, DayCycle, WorldGen, WorldGen.Editor, URP |
| `TycoWorld.GameSettings.Tests` | `Assets/GameSettings/Tests` | Editor | semua di atas + TestRunner; `overrideReferences` nunit; `UNITY_INCLUDE_TESTS` |

- Folder `Assets/Settings` sudah dipakai URP, jadi folder baru diberi nama `Assets/GameSettings`.
- **Arah dependensi:** GameSettings → Player/DayCycle/WorldGen, tidak pernah sebaliknya.
  - Modul lama tidak tahu soal pengaturan. Mereka cuma menyediakan *knob*, yaitu properti runtime yang nilai awalnya diambil dari asset/tuning.
  - **Asset tidak pernah diubah saat runtime.**
- `McpTestRunner` ditambah assembly `TycoWorld.GameSettings.Tests`.

### Knob baru di modul lama

| Modul | Knob | Default |
|---|---|---|
| WorldGen runtime | `WorldChunkIndex` (baru) + `DistanceCuller` (baru, komponen): `ViewDistance`, `PropDistanceRatio` | culler tidak aktif selama tidak ada di scene |
| Player | `FirstPersonPlayer.LookSensitivity` (pengali), `InvertY` (hanya untuk input asli; `ScriptedIntentSource` tidak terpengaruh), `EscapeUnlocksCursor` | 1, false, true |
| DayCycle | `DayCycleDirector.MinFogDensity` | 0 |
| DayCycle | `StreetLampSystem.LightBudget` / `MaxLightDistance` | tanpa batas (perilaku lama) |
| DayCycle | `AmbienceAudio.Level` (pengali volume) | 1 |

FOV dan *far clip* langsung diset ke `FirstPersonPlayer.View` (Camera). Jam memakai `DayCycleDirector.ShowClock` yang sudah ada.

### Unit di `TycoWorld.GameSettings`

**Logika murni (dites di EditMode):**
- `GameSettings`: data serializable dengan `version`. Isinya grafis, kontrol, audio, dan tampilan (§4).
- `QualityPresets`: `Apply(Preset, GameSettings)` dan `Matches(GameSettings) → Preset`. Kalau nilai grafis tidak cocok dengan preset mana pun, hasilnya Kustom.
- `SettingsStore`: `Load()/Save()` lewat `ISettingsBackend`. Backend default `PlayerPrefsBackend` menyimpan satu key `tyco.settings` berisi JSON.
- `ViewDistanceRules`: `FarClip(D) = 1,05·D`, `MinFog(D) = 2,3 / D`, `PropDistance(D) = max(80, 0,6·D)`.

**Komponen:**
- `SettingsApplier` menerapkan `GameSettings` ke kamera, `DistanceCuller`, DayCycle, `QualityApplier`, audio, dan layar.
- `QualityApplier` mengelola salinan URP asset dan snapshot QualitySettings (§5.4).
- `PauseController` dan `GameMenu`: UI Toolkit yang dibangun dari kode (`PauseMenuView`, `SettingsView`, `FpsCounterView`), memakai pola `HudView`.

### Alur saat Play
1. `SettingsApplier.Awake`:
   - `Load()`, dengan fallback ke default;
   - `QualityApplier` membuat salinan URP asset dan merekam QualitySettings;
   - semua pengaturan diterapkan sebelum frame pertama.
2. Setelah `GeneratedRoot` muncul (dicek tiap frame sampai ketemu), `DistanceCuller` membangun `WorldChunkIndex`.
3. **Setiap 0,25 s:**
   - `DistanceCuller` mengecek jarak dari kamera aktif (`Camera.main`);
   - `StreetLampSystem` memilih N lampu terdekat.
4. **Esc** → `PauseController.Open()`:
   - `Time.timeScale = 0`;
   - `AudioListener.pause = true`;
   - `Locks.Acquire(pause, All)`;
   - kursor dilepas.

   Tombol Lanjutkan atau Esc lagi membalik semua langkah itu, termasuk mengunci kursor lagi.
5. Saat Pengaturan ditutup: `Save()`.
6. Saat Play berhenti atau aplikasi ditutup: `QualityApplier` mengembalikan URP asset asli dan nilai QualitySettings.

## 4. Pengaturan

### Isi per tab

| Tab | Item | Rentang / pilihan |
|---|---|---|
| Grafis | Preset | Rendah / Sedang / Tinggi / Kustom |
| | View distance | 100–800 m, langkah 50 |
| | Render scale | 50–100 %, langkah 5. Di bawah 100% memakai upscaler STP |
| | Bayangan | Mati / Rendah / Sedang / Tinggi |
| | SSAO | on/off |
| | GPU occlusion culling | on/off |
| | Lampu jalan menyala | 8 / 16 / 32 |
| | Batas FPS | 30 / 60 / 120 / Tanpa batas |
| | VSync | on/off |
| | Mode layar | Layar penuh / Jendela |
| | Resolusi | daftar `Screen.resolutions` tanpa duplikat |
| Kontrol | Sensitivitas mouse | 0,2–3,0× |
| | Invert Y | on/off |
| | FOV | 60–100° |
| Audio | Master / Efek / Ambience | 0–100 % |
| Tampilan | Penghitung FPS | on/off (default off) |
| | Jam | on/off (default off; F3 debug tetap bisa) |

Tombol di bawah halaman: **Kembalikan default** dan **Kembali**.

### Preset (default: Sedang)

| | Rendah | Sedang | Tinggi |
|---|---|---|---|
| View distance | 200 m | 300 m | 500 m |
| Render scale | 70 % + STP | 85 % + STP | 100 %, MSAA 2× |
| Bayangan | Rendah: 30 m, 1 cascade, 1024, keras | Sedang: 50 m, 2 cascade, 2048, halus | Tinggi: 80 m, 4 cascade, 2048, halus |
| SSAO | mati | mati | nyala |
| GPU occlusion | mati | mati | mati |
| Lampu menyala | 8 | 16 | 32 |
| Small-mesh culling | 2 % layar | 1 % | 0 |

- Batas FPS (default 60), VSync (default mati), mode layar, dan resolusi **tidak ikut preset**.
- Kalau pengukuran (§7) menunjukkan GPU occlusion menurunkan FPS, default-nya diubah jadi mati di semua preset, dan hasilnya dicatat.
- Bayangan "Mati" mematikan bayangan matahari dan bulan. Bayangan senter tetap aktif, karena senter penting untuk horor.

### Penyimpanan
- Satu key PlayerPrefs `tyco.settings` berisi JSON `GameSettings`, dengan `version = 1`.
- Kalau JSON tidak bisa dibaca, `version` tidak dikenal, atau ada nilai di luar rentang: nilai dikembalikan ke rentang (clamp) atau ke default, dan muncul satu warning `[Settings]`.
- Disimpan saat halaman Pengaturan ditutup. Selama halaman terbuka, perubahan langsung diterapkan tanpa disimpan.

## 5. Performa

### 5.1 Culling per kategori (`WorldChunkIndex` + `DistanceCuller`)
- **Indeks:** membaca anak `GeneratedRoot` dengan nama `Roads|Buildings|Areas|Water|Props/Trees|Props/Lamps` → `Chunk_x_z`.
  - Setiap container menjadi satu grup: kategori, daftar `Renderer`, dan bounds gabungan.
  - Total sekitar 814 grup.
  - `Ground`, `KeyBuildings`, dan `WorldEdge` tidak diindeks, jadi selalu tampil.
- **Jarak** dihitung ke tepi bounds (`max(0, |p − c| − r)`), jadi jalan/area panjang tetap tampil selama ada bagian yang dekat.

| Kategori | Tampil sampai |
|---|---|
| Jalan, area, air, gedung | D |
| Pohon, tiang lampu | `PropDistance(D)` = max(80, 0,6·D) |

- **Histeresis 10 m:** grup baru disembunyikan setelah melewati batas + 10 m, dan baru tampil lagi setelah masuk batas − 10 m.
- Yang dimatikan hanya `Renderer.enabled`. Collider tetap aktif, supaya fisika, `GroundProbe`, dan motor nanti tidak terpengaruh.
- Pengecekan tiap 0,25 s, atau langsung saat kamera melompat lebih dari 50 m (teleport/respawn).
- Kalau `GeneratedRoot` tidak ada, culler diam tanpa error.

### 5.2 Kabut dan *far clip*
- `camera.farClipPlane = 1,05·D`.
- `DayCycleDirector.MinFogDensity = 2,3 / D`, dan director memakai `max(profil, MinFogDensity)`. Artinya tepi pandang tertutup kabut sekitar 90%.
- Contoh nilai:
  - D = 300 → 0,0077, jadi siang sedikit lebih berkabut daripada sekarang (0,004);
  - D = 500 → 0,0046;
  - D = 800 → 0,0029, sehingga nilai profil yang berlaku.
- Kalau kabut profil lebih tebal (malam, horor), nilai profil yang dipakai.

### 5.3 Jatah lampu jalan
- `StreetLampSystem`: setiap 0,25 s, pilih N lampu yang **sedang menyala** dan paling dekat ke kamera dalam radius `MaxLightDistance` (= `PropDistance(D)`).
  - Hanya lampu terpilih yang `Light.enabled = true`.
  - Lampu yang baru masuk jatah fade-in 0,5 s.
  - Lampu yang keluar jatah fade-out 0,5 s, lalu dimatikan.
- Kepala lampu (emissive), kedip, dan lampu mati saat horor tetap seperti sekarang.
- Per frame, sistem hanya menyentuh lampu yang masuk jatah, tidak lagi 876 lampu sekaligus. Ini menutup temuan review day/night Minor #8.

### 5.4 Kualitas (`QualityApplier`)
- **Salinan runtime:** `copy = Instantiate(QualitySettings.renderPipeline)` dengan `HideFlags.DontSave`, lalu `QualitySettings.renderPipeline = copy`. Nilai yang diubah, hanya pada salinan:
  - `shadowDistance`, `shadowCascadeCount`, `mainLightShadowmapResolution`, kualitas soft shadow;
  - `renderScale`, `upscalingFilter` (STP);
  - `msaaSampleCount`;
  - `gpuResidentDrawerEnableOcclusionCullingInCameras`;
  - `smallMeshScreenPercentage`.
- **SSAO:** menu Setup menambahkan `PC_Renderer_NoSSAO`, yaitu salinan `PC_Renderer` tanpa fitur SSAO, ke daftar renderer `PC_RPAsset`. Perubahan ini dilakukan sekali di editor dan di-commit. Saat runtime, kamera memakai `UniversalAdditionalCameraData.SetRenderer(index)`.
- **Bayangan Mati:** `renderShadows = false` di kamera dan jarak bayangan 0 di salinan. Senter memakai additional-light shadow, jadi tetap aktif.
- **Snapshot:** `renderPipeline` asli, `vSyncCount`, dan `Application.targetFrameRate` direkam di `Awake`, lalu dikembalikan di `OnDestroy` dan `Application.quitting`.
  - Tujuannya supaya `ProjectSettings/QualitySettings.asset` dan `PC_RPAsset.asset` tidak berubah setelah Play, dibuktikan dengan `git status` (§7).
- **Batas FPS** lewat `Application.targetFrameRate`. **VSync** lewat `QualitySettings.vSyncCount`. **Mode layar/resolusi** lewat `Screen.SetResolution`; di editor tidak berefek, jadi dicek manual di build.

### 5.5 Audio
- **Master:** `AudioListener.volume`.
- **Ambience:** `AmbienceAudio.Level`.
- **Efek:** `AudioSource.volume` diset ke level Efek untuk semua AudioSource selain milik ambience. `PlayOneShot` mengalikan volume source, jadi footstep dan interaksi ikut terpengaruh tanpa perlu mengubah Player.
- Diterapkan ulang saat `sceneLoaded`.
- Motor (#5) nanti membaca level Efek sendiri untuk suara mesinnya.
- AudioMixer tidak dipakai, karena asset-nya tidak bisa dibuat dari kode lewat API publik.

## 6. Menu

- **Satu `UIDocument` "GameMenu"** yang memakai `PlayerHudPanel` dengan `sortingOrder` di atas HUD. Dibangun dari kode dan memakai tema default yang sudah ada di `PlayerHudTheme.tss`.
- **Pause:** layar digelapkan (hitam 60%), panel di tengah dengan judul "Jeda" dan tombol **Lanjutkan**, **Pengaturan**, **Keluar**.
  - Keluar meminta konfirmasi "Keluar dari game?".
  - Di editor, Keluar menghentikan Play; di build, Keluar memanggil `Application.Quit`.
- **Pengaturan:** 4 tombol tab + isi tab memakai `Slider`/`SliderInt`, `Toggle`, dan `DropdownField`, masing-masing dengan label nilai (mis. "300 m", "85 %").
- **Esc:**
  - Pause sedang tertutup → buka.
  - Pause terbuka di halaman utama → tutup.
  - Halaman Pengaturan terbuka → kembali ke menu pause, sambil menyimpan.
- Saat `PauseController` aktif, `FirstPersonPlayer.EscapeUnlocksCursor = false`. Tanpa `PauseController` (scene uji), Esc tetap melepas kursor seperti sekarang.
- Esc saat dunia belum siap (`PlayerSpawner` masih menunggu): menu boleh dibuka. Lanjutkan hanya melepas kunci milik pause, sedangkan kunci milik spawner tetap ada, karena `ControlLocks` dipisah per pemilik.
- **Penghitung FPS:** label kecil di pojok kiri atas, rata-rata 0,5 s, dan tetap ada di luar menu.
- **Spike pertama:** apakah UI Toolkit runtime dengan Input System saja (input lama nonaktif) bisa menerima klik/hover tanpa EventSystem. Kalau tidak, Setup menambahkan `EventSystem` + `InputSystemUIInputModule` ke `World.unity`.

### Menu editor "Tyco World/Game Settings Setup"
Aman dijalankan berulang (idempoten), dengan pola yang sama seperti hasil perbaikan Day Cycle Setup:
- **referensi diisi hanya kalau kosong;**
- objek baru diberi nilai awal hanya saat dibuat pertama kali.

Langkahnya:
1. Cek scene sudah tersimpan.
2. Buat `PC_Renderer_NoSSAO` dan daftarkan ke `PC_RPAsset` kalau belum ada.
3. Buat `GameSystems` di `World.unity`, berisi `SettingsApplier`, `QualityApplier`, `PauseController`, `DistanceCuller`, dan `UIDocument`. Tambahkan EventSystem kalau spike menunjukkan perlu.
4. Simpan.

## 7. Pengetesan

### EditMode (`TycoWorld.GameSettings.Tests` + tambahan di tes modul lama)
- `QualityPresets`:
  - setiap preset mengisi nilai sesuai tabel §4;
  - mengubah satu nilai mengubah hasil `Matches` jadi Kustom;
  - batas FPS dan resolusi tidak memengaruhi `Matches`.
- `SettingsStore`:
  - nilai yang disimpan bisa dibaca kembali sama persis;
  - JSON rusak → default + 1 warning;
  - nilai di luar rentang di-clamp;
  - `version` tidak dikenal → default.
- `ViewDistanceRules`: *far clip*, kabut minimum, dan jarak props di D = 100/300/800.
- `WorldChunkIndex`, dengan hierarki sintetis:
  - kategori dan koordinat chunk terbaca dari nama;
  - container yang tidak dikenal diabaikan;
  - Ground/KeyBuildings tidak diindeks.
- `DistanceCuller`:
  - renderer mati di luar D dan nyala di dalamnya;
  - histeresis ±10 m;
  - props memakai jarak props;
  - collider tetap aktif;
  - lompatan >50 m langsung dievaluasi.
- `StreetLampSystem`:
  - jatah N memilih lampu terdekat yang menyala;
  - batas radius berlaku;
  - fade masuk/keluar;
  - tanpa jatah = perilaku lama.
- `DayCycleDirector`: `MinFogDensity` menang atas profil siang, dan kalah dari profil horor.
- `FirstPersonPlayer`:
  - `LookSensitivity` dan `InvertY` berlaku untuk input asli, tidak untuk scripted;
  - `EscapeUnlocksCursor = false` membuat Esc tidak melepas kursor.
- `PauseController`, memakai fake:
  - `Open` → timeScale 0, audio di-pause, kunci All, kursor lepas;
  - `Close` mengembalikan semuanya;
  - kunci milik spawner tidak tersentuh.
- `QualityApplier`:
  - salinan dibuat, asset asli tidak dirty;
  - nilai bayangan, render scale, dan MSAA masuk ke salinan;
  - `Restore` mengembalikan `renderPipeline` dan `vSyncCount`.
- UI:
  - `PauseMenuView`/`SettingsView` membangun elemen dengan nama yang diharapkan;
  - menggeser slider mengubah model dan label;
  - memilih preset mengisi semua kontrol grafis.

### Integrasi (MCP)
- **Baseline** diambil di task pertama, sebelum ada perubahan apa pun. Helper `McpPerfProbe` (assembly tes) masuk Play, menaruh kamera di 4 titik tetap, dan merekam rata-rata 5 s.
  - **Titik:**
    1. halaman kos siang;
    2. jalan utama menghadap kota, siang;
    3. titik yang sama jam 21:00;
    4. pemandangan jauh dari ketinggian 30 m.
  - **Yang direkam:**
    - FPS rata-rata dan waktu frame;
    - `UnityStats` (batches, SetPass calls, segitiga);
    - jumlah `Light` aktif;
    - jumlah `Renderer` aktif.
- **Setelah selesai:** ukur ulang untuk preset Rendah/Sedang/Tinggi, lalu buat tabel sebelum/sesudah di `docs/superpowers/notes/2026-10-14-performance-verification.md`.
- **Screenshot** D = 150 dan D = 800 dari titik 2, untuk cek apakah tepi pandang tertutup kabut tanpa *pop-in* yang mencolok.
- **Pause lewat MCP:** jam game berhenti selama 3 s nyata, lalu jalan lagi setelah Lanjutkan.
- Console tanpa error/warning.
- `git status` sama persis sebelum dan sesudah Play.
- Penuh: `McpTestRunner.Run(".")`, semua tes lama + tes baru lulus (failed = 0).

### QA manual (user)
- Esc, klik tombol, dan geser slider dengan mouse asli. Kursor kembali terkunci setelah Lanjutkan.
- Rasa sensitivitas dan FOV.
- Mode layar dan resolusi di **build Windows**, termasuk penghitung FPS di build.
- Tampilan preset Rendah: render scale 70% + STP masih enak dilihat.

## 8. Kriteria selesai
- Semua tes lulus. Pengukuran menunjukkan **malam di preset Sedang punya light aktif ≤ 16 + matahari/bulan/senter**, dan FPS di titik 3 naik dibanding baseline. Angka nyatanya dicatat, tanpa target angka absolut, karena FPS di editor bergantung mesin.
- Menu Pause/Pengaturan bisa dipakai, pengaturan tersimpan antar sesi Play, dan `git status` bersih setelah Play.
- Catatan verifikasi dan checklist QA manual sudah ditulis.

## 9. Risiko
- **Setter URP 17 tidak publik:** sebagian properti URP asset mungkin tidak punya setter publik (mis. kualitas soft shadow). Task awal mengecek setiap properti. Kalau tidak bisa diubah, opsi itu jatuh ke pengaturan per kamera atau dihapus dari preset, dan dicatat.
- **`QualitySettings.renderPipeline` di editor:** mengganti nilainya saat Play bisa saja ikut tersimpan ke `QualitySettings.asset`. Ini dicegah dengan restore di `OnDestroy`, dan dikunci oleh cek `git status`. Kalau tetap bocor, fallback-nya tiga URP asset tetap (Low/Med/High) yang di-commit, plus `SetQualityLevel` dengan restore level.
- **STP butuh dukungan compute dan motion vector:** kalau tidak tersedia, URP jatuh ke upscaler bilinear/FSR. Tidak fatal.
- **GPU occlusion** bisa saja justru menambah beban di GPU lemah. Diputuskan dari hasil ukur.
- **Kabut minimum membuat siang lebih berkabut** di view distance kecil. Faktor 2,3 dan nilainya bisa di-tuning.
- **Grup chunk berukuran 100 m**, jadi satu blok gedung muncul bersamaan. Diharapkan tertutup kabut. Kalau masih mencolok, diganti ke granularitas per renderer.

## 10. Iterasi berikutnya (sudah disepakati di brainstorming)
- **#5 Motor:**
  - kontrol arcade stabil berbasis CharacterController: tidak bisa terguling, miring hanya visual, nabrak tembok berhenti;
  - **kamera orang ketiga, dengan V untuk ganti ke orang pertama**;
  - model pengendara placeholder dari primitif dengan pose duduk;
  - naik/turun dengan E, action map Vehicle;
  - suara mesin dengan pitch mengikuti kecepatan, lampu depan, klakson;
  - speedometer di HUD;
  - volume mesin mengikuti level Efek;
  - kamera motor memakai view distance/FOV dari Settings.
- **#6 Minimap + HUD lengkap:**
  - minimap **bulat yang berputar ikut arah hadap**, dari tekstur peta yang di-bake di editor (data OSM → raster);
  - **M membuka peta penuh**, utara di atas;
  - penanda kos, motor, dan gedung kunci;
  - jam selalu tampil, nama jalan/lokasi;
  - toggle minimap di tab Tampilan.

## 11. Catatan implementasi

- **GPU occlusion default mati di semua preset** (aturan §4). Hasil ukur di preset Sedang: atas 30 m mati 268.8 fps vs nyala 231.1 fps (mati 16 % lebih cepat); di jalan (setinggi mata) netral (mati 235.5 vs nyala 237.8 fps). Opsi ini tetap bisa dinyalakan di tab Grafis.
