# Verifikasi: karakter dan animasi (#7)

- **Tanggal:** 2026-10-15
- **Branch:** `feature/kampung-life` (PR #8)
- **Spec:** `docs/superpowers/specs/2026-10-15-characters-animation-design.md`

## Aset

| Aset | Sumber | Lisensi |
|---|---|---|
| Animasi (45 klip) | Quaternius Universal Animation Library (Standard), OpenGameArt | CC0 |
| 6 model pria, 4 model wanita | Quaternius Ultimate Modular Men / Women, zip Google Drive diunduh user lewat browser | CC0 |
| Galon dan dispenser | "Water Cooler" oleh J-Toastie, Poly Pizza | CC-BY 3.0, atribusi di `SOURCES.md` |

Kedua zip dibaca hanya dengan `python -I` di folder scratch. Yang disalin ke `Assets/` hanya FBX dari folder `Humanoid Rig/Individual Characters/FBX` dan `License.txt`. File `.blend` dan `.gltf` tidak dipakai. Zip asli masih ada di root proyek: belum di-track dan tidak di-commit.

## Hasil impor

| Model | Segitiga | Submesh setelah digabung |
|---|---|---|
| Men_Beach | 6578 | 8 |
| Men_Casual | 6206 | 7 |
| Men_Casual2 | 5776 | 9 |
| Men_Farmer | 5476 | 8 |
| Men_Suit | 6634 | 8 |
| Men_Worker | 5240 | 11 |
| Women_Adventurer | 6800 | 9 |
| Women_Casual | 6396 | 7 |
| Women_Formal | 6130 | 5 |
| Women_Suit | 6494 | 6 |

Semua model menghasilkan avatar humanoid yang valid. Segitiganya kira-kira setengah dari manekin UAL (13,7k).

## Bug yang ditemukan di Play

**Badan menghadap ke belakang.** Di Play, penjaga warung membelakangi pemain. Pengukuran per orang menunjukkan:

- Model Modular menghadap +Z saat bind pose, jadi tidak di-flip.
- Badan yang sedang dianimasikan menghadap -Z (dot dengan arah pemegang -0,95).
- Badan yang animatornya ter-cull masih dalam T-pose dan menghadap +Z.

**Penyebab:** klip UAL diimpor dengan `keepOriginalOrientation = true`, dan klip itu dibuat menghadap -Z.

**Perbaikan:**

- Klip diimpor berdasarkan orientasi badan.
- Flip bind pose di `CharacterFactory.Normalise` dihapus.
- Tes baru mengevaluasi animator setiap model, dengan syarat lengan sudah turun dari T-pose, lalu mengecek arah badannya.

Setelah diperbaiki, 8 dari 8 badan yang sedang dianimasikan menghadap ke depan.

## Pengecekan di Play

Save user dicadangkan dulu, lalu saat Play `SaveBackend = MemorySaveBackend`. Setelah keluar Play, save user dicek: sama persis dengan cadangan.

| Adegan | Hasil |
|---|---|
| Penjaga warung | `Women_Formal` menghadap pemain. Kulit sawo matang, kepang merah ditint hitam (`modular_keeper2.png`) |
| Pejalan kaki | `Men_Worker` berjalan dengan animasi jalan, menghadap arah jalannya (`modular_walker2.png`). Wanita berjas dan wanita bertas punggung juga terlihat bersih setelah mesh digabung (`merged_walker.png`) |
| Ojek | Penumpang (`Men_Farmer`) duduk di jok belakang menghadap depan. Badan pemain (`Men_Beach`) mengendarai motor di kamera orang ketiga (`modular_ojek.png`) |
| Galon | Pemesan (`Women_Adventurer`) memegang galon di depan dada, lalu berbalik ke gerbang. Uang +Rp 15.000 (`modular_galon_handover.png`) |

## Performa

**Metode:**

- Rata-rata 300 frame, diukur lewat `EditorApplication.update`.
- "none" berarti `Gameplay/Objects` dimatikan. "base" berarti semuanya hidup, dan pengukuran menunggu 700 frame dulu supaya jalan terisi orang.
- Jam 10.00, saat siang ramai.

| Langkah | Titik | Batch none → base | Kenaikan | FPS none → base | Segitiga none → base | Orang |
|---|---|---|---|---|---|---|
| 4 bagian per orang | kos | 8135 → 8874 | +9,1% | 137,0 → 114,6 (84%) | 210k → 536k | 34 |
| 4 bagian per orang | road | 6345 → 6628 | +4,5% | 133,1 → 130,1 (98%) | 173k → 239k | 36 |
| Digabung + tanpa bayangan jauh | kos | 8133 → 8517 | **+4,7%** | 155,3 → 132,4 (85%) | 210k → 442k | 34 |
| Digabung + tanpa bayangan jauh | road | 6344 → 6781 | **+6,9%** | 162,8 → 124,7 (77%) | 173k → 384k | 36 |
| Digabung (ulang) | kos | 8125 → 8508 | **+4,7%** | 147,3 → 124,9 (85%) | 201k → 423k | 34 |

**Kesimpulan:**

- **Batch lulus** di semua titik. Batas spec +10%. Dengan model terpisah 4 bagian, titik kos sempat +9,1%.
- **FPS dengan gameplay** 125–132 di editor, atau 77–85% dari kondisi tanpa gameplay.
  - Turun dibanding orang primitif (92–98%). Bebannya 34–36 animator humanoid yang aktif dalam jarak 40 m.
  - Angka FPS "none" berisik (137–163 di titik yang sama), jadi persentasenya juga ikut berisik.
- **Kalau FPS kurang di mesin user,** ada dua tuas: kurangi jumlah pejalan kaki siang (`PedestrianSchedule.Day`), atau perkecil `PedestrianSystem.DetailRange` (40 m).

## Tes

- Gameplay 111/0.
- Suite penuh 1108/0.
- **Tes baru:**
  - arah hadap setelah animasi;
  - satu skinned mesh per model;
  - pemisahan pria/wanita dan tint kulit/rambut;
  - LOD bayangan pejalan kaki.
