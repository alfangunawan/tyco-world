# Jalan Bersih dan Pembatas Jalan Buntu — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rumah tidak lagi berdiri di badan jalan, jalan yang menembus gedung bernama atau kampus dipotong di depan dindingnya, dan setiap ujung jalan buntu punya pembatas padat:
- tembok rendah + pot di gang;
- portal palang di jalan servis;
- beton bergaris + plang "JALAN DITUTUP" di batas peta.

**Architecture:**
- Dua langkah murni baru di WorldGen:
  - `RoadClearance` berjalan di akhir `FeatureClassifier.Classify`. Hasilnya ikut dipakai oleh dunia, bake `KampungData` (peta HP, GPS, jalur warga), dan perencana kos.
  - `DeadEndPlanner` membaca `RoadNetwork` di dalam `WorldMeshPipeline.Build`.
- `BarrierBuilder` mengubah titik buntu menjadi dua bagian:
  - mesh chunk prosedural (tembok, portal, collider) di grup `Barriers`;
  - penempatan prefab (pot, beton, plang) di grup `Props/Barriers`.
- Prefab dibuat dari model CC0 yang diunduh (Kenney, Quaternius via Poly Pizza).

**Tech Stack:** Unity 6000.6.5f1, URP 17.6, C# 9, Unity Test Framework, Unity MCP, Python (`-I`) untuk konversi GLB→OBJ.

**Spec:** `docs/superpowers/specs/2026-10-15-road-clearance-dead-ends-design.md`

## Global Constraints

