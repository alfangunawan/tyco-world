# Verifikasi: kendaraan 3A, aset dan katalog

- **Tanggal:** 2026-10-15
- **Branch:** `feature/vehicles`
- **Spec:** `docs/superpowers/specs/2026-10-15-vehicles-3a-assets-design.md`
- **Plan:** `docs/superpowers/plans/2026-10-15-vehicles-3a-assets.md`

## Aset

User mengunduh 15 GLB. Semua disalin ke folder scratch baru dan dibaca hanya dengan `python -I`
(`scratchpad/tools/glb_inspect.py`): tidak ada URI eksternal, tidak ada yang dieksekusi. Lisensi dicek lewat API publik
Sketchfab dan metadata `asset.extras` di GLB.

| Id | Model | Pembuat | Lisensi | Segitiga | Material |
|---|---|---|---|---|---|
| `vario` | Vario PSX Style | Han66st | CC Attribution 4.0 | 1.354 | 1 |
| `supra` | Honda Supra x 125 | iwak.rebus (tampil sebagai boiledfish.vvti; metadata GLB: Kucing Garage) | CC Attribution 4.0 | 12.561 | 12 |
| `sedan` | 2005 Sedan PSX Style | Han66st | CC Attribution 4.0 | 724 | 1 |
| `hatch` | 2020 Small City Hatchback PSX Style | Han66st | CC Attribution 4.0 | 826 | 1 |
| `lmpv1` | Indonesian LMPV 1 | Han66st | CC Attribution 4.0 | 2.860 | 1 |
| `lmpv2` | Indonesian LMPV 2 | Han66st | CC Attribution 4.0 | 2.552 | 13 |
| `mpv2` | MPV 2 | Han66st | CC Attribution 4.0 | 2.122 | 2 |
| `van` | Blind Van PSX Style | Han66st | CC Attribution 4.0 | 753 | 1 |
| `l300` | L300 Pickup | Han66st | CC Attribution 4.0 | 2.886 | 9 |
| `pickup` | Pickup Truck | Han66st | CC Attribution 4.0 | 1.193 | 3 |
| `police` | Indonesian Police Car (Free) | Han66st | CC Attribution 4.0 | 7.397 (dua mobil) | 1 |
| `angkot` | Angkot lowpoly | ArtDha (ardaradestareza) | CC Attribution 4.0 | 5.324 | 7 |
| `carry` | Suzuki Carry kei truck low poly | bean (alwayshasbean) | CC Attribution 4.0 | 2.242 | 1 |
| `elf` | Isuzu Elf truck XYZ | ann_fenek | CC Attribution 4.0 | 10.372 | 11 |

- **Tidak dipakai:** `2006_isuzu_elf.glb` (ActsOfDoge). Lisensinya "Free Standard", bukan CC-BY, dan 69 ribu segitiga
  (anggaran 13 ribu). Truk diwakili `elf` (ann_fenek).
- **Anggaran segitiga** (≤ 13.000) terpenuhi semua model.
- **Anggaran material** (≤ 3) dilampaui lima file: angkot, elf, l300, lmpv2, supra. Semua materialnya warna datar dengan
  shader yang sama. Dibiarkan, dan diputuskan lewat pengukuran perf di bawah: batch tetap dalam batas.
- **Importer:** glTFast 6.15.0. Material diimpor ke shader graph `glTF-pbrMetallicRoughness` (properti warna
  `baseColorFactor`, bukan `_BaseColor`).

## Impor dan prefab

| Hal | Hasil |
|---|---|
| Arah depan | Dari render tiap model dari +Z dan −Z. Semua mobil, Vario, dan angkot menghadap +Z. Supra menghadap −Z (`YawFix` 180). Elf dimodelkan sepanjang −X dengan skala 1:150 (`YawFix` 90) |
| Rotasi akar | glTFast menaruh koreksi Y-up Sketchfab (270° di X) di akar GLB. Builder menumpuk yaw di atasnya, tidak menimpanya |
| Polisi | Satu mesh berisi SUV dan sedan. Builder memotong segitiga ke kotak `KeepRegion` (x 0,5–3,0) dan menyimpan mesh ringkas `police_Object_1_Kept` |
| Mesh garis | `police` Object_2 dan `l300` Object_10 bertopologi garis. Renderernya dibuang |
| Roda berputar | vario, supra, sedan, hatch, lmpv2, mpv2, van, pickup, angkot |
| Roda menyatu (tidak berputar) | lmpv1, l300, police, carry, elf. Dicatat di `SOURCES.md` |
| Belok | Mobil: tiap roda depan punya pivot belok. Supra: setang + garpu + roda depan. Vario: hanya roda depan (setang menyatu dengan fairing). Angkot: roda per as, berputar tapi tidak berbelok |
| Lampu | Kotak lampu diletakkan di kulit bodi (sinar dari depan/belakang ke segitiga model), sepasang setinggi sama |
| Ukuran | Bounds dari verteks. `Renderer.bounds` longgar untuk node yang diputar miring; Vario sempat 1,75 m (seharusnya 1,92 m) |

