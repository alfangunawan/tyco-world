# Street Frontage + Road Dressing — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Jalan di dunia generate punya tepi yang rapi (bahu + selokan, trotoar berkerb, marka tengah), dan koridor ±15 m di kiri-kanan jalan tidak lagi bolong. Caranya: tembok, pagar, dan gerbang di property line, ditambah rumah infill di kavling kosong.

**Architecture:**
- Semua dikerjakan di pipeline editor `WorldMeshPipeline.Build` (pure, deterministik):
  1. `RoadNetwork` menggabungkan way OSM menjadi chain, lalu menentukan profil tepi per sisi.
  2. `StreetContext` menyediakan query spasial (bangunan, air, area, kos).
  3. `RoadEdgeBuilder` dan `RoadMarkingBuilder` membuat tepi jalan dan marka.
  4. `FrontagePlanner` mengklasifikasi property line. Pass 1 mencari kavling kosong, lalu `InfillPlanner` mengisinya. Pass 2 menghasilkan wall run.
  5. `FrontageMeshBuilder` membuat mesh render dan collider per chunk.
- Output digabung per chunk 100 m sebagai `BuiltFeature` baru (`RoadEdges/…/Gutter|Sidewalk`, `Frontage/…/Walls`, `Buildings/…/Infill`), lalu ditulis lewat `ChunkAssetWriter` dan `SceneObjectFactory` yang sudah ada.

**Tech Stack:** Unity 6000.6.5f1, URP 17.6.0 (Simple Lit), Unity Test Framework 1.8.0, Unity MCP (`mcp__unity-mcp__Unity_RunCommand`), Bash, curl (ambientCG).

**Spec:** `docs/superpowers/specs/2026-10-14-street-frontage-design.md` (commit `00dda02`).

## Global Constraints

**Proyek dan branch**
- Project root `D:\UNITY\project\Tyco World`.
- Branch kerja `feature/street-frontage` (sudah ada, dari `main` = `38a40ee`; spec di `d335aa2` + `00dda02`).
- Unity 6000.6.5f1, URP 17.6.0, `com.unity.test-framework` 1.8.0.

**Assembly dan namespace**
- Kode baru masuk `TycoWorld.WorldGen.Editor` (namespace `TycoWorld.WorldGen.Authoring`), kecuali dua file runtime (`TycoWorld.WorldGen`, namespace `TycoWorld.WorldGen`):
  - `FeatureKind.cs`;
  - `Culling/WorldChunkIndex.cs`.
- Tes masuk `TycoWorld.WorldGen.Tests` (namespace `TycoWorld.WorldGen.Tests`). Pengecualian, tes fisika kerb:
  - `TycoWorld.Player.Tests` (`FirstPersonPlayerTests`);
  - `TycoWorld.Vehicles.Tests` (`MotorbikeTests`).
- Arah dependensi tidak berubah: WorldGen tidak mereferensikan Player/Vehicles/DayCycle.

**Aturan C# dan API Unity**
- C# 9: tidak ada `record`, `init`, `^`.
- Format angka di string memakai `CultureInfo.InvariantCulture`.
- Unity 6.6:
  - pakai `Object.FindObjectsByType<T>()` / `Object.FindAnyObjectByType<T>()` (tanpa `FindObjectsSortMode`);
  - jangan pakai API instance-ID berbasis `int`.
- Jangan pakai `??` / `?.` / `??=` pada `UnityEngine.Object` (pada objek C# biasa boleh). Ganti dengan `x != null ? x : y`.
- File yang punya `using System;` dan `using UnityEngine;` sekaligus menulis `UnityEngine.Object` secara eksplisit.
- Kata kunci C# (`short`, `base`, `out`, …) tidak dipakai sebagai nama variabel.

**Determinisme (spec §11)**
- Semua keacakan berasal dari `SplitMix64`, dengan seed dari id stabil:
  - id OSM bangunan;
  - `RoadChain.Key`;
  - posisi s terkuantisasi.
- Tidak ada `UnityEngine.Random` dan tidak ada `System.Random`.
- Output tidak boleh bergantung pada urutan iterasi `Dictionary`/`HashSet`. Kunci chunk diurutkan sebelum dipakai, dan `HashSet` hanya untuk keanggotaan atau min/max.
- Generate dua kali harus memberi hash chunk yang IDENTICAL.

**Nilai**
- Semua angka tepi, frontage, dan infill hanya ditulis sebagai field initializer di `StreetDressingSettings.cs`, disalin dari spec §12.
- Kode lain membaca nilai dari settings, kecuali konstanta geometri kecil yang dinamai di kelasnya (`RailSize`, `GateFrame`, …).

**File proyek**
- Jangan mengubah ProjectSettings.
- Jangan pernah `git add ProjectSettings`, `git add -A`, atau `git add .` (`ProjectSettings/ProjectSettings.asset` punya perubahan user yang belum di-commit).
- `Assets/WorldGen/Generated/` di-ignore git (hasil Generate). Jangan pernah meng-add-nya.
- Workspace ledger `.superpowers/sdd/2026-10-14-street-frontage/` di-ignore git (lewat `.superpowers/sdd/.gitignore`). Screenshot, file hash, dan unduhan disimpan di sana.

**Commit**
- Setiap commit diakhiri dua baris:
  - `Co-Authored-By: <model yang menulis kode> <noreply@anthropic.com>`
  - `Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5`

**Deviasi kecil yang disengaja dari spec**
- Ada unit baru `StreetContext` (Task 4) yang tidak tercantum di tabel unit spec §3. Isinya query spasial bersama untuk tepi, frontage, dan infill, supaya ketiga builder tidak menduplikasi grid bangunan, air, dan area.
- `RoadChain.Key` (hash id chain + titik awal terkuantisasi) dipakai sebagai seed, bukan `chainId` mentah. Dua potongan dari satu way yang terpotong batas area punya `Id` sama tetapi `Key` berbeda.
- Infill yang melewati cap tetap mengonsumsi angka acak, supaya rumah lain di interval yang sama tidak berubah.

---

## Review Focus

1. **Gang berbelok 90° dengan segmen OSM pendek.** Tepi jalan dan tembok harus tetap menyambung mengelilingi tikungan, tanpa lubang di sisi dalam belokan. Dipaku oleh:
   - `RoadEdgeBuilderTests.RightAngleBend_EdgesContinueAroundTheCorner` (Task 4);
   - `FrontagePlannerTests.RightAngleBend_WallFollowsTheCorner` (Task 10).
2. **Rumah OSM yang menyerong terhadap jalan.** Rumah itu harus jadi satu lot dengan satu gerbang, bukan tembok yang terpotong-potong menjadi lot kecil. Dipaku oleh `FrontagePlannerTests.RotatedHouse_IsOneLotWithOneGate` (Task 10).
3. **Jalan yang memotong batas dunia.** Tidak boleh ada tepi, tembok, atau rumah infill di luar `Bounds`. Dipaku oleh:
   - `RoadEdgeBuilderTests.RoadLeavingTheWorld_EdgesStayInside` (Task 4);
   - `InfillPlannerTests.Houses_StayInsideTheWorld` (Task 14).
4. **Gang yang berjalan sejajar dan berdekatan dengan jalan lain.** Tembok satu jalan tidak boleh masuk ke badan atau tepi jalan lainnya. Dipaku oleh `FrontagePlannerTests.ParallelRoads_WallsStayOutOfTheOtherRoad` (Task 10).
5. **Jalan lurus sangat panjang (satu segmen OSM > 100 m).** Collider tepi dan tembok tetap memakai segitiga dengan sisi ≤ 100 m (PhysX). Dipaku oleh `WorldMeshPipelineTests.LongRoadAndLargeArea_AreSplitBelowColliderEdgeLimit` (diperluas di Task 6 dan Task 13).

---

## Prosedur standar

Workspace ledger (diabaikan git): `D:\UNITY\project\Tyco World\.superpowers\sdd\2026-10-14-street-frontage\`.

### Procedure R+T — refresh, compile, dan jalankan tes

Panggil `mcp__unity-mcp__Unity_RunCommand` dengan kode di bawah. Ganti `Group` (regex nama tes; `"."` = semua).

```csharp
using UnityEditor;

internal class CommandScript : IRunCommand
{
    const string Group = "RoadNetworkTests";
    const string ResultPath = "D:/UNITY/project/Tyco World/.superpowers/sdd/2026-10-14-street-frontage/last-test.txt";

    public void Execute(ExecutionResult result)
    {
        if (EditorApplication.isPlaying) { result.LogError("STILL PLAYING"); return; }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        if (EditorApplication.isCompiling) { result.LogError("STILL COMPILING after refresh"); return; }
        string text = TycoWorld.WorldGen.Tests.McpTestRunner.Run(Group, ResultPath);
        if (text.StartsWith("group=") && text.Contains(" failed=0 ")) result.Log(text);
        else result.LogError(text);
    }
}
```

Arti hasilnya:
- `STILL COMPILING after refresh`: panggil lagi setelah beberapa detik.
- `COMPILE FAILED`: baca error lewat `mcp__unity-mcp__Unity_GetConsoleLogs` (`logTypes: "error"`, `includeStackTrace: false`).
- `DIRTY SCENE`: simpan scene dulu, atau buang perubahan yang bukan milik task.
- "Could not find type … RunCommandMacroEvaluatorEntryPoint": ulangi.
- MCP pindah ke background (>2 menit): tunggu notifikasinya. Hasil juga ada di `last-test.txt`.
- Error/warning `[WorldGen]`, `[Player]`, dan sejenisnya di payload berasal dari tes yang sengaja gagal. Yang menentukan adalah baris `group=… failed=0`.
- Pipe MCP hilang (Unity crash): berhenti dan laporkan; jangan menjalankan ulang Unity.

### Procedure G — Generate dunia lewat MCP

```csharp
using UnityEditor;
using TycoWorld.WorldGen.Authoring;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (EditorApplication.isPlaying) { result.LogError("STILL PLAYING"); return; }
        var settings = AssetDatabase.LoadAssetAtPath<WorldGenSettings>(WorldGenPaths.SettingsAsset);
        if (!WorldGenerationRunner.Generate(settings, false, out string error)) { result.LogError(error); return; }
        result.Log(WorldGenerationRunner.LastReport.Summary());
    }
}
```

- Generate memakan ±15–30 s. Kalau MCP pindah ke background, tunggu notifikasinya.
- Gagal dengan "scene belum disimpan": buka `World.unity` lewat menu atau simpan dulu, lalu ulangi.
- `Summary()` memuat counter Features, Stages, dan WARNING. Salin baris yang relevan ke laporan task.

### Procedure A — Build Art Assets lewat MCP

```csharp
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var settings = TycoWorld.WorldGen.Authoring.ArtAssetBuilder.BuildAll();
        result.Log("art ok: " + UnityEditor.AssetDatabase.GetAssetPath(settings));
    }
}
```

Setelah itu jalankan `git status --short`. Yang boleh di-add hanya file yang memang dihasilkan task, yaitu:
- material dan tekstur baru;
- `WorldGenSettings.asset`;
- `.meta`-nya.

Jika ada file lain yang berubah (misalnya prefab placeholder yang disimpan ulang dengan isi sama), jangan di-add. Kembalikan dengan `git checkout -- <path>`, kecuali perubahan itu memang bagian dari task.

### Procedure P — masuk/keluar Play lewat MCP

Masuk Play (panggil berulang sampai hasilnya diawali `READY`):

```csharp
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        UnityEngine.Application.runInBackground = true;
        if (!EditorApplication.isPlaying) { EditorApplication.isPlaying = true; result.Log("entering play"); return; }
        if (EditorApplication.isPaused) EditorApplication.isPaused = false;
        result.Log(TycoWorld.GameSettings.Tests.McpPerfProbe.Status());
    }
}
```

Keluar Play: isi `Execute` diganti menjadi `TycoWorld.GameSettings.Tests.McpPerfProbe.Release(); UnityEngine.Application.runInBackground = false; EditorApplication.isPlaying = false; result.Log("exit");`.
- `runInBackground` hanya dinyalakan selama Play (lihat memori proyek: kalau nyala di edit mode, nilainya bocor ke `ProjectSettings.asset` saat SaveAssets).

### Procedure S — screenshot dari titik tetap (Play)

Titik pandang:

| Label | Posisi (x, z) | Yaw | Isi |
|---|---|---|---|
| `kos` | lewat `McpPerfProbe.Place("kos")` | 180 | halaman kos menghadap gang |
| `road` | lewat `McpPerfProbe.Place("road")` | 57 | Jl. Sukabirus ke timur laut |
| `high` | lewat `McpPerfProbe.Place("high")` | 24 | 30 m di atas jalan, menghadap kampus |
| `preview` | (-194.6, -632.7) | 274 | tikungan Jl. Sukabirus (PreviewCam) |
| `kos0` | (-321.16, -612.64) | 0 | dari spawn kos membelakangi gerbang |

Langkah 1 (satu panggilan MCP). Untuk titik `kos`/`road`/`high`, isi `Execute` dengan:
```csharp
result.Log(TycoWorld.GameSettings.Tests.McpPerfProbe.Place("road"));
result.Log(TycoWorld.GameSettings.Tests.McpPerfProbe.SetTime(10f));
```

Untuk `preview`/`kos0`, panggil dulu `Place("road")` (untuk mengunci gerak), lalu teleport:

```csharp
using UnityEngine;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        result.Log(TycoWorld.GameSettings.Tests.McpPerfProbe.Place("road"));
        var player = Object.FindAnyObjectByType<TycoWorld.Player.FirstPersonPlayer>();
        float x = -194.6f, z = -632.7f, yaw = 274f;
        var hits = Physics.RaycastAll(new Vector3(x, 300f, z), Vector3.down, 600f);
        float y = 0f;
        foreach (var h in hits) if (h.point.y < 5f && h.point.y > y) y = h.point.y;
        player.Teleport(new Vector3(x, y, z), yaw);
        result.Log(TycoWorld.GameSettings.Tests.McpPerfProbe.SetTime(10f));
    }
}
```

Langkah 2 (panggilan MCP berikutnya, supaya DayCycle sempat menerapkan jamnya):
```csharp
result.Log(TycoWorld.Player.Tests.McpRender.SavePng(UnityEngine.Camera.main,
    "D:/UNITY/project/Tyco World/.superpowers/sdd/2026-10-14-street-frontage/shots/<tahap>/<label>_10.png", 1280, 720));
```

- Ulangi untuk jam 21 (`SetTime(21f)`).
- Lihat setiap PNG dengan tool Read (gambar) sebelum melapor.

### Procedure Perf — ukur performa (Play, preset tetap)

Untuk setiap titik `kos`, `road`, `high`, di jam 10 dan 21, jalankan empat panggilan MCP:
1. `Place(...)` + `SetTime(...)`;
2. `McpPerfProbe.Begin(5f)`;
3. tunggu ±7 s, lalu `McpPerfProbe.Report("<label>")` (ulangi selama hasilnya `PENDING`);
4. catat barisnya.

Settings grafis jangan diubah. Pakai preset yang sedang aktif (Sedang) baik untuk before maupun after.

### Procedure C — commit

```bash
git -C "D:/UNITY/project/Tyco World" add <path...>
git -C "D:/UNITY/project/Tyco World" commit -m "<subject>

Co-Authored-By: <model> <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

- Ikutkan `.meta` (cukup add foldernya atau file `.meta` bersama file-nya).
- Jangan pernah add ProjectSettings atau `Assets/WorldGen/Generated`.

### Catatan snippet MCP

- Bentuknya wajib `internal class CommandScript : IRunCommand`.
- Tanpa `System.Reflection`.
- Format angka dengan `ToString("F2", CultureInfo.InvariantCulture)`.
- `GetInstanceID()` tidak bisa dipakai (error Unity 6.6). Kalau perlu, pakai `GetEntityId().ToString()`.

---

## Peta file

```
docs/superpowers/notes/2026-10-14-street-frontage-verification.md                  (Task 0, 9, 13, 16)
Assets/WorldGen/Editor/Settings/StreetDressingSettings.cs                          (Task 1)
Assets/WorldGen/Editor/Settings/WorldGenSettings.cs, DefaultSettingsFactory.cs     (ubah, Task 1)
Assets/WorldGen/Editor/Geometry/Polyline2D.cs; Builders/SolidBox.cs                (Task 2)
Assets/WorldGen/Editor/Builders/MeshData.cs; Geometry/Polygon.cs                   (ubah, Task 2)
Assets/WorldGen/Editor/Builders/RoadNetwork.cs                                     (Task 3)
Assets/WorldGen/Editor/Builders/StreetContext.cs, RoadEdgeBuilder.cs               (Task 4)
Assets/WorldGen/Editor/Builders/RoadMarkingBuilder.cs                              (Task 5)
Assets/WorldGen/Runtime/FeatureKind.cs, Culling/WorldChunkIndex.cs                 (ubah, Task 6)
Assets/WorldGen/Editor/Pipeline/WorldMeshPipeline.cs                   (ubah, Task 5, 6, 7, 13, 15)
Assets/WorldGen/Editor/Pipeline/ChunkAssetWriter.cs, SceneObjectFactory.cs;
    Classify/SurfaceRules.cs                                                       (ubah, Task 6)
Assets/WorldGen/Editor/Builders/PropPlacer.cs                                      (ubah, Task 7)
Assets/WorldGen/Editor/Art/StreetTextureGenerator.cs                               (Task 9, 12)
Assets/WorldGen/Editor/Art/ArtAssetBuilder.cs; WorldGenPaths.cs;
    Assets/WorldGen/Textures/SOURCES.md                                            (ubah, Task 9, 12)
Assets/WorldGen/Textures/Street/*, Materials/Edge_*, Road_Marking, Frontage_*      (Task 9, 12)
Assets/WorldGen/Textures/CC0/Plaster001, Bricks059                                 (Task 12)
Assets/WorldGen/Editor/Builders/FrontagePlanner.cs                                 (Task 10)
Assets/WorldGen/Editor/Builders/FrontageMeshBuilder.cs                             (Task 11)
Assets/WorldGen/Editor/Builders/InfillPlanner.cs                                   (Task 14)
Assets/WorldGen/Tests/DefaultSettingsTests.cs, ArtTests.cs, WorldMeshPipelineTests.cs,
    WorldChunkIndexTests.cs, SurfaceRulesTests.cs                                  (ubah)
Assets/WorldGen/Tests/StreetGeometryTests.cs (2), RoadNetworkTests.cs (3), StreetFixture.cs,
    RoadEdgeBuilderTests.cs (4), RoadMarkingBuilderTests.cs (5), StreetPropTests.cs (7),
    FrontagePlannerTests.cs (10), FrontageMeshBuilderTests.cs (11), InfillPlannerTests.cs (14)
Assets/Player/Tests/FirstPersonPlayerTests.cs; Assets/Vehicles/Tests/MotorbikeTests.cs (ubah, Task 8)
docs/superpowers/specs/2026-10-13-osm-world-generator-design.md                    (ubah, Task 16)
```

**Checkpoint user:**
- Setelah Task 9 (M1, tepi jalan dan marka) dan Task 13 (M2, tembok), controller berhenti, menunjukkan screenshot before/after ke user, dan menunggu tanggapan sebelum lanjut.
- Task 16 berakhir dengan laporan akhir.

---

### Task 0: Baseline "before" (tanpa kode)

**Files:**
- Create: `docs/superpowers/notes/2026-10-14-street-frontage-verification.md`

**Interfaces:**
- Consumes: Procedure G, P, S, Perf; `McpVerify.WriteChunkHashes` (`Assets/WorldGen/Tests/McpVerify.cs`).
- Produces:
  - `.superpowers/sdd/2026-10-14-street-frontage/hashes-before.txt`;
  - `shots/before/*.png`;
  - angka perf "before" di catatan verifikasi.
- Task 16 membandingkan dengan semuanya.

- [ ] **Step 1: Pastikan bersih**

Jalankan `git -C "D:/UNITY/project/Tyco World" status --short`. Yang boleh muncul hanya ` M ProjectSettings/ProjectSettings.asset`.

- [ ] **Step 2: Generate dari kode sekarang dan simpan hash**

Jalankan Procedure G, lalu panggilan MCP berikut:

```csharp
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        result.Log(TycoWorld.WorldGen.Tests.McpVerify.WriteChunkHashes(
            "D:/UNITY/project/Tyco World/.superpowers/sdd/2026-10-14-street-frontage/hashes-before.txt"));
    }
}
```

Expected: `chunks=278`. Catat baris `Features:`, `Trees:`, dan `Stages:` dari Summary.

- [ ] **Step 3: Screenshot before**

Jalankan Procedure P (masuk), lalu Procedure S untuk `kos`, `road`, `high`, `preview`, `kos0` di jam 10 dan 21. Simpan ke `shots/before/`.

- [ ] **Step 4: Perf before**

Jalankan Procedure Perf untuk `kos`, `road`, `high` di jam 10 dan 21. Lalu keluar Play (Procedure P).

- [ ] **Step 5: Tulis catatan verifikasi awal**

`docs/superpowers/notes/2026-10-14-street-frontage-verification.md`:

```markdown
# Street Frontage + Road Dressing — verifikasi

## Before (main 38a40ee, sebelum perubahan kode)

- Generate: <ms total> ms; Features: <salin>; Trees/Lamps: <salin>
- Hash chunk: `.superpowers/sdd/2026-10-14-street-frontage/hashes-before.txt` (278 chunk)
- Screenshot: `shots/before/{kos,road,high,preview,kos0}_{10,21}.png`

| Titik | Jam | fps | batches | setpass | tris | renderers |
|---|---|---|---|---|---|---|
| kos | 10 | … | … | … | … | … |
| road | 10 | … | … | … | … | … |
| high | 10 | … | … | … | … | … |
| kos | 21 | … | … | … | … | … |
| road | 21 | … | … | … | … | … |
| high | 21 | … | … | … | … | … |
```

Isi `…` dengan angka asli dari `Report`.

- [ ] **Step 6: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add docs/superpowers/notes/2026-10-14-street-frontage-verification.md
git -C "D:/UNITY/project/Tyco World" commit -m "docs(worldgen): street frontage baseline before changes

Co-Authored-By: <model> <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

---

### Task 1: Settings street dressing

**Files:**
- Create: `Assets/WorldGen/Editor/Settings/StreetDressingSettings.cs`
- Modify: `Assets/WorldGen/Editor/Settings/WorldGenSettings.cs` (setelah blok `[Header("Props")]`, sebelum `[Header("Key buildings")]`)
- Modify: `Assets/WorldGen/Editor/Settings/DefaultSettingsFactory.cs` (`Populate`, setelah `s.lampOffset = 0.6f;`)
- Test: `Assets/WorldGen/Tests/DefaultSettingsTests.cs`

**Interfaces:**
- Produces (dipakai semua task berikutnya):
  - `enum EdgeProfile { None, Gutter, Sidewalk }`;
  - `enum WallStyle { Plaster, LowFence, Brick, Campus, Cemetery }`;
  - `RoadEdgeSettings` dengan `float GutterProfileWidth` dan `float Width(EdgeProfile)`;
  - `WallStyleRule`;
  - `FrontageSettings` dengan `WallStyleRule Style(WallStyle)` dan `WallStyleRule PickResidential(double roll)`;
  - `InfillSettings`;
  - field `WorldGenSettings.roadEdges`, `.frontage`, `.infill`.

- [ ] **Step 1: Tulis tes yang gagal**

Tambahkan ke `DefaultSettingsTests` (file sudah punya `using System.Collections.Generic; using System.Linq; using NUnit.Framework; using TycoWorld.WorldGen.Authoring; using UnityEngine;`):

```csharp
        [Test]
        public void RoadEdges_MatchStreetFrontageSpec()
        {
            var e = s.roadEdges;
            CollectionAssert.AreEqual(new[] { "primary", "secondary" }, e.sidewalkHighways);
            CollectionAssert.AreEqual(new[] { "tertiary" }, e.campusSidewalkHighways);
            CollectionAssert.AreEqual(new[] { "tertiary", "unclassified", "residential", "living_street", "service" }, e.gutterHighways);
            CollectionAssert.AreEqual(new[] { "driveway", "parking_aisle", "drive-through" }, e.noEdgeServiceValues);
            Assert.AreEqual(10f, e.campusSidewalkDistance);
            Assert.AreEqual(8f, e.minProfileRun);
            Assert.AreEqual(0.5f, e.sampleStep);
            Assert.AreEqual(0.3f, e.mouthMargin, 1e-6f);
            Assert.AreEqual(0.055f, e.edgeBaseY, 1e-6f);
            Assert.AreEqual(0.155f, e.gutterLipY, 1e-6f);
            Assert.AreEqual(0.21f, e.sidewalkTopY, 1e-6f);
            Assert.AreEqual(0.05f, e.underlap, 1e-6f);
            Assert.AreEqual(0.3f, e.shoulderWidth, 1e-6f);
            Assert.AreEqual(0.5f, e.gutterWidth, 1e-6f);
            Assert.AreEqual(0.08f, e.gutterLipWidth, 1e-6f);
            Assert.AreEqual(1.5f, e.sidewalkWidth, 1e-6f);
            Assert.AreEqual(0.15f, e.kerbWidth, 1e-6f);
            Assert.AreEqual(0.8f, e.Width(EdgeProfile.Gutter), 1e-6f);
            Assert.AreEqual(1.5f, e.Width(EdgeProfile.Sidewalk), 1e-6f);
            Assert.AreEqual(0f, e.Width(EdgeProfile.None));
            CollectionAssert.AreEqual(new[] { "primary", "secondary" }, e.markingHighways);
            CollectionAssert.AreEqual(new[] { "tertiary" }, e.wideMarkingHighways);
            Assert.AreEqual(7f, e.markingMinWidth);
            Assert.AreEqual(0.12f, e.markingWidth, 1e-6f);
            Assert.AreEqual(0.003f, e.markingLift, 1e-6f);
            Assert.AreEqual(3f, e.dashLength);
            Assert.AreEqual(5f, e.dashGap);
            Assert.AreEqual(2f, e.dashStart);
            Assert.AreEqual(3f, e.junctionDashClearance);
            Assert.AreEqual(0.45f, e.sidewalkLampOffset, 1e-6f);
            Assert.AreEqual(0.15f, e.gutterLampOffset, 1e-6f);
        }

        [Test]
        public void Frontage_MatchesStreetFrontageSpec()
        {
            var f = s.frontage;
            Assert.AreEqual(0.4f, f.propertyLineGap, 1e-6f);
            Assert.AreEqual(15f, f.corridorDepth);
            Assert.AreEqual(1f, f.flushDistance);
            Assert.AreEqual(1.5f, f.minInterval);
            Assert.AreEqual(0.5f, f.contextProbe);
            Assert.AreEqual(0.15f, f.wallThickness, 1e-6f);
            Assert.AreEqual(0.25f, f.pilasterSize, 1e-6f);
            Assert.AreEqual(3f, f.pilasterSpacing);
            Assert.AreEqual(0.1f, f.pilasterExtra, 1e-6f);
            Assert.AreEqual(2f, f.gateWidth);
            Assert.AreEqual(0.9f, f.doorWidth, 1e-6f);
            Assert.AreEqual(1.6f, f.gateHeight, 1e-6f);
            Assert.AreEqual(4.5f, f.gateMinLot);
            Assert.AreEqual(2.5f, f.doorMinLot);
            Assert.AreEqual(0.165f, f.slabTopY, 1e-6f);
            Assert.AreEqual(0.4f, f.slabExtraWidth, 1e-6f);
            Assert.AreEqual(8f, f.kosExclusionRadius);
            Assert.AreEqual(409467778L, f.kosBuildingId);
            Assert.AreEqual(8, f.atlasBands);
            Assert.AreEqual(5, f.styles.Length);
            var plaster = f.Style(WallStyle.Plaster);
            Assert.AreEqual(0.55f, plaster.share, 1e-6f);
            Assert.AreEqual(1.6f, plaster.minHeight, 1e-6f);
            Assert.AreEqual(2.0f, plaster.maxHeight, 1e-6f);
            Assert.AreEqual(0, plaster.firstBand);
            Assert.AreEqual(6, plaster.bandCount);
            var low = f.Style(WallStyle.LowFence);
            Assert.AreEqual(0.8f, low.maxHeight, 1e-6f);
            Assert.AreEqual(0.9f, low.fenceHeight, 1e-6f);
            var brick = f.Style(WallStyle.Brick);
            Assert.AreEqual(1.7f, brick.minHeight, 1e-6f);
            Assert.AreEqual(6, brick.firstBand);
            Assert.AreEqual(2, brick.bandCount);
            Assert.AreEqual(1.2f, f.Style(WallStyle.Campus).fenceHeight, 1e-6f);
            Assert.AreEqual(0.6f, f.Style(WallStyle.Campus).maxHeight, 1e-6f);
            Assert.AreEqual(0.7f, f.Style(WallStyle.Cemetery).maxHeight, 1e-6f);
            Assert.AreEqual(1f, f.styles.Where(x => x.share > 0f).Sum(x => x.share), 1e-5f);
        }

        [TestCase(0.0, WallStyle.Plaster)]
        [TestCase(0.549, WallStyle.Plaster)]
        [TestCase(0.6, WallStyle.LowFence)]
        [TestCase(0.95, WallStyle.Brick)]
        [TestCase(0.9999, WallStyle.Brick)]
        public void PickResidential_FollowsShares(double roll, WallStyle expected)
        {
            Assert.AreEqual(expected, s.frontage.PickResidential(roll).style);
        }

        [Test]
        public void Infill_MatchesStreetFrontageSpec()
        {
            var i = s.infill;
            Assert.IsTrue(i.enabled);
            Assert.AreEqual(6f, i.minLotLength);
            Assert.AreEqual(6f, i.minWidth);
            Assert.AreEqual(10f, i.maxWidth);
            Assert.AreEqual(8f, i.minDepth);
            Assert.AreEqual(12f, i.maxDepth);
            Assert.AreEqual(0f, i.minGap);
            Assert.AreEqual(0.6f, i.maxGap, 1e-6f);
            Assert.AreEqual(0.35f, i.flushShare, 1e-6f);
            Assert.AreEqual(0.1f, i.flushInset, 1e-6f);
            Assert.AreEqual(1.5f, i.minSetback);
            Assert.AreEqual(4f, i.maxSetback);
            Assert.AreEqual(0.4f, i.twoStoreyShare, 1e-6f);
            Assert.AreEqual(0.5f, i.clearance, 1e-6f);
            Assert.AreEqual(2500, i.maxInfill);
        }

        [Test]
        public void SavedAsset_HasStreetDressing()
        {
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<WorldGenSettings>(WorldGenPaths.SettingsAsset);
            Assert.IsNotNull(asset);
            Assert.IsNotNull(asset.roadEdges);
            Assert.AreEqual(1.5f, asset.roadEdges.sidewalkWidth, 1e-6f);
            Assert.IsNotNull(asset.frontage);
            Assert.AreEqual(409467778L, asset.frontage.kosBuildingId);
            Assert.AreEqual(5, asset.frontage.styles.Length);
            Assert.IsNotNull(asset.infill);
            Assert.AreEqual(2500, asset.infill.maxInfill);
        }
```

- [ ] **Step 2: Jalankan, pastikan gagal**

Procedure R+T, `Group = "DefaultSettingsTests"`. Expected: `COMPILE FAILED` (`EdgeProfile`/`roadEdges` tidak dikenal).

- [ ] **Step 3: Implementasi**

`Assets/WorldGen/Editor/Settings/StreetDressingSettings.cs`:

```csharp
using System;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>What lines one side of a road: nothing, a concrete shoulder + open gutter, or a raised kerbed sidewalk.</summary>
    public enum EdgeProfile { None, Gutter, Sidewalk }

    public enum WallStyle { Plaster, LowFence, Brick, Campus, Cemetery }

    /// <summary>
    /// Road dressing (spec street-frontage §4–§6, §9, §12). Field initializers are the defaults, so settings assets saved
    /// before this class existed pick them up when they load.
    /// </summary>
    [Serializable]
    public sealed class RoadEdgeSettings
    {
        public string[] sidewalkHighways = { "primary", "secondary" };
        public string[] campusSidewalkHighways = { "tertiary" };
        public string[] gutterHighways = { "tertiary", "unclassified", "residential", "living_street", "service" };
        public string[] noEdgeServiceValues = { "driveway", "parking_aisle", "drive-through" };
        public float campusSidewalkDistance = 10f;
        public float minProfileRun = 8f;
        public float sampleStep = 0.5f;
        public float mouthMargin = 0.3f;
        public float minRunLength = 0.5f;

        [Header("Cross-section")]
        public float edgeBaseY = 0.055f;
        public float gutterLipY = 0.155f;
        public float sidewalkTopY = 0.21f;
        public float underlap = 0.05f;
        public float shoulderWidth = 0.30f;
        public float gutterWidth = 0.50f;
        public float gutterLipWidth = 0.08f;
        public float sidewalkWidth = 1.5f;
        public float kerbWidth = 0.15f;

        [Header("Markings")]
        public string[] markingHighways = { "primary", "secondary" };
        public string[] wideMarkingHighways = { "tertiary" };
        public float markingMinWidth = 7f;
        public float markingWidth = 0.12f;
        public float markingLift = 0.003f;
        public float dashLength = 3f;
        public float dashGap = 5f;
        public float dashStart = 2f;
        public float junctionDashClearance = 3f;

        [Header("Lamps")]
        public float sidewalkLampOffset = 0.45f;
        public float gutterLampOffset = 0.15f;

        [Header("Materials")]
        public Material concrete;
        public Material gutter;
        public Material kerb;
        public Material sidewalk;
        public Material marking;

        public float GutterProfileWidth => shoulderWidth + gutterWidth;

        public float Width(EdgeProfile profile) =>
            profile == EdgeProfile.Sidewalk ? sidewalkWidth : profile == EdgeProfile.Gutter ? GutterProfileWidth : 0f;
    }

    /// <summary>One wall look: residential styles have a share; context styles (Campus, Cemetery) have share 0.</summary>
    [Serializable]
    public sealed class WallStyleRule
    {
        public WallStyle style;
        public float share;
        public float minHeight;
        public float maxHeight;
        public float fenceHeight;
        public int firstBand;
        public int bandCount = 1;

        public WallStyleRule() { }

        public WallStyleRule(WallStyle style, float share, float minHeight, float maxHeight, float fenceHeight, int firstBand, int bandCount)
        {
            this.style = style;
            this.share = share;
            this.minHeight = minHeight;
            this.maxHeight = maxHeight;
            this.fenceHeight = fenceHeight;
            this.firstBand = firstBand;
            this.bandCount = bandCount;
        }
    }

    /// <summary>Property-line walls, gates and slabs (spec street-frontage §7, §12).</summary>
    [Serializable]
    public sealed class FrontageSettings
    {
        public float propertyLineGap = 0.4f;
        public float corridorDepth = 15f;
        public float flushDistance = 1f;
        public float minInterval = 1.5f;
        public float contextProbe = 0.5f;
        public float wallThickness = 0.15f;
        public float copingHeight = 0.05f;
        public float copingOverhang = 0.02f;
        public float pilasterSize = 0.25f;
        public float pilasterSpacing = 3f;
        public float pilasterExtra = 0.1f;
        public float gateWidth = 2f;
        public float doorWidth = 0.9f;
        public float gateHeight = 1.6f;
        public float gateMinLot = 4.5f;
        public float doorMinLot = 2.5f;
        public float gateEndMargin = 0.3f;
        public float slabTopY = 0.165f;
        public float slabExtraWidth = 0.4f;
        public float kosExclusionRadius = 8f;
        public long kosBuildingId = 409467778;

        [Header("Wall atlas")]
        public int atlasBands = 8;
        public float atlasTileMeters = 4f;
        public float atlasBandMeters = 2f;

        public WallStyleRule[] styles =
        {
            new WallStyleRule(WallStyle.Plaster, 0.55f, 1.6f, 2.0f, 0f, 0, 6),
            new WallStyleRule(WallStyle.LowFence, 0.35f, 0.8f, 0.8f, 0.9f, 0, 6),
            new WallStyleRule(WallStyle.Brick, 0.10f, 1.7f, 1.7f, 0f, 6, 2),
            new WallStyleRule(WallStyle.Campus, 0f, 0.6f, 0.6f, 1.2f, 5, 1),
            new WallStyleRule(WallStyle.Cemetery, 0f, 0.7f, 0.7f, 0f, 5, 1)
        };

        [Header("Materials")]
        public Material wallAtlas;
        public Material fence;
        public Material gateMetal;

        public WallStyleRule Style(WallStyle style)
        {
            if (styles == null) return null;
            foreach (var s in styles)
                if (s != null && s.style == style) return s;
            return null;
        }

        /// <summary>Residential style (share &gt; 0) chosen by a uniform roll in [0, 1).</summary>
        public WallStyleRule PickResidential(double roll)
        {
            double total = 0;
            foreach (var s in styles)
                if (s != null && s.share > 0f) total += s.share;
            double acc = 0;
            WallStyleRule last = null;
            foreach (var s in styles)
            {
                if (s == null || s.share <= 0f) continue;
                acc += s.share / total;
                last = s;
                if (roll < acc) return s;
            }
            return last;
        }
    }

    /// <summary>Made-up houses on empty lots beside roads (spec street-frontage §8, §12).</summary>
    [Serializable]
    public sealed class InfillSettings
    {
        public bool enabled = true;
        public float minLotLength = 6f;
        public float minWidth = 6f;
        public float maxWidth = 10f;
        public float minDepth = 8f;
        public float maxDepth = 12f;
        public float minGap = 0f;
        public float maxGap = 0.6f;
        public float flushShare = 0.35f;
        public float flushInset = 0.1f;
        public float minSetback = 1.5f;
        public float maxSetback = 4f;
        public float twoStoreyShare = 0.4f;
        public float clearance = 0.5f;
        public int maxInfill = 2500;
    }
}
```

Di `WorldGenSettings.cs`, tepat sebelum `[Header("Key buildings")]`:

```csharp
        [Header("Street dressing")]
        public RoadEdgeSettings roadEdges = new RoadEdgeSettings();
        public FrontageSettings frontage = new FrontageSettings();
        public InfillSettings infill = new InfillSettings();

```

Di `DefaultSettingsFactory.Populate`, tepat setelah `s.lampOffset = 0.6f;`:

```csharp

            s.roadEdges = new RoadEdgeSettings();
            s.frontage = new FrontageSettings();
            s.infill = new InfillSettings();
```

- [ ] **Step 4: Jalankan, pastikan lulus**

Procedure R+T, `Group = "DefaultSettingsTests"`. Expected: `failed=0`.
- `SavedAsset_HasStreetDressing` ikut lulus karena Unity memakai initializer untuk field yang belum ada di YAML.
- Asset `WorldGenSettings.asset` **tidak** perlu disimpan di task ini.

- [ ] **Step 5: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add Assets/WorldGen/Editor/Settings/StreetDressingSettings.cs Assets/WorldGen/Editor/Settings/StreetDressingSettings.cs.meta Assets/WorldGen/Editor/Settings/WorldGenSettings.cs Assets/WorldGen/Editor/Settings/DefaultSettingsFactory.cs Assets/WorldGen/Tests/DefaultSettingsTests.cs
git -C "D:/UNITY/project/Tyco World" commit -m "feat(worldgen): street dressing settings for edges, frontage and infill