**Proyek dan branch**
- Root `D:\UNITY\project\Tyco World`, branch `feature/kampung-life` (PR #8). Spec ada di commit `0a848de`.

**Assembly**
- WorldGen editor: namespace `TycoWorld.WorldGen.Authoring`.
- WorldGen runtime: `TycoWorld.WorldGen`.
- Tes: `TycoWorld.WorldGen.Tests`.
- Gameplay editor (`KampungBaker`): `TycoWorld.Gameplay.Authoring`.

**Aturan kode**
- C# 9.
- Angka diformat dengan `CultureInfo.InvariantCulture`.
- Jangan pakai `??` / `?.` pada `UnityEngine.Object`.
- Tidak ada instance-ID.

**Determinisme**
- Fitur diproses berurutan menurut `Id`, chain menurut `RoadChain.Id`.
- Tidak ada bilangan acak di RoadClearance maupun DeadEndPlanner.
- `Sukabirus_IsDeterministic` harus tetap lulus.

**Nilai**
- Semua angka hanya ditulis sebagai initializer field `RoadClearanceSettings` / `DeadEndSettings`, disalin dari spec:
  - jarak bersih = setengah lebar + 0,3 m;
  - geser maksimal 2,0 m, ditambah 0,05 m;
  - rumah < 300 m²;
  - batas peta < 20 m;
  - pembatas 0,3 m sebelum ujung;
  - lebar minimal 1,2 m;
  - tembok 0,9 m;
  - bangunan di depan ujung 2 m.

**Teks pemain:** `"JALAN DITUTUP"`.

**File proyek**
- Jangan stage `ProjectSettings/*`, `Assets/WorldGen/Materials/WorldGen_Fallback.mat`, `Assets/WorldGen/Generated/`, atau zip di root.
- Jangan pakai `git add -A` / `git add .`.
- Commit diakhiri:
  - `Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>`
  - `Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5`

**Unduhan**
- Simpan ke folder scratch sendiri dan baca hanya dengan `python -I`. Tidak ada yang dieksekusi.
- Yang masuk Assets hanya OBJ/MTL/PNG hasil konversi dan catatan lisensi.

## Prosedur standar

**Compile**
1. Pastikan `EditorApplication.isPlaying == false`. Kalau user sedang main, berhenti dan beri tahu.
2. `AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport)` lalu `CompilationPipeline.RequestScriptCompilation()`.

**Tes**
- Dalam panggilan terpisah, cek dulu bahwa tipe baru sudah ada.
- `TycoWorld.WorldGen.Tests.McpTestRunner.Run("TycoWorld", "<scratch>/gameplay/tests_<nama>.txt")`.
- Baris pertama hasil: `group=... passed=N failed=0`.

**Play** (hanya di Task 8)
- Backup `tyco.save` dulu.
- Selama Play pakai `SaveBackend = new MemorySaveBackend()`.
- Setelah keluar, verifikasi save sama dengan backup.

## Review Focus

1. **Ujung jalan yang menempel ke badan jalan lain (pertigaan T) bukan jalan buntu.** Ujung chain tepat di atas garis tengah atau di dalam badan jalan lain tidak diberi pembatas. Dipaku oleh `DeadEndPlannerTests.TJunction_IsNotADeadEnd` (Task 3).
2. **Rumah yang setelah digeser menumpuk rumah lain atau masuk jalan lain** dihapus, bukan dibiarkan bertumpuk. Dipaku oleh `RoadClearanceTests.ShiftThatHitsAnotherHouse_Drops` dan `HouseBetweenTwoRoads_Drops` (Task 2).
3. **Jalan melingkar (chain tertutup)** tidak punya ujung, jadi tidak ada pembatas. Dipaku oleh `DeadEndPlannerTests.ClosedLoop_HasNoEnds` (Task 3).
4. **Jalan yang menyusuri dinding gedung bernama (dekat, tapi tidak masuk) tetap utuh.** Hanya bagian di dalam footprint yang dipotong. Dipaku oleh `RoadClearanceTests.RoadAlongNamedBuilding_IsNotCut` (Task 2).
5. **Potongan jalan sangat pendek** (< 3 m) setelah dipotong dibuang, dan tidak diberi pembatas. Dipaku oleh `RoadClearanceTests.ShortStub_IsDropped` (Task 2).

---

## Peta file

| File | Tugas | Isi |
|---|---|---|
| `Assets/WorldGen/Editor/Settings/StreetDressingSettings.cs` | 1 | + `RoadClearanceSettings`, `DeadEndSettings` |
| `Assets/WorldGen/Editor/Settings/WorldGenSettings.cs` | 1 | + `clearance`, `deadEnds`, `barrierPrefabs` |
| `Assets/WorldGen/Editor/Settings/DefaultSettingsFactory.cs` | 1 | `Populate` mengisi kedua grup |
| `Assets/WorldGen/Editor/Builders/RoadClearance.cs` | 2 | baru, murni |
| `Assets/WorldGen/Editor/Classify/FeatureClassifier.cs` | 2 | panggil `RoadClearance.Apply` sebelum `return world` |
| `Assets/WorldGen/Editor/Builders/DeadEndPlanner.cs` | 3 | baru, murni |
| `Assets/WorldGen/Editor/Builders/BarrierBuilder.cs` | 4 | baru: mesh, collider, props, blockers |
| `Assets/WorldGen/Runtime/FeatureKind.cs` | 4 | + `Barrier` (di akhir) |
| `Assets/WorldGen/Editor/Pipeline/WorldMeshPipeline.cs` | 4 | `FeatureGroups.Barriers`, `BarrierPropsGroup`, `BuiltWorld.Barriers`, wiring, hash |
| `Assets/WorldGen/Editor/Pipeline/SceneObjectFactory.cs` | 4 | `PlaceProps` untuk pembatas |
| `Assets/WorldGen/Runtime/Culling/WorldChunkIndex.cs` | 4 | `Barriers`, `Props/Barriers` → Prop |
| `Assets/WorldGen/Art/ThirdParty/Barriers/*` | 5 | OBJ/MTL/PNG + `SOURCES.md` |
| `Assets/WorldGen/Editor/Art/BarrierArt.cs` | 5 | baru: prefab + tekstur garis |
| `Assets/WorldGen/Editor/Art/ArtAssetBuilder.cs` | 5 | material `Barrier_Stripe`, assign prefab |
| Tes | 1–7 | `DefaultSettingsTests`, `RoadClearanceTests`, `DeadEndPlannerTests`, `BarrierBuilderTests`, `WorldMeshPipelineTests`, `WorldChunkIndexTests`, `ArtTests`, `KampungBakerTests` |

---

### Task 1: Settings

**Files:**
- Modify: `Assets/WorldGen/Editor/Settings/StreetDressingSettings.cs` (tambah di akhir namespace)
- Modify: `Assets/WorldGen/Editor/Settings/WorldGenSettings.cs:62-65`
- Modify: `Assets/WorldGen/Editor/Settings/DefaultSettingsFactory.cs:123-125`
- Test: `Assets/WorldGen/Tests/DefaultSettingsTests.cs`

**Interfaces:**
- Produces:
  - `RoadClearanceSettings { bool enabled; float margin, maxShift, shiftExtra, houseMaxArea, sampleStep, minPieceLength; }`
  - `DeadEndSettings { bool enabled; float boundaryDistance, endInset, minWidth, joinMargin, buildingAhead, wallHeight, wallThickness, portalHeight, portalPost, blockSpacing; int wallBand; string[] portalHighways; Material stripe; }`
  - `WorldGenSettings.clearance`, `.deadEnds`, `.barrierPrefabs` (0 pot, 1 beton, 2 plang).

- [ ] **Step 1: Tes gagal**

```csharp
[Test]
public void Defaults_RoadClearanceAndDeadEnds_MatchTheSpec()
{
    var s = DefaultSettingsFactory.CreateInMemory();
    try
    {
        Assert.IsTrue(s.clearance.enabled);
        Assert.AreEqual(0.3f, s.clearance.margin);
        Assert.AreEqual(2.0f, s.clearance.maxShift);
        Assert.AreEqual(0.05f, s.clearance.shiftExtra);
        Assert.AreEqual(300f, s.clearance.houseMaxArea);
        Assert.AreEqual(3f, s.clearance.minPieceLength);
        Assert.IsTrue(s.deadEnds.enabled);
        Assert.AreEqual(20f, s.deadEnds.boundaryDistance);
        Assert.AreEqual(0.3f, s.deadEnds.endInset);
        Assert.AreEqual(1.2f, s.deadEnds.minWidth);
        Assert.AreEqual(0.9f, s.deadEnds.wallHeight);
        Assert.AreEqual(2f, s.deadEnds.buildingAhead);
        CollectionAssert.AreEquivalent(new[] { "service", "track" }, s.deadEnds.portalHighways);
        Assert.IsNotNull(s.barrierPrefabs);
    }
    finally { Object.DestroyImmediate(s); }
}

[Test]
public void SavedAsset_HasRoadClearanceAndDeadEnds()
{
    var s = AssetDatabase.LoadAssetAtPath<WorldGenSettings>(WorldGenPaths.SettingsAsset);
    Assume.That(s, Is.Not.Null);
    Assert.IsNotNull(s.clearance);
    Assert.IsNotNull(s.deadEnds);
    Assert.Greater(s.deadEnds.wallHeight, 0f, "an asset saved before this field existed gets the initializer defaults");
}
```

Pakai konstanta path settings yang sudah dipakai `SavedAsset_HasStreetDressing` di file yang sama.

- [ ] **Step 2:** Jalankan. Harus FAIL dengan error kompilasi `clearance` tidak ada.

- [ ] **Step 3: Implementasi** (di `StreetDressingSettings.cs`)

```csharp
/// <summary>Roads clear of buildings (spec road-clearance §2).</summary>
[Serializable]
public sealed class RoadClearanceSettings
{
    public bool enabled = true;
    /// <summary>Clear distance from a road's centreline = half its width + this.</summary>
    public float margin = 0.3f;
    /// <summary>A house further into the road than this is dropped, not moved.</summary>
    public float maxShift = 2.0f;
    public float shiftExtra = 0.05f;
    /// <summary>Unnamed, non-campus, non-key buildings smaller than this (m²) count as houses.</summary>
    public float houseMaxArea = 300f;
    /// <summary>Step along a road when finding where it enters a building.</summary>
    public float sampleStep = 0.5f;
    /// <summary>Road pieces left shorter than this after a cut are dropped.</summary>
    public float minPieceLength = 3f;
}

/// <summary>Barriers at dead ends (spec road-clearance §3).</summary>
[Serializable]
public sealed class DeadEndSettings
{
    public bool enabled = true;
    public float boundaryDistance = 20f;
    public float endInset = 0.3f;
    public float minWidth = 1.2f;
    /// <summary>An end within another road's half width + this is a junction, not a dead end.</summary>
    public float joinMargin = 0.5f;
    /// <summary>A building this close past the end already closes it.</summary>
    public float buildingAhead = 2f;
    public float wallHeight = 0.9f;
    public float wallThickness = 0.2f;
    public int wallBand = 1;
    public float portalHeight = 1.0f;
    public float portalPost = 0.1f;
    /// <summary>Spacing of the concrete blocks across a road at the map edge.</summary>
    public float blockSpacing = 1.4f;
    public string[] portalHighways = { "service", "track" };
    public Material stripe;
}
```

Di `WorldGenSettings.cs`, setelah `infill`:

```csharp
public RoadClearanceSettings clearance = new RoadClearanceSettings();
public DeadEndSettings deadEnds = new DeadEndSettings();
[Tooltip("Dead-end props: 0 potted plant, 1 concrete block, 2 road-closed barricade with its sign.")]
public GameObject[] barrierPrefabs = new GameObject[0];
```

Di `DefaultSettingsFactory.Populate`, mengikuti baris `infill`:

```csharp
s.clearance = new RoadClearanceSettings();
s.deadEnds = new DeadEndSettings();
```

- [ ] **Step 4:** Compile dan jalankan. PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/WorldGen/Editor/Settings/StreetDressingSettings.cs Assets/WorldGen/Editor/Settings/WorldGenSettings.cs Assets/WorldGen/Editor/Settings/DefaultSettingsFactory.cs Assets/WorldGen/Tests/DefaultSettingsTests.cs
git commit -m "feat(worldgen): settings for road clearance and dead-end barriers"
```

---

### Task 2: RoadClearance

**Files:**
- Create: `Assets/WorldGen/Editor/Builders/RoadClearance.cs`
- Modify: `Assets/WorldGen/Editor/Classify/FeatureClassifier.cs` (satu baris sebelum `return world;`, sekitar L89)
- Test: `Assets/WorldGen/Tests/RoadClearanceTests.cs`

**Interfaces:**
- Consumes: `ClassifiedWorld` (`Roads`, `Buildings`), `RoadFeature.Pieces` / `Width` / `Id`, `BuildingFeature.Spec.Footprint` (`Polygon`), `IsCampus`, `Key`, `Name`, `Centroid`, `Polygon2D.SegmentDistance` / `ContainsPoint` / `RingsOverlap`, `Polyline2D.Cumulative` / `Slice` / `Project` / `PointAt`.
- Produces:
  - `public static class RoadClearance`
  - `Apply(ClassifiedWorld world, RoadClearanceSettings s, GenerationReport report)`
  - `bool IsHouse(BuildingFeature b, RoadClearanceSettings s)`
  - konstanta report `Shifted="ClearanceShifted"`, `Dropped="ClearanceDropped"`, `RoadCuts="ClearanceRoadCuts"`.

- [ ] **Step 1: Tes gagal** (`RoadClearanceTests.cs`)

Dunia dibangun dengan `TestOsm` lalu `new FeatureClassifier(settings, report).Classify(osm.Data)`, persis seperti `StreetFixture`. Jalan residential lebarnya dari aturan default; tes membaca `road.Width` dari hasil classify.

```csharp
using System.Linq;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class RoadClearanceTests
    {
        WorldGenSettings settings;
        GenerationReport report;

        [SetUp] public void SetUp() { settings = DefaultSettingsFactory.CreateInMemory(); report = new GenerationReport(); }
        [TearDown] public void TearDown() => Object.DestroyImmediate(settings);

        ClassifiedWorld World(System.Action<TestOsm> build)
        {
            var osm = new TestOsm();
            build(osm);
            return new FeatureClassifier(settings, report).Classify(osm.Data);
        }

        static void Street(TestOsm osm) => osm.Way(1, new[] { new Vector2(-100f, 0f), new Vector2(100f, 0f) }, false, ("highway", "residential"));

        static float Clear(ClassifiedWorld w, RoadClearanceSettings s) => w.Roads[0].Width * 0.5f + s.margin;

        static float Gap(ClassifiedWorld w, long id) =>
            Mathf.Abs(w.Buildings.First(b => b.Id == id).Spec.Footprint.Outer.Min(p => Mathf.Abs(p.y)));

        [Test]
        public void HouseOneMetreIntoTheRoad_MovesBackOut()
        {
            var w = World(o => { Street(o); o.Rect(10, 0f, 3f, 8f, 8f, ("building", "house")); });
            Assert.IsTrue(w.Buildings.Any(b => b.Id == 10), "kept");
            Assert.GreaterOrEqual(Gap(w, 10), Clear(w, settings.clearance) - 1e-3f, "front wall now clear of the road");
            Assert.AreEqual(1, report.GetCount(RoadClearance.Shifted));
        }

        [Test]
        public void HouseThreeMetresIn_IsDropped()
        {
            var w = World(o => { Street(o); o.Rect(10, 0f, 1.5f, 8f, 8f, ("building", "house")); });
            Assert.IsFalse(w.Buildings.Any(b => b.Id == 10));
            Assert.AreEqual(1, report.GetCount(RoadClearance.Dropped));
        }

        [Test]
        public void HouseAcrossTheCentreline_IsDropped()
        {
            var w = World(o => { Street(o); o.Rect(10, 0f, 0f, 8f, 8f, ("building", "house")); });
            Assert.IsFalse(w.Buildings.Any(b => b.Id == 10));
        }

        [Test]
        public void HouseClearOfTheRoad_IsUntouched()
        {
            var w = World(o => { Street(o); o.Rect(10, 0f, 12f, 8f, 8f, ("building", "house")); });
            var b = w.Buildings.First(x => x.Id == 10);
            Assert.AreEqual(8f, b.Spec.Footprint.Outer.Min(p => p.y), 0.01f);
            Assert.AreEqual(0, report.GetCount(RoadClearance.Shifted));
        }

        [Test]
        public void ShiftThatHitsAnotherHouse_Drops()
        {
            var w = World(o =>
            {
                Street(o);
                o.Rect(10, 0f, 3f, 8f, 8f, ("building", "house"));
                o.Rect(11, 0f, 11.2f, 8f, 8f, ("building", "house"));
            });
            Assert.IsFalse(w.Buildings.Any(b => b.Id == 10), "moved back it would overlap house 11");
            Assert.IsTrue(w.Buildings.Any(b => b.Id == 11));
        }

        [Test]
        public void HouseBetweenTwoRoads_Drops()
        {
            var w = World(o =>
            {
                Street(o);
                o.Way(2, new[] { new Vector2(-100f, 9f), new Vector2(100f, 9f) }, false, ("highway", "residential"));
                o.Rect(10, 0f, 4.5f, 8f, 6f, ("building", "house"));
            });
            Assert.IsFalse(w.Buildings.Any(b => b.Id == 10), "no way out of both roads");
        }

        [Test]
        public void NamedBuilding_Stays_AndTheRoadIsCutShortOfItsWalls()
        {
            var w = World(o => { Street(o); o.Rect(20, 0f, 0f, 30f, 30f, ("building", "university"), ("name", "Gedung Uji")); });
            Assert.IsTrue(w.Buildings.Any(b => b.Id == 20));
            var pieces = w.Roads.Where(r => r.Id == 1).SelectMany(r => r.Pieces).ToList();
            Assert.AreEqual(2, pieces.Count, "one piece each side");
            float clear = Clear(w, settings.clearance);
            foreach (var piece in pieces)
                foreach (var p in piece.Points)
                    Assert.GreaterOrEqual(Mathf.Abs(p.x), 15f + clear - 0.6f, "stops a clear distance before the wall (sampling tolerance)");
            Assert.IsFalse(pieces[0].ExtendEnd && pieces[1].ExtendStart, "the cut ends are dead ends");
            Assert.AreEqual(1, report.GetCount(RoadClearance.RoadCuts));
        }

        [Test]
        public void RoadAlongNamedBuilding_IsNotCut()
        {
            var w = World(o => { Street(o); o.Rect(20, 0f, 15.8f, 30f, 30f, ("building", "university"), ("name", "Gedung Uji")); });
            Assert.AreEqual(1, w.Roads.First(r => r.Id == 1).Pieces.Count);
            Assert.AreEqual(0, report.GetCount(RoadClearance.RoadCuts));
        }

        [Test]
        public void ShortStub_IsDropped()
        {
            var w = World(o =>
            {
                o.Way(1, new[] { new Vector2(-2f, 0f), new Vector2(100f, 0f) }, false, ("highway", "residential"));
                o.Rect(20, 10f, 0f, 20f, 20f, ("building", "university"), ("name", "Gedung Uji"));
            });
            var pieces = w.Roads.First(r => r.Id == 1).Pieces;
            Assert.AreEqual(1, pieces.Count, "the 2 m stub before the wall is gone");
            Assert.Greater(pieces[0].Points[0].x, 20f);
        }

        [Test]
        public void KeyBuildings_AreNeverMoved()
        {
            settings.keyBuildings = new[] { new KeyBuilding { osmId = 10, label = "Kos Uji" } };
            var w = World(o => { Street(o); o.Rect(10, 0f, 3f, 8f, 8f, ("building", "house")); });
            Assert.AreEqual(-1f, w.Buildings.First(b => b.Id == 10).Spec.Footprint.Outer.Min(p => p.y), 0.01f);
        }

        [Test]
        public void SameWorldTwice_SameResult()
        {
            string Run()
            {
                report = new GenerationReport();
                var w = World(o => { Street(o); for (int i = 0; i < 6; i++) o.Rect(30 + i, -50f + i * 18f, 2.5f + 0.4f * i, 8f, 8f, ("building", "house")); });
                return string.Join(";", w.Buildings.OrderBy(b => b.Id).Select(b => b.Id + ":" + b.Spec.Footprint.Outer[0].ToString("F3")));
            }
            Assert.AreEqual(Run(), Run());
        }
    }
}
```

Catatan untuk implementer: `o.Rect(id, x, z, w, d, tags)` memakai titik pusat `(x, z)` dan ukuran `w × d`. Cek urutan argumen di `TestOsm.Rect` sebelum menjalankan tes. Kalau yang dimaksud ternyata sudut, sesuaikan angka tes sehingga:
- rumah "1 m masuk" berarti dinding depan 1 m di dalam jarak bersih;
- rumah "3 m masuk" berarti lebih dari `maxShift`.

Kos asli (way 409467778) memakai prefab dan `KeyBuilding`. Tes kunci di atas memaku bahwa `Key != null` tidak pernah digeser.

- [ ] **Step 2:** Jalankan. Harus FAIL dengan error kompilasi `RoadClearance` tidak ada.

- [ ] **Step 3: Implementasi** (`RoadClearance.cs`)

```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>
    /// Keeps roads clear of buildings (spec road-clearance §2). A plain house poking into a road is moved back out of
    /// it, or dropped when it sits too far in, across the centreline, between two roads, or onto another building once
    /// moved. A road running into a named, campus, key or large building is cut a clear distance short of its walls.
    /// Runs at the end of classification, so the world, the gameplay bake and the kos planner see the same roads and
    /// houses. Pure and deterministic: ids in order, no randomness.
    /// </summary>
    public static class RoadClearance
    {
        public const string Shifted = "ClearanceShifted", Dropped = "ClearanceDropped", RoadCuts = "ClearanceRoadCuts";

        public static void Apply(ClassifiedWorld world, RoadClearanceSettings s, GenerationReport report)
        {
            if (world == null || s == null || !s.enabled) return;
            CutRoads(world, s, report);
            MoveHouses(world, s, report);
        }

        public static bool IsHouse(BuildingFeature b, RoadClearanceSettings s) =>
            !b.IsCampus && b.Key == null && string.IsNullOrEmpty(b.Name) && b.Spec != null && b.Spec.Footprint != null &&
            b.Spec.Footprint.Area < s.houseMaxArea;

        static float Clear(RoadFeature r, RoadClearanceSettings s) => r.Width * 0.5f + s.margin;

        // ---------- roads through kept buildings ----------

        static void CutRoads(ClassifiedWorld world, RoadClearanceSettings s, GenerationReport report)
        {
            var solid = world.Buildings.Where(b => !IsHouse(b, s) && b.Spec != null && b.Spec.Footprint != null).OrderBy(b => b.Id).ToList();
            if (solid.Count == 0) return;
            foreach (var road in world.Roads.OrderBy(r => r.Id))
            {
                float clear = Clear(road, s);
                var kept = new List<LinePiece>();
                foreach (var piece in road.Pieces)
                {
                    var pts = piece.Points;
                    var cum = Polyline2D.Cumulative(pts);
                    float length = cum[cum.Length - 1];
                    var inside = Inside(pts, cum, length, solid, s.sampleStep);
                    if (inside.Count == 0)
                    {
                        kept.Add(piece);
                        continue;
                    }
                    report.Count(RoadCuts, inside.Count);
                    float start = 0f;
                    foreach (var (a, b) in inside)
                    {
                        Keep(pts, cum, start, a - clear, start <= 0f && piece.ExtendStart, false, s, kept);
                        start = b + clear;
                    }
                    Keep(pts, cum, start, length, false, piece.ExtendEnd, s, kept);
                }
                road.Pieces.Clear();
                road.Pieces.AddRange(kept);
            }
            world.Roads.RemoveAll(r => r.Pieces.Count == 0);
        }

        /// <summary>The stretches (arc length) of the line inside any of the buildings, merged, from samples every step.</summary>
        static List<(float, float)> Inside(List<Vector2> pts, float[] cum, float length, List<BuildingFeature> solid, float step)
        {
            var spans = new List<(float, float)>();
            step = Mathf.Max(step, 0.05f);
            float spanStart = -1f;
            for (float s = 0f; ; s += step)
            {
                bool last = s >= length;
                if (last) s = length;
                var p = Polyline2D.PointAt(pts, cum, s, out _);
                bool hit = false;
                foreach (var b in solid)
                    if (b.Spec.Footprint.Bounds.Contains(p) && b.Spec.Footprint.Contains(p)) { hit = true; break; }
                if (hit && spanStart < 0f) spanStart = Mathf.Max(0f, s - step * 0.5f);
                if (!hit && spanStart >= 0f)
                {
                    spans.Add((spanStart, s - step * 0.5f));
                    spanStart = -1f;
                }
                if (last) break;
            }
            if (spanStart >= 0f) spans.Add((spanStart, length));
            return spans;
        }

        static void Keep(List<Vector2> pts, float[] cum, float a, float b, bool extendStart, bool extendEnd, RoadClearanceSettings s,
            List<LinePiece> into)
        {
            if (b - a < s.minPieceLength) return;
            var slice = Polyline2D.Slice(pts, cum, a, b, null);
            if (slice.Count >= 2) into.Add(new LinePiece(slice, extendStart, extendEnd));
        }

        // ---------- houses ----------

        static void MoveHouses(ClassifiedWorld world, RoadClearanceSettings s, GenerationReport report)
        {
            var dropped = new HashSet<long>();
            foreach (var house in world.Buildings.Where(b => IsHouse(b, s)).OrderBy(b => b.Id).ToList())
            {
                var ring = house.Spec.Footprint.Outer;
                if (!Need(ring, world.Roads, s, out float need, out Vector2 away, out bool crossing)) continue;
                if (crossing || need > s.maxShift || away == Vector2.zero)
                {
                    dropped.Add(house.Id);
                    continue;
                }
                var offset = away * (need + s.shiftExtra);
                var moved = ring.Select(p => p + offset).ToList();
                bool clash = Need(moved, world.Roads, s, out _, out _, out _) ||
                             world.Buildings.Any(o => o != house && !dropped.Contains(o.Id) && o.Spec != null && o.Spec.Footprint != null &&
                                                      Polygon2D.RingsOverlap(moved, o.Spec.Footprint.Outer));
                if (clash)
                {
                    dropped.Add(house.Id);
                    continue;
                }
                Move(house, offset);
                report.Count(Shifted);
            }
            if (dropped.Count == 0) return;
            world.Buildings.RemoveAll(b => dropped.Contains(b.Id));
            report.Count(Dropped, dropped.Count);
        }

        /// <summary>
        /// How far the ring sits inside the clear distance of the worst road (and which way is out); true when it does.
        /// Two roads pushing opposite ways count as crossing (no way out).
        /// </summary>
        static bool Need(IReadOnlyList<Vector2> ring, List<RoadFeature> roads, RoadClearanceSettings s, out float need, out Vector2 away,
            out bool crossing)
        {
            need = 0f;
            away = Vector2.zero;
            crossing = false;
            var bounds = Polygon2D.Bounds(ring);
            var pushes = new List<Vector2>();
            foreach (var road in roads)
            {
                float clear = Clear(road, s);
                foreach (var piece in road.Pieces)
                {
                    float d = Closest(ring, piece.Points, bounds, clear, out var onRing, out var onLine, out bool cross);
                    if (cross)
                    {
                        crossing = true;
                        return true;
                    }
                    if (d >= clear) continue;
                    var dir = (onRing - onLine).sqrMagnitude > 1e-8f ? (onRing - onLine).normalized : Vector2.zero;
                    pushes.Add(dir);
                    if (clear - d > need)
                    {
                        need = clear - d;
                        away = dir;
                    }
                }
            }
            for (int i = 0; i < pushes.Count; i++)
                for (int j = i + 1; j < pushes.Count; j++)
                    if (Vector2.Dot(pushes[i], pushes[j]) < 0f) crossing = true;
            return pushes.Count > 0;
        }

        /// <summary>Distance between the ring and the line (closest points out); cross when they intersect or the line runs inside.</summary>
        static float Closest(IReadOnlyList<Vector2> ring, List<Vector2> line, Rect ringBounds, float reach, out Vector2 onRing,
            out Vector2 onLine, out bool cross)
        {
            onRing = onLine = Vector2.zero;
            cross = false;
            float best = float.PositiveInfinity;
            var grown = new Rect(ringBounds.xMin - reach, ringBounds.yMin - reach, ringBounds.width + 2f * reach, ringBounds.height + 2f * reach);
            for (int j = 1; j < line.Count; j++)
            {
                Vector2 c = line[j - 1], d = line[j];
                if (!grown.Overlaps(Polygon2D.Bounds(new[] { c, d }))) continue;
                if (Polygon2D.ContainsPoint(ring, c) || Polygon2D.ContainsPoint(ring, d))
                {
                    cross = true;
                    return 0f;
                }
                for (int i = 0; i < ring.Count; i++)
                {
                    Vector2 a = ring[i], b = ring[(i + 1) % ring.Count];
                    if (Polygon2D.SegmentDistance(a, b, c, d) <= 0f)
                    {
                        cross = true;
                        return 0f;
                    }
                    Pair(a, b, c, d, ref best, ref onRing, ref onLine);
                }
            }
            return best;
        }

        /// <summary>Closest points of two non-crossing segments: the nearest endpoint of either onto the other.</summary>
        static void Pair(Vector2 a, Vector2 b, Vector2 c, Vector2 d, ref float best, ref Vector2 onRing, ref Vector2 onLine)
        {
            void Try(Vector2 r, Vector2 l)
            {
                float dist = Vector2.Distance(r, l);
                if (dist >= best) return;
                best = dist;
                onRing = r;
                onLine = l;
            }
            Try(a, OnSegment(a, c, d));
            Try(b, OnSegment(b, c, d));
            Try(OnSegment(c, a, b), c);
            Try(OnSegment(d, a, b), d);
        }

        static Vector2 OnSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = ab.sqrMagnitude > 1e-12f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return a + ab * t;
        }

        static void Move(BuildingFeature b, Vector2 offset)
        {
            var f = b.Spec.Footprint;
            var outer = f.Outer.Select(p => p + offset).ToList();
            var holes = f.Holes != null ? f.Holes.Select(h => h.Select(p => p + offset).ToList()).ToList() : null;
            b.Spec.Footprint = new Polygon(outer, holes);
            b.Centroid += offset;
        }
    }
}
```

Kalau `BuildingSpec.Footprint` ternyata readonly, ganti baris terakhir `Move` dengan membuat `BuildingSpec` baru yang menyalin semua field (`Id`, `WallHeight`, `Levels`, `LevelHeight`, `AllowGable`, `RoofOnly`, `RoofOverride`). Field-nya ada di `BuildingSpec.cs:8-18`.

Di `FeatureClassifier.Classify`, tepat sebelum `return world;`:

```csharp
            RoadClearance.Apply(world, settings.clearance, report);
