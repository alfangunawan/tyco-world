# Kendaraan 3A: aset dan katalog kendaraan (sub-proyek 3A): spec desain

- **Tanggal:** 2026-10-15
- **Status:** Permintaan user setelah playtest:
  - "pose pengendara tidak cocok dengan model motor, cari aset kendaraan yang bagus";
  - "tambah motor, mobil, truk, oplet dan kendaraan lain dari aset gratis yang cocok".
- **Keputusan user:**
  - Kendaraan dipakai untuk lalu lintas, dan semuanya bisa dicuri.
  - Aset CC-BY yang realistis dan khas Indonesia, dari Sketchfab. User mengunduhnya lewat browser.
  - Set: seri Han66st ditambah model pelengkap (§1).
  - Pekerjaan dibagi tiga tahap. Masing-masing punya spec, plan, dan PR sendiri:
    - **3A, aset dan katalog:** dokumen ini.
    - **3B, lalu lintas:** `2026-10-15-vehicles-3b-traffic-design.md`.
    - **3C, akibat:** `2026-10-15-vehicles-3c-consequences-design.md`.
- **Dasar:**
  - Motor (#5): `Motorbike`, `BikeTuning`, `MotorbikeVisuals`.
  - Kampung life (#6): `VehicleSystem`, `WorldVehicle`, `VehicleFactory`, `RideAnchors`, `CharacterRig`.

## 1. Model

Semua model berlisensi **CC Attribution 4.0** dan diunduh dari Sketchfab dalam format **GLB**.

| # | Id | Model | Pembuat | Sketchfab | Segitiga | Kelas | Panjang asli |
|---|---|---|---|---|---|---|---|
| 1 | `vario` | Vario PSX Style | Han66st | https://sketchfab.com/models/6c67aa3f3c60455ba12780f442c65a81 | 1.354 | Motor matik | 1,92 m |
| 2 | `supra` | Honda Supra x 125 | iwak.rebus | https://sketchfab.com/models/6d61abb3aa3a4161bda31ef783111015 | 12.565 | Motor bebek | 1,89 m |
| 3 | `sedan` | 2005 Sedan PSX Style | Han66st | https://sketchfab.com/models/5d370d67172041dbae7d6f112255fdd9 | 724 | Mobil kecil | 4,45 m |
| 4 | `hatch` | 2020 Small City Hatchback PSX Style | Han66st | https://sketchfab.com/models/1c2fdb3a653e4cd2b6cd0fa1a5c7c1e9 | 826 | Mobil kecil | 3,80 m |
| 5 | `lmpv1` | Indonesian LMPV 1 | Han66st | https://sketchfab.com/models/835fc6d910444b2db3627eeacb747ca1 | 2.860 | MPV | 4,40 m |
| 6 | `lmpv2` | Indonesian LMPV 2 | Han66st | https://sketchfab.com/models/13dd216f01124fb89d9e3da21015c6bf | 2.552 | MPV | 4,10 m |
| 7 | `mpv2` | MPV 2 | Han66st | https://sketchfab.com/models/eff0a2db0bcf46c59c5bfe1c8804083d | 2.122 | MPV | 4,70 m |
| 8 | `van` | Blind Van PSX Style | Han66st | https://sketchfab.com/models/095ecbdd43ac450fa392dcb47c2478cb | 753 | Van | 4,05 m |
| 9 | `l300` | L300 Pickup | Han66st | https://sketchfab.com/models/24e13a998f2b4944aa2d9a680c837981 | 2.886 | Pikap | 4,30 m |
| 10 | `pickup` | Pickup Truck | Han66st | https://sketchfab.com/models/616761063ad845fe84e1d965bdce5595 | 1.193 | Pikap | 5,30 m |
| 11 | `police` | Indonesian Police Car (Free) | Han66st | https://sketchfab.com/models/f548bd218ba94711b16cf1aeeee028ff | 7.398 | Polisi | 4,45 m |
| 12 | `angkot` | Angkot lowpoly | ardaradestareza | https://sketchfab.com/models/7fed82dcb4604b039b1d642636ced814 | 5.326 | Angkot | 3,90 m |
| 13 | `carry` | Suzuki Carry kei truck low poly | alwayshasbean | https://sketchfab.com/models/6bc99e709e9748da98cc9ac676684510 | 2.246 | Pikap | 3,40 m |
| 14 | `elf` | Isuzu Elf truck XYZ | ann_fenek | https://sketchfab.com/models/ee9e569d3fea439cb41166d07aeb7cca | 10.606 | Truk | 6,00 m |

- File GLB polisi berisi dua mobil dalam **satu mesh**. Yang dipakai hanya sedan: builder memotong segitiga ke sebuah kotak (`KeepRegion`, §3) dan menyimpan mesh hasil potongan.
- Panjang asli dipakai untuk menormalkan skala. Model diskala seragam sampai panjang bounds-nya sama dengan angka di tabel, dengan toleransi 2%.

## 2. Unduh dan impor

- **Unduh:** user mengunduh ke-14 GLB lewat browser ke satu folder lalu memberi tahu path-nya.
- **Data dari luar dianggap tidak tepercaya:**
  - Semua file disalin ke folder scratch baru.
  - Chunk JSON-nya dibaca hanya dengan `python -I`. Yang dicek: node, mesh, gambar, ekstensi yang dipakai, jumlah segitiga, dan tidak ada URI eksternal.
  - Tidak ada yang dieksekusi dari unduhan.
- **Penyimpanan:** setelah lolos cek, file disalin ke `Assets/Vehicles/Art/ThirdParty/<Pembuat>/<Id>.glb`. Setiap folder punya `SOURCES.md` yang mencatat judul, pembuat, link, lisensi, dan perubahan (skala, arah hadap, subtree yang dipakai).
- **Importer:** paket resmi Unity **glTFast** (`com.unity.cloud.gltfast` 6.15.0).
  - Hierarki node tetap utuh, jadi roda bisa diputar dan setang bisa dibelokkan.
  - Material diimpor ke shader graph glTFast `glTF-pbrMetallicRoughness` untuk URP (warna di properti `baseColorFactor`). Cat ulang mengisi properti itu.
  - Koreksi Y-up dari Sketchfab ada di rotasi akar GLB; builder menumpuk yaw di atasnya.
  - Kalau tekstur PSX terlihat buram, filternya Bilinear dengan mipmap.
- **Kredit:** CC-BY mewajibkan atribusi.
  - Menu jeda mendapat tombol **Kredit**. Isinya daftar semua aset pihak ketiga: kendaraan CC-BY, karakter CC0, galon CC-BY, dan tekstur ambientCG.
  - Daftar itu dibaca dari satu file `Assets/GameSettings/Resources/Credits.txt` (supaya bisa dimuat `Resources.Load`) yang ditulis builder dari semua tabel `SOURCES.md`, dibaca menurut header tiap tabel. Suara buatan proyek dan kandidat yang ditolak tidak ikut.

## 3. Spec model: `VehicleModelCatalog`

Satu tabel di kode editor, satu baris per model. Isinya diisi setelah GLB diperiksa (nama node sudah diketahui):

- `id`, file, kelas, panjang asli.
- Koreksi yaw supaya bagian depan model menghadap +Z.
- Kotak potong (`KeepRegion`, opsional): kalau satu mesh berisi lebih dari satu kendaraan, hanya segitiga yang pusatnya di dalam kotak yang disimpan.
- Nama node roda: depan dan belakang (boleh lebih dari satu, misalnya ban ganda Elf).
- Node setang atau setir (opsional).
- Material bodi yang boleh dicat. Livery tetap (polisi, angkot) tidak dicat.
- Posisi lampu depan dan belakang.
- **Motor:** `RideAnchors`, yaitu jok, pegangan kiri/kanan, pijakan kiri/kanan, boncengan, dan pijakan boncengan.
- **Mobil:** kursi sopir dan sisi pintu sopir. Indonesia setir kanan, jadi pintu sopir di kanan.
- **Angkot:** titik pintu belakang dan pintu penumpang (dipakai di 3B).

**Kalau nama node roda tidak ada,** misalnya karena roda menyatu dengan bodi, roda tidak berputar. Kondisi ini dicatat di report dan di `SOURCES.md`. Game tetap jalan.

Dua kasus dari model yang diunduh:

- **Setang yang menyatu dengan fairing** (Vario) tidak berbelok; hanya roda depan yang berbelok, dan pegangan tangan ikut fairing. Node lain yang ikut berbelok bersama setang ditulis di `ExtraSteerNodes` (Supra: garpu dan spakbor).
- **Satu mesh per as roda** (angkot): roda berputar tapi tidak berbelok (`SolidFrontAxle`).

## 4. Prefab

`VehicleModelBuilder` (editor) membuat satu prefab per model di `Assets/Vehicles/Prefabs/Models/`. Prefab bisa dibangun ulang (idempotent).

**Struktur prefab:**

```
<Id> (CharacterController + Motorbike + MotorbikeVisuals; kunci curian dipasang gameplay)
├─ Model        (instance GLB: diskala, diputar, berdiri di y = 0, dipusatkan di X/Z)
├─ Seat         (+ RideAnchors untuk motor)
├─ Hull         (BoxCollider dari bounds model; controller mengabaikannya, seperti mobil warga sekarang)
├─ Lights       (lampu depan Spot + quad emissive depan/belakang)
└─ Audio        (mesin, klakson; klip dari VehicleAudioBuilder yang ada)
```

- **`MotorbikeVisuals`** diperluas (`ConfigureModel`) untuk model baru, bukan kelas baru. Isinya:
  - array roda yang diputar sesuai kecepatan;
  - pivot setang/setir yang mengikuti input belok;
  - pivot condong (motor saja);
  - lampu.
- **`MotorbikeVisuals`** tetap ada untuk motor primitif cadangan.
- **Cat:** varian material per warna dibuat sekali, dengan pola `Palette` yang sudah ada (bukan MaterialPropertyBlock). Dengan begitu SRP Batcher tetap bekerja.
  - Motor memakai enam warna dari `VehicleFactory.BikeColours`, mobil dari `CarColours`.
- **Cadangan:** kalau GLB atau prefab satu model tidak ada, `VehicleFactory` memakai kendaraan primitif yang sekarang dan report mencatat `VehicleModelMissing`.

## 5. Kelas dan handling

Semua kendaraan memakai controller arcade yang sama (`Motorbike`). Angka per kelas ada di `VehicleClassPresets`; tiap model punya aset `BikeTuning` sendiri di `Assets/Vehicles/Config/Tuning/<id>.asset` karena ukuran tubuhnya dihitung dari bounds model. Preset-preset ini menggantikan `VehicleFactory.CarTuning`.

| Kelas | Maks | Akselerasi diam → maks | Laju belok rendah/tinggi | Condong | Kamera |
|---|---|---|---|---|---|
| Motor matik | 60 km/h | 4,0 → 1,6 | 150 / 60 | ya | orang 1 & 3 |
| Motor bebek | 65 km/h | 4,2 → 1,6 | 145 / 58 | ya | orang 1 & 3 |
| Mobil kecil | 50 km/h | 3,2 → 1,2 | 75 / 30 | tidak | orang 3 |
| MPV | 45 km/h | 3,0 → 1,1 | 70 / 28 | tidak | orang 3 |
| Van | 40 km/h | 2,7 → 1,0 | 65 / 26 | tidak | orang 3 |
| Angkot | 40 km/h | 2,7 → 1,0 | 65 / 26 | tidak | orang 3 |
| Pikap | 45 km/h | 2,8 → 1,0 | 65 / 26 | tidak | orang 3 |
| Truk | 35 km/h | 1,8 → 0,7 | 50 / 20 | tidak | orang 3, lebih jauh |
| Polisi | 55 km/h | 3,6 → 1,3 | 78 / 32 | tidak | orang 3 |

- **Ukuran tubuh:** kotak benturan, radius, tinggi, dan jarak turun dihitung dari bounds model, bukan diketik manual.
- **Kamera mobil hanya orang ketiga,** karena model PSX tidak punya interior. Badan pemain disembunyikan saat di dalam mobil.

## 6. Pemakaian di game (dalam 3A)

- **Motor kos pemain** diganti Vario.
  - Id interaksi, posisi parkir, save, dan semua misi tetap sama.
  - Prefab primitif `Motorbike` tetap ada sebagai cadangan.
- **Kendaraan parkir warga:** titik bake di `VehicleSystem` tetap (14 motor, 5 mobil).
  - **Motor:** Vario dan Supra berselang-seling menurut indeks titik parkir.
  - **Mobil:** campuran deterministik menurut indeks titik parkir, dari sedan, hatchback, lmpv1, lmpv2, mpv2, van, l300, pickup, carry, dan angkot.
  - Polisi dan Elf tidak diparkir di titik warga; keduanya muncul di lalu lintas (3B).
- **Pencurian, cat ulang, dan heat** tetap memakai alur #6. `VehicleKind` ditambah satu nilai, yaitu kelas kendaraan, supaya teks ("motor", "mobil", "angkot", "truk") dan durasi bobol bisa per kelas.
- **Ojek dan galon:** penumpang dan galon memakai `anchors.pillion` dari model motor yang sedang dipakai.

## 7. Pose pengendara

- `CharacterRig.Ride(anchors, asRider)` dipakai dengan anchor dari tabel per model.
- Sopir mobil yang terlihat (di 3B/3C, saat turun atau naik) memakai klip UAL `Driving_Loop`.
- **Syarat di Play** (Vario dan Supra, sebagai pengendara dan pembonceng):
  - tangan paling jauh 6 cm dari pegangan;
  - kaki paling jauh 6 cm dari pijakan;
  - pinggul di atas jok;
  - tidak ada bagian badan yang menembus bodi motor, dicek dari screenshot depan, samping, dan atas.

## 8. Performa

- **Anggaran per model:** maksimal 13 ribu segitiga (yang paling berat Supra, 12,6 ribu) dan maksimal 3 material. Lima file melampaui batas material (angkot 7, elf 11, l300 9, lmpv2 13, supra 12; semuanya warna datar). Dibiarkan karena pengukuran batch tetap di bawah +3% (lihat catatan verifikasi).
- **Jumlah kendaraan parkir** tetap sama. Syaratnya: batch di titik kos dan jalan jam 10.00 naik paling banyak 3% dibanding sebelum perubahan.
- **Bayangan:** kendaraan parkir lebih dari 40 m tidak membuat bayangan, sama seperti pejalan kaki.

## 9. Pengetesan

**EditMode:**
- **Katalog:**
  - id unik;
  - file ada;
  - panjang lebih dari 0;
  - setiap motor punya anchor lengkap: pegangan di atas jok, pijakan di bawah jok;
  - setiap mobil punya kursi sopir dan pintu di sisi kanan.
- **Prefab, untuk setiap model di katalog:**
  - panjang sesuai tabel (±2%);
  - berdiri di y = 0 (±0,02);
  - depan ke +Z (lampu depan di z > 0);
  - hull menutup bounds;
  - roda ditemukan, atau dicatat tidak ada;
  - jumlah segitiga dan material dalam anggaran.
- **Tuning:** setiap kelas punya preset dengan angka sesuai §5. Kotak benturan diambil dari bounds model.
- **`VehicleFactory`:**
  - campuran kendaraan parkir deterministik menurut indeks;
  - kalau prefab tidak ada, dipakai kendaraan primitif dan counter report naik.
- **Kredit:** setiap baris tabel `SOURCES.md` di Assets muncul di `Credits.txt`, kecuali suara buatan proyek dan kandidat yang ditolak.

**Play** (save user dicadangkan dulu, seperti biasa):
- Screenshot tiap model.
- Pose pengendara dan pembonceng diukur (§7).
- Tiap kelas dikendarai di lintasan uji: kecepatan maksimal tercapai ±10%, dan bisa belok tanpa tersangkut.
- Mencuri satu mobil dan satu motor dengan model baru, lalu mengecat ulang di bengkel.
- Pengukuran perf seperti §8.

## 10. Risiko

| Risiko | Mitigasi |
|---|---|
| Nama node roda tidak terpisah | Roda tidak berputar; dicatat di report |
| Gaya PSX berbeda dengan dunia | Dicek dari screenshot sebelum lanjut ke 3B |
| glTFast belum terpasang | Ditambahkan ke manifest; tes memastikan importer menghasilkan GameObject |
| Supra paling berat (12,6 ribu segitiga) | Masih dalam anggaran; lewat 40 m tanpa bayangan |