Co-Authored-By: <model> <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

---

### Task 2: Helper geometri (polyline, box, append, overlap)

**Files:**
- Create: `Assets/WorldGen/Editor/Geometry/Polyline2D.cs`
- Create: `Assets/WorldGen/Editor/Builders/SolidBox.cs`
- Modify: `Assets/WorldGen/Editor/Builders/MeshData.cs` (tambah `Append` setelah `AddQuad`)
- Modify: `Assets/WorldGen/Editor/Geometry/Polygon.cs` (tambah `RingsOverlap` dan `SegmentDistance` di `Polygon2D`, sebelum `SegmentsCross`)
- Test: `Assets/WorldGen/Tests/StreetGeometryTests.cs`

**Interfaces:**
- Consumes: `MeshData` (`AddQuad`, `Vertices`, `Normals`, `Uvs`, `Indices`), `Polygon2D`, `Xz`, `RoadMeshBuilder.MiterLimit` (= 2).
- Produces:
  - `Polyline2D.Cumulative(IReadOnlyList<Vector2>) : float[]`
  - `Polyline2D.LeftNormal(Vector2) : Vector2`
  - `Polyline2D.SegmentAt(float[] cum, float s) : int`
  - `Polyline2D.PointAt(IReadOnlyList<Vector2> pts, float[] cum, float s, out Vector2 tangent) : Vector2`
  - `Polyline2D.Slice(IReadOnlyList<Vector2> pts, float[] cum, float s0, float s1, List<float> along) : List<Vector2>`
  - `Polyline2D.Offset(IReadOnlyList<Vector2> pts, IReadOnlyList<float> s, float offset, List<float> along) : List<Vector2>`
  - `Polyline2D.Project(IReadOnlyList<Vector2> pts, float[] cum, Vector2 p, out float distance) : float`
  - `Polyline2D.DistanceTo(IReadOnlyList<Vector2> pts, Vector2 p) : float`
  - `Polyline2D.RayToRing(Vector2 origin, Vector2 dir, IReadOnlyList<Vector2> ring, float maxDistance) : float` (+∞ jika tidak kena)
  - `Polyline2D.Bisect(float keep, float drop, Func<float,bool> passes, int steps) : float`
  - `BoxUv.Meters`, `BoxUv.AtlasBand(int band, int bands, float bandMeters, float tileMeters)`, `BoxUv.Side(along, y)`, `BoxUv.Top(along, across, y)`
  - `SolidBox.Add(MeshData m, int sub, Vector2 a, Vector2 b, float y0, float y1, float thickness, BoxUv uv, bool capEnds = true)`
  - `SolidBox.AddPost(MeshData m, int sub, Vector2 c, Vector2 dir, float size, float y0, float y1, BoxUv uv)`
  - `MeshData.Append(MeshData src, int[] submeshMap = null)`
  - `Polygon2D.RingsOverlap(IReadOnlyList<Vector2> a, IReadOnlyList<Vector2> b) : bool`
  - `Polygon2D.SegmentDistance(Vector2 a, Vector2 b, Vector2 c, Vector2 d) : float`

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/WorldGen/Tests/StreetGeometryTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class StreetGeometryTests
    {
        static Vector2 V(float x, float y) => new Vector2(x, y);

        static List<Vector2> Square(float x, float y, float w, float h) =>
            new List<Vector2> { V(x, y), V(x + w, y), V(x + w, y + h), V(x, y + h) };

        [Test]
        public void Cumulative_AndPointAt_FollowArcLength()
        {
            var pts = new List<Vector2> { V(0, 0), V(10, 0), V(10, 10) };
            var cum = Polyline2D.Cumulative(pts);
            CollectionAssert.AreEqual(new[] { 0f, 10f, 20f }, cum);
            var p = Polyline2D.PointAt(pts, cum, 15f, out var t);
            Assert.AreEqual(V(10, 5), p);
            Assert.AreEqual(V(0, 1), t);
            Assert.AreEqual(V(10, 10), Polyline2D.PointAt(pts, cum, 99f, out _), "clamped to the end");
            Assert.AreEqual(V(0, 0), Polyline2D.PointAt(pts, cum, -5f, out _), "clamped to the start");
        }

        [Test]
        public void Slice_KeepsInteriorVertices()
        {
            var pts = new List<Vector2> { V(0, 0), V(10, 0), V(10, 10) };
            var along = new List<float>();
            var slice = Polyline2D.Slice(pts, Polyline2D.Cumulative(pts), 5f, 15f, along);
            CollectionAssert.AreEqual(new[] { V(5, 0), V(10, 0), V(10, 5) }, slice);
            CollectionAssert.AreEqual(new[] { 5f, 10f, 15f }, along);
        }

        [Test]
        public void Offset_StraightLine_IsParallel()
        {
            var pts = new List<Vector2> { V(0, 0), V(10, 0) };
            var along = new List<float>();
            var left = Polyline2D.Offset(pts, new[] { 0f, 10f }, 2f, along);
            CollectionAssert.AreEqual(new[] { V(0, 2), V(10, 2) }, left);
            var right = Polyline2D.Offset(pts, new[] { 0f, 10f }, -2f, along);
            CollectionAssert.AreEqual(new[] { V(0, -2), V(10, -2) }, right);
            CollectionAssert.AreEqual(new[] { 0f, 10f }, along);
        }

        [Test]
        public void Offset_RightAngle_MitersToTheCorner()
        {
            var pts = new List<Vector2> { V(0, 0), V(10, 0), V(10, 10) };
            var along = new List<float>();
            var inner = Polyline2D.Offset(pts, new[] { 0f, 10f, 20f }, 1f, along);
            Assert.AreEqual(3, inner.Count);
            Assert.AreEqual(9f, inner[1].x, 1e-4f);
            Assert.AreEqual(1f, inner[1].y, 1e-4f);
            var outer = Polyline2D.Offset(pts, new[] { 0f, 10f, 20f }, -1f, along);
            Assert.AreEqual(11f, outer[1].x, 1e-4f);
            Assert.AreEqual(-1f, outer[1].y, 1e-4f);
        }

        [Test]
        public void Offset_Hairpin_BevelsWithTheSameCountAtAnyDistance()
        {
            var pts = new List<Vector2> { V(0, 0), V(10, 0), V(0, 1) };
            var s = new[] { 0f, 10f, 10f + Vector2.Distance(V(10, 0), V(0, 1)) };
            var along = new List<float>();
            var a = Polyline2D.Offset(pts, s, 1f, along);
            var b = Polyline2D.Offset(pts, s, 3f, along);
            Assert.AreEqual(4, a.Count);
            Assert.AreEqual(4, b.Count);
            CollectionAssert.AreEqual(new[] { 0f, 10f, 10f, s[2] }, along);
        }

        [Test]
        public void Project_FindsArcLengthAndDistance()
        {
            var pts = new List<Vector2> { V(0, 0), V(10, 0), V(10, 10) };
            float s = Polyline2D.Project(pts, Polyline2D.Cumulative(pts), V(12, 4), out float d);
            Assert.AreEqual(14f, s, 1e-4f);
            Assert.AreEqual(2f, d, 1e-4f);
            Assert.AreEqual(2f, Polyline2D.DistanceTo(pts, V(12, 4)), 1e-4f);
        }

        [Test]
        public void RayToRing_HitsTheNearestEdge()
        {
            var ring = Square(10, 0, 10, 10);
            Assert.AreEqual(10f, Polyline2D.RayToRing(V(0, 5), V(1, 0), ring, 50f), 1e-4f);
            Assert.IsTrue(float.IsPositiveInfinity(Polyline2D.RayToRing(V(0, 5), V(-1, 0), ring, 50f)));
            Assert.IsTrue(float.IsPositiveInfinity(Polyline2D.RayToRing(V(0, 5), V(1, 0), ring, 5f)), "beyond maxDistance");
        }

        [Test]
        public void Bisect_ConvergesOnTheKeepSide()
        {
            Assert.AreEqual(3f, Polyline2D.Bisect(0f, 4f, x => x < 3.3f, 3), 1e-5f);
            Assert.AreEqual(4f, Polyline2D.Bisect(4f, 0f, x => x > 3.6f, 1), 1e-5f);
        }

        [Test]
        public void RingsOverlap_DetectsCrossingContainedAndApart()
        {
            Assert.IsTrue(Polygon2D.RingsOverlap(Square(0, 0, 10, 10), Square(5, 5, 10, 10)));
            Assert.IsTrue(Polygon2D.RingsOverlap(Square(0, 0, 10, 10), Square(2, 2, 2, 2)));
            Assert.IsTrue(Polygon2D.RingsOverlap(Square(2, 2, 2, 2), Square(0, 0, 10, 10)));
            Assert.IsFalse(Polygon2D.RingsOverlap(Square(0, 0, 10, 10), Square(20, 0, 5, 5)));
        }

        [Test]
        public void SegmentDistance_ZeroWhenCrossing()
        {
            Assert.AreEqual(0f, Polygon2D.SegmentDistance(V(0, -1), V(0, 1), V(-1, 0), V(1, 0)), 1e-6f);
            Assert.AreEqual(2f, Polygon2D.SegmentDistance(V(0, 0), V(10, 0), V(5, 2), V(5, 8)), 1e-6f);
        }

        [Test]
        public void SolidBox_HasFiveOutwardFaces()
        {
            var m = new MeshData(1);
            SolidBox.Add(m, 0, V(0, 0), V(2, 0), 0f, 1f, 0.2f, BoxUv.Meters);
            Assert.AreEqual(20, m.Vertices.Count);
            Assert.AreEqual(10, m.TriangleCount);
            var centre = new Vector3(1f, 0.5f, 0f);
            var idx = m.Indices(0);
            for (int t = 0; t < idx.Count; t += 3)
            {
                var c = (m.Vertices[idx[t]] + m.Vertices[idx[t + 1]] + m.Vertices[idx[t + 2]]) / 3f;
                Assert.Greater(Vector3.Dot(c - centre, m.Normals[idx[t]]), 0f, "face " + t / 3);
            }
            Assert.AreEqual(-0.1f, m.Vertices.Min(v => v.z), 1e-5f);
            Assert.AreEqual(1f, m.Vertices.Max(v => v.y), 1e-5f);
        }

        [Test]
        public void SolidBox_WithoutEnds_HasThreeFaces()
        {
            var m = new MeshData(1);
            SolidBox.Add(m, 0, V(0, 0), V(2, 0), 0f, 1f, 0.2f, BoxUv.Meters, false);
            Assert.AreEqual(6, m.TriangleCount);
        }

        [Test]
        public void AtlasBand_MapsIntoTheInsetBand()
        {
            var uv = BoxUv.AtlasBand(2, 8, 2f, 4f);
            Assert.AreEqual(1f, uv.Side(4f, 2f).x, 1e-6f);
            Assert.AreEqual(2.98f / 8f, uv.Side(4f, 2f).y, 1e-6f);
            Assert.AreEqual(2.02f / 8f, uv.Side(0f, 0f).y, 1e-6f);
            Assert.AreEqual(2.98f / 8f, uv.Side(0f, 5f).y, 1e-6f, "heights above the band clamp to its top");
            Assert.AreEqual(new Vector2(3f, 0.15f), BoxUv.Meters.Top(3f, 0.15f, 1f));
        }

        [Test]
        public void Append_OffsetsIndicesIntoMappedSubmeshes()
        {
            var a = new MeshData(2);
            a.AddQuad(0, Vector3.zero, Vector3.right, new Vector3(1, 0, 1), Vector3.forward, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, Vector3.up);
            var b = new MeshData(1);
            b.AddQuad(0, Vector3.zero, Vector3.right, new Vector3(1, 0, 1), Vector3.forward, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, Vector3.up);
            a.Append(b, new[] { 1 });
            Assert.AreEqual(8, a.Vertices.Count);
            Assert.AreEqual(8, a.Normals.Count);
            Assert.AreEqual(8, a.Uvs.Count);
            Assert.AreEqual(6, a.Indices(0).Count);
            CollectionAssert.AreEqual(new[] { 4, 5, 6, 4, 6, 7 }, a.Indices(1));
            var c = new MeshData(2);
            c.Append(a);
            CollectionAssert.AreEqual(a.Indices(1), c.Indices(1));
        }
    }
}
```

- [ ] **Step 2: Jalankan, pastikan gagal**

Procedure R+T, `Group = "StreetGeometryTests"`. Expected: `COMPILE FAILED`.

- [ ] **Step 3: Implementasi**

`Assets/WorldGen/Editor/Geometry/Polyline2D.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Open polylines on the ground plane. "s" is arc length along the polyline (spec street-frontage §5).</summary>
    public static class Polyline2D
    {
        public static float[] Cumulative(IReadOnlyList<Vector2> pts)
        {
            var cum = new float[pts.Count];
            for (int i = 1; i < pts.Count; i++) cum[i] = cum[i - 1] + Vector2.Distance(pts[i - 1], pts[i]);
            return cum;
        }

        public static Vector2 LeftNormal(Vector2 dir) => new Vector2(-dir.y, dir.x);

        /// <summary>Index i of the segment pts[i]..pts[i+1] holding arc length s (clamped to the first/last segment).</summary>
        public static int SegmentAt(float[] cum, float s)
        {
            int last = cum.Length - 2;
            if (last <= 0) return 0;
            int lo = 0, hi = last;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (cum[mid] <= s) lo = mid;
                else hi = mid - 1;
            }
            return lo;
        }

        public static Vector2 PointAt(IReadOnlyList<Vector2> pts, float[] cum, float s, out Vector2 tangent)
        {
            int i = SegmentAt(cum, s);
            Vector2 a = pts[i], b = pts[i + 1];
            float length = cum[i + 1] - cum[i];
            tangent = length > 1e-6f ? (b - a) / length : Vector2.right;
            float t = length > 1e-6f ? Mathf.Clamp01((s - cum[i]) / length) : 0f;
            return Vector2.Lerp(a, b, t);
        }

        /// <summary>Sub-polyline from s0 to s1: both cut points plus every vertex strictly between; along receives each point's s.</summary>
        public static List<Vector2> Slice(IReadOnlyList<Vector2> pts, float[] cum, float s0, float s1, List<float> along)
        {
            along.Clear();
            var result = new List<Vector2> { PointAt(pts, cum, s0, out _) };
            along.Add(s0);
            for (int i = 0; i < pts.Count; i++)
            {
                if (cum[i] <= s0 + 1e-4f || cum[i] >= s1 - 1e-4f) continue;
                result.Add(pts[i]);
                along.Add(cum[i]);
            }
            result.Add(PointAt(pts, cum, s1, out _));
            along.Add(s1);
            return result;
        }

        /// <summary>
        /// Mitered offset; positive = left of travel. A joint whose miter would be longer than RoadMeshBuilder.MiterLimit
        /// times the offset becomes a bevel (two points). The decision depends only on the turn angle, so every offset of one
        /// polyline has the same point count. along receives the centreline s of each output point.
        /// </summary>
        public static List<Vector2> Offset(IReadOnlyList<Vector2> pts, IReadOnlyList<float> s, float offset, List<float> along)
        {
            int n = pts.Count;
            along.Clear();
            var result = new List<Vector2>(n + 4);
            var normals = new Vector2[n - 1];
            for (int i = 0; i + 1 < n; i++) normals[i] = LeftNormal((pts[i + 1] - pts[i]).normalized);
            result.Add(pts[0] + normals[0] * offset);
            along.Add(s[0]);
            for (int i = 1; i + 1 < n; i++)
            {
                Vector2 n0 = normals[i - 1], n1 = normals[i];
                Vector2 sum = n0 + n1;
                float cosHalf = sum.sqrMagnitude > 1e-6f ? Vector2.Dot(sum.normalized, n1) : 0f;
                if (cosHalf > 1e-4f && 1f / cosHalf <= RoadMeshBuilder.MiterLimit)
                {
                    result.Add(pts[i] + sum.normalized * (offset / cosHalf));
                    along.Add(s[i]);
                }
                else
                {
                    result.Add(pts[i] + n0 * offset);
                    along.Add(s[i]);
                    result.Add(pts[i] + n1 * offset);
                    along.Add(s[i]);
                }
            }
            result.Add(pts[n - 1] + normals[n - 2] * offset);
            along.Add(s[n - 1]);
            return result;
        }

        /// <summary>Arc length of the polyline point closest to p; distance receives how far p is from it.</summary>
        public static float Project(IReadOnlyList<Vector2> pts, float[] cum, Vector2 p, out float distance)
        {
            float best = 0f;
            distance = float.PositiveInfinity;
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                Vector2 a = pts[i], ab = pts[i + 1] - a;
                float lengthSq = ab.sqrMagnitude;
                float t = lengthSq > 1e-12f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSq) : 0f;
                float d = Vector2.Distance(p, a + ab * t);
                if (d >= distance) continue;
                distance = d;
                best = cum[i] + t * (cum[i + 1] - cum[i]);
            }
            return best;
        }

        public static float DistanceTo(IReadOnlyList<Vector2> pts, Vector2 p)
        {
            float best = float.PositiveInfinity;
            for (int i = 0; i + 1 < pts.Count; i++) best = Mathf.Min(best, Polygon2D.DistanceToSegment(p, pts[i], pts[i + 1]));
            return best;
        }

        /// <summary>Distance along the ray (dir normalised) to the first ring edge it crosses; +∞ when none within maxDistance.</summary>
        public static float RayToRing(Vector2 origin, Vector2 dir, IReadOnlyList<Vector2> ring, float maxDistance)
        {
            float best = float.PositiveInfinity;
            for (int i = 0, n = ring.Count; i < n; i++)
            {
                Vector2 a = ring[i], e = ring[(i + 1) % n] - a;
                float denom = Polygon2D.Cross(dir, e);
                if (Mathf.Abs(denom) < 1e-9f) continue;
                Vector2 ao = a - origin;
                float t = Polygon2D.Cross(ao, e) / denom;
                float u = Polygon2D.Cross(ao, dir) / denom;
                if (t >= 0f && t <= maxDistance && u >= 0f && u <= 1f && t < best) best = t;
            }
            return best;
        }

        /// <summary>Moves from keep (passes) toward drop (fails) by halving; returns the last value that still passes.</summary>
        public static float Bisect(float keep, float drop, Func<float, bool> passes, int steps)
        {
            for (int i = 0; i < steps; i++)
            {
                float mid = (keep + drop) * 0.5f;
                if (passes(mid)) keep = mid;
                else drop = mid;
            }
            return keep;
        }
    }
}
```

`Assets/WorldGen/Editor/Builders/SolidBox.cs`:

```csharp
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>UV mapping for SolidBox: plain metres, or a horizontal band of a stacked atlas (spec street-frontage §10).</summary>
    public readonly struct BoxUv
    {
        public readonly bool Band;
        public readonly float VMin, VMax, BandMeters, TileMeters;

        BoxUv(bool band, float vMin, float vMax, float bandMeters, float tileMeters)
        {
            Band = band;
            VMin = vMin;
            VMax = vMax;
            BandMeters = bandMeters;
            TileMeters = tileMeters;
        }

        /// <summary>Sides: u = metres along, v = metres up. Top: u = metres along, v = metres across.</summary>
        public static BoxUv Meters => new BoxUv(false, 0f, 0f, 1f, 1f);

        /// <summary>Band <paramref name="band"/> of <paramref name="bands"/> stacked bands (band 0 at v = 0), inset 2% against mip bleeding.</summary>
        public static BoxUv AtlasBand(int band, int bands, float bandMeters, float tileMeters) =>
            new BoxUv(true, (band + 0.02f) / bands, (band + 0.98f) / bands, bandMeters, tileMeters);

        public Vector2 Side(float along, float y) =>
            Band ? new Vector2(along / TileMeters, VMin + Mathf.Clamp01(y / BandMeters) * (VMax - VMin)) : new Vector2(along, y);

        public Vector2 Top(float along, float across, float y) =>
            Band ? new Vector2(along / TileMeters, VMin + Mathf.Clamp01(y / BandMeters) * (VMax - VMin)) : new Vector2(along, across);
    }

    /// <summary>Axis-aligned-to-a-segment boxes for walls, coping, posts and colliders. No bottom face.</summary>
    public static class SolidBox
    {
        public static void Add(MeshData m, int sub, Vector2 a, Vector2 b, float y0, float y1, float thickness, BoxUv uv, bool capEnds = true)
        {
            Vector2 d = b - a;
            float length = d.magnitude;
            if (length < 1e-4f || y1 - y0 < 1e-4f || thickness < 1e-4f) return;
            d /= length;
            Vector2 n = Polyline2D.LeftNormal(d);
            Vector2 h = n * (thickness * 0.5f);
            Vector2 al = a + h, ar = a - h, bl = b + h, br = b - h;
            Vector3 left = Xz.Dir(n), forward = Xz.Dir(d);
            m.AddQuad(sub, Xz.At(al, y0), Xz.At(bl, y0), Xz.At(bl, y1), Xz.At(al, y1),
                uv.Side(0f, y0), uv.Side(length, y0), uv.Side(length, y1), uv.Side(0f, y1), left);
            m.AddQuad(sub, Xz.At(ar, y0), Xz.At(br, y0), Xz.At(br, y1), Xz.At(ar, y1),
                uv.Side(0f, y0), uv.Side(length, y0), uv.Side(length, y1), uv.Side(0f, y1), -left);
            m.AddQuad(sub, Xz.At(ar, y1), Xz.At(br, y1), Xz.At(bl, y1), Xz.At(al, y1),
                uv.Top(0f, 0f, y1), uv.Top(length, 0f, y1), uv.Top(length, thickness, y1), uv.Top(0f, thickness, y1), Vector3.up);
            if (!capEnds) return;
            m.AddQuad(sub, Xz.At(ar, y0), Xz.At(al, y0), Xz.At(al, y1), Xz.At(ar, y1),
                uv.Side(0f, y0), uv.Side(thickness, y0), uv.Side(thickness, y1), uv.Side(0f, y1), -forward);
            m.AddQuad(sub, Xz.At(bl, y0), Xz.At(br, y0), Xz.At(br, y1), Xz.At(bl, y1),
                uv.Side(0f, y0), uv.Side(thickness, y0), uv.Side(thickness, y1), uv.Side(0f, y1), forward);
        }

        /// <summary>Square post of side <paramref name="size"/> centred on c, its faces aligned with dir.</summary>
        public static void AddPost(MeshData m, int sub, Vector2 c, Vector2 dir, float size, float y0, float y1, BoxUv uv)
        {
            Vector2 half = dir.normalized * (size * 0.5f);
            Add(m, sub, c - half, c + half, y0, y1, size, uv);
        }
    }
}
```

Di `MeshData.cs`, setelah method `AddQuad`:

```csharp
        /// <summary>Appends src; its submesh s lands in this mesh's submesh submeshMap[s] (same index when the map is null).</summary>
        public void Append(MeshData src, int[] submeshMap = null)
        {
            int offset = Vertices.Count;
            Vertices.AddRange(src.Vertices);
            Normals.AddRange(src.Normals);
            Uvs.AddRange(src.Uvs);
            for (int s = 0; s < src.submeshes.Length; s++)
            {
                var target = submeshes[submeshMap != null ? submeshMap[s] : s];
                foreach (int i in src.submeshes[s]) target.Add(i + offset);
            }
        }
```

Di `Polygon.cs`, di dalam `Polygon2D`, tepat sebelum `static bool SegmentsCross`:

```csharp
        /// <summary>True when the rings' areas touch: a vertex of one inside the other, or crossing edges.</summary>
        public static bool RingsOverlap(IReadOnlyList<Vector2> a, IReadOnlyList<Vector2> b)
        {
            if (!Bounds(a).Overlaps(Bounds(b))) return false;
            foreach (var p in a) if (ContainsPoint(b, p)) return true;
            foreach (var p in b) if (ContainsPoint(a, p)) return true;
            for (int i = 0; i < a.Count; i++)
                for (int j = 0; j < b.Count; j++)
                    if (SegmentsCross(a[i], a[(i + 1) % a.Count], b[j], b[(j + 1) % b.Count])) return true;
            return false;
        }

        /// <summary>Shortest distance between segments ab and cd (0 when they cross).</summary>
        public static float SegmentDistance(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            if (SegmentsCross(a, b, c, d)) return 0f;
            return Mathf.Min(Mathf.Min(DistanceToSegment(a, c, d), DistanceToSegment(b, c, d)),
                Mathf.Min(DistanceToSegment(c, a, b), DistanceToSegment(d, a, b)));
        }

```

- [ ] **Step 4: Jalankan, pastikan lulus**

Procedure R+T, `Group = "StreetGeometryTests|MeshBuilderTests|Polygon2DTests"`. Expected: `failed=0`.

- [ ] **Step 5: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add Assets/WorldGen/Editor/Geometry/Polyline2D.cs Assets/WorldGen/Editor/Geometry/Polyline2D.cs.meta Assets/WorldGen/Editor/Builders/SolidBox.cs Assets/WorldGen/Editor/Builders/SolidBox.cs.meta Assets/WorldGen/Editor/Builders/MeshData.cs Assets/WorldGen/Editor/Geometry/Polygon.cs Assets/WorldGen/Tests/StreetGeometryTests.cs Assets/WorldGen/Tests/StreetGeometryTests.cs.meta
git -C "D:/UNITY/project/Tyco World" commit -m "feat(worldgen): polyline, solid box and mesh append helpers

Co-Authored-By: <model> <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

---

### Task 3: `RoadNetwork` — chain, prioritas, dan profil tepi per sisi

**Files:**
- Create: `Assets/WorldGen/Editor/Builders/RoadNetwork.cs`
- Test: `Assets/WorldGen/Tests/RoadNetworkTests.cs`

**Interfaces:**
- Consumes (Task 1–2): `RoadEdgeSettings`, `EdgeProfile`, `Polyline2D.*`, `SpatialGrid<T>`, `SplitMix64.Hash`. `RoadFeature` (`Id`, `Highway`, `Rule.rank`, `Width`, `Y`, `Pieces[*].Points`, `Tags`).
- Produces:
  - `interface IEdgeLookup`:
    - `EdgeProfile ProfileAt(long roadId, Vector2 p, Vector2 sideDirection)`
    - `float MaxEdgeWidth(long roadId)`
  - `class ChainMember { RoadFeature Road; float S0, S1; }`
  - `class RoadChain`:
    - fields: `int Index; long Id; long Key; string Highway; int Rank; float Width; float Y; EdgeProfile BaseProfile; bool CampusCandidate; float MaxEdgeWidth; List<Vector2> Points; float[] Cum; List<ChainMember> Members; EdgeProfile[] Left, Right`
    - members: `HalfWidth`, `Length`, `Profiles(int side)`, `SampleS(int i, float step)`, `ProfileAt(int side, float s, float step)`, `PointAt(float s, out Vector2 tangent)`, `SidePoint(float s, int side, float offset)`
  - `class RoadNetwork : IEdgeLookup`:
    - `static int[] Sides = { 1, -1 }` (+1 = kiri arah chain)
    - `List<RoadChain> Chains`, `float MaxHalfWidth`, `float MaxReach`
    - `RoadNetwork(IReadOnlyList<RoadFeature> roads, IReadOnlyList<Polygon> campus, RoadEdgeSettings settings)`
    - `static EdgeProfile BaseProfileOf(RoadFeature, RoadEdgeSettings)`
    - `static bool Contains(string[] values, string value)`
    - `static bool Beats(RoadChain a, RoadChain b)`
    - `static void Smooth(EdgeProfile[] profiles, int minSamples)`
    - `void Nearby(Vector2 p, float radius, RoadChain except, Dictionary<RoadChain, float> result)`
    - `float DistanceToChain(RoadChain chain, Vector2 p, float radius, float sMin, float sMax)`

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/WorldGen/Tests/RoadNetworkTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class RoadNetworkTests
    {
        WorldGenSettings s;

        [SetUp]
        public void SetUp() => s = DefaultSettingsFactory.CreateInMemory();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(s);

        static Vector2 V(float x, float y) => new Vector2(x, y);

        static Polygon Square(float x, float y, float w, float h) => new Polygon(new List<Vector2> { V(x, y), V(x + w, y), V(x + w, y + h), V(x, y + h) });

        RoadFeature Road(long id, string highway, float width, params Vector2[] pts)
        {
            var rule = s.roadRules.Single(r => r.highway == highway);
            var road = new RoadFeature
            {
                Id = id, Highway = highway, Rule = rule, Width = width, Y = s.roadBaseY + rule.rank * s.roadRankStep,
                Tags = TestOsm.Tags(("highway", highway))
            };
            road.Pieces.Add(new LinePiece(new List<Vector2>(pts), false, false));
            return road;
        }

        RoadNetwork Net(IReadOnlyList<Polygon> campus, params RoadFeature[] roads) => new RoadNetwork(roads, campus ?? new Polygon[0], s.roadEdges);

        [Test]
        public void TwoCollinearWays_FormOneChain()
        {
            var net = Net(null, Road(7, "residential", 5f, V(0, 0), V(50, 0)), Road(3, "residential", 5f, V(50, 0), V(100, 0)));
            Assert.AreEqual(1, net.Chains.Count);
            var c = net.Chains[0];
            Assert.AreEqual(100f, c.Length, 1e-3f);
            Assert.AreEqual(3, c.Points.Count);
            Assert.AreEqual(3, c.Id, "chain id = smallest way id");
            Assert.AreEqual(2, c.Members.Count);
            Assert.AreEqual(0f, c.Members[0].S0, 1e-3f);
            Assert.AreEqual(50f, c.Members[0].S1, 1e-3f);
            Assert.AreEqual(50f, c.Members[1].S0, 1e-3f);
            Assert.AreEqual(100f, c.Members[1].S1, 1e-3f);
            Assert.AreEqual(2.5f, c.HalfWidth);
        }

        [Test]
        public void ReversedWay_IsFlippedIntoTheChain()
        {
            var net = Net(null, Road(1, "residential", 5f, V(0, 0), V(50, 0)), Road(2, "residential", 5f, V(100, 0), V(50, 0)));
            Assert.AreEqual(1, net.Chains.Count);
            var c = net.Chains[0];
            Assert.AreEqual(V(0, 0), c.Points[0]);
            Assert.AreEqual(V(50, 0), c.Points[1]);
            Assert.AreEqual(V(100, 0), c.Points[2]);
            Assert.AreEqual(2, c.Members[1].Road.Id);
            Assert.AreEqual(50f, c.Members[1].S0, 1e-3f);
        }

        [Test]
        public void ThreeWaysAtANode_StaySeparate()
        {
            var net = Net(null, Road(1, "residential", 5f, V(0, 0), V(50, 0)), Road(2, "residential", 5f, V(50, 0), V(100, 0)),
                Road(3, "residential", 5f, V(50, 0), V(50, 50)));
            Assert.AreEqual(3, net.Chains.Count);
        }

        [Test]
        public void DifferentHighwaysOrWidths_StaySeparate()
        {
            Assert.AreEqual(2, Net(null, Road(1, "residential", 5f, V(0, 0), V(50, 0)), Road(2, "tertiary", 5f, V(50, 0), V(100, 0))).Chains.Count);
            Assert.AreEqual(2, Net(null, Road(1, "residential", 5f, V(0, 0), V(50, 0)), Road(2, "residential", 6f, V(50, 0), V(100, 0))).Chains.Count);
        }

        [Test]
        public void ClosedLoop_IsOneChain()
        {
            var net = Net(null, Road(1, "residential", 5f, V(0, 0), V(50, 0), V(50, 50)), Road(2, "residential", 5f, V(50, 50), V(0, 50), V(0, 0)));
            Assert.AreEqual(1, net.Chains.Count);
            Assert.AreEqual(200f, net.Chains[0].Length, 1e-3f);
        }

        [TestCase("primary", null, EdgeProfile.Sidewalk)]
        [TestCase("secondary", null, EdgeProfile.Sidewalk)]
        [TestCase("tertiary", null, EdgeProfile.Gutter)]
        [TestCase("residential", null, EdgeProfile.Gutter)]
        [TestCase("living_street", null, EdgeProfile.Gutter)]
        [TestCase("service", null, EdgeProfile.Gutter)]
        [TestCase("service", "alley", EdgeProfile.Gutter)]
        [TestCase("service", "driveway", EdgeProfile.None)]
        [TestCase("service", "parking_aisle", EdgeProfile.None)]
        [TestCase("service", "drive-through", EdgeProfile.None)]
        [TestCase("footway", null, EdgeProfile.None)]
        [TestCase("pedestrian", null, EdgeProfile.None)]
        [TestCase("track", null, EdgeProfile.None)]
        public void BaseProfile_FollowsHighwayAndServiceTag(string highway, string service, EdgeProfile expected)
        {
            var road = Road(1, highway, 5f, V(0, 0), V(10, 0));
            road.Tags = TestOsm.Tags(("highway", highway), ("service", service));
            Assert.AreEqual(expected, RoadNetwork.BaseProfileOf(road, s.roadEdges));
        }

        [Test]
        public void Tertiary_SideFacingTheCampus_GetsSidewalk()
        {
            // Travel east: +1 (left) is north. The campus starts 2 m north of the asphalt edge.
            var net = Net(new[] { Square(0, 5, 100, 55) }, Road(1, "tertiary", 6f, V(0, 0), V(100, 0)));
            var c = net.Chains[0];
            Assert.IsTrue(c.Left.All(p => p == EdgeProfile.Sidewalk));
            Assert.IsTrue(c.Right.All(p => p == EdgeProfile.Gutter), "the far side is within 10 m too, but faces away from the campus");
            Assert.AreEqual(1.5f, c.MaxEdgeWidth, 1e-5f);
            Assert.AreEqual(201, c.Left.Length);
        }

        [Test]
        public void Tertiary_InsideTheCampus_GetsSidewalksOnBothSides()
        {
            var c = Net(new[] { Square(-10, -50, 120, 100) }, Road(1, "tertiary", 6f, V(0, 0), V(100, 0))).Chains[0];
            Assert.IsTrue(c.Left.All(p => p == EdgeProfile.Sidewalk));
            Assert.IsTrue(c.Right.All(p => p == EdgeProfile.Sidewalk));
        }

        [Test]
        public void Residential_NextToTheCampus_StaysGutter()
        {
            var c = Net(new[] { Square(0, 5, 100, 55) }, Road(1, "residential", 5f, V(0, 0), V(100, 0))).Chains[0];
            Assert.IsTrue(c.Left.All(p => p == EdgeProfile.Gutter));
            Assert.AreEqual(0.8f, c.MaxEdgeWidth, 1e-5f);
        }

        [Test]
        public void ShortCampusStretch_FollowsItsNeighbours()
        {
            // A 1 m wide campus corner 9.55 m from the north sample line: within 10 m over only ~7 m of road (< 8 m).
            var c = Net(new[] { Square(50, 13.3f, 1, 7) }, Road(1, "tertiary", 6f, V(0, 0), V(100, 0))).Chains[0];
            Assert.IsTrue(c.Left.All(p => p == EdgeProfile.Gutter));
        }

        [Test]
        public void Smooth_ShortRunTakesTheLongerNeighbour()
        {
            var g = EdgeProfile.Gutter;
            var w = EdgeProfile.Sidewalk;
            var a = new[] { g, g, g, g, w, w, g, g, g };
            RoadNetwork.Smooth(a, 3);
            Assert.IsTrue(a.All(p => p == g));
            var b = new[] { g, g, w, w, w, w, w, w };
            RoadNetwork.Smooth(b, 3);
            Assert.IsTrue(b.All(p => p == w));
            var c = new[] { w, w, w, w, g, g, g, g };
            RoadNetwork.Smooth(c, 3);
            CollectionAssert.AreEqual(new[] { w, w, w, w, g, g, g, g }, c, "runs at least minSamples long stay");
        }

        [Test]
        public void Beats_RankThenWidthThenId()
        {
            var a = new RoadChain { Rank = 6, Width = 6f, Id = 9, Index = 0 };
            var b = new RoadChain { Rank = 4, Width = 8f, Id = 1, Index = 1 };
            Assert.IsTrue(RoadNetwork.Beats(a, b));
            Assert.IsFalse(RoadNetwork.Beats(b, a));
            var c = new RoadChain { Rank = 4, Width = 5f, Id = 0, Index = 2 };
            Assert.IsTrue(RoadNetwork.Beats(b, c), "same rank: wider wins");
            var d = new RoadChain { Rank = 4, Width = 5f, Id = 3, Index = 3 };
            Assert.IsTrue(RoadNetwork.Beats(c, d), "same rank and width: smaller id wins");
            var e = new RoadChain { Rank = 4, Width = 5f, Id = 3, Index = 4 };
            Assert.IsTrue(RoadNetwork.Beats(d, e), "same id (pieces of one way): lower index wins");
        }

        [Test]
        public void Nearby_ReportsTheClosestDistancePerChain()
        {
            var net = Net(null, Road(1, "residential", 5f, V(0, 0), V(100, 0)), Road(2, "residential", 5f, V(50, 10), V(50, 60)));
            var near = new Dictionary<RoadChain, float>();
            net.Nearby(V(50, 5), 20f, null, near);
            Assert.AreEqual(2, near.Count);
            Assert.IsTrue(near.Values.All(d => Mathf.Abs(d - 5f) < 1e-4f));
            net.Nearby(V(50, 5), 20f, net.Chains[0], near);
            Assert.AreEqual(1, near.Count);
            net.Nearby(V(50, 5), 3f, null, near);
            Assert.AreEqual(0, near.Count);
        }

        [Test]
        public void DistanceToChain_IgnoresTheNearbyStretch()
        {
            // A U-turn: the return leg runs 6 m north of the outbound leg.
            var net = Net(null, Road(1, "residential", 5f, V(0, 0), V(100, 0), V(100, 6), V(0, 6)));
            var chain = net.Chains[0];
            Assert.AreEqual(3f, net.DistanceToChain(chain, V(50, 3), 10f, 45f, 55f), 1e-4f, "return leg (s 106..206) still counts");
            Assert.AreEqual(10f, net.DistanceToChain(chain, V(50, 3), 10f, 0f, 300f), 1e-4f, "everything ignored: radius");
        }

        [Test]
        public void EdgeLookup_ByRoadAndSide()
        {
            var net = Net(new[] { Square(0, 5, 100, 55) }, Road(1, "tertiary", 6f, V(0, 0), V(100, 0)));
            Assert.AreEqual(EdgeProfile.Sidewalk, net.ProfileAt(1, V(50, 0), V(0, 1)));
            Assert.AreEqual(EdgeProfile.Gutter, net.ProfileAt(1, V(50, 0), V(0, -1)));
            Assert.AreEqual(EdgeProfile.None, net.ProfileAt(99, V(50, 0), V(0, 1)));
            Assert.AreEqual(1.5f, net.MaxEdgeWidth(1), 1e-5f);
            Assert.AreEqual(0f, net.MaxEdgeWidth(99));
            Assert.AreEqual(3f, net.MaxHalfWidth, 1e-5f);
            Assert.AreEqual(3f + 1.5f + 1f, net.MaxReach, 1e-5f);
        }

        [Test]
        public void ChainKey_DiffersForPiecesOfOneWay()
        {
            var road = Road(1, "residential", 5f, V(0, 0), V(50, 0));
            road.Pieces.Add(new LinePiece(new List<Vector2> { V(200, 0), V(250, 0) }, false, false));
            var net = Net(null, road);
            Assert.AreEqual(2, net.Chains.Count);
            Assert.AreEqual(net.Chains[0].Id, net.Chains[1].Id);
            Assert.AreNotEqual(net.Chains[0].Key, net.Chains[1].Key);
        }
    }
}
```

