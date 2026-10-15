# Karakter dan animasi (#7): spec desain

- **Tanggal:** 2026-10-15
- **Status:** Permintaan user setelah playtest:
  - "karakter saya ataupun semua npc itu jangan pake aset buatan sendiri, tapi cari dari internet yang free to use, pastikan cocok";
  - "pas di titik penjemputan tiba-tiba orang hilang, harusnya ada animasi dia naik motor";
  - "angkat galon pun sama, gak ada animasi dan asetnya".

  Keputusan diambil oleh Claude dan bisa diubah user.
- **Dasar:** Kampung Life (#6).

## 1. Aset

| Kebutuhan | Pilihan | Lisensi | Alasan |
|---|---|---|---|
| Animasi | Quaternius **Universal Animation Library (Standard)**, 45 klip pada rig humanoid | CC0 | Isinya `Walk_Loop`, `Jog_Fwd_Loop`, `Sprint_Loop`, `Idle_Loop`, `Idle_Talking_Loop`, `Sitting_Enter/Idle/Exit`, `Driving_Loop`, `PickUp_Table`, dan `Interact`. Klip ini bisa dipasang ke model Humanoid apa pun (retarget Mecanim) |
| Warga dan pemain | Quaternius **Ultimate Modular Men** dan **Ultimate Modular Women** (rig Humanoid) | CC0 | Proporsi realistis dan pakaian sehari-hari (kaos, kemeja, rompi pekerja, jas, terusan). Warna kulit bisa diganti menjadi sawo matang |
| Galon | "Water Cooler" oleh J-Toastie (Poly Pizza): tabung birunya dipakai sebagai galon, dan dispensernya sebagai prop | CC-BY 3.0, atribusi di `Assets/Gameplay/Art/ThirdParty/SOURCES.md` | Bentuknya persis galon isi ulang 19 L |

**Paket yang ditolak:**
- **Ultimate Animated Character Pack (Quaternius, CC0):** gaya chibi kartun, kepala besar, kulit hitam dengan mata kartun. Tidak cocok dengan kampung horor bergaya Chilla's Art.
- **Kenney Animated Characters (CC0):** kartun, dan hanya punya 3 animasi.

Setiap unduhan masuk ke folder scratch tersendiri dan diperlakukan sebagai data yang tidak tepercaya. Yang disalin ke `Assets/` hanya FBX dan file lisensi. File `.blend` tidak pernah dibuka.

**Status unduhan:**
- **Sudah:** UAL dan galon.
- **Menunggu:** Modular Men/Women hanya tersedia di Google Drive Quaternius, dan Drive sedang membalas "Quota exceeded". Skrip latar mencoba ulang tiap 10 menit.
- **Sementara itu:** sistem dibangun memakai mesh manekin UAL. Setelah paket Modular masuk, cukup ganti daftar model di `CharacterLibrary`.

## 2. Impor

`CharacterImportRules` (AssetPostprocessor) mengatur semua FBX di `Assets/Gameplay/Art/ThirdParty/Quaternius/`:
- diimpor sebagai Humanoid, dengan avatar dari model itu sendiri;
- hanya library animasi yang menyimpan klipnya;
- nama klip tanpa awalan `Rig|`;
- klip `*_Loop` diputar berulang;
- root dikunci di tempat, karena game sendiri yang menggerakkan orang.

## 3. Runtime

- **`CharacterLibrary`** (ScriptableObject, diisi oleh Gameplay Setup) memuat:
  - AnimatorController `Kampung`;
  - daftar model karakter;
  - prefab galon dan dispenser.
- **AnimatorController `Kampung`** dibangun dari kode oleh `CharacterAnimatorBuilder`:
  - **Parameter:** `Speed`, `Sit`, `Drive`, `Talk`, dan trigger `PickUp`.
  - **State:**
    - Locomotion: blend tree Idle → Walk → Jog → Sprint menurut `Speed`;
    - Talk;
    - SitEnter → SitIdle → SitExit;
    - Drive;
    - PickUp.
  - **IK:** IK pass menyala supaya tangan bisa memegang barang.
- **`CharacterRig`** adalah komponen pada setiap orang. Fungsinya `Move(speed)`, `Sit`, `Drive`, `Talk`, `PickUp`, dan `Hold(item)`. `Hold` memakai IK tangan pada pegangan barang dan berjalan di `OnAnimatorIK`.
- **Siapa yang memakai `CharacterRig`:** pejalan kaki, NPC misi, penjaga toko, Pak RT, dan sosok (dengan material pucat). Pejalan kaki memakai Animator culling `CullUpdateTransforms` sebagai bagian dari LOD.

## 4. Adegan

| Adegan | Yang terjadi |
|---|---|
| Ojek: penumpang naik | Pemain berhenti di dekat penumpang (laju di bawah 1 m/s). Penumpang berjalan ke sisi belakang motor, memutar `Sitting_Enter` sambil bergeser ke jok belakang, lalu menempel ke motor dengan `Sitting_Idle_Loop`. Pinggulnya disejajarkan dengan jok setiap frame |
| Ojek: penumpang turun | Pemain berhenti di tujuan. Penumpang memutar `Sitting_Exit`, turun ke samping motor, berjalan 6 m menjauh, lalu hilang |
| Galon: diambil | Penjaga warung mengambil galon (`PickUp_Table`). Galon muncul di bahu kanan pemain dengan gerak angkat 0,5 s. Kalau pemain naik motor, galon diikat di jok belakang |
| Galon: diserahkan | Galon pindah dari pemain ke tangan pemesan (IK hold). Pemesan memutar `Interact`, lalu berbalik dan masuk lewat gerbang |
| Pengendara | Saat kamera orang ketiga, model karakter pemain duduk di motor dengan `Driving_Loop`. Pengendara primitif bawaan motor disembunyikan |

## 5. Pengetesan

- **Impor:** avatar valid dan humanoid, 46 klip, flag loop benar.
- **Controller:** semua state dan parameter ada, IK pass menyala.
- **Rig:** `Move` dan `Sit` mengubah parameter. `Hold` menempelkan barang ke target tangan.
- **Ojek:** penumpang tidak langsung hilang. Ia menempel ke jok hanya setelah berjalan dan memutar enter, dan turun di tujuan.
- **Galon:** ada model galon di bahu pemain (atau di jok saat naik motor). Galon pindah ke pemesan saat diserahkan.
- **Performa:** sweep kos/road/high dengan batas yang sama seperti #6 (batch ≤ +10%).
