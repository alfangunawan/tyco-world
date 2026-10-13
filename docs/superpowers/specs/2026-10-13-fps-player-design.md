# First-Person Player (jalan kaki) — Design Spec

- **Tanggal:** 2026-10-13
- **Status:** Menunggu review
- **Proyek:** Tyco World (Unity 6000.6.5f1, URP 17.6, Forward+)
- **Sub-proyek:** #2. Dibangun di atas World Generator (#1, spec `2026-10-13-osm-world-generator-design.md`).

## 1. Latar dan tujuan

World Generator sudah menghasilkan dunia OSM di sekitar Telkom University, tapi dunia itu belum bisa dijelajahi:
- belum ada player, AudioListener, audio, maupun HUD;
- jalan, area, dan air belum punya collider, jadi yang bisa diinjak hanya kotak `Ground` di y=0;
- pinggir dunia belum dipagari.

Visi game: pagi hari pemain menjalani hidup sebagai mahasiswa, malam hari game berubah horor. Pemain bisa naik motor, dan kosnya ada di **Kos Raflesia 3**, Sukabirus.

Sub-proyek ini membuat fondasinya, yaitu **player first-person yang berjalan kaki**, lengkap dengan bunyi langkah, interaksi, HUD, dan titik spawn di kos.

Tiga hal dikerjakan di sub-proyek berikutnya, dan desain ini tidak boleh menghalangi ketiganya:
- siklus pagi/malam;
- naik motor;
- sistem cerita/quest (termasuk cutscene yang mengendalikan pemain).

### Keputusan yang sudah disepakati

| Topik | Keputusan |
|---|---|
| Cakupan | Player jalan kaki dulu. Pagi/malam, motor, dan cerita jadi sub-proyek terpisah. |
| Input | Keyboard + mouse. Binding gamepad bawaan tetap ada di asset, tapi tidak dites atau di-tuning. |
| Gerak | Jalan, lari dengan stamina terbatas, lompat, jongkok **toggle** (C), head-bob halus. |
| Senter | Tombol F nyala/mati dengan bunyi klik, **tanpa baterai**. |
| Suara | Langkah kaki sesuai permukaan + bunyi interaksi. |
| Interaksi (E) | Di depan kos: pagar besi buka/tutup, motor parkir sementara ("belum bisa"), pintu kos terkunci. |
| Lokasi kos | Kos Raflesia 3 = -6.978132, 107.631113 (dari user, Google Maps). |
| Audio | Kenney CC0 (Impact Sounds + RPG Audio), dengan `SOURCES.md`. |

## 2. Ruang lingkup

### Termasuk
- Assembly baru `TycoWorld.Player`, `TycoWorld.Player.Editor`, dan `TycoWorld.Player.Tests`.
- Kontrol berbasis intent, dengan kunci kemampuan per pemilik (siap dipakai motor dan cutscene).
- Gerak first-person memakai `CharacterController`: jalan, lari + stamina, lompat, jongkok toggle dengan cek ruang kepala, head-bob, dan mouse look.
- Bunyi langkah per permukaan, berdasarkan material yang **digambar** generator.
- Perubahan WorldGen:
  - collider untuk jalan, area, dan air;
  - komponen `GroundSurface`;
  - dinding batas dunia;
  - gedung kunci Kos Raflesia 3.
- Sistem interaksi umum dengan tiga objek: pagar, pintu terkunci, dan motor sementara.
- Senter, HUD (UI Toolkit), spawn di kos, dan respawn saat jatuh.
- Menu editor "Player Setup" yang menaruh player dan halaman depan kos di `World.unity`.
- Audio Kenney CC0 dan aturan import-nya.

### Tidak termasuk (non-goals)
- Siklus pagi/malam, NPC dan kehidupan kampus, horor malam (musuh, event).
- Naik motor. Yang dibuat sekarang hanya placeholder motor dan kunci kemampuan yang nanti dipakai motor.
- Interior bangunan. Pintu kos terkunci karena interiornya belum ada.
- Tag `surface=*` untuk bunyi langkah. Tag ini baru dipakai kalau material visual juga ikut tag tersebut.
- Gamepad, menu pause/pengaturan, save/load, ambience, dan AudioMixer.
- Bunyi cipratan air (paket Kenney tidak punya).

