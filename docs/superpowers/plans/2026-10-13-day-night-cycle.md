# Day/Night Cycle Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Jam dunia yang berjalan (24 menit = 1 hari, mulai 06:30) dengan transisi cahaya halus subuh → siang → senja → malam, mode horor 19:00–05:00 (visual lebih seram, ambience, lampu berkedip), dan tombol debug T / `[` `]` / F3.

**Architecture:**
- Logika murni dites di EditMode: `WorldClock`, `DayPhase`, `DayCycleProfile.Evaluate` → `LightingState`, `LampFlicker`, `DebugTimeControls`.
- Satu `DayCycleDirector` (MonoBehaviour di World.unity) menerapkan hasilnya tiap frame ke matahari dan bulan, `RenderSettings` (ambient Trilight + kabut), salinan skybox procedural, tiga Volume berlapis (Malam = dasar, Siang dan Horor di atasnya), lampu jalan (`StreetLampSystem` dengan salinan material runtime), ambience, dan HUD pemain.
- Generator memasang penanda `StreetLamp` di prefab lampu.

**Tech Stack:** Unity 6000.6.5f1, URP 17.6.0 (Volume, Skybox/Procedural), Input System 1.20.0 (`Keyboard.current` untuk debug), UI Toolkit (HUD yang sudah ada), Unity Test Framework 1.8.0, Unity MCP, PowerShell/Bash.

**Spec:** `docs/superpowers/specs/2026-10-13-day-night-cycle-design.md`

## Global Constraints

**Proyek dan branch**
- Project root `D:\UNITY\project\Tyco World`. Branch kerja `feature/day-night-cycle` (sudah ada, dibuat dari `main` = `8328ba1`; spec di commit `7f0a116`).
- Unity 6000.6.5f1, URP 17.6.0, `com.unity.inputsystem` 1.20.0, `com.unity.test-framework` 1.8.0.

**Assembly dan namespace**
- Runtime `TycoWorld.DayCycle` (namespace `TycoWorld.DayCycle`).
- Editor `TycoWorld.DayCycle.Editor` (namespace `TycoWorld.DayCycle.Authoring`).
- Tes `TycoWorld.DayCycle.Tests` (namespace `TycoWorld.DayCycle.Tests`).
- Arah dependensi DayCycle → Player → WorldGen. Tidak ada yang mereferensikan DayCycle.

**Aturan C# dan API Unity**
- C# 9: tidak ada `record`, `init`, `^`.
- Semua format angka memakai `CultureInfo.InvariantCulture`.
- Unity 6.6:
  - pakai `Object.FindObjectsByType<T>()` / `Object.FindAnyObjectByType<T>()` (overload dengan `FindObjectsSortMode` sudah obsolete);
  - jangan pakai API instance-ID berbasis `int`.
- Jangan pakai `??` / `?.` / `??=` pada `UnityEngine.Object`.
- File yang punya `using System;` dan `using UnityEngine;` sekaligus menulis `UnityEngine.Object` secara eksplisit.

**Runtime**
- **Tidak boleh mengubah asset saat runtime.**
  - Material kepala lampu dan skybox memakai salinan `new Material(...)`.
  - VolumeProfile hanya diubah lewat `Volume.weight`.
  - Setelah Play, `git status` tidak boleh menunjukkan asset yang berubah.
- Domain reload saat Play mati: tidak ada state `static` yang bisa berubah. Event ada di instance.
- Komponen yang dites punya `Initialize()` / `Tick(dt)` / `Refresh()` eksplisit. `Update()` hanya bekerja kalau `Application.isPlaying`.

**Tes**
- Tes yang menyentuh `RenderSettings` wajib menyimpan nilainya di `SetUp` dan memulihkannya di `TearDown` (helper `RenderSettingsSnapshot`, Task 10).
- Fixture GameObject dihancurkan di `TearDown`.

**Teks yang tampil ke pemain** (tulis persis):
- `"Malam semakin larut…"` (karakter elipsis U+2026);
- `"Subuh. Malam sudah lewat."`;
- `"Lompat ke 19:30"` / `"Lompat ke 06:30"`;
- `"Kecepatan waktu x1"` / `"x10"` / `"x60"`.

**Nilai dan konfigurasi**
- Nilai default hanya ditulis sebagai initializer field di `DayCycleProfile` dan konstanta di `DayTimes`, disalin dari spec §1 dan §4.
- Jangan mengubah ProjectSettings. Jangan pernah `git add ProjectSettings`, `git add -A`, atau `git add .`.

**Commit**
- Setiap commit diakhiri:
  ```
  Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5
  ```

## Review Focus

1. **Loncat waktu berulang dengan T, atau kecepatan x60 dengan frame panjang yang melewati 19:00/05:00 dalam satu tick.** Event horor harus terpicu tepat sekali per perpindahan, dan pesan HUD tidak boleh dobel. Dipaku oleh:
   - `DayPhaseTests.LargeJump_StillFiresOnce` dan `JumpBackAndForth_FiresEachCrossing` (Task 2);
   - `DayCycleDirectorTests.Tick_LongFrameAcross1900_FiresOnce` (Task 10).
2. **Asset tidak boleh berubah karena runtime** (material kepala lampu, material skybox, profil Volume). Dipaku oleh:
   - `StreetLampSystemTests.Apply_NeverTouchesSharedHeadMaterial` (Task 7);
   - `DayCycleDirectorTests.Skybox_UsesRuntimeCopy` (Task 10);
   - pengecekan `git status` setelah Play (Task 14).
3. **Dunia lama tanpa `StreetLamp`.** Hanya satu warning, lampu dibiarkan apa adanya, tanpa exception. Dipaku oleh `DayCycleDirectorTests.NoStreetLamps_WarnsOnce` (Task 10).
4. **Scene tanpa Player atau HUD** (misalnya scene uji, atau pemain sedang dinonaktifkan). Director tetap berjalan tanpa NullReferenceException. Dipaku oleh `DayCycleDirectorTests.WithoutPlayer_RunsWithoutErrors` (Task 10).
5. **Menjalankan ulang Day Cycle Setup tidak menimpa tuning user** (profil, profil Volume, klip). Dipaku oleh `DayCycleSetupTests.Ensure*_KeepsExisting` (Task 13).

---

## Prosedur standar

Workspace ledger (diabaikan git): `D:\UNITY\project\Tyco World\.superpowers\sdd\2026-10-13-day-night-cycle\` (dibuat di Task 1).

### Procedure R+T — refresh, compile, dan jalankan tes

Panggil `mcp__unity-mcp__Unity_RunCommand` dengan kode di bawah. Ganti `Group` (regex nama tes; `"."` = semua).

```csharp
using UnityEditor;

internal class CommandScript : IRunCommand
{
    const string Group = "DayCycleSmokeTests";
    const string ResultPath = "D:/UNITY/project/Tyco World/.superpowers/sdd/2026-10-13-day-night-cycle/last-test.txt";

    public void Execute(ExecutionResult result)
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        if (EditorApplication.isCompiling) { result.LogError("STILL COMPILING after refresh"); return; }
        string text = TycoWorld.WorldGen.Tests.McpTestRunner.Run(Group, ResultPath);
        if (text.StartsWith("group=") && text.Contains(" failed=0 ")) result.Log(text);
        else result.LogError(text);
    }
}
```

Arti hasilnya:
- `COMPILE FAILED`: baca error lewat `mcp__unity-mcp__Unity_GetConsoleLogs` (`logTypes: "error"`, `includeStackTrace: false`).
- `DIRTY SCENE`: simpan scene dulu.
- Kalau panggilan MCP gagal karena domain reload ("Could not find type … RunCommandMacroEvaluatorEntryPoint"), ulangi.
- Error `[Player] …` yang muncul saat Group `"."` berasal dari tes spawner dan sudah ditangkap `LogAssert`. Itu bukan kegagalan.

### Procedure C — commit

```bash
git -C "D:/UNITY/project/Tyco World" add <path...>
git -C "D:/UNITY/project/Tyco World" commit -m "<subject>

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

- Ikutkan `.meta` (cukup add foldernya).
- Jangan pernah add ProjectSettings.

### Catatan snippet MCP

- Bentuknya wajib `internal class CommandScript : IRunCommand`.
- Tanpa `System.Reflection`.
- Format `{0:F2}` tidak didukung `result.Log`, jadi format sendiri dengan `ToString("F2", CultureInfo.InvariantCulture)`.

---

## Peta file

```
Assets/DayCycle/Runtime/TycoWorld.DayCycle.asmdef, DayTimes.cs                    (Task 1)
Assets/DayCycle/Runtime/Time/WorldClock.cs, DayPhase.cs                            (Task 2)
Assets/DayCycle/Runtime/Profile/DayCycleProfile.cs, LightingState.cs               (Task 3)
Assets/DayCycle/Runtime/Lamps/LampFlicker.cs (4), StreetLampSystem.cs (7)
Assets/DayCycle/Runtime/Debug/DebugTimeControls.cs (5), DayCycleDebug.cs (11)
Assets/DayCycle/Runtime/Audio/AmbienceAudio.cs                                     (Task 9)
Assets/DayCycle/Runtime/DayCycleDirector.cs                                        (Task 10)
Assets/DayCycle/Editor/TycoWorld.DayCycle.Editor.asmdef, DayCyclePaths.cs          (Task 1)
Assets/DayCycle/Editor/AmbienceSynth.cs, WavWriter.cs, DayCycleAudioImportRules.cs, DayCycleAudioBuilder.cs (Task 12)
Assets/DayCycle/Editor/DayCycleAssetsBuilder.cs, DayCycleSceneSetup.cs             (Task 13)
Assets/DayCycle/Tests/TycoWorld.DayCycle.Tests.asmdef, DayCycleSmokeTests.cs       (Task 1)
Assets/DayCycle/Tests/WorldClockTests.cs, DayPhaseTests.cs (2), DayCycleProfileTests.cs (3),
    LampFlickerTests.cs (4), DebugTimeControlsTests.cs (5), LampFixture.cs + StreetLampSystemTests.cs (7),
    AmbienceAudioTests.cs (9), RenderSettingsSnapshot.cs + DirectorFixture.cs + DayCycleDirectorTests.cs (10),
    DayCycleDebugTests.cs (11), AmbienceSynthTests.cs + WavWriterTests.cs + DayCycleAudioAssetsTests.cs (12),
    DayCycleSetupTests.cs (13), McpDayCycleDriver.cs (14)
Assets/DayCycle/Audio/ambience_night_crickets.mp3, ambience_day.wav, ambience_horror_drone.wav, SOURCES.md (Task 12)
Assets/DayCycle/Config/DayCycleProfile.asset, Volume_Day.asset, Volume_Horror.asset, Sky_Procedural.mat (Task 13)
Assets/WorldGen/Runtime/StreetLamp.cs (6), ISceneLightingOwner.cs (10)
Assets/WorldGen/Editor/Art/PlaceholderPrefabFactory.cs (6), Preview/PreviewSceneSetup.cs (13)
Assets/WorldGen/Tests/McpTestRunner.cs (1), StreetLampPrefabTests.cs (6)
Assets/WorldGen/Prefabs/StreetLamp_A.prefab                                        (Task 6, dibangun ulang)
Assets/Player/Runtime/UI/HudModel.cs, HudView.cs (8); Assets/Player/Editor/PlayerRigBuilder.cs (13)
Assets/Player/Tests/HudModelTests.cs, HudViewTests.cs (8), PlayerAssetsTests.cs (13)
Assets/Player/Prefabs/Player.prefab, Assets/Scenes/World.unity                     (Task 13)
docs/superpowers/specs/2026-10-13-day-night-cycle-design.md                        (Task 13: status + §11)
docs/superpowers/notes/2026-10-13-day-night-verification.md                        (Task 14)
```

**Catatan desain lapisan Volume** (dicatat di spec pada Task 13):
- URP mencampur Volume global berdasarkan prioritas.
- Volume Malam (`Global Volume`, prioritas 0) jadi dasar dengan bobot tetap 1.
- Volume Siang (prioritas 1) memakai bobot = `DayVolumeWeight` dan meng-override semua parameter yang di-override Malam. Hasilnya sama dengan lerp(Malam, Siang, w).
- Volume Horor (prioritas 2) memakai bobot = `HorrorWeight`.

---
### Task 1: Scaffold assembly DayCycle, runner tiga assembly, hash dasar

**Files:**
- Create: `Assets/DayCycle/Runtime/TycoWorld.DayCycle.asmdef`, `Assets/DayCycle/Runtime/DayTimes.cs`
- Create: `Assets/DayCycle/Editor/TycoWorld.DayCycle.Editor.asmdef`, `Assets/DayCycle/Editor/DayCyclePaths.cs`
- Create: `Assets/DayCycle/Tests/TycoWorld.DayCycle.Tests.asmdef`, `Assets/DayCycle/Tests/DayCycleSmokeTests.cs`
- Modify: `Assets/WorldGen/Tests/McpTestRunner.cs` (tambah `TycoWorld.DayCycle.Tests`)

**Interfaces:**
- Produces:
  - `DayTimes` dengan konstanta float `StartHour`, `DawnStart`, `DayStart`, `DuskStart`, `NightStart`, `HorrorStart`, `HorrorEnd`, `DebugMorning`, `DebugNight`, `RealSecondsPerGameHour`;
  - konstanta `DayCyclePaths.*`;
  - runner yang mencakup ketiga assembly tes.

- [ ] **Step 1: Ledger dan hash dasar (sebelum ada perubahan apa pun)**

```bash
mkdir -p "/d/UNITY/project/Tyco World/.superpowers/sdd/2026-10-13-day-night-cycle"
git -C "/d/UNITY/project/Tyco World" check-ignore -v ".superpowers/sdd/2026-10-13-day-night-cycle/x.txt"
```
Expected: `.superpowers/sdd/.gitignore:1:*`.

Snippet MCP:
```csharp
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;

internal class CommandScript : IRunCommand
{
    const string Dir = "D:/UNITY/project/Tyco World/.superpowers/sdd/2026-10-13-day-night-cycle";

    public void Execute(ExecutionResult result)
    {
        EditorSceneManager.SaveOpenScenes();
        var settings = AssetDatabase.LoadAssetAtPath<WorldGenSettings>(WorldGenPaths.SettingsAsset);
        if (!WorldGenerationRunner.Generate(settings, false, out string error)) { result.LogError(error); return; }
        result.Log(TycoWorld.WorldGen.Tests.McpVerify.WriteChunkHashes(Dir + "/hashes_before.txt"));
        result.Log(WorldGenerationRunner.LastReport.Summary());
    }
}
```
Expected: `chunks=278`, `KeyBuilding=6`, `Building=4915`, tanpa warning PhysX.

- [ ] **Step 2: Tulis tes smoke yang gagal**

`Assets/DayCycle/Tests/TycoWorld.DayCycle.Tests.asmdef`:
```json
{
    "name": "TycoWorld.DayCycle.Tests",
    "rootNamespace": "TycoWorld.DayCycle.Tests",
    "references": [
        "TycoWorld.DayCycle",
        "TycoWorld.DayCycle.Editor",
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

`Assets/DayCycle/Tests/DayCycleSmokeTests.cs`:
```csharp
using NUnit.Framework;
using TycoWorld.DayCycle.Authoring;

namespace TycoWorld.DayCycle.Tests
{
    public class DayCycleSmokeTests
    {
        [Test]
        public void DayTimes_MatchSpec()
        {
            Assert.AreEqual(6.5f, DayTimes.StartHour);
            Assert.AreEqual(5f, DayTimes.DawnStart);
            Assert.AreEqual(6.5f, DayTimes.DayStart);
            Assert.AreEqual(17.5f, DayTimes.DuskStart);
            Assert.AreEqual(19f, DayTimes.NightStart);
            Assert.AreEqual(19f, DayTimes.HorrorStart);
            Assert.AreEqual(5f, DayTimes.HorrorEnd);
            Assert.AreEqual(6.5f, DayTimes.DebugMorning);
            Assert.AreEqual(19.5f, DayTimes.DebugNight);
            Assert.AreEqual(60f, DayTimes.RealSecondsPerGameHour);
        }

        [Test]
        public void Paths_AreUnderDayCycleRoot()
        {
            Assert.AreEqual("Assets/DayCycle", DayCyclePaths.Root);
            StringAssert.StartsWith(DayCyclePaths.Root + "/", DayCyclePaths.ProfileAsset);
            StringAssert.StartsWith(DayCyclePaths.Root + "/", DayCyclePaths.NightAmbienceClip);
        }