```

- [ ] **Step 4:** Compile dan jalankan seluruh suite. Semua `RoadClearanceTests` PASS.
  - Tes WorldGen lama mungkin berubah angka (rumah fixture yang tadinya masuk jalan sekarang digeser). Kalau gagal, periksa dulu apakah perubahannya memang benar menurut spec; jangan langsung melonggarkan assert.
  - `Sukabirus_IsDeterministic` harus tetap lulus.

- [ ] **Step 5: Commit**

```bash
git add Assets/WorldGen/Editor/Builders/RoadClearance.cs Assets/WorldGen/Editor/Builders/RoadClearance.cs.meta Assets/WorldGen/Editor/Classify/FeatureClassifier.cs Assets/WorldGen/Tests/RoadClearanceTests.cs Assets/WorldGen/Tests/RoadClearanceTests.cs.meta
git commit -m "feat(worldgen): houses off the roads, roads cut short of named buildings"
```

---

### Task 3: DeadEndPlanner

**Files:**
- Create: `Assets/WorldGen/Editor/Builders/DeadEndPlanner.cs`
- Test: `Assets/WorldGen/Tests/DeadEndPlannerTests.cs`

**Interfaces:**
- Consumes:
  - `RoadNetwork.Chains` (`RoadChain.Points`, `Closed`, `HalfWidth`, `Width`, `Highway`, `Id`);
  - `RoadNetwork.Nearby(Vector2 p, float radius, RoadChain except, Dictionary<RoadChain,float> result)`, yang hasilnya berisi jarak ke garis tengah chain lain;
  - `RoadNetwork.MaxEdgeWidth(long roadId)`;
  - `ClassifiedWorld.LocalBounds`, `Areas`, `Buildings`;
  - `StreetContext.DistanceToKos(Vector2)`.
- Produces:
  - `public enum DeadEndKind { Wall, Portal, Boundary }`
  - `public readonly struct DeadEnd { Vector2 Position, Outward; float Width; DeadEndKind Kind; long RoadId; }`
  - `DeadEndPlanner.Plan(RoadNetwork network, ClassifiedWorld world, StreetContext street, DeadEndSettings s, float kosRadius, GenerationReport report) → List<DeadEnd>`
  - konstanta report `Walls="DeadEndWalls"`, `Portals="DeadEndPortals"`, `Boundaries="DeadEndBarriers"`, `Skipped="DeadEndSkipped"`.

- [ ] **Step 1: Tes gagal**

```csharp
using System.Linq;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class DeadEndPlannerTests
    {
        WorldGenSettings settings;
        GenerationReport report;

        [SetUp] public void SetUp() { settings = DefaultSettingsFactory.CreateInMemory(); report = new GenerationReport(); }
        [TearDown] public void TearDown() => Object.DestroyImmediate(settings);

        System.Collections.Generic.List<DeadEnd> Plan(System.Action<TestOsm> build)
        {
            var osm = new TestOsm();
            build(osm);
            var world = new FeatureClassifier(settings, report).Classify(osm.Data);
            var network = new RoadNetwork(world.Roads, new System.Collections.Generic.List<Polygon>(), settings.roadEdges);
            var street = StreetContext.From(world, settings);
            return DeadEndPlanner.Plan(network, world, street, settings.deadEnds, settings.frontage.kosExclusionRadius, report);
        }

        [Test]
        public void GangEndingInTheOpen_GetsALowWall_AcrossIt_FacingOut()
        {
            var ends = Plan(o =>
            {
                o.Way(1, new[] { new Vector2(-100f, 0f), new Vector2(100f, 0f) }, false, ("highway", "residential"));
                o.Way(2, new[] { new Vector2(0f, 0f), new Vector2(0f, 60f) }, false, ("highway", "residential"));
            });
            var end = ends.Single(e => e.RoadId == 2);
            Assert.AreEqual(DeadEndKind.Wall, end.Kind);
            Assert.AreEqual(Vector2.up, end.Outward);
            Assert.AreEqual(60f - settings.deadEnds.endInset, end.Position.y, 0.01f);
            Assert.GreaterOrEqual(end.Width, settings.deadEnds.minWidth);
        }

        [Test]
        public void TJunction_IsNotADeadEnd()
        {
            var ends = Plan(o =>
            {
                o.Way(1, new[] { new Vector2(-100f, 0f), new Vector2(100f, 0f) }, false, ("highway", "residential"));
                o.Way(2, new[] { new Vector2(0f, 1.5f), new Vector2(0f, 60f) }, false, ("highway", "residential"));
            });
            Assert.IsFalse(ends.Any(e => e.RoadId == 2 && e.Position.y < 10f), "the end inside road 1's carriageway meets it");
        }

        [Test]
        public void ServiceRoad_GetsAPortal()
        {
            var ends = Plan(o =>
            {
                o.Way(1, new[] { new Vector2(-100f, 0f), new Vector2(100f, 0f) }, false, ("highway", "residential"));
                o.Way(2, new[] { new Vector2(0f, 0f), new Vector2(0f, 40f) }, false, ("highway", "service"));
            });
            Assert.AreEqual(DeadEndKind.Portal, ends.Single(e => e.RoadId == 2).Kind);
        }

        [Test]
        public void EndAtTheMapEdge_GetsTheRoadClosedBarrier()
        {
            var ends = Plan(o => o.Way(1, new[] { new Vector2(0f, 0f), new Vector2(5000f, 0f) }, false, ("highway", "residential")));
            Assert.IsTrue(ends.Any(e => e.Kind == DeadEndKind.Boundary), "the road is clipped at the bounds");
        }

        [Test]
        public void EndAtAHouse_IsAlreadyClosed()
        {
            var ends = Plan(o =>
            {
                o.Way(1, new[] { new Vector2(-100f, 0f), new Vector2(100f, 0f) }, false, ("highway", "residential"));
                o.Way(2, new[] { new Vector2(0f, 0f), new Vector2(0f, 30f) }, false, ("highway", "footway"));
                o.Rect(50, 0f, 36f, 10f, 10f, ("building", "house"));
            });
            Assert.IsFalse(ends.Any(e => e.RoadId == 2));
            Assert.Greater(report.GetCount(DeadEndPlanner.Skipped), 0);
        }

        [Test]
        public void EndInsideAParkingLot_IsSkipped()
        {
            var ends = Plan(o =>
            {
                o.Way(1, new[] { new Vector2(-100f, 0f), new Vector2(100f, 0f) }, false, ("highway", "residential"));
                o.Way(2, new[] { new Vector2(0f, 0f), new Vector2(0f, 30f) }, false, ("highway", "service"));
                o.Rect(60, 0f, 35f, 30f, 20f, ("amenity", "parking"));
            });
            Assert.IsFalse(ends.Any(e => e.RoadId == 2));
        }

        [Test]
        public void ClosedLoop_HasNoEnds()
        {
            var ends = Plan(o => o.Way(1, new[] { new Vector2(0f, 0f), new Vector2(40f, 0f), new Vector2(40f, 40f), new Vector2(0f, 40f), new Vector2(0f, 0f) },
                false, ("highway", "residential")));
            Assert.IsEmpty(ends);
        }
    }
}
```

- [ ] **Step 2:** Jalankan. Harus FAIL dengan error kompilasi.

- [ ] **Step 3: Implementasi** (`DeadEndPlanner.cs`)

```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    public enum DeadEndKind { Wall, Portal, Boundary }

    /// <summary>One barrier to place: where it stands, which way the road ran out, how wide it is and what kind.</summary>
    public readonly struct DeadEnd
    {
        public readonly Vector2 Position, Outward;
        public readonly float Width;
        public readonly DeadEndKind Kind;
        public readonly long RoadId;

        public DeadEnd(Vector2 position, Vector2 outward, float width, DeadEndKind kind, long roadId)
        {
            Position = position;
            Outward = outward;
            Width = width;
            Kind = kind;
            RoadId = roadId;
        }

        /// <summary>Across the road (left of the outward direction).</summary>
        public Vector2 Across => new Vector2(-Outward.y, Outward.x);
    }

    /// <summary>
    /// The road ends that need closing (spec road-clearance §3). An end is open when it reaches no other road's
    /// carriageway; it is skipped inside a parking lot, in front of a building that already closes it, and by the kos.
    /// What closes it: the road-closed barrier near the map edge, a portal on service roads and tracks, a low wall
    /// with pots elsewhere. Chains in id order, no randomness.
    /// </summary>
    public static class DeadEndPlanner
    {
        public const string Walls = "DeadEndWalls", Portals = "DeadEndPortals", Boundaries = "DeadEndBarriers", Skipped = "DeadEndSkipped";

        public static List<DeadEnd> Plan(RoadNetwork network, ClassifiedWorld world, StreetContext street, DeadEndSettings s, float kosRadius,
            GenerationReport report)
        {
            var ends = new List<DeadEnd>();
            if (network == null || s == null || !s.enabled) return ends;
            var near = new Dictionary<RoadChain, float>();
            foreach (var chain in network.Chains.OrderBy(c => c.Id))
            {
                if (chain.Closed || chain.Points.Count < 2) continue;
                var pts = chain.Points;
                Consider(pts[0], (pts[0] - pts[1]).normalized);
                Consider(pts[pts.Count - 1], (pts[pts.Count - 1] - pts[pts.Count - 2]).normalized);

                void Consider(Vector2 end, Vector2 outward)
                {
                    near.Clear();
                    network.Nearby(end, network.MaxHalfWidth + s.joinMargin + chain.HalfWidth, chain, near);
                    foreach (var kv in near)
                        if (kv.Value <= kv.Key.HalfWidth + s.joinMargin) return;
                    if (Skip(end, outward, chain, world, street, s, kosRadius))
                    {
                        report.Count(Skipped);
                        return;
                    }
                    float width = Mathf.Max(s.minWidth, chain.Width + 2f * network.MaxEdgeWidth(chain.Id));
                    var kind = ToBounds(end, world.LocalBounds) < s.boundaryDistance ? DeadEndKind.Boundary
                        : s.portalHighways != null && s.portalHighways.Contains(chain.Highway) ? DeadEndKind.Portal
                        : DeadEndKind.Wall;
                    ends.Add(new DeadEnd(end - outward * s.endInset, outward, width, kind, chain.Id));
                    report.Count(kind == DeadEndKind.Wall ? Walls : kind == DeadEndKind.Portal ? Portals : Boundaries);
                }
            }
            return ends;
        }

        static float ToBounds(Vector2 p, Rect b) => Mathf.Min(Mathf.Min(p.x - b.xMin, b.xMax - p.x), Mathf.Min(p.y - b.yMin, b.yMax - p.y));

        static bool Skip(Vector2 end, Vector2 outward, RoadChain chain, ClassifiedWorld world, StreetContext street, DeadEndSettings s, float kosRadius)
        {
            if (street != null && street.DistanceToKos(end) < kosRadius) return true;
            foreach (var area in world.Areas)
                if (area.Rule != null && area.Rule.category == AreaCategory.Parking && area.Shape != null && area.Shape.Contains(end))
                    return true;
            var ahead = end + outward * (chain.HalfWidth + s.buildingAhead);
            foreach (var b in world.Buildings)
            {
                var f = b.Spec != null ? b.Spec.Footprint : null;
                if (f == null) continue;
                if (f.Contains(ahead) || f.DistanceToBoundary(end) <= chain.HalfWidth + s.buildingAhead) return true;
            }
            return false;
        }
    }
}
```

Cek dulu arti nilai hasil `RoadNetwork.Nearby` (`RoadNetwork.cs:160`). Kalau isinya bukan jarak ke garis tengah, sesuaikan perbandingan `kv.Value <= kv.Key.HalfWidth + joinMargin`, supaya `TJunction_IsNotADeadEnd` dan `GangEndingInTheOpen...` sama-sama lulus.

`DistanceToBoundary` menghitung jarak ke tepi footprint. Untuk ujung yang berada di dalam footprint, `Contains(ahead)` sudah menangkapnya.

- [ ] **Step 4:** Compile dan jalankan. PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/WorldGen/Editor/Builders/DeadEndPlanner.cs Assets/WorldGen/Editor/Builders/DeadEndPlanner.cs.meta Assets/WorldGen/Tests/DeadEndPlannerTests.cs Assets/WorldGen/Tests/DeadEndPlannerTests.cs.meta
git commit -m "feat(worldgen): find the dead ends that need a barrier"
```