- [ ] **Step 2: Jalankan, pastikan gagal**

Procedure R+T, `Group = "RoadNetworkTests"`. Expected: `COMPILE FAILED`.

- [ ] **Step 3: Implementasi**

`Assets/WorldGen/Editor/Builders/RoadNetwork.cs`:

```csharp
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Edge profile and edge width of a road, as seen by props placed along one OSM way.</summary>
    public interface IEdgeLookup
    {
        /// <summary>Profile of the road side that <paramref name="sideDirection"/> points to, near centreline point p.</summary>
        EdgeProfile ProfileAt(long roadId, Vector2 p, Vector2 sideDirection);

        /// <summary>Widest edge on either side of the road; 0 when it has none.</summary>
        float MaxEdgeWidth(long roadId);
    }

    public sealed class ChainMember
    {
        public RoadFeature Road;
        public float S0, S1;
    }

    /// <summary>OSM way pieces of one kind of road joined end to end (spec street-frontage §5).</summary>
    public sealed class RoadChain
    {
        public int Index;
        public long Id;
        /// <summary>Stable seed: smallest way id plus the quantised first point (pieces of one clipped way differ).</summary>
        public long Key;
        public string Highway = "";
        public int Rank;
        public float Width;
        public float Y;
        public EdgeProfile BaseProfile;
        public bool CampusCandidate;
        public float MaxEdgeWidth;
        public List<Vector2> Points = new List<Vector2>();
        public float[] Cum = new float[0];
        public readonly List<ChainMember> Members = new List<ChainMember>();
        /// <summary>Profile per sample (s = i × step) on the left (+1) and right (-1) side, after the campus rule and smoothing.</summary>
        public EdgeProfile[] Left = new EdgeProfile[0];
        public EdgeProfile[] Right = new EdgeProfile[0];

        public float HalfWidth => Width * 0.5f;
        public float Length => Cum.Length > 0 ? Cum[Cum.Length - 1] : 0f;
        public EdgeProfile[] Profiles(int side) => side > 0 ? Left : Right;

        /// <summary>Arc length of sample i; the last sample sits on the chain end.</summary>
        public float SampleS(int i, float step) => Mathf.Min(i * step, Length);

        public EdgeProfile ProfileAt(int side, float s, float step)
        {
            var p = Profiles(side);
            return p.Length == 0 ? EdgeProfile.None : p[Mathf.Clamp(Mathf.RoundToInt(s / step), 0, p.Length - 1)];
        }

        public Vector2 PointAt(float s, out Vector2 tangent) => Polyline2D.PointAt(Points, Cum, s, out tangent);

        /// <summary>Point at arc length s, <paramref name="offset"/> metres to the given side (+1 left) of the centreline.</summary>
        public Vector2 SidePoint(float s, int side, float offset)
        {
            var c = PointAt(s, out var t);
            return c + Polyline2D.LeftNormal(t) * (side * offset);
        }
    }

    /// <summary>Road chains, per-side edge profiles and a segment grid for distance queries (spec street-frontage §4–§5).</summary>
    public sealed class RoadNetwork : IEdgeLookup
    {
        public static readonly int[] Sides = { 1, -1 };

        public readonly List<RoadChain> Chains = new List<RoadChain>();
        public float MaxHalfWidth { get; private set; }
        /// <summary>Largest half width + edge width of any chain, plus 1 m: a safe query radius for band tests.</summary>
        public float MaxReach { get; private set; }

        readonly RoadEdgeSettings settings;
        readonly Dictionary<long, List<RoadChain>> chainsOfRoad = new Dictionary<long, List<RoadChain>>();
        readonly List<Seg> segs = new List<Seg>();
        readonly SpatialGrid<int> grid = new SpatialGrid<int>(20f);
        readonly HashSet<int> hits = new HashSet<int>();

        readonly struct Seg
        {
            public readonly int Chain;
            public readonly Vector2 A, B;
            public readonly float S0, S1;

            public Seg(int chain, Vector2 a, Vector2 b, float s0, float s1)
            {
                Chain = chain;
                A = a;
                B = b;
                S0 = s0;
                S1 = s1;
            }
        }

        sealed class Unit
        {
            public RoadFeature Road;
            public List<Vector2> Points;
            public string MergeKey;
            public int StartLink = -1, EndLink = -1;
            public bool StartLinkAtStart, EndLinkAtStart;
        }

        public RoadNetwork(IReadOnlyList<RoadFeature> roads, IReadOnlyList<Polygon> campus, RoadEdgeSettings settings)
        {
            this.settings = settings;
            BuildChains(roads);
            foreach (var chain in Chains) IndexSegments(chain);
            foreach (var chain in Chains)
            {
                chain.Left = ComputeProfiles(chain, 1, campus);
                chain.Right = ComputeProfiles(chain, -1, campus);
                float max = 0f;
                foreach (var p in chain.Left) max = Mathf.Max(max, settings.Width(p));
                foreach (var p in chain.Right) max = Mathf.Max(max, settings.Width(p));
                chain.MaxEdgeWidth = max;
                MaxHalfWidth = Mathf.Max(MaxHalfWidth, chain.HalfWidth);
                MaxReach = Mathf.Max(MaxReach, chain.HalfWidth + max + 1f);
            }
        }

        public static EdgeProfile BaseProfileOf(RoadFeature road, RoadEdgeSettings s)
        {
            string highway = road.Highway ?? "";
            if (Contains(s.sidewalkHighways, highway)) return EdgeProfile.Sidewalk;
            if (!Contains(s.gutterHighways, highway)) return EdgeProfile.None;
            if (highway == "service" && road.Tags != null && road.Tags.TryGetValue("service", out var value) && Contains(s.noEdgeServiceValues, value))
                return EdgeProfile.None;
            return EdgeProfile.Gutter;
        }

        public static bool Contains(string[] values, string value)
        {
            if (values == null) return false;
            foreach (var v in values)
                if (v == value) return true;
            return false;
        }

        /// <summary>Junction priority: higher rank, then wider, then smaller id, then lower index (spec street-frontage §5).</summary>
        public static bool Beats(RoadChain a, RoadChain b)
        {
            if (a.Rank != b.Rank) return a.Rank > b.Rank;
            if (Mathf.Abs(a.Width - b.Width) > 1e-3f) return a.Width > b.Width;
            if (a.Id != b.Id) return a.Id < b.Id;
            return a.Index < b.Index;
        }

        /// <summary>Closest distance from p to each chain (except <paramref name="except"/>) whose centreline is within radius.</summary>
        public void Nearby(Vector2 p, float radius, RoadChain except, Dictionary<RoadChain, float> result)
        {
            result.Clear();
            hits.Clear();
            grid.Query(Around(p, radius), hits);
            foreach (int i in hits)
            {
                var seg = segs[i];
                var chain = Chains[seg.Chain];
                if (chain == except) continue;
                float d = Polygon2D.DistanceToSegment(p, seg.A, seg.B);
                if (d > radius) continue;
                if (!result.TryGetValue(chain, out float old) || d < old) result[chain] = d;
            }
        }

        /// <summary>
        /// Distance from p to the chain's own centreline, ignoring segments whose arc range overlaps [sMin, sMax] (the stretch
        /// p was offset from). Returns <paramref name="radius"/> when nothing is closer.
        /// </summary>
        public float DistanceToChain(RoadChain chain, Vector2 p, float radius, float sMin, float sMax)
        {
            hits.Clear();
            grid.Query(Around(p, radius), hits);
            float best = radius;
            foreach (int i in hits)
            {
                var seg = segs[i];
                if (seg.Chain != chain.Index || (seg.S1 > sMin && seg.S0 < sMax)) continue;
                best = Mathf.Min(best, Polygon2D.DistanceToSegment(p, seg.A, seg.B));
            }
            return best;
        }

        public EdgeProfile ProfileAt(long roadId, Vector2 p, Vector2 sideDirection)
        {
            var chain = Closest(roadId, p, out float s);
            if (chain == null) return EdgeProfile.None;
            chain.PointAt(s, out var t);
            int side = Vector2.Dot(sideDirection, Polyline2D.LeftNormal(t)) >= 0f ? 1 : -1;
            return chain.ProfileAt(side, s, settings.sampleStep);
        }

        public float MaxEdgeWidth(long roadId)
        {
            if (!chainsOfRoad.TryGetValue(roadId, out var list)) return 0f;
            float max = 0f;
            foreach (var c in list) max = Mathf.Max(max, c.MaxEdgeWidth);
            return max;
        }

        /// <summary>Runs shorter than minSamples take the profile of their longer neighbour (spec street-frontage §4).</summary>
        public static void Smooth(EdgeProfile[] profiles, int minSamples)
        {
            while (true)
            {
                var runs = Runs(profiles);
                if (runs.Count < 2) return;
                int pick = -1;
                for (int r = 0; r < runs.Count; r++)
                    if (runs[r].count < minSamples && (pick < 0 || runs[r].count < runs[pick].count)) pick = r;
                if (pick < 0) return;
                int neighbour = pick == 0 ? 1
                    : pick == runs.Count - 1 ? pick - 1
                    : runs[pick - 1].count >= runs[pick + 1].count ? pick - 1 : pick + 1;
                for (int i = runs[pick].start; i < runs[pick].start + runs[pick].count; i++) profiles[i] = runs[neighbour].profile;
            }
        }

        static List<(EdgeProfile profile, int start, int count)> Runs(EdgeProfile[] profiles)
        {
            var runs = new List<(EdgeProfile profile, int start, int count)>();
            int start = 0;
            for (int i = 1; i <= profiles.Length; i++)
            {
                if (i < profiles.Length && profiles[i] == profiles[start]) continue;
                runs.Add((profiles[start], start, i - start));
                start = i;
            }
            return runs;
        }

        RoadChain Closest(long roadId, Vector2 p, out float s)
        {
            s = 0f;
            if (!chainsOfRoad.TryGetValue(roadId, out var list)) return null;
            RoadChain best = null;
            float bestDistance = float.PositiveInfinity;
            foreach (var c in list)
            {
                float cs = Polyline2D.Project(c.Points, c.Cum, p, out float d);
                if (d >= bestDistance) continue;
                bestDistance = d;
                best = c;
                s = cs;
            }
            return best;
        }

        EdgeProfile[] ComputeProfiles(RoadChain chain, int side, IReadOnlyList<Polygon> campus)
        {
            float step = settings.sampleStep;
            int n = Mathf.Max(1, Mathf.CeilToInt(chain.Length / step - 1e-4f) + 1);
            var result = new EdgeProfile[n];
            float probe = chain.HalfWidth + settings.sidewalkWidth * 0.5f;
            float cap = settings.campusSidewalkDistance + 1f;
            for (int i = 0; i < n; i++)
            {
                var profile = chain.BaseProfile;
                if (profile == EdgeProfile.Gutter && chain.CampusCandidate && campus != null && campus.Count > 0)
                {
                    float s = chain.SampleS(i, step);
                    float here = CampusDistance(campus, chain.SidePoint(s, side, probe), cap);
                    float there = CampusDistance(campus, chain.SidePoint(s, -side, probe), cap);
                    bool facing = here <= settings.campusSidewalkDistance && (here < there - 0.5f || (here == 0f && there == 0f));
                    if (facing) profile = EdgeProfile.Sidewalk;
                }
                result[i] = profile;
            }
            Smooth(result, Mathf.RoundToInt(settings.minProfileRun / step));
            return result;
        }

        static float CampusDistance(IReadOnlyList<Polygon> polygons, Vector2 p, float cap)
        {
            float best = cap;
            foreach (var poly in polygons)
            {
                var b = poly.Bounds;
                if (p.x < b.xMin - cap || p.x > b.xMax + cap || p.y < b.yMin - cap || p.y > b.yMax + cap) continue;
                if (poly.Contains(p)) return 0f;
                best = Mathf.Min(best, poly.DistanceToBoundary(p));
            }
            return best;
        }

        void BuildChains(IReadOnlyList<RoadFeature> roads)
        {
            var units = new List<Unit>();
            foreach (var road in roads)
                foreach (var piece in road.Pieces)
                {
                    var pts = Dedup(piece.Points);
                    if (pts.Count < 2) continue;
                    units.Add(new Unit { Road = road, Points = pts, MergeKey = MergeKeyOf(road) });
                }

            var vertexUse = new Dictionary<long, int>();
            var endpoints = new Dictionary<long, List<(int unit, bool atStart)>>();
            for (int u = 0; u < units.Count; u++)
            {
                var pts = units[u].Points;
                foreach (var p in pts)
                {
                    long k = Quantize(p);
                    vertexUse[k] = vertexUse.TryGetValue(k, out int c) ? c + 1 : 1;
                }
                AddEndpoint(endpoints, Quantize(pts[0]), u, true);
                AddEndpoint(endpoints, Quantize(pts[pts.Count - 1]), u, false);
            }
            // Each link is symmetric and every unit end sits under exactly one key, so iteration order does not matter.
            foreach (var kv in endpoints)
            {
                var ends = kv.Value;
                if (ends.Count != 2 || vertexUse[kv.Key] != 2) continue;
                var (a, aStart) = ends[0];
                var (b, bStart) = ends[1];
                if (a == b || units[a].MergeKey != units[b].MergeKey) continue;
                Link(units[a], aStart, b, bStart);
                Link(units[b], bStart, a, aStart);
            }

            var visited = new bool[units.Count];
            for (int u = 0; u < units.Count; u++)
            {
                if (visited[u] || (units[u].StartLink >= 0 && units[u].EndLink >= 0)) continue;
                Walk(units, visited, u, units[u].StartLink < 0);
            }
            for (int u = 0; u < units.Count; u++)
                if (!visited[u]) Walk(units, visited, u, true);
        }

        void Walk(List<Unit> units, bool[] visited, int first, bool forward)
        {
            var chain = new RoadChain { Index = Chains.Count };
            var ranges = new List<(RoadFeature road, int start, int end)>();
            int u = first;
            bool fwd = forward;
            while (u >= 0 && !visited[u])
            {
                visited[u] = true;
                var unit = units[u];
                int start = chain.Points.Count == 0 ? 0 : chain.Points.Count - 1;
                for (int i = 0; i < unit.Points.Count; i++)
                {
                    if (i == 0 && chain.Points.Count > 0) continue;
                    chain.Points.Add(fwd ? unit.Points[i] : unit.Points[unit.Points.Count - 1 - i]);
                }
                ranges.Add((unit.Road, start, chain.Points.Count - 1));
                bool enterAtStart = fwd ? unit.EndLinkAtStart : unit.StartLinkAtStart;
                u = fwd ? unit.EndLink : unit.StartLink;
                fwd = enterAtStart;
            }
            chain.Cum = Polyline2D.Cumulative(chain.Points);
            var head = ranges[0].road;
            chain.Highway = head.Highway ?? "";
            chain.Rank = head.Rule != null ? head.Rule.rank : 0;
            chain.Width = head.Width;
            chain.Y = head.Y;
            chain.BaseProfile = BaseProfileOf(head, settings);
            chain.CampusCandidate = Contains(settings.campusSidewalkHighways, chain.Highway);
            long id = long.MaxValue;
            foreach (var (road, start, end) in ranges)
            {
                chain.Members.Add(new ChainMember { Road = road, S0 = chain.Cum[start], S1 = chain.Cum[end] });
                if (road.Id < id) id = road.Id;
                if (!chainsOfRoad.TryGetValue(road.Id, out var list)) chainsOfRoad[road.Id] = list = new List<RoadChain>();
                if (!list.Contains(chain)) list.Add(chain);
            }
            chain.Id = id;
            chain.Key = unchecked((long)SplitMix64.Hash(id, "chain:" + Quantize(chain.Points[0]).ToString(CultureInfo.InvariantCulture)));
            Chains.Add(chain);
        }

        void IndexSegments(RoadChain chain)
        {
            var pts = chain.Points;
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                Vector2 a = pts[i], b = pts[i + 1];
                grid.Insert(Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y)), segs.Count);
                segs.Add(new Seg(chain.Index, a, b, chain.Cum[i], chain.Cum[i + 1]));
            }
        }

        string MergeKeyOf(RoadFeature road) =>
            (road.Highway ?? "") + "|" + Mathf.RoundToInt(road.Width * 100f).ToString(CultureInfo.InvariantCulture) + "|" + BaseProfileOf(road, settings);

        static void AddEndpoint(Dictionary<long, List<(int unit, bool atStart)>> map, long key, int unit, bool atStart)
        {
            if (!map.TryGetValue(key, out var list)) map[key] = list = new List<(int unit, bool atStart)>();
            list.Add((unit, atStart));
        }

        static void Link(Unit unit, bool atStart, int other, bool otherAtStart)
        {
            if (atStart)
            {
                unit.StartLink = other;
                unit.StartLinkAtStart = otherAtStart;
            }
            else
            {
                unit.EndLink = other;
                unit.EndLinkAtStart = otherAtStart;
            }
        }

        static List<Vector2> Dedup(IReadOnlyList<Vector2> points)
        {
            var result = new List<Vector2>(points.Count);
            foreach (var p in points)
                if (result.Count == 0 || (p - result[result.Count - 1]).sqrMagnitude > 1e-4f) result.Add(p);
            return result;
        }

        static long Quantize(Vector2 p) => ((long)Mathf.RoundToInt(p.x * 100f) << 32) ^ (uint)Mathf.RoundToInt(p.y * 100f);

        static Rect Around(Vector2 p, float r) => Rect.MinMaxRect(p.x - r, p.y - r, p.x + r, p.y + r);
    }
}
```

Catatan untuk implementer:
- `Walk` berjalan dari unit yang ujungnya bebas.
  - "Masuk lewat start" berarti unit berikutnya dijalani maju; "masuk lewat end" berarti dibalik.
  - Loop tertutup diproses di putaran kedua, mulai dari unit pertama yang belum dikunjungi, arah maju.
- Di `ClosedLoop_IsOneChain` kedua way bertemu di dua titik, (50,50) dan (0,0), dan keduanya ter-link. Unit 0 punya StartLink dan EndLink, jadi dilewati di putaran pertama. Putaran kedua berjalan maju dari unit 0, lalu unit 1, lalu kembali ke unit 0 (sudah dikunjungi) dan berhenti. Panjang chain 200.

- [ ] **Step 4: Jalankan, pastikan lulus**

Procedure R+T, `Group = "RoadNetworkTests"`. Expected: `failed=0`.

- [ ] **Step 5: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add Assets/WorldGen/Editor/Builders/RoadNetwork.cs Assets/WorldGen/Editor/Builders/RoadNetwork.cs.meta Assets/WorldGen/Tests/RoadNetworkTests.cs Assets/WorldGen/Tests/RoadNetworkTests.cs.meta
git -C "D:/UNITY/project/Tyco World" commit -m "feat(worldgen): road network chains with per-side edge profiles

Co-Authored-By: <model> <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

---

### Task 4: `StreetContext`, `StreetFixture`, dan `RoadEdgeBuilder`

**Files:**
- Create: `Assets/WorldGen/Editor/Builders/StreetContext.cs`
- Create: `Assets/WorldGen/Editor/Builders/RoadEdgeBuilder.cs`
- Create: `Assets/WorldGen/Tests/StreetFixture.cs`
- Test: `Assets/WorldGen/Tests/RoadEdgeBuilderTests.cs`

**Interfaces:**
- Consumes:
  - Task 1–3.
  - `ClassifiedWorld` (`LocalBounds`, `Buildings`, `WaterAreas`, `WaterLines`, `Areas`).
  - `BuildingFeature.Spec.Footprint`, `.Centroid`, `.Key.prefab`.
  - `WaterLineFeature.Width`, `.Pieces`.
  - `AreaFeature.Rule.category`, `.Shape`.
  - `FeatureClassifier(settings, report).Classify(OsmData)`.
  - `WorldMeshPipeline.ChunkOf(Vector2, float)`.
- Produces:
  - `class StreetBuilding { long Id; Polygon Footprint; Vector2 Centroid; bool Excluded; bool IsInfill; }`
  - `class StreetContext`:
    - fields: `Rect Bounds`, `Polygon Kos`, `List<StreetBuilding> Buildings`
    - constructor: `StreetContext(Rect, IEnumerable<StreetBuilding>, IEnumerable<WaterAreaFeature>, IEnumerable<WaterLineFeature>, IEnumerable<AreaFeature>, Polygon kos)`
    - `static StreetContext From(ClassifiedWorld, WorldGenSettings)`
    - `void AddBuilding(StreetBuilding)`
    - `bool InBounds(Vector2)`, `StreetBuilding BuildingAt(Vector2)`, `bool InsideBuilding(Vector2)`, `bool InWater(Vector2)`
    - `bool InArea(Vector2, AreaCategory)`, `bool InAnyArea(Vector2)`, `float DistanceToKos(Vector2)`
    - `StreetBuilding Raycast(Vector2 origin, Vector2 dir, float maxDistance, out float distance)` (null bila tidak kena)
    - `bool OverlapsBuilding(IReadOnlyList<Vector2>)`, `bool OverlapsWater(IReadOnlyList<Vector2>)`, `bool OverlapsArea(IReadOnlyList<Vector2>)`, `bool NearKos(IReadOnlyList<Vector2> ring, float radius)`
  - `class EdgeRun { RoadChain Chain; int Side; EdgeProfile Profile; float S0, S1; float Length; }`
  - `class RoadEdgeResult { List<EdgeRun> Runs; Dictionary<Vector2Int, MeshData> Gutter, Sidewalk; float GutterMeters, SidewalkMeters; }`
  - `static class RoadEdgeBuilder`:
    - names: `GutterName = "Gutter"`, `SidewalkName = "Sidewalk"`
    - submeshes: `GutterConcrete = 0`, `GutterChannel = 1`, `GutterSubmeshes = 2`, `SidewalkBody = 0`, `SidewalkKerb = 1`, `SidewalkConcrete = 2`, `SidewalkSubmeshes = 3`
    - `static RoadEdgeResult Build(RoadNetwork, StreetContext, RoadEdgeSettings, float chunkSize)`
    - `static bool Keep(RoadChain, int side, float s, float width, RoadNetwork, StreetContext, RoadEdgeSettings, Dictionary<RoadChain,float> near)`
  - Test fixture `StreetFixture` (internal):
    - constants: `Residential, Tertiary, Service, Driveway, Footway, Campus, Park, SetbackHouse, FlushHouse, Shed, Drain, Kos`
    - members: `Settings`, `Report`, `World`, `Network`, `Context`, `static TestOsm Osm()`, `RoadChain Chain(long roadId)`
    - constructor: `StreetFixture(WorldGenSettings settings, System.Action<TestOsm> extra = null)`
  - Task 6, 7, 10, 14, dan 15 juga memakai fixture ini.

- [ ] **Step 1: Tulis fixture dan tes yang gagal**

`Assets/WorldGen/Tests/StreetFixture.cs`:

```csharp
using System;
using System.Linq;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    /// <summary>
    /// Synthetic street block for the street-frontage tests (spec street-frontage §14), local metres (x east, z north):
    ///
    ///   z 64..120  campus (amenity=university), x 0..200
    ///   z 60       T1 tertiary, lanes 2 (6 m): (0,60) → (100,60) → (200,60)
    ///   x 100      S1 service (100,0) → (100,60)
    ///   z 4.5      drain (waterway, 1.5 m) x 0..90, north of R1
    ///   kos        x 120..130, z 10..20 (way 409467778, key building without prefab)
    ///   z 0        R1 residential (5 m): (0,0) → (100,0) → (200,0)
    ///   south of R1: H1 setback house x 20..30 (front z -7.7, 4 m behind the property line), footway F1 at x 40,
    ///                H2 flush house x 60..70 (front z -4.2), driveway D1 at x 150,
    ///                park x 160..200 z -40..-4, shed x 180..185 z -10..-3 (over R1's south edge band).
    /// </summary>
    internal sealed class StreetFixture
    {
        public const long Residential = 1001, Tertiary = 1002, Service = 1003, Driveway = 1004, Footway = 1005;
        public const long Campus = 1010, Park = 1011, SetbackHouse = 1020, FlushHouse = 1021, Shed = 1022, Drain = 1030;
        public const long Kos = 409467778;

        public readonly WorldGenSettings Settings;
        public readonly GenerationReport Report = new GenerationReport();
        public readonly ClassifiedWorld World;
        public readonly RoadNetwork Network;
        public readonly StreetContext Context;

        public StreetFixture(WorldGenSettings settings, Action<TestOsm> extra = null)
        {
            Settings = settings;
            var osm = Osm();
            extra?.Invoke(osm);
            World = new FeatureClassifier(settings, Report).Classify(osm.Data);
            var campus = World.Areas.Where(a => a.Rule != null && a.Rule.category == AreaCategory.Campus).Select(a => a.Shape).ToList();
            Network = new RoadNetwork(World.Roads, campus, settings.roadEdges);
            Context = StreetContext.From(World, settings);
        }

        public static TestOsm Osm()
        {
            var osm = new TestOsm();
            osm.Way(Residential, new[] { new Vector2(0f, 0f), new Vector2(100f, 0f), new Vector2(200f, 0f) }, false, ("highway", "residential"));
            osm.Way(Tertiary, new[] { new Vector2(0f, 60f), new Vector2(100f, 60f), new Vector2(200f, 60f) }, false, ("highway", "tertiary"), ("lanes", "2"));
            osm.Way(Service, new[] { new Vector2(100f, 0f), new Vector2(100f, 60f) }, false, ("highway", "service"));
            osm.Way(Driveway, new[] { new Vector2(150f, 0f), new Vector2(150f, -20f) }, false, ("highway", "service"), ("service", "driveway"));
            osm.Way(Footway, new[] { new Vector2(40f, 0f), new Vector2(40f, -30f) }, false, ("highway", "footway"));
            osm.Rect(Campus, 0f, 64f, 200f, 56f, ("amenity", "university"));
            osm.Rect(Park, 160f, -40f, 40f, 36f, ("leisure", "park"));
            osm.Rect(SetbackHouse, 20f, -17.7f, 10f, 10f, ("building", "house"));
            osm.Rect(FlushHouse, 60f, -14.2f, 10f, 10f, ("building", "house"));
            osm.Rect(Shed, 180f, -10f, 5f, 7f, ("building", "shed"));
            osm.Way(Drain, new[] { new Vector2(0f, 4.5f), new Vector2(90f, 4.5f) }, false, ("waterway", "drain"));
            osm.Rect(Kos, 120f, 10f, 10f, 10f, ("building", "house"));
            return osm;
        }

        public RoadChain Chain(long roadId) => Network.Chains.Single(c => c.Members.Any(m => m.Road.Id == roadId));
    }
}
```

`Assets/WorldGen/Tests/RoadEdgeBuilderTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class RoadEdgeBuilderTests
    {
        WorldGenSettings settings;

        [SetUp]
        public void SetUp() => settings = DefaultSettingsFactory.CreateInMemory();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(settings);

        static Vector2 V(float x, float y) => new Vector2(x, y);

        RoadEdgeResult Build(StreetFixture fx) => RoadEdgeBuilder.Build(fx.Network, fx.Context, settings.roadEdges, 100f);

        static List<EdgeRun> RunsOf(RoadEdgeResult r, long roadId, int side) =>
            r.Runs.Where(x => x.Side == side && x.Chain.Members.Any(m => m.Road.Id == roadId)).ToList();

        static bool Covers(IEnumerable<EdgeRun> runs, float s0, float s1) => runs.Any(r => r.S1 > s0 && r.S0 < s1);

        static IEnumerable<MeshData> Meshes(RoadEdgeResult r) => r.Gutter.Values.Concat(r.Sidewalk.Values);

        [Test]
        public void Heights_AreOnlyTheSpecLayers()
        {
            var r = Build(new StreetFixture(settings));
            var allowed = new[] { 0f, 0.055f, 0.155f, 0.21f };
            Assert.Greater(Meshes(r).Sum(m => m.Vertices.Count), 0);
            foreach (var mesh in Meshes(r))
                foreach (var v in mesh.Vertices)
                    Assert.IsTrue(allowed.Any(y => Mathf.Abs(v.y - y) < 1e-4f), "y=" + v.y);
        }

        [Test]
        public void ResidentialEdge_LeavesAMouthForTheServiceRoad()
        {
            var r = Build(new StreetFixture(settings));
            // R1 runs east, so +1 (left) is north, where S1 joins at x = 100: mouth = 1.75 + 0.3.
            var north = RunsOf(r, StreetFixture.Residential, 1);
            Assert.IsFalse(Covers(north, 100f - 2.0f, 100f + 2.0f));
            Assert.IsTrue(Covers(north, 103f, 110f));
            Assert.IsTrue(north.All(x => x.Profile == EdgeProfile.Gutter));
        }

        [Test]
        public void ServiceEdge_StopsAtTheBandsOfTheRoadsItJoins()
        {
            var r = Build(new StreetFixture(settings));
            var runs = RunsOf(r, StreetFixture.Service, 1).Concat(RunsOf(r, StreetFixture.Service, -1)).ToList();
            Assert.AreEqual(2, runs.Count);
            foreach (var run in runs)
            {
                Assert.GreaterOrEqual(run.S0, 2.5f + 0.8f - 0.07f, "R1 band: half width + gutter");
                Assert.LessOrEqual(run.S1, 60f - (3f + 1.5f) + 0.07f, "T1 band: half width + widest edge (campus sidewalk)");
                Assert.Greater(run.Length, 50f);
            }
        }

        [Test]
        public void DrivewayAndFootway_HaveNoEdges()
        {
            var r = Build(new StreetFixture(settings));
            Assert.IsFalse(r.Runs.Any(x => x.Chain.Members.Any(m => m.Road.Id == StreetFixture.Driveway || m.Road.Id == StreetFixture.Footway)));
        }

        [Test]
        public void FootwayAndDriveway_CutMouthsIntoTheResidentialEdge()
        {
            var south = RunsOf(Build(new StreetFixture(settings)), StreetFixture.Residential, -1);
            Assert.IsFalse(Covers(south, 40f - 1.0f, 40f + 1.0f), "footway: 0.75 + 0.3");
            Assert.IsFalse(Covers(south, 150f - 2.0f, 150f + 2.0f), "driveway: 1.75 + 0.3");
        }

        [Test]
        public void ShedOverTheEdge_CutsIt()
        {
            var south = RunsOf(Build(new StreetFixture(settings)), StreetFixture.Residential, -1);
            Assert.IsFalse(Covers(south, 180.1f, 184.9f));
            Assert.IsTrue(Covers(south, 170f, 175f));
        }

        [Test]
        public void CampusSide_IsSidewalk_OtherSideIsGutter()
        {
            var r = Build(new StreetFixture(settings));
            var north = RunsOf(r, StreetFixture.Tertiary, 1);
            var south = RunsOf(r, StreetFixture.Tertiary, -1);
            Assert.IsTrue(north.All(x => x.Profile == EdgeProfile.Sidewalk));
            Assert.IsTrue(south.All(x => x.Profile == EdgeProfile.Gutter));
            Assert.Greater(north.Sum(x => x.Length), 195f);
            Assert.Greater(r.Sidewalk.Count, 0);
        }

        [Test]
        public void Drain_BesideTheEdge_DoesNotCutIt()
        {
            var north = RunsOf(Build(new StreetFixture(settings)), StreetFixture.Residential, 1);
            Assert.Greater(north.Where(x => x.S0 < 90f).Sum(x => Mathf.Min(x.S1, 90f) - x.S0), 85f);
        }

        [Test]
        public void SameInput_SameMeshes()
        {
            var a = Build(new StreetFixture(settings));
            var b = Build(new StreetFixture(settings));
            Assert.AreEqual(a.Runs.Count, b.Runs.Count);
            CollectionAssert.AreEquivalent(a.Gutter.Keys, b.Gutter.Keys);
            foreach (var key in a.Gutter.Keys) Assert.AreEqual(a.Gutter[key].ContentHash(), b.Gutter[key].ContentHash());
            foreach (var key in a.Sidewalk.Keys) Assert.AreEqual(a.Sidewalk[key].ContentHash(), b.Sidewalk[key].ContentHash());
        }

        [Test]
        public void Meshes_AreValid_AndMetersMatchTheRuns()
        {
            var r = Build(new StreetFixture(settings));
            foreach (var mesh in Meshes(r)) Assert.IsFalse(mesh.HasInvalidValues());
            Assert.AreEqual(r.Runs.Where(x => x.Profile == EdgeProfile.Gutter).Sum(x => x.Length), r.GutterMeters, 1e-2f);
            Assert.AreEqual(r.Runs.Where(x => x.Profile == EdgeProfile.Sidewalk).Sum(x => x.Length), r.SidewalkMeters, 1e-2f);
            Assert.IsTrue(r.Runs.All(x => x.Length >= settings.roadEdges.minRunLength));
            Assert.IsTrue(r.Gutter.Values.All(m => m.SubmeshCount == RoadEdgeBuilder.GutterSubmeshes));
            Assert.IsTrue(r.Sidewalk.Values.All(m => m.SubmeshCount == RoadEdgeBuilder.SidewalkSubmeshes));
            Assert.IsTrue(r.Gutter.Values.Any(m => m.Indices(RoadEdgeBuilder.GutterChannel).Count > 0));
            Assert.IsTrue(r.Sidewalk.Values.Any(m => m.Indices(RoadEdgeBuilder.SidewalkKerb).Count > 0));
        }

        [Test]
        public void RightAngleBend_EdgesContinueAroundTheCorner()
        {
            var fx = new StreetFixture(settings, osm => osm.Way(2001, new[] { V(-300, -300), V(-250, -300), V(-250, -250) }, false, ("highway", "residential")));
            var r = Build(fx);
            foreach (int side in RoadNetwork.Sides)
            {
                var runs = RunsOf(r, 2001, side);
                Assert.AreEqual(1, runs.Count, "side " + side);
                Assert.LessOrEqual(runs[0].S0, 0.01f);
                Assert.GreaterOrEqual(runs[0].S1, 99.99f);
            }
        }

        [Test]
        public void RoadLeavingTheWorld_EdgesStayInside()
        {
            var bounds = new StreetFixture(settings).World.LocalBounds;
            var fx = new StreetFixture(settings, osm => osm.Way(2002, new[] { V(bounds.xMax - 50f, 300f), V(bounds.xMax + 50f, 300f) }, false, ("highway", "residential")));
            var r = Build(fx);
            Assert.Greater(RunsOf(r, 2002, 1).Count, 0);
            foreach (var mesh in Meshes(r))
                foreach (var v in mesh.Vertices)
                    Assert.LessOrEqual(v.x, bounds.xMax + 0.01f);
        }
    }
}
```

- [ ] **Step 2: Jalankan, pastikan gagal**

Procedure R+T, `Group = "RoadEdgeBuilderTests"`. Expected: `COMPILE FAILED`.

- [ ] **Step 3: Implementasi `StreetContext`**