        [Test]
        public void Urp_And_InputSystem_AreReferenced()
        {
            Assert.IsNotNull(typeof(UnityEngine.Rendering.Volume));
            Assert.IsNotNull(typeof(UnityEngine.InputSystem.Keyboard));
        }
    }
}
```

- [ ] **Step 3: Jalankan, pastikan gagal**

Procedure R+T dengan `Group = "DayCycleSmokeTests"`.
Expected: `COMPILE FAILED` (`CS0234 TycoWorld.DayCycle.Authoring` / `CS0103 DayTimes`).

- [ ] **Step 4: Implementasi**

`Assets/DayCycle/Runtime/TycoWorld.DayCycle.asmdef`:
```json
{
    "name": "TycoWorld.DayCycle",
    "rootNamespace": "TycoWorld.DayCycle",
    "references": [
        "TycoWorld.WorldGen",
        "TycoWorld.Player",
        "Unity.InputSystem",
        "Unity.RenderPipelines.Core.Runtime",
        "Unity.RenderPipelines.Universal.Runtime"
    ],
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

`Assets/DayCycle/Runtime/DayTimes.cs`:
```csharp
namespace TycoWorld.DayCycle
{
    /// <summary>Clock constants from spec #3 §1 (hours are 0–24 game hours).</summary>
    public static class DayTimes
    {
        public const float StartHour = 6.5f;
        public const float DawnStart = 5f;
        public const float DayStart = 6.5f;
        public const float DuskStart = 17.5f;
        public const float NightStart = 19f;
        public const float HorrorStart = 19f;
        public const float HorrorEnd = 5f;
        public const float DebugMorning = 6.5f;
        public const float DebugNight = 19.5f;
        /// <summary>One real minute is one game hour (a day lasts 24 real minutes).</summary>
        public const float RealSecondsPerGameHour = 60f;
    }
}
```

`Assets/DayCycle/Editor/TycoWorld.DayCycle.Editor.asmdef`:
```json
{
    "name": "TycoWorld.DayCycle.Editor",
    "rootNamespace": "TycoWorld.DayCycle.Authoring",
    "references": [
        "TycoWorld.DayCycle",
        "TycoWorld.Player",
        "TycoWorld.Player.Editor",
        "TycoWorld.WorldGen",
        "TycoWorld.WorldGen.Editor",
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

`Assets/DayCycle/Editor/DayCyclePaths.cs`:
```csharp
namespace TycoWorld.DayCycle.Authoring
{
    /// <summary>Project-relative paths of the day/night cycle sub-project.</summary>
    public static class DayCyclePaths
    {
        public const string Root = "Assets/DayCycle";
        public const string ConfigFolder = Root + "/Config";
        public const string ProfileAsset = ConfigFolder + "/DayCycleProfile.asset";
        public const string DayVolumeAsset = ConfigFolder + "/Volume_Day.asset";
        public const string HorrorVolumeAsset = ConfigFolder + "/Volume_Horror.asset";
        public const string SkyboxMaterial = ConfigFolder + "/Sky_Procedural.mat";
        public const string AudioFolder = Root + "/Audio";
        public const string DayAmbienceClip = AudioFolder + "/ambience_day.wav";
        public const string NightAmbienceClip = AudioFolder + "/ambience_night_crickets.mp3";
        public const string HorrorDroneClip = AudioFolder + "/ambience_horror_drone.wav";
        public const string AudioSourcesFile = AudioFolder + "/SOURCES.md";
    }
}
```

`Assets/WorldGen/Tests/McpTestRunner.cs`: ganti baris `assemblyNames` menjadi
```csharp
                    assemblyNames = new[] { "TycoWorld.WorldGen.Tests", "TycoWorld.Player.Tests", "TycoWorld.DayCycle.Tests" },
```

- [ ] **Step 5: Jalankan, pastikan lulus**

Procedure R+T dengan `Group = "DayCycleSmokeTests"`. Expected: `passed=3 failed=0`.

Lalu `Group = "."`. Expected: `passed=431 failed=0` (428 + 3).

- [ ] **Step 6: Commit**

Procedure C:
- path: `Assets/DayCycle` `Assets/DayCycle.meta` `Assets/WorldGen/Tests/McpTestRunner.cs`
- subject: `chore(daycycle): scaffold day cycle assemblies and three-assembly test runner`

---

### Task 2: `WorldClock` dan `DayPhase`

**Files:**
- Create: `Assets/DayCycle/Runtime/Time/WorldClock.cs`, `Assets/DayCycle/Runtime/Time/DayPhase.cs`
- Test: `Assets/DayCycle/Tests/WorldClockTests.cs`, `Assets/DayCycle/Tests/DayPhaseTests.cs`

**Interfaces:**
- Consumes: `DayTimes`.
- Produces:
  - `WorldClock(float startHours)` dengan `Hours` [0,24), `Day` (≥1), `Hour`, `Minute`, `Display` ("HH:MM"), `SpeedMultiplier` (get/set), `Tick(float dt)`, `SetTime(float hours)`.
  - `enum DayPhaseKind { Dawn, Day, Dusk, Night }`.
  - `DayPhase` dengan:
    - `Phase`, `IsHorror`;
    - static `PhaseAt(float)` dan `HorrorAt(float)`;
    - `Initialize(float hours)` (tanpa event) dan `Update(float hours)`;
    - `event Action HorrorStarted` dan `HorrorEnded`.

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/DayCycle/Tests/WorldClockTests.cs`:
```csharp
using NUnit.Framework;

namespace TycoWorld.DayCycle.Tests
{
    public class WorldClockTests
    {
        [Test]
        public void StartsAtGivenTime_OnDayOne()
        {
            var c = new WorldClock(6.5f);
            Assert.AreEqual(6.5f, c.Hours, 1e-5f);
            Assert.AreEqual(1, c.Day);
            Assert.AreEqual("06:30", c.Display);
        }

        [Test]
        public void OneRealMinute_IsOneGameHour()
        {
            var c = new WorldClock(6.5f);
            c.Tick(60f);
            Assert.AreEqual(7.5f, c.Hours, 1e-4f);
        }

        [Test]
        public void Speed_MultipliesRate()
        {
            var c = new WorldClock(6.5f) { SpeedMultiplier = 60f };
            c.Tick(1f);
            Assert.AreEqual(7.5f, c.Hours, 1e-4f);
        }

        [Test]
        public void PassingMidnight_IncrementsDay()
        {
            var c = new WorldClock(23.5f);
            c.Tick(60f);
            Assert.AreEqual(0.5f, c.Hours, 1e-4f);
            Assert.AreEqual(2, c.Day);
        }

        [Test]
        public void SetTime_WrapsIntoRange_AndKeepsDay()
        {
            var c = new WorldClock(6.5f);
            c.SetTime(-1f);
            Assert.AreEqual(23f, c.Hours, 1e-4f);
            c.SetTime(25.5f);
            Assert.AreEqual(1.5f, c.Hours, 1e-4f);
            Assert.AreEqual(1, c.Day);
        }

        [Test]
        public void Display_IsPadded()
        {
            var c = new WorldClock(19.5f);
            Assert.AreEqual("19:30", c.Display);
            c.SetTime(0.25f);
            Assert.AreEqual("00:15", c.Display);
        }

        [Test]
        public void ZeroOrNegativeDt_DoesNothing()
        {
            var c = new WorldClock(6.5f);
            c.Tick(0f);
            c.Tick(-5f);
            Assert.AreEqual(6.5f, c.Hours, 1e-5f);
        }
    }
}
```

`Assets/DayCycle/Tests/DayPhaseTests.cs`:
```csharp
using NUnit.Framework;

namespace TycoWorld.DayCycle.Tests
{
    public class DayPhaseTests
    {
        [TestCase(4.99f, DayPhaseKind.Night)]
        [TestCase(5f, DayPhaseKind.Dawn)]
        [TestCase(6.49f, DayPhaseKind.Dawn)]
        [TestCase(6.5f, DayPhaseKind.Day)]
        [TestCase(17.49f, DayPhaseKind.Day)]
        [TestCase(17.5f, DayPhaseKind.Dusk)]
        [TestCase(18.99f, DayPhaseKind.Dusk)]
        [TestCase(19f, DayPhaseKind.Night)]
        [TestCase(0f, DayPhaseKind.Night)]
        public void PhaseAt_FollowsSpec(float hours, DayPhaseKind expected) => Assert.AreEqual(expected, DayPhase.PhaseAt(hours));

        [TestCase(18.99f, false)]
        [TestCase(19f, true)]
        [TestCase(23.9f, true)]
        [TestCase(2f, true)]
        [TestCase(4.99f, true)]
        [TestCase(5f, false)]
        [TestCase(12f, false)]
        public void HorrorAt_Is1900To0500(float hours, bool expected) => Assert.AreEqual(expected, DayPhase.HorrorAt(hours));

        static (DayPhase Phase, int[] Counts) Make(float start)
        {
            var p = new DayPhase();
            var counts = new int[2];
            p.HorrorStarted += () => counts[0]++;
            p.HorrorEnded += () => counts[1]++;
            p.Initialize(start);
            return (p, counts);
        }

        [Test]
        public void Initialize_DoesNotFireEvents()
        {
            var (p, counts) = Make(22f);
            Assert.IsTrue(p.IsHorror);
            Assert.AreEqual(DayPhaseKind.Night, p.Phase);
            CollectionAssert.AreEqual(new[] { 0, 0 }, counts);
        }

        [Test]
        public void Crossing1900_FiresStartedOnce()
        {
            var (p, counts) = Make(18.9f);
            p.Update(18.95f);
            p.Update(19.01f);
            p.Update(19.5f);
            CollectionAssert.AreEqual(new[] { 1, 0 }, counts);
        }

        [Test]
        public void Crossing0500_FiresEndedOnce()
        {
            var (p, counts) = Make(4.9f);
            p.Update(5.01f);
            p.Update(6f);
            CollectionAssert.AreEqual(new[] { 0, 1 }, counts);
        }

        [Test]
        public void LargeJump_StillFiresOnce()
        {
            var (p, counts) = Make(18f);
            p.Update(20.5f);
            CollectionAssert.AreEqual(new[] { 1, 0 }, counts);
        }

        [Test]
        public void JumpBackAndForth_FiresEachCrossing()
        {
            var (p, counts) = Make(12f);
            p.Update(19.5f);
            p.Update(6.5f);
            p.Update(19.5f);
            CollectionAssert.AreEqual(new[] { 2, 1 }, counts);
        }
    }
}
```

- [ ] **Step 2: Jalankan, pastikan gagal**

Procedure R+T dengan `Group = "WorldClockTests|DayPhaseTests"`.
Expected: `COMPILE FAILED` (`CS0246 WorldClock`, `DayPhase`, `DayPhaseKind`).

- [ ] **Step 3: Implementasi**

`Assets/DayCycle/Runtime/Time/WorldClock.cs`:
```csharp
using System.Globalization;
using UnityEngine;

namespace TycoWorld.DayCycle
{
    /// <summary>Game clock: one real minute is one game hour, times SpeedMultiplier (debug).</summary>
    public sealed class WorldClock
    {
        public WorldClock(float startHours) => SetTime(startHours);

        public float Hours { get; private set; }
        public int Day { get; private set; } = 1;
        public float SpeedMultiplier { get; set; } = 1f;
        public int Hour => Mathf.FloorToInt(Hours);
        public int Minute => Mathf.Clamp(Mathf.FloorToInt((Hours - Hour) * 60f + 1e-3f), 0, 59);
        public string Display => Hour.ToString("00", CultureInfo.InvariantCulture) + ":" + Minute.ToString("00", CultureInfo.InvariantCulture);

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            Hours += dt / DayTimes.RealSecondsPerGameHour * SpeedMultiplier;
            while (Hours >= 24f)
            {
                Hours -= 24f;
                Day++;
            }
        }

        public void SetTime(float hours) => Hours = Mathf.Repeat(hours, 24f);
    }
}
```

`Assets/DayCycle/Runtime/Time/DayPhase.cs`:
```csharp
using System;

namespace TycoWorld.DayCycle
{
    public enum DayPhaseKind { Dawn, Day, Dusk, Night }

    /// <summary>Phase of the day and the horror window (19:00–05:00); events fire once per transition, also on time jumps.</summary>
    public sealed class DayPhase
    {
        public event Action HorrorStarted;
        public event Action HorrorEnded;

        public DayPhaseKind Phase { get; private set; }
        public bool IsHorror { get; private set; }

        public static DayPhaseKind PhaseAt(float hours)
        {
            if (hours >= DayTimes.NightStart || hours < DayTimes.DawnStart) return DayPhaseKind.Night;
            if (hours < DayTimes.DayStart) return DayPhaseKind.Dawn;
            return hours < DayTimes.DuskStart ? DayPhaseKind.Day : DayPhaseKind.Dusk;
        }

        public static bool HorrorAt(float hours) => hours >= DayTimes.HorrorStart || hours < DayTimes.HorrorEnd;

        public void Initialize(float hours)
        {
            Phase = PhaseAt(hours);
            IsHorror = HorrorAt(hours);
        }

        public void Update(float hours)
        {
            Phase = PhaseAt(hours);
            bool horror = HorrorAt(hours);
            if (horror == IsHorror) return;
            IsHorror = horror;
            if (horror) HorrorStarted?.Invoke();
            else HorrorEnded?.Invoke();
        }
    }
}
```

- [ ] **Step 4: Jalankan, pastikan lulus**

Procedure R+T dengan `Group = "WorldClockTests|DayPhaseTests"`. Expected: `passed=28 failed=0`.

- [ ] **Step 5: Commit**

Procedure C:
- path: `Assets/DayCycle`
- subject: `feat(daycycle): world clock and day phase with horror events`

---

### Task 3: `DayCycleProfile` dan `LightingState`

**Files:**
- Create: `Assets/DayCycle/Runtime/Profile/LightingState.cs`, `Assets/DayCycle/Runtime/Profile/DayCycleProfile.cs`
- Test: `Assets/DayCycle/Tests/DayCycleProfileTests.cs`

**Interfaces:**
- Consumes: `DayTimes`.
- Produces:
  - `struct LightingState` dengan field:
    - matahari: `SunElevation`, `SunYaw`, `SunIntensity`, `SunColor`, `SunEnabled`;
    - bulan: `MoonIntensity`, `MoonEnabled`;
    - ambient: `AmbientSky`, `AmbientEquator`, `AmbientGround`;
    - kabut: `FogDensity`, `FogColor`;
    - langit: `SkyExposure`, `SkyTint`;
    - Volume: `DayVolumeWeight`, `HorrorWeight`;
    - lampu: `LampIntensity01`;
    - ambience: `DayAmbience`, `NightAmbience`, `HorrorDrone`.
  - `DayCycleProfile : ScriptableObject` dengan:
    - field publik sesuai kode di bawah;
    - `CreateDefault()`, `SunElevation(float)`, `SunYaw(float)`, `HorrorWeight(float)`, static `DayAmbienceWeight(float)`, `Evaluate(float hours)` → `LightingState`.

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/DayCycle/Tests/DayCycleProfileTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.DayCycle.Tests
{
    public class DayCycleProfileTests
    {
        DayCycleProfile p;

        [SetUp]
        public void SetUp() => p = DayCycleProfile.CreateDefault();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(p);

        [Test]
        public void Defaults_MatchSpec()
        {
            Assert.AreEqual(5.75f, p.sunrise);
            Assert.AreEqual(17.75f, p.sunset);
            Assert.AreEqual(75f, p.sunMaxElevation);
            Assert.AreEqual(1.3f, p.sunIntensity);
            Assert.AreEqual(0.15f, p.moonIntensity);
            Assert.AreEqual(1.3f, p.skyExposureDay);
            Assert.AreEqual(0.8f, p.skyExposureDusk);
            Assert.AreEqual(0.08f, p.skyExposureNight);
            Assert.AreEqual(0.004f, p.fogDensityDay);
            Assert.AreEqual(0.008f, p.fogDensityDusk);
            Assert.AreEqual(0.02f, p.fogDensityNight);
            Assert.AreEqual(1.6f, p.horrorFogMultiplier);
            Assert.AreEqual(0.5f, p.horrorFadeHours);
            Assert.AreEqual(0.08f, p.flickerFraction);
            Assert.AreEqual(0.03f, p.deadFraction);
            Assert.AreEqual(5f, p.lampOnBelowElevation);
            Assert.AreEqual(3f, p.lampIntensity);
        }

        [Test]
        public void SunElevation_AtKeyTimes()
        {
            Assert.That(p.SunElevation(6f), Is.InRange(0f, 20f));
            Assert.AreEqual(75f, p.SunElevation(11.75f), 0.5f);
            Assert.Less(p.SunElevation(18.5f), 0f);
            Assert.Less(p.SunElevation(3f), -30f);
        }

        [Test]
        public void SunYaw_EastAtSunrise_WestAtSunset()
        {
            Assert.AreEqual(270f, p.SunYaw(6f), 15f);
            Assert.AreEqual(90f, p.SunYaw(17.5f), 15f);
        }

        [Test]
        public void SunAtNoon_MoonAtNight()
        {
            var noon = p.Evaluate(12f);
            Assert.IsTrue(noon.SunEnabled);
            Assert.IsFalse(noon.MoonEnabled);
            Assert.Greater(noon.SunIntensity, 1.2f);
            var night = p.Evaluate(22f);
            Assert.IsFalse(night.SunEnabled);
            Assert.IsTrue(night.MoonEnabled);
            Assert.AreEqual(0.15f, night.MoonIntensity, 1e-4f);
        }

        [Test]
        public void Fog_DayDuskNightAndHorror()
        {
            Assert.AreEqual(0.004f, p.Evaluate(12f).FogDensity, 1e-4f);
            Assert.AreEqual(0.032f, p.Evaluate(22f).FogDensity, 1e-4f);
            Assert.That(p.Evaluate(18.25f).FogDensity, Is.InRange(0.004f, 0.032f));
        }

        [Test]
        public void SkyExposure_DayAndNight()
        {
            Assert.AreEqual(1.3f, p.Evaluate(12f).SkyExposure, 1e-3f);
            Assert.AreEqual(0.08f, p.Evaluate(22f).SkyExposure, 1e-3f);
        }

        [Test]
        public void DayVolumeWeight_FullDayZeroNight()
        {
            Assert.AreEqual(1f, p.Evaluate(12f).DayVolumeWeight, 1e-4f);
            Assert.AreEqual(0f, p.Evaluate(22f).DayVolumeWeight, 1e-4f);
        }

        [TestCase(18.99f, 0f)]
        [TestCase(19.25f, 0.5f)]
        [TestCase(22f, 1f)]
        [TestCase(4.75f, 0.5f)]
        [TestCase(5f, 0f)]
        [TestCase(12f, 0f)]
        public void HorrorWeight_RampsOverHalfAnHour(float hours, float expected) =>
            Assert.AreEqual(expected, p.HorrorWeight(hours), 1e-3f);

        [Test]
        public void Lamps_OffByDay_OnAtNight_RisingAtDusk()
        {
            Assert.AreEqual(0f, p.Evaluate(12f).LampIntensity01, 1e-4f);
            Assert.AreEqual(0f, p.Evaluate(17f).LampIntensity01, 1e-4f);
            Assert.AreEqual(1f, p.Evaluate(22f).LampIntensity01, 1e-4f);
            float previous = 0f;
            for (float h = 17.5f; h <= 18.5f; h += 0.05f)
            {
                float v = p.Evaluate(h).LampIntensity01;
                Assert.GreaterOrEqual(v + 1e-5f, previous, h.ToString());
                previous = v;
            }
            Assert.AreEqual(1f, previous, 1e-4f);
        }

        [Test]
        public void Ambience_CrossfadesAtDuskAndDawn()
        {
            var noon = p.Evaluate(12f);
            Assert.AreEqual(1f, noon.DayAmbience, 1e-4f);
            Assert.AreEqual(0f, noon.NightAmbience, 1e-4f);
            var night = p.Evaluate(22f);
            Assert.AreEqual(0f, night.DayAmbience, 1e-4f);
            Assert.AreEqual(1f, night.NightAmbience, 1e-4f);
            Assert.AreEqual(1f, night.HorrorDrone, 1e-4f);
            Assert.AreEqual(0.5f, p.Evaluate(18f).DayAmbience, 1e-4f);
            Assert.AreEqual(0.5f, p.Evaluate(5.5f).DayAmbience, 1e-4f);
        }
    }
}
```

- [ ] **Step 2: Jalankan, pastikan gagal**

Procedure R+T dengan `Group = "DayCycleProfileTests"`. Expected: `COMPILE FAILED` (`CS0246 DayCycleProfile`).

- [ ] **Step 3: Implementasi**

`Assets/DayCycle/Runtime/Profile/LightingState.cs`:
```csharp
using UnityEngine;

namespace TycoWorld.DayCycle
{
    /// <summary>Everything the director applies for one moment of the day (pure data, testable without a scene).</summary>
    public struct LightingState
    {
        public float SunElevation, SunYaw, SunIntensity;
        public Color SunColor;
        public bool SunEnabled;
        public float MoonIntensity;
        public bool MoonEnabled;
        public Color AmbientSky, AmbientEquator, AmbientGround;
        public float FogDensity;
        public Color FogColor;
        public float SkyExposure;
        public Color SkyTint;
        public float DayVolumeWeight, HorrorWeight;
        public float LampIntensity01;
        public float DayAmbience, NightAmbience, HorrorDrone;
    }
}
```

`Assets/DayCycle/Runtime/Profile/DayCycleProfile.cs`:
```csharp
using UnityEngine;

namespace TycoWorld.DayCycle
{
    /// <summary>All look values of the day cycle (spec #3 §4); Evaluate turns a game hour into a LightingState.</summary>
    [CreateAssetMenu(menuName = "Tyco World/Day Cycle Profile", fileName = "DayCycleProfile")]
    public sealed class DayCycleProfile : ScriptableObject
    {
        [Header("Sun & moon")]
        public float sunrise = 5.75f;
        public float sunset = 17.75f;
        public float sunMaxElevation = 75f;
        public float sunIntensity = 1.3f;
        public Color sunHorizonColor = new Color(1f, 0.69f, 0.44f);
        public Color sunNoonColor = new Color(1f, 0.957f, 0.878f);
        public float moonIntensity = 0.15f;
        public Color moonColor = new Color(0.616f, 0.706f, 1f);

        [Header("Sky")]
        public float skyExposureDay = 1.3f;
        public float skyExposureDusk = 0.8f;
        public float skyExposureNight = 0.08f;
        public Color skyTintDay = new Color(0.5f, 0.5f, 0.5f);
        public Color skyTintNight = new Color(0.2f, 0.25f, 0.4f);

        [Header("Ambient (Trilight)")]
        public Color ambientSkyDay = new Color(0.624f, 0.722f, 0.847f);
        public Color ambientEquatorDay = new Color(0.541f, 0.604f, 0.659f);
        public Color ambientGroundDay = new Color(0.353f, 0.314f, 0.251f);
        public Color ambientSkyDusk = new Color(0.69f, 0.478f, 0.549f);
        public Color ambientEquatorDusk = new Color(0.753f, 0.541f, 0.416f);
        public Color ambientGroundDusk = new Color(0.227f, 0.188f, 0.188f);
        public Color ambientSkyNight = new Color(0.071f, 0.094f, 0.149f);
        public Color ambientEquatorNight = new Color(0.055f, 0.071f, 0.094f);
        public Color ambientGroundNight = new Color(0.02f, 0.024f, 0.031f);

        [Header("Fog")]
        public float fogDensityDay = 0.004f;
        public float fogDensityDusk = 0.008f;
        public float fogDensityNight = 0.02f;
        public Color fogColorDay = new Color(0.722f, 0.769f, 0.8f);
        public Color fogColorDusk = new Color(0.753f, 0.541f, 0.416f);
        public Color fogColorNight = new Color(0.055f, 0.071f, 0.094f);

        [Header("Horror")]
        public float horrorFadeHours = 0.5f;
        public float horrorFogMultiplier = 1.6f;
        public Color horrorFogColor = new Color(0.027f, 0.035f, 0.047f);
        public float flickerFraction = 0.08f;
        public float deadFraction = 0.03f;

        [Header("Street lamps")]
        public float lampOnBelowElevation = 5f;
        public float lampFadeDegrees = 6f;
        public float lampIntensity = 3f;

        [Header("Ambience")]
        public AudioClip dayClip;
        public AudioClip nightClip;
        public AudioClip horrorDroneClip;
        public float dayVolume = 0.35f;
        public float nightVolume = 0.4f;
        public float droneVolume = 0.3f;

        const float DaylightFullElevation = 15f;
        const float TwilightBand = 12f;
        const float TwilightBlend = 0.7f;

        public static DayCycleProfile CreateDefault() => CreateInstance<DayCycleProfile>();

        /// <summary>Degrees above (positive) or below (negative) the horizon; a sine arc between sunrise and sunset.</summary>
        public float SunElevation(float hours)
        {
            float h = Mathf.Repeat(hours, 24f);
            if (h >= sunrise && h <= sunset) return Mathf.Sin(Mathf.PI * (h - sunrise) / (sunset - sunrise)) * sunMaxElevation;
            float sinceSunset = Mathf.Repeat(h - sunset, 24f), untilSunrise = Mathf.Repeat(sunrise - h, 24f);
            return -Mathf.Min(Mathf.Min(sinceSunset, untilSunrise) * 15f, 60f);
        }

        /// <summary>Light yaw: 270 at sunrise (light travels west from an eastern sun), 0 at noon, 90 at sunset.</summary>
        public float SunYaw(float hours)
        {
            float t = Mathf.Clamp01((Mathf.Repeat(hours, 24f) - sunrise) / (sunset - sunrise));
            return Mathf.Repeat(270f + 180f * t, 360f);
        }

        public float HorrorWeight(float hours)
        {
            float h = Mathf.Repeat(hours, 24f);
            if (h >= DayTimes.HorrorStart) return Mathf.Clamp01((h - DayTimes.HorrorStart) / horrorFadeHours);
            if (h < DayTimes.HorrorEnd) return Mathf.Clamp01((DayTimes.HorrorEnd - h) / horrorFadeHours);
            return 0f;
        }

        /// <summary>Day ambience: full 06:00–17:30, crossfades 05:00–06:00 and 17:30–18:30.</summary>
        public static float DayAmbienceWeight(float hours)
        {
            float h = Mathf.Repeat(hours, 24f);
            if (h >= 6f && h <= 17.5f) return 1f;
            if (h > 5f && h < 6f) return h - 5f;
            if (h > 17.5f && h < 18.5f) return 1f - (h - 17.5f);
            return 0f;
        }

        public LightingState Evaluate(float hours)
        {
            float h = Mathf.Repeat(hours, 24f);
            float elevation = SunElevation(h);
            float daylight = Mathf.Clamp01(elevation / DaylightFullElevation);
            float twilight = (1f - Mathf.Clamp01(Mathf.Abs(elevation) / TwilightBand)) * TwilightBlend;
            float horror = HorrorWeight(h);
            var s = new LightingState
            {
                SunElevation = elevation,
                SunYaw = SunYaw(h),
                SunEnabled = elevation > 0f,
                SunColor = Color.Lerp(sunHorizonColor, sunNoonColor, Mathf.Clamp01(elevation / 30f)),
                AmbientSky = Mix(ambientSkyNight, ambientSkyDay, ambientSkyDusk, daylight, twilight),
                AmbientEquator = Mix(ambientEquatorNight, ambientEquatorDay, ambientEquatorDusk, daylight, twilight),
                AmbientGround = Mix(ambientGroundNight, ambientGroundDay, ambientGroundDusk, daylight, twilight),
                FogDensity = Mix(fogDensityNight, fogDensityDay, fogDensityDusk, daylight, twilight) * Mathf.Lerp(1f, horrorFogMultiplier, horror),
                FogColor = Color.Lerp(Mix(fogColorNight, fogColorDay, fogColorDusk, daylight, twilight), horrorFogColor, horror * 0.6f),
                SkyExposure = Mix(skyExposureNight, skyExposureDay, skyExposureDusk, daylight, twilight),
                SkyTint = Color.Lerp(skyTintNight, skyTintDay, daylight),
                DayVolumeWeight = daylight,
                HorrorWeight = horror,
                LampIntensity01 = Mathf.Clamp01((lampOnBelowElevation - elevation) / lampFadeDegrees),
                DayAmbience = DayAmbienceWeight(h),
                HorrorDrone = horror
            };
            s.SunIntensity = s.SunEnabled ? sunIntensity * Mathf.Clamp01(elevation / 10f) : 0f;
            s.MoonEnabled = !s.SunEnabled;
            s.MoonIntensity = s.MoonEnabled ? moonIntensity * Mathf.Clamp01(-elevation / 6f) : 0f;
            s.NightAmbience = 1f - s.DayAmbience;
            return s;
        }

        static float Mix(float night, float day, float dusk, float daylight, float twilight) =>
            Mathf.Lerp(Mathf.Lerp(night, day, daylight), dusk, twilight);

        static Color Mix(Color night, Color day, Color dusk, float daylight, float twilight) =>
            Color.Lerp(Color.Lerp(night, day, daylight), dusk, twilight);
    }
}
```

- [ ] **Step 4: Jalankan, pastikan lulus**

Procedure R+T dengan `Group = "DayCycleProfileTests"`. Expected: `passed=15 failed=0`.

- [ ] **Step 5: Commit**

Procedure C:
- path: `Assets/DayCycle`
- subject: `feat(daycycle): day cycle profile and lighting state evaluation`

---

### Task 4: `LampFlicker`

**Files:**
- Create: `Assets/DayCycle/Runtime/Lamps/LampFlicker.cs`
- Test: `Assets/DayCycle/Tests/LampFlickerTests.cs`

**Interfaces:**
- Produces:
  - `enum LampRole { Normal, Flicker, Dead }`.
  - `LampFlicker` dengan `SegmentSeconds` (0,35), `Seed(Vector3)` → uint, `RoleFor(uint seed, float flickerFraction, float deadFraction)` → `LampRole`, dan `IsLit(uint seed, float timeSeconds)` → bool (celah padam 0,05–0,3 s, tidak pernah menyentuh batas segmen).

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/DayCycle/Tests/LampFlickerTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.DayCycle.Tests
{
    public class LampFlickerTests
    {
        [Test]
        public void Seed_IsDeterministicPerPosition()
        {
            var a = new Vector3(-321.2f, 0f, -622.4f);
            Assert.AreEqual(LampFlicker.Seed(a), LampFlicker.Seed(a));
            Assert.AreNotEqual(LampFlicker.Seed(a), LampFlicker.Seed(a + new Vector3(3f, 0f, 0f)));
        }

        [Test]
        public void Roles_MatchFractions()
        {
            int dead = 0, flicker = 0, total = 0;
            for (int x = 0; x < 64; x++)
            for (int z = 0; z < 63; z++)
            {
                var role = LampFlicker.RoleFor(LampFlicker.Seed(new Vector3(x * 3.7f - 900f, 0f, z * 4.1f - 1300f)), 0.08f, 0.03f);
                if (role == LampRole.Dead) dead++;
                else if (role == LampRole.Flicker) flicker++;
                total++;
            }
            Assert.AreEqual(0.03f, (float)dead / total, 0.01f);
            Assert.AreEqual(0.08f, (float)flicker / total, 0.015f);
        }

        [Test]
        public void IsLit_IsDeterministic_AndHasBothStates()
        {
            uint seed = LampFlicker.Seed(new Vector3(10f, 0f, 20f));
            int lit = 0, off = 0;
            for (float t = 0f; t < 30f; t += 0.01f)
            {
                bool a = LampFlicker.IsLit(seed, t);
                Assert.AreEqual(a, LampFlicker.IsLit(seed, t));
                if (a) lit++; else off++;
            }
            Assert.Greater(lit, 0);
            Assert.Greater(off, 0);
        }

        [Test]
        public void OffGaps_AreShort()
        {
            uint seed = LampFlicker.Seed(new Vector3(-5f, 0f, 7f));
            float longest = 0f, run = 0f;
            const float step = 0.005f;
            for (float t = 0f; t < 60f; t += step)
            {
                if (LampFlicker.IsLit(seed, t)) run = 0f;
                else
                {
                    run += step;
                    longest = Mathf.Max(longest, run);
                }
            }
            Assert.LessOrEqual(longest, 0.305f);
            Assert.Greater(longest, 0.04f);
        }
    }
}
```

- [ ] **Step 2: Jalankan, pastikan gagal**

Procedure R+T dengan `Group = "LampFlickerTests"`. Expected: `COMPILE FAILED` (`CS0103 LampFlicker`).

- [ ] **Step 3: Implementasi**

`Assets/DayCycle/Runtime/Lamps/LampFlicker.cs`:
```csharp
using UnityEngine;

namespace TycoWorld.DayCycle
{
    public enum LampRole { Normal, Flicker, Dead }

    /// <summary>Deterministic per-lamp horror behaviour: the same lamps flicker or die every night.</summary>
    public static class LampFlicker
    {
        public const float SegmentSeconds = 0.35f;

        public static uint Seed(Vector3 position)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)Mathf.RoundToInt(position.x * 10f)) * 16777619u;
                h = (h ^ (uint)Mathf.RoundToInt(position.z * 10f)) * 16777619u;
                return Avalanche(h);
            }
        }

        public static LampRole RoleFor(uint seed, float flickerFraction, float deadFraction)
        {
            float u = Unit(seed);
            if (u < deadFraction) return LampRole.Dead;
            return u < deadFraction + flickerFraction ? LampRole.Flicker : LampRole.Normal;
        }

        /// <summary>Half of the 0.35 s segments contain one 0.05–0.3 s dark gap that never touches a segment edge.</summary>
        public static bool IsLit(uint seed, float timeSeconds)
        {
            int segment = Mathf.FloorToInt(timeSeconds / SegmentSeconds);
            uint s = Avalanche(unchecked(seed ^ ((uint)segment * 0x9E3779B9u)));
            if ((s & 1u) == 0u) return true;
            float offLength = 0.05f + 0.25f * Unit(s >> 1);
            float start = 0.02f + (SegmentSeconds - offLength - 0.04f) * Unit(Avalanche(s));
            float local = timeSeconds - segment * SegmentSeconds;
            return local < start || local >= start + offLength;
        }

        static uint Avalanche(uint h)
        {
            unchecked
            {
                h ^= h >> 16;
                h *= 0x7feb352du;
                h ^= h >> 15;
                h *= 0x846ca68bu;
                h ^= h >> 16;
                return h;
            }
        }

        static float Unit(uint h) => (h & 0xFFFFFFu) / 16777216f;
    }
}
```

- [ ] **Step 4: Jalankan, pastikan lulus**

Procedure R+T dengan `Group = "LampFlickerTests"`. Expected: `passed=4 failed=0`.

- [ ] **Step 5: Commit**

Procedure C:
- path: `Assets/DayCycle`
- subject: `feat(daycycle): deterministic street lamp flicker roles`

---

### Task 5: `DebugTimeControls`

**Files:**
- Create: `Assets/DayCycle/Runtime/Debug/DebugTimeControls.cs`
- Test: `Assets/DayCycle/Tests/DebugTimeControlsTests.cs`

**Interfaces:**
- Consumes: `WorldClock`, `DayTimes`.
- Produces:
  - `struct DebugKeys { bool JumpTime, SlowerSpeed, FasterSpeed, ToggleClock; }`.
  - `DebugTimeControls` dengan `Speed`, `ClockVisible`, `LastMessage`, dan `Apply(DebugKeys, WorldClock)` → bool.

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/DayCycle/Tests/DebugTimeControlsTests.cs`:
```csharp
using NUnit.Framework;

namespace TycoWorld.DayCycle.Tests
{
    public class DebugTimeControlsTests
    {
        static readonly DebugKeys T = new DebugKeys { JumpTime = true };
        static readonly DebugKeys Faster = new DebugKeys { FasterSpeed = true };
        static readonly DebugKeys Slower = new DebugKeys { SlowerSpeed = true };

        [Test]
        public void T_FromDay_JumpsTo1930()
        {
            var c = new WorldClock(12f);
            var d = new DebugTimeControls();
            Assert.IsTrue(d.Apply(T, c));
            Assert.AreEqual(19.5f, c.Hours, 1e-4f);
            Assert.AreEqual("Lompat ke 19:30", d.LastMessage);
        }

        [TestCase(19.5f)]
        [TestCase(3f)]
        [TestCase(23f)]
        public void T_FromNight_JumpsTo0630(float start)
        {
            var c = new WorldClock(start);
            var d = new DebugTimeControls();
            d.Apply(T, c);
            Assert.AreEqual(6.5f, c.Hours, 1e-4f);
            Assert.AreEqual("Lompat ke 06:30", d.LastMessage);
        }

        [Test]
        public void Faster_Cycles_1_10_60_1_AndSetsClock()
        {
            var c = new WorldClock(12f);
            var d = new DebugTimeControls();
            Assert.AreEqual(1f, d.Speed);
            d.Apply(Faster, c);
            Assert.AreEqual(10f, d.Speed);
            Assert.AreEqual(10f, c.SpeedMultiplier);
            Assert.AreEqual("Kecepatan waktu x10", d.LastMessage);
            d.Apply(Faster, c);
            Assert.AreEqual(60f, d.Speed);
            d.Apply(Faster, c);
            Assert.AreEqual(1f, d.Speed);
            Assert.AreEqual("Kecepatan waktu x1", d.LastMessage);
        }

        [Test]
        public void Slower_Cycles_1_60_10()
        {
            var c = new WorldClock(12f);
            var d = new DebugTimeControls();
            d.Apply(Slower, c);
            Assert.AreEqual(60f, d.Speed);
            d.Apply(Slower, c);
            Assert.AreEqual(10f, d.Speed);
        }

        [Test]
        public void F3_TogglesClock()
        {
            var c = new WorldClock(12f);
            var d = new DebugTimeControls();
            d.Apply(new DebugKeys { ToggleClock = true }, c);
            Assert.IsTrue(d.ClockVisible);
            d.Apply(new DebugKeys { ToggleClock = true }, c);
            Assert.IsFalse(d.ClockVisible);
        }

        [Test]
        public void NoKeys_ChangesNothing()
        {
            var c = new WorldClock(12f);
            var d = new DebugTimeControls();
            Assert.IsFalse(d.Apply(default, c));
            Assert.AreEqual(12f, c.Hours, 1e-5f);
            Assert.AreEqual("", d.LastMessage);
        }
    }
}
```

- [ ] **Step 2: Jalankan, pastikan gagal**

Procedure R+T dengan `Group = "DebugTimeControlsTests"`. Expected: `COMPILE FAILED` (`CS0246 DebugKeys`, `DebugTimeControls`).

- [ ] **Step 3: Implementasi**

`Assets/DayCycle/Runtime/Debug/DebugTimeControls.cs`:
```csharp
using System.Globalization;

namespace TycoWorld.DayCycle
{
    public struct DebugKeys
    {
        public bool JumpTime, SlowerSpeed, FasterSpeed, ToggleClock;
    }

    /// <summary>Debug time controls: T jumps 06:30 ↔ 19:30, [ / ] cycle the speed (x1, x10, x60), F3 toggles the HUD clock.</summary>
    public sealed class DebugTimeControls
    {
        static readonly float[] Speeds = { 1f, 10f, 60f };
        int speedIndex;

        public float Speed => Speeds[speedIndex];
        public bool ClockVisible { get; private set; }
        public string LastMessage { get; private set; } = "";

        public bool Apply(DebugKeys keys, WorldClock clock)
        {
            LastMessage = "";
            bool changed = false;
            if (keys.JumpTime)
            {
                bool daytime = clock.Hours >= DayTimes.DebugMorning && clock.Hours < DayTimes.DebugNight;
                clock.SetTime(daytime ? DayTimes.DebugNight : DayTimes.DebugMorning);
                LastMessage = "Lompat ke " + clock.Display;
                changed = true;
            }
            if (keys.FasterSpeed || keys.SlowerSpeed)
            {
                int step = keys.FasterSpeed ? 1 : -1;
                speedIndex = (speedIndex + step + Speeds.Length) % Speeds.Length;
                clock.SpeedMultiplier = Speed;
                LastMessage = "Kecepatan waktu x" + Speed.ToString("0", CultureInfo.InvariantCulture);
                changed = true;
            }
            if (keys.ToggleClock)
            {
                ClockVisible = !ClockVisible;
                changed = true;
            }
            return changed;
        }
    }
}
```

- [ ] **Step 4: Jalankan, pastikan lulus**

Procedure R+T dengan `Group = "DebugTimeControlsTests"`. Expected: `passed=8 failed=0`.

Lalu `Group = "."`. Expected: `failed=0`.

- [ ] **Step 5: Commit**

Procedure C:
- path: `Assets/DayCycle`
- subject: `feat(daycycle): debug time controls`

---
### Task 6: Penanda `StreetLamp` di prefab lampu (WorldGen)

**Files:**
- Create: `Assets/WorldGen/Runtime/StreetLamp.cs`
- Modify: `Assets/WorldGen/Editor/Art/PlaceholderPrefabFactory.cs` (dekorasi `StreetLamp_A`, `AddLampLight` mengembalikan `Light`)
- Test: `Assets/WorldGen/Tests/StreetLampPrefabTests.cs`
- Regenerate: `Assets/WorldGen/Prefabs/StreetLamp_A.prefab` (lewat Build Art Assets)

**Interfaces:**
- Produces: `TycoWorld.WorldGen.StreetLamp : MonoBehaviour` dengan `Light Light`, `Renderer HeadRenderer`, `int HeadMaterialIndex`, dan `Initialize(Light, Renderer, int)`.
- Di prefab: `Light` = anak "Light", `HeadRenderer` = MeshRenderer root, `HeadMaterialIndex` = 1 (slot `M_Lamp_Head`).

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/WorldGen/Tests/StreetLampPrefabTests.cs`:
```csharp
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class StreetLampPrefabTests
    {
        [Test]
        public void LampPrefab_HasStreetLampMarker()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WorldGenPaths.PrefabsFolder + "/StreetLamp_A.prefab");
            Assert.IsNotNull(prefab, "Jalankan Build Art Assets dulu.");
            var lamp = prefab.GetComponent<StreetLamp>();
            Assert.IsNotNull(lamp, "StreetLamp_A belum punya StreetLamp.");
            Assert.IsNotNull(lamp.Light);
            Assert.AreEqual(LightType.Point, lamp.Light.type);
            Assert.AreNotSame(prefab.transform, lamp.Light.transform);
            Assert.IsTrue(lamp.Light.transform.IsChildOf(prefab.transform));
            Assert.AreSame(prefab.GetComponent<MeshRenderer>(), lamp.HeadRenderer);
            Assert.AreEqual("M_Lamp_Head", lamp.HeadRenderer.sharedMaterials[lamp.HeadMaterialIndex].name);
        }
    }
}
```

- [ ] **Step 2: Jalankan, pastikan gagal**

Procedure R+T dengan `Group = "StreetLampPrefabTests"`. Expected: `COMPILE FAILED` (`CS0246 StreetLamp`).

- [ ] **Step 3: Tambah komponen penanda, lalu jalankan lagi**

`Assets/WorldGen/Runtime/StreetLamp.cs`:
```csharp
using UnityEngine;

namespace TycoWorld.WorldGen
{
    /// <summary>Marks a street lamp for the day cycle: its point light and the renderer slot of the glowing head.</summary>
    [DisallowMultipleComponent]
    public sealed class StreetLamp : MonoBehaviour
    {
        [SerializeField] Light lampLight;
        [SerializeField] Renderer headRenderer;
        [SerializeField] int headMaterialIndex;

        public Light Light => lampLight;
        public Renderer HeadRenderer => headRenderer;
        public int HeadMaterialIndex => headMaterialIndex;

        public void Initialize(Light light, Renderer renderer, int materialIndex)
        {
            lampLight = light;
            headRenderer = renderer;
            headMaterialIndex = materialIndex;
        }
    }
}
```

Procedure R+T dengan `Group = "StreetLampPrefabTests"`. Expected: `failed=1`, pesan `StreetLamp_A belum punya StreetLamp.` (RED yang benar: prefab lama belum dipasangi penanda).

- [ ] **Step 4: Pasang penanda di factory**

`Assets/WorldGen/Editor/Art/PlaceholderPrefabFactory.cs`. Ganti blok `Lamp = SavePrefab(...)`:
```csharp
                Lamp = SavePrefab("StreetLamp_A", lamp, new[] { metal, head }, go =>
                {
                    AddCapsule(go, 0.1f, 6f);
                    AddLampLight(go);
                })
```
menjadi
```csharp
                Lamp = SavePrefab("StreetLamp_A", lamp, new[] { metal, head }, go =>
                {
                    AddCapsule(go, 0.1f, 6f);
                    var light = AddLampLight(go);
                    go.AddComponent<StreetLamp>().Initialize(light, go.GetComponent<MeshRenderer>(), 1);
                })
```
Lalu ganti tanda tangan dan akhir `AddLampLight`:
```csharp
        static void AddLampLight(GameObject lamp)
```
menjadi
```csharp
        static Light AddLampLight(GameObject lamp)
```
dan tambahkan baris `return light;` setelah `light.shadows = LightShadows.None;`.

- [ ] **Step 5: Bangun ulang art assets, lalu jalankan tes**

Snippet MCP:
```csharp
using UnityEditor;
using UnityEditor.SceneManagement;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        if (EditorApplication.isCompiling) { result.LogError("STILL COMPILING"); return; }
        EditorSceneManager.SaveOpenScenes();
        TycoWorld.WorldGen.Authoring.ArtAssetBuilder.BuildAll();
        var prefab = AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/WorldGen/Prefabs/StreetLamp_A.prefab");
        result.Log("marker=" + (prefab.GetComponent<TycoWorld.WorldGen.StreetLamp>() != null));
    }
}
```
Expected: `marker=True`.

Procedure R+T dengan `Group = "StreetLampPrefabTests"`. Expected: `passed=1 failed=0`.

- [ ] **Step 6: Generate ulang dan bandingkan hash**

Snippet MCP:
```csharp
using TycoWorld.WorldGen;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

internal class CommandScript : IRunCommand
{
    const string Dir = "D:/UNITY/project/Tyco World/.superpowers/sdd/2026-10-13-day-night-cycle";

    public void Execute(ExecutionResult result)
    {
        EditorSceneManager.SaveOpenScenes();
        var settings = AssetDatabase.LoadAssetAtPath<WorldGenSettings>(WorldGenPaths.SettingsAsset);
        if (!WorldGenerationRunner.Generate(settings, false, out string error)) { result.LogError(error); return; }
        result.Log(TycoWorld.WorldGen.Tests.McpVerify.CompareChunkHashes(Dir + "/hashes_before.txt"));
        var root = Object.FindAnyObjectByType<GeneratedRoot>();
        int marked = root.GetComponentsInChildren<StreetLamp>(true).Length, lights = 0;
        foreach (var l in root.GetComponentsInChildren<Light>(true)) if (l.type == LightType.Point) lights++;
        result.Log("lampsPlaced=" + WorldGenerationRunner.LastReport.LampsPlaced + " marked=" + marked + " pointLights=" + lights);
    }
}
```
Expected:
- `IDENTICAL chunks=278` (komponen tidak ikut di-hash);
- `lampsPlaced=876 marked=876 pointLights=876`.

- [ ] **Step 7: Suite penuh dan commit**

Procedure R+T dengan `Group = "."`. Expected: `failed=0`.

```bash
git -C "D:/UNITY/project/Tyco World" status --short Assets/WorldGen
```
Expected: hanya `StreetLamp.cs` (+ `.meta`), `PlaceholderPrefabFactory.cs`, `StreetLampPrefabTests.cs` (+ `.meta`), dan `Prefabs/StreetLamp_A.prefab`. Kalau asset art lain ikut berubah, periksa `git diff` dulu dan jangan ikut di-add kecuali isinya memang berubah karena task ini.

Procedure C:
- path: `Assets/WorldGen/Runtime/StreetLamp.cs` `Assets/WorldGen/Runtime/StreetLamp.cs.meta` `Assets/WorldGen/Editor/Art/PlaceholderPrefabFactory.cs` `Assets/WorldGen/Tests/StreetLampPrefabTests.cs` `Assets/WorldGen/Tests/StreetLampPrefabTests.cs.meta` `Assets/WorldGen/Prefabs/StreetLamp_A.prefab`
- subject: `feat(worldgen): mark street lamp prefab for the day cycle`

---

### Task 7: `StreetLampSystem`

**Files:**
- Create: `Assets/DayCycle/Runtime/Lamps/StreetLampSystem.cs`
- Test: `Assets/DayCycle/Tests/LampFixture.cs`, `Assets/DayCycle/Tests/StreetLampSystemTests.cs`

**Interfaces:**
- Consumes: `StreetLamp` (Task 6), `LampFlicker` / `LampRole` (Task 4), `DayCycleProfile` (Task 3: `flickerFraction`, `deadFraction`, `lampIntensity`).
- Produces: `StreetLampSystem : IDisposable` dengan:
  - `StreetLampSystem(DayCycleProfile)`;
  - `bool Collected`, `int Count`, `int LitCount`, `int RoleCount(LampRole)`;
  - `void Collect(IReadOnlyList<StreetLamp>)`;
  - `void Apply(float intensity01, float horrorWeight, float timeSeconds)`;
  - `void Dispose()` (memulihkan material asli, menghancurkan salinan; aman dipanggil berulang).
- Produces (tes): `LampFixture.Create(Transform parent, Vector3 position)` → `StreetLamp`, `LampFixture.PositionWithRole(LampRole, DayCycleProfile)` → `Vector3`, `LampFixture.HeadAsset` / `MetalAsset`.

**Desain:** dua salinan material runtime dari `M_Lamp_Head` dipakai bersama semua lampu.
- "On": emisi = emisi asli × intensitas.
- "Off": emisi hitam.
- Tiap lampu menyimpan array material asli, On, dan Off. `sharedMaterials` hanya diganti saat status nyala berubah.
- Asset tidak pernah disentuh.

- [ ] **Step 1: Tulis fixture dan tes yang gagal**

`Assets/DayCycle/Tests/LampFixture.cs`:
```csharp
using TycoWorld.WorldGen;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.DayCycle.Tests
{
    /// <summary>Code-built street lamps that use the real lamp materials, for lamp and director tests.</summary>
    internal static class LampFixture
    {
        public static Material HeadAsset => AssetDatabase.LoadAssetAtPath<Material>(WorldGenPaths.MaterialsFolder + "/M_Lamp_Head.mat");
        public static Material MetalAsset => AssetDatabase.LoadAssetAtPath<Material>(WorldGenPaths.MaterialsFolder + "/M_Lamp_Metal.mat");

        public static StreetLamp Create(Transform parent, Vector3 position)
        {
            var go = new GameObject("TestLamp");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { MetalAsset, HeadAsset };
            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(go.transform, false);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            var lamp = go.AddComponent<StreetLamp>();
            lamp.Initialize(light, renderer, 1);
            return lamp;
        }

        /// <summary>First position along +x (3 m apart, far from the world) whose lamp gets the requested role.</summary>
        public static Vector3 PositionWithRole(LampRole role, DayCycleProfile profile)
        {
            for (int i = 0; i < 10000; i++)
            {
                var p = new Vector3(5000f + i * 3f, 1000f, 5000f);
                if (LampFlicker.RoleFor(LampFlicker.Seed(p), profile.flickerFraction, profile.deadFraction) == role) return p;
            }
            throw new System.InvalidOperationException("Tidak ada posisi dengan peran " + role);
        }
    }
}
```

`Assets/DayCycle/Tests/StreetLampSystemTests.cs`:
```csharp
using System.Collections.Generic;
using NUnit.Framework;
using TycoWorld.WorldGen;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.DayCycle.Tests
{
    public class StreetLampSystemTests
    {
        readonly List<GameObject> created = new List<GameObject>();
        DayCycleProfile profile;
        StreetLampSystem system;

        [SetUp]
        public void SetUp()
        {
            profile = DayCycleProfile.CreateDefault();
            system = new StreetLampSystem(profile);
        }

        [TearDown]
        public void TearDown()
        {
            system.Dispose();
            foreach (var go in created) if (go != null) Object.DestroyImmediate(go);
            created.Clear();
            Object.DestroyImmediate(profile);
        }

        StreetLamp Lamp(LampRole role)
        {
            var lamp = LampFixture.Create(null, LampFixture.PositionWithRole(role, profile));
            created.Add(lamp.gameObject);
            return lamp;
        }

        [Test]
        public void Night_EnablesLight_WithScaledIntensity()
        {
            var lamp = Lamp(LampRole.Normal);
            system.Collect(new[] { lamp });
            system.Apply(0.5f, 0f, 0f);
            Assert.IsTrue(lamp.Light.enabled);
            Assert.AreEqual(1.5f, lamp.Light.intensity, 1e-4f);
            Assert.AreEqual(1, system.LitCount);
        }

        [Test]
        public void Day_DisablesLight_AndDarkensHead()
        {
            var lamp = Lamp(LampRole.Normal);
            system.Collect(new[] { lamp });
            system.Apply(0f, 0f, 0f);
            Assert.IsFalse(lamp.Light.enabled);
            var head = lamp.HeadRenderer.sharedMaterials[1];
            Assert.AreNotSame(LampFixture.HeadAsset, head);
            Assert.AreEqual(Color.black, head.GetColor("_EmissionColor"));
            Assert.AreEqual(0, system.LitCount);
        }

        [Test]
        public void Apply_NeverTouchesSharedHeadMaterial()
        {
            var asset = LampFixture.HeadAsset;
            var before = asset.GetColor("_EmissionColor");
            var lamp = Lamp(LampRole.Normal);
            system.Collect(new[] { lamp });
            system.Apply(1f, 0f, 0f);
            var lit = lamp.HeadRenderer.sharedMaterials[1];
            Assert.AreNotSame(asset, lit);
            Assert.AreEqual(before, lit.GetColor("_EmissionColor"));
            system.Apply(0.25f, 0f, 0f);
            system.Apply(0f, 0f, 0f);
            Assert.AreEqual(before, asset.GetColor("_EmissionColor"));
            Assert.IsFalse(EditorUtility.IsDirty(asset));
            system.Dispose();
            Assert.AreSame(asset, lamp.HeadRenderer.sharedMaterials[1]);
        }

        [Test]
        public void Horror_DeadLampStaysOff_NormalStaysOn()
        {
            var dead = Lamp(LampRole.Dead);
            var normal = Lamp(LampRole.Normal);
            system.Collect(new[] { dead, normal });
            for (float t = 0f; t < 5f; t += 0.05f)
            {
                system.Apply(1f, 1f, t);
                Assert.IsFalse(dead.Light.enabled);
                Assert.IsTrue(normal.Light.enabled);
            }
        }

        [Test]
        public void FlickerLamp_BlinksOnlyDuringHorror()
        {
            var lamp = Lamp(LampRole.Flicker);
            system.Collect(new[] { lamp });
            for (float t = 0f; t < 10f; t += 0.01f)
            {
                system.Apply(1f, 0f, t);
                Assert.IsTrue(lamp.Light.enabled, "tanpa horor lampu tidak boleh berkedip");
            }
            int off = 0, samples = 0;
            for (float t = 0f; t < 10f; t += 0.01f)
            {
                system.Apply(1f, 1f, t);
                samples++;
                if (!lamp.Light.enabled) off++;
            }
            Assert.Greater(off, 0);
            Assert.Less(off, samples / 2);
        }

        [Test]
        public void LampWithoutLight_IsSkipped()
        {
            var go = new GameObject("NoLight");
            created.Add(go);
            var lamp = go.AddComponent<StreetLamp>();
            system.Collect(new[] { lamp });
            Assert.IsTrue(system.Collected);
            Assert.AreEqual(0, system.Count);
            Assert.DoesNotThrow(() => system.Apply(1f, 1f, 0f));
        }

        [Test]
        public void Roles_AreCounted()
        {
            system.Collect(new[] { Lamp(LampRole.Dead), Lamp(LampRole.Flicker), Lamp(LampRole.Normal) });
            Assert.AreEqual(3, system.Count);
            Assert.AreEqual(1, system.RoleCount(LampRole.Dead));
            Assert.AreEqual(1, system.RoleCount(LampRole.Flicker));
            Assert.AreEqual(1, system.RoleCount(LampRole.Normal));
        }
    }
}
```

- [ ] **Step 2: Jalankan, pastikan gagal**

Procedure R+T dengan `Group = "StreetLampSystemTests"`. Expected: `COMPILE FAILED` (`CS0246 StreetLampSystem`).

- [ ] **Step 3: Implementasi**

`Assets/DayCycle/Runtime/Lamps/StreetLampSystem.cs`:
```csharp
using System;
using System.Collections.Generic;
using TycoWorld.WorldGen;
using UnityEngine;

namespace TycoWorld.DayCycle
{
    /// <summary>
    /// Switches the generated street lamps with the day cycle: light on/off and intensity, head emission through two
    /// runtime material copies (the shared M_Lamp_Head asset is never modified), and horror flicker/dead lamps.
    /// </summary>
    public sealed class StreetLampSystem : IDisposable
    {
        sealed class Entry
        {
            public StreetLamp Lamp;
            public uint Seed;
            public LampRole Role;
            public Material[] Original, On, Off;
            public int Shown = -1;
        }

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        readonly DayCycleProfile profile;
        readonly List<Entry> entries = new List<Entry>();
        Material headOn, headOff;
        Color baseEmission;

        public StreetLampSystem(DayCycleProfile profile) => this.profile = profile;

        public bool Collected { get; private set; }
        public int Count => entries.Count;
        public int LitCount { get; private set; }

        public int RoleCount(LampRole role)
        {
            int n = 0;
            foreach (var e in entries)
                if (e.Role == role) n++;
            return n;
        }

        public void Collect(IReadOnlyList<StreetLamp> lamps)
        {
            Collected = true;
            foreach (var lamp in lamps)
            {
                if (lamp == null || lamp.Light == null) continue;
                var entry = new Entry { Lamp = lamp, Seed = LampFlicker.Seed(lamp.transform.position) };
                entry.Role = LampFlicker.RoleFor(entry.Seed, profile.flickerFraction, profile.deadFraction);
                var renderer = lamp.HeadRenderer;
                int index = lamp.HeadMaterialIndex;
                if (renderer != null && index >= 0 && index < renderer.sharedMaterials.Length && renderer.sharedMaterials[index] != null)
                {
                    entry.Original = renderer.sharedMaterials;
                    EnsureCopies(entry.Original[index]);
                    entry.On = Replace(entry.Original, index, headOn);
                    entry.Off = Replace(entry.Original, index, headOff);
                }
                entries.Add(entry);
            }
        }

        public void Apply(float intensity01, float horrorWeight, float timeSeconds)
        {
            if (headOn != null) headOn.SetColor(EmissionId, baseEmission * intensity01);
            bool horror = horrorWeight > 0f;
            int lit = 0;
            foreach (var e in entries)
            {
                if (e.Lamp == null) continue;
                bool on = intensity01 > 0f;
                if (on && horror && e.Role == LampRole.Dead) on = false;
                if (on && horror && e.Role == LampRole.Flicker) on = LampFlicker.IsLit(e.Seed, timeSeconds);
                var light = e.Lamp.Light;
                if (light != null)
                {
                    light.enabled = on;
                    light.intensity = profile.lampIntensity * intensity01;
                }
                int shown = on ? 1 : 0;
                if (e.On != null && e.Shown != shown && e.Lamp.HeadRenderer != null)
                {
                    e.Lamp.HeadRenderer.sharedMaterials = on ? e.On : e.Off;
                    e.Shown = shown;
                }
                if (on) lit++;
            }
            LitCount = lit;
        }

        public void Dispose()
        {
            foreach (var e in entries)
                if (e.Lamp != null && e.Original != null && e.Lamp.HeadRenderer != null)
                    e.Lamp.HeadRenderer.sharedMaterials = e.Original;
            entries.Clear();
            LitCount = 0;
            DestroyCopy(ref headOn);
            DestroyCopy(ref headOff);
        }

        void EnsureCopies(Material source)
        {
            if (headOn != null) return;
            baseEmission = source.HasProperty(EmissionId) ? source.GetColor(EmissionId) : Color.black;
            headOn = new Material(source) { name = source.name + " (Runtime On)", hideFlags = HideFlags.DontSave };
            headOff = new Material(source) { name = source.name + " (Runtime Off)", hideFlags = HideFlags.DontSave };
            headOff.SetColor(EmissionId, Color.black);
        }

        static Material[] Replace(Material[] original, int index, Material material)
        {
            var copy = (Material[])original.Clone();
            copy[index] = material;
            return copy;
        }

        static void DestroyCopy(ref Material material)
        {
            if (material == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(material);
            else UnityEngine.Object.DestroyImmediate(material);
            material = null;
        }
    }
}
```

- [ ] **Step 4: Jalankan, pastikan lulus**

Procedure R+T dengan `Group = "StreetLampSystemTests"`. Expected: `passed=7 failed=0`.

Lalu jalankan `git status --short Assets/WorldGen/Materials`. Expected: kosong (asset tidak berubah).

- [ ] **Step 5: Commit**

Procedure C:
- path: `Assets/DayCycle`
- subject: `feat(daycycle): street lamp system with runtime head materials and horror flicker`

---

### Task 8: Label jam di HUD pemain

**Files:**
- Modify: `Assets/Player/Runtime/UI/HudModel.cs`, `Assets/Player/Runtime/UI/HudView.cs`
- Test: `Assets/Player/Tests/HudModelTests.cs`, `Assets/Player/Tests/HudViewTests.cs`

**Interfaces:**
- Produces:
  - `HudModel.Clock` (string, default `""`), `HudModel.ClockVisible` (bool, default false), `HudModel.SetClock(string text, bool visible)`;
  - `HudView.ClockName = "clock"`: label kanan atas, tampil hanya kalau `ClockVisible` dan teks tidak kosong.

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/Player/Tests/HudModelTests.cs`: tambahkan di dalam kelas (setelah `Message_ExpiresAfterSeconds`):
```csharp
        [Test]
        public void Clock_HiddenByDefault_AndSettable()
        {
            var m = Make();
            Assert.AreEqual("", m.Clock);
            Assert.IsFalse(m.ClockVisible);
            m.SetClock("19:30", true);
            Assert.AreEqual("19:30", m.Clock);
            Assert.IsTrue(m.ClockVisible);
            m.SetClock(null, false);
            Assert.AreEqual("", m.Clock);
            Assert.IsFalse(m.ClockVisible);
        }
```

`Assets/Player/Tests/HudViewTests.cs`: di `Build_CreatesNamedElements`, ganti array nama menjadi
```csharp
            foreach (var name in new[] { HudView.CrosshairName, HudView.PromptName, HudView.MessageName, HudView.StaminaName, HudView.StaminaFillName, HudView.ClockName })
```
lalu tambahkan tes:
```csharp
        [Test]
        public void Render_ClockTopRight_OnlyWhenVisible()
        {
            var (root, view, model) = Make();
            view.Render(model);
            var clock = root.Q<Label>(HudView.ClockName);
            Assert.AreEqual(DisplayStyle.None, clock.style.display.value);
            model.SetClock("06:30", true);
            view.Render(model);
            Assert.AreEqual("06:30", clock.text);
            Assert.AreEqual(DisplayStyle.Flex, clock.style.display.value);
            Assert.AreEqual(TextAnchor.MiddleRight, clock.style.unityTextAlign.value);
            model.SetClock("06:30", false);
            view.Render(model);
            Assert.AreEqual(DisplayStyle.None, clock.style.display.value);
        }
```

- [ ] **Step 2: Jalankan, pastikan gagal**

Procedure R+T dengan `Group = "HudModelTests|HudViewTests"`. Expected: `COMPILE FAILED` (`CS1061 Clock` / `SetClock`, `CS0117 ClockName`).

- [ ] **Step 3: Implementasi**

`HudModel.cs`. Setelah baris `public bool CrosshairVisible { get; set; } = true;` tambahkan:
```csharp
        public string Clock { get; private set; } = "";
        public bool ClockVisible { get; private set; }
```
Setelah method `SetPrompt` tambahkan:
```csharp
        public void SetClock(string text, bool visible)
        {
            Clock = text ?? "";
            ClockVisible = visible;
        }
```

`HudView.cs`:
1. Setelah `public const string StaminaFillName = "stamina-fill";` tambahkan `public const string ClockName = "clock";`.
2. Ganti `readonly Label prompt, message;` menjadi `readonly Label prompt, message, clock;`.
3. Ubah ringkasan kelas menjadi `/// <summary>Code-built UI Toolkit HUD: crosshair dot, interaction prompt, message line, stamina bar, debug clock.</summary>`.
4. Setelah `staminaBar.Add(staminaFill);` tambahkan:
```csharp
            clock = new Label { name = ClockName, pickingMode = PickingMode.Ignore };
            Absolute(clock);
            clock.style.top = 16f;
            clock.style.right = 24f;
            clock.style.unityTextAlign = TextAnchor.MiddleRight;
            clock.style.fontSize = 22f;
            clock.style.color = new Color(0.92f, 0.92f, 0.88f, 0.9f);
            clock.style.display = DisplayStyle.None;
```
5. Setelah `root.Add(staminaBar);` tambahkan `root.Add(clock);`.
6. Di akhir `Render`, tambahkan:
```csharp
            clock.text = model.Clock;
            clock.style.display = model.ClockVisible && model.Clock.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
```

- [ ] **Step 4: Jalankan, pastikan lulus**

Procedure R+T dengan `Group = "HudModelTests|HudViewTests"`. Expected: `failed=0`, dengan dua tes baru ikut lulus.

- [ ] **Step 5: Commit**

Procedure C:
- path: `Assets/Player/Runtime/UI/HudModel.cs` `Assets/Player/Runtime/UI/HudView.cs` `Assets/Player/Tests/HudModelTests.cs` `Assets/Player/Tests/HudViewTests.cs`
- subject: `feat(player): HUD clock label for the day cycle`

---

### Task 9: `AmbienceAudio`

**Files:**
- Create: `Assets/DayCycle/Runtime/Audio/AmbienceAudio.cs`
- Test: `Assets/DayCycle/Tests/AmbienceAudioTests.cs`

**Interfaces:**
- Consumes: `DayCycleProfile` (`dayClip`, `nightClip`, `horrorDroneClip`, `dayVolume`, `nightVolume`, `droneVolume`, `Evaluate`).
- Produces: `AmbienceAudio : MonoBehaviour` dengan:
  - konstanta `DayName`, `NightName`, `DroneName`;
  - `AudioSource Day`, `Night`, `Drone`;
  - `Initialize(DayCycleProfile)` (membuat atau memakai ulang tiga anak);
  - `Apply(float dayWeight, float nightWeight, float droneWeight)`.

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/DayCycle/Tests/AmbienceAudioTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.DayCycle.Tests
{
    public class AmbienceAudioTests
    {
        GameObject go;
        DayCycleProfile profile;
        AmbienceAudio ambience;

        [SetUp]
        public void SetUp()
        {
            profile = DayCycleProfile.CreateDefault();
            profile.dayClip = AudioClip.Create("day", 2205, 1, 22050, false);
            profile.nightClip = AudioClip.Create("night", 2205, 1, 22050, false);
            profile.horrorDroneClip = AudioClip.Create("drone", 2205, 1, 22050, false);
            go = new GameObject("Ambience");
            ambience = go.AddComponent<AmbienceAudio>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(profile.dayClip);
            Object.DestroyImmediate(profile.nightClip);
            Object.DestroyImmediate(profile.horrorDroneClip);
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void Initialize_CreatesThreeLooping2DSources()
        {
            ambience.Initialize(profile);
            foreach (var s in new[] { ambience.Day, ambience.Night, ambience.Drone })
            {
                Assert.IsNotNull(s);
                Assert.IsTrue(s.loop);
                Assert.IsFalse(s.playOnAwake);
                Assert.AreEqual(0f, s.spatialBlend);
                Assert.IsTrue(s.transform.IsChildOf(go.transform));
            }
            Assert.AreSame(profile.dayClip, ambience.Day.clip);
            Assert.AreSame(profile.nightClip, ambience.Night.clip);
            Assert.AreSame(profile.horrorDroneClip, ambience.Drone.clip);
        }

        [Test]
        public void Initialize_Twice_ReusesSources()
        {
            ambience.Initialize(profile);
            ambience.Initialize(profile);
            Assert.AreEqual(3, go.GetComponentsInChildren<AudioSource>(true).Length);
        }

        [Test]
        public void Volumes_FollowTheClock()
        {
            ambience.Initialize(profile);
            var noon = profile.Evaluate(12f);
            ambience.Apply(noon.DayAmbience, noon.NightAmbience, noon.HorrorDrone);
            Assert.AreEqual(0.35f, ambience.Day.volume, 1e-4f);
            Assert.AreEqual(0f, ambience.Night.volume, 1e-4f);
            Assert.AreEqual(0f, ambience.Drone.volume, 1e-4f);
            var night = profile.Evaluate(22f);
            ambience.Apply(night.DayAmbience, night.NightAmbience, night.HorrorDrone);
            Assert.AreEqual(0f, ambience.Day.volume, 1e-4f);
            Assert.AreEqual(0.4f, ambience.Night.volume, 1e-4f);
            Assert.AreEqual(0.3f, ambience.Drone.volume, 1e-4f);
            var dusk = profile.Evaluate(18f);
            ambience.Apply(dusk.DayAmbience, dusk.NightAmbience, dusk.HorrorDrone);
            Assert.AreEqual(0.175f, ambience.Day.volume, 1e-4f);
            Assert.AreEqual(0.2f, ambience.Night.volume, 1e-4f);
        }

        [Test]
        public void MissingClips_DoNotThrow()
        {
            var empty = DayCycleProfile.CreateDefault();
            try
            {
                ambience.Initialize(empty);
                Assert.IsNull(ambience.Day.clip);
                Assert.DoesNotThrow(() => ambience.Apply(1f, 1f, 1f));
            }
            finally
            {
                Object.DestroyImmediate(empty);
            }
        }

        [Test]
        public void Apply_BeforeInitialize_DoesNothing() => Assert.DoesNotThrow(() => ambience.Apply(1f, 1f, 1f));
    }
}
```

- [ ] **Step 2: Jalankan, pastikan gagal**

Procedure R+T dengan `Group = "AmbienceAudioTests"`. Expected: `COMPILE FAILED` (`CS0246 AmbienceAudio`).

- [ ] **Step 3: Implementasi**

`Assets/DayCycle/Runtime/Audio/AmbienceAudio.cs`:
```csharp
using UnityEngine;

namespace TycoWorld.DayCycle
{
    /// <summary>Three looping 2D ambience layers (day, night, horror drone); their volumes come from the day cycle.</summary>
    public sealed class AmbienceAudio : MonoBehaviour
    {
        public const string DayName = "Ambience_Day";
        public const string NightName = "Ambience_Night";
        public const string DroneName = "Ambience_Drone";

        DayCycleProfile profile;

        public AudioSource Day { get; private set; }
        public AudioSource Night { get; private set; }
        public AudioSource Drone { get; private set; }

        public void Initialize(DayCycleProfile cycleProfile)
        {
            profile = cycleProfile;
            Day = Layer(DayName, profile.dayClip);
            Night = Layer(NightName, profile.nightClip);
            Drone = Layer(DroneName, profile.horrorDroneClip);
        }

        public void Apply(float dayWeight, float nightWeight, float droneWeight)
        {
            if (profile == null) return;
            Set(Day, dayWeight * profile.dayVolume);
            Set(Night, nightWeight * profile.nightVolume);
            Set(Drone, droneWeight * profile.droneVolume);
        }

        AudioSource Layer(string layerName, AudioClip clip)
        {
            var child = transform.Find(layerName);
            if (child == null)
            {
                child = new GameObject(layerName).transform;
                child.SetParent(transform, false);
            }
            var source = child.GetComponent<AudioSource>();
            if (source == null) source = child.gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            return source;
        }

        static void Set(AudioSource source, float volume)
        {
            source.volume = volume;
            if (!Application.isPlaying || source.clip == null) return;
            bool audible = volume > 0.001f;
            if (audible && !source.isPlaying) source.Play();
            else if (!audible && source.isPlaying) source.Stop();
        }
    }
}
```

- [ ] **Step 4: Jalankan, pastikan lulus**

Procedure R+T dengan `Group = "AmbienceAudioTests"`. Expected: `passed=5 failed=0`.

- [ ] **Step 5: Commit**

Procedure C:
- path: `Assets/DayCycle`
- subject: `feat(daycycle): day, night and horror ambience layers`

---

### Task 10: `DayCycleDirector`

**Files:**
- Create: `Assets/WorldGen/Runtime/ISceneLightingOwner.cs`
- Create: `Assets/DayCycle/Runtime/DayCycleDirector.cs`
- Test: `Assets/DayCycle/Tests/RenderSettingsSnapshot.cs`, `Assets/DayCycle/Tests/DirectorFixture.cs`, `Assets/DayCycle/Tests/DayCycleDirectorTests.cs`

**Interfaces:**
- Consumes:
  - `WorldClock`, `DayPhase`, `DayPhaseKind`, `DayTimes` (Task 1–2);
  - `DayCycleProfile.Evaluate` / `LightingState` (Task 3);
  - `StreetLampSystem` (Task 7), `AmbienceAudio` (Task 9);
  - `HudModel.SetClock` / `ShowMessage` (Task 8);
  - `FirstPersonPlayer.IsInitialized` / `Hud`, `GeneratedRoot`, `StreetLamp`.
- Produces:
  - `TycoWorld.WorldGen.ISceneLightingOwner` (antarmuka penanda kosong).
  - `DayCycleDirector : MonoBehaviour, ISceneLightingOwner` dengan:
    - konstanta `HorrorStartMessage`, `HorrorEndMessage`, `MissingProfileWarning`, `NoLampsWarning`;
    - `Configure(DayCycleProfile, Light sun, Light moon, Volume day, Volume night, Volume horror, Material skybox, AmbienceAudio, FirstPersonPlayer)`;
    - getter `Profile`, `Sun`, `Moon`, `DayVolume`, `NightVolume`, `HorrorVolume`, `SkyboxMaterial`, `Ambience`, `Player`;
    - `Initialize()`, `Initialize(float startHours)`, `Tick(float dt)`, `Shutdown()`, `ShowMessage(string)`;
    - `Clock`, `Phase`, `IsHorror`, `IsInitialized`, `ShowClock` (get/set), `State`, `Lamps`, `SkyboxInstance`;
    - `Func<GeneratedRoot> FindWorldRoot` (get/set);
    - `event Action HorrorStarted`, `HorrorEnded`.
- Produces (tes): `RenderSettingsSnapshot`, `DirectorFixture`. Keduanya dipakai ulang Task 11.

**Catatan:**
- Mengubah RenderSettings lewat script tidak membuat scene dirty, dan ambient probe Trilight langsung ikut berubah tanpa `DynamicGI.UpdateEnvironment` (dicek saat menulis plan: probe r0 0,0039 → 0,2424).
- `FindWorldRoot` adalah hook. Tes mengisinya supaya tidak menemukan World_Generated asli yang mungkin sedang terbuka di editor.

- [ ] **Step 1: Tulis helper dan tes yang gagal**

`Assets/DayCycle/Tests/RenderSettingsSnapshot.cs`:
```csharp
using UnityEngine;
using UnityEngine.Rendering;

namespace TycoWorld.DayCycle.Tests
{
    /// <summary>Saves the RenderSettings a day cycle test touches and puts them back in TearDown.</summary>
    internal sealed class RenderSettingsSnapshot
    {
        readonly AmbientMode ambientMode;
        readonly Color sky, equator, ground, fogColor;
        readonly bool fog;
        readonly FogMode fogMode;
        readonly float fogDensity;
        readonly Material skybox;
        readonly Light sun;

        public RenderSettingsSnapshot()
        {
            ambientMode = RenderSettings.ambientMode;
            sky = RenderSettings.ambientSkyColor;
            equator = RenderSettings.ambientEquatorColor;
            ground = RenderSettings.ambientGroundColor;
            fog = RenderSettings.fog;
            fogMode = RenderSettings.fogMode;
            fogDensity = RenderSettings.fogDensity;
            fogColor = RenderSettings.fogColor;
            skybox = RenderSettings.skybox;
            sun = RenderSettings.sun;
        }

        public void Restore()
        {
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground;
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.fog = fog;
            RenderSettings.fogMode = fogMode;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogColor = fogColor;
            RenderSettings.skybox = skybox;
            RenderSettings.sun = sun;
        }
    }
}
```

`Assets/DayCycle/Tests/DirectorFixture.cs`:
```csharp
using System;
using System.Collections.Generic;
using TycoWorld.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace TycoWorld.DayCycle.Tests
{
    /// <summary>A code-built director with sun, moon, three volumes, a skybox, ambience and (optionally) a player.</summary>
    internal sealed class DirectorFixture : IDisposable
    {
        readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
        readonly RenderSettingsSnapshot snapshot = new RenderSettingsSnapshot();

        public readonly DayCycleProfile Profile;
        public readonly Light Sun, Moon;
        public readonly Volume Day, Night, Horror;
        public readonly Material Sky;
        public readonly GameObject Root;
        public readonly AmbienceAudio Ambience;
        public readonly DayCycleDirector Director;
        public FirstPersonPlayer Player { get; private set; }
        public int Started, Ended;

        public DirectorFixture()
        {
            Profile = Track(DayCycleProfile.CreateDefault());
            Sun = NewLight("Sun");
            Moon = NewLight("Moon");
            Night = NewVolume("Volume_Night", 0f);
            Day = NewVolume("Volume_Day", 1f);
            Horror = NewVolume("Volume_Horror", 2f);
            Sky = Track(new Material(Shader.Find("Skybox/Procedural")));
            Sky.SetFloat("_Exposure", 9f);
            Root = Track(new GameObject("DayCycle"));
            Ambience = Root.AddComponent<AmbienceAudio>();
            Director = Root.AddComponent<DayCycleDirector>();
            Wire(null);
            Director.FindWorldRoot = () => null;
            Director.HorrorStarted += () => Started++;
            Director.HorrorEnded += () => Ended++;
        }

        public T Track<T>(T obj) where T : UnityEngine.Object
        {
            created.Add(obj);
            return obj;
        }

        public void Wire(FirstPersonPlayer player) =>
            Director.Configure(Profile, Sun, Moon, Day, Night, Horror, Sky, Ambience, player);

        /// <summary>An initialized player far outside the world, wired into the director (for HUD checks).</summary>
        public FirstPersonPlayer AddPlayer()
        {
            var tuning = Track(ScriptableObject.CreateInstance<PlayerTuning>());
            var go = Track(new GameObject("Player"));
            go.transform.position = new Vector3(5000f, 1000f, 5000f);
            var pivot = new GameObject("CameraPivot").transform;
            pivot.SetParent(go.transform, false);
            var view = new GameObject("Camera").transform;
            view.SetParent(pivot, false);
            Player = go.AddComponent<FirstPersonPlayer>();
            Player.Configure(tuning, pivot, view);
            Player.Initialize();
            Wire(Player);
            return Player;
        }

        Light NewLight(string name)
        {
            var light = Track(new GameObject(name)).AddComponent<Light>();
            light.type = LightType.Directional;
            return light;
        }

        Volume NewVolume(string name, float priority)
        {
            var volume = Track(new GameObject(name)).AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = priority;
            return volume;
        }

        public void Dispose()
        {
            Director.Shutdown();
            for (int i = created.Count - 1; i >= 0; i--)
                if (created[i] != null) UnityEngine.Object.DestroyImmediate(created[i]);
            created.Clear();
            snapshot.Restore();
        }
    }
}
```

`Assets/DayCycle/Tests/DayCycleDirectorTests.cs`:
```csharp
using NUnit.Framework;
using TycoWorld.WorldGen;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace TycoWorld.DayCycle.Tests
{
    public class DayCycleDirectorTests
    {
        DirectorFixture fx;

        [SetUp]
        public void SetUp() => fx = new DirectorFixture();

        [TearDown]
        public void TearDown() => fx.Dispose();

        static void AreClose(Color expected, Color actual)
        {
            Assert.AreEqual(expected.r, actual.r, 1e-3f);
            Assert.AreEqual(expected.g, actual.g, 1e-3f);
            Assert.AreEqual(expected.b, actual.b, 1e-3f);
        }

        [Test]
        public void Messages_AreExact()
        {
            Assert.AreEqual("Malam semakin larut\u2026", DayCycleDirector.HorrorStartMessage);
            Assert.AreEqual("Subuh. Malam sudah lewat.", DayCycleDirector.HorrorEndMessage);
        }

        [Test]
        public void Initialize_StartsAt0630_WithoutEvents()
        {
            fx.Director.Initialize();
            Assert.AreEqual("06:30", fx.Director.Clock.Display);
            Assert.AreEqual(1, fx.Director.Clock.Day);
            Assert.AreEqual(DayPhaseKind.Day, fx.Director.Phase);
            Assert.IsFalse(fx.Director.IsHorror);
            Assert.AreEqual(0, fx.Started + fx.Ended);
            Assert.IsTrue(fx.Sun.enabled);
            Assert.AreSame(fx.Sun, RenderSettings.sun);
        }

        [Test]
        public void Noon_AppliesDaylight()
        {
            fx.Director.Initialize(12f);
            Assert.IsTrue(fx.Sun.enabled);
            Assert.IsFalse(fx.Moon.enabled);
            Assert.AreEqual(fx.Profile.sunIntensity, fx.Sun.intensity, 1e-3f);
            Assert.AreSame(fx.Sun, RenderSettings.sun);
            Assert.AreEqual(AmbientMode.Trilight, RenderSettings.ambientMode);
            AreClose(fx.Profile.ambientSkyDay, RenderSettings.ambientSkyColor);
            AreClose(fx.Profile.ambientGroundDay, RenderSettings.ambientGroundColor);
            Assert.IsTrue(RenderSettings.fog);
            Assert.AreEqual(FogMode.Exponential, RenderSettings.fogMode);
            Assert.AreEqual(0.004f, RenderSettings.fogDensity, 1e-4f);
            Assert.AreEqual(1f, fx.Day.weight, 1e-4f);
            Assert.AreEqual(1f, fx.Night.weight, 1e-4f);
            Assert.AreEqual(0f, fx.Horror.weight, 1e-4f);
        }

        [Test]
        public void Night_AppliesMoonFogAndHorrorVolume()
        {
            fx.Director.Initialize(22f);
            Assert.IsFalse(fx.Sun.enabled);
            Assert.IsTrue(fx.Moon.enabled);
            Assert.AreEqual(0.15f, fx.Moon.intensity, 1e-4f);
            Assert.AreSame(fx.Moon, RenderSettings.sun);
            AreClose(fx.Profile.ambientSkyNight, RenderSettings.ambientSkyColor);
            Assert.AreEqual(0.032f, RenderSettings.fogDensity, 1e-4f);
            Assert.AreEqual(0f, fx.Day.weight, 1e-4f);
            Assert.AreEqual(1f, fx.Night.weight, 1e-4f);
            Assert.AreEqual(1f, fx.Horror.weight, 1e-4f);
            Assert.IsTrue(fx.Director.IsHorror);
            Assert.AreEqual(0, fx.Started);
        }

        [Test]
        public void Tick_LongFrameAcross1900_FiresOnce()
        {
            fx.Director.Initialize(18.9f);
            fx.Director.Tick(60f);
            Assert.IsTrue(fx.Director.IsHorror);
            Assert.AreEqual(1, fx.Started);
            fx.Director.Tick(1f);
            fx.Director.Tick(60f);
            Assert.AreEqual(1, fx.Started);
            Assert.AreEqual(0, fx.Ended);
        }

        [Test]
        public void Tick_Across0500_FiresEndedOnce()
        {
            fx.Director.Initialize(4.5f);
            fx.Director.Tick(60f);
            Assert.IsFalse(fx.Director.IsHorror);
            Assert.AreEqual(DayPhaseKind.Dawn, fx.Director.Phase);
            Assert.AreEqual(1, fx.Ended);
            Assert.AreEqual(0, fx.Started);
        }

        [Test]
        public void HorrorMessages_GoToPlayerHud()
        {
            var player = fx.AddPlayer();
            fx.Director.Initialize(18.9f);
            fx.Director.Tick(60f);
            Assert.AreEqual(DayCycleDirector.HorrorStartMessage, player.Hud.Message);
            fx.Director.Clock.SetTime(4.9f);
            fx.Director.Tick(12f);
            Assert.AreEqual(DayCycleDirector.HorrorEndMessage, player.Hud.Message);
        }

        [Test]
        public void ShowClock_WritesClockToHud()
        {
            var player = fx.AddPlayer();
            fx.Director.Initialize(12f);
            fx.Director.Tick(0.01f);
            Assert.IsFalse(player.Hud.ClockVisible);
            fx.Director.ShowClock = true;
            fx.Director.Tick(0.01f);
            Assert.IsTrue(player.Hud.ClockVisible);
            Assert.AreEqual(fx.Director.Clock.Display, player.Hud.Clock);
        }

        [Test]
        public void WithoutPlayer_RunsWithoutErrors()
        {
            fx.Director.Configure(fx.Profile, null, null, null, null, null, null, null, null);
            fx.Director.Initialize(18.9f);
            Assert.DoesNotThrow(() => fx.Director.Tick(60f));
            Assert.IsTrue(fx.Director.IsHorror);
            Assert.DoesNotThrow(() => fx.Director.ShowMessage("x"));
        }

        [Test]
        public void MissingProfile_UsesDefault_WithOneWarning()
        {
            fx.Director.Configure(null, fx.Sun, fx.Moon, fx.Day, fx.Night, fx.Horror, fx.Sky, fx.Ambience, null);
            LogAssert.Expect(LogType.Warning, DayCycleDirector.MissingProfileWarning);
            fx.Director.Initialize(12f);
            Assert.IsNotNull(fx.Director.Profile);
            Assert.IsTrue(fx.Sun.enabled);
        }

        [Test]
        public void Skybox_UsesRuntimeCopy()
        {
            fx.Director.Initialize(12f);
            var copy = fx.Director.SkyboxInstance;
            Assert.IsNotNull(copy);
            Assert.AreNotSame(fx.Sky, copy);
            Assert.AreSame(copy, RenderSettings.skybox);
            Assert.AreEqual(1.3f, copy.GetFloat("_Exposure"), 1e-3f);
            Assert.AreEqual(9f, fx.Sky.GetFloat("_Exposure"), 1e-5f);
            fx.Director.Shutdown();
            Assert.AreSame(fx.Sky, RenderSettings.skybox);
        }

        [Test]
        public void NoStreetLamps_WarnsOnce()
        {
            var root = fx.Track(new GameObject("World_Generated_Test")).AddComponent<GeneratedRoot>();
            fx.Director.FindWorldRoot = () => root;
            fx.Director.Initialize(22f);
            LogAssert.Expect(LogType.Warning, DayCycleDirector.NoLampsWarning);
            for (int i = 0; i < 5; i++) fx.Director.Tick(1f);
            LogAssert.NoUnexpectedReceived();
            Assert.IsTrue(fx.Director.Lamps.Collected);
            Assert.AreEqual(0, fx.Director.Lamps.Count);
        }

        [Test]
        public void StreetLamps_UnderWorldRoot_AreLitAtNightOnly()
        {
            var rootGo = fx.Track(new GameObject("World_Generated_Test"));
            var root = rootGo.AddComponent<GeneratedRoot>();
            var lamp = LampFixture.Create(rootGo.transform, LampFixture.PositionWithRole(LampRole.Normal, fx.Profile));
            fx.Director.FindWorldRoot = () => root;
            fx.Director.Initialize(22f);
            fx.Director.Tick(0.1f);
            Assert.AreEqual(1, fx.Director.Lamps.Count);
            Assert.IsTrue(lamp.Light.enabled);
            Assert.AreEqual(fx.Profile.lampIntensity, lamp.Light.intensity, 1e-4f);
            fx.Director.Clock.SetTime(12f);
            fx.Director.Tick(0.1f);
            Assert.IsFalse(lamp.Light.enabled);
        }
    }
}
```

- [ ] **Step 2: Jalankan, pastikan gagal**

Procedure R+T dengan `Group = "DayCycleDirectorTests"`. Expected: `COMPILE FAILED` (`CS0246 DayCycleDirector`).

- [ ] **Step 3: Implementasi**

`Assets/WorldGen/Runtime/ISceneLightingOwner.cs`:
```csharp
namespace TycoWorld.WorldGen
{
    /// <summary>Implemented by a component that drives scene lighting at runtime; preview setup then leaves lighting alone.</summary>
    public interface ISceneLightingOwner
    {
    }
}
```

`Assets/DayCycle/Runtime/DayCycleDirector.cs`:
```csharp
using System;
using TycoWorld.Player;
using TycoWorld.WorldGen;
using UnityEngine;
using UnityEngine.Rendering;

namespace TycoWorld.DayCycle
{
    /// <summary>
    /// The single per-frame driver of the day cycle (spec #3 §3): clock → phase (events) → profile → sun/moon,
    /// RenderSettings, skybox copy, three volumes, street lamps, ambience and the player HUD.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DayCycleDirector : MonoBehaviour, ISceneLightingOwner
    {
        public const string HorrorStartMessage = "Malam semakin larut\u2026";
        public const string HorrorEndMessage = "Subuh. Malam sudah lewat.";
        public const string MissingProfileWarning = "[DayCycle] DayCycleProfile kosong; memakai nilai default.";
        public const string NoLampsWarning = "[DayCycle] Dunia belum punya penanda StreetLamp. Generate ulang untuk lampu dinamis.";
        const float LampPollSeconds = 0.5f;

        static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        static readonly int SkyTintId = Shader.PropertyToID("_SkyTint");

        [SerializeField] DayCycleProfile profile;
        [SerializeField] Light sun;
        [SerializeField] Light moon;
        [SerializeField] Volume dayVolume;
        [SerializeField] Volume nightVolume;
        [SerializeField] Volume horrorVolume;
        [SerializeField] Material skyboxMaterial;
        [SerializeField] AmbienceAudio ambience;
        [SerializeField] FirstPersonPlayer player;

        DayPhase phase;
        Material skyCopy;
        bool ownsProfile;
        float elapsed;
        float nextLampPoll;

        public event Action HorrorStarted;
        public event Action HorrorEnded;

        public DayCycleProfile Profile => profile;
        public Light Sun => sun;
        public Light Moon => moon;
        public Volume DayVolume => dayVolume;
        public Volume NightVolume => nightVolume;
        public Volume HorrorVolume => horrorVolume;
        public Material SkyboxMaterial => skyboxMaterial;
        public AmbienceAudio Ambience => ambience;
        public FirstPersonPlayer Player => player;

        public WorldClock Clock { get; private set; }
        public DayPhaseKind Phase => phase != null ? phase.Phase : DayPhase.PhaseAt(DayTimes.StartHour);
        public bool IsHorror => phase != null && phase.IsHorror;
        public bool IsInitialized { get; private set; }
        public bool ShowClock { get; set; }
        public LightingState State { get; private set; }
        public StreetLampSystem Lamps { get; private set; }
        public Material SkyboxInstance => skyCopy;

        /// <summary>Finds the generated world (another scene, so it cannot be serialized); tests replace it.</summary>
        public Func<GeneratedRoot> FindWorldRoot { get; set; } = () => UnityEngine.Object.FindAnyObjectByType<GeneratedRoot>();

        HudModel Hud => player != null && player.IsInitialized ? player.Hud : null;

        public void Configure(DayCycleProfile cycleProfile, Light sunLight, Light moonLight, Volume day, Volume night, Volume horror,
            Material skybox, AmbienceAudio ambienceAudio, FirstPersonPlayer firstPerson)
        {
            profile = cycleProfile;
            sun = sunLight;
            moon = moonLight;
            dayVolume = day;
            nightVolume = night;
            horrorVolume = horror;
            skyboxMaterial = skybox;
            ambience = ambienceAudio;
            player = firstPerson;
        }

        void Awake() => Initialize();

        void Update()
        {
            if (!Application.isPlaying) return;
            Tick(Time.deltaTime);
        }

        void OnDestroy() => Shutdown();

        public void Initialize() => Initialize(DayTimes.StartHour);

        public void Initialize(float startHours)
        {
            if (IsInitialized) return;
            if (profile == null)
            {
                Debug.LogWarning(MissingProfileWarning);
                profile = DayCycleProfile.CreateDefault();
                profile.hideFlags = HideFlags.DontSave;
                ownsProfile = true;
            }
            Clock = new WorldClock(startHours);
            phase = new DayPhase();
            phase.HorrorStarted += OnHorrorStarted;
            phase.HorrorEnded += OnHorrorEnded;
            phase.Initialize(Clock.Hours);
            Lamps = new StreetLampSystem(profile);
            if (skyboxMaterial != null)
            {
                skyCopy = new Material(skyboxMaterial) { name = skyboxMaterial.name + " (Runtime)", hideFlags = HideFlags.DontSave };
                RenderSettings.skybox = skyCopy;
            }
            if (ambience != null) ambience.Initialize(profile);
            elapsed = 0f;
            nextLampPoll = 0f;
            IsInitialized = true;
            Apply();
        }

        public void Tick(float dt)
        {
            if (!IsInitialized) Initialize();
            if (dt > 0f) elapsed += dt;
            Clock.Tick(dt);
            phase.Update(Clock.Hours);
            PollLamps();
            Apply();
        }

        public void ShowMessage(string text)
        {
            var hud = Hud;
            if (hud != null) hud.ShowMessage(text);
        }

        /// <summary>Restores lamp materials and the skybox asset and frees runtime copies; safe to call twice.</summary>
        public void Shutdown()
        {
            if (Lamps != null) Lamps.Dispose();
            if (skyCopy != null)
            {
                if (RenderSettings.skybox == skyCopy) RenderSettings.skybox = skyboxMaterial;
                DestroyOwned(skyCopy);
                skyCopy = null;
            }
            if (ownsProfile && profile != null)
            {
                DestroyOwned(profile);
                profile = null;
                ownsProfile = false;
            }
            IsInitialized = false;
        }

        void OnHorrorStarted()
        {
            ShowMessage(HorrorStartMessage);
            HorrorStarted?.Invoke();
        }

        void OnHorrorEnded()
        {
            ShowMessage(HorrorEndMessage);
            HorrorEnded?.Invoke();
        }

        void PollLamps()
        {
            if (Lamps.Collected || elapsed < nextLampPoll) return;
            nextLampPoll = elapsed + LampPollSeconds;
            var root = FindWorldRoot != null ? FindWorldRoot() : null;
            if (root == null) return;
            var found = root.GetComponentsInChildren<StreetLamp>(true);
            if (found.Length == 0) Debug.LogWarning(NoLampsWarning);
            Lamps.Collect(found);
        }

        void Apply()
        {
            var s = profile.Evaluate(Clock.Hours);
            State = s;
            if (sun != null)
            {
                sun.enabled = s.SunEnabled;
                sun.intensity = s.SunIntensity;
                sun.color = s.SunColor;
                sun.transform.rotation = Quaternion.Euler(s.SunElevation, s.SunYaw, 0f);
            }
            if (moon != null)
            {
                moon.enabled = s.MoonEnabled;
                moon.intensity = s.MoonIntensity;
                moon.color = profile.moonColor;
            }
            RenderSettings.sun = s.SunEnabled && sun != null ? sun : moon;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = s.AmbientSky;
            RenderSettings.ambientEquatorColor = s.AmbientEquator;
            RenderSettings.ambientGroundColor = s.AmbientGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = s.FogDensity;
            RenderSettings.fogColor = s.FogColor;
            if (skyCopy != null)
            {
                skyCopy.SetFloat(ExposureId, s.SkyExposure);
                skyCopy.SetColor(SkyTintId, s.SkyTint);
            }
            if (nightVolume != null) nightVolume.weight = 1f;
            if (dayVolume != null) dayVolume.weight = s.DayVolumeWeight;
            if (horrorVolume != null) horrorVolume.weight = s.HorrorWeight;
            Lamps.Apply(s.LampIntensity01, s.HorrorWeight, elapsed);
            if (ambience != null) ambience.Apply(s.DayAmbience, s.NightAmbience, s.HorrorDrone);
            var hud = Hud;
            if (hud != null) hud.SetClock(Clock.Display, ShowClock);
        }

        static void DestroyOwned(UnityEngine.Object obj)
        {
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }
    }
}
```

- [ ] **Step 4: Jalankan, pastikan lulus**

Procedure R+T dengan `Group = "DayCycleDirectorTests"`. Expected: `passed=13 failed=0`.

Lalu `Group = "."`. Expected: `failed=0`.

- [ ] **Step 5: Commit**

Procedure C:
- path: `Assets/DayCycle` `Assets/WorldGen/Runtime/ISceneLightingOwner.cs` `Assets/WorldGen/Runtime/ISceneLightingOwner.cs.meta`
- subject: `feat(daycycle): day cycle director applying sun, sky, fog, volumes, lamps, ambience and HUD`

---
### Task 11: `DayCycleDebug`

**Files:**
- Create: `Assets/DayCycle/Runtime/Debug/DayCycleDebug.cs`
- Test: `Assets/DayCycle/Tests/DayCycleDebugTests.cs`

**Interfaces:**
- Consumes:
  - `DebugTimeControls` / `DebugKeys` (Task 5);
  - `DayCycleDirector.Clock`, `IsInitialized`, `ShowClock`, `ShowMessage` (Task 10);
  - `DirectorFixture` (Task 10);
  - `FirstPersonPlayer.Locks` / `PlayerAbility.All`.
- Produces: `DayCycleDebug : MonoBehaviour` (`[RequireComponent(typeof(DayCycleDirector))]`) dengan `static bool Allowed`, `DebugTimeControls Controls`, dan `bool ApplyKeys(DebugKeys)`.

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/DayCycle/Tests/DayCycleDebugTests.cs`:
```csharp
using NUnit.Framework;
using TycoWorld.Player;

namespace TycoWorld.DayCycle.Tests
{
    public class DayCycleDebugTests
    {
        static readonly DebugKeys T = new DebugKeys { JumpTime = true };

        DirectorFixture fx;
        FirstPersonPlayer player;
        DayCycleDebug debug;

        [SetUp]
        public void SetUp()
        {
            fx = new DirectorFixture();
            player = fx.AddPlayer();
            debug = fx.Root.AddComponent<DayCycleDebug>();
        }

        [TearDown]
        public void TearDown() => fx.Dispose();

        [Test]
        public void Allowed_InEditor() => Assert.IsTrue(DayCycleDebug.Allowed);

        [Test]
        public void T_JumpsToNight_ThenHorrorStartsOnNextTick()
        {
            fx.Director.Initialize(12f);
            Assert.IsTrue(debug.ApplyKeys(T));
            Assert.AreEqual(19.5f, fx.Director.Clock.Hours, 1e-4f);
            Assert.AreEqual("Lompat ke 19:30", player.Hud.Message);
            fx.Director.Tick(0.016f);
            Assert.IsTrue(fx.Director.IsHorror);
            Assert.AreEqual(1, fx.Started);
            Assert.AreEqual(DayCycleDirector.HorrorStartMessage, player.Hud.Message);
            debug.ApplyKeys(T);
            fx.Director.Tick(0.016f);
            Assert.AreEqual(6.5f, fx.Director.Clock.Hours, 1e-3f);
            Assert.AreEqual(1, fx.Ended);
        }

        [Test]
        public void Brackets_ChangeSpeed()
        {
            fx.Director.Initialize(12f);
            debug.ApplyKeys(new DebugKeys { FasterSpeed = true });
            Assert.AreEqual(10f, fx.Director.Clock.SpeedMultiplier);
            Assert.AreEqual("Kecepatan waktu x10", player.Hud.Message);
            fx.Director.Tick(6f);
            Assert.AreEqual(13f, fx.Director.Clock.Hours, 1e-3f);
            debug.ApplyKeys(new DebugKeys { SlowerSpeed = true });
            Assert.AreEqual(1f, fx.Director.Clock.SpeedMultiplier);
        }

        [Test]
        public void F3_TogglesHudClock()
        {
            fx.Director.Initialize(12f);
            debug.ApplyKeys(new DebugKeys { ToggleClock = true });
            Assert.IsTrue(fx.Director.ShowClock);
            fx.Director.Tick(0.016f);
            Assert.IsTrue(player.Hud.ClockVisible);
            Assert.AreEqual("12:00", player.Hud.Clock);
        }

        [Test]
        public void WorksWhilePlayerControlsAreLocked()
        {
            fx.Director.Initialize(12f);
            player.Locks.Acquire(this, PlayerAbility.All);
            Assert.IsTrue(debug.ApplyKeys(T));
            Assert.AreEqual(19.5f, fx.Director.Clock.Hours, 1e-4f);
        }

        [Test]
        public void BeforeDirectorInitialized_DoesNothing()
        {
            Assert.IsFalse(debug.ApplyKeys(T));
            Assert.IsFalse(fx.Director.IsInitialized);
        }

        [Test]
        public void NoKeys_ReturnsFalse()
        {
            fx.Director.Initialize(12f);
            Assert.IsFalse(debug.ApplyKeys(default));
            Assert.AreEqual(12f, fx.Director.Clock.Hours, 1e-5f);
        }
    }
}
```

- [ ] **Step 2: Jalankan, pastikan gagal**

Procedure R+T dengan `Group = "DayCycleDebugTests"`. Expected: `COMPILE FAILED` (`CS0246 DayCycleDebug`).

- [ ] **Step 3: Implementasi**

`Assets/DayCycle/Runtime/Debug/DayCycleDebug.cs`:
```csharp
using UnityEngine;
using UnityEngine.InputSystem;

namespace TycoWorld.DayCycle
{
    /// <summary>
    /// Debug keys for the Editor and development builds: T jumps 06:30 ↔ 19:30, [ / ] change the speed, F3 shows the
    /// HUD clock. Reads the keyboard directly, so it keeps working while player controls are locked.
    /// </summary>
    [RequireComponent(typeof(DayCycleDirector))]
    public sealed class DayCycleDebug : MonoBehaviour
    {
        readonly DebugTimeControls controls = new DebugTimeControls();
        DayCycleDirector director;

        public static bool Allowed => Application.isEditor || Debug.isDebugBuild;
        public DebugTimeControls Controls => controls;

        void Awake()
        {
            if (!Allowed) enabled = false;
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            ApplyKeys(new DebugKeys
            {
                JumpTime = keyboard.tKey.wasPressedThisFrame,
                SlowerSpeed = keyboard.leftBracketKey.wasPressedThisFrame,
                FasterSpeed = keyboard.rightBracketKey.wasPressedThisFrame,
                ToggleClock = keyboard.f3Key.wasPressedThisFrame
            });
        }

        public bool ApplyKeys(DebugKeys keys)
        {
            if (director == null) director = GetComponent<DayCycleDirector>();
            if (director == null || !director.IsInitialized) return false;
            if (!controls.Apply(keys, director.Clock)) return false;
            director.ShowClock = controls.ClockVisible;
            if (controls.LastMessage.Length > 0) director.ShowMessage(controls.LastMessage);
            return true;
        }
    }
}
```

- [ ] **Step 4: Jalankan, pastikan lulus**

Procedure R+T dengan `Group = "DayCycleDebugTests"`. Expected: `passed=7 failed=0`.

- [ ] **Step 5: Commit**

Procedure C:
- path: `Assets/DayCycle`
- subject: `feat(daycycle): debug time keys (T, brackets, F3)`

---

### Task 12: Audio ambience (CC0 jangkrik + sintesis siang/dengung)

**Files:**
- Create: `Assets/DayCycle/Editor/AmbienceSynth.cs`, `Assets/DayCycle/Editor/WavWriter.cs`, `Assets/DayCycle/Editor/DayCycleAudioImportRules.cs`, `Assets/DayCycle/Editor/DayCycleAudioBuilder.cs`
- Create (asset):
  - `Assets/DayCycle/Audio/ambience_night_crickets.mp3` (unduhan CC0);
  - `ambience_day.wav` dan `ambience_horror_drone.wav` (sintesis);
  - `SOURCES.md`.
- Test: `Assets/DayCycle/Tests/AmbienceSynthTests.cs`, `Assets/DayCycle/Tests/WavWriterTests.cs`, `Assets/DayCycle/Tests/DayCycleAudioAssetsTests.cs`

**Interfaces:**
- Consumes: `DayCyclePaths` (Task 1: `AudioFolder`, `DayAmbienceClip`, `NightAmbienceClip`, `HorrorDroneClip`, `AudioSourcesFile`; `NightAmbienceSynthClip` ditambahkan di task ini); `AssetFolders.Ensure`, `WorldGenPaths.ToAbsolute`.
- Produces:
  - `AmbienceSynth` dengan `SampleRate` (22050), `LoopFadeSeconds` (0.5), `Day(float seconds = 20f)`, `Night(float seconds = 16f)`, `Drone(float seconds = 12f)`, `Loop(float[] raw, int fade)`, `Normalize(float[] samples, float peak)`.
  - `WavWriter.Encode(float[] samples, int sampleRate)` → `byte[]`.
  - `DayCycleAudioBuilder.WriteMissingClips()` dan getter `DayClip`, `NightClip` (mp3, cadangan wav sintesis), `DroneClip`.

**Sumber suara** (dicek saat menulis plan):
- "Crickets Ambient Noise – loopable" oleh Wolfgang_ di OpenGameArt, lisensi **CC0**, 11 s, `https://opengameart.org/sites/default/files/crickets_1.mp3`. Halaman: `https://opengameart.org/content/crickets-ambient-noise-loopable`.
- Loop siang CC0 yang cocok (burung + angin, tanpa musik) tidak ditemukan. Siang dan dengung horor disintesis sesuai fallback spec §5.

- [ ] **Step 1: Tulis tes sintesis dan WAV yang gagal**

`Assets/DayCycle/Tests/AmbienceSynthTests.cs`:
```csharp
using System;
using NUnit.Framework;
using TycoWorld.DayCycle.Authoring;

namespace TycoWorld.DayCycle.Tests
{
    public class AmbienceSynthTests
    {
        static float[] Make(string kind, float seconds) =>
            kind == "day" ? AmbienceSynth.Day(seconds) : kind == "night" ? AmbienceSynth.Night(seconds) : AmbienceSynth.Drone(seconds);

        [TestCase("day")]
        [TestCase("night")]
        [TestCase("drone")]
        public void IsDeterministic(string kind) => CollectionAssert.AreEqual(Make(kind, 3f), Make(kind, 3f));

        [TestCase("day")]
        [TestCase("night")]
        [TestCase("drone")]
        public void HasRequestedLength_AndHealthyLevel(string kind)
        {
            var s = Make(kind, 3f);
            Assert.AreEqual(3 * AmbienceSynth.SampleRate, s.Length);
            float peak = 0f;
            double sum = 0;
            foreach (var v in s)
            {
                peak = Math.Max(peak, Math.Abs(v));
                sum += v * v;
            }
            Assert.AreEqual(0.8f, peak, 1e-4f);
            Assert.Greater(Math.Sqrt(sum / s.Length), 0.02);
        }

        [TestCase("day")]
        [TestCase("night")]
        [TestCase("drone")]
        public void LoopSeam_IsNoBiggerThanAnInnerStep(string kind)
        {
            var s = Make(kind, 3f);
            float inner = 0f;
            for (int i = 1; i < s.Length; i++) inner = Math.Max(inner, Math.Abs(s[i] - s[i - 1]));
            Assert.LessOrEqual(Math.Abs(s[0] - s[s.Length - 1]), inner + 1e-4f);
        }

        [Test]
        public void Loop_CrossfadesTailIntoHead()
        {
            var loop = AmbienceSynth.Loop(new[] { 0f, 0f, 0f, 0f, 1f, 1f }, 2);
            CollectionAssert.AreEqual(new[] { 1f, 0.5f, 0f, 0f }, loop);
        }
    }
}
```

`Assets/DayCycle/Tests/WavWriterTests.cs`:
```csharp
using System;
using System.Text;
using NUnit.Framework;
using TycoWorld.DayCycle.Authoring;

namespace TycoWorld.DayCycle.Tests
{
    public class WavWriterTests
    {
        [Test]
        public void Encode_WritesPcmMonoHeaderAndClampedSamples()
        {
            var bytes = WavWriter.Encode(new[] { 0f, 1f, -1f, 2f }, 22050);
            Assert.AreEqual(44 + 8, bytes.Length);
            Assert.AreEqual("RIFF", Encoding.ASCII.GetString(bytes, 0, 4));
            Assert.AreEqual(36 + 8, BitConverter.ToInt32(bytes, 4));
            Assert.AreEqual("WAVE", Encoding.ASCII.GetString(bytes, 8, 4));
            Assert.AreEqual("fmt ", Encoding.ASCII.GetString(bytes, 12, 4));
            Assert.AreEqual(1, BitConverter.ToInt16(bytes, 20));
            Assert.AreEqual(1, BitConverter.ToInt16(bytes, 22));
            Assert.AreEqual(22050, BitConverter.ToInt32(bytes, 24));
            Assert.AreEqual(44100, BitConverter.ToInt32(bytes, 28));
            Assert.AreEqual(16, BitConverter.ToInt16(bytes, 34));
            Assert.AreEqual("data", Encoding.ASCII.GetString(bytes, 36, 4));
            Assert.AreEqual(8, BitConverter.ToInt32(bytes, 40));
            Assert.AreEqual(0, BitConverter.ToInt16(bytes, 44));
            Assert.AreEqual(32767, BitConverter.ToInt16(bytes, 46));
            Assert.AreEqual(-32767, BitConverter.ToInt16(bytes, 48));
            Assert.AreEqual(32767, BitConverter.ToInt16(bytes, 50));
        }
    }
}
```

- [ ] **Step 2: Jalankan, pastikan gagal**

Procedure R+T dengan `Group = "AmbienceSynthTests|WavWriterTests"`. Expected: `COMPILE FAILED` (`CS0103 AmbienceSynth`, `WavWriter`).

- [ ] **Step 3: Implementasi sintesis dan WAV**

`Assets/DayCycle/Editor/AmbienceSynth.cs`:
```csharp
using System;

namespace TycoWorld.DayCycle.Authoring
{
    /// <summary>Deterministic placeholder ambience (spec #3 §5): seamless mono loops from fixed seeds.</summary>
    public static class AmbienceSynth
    {
        public const int SampleRate = 22050;
        public const float LoopFadeSeconds = 0.5f;
        const float Peak = 0.8f;

        static int FadeSamples => (int)(LoopFadeSeconds * SampleRate);

        /// <summary>Wind with gusts and short bird calls.</summary>
        public static float[] Day(float seconds = 20f)
        {
            var rng = new Random(311);
            var raw = new float[(int)(seconds * SampleRate) + FadeSamples];
            AddWind(raw, rng, 3f, 500f);
            AddBirds(raw, rng, Math.Max(1, (int)(seconds * 0.8f)));
            return Finish(raw);
        }

        /// <summary>Crickets over a low wind (fallback when the CC0 cricket loop is missing).</summary>
        public static float[] Night(float seconds = 16f)
        {
            var rng = new Random(733);
            var raw = new float[(int)(seconds * SampleRate) + FadeSamples];
            AddWind(raw, rng, 2f, 300f);
            AddCrickets(raw, rng, 6);
            return Finish(raw);
        }

        /// <summary>Low beating sines with a slow swell and dark noise.</summary>
        public static float[] Drone(float seconds = 12f)
        {
            var rng = new Random(977);
            var raw = new float[(int)(seconds * SampleRate) + FadeSamples];
            for (int i = 0; i < raw.Length; i++)
            {
                double t = (double)i / SampleRate;
                double swell = 0.7 + 0.3 * Math.Sin(2.0 * Math.PI * t / 9.0);
                raw[i] += (float)(swell * (0.5 * Math.Sin(2.0 * Math.PI * 55.0 * t)
                    + 0.35 * Math.Sin(2.0 * Math.PI * 58.3 * t)
                    + 0.12 * Math.Sin(2.0 * Math.PI * 110.5 * t)));
            }
            AddWind(raw, rng, 1.5f, 150f);
            return Finish(raw);
        }

        /// <summary>Turns n + fade samples into a seamless n-sample loop by cross-fading the tail into the head.</summary>
        public static float[] Loop(float[] raw, int fade)
        {
            int n = raw.Length - fade;
            var output = new float[n];
            Array.Copy(raw, output, n);
            for (int i = 0; i < fade; i++)
            {
                float w = (float)i / fade;
                output[i] = raw[i] * w + raw[n + i] * (1f - w);
            }
            return output;
        }

        public static void Normalize(float[] samples, float peak)
        {
            float max = 0f;
            foreach (var s in samples) max = Math.Max(max, Math.Abs(s));
            if (max <= 0f) return;
            float k = peak / max;
            for (int i = 0; i < samples.Length; i++) samples[i] *= k;
        }

        static float[] Finish(float[] raw)
        {
            var loop = Loop(raw, FadeSamples);
            Normalize(loop, Peak);
            return loop;
        }

        static void AddWind(float[] buffer, Random rng, float amplitude, float cutoffHz)
        {
            float a = 1f - (float)Math.Exp(-2.0 * Math.PI * cutoffHz / SampleRate);
            float low = 0f, low2 = 0f;
            for (int i = 0; i < buffer.Length; i++)
            {
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                low += a * (white - low);
                low2 += a * (low - low2);
                float gust = 0.6f + 0.4f * (float)Math.Sin(2.0 * Math.PI * i / (SampleRate * 7.3));
                buffer[i] += low2 * gust * amplitude;
            }
        }

        static void AddBirds(float[] buffer, Random rng, int calls)
        {
            for (int c = 0; c < calls; c++)
            {
                int start = rng.Next(0, Math.Max(1, buffer.Length - SampleRate));
                int notes = 2 + rng.Next(3);
                float baseHz = 2200f + (float)rng.NextDouble() * 1800f;
                for (int k = 0; k < notes; k++)
                {
                    int noteStart = start + k * (int)(0.16f * SampleRate);
                    int length = (int)((0.06f + 0.08f * (float)rng.NextDouble()) * SampleRate);
                    float sweep = 600f + 900f * (float)rng.NextDouble();
                    double phase = 0;
                    for (int i = 0; i < length && noteStart + i < buffer.Length; i++)
                    {
                        float t = (float)i / length;
                        phase += 2.0 * Math.PI * (baseHz + sweep * t) / SampleRate;
                        float env = (float)Math.Sin(Math.PI * t);
                        buffer[noteStart + i] += 0.25f * env * env * (float)Math.Sin(phase);
                    }
                }
            }
        }

        static void AddCrickets(float[] buffer, Random rng, int crickets)
        {
            for (int c = 0; c < crickets; c++)
            {
                double carrier = 4200.0 + 600.0 * rng.NextDouble();
                double pulseHz = 28.0 + 8.0 * rng.NextDouble();
                double chirpSeconds = 0.12 + 0.08 * rng.NextDouble();
                double periodSeconds = 0.5 + 0.5 * rng.NextDouble();
                float level = 0.08f + 0.08f * (float)rng.NextDouble();
                int offset = rng.Next(SampleRate);
                for (int i = 0; i < buffer.Length; i++)
                {
                    double t = (double)(i + offset) / SampleRate;
                    double inPeriod = t % periodSeconds;
                    if (inPeriod > chirpSeconds) continue;
                    double env = Math.Sin(Math.PI * inPeriod / chirpSeconds);
                    double pulse = 0.5 + 0.5 * Math.Sin(2.0 * Math.PI * pulseHz * t);
                    buffer[i] += (float)(level * env * pulse * pulse * Math.Sin(2.0 * Math.PI * carrier * t));
                }
            }
        }
    }
}
```

`Assets/DayCycle/Editor/WavWriter.cs`:
```csharp
using System;
using System.IO;
using System.Text;

namespace TycoWorld.DayCycle.Authoring
{
    /// <summary>16-bit PCM mono WAV encoder for the synthesized ambience.</summary>
    public static class WavWriter
    {
        public static byte[] Encode(float[] samples, int sampleRate)
        {
            int dataBytes = samples.Length * 2;
            using (var stream = new MemoryStream(44 + dataBytes))
            using (var w = new BinaryWriter(stream))
            {
                w.Write(Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + dataBytes);
                w.Write(Encoding.ASCII.GetBytes("WAVE"));
                w.Write(Encoding.ASCII.GetBytes("fmt "));
                w.Write(16);
                w.Write((short)1);
                w.Write((short)1);
                w.Write(sampleRate);
                w.Write(sampleRate * 2);
                w.Write((short)2);
                w.Write((short)16);
                w.Write(Encoding.ASCII.GetBytes("data"));
                w.Write(dataBytes);
                foreach (var s in samples)
                    w.Write((short)Math.Round(Math.Max(-1f, Math.Min(1f, s)) * 32767f));
                w.Flush();
                return stream.ToArray();
            }
        }
    }
}
```

- [ ] **Step 4: Jalankan, pastikan lulus**

Procedure R+T dengan `Group = "AmbienceSynthTests|WavWriterTests"`. Expected: `passed=11 failed=0`.

- [ ] **Step 5: Tulis tes aset audio yang gagal**

`Assets/DayCycle/Tests/DayCycleAudioAssetsTests.cs`:
```csharp
using System.IO;
using NUnit.Framework;
using TycoWorld.DayCycle.Authoring;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.DayCycle.Tests
{
    public class DayCycleAudioAssetsTests
    {
        [Test]
        public void AmbienceClips_Exist_AsCompressedLoops()
        {
            foreach (var clip in new[] { DayCycleAudioBuilder.DayClip, DayCycleAudioBuilder.NightClip, DayCycleAudioBuilder.DroneClip })
            {
                Assert.IsNotNull(clip, "Jalankan DayCycleAudioBuilder.WriteMissingClips (lewat Day Cycle Setup).");
                Assert.GreaterOrEqual(clip.length, 10f, clip.name);
                var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip));
                Assert.AreEqual(AudioClipLoadType.CompressedInMemory, importer.defaultSampleSettings.loadType, clip.name);
                Assert.AreEqual(AudioCompressionFormat.Vorbis, importer.defaultSampleSettings.compressionFormat, clip.name);
            }
        }

        [Test]
        public void Sources_ListEveryClipAndLicense()
        {
            string path = WorldGenPaths.ToAbsolute(DayCyclePaths.AudioSourcesFile);
            Assert.IsTrue(File.Exists(path), DayCyclePaths.AudioSourcesFile);
            string text = File.ReadAllText(path);
            foreach (var clip in new[] { DayCycleAudioBuilder.DayClip, DayCycleAudioBuilder.NightClip, DayCycleAudioBuilder.DroneClip })
                StringAssert.Contains(Path.GetFileName(AssetDatabase.GetAssetPath(clip)), text);
            StringAssert.Contains("CC0", text);
        }
    }
}
```

Procedure R+T dengan `Group = "DayCycleAudioAssetsTests"`. Expected: `COMPILE FAILED` (`CS0103 DayCycleAudioBuilder`).

- [ ] **Step 6: Implementasi builder dan aturan import**

`Assets/DayCycle/Editor/DayCycleAudioImportRules.cs`:
```csharp
using System;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.DayCycle.Authoring
{
    /// <summary>Ambience loops: Vorbis, compressed in memory, loaded in the background (long looping clips).</summary>
    public sealed class DayCycleAudioImportRules : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(DayCyclePaths.AudioFolder + "/", StringComparison.Ordinal)) return;
            var importer = (AudioImporter)assetImporter;
            importer.loadInBackground = true;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.6f;
            importer.defaultSampleSettings = settings;
        }
    }
}
```

`Assets/DayCycle/Editor/DayCycleAudioBuilder.cs`:
```csharp
using System;
using System.IO;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.DayCycle.Authoring
{
    /// <summary>Writes the synthesized ambience WAVs that are missing (never overwrites) and loads the three clips.</summary>
    public static class DayCycleAudioBuilder
    {
        public static AudioClip DayClip => Load(DayCyclePaths.DayAmbienceClip);
        public static AudioClip DroneClip => Load(DayCyclePaths.HorrorDroneClip);

        /// <summary>The CC0 cricket loop, or the synthesized night loop when the download is missing.</summary>
        public static AudioClip NightClip
        {
            get
            {
                var clip = Load(DayCyclePaths.NightAmbienceClip);
                return clip != null ? clip : Load(DayCyclePaths.NightAmbienceSynthClip);
            }
        }

        public static void WriteMissingClips()
        {
            AssetFolders.Ensure(DayCyclePaths.AudioFolder);
            WriteIfMissing(DayCyclePaths.DayAmbienceClip, () => AmbienceSynth.Day());
            WriteIfMissing(DayCyclePaths.HorrorDroneClip, () => AmbienceSynth.Drone());
            if (!File.Exists(WorldGenPaths.ToAbsolute(DayCyclePaths.NightAmbienceClip)))
                WriteIfMissing(DayCyclePaths.NightAmbienceSynthClip, () => AmbienceSynth.Night());
        }

        static void WriteIfMissing(string assetPath, Func<float[]> make)
        {
            string full = WorldGenPaths.ToAbsolute(assetPath);
            if (File.Exists(full)) return;
            File.WriteAllBytes(full, WavWriter.Encode(make(), AmbienceSynth.SampleRate));
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        }

        static AudioClip Load(string path) => AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
}
```

`Assets/DayCycle/Editor/DayCyclePaths.cs`: tambahkan setelah baris `NightAmbienceClip`:
```csharp
        public const string NightAmbienceSynthClip = AudioFolder + "/ambience_night.wav";
```

Procedure R+T dengan `Group = "DayCycleAudioAssetsTests"`. Expected: `failed=2` (klip belum ada). Ini RED yang benar.

- [ ] **Step 7: Unduh loop jangkrik CC0**

```bash
DL="D:/Temp/claude/D--UNITY-project-Tyco-World/57a160ea-9ce5-4cb2-9cf1-6426eefd324c/scratchpad/dl-crickets"
mkdir -p "$DL"
curl -sSL --max-time 60 -o "$DL/crickets_1.mp3" "https://opengameart.org/sites/default/files/crickets_1.mp3"
file "$DL/crickets_1.mp3"; ls -l "$DL"
```
Expected:
- `file` melaporkan audio MPEG (ID3 / MPEG ADTS layer III);
- ukuran sekitar 184 KB.

Kalau cocok:
```bash
mkdir -p "/d/UNITY/project/Tyco World/Assets/DayCycle/Audio"
cp "$DL/crickets_1.mp3" "/d/UNITY/project/Tyco World/Assets/DayCycle/Audio/ambience_night_crickets.mp3"
```

Kalau unduhan gagal atau file bukan audio: jangan salin apa pun dan catat `Ruling:` di ledger. `WriteMissingClips` lalu otomatis menulis `ambience_night.wav` hasil sintesis, dan baris jangkrik di SOURCES.md diganti baris sintesis malam.

- [ ] **Step 8: Tulis SOURCES.md**

`Assets/DayCycle/Audio/SOURCES.md`:
```markdown
# Sumber audio ambience (sub-proyek #3)

| File | Sumber | Lisensi |
|---|---|---|
| `ambience_night_crickets.mp3` | "Crickets Ambient Noise - loopable" oleh Wolfgang_, OpenGameArt: https://opengameart.org/content/crickets-ambient-noise-loopable (file asli `crickets_1.mp3`, 11 s) | CC0 1.0 |
| `ambience_day.wav` | Sintesis `AmbienceSynth.Day()` (angin + kicau burung, seed 311, 20 s) | CC0 1.0 (buatan proyek ini) |
| `ambience_horror_drone.wav` | Sintesis `AmbienceSynth.Drone()` (sinus rendah berdenyut + noise gelap, seed 977, 12 s) | CC0 1.0 (buatan proyek ini) |

Loop siang CC0 tanpa musik tidak ditemukan saat pencarian (2026-10-13), jadi siang dan dengung memakai sintesis (spec #3 §5). Ganti kapan saja lewat `Assets/DayCycle/Config/DayCycleProfile.asset`.
```

- [ ] **Step 9: Tulis WAV sintesis dan import**

Snippet MCP:
```csharp
using TycoWorld.DayCycle.Authoring;
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        if (EditorApplication.isCompiling) { result.LogError("STILL COMPILING"); return; }
        DayCycleAudioBuilder.WriteMissingClips();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (var clip in new[] { DayCycleAudioBuilder.DayClip, DayCycleAudioBuilder.NightClip, DayCycleAudioBuilder.DroneClip })
            result.Log(clip == null ? "MISSING" : clip.name + " len=" + clip.length.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " ch=" + clip.channels);
    }
}
```
Expected:
- `ambience_day len=20.0 ch=1`;
- `ambience_night_crickets len≈11.0`;
- `ambience_horror_drone len=12.0 ch=1`.

Kalau mp3 sudah diimport sebelum aturan import ada, jalankan `AssetDatabase.ImportAsset(DayCyclePaths.NightAmbienceClip, ImportAssetOptions.ForceUpdate)` sekali lagi.

- [ ] **Step 10: Jalankan, pastikan lulus**

Procedure R+T dengan `Group = "AmbienceSynthTests|WavWriterTests|DayCycleAudioAssetsTests"`. Expected: `passed=13 failed=0`.

- [ ] **Step 11: Commit**

Procedure C:
- path: `Assets/DayCycle`
- subject: `feat(daycycle): ambience audio (CC0 crickets, synthesized day and drone loops)`

---

### Task 13: Day Cycle Setup, aset konfigurasi, dan integrasi scene

**Files:**
- Create: `Assets/DayCycle/Editor/DayCycleAssetsBuilder.cs`, `Assets/DayCycle/Editor/DayCycleSceneSetup.cs`
- Modify: `Assets/WorldGen/Editor/Preview/PreviewSceneSetup.cs` (`HasLightingOwner`, lewati lampu dan RenderSettings bila ada pemilik)
- Modify: `Assets/Player/Editor/PlayerRigBuilder.cs` (kamera `CameraClearFlags.Skybox`)
- Modify: `docs/superpowers/specs/2026-10-13-day-night-cycle-design.md` (status + §11 catatan implementasi)
- Test: `Assets/DayCycle/Tests/DayCycleSetupTests.cs`, `Assets/Player/Tests/PlayerAssetsTests.cs`
- Output: `Assets/DayCycle/Config/{DayCycleProfile.asset, Volume_Day.asset, Volume_Horror.asset, Sky_Procedural.mat}`, `Assets/Scenes/World.unity`, `Assets/Player/Prefabs/Player.prefab`

**Interfaces:**
- Consumes:
  - semua runtime DayCycle;
  - `DayCycleAudioBuilder` (Task 12);
  - `WorldScenes.CheckSaved` / `EnsureWorldSceneOpen` / `OpenGeneratedSceneAdditive`, `AssetFolders.Ensure`;
  - `PlayerSceneSetup.Run(bool, bool, Vector2, out string)`, `PreviewSceneSetup.Run(WorldGenSettings, bool, out string)`, `DefaultSettingsFactory.LoadOrCreateAsset()`.
- Produces:
  - `DayCycleAssets { Profile, DayVolume, HorrorVolume, Skybox }`.
  - `DayCycleAssetsBuilder` dengan `BuildAll()`, `EnsureClips(DayCycleProfile, AudioClip, AudioClip, AudioClip)` → bool, `EnsureDayVolume(VolumeProfile)` → bool, `EnsureHorrorVolume(VolumeProfile)` → bool, `CreateSkybox()` → Material.
  - `DayCycleSceneSetup.Run(bool interactive, out string error)` dan menu "Tyco World/Day Cycle Setup".
  - `PreviewSceneSetup.HasLightingOwner(IEnumerable<GameObject>)` → bool.

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/DayCycle/Tests/DayCycleSetupTests.cs`:
```csharp
using System.Collections.Generic;
using NUnit.Framework;
using TycoWorld.DayCycle.Authoring;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TycoWorld.DayCycle.Tests
{
    public class DayCycleSetupTests
    {
        readonly List<Object> created = new List<Object>();

        T Track<T>(T obj) where T : Object
        {
            created.Add(obj);
            return obj;
        }

        VolumeProfile NewProfile() => Track(ScriptableObject.CreateInstance<VolumeProfile>());

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in created)
            {
                if (obj is VolumeProfile profile)
                    foreach (var component in profile.components) Object.DestroyImmediate(component);
                if (obj != null) Object.DestroyImmediate(obj);
            }
            created.Clear();
        }

        [Test]
        public void DayVolume_OverridesEveryNightParameter()
        {
            var night = AssetDatabase.LoadAssetAtPath<VolumeProfile>(WorldGenPaths.VolumeProfileAsset);
            Assert.IsNotNull(night, WorldGenPaths.VolumeProfileAsset);
            var day = NewProfile();
            DayCycleAssetsBuilder.EnsureDayVolume(day);
            foreach (var n in night.components)
            {
                Assert.IsTrue(day.TryGet(n.GetType(), out VolumeComponent d), n.GetType().Name);
                for (int i = 0; i < n.parameters.Count; i++)
                    if (n.parameters[i].overrideState)
                        Assert.IsTrue(d.parameters[i].overrideState, n.GetType().Name + " parameter " + i);
            }
        }

        [Test]
        public void DayVolume_MatchesSpec()
        {
            var day = NewProfile();
            DayCycleAssetsBuilder.EnsureDayVolume(day);
            Assert.IsTrue(day.TryGet(out FilmGrain grain));
            Assert.AreEqual(0.15f, grain.intensity.value, 1e-4f);
            Assert.IsTrue(day.TryGet(out Vignette vignette));
            Assert.AreEqual(0.15f, vignette.intensity.value, 1e-4f);
            Assert.IsTrue(day.TryGet(out ColorAdjustments color));
            Assert.AreEqual(5f, color.saturation.value, 1e-4f);
            Assert.IsTrue(day.TryGet(out Bloom bloom));
            Assert.AreEqual(0.3f, bloom.intensity.value, 1e-4f);
            Assert.IsTrue(day.TryGet(out ChromaticAberration ca));
            Assert.AreEqual(0f, ca.intensity.value, 1e-4f);
            Assert.IsTrue(day.TryGet(out Tonemapping tonemapping));
            Assert.AreEqual(TonemappingMode.ACES, tonemapping.mode.value);
        }

        [Test]
        public void HorrorVolume_MatchesSpec()
        {
            var horror = NewProfile();
            DayCycleAssetsBuilder.EnsureHorrorVolume(horror);
            Assert.IsTrue(horror.TryGet(out FilmGrain grain));
            Assert.AreEqual(0.8f, grain.intensity.value, 1e-4f);
            Assert.IsTrue(horror.TryGet(out Vignette vignette));
            Assert.AreEqual(0.5f, vignette.intensity.value, 1e-4f);
            Assert.IsTrue(horror.TryGet(out ChromaticAberration ca));
            Assert.AreEqual(0.3f, ca.intensity.value, 1e-4f);
            Assert.IsTrue(horror.TryGet(out ColorAdjustments color));
            Assert.AreEqual(-45f, color.saturation.value, 1e-4f);
            Assert.AreEqual(20f, color.contrast.value, 1e-4f);
        }

        [Test]
        public void EnsureDayVolume_KeepsExisting()
        {
            var day = NewProfile();
            Assert.IsTrue(DayCycleAssetsBuilder.EnsureDayVolume(day));
            day.TryGet(out FilmGrain grain);
            grain.intensity.Override(0.42f);
            Assert.IsFalse(DayCycleAssetsBuilder.EnsureDayVolume(day));
            Assert.AreEqual(0.42f, grain.intensity.value, 1e-5f);
            Assert.AreEqual(6, day.components.Count);
        }

        [Test]
        public void EnsureHorrorVolume_KeepsExisting()
        {
            var horror = NewProfile();
            Assert.IsTrue(DayCycleAssetsBuilder.EnsureHorrorVolume(horror));
            horror.TryGet(out Vignette vignette);
            vignette.intensity.Override(0.7f);
            Assert.IsFalse(DayCycleAssetsBuilder.EnsureHorrorVolume(horror));
            Assert.AreEqual(0.7f, vignette.intensity.value, 1e-5f);
            Assert.AreEqual(4, horror.components.Count);
        }

        [Test]
        public void EnsureClips_KeepsExisting()
        {
            var profile = Track(DayCycleProfile.CreateDefault());
            var mine = Track(AudioClip.Create("mine", 100, 1, 22050, false));
            var day = Track(AudioClip.Create("day", 100, 1, 22050, false));
            var night = Track(AudioClip.Create("night", 100, 1, 22050, false));
            var drone = Track(AudioClip.Create("drone", 100, 1, 22050, false));
            profile.dayClip = mine;
            Assert.IsTrue(DayCycleAssetsBuilder.EnsureClips(profile, day, night, drone));
            Assert.AreSame(mine, profile.dayClip);
            Assert.AreSame(night, profile.nightClip);
            Assert.AreSame(drone, profile.horrorDroneClip);
            Assert.IsFalse(DayCycleAssetsBuilder.EnsureClips(profile, day, night, drone));
        }

        [Test]
        public void Skybox_IsProceduralWithDayExposure()
        {
            var sky = Track(DayCycleAssetsBuilder.CreateSkybox());
            Assert.AreEqual("Skybox/Procedural", sky.shader.name);
            Assert.AreEqual(1.3f, sky.GetFloat("_Exposure"), 1e-4f);
        }

        [Test]
        public void PreviewSetup_SeesDirectorAsLightingOwner()
        {
            var owner = Track(new GameObject("DayCycle"));
            owner.AddComponent<DayCycleDirector>();
            var plain = Track(new GameObject("Plain"));
            Assert.IsTrue(PreviewSceneSetup.HasLightingOwner(new[] { plain, owner }));
            Assert.IsFalse(PreviewSceneSetup.HasLightingOwner(new[] { plain }));
        }
    }
}
```

`Assets/Player/Tests/PlayerAssetsTests.cs`: di `Rig_HasOneCameraAndListener_AtEyeHeight_OnPlayerLayer`, setelah `Assert.AreEqual(70f, cam.fieldOfView, 1e-3f);` tambahkan:
```csharp
            Assert.AreEqual(CameraClearFlags.Skybox, cam.clearFlags);