---

### Task 4: BarrierBuilder dan wiring pipeline

**Files:**
- Create: `Assets/WorldGen/Editor/Builders/BarrierBuilder.cs`
- Modify: `Assets/WorldGen/Runtime/FeatureKind.cs` (tambah `Barrier` di akhir)
- Modify: `Assets/WorldGen/Editor/Pipeline/WorldMeshPipeline.cs`:
  - konstanta grup (L9-18, L60-61);
  - `BuiltWorld` (L49-50);
  - `Build` (setelah AddFrontage, sebelum PropPlacer);
  - hash (L397-398).
- Modify: `Assets/WorldGen/Editor/Pipeline/SceneObjectFactory.cs:100-102`
- Modify: `Assets/WorldGen/Runtime/Culling/WorldChunkIndex.cs:20-27`
- Test: `Assets/WorldGen/Tests/BarrierBuilderTests.cs`, `WorldMeshPipelineTests.cs`, `WorldChunkIndexTests.cs:65-79`

**Interfaces:**
- Consumes:
  - `DeadEnd` (Task 3);
  - `SolidBox.Add(MeshData, int sub, Vector2 a, Vector2 b, float y0, float y1, float thickness, BoxUv uv, BoxEnds ends, bool top)`;
  - `SolidBox.AddPost(m, sub, c, dir, size, y0, y1, uv)`;
  - `BoxUv.AtlasBand(band, bands, bandMeters, tileMeters)`, `BoxUv.Meters`;
  - `FrontageSettings` (`atlasBands`, `atlasBandMeters`, `atlasTileMeters`, `wallAtlas`);
  - `RoadEdgeSettings.concrete`;
  - `WorldMeshPipeline.ChunkOf(Vector2, float)`;
  - `PropPlacement`, `WallSegment`.