## 3. Lokasi Kos Raflesia 3

- Koordinat user (-6.978132, 107.631113) dalam proyeksi lokal (origin -6.97265, 107.63400) jatuh di sekitar (-319,0; -606,3). Titik itu berada **di dalam** bangunan OSM way **409467778**: `building=yes`, tanpa nama.
- Setelah dirapikan classifier, bangunan ini berbentuk persegi panjang x[-329,6; -318,4] × z[-611,5; -599,3], ukuran 10,97 × 12,09 m.
- Tetangganya:

| Arah | Objek | Jarak |
|---|---|---|
| Utara | Gallery Futsal (309542536) | 0,6 m dari dinding utara |
| Selatan | Ujung jalan residential 689899501, di (-321,3; -621,1) | ~9,7 m dari dinding selatan; tepi jalan (half-width 2,5) ~7,2 m |
| Selatan, di seberang jalan | Bangunan 409467734 | mulai z ≤ -622,1 |
| Barat | Living street 1112316813 | ~15 m |

- Prediksi planner (§9): halaman depan di **sisi selatan**, menghadap yaw 180°, dengan kedalaman 6,0 m.
- Titik dari user berada 0,5 m di dalam dinding **timur**, jadi bisa saja pintu aslinya menghadap timur. Arah gerbang dikonfirmasi ke user lewat screenshot (§14). Halaman depan adalah konten manual, jadi bisa digeser.

## 4. Arsitektur

### Assembly

| Assembly | Folder | Platform | Referensi |
|---|---|---|---|
| `TycoWorld.Player` | `Assets/Player/Runtime` | semua | `TycoWorld.WorldGen`, `Unity.InputSystem` |
| `TycoWorld.Player.Editor` (namespace `TycoWorld.Player.Authoring`) | `Assets/Player/Editor` | Editor | Player, WorldGen, WorldGen.Editor, InputSystem, URP Core/Universal Runtime |
| `TycoWorld.Player.Tests` | `Assets/Player/Tests` | Editor | Player, Player.Editor, WorldGen, WorldGen.Editor, InputSystem, TestRunner ×2, URP Core/Universal; `overrideReferences` nunit; `UNITY_INCLUDE_TESTS` |

- Dependensi selalu Player → WorldGen, tidak pernah sebaliknya. Komponen runtime yang dibuat generator (`SurfaceType`, `GroundSurface`) tinggal di `TycoWorld.WorldGen`.
- `McpTestRunner` (di WorldGen.Tests) diubah supaya memakai `assemblyNames = { "TycoWorld.WorldGen.Tests", "TycoWorld.Player.Tests" }`. Group `"."` menjalankan semua tes.

### Struktur folder

```
Assets/Player/
  Runtime/   Input/ Tuning/ Motion/ Core/ Audio/ Interaction/ Equipment/ UI/
  Editor/    PlayerPaths, KosSitePlanner, PlayerAssetsBuilder, PlaceholderPropFactory,
             PlayerRigBuilder, PlayerSceneSetup, AudioImportRules
  Tests/
  Audio/     Footsteps/*.ogg  Interactions/*.ogg  SOURCES.md
  Config/    PlayerTuning.asset  FootstepBank.asset
  UI/        PlayerHudPanel.asset  PlayerHudTheme.tss
  Materials/ Prefabs/ (Player, Gate, KosDoor, Motor_Placeholder, FencePanel, KosFrontyard, Meshes/)
Assets/WorldGen/Runtime/  + SurfaceType.cs  GroundSurface.cs
Assets/WorldGen/Editor/   + Classify/SurfaceRules.cs
```

### Kontrol berbasis intent

```csharp
public struct PlayerIntent { public Vector2 Move, LookDegrees; public bool Sprint, Jump, CrouchToggle, Interact, Flashlight; }
public interface IPlayerIntentSource { PlayerIntent Read(float dt); }
[Flags] public enum PlayerAbility { None = 0, Move = 1, Look = 2, Interact = 4, Flashlight = 8, All = 15 }
public sealed class ControlLocks { void Acquire(object owner, PlayerAbility blocked); bool Release(object owner);
                                   bool Allows(PlayerAbility a); event Action Changed; }
```