Foto: `scratchpad/vehicles-3a/models_side.png`, `models_front2.png`, `models_back2.png`. Ke-14 model tegak, menghadap +Z,
roda di tanah, polisi tinggal sedan.

## Pose pengendara

Motor dipasang di lintasan uji (x 7000, y 1000). Pengendara memakai badan pemain dari `RiderBodySystem` (Vario) atau rig
dengan logika yang sama (Supra). Pembonceng memakai `CharacterRig.Ride(anchors, false)` dengan pinggul diletakkan di jok
belakang seperti `PassengerRide`. Jarak diukur 1 frame setelah IK.

| Motor | Tangan kiri/kanan | Kaki kiri/kanan | Pinggul (vertikal) | Pembonceng kaki kiri/kanan | Pembonceng pinggul |
|---|---|---|---|---|---|
| Vario | 0,000 / 0,000 m | 0,000 / 0,000 m | 0,000 m | 0,000 / 0,000 m | −0,001 m |
| Supra | 0,001 / 0,001 m | 0,000 / 0,000 m | 0,002 m | 0,000 / 0,000 m | 0,000 m |

Syarat spec §7 (≤ 0,06 m; pinggul ≤ 0,08 m) terpenuhi. Foto samping/depan/atas: `pose_vario_*.png`, `pose_supra_*.png`.
Pengendara duduk di jok dengan tangan di setang dan kaki di dek (Vario) atau pijakan (Supra). Pembonceng di jok belakang,
kakinya di pijakan; tangannya bebas karena tidak ada anchor tangan pembonceng.

## Handling

Lantai uji 240 × 2000 m. Gas penuh sampai kecepatan stabil, belok penuh 3 s, rem 8 s, lalu turun.

| Model | Kelas | Maks (km/h) | Preset | Belok 3 s | Berhenti | Turun: jarak kaki ke hull |
|---|---|---|---|---|---|---|
| vario | Motor matik | 60,0 | 60 | 180° | ya | 0,79 m |
| supra | Motor bebek | 65,0 | 65 | 174° | ya | 0,76 m |
| sedan | Mobil kecil | 50,0 | 50 | 90° | ya | 0,60 m |
| lmpv1 | MPV | 45,0 | 45 | 84° | ya | 0,60 m |
| van | Van | 40,0 | 40 | 78° | ya | 0,60 m |
| angkot | Angkot | 40,0 | 40 | 78° | ya | 0,60 m |
| l300 | Pikap | 45,0 | 45 | 78° | ya | 0,60 m |
| elf | Truk | 35,0 | 35 | 60° | ya | 0,60 m |
| police | Polisi | 55,0 | 55 | 96° | ya | 0,60 m |

Tidak ada benturan (`bumps=0`) dan tidak ada yang tersangkut.

## Kendaraan parkir warga

- Di sekitar kos: 7 motor (Vario dan Supra berselang-seling) dan 3 mobil (dua angkot, satu L300).
- Kendaraan lebih dari 40 m tidak membuat bayangan (`CastsShadow=false`), yang dekat membuat bayangan.
- Warna: Supra warna 4 tampil hijau di atas teksturnya (kekuatan 0,6). Angkot tetap livery biru.
  Foto: `parked_0_angkot.png`, `parked_3_supra.png`.
- **Mencuri L300:** prompt "[E] Bobol kunci pikap". Setelah 4 s pemain langsung masuk. Warga melihat, heat naik ke 2,
  dua pengejar datang, pemain tertangkap: denda, diantar pulang, kendaraan kembali. Alur #6 utuh.
- **Cat ulang:** L300 yang sudah diambil dibawa ke Bengkel Jaya Motor, "Cat ulang" dibeli. Warna 2 → 3,
  `baseColorFactor` 0,88 → 0,28 (abu tua), pesan "Beres. Mobilnya dicat ulang." Foto: `repainted_l300.png`.

## Kredit

Menu jeda → Kredit menampilkan isi `Assets/GameSettings/Resources/Credits.txt` dalam panel yang bisa digulir, dengan
tombol Kembali (`credits.png`). Isinya semua tabel `SOURCES.md` di Assets, termasuk kendaraan CC-BY, karakter CC0,
galon CC-BY 3.0, suara Kenney, dan tekstur ambientCG. Suara buatan proyek dan kandidat yang ditolak tidak ikut.

## Performa

Rata-rata 5 s setelah 1 s pemanasan, jam 10:00. `none` = objek gameplay mati, `base` = gameplay aktif. Tidak ada proses
berat lain (hanya shader compiler yang menganggur).

| Titik | Batch sebelum (sub-proyek 2) | Batch sesudah | FPS sesudah |
|---|---|---|---|
| kos, base | 7742 / 8273 | 8076 / 8197 | 107,3 / 111,5 |
| jalan, base | 5967 | 5887 | 124,0 |
| kos, none | 7630 / 7630 | 7768 / 7768 | 121,9 / 126,5 |
| jalan, none | 5525 | 5530 | 134,1 |

