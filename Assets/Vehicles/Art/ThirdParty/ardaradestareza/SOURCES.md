# Aset pihak ketiga: kendaraan (ardaradestareza)

Model di folder ini diunduh dari Sketchfab dalam format glTF Binary (.glb). Lisensinya **CC Attribution 4.0**
(https://creativecommons.org/licenses/by/4.0/): boleh dipakai, termasuk komersial, dengan atribusi.

| File | Model | Pembuat | Sumber | Lisensi |
|---|---|---|---|---|
| `angkot.glb` | Angkot lowpoly | ArtDha (ardaradestareza) | https://sketchfab.com/models/7fed82dcb4604b039b1d642636ced814 | CC Attribution 4.0 |

**Perubahan:** file .glb tidak diubah. Di game modelnya diskala, diputar menghadap depan, diberi warna cat (tint), dan
diberi pivot roda, lampu, dan kotak benturan oleh `VehicleModelBuilder` (lihat `VehicleModelCatalog`).

Roda depan (`ban`) dan belakang (`ban blk`) masing-masing satu mesh per as: berputar, tapi roda depan tidak berbelok.
