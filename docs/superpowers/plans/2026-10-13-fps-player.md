# First-Person Player Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Pemain first-person yang berjalan kaki di dunia OSM hasil generator, dengan stamina, jongkok, head-bob, senter, bunyi langkah per permukaan, interaksi E (pagar, pintu terkunci, motor sementara), HUD, dan spawn di halaman depan Kos Raflesia 3.

**Architecture:**
- Generator diperluas supaya jalan, area, dan air punya collider dan komponen `GroundSurface` (diturunkan dari material yang digambar), ditambah dinding batas dunia dan gedung kunci Kos Raflesia 3.
- Assembly baru `TycoWorld.Player` berisi:
  - logika murni yang dites di EditMode;
  - satu orchestrator `FirstPersonPlayer.Tick(dt)` yang digerakkan `IPlayerIntentSource`, entah input asli atau skrip;
  - modul `IPlayerModule` untuk langkah kaki, interaksi, senter, dan HUD.
- Halaman depan kos adalah konten manual yang dihitung sekali oleh editor dari cache OSM lalu ditaruh di `World.unity`.

**Tech Stack:** Unity 6000.6.5f1, URP 17.6.0 (Forward+), Input System 1.20.0 (project-wide actions), UI Toolkit runtime, Unity Test Framework 1.8.0 (NUnit 3.5), Unity MCP (`mcp__unity-mcp__Unity_RunCommand`, `Unity_GetConsoleLogs`), PowerShell untuk git dan download.

**Spec:** `docs/superpowers/specs/2026-10-13-fps-player-design.md`

## Global Constraints

**Proyek dan versi**
- Project root: `D:\UNITY\project\Tyco World`. Branch kerja: `feature/fps-player` (sudah ada dan sudah di-checkout; spec di commit `4c8c22a`).
- Unity 6000.6.5f1, URP 17.6.0, `com.unity.inputsystem` 1.20.0, `com.unity.test-framework` 1.8.0.

**Assembly dan namespace**
- Runtime: `TycoWorld.Player` (namespace `TycoWorld.Player`).
- Editor: `TycoWorld.Player.Editor` (namespace `TycoWorld.Player.Authoring`).
- Tes: `TycoWorld.Player.Tests` (namespace `TycoWorld.Player.Tests`).
- Kode WorldGen tetap di `TycoWorld.WorldGen` / `TycoWorld.WorldGen.Authoring` / `TycoWorld.WorldGen.Tests`.
- Dependensi selalu Player → WorldGen, tidak pernah sebaliknya.

**Aturan C# dan API Unity**
- C# 9 di Unity: tidak ada `record`, setter `init`, atau operator indeks `^`.
- Locale user `id-ID`: semua parse/format angka memakai `CultureInfo.InvariantCulture`.
- Unity 6.6: API instance-ID berbasis `int` (`GetInstanceID`, `hit.colliderInstanceID`) adalah compile error. Cache memakai `Dictionary<Collider, …>`.
- Untuk mencari objek di scene, pakai `Object.FindAnyObjectByType<T>()` / `Object.FindObjectsByType<T>(FindObjectsSortMode.None)`.
- Jangan pakai `??` / `?.` / `??=` pada `UnityEngine.Object` (fake-null); pakai `if (x == null)`. Pada interface dan kelas C# biasa boleh.
- File yang punya `using System;` dan `using UnityEngine;` sekaligus menulis `UnityEngine.Object` secara eksplisit.
- Keacakan di runtime hanya lewat `NoRepeatPicker` (Task 8). Jangan pakai `UnityEngine.Random` / `System.Random`.

**Fisika dan state**
- Semua query fisika Player memakai `PlayerLayers.QueryMask` dan `QueryTriggerInteraction.Ignore` (proyek memakai `queriesHitTriggers: 1`).
- Layer pemain = layer bawaan 2 "Ignore Raycast". **Jangan mengubah ProjectSettings** (tidak ada layer/tag baru) dan jangan pernah `git add ProjectSettings` (ada perubahan App UI milik user yang belum di-commit).
- Domain reload saat Play dimatikan (`EnterPlayModeOptions: 3`). Setiap field atau event `static` di-reset lewat `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]`.
- `autoSyncTransforms: 0`: memindahkan pemain hanya lewat `FirstPersonPlayer.Teleport`. Fixture fisika di tes memanggil `Physics.SyncTransforms()` setelah membuat atau memindahkan collider.
- Komponen yang dites mengekspos `Initialize()` / `Tick(dt)`. `Update()` hanya memanggil `Tick` kalau `Application.isPlaying`. Kursor tidak pernah disentuh saat `!Application.isPlaying`.
- Fixture fisika di tes dipusatkan di `PhysicsFixture.Origin = (5000, 1000, 5000)`, di luar dunia (x −939…442, z −1366…570).

**Teks yang tampil ke pemain** (tulis persis): `"[E] Buka pagar"`, `"[E] Tutup pagar"`, `"[E] Buka pintu"`, `"Pintunya terkunci."`, `"[E] Naik motor"`, `"Belum bisa naik motor."`, `"Kos Raflesia 3"`, `"Dunia belum di-generate. Buka Tyco World > World Generator."`, `"Kamu kembali ke kos."`.

**Nilai default dan lokasi output**
- Nilai default tuning hanya ditulis sebagai initializer field di `PlayerTuning.cs` (Task 6), disalin dari tabel spec §4.
- Output generator tetap hanya di `Assets/WorldGen/Generated/`. Aset Player ada di `Assets/Player/`.

**Commit**
- Setiap commit diakhiri dua baris trailer:
  ```
  Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5
  ```

## Review Focus

1. **Play sebelum Generate, atau scene hasil generate hilang.** Pemain harus tetap beku, tidak jatuh ke void, HUD memberi tahu, dan hanya satu error yang tercatat. Dipaku oleh `PlayerSpawnerTests.WorldMissing_AfterTimeout` dan `Waiting_KeepsPlayerFrozen` (Task 14).
2. **Sambungan antar-pita jalan dan tepi pita tipis (0,06–0,10 m).** Pemain tidak boleh tersangkut, dan bunyi mendarat tidak boleh muncul hanya karena melewati tepi pita. Dipaku oleh:
   - `FirstPersonPlayerTests.Tick_StepsOntoThinRibbon` dan `Tick_WalkingOffRibbon_NoLanding` (Task 10);
   - `StepCycleTests.Landing_SmallDropIgnored` (Task 8).
3. **Sisa state static antar sesi Play** (domain reload mati): subscriber `Interactable.Interacted` dan cache `GroundProbe` yang menyimpan collider yang sudah dihancurkan. Dipaku oleh:
   - `InteractionTests.Interacted_EventCarriesId_AndResetClearsSubscribers` (Task 12);
   - `GroundProbeTests.Probe_AfterClearCache_DoesNotReturnDestroyedCollider` (Task 10).
4. **Kursor terlepas (Esc / alt-tab).** Gerakan mouse tidak boleh memutar kamera selama kursor tidak terkunci. Dipaku oleh `PlayerInputReaderTests.LookFromPointer_Unlocked_IsZero` (Task 10).
5. **Titik spawn terhalang** (pohon/lampu hasil generate, atau halaman depan yang digeser user ke dalam dinding). Pemain tidak boleh muncul di dalam collider. Dipaku oleh:
   - `PlayerSpawnerTests.Nudges_OutOfBlocker`;
   - `TryFindSpawn_AllBlocked_ReturnsFalse` (Task 14);
   - peringatan `OverlapBox` di Player Setup (Task 18).

---

## Prosedur standar (dipakai semua task)

Workspace ledger yang diabaikan git: `D:\UNITY\project\Tyco World\.superpowers\sdd\2026-10-13-fps-player\`. Folder ini dibuat di Task 1.

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

Unity lalu compile dan melakukan domain reload. Kalau panggilan MCP berikutnya gagal karena reload, ulangi panggilan itu.

### Procedure T — jalankan tes EditMode (dua assembly tes)

Panggil `mcp__unity-mcp__Unity_RunCommand` dengan kode di bawah. Ganti `Group` sesuai langkah: regex yang dicocokkan ke nama lengkap tes, dan `"."` berarti semua tes.

```csharp
using UnityEditor;

internal class CommandScript : IRunCommand
{
    const string Group = "SmokeTests";
    const string ResultPath = "D:/UNITY/project/Tyco World/.superpowers/sdd/2026-10-13-fps-player/last-test.txt";

    public void Execute(ExecutionResult result)
    {
        string text = TycoWorld.WorldGen.Tests.McpTestRunner.Run(Group, ResultPath);
        if (text.StartsWith("group=") && text.Contains(" failed=0 ")) result.Log(text);
        else result.LogError(text);
    }
}
```

Arti hasilnya:
- `STILL COMPILING`: ulangi sebentar lagi.
- `COMPILE FAILED`: baca error lewat `mcp__unity-mcp__Unity_GetConsoleLogs` (`logTypes: "error"`).
- `DIRTY SCENE: …`: simpan scene dulu.
- "Expected: COMPILE FAILED" pada langkah red berarti hasil `COMPILE FAILED`, lalu log Console menunjukkan `CS0246`/`CS0103`/`CS1061` untuk tipe atau anggota yang belum dibuat.

### Procedure C — commit

```powershell
git -C "D:\UNITY\project\Tyco World" add <path...>
git -C "D:\UNITY\project\Tyco World" commit -m @'
<subject>

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5
'@
```

- Selalu ikutkan file `.meta` yang dibuat Unity (cukup `git add` foldernya).
- Jangan pernah `git add -A`, `git add .`, atau `git add ProjectSettings`.

### Catatan snippet MCP

- Snippet `Unity_RunCommand` wajib berbentuk `internal class CommandScript : IRunCommand`.
- Tidak boleh memakai `System.Reflection`.
- Tulis `UnityEngine.Mesh` / `UnityEngine.Object` secara eksplisit.
- Compiler MCP tidak punya `System.dll`: jangan pakai `SortedDictionary`, `Regex`, atau `Stopwatch` di dalam snippet. Helper yang butuh itu ditaruh di assembly tes (pola `McpVerify`).

---

## Peta file

```
Assets/WorldGen/Runtime/SurfaceType.cs, GroundSurface.cs                        (Task 2)
Assets/WorldGen/Editor/Classify/SurfaceRules.cs                                 (Task 2)
Assets/WorldGen/Editor/Art/ArtAssetBuilder.cs                                   (Task 2: RoadMaterialName)
Assets/WorldGen/Editor/Pipeline/WorldMeshPipeline.cs                            (Task 2, 3)
Assets/WorldGen/Editor/Pipeline/SceneObjectFactory.cs                           (Task 3)
Assets/WorldGen/Editor/Builders/GroundBuilder.cs                                (Task 3)
Assets/WorldGen/Editor/Settings/DefaultSettingsFactory.cs, Config/WorldGenSettings.asset   (Task 4)
Assets/WorldGen/Tests/McpTestRunner.cs (Task 1), SurfaceRulesTests.cs (Task 2), GroundBuilderTests.cs,
    SceneObjectFactoryTests.cs (Task 3), DefaultSettingsTests.cs, WorldMeshPipelineTests.cs (Task 2–4)
Assets/InputSystem_Actions.inputactions                                         (Task 5)
Assets/Player/Runtime/TycoWorld.Player.asmdef, PlayerLayers.cs                  (Task 1)
Assets/Player/Runtime/Input/      PlayerIntent.cs IPlayerIntentSource.cs ScriptedIntentSource.cs ControlLocks.cs (Task 5)
                                  PlayerInputReader.cs (Task 10)
Assets/Player/Runtime/Tuning/     PlayerTuning.cs (Task 6)
Assets/Player/Runtime/Motion/     MovementModel.cs LookModel.cs (6) StaminaModel.cs CrouchModel.cs (7)
                                  StepCycle.cs HeadBob.cs (8)
Assets/Player/Runtime/Core/       NoRepeatPicker.cs (8) SpawnNudge.cs (9) GroundProbe.cs IPlayerModule.cs
                                  FirstPersonPlayer.cs (10) PlayerSpawnPoint.cs PlayerSpawner.cs (14)
Assets/Player/Runtime/Audio/      ISoundPlayer.cs AudioSourceSoundPlayer.cs FootstepBank.cs FootstepAudio.cs (11)
Assets/Player/Runtime/Interaction/ IPlayerFeedback.cs (9) GateSwing.cs (9) Interactable.cs InteractionContext.cs
                                  Interactor.cs GateInteractable.cs LockedDoorInteractable.cs
                                  PlaceholderVehicleInteractable.cs (12)
Assets/Player/Runtime/Equipment/  Flashlight.cs (13)
Assets/Player/Runtime/UI/         HudModel.cs (9) HudView.cs PlayerHud.cs (13)
Assets/Player/Editor/TycoWorld.Player.Editor.asmdef, PlayerPaths.cs (Task 1)
Assets/Player/Editor/             PlayerAudioMap.cs AudioImportRules.cs (15) KosSitePlanner.cs KosSiteSource.cs (16)
                                  PlayerAssetsBuilder.cs PlaceholderPropFactory.cs PlayerRigBuilder.cs (17)
                                  KosFrontyardBuilder.cs PlayerSceneSetup.cs (18)
Assets/Player/Tests/TycoWorld.Player.Tests.asmdef, PhysicsFixture.cs, SmokeTests.cs (Task 1),
    TestRig.cs RecordingSoundPlayer.cs (Task 10/11), McpPlayerDriver.cs (Task 19), *Tests.cs
Assets/Player/Audio/{Footsteps,Interactions}/*.ogg, SOURCES.md                  (Task 15)
Assets/Player/Config, UI, Materials, Prefabs                                    (Task 17)
Assets/Scenes/World.unity                                                       (Task 18)
docs/superpowers/specs/2026-10-13-osm-world-generator-design.md                 (Task 3, 4)
docs/superpowers/notes/2026-10-13-worldgen-baseline.md                          (Task 4)
docs/superpowers/notes/2026-10-13-player-verification.md                        (Task 19)
```

---
### Task 1: Scaffold assembly Player, runner dua assembly, rekam hash dasar

**Files:**
- Create: `Assets/Player/Runtime/TycoWorld.Player.asmdef`, `Assets/Player/Runtime/PlayerLayers.cs`
- Create: `Assets/Player/Editor/TycoWorld.Player.Editor.asmdef`, `Assets/Player/Editor/PlayerPaths.cs`
- Create: `Assets/Player/Tests/TycoWorld.Player.Tests.asmdef`, `Assets/Player/Tests/PhysicsFixture.cs`, `Assets/Player/Tests/PlayerSmokeTests.cs`
- Modify: `Assets/WorldGen/Tests/McpTestRunner.cs` (assemblyNames + buat folder hasil)
- Modify: `Assets/WorldGen/Tests/McpVerify.cs` (+ `WriteChunkHashes`, `CompareChunkHashes`)

**Interfaces:**
- Produces:
  - `PlayerLayers.Player` (int 2) dan `PlayerLayers.QueryMask` (int).
  - Konstanta `PlayerPaths.*`.
  - `PhysicsFixture` (`Origin`, `Box`, `Floor`, `Track`, `Sync`, `Dispose`).
  - `McpVerify.WriteChunkHashes(path)` / `CompareChunkHashes(path)`.
  - `McpTestRunner.Run` sekarang mencakup `TycoWorld.Player.Tests`.

- [ ] **Step 1: Siapkan ledger dan rekam hash chunk sebelum ada perubahan apa pun**

Buat folder ledger dan cek bahwa folder itu diabaikan git:

```powershell
New-Item -ItemType Directory -Force "D:\UNITY\project\Tyco World\.superpowers\sdd\2026-10-13-fps-player" | Out-Null
git -C "D:\UNITY\project\Tyco World" check-ignore -v ".superpowers/sdd/2026-10-13-fps-player/x.txt"
```
Expected: output memuat `.superpowers/sdd/.gitignore:1:*`.

Tambahkan dua helper ke `Assets/WorldGen/Tests/McpVerify.cs`, lalu tulis ulang seluruh file menjadi:

```csharp
using System.Collections.Generic;
using System.IO;
using TycoWorld.WorldGen.Authoring;

namespace TycoWorld.WorldGen.Tests
{
    /// <summary>Dev helper for Unity MCP verification scripts, whose compiler cannot reference System.dll types such as SortedDictionary.</summary>
    public static class McpVerify
    {
        public static Dictionary<string, string> LastChunkHashes()
        {
            var result = new Dictionary<string, string>();
            var report = WorldGenerationRunner.LastReport;
            if (report == null) return result;
            foreach (var kv in report.ChunkHashes) result[kv.Key] = kv.Value;
            return result;
        }

        /// <summary>Writes "Chunk_x_z=hash" lines (ordinal order) of the last Generate to <paramref name="path"/>.</summary>
        public static string WriteChunkHashes(string path)
        {
            var report = WorldGenerationRunner.LastReport;
            if (report == null) return "NO REPORT";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var lines = new List<string>();
            foreach (var kv in report.ChunkHashes) lines.Add(kv.Key + "=" + kv.Value);
            File.WriteAllLines(path, lines);
            return "chunks=" + lines.Count;
        }

        /// <summary>Compares the last Generate's chunk hashes with a file written by <see cref="WriteChunkHashes"/>.</summary>
        public static string CompareChunkHashes(string path)
        {
            var report = WorldGenerationRunner.LastReport;
            if (report == null) return "NO REPORT";
            var before = new Dictionary<string, string>();
            foreach (var line in File.ReadAllLines(path))
            {
                int eq = line.IndexOf('=');
                if (eq > 0) before[line.Substring(0, eq)] = line.Substring(eq + 1);
            }
            var diffs = new List<string>();
            foreach (var kv in before)
            {
                if (!report.ChunkHashes.TryGetValue(kv.Key, out var now)) diffs.Add("missing " + kv.Key);
                else if (now != kv.Value) diffs.Add("changed " + kv.Key);
            }
            foreach (var kv in report.ChunkHashes)
                if (!before.ContainsKey(kv.Key)) diffs.Add("new " + kv.Key);
            return diffs.Count == 0 ? "IDENTICAL chunks=" + before.Count : "DIFF " + string.Join(", ", diffs);
        }
    }
}
```

Lalu jalankan Procedure R. Setelah compile selesai, jalankan snippet MCP ini (Generate dengan kode **sebelum** perubahan WorldGen apa pun):

```csharp
using TycoWorld.WorldGen.Authoring;
using UnityEditor;

internal class CommandScript : IRunCommand
{
    const string Dir = "D:/UNITY/project/Tyco World/.superpowers/sdd/2026-10-13-fps-player";

    public void Execute(ExecutionResult result)
    {
        var settings = AssetDatabase.LoadAssetAtPath<WorldGenSettings>(WorldGenPaths.SettingsAsset);
        if (!WorldGenerationRunner.Generate(settings, false, out string error)) { result.LogError(error); return; }
        result.Log(TycoWorld.WorldGen.Tests.McpVerify.WriteChunkHashes(Dir + "/hashes_before.txt"));
        System.IO.File.WriteAllText(Dir + "/report_before.txt", WorldGenerationRunner.LastReport.Summary());
        result.Log(WorldGenerationRunner.LastReport.Summary());
    }
}
```
Expected:
- log `chunks=278`;
- ringkasan memuat `KeyBuilding=5` dan `Building=4916`.

Kalau Generate menolak karena scene belum disimpan, simpan dulu scene yang terbuka (`EditorSceneManager.SaveOpenScenes()`) lalu ulangi.

- [ ] **Step 2: Tulis tes smoke yang gagal**

`Assets/Player/Tests/TycoWorld.Player.Tests.asmdef`:
```json
{
    "name": "TycoWorld.Player.Tests",
    "rootNamespace": "TycoWorld.Player.Tests",
    "references": [
        "TycoWorld.Player",
        "TycoWorld.Player.Editor",
        "TycoWorld.WorldGen",
        "TycoWorld.WorldGen.Editor",
        "Unity.InputSystem",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner",
        "Unity.RenderPipelines.Core.Runtime",
        "Unity.RenderPipelines.GPUDriven.Runtime",
        "Unity.RenderPipelines.Universal.Runtime"
    ],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": ["nunit.framework.dll"],
    "autoReferenced": false,
    "defineConstraints": ["UNITY_INCLUDE_TESTS"],
    "versionDefines": [],
    "noEngineReferences": false
}
```

`Assets/Player/Tests/PhysicsFixture.cs`:
```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.Player.Tests
{
    /// <summary>Tracks test GameObjects far outside the generated world and destroys them on Dispose.</summary>
    internal sealed class PhysicsFixture : IDisposable
    {
        public static readonly Vector3 Origin = new Vector3(5000f, 1000f, 5000f);
        readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

        public T Track<T>(T obj) where T : UnityEngine.Object
        {
            created.Add(obj);
            return obj;
        }

        /// <summary>Box collider centred at Origin + localCenter.</summary>
        public GameObject Box(string name, Vector3 localCenter, Vector3 size)
        {
            var go = Track(new GameObject(name));
            go.transform.position = Origin + localCenter;
            go.AddComponent<BoxCollider>().size = size;
            return go;
        }

        /// <summary>1 m thick square floor whose top face is at Origin.y + topY.</summary>
        public GameObject Floor(float topY = 0f, float size = 100f) =>
            Box("Floor", new Vector3(0f, topY - 0.5f, 0f), new Vector3(size, 1f, size));

        public void Sync() => Physics.SyncTransforms();

        public void Dispose()
        {
            for (int i = created.Count - 1; i >= 0; i--)
                if (created[i] != null) UnityEngine.Object.DestroyImmediate(created[i]);
            created.Clear();
        }
    }
}
```

`Assets/Player/Tests/PlayerSmokeTests.cs`:
```csharp
using NUnit.Framework;
using TycoWorld.Player.Authoring;
using UnityEngine;

namespace TycoWorld.Player.Tests
{
    public class PlayerSmokeTests
    {
        [Test]
        public void Paths_AreUnderPlayerRoot()
        {
            Assert.AreEqual("Assets/Player", PlayerPaths.Root);
            StringAssert.StartsWith(PlayerPaths.Root + "/", PlayerPaths.PlayerPrefab);
            StringAssert.StartsWith(PlayerPaths.Root + "/", PlayerPaths.FootstepAudioFolder);
            Assert.AreEqual("Assets/InputSystem_Actions.inputactions", PlayerPaths.InputActionsAsset);
        }

        [Test]
        public void InputSystem_IsReferenced() => Assert.IsNotNull(typeof(UnityEngine.InputSystem.InputAction));

        [Test]
        public void PlayerLayer_IsBuiltInIgnoreRaycast()
        {
            Assert.AreEqual(LayerMask.NameToLayer("Ignore Raycast"), PlayerLayers.Player);
            Assert.AreEqual(0, PlayerLayers.QueryMask & (1 << PlayerLayers.Player));
            Assert.AreNotEqual(0, PlayerLayers.QueryMask & 1);
        }

        [Test]
        public void PhysicsFixture_RaycastHitsFloorTop()
        {
            using (var fx = new PhysicsFixture())
            {
                fx.Floor(0.3f);
                fx.Sync();
                bool hit = Physics.Raycast(PhysicsFixture.Origin + Vector3.up * 3f, Vector3.down, out var info, 10f,
                    PlayerLayers.QueryMask, QueryTriggerInteraction.Ignore);
                Assert.IsTrue(hit);
                Assert.AreEqual(PhysicsFixture.Origin.y + 0.3f, info.point.y, 1e-3f);
            }
        }
    }
}
```

- [ ] **Step 3: Jalankan tes, pastikan gagal**

Procedure R, lalu Procedure T dengan `Group = "PlayerSmokeTests"`.
Expected: COMPILE FAILED, karena asmdef `TycoWorld.Player` / `TycoWorld.Player.Editor` belum ada (`CS0246 PlayerPaths`, `PlayerLayers`).

- [ ] **Step 4: Buat assembly dan kode minimal**

`Assets/Player/Runtime/TycoWorld.Player.asmdef`:
```json
{
    "name": "TycoWorld.Player",
    "rootNamespace": "TycoWorld.Player",
    "references": ["TycoWorld.WorldGen", "Unity.InputSystem"],
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

`Assets/Player/Runtime/PlayerLayers.cs`:
```csharp
namespace TycoWorld.Player
{
    /// <summary>The player lives on the built-in "Ignore Raycast" layer so its own queries never hit itself.</summary>
    public static class PlayerLayers
    {
        public const int Player = 2;

        /// <summary>Layer mask for every player query (ground, headroom, interaction, spawn).</summary>
        public static int QueryMask => ~(1 << Player);
    }
}
```

`Assets/Player/Editor/TycoWorld.Player.Editor.asmdef`:
```json
{
    "name": "TycoWorld.Player.Editor",
    "rootNamespace": "TycoWorld.Player.Authoring",
    "references": [
        "TycoWorld.Player",
        "TycoWorld.WorldGen",
        "TycoWorld.WorldGen.Editor",
        "Unity.InputSystem",
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

`Assets/Player/Editor/PlayerPaths.cs`:
```csharp
namespace TycoWorld.Player.Authoring
{
    /// <summary>Project-relative paths of the player sub-project.</summary>
    public static class PlayerPaths
    {
        public const string Root = "Assets/Player";
        public const string ConfigFolder = Root + "/Config";
        public const string TuningAsset = ConfigFolder + "/PlayerTuning.asset";
        public const string FootstepBankAsset = ConfigFolder + "/FootstepBank.asset";
        public const string AudioFolder = Root + "/Audio";
        public const string FootstepAudioFolder = AudioFolder + "/Footsteps";
        public const string InteractionAudioFolder = AudioFolder + "/Interactions";
        public const string AudioSourcesFile = AudioFolder + "/SOURCES.md";
        public const string UiFolder = Root + "/UI";
        public const string PanelSettingsAsset = UiFolder + "/PlayerHudPanel.asset";
        public const string ThemeAsset = UiFolder + "/PlayerHudTheme.tss";
        public const string MaterialsFolder = Root + "/Materials";
        public const string PrefabsFolder = Root + "/Prefabs";
        public const string MeshesFolder = PrefabsFolder + "/Meshes";
        public const string PlayerPrefab = PrefabsFolder + "/Player.prefab";
        public const string GatePrefab = PrefabsFolder + "/Gate.prefab";
        public const string DoorPrefab = PrefabsFolder + "/KosDoor.prefab";
        public const string MotorPrefab = PrefabsFolder + "/Motor_Placeholder.prefab";
        public const string InputActionsAsset = "Assets/InputSystem_Actions.inputactions";
    }
}
```

Ubah `Assets/WorldGen/Tests/McpTestRunner.cs`:
- ringkasan kelas menjadi `/// <summary>Dev helper: runs the project's EditMode test assemblies synchronously (Procedure T, invoked through Unity MCP).</summary>`;
- isi filter menjadi:
```csharp
                var filter = new Filter
                {
                    testMode = TestMode.EditMode,
                    assemblyNames = new[] { "TycoWorld.WorldGen.Tests", "TycoWorld.Player.Tests" },
                    groupNames = new[] { group }
                };
```
- penulisan file hasil menjadi:
```csharp
            if (!string.IsNullOrEmpty(resultPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(resultPath));
                File.WriteAllText(resultPath, text);
            }
```

- [ ] **Step 5: Jalankan tes, pastikan lulus**

Procedure R, lalu Procedure T dengan `Group = "PlayerSmokeTests"`.
Expected: `group=PlayerSmokeTests passed=4 failed=0 skipped=0`.

Lalu Procedure T dengan `Group = "."`.
Expected: `passed=227 failed=0` (223 lama + 4 baru).

- [ ] **Step 6: Commit**

Procedure C:
- path: `Assets/Player` `Assets/WorldGen/Tests/McpTestRunner.cs` `Assets/WorldGen/Tests/McpVerify.cs`
- subject: `chore(player): scaffold player assemblies and multi-assembly test runner`

---

### Task 2: Klasifikasi permukaan pijak (WorldGen)

**Files:**
- Create: `Assets/WorldGen/Runtime/SurfaceType.cs`, `Assets/WorldGen/Runtime/GroundSurface.cs`
- Create: `Assets/WorldGen/Editor/Classify/SurfaceRules.cs`
- Modify: `Assets/WorldGen/Editor/Art/ArtAssetBuilder.cs` (ekstrak `RoadMaterialName`)
- Modify: `Assets/WorldGen/Editor/Pipeline/WorldMeshPipeline.cs` (`BuiltFeature.Surface`, isi di `Build`)
- Test: `Assets/WorldGen/Tests/SurfaceRulesTests.cs`, `Assets/WorldGen/Tests/WorldMeshPipelineTests.cs`

**Interfaces:**
- Produces:
  - `enum SurfaceType { Default, Asphalt, Paving, Concrete, Grass, Dirt, Water }` (namespace `TycoWorld.WorldGen`).
  - `GroundSurface : MonoBehaviour` dengan `Type` dan `Initialize(SurfaceType)`.
  - `SurfaceRules.Ground`, `ForMaterialName(string)`, `ForRoad(string)`, `ForArea(AreaCategory)`, `For(BuiltFeature)`.
  - `ArtAssetBuilder.RoadMaterialName(string)`.
  - `BuiltFeature.Surface`.

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/WorldGen/Tests/SurfaceRulesTests.cs`:
```csharp
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class SurfaceRulesTests
    {
        [TestCase("Road_Asphalt", SurfaceType.Asphalt)]
        [TestCase("Path_Paving", SurfaceType.Paving)]
        [TestCase("Area_Campus", SurfaceType.Paving)]
        [TestCase("Area_Parking", SurfaceType.Concrete)]
        [TestCase("Area_Hardground", SurfaceType.Concrete)]
        [TestCase("Area_Grass", SurfaceType.Grass)]
        [TestCase("Area_Farmland", SurfaceType.Grass)]
        [TestCase("Area_Pitch", SurfaceType.Grass)]
        [TestCase("Ground_Dirt", SurfaceType.Dirt)]
        [TestCase("Area_Cemetery", SurfaceType.Dirt)]
        [TestCase("Water", SurfaceType.Water)]
        [TestCase("M_Res_Ground_KosDoor", SurfaceType.Default)]
        [TestCase("", SurfaceType.Default)]
        public void ForMaterialName_MapsDrawnMaterial(string name, SurfaceType expected)
        {
            Assert.AreEqual(expected, SurfaceRules.ForMaterialName(name));
        }

        [TestCase("primary", SurfaceType.Asphalt)]
        [TestCase("residential", SurfaceType.Asphalt)]
        [TestCase("living_street", SurfaceType.Asphalt)]
        [TestCase("service", SurfaceType.Asphalt)]
        [TestCase("footway", SurfaceType.Paving)]
        [TestCase("pedestrian", SurfaceType.Paving)]
        [TestCase("steps", SurfaceType.Paving)]
        [TestCase("track", SurfaceType.Dirt)]
        public void ForRoad_FollowsRoadMaterial(string highway, SurfaceType expected)
        {
            Assert.AreEqual(expected, SurfaceRules.ForRoad(highway));
            Assert.AreEqual(SurfaceRules.ForMaterialName(ArtAssetBuilder.RoadMaterialName(highway)), SurfaceRules.ForRoad(highway));
        }

        [TestCase(AreaCategory.Grass, SurfaceType.Grass)]
        [TestCase(AreaCategory.Wood, SurfaceType.Grass)]
        [TestCase(AreaCategory.Farmland, SurfaceType.Grass)]
        [TestCase(AreaCategory.Pitch, SurfaceType.Grass)]
        [TestCase(AreaCategory.Parking, SurfaceType.Concrete)]
        [TestCase(AreaCategory.Hardground, SurfaceType.Concrete)]
        [TestCase(AreaCategory.Campus, SurfaceType.Paving)]
        [TestCase(AreaCategory.Paving, SurfaceType.Paving)]
        [TestCase(AreaCategory.Cemetery, SurfaceType.Dirt)]
        public void ForArea_FollowsAreaMaterial(AreaCategory category, SurfaceType expected)
        {
            Assert.AreEqual(expected, SurfaceRules.ForArea(category));
        }

        [Test]
        public void EveryDefaultRule_IsWalkable()
        {
            var s = DefaultSettingsFactory.CreateInMemory();
            try
            {
                foreach (var road in s.roadRules) Assert.AreNotEqual(SurfaceType.Default, SurfaceRules.ForRoad(road.highway), road.highway);
                foreach (var area in s.areaRules) Assert.AreNotEqual(SurfaceType.Default, SurfaceRules.ForArea(area.category), area.name);
            }
            finally
            {
                Object.DestroyImmediate(s);
            }
        }

        [Test]
        public void For_PrefersDrawnMaterialOverCategory()
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Simple Lit")) { name = "Area_Parking" };
            try
            {
                var feature = new BuiltFeature { Kind = FeatureKind.Area, Category = "Grass", Materials = new[] { material } };
                Assert.AreEqual(SurfaceType.Concrete, SurfaceRules.For(feature));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void For_FallsBackToKindAndCategory()
        {
            Assert.AreEqual(SurfaceType.Asphalt, SurfaceRules.For(new BuiltFeature { Kind = FeatureKind.Road, Category = "residential" }));
            Assert.AreEqual(SurfaceType.Grass, SurfaceRules.For(new BuiltFeature { Kind = FeatureKind.Area, Category = "Grass" }));
            Assert.AreEqual(SurfaceType.Water, SurfaceRules.For(new BuiltFeature { Kind = FeatureKind.WaterArea }));
            Assert.AreEqual(SurfaceType.Water, SurfaceRules.For(new BuiltFeature { Kind = FeatureKind.WaterLine }));
            Assert.AreEqual(SurfaceType.Default, SurfaceRules.For(new BuiltFeature { Kind = FeatureKind.Building }));
            Assert.AreEqual(SurfaceType.Default, SurfaceRules.For(new BuiltFeature { Kind = FeatureKind.Area, Category = "Unknown" }));
        }

        [Test]
        public void Ground_IsDirt() => Assert.AreEqual(SurfaceType.Dirt, SurfaceRules.Ground);

        [Test]
        public void GroundSurface_StoresType()
        {
            var go = new GameObject("surface");
            try
            {
                var surface = go.AddComponent<GroundSurface>();
                Assert.AreEqual(SurfaceType.Default, surface.Type);
                surface.Initialize(SurfaceType.Grass);
                Assert.AreEqual(SurfaceType.Grass, surface.Type);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
```

Tambahkan ke `Assets/WorldGen/Tests/WorldMeshPipelineTests.cs`, di dalam kelas setelah `Synthetic_BuildsAllKinds`:
```csharp
        [Test]
        public void Synthetic_SurfacesAssigned()
        {
            var built = Build("synthetic_world.json", new GenerationReport());
            Assert.AreEqual(SurfaceType.Asphalt, built.Features.Single(f => f.Kind == FeatureKind.Road && f.OsmId == 402).Surface);
            Assert.AreEqual(SurfaceType.Grass, built.Features.Single(f => f.Kind == FeatureKind.Area && f.OsmId == 404).Surface);
            Assert.AreEqual(SurfaceType.Paving, built.Features.Single(f => f.Kind == FeatureKind.Area && f.OsmId == 405).Surface);
            Assert.AreEqual(SurfaceType.Default, built.Features.Single(f => f.Kind == FeatureKind.Building && f.OsmId == 401).Surface);
        }
```

- [ ] **Step 2: Jalankan tes, pastikan gagal**

Procedure R, lalu Procedure T dengan `Group = "SurfaceRulesTests|Synthetic_SurfacesAssigned"`.
Expected: COMPILE FAILED (`CS0246 SurfaceType`, `SurfaceRules`, `GroundSurface`; `CS0117 RoadMaterialName`; `CS1061 Surface`).

- [ ] **Step 3: Implementasi**

`Assets/WorldGen/Runtime/SurfaceType.cs`:
```csharp
namespace TycoWorld.WorldGen
{
    /// <summary>Walkable surface of a generated feature, derived from the material it is drawn with.</summary>
    public enum SurfaceType { Default, Asphalt, Paving, Concrete, Grass, Dirt, Water }
}
```

`Assets/WorldGen/Runtime/GroundSurface.cs`:
```csharp
using UnityEngine;

namespace TycoWorld.WorldGen
{
    /// <summary>Tells footsteps (and later vehicles) what a walkable collider is made of.</summary>
    [DisallowMultipleComponent]
    public sealed class GroundSurface : MonoBehaviour
    {
        [SerializeField] SurfaceType type;

        public SurfaceType Type => type;

        public void Initialize(SurfaceType surface) => type = surface;
    }
}
```

Di `Assets/WorldGen/Editor/Art/ArtAssetBuilder.cs`, tepat setelah method `AreaMaterialName`, tambahkan:
```csharp
        public static string RoadMaterialName(string highway) =>
            highway is "footway" or "path" or "cycleway" or "steps" or "pedestrian" ? "Path_Paving"
            : highway == "track" ? "Ground_Dirt" : "Road_Asphalt";
```
Lalu ganti tiga baris pertama di `Assign` ini:
```csharp
            foreach (var rule in s.roadRules)
                rule.material = rule.highway is "footway" or "path" or "cycleway" or "steps" or "pedestrian" ? m["Path_Paving"]
                    : rule.highway == "track" ? m["Ground_Dirt"] : m["Road_Asphalt"];
```
dengan:
```csharp
            foreach (var rule in s.roadRules) rule.material = m[RoadMaterialName(rule.highway)];
```

`Assets/WorldGen/Editor/Classify/SurfaceRules.cs`:
```csharp
using System;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Maps what the generator draws to the walkable surface it sounds like (spec #2 §5).</summary>
    public static class SurfaceRules
    {
        public const SurfaceType Ground = SurfaceType.Dirt;

        public static SurfaceType ForMaterialName(string name)
        {
            switch (name)
            {
                case "Road_Asphalt": return SurfaceType.Asphalt;
                case "Path_Paving":
                case "Area_Campus": return SurfaceType.Paving;
                case "Area_Parking":
                case "Area_Hardground": return SurfaceType.Concrete;
                case "Area_Grass":
                case "Area_Farmland":
                case "Area_Pitch": return SurfaceType.Grass;
                case "Ground_Dirt":
                case "Area_Cemetery": return SurfaceType.Dirt;
                case "Water": return SurfaceType.Water;
                default: return SurfaceType.Default;
            }
        }

        public static SurfaceType ForRoad(string highway) => ForMaterialName(ArtAssetBuilder.RoadMaterialName(highway));

        public static SurfaceType ForArea(AreaCategory category) => ForMaterialName(ArtAssetBuilder.AreaMaterialName(category));

        /// <summary>Drawn material first; when it is unknown or missing, the feature kind and category decide.</summary>
        public static SurfaceType For(BuiltFeature feature)
        {
            if (feature.Materials != null && feature.Materials.Length > 0 && feature.Materials[0] != null)
            {
                var byMaterial = ForMaterialName(feature.Materials[0].name);
                if (byMaterial != SurfaceType.Default) return byMaterial;
            }
            switch (feature.Kind)
            {
                case FeatureKind.Road: return ForRoad(feature.Category);
                case FeatureKind.Area:
                    return Enum.TryParse(feature.Category, out AreaCategory category) ? ForArea(category) : SurfaceType.Default;
                case FeatureKind.WaterArea:
                case FeatureKind.WaterLine: return SurfaceType.Water;
                default: return SurfaceType.Default;
            }
        }
    }
}
```

Di `Assets/WorldGen/Editor/Pipeline/WorldMeshPipeline.cs`:
- tambahkan field di kelas `BuiltFeature`, setelah `public bool AddCollider;`:
```csharp
        public SurfaceType Surface;
```
- di `Build`, ganti
```csharp
            AddWater(world, settings, built, report);

```
dengan
```csharp
            AddWater(world, settings, built, report);
            foreach (var feature in built.Features) feature.Surface = SurfaceRules.For(feature);

```

- [ ] **Step 4: Jalankan tes, pastikan lulus**

Procedure R, lalu Procedure T dengan `Group = "SurfaceRulesTests|Synthetic_SurfacesAssigned"`.
Expected: `passed=36 failed=0`.

Lalu Procedure T dengan `Group = "."`.
Expected: `failed=0`.

- [ ] **Step 5: Commit**

Procedure C:
- path: `Assets/WorldGen/Runtime` `Assets/WorldGen/Editor/Classify/SurfaceRules.cs` `Assets/WorldGen/Editor/Classify/SurfaceRules.cs.meta` `Assets/WorldGen/Editor/Art/ArtAssetBuilder.cs` `Assets/WorldGen/Editor/Pipeline/WorldMeshPipeline.cs` `Assets/WorldGen/Tests`
- subject: `feat(worldgen): classify walkable surface per feature`

---

### Task 3: Collider permukaan, `GroundSurface` di scene, dinding batas dunia (WorldGen)

**Files:**
- Modify: `Assets/WorldGen/Editor/Pipeline/WorldMeshPipeline.cs` (`AddCollider = true` di AddRoads/AddAreas/AddWater)
- Modify: `Assets/WorldGen/Editor/Builders/GroundBuilder.cs` (+ `WallBox`, `EdgeWalls`, konstanta)
- Modify: `Assets/WorldGen/Editor/Pipeline/SceneObjectFactory.cs` (+ `AddEdgeWalls`, `ApplySurface`, dipanggil di `Populate`)
- Modify: `docs/superpowers/specs/2026-10-13-osm-world-generator-design.md` (§7, §7.1, §7.3, §7.4, §11)
- Test: `Assets/WorldGen/Tests/GroundBuilderTests.cs`, `Assets/WorldGen/Tests/SceneObjectFactoryTests.cs`, `Assets/WorldGen/Tests/WorldMeshPipelineTests.cs`

**Interfaces:**
- Consumes: `BuiltFeature.Surface`, `SurfaceRules.Ground`, `GroundSurface` (Task 2).
- Produces:
  - `GroundBuilder.WallBox { Center, Size }`.
  - `GroundBuilder.EdgeWalls(Rect, float height, float thickness)`: urutan West, East, South, North.
  - `GroundBuilder.EdgeWallHeight` (30) dan `EdgeWallThickness` (2).
  - `SceneObjectFactory.AddEdgeWalls(Transform, Rect)` dan `SceneObjectFactory.ApplySurface(GameObject, SurfaceType)`.
  - Scene hasil generate punya `World_Generated/WorldEdge/Edge_{West,East,South,North}`.

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/WorldGen/Tests/GroundBuilderTests.cs`:
```csharp
using System.Linq;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class GroundBuilderTests
    {
        [Test]
        public void ColliderBox_TopFaceAtZero()
        {
            GroundBuilder.ColliderBox(new Rect(-10f, -20f, 30f, 50f), out var center, out var size);
            Assert.AreEqual(0f, center.y + size.y * 0.5f, 1e-5f);
        }

        [Test]
        public void EdgeWalls_InnerFacesOnBounds()
        {
            var r = new Rect(-10f, -20f, 30f, 50f);
            var w = GroundBuilder.EdgeWalls(r, 30f, 2f);
            Assert.AreEqual(4, w.Length);
            Assert.AreEqual(r.xMin, w[0].Center.x + w[0].Size.x * 0.5f, 1e-4f, "west");
            Assert.AreEqual(r.xMax, w[1].Center.x - w[1].Size.x * 0.5f, 1e-4f, "east");
            Assert.AreEqual(r.yMin, w[2].Center.z + w[2].Size.z * 0.5f, 1e-4f, "south");
            Assert.AreEqual(r.yMax, w[3].Center.z - w[3].Size.z * 0.5f, 1e-4f, "north");
            foreach (var b in w)
            {
                Assert.AreEqual(-1f, b.Center.y - b.Size.y * 0.5f, 1e-4f);
                Assert.AreEqual(30f, b.Center.y + b.Size.y * 0.5f, 1e-4f);
            }
        }

        [Test]
        public void EdgeWalls_CoverCorners()
        {
            var r = new Rect(0f, 0f, 10f, 10f);
            var w = GroundBuilder.EdgeWalls(r, 30f, 2f);
            var corners = new[] { new Vector3(-0.5f, 1f, -0.5f), new Vector3(10.5f, 1f, -0.5f), new Vector3(10.5f, 1f, 10.5f), new Vector3(-0.5f, 1f, 10.5f) };
            foreach (var corner in corners)
                Assert.IsTrue(w.Any(b => new Bounds(b.Center, b.Size).Contains(corner)), corner.ToString());
        }
    }
}
```

`Assets/WorldGen/Tests/SceneObjectFactoryTests.cs`:
```csharp
using System.Collections.Generic;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class SceneObjectFactoryTests
    {
        readonly List<GameObject> created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created) if (go != null) Object.DestroyImmediate(go);
            created.Clear();
        }

        GameObject Make(string name)
        {
            var go = new GameObject(name);
            created.Add(go);
            return go;
        }

        [Test]
        public void AddEdgeWalls_CreatesFourStaticInvisibleBoxes()
        {
            var parent = Make("WorldEdge").transform;
            SceneObjectFactory.AddEdgeWalls(parent, new Rect(0f, 0f, 100f, 50f));
            Assert.AreEqual(4, parent.childCount);
            CollectionAssert.AreEquivalent(new[] { "Edge_West", "Edge_East", "Edge_South", "Edge_North" },
                new[] { parent.GetChild(0).name, parent.GetChild(1).name, parent.GetChild(2).name, parent.GetChild(3).name });
            foreach (Transform child in parent)
            {
                Assert.IsNotNull(child.GetComponent<BoxCollider>());
                Assert.IsNull(child.GetComponent<MeshRenderer>());
                Assert.IsTrue(GameObjectUtility.AreStaticEditorFlagsSet(child.gameObject, StaticEditorFlags.OccludeeStatic));
            }
            var west = parent.Find("Edge_West").GetComponent<BoxCollider>();
            Assert.AreEqual(0f, west.center.x + west.size.x * 0.5f, 1e-4f);
        }

        [Test]
        public void ApplySurface_DefaultAddsNothing()
        {
            var go = Make("building");
            SceneObjectFactory.ApplySurface(go, SurfaceType.Default);
            Assert.IsNull(go.GetComponent<GroundSurface>());
        }

        [Test]
        public void ApplySurface_AddsOrUpdatesComponent()
        {
            var go = Make("road");
            SceneObjectFactory.ApplySurface(go, SurfaceType.Asphalt);
            Assert.AreEqual(SurfaceType.Asphalt, go.GetComponent<GroundSurface>().Type);
            SceneObjectFactory.ApplySurface(go, SurfaceType.Paving);
            Assert.AreEqual(1, go.GetComponents<GroundSurface>().Length);
            Assert.AreEqual(SurfaceType.Paving, go.GetComponent<GroundSurface>().Type);
        }
    }
}
```

Tambahkan ke `Assets/WorldGen/Tests/WorldMeshPipelineTests.cs`, setelah `Synthetic_SurfacesAssigned`:
```csharp
        [Test]
        public void Synthetic_WalkableFeaturesHaveColliders()
        {
            var built = Build("synthetic_world.json", new GenerationReport());
            Assert.Greater(built.Features.Count, 0);
            foreach (var f in built.Features.Where(f => f.Mesh != null))
                Assert.IsTrue(f.AddCollider, f.Kind + " " + f.OsmId);
        }
```

- [ ] **Step 2: Jalankan tes, pastikan gagal**

Procedure R, lalu Procedure T dengan `Group = "GroundBuilderTests|SceneObjectFactoryTests|Synthetic_WalkableFeaturesHaveColliders"`.
Expected: COMPILE FAILED (`CS0117 EdgeWalls`, `AddEdgeWalls`, `ApplySurface`).

- [ ] **Step 3: Implementasi**

`Assets/WorldGen/Editor/Builders/GroundBuilder.cs`, seluruh isi:
```csharp
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Single ground quad covering the area; its 1 m thick box collider has its top face at y = 0.</summary>
    public static class GroundBuilder
    {
        public const float EdgeWallHeight = 30f;
        public const float EdgeWallThickness = 2f;

        public struct WallBox
        {
            public Vector3 Center;
            public Vector3 Size;
        }

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

        /// <summary>Four boxes (West, East, South, North) whose inner faces lie exactly on the bounds, from y = -1 to <paramref name="height"/>.</summary>
        public static WallBox[] EdgeWalls(Rect bounds, float height, float thickness)
        {
            float cy = (height - 1f) * 0.5f, sy = height + 1f, half = thickness * 0.5f;
            float spanX = bounds.width + 2f * thickness, spanZ = bounds.height + 2f * thickness;
            return new[]
            {
                new WallBox { Center = new Vector3(bounds.xMin - half, cy, bounds.center.y), Size = new Vector3(thickness, sy, spanZ) },
                new WallBox { Center = new Vector3(bounds.xMax + half, cy, bounds.center.y), Size = new Vector3(thickness, sy, spanZ) },
                new WallBox { Center = new Vector3(bounds.center.x, cy, bounds.yMin - half), Size = new Vector3(spanX, sy, thickness) },
                new WallBox { Center = new Vector3(bounds.center.x, cy, bounds.yMax + half), Size = new Vector3(spanX, sy, thickness) }
            };
        }
    }
}
```

Di `Assets/WorldGen/Editor/Pipeline/WorldMeshPipeline.cs`, tambahkan `AddCollider = true` pada **empat** inisialisasi `new BuiltFeature` di `AddRoads`, `AddAreas`, dan `AddWater` (area air dan garis air). Contoh untuk `AddRoads`:
```csharp
                    Mesh = mesh, Materials = new[] { road.Rule.material }, Category = road.Highway, Tags = road.Tags,
                    AddCollider = true
```
Pola yang sama berlaku untuk `AddAreas` (`..., Tags = area.Tags, AddCollider = true`), area air (`..., Tags = water.Tags, AddCollider = true`), dan garis air (`..., Tags = line.Tags, AddCollider = true`).

Di `Assets/WorldGen/Editor/Pipeline/SceneObjectFactory.cs`:
- Setelah `GameObjectUtility.SetStaticEditorFlags(ground, Flags);` (blok Ground), tambahkan:
```csharp
            SetSurface(ground, SurfaceRules.Ground);
            AddEdgeWalls(Container("WorldEdge"), world.Bounds);
```
- Setelah `osm.Initialize(feature.OsmId, feature.ElementType, feature.Kind, feature.Name ?? "", feature.Tags);`, tambahkan:
```csharp
                ApplySurface(go, feature.Surface);
```
- Tambahkan method publik ini sebelum `static string ObjectName(BuiltFeature f)`:
```csharp
        public static void AddEdgeWalls(Transform parent, Rect bounds)
        {
            string[] names = { "Edge_West", "Edge_East", "Edge_South", "Edge_North" };
            var walls = GroundBuilder.EdgeWalls(bounds, GroundBuilder.EdgeWallHeight, GroundBuilder.EdgeWallThickness);
            for (int i = 0; i < walls.Length; i++)
            {
                var wall = new GameObject(names[i]);
                wall.transform.SetParent(parent, false);
                var box = wall.AddComponent<BoxCollider>();
                box.center = walls[i].Center;
                box.size = walls[i].Size;
                GameObjectUtility.SetStaticEditorFlags(wall, Flags);
            }
        }

        /// <summary>Adds or updates <see cref="GroundSurface"/>; Default means "not a walkable surface" and adds nothing.</summary>
        public static void ApplySurface(GameObject go, SurfaceType surface)
        {
            if (surface == SurfaceType.Default) return;
            SetSurface(go, surface);
        }

        static void SetSurface(GameObject go, SurfaceType surface)
        {
            var component = go.GetComponent<GroundSurface>();
            if (component == null) component = go.AddComponent<GroundSurface>();
            component.Initialize(surface);
        }
```

Ubah `docs/superpowers/specs/2026-10-13-osm-world-generator-design.md`:
- Ganti paragraf "Satu-satunya permukaan yang bisa dipijak adalah collider ground. Selisih maksimal 0.1 m antara collider dan permukaan visual tidak terlihat dari kamera orang pertama." dengan:
  "Ground, jalan, area, dan air punya collider (lihat spec `2026-10-13-fps-player-design.md` §5). Permukaan yang lebih tinggi digambar di atas dan juga dipijak lebih dulu, jadi raycast ke bawah selalu mengenai permukaan yang terlihat. Setiap collider permukaan membawa `GroundSurface` (jenis permukaan dari material)."
- §7.1: ganti `- Jalan tidak punya collider.` dengan `- Jalan punya MeshCollider (non-convex, static) dan GroundSurface (Asphalt/Paving/Dirt).`
- §7.3: ganti `Area tidak punya collider.` dengan `Area punya MeshCollider dan GroundSurface.`, dan `- Air hanya visual, tanpa collider maupun kedalaman.` dengan `- Air punya MeshCollider datar dan GroundSurface Water, tanpa kedalaman.`
- §7.4: tambahkan butir `- GroundSurface Dirt. Empat BoxCollider tak terlihat (WorldEdge, setinggi 30 m, tebal 2 m) memagari batas area.`
- Hierarki §11:
  - `Roads/…`, `Areas/…`, dan `Water/…` jadi `[OsmFeature, GroundSurface, MeshCollider]`;
  - tambahkan baris `  WorldEdge/Edge_West|East|South|North                         [BoxCollider]` di bawah `Ground`.

- [ ] **Step 4: Jalankan tes, pastikan lulus**

Procedure R, lalu Procedure T dengan `Group = "GroundBuilderTests|SceneObjectFactoryTests|Synthetic_WalkableFeaturesHaveColliders"`.
Expected: `passed=7 failed=0`.

Lalu `Group = "."`.
Expected: `failed=0`. Tes lama `WorldMeshPipelineTests` untuk gedung kunci ber-prefab tetap lulus, karena fitur itu tidak punya mesh.

- [ ] **Step 5: Commit**

Procedure C:
- path: `Assets/WorldGen/Editor` `Assets/WorldGen/Tests` `docs/superpowers/specs/2026-10-13-osm-world-generator-design.md`
- subject: `feat(worldgen): walkable colliders on roads, areas and water; world edge walls`

---

### Task 4: Gedung kunci Kos Raflesia 3, generate ulang, cek regresi

**Files:**
- Modify: `Assets/WorldGen/Editor/Settings/DefaultSettingsFactory.cs` (daftar `keyBuildings`)
- Modify: `Assets/WorldGen/Config/WorldGenSettings.asset` (lewat snippet MCP)
- Modify: `Assets/WorldGen/Tests/DefaultSettingsTests.cs`
- Modify: `docs/superpowers/specs/2026-10-13-osm-world-generator-design.md` (§10), `docs/superpowers/notes/2026-10-13-worldgen-baseline.md`

**Interfaces:**
- Produces: gedung kunci `{409467778, "Kos Raflesia 3"}` di default dan di asset tersimpan. Setelah generate ulang, scene hasil generate punya `KeyBuildings/Kos Raflesia 3` dengan `KeyBuildingMarker`.

- [ ] **Step 1: Tulis tes yang gagal**

Di `Assets/WorldGen/Tests/DefaultSettingsTests.cs`, ganti tes `KeyBuildings_MatchSpec` dengan:
```csharp
        [Test]
        public void KeyBuildings_MatchSpec()
        {
            CollectionAssert.AreEqual(new long[] { 1257717375, 197341486, 1293369802, 1293369773, 1293369796, 409467778 },
                s.keyBuildings.Select(k => k.osmId).ToArray());
            Assert.AreEqual("Telkom University Landmark Tower (TULT)", s.keyBuildings[0].label);
            Assert.AreEqual("Kos Raflesia 3", s.keyBuildings[5].label);
            Assert.AreEqual(0, s.keyBuildings[5].overrideLevels);
            Assert.AreEqual(RoofOverride.Auto, s.keyBuildings[5].overrideRoof);
        }

        [Test]
        public void SavedAsset_HasKosRaflesia3()
        {
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<WorldGenSettings>(WorldGenPaths.SettingsAsset);
            Assert.IsNotNull(asset);
            var kos = asset.keyBuildings.SingleOrDefault(k => k.osmId == 409467778);
            Assert.IsNotNull(kos);
            Assert.AreEqual("Kos Raflesia 3", kos.label);
        }
```

- [ ] **Step 2: Jalankan tes, pastikan gagal**

Procedure T dengan `Group = "KeyBuildings_MatchSpec|SavedAsset_HasKosRaflesia3"`.
Expected: `failed=2`:
- `Expected ... length 6 but was 5`;
- `SavedAsset_HasKosRaflesia3`: `Expected: not null`.

- [ ] **Step 3: Implementasi**

Di `DefaultSettingsFactory.Populate`, tambahkan entri terakhir pada daftar `s.keyBuildings`:
```csharp
                new KeyBuilding(1293369796, "Asrama Putri Gedung A"),
                new KeyBuilding(409467778, "Kos Raflesia 3")
```

Jalankan Procedure R, lalu snippet MCP berikut untuk memperbarui asset tersimpan:
```csharp
using TycoWorld.WorldGen.Authoring;
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var s = AssetDatabase.LoadAssetAtPath<WorldGenSettings>(WorldGenPaths.SettingsAsset);
        if (!s.keyBuildings.Exists(k => k.osmId == 409467778)) s.keyBuildings.Add(new KeyBuilding(409467778, "Kos Raflesia 3"));
        EditorUtility.SetDirty(s);
        AssetDatabase.SaveAssets();
        result.Log("keyBuildings={0}", s.keyBuildings.Count);
    }
}
```
Expected: `keyBuildings=6`.

- [ ] **Step 4: Jalankan tes, pastikan lulus**

Procedure T dengan `Group = "DefaultSettingsTests"`.
Expected: `failed=0`.

Lalu `Group = "."`.
Expected: `failed=0`.

- [ ] **Step 5: Generate ulang dan verifikasi dunia asli (MCP)**

Jalankan snippet ini:
```csharp
using TycoWorld.WorldGen;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEngine;

internal class CommandScript : IRunCommand
{
    const string Dir = "D:/UNITY/project/Tyco World/.superpowers/sdd/2026-10-13-fps-player";

    public void Execute(ExecutionResult result)
    {
        var settings = AssetDatabase.LoadAssetAtPath<WorldGenSettings>(WorldGenPaths.SettingsAsset);
        if (!WorldGenerationRunner.Generate(settings, false, out string error)) { result.LogError(error); return; }
        var report = WorldGenerationRunner.LastReport;
        string cmp = TycoWorld.WorldGen.Tests.McpVerify.CompareChunkHashes(Dir + "/hashes_before.txt");
        if (cmp.StartsWith("IDENTICAL")) result.Log(cmp); else result.LogError(cmp);

        int expectedMesh = report.GetCount("Building") + report.GetCount("KeyBuilding") + report.GetCount("Road")
            + report.GetCount("Area") + report.GetCount("WaterArea") + report.GetCount("WaterLine");
        int meshColliders = UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None).Length;
        result.Log("KeyBuilding={0} Building={1} meshColliders={2} expected={3}",
            report.GetCount("KeyBuilding"), report.GetCount("Building"), meshColliders, expectedMesh);

        int missingSurface = 0;
        foreach (var f in UnityEngine.Object.FindObjectsByType<OsmFeature>(FindObjectsSortMode.None))
        {
            if (f.Kind == FeatureKind.Building) continue;
            var gs = f.GetComponent<GroundSurface>();
            if (gs == null || gs.Type == SurfaceType.Default) missingSurface++;
        }
        var edge = GameObject.Find("World_Generated/WorldEdge");
        result.Log("walkable without surface={0} edgeWalls={1}", missingSurface, edge == null ? -1 : edge.transform.childCount);

        Physics.SyncTransforms();
        RaycastHit hit;
        if (Physics.Raycast(new Vector3(-324f, 60f, -605f), Vector3.down, out hit, 100f, ~0, QueryTriggerInteraction.Ignore))
        {
            var marker = hit.collider.GetComponentInParent<KeyBuildingMarker>();
            result.Log("kos ray -> {0} marker={1}", hit.collider.name, marker == null ? "none" : marker.Label);
        }
        else result.LogError("kos ray missed");
        if (Physics.Raycast(new Vector3(-194.6f, 30f, -632.7f), Vector3.down, out hit, 60f, ~0, QueryTriggerInteraction.Ignore))
        {
            var gs = hit.collider.GetComponentInParent<GroundSurface>();
            var osm = hit.collider.GetComponentInParent<OsmFeature>();
            result.Log("sukabirus ray -> {0} surface={1} way={2} y={3:F3}", hit.collider.name,
                gs == null ? "none" : gs.Type.ToString(), osm == null ? 0 : osm.OsmId, hit.point.y);
        }
        else result.LogError("sukabirus ray missed");
    }
}
```
Expected:
- `IDENTICAL chunks=278`;
- `KeyBuilding=6 Building=4915` dengan `meshColliders` = `expected` (sekitar 5741);
- `walkable without surface=0 edgeWalls=4`;
- `kos ray -> Kos Raflesia 3 marker=Kos Raflesia 3`;
- `sukabirus ray -> … surface=Asphalt way=95529338 y=0.090`.

Lalu `mcp__unity-mcp__Unity_GetConsoleLogs` (`logTypes: "error,warning"`). Expected: tidak ada warning/error baru.

**Kalau hasilnya `DIFF`:** berhenti dan catat di ledger. Collider dan komponen tidak ikut di-hash, jadi selisih berarti ada perubahan mesh yang tidak disengaja.

- [ ] **Step 6: Perbarui dokumen**

Di spec #1 §10:
- tambahkan baris tabel `| 409467778 | Kos Raflesia 3 (kos pemain, spec #2 §3) |`;
- ganti kalimat "Kos di Sukabirus ditambahkan pengguna belakangan lewat Settings." dengan "Kos Raflesia 3 (kos pemain) ditambahkan di sub-proyek #2."

Di spec #1 §15 (Integrasi), tambahkan butir: `raycast ke bawah di Jl. Sukabirus mengenai collider jalan dengan GroundSurface Asphalt; 4 dinding WorldEdge ada; hash chunk tidak berubah karena collider (sub-proyek #2)`.

Di `docs/superpowers/notes/2026-10-13-worldgen-baseline.md`, tambahkan di akhir:
```markdown
- Sub-proyek #2 (player): jalan, area, dan air sekarang punya MeshCollider + GroundSurface; ada 4 dinding batas WorldEdge; Kos Raflesia 3 (way 409467778) jadi gedung kunci keenam (KeyBuilding=6, Building=4915). Hash ke-278 chunk identik dengan sebelum perubahan.
```

- [ ] **Step 7: Commit**

Procedure C:
- path: `Assets/WorldGen/Editor/Settings/DefaultSettingsFactory.cs` `Assets/WorldGen/Config/WorldGenSettings.asset` `Assets/WorldGen/Tests/DefaultSettingsTests.cs` `docs/superpowers/specs/2026-10-13-osm-world-generator-design.md` `docs/superpowers/notes/2026-10-13-worldgen-baseline.md`
- subject: `feat(worldgen): add Kos Raflesia 3 key building`

---
### Task 5: Asset input, intent, sumber intent berskrip, kunci kemampuan

**Files:**
- Modify: `Assets/InputSystem_Actions.inputactions` (Interact tanpa Hold, action Flashlight → F)
- Create: `Assets/Player/Runtime/Input/PlayerIntent.cs`, `IPlayerIntentSource.cs`, `ScriptedIntentSource.cs`, `ControlLocks.cs`
- Test: `Assets/Player/Tests/InputActionsAssetTests.cs`, `ScriptedIntentSourceTests.cs`, `ControlLocksTests.cs`

**Interfaces:**
- Produces:
  - `struct PlayerIntent { Vector2 Move, LookDegrees; bool Sprint, Jump, CrouchToggle, Interact, Flashlight; }`.
  - `interface IPlayerIntentSource { PlayerIntent Read(float dt); }`.
  - `ScriptedIntentSource`:
    - `Hold(PlayerIntent held, float seconds)`: Move/Sprint ditahan; `LookDegrees` dibaca sebagai derajat per detik.
    - `Press(PlayerIntent pressed)`: flag edge plus delta look seketika.
    - `Clear()` dan `IsDone`.
  - `[Flags] enum PlayerAbility { None, Move=1, Look=2, Interact=4, Flashlight=8, All=15 }`.
  - `ControlLocks`: `Acquire(object, PlayerAbility)`, `Release(object)` → bool, `Allows(PlayerAbility)`, `IsHeldBy(object)`, `Blocked`, `event Action Changed` (hanya terpicu saat `Blocked` berubah).

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/Player/Tests/InputActionsAssetTests.cs`:
```csharp
using System.Linq;
using NUnit.Framework;
using TycoWorld.Player.Authoring;
using UnityEditor;
using UnityEngine.InputSystem;

namespace TycoWorld.Player.Tests
{
    public class InputActionsAssetTests
    {
        static InputActionAsset Load()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(PlayerPaths.InputActionsAsset);
            Assert.IsNotNull(asset);
            return asset;
        }

        [Test]
        public void PlayerMap_HasRequiredActions()
        {
            var map = Load().FindActionMap("Player", true);
            foreach (var name in new[] { "Move", "Look", "Sprint", "Jump", "Crouch", "Interact", "Flashlight", "Attack" })
                Assert.IsNotNull(map.FindAction(name), name);
        }

        [Test]
        public void Flashlight_IsButtonBoundToF()
        {
            var action = Load().FindAction("Player/Flashlight", true);
            Assert.AreEqual(InputActionType.Button, action.type);
            Assert.IsTrue(action.bindings.Any(b => b.path == "<Keyboard>/f" && b.groups.Contains("Keyboard&Mouse")));
        }

        [Test]
        public void Interact_IsTapNotHold()
        {
            var action = Load().FindAction("Player/Interact", true);
            Assert.IsTrue(string.IsNullOrEmpty(action.interactions), action.interactions);
            Assert.IsTrue(action.bindings.All(b => string.IsNullOrEmpty(b.interactions)));
            Assert.IsTrue(action.bindings.Any(b => b.path == "<Keyboard>/e"));
        }

        [Test]
        public void Crouch_IsBoundToC() =>
            Assert.IsTrue(Load().FindAction("Player/Crouch", true).bindings.Any(b => b.path == "<Keyboard>/c"));
    }
}
```

`Assets/Player/Tests/ScriptedIntentSourceTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.Player.Tests
{
    public class ScriptedIntentSourceTests
    {
        [Test]
        public void Press_FiresEdgesOnce()
        {
            var s = new ScriptedIntentSource();
            s.Press(new PlayerIntent { Interact = true, Jump = true });
            Assert.IsFalse(s.IsDone);
            var first = s.Read(0.016f);
            Assert.IsTrue(first.Interact);
            Assert.IsTrue(first.Jump);
            var second = s.Read(0.016f);
            Assert.IsFalse(second.Interact);
            Assert.IsFalse(second.Jump);
            Assert.IsTrue(s.IsDone);
        }

        [Test]
        public void Hold_ExpiresByAccumulatedTime()
        {
            var s = new ScriptedIntentSource();
            s.Hold(new PlayerIntent { Move = Vector2.up, Sprint = true }, 0.5f);
            int moving = 0;
            for (int i = 0; i < 5; i++)
                if (s.Read(0.2f).Move == Vector2.up) moving++;
            Assert.AreEqual(3, moving);
            Assert.IsTrue(s.IsDone);
        }

        [Test]
        public void Hold_LookIsDegreesPerSecond()
        {
            var s = new ScriptedIntentSource();
            s.Hold(new PlayerIntent { LookDegrees = new Vector2(90f, -10f) }, 1f);
            var intent = s.Read(0.5f);
            Assert.AreEqual(45f, intent.LookDegrees.x, 1e-4f);
            Assert.AreEqual(-5f, intent.LookDegrees.y, 1e-4f);
        }

        [Test]
        public void Hold_IgnoresEdgeFlags()
        {
            var s = new ScriptedIntentSource();
            s.Hold(new PlayerIntent { Move = Vector2.up, Interact = true }, 1f);
            Assert.IsFalse(s.Read(0.1f).Interact);
        }

        [Test]
        public void Segments_RunInOrder_AndEdgesMergeWithHold()
        {
            var s = new ScriptedIntentSource();
            s.Hold(new PlayerIntent { Move = Vector2.up }, 0.1f);
            s.Hold(new PlayerIntent { Move = Vector2.right }, 0.1f);
            s.Press(new PlayerIntent { Flashlight = true, LookDegrees = new Vector2(30f, 0f) });
            var a = s.Read(0.1f);
            Assert.AreEqual(Vector2.up, a.Move);
            Assert.IsTrue(a.Flashlight);
            Assert.AreEqual(30f, a.LookDegrees.x, 1e-4f);
            Assert.AreEqual(Vector2.right, s.Read(0.1f).Move);
            Assert.AreEqual(Vector2.zero, s.Read(0.1f).Move);
        }

        [Test]
        public void Clear_DropsEverything()
        {
            var s = new ScriptedIntentSource();
            s.Hold(new PlayerIntent { Move = Vector2.up }, 5f);
            s.Press(new PlayerIntent { Jump = true });
            s.Clear();
            Assert.IsTrue(s.IsDone);
            var intent = s.Read(0.1f);
            Assert.AreEqual(Vector2.zero, intent.Move);
            Assert.IsFalse(intent.Jump);
        }
    }
}
```

`Assets/Player/Tests/ControlLocksTests.cs`:
```csharp
using System;
using NUnit.Framework;

namespace TycoWorld.Player.Tests
{
    public class ControlLocksTests
    {
        [Test]
        public void Empty_AllowsEverything()
        {
            var locks = new ControlLocks();
            Assert.AreEqual(PlayerAbility.None, locks.Blocked);
            Assert.IsTrue(locks.Allows(PlayerAbility.All));
        }

        [Test]
        public void Acquire_BlocksUnionOfOwners()
        {
            var locks = new ControlLocks();
            object bike = new object(), cutscene = new object();
            locks.Acquire(bike, PlayerAbility.Move | PlayerAbility.Interact);
            locks.Acquire(cutscene, PlayerAbility.Look);
            Assert.IsFalse(locks.Allows(PlayerAbility.Move));
            Assert.IsFalse(locks.Allows(PlayerAbility.Look));
            Assert.IsTrue(locks.Allows(PlayerAbility.Flashlight));
            Assert.IsTrue(locks.Release(bike));
            Assert.IsTrue(locks.Allows(PlayerAbility.Move));
            Assert.IsFalse(locks.Allows(PlayerAbility.Look));
        }

        [Test]
        public void Release_ByWrongOwner_IsNoOp()
        {
            var locks = new ControlLocks();
            object bike = new object();
            locks.Acquire(bike, PlayerAbility.Move);
            Assert.IsFalse(locks.Release(new object()));
            Assert.IsFalse(locks.Release(null));
            Assert.IsFalse(locks.Allows(PlayerAbility.Move));
            Assert.IsTrue(locks.IsHeldBy(bike));
        }

        [Test]
        public void Acquire_SameOwner_ReplacesMask()
        {
            var locks = new ControlLocks();
            object owner = new object();
            locks.Acquire(owner, PlayerAbility.All);
            locks.Acquire(owner, PlayerAbility.Look);
            Assert.IsTrue(locks.Allows(PlayerAbility.Move));
            Assert.IsFalse(locks.Allows(PlayerAbility.Look));
        }

        [Test]
        public void Changed_FiresOnlyWhenBlockedChanges()
        {
            var locks = new ControlLocks();
            int fired = 0;
            locks.Changed += () => fired++;
            object a = new object(), b = new object();
            locks.Acquire(a, PlayerAbility.Move);
            locks.Acquire(b, PlayerAbility.Move);
            locks.Release(a);
            locks.Release(b);
            Assert.AreEqual(2, fired);
        }

        [Test]
        public void Acquire_NullOwner_Throws() =>
            Assert.Throws<ArgumentNullException>(() => new ControlLocks().Acquire(null, PlayerAbility.Move));
    }
}
```

- [ ] **Step 2: Jalankan tes, pastikan gagal**

Procedure R, lalu Procedure T dengan `Group = "InputActionsAssetTests|ScriptedIntentSourceTests|ControlLocksTests"`.
Expected: COMPILE FAILED (`CS0246 ScriptedIntentSource`, `PlayerIntent`, `ControlLocks`, `PlayerAbility`).

- [ ] **Step 3: Implementasi**

Ubah asset input dengan skrip Python lewat tool Bash. Skrip ini idempoten dan GUID-nya tetap:
```bash
python -I - "D:/UNITY/project/Tyco World/Assets/InputSystem_Actions.inputactions" <<'EOF'
import json, sys
path = sys.argv[1]
with open(path, encoding='utf-8-sig') as f:
    d = json.load(f)
player = next(m for m in d['maps'] if m['name'] == 'Player')
for a in player['actions']:
    if a['name'] == 'Interact':
        a['interactions'] = ''
if not any(a['name'] == 'Flashlight' for a in player['actions']):
    player['actions'].append({"name": "Flashlight", "type": "Button", "id": "5d3f6a1e-2b7c-4e8d-9f10-1a2b3c4d5e6f",
                              "expectedControlType": "Button", "processors": "", "interactions": "", "initialStateCheck": False})
    player['bindings'].append({"name": "", "id": "8e7d6c5b-4a39-4281-b0c1-d2e3f4a5b6c7", "path": "<Keyboard>/f",
                               "interactions": "", "processors": "", "groups": "Keyboard&Mouse", "action": "Flashlight",
                               "isComposite": False, "isPartOfComposite": False})
with open(path, 'w', encoding='utf-8', newline='\n') as f:
    json.dump(d, f, indent=4, ensure_ascii=False)
    f.write('\n')
print('ok')
EOF
```
Expected: `ok`. Periksa dengan `git -C "D:/UNITY/project/Tyco World" diff --stat Assets/InputSystem_Actions.inputactions`: perubahan hanya di file itu.

`Assets/Player/Runtime/Input/PlayerIntent.cs`:
```csharp
using UnityEngine;

namespace TycoWorld.Player
{
    /// <summary>What the player wants this frame, independent of the device (keyboard, script, cutscene).</summary>
    public struct PlayerIntent
    {
        /// <summary>x = strafe right, y = forward; magnitude ≤ 1.</summary>
        public Vector2 Move;
        /// <summary>x = yaw delta, y = pitch delta (positive looks up), in degrees for this frame.</summary>
        public Vector2 LookDegrees;
        /// <summary>Held.</summary>
        public bool Sprint;
        /// <summary>Pressed this frame.</summary>
        public bool Jump, CrouchToggle, Interact, Flashlight;
    }
}
```

`Assets/Player/Runtime/Input/IPlayerIntentSource.cs`:
```csharp
namespace TycoWorld.Player
{
    public interface IPlayerIntentSource
    {
        PlayerIntent Read(float dt);
    }
}
```

`Assets/Player/Runtime/Input/ScriptedIntentSource.cs`:
```csharp
using System.Collections.Generic;

namespace TycoWorld.Player
{
    /// <summary>Intent source driven by code: tests, MCP verification, and later cutscenes.</summary>
    public sealed class ScriptedIntentSource : IPlayerIntentSource
    {
        struct Segment
        {
            public PlayerIntent Intent;
            public float Remaining;
        }

        readonly List<Segment> segments = new List<Segment>();
        PlayerIntent edges;
        bool hasEdges;

        public bool IsDone => segments.Count == 0 && !hasEdges;

        /// <summary>Holds Move and Sprint for <paramref name="seconds"/>; LookDegrees is read as degrees per second. Edge flags are ignored.</summary>
        public void Hold(PlayerIntent held, float seconds)
        {
            if (seconds > 0f) segments.Add(new Segment { Intent = held, Remaining = seconds });
        }

        /// <summary>Fires Jump/CrouchToggle/Interact/Flashlight and an instant look delta on the next Read.</summary>
        public void Press(PlayerIntent pressed)
        {
            edges.Jump |= pressed.Jump;
            edges.CrouchToggle |= pressed.CrouchToggle;
            edges.Interact |= pressed.Interact;
            edges.Flashlight |= pressed.Flashlight;
            edges.LookDegrees += pressed.LookDegrees;
            hasEdges = true;
        }

        public void Clear()
        {
            segments.Clear();
            edges = default;
            hasEdges = false;
        }

        public PlayerIntent Read(float dt)
        {
            var result = new PlayerIntent();
            if (segments.Count > 0)
            {
                var segment = segments[0];
                result.Move = segment.Intent.Move;
                result.Sprint = segment.Intent.Sprint;
                result.LookDegrees = segment.Intent.LookDegrees * dt;
                segment.Remaining -= dt;
                if (segment.Remaining <= 0f) segments.RemoveAt(0);
                else segments[0] = segment;
            }
            if (hasEdges)
            {
                result.Jump = edges.Jump;
                result.CrouchToggle = edges.CrouchToggle;
                result.Interact = edges.Interact;
                result.Flashlight = edges.Flashlight;
                result.LookDegrees += edges.LookDegrees;
                edges = default;
                hasEdges = false;
            }
            return result;
        }
    }
}
```

`Assets/Player/Runtime/Input/ControlLocks.cs`:
```csharp
using System;
using System.Collections.Generic;

namespace TycoWorld.Player
{
    [Flags]
    public enum PlayerAbility
    {
        None = 0,
        Move = 1,
        Look = 2,
        Interact = 4,
        Flashlight = 8,
        All = Move | Look | Interact | Flashlight
    }

    /// <summary>Owner-keyed ability locks: a vehicle, cutscene or spawner can only release its own lock.</summary>
    public sealed class ControlLocks
    {
        readonly Dictionary<object, PlayerAbility> locks = new Dictionary<object, PlayerAbility>();

        public event Action Changed;

        public PlayerAbility Blocked { get; private set; }

        public void Acquire(object owner, PlayerAbility blocked)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            locks[owner] = blocked;
            Recompute();
        }

        public bool Release(object owner)
        {
            if (owner == null || !locks.Remove(owner)) return false;
            Recompute();
            return true;
        }

        public bool IsHeldBy(object owner) => owner != null && locks.ContainsKey(owner);

        public bool Allows(PlayerAbility ability) => (Blocked & ability) == 0;

        void Recompute()
        {
            var blocked = PlayerAbility.None;
            foreach (var mask in locks.Values) blocked |= mask;
            if (blocked == Blocked) return;
            Blocked = blocked;
            Changed?.Invoke();
        }
    }
}
```

- [ ] **Step 4: Jalankan tes, pastikan lulus**

Procedure R, lalu Procedure T dengan `Group = "InputActionsAssetTests|ScriptedIntentSourceTests|ControlLocksTests"`.
Expected: `passed=16 failed=0`.

- [ ] **Step 5: Commit**

Procedure C:
- path: `Assets/InputSystem_Actions.inputactions` `Assets/Player`
- subject: `feat(player): intents, scripted source, control locks, flashlight action`

---

### Task 6: `PlayerTuning`, `MovementModel`, `LookModel`

**Files:**
- Create: `Assets/Player/Runtime/Tuning/PlayerTuning.cs`
- Create: `Assets/Player/Runtime/Motion/MovementModel.cs`, `Assets/Player/Runtime/Motion/LookModel.cs`
- Test: `Assets/Player/Tests/PlayerTuningTests.cs`, `MovementModelTests.cs`, `LookModelTests.cs`

**Interfaces:**
- Produces:
  - `PlayerTuning : ScriptableObject` dengan field `body` (`BodyTuning`), `movement`, `stamina`, `look`, `crouch`, `bob`, `interaction`, `flashlight`, `hud`, `respawnBelowY`, `worldWaitSeconds`.
  - Kelas tuning `[Serializable]` dengan nama field persis seperti di kode di bawah.
  - `struct MoveInput { Vector2 Move; Vector3 Forward, Right; bool Grounded, Sprinting, Crouched, JumpRequested; }`.
  - `MovementModel(MovementTuning)` dengan:
    - `Velocity` (field `Vector3`), `JumpedThisStep`;
    - `TargetSpeed(bool sprinting, bool crouched)`, `CanJump(bool groundedNow, bool crouched)`;
    - `Step(in MoveInput, float dt)` → perpindahan `Vector3`;
    - `HitCeiling()` dan `ResetMotion()`.
  - `LookModel.Apply(Vector2 yawPitch, Vector2 deltaDegrees, float pitchLimit)` → `Vector2` dan `LookModel.PitchRotation(float pitch)` → `Quaternion`.

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/Player/Tests/PlayerTuningTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.Player.Tests
{
    public class PlayerTuningTests
    {
        [Test]
        public void Defaults_MatchSpec()
        {
            var t = ScriptableObject.CreateInstance<PlayerTuning>();
            try
            {
                Assert.AreEqual(1.8f, t.body.height);
                Assert.AreEqual(0.35f, t.body.radius);
                Assert.AreEqual(0.3f, t.body.stepOffset);
                Assert.AreEqual(0.05f, t.body.skinWidth);
                Assert.AreEqual(3f, t.movement.walkSpeed);
                Assert.AreEqual(5.5f, t.movement.sprintSpeed);
                Assert.AreEqual(1.5f, t.movement.crouchSpeed);
                Assert.AreEqual(12f, t.movement.acceleration);
                Assert.AreEqual(16f, t.movement.deceleration);
                Assert.AreEqual(0.3f, t.movement.airControl);
                Assert.AreEqual(0.8f, t.movement.jumpHeight);
                Assert.AreEqual(9.81f, t.movement.gravity);
                Assert.AreEqual(2f, t.movement.groundStick);
                Assert.AreEqual(0.1f, t.movement.coyoteTime);
                Assert.AreEqual(100f, t.stamina.max);
                Assert.AreEqual(18f, t.stamina.sprintDrainPerSecond);
                Assert.AreEqual(14f, t.stamina.regenPerSecond);
                Assert.AreEqual(1f, t.stamina.regenDelay);
                Assert.AreEqual(0.3f, t.stamina.recoverThreshold);
                Assert.AreEqual(8f, t.stamina.jumpCost);
                Assert.AreEqual(0.1f, t.look.degreesPerPixel);
                Assert.AreEqual(85f, t.look.pitchLimit);
                Assert.AreEqual(70f, t.look.fieldOfView);
                Assert.AreEqual(1f, t.crouch.crouchHeight);
                Assert.AreEqual(1.6f, t.crouch.standingEyeHeight);
                Assert.AreEqual(0.85f, t.crouch.crouchEyeHeight);
                Assert.AreEqual(0.75f, t.bob.walkStride);
                Assert.AreEqual(1f, t.bob.sprintStride);
                Assert.AreEqual(0.55f, t.bob.crouchStride);
                Assert.AreEqual(0.045f, t.bob.walkAmplitude);
                Assert.AreEqual(0.07f, t.bob.sprintAmplitude);
                Assert.AreEqual(0.025f, t.bob.crouchAmplitude);
                Assert.AreEqual(0.5f, t.bob.lateralRatio);
                Assert.AreEqual(2.2f, t.interaction.range);
                Assert.AreEqual(0.12f, t.interaction.sphereRadius);
                Assert.AreEqual(55f, t.flashlight.outerAngle);
                Assert.AreEqual(30f, t.flashlight.innerAngle);
                Assert.AreEqual(22f, t.flashlight.range);
                Assert.AreEqual(14f, t.flashlight.lagSpeed);
                Assert.IsFalse(t.flashlight.startOn);
                Assert.AreEqual(2.5f, t.hud.messageSeconds);
                Assert.AreEqual(0.8f, t.hud.staminaHideDelay);
                Assert.AreEqual(-20f, t.respawnBelowY);
                Assert.AreEqual(15f, t.worldWaitSeconds);
            }
            finally
            {
                Object.DestroyImmediate(t);
            }
        }
    }
}
```

`Assets/Player/Tests/MovementModelTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.Player.Tests
{
    public class MovementModelTests
    {
        const float Dt = 1f / 60f;

        static MoveInput Input(Vector2 move, bool grounded = true, bool sprint = false, bool crouch = false, bool jump = false) =>
            new MoveInput { Move = move, Forward = Vector3.forward, Right = Vector3.right, Grounded = grounded, Sprinting = sprint, Crouched = crouch, JumpRequested = jump };

        static float Horizontal(MovementModel m) => new Vector3(m.Velocity.x, 0f, m.Velocity.z).magnitude;

        static MovementModel Run(MoveInput input, int steps)
        {
            var m = new MovementModel(new MovementTuning());
            for (int i = 0; i < steps; i++) m.Step(input, Dt);
            return m;
        }

        [Test]
        public void Walk_ReachesWalkSpeed() => Assert.AreEqual(3f, Horizontal(Run(Input(Vector2.up), 60)), 1e-3f);

        [Test]
        public void Sprint_ReachesSprintSpeed() => Assert.AreEqual(5.5f, Horizontal(Run(Input(Vector2.up, sprint: true), 60)), 1e-3f);

        [Test]
        public void Crouch_UsesCrouchSpeed_EvenWhenSprintHeld() =>
            Assert.AreEqual(1.5f, Horizontal(Run(Input(Vector2.up, sprint: true, crouch: true), 60)), 1e-3f);

        [Test]
        public void Diagonal_IsNotFaster() => Assert.AreEqual(3f, Horizontal(Run(Input(new Vector2(1f, 1f)), 60)), 1e-3f);

        [Test]
        public void Move_FollowsBodyAxes()
        {
            var m = Run(Input(Vector2.right), 60);
            Assert.AreEqual(3f, m.Velocity.x, 1e-3f);
            Assert.AreEqual(0f, m.Velocity.z, 1e-3f);
        }

        [Test]
        public void NoInput_DeceleratesToRest()
        {
            var m = Run(Input(Vector2.up), 60);
            for (int i = 0; i < 30; i++) m.Step(Input(Vector2.zero), Dt);
            Assert.AreEqual(0f, Horizontal(m), 1e-4f);
        }

        [Test]
        public void Grounded_KeepsDownwardStick()
        {
            var m = Run(Input(Vector2.zero), 1);
            Assert.AreEqual(-2f, m.Velocity.y, 1e-5f);
        }

        [Test]
        public void Jump_ApexMatchesJumpHeight()
        {
            var m = new MovementModel(new MovementTuning());
            m.Step(Input(Vector2.zero), Dt);
            Assert.IsTrue(m.CanJump(true, false));
            float y = m.Step(Input(Vector2.zero, jump: true), Dt).y;
            Assert.IsTrue(m.JumpedThisStep);
            float apex = y;
            for (int i = 0; i < 120 && y >= 0f; i++)
            {
                y += m.Step(Input(Vector2.zero, grounded: false), Dt).y;
                apex = Mathf.Max(apex, y);
            }
            Assert.That(apex, Is.InRange(0.78f, 0.9f));
        }

        [Test]
        public void CanJump_RespectsCoyoteTime()
        {
            var m = new MovementModel(new MovementTuning());
            m.Step(Input(Vector2.zero), Dt);
            m.Step(Input(Vector2.zero, grounded: false), 0.05f);
            Assert.IsTrue(m.CanJump(false, false));
            m.Step(Input(Vector2.zero, grounded: false), 0.1f);
            Assert.IsFalse(m.CanJump(false, false));
        }

        [Test]
        public void CanJump_FalseWhileCrouched() => Assert.IsFalse(Run(Input(Vector2.zero), 1).CanJump(true, true));

        [Test]
        public void AirControl_ReducesAcceleration()
        {
            var m = new MovementModel(new MovementTuning());
            for (int i = 0; i < 6; i++) m.Step(Input(Vector2.up, grounded: false), Dt);
            Assert.AreEqual(12f * 0.3f * 0.1f, Horizontal(m), 0.01f);
        }

        [Test]
        public void HitCeiling_StopsUpwardVelocity()
        {
            var m = Run(Input(Vector2.zero), 1);
            m.Step(Input(Vector2.zero, jump: true), Dt);
            Assert.Greater(m.Velocity.y, 0f);
            m.HitCeiling();
            Assert.AreEqual(0f, m.Velocity.y);
        }

        [Test]
        public void ResetMotion_ClearsVelocityAndCoyote()
        {
            var m = Run(Input(Vector2.up), 30);
            m.ResetMotion();
            Assert.AreEqual(Vector3.zero, m.Velocity);
            Assert.IsFalse(m.CanJump(false, false));
        }
    }
}
```

`Assets/Player/Tests/LookModelTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.Player.Tests
{
    public class LookModelTests
    {
        [Test]
        public void Pitch_ClampsAtLimit()
        {
            Assert.AreEqual(85f, LookModel.Apply(Vector2.zero, new Vector2(0f, 200f), 85f).y);
            Assert.AreEqual(-85f, LookModel.Apply(Vector2.zero, new Vector2(0f, -200f), 85f).y);
        }

        [Test]
        public void Yaw_WrapsIntoZeroTo360()
        {
            Assert.AreEqual(10f, LookModel.Apply(new Vector2(350f, 0f), new Vector2(20f, 0f), 85f).x, 1e-4f);
            Assert.AreEqual(350f, LookModel.Apply(new Vector2(10f, 0f), new Vector2(-20f, 0f), 85f).x, 1e-4f);
        }

        [Test]
        public void PitchRotation_PositiveLooksUp()
        {
            Assert.Greater((LookModel.PitchRotation(30f) * Vector3.forward).y, 0.4f);
            Assert.Less((LookModel.PitchRotation(-30f) * Vector3.forward).y, -0.4f);
        }
    }
}
```

- [ ] **Step 2: Jalankan tes, pastikan gagal**

Procedure R, lalu Procedure T dengan `Group = "PlayerTuningTests|MovementModelTests|LookModelTests"`.
Expected: COMPILE FAILED (`CS0246 PlayerTuning`, `MovementModel`, `MovementTuning`, `MoveInput`, `LookModel`).

- [ ] **Step 3: Implementasi**

`Assets/Player/Runtime/Tuning/PlayerTuning.cs`:
```csharp
using System;
using UnityEngine;

namespace TycoWorld.Player
{
    [Serializable]
    public sealed class BodyTuning
    {
        public float height = 1.8f;
        public float radius = 0.35f;
        public float stepOffset = 0.3f;
        public float skinWidth = 0.05f;
        public float slopeLimit = 50f;
    }

    [Serializable]
    public sealed class MovementTuning
    {
        public float walkSpeed = 3f;
        public float sprintSpeed = 5.5f;
        public float crouchSpeed = 1.5f;
        public float acceleration = 12f;
        public float deceleration = 16f;
        public float airControl = 0.3f;
        public float jumpHeight = 0.8f;
        public float gravity = 9.81f;
        public float groundStick = 2f;
        public float coyoteTime = 0.1f;
    }

    [Serializable]
    public sealed class StaminaTuning
    {
        public float max = 100f;
        public float sprintDrainPerSecond = 18f;
        public float regenPerSecond = 14f;
        public float regenDelay = 1f;
        public float recoverThreshold = 0.3f;
        public float jumpCost = 8f;
    }

    [Serializable]
    public sealed class LookTuning
    {
        public float degreesPerPixel = 0.1f;
        public float pitchLimit = 85f;
        public float fieldOfView = 70f;
    }

    [Serializable]
    public sealed class CrouchTuning
    {
        public float crouchHeight = 1f;
        public float standingEyeHeight = 1.6f;
        public float crouchEyeHeight = 0.85f;
        public float transitionSpeed = 4f;
    }

    [Serializable]
    public sealed class BobTuning
    {
        public float walkStride = 0.75f;
        public float sprintStride = 1f;
        public float crouchStride = 0.55f;
        public float walkAmplitude = 0.045f;
        public float sprintAmplitude = 0.07f;
        public float crouchAmplitude = 0.025f;
        public float lateralRatio = 0.5f;
        public float fadeSpeed = 4f;
        public float landingMinSpeed = 3f;
    }

    [Serializable]
    public sealed class InteractionTuning
    {
        public float range = 2.2f;
        public float sphereRadius = 0.12f;
    }

    [Serializable]
    public sealed class FlashlightTuning
    {
        public float outerAngle = 55f;
        public float innerAngle = 30f;
        public float range = 22f;
        public float intensity = 3f;
        public Color color = new Color(1f, 0.95f, 0.85f);
        public float lagSpeed = 14f;
        public bool startOn;
    }

    [Serializable]
    public sealed class HudTuning
    {
        public float messageSeconds = 2.5f;
        public float staminaHideDelay = 0.8f;
    }

    /// <summary>All player tuning in one asset; field initialisers are the spec defaults (spec #2 §4).</summary>
    [CreateAssetMenu(menuName = "Tyco World/Player Tuning", fileName = "PlayerTuning")]
    public sealed class PlayerTuning : ScriptableObject
    {
        public BodyTuning body = new BodyTuning();
        public MovementTuning movement = new MovementTuning();
        public StaminaTuning stamina = new StaminaTuning();
        public LookTuning look = new LookTuning();
        public CrouchTuning crouch = new CrouchTuning();
        public BobTuning bob = new BobTuning();
        public InteractionTuning interaction = new InteractionTuning();
        public FlashlightTuning flashlight = new FlashlightTuning();
        public HudTuning hud = new HudTuning();
        public float respawnBelowY = -20f;
        public float worldWaitSeconds = 15f;
    }
}
```

`Assets/Player/Runtime/Motion/MovementModel.cs`:
```csharp
using UnityEngine;

namespace TycoWorld.Player
{
    public struct MoveInput
    {
        public Vector2 Move;
        public Vector3 Forward, Right;
        public bool Grounded, Sprinting, Crouched, JumpRequested;
    }

    /// <summary>Pure character motion: speed per mode, acceleration, air control, jump with coyote time, gravity.</summary>
    public sealed class MovementModel
    {
        readonly MovementTuning t;
        float sinceGrounded = float.PositiveInfinity;

        public Vector3 Velocity;

        public bool JumpedThisStep { get; private set; }

        public MovementModel(MovementTuning tuning) { t = tuning; }

        public float TargetSpeed(bool sprinting, bool crouched) => crouched ? t.crouchSpeed : sprinting ? t.sprintSpeed : t.walkSpeed;

        public bool CanJump(bool groundedNow, bool crouched) => !crouched && (groundedNow || sinceGrounded <= t.coyoteTime);

        /// <summary>Advances velocity by <paramref name="dt"/> and returns the displacement to feed CharacterController.Move.</summary>
        public Vector3 Step(in MoveInput input, float dt)
        {
            JumpedThisStep = false;
            var move = Vector2.ClampMagnitude(input.Move, 1f);
            var wish = (input.Forward * move.y + input.Right * move.x) * TargetSpeed(input.Sprinting, input.Crouched);
            wish.y = 0f;
            var horizontal = new Vector3(Velocity.x, 0f, Velocity.z);
            float rate = wish.sqrMagnitude > 1e-6f ? t.acceleration : t.deceleration;
            if (!input.Grounded) rate *= t.airControl;
            horizontal = Vector3.MoveTowards(horizontal, wish, rate * dt);

            if (input.Grounded) sinceGrounded = 0f;
            else sinceGrounded += dt;

            float vy = Velocity.y;
            if (input.JumpRequested && CanJump(input.Grounded, input.Crouched))
            {
                vy = Mathf.Sqrt(2f * t.gravity * t.jumpHeight);
                JumpedThisStep = true;
                sinceGrounded = float.PositiveInfinity;
            }
            else if (input.Grounded && vy <= 0f) vy = -t.groundStick;
            else vy -= t.gravity * dt;

            Velocity = new Vector3(horizontal.x, vy, horizontal.z);
            return Velocity * dt;
        }

        public void HitCeiling()
        {
            if (Velocity.y > 0f) Velocity.y = 0f;
        }

        public void ResetMotion()
        {
            Velocity = Vector3.zero;
            sinceGrounded = float.PositiveInfinity;
        }
    }
}
```

`Assets/Player/Runtime/Motion/LookModel.cs`:
```csharp
using UnityEngine;

namespace TycoWorld.Player
{
    /// <summary>Yaw wraps to [0, 360); pitch (positive = up) is clamped.</summary>
    public static class LookModel
    {
        public static Vector2 Apply(Vector2 yawPitch, Vector2 deltaDegrees, float pitchLimit) =>
            new Vector2(Mathf.Repeat(yawPitch.x + deltaDegrees.x, 360f),
                Mathf.Clamp(yawPitch.y + deltaDegrees.y, -pitchLimit, pitchLimit));

        public static Quaternion PitchRotation(float pitch) => Quaternion.Euler(-pitch, 0f, 0f);
    }
}
```

- [ ] **Step 4: Jalankan tes, pastikan lulus**

Procedure R, lalu Procedure T dengan `Group = "PlayerTuningTests|MovementModelTests|LookModelTests"`.
Expected: `passed=17 failed=0`.

- [ ] **Step 5: Commit**

Procedure C:
- path: `Assets/Player`
- subject: `feat(player): tuning asset, movement and look models`

---

### Task 7: `StaminaModel` dan `CrouchModel`

**Files:**
- Create: `Assets/Player/Runtime/Motion/StaminaModel.cs`, `Assets/Player/Runtime/Motion/CrouchModel.cs`
- Test: `Assets/Player/Tests/StaminaModelTests.cs`, `CrouchModelTests.cs`

**Interfaces:**
- Consumes: `StaminaTuning`, `CrouchTuning` (Task 6).
- Produces:
  - `StaminaModel(StaminaTuning)`: `Normalized`, `Exhausted`, `CanSprint`, `Tick(bool sprintingAndMoving, float dt)`, `TrySpend(float)` → bool, `Refill()`.
  - `CrouchModel(CrouchTuning, float standingHeight)`: `Crouched`, `Height`, `EyeHeight`, `TargetHeight`, `RequestToggle(bool hasHeadroomToStand)`, `Tick(float dt)`.

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/Player/Tests/StaminaModelTests.cs`:
```csharp
using NUnit.Framework;

namespace TycoWorld.Player.Tests
{
    public class StaminaModelTests
    {
        [Test]
        public void StartsFull() => Assert.AreEqual(1f, new StaminaModel(new StaminaTuning()).Normalized);

        [Test]
        public void Drains_OnlyWhileSprintingAndMoving()
        {
            var s = new StaminaModel(new StaminaTuning());
            s.Tick(false, 1f);
            Assert.AreEqual(1f, s.Normalized, 1e-5f);
            s.Tick(true, 1f);
            Assert.AreEqual(0.82f, s.Normalized, 1e-4f);
        }

        [Test]
        public void Regen_WaitsForDelay()
        {
            var s = new StaminaModel(new StaminaTuning());
            s.Tick(true, 1f);
            s.Tick(false, 0.5f);
            Assert.AreEqual(0.82f, s.Normalized, 1e-4f);
            s.Tick(false, 0.5f);
            Assert.AreEqual(0.89f, s.Normalized, 1e-4f);
        }

        [Test]
        public void Exhausted_LocksSprintUntilThreshold()
        {
            var s = new StaminaModel(new StaminaTuning());
            s.Tick(true, 6f);
            Assert.AreEqual(0f, s.Normalized, 1e-5f);
            Assert.IsTrue(s.Exhausted);
            Assert.IsFalse(s.CanSprint);
            s.Tick(true, 1f);
            s.Tick(true, 1f);
            Assert.AreEqual(0.28f, s.Normalized, 1e-4f);
            Assert.IsTrue(s.Exhausted);
            s.Tick(true, 0.2f);
            Assert.IsFalse(s.Exhausted);
            Assert.IsTrue(s.CanSprint);
        }

        [Test]
        public void TrySpend_TakesCost_AndRefusesWhenExhaustedOrShort()
        {
            var s = new StaminaModel(new StaminaTuning());
            Assert.IsTrue(s.TrySpend(8f));
            Assert.AreEqual(0.92f, s.Normalized, 1e-4f);
            Assert.IsFalse(s.TrySpend(500f));
            s.Tick(true, 6f);
            Assert.IsFalse(s.TrySpend(1f));
        }

        [Test]
        public void Refill_RestoresFullAndClearsExhaustion()
        {
            var s = new StaminaModel(new StaminaTuning());
            s.Tick(true, 6f);
            s.Refill();
            Assert.AreEqual(1f, s.Normalized);
            Assert.IsFalse(s.Exhausted);
        }
    }
}
```

`Assets/Player/Tests/CrouchModelTests.cs`:
```csharp
using NUnit.Framework;

namespace TycoWorld.Player.Tests
{
    public class CrouchModelTests
    {
        static CrouchModel Make() => new CrouchModel(new CrouchTuning(), 1.8f);

        [Test]
        public void StartsStanding()
        {
            var c = Make();
            Assert.IsFalse(c.Crouched);
            Assert.AreEqual(1.8f, c.Height);
            Assert.AreEqual(1.6f, c.EyeHeight);
        }

        [Test]
        public void Toggle_CrouchesThenStands()
        {
            var c = Make();
            c.RequestToggle(true);
            Assert.IsTrue(c.Crouched);
            c.RequestToggle(true);
            Assert.IsFalse(c.Crouched);
        }

        [Test]
        public void Toggle_WithoutHeadroom_StaysCrouched()
        {
            var c = Make();
            c.RequestToggle(true);
            c.RequestToggle(false);
            Assert.IsTrue(c.Crouched);
        }

        [Test]
        public void HeightAndEye_MoveTogether()
        {
            var c = Make();
            c.RequestToggle(true);
            c.Tick(0.1f);
            Assert.AreEqual(1.4f, c.Height, 1e-4f);
            Assert.AreEqual(1.225f, c.EyeHeight, 1e-4f);
            c.Tick(0.2f);
            Assert.AreEqual(1f, c.Height, 1e-4f);
            Assert.AreEqual(0.85f, c.EyeHeight, 1e-4f);
            Assert.AreEqual(1f, c.TargetHeight);
        }
    }
}
```

- [ ] **Step 2: Jalankan tes, pastikan gagal**

Procedure R, lalu Procedure T dengan `Group = "StaminaModelTests|CrouchModelTests"`.
Expected: COMPILE FAILED (`CS0246 StaminaModel`, `CrouchModel`).

- [ ] **Step 3: Implementasi**

`Assets/Player/Runtime/Motion/StaminaModel.cs`:
```csharp
using UnityEngine;

namespace TycoWorld.Player
{
    /// <summary>Sprint stamina: drains while sprinting and moving, regenerates after a delay, locks out when empty until a threshold.</summary>
    public sealed class StaminaModel
    {
        readonly StaminaTuning t;
        float current;
        float sinceSpend;

        public StaminaModel(StaminaTuning tuning)
        {
            t = tuning;
            current = t.max;
            sinceSpend = t.regenDelay;
        }

        public float Normalized => t.max > 0f ? current / t.max : 0f;
        public bool Exhausted { get; private set; }
        public bool CanSprint => !Exhausted && current > 0f;

        public void Tick(bool sprintingAndMoving, float dt)
        {
            if (sprintingAndMoving && CanSprint)
            {
                current = Mathf.Max(0f, current - t.sprintDrainPerSecond * dt);
                sinceSpend = 0f;
                if (current <= 0f) Exhausted = true;
            }
            else
            {
                sinceSpend += dt;
                if (sinceSpend >= t.regenDelay) current = Mathf.Min(t.max, current + t.regenPerSecond * dt);
            }
            if (Exhausted && Normalized >= t.recoverThreshold) Exhausted = false;
        }

        public bool TrySpend(float amount)
        {
            if (Exhausted || current < amount) return false;
            current -= amount;
            sinceSpend = 0f;
            if (current <= 0f) Exhausted = true;
            return true;
        }

        public void Refill()
        {
            current = t.max;
            Exhausted = false;
            sinceSpend = t.regenDelay;
        }
    }
}
```

`Assets/Player/Runtime/Motion/CrouchModel.cs`:
```csharp
using UnityEngine;

namespace TycoWorld.Player
{
    /// <summary>Toggle crouch; standing up needs headroom (checked by the caller). Body and eye height move together.</summary>
    public sealed class CrouchModel
    {
        readonly CrouchTuning t;
        readonly float standingHeight;

        public CrouchModel(CrouchTuning tuning, float standingHeight)
        {
            t = tuning;
            this.standingHeight = standingHeight;
            Height = standingHeight;
            EyeHeight = t.standingEyeHeight;
        }

        public bool Crouched { get; private set; }
        public float Height { get; private set; }
        public float EyeHeight { get; private set; }
        public float TargetHeight => Crouched ? t.crouchHeight : standingHeight;

        public void RequestToggle(bool hasHeadroomToStand)
        {
            if (!Crouched) Crouched = true;
            else if (hasHeadroomToStand) Crouched = false;
        }

        public void Tick(float dt)
        {
            Height = Mathf.MoveTowards(Height, TargetHeight, t.transitionSpeed * dt);
            float k = Mathf.InverseLerp(t.crouchHeight, standingHeight, Height);
            EyeHeight = Mathf.Lerp(t.crouchEyeHeight, t.standingEyeHeight, k);
        }
    }
}
```

- [ ] **Step 4: Jalankan tes, pastikan lulus**

Procedure R, lalu Procedure T dengan `Group = "StaminaModelTests|CrouchModelTests"`.
Expected: `passed=10 failed=0`.

- [ ] **Step 5: Commit**

Procedure C:
- path: `Assets/Player`
- subject: `feat(player): stamina and crouch models`

---

### Task 8: `StepCycle`, `HeadBob`, `NoRepeatPicker`

**Files:**
- Create: `Assets/Player/Runtime/Motion/StepCycle.cs`, `Assets/Player/Runtime/Motion/HeadBob.cs`, `Assets/Player/Runtime/Core/NoRepeatPicker.cs`
- Test: `Assets/Player/Tests/StepCycleTests.cs`, `HeadBobTests.cs`, `NoRepeatPickerTests.cs`

**Interfaces:**
- Produces:
  - `StepCycle(float landingMinSpeed)` dengan:
    - `CyclePhase01` (dua langkah per siklus; kontak kaki di 0 dan 0,5) dan `StepCount`;
    - `Advance(float horizontalSpeed, bool grounded, float stride, float dt)` → int, jumlah langkah;
    - `UpdateLanding(bool grounded, float verticalVelocity, out float impactSpeed)` → bool.
  - `HeadBob.Offset(float cyclePhase01, float amplitude, float lateralRatio)` → `Vector3` lokal.
  - `struct NoRepeatPicker(uint seed)` dengan `Next(int count)` → int dan `Range(float min, float max)` → float.

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/Player/Tests/StepCycleTests.cs`:
```csharp
using NUnit.Framework;

namespace TycoWorld.Player.Tests
{
    public class StepCycleTests
    {
        [Test]
        public void Walking_GivesOneStepPerStride()
        {
            var c = new StepCycle(3f);
            int steps = 0;
            for (int i = 0; i < 76; i++) steps += c.Advance(3f, true, 0.75f, 1f / 30f);
            Assert.AreEqual(10, steps);
            Assert.AreEqual(10, c.StepCount);
        }

        [Test]
        public void IdleOrAirborne_GivesNoSteps()
        {
            var c = new StepCycle(3f);
            Assert.AreEqual(0, c.Advance(0f, true, 0.75f, 1f));
            Assert.AreEqual(0, c.Advance(5f, false, 0.75f, 1f));
            Assert.AreEqual(0, c.StepCount);
        }

        [Test]
        public void Phase_WrapsEveryTwoStrides()
        {
            var c = new StepCycle(3f);
            c.Advance(1f, true, 1f, 0.5f);
            Assert.AreEqual(0.25f, c.CyclePhase01, 1e-4f);
            c.Advance(1f, true, 1f, 2f);
            Assert.AreEqual(0.25f, c.CyclePhase01, 1e-4f);
        }

        [Test]
        public void Landing_ReportedOnceWithImpactSpeed()
        {
            var c = new StepCycle(3f);
            Assert.IsFalse(c.UpdateLanding(false, -2f, out _));
            Assert.IsFalse(c.UpdateLanding(false, -5f, out _));
            Assert.IsTrue(c.UpdateLanding(true, -2f, out float impact));
            Assert.AreEqual(5f, impact, 1e-5f);
            Assert.IsFalse(c.UpdateLanding(true, -2f, out _));
        }

        [Test]
        public void Landing_SmallDropIgnored()
        {
            var c = new StepCycle(3f);
            c.UpdateLanding(false, -2.2f, out _);
            Assert.IsFalse(c.UpdateLanding(true, -2f, out _));
        }
    }
}
```

`Assets/Player/Tests/HeadBobTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.Player.Tests
{
    public class HeadBobTests
    {
        [Test]
        public void FootContacts_HaveNoOffset()
        {
            Assert.AreEqual(Vector3.zero, HeadBob.Offset(0f, 0.05f, 0.5f));
            Assert.AreEqual(0f, HeadBob.Offset(0.5f, 0.05f, 0.5f).y, 1e-5f);
        }

        [Test]
        public void MidStep_PeaksAndSwaysSideToSide()
        {
            var a = HeadBob.Offset(0.25f, 0.05f, 0.5f);
            var b = HeadBob.Offset(0.75f, 0.05f, 0.5f);
            Assert.AreEqual(0.05f, a.y, 1e-5f);
            Assert.AreEqual(0.025f, a.x, 1e-5f);
            Assert.AreEqual(a.y, b.y, 1e-5f);
            Assert.AreEqual(-a.x, b.x, 1e-5f);
            Assert.AreEqual(0f, a.z);
        }
    }
}
```

`Assets/Player/Tests/NoRepeatPickerTests.cs`:
```csharp
using System.Collections.Generic;
using NUnit.Framework;

namespace TycoWorld.Player.Tests
{
    public class NoRepeatPickerTests
    {
        [Test]
        public void Next_NeverRepeatsAndCoversAll()
        {
            var picker = new NoRepeatPicker(42u);
            var seen = new HashSet<int>();
            int last = -1;
            for (int i = 0; i < 1000; i++)
            {
                int pick = picker.Next(4);
                Assert.That(pick, Is.InRange(0, 3));
                Assert.AreNotEqual(last, pick);
                seen.Add(pick);
                last = pick;
            }
            Assert.AreEqual(4, seen.Count);
        }

        [Test]
        public void SameSeed_SameSequence()
        {
            var a = new NoRepeatPicker(7u);
            var b = new NoRepeatPicker(7u);
            for (int i = 0; i < 50; i++) Assert.AreEqual(a.Next(5), b.Next(5));
        }

        [Test]
        public void EdgeCounts()
        {
            var p = new NoRepeatPicker(1u);
            Assert.AreEqual(-1, p.Next(0));
            Assert.AreEqual(0, p.Next(1));
            Assert.AreEqual(0, p.Next(1));
        }

        [Test]
        public void Range_StaysInside()
        {
            var p = new NoRepeatPicker(0u);
            for (int i = 0; i < 200; i++) Assert.That(p.Range(0.9f, 1.1f), Is.InRange(0.9f, 1.1f));
        }
    }
}
```

- [ ] **Step 2: Jalankan tes, pastikan gagal**

Procedure R, lalu Procedure T dengan `Group = "StepCycleTests|HeadBobTests|NoRepeatPickerTests"`.
Expected: COMPILE FAILED (`CS0246 StepCycle`, `HeadBob`, `NoRepeatPicker`).

- [ ] **Step 3: Implementasi**

`Assets/Player/Runtime/Motion/StepCycle.cs`:
```csharp
using UnityEngine;

namespace TycoWorld.Player
{
    /// <summary>Distance-based footsteps: one step per stride, two steps per bob cycle; detects hard landings.</summary>
    public sealed class StepCycle
    {
        readonly float landingMinSpeed;
        float cycleDistance;
        bool airborne;
        float peakFallSpeed;

        public StepCycle(float landingMinSpeed) { this.landingMinSpeed = landingMinSpeed; }

        public float CyclePhase01 { get; private set; }
        public int StepCount { get; private set; }

        public int Advance(float horizontalSpeed, bool grounded, float stride, float dt)
        {
            if (!grounded || stride <= 0f || horizontalSpeed <= 0f) return 0;
            float before = cycleDistance;
            cycleDistance += horizontalSpeed * dt;
            int steps = Mathf.FloorToInt(cycleDistance / stride) - Mathf.FloorToInt(before / stride);
            float cycle = stride * 2f;
            cycleDistance %= cycle;
            CyclePhase01 = cycleDistance / cycle;
            StepCount += steps;
            return steps;
        }

        /// <summary>True on the first grounded frame after falling at least <c>landingMinSpeed</c>.</summary>
        public bool UpdateLanding(bool grounded, float verticalVelocity, out float impactSpeed)
        {
            impactSpeed = 0f;
            if (!grounded)
            {
                airborne = true;
                peakFallSpeed = Mathf.Max(peakFallSpeed, -verticalVelocity);
                return false;
            }
            if (!airborne) return false;
            airborne = false;
            float peak = peakFallSpeed;
            peakFallSpeed = 0f;
            if (peak < landingMinSpeed) return false;
            impactSpeed = peak;
            return true;
        }
    }
}
```

`Assets/Player/Runtime/Motion/HeadBob.cs`:
```csharp
using UnityEngine;

namespace TycoWorld.Player
{
    /// <summary>Camera bob: dips at each foot contact (phase 0 and 0.5), sways sideways once per two steps.</summary>
    public static class HeadBob
    {
        public static Vector3 Offset(float cyclePhase01, float amplitude, float lateralRatio)
        {
            float s = Mathf.Sin(2f * Mathf.PI * cyclePhase01);
            return new Vector3(amplitude * lateralRatio * s, amplitude * Mathf.Abs(s), 0f);
        }
    }
}
```

`Assets/Player/Runtime/Core/NoRepeatPicker.cs`:
```csharp
namespace TycoWorld.Player
{
    /// <summary>Deterministic xorshift picker that never returns the same index twice in a row.</summary>
    public struct NoRepeatPicker
    {
        uint state;
        int last;

        public NoRepeatPicker(uint seed)
        {
            state = seed == 0u ? 0x9E3779B9u : seed;
            last = -1;
        }

        uint NextUInt()
        {
            if (state == 0u) state = 0x9E3779B9u;
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }

        public int Next(int count)
        {
            if (count <= 0) return -1;
            if (count == 1)
            {
                last = 0;
                return 0;
            }
            bool avoid = last >= 0 && last < count;
            int pick = (int)(NextUInt() % (uint)(avoid ? count - 1 : count));
            if (avoid && pick >= last) pick++;
            last = pick;
            return pick;
        }

        public float Range(float min, float max) => min + (max - min) * (NextUInt() / (float)uint.MaxValue);
    }
}
```

- [ ] **Step 4: Jalankan tes, pastikan lulus**

Procedure R, lalu Procedure T dengan `Group = "StepCycleTests|HeadBobTests|NoRepeatPickerTests"`.
Expected: `passed=11 failed=0`.

- [ ] **Step 5: Commit**

Procedure C:
- path: `Assets/Player`
- subject: `feat(player): step cycle, head bob and no-repeat picker`

---

### Task 9: `HudModel`, `GateSwing`, `SpawnNudge`

**Files:**
- Create: `Assets/Player/Runtime/Interaction/IPlayerFeedback.cs`, `Assets/Player/Runtime/UI/HudModel.cs`
- Create: `Assets/Player/Runtime/Interaction/GateSwing.cs`, `Assets/Player/Runtime/Core/SpawnNudge.cs`
- Test: `Assets/Player/Tests/HudModelTests.cs`, `GateSwingTests.cs`, `SpawnNudgeTests.cs`

**Interfaces:**
- Consumes: `HudTuning` (Task 6).
- Produces:
  - `interface IPlayerFeedback { void ShowMessage(string text, float seconds = -1f); }`; nilai ≤ 0 berarti memakai durasi default tuning.
  - `HudModel(HudTuning) : IPlayerFeedback` dengan:
    - `Prompt`, `Message`, `StaminaFill`, `StaminaVisible`, `CrosshairVisible` (get/set);
    - `SetPrompt(string)` dan `Tick(float dt, float stamina01)`.
  - `enum GateEvent { Unlatch, Creak, Latch }`.
  - `GateSwing(float openAngleDeg = 100f, float speedDegPerSec = 120f)` dengan:
    - `enum State { Closed, Opening, Open, Closing }`;
    - `Current`, `AngleDeg`, `IsMoving`;
    - `Toggle(float swingSign)` → bool dan `Tick(float dt)` → `IReadOnlyList<GateEvent>`.
  - `SpawnNudge.Candidates(Vector3 origin, float step = 0.5f, float maxRadius = 2f, int perRing = 8)` → `List<Vector3>`.

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/Player/Tests/HudModelTests.cs`:
```csharp
using NUnit.Framework;

namespace TycoWorld.Player.Tests
{
    public class HudModelTests
    {
        static HudModel Make() => new HudModel(new HudTuning());

        [Test]
        public void Defaults_HideStaminaAndShowCrosshair()
        {
            var m = Make();
            Assert.AreEqual("", m.Prompt);
            Assert.AreEqual("", m.Message);
            Assert.IsFalse(m.StaminaVisible);
            Assert.IsTrue(m.CrosshairVisible);
        }

        [Test]
        public void Prompt_SetAndClear()
        {
            var m = Make();
            m.SetPrompt("[E] Buka pagar");
            Assert.AreEqual("[E] Buka pagar", m.Prompt);
            m.SetPrompt(null);
            Assert.AreEqual("", m.Prompt);
        }

        [Test]
        public void Message_ExpiresAfterSeconds()
        {
            var m = Make();
            m.ShowMessage("Pintunya terkunci.", 1f);
            m.Tick(0.5f, 1f);
            Assert.AreEqual("Pintunya terkunci.", m.Message);
            m.Tick(0.6f, 1f);
            Assert.AreEqual("", m.Message);
        }

        [Test]
        public void Message_DefaultDurationComesFromTuning()
        {
            var m = Make();
            m.ShowMessage("Kos Raflesia 3");
            m.Tick(2.4f, 1f);
            Assert.AreEqual("Kos Raflesia 3", m.Message);
            m.Tick(0.2f, 1f);
            Assert.AreEqual("", m.Message);
        }

        [Test]
        public void Stamina_VisibleBelowFull_HiddenAfterDelay()
        {
            var m = Make();
            m.Tick(0.1f, 0.5f);
            Assert.IsTrue(m.StaminaVisible);
            Assert.AreEqual(0.5f, m.StaminaFill, 1e-5f);
            m.Tick(0.5f, 1f);
            Assert.IsTrue(m.StaminaVisible);
            m.Tick(0.4f, 1f);
            Assert.IsFalse(m.StaminaVisible);
        }
    }
}
```

`Assets/Player/Tests/GateSwingTests.cs`:
```csharp
using System.Linq;
using NUnit.Framework;

namespace TycoWorld.Player.Tests
{
    public class GateSwingTests
    {
        [Test]
        public void Opening_EmitsUnlatchAndCreak_ThenOpens()
        {
            var g = new GateSwing();
            Assert.IsTrue(g.Toggle(1f));
            Assert.AreEqual(GateSwing.State.Opening, g.Current);
            CollectionAssert.AreEqual(new[] { GateEvent.Unlatch, GateEvent.Creak }, g.Tick(0.01f).ToArray());
            g.Tick(1f);
            Assert.AreEqual(GateSwing.State.Open, g.Current);
            Assert.AreEqual(100f, g.AngleDeg, 1e-3f);
            Assert.AreEqual(0, g.Tick(0.1f).Count);
        }

        [Test]
        public void NegativeSign_SwingsTheOtherWay()
        {
            var g = new GateSwing();
            g.Toggle(-1f);
            g.Tick(1f);
            Assert.AreEqual(-100f, g.AngleDeg, 1e-3f);
        }

        [Test]
        public void Toggle_IgnoredWhileMoving()
        {
            var g = new GateSwing();
            g.Toggle(1f);
            g.Tick(0.1f);
            Assert.IsTrue(g.IsMoving);
            Assert.IsFalse(g.Toggle(1f));
        }

        [Test]
        public void Closing_EmitsCreakThenLatch()
        {
            var g = new GateSwing();
            g.Toggle(1f);
            g.Tick(1f);
            Assert.IsTrue(g.Toggle(-1f));
            Assert.AreEqual(GateSwing.State.Closing, g.Current);
            CollectionAssert.AreEqual(new[] { GateEvent.Creak, GateEvent.Latch }, g.Tick(1f).ToArray());
            Assert.AreEqual(GateSwing.State.Closed, g.Current);
            Assert.AreEqual(0f, g.AngleDeg, 1e-3f);
        }
    }
}
```

`Assets/Player/Tests/SpawnNudgeTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.Player.Tests
{
    public class SpawnNudgeTests
    {
        [Test]
        public void Candidates_StartAtOrigin_AndGrowOutward()
        {
            var origin = new Vector3(3f, 1f, -2f);
            var list = SpawnNudge.Candidates(origin);
            Assert.AreEqual(33, list.Count);
            Assert.AreEqual(origin, list[0]);
            float previous = 0f;
            foreach (var c in list)
            {
                float d = Vector3.Distance(origin, c);
                Assert.GreaterOrEqual(d + 1e-4f, previous);
                Assert.LessOrEqual(d, 2f + 1e-4f);
                Assert.AreEqual(origin.y, c.y);
                previous = d;
            }
        }
    }
}
```

- [ ] **Step 2: Jalankan tes, pastikan gagal**

Procedure R, lalu Procedure T dengan `Group = "HudModelTests|GateSwingTests|SpawnNudgeTests"`.
Expected: COMPILE FAILED (`CS0246 HudModel`, `GateSwing`, `GateEvent`, `SpawnNudge`).

- [ ] **Step 3: Implementasi**

`Assets/Player/Runtime/Interaction/IPlayerFeedback.cs`:
```csharp
namespace TycoWorld.Player
{
    /// <summary>Short text feedback to the player (HUD). seconds ≤ 0 uses the tuning default.</summary>
    public interface IPlayerFeedback
    {
        void ShowMessage(string text, float seconds = -1f);
    }
}
```

`Assets/Player/Runtime/UI/HudModel.cs`:
```csharp
using UnityEngine;

namespace TycoWorld.Player
{
    /// <summary>Everything the HUD shows, without any UI code (rendered by HudView).</summary>
    public sealed class HudModel : IPlayerFeedback
    {
        readonly HudTuning t;
        float messageTimeLeft;
        float sinceFull;

        public HudModel(HudTuning tuning)
        {
            t = tuning;
            sinceFull = tuning.staminaHideDelay;
        }

        public string Prompt { get; private set; } = "";
        public string Message { get; private set; } = "";
        public float StaminaFill { get; private set; } = 1f;
        public bool StaminaVisible { get; private set; }
        public bool CrosshairVisible { get; set; } = true;

        public void SetPrompt(string prompt) => Prompt = prompt ?? "";

        public void ShowMessage(string text, float seconds = -1f)
        {
            Message = text ?? "";
            messageTimeLeft = seconds > 0f ? seconds : t.messageSeconds;
        }

        public void Tick(float dt, float stamina01)
        {
            if (messageTimeLeft > 0f)
            {
                messageTimeLeft -= dt;
                if (messageTimeLeft <= 0f) Message = "";
            }
            StaminaFill = Mathf.Clamp01(stamina01);
            if (StaminaFill < 0.999f)
            {
                sinceFull = 0f;
                StaminaVisible = true;
            }
            else
            {
                sinceFull += dt;
                StaminaVisible = sinceFull < t.staminaHideDelay;
            }
        }
    }
}
```

`Assets/Player/Runtime/Interaction/GateSwing.cs`:
```csharp
using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.Player
{
    public enum GateEvent { Unlatch, Creak, Latch }

    /// <summary>Hinged gate state machine. Angle is around the hinge's Y axis; sign picks the swing direction.</summary>
    public sealed class GateSwing
    {
        public enum State { Closed, Opening, Open, Closing }

        readonly float openAngle;
        readonly float speed;
        readonly List<GateEvent> pending = new List<GateEvent>();
        readonly List<GateEvent> emitted = new List<GateEvent>();
        float sign = 1f;

        public GateSwing(float openAngleDeg = 100f, float speedDegPerSec = 120f)
        {
            openAngle = openAngleDeg;
            speed = speedDegPerSec;
        }

        public State Current { get; private set; } = State.Closed;
        public float AngleDeg { get; private set; }
        public bool IsMoving => Current == State.Opening || Current == State.Closing;

        public bool Toggle(float swingSign)
        {
            if (IsMoving) return false;
            if (Current == State.Closed)
            {
                sign = swingSign >= 0f ? 1f : -1f;
                Current = State.Opening;
                pending.Add(GateEvent.Unlatch);
                pending.Add(GateEvent.Creak);
            }
            else
            {
                Current = State.Closing;
                pending.Add(GateEvent.Creak);
            }
            return true;
        }

        public IReadOnlyList<GateEvent> Tick(float dt)
        {
            emitted.Clear();
            emitted.AddRange(pending);
            pending.Clear();
            if (Current == State.Opening)
            {
                AngleDeg = Mathf.MoveTowards(AngleDeg, openAngle * sign, speed * dt);
                if (Mathf.Approximately(AngleDeg, openAngle * sign)) Current = State.Open;
            }
            else if (Current == State.Closing)
            {
                AngleDeg = Mathf.MoveTowards(AngleDeg, 0f, speed * dt);
                if (Mathf.Approximately(AngleDeg, 0f))
                {
                    AngleDeg = 0f;
                    Current = State.Closed;
                    emitted.Add(GateEvent.Latch);
                }
            }
            return emitted;
        }
    }
}
```

`Assets/Player/Runtime/Core/SpawnNudge.cs`:
```csharp
using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.Player
{
    /// <summary>Spawn fallbacks: the desired point first, then rings of candidates nearest-first.</summary>
    public static class SpawnNudge
    {
        public static List<Vector3> Candidates(Vector3 origin, float step = 0.5f, float maxRadius = 2f, int perRing = 8)
        {
            var list = new List<Vector3> { origin };
            int rings = Mathf.FloorToInt(maxRadius / step + 1e-4f);
            for (int ring = 1; ring <= rings; ring++)
            {
                float r = ring * step;
                for (int i = 0; i < perRing; i++)
                {
                    float a = 2f * Mathf.PI * i / perRing;
                    list.Add(origin + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
                }
            }
            return list;
        }
    }
}
```

- [ ] **Step 4: Jalankan tes, pastikan lulus**

Procedure R, lalu Procedure T dengan `Group = "HudModelTests|GateSwingTests|SpawnNudgeTests"`.
Expected: `passed=10 failed=0`.

- [ ] **Step 5: Commit**

Procedure C:
- path: `Assets/Player`
- subject: `feat(player): hud model, gate swing and spawn nudge`

---
### Task 10: `FirstPersonPlayer` (orchestrator + CharacterController), `GroundProbe`, `PlayerInputReader`

**Files:**
- Create: `Assets/Player/Runtime/Core/IPlayerModule.cs`, `GroundProbe.cs`, `FirstPersonPlayer.cs`
- Create: `Assets/Player/Runtime/Input/PlayerInputReader.cs`
- Create: `Assets/Player/Tests/TestRig.cs`
- Test: `Assets/Player/Tests/FirstPersonPlayerTests.cs`, `GroundProbeTests.cs`, `PlayerInputReaderTests.cs`

**Interfaces:**
- Consumes: semua model dari Task 5–9, `PlayerTuning` (Task 6), `GroundSurface`/`SurfaceType`/`OsmFeature` (WorldGen).
- Produces:
  - `interface IPlayerModule { int Order { get; } void Initialize(FirstPersonPlayer player); void Tick(FirstPersonPlayer player, float dt); }`. Modul dikumpulkan dari anak-anak pemain saat `Initialize`, lalu diurutkan berdasarkan `Order`.
  - `struct GroundInfo { bool Hit; SurfaceType Surface; OsmFeature Feature; Collider Collider; Vector3 Point; }`.
  - `GroundProbe.Probe(Vector3 from, float distance, out GroundInfo info)` → bool, `GroundProbe.ClearCache()`.
  - `PlayerInputReader(InputActionAsset, LookTuning) : IPlayerIntentSource` dengan `Enable()`, `Disable()`, `CursorLocked`, `AttackPressed`, `ReleaseCursorPressed`, dan static `LookFromPointer(Vector2 pixels, float degreesPerPixel, bool cursorLocked)`.
  - `FirstPersonPlayer` (MonoBehaviour, `[RequireComponent(CharacterController)]`):
    - Setup: `Configure(PlayerTuning, Transform pivot, Transform view)`, `Initialize()`, `SetIntentSource(IPlayerIntentSource)` (null berarti pembaca input asli), `Tick(float dt)`, `Teleport(Vector3 feet, float yawDegrees)`, `LockCursor()`, `UnlockCursor()`.
    - State: `Tuning`, `CameraPivot`, `View`, `IsInitialized`, `Locks`, `Stamina`, `Hud`, `Intent`, `Ground`, `IsGrounded`, `IsCrouched`, `IsSprinting`, `StepsThisFrame`, `StepCount`, `JumpedThisFrame`, `LandedThisFrame`, `LandingImpact`, `Velocity`, `Yaw`, `Pitch`, `Modules`.
    - `event Action FellOutOfWorld`.
  - `TestRig` (tes): `Create(PhysicsFixture, Vector3 localFeet, float yaw = 0f)`, `Init()`, `Run(float seconds, float dt = 1/60)`, `Local`, `Root`, `Player`, `Pivot`, `View`, `Intents`, `Tuning`.

- [ ] **Step 1: Spike: apakah `CharacterController.Move` jalan di EditMode?**

Buat dulu `Assets/Player/Tests/TestRig.cs`, `Assets/Player/Runtime/Core/IPlayerModule.cs`, dan tes pertama saja, yaitu `CharacterController_MoveInEditMode_IsBlockedByWall` dari Step 2, lalu implementasi Step 4. Jalankan tes itu sendirian (`Group = "CharacterController_MoveInEditMode"`).
- **Kalau lulus:** lanjutkan seperti tertulis.
- **Kalau gagal** karena CharacterController tidak bergerak di EditMode (z tetap 0):
  - beri atribut `[Ignore("CharacterController butuh Play mode — diverifikasi di Task 19")]` pada semua tes di `FirstPersonPlayerTests` yang memanggil `rig.Run`/`Tick` dengan gerakan;
  - catat di ledger: `Task 10: Ruling: CC tidak bergerak di EditMode; tes gerak dipindah ke Task 19 Step 3`;
  - salin daftar tes yang di-Ignore ke checklist Task 19.

  Tes logika murni (Task 6–9) tetap menjaga matematika gerak.

- [ ] **Step 2: Tulis tes yang gagal**

`Assets/Player/Tests/TestRig.cs`:
```csharp
using UnityEngine;

namespace TycoWorld.Player.Tests
{
    /// <summary>A code-built player for physics tests; add modules after Create and before Init.</summary>
    internal sealed class TestRig
    {
        public readonly GameObject Root;
        public readonly FirstPersonPlayer Player;
        public readonly Transform Pivot, View;
        public readonly PlayerTuning Tuning;
        public readonly ScriptedIntentSource Intents = new ScriptedIntentSource();

        TestRig(GameObject root, FirstPersonPlayer player, Transform pivot, Transform view, PlayerTuning tuning)
        {
            Root = root;
            Player = player;
            Pivot = pivot;
            View = view;
            Tuning = tuning;
        }

        public Vector3 Local => Root.transform.position - PhysicsFixture.Origin;

        public static TestRig Create(PhysicsFixture fx, Vector3 localFeet, float yaw = 0f)
        {
            var tuning = fx.Track(ScriptableObject.CreateInstance<PlayerTuning>());
            var root = fx.Track(new GameObject("Player"));
            root.layer = PlayerLayers.Player;
            root.transform.SetPositionAndRotation(PhysicsFixture.Origin + localFeet, Quaternion.Euler(0f, yaw, 0f));
            var pivot = new GameObject("CameraPivot").transform;
            pivot.SetParent(root.transform, false);
            pivot.localPosition = new Vector3(0f, 1.6f, 0f);
            var view = new GameObject("Camera").transform;
            view.SetParent(pivot, false);
            var player = root.AddComponent<FirstPersonPlayer>();
            player.Configure(tuning, pivot, view);
            return new TestRig(root, player, pivot, view, tuning);
        }

        public TestRig Init()
        {
            Player.Initialize();
            Player.SetIntentSource(Intents);
            Player.Teleport(Root.transform.position, Root.transform.eulerAngles.y);
            return this;
        }

        public void Run(float seconds, float dt = 1f / 60f)
        {
            int frames = Mathf.Max(1, Mathf.RoundToInt(seconds / dt));
            for (int i = 0; i < frames; i++) Player.Tick(dt);
        }
    }
}
```

`Assets/Player/Tests/FirstPersonPlayerTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.Player.Tests
{
    public class FirstPersonPlayerTests
    {
        PhysicsFixture fx;

        [SetUp]
        public void SetUp()
        {
            fx = new PhysicsFixture();
            GroundProbe.ClearCache();
        }

        [TearDown]
        public void TearDown()
        {
            fx.Dispose();
            GroundProbe.ClearCache();
        }

        TestRig FloorRig(float yaw = 0f)
        {
            fx.Floor();
            fx.Sync();
            return TestRig.Create(fx, Vector3.zero, yaw).Init();
        }

        static PlayerIntent Forward(bool sprint = false) => new PlayerIntent { Move = Vector2.up, Sprint = sprint };

        GameObject Ribbon(float y, float zStart, float zEnd)
        {
            var mesh = fx.Track(new UnityEngine.Mesh());
            mesh.vertices = new[] { new Vector3(-5f, y, zStart), new Vector3(5f, y, zStart), new Vector3(5f, y, zEnd), new Vector3(-5f, y, zEnd) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            var go = fx.Track(new GameObject("Ribbon"));
            go.transform.position = PhysicsFixture.Origin;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        [Test]
        public void CharacterController_MoveInEditMode_IsBlockedByWall()
        {
            fx.Floor();
            fx.Box("Wall", new Vector3(0f, 1.5f, 3f), new Vector3(10f, 3f, 0.2f));
            fx.Sync();
            var rig = TestRig.Create(fx, Vector3.zero).Init();
            rig.Intents.Hold(Forward(), 3f);
            rig.Run(3f);
            Assert.Greater(rig.Local.z, 2f, "moved");
            Assert.Less(rig.Local.z, 2.61f, "blocked by wall face at z = 2.9");
        }

        [Test]
        public void Tick_WalkForward_MovesExpectedDistance()
        {
            var rig = FloorRig();
            rig.Intents.Hold(Forward(), 2f);
            rig.Run(2f);
            Assert.AreEqual(5.625f, rig.Local.z, 0.3f);
            Assert.AreEqual(0f, rig.Local.x, 0.05f);
            Assert.IsTrue(rig.Player.IsGrounded);
        }

        [Test]
        public void Tick_LargeDt_DoesNotPassThroughWall()
        {
            fx.Floor();
            fx.Box("Wall", new Vector3(0f, 1.5f, 3f), new Vector3(10f, 3f, 0.2f));
            fx.Sync();
            var rig = TestRig.Create(fx, Vector3.zero).Init();
            rig.Intents.Hold(Forward(true), 3f);
            rig.Run(3f, 0.33f);
            Assert.Less(rig.Local.z, 2.61f);
        }

        [Test]
        public void Tick_StepsOntoThinRibbon()
        {
            fx.Floor();
            Ribbon(0.08f, 2f, 12f);
            fx.Sync();
            var rig = TestRig.Create(fx, Vector3.zero).Init();
            rig.Intents.Hold(Forward(), 2.5f);
            rig.Run(2.5f);
            Assert.Greater(rig.Local.z, 3f);
            Assert.That(rig.Local.y, Is.InRange(0.06f, 0.16f), "on the 0.08 m ribbon (within skin width)");
            Assert.IsTrue(rig.Player.IsGrounded);
        }

        [Test]
        public void Tick_WalkingOffRibbon_NoLanding()
        {
            fx.Floor();
            Ribbon(0.08f, -2f, 1f);
            fx.Sync();
            var rig = TestRig.Create(fx, new Vector3(0f, 0.1f, 0f)).Init();
            rig.Intents.Hold(Forward(), 2f);
            bool landed = false;
            for (int i = 0; i < 120; i++)
            {
                rig.Player.Tick(1f / 60f);
                landed |= rig.Player.LandedThisFrame;
            }
            Assert.Greater(rig.Local.z, 2f);
            Assert.That(rig.Local.y, Is.InRange(-0.01f, 0.07f), "back on the floor (within skin width)");
            Assert.IsFalse(landed);
        }

        [Test]
        public void Crouch_LowersBodyAndEye()
        {
            var rig = FloorRig();
            rig.Intents.Press(new PlayerIntent { CrouchToggle = true });
            rig.Run(0.5f);
            Assert.IsTrue(rig.Player.IsCrouched);
            Assert.AreEqual(1f, rig.Root.GetComponent<CharacterController>().height, 1e-3f);
            Assert.AreEqual(0.85f, rig.Pivot.localPosition.y, 1e-3f);
        }

        [Test]
        public void Crouch_UnderLowCeiling_CannotStand()
        {
            fx.Floor();
            fx.Box("Ceiling", new Vector3(0f, 1.55f, 5.5f), new Vector3(10f, 0.5f, 5f));
            fx.Sync();
            var rig = TestRig.Create(fx, Vector3.zero).Init();
            rig.Intents.Press(new PlayerIntent { CrouchToggle = true });
            rig.Run(0.5f);
            rig.Intents.Hold(Forward(), 3f);
            rig.Run(3f);
            Assert.Greater(rig.Local.z, 3.5f, "walked under the ceiling");
            rig.Intents.Press(new PlayerIntent { CrouchToggle = true });
            rig.Run(0.5f);
            Assert.IsTrue(rig.Player.IsCrouched);
            Assert.AreEqual(1f, rig.Root.GetComponent<CharacterController>().height, 1e-3f);
        }

        [Test]
        public void Teleport_SurvivesNextMove()
        {
            var rig = FloorRig();
            rig.Player.Teleport(PhysicsFixture.Origin + new Vector3(10f, 0f, 10f), 90f);
            rig.Player.Tick(1f / 60f);
            Assert.AreEqual(10f, rig.Local.x, 0.05f);
            Assert.AreEqual(10f, rig.Local.z, 0.05f);
            Assert.AreEqual(90f, rig.Player.Yaw, 1e-3f);
        }

        [Test]
        public void MoveLock_DisablesBodyAndFreezesPosition()
        {
            var rig = FloorRig();
            var owner = new object();
            rig.Player.Locks.Acquire(owner, PlayerAbility.Move);
            var body = rig.Root.GetComponent<CharacterController>();
            Assert.IsFalse(body.enabled);
            rig.Intents.Hold(Forward(), 1f);
            rig.Run(1f);
            Assert.AreEqual(0f, rig.Local.z, 1e-3f);
            rig.Player.Locks.Release(owner);
            Assert.IsTrue(body.enabled);
        }

        [Test]
        public void Jump_LeavesGroundAndSpendsStamina()
        {
            var rig = FloorRig();
            rig.Run(0.2f);
            rig.Intents.Press(new PlayerIntent { Jump = true });
            rig.Run(0.25f);
            Assert.Greater(rig.Local.y, 0.4f);
            Assert.AreEqual(0.92f, rig.Player.Stamina.Normalized, 0.01f);
        }

        [Test]
        public void Sprint_DrainsStaminaAndIsFaster()
        {
            var rig = FloorRig();
            rig.Intents.Hold(Forward(true), 1f);
            rig.Run(1f);
            Assert.AreEqual(0.82f, rig.Player.Stamina.Normalized, 0.02f);
            Assert.Greater(new Vector3(rig.Player.Velocity.x, 0f, rig.Player.Velocity.z).magnitude, 5f);
            Assert.IsTrue(rig.Player.Hud.StaminaVisible);
        }

        [Test]
        public void Look_RotatesBodyYawAndPivotPitch()
        {
            var rig = FloorRig();
            rig.Intents.Press(new PlayerIntent { LookDegrees = new Vector2(30f, 10f) });
            rig.Player.Tick(1f / 60f);
            Assert.AreEqual(30f, rig.Player.Yaw, 1e-3f);
            Assert.AreEqual(10f, rig.Player.Pitch, 1e-3f);
            Assert.AreEqual(30f, rig.Root.transform.eulerAngles.y, 1e-2f);
            Assert.AreEqual(350f, rig.Pivot.localEulerAngles.x, 1e-2f);
        }

        [Test]
        public void Walking_CountsStepsAndBobs_ThenBobFades()
        {
            var rig = FloorRig();
            rig.Intents.Hold(Forward(), 3f);
            float maxBob = 0f;
            for (int i = 0; i < 180; i++)
            {
                rig.Player.Tick(1f / 60f);
                maxBob = Mathf.Max(maxBob, rig.View.localPosition.y);
            }
            Assert.That(rig.Player.StepCount, Is.InRange(10, 12));
            Assert.Greater(maxBob, 0.02f);
            rig.Run(1f);
            Assert.AreEqual(0f, rig.View.localPosition.magnitude, 1e-3f);
        }

        [Test]
        public void FellOutOfWorld_RaisedBelowThreshold()
        {
            var rig = TestRig.Create(fx, Vector3.zero).Init();
            int fell = 0;
            rig.Player.FellOutOfWorld += () => fell++;
            rig.Player.Teleport(new Vector3(5000f, -25f, 5000f), 0f);
            rig.Player.Tick(1f / 60f);
            Assert.GreaterOrEqual(fell, 1);
        }

        [Test]
        public void Ground_IsProbedEveryTick()
        {
            var floor = fx.Floor();
            floor.AddComponent<TycoWorld.WorldGen.GroundSurface>().Initialize(TycoWorld.WorldGen.SurfaceType.Concrete);
            fx.Sync();
            var rig = TestRig.Create(fx, Vector3.zero).Init();
            rig.Player.Tick(1f / 60f);
            Assert.IsTrue(rig.Player.Ground.Hit);
            Assert.AreEqual(TycoWorld.WorldGen.SurfaceType.Concrete, rig.Player.Ground.Surface);
        }
    }
}
```

`Assets/Player/Tests/GroundProbeTests.cs`:
```csharp
using NUnit.Framework;
using TycoWorld.WorldGen;
using UnityEngine;

namespace TycoWorld.Player.Tests
{
    public class GroundProbeTests
    {
        PhysicsFixture fx;
        static Vector3 From => PhysicsFixture.Origin + Vector3.up * 0.5f;

        [SetUp]
        public void SetUp()
        {
            fx = new PhysicsFixture();
            GroundProbe.ClearCache();
        }

        [TearDown]
        public void TearDown()
        {
            fx.Dispose();
            GroundProbe.ClearCache();
        }

        [Test]
        public void Probe_ReturnsSurfaceAndFeature()
        {
            var box = fx.Floor();
            box.AddComponent<GroundSurface>().Initialize(SurfaceType.Grass);
            box.AddComponent<OsmFeature>().Initialize(77, OsmElementType.Way, FeatureKind.Area, "lapangan", null);
            fx.Sync();
            Assert.IsTrue(GroundProbe.Probe(From, 2f, out var info));
            Assert.IsTrue(info.Hit);
            Assert.AreEqual(SurfaceType.Grass, info.Surface);
            Assert.AreEqual(77, info.Feature.OsmId);
            Assert.AreSame(box.GetComponent<BoxCollider>(), info.Collider);
            Assert.AreEqual(PhysicsFixture.Origin.y, info.Point.y, 1e-3f);
        }

        [Test]
        public void Probe_PlainCollider_IsDefault()
        {
            fx.Floor();
            fx.Sync();
            Assert.IsTrue(GroundProbe.Probe(From, 2f, out var info));
            Assert.AreEqual(SurfaceType.Default, info.Surface);
            Assert.IsNull(info.Feature);
        }

        [Test]
        public void Probe_IgnoresTriggersAndPlayerLayer()
        {
            fx.Floor().AddComponent<GroundSurface>().Initialize(SurfaceType.Dirt);
            fx.Box("Trigger", new Vector3(0f, 0.2f, 0f), new Vector3(2f, 0.1f, 2f)).GetComponent<BoxCollider>().isTrigger = true;
            fx.Box("PlayerPart", new Vector3(0f, 0.3f, 0f), new Vector3(2f, 0.1f, 2f)).layer = PlayerLayers.Player;
            fx.Sync();
            Assert.IsTrue(GroundProbe.Probe(From, 2f, out var info));
            Assert.AreEqual(SurfaceType.Dirt, info.Surface);
        }

        [Test]
        public void Probe_TopMostSurfaceWins()
        {
            fx.Floor().AddComponent<GroundSurface>().Initialize(SurfaceType.Dirt);
            fx.Box("Road", new Vector3(0f, 0.04f, 0f), new Vector3(2f, 0.08f, 2f)).AddComponent<GroundSurface>().Initialize(SurfaceType.Asphalt);
            fx.Sync();
            Assert.IsTrue(GroundProbe.Probe(From, 2f, out var info));
            Assert.AreEqual(SurfaceType.Asphalt, info.Surface);
        }

        [Test]
        public void Probe_ChildCollider_UsesParentSurface()
        {
            var parent = fx.Track(new GameObject("Area"));
            parent.AddComponent<GroundSurface>().Initialize(SurfaceType.Paving);
            var child = fx.Box("Collider", new Vector3(0f, -0.5f, 0f), new Vector3(4f, 1f, 4f));
            child.transform.SetParent(parent.transform, true);
            fx.Sync();
            Assert.IsTrue(GroundProbe.Probe(From, 2f, out var info));
            Assert.AreEqual(SurfaceType.Paving, info.Surface);
        }

        [Test]
        public void Probe_AfterClearCache_DoesNotReturnDestroyedCollider()
        {
            var grass = fx.Floor();
            grass.AddComponent<GroundSurface>().Initialize(SurfaceType.Grass);
            fx.Sync();
            Assert.IsTrue(GroundProbe.Probe(From, 2f, out _));
            Object.DestroyImmediate(grass);
            GroundProbe.ClearCache();
            var plain = fx.Floor();
            fx.Sync();
            Assert.IsTrue(GroundProbe.Probe(From, 2f, out var info));
            Assert.AreEqual(SurfaceType.Default, info.Surface);
            Assert.AreSame(plain.GetComponent<BoxCollider>(), info.Collider);
        }

        [Test]
        public void Probe_Miss_ReturnsFalse()
        {
            Assert.IsFalse(GroundProbe.Probe(From, 2f, out var info));
            Assert.IsFalse(info.Hit);
        }
    }
}
```

`Assets/Player/Tests/PlayerInputReaderTests.cs`:
```csharp
using NUnit.Framework;
using TycoWorld.Player.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TycoWorld.Player.Tests
{
    public class PlayerInputReaderTests
    {
        [Test]
        public void LookFromPointer_Locked_ScalesPixelsToDegrees() =>
            Assert.AreEqual(new Vector2(1f, -0.5f), PlayerInputReader.LookFromPointer(new Vector2(10f, -5f), 0.1f, true));

        [Test]
        public void LookFromPointer_Unlocked_IsZero() =>
            Assert.AreEqual(Vector2.zero, PlayerInputReader.LookFromPointer(new Vector2(300f, 200f), 0.1f, false));

        [Test]
        public void Constructor_FindsActions_AndIdleReadIsEmpty()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(PlayerPaths.InputActionsAsset);
            var reader = new PlayerInputReader(asset, new LookTuning());
            var intent = reader.Read(0.016f);
            Assert.AreEqual(Vector2.zero, intent.Move);
            Assert.IsFalse(intent.Jump || intent.Interact || intent.Flashlight || intent.CrouchToggle || intent.Sprint);
        }
    }
}
```

- [ ] **Step 3: Jalankan tes, pastikan gagal**

Procedure R, lalu Procedure T dengan `Group = "FirstPersonPlayerTests|GroundProbeTests|PlayerInputReaderTests"`.
Expected: COMPILE FAILED (`CS0246 FirstPersonPlayer`, `GroundProbe`, `PlayerInputReader`).

- [ ] **Step 4: Implementasi**

`Assets/Player/Runtime/Core/IPlayerModule.cs`:
```csharp
namespace TycoWorld.Player
{
    /// <summary>Optional player part (footsteps, interaction, flashlight, HUD) ticked by FirstPersonPlayer after movement, by ascending Order.</summary>
    public interface IPlayerModule
    {
        int Order { get; }
        void Initialize(FirstPersonPlayer player);
        void Tick(FirstPersonPlayer player, float dt);
    }
}
```

`Assets/Player/Runtime/Core/GroundProbe.cs`:
```csharp
using System.Collections.Generic;
using TycoWorld.WorldGen;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TycoWorld.Player
{
    public struct GroundInfo
    {
        public bool Hit;
        public SurfaceType Surface;
        public OsmFeature Feature;
        public Collider Collider;
        public Vector3 Point;
    }

    /// <summary>Downward ray that reports the walkable surface and OSM feature under a point (cached per collider).</summary>
    public static class GroundProbe
    {
        struct Entry
        {
            public SurfaceType Surface;
            public OsmFeature Feature;
        }

        static readonly Dictionary<Collider, Entry> cache = new Dictionary<Collider, Entry>();
        static bool hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            cache.Clear();
            hooked = false;
        }

        public static bool Probe(Vector3 from, float distance, out GroundInfo info)
        {
            EnsureHooked();
            info = default;
            if (!Physics.Raycast(from, Vector3.down, out var hit, distance, PlayerLayers.QueryMask, QueryTriggerInteraction.Ignore))
                return false;
            if (!cache.TryGetValue(hit.collider, out var entry))
            {
                var surface = hit.collider.GetComponentInParent<GroundSurface>();
                entry = new Entry
                {
                    Surface = surface != null ? surface.Type : SurfaceType.Default,
                    Feature = hit.collider.GetComponentInParent<OsmFeature>()
                };
                cache[hit.collider] = entry;
            }
            info = new GroundInfo { Hit = true, Surface = entry.Surface, Feature = entry.Feature, Collider = hit.collider, Point = hit.point };
            return true;
        }

        public static void ClearCache() => cache.Clear();

        static void EnsureHooked()
        {
            if (hooked) return;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            hooked = true;
        }

        static void OnSceneUnloaded(Scene scene) => cache.Clear();
    }
}
```

`Assets/Player/Runtime/Input/PlayerInputReader.cs`:
```csharp
using UnityEngine;
using UnityEngine.InputSystem;

namespace TycoWorld.Player
{
    /// <summary>Reads the project-wide "Player" action map into a PlayerIntent (keyboard + mouse).</summary>
    public sealed class PlayerInputReader : IPlayerIntentSource
    {
        readonly InputActionMap map;
        readonly InputAction move, look, sprint, jump, crouch, interact, flashlight, attack;
        readonly LookTuning tuning;

        public PlayerInputReader(InputActionAsset asset, LookTuning lookTuning)
        {
            map = asset.FindActionMap("Player", true);
            move = map.FindAction("Move", true);
            look = map.FindAction("Look", true);
            sprint = map.FindAction("Sprint", true);
            jump = map.FindAction("Jump", true);
            crouch = map.FindAction("Crouch", true);
            interact = map.FindAction("Interact", true);
            flashlight = map.FindAction("Flashlight", true);
            attack = map.FindAction("Attack", true);
            tuning = lookTuning;
        }

        /// <summary>Mouse look only applies while the cursor is locked (Esc / alt-tab must not spin the camera).</summary>
        public bool CursorLocked { get; set; } = true;

        public bool AttackPressed => attack.WasPressedThisFrame();

        public bool ReleaseCursorPressed => Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;

        public void Enable() => map.Enable();

        public void Disable() => map.Disable();

        public PlayerIntent Read(float dt) => new PlayerIntent
        {
            Move = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f),
            LookDegrees = LookFromPointer(look.ReadValue<Vector2>(), tuning.degreesPerPixel, CursorLocked),
            Sprint = sprint.IsPressed(),
            Jump = jump.WasPressedThisFrame(),
            CrouchToggle = crouch.WasPressedThisFrame(),
            Interact = interact.WasPressedThisFrame(),
            Flashlight = flashlight.WasPressedThisFrame()
        };

        public static Vector2 LookFromPointer(Vector2 pixels, float degreesPerPixel, bool cursorLocked) =>
            cursorLocked ? pixels * degreesPerPixel : Vector2.zero;
    }
}
```

`Assets/Player/Runtime/Core/FirstPersonPlayer.cs`:
```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TycoWorld.Player
{
    /// <summary>
    /// The single per-frame entry point of the player: intent → look → crouch → move → stamina → steps/bob →
    /// ground probe → HUD model → modules (by Order) → fall check. Spec #2 §4.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public sealed class FirstPersonPlayer : MonoBehaviour
    {
        [SerializeField] PlayerTuning tuning;
        [SerializeField] Transform cameraPivot;
        [SerializeField] Transform cameraView;

        readonly List<IPlayerModule> modules = new List<IPlayerModule>();
        CharacterController body;
        MovementModel movement;
        CrouchModel crouch;
        StepCycle steps;
        IPlayerIntentSource scriptedSource;
        PlayerInputReader reader;
        Vector2 yawPitch;
        float bobWeight;

        public event Action FellOutOfWorld;

        public PlayerTuning Tuning => tuning;
        public Transform CameraPivot => cameraPivot;
        public Transform View => cameraView;
        public bool IsInitialized { get; private set; }
        public ControlLocks Locks { get; private set; }
        public StaminaModel Stamina { get; private set; }
        public HudModel Hud { get; private set; }
        public PlayerIntent Intent { get; private set; }
        public GroundInfo Ground { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsCrouched => crouch != null && crouch.Crouched;
        public bool IsSprinting { get; private set; }
        public int StepsThisFrame { get; private set; }
        public int StepCount => steps != null ? steps.StepCount : 0;
        public bool JumpedThisFrame { get; private set; }
        public bool LandedThisFrame { get; private set; }
        public float LandingImpact { get; private set; }
        public Vector3 Velocity => movement != null ? movement.Velocity : Vector3.zero;
        public float Yaw => yawPitch.x;
        public float Pitch => yawPitch.y;
        public IReadOnlyList<IPlayerModule> Modules => modules;

        public void Configure(PlayerTuning playerTuning, Transform pivot, Transform view)
        {
            tuning = playerTuning;
            cameraPivot = pivot;
            cameraView = view;
        }

        void Awake() => Initialize();

        void OnEnable()
        {
            if (reader != null) reader.Enable();
        }

        void OnDisable()
        {
            if (reader != null) reader.Disable();
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            HandleCursor();
            Tick(Time.deltaTime);
        }

        public void Initialize()
        {
            if (IsInitialized) return;
            if (tuning == null)
            {
                Debug.LogWarning("[Player] PlayerTuning kosong; memakai nilai default.");
                tuning = ScriptableObject.CreateInstance<PlayerTuning>();
                tuning.hideFlags = HideFlags.DontSave;
            }
            body = GetComponent<CharacterController>();
            var b = tuning.body;
            body.radius = b.radius;
            body.stepOffset = b.stepOffset;
            body.skinWidth = b.skinWidth;
            body.slopeLimit = b.slopeLimit;
            body.minMoveDistance = 0f;

            Locks = new ControlLocks();
            Locks.Changed += OnLocksChanged;
            Stamina = new StaminaModel(tuning.stamina);
            Hud = new HudModel(tuning.hud);
            movement = new MovementModel(tuning.movement);
            crouch = new CrouchModel(tuning.crouch, b.height);
            steps = new StepCycle(tuning.bob.landingMinSpeed);
            yawPitch = new Vector2(Mathf.Repeat(transform.eulerAngles.y, 360f), 0f);
            ApplyCrouchHeight();

            modules.Clear();
            modules.AddRange(GetComponentsInChildren<IPlayerModule>(true));
            modules.Sort((x, y) => x.Order.CompareTo(y.Order));
            IsInitialized = true;
            foreach (var module in modules) module.Initialize(this);
        }

        /// <summary>null restores the real keyboard/mouse reader.</summary>
        public void SetIntentSource(IPlayerIntentSource source) => scriptedSource = source;

        public void Tick(float dt)
        {
            if (!IsInitialized) Initialize();
            if (dt <= 0f) return;
            var source = ActiveSource;
            Intent = Filter(source != null ? source.Read(dt) : default);

            yawPitch = LookModel.Apply(yawPitch, Intent.LookDegrees, tuning.look.pitchLimit);
            transform.rotation = Quaternion.Euler(0f, yawPitch.x, 0f);
            if (cameraPivot != null) cameraPivot.localRotation = LookModel.PitchRotation(yawPitch.y);

            if (Intent.CrouchToggle) crouch.RequestToggle(HasHeadroomToStand());
            crouch.Tick(dt);
            ApplyCrouchHeight();

            StepsThisFrame = 0;
            JumpedThisFrame = false;
            LandedThisFrame = false;
            LandingImpact = 0f;
            float horizontalSpeed = 0f;
            if (body.enabled)
            {
                bool grounded = body.isGrounded;
                IsSprinting = Intent.Sprint && !crouch.Crouched && Intent.Move.sqrMagnitude > 0.01f && Stamina.CanSprint;
                bool jump = Intent.Jump && movement.CanJump(grounded, crouch.Crouched) && Stamina.TrySpend(tuning.stamina.jumpCost);
                var input = new MoveInput
                {
                    Move = Intent.Move, Forward = transform.forward, Right = transform.right, Grounded = grounded,
                    Sprinting = IsSprinting, Crouched = crouch.Crouched, JumpRequested = jump
                };
                var before = transform.position;
                var flags = body.Move(movement.Step(input, dt));
                if ((flags & CollisionFlags.Above) != 0) movement.HitCeiling();
                JumpedThisFrame = movement.JumpedThisStep;
                IsGrounded = body.isGrounded;
                // Actual displacement, not CharacterController.velocity (which depends on Time.deltaTime, not dt).
                var planar = transform.position - before;
                planar.y = 0f;
                horizontalSpeed = planar.magnitude / dt;
                StepsThisFrame = steps.Advance(horizontalSpeed, IsGrounded, Stride(), dt);
                LandedThisFrame = steps.UpdateLanding(IsGrounded, movement.Velocity.y, out float impact);
                LandingImpact = impact;
            }
            else
            {
                IsSprinting = false;
                IsGrounded = false;
            }
            Stamina.Tick(IsSprinting, dt);
            ApplyBob(horizontalSpeed, dt);
            Ground = GroundProbe.Probe(transform.position + Vector3.up * 0.5f, 1.5f, out var ground) ? ground : default;
            Hud.Tick(dt, Stamina.Normalized);
            foreach (var module in modules) module.Tick(this, dt);
            if (transform.position.y < tuning.respawnBelowY) FellOutOfWorld?.Invoke();
        }

        /// <summary>The only way to move the player instantly (autoSyncTransforms is off).</summary>
        public void Teleport(Vector3 feet, float yawDegrees)
        {
            if (!IsInitialized) Initialize();
            body.enabled = false;
            transform.SetPositionAndRotation(feet, Quaternion.Euler(0f, yawDegrees, 0f));
            yawPitch = new Vector2(Mathf.Repeat(yawDegrees, 360f), 0f);
            if (cameraPivot != null) cameraPivot.localRotation = Quaternion.identity;
            movement.ResetMotion();
            Physics.SyncTransforms();
            body.enabled = Locks.Allows(PlayerAbility.Move);
        }

        public void LockCursor()
        {
            if (!Application.isPlaying) return;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void UnlockCursor()
        {
            if (!Application.isPlaying) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        IPlayerIntentSource ActiveSource
        {
            get
            {
                if (scriptedSource != null) return scriptedSource;
                if (reader == null && Application.isPlaying && InputSystem.actions != null)
                {
                    reader = new PlayerInputReader(InputSystem.actions, tuning.look);
                    reader.Enable();
                }
                return reader;
            }
        }

        void HandleCursor()
        {
            if (scriptedSource != null || ActiveSource == null) return;
            if (reader.ReleaseCursorPressed) UnlockCursor();
            else if (reader.AttackPressed && Cursor.lockState != CursorLockMode.Locked && Locks.Allows(PlayerAbility.Look)) LockCursor();
            reader.CursorLocked = Cursor.lockState == CursorLockMode.Locked;
        }

        PlayerIntent Filter(PlayerIntent intent)
        {
            if (!Locks.Allows(PlayerAbility.Move))
            {
                intent.Move = Vector2.zero;
                intent.Sprint = false;
                intent.Jump = false;
                intent.CrouchToggle = false;
            }
            if (!Locks.Allows(PlayerAbility.Look)) intent.LookDegrees = Vector2.zero;
            if (!Locks.Allows(PlayerAbility.Interact)) intent.Interact = false;
            if (!Locks.Allows(PlayerAbility.Flashlight)) intent.Flashlight = false;
            return intent;
        }

        void OnLocksChanged()
        {
            bool canMove = Locks.Allows(PlayerAbility.Move);
            if (body.enabled == canMove) return;
            body.enabled = canMove;
            movement.ResetMotion();
        }

        bool HasHeadroomToStand()
        {
            float r = tuning.body.radius;
            var feet = transform.position;
            return !Physics.CheckCapsule(feet + Vector3.up * (r + 0.05f), feet + Vector3.up * (tuning.body.height - r), r * 0.95f,
                PlayerLayers.QueryMask, QueryTriggerInteraction.Ignore);
        }

        void ApplyCrouchHeight()
        {
            body.height = crouch.Height;
            body.center = new Vector3(0f, crouch.Height * 0.5f, 0f);
            if (cameraPivot != null) cameraPivot.localPosition = new Vector3(0f, crouch.EyeHeight, 0f);
        }

        float Stride() => crouch.Crouched ? tuning.bob.crouchStride : IsSprinting ? tuning.bob.sprintStride : tuning.bob.walkStride;

        void ApplyBob(float horizontalSpeed, float dt)
        {
            bool moving = IsGrounded && horizontalSpeed > 0.2f;
            bobWeight = Mathf.MoveTowards(bobWeight, moving ? 1f : 0f, tuning.bob.fadeSpeed * dt);
            float amplitude = crouch.Crouched ? tuning.bob.crouchAmplitude : IsSprinting ? tuning.bob.sprintAmplitude : tuning.bob.walkAmplitude;
            if (cameraView != null)
                cameraView.localPosition = HeadBob.Offset(steps.CyclePhase01, amplitude, tuning.bob.lateralRatio) * bobWeight;
        }
    }
}
```

- [ ] **Step 5: Jalankan tes, pastikan lulus**

Procedure R, lalu Procedure T dengan `Group = "FirstPersonPlayerTests|GroundProbeTests|PlayerInputReaderTests"`.
Expected: `passed=25 failed=0`. Kalau spike Step 1 gagal, angka ini berkurang sebanyak tes yang di-Ignore (`skipped>0`).

Kalau `Tick_WalkForward_MovesExpectedDistance` meleset sedikit di luar toleransi karena frame pertama belum grounded, jangan longgarkan toleransi. Cek dulu bahwa `Teleport` menaruh kaki tepat di lantai dan frame pertama memakai `airControl`. Catat selisih yang terukur di ledger.

Lalu `Group = "."`. Expected: `failed=0`.

- [ ] **Step 6: Commit**

Procedure C:
- path: `Assets/Player`
- subject: `feat(player): first-person controller orchestrator, ground probe and input reader`

---

### Task 11: Bunyi langkah per permukaan

**Files:**
- Create: `Assets/Player/Runtime/Audio/ISoundPlayer.cs`, `AudioSourceSoundPlayer.cs`, `FootstepBank.cs`, `FootstepAudio.cs`
- Create: `Assets/Player/Tests/RecordingSoundPlayer.cs`
- Test: `Assets/Player/Tests/FootstepAudioTests.cs`

**Interfaces:**
- Consumes: `FirstPersonPlayer` (`Ground`, `StepsThisFrame`, `JumpedThisFrame`, `LandedThisFrame`, `IsCrouched`, `IsSprinting`), `IPlayerModule`, `NoRepeatPicker`.
- Produces:
  - `interface ISoundPlayer { void Play(AudioClip clip, float volume, float pitch); }`.
  - `AudioSourceSoundPlayer(AudioSource)`.
  - `[Serializable] SurfaceClips { SurfaceType surface; AudioClip[] clips; float volume = 0.5f, pitchMin = 0.95f, pitchMax = 1.05f; }`.
  - `FootstepBank : ScriptableObject` dengan `List<SurfaceClips> surfaces` dan `Resolve(SurfaceType)` → `SurfaceClips` (atau null).
  - `FootstepAudio : MonoBehaviour, IPlayerModule` (Order 10) dengan:
    - `Configure(FootstepBank, AudioSource)`, `SetSoundPlayer(ISoundPlayer)`, `Bank`, `LastSurface`, `PlayedCount`;
    - konstanta `LandingVolumeScale = 1.6f`, `CrouchVolumeScale = 0.6f`, `SprintVolumeScale = 1.2f`.
  - `RecordingSoundPlayer` (tes) dengan `Calls` (`List<(AudioClip Clip, float Volume, float Pitch)>`).

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/Player/Tests/RecordingSoundPlayer.cs`:
```csharp
using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.Player.Tests
{
    internal sealed class RecordingSoundPlayer : ISoundPlayer
    {
        public readonly List<(AudioClip Clip, float Volume, float Pitch)> Calls = new List<(AudioClip, float, float)>();

        public void Play(AudioClip clip, float volume, float pitch) => Calls.Add((clip, volume, pitch));
    }
}
```

`Assets/Player/Tests/FootstepAudioTests.cs`:
```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TycoWorld.WorldGen;
using UnityEngine;

namespace TycoWorld.Player.Tests
{
    public class FootstepAudioTests
    {
        PhysicsFixture fx;

        [SetUp]
        public void SetUp()
        {
            fx = new PhysicsFixture();
            GroundProbe.ClearCache();
        }

        [TearDown]
        public void TearDown()
        {
            fx.Dispose();
            GroundProbe.ClearCache();
        }

        AudioClip[] Clips(string prefix, int count)
        {
            var clips = new AudioClip[count];
            for (int i = 0; i < count; i++) clips[i] = fx.Track(AudioClip.Create(prefix + i, 441, 1, 44100, false));
            return clips;
        }

        FootstepBank Bank(out SurfaceClips grass, out SurfaceClips fallback)
        {
            var bank = fx.Track(ScriptableObject.CreateInstance<FootstepBank>());
            grass = new SurfaceClips { surface = SurfaceType.Grass, clips = Clips("grass", 3), volume = 0.5f, pitchMin = 0.9f, pitchMax = 1.1f };
            fallback = new SurfaceClips { surface = SurfaceType.Default, clips = Clips("default", 2), volume = 0.4f };
            bank.surfaces = new List<SurfaceClips> { fallback, grass };
            return bank;
        }

        (TestRig Rig, FootstepAudio Audio, RecordingSoundPlayer Sound, SurfaceClips Grass) GrassRig(Vector3 localFeet)
        {
            fx.Floor().AddComponent<GroundSurface>().Initialize(SurfaceType.Grass);
            fx.Sync();
            var rig = TestRig.Create(fx, localFeet);
            var audio = rig.Root.AddComponent<FootstepAudio>();
            audio.Configure(Bank(out var grass, out _), null);
            var sound = new RecordingSoundPlayer();
            audio.SetSoundPlayer(sound);
            rig.Init();
            return (rig, audio, sound, grass);
        }

        [Test]
        public void Bank_ResolvesSurface_AndFallsBackToDefault()
        {
            var bank = Bank(out var grass, out var fallback);
            Assert.AreSame(grass, bank.Resolve(SurfaceType.Grass));
            Assert.AreSame(fallback, bank.Resolve(SurfaceType.Asphalt));
            grass.clips = new AudioClip[0];
            Assert.AreSame(fallback, bank.Resolve(SurfaceType.Grass));
        }

        [Test]
        public void Walking_PlaysOneClipPerStep_FromProbedSurface()
        {
            var (rig, audio, sound, grass) = GrassRig(Vector3.zero);
            rig.Intents.Hold(new PlayerIntent { Move = Vector2.up }, 3f);
            rig.Run(3f);
            Assert.Greater(rig.Player.StepCount, 5);
            Assert.AreEqual(rig.Player.StepCount, sound.Calls.Count);
            Assert.AreEqual(sound.Calls.Count, audio.PlayedCount);
            Assert.AreEqual(SurfaceType.Grass, audio.LastSurface);
            foreach (var call in sound.Calls)
            {
                CollectionAssert.Contains(grass.clips, call.Clip);
                Assert.That(call.Pitch, Is.InRange(0.9f, 1.1f));
                Assert.AreEqual(0.5f, call.Volume, 1e-5f);
            }
            for (int i = 1; i < sound.Calls.Count; i++) Assert.AreNotSame(sound.Calls[i - 1].Clip, sound.Calls[i].Clip);
        }

        [Test]
        public void Landing_PlaysLouder()
        {
            var (rig, _, sound, _) = GrassRig(new Vector3(0f, 2f, 0f));
            rig.Run(1f);
            Assert.IsTrue(sound.Calls.Any(c => Mathf.Abs(c.Volume - 0.5f * FootstepAudio.LandingVolumeScale) < 1e-4f));
        }

        [Test]
        public void Crouched_StepsAreQuieter()
        {
            var (rig, _, sound, _) = GrassRig(Vector3.zero);
            rig.Intents.Press(new PlayerIntent { CrouchToggle = true });
            rig.Run(0.5f);
            sound.Calls.Clear();
            rig.Intents.Hold(new PlayerIntent { Move = Vector2.up }, 3f);
            rig.Run(3f);
            Assert.Greater(sound.Calls.Count, 0);
            foreach (var call in sound.Calls) Assert.AreEqual(0.5f * FootstepAudio.CrouchVolumeScale, call.Volume, 1e-5f);
        }

        [Test]
        public void WithoutBank_DoesNothing()
        {
            fx.Floor();
            fx.Sync();
            var rig = TestRig.Create(fx, Vector3.zero);
            var audio = rig.Root.AddComponent<FootstepAudio>();
            rig.Init();
            rig.Intents.Hold(new PlayerIntent { Move = Vector2.up }, 1f);
            Assert.DoesNotThrow(() => rig.Run(1f));
            Assert.AreEqual(0, audio.PlayedCount);
        }
    }
}
```

- [ ] **Step 2: Jalankan tes, pastikan gagal**

Procedure R, lalu Procedure T dengan `Group = "FootstepAudioTests"`.
Expected: COMPILE FAILED (`CS0246 ISoundPlayer`, `FootstepBank`, `SurfaceClips`, `FootstepAudio`).

- [ ] **Step 3: Implementasi**

`Assets/Player/Runtime/Audio/ISoundPlayer.cs`:
```csharp
using UnityEngine;

namespace TycoWorld.Player
{
    /// <summary>One-shot sound output; tests replace it with a recorder.</summary>
    public interface ISoundPlayer
    {
        void Play(AudioClip clip, float volume, float pitch);
    }
}
```

`Assets/Player/Runtime/Audio/AudioSourceSoundPlayer.cs`:
```csharp
using UnityEngine;

namespace TycoWorld.Player
{
    public sealed class AudioSourceSoundPlayer : ISoundPlayer
    {
        readonly AudioSource source;

        public AudioSourceSoundPlayer(AudioSource source) { this.source = source; }

        public void Play(AudioClip clip, float volume, float pitch)
        {
            if (clip == null || source == null) return;
            source.pitch = pitch;
            source.PlayOneShot(clip, volume);
        }
    }
}
```

`Assets/Player/Runtime/Audio/FootstepBank.cs`:
```csharp
using System;
using System.Collections.Generic;
using TycoWorld.WorldGen;
using UnityEngine;

namespace TycoWorld.Player
{
    [Serializable]
    public sealed class SurfaceClips
    {
        public SurfaceType surface;
        public AudioClip[] clips = new AudioClip[0];
        public float volume = 0.5f;
        public float pitchMin = 0.95f;
        public float pitchMax = 1.05f;
    }

    /// <summary>Footstep clips per surface; the Default entry is the fallback for surfaces without clips.</summary>
    [CreateAssetMenu(menuName = "Tyco World/Footstep Bank", fileName = "FootstepBank")]
    public sealed class FootstepBank : ScriptableObject
    {
        public List<SurfaceClips> surfaces = new List<SurfaceClips>();

        public SurfaceClips Resolve(SurfaceType surface)
        {
            SurfaceClips fallback = null;
            foreach (var entry in surfaces)
            {
                if (entry == null || entry.clips == null || entry.clips.Length == 0) continue;
                if (entry.surface == surface) return entry;
                if (entry.surface == SurfaceType.Default) fallback = entry;
            }
            return fallback;
        }
    }
}
```

`Assets/Player/Runtime/Audio/FootstepAudio.cs`:
```csharp
using TycoWorld.WorldGen;
using UnityEngine;

namespace TycoWorld.Player
{
    /// <summary>Plays one footstep per step event (plus jump and landing) for the surface under the player.</summary>
    public sealed class FootstepAudio : MonoBehaviour, IPlayerModule
    {
        public const float LandingVolumeScale = 1.6f;
        public const float CrouchVolumeScale = 0.6f;
        public const float SprintVolumeScale = 1.2f;

        [SerializeField] FootstepBank bank;
        [SerializeField] AudioSource source;

        ISoundPlayer sound;
        NoRepeatPicker picker = new NoRepeatPicker(0x5EED5EEDu);

        public int Order => 10;
        public FootstepBank Bank => bank;
        public SurfaceType LastSurface { get; private set; } = SurfaceType.Default;
        public int PlayedCount { get; private set; }

        public void Configure(FootstepBank footstepBank, AudioSource audioSource)
        {
            bank = footstepBank;
            source = audioSource;
        }

        public void SetSoundPlayer(ISoundPlayer player) => sound = player;

        public void Initialize(FirstPersonPlayer player)
        {
            if (sound == null && source != null) sound = new AudioSourceSoundPlayer(source);
        }

        public void Tick(FirstPersonPlayer player, float dt)
        {
            var surface = player.Ground.Hit ? player.Ground.Surface : SurfaceType.Default;
            float mode = player.IsCrouched ? CrouchVolumeScale : player.IsSprinting ? SprintVolumeScale : 1f;
            if (player.LandedThisFrame) PlayFor(surface, LandingVolumeScale);
            if (player.JumpedThisFrame) PlayFor(surface, mode);
            for (int i = 0; i < player.StepsThisFrame; i++) PlayFor(surface, mode);
        }

        void PlayFor(SurfaceType surface, float volumeScale)
        {
            if (bank == null || sound == null) return;
            var set = bank.Resolve(surface);
            if (set == null) return;
            int index = picker.Next(set.clips.Length);
            sound.Play(set.clips[index], set.volume * volumeScale, picker.Range(set.pitchMin, set.pitchMax));
            LastSurface = surface;
            PlayedCount++;
        }
    }
}
```

- [ ] **Step 4: Jalankan tes, pastikan lulus**

Procedure R, lalu Procedure T dengan `Group = "FootstepAudioTests"`.
Expected: `passed=5 failed=0`.

Lalu `Group = "."`. Expected: `failed=0`.

- [ ] **Step 5: Commit**

Procedure C:
- path: `Assets/Player`
- subject: `feat(player): surface-aware footsteps`

---

### Task 12: Sistem interaksi + pagar, pintu terkunci, motor sementara

**Files:**
- Create: `Assets/Player/Runtime/Interaction/InteractionContext.cs`, `Interactable.cs`, `Interactor.cs`, `GateInteractable.cs`, `LockedDoorInteractable.cs`, `PlaceholderVehicleInteractable.cs`
- Test: `Assets/Player/Tests/InteractionTests.cs`

**Interfaces:**
- Consumes: `GateSwing`, `GateEvent`, `IPlayerFeedback`, `HudModel` (Task 9); `ISoundPlayer`, `AudioSourceSoundPlayer` (Task 11); `FirstPersonPlayer`, `IPlayerModule` (Task 10).
- Produces:
  - `readonly struct InteractionContext(FirstPersonPlayer player, IPlayerFeedback feedback, Vector3 viewOrigin)` dengan `Player`, `Feedback`, `ViewOrigin`.
  - `abstract Interactable : MonoBehaviour` dengan:
    - `InteractionId`, `SetInteractionId(string)`, `SetAudioSource(AudioSource)`, `SetSoundPlayer(ISoundPlayer)`;
    - abstract `GetPrompt(in InteractionContext)` dan virtual `CanInteract(in InteractionContext)`;
    - `Interact(in InteractionContext)` → bool, yang memanggil `OnInteract` lalu memicu `Interacted`;
    - `static event Action<Interactable> Interacted` dan `static ResetStatics()`.
  - `GateInteractable`: `Configure(Transform leaf, AudioClip unlatch, AudioClip creak, AudioClip latch)`, `State`, `AngleDeg`, `Tick(float dt)`. Prompt "[E] Buka pagar" / "[E] Tutup pagar".
  - `LockedDoorInteractable`: `Configure(AudioClip rattle)`, prompt "[E] Buka pintu", pesan "Pintunya terkunci.".
  - `PlaceholderVehicleInteractable`: `Configure(AudioClip keys)`, prompt "[E] Naik motor", pesan "Belum bisa naik motor.".
  - `Interactor : MonoBehaviour, IPlayerModule` (Order 20, ditaruh di kamera) dengan `Focus` dan `static FindFocus(Ray, float range, float sphereRadius)` → `Interactable`.

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/Player/Tests/InteractionTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.Player.Tests
{
    public class InteractionTests
    {
        PhysicsFixture fx;
        static Ray Eye => new Ray(PhysicsFixture.Origin + Vector3.up * 1.6f, Vector3.forward);

        [SetUp]
        public void SetUp()
        {
            fx = new PhysicsFixture();
            Interactable.ResetStatics();
        }

        [TearDown]
        public void TearDown()
        {
            fx.Dispose();
            Interactable.ResetStatics();
        }

        AudioClip Clip(string name) => fx.Track(AudioClip.Create(name, 441, 1, 44100, false));

        LockedDoorInteractable Door(float z, out RecordingSoundPlayer sound, out AudioClip rattle)
        {
            var go = fx.Box("Door", new Vector3(0f, 1.6f, z), new Vector3(1f, 2f, 0.1f));
            var door = go.AddComponent<LockedDoorInteractable>();
            rattle = Clip("rattle");
            door.Configure(rattle);
            sound = new RecordingSoundPlayer();
            door.SetSoundPlayer(sound);
            fx.Sync();
            return door;
        }

        InteractionContext Context(IPlayerFeedback feedback) => new InteractionContext(null, feedback, PhysicsFixture.Origin + Vector3.up * 1.6f);

        [Test]
        public void FindFocus_WithinRangeOnly()
        {
            var near = Door(2f, out _, out _);
            Assert.AreSame(near, Interactor.FindFocus(Eye, 2.2f, 0.12f));
            Object.DestroyImmediate(near.gameObject);
            Door(2.5f, out _, out _);
            Assert.IsNull(Interactor.FindFocus(Eye, 2.2f, 0.12f));
        }

        [Test]
        public void FindFocus_BlockedByWall()
        {
            Door(2f, out _, out _);
            fx.Box("Wall", new Vector3(0f, 1.6f, 1f), new Vector3(3f, 3f, 0.1f));
            fx.Sync();
            Assert.IsNull(Interactor.FindFocus(Eye, 2.2f, 0.12f));
        }

        [Test]
        public void FindFocus_ChildColliderResolvesParent()
        {
            var parent = fx.Track(new GameObject("Motor"));
            var vehicle = parent.AddComponent<PlaceholderVehicleInteractable>();
            var child = fx.Box("Body", new Vector3(0f, 1.6f, 1.5f), new Vector3(0.6f, 1f, 1.8f));
            child.transform.SetParent(parent.transform, true);
            fx.Sync();
            Assert.AreSame(vehicle, Interactor.FindFocus(Eye, 2.2f, 0.12f));
        }

        [Test]
        public void FindFocus_SphereCatchesNearMiss()
        {
            var go = fx.Box("Handle", new Vector3(0.15f, 1.6f, 1.5f), new Vector3(0.1f, 0.1f, 0.1f));
            var door = go.AddComponent<LockedDoorInteractable>();
            fx.Sync();
            Assert.AreSame(door, Interactor.FindFocus(Eye, 2.2f, 0.12f));
        }

        [Test]
        public void LockedDoor_ShowsMessageAndRattles()
        {
            var door = Door(2f, out var sound, out var rattle);
            var hud = new HudModel(new HudTuning());
            var ctx = Context(hud);
            Assert.AreEqual("[E] Buka pintu", door.GetPrompt(ctx));
            Assert.IsTrue(door.Interact(ctx));
            Assert.AreEqual("Pintunya terkunci.", hud.Message);
            Assert.AreEqual(1, sound.Calls.Count);
            Assert.AreSame(rattle, sound.Calls[0].Clip);
        }

        [Test]
        public void Vehicle_PromptMessageAndKeys()
        {
            var go = fx.Box("Motor", new Vector3(0f, 0.5f, 2f), new Vector3(0.7f, 1f, 1.8f));
            var vehicle = go.AddComponent<PlaceholderVehicleInteractable>();
            var keys = Clip("keys");
            vehicle.Configure(keys);
            var sound = new RecordingSoundPlayer();
            vehicle.SetSoundPlayer(sound);
            var hud = new HudModel(new HudTuning());
            var ctx = Context(hud);
            Assert.AreEqual("[E] Naik motor", vehicle.GetPrompt(ctx));
            vehicle.Interact(ctx);
            Assert.AreEqual("Belum bisa naik motor.", hud.Message);
            Assert.AreSame(keys, sound.Calls[0].Clip);
        }

        [Test]
        public void Gate_TogglesSwingsAwayAndPlaysSounds()
        {
            var root = fx.Track(new GameObject("Gate"));
            root.transform.position = PhysicsFixture.Origin + new Vector3(0f, 0f, 2f);
            var leaf = new GameObject("Leaf").transform;
            leaf.SetParent(root.transform, false);
            var gate = root.AddComponent<GateInteractable>();
            AudioClip unlatch = Clip("unlatch"), creak = Clip("creak"), latch = Clip("latch");
            gate.Configure(leaf, unlatch, creak, latch);
            var sound = new RecordingSoundPlayer();
            gate.SetSoundPlayer(sound);
            var ctx = Context(new HudModel(new HudTuning()));

            Assert.AreEqual("[E] Buka pagar", gate.GetPrompt(ctx));
            Assert.IsTrue(gate.Interact(ctx));
            gate.Tick(0.05f);
            Assert.AreSame(unlatch, sound.Calls[0].Clip);
            Assert.AreSame(creak, sound.Calls[1].Clip);
            Assert.IsFalse(gate.Interact(ctx), "ignored while swinging");
            gate.Tick(1f);
            Assert.AreEqual(GateSwing.State.Open, gate.State);
            Assert.AreEqual(-100f, gate.AngleDeg, 1e-3f, "viewer at -z → leaf swings toward +z");
            Assert.AreEqual(260f, leaf.localEulerAngles.y, 0.01f);
            Assert.AreEqual("[E] Tutup pagar", gate.GetPrompt(ctx));

            Assert.IsTrue(gate.Interact(ctx));
            gate.Tick(1f);
            Assert.AreEqual(GateSwing.State.Closed, gate.State);
            Assert.AreSame(latch, sound.Calls[sound.Calls.Count - 1].Clip);
        }

        [Test]
        public void Interacted_EventCarriesId_AndResetClearsSubscribers()
        {
            var door = Door(2f, out _, out _);
            door.SetInteractionId("kos-door");
            Interactable received = null;
            Interactable.Interacted += i => received = i;
            door.Interact(Context(null));
            Assert.AreSame(door, received);
            Assert.AreEqual("kos-door", received.InteractionId);

            received = null;
            Interactable.ResetStatics();
            door.Interact(Context(null));
            Assert.IsNull(received);
        }

        [Test]
        public void Interactor_SetsPrompt_InteractsOnPress_AndRespectsLock()
        {
            fx.Floor();
            Door(1.5f, out _, out _);
            var rig = TestRig.Create(fx, Vector3.zero);
            var interactor = rig.View.gameObject.AddComponent<Interactor>();
            rig.Init();
            rig.Player.Tick(1f / 60f);
            Assert.IsNotNull(interactor.Focus);
            Assert.AreEqual("[E] Buka pintu", rig.Player.Hud.Prompt);

            rig.Intents.Press(new PlayerIntent { Interact = true });
            rig.Player.Tick(1f / 60f);
            Assert.AreEqual("Pintunya terkunci.", rig.Player.Hud.Message);

            rig.Player.Locks.Acquire(this, PlayerAbility.Interact);
            rig.Player.Tick(1f / 60f);
            Assert.IsNull(interactor.Focus);
            Assert.AreEqual("", rig.Player.Hud.Prompt);
        }
    }
}
```

- [ ] **Step 2: Jalankan tes, pastikan gagal**

Procedure R, lalu Procedure T dengan `Group = "InteractionTests"`.
Expected: COMPILE FAILED (`CS0246 Interactable`, `Interactor`, `LockedDoorInteractable`, `GateInteractable`, `PlaceholderVehicleInteractable`, `InteractionContext`).

- [ ] **Step 3: Implementasi**

`Assets/Player/Runtime/Interaction/InteractionContext.cs`:
```csharp
using UnityEngine;

namespace TycoWorld.Player
{
    public readonly struct InteractionContext
    {
        public readonly FirstPersonPlayer Player;
        public readonly IPlayerFeedback Feedback;
        public readonly Vector3 ViewOrigin;

        public InteractionContext(FirstPersonPlayer player, IPlayerFeedback feedback, Vector3 viewOrigin)
        {
            Player = player;
            Feedback = feedback;
            ViewOrigin = viewOrigin;
        }
    }
}
```

`Assets/Player/Runtime/Interaction/Interactable.cs`:
```csharp
using System;
using UnityEngine;

namespace TycoWorld.Player
{
    /// <summary>Something the player can use with E. Interacted (with InteractionId) is the hook for the story system.</summary>
    public abstract class Interactable : MonoBehaviour
    {
        [SerializeField] string interactionId = "";
        [SerializeField] AudioSource audioSource;

        ISoundPlayer soundPlayer;

        public static event Action<Interactable> Interacted;

        /// <summary>Clears static subscribers; runs automatically before each Play session (domain reload is off).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetStatics() => Interacted = null;

        public string InteractionId => interactionId;
        public AudioSource AudioOutput => audioSource;

        public void SetInteractionId(string id) => interactionId = id ?? "";

        public void SetAudioSource(AudioSource source)
        {
            audioSource = source;
            soundPlayer = null;
        }

        public void SetSoundPlayer(ISoundPlayer player) => soundPlayer = player;

        protected ISoundPlayer Sound
        {
            get
            {
                if (soundPlayer == null && audioSource != null) soundPlayer = new AudioSourceSoundPlayer(audioSource);
                return soundPlayer;
            }
        }

        public abstract string GetPrompt(in InteractionContext context);

        public virtual bool CanInteract(in InteractionContext context) => true;

        public bool Interact(in InteractionContext context)
        {
            if (!CanInteract(context)) return false;
            OnInteract(context);
            Interacted?.Invoke(this);
            return true;
        }

        protected abstract void OnInteract(in InteractionContext context);
    }
}
```

`Assets/Player/Runtime/Interaction/GateInteractable.cs`:
```csharp
using UnityEngine;

namespace TycoWorld.Player
{
    /// <summary>Iron gate on a hinge: swings away from whoever opens it; unlatch + creak when opening, creak + latch when closing.</summary>
    public sealed class GateInteractable : Interactable
    {
        [SerializeField] Transform leaf;
        [SerializeField] AudioClip unlatchClip;
        [SerializeField] AudioClip creakClip;
        [SerializeField] AudioClip latchClip;

        GateSwing swing;

        GateSwing Swing => swing ?? (swing = new GateSwing());

        public GateSwing.State State => Swing.Current;
        public float AngleDeg => Swing.AngleDeg;

        public void Configure(Transform leafPivot, AudioClip unlatch, AudioClip creak, AudioClip latch)
        {
            leaf = leafPivot;
            unlatchClip = unlatch;
            creakClip = creak;
            latchClip = latch;
        }

        public override string GetPrompt(in InteractionContext context) =>
            State == GateSwing.State.Closed || State == GateSwing.State.Closing ? "[E] Buka pagar" : "[E] Tutup pagar";

        public override bool CanInteract(in InteractionContext context) => !Swing.IsMoving;

        protected override void OnInteract(in InteractionContext context)
        {
            // Leaf extends along local +x from the hinge; a positive Y angle moves its free end toward -z.
            Vector3 viewer = transform.InverseTransformPoint(context.ViewOrigin);
            Swing.Toggle(viewer.z >= 0f ? 1f : -1f);
        }

        void Update()
        {
            if (Application.isPlaying) Tick(Time.deltaTime);
        }

        public void Tick(float dt)
        {
            foreach (var e in Swing.Tick(dt))
            {
                var clip = e == GateEvent.Unlatch ? unlatchClip : e == GateEvent.Creak ? creakClip : latchClip;
                var output = Sound;
                if (output != null) output.Play(clip, 0.9f, 1f);
            }
            if (leaf != null) leaf.localRotation = Quaternion.Euler(0f, Swing.AngleDeg, 0f);
        }
    }
}
```

`Assets/Player/Runtime/Interaction/LockedDoorInteractable.cs`:
```csharp
using UnityEngine;

namespace TycoWorld.Player
{
    /// <summary>A door that does not open yet (interiors are a later sub-project).</summary>
    public sealed class LockedDoorInteractable : Interactable
    {
        public const string LockedMessage = "Pintunya terkunci.";

        [SerializeField] AudioClip rattleClip;

        public void Configure(AudioClip rattle) => rattleClip = rattle;

        public override string GetPrompt(in InteractionContext context) => "[E] Buka pintu";

        protected override void OnInteract(in InteractionContext context)
        {
            if (context.Feedback != null) context.Feedback.ShowMessage(LockedMessage);
            var output = Sound;
            if (output != null) output.Play(rattleClip, 1f, 1.1f);
        }
    }
}
```

`Assets/Player/Runtime/Interaction/PlaceholderVehicleInteractable.cs`:
```csharp
using UnityEngine;

namespace TycoWorld.Player
{
    /// <summary>The parked motorbike until the vehicle sub-project: it only answers that riding is not possible yet.</summary>
    public sealed class PlaceholderVehicleInteractable : Interactable
    {
        public const string NotYetMessage = "Belum bisa naik motor.";

        [SerializeField] AudioClip keysClip;

        public void Configure(AudioClip keys) => keysClip = keys;

        public override string GetPrompt(in InteractionContext context) => "[E] Naik motor";

        protected override void OnInteract(in InteractionContext context)
        {
            if (context.Feedback != null) context.Feedback.ShowMessage(NotYetMessage);
            var output = Sound;
            if (output != null) output.Play(keysClip, 0.8f, 1f);
        }
    }
}
```

`Assets/Player/Runtime/Interaction/Interactor.cs`:
```csharp
using UnityEngine;

namespace TycoWorld.Player
{
    /// <summary>Lives on the camera: finds the Interactable in view, shows its prompt, and uses it on Interact.</summary>
    public sealed class Interactor : MonoBehaviour, IPlayerModule
    {
        public int Order => 20;
        public Interactable Focus { get; private set; }

        public void Initialize(FirstPersonPlayer player) { }

        public void Tick(FirstPersonPlayer player, float dt)
        {
            var t = player.Tuning.interaction;
            Focus = player.Locks.Allows(PlayerAbility.Interact)
                ? FindFocus(new Ray(transform.position, transform.forward), t.range, t.sphereRadius)
                : null;
            var context = new InteractionContext(player, player.Hud, transform.position);
            bool usable = Focus != null && Focus.CanInteract(context);
            player.Hud.SetPrompt(usable ? Focus.GetPrompt(context) : "");
            if (usable && player.Intent.Interact) Focus.Interact(context);
        }

        /// <summary>Ray first; if it hits nothing interactable, a thin sphere cast no farther than the ray's hit.</summary>
        public static Interactable FindFocus(Ray ray, float range, float sphereRadius)
        {
            float reach = range;
            if (Physics.Raycast(ray, out var hit, range, PlayerLayers.QueryMask, QueryTriggerInteraction.Ignore))
            {
                var direct = hit.collider.GetComponentInParent<Interactable>();
                if (direct != null) return direct;
                reach = hit.distance;
            }
            if (Physics.SphereCast(ray, sphereRadius, out var sphereHit, reach, PlayerLayers.QueryMask, QueryTriggerInteraction.Ignore))
                return sphereHit.collider.GetComponentInParent<Interactable>();
            return null;
        }
    }
}
```

- [ ] **Step 4: Jalankan tes, pastikan lulus**

Procedure R, lalu Procedure T dengan `Group = "InteractionTests"`.
Expected: `passed=9 failed=0`.

Lalu `Group = "."`. Expected: `failed=0`.

- [ ] **Step 5: Commit**

Procedure C:
- path: `Assets/Player`
- subject: `feat(player): interaction system with gate, locked door and placeholder motorbike`

---

### Task 13: Senter dan HUD (UI Toolkit)

**Files:**
- Create: `Assets/Player/Runtime/Equipment/Flashlight.cs`, `Assets/Player/Runtime/UI/HudView.cs`, `Assets/Player/Runtime/UI/PlayerHud.cs`
- Test: `Assets/Player/Tests/FlashlightTests.cs`, `HudViewTests.cs`

**Interfaces:**
- Consumes: `FirstPersonPlayer` (`Intent`, `Tuning.flashlight`, `Hud`), `ISoundPlayer`, `IPlayerModule`, `HudModel`.
- Produces:
  - `Flashlight : MonoBehaviour, IPlayerModule` (Order 30, `[RequireComponent(Light)]`, ditaruh di bawah kamera) dengan `Configure(AudioSource click, AudioClip clip)`, `SetSoundPlayer(ISoundPlayer)`, `IsOn`, `Toggle()`.
  - `HudView(VisualElement root)` dengan `Render(HudModel)` dan konstanta nama `CrosshairName = "crosshair"`, `PromptName = "prompt"`, `MessageName = "message"`, `StaminaName = "stamina"`, `StaminaFillName = "stamina-fill"`.
  - `PlayerHud : MonoBehaviour, IPlayerModule` (Order 40, `[RequireComponent(UIDocument)]`) dengan `View`.

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/Player/Tests/FlashlightTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.Player.Tests
{
    public class FlashlightTests
    {
        PhysicsFixture fx;

        [SetUp]
        public void SetUp() => fx = new PhysicsFixture();

        [TearDown]
        public void TearDown() => fx.Dispose();

        (TestRig Rig, Flashlight Light, RecordingSoundPlayer Sound) Rig()
        {
            fx.Floor();
            fx.Sync();
            var rig = TestRig.Create(fx, Vector3.zero);
            var go = new GameObject("Flashlight");
            go.transform.SetParent(rig.View, false);
            var flashlight = go.AddComponent<Flashlight>();
            var sound = new RecordingSoundPlayer();
            flashlight.Configure(null, fx.Track(AudioClip.Create("click", 441, 1, 44100, false)));
            flashlight.SetSoundPlayer(sound);
            rig.Init();
            return (rig, flashlight, sound);
        }

        [Test]
        public void Initialize_AppliesTuning_StartsOff()
        {
            var (_, flashlight, _) = Rig();
            var light = flashlight.GetComponent<Light>();
            Assert.AreEqual(LightType.Spot, light.type);
            Assert.AreEqual(55f, light.spotAngle, 1e-3f);
            Assert.AreEqual(30f, light.innerSpotAngle, 1e-3f);
            Assert.AreEqual(22f, light.range, 1e-3f);
            Assert.AreEqual(LightShadows.Soft, light.shadows);
            Assert.IsFalse(flashlight.IsOn);
        }

        [Test]
        public void Press_TogglesAndClicks()
        {
            var (rig, flashlight, sound) = Rig();
            rig.Intents.Press(new PlayerIntent { Flashlight = true });
            rig.Player.Tick(1f / 60f);
            Assert.IsTrue(flashlight.IsOn);
            rig.Intents.Press(new PlayerIntent { Flashlight = true });
            rig.Player.Tick(1f / 60f);
            Assert.IsFalse(flashlight.IsOn);
            Assert.AreEqual(2, sound.Calls.Count);
        }

        [Test]
        public void Lock_BlocksToggle()
        {
            var (rig, flashlight, _) = Rig();
            rig.Player.Locks.Acquire(this, PlayerAbility.Flashlight);
            rig.Intents.Press(new PlayerIntent { Flashlight = true });
            rig.Player.Tick(1f / 60f);
            Assert.IsFalse(flashlight.IsOn);
        }

        [Test]
        public void Beam_LagsThenFollowsView()
        {
            var (rig, flashlight, _) = Rig();
            rig.Intents.Press(new PlayerIntent { LookDegrees = new Vector2(90f, 0f) });
            rig.Player.Tick(1f / 60f);
            Assert.Greater(Quaternion.Angle(flashlight.transform.rotation, rig.View.rotation), 5f);
            rig.Run(0.5f);
            Assert.Less(Quaternion.Angle(flashlight.transform.rotation, rig.View.rotation), 1f);
        }
    }
}
```

`Assets/Player/Tests/HudViewTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace TycoWorld.Player.Tests
{
    public class HudViewTests
    {
        static (VisualElement Root, HudView View, HudModel Model) Make()
        {
            var root = new VisualElement();
            return (root, new HudView(root), new HudModel(new HudTuning()));
        }

        [Test]
        public void Build_CreatesNamedElements()
        {
            var (root, _, _) = Make();
            foreach (var name in new[] { HudView.CrosshairName, HudView.PromptName, HudView.MessageName, HudView.StaminaName, HudView.StaminaFillName })
                Assert.IsNotNull(root.Q(name), name);
        }

        [Test]
        public void Render_ShowsPrompt_AndHidesWhenEmpty()
        {
            var (root, view, model) = Make();
            model.SetPrompt("[E] Buka pagar");
            view.Render(model);
            var prompt = root.Q<Label>(HudView.PromptName);
            Assert.AreEqual("[E] Buka pagar", prompt.text);
            Assert.AreEqual(DisplayStyle.Flex, prompt.style.display.value);
            model.SetPrompt("");
            view.Render(model);
            Assert.AreEqual(DisplayStyle.None, prompt.style.display.value);
        }

        [Test]
        public void Render_MessageAndStamina()
        {
            var (root, view, model) = Make();
            model.ShowMessage("Kos Raflesia 3");
            model.Tick(0.1f, 0.5f);
            view.Render(model);
            Assert.AreEqual("Kos Raflesia 3", root.Q<Label>(HudView.MessageName).text);
            Assert.AreEqual(DisplayStyle.Flex, root.Q(HudView.StaminaName).style.display.value);
            var width = root.Q(HudView.StaminaFillName).style.width.value;
            Assert.AreEqual(LengthUnit.Percent, width.unit);
            Assert.AreEqual(50f, width.value, 1e-3f);
        }

        [Test]
        public void Render_HidesCrosshairWhenModelSaysSo()
        {
            var (root, view, model) = Make();
            model.CrosshairVisible = false;
            view.Render(model);
            Assert.AreEqual(DisplayStyle.None, root.Q(HudView.CrosshairName).style.display.value);
        }

        [Test]
        public void PlayerHud_WithoutPanel_DoesNotThrow()
        {
            using (var fx = new PhysicsFixture())
            {
                var rig = TestRig.Create(fx, Vector3.zero);
                rig.Root.AddComponent<PlayerHud>();
                rig.Init();
                Assert.DoesNotThrow(() => rig.Player.Tick(1f / 60f));
            }
        }
    }
}
```

- [ ] **Step 2: Jalankan tes, pastikan gagal**

Procedure R, lalu Procedure T dengan `Group = "FlashlightTests|HudViewTests"`.
Expected: COMPILE FAILED (`CS0246 Flashlight`, `HudView`, `PlayerHud`).

- [ ] **Step 3: Implementasi**

`Assets/Player/Runtime/Equipment/Flashlight.cs`:
```csharp
using UnityEngine;

namespace TycoWorld.Player
{
    /// <summary>Spot light under the camera: F toggles it with a click; the beam trails the view slightly.</summary>
    [RequireComponent(typeof(Light))]
    public sealed class Flashlight : MonoBehaviour, IPlayerModule
    {
        [SerializeField] AudioSource clickSource;
        [SerializeField] AudioClip clickClip;

        Light spot;
        ISoundPlayer sound;
        Transform follow;
        Quaternion current;

        public int Order => 30;
        public bool IsOn => spot != null && spot.enabled;

        public void Configure(AudioSource source, AudioClip click)
        {
            clickSource = source;
            clickClip = click;
        }

        public void SetSoundPlayer(ISoundPlayer player) => sound = player;

        public void Initialize(FirstPersonPlayer player)
        {
            var t = player.Tuning.flashlight;
            spot = GetComponent<Light>();
            spot.type = LightType.Spot;
            spot.spotAngle = t.outerAngle;
            spot.innerSpotAngle = t.innerAngle;
            spot.range = t.range;
            spot.intensity = t.intensity;
            spot.color = t.color;
            spot.shadows = LightShadows.Soft;
            spot.enabled = t.startOn;
            if (sound == null && clickSource != null) sound = new AudioSourceSoundPlayer(clickSource);
            follow = transform.parent;
            current = follow != null ? follow.rotation : transform.rotation;
        }

        public void Toggle()
        {
            if (spot == null) return;
            spot.enabled = !spot.enabled;
            if (sound != null) sound.Play(clickClip, 0.8f, spot.enabled ? 1.05f : 0.95f);
        }

        public void Tick(FirstPersonPlayer player, float dt)
        {
            if (player.Intent.Flashlight) Toggle();
            if (follow == null) return;
            current = Quaternion.Slerp(current, follow.rotation, 1f - Mathf.Exp(-player.Tuning.flashlight.lagSpeed * dt));
            transform.rotation = current;
        }
    }
}
```

`Assets/Player/Runtime/UI/HudView.cs`:
```csharp
using UnityEngine;
using UnityEngine.UIElements;

namespace TycoWorld.Player
{
    /// <summary>Code-built UI Toolkit HUD: crosshair dot, interaction prompt, message line, stamina bar.</summary>
    public sealed class HudView
    {
        public const string CrosshairName = "crosshair";
        public const string PromptName = "prompt";
        public const string MessageName = "message";
        public const string StaminaName = "stamina";
        public const string StaminaFillName = "stamina-fill";

        readonly VisualElement crosshair, staminaBar, staminaFill;
        readonly Label prompt, message;

        public HudView(VisualElement root)
        {
            root.style.flexGrow = 1f;
            root.pickingMode = PickingMode.Ignore;

            crosshair = new VisualElement { name = CrosshairName, pickingMode = PickingMode.Ignore };
            Absolute(crosshair);
            crosshair.style.left = Length.Percent(50f);
            crosshair.style.top = Length.Percent(50f);
            crosshair.style.width = 4f;
            crosshair.style.height = 4f;
            crosshair.style.marginLeft = -2f;
            crosshair.style.marginTop = -2f;
            crosshair.style.backgroundColor = new Color(1f, 1f, 1f, 0.8f);
            crosshair.style.borderTopLeftRadius = 2f;
            crosshair.style.borderTopRightRadius = 2f;
            crosshair.style.borderBottomLeftRadius = 2f;
            crosshair.style.borderBottomRightRadius = 2f;

            prompt = CenteredLabel(PromptName, 22f);
            prompt.style.top = Length.Percent(56f);

            message = CenteredLabel(MessageName, 24f);
            message.style.bottom = Length.Percent(12f);

            staminaBar = new VisualElement { name = StaminaName, pickingMode = PickingMode.Ignore };
            Absolute(staminaBar);
            staminaBar.style.left = Length.Percent(50f);
            staminaBar.style.bottom = Length.Percent(6f);
            staminaBar.style.width = 240f;
            staminaBar.style.height = 6f;
            staminaBar.style.marginLeft = -120f;
            staminaBar.style.backgroundColor = new Color(1f, 1f, 1f, 0.15f);

            staminaFill = new VisualElement { name = StaminaFillName, pickingMode = PickingMode.Ignore };
            staminaFill.style.height = Length.Percent(100f);
            staminaFill.style.width = Length.Percent(100f);
            staminaFill.style.backgroundColor = new Color(0.86f, 0.86f, 0.78f, 0.8f);
            staminaBar.Add(staminaFill);

            root.Add(crosshair);
            root.Add(prompt);
            root.Add(message);
            root.Add(staminaBar);
        }

        public void Render(HudModel model)
        {
            crosshair.style.display = model.CrosshairVisible ? DisplayStyle.Flex : DisplayStyle.None;
            prompt.text = model.Prompt;
            prompt.style.display = model.Prompt.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            message.text = model.Message;
            message.style.display = model.Message.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            staminaBar.style.display = model.StaminaVisible ? DisplayStyle.Flex : DisplayStyle.None;
            staminaFill.style.width = Length.Percent(model.StaminaFill * 100f);
        }

        static void Absolute(VisualElement e) => e.style.position = Position.Absolute;

        static Label CenteredLabel(string name, float size)
        {
            var label = new Label { name = name, pickingMode = PickingMode.Ignore };
            Absolute(label);
            label.style.left = 0f;
            label.style.right = 0f;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.fontSize = size;
            label.style.color = new Color(0.92f, 0.92f, 0.88f, 1f);
            label.style.display = DisplayStyle.None;
            return label;
        }
    }
}
```

`Assets/Player/Runtime/UI/PlayerHud.cs`:
```csharp
using UnityEngine;
using UnityEngine.UIElements;

namespace TycoWorld.Player
{
    /// <summary>Renders FirstPersonPlayer.Hud into this GameObject's UIDocument (built lazily once the panel exists).</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class PlayerHud : MonoBehaviour, IPlayerModule
    {
        HudView view;

        public int Order => 40;
        public HudView View => view;

        public void Initialize(FirstPersonPlayer player) => TryBuild();

        public void Tick(FirstPersonPlayer player, float dt)
        {
            if (view == null) TryBuild();
            if (view != null) view.Render(player.Hud);
        }

        void TryBuild()
        {
            var document = GetComponent<UIDocument>();
            if (document == null || document.rootVisualElement == null) return;
            view = new HudView(document.rootVisualElement);
        }
    }
}
```

- [ ] **Step 4: Jalankan tes, pastikan lulus**

Procedure R, lalu Procedure T dengan `Group = "FlashlightTests|HudViewTests"`.
Expected: `passed=9 failed=0`.

Lalu `Group = "."`. Expected: `failed=0`.

- [ ] **Step 5: Commit**

Procedure C:
- path: `Assets/Player`
- subject: `feat(player): flashlight and UI Toolkit HUD`

---

### Task 14: Titik spawn dan spawner

**Files:**
- Create: `Assets/Player/Runtime/Core/PlayerSpawnPoint.cs`, `Assets/Player/Runtime/Core/PlayerSpawner.cs`
- Test: `Assets/Player/Tests/PlayerSpawnerTests.cs`

**Interfaces:**
- Consumes: `FirstPersonPlayer` (`Initialize`, `Teleport`, `Locks`, `Hud`, `Tuning`, `LockCursor`, `FellOutOfWorld`), `SpawnNudge`, `GeneratedRoot` (WorldGen).
- Produces:
  - `PlayerSpawnPoint : MonoBehaviour` dengan `Configure(string id, string arrivalMessage)`, `Id`, `ArrivalMessage`, `Feet`, `Yaw`.
  - `PlayerSpawner : MonoBehaviour` dengan:
    - `enum SpawnState { WaitingForWorld, Spawned, WorldMissing }`, `State`, `WaitedSeconds`;
    - `Func<bool> WorldReady` (bisa diganti di tes);
    - `Configure(FirstPersonPlayer, PlayerSpawnPoint)`, `Begin()`, `Tick(float dt)`, `TrySpawnNow()` → bool, `TryFindSpawn(Vector3 desiredFeet, out Vector3 feet)` → bool;
    - konstanta `WorldMissingMessage`, `RespawnMessage`.

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/Player/Tests/PlayerSpawnerTests.cs`:
```csharp
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TycoWorld.Player.Tests
{
    public class PlayerSpawnerTests
    {
        PhysicsFixture fx;

        [SetUp]
        public void SetUp()
        {
            fx = new PhysicsFixture();
            GroundProbe.ClearCache();
        }

        [TearDown]
        public void TearDown()
        {
            fx.Dispose();
            GroundProbe.ClearCache();
        }

        (TestRig Rig, PlayerSpawner Spawner, PlayerSpawnPoint Point) Setup(bool worldReady)
        {
            fx.Floor(0.3f);
            var pointGo = fx.Track(new GameObject("PlayerSpawn_Kos"));
            pointGo.transform.SetPositionAndRotation(PhysicsFixture.Origin, Quaternion.Euler(0f, 180f, 0f));
            var point = pointGo.AddComponent<PlayerSpawnPoint>();
            point.Configure("kos", "Kos Raflesia 3");
            fx.Sync();
            var rig = TestRig.Create(fx, new Vector3(0f, 0.3f, -10f)).Init();
            var spawner = rig.Root.AddComponent<PlayerSpawner>();
            spawner.Configure(rig.Player, point);
            spawner.WorldReady = () => worldReady;
            spawner.Begin();
            return (rig, spawner, point);
        }

        [Test]
        public void Spawns_OnProbedFloor_FacingSpawnYaw()
        {
            var (rig, spawner, _) = Setup(true);
            spawner.Tick(0.016f);
            Assert.AreEqual(PlayerSpawner.SpawnState.Spawned, spawner.State);
            Assert.AreEqual(0.36f, rig.Local.y, 0.02f);
            Assert.AreEqual(0f, rig.Local.x, 1e-3f);
            Assert.AreEqual(0f, rig.Local.z, 1e-3f);
            Assert.AreEqual(180f, rig.Player.Yaw, 1e-3f);
            Assert.IsTrue(rig.Player.Locks.Allows(PlayerAbility.All));
            Assert.IsTrue(rig.Player.Hud.CrosshairVisible);
            Assert.AreEqual("Kos Raflesia 3", rig.Player.Hud.Message);
        }

        [Test]
        public void Waiting_KeepsPlayerFrozen()
        {
            var (rig, spawner, _) = Setup(false);
            rig.Intents.Hold(new PlayerIntent { Move = Vector2.up }, 1f);
            spawner.Tick(1f);
            rig.Run(1f);
            Assert.AreEqual(PlayerSpawner.SpawnState.WaitingForWorld, spawner.State);
            Assert.IsFalse(rig.Player.Locks.Allows(PlayerAbility.Move));
            Assert.IsFalse(rig.Root.GetComponent<CharacterController>().enabled);
            Assert.AreEqual(-10f, rig.Local.z, 1e-3f);
            Assert.IsFalse(rig.Player.Hud.CrosshairVisible);
        }

        [Test]
        public void WorldMissing_AfterTimeout()
        {
            var (rig, spawner, _) = Setup(false);
            LogAssert.Expect(LogType.Error, new Regex("Dunia belum di-generate"));
            for (int i = 0; i < 16; i++) spawner.Tick(1f);
            Assert.AreEqual(PlayerSpawner.SpawnState.WorldMissing, spawner.State);
            StringAssert.Contains("belum di-generate", rig.Player.Hud.Message);
            Assert.IsFalse(rig.Player.Locks.Allows(PlayerAbility.Move));
        }

        [Test]
        public void Nudges_OutOfBlocker()
        {
            var (rig, spawner, _) = Setup(true);
            fx.Box("Lamp", new Vector3(0f, 1.2f, 0f), new Vector3(0.5f, 1.8f, 0.5f));
            fx.Sync();
            Assert.IsTrue(spawner.TrySpawnNow());
            float planar = new Vector2(rig.Local.x, rig.Local.z).magnitude;
            Assert.That(planar, Is.InRange(0.4f, 2.01f));
            var feet = rig.Root.transform.position;
            float r = rig.Tuning.body.radius;
            Assert.IsFalse(Physics.CheckCapsule(feet + Vector3.up * (r + 0.05f), feet + Vector3.up * (1.8f - r), r,
                PlayerLayers.QueryMask, QueryTriggerInteraction.Ignore));
        }

        [Test]
        public void TryFindSpawn_AllBlocked_ReturnsFalse()
        {
            var (_, spawner, point) = Setup(true);
            fx.Box("Building", new Vector3(0f, 1.8f, 0f), new Vector3(6f, 3f, 6f));
            fx.Sync();
            Assert.IsFalse(spawner.TryFindSpawn(point.Feet, out var feet));
            Assert.AreEqual(point.Feet, feet);
        }

        [Test]
        public void FellOutOfWorld_RespawnsAtPoint()
        {
            var (rig, spawner, _) = Setup(true);
            spawner.Tick(0.016f);
            rig.Player.Teleport(new Vector3(5000f, -30f, 5000f), 0f);
            rig.Player.Tick(1f / 60f);
            Assert.AreEqual(0.36f, rig.Local.y, 0.05f);
            Assert.AreEqual(PlayerSpawner.RespawnMessage, rig.Player.Hud.Message);
        }

        [Test]
        public void MissingSpawnPoint_ReportsWorldMissing()
        {
            fx.Floor();
            fx.Sync();
            var rig = TestRig.Create(fx, Vector3.zero).Init();
            var spawner = rig.Root.AddComponent<PlayerSpawner>();
            spawner.Configure(rig.Player, null);
            spawner.WorldReady = () => true;
            spawner.Begin();
            LogAssert.Expect(LogType.Error, new Regex("PlayerSpawnPoint"));
            spawner.Tick(0.016f);
            Assert.AreEqual(PlayerSpawner.SpawnState.WorldMissing, spawner.State);
        }
    }
}
```

- [ ] **Step 2: Jalankan tes, pastikan gagal**

Procedure R, lalu Procedure T dengan `Group = "PlayerSpawnerTests"`.
Expected: COMPILE FAILED (`CS0246 PlayerSpawner`, `PlayerSpawnPoint`).

- [ ] **Step 3: Implementasi**

`Assets/Player/Runtime/Core/PlayerSpawnPoint.cs`:
```csharp
using UnityEngine;

namespace TycoWorld.Player
{
    /// <summary>Where the player appears (feet position + facing). Manual content in World.unity.</summary>
    public sealed class PlayerSpawnPoint : MonoBehaviour
    {
        [SerializeField] string id = "kos";
        [SerializeField] string arrivalMessage = "Kos Raflesia 3";

        public string Id => id;
        public string ArrivalMessage => arrivalMessage;
        public Vector3 Feet => transform.position;
        public float Yaw => transform.eulerAngles.y;

        public void Configure(string spawnId, string message)
        {
            id = spawnId ?? "";
            arrivalMessage = message ?? "";
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.35f);
            Gizmos.DrawLine(transform.position + Vector3.up * 1.6f, transform.position + Vector3.up * 1.6f + transform.forward);
        }
    }
}
```

`Assets/Player/Runtime/Core/PlayerSpawner.cs`:
```csharp
using System;
using TycoWorld.WorldGen;
using UnityEngine;

namespace TycoWorld.Player
{
    /// <summary>Keeps the player frozen until the generated world exists, then places it at the spawn point; respawns after a fall.</summary>
    public sealed class PlayerSpawner : MonoBehaviour
    {
        public const string WorldMissingMessage = "Dunia belum di-generate. Buka Tyco World > World Generator.";
        public const string RespawnMessage = "Kamu kembali ke kos.";

        public enum SpawnState { WaitingForWorld, Spawned, WorldMissing }

        [SerializeField] FirstPersonPlayer player;
        [SerializeField] PlayerSpawnPoint spawnPoint;

        public Func<bool> WorldReady;
        public SpawnState State { get; private set; } = SpawnState.WaitingForWorld;
        public float WaitedSeconds { get; private set; }
        public PlayerSpawnPoint SpawnPoint => spawnPoint;

        public void Configure(FirstPersonPlayer target, PlayerSpawnPoint point)
        {
            player = target;
            spawnPoint = point;
        }

        void Start()
        {
            if (Application.isPlaying) Begin();
        }

        void Update()
        {
            if (Application.isPlaying) Tick(Time.deltaTime);
        }

        void OnDestroy()
        {
            if (player != null) player.FellOutOfWorld -= OnFell;
        }

        public void Begin()
        {
            if (player == null) player = GetComponent<FirstPersonPlayer>();
            player.Initialize();
            player.Locks.Acquire(this, PlayerAbility.All);
            player.Hud.CrosshairVisible = false;
            State = SpawnState.WaitingForWorld;
            WaitedSeconds = 0f;
            player.FellOutOfWorld -= OnFell;
            player.FellOutOfWorld += OnFell;
        }

        public void Tick(float dt)
        {
            if (State != SpawnState.WaitingForWorld) return;
            WaitedSeconds += dt;
            if (IsWorldReady() && TrySpawnNow()) return;
            if (State == SpawnState.WaitingForWorld && WaitedSeconds >= player.Tuning.worldWaitSeconds) Fail(WorldMissingMessage);
        }

        public bool TrySpawnNow()
        {
            if (spawnPoint == null)
            {
                Fail("PlayerSpawnPoint belum di-set di PlayerSpawner. Jalankan Tyco World > Player Setup.");
                return false;
            }
            if (!TryFindSpawn(spawnPoint.Feet, out var feet))
                Debug.LogWarning("[Player] Titik spawn terhalang; pemain tetap ditaruh di " + spawnPoint.name + ".");
            player.Teleport(feet, spawnPoint.Yaw);
            player.Locks.Release(this);
            player.Hud.CrosshairVisible = true;
            player.Hud.ShowMessage(spawnPoint.ArrivalMessage);
            player.LockCursor();
            State = SpawnState.Spawned;
            return true;
        }

        /// <summary>Probes the floor under each nudge candidate and returns the first one where the standing capsule fits.</summary>
        public bool TryFindSpawn(Vector3 desiredFeet, out Vector3 feet)
        {
            Physics.SyncTransforms();
            float r = player.Tuning.body.radius, h = player.Tuning.body.height;
            foreach (var candidate in SpawnNudge.Candidates(desiredFeet))
            {
                // Start 1 m up: a ray that starts inside a blocker (lamp, wall) ignores it and finds the real floor,
                // so the capsule test below rejects the spot instead of spawning on top of the blocker.
                if (!Physics.Raycast(candidate + Vector3.up, Vector3.down, out var hit, 3f, PlayerLayers.QueryMask, QueryTriggerInteraction.Ignore))
                    continue;
                var f = hit.point + Vector3.up * 0.06f;
                if (Physics.CheckCapsule(f + Vector3.up * (r + 0.05f), f + Vector3.up * (h - r), r, PlayerLayers.QueryMask, QueryTriggerInteraction.Ignore))
                    continue;
                feet = f;
                return true;
            }
            feet = desiredFeet;
            return false;
        }

        bool IsWorldReady() => WorldReady != null ? WorldReady() : UnityEngine.Object.FindAnyObjectByType<GeneratedRoot>() != null;

        void Fail(string message)
        {
            State = SpawnState.WorldMissing;
            player.Hud.ShowMessage(message, 3600f);
            Debug.LogError("[Player] " + message);
        }

        void OnFell()
        {
            if (State != SpawnState.Spawned || spawnPoint == null) return;
            TryFindSpawn(spawnPoint.Feet, out var feet);
            player.Teleport(feet, spawnPoint.Yaw);
            player.Hud.ShowMessage(RespawnMessage);
        }
    }
}
```

- [ ] **Step 4: Jalankan tes, pastikan lulus**

Procedure R, lalu Procedure T dengan `Group = "PlayerSpawnerTests"`.
Expected: `passed=7 failed=0`.

Lalu `Group = "."`. Expected: `failed=0`.

- [ ] **Step 5: Commit**

Procedure C:
- path: `Assets/Player`
- subject: `feat(player): spawn point and spawner with world wait, nudge and respawn`

---
### Task 15: Audio Kenney CC0, peta audio, aturan import

**Files:**
- Create: `Assets/Player/Editor/PlayerAudioMap.cs`, `Assets/Player/Editor/AudioImportRules.cs`
- Create: `Assets/Player/Audio/Footsteps/*.ogg`, `Assets/Player/Audio/Interactions/*.ogg`, `Assets/Player/Audio/SOURCES.md`
- Test: `Assets/Player/Tests/AudioAssetsTests.cs`

**Interfaces:**
- Consumes: `SurfaceType` (WorldGen), `PlayerPaths`.
- Produces:
  - `FootstepSet { SurfaceType Surface; string[] Files; float Volume, PitchMin, PitchMax; }`.
  - `PlayerAudioMap` dengan:
    - `Footsteps` (`FootstepSet[]`, satu per `SurfaceType`) dan `AllFootstepFiles`;
    - konstanta `GateUnlatch`, `GateCreak`, `GateLatch`, `DoorRattle`, `FlashlightClick`, `VehicleKeys`, dan `InteractionFiles`;
    - `FootstepPath(file)` dan `InteractionPath(file)`.

- [ ] **Step 1: Download ke folder kosong baru dan daftar isinya**

```powershell
$base = "D:\Temp\claude\D--UNITY-project-Tyco-World\57a160ea-9ce5-4cb2-9cf1-6426eefd324c\scratchpad\kenney"
New-Item -ItemType Directory -Force "$base\impact", "$base\rpg" | Out-Null
Invoke-WebRequest -Uri "https://kenney.nl/media/pages/assets/impact-sounds/87b4ddecda-1677589768/kenney_impact-sounds.zip" -OutFile "$base\impact\pack.zip" -UserAgent "TycoWorld/1.0"
Invoke-WebRequest -Uri "https://kenney.nl/media/pages/assets/rpg-audio/8e99002d76-1677590336/kenney_rpg-audio.zip" -OutFile "$base\rpg\pack.zip" -UserAgent "TycoWorld/1.0"
Expand-Archive "$base\impact\pack.zip" -DestinationPath "$base\impact\x" -Force
Expand-Archive "$base\rpg\pack.zip" -DestinationPath "$base\rpg\x" -Force
Get-ChildItem -Recurse "$base\impact\x", "$base\rpg\x" -Filter *.ogg | Select-Object -ExpandProperty Name | Sort-Object
Get-ChildItem -Recurse "$base\impact\x", "$base\rpg\x" -Include *.txt | ForEach-Object { $_.FullName; Get-Content $_.FullName -TotalCount 12 }
```

Expected:
- Impact Sounds berisi 130 file `.ogg`, termasuk `footstep_concrete_000…004`, `footstep_grass_000…004`, `footstep_snow_000…004`.
- RPG Audio berisi 50 file `.ogg`, termasuk `footstep00…09`, `creak1…3`, `metalLatch`, `metalClick`, `handleCoins`.
- File lisensi menyebut CC0 / Creative Commons Zero.

Kalau ada URL yang 404, buka halaman aset (`https://kenney.nl/assets/impact-sounds`, `https://kenney.nl/assets/rpg-audio`) lewat WebFetch dan ambil tautan "Continue without donating".

**Kalau nama file berbeda dari yang di atas**, sesuaikan nama di `PlayerAudioMap` (Step 3) dengan file yang benar-benar ada, lalu catat di ledger:
`Task 15: Ruling: <nama asli> dipakai untuk <peran> — cost if wrong: bunyi kurang pas, cukup ganti nama di PlayerAudioMap`.

- [ ] **Step 2: Tulis tes yang gagal**

`Assets/Player/Tests/AudioAssetsTests.cs`:
```csharp
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TycoWorld.Player.Authoring;
using TycoWorld.WorldGen;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.Player.Tests
{
    public class AudioAssetsTests
    {
        [Test]
        public void FootstepSets_CoverEverySurface()
        {
            foreach (SurfaceType surface in Enum.GetValues(typeof(SurfaceType)))
            {
                var set = PlayerAudioMap.Footsteps.SingleOrDefault(f => f.Surface == surface);
                Assert.IsNotNull(set, surface.ToString());
                Assert.Greater(set.Files.Length, 0, surface.ToString());
                Assert.LessOrEqual(set.PitchMin, set.PitchMax);
            }
        }

        [Test]
        public void MappedFiles_AreImportedAsMonoDecompressedClips()
        {
            var paths = PlayerAudioMap.AllFootstepFiles.Select(PlayerAudioMap.FootstepPath)
                .Concat(PlayerAudioMap.InteractionFiles.Select(PlayerAudioMap.InteractionPath));
            foreach (var path in paths)
            {
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<AudioClip>(path), path);
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                Assert.IsTrue(importer.forceToMono, path);
                Assert.AreEqual(AudioClipLoadType.DecompressOnLoad, importer.defaultSampleSettings.loadType, path);
            }
        }

        [Test]
        public void Sources_ListEveryFileAndLicense()
        {
            string text = File.ReadAllText(WorldGenPaths.ToAbsolute(PlayerPaths.AudioSourcesFile));
            StringAssert.Contains("CC0", text);
            StringAssert.Contains("https://kenney.nl/assets/impact-sounds", text);
            StringAssert.Contains("https://kenney.nl/assets/rpg-audio", text);
            foreach (var file in PlayerAudioMap.AllFootstepFiles.Concat(PlayerAudioMap.InteractionFiles))
                StringAssert.Contains(file, text);
        }
    }
}
```

- [ ] **Step 3: Jalankan tes, pastikan gagal; lalu tulis peta dan aturan import**

Procedure R, lalu Procedure T dengan `Group = "AudioAssetsTests"`.
Expected: COMPILE FAILED (`CS0103 PlayerAudioMap`).

`Assets/Player/Editor/PlayerAudioMap.cs`:
```csharp
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TycoWorld.WorldGen;

namespace TycoWorld.Player.Authoring
{
    public sealed class FootstepSet
    {
        public SurfaceType Surface;
        public string[] Files;
        public float Volume, PitchMin, PitchMax;
    }

    /// <summary>Which Kenney CC0 clip plays for which surface or interaction (spec #2 §7).</summary>
    public static class PlayerAudioMap
    {
        public const string GateUnlatch = "metalLatch.ogg";
        public const string GateCreak = "creak1.ogg";
        public const string GateLatch = "metalLatch.ogg";
        public const string DoorRattle = "metalLatch.ogg";
        public const string FlashlightClick = "metalClick.ogg";
        public const string VehicleKeys = "handleCoins.ogg";

        public static readonly string[] InteractionFiles = { "metalLatch.ogg", "creak1.ogg", "metalClick.ogg", "handleCoins.ogg" };

        static readonly string[] Concrete = Seq("footstep_concrete_{0:000}.ogg", 5);

        public static readonly FootstepSet[] Footsteps =
        {
            Set(SurfaceType.Default, Concrete, 0.45f, 0.95f, 1.05f),
            Set(SurfaceType.Asphalt, Concrete, 0.45f, 0.88f, 0.98f),
            Set(SurfaceType.Concrete, Concrete, 0.45f, 0.98f, 1.08f),
            Set(SurfaceType.Paving, Concrete, 0.45f, 1.05f, 1.15f),
            Set(SurfaceType.Grass, Seq("footstep_grass_{0:000}.ogg", 5), 0.5f, 0.95f, 1.05f),
            Set(SurfaceType.Dirt, Seq("footstep{0:00}.ogg", 10), 0.5f, 0.9f, 1.0f),
            Set(SurfaceType.Water, Seq("footstep_snow_{0:000}.ogg", 5), 0.5f, 0.7f, 0.8f)
        };

        public static IEnumerable<string> AllFootstepFiles => Footsteps.SelectMany(f => f.Files).Distinct();

        public static string FootstepPath(string file) => PlayerPaths.FootstepAudioFolder + "/" + file;

        public static string InteractionPath(string file) => PlayerPaths.InteractionAudioFolder + "/" + file;

        static FootstepSet Set(SurfaceType surface, string[] files, float volume, float pitchMin, float pitchMax) =>
            new FootstepSet { Surface = surface, Files = files, Volume = volume, PitchMin = pitchMin, PitchMax = pitchMax };

        static string[] Seq(string format, int count)
        {
            var result = new string[count];
            for (int i = 0; i < count; i++) result[i] = string.Format(CultureInfo.InvariantCulture, format, i);
            return result;
        }
    }
}
```

`Assets/Player/Editor/AudioImportRules.cs`:
```csharp
using System;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.Player.Authoring
{
    /// <summary>Player sound effects: mono, Vorbis, decompressed on load (short one-shots).</summary>
    public sealed class AudioImportRules : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(PlayerPaths.AudioFolder + "/", StringComparison.Ordinal)) return;
            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = true;
            importer.loadInBackground = false;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            importer.defaultSampleSettings = settings;
        }
    }
}
```

Procedure R, lalu Procedure T dengan `Group = "AudioAssetsTests"`.
Expected:
- `failed=2` (file audio dan `SOURCES.md` belum ada);
- `FootstepSets_CoverEverySurface` lulus.

- [ ] **Step 4: Salin file yang dipakai dan tulis SOURCES.md**

Aturan import harus sudah ter-compile (Step 3) **sebelum** file `.ogg` masuk ke Assets.

```powershell
$base = "D:\Temp\claude\D--UNITY-project-Tyco-World\57a160ea-9ce5-4cb2-9cf1-6426eefd324c\scratchpad\kenney"
$dst = "D:\UNITY\project\Tyco World\Assets\Player\Audio"
New-Item -ItemType Directory -Force "$dst\Footsteps", "$dst\Interactions" | Out-Null
$foot = @(0..4 | ForEach-Object { "footstep_concrete_{0:000}.ogg" -f $_; "footstep_grass_{0:000}.ogg" -f $_; "footstep_snow_{0:000}.ogg" -f $_ }) + @(0..9 | ForEach-Object { "footstep{0:00}.ogg" -f $_ })
foreach ($f in $foot) { Copy-Item (Get-ChildItem -Recurse "$base\impact\x", "$base\rpg\x" -Filter $f | Select-Object -First 1).FullName "$dst\Footsteps\$f" }
foreach ($f in @("metalLatch.ogg", "creak1.ogg", "metalClick.ogg", "handleCoins.ogg")) { Copy-Item (Get-ChildItem -Recurse "$base\rpg\x" -Filter $f | Select-Object -First 1).FullName "$dst\Interactions\$f" }
(Get-ChildItem "$dst\Footsteps").Count; (Get-ChildItem "$dst\Interactions").Count
```
Expected: `25` dan `4`. Kalau ada `Copy-Item` yang gagal karena `$null`, berarti nama file berbeda; kembali ke catatan di Step 1.

Tulis `Assets/Player/Audio/SOURCES.md`:
```markdown
# Sumber audio player

Semua file di folder ini berasal dari paket Kenney berlisensi **CC0 1.0 (Creative Commons Zero, domain publik)**.
Atribusi tidak wajib, tapi dicatat di sini.

| Paket | URL |
|---|---|
| Kenney Impact Sounds | https://kenney.nl/assets/impact-sounds |
| Kenney RPG Audio | https://kenney.nl/assets/rpg-audio |

| File | Paket | Dipakai untuk |
|---|---|---|
| Footsteps/footstep_concrete_000.ogg … footstep_concrete_004.ogg | Impact Sounds | langkah di aspal, beton, paving, default |
| Footsteps/footstep_grass_000.ogg … footstep_grass_004.ogg | Impact Sounds | langkah di rumput, sawah, lapangan |
| Footsteps/footstep_snow_000.ogg … footstep_snow_004.ogg | Impact Sounds | langkah di air dangkal (pitch rendah) |
| Footsteps/footstep00.ogg … footstep09.ogg | RPG Audio | langkah di tanah |
| Interactions/metalLatch.ogg | RPG Audio | gerendel pagar, gagang pintu terkunci |
| Interactions/creak1.ogg | RPG Audio | derit pagar |
| Interactions/metalClick.ogg | RPG Audio | klik senter |
| Interactions/handleCoins.ogg | RPG Audio | gemerincing kunci motor |

Nama lengkap tiap file: footstep_concrete_000.ogg, footstep_concrete_001.ogg, footstep_concrete_002.ogg,
footstep_concrete_003.ogg, footstep_concrete_004.ogg, footstep_grass_000.ogg, footstep_grass_001.ogg,
footstep_grass_002.ogg, footstep_grass_003.ogg, footstep_grass_004.ogg, footstep_snow_000.ogg, footstep_snow_001.ogg,
footstep_snow_002.ogg, footstep_snow_003.ogg, footstep_snow_004.ogg, footstep00.ogg, footstep01.ogg, footstep02.ogg,
footstep03.ogg, footstep04.ogg, footstep05.ogg, footstep06.ogg, footstep07.ogg, footstep08.ogg, footstep09.ogg,
metalLatch.ogg, creak1.ogg, metalClick.ogg, handleCoins.ogg.
```

- [ ] **Step 5: Jalankan tes, pastikan lulus**

Procedure R, lalu Procedure T dengan `Group = "AudioAssetsTests"`.
Expected: `passed=3 failed=0`.

Lalu `Group = "."`. Expected: `failed=0`.

- [ ] **Step 6: Commit**

Procedure C:
- path: `Assets/Player`
- subject: `chore(player): add Kenney CC0 audio with sources and import rules`

---

### Task 16: `KosSitePlanner` (penataan halaman depan kos)

**Files:**
- Create: `Assets/Player/Editor/KosSitePlanner.cs`, `Assets/Player/Editor/KosSiteSource.cs`
- Test: `Assets/Player/Tests/KosSitePlannerTests.cs`

**Interfaces:**
- Consumes: `Polygon2D` (`EnsureWinding`, `ContainsPoint`, `Cross`, `DistanceToSegment`), `ClassifiedWorld`, `BuildingFeature`, `RoadFeature` (WorldGen editor).
- Produces:
  - `struct RoadLine { long Id; List<Vector2> Points; float HalfWidth; }`.
  - `KosSiteOptions` dengan field `fenceRoadGap = 1`, `minYard = 2.5`, `maxYard = 6`, `gateWidth = 2`, `spawnOffset = 1.2`, `minEdge = 3`, `motorDepthRatio = 0.45`, `sampleStep = 0.5`, dan `preferredOutward` (`Vector2`; zero berarti otomatis).
  - `struct KosSitePlan { int EdgeIndex; Vector2 WallA, WallB, Along, Outward, Door, Gate, Spawn, Motor; float YardDepth, SpawnYaw, MotorYaw, RoadClearance, RoadDistance; long RoadId; }`.
  - `KosSitePlanner`:
    - `TryPlan(IReadOnlyList<Vector2> footprint, IReadOnlyList<RoadLine> roads, IReadOnlyList<IReadOnlyList<Vector2>> neighbours, KosSiteOptions options, out KosSitePlan plan, out string reason)` → bool;
    - `YawOf(Vector2)` dan `Describe(KosSitePlan)`.
  - `KosSiteSource.TryBuild(ClassifiedWorld world, long kosId, float radius, out List<Vector2> footprint, out List<RoadLine> roads, out List<IReadOnlyList<Vector2>> neighbours)` → bool.

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/Player/Tests/KosSitePlannerTests.cs`:
```csharp
using System.Collections.Generic;
using NUnit.Framework;
using TycoWorld.Player.Authoring;
using UnityEngine;

namespace TycoWorld.Player.Tests
{
    public class KosSitePlannerTests
    {
        static readonly List<Vector2> Kos = new List<Vector2> { new Vector2(0f, 0f), new Vector2(10f, 0f), new Vector2(10f, 12f), new Vector2(0f, 12f) };
        static readonly List<IReadOnlyList<Vector2>> NoNeighbours = new List<IReadOnlyList<Vector2>>();

        static RoadLine Road(long id, float halfWidth, params Vector2[] points) =>
            new RoadLine { Id = id, HalfWidth = halfWidth, Points = new List<Vector2>(points) };

        static KosSitePlan Plan(IReadOnlyList<Vector2> footprint, IReadOnlyList<RoadLine> roads, IReadOnlyList<IReadOnlyList<Vector2>> neighbours, KosSiteOptions options = null)
        {
            Assert.IsTrue(KosSitePlanner.TryPlan(footprint, roads, neighbours, options ?? new KosSiteOptions(), out var plan, out string reason), reason);
            return plan;
        }

        static void Near(Vector2 expected, Vector2 actual, float tolerance = 1e-3f)
        {
            Assert.AreEqual(expected.x, actual.x, tolerance, "x");
            Assert.AreEqual(expected.y, actual.y, tolerance, "y");
        }

        [Test]
        public void PicksEdgeFacingNearestRoad()
        {
            var plan = Plan(Kos, new[] { Road(1, 2.5f, new Vector2(3f, -9f), new Vector2(3f, -30f)) }, NoNeighbours);
            Assert.AreEqual(0, plan.EdgeIndex);
            Near(new Vector2(0f, -1f), plan.Outward);
            Assert.AreEqual(180f, plan.SpawnYaw, 1e-3f);
            Assert.AreEqual(5.5f, plan.YardDepth, 1e-3f);
            Assert.AreEqual(6.5f, plan.RoadClearance, 1e-3f);
            Assert.AreEqual(1, plan.RoadId);
            Near(new Vector2(3f, 0f), plan.Door);
            Near(new Vector2(3f, -5.5f), plan.Gate);
            Near(new Vector2(3f, -1.2f), plan.Spawn);
            Near(new Vector2(7f, -2.475f), plan.Motor);
            Assert.AreEqual(90f, plan.MotorYaw, 1e-3f);
        }

        [Test]
        public void Motor_StaysOutOfGateCorridor()
        {
            var plan = Plan(Kos, new[] { Road(1, 2.5f, new Vector2(3f, -9f), new Vector2(3f, -30f)) }, NoNeighbours);
            Assert.GreaterOrEqual(Mathf.Abs(Vector2.Dot(plan.Motor - plan.Door, plan.Along)), 1.5f);
        }

        [Test]
        public void NeighbourBlockingYard_FallsBackToNextEdge()
        {
            var roads = new[]
            {
                Road(1, 2.5f, new Vector2(3f, -9f), new Vector2(3f, -30f)),
                Road(2, 2f, new Vector2(20f, -10f), new Vector2(20f, 30f))
            };
            var blocker = new List<IReadOnlyList<Vector2>>
            {
                new List<Vector2> { new Vector2(0f, -3f), new Vector2(10f, -3f), new Vector2(10f, -1f), new Vector2(0f, -1f) }
            };
            var plan = Plan(Kos, roads, blocker);
            Assert.AreEqual(1, plan.EdgeIndex);
            Assert.AreEqual(90f, plan.SpawnYaw, 1e-3f);
            Assert.AreEqual(6f, plan.YardDepth, 1e-3f);
            Assert.AreEqual(2, plan.RoadId);
        }

        [Test]
        public void Yard_ClampsToMaxAndMin()
        {
            Assert.AreEqual(6f, Plan(Kos, new[] { Road(1, 2.5f, new Vector2(3f, -40f), new Vector2(3f, -60f)) }, NoNeighbours).YardDepth, 1e-3f);
            Assert.AreEqual(2.5f, Plan(Kos, new[] { Road(1, 2.5f, new Vector2(3f, -3f), new Vector2(3f, -30f)) }, NoNeighbours).YardDepth, 1e-3f);
        }

        [Test]
        public void Gate_ClampedInsideEdge()
        {
            var plan = Plan(Kos, new[] { Road(1, 2.5f, new Vector2(0.5f, -9f), new Vector2(0.5f, -30f)) }, NoNeighbours);
            Assert.AreEqual(0, plan.EdgeIndex);
            Near(new Vector2(1.5f, 0f), plan.Door);
        }

        [Test]
        public void ClockwiseFootprint_GivesSameResult()
        {
            var clockwise = new List<Vector2>(Kos);
            clockwise.Reverse();
            var plan = Plan(clockwise, new[] { Road(1, 2.5f, new Vector2(3f, -9f), new Vector2(3f, -30f)) }, NoNeighbours);
            Assert.AreEqual(180f, plan.SpawnYaw, 1e-3f);
            Near(new Vector2(3f, 0f), plan.Door);
        }

        [Test]
        public void PreferredOutward_OverridesNearestRoad()
        {
            var roads = new[]
            {
                Road(1, 2.5f, new Vector2(3f, -9f), new Vector2(3f, -30f)),
                Road(2, 2f, new Vector2(20f, -10f), new Vector2(20f, 30f))
            };
            var plan = Plan(Kos, roads, NoNeighbours, new KosSiteOptions { preferredOutward = new Vector2(1f, 0f) });
            Assert.AreEqual(90f, plan.SpawnYaw, 1e-3f);
            Assert.AreEqual(2, plan.RoadId);
        }

        [Test]
        public void NoRoads_ReturnsFalseWithReason()
        {
            Assert.IsFalse(KosSitePlanner.TryPlan(Kos, new RoadLine[0], NoNeighbours, new KosSiteOptions(), out _, out string reason));
            StringAssert.Contains("jalan", reason);
        }

        [Test]
        public void AllRoadFacingEdgesBlocked_ReturnsFalse()
        {
            var blocker = new List<IReadOnlyList<Vector2>>
            {
                new List<Vector2> { new Vector2(0f, -3f), new Vector2(10f, -3f), new Vector2(10f, -1f), new Vector2(0f, -1f) }
            };
            Assert.IsFalse(KosSitePlanner.TryPlan(Kos, new[] { Road(1, 2.5f, new Vector2(3f, -9f), new Vector2(3f, -30f)) }, blocker,
                new KosSiteOptions(), out _, out string reason));
            StringAssert.Contains("terhalang", reason);
        }

        [Test]
        public void YawOf_UsesUnityConvention()
        {
            Assert.AreEqual(0f, KosSitePlanner.YawOf(new Vector2(0f, 1f)), 1e-4f);
            Assert.AreEqual(90f, KosSitePlanner.YawOf(new Vector2(1f, 0f)), 1e-4f);
            Assert.AreEqual(180f, KosSitePlanner.YawOf(new Vector2(0f, -1f)), 1e-4f);
            Assert.AreEqual(270f, KosSitePlanner.YawOf(new Vector2(-1f, 0f)), 1e-4f);
        }
    }
}
```

- [ ] **Step 2: Jalankan tes, pastikan gagal**

Procedure R, lalu Procedure T dengan `Group = "KosSitePlannerTests"`.
Expected: COMPILE FAILED (`CS0246 RoadLine`, `KosSitePlan`, `KosSiteOptions`; `CS0103 KosSitePlanner`).

- [ ] **Step 3: Implementasi**

`Assets/Player/Editor/KosSitePlanner.cs`:
```csharp
using System.Collections.Generic;
using System.Globalization;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.Player.Authoring
{
    public struct RoadLine
    {
        public long Id;
        public List<Vector2> Points;
        public float HalfWidth;
    }

    public sealed class KosSiteOptions
    {
        public float fenceRoadGap = 1f;
        public float minYard = 2.5f;
        public float maxYard = 6f;
        public float gateWidth = 2f;
        public float spawnOffset = 1.2f;
        public float minEdge = 3f;
        public float motorDepthRatio = 0.45f;
        public float sampleStep = 0.5f;
        /// <summary>Zero = pick the edge nearest a road; otherwise pick the road-facing edge whose outward normal is closest to this.</summary>
        public Vector2 preferredOutward = Vector2.zero;
    }

    /// <summary>Local 2D (x = east, y = north) layout of the kos front yard. Yaw follows Unity (0 = north, 90 = east).</summary>
    public struct KosSitePlan
    {
        public int EdgeIndex;
        public Vector2 WallA, WallB, Along, Outward, Door, Gate, Spawn, Motor;
        public float YardDepth, SpawnYaw, MotorYaw, RoadClearance, RoadDistance;
        public long RoadId;
    }

    /// <summary>Pure planner for the kos front yard (spec #2 §9): the wall facing the nearest road with an unobstructed yard.</summary>
    public static class KosSitePlanner
    {
        public static bool TryPlan(IReadOnlyList<Vector2> footprint, IReadOnlyList<RoadLine> roads, IReadOnlyList<IReadOnlyList<Vector2>> neighbours,
            KosSiteOptions options, out KosSitePlan plan, out string reason)
        {
            plan = default;
            reason = null;
            if (footprint == null || footprint.Count < 3) { reason = "Tapak kos tidak valid."; return false; }
            if (roads == null || roads.Count == 0) { reason = "Tidak ada jalan di sekitar kos."; return false; }
            var ring = new List<Vector2>(footprint);
            Polygon2D.EnsureWinding(ring, true);

            bool found = false, anyFacingRoad = false;
            float bestScore = float.MaxValue, bestLength = 0f;
            for (int i = 0; i < ring.Count; i++)
            {
                Vector2 a = ring[i], b = ring[(i + 1) % ring.Count];
                float length = Vector2.Distance(a, b);
                if (length < options.minEdge) continue;
                Vector2 along = (b - a) / length;
                Vector2 outward = new Vector2(along.y, -along.x);
                if (!NearestRoadInFront(a, along, length, outward, roads, options.sampleStep, out var roadPoint, out float distance, out var road)) continue;
                anyFacingRoad = true;
                float clearance = Vector2.Dot(roadPoint - a, outward) - road.HalfWidth;
                float yard = Mathf.Clamp(clearance - options.fenceRoadGap, options.minYard, options.maxYard);
                if (YardBlocked(a, b, along, outward, yard, neighbours)) continue;

                float score = options.preferredOutward.sqrMagnitude > 1e-6f ? -Vector2.Dot(outward, options.preferredOutward.normalized) : distance;
                bool better = !found || score < bestScore - 0.01f || (Mathf.Abs(score - bestScore) <= 0.01f && length > bestLength);
                if (!better) continue;
                plan = Build(i, a, along, outward, length, roadPoint, road, clearance, yard, distance, options);
                bestScore = score;
                bestLength = length;
                found = true;
            }
            if (found) return true;
            reason = anyFacingRoad
                ? "Semua sisi kos yang menghadap jalan terhalang bangunan tetangga."
                : "Tidak ada jalan di depan sisi mana pun dari kos.";
            return false;
        }

        public static float YawOf(Vector2 direction) => Mathf.Repeat(Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg, 360f);

        public static string Describe(KosSitePlan p) => string.Format(CultureInfo.InvariantCulture,
            "edge={0} outward=({1:F2},{2:F2}) yard={3:F2} clearance={4:F2} road={5} door=({6:F2},{7:F2}) gate=({8:F2},{9:F2}) spawn=({10:F2},{11:F2}) yaw={12:F1} motor=({13:F2},{14:F2}) motorYaw={15:F1}",
            p.EdgeIndex, p.Outward.x, p.Outward.y, p.YardDepth, p.RoadClearance, p.RoadId, p.Door.x, p.Door.y, p.Gate.x, p.Gate.y,
            p.Spawn.x, p.Spawn.y, p.SpawnYaw, p.Motor.x, p.Motor.y, p.MotorYaw);

        static KosSitePlan Build(int index, Vector2 a, Vector2 along, Vector2 outward, float length, Vector2 roadPoint, RoadLine road,
            float clearance, float yard, float distance, KosSiteOptions o)
        {
            float halfGate = o.gateWidth * 0.5f, margin = halfGate + 0.5f;
            float s = Vector2.Dot(roadPoint - a, along);
            s = length >= 2f * margin ? Mathf.Clamp(s, margin, length - margin) : length * 0.5f;
            var door = a + along * s;
            float leftRoom = s - halfGate, rightRoom = length - s - halfGate;
            float motorAlong = rightRoom >= leftRoom ? s + halfGate + rightRoom * 0.5f : leftRoom * 0.5f;
            return new KosSitePlan
            {
                EdgeIndex = index, WallA = a, WallB = a + along * length, Along = along, Outward = outward,
                Door = door, Gate = door + outward * yard, Spawn = door + outward * o.spawnOffset,
                Motor = a + along * motorAlong + outward * (yard * o.motorDepthRatio),
                YardDepth = yard, SpawnYaw = YawOf(outward), MotorYaw = YawOf(along),
                RoadClearance = clearance, RoadDistance = distance, RoadId = road.Id
            };
        }

        /// <summary>Closest road point (over samples along the edge) that lies in front of the wall.</summary>
        static bool NearestRoadInFront(Vector2 a, Vector2 along, float length, Vector2 outward, IReadOnlyList<RoadLine> roads, float step,
            out Vector2 point, out float distance, out RoadLine road)
        {
            point = default;
            road = default;
            distance = float.MaxValue;
            int samples = Mathf.Max(1, Mathf.CeilToInt(length / step));
            for (int k = 0; k <= samples; k++)
            {
                var e = a + along * (length * k / samples);
                foreach (var line in roads)
                {
                    if (line.Points == null) continue;
                    for (int j = 0; j + 1 < line.Points.Count; j++)
                    {
                        var r = ClosestOnSegment(e, line.Points[j], line.Points[j + 1]);
                        if (Vector2.Dot(r - e, outward) <= 0.1f) continue;
                        float d = Vector2.Distance(r, e);
                        if (d >= distance) continue;
                        distance = d;
                        point = r;
                        road = line;
                    }
                }
            }
            return distance < float.MaxValue;
        }

        static Vector2 ClosestOnSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-8f) return a;
            return a + ab * Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
        }

        static bool YardBlocked(Vector2 a, Vector2 b, Vector2 along, Vector2 outward, float yard, IReadOnlyList<IReadOnlyList<Vector2>> neighbours)
        {
            if (neighbours == null) return false;
            const float inset = 0.05f;
            var yardRing = new List<Vector2>
            {
                a + along * inset + outward * inset,
                b - along * inset + outward * inset,
                b - along * inset + outward * yard,
                a + along * inset + outward * yard
            };
            foreach (var other in neighbours)
            {
                if (other == null || other.Count < 3) continue;
                foreach (var p in other) if (Polygon2D.ContainsPoint(yardRing, p)) return true;
                foreach (var p in yardRing) if (Polygon2D.ContainsPoint(other, p)) return true;
                for (int i = 0; i < yardRing.Count; i++)
                for (int j = 0; j < other.Count; j++)
                    if (SegmentsCross(yardRing[i], yardRing[(i + 1) % yardRing.Count], other[j], other[(j + 1) % other.Count])) return true;
            }
            return false;
        }

        static bool SegmentsCross(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
        {
            float d1 = Polygon2D.Cross(p2 - p1, q1 - p1), d2 = Polygon2D.Cross(p2 - p1, q2 - p1);
            float d3 = Polygon2D.Cross(q2 - q1, p1 - q1), d4 = Polygon2D.Cross(q2 - q1, p2 - q1);
            return (d1 > 0f) != (d2 > 0f) && (d3 > 0f) != (d4 > 0f);
        }
    }
}
```

`Assets/Player/Editor/KosSiteSource.cs`:
```csharp
using System.Collections.Generic;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.Player.Authoring
{
    /// <summary>Extracts the kos footprint, nearby road pieces and neighbouring footprints from classified OSM data.</summary>
    public static class KosSiteSource
    {
        public static bool TryBuild(ClassifiedWorld world, long kosId, float radius, out List<Vector2> footprint,
            out List<RoadLine> roads, out List<IReadOnlyList<Vector2>> neighbours)
        {
            footprint = null;
            roads = new List<RoadLine>();
            neighbours = new List<IReadOnlyList<Vector2>>();
            BuildingFeature kos = null;
            foreach (var b in world.Buildings)
                if (b.Id == kosId) { kos = b; break; }
            if (kos == null) return false;

            footprint = new List<Vector2>(kos.Spec.Footprint.Outer);
            var center = kos.Centroid;
            foreach (var road in world.Roads)
                foreach (var piece in road.Pieces)
                    if (Near(piece.Points, center, radius))
                        roads.Add(new RoadLine { Id = road.Id, Points = new List<Vector2>(piece.Points), HalfWidth = road.Width * 0.5f });
            foreach (var b in world.Buildings)
                if (b.Id != kosId && Vector2.Distance(b.Centroid, center) <= radius) neighbours.Add(b.Spec.Footprint.Outer);
            return true;
        }

        static bool Near(List<Vector2> points, Vector2 center, float radius)
        {
            for (int i = 0; i + 1 < points.Count; i++)
                if (Polygon2D.DistanceToSegment(center, points[i], points[i + 1]) <= radius) return true;
            return false;
        }
    }
}
```

- [ ] **Step 4: Jalankan tes, pastikan lulus**

Procedure R, lalu Procedure T dengan `Group = "KosSitePlannerTests"`.
Expected: `passed=10 failed=0`.

- [ ] **Step 5: Cek dengan data asli (MCP)**

```csharp
using System.IO;
using TycoWorld.Player.Authoring;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var settings = AssetDatabase.LoadAssetAtPath<WorldGenSettings>(WorldGenPaths.SettingsAsset);
        var world = WorldMeshPipeline.Classify(File.ReadAllText(WorldGenPaths.ToAbsolute(settings.rawDataPath)), settings, new GenerationReport());
        if (!KosSiteSource.TryBuild(world, 409467778, 60f, out var footprint, out var roads, out var neighbours)) { result.LogError("kos not found"); return; }
        result.Log("footprint={0} roads={1} neighbours={2}", footprint.Count, roads.Count, neighbours.Count);
        if (!KosSitePlanner.TryPlan(footprint, roads, neighbours, new KosSiteOptions(), out var plan, out string reason)) { result.LogError(reason); return; }
        result.Log(KosSitePlanner.Describe(plan));
    }
}
```
Expected (prediksi dari data):
- `outward=(0.00,-1.00)`, `yard=6.00`, `road=689899501`;
- `door=(-321.3±0.5,-611.5±0.2)`;
- `spawn=(-321.3±0.5,-612.7±0.2)`, `yaw=180.0`.

**Kalau sisi yang terpilih lain:** jangan ubah planner. Catat `Describe` ke ledger. Arah gerbang tetap dikonfirmasi ke user di Task 18.

- [ ] **Step 6: Commit**

Procedure C:
- path: `Assets/Player`
- subject: `feat(player): kos front-yard site planner`

---

### Task 17: Builder aset, prop placeholder, dan prefab pemain

**Files:**
- Create: `Assets/Player/Editor/PlaceholderPropFactory.cs`, `Assets/Player/Editor/PlayerRigBuilder.cs`, `Assets/Player/Editor/PlayerAssetsBuilder.cs`
- Create (dihasilkan builder):
  - `Assets/Player/Config/PlayerTuning.asset`, `FootstepBank.asset`
  - `Assets/Player/UI/PlayerHudPanel.asset`, `PlayerHudTheme.tss`
  - `Assets/Player/Materials/*.mat`
  - `Assets/Player/Prefabs/{Player,Gate,KosDoor,Motor_Placeholder}.prefab` dan `Assets/Player/Prefabs/Meshes/*.asset`
- Test: `Assets/Player/Tests/PlayerAssetsTests.cs`

**Interfaces:**
- Consumes:
  - semua komponen runtime;
  - `PlayerAudioMap` (Task 15);
  - `MeshData` dan `PlaceholderMeshes` (`AddBox`, `AddCylinder`), `AssetFolders.Ensure`, `WorldGenPaths.MaterialsFolder`, `GroundSurface` (WorldGen).
- Produces:
  - `PlaceholderPropFactory` dengan:
    - konstanta `GateWidth = 2f`, `GateHeight = 1.8f`, `GateBottomGap = 0.14f`, `WallHeight = 1.92f`, `WallThickness = 0.15f`, `YardThickness = 0.12f`;
    - pembuat objek: `CreateGate(Material metal, AudioClip unlatch, AudioClip creak, AudioClip latch)`, `CreateDoor(Material wood, Material metal, AudioClip rattle)`, `CreateMotor(Material body, Material dark, AudioClip keys)` → `GameObject`;
    - `UnitBoxMesh()` → `UnityEngine.Mesh` (pivot di tengah bawah);
    - `CreateBlock(string name, Transform parent, Vector3 worldFrom, Vector3 worldTo, float height, float thickness, Material material, UnityEngine.Mesh unitBox)` → `GameObject`;
    - `SaveAsPrefab(GameObject go, string path)` → `GameObject` (menyimpan mesh non-asset ke `MeshesFolder`, lalu menghancurkan `go`).
  - `PlayerRigBuilder.CreateRig(PlayerTuning, FootstepBank, PanelSettings, AudioClip flashlightClick)` → `GameObject`.
  - `PlayerAssets { Tuning, Footsteps, Panel, Clips (Dictionary<string, AudioClip>), Materials (Dictionary<string, Material>), UnitBox, PlayerPrefab, GatePrefab, DoorPrefab, MotorPrefab }`.
  - `PlayerAssetsBuilder.BuildAll()` → `PlayerAssets` dan `PlayerAssetsBuilder.FillFootsteps(FootstepBank)`.

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/Player/Tests/PlayerAssetsTests.cs`:
```csharp
using System;
using System.Linq;
using NUnit.Framework;
using TycoWorld.Player.Authoring;
using TycoWorld.WorldGen;
using UnityEngine;

namespace TycoWorld.Player.Tests
{
    public class PlayerAssetsTests
    {
        PhysicsFixture fx;

        [SetUp]
        public void SetUp() => fx = new PhysicsFixture();

        [TearDown]
        public void TearDown() => fx.Dispose();

        GameObject Rig(out PlayerTuning tuning, out FootstepBank bank)
        {
            tuning = fx.Track(ScriptableObject.CreateInstance<PlayerTuning>());
            bank = fx.Track(ScriptableObject.CreateInstance<FootstepBank>());
            return fx.Track(PlayerRigBuilder.CreateRig(tuning, bank, null, null));
        }

        [Test]
        public void Rig_HasOneCameraAndListener_AtEyeHeight_OnPlayerLayer()
        {
            var rig = Rig(out var tuning, out var bank);
            Assert.AreEqual(1, rig.GetComponentsInChildren<Camera>(true).Length);
            Assert.AreEqual(1, rig.GetComponentsInChildren<AudioListener>(true).Length);
            Assert.AreEqual(PlayerLayers.Player, rig.layer);
            var cam = rig.GetComponentInChildren<Camera>(true);
            Assert.IsTrue(cam.CompareTag("MainCamera"));
            Assert.AreEqual(70f, cam.fieldOfView, 1e-3f);
            Assert.AreEqual(1.6f, cam.transform.parent.localPosition.y, 1e-4f);
            var cc = rig.GetComponent<CharacterController>();
            Assert.AreEqual(1.8f, cc.height, 1e-4f);
            Assert.AreEqual(0.9f, cc.center.y, 1e-4f);
            var player = rig.GetComponent<FirstPersonPlayer>();
            Assert.AreSame(tuning, player.Tuning);
            Assert.AreSame(cam.transform.parent, player.CameraPivot);
            Assert.AreSame(cam.transform, player.View);
            Assert.AreSame(bank, rig.GetComponent<FootstepAudio>().Bank);
            Assert.IsNotNull(cam.GetComponent<Interactor>());
            Assert.IsNotNull(rig.GetComponent<PlayerHud>());
            Assert.IsNotNull(rig.GetComponent<PlayerSpawner>());
            var light = rig.GetComponentInChildren<Light>(true);
            Assert.AreEqual(LightType.Spot, light.type);
            Assert.AreEqual(LightShadows.Soft, light.shadows);
            Assert.IsFalse(light.enabled);
        }

        [Test]
        public void Rig_InitializesModulesInOrder()
        {
            var rig = Rig(out _, out _);
            var player = rig.GetComponent<FirstPersonPlayer>();
            player.Initialize();
            CollectionAssert.AreEqual(new[] { 10, 20, 30, 40 }, player.Modules.Select(m => m.Order).ToArray());
        }

        [Test]
        public void Gate_HasHingedLeafTallerThanJumpReach()
        {
            var gate = fx.Track(PlaceholderPropFactory.CreateGate(null, null, null, null));
            var interactable = gate.GetComponent<GateInteractable>();
            Assert.IsNotNull(interactable);
            var hinge = gate.transform.Find("Hinge");
            Assert.IsNotNull(hinge);
            Assert.AreEqual(-PlaceholderPropFactory.GateWidth * 0.5f, hinge.localPosition.x, 1e-4f);
            var leaf = hinge.Find("Leaf").GetComponent<BoxCollider>();
            Assert.GreaterOrEqual(leaf.size.y, 1.7f);
            Assert.AreEqual(PlaceholderPropFactory.GateWidth, leaf.size.x, 1e-4f);
            Assert.Greater(leaf.center.x, 0f, "leaf extends along +x from the hinge");
            Assert.IsNotNull(gate.GetComponent<AudioSource>());
        }

        [Test]
        public void Door_And_Motor_AreInteractablesWithColliders()
        {
            var door = fx.Track(PlaceholderPropFactory.CreateDoor(null, null, null));
            Assert.IsNotNull(door.GetComponent<LockedDoorInteractable>());
            Assert.IsNotNull(door.GetComponentInChildren<BoxCollider>());
            var motor = fx.Track(PlaceholderPropFactory.CreateMotor(null, null, null));
            Assert.IsNotNull(motor.GetComponent<PlaceholderVehicleInteractable>());
            var box = motor.GetComponent<BoxCollider>();
            Assert.IsNotNull(box);
            Assert.Greater(box.size.z, box.size.x, "motor is longer than wide (local +z forward)");
        }

        [Test]
        public void Block_SpansFromTo_WithHeight()
        {
            var unit = fx.Track(PlaceholderPropFactory.UnitBoxMesh());
            var block = fx.Track(PlaceholderPropFactory.CreateBlock("Wall_Test", null, new Vector3(5000f, 0f, 5000f), new Vector3(5004f, 0f, 5000f), 1.8f, 0.15f, null, unit));
            Physics.SyncTransforms();
            var bounds = block.GetComponent<BoxCollider>().bounds;
            Assert.AreEqual(4f, bounds.size.x, 1e-3f);
            Assert.AreEqual(1.8f, bounds.size.y, 1e-3f);
            Assert.AreEqual(0f, bounds.min.y, 1e-3f);
            Assert.AreEqual(5002f, bounds.center.x, 1e-3f);
        }

        [Test]
        public void FillFootsteps_CoversEverySurfaceWithClips()
        {
            var bank = fx.Track(ScriptableObject.CreateInstance<FootstepBank>());
            PlayerAssetsBuilder.FillFootsteps(bank);
            foreach (SurfaceType surface in Enum.GetValues(typeof(SurfaceType)))
            {
                var entry = bank.surfaces.Single(s => s.surface == surface);
                Assert.Greater(entry.clips.Length, 0, surface.ToString());
                Assert.IsTrue(entry.clips.All(c => c != null), surface.ToString());
            }
        }
    }
}
```

- [ ] **Step 2: Jalankan tes, pastikan gagal**

Procedure R, lalu Procedure T dengan `Group = "PlayerAssetsTests"`.
Expected: COMPILE FAILED (`CS0103 PlayerRigBuilder`, `PlaceholderPropFactory`, `PlayerAssetsBuilder`).

- [ ] **Step 3: Implementasi**

`Assets/Player/Editor/PlaceholderPropFactory.cs`:
```csharp
using System.Collections.Generic;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TycoWorld.Player.Authoring
{
    /// <summary>Low-poly placeholder props for the kos front yard. Create* builds unsaved objects; SaveAsPrefab persists them.</summary>
    public static class PlaceholderPropFactory
    {
        public const float GateWidth = 2f;
        public const float GateHeight = 1.8f;
        public const float GateBottomGap = 0.14f;
        public const float WallHeight = 1.92f;
        public const float WallThickness = 0.15f;
        public const float YardThickness = 0.12f;

        public static GameObject CreateGate(Material metal, AudioClip unlatch, AudioClip creak, AudioClip latch)
        {
            var root = new GameObject("Gate");
            var posts = new MeshData(1);
            float postX = GateWidth * 0.5f + 0.06f;
            PlaceholderMeshes.AddBox(posts, 0, new Vector3(-postX, 1f, 0f), new Vector3(0.1f, 2f, 0.1f));
            PlaceholderMeshes.AddBox(posts, 0, new Vector3(postX, 1f, 0f), new Vector3(0.1f, 2f, 0.1f));
            AddMesh(root, "Gate_Posts", posts, metal);
            AddBoxCollider(root, new Vector3(-postX, 1f, 0f), new Vector3(0.1f, 2f, 0.1f));
            AddBoxCollider(root, new Vector3(postX, 1f, 0f), new Vector3(0.1f, 2f, 0.1f));

            var hinge = new GameObject("Hinge").transform;
            hinge.SetParent(root.transform, false);
            hinge.localPosition = new Vector3(-GateWidth * 0.5f, 0f, 0f);
            var leafGo = new GameObject("Leaf");
            leafGo.transform.SetParent(hinge, false);
            var leaf = new MeshData(1);
            float bottom = GateBottomGap, top = GateBottomGap + GateHeight;
            PlaceholderMeshes.AddBox(leaf, 0, new Vector3(GateWidth * 0.5f, bottom + 0.03f, 0f), new Vector3(GateWidth, 0.06f, 0.04f));
            PlaceholderMeshes.AddBox(leaf, 0, new Vector3(GateWidth * 0.5f, top - 0.03f, 0f), new Vector3(GateWidth, 0.06f, 0.04f));
            for (float x = 0.075f; x < GateWidth; x += 0.15f)
                PlaceholderMeshes.AddBox(leaf, 0, new Vector3(x, (bottom + top) * 0.5f, 0f), new Vector3(0.025f, GateHeight, 0.025f));
            AddMesh(leafGo, "Gate_Leaf", leaf, metal);
            AddBoxCollider(leafGo, new Vector3(GateWidth * 0.5f, (bottom + top) * 0.5f, 0f), new Vector3(GateWidth, GateHeight, 0.06f));

            var source = Add3DSource(root);
            var gate = root.AddComponent<GateInteractable>();
            gate.Configure(hinge, unlatch, creak, latch);
            gate.SetAudioSource(source);
            gate.SetInteractionId("kos-gate");
            return root;
        }

        public static GameObject CreateDoor(Material wood, Material metal, AudioClip rattle)
        {
            var root = new GameObject("KosDoor");
            var slab = new MeshData(2);
            PlaceholderMeshes.AddBox(slab, 0, new Vector3(0f, 1.05f, 0.03f), new Vector3(0.9f, 2.1f, 0.06f));
            PlaceholderMeshes.AddBox(slab, 1, new Vector3(0.33f, 1f, 0.08f), new Vector3(0.12f, 0.03f, 0.04f));
            AddMesh(root, "KosDoor_Slab", slab, wood, metal);
            AddBoxCollider(root, new Vector3(0f, 1.05f, 0.03f), new Vector3(0.9f, 2.1f, 0.06f));
            var door = root.AddComponent<LockedDoorInteractable>();
            door.Configure(rattle);
            door.SetAudioSource(Add3DSource(root));
            door.SetInteractionId("kos-door");
            return root;
        }

        public static GameObject CreateMotor(Material body, Material dark, AudioClip keys)
        {
            var root = new GameObject("Motor_Placeholder");
            var m = new MeshData(2);
            PlaceholderMeshes.AddBox(m, 0, new Vector3(0f, 0.6f, 0f), new Vector3(0.32f, 0.3f, 1.1f));
            PlaceholderMeshes.AddBox(m, 0, new Vector3(0f, 0.85f, 0.5f), new Vector3(0.4f, 0.45f, 0.25f));
            PlaceholderMeshes.AddBox(m, 1, new Vector3(0f, 0.86f, -0.2f), new Vector3(0.3f, 0.1f, 0.7f));
            PlaceholderMeshes.AddBox(m, 1, new Vector3(0f, 1.08f, 0.62f), new Vector3(0.7f, 0.04f, 0.04f));
            AddWheel(m, 1, new Vector3(0f, 0.3f, 0.65f), 0.3f, 0.1f, 10);
            AddWheel(m, 1, new Vector3(0f, 0.3f, -0.65f), 0.3f, 0.1f, 10);
            AddMesh(root, "Motor_Body", m, body, dark);
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.55f, 0f);
            box.size = new Vector3(0.7f, 1.1f, 1.9f);
            var vehicle = root.AddComponent<PlaceholderVehicleInteractable>();
            vehicle.Configure(keys);
            vehicle.SetAudioSource(Add3DSource(root));
            vehicle.SetInteractionId("kos-motor");
            return root;
        }

        /// <summary>Unit cube with its pivot at the bottom centre (scale = width, height, length).</summary>
        public static UnityEngine.Mesh UnitBoxMesh()
        {
            var data = new MeshData(1);
            PlaceholderMeshes.AddBox(data, 0, new Vector3(0f, 0.5f, 0f), Vector3.one);
            return data.ToMesh("UnitBox", out _);
        }

        /// <summary>Box from worldFrom to worldTo (y of worldFrom is its base), as a scaled unit box with a BoxCollider.</summary>
        public static GameObject CreateBlock(string name, Transform parent, Vector3 worldFrom, Vector3 worldTo, float height, float thickness,
            Material material, UnityEngine.Mesh unitBox)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, true);
            var span = worldTo - worldFrom;
            span.y = 0f;
            float length = span.magnitude;
            var rotation = length > 1e-4f ? Quaternion.LookRotation(span / length, Vector3.up) : Quaternion.identity;
            go.transform.SetPositionAndRotation((worldFrom + worldTo) * 0.5f, rotation);
            go.transform.position = new Vector3(go.transform.position.x, worldFrom.y, go.transform.position.z);
            go.transform.localScale = new Vector3(thickness, height, Mathf.Max(length, 0.01f));
            go.AddComponent<MeshFilter>().sharedMesh = unitBox;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.5f, 0f);
            box.size = Vector3.one;
            return go;
        }

        /// <summary>Saves non-asset meshes under Prefabs/Meshes, writes the prefab, destroys the scene object.</summary>
        public static GameObject SaveAsPrefab(GameObject go, string path)
        {
            AssetFolders.Ensure(PlayerPaths.MeshesFolder);
            foreach (var filter in go.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null || AssetDatabase.Contains(mesh)) continue;
                string meshPath = PlayerPaths.MeshesFolder + "/" + mesh.name + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(meshPath);
                if (existing == null) AssetDatabase.CreateAsset(mesh, meshPath);
                else
                {
                    EditorUtility.CopySerialized(mesh, existing);
                    filter.sharedMesh = existing;
                    Object.DestroyImmediate(mesh);
                }
            }
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static void AddMesh(GameObject go, string meshName, MeshData data, params Material[] materials)
        {
            go.AddComponent<MeshFilter>().sharedMesh = data.ToMesh(meshName, out _);
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
        }

        static void AddBoxCollider(GameObject go, Vector3 center, Vector3 size)
        {
            var box = go.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
        }

        static AudioSource Add3DSource(GameObject go)
        {
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.minDistance = 1f;
            source.maxDistance = 25f;
            source.rolloffMode = AudioRolloffMode.Linear;
            return source;
        }

        /// <summary>Cylinder whose axis is local X (a wheel seen from the side).</summary>
        static void AddWheel(MeshData m, int sub, Vector3 center, float radius, float width, int sides)
        {
            var half = Vector3.right * (width * 0.5f);
            for (int i = 0; i < sides; i++)
            {
                float a0 = 2f * Mathf.PI * i / sides, a1 = 2f * Mathf.PI * (i + 1) / sides;
                var d0 = new Vector3(0f, Mathf.Sin(a0), Mathf.Cos(a0)) * radius;
                var d1 = new Vector3(0f, Mathf.Sin(a1), Mathf.Cos(a1)) * radius;
                var outward = ((d0 + d1) * 0.5f).normalized;
                m.AddQuad(sub, center - half + d0, center - half + d1, center + half + d1, center + half + d0,
                    Vector2.zero, Vector2.right, Vector2.one, Vector2.up, outward);
                m.AddTriangle(sub, center + half, center + half + d0, center + half + d1, Vector2.zero, Vector2.right, Vector2.up, Vector3.right);
                m.AddTriangle(sub, center - half, center - half + d0, center - half + d1, Vector2.zero, Vector2.right, Vector2.up, Vector3.left);
            }
        }
    }
}
```

`Assets/Player/Editor/PlayerRigBuilder.cs`:
```csharp
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace TycoWorld.Player.Authoring
{
    /// <summary>Builds the unsaved Player hierarchy (spec #2 §11).</summary>
    public static class PlayerRigBuilder
    {
        public static GameObject CreateRig(PlayerTuning tuning, FootstepBank bank, PanelSettings panel, AudioClip flashlightClick)
        {
            var root = new GameObject("Player") { layer = PlayerLayers.Player };
            var cc = root.AddComponent<CharacterController>();
            cc.height = tuning.body.height;
            cc.radius = tuning.body.radius;
            cc.center = new Vector3(0f, tuning.body.height * 0.5f, 0f);
            cc.stepOffset = tuning.body.stepOffset;
            cc.skinWidth = tuning.body.skinWidth;
            cc.slopeLimit = tuning.body.slopeLimit;
            cc.minMoveDistance = 0f;
            var stepSource = Add2DSource(root);

            var pivot = new GameObject("CameraPivot") { layer = PlayerLayers.Player };
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = new Vector3(0f, tuning.crouch.standingEyeHeight, 0f);

            var camGo = new GameObject("Camera") { layer = PlayerLayers.Player, tag = "MainCamera" };
            camGo.transform.SetParent(pivot.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = tuning.look.fieldOfView;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 400f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x0E, 0x12, 0x18, 0xFF);
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<Interactor>();

            var flashGo = new GameObject("Flashlight") { layer = PlayerLayers.Player };
            flashGo.transform.SetParent(camGo.transform, false);
            flashGo.transform.localPosition = new Vector3(0.18f, -0.15f, 0.1f);
            var light = flashGo.AddComponent<Light>();
            light.type = LightType.Spot;
            light.spotAngle = tuning.flashlight.outerAngle;
            light.innerSpotAngle = tuning.flashlight.innerAngle;
            light.range = tuning.flashlight.range;
            light.intensity = tuning.flashlight.intensity;
            light.color = tuning.flashlight.color;
            light.shadows = LightShadows.Soft;
            light.enabled = tuning.flashlight.startOn;
            flashGo.AddComponent<Flashlight>().Configure(Add2DSource(flashGo), flashlightClick);

            var player = root.AddComponent<FirstPersonPlayer>();
            player.Configure(tuning, pivot.transform, camGo.transform);
            root.AddComponent<FootstepAudio>().Configure(bank, stepSource);
            root.AddComponent<UIDocument>().panelSettings = panel;
            root.AddComponent<PlayerHud>();
            root.AddComponent<PlayerSpawner>().Configure(player, null);
            return root;
        }

        static AudioSource Add2DSource(GameObject go)
        {
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }
    }
}
```

`Assets/Player/Editor/PlayerAssetsBuilder.cs`:
```csharp
using System.Collections.Generic;
using System.IO;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace TycoWorld.Player.Authoring
{
    public sealed class PlayerAssets
    {
        public PlayerTuning Tuning;
        public FootstepBank Footsteps;
        public PanelSettings Panel;
        public readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
        public readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        public UnityEngine.Mesh UnitBox;
        public GameObject PlayerPrefab, GatePrefab, DoorPrefab, MotorPrefab;
    }

    /// <summary>Creates or updates every player asset in place (re-running is safe).</summary>
    public static class PlayerAssetsBuilder
    {
        const string Theme = "@import url(\"unity-theme://default\");\n";

        [MenuItem("Tyco World/Player/Build Player Assets")]
        static void Menu() => BuildAll();

        public static PlayerAssets BuildAll()
        {
            // Create* builds temporary objects in the active scene; re-save scenes that were clean so Procedure T is not blocked.
            var cleanScenes = new List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (!s.isDirty && s.path.Length > 0) cleanScenes.Add(s);
            }
            foreach (var folder in new[] { PlayerPaths.ConfigFolder, PlayerPaths.UiFolder, PlayerPaths.MaterialsFolder, PlayerPaths.MeshesFolder })
                AssetFolders.Ensure(folder);
            var a = new PlayerAssets
            {
                Tuning = LoadOrCreate<PlayerTuning>(PlayerPaths.TuningAsset),
                Footsteps = LoadOrCreate<FootstepBank>(PlayerPaths.FootstepBankAsset)
            };
            FillFootsteps(a.Footsteps);
            EditorUtility.SetDirty(a.Footsteps);
            a.Panel = BuildPanel();
            foreach (var file in PlayerAudioMap.InteractionFiles)
                a.Clips[file] = AssetDatabase.LoadAssetAtPath<AudioClip>(PlayerAudioMap.InteractionPath(file));

            a.Materials["M_Gate_Metal"] = Mat("M_Gate_Metal", "2B2F33", 0.45f);
            a.Materials["M_Door_Wood"] = Mat("M_Door_Wood", "5A3B22", 0.2f);
            a.Materials["M_Wall_Plaster"] = Mat("M_Wall_Plaster", "B8B2A6", 0.05f);
            a.Materials["M_Motor_Body"] = Mat("M_Motor_Body", "7A1E1E", 0.5f);
            a.Materials["M_Motor_Dark"] = Mat("M_Motor_Dark", "1C1C1C", 0.3f);
            a.Materials["Yard"] = AssetDatabase.LoadAssetAtPath<Material>(WorldGenPaths.MaterialsFolder + "/Area_Hardground.mat");

            a.UnitBox = SaveMesh(PlaceholderPropFactory.UnitBoxMesh());
            a.GatePrefab = PlaceholderPropFactory.SaveAsPrefab(PlaceholderPropFactory.CreateGate(a.Materials["M_Gate_Metal"],
                a.Clips[PlayerAudioMap.GateUnlatch], a.Clips[PlayerAudioMap.GateCreak], a.Clips[PlayerAudioMap.GateLatch]), PlayerPaths.GatePrefab);
            a.DoorPrefab = PlaceholderPropFactory.SaveAsPrefab(PlaceholderPropFactory.CreateDoor(a.Materials["M_Door_Wood"], a.Materials["M_Gate_Metal"],
                a.Clips[PlayerAudioMap.DoorRattle]), PlayerPaths.DoorPrefab);
            a.MotorPrefab = PlaceholderPropFactory.SaveAsPrefab(PlaceholderPropFactory.CreateMotor(a.Materials["M_Motor_Body"], a.Materials["M_Motor_Dark"],
                a.Clips[PlayerAudioMap.VehicleKeys]), PlayerPaths.MotorPrefab);
            a.PlayerPrefab = PlaceholderPropFactory.SaveAsPrefab(PlayerRigBuilder.CreateRig(a.Tuning, a.Footsteps, a.Panel,
                a.Clips[PlayerAudioMap.FlashlightClick]), PlayerPaths.PlayerPrefab);
            AssetDatabase.SaveAssets();
            foreach (var s in cleanScenes)
                if (s.IsValid() && s.isDirty) EditorSceneManager.SaveScene(s);
            return a;
        }

        public static void FillFootsteps(FootstepBank bank)
        {
            bank.surfaces = new List<SurfaceClips>();
            foreach (var set in PlayerAudioMap.Footsteps)
            {
                var clips = new AudioClip[set.Files.Length];
                for (int i = 0; i < clips.Length; i++) clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(PlayerAudioMap.FootstepPath(set.Files[i]));
                bank.surfaces.Add(new SurfaceClips { surface = set.Surface, clips = clips, volume = set.Volume, pitchMin = set.PitchMin, pitchMax = set.PitchMax });
            }
        }

        static PanelSettings BuildPanel()
        {
            string abs = WorldGenPaths.ToAbsolute(PlayerPaths.ThemeAsset);
            if (!File.Exists(abs) || File.ReadAllText(abs) != Theme)
            {
                File.WriteAllText(abs, Theme);
                AssetDatabase.ImportAsset(PlayerPaths.ThemeAsset, ImportAssetOptions.ForceSynchronousImport);
            }
            var panel = LoadOrCreate<PanelSettings>(PlayerPaths.PanelSettingsAsset);
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(PlayerPaths.ThemeAsset);
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            EditorUtility.SetDirty(panel);
            return panel;
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        static UnityEngine.Mesh SaveMesh(UnityEngine.Mesh mesh)
        {
            string path = PlayerPaths.MeshesFolder + "/" + mesh.name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        static Material Mat(string name, string hex, float smoothness)
        {
            string path = PlayerPaths.MaterialsFolder + "/" + name + ".mat";
            var shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
```

- [ ] **Step 4: Jalankan tes, pastikan lulus; lalu bangun aset sungguhan**

Procedure R, lalu Procedure T dengan `Group = "PlayerAssetsTests"`.
Expected: `passed=6 failed=0`.

Lalu snippet MCP:
```csharp
using TycoWorld.Player.Authoring;
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var a = PlayerAssetsBuilder.BuildAll();
        result.Log("player={0} gate={1} door={2} motor={3} panel={4} theme={5} bank={6}",
            AssetDatabase.GetAssetPath(a.PlayerPrefab), AssetDatabase.GetAssetPath(a.GatePrefab), AssetDatabase.GetAssetPath(a.DoorPrefab),
            AssetDatabase.GetAssetPath(a.MotorPrefab), AssetDatabase.GetAssetPath(a.Panel),
            a.Panel.themeStyleSheet == null ? "MISSING" : "ok", a.Footsteps.surfaces.Count);
    }
}
```
Expected:
- path keempat prefab ada di `Assets/Player/Prefabs`;
- `theme=ok`, `bank=7`.

Jalankan snippet itu dua kali. Pada run kedua, `git status --short Assets/Player` tidak boleh menunjukkan file mesh baru dengan nama berbeda (builder memperbarui di tempat). Lalu `mcp__unity-mcp__Unity_GetConsoleLogs` (`logTypes: "error,warning"`): tidak ada error baru.

Lalu `Group = "."`. Expected: `failed=0`.

- [ ] **Step 5: Commit**

Procedure C:
- path: `Assets/Player`
- subject: `feat(player): asset builder, placeholder kos props and player prefab`

---

### Task 18: Menu Player Setup dan penempatan di `World.unity`

**Files:**
- Create: `Assets/Player/Editor/KosFrontyardBuilder.cs`, `Assets/Player/Editor/PlayerSceneSetup.cs`
- Create: `Assets/Player/Tests/McpRender.cs` (helper dev untuk render ke PNG/luminansi)
- Modify: `Assets/Scenes/World.unity` (lewat menu, bukan diedit tangan)
- Test: `Assets/Player/Tests/KosFrontyardBuilderTests.cs`

**Interfaces:**
- Consumes:
  - `KosSitePlan`, `KosSitePlanner`, `KosSiteSource` (Task 16);
  - `PlayerAssets`, `PlaceholderPropFactory`, `PlayerAssetsBuilder` (Task 17);
  - `WorldScenes.CheckSaved/EnsureWorldSceneOpen/OpenGeneratedSceneAdditive`, `WorldMeshPipeline.Classify`, `DefaultSettingsFactory.LoadOrCreateAsset`, `Xz.At` (WorldGen editor).
- Produces:
  - `KosFrontyardBuilder.Build(KosSitePlan plan, GameObject gate, GameObject door, GameObject motor, Material yard, Material wall, UnityEngine.Mesh unitBox)` → `GameObject` "KosFrontyard", berisi:
    - `Yard` dengan `GroundSurface` Concrete;
    - `Wall_Left`, `Wall_Right`, `Wall_FrontLeft`, `Wall_FrontRight`;
    - instance gate/door/motor;
    - `PlayerSpawn_Kos` (`PlayerSpawnPoint` id "kos").
  - `PlayerSceneSetup` dengan:
    - konstanta `KosOsmId = 409467778`, `FrontyardName = "KosFrontyard"`, `PlayerName = "Player"`;
    - `Run(bool interactive, bool replaceFrontyard, Vector2 preferredOutward, out string error)` → bool;
    - menu "Tyco World/Player Setup" dan "Tyco World/Re-place Kos Frontyard".
  - `McpRender.Render(Camera, int w, int h)` → `Texture2D`, `SavePng(Camera, string path, int w, int h)`, `CenterLuminance(Camera, int size = 256)` → float, `KosShots(string dir)` → string.

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/Player/Tests/KosFrontyardBuilderTests.cs`:
```csharp
using System.Collections.Generic;
using NUnit.Framework;
using TycoWorld.Player.Authoring;
using TycoWorld.WorldGen;
using UnityEngine;

namespace TycoWorld.Player.Tests
{
    public class KosFrontyardBuilderTests
    {
        static readonly Vector2 Offset = new Vector2(5000f, 5000f);
        PhysicsFixture fx;

        [SetUp]
        public void SetUp() => fx = new PhysicsFixture();

        [TearDown]
        public void TearDown() => fx.Dispose();

        GameObject Build(out KosSitePlan plan)
        {
            var kos = new List<Vector2> { Offset, Offset + new Vector2(10f, 0f), Offset + new Vector2(10f, 12f), Offset + new Vector2(0f, 12f) };
            var road = new RoadLine { Id = 1, HalfWidth = 2.5f, Points = new List<Vector2> { Offset + new Vector2(3f, -9f), Offset + new Vector2(3f, -30f) } };
            Assert.IsTrue(KosSitePlanner.TryPlan(kos, new[] { road }, new List<IReadOnlyList<Vector2>>(), new KosSiteOptions(), out plan, out _));
            var gate = fx.Track(PlaceholderPropFactory.CreateGate(null, null, null, null));
            var door = fx.Track(PlaceholderPropFactory.CreateDoor(null, null, null));
            var motor = fx.Track(PlaceholderPropFactory.CreateMotor(null, null, null));
            var unit = fx.Track(PlaceholderPropFactory.UnitBoxMesh());
            var root = fx.Track(KosFrontyardBuilder.Build(plan, gate, door, motor, null, null, unit));
            Physics.SyncTransforms();
            return root;
        }

        static void Near(Vector3 expected, Vector3 actual, float tolerance = 1e-3f)
        {
            Assert.AreEqual(expected.x, actual.x, tolerance, "x");
            Assert.AreEqual(expected.y, actual.y, tolerance, "y");
            Assert.AreEqual(expected.z, actual.z, tolerance, "z");
        }

        [Test]
        public void Build_PlacesPartsFromPlan()
        {
            var root = Build(out _);
            Assert.AreEqual("KosFrontyard", root.name);
            float slab = PlaceholderPropFactory.YardThickness;
            var spawn = root.GetComponentInChildren<PlayerSpawnPoint>();
            Assert.AreEqual("kos", spawn.Id);
            Assert.AreEqual("Kos Raflesia 3", spawn.ArrivalMessage);
            Near(new Vector3(5003f, slab, 4998.8f), spawn.Feet);
            Assert.AreEqual(180f, spawn.Yaw, 1e-2f);
            Near(new Vector3(5003f, 0f, 4994.5f), root.GetComponentInChildren<GateInteractable>().transform.position);
            Near(new Vector3(5003f, slab, 5000f), root.GetComponentInChildren<LockedDoorInteractable>().transform.position);
            var motor = root.GetComponentInChildren<PlaceholderVehicleInteractable>().transform;
            Near(new Vector3(5007f, slab, 4997.525f), motor.position);
            Assert.AreEqual(90f, motor.eulerAngles.y, 1e-2f);
        }

        [Test]
        public void Yard_IsConcreteSlabCoveringTheYard()
        {
            var root = Build(out _);
            var yard = root.transform.Find("Yard");
            Assert.AreEqual(SurfaceType.Concrete, yard.GetComponent<GroundSurface>().Type);
            var b = yard.GetComponent<BoxCollider>().bounds;
            Assert.AreEqual(PlaceholderPropFactory.YardThickness, b.max.y, 1e-3f);
            Assert.AreEqual(5000f, b.min.x, 1e-2f);
            Assert.AreEqual(5010f, b.max.x, 1e-2f);
            Assert.AreEqual(4994.5f, b.min.z, 1e-2f);
            Assert.AreEqual(5000f, b.max.z, 1e-2f);
        }

        [Test]
        public void Walls_AreTallerThanJumpReach_AndLeaveTheGateOpen()
        {
            var root = Build(out _);
            int walls = 0;
            foreach (Transform child in root.transform)
            {
                if (!child.name.StartsWith("Wall_")) continue;
                walls++;
                var b = child.GetComponent<BoxCollider>().bounds;
                Assert.GreaterOrEqual(b.size.y, 1.8f, child.name);
                bool front = child.name.StartsWith("Wall_Front");
                if (front) Assert.IsTrue(b.max.x <= 5002f + 1e-3f || b.min.x >= 5004f - 1e-3f, child.name + " blocks the gate");
            }
            Assert.AreEqual(4, walls);
        }
    }
}
```

- [ ] **Step 2: Jalankan tes, pastikan gagal**

Procedure R, lalu Procedure T dengan `Group = "KosFrontyardBuilderTests"`.
Expected: COMPILE FAILED (`CS0103 KosFrontyardBuilder`).

- [ ] **Step 3: Implementasi**

`Assets/Player/Editor/KosFrontyardBuilder.cs`:
```csharp
using TycoWorld.WorldGen;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.Player.Authoring
{
    /// <summary>Turns a KosSitePlan into the front-yard hierarchy in the active scene (spec #2 §9).</summary>
    public static class KosFrontyardBuilder
    {
        public static GameObject Build(KosSitePlan plan, GameObject gate, GameObject door, GameObject motor, Material yard, Material wall, UnityEngine.Mesh unitBox)
        {
            float slab = PlaceholderPropFactory.YardThickness;
            var outward = Xz.At(plan.Outward, 0f);
            var along = Xz.At(plan.Along, 0f);
            var root = new GameObject("KosFrontyard");
            root.transform.SetPositionAndRotation(Xz.At(plan.Door, 0f), Quaternion.Euler(0f, plan.SpawnYaw, 0f));

            Vector3 a = Xz.At(plan.WallA, 0f), b = Xz.At(plan.WallB, 0f);
            Vector3 aOut = a + outward * plan.YardDepth, bOut = b + outward * plan.YardDepth;

            var yardGo = PlaceholderPropFactory.CreateBlock("Yard", root.transform, (a + b) * 0.5f, (aOut + bOut) * 0.5f, slab, Vector3.Distance(a, b), yard, unitBox);
            yardGo.AddComponent<GroundSurface>().Initialize(SurfaceType.Concrete);

            float h = PlaceholderPropFactory.WallHeight, t = PlaceholderPropFactory.WallThickness;
            var gateCenter = Xz.At(plan.Gate, 0f);
            float openingHalf = PlaceholderPropFactory.GateWidth * 0.5f + 0.12f;
            bool aIsLeft = Vector3.Dot(a - gateCenter, along) < 0f;
            Vector3 leftOut = aIsLeft ? aOut : bOut, rightOut = aIsLeft ? bOut : aOut;
            Vector3 leftIn = aIsLeft ? a : b, rightIn = aIsLeft ? b : a;
            Vector3 leftDir = (leftOut - gateCenter).normalized;
            Vector3 rightDir = (rightOut - gateCenter).normalized;
            PlaceholderPropFactory.CreateBlock("Wall_Left", root.transform, leftIn, leftOut, h, t, wall, unitBox);
            PlaceholderPropFactory.CreateBlock("Wall_Right", root.transform, rightIn, rightOut, h, t, wall, unitBox);
            PlaceholderPropFactory.CreateBlock("Wall_FrontLeft", root.transform, leftOut, gateCenter + leftDir * openingHalf, h, t, wall, unitBox);
            PlaceholderPropFactory.CreateBlock("Wall_FrontRight", root.transform, gateCenter + rightDir * openingHalf, rightOut, h, t, wall, unitBox);

            Place(gate, root.transform, gateCenter, plan.SpawnYaw);
            Place(door, root.transform, Xz.At(plan.Door, slab), plan.SpawnYaw);
            Place(motor, root.transform, Xz.At(plan.Motor, slab), plan.MotorYaw);

            var spawn = new GameObject("PlayerSpawn_Kos");
            spawn.transform.SetParent(root.transform, true);
            spawn.transform.SetPositionAndRotation(Xz.At(plan.Spawn, slab), Quaternion.Euler(0f, plan.SpawnYaw, 0f));
            spawn.AddComponent<PlayerSpawnPoint>().Configure("kos", "Kos Raflesia 3");
            return root;
        }

        static void Place(GameObject source, Transform parent, Vector3 position, float yaw)
        {
            if (source == null) return;
            var go = PrefabUtility.IsPartOfPrefabAsset(source) ? (GameObject)PrefabUtility.InstantiatePrefab(source) : Object.Instantiate(source);
            go.name = source.name;
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        }
    }
}
```

`Assets/Player/Editor/PlayerSceneSetup.cs`:
```csharp
using System.IO;
using System.Text;
using TycoWorld.WorldGen;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TycoWorld.Player.Authoring
{
    /// <summary>Puts the player and the kos front yard into World.unity (manual content; idempotent).</summary>
    public static class PlayerSceneSetup
    {
        public const long KosOsmId = 409467778;
        public const string FrontyardName = "KosFrontyard";
        public const string PlayerName = "Player";

        [MenuItem("Tyco World/Player Setup")]
        static void SetupMenu()
        {
            if (!Run(true, false, Vector2.zero, out string error)) EditorUtility.DisplayDialog("Player Setup gagal", error, "OK");
        }

        [MenuItem("Tyco World/Re-place Kos Frontyard")]
        static void ReplaceMenu()
        {
            if (!Run(true, true, Vector2.zero, out string error)) EditorUtility.DisplayDialog("Re-place gagal", error, "OK");
        }

        public static bool Run(bool interactive, bool replaceFrontyard, Vector2 preferredOutward, out string error)
        {
            if (!WorldScenes.CheckSaved(interactive, out error)) return false;
            var world = WorldScenes.EnsureWorldSceneOpen();
            var assets = PlayerAssetsBuilder.BuildAll();

            var frontyard = FindRoot(world, FrontyardName);
            if (frontyard == null || replaceFrontyard)
            {
                if (!TryPlanKos(preferredOutward, out var plan, out error)) return false;
                if (frontyard != null) Object.DestroyImmediate(frontyard);
                SceneManager.SetActiveScene(world);
                frontyard = KosFrontyardBuilder.Build(plan, assets.GatePrefab, assets.DoorPrefab, assets.MotorPrefab,
                    assets.Materials["Yard"], assets.Materials["M_Wall_Plaster"], assets.UnitBox);
                Debug.Log("[Player] Halaman kos: " + KosSitePlanner.Describe(plan));
            }
            var spawn = frontyard.GetComponentInChildren<PlayerSpawnPoint>(true);

            var playerGo = FindRoot(world, PlayerName);
            if (playerGo == null)
            {
                playerGo = (GameObject)PrefabUtility.InstantiatePrefab(assets.PlayerPrefab, world);
                playerGo.name = PlayerName;
            }
            playerGo.transform.SetPositionAndRotation(spawn.Feet, Quaternion.Euler(0f, spawn.Yaw, 0f));
            var spawner = playerGo.GetComponent<PlayerSpawner>();
            spawner.Configure(playerGo.GetComponent<FirstPersonPlayer>(), spawn);
            PrefabUtility.RecordPrefabInstancePropertyModifications(spawner);

            var preview = FindRoot(world, "PreviewCam");
            if (preview != null) preview.SetActive(false);

            EditorSceneManager.MarkSceneDirty(world);
            EditorSceneManager.SaveScene(world);
            WorldScenes.OpenGeneratedSceneAdditive();
            SceneManager.SetActiveScene(world);
            WarnOverlaps(frontyard);
            return true;
        }

        static bool TryPlanKos(Vector2 preferredOutward, out KosSitePlan plan, out string error)
        {
            plan = default;
            var settings = DefaultSettingsFactory.LoadOrCreateAsset();
            string raw = WorldGenPaths.ToAbsolute(settings.rawDataPath);
            if (!File.Exists(raw)) { error = "Data OSM belum ada (" + settings.rawDataPath + ")."; return false; }
            var classified = WorldMeshPipeline.Classify(File.ReadAllText(raw), settings, new GenerationReport());
            if (!KosSiteSource.TryBuild(classified, KosOsmId, 60f, out var footprint, out var roads, out var neighbours))
            {
                error = "Kos Raflesia 3 (way 409467778) tidak ada di data OSM.";
                return false;
            }
            if (!KosSitePlanner.TryPlan(footprint, roads, neighbours, new KosSiteOptions { preferredOutward = preferredOutward }, out plan, out string reason))
            {
                error = "Tidak bisa menata halaman kos: " + reason;
                return false;
            }
            error = null;
            return true;
        }

        static GameObject FindRoot(Scene scene, string name)
        {
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == name) return go;
            return null;
        }

        /// <summary>Warns about generated props or buildings that intrude into the front yard (they are not moved).</summary>
        static void WarnOverlaps(GameObject frontyard)
        {
            Physics.SyncTransforms();
            var yard = frontyard.transform.Find("Yard").GetComponent<BoxCollider>();
            var b = yard.bounds;
            var hits = Physics.OverlapBox(b.center + Vector3.up * 1f, new Vector3(b.extents.x, 0.9f, b.extents.z) * 0.95f,
                Quaternion.identity, PlayerLayers.QueryMask, QueryTriggerInteraction.Ignore);
            var sb = new StringBuilder();
            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(frontyard.transform)) continue;
                if (hit.GetComponentInParent<GroundSurface>() != null) continue;
                var marker = hit.GetComponentInParent<KeyBuildingMarker>();
                if (marker != null && marker.OsmId == KosOsmId) continue;
                sb.Append(hit.transform.root.name).Append('/').Append(hit.name).Append("; ");
            }
            if (sb.Length > 0) Debug.LogWarning("[Player] Objek generate menembus halaman kos: " + sb);
        }
    }
}
```

`Assets/Player/Tests/McpRender.cs`:
```csharp
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace TycoWorld.Player.Tests
{
    /// <summary>Dev helper for MCP verification: renders a camera off-screen (works with an unfocused editor).</summary>
    public static class McpRender
    {
        public static Texture2D Render(Camera cam, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            var request = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
            else
            {
                var previous = cam.targetTexture;
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = previous;
            }
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(rt);
            return tex;
        }

        public static string SavePng(Camera cam, string path, int w, int h)
        {
            var tex = Render(cam, w, h);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return path;
        }

        public static float CenterLuminance(Camera cam, int size = 256)
        {
            var tex = Render(cam, size, size);
            float sum = 0f;
            int n = 0;
            for (int y = size / 4; y < size * 3 / 4; y++)
            for (int x = size / 4; x < size * 3 / 4; x++)
            {
                var c = tex.GetPixel(x, y);
                sum += 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                n++;
            }
            Object.DestroyImmediate(tex);
            return sum / n;
        }

        /// <summary>Lit front view (from beyond the gate) and a north-up top view of the kos front yard, with fog off.</summary>
        public static string KosShots(string dir)
        {
            var spawn = Object.FindAnyObjectByType<PlayerSpawnPoint>();
            var gate = Object.FindAnyObjectByType<GateInteractable>();
            if (spawn == null || gate == null) return "NO FRONTYARD";
            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;
            var lightGo = new GameObject("McpTempLight") { hideFlags = HideFlags.HideAndDontSave };
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightGo.transform.rotation = Quaternion.Euler(50f, 30f, 0f);
            var camGo = new GameObject("McpTempCam") { hideFlags = HideFlags.HideAndDontSave };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.45f, 0.5f, 0.55f);
            cam.farClipPlane = 300f;
            try
            {
                var forward = spawn.transform.forward;
                camGo.transform.position = gate.transform.position + forward * 6f + Vector3.up * 2.2f;
                camGo.transform.LookAt(spawn.Feet + Vector3.up * 1.4f - forward * 1.2f);
                SavePng(cam, dir + "/kos_front.png", 1280, 720);
                cam.orthographic = true;
                cam.orthographicSize = 18f;
                camGo.transform.SetPositionAndRotation(spawn.Feet + Vector3.up * 80f, Quaternion.Euler(90f, 0f, 0f));
                SavePng(cam, dir + "/kos_top.png", 1024, 1024);
            }
            finally
            {
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(lightGo);
                RenderSettings.fog = fog;
            }
            return dir + "/kos_front.png, " + dir + "/kos_top.png";
        }
    }
}
```

- [ ] **Step 4: Jalankan tes, pastikan lulus**

Procedure R, lalu Procedure T dengan `Group = "KosFrontyardBuilderTests"`.
Expected: `passed=3 failed=0`.

Lalu `Group = "."`. Expected: `failed=0`.

- [ ] **Step 5: Jalankan setup di proyek dan verifikasi (MCP)**

Simpan dulu semua scene (`EditorSceneManager.SaveOpenScenes()`), lalu jalankan:
```csharp
using TycoWorld.Player;
using TycoWorld.Player.Authoring;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEngine;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (!PlayerSceneSetup.Run(false, false, Vector2.zero, out string error)) { result.LogError(error); return; }
        int mainCams = 0, listeners = 0;
        foreach (var cam in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            if (cam.isActiveAndEnabled && cam.CompareTag("MainCamera")) mainCams++;
        foreach (var l in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            if (l.isActiveAndEnabled) listeners++;
        var spawner = UnityEngine.Object.FindAnyObjectByType<PlayerSpawner>();
        var frontyard = GameObject.Find("KosFrontyard");
        result.Log("mainCams={0} listeners={1} spawnPoint={2} frontyard={3} pos={4}", mainCams, listeners,
            spawner != null && spawner.SpawnPoint != null ? spawner.SpawnPoint.name : "NONE",
            frontyard != null, frontyard != null ? frontyard.transform.position.ToString("F2") : "-");
    }
}
```
Expected:
- `mainCams=1 listeners=1 spawnPoint=PlayerSpawn_Kos frontyard=True`;
- log `[Player] Halaman kos: edge=…` sama dengan hasil Task 16 Step 5.

Cek idempoten: geser `KosFrontyard` +1 m di x lewat snippet, `SaveOpenScenes`, lalu jalankan `PlayerSceneSetup.Run(false, false, Vector2.zero, out _)` lagi.
Expected:
- posisi `KosFrontyard` tetap bergeser (tidak di-reset);
- hanya ada satu `Player` dan satu `KosFrontyard` di root `World.unity`.

Kembalikan dengan `Run(false, true, Vector2.zero, out _)`, lalu `PreviewSceneSetup.Run(DefaultSettingsFactory.LoadOrCreateAsset(), false, out _)`.
Expected: `GameObject` `PreviewCam` tetap `activeSelf == false` dan jumlah MainCamera aktif tetap 1.

Lalu `mcp__unity-mcp__Unity_GetConsoleLogs` (`logTypes: "warning,error"`). Kalau ada warning `Objek generate menembus halaman kos`, catat nama objeknya di ledger; tidak perlu diperbaiki di task ini.

- [ ] **Step 6: Screenshot dan konfirmasi arah gerbang ke user**

```csharp
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result) =>
        result.Log(TycoWorld.Player.Tests.McpRender.KosShots("D:/UNITY/project/Tyco World/.superpowers/sdd/2026-10-13-fps-player"));
}
```
Lihat kedua PNG dengan Read tool, lalu kirim ke user (`SendUserFile`). Tanyakan dalam bahasa Indonesia santai: apakah gerbang Kos Raflesia 3 memang di sisi itu (selatan, menghadap ujung jalan kecil), atau di sisi lain (misalnya timur).
- **Kalau user menyebut sisi lain:** jalankan `PlayerSceneSetup.Run(false, true, <outward sisi itu, mis. new Vector2(1f, 0f) untuk timur>, out _)`, ulangi Step 5–6, lalu catat ruling-nya.
- **Kalau user setuju:** lanjut.

- [ ] **Step 7: Commit**

Procedure C:
- path: `Assets/Player` `Assets/Scenes/World.unity`
- subject: `feat(player): player setup menu places player and kos front yard in World.unity`

---

### Task 19: Verifikasi Play (MCP, intent berskrip) dan catatan

**Files:**
- Create: `Assets/Player/Tests/McpPlayerDriver.cs`
- Create: `docs/superpowers/notes/2026-10-13-player-verification.md`

**Interfaces:**
- Consumes: semua komponen runtime; `McpRender` (Task 18).
- Produces: `McpPlayerDriver` dengan:
  - kontrol: `Install()`, `Hold(float x, float y, bool sprint, float seconds)`, `Press(bool interact, bool jump, bool crouch, bool flashlight)`, `Face(float x, float y, float z)`, `TeleportTo(float x, float y, float z, float yaw)`;
  - pembacaan: `Status()` → string, `FlashlightLuminance()` → string, `Spawn()` → string.

- [ ] **Step 1: Tulis helper driver**

`Assets/Player/Tests/McpPlayerDriver.cs`:
```csharp
using System.Globalization;
using UnityEngine;

namespace TycoWorld.Player.Tests
{
    /// <summary>Dev helper: drives the real player in Play mode from Unity MCP snippets (domain reload is off, so statics persist).</summary>
    public static class McpPlayerDriver
    {
        sealed class CountingSoundPlayer : ISoundPlayer
        {
            readonly ISoundPlayer inner;
            public int Count;
            public CountingSoundPlayer(ISoundPlayer inner) { this.inner = inner; }

            public void Play(AudioClip clip, float volume, float pitch)
            {
                Count++;
                if (inner != null) inner.Play(clip, volume, pitch);
            }
        }

        static ScriptedIntentSource source;
        static CountingSoundPlayer gateSound, doorSound, motorSound;

        static FirstPersonPlayer Player => Object.FindAnyObjectByType<FirstPersonPlayer>();

        public static string Install()
        {
            var p = Player;
            if (p == null) return "NO PLAYER";
            source = new ScriptedIntentSource();
            p.SetIntentSource(source);
            gateSound = Wrap(Object.FindAnyObjectByType<GateInteractable>());
            doorSound = Wrap(Object.FindAnyObjectByType<LockedDoorInteractable>());
            motorSound = Wrap(Object.FindAnyObjectByType<PlaceholderVehicleInteractable>());
            return Status();
        }

        public static string Hold(float x, float y, bool sprint, float seconds)
        {
            source.Hold(new PlayerIntent { Move = new Vector2(x, y), Sprint = sprint }, seconds);
            return Status();
        }

        public static string Press(bool interact, bool jump, bool crouch, bool flashlight)
        {
            source.Press(new PlayerIntent { Interact = interact, Jump = jump, CrouchToggle = crouch, Flashlight = flashlight });
            return Status();
        }

        /// <summary>Turns the view toward a world point (yaw and pitch) on the next frame.</summary>
        public static string Face(float x, float y, float z)
        {
            var p = Player;
            var dir = new Vector3(x, y, z) - p.View.position;
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Atan2(dir.y, new Vector2(dir.x, dir.z).magnitude) * Mathf.Rad2Deg;
            source.Press(new PlayerIntent { LookDegrees = new Vector2(Mathf.DeltaAngle(p.Yaw, yaw), pitch - p.Pitch) });
            return Status();
        }

        public static string TeleportTo(float x, float y, float z, float yaw)
        {
            Player.Teleport(new Vector3(x, y, z), yaw);
            return Status();
        }

        public static string Spawn()
        {
            var point = Object.FindAnyObjectByType<PlayerSpawnPoint>();
            return string.Format(CultureInfo.InvariantCulture, "spawn=({0:F2},{1:F2},{2:F2}) yaw={3:F1}", point.Feet.x, point.Feet.y, point.Feet.z, point.Yaw);
        }

        public static string FlashlightLuminance()
        {
            var p = Player;
            var cam = p.View.GetComponent<Camera>();
            var flashlight = p.GetComponentInChildren<Flashlight>(true);
            return string.Format(CultureInfo.InvariantCulture, "flashlight={0} lum={1:F4}", flashlight.IsOn, McpRender.CenterLuminance(cam));
        }

        public static string Status()
        {
            var p = Player;
            if (p == null) return "NO PLAYER";
            var spawner = p.GetComponent<PlayerSpawner>();
            var steps = p.GetComponent<FootstepAudio>();
            var interactor = p.GetComponentInChildren<Interactor>(true);
            var flashlight = p.GetComponentInChildren<Flashlight>(true);
            var hud = p.GetComponent<PlayerHud>();
            var gate = Object.FindAnyObjectByType<GateInteractable>();
            var pos = p.transform.position;
            return string.Format(CultureInfo.InvariantCulture,
                "state={0} waited={1:F2} pos=({2:F2},{3:F2},{4:F2}) yaw={5:F1} grounded={6} surface={7} feature={8} kind={9} stamina={10:F2} staminaVisible={11} " +
                "prompt='{12}' message='{13}' steps={14} footsteps={15} lastSurface={16} gate={17} gateAngle={18:F0} gateSounds={19} doorSounds={20} motorSounds={21} " +
                "flashlight={22} focus={23} hudBuilt={24} scriptDone={25} frame={26} crouched={27}",
                spawner != null ? spawner.State.ToString() : "-", spawner != null ? spawner.WaitedSeconds : 0f,
                pos.x, pos.y, pos.z, p.Yaw, p.IsGrounded, p.Ground.Surface,
                p.Ground.Feature != null ? p.Ground.Feature.OsmId : 0, p.Ground.Feature != null ? p.Ground.Feature.Kind.ToString() : "-",
                p.Stamina.Normalized, p.Hud.StaminaVisible, p.Hud.Prompt, p.Hud.Message, p.StepCount,
                steps != null ? steps.PlayedCount : -1, steps != null ? steps.LastSurface.ToString() : "-",
                gate != null ? gate.State.ToString() : "-", gate != null ? gate.AngleDeg : 0f,
                gateSound != null ? gateSound.Count : -1, doorSound != null ? doorSound.Count : -1, motorSound != null ? motorSound.Count : -1,
                flashlight != null && flashlight.IsOn, interactor != null && interactor.Focus != null ? interactor.Focus.name : "-",
                hud != null && hud.View != null, source == null || source.IsDone, Time.frameCount, p.IsCrouched);
        }

        static CountingSoundPlayer Wrap(Interactable target)
        {
            if (target == null) return null;
            var counter = new CountingSoundPlayer(target.AudioOutput != null ? new AudioSourceSoundPlayer(target.AudioOutput) : null);
            target.SetSoundPlayer(counter);
            return counter;
        }
    }
}
```

Procedure R, lalu Procedure T dengan `Group = "."`. Expected: `failed=0` (driver tidak punya tes; ini cek compile).

- [ ] **Step 2: Masuk Play dan tunggu spawn**

Pastikan `World.unity` dan `World_Generated.unity` terbuka dan tersimpan. Lalu panggil snippet berikut:
```csharp
using UnityEditor;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result) { EditorApplication.EnterPlaymode(); result.Log("entering"); }
}
```

Polling dengan snippet di bawah sampai `state=Spawned` (ulangi tiap beberapa detik; jangan pakai sleep di dalam snippet):
```csharp
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result) => result.Log(TycoWorld.Player.Tests.McpPlayerDriver.Status());
}
```

Lalu panggil `McpPlayerDriver.Install()` dan `McpPlayerDriver.Spawn()`.

Expected:
- `state=Spawned`;
- `pos` ≤ 1 m dari `spawn`;
- `grounded=True surface=Concrete hudBuilt=True message='Kos Raflesia 3'`.

Catat `waited` (waktu sampai spawn) ke ledger.

- [ ] **Step 3: Jalankan skenario (satu panggilan MCP per baris, polling `Status()` sampai `scriptDone=True` setiap kali ada Hold)**

Ganti `CommandScript` dengan panggilan `McpPlayerDriver` yang sesuai. Ekspektasi tiap langkah:

1. **Pagar tertutup menghalangi.**
   - Panggilan: `Hold(0, 1, false, 4)`.
   - Expected: pemain berhenti sebelum garis pagar, yaitu jarak dari spawn sepanjang arah hadap kurang dari kedalaman halaman dikurangi 1,2 m (sekitar 4,8 m). `focus=Gate`, `prompt='[E] Buka pagar'`.
2. **Buka pagar.**
   - Panggilan: `Press(true, false, false, false)`, polling 2 detik.
   - Expected: `gate=Open gateAngle=±100 gateSounds>=2`.
3. **Keluar ke jalan.**
   - Panggilan: `Hold(0, 1, false, 5)`.
   - Expected: `surface=Asphalt kind=Road` (catat `feature`, diprediksi 689899501), `steps>0`, `footsteps>=steps`, `lastSurface=Asphalt`.
4. **Lari menguras stamina.**
   - Panggilan: `Hold(0, 1, true, 3)`.
   - Expected: `stamina<0.6 staminaVisible=True`. Lalu `Hold(0, 1, true, 6)`, expected `stamina<=0.3` (terkunci, tidak bisa lari).
5. **Pintu terkunci.**
   - Panggilan: `TeleportTo(<spawn>, <yaw spawn + 180>)` lalu `Face(<posisi pintu + 1 m ke atas>)`.
   - Expected: `focus=KosDoor prompt='[E] Buka pintu'`. Lalu `Press(true, …)`, expected `message='Pintunya terkunci.' doorSounds=1`.
6. **Motor sementara.**
   - Panggilan: `Face(<posisi motor + 0,8 m ke atas>)`. Kalau `focus` bukan motor, `Hold` maju 0,3 detik sampai `focus=Motor_Placeholder`.
   - Expected: `prompt='[E] Naik motor'`. Lalu `Press(true, …)`, expected `message='Belum bisa naik motor.' motorSounds=1`.
7. **Senter.**
   - Panggilan: `TeleportTo(<spawn>, <yaw spawn + 180>)`, `FlashlightLuminance()` (L0), `Press(false, false, false, true)`, polling satu `Status()`, lalu `FlashlightLuminance()` (L1).
   - Expected: `flashlight=True` dan `L1 > L0 * 1.5` serta `L1 - L0 > 0.02`.
   - **Kalau gagal:** cek dulu apakah sorot senter tidak dirender karena batas light Forward+. Kalau benar itu penyebabnya, tambahkan task susulan `LampLightCuller` (lampu jalan hanya aktif dalam radius sekitar 70 m dari kamera) dengan siklus TDD yang sama sebelum lanjut.
8. **Jatuh.**
   - Panggilan: `TeleportTo(<spawn.x>, -30, <spawn.z>, 0)`, polling.
   - Expected: `message='Kamu kembali ke kos.'` dan `pos` kembali ≤ 1 m dari spawn.
9. **Jongkok.**
   - Panggilan: `Press(false, false, true, false)`, lalu polling 0,5 detik.
   - Expected: `crouched=True`.
   - Panggilan: `Press` jongkok lagi.
   - Expected: `crouched=False` (di halaman terbuka ada ruang kepala).
10. **Tes CharacterController yang di-Ignore** di Task 10 (kalau spike gagal): ulangi skenarionya di sini dengan `Hold`/`TeleportTo` dan catat hasilnya.

Keluar Play:
```csharp
using UnityEditor;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result) { EditorApplication.ExitPlaymode(); result.Log("exiting"); }
}
```
Lalu `mcp__unity-mcp__Unity_GetConsoleLogs` (`logTypes: "error,warning"`).
Expected: tidak ada error. Setiap warning dicatat beserta isinya; warning UI Toolkit theme atau AudioListener ganda adalah bug yang harus diperbaiki sebelum commit.

Simpan screenshot pemain di halaman depan dengan `McpRender.SavePng(<kamera pemain>, ".../player_view.png", 1280, 720)` selagi masih di Play mode (sebelum Exit), lalu kirim ke user dengan `SendUserFile`.

- [ ] **Step 4: Tulis catatan verifikasi dan checklist QA manual**

`docs/superpowers/notes/2026-10-13-player-verification.md`:
```markdown
# Player first-person — verifikasi (2026-10-13)

- Tes EditMode: <angka dari Procedure T Group "."> (WorldGen + Player), failed=0.
- Generate ulang: hash 278 chunk identik dengan sebelum sub-proyek #2; KeyBuilding=6, Building=4915; MeshCollider <angka>; WorldEdge 4.
- Halaman kos: <output KosSitePlanner.Describe>; arah gerbang dikonfirmasi user: <ya/sisi lain>.
- Play (MCP, intent berskrip): spawn dalam <waited> s; <hasil langkah 1–9 dari Task 19 Step 3, satu baris per langkah>.
- Senter: luminansi tengah L0=<…> → L1=<…>.
- Console setelah Play: <tidak ada error / daftar warning>.

## Checklist QA manual (perlu dicoba sendiri, input asli tidak bisa diotomasi)
- [ ] Klik Game view → kursor terkunci; Esc melepas; klik kiri mengunci lagi; kamera tidak berputar saat kursor lepas.
- [ ] WASD terasa pas (jalan 3 m/s), Shift lari, stamina habis sekitar 5–6 detik, bar stamina muncul/hilang.
- [ ] Spasi lompat (tidak bisa melompati pagar), C jongkok/berdiri, tidak bisa berdiri di bawah atap rendah.
- [ ] Sensitivitas mouse (0.1°/px) dan head-bob nyaman; ubah di `Assets/Player/Config/PlayerTuning.asset` kalau perlu.
- [ ] F senter: klik terdengar, sorotan terlihat di malam hari.
- [ ] E di pagar (derit + gerendel), pintu kos (terkunci), motor (belum bisa).
- [ ] Bunyi langkah berbeda di halaman (beton), jalan (aspal), rumput, dan tanah; volume seimbang.
- [ ] Berjalan ke tepi dunia → tertahan dinding tak terlihat.
```
Isi semua `<…>` dengan angka nyata dari langkah sebelumnya. Jangan ada placeholder yang tersisa.

- [ ] **Step 5: Jalankan semua tes sekali lagi, lalu commit**

Procedure T dengan `Group = "."`. Expected: `failed=0`.

Procedure C:
- path: `Assets/Player/Tests/McpPlayerDriver.cs` `Assets/Player/Tests/McpPlayerDriver.cs.meta` `docs/superpowers/notes/2026-10-13-player-verification.md`
- subject: `test(player): play-mode verification driver and notes`