```

- [ ] **Step 2: Jalankan, pastikan gagal**

Procedure R+T dengan `Group = "DayCycleSetupTests|PlayerAssetsTests"`. Expected: `COMPILE FAILED` (`CS0103 DayCycleAssetsBuilder`, `CS0117 HasLightingOwner`).

- [ ] **Step 3: Implementasi builder aset**

`Assets/DayCycle/Editor/DayCycleAssetsBuilder.cs`:
```csharp
using System;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TycoWorld.DayCycle.Authoring
{
    public sealed class DayCycleAssets
    {
        public DayCycleProfile Profile;
        public VolumeProfile DayVolume, HorrorVolume;
        public Material Skybox;
    }

    /// <summary>Creates the day cycle assets once; re-running never overwrites what the user tuned (spec #3 §6).</summary>
    public static class DayCycleAssetsBuilder
    {
        public static DayCycleAssets BuildAll()
        {
            AssetFolders.Ensure(DayCyclePaths.ConfigFolder);
            DayCycleAudioBuilder.WriteMissingClips();
            var assets = new DayCycleAssets
            {
                Profile = LoadOrCreate(DayCyclePaths.ProfileAsset, DayCycleProfile.CreateDefault),
                DayVolume = LoadOrCreate(DayCyclePaths.DayVolumeAsset, ScriptableObject.CreateInstance<VolumeProfile>),
                HorrorVolume = LoadOrCreate(DayCyclePaths.HorrorVolumeAsset, ScriptableObject.CreateInstance<VolumeProfile>),
                Skybox = LoadOrCreate(DayCyclePaths.SkyboxMaterial, CreateSkybox)
            };
            if (EnsureClips(assets.Profile, DayCycleAudioBuilder.DayClip, DayCycleAudioBuilder.NightClip, DayCycleAudioBuilder.DroneClip))
                EditorUtility.SetDirty(assets.Profile);
            if (EnsureDayVolume(assets.DayVolume)) EditorUtility.SetDirty(assets.DayVolume);
            if (EnsureHorrorVolume(assets.HorrorVolume)) EditorUtility.SetDirty(assets.HorrorVolume);
            AssetDatabase.SaveAssets();
            return assets;
        }

        /// <summary>Fills only the clip slots that are still empty.</summary>
        public static bool EnsureClips(DayCycleProfile profile, AudioClip day, AudioClip night, AudioClip drone)
        {
            bool changed = false;
            if (profile.dayClip == null && day != null) { profile.dayClip = day; changed = true; }
            if (profile.nightClip == null && night != null) { profile.nightClip = night; changed = true; }
            if (profile.horrorDroneClip == null && drone != null) { profile.horrorDroneClip = drone; changed = true; }
            return changed;
        }

        /// <summary>Day look; overrides every parameter the night VHS profile overrides, so its weight is a true blend.</summary>
        public static bool EnsureDayVolume(VolumeProfile profile)
        {
            bool changed = false;
            if (TryAdd(profile, out Tonemapping tonemapping)) { tonemapping.mode.Override(TonemappingMode.ACES); changed = true; }
            if (TryAdd(profile, out ColorAdjustments color)) { color.saturation.Override(5f); color.contrast.Override(0f); changed = true; }
            if (TryAdd(profile, out FilmGrain grain)) { grain.intensity.Override(0.15f); changed = true; }
            if (TryAdd(profile, out Vignette vignette)) { vignette.intensity.Override(0.15f); changed = true; }
            if (TryAdd(profile, out ChromaticAberration ca)) { ca.intensity.Override(0f); changed = true; }
            if (TryAdd(profile, out Bloom bloom)) { bloom.intensity.Override(0.3f); bloom.threshold.Override(1f); changed = true; }
            return changed;
        }

        public static bool EnsureHorrorVolume(VolumeProfile profile)
        {
            bool changed = false;
            if (TryAdd(profile, out ColorAdjustments color)) { color.saturation.Override(-45f); color.contrast.Override(20f); changed = true; }
            if (TryAdd(profile, out FilmGrain grain)) { grain.intensity.Override(0.8f); changed = true; }
            if (TryAdd(profile, out Vignette vignette)) { vignette.intensity.Override(0.5f); changed = true; }
            if (TryAdd(profile, out ChromaticAberration ca)) { ca.intensity.Override(0.3f); changed = true; }
            return changed;
        }

        public static Material CreateSkybox()
        {
            var sky = new Material(Shader.Find("Skybox/Procedural")) { name = "Sky_Procedural" };
            sky.SetFloat("_SunSize", 0.04f);
            sky.SetFloat("_SunSizeConvergence", 5f);
            sky.SetFloat("_AtmosphereThickness", 1f);
            sky.SetColor("_SkyTint", new Color(0.5f, 0.5f, 0.5f));
            sky.SetColor("_GroundColor", new Color(0.37f, 0.35f, 0.34f));
            sky.SetFloat("_Exposure", 1.3f);
            return sky;
        }

        static T LoadOrCreate<T>(string path, Func<T> create) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = create();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static bool TryAdd<T>(VolumeProfile profile, out T component) where T : VolumeComponent
        {
            if (profile.TryGet(out component)) return false;
            component = profile.Add<T>(true);
            component.name = typeof(T).Name;
            if (EditorUtility.IsPersistent(profile)) AssetDatabase.AddObjectToAsset(component, profile);
            return true;
        }
    }
}
```

- [ ] **Step 4: Pemilik pencahayaan di PreviewSceneSetup**

`Assets/WorldGen/Editor/Preview/PreviewSceneSetup.cs`:

1. Di `Run`, ganti blok dari `var moonGo = FindOrCreate(world, "Moon");` sampai `RenderSettings.sun = moon;` (termasuk semua baris RenderSettings di antaranya) dengan:
```csharp
            if (HasLightingOwner(world.GetRootGameObjects()))
                Debug.Log("[WorldGen] Pencahayaan dikelola siklus pagi/malam; Moon dan RenderSettings tidak diubah.");
            else
                ApplyNightLighting(world);
