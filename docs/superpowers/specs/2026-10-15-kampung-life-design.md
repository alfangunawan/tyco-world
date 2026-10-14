# Kampung Life (#6) — Design Spec

- **Tanggal:** 2026-10-15
- **Status:** Diputuskan sendiri oleh Claude atas goal sesi dari user: "lanjutkan pengembangan hingga ada pedestriannya, ada side mission, ada toko2 yang bisa dibeli (toko kelontong, toko baju, dan lainnya), bisa mencuri mobil, apapun yang oke ... agar mirip GTA V ... tetapi masih sesuai tema game kita". Tidak ada sesi tanya-jawab; setiap keputusan dicatat di sini dan bisa diubah user.
- **Dasar:** World Generator (#1), Player (#2), Siklus Pagi/Malam (#3), Settings (#4), Motor (#5), Street Frontage (+ polish). Peta arsitektur: assembly WorldGen < Player < {Vehicles, DayCycle} < GameSettings.

## 1. Tujuan dan tema

Dunia sudah rapi tetapi kosong. Sub-proyek ini membuat loop gameplay ala GTA V yang tetap membumi di kampung mahasiswa Sukabirus:

- **Siang = hidup mahasiswa.** Uang jajan, warung, distro, kerja sampingan (ojek, antar galon, titipan), dan warga yang lalu-lalang.
- **Malam = horor.** Toko tutup kecuali satu minimarket 24 jam, jalan sepi, ada "sosok" di pinggir jalan, dan misi malam (ronda, suara dari kuburan).
- **Kriminal kecil, konsekuensi kampung.** Mencuri motor atau mobil bisa dipergoki warga ("Maling!"). Warga mengejar, dan kalau tertangkap kamu didenda serta digiring pulang. Tidak ada senjata dan tidak ada kematian.

Non-goal: interior bangunan, NavMesh, animasi skeletal, dialog bercabang, cutscene, multiplayer, dan aset berlisensi. Semua visual dibangun dari primitif dan material prosedural, seperti motor.

## 2. Keputusan utama

| Topik | Keputusan | Alasan |
|---|---|---|
| Assembly | `TycoWorld.Gameplay` (Runtime/Editor/Tests) di atas Player, Vehicles, DayCycle, dan WorldGen | Tidak membalik arah dependensi; GameSettings tidak perlu tahu |
| Data dunia untuk gameplay | Di-bake di editor ke `KampungData` (ScriptableObject, di-commit) dari pipeline WorldGen yang sama: jalur jalan kaki, jalur kendaraan, lokasi toko, parkir, pintu rumah, dan titik khusus | Runtime tidak punya graf jalan; bake deterministik dan cocok dengan dunia hasil Generate |
| Objek runtime | Toko, NPC, dan kendaraan dunia dibuat oleh `KampungDirector` di root `Gameplay` (World.unity), tidak pernah di bawah GeneratedRoot | Generated root dihapus tiap Generate; culling sendiri |
| Uang | Rupiah (`long`). Awal Rp 50.000. Tiap bangun tidur dapat "uang jajan" Rp 25.000 | Skala harga warung nyata |
| Kebutuhan | Lapar dan Energi (0–100), turun mengikuti jam game. Lapar < 15: sprint mati. Energi 0: pingsan, bangun di kos (Rp 10.000 hilang) | Alasan untuk belanja dan pulang; tetap ringan |
| Simpan | JSON di PlayerPrefs `tyco.save` (versi 1): uang, tas, baju, kebutuhan, hari dan jam, misi selesai, kendaraan milik | Pola sama dengan SettingsStore |
| UI | UI Toolkit dibangun dari kode (seperti HUD). Overlay `GameplayHud` (uang, kebutuhan, tujuan misi, kompas, toast antrean), modal toko, dan HP (Tab) | Konsisten dengan proyek; HUD lama hanya punya satu slot pesan |
| Tutup modal | Tab atau tombol "Tutup" untuk HP; E atau tombol "Tutup" untuk toko. Esc tetap milik menu jeda | GameMenu memiliki Esc |
| Pejalan kaki | Agen sederhana di jalur jalan kaki hasil bake, dengan visual primitif (badan, kepala, tangan, kaki berayun). Hanya hidup dalam radius 70 m dari kamera; siang ±28, malam ±6, ditambah "sosok" | Murah, deterministik, tanpa NavMesh |
| Kendaraan dunia | Motor parkir (prefab Motorbike) dan mobil (angkot/minibus, memakai controller `Motorbike` dengan `CarTuning`, lean 0) di titik parkir hasil bake; di-instantiate dalam radius 120 m | Memakai ulang fisika arcade yang sudah teruji |
| Mencuri | Kendaraan milik warga: "[E] Bobol kunci motor/mobil", 3 detik tanpa bergerak (mobil 4 detik + alarm). Saksi = warga dalam 18 m yang melihat (raycast) | GTA-like, tanpa kekerasan |
| Wanted | "Panas" 0–3 bintang ("Maling!"). Warga terdekat mengejar dengan lari 5.5 m/s dan teriak. Tertangkap: denda Rp 100.000 (atau semua uang bila kurang), kendaraan curian kembali, dan kamu diantar ke kos. Panas turun bila tidak terlihat warga selama 25 detik. Bengkel "Cat ulang" (Rp 50.000) menghapus panas kendaraan | Pengganti polisi yang sesuai kampung |
| Toko | Tipe: Warung kelontong, Warung makan, Distro (baju), Konter pulsa, Apotek, Bengkel, Minimarket 24 jam. Lokasi: bangunan Flush (ruko) di tepi jalan, rumah infill ruko, dan bangunan OSM bertag shop/amenity. Etalase berupa meja, tenda, papan nama, dan penjaga (NPC). Jam buka per tipe | Fasad "Warung/RollingDoor" sudah ada di ruko |
| Baju | Kaos, kemeja, jaket (malam: Energi turun lebih lambat), hoodie hitam (saksi butuh 30% lebih dekat), helm (tidak mengubah visual orang pertama). Terlihat pada pengendara saat kamera orang ketiga | Kosmetik plus efek kecil |
| Misi | Kerangka langkah: Pergi ke, Interaksi, Bawa barang, Antar penumpang, dan batas waktu opsional. Didapat dari aplikasi "Kerjaan" di HP (butuh paket data dari konter pulsa) dan dari NPC tertentu. Hadiah uang | Variasi siang dan malam |
| Penanda | Pilar cahaya di dunia, kompas di atas layar dengan jarak, dan peta di HP (garis jalan dari bake, panah pemain, penanda) | GTA-like waypoint |

## 3. Data `KampungData` (bake editor)

Menu "Tyco World/Gameplay/Bake Kampung Data" menjalankan Classify → RoadNetwork → RoadEdgeBuilder → FrontagePlanner pass 1 → InfillPlanner → pass 2 → Walls. Ini urutan yang sama dengan Generate, sehingga hasilnya cocok dengan dunia yang di-generate. Isi asset:

- **`walkLanes`:** polyline per sisi chain yang bertepi, di tengah bahu (selokan, hw + 0.15) atau trotoar (hw + kerb + lebar/2), dipotong di mulut simpang. Ditambah centreline jalan kecil tanpa tepi (footway, path, gang, pedestrian). Setiap jalur punya node awal dan akhir. Node = ujung-ujung jalur yang berdekatan (≤ 6 m) digabung, termasuk ujung jalur di seberang jalan (menyeberang).
- **`driveLanes`:** centreline chain dengan offset lajur kiri (Indonesia lajur kiri) ±hw/2, untuk kendaraan warga yang lewat (fase akhir, opsional).
- **`shops`:** `ShopSite` {id, type, name, counter (posisi), yaw (menghadap jalan), buildingId}. Kandidat diambil dari interval Flush pass 2 (bangunan dekat garis kavling) dan bangunan OSM bertag shop/amenity, sampel tiap ≥ 40 m. Tipe dan nama dipilih dengan hash yang deterministik dan kuota per tipe; nama fiktif (tidak meniru merek nyata).
- **`parking`:** titik parkir motor atau mobil di bahu jalan dekat toko dan di depan gerbang rumah, menghadap searah jalan.
- **`doors`:** gerbang dan pintu rumah (bukaan Lot pass 2) sebagai tujuan antar barang.
- **`landmarks`:** kos (spawn), masjid (place_of_worship), kuburan (area Cemetery), gerbang kampus, dan pos ronda (diturunkan dari simpang yang dekat kos).

## 4. Sistem runtime (`TycoWorld.Gameplay`)

- **`KampungDirector`** (root "Gameplay" di World.unity): menunggu GeneratedRoot dan player, memuat save, menjalankan sistem, lalu `Tick` tiap frame dalam urutan Needs → Shops → Pedestrians → Vehicles → Heat → Missions → HUD.
- **Model murni (diuji EditMode):** `Wallet`, `Inventory`, `Needs`, `ItemCatalog` (SO), `ShopStock`, `HeatModel`, `MissionModel`, `SaveStore` (+ `MemorySaveBackend`), `LaneGraph` (memilih jalur lanjut), `PedestrianSchedule` (kepadatan dari jam), `OpeningHours`, `RupiahFormat` ("Rp 12.000").
- **Komponen:** `ShopStall` (meja, tenda, papan nama TextMesh, penjaga) dengan `ShopCounterInteractable`; `Pedestrian` (agen + visual + `TalkInteractable`); `WorldVehicle` (pemilik, terkunci, dan `StealVehicleInteractable` yang menggantikan `MotorbikeInteractable`); `MissionBeacon`; `KosBedInteractable` (pintu kos: "[E] Masuk dan tidur").
- **UI:** `GameplayHud`, `ShopView`, dan `PhoneView` (tab Dompet, Kerjaan, Tas, Peta).

## 5. Misi (set awal)

| Misi | Waktu | Inti | Hadiah |
|---|---|---|---|
| Ojek Kampus | 06–21 | Jemput penumpang di jalur jalan kaki, antar ke tujuan (motor, batas waktu) | Rp 8.000 + Rp 6/m |
| Antar Galon | 07–18 | Ambil galon di warung kelontong (dibayar pemberi), antar ke pintu rumah | Rp 15.000 |
| Titipan Belanja | 08–20 | Beli 2–3 barang di warung, antar ke teman kos | Uang barang + Rp 10.000 |
| Kucing Hilang | 07–17 | Temukan kucing (mengeong) dalam 150 m, bawa pulang ke pemilik | Rp 20.000 |
| Ronda Malam | 21–02 | Datangi 3 titik ronda berurutan; kejadian horor di titik kedua | Rp 30.000 |
| Suara dari Kuburan | 22–04 | Cari sumber suara di kuburan, temukan radio tua | Rp 40.000 + cerita |

## 6. Tahapan

1. **Inti dan toko:** assembly, model, save, HUD, bake shop/parking/landmark, warung kelontong, warung makan, distro, konter, apotek, bengkel, minimarket 24 jam, tidur di kos, dan kebutuhan.
2. **Pejalan kaki:** walk lanes, agen, ngobrol, kepadatan siang/malam, dan sosok malam.
3. **Misi dan HP:** kerangka misi, enam misi, HP (dompet, kerjaan, tas, peta), beacon, dan kompas.
4. **Kendaraan dan mencuri:** motor dan mobil parkir milik warga, bobol kunci, saksi, panas, kejar-kejaran warga, dan cat ulang di bengkel.
5. **Verifikasi:** tes, Play via MCP (belanja, misi, mencuri, malam), perf (NPC dan kendaraan ≤ +10% batch), dokumen, dan PR.

## 7. Pengetesan

- Model murni: harga dan saldo tidak pernah negatif, tas bertumpuk, kebutuhan turun per jam game, jam buka, save round-trip dan versi tak dikenal, panas naik/turun, langkah misi, pilihan jalur di node, dan format Rupiah.
- Bake: deterministik (dua kali sama), toko tidak di dalam bangunan dan menghadap jalan, parkir di atas permukaan jalan atau bahu, dan node jalur terhubung.
- Komponen (EditMode dengan fixture): beli di etalase mengurangi uang, tidur memajukan jam, bobol kunci memanggil `Mount`, saksi menaikkan panas, dan pejalan kaki tetap di jalurnya.
- Play via MCP: belanja di warung nyata, satu misi antar selesai, mencuri motor di depan warga lalu dikejar, dan malam: toko tutup dan sosok muncul.

## 8. Kriteria selesai

- Ada pejalan kaki (siang ramai, malam sepi dengan sosok), toko kelontong, makan, distro, dan toko lain yang bisa dibeli, enam side mission yang bisa diselesaikan, motor dan mobil warga yang bisa dicuri dengan konsekuensi, HP dengan peta, dan simpan/muat.
- Semua tes lulus, dunia tetap deterministik, dan perf batch naik ≤ +10% di titik kos/road/high.
