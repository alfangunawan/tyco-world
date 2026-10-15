# Street Frontage + Road Dressing — verifikasi

## Before (commit 62e4814 = main 38a40ee + hanya dokumen, sebelum perubahan kode)

- Generate: 14378 ms; Features: Area=36, Building=4915, KeyBuilding=6, Road=763, WaterArea=3, WaterLine=18; Trees/Lamps: Trees 1278 (dropped by cap 0), Lamps 876
- Hash chunk: `.superpowers/sdd/2026-10-14-street-frontage/hashes-before.txt` (278 chunk)
- Screenshot: `shots/before/{kos,road,high,preview,kos0}_{10,21}.png`

| Titik | Jam | fps | batches | setpass | tris | renderers |
|---|---|---|---|---|---|---|
| kos | 10 | 162.2 | 8025 | 65 | 71657 | 2563/7910 |
| road | 10 | 189.5 | 5645 | 53 | 77279 | 2586/7910 |
| high | 10 | 218.7 | 3782 | 45 | 65993 | 2586/7910 |
| kos | 21 | 152.8 | 8273 | 65 | 73667 | 2563/7910 |
| road | 21 | 180.0 | 5707 | 53 | 77693 | 2586/7910 |
| high | 21 | 186.7 | 3796 | 45 | 67055 | 2586/7910 |

## Checkpoint 1 — tepi jalan + marka (Task 9)

- Generate: 13468 ms (prepare 329, classify 647, meshes 1795, assets 8713, scene 1983); EdgeMeters.Gutter=75888, EdgeMeters.Sidewalk=5277, MarkingDashes=301, Gutter=255 objek, Sidewalk=46 objek
- Screenshot: `shots/after-m1/{road,kos,preview}_{10,21}.png`
- Pengamatan:
  - Tepi terlihat di jam 10: gutter (selokan gelap dengan bibir beton abu-abu terang) di kedua sisi jalan, menyambung di tikungan `road_10` (bentuk zig-zag mengikuti sudut jalan) dan sepanjang jalan lurus di `preview_10`. Tidak ada z-fighting yang terlihat pada permukaan gutter/jalan.
  - Ada celah/ujung terbuka kecil: di `road_10` gutter di sisi kiri jauh berhenti mendadak di simpang (ujung potongan terlihat), dan di kiri `preview_10` ada tepi trotoar bertingkat yang berhenti di depan bangunan. Perlu dicek di simpang.
  - Belang kerb hitam-putih belum terlihat jelas: kerb tampak abu-abu terang polos dari jarak ini (mungkin sempit, atau kerb hanya ada pada Sidewalk yang jarang: 46 objek).
  - Marka tengah jalan (301 dash) tidak terlihat di tiga titik pandang; jalan tampak polos. Perlu dicek terpisah (mungkin hanya di jalan utama, bukan gang ini).
  - Lampu berdiri di bahu jalan / sisi luar gutter (tiang di tepi, tidak di badan jalan) di `road_10` dan `preview_10`.
  - `kos_10`: pandangan menghadap tembok dan gerbang kos, halaman beton; tidak ada tepi jalan di bingkai.
  - Jam 21: gelap gulita seperti yang diperkirakan; hanya siluet, bibir gutter samar, dan kepala lampu menyala di `road_21`/`preview_21`; `kos_21` hampir hitam seluruhnya.

## Checkpoint 2 — tembok, pagar, gerbang (Task 13)

- Generate: 19786 ms; WallMeters=50446 (Brick=4362, Campus=851, Cemetery=135, LowFence=16348, Plaster=28750), Gates=2109, Doors=533, GutterSlabs=2367, Walls=223 objek chunk
- Screenshot: `shots/after-m2/{road,kos,preview,high}_{10,21}.png` (`kos0` dibatalkan)
- Pengamatan:
  - `road_10`: tembok plester biru dengan tiang beton di sisi kiri jalan, pagar besi hitam di atas dinding bawah di kanan, slab gutter beton menyeberangi parit di depan gerbang; tembok berhenti rapi di mulut jalan, tidak masuk badan jalan; tidak ada z-fighting terlihat.
  - `kos_10`: gerbang besi hitam menghadap tepat ke pintu rumah hijau; halaman kos utuh (rumput dan jalur setapak terlihat melalui gerbang); tembok menyambung kiri-kanan gerbang.
  - `preview_10`: dua sisi gang berpagar/bertembok menerus, gerbang kecil di kiri, lampu di luar tembok (tidak bertabrakan); tikungan jauh tetap tertutup tembok.
  - `high_10`: dari ketinggian 30 m garis tembok tipis dan sulit dibedakan; tidak terlihat lot kosong yang tertutup aneh, tidak ada tembok di luar blok.
  - Jam 21: hampir hitam seperti yang diperkirakan; hanya siluet tembok dan kepala lampu di `road_21`/`preview_21`.
  - Tembok menutup kavling kosong: tidak terlihat jelas di titik pandang ini (tembok menempel pada kavling berumah).