`Assets/WorldGen/Editor/Builders/StreetContext.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>A footprint the street builders must respect: an OSM building or an infill house.</summary>
    public sealed class StreetBuilding
    {
        public long Id;
        public Polygon Footprint;
        public Vector2 Centroid;
        /// <summary>Key building replaced by a hand-made prefab: no wall and no gate in front of it.</summary>
        public bool Excluded;
        public bool IsInfill;
    }

    /// <summary>Spatial lookups shared by the edge, frontage and infill builders (spec street-frontage §5, §7, §8).</summary>
    public sealed class StreetContext
    {
        public readonly Rect Bounds;
        /// <summary>Footprint of the player's kos (FrontageSettings.kosBuildingId), or null when it is not in the data.</summary>
        public readonly Polygon Kos;
        public readonly List<StreetBuilding> Buildings = new List<StreetBuilding>();

        readonly SpatialGrid<StreetBuilding> buildingGrid = new SpatialGrid<StreetBuilding>(20f);
        readonly HashSet<StreetBuilding> hits = new HashSet<StreetBuilding>();
        readonly List<Polygon> water = new List<Polygon>();
        readonly List<(Vector2 a, Vector2 b, float halfWidth)> waterSegs = new List<(Vector2 a, Vector2 b, float halfWidth)>();
        readonly SpatialGrid<int> waterGrid = new SpatialGrid<int>(20f);
        readonly HashSet<int> waterHits = new HashSet<int>();
        readonly List<AreaFeature> areas = new List<AreaFeature>();
        readonly float maxWaterHalfWidth;

        public StreetContext(Rect bounds, IEnumerable<StreetBuilding> buildings, IEnumerable<WaterAreaFeature> waterAreas,
            IEnumerable<WaterLineFeature> waterLines, IEnumerable<AreaFeature> areas, Polygon kos)
        {
            Bounds = bounds;
            Kos = kos;
            foreach (var b in buildings) AddBuilding(b);
            foreach (var w in waterAreas)
                if (w.Shape != null) water.Add(w.Shape);
            foreach (var line in waterLines)
            {
                float hw = line.Width * 0.5f;
                maxWaterHalfWidth = Mathf.Max(maxWaterHalfWidth, hw);
                foreach (var piece in line.Pieces)
                    for (int i = 0; i + 1 < piece.Points.Count; i++)
                    {
                        Vector2 a = piece.Points[i], b = piece.Points[i + 1];
                        waterGrid.Insert(Rect.MinMaxRect(Mathf.Min(a.x, b.x) - hw, Mathf.Min(a.y, b.y) - hw, Mathf.Max(a.x, b.x) + hw, Mathf.Max(a.y, b.y) + hw), waterSegs.Count);
                        waterSegs.Add((a, b, hw));
                    }
            }
            foreach (var a in areas)
                if (a.Shape != null && a.Rule != null) this.areas.Add(a);
        }

        public static StreetContext From(ClassifiedWorld world, WorldGenSettings settings)
        {
            var buildings = new List<StreetBuilding>();
            Polygon kos = null;
            foreach (var b in world.Buildings)
            {
                buildings.Add(new StreetBuilding
                {
                    Id = b.Id, Footprint = b.Spec.Footprint, Centroid = b.Centroid,
                    Excluded = b.Key != null && b.Key.prefab != null
                });
                if (b.Id == settings.frontage.kosBuildingId) kos = b.Spec.Footprint;
            }
            return new StreetContext(world.LocalBounds, buildings, world.WaterAreas, world.WaterLines, world.Areas, kos);
        }

        public void AddBuilding(StreetBuilding b)
        {
            Buildings.Add(b);
            buildingGrid.Insert(b.Footprint.Bounds, b);
        }

        public bool InBounds(Vector2 p) => Bounds.Contains(p);

        /// <summary>The building whose footprint contains p (smallest id when several do), or null.</summary>
        public StreetBuilding BuildingAt(Vector2 p)
        {
            hits.Clear();
            buildingGrid.Query(new Rect(p, Vector2.zero), hits);
            StreetBuilding found = null;
            foreach (var b in hits)
                if (b.Footprint.Contains(p) && (found == null || b.Id < found.Id)) found = b;
            return found;
        }

        public bool InsideBuilding(Vector2 p) => BuildingAt(p) != null;

        public bool InWater(Vector2 p)
        {
            foreach (var w in water)
                if (w.Bounds.Contains(p) && w.Contains(p)) return true;
            if (waterSegs.Count == 0) return false;
            waterHits.Clear();
            waterGrid.Query(new Rect(p, Vector2.zero), waterHits);
            foreach (int i in waterHits)
            {
                var (a, b, hw) = waterSegs[i];
                if (Polygon2D.DistanceToSegment(p, a, b) < hw) return true;
            }
            return false;
        }

        public bool InArea(Vector2 p, AreaCategory category)
        {
            foreach (var a in areas)
                if (a.Rule.category == category && a.Shape.Bounds.Contains(p) && a.Shape.Contains(p)) return true;
            return false;
        }

        public bool InAnyArea(Vector2 p)
        {
            foreach (var a in areas)
                if (a.Shape.Bounds.Contains(p) && a.Shape.Contains(p)) return true;
            return false;
        }

        public float DistanceToKos(Vector2 p) =>
            Kos == null ? float.PositiveInfinity : Kos.Contains(p) ? 0f : Kos.DistanceToBoundary(p);

        /// <summary>First building the ray (dir normalised) meets within maxDistance; distance 0 when origin is inside one; null when none.</summary>
        public StreetBuilding Raycast(Vector2 origin, Vector2 dir, float maxDistance, out float distance)
        {
            var inside = BuildingAt(origin);
            if (inside != null)
            {
                distance = 0f;
                return inside;
            }
            distance = float.PositiveInfinity;
            Vector2 end = origin + dir * maxDistance;
            hits.Clear();
            buildingGrid.Query(Rect.MinMaxRect(Mathf.Min(origin.x, end.x), Mathf.Min(origin.y, end.y), Mathf.Max(origin.x, end.x), Mathf.Max(origin.y, end.y)), hits);
            StreetBuilding best = null;
            foreach (var b in hits)
            {
                float d = Polyline2D.RayToRing(origin, dir, b.Footprint.Outer, maxDistance);
                if (d < distance || (d == distance && best != null && b.Id < best.Id))
                {
                    distance = d;
                    best = b;
                }
            }
            return best;
        }

        public bool OverlapsBuilding(IReadOnlyList<Vector2> ring)
        {
            hits.Clear();
            buildingGrid.Query(Polygon2D.Bounds(ring), hits);
            foreach (var b in hits)
                if (Polygon2D.RingsOverlap(ring, b.Footprint.Outer)) return true;
            return false;
        }

        public bool OverlapsWater(IReadOnlyList<Vector2> ring)
        {
            foreach (var w in water)
                if (Polygon2D.RingsOverlap(ring, w.Outer)) return true;
            if (waterSegs.Count == 0) return false;
            var b = Polygon2D.Bounds(ring);
            waterHits.Clear();
            waterGrid.Query(Rect.MinMaxRect(b.xMin - maxWaterHalfWidth, b.yMin - maxWaterHalfWidth, b.xMax + maxWaterHalfWidth, b.yMax + maxWaterHalfWidth), waterHits);
            foreach (int i in waterHits)
            {
                var (a, c, hw) = waterSegs[i];
                if (Polygon2D.ContainsPoint(ring, a) || Polygon2D.ContainsPoint(ring, c)) return true;
                for (int k = 0; k < ring.Count; k++)
                    if (Polygon2D.SegmentDistance(ring[k], ring[(k + 1) % ring.Count], a, c) < hw) return true;
            }
            return false;
        }

        public bool OverlapsArea(IReadOnlyList<Vector2> ring)
        {
            foreach (var a in areas)
                if (Polygon2D.RingsOverlap(ring, a.Shape.Outer)) return true;
            return false;
        }

        public bool NearKos(IReadOnlyList<Vector2> ring, float radius)
        {
            if (Kos == null) return false;
            if (Polygon2D.RingsOverlap(ring, Kos.Outer)) return true;
            foreach (var p in ring)
                if (Kos.DistanceToBoundary(p) < radius) return true;
            foreach (var p in Kos.Outer)
                if (Polygon2D.DistanceToRing(ring, p) < radius) return true;
            return false;
        }
    }
}
```

- [ ] **Step 4: Implementasi `RoadEdgeBuilder`**

`Assets/WorldGen/Editor/Builders/RoadEdgeBuilder.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>One continuous stretch of shoulder + gutter, or of sidewalk, along one side of a chain.</summary>
    public sealed class EdgeRun
    {
        public RoadChain Chain;
        public int Side;
        public EdgeProfile Profile;
        public float S0, S1;
        public float Length => S1 - S0;
    }

    public sealed class RoadEdgeResult
    {
        public readonly List<EdgeRun> Runs = new List<EdgeRun>();
        public readonly Dictionary<Vector2Int, MeshData> Gutter = new Dictionary<Vector2Int, MeshData>();
        public readonly Dictionary<Vector2Int, MeshData> Sidewalk = new Dictionary<Vector2Int, MeshData>();
        public float GutterMeters, SidewalkMeters;
    }

    /// <summary>Shoulders, gutters and kerbed sidewalks beside the asphalt (spec street-frontage §4–§5).</summary>
    public static class RoadEdgeBuilder
    {
        public const string GutterName = "Gutter", SidewalkName = "Sidewalk";
        public const int GutterConcrete = 0, GutterChannel = 1, GutterSubmeshes = 2;
        public const int SidewalkBody = 0, SidewalkKerb = 1, SidewalkConcrete = 2, SidewalkSubmeshes = 3;
        const int BisectSteps = 3;

        /// <summary>Horizontal top surface between offsets X0 and X1 (metres from the asphalt edge) at height Y.</summary>
        readonly struct Strip
        {
            public readonly float X0, X1, Y;
            public readonly int Sub;

            public Strip(float x0, float x1, float y, int sub)
            {
                X0 = x0;
                X1 = x1;
                Y = y;
                Sub = sub;
            }
        }

        /// <summary>Vertical face along the road at offset X; Outward faces away from the road.</summary>
        readonly struct Face
        {
            public readonly float X, Y0, Y1;
            public readonly int Sub;
            public readonly bool Outward;

            public Face(float x, float y0, float y1, int sub, bool outward)
            {
                X = x;
                Y0 = y0;
                Y1 = y1;
                Sub = sub;
                Outward = outward;
            }
        }

        /// <summary>End cap across a run (at its start and end) between offsets X0 and X1.</summary>
        readonly struct Cap
        {
            public readonly float X0, X1, Y0, Y1;
            public readonly int Sub;

            public Cap(float x0, float x1, float y0, float y1, int sub)
            {
                X0 = x0;
                X1 = x1;
                Y0 = y0;
                Y1 = y1;
                Sub = sub;
            }
        }

        sealed class Section
        {
            public readonly List<Strip> Strips = new List<Strip>();
            public readonly List<Face> Faces = new List<Face>();
            public readonly List<Cap> Caps = new List<Cap>();
            /// <summary>Every strip boundary offset, ascending; faces and caps only use these values.</summary>
            public readonly List<float> Xs = new List<float>();

            public void Seal()
            {
                var set = new SortedSet<float>();
                foreach (var s in Strips)
                {
                    set.Add(s.X0);
                    set.Add(s.X1);
                }
                Xs.AddRange(set);
            }
        }

        public static RoadEdgeResult Build(RoadNetwork net, StreetContext ctx, RoadEdgeSettings s, float chunkSize)
        {
            var result = new RoadEdgeResult();
            var near = new Dictionary<RoadChain, float>();
            foreach (var chain in net.Chains)
            {
                if (chain.BaseProfile == EdgeProfile.None || chain.Points.Count < 2) continue;
                foreach (int side in RoadNetwork.Sides) CollectRuns(chain, side, net, ctx, s, near, result.Runs);
            }
            var gutter = GutterSection(s);
            var sidewalk = SidewalkSection(s);
            foreach (var run in result.Runs)
            {
                if (run.Profile == EdgeProfile.Sidewalk)
                {
                    Emit(run, sidewalk, result.Sidewalk, SidewalkSubmeshes, chunkSize);
                    result.SidewalkMeters += run.Length;
                }
                else
                {
                    Emit(run, gutter, result.Gutter, GutterSubmeshes, chunkSize);
                    result.GutterMeters += run.Length;
                }
            }
            return result;
        }

        /// <summary>The band from the asphalt edge out to <paramref name="width"/> is free at arc length s (spec §5 rules 1–6).</summary>
        public static bool Keep(RoadChain chain, int side, float s, float width, RoadNetwork net, StreetContext ctx, RoadEdgeSettings set,
            Dictionary<RoadChain, float> near)
        {
            float hw = chain.HalfWidth;
            return Free(chain, side, s, hw + set.underlap, net, ctx, set, near) && Free(chain, side, s, hw + width, net, ctx, set, near);
        }

        static bool Free(RoadChain chain, int side, float s, float offset, RoadNetwork net, StreetContext ctx, RoadEdgeSettings set,
            Dictionary<RoadChain, float> near)
        {
            var p = chain.SidePoint(s, side, offset);
            if (!ctx.InBounds(p) || ctx.InsideBuilding(p) || ctx.InWater(p)) return false;
            if (net.DistanceToChain(chain, p, offset, s - offset - 1f, s + offset + 1f) < offset - 0.05f) return false;
            net.Nearby(p, net.MaxReach, chain, near);
            foreach (var kv in near)
            {
                var other = kv.Key;
                float limit = RoadNetwork.Beats(other, chain) ? other.HalfWidth + other.MaxEdgeWidth : other.HalfWidth + set.mouthMargin;
                if (kv.Value < limit) return false;
            }
            return true;
        }

        static void CollectRuns(RoadChain chain, int side, RoadNetwork net, StreetContext ctx, RoadEdgeSettings s,
            Dictionary<RoadChain, float> near, List<EdgeRun> runs)
        {
            var profiles = chain.Profiles(side);
            int n = profiles.Length;
            float step = s.sampleStep;
            var keep = new bool[n];
            for (int i = 0; i < n; i++)
                keep[i] = profiles[i] != EdgeProfile.None && Keep(chain, side, chain.SampleS(i, step), s.Width(profiles[i]), net, ctx, s, near);

            int start = -1;
            for (int i = 0; i <= n; i++)
            {
                if (i < n && start >= 0 && keep[i] && profiles[i] == profiles[start]) continue;
                if (start >= 0)
                {
                    int last = i - 1;
                    var profile = profiles[start];
                    float width = s.Width(profile);
                    float s0 = chain.SampleS(start, step), s1 = chain.SampleS(last, step);
                    if (start > 0)
                    {
                        float before = chain.SampleS(start - 1, step);
                        s0 = keep[start - 1] ? 0.5f * (before + s0) : Polyline2D.Bisect(s0, before, x => Keep(chain, side, x, width, net, ctx, s, near), BisectSteps);
                    }
                    if (last < n - 1)
                    {
                        float after = chain.SampleS(last + 1, step);
                        s1 = keep[last + 1] ? 0.5f * (s1 + after) : Polyline2D.Bisect(s1, after, x => Keep(chain, side, x, width, net, ctx, s, near), BisectSteps);
                    }
                    if (s1 - s0 >= s.minRunLength) runs.Add(new EdgeRun { Chain = chain, Side = side, Profile = profile, S0 = s0, S1 = s1 });
                    start = -1;
                }
                if (i < n && keep[i]) start = i;
            }
        }

        static Section GutterSection(RoadEdgeSettings s)
        {
            float sh = s.shoulderWidth, lip = s.gutterLipWidth, w = s.GutterProfileWidth, b = s.edgeBaseY, top = s.gutterLipY;
            var x = new Section();
            x.Strips.Add(new Strip(-s.underlap, sh, b, GutterConcrete));
            x.Strips.Add(new Strip(sh, sh + lip, top, GutterConcrete));
            x.Strips.Add(new Strip(sh + lip, w - lip, b, GutterChannel));
            x.Strips.Add(new Strip(w - lip, w, top, GutterConcrete));
            x.Faces.Add(new Face(sh, b, top, GutterConcrete, false));
            x.Faces.Add(new Face(sh + lip, b, top, GutterConcrete, true));
            x.Faces.Add(new Face(w - lip, b, top, GutterConcrete, false));
            x.Faces.Add(new Face(w, 0f, top, GutterConcrete, true));
            x.Caps.Add(new Cap(-s.underlap, w, 0f, b, GutterConcrete));
            x.Caps.Add(new Cap(sh, sh + lip, b, top, GutterConcrete));
            x.Caps.Add(new Cap(w - lip, w, b, top, GutterConcrete));
            x.Seal();
            return x;
        }

        static Section SidewalkSection(RoadEdgeSettings s)
        {
            float k = s.kerbWidth, w = s.sidewalkWidth, b = s.edgeBaseY, top = s.sidewalkTopY;
            var x = new Section();
            x.Strips.Add(new Strip(-s.underlap, 0f, b, SidewalkConcrete));
            x.Strips.Add(new Strip(0f, k, top, SidewalkKerb));
            x.Strips.Add(new Strip(k, w, top, SidewalkBody));
            x.Faces.Add(new Face(0f, b, top, SidewalkKerb, false));
            x.Faces.Add(new Face(w, 0f, top, SidewalkConcrete, true));
            x.Caps.Add(new Cap(-s.underlap, 0f, 0f, b, SidewalkConcrete));
            x.Caps.Add(new Cap(0f, w, 0f, top, SidewalkConcrete));
            x.Seal();
            return x;
        }

        static void Emit(EdgeRun run, Section section, Dictionary<Vector2Int, MeshData> meshes, int submeshes, float chunkSize)
        {
            var chain = run.Chain;
            var along = new List<float>();
            var center = Polyline2D.Slice(chain.Points, chain.Cum, run.S0, run.S1, along);
            var lineAlong = new List<float>();
            var lines = new Dictionary<float, List<Vector2>>();
            foreach (float x in section.Xs) lines[x] = Polyline2D.Offset(center, along, run.Side * (chain.HalfWidth + x), lineAlong);
            var outer = lines[section.Xs[section.Xs.Count - 1]];
            for (int j = 0; j + 1 < outer.Count; j++)
            {
                Vector2 dir = outer[j + 1] - outer[j];
                if (dir.sqrMagnitude < 1e-10f) continue;
                var mesh = MeshIn(meshes, WorldMeshPipeline.ChunkOf((outer[j] + outer[j + 1]) * 0.5f, chunkSize), submeshes);
                Vector3 away = Xz.Dir(Polyline2D.LeftNormal(dir.normalized) * run.Side);
                float a0 = lineAlong[j], a1 = lineAlong[j + 1];
                foreach (var st in section.Strips)
                {
                    var l0 = lines[st.X0];
                    var l1 = lines[st.X1];
                    mesh.AddQuad(st.Sub, Xz.At(l0[j], st.Y), Xz.At(l0[j + 1], st.Y), Xz.At(l1[j + 1], st.Y), Xz.At(l1[j], st.Y),
                        new Vector2(a0, st.X0), new Vector2(a1, st.X0), new Vector2(a1, st.X1), new Vector2(a0, st.X1), Vector3.up);
                }
                foreach (var f in section.Faces)
                {
                    var l = lines[f.X];
                    mesh.AddQuad(f.Sub, Xz.At(l[j], f.Y0), Xz.At(l[j + 1], f.Y0), Xz.At(l[j + 1], f.Y1), Xz.At(l[j], f.Y1),
                        new Vector2(a0, f.Y0), new Vector2(a1, f.Y0), new Vector2(a1, f.Y1), new Vector2(a0, f.Y1), f.Outward ? away : -away);
                }
            }
            AddCaps(run, section, lines, meshes, submeshes, chunkSize, true);
            AddCaps(run, section, lines, meshes, submeshes, chunkSize, false);
        }

        static void AddCaps(EdgeRun run, Section section, Dictionary<float, List<Vector2>> lines, Dictionary<Vector2Int, MeshData> meshes,
            int submeshes, float chunkSize, bool atStart)
        {
            run.Chain.PointAt(atStart ? run.S0 : run.S1, out var t);
            Vector3 normal = Xz.Dir(atStart ? -t : t);
            var outer = lines[section.Xs[section.Xs.Count - 1]];
            int j = atStart ? 0 : outer.Count - 1;
            var mesh = MeshIn(meshes, WorldMeshPipeline.ChunkOf(outer[j], chunkSize), submeshes);
            foreach (var c in section.Caps)
            {
                Vector2 p0 = lines[c.X0][j], p1 = lines[c.X1][j];
                mesh.AddQuad(c.Sub, Xz.At(p0, c.Y0), Xz.At(p1, c.Y0), Xz.At(p1, c.Y1), Xz.At(p0, c.Y1),
                    new Vector2(c.X0, c.Y0), new Vector2(c.X1, c.Y0), new Vector2(c.X1, c.Y1), new Vector2(c.X0, c.Y1), normal);
            }
        }

        static MeshData MeshIn(Dictionary<Vector2Int, MeshData> meshes, Vector2Int chunk, int submeshes)
        {
            if (!meshes.TryGetValue(chunk, out var mesh)) meshes[chunk] = mesh = new MeshData(submeshes);
            return mesh;
        }
    }
}
```

Catatan untuk implementer:
- `Keep` mengecek dua titik: tepi dalam (hw + underlap) dan tepi luar (hw + lebar profil).
- Batas run yang terpotong dirapikan dengan bisection 3 langkah. Presisinya 0.0625 m, jadi toleransi tes ±0.07.
- Batas run karena pergantian profil (selokan ↔ trotoar) diletakkan di tengah antara dua sampel, tanpa bisection.
- Jalan S1 berjalan ke utara, jadi sisi +1 (kiri) S1 adalah barat. Tes service menghitung kedua sisi.

- [ ] **Step 5: Jalankan, pastikan lulus**

Procedure R+T, `Group = "RoadEdgeBuilderTests|RoadNetworkTests"`. Expected: `failed=0`.

- [ ] **Step 6: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add Assets/WorldGen/Editor/Builders/StreetContext.cs Assets/WorldGen/Editor/Builders/StreetContext.cs.meta Assets/WorldGen/Editor/Builders/RoadEdgeBuilder.cs Assets/WorldGen/Editor/Builders/RoadEdgeBuilder.cs.meta Assets/WorldGen/Tests/StreetFixture.cs Assets/WorldGen/Tests/StreetFixture.cs.meta Assets/WorldGen/Tests/RoadEdgeBuilderTests.cs Assets/WorldGen/Tests/RoadEdgeBuilderTests.cs.meta
git -C "D:/UNITY/project/Tyco World" commit -m "feat(worldgen): road edge builder for gutters and kerbed sidewalks

Co-Authored-By: <model> <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

---

### Task 5: Marka tengah (`RoadMarkingBuilder`) dan jaringan jalan di pipeline

**Files:**
- Create: `Assets/WorldGen/Editor/Builders/RoadMarkingBuilder.cs`
- Modify: `Assets/WorldGen/Editor/Pipeline/WorldMeshPipeline.cs` (`Build`, `AddRoads`, method baru `CampusShapes`)
- Test: `Assets/WorldGen/Tests/RoadMarkingBuilderTests.cs`, `Assets/WorldGen/Tests/WorldMeshPipelineTests.cs`

**Interfaces:**
- Consumes: `RoadNetwork` (`Chains`, `Nearby`, `MaxHalfWidth`), `RoadChain` (`Members`, `Points`, `Cum`, `Y`, `Highway`, `Width`, `Length`), `Polyline2D.Slice/Offset/PointAt`, `RoadEdgeSettings` (marking fields, `marking` material).
- Produces:
  - `RoadMarkingBuilder.Submesh = 1`
  - `RoadMarkingBuilder.IsMarked(RoadChain, RoadEdgeSettings) : bool`
  - `RoadMarkingBuilder.Build(RoadNetwork, RoadEdgeSettings, IReadOnlyDictionary<long, MeshData> roadMeshes) : int` (jumlah dash)
- Perubahan pipeline:
  - `WorldMeshPipeline.Build` kini membuat `var network = new RoadNetwork(world.Roads, CampusShapes(world), settings.roadEdges)` di awal. Task 6, 7, 13, dan 15 memakai variabel `network` ini.
  - Mesh jalan memakai 2 submesh. `Materials = { road.Rule.material, settings.roadEdges.marking }`.
  - Counter report baru: `"MarkingDashes"`.

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/WorldGen/Tests/RoadMarkingBuilderTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class RoadMarkingBuilderTests
    {
        WorldGenSettings s;

        [SetUp]
        public void SetUp() => s = DefaultSettingsFactory.CreateInMemory();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(s);

        static Vector2 V(float x, float y) => new Vector2(x, y);

        RoadFeature Road(long id, string highway, float width, params Vector2[] pts)
        {
            var rule = s.roadRules.Single(r => r.highway == highway);
            var road = new RoadFeature
            {
                Id = id, Highway = highway, Rule = rule, Width = width, Y = s.roadBaseY + rule.rank * s.roadRankStep,
                Tags = TestOsm.Tags(("highway", highway))
            };
            road.Pieces.Add(new LinePiece(new List<Vector2>(pts), false, false));
            return road;
        }

        int Mark(out Dictionary<long, MeshData> meshes, params RoadFeature[] roads)
        {
            meshes = roads.ToDictionary(r => r.Id, r => new MeshData(2));
            var net = new RoadNetwork(roads, new Polygon[0], s.roadEdges);
            return RoadMarkingBuilder.Build(net, s.roadEdges, meshes);
        }

        static float Area(MeshData m, int sub)
        {
            float area = 0f;
            var idx = m.Indices(sub);
            for (int t = 0; t < idx.Count; t += 3)
                area += Vector3.Cross(m.Vertices[idx[t + 1]] - m.Vertices[idx[t]], m.Vertices[idx[t + 2]] - m.Vertices[idx[t]]).magnitude * 0.5f;
            return area;
        }

        [Test]
        public void Primary100m_HasTheDashPattern()
        {
            var road = Road(1, "primary", 6f, V(0, 0), V(100, 0));
            Assert.AreEqual(12, Mark(out var meshes, road), "dashes start at 2 m, every 8 m, the last ends by 100 m");
            Assert.AreEqual(12 * 6, meshes[1].Indices(RoadMarkingBuilder.Submesh).Count);
            Assert.AreEqual(0, meshes[1].Indices(0).Count, "asphalt submesh untouched");
            foreach (var v in meshes[1].Vertices)
            {
                Assert.AreEqual(road.Y + 0.003f, v.y, 1e-5f);
                Assert.AreEqual(0.06f, Mathf.Abs(v.z), 1e-4f);
            }
            Assert.AreEqual(12 * 3f * 0.12f, Area(meshes[1], RoadMarkingBuilder.Submesh), 1e-3f);
        }

        [Test]
        public void Dashes_StopNearACrossingRoad()
        {
            // Crossing residential at x = 50 (half width 2.5): no dash within 2.5 + 3 m of it.
            Assert.AreEqual(10, Mark(out _, Road(1, "primary", 6f, V(0, 0), V(100, 0)), Road(2, "residential", 5f, V(50, -50), V(50, 50))));
        }

        [Test]
        public void ResidentialAndNarrowTertiary_AreNotMarked()
        {
            Assert.AreEqual(0, Mark(out _, Road(1, "residential", 5f, V(0, 0), V(100, 0))));
            Assert.AreEqual(0, Mark(out _, Road(1, "tertiary", 6f, V(0, 0), V(100, 0))));
        }

        [Test]
        public void WideTertiary_IsMarked()
        {
            Assert.AreEqual(12, Mark(out _, Road(1, "tertiary", 12f, V(0, 0), V(100, 0))));
        }

        [Test]
        public void Dashes_FollowTheChainAcrossWays()
        {
            int count = Mark(out var meshes, Road(1, "primary", 6f, V(0, 0), V(44, 0)), Road(2, "primary", 6f, V(44, 0), V(100, 0)));
            Assert.AreEqual(12, count);
            Assert.Greater(meshes[1].Indices(RoadMarkingBuilder.Submesh).Count, 0);
            Assert.Greater(meshes[2].Indices(RoadMarkingBuilder.Submesh).Count, 0);
            float total = Area(meshes[1], RoadMarkingBuilder.Submesh) + Area(meshes[2], RoadMarkingBuilder.Submesh);
            Assert.AreEqual(12 * 3f * 0.12f, total, 1e-3f, "the dash over the way boundary (42..45) is split, not lost or doubled");
        }
    }
}
```

Tambahkan ke `WorldMeshPipelineTests` (sesudah `Synthetic_BuildingMaterialsFollowVariantRule`):

```csharp
        [Test]
        public void MarkedRoad_KeepsItsAsphaltSurface()
        {
            var asphalt = Materials(1)[0];
            asphalt.name = "Road_Asphalt";
            var marking = Materials(1)[0];
            marking.name = "Road_Marking";
            settings.roadRules.Single(r => r.highway == "primary").material = asphalt;
            settings.roadEdges.marking = marking;
            var osm = new TestOsm();
            osm.Way(950, new[] { new Vector2(-300f, -500f), new Vector2(-200f, -500f) }, false, ("highway", "primary"));
            var report = new GenerationReport();
            var built = WorldMeshPipeline.Build(new FeatureClassifier(settings, report).Classify(osm.Data), settings, report);
            var road = built.Features.Single(f => f.Kind == FeatureKind.Road && f.OsmId == 950);
            Assert.AreEqual(SurfaceType.Asphalt, road.Surface);
            Assert.AreSame(asphalt, road.Materials[0]);
            Assert.AreSame(marking, road.Materials[1]);
            Assert.Greater(road.Mesh.Indices(RoadMarkingBuilder.Submesh).Count, 0);
            Assert.AreEqual(12, report.GetCount("MarkingDashes"));
        }
```

- [ ] **Step 2: Jalankan, pastikan gagal**

Procedure R+T, `Group = "RoadMarkingBuilderTests|WorldMeshPipelineTests"`. Expected: `COMPILE FAILED`.

- [ ] **Step 3: Implementasi `RoadMarkingBuilder`**

`Assets/WorldGen/Editor/Builders/RoadMarkingBuilder.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Dashed centre line on main roads, written into submesh 1 of the road meshes (spec street-frontage §6).</summary>
    public static class RoadMarkingBuilder
    {
        public const int Submesh = 1;

        public static bool IsMarked(RoadChain chain, RoadEdgeSettings s) =>
            RoadNetwork.Contains(s.markingHighways, chain.Highway) ||
            (RoadNetwork.Contains(s.wideMarkingHighways, chain.Highway) && chain.Width >= s.markingMinWidth - 1e-3f);

        /// <summary>Adds the dashes of every marked chain to the meshes of the ways they lie on; returns the dash count.</summary>
        public static int Build(RoadNetwork net, RoadEdgeSettings s, IReadOnlyDictionary<long, MeshData> roadMeshes)
        {
            int dashes = 0;
            var near = new Dictionary<RoadChain, float>();
            var along = new List<float>();
            var lineAlong = new List<float>();
            foreach (var chain in net.Chains)
            {
                if (!IsMarked(chain, s)) continue;
                for (float a = s.dashStart; a + s.dashLength <= chain.Length + 1e-4f; a += s.dashLength + s.dashGap)
                {
                    float b = a + s.dashLength;
                    if (NearJunction(chain, a, b, net, s, near)) continue;
                    foreach (var m in chain.Members)
                    {
                        float s0 = Mathf.Max(a, m.S0), s1 = Mathf.Min(b, m.S1);
                        if (s1 - s0 < 0.05f || !roadMeshes.TryGetValue(m.Road.Id, out var mesh)) continue;
                        AddStrip(mesh, chain, s0, s1, s, along, lineAlong);
                    }
                    dashes++;
                }
            }
            return dashes;
        }

        static bool NearJunction(RoadChain chain, float a, float b, RoadNetwork net, RoadEdgeSettings s, Dictionary<RoadChain, float> near)
        {
            for (float x = a; x <= b + 1e-4f; x += 0.5f)
            {
                var p = chain.PointAt(Mathf.Min(x, b), out _);
                net.Nearby(p, net.MaxHalfWidth + s.junctionDashClearance, chain, near);
                foreach (var kv in near)
                    if (kv.Value < kv.Key.HalfWidth + s.junctionDashClearance) return true;
            }
            return false;
        }

        static void AddStrip(MeshData mesh, RoadChain chain, float s0, float s1, RoadEdgeSettings s, List<float> along, List<float> lineAlong)
        {
            var center = Polyline2D.Slice(chain.Points, chain.Cum, s0, s1, along);
            float half = s.markingWidth * 0.5f, y = chain.Y + s.markingLift;
            var left = Polyline2D.Offset(center, along, half, lineAlong);
            var right = Polyline2D.Offset(center, along, -half, lineAlong);
            for (int j = 0; j + 1 < left.Count; j++)
                mesh.AddQuad(Submesh, Xz.At(right[j], y), Xz.At(right[j + 1], y), Xz.At(left[j + 1], y), Xz.At(left[j], y),
                    new Vector2(0f, lineAlong[j]), new Vector2(0f, lineAlong[j + 1]), new Vector2(s.markingWidth, lineAlong[j + 1]),
                    new Vector2(s.markingWidth, lineAlong[j]), Vector3.up);
        }
    }
}
```

- [ ] **Step 4: Ubah pipeline**

Di `WorldMeshPipeline.Build`, ganti baris `AddRoads(world, built, report);` dengan:

```csharp
            var network = new RoadNetwork(world.Roads, CampusShapes(world), settings.roadEdges);
            AddRoads(world, built, network, settings, report);
```

Ganti seluruh method `AddRoads` dengan:

```csharp
        static void AddRoads(ClassifiedWorld world, BuiltWorld built, RoadNetwork network, WorldGenSettings settings, GenerationReport report)
        {
            var meshes = new Dictionary<long, MeshData>();
            foreach (var road in world.Roads)
            {
                var mesh = new MeshData(2);
                foreach (var piece in road.Pieces)
                    RoadMeshBuilder.AddRibbon(mesh, 0, piece.Points, road.Width, road.Y, piece.ExtendStart, piece.ExtendEnd);
                meshes[road.Id] = mesh;
            }
            report.Count("MarkingDashes", RoadMarkingBuilder.Build(network, settings.roadEdges, meshes));
            foreach (var road in world.Roads)
            {
                var mesh = meshes[road.Id];
                if (mesh.IsEmpty) { report.Skip(SkipReasons.RoadEmptyMesh, road.Id); continue; }
                mesh = mesh.SplitLongTriangles(MaxColliderEdge);
                var anchor = MidPoint(road.Pieces);
                built.Features.Add(new BuiltFeature
                {
                    Kind = FeatureKind.Road, OsmId = road.Id, ElementType = OsmElementType.Way, Name = road.Name,
                    Group = FeatureGroups.Roads, Anchor = anchor, Chunk = ChunkOf(anchor, built.ChunkSize),
                    Mesh = mesh, Materials = new[] { road.Rule.material, settings.roadEdges.marking }, Category = road.Highway, Tags = road.Tags,
                    AddCollider = true
                });
                report.Count("Road");
            }
        }

        static List<Polygon> CampusShapes(ClassifiedWorld world) =>
            world.Areas.Where(a => a.Rule != null && a.Rule.category == AreaCategory.Campus && a.Shape != null).Select(a => a.Shape).ToList();
```

Catatan:
- Mesh jalan yang tanpa marka punya submesh 1 kosong. `MeshData.WriteTo` membuang submesh kosong, jadi material marka tidak dipakai di jalan itu.
- `ContentHash` sekarang mencampur 2 submesh, jadi hash semua chunk berubah sekali. Itu disengaja.

- [ ] **Step 5: Jalankan, pastikan lulus**

Procedure R+T, `Group = "RoadMarkingBuilderTests|WorldMeshPipelineTests|SurfaceRulesTests"`. Expected: `failed=0`. Tes lama `Sukabirus_*` dan `Synthetic_*` harus tetap lulus.

- [ ] **Step 6: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add Assets/WorldGen/Editor/Builders/RoadMarkingBuilder.cs Assets/WorldGen/Editor/Builders/RoadMarkingBuilder.cs.meta Assets/WorldGen/Editor/Pipeline/WorldMeshPipeline.cs Assets/WorldGen/Tests/RoadMarkingBuilderTests.cs Assets/WorldGen/Tests/RoadMarkingBuilderTests.cs.meta Assets/WorldGen/Tests/WorldMeshPipelineTests.cs
git -C "D:/UNITY/project/Tyco World" commit -m "feat(worldgen): dashed centre markings on main roads

Co-Authored-By: <model> <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

---

### Task 6: Plumbing objek chunk + tepi jalan di dunia generate

**Files:**
- Modify: `Assets/WorldGen/Runtime/FeatureKind.cs`
- Modify: `Assets/WorldGen/Editor/Pipeline/WorldMeshPipeline.cs` (`FeatureGroups`, `BuiltFeature`, `Build`, method baru `ChunkFeatureId`, `AddChunkFeatures`, `AddEdges`, `ComputeChunkHashes`)
- Modify: `Assets/WorldGen/Editor/Pipeline/ChunkAssetWriter.cs`
- Modify: `Assets/WorldGen/Editor/Pipeline/SceneObjectFactory.cs` (blok collider dan `OsmFeature` di `Populate`)
- Modify: `Assets/WorldGen/Editor/Classify/SurfaceRules.cs`
- Modify: `Assets/WorldGen/Runtime/Culling/WorldChunkIndex.cs`
- Test: `Assets/WorldGen/Tests/WorldMeshPipelineTests.cs`, `WorldChunkIndexTests.cs`, `SurfaceRulesTests.cs`

**Interfaces:**
- Consumes: `RoadEdgeBuilder.Build`, `StreetContext.From`, `network` (Task 5).
- Produces:
  - `FeatureKind.RoadEdge`, `FeatureKind.Frontage`
  - `FeatureGroups.RoadEdges = "RoadEdges"`, `FeatureGroups.Frontage = "Frontage"`
  - `BuiltFeature.ColliderMesh` (MeshData, boleh null), `BuiltFeature.HasOsmIdentity` (default true)
  - `MeshAsset.Collider` (Mesh, boleh null)
  - `WorldMeshPipeline.ChunkFeatureId(string group, string name, Vector2Int chunk) : long` (negatif)
  - `WorldMeshPipeline.AddChunkFeatures(BuiltWorld, IReadOnlyDictionary<Vector2Int, MeshData> meshes, IReadOnlyDictionary<Vector2Int, MeshData> colliders, FeatureKind, string group, string name, Material[] materials, GenerationReport)` (private static, dipakai Task 13 dan 15)
  - Di `Build`, variabel `street` (`StreetContext`)
  - Counter report: `"Gutter"`, `"Sidewalk"` (jumlah objek chunk), `"EdgeMeters.Gutter"`, `"EdgeMeters.Sidewalk"`

- [ ] **Step 1: Tulis tes yang gagal**

Di `SurfaceRulesTests`, tambahkan tiga `[TestCase]` di atas `ForMaterialName_MapsDrawnMaterial`:

```csharp
        [TestCase("Edge_Sidewalk", SurfaceType.Paving)]
        [TestCase("Edge_Concrete", SurfaceType.Concrete)]
        [TestCase("Edge_Gutter", SurfaceType.Concrete)]
```

Lalu tambahkan tes berikut:

```csharp
        [Test]
        public void For_RoadEdgeWithoutMaterial_UsesItsName()
        {
            Assert.AreEqual(SurfaceType.Paving, SurfaceRules.For(new BuiltFeature { Kind = FeatureKind.RoadEdge, Name = RoadEdgeBuilder.SidewalkName }));
            Assert.AreEqual(SurfaceType.Concrete, SurfaceRules.For(new BuiltFeature { Kind = FeatureKind.RoadEdge, Name = RoadEdgeBuilder.GutterName }));
            Assert.AreEqual(SurfaceType.Default, SurfaceRules.For(new BuiltFeature { Kind = FeatureKind.Frontage, Name = "Walls" }));
        }
```

Di `WorldChunkIndexTests.Build_IndexesEveryContainerTheGeneratorCreates`, ganti dua baris pertama:

```csharp
            var structures = new[] { FeatureGroups.Roads, FeatureGroups.Buildings, FeatureGroups.Areas, FeatureGroups.Water, FeatureGroups.Frontage };
            var props = new[] { WorldMeshPipeline.TreesGroup, WorldMeshPipeline.LampsGroup, FeatureGroups.RoadEdges };
```

Di `WorldMeshPipelineTests`, tambahkan:

```csharp
        BuiltWorld BuildStreet(GenerationReport report, System.Action<TestOsm> extra = null)
        {
            var osm = StreetFixture.Osm();
            extra?.Invoke(osm);
            return WorldMeshPipeline.Build(new FeatureClassifier(settings, report).Classify(osm.Data), settings, report);
        }

        [Test]
        public void Street_EdgesAreChunkObjectsWithoutOsmIdentity()
        {
            var report = new GenerationReport();
            var edges = BuildStreet(report).Features.Where(f => f.Kind == FeatureKind.RoadEdge).ToList();
            Assert.Greater(edges.Count, 0);
            foreach (var f in edges)
            {
                Assert.AreEqual(FeatureGroups.RoadEdges, f.Group);
                Assert.IsFalse(f.HasOsmIdentity);
                Assert.IsTrue(f.AddCollider);
                Assert.IsNull(f.ColliderMesh);
                Assert.Less(f.OsmId, 0);
                Assert.AreEqual(f.Name == RoadEdgeBuilder.SidewalkName ? SurfaceType.Paving : SurfaceType.Concrete, f.Surface);
                Assert.AreEqual(f.Chunk, WorldMeshPipeline.ChunkOf(f.Anchor, 100f));
            }
            Assert.IsTrue(edges.Any(f => f.Name == RoadEdgeBuilder.SidewalkName));
            Assert.IsTrue(edges.Any(f => f.Name == RoadEdgeBuilder.GutterName));
            Assert.AreEqual(edges.Count, edges.Select(f => f.OsmId).Distinct().Count());
            Assert.Greater(report.GetCount("EdgeMeters.Gutter"), 0);
            Assert.Greater(report.GetCount("EdgeMeters.Sidewalk"), 0);
        }

        [Test]
        public void Street_IsDeterministic()
        {
            var first = new GenerationReport();
            var second = new GenerationReport();
            BuildStreet(first);
            BuildStreet(second);
            CollectionAssert.AreEqual(first.ChunkHashes, second.ChunkHashes);
        }

        [Test]
        public void ChunkFeatureId_IsNegativeAndDistinct()
        {
            long a = WorldMeshPipeline.ChunkFeatureId(FeatureGroups.RoadEdges, RoadEdgeBuilder.GutterName, new Vector2Int(1, 2));
            long b = WorldMeshPipeline.ChunkFeatureId(FeatureGroups.RoadEdges, RoadEdgeBuilder.SidewalkName, new Vector2Int(1, 2));
            long c = WorldMeshPipeline.ChunkFeatureId(FeatureGroups.RoadEdges, RoadEdgeBuilder.GutterName, new Vector2Int(2, 1));
            Assert.Less(a, 0);
            Assert.AreNotEqual(a, b);
            Assert.AreNotEqual(a, c);
            Assert.AreEqual(a, WorldMeshPipeline.ChunkFeatureId(FeatureGroups.RoadEdges, RoadEdgeBuilder.GutterName, new Vector2Int(1, 2)));
        }