- **`PlayerInputReader`** adalah sumber intent default. Ia membaca `InputSystem.actions`, map "Player", lalu mengubah delta mouse jadi derajat memakai sensitivitas. Saat kursor tidak terkunci, nilai look yang dikirim nol.
- **`ScriptedIntentSource`** dipakai untuk tes, verifikasi MCP, dan cutscene nanti. Ia punya `Hold(intent, seconds)` dan `Press(edges)`.
- **Kunci kemampuan dipegang per pemilik.** Cutscene dan motor tidak bisa saling melepas kunci yang bukan miliknya. Selama Move terkunci oleh siapa pun, `CharacterController` dimatikan.

### Orchestrator

`FirstPersonPlayer` adalah satu-satunya komponen yang punya `Update`. Ia memanggil `Tick(Time.deltaTime)`, yang menjalankan urutan tetap:

1. baca intent
2. look
3. crouch
4. move (`CharacterController.Move`)
5. stamina
6. step cycle
7. head-bob dan langkah kaki
8. interactor
9. senter
10. HUD
11. cek jatuh

Sub-komponen hanya mengekspos `Tick`/`Initialize`. Dengan begitu tidak ada masalah urutan eksekusi script, dan tes cukup lewat satu pintu.

```csharp
public sealed class FirstPersonPlayer : MonoBehaviour {
  ControlLocks Locks; StaminaModel Stamina; GroundInfo Ground; bool IsGrounded, IsCrouched;
  void Initialize(); void SetIntentSource(IPlayerIntentSource s /* null → reader */);
  void Tick(float dt); void Teleport(Vector3 feet, float yawDeg); }
```

`Teleport` mematikan CharacterController, mengatur pose, memanggil `Physics.SyncTransforms()`, lalu menyalakan CharacterController lagi. Ini wajib karena proyek memakai `autoSyncTransforms: 0`.

### Logika murni (dites di EditMode)

| Unit | Tanggung jawab |
|---|---|
| `MovementModel` | Kecepatan target (jalan/lari/jongkok), percepatan dan perlambatan, kontrol di udara, lompat (dengan coyote time), gravitasi, ground-stick |
| `LookModel` | Yaw bebas, pitch dibatasi ±85° |
| `CrouchModel` | Toggle, cek ruang kepala, transisi tinggi badan dan mata |
| `StaminaModel` | Terkuras saat lari sambil bergerak, pulih setelah jeda, terkunci saat habis, biaya lompat |
| `StepCycle` | Langkah dihitung dari jarak horizontal (stride per mode), event langkah dan pendaratan, fase untuk head-bob |
| `HeadBob` | Offset kamera dari fase |
| `NoRepeatPicker(seed)` | Pilih klip acak tanpa mengulang klip yang sama (xorshift sendiri; `SplitMix64` hanya ada di editor) |
| `GateSwing` | State machine pagar: Closed → Opening → Open → Closing, dengan event Unlatch/Creak/Latch |
| `HudModel` | Prompt, pesan berwaktu, isi dan visibilitas bar stamina; mengimplementasikan `IPlayerFeedback` |
| `SpawnNudge` | Kandidat posisi pengganti dalam cincin 0,5 m sampai radius 2 m |

### Tuning (`PlayerTuning`, ScriptableObject)

Nilai default ditulis sebagai initializer field di grup `[Serializable]`, dan dikunci oleh tes `Defaults_MatchSpec`.

| Grup | Nilai |
|---|---|
| CharacterController | tinggi 1,8; radius 0,35; step 0,3; skin 0,05; minMoveDistance 0 |
| Movement | jalan 3,0; lari 5,5; jongkok 1,5 m/s; akselerasi 12; perlambatan 16; kontrol di udara 0,3; lompat 0,8 m; ground-stick -2 m/s; coyote 0,1 s |
| Stamina | maks 100; terkuras 18/s; pulih 14/s setelah 1 s; terkunci sampai 30%; lompat memakan 8 |
| Look | 0,1°/px; pitch ±85°; FOV 70 |
| Crouch | tinggi 1,0; tinggi mata 1,6 (berdiri) / 0,85 (jongkok) |
| Bob | stride 0,75 (jalan) / 1,0 (lari) / 0,55 (jongkok) m; amplitudo vertikal 0,045 / 0,07 / 0,025; lateral ×0,5 |
| Interaction | jangkauan 2,2 m; sphere fallback r 0,12 |
| Flashlight | spot outer 55° / inner 30°; jangkauan 22 m; soft shadow; lag 14/s; mati saat mulai |
| HUD | pesan tampil 2,5 s; bar stamina hilang 0,8 s setelah penuh |

