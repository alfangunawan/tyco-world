# Verifikasi: jalan bersih dan pembatas jalan buntu (#8, sub-proyek 2)

- **Tanggal:** 2026-10-15
- **Branch:** `feature/kampung-life` (PR #8)
- **Spec:** `docs/superpowers/specs/2026-10-15-road-clearance-dead-ends-design.md`
- **Plan:** `docs/superpowers/plans/2026-10-15-road-clearance-dead-ends.md`

## Aset

| File | Model | Pembuat | Sumber | Lisensi |
|---|---|---|---|---|
| `Kenney_PottedPlant.obj` | Potted Plant | Kenney | https://poly.pizza/m/23Dx9CC95C | CC0 1.0 |
| `Quaternius_BarrierSingle.obj` | Barrier Single | Quaternius | https://poly.pizza/m/wCUxgt2jSP | CC0 1.0 |
| `Quaternius_TrafficBarrier.obj` + atlas | Traffic Barrier | Quaternius | https://poly.pizza/m/cM3aJPU9NS | CC0 1.0 |

- GLB diunduh ke folder scratch masing-masing, lalu dibaca hanya dengan `python -I` (`scratchpad/tools/gltf2obj.py`). Tidak ada yang dieksekusi dari unduhan.
- Dicatat di `Assets/WorldGen/Art/ThirdParty/Barriers/SOURCES.md`.
- Jersey barrier putih Kenney tidak dipakai. Balok Quaternius sudah bergaris kuning-hitam, jadi lebih terbaca sebagai "jalan ditutup".

## Generate

| Hal | Hasil |
|---|---|
| Waktu generate | 35,9 s (budget 60 s) |
| Chunk | 281 |
| Determinisme | Generate dua kali: `IDENTICAL chunks=281` |
| Chunk yang punya pembatas | 98 |

**Counter report:**

| Counter | Nilai |
|---|---|
| `ClearanceShifted` | 558 |
| `ClearanceDropped` | 377 |
| `ClearanceRoadCuts` | 9 |
| `DeadEndWalls` | 84 |
| `DeadEndPortals` | 34 |
| `DeadEndBarriers` | 54 |
| `DeadEndSkipped` | 125 |

`KampungData` di-bake ulang dari jalan yang sudah dipotong: `mapRoads` 593 → 597.

## Pindai ulang jalan

Metode sama dengan spec §1: titik sampel tiap 2 m di semua `mapRoads`, raycast ke bawah, titik tertutup jika yang pertama kena berada di atas 1 m. Titik yang tertutup lalu dicek dengan kapsul setinggi badan (0,5–1,5 m).

| | Sebelum | Sesudah |
|---|---|---|
| Titik sampel | 31.239 | 31.204 |
| Titik tertutup | 274 | 120 |
| Kanopi terbuka di bawahnya | — | 61 |
| Padat setinggi badan | — | 59 |

**Ke-59 titik padat:**

| Penyebab | Kena | Penjelasan |
|---|---|---|
| Prop pembatas (balok, palang) | 50 | Memang menutup ujung jalan |
| Tembok/portal pembatas | 9 | Memang menutup ujung jalan |
| Tembok tepi peta (`Edge_*`) | 38 | Ujung jalan di batas peta; sama-sama ditutup pembatas |
| Tembok frontage | 2 | Di (387.7, -1237.4) dan (0.4, -1121.1). Masalah frontage lama, bukan dari perubahan ini |
| Grha Wiyata A dan B | 2 | Sampel terakhir di ujung potongan jalan, masih dalam toleransi sampling 2 m |
| Pohon | 1 | Pohon di tepi jalan |

Kolom "Kena" dihitung per collider. Satu titik bisa kena lebih dari satu collider, jadi totalnya lebih dari 59. **Tidak ada lagi rumah OSM yang menutup jalan.**

## Bug yang ditemukan di Play

**Tembok menutup jalan di depan gerbang kos.**

- Jalan kos punya ujung buntu 9 m dari kos. Zona larangan kos di frontage hanya 8 m, jadi ujung itu diberi tembok + pot tepat di luar gerbang (`shot_kos.png`, `shot_kos_top.png`).
- **Perbaikan:** `DeadEndSettings.kosRadius = 20` m, terpisah dari radius frontage.
- **Tes:** `EndInFrontOfTheKos_StaysOpen`. Dengan radius 8 m pembatas tetap muncul, dengan 20 m tidak.
- Tembok gang 85 → 84. Ujung jalan kini terbuka ke gerbang kos (`shot_kos_top_after.png`).

**Balok di batas peta menghadap ke belakang, plang terbaca terbalik.**

- Sisi bergaris balok Quaternius ada di +Z model. Balok dipasang dengan yaw jalan, sehingga yang menghadap pemain sisi polosnya.
- Plang punya dua `TextMesh` (depan dan belakang). Shader teks menggambar dua sisi, jadi tulisan belakang tembus dan bertumpuk terbalik (`shot_boundary.png`).
- **Perbaikan:** balok diputar yaw + 180°. Plang hanya punya satu `TextMesh` yang terbaca dari sisi jalan.
- **Tes:** `Props_FaceTheRoad_PerVariant` dan `BarrierPrefabs_AreBuilt_Solid_AndOfASensibleSize`, merah lalu hijau.
- Hasilnya di `shot_boundary2.png`: garis kuning-hitam menghadap jalan, "JALAN DITUTUP" terbaca sekali.

## Pengecekan di Play

Save user dicadangkan dulu, lalu saat Play `SaveBackend = MemorySaveBackend`. Setelah keluar Play, save user dicek: sama persis dengan cadangan.

| Adegan | Hasil |
|---|---|
| Tembok gang (-378.4, -694.5) | Tembok rendah plester melintang selebar jalan, pot di kedua ujung (`shot_wall.png`) |
| Portal (-256.6, -601.4) | Palang kuning-hitam di dua tiang, pas di ujung jalan servis (`shot_portal.png`) |
| Batas peta (-939.0, -600.7) | Dua balok bergaris + palang "JALAN DITUTUP" (`shot_boundary2.png`) |
| Jalan kaki ke tembok gang | Sprint 3 s dari 4 m. Pemain berhenti 0,58 m sebelum pusat tembok, tidak tembus |
| Motor ke portal | Gas penuh dari 20 m. Menabrak di 37,6 km/h, 1 bump, berhenti 1,2 m sebelum pusat portal, tidak tembus |

## Performa

Rata-rata 300 frame, jam 10:00, preset sama dengan pengukuran sebelumnya. `none` = objek gameplay mati, `base` = gameplay aktif. Pengukuran pertama tercemar proses grep di latar belakang, jadi diulang setelah prosesnya dihentikan. Angka di bawah dari pengukuran ulang.

| Titik | Batch sebelum | Batch sesudah | Selisih | FPS sebelum | FPS sesudah |
|---|---|---|---|---|---|
| kos, base | 8517 / 8508 | 8167 / 8142 | −4,2% | 132,4 / 124,9 | 119,3 / 113,3 |
| jalan, base | 6781 | 5896 | −13,1% | 124,7 | 131,3 |
| kos, none | 8133 / 8125 | 7630 / 7630 | −6,1% | 155,3 / 147,3 | 132,1 / 137,3 |
| jalan, none | 6344 | 5525 | −12,9% | 162,8 | 149,9 |

- **Kriteria batch ≤ +3% terpenuhi.** Batch justru turun, karena 377 rumah yang menutup jalan dihapus.
- FPS editor berisik. Di jalan FPS `base` naik, di kos turun sekitar 10%, padahal batch dan segitiga `none` di kos juga turun. Spec tidak menetapkan kriteria FPS. Ini perlu dicek ulang di build player kalau terasa di playtest.

## Tes

- Suite penuh: 1147/0.
- `StreetLampSystemTests.Apply_NeverTouchesSharedHeadMaterial` gagal sekali, hanya jika suite dijalankan dalam perintah editor yang sama langsung setelah `ArtAssetBuilder.BuildAll`. Material kepala lampu masih ditandai dirty sampai update editor berikutnya. Dijalankan ulang di perintah berikutnya: 1147/0. Ini sensitivitas tes yang sudah ada sebelumnya, bukan dari perubahan ini.

## Sisa

- Dua tembok frontage di badan jalan (lihat tabel pindai ulang): di luar lingkup sub-proyek ini.
- Ke-61 titik kanopi (`building=roof`) sengaja dibiarkan. Jalan lewat di bawah atap terbuka.