```

Di `LongRoadAndLargeArea_AreSplitBelowColliderEdgeLimit`, tambahkan sebelum `}` penutup method:

```csharp
            var edges = built.Features.Where(f => f.Kind == FeatureKind.RoadEdge).ToList();
            Assert.Greater(edges.Count, 0);
            foreach (var f in edges)
                Assert.LessOrEqual(MeshDataSplitTests.MaxEdge(f.Mesh), WorldMeshPipeline.MaxColliderEdge + 1e-3f, f.Name + " " + f.Chunk);
```

- [ ] **Step 2: Jalankan, pastikan gagal**

Procedure R+T, `Group = "WorldMeshPipelineTests|WorldChunkIndexTests|SurfaceRulesTests"`. Expected: `COMPILE FAILED`.

- [ ] **Step 3: Implementasi**

`Assets/WorldGen/Runtime/FeatureKind.cs`, baris enum `FeatureKind` (nilai lama tidak bergeser; `OsmFeature` menyimpan int):

```csharp
    public enum FeatureKind { Road, Building, Area, WaterLine, WaterArea, RoadEdge, Frontage }
```

`WorldMeshPipeline.cs`:

1. Di `FeatureGroups`, tambahkan:

```csharp
        public const string RoadEdges = "RoadEdges";
        public const string Frontage = "Frontage";
```

2. Di `BuiltFeature`, setelah `public IReadOnlyDictionary<string, string> Tags = OsmTags.Empty;`:

```csharp
        /// <summary>Separate collision mesh (solid boxes for walls); null = collide with Mesh.</summary>
        public MeshData ColliderMesh;
        /// <summary>False for generated chunk objects with no OSM element behind them (edges, walls): they get no OsmFeature.</summary>
        public bool HasOsmIdentity = true;
```

3. Di `Build`, sesudah `AddWater(world, settings, built, report);`:

```csharp
            var street = StreetContext.From(world, settings);
            AddEdges(built, network, street, settings, report);
```

4. Method baru (letakkan sebelum `static int Length(Material[] list)`):

```csharp
        /// <summary>Stable negative id of a per-chunk generated object (never collides with OSM ids, which are positive).</summary>
        public static long ChunkFeatureId(string group, string name, Vector2Int chunk) =>
            -(long)(SplitMix64.Hash(((long)chunk.x << 32) ^ (uint)chunk.y, group + "/" + name) & 0x3FFF_FFFF_FFFF_FFFFUL) - 1;

        static void AddChunkFeatures(BuiltWorld built, IReadOnlyDictionary<Vector2Int, MeshData> meshes, IReadOnlyDictionary<Vector2Int, MeshData> colliders,
            FeatureKind kind, string group, string name, Material[] materials, GenerationReport report)
        {
            var chunks = new List<Vector2Int>(meshes.Keys);
            chunks.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            foreach (var chunk in chunks)
            {
                var mesh = meshes[chunk];
                if (mesh.IsEmpty) continue;
                MeshData collider = null;
                if (colliders != null && colliders.TryGetValue(chunk, out var c) && !c.IsEmpty) collider = c.SplitLongTriangles(MaxColliderEdge);
                built.Features.Add(new BuiltFeature
                {
                    Kind = kind, OsmId = ChunkFeatureId(group, name, chunk), ElementType = OsmElementType.Way, Name = name,
                    Group = group, Chunk = chunk,
                    Anchor = new Vector2((chunk.x + 0.5f) * built.ChunkSize, (chunk.y + 0.5f) * built.ChunkSize),
                    Mesh = mesh.SplitLongTriangles(MaxColliderEdge), ColliderMesh = collider,
                    Materials = materials, AddCollider = true, HasOsmIdentity = false, Category = name
                });
                report.Count(name);
            }
        }

        static void AddEdges(BuiltWorld built, RoadNetwork network, StreetContext street, WorldGenSettings settings, GenerationReport report)
        {
            var e = settings.roadEdges;
            var edges = RoadEdgeBuilder.Build(network, street, e, built.ChunkSize);
            AddChunkFeatures(built, edges.Gutter, null, FeatureKind.RoadEdge, FeatureGroups.RoadEdges, RoadEdgeBuilder.GutterName,
                new[] { e.concrete, e.gutter }, report);
            AddChunkFeatures(built, edges.Sidewalk, null, FeatureKind.RoadEdge, FeatureGroups.RoadEdges, RoadEdgeBuilder.SidewalkName,
                new[] { e.sidewalk, e.kerb, e.concrete }, report);
            report.Count("EdgeMeters.Gutter", Mathf.RoundToInt(edges.GutterMeters));
            report.Count("EdgeMeters.Sidewalk", Mathf.RoundToInt(edges.SidewalkMeters));
        }
```

Urutan material mengikuti konstanta submesh `RoadEdgeBuilder`:
- Gutter: 0 = concrete, 1 = channel.
- Sidewalk: 0 = body, 1 = kerb, 2 = concrete.

5. Di `ComputeChunkHashes`, ganti loop fitur dengan:

```csharp
            foreach (var f in built.Features.OrderBy(f => f.Kind).ThenBy(f => f.OsmId))
            {
                Mix(f.Chunk, f.Mesh != null ? f.Mesh.ContentHash() : unchecked((ulong)f.OsmId));
                if (f.ColliderMesh != null) Mix(f.Chunk, f.ColliderMesh.ContentHash());
            }
```

`ChunkAssetWriter.cs`:

1. Ganti struct `MeshAsset`:

```csharp
    public readonly struct MeshAsset
    {
        public readonly Mesh Mesh;
        public readonly int[] UsedSubmeshes;
        /// <summary>Separate collision mesh, or null to collide with Mesh.</summary>
        public readonly Mesh Collider;

        public MeshAsset(Mesh mesh, int[] usedSubmeshes, Mesh collider = null)
        {
            Mesh = mesh;
            UsedSubmeshes = usedSubmeshes;
            Collider = collider;
        }
    }
```

2. Di loop `foreach (var feature in group...)`, ganti `result[feature] = new MeshAsset(mesh, used);` dengan:

```csharp
                        Mesh collider = null;
                        if (feature.ColliderMesh != null && !feature.ColliderMesh.IsEmpty)
                        {
                            collider = feature.ColliderMesh.ToMesh(mesh.name + "_Collider", out _);
                            AssetDatabase.AddObjectToAsset(collider, main);
                        }
                        result[feature] = new MeshAsset(mesh, used, collider);
```

`SceneObjectFactory.cs`, di `Populate`:

1. Ganti `if (feature.AddCollider) go.AddComponent<MeshCollider>().sharedMesh = asset.Mesh;` dengan:

```csharp
                    if (feature.AddCollider) go.AddComponent<MeshCollider>().sharedMesh = asset.Collider != null ? asset.Collider : asset.Mesh;
```

2. Ganti tiga baris `var osm = ...; if (osm == null) ...; osm.Initialize(...);` dengan:

```csharp
                if (feature.HasOsmIdentity)
                {
                    var osm = go.GetComponent<OsmFeature>();
                    if (osm == null) osm = go.AddComponent<OsmFeature>();
                    osm.Initialize(feature.OsmId, feature.ElementType, feature.Kind, feature.Name ?? "", feature.Tags);
                }
```

`SurfaceRules.cs`:

1. Di `ForMaterialName`, tambahkan case sebelum `default`:

```csharp
                case "Edge_Sidewalk": return SurfaceType.Paving;
                case "Edge_Concrete":
                case "Edge_Gutter": return SurfaceType.Concrete;
```

2. Di `For`, tambahkan case sebelum `default`:

```csharp
                case FeatureKind.RoadEdge: return feature.Name == RoadEdgeBuilder.SidewalkName ? SurfaceType.Paving : SurfaceType.Concrete;
```

`WorldChunkIndex.cs`, di `Build` setelah baris `Props/Lamps`:

```csharp
            Add(root.Find("RoadEdges"), CullCategory.Prop, groups);
            Add(root.Find("Frontage"), CullCategory.Structure, groups);
```

Lalu perbarui komentar summary kelas menjadi `Roads|Buildings|Areas|Water|Frontage|RoadEdges|Props/Trees|Props/Lamps → Chunk_x_z`.

- [ ] **Step 4: Jalankan, pastikan lulus**

Procedure R+T, `Group = "."` (semua). Expected: `failed=0`.

- [ ] **Step 5: Generate dan periksa hierarki**

Jalankan Procedure G. Expected: Summary memuat `EdgeMeters.Gutter=…`, `EdgeMeters.Sidewalk=…`, `Gutter=…`, `Sidewalk=…`, dan tidak ada error.

Lalu jalankan snippet berikut:

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var scene = SceneManager.GetSceneByPath("Assets/WorldGen/Generated/World_Generated.unity");
        GameObject root = null;
        foreach (var go in scene.GetRootGameObjects()) if (go.name == "World_Generated") root = go;
        var edges = root.transform.Find("RoadEdges");
        int objects = 0, colliders = 0, surfaces = 0, osm = 0;
        foreach (var mf in edges.GetComponentsInChildren<MeshFilter>())
        {
            objects++;
            if (mf.GetComponent<MeshCollider>() != null) colliders++;
            if (mf.GetComponent<TycoWorld.WorldGen.GroundSurface>() != null) surfaces++;
            if (mf.GetComponent<TycoWorld.WorldGen.OsmFeature>() != null) osm++;
        }
        result.Log("edges objects=" + objects + " colliders=" + colliders + " surfaces=" + surfaces + " osm=" + osm);
    }
}
```

Expected: `objects == colliders == surfaces > 0`, `osm=0`.

- [ ] **Step 6: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add Assets/WorldGen/Runtime/FeatureKind.cs Assets/WorldGen/Runtime/Culling/WorldChunkIndex.cs Assets/WorldGen/Editor/Pipeline/WorldMeshPipeline.cs Assets/WorldGen/Editor/Pipeline/ChunkAssetWriter.cs Assets/WorldGen/Editor/Pipeline/SceneObjectFactory.cs Assets/WorldGen/Editor/Classify/SurfaceRules.cs Assets/WorldGen/Tests/WorldMeshPipelineTests.cs Assets/WorldGen/Tests/WorldChunkIndexTests.cs Assets/WorldGen/Tests/SurfaceRulesTests.cs
git -C "D:/UNITY/project/Tyco World" commit -m "feat(worldgen): road edges as per-chunk objects with colliders and surfaces

Co-Authored-By: <model> <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

---

### Task 7: Lampu dan pohon mengikuti tepi jalan, tembok, dan infill

**Files:**
- Modify: `Assets/WorldGen/Editor/Builders/PropPlacer.cs` (ganti seluruh isi file dengan versi di bawah)
- Modify: `Assets/WorldGen/Editor/Pipeline/WorldMeshPipeline.cs` (baris `var placer = new PropPlacer(...)`)
- Test: `Assets/WorldGen/Tests/StreetPropTests.cs`

**Interfaces:**
- Consumes: `IEdgeLookup` (`RoadNetwork`), `RoadEdgeSettings.sidewalkLampOffset/gutterLampOffset`, `StreetFixture`.
- Produces:
  - `readonly struct WallSegment { Vector2 A, B; WallSegment(Vector2 a, Vector2 b) }`
  - `PropPlacer(IReadOnlyList<BuildingFeature>, IReadOnlyList<RoadFeature>, IEdgeLookup edges = null, RoadEdgeSettings edgeSettings = null)`
  - `void AddBuildings(IEnumerable<BuildingFeature>)`
  - `void AddWalls(IEnumerable<WallSegment>)`
  - `bool IsNearWall(Vector2 p, float clearance)`
  - Konstanta: `LampOwnRoadClearance = 0.05f`, `TreeEdgeClearance = 0.5f`, `LampWallClearance = 0.35f`, `TreeWallClearance = 0.8f`
- Tanpa `edges`/`edgeSettings`, perilakunya sama seperti sebelumnya. Satu-satunya beda: lampu kini boleh berdiri 0.05 m (bukan 0.2 m) dari aspal **jalannya sendiri**. Tes lama tetap lulus.

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/WorldGen/Tests/StreetPropTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class StreetPropTests
    {
        WorldGenSettings settings;

        [SetUp]
        public void SetUp() => settings = DefaultSettingsFactory.CreateInMemory();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(settings);

        static Polygon Square(float x, float z, float w, float d) => new Polygon(new List<Vector2>
            { new Vector2(x, z), new Vector2(x + w, z), new Vector2(x + w, z + d), new Vector2(x, z + d) });

        static AreaFeature Grass(Polygon shape) =>
            new AreaFeature { Id = 77, Shape = shape, Rule = new AreaRule { category = AreaCategory.Grass, treeSpacing = 3f } };

        PropPlacer Placer(StreetFixture fx) => new PropPlacer(fx.World.Buildings, fx.World.Roads, fx.Network, settings.roadEdges);

        [Test]
        public void Lamps_StandOnTheShoulderOfGutterRoads()
        {
            var fx = new StreetFixture(settings);
            var lamps = Placer(fx).PlaceLamps(fx.World.Roads, 8f, 0.6f, new GenerationReport());
            var onR1 = lamps.Where(l => Mathf.Abs(l.Position.y) < 4f && l.Position.x > 5f && l.Position.x < 195f).ToList();
            Assert.GreaterOrEqual(onR1.Count, 3);
            foreach (var l in onR1) Assert.AreEqual(2.5f + 0.15f, Mathf.Abs(l.Position.y), 1e-3f);
        }

        [Test]
        public void Lamps_OnTheCampusSide_StandOnTheSidewalk()
        {
            var fx = new StreetFixture(settings);
            var lamps = Placer(fx).PlaceLamps(fx.World.Roads, 8f, 0.6f, new GenerationReport());
            var onT1 = lamps.Where(l => Mathf.Abs(l.Position.y - 60f) < 5f && l.Position.x > 5f && l.Position.x < 195f).ToList();
            Assert.IsTrue(onT1.Any(l => l.Position.y > 60f));
            Assert.IsTrue(onT1.Any(l => l.Position.y < 60f));
            foreach (var l in onT1)
                Assert.AreEqual(l.Position.y > 60f ? 60f + 3f + 0.45f : 60f - 3f - 0.15f, l.Position.y, 1e-3f);
        }

        [Test]
        public void ScatterTrees_KeepOffTheEdges()
        {
            var fx = new StreetFixture(settings);
            // The grass reaches the R1 centreline, so the edge clearance (hw 2.5 + gutter 0.8 + 0.5) is what keeps trees off.
            var trees = Placer(fx).PlaceTrees(new TreeFeature[0], new[] { Grass(Square(5f, -30f, 190f, 30f)) }, 4000, 1, new GenerationReport());
            Assert.Greater(trees.Count, 50);
            Assert.IsTrue(trees.Any(t => Mathf.Abs(t.Position.y) < 4.5f), "some trees stand right behind the clearance");
            foreach (var t in trees) Assert.GreaterOrEqual(Mathf.Abs(t.Position.y), 2.5f + 0.8f + 0.5f - 1e-3f);
        }

        [Test]
        public void LampsAndTrees_KeepOffWalls()
        {
            var fx = new StreetFixture(settings);
            var placer = Placer(fx);
            var wall = new WallSegment(new Vector2(0f, -4.5f), new Vector2(200f, -4.5f));
            placer.AddWalls(new[] { wall });
            Assert.IsTrue(placer.IsNearWall(new Vector2(50f, -4.3f), 0.35f));
            Assert.IsFalse(placer.IsNearWall(new Vector2(50f, -5f), 0.35f));
            var trees = placer.PlaceTrees(new TreeFeature[0], new[] { Grass(Square(5f, -30f, 190f, 30f)) }, 4000, 1, new GenerationReport());
            Assert.Greater(trees.Count, 50);
            foreach (var t in trees) Assert.GreaterOrEqual(Polygon2D.DistanceToSegment(t.Position, wall.A, wall.B), 0.8f - 1e-3f);
            var lamps = placer.PlaceLamps(fx.World.Roads, 8f, 0.6f, new GenerationReport());
            foreach (var l in lamps) Assert.GreaterOrEqual(Polygon2D.DistanceToSegment(l.Position, wall.A, wall.B), 0.35f - 1e-3f);
        }

        [Test]
        public void AddedBuildings_BlockScatterTrees()
        {
            var fx = new StreetFixture(settings);
            var placer = Placer(fx);
            var house = Square(100f, -30f, 20f, 20f);
            placer.AddBuildings(new[] { new BuildingFeature { Id = -5, Spec = new BuildingSpec { Id = -5, Footprint = house } } });
            var trees = placer.PlaceTrees(new TreeFeature[0], new[] { Grass(Square(5f, -40f, 190f, 35f)) }, 4000, 1, new GenerationReport());
            Assert.IsFalse(trees.Any(t => house.Contains(t.Position)));
        }
    }
}
```

- [ ] **Step 2: Jalankan, pastikan gagal**

Procedure R+T, `Group = "StreetPropTests"`. Expected: `COMPILE FAILED`.

- [ ] **Step 3: Implementasi — ganti isi `PropPlacer.cs`**

```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    public struct PropPlacement
    {
        public Vector2 Position;
        public float YawDegrees;
        public float Scale;
        public int Variant;
    }

    /// <summary>A straight piece of wall or closed gate that lamps and trees keep clear of (spec street-frontage §9).</summary>
    public readonly struct WallSegment
    {
        public readonly Vector2 A, B;

        public WallSegment(Vector2 a, Vector2 b)
        {
            A = a;
            B = b;
        }
    }

    /// <summary>Deterministic placement of trees (OSM nodes + Poisson scatter) and street lamps.</summary>
    public sealed class PropPlacer
    {
        public const float MinTreeGap = 2f;
        public const float TreeBuildingBuffer = 1f;
        public const float TreeRoadClearance = 1f;
        public const float LampBuildingBuffer = 0.3f;
        public const float LampRoadClearance = 0.2f;
        public const float LampOwnRoadClearance = 0.05f;
        public const float TreeEdgeClearance = 0.5f;
        public const float LampWallClearance = 0.35f;
        public const float TreeWallClearance = 0.8f;

        readonly struct RoadSegment
        {
            public readonly Vector2 A, B;
            public readonly float HalfWidth, EdgeReach;
            public readonly long RoadId;

            public RoadSegment(Vector2 a, Vector2 b, float halfWidth, float edgeReach, long roadId)
            {
                A = a;
                B = b;
                HalfWidth = halfWidth;
                EdgeReach = edgeReach;
                RoadId = roadId;
            }
        }

        readonly SpatialGrid<BuildingFeature> buildingGrid = new SpatialGrid<BuildingFeature>(20f);
        readonly SpatialGrid<int> roadGrid = new SpatialGrid<int>(20f);
        readonly List<RoadSegment> segments = new List<RoadSegment>();
        readonly SpatialGrid<int> wallGrid = new SpatialGrid<int>(20f);
        readonly List<WallSegment> walls = new List<WallSegment>();
        readonly IEdgeLookup edges;
        readonly RoadEdgeSettings edgeSettings;
        readonly float maxEdgeReach;

        public PropPlacer(IReadOnlyList<BuildingFeature> buildings, IReadOnlyList<RoadFeature> roads, IEdgeLookup edges = null,
            RoadEdgeSettings edgeSettings = null)
        {
            this.edges = edges != null && edgeSettings != null ? edges : null;
            this.edgeSettings = edgeSettings;
            foreach (var b in buildings) buildingGrid.Insert(b.Spec.Footprint.Bounds, b);
            foreach (var road in roads)
            {
                float hw = road.Width * 0.5f;
                float reach = this.edges != null ? this.edges.MaxEdgeWidth(road.Id) : 0f;
                maxEdgeReach = Mathf.Max(maxEdgeReach, reach);
                foreach (var piece in road.Pieces)
                    for (int i = 0; i + 1 < piece.Points.Count; i++)
                    {
                        Vector2 a = piece.Points[i], b = piece.Points[i + 1];
                        var rect = Rect.MinMaxRect(Mathf.Min(a.x, b.x) - hw, Mathf.Min(a.y, b.y) - hw, Mathf.Max(a.x, b.x) + hw, Mathf.Max(a.y, b.y) + hw);
                        roadGrid.Insert(rect, segments.Count);
                        segments.Add(new RoadSegment(a, b, hw, reach, road.Id));
                    }
            }
        }

        static Rect Around(Vector2 p, float radius) => Rect.MinMaxRect(p.x - radius, p.y - radius, p.x + radius, p.y + radius);

        /// <summary>Extra footprints (infill houses) that trees and lamps must avoid like OSM buildings.</summary>
        public void AddBuildings(IEnumerable<BuildingFeature> extra)
        {
            foreach (var b in extra) buildingGrid.Insert(b.Spec.Footprint.Bounds, b);
        }

        public void AddWalls(IEnumerable<WallSegment> segmentsToAvoid)
        {
            foreach (var w in segmentsToAvoid)
            {
                wallGrid.Insert(Rect.MinMaxRect(Mathf.Min(w.A.x, w.B.x), Mathf.Min(w.A.y, w.B.y), Mathf.Max(w.A.x, w.B.x), Mathf.Max(w.A.y, w.B.y)), walls.Count);
                walls.Add(w);
            }
        }

        public bool IsNearWall(Vector2 p, float clearance)
        {
            if (walls.Count == 0) return false;
            var hits = new HashSet<int>();
            wallGrid.Query(Around(p, clearance), hits);
            foreach (int i in hits)
                if (Polygon2D.DistanceToSegment(p, walls[i].A, walls[i].B) < clearance) return true;
            return false;
        }

        public bool IsInsideBuilding(Vector2 p, float buffer)
        {
            var hits = new HashSet<BuildingFeature>();
            buildingGrid.Query(Around(p, buffer), hits);
            foreach (var b in hits)
            {
                var footprint = b.Spec.Footprint;
                if (footprint.Contains(p)) return true;
                if (buffer > 0f && footprint.DistanceToBoundary(p) < buffer) return true;
            }
            return false;
        }

        public bool IsOnRoad(Vector2 p, float clearance)
        {
            var hits = new HashSet<int>();
            roadGrid.Query(Around(p, clearance), hits);
            foreach (int i in hits)
            {
                var s = segments[i];
                if (Polygon2D.DistanceToSegment(p, s.A, s.B) < s.HalfWidth + clearance) return true;
            }
            return false;
        }

        /// <summary>Scatter trees keep hw + max(1, edge width + 0.5) from every road (spec street-frontage §9).</summary>
        bool TooCloseToRoadForTree(Vector2 p)
        {
            var hits = new HashSet<int>();
            roadGrid.Query(Around(p, Mathf.Max(TreeRoadClearance, maxEdgeReach + TreeEdgeClearance)), hits);
            foreach (int i in hits)
            {
                var s = segments[i];
                float clearance = Mathf.Max(TreeRoadClearance, s.EdgeReach + TreeEdgeClearance);
                if (Polygon2D.DistanceToSegment(p, s.A, s.B) < s.HalfWidth + clearance) return true;
            }
            return false;
        }

        /// <summary>A lamp may stand right beside its own asphalt (on the shoulder) but keeps 0.2 m off other roads.</summary>
        bool BlocksLamp(Vector2 p, long ownRoad)
        {
            var hits = new HashSet<int>();
            roadGrid.Query(Around(p, LampRoadClearance), hits);
            foreach (int i in hits)
            {
                var s = segments[i];
                float clearance = s.RoadId == ownRoad ? LampOwnRoadClearance : LampRoadClearance;
                if (Polygon2D.DistanceToSegment(p, s.A, s.B) < s.HalfWidth + clearance) return true;
            }
            return false;
        }

        public List<PropPlacement> PlaceTrees(IReadOnlyList<TreeFeature> osmTrees, IReadOnlyList<AreaFeature> areas,
            int maxTrees, int variantCount, GenerationReport report, IReadOnlyList<Polygon> noTreeZones = null)
        {
            var accepted = new List<Vector2>();
            var gapGrid = new SpatialGrid<int>(MinTreeGap * 2f);

            bool TryReserve(Vector2 p)
            {
                var near = new HashSet<int>();
                gapGrid.Query(Around(p, MinTreeGap), near);
                foreach (int i in near)
                    if ((accepted[i] - p).sqrMagnitude < MinTreeGap * MinTreeGap) return false;
                gapGrid.Insert(new Rect(p, Vector2.zero), accepted.Count);
                accepted.Add(p);
                return true;
            }

            var fromOsm = new List<PropPlacement>();
            foreach (var tree in osmTrees.OrderBy(t => t.Id))
            {
                if (IsInsideBuilding(tree.Position, 0f) || IsNearWall(tree.Position, TreeWallClearance) || !TryReserve(tree.Position)) continue;
                fromOsm.Add(Make(tree.Position, SplitMix64.For(tree.Id, "tree"), variantCount));
            }

            var blocked = new List<(Rect bounds, Polygon shape)>();
            foreach (var area in areas)
                if (area.Shape != null && (area.Rule == null || area.Rule.treeSpacing <= 0f)) blocked.Add((area.Shape.Bounds, area.Shape));
            if (noTreeZones != null)
                foreach (var zone in noTreeZones)
                    if (zone != null) blocked.Add((zone.Bounds, zone));

            bool InNoTreeZone(Vector2 p)
            {
                foreach (var (zoneBounds, shape) in blocked)
                    if (zoneBounds.Contains(p) && shape.Contains(p)) return true;
                return false;
            }

            var scatter = new List<(ulong key, PropPlacement placement)>();
            foreach (var area in areas.OrderBy(a => a.Id))
            {
                float spacing = area.Rule != null ? area.Rule.treeSpacing : 0f;
                if (spacing <= 0f) continue;
                var rng = SplitMix64.For(area.Id, "trees");
                foreach (var p in PoissonDisk.Sample(area.Shape.Bounds, spacing, rng))
                {
                    if (!area.Shape.Contains(p) || InNoTreeZone(p) || IsInsideBuilding(p, TreeBuildingBuffer) || TooCloseToRoadForTree(p) ||
                        IsNearWall(p, TreeWallClearance)) continue;
                    if (!TryReserve(p)) continue;
                    scatter.Add((rng.NextULong(), Make(p, rng, variantCount)));
                }
            }

            int room = Mathf.Max(0, maxTrees - fromOsm.Count);
            if (scatter.Count > room)
            {
                scatter.Sort((a, b) => a.key.CompareTo(b.key));
                report.TreesDroppedByCap = scatter.Count - room;
                scatter.RemoveRange(room, scatter.Count - room);
            }
            var result = new List<PropPlacement>(fromOsm);
            foreach (var s in scatter) result.Add(s.placement);
            report.TreesPlaced = result.Count;
            return result;
        }

        static PropPlacement Make(Vector2 p, SplitMix64 rng, int variantCount) => new PropPlacement
        {
            Position = p,
            YawDegrees = rng.Range(0f, 360f),
            Scale = rng.Range(0.8f, 1.2f),
            Variant = variantCount > 0 ? rng.RangeInclusive(0, variantCount - 1) : 0
        };

        public List<PropPlacement> PlaceLamps(IReadOnlyList<RoadFeature> roads, float minSpacing, float offset, GenerationReport report)
        {
            var result = new List<PropPlacement>();
            var grid = new SpatialGrid<int>(Mathf.Max(minSpacing, 1f) * 2f);
            foreach (var road in roads.OrderBy(r => r.Id))
            {
                float spacing = road.Rule != null ? road.Rule.lampSpacing : 0f;
                if (spacing <= 0f) continue;
                float hw = road.Width * 0.5f;
                int side = 1;
                foreach (var piece in road.Pieces)
                {
                    float next = spacing * 0.5f, travelled = 0f;
                    for (int i = 0; i + 1 < piece.Points.Count; i++)
                    {
                        Vector2 a = piece.Points[i], b = piece.Points[i + 1];
                        float length = Vector2.Distance(a, b);
                        if (length < 1e-4f) continue;
                        Vector2 dir = (b - a) / length;
                        var left = new Vector2(-dir.y, dir.x);
                        while (next <= travelled + length)
                        {
                            Vector2 center = a + dir * (next - travelled);
                            Vector2 sideDir = left * side;
                            Vector2 position = center + sideDir * (hw + LampOffset(road.Id, center, sideDir, offset));
                            Vector2 towardRoad = -sideDir;
                            if (!IsInsideBuilding(position, LampBuildingBuffer) && !BlocksLamp(position, road.Id) &&
                                !IsNearWall(position, LampWallClearance) && !NearOther(position, result, grid, minSpacing))
                            {
                                grid.Insert(new Rect(position, Vector2.zero), result.Count);
                                result.Add(new PropPlacement
                                {
                                    Position = position,
                                    YawDegrees = Mathf.Atan2(towardRoad.x, towardRoad.y) * Mathf.Rad2Deg,
                                    Scale = 1f,
                                    Variant = 0
                                });
                            }
                            side = -side;
                            next += spacing;
                        }
                        travelled += length;
                    }
                }
            }
            report.LampsPlaced = result.Count;
            return result;
        }

        /// <summary>Lamp distance beyond the asphalt edge: on the sidewalk, on the gutter shoulder, or the old fixed offset.</summary>
        float LampOffset(long roadId, Vector2 center, Vector2 sideDir, float fallback)
        {
            if (edges == null) return fallback;
            var profile = edges.ProfileAt(roadId, center, sideDir);
            if (profile == EdgeProfile.Sidewalk) return edgeSettings.sidewalkLampOffset;
            if (profile == EdgeProfile.Gutter) return edgeSettings.gutterLampOffset;
            return fallback;
        }

        static bool NearOther(Vector2 p, List<PropPlacement> placed, SpatialGrid<int> grid, float minSpacing)
        {
            var near = new HashSet<int>();
            grid.Query(Around(p, minSpacing), near);
            foreach (int i in near)
                if ((placed[i].Position - p).sqrMagnitude < minSpacing * minSpacing) return true;
            return false;
        }
    }
}
```

Di `WorldMeshPipeline.Build`, ganti `var placer = new PropPlacer(world.Buildings, world.Roads);` dengan:

```csharp
            var placer = new PropPlacer(world.Buildings, world.Roads, network, settings.roadEdges);
```

- [ ] **Step 4: Jalankan, pastikan lulus**

Procedure R+T, `Group = "StreetPropTests|PropPlacementTests|WorldMeshPipelineTests"`. Expected: `failed=0`.

- [ ] **Step 5: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add Assets/WorldGen/Editor/Builders/PropPlacer.cs Assets/WorldGen/Editor/Pipeline/WorldMeshPipeline.cs Assets/WorldGen/Tests/StreetPropTests.cs Assets/WorldGen/Tests/StreetPropTests.cs.meta
git -C "D:/UNITY/project/Tyco World" commit -m "feat(worldgen): lamps and trees follow road edges and walls

Co-Authored-By: <model> <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

---

### Task 8: Tes fisika kerb (player dan motor)

**Files:**
- Test: `Assets/Player/Tests/FirstPersonPlayerTests.cs` (tambahkan setelah `Tick_StepsOntoThinRibbon`)
- Test: `Assets/Vehicles/Tests/MotorbikeTests.cs` (tambahkan setelah `Ramp_WithinSlopeLimit_IsClimbed`)

**Interfaces:**
- Consumes:
  - `PhysicsFixture.Floor(float topY, float size)` dan `PhysicsFixture.Box(string, Vector3 localCenter, Vector3 size)` (Player, origin (5000, 1000, 5000));
  - `TestRig.Create(fx, Vector3).Init()`, `rig.Intents.Hold(...)`, `rig.Run(...)`, `rig.Local`, `rig.Player.IsGrounded`;
  - `VehicleFixture.Box(string, Vector3 local, Vector3 size)` (Vehicles, origin (7000, 1000, 7000));
  - `bike.Mount(player)`, `input.Hold(Gas, s)`, `Run(s)`, `bike.Bumps`.
- Produces: dua tes yang membuktikan kerb 0.13–0.21 m bisa dilewati (spec §4 "Kerb dan fisika"). Tidak ada kode produk yang berubah.

- [ ] **Step 1: Tulis tes**

Di `FirstPersonPlayerTests`:

```csharp
        [Test]
        public void Tick_StepsOntoKerb()
        {
            // Street-frontage spec §4: from residential asphalt (y 0.08) up a kerb onto a sidewalk whose top is 0.21.
            fx.Floor(0.08f);
            fx.Box("Sidewalk", new Vector3(0f, 0.21f - 0.5f, 2f + 5f), new Vector3(10f, 1f, 10f));
            fx.Sync();
            var rig = TestRig.Create(fx, new Vector3(0f, 0.08f, 0f)).Init();
            rig.Intents.Hold(Forward(), 2.5f);
            rig.Run(2.5f);
            Assert.Greater(rig.Local.z, 3f);
            Assert.That(rig.Local.y, Is.InRange(0.19f, 0.29f), "on the 0.21 m sidewalk (within skin width)");
            Assert.IsTrue(rig.Player.IsGrounded);
        }
```

Di `MotorbikeTests`:

```csharp
        [Test]
        public void Kerb_IsCrossedWithoutBump()
        {
            // Street-frontage spec §4: a 0.21 m kerb (higher than the real 0.13 m step) is below the 0.3 m step offset and below
            // the wall box cast, so the bike rides up onto the sidewalk instead of stopping.
            fx.Box("Sidewalk", new Vector3(0f, 0.21f - 0.5f, 8f + 20f), new Vector3(10f, 1f, 40f));
            fx.Sync();
            bike.Mount(player);
            input.Hold(Gas, 3f);
            Run(3f);
            Assert.Greater(bike.transform.position.z - O.z, 9f, "motor naik ke trotoar");
            Assert.AreEqual(0.21f, bike.transform.position.y - O.y, 0.03f);
            Assert.AreEqual(0, bike.Bumps);
        }
```

- [ ] **Step 2: Jalankan**

Procedure R+T, `Group = "Tick_StepsOntoKerb|Kerb_IsCrossedWithoutBump"`. Expected: `passed=2 failed=0`.

Kedua tes ini mengunci perilaku yang sudah ada (step offset 0.3), jadi langsung lulus. Kalau gagal, **jangan** mengubah tuning player atau motor. Laporkan ke controller: spec §4 lalu harus diubah ke kerb hanya visual dengan collider rata.

- [ ] **Step 3: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add Assets/Player/Tests/FirstPersonPlayerTests.cs Assets/Vehicles/Tests/MotorbikeTests.cs
git -C "D:/UNITY/project/Tyco World" commit -m "test(player,vehicles): kerbs up to 0.21 m are stepped over

Co-Authored-By: <model> <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

---

### Task 9: Material tepi jalan dan marka, lalu Checkpoint 1

**Files:**
- Create: `Assets/WorldGen/Editor/Art/StreetTextureGenerator.cs`
- Modify: `Assets/WorldGen/Editor/WorldGenPaths.cs` (tambah `StreetTexturesFolder`)
- Modify: `Assets/WorldGen/Editor/Art/ArtAssetBuilder.cs` (`BuildAll`, `CreateMaterials`, `Assign`)
- Modify: `Assets/WorldGen/Textures/SOURCES.md`
- Test: `Assets/WorldGen/Tests/ArtTests.cs`, `Assets/WorldGen/Tests/DefaultSettingsTests.cs`
- Generated (di-commit): `Assets/WorldGen/Textures/Street/KerbStripes.png(.meta)`, `Assets/WorldGen/Materials/Edge_Concrete.mat`, `Edge_Gutter.mat`, `Edge_Sidewalk.mat`, `Edge_Kerb.mat`, `Road_Marking.mat` (+ `.meta`), `Assets/WorldGen/Config/WorldGenSettings.asset`
- Update: `docs/superpowers/notes/2026-10-14-street-frontage-verification.md`

**Interfaces:**
- Consumes: pola `FacadeTextureGenerator` (`Render`/`WriteAll`), `ArtAssetBuilder.Mat(...)`, `Configure(path, normalMap, maxSize)`, `Tex(id, map)`, `Hex(...)`.
- Produces:
  - `WorldGenPaths.StreetTexturesFolder = TexturesFolder + "/Street"`
  - `StreetTextureGenerator.KerbFile = "KerbStripes.png"`, `KerbWidth = 128`, `KerbHeight = 32`
  - `StreetTextureGenerator.KerbStripes() : Texture2D`
  - `StreetTextureGenerator.WriteAll(string folder) : List<string>` (Task 12 menambah file lain)
  - Material `Edge_Concrete`, `Edge_Gutter`, `Edge_Sidewalk`, `Edge_Kerb`, `Road_Marking` di-assign ke `settings.roadEdges`

- [ ] **Step 1: Tulis tes yang gagal**

Di `ArtTests` (file sudah `using NUnit.Framework; using TycoWorld.WorldGen.Authoring; using UnityEngine;`):

```csharp
        [Test]
        public void KerbStripes_AreDarkThenLightAlongU()
        {
            var a = StreetTextureGenerator.KerbStripes();
            var b = StreetTextureGenerator.KerbStripes();
            try
            {
                Assert.AreEqual(StreetTextureGenerator.KerbWidth, a.width);
                Assert.AreEqual(StreetTextureGenerator.KerbHeight, a.height);
                CollectionAssert.AreEqual(a.GetPixels32(), b.GetPixels32());
                Assert.Less(a.GetPixel(16, 16).grayscale, 0.3f);
                Assert.Greater(a.GetPixel(96, 16).grayscale, 0.6f);
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }
```

Di `DefaultSettingsTests`:

```csharp
        static string NameOf(Material m) => m != null ? m.name : null;

        [Test]
        public void SavedAsset_HasEdgeMaterials()
        {
            var e = UnityEditor.AssetDatabase.LoadAssetAtPath<WorldGenSettings>(WorldGenPaths.SettingsAsset).roadEdges;
            Assert.AreEqual("Edge_Concrete", NameOf(e.concrete));
            Assert.AreEqual("Edge_Gutter", NameOf(e.gutter));
            Assert.AreEqual("Edge_Kerb", NameOf(e.kerb));
            Assert.AreEqual("Edge_Sidewalk", NameOf(e.sidewalk));
            Assert.AreEqual("Road_Marking", NameOf(e.marking));
        }
```

- [ ] **Step 2: Jalankan, pastikan gagal**

Procedure R+T, `Group = "ArtTests|DefaultSettingsTests"`. Expected: `COMPILE FAILED`.

- [ ] **Step 3: Implementasi generator tekstur dan path**

`WorldGenPaths.cs`, setelah `FacadeTexturesFolder`:

```csharp
        public const string StreetTexturesFolder = TexturesFolder + "/Street";
```

`Assets/WorldGen/Editor/Art/StreetTextureGenerator.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Procedural textures for road dressing and frontage (spec street-frontage §10). Deterministic per file.</summary>
    public static class StreetTextureGenerator
    {
        public const string KerbFile = "KerbStripes.png";
        public const int KerbWidth = 128, KerbHeight = 32;

        /// <summary>One metre of kerb along u: a black then a white 0.5 m block, with dirt noise and a darker lower edge.</summary>
        public static Texture2D KerbStripes()
        {
            var px = new Color32[KerbWidth * KerbHeight];
            var rng = new SplitMix64(SplitMix64.Fnv1a64("kerb"));
            for (int y = 0; y < KerbHeight; y++)
                for (int x = 0; x < KerbWidth; x++)
                {
                    float value = x < KerbWidth / 2 ? 0.12f : 0.85f;
                    value *= 1f + ((float)rng.NextDouble() - 0.5f) * 0.15f;
                    if (y < 4) value -= 0.06f;
                    value = Mathf.Clamp01(value);
                    px[y * KerbWidth + x] = new Color(value, value, value * 0.97f, 1f);
                }
            return Finish(px, KerbWidth, KerbHeight, "KerbStripes");
        }

        /// <summary>Writes every street texture as PNG into <paramref name="folder"/> and returns the asset paths.</summary>
        public static List<string> WriteAll(string folder)
        {
            AssetFolders.Ensure(folder);
            var paths = new List<string>();
            Write(folder, KerbFile, KerbStripes(), paths);
            return paths;
        }

        static Texture2D Finish(Color32[] px, int width, int height, string name)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, true) { name = name };
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        static void Write(string folder, string file, Texture2D tex, List<string> paths)
        {
            string assetPath = folder + "/" + file;
            File.WriteAllBytes(WorldGenPaths.ToAbsolute(assetPath), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            paths.Add(assetPath);
        }
    }
}
```

- [ ] **Step 4: Implementasi material dan assign**

Di `ArtAssetBuilder.BuildAll`, setelah loop `FacadeTextureGenerator.WriteAll(...)`:

```csharp
            foreach (var path in StreetTextureGenerator.WriteAll(WorldGenPaths.StreetTexturesFolder))
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                Configure(path, false, 256);
            }
