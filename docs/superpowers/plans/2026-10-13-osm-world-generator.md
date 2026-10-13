# OSM World Generator Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Tool Unity Editor yang mengubah data OpenStreetMap area Telkom University (kampus, Jl. Sukabirus, Jl. Telekomunikasi) menjadi dunia 3D low poly yang bisa dijelajahi jalan kaki, deterministik dan bisa di-generate ulang.

**Architecture:** Data OSM didownload sekali via Overpass ke `WorldGenData/osm_raw.json`. Pipeline murni (parse → proyeksi → klasifikasi → mesh → props) menghasilkan `BuiltWorld` tanpa menyentuh scene; lapisan editor lalu menulis mesh per chunk 100 m ke asset dan membangun scene additive `World_Generated.unity` (diabaikan git). Scene utama `World.unity` (lighting, preview, konten manual) tidak pernah diubah oleh Generate.

**Tech Stack:** Unity 6000.6.5f1, URP 17.6.0 (Forward+, GPU Resident Drawer), C# 9, Unity Test Framework 1.8.0 (NUnit 3.5), Newtonsoft.Json 3.2.2, Unity MCP (`mcp__unity-mcp__Unity_RunCommand`, `Unity_GetConsoleLogs`) untuk compile/test/verifikasi, PowerShell untuk git dan download.

**Spec:** `docs/superpowers/specs/2026-10-13-osm-world-generator-design.md`

## Global Constraints

- Project root: `D:\UNITY\project\Tyco World`. Branch kerja: `feature/osm-world-generator` (sudah ada, sudah di-checkout).
- Unity 6000.6.5f1, URP 17.6.0, `com.unity.test-framework` 1.8.0 (NUnit 3.5), `com.unity.nuget.newtonsoft-json` 3.2.2.
- Assembly & namespace: runtime `TycoWorld.WorldGen` (namespace `TycoWorld.WorldGen`), editor `TycoWorld.WorldGen.Editor` (namespace `TycoWorld.WorldGen.Authoring`), tes `TycoWorld.WorldGen.Tests` (namespace `TycoWorld.WorldGen.Tests`).
- C# 9 di Unity: jangan pakai `record`, setter `init`, atau operator indeks `^`.
- Locale user `id-ID` (koma desimal). Semua parse/format angka WAJIB `CultureInfo.InvariantCulture`.
- Unity 6.6: API instance-ID berbasis `int` (`GetInstanceID`, overload `int` di `AssetDatabase`) adalah compile error → pakai overload `Object`. Pakai `Object.FindObjectsByType`, bukan `FindObjectsOfType`.
- Jangan pakai `??` / `?.` pada `UnityEngine.Object` (fake-null). Pakai `if (x == null)`.
- File yang punya `using System;` dan `using UnityEngine;` sekaligus: tulis `UnityEngine.Object` secara eksplisit.
- Semua keacakan lewat `SplitMix64` (Task 5). Jangan pakai `UnityEngine.Random`/`System.Random`.
- Koordinat: lokal 2D `Vector2(x = timur, y = utara)`; 3D `Vector3(x, tinggi, z = utara)` via `Xz.At`. Winding mesh selalu lewat `MeshData.AddTriangle/AddQuad` dengan "desired normal" (Task 7) — jangan menulis indeks segitiga manual.
- Output generator hanya di `Assets/WorldGen/Generated/` (sudah di `.gitignore`). Folder itu dihapus via `System.IO` + `AssetDatabase.Refresh()` — `AssetDatabase.DeleteAsset` diblok Unity MCP ("User interactions are not supported").
- Nilai default (batas area, titik nol, tabel aturan, endpoint, gedung kunci) hanya ditulis di `DefaultSettingsFactory.Populate` (Task 9), disalin persis dari spec.
- Setiap commit diakhiri dua baris trailer:
  ```
  Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5
  ```

## Review Focus

1. **Locale `id-ID`** — "6.5" harus terbaca 6.5 (bukan 65) dan bbox query harus "-6.9850" (bukan "-6,9850"). Dipaku oleh: `TagParsingTests.Parsing_IsCultureInvariant_UnderIndonesianLocale` (Task 3), `OverpassQueryTests.Build_IsCultureInvariant` + `OsmParserTests.Parse_IsCultureInvariant` (Task 4), `FeatureClassifierTests.Width_IsCultureInvariant` (Task 10), `FetcherTests.WriteInfo_IsCultureInvariant` (Task 14).
2. **Nilai tag aneh** (`"10;12"`, `"6,5"`, `"abc"`, `"-2"`, tanggal `"2023-05-01"`) — tidak boleh crash/salah tafsir. Dipaku oleh: `TagParsingTests` (Task 3), `OsmParserTests.Synthetic_DateLikeTagStaysVerbatim` (Task 4), `FeatureClassifierTests.Synthetic_House` (Task 10).
3. **Geometri jalan degenerate** (titik ganda, U-turn 180°, belokan tajam) — tidak boleh NaN atau paku panjang. Dipaku oleh: `MeshBuilderTests.Ribbon_UTurn_ProducesNoNaN`, `Ribbon_SharpTurn_RespectsMiterLimit`, `Ribbon_DuplicatePoints_Ignored` (Task 7), `WorldMeshPipelineTests.Sukabirus_NoInvalidGeometry` (Task 12).
4. **Generate saat ada scene belum disimpan / World.unity terbuka** — tidak boleh membuang perubahan atau mengubah `World.unity`. Dipaku oleh: verifikasi V13 (hash `World.unity` sebelum/sesudah, Task 13) dan `WorldScenes.CheckSaved` yang menolak scene dirty pada mode non-interaktif.
5. **Overpass menolak/ngaco** (406 tanpa User-Agent; HTTP 200 berisi HTML atau `remark` error) — harus ditolak dan pindah endpoint. Dipaku oleh: `OverpassResponseValidatorTests` (Task 4), `FetcherTests.CreateRequest_SetsUserAgentAndPost` (Task 14).

---

## Prosedur standar (dipakai semua task)

### Procedure R — refresh & compile

Panggil `mcp__unity-mcp__Unity_RunCommand` dengan:

```csharp
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        result.Log("Refresh done. isCompiling={0}", EditorApplication.isCompiling);
    }
}
```

Setelah itu Unity compile + domain reload. Jika panggilan MCP berikutnya gagal karena reload, ulangi panggilan itu.

### Procedure T — jalankan tes EditMode

Panggil `mcp__unity-mcp__Unity_RunCommand` dengan kode di bawah; ganti nilai `Group` sesuai langkah (regex yang dicocokkan ke nama lengkap tes; `"."` = semua tes).

```csharp
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

internal class CommandScript : IRunCommand
{
    const string Group = "SmokeTests";

    class Collector : ICallbacks
    {
        public int Passed, Failed, Skipped;
        public readonly List<string> Failures = new List<string>();
        public void RunStarted(ITestAdaptor t) { }
        public void RunFinished(ITestResultAdaptor r) { }
        public void TestStarted(ITestAdaptor t) { }
        public void TestFinished(ITestResultAdaptor r)
        {
            if (r.Test.IsSuite) return;
            if (r.TestStatus == TestStatus.Passed) Passed++;
            else if (r.TestStatus == TestStatus.Failed) { Failed++; Failures.Add(r.FullName + " :: " + r.Message); }
            else Skipped++;
        }
    }

    public void Execute(ExecutionResult result)
    {
        if (EditorApplication.isCompiling) { result.LogError("STILL COMPILING - ulangi Procedure T sebentar lagi."); return; }
        if (EditorUtility.scriptCompilationFailed) { result.LogError("COMPILE FAILED - baca error via Unity_GetConsoleLogs (logTypes=error)."); return; }
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) { result.LogError("Scene belum disimpan: " + SceneManager.GetSceneAt(i).path); return; }
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        var collector = new Collector();
        api.RegisterCallbacks(collector);
        var filter = new Filter { testMode = TestMode.EditMode, assemblyNames = new[] { "TycoWorld.WorldGen.Tests" }, groupNames = new[] { Group } };
        api.Execute(new ExecutionSettings(filter) { runSynchronously = true });
        api.UnregisterCallbacks(collector);
        result.Log("group={0} passed={1} failed={2} skipped={3}", Group, collector.Passed, collector.Failed, collector.Skipped);
        foreach (var f in collector.Failures) result.LogError(f);
    }
}
```

"Expected: COMPILE FAILED" pada langkah red berarti: Procedure T mencetak `COMPILE FAILED`, lalu `mcp__unity-mcp__Unity_GetConsoleLogs` (`logTypes: "error"`) menunjukkan error `CS0246`/`CS0103` untuk tipe yang belum dibuat.

### Procedure C — commit

```powershell
git -C "D:\UNITY\project\Tyco World" add <path...>
git -C "D:\UNITY\project\Tyco World" commit -m @'
<subject>

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5
'@
```

Selalu ikutkan file `.meta` yang dibuat Unity (cukup `git add` foldernya).

### Catatan snippet MCP

Snippet `Unity_RunCommand` wajib `internal class CommandScript : IRunCommand`, tidak boleh memakai `System.Reflection`, dan harus menulis `UnityEngine.Mesh` (bukan `Mesh`) karena konteks namespace MCP.

---

## Peta file

```
Packages/manifest.json                                   (modify: + newtonsoft)
WorldGenData/osm_raw.json, osm_raw.info.json             (Task 17, dilacak git)
Assets/Scenes/World.unity                                (Task 13 dibuat minimal, Task 16 diisi preview)
Assets/WorldGen/
  Runtime/  TycoWorld.WorldGen.asmdef
            FeatureKind.cs  OsmFeature.cs  OsmFeatureRegistry.cs  KeyBuildingMarker.cs
            GeneratedRoot.cs  WorldSceneLoader.cs
  Editor/   TycoWorld.WorldGen.Editor.asmdef  WorldGenPaths.cs  AssetFolders.cs
    Geo/        GeoBounds.cs  GeoProjection.cs  TagParsing.cs
    Data/       OsmModel.cs  OsmParser.cs  OverpassResponseValidator.cs  OverpassQuery.cs
                CacheWriter.cs  OsmFetcher.cs
    Geometry/   Polygon.cs  Clipping.cs  ObbCalculator.cs  SplitMix64.cs  Xz.cs
                RingAssembler.cs  Triangulator.cs  SpatialGrid.cs  PoissonDisk.cs
    Builders/   MeshData.cs  RoadMeshBuilder.cs  AreaMeshBuilder.cs  GroundBuilder.cs
                BuildingSpec.cs  BuildingMeshBuilder.cs  PropPlacer.cs
    Settings/   WorldGenRules.cs  WorldGenSettings.cs  DefaultSettingsFactory.cs
    Classify/   Features.cs  FeatureClassifier.cs
    Pipeline/   GenerationReport.cs  WorldMeshPipeline.cs  WorldScenes.cs  ChunkAssetWriter.cs
                SceneObjectFactory.cs  WorldGenerationRunner.cs
    UI/         WorldGenWindow.cs
    Art/        FacadeTextureGenerator.cs  PlaceholderMeshes.cs  PlaceholderPrefabFactory.cs
                ArtAssetBuilder.cs
    Preview/    PreviewSceneSetup.cs
  Tests/    TycoWorld.WorldGen.Tests.asmdef  FixtureFiles.cs  TestOsm.cs  *Tests.cs
    Fixtures/   synthetic_world.json  sukabirus_small.json
  Config/   WorldGenSettings.asset  Preview_NightVHS.asset          (dibuat oleh tool)
  Materials/ Textures/{CC0,Facade}/ Textures/SOURCES.md  Prefabs/    (Task 15)
  Generated/                                                        (diabaikan git)
docs/superpowers/notes/2026-10-13-worldgen-baseline.md              (Task 17)
```

---

### Task 1: Scaffolding assembly + Newtonsoft + path helper

**Files:**
- Modify: `Packages/manifest.json`
- Create: `Assets/WorldGen/Runtime/TycoWorld.WorldGen.asmdef`
- Create: `Assets/WorldGen/Editor/TycoWorld.WorldGen.Editor.asmdef`
- Create: `Assets/WorldGen/Editor/WorldGenPaths.cs`
- Create: `Assets/WorldGen/Tests/TycoWorld.WorldGen.Tests.asmdef`
- Test: `Assets/WorldGen/Tests/SmokeTests.cs`

**Interfaces:**
- Produces: `TycoWorld.WorldGen.Authoring.WorldGenPaths` — konstanta `Root`, `ConfigFolder`, `SettingsAsset`, `VolumeProfileAsset`, `GeneratedFolder`, `GeneratedMeshesFolder`, `GeneratedSceneName`, `GeneratedScenePath`, `WorldScenePath`, `MaterialsFolder`, `TexturesFolder`, `Cc0TexturesFolder`, `FacadeTexturesFolder`, `PrefabsFolder`, `FixturesFolder`; `string ProjectRoot { get; }`; `string ToAbsolute(string projectRelativePath)`.

- [ ] **Step 1: Tambah Newtonsoft eksplisit ke manifest**

Di `Packages/manifest.json`, tambahkan baris ini tepat setelah baris `"com.unity.inputsystem": "1.20.0",`:

```json
    "com.unity.nuget.newtonsoft-json": "3.2.2",
```

- [ ] **Step 2: Buat tiga asmdef**

`Assets/WorldGen/Runtime/TycoWorld.WorldGen.asmdef`:

```json
{
    "name": "TycoWorld.WorldGen",
    "rootNamespace": "TycoWorld.WorldGen",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

`Assets/WorldGen/Editor/TycoWorld.WorldGen.Editor.asmdef`:

```json
{
    "name": "TycoWorld.WorldGen.Editor",
    "rootNamespace": "TycoWorld.WorldGen.Authoring",
    "references": [
        "TycoWorld.WorldGen",
        "Unity.RenderPipelines.Core.Runtime",
        "Unity.RenderPipelines.Universal.Runtime"
    ],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

`Assets/WorldGen/Tests/TycoWorld.WorldGen.Tests.asmdef`:

```json
{
    "name": "TycoWorld.WorldGen.Tests",
    "rootNamespace": "TycoWorld.WorldGen.Tests",
    "references": [
        "TycoWorld.WorldGen",
        "TycoWorld.WorldGen.Editor",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner",
        "Unity.RenderPipelines.Core.Runtime",
        "Unity.RenderPipelines.Universal.Runtime"
    ],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": ["nunit.framework.dll", "Newtonsoft.Json.dll"],
    "autoReferenced": false,
    "defineConstraints": ["UNITY_INCLUDE_TESTS"],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 3: Tulis tes yang gagal**

`Assets/WorldGen/Tests/SmokeTests.cs`:

```csharp
using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;

namespace TycoWorld.WorldGen.Tests
{
    public class SmokeTests
    {
        [Test]
        public void ToAbsolute_ResolvesUnderProjectRoot()
        {
            string abs = WorldGenPaths.ToAbsolute("WorldGenData/osm_raw.json");
            StringAssert.EndsWith(Path.Combine("WorldGenData", "osm_raw.json"), abs);
            StringAssert.StartsWith(WorldGenPaths.ProjectRoot, abs);
        }

        [Test]
        public void Newtonsoft_IsReferenced()
        {
            Assert.AreEqual("ok", (string)JObject.Parse("{\"v\":\"ok\"}")["v"]);
        }
    }
}
```

- [ ] **Step 4: Jalankan, pastikan gagal**

Procedure R, lalu Procedure T dengan `Group = "SmokeTests"`.
Expected: COMPILE FAILED (`WorldGenPaths` tidak ditemukan).

- [ ] **Step 5: Implementasi**

`Assets/WorldGen/Editor/WorldGenPaths.cs`:

```csharp
using System.IO;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Project-relative paths used by the world generator.</summary>
    public static class WorldGenPaths
    {
        public const string Root = "Assets/WorldGen";
        public const string ConfigFolder = Root + "/Config";
        public const string SettingsAsset = ConfigFolder + "/WorldGenSettings.asset";
        public const string VolumeProfileAsset = ConfigFolder + "/Preview_NightVHS.asset";
        public const string GeneratedFolder = Root + "/Generated";
        public const string GeneratedMeshesFolder = GeneratedFolder + "/Meshes";
        public const string GeneratedSceneName = "World_Generated";
        public const string GeneratedScenePath = GeneratedFolder + "/" + GeneratedSceneName + ".unity";
        public const string WorldScenePath = "Assets/Scenes/World.unity";
        public const string MaterialsFolder = Root + "/Materials";
        public const string TexturesFolder = Root + "/Textures";
        public const string Cc0TexturesFolder = TexturesFolder + "/CC0";
        public const string FacadeTexturesFolder = TexturesFolder + "/Facade";
        public const string PrefabsFolder = Root + "/Prefabs";
        public const string FixturesFolder = Root + "/Tests/Fixtures";

        public static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        public static string ToAbsolute(string projectRelativePath) =>
            Path.GetFullPath(Path.Combine(ProjectRoot, projectRelativePath));
    }
}
```

- [ ] **Step 6: Jalankan, pastikan lulus**

Procedure R, lalu Procedure T dengan `Group = "SmokeTests"`.
Expected: `passed=2 failed=0`.

- [ ] **Step 7: Commit**

Procedure C: `git add Packages/manifest.json Packages/packages-lock.json Assets/WorldGen`, subject `chore: scaffold WorldGen assemblies and path helper`.

---

### Task 2: Komponen runtime (metadata OSM, registry, loader)

**Files:**
- Create: `Assets/WorldGen/Runtime/FeatureKind.cs`
- Create: `Assets/WorldGen/Runtime/OsmFeature.cs`
- Create: `Assets/WorldGen/Runtime/OsmFeatureRegistry.cs`
- Create: `Assets/WorldGen/Runtime/KeyBuildingMarker.cs`
- Create: `Assets/WorldGen/Runtime/GeneratedRoot.cs`
- Create: `Assets/WorldGen/Runtime/WorldSceneLoader.cs`
- Test: `Assets/WorldGen/Tests/OsmFeatureRegistryTests.cs`

**Interfaces:**
- Produces (namespace `TycoWorld.WorldGen`):
  - `enum OsmElementType { Node, Way, Relation }`, `enum FeatureKind { Road, Building, Area, WaterLine, WaterArea }`
  - `OsmFeature : MonoBehaviour` — `long OsmId`, `OsmElementType ElementType`, `FeatureKind Kind`, `string DisplayName`, `int TagCount`, `void Initialize(long id, OsmElementType type, FeatureKind kind, string name, IEnumerable<KeyValuePair<string,string>> tags)`, `bool TryGetTag(string key, out string value)`
  - `static OsmFeatureRegistry` — `int Count`, `Register(OsmFeature)`, `Unregister(OsmFeature)`, `bool TryGet(OsmElementType, long, out OsmFeature)`, `bool TryGet(long, out OsmFeature)` (urutan Way → Relation → Node), `List<OsmFeature> FindByName(string)`, `Clear()`
  - `KeyBuildingMarker : MonoBehaviour` — `long OsmId`, `string Label`, `void Initialize(long id, string label)`
  - `GeneratedRoot : MonoBehaviour` — `string GeneratedAtUtc`, `string Summary`, `void Initialize(string atUtc, string summary)`
  - `WorldSceneLoader : MonoBehaviour` — `const string DefaultScenePath = "Assets/WorldGen/Generated/World_Generated.unity"`

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/WorldGen/Tests/OsmFeatureRegistryTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class OsmFeatureRegistryTests
    {
        readonly List<GameObject> created = new List<GameObject>();

        [SetUp]
        public void SetUp() => OsmFeatureRegistry.Clear();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created)
                if (go != null) Object.DestroyImmediate(go);
            created.Clear();
            OsmFeatureRegistry.Clear();
        }

        OsmFeature Make(long id, OsmElementType type, string name, params (string, string)[] tags)
        {
            var go = new GameObject(name);
            created.Add(go);
            var feature = go.AddComponent<OsmFeature>();
            var list = new List<KeyValuePair<string, string>>();
            foreach (var (k, v) in tags) list.Add(new KeyValuePair<string, string>(k, v));
            feature.Initialize(id, type, FeatureKind.Building, name, list);
            OsmFeatureRegistry.Register(feature);
            return feature;
        }

        [Test]
        public void TryGet_ReturnsRegisteredFeature()
        {
            var f = Make(10, OsmElementType.Way, "A");
            Assert.IsTrue(OsmFeatureRegistry.TryGet(OsmElementType.Way, 10, out var found));
            Assert.AreSame(f, found);
        }

        [Test]
        public void SameIdDifferentTypes_DoNotCollide()
        {
            var way = Make(5, OsmElementType.Way, "W");
            var node = Make(5, OsmElementType.Node, "N");
            Assert.IsTrue(OsmFeatureRegistry.TryGet(OsmElementType.Way, 5, out var w));
            Assert.IsTrue(OsmFeatureRegistry.TryGet(OsmElementType.Node, 5, out var n));
            Assert.AreSame(way, w);
            Assert.AreSame(node, n);
            Assert.AreEqual(2, OsmFeatureRegistry.Count);
        }

        [Test]
        public void TryGet_ByIdOnly_PrefersWay()
        {
            Make(7, OsmElementType.Node, "N");
            var way = Make(7, OsmElementType.Way, "W");
            Assert.IsTrue(OsmFeatureRegistry.TryGet(7, out var found));
            Assert.AreSame(way, found);
        }

        [Test]
        public void Unregister_StaleDuplicate_KeepsNewer()
        {
            var older = Make(1, OsmElementType.Way, "old");
            var newer = Make(1, OsmElementType.Way, "new");
            OsmFeatureRegistry.Unregister(older);
            Assert.IsTrue(OsmFeatureRegistry.TryGet(OsmElementType.Way, 1, out var found));
            Assert.AreSame(newer, found);
        }

        [Test]
        public void FindByName_IsCaseInsensitiveAndSortedById()
        {
            Make(30, OsmElementType.Way, "Jalan Sukabirus");
            Make(20, OsmElementType.Way, "jalan sukabirus");
            Make(25, OsmElementType.Way, "Jalan Telekomunikasi");
            var found = OsmFeatureRegistry.FindByName("JALAN SUKABIRUS");
            Assert.AreEqual(2, found.Count);
            Assert.AreEqual(20, found[0].OsmId);
            Assert.AreEqual(30, found[1].OsmId);
        }

        [Test]
        public void DestroyedFeature_IsNotReturned()
        {
            var f = Make(9, OsmElementType.Way, "gone");
            Object.DestroyImmediate(f.gameObject);
            Assert.IsFalse(OsmFeatureRegistry.TryGet(OsmElementType.Way, 9, out _));
        }

        [Test]
        public void TryGetTag_ReadsInitializedTags()
        {
            var f = Make(3, OsmElementType.Way, "T", ("highway", "tertiary"), ("width", "6"));
            Assert.IsTrue(f.TryGetTag("width", out var width));
            Assert.AreEqual("6", width);
            Assert.IsFalse(f.TryGetTag("name", out _));
            Assert.AreEqual(2, f.TagCount);
        }
    }
}
```

- [ ] **Step 2: Jalankan, pastikan gagal**

Procedure R, lalu Procedure T dengan `Group = "OsmFeatureRegistryTests"`. Expected: COMPILE FAILED (`OsmFeature` tidak ditemukan).

- [ ] **Step 3: Implementasi**

`Assets/WorldGen/Runtime/FeatureKind.cs`:

```csharp
namespace TycoWorld.WorldGen
{
    public enum OsmElementType { Node, Way, Relation }

    public enum FeatureKind { Road, Building, Area, WaterLine, WaterArea }
}
```

`Assets/WorldGen/Runtime/OsmFeature.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.WorldGen
{
    /// <summary>OSM metadata for one generated feature. Registers itself for runtime lookup.</summary>
    [DisallowMultipleComponent]
    public sealed class OsmFeature : MonoBehaviour
    {
        [SerializeField] long osmId;
        [SerializeField] OsmElementType elementType;
        [SerializeField] FeatureKind kind;
        [SerializeField] string displayName = "";
        [SerializeField] string[] tagKeys = Array.Empty<string>();
        [SerializeField] string[] tagValues = Array.Empty<string>();

        public long OsmId => osmId;
        public OsmElementType ElementType => elementType;
        public FeatureKind Kind => kind;
        public string DisplayName => displayName;
        public int TagCount => tagKeys.Length;

        public void Initialize(long id, OsmElementType type, FeatureKind featureKind, string name,
            IEnumerable<KeyValuePair<string, string>> tags)
        {
            osmId = id;
            elementType = type;
            kind = featureKind;
            displayName = name ?? "";
            var pairs = new List<KeyValuePair<string, string>>();
            if (tags != null) pairs.AddRange(tags);
            pairs.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            tagKeys = new string[pairs.Count];
            tagValues = new string[pairs.Count];
            for (int i = 0; i < pairs.Count; i++)
            {
                tagKeys[i] = pairs[i].Key;
                tagValues[i] = pairs[i].Value;
            }
        }

        public bool TryGetTag(string key, out string value)
        {
            for (int i = 0; i < tagKeys.Length; i++)
            {
                if (tagKeys[i] != key) continue;
                value = tagValues[i];
                return true;
            }
            value = null;
            return false;
        }

        void OnEnable() => OsmFeatureRegistry.Register(this);

        void OnDisable() => OsmFeatureRegistry.Unregister(this);
    }
}
```

`Assets/WorldGen/Runtime/OsmFeatureRegistry.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace TycoWorld.WorldGen
{
    /// <summary>Runtime lookup of generated features by OSM id or display name.</summary>
    public static class OsmFeatureRegistry
    {
        static readonly Dictionary<(OsmElementType, long), OsmFeature> features =
            new Dictionary<(OsmElementType, long), OsmFeature>();

        public static int Count => features.Count;

        public static void Register(OsmFeature feature)
        {
            if (feature == null) return;
            features[(feature.ElementType, feature.OsmId)] = feature;
        }

        public static void Unregister(OsmFeature feature)
        {
            if (feature == null) return;
            var key = (feature.ElementType, feature.OsmId);
            if (features.TryGetValue(key, out var current) && current == feature) features.Remove(key);
        }

        public static bool TryGet(OsmElementType type, long osmId, out OsmFeature feature) =>
            features.TryGetValue((type, osmId), out feature) && feature != null;

        public static bool TryGet(long osmId, out OsmFeature feature) =>
            TryGet(OsmElementType.Way, osmId, out feature)
            || TryGet(OsmElementType.Relation, osmId, out feature)
            || TryGet(OsmElementType.Node, osmId, out feature);

        public static List<OsmFeature> FindByName(string name)
        {
            var result = new List<OsmFeature>();
            if (string.IsNullOrEmpty(name)) return result;
            foreach (var feature in features.Values)
                if (feature != null && string.Equals(feature.DisplayName, name, StringComparison.OrdinalIgnoreCase))
                    result.Add(feature);
            result.Sort((a, b) => a.OsmId.CompareTo(b.OsmId));
            return result;
        }

        public static void Clear() => features.Clear();
    }
}
```

`Assets/WorldGen/Runtime/KeyBuildingMarker.cs`:

```csharp
using UnityEngine;

namespace TycoWorld.WorldGen
{
    /// <summary>Marks a key building that may later be replaced by a hand-made prefab with an interior.</summary>
    [DisallowMultipleComponent]
    public sealed class KeyBuildingMarker : MonoBehaviour
    {
        [SerializeField] long osmId;
        [SerializeField] string label = "";

        public long OsmId => osmId;
        public string Label => label;

        public void Initialize(long id, string buildingLabel)
        {
            osmId = id;
            label = buildingLabel ?? "";
        }
    }
}
```

`Assets/WorldGen/Runtime/GeneratedRoot.cs`:

```csharp
using UnityEngine;

namespace TycoWorld.WorldGen
{
    /// <summary>Marks the root GameObject owned by the world generator.</summary>
    [DisallowMultipleComponent]
    public sealed class GeneratedRoot : MonoBehaviour
    {
        [SerializeField] string generatedAtUtc = "";
        [SerializeField, TextArea(3, 30)] string summary = "";

        public string GeneratedAtUtc => generatedAtUtc;
        public string Summary => summary;

        public void Initialize(string atUtc, string reportSummary)
        {
            generatedAtUtc = atUtc ?? "";
            summary = reportSummary ?? "";
        }
    }
}
```

`Assets/WorldGen/Runtime/WorldSceneLoader.cs`:

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TycoWorld.WorldGen
{
    /// <summary>Loads the generated world scene additively when the main world scene starts.</summary>
    public sealed class WorldSceneLoader : MonoBehaviour
    {
        public const string DefaultScenePath = "Assets/WorldGen/Generated/World_Generated.unity";

        [SerializeField] string generatedScenePath = DefaultScenePath;

        void Awake()
        {
            if (SceneManager.GetSceneByPath(generatedScenePath).isLoaded) return;
            int buildIndex = SceneUtility.GetBuildIndexByScenePath(generatedScenePath);
            if (buildIndex < 0)
            {
                Debug.LogError($"[WorldGen] '{generatedScenePath}' tidak ada di Build Settings. Jalankan Generate dulu.");
                return;
            }
            SceneManager.LoadScene(buildIndex, LoadSceneMode.Additive);
        }
    }
}
```

- [ ] **Step 4: Jalankan, pastikan lulus**

Procedure R, lalu Procedure T dengan `Group = "OsmFeatureRegistryTests"`. Expected: `passed=7 failed=0`.

- [ ] **Step 5: Commit**

Procedure C: `git add Assets/WorldGen`, subject `feat(worldgen): add runtime OSM metadata components`.

---

### Task 3: Proyeksi geo dan parsing tag

**Files:**
- Create: `Assets/WorldGen/Editor/Geo/GeoBounds.cs`
- Create: `Assets/WorldGen/Editor/Geo/GeoProjection.cs`
- Create: `Assets/WorldGen/Editor/Geo/TagParsing.cs`
- Test: `Assets/WorldGen/Tests/GeoProjectionTests.cs`, `Assets/WorldGen/Tests/TagParsingTests.cs`

**Interfaces:**
- Produces (namespace `TycoWorld.WorldGen.Authoring`):
  - `[Serializable] struct GeoBounds { double South, West, North, East; GeoBounds(double south, double west, double north, double east); bool Contains(double lat, double lon) }`
  - `sealed class GeoProjection { GeoProjection(double originLat, double originLon); double OriginLat, OriginLon, MetersPerDegLat, MetersPerDegLon; Vector2 ToLocal(double lat, double lon); void ToGeo(Vector2 local, out double lat, out double lon); Rect ToLocalRect(GeoBounds bounds) }`
  - `static class TagParsing { bool TryParseLeadingFloat(string text, out float value); bool TryParseCount(string text, int max, out int value); bool TryParseLevels(string text, out int levels) }`

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/WorldGen/Tests/GeoProjectionTests.cs`:

```csharp
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;

namespace TycoWorld.WorldGen.Tests
{
    public class GeoProjectionTests
    {
        const double Lat0 = -6.97265, Lon0 = 107.63400;
        GeoProjection projection;

        [SetUp]
        public void SetUp() => projection = new GeoProjection(Lat0, Lon0);

        [Test]
        public void Origin_MapsToZero()
        {
            var p = projection.ToLocal(Lat0, Lon0);
            Assert.AreEqual(0f, p.x, 1e-4f);
            Assert.AreEqual(0f, p.y, 1e-4f);
        }

        [Test]
        public void MetersPerDegree_MatchSpec()
        {
            Assert.AreEqual(110590.64, projection.MetersPerDegLat, 0.1);
            Assert.AreEqual(110501.61, projection.MetersPerDegLon, 0.1);
        }

        [Test]
        public void NorthOffset_IsAbout110_59Meters()
        {
            var p = projection.ToLocal(Lat0 + 0.001, Lon0);
            Assert.AreEqual(110.59f, p.y, 0.01f);
            Assert.AreEqual(0f, p.x, 1e-4f);
        }

        [Test]
        public void EastOffset_IsAbout110_50Meters()
        {
            var p = projection.ToLocal(Lat0, Lon0 + 0.001);
            Assert.AreEqual(110.50f, p.x, 0.01f);
            Assert.AreEqual(0f, p.y, 1e-4f);
        }

        [Test]
        public void RoundTrip_ReturnsOriginalCoordinates()
        {
            var p = projection.ToLocal(-6.9771, 107.6311);
            projection.ToGeo(p, out double lat, out double lon);
            Assert.AreEqual(-6.9771, lat, 1e-7);
            Assert.AreEqual(107.6311, lon, 1e-7);
        }

        [Test]
        public void ToLocalRect_MatchesSpecBounds()
        {
            var r = projection.ToLocalRect(new GeoBounds(-6.9850, 107.6255, -6.9675, 107.6380));
            Assert.AreEqual(-939.26f, r.xMin, 0.1f);
            Assert.AreEqual(442.01f, r.xMax, 0.1f);
            Assert.AreEqual(-1365.79f, r.yMin, 0.1f);
            Assert.AreEqual(569.54f, r.yMax, 0.1f);
        }

        [Test]
        public void GeoBounds_Contains()
        {
            var b = new GeoBounds(-6.9850, 107.6255, -6.9675, 107.6380);
            Assert.IsTrue(b.Contains(-6.97265, 107.634));
            Assert.IsFalse(b.Contains(-6.99, 107.634));
        }
    }
}
```

`Assets/WorldGen/Tests/TagParsingTests.cs`:

```csharp
using System.Globalization;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;

namespace TycoWorld.WorldGen.Tests
{
    public class TagParsingTests
    {
        [TestCase("6", 6f)]
        [TestCase("6 m", 6f)]
        [TestCase("6.5", 6.5f)]
        [TestCase("6,5", 6f)]
        [TestCase("10;12", 10f)]
        [TestCase(" 4.25m ", 4.25f)]
        [TestCase(".5", 0.5f)]
        public void TryParseLeadingFloat_Valid(string text, float expected)
        {
            Assert.IsTrue(TagParsing.TryParseLeadingFloat(text, out float value));
            Assert.AreEqual(expected, value, 1e-5f);
        }

        [TestCase((string)null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("abc")]
        [TestCase("-2")]
        [TestCase("0")]
        [TestCase(".")]
        public void TryParseLeadingFloat_Invalid(string text)
        {
            Assert.IsFalse(TagParsing.TryParseLeadingFloat(text, out _));
        }

        [TestCase("4", 4)]
        [TestCase("2.5", 3)]
        [TestCase("20", 20)]
        [TestCase("1;2", 1)]
        public void TryParseLevels_Valid(string text, int expected)
        {
            Assert.IsTrue(TagParsing.TryParseLevels(text, out int levels));
            Assert.AreEqual(expected, levels);
        }

        [TestCase("abc")]
        [TestCase("0")]
        [TestCase("500")]
        public void TryParseLevels_Invalid(string text)
        {
            Assert.IsFalse(TagParsing.TryParseLevels(text, out _));
        }

        [Test]
        public void Parsing_IsCultureInvariant_UnderIndonesianLocale()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("id-ID");
                Assert.IsTrue(TagParsing.TryParseLeadingFloat("6.5", out float value));
                Assert.AreEqual(6.5f, value, 1e-5f);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }
    }
}
```

- [ ] **Step 2: Jalankan, pastikan gagal**

Procedure R, lalu Procedure T dengan `Group = "GeoProjectionTests|TagParsingTests"`. Expected: COMPILE FAILED (`GeoProjection`, `TagParsing` tidak ditemukan).

- [ ] **Step 3: Implementasi**

`Assets/WorldGen/Editor/Geo/GeoBounds.cs`:

```csharp
using System;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Latitude/longitude bounding box in degrees.</summary>
    [Serializable]
    public struct GeoBounds
    {
        public double South, West, North, East;

        public GeoBounds(double south, double west, double north, double east)
        {
            South = south;
            West = west;
            North = north;
            East = east;
        }

        public bool Contains(double lat, double lon) =>
            lat >= South && lat <= North && lon >= West && lon <= East;
    }
}
```

`Assets/WorldGen/Editor/Geo/GeoProjection.cs`:

```csharp
using System;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Local equirectangular projection around an origin. x = east, y = north, in meters.</summary>
    public sealed class GeoProjection
    {
        public double OriginLat { get; }
        public double OriginLon { get; }
        public double MetersPerDegLat { get; }
        public double MetersPerDegLon { get; }

        public GeoProjection(double originLat, double originLon)
        {
            OriginLat = originLat;
            OriginLon = originLon;
            double phi = originLat * Math.PI / 180.0;
            MetersPerDegLat = 111132.92 - 559.82 * Math.Cos(2 * phi) + 1.175 * Math.Cos(4 * phi);
            MetersPerDegLon = 111412.84 * Math.Cos(phi) - 93.5 * Math.Cos(3 * phi) + 0.118 * Math.Cos(5 * phi);
        }

        public Vector2 ToLocal(double lat, double lon) =>
            new Vector2((float)((lon - OriginLon) * MetersPerDegLon), (float)((lat - OriginLat) * MetersPerDegLat));

        public void ToGeo(Vector2 local, out double lat, out double lon)
        {
            lat = OriginLat + local.y / MetersPerDegLat;
            lon = OriginLon + local.x / MetersPerDegLon;
        }

        public Rect ToLocalRect(GeoBounds bounds)
        {
            var min = ToLocal(bounds.South, bounds.West);
            var max = ToLocal(bounds.North, bounds.East);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
```

`Assets/WorldGen/Editor/Geo/TagParsing.cs`:

```csharp
using System;
using System.Globalization;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Culture-invariant parsing of numeric OSM tag values such as "6", "6 m", "6.5", "10;12".</summary>
    public static class TagParsing
    {
        public static bool TryParseLeadingFloat(string text, out float value)
        {
            value = 0f;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string s = text.Trim();
            int i = 0;
            bool seenDigit = false, seenDot = false;
            while (i < s.Length)
            {
                char c = s[i];
                if (c >= '0' && c <= '9') { seenDigit = true; i++; }
                else if (c == '.' && !seenDot) { seenDot = true; i++; }
                else break;
            }
            if (!seenDigit) return false;
            if (!float.TryParse(s.Substring(0, i), NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return false;
            return value > 0f && !float.IsInfinity(value);
        }

        public static bool TryParseCount(string text, int max, out int value)
        {
            value = 0;
            if (!TryParseLeadingFloat(text, out float f)) return false;
            value = (int)Math.Floor(f + 0.5f);
            return value >= 1 && value <= max;
        }

        public static bool TryParseLevels(string text, out int levels) => TryParseCount(text, 200, out levels);
    }
}
```

- [ ] **Step 4: Jalankan, pastikan lulus**

Procedure R, lalu Procedure T dengan `Group = "GeoProjectionTests|TagParsingTests"`. Expected: `passed=29 failed=0` (7 proyeksi + 22 parsing).

- [ ] **Step 5: Commit**

Procedure C: `git add Assets/WorldGen`, subject `feat(worldgen): add geo projection and culture-invariant tag parsing`.

---

### Task 4: Model data OSM, parser, validator, query + fixture

**Files:**
- Create: `Assets/WorldGen/Editor/Data/OsmModel.cs`
- Create: `Assets/WorldGen/Editor/Data/OsmParser.cs`
- Create: `Assets/WorldGen/Editor/Data/OverpassResponseValidator.cs`
- Create: `Assets/WorldGen/Editor/Data/OverpassQuery.cs`
- Create: `Assets/WorldGen/Tests/Fixtures/synthetic_world.json`
- Create: `Assets/WorldGen/Tests/Fixtures/sukabirus_small.json` (download)
- Create: `Assets/WorldGen/Tests/FixtureFiles.cs`
- Test: `Assets/WorldGen/Tests/OsmParserTests.cs`, `OverpassResponseValidatorTests.cs`, `OverpassQueryTests.cs`

**Interfaces:**
- Consumes: `GeoBounds` (Task 3), `OsmElementType` (Task 2).
- Produces (namespace `TycoWorld.WorldGen.Authoring`):
  - `static class OsmTags { IReadOnlyDictionary<string,string> Empty; static string Tag(this IReadOnlyDictionary<string,string> tags, string key) }` (null jika tidak ada)
  - `OsmNode(long id, double lat, double lon, IReadOnlyDictionary<string,string> tags)` dengan field `Id, Lat, Lon, Tags`
  - `OsmWay(long id, List<long> nodeIds, IReadOnlyDictionary<string,string> tags)` dengan field `Id, NodeIds, Tags`, properti `bool IsClosed`
  - `OsmMember(OsmElementType type, long reference, string role)` dengan field `Type, Ref, Role`
  - `OsmRelation(long id, List<OsmMember> members, IReadOnlyDictionary<string,string> tags)` dengan field `Id, Members, Tags`
  - `OsmData` dengan field `Dictionary<long,OsmNode> Nodes`, `Dictionary<long,OsmWay> Ways`, `Dictionary<long,OsmRelation> Relations`
  - `static class OsmParser { OsmData Parse(string json); internal JObject Load(string json) }` (melempar `FormatException` jika tidak ada `elements`)
  - `static class OverpassResponseValidator { bool Validate(string body, out string error); int CountElements(string body) }`
  - `static class OverpassQuery { string Build(GeoBounds bounds, int timeoutSeconds) }`
  - Tes: `internal static class FixtureFiles { string Read(string fileName) }`

- [ ] **Step 1: Buat fixture sintetis**

`Assets/WorldGen/Tests/Fixtures/synthetic_world.json` (koordinat dekat titik nol; posisi lokal dicatat di komentar plan: n1 (0,0), n2 (22.1,0), n3 (22.1,22.1), n5 (5.5,5.5), n11 (0,-38.7), n12 (11.93,-38.7), n13 (11.93,-30.74), n21 (-11.05,-16.59), n22 (33.15,-16.59), n31 (-22.1,-60.8), n33 (-5.5,-44.2), n41 (-5.5,-5.5), n43 (27.6,27.6), tree 301 (-22.1,-27.65)):

```json
{
  "version": 0.6,
  "elements": [
    {"type": "node", "id": 1, "lat": -6.97265, "lon": 107.63400},
    {"type": "node", "id": 2, "lat": -6.97265, "lon": 107.63420},
    {"type": "node", "id": 3, "lat": -6.97245, "lon": 107.63420},
    {"type": "node", "id": 4, "lat": -6.97245, "lon": 107.63400},
    {"type": "node", "id": 5, "lat": -6.97260, "lon": 107.63405},
    {"type": "node", "id": 6, "lat": -6.97260, "lon": 107.63415},
    {"type": "node", "id": 7, "lat": -6.97250, "lon": 107.63415},
    {"type": "node", "id": 8, "lat": -6.97250, "lon": 107.63405},
    {"type": "node", "id": 11, "lat": -6.97300, "lon": 107.634000},
    {"type": "node", "id": 12, "lat": -6.97300, "lon": 107.634108},
    {"type": "node", "id": 13, "lat": -6.972928, "lon": 107.634108},
    {"type": "node", "id": 14, "lat": -6.972928, "lon": 107.634000},
    {"type": "node", "id": 21, "lat": -6.97280, "lon": 107.63390},
    {"type": "node", "id": 22, "lat": -6.97280, "lon": 107.63430},
    {"type": "node", "id": 31, "lat": -6.97320, "lon": 107.63380},
    {"type": "node", "id": 32, "lat": -6.97320, "lon": 107.63395},
    {"type": "node", "id": 33, "lat": -6.97305, "lon": 107.63395},
    {"type": "node", "id": 34, "lat": -6.97305, "lon": 107.63380},
    {"type": "node", "id": 41, "lat": -6.97270, "lon": 107.63395},
    {"type": "node", "id": 42, "lat": -6.97270, "lon": 107.63425},
    {"type": "node", "id": 43, "lat": -6.97240, "lon": 107.63425},
    {"type": "node", "id": 44, "lat": -6.97240, "lon": 107.63395},
    {"type": "node", "id": 301, "lat": -6.97290, "lon": 107.63380, "tags": {"natural": "tree"}},
    {"type": "way", "id": 101, "nodes": [1, 2, 3]},
    {"type": "way", "id": 102, "nodes": [1, 4, 3]},
    {"type": "way", "id": 103, "nodes": [5, 6, 7, 8, 5]},
    {"type": "way", "id": 401, "nodes": [11, 12, 13, 14, 11], "tags": {"building": "house", "height": "10;12", "building:levels": "abc", "check_date": "2023-05-01", "name": "Rumah Sintetis"}},
    {"type": "way", "id": 402, "nodes": [21, 22], "tags": {"highway": "residential", "width": "6,5", "name": "Gang Sintetis"}},
    {"type": "way", "id": 403, "nodes": [21, 999], "tags": {"highway": "footway"}},
    {"type": "way", "id": 404, "nodes": [31, 32, 33, 34, 31], "tags": {"landuse": "grass"}},
    {"type": "way", "id": 405, "nodes": [41, 42, 43, 44, 41], "tags": {"amenity": "university", "name": "Kampus Sintetis"}},
    {"type": "relation", "id": 201, "members": [
      {"type": "way", "ref": 101, "role": "outer"},
      {"type": "way", "ref": 102, "role": "outer"},
      {"type": "way", "ref": 103, "role": "inner"}
    ], "tags": {"type": "multipolygon", "building": "university", "name": "Gedung Sintetis"}}
  ]
}
```

- [ ] **Step 2: Download fixture data asli (potongan kecil Jl. Sukabirus)**

Server Overpass sering 504; skrip mencoba tiga endpoint. Jika semuanya gagal, tunggu 5–10 menit lalu ulangi — jangan lanjut tanpa file ini.

```powershell
$q = @'
[out:json][timeout:60][bbox:-6.9765,107.6310,-6.9755,107.6325];
(
  way["highway"];
  way["building"];
  node["natural"="tree"];
);
(._;>;);
out body;
'@
$dst = "D:\UNITY\project\Tyco World\Assets\WorldGen\Tests\Fixtures\sukabirus_small.json"
$ok = $false
foreach ($ep in @("https://overpass-api.de/api/interpreter","https://overpass.kumi.systems/api/interpreter","https://overpass.private.coffee/api/interpreter")) {
  try {
    Invoke-WebRequest -Uri $ep -Method Post -Body @{data=$q} -UserAgent "TycoWorld-WorldGen/1.0 (Unity editor; personal project)" -TimeoutSec 120 -OutFile $dst
    $j = Get-Content $dst -Raw | ConvertFrom-Json
    if ($j.elements.Count -gt 0) { "ok from $ep, elements=" + $j.elements.Count; $ok = $true; break }
  } catch { "fail $ep : $($_.Exception.Message)" }
}
if (-not $ok) { throw "Fixture download failed on all endpoints" }
$w = $j.elements | Where-Object { $_.type -eq 'way' -and $_.id -eq 95529338 }
"95529338 present: " + ($null -ne $w) + " name=" + $w.tags.name
```

Expected: `ok from ...`, dan `95529338 present: True name=Jalan Sukabirus`.

- [ ] **Step 3: Tulis tes yang gagal**

`Assets/WorldGen/Tests/FixtureFiles.cs`:

```csharp
using System.IO;
using TycoWorld.WorldGen.Authoring;

namespace TycoWorld.WorldGen.Tests
{
    internal static class FixtureFiles
    {
        public static string Read(string fileName) =>
            File.ReadAllText(WorldGenPaths.ToAbsolute(WorldGenPaths.FixturesFolder + "/" + fileName));
    }
}
```

`Assets/WorldGen/Tests/OsmParserTests.cs`:

```csharp
using System;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;

namespace TycoWorld.WorldGen.Tests
{
    public class OsmParserTests
    {
        static OsmData Synthetic() => OsmParser.Parse(FixtureFiles.Read("synthetic_world.json"));
        static OsmData Sukabirus() => OsmParser.Parse(FixtureFiles.Read("sukabirus_small.json"));

        [Test]
        public void Synthetic_CountsElements()
        {
            var d = Synthetic();
            Assert.AreEqual(23, d.Nodes.Count);
            Assert.AreEqual(8, d.Ways.Count);
            Assert.AreEqual(1, d.Relations.Count);
        }

        [Test]
        public void Synthetic_ReadsWayNodesAndTags()
        {
            var w = Synthetic().Ways[401];
            CollectionAssert.AreEqual(new long[] { 11, 12, 13, 14, 11 }, w.NodeIds);
            Assert.IsTrue(w.IsClosed);
            Assert.AreEqual("house", w.Tags["building"]);
            Assert.AreEqual("Rumah Sintetis", w.Tags.Tag("name"));
            Assert.IsNull(w.Tags.Tag("missing"));
        }

        [Test]
        public void Synthetic_DateLikeTagStaysVerbatim()
        {
            Assert.AreEqual("2023-05-01", Synthetic().Ways[401].Tags["check_date"]);
        }

        [Test]
        public void Synthetic_ReadsRelationMembers()
        {
            var r = Synthetic().Relations[201];
            Assert.AreEqual(3, r.Members.Count);
            Assert.AreEqual(OsmElementType.Way, r.Members[0].Type);
            Assert.AreEqual(101, r.Members[0].Ref);
            Assert.AreEqual("inner", r.Members[2].Role);
            Assert.AreEqual("multipolygon", r.Tags["type"]);
        }

        [Test]
        public void Synthetic_NodeTags()
        {
            var d = Synthetic();
            Assert.AreEqual(0, d.Nodes[1].Tags.Count);
            Assert.AreEqual("tree", d.Nodes[301].Tags["natural"]);
        }

        [Test]
        public void OpenWay_IsNotClosed()
        {
            Assert.IsFalse(Synthetic().Ways[101].IsClosed);
        }

        [Test]
        public void MissingElements_Throws()
        {
            Assert.Throws<FormatException>(() => OsmParser.Parse("{\"version\":0.6}"));
        }

        [Test]
        public void Parse_IsCultureInvariant()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("id-ID");
                Assert.AreEqual(107.634108, Synthetic().Nodes[12].Lon, 1e-9);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Test]
        public void Sukabirus_ContainsJalanSukabirus()
        {
            var d = Sukabirus();
            Assert.IsTrue(d.Ways.TryGetValue(95529338, out var w));
            Assert.AreEqual("Jalan Sukabirus", w.Tags["name"]);
            Assert.AreEqual("tertiary", w.Tags["highway"]);
        }

        [Test]
        public void Sukabirus_AllWayNodesResolvable()
        {
            var d = Sukabirus();
            foreach (var w in d.Ways.Values)
                foreach (long id in w.NodeIds)
                    Assert.IsTrue(d.Nodes.ContainsKey(id), $"way {w.Id} node {id}");
        }

        [Test]
        public void Sukabirus_HasBuildings()
        {
            Assert.GreaterOrEqual(Sukabirus().Ways.Values.Count(w => w.Tags.ContainsKey("building")), 5);
        }
    }
}
```

`Assets/WorldGen/Tests/OverpassResponseValidatorTests.cs`:

```csharp
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;

namespace TycoWorld.WorldGen.Tests
{
    public class OverpassResponseValidatorTests
    {
        const string OneNode = "{\"version\":0.6,\"elements\":[{\"type\":\"node\",\"id\":1,\"lat\":0,\"lon\":0}]}";

        [Test]
        public void Valid_ReturnsTrue()
        {
            Assert.IsTrue(OverpassResponseValidator.Validate(OneNode, out string error));
            Assert.IsNull(error);
            Assert.AreEqual(1, OverpassResponseValidator.CountElements(OneNode));
        }

        [Test]
        public void RemarkRuntimeError_IsRejected()
        {
            const string body = "{\"elements\":[{\"type\":\"node\",\"id\":1,\"lat\":0,\"lon\":0}],\"remark\":\"runtime error: Query timed out in \\\"query\\\" at line 3 after 90 seconds.\"}";
            Assert.IsFalse(OverpassResponseValidator.Validate(body, out string error));
            StringAssert.Contains("remark", error);
        }

        [Test]
        public void HtmlPage_IsRejected()
        {
            const string body = "<?xml version=\"1.0\"?><html><body>The server is probably too busy</body></html>";
            Assert.IsFalse(OverpassResponseValidator.Validate(body, out _));
        }

        [Test]
        public void EmptyElements_IsRejected()
        {
            Assert.IsFalse(OverpassResponseValidator.Validate("{\"elements\":[]}", out _));
        }

        [TestCase((string)null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Blank_IsRejected(string body)
        {
            Assert.IsFalse(OverpassResponseValidator.Validate(body, out _));
        }

        [Test]
        public void TruncatedJson_IsRejected()
        {
            Assert.IsFalse(OverpassResponseValidator.Validate("{\"elements\": [", out string error));
            Assert.IsNotNull(error);
        }
    }
}
```

`Assets/WorldGen/Tests/OverpassQueryTests.cs`:

```csharp
using System.Globalization;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;

namespace TycoWorld.WorldGen.Tests
{
    public class OverpassQueryTests
    {
        const string Expected =
            "[out:json][timeout:90][bbox:-6.9850,107.6255,-6.9675,107.6380];\n" +
            "(\n" +
            "  way[\"highway\"];\n" +
            "  way[\"building\"];\n" +
            "  relation[\"building\"];\n" +
            "  way[\"landuse\"];\n" +
            "  relation[\"landuse\"];\n" +
            "  way[\"leisure\"];\n" +
            "  way[\"natural\"];\n" +
            "  relation[\"natural\"=\"water\"];\n" +
            "  way[\"waterway\"];\n" +
            "  way[\"amenity\"~\"^(university|parking|grave_yard)$\"];\n" +
            "  node[\"natural\"=\"tree\"];\n" +
            ");\n" +
            "(._;>;);\n" +
            "out body;\n";

        static readonly GeoBounds SpecBounds = new GeoBounds(-6.9850, 107.6255, -6.9675, 107.6380);

        [Test]
        public void Build_MatchesSpecQueryExactly()
        {
            Assert.AreEqual(Expected, OverpassQuery.Build(SpecBounds, 90));
        }

        [Test]
        public void Build_IsCultureInvariant()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("id-ID");
                Assert.AreEqual(Expected, OverpassQuery.Build(SpecBounds, 90));
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }
    }
}
```

- [ ] **Step 4: Jalankan, pastikan gagal**

Procedure R, lalu Procedure T dengan `Group = "OsmParserTests|OverpassResponseValidatorTests|OverpassQueryTests"`. Expected: COMPILE FAILED (`OsmParser` dkk. tidak ditemukan).

- [ ] **Step 5: Implementasi**

`Assets/WorldGen/Editor/Data/OsmModel.cs`:

```csharp
using System.Collections.Generic;

namespace TycoWorld.WorldGen.Authoring
{
    public static class OsmTags
    {
        public static readonly IReadOnlyDictionary<string, string> Empty = new Dictionary<string, string>();

        /// <summary>Returns the tag value, or null when the tag is absent.</summary>
        public static string Tag(this IReadOnlyDictionary<string, string> tags, string key) =>
            tags != null && tags.TryGetValue(key, out var value) ? value : null;
    }

    public sealed class OsmNode
    {
        public readonly long Id;
        public readonly double Lat, Lon;
        public readonly IReadOnlyDictionary<string, string> Tags;

        public OsmNode(long id, double lat, double lon, IReadOnlyDictionary<string, string> tags)
        {
            Id = id;
            Lat = lat;
            Lon = lon;
            Tags = tags ?? OsmTags.Empty;
        }
    }

    public sealed class OsmWay
    {
        public readonly long Id;
        public readonly List<long> NodeIds;
        public readonly IReadOnlyDictionary<string, string> Tags;

        public OsmWay(long id, List<long> nodeIds, IReadOnlyDictionary<string, string> tags)
        {
            Id = id;
            NodeIds = nodeIds ?? new List<long>();
            Tags = tags ?? OsmTags.Empty;
        }

        public bool IsClosed => NodeIds.Count >= 4 && NodeIds[0] == NodeIds[NodeIds.Count - 1];
    }

    public sealed class OsmMember
    {
        public readonly OsmElementType Type;
        public readonly long Ref;
        public readonly string Role;

        public OsmMember(OsmElementType type, long reference, string role)
        {
            Type = type;
            Ref = reference;
            Role = role ?? "";
        }
    }

    public sealed class OsmRelation
    {
        public readonly long Id;
        public readonly List<OsmMember> Members;
        public readonly IReadOnlyDictionary<string, string> Tags;

        public OsmRelation(long id, List<OsmMember> members, IReadOnlyDictionary<string, string> tags)
        {
            Id = id;
            Members = members ?? new List<OsmMember>();
            Tags = tags ?? OsmTags.Empty;
        }
    }

    public sealed class OsmData
    {
        public readonly Dictionary<long, OsmNode> Nodes = new Dictionary<long, OsmNode>();
        public readonly Dictionary<long, OsmWay> Ways = new Dictionary<long, OsmWay>();
        public readonly Dictionary<long, OsmRelation> Relations = new Dictionary<long, OsmRelation>();
    }
}
```

`Assets/WorldGen/Editor/Data/OsmParser.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Parses Overpass "out body" JSON into OsmData.</summary>
    public static class OsmParser
    {
        public static OsmData Parse(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            var root = Load(json);
            if (!(root["elements"] is JArray elements)) throw new FormatException("Overpass JSON tidak punya array 'elements'.");

            var data = new OsmData();
            foreach (var token in elements)
            {
                if (!(token is JObject e) || e["id"] == null) continue;
                long id = e["id"].Value<long>();
                var tags = ReadTags(e["tags"] as JObject);
                switch ((string)e["type"])
                {
                    case "node":
                        if (e["lat"] == null || e["lon"] == null) continue;
                        data.Nodes[id] = new OsmNode(id, e["lat"].Value<double>(), e["lon"].Value<double>(), tags);
                        break;
                    case "way":
                        var nodeIds = new List<long>();
                        if (e["nodes"] is JArray nodes)
                            foreach (var n in nodes) nodeIds.Add(n.Value<long>());
                        data.Ways[id] = new OsmWay(id, nodeIds, tags);
                        break;
                    case "relation":
                        var members = new List<OsmMember>();
                        if (e["members"] is JArray list)
                            foreach (var m in list)
                                if (m is JObject member && member["ref"] != null && TryParseType((string)member["type"], out var type))
                                    members.Add(new OsmMember(type, member["ref"].Value<long>(), (string)member["role"]));
                        data.Relations[id] = new OsmRelation(id, members, tags);
                        break;
                }
            }
            return data;
        }

        /// <summary>Loads JSON without converting date-like strings (tag values must stay verbatim).</summary>
        internal static JObject Load(string json)
        {
            using (var reader = new JsonTextReader(new StringReader(json))
                   {
                       DateParseHandling = DateParseHandling.None,
                       FloatParseHandling = FloatParseHandling.Double
                   })
            {
                return JObject.Load(reader);
            }
        }

        static IReadOnlyDictionary<string, string> ReadTags(JObject tags)
        {
            if (tags == null || !tags.HasValues) return OsmTags.Empty;
            var result = new Dictionary<string, string>();
            foreach (var property in tags.Properties())
                result[property.Name] = property.Value.Type == JTokenType.String
                    ? (string)property.Value
                    : property.Value.ToString(Formatting.None);
            return result;
        }

        static bool TryParseType(string text, out OsmElementType type)
        {
            switch (text)
            {
                case "node": type = OsmElementType.Node; return true;
                case "way": type = OsmElementType.Way; return true;
                case "relation": type = OsmElementType.Relation; return true;
                default: type = OsmElementType.Node; return false;
            }
        }
    }
}
```

`Assets/WorldGen/Editor/Data/OverpassResponseValidator.cs`:

```csharp
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Rejects Overpass responses that are HTML, broken JSON, runtime errors or empty.</summary>
    public static class OverpassResponseValidator
    {
        public static bool Validate(string body, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(body)) { error = "Respons kosong."; return false; }
            if (body.TrimStart()[0] != '{') { error = "Respons bukan JSON (kemungkinan halaman error HTML)."; return false; }

            JObject root;
            try { root = OsmParser.Load(body); }
            catch (JsonException ex) { error = "JSON rusak: " + ex.Message; return false; }

            var remark = (string)root["remark"];
            if (!string.IsNullOrEmpty(remark) && remark.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                error = "Overpass remark: " + remark;
                return false;
            }
            if (!(root["elements"] is JArray elements) || elements.Count == 0)
            {
                error = "Respons tidak berisi elements.";
                return false;
            }
            return true;
        }

        public static int CountElements(string body) =>
            OsmParser.Load(body)["elements"] is JArray elements ? elements.Count : 0;
    }
}
```

`Assets/WorldGen/Editor/Data/OverpassQuery.cs`:

```csharp
using System.Globalization;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Builds the Overpass QL query from the spec (section 3).</summary>
    public static class OverpassQuery
    {
        const string Body =
            "(\n" +
            "  way[\"highway\"];\n" +
            "  way[\"building\"];\n" +
            "  relation[\"building\"];\n" +
            "  way[\"landuse\"];\n" +
            "  relation[\"landuse\"];\n" +
            "  way[\"leisure\"];\n" +
            "  way[\"natural\"];\n" +
            "  relation[\"natural\"=\"water\"];\n" +
            "  way[\"waterway\"];\n" +
            "  way[\"amenity\"~\"^(university|parking|grave_yard)$\"];\n" +
            "  node[\"natural\"=\"tree\"];\n" +
            ");\n" +
            "(._;>;);\n" +
            "out body;\n";

        public static string Build(GeoBounds bounds, int timeoutSeconds)
        {
            var inv = CultureInfo.InvariantCulture;
            string bbox = string.Format(inv, "{0:0.0000######},{1:0.0000######},{2:0.0000######},{3:0.0000######}",
                bounds.South, bounds.West, bounds.North, bounds.East);
            return string.Format(inv, "[out:json][timeout:{0}][bbox:{1}];\n", timeoutSeconds, bbox) + Body;
        }
    }
}
```

- [ ] **Step 6: Jalankan, pastikan lulus**

Procedure R, lalu Procedure T dengan `Group = "OsmParserTests|OverpassResponseValidatorTests|OverpassQueryTests"`. Expected: `passed=21 failed=0` (11 + 8 + 2).

- [ ] **Step 7: Commit**

Procedure C: `git add Assets/WorldGen`, subject `feat(worldgen): parse Overpass JSON and validate responses`.

---

### Task 5: Geometri inti (poligon, clipping, OBB, PRNG)

**Files:**
- Create: `Assets/WorldGen/Editor/Geometry/Polygon.cs`
- Create: `Assets/WorldGen/Editor/Geometry/Clipping.cs`
- Create: `Assets/WorldGen/Editor/Geometry/ObbCalculator.cs`
- Create: `Assets/WorldGen/Editor/Geometry/SplitMix64.cs`
- Create: `Assets/WorldGen/Editor/Geometry/Xz.cs`
- Test: `Assets/WorldGen/Tests/Polygon2DTests.cs`, `ClippingTests.cs`, `ObbCalculatorTests.cs`, `SplitMix64Tests.cs`

**Interfaces:**
- Produces (namespace `TycoWorld.WorldGen.Authoring`):
  - `sealed class Polygon { Polygon(List<Vector2> outer, List<List<Vector2>> holes = null); List<Vector2> Outer; List<List<Vector2>> Holes; float Area; Rect Bounds; bool Contains(Vector2 p); float DistanceToBoundary(Vector2 p) }`
  - `static class Polygon2D { float Cross(Vector2 a, Vector2 b); float SignedArea(IReadOnlyList<Vector2>); bool IsCcw(...); void EnsureWinding(List<Vector2> ring, bool ccw); Vector2 Centroid(...); bool ContainsPoint(IReadOnlyList<Vector2> ring, Vector2 p); Rect Bounds(...); float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b); float DistanceToRing(IReadOnlyList<Vector2> ring, Vector2 p); List<Vector2> Clean(IReadOnlyList<Vector2> ring, float minSegment = 0.05f, float collinearTolerance = 0.01f); bool IsSelfIntersecting(IReadOnlyList<Vector2> ring) }`
  - `sealed class PolylinePiece { List<Vector2> Points; bool StartsAtOriginalStart; bool EndsAtOriginalEnd }`
  - `static class Clipping { List<PolylinePiece> ClipPolyline(IReadOnlyList<Vector2> points, Rect rect); bool ClipSegment(Rect rect, ref Vector2 a, ref Vector2 b, out bool aClipped, out bool bClipped); List<Vector2> ClipPolygon(IReadOnlyList<Vector2> ring, Rect rect) }`
  - `struct Obb { Vector2 Center, AxisU, AxisV; float HalfU, HalfV; float Area; Vector2 Corner(int signU, int signV) }` (AxisU = sumbu panjang; AxisV = AxisU diputar +90°)
  - `static class ObbCalculator { Obb Compute(IReadOnlyList<Vector2> points); List<Vector2> ConvexHull(IReadOnlyList<Vector2> points); float YawDegreesForLocalX(Vector2 axis) }`
  - `sealed class SplitMix64 { SplitMix64(ulong seed); static SplitMix64 For(long id, string salt); static ulong Hash(long id, string salt); static ulong Fnv1a64(string text); static int Pick(long id, string salt, int count); ulong NextULong(); double NextDouble(); float Range(float min, float max); int RangeInclusive(int min, int max) }`
  - `static class Xz { Vector3 At(Vector2 p, float y); Vector3 Dir(Vector2 d) }`

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/WorldGen/Tests/Polygon2DTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class Polygon2DTests
    {
        static List<Vector2> Square(float size) => new List<Vector2>
            { new Vector2(0, 0), new Vector2(size, 0), new Vector2(size, size), new Vector2(0, size) };

        static List<Vector2> LShape() => new List<Vector2>
            { new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 4), new Vector2(4, 4), new Vector2(4, 10), new Vector2(0, 10) };

        [Test]
        public void SignedArea_CcwPositive_CwNegative()
        {
            var sq = Square(10);
            Assert.AreEqual(100f, Polygon2D.SignedArea(sq), 1e-4f);
            sq.Reverse();
            Assert.AreEqual(-100f, Polygon2D.SignedArea(sq), 1e-4f);
        }

        [Test]
        public void EnsureWinding_ReversesWhenNeeded()
        {
            var sq = Square(10);
            sq.Reverse();
            Polygon2D.EnsureWinding(sq, true);
            Assert.IsTrue(Polygon2D.IsCcw(sq));
        }

        [Test]
        public void Centroid_OfLShape()
        {
            var c = Polygon2D.Centroid(LShape());
            Assert.AreEqual(3.875f, c.x, 1e-4f);
            Assert.AreEqual(3.875f, c.y, 1e-4f);
        }

        [Test]
        public void ContainsPoint_InsideOutside()
        {
            var l = LShape();
            Assert.IsTrue(Polygon2D.ContainsPoint(l, new Vector2(2, 8)));
            Assert.IsFalse(Polygon2D.ContainsPoint(l, new Vector2(8, 8)));
        }

        [Test]
        public void Polygon_HoleExcludesPoint()
        {
            var hole = new List<Vector2> { new Vector2(3, 3), new Vector2(3, 7), new Vector2(7, 7), new Vector2(7, 3) };
            var poly = new Polygon(Square(10), new List<List<Vector2>> { hole });
            Assert.IsFalse(poly.Contains(new Vector2(5, 5)));
            Assert.IsTrue(poly.Contains(new Vector2(1, 1)));
            Assert.AreEqual(84f, poly.Area, 1e-4f);
            Assert.AreEqual(1f, poly.DistanceToBoundary(new Vector2(1, 5)), 1e-4f);
        }

        [Test]
        public void Clean_RemovesDuplicatesCollinearAndClosingPoint()
        {
            var input = new List<Vector2>
            {
                new Vector2(0, 0), new Vector2(5, 0), new Vector2(10, 0), new Vector2(10, 10),
                new Vector2(10, 10.01f), new Vector2(0, 10), new Vector2(0, 0)
            };
            var cleaned = Polygon2D.Clean(input);
            Assert.AreEqual(4, cleaned.Count);
            Assert.AreEqual(100f, Mathf.Abs(Polygon2D.SignedArea(cleaned)), 0.2f);
        }

        [Test]
        public void IsSelfIntersecting_DetectsBowtie()
        {
            var bowtie = new List<Vector2> { new Vector2(0, 0), new Vector2(10, 10), new Vector2(10, 0), new Vector2(0, 10) };
            Assert.IsTrue(Polygon2D.IsSelfIntersecting(bowtie));
            Assert.IsFalse(Polygon2D.IsSelfIntersecting(Square(10)));
            Assert.IsFalse(Polygon2D.IsSelfIntersecting(LShape()));
        }

        [Test]
        public void DistanceToSegment_ClampsToEnds()
        {
            Assert.AreEqual(5f, Polygon2D.DistanceToSegment(new Vector2(5, 5), new Vector2(0, 0), new Vector2(10, 0)), 1e-5f);
            Assert.AreEqual(5f, Polygon2D.DistanceToSegment(new Vector2(-3, 4), new Vector2(0, 0), new Vector2(10, 0)), 1e-5f);
        }

        [Test]
        public void Bounds_CoversAllPoints()
        {
            var b = Polygon2D.Bounds(LShape());
            Assert.AreEqual(Rect.MinMaxRect(0, 0, 10, 10), b);
        }
    }
}
```

`Assets/WorldGen/Tests/ClippingTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class ClippingTests
    {
        static readonly Rect Box = Rect.MinMaxRect(0, 0, 10, 10);

        static void AssertNear(Vector2 expected, Vector2 actual) =>
            Assert.Less(Vector2.Distance(expected, actual), 1e-4f, $"expected {expected} got {actual}");

        [Test]
        public void Polyline_FullyInside_KeepsBothEnds()
        {
            var pieces = Clipping.ClipPolyline(new[] { new Vector2(1, 1), new Vector2(5, 5), new Vector2(9, 1) }, Box);
            Assert.AreEqual(1, pieces.Count);
            Assert.AreEqual(3, pieces[0].Points.Count);
            Assert.IsTrue(pieces[0].StartsAtOriginalStart);
            Assert.IsTrue(pieces[0].EndsAtOriginalEnd);
        }

        [Test]
        public void Polyline_CrossingInAndOut_IsCut()
        {
            var pieces = Clipping.ClipPolyline(new[] { new Vector2(-10, 5), new Vector2(5, 5), new Vector2(5, 20) }, Box);
            Assert.AreEqual(1, pieces.Count);
            var p = pieces[0].Points;
            AssertNear(new Vector2(0, 5), p[0]);
            AssertNear(new Vector2(5, 5), p[1]);
            AssertNear(new Vector2(5, 10), p[2]);
            Assert.IsFalse(pieces[0].StartsAtOriginalStart);
            Assert.IsFalse(pieces[0].EndsAtOriginalEnd);
        }

        [Test]
        public void Polyline_ExitAndReenter_MakesTwoPieces()
        {
            var pieces = Clipping.ClipPolyline(
                new[] { new Vector2(2, 2), new Vector2(20, 2), new Vector2(20, 8), new Vector2(2, 8) }, Box);
            Assert.AreEqual(2, pieces.Count);
            Assert.IsTrue(pieces[0].StartsAtOriginalStart);
            Assert.IsFalse(pieces[0].EndsAtOriginalEnd);
            AssertNear(new Vector2(10, 2), pieces[0].Points[1]);
            Assert.IsFalse(pieces[1].StartsAtOriginalStart);
            Assert.IsTrue(pieces[1].EndsAtOriginalEnd);
            AssertNear(new Vector2(10, 8), pieces[1].Points[0]);
        }

        [Test]
        public void Polyline_FullyOutside_ReturnsNothing()
        {
            Assert.AreEqual(0, Clipping.ClipPolyline(new[] { new Vector2(20, 20), new Vector2(30, 20) }, Box).Count);
        }

        [Test]
        public void Polygon_PartiallyOutside_IsTrimmed()
        {
            var square = new List<Vector2> { new Vector2(5, 5), new Vector2(15, 5), new Vector2(15, 15), new Vector2(5, 15) };
            var clipped = Clipping.ClipPolygon(square, Box);
            Assert.AreEqual(25f, Mathf.Abs(Polygon2D.SignedArea(clipped)), 1e-3f);
        }

        [Test]
        public void Polygon_FullyOutside_IsEmpty()
        {
            var square = new List<Vector2> { new Vector2(20, 20), new Vector2(30, 20), new Vector2(30, 30), new Vector2(20, 30) };
            Assert.AreEqual(0, Clipping.ClipPolygon(square, Box).Count);
        }
    }
}
```

`Assets/WorldGen/Tests/ObbCalculatorTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class ObbCalculatorTests
    {
        static List<Vector2> RotatedRect(float w, float h, float degrees, Vector2 center)
        {
            var rot = Quaternion.Euler(0, 0, degrees);
            var pts = new List<Vector2>();
            foreach (var c in new[] { new Vector2(-w / 2, -h / 2), new Vector2(w / 2, -h / 2), new Vector2(w / 2, h / 2), new Vector2(-w / 2, h / 2) })
                pts.Add(center + (Vector2)(rot * c));
            return pts;
        }

        [Test]
        public void RotatedRectangle_RecoversSizeAndAxis()
        {
            var obb = ObbCalculator.Compute(RotatedRect(12, 8, 30, new Vector2(3, 4)));
            Assert.AreEqual(96f, obb.Area, 0.01f);
            Assert.AreEqual(6f, obb.HalfU, 1e-3f);
            Assert.AreEqual(4f, obb.HalfV, 1e-3f);
            var expectedAxis = new Vector2(Mathf.Cos(30 * Mathf.Deg2Rad), Mathf.Sin(30 * Mathf.Deg2Rad));
            Assert.Greater(Mathf.Abs(Vector2.Dot(obb.AxisU, expectedAxis)), 0.999f);
            Assert.AreEqual(3f, obb.Center.x, 1e-3f);
            Assert.AreEqual(4f, obb.Center.y, 1e-3f);
        }

        [Test]
        public void AxisV_IsAxisURotatedCounterClockwise()
        {
            var obb = ObbCalculator.Compute(RotatedRect(4, 10, 75, Vector2.zero));
            Assert.AreEqual(1f, Polygon2D.Cross(obb.AxisU, obb.AxisV), 1e-4f);
            Assert.GreaterOrEqual(obb.HalfU, obb.HalfV);
        }

        [Test]
        public void ConvexHull_DropsInteriorPoint()
        {
            var pts = new List<Vector2> { new Vector2(0, 0), new Vector2(10, 0), new Vector2(5, 5), new Vector2(10, 10), new Vector2(0, 10) };
            Assert.AreEqual(4, ObbCalculator.ConvexHull(pts).Count);
        }

        [TestCase(1f, 0f, 0f)]
        [TestCase(0f, 1f, -90f)]
        [TestCase(0f, -1f, 90f)]
        public void YawForLocalX_MapsAxis(float x, float y, float expectedYaw)
        {
            Assert.AreEqual(expectedYaw, ObbCalculator.YawDegreesForLocalX(new Vector2(x, y)), 1e-3f);
        }
    }
}
```

`Assets/WorldGen/Tests/SplitMix64Tests.cs`:

```csharp
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;

namespace TycoWorld.WorldGen.Tests
{
    public class SplitMix64Tests
    {
        [Test]
        public void SeedZero_MatchesReferenceSequence()
        {
            var rng = new SplitMix64(0);
            Assert.AreEqual(0xE220A8397B1DCDAFUL, rng.NextULong());
            Assert.AreEqual(0x6E789E6AA1B965F4UL, rng.NextULong());
            Assert.AreEqual(0x06C45D188009454FUL, rng.NextULong());
        }

        [Test]
        public void Fnv1a64_MatchesReference()
        {
            Assert.AreEqual(0xCBF29CE484222325UL, SplitMix64.Fnv1a64(""));
            Assert.AreEqual(0xAF63DC4C8601EC8CUL, SplitMix64.Fnv1a64("a"));
        }

        [Test]
        public void For_IsDeterministicAndSaltSensitive()
        {
            Assert.AreEqual(SplitMix64.Hash(401, "levels"), SplitMix64.Hash(401, "levels"));
            Assert.AreNotEqual(SplitMix64.Hash(401, "levels"), SplitMix64.Hash(401, "upper"));
        }

        [Test]
        public void Range_StaysInBounds()
        {
            var rng = new SplitMix64(42);
            for (int i = 0; i < 1000; i++)
            {
                float v = rng.Range(0.8f, 1.2f);
                Assert.GreaterOrEqual(v, 0.8f);
                Assert.Less(v, 1.2f);
            }
        }

        [Test]
        public void RangeInclusive_HitsBothEnds()
        {
            var rng = new SplitMix64(7);
            bool sawMin = false, sawMax = false;
            for (int i = 0; i < 1000; i++)
            {
                int v = rng.RangeInclusive(1, 3);
                Assert.That(v, Is.InRange(1, 3));
                sawMin |= v == 1;
                sawMax |= v == 3;
            }
            Assert.IsTrue(sawMin && sawMax);
        }

        [Test]
        public void Pick_HandlesEmptyAndRange()
        {
            Assert.AreEqual(-1, SplitMix64.Pick(5, "x", 0));
            Assert.That(SplitMix64.Pick(5, "x", 6), Is.InRange(0, 5));
        }
    }
}
```

- [ ] **Step 2: Jalankan, pastikan gagal**

Procedure R, lalu Procedure T dengan `Group = "Polygon2DTests|ClippingTests|ObbCalculatorTests|SplitMix64Tests"`. Expected: COMPILE FAILED.

- [ ] **Step 3: Implementasi**

`Assets/WorldGen/Editor/Geometry/Polygon.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Polygon on the ground plane (Vector2.x = east, Vector2.y = north). Rings have no closing duplicate.</summary>
    public sealed class Polygon
    {
        public readonly List<Vector2> Outer;
        public readonly List<List<Vector2>> Holes;

        public Polygon(List<Vector2> outer, List<List<Vector2>> holes = null)
        {
            Outer = outer ?? new List<Vector2>();
            Holes = holes ?? new List<List<Vector2>>();
        }

        public float Area
        {
            get
            {
                float area = Mathf.Abs(Polygon2D.SignedArea(Outer));
                foreach (var hole in Holes) area -= Mathf.Abs(Polygon2D.SignedArea(hole));
                return area;
            }
        }

        public Rect Bounds => Polygon2D.Bounds(Outer);

        public bool Contains(Vector2 p)
        {
            if (!Polygon2D.ContainsPoint(Outer, p)) return false;
            foreach (var hole in Holes)
                if (Polygon2D.ContainsPoint(hole, p)) return false;
            return true;
        }

        public float DistanceToBoundary(Vector2 p)
        {
            float d = Polygon2D.DistanceToRing(Outer, p);
            foreach (var hole in Holes) d = Mathf.Min(d, Polygon2D.DistanceToRing(hole, p));
            return d;
        }
    }

    public static class Polygon2D
    {
        public static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        public static float SignedArea(IReadOnlyList<Vector2> ring)
        {
            double sum = 0;
            for (int i = 0, n = ring.Count; i < n; i++)
            {
                Vector2 p = ring[i], q = ring[(i + 1) % n];
                sum += (double)p.x * q.y - (double)q.x * p.y;
            }
            return (float)(sum * 0.5);
        }

        public static bool IsCcw(IReadOnlyList<Vector2> ring) => SignedArea(ring) > 0f;

        public static void EnsureWinding(List<Vector2> ring, bool ccw)
        {
            if (ring.Count >= 3 && IsCcw(ring) != ccw) ring.Reverse();
        }

        public static Vector2 Centroid(IReadOnlyList<Vector2> ring)
        {
            double a = 0, cx = 0, cy = 0;
            for (int i = 0, n = ring.Count; i < n; i++)
            {
                Vector2 p = ring[i], q = ring[(i + 1) % n];
                double cross = (double)p.x * q.y - (double)q.x * p.y;
                a += cross;
                cx += (p.x + q.x) * cross;
                cy += (p.y + q.y) * cross;
            }
            if (Math.Abs(a) < 1e-9)
            {
                var sum = Vector2.zero;
                foreach (var p in ring) sum += p;
                return ring.Count > 0 ? sum / ring.Count : Vector2.zero;
            }
            return new Vector2((float)(cx / (3 * a)), (float)(cy / (3 * a)));
        }

        public static bool ContainsPoint(IReadOnlyList<Vector2> ring, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
            {
                Vector2 a = ring[i], b = ring[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        public static Rect Bounds(IReadOnlyList<Vector2> points)
        {
            if (points.Count == 0) return Rect.zero;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var p in points)
            {
                if (p.x < minX) minX = p.x;
                if (p.y < minY) minY = p.y;
                if (p.x > maxX) maxX = p.x;
                if (p.y > maxY) maxY = p.y;
            }
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            if (lengthSq < 1e-12f) return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSq);
            return Vector2.Distance(p, a + ab * t);
        }

        public static float DistanceToRing(IReadOnlyList<Vector2> ring, Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0, n = ring.Count; i < n; i++)
                best = Mathf.Min(best, DistanceToSegment(p, ring[i], ring[(i + 1) % n]));
            return best;
        }

        /// <summary>Removes the closing duplicate, near-duplicate points and collinear points.</summary>
        public static List<Vector2> Clean(IReadOnlyList<Vector2> ring, float minSegment = 0.05f, float collinearTolerance = 0.01f)
        {
            var pts = new List<Vector2>(ring);
            float minSq = minSegment * minSegment;
            if (pts.Count > 1 && (pts[0] - pts[pts.Count - 1]).sqrMagnitude < minSq) pts.RemoveAt(pts.Count - 1);
            bool changed = true;
            while (changed && pts.Count >= 3)
            {
                changed = false;
                for (int i = 0; i < pts.Count && pts.Count >= 3; i++)
                {
                    Vector2 prev = pts[(i - 1 + pts.Count) % pts.Count], cur = pts[i], next = pts[(i + 1) % pts.Count];
                    if ((cur - prev).sqrMagnitude < minSq || DistanceToSegment(cur, prev, next) < collinearTolerance)
                    {
                        pts.RemoveAt(i);
                        changed = true;
                        i--;
                    }
                }
            }
            return pts;
        }

        public static bool IsSelfIntersecting(IReadOnlyList<Vector2> ring)
        {
            int n = ring.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = ring[i], b = ring[(i + 1) % n];
                for (int j = i + 2; j < n; j++)
                {
                    if (i == 0 && j == n - 1) continue;
                    if (SegmentsCross(a, b, ring[j], ring[(j + 1) % n])) return true;
                }
            }
            return false;
        }

        static bool SegmentsCross(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float d1 = Cross(d - c, a - c), d2 = Cross(d - c, b - c);
            float d3 = Cross(b - a, c - a), d4 = Cross(b - a, d - a);
            return ((d1 > 0f && d2 < 0f) || (d1 < 0f && d2 > 0f)) && ((d3 > 0f && d4 < 0f) || (d3 < 0f && d4 > 0f));
        }
    }
}
```

`Assets/WorldGen/Editor/Geometry/Clipping.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    public sealed class PolylinePiece
    {
        public readonly List<Vector2> Points = new List<Vector2>();
        public bool StartsAtOriginalStart;
        public bool EndsAtOriginalEnd;
    }

    /// <summary>Liang–Barsky line clipping and Sutherland–Hodgman polygon clipping against a rectangle.</summary>
    public static class Clipping
    {
        public static List<PolylinePiece> ClipPolyline(IReadOnlyList<Vector2> points, Rect rect)
        {
            var pieces = new List<PolylinePiece>();
            PolylinePiece current = null;
            for (int i = 0; i + 1 < points.Count; i++)
            {
                Vector2 a = points[i], b = points[i + 1];
                if (!ClipSegment(rect, ref a, ref b, out bool aClipped, out bool bClipped))
                {
                    current = null;
                    continue;
                }
                if (current == null || aClipped)
                {
                    current = new PolylinePiece { StartsAtOriginalStart = i == 0 && !aClipped };
                    current.Points.Add(a);
                    pieces.Add(current);
                }
                current.Points.Add(b);
                current.EndsAtOriginalEnd = i + 1 == points.Count - 1 && !bClipped;
                if (bClipped) current = null;
            }
            return pieces;
        }

        public static bool ClipSegment(Rect rect, ref Vector2 a, ref Vector2 b, out bool aClipped, out bool bClipped)
        {
            float t0 = 0f, t1 = 1f;
            Vector2 d = b - a;
            if (!Clip(-d.x, a.x - rect.xMin, ref t0, ref t1) || !Clip(d.x, rect.xMax - a.x, ref t0, ref t1) ||
                !Clip(-d.y, a.y - rect.yMin, ref t0, ref t1) || !Clip(d.y, rect.yMax - a.y, ref t0, ref t1))
            {
                aClipped = bClipped = true;
                return false;
            }
            aClipped = t0 > 0f;
            bClipped = t1 < 1f;
            Vector2 start = a + d * t0, end = a + d * t1;
            a = start;
            b = end;
            return true;
        }

        static bool Clip(float p, float q, ref float t0, ref float t1)
        {
            if (p == 0f) return q >= 0f;
            float t = q / p;
            if (p < 0f)
            {
                if (t > t1) return false;
                if (t > t0) t0 = t;
            }
            else
            {
                if (t < t0) return false;
                if (t < t1) t1 = t;
            }
            return true;
        }

        public static List<Vector2> ClipPolygon(IReadOnlyList<Vector2> ring, Rect rect)
        {
            var output = new List<Vector2>(ring);
            output = ClipEdge(output, p => p.x >= rect.xMin, (a, b) => AtX(a, b, rect.xMin));
            output = ClipEdge(output, p => p.x <= rect.xMax, (a, b) => AtX(a, b, rect.xMax));
            output = ClipEdge(output, p => p.y >= rect.yMin, (a, b) => AtY(a, b, rect.yMin));
            output = ClipEdge(output, p => p.y <= rect.yMax, (a, b) => AtY(a, b, rect.yMax));
            return output;
        }

        static List<Vector2> ClipEdge(List<Vector2> input, Func<Vector2, bool> inside, Func<Vector2, Vector2, Vector2> intersect)
        {
            var output = new List<Vector2>();
            if (input.Count == 0) return output;
            Vector2 prev = input[input.Count - 1];
            foreach (var cur in input)
            {
                bool curIn = inside(cur), prevIn = inside(prev);
                if (curIn)
                {
                    if (!prevIn) output.Add(intersect(prev, cur));
                    output.Add(cur);
                }
                else if (prevIn)
                {
                    output.Add(intersect(prev, cur));
                }
                prev = cur;
            }
            return output;
        }

        static Vector2 AtX(Vector2 a, Vector2 b, float x) => new Vector2(x, a.y + (b.y - a.y) * (x - a.x) / (b.x - a.x));

        static Vector2 AtY(Vector2 a, Vector2 b, float y) => new Vector2(a.x + (b.x - a.x) * (y - a.y) / (b.y - a.y), y);
    }
}
```

`Assets/WorldGen/Editor/Geometry/ObbCalculator.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Oriented bounding box. AxisU is the long axis; AxisV is AxisU rotated +90 degrees.</summary>
    public struct Obb
    {
        public Vector2 Center, AxisU, AxisV;
        public float HalfU, HalfV;

        public float Area => 4f * HalfU * HalfV;

        public Vector2 Corner(int signU, int signV) => Center + AxisU * (HalfU * signU) + AxisV * (HalfV * signV);
    }

    public static class ObbCalculator
    {
        /// <summary>Minimum-area OBB via rotating calipers over the convex hull.</summary>
        public static Obb Compute(IReadOnlyList<Vector2> points)
        {
            var hull = ConvexHull(points);
            if (hull.Count < 3)
            {
                var b = Polygon2D.Bounds(points);
                return Normalize(new Obb { Center = b.center, AxisU = Vector2.right, AxisV = Vector2.up, HalfU = b.width / 2f, HalfV = b.height / 2f });
            }

            var best = new Obb();
            float bestArea = float.MaxValue;
            for (int i = 0; i < hull.Count; i++)
            {
                var edge = hull[(i + 1) % hull.Count] - hull[i];
                if (edge.sqrMagnitude < 1e-10f) continue;
                var u = edge.normalized;
                var v = new Vector2(-u.y, u.x);
                float minU = float.MaxValue, maxU = float.MinValue, minV = float.MaxValue, maxV = float.MinValue;
                foreach (var p in hull)
                {
                    float pu = Vector2.Dot(p, u), pv = Vector2.Dot(p, v);
                    if (pu < minU) minU = pu;
                    if (pu > maxU) maxU = pu;
                    if (pv < minV) minV = pv;
                    if (pv > maxV) maxV = pv;
                }
                float area = (maxU - minU) * (maxV - minV);
                if (area < bestArea - 1e-4f)
                {
                    bestArea = area;
                    best = new Obb
                    {
                        AxisU = u, AxisV = v,
                        HalfU = (maxU - minU) / 2f, HalfV = (maxV - minV) / 2f,
                        Center = u * ((minU + maxU) / 2f) + v * ((minV + maxV) / 2f)
                    };
                }
            }
            return Normalize(best);
        }

        static Obb Normalize(Obb obb)
        {
            if (obb.HalfV <= obb.HalfU) return obb;
            return new Obb { Center = obb.Center, AxisU = obb.AxisV, AxisV = -obb.AxisU, HalfU = obb.HalfV, HalfV = obb.HalfU };
        }

        /// <summary>Andrew's monotone chain. Returns a CCW hull without duplicate end point.</summary>
        public static List<Vector2> ConvexHull(IReadOnlyList<Vector2> points)
        {
            var pts = new List<Vector2>(points);
            pts.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            if (pts.Count < 3) return pts;
            var hull = new List<Vector2>(pts.Count * 2);
            foreach (var p in pts)
            {
                while (hull.Count >= 2 && Polygon2D.Cross(hull[hull.Count - 1] - hull[hull.Count - 2], p - hull[hull.Count - 2]) <= 0f)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(p);
            }
            int lower = hull.Count + 1;
            for (int i = pts.Count - 2; i >= 0; i--)
            {
                var p = pts[i];
                while (hull.Count >= lower && Polygon2D.Cross(hull[hull.Count - 1] - hull[hull.Count - 2], p - hull[hull.Count - 2]) <= 0f)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(p);
            }
            hull.RemoveAt(hull.Count - 1);
            return hull;
        }

        /// <summary>Yaw (degrees, Unity Y rotation) that turns local +X onto the given ground-plane axis.</summary>
        public static float YawDegreesForLocalX(Vector2 axis) => Mathf.Atan2(-axis.y, axis.x) * Mathf.Rad2Deg;
    }
}
```

`Assets/WorldGen/Editor/Geometry/SplitMix64.cs`:

```csharp
namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Deterministic PRNG (SplitMix64). All generator randomness must come from here.</summary>
    public sealed class SplitMix64
    {
        ulong state;

        public SplitMix64(ulong seed) { state = seed; }

        public static SplitMix64 For(long id, string salt) => new SplitMix64(unchecked((ulong)id) ^ Fnv1a64(salt));

        public static ulong Hash(long id, string salt) => For(id, salt).NextULong();

        public static int Pick(long id, string salt, int count) =>
            count <= 0 ? -1 : (int)(Hash(id, salt) % (ulong)count);

        public static ulong Fnv1a64(string text)
        {
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                if (text != null)
                    foreach (char c in text)
                    {
                        hash ^= c;
                        hash *= 1099511628211UL;
                    }
                return hash;
            }
        }

        public ulong NextULong()
        {
            unchecked
            {
                ulong z = state += 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));

        public float Range(float min, float max) => (float)(min + (max - min) * NextDouble());

        public int RangeInclusive(int min, int max) => min + (int)(NextULong() % (ulong)(max - min + 1));
    }
}
```

`Assets/WorldGen/Editor/Geometry/Xz.cs`:

```csharp
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Maps ground-plane Vector2 (x = east, y = north) to Unity 3D (x, height, z = north).</summary>
    public static class Xz
    {
        public static Vector3 At(Vector2 p, float y) => new Vector3(p.x, y, p.y);

        public static Vector3 Dir(Vector2 d) => new Vector3(d.x, 0f, d.y);
    }
}
```

- [ ] **Step 4: Jalankan, pastikan lulus**

Procedure R, lalu Procedure T dengan `Group = "Polygon2DTests|ClippingTests|ObbCalculatorTests|SplitMix64Tests"`. Expected: `passed=27 failed=0` (9 + 6 + 6 + 6).

- [ ] **Step 5: Commit**

Procedure C: `git add Assets/WorldGen`, subject `feat(worldgen): add polygon, clipping, OBB and SplitMix64 utilities`.

---

### Task 6: Perangkai ring multipolygon + triangulasi ear clipping

**Files:**
- Create: `Assets/WorldGen/Editor/Geometry/RingAssembler.cs`
- Create: `Assets/WorldGen/Editor/Geometry/Triangulator.cs`
- Test: `Assets/WorldGen/Tests/RingAssemblerTests.cs`, `Assets/WorldGen/Tests/TriangulatorTests.cs`

**Interfaces:**
- Consumes: `Polygon`, `Polygon2D` (Task 5).
- Produces:
  - `static class RingAssembler { bool TryAssemble(IReadOnlyList<IReadOnlyList<long>> ways, out List<List<long>> rings) }` — tiap ring tertutup (elemen pertama == terakhir).
  - `static class Triangulator { bool Triangulate(Polygon polygon, List<Vector2> vertices, List<int> triangles) }` — `triangles` berisi triple indeks ke `vertices`, setiap segitiga CCW di 2D.

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/WorldGen/Tests/RingAssemblerTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;

namespace TycoWorld.WorldGen.Tests
{
    public class RingAssemblerTests
    {
        static List<IReadOnlyList<long>> Ways(params long[][] ways) => new List<IReadOnlyList<long>>(ways);

        [Test]
        public void SingleClosedWay_IsOneRing()
        {
            Assert.IsTrue(RingAssembler.TryAssemble(Ways(new long[] { 1, 2, 3, 1 }), out var rings));
            Assert.AreEqual(1, rings.Count);
            CollectionAssert.AreEqual(new long[] { 1, 2, 3, 1 }, rings[0]);
        }

        [Test]
        public void TwoWays_JoinWithReversal()
        {
            Assert.IsTrue(RingAssembler.TryAssemble(Ways(new long[] { 1, 2, 3 }, new long[] { 1, 4, 3 }), out var rings));
            CollectionAssert.AreEqual(new long[] { 1, 2, 3, 4, 1 }, rings[0]);
        }

        [Test]
        public void ThreeWayChain_Closes()
        {
            Assert.IsTrue(RingAssembler.TryAssemble(Ways(new long[] { 1, 2 }, new long[] { 2, 3 }, new long[] { 3, 1 }), out var rings));
            CollectionAssert.AreEqual(new long[] { 1, 2, 3, 1 }, rings[0]);
        }

        [Test]
        public void OpenChain_Fails()
        {
            Assert.IsFalse(RingAssembler.TryAssemble(Ways(new long[] { 1, 2 }, new long[] { 2, 3 }), out _));
        }

        [Test]
        public void TwoSeparateRings()
        {
            Assert.IsTrue(RingAssembler.TryAssemble(Ways(new long[] { 1, 2, 3, 1 }, new long[] { 4, 5, 6, 4 }), out var rings));
            Assert.AreEqual(2, rings.Count);
        }

        [Test]
        public void Empty_Succeeds()
        {
            Assert.IsTrue(RingAssembler.TryAssemble(Ways(), out var rings));
            Assert.AreEqual(0, rings.Count);
        }
    }
}
```

`Assets/WorldGen/Tests/TriangulatorTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class TriangulatorTests
    {
        static List<Vector2> V(params float[] xy)
        {
            var list = new List<Vector2>();
            for (int i = 0; i < xy.Length; i += 2) list.Add(new Vector2(xy[i], xy[i + 1]));
            return list;
        }

        static float SignedTriangleArea(List<Vector2> v, List<int> t)
        {
            float sum = 0f;
            for (int i = 0; i < t.Count; i += 3)
                sum += Polygon2D.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]) * 0.5f;
            return sum;
        }

        static (List<Vector2> v, List<int> t) Run(Polygon polygon)
        {
            var v = new List<Vector2>();
            var t = new List<int>();
            Assert.IsTrue(Triangulator.Triangulate(polygon, v, t));
            return (v, t);
        }

        static List<Vector2> Comb() => V(0, 0, 10, 0, 10, 10, 8, 10, 8, 2, 6, 2, 6, 10, 4, 10, 4, 2, 2, 2, 2, 10, 0, 10);

        [Test]
        public void Square_TwoTriangles()
        {
            var (v, t) = Run(new Polygon(V(0, 0, 10, 0, 10, 10, 0, 10)));
            Assert.AreEqual(2, t.Count / 3);
            Assert.AreEqual(100f, SignedTriangleArea(v, t), 1e-3f);
        }

        [Test]
        public void LShape_FourTriangles()
        {
            var (v, t) = Run(new Polygon(V(0, 0, 10, 0, 10, 4, 4, 4, 4, 10, 0, 10)));
            Assert.AreEqual(4, t.Count / 3);
            Assert.AreEqual(64f, SignedTriangleArea(v, t), 1e-3f);
        }

        [Test]
        public void ClockwiseOuter_StillProducesCcwTriangles()
        {
            var (v, t) = Run(new Polygon(V(0, 0, 0, 10, 10, 10, 10, 0)));
            Assert.AreEqual(100f, SignedTriangleArea(v, t), 1e-3f);
        }

        [Test]
        public void SquareWithHole_CoversRingOnly()
        {
            var poly = new Polygon(V(0, 0, 10, 0, 10, 10, 0, 10), new List<List<Vector2>> { V(3, 3, 7, 3, 7, 7, 3, 7) });
            var (v, t) = Run(poly);
            Assert.AreEqual(8, t.Count / 3);
            Assert.AreEqual(84f, SignedTriangleArea(v, t), 1e-3f);
            for (int i = 0; i < t.Count; i += 3)
            {
                var centroid = (v[t[i]] + v[t[i + 1]] + v[t[i + 2]]) / 3f;
                Assert.IsFalse(centroid.x > 3f && centroid.x < 7f && centroid.y > 3f && centroid.y < 7f, "triangle inside hole");
            }
        }

        [Test]
        public void TwoHoles_AreaPreserved()
        {
            var poly = new Polygon(V(0, 0, 20, 0, 20, 10, 0, 10),
                new List<List<Vector2>> { V(2, 2, 6, 2, 6, 6, 2, 6), V(12, 3, 16, 3, 16, 7, 12, 7) });
            var (v, t) = Run(poly);
            Assert.AreEqual(14, t.Count / 3);
            Assert.AreEqual(168f, SignedTriangleArea(v, t), 1e-3f);
        }

        [Test]
        public void Comb_AreaPreserved()
        {
            var (v, t) = Run(new Polygon(Comb()));
            Assert.AreEqual(10, t.Count / 3);
            Assert.AreEqual(68f, SignedTriangleArea(v, t), 1e-3f);
        }

        [Test]
        public void AllTriangles_AreCcw()
        {
            var (v, t) = Run(new Polygon(Comb()));
            for (int i = 0; i < t.Count; i += 3)
                Assert.Greater(Polygon2D.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]), 0f);
        }

        [Test]
        public void Bowtie_DoesNotThrow()
        {
            var v = new List<Vector2>();
            var t = new List<int>();
            Assert.DoesNotThrow(() => Triangulator.Triangulate(new Polygon(V(0, 0, 10, 10, 10, 0, 0, 10)), v, t));
        }

        [Test]
        public void Degenerate_ReturnsFalse()
        {
            Assert.IsFalse(Triangulator.Triangulate(new Polygon(V(0, 0, 1, 1)), new List<Vector2>(), new List<int>()));
        }
    }
}
```

- [ ] **Step 2: Jalankan, pastikan gagal**

Procedure R, lalu Procedure T dengan `Group = "RingAssemblerTests|TriangulatorTests"`. Expected: COMPILE FAILED.

- [ ] **Step 3: Implementasi**

`Assets/WorldGen/Editor/Geometry/RingAssembler.cs`:

```csharp
using System.Collections.Generic;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Joins multipolygon member ways (node id sequences) into closed rings.</summary>
    public static class RingAssembler
    {
        public static bool TryAssemble(IReadOnlyList<IReadOnlyList<long>> ways, out List<List<long>> rings)
        {
            rings = new List<List<long>>();
            var pool = new List<List<long>>();
            foreach (var way in ways)
                if (way != null && way.Count >= 2) pool.Add(new List<long>(way));

            while (pool.Count > 0)
            {
                var ring = pool[0];
                pool.RemoveAt(0);
                while (!(ring.Count >= 4 && ring[0] == ring[ring.Count - 1]))
                {
                    long tail = ring[ring.Count - 1];
                    int found = -1;
                    bool reverse = false;
                    for (int i = 0; i < pool.Count; i++)
                    {
                        if (pool[i][0] == tail) { found = i; break; }
                        if (pool[i][pool[i].Count - 1] == tail) { found = i; reverse = true; break; }
                    }
                    if (found < 0) return false;
                    var next = pool[found];
                    pool.RemoveAt(found);
                    if (reverse) next.Reverse();
                    for (int k = 1; k < next.Count; k++) ring.Add(next[k]);
                }
                rings.Add(ring);
            }
            return true;
        }
    }
}
```

`Assets/WorldGen/Editor/Geometry/Triangulator.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Ear-clipping triangulation with hole bridging (Eberly). Output triangles are CCW in 2D.</summary>
    public static class Triangulator
    {
        const float Epsilon = 1e-7f;

        public static bool Triangulate(Polygon polygon, List<Vector2> vertices, List<int> triangles)
        {
            vertices.Clear();
            triangles.Clear();
            if (polygon == null || polygon.Outer.Count < 3) return false;

            var merged = new List<Vector2>(polygon.Outer);
            Polygon2D.EnsureWinding(merged, true);
            var holes = new List<List<Vector2>>();
            foreach (var hole in polygon.Holes)
            {
                if (hole.Count < 3) continue;
                var h = new List<Vector2>(hole);
                Polygon2D.EnsureWinding(h, false);
                holes.Add(h);
            }
            holes.Sort((a, b) => MaxX(b).CompareTo(MaxX(a)));
            foreach (var hole in holes) merged = BridgeHole(merged, hole);

            vertices.AddRange(merged);
            return EarClip(vertices, triangles);
        }

        static float MaxX(List<Vector2> ring)
        {
            float max = float.MinValue;
            foreach (var p in ring) if (p.x > max) max = p.x;
            return max;
        }

        static List<Vector2> BridgeHole(List<Vector2> outer, List<Vector2> hole)
        {
            int mi = 0;
            for (int i = 1; i < hole.Count; i++) if (hole[i].x > hole[mi].x) mi = i;
            Vector2 m = hole[mi];

            int edge = -1;
            float bestX = float.MaxValue;
            for (int i = 0; i < outer.Count; i++)
            {
                Vector2 a = outer[i], b = outer[(i + 1) % outer.Count];
                if ((a.y > m.y) == (b.y > m.y)) continue;
                float x = a.x + (m.y - a.y) * (b.x - a.x) / (b.y - a.y);
                if (x >= m.x && x < bestX) { bestX = x; edge = i; }
            }
            if (edge < 0) return outer;

            var hit = new Vector2(bestX, m.y);
            int next = (edge + 1) % outer.Count;
            int candidate = outer[edge].x > outer[next].x ? edge : next;
            Vector2 p = outer[candidate];

            int chosen = candidate;
            if (Mathf.Abs(Polygon2D.Cross(hit - m, p - m)) > Epsilon)
            {
                float bestAngle = float.MaxValue, bestDistance = float.MaxValue;
                for (int i = 0; i < outer.Count; i++)
                {
                    if (i == candidate || !IsReflex(outer, i)) continue;
                    Vector2 v = outer[i];
                    if (!PointInTriangle(v, m, hit, p)) continue;
                    float angle = Mathf.Abs(Mathf.Atan2(v.y - m.y, v.x - m.x));
                    float distance = (v - m).sqrMagnitude;
                    if (angle < bestAngle - 1e-6f || (Mathf.Abs(angle - bestAngle) <= 1e-6f && distance < bestDistance))
                    {
                        bestAngle = angle;
                        bestDistance = distance;
                        chosen = i;
                    }
                }
            }

            var result = new List<Vector2>(outer.Count + hole.Count + 2);
            for (int i = 0; i <= chosen; i++) result.Add(outer[i]);
            for (int k = 0; k <= hole.Count; k++) result.Add(hole[(mi + k) % hole.Count]);
            for (int i = chosen; i < outer.Count; i++) result.Add(outer[i]);
            return result;
        }

        static bool IsReflex(List<Vector2> ring, int i)
        {
            Vector2 prev = ring[(i - 1 + ring.Count) % ring.Count], cur = ring[i], next = ring[(i + 1) % ring.Count];
            return Polygon2D.Cross(cur - prev, next - cur) < 0f;
        }

        static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Polygon2D.Cross(b - a, p - a), d2 = Polygon2D.Cross(c - b, p - b), d3 = Polygon2D.Cross(a - c, p - c);
            bool hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNegative && hasPositive);
        }

        static bool EarClip(List<Vector2> pts, List<int> tris)
        {
            var idx = new List<int>(pts.Count);
            for (int i = 0; i < pts.Count; i++) idx.Add(i);

            while (idx.Count > 3)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int ia = idx[(i + idx.Count - 1) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                    Vector2 a = pts[ia], b = pts[ib], c = pts[ic];
                    if (Polygon2D.Cross(b - a, c - b) <= Epsilon) continue;
                    if (ContainsOther(pts, idx, ia, ib, ic)) continue;
                    tris.Add(ia);
                    tris.Add(ib);
                    tris.Add(ic);
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped && !RemoveDegenerate(pts, idx)) return false;
            }

            if (idx.Count == 3 && Polygon2D.Cross(pts[idx[1]] - pts[idx[0]], pts[idx[2]] - pts[idx[1]]) > Epsilon)
            {
                tris.Add(idx[0]);
                tris.Add(idx[1]);
                tris.Add(idx[2]);
            }
            return tris.Count > 0;
        }

        static bool ContainsOther(List<Vector2> pts, List<int> idx, int ia, int ib, int ic)
        {
            Vector2 a = pts[ia], b = pts[ib], c = pts[ic];
            foreach (int j in idx)
            {
                if (j == ia || j == ib || j == ic) continue;
                Vector2 p = pts[j];
                if (p == a || p == b || p == c) continue;
                if (PointInTriangle(p, a, b, c)) return true;
            }
            return false;
        }

        static bool RemoveDegenerate(List<Vector2> pts, List<int> idx)
        {
            for (int i = 0; i < idx.Count; i++)
            {
                Vector2 a = pts[idx[(i + idx.Count - 1) % idx.Count]], b = pts[idx[i]], c = pts[idx[(i + 1) % idx.Count]];
                if (a == b || b == c || Mathf.Abs(Polygon2D.Cross(b - a, c - b)) <= Epsilon)
                {
                    idx.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }
    }
}
```

- [ ] **Step 4: Jalankan, pastikan lulus**

Procedure R, lalu Procedure T dengan `Group = "RingAssemblerTests|TriangulatorTests"`. Expected: `passed=15 failed=0` (6 + 9).

- [ ] **Step 5: Commit**

Procedure C: `git add Assets/WorldGen`, subject `feat(worldgen): assemble multipolygon rings and triangulate with holes`.

---

### Task 7: MeshData + builder jalan, area, ground

**Files:**
- Create: `Assets/WorldGen/Editor/Builders/MeshData.cs`
- Create: `Assets/WorldGen/Editor/Builders/RoadMeshBuilder.cs`
- Create: `Assets/WorldGen/Editor/Builders/AreaMeshBuilder.cs`
- Create: `Assets/WorldGen/Editor/Builders/GroundBuilder.cs`
- Test: `Assets/WorldGen/Tests/MeshBuilderTests.cs`

**Interfaces:**
- Consumes: `Polygon`, `Polygon2D`, `Xz` (Task 5), `Triangulator` (Task 6).
- Produces:
  - `sealed class MeshData { MeshData(int submeshCount = 1); List<Vector3> Vertices, Normals; List<Vector2> Uvs; int SubmeshCount; bool IsEmpty; int TriangleCount; IReadOnlyList<int> Indices(int submesh); void AddTriangle(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector2 uvA, Vector2 uvB, Vector2 uvC, Vector3 desiredNormal); void AddQuad(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uvA, Vector2 uvB, Vector2 uvC, Vector2 uvD, Vector3 desiredNormal); void WriteTo(Mesh mesh, out int[] usedSubmeshes); Mesh ToMesh(string name, out int[] usedSubmeshes); ulong ContentHash(); bool HasInvalidValues() }` — segitiga degenerate diabaikan; winding dibalik otomatis supaya sisi depan mengikuti `desiredNormal`.
  - `static class RoadMeshBuilder { const float MiterLimit = 2f; void AddRibbon(MeshData mesh, int submesh, IReadOnlyList<Vector2> points, float width, float y, bool extendStart, bool extendEnd) }`
  - `static class AreaMeshBuilder { bool AddFlatPolygon(MeshData mesh, int submesh, Polygon polygon, float y, bool faceDown = false) }`
  - `static class GroundBuilder { MeshData Build(Rect bounds); void ColliderBox(Rect bounds, out Vector3 center, out Vector3 size) }`

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/WorldGen/Tests/MeshBuilderTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;
using UnityEngine.Rendering;

namespace TycoWorld.WorldGen.Tests
{
    public class MeshBuilderTests
    {
        static Vector3 FaceNormal(MeshData m, int sub, int tri)
        {
            var idx = m.Indices(sub);
            Vector3 a = m.Vertices[idx[tri * 3]], b = m.Vertices[idx[tri * 3 + 1]], c = m.Vertices[idx[tri * 3 + 2]];
            return Vector3.Cross(b - a, c - a).normalized;
        }

        static float DistanceToPolyline(Vector2 p, IList<Vector2> line)
        {
            float best = float.MaxValue;
            for (int i = 0; i + 1 < line.Count; i++) best = Mathf.Min(best, Polygon2D.DistanceToSegment(p, line[i], line[i + 1]));
            return best;
        }

        static MeshData Ribbon(float width, bool extendStart, params Vector2[] pts)
        {
            var m = new MeshData(1);
            RoadMeshBuilder.AddRibbon(m, 0, pts, width, 0.07f, extendStart, false);
            return m;
        }

        [Test]
        public void AddQuad_FlipsToDesiredNormal()
        {
            var m = new MeshData(1);
            m.AddQuad(0, new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 1), new Vector3(0, 0, 1),
                Vector2.zero, Vector2.right, Vector2.one, Vector2.up, Vector3.up);
            Assert.Greater(Vector3.Dot(FaceNormal(m, 0, 0), Vector3.up), 0.99f);
            Assert.Greater(Vector3.Dot(FaceNormal(m, 0, 1), Vector3.up), 0.99f);
            foreach (var n in m.Normals) Assert.Greater(n.y, 0.99f);
        }

        [Test]
        public void AddTriangle_DegenerateIsSkipped()
        {
            var m = new MeshData(1);
            m.AddTriangle(0, Vector3.zero, Vector3.right, Vector3.right * 2f, Vector2.zero, Vector2.zero, Vector2.zero, Vector3.up);
            Assert.IsTrue(m.IsEmpty);
        }

        [Test]
        public void ToMesh_DropsEmptySubmeshes()
        {
            var m = new MeshData(4);
            m.AddTriangle(1, Vector3.zero, Vector3.forward, Vector3.right, Vector2.zero, Vector2.zero, Vector2.zero, Vector3.up);
            m.AddTriangle(3, Vector3.zero, Vector3.forward, Vector3.right, Vector2.zero, Vector2.zero, Vector2.zero, Vector3.up);
            var mesh = m.ToMesh("t", out var used);
            try
            {
                CollectionAssert.AreEqual(new[] { 1, 3 }, used);
                Assert.AreEqual(2, mesh.subMeshCount);
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void ToMesh_UsesUInt32AboveLimit()
        {
            var m = new MeshData(1);
            for (int i = 0; i < 22000; i++)
                m.AddTriangle(0, new Vector3(i, 0, 0), new Vector3(i, 0, 1), new Vector3(i + 1, 0, 0), Vector2.zero, Vector2.zero, Vector2.zero, Vector3.up);
            var mesh = m.ToMesh("big", out _);
            try { Assert.AreEqual(IndexFormat.UInt32, mesh.indexFormat); }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void ContentHash_StableAndSensitive()
        {
            var a = Ribbon(6f, false, new Vector2(0, 0), new Vector2(20, 0));
            var b = Ribbon(6f, false, new Vector2(0, 0), new Vector2(20, 0));
            var c = Ribbon(6f, false, new Vector2(0, 0), new Vector2(21, 0));
            Assert.AreEqual(a.ContentHash(), b.ContentHash());
            Assert.AreNotEqual(a.ContentHash(), c.ContentHash());
        }

        [Test]
        public void Ribbon_Straight_HasConstantHalfWidth()
        {
            var m = Ribbon(6f, false, new Vector2(0, 0), new Vector2(20, 0));
            Assert.AreEqual(4, m.Vertices.Count);
            foreach (var v in m.Vertices)
            {
                Assert.AreEqual(3f, Mathf.Abs(v.z), 1e-4f);
                Assert.AreEqual(0.07f, v.y, 1e-6f);
            }
        }

        [Test]
        public void Ribbon_RightAngle_UsesMiter()
        {
            var m = Ribbon(2f, false, new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10));
            Assert.AreEqual(8, m.Vertices.Count);
            bool foundOuterCorner = false;
            foreach (var v in m.Vertices)
                if (Vector2.Distance(new Vector2(v.x, v.z), new Vector2(11, -1)) < 1e-3f) foundOuterCorner = true;
            Assert.IsTrue(foundOuterCorner);
        }

        [Test]
        public void Ribbon_SharpTurn_RespectsMiterLimit()
        {
            var line = new List<Vector2> { new Vector2(0, 0), new Vector2(10, 0), new Vector2(0, 1) };
            var m = Ribbon(4f, false, line.ToArray());
            foreach (var v in m.Vertices)
                Assert.LessOrEqual(DistanceToPolyline(new Vector2(v.x, v.z), line), RoadMeshBuilder.MiterLimit * 2f + 1e-3f);
        }

        [Test]
        public void Ribbon_UTurn_ProducesNoNaN()
        {
            var m = Ribbon(4f, false, new Vector2(0, 0), new Vector2(10, 0), new Vector2(0, 0));
            Assert.IsFalse(m.IsEmpty);
            Assert.IsFalse(m.HasInvalidValues());
        }

        [Test]
        public void Ribbon_DuplicatePoints_Ignored()
        {
            var m = Ribbon(4f, false, new Vector2(0, 0), new Vector2(0, 0), new Vector2(5, 0));
            Assert.AreEqual(4, m.Vertices.Count);
        }

        [Test]
        public void Ribbon_ExtendStart_AddsHalfWidth()
        {
            var m = Ribbon(4f, true, new Vector2(0, 0), new Vector2(10, 0));
            float minX = float.MaxValue;
            foreach (var v in m.Vertices) minX = Mathf.Min(minX, v.x);
            Assert.AreEqual(-2f, minX, 1e-4f);
        }

        [Test]
        public void Ribbon_UvAcrossWidth()
        {
            var m = Ribbon(6f, false, new Vector2(0, 0), new Vector2(20, 0));
            foreach (var uv in m.Uvs) Assert.IsTrue(Mathf.Abs(uv.x) < 1e-4f || Mathf.Abs(uv.x - 6f) < 1e-4f);
        }

        [Test]
        public void Ribbon_FacesUp()
        {
            var m = Ribbon(3f, false, new Vector2(0, 0), new Vector2(10, 3), new Vector2(15, -4), new Vector2(30, 0));
            for (int t = 0; t < m.TriangleCount; t++) Assert.Greater(FaceNormal(m, 0, t).y, 0.99f);
        }

        [Test]
        public void FlatPolygon_UpFacingAndAreaPreserved()
        {
            var m = new MeshData(1);
            var square = new Polygon(new List<Vector2> { new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10), new Vector2(0, 10) });
            Assert.IsTrue(AreaMeshBuilder.AddFlatPolygon(m, 0, square, 0.025f));
            Assert.AreEqual(2, m.TriangleCount);
            float area = 0f;
            for (int t = 0; t < m.TriangleCount; t++)
            {
                Assert.Greater(FaceNormal(m, 0, t).y, 0.99f);
                var idx = m.Indices(0);
                Vector3 a = m.Vertices[idx[t * 3]], b = m.Vertices[idx[t * 3 + 1]], c = m.Vertices[idx[t * 3 + 2]];
                area += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
            }
            Assert.AreEqual(100f, area, 1e-3f);
        }

        [Test]
        public void FlatPolygon_FaceDown()
        {
            var m = new MeshData(1);
            var square = new Polygon(new List<Vector2> { new Vector2(0, 0), new Vector2(4, 0), new Vector2(4, 4), new Vector2(0, 4) });
            Assert.IsTrue(AreaMeshBuilder.AddFlatPolygon(m, 0, square, 3f, faceDown: true));
            for (int t = 0; t < m.TriangleCount; t++) Assert.Less(FaceNormal(m, 0, t).y, -0.99f);
        }

        [Test]
        public void Ground_CoversBoundsAndColliderTopAtZero()
        {
            var bounds = Rect.MinMaxRect(-10, -20, 30, 40);
            var m = GroundBuilder.Build(bounds);
            Assert.AreEqual(4, m.Vertices.Count);
            foreach (var v in m.Vertices)
            {
                Assert.IsTrue(Mathf.Approximately(v.x, -10) || Mathf.Approximately(v.x, 30));
                Assert.IsTrue(Mathf.Approximately(v.z, -20) || Mathf.Approximately(v.z, 40));
                Assert.AreEqual(0f, v.y);
            }
            GroundBuilder.ColliderBox(bounds, out var center, out var size);
            Assert.AreEqual(0f, center.y + size.y / 2f, 1e-6f);
            Assert.AreEqual(40f, size.x, 1e-4f);
            Assert.AreEqual(60f, size.z, 1e-4f);
        }
    }
}
```

- [ ] **Step 2: Jalankan, pastikan gagal**

Procedure R, lalu Procedure T dengan `Group = "MeshBuilderTests"`. Expected: COMPILE FAILED.

- [ ] **Step 3: Implementasi**

`Assets/WorldGen/Editor/Builders/MeshData.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Flat-shaded mesh builder. Winding follows a desired normal, so callers never order indices by hand.</summary>
    public sealed class MeshData
    {
        public readonly List<Vector3> Vertices = new List<Vector3>();
        public readonly List<Vector3> Normals = new List<Vector3>();
        public readonly List<Vector2> Uvs = new List<Vector2>();
        readonly List<int>[] submeshes;

        public MeshData(int submeshCount = 1)
        {
            submeshes = new List<int>[submeshCount];
            for (int i = 0; i < submeshCount; i++) submeshes[i] = new List<int>();
        }

        public int SubmeshCount => submeshes.Length;
        public bool IsEmpty => Vertices.Count == 0;
        public IReadOnlyList<int> Indices(int submesh) => submeshes[submesh];

        public int TriangleCount
        {
            get
            {
                int count = 0;
                foreach (var s in submeshes) count += s.Count / 3;
                return count;
            }
        }

        public void AddTriangle(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector2 uvA, Vector2 uvB, Vector2 uvC, Vector3 desiredNormal)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-12f) return;
            if (Vector3.Dot(n, desiredNormal) < 0f)
            {
                (b, c) = (c, b);
                (uvB, uvC) = (uvC, uvB);
                n = -n;
            }
            n.Normalize();
            int start = Vertices.Count;
            Vertices.Add(a); Vertices.Add(b); Vertices.Add(c);
            Normals.Add(n); Normals.Add(n); Normals.Add(n);
            Uvs.Add(uvA); Uvs.Add(uvB); Uvs.Add(uvC);
            var list = submeshes[submesh];
            list.Add(start); list.Add(start + 1); list.Add(start + 2);
        }

        public void AddQuad(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d,
            Vector2 uvA, Vector2 uvB, Vector2 uvC, Vector2 uvD, Vector3 desiredNormal)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-12f) n = Vector3.Cross(c - a, d - a);
            if (n.sqrMagnitude < 1e-12f) return;
            if (Vector3.Dot(n, desiredNormal) < 0f)
            {
                (b, d) = (d, b);
                (uvB, uvD) = (uvD, uvB);
                n = -n;
            }
            n.Normalize();
            int start = Vertices.Count;
            Vertices.Add(a); Vertices.Add(b); Vertices.Add(c); Vertices.Add(d);
            for (int i = 0; i < 4; i++) Normals.Add(n);
            Uvs.Add(uvA); Uvs.Add(uvB); Uvs.Add(uvC); Uvs.Add(uvD);
            var list = submeshes[submesh];
            list.Add(start); list.Add(start + 1); list.Add(start + 2);
            list.Add(start); list.Add(start + 2); list.Add(start + 3);
        }

        /// <summary>Writes into an existing mesh. Empty submeshes are dropped; usedSubmeshes maps mesh submesh k to MeshData submesh.</summary>
        public void WriteTo(Mesh mesh, out int[] usedSubmeshes)
        {
            var used = new List<int>();
            for (int s = 0; s < submeshes.Length; s++)
                if (submeshes[s].Count > 0) used.Add(s);
            usedSubmeshes = used.ToArray();
            mesh.Clear();
            mesh.indexFormat = Vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(Vertices);
            mesh.SetNormals(Normals);
            mesh.SetUVs(0, Uvs);
            mesh.subMeshCount = used.Count;
            for (int k = 0; k < used.Count; k++) mesh.SetTriangles(submeshes[used[k]], k, false);
            mesh.RecalculateBounds();
        }

        public Mesh ToMesh(string name, out int[] usedSubmeshes)
        {
            var mesh = new Mesh { name = name };
            WriteTo(mesh, out usedSubmeshes);
            return mesh;
        }

        public ulong ContentHash()
        {
            ulong hash = 14695981039346656037UL;
            void Mix(int value)
            {
                unchecked
                {
                    hash ^= (uint)value;
                    hash *= 1099511628211UL;
                }
            }
            foreach (var v in Vertices)
            {
                Mix(Mathf.RoundToInt(v.x * 1000f));
                Mix(Mathf.RoundToInt(v.y * 1000f));
                Mix(Mathf.RoundToInt(v.z * 1000f));
            }
            foreach (var uv in Uvs)
            {
                Mix(Mathf.RoundToInt(uv.x * 1000f));
                Mix(Mathf.RoundToInt(uv.y * 1000f));
            }
            for (int s = 0; s < submeshes.Length; s++)
            {
                Mix(-1 - s);
                foreach (int i in submeshes[s]) Mix(i);
            }
            return hash;
        }

        public bool HasInvalidValues()
        {
            foreach (var v in Vertices) if (!Finite(v.x) || !Finite(v.y) || !Finite(v.z)) return true;
            foreach (var n in Normals) if (!Finite(n.x) || !Finite(n.y) || !Finite(n.z)) return true;
            foreach (var uv in Uvs) if (!Finite(uv.x) || !Finite(uv.y)) return true;
            return false;
        }

        static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
    }
}
```

`Assets/WorldGen/Editor/Builders/RoadMeshBuilder.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Flat ribbon along a centerline: mitered joints, bevel when the miter exceeds MiterLimit × half width.</summary>
    public static class RoadMeshBuilder
    {
        public const float MiterLimit = 2f;

        public static void AddRibbon(MeshData mesh, int submesh, IReadOnlyList<Vector2> points, float width, float y,
            bool extendStart, bool extendEnd)
        {
            if (points == null || width <= 0f) return;
            var pts = new List<Vector2>(points.Count);
            foreach (var p in points)
                if (pts.Count == 0 || (p - pts[pts.Count - 1]).sqrMagnitude > 1e-4f) pts.Add(p);
            if (pts.Count < 2) return;

            float hw = width * 0.5f;
            int last = pts.Count - 1;
            if (extendStart) pts[0] -= (pts[1] - pts[0]).normalized * hw;
            if (extendEnd) pts[last] += (pts[last] - pts[last - 1]).normalized * hw;

            int segments = pts.Count - 1;
            var normal = new Vector2[segments];
            for (int s = 0; s < segments; s++)
            {
                var d = (pts[s + 1] - pts[s]).normalized;
                normal[s] = new Vector2(-d.y, d.x);
            }
            var dist = new float[pts.Count];
            for (int i = 1; i < pts.Count; i++) dist[i] = dist[i - 1] + Vector2.Distance(pts[i - 1], pts[i]);

            var startL = new Vector2[segments];
            var startR = new Vector2[segments];
            var endL = new Vector2[segments];
            var endR = new Vector2[segments];
            for (int s = 0; s < segments; s++)
            {
                startL[s] = pts[s] + normal[s] * hw;
                startR[s] = pts[s] - normal[s] * hw;
                endL[s] = pts[s + 1] + normal[s] * hw;
                endR[s] = pts[s + 1] - normal[s] * hw;
            }

            for (int i = 1; i < last; i++)
            {
                Vector2 n0 = normal[i - 1], n1 = normal[i];
                Vector2 sum = n0 + n1;
                if (sum.sqrMagnitude > 1e-6f)
                {
                    Vector2 miter = sum.normalized;
                    float cosHalf = Vector2.Dot(miter, n1);
                    if (cosHalf > 1e-4f && 1f / cosHalf <= MiterLimit)
                    {
                        Vector2 offset = miter * (hw / cosHalf);
                        endL[i - 1] = startL[i] = pts[i] + offset;
                        endR[i - 1] = startR[i] = pts[i] - offset;
                        continue;
                    }
                }
                Vector2 d0 = pts[i] - pts[i - 1], d1 = pts[i + 1] - pts[i];
                bool leftTurn = d0.x * d1.y - d0.y * d1.x > 0f;
                Vector2 outer0 = leftTurn ? endR[i - 1] : endL[i - 1];
                Vector2 outer1 = leftTurn ? startR[i] : startL[i];
                var uv = new Vector2(hw, dist[i]);
                mesh.AddTriangle(submesh, Xz.At(pts[i], y), Xz.At(outer0, y), Xz.At(outer1, y), uv, uv, uv, Vector3.up);
            }

            for (int s = 0; s < segments; s++)
            {
                mesh.AddQuad(submesh,
                    Xz.At(startR[s], y), Xz.At(endR[s], y), Xz.At(endL[s], y), Xz.At(startL[s], y),
                    new Vector2(width, dist[s]), new Vector2(width, dist[s + 1]), new Vector2(0f, dist[s + 1]), new Vector2(0f, dist[s]),
                    Vector3.up);
            }
        }
    }
}
```

`Assets/WorldGen/Editor/Builders/AreaMeshBuilder.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Triangulated flat polygon at a fixed height. UV = ground position in meters.</summary>
    public static class AreaMeshBuilder
    {
        public static bool AddFlatPolygon(MeshData mesh, int submesh, Polygon polygon, float y, bool faceDown = false)
        {
            var vertices = new List<Vector2>();
            var triangles = new List<int>();
            if (!Triangulator.Triangulate(polygon, vertices, triangles)) return false;
            var normal = faceDown ? Vector3.down : Vector3.up;
            for (int t = 0; t < triangles.Count; t += 3)
            {
                Vector2 a = vertices[triangles[t]], b = vertices[triangles[t + 1]], c = vertices[triangles[t + 2]];
                mesh.AddTriangle(submesh, Xz.At(a, y), Xz.At(b, y), Xz.At(c, y), a, b, c, normal);
            }
            return true;
        }
    }
}
```

`Assets/WorldGen/Editor/Builders/GroundBuilder.cs`:

```csharp
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Single ground quad covering the area; its 1 m thick box collider has its top face at y = 0.</summary>
    public static class GroundBuilder
    {
        public static MeshData Build(Rect bounds)
        {
            var mesh = new MeshData(1);
            Vector2 a = new Vector2(bounds.xMin, bounds.yMin), b = new Vector2(bounds.xMax, bounds.yMin);
            Vector2 c = new Vector2(bounds.xMax, bounds.yMax), d = new Vector2(bounds.xMin, bounds.yMax);
            mesh.AddQuad(0, Xz.At(a, 0f), Xz.At(b, 0f), Xz.At(c, 0f), Xz.At(d, 0f), a, b, c, d, Vector3.up);
            return mesh;
        }

        public static void ColliderBox(Rect bounds, out Vector3 center, out Vector3 size)
        {
            center = new Vector3(bounds.center.x, -0.5f, bounds.center.y);
            size = new Vector3(bounds.width, 1f, bounds.height);
        }
    }
}
```

- [ ] **Step 4: Jalankan, pastikan lulus**

Procedure R, lalu Procedure T dengan `Group = "MeshBuilderTests"`. Expected: `passed=16 failed=0`.

- [ ] **Step 5: Commit**

Procedure C: `git add Assets/WorldGen`, subject `feat(worldgen): add mesh data and road, area, ground builders`.

---

### Task 8: Mesh bangunan (dinding, atap dak, atap pelana, atap saja)

**Files:**
- Create: `Assets/WorldGen/Editor/Builders/BuildingSpec.cs`
- Create: `Assets/WorldGen/Editor/Builders/BuildingMeshBuilder.cs`
- Test: `Assets/WorldGen/Tests/BuildingMeshBuilderTests.cs`

**Interfaces:**
- Consumes: `Polygon`, `Polygon2D`, `Obb`, `ObbCalculator`, `Xz` (Task 5); `MeshData`, `AreaMeshBuilder` (Task 7).
- Produces:
  - `enum RoofOverride { Auto, Flat, Gable }`, `enum RoofKind { Flat, Gable, RoofOnly }`
  - `sealed class BuildingSpec { long Id; Polygon Footprint; float WallHeight; int Levels = 1; float LevelHeight = 3.2f; bool AllowGable; bool RoofOnly; RoofOverride RoofOverride }`
  - `struct BuildingMeshParams { float FacadeTileWidth, ParapetHeight, GableMaxArea, GableMinRectangularity, GablePitchRatio, GableOverhang; static BuildingMeshParams Default }` (default 3.0, 0.6, 200, 0.85, 0.25, 0.3)
  - `struct BuildingBuildInfo { RoofKind Roof; Obb Obb; bool RoofFailed }`
  - `static class BuildingMeshBuilder { const int SubGround = 0, SubUpper = 1, SubTrim = 2, SubRoof = 3, SubmeshCount = 4; const float RoofOnlyHeight = 3f, RoofOnlyThickness = 0.15f, PillarSize = 0.2f; RoofKind ChooseRoof(BuildingSpec spec, BuildingMeshParams p, Obb obb); MeshData Build(BuildingSpec spec, BuildingMeshParams p, out BuildingBuildInfo info) }`

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/WorldGen/Tests/BuildingMeshBuilderTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class BuildingMeshBuilderTests
    {
        static readonly BuildingMeshParams P = BuildingMeshParams.Default;

        static Polygon Rect(float w, float d) => new Polygon(new List<Vector2>
            { new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, d), new Vector2(0, d) });

        static Polygon LShape() => new Polygon(new List<Vector2>
            { new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 4), new Vector2(4, 4), new Vector2(4, 10), new Vector2(0, 10) });

        static BuildingSpec Spec(Polygon footprint, float wall, int levels, bool allowGable = false) => new BuildingSpec
        {
            Id = 1, Footprint = footprint, WallHeight = wall, Levels = levels, LevelHeight = 3.2f, AllowGable = allowGable
        };

        static float MaxV(MeshData m, int sub)
        {
            float max = float.MinValue;
            foreach (int i in m.Indices(sub)) max = Mathf.Max(max, m.Uvs[i].y);
            return max;
        }

        static float MaxY(MeshData m)
        {
            float max = float.MinValue;
            foreach (var v in m.Vertices) max = Mathf.Max(max, v.y);
            return max;
        }

        [Test]
        public void FlatBox_HeightsAndUvRanges()
        {
            var m = BuildingMeshBuilder.Build(Spec(Rect(10, 10), 9.6f, 3), P, out var info);
            Assert.AreEqual(RoofKind.Flat, info.Roof);
            Assert.IsFalse(info.RoofFailed);
            Assert.AreEqual(10.2f, MaxY(m), 1e-4f);
            Assert.AreEqual(1f, MaxV(m, BuildingMeshBuilder.SubGround), 1e-4f);
            Assert.AreEqual(2f, MaxV(m, BuildingMeshBuilder.SubUpper), 1e-4f);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Walls_FaceOutward(bool clockwiseInput)
        {
            var footprint = Rect(10, 10);
            if (clockwiseInput) footprint.Outer.Reverse();
            var m = BuildingMeshBuilder.Build(Spec(footprint, 9.6f, 3), P, out _);
            var center = new Vector3(5, 0, 5);
            var idx = m.Indices(BuildingMeshBuilder.SubGround);
            Assert.Greater(idx.Count, 0);
            for (int t = 0; t < idx.Count; t += 3)
            {
                Vector3 a = m.Vertices[idx[t]], b = m.Vertices[idx[t + 1]], c = m.Vertices[idx[t + 2]];
                var normal = Vector3.Cross(b - a, c - a);
                var toFace = (a + b + c) / 3f - center;
                toFace.y = 0f;
                Assert.Greater(Vector3.Dot(normal, toFace), 0f);
            }
        }

        [Test]
        public void FlatRoof_FacesUp()
        {
            var m = BuildingMeshBuilder.Build(Spec(Rect(10, 10), 6.4f, 2), P, out _);
            foreach (int i in m.Indices(BuildingMeshBuilder.SubRoof)) Assert.Greater(m.Normals[i].y, 0.99f);
        }

        [Test]
        public void FacadeBays_AreWholeNumbers()
        {
            var m = BuildingMeshBuilder.Build(Spec(Rect(10, 10), 3.2f, 1), P, out _);
            float maxU = float.MinValue;
            foreach (int i in m.Indices(BuildingMeshBuilder.SubGround)) maxU = Mathf.Max(maxU, m.Uvs[i].x);
            Assert.AreEqual(3f, maxU, 1e-4f);
        }

        [Test]
        public void SmallRectangle_GetsGableAlongLongSide()
        {
            var m = BuildingMeshBuilder.Build(Spec(Rect(8, 12), 3.2f, 1, allowGable: true), P, out var info);
            Assert.AreEqual(RoofKind.Gable, info.Roof);
            float top = MaxY(m);
            Assert.AreEqual(5.2f, top, 1e-3f);
            float minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var v in m.Vertices)
            {
                if (Mathf.Abs(v.y - top) > 1e-3f) continue;
                Assert.AreEqual(4f, v.x, 1e-3f);
                minZ = Mathf.Min(minZ, v.z);
                maxZ = Mathf.Max(maxZ, v.z);
            }
            Assert.GreaterOrEqual(maxZ - minZ, 12f);
        }

        [Test]
        public void GableOverride_WithHole_FallsBackToFlat()
        {
            var footprint = Rect(10, 10);
            footprint.Holes.Add(new List<Vector2> { new Vector2(3, 3), new Vector2(3, 7), new Vector2(7, 7), new Vector2(7, 3) });
            var spec = Spec(footprint, 3.2f, 1, allowGable: true);
            spec.RoofOverride = RoofOverride.Gable;
            BuildingMeshBuilder.Build(spec, P, out var info);
            Assert.AreEqual(RoofKind.Flat, info.Roof);
        }

        [Test]
        public void LShape_GetsFlatRoofWithTwoSidedParapet()
        {
            var m = BuildingMeshBuilder.Build(Spec(LShape(), 3.2f, 1, allowGable: true), P, out var info);
            Assert.AreEqual(RoofKind.Flat, info.Roof);
            Assert.AreEqual(24, m.Indices(BuildingMeshBuilder.SubTrim).Count / 3);
        }

        [TestCase(4, 10f, 10f, true)]
        [TestCase(1, 25f, 10f, true)]
        [TestCase(1, 8f, 12f, false)]
        public void ChooseRoof_FallsBackToFlat(int levels, float w, float d, bool allowGable)
        {
            var spec = Spec(Rect(w, d), levels * 3.2f, levels, allowGable);
            var obb = ObbCalculator.Compute(spec.Footprint.Outer);
            Assert.AreEqual(RoofKind.Flat, BuildingMeshBuilder.ChooseRoof(spec, P, obb));
        }

        [Test]
        public void RoofOnly_HasNoWalls()
        {
            var spec = Spec(Rect(6, 4), 3f, 1);
            spec.RoofOnly = true;
            var m = BuildingMeshBuilder.Build(spec, P, out var info);
            Assert.AreEqual(RoofKind.RoofOnly, info.Roof);
            Assert.AreEqual(0, m.Indices(BuildingMeshBuilder.SubGround).Count);
            Assert.AreEqual(0, m.Indices(BuildingMeshBuilder.SubUpper).Count);
            Assert.AreEqual(BuildingMeshBuilder.RoofOnlyHeight + BuildingMeshBuilder.RoofOnlyThickness, MaxY(m), 1e-4f);
        }

        [Test]
        public void LowWall_ClampsGroundUv()
        {
            var m = BuildingMeshBuilder.Build(Spec(Rect(10, 10), 2.5f, 1), P, out _);
            Assert.AreEqual(2.5f / 3.2f, MaxV(m, BuildingMeshBuilder.SubGround), 1e-4f);
            Assert.AreEqual(0, m.Indices(BuildingMeshBuilder.SubUpper).Count);
        }

        [Test]
        public void Build_IsDeterministic()
        {
            var a = BuildingMeshBuilder.Build(Spec(LShape(), 6.4f, 2), P, out _);
            var b = BuildingMeshBuilder.Build(Spec(LShape(), 6.4f, 2), P, out _);
            Assert.AreEqual(a.ContentHash(), b.ContentHash());
        }
    }
}
```

- [ ] **Step 2: Jalankan, pastikan gagal**

Procedure R, lalu Procedure T dengan `Group = "BuildingMeshBuilderTests"`. Expected: COMPILE FAILED.

- [ ] **Step 3: Implementasi**

`Assets/WorldGen/Editor/Builders/BuildingSpec.cs`:

```csharp
namespace TycoWorld.WorldGen.Authoring
{
    public enum RoofOverride { Auto, Flat, Gable }

    public enum RoofKind { Flat, Gable, RoofOnly }

    /// <summary>Everything the mesh builder needs to know about one building.</summary>
    public sealed class BuildingSpec
    {
        public long Id;
        public Polygon Footprint;
        public float WallHeight;
        public int Levels = 1;
        public float LevelHeight = 3.2f;
        public bool AllowGable;
        public bool RoofOnly;
        public RoofOverride RoofOverride;
    }

    public struct BuildingMeshParams
    {
        public float FacadeTileWidth, ParapetHeight, GableMaxArea, GableMinRectangularity, GablePitchRatio, GableOverhang;

        public static BuildingMeshParams Default => new BuildingMeshParams
        {
            FacadeTileWidth = 3f, ParapetHeight = 0.6f, GableMaxArea = 200f,
            GableMinRectangularity = 0.85f, GablePitchRatio = 0.25f, GableOverhang = 0.3f
        };
    }

    public struct BuildingBuildInfo
    {
        public RoofKind Roof;
        public Obb Obb;
        public bool RoofFailed;
    }
}
```

`Assets/WorldGen/Editor/Builders/BuildingMeshBuilder.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Builds building meshes: per-floor facade walls, flat roof + parapet, gable roof, or roof-only shelters.</summary>
    public static class BuildingMeshBuilder
    {
        public const int SubGround = 0, SubUpper = 1, SubTrim = 2, SubRoof = 3, SubmeshCount = 4;
        public const float RoofOnlyHeight = 3f, RoofOnlyThickness = 0.15f, PillarSize = 0.2f;

        public static RoofKind ChooseRoof(BuildingSpec spec, BuildingMeshParams p, Obb obb)
        {
            if (spec.RoofOnly) return RoofKind.RoofOnly;
            bool hasHoles = spec.Footprint.Holes.Count > 0;
            if (spec.RoofOverride == RoofOverride.Flat) return RoofKind.Flat;
            if (spec.RoofOverride == RoofOverride.Gable) return hasHoles ? RoofKind.Flat : RoofKind.Gable;
            if (!spec.AllowGable || hasHoles || spec.Levels > 3) return RoofKind.Flat;
            float area = spec.Footprint.Area;
            if (area > p.GableMaxArea || obb.Area <= 1e-4f) return RoofKind.Flat;
            return area / obb.Area >= p.GableMinRectangularity ? RoofKind.Gable : RoofKind.Flat;
        }

        public static MeshData Build(BuildingSpec spec, BuildingMeshParams p, out BuildingBuildInfo info)
        {
            var footprint = Normalized(spec.Footprint);
            info = new BuildingBuildInfo { Obb = ObbCalculator.Compute(footprint.Outer) };
            info.Roof = ChooseRoof(spec, p, info.Obb);
            var mesh = new MeshData(SubmeshCount);
            switch (info.Roof)
            {
                case RoofKind.RoofOnly:
                    info.RoofFailed = !AddRoofOnly(mesh, footprint, info.Obb, spec, p);
                    break;
                case RoofKind.Gable:
                    AddWalls(mesh, footprint, spec, p);
                    AddGableRoof(mesh, info.Obb, spec, p);
                    break;
                default:
                    AddWalls(mesh, footprint, spec, p);
                    info.RoofFailed = !AddFlatRoof(mesh, footprint, spec, p);
                    break;
            }
            return mesh;
        }

        static Polygon Normalized(Polygon source)
        {
            var outer = new List<Vector2>(source.Outer);
            Polygon2D.EnsureWinding(outer, true);
            var holes = new List<List<Vector2>>();
            foreach (var h in source.Holes)
            {
                var hole = new List<Vector2>(h);
                Polygon2D.EnsureWinding(hole, false);
                holes.Add(hole);
            }
            return new Polygon(outer, holes);
        }

        static IEnumerable<List<Vector2>> Rings(Polygon polygon)
        {
            yield return polygon.Outer;
            foreach (var hole in polygon.Holes) yield return hole;
        }

        /// <summary>Outward normal for an edge of a CCW outer ring or a CW hole ring.</summary>
        static Vector3 Outward(Vector2 p0, Vector2 p1)
        {
            var d = (p1 - p0).normalized;
            return new Vector3(d.y, 0f, -d.x);
        }

        static void AddBand(MeshData m, int sub, Vector2 p0, Vector2 p1, float y0, float y1, float u1, float v1, Vector3 normal) =>
            m.AddQuad(sub, Xz.At(p0, y0), Xz.At(p1, y0), Xz.At(p1, y1), Xz.At(p0, y1),
                new Vector2(0f, 0f), new Vector2(u1, 0f), new Vector2(u1, v1), new Vector2(0f, v1), normal);

        static void AddWalls(MeshData m, Polygon footprint, BuildingSpec spec, BuildingMeshParams p)
        {
            float lh = spec.LevelHeight;
            float groundTop = Mathf.Min(lh, spec.WallHeight);
            foreach (var ring in Rings(footprint))
                for (int i = 0; i < ring.Count; i++)
                {
                    Vector2 p0 = ring[i], p1 = ring[(i + 1) % ring.Count];
                    float length = Vector2.Distance(p0, p1);
                    if (length < 1e-3f) continue;
                    float bays = Mathf.Max(1f, Mathf.Round(length / p.FacadeTileWidth));
                    var normal = Outward(p0, p1);
                    AddBand(m, SubGround, p0, p1, 0f, groundTop, bays, groundTop / lh, normal);
                    if (spec.WallHeight > lh + 1e-3f)
                        AddBand(m, SubUpper, p0, p1, lh, spec.WallHeight, bays, (spec.WallHeight - lh) / lh, normal);
                }
        }

        static bool AddFlatRoof(MeshData m, Polygon footprint, BuildingSpec spec, BuildingMeshParams p)
        {
            bool ok = AreaMeshBuilder.AddFlatPolygon(m, SubRoof, footprint, spec.WallHeight);
            float top = spec.WallHeight + p.ParapetHeight;
            float v1 = p.ParapetHeight / spec.LevelHeight;
            foreach (var ring in Rings(footprint))
                for (int i = 0; i < ring.Count; i++)
                {
                    Vector2 p0 = ring[i], p1 = ring[(i + 1) % ring.Count];
                    float length = Vector2.Distance(p0, p1);
                    if (length < 1e-3f) continue;
                    var normal = Outward(p0, p1);
                    float u1 = length / p.FacadeTileWidth;
                    AddBand(m, SubTrim, p0, p1, spec.WallHeight, top, u1, v1, normal);
                    AddBand(m, SubTrim, p0, p1, spec.WallHeight, top, u1, v1, -normal);
                }
            return ok;
        }

        static void AddGableRoof(MeshData m, Obb o, BuildingSpec spec, BuildingMeshParams p)
        {
            float h0 = spec.WallHeight;
            float rise = p.GablePitchRatio * 2f * o.HalfV;
            float h1 = h0 + rise;
            float slope = rise / Mathf.Max(o.HalfV, 1e-3f);
            float a = o.HalfU + p.GableOverhang, b = o.HalfV + p.GableOverhang;
            float eave = h0 - slope * p.GableOverhang;
            Vector2 c = o.Center, u = o.AxisU, v = o.AxisV;

            Vector3 r0 = Xz.At(c - u * a, h1), r1 = Xz.At(c + u * a, h1);
            Vector3 e0Minus = Xz.At(c - u * a - v * b, eave), e1Minus = Xz.At(c + u * a - v * b, eave);
            Vector3 e0Plus = Xz.At(c - u * a + v * b, eave), e1Plus = Xz.At(c + u * a + v * b, eave);
            float slopeLength = Mathf.Sqrt(b * b + (h1 - eave) * (h1 - eave));
            Vector3 v3 = Xz.Dir(v);
            Vector3 nMinus = (Vector3.up * b - v3 * (h1 - eave)).normalized;
            Vector3 nPlus = (Vector3.up * b + v3 * (h1 - eave)).normalized;
            Vector2 uv0 = new Vector2(-a, 0f), uv1 = new Vector2(a, 0f), uv2 = new Vector2(a, slopeLength), uv3 = new Vector2(-a, slopeLength);

            m.AddQuad(SubRoof, e0Minus, e1Minus, r1, r0, uv0, uv1, uv2, uv3, nMinus);
            m.AddQuad(SubRoof, e0Minus, e1Minus, r1, r0, uv0, uv1, uv2, uv3, -nMinus);
            m.AddQuad(SubRoof, e0Plus, e1Plus, r1, r0, uv0, uv1, uv2, uv3, nPlus);
            m.AddQuad(SubRoof, e0Plus, e1Plus, r1, r0, uv0, uv1, uv2, uv3, -nPlus);

            for (int s = -1; s <= 1; s += 2)
            {
                Vector2 end = c + u * (o.HalfU * s);
                Vector3 g0 = Xz.At(end - v * o.HalfV, h0), g1 = Xz.At(end + v * o.HalfV, h0), g2 = Xz.At(end, h1);
                m.AddTriangle(SubTrim, g0, g1, g2,
                    Vector2.zero, new Vector2(2f * o.HalfV / p.FacadeTileWidth, 0f), new Vector2(o.HalfV / p.FacadeTileWidth, rise / spec.LevelHeight),
                    Xz.Dir(u * s));
            }
        }

        static bool AddRoofOnly(MeshData m, Polygon footprint, Obb o, BuildingSpec spec, BuildingMeshParams p)
        {
            float bottom = RoofOnlyHeight, top = RoofOnlyHeight + RoofOnlyThickness;
            bool ok = AreaMeshBuilder.AddFlatPolygon(m, SubRoof, footprint, top);
            ok &= AreaMeshBuilder.AddFlatPolygon(m, SubRoof, footprint, bottom, faceDown: true);
            float v1 = RoofOnlyThickness / spec.LevelHeight;
            foreach (var ring in Rings(footprint))
                for (int i = 0; i < ring.Count; i++)
                {
                    Vector2 p0 = ring[i], p1 = ring[(i + 1) % ring.Count];
                    float length = Vector2.Distance(p0, p1);
                    if (length < 1e-3f) continue;
                    AddBand(m, SubTrim, p0, p1, bottom, top, length / p.FacadeTileWidth, v1, Outward(p0, p1));
                }
            for (int su = -1; su <= 1; su += 2)
                for (int sv = -1; sv <= 1; sv += 2)
                {
                    Vector2 corner = o.Center + o.AxisU * ((o.HalfU - PillarSize) * su) + o.AxisV * ((o.HalfV - PillarSize) * sv);
                    AddPillar(m, corner, o.AxisU, o.AxisV, bottom, spec.LevelHeight);
                }
            return ok;
        }

        static void AddPillar(MeshData m, Vector2 center, Vector2 u, Vector2 v, float height, float levelHeight)
        {
            float h = PillarSize * 0.5f;
            var ring = new[] { center + (-u - v) * h, center + (u - v) * h, center + (u + v) * h, center + (-u + v) * h };
            for (int i = 0; i < 4; i++)
            {
                Vector2 a = ring[i], b = ring[(i + 1) % 4];
                AddBand(m, SubTrim, a, b, 0f, height, 1f, height / levelHeight, Outward(a, b));
            }
        }
    }
}
```

- [ ] **Step 4: Jalankan, pastikan lulus**

Procedure R, lalu Procedure T dengan `Group = "BuildingMeshBuilderTests"`. Expected: `passed=14 failed=0`.

- [ ] **Step 5: Commit**

Procedure C: `git add Assets/WorldGen`, subject `feat(worldgen): build building meshes with flat and gable roofs`.

---

### Task 9: Settings + aturan + factory default

**Files:**
- Create: `Assets/WorldGen/Editor/AssetFolders.cs`
- Create: `Assets/WorldGen/Editor/Settings/WorldGenRules.cs`
- Create: `Assets/WorldGen/Editor/Settings/WorldGenSettings.cs`
- Create: `Assets/WorldGen/Editor/Settings/DefaultSettingsFactory.cs`
- Test: `Assets/WorldGen/Tests/DefaultSettingsTests.cs`

**Interfaces:**
- Consumes: `GeoBounds`, `GeoProjection` (Task 3); `RoofOverride`, `BuildingMeshParams` (Task 8).
- Produces:
  - `static class AssetFolders { void Ensure(string folder) }`
  - `[Serializable] RoadRule { string highway; float defaultWidth; int rank; float lampSpacing; Material material; RoadRule(); RoadRule(string highway, float defaultWidth, int rank, float lampSpacing) }`
  - `enum BuildingContext { Any, Campus, NonCampus }`, `enum BuildingMaterialSetId { Residential, Campus }`
  - `[Serializable] BuildingRule { string name; string[] buildingValues; BuildingContext context; int minLevels, maxLevels; float levelHeightOverride; bool allowGable, roofOnly; BuildingMaterialSetId materialSet; bool Matches(string buildingValue, bool isCampus) }`
  - `[Serializable] BuildingMaterialSet { Material[] groundFloor, upper, trim, flatRoof, gableRoof }`
  - `[Serializable] TagMatch { string key; string[] values; TagMatch(); TagMatch(string key, params string[] values); bool Matches(IReadOnlyDictionary<string,string> tags) }` (values kosong = nilai apa saja)
  - `enum AreaCategory { Campus, Hardground, Grass, Wood, Farmland, Parking, Cemetery, Pitch, Paving }`
  - `[Serializable] AreaRule { string name; TagMatch[] matches; AreaCategory category; float yOffset; float treeSpacing; Material material; bool Matches(IReadOnlyDictionary<string,string> tags) }` (OR antar `matches`)
  - `[Serializable] WaterwayWidth { string waterway; float width; WaterwayWidth(); WaterwayWidth(string waterway, float width) }`
  - `[Serializable] KeyBuilding { long osmId; string label; GameObject prefab; int overrideLevels; RoofOverride overrideRoof; KeyBuilding(); KeyBuilding(long osmId, string label) }`
  - `WorldGenSettings : ScriptableObject` — field publik persis seperti kode Step 3; `GeoBounds Bounds`; `GeoProjection CreateProjection()`; `BuildingMeshParams MeshParams`; `BuildingMaterialSet MaterialSet(BuildingMaterialSetId id)`
  - `static class DefaultSettingsFactory { WorldGenSettings CreateInMemory(); void Populate(WorldGenSettings s); WorldGenSettings LoadOrCreateAsset() }`

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/WorldGen/Tests/DefaultSettingsTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class DefaultSettingsTests
    {
        WorldGenSettings s;

        [SetUp]
        public void SetUp() => s = DefaultSettingsFactory.CreateInMemory();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(s);

        [Test]
        public void Area_MatchesSpec()
        {
            Assert.AreEqual(-6.9850, s.south);
            Assert.AreEqual(107.6255, s.west);
            Assert.AreEqual(-6.9675, s.north);
            Assert.AreEqual(107.6380, s.east);
            Assert.AreEqual(-6.97265, s.originLat);
            Assert.AreEqual(107.63400, s.originLon);
            Assert.AreEqual(100f, s.chunkSize);
        }

        [Test]
        public void Download_MatchesSpec()
        {
            CollectionAssert.AreEqual(new[]
            {
                "https://overpass-api.de/api/interpreter",
                "https://overpass.kumi.systems/api/interpreter",
                "https://overpass.private.coffee/api/interpreter"
            }, s.overpassEndpoints);
            Assert.AreEqual(90, s.timeoutSeconds);
            Assert.AreEqual("WorldGenData/osm_raw.json", s.rawDataPath);
        }

        [Test]
        public void RoadRules_MatchSpecTable()
        {
            Assert.AreEqual(13, s.roadRules.Count);
            var tertiary = s.roadRules.Single(r => r.highway == "tertiary");
            Assert.AreEqual(6f, tertiary.defaultWidth);
            Assert.AreEqual(6, tertiary.rank);
            Assert.AreEqual(30f, tertiary.lampSpacing);
            var service = s.roadRules.Single(r => r.highway == "service");
            Assert.AreEqual(3.5f, service.defaultWidth);
            Assert.AreEqual(50f, service.lampSpacing);
            var footway = s.roadRules.Single(r => r.highway == "footway");
            Assert.AreEqual(1.5f, footway.defaultWidth);
            Assert.AreEqual(0, footway.rank);
            Assert.AreEqual(0f, footway.lampSpacing);
            Assert.AreEqual(3f, s.lanesWidth);
            Assert.AreEqual(0.060f, s.roadBaseY, 1e-6f);
            Assert.AreEqual(0.005f, s.roadRankStep, 1e-6f);
        }

        [Test]
        public void BuildingRules_MatchSpecTable()
        {
            Assert.AreEqual(8, s.buildingRules.Count);
            var religious = s.buildingRules[2];
            CollectionAssert.Contains(religious.buildingValues, "mosque");
            Assert.AreEqual(5f, religious.levelHeightOverride);
            Assert.IsTrue(s.buildingRules[5].roofOnly);
            var residential = s.buildingRules[7];
            Assert.AreEqual(BuildingContext.NonCampus, residential.context);
            Assert.AreEqual(1, residential.minLevels);
            Assert.AreEqual(3, residential.maxLevels);
            Assert.IsTrue(residential.allowGable);
            Assert.AreEqual(BuildingMaterialSetId.Residential, residential.materialSet);
            Assert.AreEqual(BuildingMaterialSetId.Campus, s.buildingRules[6].materialSet);
        }

        [Test]
        public void BuildingScalars_MatchSpec()
        {
            Assert.AreEqual(3.2f, s.levelHeight);
            Assert.AreEqual(3f, s.facadeTileWidth);
            Assert.AreEqual(0.6f, s.parapetHeight);
            Assert.AreEqual(200f, s.gableMaxArea);
            Assert.AreEqual(0.85f, s.gableMinRectangularity);
            Assert.AreEqual(0.25f, s.gablePitchRatio);
            Assert.AreEqual(0.3f, s.gableOverhang);
            Assert.AreEqual(2.5f, s.minWallHeight);
        }

        [Test]
        public void AreaRules_MatchSpecTable()
        {
            Assert.AreEqual(9, s.areaRules.Count);
            var campus = s.areaRules.Single(a => a.category == AreaCategory.Campus);
            Assert.AreEqual(0.015f, campus.yOffset, 1e-6f);
            Assert.AreEqual(20f, campus.treeSpacing);
            var cemetery = s.areaRules.Single(a => a.category == AreaCategory.Cemetery);
            Assert.IsTrue(cemetery.Matches(new Dictionary<string, string> { { "amenity", "grave_yard" } }));
            Assert.IsTrue(cemetery.Matches(new Dictionary<string, string> { { "landuse", "cemetery" } }));
            Assert.AreEqual(0, s.areaRules.Single(a => a.category == AreaCategory.Paving).matches.Length);
        }

        [Test]
        public void WaterAndProps_MatchSpec()
        {
            Assert.AreEqual(0.045f, s.waterAreaY, 1e-6f);
            Assert.AreEqual(0.050f, s.waterLineY, 1e-6f);
            Assert.AreEqual(8f, s.waterwayWidths.Single(w => w.waterway == "river").width);
            Assert.AreEqual(1.5f, s.waterwayWidths.Single(w => w.waterway == "ditch").width);
            Assert.AreEqual(4000, s.maxTrees);
            Assert.AreEqual(8f, s.lampMinSpacing);
            Assert.AreEqual(0.6f, s.lampOffset);
        }

        [Test]
        public void KeyBuildings_MatchSpec()
        {
            CollectionAssert.AreEqual(new long[] { 1257717375, 197341486, 1293369802, 1293369773, 1293369796 },
                s.keyBuildings.Select(k => k.osmId).ToArray());
            Assert.AreEqual("Telkom University Landmark Tower (TULT)", s.keyBuildings[0].label);
        }

        [Test]
        public void BuildingRule_Matches()
        {
            var rule = new BuildingRule { buildingValues = new[] { "shed" }, context = BuildingContext.Any };
            Assert.IsTrue(rule.Matches("shed", false));
            Assert.IsFalse(rule.Matches("house", false));
            var campusOnly = new BuildingRule { buildingValues = new string[0], context = BuildingContext.Campus };
            Assert.IsTrue(campusOnly.Matches("yes", true));
            Assert.IsFalse(campusOnly.Matches("yes", false));
        }

        [Test]
        public void TagMatch_EmptyValuesMatchesAnyValue()
        {
            var any = new TagMatch("highway");
            Assert.IsTrue(any.Matches(new Dictionary<string, string> { { "highway", "x" } }));
            Assert.IsFalse(any.Matches(new Dictionary<string, string> { { "building", "x" } }));
        }
    }
}
```

- [ ] **Step 2: Jalankan, pastikan gagal**

Procedure R, lalu Procedure T dengan `Group = "DefaultSettingsTests"`. Expected: COMPILE FAILED.

- [ ] **Step 3: Implementasi**

`Assets/WorldGen/Editor/AssetFolders.cs`:

```csharp
using UnityEditor;

namespace TycoWorld.WorldGen.Authoring
{
    public static class AssetFolders
    {
        /// <summary>Creates a project folder (and missing parents), e.g. "Assets/WorldGen/Config".</summary>
        public static void Ensure(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            int slash = folder.LastIndexOf('/');
            string parent = folder.Substring(0, slash);
            Ensure(parent);
            AssetDatabase.CreateFolder(parent, folder.Substring(slash + 1));
        }
    }
}
```

`Assets/WorldGen/Editor/Settings/WorldGenRules.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    [Serializable]
    public sealed class RoadRule
    {
        public string highway = "";
        public float defaultWidth;
        public int rank;
        public float lampSpacing;
        public Material material;

        public RoadRule() { }

        public RoadRule(string highway, float defaultWidth, int rank, float lampSpacing)
        {
            this.highway = highway;
            this.defaultWidth = defaultWidth;
            this.rank = rank;
            this.lampSpacing = lampSpacing;
        }
    }

    public enum BuildingContext { Any, Campus, NonCampus }

    public enum BuildingMaterialSetId { Residential, Campus }

    [Serializable]
    public sealed class BuildingRule
    {
        public string name = "";
        public string[] buildingValues = new string[0];
        public BuildingContext context;
        public int minLevels = 1;
        public int maxLevels = 1;
        public float levelHeightOverride;
        public bool allowGable;
        public bool roofOnly;
        public BuildingMaterialSetId materialSet;

        public bool Matches(string buildingValue, bool isCampus)
        {
            if (context == BuildingContext.Campus && !isCampus) return false;
            if (context == BuildingContext.NonCampus && isCampus) return false;
            if (buildingValues == null || buildingValues.Length == 0) return true;
            foreach (var v in buildingValues)
                if (v == buildingValue) return true;
            return false;
        }
    }

    [Serializable]
    public sealed class BuildingMaterialSet
    {
        public Material[] groundFloor = new Material[0];
        public Material[] upper = new Material[0];
        public Material[] trim = new Material[0];
        public Material[] flatRoof = new Material[0];
        public Material[] gableRoof = new Material[0];
    }

    [Serializable]
    public sealed class TagMatch
    {
        public string key = "";
        public string[] values = new string[0];

        public TagMatch() { }

        public TagMatch(string key, params string[] values)
        {
            this.key = key;
            this.values = values ?? new string[0];
        }

        public bool Matches(IReadOnlyDictionary<string, string> tags)
        {
            if (tags == null || !tags.TryGetValue(key, out var value)) return false;
            if (values == null || values.Length == 0) return true;
            foreach (var v in values)
                if (v == value) return true;
            return false;
        }
    }

    public enum AreaCategory { Campus, Hardground, Grass, Wood, Farmland, Parking, Cemetery, Pitch, Paving }

    [Serializable]
    public sealed class AreaRule
    {
        public string name = "";
        public TagMatch[] matches = new TagMatch[0];
        public AreaCategory category;
        public float yOffset;
        public float treeSpacing;
        public Material material;

        public bool Matches(IReadOnlyDictionary<string, string> tags)
        {
            if (matches == null) return false;
            foreach (var m in matches)
                if (m != null && m.Matches(tags)) return true;
            return false;
        }
    }

    [Serializable]
    public sealed class WaterwayWidth
    {
        public string waterway = "";
        public float width;

        public WaterwayWidth() { }

        public WaterwayWidth(string waterway, float width)
        {
            this.waterway = waterway;
            this.width = width;
        }
    }

    [Serializable]
    public sealed class KeyBuilding
    {
        public long osmId;
        public string label = "";
        public GameObject prefab;
        public int overrideLevels;
        public RoofOverride overrideRoof;

        public KeyBuilding() { }

        public KeyBuilding(long osmId, string label)
        {
            this.osmId = osmId;
            this.label = label;
        }
    }
}
```

`Assets/WorldGen/Editor/Settings/WorldGenSettings.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>All world generator parameters. Defaults come from DefaultSettingsFactory.Populate (spec values).</summary>
    [CreateAssetMenu(menuName = "Tyco World/World Gen Settings", fileName = "WorldGenSettings")]
    public sealed class WorldGenSettings : ScriptableObject
    {
        [Header("Area")]
        public double south;
        public double west;
        public double north;
        public double east;
        public double originLat;
        public double originLon;
        public float chunkSize;

        [Header("Download")]
        public string[] overpassEndpoints = new string[0];
        public int timeoutSeconds;
        public string rawDataPath = "";

        [Header("Roads")]
        public List<RoadRule> roadRules = new List<RoadRule>();
        public float lanesWidth;
        public float roadBaseY;
        public float roadRankStep;

        [Header("Buildings")]
        public float levelHeight;
        public float facadeTileWidth;
        public float parapetHeight;
        public float gableMaxArea;
        public float gableMinRectangularity;
        public float gablePitchRatio;
        public float gableOverhang;
        public float minWallHeight;
        public List<BuildingRule> buildingRules = new List<BuildingRule>();
        public BuildingMaterialSet residentialMaterials = new BuildingMaterialSet();
        public BuildingMaterialSet campusMaterials = new BuildingMaterialSet();

        [Header("Areas")]
        public List<AreaRule> areaRules = new List<AreaRule>();

        [Header("Water")]
        public Material waterMaterial;
        public float waterAreaY;
        public float waterLineY;
        public List<WaterwayWidth> waterwayWidths = new List<WaterwayWidth>();

        [Header("Ground")]
        public Material groundMaterial;

        [Header("Props")]
        public GameObject[] treePrefabs = new GameObject[0];
        public GameObject lampPrefab;
        public int maxTrees;
        public float lampMinSpacing;
        public float lampOffset;

        [Header("Key buildings")]
        public List<KeyBuilding> keyBuildings = new List<KeyBuilding>();

        [Header("Other")]
        public Material fallbackMaterial;

        public GeoBounds Bounds => new GeoBounds(south, west, north, east);

        public GeoProjection CreateProjection() => new GeoProjection(originLat, originLon);

        public BuildingMeshParams MeshParams => new BuildingMeshParams
        {
            FacadeTileWidth = facadeTileWidth,
            ParapetHeight = parapetHeight,
            GableMaxArea = gableMaxArea,
            GableMinRectangularity = gableMinRectangularity,
            GablePitchRatio = gablePitchRatio,
            GableOverhang = gableOverhang
        };

        public BuildingMaterialSet MaterialSet(BuildingMaterialSetId id) =>
            id == BuildingMaterialSetId.Campus ? campusMaterials : residentialMaterials;

        void Reset() => DefaultSettingsFactory.Populate(this);
    }
}
```

`Assets/WorldGen/Editor/Settings/DefaultSettingsFactory.cs`:

```csharp
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Single source of the spec's default values.</summary>
    public static class DefaultSettingsFactory
    {
        public static WorldGenSettings CreateInMemory()
        {
            var s = ScriptableObject.CreateInstance<WorldGenSettings>();
            Populate(s);
            return s;
        }

        public static WorldGenSettings LoadOrCreateAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<WorldGenSettings>(WorldGenPaths.SettingsAsset);
            if (existing != null) return existing;
            AssetFolders.Ensure(WorldGenPaths.ConfigFolder);
            var s = CreateInMemory();
            AssetDatabase.CreateAsset(s, WorldGenPaths.SettingsAsset);
            AssetDatabase.SaveAssets();
            return s;
        }

        public static void Populate(WorldGenSettings s)
        {
            s.south = -6.9850;
            s.west = 107.6255;
            s.north = -6.9675;
            s.east = 107.6380;
            s.originLat = -6.97265;
            s.originLon = 107.63400;
            s.chunkSize = 100f;

            s.overpassEndpoints = new[]
            {
                "https://overpass-api.de/api/interpreter",
                "https://overpass.kumi.systems/api/interpreter",
                "https://overpass.private.coffee/api/interpreter"
            };
            s.timeoutSeconds = 90;
            s.rawDataPath = "WorldGenData/osm_raw.json";

            s.lanesWidth = 3f;
            s.roadBaseY = 0.060f;
            s.roadRankStep = 0.005f;
            s.roadRules = new List<RoadRule>
            {
                new RoadRule("primary", 8f, 8, 30f),
                new RoadRule("secondary", 7f, 7, 30f),
                new RoadRule("tertiary", 6f, 6, 30f),
                new RoadRule("unclassified", 5f, 5, 40f),
                new RoadRule("residential", 5f, 4, 40f),
                new RoadRule("living_street", 4f, 3, 0f),
                new RoadRule("pedestrian", 3f, 3, 0f),
                new RoadRule("service", 3.5f, 2, 50f),
                new RoadRule("track", 3f, 1, 0f),
                new RoadRule("footway", 1.5f, 0, 0f),
                new RoadRule("path", 1.5f, 0, 0f),
                new RoadRule("cycleway", 1.5f, 0, 0f),
                new RoadRule("steps", 1.5f, 0, 0f)
            };

            s.levelHeight = 3.2f;
            s.facadeTileWidth = 3f;
            s.parapetHeight = 0.6f;
            s.gableMaxArea = 200f;
            s.gableMinRectangularity = 0.85f;
            s.gablePitchRatio = 0.25f;
            s.gableOverhang = 0.3f;
            s.minWallHeight = 2.5f;
            s.buildingRules = new List<BuildingRule>
            {
                Rule("Dormitory", BuildingContext.Any, 4, 4, BuildingMaterialSetId.Campus, "dormitory"),
                Rule("Institutional", BuildingContext.Any, 3, 3, BuildingMaterialSetId.Campus, "university", "school", "college", "office", "public", "hospital"),
                Rule("Religious", BuildingContext.Any, 1, 1, BuildingMaterialSetId.Residential, "mosque", "church", "temple", "religious"),
                Rule("Commercial", BuildingContext.Any, 2, 2, BuildingMaterialSetId.Residential, "commercial", "retail", "shop"),
                Rule("Small", BuildingContext.Any, 1, 1, BuildingMaterialSetId.Residential, "garage", "garages", "shed", "hut", "carport", "kiosk"),
                Rule("RoofOnly", BuildingContext.Any, 1, 1, BuildingMaterialSetId.Residential, "roof"),
                Rule("CampusOther", BuildingContext.Campus, 3, 3, BuildingMaterialSetId.Campus),
                Rule("Residential", BuildingContext.NonCampus, 1, 3, BuildingMaterialSetId.Residential)
            };
            s.buildingRules[2].levelHeightOverride = 5f;
            s.buildingRules[4].allowGable = true;
            s.buildingRules[5].roofOnly = true;
            s.buildingRules[7].allowGable = true;
            s.residentialMaterials = new BuildingMaterialSet();
            s.campusMaterials = new BuildingMaterialSet();

            s.areaRules = new List<AreaRule>
            {
                Area("Parking", AreaCategory.Parking, 0.030f, 0f, new TagMatch("amenity", "parking")),
                Area("Cemetery", AreaCategory.Cemetery, 0.030f, 10f, new TagMatch("amenity", "grave_yard"), new TagMatch("landuse", "cemetery")),
                Area("Pitch", AreaCategory.Pitch, 0.030f, 0f, new TagMatch("leisure", "pitch", "track", "sports_centre", "stadium")),
                Area("Farmland", AreaCategory.Farmland, 0.025f, 0f, new TagMatch("landuse", "farmland", "paddy")),
                Area("Wood", AreaCategory.Wood, 0.025f, 4f, new TagMatch("natural", "wood", "scrub"), new TagMatch("landuse", "forest")),
                Area("Grass", AreaCategory.Grass, 0.025f, 12f, new TagMatch("landuse", "grass", "village_green", "meadow", "recreation_ground"), new TagMatch("leisure", "park", "garden"), new TagMatch("natural", "grassland")),
                Area("Hardground", AreaCategory.Hardground, 0.020f, 0f, new TagMatch("landuse", "commercial", "retail", "industrial", "construction", "brownfield")),
                Area("Campus", AreaCategory.Campus, 0.015f, 20f, new TagMatch("amenity", "university")),
                Area("Paving", AreaCategory.Paving, 0.035f, 0f)
            };

            s.waterAreaY = 0.045f;
            s.waterLineY = 0.050f;
            s.waterwayWidths = new List<WaterwayWidth>
            {
                new WaterwayWidth("river", 8f),
                new WaterwayWidth("canal", 4f),
                new WaterwayWidth("stream", 3f),
                new WaterwayWidth("drain", 1.5f),
                new WaterwayWidth("ditch", 1.5f)
            };

            s.treePrefabs = new GameObject[0];
            s.lampPrefab = null;
            s.maxTrees = 4000;
            s.lampMinSpacing = 8f;
            s.lampOffset = 0.6f;

            s.keyBuildings = new List<KeyBuilding>
            {
                new KeyBuilding(1257717375, "Telkom University Landmark Tower (TULT)"),
                new KeyBuilding(197341486, "Gedung Bangkit (Rektorat)"),
                new KeyBuilding(1293369802, "Gedung Tokong Nanas"),
                new KeyBuilding(1293369773, "Asrama Putra Gedung 1"),
                new KeyBuilding(1293369796, "Asrama Putri Gedung A")
            };
        }

        static BuildingRule Rule(string name, BuildingContext context, int minLevels, int maxLevels, BuildingMaterialSetId set, params string[] values) =>
            new BuildingRule { name = name, context = context, minLevels = minLevels, maxLevels = maxLevels, materialSet = set, buildingValues = values };

        static AreaRule Area(string name, AreaCategory category, float y, float treeSpacing, params TagMatch[] matches) =>
            new AreaRule { name = name, category = category, yOffset = y, treeSpacing = treeSpacing, matches = matches };
    }
}
```

- [ ] **Step 4: Jalankan, pastikan lulus**

Procedure R, lalu Procedure T dengan `Group = "DefaultSettingsTests"`. Expected: `passed=10 failed=0`.

- [ ] **Step 5: Commit**

Procedure C: `git add Assets/WorldGen`, subject `feat(worldgen): add settings asset with spec defaults`.

---

### Task 10: Laporan generate + klasifikasi fitur

**Files:**
- Create: `Assets/WorldGen/Editor/Pipeline/GenerationReport.cs`
- Create: `Assets/WorldGen/Editor/Classify/Features.cs`
- Create: `Assets/WorldGen/Editor/Classify/FeatureClassifier.cs`
- Create: `Assets/WorldGen/Tests/TestOsm.cs`
- Test: `Assets/WorldGen/Tests/FeatureClassifierTests.cs`

**Interfaces:**
- Consumes: `OsmData`, `OsmTags` (Task 4); `TagParsing`, `GeoProjection` (Task 3); `Polygon`, `Polygon2D`, `Clipping`, `SplitMix64` (Task 5); `RingAssembler` (Task 6); `BuildingSpec`, `RoofOverride`, `BuildingMeshBuilder.RoofOnlyHeight` (Task 8); `WorldGenSettings` + rules (Task 9).
- Produces:
  - `static class SkipReasons` (konstanta string: `BuildingUnclosed = "building:unclosed"`, `BuildingMissingNodes = "building:missing-nodes"`, `BuildingTooFewPoints = "building:too-few-points"`, `BuildingTooSmall = "building:too-small"`, `BuildingSelfIntersecting = "building:self-intersecting"`, `RelationUnclosedRing = "relation:unclosed-ring"`, `RoadMissingNodes = "road:missing-nodes"`, `RoadEmptyMesh = "road:empty-mesh"`, `WaterMissingNodes = "water:missing-nodes"`, `WaterEmptyMesh = "water:empty-mesh"`, `AreaUnclosed = "area:unclosed"`, `AreaMissingNodes = "area:missing-nodes"`, `AreaDegenerate = "area:degenerate"`, `AreaTriangulationFailed = "area:triangulation-failed"`, `RoofTriangulationFailed = "roof:triangulation-failed"`)
  - `static class CullReasons` (`BuildingOutside = "building:outside-bounds"`, `RoadOutside = "road:outside-bounds"`, `WaterOutside = "water:outside-bounds"`)
  - `sealed class GenerationReport` — `SortedDictionary<string,int> Counts`, `SortedDictionary<string,List<long>> Skipped`, `SortedDictionary<string,int> Culled`, `SortedDictionary<string,string> ChunkHashes`, `List<string> Warnings`, `List<KeyValuePair<string,double>> StageMillis`, `int Ignored, TreesPlaced, TreesDroppedByCap, LampsPlaced`, `double TotalMillis`; metode `Count(string kind, int amount = 1)`, `GetCount`, `Skip(string reason, long id)`, `SkippedTotal(string prefix = "")`, `Cull(string reason)`, `GetCulled`, `Warn(string)`, `Stage(string name, double millis)`, `string Summary()`
  - `sealed class LinePiece { LinePiece(List<Vector2> points, bool extendStart, bool extendEnd); List<Vector2> Points; bool ExtendStart, ExtendEnd }`
  - `RoadFeature { long Id; string Name; string Highway; RoadRule Rule; float Width; float Y; List<LinePiece> Pieces; IReadOnlyDictionary<string,string> Tags }`
  - `BuildingFeature { long Id; OsmElementType ElementType; string Name; BuildingSpec Spec; BuildingRule Rule; bool IsCampus; KeyBuilding Key; Vector2 Centroid; Tags }`
  - `AreaFeature { long Id; OsmElementType ElementType; string Name; AreaRule Rule; Polygon Shape; Tags }`
  - `WaterLineFeature { long Id; string Name; float Width; float Y; List<LinePiece> Pieces; Tags }`, `WaterAreaFeature { long Id; OsmElementType ElementType; string Name; Polygon Shape; Tags }`
  - `TreeFeature(long id, Vector2 position)` dengan field `Id, Position`
  - `ClassifiedWorld(Rect localBounds)` dengan `LocalBounds`, `Roads`, `Buildings`, `Areas`, `WaterLines`, `WaterAreas`, `Trees`
  - `sealed class FeatureClassifier { FeatureClassifier(WorldGenSettings settings, GenerationReport report); ClassifiedWorld Classify(OsmData osm) }`
  - Tes: `TestOsm` (lihat Step 1).

- [ ] **Step 1: Tulis helper tes dan tes yang gagal**

`Assets/WorldGen/Tests/TestOsm.cs`:

```csharp
using System.Collections.Generic;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    /// <summary>Builds OsmData from local-meter coordinates around the spec origin.</summary>
    internal sealed class TestOsm
    {
        public readonly OsmData Data = new OsmData();
        readonly GeoProjection projection = new GeoProjection(-6.97265, 107.63400);
        long nextNodeId = 9_000_000;

        public static IReadOnlyDictionary<string, string> Tags(params (string Key, string Value)[] tags)
        {
            var d = new Dictionary<string, string>();
            foreach (var (k, v) in tags)
                if (v != null) d[k] = v;
            return d;
        }

        public long Node(Vector2 local, params (string, string)[] tags)
        {
            projection.ToGeo(local, out double lat, out double lon);
            long id = nextNodeId++;
            Data.Nodes[id] = new OsmNode(id, lat, lon, Tags(tags));
            return id;
        }

        public OsmWay Way(long id, IList<Vector2> points, bool closed, params (string, string)[] tags)
        {
            var ids = new List<long>();
            foreach (var p in points) ids.Add(Node(p));
            if (closed) ids.Add(ids[0]);
            var way = new OsmWay(id, ids, Tags(tags));
            Data.Ways[id] = way;
            return way;
        }

        public OsmWay Rect(long id, float x, float z, float w, float d, params (string, string)[] tags) =>
            Way(id, new[] { new Vector2(x, z), new Vector2(x + w, z), new Vector2(x + w, z + d), new Vector2(x, z + d) }, true, tags);

        public OsmRelation Relation(long id, (long wayId, string role)[] members, params (string, string)[] tags)
        {
            var list = new List<OsmMember>();
            foreach (var (wayId, role) in members) list.Add(new OsmMember(TycoWorld.WorldGen.OsmElementType.Way, wayId, role));
            var relation = new OsmRelation(id, list, Tags(tags));
            Data.Relations[id] = relation;
            return relation;
        }
    }
}
```

`Assets/WorldGen/Tests/FeatureClassifierTests.cs`:

```csharp
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class FeatureClassifierTests
    {
        WorldGenSettings settings;
        GenerationReport report;

        [SetUp]
        public void SetUp()
        {
            settings = DefaultSettingsFactory.CreateInMemory();
            report = new GenerationReport();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(settings);

        ClassifiedWorld Classify(OsmData data) => new FeatureClassifier(settings, report).Classify(data);

        ClassifiedWorld Synthetic() => Classify(OsmParser.Parse(FixtureFiles.Read("synthetic_world.json")));

        [Test]
        public void Synthetic_House()
        {
            var b = Synthetic().Buildings.Single(x => x.Id == 401);
            Assert.AreEqual(10f, b.Spec.WallHeight, 1e-4f);
            Assert.AreEqual(3, b.Spec.Levels);
            Assert.IsTrue(b.Spec.AllowGable);
            Assert.IsFalse(b.IsCampus);
            Assert.AreEqual("Residential", b.Rule.name);
            Assert.AreEqual("Rumah Sintetis", b.Name);
            Assert.AreEqual("2023-05-01", b.Tags["check_date"]);
            Assert.AreEqual(95f, b.Spec.Footprint.Area, 0.5f);
        }

        [Test]
        public void Synthetic_RelationBuilding()
        {
            var b = Synthetic().Buildings.Single(x => x.Id == 201);
            Assert.AreEqual(OsmElementType.Relation, b.ElementType);
            Assert.AreEqual(1, b.Spec.Footprint.Holes.Count);
            Assert.IsTrue(b.IsCampus);
            Assert.AreEqual("Institutional", b.Rule.name);
            Assert.AreEqual(9.6f, b.Spec.WallHeight, 1e-4f);
        }

        [Test]
        public void Synthetic_Road()
        {
            var r = Synthetic().Roads.Single(x => x.Id == 402);
            Assert.AreEqual(6f, r.Width, 1e-4f);
            Assert.AreEqual(0.08f, r.Y, 1e-6f);
            Assert.AreEqual("Gang Sintetis", r.Name);
            Assert.AreEqual(1, r.Pieces.Count);
            Assert.IsFalse(r.Pieces[0].ExtendStart);
            Assert.IsFalse(r.Pieces[0].ExtendEnd);
        }

        [Test]
        public void Synthetic_MissingNodeRoad_IsSkipped()
        {
            var world = Synthetic();
            Assert.IsFalse(world.Roads.Any(r => r.Id == 403));
            CollectionAssert.Contains(report.Skipped[SkipReasons.RoadMissingNodes], 403L);
        }

        [Test]
        public void Synthetic_AreasAndTree()
        {
            var world = Synthetic();
            Assert.AreEqual(AreaCategory.Grass, world.Areas.Single(a => a.Id == 404).Rule.category);
            Assert.AreEqual(AreaCategory.Campus, world.Areas.Single(a => a.Id == 405).Rule.category);
            var tree = world.Trees.Single(t => t.Id == 301);
            Assert.AreEqual(-22.10f, tree.Position.x, 0.05f);
            Assert.AreEqual(-27.65f, tree.Position.y, 0.05f);
        }

        [Test]
        public void BuildingWinsOverAmenity()
        {
            var osm = new TestOsm();
            osm.Rect(1, 0, 0, 10, 10, ("building", "yes"), ("amenity", "parking"));
            var world = Classify(osm.Data);
            Assert.AreEqual(1, world.Buildings.Count);
            Assert.AreEqual(0, world.Areas.Count);
        }

        [TestCase("dormitory", false, 4, 12.8f)]
        [TestCase("university", false, 3, 9.6f)]
        [TestCase("mosque", false, 1, 5f)]
        [TestCase("commercial", false, 2, 6.4f)]
        [TestCase("shed", false, 1, 3.2f)]
        [TestCase("yes", true, 3, 9.6f)]
        public void BuildingDefaults(string value, bool campus, int levels, float wall)
        {
            var osm = new TestOsm();
            if (campus) osm.Rect(2, -50, -50, 100, 100, ("amenity", "university"));
            osm.Rect(1, 0, 0, 10, 10, ("building", value));
            var b = Classify(osm.Data).Buildings.Single(x => x.Id == 1);
            Assert.AreEqual(levels, b.Spec.Levels);
            Assert.AreEqual(wall, b.Spec.WallHeight, 1e-4f);
            Assert.AreEqual(campus, b.IsCampus);
        }

        [Test]
        public void RoofValue_IsRoofOnly()
        {
            var osm = new TestOsm();
            osm.Rect(1, 0, 0, 6, 4, ("building", "roof"));
            var b = Classify(osm.Data).Buildings.Single();
            Assert.IsTrue(b.Spec.RoofOnly);
            Assert.AreEqual(3f, b.Spec.WallHeight, 1e-4f);
        }

        [Test]
        public void NonCampusResidential_IsDeterministicAndInRange()
        {
            var osm = new TestOsm();
            osm.Rect(12345, 0, 0, 8, 8, ("building", "house"));
            int first = Classify(osm.Data).Buildings.Single().Spec.Levels;
            int second = Classify(osm.Data).Buildings.Single().Spec.Levels;
            Assert.AreEqual(first, second);
            Assert.That(first, Is.InRange(1, 3));
        }

        [TestCase("landuse", "cemetery", AreaCategory.Cemetery)]
        [TestCase("amenity", "parking", AreaCategory.Parking)]
        [TestCase("leisure", "pitch", AreaCategory.Pitch)]
        [TestCase("landuse", "paddy", AreaCategory.Farmland)]
        [TestCase("natural", "wood", AreaCategory.Wood)]
        [TestCase("leisure", "park", AreaCategory.Grass)]
        [TestCase("landuse", "industrial", AreaCategory.Hardground)]
        public void AreaRuleTable(string key, string value, AreaCategory expected)
        {
            var osm = new TestOsm();
            osm.Rect(1, 0, 0, 20, 20, (key, value));
            Assert.AreEqual(expected, Classify(osm.Data).Areas.Single().Rule.category);
        }

        [Test]
        public void HighwayAreaYes_IsPaving()
        {
            var osm = new TestOsm();
            osm.Rect(1, 0, 0, 20, 20, ("highway", "pedestrian"), ("area", "yes"));
            var world = Classify(osm.Data);
            Assert.AreEqual(0, world.Roads.Count);
            Assert.AreEqual(AreaCategory.Paving, world.Areas.Single().Rule.category);
        }

        [Test]
        public void WaterAreaAndLines()
        {
            var osm = new TestOsm();
            osm.Rect(1, 0, 0, 20, 20, ("natural", "water"));
            osm.Way(2, new[] { new Vector2(0, 50), new Vector2(40, 50) }, false, ("waterway", "drain"));
            osm.Way(3, new[] { new Vector2(0, 60), new Vector2(40, 60) }, false, ("waterway", "canal"), ("width", "3"));
            var world = Classify(osm.Data);
            Assert.AreEqual(1, world.WaterAreas.Count);
            Assert.AreEqual(1.5f, world.WaterLines.Single(w => w.Id == 2).Width, 1e-4f);
            Assert.AreEqual(3f, world.WaterLines.Single(w => w.Id == 3).Width, 1e-4f);
            Assert.AreEqual(0.050f, world.WaterLines[0].Y, 1e-6f);
        }

        [TestCase("abc", "2", 6f)]
        [TestCase(null, null, 5f)]
        [TestCase("6 m", null, 6f)]
        public void Width_FallsBackToLanesThenDefault(string width, string lanes, float expected)
        {
            var osm = new TestOsm();
            osm.Way(1, new[] { new Vector2(0, 0), new Vector2(50, 0) }, false, ("highway", "residential"), ("width", width), ("lanes", lanes));
            Assert.AreEqual(expected, Classify(osm.Data).Roads.Single().Width, 1e-4f);
        }

        [Test]
        public void Width_IsCultureInvariant()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("id-ID");
                var osm = new TestOsm();
                osm.Way(1, new[] { new Vector2(0, 0), new Vector2(50, 0) }, false, ("highway", "residential"), ("width", "6.5"));
                Assert.AreEqual(6.5f, Classify(osm.Data).Roads.Single().Width, 1e-4f);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Test]
        public void KeyBuildingOverride()
        {
            settings.keyBuildings.Add(new KeyBuilding(77, "Test") { overrideLevels = 5, overrideRoof = RoofOverride.Flat });
            var osm = new TestOsm();
            osm.Rect(77, 0, 0, 10, 10, ("building", "house"));
            var b = Classify(osm.Data).Buildings.Single();
            Assert.AreEqual(5, b.Spec.Levels);
            Assert.AreEqual(16f, b.Spec.WallHeight, 1e-4f);
            Assert.IsNotNull(b.Key);
            Assert.AreEqual(RoofOverride.Flat, b.Spec.RoofOverride);
        }

        [Test]
        public void BuildingOutsideBounds_IsCulled()
        {
            var osm = new TestOsm();
            osm.Rect(1, 0, 3000, 10, 10, ("building", "yes"));
            Assert.AreEqual(0, Classify(osm.Data).Buildings.Count);
            Assert.AreEqual(1, report.GetCulled(CullReasons.BuildingOutside));
        }

        [Test]
        public void RoadCrossingBounds_IsClipped()
        {
            var osm = new TestOsm();
            osm.Way(1, new[] { new Vector2(0, 0), new Vector2(0, 3000) }, false, ("highway", "residential"));
            var piece = Classify(osm.Data).Roads.Single().Pieces.Single();
            Assert.IsFalse(piece.ExtendEnd);
            Assert.AreEqual(569.54f, piece.Points[piece.Points.Count - 1].y, 0.1f);
        }

        [Test]
        public void TinyBuilding_IsSkipped()
        {
            var osm = new TestOsm();
            osm.Rect(1, 0, 0, 1.5f, 1.5f, ("building", "yes"));
            Classify(osm.Data);
            CollectionAssert.Contains(report.Skipped[SkipReasons.BuildingTooSmall], 1L);
        }

        [Test]
        public void SelfIntersectingBuilding_IsSkipped()
        {
            var osm = new TestOsm();
            osm.Way(1, new[] { new Vector2(0, 0), new Vector2(10, 10), new Vector2(10, 0), new Vector2(0, 10) }, true, ("building", "yes"));
            Classify(osm.Data);
            CollectionAssert.Contains(report.Skipped[SkipReasons.BuildingSelfIntersecting], 1L);
        }

        [Test]
        public void UnclosedBuildingWay_IsSkipped()
        {
            var osm = new TestOsm();
            osm.Way(1, new[] { new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10) }, false, ("building", "yes"));
            Classify(osm.Data);
            CollectionAssert.Contains(report.Skipped[SkipReasons.BuildingUnclosed], 1L);
        }

        [Test]
        public void RelationWithOpenOuter_IsSkipped()
        {
            var osm = new TestOsm();
            osm.Way(10, new[] { new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10) }, false);
            osm.Relation(500, new[] { (10L, "outer") }, ("type", "multipolygon"), ("building", "yes"));
            Classify(osm.Data);
            CollectionAssert.Contains(report.Skipped[SkipReasons.RelationUnclosedRing], 500L);
        }
    }
}
```

- [ ] **Step 2: Jalankan, pastikan gagal**

Procedure R, lalu Procedure T dengan `Group = "FeatureClassifierTests"`. Expected: COMPILE FAILED.

- [ ] **Step 3: Implementasi**

`Assets/WorldGen/Editor/Pipeline/GenerationReport.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace TycoWorld.WorldGen.Authoring
{
    public static class SkipReasons
    {
        public const string BuildingUnclosed = "building:unclosed";
        public const string BuildingMissingNodes = "building:missing-nodes";
        public const string BuildingTooFewPoints = "building:too-few-points";
        public const string BuildingTooSmall = "building:too-small";
        public const string BuildingSelfIntersecting = "building:self-intersecting";
        public const string RelationUnclosedRing = "relation:unclosed-ring";
        public const string RoadMissingNodes = "road:missing-nodes";
        public const string RoadEmptyMesh = "road:empty-mesh";
        public const string WaterMissingNodes = "water:missing-nodes";
        public const string WaterEmptyMesh = "water:empty-mesh";
        public const string AreaUnclosed = "area:unclosed";
        public const string AreaMissingNodes = "area:missing-nodes";
        public const string AreaDegenerate = "area:degenerate";
        public const string AreaTriangulationFailed = "area:triangulation-failed";
        public const string RoofTriangulationFailed = "roof:triangulation-failed";
    }

    public static class CullReasons
    {
        public const string BuildingOutside = "building:outside-bounds";
        public const string RoadOutside = "road:outside-bounds";
        public const string WaterOutside = "water:outside-bounds";
    }

    /// <summary>Counts, skips and timings of one generation run.</summary>
    public sealed class GenerationReport
    {
        public readonly SortedDictionary<string, int> Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        public readonly SortedDictionary<string, List<long>> Skipped = new SortedDictionary<string, List<long>>(StringComparer.Ordinal);
        public readonly SortedDictionary<string, int> Culled = new SortedDictionary<string, int>(StringComparer.Ordinal);
        public readonly SortedDictionary<string, string> ChunkHashes = new SortedDictionary<string, string>(StringComparer.Ordinal);
        public readonly List<string> Warnings = new List<string>();
        public readonly List<KeyValuePair<string, double>> StageMillis = new List<KeyValuePair<string, double>>();
        public int Ignored;
        public int TreesPlaced;
        public int TreesDroppedByCap;
        public int LampsPlaced;
        public double TotalMillis;

        public void Count(string kind, int amount = 1)
        {
            Counts.TryGetValue(kind, out int n);
            Counts[kind] = n + amount;
        }

        public int GetCount(string kind) => Counts.TryGetValue(kind, out int n) ? n : 0;

        public void Skip(string reason, long id)
        {
            if (!Skipped.TryGetValue(reason, out var ids))
            {
                ids = new List<long>();
                Skipped[reason] = ids;
            }
            ids.Add(id);
        }

        public int SkippedTotal(string reasonPrefix = "") =>
            Skipped.Where(kv => kv.Key.StartsWith(reasonPrefix, StringComparison.Ordinal)).Sum(kv => kv.Value.Count);

        public void Cull(string reason)
        {
            Culled.TryGetValue(reason, out int n);
            Culled[reason] = n + 1;
        }

        public int GetCulled(string reason) => Culled.TryGetValue(reason, out int n) ? n : 0;

        public void Warn(string message)
        {
            if (!Warnings.Contains(message)) Warnings.Add(message);
        }

        public void Stage(string name, double millis) => StageMillis.Add(new KeyValuePair<string, double>(name, millis));

        public string Summary()
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine("Features: " + Join(Counts.Select(kv => kv.Key + "=" + kv.Value.ToString(inv))));
            sb.AppendLine(string.Format(inv, "Trees: {0} (dropped by cap {1}), Lamps: {2}", TreesPlaced, TreesDroppedByCap, LampsPlaced));
            sb.AppendLine("Skipped: " + Join(Skipped.Select(kv =>
                kv.Key + "=" + kv.Value.Count.ToString(inv) + " [" +
                string.Join(",", kv.Value.Take(10).Select(id => id.ToString(inv))) + (kv.Value.Count > 10 ? ",..." : "") + "]")));
            sb.AppendLine("Culled: " + Join(Culled.Select(kv => kv.Key + "=" + kv.Value.ToString(inv))));
            sb.AppendLine("Ignored: " + Ignored.ToString(inv));
            sb.AppendLine("Chunks: " + ChunkHashes.Count.ToString(inv));
            sb.AppendLine("Stages: " + Join(StageMillis.Select(kv => kv.Key + " " + kv.Value.ToString("0", inv) + " ms")) +
                          string.Format(inv, " | total {0:0} ms", TotalMillis));
            foreach (var warning in Warnings) sb.AppendLine("WARNING: " + warning);
            return sb.ToString();
        }

        static string Join(IEnumerable<string> parts)
        {
            string s = string.Join(", ", parts);
            return s.Length == 0 ? "-" : s;
        }
    }
}
```

`Assets/WorldGen/Editor/Classify/Features.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    public sealed class LinePiece
    {
        public readonly List<Vector2> Points;
        public readonly bool ExtendStart;
        public readonly bool ExtendEnd;

        public LinePiece(List<Vector2> points, bool extendStart, bool extendEnd)
        {
            Points = points;
            ExtendStart = extendStart;
            ExtendEnd = extendEnd;
        }
    }

    public sealed class RoadFeature
    {
        public long Id;
        public string Name;
        public string Highway;
        public RoadRule Rule;
        public float Width;
        public float Y;
        public readonly List<LinePiece> Pieces = new List<LinePiece>();
        public IReadOnlyDictionary<string, string> Tags = OsmTags.Empty;
    }

    public sealed class BuildingFeature
    {
        public long Id;
        public OsmElementType ElementType;
        public string Name;
        public BuildingSpec Spec;
        public BuildingRule Rule;
        public bool IsCampus;
        public KeyBuilding Key;
        public Vector2 Centroid;
        public IReadOnlyDictionary<string, string> Tags = OsmTags.Empty;
    }

    public sealed class AreaFeature
    {
        public long Id;
        public OsmElementType ElementType;
        public string Name;
        public AreaRule Rule;
        public Polygon Shape;
        public IReadOnlyDictionary<string, string> Tags = OsmTags.Empty;
    }

    public sealed class WaterLineFeature
    {
        public long Id;
        public string Name;
        public float Width;
        public float Y;
        public readonly List<LinePiece> Pieces = new List<LinePiece>();
        public IReadOnlyDictionary<string, string> Tags = OsmTags.Empty;
    }

    public sealed class WaterAreaFeature
    {
        public long Id;
        public OsmElementType ElementType;
        public string Name;
        public Polygon Shape;
        public IReadOnlyDictionary<string, string> Tags = OsmTags.Empty;
    }

    public sealed class TreeFeature
    {
        public readonly long Id;
        public readonly Vector2 Position;

        public TreeFeature(long id, Vector2 position)
        {
            Id = id;
            Position = position;
        }
    }

    public sealed class ClassifiedWorld
    {
        public readonly Rect LocalBounds;
        public readonly List<RoadFeature> Roads = new List<RoadFeature>();
        public readonly List<BuildingFeature> Buildings = new List<BuildingFeature>();
        public readonly List<AreaFeature> Areas = new List<AreaFeature>();
        public readonly List<WaterLineFeature> WaterLines = new List<WaterLineFeature>();
        public readonly List<WaterAreaFeature> WaterAreas = new List<WaterAreaFeature>();
        public readonly List<TreeFeature> Trees = new List<TreeFeature>();

        public ClassifiedWorld(Rect localBounds) { LocalBounds = localBounds; }
    }
}
```

`Assets/WorldGen/Editor/Classify/FeatureClassifier.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Turns raw OSM elements into typed, projected, cleaned features (spec section 6 and 7).</summary>
    public sealed class FeatureClassifier
    {
        readonly WorldGenSettings settings;
        readonly GenerationReport report;
        readonly GeoProjection projection;
        readonly Rect bounds;
        readonly Dictionary<string, RoadRule> roadRules = new Dictionary<string, RoadRule>();
        readonly Dictionary<string, float> waterwayWidths = new Dictionary<string, float>();
        readonly Dictionary<long, KeyBuilding> keyBuildings = new Dictionary<long, KeyBuilding>();
        readonly AreaRule pavingRule;
        readonly Dictionary<long, Vector2> localCache = new Dictionary<long, Vector2>();
        readonly List<Polygon> campusPolygons = new List<Polygon>();
        OsmData data;

        public FeatureClassifier(WorldGenSettings settings, GenerationReport report)
        {
            this.settings = settings;
            this.report = report;
            projection = settings.CreateProjection();
            bounds = projection.ToLocalRect(settings.Bounds);
            foreach (var rule in settings.roadRules)
                if (rule != null && !string.IsNullOrEmpty(rule.highway)) roadRules[rule.highway] = rule;
            foreach (var w in settings.waterwayWidths)
                if (w != null && !string.IsNullOrEmpty(w.waterway)) waterwayWidths[w.waterway] = w.width;
            foreach (var key in settings.keyBuildings)
                if (key != null && key.osmId != 0) keyBuildings[key.osmId] = key;
            pavingRule = settings.areaRules.FirstOrDefault(a => a != null && a.category == AreaCategory.Paving);
        }

        public ClassifiedWorld Classify(OsmData osm)
        {
            data = osm ?? throw new ArgumentNullException(nameof(osm));
            localCache.Clear();
            campusPolygons.Clear();
            var world = new ClassifiedWorld(bounds);
            CollectCampusPolygons();

            var roadWays = new List<OsmWay>();
            foreach (var way in data.Ways.Values.OrderBy(w => w.Id))
            {
                var tags = way.Tags;
                if (IsBuilding(tags)) { ClassifyBuildingWay(way, world); continue; }

                string highway = tags.Tag("highway");
                if (highway != null && roadRules.ContainsKey(highway))
                {
                    if (tags.Tag("area") != "yes") roadWays.Add(way);
                    else if (pavingRule != null) ClassifyAreaWay(way, pavingRule, world);
                    else report.Ignored++;
                    continue;
                }

                string waterway = tags.Tag("waterway");
                if (waterway != null && waterwayWidths.TryGetValue(waterway, out float waterWidth))
                {
                    ClassifyWaterLine(way, waterWidth, world);
                    continue;
                }
                if (IsWaterArea(tags)) { ClassifyWaterAreaWay(way, world); continue; }

                var rule = MatchArea(tags);
                if (rule != null) { ClassifyAreaWay(way, rule, world); continue; }
                report.Ignored++;
            }
            ClassifyRoads(roadWays, world);

            foreach (var relation in data.Relations.Values.OrderBy(r => r.Id)) ClassifyRelation(relation, world);

            foreach (var node in data.Nodes.Values.OrderBy(n => n.Id))
            {
                if (node.Tags.Tag("natural") != "tree") continue;
                var p = LocalOf(node);
                if (bounds.Contains(p)) world.Trees.Add(new TreeFeature(node.Id, p));
            }
            return world;
        }

        static bool IsBuilding(IReadOnlyDictionary<string, string> tags)
        {
            string value = tags.Tag("building");
            return value != null && value != "no";
        }

        static bool IsWaterArea(IReadOnlyDictionary<string, string> tags)
        {
            if (tags.Tag("natural") == "water") return true;
            string landuse = tags.Tag("landuse");
            if (landuse == "reservoir" || landuse == "basin") return true;
            return tags.ContainsKey("water");
        }

        AreaRule MatchArea(IReadOnlyDictionary<string, string> tags)
        {
            foreach (var rule in settings.areaRules)
                if (rule != null && rule.Matches(tags)) return rule;
            return null;
        }

        Vector2 LocalOf(OsmNode node)
        {
            if (!localCache.TryGetValue(node.Id, out var p))
            {
                p = projection.ToLocal(node.Lat, node.Lon);
                localCache[node.Id] = p;
            }
            return p;
        }

        bool TryResolve(IReadOnlyList<long> nodeIds, out List<Vector2> points)
        {
            points = new List<Vector2>(nodeIds.Count);
            foreach (long id in nodeIds)
            {
                if (!data.Nodes.TryGetValue(id, out var node)) return false;
                points.Add(LocalOf(node));
            }
            return true;
        }

        void CollectCampusPolygons()
        {
            foreach (var way in data.Ways.Values.OrderBy(w => w.Id))
                if (way.Tags.Tag("amenity") == "university" && way.IsClosed && TryResolve(way.NodeIds, out var pts))
                    campusPolygons.Add(new Polygon(Polygon2D.Clean(pts)));
            foreach (var relation in data.Relations.Values.OrderBy(r => r.Id))
                if (relation.Tags.Tag("amenity") == "university" && TryBuildRelationPolygons(relation, out var polygons))
                    campusPolygons.AddRange(polygons);
        }

        bool IsCampus(Vector2 p)
        {
            foreach (var campus in campusPolygons)
                if (campus.Contains(p)) return true;
            return false;
        }

        // ---------- buildings ----------

        void ClassifyBuildingWay(OsmWay way, ClassifiedWorld world)
        {
            if (!way.IsClosed) { report.Skip(SkipReasons.BuildingUnclosed, way.Id); return; }
            if (!TryResolve(way.NodeIds, out var pts)) { report.Skip(SkipReasons.BuildingMissingNodes, way.Id); return; }
            AddBuilding(way.Id, OsmElementType.Way, way.Tags, new Polygon(pts), world);
        }

        void AddBuilding(long id, OsmElementType type, IReadOnlyDictionary<string, string> tags, Polygon raw, ClassifiedWorld world)
        {
            var outer = Polygon2D.Clean(raw.Outer);
            if (outer.Count < 3) { report.Skip(SkipReasons.BuildingTooFewPoints, id); return; }
            Polygon2D.EnsureWinding(outer, true);
            var holes = new List<List<Vector2>>();
            foreach (var h in raw.Holes)
            {
                var hole = Polygon2D.Clean(h);
                if (hole.Count < 3 || Mathf.Abs(Polygon2D.SignedArea(hole)) < 1f) continue;
                Polygon2D.EnsureWinding(hole, false);
                holes.Add(hole);
            }
            if (Polygon2D.IsSelfIntersecting(outer)) { report.Skip(SkipReasons.BuildingSelfIntersecting, id); return; }
            var polygon = new Polygon(outer, holes);
            if (polygon.Area < 4f) { report.Skip(SkipReasons.BuildingTooSmall, id); return; }
            var centroid = Polygon2D.Centroid(outer);
            if (!bounds.Contains(centroid)) { report.Cull(CullReasons.BuildingOutside); return; }

            bool campus = IsCampus(centroid);
            var rule = FindBuildingRule(tags.Tag("building") ?? "yes", campus);
            keyBuildings.TryGetValue(id, out var key);
            var spec = new BuildingSpec
            {
                Id = id,
                Footprint = polygon,
                AllowGable = rule.allowGable,
                RoofOnly = rule.roofOnly,
                RoofOverride = key != null ? key.overrideRoof : RoofOverride.Auto
            };
            ResolveHeight(id, tags, rule, key, spec);
            world.Buildings.Add(new BuildingFeature
            {
                Id = id, ElementType = type, Name = tags.Tag("name"), Spec = spec, Rule = rule,
                IsCampus = campus, Key = key, Centroid = centroid, Tags = tags
            });
        }

        BuildingRule FindBuildingRule(string buildingValue, bool campus)
        {
            foreach (var rule in settings.buildingRules)
                if (rule != null && rule.Matches(buildingValue, campus)) return rule;
            report.Warn("Tidak ada aturan bangunan untuk '" + buildingValue + "'; memakai default 1 lantai.");
            return new BuildingRule { name = "Fallback" };
        }

        void ResolveHeight(long id, IReadOnlyDictionary<string, string> tags, BuildingRule rule, KeyBuilding key, BuildingSpec spec)
        {
            float levelHeight = rule.levelHeightOverride > 0f ? rule.levelHeightOverride : settings.levelHeight;
            spec.LevelHeight = levelHeight;
            int levels;
            float wall;
            if (key != null && key.overrideLevels > 0)
            {
                levels = key.overrideLevels;
                wall = levels * levelHeight;
            }
            else if (TagParsing.TryParseLeadingFloat(tags.Tag("height"), out float height))
            {
                wall = height;
                levels = Mathf.Max(1, (int)Math.Floor(height / levelHeight + 0.5f));
            }
            else if (TagParsing.TryParseLevels(tags.Tag("building:levels"), out int tagged))
            {
                levels = tagged;
                wall = tagged * levelHeight;
            }
            else if (rule.roofOnly)
            {
                levels = 1;
                wall = BuildingMeshBuilder.RoofOnlyHeight;
            }
            else
            {
                levels = rule.minLevels >= rule.maxLevels
                    ? rule.minLevels
                    : SplitMix64.For(id, "levels").RangeInclusive(rule.minLevels, rule.maxLevels);
                wall = levels * levelHeight;
            }
            spec.Levels = Mathf.Max(1, levels);
            spec.WallHeight = Mathf.Max(wall, settings.minWallHeight);
        }

        // ---------- roads and water lines ----------

        void ClassifyRoads(List<OsmWay> ways, ClassifiedWorld world)
        {
            var resolved = new List<(OsmWay way, List<Vector2> points)>();
            foreach (var way in ways)
            {
                if (way.NodeIds.Count < 2 || !TryResolve(way.NodeIds, out var pts))
                {
                    report.Skip(SkipReasons.RoadMissingNodes, way.Id);
                    continue;
                }
                resolved.Add((way, pts));
            }

            var usage = new Dictionary<long, int>();
            foreach (var (way, _) in resolved)
                foreach (long id in new HashSet<long>(way.NodeIds))
                    usage[id] = usage.TryGetValue(id, out int count) ? count + 1 : 1;

            foreach (var (way, points) in resolved)
            {
                var rule = roadRules[way.Tags.Tag("highway")];
                var road = new RoadFeature
                {
                    Id = way.Id,
                    Name = way.Tags.Tag("name"),
                    Highway = rule.highway,
                    Rule = rule,
                    Width = ResolveWidth(way.Tags, rule.defaultWidth),
                    Y = settings.roadBaseY + rule.rank * settings.roadRankStep,
                    Tags = way.Tags
                };
                bool startJunction = usage[way.NodeIds[0]] > 1;
                bool endJunction = usage[way.NodeIds[way.NodeIds.Count - 1]] > 1;
                foreach (var piece in Clipping.ClipPolyline(points, bounds))
                    road.Pieces.Add(new LinePiece(piece.Points, startJunction && piece.StartsAtOriginalStart, endJunction && piece.EndsAtOriginalEnd));
                if (road.Pieces.Count == 0) { report.Cull(CullReasons.RoadOutside); continue; }
                world.Roads.Add(road);
            }
        }

        float ResolveWidth(IReadOnlyDictionary<string, string> tags, float fallback)
        {
            if (TagParsing.TryParseLeadingFloat(tags.Tag("width"), out float width)) return width;
            if (TagParsing.TryParseCount(tags.Tag("lanes"), 20, out int lanes)) return lanes * settings.lanesWidth;
            return fallback;
        }

        void ClassifyWaterLine(OsmWay way, float defaultWidth, ClassifiedWorld world)
        {
            if (way.NodeIds.Count < 2 || !TryResolve(way.NodeIds, out var pts))
            {
                report.Skip(SkipReasons.WaterMissingNodes, way.Id);
                return;
            }
            var line = new WaterLineFeature
            {
                Id = way.Id,
                Name = way.Tags.Tag("name"),
                Width = TagParsing.TryParseLeadingFloat(way.Tags.Tag("width"), out float w) ? w : defaultWidth,
                Y = settings.waterLineY,
                Tags = way.Tags
            };
            foreach (var piece in Clipping.ClipPolyline(pts, bounds)) line.Pieces.Add(new LinePiece(piece.Points, false, false));
            if (line.Pieces.Count == 0) { report.Cull(CullReasons.WaterOutside); return; }
            world.WaterLines.Add(line);
        }

        // ---------- areas ----------

        void ClassifyAreaWay(OsmWay way, AreaRule rule, ClassifiedWorld world)
        {
            if (!way.IsClosed) { report.Skip(SkipReasons.AreaUnclosed, way.Id); return; }
            if (!TryResolve(way.NodeIds, out var pts)) { report.Skip(SkipReasons.AreaMissingNodes, way.Id); return; }
            AddArea(way.Id, OsmElementType.Way, way.Tags, rule, new Polygon(pts), world);
        }

        void ClassifyWaterAreaWay(OsmWay way, ClassifiedWorld world)
        {
            if (!way.IsClosed) { report.Skip(SkipReasons.AreaUnclosed, way.Id); return; }
            if (!TryResolve(way.NodeIds, out var pts)) { report.Skip(SkipReasons.AreaMissingNodes, way.Id); return; }
            AddWaterArea(way.Id, OsmElementType.Way, way.Tags, new Polygon(pts), world);
        }

        void AddArea(long id, OsmElementType type, IReadOnlyDictionary<string, string> tags, AreaRule rule, Polygon raw, ClassifiedWorld world)
        {
            var shape = ClipAndClean(raw);
            if (shape == null) { report.Skip(SkipReasons.AreaDegenerate, id); return; }
            world.Areas.Add(new AreaFeature { Id = id, ElementType = type, Name = tags.Tag("name"), Rule = rule, Shape = shape, Tags = tags });
        }

        void AddWaterArea(long id, OsmElementType type, IReadOnlyDictionary<string, string> tags, Polygon raw, ClassifiedWorld world)
        {
            var shape = ClipAndClean(raw);
            if (shape == null) { report.Skip(SkipReasons.AreaDegenerate, id); return; }
            world.WaterAreas.Add(new WaterAreaFeature { Id = id, ElementType = type, Name = tags.Tag("name"), Shape = shape, Tags = tags });
        }

        Polygon ClipAndClean(Polygon raw)
        {
            var outer = Polygon2D.Clean(Clipping.ClipPolygon(Polygon2D.Clean(raw.Outer), bounds));
            if (outer.Count < 3 || Mathf.Abs(Polygon2D.SignedArea(outer)) < 1f) return null;
            Polygon2D.EnsureWinding(outer, true);
            var holes = new List<List<Vector2>>();
            foreach (var h in raw.Holes)
            {
                var hole = Polygon2D.Clean(Clipping.ClipPolygon(Polygon2D.Clean(h), bounds));
                if (hole.Count < 3 || Mathf.Abs(Polygon2D.SignedArea(hole)) < 1f) continue;
                Polygon2D.EnsureWinding(hole, false);
                holes.Add(hole);
            }
            return new Polygon(outer, holes);
        }

        // ---------- relations ----------

        void ClassifyRelation(OsmRelation relation, ClassifiedWorld world)
        {
            var tags = relation.Tags;
            if (tags.Tag("type") != "multipolygon") { report.Ignored++; return; }
            bool building = IsBuilding(tags);
            bool water = !building && IsWaterArea(tags);
            AreaRule rule = building || water ? null : MatchArea(tags);
            if (!building && !water && rule == null) { report.Ignored++; return; }
            if (!TryBuildRelationPolygons(relation, out var polygons))
            {
                report.Skip(SkipReasons.RelationUnclosedRing, relation.Id);
                return;
            }
            foreach (var polygon in polygons)
            {
                if (building) AddBuilding(relation.Id, OsmElementType.Relation, tags, polygon, world);
                else if (water) AddWaterArea(relation.Id, OsmElementType.Relation, tags, polygon, world);
                else AddArea(relation.Id, OsmElementType.Relation, tags, rule, polygon, world);
            }
        }

        bool TryBuildRelationPolygons(OsmRelation relation, out List<Polygon> polygons)
        {
            polygons = new List<Polygon>();
            var outerWays = new List<IReadOnlyList<long>>();
            var innerWays = new List<IReadOnlyList<long>>();
            foreach (var member in relation.Members)
            {
                if (member.Type != OsmElementType.Way) continue;
                if (!data.Ways.TryGetValue(member.Ref, out var way)) return false;
                if (member.Role == "inner") innerWays.Add(way.NodeIds);
                else outerWays.Add(way.NodeIds);
            }
            if (outerWays.Count == 0) return false;
            if (!RingAssembler.TryAssemble(outerWays, out var outerRings)) return false;
            if (!RingAssembler.TryAssemble(innerWays, out var innerRings)) return false;

            var holes = new List<List<Vector2>>();
            foreach (var ring in innerRings)
            {
                if (!TryResolve(ring, out var pts)) return false;
                holes.Add(pts);
            }
            foreach (var ring in outerRings)
            {
                if (!TryResolve(ring, out var outer)) return false;
                var own = new List<List<Vector2>>();
                foreach (var hole in holes)
                    if (hole.Count > 0 && Polygon2D.ContainsPoint(outer, hole[0])) own.Add(hole);
                polygons.Add(new Polygon(outer, own));
            }
            return true;
        }
    }
}
```

- [ ] **Step 4: Jalankan, pastikan lulus**

Procedure R, lalu Procedure T dengan `Group = "FeatureClassifierTests"`. Expected: `passed=34 failed=0` (18 tes biasa + 6 + 7 + 3 kasus TestCase).

- [ ] **Step 5: Commit**

Procedure C: `git add Assets/WorldGen`, subject `feat(worldgen): classify OSM features with spec rule tables`.

---

### Task 11: Grid spasial, Poisson disk, penempatan pohon & lampu

**Files:**
- Create: `Assets/WorldGen/Editor/Geometry/SpatialGrid.cs`
- Create: `Assets/WorldGen/Editor/Geometry/PoissonDisk.cs`
- Create: `Assets/WorldGen/Editor/Builders/PropPlacer.cs`
- Test: `Assets/WorldGen/Tests/PropPlacementTests.cs`

**Interfaces:**
- Consumes: `Polygon`, `Polygon2D`, `SplitMix64` (Task 5); `BuildingFeature`, `RoadFeature`, `AreaFeature`, `TreeFeature`, `LinePiece`, `GenerationReport` (Task 10); `RoadRule`, `AreaRule` (Task 9); `BuildingSpec` (Task 8).
- Produces:
  - `sealed class SpatialGrid<T> { SpatialGrid(float cellSize); void Insert(Rect area, T item); void Query(Rect area, HashSet<T> results) }`
  - `static class PoissonDisk { const int MaxGridCells = 2000000; List<Vector2> Sample(Rect bounds, float minDistance, SplitMix64 rng, int attempts = 20) }`
  - `struct PropPlacement { Vector2 Position; float YawDegrees; float Scale; int Variant }` (yaw memutar +Z lokal prefab)
  - `sealed class PropPlacer { const float MinTreeGap = 2f, TreeBuildingBuffer = 1f, TreeRoadClearance = 1f, LampBuildingBuffer = 0.3f; PropPlacer(IReadOnlyList<BuildingFeature> buildings, IReadOnlyList<RoadFeature> roads); bool IsInsideBuilding(Vector2 p, float buffer); bool IsOnRoad(Vector2 p, float clearance); List<PropPlacement> PlaceTrees(IReadOnlyList<TreeFeature> osmTrees, IReadOnlyList<AreaFeature> areas, int maxTrees, int variantCount, GenerationReport report); List<PropPlacement> PlaceLamps(IReadOnlyList<RoadFeature> roads, float minSpacing, float offset, GenerationReport report) }`

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/WorldGen/Tests/PropPlacementTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class PropPlacementTests
    {
        static Polygon Square(float x, float z, float w, float d) => new Polygon(new List<Vector2>
            { new Vector2(x, z), new Vector2(x + w, z), new Vector2(x + w, z + d), new Vector2(x, z + d) });

        static BuildingFeature Building(long id, float x, float z, float w, float d) =>
            new BuildingFeature { Id = id, Spec = new BuildingSpec { Id = id, Footprint = Square(x, z, w, d) } };

        static RoadFeature Road(long id, float width, float lampSpacing, params Vector2[] pts)
        {
            var road = new RoadFeature { Id = id, Width = width, Rule = new RoadRule("tertiary", width, 6, lampSpacing) };
            road.Pieces.Add(new LinePiece(new List<Vector2>(pts), false, false));
            return road;
        }

        static AreaFeature Grass(long id, Polygon shape, float spacing) =>
            new AreaFeature { Id = id, Shape = shape, Rule = new AreaRule { category = AreaCategory.Grass, treeSpacing = spacing } };

        [Test]
        public void SpatialGrid_FindsItemsAcrossCells()
        {
            var grid = new SpatialGrid<int>(10f);
            grid.Insert(Rect.MinMaxRect(-5, -5, 25, 5), 1);
            grid.Insert(Rect.MinMaxRect(100, 100, 101, 101), 2);
            var hits = new HashSet<int>();
            grid.Query(Rect.MinMaxRect(20, 0, 21, 1), hits);
            CollectionAssert.AreEquivalent(new[] { 1 }, hits);
        }

        [Test]
        public void SpatialGrid_NegativeCoordinates()
        {
            var grid = new SpatialGrid<string>(20f);
            grid.Insert(Rect.MinMaxRect(-41, -41, -39, -39), "a");
            var hits = new HashSet<string>();
            grid.Query(Rect.MinMaxRect(-40.5f, -40.5f, -40f, -40f), hits);
            Assert.IsTrue(hits.Contains("a"));
        }

        [Test]
        public void Poisson_RespectsMinimumDistanceAndBounds()
        {
            var bounds = Rect.MinMaxRect(0, 0, 100, 60);
            var pts = PoissonDisk.Sample(bounds, 5f, new SplitMix64(11));
            Assert.Greater(pts.Count, 50);
            for (int i = 0; i < pts.Count; i++)
            {
                Assert.IsTrue(bounds.Contains(pts[i]));
                for (int j = i + 1; j < pts.Count; j++)
                    Assert.GreaterOrEqual(Vector2.Distance(pts[i], pts[j]), 5f - 1e-3f);
            }
        }

        [Test]
        public void Poisson_IsDeterministic()
        {
            var a = PoissonDisk.Sample(Rect.MinMaxRect(0, 0, 50, 50), 4f, new SplitMix64(3));
            var b = PoissonDisk.Sample(Rect.MinMaxRect(0, 0, 50, 50), 4f, new SplitMix64(3));
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void Poisson_TooFineGrid_ReturnsEmpty()
        {
            Assert.AreEqual(0, PoissonDisk.Sample(Rect.MinMaxRect(0, 0, 100000, 100000), 1f, new SplitMix64(1)).Count);
        }

        [Test]
        public void Trees_AvoidBuildingsAndRoads()
        {
            var building = Building(1, 40, 40, 20, 20);
            var road = Road(2, 6f, 0f, new Vector2(0, 20), new Vector2(100, 20));
            var placer = new PropPlacer(new[] { building }, new[] { road });
            var trees = placer.PlaceTrees(new TreeFeature[0], new[] { Grass(3, Square(0, 0, 100, 100), 6f) }, 4000, 2, new GenerationReport());
            Assert.Greater(trees.Count, 20);
            foreach (var t in trees)
            {
                Assert.IsFalse(building.Spec.Footprint.Contains(t.Position));
                Assert.GreaterOrEqual(building.Spec.Footprint.DistanceToBoundary(t.Position), PropPlacer.TreeBuildingBuffer - 1e-3f);
                Assert.GreaterOrEqual(Mathf.Abs(t.Position.y - 20f), 3f + PropPlacer.TreeRoadClearance - 1e-3f);
                Assert.That(t.Scale, Is.InRange(0.8f, 1.2f));
                Assert.That(t.Variant, Is.InRange(0, 1));
            }
        }

        [Test]
        public void Trees_CapIsEnforced()
        {
            var placer = new PropPlacer(new BuildingFeature[0], new RoadFeature[0]);
            var report = new GenerationReport();
            var trees = placer.PlaceTrees(new TreeFeature[0], new[] { Grass(3, Square(0, 0, 100, 100), 6f) }, 5, 1, report);
            Assert.AreEqual(5, trees.Count);
            Assert.Greater(report.TreesDroppedByCap, 0);
            Assert.AreEqual(5, report.TreesPlaced);
        }

        [Test]
        public void OsmTreeInsideBuilding_IsDropped()
        {
            var placer = new PropPlacer(new[] { Building(1, 0, 0, 10, 10) }, new RoadFeature[0]);
            var trees = placer.PlaceTrees(new[] { new TreeFeature(7, new Vector2(5, 5)), new TreeFeature(8, new Vector2(30, 30)) },
                new AreaFeature[0], 4000, 1, new GenerationReport());
            Assert.AreEqual(1, trees.Count);
            Assert.AreEqual(new Vector2(30, 30), trees[0].Position);
        }

        [Test]
        public void Lamps_AlternateSidesAndFaceRoad()
        {
            var placer = new PropPlacer(new BuildingFeature[0], new RoadFeature[0]);
            var report = new GenerationReport();
            var lamps = placer.PlaceLamps(new[] { Road(1, 6f, 30f, new Vector2(0, 0), new Vector2(200, 0)) }, 8f, 0.6f, report);
            Assert.AreEqual(7, lamps.Count);
            Assert.AreEqual(7, report.LampsPlaced);
            Assert.AreEqual(15f, lamps[0].Position.x, 1e-3f);
            Assert.AreEqual(3.6f, lamps[0].Position.y, 1e-3f);
            Assert.AreEqual(180f, Mathf.Abs(lamps[0].YawDegrees), 1e-3f);
            Assert.AreEqual(-3.6f, lamps[1].Position.y, 1e-3f);
            Assert.AreEqual(0f, lamps[1].YawDegrees, 1e-3f);
        }

        [Test]
        public void Lamps_KeepMinimumSpacingAcrossRoads()
        {
            var placer = new PropPlacer(new BuildingFeature[0], new RoadFeature[0]);
            var lamps = placer.PlaceLamps(new[]
            {
                Road(1, 6f, 30f, new Vector2(0, 0), new Vector2(200, 0)),
                Road(2, 6f, 30f, new Vector2(0, 0), new Vector2(200, 0))
            }, 8f, 0.6f, new GenerationReport());
            Assert.AreEqual(7, lamps.Count);
        }

        [Test]
        public void Lamps_SkipRoadsWithoutSpacing()
        {
            var placer = new PropPlacer(new BuildingFeature[0], new RoadFeature[0]);
            Assert.AreEqual(0, placer.PlaceLamps(new[] { Road(1, 1.5f, 0f, new Vector2(0, 0), new Vector2(200, 0)) }, 8f, 0.6f, new GenerationReport()).Count);
        }
    }
}
```

- [ ] **Step 2: Jalankan, pastikan gagal**

Procedure R, lalu Procedure T dengan `Group = "PropPlacementTests"`. Expected: COMPILE FAILED.

- [ ] **Step 3: Implementasi**

`Assets/WorldGen/Editor/Geometry/SpatialGrid.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Uniform hash grid for rectangle-overlap queries.</summary>
    public sealed class SpatialGrid<T>
    {
        readonly float cellSize;
        readonly Dictionary<long, List<T>> cells = new Dictionary<long, List<T>>();

        public SpatialGrid(float cellSize) { this.cellSize = cellSize; }

        static long Key(int x, int y) => ((long)x << 32) ^ (uint)y;

        public void Insert(Rect area, T item)
        {
            int x0 = Mathf.FloorToInt(area.xMin / cellSize), x1 = Mathf.FloorToInt(area.xMax / cellSize);
            int y0 = Mathf.FloorToInt(area.yMin / cellSize), y1 = Mathf.FloorToInt(area.yMax / cellSize);
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                {
                    long key = Key(x, y);
                    if (!cells.TryGetValue(key, out var list))
                    {
                        list = new List<T>();
                        cells[key] = list;
                    }
                    list.Add(item);
                }
        }

        public void Query(Rect area, HashSet<T> results)
        {
            int x0 = Mathf.FloorToInt(area.xMin / cellSize), x1 = Mathf.FloorToInt(area.xMax / cellSize);
            int y0 = Mathf.FloorToInt(area.yMin / cellSize), y1 = Mathf.FloorToInt(area.yMax / cellSize);
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                    if (cells.TryGetValue(Key(x, y), out var list))
                        foreach (var item in list) results.Add(item);
        }
    }
}
```

`Assets/WorldGen/Editor/Geometry/PoissonDisk.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Bridson Poisson-disk sampling inside a rectangle, driven by SplitMix64.</summary>
    public static class PoissonDisk
    {
        public const int MaxGridCells = 2000000;

        public static List<Vector2> Sample(Rect bounds, float minDistance, SplitMix64 rng, int attempts = 20)
        {
            var points = new List<Vector2>();
            if (minDistance <= 0f || bounds.width <= 0f || bounds.height <= 0f) return points;
            float cell = minDistance / Mathf.Sqrt(2f);
            int gw = Mathf.CeilToInt(bounds.width / cell), gh = Mathf.CeilToInt(bounds.height / cell);
            if ((long)gw * gh > MaxGridCells) return points;

            var grid = new int[gw * gh];
            for (int i = 0; i < grid.Length; i++) grid[i] = -1;
            var active = new List<int>();
            float minSq = minDistance * minDistance;

            int CellX(Vector2 p) => Mathf.Clamp((int)((p.x - bounds.xMin) / cell), 0, gw - 1);
            int CellY(Vector2 p) => Mathf.Clamp((int)((p.y - bounds.yMin) / cell), 0, gh - 1);

            void Accept(Vector2 p)
            {
                grid[CellY(p) * gw + CellX(p)] = points.Count;
                active.Add(points.Count);
                points.Add(p);
            }

            bool IsFar(Vector2 c)
            {
                int cx = CellX(c), cy = CellY(c);
                for (int dy = -2; dy <= 2; dy++)
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        int x = cx + dx, y = cy + dy;
                        if (x < 0 || y < 0 || x >= gw || y >= gh) continue;
                        int j = grid[y * gw + x];
                        if (j >= 0 && (points[j] - c).sqrMagnitude < minSq) return false;
                    }
                return true;
            }

            Accept(new Vector2(rng.Range(bounds.xMin, bounds.xMax), rng.Range(bounds.yMin, bounds.yMax)));
            while (active.Count > 0)
            {
                int index = (int)(rng.NextULong() % (ulong)active.Count);
                Vector2 origin = points[active[index]];
                bool found = false;
                for (int k = 0; k < attempts; k++)
                {
                    float angle = rng.Range(0f, 2f * Mathf.PI);
                    float radius = rng.Range(minDistance, 2f * minDistance);
                    var candidate = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    if (!bounds.Contains(candidate) || !IsFar(candidate)) continue;
                    Accept(candidate);
                    found = true;
                    break;
                }
                if (!found) active.RemoveAt(index);
            }
            return points;
        }
    }
}
```

`Assets/WorldGen/Editor/Builders/PropPlacer.cs`:

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

    /// <summary>Deterministic placement of trees (OSM nodes + Poisson scatter) and street lamps.</summary>
    public sealed class PropPlacer
    {
        public const float MinTreeGap = 2f;
        public const float TreeBuildingBuffer = 1f;
        public const float TreeRoadClearance = 1f;
        public const float LampBuildingBuffer = 0.3f;

        readonly struct RoadSegment
        {
            public readonly Vector2 A, B;
            public readonly float HalfWidth;

            public RoadSegment(Vector2 a, Vector2 b, float halfWidth)
            {
                A = a;
                B = b;
                HalfWidth = halfWidth;
            }
        }

        readonly SpatialGrid<BuildingFeature> buildingGrid = new SpatialGrid<BuildingFeature>(20f);
        readonly SpatialGrid<int> roadGrid = new SpatialGrid<int>(20f);
        readonly List<RoadSegment> segments = new List<RoadSegment>();

        public PropPlacer(IReadOnlyList<BuildingFeature> buildings, IReadOnlyList<RoadFeature> roads)
        {
            foreach (var b in buildings) buildingGrid.Insert(b.Spec.Footprint.Bounds, b);
            foreach (var road in roads)
            {
                float hw = road.Width * 0.5f;
                foreach (var piece in road.Pieces)
                    for (int i = 0; i + 1 < piece.Points.Count; i++)
                    {
                        Vector2 a = piece.Points[i], b = piece.Points[i + 1];
                        var rect = Rect.MinMaxRect(Mathf.Min(a.x, b.x) - hw, Mathf.Min(a.y, b.y) - hw, Mathf.Max(a.x, b.x) + hw, Mathf.Max(a.y, b.y) + hw);
                        roadGrid.Insert(rect, segments.Count);
                        segments.Add(new RoadSegment(a, b, hw));
                    }
            }
        }

        static Rect Around(Vector2 p, float radius) => Rect.MinMaxRect(p.x - radius, p.y - radius, p.x + radius, p.y + radius);

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

        public List<PropPlacement> PlaceTrees(IReadOnlyList<TreeFeature> osmTrees, IReadOnlyList<AreaFeature> areas,
            int maxTrees, int variantCount, GenerationReport report)
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
                if (IsInsideBuilding(tree.Position, 0f) || !TryReserve(tree.Position)) continue;
                fromOsm.Add(Make(tree.Position, SplitMix64.For(tree.Id, "tree"), variantCount));
            }

            var scatter = new List<(ulong key, PropPlacement placement)>();
            foreach (var area in areas.OrderBy(a => a.Id))
            {
                float spacing = area.Rule != null ? area.Rule.treeSpacing : 0f;
                if (spacing <= 0f) continue;
                var rng = SplitMix64.For(area.Id, "trees");
                foreach (var p in PoissonDisk.Sample(area.Shape.Bounds, spacing, rng))
                {
                    if (!area.Shape.Contains(p) || IsInsideBuilding(p, TreeBuildingBuffer) || IsOnRoad(p, TreeRoadClearance)) continue;
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
                            Vector2 position = center + left * (side * (hw + offset));
                            Vector2 towardRoad = -left * side;
                            if (!IsInsideBuilding(position, LampBuildingBuffer) && !NearOther(position, result, grid, minSpacing))
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

- [ ] **Step 4: Jalankan, pastikan lulus**

Procedure R, lalu Procedure T dengan `Group = "PropPlacementTests"`. Expected: `passed=11 failed=0`.

- [ ] **Step 5: Commit**

Procedure C: `git add Assets/WorldGen`, subject `feat(worldgen): place trees and street lamps deterministically`.

---

### Task 12: Pipeline murni (fitur → mesh → props → hash chunk)

**Files:**
- Create: `Assets/WorldGen/Editor/Pipeline/WorldMeshPipeline.cs`
- Test: `Assets/WorldGen/Tests/WorldMeshPipelineTests.cs`

**Interfaces:**
- Consumes: semua builder (Task 7, 8), `FeatureClassifier` + fitur (Task 10), `PropPlacer` (Task 11), `WorldGenSettings` (Task 9), `OsmParser` (Task 4).
- Produces:
  - `static class FeatureGroups { const string Roads = "Roads", Buildings = "Buildings", Areas = "Areas", Water = "Water", KeyBuildings = "KeyBuildings" }`
  - `sealed class BuiltFeature { FeatureKind Kind; long OsmId; OsmElementType ElementType; string Name; string Group; Vector2Int Chunk; Vector2 Anchor; MeshData Mesh; Material[] Materials /* indeks = submesh MeshData */; bool AddCollider; KeyBuilding Key; Obb Obb; string Category; IReadOnlyDictionary<string,string> Tags }` — `Mesh == null` berarti gedung kunci yang diganti prefab.
  - `sealed class BuiltWorld { Rect Bounds; float ChunkSize; MeshData Ground; List<BuiltFeature> Features; List<PropPlacement> Trees; List<PropPlacement> Lamps }`
  - `static class WorldMeshPipeline { Vector2Int ChunkOf(Vector2 p, float chunkSize); string ChunkName(Vector2Int chunk); ClassifiedWorld Classify(string json, WorldGenSettings settings, GenerationReport report); BuiltWorld Build(ClassifiedWorld world, WorldGenSettings settings, GenerationReport report) }`
  - Report counts: `"Road"`, `"Building"`, `"KeyBuilding"`, `"KeyBuildingPrefab"`, `"Area"`, `"WaterArea"`, `"WaterLine"`; `report.ChunkHashes` terisi.

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/WorldGen/Tests/WorldMeshPipelineTests.cs`:

```csharp
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class WorldMeshPipelineTests
    {
        WorldGenSettings settings;
        readonly List<Object> created = new List<Object>();

        [SetUp]
        public void SetUp() => settings = DefaultSettingsFactory.CreateInMemory();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
            Object.DestroyImmediate(settings);
        }

        BuiltWorld Build(string fixture, GenerationReport report) =>
            WorldMeshPipeline.Build(WorldMeshPipeline.Classify(FixtureFiles.Read(fixture), settings, report), settings, report);

        Material[] Materials(int count)
        {
            var result = new Material[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = new Material(Shader.Find("Universal Render Pipeline/Simple Lit")) { name = "m" + i };
                created.Add(result[i]);
            }
            return result;
        }

        [TestCase(-0.01f, 0f, -1, 0)]
        [TestCase(100f, 199.99f, 1, 1)]
        [TestCase(-100f, -100f, -1, -1)]
        [TestCase(99.99f, 0f, 0, 0)]
        public void ChunkOf_FloorsCoordinates(float x, float y, int cx, int cy)
        {
            Assert.AreEqual(new Vector2Int(cx, cy), WorldMeshPipeline.ChunkOf(new Vector2(x, y), 100f));
        }

        [Test]
        public void ChunkName_IsInvariant()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("id-ID");
                Assert.AreEqual("Chunk_-3_5", WorldMeshPipeline.ChunkName(new Vector2Int(-3, 5)));
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Test]
        public void Synthetic_BuildsAllKinds()
        {
            var report = new GenerationReport();
            var built = Build("synthetic_world.json", report);
            Assert.IsTrue(built.Features.Any(f => f.Kind == FeatureKind.Road && f.OsmId == 402));
            Assert.IsTrue(built.Features.Any(f => f.Kind == FeatureKind.Building && f.OsmId == 401));
            Assert.IsTrue(built.Features.Any(f => f.Kind == FeatureKind.Building && f.OsmId == 201));
            Assert.IsTrue(built.Features.Any(f => f.Kind == FeatureKind.Area && f.OsmId == 404));
            Assert.IsTrue(built.Features.Any(f => f.Kind == FeatureKind.Area && f.OsmId == 405));
            Assert.IsFalse(built.Ground.IsEmpty);
            Assert.AreEqual(1, report.GetCount("Road"));
            Assert.Greater(report.ChunkHashes.Count, 0);
        }

        [Test]
        public void Synthetic_BuildingMaterialsFollowVariantRule()
        {
            var upper = Materials(3);
            var trim = Materials(3);
            var gable = Materials(1);
            settings.residentialMaterials = new BuildingMaterialSet
            {
                groundFloor = Materials(2), upper = upper, trim = trim, flatRoof = Materials(1), gableRoof = gable
            };
            var house = Build("synthetic_world.json", new GenerationReport()).Features.Single(f => f.OsmId == 401);
            int index = SplitMix64.Pick(401, "upper", 3);
            Assert.AreSame(upper[index], house.Materials[BuildingMeshBuilder.SubUpper]);
            Assert.AreSame(trim[index], house.Materials[BuildingMeshBuilder.SubTrim]);
            Assert.AreSame(gable[0], house.Materials[BuildingMeshBuilder.SubRoof]);
        }

        [Test]
        public void KeyBuildingWithPrefab_HasNoMesh()
        {
            var prefab = new GameObject("FakePrefab");
            created.Add(prefab);
            settings.keyBuildings.Add(new KeyBuilding(401, "Rumah Kunci") { prefab = prefab });
            var report = new GenerationReport();
            var feature = Build("synthetic_world.json", report).Features.Single(f => f.OsmId == 401);
            Assert.IsNull(feature.Mesh);
            Assert.AreEqual(FeatureGroups.KeyBuildings, feature.Group);
            Assert.AreEqual(1, report.GetCount("KeyBuildingPrefab"));
        }

        [Test]
        public void Synthetic_PropsStayInBounds()
        {
            var built = Build("synthetic_world.json", new GenerationReport());
            foreach (var t in built.Trees) Assert.IsTrue(built.Bounds.Contains(t.Position));
            foreach (var l in built.Lamps) Assert.IsTrue(built.Bounds.Contains(l.Position));
        }

        [Test]
        public void Sukabirus_IsDeterministic()
        {
            var first = new GenerationReport();
            var second = new GenerationReport();
            Build("sukabirus_small.json", first);
            Build("sukabirus_small.json", second);
            Assert.Greater(first.ChunkHashes.Count, 0);
            CollectionAssert.AreEqual(first.ChunkHashes, second.ChunkHashes);
        }

        [Test]
        public void Sukabirus_NoInvalidGeometry()
        {
            var report = new GenerationReport();
            var built = Build("sukabirus_small.json", report);
            Assert.Greater(report.GetCount("Building"), 0);
            Assert.Greater(report.GetCount("Road"), 0);
            foreach (var f in built.Features)
                if (f.Mesh != null) Assert.IsFalse(f.Mesh.HasInvalidValues(), $"{f.Kind} {f.OsmId}");
        }
    }
}
```

- [ ] **Step 2: Jalankan, pastikan gagal**

Procedure R, lalu Procedure T dengan `Group = "WorldMeshPipelineTests"`. Expected: COMPILE FAILED.

- [ ] **Step 3: Implementasi**

`Assets/WorldGen/Editor/Pipeline/WorldMeshPipeline.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    public static class FeatureGroups
    {
        public const string Roads = "Roads";
        public const string Buildings = "Buildings";
        public const string Areas = "Areas";
        public const string Water = "Water";
        public const string KeyBuildings = "KeyBuildings";
    }

    public sealed class BuiltFeature
    {
        public FeatureKind Kind;
        public long OsmId;
        public OsmElementType ElementType;
        public string Name;
        public string Group;
        public Vector2Int Chunk;
        public Vector2 Anchor;
        public MeshData Mesh;
        public Material[] Materials = new Material[0];
        public bool AddCollider;
        public KeyBuilding Key;
        public Obb Obb;
        public string Category = "";
        public IReadOnlyDictionary<string, string> Tags = OsmTags.Empty;
    }

    public sealed class BuiltWorld
    {
        public Rect Bounds;
        public float ChunkSize;
        public MeshData Ground;
        public readonly List<BuiltFeature> Features = new List<BuiltFeature>();
        public List<PropPlacement> Trees = new List<PropPlacement>();
        public List<PropPlacement> Lamps = new List<PropPlacement>();
    }

    /// <summary>Pure pipeline: classified features → meshes, materials, props, chunk hashes. Touches no scene or asset.</summary>
    public static class WorldMeshPipeline
    {
        public static Vector2Int ChunkOf(Vector2 p, float chunkSize) =>
            new Vector2Int(Mathf.FloorToInt(p.x / chunkSize), Mathf.FloorToInt(p.y / chunkSize));

        public static string ChunkName(Vector2Int chunk) =>
            "Chunk_" + chunk.x.ToString(CultureInfo.InvariantCulture) + "_" + chunk.y.ToString(CultureInfo.InvariantCulture);

        public static ClassifiedWorld Classify(string json, WorldGenSettings settings, GenerationReport report) =>
            new FeatureClassifier(settings, report).Classify(OsmParser.Parse(json));

        public static BuiltWorld Build(ClassifiedWorld world, WorldGenSettings settings, GenerationReport report)
        {
            var built = new BuiltWorld
            {
                Bounds = world.LocalBounds,
                ChunkSize = settings.chunkSize > 0f ? settings.chunkSize : 100f,
                Ground = GroundBuilder.Build(world.LocalBounds)
            };
            AddRoads(world, built, report);
            AddBuildings(world, settings, built, report);
            AddAreas(world, built, report);
            AddWater(world, settings, built, report);

            var placer = new PropPlacer(world.Buildings, world.Roads);
            int treeVariants = settings.treePrefabs != null ? settings.treePrefabs.Length : 0;
            built.Trees = placer.PlaceTrees(world.Trees, world.Areas, settings.maxTrees, treeVariants, report);
            built.Lamps = placer.PlaceLamps(world.Roads, settings.lampMinSpacing, settings.lampOffset, report);
            ComputeChunkHashes(built, report);
            return built;
        }

        static void AddRoads(ClassifiedWorld world, BuiltWorld built, GenerationReport report)
        {
            foreach (var road in world.Roads)
            {
                var mesh = new MeshData(1);
                foreach (var piece in road.Pieces)
                    RoadMeshBuilder.AddRibbon(mesh, 0, piece.Points, road.Width, road.Y, piece.ExtendStart, piece.ExtendEnd);
                if (mesh.IsEmpty) { report.Skip(SkipReasons.RoadEmptyMesh, road.Id); continue; }
                var anchor = MidPoint(road.Pieces);
                built.Features.Add(new BuiltFeature
                {
                    Kind = FeatureKind.Road, OsmId = road.Id, ElementType = OsmElementType.Way, Name = road.Name,
                    Group = FeatureGroups.Roads, Anchor = anchor, Chunk = ChunkOf(anchor, built.ChunkSize),
                    Mesh = mesh, Materials = new[] { road.Rule.material }, Category = road.Highway, Tags = road.Tags
                });
                report.Count("Road");
            }
        }

        static void AddBuildings(ClassifiedWorld world, WorldGenSettings settings, BuiltWorld built, GenerationReport report)
        {
            var meshParams = settings.MeshParams;
            foreach (var b in world.Buildings)
            {
                var feature = new BuiltFeature
                {
                    Kind = FeatureKind.Building, OsmId = b.Id, ElementType = b.ElementType, Name = b.Name,
                    Group = b.Key != null ? FeatureGroups.KeyBuildings : FeatureGroups.Buildings,
                    Anchor = b.Centroid, Chunk = ChunkOf(b.Centroid, built.ChunkSize), Key = b.Key,
                    AddCollider = true, Category = b.Rule.name, Tags = b.Tags
                };
                if (b.Key != null && b.Key.prefab != null)
                {
                    feature.Obb = ObbCalculator.Compute(b.Spec.Footprint.Outer);
                    feature.AddCollider = false;
                    built.Features.Add(feature);
                    report.Count("KeyBuildingPrefab");
                    continue;
                }

                feature.Mesh = BuildingMeshBuilder.Build(b.Spec, meshParams, out var info);
                feature.Obb = info.Obb;
                if (info.RoofFailed) report.Skip(SkipReasons.RoofTriangulationFailed, b.Id);

                var set = settings.MaterialSet(b.Rule.materialSet);
                int upper = SplitMix64.Pick(b.Id, "upper", Length(set.upper));
                var roofs = info.Roof == RoofKind.Gable ? set.gableRoof : set.flatRoof;
                feature.Materials = new Material[BuildingMeshBuilder.SubmeshCount];
                feature.Materials[BuildingMeshBuilder.SubGround] = At(set.groundFloor, SplitMix64.Pick(b.Id, "ground", Length(set.groundFloor)));
                feature.Materials[BuildingMeshBuilder.SubUpper] = At(set.upper, upper);
                feature.Materials[BuildingMeshBuilder.SubTrim] = At(set.trim, upper);
                feature.Materials[BuildingMeshBuilder.SubRoof] = At(roofs, SplitMix64.Pick(b.Id, "roof", Length(roofs)));
                built.Features.Add(feature);
                report.Count(b.Key != null ? "KeyBuilding" : "Building");
            }
        }

        static void AddAreas(ClassifiedWorld world, BuiltWorld built, GenerationReport report)
        {
            foreach (var area in world.Areas)
            {
                var mesh = new MeshData(1);
                if (!AreaMeshBuilder.AddFlatPolygon(mesh, 0, area.Shape, area.Rule.yOffset) || mesh.IsEmpty)
                {
                    report.Skip(SkipReasons.AreaTriangulationFailed, area.Id);
                    continue;
                }
                var anchor = Polygon2D.Centroid(area.Shape.Outer);
                built.Features.Add(new BuiltFeature
                {
                    Kind = FeatureKind.Area, OsmId = area.Id, ElementType = area.ElementType, Name = area.Name,
                    Group = FeatureGroups.Areas, Anchor = anchor, Chunk = ChunkOf(anchor, built.ChunkSize),
                    Mesh = mesh, Materials = new[] { area.Rule.material }, Category = area.Rule.category.ToString(), Tags = area.Tags
                });
                report.Count("Area");
            }
        }

        static void AddWater(ClassifiedWorld world, WorldGenSettings settings, BuiltWorld built, GenerationReport report)
        {
            foreach (var water in world.WaterAreas)
            {
                var mesh = new MeshData(1);
                if (!AreaMeshBuilder.AddFlatPolygon(mesh, 0, water.Shape, settings.waterAreaY) || mesh.IsEmpty)
                {
                    report.Skip(SkipReasons.AreaTriangulationFailed, water.Id);
                    continue;
                }
                var anchor = Polygon2D.Centroid(water.Shape.Outer);
                built.Features.Add(new BuiltFeature
                {
                    Kind = FeatureKind.WaterArea, OsmId = water.Id, ElementType = water.ElementType, Name = water.Name,
                    Group = FeatureGroups.Water, Anchor = anchor, Chunk = ChunkOf(anchor, built.ChunkSize),
                    Mesh = mesh, Materials = new[] { settings.waterMaterial }, Category = "Water", Tags = water.Tags
                });
                report.Count("WaterArea");
            }
            foreach (var line in world.WaterLines)
            {
                var mesh = new MeshData(1);
                foreach (var piece in line.Pieces) RoadMeshBuilder.AddRibbon(mesh, 0, piece.Points, line.Width, line.Y, false, false);
                if (mesh.IsEmpty) { report.Skip(SkipReasons.WaterEmptyMesh, line.Id); continue; }
                var anchor = MidPoint(line.Pieces);
                built.Features.Add(new BuiltFeature
                {
                    Kind = FeatureKind.WaterLine, OsmId = line.Id, ElementType = OsmElementType.Way, Name = line.Name,
                    Group = FeatureGroups.Water, Anchor = anchor, Chunk = ChunkOf(anchor, built.ChunkSize),
                    Mesh = mesh, Materials = new[] { settings.waterMaterial }, Category = "Water", Tags = line.Tags
                });
                report.Count("WaterLine");
            }
        }

        static int Length(Material[] list) => list != null ? list.Length : 0;

        static Material At(Material[] list, int index) =>
            list == null || list.Length == 0 || index < 0 ? null : list[index % list.Length];

        static Vector2 MidPoint(List<LinePiece> pieces)
        {
            var pts = pieces[0].Points;
            float total = 0f;
            for (int i = 0; i + 1 < pts.Count; i++) total += Vector2.Distance(pts[i], pts[i + 1]);
            float half = total * 0.5f, travelled = 0f;
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                float length = Vector2.Distance(pts[i], pts[i + 1]);
                if (length > 0f && travelled + length >= half) return Vector2.Lerp(pts[i], pts[i + 1], (half - travelled) / length);
                travelled += length;
            }
            return pts[0];
        }

        static void ComputeChunkHashes(BuiltWorld built, GenerationReport report)
        {
            var hashes = new SortedDictionary<string, ulong>(StringComparer.Ordinal);
            void Mix(Vector2Int chunk, ulong value)
            {
                string key = ChunkName(chunk);
                if (!hashes.TryGetValue(key, out ulong hash)) hash = 14695981039346656037UL;
                unchecked
                {
                    hash ^= value;
                    hash *= 1099511628211UL;
                }
                hashes[key] = hash;
            }

            foreach (var f in built.Features.OrderBy(f => f.Kind).ThenBy(f => f.OsmId))
                Mix(f.Chunk, f.Mesh != null ? f.Mesh.ContentHash() : unchecked((ulong)f.OsmId));
            foreach (var t in built.Trees) Mix(ChunkOf(t.Position, built.ChunkSize), Quantize(t));
            foreach (var l in built.Lamps) Mix(ChunkOf(l.Position, built.ChunkSize), Quantize(l));

            report.ChunkHashes.Clear();
            foreach (var kv in hashes) report.ChunkHashes[kv.Key] = kv.Value.ToString("x16", CultureInfo.InvariantCulture);
        }

        static ulong Quantize(PropPlacement p) => unchecked(
            ((ulong)(uint)Mathf.RoundToInt(p.Position.x * 100f) << 32) | (uint)Mathf.RoundToInt(p.Position.y * 100f));
    }
}
```

- [ ] **Step 4: Jalankan, pastikan lulus**

Procedure R, lalu Procedure T dengan `Group = "WorldMeshPipelineTests"`. Expected: `passed=11 failed=0` (4 kasus `ChunkOf` + 7 tes).

- [ ] **Step 5: Commit**

Procedure C: `git add Assets/WorldGen`, subject `feat(worldgen): add pure mesh pipeline with chunk hashes`.

---

### Task 13: Output scene & asset (runner Generate/Clear)

**Files:**
- Create: `Assets/WorldGen/Editor/Pipeline/WorldScenes.cs`
- Create: `Assets/WorldGen/Editor/Pipeline/ChunkAssetWriter.cs`
- Create: `Assets/WorldGen/Editor/Pipeline/SceneObjectFactory.cs`
- Create: `Assets/WorldGen/Editor/Pipeline/WorldGenerationRunner.cs`
- Create (oleh runner saat verifikasi): `Assets/Scenes/World.unity` (minimal: hanya `WorldSceneLoader`)
- Modify (oleh runner): `ProjectSettings/EditorBuildSettings.asset`

**Interfaces:**
- Consumes: `WorldMeshPipeline`, `BuiltWorld`, `BuiltFeature`, `FeatureGroups` (Task 12); `GroundBuilder` (Task 7); `ObbCalculator`, `Xz` (Task 5); `GenerationReport` (Task 10); `WorldGenSettings`, `AssetFolders` (Task 9); komponen runtime (Task 2); `WorldGenPaths` (Task 1).
- Produces:
  - `static class WorldScenes { bool CheckSaved(bool interactive, out string error); Scene EnsureWorldSceneOpen(); void CloseGeneratedScene(); void OpenGeneratedSceneAdditive(); void DeleteGeneratedFolder(); void EnsureBuildSettings(); void RemoveGeneratedFromBuildSettings() }`
  - `readonly struct MeshAsset { Mesh Mesh; int[] UsedSubmeshes }`
  - `static class ChunkAssetWriter { Dictionary<BuiltFeature, MeshAsset> Write(BuiltWorld world, string folder, out Mesh groundMesh) }`
  - `static class SceneObjectFactory { GameObject Populate(Scene scene, BuiltWorld world, IReadOnlyDictionary<BuiltFeature, MeshAsset> meshes, Mesh groundMesh, WorldGenSettings settings, GenerationReport report, Action<string, float> progress) }`
  - `static class WorldGenerationRunner { GenerationReport LastReport; bool Generate(WorldGenSettings settings, bool interactive, out string error); bool Clear(bool interactive, out string error) }` — `interactive = false` untuk panggilan MCP (tanpa dialog/progress bar; menolak jika ada scene dirty).

Task ini tidak punya tes EditMode (membuka/menutup scene di dalam Test Runner tidak aman). Verifikasinya adalah skrip V13 di Step 3 yang menjalankan Generate dua kali pada fixture dan memeriksa hasilnya.

- [ ] **Step 1: Implementasi**

`Assets/WorldGen/Editor/Pipeline/WorldScenes.cs`:

```csharp
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Scene bookkeeping for World.unity (manual, tracked) and World_Generated.unity (owned by the generator).</summary>
    public static class WorldScenes
    {
        public static bool CheckSaved(bool interactive, out string error)
        {
            error = null;
            if (interactive)
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return true;
                error = "Dibatalkan: ada scene yang belum disimpan.";
                return false;
            }
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isDirty || scene.path == WorldGenPaths.GeneratedScenePath) continue;
                error = "Scene belum disimpan: '" + (scene.path.Length > 0 ? scene.path : scene.name) + "'. Simpan dulu, lalu ulangi.";
                return false;
            }
            return true;
        }

        public static Scene EnsureWorldSceneOpen()
        {
            var world = SceneManager.GetSceneByPath(WorldGenPaths.WorldScenePath);
            if (!world.IsValid() || !world.isLoaded)
            {
                if (File.Exists(WorldGenPaths.ToAbsolute(WorldGenPaths.WorldScenePath)))
                {
                    world = EditorSceneManager.OpenScene(WorldGenPaths.WorldScenePath, OpenSceneMode.Single);
                }
                else
                {
                    AssetFolders.Ensure("Assets/Scenes");
                    world = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    new GameObject("WorldSceneLoader").AddComponent<WorldSceneLoader>();
                    EditorSceneManager.SaveScene(world, WorldGenPaths.WorldScenePath);
                }
            }
            SceneManager.SetActiveScene(world);
            return world;
        }

        public static void CloseGeneratedScene()
        {
            var scene = SceneManager.GetSceneByPath(WorldGenPaths.GeneratedScenePath);
            if (scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
        }

        public static void OpenGeneratedSceneAdditive()
        {
            if (!File.Exists(WorldGenPaths.ToAbsolute(WorldGenPaths.GeneratedScenePath))) return;
            var scene = SceneManager.GetSceneByPath(WorldGenPaths.GeneratedScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                EditorSceneManager.OpenScene(WorldGenPaths.GeneratedScenePath, OpenSceneMode.Additive);
        }

        /// <summary>Deletes Assets/WorldGen/Generated via System.IO (AssetDatabase.DeleteAsset is blocked under Unity MCP).</summary>
        public static void DeleteGeneratedFolder()
        {
            string abs = WorldGenPaths.ToAbsolute(WorldGenPaths.GeneratedFolder);
            if (!abs.Replace('\\', '/').EndsWith("/Assets/WorldGen/Generated", StringComparison.Ordinal))
                throw new InvalidOperationException("Menolak menghapus path tak terduga: " + abs);
            if (Directory.Exists(abs)) Directory.Delete(abs, true);
            if (File.Exists(abs + ".meta")) File.Delete(abs + ".meta");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        public static void EnsureBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == WorldGenPaths.WorldScenePath))
                scenes.Insert(0, new EditorBuildSettingsScene(WorldGenPaths.WorldScenePath, true));
            if (!scenes.Any(s => s.path == WorldGenPaths.GeneratedScenePath))
            {
                int worldIndex = scenes.FindIndex(s => s.path == WorldGenPaths.WorldScenePath);
                scenes.Insert(worldIndex + 1, new EditorBuildSettingsScene(WorldGenPaths.GeneratedScenePath, true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        public static void RemoveGeneratedFromBuildSettings()
        {
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Where(s => s.path != WorldGenPaths.GeneratedScenePath).ToArray();
        }
    }
}
```

`Assets/WorldGen/Editor/Pipeline/ChunkAssetWriter.cs`:

```csharp
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    public readonly struct MeshAsset
    {
        public readonly Mesh Mesh;
        public readonly int[] UsedSubmeshes;

        public MeshAsset(Mesh mesh, int[] usedSubmeshes)
        {
            Mesh = mesh;
            UsedSubmeshes = usedSubmeshes;
        }
    }

    /// <summary>Writes one .asset per 100 m chunk containing every feature mesh of that chunk as sub-assets.</summary>
    public static class ChunkAssetWriter
    {
        public static Dictionary<BuiltFeature, MeshAsset> Write(BuiltWorld world, string folder, out Mesh groundMesh)
        {
            AssetFolders.Ensure(folder);
            var result = new Dictionary<BuiltFeature, MeshAsset>();
            var groups = world.Features
                .Where(f => f.Mesh != null && !f.Mesh.IsEmpty)
                .GroupBy(f => f.Chunk)
                .OrderBy(g => g.Key.x).ThenBy(g => g.Key.y);

            AssetDatabase.StartAssetEditing();
            try
            {
                groundMesh = world.Ground.ToMesh("Ground", out _);
                AssetDatabase.CreateAsset(groundMesh, folder + "/Ground.asset");
                foreach (var group in groups)
                {
                    string path = folder + "/" + WorldMeshPipeline.ChunkName(group.Key) + ".asset";
                    Mesh main = null;
                    foreach (var feature in group.OrderBy(f => f.Kind).ThenBy(f => f.OsmId))
                    {
                        var mesh = feature.Mesh.ToMesh(feature.Group + "_" + feature.OsmId.ToString(CultureInfo.InvariantCulture), out var used);
                        if (main == null)
                        {
                            AssetDatabase.CreateAsset(mesh, path);
                            main = mesh;
                        }
                        else
                        {
                            AssetDatabase.AddObjectToAsset(mesh, main);
                        }
                        result[feature] = new MeshAsset(mesh, used);
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            AssetDatabase.SaveAssets();
            return result;
        }
    }
}
```

`Assets/WorldGen/Editor/Pipeline/SceneObjectFactory.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Instantiates the generated hierarchy (spec section 11) inside the generated scene.</summary>
    public static class SceneObjectFactory
    {
        const StaticEditorFlags Flags = StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;

        public static GameObject Populate(Scene scene, BuiltWorld world, IReadOnlyDictionary<BuiltFeature, MeshAsset> meshes,
            Mesh groundMesh, WorldGenSettings settings, GenerationReport report, Action<string, float> progress)
        {
            var root = new GameObject(WorldGenPaths.GeneratedSceneName);
            SceneManager.MoveGameObjectToScene(root, scene);
            var containers = new Dictionary<string, Transform>();

            Transform Container(string path)
            {
                if (containers.TryGetValue(path, out var existing)) return existing;
                int slash = path.LastIndexOf('/');
                Transform parent = slash < 0 ? root.transform : Container(path.Substring(0, slash));
                var go = new GameObject(slash < 0 ? path : path.Substring(slash + 1));
                go.transform.SetParent(parent, false);
                containers[path] = go.transform;
                return go.transform;
            }

            var ground = new GameObject("Ground");
            ground.transform.SetParent(root.transform, false);
            ground.AddComponent<MeshFilter>().sharedMesh = groundMesh;
            ground.AddComponent<MeshRenderer>().sharedMaterial = Resolve(settings.groundMaterial, settings, report, "Ground");
            GroundBuilder.ColliderBox(world.Bounds, out var center, out var size);
            var box = ground.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
            GameObjectUtility.SetStaticEditorFlags(ground, Flags);

            for (int i = 0; i < world.Features.Count; i++)
            {
                if (i % 250 == 0) progress?.Invoke("Membuat objek " + i + "/" + world.Features.Count, (float)i / world.Features.Count);
                var feature = world.Features[i];
                GameObject go;
                if (feature.Mesh == null)
                {
                    if (feature.Key == null || feature.Key.prefab == null) continue;
                    go = (GameObject)PrefabUtility.InstantiatePrefab(feature.Key.prefab, scene);
                    go.name = feature.Key.label;
                    go.transform.SetParent(Container(FeatureGroups.KeyBuildings), false);
                    go.transform.SetPositionAndRotation(Xz.At(feature.Anchor, 0f),
                        Quaternion.Euler(0f, ObbCalculator.YawDegreesForLocalX(feature.Obb.AxisU), 0f));
                }
                else
                {
                    if (!meshes.TryGetValue(feature, out var asset)) continue;
                    go = new GameObject(ObjectName(feature));
                    string path = feature.Group == FeatureGroups.KeyBuildings
                        ? FeatureGroups.KeyBuildings
                        : feature.Group + "/" + WorldMeshPipeline.ChunkName(feature.Chunk);
                    go.transform.SetParent(Container(path), false);
                    go.AddComponent<MeshFilter>().sharedMesh = asset.Mesh;
                    var materials = new Material[asset.UsedSubmeshes.Length];
                    for (int k = 0; k < materials.Length; k++)
                    {
                        int sub = asset.UsedSubmeshes[k];
                        var material = sub < feature.Materials.Length ? feature.Materials[sub] : null;
                        materials[k] = Resolve(material, settings, report, feature.Group + "/" + feature.Category);
                    }
                    go.AddComponent<MeshRenderer>().sharedMaterials = materials;
                    if (feature.AddCollider) go.AddComponent<MeshCollider>().sharedMesh = asset.Mesh;
                }

                var osm = go.GetComponent<OsmFeature>();
                if (osm == null) osm = go.AddComponent<OsmFeature>();
                osm.Initialize(feature.OsmId, feature.ElementType, feature.Kind, feature.Name ?? "", feature.Tags);
                if (feature.Key != null)
                {
                    var marker = go.GetComponent<KeyBuildingMarker>();
                    if (marker == null) marker = go.AddComponent<KeyBuildingMarker>();
                    marker.Initialize(feature.OsmId, feature.Key.label);
                }
                GameObjectUtility.SetStaticEditorFlags(go, Flags);
            }

            PlaceProps(scene, world.Trees, settings.treePrefabs, "Props/Trees", world.ChunkSize, Container, report, "pohon");
            var lampPrefabs = settings.lampPrefab != null ? new[] { settings.lampPrefab } : new GameObject[0];
            PlaceProps(scene, world.Lamps, lampPrefabs, "Props/Lamps", world.ChunkSize, Container, report, "lampu");
            return root;
        }

        static string ObjectName(BuiltFeature f)
        {
            if (f.Key != null && !string.IsNullOrEmpty(f.Key.label)) return f.Key.label;
            if (!string.IsNullOrEmpty(f.Name)) return f.Name;
            string id = f.OsmId.ToString(CultureInfo.InvariantCulture);
            switch (f.Kind)
            {
                case FeatureKind.Road: return "Road_" + id;
                case FeatureKind.Building: return "Building_" + id;
                case FeatureKind.Area: return "Area_" + f.Category + "_" + id;
                default: return "Water_" + id;
            }
        }

        static void PlaceProps(Scene scene, List<PropPlacement> placements, GameObject[] prefabs, string group, float chunkSize,
            Func<string, Transform> container, GenerationReport report, string label)
        {
            if (placements.Count == 0) return;
            if (prefabs == null || prefabs.Length == 0 || prefabs.All(p => p == null))
            {
                report.Warn("Prefab " + label + " belum diisi di Settings; " + label + " dilewati.");
                return;
            }
            var fallback = prefabs.First(p => p != null);
            foreach (var placement in placements)
            {
                var prefab = prefabs[placement.Variant % prefabs.Length];
                if (prefab == null) prefab = fallback;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                var chunk = WorldMeshPipeline.ChunkOf(placement.Position, chunkSize);
                go.transform.SetParent(container(group + "/" + WorldMeshPipeline.ChunkName(chunk)), false);
                go.transform.SetPositionAndRotation(Xz.At(placement.Position, 0f), Quaternion.Euler(0f, placement.YawDegrees, 0f));
                go.transform.localScale = prefab.transform.localScale * placement.Scale;
                GameObjectUtility.SetStaticEditorFlags(go, Flags);
            }
        }

        static Material Resolve(Material material, WorldGenSettings settings, GenerationReport report, string context)
        {
            if (material != null) return material;
            report.Warn("Material kosong untuk " + context + "; memakai fallback.");
            if (settings.fallbackMaterial != null) return settings.fallbackMaterial;
            var pipeline = GraphicsSettings.currentRenderPipeline;
            return pipeline != null ? pipeline.defaultMaterial : null;
        }
    }
}
```

`Assets/WorldGen/Editor/Pipeline/WorldGenerationRunner.cs`:

```csharp
using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Generate / Clear entry points used by the window and by Unity MCP (interactive = false).</summary>
    public static class WorldGenerationRunner
    {
        public static GenerationReport LastReport { get; private set; }

        public static bool Generate(WorldGenSettings settings, bool interactive, out string error)
        {
            error = null;
            if (settings == null) { error = "Settings kosong."; return false; }
            string raw = WorldGenPaths.ToAbsolute(settings.rawDataPath);
            if (!File.Exists(raw)) { error = "Download data dulu (tidak ada " + settings.rawDataPath + ")."; return false; }
            if (!WorldScenes.CheckSaved(interactive, out error)) return false;

            var report = new GenerationReport();
            var total = System.Diagnostics.Stopwatch.StartNew();
            var stage = System.Diagnostics.Stopwatch.StartNew();
            Scene generated = default;

            void Progress(string label, float t)
            {
                if (interactive && EditorUtility.DisplayCancelableProgressBar("World Generator", label, t))
                    throw new OperationCanceledException();
            }

            void EndStage(string name)
            {
                report.Stage(name, stage.Elapsed.TotalMilliseconds);
                stage.Restart();
            }

            try
            {
                var world = WorldScenes.EnsureWorldSceneOpen();
                WorldScenes.CloseGeneratedScene();
                WorldScenes.DeleteGeneratedFolder();
                EndStage("prepare");

                Progress("Parsing & klasifikasi OSM", 0.05f);
                var classified = WorldMeshPipeline.Classify(File.ReadAllText(raw), settings, report);
                EndStage("classify");

                Progress("Membuat mesh", 0.25f);
                var built = WorldMeshPipeline.Build(classified, settings, report);
                EndStage("meshes");

                Progress("Menulis asset mesh", 0.45f);
                var meshes = ChunkAssetWriter.Write(built, WorldGenPaths.GeneratedMeshesFolder, out var groundMesh);
                EndStage("assets");

                generated = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(generated);
                var root = SceneObjectFactory.Populate(generated, built, meshes, groundMesh, settings, report,
                    (label, t) => Progress(label, 0.6f + 0.35f * t));
                EndStage("scene");

                total.Stop();
                report.TotalMillis = total.Elapsed.TotalMilliseconds;
                root.AddComponent<GeneratedRoot>().Initialize(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture), report.Summary());
                EditorSceneManager.SaveScene(generated, WorldGenPaths.GeneratedScenePath);
                SceneManager.SetActiveScene(world);
                WorldScenes.EnsureBuildSettings();
                AssetDatabase.SaveAssets();
                LastReport = report;
                Debug.Log("[WorldGen] Generate selesai.\n" + report.Summary());
                return true;
            }
            catch (Exception ex)
            {
                if (generated.IsValid()) EditorSceneManager.CloseScene(generated, true);
                WorldScenes.DeleteGeneratedFolder();
                bool cancelled = ex is OperationCanceledException;
                error = cancelled ? "Generate dibatalkan." : "Generate gagal: " + ex.Message;
                if (!cancelled) Debug.LogException(ex);
                return false;
            }
            finally
            {
                if (interactive) EditorUtility.ClearProgressBar();
            }
        }

        public static bool Clear(bool interactive, out string error)
        {
            error = null;
            if (interactive && !EditorUtility.DisplayDialog("Clear World",
                    "Hapus semua hasil generate di Assets/WorldGen/Generated?", "Hapus", "Batal"))
            {
                error = "Dibatalkan.";
                return false;
            }
            if (!WorldScenes.CheckSaved(interactive, out error)) return false;
            WorldScenes.EnsureWorldSceneOpen();
            WorldScenes.CloseGeneratedScene();
            WorldScenes.DeleteGeneratedFolder();
            WorldScenes.RemoveGeneratedFromBuildSettings();
            LastReport = null;
            return true;
        }
    }
}
```

- [ ] **Step 2: Compile + regresi**

Procedure R, lalu Procedure T dengan `Group = "."`. Expected: `failed=0` (semua tes Task 1–12 tetap lulus; tidak ada tes baru).

- [ ] **Step 3: Verifikasi V13 — Generate dua kali pada fixture**

Pastikan tidak ada scene dirty (simpan dulu jika ada). Lalu `mcp__unity-mcp__Unity_RunCommand`:

```csharp
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using TycoWorld.WorldGen;
using TycoWorld.WorldGen.Authoring;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var s = DefaultSettingsFactory.CreateInMemory();
        s.rawDataPath = WorldGenPaths.FixturesFolder + "/sukabirus_small.json";
        string worldAbs = WorldGenPaths.ToAbsolute(WorldGenPaths.WorldScenePath);

        if (!WorldGenerationRunner.Generate(s, false, out string err)) { result.LogError("Generate #1 gagal: " + err); return; }
        var first = WorldGenerationRunner.LastReport.ChunkHashes.ToDictionary(kv => kv.Key, kv => kv.Value);
        string worldBefore = File.ReadAllText(worldAbs);

        if (!WorldGenerationRunner.Generate(s, false, out err)) { result.LogError("Generate #2 gagal: " + err); return; }
        var report = WorldGenerationRunner.LastReport;
        var second = report.ChunkHashes;
        bool identical = first.Count == second.Count && first.All(kv => second.TryGetValue(kv.Key, out var v) && v == kv.Value);
        bool worldUnchanged = worldBefore == File.ReadAllText(worldAbs);

        var meshFiles = Directory.GetFiles(WorldGenPaths.ToAbsolute(WorldGenPaths.GeneratedMeshesFolder), "Chunk_*.asset")
            .Select(Path.GetFileNameWithoutExtension).ToArray();
        bool noOrphans = meshFiles.All(name => second.ContainsKey(name));

        var root = GameObject.Find(WorldGenPaths.GeneratedSceneName);
        int features = Object.FindObjectsByType<OsmFeature>(FindObjectsSortMode.None).Length;
        int buildIndex = UnityEngine.SceneManagement.SceneUtility.GetBuildIndexByScenePath(WorldGenPaths.GeneratedScenePath);

        result.Log("chunks={0} identical={1} worldUnchanged={2} meshFiles={3} noOrphans={4}", second.Count, identical, worldUnchanged, meshFiles.Length, noOrphans);
        result.Log("root={0} osmFeatures={1} generatedBuildIndex={2}", root != null, features, buildIndex);
        result.Log(report.Summary());
        if (!identical || !worldUnchanged || !noOrphans || root == null || features == 0 || buildIndex < 0) result.LogError("V13 GAGAL");
        Object.DestroyImmediate(s);
    }
}
```

Expected: `identical=True worldUnchanged=True noOrphans=True`, `root=True`, `osmFeatures` > 0, `generatedBuildIndex` ≥ 1, tanpa `V13 GAGAL`. Lalu `mcp__unity-mcp__Unity_GetConsoleLogs` (`logTypes: "error"`) → tidak ada error baru (warning "Material kosong ... fallback" boleh, karena settings in-memory belum punya material).

- [ ] **Step 4: Verifikasi Clear**

`mcp__unity-mcp__Unity_RunCommand`:

```csharp
using System.IO;
using UnityEditor;
using TycoWorld.WorldGen.Authoring;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        bool ok = WorldGenerationRunner.Clear(false, out string err);
        bool folderGone = !Directory.Exists(WorldGenPaths.ToAbsolute(WorldGenPaths.GeneratedFolder));
        int buildIndex = UnityEngine.SceneManagement.SceneUtility.GetBuildIndexByScenePath(WorldGenPaths.GeneratedScenePath);
        result.Log("clear={0} err={1} folderGone={2} generatedBuildIndex={3}", ok, err, folderGone, buildIndex);
        if (!ok || !folderGone || buildIndex >= 0) result.LogError("Clear GAGAL");
    }
}
```

Expected: `clear=True folderGone=True generatedBuildIndex=-1`.

- [ ] **Step 5: Commit**

Procedure C: `git add Assets/WorldGen Assets/Scenes/World.unity Assets/Scenes/World.unity.meta ProjectSettings/EditorBuildSettings.asset`, subject `feat(worldgen): write chunk assets and generated scene`.

---

### Task 14: Download Overpass + editor window

**Files:**
- Create: `Assets/WorldGen/Editor/Data/CacheWriter.cs`
- Create: `Assets/WorldGen/Editor/Data/OsmFetcher.cs`
- Create: `Assets/WorldGen/Editor/UI/WorldGenWindow.cs`
- Test: `Assets/WorldGen/Tests/FetcherTests.cs`

**Interfaces:**
- Consumes: `OverpassQuery`, `OverpassResponseValidator`, `OsmParser.Load` (Task 4); `GeoBounds` (Task 3); `WorldGenSettings`, `DefaultSettingsFactory` (Task 9); `WorldGenerationRunner` (Task 13).
- Produces:
  - `static class CacheWriter { void WriteAtomic(string path, string content); string InfoPath(string rawPath); void WriteInfo(string rawPath, string endpoint, GeoBounds bounds, int elementCount, DateTime utcNow); string ReadInfoSummary(string rawPath) }`
  - `sealed class OsmFetcher { const string UserAgent; static OsmFetcher Active; string Status; bool IsDone; bool Succeeded; string Error; static OsmFetcher Start(WorldGenSettings settings, Action<OsmFetcher> onDone = null); static UnityWebRequest CreateRequest(string endpoint, string query, int timeoutSeconds); void Cancel() }`
  - `WorldGenWindow : EditorWindow` — menu `Tyco World/World Generator`.

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/WorldGen/Tests/FetcherTests.cs`:

```csharp
using System;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;

namespace TycoWorld.WorldGen.Tests
{
    public class FetcherTests
    {
        string dir;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "tyco_wg_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        [Test]
        public void CreateRequest_SetsUserAgentAndPost()
        {
            using (var request = OsmFetcher.CreateRequest("https://example.invalid/api/interpreter", "[out:json];node(1);out;", 90))
            {
                Assert.AreEqual("POST", request.method);
                Assert.AreEqual(OsmFetcher.UserAgent, request.GetRequestHeader("User-Agent"));
                Assert.AreEqual(90, request.timeout);
                Assert.AreEqual("https://example.invalid/api/interpreter", request.url);
                string body = Encoding.UTF8.GetString(request.uploadHandler.data);
                StringAssert.StartsWith("data=", body);
                Assert.AreEqual("[out:json];node(1);out;", Uri.UnescapeDataString(body.Substring(5).Replace('+', ' ')));
            }
        }

        [Test]
        public void WriteAtomic_ReplacesContentAndLeavesNoTemp()
        {
            string path = Path.Combine(dir, "sub", "osm_raw.json");
            CacheWriter.WriteAtomic(path, "first");
            CacheWriter.WriteAtomic(path, "second");
            Assert.AreEqual("second", File.ReadAllText(path));
            Assert.IsFalse(File.Exists(path + ".tmp"));
        }

        [Test]
        public void InfoPath_SitsNextToRawFile()
        {
            Assert.AreEqual(Path.Combine(dir, "osm_raw.info.json"), CacheWriter.InfoPath(Path.Combine(dir, "osm_raw.json")));
        }

        [Test]
        public void WriteInfo_IsCultureInvariant()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("id-ID");
                string raw = Path.Combine(dir, "osm_raw.json");
                File.WriteAllText(raw, "{}");
                CacheWriter.WriteInfo(raw, "https://overpass-api.de/api/interpreter",
                    new GeoBounds(-6.9850, 107.6255, -6.9675, 107.6380), 5, new DateTime(2026, 10, 13, 1, 2, 3, DateTimeKind.Utc));
                string info = File.ReadAllText(CacheWriter.InfoPath(raw));
                StringAssert.Contains("-6.985", info);
                StringAssert.DoesNotContain("-6,985", info);
                StringAssert.Contains("5 elemen", CacheWriter.ReadInfoSummary(raw));
                StringAssert.Contains("2026-10-13T01:02:03", CacheWriter.ReadInfoSummary(raw));
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Test]
        public void ReadInfoSummary_NoData()
        {
            StringAssert.StartsWith("Belum ada data", CacheWriter.ReadInfoSummary(Path.Combine(dir, "missing.json")));
        }
    }
}
```

- [ ] **Step 2: Jalankan, pastikan gagal**

Procedure R, lalu Procedure T dengan `Group = "FetcherTests"`. Expected: COMPILE FAILED.

- [ ] **Step 3: Implementasi**

`Assets/WorldGen/Editor/Data/CacheWriter.cs`:

```csharp
using System;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Atomic cache writes for WorldGenData/osm_raw.json and its info file.</summary>
    public static class CacheWriter
    {
        public static void WriteAtomic(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".tmp";
            File.WriteAllText(temp, content, new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }

        public static string InfoPath(string rawPath) =>
            Path.Combine(Path.GetDirectoryName(rawPath), Path.GetFileNameWithoutExtension(rawPath) + ".info.json");

        public static void WriteInfo(string rawPath, string endpoint, GeoBounds bounds, int elementCount, DateTime utcNow)
        {
            var info = new JObject
            {
                ["downloadedAtUtc"] = utcNow.ToString("o", CultureInfo.InvariantCulture),
                ["endpoint"] = endpoint,
                ["south"] = bounds.South,
                ["west"] = bounds.West,
                ["north"] = bounds.North,
                ["east"] = bounds.East,
                ["elementCount"] = elementCount
            };
            WriteAtomic(InfoPath(rawPath), info.ToString(Formatting.Indented));
        }

        public static string ReadInfoSummary(string rawPath)
        {
            string infoPath = InfoPath(rawPath);
            if (!File.Exists(rawPath) || !File.Exists(infoPath)) return "Belum ada data. Klik Download Data.";
            try
            {
                var info = OsmParser.Load(File.ReadAllText(infoPath));
                return string.Format(CultureInfo.InvariantCulture, "{0} elemen, diunduh {1} dari {2}",
                    (int)info["elementCount"], (string)info["downloadedAtUtc"], (string)info["endpoint"]);
            }
            catch (Exception ex)
            {
                return "Info cache rusak: " + ex.Message;
            }
        }
    }
}
```

`Assets/WorldGen/Editor/Data/OsmFetcher.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Non-blocking Overpass download driven by EditorApplication.update; tries endpoints in order.</summary>
    public sealed class OsmFetcher
    {
        public const string UserAgent = "TycoWorld-WorldGen/1.0 (Unity editor; personal project)";

        public static OsmFetcher Active { get; private set; }

        public string Status { get; private set; } = "";
        public bool IsDone { get; private set; }
        public bool Succeeded { get; private set; }
        public string Error { get; private set; }

        readonly string query;
        readonly string[] endpoints;
        readonly int timeoutSeconds;
        readonly string rawPath;
        readonly GeoBounds bounds;
        readonly Action<OsmFetcher> onDone;
        readonly List<string> failures = new List<string>();
        int endpointIndex = -1;
        UnityWebRequest request;

        OsmFetcher(WorldGenSettings settings, Action<OsmFetcher> onDone)
        {
            bounds = settings.Bounds;
            query = OverpassQuery.Build(bounds, settings.timeoutSeconds);
            endpoints = settings.overpassEndpoints ?? new string[0];
            timeoutSeconds = settings.timeoutSeconds;
            rawPath = WorldGenPaths.ToAbsolute(settings.rawDataPath);
            this.onDone = onDone;
        }

        public static OsmFetcher Start(WorldGenSettings settings, Action<OsmFetcher> onDone = null)
        {
            if (Active != null && !Active.IsDone) throw new InvalidOperationException("Download sedang berjalan.");
            var fetcher = new OsmFetcher(settings, onDone);
            Active = fetcher;
            EditorApplication.update += fetcher.Tick;
            fetcher.Next();
            return fetcher;
        }

        public static UnityWebRequest CreateRequest(string endpoint, string query, int timeoutSeconds)
        {
            var form = new WWWForm();
            form.AddField("data", query);
            var request = UnityWebRequest.Post(endpoint, form);
            request.timeout = timeoutSeconds;
            request.SetRequestHeader("User-Agent", UserAgent);
            request.SetRequestHeader("Accept", "application/json");
            return request;
        }

        void Next()
        {
            DisposeRequest();
            endpointIndex++;
            if (endpointIndex >= endpoints.Length)
            {
                Finish(false, "Semua endpoint gagal:\n" + string.Join("\n", failures));
                return;
            }
            Status = "Download dari " + endpoints[endpointIndex] + " ...";
            request = CreateRequest(endpoints[endpointIndex], query, timeoutSeconds);
            request.SendWebRequest();
        }

        void Tick()
        {
            if (IsDone || request == null || !request.isDone) return;
            string endpoint = endpoints[endpointIndex];
            if (request.result != UnityWebRequest.Result.Success)
            {
                failures.Add(endpoint + ": " + request.error);
                Next();
                return;
            }
            string body = request.downloadHandler.text;
            if (!OverpassResponseValidator.Validate(body, out string error))
            {
                failures.Add(endpoint + ": " + error);
                Next();
                return;
            }
            try
            {
                CacheWriter.WriteAtomic(rawPath, body);
                CacheWriter.WriteInfo(rawPath, endpoint, bounds, OverpassResponseValidator.CountElements(body), DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                Finish(false, "Gagal menyimpan cache: " + ex.Message);
                return;
            }
            Finish(true, null);
        }

        public void Cancel()
        {
            if (IsDone) return;
            if (request != null) request.Abort();
            Finish(false, "Dibatalkan.");
        }

        void Finish(bool ok, string error)
        {
            IsDone = true;
            Succeeded = ok;
            Error = error;
            Status = ok ? "Selesai." : "Gagal: " + error;
            EditorApplication.update -= Tick;
            DisposeRequest();
            if (ok) Debug.Log("[WorldGen] Download selesai: " + rawPath);
            else Debug.LogWarning("[WorldGen] Download gagal: " + error);
            onDone?.Invoke(this);
        }

        void DisposeRequest()
        {
            if (request == null) return;
            request.Dispose();
            request = null;
        }
    }
}
```

`Assets/WorldGen/Editor/UI/WorldGenWindow.cs`:

```csharp
using UnityEditor;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    public sealed class WorldGenWindow : EditorWindow
    {
        WorldGenSettings settings;
        Vector2 scroll;

        [MenuItem("Tyco World/World Generator")]
        public static void Open() => GetWindow<WorldGenWindow>("World Generator");

        void OnEnable()
        {
            if (settings == null) settings = DefaultSettingsFactory.LoadOrCreateAsset();
        }

        void OnInspectorUpdate()
        {
            if (OsmFetcher.Active != null && !OsmFetcher.Active.IsDone) Repaint();
        }

        void OnGUI()
        {
            settings = (WorldGenSettings)EditorGUILayout.ObjectField("Settings", settings, typeof(WorldGenSettings), false);
            if (settings == null)
            {
                EditorGUILayout.HelpBox("Belum ada WorldGenSettings.", MessageType.Warning);
                if (GUILayout.Button("Buat Settings")) settings = DefaultSettingsFactory.LoadOrCreateAsset();
                return;
            }

            EditorGUILayout.LabelField("Data OSM", CacheWriter.ReadInfoSummary(WorldGenPaths.ToAbsolute(settings.rawDataPath)), EditorStyles.wordWrappedLabel);

            var fetcher = OsmFetcher.Active;
            bool downloading = fetcher != null && !fetcher.IsDone;
            using (new EditorGUI.DisabledScope(downloading))
            {
                if (GUILayout.Button("Download Data"))
                    OsmFetcher.Start(settings, done =>
                    {
                        if (!done.Succeeded) EditorUtility.DisplayDialog("Download gagal", done.Error, "OK");
                        Repaint();
                    });
            }
            if (downloading)
            {
                EditorGUILayout.HelpBox(fetcher.Status, MessageType.Info);
                if (GUILayout.Button("Batalkan Download")) fetcher.Cancel();
            }

            using (new EditorGUI.DisabledScope(downloading))
            {
                if (GUILayout.Button("Generate") && !WorldGenerationRunner.Generate(settings, true, out string error))
                    EditorUtility.DisplayDialog("Generate gagal", error, "OK");
                if (GUILayout.Button("Clear")) WorldGenerationRunner.Clear(true, out _);
            }

            var report = WorldGenerationRunner.LastReport;
            if (report != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Laporan terakhir", EditorStyles.boldLabel);
                scroll = EditorGUILayout.BeginScrollView(scroll);
                EditorGUILayout.LabelField(report.Summary(), EditorStyles.wordWrappedLabel);
                EditorGUILayout.EndScrollView();
            }
        }
    }
}
```

- [ ] **Step 4: Jalankan, pastikan lulus**

Procedure R, lalu Procedure T dengan `Group = "FetcherTests"`. Expected: `passed=5 failed=0`.

- [ ] **Step 5: Commit**

Procedure C: `git add Assets/WorldGen`, subject `feat(worldgen): add Overpass downloader and generator window`.

---

### Task 15: Aset art (tekstur CC0, fasad prosedural, material, prefab placeholder)

**Files:**
- Create (download): `Assets/WorldGen/Textures/CC0/<AssetId>/<AssetId>_1K-JPG_Color.jpg` dan `..._NormalGL.jpg` untuk 10 aset
- Create: `Assets/WorldGen/Textures/SOURCES.md`
- Create: `Assets/WorldGen/Editor/Art/FacadeTextureGenerator.cs`
- Create: `Assets/WorldGen/Editor/Art/PlaceholderMeshes.cs`
- Create: `Assets/WorldGen/Editor/Art/PlaceholderPrefabFactory.cs`
- Create: `Assets/WorldGen/Editor/Art/ArtAssetBuilder.cs`
- Create (oleh tool): `Assets/WorldGen/Textures/Facade/*.png` (22), `Assets/WorldGen/Materials/*.mat` (42), `Assets/WorldGen/Prefabs/{Tree_LowPoly_A,Tree_LowPoly_B,StreetLamp_A}.prefab`, `Assets/WorldGen/Prefabs/Meshes/*.asset`, `Assets/WorldGen/Config/WorldGenSettings.asset`
- Test: `Assets/WorldGen/Tests/ArtTests.cs`

**Interfaces:**
- Consumes: `MeshData` (Task 7), `SplitMix64` (Task 5), `AssetFolders`, `WorldGenSettings`, `DefaultSettingsFactory`, `AreaCategory`, `BuildingMaterialSet` (Task 9), `WorldGenPaths` (Task 1).
- Produces:
  - `enum FacadeStyle { ResidentialUpper, Plain, RollingDoor, KosDoor, Warung, WallWindow, CampusUpper, CampusGround }`
  - `readonly struct FacadeSpec { string Name; FacadeStyle Style; Color Wall }`
  - `static class FacadeTextureGenerator { const int Size = 256; Color[] ResidentialPalette; Color[] CampusPalette; List<FacadeSpec> Specs(); Texture2D Render(FacadeSpec spec); List<string> WriteAll(string folder) }`
  - `static class PlaceholderMeshes { void AddCylinder(MeshData m, int sub, Vector3 baseCenter, float radius, float height, int sides); void AddCone(...same...); void AddIcosahedron(MeshData m, int sub, Vector3 center, float radius); void AddBox(MeshData m, int sub, Vector3 center, Vector3 size) }`
  - `sealed class PlaceholderPrefabs { GameObject TreeA, TreeB, Lamp }`, `static class PlaceholderPrefabFactory { PlaceholderPrefabs CreateAll(IReadOnlyDictionary<string, Material> materials) }`
  - `static class ArtAssetBuilder { string[] Cc0Ids; WorldGenSettings BuildAll(); string AreaMaterialName(AreaCategory category) }` + menu `Tyco World/World Generator/Build Art Assets`

- [ ] **Step 1: Download 10 tekstur CC0 dari ambientCG**

Arsip download diperlakukan sebagai data tak dipercaya: tiap arsip diekstrak ke folder kosongnya sendiri di `%TEMP%`, dan hanya file `*_Color.jpg` / `*_NormalGL.jpg` yang disalin ke project.

```powershell
$ids = "Asphalt033","PavingStones070","PavingStones138","Ground037","Ground106","Grass004","Grass005","Concrete034","Concrete046","RoofingTiles006"
$dest = "D:\UNITY\project\Tyco World\Assets\WorldGen\Textures\CC0"
foreach ($id in $ids) {
  $work = Join-Path $env:TEMP ("tyco_cc0_" + $id)
  Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
  New-Item -ItemType Directory $work | Out-Null
  $zip = Join-Path $work "$id.zip"
  Invoke-WebRequest -Uri "https://ambientcg.com/get?file=${id}_1K-JPG.zip" -OutFile $zip -UserAgent "TycoWorld-WorldGen/1.0 (Unity editor; personal project)" -TimeoutSec 120
  Expand-Archive -Path $zip -DestinationPath (Join-Path $work "x") -Force
  $out = Join-Path $dest $id
  New-Item -ItemType Directory -Force $out | Out-Null
  Get-ChildItem (Join-Path $work "x") -Recurse -File | Where-Object { $_.Name -match "_(Color|NormalGL)\.jpg$" } | ForEach-Object { Copy-Item $_.FullName (Join-Path $out $_.Name) -Force }
  "{0}: {1}" -f $id, ((Get-ChildItem $out).Name -join ", ")
  Remove-Item -Recurse -Force $work
}
```

Expected: 10 baris, masing-masing berisi `<id>_1K-JPG_Color.jpg, <id>_1K-JPG_NormalGL.jpg`.

- [ ] **Step 2: Tulis `SOURCES.md`**

`Assets/WorldGen/Textures/SOURCES.md`:

```markdown
# Sumber tekstur

Tekstur di `CC0/` berasal dari ambientCG (https://ambientcg.com), lisensi CC0 1.0 (domain publik,
tanpa kewajiban atribusi). Versi 1K-JPG; hanya peta Color dan NormalGL yang dipakai.

| Asset ID | Halaman | Dipakai untuk |
|---|---|---|
| Asphalt033 | https://ambientcg.com/view?id=Asphalt033 | Road_Asphalt |
| PavingStones070 | https://ambientcg.com/view?id=PavingStones070 | Path_Paving |
| PavingStones138 | https://ambientcg.com/view?id=PavingStones138 | Area_Campus |
| Ground037 | https://ambientcg.com/view?id=Ground037 | Ground_Dirt |
| Ground106 | https://ambientcg.com/view?id=Ground106 | Area_Cemetery |
| Grass004 | https://ambientcg.com/view?id=Grass004 | Area_Grass |
| Grass005 | https://ambientcg.com/view?id=Grass005 | Area_Farmland, Area_Pitch |
| Concrete034 | https://ambientcg.com/view?id=Concrete034 | Area_Parking, Area_Hardground |
| Concrete046 | https://ambientcg.com/view?id=Concrete046 | Roof_Dak_A, Roof_Dak_B |
| RoofingTiles006 | https://ambientcg.com/view?id=RoofingTiles006 | Roof_Genteng_Merah, Roof_Genteng_Coklat |

Tekstur di `Facade/` dibuat oleh `FacadeTextureGenerator` (placeholder milik proyek). Ganti dengan
foto sendiri lewat material `M_Res_*` / `M_Campus_*` kapan saja.
```

- [ ] **Step 3: Tulis tes yang gagal**

`Assets/WorldGen/Tests/ArtTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class ArtTests
    {
        static float Luminance(Color32 c) => (0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b) / 255f;

        static FacadeSpec Spec(string name) => FacadeTextureGenerator.Specs().Single(s => s.Name == name);

        [Test]
        public void Specs_Has22UniqueNames()
        {
            var specs = FacadeTextureGenerator.Specs();
            Assert.AreEqual(22, specs.Count);
            Assert.AreEqual(22, specs.Select(s => s.Name).Distinct().Count());
        }

        [Test]
        public void Render_IsDeterministicAndSized()
        {
            var a = FacadeTextureGenerator.Render(Spec("Res_Upper_0"));
            var b = FacadeTextureGenerator.Render(Spec("Res_Upper_0"));
            try
            {
                Assert.AreEqual(FacadeTextureGenerator.Size, a.width);
                Assert.AreEqual(FacadeTextureGenerator.Size, a.height);
                CollectionAssert.AreEqual(a.GetPixels32(), b.GetPixels32());
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }

        [Test]
        public void ResidentialUpper_WindowIsDarkerThanWall()
        {
            var tex = FacadeTextureGenerator.Render(Spec("Res_Upper_0"));
            try
            {
                Assert.Less(Luminance(tex.GetPixel(128, 150)), 0.3f);
                Assert.Greater(Luminance(tex.GetPixel(10, 120)), 0.6f);
            }
            finally { Object.DestroyImmediate(tex); }
        }

        [Test]
        public void RollingDoor_HasHorizontalStripes()
        {
            var tex = FacadeTextureGenerator.Render(Spec("Res_Ground_RollingDoor"));
            try
            {
                int transitions = 0;
                for (int y = 11; y < 190; y++)
                    if (Mathf.Abs(Luminance(tex.GetPixel(128, y)) - Luminance(tex.GetPixel(128, y - 1))) > 0.05f) transitions++;
                Assert.Greater(transitions, 10);
            }
            finally { Object.DestroyImmediate(tex); }
        }

        [Test]
        public void Cylinder_HasOutwardFaces()
        {
            var m = new MeshData(1);
            PlaceholderMeshes.AddCylinder(m, 0, Vector3.zero, 0.2f, 2f, 6);
            Assert.AreEqual(24, m.TriangleCount);
            AssertOutward(m, new Vector3(0f, 1f, 0f));
        }

        [Test]
        public void Cone_HasOutwardFaces()
        {
            var m = new MeshData(1);
            PlaceholderMeshes.AddCone(m, 0, Vector3.zero, 1.4f, 3.2f, 6);
            Assert.AreEqual(12, m.TriangleCount);
            AssertOutward(m, new Vector3(0f, 0.8f, 0f));
        }

        [Test]
        public void Icosahedron_HasTwentyOutwardFaces()
        {
            var m = new MeshData(1);
            PlaceholderMeshes.AddIcosahedron(m, 0, new Vector3(0f, 3f, 0f), 1.6f);
            Assert.AreEqual(20, m.TriangleCount);
            AssertOutward(m, new Vector3(0f, 3f, 0f));
        }

        [Test]
        public void Box_HasTwelveOutwardFaces()
        {
            var m = new MeshData(1);
            PlaceholderMeshes.AddBox(m, 0, new Vector3(0f, 5.9f, 0.6f), new Vector3(0.08f, 0.08f, 1.2f));
            Assert.AreEqual(12, m.TriangleCount);
            AssertOutward(m, new Vector3(0f, 5.9f, 0.6f));
        }

        [TestCase(AreaCategory.Grass, "Area_Grass")]
        [TestCase(AreaCategory.Wood, "Area_Grass")]
        [TestCase(AreaCategory.Cemetery, "Area_Cemetery")]
        [TestCase(AreaCategory.Paving, "Path_Paving")]
        [TestCase(AreaCategory.Campus, "Area_Campus")]
        public void AreaMaterialName_MapsCategories(AreaCategory category, string expected)
        {
            Assert.AreEqual(expected, ArtAssetBuilder.AreaMaterialName(category));
        }

        static void AssertOutward(MeshData m, Vector3 center)
        {
            Assert.IsFalse(m.HasInvalidValues());
            var idx = m.Indices(0);
            for (int t = 0; t < idx.Count; t += 3)
            {
                Vector3 a = m.Vertices[idx[t]], b = m.Vertices[idx[t + 1]], c = m.Vertices[idx[t + 2]];
                Assert.Greater(Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3f - center), 0f);
            }
        }
    }
}
```

- [ ] **Step 4: Jalankan, pastikan gagal**

Procedure R (sekaligus mengimpor JPG CC0), lalu Procedure T dengan `Group = "ArtTests"`. Expected: COMPILE FAILED.

- [ ] **Step 5: Implementasi**

`Assets/WorldGen/Editor/Art/FacadeTextureGenerator.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    public enum FacadeStyle { ResidentialUpper, Plain, RollingDoor, KosDoor, Warung, WallWindow, CampusUpper, CampusGround }

    public readonly struct FacadeSpec
    {
        public readonly string Name;
        public readonly FacadeStyle Style;
        public readonly Color Wall;

        public FacadeSpec(string name, FacadeStyle style, Color wall)
        {
            Name = name;
            Style = style;
            Wall = wall;
        }
    }

    /// <summary>Procedural placeholder facades. One texture = one bay: 3.0 m wide, one floor (3.2 m) high.</summary>
    public static class FacadeTextureGenerator
    {
        public const int Size = 256;

        public static readonly Color[] ResidentialPalette =
            { Hex("D9D4C7"), Hex("E3CFA0"), Hex("A9C3A0"), Hex("9FB8C9"), Hex("D8A9A9"), Hex("A7A7A7") };

        public static readonly Color[] CampusPalette = { Hex("C9C6BE"), Hex("B5B8B9") };

        public static List<FacadeSpec> Specs()
        {
            var list = new List<FacadeSpec>();
            for (int i = 0; i < ResidentialPalette.Length; i++) list.Add(new FacadeSpec("Res_Upper_" + i, FacadeStyle.ResidentialUpper, ResidentialPalette[i]));
            for (int i = 0; i < ResidentialPalette.Length; i++) list.Add(new FacadeSpec("Res_Trim_" + i, FacadeStyle.Plain, ResidentialPalette[i]));
            list.Add(new FacadeSpec("Res_Ground_RollingDoor", FacadeStyle.RollingDoor, ResidentialPalette[0]));
            list.Add(new FacadeSpec("Res_Ground_KosDoor", FacadeStyle.KosDoor, ResidentialPalette[1]));
            list.Add(new FacadeSpec("Res_Ground_Warung", FacadeStyle.Warung, ResidentialPalette[2]));
            list.Add(new FacadeSpec("Res_Ground_WallWindow", FacadeStyle.WallWindow, ResidentialPalette[3]));
            for (int i = 0; i < CampusPalette.Length; i++)
            {
                list.Add(new FacadeSpec("Campus_Upper_" + i, FacadeStyle.CampusUpper, CampusPalette[i]));
                list.Add(new FacadeSpec("Campus_Ground_" + i, FacadeStyle.CampusGround, CampusPalette[i]));
                list.Add(new FacadeSpec("Campus_Trim_" + i, FacadeStyle.Plain, CampusPalette[i]));
            }
            return list;
        }

        public static Texture2D Render(FacadeSpec spec)
        {
            var px = new Color32[Size * Size];
            var rng = new SplitMix64(SplitMix64.Fnv1a64(spec.Name));
            for (int y = 0; y < Size; y++)
            {
                float v = y / (float)(Size - 1);
                float grime = v < 0.2f ? Mathf.Lerp(0.82f, 1f, v / 0.2f) : (v > 0.95f ? 0.95f : 1f);
                for (int x = 0; x < Size; x++)
                {
                    float noise = 1f + ((float)rng.NextDouble() - 0.5f) * 0.08f;
                    px[y * Size + x] = Shade(spec.Wall, grime * noise);
                }
            }
            switch (spec.Style)
            {
                case FacadeStyle.ResidentialUpper:
                    Window(px, 0.30f, 0.70f, 0.35f, 0.80f, spec.Wall);
                    Fill(px, 0.27f, 0.73f, 0.31f, 0.33f, Shade(spec.Wall, 0.75f));
                    break;
                case FacadeStyle.WallWindow:
                    Window(px, 0.30f, 0.70f, 0.30f, 0.75f, spec.Wall);
                    break;
                case FacadeStyle.RollingDoor:
                    RollingDoor(px);
                    break;
                case FacadeStyle.KosDoor:
                    KosDoor(px, spec.Wall);
                    break;
                case FacadeStyle.Warung:
                    Warung(px);
                    break;
                case FacadeStyle.CampusUpper:
                    CampusUpper(px);
                    break;
                case FacadeStyle.CampusGround:
                    CampusGround(px);
                    break;
            }
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true) { name = spec.Name };
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        public static List<string> WriteAll(string folder)
        {
            AssetFolders.Ensure(folder);
            var paths = new List<string>();
            foreach (var spec in Specs())
            {
                var tex = Render(spec);
                string assetPath = folder + "/" + spec.Name + ".png";
                File.WriteAllBytes(WorldGenPaths.ToAbsolute(assetPath), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                paths.Add(assetPath);
            }
            return paths;
        }

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            return color;
        }

        static Color32 Shade(Color c, float k) =>
            new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), 1f);

        static void Fill(Color32[] px, float x0, float x1, float y0, float y1, Color32 color)
        {
            int ix0 = Mathf.Clamp(Mathf.RoundToInt(x0 * Size), 0, Size), ix1 = Mathf.Clamp(Mathf.RoundToInt(x1 * Size), 0, Size);
            int iy0 = Mathf.Clamp(Mathf.RoundToInt(y0 * Size), 0, Size), iy1 = Mathf.Clamp(Mathf.RoundToInt(y1 * Size), 0, Size);
            for (int y = iy0; y < iy1; y++)
                for (int x = ix0; x < ix1; x++)
                    px[y * Size + x] = color;
        }

        static void Window(Color32[] px, float x0, float x1, float y0, float y1, Color wall)
        {
            Fill(px, x0 - 0.02f, x1 + 0.02f, y0 - 0.02f, y1 + 0.02f, Shade(wall, 1.15f));
            int iy0 = Mathf.RoundToInt(y0 * Size), iy1 = Mathf.RoundToInt(y1 * Size);
            Color bottom = Hex("1C2228"), top = Hex("2E3A44");
            for (int y = iy0; y < iy1; y++)
            {
                float t = (y - iy0) / (float)Mathf.Max(1, iy1 - iy0);
                Fill(px, x0, x1, y / (float)Size, (y + 1) / (float)Size, Color.Lerp(bottom, top, t));
            }
            for (float bx = x0 + 0.05f; bx < x1 - 0.01f; bx += 0.055f) Fill(px, bx, bx + 0.008f, y0, y1, Hex("2A2A2A"));
        }

        static void RollingDoor(Color32[] px)
        {
            Fill(px, 0.08f, 0.92f, 0f, 0.78f, Hex("8C8F93"));
            int top = Mathf.RoundToInt(0.78f * Size);
            for (int y = 0; y < top; y += 6) Fill(px, 0.08f, 0.92f, y / (float)Size, (y + 2) / (float)Size, Hex("6E7175"));
            Fill(px, 0.06f, 0.94f, 0.78f, 0.86f, Hex("5A5D61"));
        }

        static void KosDoor(Color32[] px, Color wall)
        {
            Fill(px, 0.10f, 0.40f, 0f, 0.74f, Shade(wall, 1.15f));
            Fill(px, 0.12f, 0.38f, 0f, 0.72f, Hex("6B4A32"));
            Fill(px, 0.33f, 0.35f, 0.34f, 0.37f, Hex("C8B060"));
            Window(px, 0.52f, 0.86f, 0.35f, 0.75f, wall);
        }

        static void Warung(Color32[] px)
        {
            Fill(px, 0.06f, 0.94f, 0.15f, 0.70f, Hex("141414"));
            Fill(px, 0.06f, 0.94f, 0.15f, 0.32f, Hex("7A5C3A"));
            Fill(px, 0.06f, 0.94f, 0.74f, 0.92f, Hex("D8D2C0"));
        }

        static void CampusUpper(Color32[] px)
        {
            Fill(px, 0.04f, 0.96f, 0.30f, 0.85f, Hex("1E2A33"));
            for (float x = 0.04f; x < 0.97f; x += 0.23f) Fill(px, x, x + 0.012f, 0.30f, 0.85f, Hex("9A9EA2"));
        }

        static void CampusGround(Color32[] px)
        {
            Fill(px, 0.05f, 0.95f, 0f, 0.85f, Hex("22303A"));
            foreach (float x in new[] { 0.05f, 0.35f, 0.65f, 0.94f }) Fill(px, x, x + 0.012f, 0f, 0.85f, Hex("9A9EA2"));
            Fill(px, 0.42f, 0.58f, 0f, 0.80f, Hex("2C3E4A"));
        }
    }
}
```

`Assets/WorldGen/Editor/Art/PlaceholderMeshes.cs`:

```csharp
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Flat-shaded low-poly primitives for placeholder props.</summary>
    public static class PlaceholderMeshes
    {
        public static void AddCylinder(MeshData m, int sub, Vector3 baseCenter, float radius, float height, int sides)
        {
            var top = baseCenter + Vector3.up * height;
            for (int i = 0; i < sides; i++)
            {
                float a0 = 2f * Mathf.PI * i / sides, a1 = 2f * Mathf.PI * (i + 1) / sides;
                var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
                var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
                var outward = ((d0 + d1) * 0.5f).normalized;
                m.AddQuad(sub, baseCenter + d0, baseCenter + d1, top + d1, top + d0,
                    new Vector2(i, 0f), new Vector2(i + 1, 0f), new Vector2(i + 1, 1f), new Vector2(i, 1f), outward);
                m.AddTriangle(sub, top, top + d0, top + d1, Vector2.zero, Vector2.right, Vector2.up, Vector3.up);
                m.AddTriangle(sub, baseCenter, baseCenter + d0, baseCenter + d1, Vector2.zero, Vector2.right, Vector2.up, Vector3.down);
            }
        }

        public static void AddCone(MeshData m, int sub, Vector3 baseCenter, float radius, float height, int sides)
        {
            var apex = baseCenter + Vector3.up * height;
            for (int i = 0; i < sides; i++)
            {
                float a0 = 2f * Mathf.PI * i / sides, a1 = 2f * Mathf.PI * (i + 1) / sides;
                var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
                var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
                var mid = ((d0 + d1) * 0.5f).normalized;
                m.AddTriangle(sub, baseCenter + d0, baseCenter + d1, apex,
                    new Vector2(i, 0f), new Vector2(i + 1, 0f), new Vector2(i + 0.5f, 1f), (mid * height + Vector3.up * radius).normalized);
                m.AddTriangle(sub, baseCenter, baseCenter + d0, baseCenter + d1, Vector2.zero, Vector2.right, Vector2.up, Vector3.down);
            }
        }

        public static void AddIcosahedron(MeshData m, int sub, Vector3 center, float radius)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            for (int i = 0; i < v.Length; i++) v[i] = center + v[i].normalized * radius;
            int[] f =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };
            for (int i = 0; i < f.Length; i += 3)
            {
                Vector3 a = v[f[i]], b = v[f[i + 1]], c = v[f[i + 2]];
                m.AddTriangle(sub, a, b, c, Vector2.zero, Vector2.right, Vector2.up, (a + b + c) / 3f - center);
            }
        }

        public static void AddBox(MeshData m, int sub, Vector3 center, Vector3 size)
        {
            Vector3 half = size * 0.5f;
            foreach (var n in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
            {
                Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 w = Vector3.Cross(n, u);
                Vector3 c = center + Vector3.Scale(n, half);
                Vector3 du = Vector3.Scale(u, half), dw = Vector3.Scale(w, half);
                m.AddQuad(sub, c - du - dw, c + du - dw, c + du + dw, c - du + dw, Vector2.zero, Vector2.right, Vector2.one, Vector2.up, n);
            }
        }
    }
}
```

`Assets/WorldGen/Editor/Art/PlaceholderPrefabFactory.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TycoWorld.WorldGen.Authoring
{
    public sealed class PlaceholderPrefabs
    {
        public GameObject TreeA, TreeB, Lamp;
    }

    /// <summary>Creates low-poly placeholder prefabs (two trees, one street lamp). Re-running updates them in place.</summary>
    public static class PlaceholderPrefabFactory
    {
        public const string MeshFolder = WorldGenPaths.PrefabsFolder + "/Meshes";

        public static PlaceholderPrefabs CreateAll(IReadOnlyDictionary<string, Material> materials)
        {
            AssetFolders.Ensure(MeshFolder);
            var cleanScenes = new List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (!s.isDirty && s.path.Length > 0) cleanScenes.Add(s);
            }

            var trunk = materials["M_Tree_Trunk"];
            var leaves = materials["M_Tree_Leaves"];
            var metal = materials["M_Lamp_Metal"];
            var head = materials["M_Lamp_Head"];

            var treeA = new MeshData(2);
            PlaceholderMeshes.AddCylinder(treeA, 0, Vector3.zero, 0.18f, 2.2f, 6);
            PlaceholderMeshes.AddIcosahedron(treeA, 1, new Vector3(0f, 3.0f, 0f), 1.6f);
            var treeB = new MeshData(2);
            PlaceholderMeshes.AddCylinder(treeB, 0, Vector3.zero, 0.15f, 1.6f, 6);
            PlaceholderMeshes.AddCone(treeB, 1, new Vector3(0f, 1.4f, 0f), 1.4f, 3.2f, 6);
            var lamp = new MeshData(2);
            PlaceholderMeshes.AddCylinder(lamp, 0, Vector3.zero, 0.08f, 6f, 6);
            PlaceholderMeshes.AddBox(lamp, 0, new Vector3(0f, 5.9f, 0.6f), new Vector3(0.08f, 0.08f, 1.2f));
            PlaceholderMeshes.AddBox(lamp, 1, new Vector3(0f, 5.85f, 1.2f), new Vector3(0.35f, 0.12f, 0.25f));

            var result = new PlaceholderPrefabs
            {
                TreeA = SavePrefab("Tree_LowPoly_A", treeA, new[] { trunk, leaves }, go => AddCapsule(go, 0.2f, 2.2f)),
                TreeB = SavePrefab("Tree_LowPoly_B", treeB, new[] { trunk, leaves }, go => AddCapsule(go, 0.2f, 1.6f)),
                Lamp = SavePrefab("StreetLamp_A", lamp, new[] { metal, head }, go =>
                {
                    AddCapsule(go, 0.1f, 6f);
                    AddLampLight(go);
                })
            };

            foreach (var s in cleanScenes)
                if (s.IsValid() && s.isDirty) EditorSceneManager.SaveScene(s);
            return result;
        }

        static Mesh SaveMesh(string name, MeshData data)
        {
            string path = MeshFolder + "/" + name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = name };
                data.WriteTo(mesh, out _);
                AssetDatabase.CreateAsset(mesh, path);
            }
            else
            {
                data.WriteTo(mesh, out _);
                EditorUtility.SetDirty(mesh);
            }
            return mesh;
        }

        static GameObject SavePrefab(string name, MeshData data, Material[] materials, Action<GameObject> decorate)
        {
            var mesh = SaveMesh(name, data);
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var go = new GameObject(name);
                SceneManager.MoveGameObjectToScene(go, preview);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = materials;
                decorate(go);
                return PrefabUtility.SaveAsPrefabAsset(go, WorldGenPaths.PrefabsFolder + "/" + name + ".prefab");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        static void AddCapsule(GameObject go, float radius, float height)
        {
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.radius = radius;
            capsule.height = height;
            capsule.direction = 1;
            capsule.center = new Vector3(0f, height * 0.5f, 0f);
        }

        static void AddLampLight(GameObject lamp)
        {
            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(lamp.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 5.6f, 1.2f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 12f;
            light.intensity = 3f;
            light.color = new Color(1f, 0.77f, 0.54f);
            light.shadows = LightShadows.None;
        }
    }
}
```

`Assets/WorldGen/Editor/Art/ArtAssetBuilder.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Imports CC0 textures, generates facades, creates materials/prefabs and assigns them to WorldGenSettings.</summary>
    public static class ArtAssetBuilder
    {
        public static readonly string[] Cc0Ids =
        {
            "Asphalt033", "PavingStones070", "PavingStones138", "Ground037", "Ground106",
            "Grass004", "Grass005", "Concrete034", "Concrete046", "RoofingTiles006"
        };

        [MenuItem("Tyco World/World Generator/Build Art Assets")]
        static void BuildAllMenu()
        {
            BuildAll();
            EditorUtility.DisplayDialog("World Generator", "Art assets selesai dibuat dan dipasang ke Settings.", "OK");
        }

        public static WorldGenSettings BuildAll()
        {
            AssetFolders.Ensure(WorldGenPaths.MaterialsFolder);
            AssetFolders.Ensure(WorldGenPaths.FacadeTexturesFolder);
            AssetFolders.Ensure(WorldGenPaths.PrefabsFolder);
            CheckCc0Present();
            foreach (var id in Cc0Ids)
            {
                Configure(Cc0Path(id, "Color"), false, 512);
                Configure(Cc0Path(id, "NormalGL"), true, 512);
            }
            foreach (var path in FacadeTextureGenerator.WriteAll(WorldGenPaths.FacadeTexturesFolder))
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                Configure(path, false, 256);
            }
            var materials = CreateMaterials();
            var prefabs = PlaceholderPrefabFactory.CreateAll(materials);
            var settings = DefaultSettingsFactory.LoadOrCreateAsset();
            Assign(settings, materials, prefabs);
            AssetDatabase.SaveAssets();
            return settings;
        }

        public static string AreaMaterialName(AreaCategory category)
        {
            switch (category)
            {
                case AreaCategory.Campus: return "Area_Campus";
                case AreaCategory.Hardground: return "Area_Hardground";
                case AreaCategory.Grass:
                case AreaCategory.Wood: return "Area_Grass";
                case AreaCategory.Farmland: return "Area_Farmland";
                case AreaCategory.Parking: return "Area_Parking";
                case AreaCategory.Cemetery: return "Area_Cemetery";
                case AreaCategory.Pitch: return "Area_Pitch";
                default: return "Path_Paving";
            }
        }

        static string Cc0Path(string id, string map) => WorldGenPaths.Cc0TexturesFolder + "/" + id + "/" + id + "_1K-JPG_" + map + ".jpg";

        static void CheckCc0Present()
        {
            var missing = new List<string>();
            foreach (var id in Cc0Ids)
                foreach (var map in new[] { "Color", "NormalGL" })
                    if (!File.Exists(WorldGenPaths.ToAbsolute(Cc0Path(id, map)))) missing.Add(Cc0Path(id, map));
            if (missing.Count > 0)
                throw new FileNotFoundException("Tekstur CC0 belum didownload (Task 15 Step 1):\n" + string.Join("\n", missing));
        }

        static Texture2D Tex(string id, string map) => AssetDatabase.LoadAssetAtPath<Texture2D>(Cc0Path(id, map));

        static void Configure(string path, bool normalMap, int maxSize)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            bool changed = false;
            var type = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (importer.textureType != type) { importer.textureType = type; changed = true; }
            if (importer.maxTextureSize != maxSize) { importer.maxTextureSize = maxSize; changed = true; }
            if (importer.sRGBTexture == normalMap) { importer.sRGBTexture = !normalMap; changed = true; }
            if (importer.wrapMode != TextureWrapMode.Repeat) { importer.wrapMode = TextureWrapMode.Repeat; changed = true; }
            if (importer.filterMode != FilterMode.Bilinear) { importer.filterMode = FilterMode.Bilinear; changed = true; }
            if (!importer.mipmapEnabled) { importer.mipmapEnabled = true; changed = true; }
            if (changed) importer.SaveAndReimport();
        }

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            return color;
        }

        static Material Mat(string name, Texture2D baseMap, Texture2D normalMap, Color tint, float tileMeters, float smoothness = 0.1f)
        {
            string path = WorldGenPaths.MaterialsFolder + "/" + name + ".mat";
            var shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetTexture("_BaseMap", baseMap);
            material.SetColor("_BaseColor", tint);
            material.SetTexture("_BumpMap", normalMap);
            if (normalMap != null) material.EnableKeyword("_NORMALMAP");
            else material.DisableKeyword("_NORMALMAP");
            float scale = tileMeters > 0f ? 1f / tileMeters : 1f;
            material.SetTextureScale("_BaseMap", new Vector2(scale, scale));
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Dictionary<string, Material> CreateMaterials()
        {
            var m = new Dictionary<string, Material>();
            void Cc0(string name, string id, string tint, float tile) => m[name] = Mat(name, Tex(id, "Color"), Tex(id, "NormalGL"), Hex(tint), tile);

            Cc0("Road_Asphalt", "Asphalt033", "FFFFFF", 4f);
            Cc0("Path_Paving", "PavingStones070", "FFFFFF", 2f);
            Cc0("Ground_Dirt", "Ground037", "FFFFFF", 4f);
            Cc0("Area_Grass", "Grass004", "FFFFFF", 4f);
            Cc0("Area_Farmland", "Grass005", "D8D890", 4f);
            Cc0("Area_Pitch", "Grass005", "C8F0A0", 4f);
            Cc0("Area_Parking", "Concrete034", "FFFFFF", 3f);
            Cc0("Area_Hardground", "Concrete034", "B0B0B0", 3f);
            Cc0("Area_Cemetery", "Ground106", "707070", 4f);
            Cc0("Area_Campus", "PavingStones138", "E0E0E0", 2f);
            Cc0("Roof_Genteng_Merah", "RoofingTiles006", "FFFFFF", 2f);
            Cc0("Roof_Genteng_Coklat", "RoofingTiles006", "8A6A5A", 2f);
            Cc0("Roof_Dak_A", "Concrete046", "FFFFFF", 3f);
            Cc0("Roof_Dak_B", "Concrete046", "A8A8A8", 3f);
            m["Water"] = Mat("Water", null, null, Hex("10161A"), 1f, 0.9f);
            m["WorldGen_Fallback"] = Mat("WorldGen_Fallback", null, null, Hex("808080"), 1f);

            foreach (var spec in FacadeTextureGenerator.Specs())
            {
                string name = "M_" + spec.Name;
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(WorldGenPaths.FacadeTexturesFolder + "/" + spec.Name + ".png");
                m[name] = Mat(name, tex, null, Color.white, 1f);
            }

            m["M_Tree_Trunk"] = Mat("M_Tree_Trunk", null, null, Hex("5A4030"), 1f);
            m["M_Tree_Leaves"] = Mat("M_Tree_Leaves", null, null, Hex("3E5A2E"), 1f);
            m["M_Lamp_Metal"] = Mat("M_Lamp_Metal", null, null, Hex("4A4A4A"), 1f, 0.4f);
            var head = Mat("M_Lamp_Head", null, null, Hex("FFE8B0"), 1f);
            head.EnableKeyword("_EMISSION");
            head.SetColor("_EmissionColor", Hex("FFE8B0") * 2f);
            head.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(head);
            m["M_Lamp_Head"] = head;
            return m;
        }

        static Material[] Seq(IReadOnlyDictionary<string, Material> m, string prefix, int count)
        {
            var result = new Material[count];
            for (int i = 0; i < count; i++) result[i] = m[prefix + i];
            return result;
        }

        static void Assign(WorldGenSettings s, IReadOnlyDictionary<string, Material> m, PlaceholderPrefabs prefabs)
        {
            foreach (var rule in s.roadRules)
                rule.material = rule.highway is "footway" or "path" or "cycleway" or "steps" or "pedestrian" ? m["Path_Paving"]
                    : rule.highway == "track" ? m["Ground_Dirt"] : m["Road_Asphalt"];

            s.residentialMaterials = new BuildingMaterialSet
            {
                groundFloor = new[] { m["M_Res_Ground_RollingDoor"], m["M_Res_Ground_KosDoor"], m["M_Res_Ground_Warung"], m["M_Res_Ground_WallWindow"] },
                upper = Seq(m, "M_Res_Upper_", 6),
                trim = Seq(m, "M_Res_Trim_", 6),
                flatRoof = new[] { m["Roof_Dak_A"], m["Roof_Dak_B"] },
                gableRoof = new[] { m["Roof_Genteng_Merah"], m["Roof_Genteng_Coklat"] }
            };
            s.campusMaterials = new BuildingMaterialSet
            {
                groundFloor = Seq(m, "M_Campus_Ground_", 2),
                upper = Seq(m, "M_Campus_Upper_", 2),
                trim = Seq(m, "M_Campus_Trim_", 2),
                flatRoof = new[] { m["Roof_Dak_A"] },
                gableRoof = new[] { m["Roof_Genteng_Merah"] }
            };
            foreach (var rule in s.areaRules) rule.material = m[AreaMaterialName(rule.category)];
            s.waterMaterial = m["Water"];
            s.groundMaterial = m["Ground_Dirt"];
            s.fallbackMaterial = m["WorldGen_Fallback"];
            s.treePrefabs = new[] { prefabs.TreeA, prefabs.TreeB };
            s.lampPrefab = prefabs.Lamp;
            EditorUtility.SetDirty(s);
        }
    }
}
```

- [ ] **Step 6: Jalankan tes, pastikan lulus**

Procedure R, lalu Procedure T dengan `Group = "ArtTests"`. Expected: `passed=13 failed=0` (8 tes + 5 kasus `AreaMaterialName`).

- [ ] **Step 7: Verifikasi V15 — bangun semua aset art**

`mcp__unity-mcp__Unity_RunCommand`:

```csharp
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using TycoWorld.WorldGen.Authoring;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var s = ArtAssetBuilder.BuildAll();
        int materials = Directory.GetFiles(WorldGenPaths.ToAbsolute(WorldGenPaths.MaterialsFolder), "*.mat").Length;
        int facades = Directory.GetFiles(WorldGenPaths.ToAbsolute(WorldGenPaths.FacadeTexturesFolder), "*.png").Length;
        bool roadsOk = s.roadRules.All(r => r.material != null);
        bool areasOk = s.areaRules.All(a => a.material != null);
        bool setsOk = new[] { s.residentialMaterials, s.campusMaterials }.All(set =>
            new[] { set.groundFloor, set.upper, set.trim, set.flatRoof, set.gableRoof }.All(list => list.Length > 0 && list.All(m => m != null)));
        bool propsOk = s.treePrefabs.Length == 2 && s.treePrefabs.All(p => p != null) && s.lampPrefab != null
                       && s.lampPrefab.GetComponentInChildren<Light>() != null;
        result.Log("materials={0} facades={1} roadsOk={2} areasOk={3} setsOk={4} propsOk={5} water={6} ground={7}",
            materials, facades, roadsOk, areasOk, setsOk, propsOk, s.waterMaterial != null, s.groundMaterial != null);
        if (materials != 42 || facades != 22 || !roadsOk || !areasOk || !setsOk || !propsOk) result.LogError("V15 GAGAL");
    }
}
```

Expected: `materials=42 facades=22 roadsOk=True areasOk=True setsOk=True propsOk=True water=True ground=True`. Cek juga `mcp__unity-mcp__Unity_GetConsoleLogs` (`logTypes: "error"`) → kosong. Lalu Procedure T dengan `Group = "."` → `failed=0`.

- [ ] **Step 8: Commit**

Procedure C: `git add Assets/WorldGen`, subject `feat(worldgen): add CC0 textures, facade generator, materials and placeholder props`.

---

### Task 16: Scene preview + penjaga konfigurasi render

**Files:**
- Create: `Assets/WorldGen/Editor/Preview/PreviewSceneSetup.cs`
- Modify: `Assets/WorldGen/Editor/UI/WorldGenWindow.cs` (tambah tombol Setup Preview Scene)
- Modify (oleh tool): `Assets/Scenes/World.unity`; Create: `Assets/WorldGen/Config/Preview_NightVHS.asset`
- Test: `Assets/WorldGen/Tests/PreviewSceneSetupTests.cs`, `Assets/WorldGen/Tests/RenderConfigTests.cs`

**Interfaces:**
- Consumes: `WorldScenes` (Task 13), `OsmParser`, `OsmData` (Task 4), `GeoProjection` (Task 3), `WorldGenSettings`, `DefaultSettingsFactory`, `AssetFolders` (Task 9).
- Produces: `static class PreviewSceneSetup { const float EyeHeight = 1.6f; const long SukabirusWayId = 95529338; bool TryComputeStreetPose(OsmData data, GeoProjection projection, long wayId, out Vector3 position, out float yawDegrees); bool Run(WorldGenSettings settings, bool interactive, out string error) }` + menu `Tyco World/World Generator/Setup Preview Scene`.

Catatan: konfigurasi render yang diminta spec §13 (Forward+, GPU Resident Drawer Instanced Drawing, SRP Batcher, BRG Keep All) **sudah aktif** di template project (diverifikasi via MCP 2026-10-13). `RenderConfigTests` menjaganya agar tidak berubah; tes ini lulus sejak awal. Flag `BatchingStatic` sengaja tidak dipasang pada objek generate (Task 13) karena batching ditangani GPU Resident Drawer.

- [ ] **Step 1: Tulis tes**

`Assets/WorldGen/Tests/RenderConfigTests.cs`:

```csharp
using NUnit.Framework;
using UnityEditor;
using UnityEngine.Rendering.Universal;

namespace TycoWorld.WorldGen.Tests
{
    public class RenderConfigTests
    {
        [Test]
        public void PcRenderer_UsesForwardPlus()
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            Assert.IsNotNull(data);
            Assert.AreEqual(UnityEngine.Rendering.Universal.RenderingMode.ForwardPlus, data.renderingMode);
        }

        [Test]
        public void PcPipeline_UsesGpuResidentDrawer()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            Assert.IsNotNull(asset);
            Assert.AreEqual(UnityEngine.Rendering.GPUResidentDrawerMode.InstancedDrawing, asset.gpuResidentDrawerMode);
            Assert.IsTrue(asset.useSRPBatcher);
        }

        [Test]
        public void BrgVariants_KeepAll()
        {
            Assert.AreEqual(UnityEditor.Rendering.BatchRendererGroupStrippingMode.KeepAll,
                UnityEditor.Rendering.EditorGraphicsSettings.batchRendererGroupShaderStrippingMode);
        }
    }
}
```

`Assets/WorldGen/Tests/PreviewSceneSetupTests.cs`:

```csharp
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class PreviewSceneSetupTests
    {
        static readonly GeoProjection Projection = new GeoProjection(-6.97265, 107.63400);

        [Test]
        public void StreetPose_OnSukabirus()
        {
            var data = OsmParser.Parse(FixtureFiles.Read("sukabirus_small.json"));
            Assert.IsTrue(PreviewSceneSetup.TryComputeStreetPose(data, Projection, PreviewSceneSetup.SukabirusWayId, out var position, out float yaw));
            Assert.AreEqual(PreviewSceneSetup.EyeHeight, position.y, 1e-5f);
            var bounds = Projection.ToLocalRect(new GeoBounds(-6.9850, 107.6255, -6.9675, 107.6380));
            Assert.IsTrue(bounds.Contains(new Vector2(position.x, position.z)));

            var way = data.Ways[PreviewSceneSetup.SukabirusWayId];
            var first = data.Nodes[way.NodeIds[0]];
            var last = data.Nodes[way.NodeIds[way.NodeIds.Count - 1]];
            var axis = (Projection.ToLocal(last.Lat, last.Lon) - Projection.ToLocal(first.Lat, first.Lon)).normalized;
            var forward = new Vector2(Mathf.Sin(yaw * Mathf.Deg2Rad), Mathf.Cos(yaw * Mathf.Deg2Rad));
            Assert.Greater(Mathf.Abs(Vector2.Dot(forward, axis)), 0.7f);
        }

        [Test]
        public void StreetPose_MissingWay_ReturnsFalse()
        {
            Assert.IsFalse(PreviewSceneSetup.TryComputeStreetPose(new OsmData(), Projection, 1, out _, out _));
        }
    }
}
```

- [ ] **Step 2: Jalankan, pastikan `PreviewSceneSetupTests` gagal**

Procedure R, lalu Procedure T dengan `Group = "PreviewSceneSetupTests|RenderConfigTests"`. Expected: COMPILE FAILED (`PreviewSceneSetup` tidak ditemukan).

- [ ] **Step 3: Implementasi**

`Assets/WorldGen/Editor/Preview/PreviewSceneSetup.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Fills World.unity with night lighting, fog, a VHS volume and a street-level preview camera (spec section 13).</summary>
    public static class PreviewSceneSetup
    {
        public const float EyeHeight = 1.6f;
        public const long SukabirusWayId = 95529338;

        [MenuItem("Tyco World/World Generator/Setup Preview Scene")]
        static void Menu()
        {
            if (!Run(DefaultSettingsFactory.LoadOrCreateAsset(), true, out string error))
                EditorUtility.DisplayDialog("Setup Preview gagal", error, "OK");
        }

        public static bool TryComputeStreetPose(OsmData data, GeoProjection projection, long wayId, out Vector3 position, out float yawDegrees)
        {
            position = new Vector3(0f, EyeHeight, 0f);
            yawDegrees = 0f;
            if (!data.Ways.TryGetValue(wayId, out var way)) return false;
            var pts = new List<Vector2>();
            foreach (long id in way.NodeIds)
            {
                if (!data.Nodes.TryGetValue(id, out var node)) return false;
                pts.Add(projection.ToLocal(node.Lat, node.Lon));
            }
            if (pts.Count < 2) return false;

            float total = 0f;
            for (int i = 0; i + 1 < pts.Count; i++) total += Vector2.Distance(pts[i], pts[i + 1]);
            float half = total * 0.5f, travelled = 0f;
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                float length = Vector2.Distance(pts[i], pts[i + 1]);
                if (length <= 0f || travelled + length < half)
                {
                    travelled += length;
                    continue;
                }
                var mid = Vector2.Lerp(pts[i], pts[i + 1], (half - travelled) / length);
                var dir = (pts[i + 1] - pts[i]) / length;
                position = new Vector3(mid.x, EyeHeight, mid.y);
                yawDegrees = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                return true;
            }
            return false;
        }

        public static bool Run(WorldGenSettings settings, bool interactive, out string error)
        {
            if (!WorldScenes.CheckSaved(interactive, out error)) return false;
            var world = WorldScenes.EnsureWorldSceneOpen();

            var moonGo = FindOrCreate(world, "Moon");
            moonGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var moon = GetOrAdd<Light>(moonGo);
            moon.type = LightType.Directional;
            moon.intensity = 0.15f;
            moon.color = Hex("9DB4FF");
            moon.shadows = LightShadows.Soft;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Hex("121826");
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.02f;
            RenderSettings.fogColor = Hex("0E1218");
            RenderSettings.skybox = null;
            RenderSettings.sun = moon;

            var camGo = FindOrCreate(world, "PreviewCam");
            camGo.tag = "MainCamera";
            var cam = GetOrAdd<Camera>(camGo);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Hex("0E1218");
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 400f;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            if (TryLoadPose(settings, out var position, out float yaw))
            {
                camGo.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            }
            else
            {
                camGo.transform.SetPositionAndRotation(new Vector3(0f, EyeHeight, 0f), Quaternion.identity);
                Debug.LogWarning("[WorldGen] Jl. Sukabirus tidak ditemukan di cache OSM; PreviewCam diletakkan di titik nol.");
            }

            var volume = GetOrAdd<Volume>(FindOrCreate(world, "Global Volume"));
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = LoadOrCreateProfile();

            GetOrAdd<WorldSceneLoader>(FindOrCreate(world, "WorldSceneLoader"));

            EditorSceneManager.MarkSceneDirty(world);
            EditorSceneManager.SaveScene(world);
            WorldScenes.OpenGeneratedSceneAdditive();
            SceneManager.SetActiveScene(world);
            return true;
        }

        static bool TryLoadPose(WorldGenSettings settings, out Vector3 position, out float yaw)
        {
            position = default;
            yaw = 0f;
            string raw = WorldGenPaths.ToAbsolute(settings.rawDataPath);
            if (!File.Exists(raw)) return false;
            return TryComputeStreetPose(OsmParser.Parse(File.ReadAllText(raw)), settings.CreateProjection(), SukabirusWayId, out position, out yaw);
        }

        static GameObject FindOrCreate(Scene scene, string name)
        {
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == name) return go;
            var created = new GameObject(name);
            SceneManager.MoveGameObjectToScene(created, scene);
            return created;
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            if (component == null) component = go.AddComponent<T>();
            return component;
        }

        static VolumeProfile LoadOrCreateProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(WorldGenPaths.VolumeProfileAsset);
            if (profile == null)
            {
                AssetFolders.Ensure(WorldGenPaths.ConfigFolder);
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, WorldGenPaths.VolumeProfileAsset);
            }
            Get<Tonemapping>(profile).mode.Override(TonemappingMode.ACES);
            var color = Get<ColorAdjustments>(profile);
            color.saturation.Override(-25f);
            color.contrast.Override(10f);
            Get<FilmGrain>(profile).intensity.Override(0.5f);
            Get<Vignette>(profile).intensity.Override(0.35f);
            Get<ChromaticAberration>(profile).intensity.Override(0.15f);
            var bloom = Get<Bloom>(profile);
            bloom.intensity.Override(0.6f);
            bloom.threshold.Override(1f);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        static T Get<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T component)) return component;
            component = profile.Add<T>(true);
            component.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            return color;
        }
    }
}
```

Di `Assets/WorldGen/Editor/UI/WorldGenWindow.cs`, ganti blok:

```csharp
                if (GUILayout.Button("Clear")) WorldGenerationRunner.Clear(true, out _);
            }
```

dengan:

```csharp
                if (GUILayout.Button("Clear")) WorldGenerationRunner.Clear(true, out _);
                if (GUILayout.Button("Setup Preview Scene") && !PreviewSceneSetup.Run(settings, true, out string previewError))
                    EditorUtility.DisplayDialog("Setup Preview gagal", previewError, "OK");
            }
```

- [ ] **Step 4: Jalankan, pastikan lulus**

Procedure R, lalu Procedure T dengan `Group = "PreviewSceneSetupTests|RenderConfigTests"`. Expected: `passed=5 failed=0`.

- [ ] **Step 5: Verifikasi V16 — isi World.unity**

`mcp__unity-mcp__Unity_RunCommand`:

```csharp
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using TycoWorld.WorldGen;
using TycoWorld.WorldGen.Authoring;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (!PreviewSceneSetup.Run(DefaultSettingsFactory.LoadOrCreateAsset(), false, out string err)) { result.LogError("Setup gagal: " + err); return; }
        var world = SceneManager.GetSceneByPath(WorldGenPaths.WorldScenePath);
        bool moon = false, cam = false, volume = false, loader = false;
        foreach (var go in world.GetRootGameObjects())
        {
            moon |= go.name == "Moon" && go.GetComponent<Light>() != null;
            cam |= go.name == "PreviewCam" && go.GetComponent<Camera>() != null;
            volume |= go.name == "Global Volume" && go.GetComponent<Volume>() != null && go.GetComponent<Volume>().sharedProfile.components.Count == 6;
            loader |= go.GetComponent<WorldSceneLoader>() != null;
        }
        result.Log("moon={0} cam={1} volume6={2} loader={3} fog={4} density={5} dirty={6}", moon, cam, volume, loader, RenderSettings.fog, RenderSettings.fogDensity, world.isDirty);
        if (!moon || !cam || !volume || !loader || !RenderSettings.fog || world.isDirty) result.LogError("V16 GAGAL");
    }
}
```

Expected: semua `True`, `density=0.02`, `dirty=False`. (Saat ini cache OSM asli belum ada, jadi warning "PreviewCam diletakkan di titik nol" wajar; posisinya diperbaiki di Task 17.)

- [ ] **Step 6: Commit**

Procedure C: `git add Assets/WorldGen Assets/Scenes/World.unity`, subject `feat(worldgen): add night VHS preview scene setup`.

---

### Task 17: Integrasi dengan data asli + baseline

**Files:**
- Create (download): `WorldGenData/osm_raw.json`, `WorldGenData/osm_raw.info.json`
- Modify (oleh tool): `Assets/Scenes/World.unity` (posisi PreviewCam)
- Create: `docs/superpowers/notes/2026-10-13-worldgen-baseline.md`

**Interfaces:**
- Consumes: semua task sebelumnya.
- Produces: data cache yang dilacak git, hasil generate yang terverifikasi, dan catatan baseline.

- [ ] **Step 1: Semua tes hijau**

Procedure R, lalu Procedure T dengan `Group = "."`. Expected: `failed=0`.

- [ ] **Step 2: Mulai download data asli**

`mcp__unity-mcp__Unity_RunCommand`:

```csharp
using TycoWorld.WorldGen.Authoring;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var fetcher = OsmFetcher.Start(DefaultSettingsFactory.LoadOrCreateAsset());
        result.Log("started: {0}", fetcher.Status);
    }
}
```

Jangan menjalankan Procedure R selama download berlangsung (domain reload akan memutus download).

- [ ] **Step 3: Pantau sampai selesai**

Ulangi tiap ±30 detik sampai `done=True`:

```csharp
using TycoWorld.WorldGen.Authoring;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var f = OsmFetcher.Active;
        if (f == null) { result.LogError("Tidak ada download aktif."); return; }
        result.Log("done={0} ok={1} status={2} error={3}", f.IsDone, f.Succeeded, f.Status, f.Error);
    }
}
```

Expected akhir: `done=True ok=True`. Jika `ok=False` karena semua endpoint 504/timeout, tunggu 10 menit lalu ulangi Step 2 (server Overpass sedang sibuk; jangan lanjut tanpa data).

- [ ] **Step 4: Cocokkan isi data dengan spec §3 (toleransi ±10%)**

```csharp
using System.IO;
using System.Linq;
using TycoWorld.WorldGen.Authoring;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var s = DefaultSettingsFactory.LoadOrCreateAsset();
        var d = OsmParser.Parse(File.ReadAllText(WorldGenPaths.ToAbsolute(s.rawDataPath)));
        int buildings = d.Ways.Values.Count(w => w.Tags.ContainsKey("building"));
        int highways = d.Ways.Values.Count(w => w.Tags.ContainsKey("highway"));
        bool sukabirus = d.Ways.ContainsKey(95529338);
        result.Log("nodes={0} ways={1} relations={2} buildings={3} highways={4} sukabirus={5}", d.Nodes.Count, d.Ways.Count, d.Relations.Count, buildings, highways, sukabirus);
        if (buildings < 4485 || buildings > 5481 || highways < 691 || highways > 845 || !sukabirus) result.LogError("Data di luar toleransi spec §3");
        result.Log(CacheWriter.ReadInfoSummary(WorldGenPaths.ToAbsolute(s.rawDataPath)));
    }
}
```

Expected: `buildings` 4485–5481, `highways` 691–845, `sukabirus=True`, tanpa error.

- [ ] **Step 5: Catat hash World.unity, lalu Generate #1**

```powershell
(Get-FileHash "D:\UNITY\project\Tyco World\Assets\Scenes\World.unity").Hash
```

Simpan nilainya (sebut `H0`). Lalu `mcp__unity-mcp__Unity_RunCommand`:

```csharp
using System.Linq;
using TycoWorld.WorldGen.Authoring;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var s = DefaultSettingsFactory.LoadOrCreateAsset();
        if (!WorldGenerationRunner.Generate(s, false, out string err)) { result.LogError("Generate gagal: " + err); return; }
        var r = WorldGenerationRunner.LastReport;
        int skipped = r.SkippedTotal("building:") + r.SkippedTotal("relation:");
        int total = skipped + r.GetCount("Building") + r.GetCount("KeyBuilding") + r.GetCount("KeyBuildingPrefab");
        double rate = total > 0 ? (double)skipped / total : 1.0;
        result.Log("skipRate={0:P2} totalMs={1:0} chunks={2}", rate, r.TotalMillis, r.ChunkHashes.Count);
        result.Log(r.Summary());
        if (rate >= 0.02 || r.TotalMillis >= 60000) result.LogError("Kriteria skip < 2% atau waktu < 60 s tidak terpenuhi");
    }
}
```

Expected: `skipRate` < 2.00%, `totalMs` < 60000, tanpa error. Lalu `mcp__unity-mcp__Unity_GetConsoleLogs` (`logTypes: "error"`) → tidak ada error. Warning material tidak boleh muncul (Task 15 sudah mengisi semua material).

- [ ] **Step 6: Generate #2 — determinisme, asset yatim, World.unity tidak berubah**

```csharp
using System.IO;
using System.Linq;
using TycoWorld.WorldGen.Authoring;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var s = DefaultSettingsFactory.LoadOrCreateAsset();
        var first = WorldGenerationRunner.LastReport.ChunkHashes.ToDictionary(kv => kv.Key, kv => kv.Value);
        if (!WorldGenerationRunner.Generate(s, false, out string err)) { result.LogError("Generate #2 gagal: " + err); return; }
        var second = WorldGenerationRunner.LastReport.ChunkHashes;
        bool identical = first.Count == second.Count && first.All(kv => second.TryGetValue(kv.Key, out var v) && v == kv.Value);
        var files = Directory.GetFiles(WorldGenPaths.ToAbsolute(WorldGenPaths.GeneratedMeshesFolder), "Chunk_*.asset").Select(Path.GetFileNameWithoutExtension);
        bool noOrphans = files.All(second.ContainsKey);
        result.Log("identical={0} noOrphans={1}", identical, noOrphans);
        if (!identical || !noOrphans) result.LogError("Determinisme/asset yatim GAGAL");
    }
}
```

Expected: `identical=True noOrphans=True`. Lalu jalankan lagi `Get-FileHash` pada `World.unity` → harus sama dengan `H0`.

- [ ] **Step 7: Raycast collider**

```csharp
using System.Linq;
using UnityEngine;
using TycoWorld.WorldGen;
using TycoWorld.WorldGen.Authoring;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        Physics.SyncTransforms();
        var s = DefaultSettingsFactory.LoadOrCreateAsset();
        var bounds = s.CreateProjection().ToLocalRect(s.Bounds);
        var rng = new SplitMix64(2026);
        int groundHits = 0;
        for (int i = 0; i < 20; i++)
        {
            var p = new Vector3(rng.Range(bounds.xMin + 5f, bounds.xMax - 5f), 200f, rng.Range(bounds.yMin + 5f, bounds.yMax - 5f));
            var hits = Physics.RaycastAll(p, Vector3.down, 400f);
            if (hits.Any(h => h.collider.gameObject.name == "Ground" && Mathf.Abs(h.point.y) < 1e-3f)) groundHits++;
        }
        int keyHits = 0, keyCount = 0;
        foreach (var marker in Object.FindObjectsByType<KeyBuildingMarker>(FindObjectsSortMode.None))
        {
            keyCount++;
            var renderer = marker.GetComponent<Renderer>();
            Vector3 target = renderer != null ? renderer.bounds.center : marker.transform.position;
            target.y = 1.5f;
            var origin = target + new Vector3(60f, 0f, 0f);
            var hits = Physics.RaycastAll(origin, (target - origin).normalized, 80f);
            if (hits.Any(h => h.collider.gameObject == marker.gameObject)) keyHits++;
        }
        result.Log("groundHits={0}/20 keyBuildingHits={1}/{2}", groundHits, keyHits, keyCount);
        if (groundHits != 20 || keyCount != 5 || keyHits != keyCount) result.LogError("Raycast GAGAL");
    }
}
```

Expected: `groundHits=20/20 keyBuildingHits=5/5`.

- [ ] **Step 8: Posisikan PreviewCam dengan data asli**

Jalankan ulang skrip V16 dari Task 16 Step 5 (sekarang `TryLoadPose` menemukan Jl. Sukabirus). Expected: sama seperti V16, tanpa warning "titik nol".

- [ ] **Step 9: Screenshot dari atas dan dari jalan**

Posisikan Scene View dari atas (tidak mengubah scene):

```csharp
using UnityEditor;
using UnityEngine;
using TycoWorld.WorldGen.Authoring;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var s = DefaultSettingsFactory.LoadOrCreateAsset();
        var b = s.CreateProjection().ToLocalRect(s.Bounds);
        var sv = SceneView.lastActiveSceneView;
        if (sv == null) { result.LogError("Buka jendela Scene View dulu."); return; }
        sv.sceneViewState.SetAllEnabled(true);
        sv.orthographic = true;
        sv.LookAtDirect(new Vector3(b.center.x, 0f, b.center.y), Quaternion.Euler(90f, 0f, 0f), Mathf.Max(b.width, b.height) * 0.55f);
        sv.Repaint();
        result.Log("top-down ready");
    }
}
```

Lalu `mcp__unity-mcp__Unity_Camera_Capture` tanpa `cameraInstanceID`. Bandingkan tata letaknya (Jl. Sukabirus memanjang ke selatan di timur kampus, blok kampus di barat laut) dengan https://www.openstreetmap.org/#map=16/-6.9763/107.6318.

Posisikan Scene View di PreviewCam:

```csharp
using UnityEditor;
using UnityEngine;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var cam = GameObject.Find("PreviewCam");
        var sv = SceneView.lastActiveSceneView;
        if (cam == null || sv == null) { result.LogError("PreviewCam atau Scene View tidak ada."); return; }
        sv.orthographic = false;
        sv.sceneViewState.SetAllEnabled(true);
        sv.AlignViewToObject(cam.transform);
        sv.Repaint();
        result.Log("street view ready at {0}", cam.transform.position);
    }
}
```

Lalu `mcp__unity-mcp__Unity_Camera_Capture` lagi. Expected: jalan beraspal bertekstur, deretan bangunan berfasad di kiri-kanan, lampu jalan menyala, kabut gelap. Tunjukkan kedua screenshot ke user.

- [ ] **Step 10: Baseline Play mode**

```csharp
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        EditorApplication.EnterPlaymode();
        result.Log("entering play mode");
    }
}
```

Tunggu ±10 detik (panggilan MCP berikutnya bisa gagal sekali karena domain reload; ulangi), lalu:

```csharp
using UnityEditor;
using UnityEngine;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        result.Log("playing={0} frameTimeMs={1:0.00} renderTimeMs={2:0.00} batches={3} tris={4} verts={5}",
            EditorApplication.isPlaying, UnityStats.frameTime * 1000f, UnityStats.renderTime * 1000f,
            UnityStats.batches, UnityStats.triangles, UnityStats.vertices);
        EditorApplication.ExitPlaymode();
    }
}
```

Catat angkanya. Cek `mcp__unity-mcp__Unity_GetConsoleLogs` (`logTypes: "error"`) → tidak ada error dari Play mode (misalnya `WorldSceneLoader`).

- [ ] **Step 11: Tulis catatan baseline**

`docs/superpowers/notes/2026-10-13-worldgen-baseline.md` — isi dengan angka nyata dari Step 4, 5, 7, 10:

```markdown
# World Generator — baseline (2026-10-13)

- Data OSM: <nodes> node, <ways> way, <relations> relation; <buildings> bangunan, <highways> jalan (diunduh <tanggal> dari <endpoint>)
- Generate: <totalMs> ms, <chunks> chunk, skip bangunan <skipRate>
- Fitur: <baris "Features:" dari Summary>
- Props: <baris "Trees:" dari Summary>
- Determinisme: hash chunk identik pada 2 kali generate; World.unity tidak berubah
- Raycast: ground 20/20, gedung kunci 5/5
- Play mode (PreviewCam, Jl. Sukabirus): frame <frameTimeMs> ms, render <renderTimeMs> ms, <batches> batches, <tris> segitiga
```

- [ ] **Step 12: Tes akhir + commit**

Procedure T dengan `Group = "."` → `failed=0`. Lalu Procedure C: `git add WorldGenData Assets/Scenes/World.unity docs/superpowers/notes`, subject `chore(worldgen): add OSM data cache and baseline notes`.

---

## Self-review (penulis rencana)

- **Cakupan spec:** §3 area/query → Task 4, 9, 14; §4 unit → Task 1–14; §5 proyeksi → Task 3; §6 klasifikasi → Task 10; §7 geometri → Task 5–8, 10, 12; §8 props → Task 11, 15; §9 material → Task 15; §10 gedung kunci → Task 9, 10, 12, 13; §11 output → Task 2, 12, 13; §12 settings → Task 9; §13 render/preview → Task 16 (konfigurasi sudah aktif, dijaga tes); §14 error → Task 4, 13, 14; §15 tes → semua task + Task 17; §16 kriteria selesai → Task 17.
- **Penyimpangan kecil dari spec yang disengaja:** (1) hapus folder `Generated/` via `System.IO` + Refresh (DeleteAsset diblok MCP); (2) tingkat skip bangunan dihitung tanpa `building:outside-bounds` (itu pemotongan batas area, bukan data rusak); (3) `farClipPlane` PreviewCam 400 m (tak tertulis di spec; aman karena fog density 0.02).