- Produces:
  - `BarrierBuilder.Build(IReadOnlyList<DeadEnd> ends, DeadEndSettings s, FrontageSettings f, float chunkSize) → BarrierBuildResult { Dictionary<Vector2Int, MeshData> Render, Collider; List<PropPlacement> Props; List<WallSegment> Blockers; }`;
  - `BarrierBuilder.Name = "Barriers"`;
  - sub-mesh `SubWall=0, SubConcrete=1, SubStripe=2`;
  - varian prop `PotVariant=0, ConcreteVariant=1, RoadClosedVariant=2`;
  - `FeatureGroups.Barriers = "Barriers"`, `WorldMeshPipeline.BarrierPropsGroup = "Props/Barriers"`, `BuiltWorld.Barriers` (`List<PropPlacement>`).

- [ ] **Step 1: Tes gagal** (`BarrierBuilderTests.cs`)

```csharp
using System.Linq;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class BarrierBuilderTests
    {
        WorldGenSettings settings;
        [SetUp] public void SetUp() => settings = DefaultSettingsFactory.CreateInMemory();
        [TearDown] public void TearDown() => Object.DestroyImmediate(settings);

        BarrierBuildResult One(DeadEndKind kind, float width = 5f) =>
            BarrierBuilder.Build(new[] { new DeadEnd(new Vector2(10f, 20f), Vector2.up, width, kind, 7) }, settings.deadEnds, settings.frontage, 100f);

        static Bounds BoundsOf(MeshData m)
        {
            var b = new Bounds(m.Vertices[0], Vector3.zero);
            foreach (var v in m.Vertices) b.Encapsulate(v);
            return b;
        }

        [Test]
        public void Wall_SpansTheRoad_AtWallHeight_WithACollider_AndTwoPots()
        {
            var r = One(DeadEndKind.Wall);
            var render = BoundsOf(r.Render.Values.Single());
            Assert.AreEqual(5f, render.size.x, 0.3f, "across the road");
            Assert.AreEqual(settings.deadEnds.wallHeight, render.max.y, 0.1f);
            var col = BoundsOf(r.Collider.Values.Single());
            Assert.GreaterOrEqual(col.size.x, 5f - 0.01f);
            Assert.AreEqual(2, r.Props.Count(p => p.Variant == BarrierBuilder.PotVariant));
            Assert.AreEqual(1, r.Blockers.Count);
        }

        [Test]
        public void Portal_IsClosed_ToItsBarHeight()
        {
            var r = One(DeadEndKind.Portal);
            var col = BoundsOf(r.Collider.Values.Single());
            Assert.GreaterOrEqual(col.max.y, settings.deadEnds.portalHeight - 0.01f);
            Assert.GreaterOrEqual(col.size.x, 5f - 0.01f);
            Assert.IsEmpty(r.Props, "the portal is all code-built");
        }

        [Test]
        public void Boundary_LinesConcreteAcross_PlusTheRoadClosedSign()
        {
            var r = One(DeadEndKind.Boundary, 6f);
            int blocks = r.Props.Count(p => p.Variant == BarrierBuilder.ConcreteVariant);
            Assert.GreaterOrEqual(blocks, Mathf.FloorToInt(6f / settings.deadEnds.blockSpacing));
            Assert.AreEqual(1, r.Props.Count(p => p.Variant == BarrierBuilder.RoadClosedVariant));
            Assert.IsNotEmpty(r.Collider, "solid even before the prefabs are assigned");
        }

        [Test]
        public void Props_FaceAlongTheRoad()
        {
            var r = One(DeadEndKind.Boundary);
            foreach (var p in r.Props) Assert.AreEqual(0f, Mathf.DeltaAngle(p.YawDegrees, 0f), 0.5f, "outward +Z -> yaw 0");
        }
    }
}
```

