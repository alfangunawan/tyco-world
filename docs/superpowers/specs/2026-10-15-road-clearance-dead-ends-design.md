# Jalan bersih dan pembatas jalan buntu (#8, sub-proyek 2): spec desain

- **Tanggal:** 2026-10-15
- **Status:** Permintaan user setelah playtest:
  - "masih banyak rumah yang menghalangi jalanan";
  - "untuk gang/jalan buntu tolong kasih pembatas".
- **Keputusan user:**
  - Rumah digeser atau dihapus. Gedung kampus tetap, dan jalan yang menembusnya diputus lalu diberi pembatas.
  - Bentuk pembatas campuran.
  - Aset pembatas dari pack CC0 yang diunduh, ditambah tembok prosedural.
- **Dasar:** WorldGen (frontage, road dressing) dan Kampung Life (#6).

## 1. Masalah yang diukur

Pindaian di dunia hasil generate: titik sampel tiap 2 m di semua `mapRoads`, lalu raycast ke bawah. Titik dianggap tertutup jika yang pertama kena berada di ketinggian lebih dari 1 m.

| Temuan | Jumlah |
|---|---|
| Titik sampel | 31.239 |
| Titik tertutup bangunan | 274, di 134 lokasi (sel 10 m) |
| Tertutup rumah OSM tanpa nama | 203 |
| Tertutup gedung kampus bernama (Grha Wiyata, FIK, FKS, ...) | 31 |
| Ujung jalan buntu (simpul berderajat 1) | 275 |
| Ujung buntu di batas peta (kurang dari 20 m dari batas) | 70 |
| Ujung buntu di dalam kampung | 205 |

**Penyebab:** footprint rumah OSM sering meleset 1–3 m ke badan jalan. Jalan servis kampus di OSM menembus gedung (lorong atau building passage) yang tidak dimodelkan. Ujung jalan di batas area membuat pemain bisa keluar ke ruang kosong.

## 2. RoadClearance (langkah murni, setelah Classify)

`Assets/WorldGen/Editor/Builders/RoadClearance.cs`. Langkah ini mengubah `ClassifiedWorld` sebelum `Build`, sehingga frontage, infill, props, dan data gameplay ikut melihat hasilnya.

- **Koridor jalan:** garis tengah setiap `RoadFeature`. Jarak bersih = setengah lebar jalan + 0,3 m.
- **Rumah biasa** adalah bangunan yang bukan kampus, tidak punya nama, bukan `KeyBuilding` (kos dan sejenisnya), dan luasnya kurang dari 300 m².
  - **Penetrasi:** jarak bersih dikurangi jarak terdekat footprint ke garis tengah. Kalau garis tengah memotong footprint, rumah dihapus.
  - **Penetrasi 0 sampai 2,0 m:** footprint digeser menjauh dari jalan sejauh penetrasi + 0,05 m, searah dari titik jalan terdekat ke titik rumah terdekat. Setelah digeser, rumah dicek ulang terhadap semua jalan dan semua bangunan lain. Kalau masih melanggar atau jadi menumpuk, rumah dihapus.
  - **Penetrasi lebih dari 2,0 m:** rumah dihapus.
- **Gedung bernama, gedung kampus, atau gedung besar:** gedung tetap. Bagian garis tengah jalan di dalam footprint ditambah jarak bersih dipotong. `RoadFeature` dipecah jadi potongan di luar gedung, dan ujung yang baru menjadi jalan buntu (lihat §3).
- **Determinisme:** urutan diproses menurut id OSM. Tidak ada bilangan acak.
- **Report:** `ClearanceShifted`, `ClearanceDropped`, `ClearanceRoadCuts`.

## 3. DeadEndPlanner (langkah murni, dari RoadNetwork)

`Assets/WorldGen/Editor/Builders/DeadEndPlanner.cs`.

- **Ujung buntu:** ujung chain jalan yang tidak bertemu jalan lain dalam radius sambung RoadNetwork.
- **Dilewati jika:**
  - di dalam area parkir (jalan servis yang masuk lahan parkir);
  - ada footprint bangunan dalam 2 m di depan ujungnya (bangunannya sendiri sudah menutup);
  - ujungnya di halaman kos (zona larangan kos).
- **Jenis pembatas:**

| Ujung | Pembatas |
|---|---|
| Batas peta (kurang dari 20 m dari batas area) | Beton pembatas (jersey barrier) dijejer selebar jalan + plang "JALAN DITUTUP" |
| `service`, `track`, `driveway` | Portal palang besi kuning-hitam di dua tiang |
| Lainnya (gang, residential, living_street, footway, path) | Tembok rendah plester 0,9 m selebar jalan + 2 pot tanaman |

- **Posisi:** pembatas berdiri tegak lurus jalan, 0,3 m sebelum ujung, selebar jalan + tepi (minimal 1,2 m).
- **Collider:** padat, sehingga pejalan kaki, motor, dan mobil tidak bisa lewat. Portal juga tertutup.
- **Report:** `DeadEndWalls`, `DeadEndPortals`, `DeadEndBarriers`, `DeadEndSkipped`.

## 4. Aset

- **Download CC0:** model Kenney (atau sejenisnya) dari Poly Pizza, yang sudah terbukti bisa diunduh langsung dalam GLB. Yang dibutuhkan: beton pembatas, palang portal (atau tiang + palang), plang jalan, pot tanaman.
  - Konversi GLB ke OBJ dengan `scratchpad/tools/glb2obj.py`.
  - Dicatat di `Assets/WorldGen/Art/ThirdParty/SOURCES.md` (atau di folder ThirdParty yang sudah ada).
  - Unduhan diperlakukan sebagai data tidak tepercaya, sama seperti di #7.
- **Prosedural:** tembok rendah memakai atlas tembok frontage, supaya seragam dengan tembok rumah.
- **Fallback:** kalau satu model tidak ditemukan, dipakai bentuk prosedural sederhana (kotak bergaris kuning-hitam), dan report mencatatnya.

## 5. Output dan data gameplay

- Pembatas digabung per chunk di grup `Barriers/Chunk_x_z`, kategori cull Prop, dengan collider padat (box).
- `KampungData` di-bake ulang dari jalan yang sudah dipotong (`mapRoads` dan `walkLanes`). Rute GPS dan jalur pejalan kaki tidak lagi menembus gedung.
- Urutan `Build()`:
  1. RoadClearance (sebelum Build);
  2. roads;
  3. RoadNetwork;
  4. edges;
  5. frontage dan infill;
  6. buildings;
  7. areas;
  8. barriers;
  9. props (pembatas jadi penghalang);
  10. hashes.

## 6. Pengetesan

- **RoadClearance (fixture `StreetFixture`):**
  - rumah 1 m masuk jalan digeser keluar;
  - rumah 3 m masuk dihapus;
  - rumah yang dipotong garis tengah dihapus;
  - gedung bernama tetap dan jalannya terpotong jadi dua;
  - rumah yang setelah digeser menumpuk rumah lain dihapus;
  - hasil identik dua kali.
- **DeadEndPlanner:**
  - jenis pembatas menurut highway dan batas peta;
  - dilewati jika di area parkir atau di depan bangunan;
  - tegak lurus jalan;
  - lebarnya menutup jalan.
- **Fisika:** collider pembatas menghalangi `CharacterController` pemain dan motor.
- **Pipeline:** `Sukabirus_IsDeterministic` dan `Sukabirus_NoInvalidGeometry` tetap lulus.
- **Verifikasi:**
  - pindai ulang: titik tertutup bangunan di dalam kampung menjadi 0 (sisa yang ada harus dijelaskan);
  - Generate dua kali menghasilkan hash identik;
  - screenshot tiap jenis pembatas;
  - perf: batch ≤ +3%.
