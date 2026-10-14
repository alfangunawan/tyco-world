# Frontage Polish + Perf Pass — Design Spec

- **Tanggal:** 2026-10-14
- **Status:** Lanjutan langsung dari PR #6 (street frontage). Isi diminta user lewat goal sesi: "geometry polish" lalu "perf pass", sebelum sub-proyek D.
- **Dasar:** `docs/superpowers/specs/2026-10-14-street-frontage-design.md` (spec induk). Bagian yang tidak disebut di sini tetap mengikuti spec induk.

## 1. Masalah

Final review PR #6 dan cek visual menemukan masalah geometri berikut. Semuanya ada di garis kavling, tempat tiang dan kabel sub-proyek D akan dipasang.

| # | Masalah | Penyebab |
|---|---|---|
| G1 | Celah tembok 1.3–1.6 m di jalan berbentuk loop (way 197338573, 686884808) | Chain loop berawal/berakhir di satu vertex. Offset kedua ujung memakai normal berbeda (tanpa miter), dan jendela abaian `DistanceToChain` tidak membungkus. |
| G2 | Offset sisi dalam terlipat ("swallow-tail") di tikungan tajam bersegmen pendek | `Polyline2D.Offset` tidak membuang loop saat offset > jari-jari lengkung. Strip selokan bertumpuk, kotak tembok bersilang. |
| G3 | Gerbang di sisi dalam tikungan: daun gerbang menyusut sampai ±0, tiang dan pilaster berdempetan, slab bisa terbalik ke aspal | Gerbang ditaruh tanpa melihat vertex tikungan. Rentang gerbang di sisi dalam terlipat. Arah slab diambil dari tali busur rentang yang terlipat. |
| G4 | Pilaster berdempetan di samping gerbang | Akibat G3: kedua ujung gerbang yang terlipat jatuh hampir di satu titik, masing-masing dapat pilaster ujung. |
| G5 | Garis tembok bergeser 0.7 m tanpa penutup di peralihan trotoar ↔ selokan | Dua run berurutan dengan offset garis berbeda tidak disambung. |

Perf (spec induk §15): pembacaan satu frame `UnityStats.drawCalls` ternyata naik-turun ±15%. Rata-rata 90 frame stabil (min = max). Dengan cara itu, kenaikan batch yang sebenarnya sekitar +9 sampai +15%, bukan +25 sampai +33%. Titik `high` masih di batas (+15.2%).

## 2. Keputusan

### 2.1 Loop (G1)

- `RoadChain.Closed`: true bila titik pertama = titik terakhir (≤ 1 cm) dan jumlah titik ≥ 4.
- Saat chain loop dibentuk, titik awalnya **diputar ke tengah segmen terpanjang** (titik tengah disisipkan, ring dibuka di situ). Belokan di sambungan jadi 0°, sehingga offset kedua ujung memakai normal yang sama dan bertemu tepat di satu titik, untuk tepi jalan maupun tembok. Key chain dihitung dari titik awal yang baru, jadi tetap deterministik.
- `RoadNetwork.DistanceToChain` membungkus jendela abaian untuk chain loop (rentang s ± L juga diabaikan).

### 2.2 Offset bebas lipatan (G2)

`Polyline2D.Offset` menambah langkah "collapse":

1. **Ekor:** segmen pertama/terakhir yang arahnya berbalik terhadap segmen centreline sumbernya diciutkan ke titik tetangganya (diulang sampai tidak ada lagi).
2. **Loop:** untuk tiap segmen k, dicari segmen l ≥ k + 2 dalam jendela along ≤ 4·|offset| + 1 yang memotongnya. Kalau ada, titik k+1..l semuanya dipindah ke titik potong. Dipakai l terjauh, lalu pencarian lanjut dari l.

Jumlah titik **tidak berubah**: titik yang diciutkan menjadi segmen nol-panjang. Ini menjaga syarat `RoadEdgeBuilder` bahwa semua offset satu polyline punya jumlah titik sama. Pemakai sudah melewati segmen < 1 mm. `along` tetap monoton.

### 2.3 Gerbang menjauhi tikungan, satu garis tembok per sisi (G3, G4)

- `FrontagePlanner.AddOpening` memilih pusat gerbang S di [lo, hi], sedekat mungkin ke proyeksi centroid bangunan, dengan syarat total belokan centreline di [S − w/2 − 0.3, S + w/2 + 0.3] ≤ `gateMaxTurnDegrees` (5°). Kandidat dicari tiap 0.1 m ke dua arah. Kalau tidak ada kandidat yang lolos, dipakai S lama (clamp).
- **Garis tembok tunggal.** `FrontageMeshBuilder` meng-offset centreline **sekali per (chain, sisi, offset)** dengan collapse §2.2, lalu mengukurnya dengan panjang busurnya sendiri u. s centreline dipetakan ke u lewat interpolasi `along`. Batas run, potongan tembok, dan bukaan semuanya dipotong dari garis ini. Hasilnya:
  - lot yang bertemu tepat setelah sudut dalam berbagi satu titik ujung, tidak saling tumpuk;
  - bukaan selalu selebar w di garis tembok, juga melintasi tikungan (sebelumnya terlipat ke ±0 di sisi dalam);
  - slab selokan tegak lurus garis tembok di tengah bukaan.
