# Kendaraan 3A: Aset dan Katalog — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 14 kendaraan Indonesia (CC-BY, Sketchfab) masuk ke game sebagai prefab yang bisa dikendarai, dengan handling per kelas. Motor kos pemain menjadi Vario, kendaraan parkir warga memakai model baru, pose pengendara pas dengan model motornya, dan atribusi tampil di menu Kredit.

**Architecture:**
- **Impor:** GLB diimpor oleh glTFast, sehingga hierarki node tetap utuh.
- **Builder editor:** `VehicleModelBuilder` mengubah satu GLB + satu baris `VehicleModelSpec` menjadi prefab berstruktur standar:
  - akar dengan `CharacterController` + `Motorbike` + `MotorbikeVisuals` (diperluas) + `MotorbikeAudio`;
  - `LeanPivot/Model`;
  - pivot roda dan setir;
  - `Hull`, `Lights`, `Seat`, dan `RideAnchors`.
- **Handling:** satu preset per kelas di `VehicleClassPresets`. Ukuran tubuh diambil dari bounds model, dan hasilnya disimpan sebagai `BikeTuning` per model.
- **Katalog:** `VehicleCatalog` (ScriptableObject runtime) memetakan id → prefab + kelas + cat. Gameplay memakainya untuk kendaraan parkir.

**Tech Stack:** Unity 6000.6.5f1, URP 17.6, C# 9, glTFast 6.15.0, Unity Test Framework, Unity MCP, Python (`-I`) untuk memeriksa GLB.

**Spec:** `docs/superpowers/specs/2026-10-15-vehicles-3a-assets-design.md`

## Global Constraints

**Proyek dan branch**
- Root `D:\UNITY\project\Tyco World`.
- Branch `feature/vehicles` dari `main` (`384340d`). Spec ada di commit `62ccab9`.

**Assembly**
- Runtime kendaraan: `TycoWorld.Vehicles` (namespace `TycoWorld.Vehicles`).
- Editor: `TycoWorld.Vehicles.Editor` (namespace `TycoWorld.Vehicles.Authoring`).
- Tes: `TycoWorld.Vehicles.Tests`.
- Gameplay runtime: `TycoWorld.Gameplay`. Gameplay editor: `TycoWorld.Gameplay.Authoring`. Tes Gameplay: `TycoWorld.Gameplay.Tests`.
- Menu: `TycoWorld.GameSettings`.

**Aturan kode**
- C# 9.
- Angka diformat dengan `CultureInfo.InvariantCulture`.
- Jangan pakai `??` / `?.` pada `UnityEngine.Object`.
- Tidak ada bilangan acak. Pilihan model memakai `SeededRandom.Mix` dari indeks titik parkir.

**Nilai dari spec**
- Hanya ditulis di `VehicleModelCatalog` (panjang asli, §1) dan `VehicleClassPresets` (tabel §5).
- Anggaran per model: maksimal 13.000 segitiga dan 3 material.
- Toleransi panjang ±2%. Model berdiri di y = 0 (±0,02).
- Syarat pose: tangan dan kaki paling jauh 6 cm dari anchor.
- Perf: batch naik paling banyak 3%.

**Teks pemain**
- `"Kredit"`, `"Kembali"`.
- Kata benda per kelas: `"motor"`, `"mobil"`, `"angkot"`, `"pikap"`, `"truk"`.

**Lisensi**
- Semua model: CC Attribution 4.0.
- Atribusi wajib ada di `SOURCES.md` dan di `Credits.txt`.

**Unduhan**
- GLB dari user disalin ke folder scratch baru dan dibaca hanya dengan `python -I`. Tidak ada yang dieksekusi.
- Yang masuk Assets hanya `.glb` yang lolos pemeriksaan, plus `SOURCES.md`.

**File proyek**
- Jangan stage `ProjectSettings/*`, `Assets/WorldGen/Materials/WorldGen_Fallback.mat`, `Assets/WorldGen/Generated/`, atau `Assets/Gameplay/Art/Characters/Kampung.controller` (fileID-nya selalu dibuat ulang oleh setup). `Packages/manifest.json` dan `packages-lock.json` boleh di-stage di Task 1.
- Jangan pakai `git add -A` / `git add .`.
- Commit diakhiri:
  - `Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>`
  - `Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5`

**Penyimpangan nama dari spec (sudah diputuskan di plan)**
- Spec §4 menyebut komponen baru `VehicleVisuals`. Plan memperluas `MotorbikeVisuals` saja, supaya `Motorbike`, `RideAnchors.Of`, dan `RiderBodySystem` tidak perlu berubah tipe. Fungsinya sama.
- Spec §5 menyebut `Tuning_<Kelas>.asset`. Plan menyimpan `Tuning/<id>.asset` per model (preset kelas + tubuh dari bounds), karena ukuran tubuh berbeda antar-model dalam satu kelas.

## Prosedur standar

**Compile**
1. Pastikan `EditorApplication.isPlaying == false`. Kalau user sedang main, berhenti dan beri tahu.
2. `AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport)` lalu `CompilationPipeline.RequestScriptCompilation()`.
3. Baca error dengan `Unity_GetConsoleLogs`.

**Tes**
- Dalam panggilan terpisah: `TycoWorld.WorldGen.Tests.McpTestRunner.Run("<regex grup>", "<scratch>/vehicles/tests_<nama>.txt")`.
- Baris pertama hasil: `group=... passed=N failed=0`.
- Jangan jalankan tes di perintah yang sama dengan `ArtAssetBuilder.BuildAll`, karena `StreetLampSystemTests` sensitif terhadap urutan.

**Play** (Task 5 dan 9)
1. Backup `PlayerPrefs` `tyco.save` ke `scratchpad/gameplay/user-save-backupN.json`.
2. Selama Play pakai `director.SaveBackend = new MemorySaveBackend()`. Set `Application.runInBackground` hanya saat Play.
3. Setelah keluar, verifikasi save sama dengan backup; kalau beda, pulihkan dari backup.
4. Jangan edit `.cs` selama Play.
5. Cari motor pemain lewat `name == "Motorbike"`, bukan `FindAnyObjectByType<Motorbike>()`, karena itu bisa mengembalikan motor warga yang nonaktif.

## Review Focus

1. **Builder dijalankan ulang pada prefab yang sudah ada** (motor kos, model): hasilnya tetap satu `Model`, tidak ada anak ganda, root tetap dengan file id yang sama, dan id interaksi `kos-motor` tetap. Dipaku oleh `VehicleModelBuilderTests.Build_TwiceOnTheSameRoot_LeavesOneModel` (Task 4) dan `VehicleAssetsTests.KosBike_IsTheVario_AndKeepsItsInteraction` (Task 6).
2. **GLB satu model hilang:** builder melewatinya dengan peringatan dan menaikkan counter, sedangkan game memakai kendaraan primitif. Dipaku oleh `VehicleModelAssetsTests.MissingSource_IsSkippedAndCounted` (Task 4) dan `VehicleFactoryModelTests.MissingPrefab_FallsBackToTheCodeVehicle` (Task 7).
3. **Collider hull kendaraan tidak bertabrakan dengan `CharacterController`-nya sendiri,** termasuk motor kos yang tidak lewat `VehicleFactory.Activate`. Dipaku oleh `MotorbikeTests.Initialize_IgnoresItsOwnHull` (Task 4).
4. **Cat pada model ber-atlas tunggal tidak membuat kaca dan lampu ikut berwarna mencolok:** kekuatan tint 0,6. Dipaku oleh `VehicleTintTests.AtlasModel_IsTintedAtPaintStrength` (Task 7).
5. **Turun dari mobil lebar tidak menaruh pemain di dalam bodi:** `dismountSideOffset` minimal setengah lebar + radius pemain + 0,1 m. Dipaku oleh `VehicleClassPresetsTests.FitBody_DismountClearsTheSide` (Task 3).

---

## Peta file

| File | Task | Isi |
|---|---|---|
| `Packages/manifest.json` | 1 | + `com.unity.cloud.gltfast` 6.15.0 |
| `scratchpad/tools/glb_inspect.py` | 1 | pemeriksa GLB (data saja) |
| `Assets/Vehicles/Art/ThirdParty/<Pembuat>/<id>.glb` + `SOURCES.md` | 1 | model + atribusi |
| `Assets/Vehicles/Editor/Models/VehicleModelFiles.cs` | 1 | id → pembuat → path |
| `Assets/Vehicles/Runtime/Catalog/VehicleClass.cs` | 2 | enum kelas |
| `Assets/Vehicles/Runtime/Catalog/VehicleCatalog.cs` | 2 | ScriptableObject id → prefab |
| `Assets/Vehicles/Editor/Models/VehicleModelSpec.cs` | 2 | data per model |
| `Assets/Vehicles/Editor/Models/VehicleModelCatalog.cs` | 2, 5 | 14 baris |
| `Assets/Vehicles/Runtime/Catalog/VehicleClassPresets.cs` | 3 | tabel §5 + `FitBody` |
| `Assets/Vehicles/Runtime/Visuals/MotorbikeVisuals.cs` | 4 | + roda/setir dari model |
| `Assets/Vehicles/Runtime/Motorbike.cs` | 4 | abaikan collider sendiri |
| `Assets/Vehicles/Editor/MotorbikeBuilder.cs` | 4 | `BuildAudio` publik |
| `Assets/Vehicles/Editor/Models/VehicleModelBuilder.cs` | 4, 5 | GLB → prefab |
| `Assets/Vehicles/Editor/Models/VehicleModelAssets.cs` | 4 | semua prefab + tuning + katalog |
| `Assets/Vehicles/Editor/VehiclePaths.cs` | 4 | path baru |
| `Assets/Vehicles/Editor/VehicleAssetsBuilder.cs` | 4, 6 | memanggil model assets; motor kos = Vario |
| `Assets/Gameplay/Runtime/Vehicles/VehicleFactory.cs` | 7 | prefab katalog + tint |
| `Assets/Gameplay/Runtime/Vehicles/VehicleTint.cs` | 7 | cache material tint |
| `Assets/Gameplay/Runtime/Vehicles/WorldVehicle.cs` | 7 | `ModelId`, `Class`, `Noun` |
| `Assets/Gameplay/Runtime/Vehicles/VehicleSystem.cs` | 7 | pilih model per titik |
| `Assets/Gameplay/Runtime/KampungDirector.cs` | 7 | + `VehicleCatalog` |
| `Assets/Gameplay/Editor/GameplaySceneSetup.cs` | 7 | memasang katalog |
| `Assets/Vehicles/Editor/CreditsBuilder.cs` | 8 | `SOURCES.md` → `Credits.txt` |
| `Assets/GameSettings/Resources/Credits.txt` | 8 | hasil builder |
| `Assets/GameSettings/Runtime/UI/MenuPage.cs`, `PauseMenuView.cs`, `SettingsText.cs`, `Menu/GameMenu.cs` | 8 | halaman Kredit |
| `docs/superpowers/notes/2026-10-15-vehicles-3a-verification.md` | 9 | catatan verifikasi |

---

### Task 1: Unduhan, pemeriksaan, glTFast

**Files:**
- Create: `D:/Temp/claude/D--UNITY-project-Tyco-World/df943b1e-3709-4e21-a46a-54cc425c8cbd/scratchpad/tools/glb_inspect.py`
- Modify: `Packages/manifest.json`
- Create: `Assets/Vehicles/Art/ThirdParty/{Han66st,iwak.rebus,ardaradestareza,alwayshasbean,ann_fenek}/<id>.glb` + `SOURCES.md` di tiap folder
- Create: `Assets/Vehicles/Editor/Models/VehicleModelFiles.cs`
- Test: `Assets/Vehicles/Tests/VehicleArtTests.cs`

**Interfaces:**
- Produces:
  - `VehicleModelFiles.Path(string id) : string`, yaitu path `.glb` di Assets.
  - `VehicleModelFiles.Ids : IReadOnlyList<string>` (14 id, urut sesuai spec §1).
  - `VehicleModelFiles.Author(string id) : string`.

- [ ] **Step 1: Tanyakan path folder unduhan ke user, kalau belum diberikan.**
  - Salin semua `.glb` ke folder scratch baru: `scratchpad/vehicles-dl/<tanggal-jam>/`, satu file per id dengan nama `<id>.glb`.
  - Pemetaan nama file asli → id dibuat dari judul Sketchfab di spec §1. Contoh: `vario_psx_style_-_han66st.glb` → `vario`.
  - Kalau ada file yang kurang, sebutkan ke user model mana saja, lalu lanjutkan dengan yang ada. Model yang hilang ditangani di Task 4 lewat fallback.

- [ ] **Step 2: Tulis pemeriksa GLB (data saja).**

