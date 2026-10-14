# Street Frontage + Road Dressing — verifikasi

## Before (main 38a40ee, sebelum perubahan kode)

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