```
2. Tambahkan dua method di bawah `Run`. Isi `ApplyNightLighting` adalah baris yang dipindahkan apa adanya:
```csharp
        /// <summary>True when a root object (or a child) drives scene lighting at runtime, e.g. the day cycle director.</summary>
        public static bool HasLightingOwner(IEnumerable<GameObject> roots)
        {
            foreach (var go in roots)
                if (go != null && go.GetComponentInChildren<ISceneLightingOwner>(true) != null) return true;
            return false;
        }

        static void ApplyNightLighting(Scene world)
        {
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
        }
```
3. Ubah ringkasan kelas menjadi `/// <summary>Fills World.unity with night lighting (unless a runtime lighting owner exists), fog, a VHS volume and a street-level preview camera (spec section 13).</summary>`.

`Assets/Player/Editor/PlayerRigBuilder.cs`: ganti `cam.clearFlags = CameraClearFlags.SolidColor;` menjadi `cam.clearFlags = CameraClearFlags.Skybox;`. Baris `backgroundColor` tetap dipakai sebagai cadangan kalau skybox kosong.

- [ ] **Step 5: Implementasi Day Cycle Setup**

`Assets/DayCycle/Editor/DayCycleSceneSetup.cs`:
```csharp
using TycoWorld.Player;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TycoWorld.DayCycle.Authoring
{
    /// <summary>"Tyco World/Day Cycle Setup": wires the day cycle into World.unity; safe to re-run (spec #3 §6).</summary>
    public static class DayCycleSceneSetup
    {
        public const string RootName = "DayCycle";
        public const string SunName = "Sun";
        public const string MoonName = "Moon";
        public const string NightVolumeName = "Global Volume";
        public const string DayVolumeName = "Volume_Day";
        public const string HorrorVolumeName = "Volume_Horror";
        public const string PlayerName = "Player";
        public const string CameraWarning = "[DayCycle] Kamera pemain belum memakai Skybox. Jalankan \"Tyco World/Player Setup\" ulang.";
        public const string NoPlayerWarning = "[DayCycle] Player tidak ada di World.unity; jam dan pesan HUD tidak tampil. Jalankan \"Tyco World/Player Setup\".";

        [MenuItem("Tyco World/Day Cycle Setup")]
        static void SetupMenu()
        {
            if (!Run(true, out string error)) EditorUtility.DisplayDialog("Day Cycle Setup gagal", error, "OK");
        }

        public static bool Run(bool interactive, out string error)
        {
            if (!WorldScenes.CheckSaved(interactive, out error)) return false;
            var world = WorldScenes.EnsureWorldSceneOpen();
            var moonGo = FindRoot(world, MoonName);
            var nightGo = FindRoot(world, NightVolumeName);
            if (moonGo == null || moonGo.GetComponent<Light>() == null || nightGo == null || nightGo.GetComponent<Volume>() == null)
            {
                error = "Moon atau Global Volume tidak ada di World.unity. Jalankan \"Tyco World/World Generator/Setup Preview Scene\" dulu.";
                return false;
            }
            var assets = DayCycleAssetsBuilder.BuildAll();
            SceneManager.SetActiveScene(world);

            var sun = GetOrAdd<Light>(FindOrCreate(world, SunName));
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.color = assets.Profile.sunNoonColor;
            sun.intensity = assets.Profile.sunIntensity;
            sun.enabled = false;

            var day = EnsureVolume(world, DayVolumeName, 1f, assets.DayVolume);
            var horror = EnsureVolume(world, HorrorVolumeName, 2f, assets.HorrorVolume);

            var root = FindOrCreate(world, RootName);
            var ambience = GetOrAdd<AmbienceAudio>(root);
            var director = GetOrAdd<DayCycleDirector>(root);
            GetOrAdd<DayCycleDebug>(root);
            var playerGo = FindRoot(world, PlayerName);
            var player = playerGo != null ? playerGo.GetComponent<FirstPersonPlayer>() : null;
            director.Configure(assets.Profile, sun, moonGo.GetComponent<Light>(), day, nightGo.GetComponent<Volume>(), horror,
                assets.Skybox, ambience, player);
            EditorUtility.SetDirty(director);
            RenderSettings.skybox = assets.Skybox;
            if (player == null) Debug.LogWarning(NoPlayerWarning);
            else if (!UsesSkybox(player)) Debug.LogWarning(CameraWarning);

            EditorSceneManager.MarkSceneDirty(world);
            EditorSceneManager.SaveScene(world);
            WorldScenes.OpenGeneratedSceneAdditive();
            SceneManager.SetActiveScene(world);
            return true;
        }

        static bool UsesSkybox(FirstPersonPlayer player)
        {
            var cam = player.View != null ? player.View.GetComponent<Camera>() : null;
            return cam != null && cam.clearFlags == CameraClearFlags.Skybox;
        }

        static Volume EnsureVolume(Scene world, string name, float priority, VolumeProfile profile)
        {
            var volume = GetOrAdd<Volume>(FindOrCreate(world, name));
            volume.isGlobal = true;
            volume.priority = priority;
            volume.weight = 0f;
            volume.sharedProfile = profile;
            return volume;
        }

        static GameObject FindRoot(Scene scene, string name)
        {
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == name) return go;
            return null;
        }

        static GameObject FindOrCreate(Scene scene, string name)
        {
            var go = FindRoot(scene, name);
            if (go != null) return go;
            go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            if (component == null) component = go.AddComponent<T>();
            return component;
        }
    }
}
```