```python
"""Reads a .glb as data and prints what a vehicle import needs. Run with python -I; never executes anything from the file.

usage: python -I glb_inspect.py <file.glb> [<file.glb> ...]
Prints: generator, extensionsUsed, buffer URIs (must be none), images (mime, bytes), materials (name, base colour texture),
and the node tree with each node's mesh triangle count and its AABB in scene space (metres, glTF +Y up, +Z forward).
"""
import json
import struct
import sys

COMPONENT = {5120: ("b", 1), 5121: ("B", 1), 5122: ("h", 2), 5123: ("H", 2), 5125: ("I", 4), 5126: ("f", 4)}
COUNT = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}


def mat_mul(a, b):
    return [[sum(a[i][k] * b[k][j] for k in range(4)) for j in range(4)] for i in range(4)]


def node_matrix(node):
    if "matrix" in node:
        m = node["matrix"]
        return [[m[c * 4 + r] for c in range(4)] for r in range(4)]
    tx, ty, tz = node.get("translation", [0, 0, 0])
    qx, qy, qz, qw = node.get("rotation", [0, 0, 0, 1])
    sx, sy, sz = node.get("scale", [1, 1, 1])
    r = [[1 - 2 * (qy * qy + qz * qz), 2 * (qx * qy - qz * qw), 2 * (qx * qz + qy * qw)],
         [2 * (qx * qy + qz * qw), 1 - 2 * (qx * qx + qz * qz), 2 * (qy * qz - qx * qw)],
         [2 * (qx * qz - qy * qw), 2 * (qy * qz + qx * qw), 1 - 2 * (qx * qx + qy * qy)]]
    return [[r[0][0] * sx, r[0][1] * sy, r[0][2] * sz, tx], [r[1][0] * sx, r[1][1] * sy, r[1][2] * sz, ty],
            [r[2][0] * sx, r[2][1] * sy, r[2][2] * sz, tz], [0.0, 0.0, 0.0, 1.0]]


def load(path):
    data = open(path, "rb").read()
    magic, version, _ = struct.unpack("<4sII", data[:12])
    if magic != b"glTF" or version != 2:
        raise ValueError("not a glTF 2 binary")
    offset, doc, binary = 12, None, b""
    while offset < len(data):
        length, kind = struct.unpack("<I4s", data[offset:offset + 8])
        body = data[offset + 8:offset + 8 + length]
        if kind == b"JSON":
            doc = json.loads(body)
        elif kind == b"BIN\x00":
            binary = body
        offset += 8 + length
    return doc, binary


def inspect(path):
    doc, binary = load(path)
    print("==", path)
    print("generator:", doc.get("asset", {}).get("generator"))
    print("extensionsUsed:", doc.get("extensionsUsed", []))
    print("buffer uris:", [b.get("uri") for b in doc.get("buffers", []) if "uri" in b] or "none")
    for i, im in enumerate(doc.get("images", [])):
        size = doc["bufferViews"][im["bufferView"]]["byteLength"] if "bufferView" in im else -1
        print("image %d: %s %s bytes=%d uri=%s" % (i, im.get("name"), im.get("mimeType"), size, im.get("uri")))
    for i, m in enumerate(doc.get("materials", [])):
        tex = m.get("pbrMetallicRoughness", {}).get("baseColorTexture")
        print("material %d: %s texture=%s factor=%s" % (i, m.get("name"), tex and tex.get("index"),
                                                         m.get("pbrMetallicRoughness", {}).get("baseColorFactor")))
    total = [0]

    def tris(mesh_index):
        n = 0
        for p in doc["meshes"][mesh_index]["primitives"]:
            if "indices" in p:
                n += doc["accessors"][p["indices"]]["count"] // 3
            else:
                n += doc["accessors"][p["attributes"]["POSITION"]]["count"] // 3
        return n

    def aabb(mesh_index, m):
        lo, hi = [1e9] * 3, [-1e9] * 3
        for p in doc["meshes"][mesh_index]["primitives"]:
            acc = doc["accessors"][p["attributes"]["POSITION"]]
            amin, amax = acc["min"], acc["max"]
            for cx in (amin[0], amax[0]):
                for cy in (amin[1], amax[1]):
                    for cz in (amin[2], amax[2]):
                        w = [sum(m[r][c] * v for c, v in enumerate((cx, cy, cz, 1.0))) for r in range(3)]
                        lo = [min(lo[k], w[k]) for k in range(3)]
                        hi = [max(hi[k], w[k]) for k in range(3)]
        return lo, hi

    def walk(index, parent, depth):
        node = doc["nodes"][index]
        m = mat_mul(parent, node_matrix(node))
        line = "  " * depth + "- %s" % node.get("name", "#%d" % index)
        if "mesh" in node:
            t = tris(node["mesh"])
            total[0] += t
            lo, hi = aabb(node["mesh"], m)
            line += "  tris=%d  min=(%.2f,%.2f,%.2f) max=(%.2f,%.2f,%.2f)" % (t, lo[0], lo[1], lo[2], hi[0], hi[1], hi[2])
        print(line)
        for child in node.get("children", []):
            walk(child, m, depth + 1)

    identity = [[1.0 if i == j else 0.0 for j in range(4)] for i in range(4)]
    for root in doc["scenes"][doc.get("scene", 0)]["nodes"]:
        walk(root, identity, 0)
    print("total tris:", total[0])


if __name__ == "__main__":
    for p in sys.argv[1:]:
        inspect(p)
```

- [ ] **Step 3: Periksa semua file.**

```bash
S="D:/Temp/claude/D--UNITY-project-Tyco-World/df943b1e-3709-4e21-a46a-54cc425c8cbd/scratchpad"
python -I "$S/tools/glb_inspect.py" "$S"/vehicles-dl/<tanggal-jam>/*.glb > "$S/vehicles-dl/<tanggal-jam>/report.txt"
```

Expected:
- tiap file punya `buffer uris: none`;
- `total tris` ≤ 13000;
- material ≤ 3.

Pelanggaran tidak menghentikan pekerjaan, tapi dicatat sebagai `Ruling:` di ledger, misalnya kalau Supra punya 4 material.

- [ ] **Step 4: Cek lisensi tiap model lewat API Sketchfab (GET publik, tanpa login).**

```bash
for uid in 6c67aa3f3c60455ba12780f442c65a81 6d61abb3aa3a4161bda31ef783111015 5d370d67172041dbae7d6f112255fdd9 1c2fdb3a653e4cd2b6cd0fa1a5c7c1e9 835fc6d910444b2db3627eeacb747ca1 13dd216f01124fb89d9e3da21015c6bf eff0a2db0bcf46c59c5bfe1c8804083d 095ecbdd43ac450fa392dcb47c2478cb 24e13a998f2b4944aa2d9a680c837981 616761063ad845fe84e1d965bdce5595 f548bd218ba94711b16cf1aeeee028ff 7fed82dcb4604b039b1d642636ced814 6bc99e709e9748da98cc9ac676684510 ee9e569d3fea439cb41166d07aeb7cca; do
  curl -s "https://api.sketchfab.com/v3/models/$uid" -o "$S/vehicles-dl/lic_$uid.json"; done
python -I -c "import json,glob;[print(f[-37:-5], json.load(open(f,encoding='utf-8'))['license']['label'], json.load(open(f,encoding='utf-8'))['user']['username']) for f in glob.glob(r'$S/vehicles-dl/lic_*.json')]"
```

Expected: setiap baris `CC Attribution`. Model yang bukan CC Attribution tidak dipakai; laporkan ke user.

- [ ] **Step 5: Tulis tes yang gagal.**

```csharp
using System.IO;
using NUnit.Framework;
using TycoWorld.Vehicles.Authoring;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.Vehicles.Tests
{
    /// <summary>The downloaded vehicle models (spec vehicles-3a §1–§2): imported by glTFast, credited in SOURCES.md.</summary>
    public class VehicleArtTests
    {
        [Test]
        public void EveryModel_ImportsAsAGameObjectWithMeshes()
        {
            Assert.AreEqual(14, VehicleModelFiles.Ids.Count);
            foreach (var id in VehicleModelFiles.Ids)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(VehicleModelFiles.Path(id));
                Assert.IsNotNull(go, id + " (" + VehicleModelFiles.Path(id) + ")");
                Assert.IsNotEmpty(go.GetComponentsInChildren<MeshFilter>(true), id + " has meshes");
            }
        }

        [Test]
        public void EveryModel_IsCreditedInItsFolder()
        {
            foreach (var id in VehicleModelFiles.Ids)
            {
                string folder = Path.GetDirectoryName(VehicleModelFiles.Path(id));
                string sources = File.ReadAllText(Path.Combine(folder, "SOURCES.md"));
                StringAssert.Contains(Path.GetFileName(VehicleModelFiles.Path(id)), sources, id);
                StringAssert.Contains("CC Attribution 4.0", sources, id);
                StringAssert.Contains("sketchfab.com/models/", sources, id);
            }
        }
    }
}
```

- [ ] **Step 6: Compile (prosedur standar), lalu jalankan `VehicleArtTests`.**

Expected: compile error `VehicleModelFiles` tidak ada. Itu merah yang benar.

- [ ] **Step 7: Tambah glTFast dan file model.**

  - **`Packages/manifest.json`:** tambahkan di `dependencies` (urut abjad, setelah `com.unity.ai.navigation`):

    ```json
    "com.unity.cloud.gltfast": "6.15.0",
    ```

  - **Salin GLB yang lolos** ke `Assets/Vehicles/Art/ThirdParty/<Pembuat>/<id>.glb`. Folder pembuat: `Han66st`, `iwak.rebus`, `ardaradestareza`, `alwayshasbean`, `ann_fenek`.
  - **`SOURCES.md` per folder**, contoh untuk Han66st:

    ```markdown
    # Aset pihak ketiga: kendaraan (Han66st)

    Semua model di folder ini diunduh dari Sketchfab dalam format glTF Binary (.glb). Lisensinya **CC Attribution 4.0**
    (https://creativecommons.org/licenses/by/4.0/): boleh dipakai, termasuk komersial, dengan atribusi.

    | File | Model | Pembuat | Sumber | Lisensi |
    |---|---|---|---|---|
    | `vario.glb` | Vario PSX Style - Han66st | Han66st | https://sketchfab.com/models/6c67aa3f3c60455ba12780f442c65a81 | CC Attribution 4.0 |
    | `sedan.glb` | 2005 Sedan PSX Style - Han66st | Han66st | https://sketchfab.com/models/5d370d67172041dbae7d6f112255fdd9 | CC Attribution 4.0 |

    **Perubahan:** file tidak diubah. Skala, arah hadap, pivot roda, lampu, dan kotak benturan ditambahkan oleh
    `VehicleModelBuilder` saat membuat prefab (lihat `VehicleModelCatalog`).
    ```

    Barisnya mengikuti spec §1: Han66st mendapat 10 baris (vario, sedan, hatch, lmpv1, lmpv2, mpv2, van, l300, pickup, police). Baris polisi ditambah catatan "hanya sedan polisi yang dipakai".
  - **`Assets/Vehicles/Editor/Models/VehicleModelFiles.cs`:**

```csharp
using System.Collections.Generic;

namespace TycoWorld.Vehicles.Authoring
{
    /// <summary>Where each downloaded vehicle model lives (spec vehicles-3a §1–§2): id → author folder → .glb path.</summary>
    public static class VehicleModelFiles
    {
        public const string Folder = "Assets/Vehicles/Art/ThirdParty";

        static readonly (string id, string author)[] Table =
        {
            ("vario", "Han66st"), ("supra", "iwak.rebus"), ("sedan", "Han66st"), ("hatch", "Han66st"), ("lmpv1", "Han66st"),
            ("lmpv2", "Han66st"), ("mpv2", "Han66st"), ("van", "Han66st"), ("l300", "Han66st"), ("pickup", "Han66st"),
            ("police", "Han66st"), ("angkot", "ardaradestareza"), ("carry", "alwayshasbean"), ("elf", "ann_fenek")
        };

        static readonly List<string> ids = new List<string>();

        public static IReadOnlyList<string> Ids
        {
            get
            {
                if (ids.Count == 0) foreach (var row in Table) ids.Add(row.id);
                return ids;
            }
        }

        public static string Author(string id)
        {
            foreach (var row in Table) if (row.id == id) return row.author;
            return null;
        }

        public static string Path(string id) => Folder + "/" + Author(id) + "/" + id + ".glb";
    }
}
```

- [ ] **Step 8: Compile, lalu jalankan `VehicleArtTests`.**

Expected:
- `passed=2 failed=0`.
- `Packages/packages-lock.json` berisi `com.unity.cloud.gltfast` versi 6.15.0.

Kalau ada model yang sengaja tidak diunduh: hapus id-nya dari `Table`, sesuaikan angka `14` di tes, dan catat `Ruling:` di ledger.

- [ ] **Step 9: Commit.**

```bash
git add Packages/manifest.json Packages/packages-lock.json Assets/Vehicles/Art Assets/Vehicles/Editor/Models/VehicleModelFiles.cs Assets/Vehicles/Editor/Models.meta Assets/Vehicles/Editor/Models/VehicleModelFiles.cs.meta Assets/Vehicles/Tests/VehicleArtTests.cs Assets/Vehicles/Tests/VehicleArtTests.cs.meta
git commit -m "feat(vehicles): import 14 CC-BY Indonesian vehicle models with glTFast"
```

---

### Task 2: Kelas, katalog runtime, dan spec model

**Files:**
- Create: `Assets/Vehicles/Runtime/Catalog/VehicleClass.cs`
- Create: `Assets/Vehicles/Runtime/Catalog/VehicleCatalog.cs`
- Create: `Assets/Vehicles/Editor/Models/VehicleModelSpec.cs`
- Create: `Assets/Vehicles/Editor/Models/VehicleModelCatalog.cs`
- Test: `Assets/Vehicles/Tests/VehicleModelCatalogTests.cs`

**Interfaces:**
- Consumes: `VehicleModelFiles` (Task 1), `scratchpad/vehicles-dl/<tanggal-jam>/report.txt`.
- Produces:
  - `enum VehicleClass { MotorMatic, MotorBebek, CarSmall, Mpv, Van, Angkot, Pickup, Truck, Police }`
  - `static bool VehicleClasses.IsBike(VehicleClass c)` dan `static string VehicleClasses.Noun(VehicleClass c)`.
  - `VehicleCatalog : ScriptableObject` dengan `Entry[] entries`, `Entry Get(string id)`, `IReadOnlyList<Entry> Entries`.
  - `VehicleCatalog.Entry`:
    - `string id`, `VehicleClass vehicleClass`, `GameObject prefab`;
    - `string[] paintMaterials`, `float paintStrength`.
  - `VehicleModelSpec`: field-field di Step 3.
  - `VehicleModelCatalog.All : IReadOnlyList<VehicleModelSpec>` dan `VehicleModelCatalog.Get(string id)`.

- [ ] **Step 1: Tulis tes yang gagal.**

```csharp
using System.Linq;
using NUnit.Framework;
using TycoWorld.Vehicles.Authoring;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.Vehicles.Tests
{
    /// <summary>The per-model table (spec vehicles-3a §1, §3).</summary>
    public class VehicleModelCatalogTests
    {
        [Test]
        public void Catalog_ListsEveryModel_OnceEach_WithItsClassAndLength()
        {
            var all = VehicleModelCatalog.All;
            CollectionAssert.AreEqual(VehicleModelFiles.Ids, all.Select(s => s.Id).ToList());
            var expected = new (string id, VehicleClass c, float length)[]
            {
                ("vario", VehicleClass.MotorMatic, 1.92f), ("supra", VehicleClass.MotorBebek, 1.89f), ("sedan", VehicleClass.CarSmall, 4.45f),
                ("hatch", VehicleClass.CarSmall, 3.80f), ("lmpv1", VehicleClass.Mpv, 4.40f), ("lmpv2", VehicleClass.Mpv, 4.10f),
                ("mpv2", VehicleClass.Mpv, 4.70f), ("van", VehicleClass.Van, 4.05f), ("l300", VehicleClass.Pickup, 4.30f),
                ("pickup", VehicleClass.Pickup, 5.30f), ("police", VehicleClass.Police, 4.45f), ("angkot", VehicleClass.Angkot, 3.90f),
                ("carry", VehicleClass.Pickup, 3.40f), ("elf", VehicleClass.Truck, 6.00f)
            };
            foreach (var (id, c, length) in expected)
            {
                var s = VehicleModelCatalog.Get(id);
                Assert.IsNotNull(s, id);
                Assert.AreEqual(c, s.Class, id);
                Assert.AreEqual(length, s.Length, 1e-4f, id);
            }
        }

        [Test]
        public void EveryWheelAndSteerNode_ExistsInItsModel()
        {
            foreach (var s in VehicleModelCatalog.All)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(VehicleModelFiles.Path(s.Id));
                Assert.IsNotNull(model, s.Id);
                var names = model.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToList();
                foreach (var n in s.FrontWheels.Concat(s.RearWheels)) CollectionAssert.Contains(names, n, s.Id + " wheel");
                if (!string.IsNullOrEmpty(s.SteerNode)) CollectionAssert.Contains(names, s.SteerNode, s.Id + " steer");
                if (!string.IsNullOrEmpty(s.KeepSubtree)) CollectionAssert.Contains(names, s.KeepSubtree, s.Id + " subtree");
                Assert.IsTrue(s.WheelsMerged || (s.FrontWheels.Length > 0 && s.RearWheels.Length > 0), s.Id + ": wheels named, or marked merged");
                Assert.That(s.PaintStrength, Is.InRange(0f, 1f), s.Id);
            }
        }

        [Test]
        public void Classes_KnowBikesAndNouns()
        {
            Assert.IsTrue(VehicleClasses.IsBike(VehicleClass.MotorMatic));
            Assert.IsTrue(VehicleClasses.IsBike(VehicleClass.MotorBebek));
            Assert.IsFalse(VehicleClasses.IsBike(VehicleClass.Angkot));
            Assert.AreEqual("motor", VehicleClasses.Noun(VehicleClass.MotorBebek));
            Assert.AreEqual("mobil", VehicleClasses.Noun(VehicleClass.Mpv));
            Assert.AreEqual("mobil", VehicleClasses.Noun(VehicleClass.Police));
            Assert.AreEqual("angkot", VehicleClasses.Noun(VehicleClass.Angkot));
            Assert.AreEqual("pikap", VehicleClasses.Noun(VehicleClass.Pickup));
            Assert.AreEqual("truk", VehicleClasses.Noun(VehicleClass.Truck));
        }
    }
}
```

