# Motor yang Bisa Dikendarai — Design Spec

- **Tanggal:** 2026-10-14
- **Status:** Draft, menunggu review user
- **Proyek:** Tyco World (Unity 6000.6.5f1, URP 17.6, Input System 1.20)
- **Sub-proyek:** #5. Dibangun di atas World Generator (#1), First-Person Player (#2), Siklus Pagi/Malam (#3), dan Performa + Settings (#4). Keempatnya sudah ada di `main` (`1a6b072`).

## 1. Latar dan tujuan

Di halaman Kos Raflesia 3 sudah ada motor placeholder (`Motor_Placeholder`): satu mesh statis yang kalau di-E hanya menjawab "Belum bisa naik motor." Sub-proyek ini membuat motor itu **bisa dinaiki dan dikendarai keliling peta**, siang maupun malam, dengan kamera orang ketiga atau orang pertama, suara mesin, lampu depan, klakson, dan speedometer di HUD.

### Keputusan yang sudah disepakati

| Topik | Keputusan |
|---|---|
| Rasa kendali | Arcade stabil: tidak bisa terguling, miring hanya visual, nabrak tembok berhenti |
| Kecepatan maksimum | 60 km/j |
| Kamera | Orang ketiga (default), tombol V ganti ke orang pertama |
| Pengendara | Model placeholder dari primitif dengan pose duduk; tampil di orang ketiga |
| Model motor | Primitif yang dirapikan: roda terpisah dan berputar, setang ikut belok, badan miring, lampu depan & belakang |
| Suara | Rekaman CC0 dulu; kalau tidak ada yang lisensinya jelas CC0, sintesis di editor |
| Turun | E hanya saat ≤ 5 km/j; cari ruang di kiri lalu kanan |
| Integrasi | Kamera pemain yang sudah ada dipakai ulang, jadi Settings (FOV, view distance), culling, jatah lampu, dan AudioListener tetap berlaku tanpa perubahan |
| Branch | `feature/motorbike` dari `main` |

## 2. Ruang lingkup

### Termasuk
- Mode duduk di `FirstPersonPlayer` (`Seat`/`Unseat`/`IsSeated`).
- Action map **Vehicle** baru, plus pembaca input dan sumber input skrip untuk tes.
- Fisika arcade (`BikeModel` + `BikeTuning`) dan orkestrator `Motorbike`.
- Kamera naik (`RideCamera`): orang ketiga dan orang pertama.
- Naik/turun (`MotorbikeInteractable`, `DismountFinder`).
- Visual: roda berputar, setang belok, badan miring, lampu depan, lampu belakang/rem, model pengendara.
- Audio: mesin, starter, klakson, benturan; volume mengikuti level Efek.
- HUD: speedometer, indikator lampu, prompt turun.
- Menu editor **"Tyco World/Vehicle Setup"**: membangun aset dan prefab `Motorbike`, lalu mengganti motor placeholder di kos.