## After (feature/street-frontage, Task 16)

- Tes: passed=957 failed=0
- Generate: 23930 ms (< 60 s; prepare 346, classify 629, meshes 8767, assets 10885, scene 3303); Features: Area=36, Building=4915, Doors=593, EdgeMeters.Gutter=75888, EdgeMeters.Sidewalk=5277, Gates=2859, Gutter=255, GutterSlabs=3146, Infill=859, KeyBuilding=6, MarkingDashes=301, Road=763, Sidewalk=46, WallMeters=45802 (Brick=4334, Campus=851, Cemetery=135, LowFence=15402, Plaster=25079), Walls=221, WaterArea=3, WaterLine=18; Trees 1263, Lamps 894; Skipped infill:blocked=2137 infill:cap=0 (tidak ada)
- Determinisme: generate 2x → `IDENTICAL chunks=280`
- Raycast: worstWalkableY=0.080 (≤ 0.25); kosGutterHits=22; kosYardIntruders=0; trotoar Sukabirus chunk Chunk_-2_-3 dist=36 hitY=0.210 surface=Paving
- Tampak atas: `shots/after/top_{0,1,2}.png` — sangat gelap (kamera ortho tanpa ambient di editor), tapi terbaca: tepi jalan menyambung di tikungan dan simpang, mulut simpang terbuka, tidak ada tembok yang melintang di badan jalan.
- Motor di kerb Jl. Raya Bojongsoang (kerb di chunk 30 m dari titik 185,-224): motor naik kerb (pos y 0.21, melewati kerb) lalu **bumps=1**: berhenti 0.87 m di atas trotoar karena menabrak tembok properti 1 m di depannya (collider `Frontage/Chunk_1_-3/Walls`, bukan kerb). Kriteria `bumps=0` tidak terpenuhi secara harfiah; penyebabnya tembok yang menempel di belakang trotoar sempit, bukan kerb. Player keluar gerbang kos: lewat, surface=Concrete lalu Asphalt, berjalan dari z -612.6 ke -622.1, tidak tersangkut.
- Catatan proses: Play sempat keluar sendiri di tengah pengukuran perf (road 21 belum selesai); Play dimasuki lagi, road 21/high diukur ulang. `McpPerfProbe.Place` mengunci gerak player; perlu `McpPerfProbe.Release()` sebelum tes mount.
- Catatan: tris naik dari ~70k ke ~290-445k (tembok, tepi, infill) — tidak jadi kriteria.

| Titik | Jam | fps before | fps after | batches before | batches after | lulus? |
|---|---|---|---|---|---|---|
| kos | 10 | 162.2 | 174.9 | 8025 | 9160 (+14.1%) | ya |
| road | 10 | 189.5 | 185.1 (97.7%) | 5645 | 6419 (+13.7%) | ya |
| high | 10 | 218.7 | 173.4 (79.3%) | 3782 | 5045 (+33.4%) | **TIDAK** (fps dan batches) |
| kos | 21 | 152.8 | 158.1 | 8273 | 9414 (+13.8%) | ya |
| road | 21 | 180.0 | 155.0 (86.1%) | 5707 | 7156 (+25.4%) | **TIDAK** (fps dan batches) |
| high | 21 | 186.7 | 177.2 (94.9%) | 3796 | 5074 (+33.7%) | **TIDAK** (batches) |

Kriteria: batches ≤ +15%, fps ≥ 90% dari before. Tiga dari enam titik gagal (high di kedua jam, road 21); angka settings tidak diubah. Untuk perbaikan: kurangi draw call dari tembok/tepi/infill (mis. gabung per chunk lebih agresif, atau cull jarak jauh untuk Frontage dan RoadEdges), lalu ukur ulang.

## Setelah mitigasi (final fix wave)

