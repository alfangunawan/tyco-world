# Street Frontage + Road Dressing — Design Spec

- **Tanggal:** 2026-10-14
- **Status:** Disetujui di sesi brainstorm (plan mode); spec ini menuliskannya ulang secara lengkap
- **Proyek:** Tyco World (Unity 6000.6.5f1, URP 17.6)
- **Sub-proyek:** "World hidup" B+C. Dibangun di atas World Generator (#1), First-Person Player (#2), Siklus Pagi/Malam (#3), Performa + Settings (#4), dan Motor (#5), yang semuanya sudah ada di `main` (`38a40ee`).

## 1. Latar dan tujuan

Screenshot siang dunia hasil generate menunjukkan tiga masalah:

- **Tidak rapi.** Jalan hanya pita aspal datar tanpa tepi. Bangunan OSM berdiri di hamparan rumput/tanah dengan jarak dan sudut acak ke jalan, tanpa batas kavling.
- **Bolong / hampa.** OSM tidak memetakan banyak rumah, jadi ada lahan kosong luas di tepi jalan. Kampung Sukabirus aslinya padat, dan tiap rumah punya tembok, pagar, dan gerbang yang menyambung sepanjang gang, sehingga lahan di belakangnya tidak pernah terlihat.
- **Sepi.** Selain pohon dan lampu, tidak ada prop, NPC, atau kendaraan.

Kamera tidak bisa menyelesaikan ini. Player FPS melihat dari level mata, jadi fog dan view distance hanya menyembunyikan yang jauh. Kuncinya adalah **frontage**: koridor ±15 m dari jalan dibuat penuh dan rapi, karena hanya itu yang terlihat dari jalan.

Sub-proyek ini menambahkan ke World Generator:

- **C — road dressing:** bahu jalan, selokan, trotoar dengan kerb, dan marka tengah.
- **B — frontage:** tembok, pagar, dan gerbang sepanjang property line, plus rumah *infill* di kavling kosong dekat jalan.

### Roadmap "world hidup" (sub-proyek lain dikerjakan terpisah)

| # | Sub-proyek | Isi |
|---|---|---|
| A | Atmosfer & kamera | Fog haze siang, default view distance, grading, audio 3D positional (warung, motor lewat, adzan) |
| **B+C** | **Spec ini** | Tepi jalan, marka, tembok/pagar/gerbang, infill |
| D | Clutter kampung | Tiang dan kabel listrik, motor parkir, warung/gerobak, spanduk, tong sampah. Pack model CC0 (Kenney/Quaternius) |
| E | Kehidupan dinamis | NPC pejalan kaki dan motor/mobil ambient. Siang ramai, malam sepi, mengikuti WorldClock |

### Keputusan yang sudah disepakati

| Topik | Keputusan |
|---|---|
| Prioritas | B+C dulu (dampak terbesar untuk "rapi" dan "tidak bolong") |
| Sumber aset | CC0. Geometri B+C prosedural (mengikuti garis jalan) dengan tekstur ambientCG CC0 dan tekstur prosedural. Pack model CC0 dipakai mulai sub-proyek D |
| Infill | Rumah fiktif boleh, **hanya** di koridor 15 m dari jalan. Lahan dalam tetap kosong (tertutup tembok) |
| Trotoar | Primary/secondary di dua sisi. Tertiary hanya di **sisi yang menghadap kampus** (≤ 10 m dari poligon `amenity=university`). Sisi lain dan jalan lain pakai bahu semen + selokan |
| Marka | Garis putus-putus tengah di primary/secondary, dan tertiary hanya jika lebarnya ≥ 7 m (di data sekarang: Bulevar Podomoro Land) |
| Vibe | Siang ramai, malam sepi. Spec ini hanya menyiapkan panggungnya; isinya dikerjakan di D/E |
| Branch | `feature/street-frontage` dari `main` |

### Fakta data yang membentuk aturan (snapshot OSM sekarang)

- Ada satu area kampus: Telkom University (way 557649490).
- Tertiary yang menempel kampus:

  | Jalan | Panjang | Jarak minimum ke kampus |
  |---|---|---|
  | Jl. Sukabirus (way 95529338) | 905 m | 1.7 m |
  | Jl. Sukabirus (way 318899777) | 263 m | 6.2 m |
  | Jl. Sukapura (way 43543123) | 215 m | 7.5 m |
  | Tertiary tanpa nama (way 180956121) | 971 m | 4.7 m |

- Jl. Telekomunikasi di dalam area hanya sekitar 250 m, dan jaraknya 72–128 m dari kampus.
- Hampir semua tertiary ber-tag `lanes=2` (lebar 6 m). Karena itu aturan marka memakai lebar, bukan lanes.
- Jalan `service` (235 way):

  | Tag `service` | Jumlah |
  |---|---|
  | tanpa tag | 194 |
  | `drive-through` | 18 |
  | `driveway` | 12 |
  | `parking_aisle` | 8 |
  | `alley` | 3 |

- Tidak ada way `barrier=wall/fence` di area ini, jadi semua tembok bersifat prosedural.

## 2. Ruang lingkup

### Termasuk
- Profil tepi jalan per sisi: trotoar (kerb naik), bahu + selokan, atau tanpa tepi.
- Penanganan simpang lewat prioritas jalan, rantai way (*chain*), dan pemotongan sampel.
- Marka putus-putus tengah.
- Frontage: property line, deteksi lot, tembok/pagar sesuai konteks (rumah, kampus, makam), pilaster, gerbang/pintu, dan plat selokan di depan gerbang.
- Infill rumah di interval frontage kosong (koridor 15 m), memakai `BuildingMeshBuilder` yang sudah ada.
- Penyesuaian posisi lampu dan pohon terhadap tepi jalan, tembok, dan infill.
- Material dan tekstur: ambientCG CC0 (plester, bata), tekstur prosedural (kerb belang, jeruji pagar, atlas tembok), dan material marka.
- Output per chunk (draw call terkendali), collider, `GroundSurface`, culling, chunk hash, dan counter di `GenerationReport`.
- Settings baru beserta migrasi asset yang sudah ada.
- Tes EditMode dan verifikasi integrasi lewat Unity MCP.

### Tidak termasuk (non-goals)
- Mesh simpang khusus (zebra cross, pulau jalan). Simpang tetap berupa pita yang bertumpuk.
- Marka tepi, panah, dan zebra cross.
- Selokan yang benar-benar dalam (carving). Kedalaman selokan hanya visual.
- Pagar yang bisa dibuka. Semua gerbang tertutup dengan collider; hanya gerbang kos (#2) yang interaktif.
- Prop jalanan (tiang listrik, kabel, motor parkir, warung), NPC, dan traffic: masuk sub-proyek D/E.
- Perubahan tampilan bangunan OSM (fasad, atap).
- Mengubah halaman kos (#2). Frontage hanya menghindarinya.

## 3. Arsitektur

### Alur data (di dalam `WorldMeshPipeline.Build`, tetap pure tanpa menyentuh scene/asset)

```
ClassifiedWorld
  → AddRoads (+ marka, submesh 1)
  → RoadNetwork (chain, prioritas, profil per sisi, grid segmen)
  → RoadEdgeBuilder → EdgeRuns + mesh per chunk (RoadEdges)
  → FrontagePlanner pass 1 (tanpa infill) → interval kosong
  → InfillPlanner → rumah infill (BuildingSpec sintetis)
  → FrontagePlanner pass 2 (dengan infill) → wall run, gerbang, plat
  → FrontageMeshBuilder → mesh render + mesh collider per chunk (Frontage)
  → AddBuildings (OSM) + AddInfill (gabung per chunk)
  → AddAreas, AddWater
  → PropPlacer (pohon, lampu) dengan blocker baru
  → ComputeChunkHashes
```

### Unit baru (semua di `Assets/WorldGen/Editor`, namespace `TycoWorld.WorldGen.Authoring`)

| Unit | Tanggung jawab |
|---|---|
| `Settings/StreetDressingSettings.cs` | `RoadEdgeSettings`, `FrontageSettings`, `InfillSettings`: kelas `[Serializable]` dengan field initializer (§12) |
| `Geometry/Polyline2D.cs` | Panjang, titik di jarak s, normal miter, sampel offset, sub-polyline [s0, s1], jarak ray ke ring, bisection |
| `Builders/SolidBox.cs` | Box berorientasi (dari A ke B, tinggi, tebal) ke `MeshData` dengan UV meter atau UV band atlas |
| `Builders/RoadNetwork.cs` | Chain way, prioritas, profil tepi per sampel/sisi, grid segmen jalan untuk query jarak dan "di dalam band jalan lain" |
| `Builders/RoadEdgeBuilder.cs` | Sampling sisi, pemotongan di simpang, run per profil, mesh bahu/selokan/trotoar per chunk |
| `Builders/RoadMarkingBuilder.cs` | Pola dash sepanjang chain, ditekan dekat simpang, dibagi ke mesh way masing-masing |
| `Builders/FrontagePlanner.cs` | Property line, klasifikasi sampel (raycast ke bangunan, konteks area), interval, lot, gaya tembok, gerbang |
| `Builders/FrontageMeshBuilder.cs` | Tembok, coping, pilaster, band pagar dua sisi, gerbang, plat selokan, dan collider box |
| `Builders/InfillPlanner.cs` | Pembagian interval kosong jadi rumah, footprint sejajar jalan, validasi, id sintetis |
| `Art/StreetTextureGenerator.cs` | Tekstur kerb belang, jeruji pagar (alpha), atlas tembok 8 band |

### Perubahan di unit lama

| Unit | Perubahan |
|---|---|
| `Runtime/FeatureKind.cs` | Tambah `RoadEdge`, `Frontage` **di akhir** enum (nilai lama tidak bergeser) |
| `Pipeline/WorldMeshPipeline.cs` | Urutan baru (§3). `FeatureGroups.RoadEdges = "RoadEdges"` dan `FeatureGroups.Frontage = "Frontage"`. `BuiltFeature` dapat `MeshData ColliderMesh` (opsional) dan `bool HasOsmIdentity = true`. `BuiltWorld` dapat `Infill` (daftar spec rumah, untuk tes dan report) |
| `Builders/MeshData.cs` | `Append(MeshData src, int[] submeshMap)` |
| `Pipeline/ChunkAssetWriter.cs` | Menulis mesh collider sebagai sub-asset kedua jika `ColliderMesh` ada. `MeshAsset.Collider` |
| `Pipeline/SceneObjectFactory.cs` | `OsmFeature` hanya jika `HasOsmIdentity`. Collider memakai `MeshAsset.Collider` jika tidak null (cek null eksplisit, bukan `??`), selain itu mesh render |
| `Classify/SurfaceRules.cs` | `Edge_Sidewalk` → Paving; `Edge_Concrete` dan `Edge_Gutter` → Concrete |
| `Builders/PropPlacer.cs` | Offset lampu per profil, blocker tembok/infill, clearance pohon terhadap tepi (§9) |
| `Runtime/Culling/WorldChunkIndex.cs` | `RoadEdges` → `CullCategory.Prop`; `Frontage` → `CullCategory.Structure` |
| `Pipeline/GenerationReport.cs` | Counter baru (§11) |
| `Settings/WorldGenSettings.cs`, `DefaultSettingsFactory.cs` | Field settings baru (§12) |
| `Art/ArtAssetBuilder.cs` | Id CC0 baru, generator tekstur baru, material baru, assign ke settings |

## 4. Profil tepi jalan

Setiap jalan punya dua sisi (kiri dan kanan arah polyline). Profil diputuskan per sampel di sisi itu.

| `highway` | Profil |
|---|---|
| primary, secondary | **Trotoar** |
| tertiary | **Trotoar** jika sisi ini menghadap kampus (aturan di bawah tabel); selain itu **Selokan** |
| unclassified, residential, living_street | **Selokan** |
| service | **Selokan**, kecuali `service` ∈ {driveway, parking_aisle, drive-through} → **Tanpa** |
| pedestrian, footway, path, cycleway, steps, track | **Tanpa** |

**Sisi yang menghadap kampus.** Ambil titik di garis tengah tepi sisi ini, yaitu hw + 0.75 m dari garis tengah jalan, lalu hitung `d` = jaraknya ke poligon Campus (0 jika di dalam). Hitung `d_seberang` dengan cara yang sama untuk titik cermin di sisi seberang. Sisi ini menghadap kampus jika keduanya terpenuhi:
- `d` ≤ `campusSidewalkDistance` (10 m);
- `d < d_seberang − 0.5`, atau kedua titik ada di dalam kampus.

Tanpa syarat kedua, jalan 6 m yang menempel kampus akan mendapat trotoar di kedua sisi, karena sisi seberangnya pun masih dalam 10 m.

Profil diproses per run, dengan aturan berikut:
- Run dengan profil yang sama digabung.
- Run yang lebih pendek dari `minProfileRun` = 8 m mengikuti profil run tetangganya yang lebih panjang. Ini mencegah trotoar dan selokan berganti-ganti dalam jarak pendek.

### Penampang (diukur dari tepi aspal, `hw` = setengah lebar jalan)

**Selokan.** Total lebar `gutterProfileWidth` = 0.80 m, jadi garis gerbang kos (1.19 m dari aspal) tetap di luar tepi.

| Bagian | Rentang offset | Atas (y) | Material |
|---|---|---|---|
| Bahu | hw − 0.05 … hw + 0.30 | 0.055 | `Edge_Concrete` |
| Bibir dalam | hw + 0.30 … hw + 0.38 | 0.155 (muka vertikal dari 0.055) | `Edge_Concrete` |
| Kanal | hw + 0.38 … hw + 0.72 | 0.055 | `Edge_Gutter` |
| Bibir luar | hw + 0.72 … hw + 0.80 | 0.155 (muka luar turun ke 0) | `Edge_Concrete` |

**Trotoar.** Total lebar 1.50 m.

| Bagian | Rentang offset | Atas (y) | Material |
|---|---|---|---|
| Muka kerb | hw (vertikal 0.055 → 0.21) | — | `Edge_Kerb` (belang hitam-putih tiap 1 m) |
| Pinggir kerb | hw … hw + 0.15 | 0.21 | `Edge_Kerb` |
| Badan trotoar | hw + 0.15 … hw + 1.50 | 0.21 | `Edge_Sidewalk` |
| Muka luar | hw + 1.50 (vertikal 0.21 → 0) | — | `Edge_Concrete` |

Selain dua muka di atas, ada **lidah dasar** di bawah aspal: hw − 0.05 … hw pada y 0.055, material `Edge_Concrete`. Lidah ini menutup celah kecil antara aspal dan kerb.

**Tanpa:** tidak ada geometri. Property line jatuh di hw + 0.4 (§7).

**Kerb dan fisika.** Step offset player 0.3 (`PlayerTuning.cs:11`) dan motor 0.3 (`BikeTuning.cs:37`). Box-cast dinding motor dimulai di atas stepOffset (`Motorbike.cs:297`). Jadi kerb 0.155 m (dari aspal residential 0.08 m ke 0.21) dan bibir selokan bisa dilewati tanpa ramp, dan dibuktikan lewat tes (§14).

## 5. Geometri tepi jalan

### Chain
- Dua way dengan `highway` sama yang ujungnya bertemu di titik yang sama (≤ 0.01 m) digabung jadi satu chain, **jika** tidak ada ujung jalan atau vertex jalan lain di titik itu.
- Penggabungan diulang sampai stabil. Id chain = id way terkecil.
- Arah chain dimulai dari unit yang ujungnya bebas (tidak harus way pertama). Way berikutnya dibalik bila perlu.
- Aturan simpang (di bawah) tidak berlaku di antara anggota chain yang sama.

### Prioritas di simpang
Untuk pasangan jalan A dan B: rank lebih tinggi menang; kalau seri, yang lebih lebar menang; kalau masih seri, id chain lebih kecil menang.

### Sampling dan pemotongan
Tiap sisi chain disampel tiap `sampleStep` = 0.5 m sepanjang garis tengah. Sebuah sampel dibuang jika titik dalam band (hw + 0.05) **atau** titik luar band (hw + lebar profil) memenuhi salah satu syarat berikut:

1. **Di dalam band jalan lain B yang menang:** jarak ke garis tengah B < hw_B + lebarTepiMaks_B.
2. **Di mulut jalan lain B yang kalah:** jarak ke garis tengah B < hw_B + 0.3.
3. **Di dalam footprint bangunan OSM.**
4. **Di dalam area air, atau di pita garis air.**
5. **Di luar batas area.**
6. **Lipatan sisi dalam belokan:** jarak ke garis tengah chain sendiri < offset − 0.05. Segmen chain sendiri yang rentang s-nya beririsan dengan [s − offset − 1, s + offset + 1] diabaikan, supaya belokan biasa tidak membuat lubang di sisi dalam. Aturan ini hanya menangkap jalan yang melipat kembali ke dirinya sendiri (putaran U, gang buntu melingkar).

Batas run (sampel dipakai ↔ dibuang) dirapikan dengan bisection 3 langkah (presisi sekitar 6 cm).

### Mesh
- Untuk tiap run yang dipakai, ambil sub-polyline garis tengah [s0, s1] dan offset dengan normal miter. Batas miter mengikuti `RoadMeshBuilder.MiterLimit`; melewati batas itu, bevel.
- Penampang §4 diekstrusi per segmen polyline (bukan per sampel).
- UV dalam meter: u = meter sepanjang jalan, v = melintang/tinggi.
- Segmen dimasukkan ke chunk titik tengahnya. Per chunk ada paling banyak dua `MeshData`, supaya `GroundSurface` tiap objek benar:

  | Objek | Submesh 0 | Submesh 1 | Submesh 2 | Surface |
  |---|---|---|---|---|
  | `RoadEdges/Chunk_x_z/Gutter` | `Edge_Concrete` (bahu, bibir) | `Edge_Gutter` (kanal) | — | Concrete |
  | `RoadEdges/Chunk_x_z/Sidewalk` | `Edge_Sidewalk` (badan) | `Edge_Kerb` (muka + pinggir kerb) | `Edge_Concrete` (lidah, muka luar) | Paving |

  Pembagian ini tidak menambah draw call, karena jumlah pasangan (mesh, material) tetap sama.

- `SplitLongTriangles(100)` dipakai seperti permukaan pijak lainnya.

### Lapisan y (melengkapi spec #1 §7)

| Lapisan | y (m) |
|---|---|
| Garis air | 0.050 |
| **Dasar tepi (bahu, lantai kanal, lidah, kaki kerb)** | **0.055** |
| Jalan | 0.060 + rank × 0.005 |
| **Bibir selokan** | **0.155** |
| **Atas trotoar** | **0.210** |
| **Marka** | **y jalan + 0.003** |
| Tembok, gerbang, infill | dari 0 |

Dasar tepi berada di bawah jalan terendah, jadi di simpang aspal selalu tergambar di atas sisa tepi. Batas cek raycast di spec #1 §15 langkah 4 naik dari y ≤ 0.1 ke **y ≤ 0.25**.

## 6. Marka

- Berlaku untuk chain primary/secondary, dan tertiary dengan lebar ≥ `markingMinWidth` = 7 m.
- Garis putus-putus tengah: lebar 0.12 m, dash 3 m, gap 5 m. Pola dihitung sepanjang chain dan dimulai di s = 2 m.
- Dash dibuang jika ada bagiannya berjarak < hw_B + 3 m dari garis tengah jalan lain B (bukan anggota chain yang sama).
- Tiap dash dipotong di batas way dan dimasukkan ke mesh way pemiliknya sebagai **submesh 1**, material `Road_Marking`.
- Submesh 0 tetap aspal, jadi `SurfaceRules.For` (yang membaca `Materials[0]`) tetap memberi Asphalt. Collider jalan ikut memuat marka; tingginya hanya beda 3 mm, tidak terasa.

## 7. Frontage

> Diperhalus oleh `docs/superpowers/specs/2026-10-14-frontage-polish-design.md`: garis tembok tunggal per sisi chain, gerbang menjauhi tikungan dan tanpa potongan tembok pendek, loop dan pulau bundaran, tembok sambungan profil.

### Property line
- Hanya untuk sisi chain dengan profil **Trotoar** atau **Selokan**.
- Sisi dengan profil **Tanpa** (footway dan sejenisnya, driveway, parking_aisle) tidak punya frontage.
- Letak: offset = hw + lebar profil di titik itu + `propertyLineGap` = 0.4 m. Untuk residential, itu 1.2 m dari aspal.

### Klasifikasi sampel (tiap 0.5 m)
Sampel dibuang (**Blocked**) jika:
- titik property line berada di luar batas area;
- titik itu terlipat di atas jalan sendiri (§5 no. 6);
- jaraknya ke garis tengah jalan lain B < hw_B + lebar tepi maksimum B + 0.4 + 0.15 (tebal tembok).

Aturan terakhir berlaku tanpa memandang prioritas. Di pojok simpang, tembok kedua jalan berhenti di garis belakang tembok jalan lainnya.

Sampel yang tersisa diklasifikasi berurutan; aturan pertama yang cocok yang dipakai:

1. **Air** (titik 0.5 m di belakang garis ada di area air atau pita garis air) → **Kosong**.
2. **Konteks area** di titik 0.5 m di belakang garis:

   | Kategori | Hasil |
   |---|---|
   | Campus | **Pagar kampus** jika garis tengah jalan di luar kampus; jalan di dalam kampus → **Kosong** (kampus terbuka) |
   | Cemetery | **Tembok makam** jika garis tengah jalan di luar makam; selain itu **Kosong** |
   | Parking, Pitch, Farmland, Wood, Grass, Hardground, Paving | **Kosong** |

3. **Zona kos:** titik berjarak < `kosExclusionRadius` = 8 m dari footprint gedung kunci kos (way 409467778) → **Dikecualikan**. Halaman kos (#2) yang mengurus tepi di situ.
4. **Raycast ke belakang** sejauh `corridorDepth` = 15 m terhadap footprint bangunan (OSM + infill pada pass 2):

   | Jarak hit (h) | Hasil |
   |---|---|
   | h < `flushDistance` = 1.0 m | **Flush** (tidak ada tembok) |
   | 1.0 ≤ h ≤ 15 m | **Lot bangunan b** |
   | Tidak ada hit | **Lahan kosong** |

   Jika b adalah gedung kunci yang punya prefab pengganti, hasilnya **Dikecualikan**.

### Interval dan lot
- Sampel berurutan dengan hasil (dan id bangunan) yang sama membentuk satu interval.
- Interval < `minInterval` = 1.5 m digabung ke tetangga yang lebih panjang.
- Pass 1 hanya mencari interval **Lahan kosong** untuk infill (§8).
- Pass 2 (dengan infill) menghasilkan tembok:

  | Interval | Hasil |
  |---|---|
  | Lot bangunan | Tembok sepanjang lot, dengan satu gerbang/pintu dan plat selokan |
  | Lahan kosong (sisa yang tidak terisi infill) | Tembok tanpa gerbang |
  | Pagar kampus, tembok makam | Gaya khusus, menerus, tanpa gerbang (gerbang kampus adalah mulut jalan service yang sudah terpotong oleh aturan simpang) |
  | Flush, kosong, dikecualikan | Tidak ada apa-apa |

### Gaya tembok
Gaya dipilih deterministik dengan `SplitMix64` (seed: id bangunan lot, atau hash (chain, sisi, s awal × 10) untuk lahan kosong):

| Gaya | Porsi | Tinggi | Bentuk |
|---|---|---|---|
| Tembok plester | 55% | 1.6–2.0 m | Box penuh + coping beton 0.05 m |
| Tembok rendah + pagar | 35% | 0.8 m + band pagar 0.9 m | Box 0.8 m, coping, quad jeruji dua sisi (alpha clip) |
| Bata ekspos | 10% | 1.7 m | Box penuh, band atlas bata |
| Pagar kampus | — | 0.6 m + pagar 1.2 m | Seperti "rendah + pagar" |
| Tembok makam | — | 0.7 m | Box penuh, band plester abu |

- Tebal tembok 0.15 m.
- Warna plester: salah satu dari 6 band atlas (hash). Bata: salah satu dari 2 band.
- Pilaster 0.25 × 0.25 m, setinggi tembok + 0.1 m. Dipasang di ujung potongan (selalu), di vertex polyline interior hanya pada tikungan ≥ `pilasterMinTurnDegrees` = 20° (menutupi sambungan box), dan di antaranya tiap ≤ 3 m sepanjang panjang busur potongan. Pilaster yang bukan ujung menjaga jarak ≥ `pilasterMinGap` = 1 m dari pilaster lain (keputusan user di checkpoint 2).

### Gerbang dan plat selokan

| Lebar lot | Bukaan |
|---|---|
| ≥ 4.5 m | Gerbang 2.0 m |
| 2.5 – 4.5 m | Pintu 0.9 m |
| < 2.5 m | Tidak ada |

- Bukaan dipasang di proyeksi centroid bangunan ke property line, dijepit agar ada margin 0.3 m dari ujung lot.
- Daun gerbang: rangka besi (box) + quad jeruji dua sisi, tinggi 1.6 m. Collider box tertutup.
- Di depan setiap bukaan, jika profil sisi itu Selokan, ada **plat selokan**: box beton membentang dari bahu (hw + 0.30) sampai property line (hw + lebar tepi + 0.4), lebar bukaan + 0.4 m, atas di y 0.165. Plat ini ikut collider Frontage.

### Mesh
Satu `MeshData` render per chunk (`Frontage/Chunk_x_z`), dengan submesh:

| Submesh | Isi | Material |
|---|---|---|
| 0 | Tembok | `Frontage_WallAtlas` |
| 1 | Coping, pilaster, plat | `Edge_Concrete` |
| 2 | Jeruji (dua quad berlawanan arah supaya kedua sisi terkena cahaya dengan benar) | `Frontage_Fence` (alpha clip) |
| 3 | Rangka gerbang | `Frontage_GateMetal` |

`ColliderMesh` terpisah, hanya box solid: tembok setinggi penuh (termasuk band pagar), gerbang tertutup, dan plat. Quad alpha satu sisi tidak dijadikan collider.

## 8. Infill

- **Kandidat:** interval **Lahan kosong** pass 1 dengan panjang ≥ `minLotLength` = 6 m.
- **Pembagian:** sepanjang interval, rumah diletakkan berurutan.
  - Lebar acak 6–10 m.
  - Celah 0–0.6 m.
  - Sisa yang lebih pendek dari lebar minimum diserap oleh rumah terakhir di interval itu (rumah bisa lebih lebar dari 10 m, dan bisa < 16 m untuk interval pendek). Kalau interval tidak cukup untuk satu rumah, sisa dibiarkan jadi tembok.
- **Setback:**
  - 35% flush (gaya ruko): muka depan 0.1 m di belakang property line, 2 lantai, `RoofOverride.Flat`.
  - Sisanya: setback acak 1.5–4 m dan `RoofOverride.Auto`.
- **Kedalaman:** acak 8–12 m, dipotong agar sisi belakang ≤ 15 m dari property line.
- **Lantai:** 1 lantai (60%) atau 2 lantai. `LevelHeight` mengikuti settings (3.2 m).
- **Footprint:** persegi panjang yang sejajar dengan tali busur property line di sepanjang lebar rumah.
- **Validasi:**
  - Footprint asli harus berada di belakang property line semua jalan. Untuk jalan sendiri dipakai offset lokal; untuk jalan lain dipakai hw + lebar tepi maksimum + 0.4.
  - Footprint yang diperbesar 0.5 m tidak boleh berpotongan dengan:
    - bangunan OSM atau infill lain yang sudah diterima;
    - air;
    - poligon area mana pun.
  - Footprint tidak boleh berada dalam 8 m dari kos.
  - Semua sudut harus di dalam batas area.

  Jika gagal, coba lagi dengan kedalaman 8 m. Jika tetap gagal, rumah dilewati (dicatat di report).
- **Id:** `−(hash(chainId, sisi, round(s × 10)) & 0x3FFF_FFFF_FFFF_FFFF) − 1`, selalu negatif. Penambahan jalan lain tidak mengubah id rumah yang tidak terkait.
- **Material:** set residential, dengan pick `SplitMix64` yang sama seperti `AddBuildings` (salt ground/upper/roof).
- **Cap:** `maxInfill` = 2500. Kandidat diproses dalam urutan tetap (kunci chain, sisi, s). Setelah cap tercapai, sisa kandidat dilewati dan dicatat di report.
- **Output:** semua rumah di satu chunk digabung jadi **satu** objek `Buildings/Chunk_x_z/Infill`:
  - Kind `Building`, sehingga `PlayerSpawner.IsUnderBuilding` tetap bekerja.
  - Id = hash negatif dari chunk.
  - Satu MeshCollider.
  - Material = gabungan material yang dipakai di chunk itu, dengan submesh dipetakan lewat `MeshData.Append`.
  - `OsmFeature` diberi tag `generated=infill`.

## 9. Props

| Lampu | Offset tegak lurus dari garis tengah |
|---|---|
| Profil Trotoar | hw + 0.45 (di badan trotoar) |
| Profil Selokan | hw + 0.15 (di bahu) |
| Tanpa tepi | hw + 0.6 (tetap) |

Lampu juga ditolak jika:
- berjarak < 0.35 m dari wall run atau bukaan;
- berada di dalam infill (buffer 0.3);
- syarat lama tetap berlaku.

**Pohon** ditolak jika:
- berjarak ke garis tengah jalan < hw + lebar profil + 0.5;
- berjarak < 0.8 m dari wall run;
- berada di dalam infill (buffer 1 m);
- syarat lama tetap berlaku.

Implementasi:
- `PropPlacer` menerima lookup profil dan daftar blocker sebagai parameter opsional.
- Tanpa parameter itu, perilakunya sama persis seperti sekarang, jadi tes lama tetap valid.

## 10. Material dan tekstur

Shader: URP Simple Lit, sama dengan spec #1 §9.

| Material | Sumber | Catatan |
|---|---|---|
| `Edge_Concrete` | ambientCG Concrete034 | Tint B4B2AA (lebih berdebu daripada F0F0F0; keputusan user di checkpoint 1), tile 2 m |
| `Edge_Gutter` | ambientCG Concrete046 | Tint 3A3A36 (gelap, basah), smoothness 0.4 |
| `Edge_Sidewalk` | ambientCG PavingStones070 | Tint E8E8E8, tile 1.5 m |
| `Edge_Kerb` | Prosedural `StreetTextureGenerator.KerbStripes` | 2 blok/m (hitam, putih) + noise kotor |
| `Road_Marking` | Warna putih kusam DCDCD2 | Smoothness 0.2 |
| `Frontage_WallAtlas` | Prosedural atlas 8 band dari ambientCG plester dan bata | Tiap band di-tint, ditambah noda air (gradien gelap dari bawah) dan lumut di 25 cm bawah |
| `Frontage_Fence` | Prosedural `StreetTextureGenerator.FenceBars` | Jeruji vertikal dan dua palang, alpha clip 0.5, cull back (`_Cull: 2`); dua quad berlawanan arah menerangi kedua sisi (§7) |
| `Frontage_GateMetal` | Warna 2B2F33 | Smoothness 0.45 |

- Id ambientCG baru (plester dan bata) dipilih dan diverifikasi saat implementasi. Lisensinya harus CC0.
- Id baru ditambahkan ke `ArtAssetBuilder.Cc0Ids` dan `Assets/WorldGen/Textures/SOURCES.md`, dengan format 1K-JPG Color + NormalGL seperti id lain.
- Nama properti alpha clip dan cull di URP 17.6 Simple Lit (`_AlphaClip`, `_Cutoff`, `_Cull`, keyword `_ALPHATEST_ON`) diverifikasi di dokumentasi sebelum dipakai.

## 11. Output

> Culling: `Frontage` dan `RoadEdges` kini kategori Detail (max(80, 0.4 × view distance)); lihat `docs/superpowers/specs/2026-10-14-frontage-polish-design.md` §2.6.

### Hierarki `World_Generated.unity` (tambahan)

```
World_Generated
├── Roads/Chunk_x_z/…            (jalan; marka di submesh 1)
├── RoadEdges/Chunk_x_z/Gutter   (BARU, Kind RoadEdge, tanpa OsmFeature)
├── RoadEdges/Chunk_x_z/Sidewalk (BARU, Kind RoadEdge, tanpa OsmFeature)
├── Frontage/Chunk_x_z/Walls     (BARU, Kind Frontage, tanpa OsmFeature)
├── Buildings/Chunk_x_z/Infill   (BARU, Kind Building, OsmFeature generated=infill)
└── … (yang lama tidak berubah)
```

### Komponen

| Objek | Collider | `GroundSurface` |
|---|---|---|
| `Gutter` | MeshCollider dari mesh render | Concrete |
| `Sidewalk` | MeshCollider dari mesh render | Paving |
| `Walls` | MeshCollider dari `ColliderMesh` | Tidak ada |
| `Infill` | MeshCollider | Tidak ada (sama seperti bangunan) |

- Semua objek baru diberi static flag Occluder/Occludee, sama seperti yang lain.
- **Culling:** `RoadEdges` → Prop (lantai kecil, boleh hilang lebih dulu). `Frontage` → Structure (tembok membentuk siluet, tidak boleh pop).
- **Hash chunk:** fitur baru dicampur dengan urutan (Kind, OsmId), memakai `Mesh.ContentHash()`, plus `ColliderMesh.ContentHash()` jika ada. Generate dua kali harus identik.

### Counter `GenerationReport`

| Counter | Isi |
|---|---|
| `EdgeMeters.Sidewalk`, `EdgeMeters.Gutter` | Panjang tepi per profil |
| `MarkingDashes` | Jumlah dash |
| `WallMeters` | Panjang tembok, dengan rincian per gaya |
| `Gates`, `Doors`, `GutterSlabs` | Jumlah bukaan dan plat |
| `Infill` | Jumlah rumah infill |
| Skip `infill:blocked`, `infill:cap` | Rumah infill yang dilewati |

## 12. Settings (`WorldGenSettings`)

Tiga kelas `[Serializable]` baru dengan field initializer: `roadEdges`, `frontage`, `infill`. Kalau field itu belum ada di YAML asset lama, Unity memakai nilai initializer saat load, jadi asset yang sudah ada otomatis mendapat default. `DefaultSettingsFactory.Populate` meng-assign instance baru.

### `RoadEdgeSettings`

| Field | Default |
|---|---|
| `sidewalkHighways` | primary, secondary |
| `campusSidewalkHighways` | tertiary |
| `gutterHighways` | tertiary, unclassified, residential, living_street, service |
| `noEdgeServiceValues` | driveway, parking_aisle, drive-through |
| `campusSidewalkDistance` | 10 |
| `minProfileRun` | 8 |
| `mouthMargin` | 0.3 |
| `minRunLength` | 0.5 |
| `sampleStep` | 0.5 |
| `edgeBaseY` | 0.055 |
| `gutterLipY` | 0.155 |
| `sidewalkTopY` | 0.21 |
| `underlap` | 0.05 |
| `shoulderWidth` | 0.30 |
| `gutterWidth` | 0.50 |
| `gutterLipWidth` | 0.08 |
| `sidewalkWidth` | 1.5 |
| `kerbWidth` | 0.15 |
| `markingHighways` | primary, secondary |
| `wideMarkingHighways` | tertiary |
| `markingMinWidth` | 7 |
| `markingWidth` | 0.12 |
| `markingLift` | 0.003 |
| `dashStart` | 2 |
| `dashLength` | 3 |
| `dashGap` | 5 |
| `junctionDashClearance` | 3 |
| `sidewalkLampOffset` | 0.45 |
| `gutterLampOffset` | 0.15 |
| Material | `concrete`, `gutter`, `kerb`, `sidewalk`, `marking` |

### `FrontageSettings`

| Field | Default |
|---|---|
| `propertyLineGap` | 0.4 |
| `corridorDepth` | 15 |
| `flushDistance` | 1.0 |
| `minInterval` | 1.5 |
| `contextProbe` | 0.5 |
| `wallThickness` | 0.15 |
| `copingHeight` | 0.05 |
| `copingOverhang` | 0.02 |
| `pilasterSize` | 0.25 |
| `pilasterSpacing` | 3 |
| `pilasterExtra` | 0.1 |
| `pilasterMinGap` | 1 |
| `pilasterMinTurnDegrees` | 20 |
| `gateWidth` | 2.0 |
| `doorWidth` | 0.9 |
| `gateHeight` | 1.6 |
| `gateMinLot` | 4.5 |
| `doorMinLot` | 2.5 |
| `gateEndMargin` | 0.3 |
| `slabTopY` | 0.165 |
| `slabExtraWidth` | 0.4 |
| `kosExclusionRadius` | 8 |
| `kosBuildingId` | 409467778 |
| `atlasBands` | 8 |
| `atlasTileMeters` | 4 |
| `atlasBandMeters` | 2 |
| Tabel gaya | Porsi dan tinggi sesuai §7 |
| Material | `wallAtlas`, `fence`, `gateMetal` |

### `InfillSettings`

| Field | Default |
|---|---|
| `enabled` | true |
| `minLotLength` | 6 |
| `minWidth`, `maxWidth` | 6, 10 |
| `minDepth`, `maxDepth` | 8, 12 |
| `minGap`, `maxGap` | 0, 0.6 |
| `flushShare` | 0.35 |
| `flushInset` | 0.1 |
| `minSetback`, `maxSetback` | 1.5, 4 |
| `twoStoreyShare` | 0.4 |
| `clearance` | 0.5 |
| `maxInfill` | 2500 |

Tes `SavedAsset_HasStreetDressing` memastikan asset yang tersimpan memuat ketiga bagian dengan nilai default dan materialnya terisi setelah Build Art Assets. Polanya sama dengan `SavedAsset_HasKosRaflesia3`.

## 13. Penanganan error dan kasus tepi

| Kasus | Penanganan |
|---|---|
| Jalan dengan lebar ≤ 0 atau kurang dari 2 titik | Dilewati (sama seperti sekarang) |
| Run tepi lebih pendek dari 0.5 m setelah pemotongan | Dibuang |
| Belokan tajam (miter > batas) | Bevel. Lipatan sisi dalam dibuang lewat aturan §5 no. 6 |
| Material belum di-assign | `SceneObjectFactory.Resolve` memakai fallback dan mencatat warning (sama seperti sekarang) |
| Interval lot di ujung chain yang menabrak batas area | Dipotong di batas area |
| Gedung kunci dengan prefab pengganti | Tidak diberi tembok (dikecualikan) |
| Gedung kunci tanpa prefab | Diperlakukan seperti bangunan biasa |
| Infill gagal validasi | Dilewati dan dicatat. Interval tetap jadi tembok kosong di pass 2 |
| Generate lebih dari 60 s | Dicatat di report. Tidak gagal, tetapi tercatat sebagai risiko di catatan verifikasi |

## 14. Pengetesan

### EditMode (`TycoWorld.WorldGen.Tests`, ditambah tes Player/Vehicles)

**Fixture:** `StreetFixture` dibangun di atas `TestOsm`. Isinya:
- jalan residential;
- jalan tertiary yang menempel area kampus di satu sisi;
- T-junction dengan jalan service;
- driveway;
- rumah setback 4 m dan rumah flush;
- lahan kosong 20 m;
- area taman;
- garis air;
- pengganti kos (id 409467778).

**Settings:**
- default sesuai §12;
- profil per highway dan per nilai `service` sesuai §4;
- `SavedAsset_HasStreetDressing`.

**Polyline2D / SolidBox / MeshData.Append:**
- sampel offset berada di jarak yang benar;
- miter di tekukan benar;
- jarak ray-hit ke persegi benar;
- `Append` menjaga submesh;
- normal box mengarah keluar.

**RoadNetwork:**
- dua way kolinear jadi satu chain;
- urutan prioritas benar;
- driveway → Tanpa;
- sisi tertiary yang menghadap kampus → Trotoar, sisi lain → Selokan;
- run < 8 m ikut tetangga.

**RoadEdgeBuilder:**
- y verteks hanya 0.055, 0.155, atau 0.21 (dan 0 di kaki muka luar);
- tidak ada verteks di dalam band jalan cabang yang menang atau di dalam bangunan;
- footway tidak punya tepi;
- output identik dua kali;
- tidak ada sisi segitiga > 100 m;
- tidak ada NaN.

**RoadMarkingBuilder:**
- jumlah dash di jalan lurus 100 m sesuai pola;
- tidak ada dash dalam radius 3 m dari jalan cabang;
- tidak ada marka di residential atau di tertiary 6 m;
- surface jalan bermarka tetap Asphalt.

**FrontagePlanner:**
- rumah setback 4 m → tembok + satu gerbang di proyeksi centroid;
- rumah flush → tanpa tembok;
- sisi kampus → pagar menerus;
- taman dan air → tidak ada apa-apa;
- zona kos → tidak ada apa-apa;
- mulut simpang → ada celah;
- gaya stabil antar run.

**FrontageMeshBuilder:**
- tinggi tiap gaya benar;
- UV v berada di dalam band warna;
- gerbang punya collider;
- plat ada jika profilnya Selokan;
- jumlah segitiga per meter tembok di bawah batas.

**InfillPlanner:**
- ukuran di dalam rentang;
- tidak overlap dengan jalan, tepi, bangunan, air, area, atau kos;
- id negatif dan unik;
- id stabil saat jalan lain yang tidak terkait ditambah;
- cap dipatuhi.

**Pipeline:**
- fitur baru punya collider;
- `RoadEdges` dan `Walls` tidak punya `OsmFeature`;
- `Infill` ber-tag `generated=infill`;
- `Sukabirus_IsDeterministic` dan `Sukabirus_NoInvalidGeometry` tetap lulus;
- `WorldChunkIndex` memasukkan grup baru dengan kategori yang benar.

**PropPlacer:**
- offset lampu sesuai profil;
- pohon dan lampu menjaga jarak dari tepi dan tembok.

**Fisika:**
- `FirstPersonPlayerTests.Tick_StepsOntoKerb`: player naik ke trotoar y 0.21.
- `MotorbikeTests.Kerb_IsCrossedWithoutBump`: `Bumps == 0`, dan y naik sekitar 0.13–0.155.

### Integrasi (Unity MCP)

1. **Before, di `main`.**
   - Generate, lalu `McpVerify.WriteChunkHashes`.
   - Screenshot di tiga titik, masing-masing jam 10:00 dan 21:00:
     - PreviewCam (-194.6, 1.6, -632.7), yaw 274;
     - spawn kos (-321.16, 1.77, -612.64), yaw 180.8;
     - spawn kos yang sama, yaw 0.
   - Perf: `McpPerfProbe` di kos / jalan / atas, preset Sedang, siang dan malam.
2. **Semua tes EditMode lulus** (`McpTestRunner.Run`).
3. **Build Art Assets → Generate.** Target: 0 error Console, waktu < 60 s. Counter §11 dicatat.
4. **Determinisme.** Generate dua kali → `CompareChunkHashes` IDENTICAL.
5. **Raycast.**
   - 20 ray acak ke bawah, semua kena di y ≤ 0.25.
   - Tepi jalan kos → Concrete.
   - Trotoar sisi kampus Jl. Sukabirus → Paving di y ≈ 0.21.
   - `OverlapBox` di Yard kos tidak menemukan collider Frontage atau Infill.
6. **Top-down** 3 chunk simpang lewat `Unity_SceneView_Capture2DScene`.
7. **After.**
   - Ulangi screenshot dan perf.
   - Motor melewati kerb dengan `McpVehicleDriver`.
   - Player jalan dari spawn kos keluar lewat gerbang kos.

### QA manual (user)
- Jalan kaki dan naik motor di Jl. Sukabirus, gang di dekat kos, dan Jl. Raya Bojongsoang, siang dan malam.
- Nilai "rapi" dan "bolong", dan cari z-fighting atau celah di simpang.

## 15. Kriteria selesai

- Semua tes EditMode lulus. Langkah integrasi 1–7 lulus dan tercatat di `docs/superpowers/notes/2026-10-xx-street-frontage-verification.md`.
- Draw call (preset Sedang) ≤ +15%, dan FPS ≥ 90% dari angka before di titik yang sama.
- Status: **lulus** sesudah frontage polish + perf pass (`docs/superpowers/specs/2026-10-14-frontage-polish-design.md`, kategori cull Detail 200 m di High). Kenaikan batch +7.5% sampai +12.4% dan FPS ≥ 93.9% di keenam titik. Diukur dengan metode baru (rata-rata 90 frame, pembanding berpasangan di sesi yang sama) di preset tersimpan High. Angka "+25–33%" sebelumnya berasal dari pembacaan satu frame yang naik-turun ±15%. Detail: `docs/superpowers/notes/2026-10-14-street-frontage-verification.md`, bagian "Frontage polish + perf pass".
- Visual:
  - tepi jalan punya garis tegas;
  - koridor jalan tertutup tembok atau rumah;
  - tidak ada tembok yang memotong mulut jalan;
  - halaman dan gerbang kos tidak berubah.
- Spec #1 diperbarui: §2 (non-goal trotoar, selokan, marka, dan pagar sekarang dikerjakan), §7 (lapisan y), §8 (lampu dan pohon), §11 (hierarki), §12, §15 (raycast ≤ 0.25).
- Semua perubahan di-commit di `feature/street-frontage`, tanpa `ProjectSettings/ProjectSettings.asset`.

## 16. Risiko

| Risiko | Mitigasi |
|---|---|
| Draw call bertambah (sekitar 8 material baru per chunk di RoadEdges + Frontage) | Mesh digabung per chunk, SRP Batcher + GPU Resident Drawer sudah aktif, `RoadEdges` di-cull lebih dulu. Diukur di langkah integrasi 7; batas +15% |
| Shadow caster dari tembok | Jarak shadow 50 m (`PC_RPAsset`). Jika fps turun, matikan cast shadow di submesh pagar |
| Waktu generate bertambah sekitar 2–4 s (±480k sampel) | Grid spasial untuk semua query. Waktu tiap tahap dicatat di report |
| Z-fighting antara tepi dan jalan di simpang | Dasar tepi 5 mm di bawah jalan terendah, standar yang sama dengan lapisan sekarang |
| Infill menutupi bangunan OSM yang hilang dengan bentuk yang tidak cocok dengan aslinya | Disengaja (keputusan user). Dibatasi koridor 15 m |
| Tembok menutup akses pemain ke gedung yang nanti punya interior | Gedung kunci dengan prefab dikecualikan. Gerbang interaktif bisa ditambahkan per gedung nanti |
| Gerbang kos dan tembok baru bertabrakan | Zona larangan 8 m, dicek dengan `OverlapBox` di langkah integrasi 5 |

## 17. Iterasi berikutnya (di luar spec ini)

- **A:** atmosfer dan kamera (fog siang, grading, audio positional).
- **D:** clutter (tiang dan kabel listrik mengikuti property line, motor parkir di plat selokan, warung di lot flush/ruko, spanduk di tembok).
- **E:** NPC dan traffic memakai graph `RoadNetwork` (chain + simpang) yang dibuat di sini.
- Marka tepi dan zebra cross, gerbang interaktif, pagar kampus dengan gerbang utama yang dibuat manual.
