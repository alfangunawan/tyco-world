# Kampung Life (#6) — verifikasi (2026-10-15)

Spec: `docs/superpowers/specs/2026-10-15-kampung-life-design.md` (§9 mencatat keputusan yang diambil saat membangun). Branch `feature/kampung-life`, ditumpuk di atas `feature/frontage-polish` (PR #7).

## Tes otomatis

- `McpTestRunner.Run("TycoWorld")`: **1075 lulus, 0 gagal** (997 dari PR #7, ditambah 78 tes Gameplay). Sebelum mesh low-poly dan optimasi: 1071/0.
- Generate setelah semua perubahan: 32 s, hash 280 chunk **IDENTIK** dengan akhir PR #7. Re-bake `KampungData` lewat Gameplay Setup tidak mengubah asset (deterministik).
- Isi tes Gameplay:
  - **Model murni:** Rupiah, dompet, tas, kebutuhan, jam buka, simpan/muat dan versi tak dikenal, panas (naik, tertahan saat terlihat, turun satu bintang per 25 s), langkah dan jendela jam misi, pilihan jalur di node, dan jadwal kepadatan.
  - **Bake:** toko, parkir, pintu, landmark, dan jalur jalan kaki dari `StreetFixture`.
  - **Komponen dengan fixture:**
    - beli di etalase dan toko tutup malam;
    - tidur, uang jajan, pingsan, dan lapar mengunci lari;
    - HP;
    - pejalan kaki: memenuhi jalan di siang hari, kosong di malam hari dengan sosok yang hilang saat didekati, mengikuti jalur, disapa, serta saksi dan pengejar;
    - enam misi dari awal sampai dibayar, dibatalkan, waktu habis, dan pingsan;
    - Pak RT;
    - parkir yang muncul dan pergi;
    - bobol kunci tanpa saksi, ketahuan lalu tertangkap, denda, hoodie, alarm mobil, kabur membatalkan bobol, panas mendingin, dan cat ulang;
    - mesh low-poly.

## Play lewat MCP (dunia nyata, preset tersimpan High)

| Cek | Hasil |
|---|---|
| Mulai baru | Rp 50.000, Lapar 80, Energi 90; toast sambutan; 70 toko dibangun. |
| Belanja | Warung Teh Euis (31 m dari kos) menampilkan "[E] Belanja di Warung Teh Euis". Beli roti: Rp 50.000 → 44.000, roti masuk tas. Jendela toko, HUD (uang dan kebutuhan di kanan atas di bawah jam), dan peta di HP tampil benar (`shop_ui.png`, `phone_map.png`). |
| Pejalan kaki siang | 26 orang aktif dalam beberapa detik, berjalan di bahu jalan (`people_street.png`). |
| Malam | Pukul 23.00 jalan menyusut ke 3–4 orang. Sosok muncul di jalur di depan pemain. Malam sangat gelap, sesuai tema horor. |
| Antar Galon | Diambil dari HP. Beacon, objektif, dan kompas "14 m" tampil (`galon_1.png`). Galon diambil di warung (tas `galon x1`, tidak bisa lari), diserahkan ke pemesan di gerbang: **Rp 50.000 → 65.000**. |
| Suara dari Kuburan | Jam 23.00, kuburan 84 m. Tiba memicu "Cari sumber suara"; radio baru tampil dalam 22 m (16 m, beacon menyala); radio dimatikan → langkah "Pulang ke kos". |
| Pak RT | Malam hari berdiri di pos ronda (-297, -686). |
| Mencuri mobil di depan warga | Saksi 9 m: panas 2 bintang, 2 pengejar, tertangkap. Denda diambil semua yang ada (Rp 50.000), diantar ke kos (-321,2, -612,6). |
| Mencuri motor (prefab) di depan warga | HUD "MALING! [*--]", progress "Membobol kunci motor...", toast "Warga: MALING! MALING!" (`bike_prefab.png`). Pemain diam di tempat, jadi tertangkap. |
| Mencuri motor jam 02.00 tanpa saksi | Panas 0. Setelah 3 s pemain duduk di `WargaBike`. Di sini ketemu bug: flag `Hot` dari percobaan sebelumnya tertinggal. Sudah diperbaiki dan dipaku tes. |
| Angkot | Mobil hijau primitif di tepi jalan, terlihat seperti angkot (`car_road.png`). |

## Performa

Metode sama dengan PR #7:

- Batch = rata-rata `UnityStats.drawCalls` selama 90 frame.
- FPS dari `McpPerfProbe` (`Begin(5)`).
- Pembanding dalam sesi Play yang sama: "none" = root `Gameplay/Objects` dimatikan (tanpa toko, orang, kendaraan, dan objek misi); "base" = semuanya hidup.
- Langkah "base" menunggu 700 frame supaya jalan sempat terisi di titik pandang baru.

Kriteria spec §8: batch ≤ +10%.

### Sweep 1 (primitif Unity)

| Titik | Jam | Batch none | Batch base | Kenaikan | FPS none | FPS base | FPS % | Segitiga none → base |
|---|---|---|---|---|---|---|---|---|
| kos | 10 | 8926 | 9172 | +2,8% | 134,4 | 122,5 | 91% | 225k → 491k |
| kos | 21 | 9160 | 9347 | +2,0% | 124,9 | 118,0 | 94% | 236k → 321k |
| road | 10 | 6843 | 7052 | +3,1% | 138,7 | 131,3 | 95% | 189k → 306k |
| road | 21 | 6909 | 7079 | +2,5% | 131,4 | 120,8 | 92% | 195k → 220k |
| high | 10 | 4869 | 4921 | +1,1% | 153,2 | 143,2 | 93% | 169k → 182k |
| high | 21 | 4893 | 4935 | +0,9% | 130,9 | 137,2 | 105% | 177k → 178k |

Batch lulus di semua titik. Segitiga di kos siang naik dua kali lipat, karena setiap orang terdiri dari 7–8 bola/kapsul bawaan Unity yang masing-masing berisi ±800 segitiga. Karena itu `LowPolyMeshes` dibuat: bentuk sama, 12 segmen, dengan 120/144/48 segitiga per bola, kapsul, dan silinder.

### Sweep 2 (mesh low-poly)

Segitiga di kos siang turun dari +266k ke +47k, tetapi FPS siang masih 86–90%. FPS siang paling terpengaruh, jadi bebannya CPU pejalan kaki (26 orang), bukan GPU.

| Titik | Jam | Batch none | Batch base | Kenaikan | FPS none | FPS base | FPS % |
|---|---|---|---|---|---|---|---|
| kos | 10 | 8926 | 9170 | +2,7% | 130,8 | 112,3 | 86% |
| kos | 21 | 9161 | 9348 | +2,0% | 121,4 | 123,1 | 101% |
| road | 10 | 6843 | 7059 | +3,2% | 139,3 | 125,2 | 90% |
| road | 21 | 6908 | 7084 | +2,5% | 127,2 | 125,6 | 99% |
| high | 10 | 4869 | 4921 | +1,1% | 144,9 | 138,4 | 96% |
| high | 21 | 4893 | 4935 | +0,9% | 142,4 | 126,1 | 89% |

### Sweep 3 (low-poly + LOD pejalan kaki + kendaraan parkir tidur) — hasil akhir

Perubahan:

- **Pejalan kaki:** orang lebih dari 40 m dari pemain hanya di-step tiap 4 frame (dengan dt terkumpul) dan anggota badannya tidak diayun. Lengan, rambut, hijab, dan kerudung tidak membuat bayangan.
- **Kendaraan:** kendaraan parkir mematikan komponen `Motorbike`-nya (tanpa CharacterController.Move per frame) sampai ada yang naik. Kolom awake=0 menunjukkan semua kendaraan di semua titik sedang tidur.

| Titik | Jam | Batch none | Batch base | Kenaikan | FPS none | FPS base | FPS % | Segitiga none → base |
|---|---|---|---|---|---|---|---|---|
| kos | 10 | 8926 | 9183 | **+2,9%** | 133,7 | 125,2 | 94% | 225k → 270k |
| kos | 21 | 9161 | 9347 | **+2,0%** | 124,7 | 117,9 | 95% | 236k → 254k |
| road | 10 | 6843 | 7047 | **+3,0%** | 134,3 | 128,6 | 96% | 189k → 212k |
| road | 21 | 6908 | 7081 | **+2,5%** | 126,3 | 115,9 | 92% | 195k → 205k |
| high | 10 | 4869 | 4921 | **+1,1%** | 140,7 | 135,4 | 96% | 169k → 172k |
| high | 21 | 4892 | 4935 | **+0,9%** | 133,3 | 131,2 | 98% | 177k → 178k |

Hasil:

- **Batch lulus** di keenam titik, dengan kenaikan maksimal +3,0% (batas spec +10%).
- **FPS** 92–98% dari kondisi tanpa gameplay.
- **Malam:** ada dua lampu titik tambahan, yaitu lampu minimarket yang buka.
- **Catatan pengukuran:** FPS saat editor tidak fokus cukup berisik. Contohnya high 21 tercatat 105% di sweep 1 dan 89% di sweep 2, dengan frame terburuk sampai ±120 ms di langkah "none". Sebaliknya, angka batch stabil antar-sweep (selisih ≤ 15).

## Catatan

- Membobol kunci tidak mengunci kaki. Melangkah lebih dari 0,6 m membatalkannya. Kalau pemain tetap diam di depan saksi, ia pasti tertangkap: saksi 9 m sampai dalam ±1,7 s, sedangkan membobol butuh 3–4 s. Ini disengaja.
- Pengejar (5,3 m/s) sedikit lebih lambat dari lari pemain (5,5 m/s), jadi pemain bisa lolos selama stamina masih ada.
- Interactor menembak dari tinggi mata. Motor yang rendah perlu dilihat sedikit ke bawah supaya prompt muncul, sama seperti motor kos.

## Checklist QA manual (input asli)

- [ ] Tab membuka/menutup HP; tab Dompet, Tas (makan/minum, ganti baju), Kerjaan (perlu paket data dari konter), Peta (zoom +/−).
- [ ] E di etalase membuka toko; E atau tombol Tutup menutupnya; Esc tetap membuka menu jeda.
- [ ] Sapa warga (E): mereka berhenti, menghadap, dan berkata sesuai jam.
- [ ] Ambil Ojek Kampus, jemput penumpang dengan motor, antar sebelum waktu habis.
- [ ] Kucing Hilang: ikuti "meong" sampai beacon muncul dalam 30 m.
- [ ] Ronda Malam lewat Pak RT; setelah pos 2, menoleh ke belakang.
- [ ] Curi motor tanpa saksi, bawa ke bengkel, beli Cat ulang; warnanya berubah.
- [ ] Ketahuan mencuri, lari (Shift) sampai pengejar menyerah dan bintang hilang.
- [ ] Tidur di kos malam hari: pagi berikutnya uang jajan masuk dan jalan kembali ramai.