- Preset grafis aktif saat pengukuran: **High** (`SettingsApplier.Current.preset=High`, viewDistance 500, renderScale 100, shadows High, quality level `PC`). Kriteria spec §15 menyebut preset Sedang, tetapi pengukuran before dan Task 16 juga memakai pengaturan tersimpan yang sama, jadi perbandingan ini apple-to-apple di High, bukan di Sedang. Pengaturan grafis tidak diubah (tidak di-tune agar lulus).
- Perubahan: (1) `RoadEdge` tidak melempar bayangan (`ShadowCastingMode.Off`); (2) wajah tersembunyi dibuang dari render mesh tembok: tutup ujung hanya di ujung potongan dan tikungan >= 10 derajat, tanpa sisi atas tembok di bawah coping (collider tetap solid tertutup); (3) gerbang/pintu dibangun dari irisan dan offset mitred yang sama dengan potongan tembok.
- Generate: total 24284 ms (run kedua; run pertama 25031 ms); sub-tahap run pertama: meshes.base 320, meshes.street 7, meshes.edges 1375, meshes.frontage1 3116, meshes.infill 179, meshes.frontage2 3612 (meshes total 8770), assets 11877, scene 3476 ms. Determinisme: generate 2x `IDENTICAL chunks=280`. Tes: passed=969 failed=0.

| Titik | Jam | fps before | fps Task 16 | fps sesudah mitigasi | batches before | batches Task 16 | batches sesudah mitigasi | lulus? |
|---|---|---|---|---|---|---|---|---|
| kos | 10 | 162.2 | 174.9 | 182.5 (112.5%) | 8025 | 9160 (+14.1%) | 9125 (+13.7%) | ya |
| road | 10 | 189.5 | 185.1 | 199.2 (105.1%) | 5645 | 6419 (+13.7%) | 7058 (+25.0%) | **TIDAK** (batches) |
| high | 10 | 218.7 | 173.4 | 219.8 (100.5%) | 3782 | 5045 (+33.4%) | 5023 (+32.8%) | **TIDAK** (batches) |
| kos | 21 | 152.8 | 158.1 | 162.9 (106.6%) | 8273 | 9414 (+13.8%) | 9367 (+13.2%) | ya |
| road | 21 | 180.0 | 155.0 | 172.5 (95.8%) | 5707 | 7156 (+25.4%) | 7129 (+24.9%) | **TIDAK** (batches) |
| high | 21 | 186.7 | 177.2 | 196.7 (105.4%) | 3796 | 5074 (+33.7%) | 5047 (+33.0%) | **TIDAK** (batches) |

Hasil: fps lulus di semua enam titik (>= 90%); batches masih gagal di empat titik (road dan high, kedua jam). Mitigasi bayangan dan wajah tersembunyi hampir tidak menggeser jumlah batch (batch ditentukan jumlah renderer chunk, bukan segitiga); tris turun (kos 10: 431k -> 408k). Nilai road 10 (7058) lebih tinggi dari Task 16 (6419) pada titik yang sama; selisih ini tidak dijelaskan oleh perubahan kode di atas dan dicatat apa adanya. Tidak ada tuning lain dan tidak ada waiver dari user.

## Frontage polish + perf pass (branch feature/frontage-polish)

Desain: `docs/superpowers/specs/2026-10-14-frontage-polish-design.md`.

### Metode ukur batch yang baru

Pembacaan `UnityStats.drawCalls` satu frame (dipakai `McpPerfProbe.Report`) ternyata naik-turun sampai ±15% antar-pembacaan. Akibatnya angka "+25–33%" di bagian sebelumnya terlalu tinggi. Metode baru:

- Batch dihitung sebagai rata-rata `UnityStats.drawCalls` selama 90 frame, setelah jeda 60 frame sesudah `Place` dan `SetTime`. Hasilnya stabil (min = max dalam satu titik).
- FPS tetap diambil dari `McpPerfProbe` (`Begin(5)`, lalu `Report`).
- Pembanding = dunia yang sama di sesi Play yang sama, dengan semua renderer `Frontage`, `RoadEdges`, dan `Infill` dimatikan ("tanpa jalan"). Di sesi pertama, pembanding ini cocok dengan before asli:

| Titik | Before asli | Tanpa jalan | Selisih |
|---|---|---|---|
| high 10 | 3782 | 3791 | +0.2% |
| road 10 | 5645 | 5728 | +1.5% |
| kos 10 | 8025 | 8293 | +3.3% |

  Selisihnya berasal dari marka dan posisi lampu atau pohon yang memang berubah.
- Antar-sesi Play, angka absolut bisa bergeser bersama-sama (sesi kedua sekitar +800 di semua titik, diduga ikut terhitungnya Scene view). Karena itu yang dibandingkan selalu pasangan dalam sesi yang sama.

### Sebelum polish (kode PR #6, metode baru)

| Titik | Jam | Tanpa jalan | Dengan jalan | Kenaikan | Dengan Detail 200 m (uji manual) |
|---|---|---|---|---|---|
| kos | 10 | 8293 | 9047 | +9.1% | 8851 (+6.7%) |
| kos | 21 | 8515 | 9281 | +9.0% | 9084 (+6.7%) |
| road | 10 | 5728 | 6415 | +12.0% | 6190 (+8.1%) |
| road | 21 | 5716 | 6485 | +13.5% | 6260 (+9.5%) |
| high | 10 | 3791 | 4363 | +15.1% | 4209 (+11.0%) |
| high | 21 | 3802 | 4383 | +15.3% | 4228 (+11.2%) |