- [ ] **Step 2: Compile, lalu jalankan `VehicleModelCatalogTests`.** Expected: compile error, karena tipenya belum ada.

- [ ] **Step 3: Tulis tipe-tipenya.**

`Assets/Vehicles/Runtime/Catalog/VehicleClass.cs`:

```csharp
namespace TycoWorld.Vehicles
{
    /// <summary>Handling and gameplay class of a vehicle (spec vehicles-3a §5).</summary>
    public enum VehicleClass
    {
        MotorMatic,
        MotorBebek,
        CarSmall,
        Mpv,
        Van,
        Angkot,
        Pickup,
        Truck,
        Police
    }

    public static class VehicleClasses
    {
        public static bool IsBike(VehicleClass c) => c == VehicleClass.MotorMatic || c == VehicleClass.MotorBebek;

        /// <summary>The word the HUD and toasts use for it.</summary>
        public static string Noun(VehicleClass c)
        {
            switch (c)
            {
                case VehicleClass.MotorMatic:
                case VehicleClass.MotorBebek:
                    return "motor";
                case VehicleClass.Angkot:
                    return "angkot";
                case VehicleClass.Pickup:
                    return "pikap";
                case VehicleClass.Truck:
                    return "truk";
                default:
                    return "mobil";
            }
        }
    }
}
```

`Assets/Vehicles/Runtime/Catalog/VehicleCatalog.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.Vehicles
{
    /// <summary>
    /// The built vehicle models (spec vehicles-3a §4, §6): id → prefab, class and how its body may be painted. Written by
    /// VehicleModelAssets; read by gameplay to put warga vehicles on the street.
    /// </summary>
    public sealed class VehicleCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string id = "";
            public VehicleClass vehicleClass;
            public GameObject prefab;
            /// <summary>Names of the materials a repaint tints; empty = fixed livery (police, angkot).</summary>
            public string[] paintMaterials = new string[0];
            /// <summary>1 tints a body-only material fully; below 1 keeps an atlas's glass and lights readable.</summary>
            public float paintStrength = 1f;
        }

        public Entry[] entries = new Entry[0];

        public IReadOnlyList<Entry> Entries => entries;

        public Entry Get(string id)
        {
            foreach (var e in entries)
                if (e != null && e.id == id) return e;
            return null;
        }
    }
}
```

`Assets/Vehicles/Editor/Models/VehicleModelSpec.cs`:

```csharp
using UnityEngine;

namespace TycoWorld.Vehicles.Authoring
{
    /// <summary>One downloaded model and how to turn it into a vehicle (spec vehicles-3a §3). Positions are in the built prefab's space.</summary>
    public sealed class VehicleModelSpec
    {
        public string Id;
        public VehicleClass Class;
        /// <summary>Real length in metres: the model is scaled uniformly until its bounds are this long (spec §1).</summary>
        public float Length;
        /// <summary>Degrees about +Y that turn the model's nose to +Z.</summary>
        public float YawFix;
        /// <summary>Node to keep when the file holds more than one vehicle (police: the sedan); null keeps all.</summary>
        public string KeepSubtree;
        public string[] FrontWheels = new string[0];
        public string[] RearWheels = new string[0];
        /// <summary>True when the wheels are part of the body mesh: nothing spins, and the report says so.</summary>
        public bool WheelsMerged;
        /// <summary>Bike: the handlebar/fork node that turns with the steering; null for cars (their front wheels turn).</summary>
        public string SteerNode;
        public string[] PaintMaterials = new string[0];
        public float PaintStrength = 1f;
        /// <summary>Overrides for points the builder otherwise derives from the bounds (null = derive).</summary>
        public Vector3? Headlight, TailLight, DriverSeat;
        /// <summary>Angkot only: where passengers step in and out (spec vehicles-3b §7).</summary>
        public Vector3[] PassengerDoors = new Vector3[0];
        /// <summary>Bikes only (filled in Task 5): right-hand side; the left mirrors in x.</summary>
        public BikeAnchorSpec Bike;
    }

    public sealed class BikeAnchorSpec
    {
        public Vector3 Seat, GripRight, FootRight, Pillion, PillionFootRight;
    }
}
```

- [ ] **Step 4: Isi `VehicleModelCatalog` dari `report.txt` (Task 1).** Aturan pengisian per model:
  - **`YawFix`:**
    - Cari sumbu panjang dari AABB total.
    - Kalau sumbu panjangnya X, `YawFix` bernilai ±90.
    - Kalau Z, nilainya 0 atau 180.
    - Arah depannya ditentukan dengan screenshot di Task 4: lampu depan dan kaca depan harus ada di +Z.
    - Nilai awal: 0. Task 4 Step 6 membetulkannya kalau model menghadap ke belakang.
  - **`FrontWheels` / `RearWheels`:**
    - Ambil node yang namanya mengandung `wheel`, `tire`, `ban`, `velg`, atau `rim`, atau node mesh kecil berbentuk silinder. Ciri silinder: ukuran Y dan Z hampir sama (±15%), X kecil, dan letaknya di dekat dasar.
    - Depan atau belakang ditentukan dari tanda koordinat Z-nya sesudah `YawFix`.
    - Kalau roda menyatu dengan bodi, isi `WheelsMerged = true`.
  - **`SteerNode` (motor):** node bernama `handle`, `fork`, `stang`, atau `steer`. Kalau tidak ada, `null`.
  - **`PaintMaterials` / `PaintStrength`:**
    - Kalau ada material bodi tersendiri (nama mengandung `body`, `paint`, atau `car`), isi nama material itu dengan strength 1.
    - Kalau model hanya punya 1 material atlas, isi nama material itu dengan strength 0,6.
    - `police` dan `angkot`: daftar kosong.
  - **`KeepSubtree`:** untuk `police`, isi nama node sedan dari report. Sedan = node yang AABB-nya lebih pendek di Y dibanding SUV.

  Bentuk file:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.Vehicles.Authoring
{
    /// <summary>
    /// The 14 vehicles of spec vehicles-3a §1, in that order. Lengths are the spec's; node and material names come from the
    /// GLB inspection report (Task 1); bike anchors from the pose fit (Task 5).
    /// </summary>
    public static class VehicleModelCatalog
    {
        static List<VehicleModelSpec> all;

        public static IReadOnlyList<VehicleModelSpec> All => all ??= Build();

        public static VehicleModelSpec Get(string id)
        {
            foreach (var s in All) if (s.Id == id) return s;
            return null;
        }

        static List<VehicleModelSpec> Build() => new List<VehicleModelSpec>
        {
            new VehicleModelSpec
            {
                Id = "vario", Class = VehicleClass.MotorMatic, Length = 1.92f, YawFix = 0f,
                FrontWheels = new[] { "<from report>" }, RearWheels = new[] { "<from report>" }, SteerNode = "<from report or null>",
                PaintMaterials = new[] { "<from report>" }, PaintStrength = 0.6f
            },
            // ... one entry per id, in VehicleModelFiles.Ids order, with the lengths of the test above
        };
    }
}
```

  Semua `<from report>` wajib diganti nama nyata dari `report.txt` sebelum Step 5. Tes `EveryWheelAndSteerNode_ExistsInItsModel` akan gagal kalau masih ada teks `<from report>`. `??=` di sini dipakai pada `List`, bukan pada `UnityEngine.Object`, jadi aman.

- [ ] **Step 5: Compile, lalu jalankan `VehicleModelCatalogTests`.** Expected: `passed=3 failed=0`.

- [ ] **Step 6: Commit.**

```bash
git add Assets/Vehicles/Runtime/Catalog Assets/Vehicles/Runtime/Catalog.meta Assets/Vehicles/Editor/Models/VehicleModelSpec.cs Assets/Vehicles/Editor/Models/VehicleModelSpec.cs.meta Assets/Vehicles/Editor/Models/VehicleModelCatalog.cs Assets/Vehicles/Editor/Models/VehicleModelCatalog.cs.meta Assets/Vehicles/Tests/VehicleModelCatalogTests.cs Assets/Vehicles/Tests/VehicleModelCatalogTests.cs.meta
git commit -m "feat(vehicles): vehicle classes, runtime catalog and the per-model table"
```

---

### Task 3: Preset kelas dan tubuh dari bounds

**Files:**
- Create: `Assets/Vehicles/Runtime/Catalog/VehicleClassPresets.cs`
- Test: `Assets/Vehicles/Tests/VehicleClassPresetsTests.cs`

**Interfaces:**
- Consumes: `VehicleClass`, `BikeTuning`.
- Produces:
  - `VehicleClassPresets.Apply(BikeTuning t, VehicleClass c)`;
  - `VehicleClassPresets.FitBody(BikeTuning t, Bounds local, VehicleClass c)`;
  - `VehicleClassPresets.MaxKmh(VehicleClass c) : float`.

- [ ] **Step 1: Tulis tes yang gagal.**

```csharp
using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.Vehicles.Tests
{
    /// <summary>Handling per class (spec vehicles-3a §5) and the body fitted to a model's bounds.</summary>
    public class VehicleClassPresetsTests
    {
        BikeTuning t;

        [SetUp]
        public void SetUp() => t = ScriptableObject.CreateInstance<BikeTuning>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(t);

        [TestCase(VehicleClass.MotorMatic, 60f, 4.0f, 1.6f, 150f, 60f, true)]
        [TestCase(VehicleClass.MotorBebek, 65f, 4.2f, 1.6f, 145f, 58f, true)]
        [TestCase(VehicleClass.CarSmall, 50f, 3.2f, 1.2f, 75f, 30f, false)]
        [TestCase(VehicleClass.Mpv, 45f, 3.0f, 1.1f, 70f, 28f, false)]
        [TestCase(VehicleClass.Van, 40f, 2.7f, 1.0f, 65f, 26f, false)]
        [TestCase(VehicleClass.Angkot, 40f, 2.7f, 1.0f, 65f, 26f, false)]
        [TestCase(VehicleClass.Pickup, 45f, 2.8f, 1.0f, 65f, 26f, false)]
        [TestCase(VehicleClass.Truck, 35f, 1.8f, 0.7f, 50f, 20f, false)]
        [TestCase(VehicleClass.Police, 55f, 3.6f, 1.3f, 78f, 32f, false)]
        public void Apply_MatchesTheSpecTable(VehicleClass c, float kmh, float accelRest, float accelMax, float yawLow, float yawHigh, bool leans)
        {
            VehicleClassPresets.Apply(t, c);
            Assert.AreEqual(kmh / BikeTuning.KmhPerMs, t.maxForwardSpeed, 1e-3f);
            Assert.AreEqual(kmh, VehicleClassPresets.MaxKmh(c), 1e-4f);
            Assert.AreEqual(accelRest, t.accelAtRest, 1e-4f);
            Assert.AreEqual(accelMax, t.accelAtMax, 1e-4f);
            Assert.AreEqual(yawLow, t.yawRateLow, 1e-4f);
            Assert.AreEqual(yawHigh, t.yawRateHigh, 1e-4f);
            Assert.AreEqual(leans, t.maxLean > 0f);
        }

        [Test]
        public void Truck_CameraSitsFurtherBack_ThanACar()
        {
            VehicleClassPresets.Apply(t, VehicleClass.CarSmall);
            float car = t.chaseDistance;
            VehicleClassPresets.Apply(t, VehicleClass.Truck);
            Assert.Greater(t.chaseDistance, car);
        }

        [Test]
        public void FitBody_BoxCoversTheModel()
        {
            var bounds = new Bounds(new Vector3(0f, 0.8f, 0f), new Vector3(1.7f, 1.6f, 4.4f));
            VehicleClassPresets.FitBody(t, bounds, VehicleClass.Mpv);
            Assert.AreEqual(0.85f, t.blockHalfExtents.x, 1e-3f);
            Assert.AreEqual(2.2f, t.blockHalfExtents.z, 1e-3f);
            Assert.LessOrEqual(t.stepOffset + 2f * t.blockHalfExtents.y, bounds.max.y + 1e-3f, "the box stays inside the model's height");
            Assert.AreEqual(0.85f, t.bodyRadius, 1e-3f);
            Assert.GreaterOrEqual(t.bodyHeight, 2f * t.bodyRadius);
        }

        [Test]
        public void FitBody_DismountClearsTheSide()
        {
            var bounds = new Bounds(new Vector3(0f, 1.2f, 0f), new Vector3(2.0f, 2.4f, 6f));
            VehicleClassPresets.FitBody(t, bounds, VehicleClass.Truck);
            const float playerRadius = 0.35f;
            Assert.GreaterOrEqual(t.dismountSideOffset, 1.0f + playerRadius + 0.1f);
        }

        [Test]
        public void FitBody_BikeKeepsANarrowCapsule()
        {
            var bounds = new Bounds(new Vector3(0f, 0.55f, 0f), new Vector3(0.7f, 1.1f, 1.92f));
            VehicleClassPresets.FitBody(t, bounds, VehicleClass.MotorMatic);
            Assert.AreEqual(0.35f, t.blockHalfExtents.x, 1e-3f);
            Assert.AreEqual(0.96f, t.blockHalfExtents.z, 1e-3f);
            Assert.LessOrEqual(t.bodyRadius, 0.5f);
        }
    }
}
```

- [ ] **Step 2: Compile, lalu jalankan `VehicleClassPresetsTests`.** Expected: compile error, karena `VehicleClassPresets` belum ada.

- [ ] **Step 3: Implementasi.**

```csharp
using UnityEngine;

namespace TycoWorld.Vehicles
{
    /// <summary>
    /// Handling per vehicle class (spec vehicles-3a §5) and the arcade body fitted to a model's bounds. Every number of the
    /// table lives here and only here.
    /// </summary>
    public static class VehicleClassPresets
    {
        readonly struct Row
        {
            public readonly float Kmh, AccelRest, AccelMax, YawLow, YawHigh, Chase, ChaseHeight;
            public readonly bool Leans;

            public Row(float kmh, float accelRest, float accelMax, float yawLow, float yawHigh, bool leans, float chase, float chaseHeight)
            {
                Kmh = kmh;
                AccelRest = accelRest;
                AccelMax = accelMax;
                YawLow = yawLow;
                YawHigh = yawHigh;
                Leans = leans;
                Chase = chase;
                ChaseHeight = chaseHeight;
            }
        }