```

Di `CreateMaterials`, setelah baris `Cc0("Roof_Dak_B", ...)`:

```csharp
            Cc0("Edge_Concrete", "Concrete034", "F0F0F0", 2f);
            Cc0("Edge_Sidewalk", "PavingStones070", "E8E8E8", 1.5f);
            m["Edge_Gutter"] = Mat("Edge_Gutter", Tex("Concrete046", "Color"), Tex("Concrete046", "NormalGL"), Hex("3A3A36"), 2f, 0.4f);
            m["Edge_Kerb"] = Mat("Edge_Kerb", AssetDatabase.LoadAssetAtPath<Texture2D>(WorldGenPaths.StreetTexturesFolder + "/" + StreetTextureGenerator.KerbFile),
                null, Color.white, 1f);
            m["Road_Marking"] = Mat("Road_Marking", null, null, Hex("DCDCD2"), 1f, 0.2f);
```

Di `Assign`, sebelum `EditorUtility.SetDirty(s);`:

```csharp
            var e = s.roadEdges;
            e.concrete = m["Edge_Concrete"];
            e.gutter = m["Edge_Gutter"];
            e.sidewalk = m["Edge_Sidewalk"];
            e.kerb = m["Edge_Kerb"];
            e.marking = m["Road_Marking"];
```

Di `Assets/WorldGen/Textures/SOURCES.md`, perbarui kolom "Dipakai untuk":
- Concrete034 → `Area_Parking, Area_Hardground, Edge_Concrete`
- Concrete046 → `Roof_Dak_A, Roof_Dak_B, Edge_Gutter`
- PavingStones070 → `Path_Paving, Edge_Sidewalk`

Lalu tambahkan paragraf di bawah paragraf `Facade/`:

```markdown
Tekstur di `Street/` dibuat oleh `StreetTextureGenerator` (prosedural, milik proyek): `KerbStripes.png`
(belang kerb hitam-putih per 0,5 m).
```

- [ ] **Step 5: Build Art Assets, lalu jalankan tes**

Jalankan Procedure A, lalu Procedure R+T dengan `Group = "ArtTests|DefaultSettingsTests"`. Expected: `failed=0`.

`git status --short` harus menunjukkan:
- yang baru: `Textures/Street/`, lima material baru;
- yang berubah: `WorldGenSettings.asset` (blok `roadEdges`/`frontage`/`infill` kini tertulis di YAML, plus material tepi), `SOURCES.md`, file kode.

Prefab atau material lama yang berubah tanpa sebab: kembalikan dengan `git checkout -- <path>`.

- [ ] **Step 6: Generate dan Checkpoint 1**

1. Jalankan Procedure G. Catat `EdgeMeters.*`, `MarkingDashes`, `Stages`, dan total waktu.
2. Masuk Play (Procedure P).
3. Ambil screenshot Procedure S untuk `road`, `kos`, `preview` di jam 10 dan 21, ke `shots/after-m1/`. Lihat setiap PNG dengan tool Read.
4. Keluar Play.
5. Tambahkan bagian berikut ke catatan verifikasi:

```markdown
## Checkpoint 1 — tepi jalan + marka (Task 9)

- Generate: <ms> ms; EdgeMeters.Gutter=<m>, EdgeMeters.Sidewalk=<m>, MarkingDashes=<n>, Gutter=<objek>, Sidewalk=<objek>
- Screenshot: `shots/after-m1/{road,kos,preview}_{10,21}.png`
- Pengamatan: <z-fighting? celah di simpang? kerb terlihat? lampu di bahu jalan?>
```

- [ ] **Step 7: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add Assets/WorldGen/Editor/Art/StreetTextureGenerator.cs Assets/WorldGen/Editor/Art/StreetTextureGenerator.cs.meta Assets/WorldGen/Editor/WorldGenPaths.cs Assets/WorldGen/Editor/Art/ArtAssetBuilder.cs Assets/WorldGen/Textures/SOURCES.md Assets/WorldGen/Textures/Street Assets/WorldGen/Textures/Street.meta Assets/WorldGen/Materials/Edge_Concrete.mat Assets/WorldGen/Materials/Edge_Concrete.mat.meta Assets/WorldGen/Materials/Edge_Gutter.mat Assets/WorldGen/Materials/Edge_Gutter.mat.meta Assets/WorldGen/Materials/Edge_Sidewalk.mat Assets/WorldGen/Materials/Edge_Sidewalk.mat.meta Assets/WorldGen/Materials/Edge_Kerb.mat Assets/WorldGen/Materials/Edge_Kerb.mat.meta Assets/WorldGen/Materials/Road_Marking.mat Assets/WorldGen/Materials/Road_Marking.mat.meta Assets/WorldGen/Config/WorldGenSettings.asset Assets/WorldGen/Tests/ArtTests.cs Assets/WorldGen/Tests/DefaultSettingsTests.cs docs/superpowers/notes/2026-10-14-street-frontage-verification.md
git -C "D:/UNITY/project/Tyco World" commit -m "feat(worldgen): edge and marking materials, checkpoint 1 notes

Co-Authored-By: <model> <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

**STOP — Checkpoint 1.** Controller menunjukkan screenshot before/after (`road_10`, `preview_10`, `kos_10`) ke user dan menunggu tanggapan sebelum Task 10.

---

### Task 10: `FrontagePlanner` — klasifikasi property line, lot, dan wall run

**Files:**
- Create: `Assets/WorldGen/Editor/Builders/FrontagePlanner.cs`
- Test: `Assets/WorldGen/Tests/FrontagePlannerTests.cs`

**Interfaces:**
- Consumes:
  - `RoadNetwork` (`Chains`, `Nearby`, `DistanceToChain`, `MaxReach`, `Sides`);
  - `RoadChain` (`Profiles`, `SampleS`, `PointAt`, `Points`, `Cum`, `HalfWidth`, `MaxEdgeWidth`, `Key`, `Length`);
  - `StreetContext` (`InBounds`, `InWater`, `InArea`, `InAnyArea`, `DistanceToKos`, `Raycast`);
  - `FrontageSettings` (`PickResidential`, `Style`);
  - `RoadEdgeSettings.Width`, `SplitMix64`, `Polyline2D.Project`.
- Produces:
  - `enum FrontageKind { Blocked, Empty, Flush, Lot, Vacant, Campus, Cemetery, Excluded }`
  - `class FrontageInterval { RoadChain Chain; int Side; EdgeProfile Profile; float S0, S1; FrontageKind Kind; StreetBuilding Building; float Length; }`
  - `class WallOpening { float S; float Width; bool IsGate; }`
  - `class WallRun { RoadChain Chain; int Side; EdgeProfile Profile; float S0, S1; WallStyleRule Style; int Band; float Height; List<WallOpening> Openings; }`
  - `static class FrontagePlanner`:
    - `static float LineOffset(RoadChain, EdgeProfile, RoadEdgeSettings, FrontageSettings)` = hw + lebar profil + 0.4
    - `static List<FrontageInterval> Plan(RoadNetwork, StreetContext, WorldGenSettings)` (tanpa interval Blocked)
    - `static FrontageKind Classify(RoadChain, int side, float s, EdgeProfile, RoadNetwork, StreetContext, RoadEdgeSettings, FrontageSettings, Dictionary<RoadChain,float> near, out StreetBuilding owner)`
    - `static void MergeShort(List<FrontageInterval>, float min)`
    - `static List<WallRun> Walls(IReadOnlyList<FrontageInterval>, FrontageSettings)`

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/WorldGen/Tests/FrontagePlannerTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class FrontagePlannerTests
    {
        WorldGenSettings settings;

        [SetUp]
        public void SetUp() => settings = DefaultSettingsFactory.CreateInMemory();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(settings);

        static Vector2 V(float x, float y) => new Vector2(x, y);

        List<FrontageInterval> Plan(StreetFixture fx) => FrontagePlanner.Plan(fx.Network, fx.Context, settings);

        static List<FrontageInterval> Of(IEnumerable<FrontageInterval> list, long roadId, int side) =>
            list.Where(x => x.Side == side && x.Chain.Members.Any(m => m.Road.Id == roadId)).ToList();

        static FrontageInterval At(IEnumerable<FrontageInterval> list, long roadId, int side, float s) =>
            Of(list, roadId, side).FirstOrDefault(x => x.S0 <= s && s <= x.S1);

        static bool Covers(IEnumerable<FrontageInterval> list, float s0, float s1) => list.Any(x => x.S1 > s0 && x.S0 < s1);

        static IEnumerable<WallRun> WallsAt(IEnumerable<WallRun> walls, long roadId, int side, float s) =>
            walls.Where(w => w.Side == side && w.Chain.Members.Any(m => m.Road.Id == roadId) && w.S0 <= s && s <= w.S1);

        [Test]
        public void SetbackHouse_IsALotWithOneGateFacingIt()
        {
            var plan = Plan(new StreetFixture(settings));
            var lot = At(plan, StreetFixture.Residential, -1, 25f);
            Assert.IsNotNull(lot);
            Assert.AreEqual(FrontageKind.Lot, lot.Kind);
            Assert.AreEqual(StreetFixture.SetbackHouse, lot.Building.Id);
            Assert.That(lot.S0, Is.InRange(19f, 20.5f));
            Assert.That(lot.S1, Is.InRange(29.5f, 31f));
            var run = WallsAt(FrontagePlanner.Walls(plan, settings.frontage), StreetFixture.Residential, -1, 25f).Single();
            Assert.AreEqual(1, run.Openings.Count);
            Assert.IsTrue(run.Openings[0].IsGate);
            Assert.AreEqual(2f, run.Openings[0].Width);
            Assert.AreEqual(25f, run.Openings[0].S, 0.05f, "gate faces the house centroid");
            Assert.AreEqual(EdgeProfile.Gutter, run.Profile);
        }

        [Test]
        public void FlushHouse_GetsNoWall()
        {
            var plan = Plan(new StreetFixture(settings));
            Assert.AreEqual(FrontageKind.Flush, At(plan, StreetFixture.Residential, -1, 65f).Kind);
            Assert.IsFalse(WallsAt(FrontagePlanner.Walls(plan, settings.frontage), StreetFixture.Residential, -1, 65f).Any());
        }

        [Test]
        public void CampusSide_IsOneContinuousFence()
        {
            var plan = Plan(new StreetFixture(settings));
            var north = Of(plan, StreetFixture.Tertiary, 1);
            Assert.IsTrue(north.All(x => x.Kind == FrontageKind.Campus));
            Assert.Greater(north.Sum(x => x.Length), 195f);
            var walls = FrontagePlanner.Walls(plan, settings.frontage).Where(w => w.Side == 1 && w.Chain.Members.Any(m => m.Road.Id == StreetFixture.Tertiary)).ToList();
            Assert.AreEqual(1, walls.Count);
            Assert.AreEqual(WallStyle.Campus, walls[0].Style.style);
            Assert.AreEqual(0, walls[0].Openings.Count);
            Assert.AreEqual(EdgeProfile.Sidewalk, walls[0].Profile);
        }

        [Test]
        public void ParkAndWater_GetNothing()
        {
            var plan = Plan(new StreetFixture(settings));
            Assert.AreEqual(FrontageKind.Empty, At(plan, StreetFixture.Residential, -1, 170f).Kind, "park behind the line");
            Assert.AreEqual(FrontageKind.Empty, At(plan, StreetFixture.Residential, -1, 182f).Kind, "park wins over the shed on the line");
            Assert.AreEqual(FrontageKind.Empty, At(plan, StreetFixture.Residential, 1, 50f).Kind, "drain behind the line");
            var walls = FrontagePlanner.Walls(plan, settings.frontage);
            Assert.IsFalse(WallsAt(walls, StreetFixture.Residential, -1, 170f).Any());
            Assert.IsFalse(WallsAt(walls, StreetFixture.Residential, 1, 50f).Any());
        }

        [Test]
        public void Kos_IsExcluded()
        {
            var plan = Plan(new StreetFixture(settings));
            Assert.AreEqual(FrontageKind.Excluded, At(plan, StreetFixture.Residential, 1, 125f).Kind);
            Assert.IsFalse(WallsAt(FrontagePlanner.Walls(plan, settings.frontage), StreetFixture.Residential, 1, 125f).Any());
        }

        [Test]
        public void RoadMouths_LeaveGaps()
        {
            var plan = Plan(new StreetFixture(settings));
            Assert.IsFalse(Covers(Of(plan, StreetFixture.Residential, 1), 97f, 103f), "service: 1.75 + 0.8 + 0.4 + 0.15");
            Assert.IsFalse(Covers(Of(plan, StreetFixture.Residential, -1), 38.8f, 41.2f), "footway: 0.75 + 0.4 + 0.15");
            Assert.IsFalse(Covers(Of(plan, StreetFixture.Residential, -1), 147.8f, 152.2f), "driveway: 1.75 + 0.4 + 0.15");
        }

        [Test]
        public void EmptyStretch_IsVacant()
        {
            var plan = Plan(new StreetFixture(settings));
            var vacant = At(plan, StreetFixture.Residential, -1, 110f);
            Assert.AreEqual(FrontageKind.Vacant, vacant.Kind);
            Assert.Greater(vacant.Length, 60f);
            Assert.AreEqual(FrontageKind.Vacant, At(plan, StreetFixture.Tertiary, -1, 50f).Kind);
        }

        [Test]
        public void Walls_AreStable()
        {
            var plan = Plan(new StreetFixture(settings));
            var a = FrontagePlanner.Walls(plan, settings.frontage);
            var b = FrontagePlanner.Walls(Plan(new StreetFixture(settings)), settings.frontage);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Style.style, b[i].Style.style);
                Assert.AreEqual(a[i].Height, b[i].Height);
                Assert.AreEqual(a[i].Band, b[i].Band);
                Assert.AreEqual(a[i].S0, b[i].S0);
            }
            Assert.IsTrue(a.All(w => w.Band >= w.Style.firstBand && w.Band < w.Style.firstBand + w.Style.bandCount));
            Assert.IsTrue(a.All(w => w.Height >= w.Style.minHeight - 1e-5f && w.Height <= w.Style.maxHeight + 1e-5f));
        }

        [Test]
        public void MergeShort_FoldsAShortStretchIntoItsLongerNeighbour()
        {
            var chain = new RoadChain();
            var list = new List<FrontageInterval>
            {
                new FrontageInterval { Chain = chain, Kind = FrontageKind.Vacant, S0 = 0f, S1 = 5f },
                new FrontageInterval { Chain = chain, Kind = FrontageKind.Flush, S0 = 5f, S1 = 6f },
                new FrontageInterval { Chain = chain, Kind = FrontageKind.Vacant, S0 = 6f, S1 = 12f },
                new FrontageInterval { Chain = chain, Kind = FrontageKind.Blocked, S0 = 12f, S1 = 13f },
                new FrontageInterval { Chain = chain, Kind = FrontageKind.Lot, S0 = 13f, S1 = 14f }
            };
            FrontagePlanner.MergeShort(list, 1.5f);
            Assert.AreEqual(3, list.Count);
            Assert.AreEqual(FrontageKind.Vacant, list[0].Kind);
            Assert.AreEqual(0f, list[0].S0);
            Assert.AreEqual(12f, list[0].S1);
            Assert.AreEqual(FrontageKind.Blocked, list[1].Kind);
            Assert.AreEqual(FrontageKind.Lot, list[2].Kind, "a short stretch next to a blocked one has nobody to join");
        }

        [Test]
        public void RotatedHouse_IsOneLotWithOneGate()
        {
            // An 8 × 8 m house turned 30°, centred 7 m behind the property line of a residential road along z = -200.
            var c = V(-250f, -200f - 3.7f - 7f);
            var corners = new[] { V(4, 4), V(-4, 4), V(-4, -4), V(4, -4) }
                .Select(p => c + new Vector2(p.x * 0.8660254f - p.y * 0.5f, p.x * 0.5f + p.y * 0.8660254f)).ToArray();
            var fx = new StreetFixture(settings, osm =>
            {
                osm.Way(3001, new[] { V(-300f, -200f), V(-200f, -200f) }, false, ("highway", "residential"));
                osm.Way(3002, corners, true, ("building", "house"));
            });
            var plan = Plan(fx);
            var lots = Of(plan, 3001, -1).Where(x => x.Kind == FrontageKind.Lot).ToList();
            Assert.AreEqual(1, lots.Count);
            Assert.AreEqual(3002, lots[0].Building.Id);
            var walls = FrontagePlanner.Walls(plan, settings.frontage).Where(w => w.Side == -1 && w.Chain.Members.Any(m => m.Road.Id == 3001)).ToList();
            Assert.AreEqual(1, walls.Sum(w => w.Openings.Count));
        }

        [Test]
        public void ParallelRoads_WallsStayOutOfTheOtherRoad()
        {
            var fx = new StreetFixture(settings, osm =>
            {
                osm.Way(3101, new[] { V(-300f, -400f), V(-200f, -400f) }, false, ("highway", "residential"));
                osm.Way(3102, new[] { V(-300f, -393f), V(-200f, -393f) }, false, ("highway", "service"));
            });
            var service = fx.Chain(3102);
            var walls = FrontagePlanner.Walls(Plan(fx), settings.frontage).Where(w => w.Chain.Members.Any(m => m.Road.Id == 3101)).ToList();
            Assert.Greater(walls.Count, 0);
            foreach (var w in walls)
                for (float s = w.S0; s <= w.S1; s += 1f)
                {
                    var p = w.Chain.SidePoint(s, w.Side, FrontagePlanner.LineOffset(w.Chain, w.Profile, settings.roadEdges, settings.frontage) + 0.075f);
                    Assert.GreaterOrEqual(Polyline2D.DistanceTo(service.Points, p), service.HalfWidth + service.MaxEdgeWidth);
                }
        }

        [Test]
        public void RightAngleBend_WallFollowsTheCorner()
        {
            var fx = new StreetFixture(settings, osm => osm.Way(2001, new[] { V(-300, -300), V(-250, -300), V(-250, -250) }, false, ("highway", "residential")));
            var plan = Plan(fx);
            foreach (int side in RoadNetwork.Sides)
            {
                var list = Of(plan, 2001, side);
                Assert.AreEqual(1, list.Count, "side " + side);
                Assert.AreEqual(FrontageKind.Vacant, list[0].Kind);
                Assert.Greater(list[0].Length, 95f);
            }
        }
    }
}
```

- [ ] **Step 2: Jalankan, pastikan gagal**

Procedure R+T, `Group = "FrontagePlannerTests"`. Expected: `COMPILE FAILED`.

- [ ] **Step 3: Implementasi**

`Assets/WorldGen/Editor/Builders/FrontagePlanner.cs`:

```csharp
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    public enum FrontageKind { Blocked, Empty, Flush, Lot, Vacant, Campus, Cemetery, Excluded }

    /// <summary>A stretch of one chain side's property line with one classification (spec street-frontage §7).</summary>
    public sealed class FrontageInterval
    {
        public RoadChain Chain;
        public int Side;
        public EdgeProfile Profile;
        public float S0, S1;
        public FrontageKind Kind;
        /// <summary>The building behind a Lot; null for every other kind.</summary>
        public StreetBuilding Building;
        public float Length => S1 - S0;
    }

    public sealed class WallOpening
    {
        public float S;
        public float Width;
        public bool IsGate;
    }

    /// <summary>A wall or fence along one interval, with its look and any gate/door opening.</summary>
    public sealed class WallRun
    {
        public RoadChain Chain;
        public int Side;
        public EdgeProfile Profile;
        public float S0, S1;
        public WallStyleRule Style;
        public int Band;
        public float Height;
        public readonly List<WallOpening> Openings = new List<WallOpening>();
    }

    /// <summary>Classifies the property line beside every edged road and turns it into wall runs (spec street-frontage §7).</summary>
    public static class FrontagePlanner
    {
        /// <summary>Distance from the chain centreline to the property line (the road-side face of the wall).</summary>
        public static float LineOffset(RoadChain chain, EdgeProfile profile, RoadEdgeSettings e, FrontageSettings f) =>
            chain.HalfWidth + e.Width(profile) + f.propertyLineGap;

        public static List<FrontageInterval> Plan(RoadNetwork net, StreetContext ctx, WorldGenSettings settings)
        {
            var e = settings.roadEdges;
            var f = settings.frontage;
            var result = new List<FrontageInterval>();
            var near = new Dictionary<RoadChain, float>();
            foreach (var chain in net.Chains)
            {
                if (chain.BaseProfile == EdgeProfile.None || chain.Points.Count < 2) continue;
                foreach (int side in RoadNetwork.Sides)
                {
                    var profiles = chain.Profiles(side);
                    int n = profiles.Length;
                    var kinds = new FrontageKind[n];
                    var owners = new StreetBuilding[n];
                    for (int i = 0; i < n; i++)
                        kinds[i] = Classify(chain, side, chain.SampleS(i, e.sampleStep), profiles[i], net, ctx, e, f, near, out owners[i]);
                    var list = new List<FrontageInterval>();
                    int start = 0;
                    for (int i = 1; i <= n; i++)
                    {
                        if (i < n && kinds[i] == kinds[start] && owners[i] == owners[start] && profiles[i] == profiles[start]) continue;
                        float s0 = start == 0 ? 0f : 0.5f * (chain.SampleS(start - 1, e.sampleStep) + chain.SampleS(start, e.sampleStep));
                        float s1 = i == n ? chain.Length : 0.5f * (chain.SampleS(i - 1, e.sampleStep) + chain.SampleS(i, e.sampleStep));
                        list.Add(new FrontageInterval
                        {
                            Chain = chain, Side = side, Profile = profiles[start], S0 = s0, S1 = s1, Kind = kinds[start], Building = owners[start]
                        });
                        start = i;
                    }
                    MergeShort(list, f.minInterval);
                    foreach (var x in list)
                        if (x.Kind != FrontageKind.Blocked && x.Length > 1e-3f) result.Add(x);
                }
            }
            return result;
        }

        /// <summary>Classification of the property-line sample at arc length s (spec street-frontage §7, rules in order).</summary>
        public static FrontageKind Classify(RoadChain chain, int side, float s, EdgeProfile profile, RoadNetwork net, StreetContext ctx,
            RoadEdgeSettings e, FrontageSettings f, Dictionary<RoadChain, float> near, out StreetBuilding owner)
        {
            owner = null;
            var c = chain.PointAt(s, out var t);
            var outward = Polyline2D.LeftNormal(t) * side;
            float offset = LineOffset(chain, profile, e, f);
            var p = c + outward * offset;
            if (!ctx.InBounds(p)) return FrontageKind.Blocked;
            if (net.DistanceToChain(chain, p, offset, s - offset - 1f, s + offset + 1f) < offset - 0.05f) return FrontageKind.Blocked;
            net.Nearby(p, net.MaxReach + f.propertyLineGap + f.wallThickness, chain, near);
            foreach (var kv in near)
                if (kv.Value < kv.Key.HalfWidth + kv.Key.MaxEdgeWidth + f.propertyLineGap + f.wallThickness) return FrontageKind.Blocked;

            var probe = p + outward * f.contextProbe;
            if (ctx.InWater(p) || ctx.InWater(probe)) return FrontageKind.Empty;
            if (ctx.InArea(probe, AreaCategory.Campus)) return ctx.InArea(c, AreaCategory.Campus) ? FrontageKind.Empty : FrontageKind.Campus;
            if (ctx.InArea(probe, AreaCategory.Cemetery)) return ctx.InArea(c, AreaCategory.Cemetery) ? FrontageKind.Empty : FrontageKind.Cemetery;
            if (ctx.InAnyArea(probe)) return FrontageKind.Empty;
            if (ctx.DistanceToKos(p) < f.kosExclusionRadius) return FrontageKind.Excluded;

            var hit = ctx.Raycast(p, outward, f.corridorDepth, out float distance);
            if (hit == null) return FrontageKind.Vacant;
            if (hit.Excluded) return FrontageKind.Excluded;
            if (distance < f.flushDistance) return FrontageKind.Flush;
            owner = hit;
            return FrontageKind.Lot;
        }

        /// <summary>
        /// Intervals shorter than <paramref name="min"/> join their longer touching neighbour (never a Blocked one), then equal
        /// neighbours coalesce. A short interval with no eligible neighbour stays.
        /// </summary>
        public static void MergeShort(List<FrontageInterval> list, float min)
        {
            while (true)
            {
                int pick = -1, into = -1;
                for (int i = 0; i < list.Count; i++)
                {
                    var x = list[i];
                    if (x.Kind == FrontageKind.Blocked || x.Length >= min) continue;
                    int target = LongerNeighbour(list, i);
                    if (target < 0) continue;
                    if (pick < 0 || x.Length < list[pick].Length)
                    {
                        pick = i;
                        into = target;
                    }
                }
                if (pick < 0) return;
                var small = list[pick];
                var host = list[into];
                if (into < pick) host.S1 = small.S1;
                else host.S0 = small.S0;
                list.RemoveAt(pick);
                Coalesce(list);
            }
        }

        static int LongerNeighbour(List<FrontageInterval> list, int i)
        {
            int best = -1;
            for (int j = i - 1; j <= i + 1; j += 2)
            {
                if (j < 0 || j >= list.Count || list[j].Kind == FrontageKind.Blocked) continue;
                float gap = j < i ? list[i].S0 - list[j].S1 : list[j].S0 - list[i].S1;
                if (Mathf.Abs(gap) > 1e-3f) continue;
                if (best < 0 || list[j].Length > list[best].Length) best = j;
            }
            return best;
        }

        static void Coalesce(List<FrontageInterval> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                var a = list[i - 1];
                var b = list[i];
                if (a.Kind != b.Kind || a.Building != b.Building || a.Profile != b.Profile || Mathf.Abs(b.S0 - a.S1) > 1e-3f) continue;
                a.S1 = b.S1;
                list.RemoveAt(i);
            }
        }

        /// <summary>Wall runs for Lot, Vacant, Campus and Cemetery intervals; styles, heights and bands are seeded by stable ids.</summary>
        public static List<WallRun> Walls(IReadOnlyList<FrontageInterval> intervals, FrontageSettings f)
        {
            var inv = CultureInfo.InvariantCulture;
            var runs = new List<WallRun>();
            foreach (var x in intervals)
            {
                WallStyleRule style;
                SplitMix64 rng;
                switch (x.Kind)
                {
                    case FrontageKind.Lot:
                        rng = SplitMix64.For(x.Building.Id, "wall");
                        style = f.PickResidential(rng.NextDouble());
                        break;
                    case FrontageKind.Vacant:
                        rng = SplitMix64.For(x.Chain.Key, "wall:" + x.Side.ToString(inv) + ":" + Mathf.RoundToInt(x.S0 * 10f).ToString(inv));
                        style = f.PickResidential(rng.NextDouble());
                        break;
                    case FrontageKind.Campus:
                    case FrontageKind.Cemetery:
                        rng = SplitMix64.For(x.Chain.Key, "wall:" + x.Side.ToString(inv) + ":" + Mathf.RoundToInt(x.S0 * 10f).ToString(inv));
                        style = f.Style(x.Kind == FrontageKind.Campus ? WallStyle.Campus : WallStyle.Cemetery);
                        break;
                    default:
                        continue;
                }
                if (style == null) continue;
                var run = new WallRun
                {
                    Chain = x.Chain, Side = x.Side, Profile = x.Profile, S0 = x.S0, S1 = x.S1, Style = style,
                    Height = rng.Range(style.minHeight, style.maxHeight),
                    Band = style.firstBand + rng.RangeInclusive(0, Mathf.Max(0, style.bandCount - 1))
                };
                if (x.Kind == FrontageKind.Lot) AddOpening(run, x, f);
                runs.Add(run);
            }
            return runs;
        }

        static void AddOpening(WallRun run, FrontageInterval x, FrontageSettings f)
        {
            float width = x.Length >= f.gateMinLot ? f.gateWidth : x.Length >= f.doorMinLot ? f.doorWidth : 0f;
            if (width <= 0f) return;
            float lo = x.S0 + f.gateEndMargin + width * 0.5f, hi = x.S1 - f.gateEndMargin - width * 0.5f;
            if (lo > hi) return;
            float centre = Polyline2D.Project(x.Chain.Points, x.Chain.Cum, x.Building.Centroid, out _);
            run.Openings.Add(new WallOpening { S = Mathf.Clamp(centre, lo, hi), Width = width, IsGate = width >= f.gateWidth });
        }
    }
}
```

- [ ] **Step 4: Jalankan, pastikan lulus**

Procedure R+T, `Group = "FrontagePlannerTests"`. Expected: `failed=0`.

- [ ] **Step 5: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add Assets/WorldGen/Editor/Builders/FrontagePlanner.cs Assets/WorldGen/Editor/Builders/FrontagePlanner.cs.meta Assets/WorldGen/Tests/FrontagePlannerTests.cs Assets/WorldGen/Tests/FrontagePlannerTests.cs.meta
git -C "D:/UNITY/project/Tyco World" commit -m "feat(worldgen): frontage planner for property-line lots and wall runs

Co-Authored-By: <model> <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

---

### Task 11: `FrontageMeshBuilder` — tembok, pilaster, pagar, gerbang, plat

**Files:**
- Create: `Assets/WorldGen/Editor/Builders/FrontageMeshBuilder.cs`
- Test: `Assets/WorldGen/Tests/FrontageMeshBuilderTests.cs`