### Tidak termasuk (non-goals)
- Bensin, kerusakan, jatuh/terguling, atau kotak gigi.
- Lalu lintas, NPC, atau kendaraan lain.
- Menyimpan posisi motor antar sesi (belum ada sistem save; tiap Play motor mulai di kos).
- Penumpang, mode foto, atau replay.
- Kontrol gamepad khusus selain binding default Input System.
- Minimap dan penanda motor (sub-proyek #6).

## 3. Arsitektur

### Assembly

| Assembly | Folder | Platform | Referensi |
|---|---|---|---|
| `TycoWorld.Vehicles` | `Assets/Vehicles/Runtime` | semua | `TycoWorld.Player`, `TycoWorld.WorldGen`, `Unity.InputSystem` |
| `TycoWorld.Vehicles.Editor` (namespace `TycoWorld.Vehicles.Authoring`) | `Assets/Vehicles/Editor` | Editor | Vehicles, Player, Player.Editor, WorldGen, WorldGen.Editor, DayCycle.Editor (sintesis audio) |
| `TycoWorld.Vehicles.Tests` | `Assets/Vehicles/Tests` | Editor | semua di atas + TestRunner; `overrideReferences` nunit; `UNITY_INCLUDE_TESTS` |

- **Arah dependensi:** GameSettings → Vehicles → Player → WorldGen. Player tidak tahu soal motor.
- `TycoWorld.GameSettings` mendapat referensi ke `TycoWorld.Vehicles` (untuk level volume mesin).
- `McpTestRunner` ditambah assembly `TycoWorld.Vehicles.Tests`.

### Perubahan di modul lama

| Modul | Perubahan |
|---|---|
| Player | `FirstPersonPlayer.Seat(Transform seat)`, `Unseat(Vector3 feet, float yaw)`, `IsSeated`, event `Unseated`. `Teleport` selalu memanggil `Unseat` lebih dulu |
| Player | `HudModel.SetVehicle(bool visible, float speedKmh, bool headlightOn)` + elemen speedometer di `HudView` |
| Player | `Flashlight` dapat `TurnOff()` (mematikan tanpa bunyi klik; dipakai saat naik). Saat ini hanya ada `Toggle()` dan `IsOn` |
| Input | Action map **"Vehicle"** di `Assets/InputSystem_Actions.inputactions` |
| GameSettings | `SettingsApplier` menyetel `MotorbikeAudio.Level` = level Efek; `EffectsVolume` melewati AudioSource milik `MotorbikeAudio` |
| Scene | `World.unity`: motor placeholder di kos diganti instance `Motorbike` di pose yang sama (lewat Vehicle Setup) |

### Unit di `TycoWorld.Vehicles`

**Logika murni (dites di EditMode):**
- `VehicleIntent` { Throttle (-1..1), Steer (-1..1), Brake, Horn, ToggleHeadlight, ToggleCamera, Exit, LookDegrees } dan `IVehicleIntentSource`.
- `ScriptedVehicleIntentSource`: `Hold(intent, seconds)` dan `Press(intent)`, seperti `ScriptedIntentSource` pemain.
- `BikeModel`: kecepatan, laju belok, sudut miring, dan langkah perpindahan dari `BikeTuning` + intent + dt.
- `RideCameraMath`: pose kamera orang ketiga/pertama dari pose motor, offset orbit, dan aturan kembali ke belakang.
- `EngineSound`: pemetaan kecepatan/gas → pitch dan volume.

**Komponen:**
- `VehicleInputReader` (action map Vehicle). Mouse hanya terbaca saat kursor terkunci, seperti pembaca pemain.
- `Motorbike`: orkestrator dengan `Tick(dt)` (lihat §3 alur).
- `MotorbikeInteractable : Interactable`: prompt "[E] Naik motor".
- `DismountFinder`: mencari titik turun yang aman.
- `RideCamera`: menerapkan `RideCameraMath` ke kamera pemain, termasuk tarik-maju saat terhalang.
- `MotorbikeVisuals`: roda, setang, pivot miring, lampu belakang (salinan material runtime), dan model pengendara.
- `MotorbikeAudio`: sumber mesin + one-shot, dengan `Level`.
- `BikeTuning` (ScriptableObject): semua angka di §4.

### Alur saat Play
1. **Naik:** E di motor → `Motorbike.Mount(player)`:
   - `player.Locks.Acquire(motor, Move | Interact | Flashlight)` — **Look tidak dikunci**, supaya `PauseController.Resume` tetap mengunci kursor lagi;
   - senter dimatikan;
   - `player.Seat(kursi)`;
   - input Vehicle aktif, tetapi Exit diabaikan selama 0,3 s;
   - starter berbunyi, lalu mesin menyala;
   - pengendara tampil (orang ketiga), HUD kendaraan tampil.
2. **Setiap frame** (`Motorbike.Tick(dt)`):
   intent → `BikeModel` → gerak (CharacterController + box cast) → visual → kamera → audio → lampu → HUD.
3. **Turun:** E saat ≤ 5 km/j → `DismountFinder` → `player.Unseat(kaki, arah motor)` → kunci dilepas, mesin mati, pengendara disembunyikan, HUD kendaraan hilang.
4. **Saat duduk, `FirstPersonPlayer.Tick`:**
   - pemain mengikuti pose kursi;
   - melewati look, jongkok, gerak, langkah, head-bob, ground probe, dan cek jatuh;
   - **tidak** menulis transform kamera (milik `RideCamera`);
   - HUD dan modul tetap di-tick.

## 4. Nilai awal (`BikeTuning`, bisa di-tuning)

| Kelompok | Nilai |
|---|---|
| Kecepatan | maju maks 16,67 m/s (60 km/j); mundur maks 2,5 m/s (9 km/j) |
| Akselerasi gas | 4,0 m/s² saat diam, turun linear ke 1,5 m/s² di kecepatan maks (0→40 km/j ± 4 s) |
| Rem | S saat maju 7 m/s²; Space 10 m/s²; tanpa gas melambat 0,8 m/s² |
| Mundur | S saat ≤ 0,3 m/s → akselerasi mundur 2,0 m/s² |
| Belok | laju yaw maks 90°/s di kecepatan rendah, turun linear ke 35°/s di kecepatan maks; dikali `clamp01(|v| / 1,5)` (tidak bisa berputar di tempat) |
| Miring visual | maks 25° × `clamp01(|v| / 8)`, dihaluskan 6/s |
| Tanah | gravitasi 20 m/s²; tanjakan maks 35°; undakan 0,3 m |
| Badan | CharacterController radius 0,5, tinggi 1,4; box cast setengah ukuran (0,35; 0,5; 0,95) di sumbu maju |
| Tabrakan | menghadap tembok (dot > 0,5) → berhenti; bunyi kalau > 2,8 m/s (10 km/j). Menyerong → meluncur di sepanjang tembok, kecepatan × 0,8 |
| Turun | ≤ 1,4 m/s (5 km/j); titik 1,1 m ke kiri lalu ke kanan; cek kapsul pemain (radius 0,35, tinggi 1,8) |
| Kamera orang ketiga | jarak 4,5 m, tinggi 1,9 m, titik tatap 1,2 m di atas kursi; orbit yaw ±120°, pitch -10..35°; kembali ke belakang setelah 1,5 s tanpa mouse saat > 2 m/s; ikut arah motor dengan penghalusan 5/s; sphere cast radius 0,25 untuk tarik-maju |
| Kamera orang pertama | 0,75 m di atas kursi, 0,1 m ke belakang; toleh yaw ±100°, pitch -60..60°; roll = ½ sudut miring |
| Lampu depan | Spot, jangkauan 30 m, sudut 60°, intensitas 4, tanpa bayangan; default mati |
| Mesin | pitch 0,8 (langsam) → 2,0 (maks) + 0,1 saat gas; volume 0,6 → 0,9; audio 3D, min 3 m, maks 40 m |
| Klakson | cooldown 0,4 s |

## 5. Kontrol (action map "Vehicle")

| Action | Binding |
|---|---|
| Throttle (axis) | W / S, panah atas / bawah; gamepad trigger kanan / kiri |
| Steer (axis) | A / D, panah kiri / kanan; stik kiri X |
| Brake | Space; gamepad tombol selatan |
| Horn | H; gamepad stik kiri ditekan |
| Headlight | F; gamepad d-pad atas |
| Camera | V; gamepad tombol utara |
| Exit | E; gamepad tombol barat |
| Look | delta pointer; stik kanan |

Map Vehicle hanya aktif saat duduk di motor. Map Player tetap aktif (E di map Player dikunci lewat `ControlLocks` saat duduk).

## 6. HUD dan teks

- **Speedometer** di kanan bawah: angka besar ("42") + "km/j".
- **Indikator lampu:** "LAMPU" terang saat menyala, redup saat mati.
- **Prompt:** "[E] Turun" hanya saat sudah boleh turun.
- Crosshair dan bar stamina disembunyikan selama naik.
- **Teks persis:**
  - `"[E] Naik motor"`;
  - `"[E] Turun"`;
  - `"Pelankan motor dulu."`;
  - `"Tidak ada ruang untuk turun."`;
  - `"km/j"`;
  - `"LAMPU"`.

## 7. Audio

- **Mesin (loop), starter (one-shot), klakson (one-shot), benturan (one-shot).**
- **Sumber:** cari rekaman CC0, dengan lisensi yang terbukti di halaman sumbernya.
  - Hasilnya didokumentasikan di `Assets/Vehicles/Audio/SOURCES.md`.
  - Kalau tidak ditemukan yang jelas CC0, klip dibuat dengan sintesis di editor, memakai ulang `AmbienceSynth`/`WavWriter` dari DayCycle.Editor kalau cocok.
- **Volume:**
  - `MotorbikeAudio.Level` mengikuti slider Efek (diset `SettingsApplier`).
  - `EffectsVolume` melewati sumber milik `MotorbikeAudio`, karena volume mesin diatur tiap frame oleh motor.
- **Pause:** `AudioListener.pause` (dari #4) ikut menghentikan mesin.

## 8. Penanganan error dan kasus tepi
- **Scene tanpa pemain, tanpa motor, atau tanpa kamera:** tanpa exception. Motor tanpa pemain hanya diam.
- **Pause:** `Time.timeScale = 0` membuat dt = 0, jadi motor diam dan input motor diabaikan.
- **Teleport/respawn saat duduk:** pemain turun otomatis (`Unseated`), lalu motor melepas kunci dan mematikan mesin.
- **Motor jatuh di bawah y = -20:** motor kembali ke pose parkir awalnya. Kalau ada pengendara, pemain diturunkan di samping motor.
- **Kamera orang ketiga terhalang:** sphere cast menarik kamera maju, tidak pernah menembus tembok.
- **Tidak ada ruang turun:** pesan, pemain tetap duduk.
- **Prefab tanpa `BikeTuning`:** memakai nilai default + 1 warning `[Vehicle]`.
- **Asset tidak pernah diubah saat runtime:** lampu belakang memakai salinan material `DontSave`.
- **Domain reload mati:** tidak ada state static yang bisa berubah.

## 9. Pengetesan

### EditMode (`TycoWorld.Vehicles.Tests` + tambahan di Player/GameSettings)
- `BikeModel`:
  - batas 60 km/j dan 9 km/j mundur;
  - 0→40 km/j dalam 3,5–4,5 s;
  - jarak rem;
  - melambat tanpa gas;
  - S saat diam berarti mundur;
  - aturan laju belok (rendah/tinggi/diam);
  - miring.
- `FirstPersonPlayer` mode duduk:
  - mengikuti kursi;
  - tidak bergerak/jatuh;
  - kamera tidak ditulis;
  - `Unseat` mengembalikan kontrol;
  - `Teleport` → turun + event.
- `DismountFinder` (fixture fisika): kiri bebas, kiri terhalang → kanan, dua sisi terhalang → gagal.
- `RideCameraMath`: offset, batas orbit, kembali ke belakang, pose orang pertama.
- `RideCamera` + fixture: tembok di antara motor dan kamera → kamera ditarik maju.
- `Motorbike` di lantai uji (input skrip):
  - kecepatan naik dan tertahan di 60 km/j;
  - tembok di depan → berhenti tanpa menembus;
  - naik → kunci + duduk;
  - turun hanya saat pelan.
- `EngineSound`, `MotorbikeAudio.Level`, dan `EffectsVolume` melewati sumber motor.
- HUD: elemen speedometer, teks, dan tampil/sembunyi.
- Asset input: action map Vehicle dan binding utama.
- Vehicle Setup: placeholder diganti, aman dijalankan ulang, dan tidak menduplikasi motor.

### Integrasi (MCP, Play)
- Naik motor di kos.
- Input skrip: keluar lewat pagar, melaju ±10 s di Jalan Sukabirus, belok, menabrak tembok, berhenti, turun.
- **Dicatat:**
  - kecepatan puncak;
  - jarak tempuh;
  - posisi setelah tabrakan (tidak menembus);
  - FPS sambil melaju (preset Sedang).
- Screenshot:
  - orang ketiga dan orang pertama siang;
  - orang ketiga malam dengan lampu depan.
- Pause saat melaju → motor diam, lalu lanjut.
- Console bersih dan `git status` sama sebelum/sesudah Play.

### QA manual (user)
- Rasa gas, rem, dan belok dengan keyboard asli.
- Kenyamanan kamera orang ketiga dan orang pertama.
- Suara mesin, klakson, dan volume terhadap slider Efek.
- Lewat pagar kos, naik turun undakan jalan, malam dengan lampu depan.

## 10. Kriteria selesai
- Semua tes lulus.
- Motor bisa dinaiki, dikendarai sampai 60 km/j, berhenti di tembok tanpa menembus, dan diturunkan dengan aman.
- Kamera orang ketiga dan orang pertama berfungsi. HUD, suara, dan lampu bekerja.
- `git status` bersih setelah Play, dan catatan verifikasi + checklist QA manual sudah ditulis.

## 11. Risiko
- **CharacterController berbentuk kapsul vertikal**, sedangkan motor panjang 1,9 m. Box cast di sumbu maju menutupi ujung depan/belakang, tetapi sudut motor bisa sedikit masuk tembok saat menyerong. Diterima untuk arcade.
- **Rekaman CC0 yang layak belum tentu ada.** Fallback sintesis sudah disepakati.
- **Kamera orang ketiga di gang sempit** bisa sering tertarik maju. Jarak dan radius bisa di-tuning.
- **Mode duduk menyentuh inti Player.** Dikunci oleh tes regresi Player yang sudah ada (161 tes).
- **Re-place Kos Frontyard** membuat ulang motor placeholder; Vehicle Setup perlu dijalankan lagi. Didokumentasikan di menu dan catatan verifikasi.

## 12. Iterasi berikutnya
- **#6 Minimap + HUD lengkap:**
  - penanda motor di minimap;
  - HUD dirapikan bersama speedometer;
  - pemilik tunggal untuk tampilan jam (F3 dan Settings).
- Kemungkinan nanti: bensin, NPC lalu lintas, menyimpan posisi motor.