        static Row Of(VehicleClass c)
        {
            switch (c)
            {
                case VehicleClass.MotorMatic: return new Row(60f, 4.0f, 1.6f, 150f, 60f, true, 4.5f, 1.9f);
                case VehicleClass.MotorBebek: return new Row(65f, 4.2f, 1.6f, 145f, 58f, true, 4.5f, 1.9f);
                case VehicleClass.CarSmall: return new Row(50f, 3.2f, 1.2f, 75f, 30f, false, 6.5f, 2.6f);
                case VehicleClass.Mpv: return new Row(45f, 3.0f, 1.1f, 70f, 28f, false, 7f, 2.8f);
                case VehicleClass.Van: return new Row(40f, 2.7f, 1.0f, 65f, 26f, false, 7f, 2.9f);
                case VehicleClass.Angkot: return new Row(40f, 2.7f, 1.0f, 65f, 26f, false, 7f, 2.9f);
                case VehicleClass.Pickup: return new Row(45f, 2.8f, 1.0f, 65f, 26f, false, 7f, 2.8f);
                case VehicleClass.Truck: return new Row(35f, 1.8f, 0.7f, 50f, 20f, false, 9f, 3.6f);
                default: return new Row(55f, 3.6f, 1.3f, 78f, 32f, false, 6.5f, 2.6f);
            }
        }

        public static float MaxKmh(VehicleClass c) => Of(c).Kmh;

        public static void Apply(BikeTuning t, VehicleClass c)
        {
            var r = Of(c);
            t.maxForwardSpeed = r.Kmh / BikeTuning.KmhPerMs;
            t.maxReverseSpeed = VehicleClasses.IsBike(c) ? 2.5f : 3f;
            t.accelAtRest = r.AccelRest;
            t.accelAtMax = r.AccelMax;
            t.yawRateLow = r.YawLow;
            t.yawRateHigh = r.YawHigh;
            t.maxLean = r.Leans ? 25f : 0f;
            t.chaseDistance = r.Chase;
            t.chaseHeight = r.ChaseHeight;
        }