Di `WorldMeshPipelineTests` tambahkan tes berikut. Ia memakai `BuildStreet` yang sudah ada (L102-107), dengan OSM tambahan berupa gang buntu.

```csharp
[Test]
public void DeadEndGang_GetsABarrierChunk_WithACollider()
{
    var built = BuildStreet(o => o.Way(9001, new[] { new Vector2(150f, 0f), new Vector2(150f, 70f) }, false, ("highway", "residential")));
    var barrier = built.Features.Where(f => f.Kind == FeatureKind.Barrier).ToList();
    Assert.IsNotEmpty(barrier);
    Assert.IsTrue(barrier.All(f => f.ColliderMesh != null && f.Group == FeatureGroups.Barriers));
    Assert.Greater(report.GetCount(DeadEndPlanner.Walls), 0);
}
```

Kalau `BuildStreet` belum menerima `Action<TestOsm>` tambahan, ikuti cara `StreetFixture(settings, extra)` membuat `osm`.

Di `WorldChunkIndexTests` (L65-79), tambahkan `"Barriers"` dan `"Props/Barriers"` ke daftar kontainer yang diharapkan, dengan kategori Prop.

- [ ] **Step 2:** Jalankan. Harus FAIL dengan error kompilasi.

- [ ] **Step 3: Implementasi** (`BarrierBuilder.cs`)

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    public sealed class BarrierBuildResult
    {
        public readonly Dictionary<Vector2Int, MeshData> Render = new Dictionary<Vector2Int, MeshData>();
        public readonly Dictionary<Vector2Int, MeshData> Collider = new Dictionary<Vector2Int, MeshData>();
        public readonly List<PropPlacement> Props = new List<PropPlacement>();
        public readonly List<WallSegment> Blockers = new List<WallSegment>();
    }

    /// <summary>
    /// The barriers at dead ends (spec road-clearance §3–§5): a low plastered wall with two pots in front, a yellow
    /// and black portal on two posts, or a line of striped concrete blocks with the road-closed barricade at the map
    /// edge. Walls and portals are built here, merged per chunk; the downloaded models are placed as props. Every
    /// barrier also gets a solid box collider across the road.
    /// </summary>
    public static class BarrierBuilder
    {
        public const string Name = "Barriers";
        public const int SubWall = 0, SubConcrete = 1, SubStripe = 2, SubmeshCount = 3;
        public const int PotVariant = 0, ConcreteVariant = 1, RoadClosedVariant = 2;
        const float Coping = 0.06f;

        public static BarrierBuildResult Build(IReadOnlyList<DeadEnd> ends, DeadEndSettings s, FrontageSettings f, float chunkSize)
        {
            var r = new BarrierBuildResult();
            foreach (var end in ends)
            {
                var chunk = WorldMeshPipeline.ChunkOf(end.Position, chunkSize);
                var m = MeshIn(r.Render, chunk, SubmeshCount);
                var col = MeshIn(r.Collider, chunk, 1);
                var across = end.Across;
                var a = end.Position - across * (end.Width * 0.5f);
                var b = end.Position + across * (end.Width * 0.5f);
                float yaw = Mathf.Atan2(end.Outward.x, end.Outward.y) * Mathf.Rad2Deg;
                float top;
                switch (end.Kind)
                {
                    case DeadEndKind.Wall:
                        top = s.wallHeight;
                        SolidBox.Add(m, SubWall, a, b, 0f, top - Coping, s.wallThickness,
                            BoxUv.AtlasBand(s.wallBand, f.atlasBands, f.atlasBandMeters, f.atlasTileMeters), BoxEnds.Both, false);
                        SolidBox.Add(m, SubConcrete, a, b, top - Coping, top, s.wallThickness + 0.04f, BoxUv.Meters, BoxEnds.Both, true);
                        // Two pots on the road side, in from each end.
                        foreach (var at in new[] { a + across * 0.45f, b - across * 0.45f })
                            r.Props.Add(new PropPlacement { Position = at - end.Outward * (s.wallThickness * 0.5f + 0.35f), YawDegrees = yaw, Scale = 1f, Variant = PotVariant });
                        break;
                    case DeadEndKind.Portal:
                        top = s.portalHeight;
                        SolidBox.AddPost(m, SubStripe, a, across, s.portalPost, 0f, top, BoxUv.Meters);
                        SolidBox.AddPost(m, SubStripe, b, across, s.portalPost, 0f, top, BoxUv.Meters);
                        SolidBox.Add(m, SubStripe, a, b, top - 0.12f, top, 0.08f, BoxUv.Meters, BoxEnds.Both, true);
                        break;
                    default:
                        top = 1.0f;
                        int blocks = Mathf.Max(1, Mathf.FloorToInt(end.Width / s.blockSpacing));
                        for (int i = 0; i < blocks; i++)
                        {
                            float t = (i + 0.5f) / blocks;
                            r.Props.Add(new PropPlacement { Position = Vector2.Lerp(a, b, t), YawDegrees = yaw + 90f, Scale = 1f, Variant = ConcreteVariant });
                        }
                        r.Props.Add(new PropPlacement { Position = end.Position - end.Outward * 1.0f, YawDegrees = yaw + 180f, Scale = 1f, Variant = RoadClosedVariant });
                        break;
                }
                SolidBox.Add(col, 0, a, b, 0f, top, Mathf.Max(s.wallThickness, 0.3f), BoxUv.Meters, BoxEnds.Both, true);
                r.Blockers.Add(new WallSegment(a, b));
            }
            return r;
        }

        static MeshData MeshIn(Dictionary<Vector2Int, MeshData> meshes, Vector2Int chunk, int submeshes)
        {
            if (!meshes.TryGetValue(chunk, out var m)) meshes[chunk] = m = new MeshData(submeshes);
            return m;
        }
    }
}
```

Konstruktor `MeshData(int submeshCount)` dan nama `Vertices` ikuti API di `MeshData.cs`. Lihat juga cara `FrontageMeshBuilder.MeshIn` membuatnya, lalu samakan.

`yaw + 90f` untuk beton: model balok Quaternius memanjang di sumbu X lokalnya. Kalau setelah dirakit di Task 5 ternyata memanjang di Z, ubah jadi `yaw` dan sesuaikan tes `Props_FaceAlongTheRoad`. Tes itu hanya memaku pot dan plang (`RoadClosedVariant` menghadap ke dalam, `yaw + 180`). Karena itu, sesuaikan juga assert-nya supaya per varian:
- `Pot`: `yaw`;
- `Concrete`: `yaw + 90`;
- `RoadClosed`: `yaw + 180`.

Wiring:
- `FeatureKind.cs`: tambahkan `Barrier` di akhir enum.
- `FeatureGroups`: `public const string Barriers = "Barriers";`.
- `WorldMeshPipeline`: `public const string BarrierPropsGroup = "Props/Barriers";`.
- `BuiltWorld`: `public List<PropPlacement> Barriers = new List<PropPlacement>();`.

Di `Build`, setelah `var walls = AddFrontage(...)`:

```csharp
            var deadEnds = DeadEndPlanner.Plan(network, world, street, settings.deadEnds, settings.frontage.kosExclusionRadius, report);
            var barriers = BarrierBuilder.Build(deadEnds, settings.deadEnds, settings.frontage, built.ChunkSize);
            AddChunkFeatures(built, barriers.Render, barriers.Collider, FeatureKind.Barrier, FeatureGroups.Barriers, BarrierBuilder.Name,
                new[] { settings.frontage.wallAtlas, settings.roadEdges.concrete, settings.deadEnds.stripe }, report);
            built.Barriers = barriers.Props;
            report.Lap("meshes.barriers");