- [ ] **Step 6: Jalankan, pastikan lulus**

Procedure R+T dengan `Group = "DayCycleSetupTests|PlayerAssetsTests"`. Expected: `failed=0`, dengan 8 tes DayCycleSetupTests lulus.

Lalu `Group = "."`. Expected: `failed=0`.

- [ ] **Step 7: Integrasi scene (MCP): Player Setup, Day Cycle Setup dua kali, lalu Setup Preview Scene**

Snippet MCP:
```csharp
using System.Globalization;
using TycoWorld.DayCycle;
using TycoWorld.DayCycle.Authoring;
using TycoWorld.Player.Authoring;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        EditorSceneManager.SaveOpenScenes();
        if (!PlayerSceneSetup.Run(false, false, Vector2.zero, out string error)) { result.LogError("player: " + error); return; }
        if (!DayCycleSceneSetup.Run(false, out error)) { result.LogError("daycycle 1: " + error); return; }
        var profile = AssetDatabase.LoadAssetAtPath<DayCycleProfile>(DayCyclePaths.ProfileAsset);
        profile.dayVolume = 0.33f;
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        if (!DayCycleSceneSetup.Run(false, out error)) { result.LogError("daycycle 2: " + error); return; }
        if (!PreviewSceneSetup.Run(DefaultSettingsFactory.LoadOrCreateAsset(), false, out error)) { result.LogError("preview: " + error); return; }

        var world = WorldScenes.EnsureWorldSceneOpen();
        int root = 0, sun = 0, dayVol = 0, horrorVol = 0;
        foreach (var go in world.GetRootGameObjects())
        {
            if (go.name == DayCycleSceneSetup.RootName) root++;
            if (go.name == DayCycleSceneSetup.SunName) sun++;
            if (go.name == DayCycleSceneSetup.DayVolumeName) dayVol++;
            if (go.name == DayCycleSceneSetup.HorrorVolumeName) horrorVol++;
        }
        var d = Object.FindAnyObjectByType<DayCycleDirector>();
        result.Log("roots DayCycle=" + root + " Sun=" + sun + " Volume_Day=" + dayVol + " Volume_Horror=" + horrorVol);
        result.Log("profile.dayVolume=" + profile.dayVolume.ToString("F2", CultureInfo.InvariantCulture)
            + " clips=" + (profile.dayClip != null) + "/" + (profile.nightClip != null) + "/" + (profile.horrorDroneClip != null));
        result.Log("wired profile=" + (d.Profile == profile) + " sun=" + (d.Sun != null) + " moon=" + (d.Moon != null)
            + " day=" + (d.DayVolume != null) + " night=" + (d.NightVolume != null) + " horror=" + (d.HorrorVolume != null)
            + " sky=" + (d.SkyboxMaterial != null) + " ambience=" + (d.Ambience != null) + " player=" + (d.Player != null));
        result.Log("skybox=" + (RenderSettings.skybox != null ? RenderSettings.skybox.name : "null"));
        var cam = d.Player.View.GetComponent<Camera>();
        result.Log("playerCam=" + cam.clearFlags);

        profile.dayVolume = 0.35f;
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
    }
}
```
Expected:
- `roots DayCycle=1 Sun=1 Volume_Day=1 Volume_Horror=1`;
- `profile.dayVolume=0.33 clips=True/True/True` (tuning user dipertahankan setelah Setup kedua);
- `wired profile=True sun=True moon=True day=True night=True horror=True sky=True ambience=True player=True`;
- `skybox=Sky_Procedural` (Setup Preview Scene tidak menimpa pencahayaan);
- `playerCam=Skybox`.