        /// <summary>
        /// The controller capsule, the wall-stopping box, where the rider gets off and the camera look point, from the model's
        /// bounds in its own space (ground at y = 0, nose to +Z).
        /// </summary>
        public static void FitBody(BikeTuning t, Bounds local, VehicleClass c)
        {
            bool bike = VehicleClasses.IsBike(c);
            float halfWidth = local.size.x * 0.5f, halfLength = local.size.z * 0.5f;
            t.bodyRadius = bike ? Mathf.Min(0.5f, halfWidth + 0.15f) : Mathf.Min(halfWidth, 0.95f);
            t.bodyHeight = Mathf.Max(2f * t.bodyRadius, Mathf.Min(local.size.y, bike ? 1.4f : 1.8f));
            float boxHeight = Mathf.Max(0.2f, local.max.y - t.stepOffset - 0.05f);
            t.blockHalfExtents = new Vector3(halfWidth, boxHeight * 0.5f, halfLength);
            t.dismountSideOffset = Mathf.Max(bike ? 1.1f : 0f, halfWidth + 0.35f + 0.25f);
            t.lookHeight = bike ? 1.2f : Mathf.Max(1.2f, local.max.y * 0.8f);
            t.headHeight = bike ? 0.75f : 0.7f;
        }
    }
}
```

Catatan nilai di `FitBody_DismountClearsTheSide`: `halfWidth + 0.35 + 0.25 = 1.6`, yang ≥ 1,45 m. Untuk motor dengan lebar 0,7 m: `max(1.1, 0.95) = 1.1`, sama dengan nilai sekarang.

- [ ] **Step 4: Compile, lalu jalankan `VehicleClassPresetsTests`.** Expected: `passed=13 failed=0`.

- [ ] **Step 5: Commit.**

```bash
git add Assets/Vehicles/Runtime/Catalog/VehicleClassPresets.cs Assets/Vehicles/Runtime/Catalog/VehicleClassPresets.cs.meta Assets/Vehicles/Tests/VehicleClassPresetsTests.cs Assets/Vehicles/Tests/VehicleClassPresetsTests.cs.meta
git commit -m "feat(vehicles): handling presets per class, body fitted to the model"
```

---

### Task 4: Builder prefab model

**Files:**
- Modify: `Assets/Vehicles/Runtime/Visuals/MotorbikeVisuals.cs`
- Modify: `Assets/Vehicles/Runtime/Motorbike.cs` (`Initialize`)
- Modify: `Assets/Vehicles/Editor/MotorbikeBuilder.cs` (keluarkan `BuildAudio`)
- Modify: `Assets/Vehicles/Editor/VehiclePaths.cs`
- Create: `Assets/Vehicles/Editor/Models/VehicleModelBuilder.cs`
- Create: `Assets/Vehicles/Editor/Models/VehicleModelAssets.cs`
- Modify: `Assets/Vehicles/Editor/VehicleAssetsBuilder.cs` (`BuildAll` memanggil `VehicleModelAssets.BuildAll`)
- Test: `Assets/Vehicles/Tests/VehicleModelBuilderTests.cs`, `Assets/Vehicles/Tests/VehicleModelAssetsTests.cs`, `Assets/Vehicles/Tests/MotorbikeTests.cs` (+1 tes), `Assets/Vehicles/Tests/MotorbikeVisualsTests.cs` (+1 tes)

**Interfaces:**
- Consumes: `VehicleModelSpec`, `VehicleModelCatalog`, `VehicleModelFiles`, `VehicleClassPresets`, `VehicleCatalog`.
- Produces:
  - `MotorbikeVisuals.ConfigureModel(Transform lean, Transform steer, Transform[] spinning, Transform[] steering, float wheelRadius, Light light, Renderer tail, Renderer lens)`.
  - `MotorbikeVisuals.Wheels : IReadOnlyList<Transform>`, `MotorbikeVisuals.WheelRadius : float`.
  - `MotorbikeBuilder.BuildAudio(GameObject root, VehicleClipSet clips) : MotorbikeAudio`.
  - `VehicleModelBuilder.Build(GameObject root, VehicleModelSpec spec, GameObject source, BikeTuning tuning, VehicleMaterialSet materials, VehicleClipSet clips) : Bounds`. Hasilnya bounds model di ruang prefab.
  - `VehicleModelBuilder.ModelName = "Model"`, `HullName = "Hull"`, `LeanName = "LeanPivot"`, `SeatName = "Seat"`, `LightsName = "Lights"`.
  - `VehicleModelAssets.BuildAll(VehicleMaterialSet materials, VehicleClipSet clips) : VehicleCatalog`. Hasilnya ditulis ke `VehiclePaths.CatalogAsset`.
  - `VehicleModelAssets.Missing : int`, yaitu jumlah model yang dilewati karena GLB tidak ada.
  - `VehiclePaths.ModelPrefabsFolder = PrefabsFolder + "/Models"`, `ModelTuningFolder = ConfigFolder + "/Tuning"`, `CatalogAsset = ConfigFolder + "/VehicleCatalog.asset"`.

- [ ] **Step 1: Tulis tes yang gagal.**

`VehicleModelBuilderTests.cs`:

```csharp
using System.Linq;
using NUnit.Framework;
using TycoWorld.Vehicles.Authoring;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.Vehicles.Tests
{
    /// <summary>GLB + spec → standard vehicle (spec vehicles-3a §4).</summary>
    public class VehicleModelBuilderTests
    {
        GameObject root;
        BikeTuning tuning;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("VehicleUnderTest");
            tuning = ScriptableObject.CreateInstance<BikeTuning>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(tuning);
        }

        Bounds BuildModel(string id)
        {
            var spec = VehicleModelCatalog.Get(id);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(VehicleModelFiles.Path(id));
            return VehicleModelBuilder.Build(root, spec, source, tuning, VehicleMaterials.LoadOrCreate(), new VehicleClipSet());
        }

        static Bounds RendererBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>().Where(r => r.transform.IsChildOf(go.transform.Find(VehicleModelBuilder.LeanName))).ToArray();
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        [Test]
        public void EveryModel_IsScaledGroundedAndFacingForward()
        {
            foreach (var spec in VehicleModelCatalog.All)
            {
                foreach (Transform child in root.transform.Cast<Transform>().ToList()) Object.DestroyImmediate(child.gameObject);
                var b = BuildModel(spec.Id);
                var drawn = RendererBounds(root);
                Assert.AreEqual(spec.Length, drawn.size.z, spec.Length * 0.02f, spec.Id + " length");
                Assert.AreEqual(0f, drawn.min.y, 0.02f, spec.Id + " stands on the ground");
                Assert.AreEqual(0f, drawn.center.x, 0.05f, spec.Id + " centred");
                var lights = root.transform.Find(VehicleModelBuilder.LightsName);
                Assert.Greater(root.GetComponent<MotorbikeVisuals>().Headlight.transform.position.z, 0f, spec.Id + " headlight at the front");
                Assert.IsNotNull(lights, spec.Id);
                Assert.AreEqual(drawn.size.z, b.size.z, 0.01f);
            }
        }

        [Test]
        public void Car_GetsAHullOverItsBounds_AndAllWheelsSpin()
        {
            var b = BuildModel("lmpv1");
            var hull = root.transform.Find(VehicleModelBuilder.HullName).GetComponent<BoxCollider>();
            Assert.IsNotNull(hull);
            Assert.AreEqual(b.size.x, hull.size.x, 0.02f);
            Assert.AreEqual(b.size.z, hull.size.z, 0.02f);
            var visuals = root.GetComponent<MotorbikeVisuals>();
            var spec = VehicleModelCatalog.Get("lmpv1");
            if (!spec.WheelsMerged) Assert.AreEqual(spec.FrontWheels.Length + spec.RearWheels.Length, visuals.Wheels.Count);
            Assert.Greater(visuals.WheelRadius, 0.15f);
            Assert.Less(visuals.WheelRadius, 0.6f);
        }

        [Test]
        public void Build_TwiceOnTheSameRoot_LeavesOneModel()
        {
            BuildModel("sedan");
            BuildModel("sedan");
            Assert.AreEqual(1, root.GetComponentsInChildren<Transform>(true).Count(t => t.name == VehicleModelBuilder.ModelName));
            Assert.AreEqual(1, root.GetComponents<Motorbike>().Length);
            Assert.AreEqual(1, root.transform.Cast<Transform>().Count(t => t.name == VehicleModelBuilder.HullName));
        }

        [Test]
        public void Police_KeepsOnlyTheSedan()
        {
            var b = BuildModel("police");
            Assert.AreEqual(4.45f, b.size.z, 0.09f);
            Assert.Less(b.size.x, 2.2f, "one car wide, not two side by side");
        }

        [Test]
        public void EveryModel_StaysInBudget()
        {
            foreach (var spec in VehicleModelCatalog.All)
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(VehicleModelFiles.Path(spec.Id));
                int tris = source.GetComponentsInChildren<MeshFilter>(true).Sum(f => f.sharedMesh.triangles.Length / 3);
                var materials = source.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Distinct().Count();
                Assert.LessOrEqual(tris, 13000, spec.Id);
                Assert.LessOrEqual(materials, 3, spec.Id);
            }
        }
    }
}
```

Catatan untuk `Police_KeepsOnlyTheSedan`: spesifikasi tidak memaksa jumlah segitiga per subtree, cukup bounds. `EveryModel_StaysInBudget` menghitung seluruh file sumber. Kalau satu file melanggar anggaran, model itu dicatat sebagai `Ruling:` lalu dikeluarkan dari tes lewat daftar `Exempt` yang dikomentari di tes, tidak dihapus diam-diam.

`VehicleModelAssetsTests.cs`:

```csharp
using NUnit.Framework;
using TycoWorld.Vehicles.Authoring;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.Vehicles.Tests
{
    /// <summary>All model prefabs, their tuning and the catalog (spec vehicles-3a §4–§5).</summary>
    public class VehicleModelAssetsTests
    {
        [Test]
        public void Catalog_HasEveryModel_WithPrefabAndClassTuning()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<VehicleCatalog>(VehiclePaths.CatalogAsset);
            Assert.IsNotNull(catalog, "run VehicleAssetsBuilder.BuildAll");
            foreach (var spec in VehicleModelCatalog.All)
            {
                var e = catalog.Get(spec.Id);
                Assert.IsNotNull(e, spec.Id);
                Assert.AreEqual(spec.Class, e.vehicleClass);
                Assert.IsNotNull(e.prefab, spec.Id);
                var bike = e.prefab.GetComponent<Motorbike>();
                Assert.AreEqual(VehicleClassPresets.MaxKmh(spec.Class) / BikeTuning.KmhPerMs, bike.Tuning.maxForwardSpeed, 1e-3f, spec.Id);
                Assert.IsTrue(AssetDatabase.Contains(bike.Tuning), spec.Id + " tuning is an asset");
                CollectionAssert.AreEqual(spec.PaintMaterials, e.paintMaterials, spec.Id);
                Assert.AreEqual(spec.PaintStrength, e.paintStrength, 1e-4f, spec.Id);
                Assert.IsNull(e.prefab.GetComponent<MotorbikeInteractable>(), "warga vehicles get their lock from gameplay");
            }
        }

        [Test]
        public void MissingSource_IsSkippedAndCounted()
        {
            var spec = new VehicleModelSpec { Id = "does-not-exist", Class = VehicleClass.CarSmall, Length = 4f };
            Assert.IsNull(VehicleModelAssets.BuildOne(spec, null, VehicleMaterials.LoadOrCreate(), new VehicleClipSet(), out bool missing));
            Assert.IsTrue(missing);
        }
    }
}
```

Tambahkan ke `MotorbikeTests.cs`:

```csharp
        [Test]
        public void Initialize_IgnoresItsOwnHull()
        {
            var go = new GameObject("HullBike");
            try
            {
                var controller = go.AddComponent<CharacterController>();
                var hull = new GameObject("Hull").AddComponent<BoxCollider>();
                hull.transform.SetParent(go.transform, false);
                hull.size = new Vector3(1f, 1f, 3f);
                var bike = go.AddComponent<Motorbike>();
                bike.Initialize();
                Assert.IsTrue(Physics.GetIgnoreCollision(controller, hull));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
```

Tambahkan ke `MotorbikeVisualsTests.cs`:

```csharp
        [Test]
        public void ModelWheels_SpinByTheirRadius_AndFrontOnesSteer()
        {
            var go = new GameObject("CarVisuals");
            try
            {
                var lean = new GameObject("LeanPivot").transform;
                lean.SetParent(go.transform, false);
                Transform Wheel(string n)
                {
                    var t = new GameObject(n).transform;
                    t.SetParent(lean, false);
                    return t;
                }
                var fl = Wheel("FL");
                var steerFl = new GameObject("SteerFL").transform;
                steerFl.SetParent(lean, false);
                fl.SetParent(steerFl, false);
                var rl = Wheel("RL");
                var v = go.AddComponent<MotorbikeVisuals>();
                v.ConfigureModel(lean, null, new[] { fl, rl }, new[] { steerFl }, 0.35f, null, null, null);
                v.Apply(0.35f, 20f, 0f, false, false, false, 1f);
                Assert.AreEqual(Mathf.Rad2Deg, Quaternion.Angle(Quaternion.identity, rl.localRotation), 0.5f, "1 rad per radius travelled");
                Assert.AreEqual(20f, steerFl.localEulerAngles.y, 0.01f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
```

- [ ] **Step 2: Compile, lalu jalankan `VehicleModelBuilderTests|VehicleModelAssetsTests|MotorbikeTests|MotorbikeVisualsTests`.** Expected: compile error, karena tipe dan metode baru belum ada.

- [ ] **Step 3: Perluas `MotorbikeVisuals`.**
  - Tambah field:

    ```csharp
    [SerializeField] Transform[] wheels = new Transform[0];
    [SerializeField] Transform[] steerPivots = new Transform[0];
    [SerializeField] float wheelRadius = WheelRadius;
    ```

  - Tambah properti `Wheels => wheels` dan `WheelRadius => wheelRadius`. Supaya tidak bentrok dengan konstanta `WheelRadius`, konstantanya diganti nama jadi `DefaultWheelRadius`.
  - Tambah metode `ConfigureModel`:

```csharp
        /// <summary>A model-built vehicle (spec vehicles-3a §4): any number of spinning wheels and, for cars, front-wheel steer pivots.</summary>
        public void ConfigureModel(Transform lean, Transform steer, Transform[] spinning, Transform[] steering, float radius, Light light,
            Renderer tailRenderer, Renderer lensRenderer)
        {
            leanPivot = lean;
            handlebar = steer;
            frontWheel = null;
            rearWheel = null;
            rider = null;
            wheels = spinning != null ? spinning : new Transform[0];
            steerPivots = steering != null ? steering : new Transform[0];
            wheelRadius = radius > 0.05f ? radius : DefaultWheelRadius;
            headlight = light;
            tailLight = tailRenderer;
            headlightLens = lensRenderer;
        }
```

  - Di `Apply`, ganti perhitungan roda menjadi:

```csharp
            wheelAngle = Mathf.Repeat(wheelAngle + speed / wheelRadius * Mathf.Rad2Deg * dt, 360f);
            var spin = Quaternion.Euler(wheelAngle, 0f, 0f);
            if (frontWheel != null) frontWheel.localRotation = spin;
            if (rearWheel != null) rearWheel.localRotation = spin;
            foreach (var w in wheels) if (w != null) w.localRotation = spin;
            if (handlebar != null) handlebar.localRotation = Quaternion.Euler(0f, steerAngle, 0f);
            foreach (var s in steerPivots) if (s != null) s.localRotation = Quaternion.Euler(0f, steerAngle, 0f);
```

  - `Configure` (motor primitif) mengisi `wheelRadius = DefaultWheelRadius` dan mengosongkan kedua array.

- [ ] **Step 4: Ubah `Motorbike.Initialize`** supaya collider anak diabaikan oleh controller-nya sendiri. Tambahkan tepat setelah `ConfigureBody(body, tuning);`:

```csharp
            // A model vehicle's hull (box collider) must not push its own CharacterController.
            foreach (var c in GetComponentsInChildren<Collider>(true))
                if (c != body) Physics.IgnoreCollision(body, c, true);
```

- [ ] **Step 5: Keluarkan audio dari `MotorbikeBuilder.Build`** menjadi metode publik, lalu panggil dari `Build`:

```csharp
        /// <summary>The engine loop and one-shot sources under an "Audio" child, wired to MotorbikeAudio (shared by model vehicles).</summary>
        public static MotorbikeAudio BuildAudio(GameObject root, VehicleClipSet clips)
        {
            var audioGo = Child(AudioName, root.transform, new Vector3(0f, 0.5f, 0f)).gameObject;
            var sources = audioGo.GetComponents<AudioSource>();
            var engine = Configure3DSource(sources.Length > 0 ? sources[0] : audioGo.AddComponent<AudioSource>(), true);
            var oneShot = Configure3DSource(sources.Length > 1 ? sources[1] : audioGo.AddComponent<AudioSource>(), false);
            var sound = GetOrAdd<MotorbikeAudio>(root);
            sound.Configure(engine, oneShot, clips.Engine, clips.Starter, clips.Horn, clips.Bump);
            return sound;
        }
```

  Di `Build`, ganti blok audio dan `sound.Configure` dengan `var sound = BuildAudio(root, clips);`. Tes `MotorbikeBuilderTests` yang sudah ada harus tetap lulus.

- [ ] **Step 6: Tulis `VehicleModelBuilder`.**

```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TycoWorld.Vehicles.Authoring
{
    /// <summary>
    /// Turns a downloaded model and its spec row into a standard vehicle (spec vehicles-3a §4): scaled to its real length,
    /// nose to +Z, standing on y = 0, wheels on pivots that spin (and steer, at the front of cars), a hull box, lights,
    /// a seat and the arcade controller with the class's tuning. Re-running on the same root rebuilds the children.
    /// </summary>
    public static class VehicleModelBuilder
    {
        public const string ModelName = "Model", HullName = "Hull", LeanName = "LeanPivot", SeatName = "Seat", LightsName = "Lights";
        public const string SteerName = "Steer", HeadlightName = "Headlight", LensName = "HeadlightLens", TailName = "TailLight";

        /// <returns>The model's bounds in the prefab's space.</returns>
        public static Bounds Build(GameObject root, VehicleModelSpec spec, GameObject source, BikeTuning tuning, VehicleMaterialSet materials,
            VehicleClipSet clips)
        {
            foreach (var name in new[] { LeanName, HullName, SeatName, LightsName })
            {
                var old = root.transform.Find(name);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            bool bike = VehicleClasses.IsBike(spec.Class);

            var lean = NewChild(LeanName, root.transform);
            var model = Object.Instantiate(source, lean, false);
            model.name = ModelName;
            if (!string.IsNullOrEmpty(spec.KeepSubtree)) KeepOnly(model.transform, spec.KeepSubtree);
            model.transform.localRotation = Quaternion.Euler(0f, spec.YawFix, 0f);
            var b = DrawnBounds(model);
            if (b.size.z > 1e-4f) model.transform.localScale *= spec.Length / b.size.z;
            b = DrawnBounds(model);
            model.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);
            b = DrawnBounds(model);

            var spinning = new List<Transform>();
            var steering = new List<Transform>();
            Transform steer = null;
            float radius = 0f;
            if (!spec.WheelsMerged)
            {
                if (bike)
                {
                    var front = Find(model.transform, spec.FrontWheels[0]);
                    var frontCentre = DrawnBounds(front.gameObject).center;
                    steer = NewChild(SteerName, lean);
                    steer.position = new Vector3(0f, frontCentre.y, frontCentre.z);
                    if (!string.IsNullOrEmpty(spec.SteerNode)) Find(model.transform, spec.SteerNode).SetParent(steer, true);
                    spinning.Add(Pivot(front, steer, "Spin_" + front.name, out radius));
                    foreach (var n in spec.RearWheels) spinning.Add(Pivot(Find(model.transform, n), lean, "Spin_" + n, out _));
                }
                else
                {
                    foreach (var n in spec.FrontWheels)
                    {
                        var wheel = Find(model.transform, n);
                        var turn = NewChild("Steer_" + n, lean);
                        turn.position = DrawnBounds(wheel.gameObject).center;
                        spinning.Add(Pivot(wheel, turn, "Spin_" + n, out radius));
                        steering.Add(turn);
                    }
                    foreach (var n in spec.RearWheels) spinning.Add(Pivot(Find(model.transform, n), lean, "Spin_" + n, out _));
                }
            }

            var hull = NewChild(HullName, root.transform).gameObject.AddComponent<BoxCollider>();
            hull.center = b.center;
            hull.size = b.size;

            var lights = NewChild(LightsName, steer != null ? steer : lean);
            var head = spec.Headlight ?? new Vector3(0f, bike ? b.max.y * 0.82f : b.max.y * 0.45f, b.max.z - 0.05f);
            var tail = spec.TailLight ?? new Vector3(0f, bike ? b.max.y * 0.6f : b.max.y * 0.5f, b.min.z + 0.03f);
            var headlight = NewChild(HeadlightName, lights).gameObject.AddComponent<Light>();
            headlight.transform.position = head;
            headlight.transform.localRotation = Quaternion.Euler(6f, 0f, 0f);
            headlight.type = LightType.Spot;
            MotorbikeVisuals.ConfigureHeadlight(headlight, tuning);
            headlight.color = VehicleMaterials.LensEmission;
            headlight.shadows = LightShadows.None;
            headlight.enabled = false;
            float lampSpread = bike ? 0f : b.extents.x - 0.25f;
            var lens = LampQuads(LensName, lights, head + Vector3.forward * 0.03f, lampSpread, materials.HeadlightLens, false);
            var tailLamp = LampQuads(TailName, lean, tail - Vector3.forward * 0.03f, lampSpread, materials.TailLight, true);

            var seat = NewChild(SeatName, root.transform);
            seat.localPosition = spec.DriverSeat ?? (bike ? new Vector3(0f, b.max.y * 0.8f, b.center.z) : new Vector3(b.extents.x * 0.42f, b.max.y * 0.42f, b.center.z + 0.15f));

            Motorbike.ConfigureBody(GetOrAdd<CharacterController>(root), tuning);
            var visuals = GetOrAdd<MotorbikeVisuals>(root);
            visuals.ConfigureModel(lean, steer, spinning.ToArray(), steering.ToArray(), radius, headlight, tailLamp, lens);
            var sound = MotorbikeBuilder.BuildAudio(root, clips);
            GetOrAdd<Motorbike>(root).Configure(tuning, seat, visuals, sound);
            return b;
        }

        /// <summary>A pivot at the wheel's centre (axes of the vehicle) that the wheel hangs from, so spinning it turns the wheel in place.</summary>
        static Transform Pivot(Transform wheel, Transform parent, string name, out float radius)
        {
            var wb = DrawnBounds(wheel.gameObject);
            radius = wb.extents.y;
            var pivot = NewChild(name, parent);
            pivot.position = wb.center;
            wheel.SetParent(pivot, true);
            return pivot;
        }

        static Renderer LampQuads(string name, Transform parent, Vector3 at, float spread, Material material, bool facesBack)
        {
            var go = NewChild(name, parent).gameObject;
            go.transform.position = at;
            var m = new TycoWorld.WorldGen.Authoring.MeshData(1);
            foreach (float x in spread > 0f ? new[] { -spread, spread } : new[] { 0f })
                TycoWorld.Player.Authoring.PlaceholderMeshes.AddBox(m, 0, new Vector3(x, 0f, 0f), new Vector3(spread > 0f ? 0.28f : 0.15f, 0.1f, 0.02f));
            go.AddComponent<MeshFilter>().sharedMesh = m.ToMesh("Vehicle_" + name, out _);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            return r;
        }

        static void KeepOnly(Transform model, string keep)
        {
            var target = Find(model, keep);
            foreach (var t in model.GetComponentsInChildren<Transform>(true).ToList())
            {
                if (t == model || t == target || target.IsChildOf(t) || t.IsChildOf(target)) continue;
                if (t.GetComponent<Renderer>() != null) Object.DestroyImmediate(t.gameObject);
            }
        }

        static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            throw new System.ArgumentException("node not found: " + name);
        }

        static Bounds DrawnBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        static Transform NewChild(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }
    }
}
```

  Catatan implementasi:
  - **Bounds:** `DrawnBounds` memakai `Renderer.bounds`, yaitu AABB dunia. Akarnya di-reset ke titik asal di awal `Build`, jadi ruang dunia sama dengan ruang prefab.
  - **`KeepOnly`:** renderer di luar subtree terpilih dihapus. Node kosong dibiarkan karena tidak menggambar apa pun.
  - **Mesh lampu:** disimpan sebagai sub-asset prefab lewat `PlaceholderPropFactory.SaveMeshes`, dengan pola yang sama seperti `VehicleAssetsBuilder.SavePrefab`, yang dipanggil di `VehicleModelAssets`. Tujuannya supaya `Prefab_UsesOnlySavedAssets` tetap benar.
  - **`YawFix` yang salah:** kalau screenshot Task 4 Step 9 menunjukkan model menghadap ke belakang, ubah `YawFix` di katalog (±180), lalu catat `Ruling:`.

- [ ] **Step 7: Tulis `VehicleModelAssets`, tambah path, lalu panggil dari `VehicleAssetsBuilder.BuildAll`.**

`VehiclePaths` tambahan:

```csharp
        public const string ModelPrefabsFolder = PrefabsFolder + "/Models";
        public const string ModelTuningFolder = ConfigFolder + "/Tuning";
        public const string CatalogAsset = ConfigFolder + "/VehicleCatalog.asset";
```

`VehicleModelAssets.cs`:

```csharp
using System.Collections.Generic;
using TycoWorld.Player.Authoring;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.Vehicles.Authoring
{
    /// <summary>Builds every model prefab, its tuning asset and the catalog (spec vehicles-3a §4–§5). Safe to re-run.</summary>
    public static class VehicleModelAssets
    {
        public const string MissingWarning = "[Vehicle] Model kendaraan tidak ada, dilewati: ";

        public static int Missing { get; private set; }

        [MenuItem("Tyco World/Vehicle/Build Vehicle Models")]
        static void Menu() => BuildAll(VehicleMaterials.LoadOrCreate(), new VehicleClipSet
        {
            Engine = VehicleAudioBuilder.Resolve(VehicleAudioBuilder.EngineName),
            Starter = VehicleAudioBuilder.Resolve(VehicleAudioBuilder.StarterName),
            Horn = VehicleAudioBuilder.Resolve(VehicleAudioBuilder.HornName),
            Bump = VehicleAudioBuilder.Resolve(VehicleAudioBuilder.BumpName)
        });

        public static VehicleCatalog BuildAll(VehicleMaterialSet materials, VehicleClipSet clips)
        {
            AssetFolders.Ensure(VehiclePaths.ModelPrefabsFolder);
            AssetFolders.Ensure(VehiclePaths.ModelTuningFolder);
            Missing = 0;
            var entries = new List<VehicleCatalog.Entry>();
            foreach (var spec in VehicleModelCatalog.All)
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(VehicleModelFiles.Path(spec.Id));
                var prefab = BuildOne(spec, source, materials, clips, out bool missing);
                if (missing)
                {
                    Missing++;
                    Debug.LogWarning(MissingWarning + spec.Id);
                    continue;
                }
                entries.Add(new VehicleCatalog.Entry
                {
                    id = spec.Id, vehicleClass = spec.Class, prefab = prefab,
                    paintMaterials = (string[])spec.PaintMaterials.Clone(), paintStrength = spec.PaintStrength
                });
            }
            var catalog = AssetDatabase.LoadAssetAtPath<VehicleCatalog>(VehiclePaths.CatalogAsset);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<VehicleCatalog>();
                AssetDatabase.CreateAsset(catalog, VehiclePaths.CatalogAsset);
            }
            catalog.entries = entries.ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        /// <returns>The saved prefab, or null with <paramref name="missing"/> set when the model file is absent.</returns>
        public static GameObject BuildOne(VehicleModelSpec spec, GameObject source, VehicleMaterialSet materials, VehicleClipSet clips, out bool missing)
        {
            missing = source == null;
            if (missing) return null;
            string tuningPath = VehiclePaths.ModelTuningFolder + "/" + spec.Id + ".asset";
            var tuning = AssetDatabase.LoadAssetAtPath<BikeTuning>(tuningPath);
            if (tuning == null)
            {
                tuning = ScriptableObject.CreateInstance<BikeTuning>();
                AssetDatabase.CreateAsset(tuning, tuningPath);
            }
            VehicleClassPresets.Apply(tuning, spec.Class);
            string path = VehiclePaths.ModelPrefabsFolder + "/" + spec.Id + ".prefab";
            var root = new GameObject(spec.Id);
            try
            {
                var bounds = VehicleModelBuilder.Build(root, spec, source, tuning, materials, clips);
                VehicleClassPresets.FitBody(tuning, bounds, spec.Class);
                Motorbike.ConfigureBody(root.GetComponent<CharacterController>(), tuning);
                EditorUtility.SetDirty(tuning);
                PlaceholderPropFactory.SaveMeshes(root, VehiclePaths.MeshesFolder);
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
```

  Di `VehicleAssetsBuilder.BuildAll`, setelah `a.MotorbikePrefab = SavePrefab(a);`, tambahkan:

```csharp
            a.Catalog = VehicleModelAssets.BuildAll(a.Materials, a.Clips);
```

  Lalu tambah field `public VehicleCatalog Catalog;` di `VehicleAssets`.

- [ ] **Step 8: Compile, jalankan `VehicleAssetsBuilder.BuildAll()` di satu perintah, lalu di perintah berikutnya jalankan `VehicleModelBuilderTests|VehicleModelAssetsTests|MotorbikeTests|MotorbikeVisualsTests|MotorbikeBuilderTests|VehicleAssetsTests`.** Expected: `failed=0`.

- [ ] **Step 9: Foto semua model.**
  - Lewat `Unity_RunCommand` di edit mode: instantiate tiap prefab dari katalog berjajar setiap 7 m di x = 7000 (lintasan uji jauh dari dunia, seperti `McpVehicleDriver.TrackStart`).
  - Kamera ke `McpRender.SavePng(cam, "<scratch>/vehicles/models_side.png", 1600, 600)` dari samping kiri, lalu sekali lagi dari depan.
  - Cek gambarnya: semua menghadap +Z (lampu depan di kanan pada foto samping) dan roda menyentuh tanah. Bersihkan objek setelahnya.
  - Model yang salah arah: perbaiki `YawFix`, ulangi Step 8, lalu catat `Ruling:`.

- [ ] **Step 10: Commit.**

```bash
git add Assets/Vehicles/Runtime/Visuals/MotorbikeVisuals.cs Assets/Vehicles/Runtime/Motorbike.cs Assets/Vehicles/Editor/MotorbikeBuilder.cs Assets/Vehicles/Editor/VehiclePaths.cs Assets/Vehicles/Editor/VehicleAssetsBuilder.cs Assets/Vehicles/Editor/Models/VehicleModelBuilder.cs Assets/Vehicles/Editor/Models/VehicleModelBuilder.cs.meta Assets/Vehicles/Editor/Models/VehicleModelAssets.cs Assets/Vehicles/Editor/Models/VehicleModelAssets.cs.meta Assets/Vehicles/Prefabs/Models Assets/Vehicles/Prefabs/Models.meta Assets/Vehicles/Prefabs/Meshes Assets/Vehicles/Config/Tuning Assets/Vehicles/Config/Tuning.meta Assets/Vehicles/Config/VehicleCatalog.asset Assets/Vehicles/Config/VehicleCatalog.asset.meta Assets/Vehicles/Tests/VehicleModelBuilderTests.cs Assets/Vehicles/Tests/VehicleModelBuilderTests.cs.meta Assets/Vehicles/Tests/VehicleModelAssetsTests.cs Assets/Vehicles/Tests/VehicleModelAssetsTests.cs.meta Assets/Vehicles/Tests/MotorbikeTests.cs Assets/Vehicles/Tests/MotorbikeVisualsTests.cs
git commit -m "feat(vehicles): build drivable prefabs from the downloaded models"
```

---

### Task 5: Anchor pengendara di Vario dan Supra

**Files:**
- Modify: `Assets/Vehicles/Editor/Models/VehicleModelCatalog.cs` (isi `Bike` untuk `vario` dan `supra`)
- Modify: `Assets/Vehicles/Editor/Models/VehicleModelBuilder.cs` (buat `RideAnchors` dari `spec.Bike`)
- Test: `Assets/Vehicles/Tests/VehicleModelBuilderTests.cs` (+2 tes)

**Interfaces:**
- Consumes: `RideAnchors` (#6), `VehicleModelBuilder.Build` (Task 4).
- Produces: setiap prefab motor punya `RideAnchors` di bawah `LeanPivot`, dengan pegangan di bawah `Steer`. `RideAnchors.Of(bike)` mengembalikan anchor itu, bukan membuat yang baru.

- [ ] **Step 1: Tulis tes yang gagal.**

```csharp
        [TestCase("vario")]
        [TestCase("supra")]
        public void Bike_HasAnchorsInARiderShape(string id)
        {
            var b = BuildModel(id);
            var a = root.GetComponentInChildren<RideAnchors>(true);
            Assert.IsNotNull(a, id);
            Assert.AreSame(a, RideAnchors.Of(root.GetComponent<Motorbike>()), "the model's own anchors, not the code scooter's");
            Assert.Greater(a.gripRight.position.y, a.seat.position.y, "grips above the seat");
            Assert.Greater(a.seat.position.y, a.footRight.position.y + 0.2f, "seat well above the feet");
            Assert.Greater(a.gripRight.position.z, a.seat.position.z, "grips ahead of the seat");
            Assert.Less(a.pillion.position.z, a.seat.position.z, "pillion behind the rider");
            Assert.AreEqual(-a.gripRight.localPosition.x, a.gripLeft.localPosition.x, 0.01f, "left mirrors right");
            Assert.AreEqual(-a.footRight.localPosition.x, a.footLeft.localPosition.x, 0.01f);
            var inside = b;
            inside.Expand(0.2f);
            foreach (var t in new[] { a.seat, a.gripLeft, a.gripRight, a.footLeft, a.footRight, a.pillion, a.pillionFootLeft, a.pillionFootRight })
                Assert.IsTrue(inside.Contains(t.position), id + " " + t.name + " on the bike");
            Assert.IsTrue(a.gripRight.IsChildOf(root.transform.Find(VehicleModelBuilder.LeanName + "/" + VehicleModelBuilder.SteerName)), "grips turn with the bar");
        }

        [Test]
        public void Car_HasNoRideAnchors()
        {
            BuildModel("hatch");
            Assert.IsNull(root.GetComponentInChildren<RideAnchors>(true));
        }
```

- [ ] **Step 2: Compile, lalu jalankan `VehicleModelBuilderTests`.** Expected: `Bike_HasAnchorsInARiderShape` gagal dengan pesan "Expected: not null" (anchor belum ada).

- [ ] **Step 3: Isi anchor awal dari bounds model.**
  - **Bentuk perkiraannya**, untuk motor dengan tinggi `h` dan panjang `L` yang depannya di +Z:
    - jok: `(0, 0.47h, 0)`;
    - pegangan kanan: `(0.33, 0.57h, 0.28L)`;
    - pijakan kanan: `(0.13, 0.25h, 0.12L)`;
    - boncengan: `(0, 0.47h, -0.25L)`;
    - pijakan boncengan kanan: `(0.24, 0.23h, -0.08L)`.
  - Lalu sesuaikan dengan bentuk model dari foto samping Task 4 Step 9:
    - jok berada di permukaan atas busa jok;
    - pegangan di ujung setang;
    - pijakan di dek kaki (Vario) atau pijakan kaki (Supra).
  - Tulis ke katalog:

```csharp
                Bike = new BikeAnchorSpec
                {
                    Seat = new Vector3(0f, 0.78f, -0.05f), GripRight = new Vector3(0.33f, 1.02f, 0.5f), FootRight = new Vector3(0.13f, 0.38f, 0.22f),
                    Pillion = new Vector3(0f, 0.8f, -0.5f), PillionFootRight = new Vector3(0.24f, 0.32f, -0.15f)
                }
```

  Angka di atas hanya contoh bentuk. Pakai angka hasil ukur model.

- [ ] **Step 4: Buat anchor di builder.** Di `VehicleModelBuilder.Build`, untuk `bike && spec.Bike != null`, sebelum `Motorbike.Configure`:

```csharp
            if (bike && spec.Bike != null)
            {
                var a = lean.gameObject.AddComponent<RideAnchors>();
                var bar = steer != null ? steer : lean;
                a.seat = Point("RiderSeat", lean, spec.Bike.Seat);
                a.gripRight = PointWorld("GripR", bar, spec.Bike.GripRight);
                a.gripLeft = PointWorld("GripL", bar, Mirror(spec.Bike.GripRight));
                a.footRight = Point("FootR", lean, spec.Bike.FootRight);
                a.footLeft = Point("FootL", lean, Mirror(spec.Bike.FootRight));
                a.pillion = Point("PillionSeat", lean, spec.Bike.Pillion);
                a.pillionFootRight = Point("PillionFootR", lean, spec.Bike.PillionFootRight);
                a.pillionFootLeft = Point("PillionFootL", lean, Mirror(spec.Bike.PillionFootRight));
                seat.localPosition = spec.Bike.Seat;
            }
```

  Helper-nya:

```csharp
        static Vector3 Mirror(Vector3 v) => new Vector3(-v.x, v.y, v.z);

        static Transform Point(string name, Transform parent, Vector3 local)
        {
            var t = NewChild(name, parent);
            t.localPosition = local;
            return t;
        }

        /// <summary>A point given in prefab space under a parent that is not at the origin (the steer pivot).</summary>
        static Transform PointWorld(string name, Transform parent, Vector3 prefabSpace)
        {
            var t = NewChild(name, parent);
            t.position = prefabSpace;
            return t;
        }
```

- [ ] **Step 5: Bangun ulang aset (`VehicleAssetsBuilder.BuildAll`), lalu di perintah berikutnya jalankan `VehicleModelBuilderTests`.** Expected: `failed=0`.

- [ ] **Step 6: Ukur pose di Play** (prosedur Play). Yang diukur adalah motor parkir warga dengan model Vario dan Supra. Gameplay baru memakai model di Task 7, jadi langkah ini dijalankan **setelah Task 7 selesai**; urutannya dicatat di ledger. Prosedurnya:
  - Pakai `CharacterRig` pejalan kaki. `CharacterRig.Ride(anchors, true)` untuk pengendara, `Ride(anchors, false)` untuk pembonceng.
  - Tunggu 3 frame, lalu ukur:
    - jarak tulang `LeftHand`/`RightHand` ke `gripLeft`/`gripRight`;
    - `LeftFoot`/`RightFoot` ke `footLeft`/`footRight`;
    - `Hips` ke `seat`.
  - Syarat: maksimal 0,06 m. Pinggul dihitung secara vertikal saja, dengan toleransi 0,08 m.
  - Ambil screenshot depan, samping, dan atas.
  - Kalau ada yang lewat batas, geser anchor di katalog, bangun ulang, lalu ukur lagi. Setiap iterasi dicatat di ledger.

- [ ] **Step 7: Commit.**

```bash
git add Assets/Vehicles/Editor/Models/VehicleModelCatalog.cs Assets/Vehicles/Editor/Models/VehicleModelBuilder.cs Assets/Vehicles/Prefabs/Models Assets/Vehicles/Tests/VehicleModelBuilderTests.cs
git commit -m "feat(vehicles): rider and pillion anchors fitted to the Vario and the Supra"
```

---

### Task 6: Motor kos menjadi Vario

**Files:**
- Modify: `Assets/Vehicles/Editor/VehicleAssetsBuilder.cs` (`SavePrefab`)
- Test: `Assets/Vehicles/Tests/VehicleAssetsTests.cs` (+1 tes, dan sesuaikan `Prefab_UsesOnlySavedAssets`)

**Interfaces:**
- Consumes: `VehicleModelBuilder.Build`, `VehicleModelCatalog.Get("vario")`, `VehicleModelFiles.Path("vario")`.
- Produces: `Motorbike.prefab` yang berisi model Vario, dengan root, `MotorbikeInteractable` (`kos-motor`), dan aset `BikeTuning.asset` yang tetap sama.

- [ ] **Step 1: Tulis tes yang gagal.**

```csharp
        [Test]
        public void KosBike_IsTheVario_AndKeepsItsInteraction()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VehiclePaths.MotorbikePrefab);
            Assert.IsNotNull(prefab.transform.Find(VehicleModelBuilder.LeanName + "/" + VehicleModelBuilder.ModelName), "the Vario model");
            Assert.IsNull(prefab.transform.Find(MotorbikeBuilder.LeanPivotName + "/" + MotorbikeBuilder.BodyName), "no code-built body left");
            Assert.AreEqual(MotorbikeBuilder.InteractionId, prefab.GetComponent<MotorbikeInteractable>().InteractionId);
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<BikeTuning>(VehiclePaths.TuningAsset), prefab.GetComponent<Motorbike>().Tuning);
            Assert.IsNotNull(prefab.GetComponentInChildren<RideAnchors>(true));
        }
```

  Kalau `MotorbikeInteractable` belum punya getter `InteractionId`, pakai getter yang sudah ada di kelas dasar `Interactable`. Cari dengan grep `InteractionId` di `Assets/Player/Runtime`.

- [ ] **Step 2: Jalankan `VehicleAssetsTests`.** Expected: `KosBike_IsTheVario_AndKeepsItsInteraction` gagal karena belum ada `LeanPivot/Model`.

- [ ] **Step 3: Ubah `SavePrefab`.** Builder yang dipakai untuk isi prefab:
  - Vario tersedia: `VehicleModelBuilder.Build(contents, VehicleModelCatalog.Get("vario"), vario, a.Tuning, a.Materials, a.Clips)`, lalu `VehicleClassPresets.FitBody(a.Tuning, bounds, VehicleClass.MotorMatic)` dan `GetOrAdd<MotorbikeInteractable>(contents).SetInteractionId(MotorbikeBuilder.InteractionId)`.
  - Vario tidak ada: `MotorbikeBuilder.Build` seperti sekarang.

  Karena Vario membangun ulang anak `LeanPivot`, sisa anak primitif yang namanya tidak dipakai builder model dihapus dulu: `Rider`, dan `Body`/`Handlebar` di bawah `LeanPivot`. Caranya, hapus `LeanPivot` lama seluruhnya; `VehicleModelBuilder.Build` sudah melakukannya.

```csharp
        static GameObject SavePrefab(VehicleAssets a)
        {
            string path = VehiclePaths.MotorbikePrefab;
            var vario = AssetDatabase.LoadAssetAtPath<GameObject>(VehicleModelFiles.Path("vario"));
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
            var contents = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject(MotorbikeBuilder.RootName);
            try
            {
                if (vario != null)
                {
                    var bounds = VehicleModelBuilder.Build(contents, VehicleModelCatalog.Get("vario"), vario, a.Tuning, a.Materials, a.Clips);
                    VehicleClassPresets.FitBody(a.Tuning, bounds, VehicleClass.MotorMatic);
                    Motorbike.ConfigureBody(contents.GetComponent<CharacterController>(), a.Tuning);
                    var interactable = contents.GetComponent<MotorbikeInteractable>();
                    if (interactable == null) interactable = contents.AddComponent<MotorbikeInteractable>();
                    interactable.SetInteractionId(MotorbikeBuilder.InteractionId);
                    EditorUtility.SetDirty(a.Tuning);
                }
                else
                {
                    MotorbikeBuilder.Build(contents, a.Tuning, a.Materials, a.Clips);
                }
                PlaceholderPropFactory.SaveMeshes(contents, VehiclePaths.MeshesFolder);
                return PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(contents);
                else Object.DestroyImmediate(contents);
            }
        }
```

  Urutan di `BuildAll`: `SavePrefab` tetap dipanggil sebelum `VehicleModelAssets.BuildAll`.

- [ ] **Step 4: Jalankan `VehicleAssetsBuilder.BuildAll()` dan `VehicleSceneSetup.Run(false, out _)`, lalu di perintah berikutnya jalankan `VehicleAssetsTests|MotorbikeTests|VehicleSceneSetup`.** Expected: `failed=0`. Instance `Motorbike` di `World.unity` tetap di posisi parkir kos.

- [ ] **Step 5: Commit.** Stage `World.unity` hanya kalau berubah.

```bash
git add Assets/Vehicles/Editor/VehicleAssetsBuilder.cs Assets/Vehicles/Prefabs/Motorbike.prefab Assets/Vehicles/Prefabs/Meshes Assets/Vehicles/Config/BikeTuning.asset Assets/Vehicles/Tests/VehicleAssetsTests.cs
git commit -m "feat(vehicles): the kos motorbike is the Vario model"
```

---

### Task 7: Kendaraan warga memakai katalog

**Files:**
- Create: `Assets/Gameplay/Runtime/Vehicles/VehicleTint.cs`
- Modify: `Assets/Gameplay/Runtime/Vehicles/VehicleFactory.cs`
- Modify: `Assets/Gameplay/Runtime/Vehicles/WorldVehicle.cs`
- Modify: `Assets/Gameplay/Runtime/Vehicles/VehicleSystem.cs`
- Modify: `Assets/Gameplay/Runtime/KampungDirector.cs`
- Modify: `Assets/Gameplay/Editor/GameplaySceneSetup.cs`
- Test: `Assets/Gameplay/Tests/VehicleFactoryModelTests.cs`, `Assets/Gameplay/Tests/VehicleTintTests.cs`

**Interfaces:**
- Consumes: `VehicleCatalog` (Task 2/4), `VehicleClasses.Noun`.
- Produces:
  - `KampungDirector.Vehicles : VehicleCatalog`, dan `Configure(..., VehicleCatalog vehicles = null)` sebagai parameter terakhir.
  - `VehicleFactory.ParkedModelId(int spotIndex, VehicleKind kind) : string`.
  - `VehicleFactory.FromCatalog(Transform parent, VehicleCatalog catalog, string id, VehicleTint tint, Color32 colour, Palette palette, BikeTuning carTuning, GameObject bikePrefab) : Motorbike`. Kalau prefab tidak ada, factory jatuh ke primitif.
  - `VehicleTint.Apply(GameObject vehicle, VehicleCatalog.Entry entry, Color32 colour)`.
  - `WorldVehicle.ModelId : string`, `WorldVehicle.Class : VehicleClass`, dan `Noun` dari kelas.
  - `VehicleFactory.SetShadows(GameObject vehicle, bool cast)`; `VehicleSystem.ShadowRange = 40f` (spec §8).

- [ ] **Step 1: Tulis tes yang gagal.**

`VehicleTintTests.cs`:

```csharp
using NUnit.Framework;
using TycoWorld.Vehicles;
using UnityEngine;

namespace TycoWorld.Gameplay.Tests
{
    /// <summary>Repaint by tinting the model's body material (spec vehicles-3a §4), one material per colour.</summary>
    public class VehicleTintTests
    {
        GameObject car;
        Material body;
        VehicleTint tint;

        [SetUp]
        public void SetUp()
        {
            body = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Body" };
            body.SetColor("_BaseColor", Color.white);
            car = GameObject.CreatePrimitive(PrimitiveType.Cube);
            car.GetComponent<MeshRenderer>().sharedMaterial = body;
            tint = new VehicleTint();
        }

        [TearDown]
        public void TearDown()
        {
            tint.Dispose();
            Object.DestroyImmediate(car);
            Object.DestroyImmediate(body);
        }

        [Test]
        public void BodyMaterial_IsTintedFully_AndShared()
        {
            var entry = new VehicleCatalog.Entry { paintMaterials = new[] { "Body" }, paintStrength = 1f };
            var red = new Color32(180, 40, 40, 255);
            tint.Apply(car, entry, red);
            var first = car.GetComponent<MeshRenderer>().sharedMaterial;
            Assert.AreNotSame(body, first, "never edits the imported material");
            Assert.AreEqual((Color)red, first.GetColor("_BaseColor"));
            var other = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                other.GetComponent<MeshRenderer>().sharedMaterial = body;
                tint.Apply(other, entry, red);
                Assert.AreSame(first, other.GetComponent<MeshRenderer>().sharedMaterial, "one material per (source, colour)");
            }
            finally
            {
                Object.DestroyImmediate(other);
            }
        }

        [Test]
        public void AtlasModel_IsTintedAtPaintStrength()
        {
            var entry = new VehicleCatalog.Entry { paintMaterials = new[] { "Body" }, paintStrength = 0.6f };
            tint.Apply(car, entry, new Color32(0, 0, 255, 255));
            var c = car.GetComponent<MeshRenderer>().sharedMaterial.GetColor("_BaseColor");
            Assert.AreEqual(0.4f, c.r, 0.01f, "lerp(white, blue, 0.6)");
            Assert.AreEqual(1f, c.b, 0.01f);
        }

        [Test]
        public void FixedLivery_IsLeftAlone()
        {
            tint.Apply(car, new VehicleCatalog.Entry(), new Color32(0, 255, 0, 255));
            Assert.AreSame(body, car.GetComponent<MeshRenderer>().sharedMaterial);
        }
    }
}
```

`VehicleFactoryModelTests.cs`:

```csharp
using System.Linq;
using NUnit.Framework;
using TycoWorld.Vehicles;
using UnityEngine;

namespace TycoWorld.Gameplay.Tests
{
    /// <summary>Warga vehicles from the model catalog (spec vehicles-3a §6).</summary>
    public class VehicleFactoryModelTests
    {
        [Test]
        public void ParkedModels_AreDeterministic_AndFromTheRightKind()
        {
            var bikes = new[] { "vario", "supra" };
            var cars = new[] { "sedan", "hatch", "lmpv1", "lmpv2", "mpv2", "van", "l300", "pickup", "carry", "angkot" };
            for (int i = 0; i < 40; i++)
            {
                Assert.AreEqual(VehicleFactory.ParkedModelId(i, VehicleKind.Bike), VehicleFactory.ParkedModelId(i, VehicleKind.Bike));
                CollectionAssert.Contains(bikes, VehicleFactory.ParkedModelId(i, VehicleKind.Bike));
                CollectionAssert.Contains(cars, VehicleFactory.ParkedModelId(i, VehicleKind.Car));
            }
            Assert.AreEqual(2, Enumerable.Range(0, 40).Select(i => VehicleFactory.ParkedModelId(i, VehicleKind.Bike)).Distinct().Count());
            Assert.GreaterOrEqual(Enumerable.Range(0, 40).Select(i => VehicleFactory.ParkedModelId(i, VehicleKind.Car)).Distinct().Count(), 8);
        }

        [Test]
        public void MissingPrefab_FallsBackToTheCodeVehicle()
        {
            var parent = new GameObject("Parked").transform;
            var catalog = ScriptableObject.CreateInstance<VehicleCatalog>();
            var palette = new Palette(null);
            var tint = new VehicleTint();
            var carTuning = VehicleFactory.CarTuning(null);
            try
            {
                var car = VehicleFactory.FromCatalog(parent, catalog, "sedan", tint, new Color32(200, 0, 0, 255), palette, carTuning, null);
                Assert.IsNotNull(car);
                Assert.IsNotNull(car.transform.Find(VehicleFactory.PaintName), "the code-built car");
            }
            finally
            {
                Object.DestroyImmediate(parent.gameObject);
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(carTuning);
                tint.Dispose();
                palette.Dispose();
            }
        }

        [Test]
        public void Noun_FollowsTheClass()
        {
            Assert.AreEqual("angkot", VehicleClasses.Noun(VehicleClass.Angkot));
        }

        [Test]
        public void SetShadows_TogglesEveryRendererOfTheVehicle()
        {
            var go = new GameObject("Car");
            try
            {
                var a = GameObject.CreatePrimitive(PrimitiveType.Cube);
                a.transform.SetParent(go.transform, false);
                var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                b.transform.SetParent(a.transform, false);
                VehicleFactory.SetShadows(go, false);
                Assert.IsTrue(go.GetComponentsInChildren<Renderer>().All(r => r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off));
                VehicleFactory.SetShadows(go, true);
                Assert.IsTrue(go.GetComponentsInChildren<Renderer>().All(r => r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.On));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
```

  Kalau `Palette` tidak punya `Dispose`, pakai metode pembersih yang sudah ada (cek grep `Palette` di `KampungDirector.OnDestroy`).

- [ ] **Step 2: Compile, lalu jalankan `VehicleTintTests|VehicleFactoryModelTests`.** Expected: compile error, karena tipenya belum ada.

- [ ] **Step 3: Implementasi.**

`VehicleTint.cs`:

```csharp
using System;
using System.Collections.Generic;
using TycoWorld.Vehicles;
using UnityEngine;

namespace TycoWorld.Gameplay
{
    /// <summary>
    /// Paints a model vehicle (spec vehicles-3a §4): its listed body materials are swapped for tinted copies, one copy per
    /// (source material, colour), so the SRP Batcher keeps batching. paintStrength below 1 lerps from white so an atlas's
    /// glass and lights do not turn the paint colour.
    /// </summary>
    public sealed class VehicleTint : IDisposable
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        readonly Dictionary<(Material, int), Material> cache = new Dictionary<(Material, int), Material>();

        public void Apply(GameObject vehicle, VehicleCatalog.Entry entry, Color32 colour)
        {
            if (vehicle == null || entry == null || entry.paintMaterials == null || entry.paintMaterials.Length == 0) return;
            var tintColour = Color.Lerp(Color.white, colour, Mathf.Clamp01(entry.paintStrength));
            int key = ((Color32)tintColour).GetHashCode();
            foreach (var r in vehicle.GetComponentsInChildren<Renderer>(true))
            {
                var slots = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < slots.Length; i++)
                {
                    var source = Source(slots[i]);
                    if (source == null || Array.IndexOf(entry.paintMaterials, source.name) < 0) continue;
                    if (!cache.TryGetValue((source, key), out var copy))
                    {
                        copy = new Material(source) { name = source.name, hideFlags = HideFlags.DontSave };
                        if (copy.HasProperty(BaseColor)) copy.SetColor(BaseColor, tintColour);
                        cache[(source, key)] = copy;
                        sources[copy] = source;
                    }
                    slots[i] = copy;
                    changed = true;
                }
                if (changed) r.sharedMaterials = slots;
            }
        }

        readonly Dictionary<Material, Material> sources = new Dictionary<Material, Material>();

        /// <summary>The imported material behind a slot that may already hold one of our copies (repaint).</summary>
        Material Source(Material slot)
        {
            if (slot == null) return null;
            return sources.TryGetValue(slot, out var s) ? s : slot;
        }

        public void Dispose()
        {
            foreach (var m in cache.Values)
                if (m != null) UnityEngine.Object.DestroyImmediate(m);
            cache.Clear();
            sources.Clear();
        }
    }
}
```

`VehicleFactory`:
- Tambah `ParkedModelId`:

```csharp
        static readonly string[] ParkedBikes = { "vario", "supra" };
        static readonly string[] ParkedCars = { "sedan", "hatch", "lmpv1", "lmpv2", "mpv2", "van", "l300", "pickup", "carry", "angkot" };

        /// <summary>Which model stands on a parking spot: bikes alternate, cars spread over the ten parked models (spec vehicles-3a §6).</summary>
        public static string ParkedModelId(int spotIndex, VehicleKind kind) => kind == VehicleKind.Bike
            ? ParkedBikes[((spotIndex % 2) + 2) % 2]
            : ParkedCars[(int)(SeededRandom.Mix((ulong)spotIndex, 0xCA7A10UL) % (ulong)ParkedCars.Length)];
```

- Tambah `FromCatalog`:

```csharp
        public static Motorbike FromCatalog(Transform parent, VehicleCatalog catalog, string id, VehicleTint tint, Color32 colour, Palette palette,
            BikeTuning carTuning, GameObject bikePrefab)
        {
            var entry = catalog != null ? catalog.Get(id) : null;
            if (entry == null || entry.prefab == null)
            {
                Debug.LogWarning("[Gameplay] Model kendaraan '" + id + "' tidak ada; memakai bentuk sederhana.");
                return id == "vario" || id == "supra" ? Bike(parent, bikePrefab, palette, colour) : Car(parent, carTuning, palette, colour);
            }
            var go = Object.Instantiate(entry.prefab, parent, false);
            go.name = VehicleClasses.IsBike(entry.vehicleClass) ? "WargaBike" : "WargaCar";
            tint.Apply(go, entry, colour);
            Activate(go);
            return go.GetComponent<Motorbike>();
        }
```

- `Paint` yang sudah ada tetap dipakai untuk kendaraan primitif. `VehicleSystem` memanggil `tint.Apply` untuk model dan `Paint` untuk primitif (lihat Step 4).
- **`WorldVehicle`:**
  - Tambah `public readonly string ModelId;` dan `public readonly VehicleClass Class;`. Keduanya diisi lewat konstruktor dengan parameter baru `(VehicleKind kind, Motorbike body, VehicleSystem system, string modelId = null, VehicleClass cls = VehicleClass.MotorMatic)`.
  - Ganti `Noun` menjadi:

```csharp
        public string Noun => ModelId != null ? VehicleClasses.Noun(Class) : (Kind == VehicleKind.Bike ? "motor" : "mobil");
```

- **`KampungDirector`:** tambah `[SerializeField] VehicleCatalog vehicles;`, `public VehicleCatalog Vehicles => vehicles;`, dan parameter terakhir `VehicleCatalog vehicleCatalog = null` di `Configure` yang mengisi `vehicles`.
- **`GameplaySceneSetup`:** muat `AssetDatabase.LoadAssetAtPath<VehicleCatalog>(TycoWorld.Vehicles.Authoring.VehiclePaths.CatalogAsset)`, lalu teruskan ke `director.Configure(..., vehicles)`.

- [ ] **Step 4: `VehicleSystem.Spawn` memakai model.**
  - Field baru: `VehicleTint tint = new VehicleTint();`. Panggil `Dispose()` di `End()`.
  - Pool reuse harus cocok dengan `ModelId`, bukan hanya `Kind`.

```csharp
            string modelId = VehicleFactory.ParkedModelId(spotIndex, kind);
            foreach (var pooled in vehicles)
                if (!pooled.Active && pooled.Kind == kind && pooled.ModelId == modelId)
                {
                    v = pooled;
                    break;
                }
            int colour = (int)(SeededRandom.Mix((ulong)spotIndex, 0xC0105UL) % 6UL);
            var colourValue = kind == VehicleKind.Bike ? VehicleFactory.BikeColour(colour) : VehicleFactory.CarColour(colour);
            if (v == null)
            {
                var body = VehicleFactory.FromCatalog(root, director.Vehicles, modelId, tint, colourValue, director.Palette, carTuning, director.BikePrefab);
                var entry = director.Vehicles != null ? director.Vehicles.Get(modelId) : null;
                v = entry != null && entry.prefab != null
                    ? new WorldVehicle(kind, body, this, modelId, entry.vehicleClass)
                    : new WorldVehicle(kind, body, this);
                vehicles.Add(v);
            }
```

  - Baris `VehicleFactory.Paint(...)` setelah blok itu, dan di bengkel (baris ~495), diganti dengan helper:

```csharp
        void Repaint(WorldVehicle v, Color32 colour)
        {
            var entry = v.ModelId != null && director.Vehicles != null ? director.Vehicles.Get(v.ModelId) : null;
            if (entry != null) tint.Apply(v.Root, entry, colour);
            else VehicleFactory.Paint(v.Root, director.Palette, colour);
        }
```

- [ ] **Step 4b: Bayangan menurut jarak (spec §8).**
  - `VehicleFactory.SetShadows`:

```csharp
        /// <summary>Parked vehicles past the shadow range cast none (spec vehicles-3a §8), like far pedestrians.</summary>
        public static void SetShadows(GameObject vehicle, bool cast)
        {
            var mode = cast ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            foreach (var r in vehicle.GetComponentsInChildren<Renderer>(true))
                if (r.shadowCastingMode != mode) r.shadowCastingMode = mode;
        }
```

  - Di `VehicleSystem`, setiap pemeriksaan `CheckEvery` (0,5 s), untuk tiap kendaraan aktif:
    - panggil `VehicleFactory.SetShadows(v.Root, MissionContext.Flat(v.Position, viewer) <= ShadowRange)`;
    - `viewer` adalah posisi kamera atau pemain yang sudah dipakai `VehicleSystem` untuk jarak spawn;
    - simpan status terakhir di `WorldVehicle.CastsShadow`, supaya renderer hanya diubah saat statusnya berganti.

- [ ] **Step 5: Compile, lalu jalankan `Gameplay`.** Expected: `failed=0`, termasuk `VehicleTheftTests` yang lama.

- [ ] **Step 6: Jalankan `GameplaySceneSetup.Run(false, out _)` (kecuali kalau user sedang main), lalu jalankan `TycoWorld` (suite penuh).** Expected: `failed=0`.

- [ ] **Step 7: Commit.**

```bash
git add Assets/Gameplay/Runtime/Vehicles/VehicleTint.cs Assets/Gameplay/Runtime/Vehicles/VehicleTint.cs.meta Assets/Gameplay/Runtime/Vehicles/VehicleFactory.cs Assets/Gameplay/Runtime/Vehicles/WorldVehicle.cs Assets/Gameplay/Runtime/Vehicles/VehicleSystem.cs Assets/Gameplay/Runtime/KampungDirector.cs Assets/Gameplay/Editor/GameplaySceneSetup.cs Assets/Gameplay/Tests/VehicleTintTests.cs Assets/Gameplay/Tests/VehicleTintTests.cs.meta Assets/Gameplay/Tests/VehicleFactoryModelTests.cs Assets/Gameplay/Tests/VehicleFactoryModelTests.cs.meta
git commit -m "feat(gameplay): parked warga vehicles use the model catalog, tinted per colour"
```

  `World.unity` ikut di-stage kalau setup mengubahnya.

---

### Task 8: Menu Kredit

**Files:**
- Create: `Assets/Vehicles/Editor/CreditsBuilder.cs`. Builder ini sengaja ditaruh di editor Vehicles, karena itu satu-satunya assembly editor yang sudah mereferensikan WorldGen.Editor, Player.Editor, dan DayCycle.Editor. Ia hanya membaca file.
- Create: `Assets/GameSettings/Resources/Credits.txt`
- Modify: `Assets/GameSettings/Runtime/UI/MenuPage.cs`, `PauseMenuView.cs`, `SettingsText.cs`, `Assets/GameSettings/Runtime/Menu/GameMenu.cs`
- Test: `Assets/GameSettings/Tests/PauseMenuViewTests.cs` (+2 tes), `Assets/Vehicles/Tests/CreditsBuilderTests.cs`

**Interfaces:**
- Produces:
  - `MenuPage.Credits`.
  - `PauseMenuView.CreditsName = "open-credits"`, `CreditsPanelName = "credits-panel"`, `CreditsBackName = "credits-back"`, `CreditsTextName = "credits-text"`.
  - Event `CreditsClicked` dan `CreditsBackClicked`; `SetCredits(string text)`.
  - `CreditsBuilder.Collect() : string` dan `CreditsBuilder.Write()`, yang menulis ke `CreditsBuilder.OutputPath = "Assets/GameSettings/Resources/Credits.txt"`.
  - `SettingsText.Credits = "Kredit"`, `SettingsText.Back = "Kembali"`. Kalau `"Kembali"` sudah ada, pakai konstanta yang sudah ada.

- [ ] **Step 1: Tulis tes yang gagal.**

`CreditsBuilderTests.cs`:

```csharp
using System.IO;
using System.Linq;
using NUnit.Framework;
using TycoWorld.Vehicles.Authoring;

namespace TycoWorld.Vehicles.Tests
{
    /// <summary>Every third-party asset is credited in the game (spec vehicles-3a §2).</summary>
    public class CreditsBuilderTests
    {
        [Test]
        public void Credits_ListEveryRowOfEverySourcesFile()
        {
            string text = CreditsBuilder.Collect();
            var files = Directory.GetFiles("Assets", "SOURCES.md", SearchOption.AllDirectories);
            Assert.IsNotEmpty(files);
            foreach (var file in files)
                foreach (var line in File.ReadAllLines(file).Where(l => l.StartsWith("| `")))
                {
                    var cells = line.Split('|').Select(c => c.Trim()).Where(c => c.Length > 0).ToArray();
                    string model = cells[1];
                    StringAssert.Contains(model, text, file);
                }
            StringAssert.Contains("Han66st", text);
            StringAssert.Contains("CC Attribution 4.0", text);
        }

        [Test]
        public void WrittenFile_MatchesTheCollection()
        {
            Assert.AreEqual(CreditsBuilder.Collect(), File.ReadAllText(CreditsBuilder.OutputPath));
        }
    }
}
```

Tambahkan ke `PauseMenuViewTests.cs`:

```csharp
        [Test]
        public void HasACreditsButton_ThatOpensItsOwnPage()
        {
            var button = root.Q<Button>(PauseMenuView.CreditsName);
            Assert.IsNotNull(button);
            Assert.AreEqual(SettingsText.Credits, button.text);
            view.Show(MenuPage.Credits);
            Assert.AreEqual(DisplayStyle.Flex, root.Q(PauseMenuView.CreditsPanelName).style.display.value);
            Assert.AreEqual(DisplayStyle.None, root.Q(PauseMenuView.PanelName).style.display.value);
        }

        [Test]
        public void SetCredits_ShowsTheText()
        {
            view.SetCredits("Han66st — CC Attribution 4.0");
            StringAssert.Contains("Han66st", root.Q<Label>(PauseMenuView.CreditsTextName).text);
        }
```

  Nama field `root`/`view` disesuaikan dengan `SetUp` yang sudah ada di file tes itu.

- [ ] **Step 2: Compile, lalu jalankan `CreditsBuilderTests|PauseMenuViewTests`.** Expected: compile error.

- [ ] **Step 3: Implementasi.**

`CreditsBuilder.cs`:

```csharp
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;

namespace TycoWorld.Vehicles.Authoring
{
    /// <summary>Gathers every SOURCES.md table row in Assets into the in-game credits text (spec vehicles-3a §2). Sorted by path.</summary>
    public static class CreditsBuilder
    {
        public const string OutputPath = "Assets/GameSettings/Resources/Credits.txt";

        [MenuItem("Tyco World/Write Credits")]
        public static void Write()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            File.WriteAllText(OutputPath, Collect());
            AssetDatabase.ImportAsset(OutputPath);
        }

        public static string Collect()
        {
            var sb = new StringBuilder();
            sb.Append("Tyco World memakai aset pihak ketiga berikut. Terima kasih kepada para pembuatnya.\n\n");
            foreach (var file in Directory.GetFiles("Assets", "SOURCES.md", SearchOption.AllDirectories).Select(f => f.Replace('\\', '/')).OrderBy(f => f, System.StringComparer.Ordinal))
            {
                var rows = File.ReadAllLines(file).Where(l => l.StartsWith("| `")).ToList();
                if (rows.Count == 0) continue;
                foreach (var row in rows)
                {
                    var cells = row.Split('|').Select(c => c.Trim()).Where(c => c.Length > 0).ToArray();
                    if (cells.Length < 5) continue;
                    // | file | model | author | source | licence |
                    sb.Append(cells[1]).Append(" — ").Append(cells[2]).Append(" — ").Append(cells[4]).Append('\n').Append("   ").Append(cells[3]).Append('\n');
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
```

  Kalau ada `SOURCES.md` lama yang kolomnya tidak berurutan file | model | pembuat | sumber | lisensi, misalnya audio, ubah dulu tabel file itu ke urutan tersebut. Ini bagian dari task ini: tes Step 1 memeriksa kolom kedua.

- **`MenuPage`:** tambah `Credits` di akhir enum.
- **`PauseMenuView`:**
  - Tombol `MakeButton(CreditsName, SettingsText.Credits, () => CreditsClicked?.Invoke())`, disisipkan di antara tombol Pengaturan dan Keluar.
  - Panel `MenuStyle.Panel(CreditsPanelName, 640f)` yang berisi:
    - judul;
    - `ScrollView` (tinggi maksimal 420) dengan `Label` bernama `CreditsTextName` yang memakai `whiteSpace = WhiteSpace.Normal`;
    - tombol `CreditsBackName` dengan teks `SettingsText.Back` yang memicu `CreditsBackClicked`.
  - Di `Show`, tambahkan `credits.style.display = page == MenuPage.Credits ? DisplayStyle.Flex : DisplayStyle.None;`.
  - Tambah `public void SetCredits(string text) => creditsText.text = text;`.
- **`GameMenu.Build`:**
  - `pauseView.CreditsClicked += () => navigator.Go(MenuPage.Credits);`
  - `pauseView.CreditsBackClicked += () => navigator.Go(MenuPage.Main);`
  - `var credits = Resources.Load<TextAsset>("Credits"); pauseView.SetCredits(credits != null ? credits.text : "");`
  - Esc dari Kredit sudah kembali ke Main lewat `MenuNavigator.Escape` (cabang `default`).
- **`VehicleAssetsBuilder.BuildAll`:** di akhir, panggil `CreditsBuilder.Write();` supaya file kredit selalu ikut diperbarui.

- [ ] **Step 4: Compile, jalankan `CreditsBuilder.Write()`, lalu di perintah berikutnya jalankan `CreditsBuilderTests|PauseMenuViewTests|GameMenu`.** Expected: `failed=0`.

- [ ] **Step 5: Commit.**

```bash
git add Assets/Vehicles/Editor/CreditsBuilder.cs Assets/Vehicles/Editor/CreditsBuilder.cs.meta Assets/GameSettings/Resources Assets/GameSettings/Resources.meta Assets/GameSettings/Runtime/UI/MenuPage.cs Assets/GameSettings/Runtime/UI/PauseMenuView.cs Assets/GameSettings/Runtime/UI/SettingsText.cs Assets/GameSettings/Runtime/Menu/GameMenu.cs Assets/GameSettings/Tests/PauseMenuViewTests.cs Assets/Vehicles/Tests/CreditsBuilderTests.cs Assets/Vehicles/Tests/CreditsBuilderTests.cs.meta Assets/Vehicles/Editor/VehicleAssetsBuilder.cs
git commit -m "feat(menu): credits page listing every third-party asset"
```

---

### Task 9: Verifikasi di Play, dokumen

**Files:**
- Create: `docs/superpowers/notes/2026-10-15-vehicles-3a-verification.md`

- [ ] **Step 1: Jalankan suite penuh `TycoWorld`.** Expected: `failed=0`.

- [ ] **Step 2: Siapkan Play** (prosedur Play). Set jam 10:00.

- [ ] **Step 3: Pose pengendara** (Task 5 Step 6), untuk Vario dan Supra sebagai pengendara dan pembonceng. Catat jarak tiap anchor ke tulangnya, lalu ambil screenshot depan, samping, dan atas.

- [ ] **Step 4: Kendarai tiap kelas di lintasan uji** (`McpVehicleDriver.TestTrack`). Langkahnya:
  1. Instantiate prefab tiap kelas dari katalog: vario, supra, sedan, lmpv1, van, angkot, l300, elf, police.
  2. `Mount` dengan pemain.
  3. `ScriptedVehicleIntentSource.Hold(Throttle = 1)` selama 10 s, lalu catat kecepatan maksimal. Syarat: ±10% dari `VehicleClassPresets.MaxKmh`.
  4. Belok penuh 3 s, lalu cek kendaraan tidak tersangkut (kecepatan lebih dari 0 dan posisi berubah).
  5. Turun (`Dismount`), lalu cek kaki pemain berada di luar hull.
  6. Kembalikan kendaraan dan pemain.

  Pakai `First(b => b.enabled && b.name == ...)`, bukan `FindAnyObjectByType`.

- [ ] **Step 5: Kendaraan parkir warga.**
  - Teleport ke dua titik parkir mobil dan dua titik parkir motor.
  - Ambil screenshot dan cek model serta warnanya.
  - Curi satu mobil model baru (`McpGameplayDriver.GoToVehicle("car")`, lalu `Steal`), kendarai, lalu cat ulang di bengkel. Warnanya harus berubah.
  - Buka menu jeda, lalu Kredit, dan ambil screenshot.

- [ ] **Step 6: Perf.** Pakai sweep 6 langkah yang sama dengan sub-proyek 2: kos/road × none/base, 300 frame, jam 10:00. Jalankan saat tidak ada proses berat lain (cek proses `grep`/worker). Syarat: batch base naik paling banyak 3% dibanding angka terakhir sub-proyek 2, yaitu kos 7742/8273 dan jalan 5967.

- [ ] **Step 7: Keluar Play.** Verifikasi atau pulihkan save, lalu jalankan suite penuh lagi.

- [ ] **Step 8: Tulis catatan verifikasi.** Format mengikuti `2026-10-15-road-clearance-verification.md`. Isinya:
  - aset (tabel 14 model, segitiga, material);
  - hasil impor (roda per model, roda yang menyatu dengan bodi);
  - pose (jarak);
  - handling (kecepatan maksimal per kelas);
  - kendaraan parkir;
  - kredit;
  - perf;
  - tes;
  - sisa.

  Kalau ada penyimpangan baru, perbarui juga spec 3A.

- [ ] **Step 9: Commit dan push.**

```bash
git add docs/superpowers/notes/2026-10-15-vehicles-3a-verification.md docs/superpowers/specs/2026-10-15-vehicles-3a-assets-design.md
git commit -m "docs(vehicles): 3A verification"
git push -u origin feature/vehicles
```