## 5. Permukaan langkah dan collider (perubahan WorldGen)

- **Patokan bunyi adalah material yang digambar.** Fungsi editor murni `SurfaceRules` memetakan nama material ke `SurfaceType`:

| Material | SurfaceType |
|---|---|
| `Road_Asphalt` | Asphalt |
| `Path_Paving`, `Area_Campus` | Paving |
| `Area_Parking`, `Area_Hardground` | Concrete |
| `Area_Grass`, `Area_Farmland`, `Area_Pitch` | Grass |
| `Ground_Dirt`, `Area_Cemetery` | Dirt |
| `Water` | Water |
| lainnya | Default |

  Ada juga fallback per highway dan per `AreaCategory`, ditambah tes konsistensi terhadap tabel material di `ArtAssetBuilder`. Tag `surface=*` sengaja tidak dipakai: data asli punya 20 jalan `unpaved` yang tetap digambar aspal, jadi bunyinya akan tidak cocok dengan tampilannya.
- `BuiltFeature.Surface` diisi di `WorldMeshPipeline`. Lalu `SceneObjectFactory` menempelkan `GroundSurface` ke setiap fitur jalan, area, dan air, serta ke `Ground` (Dirt).
- **`AddCollider = true` untuk Road, Area, dan Water**: sekitar 820 MeshCollider statis tambahan, di luar 4.921 milik bangunan.
  - Kaki pemain menapak permukaan yang terlihat. Tingginya ≤0,10 m, masih di bawah step 0,3.
  - Raycast ke bawah mengenai permukaan paling atas, karena urutan y memang urutan gambar (jalan 0,06–0,10, air 0,045–0,05, area 0,015–0,035).
- **`GroundBuilder.EdgeWalls(Rect bounds, height, thickness)`** menghasilkan 4 kotak yang sisi dalamnya tepat di batas dunia. Kotak ini dipasang di grup `WorldEdge` sebagai BoxCollider tanpa renderer.
- **Hash chunk tidak berubah.** `ComputeChunkHashes` hanya mencampur isi mesh dan props, sedangkan collider dan komponen tidak ikut di-hash. Hash direkam sebelum perubahan, lalu dibandingkan setelah generate ulang.
- **Air**: punya collider dan `SurfaceType.Water`. Bunyinya memakai klip tanah dengan pitch lebih rendah.

### `GroundProbe` (runtime)

```csharp
public struct GroundInfo { public SurfaceType Surface; public OsmFeature Feature; public Collider Collider; public Vector3 Point; }
public static class GroundProbe { bool Probe(Vector3 from, float distance, out GroundInfo info); void ClearCache(); }
```

- Raycast ke bawah dengan `QueryTriggerInteraction.Ignore`, tanpa mengenai layer pemain. Hasilnya diambil lewat `GetComponentInParent<GroundSurface/OsmFeature>`.
- Cache memakai `Dictionary<Collider, …>`, karena API instance-ID berbasis int error di Unity 6.6. Cache di-reset saat `sceneUnloaded` dan saat `SubsystemRegistration`.
- `FirstPersonPlayer.Ground.Feature` nanti bisa dipakai sistem cerita, misalnya untuk tahu "pemain sedang di Jl. Sukabirus".

## 6. Gedung kunci Kos Raflesia 3

- Entri `new KeyBuilding(409467778, "Kos Raflesia 3")` ditambahkan ke `DefaultSettingsFactory` (daftar default jadi 6) **dan** ke `Assets/WorldGen/Config/WorldGenSettings.asset` yang tersimpan.
- Override-nya dibiarkan default (`overrideLevels 0`, roof Auto), jadi mesh dan hash chunk tetap sama.
- Setelah generate ulang, laporan seharusnya berisi `KeyBuilding=6` dan `Building=4915`.

## 7. Audio