**Interfaces:**
- Consumes: `WallRun`, `WallOpening`, `FrontagePlanner.LineOffset`, `SolidBox.Add/AddPost`, `BoxUv.Meters/AtlasBand`, `WallSegment` (Task 7), `WorldMeshPipeline.ChunkOf`.
- Produces:
  - `class FrontageMeshResult`:
    - `Dictionary<Vector2Int, MeshData> Render` (4 submesh), `Collider` (1 submesh)
    - `List<WallSegment> Blockers`
    - `SortedDictionary<string, float> MetersByStyle`
    - `float WallMeters`
    - `int Gates, Doors, Slabs, Pilasters`
  - `static class FrontageMeshBuilder`:
    - `WallsName = "Walls"`
    - submeshes: `SubWall = 0`, `SubConcrete = 1`, `SubFence = 2`, `SubMetal = 3`, `SubmeshCount = 4`
    - `static FrontageMeshResult Build(IReadOnlyList<WallRun>, RoadEdgeSettings, FrontageSettings, float chunkSize)`

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/WorldGen/Tests/FrontageMeshBuilderTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class FrontageMeshBuilderTests
    {
        WorldGenSettings settings;

        [SetUp]
        public void SetUp() => settings = DefaultSettingsFactory.CreateInMemory();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(settings);

        /// <summary>Residential chain (0,0) → (20,0), half width 2.5; its south (-1) property line is at z = -3.7.</summary>
        RoadChain Straight()
        {
            var rule = settings.roadRules.Single(r => r.highway == "residential");
            var road = new RoadFeature { Id = 1, Highway = "residential", Rule = rule, Width = 5f, Y = 0.08f, Tags = TestOsm.Tags(("highway", "residential")) };
            road.Pieces.Add(new LinePiece(new List<Vector2> { new Vector2(0f, 0f), new Vector2(20f, 0f) }, false, false));
            return new RoadNetwork(new[] { road }, new Polygon[0], settings.roadEdges).Chains[0];
        }

        WallRun Run(RoadChain chain, WallStyle style, float height, int band, float s0, float s1, params WallOpening[] openings)
        {
            var run = new WallRun
            {
                Chain = chain, Side = -1, Profile = EdgeProfile.Gutter, S0 = s0, S1 = s1,
                Style = settings.frontage.Style(style), Height = height, Band = band
            };
            run.Openings.AddRange(openings);
            return run;
        }

        FrontageMeshResult Build(params WallRun[] runs) => FrontageMeshBuilder.Build(runs, settings.roadEdges, settings.frontage, 100f);

        static List<Vector3> Verts(MeshData m, int sub) => m.Indices(sub).Select(i => m.Vertices[i]).ToList();

        static WallOpening Gate(float s) => new WallOpening { S = s, Width = 2f, IsGate = true };

        [Test]
        public void PlasterWall_FollowsTheStyleAndTheLine()
        {
            var r = Build(Run(Straight(), WallStyle.Plaster, 1.8f, 2, 2f, 18f));
            var m = r.Render.Values.Single();
            var wall = Verts(m, FrontageMeshBuilder.SubWall);
            Assert.AreEqual(1.8f, wall.Max(v => v.y), 1e-4f);
            Assert.AreEqual(-3.7f, wall.Max(v => v.z), 1e-4f, "road-side face on the property line");
            Assert.AreEqual(-3.85f, wall.Min(v => v.z), 1e-4f);
            Assert.AreEqual(1.8f + 0.05f + 0.1f, Verts(m, FrontageMeshBuilder.SubConcrete).Max(v => v.y), 1e-4f, "pilaster top");
            Assert.AreEqual(16f, r.WallMeters, 1e-3f);
            Assert.AreEqual(16f, r.MetersByStyle["Plaster"], 1e-3f);
            Assert.AreEqual(0, m.Indices(FrontageMeshBuilder.SubFence).Count);
        }

        [Test]
        public void WallUv_StaysInsideItsBand()
        {
            var m = Build(Run(Straight(), WallStyle.Plaster, 2.0f, 2, 2f, 18f)).Render.Values.Single();
            foreach (int i in m.Indices(FrontageMeshBuilder.SubWall))
                Assert.That(m.Uvs[i].y, Is.InRange(2f / 8f, 3f / 8f));
        }

        [Test]
        public void Gate_LeavesAGapInTheWall_ButHasACollider()
        {
            var r = Build(Run(Straight(), WallStyle.Plaster, 1.8f, 0, 2f, 18f, Gate(10f)));
            var m = r.Render.Values.Single();
            Assert.IsFalse(Verts(m, FrontageMeshBuilder.SubWall).Any(v => v.x > 9.01f && v.x < 10.99f));
            Assert.Greater(m.Indices(FrontageMeshBuilder.SubMetal).Count, 0);
            Assert.Greater(m.Indices(FrontageMeshBuilder.SubFence).Count, 0);
            var collider = r.Collider.Values.Single();
            Assert.IsTrue(collider.Vertices.Any(v => Mathf.Abs(v.x - 9f) < 1e-3f && Mathf.Abs(v.y - 1.6f) < 1e-3f), "closed gate box");
            Assert.AreEqual(1, r.Gates);
            Assert.AreEqual(0, r.Doors);
            Assert.AreEqual(14f, r.WallMeters, 1e-3f);
            Assert.IsTrue(r.Blockers.Any(b => Mathf.Abs(Vector2.Distance(b.A, b.B) - 2f) < 1e-3f));
        }

        [Test]
        public void GutterSide_GetsASlabInFrontOfTheGate()
        {
            var r = Build(Run(Straight(), WallStyle.Plaster, 1.8f, 0, 2f, 18f, Gate(10f)));
            Assert.AreEqual(1, r.Slabs);
            var concrete = Verts(r.Render.Values.Single(), FrontageMeshBuilder.SubConcrete);
            Assert.IsTrue(concrete.Any(v => Mathf.Abs(v.y - 0.165f) < 1e-4f && v.x >= 8.79f && v.x <= 11.21f && v.z <= -2.79f && v.z >= -3.71f));
            var sidewalkRun = Run(Straight(), WallStyle.Plaster, 1.8f, 0, 2f, 18f, Gate(10f));
            sidewalkRun.Profile = EdgeProfile.Sidewalk;
            Assert.AreEqual(0, Build(sidewalkRun).Slabs, "no gutter, no slab");
        }

        [Test]
        public void LowFence_HasABandARailAndAFullHeightCollider()
        {
            var r = Build(Run(Straight(), WallStyle.LowFence, 0.8f, 1, 2f, 18f));
            var m = r.Render.Values.Single();
            Assert.AreEqual(0.8f, Verts(m, FrontageMeshBuilder.SubWall).Max(v => v.y), 1e-4f);
            Assert.AreEqual(0.85f + 0.9f - 0.04f, Verts(m, FrontageMeshBuilder.SubFence).Max(v => v.y), 1e-4f);
            Assert.AreEqual(0.85f + 0.9f, Verts(m, FrontageMeshBuilder.SubMetal).Max(v => v.y), 1e-4f);
            Assert.IsTrue(r.Collider.Values.Single().Vertices.Any(v => Mathf.Abs(v.y - 1.75f) < 1e-4f), "solid box over the whole fence height");
        }

        [Test]
        public void AdjacentRuns_ShareTheirBoundaryPilaster()
        {
            var chain = Straight();
            var r = Build(Run(chain, WallStyle.Plaster, 1.8f, 0, 2f, 10f), Run(chain, WallStyle.Brick, 1.7f, 6, 10f, 18f));
            Assert.AreEqual(7, r.Pilasters, "2 ends + 2 inner per 8 m run, the shared end at s = 10 counted once");
        }

        [Test]
        public void TriangleBudget_PerMetre()
        {
            var r = Build(Run(Straight(), WallStyle.LowFence, 0.8f, 0, 2f, 18f, Gate(10f)));
            int tris = r.Render.Values.Sum(m => m.TriangleCount);
            Assert.Less(tris / 16f, 40f);
        }
    }
}
```

- [ ] **Step 2: Jalankan, pastikan gagal**

Procedure R+T, `Group = "FrontageMeshBuilderTests"`. Expected: `COMPILE FAILED`.

- [ ] **Step 3: Implementasi**

`Assets/WorldGen/Editor/Builders/FrontageMeshBuilder.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    public sealed class FrontageMeshResult
    {
        public readonly Dictionary<Vector2Int, MeshData> Render = new Dictionary<Vector2Int, MeshData>();
        public readonly Dictionary<Vector2Int, MeshData> Collider = new Dictionary<Vector2Int, MeshData>();
        public readonly List<WallSegment> Blockers = new List<WallSegment>();
        public readonly SortedDictionary<string, float> MetersByStyle = new SortedDictionary<string, float>(System.StringComparer.Ordinal);
        public float WallMeters;
        public int Gates, Doors, Slabs, Pilasters;
    }

    /// <summary>Walls, coping, pilasters, fence bands, gates and gutter slabs as per-chunk meshes (spec street-frontage §7).</summary>
    public static class FrontageMeshBuilder
    {
        public const string WallsName = "Walls";
        public const int SubWall = 0, SubConcrete = 1, SubFence = 2, SubMetal = 3, SubmeshCount = 4;
        const float RailSize = 0.04f, GateFrame = 0.06f, GateBottom = 0.1f, LeafThickness = 0.05f, GateColliderThickness = 0.06f;

        public static FrontageMeshResult Build(IReadOnlyList<WallRun> runs, RoadEdgeSettings e, FrontageSettings f, float chunkSize)
        {
            var result = new FrontageMeshResult();
            var pilasters = new HashSet<long>();
            foreach (var run in runs) AddRun(run, e, f, chunkSize, result, pilasters);
            return result;
        }

        static void AddRun(WallRun run, RoadEdgeSettings e, FrontageSettings f, float chunkSize, FrontageMeshResult r, HashSet<long> pilasters)
        {
            float lineOffset = FrontagePlanner.LineOffset(run.Chain, run.Profile, e, f);
            float centre = lineOffset + f.wallThickness * 0.5f;
            var openings = new List<WallOpening>(run.Openings);
            openings.Sort((a, b) => a.S.CompareTo(b.S));
            float cursor = run.S0;
            foreach (var o in openings)
            {
                AddPiece(run, cursor, o.S - o.Width * 0.5f, centre, f, chunkSize, r, pilasters);
                AddOpening(run, o, centre, lineOffset, e, f, chunkSize, r);
                cursor = o.S + o.Width * 0.5f;
            }
            AddPiece(run, cursor, run.S1, centre, f, chunkSize, r, pilasters);
        }

        static void AddPiece(WallRun run, float s0, float s1, float centre, FrontageSettings f, float chunkSize, FrontageMeshResult r,
            HashSet<long> pilasters)
        {
            if (s1 - s0 < 0.05f) return;
            var chain = run.Chain;
            float copingTop = run.Height + f.copingHeight;
            float fence = run.Style.fenceHeight;
            float total = copingTop + fence;
            var band = BoxUv.AtlasBand(run.Band, f.atlasBands, f.atlasBandMeters, f.atlasTileMeters);
            string style = run.Style.style.ToString();
            var along = new List<float>();
            var center = Polyline2D.Slice(chain.Points, chain.Cum, s0, s1, along);
            var line = Polyline2D.Offset(center, along, run.Side * centre, new List<float>());
            for (int j = 0; j + 1 < line.Count; j++)
            {
                Vector2 a = line[j], b = line[j + 1];
                float length = Vector2.Distance(a, b);
                if (length < 1e-3f) continue;
                var chunk = WorldMeshPipeline.ChunkOf((a + b) * 0.5f, chunkSize);
                var m = MeshIn(r.Render, chunk, SubmeshCount);
                var col = MeshIn(r.Collider, chunk, 1);
                SolidBox.Add(m, SubWall, a, b, 0f, run.Height, f.wallThickness, band);
                SolidBox.Add(m, SubConcrete, a, b, run.Height, copingTop, f.wallThickness + 2f * f.copingOverhang, BoxUv.Meters);
                if (fence > 0f)
                {
                    FenceBand(m, a, b, copingTop, fence - RailSize);
                    SolidBox.Add(m, SubMetal, a, b, total - RailSize, total, RailSize, BoxUv.Meters);
                }
                SolidBox.Add(col, 0, a, b, 0f, total, f.wallThickness, BoxUv.Meters);
                r.Blockers.Add(new WallSegment(a, b));
                r.WallMeters += length;
                r.MetersByStyle.TryGetValue(style, out float meters);
                r.MetersByStyle[style] = meters + length;
                Vector2 dir = (b - a) / length;
                int inner = Mathf.FloorToInt(length / f.pilasterSpacing);
                for (int k = 1; k <= inner; k++) Pilaster(a + dir * (k * length / (inner + 1)), dir, total, f, chunkSize, r, pilasters);
            }
            for (int j = 0; j < line.Count; j++)
            {
                int k = j + 1 < line.Count ? j + 1 : j - 1;
                if (k < 0) continue;
                var dir = line[k] - line[j];
                if (dir.sqrMagnitude < 1e-8f) continue;
                Pilaster(line[j], dir.normalized, total, f, chunkSize, r, pilasters);
            }
        }

        static void Pilaster(Vector2 p, Vector2 dir, float total, FrontageSettings f, float chunkSize, FrontageMeshResult r, HashSet<long> pilasters)
        {
            long key = ((long)Mathf.RoundToInt(p.x * 20f) << 32) ^ (uint)Mathf.RoundToInt(p.y * 20f);
            if (!pilasters.Add(key)) return;
            var chunk = WorldMeshPipeline.ChunkOf(p, chunkSize);
            float top = total + f.pilasterExtra;
            SolidBox.AddPost(MeshIn(r.Render, chunk, SubmeshCount), SubConcrete, p, dir, f.pilasterSize, 0f, top, BoxUv.Meters);
            SolidBox.AddPost(MeshIn(r.Collider, chunk, 1), 0, p, dir, f.pilasterSize, 0f, top, BoxUv.Meters);
            r.Pilasters++;
        }

        /// <summary>Two opposite-facing quads (both sides lit correctly) carrying the alpha-clipped bar texture: u = metres, v = 0..1.</summary>
        static void FenceBand(MeshData m, Vector2 a, Vector2 b, float y0, float height)
        {
            if (height <= 0f) return;
            float length = Vector2.Distance(a, b);
            var normal = Xz.Dir(Polyline2D.LeftNormal((b - a) / length));
            Vector3 p0 = Xz.At(a, y0), p1 = Xz.At(b, y0), p2 = Xz.At(b, y0 + height), p3 = Xz.At(a, y0 + height);
            Vector2 u0 = new Vector2(0f, 0f), u1 = new Vector2(length, 0f), u2 = new Vector2(length, 1f), u3 = new Vector2(0f, 1f);
            m.AddQuad(SubFence, p0, p1, p2, p3, u0, u1, u2, u3, normal);
            m.AddQuad(SubFence, p0, p1, p2, p3, u0, u1, u2, u3, -normal);
        }

        static void AddOpening(WallRun run, WallOpening o, float centre, float lineOffset, RoadEdgeSettings e, FrontageSettings f, float chunkSize,
            FrontageMeshResult r)
        {
            var chain = run.Chain;
            var c = chain.PointAt(o.S, out var t);
            var outward = Polyline2D.LeftNormal(t) * run.Side;
            var mid = c + outward * centre;
            Vector2 g0 = mid - t * (o.Width * 0.5f), g1 = mid + t * (o.Width * 0.5f);
            var chunk = WorldMeshPipeline.ChunkOf(mid, chunkSize);
            var m = MeshIn(r.Render, chunk, SubmeshCount);
            var col = MeshIn(r.Collider, chunk, 1);
            float top = f.gateHeight;
            SolidBox.Add(m, SubMetal, g0, g1, GateBottom, GateBottom + GateFrame, LeafThickness, BoxUv.Meters);
            SolidBox.Add(m, SubMetal, g0, g1, top - GateFrame, top, LeafThickness, BoxUv.Meters);
            SolidBox.AddPost(m, SubMetal, g0 + t * (GateFrame * 0.5f), t, GateFrame, GateBottom, top, BoxUv.Meters);
            SolidBox.AddPost(m, SubMetal, g1 - t * (GateFrame * 0.5f), t, GateFrame, GateBottom, top, BoxUv.Meters);
            FenceBand(m, g0, g1, GateBottom + GateFrame, top - GateBottom - 2f * GateFrame);
            SolidBox.Add(col, 0, g0, g1, 0f, top, GateColliderThickness, BoxUv.Meters);
            r.Blockers.Add(new WallSegment(g0, g1));
            if (o.IsGate) r.Gates++;
            else r.Doors++;
            if (run.Profile != EdgeProfile.Gutter) return;
            Vector2 near = c + outward * (chain.HalfWidth + e.shoulderWidth), far = c + outward * lineOffset;
            float width = o.Width + f.slabExtraWidth;
            SolidBox.Add(m, SubConcrete, near, far, 0f, f.slabTopY, width, BoxUv.Meters);
            SolidBox.Add(col, 0, near, far, 0f, f.slabTopY, width, BoxUv.Meters);
            r.Slabs++;
        }

        static MeshData MeshIn(Dictionary<Vector2Int, MeshData> meshes, Vector2Int chunk, int submeshes)
        {
            if (!meshes.TryGetValue(chunk, out var mesh)) meshes[chunk] = mesh = new MeshData(submeshes);
            return mesh;
        }
    }
}
```

- [ ] **Step 4: Jalankan, pastikan lulus**

Procedure R+T, `Group = "FrontageMeshBuilderTests|FrontagePlannerTests"`. Expected: `failed=0`.

- [ ] **Step 5: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add Assets/WorldGen/Editor/Builders/FrontageMeshBuilder.cs Assets/WorldGen/Editor/Builders/FrontageMeshBuilder.cs.meta Assets/WorldGen/Tests/FrontageMeshBuilderTests.cs Assets/WorldGen/Tests/FrontageMeshBuilderTests.cs.meta
git -C "D:/UNITY/project/Tyco World" commit -m "feat(worldgen): frontage meshes for walls, fences, gates and gutter slabs

Co-Authored-By: <model> <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

---

### Task 12: Tekstur dan material tembok/pagar (CC0 + prosedural)

**Files:**
- Download: `Assets/WorldGen/Textures/CC0/Plaster001/Plaster001_1K-JPG_{Color,NormalGL}.jpg`, `Assets/WorldGen/Textures/CC0/Bricks059/Bricks059_1K-JPG_{Color,NormalGL}.jpg`
- Modify: `Assets/WorldGen/Editor/Art/StreetTextureGenerator.cs` (atlas tembok, jeruji, `WriteAll`)
- Modify: `Assets/WorldGen/Editor/Art/ArtAssetBuilder.cs` (`Cc0Ids`, impor alpha, material `Frontage_*`, `Assign`)
- Modify: `Assets/WorldGen/Textures/SOURCES.md`
- Test: `Assets/WorldGen/Tests/ArtTests.cs`, `Assets/WorldGen/Tests/DefaultSettingsTests.cs`
- Generated (di-commit): `Textures/Street/WallAtlas.png`, `FenceBars.png`, `Materials/Frontage_WallAtlas.mat`, `Frontage_Fence.mat`, `Frontage_GateMetal.mat` (+ `.meta`), `Config/WorldGenSettings.asset`

**Interfaces:**
- Consumes: `FacadeTextureGenerator.ResidentialPalette` (6 warna), `StreetTextureGenerator` (Task 9), `ArtAssetBuilder.Mat/Configure/Hex`.
- Produces:
  - `StreetTextureGenerator`:
    - file: `WallAtlasFile = "WallAtlas.png"`, `FenceFile = "FenceBars.png"`
    - id CC0: `PlasterId = "Plaster001"`, `BrickId = "Bricks059"`
    - ukuran: `AtlasWidth = 256`, `BandHeight = 128`, `AtlasBands = 8`, `PixelsPerMeter = 64`, `FenceSize = 128`
    - `BrickTints`
    - `WallAtlas(Texture2D plaster, Texture2D brick) : Texture2D`
    - `FenceBars() : Texture2D`
    - `Cc0ColorPath(string id) : string`
  - Material `Frontage_WallAtlas`, `Frontage_Fence` (alpha clip), `Frontage_GateMetal` di-assign ke `settings.frontage`.

- [ ] **Step 1: Cek nama properti alpha clip URP 17**

Pakai Context7:
1. `mcp__claude_ai_Context7__resolve-library-id` dengan "Unity Universal Render Pipeline";
2. lalu `query-docs` dengan topik "Simple Lit alpha clipping material properties _AlphaClip _Cutoff _ALPHATEST_ON".

Yang harus dipastikan:
- properti float `_AlphaClip` (1 = on) dan `_Cutoff`;
- keyword `_ALPHATEST_ON`.

Jika namanya berbeda di URP 17.6, pakai nama yang benar di Step 5 dan catat di laporan task. Jika tool tidak tersedia, baca `Packages/com.unity.render-pipelines.universal/Shaders/SimpleLit.shader` lewat `Library/PackageCache` (Grep `_AlphaClip`).

- [ ] **Step 2: Unduh tekstur CC0 dan cek visual**

```bash
D="D:/UNITY/project/Tyco World/.superpowers/sdd/2026-10-14-street-frontage/downloads"
for id in Plaster001 Bricks059; do
  mkdir -p "$D/$id"
  curl -sSL -o "$D/$id/$id.zip" "https://ambientcg.com/get?file=${id}_1K-JPG.zip"
  unzip -o -j "$D/$id/$id.zip" "${id}_1K-JPG_Color.jpg" "${id}_1K-JPG_NormalGL.jpg" -d "$D/$id"
