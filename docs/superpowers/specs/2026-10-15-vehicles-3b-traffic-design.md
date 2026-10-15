# Kendaraan 3B: lalu lintas, bajak, lampu lalu lintas, dan angkot (sub-proyek 3B): spec desain

- **Tanggal:** 2026-10-15
- **Status:** Permintaan user setelah playtest: tambah motor, mobil, truk, oplet, dan kendaraan lain yang bergerak di jalan.
- **Keputusan user:**
  - Lalu lintas bisa dicuri, dengan gaya "GTA ringan".
  - Lalu lintas mengikuti lebar jalan: mobil di jalan selebar ≥ 5 m, motor juga masuk gang.
  - Pendekatan: kendaraan kinematik yang mengikuti lajur.
  - Fitur yang ditambahkan: lampu lalu lintas, penumpang angkot sungguhan, dan pemain bisa naik angkot sebagai penumpang. Pemain memilih tujuan, lalu memilih perjalanan di-skip (pindah lewat transisi) atau ditunggu sampai tiba.
- **Dasar:**
  - 3A (`2026-10-15-vehicles-3a-assets-design.md`): prefab, katalog, dan preset kelas.
  - Kampung life (#6): `PedestrianSystem`, `VehicleSystem`, `HeatModel`, `RoadGraph`, `KampungData`, `GameClock`, ponsel, dan HUD.
- **Tahap berikutnya:** 3C (`2026-10-15-vehicles-3c-consequences-design.md`), berisi kerusakan, pejalan kaki tertabrak, dan AI polisi.

## 1. Lajur lalu lintas (di-bake)

`KampungBaker` menambahkan data ke `KampungData` dari `RoadNetwork` (chain, lebar, highway) dan dari tag OSM jalan (`oneway`):

- **`trafficLanes`:** lajur berarah. Isinya:
  - titik-titik lajur;
  - node awal dan akhir;
  - batas kecepatan;
  - `allowsCars` dan `allowsBikes`;
  - prioritas: kelas jalan, sama dengan peringkat `RoadNetwork`;
  - id jalan;
  - lebar badan jalan dalam meter.
- **`trafficNodes`:** simpang dan ujung jalan. Isinya posisi, lajur masuk, dan lajur keluar.
- **`trafficConnectors`:** penyambung dari satu lajur masuk ke satu lajur keluar di sebuah node. Bentuknya kurva Bezier pendek dengan 4–6 titik, plus belok kiri / lurus / kanan.

**Aturan lajur:**

- **Jalan dua arah** mendapat dua lajur, masing-masing bergeser ke **kiri** arah jalannya sejauh seperempat lebar badan jalan (Indonesia lajur kiri).
- **Jalan `oneway=yes`** mendapat satu lajur di tengah. `oneway=-1` dibalik arahnya.
- **Mobil** boleh lewat kalau lebar badan jalan ≥ 5 m dan highway-nya bukan `service`, `footway`, `path`, `steps`, `pedestrian`, `track`, atau `cycleway`.
- **Motor** boleh lewat di semua jalan kecuali `footway`, `path`, `steps`, `pedestrian`, dan `cycleway`.
- **Batas kecepatan:**

| Jalan | Batas |
|---|---|
| Kelas utama: primary, secondary, tertiary, atau lebar ≥ 7 m | 45 km/h |
| residential / unclassified ≥ 5 m | 30 km/h |
| Gang | 20 km/h |
| service | 15 km/h |

- **Connector:**
  - Tidak ada connector ke lajur yang tidak boleh dilewati kelas kendaraan itu.
  - Di ujung buntu ada connector putar balik, khusus motor.
  - Mobil tidak memilih lajur yang hanya berujung buntu, ditandai flag `carDeadEnd` yang dihitung saat bake.
- Lajur ikut terpotong oleh RoadClearance (#8), jadi tidak menembus gedung. Pembatas jalan buntu ada di ujung lajur.

## 2. Populasi

`TrafficSystem` (`IKampungSystem`) memakai pool kendaraan dari prefab 3A.

**Target jumlah kendaraan aktif di sekitar pemain:**

| Jam | Target |
|---|---|
| 06–20 | 20 |
| 20–24 | 8 |
| 04–06 | 5 |
| 00–04 | 2 |

Di antara baris tabel, target diinterpolasi selama 30 menit game.

**Campuran kendaraan** (bobot, seed SplitMix64 dari hari + nomor spawn):

| Jenis | Bobot | Model |
|---|---|---|
| Motor | 60 | vario, supra |
| Mobil kecil / MPV / van | 25 | sedan, hatch, lmpv1, lmpv2, mpv2, van |
| Angkot | 8 | angkot (hanya di lajur mobil) |
| Pikap / truk | 7 | l300, pickup, carry, elf |
| Polisi | — | paling banyak 1 unit di lalu lintas biasa |

**Aturan spawn:**

- Kendaraan muncul di lajur yang boleh dilewati kelasnya, pada jarak 70–180 m dari pemain.
- Dicoba 8 kandidat. Kandidat di luar frustum kamera didahulukan; kalau semuanya terlihat, dipilih yang paling jauh.
- Tidak boleh muncul dalam 25 m dari kendaraan lain di lajur yang sama.

**Despawn:** kendaraan hilang kalau lebih dari 220 m dari pemain, atau macet (§3) di luar pandangan.

**Kendaraan yang dibajak** keluar dari pool sampai ditinggal (§6).

## 3. Gerak

- Kendaraan bergerak kinematik sepanjang lajur dan connector. Posisi dihitung dari jarak tempuh di polyline, arah dari tangennya, dan tinggi dari raycast ke tanah di bawah roda.
- **Kecepatan target** = min(batas lajur, kecepatan maksimal kelas, batas tikungan dari kelengkungan connector). Percepatan dan pengereman memakai angka preset kelas.
- **Jaga jarak:**
  - Setiap lajur menyimpan daftar kendaraan yang terurut menurut jarak tempuh.
  - Jarak aman ke kendaraan di depan = 2 m + 1,2 s × kecepatan.
  - Rem dihitung dengan model car-following sederhana yang mirip IDM.
- **Halangan di depan:**
  - Setiap 3 frame ada box-cast selebar kendaraan sejauh jarak aman + 4 m.
  - Yang dideteksi: pemain (jalan kaki atau berkendara), pejalan kaki, kendaraan diam, dan pembatas. Kalau ada, kendaraan mengerem sampai berhenti.
  - Terhalang pemain lebih dari 2 detik: bunyi klakson (sekali, lalu diulang tiap 4 detik).
- **Macet:**
  - Lebih dari 10 detik tidak bergerak dan di luar pandangan: despawn.
  - Motor yang macet karena halangan statis putar balik kalau ada connector putar balik.
- **Visual:**
  - Roda berputar dan setir mengikuti arah connector.
  - Motor condong di tikungan, maksimal 20°.
  - Pengendara motor AI tampil sebagai `CharacterRig.Ride` dalam jarak 40 m.
  - Mobil tidak menampilkan sopir (kacanya gelap).
- **Fisika:** kendaraan lalu lintas memakai Rigidbody kinematik + collider hull dari prefab.
  - Pemain yang menabrak terhenti (bump), sesuai perilaku controller yang sudah ada.
  - Kendaraan AI tidak pernah mendorong pemain, karena ia sudah mengerem sebelum bersentuhan.
- **Performa:**
  - Lebih dari 60 m: posisi di-update tiap 4 frame dengan waktu yang terkumpul, roda diam, tanpa bayangan.
  - Malam: semua kendaraan menyalakan lampu emissive. Hanya 4 kendaraan terdekat yang punya Light sorot sungguhan.

## 4. Simpang

- Kendaraan melambat ke 15 km/h (belok) atau 25 km/h (lurus) di 12 m terakhir sebelum node.
- **Simpang tanpa lampu:**
  - Kendaraan dari jalan berprioritas lebih rendah berhenti di garis node kalau dalam 15 m ada kendaraan dari jalan berprioritas lebih tinggi yang menuju simpang yang sama.
  - Kotak simpang dipesan oleh satu connector pada satu waktu. Connector yang tidak bersilangan boleh berjalan bersamaan, misalnya dua belokan kiri dari arah berlawanan. Persilangan antar-connector dihitung saat bake.
  - Kalau dua kendaraan datang bersamaan, yang lebih dulu memesan jalan duluan. Kalau seri, prioritas jalan menang, lalu id lajur yang lebih kecil.
- **Simpang berlampu:** lihat §5.
- **Pemain** tidak ikut aturan simpang. Kendaraan AI tetap mengerem untuknya lewat box-cast.

## 5. Lampu lalu lintas

- **Letak:**
  - Node OSM `highway=traffic_signals` dipetakan ke simpang terdekat dalam 15 m.
  - Kalau tidak ada, lampu dipasang di simpang yang punya minimal 3 lengan dan semua lengannya jalan mobil selebar ≥ 7 m.
  - Report mencatat jumlah simpang berlampu. Perkiraannya 2–4 di sekitar Jl. Telekomunikasi dan Jl. Sukabirus.
- **Fase:**
  - Lengan dikelompokkan berpasangan menurut arah (lengan yang saling berhadapan dalam ±30°).
  - Tiap kelompok mendapat hijau 20 s, kuning 3 s, lalu semua merah 1 s.
  - Waktu fase dihitung dari jam game, jadi deterministik dan tetap sama setelah dimuat ulang.
- **Perilaku AI:**
  - Berhenti di garis stop saat merah.
  - Saat kuning, berhenti kalau masih sempat mengerem dengan perlambatan nyaman 3 m/s²; kalau tidak sempat, terus jalan.
  - Belok kiri tetap ikut lampu. Tidak ada aturan "belok kiri jalan terus", supaya sederhana.
- **Prop:**
  - Tiang prosedural 4 m dengan kotak lampu 3 warna. Lampunya quad emissive yang berganti material per fase.
  - Satu tiang di tiap lengan, di sisi kiri jalan.
  - Garis stop putih melintang lajur masuk ditambahkan ke mesh marka jalan (WorldGen).
- **Pelanggaran:** pemain berkendara melewati garis stop saat merah, dan dalam 30 m ada kendaraan polisi (lalu lintas atau unit 3C) yang melihatnya. Akibatnya heat +1. Pelanggaran yang sama hanya dihitung sekali per simpang per 10 detik.

## 6. Membajak

- **Syarat:**
  - Kendaraan lalu lintas berhenti, dengan kecepatan di bawah 1 km/h: di simpang, karena terhalang, atau saat angkot berhenti.
  - Pemain jalan kaki dalam 2 m dari sisi sopir (motor: sisi mana saja).
- **Prompt:** "Tahan E: rampas `<motor/mobil/angkot/truk>`".
- **Lama tahan:** motor 1,5 s, mobil 2 s. Batal kalau pemain bergerak lebih dari 0,6 m. Selama ditahan, kendaraan tidak jalan.
- **Pengemudi:**
  - Pengendara motor turun.
  - Sopir mobil muncul di pintu kanan sebagai pejalan kaki dari pool `PedestrianSystem`.
  - Pengemudi teriak "MALING!", lalu ikut mengejar seperti warga.
- **Heat:** `HeatModel.Crime(1)`, atau `Crime(2)` untuk mobil polisi. Saksi dihitung seperti pencurian #6.
- **Kendaraan:**
  - Keluar dari pool lalu lintas.
  - Komponen `Motorbike` diaktifkan dengan preset kelasnya, dan pemain langsung naik.
  - Didaftarkan ke `VehicleSystem` sebagai `WorldVehicle` curian (`Hot = true`). Cat ulang di bengkel menghapus heat seperti #6.
- **Kendaraan yang ditinggal:**
  - Diam di tempat sebagai kendaraan parkir.
  - Hilang kalau lebih dari 220 m dari pemain, kecuali motor kos.
- **Angkot yang sedang membawa penumpang dibajak:** penumpangnya turun dulu (§7) sebelum pemain naik.

## 7. Angkot dan penumpang

- **Titik tunggu (`angkotStops`, di-bake):**
  - Dipasang di sisi kiri lajur mobil kelas utama, kira-kira tiap 200 m. Tidak boleh dalam 25 m dari simpang atau 30 m dari kos.
  - Tiap titik punya nama dari landmark terdekat dalam 80 m. Urutan pilihannya: gedung kampus bernama, masjid, gerbang, toko, kos, lalu nama jalan, misalnya "Depan Masjid Al-…" atau "Jl. Sukabirus".
  - Tiap titik diberi prop papan "ANGKOT" kecil di tiang.
- **Gerak angkot:**
  - Angkot berjalan di lajur mobil. Di setiap titik tunggu, angkot berhenti kalau ada orang yang menunggu atau penumpang yang mau turun.
  - Berhentinya minggir ke kiri, bergeser 0,8 m dari lajur, selama 3–6 detik atau sampai semua naik/turun selesai.
- **Penumpang NPC:**
  - Pejalan kaki kadang dikirim ke titik tunggu: tiap orang yang muncul punya peluang 10% pada jam 06–20.
  - Di titik tunggu, orang itu berdiri dengan pose menunggu sampai angkot lewat, paling lama 3 menit game. Setelah itu ia pergi.
  - Naik: berjalan ke pintu (titik pintu dari katalog 3A), lalu masuk (dihapus dari pool pejalan kaki).
  - Angkot menyimpan daftar penumpang beserta titik turunnya: titik lain dalam 300–1500 m sepanjang arah jalan.
  - Turun: penumpang muncul di pintu, lalu berjalan ke lajur pejalan kaki terdekat.
- **Kapasitas:** 8 orang. Angkot yang penuh tidak berhenti untuk orang yang menunggu.
- **Jam 00–04** tidak ada angkot.

## 8. Pemain naik angkot

- **Menyetop:**
  - Pemain berdiri di titik tunggu, atau di tepi lajur mobil (dalam 4 m dari lajur), lalu menekan E: "Stop angkot".
  - Angkot berikutnya yang menuju pemain, dalam 150 m dan tidak penuh, akan berhenti.
  - Kalau dalam 60 detik game tidak ada angkot, ponsel menampilkan pesan "Angkot belum lewat."
- **Memilih tujuan:** di pintu angkot, E membuka panel tujuan (UI Toolkit, gaya ponsel). Isinya:
  - daftar titik tunggu yang bisa dicapai, diurutkan dari yang terjauh, dengan nama, jarak, dan perkiraan waktu (panjang rute ÷ 25 km/h + 15 s per titik di antaranya);
  - tarif tetap **Rp 4.000**;
  - tombol **Naik**, **Skip perjalanan**, dan **Batal**.
- **Uang tidak cukup:** muncul pesan "Uangmu tidak cukup untuk ongkos angkot." dan pemain tidak naik.
- **Skip perjalanan:**
  - Layar memudar ke hitam selama 0,6 s.
  - Jam game dimajukan sesuai perkiraan waktu, sehingga Lapar dan Energi ikut berkurang lewat alur jam yang ada.
  - Pemain dipindahkan ke titik tujuan, menghadap menjauhi jalan.
  - Layar kembali terang selama 0,6 s. Angkotnya di-despawn.
  - **Skip dinonaktifkan** (tombolnya abu-abu, dengan alasan) kalau:
    - sedang ada misi berbatas waktu (ojek, galon);
    - heat lebih dari 0;
    - perjalanannya melewati jam 00.
- **Naik (menunggu sampai tiba):**
  - Badan pemain disembunyikan di kursi belakang angkot. Kamera orang ketiga mengikuti angkot, dan pemain bebas melihat dengan mouse.
  - HUD menampilkan tujuan, perkiraan sisa waktu, dan "E: turun di titik berikutnya".
  - Angkot mencari rute ke tujuan (A* di lajur mobil) dan tetap berhenti di titik lain untuk penumpang NPC.
  - Saat tiba, angkot minggir dan pemain diturunkan di sisi kiri (trotoar/bahu jalan).
- **Gangguan selama naik:**
  - Kalau heat naik (3C: polisi melihat pemain), perjalanan tetap berlanjut.
  - Kalau pemain menekan E untuk turun lebih awal, ia turun di titik berikutnya.
  - Simpanan saat di dalam angkot tidak menyimpan perjalanan. Setelah dimuat, pemain berada di titik tempat ia naik.

## 9. Integrasi

- **`VehicleSystem`:**
  - Kendaraan bajakan dan kendaraan yang ditinggal masuk daftarnya.
  - Batas jumlah motor/mobil parkir #6 tidak menghitung kendaraan bajakan.
- **`PedestrianSystem`:**
  - Menyediakan pengemudi yang turun dan penumpang angkot.
  - Pejalan kaki menyeberang di lajur jalan: lalu lintas mengerem untuk mereka lewat box-cast.
- **Lain-lain:**
  - Minimap menampilkan titik tunggu angkot sebagai ikon kecil saat pemain dalam 150 m.
  - GPS, misi, dan save tetap seperti sekarang.
  - Report bake mencatat `TrafficLanes`, `TrafficNodes`, `TrafficConnectors`, `TrafficSignals`, dan `AngkotStops`.

## 10. Bukan cakupan 3B

- Kerusakan, pejalan kaki tertabrak, dan AI polisi yang mengejar: dikerjakan di 3C.
- Menyalip.
- Parkir paralel lalu lintas.
- Bus besar.
- Kemacetan beruntun yang realistis.

## 11. Performa

- **Syarat**, diukur jam 10.00 di titik kos dan jalan, lalu lintas mati dibanding hidup:
  - batch naik paling banyak 10%;
  - FPS minimal 90% dari kondisi tanpa lalu lintas.
- Kalau FPS di editor tidak stabil, FPS diukur di build player.
- **Anggaran CPU** `TrafficSystem.Tick` dengan 20 kendaraan: paling lama 0,5 ms per frame, diukur dengan `Stopwatch` di Play.

## 12. Pengetesan

**EditMode, pada fixture `StreetFixture` / `TestOsm`:**

- **Bake lajur:**
  - jalan dua arah menghasilkan dua lajur di kiri arah jalan (offset seperempat lebar);
  - `oneway` menghasilkan satu lajur dengan arah yang benar;
  - gang tidak dilewati mobil;
  - footway tidak dilewati motor;
  - connector belok kiri/kanan/lurus benar;
  - putar balik hanya di ujung buntu dan hanya untuk motor;
  - lajur tidak menembus gedung;
  - hasil bake identik dua kali.
- **Simpang:**
  - jalan kecil memberi jalan ke jalan utama;
  - connector yang bersilangan tidak jalan bersamaan;
  - dua belokan kiri yang berhadapan boleh bersamaan.
- **Lampu:**
  - posisi dari tag OSM;
  - posisi cadangan di simpang besar;
  - siklus fase dari jam game;
  - aturan kuning (berhenti kalau sempat).
- **Populasi:**
  - target jumlah per jam;
  - bobot campuran untuk 1.000 spawn dengan seed tetap;
  - jarak spawn;
  - spawn di luar frustum didahulukan.
- **Gerak:**
  - jarak aman antar kendaraan;
  - berhenti untuk halangan;
  - klakson setelah 2 detik;
  - despawn kalau macet di luar pandangan.
- **Bajak:**
  - hanya saat kendaraan diam;
  - lama tahan per kelas;
  - batal kalau pemain bergerak;
  - pengemudi turun dan heat naik;
  - kendaraan jadi `WorldVehicle` curian.
- **Angkot:**
  - titik tunggu berjarak sekitar 200 m dan tidak dekat simpang atau kos;
  - nama titik dari landmark;
  - kapasitas 8;
  - penumpang turun di titik tujuannya;
  - tarif dipotong;
  - skip memajukan jam dan memindahkan pemain;
  - skip ditolak saat misi berbatas waktu, saat heat, atau saat melewati jam 00;
  - tidak ada angkot jam 00–04.
- **Determinisme:** dengan seed dan jam yang sama, urutan spawn identik.

**Play** (save user dicadangkan dulu):

- **Screenshot siang dan malam:** jalan utama ramai, gang hanya dilewati motor, lampu lalu lintas, dan angkot yang berhenti.
- **Bajak:** satu motor dan satu mobil.
- **Naik angkot:** sekali skip dan sekali menunggu sampai tiba.
- **Penumpang NPC:** satu orang naik lalu turun.
- **Pengukuran perf:** sesuai §11.