- **Tanpa potongan pendek.** Awal bukaan memilih yang paling dekat ke posisi yang diinginkan di antara: rata dengan ujung lot (berbagi pilaster), atau berjarak ≥ `pilasterMinGap` (1 m) dari ujung lot. Sebelumnya margin 0.3 m menyisakan potongan tembok 0.3 m dengan dua pilaster nyaris bersentuhan. `gateEndMargin` kini hanya dipakai planner.
- Pilaster ujung yang jaraknya < `pilasterSize + 0.05` dari pilaster yang sudah ada tidak dipasang lagi (sebelumnya 0.05 m).

### 2.4 Pulau bundaran dan jahitan aspal loop (ditemukan saat verifikasi)

- Sisi dalam chain loop yang inradius-nya (2 × luas / panjang) menyisakan < 2 m di luar pita tepinya adalah pulau lalu lintas. Profilnya None: tidak ada tepi jalan, tidak ada tembok. Planner memperlakukan profil None sebagai Blocked. Contoh: loop 686884808 (r ≈ 5 m) sebelumnya mendapat cincin tembok penuh pilaster.
- `RoadMeshBuilder.AddRibbon` menyambung segmen terakhir dan pertama way loop (titik awal = titik akhir, tanpa ujung bebas) dengan miter. Sebelumnya aspal menyisakan takik rumput berbentuk V di titik awal way.

### 2.5 Penutup lompatan profil (G5)

Dua run pada chain dan sisi yang sama, dengan S1 run pertama = S0 run kedua (≤ 1 mm) dan offset garis berbeda > 5 cm, mendapat **sambungan**: satu kotak tembok lurus dari ujung run pertama ke awal run kedua (gaya, tinggi, dan band run pertama), plus coping, collider, dan blocker.

### 2.6 Perf

- **Culling jarak "Detail":** `CullCategory.Detail` baru. `Frontage` dan `RoadEdges` masuk Detail. Jaraknya `ViewDistanceRules.DetailDistance(view) = max(80, view × 0.4)`, jadi High 200 m, Medium 120 m, Low 80 m. Tembok 2 m di 200 m sudah tertutup fog sekitar 60% dan bangunan. Infill tetap Structure, karena rumah tidak boleh hilang.
- **Pengukuran baru:** batch = rata-rata `UnityStats.drawCalls` selama 90 frame (setelah 60 frame jeda). Pembanding "before" = dunia yang sama dengan Frontage, RoadEdges, dan Infill dimatikan. Angkanya cocok dengan before asli (selisih ±3%).
- Target tetap: batch ≤ +15% dan FPS ≥ 90% dari before di kos, road, dan high, jam 10 dan 21.

## 3. Pengetesan

- `Polyline2D`: offset sisi dalam busur ber-segmen 1 m dengan radius < offset tidak berpotongan sendiri dan jumlah titiknya tetap; ekor terbalik diciutkan; sisi luar tidak berubah.
- `RoadNetwork`: loop satu way punya `Closed`, titik awal di tengah segmen terpanjang, belokan sambungan 0°; `DistanceToChain` loop tidak memblokir titik di dekat sambungan.
- `RoadEdgeBuilder`/`FrontageMeshBuilder` di loop: ujung run di sambungan bertemu (≤ 1 cm), tanpa celah.
- `FrontagePlanner`: lot di tikungan dengan bagian lurus → gerbang di bagian lurus; lot tanpa bagian lurus → S lama.
- `FrontageMeshBuilder`: bukaan terlipat dibuang, tembok menerus; slab mengikuti tangen; pilaster ujung tidak dobel dalam 0.3 m; sambungan profil menutup lompatan 0.7 m.
- Culling: `WorldChunkIndex` memberi Frontage dan RoadEdges kategori Detail; `DistanceCuller` memakai `DetailDistance`; `SettingsApplier` mengisinya.
- Semua tes lama tetap lulus. Generate dua kali IDENTICAL.

## 4. Kriteria selesai

- Tidak ada celah tembok di loop. Tidak ada lipatan di tikungan tajam. Tidak ada gerbang terlipat atau pilaster dobel. Lompatan profil tertutup. Semua dicek lewat tes dan tampak atas di lokasi nyata (loop 686884808, tikungan dekat titik `road`).
- Perf lulus di keenam titik (metode baru), dengan before dan after tercatat di catatan verifikasi.
- Spec induk §7, §11, dan §15 merujuk dokumen ini.
