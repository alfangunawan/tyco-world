# World Generator — baseline (2026-10-13)

- Data OSM: 27.472 node, 5.863 way, 39 relation; 4.983 bangunan, 768 jalan (diunduh 2026-10-13 06:02 UTC dari OSM API `/api/0.6/map.json` — fallback karena ketiga server Overpass sedang 504/timeout; isi sama persis dengan angka spec §3)
- Generate: 12.815 ms (run #2: 10.683 ms), 278 chunk, skip bangunan 2 dari 4.923 (0,04%; `building:too-small` 1, `relation:unclosed-ring` 1), 66 bangunan di luar batas area di-cull
- Fitur: Area=36, Building=4916, KeyBuilding=5, Road=763, WaterArea=3, WaterLine=18
- Props: 1.363 pohon (tidak ada yang terpotong batas 4000), 928 lampu jalan
- Determinisme: hash 278 chunk identik pada 2 kali generate; tidak ada asset yatim (270 file mesh); hash `World.unity` sama sebelum/sesudah generate
- Raycast: ground 20/20, gedung kunci 5/5
- PreviewCam: (-194,6; 1,6; -632,7), yaw 274° — titik tengah Jl. Sukabirus (way 95529338) berada tepat di belokannya ke barat
- Play mode (PreviewCam, Jl. Sukabirus): 3.391 draw call, 43 SetPass, 61.521 segitiga, 156.099 verteks, 929 light, 8.033 MeshRenderer, tanpa error Console
- Frame time tidak tercatat dengan andal: editor tidak fokus saat diukur lewat Unity MCP, sehingga `UnityStats` membeku di 362 ms. Ukur ulang dengan jendela Game + Stats saat editor fokus.
- Catatan visual: lampu jalan tampak menyala (kepala lampu emissive), tapi sebaran cahayanya di aspal belum terlihat pada intensitas 3 / jangkauan 12 m.
- Setelah perbaikan final review: 1.278 pohon (pohon di air/lapangan/parkir tidak lagi ditaruh), 876 lampu (tiang di badan jalan lain dibuang); generate 11.469 ms; tanpa warning/error Console
- Sub-proyek #2 (player): jalan, area, dan air sekarang punya MeshCollider + GroundSurface; ada 4 dinding batas WorldEdge; Kos Raflesia 3 (way 409467778) jadi gedung kunci keenam (KeyBuilding=6, Building=4915). Collider/komponen/gedung kunci saja tidak mengubah hash (278 chunk IDENTICAL). Segitiga permukaan pijak lalu dipecah ke sisi ≤ 100 m (PhysX memperingatkan segitiga > 500 m pada Areas_557649490, Chunk_-8_-7, Chunk_-5_5): 52 chunk berubah hash secara sengaja, generate berikutnya identik lagi; MeshCollider 5741; tanpa warning Console.
