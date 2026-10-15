# Aset pihak ketiga: pembatas jalan buntu

Semua model di folder ini diunduh dari Poly Pizza dalam format glTF binary (.glb). Ketiganya berlisensi
**Public Domain (CC0 1.0)**, https://creativecommons.org/publicdomain/zero/1.0/, jadi boleh dipakai di game, termasuk
komersial, tanpa atribusi.

| File | Model | Pembuat | Sumber | Lisensi |
|---|---|---|---|---|
| `Kenney_PottedPlant.obj` | Potted Plant | Kenney | https://poly.pizza/m/23Dx9CC95C | CC0 1.0 |
| `Quaternius_BarrierSingle.obj` | Barrier Single | Quaternius | https://poly.pizza/m/wCUxgt2jSP | CC0 1.0 |
| `Quaternius_TrafficBarrier.obj` (+ `Quaternius_TrafficBarrier_Atlas.047.png`) | Traffic Barrier | Quaternius | https://poly.pizza/m/cM3aJPU9NS | CC0 1.0 |

**Perubahan:**
- GLB dikonversi ke OBJ/MTL. Transform setiap node (translasi, rotasi, skala) diterapkan ke verteks dan normal.
- Tekstur atlas Traffic Barrier diekspor apa adanya ke PNG.
- Model digeser supaya bagian bawahnya tepat di titik asal (dasar di y = 0, di tengah X/Z).
- Skala akhir diatur oleh `BarrierArt` saat membuat prefab:
  - pot setinggi 0,7 m;
  - balok setinggi 0,8 m;
  - palang setinggi 0,9 m, ditambah papan "JALAN DITUTUP" yang dibuat dari kode.
