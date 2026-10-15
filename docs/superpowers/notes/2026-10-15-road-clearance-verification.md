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
| Waktu generate | 37,9 s (budget 60 s) |
| Chunk | 281 |
| Determinisme | Generate dua kali: `IDENTICAL chunks=281` |
| Objek chunk pembatas | 154 (termasuk chunk yang hanya berisi collider pembatas batas peta) |

**Counter report:**

| Counter | Nilai |
|---|---|
| `ClearanceShifted` | 558 |
| `ClearanceDropped` | 377 |
| `ClearanceRoadCuts` | 9 |
| `DeadEndWalls` | 132 |
| `DeadEndPortals` | 48 |
| `DeadEndBarriers` | 70 |
| `DeadEndSkipped` | 43 |

Angka ini setelah perbaikan review akhir (lihat bagian di bawah). Sebelumnya 84 / 34 / 54 / 125: 82 ujung dilewati hanya karena ada rumah di samping ujung, dan 4 ujung lollipop dianggap buntu.

`KampungData` di-bake ulang dari jalan yang sudah dipotong: `mapRoads` 593 → 597.

## Pindai ulang jalan

Metode sama dengan spec §1: titik sampel tiap 2 m di semua `mapRoads`, raycast ke bawah, titik tertutup jika yang pertama kena berada di atas 1 m. Titik yang tertutup lalu dicek dengan kapsul setinggi badan (0,5–1,5 m).

| | Sebelum | Sesudah |
|---|---|---|
| Titik sampel | 31.239 | 31.204 |
| Titik tertutup | 274 | 127 |
| Kanopi terbuka di bawahnya | — | 61 |
| Padat setinggi badan | — | 66 |

**Ke-66 titik padat** (rincian di bawah dari pindaian sebelum review akhir, saat masih 59 titik; setelah review, pembatas bertambah, dan penyebab lainnya tetap sama):

| Penyebab | Kena | Penjelasan |
|---|---|---|
| Prop pembatas (balok, palang) | 50 | Memang menutup ujung jalan |
| Tembok/portal pembatas | 9 | Memang menutup ujung jalan |
| Tembok tepi peta (`Edge_*`) | 38 | Ujung jalan di batas peta; sama-sama ditutup pembatas |
| Tembok frontage | 2 | Di (387.7, -1237.4) dan (0.4, -1121.1). Masalah frontage lama, bukan dari perubahan ini |
| Grha Wiyata A dan B | 2 | Sampel terakhir di ujung potongan jalan, masih dalam toleransi sampling 2 m |
| Pohon | 1 | Pohon di tepi jalan |

Kolom "Kena" dihitung per collider. Satu titik bisa kena lebih dari satu collider, jadi totalnya lebih dari jumlah titik. Setelah review akhir: prop pembatas 75, tembok/portal/collider pembatas 42, tembok tepi peta 40, frontage 2, Grha Wiyata 2, pohon 1. **Tidak ada lagi rumah OSM yang menutup jalan.**

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

**Setelah review akhir** (pembatas bertambah ~80), batch: kos base 7742 / 8273, jalan base 5967. Masih di bawah angka sebelum perubahan. FPS di pengukuran ini 88–116, turun di semua titik termasuk `none`, sementara dua proses worker Unity sedang aktif. Karena itu angka FPS ini tidak dipakai.

- **Kriteria batch ≤ +3% terpenuhi.** Batch justru turun, karena 377 rumah yang menutup jalan dihapus.
- FPS editor berisik. Di jalan FPS `base` naik, di kos turun sekitar 10%, padahal batch dan segitiga `none` di kos juga turun. Spec tidak menetapkan kriteria FPS. Ini perlu dicek ulang di build player kalau terasa di playtest.

## Tes

- Suite penuh: 1147/0 sebelum review akhir, 1154/0 sesudahnya.
- `StreetLampSystemTests.Apply_NeverTouchesSharedHeadMaterial` gagal sekali, hanya jika suite dijalankan dalam perintah editor yang sama langsung setelah `ArtAssetBuilder.BuildAll`. Material kepala lampu masih ditandai dirty sampai update editor berikutnya. Dijalankan ulang di perintah berikutnya: 1147/0. Ini sensitivitas tes yang sudah ada sebelumnya, bukan dari perubahan ini.

## Sisa

- Dua tembok frontage di badan jalan (lihat tabel pindai ulang): di luar lingkup sub-proyek ini.
- Ke-61 titik kanopi (`building=roof`) sengaja dibiarkan. Jalan lewat di bawah atap terbuka.

## Review akhir

Reviewer membaca seluruh cabang (84cc08a..49413cf). Temuan yang diperbaiki, masing-masing dengan tes yang merah lebih dulu:

| Temuan | Perbaikan | Tes |
|---|---|---|
| Ujung jalan yang kembali ke batangnya sendiri (bentuk lollipop atau "P") dianggap buntu, sehingga ada tembok di simpang yang masih dipakai rute GPS | Ujung juga dicek terhadap jalannya sendiri, di luar busur dekat ujung itu | `EndThatLoopsBackOntoItsOwnRoad_IsNotADeadEnd` |
| Rumah di samping ujung dianggap menutup ujung. Gang yang berakhir ke tanah kosong tidak diberi pembatas | Sinar lurus ke depan dari selebar jalan. Ujung dianggap tertutup hanya jika bangunan menahan semua sinar, kecuali celah lebih sempit dari 0,7 m. Atap terbuka (`building=roof`) tidak dihitung | `HouseBesideTheEnd_WithOpenGroundAhead_StillGetsAWall`, `OpenCanopyAhead_DoesNotCloseTheEnd` |
| Chunk yang hanya berisi pembatas batas peta kehilangan collider buatan kode, karena fitur chunk dibuat dari mesh render saja | Fitur chunk dibuat juga untuk chunk yang hanya punya collider. Writer dan factory membuat objek collider tanpa renderer | `MapEdgeBarrier_GetsItsCollider_InAChunkWithNothingElseBuilt`, `ColliderOnlyFeature_IsWritten_AndBecomesAnInvisibleCollider` |
| Balok batas peta dihitung dengan `floor`, sehingga ada celah ±0,9 m di samping satu balok pada jalan 3,7 m | Jumlah balok `ceil((lebar − 0,6) / 1,9)`. Celah total paling lebar 0,6 m | `Boundary_BlocksCoverTheWholeRoad` |
| Tes art menimpa prefab proyek dengan papan bermaterial default (abu-abu) | Tes membangun prefab di folder sementara | `SavedRoadClosedPrefab_BoardUsesTheBoardMaterial` |

Pengecekan di dunia setelah perbaikan:

- Dua simpang lollipop yang ditunjuk reviewer, (166, −563) dan (280, −1034), tidak lagi punya collider atau prop pembatas dalam radius 6 m.
- Tembok baru di (−658.6, −741.0): jalan dulu berakhir ke rumput dengan rumah di sampingnya. Sekarang ditutup tembok + pot (`new_wall_0.png`).
- Generate dua kali: `IDENTICAL chunks=281`.