Lalu `mcp__unity-mcp__Unity_GetConsoleLogs` (`logTypes: "error,warning"`). Expected: tanpa error dan tanpa `[DayCycle]` warning. Log info `[WorldGen] Pencahayaan dikelola siklus pagi/malam…` boleh muncul.

- [ ] **Step 8: Catatan implementasi di spec**

`docs/superpowers/specs/2026-10-13-day-night-cycle-design.md`:
- ganti `- **Status:** Menunggu review` menjadi `- **Status:** Disetujui (plan: docs/superpowers/plans/2026-10-13-day-night-cycle.md)`;
- tambahkan di akhir file:
```markdown

## 11. Catatan implementasi (dari plan)

- **Lapisan Volume.** URP mencampur Volume global urut prioritas.
  - Malam (`Global Volume`, `Preview_NightVHS`, prioritas 0) jadi dasar dengan bobot tetap 1.
  - Siang (prioritas 1, bobot = bobot Siang) meng-override semua parameter yang di-override Malam, sehingga hasilnya sama dengan lerp(Malam, Siang). Ini menggantikan rumus "Malam = 1 − Siang" di §4.
  - Horor (prioritas 2, bobot = bobot Horor) di atas keduanya.
- **`DayCycleProfile.Evaluate(hours)`** menghitung bobot horor sendiri dari jam. Tidak ada parameter `horrorWeight`.
- **`StreetLampSystem`** adalah kelas C# milik director, bukan MonoBehaviour. Kepala lampu memakai dua salinan material runtime ("On" dengan emisi × intensitas, "Off" hitam) yang dipakai bersama semua lampu. `sharedMaterials` hanya diganti saat status nyala berubah.
- **Kamera pemain** memakai `CameraClearFlags.Skybox` dari `PlayerRigBuilder`. Day Cycle Setup memberi warning kalau kamera di scene belum skybox, yang berarti Player Setup perlu dijalankan ulang.
- **Matahari** dimatikan di Edit mode; director menyalakannya saat Play. Di editor, skybox scene = `Sky_Procedural.mat`; saat Play dipakai salinannya.
- **Audio.**
  - Malam: "Crickets Ambient Noise – loopable" (Wolfgang_, OpenGameArt, CC0).
  - Siang dan dengung: sintesis `AmbienceSynth`.
  - Kalau unduhan jangkrik hilang, `ambience_night.wav` hasil sintesis dipakai.
- **`DayCycleDirector.FindWorldRoot`** adalah hook `Func<GeneratedRoot>`, karena dunia ada di scene lain dan tidak bisa diserialisasi. Lampu dicari tiap 0,5 s sampai `GeneratedRoot` ada.
```