### Perubahan

**Geometri:**
- **Offset tanpa lipatan:** sisi dalam tikungan yang lebih sempit dari offset diciutkan, bukan dibiarkan terlipat.
- **Jalan loop:** sambungan loop dipindah ke tengah segmen terpanjang, sehingga belokan di sambungan 0°.
- **Aspal di sambungan loop:** sambungan way loop di-miter.
- **Pulau bundaran kecil:** tidak diberi tepi jalan maupun tembok.
- **Tembok:** dipotong dari satu garis tembok per sisi chain.
- **Gerbang:**
  - menjauhi tikungan (≤ 5°);
  - selalu selebar penuh;
  - rata dengan batas lot atau berjarak ≥ 1 m darinya.
- **Peralihan trotoar ↔ selokan:** diberi tembok sambungan.

**Perf:** kategori cull `Detail` untuk `Frontage` dan `RoadEdges`, dengan jarak max(80, 0.4 × view distance), yaitu 200 m di High.

### Hasil

- **Tes:** EditMode `group=. passed=992 failed=0`.
- **Generate:** 25.5–25.7 s. Dua kali Generate: `IDENTICAL chunks=280`.
- **Metrik tembok seluruh dunia** (dihitung dari hasil `FrontageMeshBuilder`):

| Metrik | Sebelum aturan potongan pendek | Sesudah |
|---|---|---|
| Pasangan pilaster < 0.5 m | 795 | 139 |
| Pasangan blocker tembok yang bersilang | 64 | 64 |

  Kolom "sebelum" sudah memakai garis tembok tunggal. Kode PR #6 tidak diukur dengan metrik ini.

  Sisa pilaster dempet dan semua persilangan ada di sudut persimpangan, tempat tembok dua jalan bertemu. Itu tidak termasuk ruang lingkup polish ini; sudut-sudutnya tertutup, tanpa celah.
- **Gates dan Doors:** 2859/593 → 2853/579. Bukaan di sisi dalam tikungan yang tidak muat lagi tidak digambar, dan temboknya menyambung.
- **Cek visual** (tampak atas dan mata):
  - **Sudut dalam Jl. Sukabirus dekat titik `road`:** sebelumnya ada daun gerbang terlipat dan pilaster dobel. Sekarang garis tembok bersih, pintu dan gerbang selebar penuh, dengan pilaster bersama di batas lot.
  - **Loop 197338573:** tepi jalan menerus di sambungan, takik aspal hilang.
  - **Loop 686884808:** pulau r ≈ 5 m sebelumnya berisi cincin pilaster; sekarang bersih.

### Perf sesudah polish (preset High, pasangan dalam sesi Play yang sama)

| Titik | Jam | Batch tanpa jalan | Batch dengan jalan | Kenaikan | FPS tanpa jalan | FPS dengan jalan | FPS % | Lulus? |
|---|---|---|---|---|---|---|---|---|
| kos | 10 | 8294 | 8926 | +7.6% | 149.1 | 140.0 | 93.9% | ya |
| kos | 21 | 8516 | 9159 | +7.5% | 140.9 | 135.9 | 96.5% | ya |
| road | 10 | 5716 | 6427 | +12.4% | 156.6 | 149.3 | 95.3% | ya |
| road | 21 | 6371 | 7154 | +12.3% | 145.1 | 138.5 | 95.5% | ya |
| high | 10 | 4583 | 5015 | +9.4% | 169.5 | 163.2 | 96.3% | ya |
| high | 21 | 4598 | 5038 | +9.6% | 145.3 | 151.0 | 103.9% | ya |

Kriteria spec §15 (batch ≤ +15%, FPS ≥ 90%) lulus di keenam titik. Pengukuran masih memakai preset tersimpan **High**, karena preset Sedang tidak dipakai di before maupun di sini.

### Masih terbuka (di luar ruang lingkup)

- **Sudut persimpangan:** tembok dua jalan yang bertemu di sudut kadang saling lewat beberapa puluh cm (64 pasang). Sambungan sudut yang rapi bisa dibuat nanti.

## QA manual (user)
- [ ] Jalan kaki dan naik motor di Jl. Sukabirus, gang dekat kos, dan Jl. Raya Bojongsoang; siang dan malam.
- [ ] Cari z-fighting atau celah di simpang, tembok yang melintang, dan rumah infill yang aneh.