done
ls -la "$D"/*/
```

Lihat kedua `*_Color.jpg` dengan tool Read (gambar). Kriterianya:
- plester berwarna terang dan netral (untuk di-tint);
- bata berwarna merah/oranye dengan nat.

Kalau tidak cocok, coba `Plaster003` / `Bricks090` (keduanya terverifikasi ada saat plan ditulis). Lalu ganti konstanta `PlasterId`/`BrickId` di Step 4 sesuai pilihan.

Salin hanya dua JPG per id ke `Assets/WorldGen/Textures/CC0/<id>/`. File zip tetap di folder downloads yang di-ignore.

- [ ] **Step 3: Tulis tes yang gagal**

Di `ArtTests`:

```csharp
        static Texture2D Flat(float gray)
        {
            var t = new Texture2D(4, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
            var px = new Color[16];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(gray, gray, gray, 1f);
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        static Color AtlasPixel(Texture2D atlas, int band, int y) => atlas.GetPixel(10, band * StreetTextureGenerator.BandHeight + y);

        [Test]
        public void WallAtlas_BandsCarryTheirTint()
        {
            var plaster = Flat(0.5f);
            var brick = Flat(0.5f);
            var atlas = StreetTextureGenerator.WallAtlas(plaster, brick);
            try
            {
                Assert.AreEqual(StreetTextureGenerator.AtlasWidth, atlas.width);
                Assert.AreEqual(StreetTextureGenerator.BandHeight * StreetTextureGenerator.AtlasBands, atlas.height);
                // y = 50 px = 0.78 m: above the 25 cm grime, below the streaks that start at 1.2 m.
                for (int band = 0; band < 6; band++)
                {
                    var expected = FacadeTextureGenerator.ResidentialPalette[band];
                    var c = AtlasPixel(atlas, band, 50);
                    Assert.AreEqual(expected.r, c.r, 0.02f, "band " + band);
                    Assert.AreEqual(expected.g, c.g, 0.02f, "band " + band);
                    Assert.AreEqual(expected.b, c.b, 0.02f, "band " + band);
                }
                for (int i = 0; i < 2; i++)
                {
                    var expected = StreetTextureGenerator.BrickTints[i] * 0.5f;
                    var c = AtlasPixel(atlas, 6 + i, 50);
                    Assert.AreEqual(expected.r, c.r, 0.02f);
                    Assert.AreEqual(expected.g, c.g, 0.02f);
                }
                Assert.Less(AtlasPixel(atlas, 0, 2).grayscale, AtlasPixel(atlas, 0, 50).grayscale, "grime at the foot");
            }
            finally
            {
                Object.DestroyImmediate(plaster);
                Object.DestroyImmediate(brick);
                Object.DestroyImmediate(atlas);
            }
        }

        [Test]
        public void WallAtlas_IsDeterministic()
        {
            var plaster = Flat(0.6f);
            var brick = Flat(0.4f);
            var a = StreetTextureGenerator.WallAtlas(plaster, brick);
            var b = StreetTextureGenerator.WallAtlas(plaster, brick);
            try { CollectionAssert.AreEqual(a.GetPixels32(), b.GetPixels32()); }
            finally
            {
                Object.DestroyImmediate(plaster);
                Object.DestroyImmediate(brick);
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }

        [Test]
        public void FenceBars_HaveOpaqueBarsAndClearGaps()
        {
            var f = StreetTextureGenerator.FenceBars();
            try
            {
                Assert.AreEqual(1f, f.GetPixel(1, 30).a, 1e-3f, "bar");
                Assert.AreEqual(0f, f.GetPixel(8, 30).a, 1e-3f, "gap");
                Assert.AreEqual(1f, f.GetPixel(8, 1).a, 1e-3f, "bottom rail");
                Assert.AreEqual(1f, f.GetPixel(8, 62).a, 1e-3f, "middle rail");
                Assert.AreEqual(1f, f.GetPixel(8, 126).a, 1e-3f, "top rail");
            }
            finally { Object.DestroyImmediate(f); }
        }

        [Test]
        public void FenceMaterial_ClipsAlpha()
        {
            var m = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(WorldGenPaths.MaterialsFolder + "/Frontage_Fence.mat");
            Assert.IsNotNull(m);
            Assert.AreEqual(1f, m.GetFloat("_AlphaClip"));
            Assert.IsTrue(m.IsKeywordEnabled("_ALPHATEST_ON"));
            Assert.AreEqual((int)UnityEngine.Rendering.RenderQueue.AlphaTest, m.renderQueue);
        }
```

Di `DefaultSettingsTests`:

```csharp
        [Test]
        public void SavedAsset_HasFrontageMaterials()
        {
            var f = UnityEditor.AssetDatabase.LoadAssetAtPath<WorldGenSettings>(WorldGenPaths.SettingsAsset).frontage;
            Assert.AreEqual("Frontage_WallAtlas", NameOf(f.wallAtlas));
            Assert.AreEqual("Frontage_Fence", NameOf(f.fence));
            Assert.AreEqual("Frontage_GateMetal", NameOf(f.gateMetal));
        }
```

Procedure R+T, `Group = "ArtTests"`. Expected: `COMPILE FAILED`.

- [ ] **Step 4: Implementasi generator**

Di `StreetTextureGenerator`, tambahkan konstanta dan method berikut (biarkan `KerbStripes`, `Finish`, dan `Write` tetap ada):

```csharp
        public const string WallAtlasFile = "WallAtlas.png", FenceFile = "FenceBars.png";
        public const string PlasterId = "Plaster001", BrickId = "Bricks059";
        public const int AtlasWidth = 256, BandHeight = 128, AtlasBands = 8, PixelsPerMeter = 64, FenceSize = 128;
        const float PlasterSourceMeters = 2f, BrickSourceMeters = 1.5f;

        /// <summary>Tints of the two brick bands (6 and 7) over the CC0 brick colour map.</summary>
        public static readonly Color[] BrickTints = { Color.white, new Color(0.8f, 0.72f, 0.68f, 1f) };

        static readonly Color Moss = new Color(0.33f, 0.40f, 0.24f, 1f);
        static readonly Color32 Iron = new Color32(0x2B, 0x2F, 0x33, 255);

        public static string Cc0ColorPath(string id) => WorldGenPaths.Cc0TexturesFolder + "/" + id + "/" + id + "_1K-JPG_Color.jpg";

        /// <summary>
        /// Eight stacked bands (band 0 at the bottom), each 4 m × 2 m of wall at 64 px/m. Bands 0–5 are the plaster colour map
        /// re-tinted with the residential facade palette, bands 6–7 the brick map with BrickTints. Bottom 25 cm get green grime,
        /// and three faint rain streaks per band run down from 1.2 m.
        /// </summary>
        public static Texture2D WallAtlas(Texture2D plaster, Texture2D brick)
        {
            var px = new Color32[AtlasWidth * BandHeight * AtlasBands];
            float plasterMean = MeanLuminance(plaster);
            var palette = FacadeTextureGenerator.ResidentialPalette;
            var rng = new SplitMix64(SplitMix64.Fnv1a64("wall-atlas"));
            var streaks = new int[3];
            for (int band = 0; band < AtlasBands; band++)
            {
                for (int k = 0; k < streaks.Length; k++) streaks[k] = rng.RangeInclusive(0, AtlasWidth - 1);
                bool isBrick = band >= palette.Length;
                for (int y = 0; y < BandHeight; y++)
                {
                    float my = y / (float)PixelsPerMeter;
                    for (int x = 0; x < AtlasWidth; x++)
                    {
                        float mx = x / (float)PixelsPerMeter;
                        Color c;
                        if (isBrick)
                        {
                            c = brick.GetPixelBilinear(mx / BrickSourceMeters, my / BrickSourceMeters) * BrickTints[band - palette.Length];
                        }
                        else
                        {
                            float lum = plaster.GetPixelBilinear(mx / PlasterSourceMeters, my / PlasterSourceMeters).grayscale;
                            float k = plasterMean > 1e-4f ? Mathf.Clamp(lum / plasterMean, 0.6f, 1.4f) : 1f;
                            c = palette[band] * k;
                        }
                        c = Weather(c, x, my, streaks);
                        c.a = 1f;
                        px[(band * BandHeight + y) * AtlasWidth + x] = c;
                    }
                }
            }
            return Finish(px, AtlasWidth, BandHeight * AtlasBands, "WallAtlas");
        }

        /// <summary>One metre of iron fence along u (8 bars), the band height along v: bars and three rails opaque, gaps clear.</summary>
        public static Texture2D FenceBars()
        {
            var px = new Color32[FenceSize * FenceSize];
            var clear = new Color32(0, 0, 0, 0);
            for (int y = 0; y < FenceSize; y++)
                for (int x = 0; x < FenceSize; x++)
                {
                    bool bar = x % 16 < 3;
                    bool rail = y < 4 || y >= FenceSize - 4 || (y >= 60 && y < 64);
                    px[y * FenceSize + x] = bar || rail ? Iron : clear;
                }
            return Finish(px, FenceSize, FenceSize, "FenceBars");
        }

        static Color Weather(Color c, int x, float my, int[] streaks)
        {
            if (my < 0.25f)
            {
                float t = 1f - my / 0.25f;
                c = Color.Lerp(c * (1f - 0.3f * t), Moss, 0.25f * t);
            }
            if (my > 1.2f)
                foreach (int sx in streaks)
                    if (Mathf.Abs(x - sx) <= 2) c *= 0.88f;
            return c;
        }

        static float MeanLuminance(Texture2D tex)
        {
            float sum = 0f;
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++) sum += tex.GetPixelBilinear((x + 0.5f) / 32f, (y + 0.5f) / 32f).grayscale;
            return sum / (32f * 32f);
        }

        static Texture2D LoadReadable(string assetPath)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(WorldGenPaths.ToAbsolute(assetPath)));
            tex.wrapMode = TextureWrapMode.Repeat;
            return tex;
        }
```

Ganti `WriteAll` dengan:

```csharp
        /// <summary>Writes every street texture as PNG into <paramref name="folder"/> and returns the asset paths. Needs the CC0 plaster and brick maps.</summary>
        public static List<string> WriteAll(string folder)
        {
            AssetFolders.Ensure(folder);
            var paths = new List<string>();
            Write(folder, KerbFile, KerbStripes(), paths);
            Write(folder, FenceFile, FenceBars(), paths);
            var plaster = LoadReadable(Cc0ColorPath(PlasterId));
            var brick = LoadReadable(Cc0ColorPath(BrickId));
            try
            {
                Write(folder, WallAtlasFile, WallAtlas(plaster, brick), paths);
            }
            finally
            {
                Object.DestroyImmediate(plaster);
                Object.DestroyImmediate(brick);
            }
            return paths;
        }
```

- [ ] **Step 5: Implementasi material**

Di `ArtAssetBuilder`:

1. `Cc0Ids`: tambahkan `StreetTextureGenerator.PlasterId, StreetTextureGenerator.BrickId` di akhir array.

2. Ganti loop tekstur street (dari Task 9) dengan:

```csharp
            foreach (var path in StreetTextureGenerator.WriteAll(WorldGenPaths.StreetTexturesFolder))
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                Configure(path, false, path.EndsWith(StreetTextureGenerator.WallAtlasFile) ? 1024 : 256);
                if (path.EndsWith(StreetTextureGenerator.FenceFile)) ConfigureAlpha(path);
            }
```

3. Method baru (di dekat `Configure`):

```csharp
        /// <summary>Alpha-tested texture: keep transparency and preserve coverage in the mips so thin bars do not vanish far away.</summary>
        static void ConfigureAlpha(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            bool changed = false;
            if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
            if (importer.alphaSource != TextureImporterAlphaSource.FromInput) { importer.alphaSource = TextureImporterAlphaSource.FromInput; changed = true; }
            if (!importer.mipMapsPreserveCoverage) { importer.mipMapsPreserveCoverage = true; changed = true; }
            if (Mathf.Abs(importer.alphaTestReferenceValue - 0.5f) > 1e-4f) { importer.alphaTestReferenceValue = 0.5f; changed = true; }
            if (changed) importer.SaveAndReimport();
        }

        static Texture2D StreetTexture(string file) => AssetDatabase.LoadAssetAtPath<Texture2D>(WorldGenPaths.StreetTexturesFolder + "/" + file);
```

4. Di `CreateMaterials`, setelah material `Road_Marking`:

```csharp
            m["Frontage_WallAtlas"] = Mat("Frontage_WallAtlas", StreetTexture(StreetTextureGenerator.WallAtlasFile), null, Color.white, 1f);
            var fence = Mat("Frontage_Fence", StreetTexture(StreetTextureGenerator.FenceFile), null, Color.white, 1f, 0.3f);
            fence.SetFloat("_AlphaClip", 1f);
            fence.SetFloat("_Cutoff", 0.5f);
            fence.EnableKeyword("_ALPHATEST_ON");
            fence.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            EditorUtility.SetDirty(fence);
            m["Frontage_Fence"] = fence;
            m["Frontage_GateMetal"] = Mat("Frontage_GateMetal", null, null, Hex("2B2F33"), 1f, 0.45f);
```

Jika Step 1 menemukan nama lain, pakai nama itu. Sesuaikan juga tes `FenceMaterial_ClipsAlpha`.

5. Di `Assign`, sebelum `EditorUtility.SetDirty(s);`:

```csharp
            var f = s.frontage;
            f.wallAtlas = m["Frontage_WallAtlas"];
            f.fence = m["Frontage_Fence"];
            f.gateMetal = m["Frontage_GateMetal"];
```

6. `SOURCES.md`: tambahkan dua baris tabel berikut. Sesuaikan id jika di Step 2 diganti.

```markdown
| Plaster001 | https://ambientcg.com/view?id=Plaster001 | Frontage_WallAtlas (band plester 0–5, di-tint) |
| Bricks059 | https://ambientcg.com/view?id=Bricks059 | Frontage_WallAtlas (band bata 6–7) |
```

Lalu ubah paragraf `Street/` menjadi:

```markdown
Tekstur di `Street/` dibuat oleh `StreetTextureGenerator` (prosedural, milik proyek): `KerbStripes.png`
(belang kerb), `FenceBars.png` (jeruji besi alpha-clip), dan `WallAtlas.png` (8 band tembok, turunan
Plaster001/Bricks059 yang di-tint, plus noda dan lumut).
```

- [ ] **Step 6: Build Art Assets, lalu jalankan tes**

Jalankan Procedure A, lalu Procedure R+T dengan `Group = "ArtTests|DefaultSettingsTests"`. Expected: `failed=0`.

Lihat `Assets/WorldGen/Textures/Street/WallAtlas.png` dengan tool Read. Yang harus terlihat: 8 band warna, bawahnya kusam, dan 2 band teratas bata.

- [ ] **Step 7: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add Assets/WorldGen/Textures/CC0/Plaster001 Assets/WorldGen/Textures/CC0/Plaster001.meta Assets/WorldGen/Textures/CC0/Bricks059 Assets/WorldGen/Textures/CC0/Bricks059.meta Assets/WorldGen/Textures/Street Assets/WorldGen/Textures/SOURCES.md Assets/WorldGen/Editor/Art/StreetTextureGenerator.cs Assets/WorldGen/Editor/Art/ArtAssetBuilder.cs Assets/WorldGen/Materials/Frontage_WallAtlas.mat Assets/WorldGen/Materials/Frontage_WallAtlas.mat.meta Assets/WorldGen/Materials/Frontage_Fence.mat Assets/WorldGen/Materials/Frontage_Fence.mat.meta Assets/WorldGen/Materials/Frontage_GateMetal.mat Assets/WorldGen/Materials/Frontage_GateMetal.mat.meta Assets/WorldGen/Config/WorldGenSettings.asset Assets/WorldGen/Tests/ArtTests.cs Assets/WorldGen/Tests/DefaultSettingsTests.cs
git -C "D:/UNITY/project/Tyco World" commit -m "feat(worldgen): wall atlas, fence bars and frontage materials

Co-Authored-By: <model> <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

Kalau id diganti di Step 2, sesuaikan path pada perintah `git add`.

---

### Task 13: Frontage di pipeline, lalu Checkpoint 2

**Files:**
- Modify: `Assets/WorldGen/Editor/Pipeline/WorldMeshPipeline.cs` (`Build`, method baru `AddFrontage`)
- Test: `Assets/WorldGen/Tests/WorldMeshPipelineTests.cs`
- Update: `docs/superpowers/notes/2026-10-14-street-frontage-verification.md`

**Interfaces:**
- Consumes:
  - `FrontagePlanner.Plan/Walls`, `FrontageMeshBuilder.Build`;
  - `AddChunkFeatures` (Task 6);
  - `PropPlacer.AddWalls` (Task 7);
  - variabel `network` dan `street` di `Build`.
- Produces:
  - `static List<WallSegment> AddFrontage(BuiltWorld, IReadOnlyList<FrontageInterval>, WorldGenSettings, GenerationReport)` (private)
  - Counter report: `"Walls"` (objek chunk), `"WallMeters"`, `"WallMeters.<Style>"`, `"Gates"`, `"Doors"`, `"GutterSlabs"`

- [ ] **Step 1: Tulis tes yang gagal**

Di `WorldMeshPipelineTests`:

```csharp
        [Test]
        public void Street_WallsAreChunkObjectsWithSeparateColliders()
        {
            var report = new GenerationReport();
            var walls = BuildStreet(report).Features.Where(f => f.Kind == FeatureKind.Frontage).ToList();
            Assert.Greater(walls.Count, 0);
            foreach (var f in walls)
            {
                Assert.AreEqual(FeatureGroups.Frontage, f.Group);
                Assert.AreEqual(FrontageMeshBuilder.WallsName, f.Name);
                Assert.IsFalse(f.HasOsmIdentity);
                Assert.IsTrue(f.AddCollider);
                Assert.IsNotNull(f.ColliderMesh);
                Assert.AreEqual(SurfaceType.Default, f.Surface);
                Assert.AreEqual(FrontageMeshBuilder.SubmeshCount, f.Materials.Length);
            }
            Assert.Greater(report.GetCount("WallMeters"), 0);
            Assert.GreaterOrEqual(report.GetCount("Gates"), 1);
            Assert.GreaterOrEqual(report.GetCount("GutterSlabs"), 1);
            Assert.Greater(report.GetCount("WallMeters.Campus"), 150);
        }

        [Test]
        public void LongStraightRoad_WallCollidersStayUnder100m()
        {
            var osm = new TestOsm();
            osm.Way(960, new[] { new Vector2(-600f, -1000f), new Vector2(400f, -1000f) }, false, ("highway", "residential"));
            var report = new GenerationReport();
            var built = WorldMeshPipeline.Build(new FeatureClassifier(settings, report).Classify(osm.Data), settings, report);
            var walls = built.Features.Where(f => f.Kind == FeatureKind.Frontage).ToList();
            Assert.Greater(walls.Count, 0);
            foreach (var f in walls)
            {
                Assert.LessOrEqual(MeshDataSplitTests.MaxEdge(f.Mesh), WorldMeshPipeline.MaxColliderEdge + 1e-3f);
                Assert.LessOrEqual(MeshDataSplitTests.MaxEdge(f.ColliderMesh), WorldMeshPipeline.MaxColliderEdge + 1e-3f);
            }
        }
```

Procedure R+T, `Group = "WorldMeshPipelineTests"`. Expected: kedua tes baru gagal (tidak ada fitur Frontage).

- [ ] **Step 2: Implementasi**

Di `WorldMeshPipeline.Build`, tepat setelah `AddEdges(built, network, street, settings, report);`:

```csharp
            var walls = AddFrontage(built, FrontagePlanner.Plan(network, street, settings), settings, report);
```

Setelah baris `var placer = new PropPlacer(world.Buildings, world.Roads, network, settings.roadEdges);`:

```csharp
            placer.AddWalls(walls);
```

Method baru (di bawah `AddEdges`):

```csharp
        static List<WallSegment> AddFrontage(BuiltWorld built, IReadOnlyList<FrontageInterval> plan, WorldGenSettings settings, GenerationReport report)
        {
            var f = settings.frontage;
            var meshes = FrontageMeshBuilder.Build(FrontagePlanner.Walls(plan, f), settings.roadEdges, f, built.ChunkSize);
            AddChunkFeatures(built, meshes.Render, meshes.Collider, FeatureKind.Frontage, FeatureGroups.Frontage, FrontageMeshBuilder.WallsName,
                new[] { f.wallAtlas, settings.roadEdges.concrete, f.fence, f.gateMetal }, report);
            report.Count("WallMeters", Mathf.RoundToInt(meshes.WallMeters));
            foreach (var kv in meshes.MetersByStyle) report.Count("WallMeters." + kv.Key, Mathf.RoundToInt(kv.Value));
            report.Count("Gates", meshes.Gates);
            report.Count("Doors", meshes.Doors);
            report.Count("GutterSlabs", meshes.Slabs);
            return meshes.Blockers;
        }
```

Urutan material mengikuti `FrontageMeshBuilder`: 0 = wall atlas, 1 = concrete, 2 = fence, 3 = gate metal.

- [ ] **Step 3: Jalankan semua tes**

Procedure R+T, `Group = "."`. Expected: `failed=0`.

- [ ] **Step 4: Generate dan Checkpoint 2**

1. Jalankan Procedure G. Catat `WallMeters*`, `Gates`, `Doors`, `GutterSlabs`, `Walls`, `Stages`, dan total waktu. Tidak boleh ada error.
2. Masuk Play (Procedure P).
3. Ambil screenshot Procedure S untuk `road`, `kos`, `kos0`, `preview`, `high` di jam 10 dan 21, ke `shots/after-m2/`. Lihat setiap PNG.
4. Keluar Play.
5. Tambahkan ke catatan verifikasi:

```markdown
## Checkpoint 2 — tembok, pagar, gerbang (Task 13)

- Generate: <ms> ms; WallMeters=<m> (<per gaya>), Gates=<n>, Doors=<n>, GutterSlabs=<n>, Walls=<objek>
- Screenshot: `shots/after-m2/{road,kos,kos0,preview,high}_{10,21}.png`
- Pengamatan: <tembok menutup kavling kosong? gerbang menghadap rumah? tembok memotong mulut jalan? halaman kos utuh?>
```

- [ ] **Step 5: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add Assets/WorldGen/Editor/Pipeline/WorldMeshPipeline.cs Assets/WorldGen/Tests/WorldMeshPipelineTests.cs docs/superpowers/notes/2026-10-14-street-frontage-verification.md
git -C "D:/UNITY/project/Tyco World" commit -m "feat(worldgen): property-line walls in the generated world, checkpoint 2

Co-Authored-By: <model> <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

**STOP — Checkpoint 2.** Controller menunjukkan screenshot before/after-m1/after-m2 ke user dan menunggu tanggapan sebelum Task 14.

---

### Task 14: `InfillPlanner` — rumah fiktif di kavling kosong dekat jalan

**Files:**
- Create: `Assets/WorldGen/Editor/Builders/InfillPlanner.cs`
- Test: `Assets/WorldGen/Tests/InfillPlannerTests.cs`

**Interfaces:**
- Consumes:
  - `FrontageInterval` (pass 1) dan `FrontagePlanner.LineOffset`;
  - `RoadNetwork.Nearby/MaxReach`, `RoadChain.SidePoint/Key/Index`;
  - `StreetContext` (`InBounds`, `OverlapsBuilding/Water/Area`, `NearKos`, `AddBuilding`);
  - `BuildingSpec`, `RoofOverride`, `SplitMix64`;
  - `WorldGenSettings.levelHeight/minWallHeight/infill/frontage/roadEdges`.
- Produces:
  - `class InfillHouse { long Id; BuildingSpec Spec; Vector2 Centroid; bool Flush; BuildingFeature AsBuildingFeature(); }`
  - `static class InfillPlanner`:
    - skip reasons: `SkipBlocked = "infill:blocked"`, `SkipCap = "infill:cap"`
    - `static long InfillId(long chainKey, int side, float s)` (selalu negatif)
    - `static List<InfillHouse> Plan(IReadOnlyList<FrontageInterval> pass1, RoadNetwork, StreetContext, WorldGenSettings, GenerationReport)`. Setiap rumah yang diterima langsung didaftarkan ke `ctx` (`IsInfill = true`).

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/WorldGen/Tests/InfillPlannerTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class InfillPlannerTests
    {
        WorldGenSettings settings;

        [SetUp]
        public void SetUp() => settings = DefaultSettingsFactory.CreateInMemory();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(settings);

        static Vector2 V(float x, float y) => new Vector2(x, y);

        List<InfillHouse> Plan(StreetFixture fx, GenerationReport report = null) =>
            InfillPlanner.Plan(FrontagePlanner.Plan(fx.Network, fx.Context, settings), fx.Network, fx.Context, settings, report ?? new GenerationReport());

        [Test]
        public void Houses_FitTheSpecRanges()
        {
            var houses = Plan(new StreetFixture(settings));
            Assert.Greater(houses.Count, 5);
            foreach (var h in houses)
            {
                var obb = ObbCalculator.Compute(h.Spec.Footprint.Outer);
                float a = 2f * obb.HalfU, b = 2f * obb.HalfV;
                Assert.GreaterOrEqual(Mathf.Min(a, b), 6f - 1e-3f);
                Assert.LessOrEqual(Mathf.Max(a, b), 16f + 1e-3f);
                Assert.That(h.Spec.Levels, Is.InRange(1, 2));
                Assert.AreEqual(h.Spec.Levels * settings.levelHeight, h.Spec.WallHeight, 1e-4f);
                Assert.AreEqual(h.Id, h.Spec.Id);
                if (h.Flush)
                {
                    Assert.AreEqual(2, h.Spec.Levels);
                    Assert.AreEqual(RoofOverride.Flat, h.Spec.RoofOverride);
                    Assert.IsFalse(h.Spec.AllowGable);
                }
                else
                {
                    Assert.AreEqual(RoofOverride.Auto, h.Spec.RoofOverride);
                    Assert.IsTrue(h.Spec.AllowGable);
                }
            }
            Assert.IsTrue(houses.Any(h => h.Flush));
            Assert.IsTrue(houses.Any(h => !h.Flush));
        }

        [Test]
        public void Houses_OverlapNothing()
        {
            var fx = new StreetFixture(settings);
            var houses = Plan(fx);
            var areas = fx.World.Areas.Select(a => a.Shape.Outer).ToList();
            foreach (var h in houses)
            {
                var ring = h.Spec.Footprint.Outer;
                foreach (var b in fx.World.Buildings) Assert.IsFalse(Polygon2D.RingsOverlap(ring, b.Spec.Footprint.Outer), "osm " + b.Id);
                foreach (var other in houses)
                    if (other != h) Assert.IsFalse(Polygon2D.RingsOverlap(ring, other.Spec.Footprint.Outer), h.Id + " vs " + other.Id);
                foreach (var area in areas) Assert.IsFalse(Polygon2D.RingsOverlap(ring, area));
                Assert.IsFalse(fx.Context.OverlapsWater(ring), "water");
                Assert.IsFalse(fx.Context.NearKos(ring, settings.frontage.kosExclusionRadius - 1e-3f), "kos");
                foreach (var p in ring)
                    foreach (var chain in fx.Network.Chains)
                    {
                        float line = chain.HalfWidth + (chain.BaseProfile == EdgeProfile.None ? 0f : 0.8f) + 0.4f;
                        Assert.GreaterOrEqual(Polyline2D.DistanceTo(chain.Points, p), line - 0.02f, "behind the property line of " + chain.Id);
                    }
            }
        }

        [Test]
        public void NoHouses_OnTheCampusOrDrainFrontage()
        {
            var houses = Plan(new StreetFixture(settings));
            Assert.IsFalse(houses.Any(h => h.Centroid.y > 60f), "campus side of T1");
            Assert.IsFalse(houses.Any(h => h.Centroid.x < 80f && h.Centroid.y > 0f && h.Centroid.y < 20f), "R1 north frontage behind the drain");
        }

        [Test]
        public void Ids_AreNegativeUniqueAndStable()
        {
            var a = Plan(new StreetFixture(settings)).Select(h => h.Id).ToList();
            var b = Plan(new StreetFixture(settings, osm => osm.Way(4001, new[] { V(-300f, -600f), V(-200f, -600f) }, false, ("highway", "residential"))))
                .Select(h => h.Id).ToList();
            Assert.IsTrue(a.All(id => id < 0));
            Assert.AreEqual(a.Count, a.Distinct().Count());
            CollectionAssert.IsSubsetOf(a, b, "an unrelated road elsewhere does not renumber these houses");
            Assert.Greater(b.Count, a.Count);
        }

        [Test]
        public void Cap_StopsPlacementAndIsReported()
        {
            settings.infill.maxInfill = 3;
            var report = new GenerationReport();
            Assert.AreEqual(3, Plan(new StreetFixture(settings), report).Count);
            Assert.Greater(report.SkippedTotal(InfillPlanner.SkipCap), 0);
        }

        [Test]
        public void Disabled_PlacesNothing()
        {
            settings.infill.enabled = false;
            Assert.AreEqual(0, Plan(new StreetFixture(settings)).Count);
        }

        [Test]
        public void AcceptedHouses_AreRegisteredInTheContext()
        {
            var fx = new StreetFixture(settings);
            var houses = Plan(fx);
            Assert.AreEqual(houses.Count, fx.Context.Buildings.Count(b => b.IsInfill));
            Assert.IsTrue(houses.All(h => fx.Context.BuildingAt(h.Centroid) != null));
        }

        [Test]
        public void Houses_StayInsideTheWorld()
        {
            var bounds = new StreetFixture(settings).World.LocalBounds;
            var fx = new StreetFixture(settings, osm => osm.Way(4002, new[] { V(0f, bounds.yMax - 6f), V(100f, bounds.yMax - 6f) }, false, ("highway", "residential")));
            var houses = Plan(fx);
            foreach (var h in houses)
                foreach (var p in h.Spec.Footprint.Outer) Assert.IsTrue(bounds.Contains(p), "house " + h.Id);
            Assert.IsTrue(houses.Any(h => h.Centroid.y > bounds.yMax - 30f), "the south side of the edge road still fills");
        }
    }
}
```

- [ ] **Step 2: Jalankan, pastikan gagal**

Procedure R+T, `Group = "InfillPlannerTests"`. Expected: `COMPILE FAILED`.

- [ ] **Step 3: Implementasi**

`Assets/WorldGen/Editor/Builders/InfillPlanner.cs`:

```csharp
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>A made-up house filling an empty lot beside a road (spec street-frontage §8).</summary>
    public sealed class InfillHouse
    {
        public long Id;
        public BuildingSpec Spec;
        public Vector2 Centroid;
        /// <summary>Ruko style: front at the property line, two floors, flat roof, no wall in front.</summary>
        public bool Flush;

        public BuildingFeature AsBuildingFeature() => new BuildingFeature { Id = Id, Spec = Spec, Centroid = Centroid, Name = "Infill" };
    }

    /// <summary>Row houses along pass-1 Vacant frontage, inside the 15 m corridor only (spec street-frontage §8).</summary>
    public static class InfillPlanner
    {
        public const string SkipBlocked = "infill:blocked", SkipCap = "infill:cap";

        /// <summary>Stable negative id from the chain key, side and start of the house along the chain.</summary>
        public static long InfillId(long chainKey, int side, float s)
        {
            var inv = CultureInfo.InvariantCulture;
            ulong h = SplitMix64.Hash(chainKey, "infill:" + side.ToString(inv) + ":" + Mathf.RoundToInt(s * 10f).ToString(inv));
            return -(long)(h & 0x3FFF_FFFF_FFFF_FFFFUL) - 1;
        }

        public static List<InfillHouse> Plan(IReadOnlyList<FrontageInterval> pass1, RoadNetwork net, StreetContext ctx, WorldGenSettings settings,
            GenerationReport report)
        {
            var houses = new List<InfillHouse>();
            var inf = settings.infill;
            if (inf == null || !inf.enabled) return houses;
            var near = new Dictionary<RoadChain, float>();
            var candidates = pass1.Where(x => x.Kind == FrontageKind.Vacant && x.Length >= inf.minLotLength)
                .OrderBy(x => x.Chain.Key).ThenBy(x => x.Chain.Index).ThenBy(x => x.Side).ThenBy(x => x.S0).ToList();
            foreach (var x in candidates) PlaceAlong(x, net, ctx, settings, near, houses, report);
            return houses;
        }

        static void PlaceAlong(FrontageInterval x, RoadNetwork net, StreetContext ctx, WorldGenSettings settings, Dictionary<RoadChain, float> near,
            List<InfillHouse> houses, GenerationReport report)
        {
            var inf = settings.infill;
            var inv = CultureInfo.InvariantCulture;
            var rng = SplitMix64.For(x.Chain.Key, "infill:" + x.Side.ToString(inv) + ":" + Mathf.RoundToInt(x.S0 * 10f).ToString(inv));
            float cursor = x.S0;
            while (x.S1 - cursor >= inf.minWidth)
            {
                // Every roll happens for every slot, accepted or not, so one blocked house never shifts its neighbours.
                float remaining = x.S1 - cursor;
                float width = Mathf.Min(rng.Range(inf.minWidth, inf.maxWidth), remaining);
                if (remaining - width < inf.minWidth) width = remaining;
                bool flush = rng.NextDouble() < inf.flushShare;
                float setback = flush ? inf.flushInset : rng.Range(inf.minSetback, inf.maxSetback);
                float depth = rng.Range(inf.minDepth, inf.maxDepth);
                bool twoStorey = flush || rng.NextDouble() < inf.twoStoreyShare;
                float gap = rng.Range(inf.minGap, inf.maxGap);
                long id = InfillId(x.Chain.Key, x.Side, cursor);
                if (houses.Count >= inf.maxInfill)
                {
                    report?.Skip(SkipCap, id);
                }
                else
                {
                    var house = TryPlace(x, cursor, cursor + width, setback, depth, flush, twoStorey, id, net, ctx, settings, near);
                    if (house == null)
                    {
                        report?.Skip(SkipBlocked, id);
                    }
                    else
                    {
                        houses.Add(house);
                        ctx.AddBuilding(new StreetBuilding { Id = house.Id, Footprint = house.Spec.Footprint, Centroid = house.Centroid, IsInfill = true });
                    }
                }
                cursor += width + gap;
            }
        }

        static InfillHouse TryPlace(FrontageInterval x, float a, float b, float setback, float depth, bool flush, bool twoStorey, long id,
            RoadNetwork net, StreetContext ctx, WorldGenSettings settings, Dictionary<RoadChain, float> near)
        {
            var f = settings.frontage;
            var inf = settings.infill;
            var chain = x.Chain;
            float offset = FrontagePlanner.LineOffset(chain, x.Profile, settings.roadEdges, f);
            Vector2 pa = chain.SidePoint(a, x.Side, offset), pb = chain.SidePoint(b, x.Side, offset);
            Vector2 u = pb - pa;
            float width = u.magnitude;
            if (width < 1e-3f) return null;
            u /= width;
            Vector2 outward = Polyline2D.LeftNormal(u) * x.Side;
            Vector2 front = pa + outward * setback;
            float maxDepth = Mathf.Min(depth, f.corridorDepth - setback);
            var depths = maxDepth > inf.minDepth + 1e-3f ? new[] { maxDepth, inf.minDepth } : new[] { maxDepth };
            float c = inf.clearance;
            foreach (float d in depths)
            {
                var ring = Rect(front, u, outward, width, d);
                var grown = Rect(front - u * c - outward * c, u, outward, width + 2f * c, d + 2f * c);
                if (!Fits(ring, grown, chain, offset, net, ctx, f, near)) continue;
                int levels = twoStorey ? 2 : 1;
                var spec = new BuildingSpec
                {
                    Id = id, Footprint = new Polygon(ring), Levels = levels, LevelHeight = settings.levelHeight,
                    WallHeight = Mathf.Max(levels * settings.levelHeight, settings.minWallHeight),
                    AllowGable = !flush, RoofOverride = flush ? RoofOverride.Flat : RoofOverride.Auto
                };
                return new InfillHouse { Id = id, Spec = spec, Centroid = Polygon2D.Centroid(ring), Flush = flush };
            }
            return null;
        }

        static List<Vector2> Rect(Vector2 front, Vector2 u, Vector2 outward, float width, float depth)
        {
            var ring = new List<Vector2> { front, front + u * width, front + u * width + outward * depth, front + outward * depth };
            Polygon2D.EnsureWinding(ring, true);
            return ring;
        }

        static bool Fits(List<Vector2> ring, List<Vector2> grown, RoadChain own, float ownOffset, RoadNetwork net, StreetContext ctx,
            FrontageSettings f, Dictionary<RoadChain, float> near)
        {
            foreach (var p in ring)
                if (!ctx.InBounds(p)) return false;
            if (ctx.OverlapsBuilding(grown) || ctx.OverlapsWater(grown) || ctx.OverlapsArea(grown) || ctx.NearKos(ring, f.kosExclusionRadius)) return false;
            foreach (var p in Boundary(ring, 0.5f))
            {
                net.Nearby(p, net.MaxReach + f.propertyLineGap, null, near);
                foreach (var kv in near)
                {
                    float limit = kv.Key == own ? ownOffset - 0.01f : kv.Key.HalfWidth + kv.Key.MaxEdgeWidth + f.propertyLineGap;
                    if (kv.Value < limit) return false;
                }
            }
            return true;
        }

        static IEnumerable<Vector2> Boundary(List<Vector2> ring, float step)
        {
            for (int i = 0; i < ring.Count; i++)
            {
                Vector2 a = ring[i], b = ring[(i + 1) % ring.Count];
                int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / step));
                for (int k = 0; k < n; k++) yield return Vector2.Lerp(a, b, k / (float)n);
            }
        }
    }
}
```

- [ ] **Step 4: Jalankan, pastikan lulus**

Procedure R+T, `Group = "InfillPlannerTests|FrontagePlannerTests"`. Expected: `failed=0`.

- [ ] **Step 5: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add Assets/WorldGen/Editor/Builders/InfillPlanner.cs Assets/WorldGen/Editor/Builders/InfillPlanner.cs.meta Assets/WorldGen/Tests/InfillPlannerTests.cs Assets/WorldGen/Tests/InfillPlannerTests.cs.meta
git -C "D:/UNITY/project/Tyco World" commit -m "feat(worldgen): infill planner for houses on empty roadside lots

Co-Authored-By: <model> <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

---

### Task 15: Infill di pipeline (pass 1 → infill → pass 2)

**Files:**
- Modify: `Assets/WorldGen/Editor/Pipeline/WorldMeshPipeline.cs`:
  - `BuiltWorld.Infill`;
  - `Build`;
  - `AddBuildings` (refaktor material ke method baru `BuildingMaterials`);
  - method baru `AddInfill`.
- Test: `Assets/WorldGen/Tests/WorldMeshPipelineTests.cs`

**Interfaces:**
- Consumes: `InfillPlanner.Plan`, `InfillHouse.AsBuildingFeature`, `BuildingMeshBuilder.Build`, `MeshData.Append`, `ChunkFeatureId`, `PropPlacer.AddBuildings`.
- Produces:
  - `BuiltWorld.Infill : List<InfillHouse>`
  - Per chunk ada satu fitur `Buildings/Chunk_x_z/Infill`:
    - Kind `Building`, Name `"Infill"`, Category `"Infill"`, Tags `generated=infill`, `HasOsmIdentity = true`;
    - materialnya gabungan dari semua rumah di chunk itu.
  - Counter report: `"Infill"` (jumlah rumah), skip `infill:blocked` dan `infill:cap`.

- [ ] **Step 1: Tulis tes yang gagal**

Di `WorldMeshPipelineTests`:

```csharp
        [Test]
        public void Street_InfillIsOneObjectPerChunk()
        {
            var report = new GenerationReport();
            var built = BuildStreet(report);
            var infill = built.Features.Where(f => f.Kind == FeatureKind.Building && f.Name == "Infill").ToList();
            Assert.Greater(infill.Count, 0);
            Assert.AreEqual(infill.Count, infill.Select(f => f.Chunk).Distinct().Count());
            foreach (var f in infill)
            {
                Assert.AreEqual(FeatureGroups.Buildings, f.Group);
                Assert.AreEqual("Infill", f.Category);
                Assert.IsTrue(f.HasOsmIdentity);
                Assert.IsTrue(f.AddCollider);
                Assert.Less(f.OsmId, 0);
                Assert.IsTrue(f.Tags.TryGetValue("generated", out var generated) && generated == "infill");
                Assert.AreEqual(f.Materials.Length, f.Mesh.SubmeshCount);
                Assert.IsFalse(f.Mesh.HasInvalidValues());
            }
            Assert.Greater(built.Infill.Count, 0);
            Assert.AreEqual(built.Infill.Count, report.GetCount("Infill"));
        }

        [Test]
        public void Street_InfillMaterialsFollowTheVariantRule()
        {
            var upper = Materials(3);
            settings.residentialMaterials = new BuildingMaterialSet
            {
                groundFloor = Materials(2), upper = upper, trim = Materials(3), flatRoof = Materials(1), gableRoof = Materials(1)
            };
            var built = BuildStreet(new GenerationReport());
            var byChunk = built.Features.Where(f => f.Kind == FeatureKind.Building && f.Name == "Infill").ToDictionary(f => f.Chunk);
            foreach (var h in built.Infill)
            {
                var feature = byChunk[WorldMeshPipeline.ChunkOf(h.Centroid, built.ChunkSize)];
                CollectionAssert.Contains(feature.Materials, upper[SplitMix64.Pick(h.Id, "upper", 3)]);
            }
        }

        [Test]
        public void Street_WallsStandInFrontOfSetBackInfill()
        {
            var report = new GenerationReport();
            var built = BuildStreet(report);
            int setBack = built.Infill.Count(h => !h.Flush);
            Assert.Greater(setBack, 0);
            Assert.GreaterOrEqual(report.GetCount("Gates") + report.GetCount("Doors"), 2, "OSM house H1 plus at least one infill house");
        }
```

Procedure R+T, `Group = "WorldMeshPipelineTests"`. Expected: `COMPILE FAILED` (`BuiltWorld.Infill` belum ada).

- [ ] **Step 2: Implementasi**

`WorldMeshPipeline.cs`:

1. Di `BuiltWorld`, tambahkan:

```csharp
        public readonly List<InfillHouse> Infill = new List<InfillHouse>();
```

2. Di kelas `WorldMeshPipeline`, tambahkan field:

```csharp
        static readonly IReadOnlyDictionary<string, string> InfillTags = new Dictionary<string, string> { { "generated", "infill" } };
```

3. Di `AddBuildings`, ganti tujuh baris dari `var set = settings.MaterialSet(...)` sampai `feature.Materials[BuildingMeshBuilder.SubRoof] = ...` dengan:

```csharp
                feature.Materials = BuildingMaterials(settings.MaterialSet(b.Rule.materialSet), b.Id, info.Roof);
```

Lalu tambahkan method:

```csharp
        static Material[] BuildingMaterials(BuildingMaterialSet set, long id, RoofKind roof)
        {
            int upper = SplitMix64.Pick(id, "upper", Length(set.upper));
            var roofs = roof == RoofKind.Gable ? set.gableRoof : set.flatRoof;
            var materials = new Material[BuildingMeshBuilder.SubmeshCount];
            materials[BuildingMeshBuilder.SubGround] = At(set.groundFloor, SplitMix64.Pick(id, "ground", Length(set.groundFloor)));
            materials[BuildingMeshBuilder.SubUpper] = At(set.upper, upper);
            materials[BuildingMeshBuilder.SubTrim] = At(set.trim, upper);
            materials[BuildingMeshBuilder.SubRoof] = At(roofs, SplitMix64.Pick(id, "roof", Length(roofs)));
            return materials;
        }
```

4. Method `AddInfill`:

```csharp
        static void AddInfill(BuiltWorld built, List<InfillHouse> houses, WorldGenSettings settings, GenerationReport report)
        {
            built.Infill.AddRange(houses);
            var byChunk = new Dictionary<Vector2Int, List<InfillHouse>>();
            foreach (var h in houses)
            {
                var chunk = ChunkOf(h.Centroid, built.ChunkSize);
                if (!byChunk.TryGetValue(chunk, out var list)) byChunk[chunk] = list = new List<InfillHouse>();
                list.Add(h);
            }
            var chunks = new List<Vector2Int>(byChunk.Keys);
            chunks.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            var meshParams = settings.MeshParams;
            foreach (var chunk in chunks)
            {
                var list = byChunk[chunk];
                list.Sort((a, b) => a.Id.CompareTo(b.Id));
                var materials = new List<Material>();
                var parts = new List<(MeshData mesh, int[] map)>();
                foreach (var h in list)
                {
                    var mesh = BuildingMeshBuilder.Build(h.Spec, meshParams, out var info);
                    if (info.RoofFailed) report.Skip(SkipReasons.RoofTriangulationFailed, h.Id);
                    var picked = BuildingMaterials(settings.residentialMaterials, h.Id, info.Roof);
                    var map = new int[BuildingMeshBuilder.SubmeshCount];
                    for (int s = 0; s < map.Length; s++)
                    {
                        int index = materials.IndexOf(picked[s]);
                        if (index < 0)
                        {
                            index = materials.Count;
                            materials.Add(picked[s]);
                        }
                        map[s] = index;
                    }
                    parts.Add((mesh, map));
                }
                var merged = new MeshData(Mathf.Max(1, materials.Count));
                foreach (var (mesh, map) in parts) merged.Append(mesh, map);
                if (materials.Count == 0) materials.Add(null);
                built.Features.Add(new BuiltFeature
                {
                    Kind = FeatureKind.Building, OsmId = ChunkFeatureId(FeatureGroups.Buildings, "Infill", chunk), ElementType = OsmElementType.Way,
                    Name = "Infill", Group = FeatureGroups.Buildings, Chunk = chunk,
                    Anchor = new Vector2((chunk.x + 0.5f) * built.ChunkSize, (chunk.y + 0.5f) * built.ChunkSize),
                    Mesh = merged, Materials = materials.ToArray(), AddCollider = true, Category = "Infill", Tags = InfillTags
                });
                report.Count("Infill", list.Count);
            }
        }
```

5. Di `Build`, ganti baris `var walls = AddFrontage(...)` (dari Task 13) dengan:

```csharp
            var houses = InfillPlanner.Plan(FrontagePlanner.Plan(network, street, settings), network, street, settings, report);
            AddInfill(built, houses, settings, report);
            var walls = AddFrontage(built, FrontagePlanner.Plan(network, street, settings), settings, report);
```

Lalu setelah `placer.AddWalls(walls);`:

```csharp
            placer.AddBuildings(houses.Select(h => h.AsBuildingFeature()));
```

Catatan:
- `FrontagePlanner.Plan` dipanggil dua kali, dan itu disengaja. Pass kedua melihat rumah infill yang sudah ditambahkan ke `street`, jadi rumah flush menjadi Flush (tanpa tembok) dan rumah setback menjadi Lot (tembok + gerbang).
- `SurfaceRules.For` untuk infill memberi `Default` (Kind Building), sama seperti bangunan biasa.

- [ ] **Step 3: Jalankan semua tes**

Procedure R+T, `Group = "."`. Expected: `failed=0`, termasuk `Sukabirus_IsDeterministic`, `Sukabirus_NoInvalidGeometry`, `Synthetic_BuildingMaterialsFollowVariantRule`, dan `LongStraightRoad_WallCollidersStayUnder100m`.

- [ ] **Step 4: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add Assets/WorldGen/Editor/Pipeline/WorldMeshPipeline.cs Assets/WorldGen/Tests/WorldMeshPipelineTests.cs
git -C "D:/UNITY/project/Tyco World" commit -m "feat(worldgen): infill houses merged per chunk, frontage second pass

Co-Authored-By: <model> <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

---

### Task 16: Verifikasi integrasi, catatan, dan pembaruan spec #1

**Files:**
- Update: `docs/superpowers/notes/2026-10-14-street-frontage-verification.md`
- Modify: `docs/superpowers/specs/2026-10-13-osm-world-generator-design.md` (§2, §7, §8, §11, §12, §15, §18)

**Interfaces:**
- Consumes: semua Procedure, `McpVerify.WriteChunkHashes/CompareChunkHashes`, `McpPlayerDriver` (Install, TeleportTo, Face, Hold, Status), `McpVehicleDriver` (Install, PlaceAt, StandBeside, OpenGate, Hold, Status, Release), `McpRender.SavePng`.
- Produces: bukti kriteria selesai spec street-frontage §15.

- [ ] **Step 1: Semua tes**

Procedure R+T, `Group = "."`. Expected: `failed=0`. Catat jumlah passed.

- [ ] **Step 2: Generate + determinisme**

1. Jalankan Procedure G. Catat Summary lengkap (Features, Skipped `infill:*`, Stages, total). Total harus < 60000 ms.
2. Jalankan `McpVerify.WriteChunkHashes(".../hashes-after.txt")`.
3. Jalankan Procedure G sekali lagi.
4. Jalankan `McpVerify.CompareChunkHashes(".../hashes-after.txt")`. Expected: `IDENTICAL chunks=…`.

- [ ] **Step 3: Raycast dan kos (edit mode)**

```csharp
using System.Globalization;
using System.Text;
using UnityEngine;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        // 1) 20 fixed probes spread over the world: the highest walkable surface must be at most 0.25 m.
        float worst = float.NegativeInfinity;
        for (int i = 0; i < 20; i++)
        {
            float x = -900f + (i % 5) * 320f + (i * 37f % 50f), z = -1300f + (i / 5) * 450f + (i * 53f % 60f);
            float top = float.NegativeInfinity;
            foreach (var hit in Physics.RaycastAll(new Vector3(x, 300f, z), Vector3.down, 600f))
            {
                bool walkable = hit.collider.GetComponentInParent<TycoWorld.WorldGen.GroundSurface>() != null || hit.collider.name == "Ground";
                if (walkable && hit.point.y < 5f && hit.point.y > top) top = hit.point.y;
            }
            worst = Mathf.Max(worst, top);
        }
        sb.Append("worstWalkableY=").Append(worst.ToString("F3", inv));

        // 2) Around the kos gate the gutter (Concrete) is reachable.
        var gate = Object.FindAnyObjectByType<TycoWorld.Player.GateInteractable>();
        int concrete = 0;
        for (float dx = -2f; dx <= 2f; dx += 0.25f)
            for (float dz = -2f; dz <= 2f; dz += 0.25f)
            {
                var o = gate.transform.position + new Vector3(dx, 3f, dz);
                if (!Physics.Raycast(o, Vector3.down, out var h, 5f)) continue;
                var s = h.collider.GetComponentInParent<TycoWorld.WorldGen.GroundSurface>();
                if (s != null && s.Type == TycoWorld.WorldGen.SurfaceType.Concrete && h.collider.transform.parent != null &&
                    h.collider.transform.parent.parent != null && h.collider.transform.parent.parent.name == "RoadEdges") concrete++;
            }
        sb.Append(" kosGutterHits=").Append(concrete);

        // 3) Nothing generated (walls, infill) intrudes into the kos front yard.
        var yard = GameObject.Find("KosFrontyard");
        var b = new Bounds(yard.transform.position, Vector3.zero);
        foreach (var r in yard.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
        int intruders = 0;
        foreach (var c in Physics.OverlapBox(b.center, b.extents * 0.95f))
        {
            var t = c.transform;
            bool generated = t.name == "Infill" || (t.parent != null && t.parent.parent != null && t.parent.parent.name == "Frontage");
            if (generated) { intruders++; sb.Append(" intruder=").Append(t.name); }
        }
        sb.Append(" kosYardIntruders=").Append(intruders);
        result.Log(sb.ToString());
    }
}
```

Expected:
- `worstWalkableY` ≤ 0.250;
- `kosGutterHits` > 0;
- `kosYardIntruders=0`.

Nama yang dipakai sudah dicek saat plan ditulis: `GroundSurface.Type` (`Assets/WorldGen/Runtime/GroundSurface.cs`), `TycoWorld.Player.GateInteractable`, dan root `KosFrontyard` (`PlayerSceneSetup.FrontyardName`).

- [ ] **Step 4: Trotoar sisi kampus Jl. Sukabirus**

```csharp
using System.Globalization;
using UnityEngine;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var inv = CultureInfo.InvariantCulture;
        var target = new Vector3(-158f, 0f, -218f);   // start of Jalan Sukabirus way 95529338, beside the campus
        MeshFilter best = null;
        float bestDistance = float.MaxValue;
        foreach (var mf in Object.FindObjectsByType<MeshFilter>())
        {
            if (mf.name != "Sidewalk" || mf.transform.parent == null || mf.transform.parent.parent == null || mf.transform.parent.parent.name != "RoadEdges") continue;
            float d = Vector3.Distance(mf.GetComponent<Renderer>().bounds.center, target);
            if (d < bestDistance) { bestDistance = d; best = mf; }
        }
        if (best == null) { result.LogError("NO SIDEWALK"); return; }
        var mesh = best.sharedMesh;
        var tris = mesh.GetTriangles(0);
        var v = mesh.vertices;
        var p = (v[tris[0]] + v[tris[1]] + v[tris[2]]) / 3f;
        Physics.Raycast(p + Vector3.up, Vector3.down, out var hit, 2f);
        var surface = hit.collider != null ? hit.collider.GetComponentInParent<TycoWorld.WorldGen.GroundSurface>() : null;
        result.Log("sidewalkChunk=" + best.transform.parent.name + " dist=" + bestDistance.ToString("F0", inv) + " hitY=" + hit.point.y.ToString("F3", inv) +
                   " surface=" + (surface != null ? surface.Type.ToString() : "-"));
    }
}
```

Expected: `hitY` ≈ 0.210 dan `surface=Paving`. `dist` dipakai untuk memastikan chunk-nya memang di dekat Sukabirus; jarak < 150 m sudah cukup.

- [ ] **Step 5: Tampak atas 3 chunk simpang (edit mode)**

```csharp
using UnityEngine;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        string dir = "D:/UNITY/project/Tyco World/.superpowers/sdd/2026-10-14-street-frontage/shots/after";
        var spots = new[] { new Vector2(-194.6f, -632.7f), new Vector2(-321.2f, -612.6f), new Vector2(-60f, 400f) };
        var go = new GameObject("TopDownCam") { hideFlags = HideFlags.HideAndDontSave };
        var light = new GameObject("TopDownSun") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 50f;
            cam.farClipPlane = 600f;
            var sun = light.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(60f, -30f, 0f);
            for (int i = 0; i < spots.Length; i++)
            {
                go.transform.SetPositionAndRotation(new Vector3(spots[i].x, 300f, spots[i].y), Quaternion.Euler(90f, 0f, 0f));
                result.Log(TycoWorld.Player.Tests.McpRender.SavePng(cam, dir + "/top_" + i + ".png", 1024, 1024));
            }
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(light);
        }
    }
}
```

Lihat ketiga PNG. Yang dicari:
- tepi jalan menyambung;
- mulut simpang terbuka;
- tidak ada tembok yang melintang di badan jalan.

- [ ] **Step 6: Screenshot dan perf "after"**

1. Masuk Play (Procedure P).
2. Ambil screenshot Procedure S untuk lima titik di jam 10 dan 21, ke `shots/after/`.
3. Jalankan Procedure Perf untuk `kos`/`road`/`high` di jam 10 dan 21.
4. Bandingkan dengan tabel before:
   - batches ≤ 1.15 × before;
   - fps ≥ 0.9 × before.

- [ ] **Step 7: Motor melewati kerb dan player keluar gerbang kos (Play)**

**Motor.** Cari muka kerb trotoar di Jl. Raya Bojongsoang (dekat (185, -224)), lalu taruh motor 4 m di depannya, menghadap kerb. Masih dalam Play, jalankan:

```csharp
using System.Globalization;
using UnityEngine;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var target = new Vector3(185f, 0f, -224f);
        MeshFilter best = null;
        float bestDistance = float.MaxValue;
        foreach (var mf in Object.FindObjectsByType<MeshFilter>())
        {
            if (mf.name != "Sidewalk") continue;
            float d = Vector3.Distance(mf.GetComponent<Renderer>().bounds.center, target);
            if (d < bestDistance) { bestDistance = d; best = mf; }
        }
        var mesh = best.sharedMesh;
        var v = mesh.vertices;
        var tris = mesh.GetTriangles(1);   // kerb submesh (top strip + road-facing face)
        for (int t = 0; t < tris.Length; t += 3)
        {
            Vector3 a = v[tris[t]], b = v[tris[t + 1]], c = v[tris[t + 2]];
            var n = Vector3.Cross(b - a, c - a).normalized;
            if (Mathf.Abs(n.y) > 0.1f) continue;   // the vertical kerb face; its normal points at the road
            var centre = (a + b + c) / 3f;
            var start = centre + new Vector3(n.x, 0f, n.z).normalized * 4f;
            float yaw = Mathf.Atan2(-n.x, -n.z) * Mathf.Rad2Deg;
            result.Log(TycoWorld.Vehicles.Tests.McpVehicleDriver.Install());
            result.Log(TycoWorld.Vehicles.Tests.McpVehicleDriver.PlaceAt(start.x, start.z, yaw));
            result.Log("kerb=" + centre.ToString("F2") + " yaw=" + yaw.ToString("F1", CultureInfo.InvariantCulture));
            return;
        }
        result.LogError("NO KERB FACE");
    }
}
```

Lalu naikkan player ke motor dengan urutan yang sama seperti di `docs/superpowers/notes/2026-10-14-motorbike-verification.md`:
1. `McpPlayerDriver.Install()`;
2. `McpVehicleDriver.StandBeside()`;
3. `McpPlayerDriver.Face(<pos motor + 0.8 m>)`;
4. `McpPlayerDriver.Press(true, false, false, false)`;
5. `McpVehicleDriver.Status()` harus menunjukkan `mounted=True`.

Setelah itu jalankan `McpVehicleDriver.Hold(1f, 0f, false, 2.5f)`. Tunggu ±3 s, lalu panggil `McpVehicleDriver.Status()`.

Expected: `bumps=0`, dan `pos` y ≈ 0.21 (motor di atas trotoar) atau motor sudah melewati trotoar. Setelah selesai, panggil `McpVehicleDriver.Release()`.

**Player.** Pindahkan player ke spawn kos lalu jalankan:
1. `McpPlayerDriver.TeleportTo(-321.16f, 1.77f, -612.64f, 180.8f)`, lalu `McpVehicleDriver.OpenGate()`.
2. `McpPlayerDriver.Hold(0f, 1f, false, 4f)`. Tunggu ±4 s, lalu panggil `McpPlayerDriver.Status()`.

Expected: player sudah keluar gerbang. `surface=` sempat bernilai `Concrete` (bahu/selokan) atau `Asphalt`, dan tidak tersangkut tembok baru.

Keluar Play (Procedure P).

- [ ] **Step 8: Catatan verifikasi akhir**

Tambahkan ke `docs/superpowers/notes/2026-10-14-street-frontage-verification.md`:

```markdown
## After (feature/street-frontage, Task 16)

- Tes: passed=<n> failed=0
- Generate: <ms> ms (< 60 s); Features: <salin>; Infill=<n>; Skipped infill:blocked=<n> infill:cap=<n>
- Determinisme: generate 2× → `IDENTICAL chunks=<n>`
- Raycast: worstWalkableY=<y> (≤ 0.25); kosGutterHits=<n>; kosYardIntruders=0; trotoar Sukabirus hitY=<y> surface=Paving
- Tampak atas: `shots/after/top_{0,1,2}.png` — <pengamatan>
- Motor di kerb Jl. Raya Bojongsoang: bumps=0, <hasil>; player keluar gerbang kos: <hasil>

| Titik | Jam | fps before | fps after | batches before | batches after | lulus? |
|---|---|---|---|---|---|---|
| kos | 10 | … | … | … | … | … |
| road | 10 | … | … | … | … | … |
| high | 10 | … | … | … | … | … |
| kos | 21 | … | … | … | … | … |
| road | 21 | … | … | … | … | … |
| high | 21 | … | … | … | … | … |

Kriteria: batches ≤ +15%, fps ≥ 90% dari before.

## QA manual (user)
- [ ] Jalan kaki dan naik motor di Jl. Sukabirus, gang dekat kos, dan Jl. Raya Bojongsoang; siang dan malam.
- [ ] Cari z-fighting atau celah di simpang, tembok yang melintang, dan rumah infill yang aneh.
```

Kalau ada kriteria yang tidak lulus, tulis apa adanya, termasuk angkanya, lalu laporkan ke controller. Jangan mengubah angka settings untuk "meluluskan".

- [ ] **Step 9: Perbarui spec world generator (#1)**

Di `docs/superpowers/specs/2026-10-13-osm-world-generator-design.md`:
- **§2 Tidak termasuk:**
  - Ganti butir "Mesh simpang khusus, trotoar, selokan, marka jalan." dengan "Mesh simpang khusus (trotoar, selokan, dan marka tengah kini dibuat oleh spec `2026-10-14-street-frontage-design.md`)."
  - Ganti butir "Pagar/tembok (data OSM kosong untuk area ini)." dengan "Pagar/tembok dari data OSM (data kosong); tembok prosedural dibuat oleh spec street-frontage."
- **§7 Lapisan ketinggian:** tambahkan baris `Dasar tepi jalan (bahu, kanal, kaki kerb) | 0.055`, `Bibir selokan | 0.155`, `Atas trotoar | 0.210`, dan `Marka | y jalan + 0.003`. Tambahkan catatan: "Detail di spec street-frontage §5."
- **§8 Lampu jalan:** tambahkan kalimat "Offset mengikuti profil tepi: trotoar hw + 0.45, selokan hw + 0.15 (di bahu), tanpa tepi hw + 0.6; lampu menjauhi tembok 0.35 m (spec street-frontage §9)."
- **§8 Pohon:** tambahkan butir "jarak ke garis tengah jalan < hw + max(1, lebar tepi + 0.5), atau < 0.8 m dari tembok."
- **§11 Hierarki:** tambahkan `RoadEdges/Chunk_x_z/{Gutter,Sidewalk}`, `Frontage/Chunk_x_z/Walls`, dan `Buildings/Chunk_x_z/Infill`.
- **§12 Settings:** tambahkan butir "`roadEdges`, `frontage`, `infill` — lihat spec street-frontage §12."
- **§15 langkah 4:** ganti "y ≤ 0.1" menjadi "y ≤ 0.25 (trotoar 0.21)".
- **§18:** tandai "selokan di tepi jalan, trotoar, marka jalan" sebagai selesai (spec street-frontage). "Parit/sungai dengan kedalaman" dan "mesh simpang" tetap terbuka.

- [ ] **Step 10: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add docs/superpowers/notes/2026-10-14-street-frontage-verification.md docs/superpowers/specs/2026-10-13-osm-world-generator-design.md
git -C "D:/UNITY/project/Tyco World" commit -m "docs(worldgen): street frontage verification and world spec update

Co-Authored-By: <model> <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

---

## Tes otomatis (ringkasan)

| Kelas | Task | Isi pokok |
|---|---|---|
| `DefaultSettingsTests` | 1, 9, 12 | Nilai §12, porsi gaya tembok, asset tersimpan beserta materialnya |
| `StreetGeometryTests` | 2 | Polyline, offset/miter/bevel, ray, bisection, box, atlas UV, append, overlap |
| `RoadNetworkTests` | 3 | Chain, profil dasar, sisi kampus, smoothing, prioritas, query jarak |
| `RoadEdgeBuilderTests` | 4 | Lapisan y, mulut simpang, potongan bangunan/air, sisi kampus, tikungan 90°, batas dunia |
| `RoadMarkingBuilderTests` | 5 | Pola dash, simpang, jalan yang tidak bermarka, dash lintas way |
| `WorldMeshPipelineTests` | 5, 6, 13, 15 | Surface aspal, objek chunk, determinisme, collider ≤ 100 m, tembok, infill |
| `WorldChunkIndexTests`, `SurfaceRulesTests` | 6 | Kategori cull dan surface tepi |
| `StreetPropTests` | 7 | Offset lampu per profil, pohon dan lampu menjauhi tepi dan tembok |
| `FirstPersonPlayerTests`, `MotorbikeTests` | 8 | Kerb bisa dilewati |
| `ArtTests` | 9, 12 | Kerb, atlas, jeruji, material alpha clip |
| `FrontagePlannerTests` | 10 | Lot dan gerbang, flush, kampus, taman/air, kos, mulut, rumah menyerong, jalan sejajar, tikungan |
| `FrontageMeshBuilderTests` | 11 | Tinggi, band UV, gerbang dan collider, plat, pagar, pilaster bersama, anggaran segitiga |
| `InfillPlannerTests` | 14 | Ukuran, tanpa overlap, kampus/air/kos, id stabil, cap, batas dunia |
