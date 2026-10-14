# OSM World Generator — Design Spec

- **Tanggal:** 2026-10-13
- **Status:** Menunggu review
- **Proyek:** Tyco World (Unity 6000.6.5f1, URP 17.6)
- **Sub-proyek:** #1 dari game open world horor berbasis cerita

## 1. Latar dan tujuan

Tyco World adalah game open world horor berbasis cerita yang berlatar area nyata di sekitar
Telkom University, Bandung. Sub-proyek pertama ini membuat **World Generator**: tool di Unity
Editor yang mengubah data OpenStreetMap (OSM) menjadi dunia 3D low poly yang bisa dijelajahi
dengan berjalan kaki.

Sub-proyek lain (player controller, sistem cerita/quest, musuh AI, audio, interior, save)
dikerjakan terpisah, masing-masing dengan siklus spec → plan → implementasi sendiri.

### Keputusan yang sudah disepakati

| Topik | Keputusan |
|---|---|
| Tujuan | Proyek pribadi. Nama asli (Tel-U, TULT, Sukabirus) boleh dipakai. |
| Gaya visual | Low poly + tekstur semi-foto + filter grain/VHS (ala *Chilla's Art*, *Fears to Fathom*). |
| Platform/kamera | PC, first person. |
| Pergerakan | Jalan kaki saja. |
| Interior | Hampir semua bangunan hanya eksterior; gedung kunci bisa diganti prefab berinterior buatan tangan. |
| Pendekatan | Importer C# buatan sendiri di Unity Editor (bukan Blender/Blosm, bukan plugin berbayar). |

## 2. Ruang lingkup

### Termasuk
- Download data OSM via Overpass API dan menyimpannya di project (bisa dipakai offline).
- Parsing, proyeksi koordinat, klasifikasi fitur.
- Generate mesh: ground, jalan, bangunan (dinding + atap), lahan, air.
- Penempatan props: pohon (node OSM + sebaran) dan lampu jalan (prosedural).
- Material dan tekstur gaya A: tekstur CC0 + tekstur fasad prosedural (placeholder).
- Prefab placeholder low poly untuk pohon dan lampu.
- Sistem gedung kunci (override dan penggantian prefab).
- Komponen runtime untuk metadata OSM yang bisa dipakai sistem cerita nanti.
- Editor window untuk Download / Generate / Clear dan statistik.
- Scene preview: cahaya malam, fog, post-processing grain/VHS.
- Konfigurasi render: Forward+ dan GPU Resident Drawer.
- Tes EditMode dan verifikasi integrasi dengan data asli.

### Tidak termasuk (non-goals)
- Player controller, sistem cerita/quest, AI, audio, UI game, save.
- Interior bangunan (gedung kunci hanya ditandai).
- Parit/sungai yang punya kedalaman dan collider; terrain dengan kontur ketinggian.
- Pagar/tembok dari data OSM (data kosong); tembok prosedural dibuat oleh spec street-frontage.
- Mesh simpang khusus (trotoar, selokan, dan marka tengah kini dibuat oleh spec `2026-10-14-street-frontage-design.md`).
- LOD, impostor, streaming dunia.
- Generate dunia saat runtime (semuanya dilakukan di Editor).
- Kendaraan.

## 3. Area dan data

### Batas area

| | Nilai |
|---|---|
| Selatan / Barat / Utara / Timur | -6.9850 / 107.6255 / -6.9675 / 107.6380 |
| Titik nol (origin) | Simpang Jl. Telekomunikasi–Jl. Sukabirus: lat -6.97265, lon 107.63400 |
| Ukuran | ±1381 m (timur–barat) × ±1935 m (utara–selatan) |
| Batas dalam koordinat lokal | x: -939.3 … +442.0 m, z: -1365.8 … +569.5 m |

Area ini mencakup seluruh poligon kampus Telkom University di OSM (way 557649490),
Jl. Sukabirus dari ujung ke ujung (±1.4 km), dan ruas Jl. Telekomunikasi yang tercatat di OSM.

### Isi data (snapshot OSM Juli 2026)

| Jenis | Jumlah |
|---|---|
| Bangunan (way) | 4.983 |
| Bangunan (relation multipolygon) | 4 |
| Ruas jalan (`highway`) | 768 |
| Waterway (garis) | 18 |
| Area air (`natural=water`) | 3 |
| Landuse / leisure | 37 / 15 |
| Node pohon | 30 |
| Pagar/tembok, lampu jalan | 0 / 0 |
| Bangunan dengan `building:levels` / `height` | ±55 / 0 |

### Query Overpass

```
[out:json][timeout:90][bbox:-6.9850,107.6255,-6.9675,107.6380];
(
  way["highway"];
  way["building"];
  relation["building"];
  way["landuse"];
  relation["landuse"];
  way["leisure"];
  way["natural"];
  relation["natural"="water"];
  way["waterway"];
  way["amenity"~"^(university|parking|grave_yard)$"];
  node["natural"="tree"];
);
(._;>;);
out body;
```

Batas area dan daftar endpoint diambil dari Settings; teks query dibuat oleh `OverpassQuery`.

## 4. Arsitektur

### Alur data

```
Overpass API ──(tombol Download)──► WorldGenData/osm_raw.json (+ osm_raw.info.json)
                                          │
                                   OsmParser → OsmData
                                          │
                              GeoProjection: lat/lon → meter lokal
                                          │
                     FeatureClassifier → RoadFeature / BuildingFeature /
                                         AreaFeature / WaterFeature / TreeFeature
                                          │
     GroundBuilder · RoadMeshBuilder (jalan + waterway garis) · BuildingMeshBuilder ·
     AreaMeshBuilder (lahan + air area) · PropPlacer
                                          │
     WorldBuilder → scene World_Generated.unity + Chunk_x_z.asset + GenerationReport
```

### Assembly

| Assembly | Lokasi | Isi |
|---|---|---|
| `TycoWorld.WorldGen` (runtime) | `Assets/WorldGen/Runtime/` | `OsmFeature`, `OsmFeatureRegistry`, `KeyBuildingMarker`, `GeneratedRoot`, `WorldSceneLoader` |
| `TycoWorld.WorldGen.Editor` (editor) | `Assets/WorldGen/Editor/` | Semua logika generator, settings, window |
| `TycoWorld.WorldGen.Tests` (editor, tes) | `Assets/WorldGen/Tests/` | Tes EditMode + `Fixtures/` |

Paket tambahan: `com.unity.nuget.newtonsoft-json` (untuk membaca tag OSM yang berbentuk dictionary).

### Unit dan tanggung jawabnya

| Unit | Tanggung jawab | Bergantung pada |
|---|---|---|
| `OverpassQuery` | Menyusun teks query dari batas area. | — |
| `OsmFetcher` | Mengirim query ke endpoint satu per satu (timeout 90 detik), memvalidasi respons, menulis cache secara atomik. | `OverpassQuery`, `OverpassResponseValidator` |
| `OverpassResponseValidator` | Fungsi murni: memastikan respons adalah JSON, punya `elements` yang tidak kosong, dan tidak punya `remark` berisi error. | — |
| `OsmParser` | JSON → `OsmData` (dictionary node, way, relation beserta tag). | Newtonsoft |
| `GeoProjection` | lat/lon ↔ meter lokal (lihat §5). | — |
| `FeatureClassifier` | Elemen OSM → fitur bertipe beserta parameternya (lebar, tinggi, kategori). | Settings, `RingAssembler` |
| `RingAssembler` | Menggabungkan member way milik multipolygon menjadi ring tertutup. | — |
| `Polygon2D` | Luas, arah urutan titik, titik di dalam poligon, centroid, pembersihan titik. | — |
| `Triangulator` | Ear clipping dengan dukungan lubang (bridging metode Eberly). | `Polygon2D` |
| `Clipping` | Liang–Barsky (garis) dan Sutherland–Hodgman (poligon) terhadap batas area. | — |
| `ObbCalculator` | Oriented bounding box dengan luas minimum (rotating calipers pada convex hull). | — |
| `SplitMix64` | PRNG deterministik. `hash(osmId, salt)` = `osmId XOR FNV-1a-64(salt)`, dipakai sebagai seed. | — |
| `SpatialGrid` | Grid 20 m untuk kueri cepat tapak bangunan dan segmen jalan. | — |
| `PoissonDisk` | Sebaran titik Bridson di dalam poligon. | `SplitMix64` |
| `GroundBuilder` | Bidang ground seluas area + BoxCollider. | — |
| `RoadMeshBuilder` | Mesh pita jalan dan pita waterway. | `Polygon2D` |
| `BuildingMeshBuilder` | Mesh dinding, atap, dan trim per bangunan. | `Triangulator`, `ObbCalculator` |
| `AreaMeshBuilder` | Mesh poligon datar untuk lahan dan area air. | `Triangulator` |
| `PropPlacer` | Posisi pohon dan lampu (tanpa membuat GameObject). | `PoissonDisk`, `SpatialGrid` |
| `ChunkAssetWriter` | Mengelompokkan mesh per chunk 100 × 100 m dan menyimpannya ke `Chunk_x_z.asset`. | AssetDatabase |
| `WorldBuilder` | Mengatur urutan seluruh proses, membuat GameObject, menyimpan scene, dan membuat `GenerationReport`. | Semua di atas |
| `WorldGenSettings` | ScriptableObject berisi semua parameter dan tabel aturan (§8). | — |
| `DefaultSettingsFactory` | Membuat asset Settings dengan isi default dari spec ini. | — |
| `WorldGenWindow` | Menu `Tyco World > World Generator`. | `OsmFetcher`, `WorldBuilder` |
| `FacadeTextureGenerator` | Membuat tekstur fasad prosedural (PNG). | — |
| `PlaceholderPrefabFactory` | Membuat prefab pohon dan lampu low poly placeholder. | — |

Unit di bawah `Data/`, `Geo/`, `Classify/`, dan `Geometry/` tidak menyentuh scene maupun AssetDatabase,
sehingga bisa dites langsung.

### Struktur folder

```
WorldGenData/                      (di luar Assets, dilacak git)
  osm_raw.json
  osm_raw.info.json                (waktu download, batas area, endpoint, jumlah elemen)
Assets/Scenes/World.unity          (scene utama: konten manual + lighting + preview; dilacak git)
Assets/WorldGen/
  Runtime/                         (asmdef TycoWorld.WorldGen)
  Editor/                          (asmdef TycoWorld.WorldGen.Editor)
    Data/ Geo/ Classify/ Geometry/ Builders/ Pipeline/ Settings/ UI/ Art/
  Tests/                           (asmdef TycoWorld.WorldGen.Tests) + Fixtures/
  Config/WorldGenSettings.asset
  Materials/  Textures/{CC0,Facade}/  Textures/SOURCES.md  Prefabs/
  Generated/                       (DIABAIKAN git; dihapus & dibuat ulang setiap Generate)
    World_Generated.unity
    Meshes/Chunk_{ix}_{iz}.asset
```

### Pemisahan scene

**Ini perubahan dari Bagian 3 yang disetujui di chat**, yang semula menaruh `World_Generated` di
dalam `World.unity`. Alasannya: scene berisi ±6.000 objek bisa berukuran belasan MB, dan setiap
kali Generate isinya berubah semua, sehingga riwayat git cepat membengkak. Dengan pemisahan ini:

- `World.unity` (dilacak git) berisi konten manual: pemicu cerita, props manual, interior, lighting,
  dan kamera preview. **Generate tidak pernah mengubah scene ini.**
- `World_Generated.unity` (diabaikan git) sepenuhnya dimiliki generator dan dimuat secara additive.
- Karena referensi antar-scene tidak bisa diserialisasi, konten manual menemukan objek hasil generate
  lewat `OsmFeatureRegistry` (berdasarkan OSM id atau nama).
- `WorldSceneLoader` (dipasang di `World.unity`) memuat `World_Generated` secara additive saat Awake
  jika scene itu belum dimuat. Kedua scene didaftarkan di Build Settings.
- Setelah clone baru, cukup klik Generate: hasilnya deterministik dari `osm_raw.json` + Settings + kode.

## 5. Proyeksi koordinat

Proyeksi equirectangular lokal di sekitar titik nol (φ₀ = -6.97265°, λ₀ = 107.63400°):

```
mPerDegLat = 111132.92 − 559.82·cos(2φ₀) + 1.175·cos(4φ₀)            ≈ 110590.64
mPerDegLon = 111412.84·cos(φ₀) − 93.5·cos(3φ₀) + 0.118·cos(5φ₀)     ≈ 110501.61
x (timur) = (λ − λ₀) · mPerDegLon
z (utara) = (φ − φ₀) · mPerDegLat
```

Pada jarak < 2 km, galatnya jauh di bawah 1 m. Perhitungan memakai `double`, lalu dikonversi ke `float`
setelah dikurangi titik nol.

## 6. Klasifikasi fitur

Urutan pengecekan per elemen (yang pertama cocok dipakai):

1. Punya tag `building` dengan nilai selain `no`, berupa way tertutup atau relation multipolygon → **Bangunan**.
2. Punya `highway` yang ada di tabel jalan (§7.1):
   - dengan `area=yes` → **Area** kategori Paving;
   - tanpa itu → **Jalan**.
3. `waterway` ∈ {river, stream, canal, drain, ditch} → **Air (garis)**.
4. `natural=water`, `landuse` ∈ {reservoir, basin}, atau punya tag `water` → **Air (area)**.
5. Cocok dengan tabel area (§7.3) → **Area**.
6. Node dengan `natural=tree` → **Pohon**.
7. Selain itu → diabaikan (dihitung di laporan sebagai "ignored").

Aturan tambahan:
- **Way tertutup**: node pertama sama dengan node terakhir.
- **Relation multipolygon**: member `outer` dirangkai lewat `RingAssembler`, member `inner` menjadi lubang.
  Ring yang tidak bisa ditutup dilewati.
- **Konteks kampus**: sebuah bangunan termasuk konteks kampus jika centroid-nya ada di dalam poligon
  `amenity=university` (way 557649490).

## 7. Aturan geometri

### Lapisan ketinggian (anti z-fighting)

| Lapisan | y (m) |
|---|---|
| Ground | 0.000 (permukaan collider) |
| Area Campus | 0.015 |
| Area Hardground | 0.020 |
| Area Grass / Wood / Farmland | 0.025 |
| Area Parking / Cemetery / Pitch | 0.030 |
| Area Paving (`highway` + `area=yes`) | 0.035 |
| Air (area) | 0.045 |
| Air (garis) | 0.050 |
| Dasar tepi jalan (bahu, kanal, kaki kerb) | 0.055 |
| Jalan | 0.060 + rank × 0.005 (rank di §7.1) |
| Bibir selokan | 0.155 |
| Atas trotoar | 0.210 |
| Marka | y jalan + 0.003 |

Ground, jalan, area, dan air punya collider (lihat spec `2026-10-13-fps-player-design.md` §5). Permukaan yang
lebih tinggi digambar di atas dan juga dipijak lebih dulu, jadi raycast ke bawah selalu mengenai permukaan yang
terlihat. Setiap collider permukaan membawa `GroundSurface` (jenis permukaan dari material).
Detail di spec street-frontage §5.

### 7.1 Jalan

| `highway` | Lebar default (m) | Rank | Jarak lampu (m) |
|---|---|---|---|
| primary | 8.0 | 8 | 30 |
| secondary | 7.0 | 7 | 30 |
| tertiary | 6.0 | 6 | 30 |
| unclassified | 5.0 | 5 | 40 |
| residential | 5.0 | 4 | 40 |
| living_street, pedestrian | 4.0 / 3.0 | 3 | 0 |
| service | 3.5 | 2 | 50 |
| track | 3.0 | 1 | 0 |
| footway, path, cycleway, steps | 1.5 | 0 | 0 |

Jalan dengan `highway` di luar tabel ini (misalnya `proposed`, `construction`, `platform`, `corridor`) diabaikan.
Jarak lampu 0 berarti tidak dipasang lampu.

**Lebar:**
1. Pakai tag `width`: ambil angka di awal teks ("6", "6 m", "6.5"); kalau tidak valid, lanjut ke langkah 2.
2. Pakai `lanes` × 3.0 m.
3. Pakai lebar default dari tabel.

**Mesh pita:**
- Untuk tiap titik garis tengah, dihitung arah miter dengan panjang `halfWidth / cos(θ/2)`.
  Jika panjangnya melebihi `2 × halfWidth`, sambungan diganti bevel (dua verteks).
- Ujung jalan dipotong datar. Jika node ujung juga dipakai jalan lain (simpang), ujungnya
  diperpanjang sejauh `halfWidth` agar celah di simpang tertutup oleh tumpukan pita.
- UV dalam meter: u = posisi melintang (0 … lebar), v = jarak sepanjang jalan. Ukuran ulangan
  tekstur diatur di material.
- Pita dipotong tepat di batas area (Liang–Barsky).
- Jembatan (`bridge=yes`) diperlakukan sama seperti jalan biasa (datar).
- Jalan punya MeshCollider (non-convex, static) dan GroundSurface (Asphalt/Paving/Dirt).

### 7.2 Bangunan

**Tinggi dinding (`wallHeight`):**
1. Tag `height` (angka di awal teks, satuan meter).
2. `building:levels` × `levelHeight`.
3. Default dari tabel aturan bangunan.
4. Hasilnya tidak boleh kurang dari 2.5 m.

`levelHeight` default 3.2 m.

**Tabel aturan bangunan** (urutan dicek dari atas; nilai `building:levels` atau `height` dari OSM selalu didahulukan):

| # | Nilai `building` | Konteks | Lantai default | Atap pelana boleh? | Set material |
|---|---|---|---|---|---|
| 1 | dormitory | semua | 4 | tidak | Campus |
| 2 | university, school, college, office, public, hospital | semua | 3 | tidak | Campus |
| 3 | mosque, church, temple, religious | semua | 1 (levelHeight 5.0 m) | tidak | Residential |
| 4 | commercial, retail, shop | semua | 2 | tidak | Residential |
| 5 | garage, garages, shed, hut, carport, kiosk | semua | 1 | ya | Residential |
| 6 | roof | semua | atap saja (lihat bawah) | — | Residential |
| 7 | lainnya (yes, house, residential, apartments, …) | kampus | 3 | tidak | Campus |
| 8 | lainnya | non-kampus | acak 1–3 (seragam) | ya | Residential |

Angka acak memakai `SplitMix64` dengan seed `hash(osmId, "levels")`, jadi hasilnya selalu sama
setiap kali Generate.

**Submesh per bangunan** (submesh yang kosong tidak dibuat, dan array material disesuaikan):

| Submesh | Isi | Material |
|---|---|---|
| GroundFloor | Dinding dari 0 sampai `min(levelHeight, wallHeight)` | daftar `groundFloor` |
| UpperFloors | Dinding dari `levelHeight` sampai `wallHeight` | daftar `upper` |
| Trim | Parapet (dua sisi) dan segitiga ujung atap pelana | daftar `trim` (indeks sama dengan `upper`) |
| Roof | Tutup atap (dak) atau bidang atap pelana (dua sisi) | daftar `flatRoof` atau `gableRoof` |

**Indeks varian material:** `hash(osmId, salt) mod panjangDaftar`.
- Salt `"ground"` untuk GroundFloor.
- Salt `"upper"` untuk UpperFloors dan Trim, jadi warna trim selalu cocok dengan warna lantai atas.
- Salt `"roof"` untuk Roof.

**UV fasad:**
- Arah horizontal: tiap sisi dinding dibagi `bays = max(1, round(panjangSisi / facadeTileWidth))`
  (default 3.0 m), dengan u = 0 … bays. Jendela tidak terpotong di sudut bangunan.
- Arah vertikal: v = 0 … `min(1, wallHeight / levelHeight)` untuk lantai dasar, dan
  v = 0 … `(wallHeight − levelHeight) / levelHeight` untuk lantai atas.
- Jika tinggi berasal dari tag `height`, jumlah lantai (untuk syarat atap pelana) dihitung
  sebagai `max(1, round(wallHeight / levelHeight))`.
- Normal dibuat per sisi (flat shading, verteks tidak dipakai bersama antar sisi).

**Atap pelana** dipakai jika semua syarat ini terpenuhi:
- aturan bangunannya mengizinkan (#5 atau #8);
- tidak ada lubang;
- luas ≤ 200 m²;
- rectangularity (luas tapak / luas OBB) ≥ 0.85;
- jumlah lantai ≤ 3;
- tidak ada override `Flat`.

Geometrinya:
- Bubungan sejajar sisi panjang OBB.
- Tinggi bubungan = `wallHeight + 0.25 × sisiPendek`.
- Bidang atap menutupi OBB yang diperluas 0.3 m ke semua arah (overhang).
- Dinding mengikuti tapak asli sampai ketinggian `wallHeight`.
- Segitiga ujung atap berdiri di kedua sisi pendek OBB.

**Atap dak (datar):**
- Tutup atap di ketinggian `wallHeight`, ditriangulasi termasuk lubang.
- Parapet setinggi 0.6 m di atas `wallHeight`, dua sisi (luar dan dalam), masuk submesh Trim.

**`building=roof`:**
- Pelat setebal 0.15 m di ketinggian 3.0 m (atas, bawah, dan tepi).
- Empat tiang 0.2 × 0.2 m di sudut-sudut OBB.
- Tanpa dinding.

**Collider:** MeshCollider (non-convex, static) memakai mesh bangunan itu sendiri.

**Bangunan dilewati** (dicatat dengan alasannya) jika:
- jumlah titiknya kurang dari 3 setelah pembersihan;
- luasnya < 4 m²;
- garisnya saling silang (self-intersecting);
- centroid-nya di luar batas area;
- ring multipolygon-nya tidak bisa ditutup.

**Pembersihan sebelum diproses:**
- Titik berurutan yang berjarak < 0.05 m dihapus.
- Titik yang segaris (deviasi < 0.01 m) dihapus.
- Ring luar dibuat CCW, lubang dibuat CW.

### 7.3 Area dan air

| Kecocokan tag (urutan dicek dari atas) | Kategori | Material | Jarak sebaran pohon (m) |
|---|---|---|---|
| `amenity=parking` | Parking | Area_Parking | 0 |
| `amenity=grave_yard`, `landuse=cemetery` | Cemetery | Area_Cemetery | 10 |
| `leisure` ∈ {pitch, track, sports_centre, stadium} | Pitch | Area_Pitch | 0 |
| `landuse` ∈ {farmland, paddy} | Farmland | Area_Farmland | 0 |
| `natural` ∈ {wood, scrub}, `landuse=forest` | Wood | Area_Grass | 4 |
| `landuse` ∈ {grass, village_green, meadow, recreation_ground}, `leisure` ∈ {park, garden}, `natural=grassland` | Grass | Area_Grass | 12 |
| `landuse` ∈ {commercial, retail, industrial, construction, brownfield} | Hardground | Area_Hardground | 0 |
| `amenity=university` | Campus | Area_Campus | 20 |
| `highway` + `area=yes` | Paving | Path_Paving | 0 |

`landuse=residential` tidak digambar (cukup ground).

- Poligon area dipotong di batas area (Sutherland–Hodgman), lalu ditriangulasi.
- UV dalam meter (proyeksi XZ). Area punya MeshCollider dan GroundSurface.
- **Air (area)**: poligon datar dengan material `Water`.
- **Air (garis)**: pita dengan lebar dari tag `width`, atau default river 8, canal 4, stream 3, drain/ditch 1.5 m.
- Air punya MeshCollider datar dan GroundSurface Water, tanpa kedalaman.

### 7.4 Ground

- Satu bidang seukuran batas area di y = 0, dengan material `Ground_Dirt` dan UV dalam meter.
- BoxCollider setebal 1 m dengan permukaan atas di y = 0.
- GroundSurface Dirt. Empat BoxCollider tak terlihat (WorldEdge, setinggi 30 m, tebal 2 m) memagari batas area.

## 8. Props

### Pohon
- **Node OSM `natural=tree`**: ditempatkan langsung, kecuali jika jatuh di dalam bangunan.
- **Sebaran**: Poisson disk (Bridson) di dalam poligon area, dengan jarak minimum sesuai tabel §7.3
  dan seed `hash(areaId, "trees")`.
- Titik ditolak jika:
  - berada di dalam tapak bangunan (dengan buffer 1 m);
  - jaraknya ke garis tengah jalan < `halfWidth + 1 m`;
  - jarak ke garis tengah jalan < hw + max(1, lebar tepi + 0.5), atau < 0.8 m dari tembok;
  - berada di dalam lubang poligon.
- Batas global `maxTrees` = 4000. Kalau terlampaui, titik dipilih secara deterministik dan sisanya
  dibuang (jumlahnya dicatat di laporan).
- Rotasi Y acak 0–360°, skala 0.8–1.2, varian prefab acak (semuanya dari `SplitMix64`).

### Lampu jalan
- Dipasang menyusuri setiap jalan dengan jarak sesuai tabel §7.1.
- Posisinya di offset `halfWidth + 0.6 m` tegak lurus terhadap jalan, selang-seling kiri dan kanan.
- Offset mengikuti profil tepi: trotoar hw + 0.45, selokan hw + 0.15 (di bahu), tanpa tepi hw + 0.6; lampu menjauhi tembok 0.35 m (spec street-frontage §9).
- Lampu menghadap ke jalan.
- Lampu ditolak jika berada di dalam bangunan, atau dalam radius 8 m dari lampu lain
  (mencegah lampu menumpuk di simpang).
- Prefab lampu berisi point light: tanpa bayangan, jangkauan 12 m, warna putih hangat (±3500 K).

### Prefab placeholder (`PlaceholderPrefabFactory`)
- `Tree_LowPoly_A`: batang silinder 6 sisi + tajuk icosphere. Collider capsule di batang.
- `Tree_LowPoly_B`: batang + tajuk kerucut 6 sisi.
- `StreetLamp_A`: tiang silinder 6 sisi setinggi 6 m, lengan, kepala lampu, dan point light. Collider capsule.

Prefab ini bisa diganti kapan saja lewat Settings.

## 9. Material dan tekstur

- Shader: **URP Simple Lit** untuk semua material generator.
- **Tekstur CC0** dari ambientCG (atau Poly Haven sebagai cadangan), versi 1K-JPG. Yang dipakai hanya peta
  Color + NormalGL, di-import dengan max size 512, filter bilinear, dan mipmap aktif.
  ID asset yang dipilih dicatat di `Assets/WorldGen/Textures/SOURCES.md`.

| Material | Sumber | Ukuran ulangan (m) |
|---|---|---|
| Road_Asphalt | ambientCG Asphalt | 4 |
| Path_Paving | ambientCG PavingStones | 2 |
| Ground_Dirt | ambientCG Ground | 4 |
| Area_Grass | ambientCG Grass | 4 |
| Area_Farmland | ambientCG Grass + tint hijau kekuningan | 4 |
| Area_Pitch | ambientCG Grass + tint hijau terang | 4 |
| Area_Parking, Area_Hardground | ambientCG Concrete | 3 |
| Area_Cemetery | ambientCG Ground + tint gelap | 4 |
| Area_Campus | ambientCG PavingStones + tint terang | 2 |
| Roof_Genteng_Merah, Roof_Genteng_Coklat | ambientCG RoofingTiles + tint | 2 |
| Roof_Dak_A, Roof_Dak_B | ambientCG Concrete + tint | 3 |
| Water | tanpa tekstur; warna #10161A, smoothness 0.9 | — |

- **Tekstur fasad prosedural** (`FacadeTextureGenerator`): PNG 256 × 256 yang mewakili satu bay
  (3.0 m × 3.2 m). Isinya warna dasar + noise + gradasi kotor di bagian bawah + elemen bangunan.
  - **Residential upper** (6 warna: putih kusam #D9D4C7, krem #E3CFA0, hijau pucat #A9C3A0,
    biru pucat #9FB8C9, pink pucat #D8A9A9, abu #A7A7A7): jendela kaca gelap berbingkai, dengan teralis.
  - **Residential trim** (6 warna yang sama): polos.
  - **Residential ground floor** (4 jenis): rolling door, pintu + jendela kos, bukaan warung + papan
    nama kosong, dinding + jendela.
  - **Campus upper** (2 varian): beton terang dengan pita jendela lebar.
  - **Campus ground floor** (2 varian): kaca + pintu.
  - **Campus trim** (2 varian): beton polos.
- Set material Residential dan Campus dipakai oleh tabel aturan bangunan (§7.2). Atap dak Campus
  memakai `Roof_Dak_A`.
- Tekstur fasad ini adalah **placeholder**. Pengguna bisa menggantinya dengan foto sendiri lewat
  material di Settings.
- Material yang kosong di Settings diganti material abu-abu netral `WorldGen_Fallback`, disertai warning.

## 10. Gedung kunci

`WorldGenSettings.keyBuildings`: daftar `{ osmId, label, prefab (opsional), overrideLevels (0 = tidak ada), overrideRoof (Auto | Flat | Gable) }`.

- **Prefab terisi**: mesh tidak di-generate. Prefab ditaruh di centroid tapak dengan rotasi Y
  mengikuti sumbu panjang OBB (konvensi: sumbu panjang prefab = +X lokal).
- **Prefab kosong**: bangunan di-generate seperti biasa dengan override yang berlaku, lalu diberi
  komponen `KeyBuildingMarker`.
- Semua gedung kunci ditempatkan di bawah `World_Generated/KeyBuildings/`.
- Gedung kunci yang id-nya tidak ditemukan di data menghasilkan warning, tanpa error.

Isi awal:

| OSM way id | Label |
|---|---|
| 1257717375 | Telkom University Landmark Tower (TULT) |
| 197341486 | Gedung Bangkit (Rektorat) |
| 1293369802 | Gedung Tokong Nanas |
| 1293369773 | Asrama Putra Gedung 1 |
| 1293369796 | Asrama Putri Gedung A |
| 409467778 | Kos Raflesia 3 (kos pemain, spec #2 §3) |

Kos Raflesia 3 (kos pemain) ditambahkan di sub-proyek #2.

## 11. Output

### Hierarki `World_Generated.unity`

```
World_Generated            [GeneratedRoot]
  Ground                                                       [GroundSurface, BoxCollider]
  WorldEdge/Edge_West|East|South|North                         [BoxCollider]
  Roads/Chunk_ix_iz/<nama jalan atau Road_<id>>                [OsmFeature, GroundSurface, MeshCollider]
  Buildings/Chunk_ix_iz/<nama gedung atau Building_<id>>       [OsmFeature, MeshCollider]
  Areas/Chunk_ix_iz/<Area_<kategori>_<id>>                     [OsmFeature, GroundSurface, MeshCollider]
  RoadEdges/Chunk_ix_iz/{Gutter,Sidewalk}                      [GroundSurface, MeshCollider]
  Frontage/Chunk_ix_iz/Walls                                   [MeshCollider]
  Buildings/Chunk_ix_iz/Infill                                 [OsmFeature, MeshCollider]
  Water/Chunk_ix_iz/<Water_<id>>                               [OsmFeature, GroundSurface, MeshCollider]
  Props/Trees/Chunk_ix_iz/...   Props/Lamps/Chunk_ix_iz/...
  KeyBuildings/<label>         [OsmFeature, KeyBuildingMarker]
```

- Chunk ditentukan dari centroid fitur (jalan: titik tengah polyline):
  `ix = floor(x / 100)`, `iz = floor(z / 100)`. Rentangnya ix −10…4 dan iz −14…5.
- Satu `Chunk_ix_iz.asset` berisi semua mesh untuk fitur di chunk tersebut (sub-asset).
  Mesh dengan lebih dari 65.535 verteks memakai index format UInt32.
- Semua objek hasil generate diberi flag static, kecuali root.

### Komponen runtime

| Komponen | Isi / API |
|---|---|
| `GeneratedRoot` | Penanda root milik generator. Menyimpan waktu generate dan ringkasan statistik. |
| `OsmFeature` | `long OsmId`, `OsmElementType ElementType`, `FeatureKind Kind`, `string DisplayName`, array tag (key/value). API: `TryGetTag(key, out value)`. Mendaftar ke `OsmFeatureRegistry` di `OnEnable`, keluar di `OnDisable`. |
| `OsmFeatureRegistry` | Static: `TryGet(long osmId, out OsmFeature)`, `FindByName(string)` (mengembalikan semua yang cocok). |
| `KeyBuildingMarker` | `string Label`, `long OsmId`. |
| `WorldSceneLoader` | Di `World.unity`. Memuat `World_Generated` secara additive saat Awake jika belum dimuat. |

### `GenerationReport`

Berisi:
- jumlah per jenis fitur;
- jumlah yang dilewati per alasan (beserta daftar id);
- jumlah yang diabaikan;
- jumlah pohon dan lampu (termasuk yang terpotong batas `maxTrees`);
- durasi tiap tahap;
- hash konten per chunk (dipakai untuk tes determinisme).

Laporan ditampilkan di window, dan rinciannya ditulis ke Console.

## 12. Settings (`WorldGenSettings`)

| Grup | Field |
|---|---|
| Area | `south, west, north, east` (double), `originLat, originLon` (double), `chunkSize` = 100 |
| Download | `overpassEndpoints` = [overpass-api.de, overpass.kumi.systems, overpass.private.coffee], `timeoutSeconds` = 90, `rawDataPath` = `WorldGenData/osm_raw.json` |
| Jalan | daftar `RoadRule { highway, defaultWidth, rank, material, lampSpacing }`, `lanesWidth` = 3.0 |
| Bangunan | `levelHeight` = 3.2, `facadeTileWidth` = 3.0, `parapetHeight` = 0.6, `gableMaxArea` = 200, `gableMinRectangularity` = 0.85, `gablePitchRatio` = 0.25, `gableOverhang` = 0.3, `minWallHeight` = 2.5, daftar `BuildingRule { buildingValues[], context, minLevels, maxLevels, levelHeightOverride, allowGable, materialSet, roofOnly }`, `materialSets { Residential, Campus }` masing-masing `{ groundFloor[], upper[], trim[], flatRoof[], gableRoof[] }` |
| Area | daftar `AreaRule { matches[] (key, values[]), category, yOffset, material, treeSpacing }` |
| Air | `waterMaterial`, lebar default per jenis waterway |
| Props | `treePrefabs[]`, `lampPrefab`, `maxTrees` = 4000, `lampMinSpacing` = 8 |
| Gedung kunci | daftar `KeyBuilding` (§10) |
| Lainnya | `fallbackMaterial` |
| Tepi, frontage, infill | `roadEdges`, `frontage`, `infill` — lihat spec street-frontage §12 |

Semua nilai default di tabel ini sama dengan yang tertulis di spec ini, dan diisi oleh `DefaultSettingsFactory`.

## 13. Render dan scene preview

**Konfigurasi project** (sekali, saat implementasi):
- `PC_Renderer` → Rendering Path **Forward+**.
- `PC_RPAsset` → **GPU Resident Drawer: Instanced Drawing**.
- Graphics Settings → BatchRendererGroup Variants: **Keep All**.
- Nama field dan apakah GPU Resident Drawer bisa berjalan bersama static batching
  diverifikasi dulu ke dokumentasi Unity 6.6 sebelum diubah.

**`World.unity`:**
- Directional Light "Moon": intensitas 0.15, warna #9DB4FF, rotasi (50°, −30°, 0), soft shadows.
- Ambient gelap kebiruan. Fog exponential dengan density 0.02 dan warna #0E1218.
- Kamera dengan clear color yang sama dengan warna fog.
- Global Volume dengan profil `Preview_NightVHS`:
  - Tonemapping ACES;
  - Color Adjustments: saturation −25, contrast +10;
  - Film Grain intensitas 0.5;
  - Vignette 0.35;
  - Chromatic Aberration 0.15;
  - Bloom intensitas 0.6, threshold 1.0.
- `PreviewCam` di titik tengah way 95529338 (Jl. Sukabirus) setinggi 1.6 m, menghadap sepanjang
  arah jalan. Posisi dan arahnya dihitung otomatis dari data saat setup.
- `WorldSceneLoader`.

## 14. Penanganan error

| Situasi | Perilaku |
|---|---|
| Endpoint timeout, HTTP error, HTML, JSON rusak, ada `remark` error, atau `elements` kosong | Coba endpoint berikutnya. Jika semua gagal: dialog error, cache lama tidak disentuh. |
| Download sukses | Ditulis ke file sementara dan divalidasi, lalu menggantikan `osm_raw.json`. `osm_raw.info.json` diperbarui. |
| Generate ketika cache belum ada | Pesan "Download data dulu". |
| `World.unity` atau scene lain sedang punya perubahan belum disimpan | `SaveCurrentModifiedScenesIfUserWantsTo`. Jika dibatalkan, Generate juga batal. |
| Way merujuk node yang hilang | Fitur dilewati dan dicatat. |
| Geometri tidak valid (§7.2) | Fitur dilewati dan dicatat. Proses tetap lanjut. |
| Material atau prefab kosong | Diganti fallback, disertai warning. |
| Id gedung kunci tidak ditemukan | Warning. |
| Progress bar dibatalkan | Folder `Generated/` dihapus. Scene generate yang setengah jadi ditutup tanpa disimpan. |
| Clear | Konfirmasi dulu, lalu hapus `Assets/WorldGen/Generated/`. Hanya folder itu yang disentuh. |

- Generate hanya menulis ke `Assets/WorldGen/Generated/` dan Build Settings.
- Scene `World_Generated.unity` disimpan otomatis karena sepenuhnya milik generator.
- `World.unity` tidak pernah diubah oleh Generate.
- Asset ditulis di dalam blok `AssetDatabase.StartAssetEditing/StopAssetEditing`.

## 15. Pengetesan

### Tes EditMode (`TycoWorld.WorldGen.Tests`)

| Area | Kasus |
|---|---|
| `GeoProjection` | Titik nol → (0, 0). +0.001° lintang → z = 110.59 ± 0.01 m. +0.001° bujur → x = 110.50 ± 0.01 m. Hasil bolak-balik tetap sama. |
| `OverpassResponseValidator` | JSON valid → OK. `remark` runtime error → gagal. Halaman HTML → gagal. `elements` kosong → gagal. |
| `OsmParser` | Fixture `sukabirus_small.json` (potongan data asli: way 95529338, beberapa bangunan, satu multipolygon, satu node pohon) → jumlah node/way/relation dan tag yang benar. |
| `RingAssembler` | Dua way yang bersambung menjadi satu ring. Way dengan arah terbalik tetap bisa dirangkai. Ring tidak tertutup → gagal. |
| `FeatureClassifier` | Contoh tag untuk setiap baris tabel §6, §7.1, §7.2, §7.3. Urutan prioritas (`building` + `amenity` → Bangunan). Parsing `width` ("6", "6 m", "abc"). |
| `Polygon2D` | Luas, CCW/CW, titik di dalam poligon (termasuk di dalam lubang), pembersihan titik ganda dan titik segaris. |
| `Triangulator` | Persegi (2 segitiga), bentuk L (n−2 segitiga), poligon berlubang. Total luas segitiga = luas poligon. |
| `Clipping` | Garis keluar-masuk batas area. Poligon yang sebagian di luar batas. |
| `ObbCalculator` | Persegi panjang yang diputar 30° → OBB sama dengan persegi panjang itu. |
| `RoadMeshBuilder` | Garis lurus lebar w → semua verteks berjarak w/2 dari garis tengah. Belokan tajam → bevel (tidak ada paku). |
| `BuildingMeshBuilder` | Tapak 10 × 10 m, 3 lantai → tinggi dinding 9.6 m, v UpperFloors maksimal 2. Persegi panjang 8 × 12 m kecil → atap pelana dengan bubungan sejajar sisi 12 m. Bentuk L → atap dak + parapet. |
| `PoissonDisk` / `PropPlacer` | Semua titik ≥ jarak minimum, tidak ada yang di dalam bangunan atau di badan jalan. |
| Determinisme | Generate fixture dua kali → hash mesh identik. |
| Chunk | `ix`/`iz` benar untuk koordinat negatif dan tepat di batas chunk. |

### Integrasi (dengan data asli, dijalankan lewat Unity MCP)
1. Download, lalu `osm_raw.json` valid dan jumlah elemennya sesuai dengan §3 (toleransi ±10%,
   karena OSM terus berubah).
2. Generate → Console tanpa error, bangunan yang dilewati < 2%, waktu generate < 60 detik.
3. Generate kedua kalinya → hash semua chunk identik dan tidak ada asset yatim.
4. Raycast di edit mode:
   - ke bawah di 20 titik acak → semuanya mengenai ground (y = 0) atau permukaan jalan/area/air (y ≤ 0.25, trotoar 0.21);
   - horizontal ke 6 gedung kunci → semuanya mengenai MeshCollider.
8. Sub-proyek #2: raycast ke bawah di Jl. Sukabirus mengenai collider jalan dengan GroundSurface Asphalt; 4 dinding
   WorldEdge ada; tidak ada warning PhysX segitiga > 500 m (sisi segitiga permukaan pijak ≤ 100 m).
5. Screenshot tampak atas seluruh area: tata letaknya cocok dengan openstreetmap.org.
6. Screenshot dari `PreviewCam` di Jl. Sukabirus: tekstur terlihat, lampu menyala, grain/fog aktif.
7. Play mode di `PreviewCam`: frame time dan jumlah batches dicatat sebagai baseline (bukan syarat lulus).

## 16. Kriteria selesai

- Semua tes EditMode lulus.
- Semua langkah integrasi 1–6 lulus, dan baseline langkah 7 tercatat.
- Window `Tyco World > World Generator` berfungsi: Download, Generate, Clear, dan statistik.
- `World.unity` terbuka dengan `World_Generated` dimuat additive, dan Play mode berjalan tanpa error.
- `SOURCES.md` mencantumkan semua tekstur CC0.
- Semua perubahan di-commit di branch `feature/osm-world-generator`.

## 17. Risiko dan catatan

| Risiko | Mitigasi |
|---|---|
| Server Overpass sering 504 atau sibuk | Data disimpan di project, ada tiga endpoint cadangan, dan Download jarang diperlukan. |
| Data OSM kurang lengkap atau usang (kos baru belum terpetakan) | Bangunan bisa ditambahkan manual. Atau pengguna menambahkannya sendiri ke OSM (editor iD), lalu Download ulang. |
| API Unity 6.6 (field GPU Resident Drawer, Forward+) berbeda dari yang diingat | Diverifikasi ke dokumentasi sebelum mengubah konfigurasi (§13). |
| Batas jumlah light per view di Forward+ | Jangkauan light 12 m dan fog. Jumlah light yang terlihat dicek di langkah integrasi 7. |
| Area dari lapisan yang sama saling tumpang → z-fighting | Diterima untuk versi ini. Bisa diperbaiki nanti dengan offset per fitur. |
| Atap pelana pada tapak yang hampir persegi panjang sedikit meleset dari dinding | Syarat rectangularity ≥ 0.85 dan overhang 0.3 m menutupi selisihnya. |
| Lisensi ODbL | Proyek pribadi. Jika dirilis, tambahkan "© OpenStreetMap contributors" di credit. |

## 18. Iterasi berikutnya (di luar spec ini)

- Parit dan sungai dengan kedalaman (carving) dan mesh simpang tetap terbuka. ~~Selokan di tepi jalan, trotoar, marka jalan~~ selesai (spec street-frontage).
- Pagar kampus dan gerbang (manual atau prefab).
- Tekstur fasad dari foto asli Sukabirus, papan nama, spanduk, dan kabel listrik.
- LOD/impostor jika baseline performa kurang.
- Sub-proyek berikutnya: player controller dan sistem cerita, yang memakai `OsmFeatureRegistry`.
