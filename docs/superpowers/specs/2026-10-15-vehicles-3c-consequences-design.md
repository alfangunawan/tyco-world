# Kendaraan 3C: kerusakan, pejalan kaki tertabrak, AI polisi (sub-proyek 3C): spec desain

- **Tanggal:** 2026-10-15
- **Status:** Permintaan user: "include AI polisi yang ngejar, kerusakan kendaraan, pejalan kaki tertabrak".
- **Keputusan user:**
  - Gaya "GTA ringan".
  - Tidak ada kematian dan tidak ada darah. Horornya tetap datang dari suasana.
- **Dasar:**
  - 3A (`2026-10-15-vehicles-3a-assets-design.md`).
  - 3B (`2026-10-15-vehicles-3b-traffic-design.md`).
  - Kampung life (#6): `HeatModel`, alur tertangkap (denda Rp 100.000, lalu diantar pulang), dan pengejaran oleh warga.

## 1. Kerusakan kendaraan

- **HP:** setiap kendaraan punya HP 0–100. Ini berlaku untuk kendaraan pemain, kendaraan parkir, kendaraan lalu lintas, dan mobil polisi.
- **Benturan:**
  - `Motorbike` sudah menghitung benturan (`Bumps`). Sekarang ia juga mencatat **kecepatan tumbukan**, yaitu komponen kecepatan ke arah normal kontak.
  - Kerusakan = (kecepatan tumbukan dalam km/h − 15) × faktor kelas. Benturan di bawah 15 km/h tidak merusak.
  - Faktor kelas: motor 2,0; mobil, van, angkot, pikap, dan polisi 1,2; truk 0,8.
  - Kalau dua kendaraan bertabrakan, keduanya rusak. Hitungannya memakai kecepatan relatif dan faktor kelas masing-masing.
- **Akibat:**

| HP | Asap | Kecepatan maks | Lainnya |
|---|---|---|---|
| < 50 | tipis, abu-abu | ×0,8 | bodi sedikit lebih gelap |
| < 20 | tebal, gelap | ×0,6 | mesin terbatuk (suara putus-putus tiap 2–4 s), bodi lebih gelap |
| 0 | tebal | — | mesin mati, tidak bisa digas; toast "Mesin mati." |

- **Visual:**
  - Asap berasal dari partikel URP dengan tekstur asap prosedural. Letaknya di mesin: depan untuk mobil, di bawah jok untuk motor.
  - Bodi yang gelap memakai varian material per tingkat (100 / <50 / <20) yang dibuat lewat `Palette`, jadi SRP Batcher tetap bekerja.
  - Tidak ada penyok, api, atau ledakan.
- **Bengkel:** di samping "Cat ulang", ada interaksi baru "Perbaiki (Rp X)". Tarifnya Rp 500 per HP yang hilang, jadi perbaikan penuh Rp 50.000. Kalau uangnya kurang, HP diperbaiki sebanyak yang terbayar.
- **Motor kos:**
  - HP-nya disimpan di save. Versi save naik, dan save lama dimigrasikan dengan HP 100.
  - Kalau HP-nya 0, jam 06.00 berikutnya motor kos kembali ke parkiran kos dengan HP 100. Muncul toast: "Motormu sudah diderek ke kos dan diperbaiki seadanya."
- **Kendaraan lain** yang HP-nya 0 tetap di tempat sebagai bangkai sampai hilang karena jarak (aturan 3B).

## 2. Pejalan kaki tertabrak

- **Deteksi:**
  - Saat kendaraan pemain melaju di atas 10 km/h, setiap FixedUpdate ada `OverlapBox` hull yang digeser ke depan sejauh kecepatan × dt.
  - Orang yang kena kapsulnya dianggap tertabrak.
  - Kendaraan lalu lintas tidak pernah menabrak, karena selalu mengerem.
- **Reaksi orang:**
  - Terdorong 1–2 m searah kendaraan dalam 0,4 s, menempel tanah lewat raycast, sambil memainkan klip UAL `Death01` sampai rebah.
  - Rebah 3–5 s.
  - Bangun dengan `Death01` diputar mundur pada kecepatan 1,5.
  - Berjalan pincang menjauhi pemain dengan `Walk_Loop` pada kecepatan 0,6, sambil berteriak "WOI! TABRAK LARI!" (toast). Setelah 20 s ia kembali normal.
- **Heat:**
  - Korban selalu menjadi saksi, jadi `HeatModel.Crime(2)`.
  - Tabrakan berikutnya dalam 10 s tidak menambah heat lagi.
- **Kendaraan pemain:** −5 HP, dan kecepatannya turun 30%.
- **Pengendara motor AI** yang ditabrak di atas 15 km/h:
  - Jatuh dengan reaksi yang sama.
  - Motornya rebah (miring 80°) dan diam.
  - Setelah bangun, ia menegakkan motornya, lalu kembali ke lalu lintas setelah 8 s.
  - Heat +1 kalau ada saksi (aturan saksi #6).
- **Sosok pucat** (malam, #6) tidak bisa ditabrak:
  - Kendaraan menembus sosoknya, lalu sosok itu hilang.
  - Muncul toast "…" dan ada suara angin pendek.
  - Heat tidak naik.
- **Tanpa kematian dan tanpa darah.**

## 3. AI polisi

- **Jumlah unit:**
  - Heat ≥ 2: 1 unit.
  - Heat 3: 2 unit.
  - Mobil polisi lalu lintas (3B) dalam 150 m diubah jadi unit lebih dulu. Unit lain muncul di lajur mobil pada jarak 100–150 m dari pemain, di luar pandangan bila bisa.
- **Isi unit:**
  - Prefab `police` dari 3A.
  - Petugas: `CharacterRig` model pria dengan seragam cokelat (tint).
  - Sirene: dua nada naik-turun hasil sintesis `EngineSynth`, terdengar dalam 80 m.
  - Lampu merah-biru emissive bergantian, plus satu Light berkedip dalam 40 m.
- **State:**
  1. **Approach:**
     - Kendaraan berjalan kinematik di lajur menuju titik lajur terdekat dari pemain (A*), dengan kecepatan sampai 55 km/h.
     - Lampu merah diabaikan, tapi kendaraan tetap mengerem untuk halangan.
     - Kendaraan lalu lintas dalam 40 m di depannya di lajur yang sama minggir 1,5 m ke kiri lalu berhenti sampai polisi lewat.
  2. **Pursuit** (pemain dalam 30 m, atau titik lajur terdekat lebih dari 25 m dari pemain):
     - Kendaraan pindah ke controller arcade `Motorbike` dengan preset polisi.
     - Input diberikan oleh `PoliceIntentSource`:
       - setir menuju posisi pemain 0,5 s ke depan;
       - kalau pemain berkendara, targetnya 6 m di depan pemain untuk memotong jalannya;
       - gas diatur menurut jarak;
       - tiga raycast (kiri, tengah, kanan) untuk menghindari tembok;
       - macet (di bawah 1 km/h selama 3 s): mundur 1,5 s dengan setir dibalik.
     - Polisi tidak sengaja menabrak. Benturan tetap merusak kedua kendaraan (§1).
  3. **Foot chase** (pemain jalan kaki dan unit dalam 10 m):
     - Unit berhenti, petugas turun, lalu mengejar dengan perilaku pengejar warga yang sudah ada.
     - Kecepatannya 10% di atas warga tapi di bawah sprint pemain, jadi pemain yang staminanya cukup bisa lolos.
     - Petugas menangkap pemain dalam jarak 1,3 m.
- **Tertangkap:**
  - Kondisi:
    - pemain jalan kaki dan diraih petugas; atau
    - pemain berkendara di bawah 3 km/h dan ada unit dalam 4 m selama 2 s.
  - Akibatnya alur tertangkap #6 berjalan:
    - denda Rp 100.000, atau seluruh uang kalau kurang;
    - toast "Kamu ditangkap polisi.";
    - pemain diantar pulang.
  - Kendaraan curian dikembalikan, yaitu di-despawn.
- **Melihat dan lolos:**
  - Unit melihat pemain kalau jaraknya dalam 60 m dan raycast garis pandangnya bebas (dicek tiap 0,5 s).
  - Penglihatan polisi dan warga sama-sama dipakai sebagai `seen` di `HeatModel.Tick`.
  - Heat turun satu bintang tiap 25 s tidak terlihat. Angka ini sudah ada di #6, tidak berubah.
  - Kalau heat turun di bawah 2, unit pindah ke mode lalu lintas, menjauh, lalu hilang setelah lebih dari 220 m.
  - Heat 1 tetap hanya dikejar warga, seperti #6.
- **Pelanggaran lampu merah (3B §5):** "dilihat polisi" berarti unit atau mobil polisi lalu lintas dalam 30 m dengan garis pandang bebas.

## 4. Bukan cakupan

- Tembakan dan senjata.
- Helikopter dan roadblock.
- Kematian pemain dan NPC.
- Penyok mesh.
- Api dan ledakan.
- Asuransi atau derek berbayar.

## 5. Performa

- `PoliceIntentSource` dan controller arcade aktif paling banyak untuk 2 unit.
- Sirene hanya terdengar dalam 80 m.
- Partikel asap aktif paling banyak pada 6 kendaraan terdekat.
- Syarat: dengan 2 unit mengejar, FPS minimal 90% dibanding lalu lintas biasa (3B §11).

## 6. Pengetesan

**EditMode:**

- **Kerusakan:**
  - rumus kerusakan per kelas;
  - di bawah 15 km/h tidak ada kerusakan;
  - ambang 50 / 20 / 0 dan kecepatan maksimal pada tiap ambang;
  - mesin mati di HP 0;
  - tarif perbaikan dan perbaikan sebagian kalau uang kurang;
  - migrasi save lama ke HP 100;
  - derek motor kos jam 06.00.
- **Tertabrak:**
  - hanya di atas 10 km/h;
  - urutan rebah, bangun, lalu pincang dengan durasinya;
  - heat +2 sekali per 10 s;
  - sosok pucat tidak bisa ditabrak;
  - pengendara motor AI jatuh lalu kembali ke lalu lintas.
- **Polisi:**
  - jumlah unit menurut heat;
  - mobil polisi lalu lintas dipakai lebih dulu;
  - jarak spawn;
  - perpindahan Approach → Pursuit → Foot chase;
  - kendaraan lalu lintas minggir;
  - syarat tertangkap saat berkendara dan saat jalan kaki;
  - heat tetap tertahan selama terlihat, lalu turun saat tidak terlihat;
  - unit pergi saat heat di bawah 2.

**Play** (save user dicadangkan dulu):

- Menabrak tembok di atas 40 km/h sampai asap muncul, lalu memperbaikinya di bengkel.
- Menabrak pejalan kaki, lalu heat naik ke 2 dan polisi datang.
- Kabur sampai lolos.
- Sengaja tertangkap saat berkendara dan saat jalan kaki.
- Pengukuran perf sesuai §5.