```

Nama metode lap/stage mengikuti pemanggilan yang sudah ada di `Build`.

PropPlacer menghindari pembatas:

```csharp
            placer.AddWalls(walls);
            placer.AddWalls(barriers.Blockers);
```

Di `ComputeChunkHashes`, di sebelah Trees/Lamps:

```csharp
            foreach (var p in built.Barriers) Mix(ChunkOf(p.Position, built.ChunkSize), Quantize(p));
```

Di `SceneObjectFactory`, setelah lampu:

```csharp
            PlaceProps(scene, world.Barriers, settings.barrierPrefabs, WorldMeshPipeline.BarrierPropsGroup, world.ChunkSize, Container, report, "pembatas");
```

Di `WorldChunkIndex.Build`:

```csharp
            Add(root.Find("Barriers"), CullCategory.Prop, groups);
            Add(root.Find("Props/Barriers"), CullCategory.Prop, groups);
```

`SurfaceRules.For` mengembalikan Default untuk kind yang tidak dikenal, jadi sudah benar.

- [ ] **Step 4:** Compile dan jalankan seluruh suite. Semua PASS, termasuk `Sukabirus_IsDeterministic` dan `Sukabirus_NoInvalidGeometry`.

- [ ] **Step 5: Commit**

```bash
git add Assets/WorldGen/Editor/Builders/BarrierBuilder.cs Assets/WorldGen/Editor/Builders/BarrierBuilder.cs.meta Assets/WorldGen/Runtime/FeatureKind.cs Assets/WorldGen/Editor/Pipeline/WorldMeshPipeline.cs Assets/WorldGen/Editor/Pipeline/SceneObjectFactory.cs Assets/WorldGen/Runtime/Culling/WorldChunkIndex.cs Assets/WorldGen/Tests/BarrierBuilderTests.cs Assets/WorldGen/Tests/BarrierBuilderTests.cs.meta Assets/WorldGen/Tests/WorldMeshPipelineTests.cs Assets/WorldGen/Tests/WorldChunkIndexTests.cs
git commit -m "feat(worldgen): barriers at dead ends - walls, portals, road-closed lines"
```

---

### Task 5: Aset pembatas (CC0) dan prefab

**Files:**
- Create: `Assets/WorldGen/Art/ThirdParty/Barriers/`:
  - `Kenney_ConstructionBarrier.obj/.mtl`
  - `Quaternius_BarrierSingle.obj/.mtl`
  - `Kenney_PottedPlant.obj/.mtl`
  - `Quaternius_TrafficBarrier.obj/.mtl/.png`
  - `SOURCES.md`
- Create: `Assets/WorldGen/Editor/Art/BarrierArt.cs`
- Modify: `Assets/WorldGen/Editor/Art/ArtAssetBuilder.cs` (`CreateMaterials` L149-203, `Assign` L212-249)
- Test: `Assets/WorldGen/Tests/ArtTests.cs`

**Sumber** (sudah diunduh ke `scratchpad/downloads/polypizza_barriers/`; lisensi "Public Domain (CC0)" dicek di halaman masing-masing):

| Model | Pembuat | Halaman | GLB |
|---|---|---|---|
| Construction Barrier (beton jersey putih) | Kenney | poly.pizza/m/4mrO9ueiQr | `4mrO9ueiQr.glb` |
| Barrier Single (balok bergaris kuning-hitam) | Quaternius | poly.pizza/m/wCUxgt2jSP | `wCUxgt2jSP.glb` |
| Potted Plant | Kenney | poly.pizza/m/23Dx9CC95C | `23Dx9CC95C.glb` |
| Traffic Barrier (palang merah-putih) | Quaternius | poly.pizza/m/cM3aJPU9NS | `cM3aJPU9NS.glb` (bertekstur) |

- [ ] **Step 1: Perluas konverter** di scratch (bukan kode produk): `scratchpad/tools/glb2obj.py`.
  - Telusuri hierarki node mulai dari `scene.nodes`, lalu terapkan TRS (`translation`/`rotation`/`scale`/`matrix`) ke posisi dan normal setiap mesh. Model Kenney/Quaternius punya node berskala 100.
  - Primitif dengan `baseColorTexture` mengekspor gambarnya (bufferView image) ke `<out>.png`. MTL menulis `map_Kd <out>.png` dan UV `vt` (v dibalik: `1 - v`).
  - Primitif tanpa tekstur menulis `Kd` dari `baseColorFactor`, seperti sekarang.
  - Jalankan dengan `python -I` untuk keempat GLB, dengan `--ground`. Hasilnya cek tinggi dan lebar bounding box: beton sekitar 0,8 × 1,5 m, pot sekitar 0,6 m tinggi. Pakai `--scale=` kalau perlu.

- [ ] **Step 2: Tes gagal** (`ArtTests.cs`)

```csharp
[Test]
public void BarrierPrefabs_AreBuilt_Solid_AndOfASensibleSize()
{
    var prefabs = BarrierArt.BuildPrefabs();
    Assert.AreEqual(3, prefabs.Length);
    float[,] height = { { 0.3f, 1.1f }, { 0.5f, 1.2f }, { 0.9f, 2.2f } };
    for (int i = 0; i < 3; i++)
    {
        var p = prefabs[i];
        Assert.IsNotNull(p, "prefab " + i);
        Assert.IsNotNull(p.GetComponentInChildren<Collider>(), p.name + " is solid");
        var b = new Bounds(p.transform.position, Vector3.zero);
        foreach (var r in p.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
        Assert.That(b.size.y, Is.InRange(height[i, 0], height[i, 1]), p.name + " height");
    }
    Assert.IsNotNull(prefabs[BarrierBuilder.RoadClosedVariant].GetComponentInChildren<TextMesh>(), "the sign reads JALAN DITUTUP");
    Assert.AreEqual("JALAN DITUTUP", prefabs[BarrierBuilder.RoadClosedVariant].GetComponentInChildren<TextMesh>().text);
}

[Test]
public void SavedSettings_UseTheBarrierPrefabs_AndTheStripeMaterial()
{
    var s = AssetDatabase.LoadAssetAtPath<WorldGenSettings>(WorldGenPaths.SettingsAsset);
    Assume.That(s, Is.Not.Null);
    Assert.AreEqual(3, s.barrierPrefabs.Length);
    Assert.IsTrue(s.barrierPrefabs.All(p => p != null));
    Assert.IsNotNull(s.deadEnds.stripe);
}
```

- [ ] **Step 3:** Jalankan. Harus FAIL dengan error kompilasi `BarrierArt`.

- [ ] **Step 4: Implementasi** (`BarrierArt.cs`)

```csharp
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>
    /// Prefabs for the dead-end props (spec road-clearance §4): the potted plant, the striped concrete block and the
    /// road-closed barricade with its "JALAN DITUTUP" board, each from a downloaded CC0 model scaled to size, with a
    /// box collider; and the yellow-black stripe texture the code-built portals use.
    /// </summary>
    public static class BarrierArt
    {
        public const string ModelsFolder = "Assets/WorldGen/Art/ThirdParty/Barriers/";
        public const string PrefabFolder = "Assets/WorldGen/Prefabs/Barriers/";
        public const string StripeTexture = "Assets/WorldGen/Textures/Generated/Barrier_Stripe.png";

        public static GameObject[] BuildPrefabs()
        {
            Directory.CreateDirectory(PrefabFolder);
            return new[]
            {
                Prefab("Barrier_Pot", "Kenney_PottedPlant.obj", height: 0.7f, null),
                Prefab("Barrier_Concrete", "Quaternius_BarrierSingle.obj", height: 0.8f, null),
                Prefab("Barrier_RoadClosed", "Quaternius_TrafficBarrier.obj", height: 1.1f, AddSign)
            };
        }

        static GameObject Prefab(string name, string model, float height, System.Action<GameObject> extra)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsFolder + model);
            if (source == null) return null;
            var root = new GameObject(name);
            var body = (GameObject)Object.Instantiate(source, root.transform, false);
            body.name = "Model";
            var b = BoundsOf(body);
            if (b.size.y > 1e-4f) body.transform.localScale *= height / b.size.y;
            b = BoundsOf(body);
            body.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);
            extra?.Invoke(root);
            b = BoundsOf(root);
            var box = root.AddComponent<BoxCollider>();
            box.center = b.center;
            box.size = b.size;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + name + ".prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        static void AddSign(GameObject root)
        {
            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Board";
            Object.DestroyImmediate(board.GetComponent<Collider>());
            board.transform.SetParent(root.transform, false);
            board.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            board.transform.localScale = new Vector3(1.3f, 0.36f, 0.04f);
            var text = new GameObject("Text").AddComponent<TextMesh>();
            text.transform.SetParent(root.transform, false);
            text.transform.localPosition = new Vector3(0f, 1.35f, -0.03f);
            text.text = "JALAN DITUTUP";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
            text.fontSize = 48;
            text.characterSize = 0.03f;
            text.anchor = TextAnchor.MiddleCenter;
            text.color = new Color(0.75f, 0.05f, 0.05f);
        }

        static Bounds BoundsOf(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>Diagonal yellow and black bands, tiling along a bar (written once, then imported).</summary>
        public static Texture2D StripeTexture2D()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            var yellow = new Color32(232, 190, 20, 255);
            var black = new Color32(22, 22, 22, 255);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, ((x + y) / (size / 4)) % 2 == 0 ? yellow : black);
            tex.Apply();
            return tex;
        }
    }
}
```

Board papan memakai material putih dari palet material WorldGen yang sudah ada, misalnya `settings.roadEdges.concrete`. Jangan pakai material default primitif. Untuk itu `AddSign` menerima `Material`, disalurkan dari `ArtAssetBuilder.Assign`.

Di `ArtAssetBuilder.CreateMaterials`:
- tulis `StripeTexture2D()` ke `BarrierArt.StripeTexture` dengan pola yang sama seperti tekstur generated lain;
- buat `m["Barrier_Stripe"] = Mat("Barrier_Stripe", stripeTex, null, Color.white, 0.5f, 0.3f)`.

Di `Assign`:

```csharp
            s.deadEnds.stripe = m["Barrier_Stripe"];
            s.barrierPrefabs = BarrierArt.BuildPrefabs();