- [ ] **Step 9: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" status --short
```
Expected (selain `ProjectSettings/ProjectSettings.asset` milik user):
- file baru di `Assets/DayCycle/Editor` dan `Assets/DayCycle/Config`, serta `Assets/DayCycle/Tests/DayCycleSetupTests.cs`;
- file yang berubah: `PreviewSceneSetup.cs`, `PlayerRigBuilder.cs`, `PlayerAssetsTests.cs`, `Assets/Player/Prefabs/Player.prefab`, `Assets/Scenes/World.unity`, dan spec.

Kalau ada asset Player lain yang berubah, periksa diff-nya dulu dan add hanya yang memang berubah karena kamera.

Procedure C:
- path: `Assets/DayCycle` `Assets/WorldGen/Editor/Preview/PreviewSceneSetup.cs` `Assets/Player/Editor/PlayerRigBuilder.cs` `Assets/Player/Tests/PlayerAssetsTests.cs` `Assets/Player/Prefabs/Player.prefab` `Assets/Scenes/World.unity` `docs/superpowers/specs/2026-10-13-day-night-cycle-design.md`
- subject: `feat(daycycle): day cycle setup menu, config assets and World.unity wiring`

---

### Task 14: Verifikasi Play (MCP) dan catatan verifikasi

**Files:**
- Create: `Assets/DayCycle/Tests/McpDayCycleDriver.cs` (helper dev, bukan tes)
- Create: `docs/superpowers/notes/2026-10-13-day-night-verification.md`

**Interfaces:**
- Consumes:
  - `DayCycleDirector` (`State`, `Clock`, `Lamps`, `Ambience`, `Player`, `Sun`, `Moon`, events);
  - `DayCycleDebug.ApplyKeys`;
  - `TycoWorld.Player.Tests.McpRender.SavePng(Camera, string, int, int)` (sudah ada).
- Produces: `McpDayCycleDriver.Install()`, `Status()`, `SetTime(float)`, `Keys(bool jump, bool slower, bool faster, bool clock)`. Semuanya mengembalikan string.

**Catatan Play lewat MCP:**
- Pesan HUD hilang setelah 2,5 s (`HudTuning.messageSeconds`), sedangkan jeda antar panggilan MCP bisa lebih lama. Karena itu driver menyimpan pesan HUD tepat saat event horor terjadi (`event=`).
- Editor tidak fokus, jadi loop Play beku di frame 2 kecuali `Application.runInBackground = true`. `Install()` menyetel ini hanya untuk sesi runtime itu; PlayerSettings tidak berubah.
- Setiap panggilan MCP terpisah memberi jeda beberapa frame.

- [ ] **Step 1: Tulis helper**

`Assets/DayCycle/Tests/McpDayCycleDriver.cs`:
```csharp
using System.Globalization;
using System.Text;
using UnityEngine;