- **Sumber**: Kenney CC0 [Impact Sounds](https://kenney.nl/assets/impact-sounds) dan [RPG Audio](https://kenney.nl/assets/rpg-audio).
  - Zip di-download ke folder kosong baru di scratchpad, lalu isinya **didaftar dulu** sebelum dipetakan.
  - Hanya `.ogg` yang dipakai yang disalin ke `Assets/Player/Audio/{Footsteps,Interactions}`.
  - `SOURCES.md` mencatat nama paket, URL, lisensi, dan pemakaian tiap file.
- **Pemetaan awal** (bisa disesuaikan dengan isi zip yang sebenarnya):

| Pemakaian | Klip |
|---|---|
| Asphalt / Concrete / Paving | `footstep_concrete_00x`, pitch beda per permukaan |
| Grass | `footstep_grass_00x` |
| Dirt (+ Water dengan pitch rendah) | footstep RPG atau `footstep_snow_00x` |
| Pagar | creak + metalLatch |
| Pintu terkunci | metalLatch / metalClick (gagang bergetar) |
| Motor | handleCoins (gemerincing kunci) |
| Senter | metalClick |

- **`FootstepBank`** (ScriptableObject) berisi klip, volume, dan rentang pitch per `SurfaceType`. Entri Default dipakai sebagai cadangan.
- **`FootstepAudio`** memutar satu klip per event langkah. Saat mendarat, bunyinya lebih keras.
- **`ISoundPlayer { Play(clip, volume, pitch) }`**: di tes, implementasinya diganti perekam panggilan.
- Langkah kaki 2D (tidak spasial). Bunyi interaksi 3D di posisi objek. Senter 2D.
- **`AudioImportRules`** (`OnPreprocessAudio` untuk `Assets/Player/Audio`): mono, Vorbis, DecompressOnLoad.

## 8. Interaksi

```csharp
public abstract class Interactable : MonoBehaviour {
  public string InteractionId; virtual string GetPrompt(in InteractionContext c);
  virtual bool CanInteract(in InteractionContext c); abstract void Interact(in InteractionContext c);
  public static event Action<Interactable> Interacted; }   // di-reset saat SubsystemRegistration
public readonly struct InteractionContext { FirstPersonPlayer Player; IPlayerFeedback Feedback; Vector3 ViewOrigin; }
public interface IPlayerFeedback { void ShowMessage(string text, float seconds = 2.5f); }
```

- **`Interactor`**: raycast dari kamera sejauh ≤2,2 m, dengan sphere-cast cadangan r 0,12. Query memakai `QueryTriggerInteraction.Ignore`. Hasil kena dicari ke atas dengan `GetComponentInParent<Interactable>`. Ada tembok di antara pemain dan objek berarti tidak ada fokus.

| Objek | Prompt | Aksi |
|---|---|---|
| `GateInteractable` (pagar) | "[E] Buka pagar" / "[E] Tutup pagar" | Mengayun menjauhi pemain. Bunyi gerendel + derit saat dibuka, gerendel saat selesai menutup. Diabaikan selama pagar masih bergerak. |
| `LockedDoorInteractable` (pintu kos) | "[E] Buka pintu" | Pesan "Pintunya terkunci." + bunyi gagang |
| `PlaceholderVehicleInteractable` (motor) | "[E] Naik motor" | Pesan "Belum bisa naik motor." + gemerincing kunci |

- `Interactable.Interacted` beserta `InteractionId` adalah titik kait untuk sistem cerita nanti.
- **Perubahan asset input**:
  - `Interact` saat ini memakai interaction **Hold**, sehingga baru jalan setelah ditahan sekitar 0,4 s. Hold dibuang supaya E cukup ditekan sekali.
  - Action baru `Flashlight` (Button) di-bind ke `<Keyboard>/f`, group `Keyboard&Mouse`. Tombol F belum dipakai binding lain.

## 9. Halaman depan kos (`KosSitePlanner` + `KosFrontyard`)

Spec #1 §4 menetapkan bahwa konten generator boleh dibuang dan dibuat ulang, sedangkan konten manual tinggal di `World.unity`. Halaman depan kos adalah **konten manual**: ia dihitung sekali oleh editor dari cache OSM, ditaruh di `World.unity`, dan bisa digeser user.

**Planner** (editor, murni):
`KosSitePlanner.TryPlan(footprintCcw, roads, neighbours, options, out plan, out reason)`

1. Untuk setiap sisi dengan panjang ≥ `minEdge` (3 m), hitung normal keluar. Untuk poligon CCW nilainya `(dy, -dx)`.
2. Untuk setiap sisi, cari jalan terdekat yang ada di depannya (dot product > 0). Clearance = jarak − half-width jalan.
3. Urutkan sisi menurut clearance. Buang sisi yang kotak halamannya bertabrakan dengan footprint tetangga.
4. `yard = clamp(clearance − fenceRoadGap(1), minYard 2,5, maxYard 6)`.
5. Pagar dan pintu diletakkan di proyeksi titik jalan terdekat ke sisi itu, dijepit ke `[gateWidth/2 + 0,5, L − …]`.
6. Spawn = pintu + n × 1,2, menghadap n.
7. Motor diletakkan di tengah bagian sisi yang tersisa lebih lebar, pada jarak yard × 0,45 dari dinding, sejajar dinding.
8. Tanpa jalan → `false` dengan alasan.

Isi **prefab `KosFrontyard`**:
- lantai halaman (`GroundSurface` Concrete, material `Area_Hardground`);
- pagar berbentuk U setinggi **≥1,7 m** (lompatan 0,8 + step 0,3 ≈ 1,1 m, jadi tidak bisa dilompati);
- pagar besi berengsel, lebar 2 m;
- pintu (+0,03 m dari dinding);
- motor placeholder dari primitif;
- `PlayerSpawnPoint` (id "kos").

Semua bagian memakai BoxCollider dan material placeholder.

## 10. HUD

- **UI Toolkit, tree dibangun dari kode**: `HudView(VisualElement root)` dengan elemen bernama `crosshair`, `prompt`, `message`, dan `stamina`. `PlayerHud` membuat view itu dari `UIDocument.rootVisualElement`.
- **Aset**:
  - `PlayerHudPanel.asset`: PanelSettings, ScaleWithScreenSize 1920×1080.
  - `PlayerHudTheme.tss`: `@import url("unity-theme://default");`.

  TMP Essentials tidak dibutuhkan.
- **Cadangan**: kalau saat verifikasi teks tidak muncul, ganti ke uGUI dengan `Text` + `LegacyRuntime.ttf` (tanpa aset tambahan).
- **Verifikasi**: HUD tidak ikut tertangkap capture kamera, jadi dicek lewat state model dan VisualElement. Opsional, panel bisa dirender ke RenderTexture (`PanelSettings.targetTexture`).

## 11. Rig player dan setup scene

**`Player.prefab`** (dibangun dari kode oleh `PlayerRigBuilder`):

```
Player  (layer Ignore Raycast; CharacterController; FirstPersonPlayer; PlayerSpawner; FootstepAudio;
         AudioSource 2D; UIDocument + PlayerHud)
└─ CameraPivot (y = tinggi mata; pitch)
   └─ Camera  (tag MainCamera; AudioListener; Interactor; near 0,05; far 400; FOV 70; post-processing aktif; clear #0E1218)
      └─ Flashlight (Spot Light, soft shadow; Flashlight + AudioSource 2D untuk klik)
```

**Menu "Tyco World/Player Setup"**:
1. Menjalankan `WorldScenes.CheckSaved` dan `EnsureWorldSceneOpen`.
2. Membangun aset: tuning, bank, panel/tss, material, dan prefab.
3. Mengklasifikasi cache OSM, lalu menjalankan planner.
4. Menaruh instance Player, `KosFrontyard`, dan `PlayerSpawn_Kos` di `World.unity`.
5. **Menonaktifkan `PreviewCam`.**
6. Memberi peringatan kalau `Physics.OverlapBox` mendeteksi tabrakan dengan lampu/pohon hasil generate.
7. Menyimpan `World.unity`.

Menu ini **idempoten**: kalau dijalankan lagi, halaman depan yang sudah digeser user tidak dipindahkan. Untuk menghitung ulang posisinya ada menu **"Re-place Kos Frontyard"**.

`PreviewSceneSetup` mencari `PreviewCam` di semua root, termasuk yang nonaktif, dan tidak pernah memanggil `SetActive`. Jadi PreviewCam yang sudah nonaktif tidak akan menyala lagi, dan kode `PreviewSceneSetup` tidak perlu diubah.

## 12. Alur saat Play

1. **Awake.**
   - `WorldSceneLoader` memuat `World_Generated` secara additive. Objeknya baru muncul di frame berikutnya. Kalau scene itu sudah terbuka di editor, tidak ada yang dilakukan.
   - `FirstPersonPlayer.Initialize` memanggil `Locks.Acquire(spawner, All)` dan CharacterController dimatikan, jadi gravitasi belum berlaku.
   - `PlayerHud` membangun view.
2. **Start.** `PlayerSpawner` masuk state `WaitingForWorld`. Ia mendengarkan `SceneManager.sceneLoaded` dan juga melakukan polling `FindAnyObjectByType<GeneratedRoot>()`.
3. **Dunia siap.** Spawner menjalankan:
   - cari `PlayerSpawnPoint("kos")`;
   - `Physics.SyncTransforms()`;
   - raycast ke lantai;
   - `CheckCapsule`; kalau terhalang, coba posisi dari `SpawnNudge`;
   - `Teleport`, lalu `Locks.Release(spawner)`;
   - kunci kursor dan tampilkan pesan "Kos Raflesia 3".

   Kalau dalam 15 s dunia belum ada, state jadi `WorldMissing`: HUD menampilkan "Dunia belum di-generate", pemain tetap beku, dan satu error dicatat di log.
4. **Setiap frame.** `Tick`. Esc melepas kursor, klik kiri (Attack) mengunci lagi. Kalau y < -20, pemain di-teleport ke spawn disertai pesan.
5. **Titik kait untuk sub-proyek berikutnya.**
   - Motor: `Locks.Acquire(bike, Move | Interact)`, lalu saat turun `Teleport` + `Release`.
   - Cutscene: `Acquire(cutscene, All)` atau `SetIntentSource(scripted)`.
   - Cerita: `Interactable.Interacted`, id `PlayerSpawnPoint`, dan `Ground.Feature`.

## 13. Jebakan proyek

| Jebakan | Penanganan |
|---|---|
| Domain reload mati (`EnterPlayModeOptions: 3`), jadi isi static tersisa antar sesi Play | Reset di `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` untuk `Interactable.Interacted` dan cache `GroundProbe`. Utamakan state di instance. |
| `autoSyncTransforms: 0` | Teleport selalu lewat rutin §4, dikunci tes `Teleport_SurvivesNextMove`. |
| `queriesHitTriggers: 1` | Semua query memakai `QueryTriggerInteraction.Ignore`, dikunci tes trigger. |
| Tes EditMode tidak memanggil Awake/Update | `Initialize()` + `Tick(dt)` eksplisit. Fixture fisika diletakkan di (5000, 1000, 5000), di luar dunia yang sedang terbuka. |
| `CharacterController.Move` di EditMode belum terbukti | Jadi tes pertama task controller (spike). Kalau gagal, tes CharacterController dipindah ke verifikasi Play. |
| Input keyboard tidak masuk saat editor tidak fokus (MCP) | Verifikasi Play memakai `ScriptedIntentSource`. Rasa kontrol yang asli dicek manual oleh user. |
| Batas jumlah light Forward+ dengan 876 lampu jalan | Cek luminansi senter. Kalau gagal, ada task tambahan `LampLightCuller` (lampu hanya aktif dalam radius sekitar 70 m). |
| Ada perubahan App UI di `ProjectSettings.asset` yang belum di-commit | Commit selalu men-stage path secara eksplisit. Tidak ada perubahan ProjectSettings sama sekali (layer pemain memakai Ignore Raycast bawaan). |
| Dua MainCamera / dua AudioListener | Setup menonaktifkan PreviewCam, lalu jumlahnya dicek. |

## 14. Pengetesan

### Tes EditMode (`TycoWorld.Player.Tests` + perubahan di `TycoWorld.WorldGen.Tests`)
- **WorldGen**:
  - `SurfaceRulesTests`: setiap highway, setiap `AreaCategory`, tabel material, dan konsistensi dengan `ArtAssetBuilder`.
  - Surface dan collider di pipeline sintetis.
  - `EdgeWalls`.
  - `DefaultSettingsTests` dengan 6 gedung kunci, plus pengecekan asset tersimpan.
- **Player, logika murni**: tuning, movement, look, stamina, crouch, step cycle, bob, picker, HUD model, gate, dan nudge.
- **Asset input**: Flashlight=F, Interact tanpa Hold, Crouch=C.
- **Fisika (fixture jauh)**:
  - CharacterController terhalang tembok;
  - naik ke ribbon tipis 0,08 m;
  - tidak bisa berdiri di bawah plafon rendah;
  - teleport tetap bertahan setelah `Move` berikutnya;
  - kunci Move membuat pemain tidak bergeser;
  - probe permukaan (Grass/Default/trigger/yang paling atas);
  - fokus interaksi (jangkauan, tembok, collider anak);
  - spawner (lantai, nudge, WorldMissing, respawn).
- **Editor**:
  - `KosSitePlanner` dengan data sintetis;
  - rig: satu kamera, satu listener, mata 1,6, layer benar, semua referensi terisi;
  - prop: pagar ≥1,7 m;
  - audio: file ada, mono, tercatat di `SOURCES.md`.

### Integrasi (data asli, lewat Unity MCP)
- **Setelah generate ulang**:
  - 278 hash chunk identik dengan rekaman sebelumnya;
  - `KeyBuilding=6`, `Building=4915`;
  - jumlah MeshCollider = bangunan + 763 jalan + 36 area + 21 air;
  - 4 WorldEdge;
  - raycast mengenai kos, dan Jl. Sukabirus terbaca Asphalt.
- **Planner pada data asli**: sisi selatan, pagar di x ≈ -321,3 ± 0,5, yard 6,0, spawn ≈ (-321,3; -612,6).
- **Setup**:
  - idempoten;
  - satu MainCamera aktif dan satu AudioListener;
  - "Setup Preview Scene" tidak menyalakan ulang PreviewCam.
- **Screenshot dari titik spawn** dikirim ke user untuk konfirmasi arah gerbang.
- **Play dengan scripted intent**:
  - spawn ≤1 m dari titik rencana, permukaan Concrete;
  - pagar tertutup menghalangi jalan;
  - E membuka pagar dan bunyinya terdengar;
  - berjalan keluar: permukaan berubah ke Asphalt dan `Ground.Feature.OsmId == 689899501`;
  - lari menguras stamina dan HUD stamina muncul;
  - pintu memberi pesan terkunci, motor memberi pesan "belum bisa";
  - jatuh sampai y=-30 → respawn;
  - luminansi naik saat senter dinyalakan;
  - waktu dari Play sampai spawn dicatat;
  - Console tanpa error/warning.

### QA manual oleh user
Rasa mouse dan keyboard asli, kunci kursor, sensitivitas, kenyamanan head-bob, dan keseimbangan volume suara.

## 15. Kriteria selesai
- Semua tes lama (223) dan tes baru lulus.
- Hash chunk tidak berubah, dan generate tetap deterministik.
- Play di `World.unity` membuat pemain muncul di depan Kos Raflesia 3, dan semua fitur di §1 berjalan seperti diverifikasi di §14.
- Console bersih.
- Catatan verifikasi dan checklist QA manual ditulis di `docs/superpowers/notes/`.
- Spec #1 diperbarui: §7, §7.1, §7.3 (jalan/area/air punya collider), §10 (gedung kunci), §15.

## 16. Risiko dan catatan
- **Arah gerbang mungkin salah** (timur vs selatan). Halaman depan bisa digeser manual, atau dihitung ulang lewat "Re-place Kos Frontyard".
- **Waktu cooking ~5,7k MeshCollider saat scene dimuat.** Diukur di verifikasi Play.
- **Isi zip Kenney bisa beda dari ingatan.** Isinya didaftar dulu sebelum dipetakan. Karena pemetaan disimpan sebagai data di `FootstepBank`, mudah diubah.
- **Kalau HUD UI Toolkit bermasalah** (font/theme), ganti ke uGUI legacy `Text`.

## 17. Iterasi berikutnya (di luar spec ini)
1. Siklus pagi/malam: jam dunia, pencahayaan, mode horor.
2. Motor: naik/turun memakai `ControlLocks` dan `Teleport`, fisika motor di jalan yang sekarang sudah punya collider.
3. Sistem cerita/quest: `Interactable.Interacted`, `Ground.Feature`, dan `ScriptedIntentSource` untuk cutscene.
4. Interior Kos Raflesia 3: `LockedDoorInteractable` diganti pintu yang bisa dibuka.