```

`SOURCES.md` di folder Barriers mencatat:
- untuk tiap model: nama, pembuat, URL halaman Poly Pizza, lisensi CC0 1.0;
- perubahan: GLB→OBJ (transform node diterapkan, tekstur diekspor) dan skala.

- [ ] **Step 5:** Compile, lalu jalankan "Tyco World > Build Art Assets" (atau `ArtAssetBuilder` lewat MCP), lalu tes. PASS.

- [ ] **Step 6: Commit**

```bash
git add Assets/WorldGen/Art Assets/WorldGen/Art.meta Assets/WorldGen/Editor/Art/BarrierArt.cs Assets/WorldGen/Editor/Art/BarrierArt.cs.meta Assets/WorldGen/Editor/Art/ArtAssetBuilder.cs Assets/WorldGen/Prefabs/Barriers Assets/WorldGen/Prefabs/Barriers.meta Assets/WorldGen/Textures/Generated/Barrier_Stripe.png Assets/WorldGen/Textures/Generated/Barrier_Stripe.png.meta Assets/WorldGen/Tests/ArtTests.cs <settings asset dan material baru yang muncul di git status>
git commit -m "feat(worldgen): CC0 barrier models, prefabs and the stripe material"
```

---

### Task 6: Data gameplay mengikuti jalan yang dipotong

**Files:**
- Test: `Assets/Gameplay/Tests/KampungBakerTests.cs`. Kalau belum ada, buat file baru di namespace `TycoWorld.Gameplay.Tests`.

**Interfaces:**
- Consumes: `KampungBaker.Bake(ClassifiedWorld, settings, report)` (`KampungBaker.cs:29`) dan `RoadClearance` (Task 2, otomatis lewat `Classify`).

- [ ] **Step 1: Tes** (harus lulus tanpa kode baru, karena Classify sudah memotong; ini memaku perilakunya)

```csharp
[Test]
public void MapRoads_StopShortOfANamedBuilding_ARoadRanThrough()
{
    var settings = DefaultSettingsFactory.CreateInMemory();
    try
    {
        var report = new GenerationReport();
        var osm = new TestOsm();
        osm.Way(1, new[] { new Vector2(-100f, 0f), new Vector2(100f, 0f) }, false, ("highway", "residential"));
        osm.Rect(20, 0f, 0f, 30f, 30f, ("building", "university"), ("name", "Gedung Uji"));
        var world = new FeatureClassifier(settings, report).Classify(osm.Data);
        var data = KampungBaker.Bake(world, settings, report);
        var footprint = world.Buildings.First(b => b.Id == 20).Spec.Footprint;
        foreach (var lane in data.mapRoads)
            foreach (var p in lane.points)
                Assert.IsFalse(footprint.Contains(p), "the phone map and GPS do not route through the building");
        Object.DestroyImmediate(data);
    }
    finally { Object.DestroyImmediate(settings); }
}
```

Sesuaikan tipe kembalian `Bake` (KampungData atau out-param) dengan `KampungBaker.cs:29`. Kalau assembly tes Gameplay belum mereferensikan `TestOsm` (WorldGen.Tests), salin dua metode helper yang dipakai ke tes ini, atau tambahkan referensi assembly yang sudah dipakai `KampungFixture`.

- [ ] **Step 2:** Jalankan. PASS.
- [ ] **Step 3:** Commit `test(gameplay): baked map roads respect road clearance`.

---

### Task 7: Generate, bake, setup, dan verifikasi

- [ ] **Step 1:** Cek bahwa user tidak sedang Play. Lalu jalankan berurutan:
  1. "Tyco World > Build Art Assets";
  2. World Generator > Generate;
  3. "Tyco World > Gameplay > Bake Kampung Data";
  4. "Tyco World > Gameplay Setup".

  Catat counter report: `ClearanceShifted`, `ClearanceDropped`, `ClearanceRoadCuts`, `DeadEndWalls`, `DeadEndPortals`, `DeadEndBarriers`, `DeadEndSkipped`. Waktu generate harus < 60 s.
- [ ] **Step 2: Determinisme.** Generate lagi, lalu `CompareChunkHashes` harus IDENTICAL.
- [ ] **Step 3: Pindai ulang** dengan skrip pindaian dari spec §1 (titik `mapRoads` tiap 2 m, raycast ke bawah). Target: titik tertutup bangunan > 1 m menjadi 0, kecuali sisa yang dijelaskan satu per satu.
- [ ] **Step 4: Screenshot** (Play, siang, kamera pemain), satu per jenis pembatas: tembok + pot di gang, portal, beton + plang di batas peta. Tambah satu screenshot lokasi rumah yang tadinya menutup jalan.
- [ ] **Step 5: Fisika di Play.**
  - Jalan kaki menabrak tembok gang: berhenti.
  - Motor menabrak portal pada 30 km/j: berhenti tanpa menembus.
  - `McpGameplayDriver` / `McpVehicleDriver` yang sudah ada dipakai untuk teleport dan menyetir.
- [ ] **Step 6: Perf.** Sweep kos/road jam 10:00, metode rata-rata 300 frame (seperti catatan karakter). Batch ≤ +3% dibanding sebelum perubahan.
- [ ] **Step 7: Dokumen.**
  - Tulis `docs/superpowers/notes/2026-10-15-road-clearance-verification.md`, berisi angka, screenshot, dan sisa.
  - Perbarui spec bila ada deviasi.
- [ ] **Step 8:** Suite penuh `McpTestRunner.Run("TycoWorld", ...)`: 0 gagal. Lalu commit file terkait, termasuk scene World.unity kalau berubah dan `KampungData.asset`, kemudian push.