namespace TycoWorld.DayCycle.Tests
{
    /// <summary>Dev helper for MCP verification in Play mode (not a test). Install() resets its counters per director.</summary>
    public static class McpDayCycleDriver
    {
        static DayCycleDirector hooked;
        static int started, ended;
        static string lastEvent = "";

        static DayCycleDirector Director => Object.FindAnyObjectByType<DayCycleDirector>();

        public static string Install()
        {
            Application.runInBackground = true;
            var d = Director;
            if (d == null) return "NO DIRECTOR";
            if (hooked != d)
            {
                started = 0;
                ended = 0;
                lastEvent = "";
                d.HorrorStarted += () => { started++; lastEvent = HudMessage(d); };
                d.HorrorEnded += () => { ended++; lastEvent = HudMessage(d); };
                hooked = d;
            }
            return Status();
        }

        public static string SetTime(float hours)
        {
            var d = Director;
            if (d == null || !d.IsInitialized) return "NOT READY";
            d.Clock.SetTime(hours);
            return Status();
        }

        public static string Keys(bool jump, bool slower, bool faster, bool clock)
        {
            var debug = Object.FindAnyObjectByType<DayCycleDebug>();
            if (debug == null) return "NO DEBUG";
            bool applied = debug.ApplyKeys(new DebugKeys { JumpTime = jump, SlowerSpeed = slower, FasterSpeed = faster, ToggleClock = clock });
            return "applied=" + applied + " " + Status();
        }

        public static string Status()
        {
            var d = Director;
            if (d == null || !d.IsInitialized) return "NOT READY";
            var s = d.State;
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("t=").Append(d.Clock.Display).Append(" day=").Append(d.Clock.Day)
                .Append(" speed=x").Append(d.Clock.SpeedMultiplier.ToString("0", inv))
                .Append(" phase=").Append(d.Phase).Append(" horror=").Append(d.IsHorror)
                .Append(" started=").Append(started).Append(" ended=").Append(ended)
                .Append(" event='").Append(lastEvent).Append(''')
                .Append(" sunElev=").Append(s.SunElevation.ToString("F1", inv))
                .Append(" sun=").Append(d.Sun != null && d.Sun.enabled)
                .Append(" moon=").Append(d.Moon != null && d.Moon.enabled)
                .Append(" fog=").Append(RenderSettings.fogDensity.ToString("F4", inv))
                .Append(" dayW=").Append(s.DayVolumeWeight.ToString("F2", inv))
                .Append(" horrorW=").Append(s.HorrorWeight.ToString("F2", inv))
                .Append(" lamps=").Append(d.Lamps.Count).Append(" lit=").Append(d.Lamps.LitCount)
                .Append(" flicker=").Append(d.Lamps.RoleCount(LampRole.Flicker))
                .Append(" dead=").Append(d.Lamps.RoleCount(LampRole.Dead));
            if (d.Ambience != null && d.Ambience.Day != null)
                sb.Append(" amb=").Append(d.Ambience.Day.volume.ToString("F2", inv))
                    .Append('/').Append(d.Ambience.Night.volume.ToString("F2", inv))
                    .Append('/').Append(d.Ambience.Drone.volume.ToString("F2", inv));
            var player = d.Player;
            if (player != null && player.IsInitialized)
                sb.Append(" msg='").Append(player.Hud.Message).Append("' clock='")
                    .Append(player.Hud.ClockVisible ? player.Hud.Clock : "").Append('\'');
            sb.Append(" fps=").Append((1f / Mathf.Max(Time.smoothDeltaTime, 1e-4f)).ToString("F0", inv));
            return sb.ToString();
        }

        /// <summary>The HUD message right after an event fired (messages fade after 2.5 s, MCP polls are slower).</summary>
        static string HudMessage(DayCycleDirector d) => d.Player != null && d.Player.IsInitialized ? d.Player.Hud.Message : "";
    }
}
```

Procedure R+T dengan `Group = "DayCycleSmokeTests"`. Expected: `passed=3 failed=0` (memastikan helper ikut ter-compile).

- [ ] **Step 2: Masuk Play**

```bash
git -C "D:/UNITY/project/Tyco World" status --short > "D:/UNITY/project/Tyco World/.superpowers/sdd/2026-10-13-day-night-cycle/status_before_play.txt"
```

Snippet MCP:
```csharp
using UnityEditor;
using UnityEditor.SceneManagement;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        EditorSceneManager.SaveOpenScenes();
        EditorApplication.isPlaying = true;
        result.Log("entering play");
    }
}
```

- [ ] **Step 3: Pagi 06:30**

Panggil `result.Log(TycoWorld.DayCycle.Tests.McpDayCycleDriver.Install());` dalam snippet `CommandScript`. Kalau hasilnya `NOT READY`, ulangi.

Expected:
- `t=06:3x day=1 phase=Day horror=False started=0 ended=0`;
- `sun=True moon=False`;
- `dayW` sekitar 0,97, `horrorW=0.00`;
- `lamps=876 lit=0`;
- `amb=0.35/0.00/0.00`.

Lalu screenshot:
```csharp
using UnityEngine;

internal class CommandScript : IRunCommand
{
    const string Dir = "D:/UNITY/project/Tyco World/.superpowers/sdd/2026-10-13-day-night-cycle";

    public void Execute(ExecutionResult result)
    {
        result.Log(TycoWorld.Player.Tests.McpRender.SavePng(Camera.main, Dir + "/shot_0630.png", 1280, 720));
        result.Log(TycoWorld.DayCycle.Tests.McpDayCycleDriver.Status());
    }
}
```
Baca PNG dengan Read. Expected: langit biru terang, bayangan matahari, dan tanpa kabut gelap.

- [ ] **Step 4: Siang dan senja**

1. Panggil `McpDayCycleDriver.SetTime(12f)`, lalu di panggilan berikutnya simpan screenshot `shot_1200.png` + Status.
   Expected: `sunElev` sekitar 75, `fog=0.0040`, `dayW=1.00`.
2. Panggil `SetTime(18f)`, lalu di panggilan berikutnya simpan `shot_1800.png` + Status.
   Expected:
   - `sun=False moon=True` (elevasi −3,75°);
   - `lit` > 800;
   - `amb=0.18/0.20/0.00`;
   - langit jingga atau gelap di screenshot.
3. Catat `fps` siang (risiko bayangan di spec §9).

- [ ] **Step 5: T → 19:30 (horor)**

1. Panggil `McpDayCycleDriver.SetTime(12f)`, lalu `McpDayCycleDriver.Keys(true, false, false, false)`.
   Expected: `applied=True t=19:30`.
2. Di panggilan berikutnya, panggil `Status()`.
   Expected:
   - `phase=Night horror=True started=1`;
   - `event='Malam semakin larut…'` (pesan HUD tampil 2,5 s, jadi `msg` bisa sudah kosong);
   - `dayW=0.00 horrorW=1.00 fog=0.0320`;
   - `flicker` sekitar 70 dan `dead` sekitar 26;
   - `lit` antara `876 − dead − flicker` dan `876 − dead`;
   - `amb=0.00/0.40/0.30`.
3. Simpan screenshot `shot_1930.png`. Expected: gelap, lampu jalan menyala, grain dan desaturasi.
4. Panggil `Status()` tiga kali lagi (panggilan MCP terpisah). Expected: angka `lit` berubah-ubah (ada lampu berkedip) dan `started` tetap 1.

- [ ] **Step 6: Kecepatan x60 sampai subuh, lalu F3**

1. Panggil `SetTime(4.95f)`, lalu `Keys(false, false, true, false)` dua kali.
   Expected: `speed=x60`; pesan terakhir `Kecepatan waktu x60`.
2. Tunggu, lalu panggil `Status()` sampai `t` ≥ `05:00` (sekitar 3 detik nyata).
   Expected: `horror=False ended=1 event='Subuh. Malam sudah lewat.'`.
3. Panggil `Keys(false, false, false, true)`, lalu `Status()`. Expected: `clock='05:..'` tampil.
4. Panggil `Keys(false, true, false, false)` dua kali. Expected: kembali `speed=x1`.

- [ ] **Step 7: Keluar Play, cek Console dan asset**

Snippet: `EditorApplication.isPlaying = false;`.

`mcp__unity-mcp__Unity_GetConsoleLogs` dengan `logTypes: "error,warning"`. Expected: tidak ada error atau exception, dan tidak ada warning `[DayCycle]`.

```bash
git -C "D:/UNITY/project/Tyco World" status --short
```
Expected: sama persis dengan `status_before_play.txt`. Tidak boleh ada material, profil, atau scene yang berubah karena Play.

- [ ] **Step 8: Kirim screenshot ke user**

Muat `SendUserFile` lewat ToolSearch (`select:SendUserFile`), lalu kirim `shot_0630.png`, `shot_1200.png`, `shot_1800.png`, dan `shot_1930.png` dari folder ledger.

- [ ] **Step 9: Suite penuh**

Procedure R+T dengan `Group = "."`. Expected: `passed=542 failed=0`. Angkanya adalah 428 tes lama ditambah 114 tes baru. Kalau berbeda, catat jumlah aktual dan penyebabnya di ledger.

- [ ] **Step 10: Catatan verifikasi**

`docs/superpowers/notes/2026-10-13-day-night-verification.md`. Struktur ini diisi dengan angka nyata dari output Step 3–9, bukan dari Expected:
```markdown
# Siklus pagi/malam — verifikasi (2026-10-13)

- **Tes EditMode:** <passed> lulus (WorldGen + Player + DayCycle), failed=0.
- **Generate ulang (Task 6):** <output CompareChunkHashes>; lampsPlaced=<n> marked=<n> pointLights=<n>.
- **Day Cycle Setup (Task 13):**
  - dijalankan dua kali tanpa duplikat (<output roots>);
  - tuning profil dipertahankan;
  - Setup Preview Scene tidak menimpa skybox (<output skybox>);
  - kamera pemain <output playerCam>.
- **Play (MCP, runInBackground runtime saja):**
  1. 06:30 — <Status>. Screenshot shot_0630.
  2. 12:00 — <Status>, fps <n>. Screenshot shot_1200.
  3. 18:00 — <Status>. Screenshot shot_1800.
  4. T → 19:30 — <Status>. Pesan HUD "Malam semakin larut…". Screenshot shot_1930.
  5. Kedip — lit berturut-turut <a>, <b>, <c>; started tetap 1.
  6. x60 → 05:00 — <Status>; pesan "Subuh. Malam sudah lewat."
  7. F3 — jam tampil <clock>.
- **Console setelah Play:** <hasil>. **git status sebelum/sesudah Play:** identik.

## Checklist QA manual (input asli tidak bisa diotomasi)
- [ ] Play di World.unity: mulai 06:30 terang, tanpa kedip malam→pagi di frame pertama.
- [ ] T bolak-balik 06:30 ↔ 19:30; pesan "Lompat ke …" muncul.
- [ ] `]` / `[` mengubah kecepatan (x1, x10, x60) dengan pesan; F3 menampilkan/menyembunyikan jam kanan atas.
- [ ] T tetap bekerja saat kursor dilepas (Esc).
- [ ] Ambience: burung/angin siang, jangkrik malam, dengung horor; volume seimbang dengan langkah kaki.
- [ ] Senja (x60 dari 17:00): langit, kabut, dan lampu memudar halus, tanpa loncatan.
- [ ] Lampu berkedip terasa seram tapi tidak mengganggu; lampu mati selalu yang sama tiap malam.
- [ ] Tuning bisa di `Assets/DayCycle/Config/DayCycleProfile.asset`, `Volume_Day.asset`, dan `Volume_Horror.asset`.
```

- [ ] **Step 11: Commit**

Procedure C:
- path: `Assets/DayCycle/Tests/McpDayCycleDriver.cs` `Assets/DayCycle/Tests/McpDayCycleDriver.cs.meta` `docs/superpowers/notes/2026-10-13-day-night-verification.md`
- subject: `docs(daycycle): Play verification notes and MCP driver`