- **Kriteria batch base ≤ +3% terpenuhi:** kos rata-rata 8137 lawan 8008 (+1,6%), jalan −1,3%. Lima model dengan
  material lebih dari 3 tidak membuat batch melewati batas.
- Kenaikan kecil di `kos, none` berasal dari motor kos yang kini model Vario (beberapa renderer) di scene World.
- FPS editor berisik dan tidak punya kriteria di spec 3A.

## Bug yang ditemukan dan diperbaiki

| Temuan | Perbaikan | Tes |
|---|---|---|
| Kode plan menimpa rotasi akar GLB, model akan rebah | Yaw ditumpuk di atas rotasi akar | asersi tegak di `EveryModel_IsScaledGroundedAndFacingForward` |
| Semua prefab memakai mesh lampu yang sama (`SaveMeshes` per nama) | Nama mesh lampu memuat id | `EachPrefab_KeepsItsOwnLampMeshes` |
| Kotak lampu melayang di depan bumper bulat; lampu Vario tidak menyentuh bodi | Lampu diletakkan di kulit bodi | `LampQuads_SitOnTheBody_NotInFrontOfIt` (merah 0,5 m → hijau) |
| Vario 10% terlalu pendek, melayang 2 cm, tidak di tengah | Bounds dari verteks | `EveryModel_IsScaled…` (merah 1,749 m → hijau) |
| `RiderBodySystem` dan `PassengerRide` menganggap "punya Hull" = mobil, padahal motor model punya Hull: badan pemain tidak tampil di Vario kos dan penumpang ojek "masuk mobil" | `VehicleFactory.IsCar` = Hull dan tanpa `RideAnchors` | `IsCar_TellsCarsFromBikes_ModelAndCodeBuilt` (merah Vario → hijau) |
| Warna cat ulang tidak sampai ke material glTFast | `VehicleTint` juga mengisi `baseColorFactor` | `GltfMaterial_IsTintedThroughBaseColorFactor` |

## Tes

- Suite penuh: 1202/0 sebelum Play (setelah Task 8), 1203/0 setelah Play (dengan tes `IsCar`).
- Save user dicadangkan (`user-save-backup13.json`, len 267). Saat Play `SaveBackend = MemorySaveBackend`. Setelah keluar
  Play save sama persis dengan cadangan. `runInBackground` hanya nyala selama Play.

## Sisa

- **Badan pemain tetap anak motor setelah turun.** `RiderBodySystem` menyembunyikan badan tapi tidak memindahkannya
  kembali ke `ObjectsRoot`. Kalau motor itu kemudian dihancurkan (bukan dinonaktifkan), badan ikut hancur. Di game
  kendaraan hanya dinonaktifkan, jadi tidak terjadi; muncul saat uji pose. Perilaku lama #6.
- Tangan pembonceng bebas (tidak ada anchor tangan pembonceng).
- Setang Vario tidak berbelok (menyatu dengan fairing); roda depan angkot berputar tapi tidak berbelok.
- `elf.prefab` menulis urutan objek berbeda tiap build (dua node bernama `pasted__pCube3`), tanpa perubahan isi.
- Kredit suara player menulis paket Kenney dua kali (sekali dari tabel paket, sekali dari tabel file).

## Review akhir

Reviewer membaca seluruh cabang (384340d..5af8387). Tidak ada temuan kritis. Tiga temuan penting diperbaiki, masing-masing
dengan tes yang merah lebih dulu (suite 1206/0):

| Temuan | Perbaikan | Tes |
|---|---|---|
| Saat misi galon, `RideAnchors.Of` menambah anchor ke mobil curian, sehingga aturan "Hull dan tanpa RideAnchors" membuat mobil itu dianggap motor selama sesi | Mobil ditandai `BikeTuning.isCar` (diisi preset kelas dan `CarTuning`) | `IsCar_Holds_AfterRideAnchorsAreAskedForACar`, `Apply_MatchesTheSpecTable` |
| Mobil masih bisa kamera orang pertama (spec §5) | `RideCamera.Toggle` menahan mobil di orang ketiga | `Car_StaysInThirdPerson` |
| Kredit belum menyebut perubahan materi dan tautan lisensi (CC BY 4.0) | Kredit memuat catatan "Perubahan" tiap `SOURCES.md` dan daftar tautan lisensi | `Credits_SayWhatWasChanged_AndLinkTheLicences` |

Temuan minor yang ditunda (untuk 3B):

- Vario hilang: motor kos jadi campuran.
- Sumbu belok motor tegak di as roda depan.
- Bayangan badan pemain ikut dimatikan.
- Kunci cache tint memakai hash warna.
- Pickup ditint penuh di atas atlas.
- Node roda hilang menggagalkan build.
- Turun mobil di kiri dulu.
- Motor warga cadangan tidak dicat.
- Parser tabel kredit rapuh.
- Teks "Mobilnya" untuk angkot dan pikap.
