# Performa + Pause/Settings Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Game lebih ringan (render sesuai jarak, jatah lampu jalan, preset kualitas + upscaler STP), plus menu Pause (Esc) dan halaman Pengaturan 4 tab (Grafis, Kontrol, Audio, Tampilan) yang tersimpan di PlayerPrefs.

**Architecture:**
- Assembly baru `TycoWorld.GameSettings` berada di atas Player/DayCycle/WorldGen. Modul lama hanya mendapat *knob* (properti runtime):
  - **WorldGen** mendapat `DistanceCuller` (culling renderer per chunk × kategori).
  - **Player** mendapat sensitivitas, invert Y, dan Esc.
  - **DayCycle** mendapat kabut minimum, jatah lampu, dan level ambience.
- Logika murni dites di EditMode:
  - model `SettingsData`, preset, validator, aturan view distance, `SettingsStore`;
  - presenter, navigator menu, pause, indeks chunk, jatah lampu.
- Komponen di objek `GameSystems` (World.unity):
  - `SettingsApplier` menerapkan semua nilai, termasuk salinan runtime URP asset lewat `QualityApplier`;
  - `GameMenu` membangun UI Toolkit dari kode dan menangani Esc.

**Tech Stack:** Unity 6000.6.5f1, URP 17.6.0 (Forward+, GPU Resident Drawer, STP), Input System 1.20.0, UI Toolkit (runtime, `PlayerHudPanel`), Unity Test Framework 1.8.0, Unity MCP, Bash.

**Spec:** `docs/superpowers/specs/2026-10-14-performance-settings-design.md`

## Global Constraints

**Proyek dan branch**
- Project root `D:\UNITY\project\Tyco World`. Branch kerja `feature/performance-settings` (sudah ada, dibuat dari `main` = `1d0e1fb`; spec di commit `4ab0c2e`).
- Unity 6000.6.5f1, URP 17.6.0, `com.unity.inputsystem` 1.20.0, `com.unity.test-framework` 1.8.0.

**Assembly dan namespace**
- Runtime `TycoWorld.GameSettings` (namespace `TycoWorld.GameSettings`).
- Editor `TycoWorld.GameSettings.Editor` (namespace `TycoWorld.GameSettings.Authoring`).
- Tes `TycoWorld.GameSettings.Tests` (namespace `TycoWorld.GameSettings.Tests`).
- Arah dependensi: GameSettings → DayCycle → Player → WorldGen. Tidak ada modul lama yang mereferensikan GameSettings.
- **Kelas data yang di spec disebut `GameSettings` bernama `SettingsData` di kode.** Nama kelas yang sama dengan namespace `TycoWorld.GameSettings` memicu error CS0118 di namespace lain.

**Aturan C# dan API Unity**
- C# 9: tidak ada `record`, `init`, `^`.
- Semua format angka memakai `CultureInfo.InvariantCulture`.
- Unity 6.6:
  - pakai `Object.FindObjectsByType<T>()` / `Object.FindAnyObjectByType<T>()` (tanpa `FindObjectsSortMode`);
  - jangan pakai API instance-ID berbasis `int`.
- Jangan pakai `??` / `?.` / `??=` pada `UnityEngine.Object`.
- File yang punya `using System;` dan `using UnityEngine;` sekaligus menulis `UnityEngine.Object` secara eksplisit.

**Runtime**
- **Tidak boleh mengubah asset saat runtime.**
  - URP asset hanya diubah lewat salinan `Instantiate` + `HideFlags.DontSave`.
  - `QualitySettings.renderPipeline`, `vSyncCount`, dan `Application.targetFrameRate` dikembalikan di `QualityApplier.Dispose`.
  - `Time.timeScale` dan `AudioListener.pause` dikembalikan di `PauseController.Shutdown`.
  - Setelah Play, `git status` tidak boleh berubah.
- Domain reload saat Play mati: **tidak ada state `static` yang bisa berubah** (konstanta dan method statis murni boleh). Event ada di instance.
- Komponen yang dites punya `Initialize()` / `Tick(dt)` / method publik eksplisit. `Update()` hanya bekerja kalau `Application.isPlaying`.

**Tes**
- Tes **tidak pernah** mengubah `Time.timeScale` (pakai fake `IPauseEffects`).
- Tes yang menyentuh `QualitySettings`, `Application.targetFrameRate`, `AudioListener.volume`, atau `RenderSettings` wajib memulihkannya di `TearDown`.
- PlayerPrefs hanya disentuh lewat key uji `tyco.settings.test`, yang dihapus di `TearDown`.
- Fixture GameObject dihancurkan di `TearDown`.

**Teks yang tampil ke pemain** (tulis persis, semua ada di `SettingsText`):
- Menu: `"Jeda"`, `"Lanjutkan"`, `"Pengaturan"`, `"Keluar"`, `"Keluar dari game?"`, `"Ya, keluar"`, `"Batal"`, `"Kembali"`, `"Kembalikan default"`.
- Tab: `"Grafis"`, `"Kontrol"`, `"Audio"`, `"Tampilan"`.
- Preset: `"Rendah"`, `"Sedang"`, `"Tinggi"`, `"Kustom"`.
- Bayangan: `"Mati"`, `"Rendah"`, `"Sedang"`, `"Tinggi"`.
- Batas FPS: `"30 FPS"`, `"60 FPS"`, `"120 FPS"`, `"Tanpa batas"`.
- Mode layar: `"Layar penuh"`, `"Jendela"`.
- Resolusi: `"Saat ini"`.
- Satuan: `"300 m"`, `"85 %"`, `"1,5×"`, `"70°"`, `"60 FPS"`.

**Nilai dan konfigurasi**
- Angka preset hanya ditulis di `QualityPresets.Apply`; nilai default `SettingsData` sama dengan preset Sedang.
- Tier bayangan hanya di `ShadowTiers.For`. Rumus jarak hanya di `ViewDistanceRules`. Semuanya disalin dari spec §4 dan §5.
- Jangan mengubah ProjectSettings. Jangan pernah `git add ProjectSettings`, `git add -A`, atau `git add .`. `ProjectSettings/ProjectSettings.asset` punya perubahan milik user yang belum di-commit.
- `Assets/Settings/PC_RPAsset.asset` dan `PC_Renderer_NoSSAO.asset` boleh berubah, **hanya di Task 7** (edit-time, lewat builder).

**Commit**
- Setiap commit diakhiri:
  ```
  Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5
  ```

**Deviasi kecil yang disengaja dari spec** (supaya reviewer tidak menganggapnya penyimpangan):
- Kelas data `GameSettings` di spec bernama `SettingsData` di kode (lihat aturan namespace di atas).
- `QualityApplier` berupa kelas biasa (`IDisposable`) yang dimiliki `SettingsApplier`, bukan komponen terpisah. Pemulihan terjadi di `SettingsApplier.OnDestroy`, yang juga dipanggil saat aplikasi ditutup, jadi `Application.quitting` tidak perlu di-hook.
- `DistanceCuller` mengecek jarak sendiri (bukan `CullingGroup`; ini sudah disepakati di brainstorming). Jaraknya diukur ke AABB grup (`Bounds.SqrDistance`), versi tepat dari "jarak ke tepi bounds" di spec §5.1.
- Bayangan keras/halus/mati diatur lewat `Light.shadows` matahari dan bulan, karena setter `supportsSoftShadows` dan `supportsMainLightShadows` di URP 17.6 bersifat internal.

## Review Focus

1. **Play dihentikan (atau `GameSystems` dihancurkan) saat menu pause terbuka.** `Time.timeScale` harus kembali 1, `AudioListener.pause` false, serta `QualitySettings.renderPipeline`, `vSyncCount`, dan `targetFrameRate` kembali seperti semula. Dipaku oleh:
   - `PauseControllerTests.Shutdown_WhilePaused_RestoresTimeAndAudio` (Task 10);
   - `GameMenuTests.Shutdown_WhilePaused_Resumes` (Task 12);
   - `QualityApplierTests.Dispose_RestoresPipelineVSyncAndFrameRate` (Task 7);
   - cek `git status` setelah Play (Task 14).
2. **Data PlayerPrefs rusak, versinya tidak dikenal, atau nilainya di luar rentang.** Hasilnya default atau di-clamp, dengan tepat 1 warning, tanpa exception. Dipaku oleh `SettingsStoreTests.Load_*` (Task 3).
3. **Kamera melompat lebih dari 50 m (teleport/respawn).** Culling dan jatah lampu langsung dievaluasi ulang, jadi tidak ada area kosong di sekitar pemain. Dipaku oleh:
   - `DistanceCullerTests.ViewerJump_EvaluatesImmediately` (Task 4);
   - `StreetLampBudgetTests.ViewerJump_ReselectsImmediately` (Task 6).
4. **Dunia di-load setelah `GameSystems` mulai (additive, frame berikutnya), tidak ada dunia sama sekali, atau dunia hilang.** Culler menunggu tanpa error dan membangun indeks saat root muncul. Dipaku oleh `DistanceCullerTests.WorldAppearsLater_IndexBuiltThen`, `WorldMissing_NoErrors`, dan `WorldDestroyed_ResetsWithoutErrors` (Task 4).
5. **Esc saat dunia belum siap (spawner memegang kunci), atau Esc ditekan berulang.** Lanjutkan tidak melepas kunci milik spawner, dan halaman menu tetap konsisten. Dipaku oleh:
   - `PauseControllerTests.Resume_KeepsOtherOwnersLocks` dan `MenuNavigatorTests.Escape_*` (Task 10);
   - `GameMenuTests.Escape_Twice_Resumes` (Task 12).

---

## Prosedur standar

Workspace ledger (diabaikan git): `D:\UNITY\project\Tyco World\.superpowers\sdd\2026-10-14-performance-settings\` (dibuat di Task 1).

### Procedure R+T — refresh, compile, dan jalankan tes

Panggil `mcp__unity-mcp__Unity_RunCommand` dengan kode di bawah. Ganti `Group` (regex nama tes; `"."` = semua).

```csharp
using UnityEditor;

internal class CommandScript : IRunCommand
{
    const string Group = "GameSettingsSmokeTests";
    const string ResultPath = "D:/UNITY/project/Tyco World/.superpowers/sdd/2026-10-14-performance-settings/last-test.txt";

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
- `DIRTY SCENE`: simpan scene dulu.
- Kalau panggilan MCP gagal karena domain reload ("Could not find type … RunCommandMacroEvaluatorEntryPoint"), ulangi.
- Kalau MCP pindah ke background (>2 menit), tunggu notifikasinya. Hasil juga tertulis di `last-test.txt`.
- Saat Group `"."`, payload MCP berisi error/warning `[Player]`, `[DayCycle]`, `[WorldGen]`, dan `[Settings]` dari tes yang memang sengaja gagal (ditangkap `LogAssert`). Itu bukan kegagalan; yang menentukan adalah baris `group=… failed=0`.
- Kalau pipe MCP hilang (Unity crash), berhenti dan laporkan. Jangan mencoba menjalankan ulang Unity.

### Procedure P — masuk/keluar Play lewat MCP

Masuk Play (panggil berulang sampai hasilnya diawali `READY`):

```csharp
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (!EditorApplication.isPlaying) { EditorApplication.isPlaying = true; result.Log("entering play"); return; }
        if (EditorApplication.isPaused) EditorApplication.isPaused = false;
        result.Log(TycoWorld.GameSettings.Tests.McpPerfProbe.Status());
    }
}
```

Keluar Play: snippet yang sama dengan isi `Execute` diganti menjadi `EditorApplication.isPlaying = false; result.Log("exit");`.
- Editor bisa masuk Play dalam keadaan Pause. Snippet di atas melepasnya.
- `McpPerfProbe.Place` menyalakan `Application.runInBackground`.

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
Assets/GameSettings/Runtime/TycoWorld.GameSettings.asmdef                                      (Task 1)
Assets/GameSettings/Runtime/Diagnostics/FrameStats.cs, FrameStatsSampler.cs                    (Task 1)
Assets/GameSettings/Editor/TycoWorld.GameSettings.Editor.asmdef, GameSettingsPaths.cs           (Task 1)
Assets/GameSettings/Tests/TycoWorld.GameSettings.Tests.asmdef, GameSettingsSmokeTests.cs,
    FrameStatsTests.cs, McpPerfProbe.cs                                                        (Task 1)
Assets/WorldGen/Tests/McpTestRunner.cs                                         (ubah, Task 1)
Assets/GameSettings/Runtime/Settings/SettingsData.cs, SettingsEnums.cs, ShadowTiers.cs,
    QualityPresets.cs, SettingsValidator.cs, ViewDistanceRules.cs                              (Task 2)
Assets/GameSettings/Runtime/Settings/ISettingsBackend.cs, PlayerPrefsBackend.cs, SettingsStore.cs (Task 3)
Assets/WorldGen/Runtime/Culling/CullCategory.cs, ChunkGroup.cs, WorldChunkIndex.cs,
    DistanceCuller.cs; Assets/WorldGen/Tests/CullingFixture.cs, WorldChunkIndexTests.cs,
    DistanceCullerTests.cs                                                                     (Task 4)
Assets/Player/Runtime/Core/FirstPersonPlayer.cs, Input/PlayerInputReader.cs    (ubah, Task 5)
Assets/DayCycle/Runtime/Lamps/StreetLampSystem.cs, Audio/AmbienceAudio.cs,
    DayCycleDirector.cs; Assets/DayCycle/Tests/DirectorFixture.cs              (ubah, Task 6)
Assets/DayCycle/Tests/StreetLampBudgetTests.cs                                                 (Task 6)
Assets/GameSettings/Runtime/Quality/QualityApplier.cs; Editor/NoSsaoRendererBuilder.cs;
    Assets/Settings/PC_Renderer_NoSSAO.asset, PC_RPAsset.asset (ubah)                          (Task 7)
Assets/GameSettings/Runtime/UI/SettingsText.cs, SettingsPresenter.cs, ResolutionOptions.cs     (Task 8)
Assets/GameSettings/Runtime/UI/MenuStyle.cs, MenuPage.cs, PauseMenuView.cs, SettingsView.cs,
    FpsCounterView.cs                                                                          (Task 9)
Assets/GameSettings/Runtime/Menu/MenuNavigator.cs, IPauseEffects.cs, UnityPauseEffects.cs,
    PauseController.cs; Tests/GameFixture.cs                                                   (Task 10)
Assets/GameSettings/Runtime/SettingsApplier.cs, Audio/EffectsVolume.cs                         (Task 11)
Assets/GameSettings/Runtime/Menu/GameMenu.cs                                                   (Task 12)
Assets/GameSettings/Editor/GameSettingsSceneSetup.cs; Tests/McpGameDriver.cs;
    Assets/Scenes/World.unity (ubah)                                                           (Task 13)
docs/superpowers/notes/2026-10-14-performance-verification.md                                  (Task 14)
```

---

### Task 1: Scaffold assembly GameSettings, `FrameStats`, probe performa, dan baseline

**Files:**
- Create: `Assets/GameSettings/Runtime/TycoWorld.GameSettings.asmdef`
- Create: `Assets/GameSettings/Runtime/Diagnostics/FrameStats.cs`
- Create: `Assets/GameSettings/Runtime/Diagnostics/FrameStatsSampler.cs`
- Create: `Assets/GameSettings/Editor/TycoWorld.GameSettings.Editor.asmdef`
- Create: `Assets/GameSettings/Editor/GameSettingsPaths.cs`
- Create: `Assets/GameSettings/Tests/TycoWorld.GameSettings.Tests.asmdef`
- Create: `Assets/GameSettings/Tests/GameSettingsSmokeTests.cs`
- Create: `Assets/GameSettings/Tests/FrameStatsTests.cs`
- Create: `Assets/GameSettings/Tests/McpPerfProbe.cs`
- Modify: `Assets/WorldGen/Tests/McpTestRunner.cs` (daftar `assemblyNames`)

**Interfaces:**
- Produces:
  - `FrameStats(float windowSeconds)` dengan `bool Sample(float unscaledDeltaTime)`, `HasResult`, `AverageFps`, `AverageMs`, `WorstMs`, `WindowSeconds`, dan `Clear()`.
  - `FrameStatsSampler : MonoBehaviour` dengan `Begin(float seconds, float warmupSeconds = 1f)`, `IsDone`, dan `Stats`.
  - `GameSettingsPaths.PipelineAsset`, `.RendererAsset`, `.NoSsaoRendererAsset`.
  - `McpPerfProbe.Status() / Place(string) / SetTime(float) / Begin(float) / Report(string) / Release()`.

- [ ] **Step 1: Buat workspace ledger**

```bash
mkdir -p "/d/UNITY/project/Tyco World/.superpowers/sdd/2026-10-14-performance-settings"
git -C "/d/UNITY/project/Tyco World" check-ignore -q .superpowers/sdd && echo ignored
```
Expected: `ignored`.

- [ ] **Step 2: Tulis asmdef runtime**

`Assets/GameSettings/Runtime/TycoWorld.GameSettings.asmdef`:
```json
{
    "name": "TycoWorld.GameSettings",
    "rootNamespace": "TycoWorld.GameSettings",
    "references": [
        "TycoWorld.WorldGen",
        "TycoWorld.Player",
        "TycoWorld.DayCycle",
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

- [ ] **Step 3: Tulis asmdef editor dan `GameSettingsPaths`**

`Assets/GameSettings/Editor/TycoWorld.GameSettings.Editor.asmdef`:
```json
{
    "name": "TycoWorld.GameSettings.Editor",
    "rootNamespace": "TycoWorld.GameSettings.Authoring",
    "references": [
        "TycoWorld.GameSettings",
        "TycoWorld.Player",
        "TycoWorld.Player.Editor",
        "TycoWorld.DayCycle",
        "TycoWorld.DayCycle.Editor",
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

`Assets/GameSettings/Editor/GameSettingsPaths.cs`:
```csharp
namespace TycoWorld.GameSettings.Authoring
{
    /// <summary>Project-relative paths used by the settings sub-project (spec #4).</summary>
    public static class GameSettingsPaths
    {
        public const string PipelineAsset = "Assets/Settings/PC_RPAsset.asset";
        public const string RendererAsset = "Assets/Settings/PC_Renderer.asset";
        public const string NoSsaoRendererAsset = "Assets/Settings/PC_Renderer_NoSSAO.asset";
    }
}
```

- [ ] **Step 4: Tulis asmdef tes**

`Assets/GameSettings/Tests/TycoWorld.GameSettings.Tests.asmdef`:
```json
{
    "name": "TycoWorld.GameSettings.Tests",
    "rootNamespace": "TycoWorld.GameSettings.Tests",
    "references": [
        "TycoWorld.GameSettings",
        "TycoWorld.GameSettings.Editor",
        "TycoWorld.DayCycle",
        "TycoWorld.DayCycle.Editor",
        "TycoWorld.Player",
        "TycoWorld.Player.Editor",
        "TycoWorld.Player.Tests",
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
`TycoWorld.Player.Tests` direferensikan untuk `McpRender.SavePng` (screenshot di Task 14).

- [ ] **Step 5: Tambahkan assembly tes baru ke runner**

Di `Assets/WorldGen/Tests/McpTestRunner.cs`, ganti baris:
```csharp
                    assemblyNames = new[] { "TycoWorld.WorldGen.Tests", "TycoWorld.Player.Tests", "TycoWorld.DayCycle.Tests" },
```
dengan:
```csharp
                    assemblyNames = new[] { "TycoWorld.WorldGen.Tests", "TycoWorld.Player.Tests", "TycoWorld.DayCycle.Tests", "TycoWorld.GameSettings.Tests" },
```

- [ ] **Step 6: Tulis tes yang gagal (smoke + `FrameStats`)**

`Assets/GameSettings/Tests/GameSettingsSmokeTests.cs`:
```csharp
using NUnit.Framework;
using TycoWorld.GameSettings.Authoring;

namespace TycoWorld.GameSettings.Tests
{
    public class GameSettingsSmokeTests
    {
        [Test]
        public void Assemblies_Load()
        {
            Assert.AreEqual("TycoWorld.GameSettings", typeof(FrameStats).Assembly.GetName().Name);
            Assert.AreEqual("TycoWorld.GameSettings.Editor", typeof(GameSettingsPaths).Assembly.GetName().Name);
        }
    }
}
```

`Assets/GameSettings/Tests/FrameStatsTests.cs`:
```csharp
using NUnit.Framework;

namespace TycoWorld.GameSettings.Tests
{
    public class FrameStatsTests
    {
        // Powers of two keep the float sums exact.
        [Test]
        public void Sample_ReportsAverageWhenTheWindowFills()
        {
            var stats = new FrameStats(0.5f);
            for (int i = 0; i < 31; i++) Assert.IsFalse(stats.Sample(1f / 64f));
            Assert.IsTrue(stats.Sample(1f / 64f));
            Assert.IsTrue(stats.HasResult);
            Assert.AreEqual(64f, stats.AverageFps, 1e-3f);
            Assert.AreEqual(15.625f, stats.AverageMs, 1e-3f);
        }

        [Test]
        public void Sample_TracksTheWorstFrame()
        {
            var stats = new FrameStats(0.5f);
            for (int i = 0; i < 6; i++) stats.Sample(0.0625f);
            Assert.IsTrue(stats.Sample(0.125f));
            Assert.AreEqual(125f, stats.WorstMs, 1e-3f);
            Assert.AreEqual(14f, stats.AverageFps, 1e-3f);
        }

        [Test]
        public void Sample_IgnoresZeroAndNegativeFrames()
        {
            var stats = new FrameStats(0.5f);
            Assert.IsFalse(stats.Sample(0f));
            Assert.IsFalse(stats.Sample(-1f));
            Assert.IsFalse(stats.HasResult);
        }

        [Test]
        public void Clear_ForgetsTheResult()
        {
            var stats = new FrameStats(0.25f);
            stats.Sample(0.25f);
            stats.Clear();
            Assert.IsFalse(stats.HasResult);
            Assert.AreEqual(0f, stats.AverageFps);
        }
    }
}
```

- [ ] **Step 7: Jalankan tes, pastikan gagal**

Procedure R+T, `Group = "GameSettingsSmokeTests|FrameStatsTests"`.
Expected: `COMPILE FAILED` (tipe `FrameStats` belum ada).

- [ ] **Step 8: Implementasi `FrameStats` dan `FrameStatsSampler`**

`Assets/GameSettings/Runtime/Diagnostics/FrameStats.cs`:
```csharp
using UnityEngine;

namespace TycoWorld.GameSettings
{
    /// <summary>Frame-time statistics over a fixed window of real time (FPS counter, performance probe).</summary>
    public sealed class FrameStats
    {
        readonly float window;
        float elapsed, worst;
        int frames;

        public FrameStats(float windowSeconds) => window = Mathf.Max(0.01f, windowSeconds);

        public float WindowSeconds => window;
        public bool HasResult { get; private set; }
        public float AverageFps { get; private set; }
        public float AverageMs { get; private set; }
        public float WorstMs { get; private set; }

        /// <summary>Adds one frame; returns true when a window just completed and the averages were refreshed.</summary>
        public bool Sample(float unscaledDeltaTime)
        {
            if (unscaledDeltaTime <= 0f) return false;
            elapsed += unscaledDeltaTime;
            frames++;
            if (unscaledDeltaTime > worst) worst = unscaledDeltaTime;
            if (elapsed < window) return false;
            AverageFps = frames / elapsed;
            AverageMs = elapsed * 1000f / frames;
            WorstMs = worst * 1000f;
            HasResult = true;
            elapsed = 0f;
            worst = 0f;
            frames = 0;
            return true;
        }

        public void Clear()
        {
            elapsed = 0f;
            worst = 0f;
            frames = 0;
            HasResult = false;
            AverageFps = 0f;
            AverageMs = 0f;
            WorstMs = 0f;
        }
    }
}
```

`Assets/GameSettings/Runtime/Diagnostics/FrameStatsSampler.cs`:
```csharp
using UnityEngine;

namespace TycoWorld.GameSettings
{
    /// <summary>Collects one FrameStats window after a warm-up (performance probe over MCP).</summary>
    public sealed class FrameStatsSampler : MonoBehaviour
    {
        FrameStats stats;
        float warmup;

        public bool IsDone => stats != null && stats.HasResult;
        public FrameStats Stats => stats;

        public void Begin(float seconds, float warmupSeconds = 1f)
        {
            stats = new FrameStats(seconds);
            warmup = warmupSeconds;
        }

        void Update()
        {
            if (stats == null || stats.HasResult) return;
            float dt = Time.unscaledDeltaTime;
            if (warmup > 0f)
            {
                warmup -= dt;
                return;
            }
            stats.Sample(dt);
        }
    }
}
```

- [ ] **Step 9: Jalankan tes, pastikan lulus**

Procedure R+T, `Group = "GameSettingsSmokeTests|FrameStatsTests"`.
Expected: `group=GameSettingsSmokeTests|FrameStatsTests passed=5 failed=0 skipped=0`.

- [ ] **Step 10: Tulis `McpPerfProbe` (helper Play, bukan tes)**

`Assets/GameSettings/Tests/McpPerfProbe.cs`:
```csharp
using System.Globalization;
using System.Text;
using TycoWorld.DayCycle;
using TycoWorld.Player;
using TycoWorld.WorldGen;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.GameSettings.Tests
{
    /// <summary>
    /// Dev helper for MCP performance measurements in Play mode (not a test). Keeps no static state: the sampler and the
    /// player lock live on the "PerfProbe" GameObject. Viewpoints: "kos" (yard, facing the lane), "road" (Jalan
    /// Sukabirus, looking north-east along it) and "high" (30 m above the road, facing the campus).
    /// </summary>
    public static class McpPerfProbe
    {
        public const string ProbeName = "PerfProbe";

        public static string Status()
        {
            if (!EditorApplication.isPlaying) return "NOT PLAYING";
            var player = Object.FindAnyObjectByType<FirstPersonPlayer>();
            if (player == null || !player.IsInitialized) return "NO PLAYER";
            if (Object.FindAnyObjectByType<GeneratedRoot>() == null) return "NO WORLD";
            return "READY t=" + Clock() + " pos=" + Vec(player.transform.position) + " paused=" + EditorApplication.isPaused;
        }

        public static string Place(string viewpoint)
        {
            Application.runInBackground = true;
            if (EditorApplication.isPaused) EditorApplication.isPaused = false;
            var player = Object.FindAnyObjectByType<FirstPersonPlayer>();
            if (player == null || !player.IsInitialized) return "NO PLAYER";
            float x, z, yaw, lift = 0f;
            switch (viewpoint)
            {
                case "kos": x = -321.3f; z = -612.6f; yaw = 180f; break;
                case "road": x = -287.7f; z = -633.3f; yaw = 57f; break;
                case "high": x = -287.7f; z = -633.3f; yaw = 24f; lift = 30f; break;
                default: return "UNKNOWN VIEWPOINT " + viewpoint;
            }
            if (!TryGround(x, z, out var ground)) return "NO GROUND at " + viewpoint;
            // Locking Move turns the CharacterController off, so the player hangs at the lifted viewpoint without falling.
            player.Locks.Acquire(Sampler(), PlayerAbility.All);
            player.Teleport(ground + Vector3.up * lift, yaw);
            return "placed " + viewpoint + " feet=" + Vec(player.transform.position) + " yaw=" + yaw.ToString("F0", CultureInfo.InvariantCulture);
        }

        public static string SetTime(float hours)
        {
            var director = Object.FindAnyObjectByType<DayCycleDirector>();
            if (director == null || !director.IsInitialized) return "NO DIRECTOR";
            director.Clock.SetTime(hours);
            return "t=" + director.Clock.Display;
        }

        public static string Begin(float seconds)
        {
            Sampler().Begin(seconds);
            return "sampling " + seconds.ToString("F1", CultureInfo.InvariantCulture) + " s after a 1 s warm-up";
        }

        public static string Report(string label)
        {
            var sampler = Sampler();
            if (!sampler.IsDone) return "PENDING";
            var s = sampler.Stats;
            var inv = CultureInfo.InvariantCulture;
            int lights = 0;
            foreach (var light in Object.FindObjectsByType<Light>())
                if (light.enabled) lights++;
            int renderers = 0, total = 0;
            foreach (var r in Object.FindObjectsByType<Renderer>())
            {
                total++;
                if (r.enabled) renderers++;
            }
            var cam = Camera.main;
            var sb = new StringBuilder();
            sb.Append(label)
                .Append(" fps=").Append(s.AverageFps.ToString("F1", inv))
                .Append(" ms=").Append(s.AverageMs.ToString("F2", inv))
                .Append(" worst=").Append(s.WorstMs.ToString("F1", inv))
                .Append(" batches=").Append(UnityStats.batches)
                .Append(" setpass=").Append(UnityStats.setPassCalls)
                .Append(" tris=").Append(UnityStats.triangles)
                .Append(" lights=").Append(lights)
                .Append(" renderers=").Append(renderers).Append('/').Append(total)
                .Append(" far=").Append(cam != null ? cam.farClipPlane.ToString("F0", inv) : "-")
                .Append(" fog=").Append(RenderSettings.fogDensity.ToString("F4", inv))
                .Append(" t=").Append(Clock());
            return sb.ToString();
        }

        public static string Release()
        {
            var go = GameObject.Find(ProbeName);
            if (go == null) return "NO PROBE";
            var player = Object.FindAnyObjectByType<FirstPersonPlayer>();
            if (player != null && player.IsInitialized) player.Locks.Release(go.GetComponent<FrameStatsSampler>());
            Object.Destroy(go);
            return "released";
        }

        static FrameStatsSampler Sampler()
        {
            var go = GameObject.Find(ProbeName);
            if (go == null) go = new GameObject(ProbeName);
            var sampler = go.GetComponent<FrameStatsSampler>();
            if (sampler == null) sampler = go.AddComponent<FrameStatsSampler>();
            return sampler;
        }

        /// <summary>The highest walkable surface below 5 m under (x, z): a GroundSurface or the Ground quad, never a roof or tree.</summary>
        static bool TryGround(float x, float z, out Vector3 point)
        {
            point = default;
            float best = float.NegativeInfinity;
            foreach (var hit in Physics.RaycastAll(new Vector3(x, 300f, z), Vector3.down, 600f, ~0, QueryTriggerInteraction.Ignore))
            {
                bool walkable = hit.collider.GetComponentInParent<GroundSurface>() != null || hit.collider.name == "Ground";
                if (!walkable || hit.point.y > 5f || hit.point.y <= best) continue;
                best = hit.point.y;
                point = hit.point;
            }
            return best > float.NegativeInfinity;
        }

        static string Clock()
        {
            var director = Object.FindAnyObjectByType<DayCycleDirector>();
            return director != null && director.IsInitialized ? director.Clock.Display : "-";
        }

        static string Vec(Vector3 v)
        {
            var inv = CultureInfo.InvariantCulture;
            return "(" + v.x.ToString("F1", inv) + "," + v.y.ToString("F1", inv) + "," + v.z.ToString("F1", inv) + ")";
        }
    }
}
```

- [ ] **Step 11: Compile (tes smoke lagi)**

Procedure R+T, `Group = "GameSettingsSmokeTests"`.
Expected: `passed=1 failed=0`.

- [ ] **Step 12: Ukur baseline (sebelum ada perubahan perilaku)**

Pastikan `World.unity` terbuka dan tersimpan. Masuk Play dengan Procedure P sampai `READY`. Untuk tiap baris tabel berikut, jalankan satu snippet MCP per panggilan, berurutan:
1. `McpPerfProbe.Place("<vp>")`
2. `McpPerfProbe.SetTime(<jam>f)`
3. `McpPerfProbe.Begin(5f)`
4. `McpPerfProbe.Report("<label>")`, diulang sampai bukan `PENDING` (±6–7 s).

| label | vp | jam |
|---|---|---|
| `base-kos-day` | kos | 12 |
| `base-road-day` | road | 12 |
| `base-road-night` | road | 21 |
| `base-high-day` | high | 12 |

Contoh satu panggilan:
```csharp
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        result.Log(TycoWorld.GameSettings.Tests.McpPerfProbe.Report("base-road-night"));
    }
}
```
Setelah keempat titik selesai, jalankan `McpPerfProbe.Release()`, lalu keluar Play (Procedure P). Salin keempat baris `Report` apa adanya ke `.superpowers/sdd/2026-10-14-performance-settings/perf-baseline.md`, di bawah judul `# Baseline (sebelum Task 2)`. Angka ini dipakai Task 14.

Hasil `base-road-night` seharusnya menunjukkan `lights=` sekitar 870+ (semua lampu jalan menyala). Kalau `Place` mengembalikan `NO GROUND`, catat dan lanjutkan dengan titik lainnya.

- [ ] **Step 13: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add Assets/GameSettings Assets/WorldGen/Tests/McpTestRunner.cs
git -C "D:/UNITY/project/Tyco World" commit -m "feat(settings): scaffold GameSettings assemblies, frame stats and performance probe

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

---

### Task 2: Model `SettingsData`, preset, tier bayangan, validator, dan aturan view distance

**Files:**
- Create: `Assets/GameSettings/Runtime/Settings/SettingsEnums.cs`
- Create: `Assets/GameSettings/Runtime/Settings/SettingsData.cs`
- Create: `Assets/GameSettings/Runtime/Settings/ShadowTiers.cs`
- Create: `Assets/GameSettings/Runtime/Settings/QualityPresets.cs`
- Create: `Assets/GameSettings/Runtime/Settings/ViewDistanceRules.cs`
- Create: `Assets/GameSettings/Runtime/Settings/SettingsValidator.cs`
- Test: `Assets/GameSettings/Tests/QualityPresetsTests.cs`, `SettingsValidatorTests.cs`, `ViewDistanceRulesTests.cs`

**Interfaces:**
- Produces:
  - `enum QualityPreset { Low, Medium, High, Custom }` dan `enum ShadowQuality { Off, Low, Medium, High }`.
  - `SettingsData` (field publik huruf kecil, lihat kode) dengan `Clone()` dan `const int CurrentVersion = 1`.
  - `ShadowTier { Directional, Distance, Cascades, Resolution, Soft }` dan `ShadowTiers.For(ShadowQuality)`.
  - `QualityPresets.Apply(QualityPreset, SettingsData)`, `Detect(SettingsData) → QualityPreset`, `GraphicsEqual(a, b)`.
  - `ViewDistanceRules.Min/Max/Step`, `FarClip(float)`, `MinFogDensity(float)`, `PropDistance(float)`.
  - `SettingsValidator.Sanitize(SettingsData) → bool`, `LampLightChoices() → int[] {8,16,32}`, `FpsLimitChoices() → int[] {30,60,120,0}`, konstanta `RenderScaleMin/Max/Step`, `SensitivityMin/Max`, `FovMin/Max`.

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/GameSettings/Tests/QualityPresetsTests.cs`:
```csharp
using NUnit.Framework;

namespace TycoWorld.GameSettings.Tests
{
    public class QualityPresetsTests
    {
        [Test]
        public void Defaults_AreTheMediumPreset()
        {
            var s = new SettingsData();
            Assert.AreEqual(QualityPreset.Medium, s.preset);
            Assert.AreEqual(QualityPreset.Medium, QualityPresets.Detect(s));
            Assert.AreEqual(SettingsData.CurrentVersion, s.version);
            Assert.AreEqual(60, s.fpsLimit);
            Assert.IsFalse(s.vSync);
        }

        [TestCase(QualityPreset.Low, 200, 70, ShadowQuality.Low, false, 8, 2f, 1)]
        [TestCase(QualityPreset.Medium, 300, 85, ShadowQuality.Medium, false, 16, 1f, 1)]
        [TestCase(QualityPreset.High, 500, 100, ShadowQuality.High, true, 32, 0f, 2)]
        public void Apply_FillsTheSpecTable(QualityPreset preset, int view, int scale, ShadowQuality shadows, bool ssao, int lamps,
            float smallMesh, int msaa)
        {
            var s = new SettingsData();
            QualityPresets.Apply(preset, s);
            Assert.AreEqual(preset, s.preset);
            Assert.AreEqual(view, s.viewDistance);
            Assert.AreEqual(scale, s.renderScale);
            Assert.AreEqual(shadows, s.shadows);
            Assert.AreEqual(ssao, s.ssao);
            Assert.IsTrue(s.gpuOcclusion);
            Assert.AreEqual(lamps, s.lampLights);
            Assert.AreEqual(smallMesh, s.smallMeshCulling, 1e-4f);
            Assert.AreEqual(msaa, s.msaa);
        }

        [Test]
        public void Detect_ChangedGraphicsValue_IsCustom()
        {
            var s = new SettingsData { viewDistance = 450 };
            Assert.AreEqual(QualityPreset.Custom, QualityPresets.Detect(s));
        }

        [Test]
        public void Detect_IgnoresEverythingOutsideThePreset()
        {
            var s = new SettingsData();
            QualityPresets.Apply(QualityPreset.High, s);
            s.fpsLimit = 0;
            s.vSync = true;
            s.fullscreen = false;
            s.resolutionWidth = 1280;
            s.resolutionHeight = 720;
            s.mouseSensitivity = 2f;
            s.fieldOfView = 90;
            s.masterVolume = 10;
            s.showFps = true;
            Assert.AreEqual(QualityPreset.High, QualityPresets.Detect(s));
        }

        [Test]
        public void Apply_Custom_OnlyMarksThePreset()
        {
            var s = new SettingsData { viewDistance = 450 };
            QualityPresets.Apply(QualityPreset.Custom, s);
            Assert.AreEqual(450, s.viewDistance);
            Assert.AreEqual(QualityPreset.Custom, s.preset);
        }

        [Test]
        public void ShadowTiers_MatchTheSpec()
        {
            var off = ShadowTiers.For(ShadowQuality.Off);
            Assert.IsFalse(off.Directional);
            Assert.Greater(off.Distance, 0f, "senter tetap butuh jarak bayangan");
            AssertTier(ShadowTiers.For(ShadowQuality.Low), 30f, 1, 1024, false);
            AssertTier(ShadowTiers.For(ShadowQuality.Medium), 50f, 2, 2048, true);
            AssertTier(ShadowTiers.For(ShadowQuality.High), 80f, 4, 2048, true);
        }

        [Test]
        public void Clone_IsIndependent()
        {
            var a = new SettingsData();
            var b = a.Clone();
            b.viewDistance = 800;
            Assert.AreEqual(300, a.viewDistance);
        }

        static void AssertTier(ShadowTier tier, float distance, int cascades, int resolution, bool soft)
        {
            Assert.IsTrue(tier.Directional);
            Assert.AreEqual(distance, tier.Distance);
            Assert.AreEqual(cascades, tier.Cascades);
            Assert.AreEqual(resolution, tier.Resolution);
            Assert.AreEqual(soft, tier.Soft);
        }
    }
}
```

`Assets/GameSettings/Tests/SettingsValidatorTests.cs`:
```csharp
using NUnit.Framework;

namespace TycoWorld.GameSettings.Tests
{
    public class SettingsValidatorTests
    {
        [Test]
        public void Defaults_AreValid()
        {
            var s = new SettingsData();
            Assert.IsFalse(SettingsValidator.Sanitize(s));
            Assert.AreEqual(QualityPreset.Medium, s.preset);
        }

        [TestCase(5000, 800)]
        [TestCase(10, 100)]
        [TestCase(333, 350)]
        [TestCase(450, 450)]
        public void ViewDistance_IsClampedAndSnappedTo50(int input, int expected)
        {
            var s = new SettingsData { viewDistance = input };
            Assert.AreEqual(input != expected, SettingsValidator.Sanitize(s));
            Assert.AreEqual(expected, s.viewDistance);
        }

        [Test]
        public void OutOfRangeValues_AreClamped()
        {
            var s = new SettingsData
            {
                renderScale = 42, lampLights = 20, fpsLimit = 45, msaa = 8, smallMeshCulling = 50f,
                mouseSensitivity = 9f, fieldOfView = 10, masterVolume = -5, effectsVolume = 300, ambienceVolume = 101
            };
            Assert.IsTrue(SettingsValidator.Sanitize(s));
            Assert.AreEqual(50, s.renderScale);
            Assert.AreEqual(16, s.lampLights);
            Assert.AreEqual(60, s.fpsLimit);
            Assert.AreEqual(1, s.msaa);
            Assert.AreEqual(20f, s.smallMeshCulling);
            Assert.AreEqual(3f, s.mouseSensitivity);
            Assert.AreEqual(60, s.fieldOfView);
            Assert.AreEqual(0, s.masterVolume);
            Assert.AreEqual(100, s.effectsVolume);
            Assert.AreEqual(100, s.ambienceVolume);
        }

        [Test]
        public void NaNSensitivity_FallsBackToDefault()
        {
            var s = new SettingsData { mouseSensitivity = float.NaN };
            Assert.IsTrue(SettingsValidator.Sanitize(s));
            Assert.AreEqual(1f, s.mouseSensitivity);
        }

        [Test]
        public void UnknownShadowQuality_BecomesMedium()
        {
            var s = new SettingsData { shadows = (ShadowQuality)9 };
            Assert.IsTrue(SettingsValidator.Sanitize(s));
            Assert.AreEqual(ShadowQuality.Medium, s.shadows);
        }

        [TestCase(-1, 600)]
        [TestCase(1280, 0)]
        public void HalfSetResolution_MeansKeepCurrent(int w, int h)
        {
            var s = new SettingsData { resolutionWidth = w, resolutionHeight = h };
            Assert.IsTrue(SettingsValidator.Sanitize(s));
            Assert.AreEqual(0, s.resolutionWidth);
            Assert.AreEqual(0, s.resolutionHeight);
        }

        [Test]
        public void FullResolution_IsKept()
        {
            var s = new SettingsData { resolutionWidth = 1920, resolutionHeight = 1080 };
            Assert.IsFalse(SettingsValidator.Sanitize(s));
            Assert.AreEqual(1920, s.resolutionWidth);
        }

        [Test]
        public void MismatchedPreset_IsFixedSilently()
        {
            var s = new SettingsData { preset = QualityPreset.High };
            Assert.IsFalse(SettingsValidator.Sanitize(s));
            Assert.AreEqual(QualityPreset.Medium, s.preset);
        }

        [Test]
        public void Choices_MatchTheSpec()
        {
            CollectionAssert.AreEqual(new[] { 8, 16, 32 }, SettingsValidator.LampLightChoices());
            CollectionAssert.AreEqual(new[] { 30, 60, 120, 0 }, SettingsValidator.FpsLimitChoices());
        }
    }
}
```

`Assets/GameSettings/Tests/ViewDistanceRulesTests.cs`:
```csharp
using NUnit.Framework;

namespace TycoWorld.GameSettings.Tests
{
    public class ViewDistanceRulesTests
    {
        [TestCase(100f, 105f)]
        [TestCase(300f, 315f)]
        [TestCase(800f, 840f)]
        public void FarClip_Is105Percent(float view, float expected) => Assert.AreEqual(expected, ViewDistanceRules.FarClip(view), 1e-3f);

        [TestCase(300f, 0.0076667f)]
        [TestCase(500f, 0.0046f)]
        [TestCase(800f, 0.002875f)]
        public void MinFogDensity_Is2Point3OverDistance(float view, float expected) =>
            Assert.AreEqual(expected, ViewDistanceRules.MinFogDensity(view), 1e-6f);

        [TestCase(100f, 80f)]
        [TestCase(300f, 180f)]
        [TestCase(800f, 480f)]
        public void PropDistance_Is60PercentWith80Minimum(float view, float expected) =>
            Assert.AreEqual(expected, ViewDistanceRules.PropDistance(view), 1e-3f);

        [Test]
        public void SliderRange_MatchesTheSpec()
        {
            Assert.AreEqual(100, ViewDistanceRules.Min);
            Assert.AreEqual(800, ViewDistanceRules.Max);
            Assert.AreEqual(50, ViewDistanceRules.Step);
        }
    }
}
```

- [ ] **Step 2: Jalankan tes, pastikan gagal**

Procedure R+T, `Group = "QualityPresetsTests|SettingsValidatorTests|ViewDistanceRulesTests"`.
Expected: `COMPILE FAILED` (`SettingsData` belum ada).

- [ ] **Step 3: Implementasi**

`Assets/GameSettings/Runtime/Settings/SettingsEnums.cs`:
```csharp
namespace TycoWorld.GameSettings
{
    public enum QualityPreset
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Custom = 3
    }

    public enum ShadowQuality
    {
        Off = 0,
        Low = 1,
        Medium = 2,
        High = 3
    }
}
```

`Assets/GameSettings/Runtime/Settings/SettingsData.cs`:
```csharp
using System;

namespace TycoWorld.GameSettings
{
    /// <summary>
    /// Everything the settings page stores (spec #4 §4; the spec calls this type "GameSettings"), saved to PlayerPrefs
    /// as JSON. Field defaults equal the Medium preset.
    /// </summary>
    [Serializable]
    public sealed class SettingsData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;

        // Grafis
        public QualityPreset preset = QualityPreset.Medium;
        public int viewDistance = 300;
        /// <summary>Percent; below 100 the STP upscaler is used.</summary>
        public int renderScale = 85;
        public ShadowQuality shadows = ShadowQuality.Medium;
        public bool ssao;
        public bool gpuOcclusion = true;
        public int lampLights = 16;
        /// <summary>GPU Resident Drawer small-mesh culling in % of the screen; set by presets only.</summary>
        public float smallMeshCulling = 1f;
        /// <summary>1 or 2; set by presets only and ignored while the render scale is below 100 %.</summary>
        public int msaa = 1;
        /// <summary>0 = no limit.</summary>
        public int fpsLimit = 60;
        public bool vSync;
        public bool fullscreen = true;
        /// <summary>0 × 0 keeps the current resolution.</summary>
        public int resolutionWidth;
        public int resolutionHeight;

        // Kontrol
        public float mouseSensitivity = 1f;
        public bool invertY;
        public int fieldOfView = 70;

        // Audio, in percent
        public int masterVolume = 100;
        public int effectsVolume = 100;
        public int ambienceVolume = 100;

        // Tampilan
        public bool showFps;
        public bool showClock;

        public SettingsData Clone() => (SettingsData)MemberwiseClone();
    }
}
```

`Assets/GameSettings/Runtime/Settings/ShadowTiers.cs`:
```csharp
namespace TycoWorld.GameSettings
{
    /// <summary>Shadow values of one quality tier (spec #4 §4).</summary>
    public readonly struct ShadowTier
    {
        public ShadowTier(bool directional, float distance, int cascades, int resolution, bool soft)
        {
            Directional = directional;
            Distance = distance;
            Cascades = cascades;
            Resolution = resolution;
            Soft = soft;
        }

        /// <summary>False: the sun and moon cast no shadows. The flashlight (an additional light) keeps its own.</summary>
        public bool Directional { get; }
        public float Distance { get; }
        public int Cascades { get; }
        public int Resolution { get; }
        public bool Soft { get; }
    }

    public static class ShadowTiers
    {
        public static ShadowTier For(ShadowQuality quality)
        {
            switch (quality)
            {
                case ShadowQuality.Off: return new ShadowTier(false, 25f, 1, 1024, false);
                case ShadowQuality.Low: return new ShadowTier(true, 30f, 1, 1024, false);
                case ShadowQuality.High: return new ShadowTier(true, 80f, 4, 2048, true);
                default: return new ShadowTier(true, 50f, 2, 2048, true);
            }
        }
    }
}
```

`Assets/GameSettings/Runtime/Settings/QualityPresets.cs`:
```csharp
using UnityEngine;

namespace TycoWorld.GameSettings
{
    /// <summary>The Low/Medium/High graphics presets of spec #4 §4; FPS limit, VSync, screen and non-graphics settings are not part of them.</summary>
    public static class QualityPresets
    {
        public static void Apply(QualityPreset preset, SettingsData s)
        {
            switch (preset)
            {
                case QualityPreset.Low:
                    Set(s, 200, 70, ShadowQuality.Low, false, true, 8, 2f, 1);
                    break;
                case QualityPreset.Medium:
                    Set(s, 300, 85, ShadowQuality.Medium, false, true, 16, 1f, 1);
                    break;
                case QualityPreset.High:
                    Set(s, 500, 100, ShadowQuality.High, true, true, 32, 0f, 2);
                    break;
            }
            s.preset = preset;
        }

        /// <summary>The named preset whose graphics values equal <paramref name="s"/>'s, otherwise Custom.</summary>
        public static QualityPreset Detect(SettingsData s)
        {
            var probe = new SettingsData();
            foreach (var preset in new[] { QualityPreset.Low, QualityPreset.Medium, QualityPreset.High })
            {
                Apply(preset, probe);
                if (GraphicsEqual(probe, s)) return preset;
            }
            return QualityPreset.Custom;
        }

        public static bool GraphicsEqual(SettingsData a, SettingsData b) =>
            a.viewDistance == b.viewDistance && a.renderScale == b.renderScale && a.shadows == b.shadows && a.ssao == b.ssao &&
            a.gpuOcclusion == b.gpuOcclusion && a.lampLights == b.lampLights &&
            Mathf.Abs(a.smallMeshCulling - b.smallMeshCulling) < 0.001f && a.msaa == b.msaa;

        static void Set(SettingsData s, int viewDistance, int renderScale, ShadowQuality shadows, bool ssao, bool gpuOcclusion,
            int lampLights, float smallMeshCulling, int msaa)
        {
            s.viewDistance = viewDistance;
            s.renderScale = renderScale;
            s.shadows = shadows;
            s.ssao = ssao;
            s.gpuOcclusion = gpuOcclusion;
            s.lampLights = lampLights;
            s.smallMeshCulling = smallMeshCulling;
            s.msaa = msaa;
        }
    }
}
```

`Assets/GameSettings/Runtime/Settings/ViewDistanceRules.cs`:
```csharp
using UnityEngine;

namespace TycoWorld.GameSettings
{
    /// <summary>How the view distance setting drives the camera, fog and culling (spec #4 §5.1–5.2).</summary>
    public static class ViewDistanceRules
    {
        public const int Min = 100;
        public const int Max = 800;
        public const int Step = 50;
        public const float FarClipFactor = 1.05f;
        /// <summary>Exponential fog reaches about 90 % (e^-2.3) at the view distance.</summary>
        public const float FogFactor = 2.3f;
        public const float PropRatio = 0.6f;
        public const float PropMinimum = 80f;

        public static float FarClip(float viewDistance) => viewDistance * FarClipFactor;

        public static float MinFogDensity(float viewDistance) => FogFactor / Mathf.Max(1f, viewDistance);

        /// <summary>Trees and lamp posts disappear (and lamp lights are budgeted) within this distance.</summary>
        public static float PropDistance(float viewDistance) => Mathf.Max(PropMinimum, viewDistance * PropRatio);
    }
}
```

`Assets/GameSettings/Runtime/Settings/SettingsValidator.cs`:
```csharp
using System;
using UnityEngine;

namespace TycoWorld.GameSettings
{
    /// <summary>Clamps loaded or edited settings into the ranges of spec #4 §4 and keeps <c>preset</c> consistent.</summary>
    public static class SettingsValidator
    {
        public const int RenderScaleMin = 50;
        public const int RenderScaleMax = 100;
        public const int RenderScaleStep = 5;
        public const float SensitivityMin = 0.2f;
        public const float SensitivityMax = 3f;
        public const int FovMin = 60;
        public const int FovMax = 100;

        public static int[] LampLightChoices() => new[] { 8, 16, 32 };

        /// <summary>0 = no limit.</summary>
        public static int[] FpsLimitChoices() => new[] { 30, 60, 120, 0 };

        /// <summary>True when an out-of-range value had to change. A preset that does not match the values is fixed silently.</summary>
        public static bool Sanitize(SettingsData s)
        {
            bool changed = false;
            changed |= Fix(ref s.viewDistance, Snap(s.viewDistance, ViewDistanceRules.Min, ViewDistanceRules.Max, ViewDistanceRules.Step));
            changed |= Fix(ref s.renderScale, Snap(s.renderScale, RenderScaleMin, RenderScaleMax, RenderScaleStep));
            changed |= Fix(ref s.lampLights, Nearest(s.lampLights, LampLightChoices()));
            changed |= Fix(ref s.fpsLimit, Array.IndexOf(FpsLimitChoices(), s.fpsLimit) >= 0 ? s.fpsLimit : 60);
            changed |= Fix(ref s.msaa, s.msaa == 2 ? 2 : 1);
            changed |= Fix(ref s.smallMeshCulling, Clamp(s.smallMeshCulling, 0f, 20f, 1f));
            changed |= Fix(ref s.mouseSensitivity, Clamp(s.mouseSensitivity, SensitivityMin, SensitivityMax, 1f));
            changed |= Fix(ref s.fieldOfView, Mathf.Clamp(s.fieldOfView, FovMin, FovMax));
            changed |= Fix(ref s.masterVolume, Mathf.Clamp(s.masterVolume, 0, 100));
            changed |= Fix(ref s.effectsVolume, Mathf.Clamp(s.effectsVolume, 0, 100));
            changed |= Fix(ref s.ambienceVolume, Mathf.Clamp(s.ambienceVolume, 0, 100));
            bool keepCurrent = s.resolutionWidth == 0 && s.resolutionHeight == 0;
            if (!keepCurrent && (s.resolutionWidth <= 0 || s.resolutionHeight <= 0))
            {
                s.resolutionWidth = 0;
                s.resolutionHeight = 0;
                changed = true;
            }
            if (!Enum.IsDefined(typeof(ShadowQuality), s.shadows))
            {
                s.shadows = ShadowQuality.Medium;
                changed = true;
            }
            s.preset = QualityPresets.Detect(s);
            return changed;
        }

        static int Snap(int value, int min, int max, int step)
        {
            int clamped = Mathf.Clamp(value, min, max);
            return min + Mathf.RoundToInt((clamped - min) / (float)step) * step;
        }

        static int Nearest(int value, int[] choices)
        {
            int best = choices[0];
            foreach (int choice in choices)
                if (Math.Abs(choice - value) < Math.Abs(best - value)) best = choice;
            return best;
        }

        static float Clamp(float value, float min, float max, float fallback) => float.IsNaN(value) ? fallback : Mathf.Clamp(value, min, max);

        static bool Fix(ref int field, int value)
        {
            if (field == value) return false;
            field = value;
            return true;
        }

        static bool Fix(ref float field, float value)
        {
            if (field == value) return false;
            field = value;
            return true;
        }
    }
}
```
Catatan: `Fix(ref float…)` membandingkan dengan `==`, jadi NaN selalu dianggap berubah lalu diganti fallback, dan nilai normal yang sudah di dalam rentang tidak dianggap berubah.

- [ ] **Step 4: Jalankan tes, pastikan lulus**

Procedure R+T, `Group = "QualityPresetsTests|SettingsValidatorTests|ViewDistanceRulesTests"`.
Expected: `passed=32 failed=0` (QualityPresets 9 + Validator 13 + Rules 10).

- [ ] **Step 5: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add Assets/GameSettings
git -C "D:/UNITY/project/Tyco World" commit -m "feat(settings): settings model, quality presets, shadow tiers and validation

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

---

### Task 3: `SettingsStore` (PlayerPrefs JSON)

**Files:**
- Create: `Assets/GameSettings/Runtime/Settings/ISettingsBackend.cs`
- Create: `Assets/GameSettings/Runtime/Settings/PlayerPrefsBackend.cs`
- Create: `Assets/GameSettings/Runtime/Settings/SettingsStore.cs`
- Create: `Assets/GameSettings/Tests/MemorySettingsBackend.cs`
- Test: `Assets/GameSettings/Tests/SettingsStoreTests.cs`

**Interfaces:**
- Consumes: `SettingsData`, `SettingsValidator.Sanitize` (Task 2).
- Produces:
  - `interface ISettingsBackend { string Read(); void Write(string json); }`.
  - `PlayerPrefsBackend(string key = PlayerPrefsBackend.DefaultKey)` dengan `DefaultKey = "tyco.settings"`.
  - `SettingsStore(ISettingsBackend)` dengan `SettingsData Load()` dan `void Save(SettingsData)`, plus konstanta `UnreadableWarning`, `UnknownVersionWarningPrefix`, `ClampedWarning`.
  - Tes: `MemorySettingsBackend` (`Json`, `Writes`).

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/GameSettings/Tests/MemorySettingsBackend.cs`:
```csharp
namespace TycoWorld.GameSettings.Tests
{
    /// <summary>In-memory settings storage for tests.</summary>
    internal sealed class MemorySettingsBackend : ISettingsBackend
    {
        public string Json = "";
        public int Writes;

        public string Read() => Json;

        public void Write(string json)
        {
            Json = json;
            Writes++;
        }
    }
}
```

`Assets/GameSettings/Tests/SettingsStoreTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TycoWorld.GameSettings.Tests
{
    public class SettingsStoreTests
    {
        const string TestKey = "tyco.settings.test";

        [TearDown]
        public void TearDown() => PlayerPrefs.DeleteKey(TestKey);

        [Test]
        public void Save_ThenLoad_RoundTrips()
        {
            var backend = new MemorySettingsBackend();
            var store = new SettingsStore(backend);
            var s = new SettingsData();
            QualityPresets.Apply(QualityPreset.High, s);
            s.fpsLimit = 0;
            s.mouseSensitivity = 1.5f;
            s.invertY = true;
            s.ambienceVolume = 40;
            s.showFps = true;
            store.Save(s);
            Assert.AreEqual(1, backend.Writes);
            var loaded = store.Load();
            Assert.AreEqual(QualityPreset.High, loaded.preset);
            Assert.AreEqual(500, loaded.viewDistance);
            Assert.AreEqual(0, loaded.fpsLimit);
            Assert.AreEqual(1.5f, loaded.mouseSensitivity);
            Assert.IsTrue(loaded.invertY);
            Assert.AreEqual(40, loaded.ambienceVolume);
            Assert.IsTrue(loaded.showFps);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Load_Empty_ReturnsDefaultsSilently()
        {
            var loaded = new SettingsStore(new MemorySettingsBackend()).Load();
            Assert.AreEqual(QualityPreset.Medium, loaded.preset);
            Assert.AreEqual(300, loaded.viewDistance);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Load_Garbage_ReturnsDefaultsWithOneWarning()
        {
            var backend = new MemorySettingsBackend { Json = "{ not json" };
            LogAssert.Expect(LogType.Warning, SettingsStore.UnreadableWarning);
            var loaded = new SettingsStore(backend).Load();
            Assert.AreEqual(300, loaded.viewDistance);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Load_UnknownVersion_ReturnsDefaultsWithOneWarning()
        {
            var backend = new MemorySettingsBackend { Json = "{\"version\":7,\"viewDistance\":500}" };
            LogAssert.Expect(LogType.Warning, SettingsStore.UnknownVersionWarningPrefix + "7; memakai nilai default.");
            var loaded = new SettingsStore(backend).Load();
            Assert.AreEqual(300, loaded.viewDistance);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Load_OutOfRange_ClampsWithOneWarning()
        {
            var backend = new MemorySettingsBackend { Json = "{\"version\":1,\"viewDistance\":5000,\"fieldOfView\":10}" };
            LogAssert.Expect(LogType.Warning, SettingsStore.ClampedWarning);
            var loaded = new SettingsStore(backend).Load();
            Assert.AreEqual(800, loaded.viewDistance);
            Assert.AreEqual(60, loaded.fieldOfView);
            Assert.AreEqual(QualityPreset.Custom, loaded.preset);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Load_MissingFields_KeepTheirDefaults()
        {
            var backend = new MemorySettingsBackend { Json = "{\"version\":1,\"viewDistance\":500}" };
            var loaded = new SettingsStore(backend).Load();
            Assert.AreEqual(500, loaded.viewDistance);
            Assert.AreEqual(85, loaded.renderScale);
            Assert.AreEqual(70, loaded.fieldOfView);
            Assert.AreEqual(QualityPreset.Custom, loaded.preset);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void PlayerPrefsBackend_RoundTrips()
        {
            var backend = new PlayerPrefsBackend(TestKey);
            Assert.AreEqual("", backend.Read());
            backend.Write("{\"version\":1}");
            Assert.AreEqual("{\"version\":1}", backend.Read());
            Assert.AreEqual("tyco.settings", PlayerPrefsBackend.DefaultKey);
        }
    }
}
```

- [ ] **Step 2: Jalankan tes, pastikan gagal**

Procedure R+T, `Group = "SettingsStoreTests"`.
Expected: `COMPILE FAILED` (`ISettingsBackend` belum ada).

- [ ] **Step 3: Implementasi**

`Assets/GameSettings/Runtime/Settings/ISettingsBackend.cs`:
```csharp
namespace TycoWorld.GameSettings
{
    /// <summary>Where the settings JSON lives (PlayerPrefs in the game, memory in tests).</summary>
    public interface ISettingsBackend
    {
        /// <summary>The stored JSON, or an empty string when nothing was saved yet.</summary>
        string Read();

        void Write(string json);
    }
}
```

`Assets/GameSettings/Runtime/Settings/PlayerPrefsBackend.cs`:
```csharp
using UnityEngine;

namespace TycoWorld.GameSettings
{
    /// <summary>Stores the settings JSON under one PlayerPrefs key (spec #4 §4).</summary>
    public sealed class PlayerPrefsBackend : ISettingsBackend
    {
        public const string DefaultKey = "tyco.settings";

        readonly string key;

        public PlayerPrefsBackend(string key = DefaultKey) => this.key = key;

        public string Read() => PlayerPrefs.GetString(key, "");

        public void Write(string json)
        {
            PlayerPrefs.SetString(key, json);
            PlayerPrefs.Save();
        }
    }
}
```

`Assets/GameSettings/Runtime/Settings/SettingsStore.cs`:
```csharp
using System;
using System.Globalization;
using UnityEngine;

namespace TycoWorld.GameSettings
{
    /// <summary>Loads and saves SettingsData as JSON; bad data falls back to defaults with exactly one warning (spec #4 §4).</summary>
    public sealed class SettingsStore
    {
        public const string UnreadableWarning = "[Settings] Pengaturan tersimpan tidak bisa dibaca; memakai nilai default.";
        public const string UnknownVersionWarningPrefix = "[Settings] Versi pengaturan tidak dikenal: ";
        public const string ClampedWarning = "[Settings] Beberapa pengaturan di luar rentang; nilainya disesuaikan.";

        readonly ISettingsBackend backend;

        public SettingsStore(ISettingsBackend backend) => this.backend = backend;

        public SettingsData Load()
        {
            string json = backend.Read();
            if (string.IsNullOrEmpty(json)) return new SettingsData();
            SettingsData loaded = null;
            try
            {
                loaded = JsonUtility.FromJson<SettingsData>(json);
            }
            catch (ArgumentException)
            {
            }
            if (loaded == null)
            {
                Debug.LogWarning(UnreadableWarning);
                return new SettingsData();
            }
            if (loaded.version != SettingsData.CurrentVersion)
            {
                Debug.LogWarning(UnknownVersionWarningPrefix + loaded.version.ToString(CultureInfo.InvariantCulture) + "; memakai nilai default.");
                return new SettingsData();
            }
            if (SettingsValidator.Sanitize(loaded)) Debug.LogWarning(ClampedWarning);
            return loaded;
        }

        public void Save(SettingsData settings) => backend.Write(JsonUtility.ToJson(settings));
    }
}
```

- [ ] **Step 4: Jalankan tes, pastikan lulus**

Procedure R+T, `Group = "SettingsStoreTests"`.
Expected: `passed=7 failed=0`.

- [ ] **Step 5: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add Assets/GameSettings
git -C "D:/UNITY/project/Tyco World" commit -m "feat(settings): PlayerPrefs settings store with defaults on bad data

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

---

### Task 4: Culling per chunk × kategori (`WorldChunkIndex` + `DistanceCuller`, WorldGen runtime)

**Files:**
- Create: `Assets/WorldGen/Runtime/Culling/CullCategory.cs`
- Create: `Assets/WorldGen/Runtime/Culling/ChunkGroup.cs`
- Create: `Assets/WorldGen/Runtime/Culling/WorldChunkIndex.cs`
- Create: `Assets/WorldGen/Runtime/Culling/DistanceCuller.cs`
- Create: `Assets/WorldGen/Tests/CullingFixture.cs`
- Test: `Assets/WorldGen/Tests/WorldChunkIndexTests.cs`, `Assets/WorldGen/Tests/DistanceCullerTests.cs`

**Interfaces:**
- Produces (namespace `TycoWorld.WorldGen`):
  - `enum CullCategory { Structure, Prop }`.
  - `ChunkGroup` dengan `Category`, `Chunk` (Vector2Int), `Renderers` (IReadOnlyList<Renderer>), `Bounds`, `Visible`, `float DistanceTo(Vector3)`, dan `void SetVisible(bool)`.
  - `WorldChunkIndex.Build(Transform root) → List<ChunkGroup>` dan `TryParseChunk(string, out Vector2Int) → bool`.
  - `DistanceCuller : MonoBehaviour` dengan:
    - konstanta `IntervalSeconds = 0.25f`, `HysteresisMeters = 10f`, `JumpMeters = 50f`;
    - `ViewDistance`, `PropDistance` (default `+∞` = tidak ada culling);
    - seam `FindWorldRoot` (Func<GeneratedRoot>) dan `FindViewer` (Func<Vector3?>);
    - `HasWorld`, `Groups`, `HiddenCount`, `Tick(float dt)`, `Evaluate(Vector3 viewer)`.

Catatan desain:
- Jarak diukur ke AABB grup (`Bounds.SqrDistance`), jadi jalan/area panjang tetap tampil selama ada bagian yang dekat. Ini versi tepat dari rumus "tepi bounds" di spec §5.1.
- Hanya `Renderer.enabled` yang diubah; collider tidak disentuh.
- `Update` memakai `Time.unscaledDeltaTime`, supaya perubahan view distance tetap berlaku saat game di-pause.

- [ ] **Step 1: Tulis fixture dan tes yang gagal**

`Assets/WorldGen/Tests/CullingFixture.cs`:
```csharp
using System;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    /// <summary>A code-built generated-world hierarchy (Group/Chunk_x_z/cubes) far outside the real world.</summary>
    internal sealed class CullingFixture : IDisposable
    {
        public static readonly Vector3 Origin = new Vector3(5000f, 1000f, 5000f);

        public readonly GameObject Root;
        public readonly GeneratedRoot World;

        public CullingFixture()
        {
            Root = new GameObject("World_Generated_Test");
            World = Root.AddComponent<GeneratedRoot>();
        }

        /// <summary>A 1 m cube (MeshRenderer + BoxCollider) under <paramref name="path"/>, centred at Origin + local.</summary>
        public GameObject Cube(string path, Vector3 local)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(Ensure(path), false);
            cube.transform.position = Origin + local;
            return cube;
        }

        public Transform Ensure(string path)
        {
            var t = Root.transform;
            foreach (var part in path.Split('/'))
            {
                var child = t.Find(part);
                if (child == null)
                {
                    child = new GameObject(part).transform;
                    child.SetParent(t, false);
                }
                t = child;
            }
            return t;
        }

        public void Dispose()
        {
            if (Root != null) UnityEngine.Object.DestroyImmediate(Root);
        }
    }
}
```

`Assets/WorldGen/Tests/WorldChunkIndexTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class WorldChunkIndexTests
    {
        CullingFixture fx;

        [SetUp]
        public void SetUp() => fx = new CullingFixture();

        [TearDown]
        public void TearDown() => fx.Dispose();

        [TestCase("Chunk_3_-2", true, 3, -2)]
        [TestCase("Chunk_-10_5", true, -10, 5)]
        [TestCase("Chunk_0_0", true, 0, 0)]
        [TestCase("Chunk_x_1", false, 0, 0)]
        [TestCase("Chunk_1", false, 0, 0)]
        [TestCase("Chunk_1_2_3", false, 0, 0)]
        [TestCase("Trees", false, 0, 0)]
        public void TryParseChunk(string name, bool ok, int x, int z)
        {
            Assert.AreEqual(ok, WorldChunkIndex.TryParseChunk(name, out var chunk));
            if (ok) Assert.AreEqual(new Vector2Int(x, z), chunk);
        }

        [Test]
        public void Build_IndexesOnlyTheKnownChunkContainers()
        {
            fx.Cube("Roads/Chunk_0_0", new Vector3(0f, 0f, 0f));
            fx.Cube("Buildings/Chunk_1_0", new Vector3(100f, 0f, 0f));
            fx.Cube("Buildings/Chunk_1_0", new Vector3(120f, 0f, 10f));
            fx.Cube("Areas/Chunk_0_1", new Vector3(0f, 0f, 100f));
            fx.Cube("Water/Chunk_2_2", new Vector3(200f, 0f, 200f));
            fx.Cube("Props/Trees/Chunk_0_0", new Vector3(5f, 0f, 5f));
            fx.Cube("Props/Lamps/Chunk_1_1", new Vector3(105f, 0f, 105f));
            fx.Cube("Ground", Vector3.zero);
            fx.Cube("KeyBuildings/Kos Raflesia 3", new Vector3(10f, 0f, 10f));
            fx.Cube("Roads/Misc", new Vector3(0f, 0f, 50f));
            fx.Ensure("Buildings/Chunk_9_9");

            var groups = WorldChunkIndex.Build(fx.Root.transform);

            Assert.AreEqual(6, groups.Count);
            int structures = 0, props = 0;
            foreach (var g in groups)
            {
                if (g.Category == CullCategory.Structure) structures++;
                else props++;
                Assert.IsTrue(g.Visible);
            }
            Assert.AreEqual(4, structures);
            Assert.AreEqual(2, props);
            var buildings = groups.Find(g => g.Chunk == new Vector2Int(1, 0));
            Assert.AreEqual(2, buildings.Renderers.Count);
            Assert.IsTrue(buildings.Bounds.Contains(CullingFixture.Origin + new Vector3(100f, 0f, 0f)));
            Assert.IsTrue(buildings.Bounds.Contains(CullingFixture.Origin + new Vector3(120f, 0f, 10f)));
        }

        [Test]
        public void Build_NullRoot_IsEmpty() => Assert.AreEqual(0, WorldChunkIndex.Build(null).Count);

        [Test]
        public void DistanceTo_IsMeasuredToTheBoundsEdge()
        {
            fx.Cube("Roads/Chunk_1_0", new Vector3(150.5f, 0f, 0f));
            var group = WorldChunkIndex.Build(fx.Root.transform)[0];
            Assert.AreEqual(150f, group.DistanceTo(CullingFixture.Origin), 1e-3f);
        }
    }
}
```

`Assets/WorldGen/Tests/DistanceCullerTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TycoWorld.WorldGen.Tests
{
    public class DistanceCullerTests
    {
        CullingFixture fx;
        DistanceCuller culler;
        Vector3 viewer;
        GeneratedRoot worldForCuller;

        [SetUp]
        public void SetUp()
        {
            fx = new CullingFixture();
            culler = new GameObject("Culler").AddComponent<DistanceCuller>();
            viewer = CullingFixture.Origin;
            worldForCuller = fx.World;
            culler.FindWorldRoot = () => worldForCuller;
            culler.FindViewer = () => viewer;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(culler.gameObject);
            fx.Dispose();
        }

        static bool Shown(GameObject go) => go.GetComponent<Renderer>().enabled;

        // Cubes are 1 m wide: a cube centred at x + 0.5 has its near face exactly x metres from the origin.

        [Test]
        public void InfiniteDistances_HideNothing()
        {
            var far = fx.Cube("Buildings/Chunk_9_0", new Vector3(900.5f, 0f, 0f));
            culler.Tick(0.25f);
            Assert.IsTrue(culler.HasWorld);
            Assert.IsTrue(Shown(far));
            Assert.AreEqual(0, culler.HiddenCount);
        }

        [Test]
        public void Structures_BeyondViewDistance_AreHidden_CollidersStay()
        {
            var near = fx.Cube("Buildings/Chunk_0_0", new Vector3(50.5f, 0f, 0f));
            var far = fx.Cube("Buildings/Chunk_1_0", new Vector3(150.5f, 0f, 0f));
            culler.ViewDistance = 100f;
            culler.Tick(0.25f);
            Assert.IsTrue(Shown(near));
            Assert.IsFalse(Shown(far));
            Assert.IsTrue(far.GetComponent<Collider>().enabled);
            Assert.AreEqual(1, culler.HiddenCount);
        }

        [Test]
        public void Props_UseThePropDistance()
        {
            var tree = fx.Cube("Props/Trees/Chunk_0_0", new Vector3(75.5f, 0f, 0f));
            var building = fx.Cube("Buildings/Chunk_0_0", new Vector3(75.5f, 0f, 0f));
            culler.ViewDistance = 100f;
            culler.PropDistance = 60f;
            culler.Tick(0.25f);
            Assert.IsFalse(Shown(tree));
            Assert.IsTrue(Shown(building));
        }

        [Test]
        public void Hysteresis_KeepsStateWithin10Metres()
        {
            var b = fx.Cube("Buildings/Chunk_1_0", new Vector3(105.5f, 0f, 0f));
            culler.ViewDistance = 100f;
            culler.Tick(0.25f);
            Assert.IsTrue(Shown(b), "105 m: masih di dalam batas + 10 m");
            culler.Evaluate(CullingFixture.Origin + new Vector3(-10f, 0f, 0f));
            Assert.IsFalse(Shown(b), "115 m: lewat batas + 10 m");
            culler.Evaluate(CullingFixture.Origin + new Vector3(12f, 0f, 0f));
            Assert.IsFalse(Shown(b), "93 m: belum masuk batas - 10 m");
            culler.Evaluate(CullingFixture.Origin + new Vector3(16f, 0f, 0f));
            Assert.IsTrue(Shown(b), "89 m: masuk batas - 10 m");
        }

        [Test]
        public void SmallMoves_WaitForTheInterval()
        {
            var b = fx.Cube("Buildings/Chunk_1_0", new Vector3(105.5f, 0f, 0f));
            culler.ViewDistance = 100f;
            culler.Tick(0.25f);
            viewer = CullingFixture.Origin + new Vector3(-10f, 0f, 0f);
            culler.Tick(0.05f);
            Assert.IsTrue(Shown(b), "belum dievaluasi ulang");
            culler.Tick(0.25f);
            Assert.IsFalse(Shown(b));
        }

        [Test]
        public void ViewerJump_EvaluatesImmediately()
        {
            var b = fx.Cube("Buildings/Chunk_1_0", new Vector3(105.5f, 0f, 0f));
            culler.ViewDistance = 100f;
            culler.Tick(0.25f);
            viewer = CullingFixture.Origin + new Vector3(-60f, 0f, 0f);
            culler.Tick(0.01f);
            Assert.IsFalse(Shown(b), "teleport > 50 m langsung dievaluasi");
        }

        [Test]
        public void ChangingViewDistance_EvaluatesOnTheNextTick()
        {
            var b = fx.Cube("Buildings/Chunk_1_0", new Vector3(200.5f, 0f, 0f));
            culler.ViewDistance = 300f;
            culler.Tick(0.25f);
            Assert.IsTrue(Shown(b));
            culler.ViewDistance = 150f;
            culler.Tick(0.01f);
            Assert.IsFalse(Shown(b));
        }

        [Test]
        public void WorldAppearsLater_IndexBuiltThen()
        {
            fx.Cube("Buildings/Chunk_0_0", new Vector3(10.5f, 0f, 0f));
            worldForCuller = null;
            culler.Tick(0.3f);
            Assert.IsFalse(culler.HasWorld);
            worldForCuller = fx.World;
            culler.Tick(0.3f);
            Assert.IsTrue(culler.HasWorld);
            Assert.AreEqual(1, culler.Groups.Count);
        }

        [Test]
        public void WorldMissing_NoErrors()
        {
            worldForCuller = null;
            for (int i = 0; i < 10; i++) culler.Tick(0.3f);
            Assert.IsFalse(culler.HasWorld);
            Assert.AreEqual(0, culler.Groups.Count);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void WorldDestroyed_ResetsWithoutErrors()
        {
            fx.Cube("Buildings/Chunk_0_0", new Vector3(10.5f, 0f, 0f));
            culler.ViewDistance = 100f;
            culler.Tick(0.25f);
            Assert.IsTrue(culler.HasWorld);
            Object.DestroyImmediate(fx.Root);
            culler.Tick(0.25f);
            Assert.IsFalse(culler.HasWorld);
            Assert.AreEqual(0, culler.HiddenCount);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void NoViewer_LeavesEverythingAsIs()
        {
            var far = fx.Cube("Buildings/Chunk_9_0", new Vector3(900.5f, 0f, 0f));
            culler.ViewDistance = 100f;
            culler.FindViewer = () => null;
            culler.Tick(0.25f);
            Assert.IsTrue(Shown(far));
        }

        [Test]
        public void ShowAll_RestoresHiddenRenderers()
        {
            var far = fx.Cube("Buildings/Chunk_1_0", new Vector3(150.5f, 0f, 0f));
            culler.ViewDistance = 100f;
            culler.Tick(0.25f);
            Assert.IsFalse(Shown(far));
            culler.ShowAll();
            Assert.IsTrue(Shown(far));
            Assert.AreEqual(0, culler.HiddenCount);
        }
    }
}
```

- [ ] **Step 2: Jalankan tes, pastikan gagal**

Procedure R+T, `Group = "WorldChunkIndexTests|DistanceCullerTests"`.
Expected: `COMPILE FAILED` (`WorldChunkIndex` belum ada).

- [ ] **Step 3: Implementasi**

`Assets/WorldGen/Runtime/Culling/CullCategory.cs`:
```csharp
namespace TycoWorld.WorldGen
{
    /// <summary>Structures (roads, areas, water, buildings) stay until the view distance; props (trees, lamp posts) go sooner.</summary>
    public enum CullCategory
    {
        Structure,
        Prop
    }
}
```

`Assets/WorldGen/Runtime/Culling/ChunkGroup.cs`:
```csharp
using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.WorldGen
{
    /// <summary>The renderers of one Chunk_x_z container (one category) and their combined world bounds.</summary>
    public sealed class ChunkGroup
    {
        readonly Renderer[] renderers;

        public ChunkGroup(CullCategory category, Vector2Int chunk, Renderer[] renderers, Bounds bounds)
        {
            Category = category;
            Chunk = chunk;
            this.renderers = renderers;
            Bounds = bounds;
            Visible = true;
        }

        public CullCategory Category { get; }
        public Vector2Int Chunk { get; }
        public IReadOnlyList<Renderer> Renderers => renderers;
        public Bounds Bounds { get; }
        public bool Visible { get; private set; }

        /// <summary>Distance from <paramref name="point"/> to the nearest point of the group's bounds (0 inside).</summary>
        public float DistanceTo(Vector3 point) => Mathf.Sqrt(Bounds.SqrDistance(point));

        /// <summary>Enables or disables the renderers only (colliders are untouched); no-op when unchanged.</summary>
        public void SetVisible(bool visible)
        {
            if (Visible == visible) return;
            Visible = visible;
            foreach (var r in renderers)
                if (r != null) r.enabled = visible;
        }
    }
}
```

`Assets/WorldGen/Runtime/Culling/WorldChunkIndex.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace TycoWorld.WorldGen
{
    /// <summary>
    /// Reads the generated hierarchy (Roads|Buildings|Areas|Water|Props/Trees|Props/Lamps → Chunk_x_z) into cullable
    /// groups. Ground, KeyBuildings and WorldEdge are not indexed, so they are always drawn (spec #4 §5.1).
    /// </summary>
    public static class WorldChunkIndex
    {
        const string ChunkPrefix = "Chunk_";

        public static List<ChunkGroup> Build(Transform root)
        {
            var groups = new List<ChunkGroup>();
            if (root == null) return groups;
            Add(root.Find("Roads"), CullCategory.Structure, groups);
            Add(root.Find("Buildings"), CullCategory.Structure, groups);
            Add(root.Find("Areas"), CullCategory.Structure, groups);
            Add(root.Find("Water"), CullCategory.Structure, groups);
            Add(root.Find("Props/Trees"), CullCategory.Prop, groups);
            Add(root.Find("Props/Lamps"), CullCategory.Prop, groups);
            return groups;
        }

        public static bool TryParseChunk(string name, out Vector2Int chunk)
        {
            chunk = default;
            if (name == null || !name.StartsWith(ChunkPrefix, StringComparison.Ordinal)) return false;
            var parts = name.Substring(ChunkPrefix.Length).Split('_');
            if (parts.Length != 2) return false;
            if (!int.TryParse(parts[0], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int x)) return false;
            if (!int.TryParse(parts[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int z)) return false;
            chunk = new Vector2Int(x, z);
            return true;
        }

        static void Add(Transform container, CullCategory category, List<ChunkGroup> groups)
        {
            if (container == null) return;
            for (int i = 0; i < container.childCount; i++)
            {
                var child = container.GetChild(i);
                if (!TryParseChunk(child.name, out var chunk)) continue;
                var renderers = child.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) continue;
                var bounds = renderers[0].bounds;
                for (int r = 1; r < renderers.Length; r++) bounds.Encapsulate(renderers[r].bounds);
                groups.Add(new ChunkGroup(category, chunk, renderers, bounds));
            }
        }
    }
}
```

`Assets/WorldGen/Runtime/Culling/DistanceCuller.cs`:
```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.WorldGen
{
    /// <summary>
    /// Hides the renderers of whole chunk groups beyond the view distance (props beyond the prop distance), every 0.25 s
    /// or at once after a jump over 50 m, with 10 m hysteresis. Colliders stay. Infinite distances (default) cull nothing
    /// (spec #4 §5.1).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DistanceCuller : MonoBehaviour
    {
        public const float IntervalSeconds = 0.25f;
        public const float HysteresisMeters = 10f;
        public const float JumpMeters = 50f;

        static readonly IReadOnlyList<ChunkGroup> NoGroups = Array.Empty<ChunkGroup>();

        float viewDistance = float.PositiveInfinity;
        float propDistance = float.PositiveInfinity;
        List<ChunkGroup> groups;
        GeneratedRoot root;
        float timer, searchTimer;
        Vector3 lastViewer;
        bool hasViewer, dirty;

        public float ViewDistance
        {
            get => viewDistance;
            set
            {
                if (value == viewDistance) return;
                viewDistance = value;
                dirty = true;
            }
        }

        public float PropDistance
        {
            get => propDistance;
            set
            {
                if (value == propDistance) return;
                propDistance = value;
                dirty = true;
            }
        }

        /// <summary>Finds the generated world (another scene, so it cannot be serialized); tests replace it.</summary>
        public Func<GeneratedRoot> FindWorldRoot { get; set; } = () => UnityEngine.Object.FindAnyObjectByType<GeneratedRoot>();

        /// <summary>Where distances are measured from (the active camera); tests replace it.</summary>
        public Func<Vector3?> FindViewer { get; set; } = MainCameraPosition;

        public bool HasWorld => groups != null;
        public IReadOnlyList<ChunkGroup> Groups => groups != null ? (IReadOnlyList<ChunkGroup>)groups : NoGroups;
        public int HiddenCount { get; private set; }

        void Update()
        {
            if (!Application.isPlaying) return;
            Tick(Time.unscaledDeltaTime);
        }

        void OnDisable() => ShowAll();

        public void Tick(float dt)
        {
            if (groups != null && root == null)
            {
                // The world scene was unloaded: its renderers are gone with it.
                groups = null;
                hasViewer = false;
                HiddenCount = 0;
            }
            if (groups == null)
            {
                searchTimer -= dt;
                if (searchTimer > 0f) return;
                searchTimer = IntervalSeconds;
                root = FindWorldRoot != null ? FindWorldRoot() : null;
                if (root == null) return;
                groups = WorldChunkIndex.Build(root.transform);
                hasViewer = false;
            }
            var viewer = FindViewer != null ? FindViewer() : null;
            if (!viewer.HasValue) return;
            timer += dt;
            bool jumped = !hasViewer || (viewer.Value - lastViewer).sqrMagnitude > JumpMeters * JumpMeters;
            if (!dirty && !jumped && timer < IntervalSeconds) return;
            Evaluate(viewer.Value);
        }

        public void Evaluate(Vector3 viewer)
        {
            if (groups == null) return;
            timer = 0f;
            dirty = false;
            lastViewer = viewer;
            hasViewer = true;
            int hidden = 0;
            foreach (var g in groups)
            {
                float limit = g.Category == CullCategory.Prop ? propDistance : viewDistance;
                float distance = g.DistanceTo(viewer);
                bool show = g.Visible ? distance <= limit + HysteresisMeters : distance <= limit - HysteresisMeters;
                g.SetVisible(show);
                if (!show) hidden++;
            }
            HiddenCount = hidden;
        }

        public void ShowAll()
        {
            if (groups == null) return;
            foreach (var g in groups) g.SetVisible(true);
            HiddenCount = 0;
        }

        static Vector3? MainCameraPosition()
        {
            var cam = Camera.main;
            return cam != null ? cam.transform.position : (Vector3?)null;
        }
    }
}
```

- [ ] **Step 4: Jalankan tes, pastikan lulus**

Procedure R+T, `Group = "WorldChunkIndexTests|DistanceCullerTests"`.
Expected: `passed=22 failed=0` (index 10, culler 12).

- [ ] **Step 5: Jalankan seluruh tes WorldGen (regresi)**

Procedure R+T, `Group = "TycoWorld.WorldGen.Tests"`.
Expected: `failed=0`.

- [ ] **Step 6: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add Assets/WorldGen/Runtime Assets/WorldGen/Tests
git -C "D:/UNITY/project/Tyco World" commit -m "feat(worldgen): distance culling of generated chunk groups

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

---

### Task 5: Knob Player (sensitivitas, invert Y, Esc)

**Files:**
- Modify: `Assets/Player/Runtime/Input/PlayerInputReader.cs`
- Modify: `Assets/Player/Runtime/Core/FirstPersonPlayer.cs`
- Test: `Assets/Player/Tests/PlayerInputReaderTests.cs`, `Assets/Player/Tests/FirstPersonPlayerTests.cs`

**Interfaces:**
- Produces:
  - `PlayerInputReader.ApplyLookSettings(Vector2 degrees, float sensitivity, bool invertY) → Vector2`.
  - `FirstPersonPlayer.LookSensitivity` (float, default 1), `InvertY` (bool, default false), dan `EscapeUnlocksCursor` (bool, default true).
  - Sensitivitas dan invert hanya berlaku untuk pembaca keyboard/mouse asli. `ScriptedIntentSource` tidak terpengaruh.

- [ ] **Step 1: Tulis tes yang gagal**

Tambahkan ke kelas `PlayerInputReaderTests` (`Assets/Player/Tests/PlayerInputReaderTests.cs`), sebelum kurung tutup kelas:
```csharp
        [Test]
        public void ApplyLookSettings_ScalesAndInvertsVertical()
        {
            Assert.AreEqual(new Vector2(3f, 1.5f), PlayerInputReader.ApplyLookSettings(new Vector2(2f, 1f), 1.5f, false));
            Assert.AreEqual(new Vector2(2f, -1f), PlayerInputReader.ApplyLookSettings(new Vector2(2f, 1f), 1f, true));
        }
```

Tambahkan ke kelas `FirstPersonPlayerTests` (`Assets/Player/Tests/FirstPersonPlayerTests.cs`), sebelum kurung tutup kelas:
```csharp
        [Test]
        public void LookSettings_DoNotTouchScriptedLook()
        {
            var rig = FloorRig();
            rig.Player.LookSensitivity = 3f;
            rig.Player.InvertY = true;
            rig.Intents.Press(new PlayerIntent { LookDegrees = new Vector2(30f, 10f) });
            rig.Player.Tick(1f / 60f);
            Assert.AreEqual(30f, rig.Player.Yaw, 1e-3f);
            Assert.AreEqual(10f, rig.Player.Pitch, 1e-3f);
        }

        [Test]
        public void SettingsKnobs_DefaultToTheOldBehaviour()
        {
            var rig = FloorRig();
            Assert.AreEqual(1f, rig.Player.LookSensitivity);
            Assert.IsFalse(rig.Player.InvertY);
            Assert.IsTrue(rig.Player.EscapeUnlocksCursor);
        }
```

- [ ] **Step 2: Jalankan tes, pastikan gagal**

Procedure R+T, `Group = "PlayerInputReaderTests|FirstPersonPlayerTests"`.
Expected: `COMPILE FAILED` (`ApplyLookSettings` / `LookSensitivity` belum ada).

- [ ] **Step 3: Implementasi**

Di `Assets/Player/Runtime/Input/PlayerInputReader.cs`, tambahkan setelah method `LookFromPointer`:
```csharp

        /// <summary>Applies the settings page's sensitivity multiplier and vertical inversion to a look delta in degrees (spec #4 §4).</summary>
        public static Vector2 ApplyLookSettings(Vector2 degrees, float sensitivity, bool invertY) =>
            new Vector2(degrees.x * sensitivity, (invertY ? -degrees.y : degrees.y) * sensitivity);
```

Di `Assets/Player/Runtime/Core/FirstPersonPlayer.cs`:

1. Setelah baris `public IReadOnlyList<IPlayerModule> Modules => modules;` tambahkan:
```csharp

        /// <summary>Settings multiplier on mouse look; only the keyboard/mouse reader is scaled, scripted sources are not.</summary>
        public float LookSensitivity { get; set; } = 1f;

        /// <summary>Settings: invert vertical mouse look (keyboard/mouse reader only).</summary>
        public bool InvertY { get; set; }

        /// <summary>False while the pause menu owns Esc (spec #4 §6); true keeps Esc = release the cursor.</summary>
        public bool EscapeUnlocksCursor { get; set; } = true;
```

2. Di `Tick`, ganti baris:
```csharp
            Intent = Filter(source != null ? source.Read(dt) : default);
```
dengan:
```csharp
            var raw = source != null ? source.Read(dt) : default;
            if (reader != null && ReferenceEquals(source, reader))
                raw.LookDegrees = PlayerInputReader.ApplyLookSettings(raw.LookDegrees, LookSensitivity, InvertY);
            Intent = Filter(raw);
```

3. Di `HandleCursor`, ganti baris:
```csharp
            if (reader.ReleaseCursorPressed) UnlockCursor();
```
dengan:
```csharp
            if (EscapeUnlocksCursor && reader.ReleaseCursorPressed) UnlockCursor();
```

- [ ] **Step 4: Jalankan tes, pastikan lulus**

Procedure R+T, `Group = "PlayerInputReaderTests|FirstPersonPlayerTests"`.
Expected: `failed=0` (3 tes baru ikut lulus).

- [ ] **Step 5: Regresi Player**

Procedure R+T, `Group = "TycoWorld.Player.Tests"`.
Expected: `failed=0`.

- [ ] **Step 6: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add Assets/Player/Runtime Assets/Player/Tests
git -C "D:/UNITY/project/Tyco World" commit -m "feat(player): look sensitivity, invert Y and Esc ownership knobs

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

---

### Task 6: Knob DayCycle (kabut minimum, jatah lampu jalan, level ambience)

**Files:**
- Modify (ganti seluruh isi): `Assets/DayCycle/Runtime/Lamps/StreetLampSystem.cs`
- Modify: `Assets/DayCycle/Runtime/Audio/AmbienceAudio.cs`
- Modify: `Assets/DayCycle/Runtime/DayCycleDirector.cs`
- Modify: `Assets/DayCycle/Tests/DirectorFixture.cs`
- Create: `Assets/DayCycle/Tests/StreetLampBudgetTests.cs`
- Test: `Assets/DayCycle/Tests/DayCycleDirectorTests.cs`, `Assets/DayCycle/Tests/AmbienceAudioTests.cs`

**Interfaces:**
- Consumes: `StreetLamp`, `LampFlicker`, `DayCycleProfile.lampIntensity` (sudah ada).
- Produces:
  - `StreetLampSystem`:
    - `LightBudget` (int, default `int.MaxValue`), `MaxLightDistance` (float, default `+∞`), `Viewer` (Vector3?);
    - `LightsOn` (int);
    - `Apply(float intensity01, float horrorWeight, float timeSeconds, float dt = 0f)`;
    - konstanta `SelectIntervalSeconds = 0.25f`, `FadeSeconds = 0.5f`, `ViewerJumpMeters = 50f`.
  - `AmbienceAudio.Level` (0–1, default 1).
  - `DayCycleDirector.MinFogDensity` (float, default 0) dan `FindViewer` (Func<Vector3?>, default posisi `Camera.main`).

Aturan jatah lampu (spec §5.3):
- Tanpa jatah (default), perilaku lama tidak berubah.
- Dengan jatah dan `Viewer` terisi, seleksi diulang tiap 0,25 s. Seleksi juga langsung diulang saat viewer melompat lebih dari 50 m, saat setter jatah/jarak dipanggil, atau setelah `Collect`.
- Yang dipilih adalah N lampu *eligible* terdekat dalam radius `MaxLightDistance`. Eligible artinya malam (intensity > 0) dan bukan lampu Dead saat horor.
- Kedip tetap instan. Fade 0,5 s hanya berlaku untuk lampu yang masuk/keluar jatah.
- `dt <= 0`, atau frame pertama setelah `Collect`, langsung *snap* tanpa fade.
- `Light` disentuh hanya saat nilainya berubah.

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/DayCycle/Tests/StreetLampBudgetTests.cs`:
```csharp
using System.Collections.Generic;
using NUnit.Framework;
using TycoWorld.WorldGen;
using UnityEngine;

namespace TycoWorld.DayCycle.Tests
{
    public class StreetLampBudgetTests
    {
        static readonly Vector3 Base = new Vector3(5000f, 1000f, 5000f);
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

        StreetLamp LampAt(float x) => Track(LampFixture.Create(null, Base + new Vector3(x, 0f, 0f)));

        StreetLamp Track(StreetLamp lamp)
        {
            created.Add(lamp.gameObject);
            return lamp;
        }

        float Full => profile.lampIntensity;

        [Test]
        public void NoBudget_EveryLitLampEmits()
        {
            var a = LampAt(10f);
            var b = LampAt(20f);
            system.Collect(new[] { a, b });
            system.Viewer = Base;
            system.Apply(1f, 0f, 0f);
            Assert.IsTrue(a.Light.enabled && b.Light.enabled);
            Assert.AreEqual(2, system.LightsOn);
        }

        [Test]
        public void Budget_OnlyTheNearestEmit_HeadsStayLit()
        {
            var lamps = new[] { LampAt(10f), LampAt(20f), LampAt(30f), LampAt(40f), LampAt(50f) };
            system.Collect(lamps);
            system.LightBudget = 2;
            system.Viewer = Base;
            system.Apply(1f, 0f, 0f);
            Assert.IsTrue(lamps[0].Light.enabled);
            Assert.IsTrue(lamps[1].Light.enabled);
            for (int i = 2; i < lamps.Length; i++) Assert.IsFalse(lamps[i].Light.enabled, "lampu " + i);
            Assert.AreEqual(2, system.LightsOn);
            Assert.AreEqual(5, system.LitCount);
            Assert.AreSame(lamps[0].HeadRenderer.sharedMaterials[1], lamps[4].HeadRenderer.sharedMaterials[1], "kepala lampu jauh tetap menyala");
        }

        [Test]
        public void MaxLightDistance_LimitsTheBudget()
        {
            var lamps = new[] { LampAt(10f), LampAt(20f), LampAt(30f) };
            system.Collect(lamps);
            system.LightBudget = 10;
            system.MaxLightDistance = 25f;
            system.Viewer = Base;
            system.Apply(1f, 0f, 0f);
            Assert.IsTrue(lamps[0].Light.enabled);
            Assert.IsTrue(lamps[1].Light.enabled);
            Assert.IsFalse(lamps[2].Light.enabled);
        }

        [Test]
        public void Selection_FadesOverHalfASecond()
        {
            var a = LampAt(0f);
            var b = LampAt(40f);
            system.Collect(new[] { a, b });
            system.LightBudget = 1;
            system.Viewer = Base;
            system.Apply(1f, 0f, 0f);
            Assert.AreEqual(Full, a.Light.intensity, 1e-4f);
            Assert.IsFalse(b.Light.enabled);

            system.Viewer = Base + new Vector3(40f, 0f, 0f);
            system.Apply(1f, 0f, 0f, 0.25f);
            Assert.IsTrue(a.Light.enabled);
            Assert.IsTrue(b.Light.enabled);
            Assert.AreEqual(Full * 0.5f, a.Light.intensity, 1e-4f);
            Assert.AreEqual(Full * 0.5f, b.Light.intensity, 1e-4f);

            system.Apply(1f, 0f, 0f, 0.25f);
            Assert.IsFalse(a.Light.enabled);
            Assert.AreEqual(Full, b.Light.intensity, 1e-4f);
        }

        [Test]
        public void ViewerJump_ReselectsImmediately()
        {
            var a = LampAt(0f);
            var c = LampAt(60f);
            system.Collect(new[] { a, c });
            system.LightBudget = 1;
            system.Viewer = Base;
            system.Apply(1f, 0f, 0f);
            Assert.IsFalse(c.Light.enabled);
            system.Viewer = Base + new Vector3(60f, 0f, 0f);
            system.Apply(1f, 0f, 0f, 0.01f);
            Assert.IsTrue(c.Light.enabled);
        }

        [Test]
        public void FirstFrameAfterCollect_DoesNotFade()
        {
            var a = LampAt(0f);
            var b = LampAt(30f);
            system.Collect(new[] { a, b });
            system.LightBudget = 1;
            system.Viewer = Base;
            system.Apply(1f, 0f, 0f, 0.016f);
            Assert.IsTrue(a.Light.enabled);
            Assert.IsFalse(b.Light.enabled, "lampu di luar jatah langsung mati, tidak memudar dari menyala");
        }

        [Test]
        public void HorrorDeadLamps_DoNotUseTheBudget()
        {
            var dead = Track(LampFixture.Create(null, LampFixture.PositionWithRole(LampRole.Dead, profile)));
            var normal = Track(LampFixture.Create(null, LampFixture.PositionWithRole(LampRole.Normal, profile)));
            system.Collect(new[] { dead, normal });
            system.LightBudget = 1;
            system.Viewer = dead.transform.position;
            system.Apply(1f, 1f, 0f);
            Assert.IsFalse(dead.Light.enabled);
            Assert.IsTrue(normal.Light.enabled);
        }

        [Test]
        public void NoViewer_KeepsEveryLampEmitting()
        {
            var a = LampAt(0f);
            var b = LampAt(30f);
            system.Collect(new[] { a, b });
            system.LightBudget = 1;
            system.Viewer = null;
            system.Apply(1f, 0f, 0f);
            Assert.IsTrue(a.Light.enabled && b.Light.enabled);
        }

        [Test]
        public void Daytime_NoLightsEvenWithABudget()
        {
            var a = LampAt(0f);
            system.Collect(new[] { a });
            system.LightBudget = 16;
            system.Viewer = Base;
            system.Apply(0f, 0f, 0f);
            Assert.IsFalse(a.Light.enabled);
            Assert.AreEqual(0, system.LightsOn);
        }

        [Test]
        public void Intensity_FollowsTheCycle()
        {
            var a = LampAt(0f);
            system.Collect(new[] { a });
            system.LightBudget = 1;
            system.Viewer = Base;
            system.Apply(0.5f, 0f, 0f);
            Assert.AreEqual(Full * 0.5f, a.Light.intensity, 1e-4f);
        }
    }
}
```

Tambahkan ke kelas `DayCycleDirectorTests` (`Assets/DayCycle/Tests/DayCycleDirectorTests.cs`), sebelum kurung tutup kelas:
```csharp
        [Test]
        public void MinFogDensity_ThickensThinDayFog()
        {
            fx.Director.Initialize(12f);
            fx.Director.MinFogDensity = 0.0077f;
            fx.Director.Tick(0.016f);
            Assert.AreEqual(0.0077f, RenderSettings.fogDensity, 1e-6f);
        }

        [Test]
        public void MinFogDensity_NeverThinsHorrorFog()
        {
            fx.Director.Initialize(22f);
            fx.Director.MinFogDensity = 0.0077f;
            fx.Director.Tick(0.016f);
            Assert.AreEqual(0.032f, RenderSettings.fogDensity, 1e-4f);
        }

        [Test]
        public void LampBudget_IsMeasuredFromTheViewer()
        {
            var rootGo = fx.Track(new GameObject("World_Generated_Test"));
            var root = rootGo.AddComponent<GeneratedRoot>();
            var near = LampFixture.Create(rootGo.transform, LampFixture.PositionWithRole(LampRole.Normal, fx.Profile));
            var far = LampFixture.Create(rootGo.transform, near.transform.position + new Vector3(0f, 0f, 30f));
            fx.Director.FindWorldRoot = () => root;
            fx.Director.FindViewer = () => near.transform.position;
            fx.Director.Initialize(22f);
            fx.Director.Lamps.LightBudget = 1;
            fx.Director.Tick(0.016f);
            Assert.IsTrue(near.Light.enabled);
            Assert.IsFalse(far.Light.enabled);
            Assert.AreEqual(1, fx.Director.Lamps.LightsOn);
        }
```
(`DayCycleDirectorTests` sudah memakai `using TycoWorld.WorldGen;` untuk `GeneratedRoot`. Kalau belum, tambahkan.)

Tambahkan ke kelas `AmbienceAudioTests` (`Assets/DayCycle/Tests/AmbienceAudioTests.cs`), sebelum kurung tutup kelas:
```csharp
        [Test]
        public void Level_ScalesEveryLayer()
        {
            ambience.Initialize(profile);
            ambience.Level = 0.5f;
            ambience.Apply(1f, 1f, 1f);
            Assert.AreEqual(profile.dayVolume * 0.5f, ambience.Day.volume, 1e-5f);
            Assert.AreEqual(profile.nightVolume * 0.5f, ambience.Night.volume, 1e-5f);
            Assert.AreEqual(profile.droneVolume * 0.5f, ambience.Drone.volume, 1e-5f);
        }

        [Test]
        public void Level_IsClampedTo01()
        {
            ambience.Level = 3f;
            Assert.AreEqual(1f, ambience.Level);
            ambience.Level = -1f;
            Assert.AreEqual(0f, ambience.Level);
        }
```

- [ ] **Step 2: Jalankan tes, pastikan gagal**

Procedure R+T, `Group = "StreetLampBudgetTests|DayCycleDirectorTests|AmbienceAudioTests"`.
Expected: `COMPILE FAILED` (`LightBudget`, `MinFogDensity`, `Level` belum ada).

- [ ] **Step 3: Ganti seluruh isi `StreetLampSystem.cs`**

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
    /// With a light budget only the nearest lamps to <see cref="Viewer"/> emit light and the rest keep only their lit
    /// heads (spec #4 §5.3). Light components are only touched when their value changes.
    /// </summary>
    public sealed class StreetLampSystem : IDisposable
    {
        public const float SelectIntervalSeconds = 0.25f;
        public const float FadeSeconds = 0.5f;
        public const float ViewerJumpMeters = 50f;

        sealed class Entry
        {
            public StreetLamp Lamp;
            public uint Seed;
            public LampRole Role;
            public Material[] Original, On, Off;
            public int Shown = -1;
            public bool Selected = true;
            public float Fade = 1f;
            public bool LightOn;
            public float LightIntensity;
        }

        struct Candidate
        {
            public float SqrDistance;
            public int Index;
        }

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        readonly DayCycleProfile profile;
        readonly List<Entry> entries = new List<Entry>();
        readonly List<Candidate> candidates = new List<Candidate>();
        Material headOn, headOff;
        Color baseEmission;
        int lightBudget = int.MaxValue;
        float maxLightDistance = float.PositiveInfinity;
        bool selectionDirty = true;
        bool snapNextApply = true;
        float selectTimer;
        Vector3? lastViewer;

        public StreetLampSystem(DayCycleProfile profile) => this.profile = profile;

        public bool Collected { get; private set; }
        public int Count => entries.Count;
        /// <summary>Lamps that are on (lit head), whether or not their light is within the budget.</summary>
        public int LitCount { get; private set; }
        /// <summary>Lamps whose Light component is enabled: at most the budget plus lamps still fading out.</summary>
        public int LightsOn { get; private set; }

        /// <summary>How many lamps may emit light; int.MaxValue (default) keeps every lit lamp's light on.</summary>
        public int LightBudget
        {
            get => lightBudget;
            set
            {
                lightBudget = Mathf.Max(0, value);
                selectionDirty = true;
            }
        }

        /// <summary>Lamps farther than this from the viewer never emit light; infinity (default) = no limit.</summary>
        public float MaxLightDistance
        {
            get => maxLightDistance;
            set
            {
                maxLightDistance = Mathf.Max(0f, value);
                selectionDirty = true;
            }
        }

        /// <summary>Where the budget is measured from (the active camera); null applies no budget.</summary>
        public Vector3? Viewer { get; set; }

        bool Budgeted => lightBudget < int.MaxValue || !float.IsPositiveInfinity(maxLightDistance);

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
                var entry = new Entry
                {
                    Lamp = lamp,
                    Seed = LampFlicker.Seed(lamp.transform.position),
                    LightOn = lamp.Light.enabled,
                    LightIntensity = lamp.Light.intensity
                };
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
            selectionDirty = true;
            snapNextApply = true;
        }

        /// <param name="dt">Seconds since the last call; drives the budget fades (0 = snap).</param>
        public void Apply(float intensity01, float horrorWeight, float timeSeconds, float dt = 0f)
        {
            if (headOn != null) headOn.SetColor(EmissionId, baseEmission * intensity01);
            bool horror = horrorWeight > 0f;
            Select(intensity01, horror, dt);
            bool snap = dt <= 0f || snapNextApply;
            snapNextApply = false;
            float full = profile.lampIntensity * intensity01;
            int lit = 0, lightsOn = 0;
            foreach (var e in entries)
            {
                if (e.Lamp == null) continue;
                bool on = Eligible(e, intensity01, horror);
                if (on && horror && e.Role == LampRole.Flicker) on = LampFlicker.IsLit(e.Seed, timeSeconds);
                float target = e.Selected ? 1f : 0f;
                e.Fade = snap ? target : Mathf.MoveTowards(e.Fade, target, dt / FadeSeconds);
                var light = e.Lamp.Light;
                if (light != null)
                {
                    bool enabled = on && e.Fade > 0f;
                    if (enabled != e.LightOn)
                    {
                        light.enabled = enabled;
                        e.LightOn = enabled;
                    }
                    float intensity = full * e.Fade;
                    if (enabled && intensity != e.LightIntensity)
                    {
                        light.intensity = intensity;
                        e.LightIntensity = intensity;
                    }
                    if (enabled) lightsOn++;
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
            LightsOn = lightsOn;
        }

        public void Dispose()
        {
            foreach (var e in entries)
                if (e.Lamp != null && e.Original != null && e.Lamp.HeadRenderer != null)
                    e.Lamp.HeadRenderer.sharedMaterials = e.Original;
            entries.Clear();
            LitCount = 0;
            LightsOn = 0;
            DestroyCopy(ref headOn);
            DestroyCopy(ref headOff);
        }

        static bool Eligible(Entry e, float intensity01, bool horror) => intensity01 > 0f && !(horror && e.Role == LampRole.Dead);

        void Select(float intensity01, bool horror, float dt)
        {
            if (!Budgeted || !Viewer.HasValue)
            {
                foreach (var e in entries) e.Selected = true;
                lastViewer = null;
                return;
            }
            var viewer = Viewer.Value;
            selectTimer += Mathf.Max(0f, dt);
            bool jumped = !lastViewer.HasValue || (viewer - lastViewer.Value).sqrMagnitude > ViewerJumpMeters * ViewerJumpMeters;
            if (!selectionDirty && !jumped && selectTimer < SelectIntervalSeconds) return;
            selectTimer = 0f;
            selectionDirty = false;
            lastViewer = viewer;
            float maxSqr = float.IsPositiveInfinity(maxLightDistance) ? float.PositiveInfinity : maxLightDistance * maxLightDistance;
            candidates.Clear();
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                e.Selected = false;
                if (e.Lamp == null || !Eligible(e, intensity01, horror)) continue;
                float sqr = (e.Lamp.transform.position - viewer).sqrMagnitude;
                if (sqr <= maxSqr) candidates.Add(new Candidate { SqrDistance = sqr, Index = i });
            }
            candidates.Sort(ByDistance);
            int count = Mathf.Min(lightBudget, candidates.Count);
            for (int k = 0; k < count; k++) entries[candidates[k].Index].Selected = true;
        }

        static int ByDistance(Candidate a, Candidate b) => a.SqrDistance.CompareTo(b.SqrDistance);

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

- [ ] **Step 4: Level di `AmbienceAudio`**

Di `Assets/DayCycle/Runtime/Audio/AmbienceAudio.cs`:

1. Ganti baris `DayCycleProfile profile;` dengan:
```csharp
        DayCycleProfile profile;
        float level = 1f;

        /// <summary>Settings multiplier (0–1) on top of the day cycle's layer volumes (spec #4 §5.5).</summary>
        public float Level
        {
            get => level;
            set => level = Mathf.Clamp01(value);
        }
```
2. Di `Apply`, ganti ketiga baris `Set(...)` dengan:
```csharp
            Set(Day, dayWeight * profile.dayVolume * level);
            Set(Night, nightWeight * profile.nightVolume * level);
            Set(Drone, droneWeight * profile.droneVolume * level);
```

- [ ] **Step 5: Knob di `DayCycleDirector`**

Di `Assets/DayCycle/Runtime/DayCycleDirector.cs`:

1. Setelah baris `public Func<GeneratedRoot> FindWorldRoot { get; set; } = () => UnityEngine.Object.FindAnyObjectByType<GeneratedRoot>();` tambahkan:
```csharp

        /// <summary>Where lamp lights are budgeted from: the main camera; tests replace it.</summary>
        public Func<Vector3?> FindViewer { get; set; } = MainCameraPosition;

        /// <summary>The fog is never thinner than this (view distance setting, spec #4 §5.2); 0 = profile fog only.</summary>
        public float MinFogDensity { get; set; }
```
2. Di `Initialize(float startHours)`, ganti baris `Apply();` (tepat setelah `IsInitialized = true;`) dengan `Apply(0f);`.
3. Di `Tick`, ganti baris `Apply();` (tepat setelah `PollLamps();`) dengan `Apply(dt);`.
4. Ganti signature `void Apply()` dengan `void Apply(float dt)`.
5. Di `Apply`, ganti baris:
```csharp
            RenderSettings.fogDensity = s.FogDensity;
```
dengan:
```csharp
            RenderSettings.fogDensity = Mathf.Max(s.FogDensity, MinFogDensity);
```
6. Di `Apply`, ganti baris:
```csharp
            Lamps.Apply(s.LampIntensity01, s.HorrorWeight, elapsed);
```
dengan:
```csharp
            Lamps.Viewer = FindViewer != null ? FindViewer() : null;
            Lamps.Apply(s.LampIntensity01, s.HorrorWeight, elapsed, dt);
```
7. Sebelum method `static void DestroyOwned(UnityEngine.Object obj)`, tambahkan:
```csharp
        static Vector3? MainCameraPosition()
        {
            var cam = Camera.main;
            return cam != null ? cam.transform.position : (Vector3?)null;
        }

```

- [ ] **Step 6: Fixture director tanpa viewer**

Di `Assets/DayCycle/Tests/DirectorFixture.cs`, setelah baris `Director.FindWorldRoot = () => null;` tambahkan:
```csharp
            Director.FindViewer = () => null;
```

- [ ] **Step 7: Jalankan tes, pastikan lulus**

Procedure R+T, `Group = "StreetLampBudgetTests|StreetLampSystemTests|DayCycleDirectorTests|AmbienceAudioTests"`.
Expected: `failed=0`. Tes lama `StreetLampSystemTests` tetap lulus (tanpa jatah, perilaku lama).

- [ ] **Step 8: Regresi DayCycle**

Procedure R+T, `Group = "TycoWorld.DayCycle.Tests"`.
Expected: `failed=0`.

- [ ] **Step 9: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add Assets/DayCycle/Runtime Assets/DayCycle/Tests
git -C "D:/UNITY/project/Tyco World" commit -m "feat(daycycle): lamp light budget, minimum fog density and ambience level knobs

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

---

### Task 7: `QualityApplier` (salinan runtime URP asset) dan renderer tanpa SSAO

**Files:**
- Create: `Assets/GameSettings/Runtime/Quality/QualityApplier.cs`
- Create: `Assets/GameSettings/Editor/NoSsaoRendererBuilder.cs`
- Create (lewat builder): `Assets/Settings/PC_Renderer_NoSSAO.asset`
- Modify (lewat builder): `Assets/Settings/PC_RPAsset.asset` (satu entri tambahan di `m_RendererDataList`)
- Test: `Assets/GameSettings/Tests/QualityApplierTests.cs`, `Assets/GameSettings/Tests/NoSsaoRendererBuilderTests.cs`

**Interfaces:**
- Consumes: `SettingsData`, `ShadowTiers`, `GameSettingsPaths` (Task 1–2).
- Produces:
  - `QualityApplier : IDisposable`:
    - konstruktor merekam `QualitySettings.renderPipeline`, `vSyncCount`, dan `Application.targetFrameRate`, lalu memasang salinan;
    - `RuntimeAsset`, `OriginalPipeline`, `LastRendererIndex`;
    - `Apply(SettingsData, Camera camera, IEnumerable<Light> directionalLights)` (kamera/lampu boleh null);
    - `Dispose()` (aman dipanggil dua kali);
    - konstanta `NoSsaoRendererName = "PC_Renderer_NoSSAO"` dan `MissingPipelineWarning`.
  - `NoSsaoRendererBuilder.Ensure() → int` (indeks renderer tanpa SSAO di `PC_RPAsset`).

Catatan API (dicek di source URP 17.6):
- Setter `supportsSoftShadows` dan `supportsMainLightShadows` bersifat internal. Karena itu bayangan keras/halus/mati diatur lewat `Light.shadows` matahari dan bulan (objek scene, kembali sendiri setelah Play).
- `upscalingFilter` ditandai obsolete hanya kalau `ENABLE_UPSCALER_FRAMEWORK` aktif (tidak aktif di proyek ini). Pemakaiannya dibungkus `#pragma warning disable CS0618`.

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/GameSettings/Tests/QualityApplierTests.cs`:
```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TycoWorld.GameSettings.Tests
{
    public class QualityApplierTests
    {
        readonly List<Object> created = new List<Object>();
        RenderPipelineAsset pipelineBefore;
        int vSyncBefore, frameRateBefore;
        QualityApplier applier;

        [SetUp]
        public void SetUp()
        {
            pipelineBefore = QualitySettings.renderPipeline;
            vSyncBefore = QualitySettings.vSyncCount;
            frameRateBefore = Application.targetFrameRate;
            applier = new QualityApplier();
        }

        [TearDown]
        public void TearDown()
        {
            applier.Dispose();
            if (QualitySettings.renderPipeline != pipelineBefore) QualitySettings.renderPipeline = pipelineBefore;
            if (QualitySettings.vSyncCount != vSyncBefore) QualitySettings.vSyncCount = vSyncBefore;
            Application.targetFrameRate = frameRateBefore;
            foreach (var obj in created) if (obj != null) Object.DestroyImmediate(obj);
            created.Clear();
        }

        T Track<T>(T obj) where T : Object
        {
            created.Add(obj);
            return obj;
        }

        Light DirectionalLight(string name)
        {
            var light = Track(new GameObject(name)).AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            return light;
        }

        static SettingsData Preset(QualityPreset preset)
        {
            var s = new SettingsData();
            QualityPresets.Apply(preset, s);
            return s;
        }

        [Test]
        public void Constructor_SwapsInARuntimeCopy()
        {
            Assert.IsNotNull(applier.RuntimeAsset);
            Assert.AreSame(applier.RuntimeAsset, QualitySettings.renderPipeline);
            Assert.AreSame(pipelineBefore, applier.OriginalPipeline);
            Assert.AreNotSame(pipelineBefore, applier.RuntimeAsset);
            Assert.AreNotEqual(0, (int)(applier.RuntimeAsset.hideFlags & HideFlags.DontSave));
        }

        [Test]
        public void Apply_WritesTheCopyOnly()
        {
            var original = (UniversalRenderPipelineAsset)pipelineBefore;
            float shadowBefore = original.shadowDistance;
            float scaleBefore = original.renderScale;
            int msaaBefore = original.msaaSampleCount;
            bool occlusionBefore = original.gpuResidentDrawerEnableOcclusionCullingInCameras;

            applier.Apply(Preset(QualityPreset.High), null, null);

            var copy = applier.RuntimeAsset;
            Assert.AreEqual(80f, copy.shadowDistance);
            Assert.AreEqual(4, copy.shadowCascadeCount);
            Assert.AreEqual(2048, copy.mainLightShadowmapResolution);
            Assert.AreEqual(1f, copy.renderScale);
            Assert.AreEqual(2, copy.msaaSampleCount);
            Assert.IsTrue(copy.gpuResidentDrawerEnableOcclusionCullingInCameras);
            Assert.AreEqual(0f, copy.smallMeshScreenPercentage);
            Assert.AreEqual(shadowBefore, original.shadowDistance);
            Assert.AreEqual(scaleBefore, original.renderScale);
            Assert.AreEqual(msaaBefore, original.msaaSampleCount);
            Assert.AreEqual(occlusionBefore, original.gpuResidentDrawerEnableOcclusionCullingInCameras);
            Assert.IsFalse(EditorUtility.IsDirty(original), "PC_RPAsset tidak boleh berubah");
        }

        [Test]
        public void Apply_RenderScaleBelow100_UsesStpWithoutMsaa()
        {
            var s = Preset(QualityPreset.High);
            s.renderScale = 70;
            applier.Apply(s, null, null);
            Assert.AreEqual(0.7f, applier.RuntimeAsset.renderScale, 1e-4f);
#pragma warning disable CS0618
            Assert.AreEqual(UpscalingFilterSelection.STP, applier.RuntimeAsset.upscalingFilter);
#pragma warning restore CS0618
            Assert.AreEqual(1, applier.RuntimeAsset.msaaSampleCount);
        }

        [TestCase(ShadowQuality.Off, LightShadows.None)]
        [TestCase(ShadowQuality.Low, LightShadows.Hard)]
        [TestCase(ShadowQuality.Medium, LightShadows.Soft)]
        [TestCase(ShadowQuality.High, LightShadows.Soft)]
        public void Apply_SetsSunAndMoonShadowType(ShadowQuality quality, LightShadows expected)
        {
            var sun = DirectionalLight("Sun");
            var moon = DirectionalLight("Moon");
            applier.Apply(new SettingsData { shadows = quality }, null, new[] { sun, moon, null });
            Assert.AreEqual(expected, sun.shadows);
            Assert.AreEqual(expected, moon.shadows);
        }

        [Test]
        public void Apply_ShadowsOff_KeepsAShadowDistanceForTheFlashlight()
        {
            applier.Apply(new SettingsData { shadows = ShadowQuality.Off }, null, null);
            Assert.Greater(applier.RuntimeAsset.shadowDistance, 0f);
        }

        [Test]
        public void Apply_Ssao_SwitchesTheCameraRenderer()
        {
            var cam = Track(new GameObject("Cam")).AddComponent<Camera>();
            var s = new SettingsData { ssao = false };
            applier.Apply(s, cam, null);
            Assert.Greater(applier.LastRendererIndex, 0, "PC_Renderer_NoSSAO harus terdaftar di PC_RPAsset");
            s.ssao = true;
            applier.Apply(s, cam, null);
            Assert.AreEqual(-1, applier.LastRendererIndex);
        }

        [Test]
        public void Apply_FpsLimitAndVSync()
        {
            applier.Apply(new SettingsData { fpsLimit = 30, vSync = true }, null, null);
            Assert.AreEqual(30, Application.targetFrameRate);
            Assert.AreEqual(1, QualitySettings.vSyncCount);
            applier.Apply(new SettingsData { fpsLimit = 0, vSync = false }, null, null);
            Assert.AreEqual(-1, Application.targetFrameRate);
            Assert.AreEqual(0, QualitySettings.vSyncCount);
        }

        [Test]
        public void Dispose_RestoresPipelineVSyncAndFrameRate()
        {
            applier.Apply(new SettingsData { fpsLimit = 30, vSync = true }, null, null);
            var copy = applier.RuntimeAsset;
            applier.Dispose();
            Assert.AreSame(pipelineBefore, QualitySettings.renderPipeline);
            Assert.AreEqual(vSyncBefore, QualitySettings.vSyncCount);
            Assert.AreEqual(frameRateBefore, Application.targetFrameRate);
            Assert.IsTrue(copy == null, "salinan runtime dihancurkan");
            Assert.DoesNotThrow(() => applier.Dispose());
        }

        [Test]
        public void Apply_AfterDispose_DoesNothing()
        {
            applier.Dispose();
            applier.Apply(new SettingsData { fpsLimit = 30 }, null, null);
            Assert.AreEqual(frameRateBefore, Application.targetFrameRate);
        }
    }
}
```

`Assets/GameSettings/Tests/NoSsaoRendererBuilderTests.cs`:
```csharp
using NUnit.Framework;
using TycoWorld.GameSettings.Authoring;
using UnityEditor;
using UnityEngine.Rendering.Universal;

namespace TycoWorld.GameSettings.Tests
{
    public class NoSsaoRendererBuilderTests
    {
        [Test]
        public void Ensure_RegistersOneFeaturelessCopyOfThePcRenderer()
        {
            int first = NoSsaoRendererBuilder.Ensure();
            int second = NoSsaoRendererBuilder.Ensure();
            Assert.AreEqual(first, second);
            Assert.Greater(first, 0);
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(GameSettingsPaths.PipelineAsset);
            var source = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(GameSettingsPaths.RendererAsset);
            var copy = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(GameSettingsPaths.NoSsaoRendererAsset);
            Assert.IsNotNull(copy);
            Assert.AreEqual(QualityApplier.NoSsaoRendererName, copy.name);
            Assert.AreEqual(0, copy.rendererFeatures.Count);
            Assert.AreEqual(source.renderingMode, copy.renderingMode);
            int matches = 0;
            foreach (var data in pipeline.rendererDataList)
                if (data == copy) matches++;
            Assert.AreEqual(1, matches);
            Assert.AreSame(source, pipeline.rendererDataList[0], "renderer default tetap PC_Renderer");
        }
    }
}
```

- [ ] **Step 2: Jalankan tes, pastikan gagal**

Procedure R+T, `Group = "QualityApplierTests|NoSsaoRendererBuilderTests"`.
Expected: `COMPILE FAILED` (`QualityApplier` belum ada).

- [ ] **Step 3: Implementasi `QualityApplier`**

`Assets/GameSettings/Runtime/Quality/QualityApplier.cs`:
```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TycoWorld.GameSettings
{
    /// <summary>
    /// Applies graphics settings to a runtime copy of the active URP asset (the asset file is never modified),
    /// switches the camera to the SSAO-free renderer, sets the sun/moon shadow type, the FPS limit and VSync, and puts
    /// every global it touched back on Dispose (spec #4 §5.4).
    /// </summary>
    public sealed class QualityApplier : IDisposable
    {
        public const string NoSsaoRendererName = "PC_Renderer_NoSSAO";
        public const string MissingPipelineWarning = "[Settings] URP asset aktif tidak ditemukan; pengaturan grafis dilewati.";

        readonly RenderPipelineAsset originalPipeline;
        readonly int originalVSync;
        readonly int originalFrameRate;
        UniversalRenderPipelineAsset copy;
        bool disposed;

        public QualityApplier()
        {
            originalPipeline = QualitySettings.renderPipeline;
            originalVSync = QualitySettings.vSyncCount;
            originalFrameRate = Application.targetFrameRate;
            var source = originalPipeline as UniversalRenderPipelineAsset;
            if (source == null) source = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (source == null)
            {
                Debug.LogWarning(MissingPipelineWarning);
                return;
            }
            copy = UnityEngine.Object.Instantiate(source);
            copy.name = source.name + " (Runtime)";
            copy.hideFlags = HideFlags.DontSave;
            QualitySettings.renderPipeline = copy;
        }

        public UniversalRenderPipelineAsset RuntimeAsset => copy;
        public RenderPipelineAsset OriginalPipeline => originalPipeline;
        /// <summary>The renderer index last given to the camera (-1 = the pipeline's default renderer).</summary>
        public int LastRendererIndex { get; private set; } = -1;

        public void Apply(SettingsData s, Camera camera, IEnumerable<Light> directionalLights)
        {
            if (disposed) return;
            QualitySettings.vSyncCount = s.vSync ? 1 : 0;
            Application.targetFrameRate = s.fpsLimit > 0 ? s.fpsLimit : -1;
            var tier = ShadowTiers.For(s.shadows);
            if (directionalLights != null)
            {
                var type = !tier.Directional ? LightShadows.None : tier.Soft ? LightShadows.Soft : LightShadows.Hard;
                foreach (var light in directionalLights)
                    if (light != null) light.shadows = type;
            }
            if (copy == null) return;
            copy.shadowDistance = tier.Distance;
            copy.shadowCascadeCount = tier.Cascades;
            copy.mainLightShadowmapResolution = tier.Resolution;
            bool upscaled = s.renderScale < 100;
            copy.renderScale = s.renderScale / 100f;
#pragma warning disable CS0618 // obsolete only when the upscaler framework is enabled, which this project does not use
            copy.upscalingFilter = upscaled ? UpscalingFilterSelection.STP : UpscalingFilterSelection.Auto;
#pragma warning restore CS0618
            copy.msaaSampleCount = upscaled ? 1 : s.msaa;
            copy.gpuResidentDrawerEnableOcclusionCullingInCameras = s.gpuOcclusion;
            copy.smallMeshScreenPercentage = s.smallMeshCulling;
            if (camera != null)
            {
                int index = s.ssao ? -1 : FindRenderer(NoSsaoRendererName);
                camera.GetUniversalAdditionalCameraData().SetRenderer(index);
                LastRendererIndex = index;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (copy != null)
            {
                if (QualitySettings.renderPipeline == copy) QualitySettings.renderPipeline = originalPipeline;
                if (Application.isPlaying) UnityEngine.Object.Destroy(copy);
                else UnityEngine.Object.DestroyImmediate(copy);
                copy = null;
            }
            QualitySettings.vSyncCount = originalVSync;
            Application.targetFrameRate = originalFrameRate;
        }

        int FindRenderer(string rendererName)
        {
            var list = copy.rendererDataList;
            for (int i = 0; i < list.Length; i++)
                if (list[i] != null && list[i].name == rendererName) return i;
            return -1;
        }
    }
}
```

- [ ] **Step 4: Implementasi `NoSsaoRendererBuilder`**

`Assets/GameSettings/Editor/NoSsaoRendererBuilder.cs`:
```csharp
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TycoWorld.GameSettings.Authoring
{
    /// <summary>Creates PC_Renderer_NoSSAO (PC_Renderer without renderer features) and registers it on PC_RPAsset once (spec #4 §5.4).</summary>
    public static class NoSsaoRendererBuilder
    {
        public const string MissingAssetsError = "[Settings] PC_RPAsset atau PC_Renderer tidak ditemukan di Assets/Settings.";

        /// <summary>The SSAO-free renderer's index in PC_RPAsset's renderer list; creates and registers it when missing.</summary>
        public static int Ensure()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(GameSettingsPaths.PipelineAsset);
            var source = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(GameSettingsPaths.RendererAsset);
            if (pipeline == null || source == null)
            {
                Debug.LogError(MissingAssetsError);
                return -1;
            }
            var copy = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(GameSettingsPaths.NoSsaoRendererAsset);
            if (copy == null)
            {
                copy = ScriptableObject.CreateInstance<UniversalRendererData>();
                EditorUtility.CopySerialized(source, copy);
                var serialized = new SerializedObject(copy);
                serialized.FindProperty("m_RendererFeatures").ClearArray();
                serialized.FindProperty("m_RendererFeatureMap").ClearArray();
                serialized.ApplyModifiedPropertiesWithoutUndo();
                copy.name = QualityApplier.NoSsaoRendererName;
                AssetDatabase.CreateAsset(copy, GameSettingsPaths.NoSsaoRendererAsset);
            }
            var pipelineObject = new SerializedObject(pipeline);
            var list = pipelineObject.FindProperty("m_RendererDataList");
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == copy) return i;
            int index = list.arraySize;
            list.arraySize = index + 1;
            list.GetArrayElementAtIndex(index).objectReferenceValue = copy;
            pipelineObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();
            return index;
        }
    }
}
```

- [ ] **Step 5: Bangun renderer tanpa SSAO sekali (MCP)**

```csharp
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        if (EditorApplication.isCompiling) { result.LogError("STILL COMPILING after refresh"); return; }
        result.Log("noSsaoIndex=" + TycoWorld.GameSettings.Authoring.NoSsaoRendererBuilder.Ensure());
    }
}
```
Expected: `noSsaoIndex=1`.

Lalu cek diff:
```bash
git -C "D:/UNITY/project/Tyco World" diff --stat -- Assets/Settings
git -C "D:/UNITY/project/Tyco World" status --short -- Assets/Settings
```
Expected: `PC_RPAsset.asset` hanya bertambah satu baris `- {fileID: …, guid: …}` di `m_RendererDataList`, dan `PC_Renderer_NoSSAO.asset` (+ `.meta`) baru. Kalau ada perubahan lain di `PC_RPAsset.asset` akibat serialisasi ulang, periksa: nilai seperti `m_ShadowDistance` atau `m_RenderScale` **tidak boleh** berubah.

- [ ] **Step 6: Jalankan tes, pastikan lulus**

Procedure R+T, `Group = "QualityApplierTests|NoSsaoRendererBuilderTests"`.
Expected: `passed=13 failed=0` (Applier 12 + Builder 1).

- [ ] **Step 7: Pastikan tes tidak meninggalkan perubahan**

```bash
git -C "D:/UNITY/project/Tyco World" status --short
```
Expected: hanya file Task 7 dan `ProjectSettings/ProjectSettings.asset` (milik user). Kalau `ProjectSettings/QualitySettings.asset` muncul, berarti tes gagal memulihkan pipeline: perbaiki `TearDown`/`Dispose`, lalu kembalikan file itu dengan `git checkout -- ProjectSettings/QualitySettings.asset` (file ini bersih sebelum task).

- [ ] **Step 8: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add Assets/GameSettings Assets/Settings/PC_RPAsset.asset Assets/Settings/PC_Renderer_NoSSAO.asset Assets/Settings/PC_Renderer_NoSSAO.asset.meta
git -C "D:/UNITY/project/Tyco World" commit -m "feat(settings): runtime URP asset copy for quality settings and an SSAO-free renderer

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

---

### Task 8: `SettingsText`, `ResolutionOptions`, dan `SettingsPresenter`

**Files:**
- Create: `Assets/GameSettings/Runtime/UI/SettingsText.cs`
- Create: `Assets/GameSettings/Runtime/UI/ResolutionOptions.cs`
- Create: `Assets/GameSettings/Runtime/UI/SettingsPresenter.cs`
- Test: `Assets/GameSettings/Tests/SettingsTextTests.cs`, `ResolutionOptionsTests.cs`, `SettingsPresenterTests.cs`

**Interfaces:**
- Consumes: `SettingsData`, `QualityPresets`, `SettingsValidator` (Task 2).
- Produces:
  - `SettingsText`:
    - konstanta teks menu/tab/label (lihat kode);
    - `PresetName`, `ShadowName`, `Meters`, `Percent`, `Multiplier`, `Degrees`, `LampLights`, `FpsLimit`, `ScreenMode`, `Resolution(Vector2Int)`, `Fps(float)`;
    - `PresetChoices()`, `ShadowChoices()`, `LampLightChoices()`, `FpsLimitChoices()`, `ScreenModeChoices()` → `List<string>` (urutannya sama dengan enum/array nilai).
  - `ResolutionOptions.Distinct(IEnumerable<Vector2Int>) → List<Vector2Int>`, `Labels(IReadOnlyList<Vector2Int>) → List<string>`, `IndexOf(sizes, w, h) → int`, `At(sizes, index) → Vector2Int`.
  - `SettingsPresenter(SettingsData initial)` dengan:
    - `Working` (salinan milik presenter);
    - `event Action<SettingsData> Changed` (menerima salinan);
    - `Load(SettingsData)` (tanpa event), `SelectPreset(QualityPreset)`, `Edit(Action<SettingsData>)`, `ResetDefaults()`.

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/GameSettings/Tests/SettingsTextTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.GameSettings.Tests
{
    public class SettingsTextTests
    {
        [Test]
        public void Formats_Values()
        {
            Assert.AreEqual("300 m", SettingsText.Meters(300));
            Assert.AreEqual("85 %", SettingsText.Percent(85));
            Assert.AreEqual("1,5×", SettingsText.Multiplier(1.5f));
            Assert.AreEqual("1,0×", SettingsText.Multiplier(1f));
            Assert.AreEqual("70°", SettingsText.Degrees(70));
            Assert.AreEqual("16", SettingsText.LampLights(16));
            Assert.AreEqual("60 FPS", SettingsText.FpsLimit(60));
            Assert.AreEqual("Tanpa batas", SettingsText.FpsLimit(0));
            Assert.AreEqual("1920 × 1080", SettingsText.Resolution(new Vector2Int(1920, 1080)));
            Assert.AreEqual("Saat ini", SettingsText.Resolution(Vector2Int.zero));
            Assert.AreEqual("60 FPS", SettingsText.Fps(59.6f));
        }

        [Test]
        public void Names_AreIndonesian()
        {
            Assert.AreEqual("Rendah", SettingsText.PresetName(QualityPreset.Low));
            Assert.AreEqual("Sedang", SettingsText.PresetName(QualityPreset.Medium));
            Assert.AreEqual("Tinggi", SettingsText.PresetName(QualityPreset.High));
            Assert.AreEqual("Kustom", SettingsText.PresetName(QualityPreset.Custom));
            Assert.AreEqual("Mati", SettingsText.ShadowName(ShadowQuality.Off));
            Assert.AreEqual("Tinggi", SettingsText.ShadowName(ShadowQuality.High));
            Assert.AreEqual("Layar penuh", SettingsText.ScreenMode(true));
            Assert.AreEqual("Jendela", SettingsText.ScreenMode(false));
        }

        [Test]
        public void Choices_FollowTheValueOrder()
        {
            CollectionAssert.AreEqual(new[] { "Rendah", "Sedang", "Tinggi", "Kustom" }, SettingsText.PresetChoices());
            CollectionAssert.AreEqual(new[] { "Mati", "Rendah", "Sedang", "Tinggi" }, SettingsText.ShadowChoices());
            CollectionAssert.AreEqual(new[] { "8", "16", "32" }, SettingsText.LampLightChoices());
            CollectionAssert.AreEqual(new[] { "30 FPS", "60 FPS", "120 FPS", "Tanpa batas" }, SettingsText.FpsLimitChoices());
            CollectionAssert.AreEqual(new[] { "Layar penuh", "Jendela" }, SettingsText.ScreenModeChoices());
        }

        [Test]
        public void MenuTexts_MatchTheSpec()
        {
            Assert.AreEqual("Jeda", SettingsText.PauseTitle);
            Assert.AreEqual("Lanjutkan", SettingsText.Resume);
            Assert.AreEqual("Pengaturan", SettingsText.OpenSettings);
            Assert.AreEqual("Keluar", SettingsText.Quit);
            Assert.AreEqual("Keluar dari game?", SettingsText.QuitQuestion);
            Assert.AreEqual("Kembalikan default", SettingsText.ResetDefaults);
        }
    }
}
```

`Assets/GameSettings/Tests/ResolutionOptionsTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.GameSettings.Tests
{
    public class ResolutionOptionsTests
    {
        static Vector2Int R(int w, int h) => new Vector2Int(w, h);

        [Test]
        public void Distinct_DropsDuplicatesAndZeros_LargestFirst()
        {
            var sizes = ResolutionOptions.Distinct(new[] { R(1280, 720), R(1920, 1080), R(1280, 720), R(0, 0), R(1920, 1080), R(1600, 900) });
            CollectionAssert.AreEqual(new[] { R(1920, 1080), R(1600, 900), R(1280, 720) }, sizes);
        }

        [Test]
        public void Labels_StartWithCurrent()
        {
            CollectionAssert.AreEqual(new[] { "Saat ini", "1920 × 1080", "1280 × 720" },
                ResolutionOptions.Labels(new[] { R(1920, 1080), R(1280, 720) }));
        }

        [Test]
        public void IndexOfAndAt_RoundTrip()
        {
            var sizes = new[] { R(1920, 1080), R(1600, 900) };
            Assert.AreEqual(2, ResolutionOptions.IndexOf(sizes, 1600, 900));
            Assert.AreEqual(R(1600, 900), ResolutionOptions.At(sizes, 2));
            Assert.AreEqual(0, ResolutionOptions.IndexOf(sizes, 0, 0));
            Assert.AreEqual(0, ResolutionOptions.IndexOf(sizes, 800, 600));
            Assert.AreEqual(Vector2Int.zero, ResolutionOptions.At(sizes, 0));
            Assert.AreEqual(Vector2Int.zero, ResolutionOptions.At(sizes, 9));
        }
    }
}
```

`Assets/GameSettings/Tests/SettingsPresenterTests.cs`:
```csharp
using NUnit.Framework;

namespace TycoWorld.GameSettings.Tests
{
    public class SettingsPresenterTests
    {
        SettingsPresenter presenter;

        [SetUp]
        public void SetUp() => presenter = new SettingsPresenter(new SettingsData());

        [Test]
        public void SelectPreset_FillsGraphicsAndNotifiesACopy()
        {
            SettingsData received = null;
            int calls = 0;
            presenter.Changed += s =>
            {
                received = s;
                calls++;
            };
            presenter.SelectPreset(QualityPreset.High);
            Assert.AreEqual(1, calls);
            Assert.AreEqual(QualityPreset.High, received.preset);
            Assert.AreEqual(500, received.viewDistance);
            received.viewDistance = 100;
            Assert.AreEqual(500, presenter.Working.viewDistance, "pendengar menerima salinan");
        }

        [Test]
        public void Edit_GraphicsValue_SwitchesToCustom()
        {
            presenter.Edit(s => s.viewDistance = 450);
            Assert.AreEqual(450, presenter.Working.viewDistance);
            Assert.AreEqual(QualityPreset.Custom, presenter.Working.preset);
        }

        [Test]
        public void Edit_BackToPresetValues_DetectsThePreset()
        {
            presenter.Edit(s => s.viewDistance = 450);
            presenter.Edit(s => s.viewDistance = 300);
            Assert.AreEqual(QualityPreset.Medium, presenter.Working.preset);
        }

        [Test]
        public void Edit_SnapsAndClamps()
        {
            presenter.Edit(s => s.viewDistance = 333);
            Assert.AreEqual(350, presenter.Working.viewDistance);
            presenter.Edit(s => s.fieldOfView = 200);
            Assert.AreEqual(100, presenter.Working.fieldOfView);
        }

        [Test]
        public void Edit_NonGraphics_KeepsThePreset()
        {
            presenter.SelectPreset(QualityPreset.High);
            presenter.Edit(s => s.fieldOfView = 90);
            Assert.AreEqual(QualityPreset.High, presenter.Working.preset);
            Assert.AreEqual(90, presenter.Working.fieldOfView);
        }

        [Test]
        public void SelectCustom_KeepsTheValues()
        {
            presenter.SelectPreset(QualityPreset.High);
            presenter.SelectPreset(QualityPreset.Custom);
            Assert.AreEqual(500, presenter.Working.viewDistance);
            Assert.AreEqual(QualityPreset.High, presenter.Working.preset, "Kustom hanya tampil kalau nilainya memang tidak cocok preset");
        }

        [Test]
        public void ResetDefaults_RestoresMedium()
        {
            presenter.SelectPreset(QualityPreset.High);
            presenter.Edit(s => s.masterVolume = 10);
            presenter.ResetDefaults();
            Assert.AreEqual(QualityPreset.Medium, presenter.Working.preset);
            Assert.AreEqual(300, presenter.Working.viewDistance);
            Assert.AreEqual(100, presenter.Working.masterVolume);
        }

        [Test]
        public void Load_ReplacesWorkingWithoutNotifying()
        {
            int calls = 0;
            presenter.Changed += _ => calls++;
            var loaded = new SettingsData { viewDistance = 500 };
            presenter.Load(loaded);
            Assert.AreEqual(500, presenter.Working.viewDistance);
            Assert.AreEqual(0, calls);
            loaded.viewDistance = 100;
            Assert.AreEqual(500, presenter.Working.viewDistance);
        }
    }
}
```

- [ ] **Step 2: Jalankan tes, pastikan gagal**

Procedure R+T, `Group = "SettingsTextTests|ResolutionOptionsTests|SettingsPresenterTests"`.
Expected: `COMPILE FAILED`.

- [ ] **Step 3: Implementasi**

`Assets/GameSettings/Runtime/UI/SettingsText.cs`:
```csharp
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace TycoWorld.GameSettings
{
    /// <summary>Every player-facing string of the pause and settings menus (spec #4 §4/§6) and how values are written.</summary>
    public static class SettingsText
    {
        public const string PauseTitle = "Jeda";
        public const string Resume = "Lanjutkan";
        public const string OpenSettings = "Pengaturan";
        public const string Quit = "Keluar";
        public const string QuitQuestion = "Keluar dari game?";
        public const string QuitYes = "Ya, keluar";
        public const string QuitNo = "Batal";
        public const string SettingsTitle = "Pengaturan";
        public const string Back = "Kembali";
        public const string ResetDefaults = "Kembalikan default";

        public const string TabGraphics = "Grafis";
        public const string TabControls = "Kontrol";
        public const string TabAudio = "Audio";
        public const string TabDisplay = "Tampilan";

        public const string PresetLabel = "Preset";
        public const string ViewDistanceLabel = "View distance";
        public const string RenderScaleLabel = "Render scale";
        public const string ShadowsLabel = "Bayangan";
        public const string SsaoLabel = "SSAO";
        public const string GpuOcclusionLabel = "GPU occlusion culling";
        public const string LampLightsLabel = "Lampu jalan menyala";
        public const string FpsLimitLabel = "Batas FPS";
        public const string VSyncLabel = "VSync";
        public const string ScreenModeLabel = "Mode layar";
        public const string ResolutionLabel = "Resolusi";
        public const string SensitivityLabel = "Sensitivitas mouse";
        public const string InvertYLabel = "Invert Y";
        public const string FovLabel = "FOV";
        public const string MasterLabel = "Master";
        public const string EffectsLabel = "Efek";
        public const string AmbienceLabel = "Ambience";
        public const string ShowFpsLabel = "Penghitung FPS";
        public const string ShowClockLabel = "Jam";
        public const string CurrentResolution = "Saat ini";

        public static string PresetName(QualityPreset preset)
        {
            switch (preset)
            {
                case QualityPreset.Low: return "Rendah";
                case QualityPreset.Medium: return "Sedang";
                case QualityPreset.High: return "Tinggi";
                default: return "Kustom";
            }
        }

        public static string ShadowName(ShadowQuality quality)
        {
            switch (quality)
            {
                case ShadowQuality.Off: return "Mati";
                case ShadowQuality.Low: return "Rendah";
                case ShadowQuality.High: return "Tinggi";
                default: return "Sedang";
            }
        }

        public static string Meters(int meters) => meters.ToString(CultureInfo.InvariantCulture) + " m";

        public static string Percent(int percent) => percent.ToString(CultureInfo.InvariantCulture) + " %";

        /// <summary>Indonesian decimal comma: 1.5 → "1,5×".</summary>
        public static string Multiplier(float value) => value.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',') + "×";

        public static string Degrees(int degrees) => degrees.ToString(CultureInfo.InvariantCulture) + "°";

        public static string LampLights(int lamps) => lamps.ToString(CultureInfo.InvariantCulture);

        public static string FpsLimit(int fps) => fps <= 0 ? "Tanpa batas" : fps.ToString(CultureInfo.InvariantCulture) + " FPS";

        public static string ScreenMode(bool fullscreen) => fullscreen ? "Layar penuh" : "Jendela";

        public static string Resolution(Vector2Int size) =>
            size.x <= 0 || size.y <= 0
                ? CurrentResolution
                : size.x.ToString(CultureInfo.InvariantCulture) + " × " + size.y.ToString(CultureInfo.InvariantCulture);

        public static string Fps(float fps) => Mathf.RoundToInt(fps).ToString(CultureInfo.InvariantCulture) + " FPS";

        public static List<string> PresetChoices() => new List<string>
        {
            PresetName(QualityPreset.Low), PresetName(QualityPreset.Medium), PresetName(QualityPreset.High), PresetName(QualityPreset.Custom)
        };

        public static List<string> ShadowChoices() => new List<string>
        {
            ShadowName(ShadowQuality.Off), ShadowName(ShadowQuality.Low), ShadowName(ShadowQuality.Medium), ShadowName(ShadowQuality.High)
        };

        public static List<string> LampLightChoices()
        {
            var list = new List<string>();
            foreach (int lamps in SettingsValidator.LampLightChoices()) list.Add(LampLights(lamps));
            return list;
        }

        public static List<string> FpsLimitChoices()
        {
            var list = new List<string>();
            foreach (int fps in SettingsValidator.FpsLimitChoices()) list.Add(FpsLimit(fps));
            return list;
        }

        public static List<string> ScreenModeChoices() => new List<string> { ScreenMode(true), ScreenMode(false) };
    }
}
```

`Assets/GameSettings/Runtime/UI/ResolutionOptions.cs`:
```csharp
using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.GameSettings
{
    /// <summary>The resolution dropdown: "Saat ini" (keep the current size) followed by distinct sizes, largest first.</summary>
    public static class ResolutionOptions
    {
        public static List<Vector2Int> Distinct(IEnumerable<Vector2Int> sizes)
        {
            var result = new List<Vector2Int>();
            if (sizes == null) return result;
            foreach (var size in sizes)
                if (size.x > 0 && size.y > 0 && !result.Contains(size)) result.Add(size);
            result.Sort((a, b) => a.x != b.x ? b.x.CompareTo(a.x) : b.y.CompareTo(a.y));
            return result;
        }

        public static List<string> Labels(IReadOnlyList<Vector2Int> sizes)
        {
            var labels = new List<string> { SettingsText.CurrentResolution };
            if (sizes != null)
                foreach (var size in sizes) labels.Add(SettingsText.Resolution(size));
            return labels;
        }

        /// <summary>Dropdown index of a stored resolution: 0 ("Saat ini") for 0 × 0 or a size not in the list.</summary>
        public static int IndexOf(IReadOnlyList<Vector2Int> sizes, int width, int height)
        {
            for (int i = 0; i < sizes.Count; i++)
                if (sizes[i].x == width && sizes[i].y == height) return i + 1;
            return 0;
        }

        /// <summary>The size a dropdown index stands for; index 0 or out of range → 0 × 0 (keep current).</summary>
        public static Vector2Int At(IReadOnlyList<Vector2Int> sizes, int index) =>
            index >= 1 && index <= sizes.Count ? sizes[index - 1] : Vector2Int.zero;
    }
}
```

`Assets/GameSettings/Runtime/UI/SettingsPresenter.cs`:
```csharp
using System;

namespace TycoWorld.GameSettings
{
    /// <summary>
    /// The settings page's working copy: every edit is validated, the preset is re-detected (any changed graphics value
    /// makes it Custom), and listeners receive a copy so they cannot change the page behind its back (spec #4 §4).
    /// </summary>
    public sealed class SettingsPresenter
    {
        SettingsData working;

        public SettingsPresenter(SettingsData initial) => working = initial != null ? initial.Clone() : new SettingsData();

        public event Action<SettingsData> Changed;

        public SettingsData Working => working;

        /// <summary>Shows other settings (the applied ones when the page opens) without raising Changed.</summary>
        public void Load(SettingsData settings) => working = settings != null ? settings.Clone() : new SettingsData();

        public void SelectPreset(QualityPreset preset)
        {
            if (preset != QualityPreset.Custom) QualityPresets.Apply(preset, working);
            working.preset = QualityPresets.Detect(working);
            Notify();
        }

        public void Edit(Action<SettingsData> edit)
        {
            edit(working);
            SettingsValidator.Sanitize(working);
            Notify();
        }

        public void ResetDefaults()
        {
            working = new SettingsData();
            Notify();
        }

        void Notify() => Changed?.Invoke(working.Clone());
    }
}
```

- [ ] **Step 4: Jalankan tes, pastikan lulus**

Procedure R+T, `Group = "SettingsTextTests|ResolutionOptionsTests|SettingsPresenterTests"`.
Expected: `passed=15 failed=0` (4 + 3 + 8).

- [ ] **Step 5: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add Assets/GameSettings
git -C "D:/UNITY/project/Tyco World" commit -m "feat(settings): menu texts, resolution options and settings presenter

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

---

### Task 9: View UI Toolkit (`PauseMenuView`, `SettingsView`, `FpsCounterView`)

**Files:**
- Create: `Assets/GameSettings/Runtime/UI/MenuPage.cs`
- Create: `Assets/GameSettings/Runtime/UI/MenuStyle.cs`
- Create: `Assets/GameSettings/Runtime/UI/PauseMenuView.cs`
- Create: `Assets/GameSettings/Runtime/UI/SettingsView.cs`
- Create: `Assets/GameSettings/Runtime/UI/FpsCounterView.cs`
- Test: `Assets/GameSettings/Tests/PauseMenuViewTests.cs`, `SettingsViewTests.cs`, `FpsCounterViewTests.cs`

**Interfaces:**
- Consumes: `SettingsText`, `SettingsPresenter`, `ResolutionOptions` (Task 8), `ViewDistanceRules`, `SettingsValidator` (Task 2).
- Produces:
  - `enum MenuPage { Closed, Main, Settings, ConfirmQuit }` dan `enum SettingsTab { Graphics, Controls, Audio, Display }`.
  - `PauseMenuView(VisualElement root)`:
    - konstanta nama elemen;
    - event `ResumeClicked`, `SettingsClicked`, `QuitClicked`, `QuitConfirmed`, `QuitCancelled`;
    - `SettingsHost`, `Overlay`, `Page`, `Show(MenuPage)`.
  - `SettingsView(VisualElement host, SettingsPresenter, IReadOnlyList<Vector2Int> resolutions)`:
    - konstanta nama kontrol dan `ValueSuffix = "-value"`;
    - `TabName(SettingsTab)`, `PageName(SettingsTab)`;
    - `event BackClicked`, `Root`, `Tab`, `ShowTab(SettingsTab)`, `Refresh()`.
  - `FpsCounterView(VisualElement root)` dengan `LabelName = "fps"` dan `Render(bool visible, float fps)`.
  - `MenuStyle`: warna dan helper `FullScreen`, `Panel`, `Title`, `MakeButton`, `Row`, `Field<T>`.

Catatan:
- Di EditMode, `ChangeEvent` hanya terkirim kalau elemen menempel di panel. Karena itu tes view memeriksa struktur dan `Refresh` (presenter → kontrol). Jalur kontrol → presenter sudah dites lewat presenter (Task 8) dan diuji klik nyata di Task 13/14.
- Helper pembuat kontrol diberi awalan `Make…`. Nama method yang sama dengan nama tipe (`Button`, `Toggle`) di dalam kelas yang sama membuat `new Button(...)` gagal dengan error CS0118.

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/GameSettings/Tests/PauseMenuViewTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine.UIElements;

namespace TycoWorld.GameSettings.Tests
{
    public class PauseMenuViewTests
    {
        VisualElement root;
        PauseMenuView view;

        [SetUp]
        public void SetUp()
        {
            root = new VisualElement();
            view = new PauseMenuView(root);
        }

        static DisplayStyle Display(VisualElement e) => e.style.display.value;

        [Test]
        public void Builds_NamedElementsWithIndonesianText()
        {
            Assert.AreEqual("Lanjutkan", root.Q<Button>(PauseMenuView.ResumeName).text);
            Assert.AreEqual("Pengaturan", root.Q<Button>(PauseMenuView.SettingsName).text);
            Assert.AreEqual("Keluar", root.Q<Button>(PauseMenuView.QuitName).text);
            Assert.AreEqual("Ya, keluar", root.Q<Button>(PauseMenuView.QuitYesName).text);
            Assert.AreEqual("Batal", root.Q<Button>(PauseMenuView.QuitNoName).text);
            Assert.IsNotNull(root.Q(PauseMenuView.SettingsHostName));
            Assert.AreSame(root.Q(PauseMenuView.SettingsHostName), view.SettingsHost);
        }

        [Test]
        public void StartsClosed()
        {
            Assert.AreEqual(MenuPage.Closed, view.Page);
            Assert.AreEqual(DisplayStyle.None, Display(root.Q(PauseMenuView.OverlayName)));
        }

        [TestCase(MenuPage.Main, PauseMenuView.PanelName)]
        [TestCase(MenuPage.Settings, PauseMenuView.SettingsHostName)]
        [TestCase(MenuPage.ConfirmQuit, PauseMenuView.ConfirmName)]
        public void Show_DisplaysExactlyOneSection(MenuPage page, string visible)
        {
            view.Show(page);
            Assert.AreEqual(DisplayStyle.Flex, Display(root.Q(PauseMenuView.OverlayName)));
            foreach (var name in new[] { PauseMenuView.PanelName, PauseMenuView.SettingsHostName, PauseMenuView.ConfirmName })
                Assert.AreEqual(name == visible ? DisplayStyle.Flex : DisplayStyle.None, Display(root.Q(name)), name);
        }
    }
}
```

`Assets/GameSettings/Tests/SettingsViewTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace TycoWorld.GameSettings.Tests
{
    public class SettingsViewTests
    {
        SettingsPresenter presenter;
        VisualElement host;
        SettingsView view;

        [SetUp]
        public void SetUp()
        {
            presenter = new SettingsPresenter(new SettingsData());
            host = new VisualElement();
            view = new SettingsView(host, presenter, new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) });
        }

        T Q<T>(string name) where T : VisualElement
        {
            var element = host.Q<T>(name);
            Assert.IsNotNull(element, name);
            return element;
        }

        string ValueText(string name) => Q<Label>(name + SettingsView.ValueSuffix).text;

        [Test]
        public void Builds_EveryNamedControl()
        {
            foreach (var name in new[]
                     {
                         SettingsView.PresetName, SettingsView.ViewDistanceName, SettingsView.RenderScaleName, SettingsView.ShadowsName,
                         SettingsView.SsaoName, SettingsView.GpuOcclusionName, SettingsView.LampLightsName, SettingsView.FpsLimitName,
                         SettingsView.VSyncName, SettingsView.ScreenModeName, SettingsView.ResolutionName, SettingsView.SensitivityName,
                         SettingsView.InvertYName, SettingsView.FovName, SettingsView.MasterName, SettingsView.EffectsName,
                         SettingsView.AmbienceName, SettingsView.ShowFpsName, SettingsView.ShowClockName, SettingsView.ResetName,
                         SettingsView.BackName
                     })
                Assert.IsNotNull(host.Q(name), name);
            foreach (SettingsTab tab in System.Enum.GetValues(typeof(SettingsTab)))
            {
                Assert.IsNotNull(host.Q(SettingsView.TabName(tab)), SettingsView.TabName(tab));
                Assert.IsNotNull(host.Q(SettingsView.PageName(tab)), SettingsView.PageName(tab));
            }
        }

        [Test]
        public void Refresh_ShowsTheDefaults()
        {
            Assert.AreEqual("Sedang", Q<DropdownField>(SettingsView.PresetName).value);
            Assert.AreEqual(300, Q<SliderInt>(SettingsView.ViewDistanceName).value);
            Assert.AreEqual("300 m", ValueText(SettingsView.ViewDistanceName));
            Assert.AreEqual("85 %", ValueText(SettingsView.RenderScaleName));
            Assert.AreEqual("Sedang", Q<DropdownField>(SettingsView.ShadowsName).value);
            Assert.AreEqual("16", Q<DropdownField>(SettingsView.LampLightsName).value);
            Assert.AreEqual("60 FPS", Q<DropdownField>(SettingsView.FpsLimitName).value);
            Assert.AreEqual("Layar penuh", Q<DropdownField>(SettingsView.ScreenModeName).value);
            Assert.AreEqual("Saat ini", Q<DropdownField>(SettingsView.ResolutionName).value);
            Assert.AreEqual("1,0×", ValueText(SettingsView.SensitivityName));
            Assert.AreEqual("70°", ValueText(SettingsView.FovName));
            Assert.AreEqual("100 %", ValueText(SettingsView.MasterName));
            Assert.IsTrue(Q<Toggle>(SettingsView.GpuOcclusionName).value);
            Assert.IsFalse(Q<Toggle>(SettingsView.ShowFpsName).value);
        }

        [Test]
        public void PresenterChanges_AreShown()
        {
            presenter.SelectPreset(QualityPreset.High);
            Assert.AreEqual("Tinggi", Q<DropdownField>(SettingsView.PresetName).value);
            Assert.AreEqual(500, Q<SliderInt>(SettingsView.ViewDistanceName).value);
            Assert.AreEqual("500 m", ValueText(SettingsView.ViewDistanceName));
            Assert.AreEqual("Tinggi", Q<DropdownField>(SettingsView.ShadowsName).value);
            Assert.IsTrue(Q<Toggle>(SettingsView.SsaoName).value);
            Assert.AreEqual("32", Q<DropdownField>(SettingsView.LampLightsName).value);
        }

        [Test]
        public void CustomValues_ShowKustom()
        {
            presenter.Edit(s => s.viewDistance = 450);
            Assert.AreEqual("Kustom", Q<DropdownField>(SettingsView.PresetName).value);
        }

        [Test]
        public void Resolution_ChoicesStartWithCurrent()
        {
            CollectionAssert.AreEqual(new[] { "Saat ini", "1920 × 1080", "1280 × 720" }, Q<DropdownField>(SettingsView.ResolutionName).choices);
            presenter.Edit(s =>
            {
                s.resolutionWidth = 1280;
                s.resolutionHeight = 720;
            });
            Assert.AreEqual("1280 × 720", Q<DropdownField>(SettingsView.ResolutionName).value);
        }

        [Test]
        public void StartsOnGraphics_ShowTabShowsOnePage()
        {
            Assert.AreEqual(SettingsTab.Graphics, view.Tab);
            view.ShowTab(SettingsTab.Audio);
            Assert.AreEqual(SettingsTab.Audio, view.Tab);
            foreach (SettingsTab tab in System.Enum.GetValues(typeof(SettingsTab)))
                Assert.AreEqual(tab == SettingsTab.Audio ? DisplayStyle.Flex : DisplayStyle.None,
                    host.Q(SettingsView.PageName(tab)).style.display.value, tab.ToString());
        }
    }
}
```

`Assets/GameSettings/Tests/FpsCounterViewTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine.UIElements;

namespace TycoWorld.GameSettings.Tests
{
    public class FpsCounterViewTests
    {
        [Test]
        public void Render_ShowsRoundedFps()
        {
            var root = new VisualElement();
            var view = new FpsCounterView(root);
            view.Render(true, 59.6f);
            var label = root.Q<Label>(FpsCounterView.LabelName);
            Assert.AreEqual("60 FPS", label.text);
            Assert.AreEqual(DisplayStyle.Flex, label.style.display.value);
        }

        [Test]
        public void Render_Hidden()
        {
            var root = new VisualElement();
            var view = new FpsCounterView(root);
            view.Render(false, 60f);
            Assert.AreEqual(DisplayStyle.None, root.Q<Label>(FpsCounterView.LabelName).style.display.value);
        }
    }
}
```

- [ ] **Step 2: Jalankan tes, pastikan gagal**

Procedure R+T, `Group = "PauseMenuViewTests|SettingsViewTests|FpsCounterViewTests"`.
Expected: `COMPILE FAILED`.

- [ ] **Step 3: Implementasi**

`Assets/GameSettings/Runtime/UI/MenuPage.cs`:
```csharp
namespace TycoWorld.GameSettings
{
    public enum MenuPage
    {
        Closed,
        Main,
        Settings,
        ConfirmQuit
    }
}
```

`Assets/GameSettings/Runtime/UI/MenuStyle.cs`:
```csharp
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TycoWorld.GameSettings
{
    /// <summary>Inline styles shared by the code-built menus (no USS, like the HUD).</summary>
    public static class MenuStyle
    {
        public static readonly Color TextColor = new Color(0.92f, 0.92f, 0.88f, 1f);
        public static readonly Color DimColor = new Color(0f, 0f, 0f, 0.6f);
        public static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.11f, 0.96f);
        public static readonly Color ButtonColor = new Color(0.18f, 0.19f, 0.22f, 1f);
        public static readonly Color SelectedColor = new Color(0.55f, 0.42f, 0.16f, 1f);

        public static void FullScreen(VisualElement e)
        {
            e.style.position = Position.Absolute;
            e.style.left = 0f;
            e.style.right = 0f;
            e.style.top = 0f;
            e.style.bottom = 0f;
        }

        public static VisualElement Panel(string name, float width)
        {
            var panel = new VisualElement { name = name };
            panel.style.width = width;
            panel.style.backgroundColor = PanelColor;
            Round(panel, 8f);
            Pad(panel, 24f);
            return panel;
        }

        public static Label Title(string text)
        {
            var label = new Label(text);
            label.style.fontSize = 30f;
            label.style.color = TextColor;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.marginBottom = 16f;
            return label;
        }

        public static Button MakeButton(string name, string text, Action onClick)
        {
            var button = new Button(onClick) { name = name, text = text };
            button.style.height = 44f;
            button.style.fontSize = 20f;
            button.style.marginTop = 6f;
            button.style.marginBottom = 6f;
            button.style.color = TextColor;
            button.style.backgroundColor = ButtonColor;
            Round(button, 6f);
            return button;
        }

        public static VisualElement Row()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.Center;
            return row;
        }

        /// <summary>Spacing and a readable, aligned label for a settings field.</summary>
        public static void Field<T>(BaseField<T> field)
        {
            field.style.marginTop = 4f;
            field.style.marginBottom = 4f;
            field.labelElement.style.color = TextColor;
            field.labelElement.style.minWidth = 260f;
            field.labelElement.style.fontSize = 18f;
        }

        public static void Round(VisualElement e, float radius)
        {
            e.style.borderTopLeftRadius = radius;
            e.style.borderTopRightRadius = radius;
            e.style.borderBottomLeftRadius = radius;
            e.style.borderBottomRightRadius = radius;
        }

        public static void Pad(VisualElement e, float padding)
        {
            e.style.paddingLeft = padding;
            e.style.paddingRight = padding;
            e.style.paddingTop = padding;
            e.style.paddingBottom = padding;
        }
    }
}
```

`Assets/GameSettings/Runtime/UI/PauseMenuView.cs`:
```csharp
using System;
using UnityEngine.UIElements;

namespace TycoWorld.GameSettings
{
    /// <summary>Code-built pause overlay: main panel (Lanjutkan / Pengaturan / Keluar), quit confirmation and the settings host (spec #4 §6).</summary>
    public sealed class PauseMenuView
    {
        public const string OverlayName = "pause-overlay";
        public const string PanelName = "pause-panel";
        public const string ResumeName = "resume";
        public const string SettingsName = "open-settings";
        public const string QuitName = "quit";
        public const string ConfirmName = "quit-confirm";
        public const string QuitYesName = "quit-yes";
        public const string QuitNoName = "quit-no";
        public const string SettingsHostName = "settings-host";

        readonly VisualElement overlay, panel, confirm;

        public event Action ResumeClicked;
        public event Action SettingsClicked;
        public event Action QuitClicked;
        public event Action QuitConfirmed;
        public event Action QuitCancelled;

        public PauseMenuView(VisualElement root)
        {
            overlay = new VisualElement { name = OverlayName };
            MenuStyle.FullScreen(overlay);
            overlay.style.backgroundColor = MenuStyle.DimColor;
            overlay.style.alignItems = Align.Center;
            overlay.style.justifyContent = Justify.Center;

            panel = MenuStyle.Panel(PanelName, 420f);
            panel.Add(MenuStyle.Title(SettingsText.PauseTitle));
            panel.Add(MenuStyle.MakeButton(ResumeName, SettingsText.Resume, () => ResumeClicked?.Invoke()));
            panel.Add(MenuStyle.MakeButton(SettingsName, SettingsText.OpenSettings, () => SettingsClicked?.Invoke()));
            panel.Add(MenuStyle.MakeButton(QuitName, SettingsText.Quit, () => QuitClicked?.Invoke()));

            confirm = MenuStyle.Panel(ConfirmName, 420f);
            confirm.Add(MenuStyle.Title(SettingsText.QuitQuestion));
            var row = MenuStyle.Row();
            var yes = MenuStyle.MakeButton(QuitYesName, SettingsText.QuitYes, () => QuitConfirmed?.Invoke());
            var no = MenuStyle.MakeButton(QuitNoName, SettingsText.QuitNo, () => QuitCancelled?.Invoke());
            yes.style.flexGrow = 1f;
            yes.style.marginRight = 8f;
            no.style.flexGrow = 1f;
            row.Add(yes);
            row.Add(no);
            confirm.Add(row);

            SettingsHost = new VisualElement { name = SettingsHostName };

            overlay.Add(panel);
            overlay.Add(confirm);
            overlay.Add(SettingsHost);
            root.Add(overlay);
            Show(MenuPage.Closed);
        }

        public VisualElement Overlay => overlay;
        public VisualElement SettingsHost { get; }
        public MenuPage Page { get; private set; }

        public void Show(MenuPage page)
        {
            Page = page;
            overlay.style.display = page == MenuPage.Closed ? DisplayStyle.None : DisplayStyle.Flex;
            panel.style.display = page == MenuPage.Main ? DisplayStyle.Flex : DisplayStyle.None;
            confirm.style.display = page == MenuPage.ConfirmQuit ? DisplayStyle.Flex : DisplayStyle.None;
            SettingsHost.style.display = page == MenuPage.Settings ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
```

`Assets/GameSettings/Runtime/UI/FpsCounterView.cs`:
```csharp
using UnityEngine;
using UnityEngine.UIElements;

namespace TycoWorld.GameSettings
{
    /// <summary>Small FPS label in the top-left corner (Tampilan → Penghitung FPS).</summary>
    public sealed class FpsCounterView
    {
        public const string LabelName = "fps";

        readonly Label label;

        public FpsCounterView(VisualElement root)
        {
            label = new Label { name = LabelName, pickingMode = PickingMode.Ignore };
            label.style.position = Position.Absolute;
            label.style.left = 16f;
            label.style.top = 12f;
            label.style.fontSize = 18f;
            label.style.color = MenuStyle.TextColor;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.display = DisplayStyle.None;
            root.Add(label);
        }

        public void Render(bool visible, float fps)
        {
            label.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (visible) label.text = SettingsText.Fps(fps);
        }
    }
}
```

`Assets/GameSettings/Runtime/UI/SettingsView.cs`:
```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TycoWorld.GameSettings
{
    public enum SettingsTab
    {
        Graphics,
        Controls,
        Audio,
        Display
    }

    /// <summary>
    /// Code-built settings page with four tabs (spec #4 §4/§6). Every control writes through the SettingsPresenter;
    /// Refresh reads the presenter's values back into the controls without raising change events.
    /// </summary>
    public sealed class SettingsView
    {
        public const string RootName = "settings-panel";
        public const string PresetName = "preset";
        public const string ViewDistanceName = "view-distance";
        public const string RenderScaleName = "render-scale";
        public const string ShadowsName = "shadows";
        public const string SsaoName = "ssao";
        public const string GpuOcclusionName = "gpu-occlusion";
        public const string LampLightsName = "lamp-lights";
        public const string FpsLimitName = "fps-limit";
        public const string VSyncName = "vsync";
        public const string ScreenModeName = "screen-mode";
        public const string ResolutionName = "resolution";
        public const string SensitivityName = "mouse-sensitivity";
        public const string InvertYName = "invert-y";
        public const string FovName = "fov";
        public const string MasterName = "master-volume";
        public const string EffectsName = "effects-volume";
        public const string AmbienceName = "ambience-volume";
        public const string ShowFpsName = "show-fps";
        public const string ShowClockName = "show-clock";
        public const string ResetName = "reset-defaults";
        public const string BackName = "back";
        public const string ValueSuffix = "-value";

        readonly SettingsPresenter presenter;
        readonly List<Vector2Int> resolutions = new List<Vector2Int>();
        readonly Dictionary<SettingsTab, VisualElement> pages = new Dictionary<SettingsTab, VisualElement>();
        readonly Dictionary<SettingsTab, Button> tabs = new Dictionary<SettingsTab, Button>();
        readonly Dictionary<string, Label> values = new Dictionary<string, Label>();
        readonly DropdownField preset, shadows, lampLights, fpsLimit, screenMode, resolution;
        readonly SliderInt viewDistance, renderScale, fieldOfView, masterVolume, effectsVolume, ambienceVolume;
        readonly Slider sensitivity;
        readonly Toggle ssao, gpuOcclusion, vSync, invertY, showFps, showClock;

        public event Action BackClicked;

        public SettingsView(VisualElement host, SettingsPresenter settingsPresenter, IReadOnlyList<Vector2Int> resolutionOptions)
        {
            presenter = settingsPresenter;
            if (resolutionOptions != null) resolutions.AddRange(resolutionOptions);

            Root = MenuStyle.Panel(RootName, 760f);
            Root.Add(MenuStyle.Title(SettingsText.SettingsTitle));
            var tabRow = MenuStyle.Row();
            tabRow.style.marginBottom = 12f;
            Root.Add(tabRow);
            foreach (SettingsTab tab in Enum.GetValues(typeof(SettingsTab)))
            {
                var shown = tab;
                var button = MenuStyle.MakeButton(TabName(tab), TabTitle(tab), () => ShowTab(shown));
                button.style.flexGrow = 1f;
                tabRow.Add(button);
                tabs[tab] = button;
                var page = new VisualElement { name = PageName(tab) };
                Root.Add(page);
                pages[tab] = page;
            }

            var g = pages[SettingsTab.Graphics];
            preset = MakeDropdown(g, PresetName, SettingsText.PresetLabel, SettingsText.PresetChoices(), i => presenter.SelectPreset((QualityPreset)i));
            viewDistance = MakeIntSlider(g, ViewDistanceName, SettingsText.ViewDistanceLabel, ViewDistanceRules.Min, ViewDistanceRules.Max,
                v => presenter.Edit(s => s.viewDistance = v));
            renderScale = MakeIntSlider(g, RenderScaleName, SettingsText.RenderScaleLabel, SettingsValidator.RenderScaleMin,
                SettingsValidator.RenderScaleMax, v => presenter.Edit(s => s.renderScale = v));
            shadows = MakeDropdown(g, ShadowsName, SettingsText.ShadowsLabel, SettingsText.ShadowChoices(),
                i => presenter.Edit(s => s.shadows = (ShadowQuality)i));
            ssao = MakeToggle(g, SsaoName, SettingsText.SsaoLabel, v => presenter.Edit(s => s.ssao = v));
            gpuOcclusion = MakeToggle(g, GpuOcclusionName, SettingsText.GpuOcclusionLabel, v => presenter.Edit(s => s.gpuOcclusion = v));
            lampLights = MakeDropdown(g, LampLightsName, SettingsText.LampLightsLabel, SettingsText.LampLightChoices(),
                i => presenter.Edit(s => s.lampLights = SettingsValidator.LampLightChoices()[i]));
            fpsLimit = MakeDropdown(g, FpsLimitName, SettingsText.FpsLimitLabel, SettingsText.FpsLimitChoices(),
                i => presenter.Edit(s => s.fpsLimit = SettingsValidator.FpsLimitChoices()[i]));
            vSync = MakeToggle(g, VSyncName, SettingsText.VSyncLabel, v => presenter.Edit(s => s.vSync = v));
            screenMode = MakeDropdown(g, ScreenModeName, SettingsText.ScreenModeLabel, SettingsText.ScreenModeChoices(),
                i => presenter.Edit(s => s.fullscreen = i == 0));
            resolution = MakeDropdown(g, ResolutionName, SettingsText.ResolutionLabel, ResolutionOptions.Labels(resolutions),
                i => presenter.Edit(s => SetResolution(s, i)));

            var c = pages[SettingsTab.Controls];
            sensitivity = MakeFloatSlider(c, SensitivityName, SettingsText.SensitivityLabel, SettingsValidator.SensitivityMin,
                SettingsValidator.SensitivityMax, v => presenter.Edit(s => s.mouseSensitivity = Mathf.Round(v * 10f) / 10f));
            invertY = MakeToggle(c, InvertYName, SettingsText.InvertYLabel, v => presenter.Edit(s => s.invertY = v));
            fieldOfView = MakeIntSlider(c, FovName, SettingsText.FovLabel, SettingsValidator.FovMin, SettingsValidator.FovMax,
                v => presenter.Edit(s => s.fieldOfView = v));

            var a = pages[SettingsTab.Audio];
            masterVolume = MakeIntSlider(a, MasterName, SettingsText.MasterLabel, 0, 100, v => presenter.Edit(s => s.masterVolume = v));
            effectsVolume = MakeIntSlider(a, EffectsName, SettingsText.EffectsLabel, 0, 100, v => presenter.Edit(s => s.effectsVolume = v));
            ambienceVolume = MakeIntSlider(a, AmbienceName, SettingsText.AmbienceLabel, 0, 100, v => presenter.Edit(s => s.ambienceVolume = v));

            var d = pages[SettingsTab.Display];
            showFps = MakeToggle(d, ShowFpsName, SettingsText.ShowFpsLabel, v => presenter.Edit(s => s.showFps = v));
            showClock = MakeToggle(d, ShowClockName, SettingsText.ShowClockLabel, v => presenter.Edit(s => s.showClock = v));

            var footer = MenuStyle.Row();
            footer.style.marginTop = 16f;
            var reset = MenuStyle.MakeButton(ResetName, SettingsText.ResetDefaults, () => presenter.ResetDefaults());
            var back = MenuStyle.MakeButton(BackName, SettingsText.Back, () => BackClicked?.Invoke());
            reset.style.flexGrow = 1f;
            reset.style.marginRight = 8f;
            back.style.flexGrow = 1f;
            footer.Add(reset);
            footer.Add(back);
            Root.Add(footer);

            host.Add(Root);
            presenter.Changed += _ => Refresh();
            Refresh();
            ShowTab(SettingsTab.Graphics);
        }

        public VisualElement Root { get; }
        public SettingsTab Tab { get; private set; }

        public static string TabName(SettingsTab tab) => "tab-" + Key(tab);

        public static string PageName(SettingsTab tab) => "page-" + Key(tab);

        public void ShowTab(SettingsTab tab)
        {
            Tab = tab;
            foreach (var pair in pages) pair.Value.style.display = pair.Key == tab ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (var pair in tabs) pair.Value.style.backgroundColor = pair.Key == tab ? MenuStyle.SelectedColor : MenuStyle.ButtonColor;
        }

        public void Refresh()
        {
            var s = presenter.Working;
            preset.SetValueWithoutNotify(SettingsText.PresetName(s.preset));
            SetSlider(viewDistance, ViewDistanceName, s.viewDistance, SettingsText.Meters(s.viewDistance));
            SetSlider(renderScale, RenderScaleName, s.renderScale, SettingsText.Percent(s.renderScale));
            shadows.SetValueWithoutNotify(SettingsText.ShadowName(s.shadows));
            ssao.SetValueWithoutNotify(s.ssao);
            gpuOcclusion.SetValueWithoutNotify(s.gpuOcclusion);
            lampLights.SetValueWithoutNotify(SettingsText.LampLights(s.lampLights));
            fpsLimit.SetValueWithoutNotify(SettingsText.FpsLimit(s.fpsLimit));
            vSync.SetValueWithoutNotify(s.vSync);
            screenMode.SetValueWithoutNotify(SettingsText.ScreenMode(s.fullscreen));
            resolution.SetValueWithoutNotify(resolution.choices[ResolutionOptions.IndexOf(resolutions, s.resolutionWidth, s.resolutionHeight)]);
            sensitivity.SetValueWithoutNotify(s.mouseSensitivity);
            values[SensitivityName].text = SettingsText.Multiplier(s.mouseSensitivity);
            invertY.SetValueWithoutNotify(s.invertY);
            SetSlider(fieldOfView, FovName, s.fieldOfView, SettingsText.Degrees(s.fieldOfView));
            SetSlider(masterVolume, MasterName, s.masterVolume, SettingsText.Percent(s.masterVolume));
            SetSlider(effectsVolume, EffectsName, s.effectsVolume, SettingsText.Percent(s.effectsVolume));
            SetSlider(ambienceVolume, AmbienceName, s.ambienceVolume, SettingsText.Percent(s.ambienceVolume));
            showFps.SetValueWithoutNotify(s.showFps);
            showClock.SetValueWithoutNotify(s.showClock);
        }

        void SetResolution(SettingsData s, int index)
        {
            var size = ResolutionOptions.At(resolutions, index);
            s.resolutionWidth = size.x;
            s.resolutionHeight = size.y;
        }

        void SetSlider(SliderInt slider, string name, int value, string text)
        {
            slider.SetValueWithoutNotify(value);
            values[name].text = text;
        }

        DropdownField MakeDropdown(VisualElement parent, string name, string label, List<string> choices, Action<int> onIndex)
        {
            var field = new DropdownField(label, choices, 0) { name = name };
            field.RegisterValueChangedCallback(e =>
            {
                int index = field.choices.IndexOf(e.newValue);
                if (index >= 0) onIndex(index);
            });
            MenuStyle.Field(field);
            parent.Add(field);
            return field;
        }

        Toggle MakeToggle(VisualElement parent, string name, string label, Action<bool> onChange)
        {
            var field = new Toggle(label) { name = name };
            field.RegisterValueChangedCallback(e => onChange(e.newValue));
            MenuStyle.Field(field);
            parent.Add(field);
            return field;
        }

        SliderInt MakeIntSlider(VisualElement parent, string name, string label, int low, int high, Action<int> onChange)
        {
            var field = new SliderInt(label, low, high) { name = name };
            field.RegisterValueChangedCallback(e => onChange(e.newValue));
            MenuStyle.Field(field);
            AddWithValue(parent, field, name);
            return field;
        }

        Slider MakeFloatSlider(VisualElement parent, string name, string label, float low, float high, Action<float> onChange)
        {
            var field = new Slider(label, low, high) { name = name };
            field.RegisterValueChangedCallback(e => onChange(e.newValue));
            MenuStyle.Field(field);
            AddWithValue(parent, field, name);
            return field;
        }

        void AddWithValue(VisualElement parent, VisualElement field, string name)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            field.style.flexGrow = 1f;
            row.Add(field);
            var value = new Label { name = name + ValueSuffix };
            value.style.width = 90f;
            value.style.unityTextAlign = TextAnchor.MiddleRight;
            value.style.color = MenuStyle.TextColor;
            value.style.fontSize = 18f;
            row.Add(value);
            values[name] = value;
            parent.Add(row);
        }

        static string Key(SettingsTab tab)
        {
            switch (tab)
            {
                case SettingsTab.Controls: return "controls";
                case SettingsTab.Audio: return "audio";
                case SettingsTab.Display: return "display";
                default: return "graphics";
            }
        }

        static string TabTitle(SettingsTab tab)
        {
            switch (tab)
            {
                case SettingsTab.Controls: return SettingsText.TabControls;
                case SettingsTab.Audio: return SettingsText.TabAudio;
                case SettingsTab.Display: return SettingsText.TabDisplay;
                default: return SettingsText.TabGraphics;
            }
        }
    }
}
```

- [ ] **Step 4: Jalankan tes, pastikan lulus**

Procedure R+T, `Group = "PauseMenuViewTests|SettingsViewTests|FpsCounterViewTests"`.
Expected: `passed=13 failed=0` (PauseMenu 5 + Settings 6 + Fps 2).

- [ ] **Step 5: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add Assets/GameSettings
git -C "D:/UNITY/project/Tyco World" commit -m "feat(settings): code-built pause menu, settings page and FPS counter views

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

---

### Task 10: `MenuNavigator`, `PauseController`, dan fixture tes `GameFixture`

**Files:**
- Create: `Assets/GameSettings/Runtime/Menu/MenuNavigator.cs`
- Create: `Assets/GameSettings/Runtime/Menu/IPauseEffects.cs`
- Create: `Assets/GameSettings/Runtime/Menu/UnityPauseEffects.cs`
- Create: `Assets/GameSettings/Runtime/Menu/PauseController.cs`
- Create: `Assets/GameSettings/Tests/GameFixture.cs`
- Create: `Assets/GameSettings/Tests/FakePauseEffects.cs`
- Test: `Assets/GameSettings/Tests/MenuNavigatorTests.cs`, `PauseControllerTests.cs`

**Interfaces:**
- Consumes: `MenuPage` (Task 9), `FirstPersonPlayer.Locks/LockCursor/UnlockCursor/EscapeUnlocksCursor` (Task 5).
- Produces:
  - `MenuNavigator` dengan `Page`, `event Action<MenuPage, MenuPage> PageChanged` (from, to), `Escape()`, dan `Go(MenuPage)`.
  - `interface IPauseEffects { void SetPaused(bool paused); }` dan `UnityPauseEffects`. Yang terakhir mengubah `Time.timeScale` dan `AudioListener.pause`, lalu mengembalikan skala waktu sebelumnya.
  - `PauseController(IPauseEffects)` dengan `IsPaused`, `Attach(FirstPersonPlayer)`, `Pause()`, `Resume()`, dan `Shutdown()`.
  - Tes:
    - `GameFixture` dengan `Track<T>`, `CreatePlayer()` (player dengan Camera asli), `CreateDirector(FirstPersonPlayer)` (jam 12:00, matahari/bulan/ambience sendiri), serta pemulihan `AudioListener.volume` dan `RenderSettings`;
    - `FakePauseEffects` (`Paused`, `Calls`).

- [ ] **Step 1: Tulis fixture dan tes yang gagal**

`Assets/GameSettings/Tests/FakePauseEffects.cs`:
```csharp
namespace TycoWorld.GameSettings.Tests
{
    /// <summary>Records pause side effects instead of touching Time.timeScale (EditMode tests must never change it).</summary>
    internal sealed class FakePauseEffects : IPauseEffects
    {
        public bool Paused;
        public int Calls;

        public void SetPaused(bool paused)
        {
            Paused = paused;
            Calls++;
        }
    }
}
```

`Assets/GameSettings/Tests/GameFixture.cs`:
```csharp
using System;
using System.Collections.Generic;
using TycoWorld.DayCycle;
using TycoWorld.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace TycoWorld.GameSettings.Tests
{
    /// <summary>Code-built player, day-cycle director and scratch objects for settings tests; restores the globals they touch.</summary>
    internal sealed class GameFixture : IDisposable
    {
        readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
        readonly List<DayCycleDirector> directors = new List<DayCycleDirector>();
        readonly float listenerVolume = AudioListener.volume;
        readonly bool fog = RenderSettings.fog;
        readonly FogMode fogMode = RenderSettings.fogMode;
        readonly float fogDensity = RenderSettings.fogDensity;
        readonly Color fogColor = RenderSettings.fogColor;
        readonly AmbientMode ambientMode = RenderSettings.ambientMode;
        readonly Color ambientSky = RenderSettings.ambientSkyColor;
        readonly Color ambientEquator = RenderSettings.ambientEquatorColor;
        readonly Color ambientGround = RenderSettings.ambientGroundColor;
        readonly Material skybox = RenderSettings.skybox;
        readonly Light sun = RenderSettings.sun;

        public T Track<T>(T obj) where T : UnityEngine.Object
        {
            created.Add(obj);
            return obj;
        }

        /// <summary>An initialized first-person player far outside the world, with a real Camera on its view.</summary>
        public FirstPersonPlayer CreatePlayer()
        {
            var tuning = Track(ScriptableObject.CreateInstance<PlayerTuning>());
            var go = Track(new GameObject("Player"));
            go.transform.position = new Vector3(5000f, 1000f, 5000f);
            var pivot = new GameObject("CameraPivot").transform;
            pivot.SetParent(go.transform, false);
            var view = new GameObject("Camera");
            view.transform.SetParent(pivot, false);
            view.AddComponent<Camera>();
            var player = go.AddComponent<FirstPersonPlayer>();
            player.Configure(tuning, pivot, view.transform);
            player.Initialize();
            return player;
        }

        /// <summary>An initialized director at 12:00 with its own sun, moon and ambience (no volumes, no skybox, no world).</summary>
        public DayCycleDirector CreateDirector(FirstPersonPlayer player)
        {
            var profile = Track(DayCycleProfile.CreateDefault());
            var sunLight = NewLight("Sun");
            var moonLight = NewLight("Moon");
            var root = Track(new GameObject("DayCycle"));
            var ambience = root.AddComponent<AmbienceAudio>();
            var director = root.AddComponent<DayCycleDirector>();
            director.Configure(profile, sunLight, moonLight, null, null, null, null, ambience, player);
            director.FindWorldRoot = () => null;
            director.FindViewer = () => null;
            director.Initialize(12f);
            directors.Add(director);
            return director;
        }

        Light NewLight(string name)
        {
            var light = Track(new GameObject(name)).AddComponent<Light>();
            light.type = LightType.Directional;
            return light;
        }

        public void Dispose()
        {
            foreach (var director in directors)
                if (director != null) director.Shutdown();
            directors.Clear();
            for (int i = created.Count - 1; i >= 0; i--)
                if (created[i] != null) UnityEngine.Object.DestroyImmediate(created[i]);
            created.Clear();
            AudioListener.volume = listenerVolume;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;
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

`Assets/GameSettings/Tests/MenuNavigatorTests.cs`:
```csharp
using System.Collections.Generic;
using NUnit.Framework;

namespace TycoWorld.GameSettings.Tests
{
    public class MenuNavigatorTests
    {
        MenuNavigator nav;
        readonly List<string> changes = new List<string>();

        [SetUp]
        public void SetUp()
        {
            nav = new MenuNavigator();
            changes.Clear();
            nav.PageChanged += (from, to) => changes.Add(from + ">" + to);
        }

        [Test]
        public void StartsClosed() => Assert.AreEqual(MenuPage.Closed, nav.Page);

        [Test]
        public void Escape_OpensAndClosesTheMainPage()
        {
            nav.Escape();
            Assert.AreEqual(MenuPage.Main, nav.Page);
            nav.Escape();
            Assert.AreEqual(MenuPage.Closed, nav.Page);
            CollectionAssert.AreEqual(new[] { "Closed>Main", "Main>Closed" }, changes);
        }

        [TestCase(MenuPage.Settings)]
        [TestCase(MenuPage.ConfirmQuit)]
        public void Escape_FromASubPage_ReturnsToMain(MenuPage page)
        {
            nav.Go(MenuPage.Main);
            nav.Go(page);
            nav.Escape();
            Assert.AreEqual(MenuPage.Main, nav.Page);
        }

        [Test]
        public void Go_SamePage_RaisesNothing()
        {
            nav.Go(MenuPage.Closed);
            Assert.AreEqual(0, changes.Count);
        }

        [Test]
        public void Escape_PressedRepeatedly_StaysConsistent()
        {
            for (int i = 0; i < 5; i++) nav.Escape();
            Assert.AreEqual(MenuPage.Main, nav.Page);
            Assert.AreEqual(5, changes.Count);
            Assert.AreEqual("Closed>Main", changes[4]);
        }
    }
}
```

`Assets/GameSettings/Tests/PauseControllerTests.cs`:
```csharp
using NUnit.Framework;
using TycoWorld.Player;

namespace TycoWorld.GameSettings.Tests
{
    public class PauseControllerTests
    {
        GameFixture fx;
        FakePauseEffects effects;
        PauseController pause;
        FirstPersonPlayer player;

        [SetUp]
        public void SetUp()
        {
            fx = new GameFixture();
            effects = new FakePauseEffects();
            pause = new PauseController(effects);
            player = fx.CreatePlayer();
        }

        [TearDown]
        public void TearDown() => fx.Dispose();

        [Test]
        public void Attach_TakesEscapeAwayFromThePlayer()
        {
            pause.Attach(player);
            Assert.IsFalse(player.EscapeUnlocksCursor);
        }

        [Test]
        public void Pause_FreezesTimeAudioAndControls()
        {
            pause.Attach(player);
            pause.Pause();
            Assert.IsTrue(pause.IsPaused);
            Assert.IsTrue(effects.Paused);
            foreach (var ability in new[] { PlayerAbility.Move, PlayerAbility.Look, PlayerAbility.Interact, PlayerAbility.Flashlight })
                Assert.IsFalse(player.Locks.Allows(ability), ability.ToString());
        }

        [Test]
        public void Resume_RestoresEverything()
        {
            pause.Attach(player);
            pause.Pause();
            pause.Resume();
            Assert.IsFalse(pause.IsPaused);
            Assert.IsFalse(effects.Paused);
            Assert.IsTrue(player.Locks.Allows(PlayerAbility.All));
        }

        [Test]
        public void Resume_KeepsOtherOwnersLocks()
        {
            var spawner = new object();
            player.Locks.Acquire(spawner, PlayerAbility.All);
            pause.Attach(player);
            pause.Pause();
            pause.Resume();
            Assert.IsTrue(player.Locks.IsHeldBy(spawner));
            Assert.IsFalse(player.Locks.Allows(PlayerAbility.Move));
        }

        [Test]
        public void PauseAndResume_AreIdempotent()
        {
            pause.Attach(player);
            pause.Pause();
            pause.Pause();
            Assert.AreEqual(1, effects.Calls);
            pause.Resume();
            pause.Resume();
            Assert.AreEqual(2, effects.Calls);
        }

        [Test]
        public void Shutdown_WhilePaused_RestoresTimeAndAudio()
        {
            pause.Attach(player);
            pause.Pause();
            pause.Shutdown();
            Assert.IsFalse(pause.IsPaused);
            Assert.IsFalse(effects.Paused);
            Assert.IsTrue(player.Locks.Allows(PlayerAbility.Move));
        }

        [Test]
        public void WithoutPlayer_StillPausesTime()
        {
            pause.Attach(null);
            pause.Pause();
            Assert.IsTrue(effects.Paused);
            pause.Resume();
            Assert.IsFalse(effects.Paused);
        }
    }
}
```

- [ ] **Step 2: Jalankan tes, pastikan gagal**

Procedure R+T, `Group = "MenuNavigatorTests|PauseControllerTests"`.
Expected: `COMPILE FAILED`.

- [ ] **Step 3: Implementasi**

`Assets/GameSettings/Runtime/Menu/MenuNavigator.cs`:
```csharp
using System;

namespace TycoWorld.GameSettings
{
    /// <summary>
    /// Which menu page is open (spec #4 §6). Esc: closed → main, main → closed, settings/confirm → main.
    /// PageChanged reports (from, to) so the menu can pause, resume and save at the right moments.
    /// </summary>
    public sealed class MenuNavigator
    {
        public event Action<MenuPage, MenuPage> PageChanged;

        public MenuPage Page { get; private set; } = MenuPage.Closed;

        public void Escape()
        {
            switch (Page)
            {
                case MenuPage.Closed:
                    Go(MenuPage.Main);
                    break;
                case MenuPage.Main:
                    Go(MenuPage.Closed);
                    break;
                default:
                    Go(MenuPage.Main);
                    break;
            }
        }

        public void Go(MenuPage page)
        {
            if (page == Page) return;
            var from = Page;
            Page = page;
            PageChanged?.Invoke(from, page);
        }
    }
}
```

`Assets/GameSettings/Runtime/Menu/IPauseEffects.cs`:
```csharp
namespace TycoWorld.GameSettings
{
    /// <summary>The global side effects of pausing; tests use a fake so Time.timeScale is never touched in EditMode.</summary>
    public interface IPauseEffects
    {
        void SetPaused(bool paused);
    }
}
```

`Assets/GameSettings/Runtime/Menu/UnityPauseEffects.cs`:
```csharp
using UnityEngine;

namespace TycoWorld.GameSettings
{
    /// <summary>Freezes Time.timeScale and pauses every AudioSource; resuming restores the previous time scale.</summary>
    public sealed class UnityPauseEffects : IPauseEffects
    {
        float previousScale = 1f;

        public void SetPaused(bool paused)
        {
            if (paused)
            {
                previousScale = Time.timeScale > 0f ? Time.timeScale : 1f;
                Time.timeScale = 0f;
                AudioListener.pause = true;
            }
            else
            {
                Time.timeScale = previousScale;
                AudioListener.pause = false;
            }
        }
    }
}
```

`Assets/GameSettings/Runtime/Menu/PauseController.cs`:
```csharp
using TycoWorld.Player;

namespace TycoWorld.GameSettings
{
    /// <summary>
    /// Pausing (spec #4 §3/§6): freezes game time and audio, locks every player ability under its own owner and frees
    /// the cursor. Resume undoes exactly that, so locks held by others (the spawner while the world loads) stay.
    /// </summary>
    public sealed class PauseController
    {
        readonly IPauseEffects effects;
        readonly object lockOwner = new object();
        FirstPersonPlayer player;

        public PauseController(IPauseEffects effects) => this.effects = effects;

        public bool IsPaused { get; private set; }

        /// <summary>Takes Esc away from the player (it now opens the menu). Null is allowed (scenes without a player).</summary>
        public void Attach(FirstPersonPlayer firstPerson)
        {
            player = firstPerson;
            if (player != null) player.EscapeUnlocksCursor = false;
        }

        public void Pause()
        {
            if (IsPaused) return;
            IsPaused = true;
            effects.SetPaused(true);
            if (player != null && player.IsInitialized)
            {
                player.Locks.Acquire(lockOwner, PlayerAbility.All);
                player.UnlockCursor();
            }
        }

        public void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;
            effects.SetPaused(false);
            if (player != null && player.IsInitialized)
            {
                player.Locks.Release(lockOwner);
                if (player.Locks.Allows(PlayerAbility.Look)) player.LockCursor();
            }
        }

        /// <summary>The menu is going away (Play stopped, scene unloaded): never leave the game frozen.</summary>
        public void Shutdown() => Resume();
    }
}
```

- [ ] **Step 4: Jalankan tes, pastikan lulus**

Procedure R+T, `Group = "MenuNavigatorTests|PauseControllerTests"`.
Expected: `passed=13 failed=0` (Navigator 6 + Pause 7).

- [ ] **Step 5: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add Assets/GameSettings
git -C "D:/UNITY/project/Tyco World" commit -m "feat(settings): menu navigation and pause controller

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

---

### Task 11: `SettingsApplier` dan volume Efek

**Files:**
- Create: `Assets/GameSettings/Runtime/Audio/EffectsVolume.cs`
- Create: `Assets/GameSettings/Runtime/SettingsApplier.cs`
- Test: `Assets/GameSettings/Tests/EffectsVolumeTests.cs`, `Assets/GameSettings/Tests/SettingsApplierTests.cs`

**Interfaces:**
- Consumes:
  - `SettingsStore`, `ISettingsBackend`, `PlayerPrefsBackend` (Task 3);
  - `QualityApplier` (Task 7);
  - `ViewDistanceRules`, `SettingsValidator` (Task 2);
  - `DistanceCuller` (Task 4);
  - `FirstPersonPlayer.LookSensitivity/InvertY/View` (Task 5);
  - `DayCycleDirector.MinFogDensity/ShowClock/Lamps/Ambience/Sun/Moon/IsInitialized`, `StreetLampSystem.LightBudget/MaxLightDistance`, `AmbienceAudio.Level` (Task 6).
- Produces:
  - `EffectsVolume.Apply(IEnumerable<AudioSource>, float level)` dan `Restore()`.
  - `SettingsApplier : MonoBehaviour`:
    - `Current`, `IsInitialized`, `Quality`;
    - `Backend` (null = PlayerPrefs), seam `FindPlayer` dan `FindDirector`;
    - `event Action<SettingsData> Applied`;
    - `Initialize()`, `Apply(SettingsData)`, `ApplyAll()`, `Save()`, `Shutdown()`.

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/GameSettings/Tests/EffectsVolumeTests.cs`:
```csharp
using NUnit.Framework;
using TycoWorld.DayCycle;
using UnityEngine;

namespace TycoWorld.GameSettings.Tests
{
    public class EffectsVolumeTests
    {
        GameFixture fx;
        EffectsVolume effects;

        [SetUp]
        public void SetUp()
        {
            fx = new GameFixture();
            effects = new EffectsVolume();
        }

        [TearDown]
        public void TearDown() => fx.Dispose();

        AudioSource Source(float volume)
        {
            var source = fx.Track(new GameObject("Sfx")).AddComponent<AudioSource>();
            source.volume = volume;
            return source;
        }

        [Test]
        public void Apply_ScalesFromTheOriginalVolume()
        {
            var source = Source(0.8f);
            effects.Apply(new[] { source }, 0.5f);
            Assert.AreEqual(0.4f, source.volume, 1e-5f);
            effects.Apply(new[] { source }, 0.5f);
            Assert.AreEqual(0.4f, source.volume, 1e-5f, "tidak mengecil berulang");
            effects.Apply(new[] { source }, 1f);
            Assert.AreEqual(0.8f, source.volume, 1e-5f);
        }

        [Test]
        public void Apply_SkipsAmbienceLayers()
        {
            var root = fx.Track(new GameObject("Ambience"));
            root.AddComponent<AmbienceAudio>();
            var layer = new GameObject("Ambience_Day");
            layer.transform.SetParent(root.transform, false);
            var source = layer.AddComponent<AudioSource>();
            source.volume = 0.3f;
            effects.Apply(new[] { source }, 0.5f);
            Assert.AreEqual(0.3f, source.volume, 1e-5f);
        }

        [Test]
        public void Restore_PutsOriginalsBack()
        {
            var source = Source(0.8f);
            effects.Apply(new[] { source }, 0.25f);
            effects.Restore();
            Assert.AreEqual(0.8f, source.volume, 1e-5f);
        }

        [Test]
        public void Apply_IgnoresNullAndDestroyedSources()
        {
            var source = Source(0.8f);
            Object.DestroyImmediate(source);
            Assert.DoesNotThrow(() => effects.Apply(new AudioSource[] { null, source }, 0.5f));
            Assert.DoesNotThrow(() => effects.Apply(null, 0.5f));
        }
    }
}
```

`Assets/GameSettings/Tests/SettingsApplierTests.cs`:
```csharp
using NUnit.Framework;
using TycoWorld.DayCycle;
using TycoWorld.Player;
using TycoWorld.WorldGen;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace TycoWorld.GameSettings.Tests
{
    public class SettingsApplierTests
    {
        GameFixture fx;
        FirstPersonPlayer player;
        DayCycleDirector director;
        SettingsApplier applier;
        DistanceCuller culler;
        MemorySettingsBackend backend;
        RenderPipelineAsset pipelineBefore;
        int frameRateBefore, vSyncBefore;

        [SetUp]
        public void SetUp()
        {
            pipelineBefore = QualitySettings.renderPipeline;
            frameRateBefore = Application.targetFrameRate;
            vSyncBefore = QualitySettings.vSyncCount;
            fx = new GameFixture();
            player = fx.CreatePlayer();
            director = fx.CreateDirector(player);
            var systems = fx.Track(new GameObject("GameSystems"));
            culler = systems.AddComponent<DistanceCuller>();
            culler.FindWorldRoot = () => null;
            culler.FindViewer = () => null;
            applier = systems.AddComponent<SettingsApplier>();
            backend = new MemorySettingsBackend();
            applier.Backend = backend;
            applier.FindPlayer = () => player;
            applier.FindDirector = () => director;
        }

        [TearDown]
        public void TearDown()
        {
            applier.Shutdown();
            fx.Dispose();
            if (QualitySettings.renderPipeline != pipelineBefore) QualitySettings.renderPipeline = pipelineBefore;
            if (QualitySettings.vSyncCount != vSyncBefore) QualitySettings.vSyncCount = vSyncBefore;
            Application.targetFrameRate = frameRateBefore;
        }

        Camera Cam => player.View.GetComponent<Camera>();

        [Test]
        public void Initialize_LoadsTheStoredSettings()
        {
            backend.Json = "{\"version\":1,\"viewDistance\":500}";
            applier.Initialize();
            Assert.AreEqual(500, applier.Current.viewDistance);
        }

        [Test]
        public void Apply_DrivesCameraPlayerCullerAndDirector()
        {
            applier.Initialize();
            applier.Apply(new SettingsData
            {
                viewDistance = 300, fieldOfView = 80, mouseSensitivity = 1.5f, invertY = true, lampLights = 8, ambienceVolume = 50,
                showClock = true
            });
            Assert.AreEqual(315f, Cam.farClipPlane, 1e-3f);
            Assert.AreEqual(80f, Cam.fieldOfView, 1e-3f);
            Assert.AreEqual(1.5f, player.LookSensitivity, 1e-5f);
            Assert.IsTrue(player.InvertY);
            Assert.AreEqual(300f, culler.ViewDistance);
            Assert.AreEqual(180f, culler.PropDistance, 1e-3f);
            Assert.AreEqual(2.3f / 300f, director.MinFogDensity, 1e-6f);
            Assert.AreEqual(8, director.Lamps.LightBudget);
            Assert.AreEqual(180f, director.Lamps.MaxLightDistance, 1e-3f);
            Assert.AreEqual(0.5f, director.Ambience.Level, 1e-5f);
            Assert.IsTrue(director.ShowClock);
        }

        [Test]
        public void Apply_ShadowQualityReachesSunAndMoon()
        {
            applier.Initialize();
            applier.Apply(new SettingsData { shadows = ShadowQuality.Low });
            Assert.AreEqual(LightShadows.Hard, director.Sun.shadows);
            Assert.AreEqual(LightShadows.Hard, director.Moon.shadows);
        }

        [Test]
        public void Apply_SetsMasterAndEffectsVolumes_LeavesAmbience()
        {
            applier.Initialize();
            var source = fx.Track(new GameObject("Sfx")).AddComponent<AudioSource>();
            source.volume = 0.8f;
            float dayBefore = director.Ambience.Day.volume;
            applier.Apply(new SettingsData { masterVolume = 40, effectsVolume = 50 });
            Assert.AreEqual(0.4f, AudioListener.volume, 1e-5f);
            Assert.AreEqual(0.4f, source.volume, 1e-5f);
            applier.Apply(new SettingsData { masterVolume = 40, effectsVolume = 50 });
            Assert.AreEqual(0.4f, source.volume, 1e-5f, "tidak mengecil berulang");
            Assert.AreEqual(dayBefore, director.Ambience.Day.volume, 1e-5f, "ambience diatur lewat Level, bukan Efek");
        }

        [Test]
        public void Apply_SanitizesInput()
        {
            applier.Initialize();
            applier.Apply(new SettingsData { viewDistance = 5000 });
            Assert.AreEqual(800, applier.Current.viewDistance);
            Assert.AreEqual(840f, Cam.farClipPlane, 1e-3f);
        }

        [Test]
        public void Apply_KeepsItsOwnCopy()
        {
            applier.Initialize();
            var settings = new SettingsData { viewDistance = 450 };
            applier.Apply(settings);
            settings.viewDistance = 100;
            Assert.AreEqual(450, applier.Current.viewDistance);
        }

        [Test]
        public void Save_WritesTheBackendOnlyWhenAsked()
        {
            applier.Initialize();
            applier.Apply(new SettingsData { viewDistance = 450 });
            Assert.AreEqual(0, backend.Writes);
            applier.Save();
            Assert.AreEqual(1, backend.Writes);
            StringAssert.Contains("\"viewDistance\":450", backend.Json);
        }

        [Test]
        public void ApplyAll_RaisesApplied()
        {
            int calls = 0;
            applier.Applied += _ => calls++;
            applier.Initialize();
            applier.ApplyAll();
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void Shutdown_RestoresTheRenderPipeline()
        {
            applier.Initialize();
            Assert.AreNotSame(pipelineBefore, QualitySettings.renderPipeline);
            applier.Shutdown();
            Assert.AreSame(pipelineBefore, QualitySettings.renderPipeline);
            Assert.IsFalse(applier.IsInitialized);
        }

        [Test]
        public void WithoutPlayerOrDirector_NoErrors()
        {
            applier.FindPlayer = () => null;
            applier.FindDirector = () => null;
            applier.Initialize();
            Assert.DoesNotThrow(() => applier.Apply(new SettingsData()));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
```

- [ ] **Step 2: Jalankan tes, pastikan gagal**

Procedure R+T, `Group = "EffectsVolumeTests|SettingsApplierTests"`.
Expected: `COMPILE FAILED`.

- [ ] **Step 3: Implementasi**

`Assets/GameSettings/Runtime/Audio/EffectsVolume.cs`:
```csharp
using System.Collections.Generic;
using TycoWorld.DayCycle;
using UnityEngine;

namespace TycoWorld.GameSettings
{
    /// <summary>
    /// The "Efek" volume (spec #4 §5.5): scales every AudioSource except the ambience layers from its original volume.
    /// PlayOneShot multiplies by the source volume, so footsteps and interaction sounds follow without code changes.
    /// </summary>
    public sealed class EffectsVolume
    {
        readonly Dictionary<AudioSource, float> originals = new Dictionary<AudioSource, float>();

        public void Apply(IEnumerable<AudioSource> sources, float level)
        {
            if (sources == null) return;
            foreach (var source in sources)
            {
                if (source == null || source.GetComponentInParent<AmbienceAudio>(true) != null) continue;
                if (!originals.TryGetValue(source, out float original))
                {
                    original = source.volume;
                    originals[source] = original;
                }
                source.volume = original * level;
            }
        }

        public void Restore()
        {
            foreach (var pair in originals)
                if (pair.Key != null) pair.Key.volume = pair.Value;
            originals.Clear();
        }
    }
}
```

`Assets/GameSettings/Runtime/SettingsApplier.cs`:
```csharp
using System;
using TycoWorld.DayCycle;
using TycoWorld.Player;
using TycoWorld.WorldGen;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TycoWorld.GameSettings
{
    /// <summary>
    /// Owns the current settings (spec #4 §3): loads them before the first frame, applies them to the camera, player,
    /// culler, day cycle, quality, audio and screen whenever they change, saves on request, and gives the quality
    /// globals back on shutdown.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SettingsApplier : MonoBehaviour
    {
        /// <summary>Re-apply every frame for at most this long until the player and director are initialized.</summary>
        public const float TargetRetrySeconds = 10f;

        readonly EffectsVolume effects = new EffectsVolume();
        SettingsStore store;
        QualityApplier quality;
        float retryTime;
        bool targetsReady;
        bool screenApplied, lastFullscreen;
        int lastWidth, lastHeight;

        public event Action<SettingsData> Applied;

        public SettingsData Current { get; private set; } = new SettingsData();
        public bool IsInitialized { get; private set; }
        public QualityApplier Quality => quality;
        /// <summary>Settings storage; null (default) = PlayerPrefs. Tests set a memory backend before Initialize.</summary>
        public ISettingsBackend Backend { get; set; }
        public Func<FirstPersonPlayer> FindPlayer { get; set; } = () => UnityEngine.Object.FindAnyObjectByType<FirstPersonPlayer>();
        public Func<DayCycleDirector> FindDirector { get; set; } = () => UnityEngine.Object.FindAnyObjectByType<DayCycleDirector>();

        void Awake()
        {
            if (Application.isPlaying) Initialize();
        }

        void Start()
        {
            if (Application.isPlaying) ApplyAll();
        }

        void Update()
        {
            if (!Application.isPlaying || targetsReady) return;
            retryTime += Time.unscaledDeltaTime;
            if (retryTime > TargetRetrySeconds)
            {
                targetsReady = true;
                return;
            }
            ApplyAll();
        }

        void OnDestroy() => Shutdown();

        public void Initialize()
        {
            if (IsInitialized) return;
            store = new SettingsStore(Backend != null ? Backend : new PlayerPrefsBackend());
            Current = store.Load();
            quality = new QualityApplier();
            SceneManager.sceneLoaded += OnSceneLoaded;
            IsInitialized = true;
        }

        /// <summary>Validates a copy of <paramref name="settings"/>, makes it current and applies it (live, not saved).</summary>
        public void Apply(SettingsData settings)
        {
            if (!IsInitialized) Initialize();
            var copy = settings.Clone();
            SettingsValidator.Sanitize(copy);
            Current = copy;
            ApplyAll();
        }

        public void Save()
        {
            if (store != null) store.Save(Current);
        }

        public void Shutdown()
        {
            if (!IsInitialized) return;
            IsInitialized = false;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (quality != null) quality.Dispose();
            quality = null;
        }

        public void ApplyAll()
        {
            if (!IsInitialized) Initialize();
            var s = Current;
            float view = s.viewDistance;
            float props = ViewDistanceRules.PropDistance(view);

            var player = FindPlayer != null ? FindPlayer() : null;
            var camera = player != null && player.View != null ? player.View.GetComponent<Camera>() : null;
            if (camera != null)
            {
                camera.farClipPlane = ViewDistanceRules.FarClip(view);
                camera.fieldOfView = s.fieldOfView;
            }
            if (player != null)
            {
                player.LookSensitivity = s.mouseSensitivity;
                player.InvertY = s.invertY;
            }

            var culler = GetComponent<DistanceCuller>();
            if (culler == null) culler = UnityEngine.Object.FindAnyObjectByType<DistanceCuller>();
            if (culler != null)
            {
                culler.ViewDistance = view;
                culler.PropDistance = props;
            }

            var director = FindDirector != null ? FindDirector() : null;
            Light sun = null, moon = null;
            if (director != null)
            {
                director.MinFogDensity = ViewDistanceRules.MinFogDensity(view);
                director.ShowClock = s.showClock;
                if (director.Lamps != null)
                {
                    director.Lamps.LightBudget = s.lampLights;
                    director.Lamps.MaxLightDistance = props;
                }
                if (director.Ambience != null) director.Ambience.Level = s.ambienceVolume / 100f;
                sun = director.Sun;
                moon = director.Moon;
            }

            quality.Apply(s, camera, new[] { sun, moon });
            AudioListener.volume = s.masterVolume / 100f;
            effects.Apply(UnityEngine.Object.FindObjectsByType<AudioSource>(), s.effectsVolume / 100f);
            ApplyScreen(s);
            targetsReady = player != null && player.IsInitialized && (director == null || director.IsInitialized);
            Applied?.Invoke(s);
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyAll();

        void ApplyScreen(SettingsData s)
        {
            // Screen.SetResolution has no effect in the editor's Game view; screen settings are checked by hand in a build.
            if (Application.isEditor) return;
            if (screenApplied && lastFullscreen == s.fullscreen && lastWidth == s.resolutionWidth && lastHeight == s.resolutionHeight) return;
            bool first = !screenApplied;
            screenApplied = true;
            lastFullscreen = s.fullscreen;
            lastWidth = s.resolutionWidth;
            lastHeight = s.resolutionHeight;
            bool currentlyFullscreen = Screen.fullScreenMode != FullScreenMode.Windowed;
            if (first && s.resolutionWidth == 0 && s.fullscreen == currentlyFullscreen) return;
            var mode = s.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            int width = s.resolutionWidth > 0 ? s.resolutionWidth : s.fullscreen ? Screen.currentResolution.width : Screen.width;
            int height = s.resolutionHeight > 0 ? s.resolutionHeight : s.fullscreen ? Screen.currentResolution.height : Screen.height;
            Screen.SetResolution(width, height, mode);
        }
    }
}
```

- [ ] **Step 4: Jalankan tes, pastikan lulus**

Procedure R+T, `Group = "EffectsVolumeTests|SettingsApplierTests"`.
Expected: `passed=14 failed=0` (Effects 4 + Applier 10).

- [ ] **Step 5: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add Assets/GameSettings
git -C "D:/UNITY/project/Tyco World" commit -m "feat(settings): settings applier driving camera, culling, day cycle, quality and audio

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

---

### Task 12: `GameMenu` (Esc, pause, halaman Pengaturan, penghitung FPS)

**Files:**
- Create: `Assets/GameSettings/Runtime/Menu/GameMenu.cs`
- Test: `Assets/GameSettings/Tests/GameMenuTests.cs`

**Interfaces:**
- Consumes:
  - `MenuNavigator`, `PauseController`, `IPauseEffects`, `UnityPauseEffects` (Task 10);
  - `PauseMenuView`, `SettingsView`, `FpsCounterView` (Task 9);
  - `SettingsPresenter`, `ResolutionOptions` (Task 8);
  - `SettingsApplier` (Task 11), `FrameStats` (Task 1).
- Produces: `GameMenu : MonoBehaviour` (`[RequireComponent(typeof(UIDocument))]`) dengan:
  - seam `PauseEffects`, `FindPlayer`, `ListResolutions`, `QuitAction`;
  - `Navigator`, `Pause`, `Presenter`, `PauseView`, `SettingsView`, `IsBuilt`;
  - `Initialize()`, `Build(VisualElement root)`, `Escape()`, `Resume()`, `OpenSettings()`, `RequestQuit()`, `CancelQuit()`, `ConfirmQuit()`, `BackFromSettings()`, `Tick(float unscaledDt)`, `Shutdown()`.

Alur halaman (spec §6):
- Dari Closed → pause.
- Ke Closed → resume.
- Keluar dari Settings → `applier.Save()`.
- Masuk Settings → presenter memuat `applier.Current` lalu `Refresh`.
- Perubahan di presenter langsung masuk lewat `applier.Apply`.
- Tombol view memanggil method publik `GameMenu`, sehingga tes EditMode memanggil method yang sama.

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/GameSettings/Tests/GameMenuTests.cs`:
```csharp
using NUnit.Framework;
using TycoWorld.Player;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace TycoWorld.GameSettings.Tests
{
    public class GameMenuTests
    {
        GameFixture fx;
        FirstPersonPlayer player;
        SettingsApplier applier;
        GameMenu menu;
        FakePauseEffects effects;
        MemorySettingsBackend backend;
        VisualElement root;
        int quits;
        RenderPipelineAsset pipelineBefore;
        int frameRateBefore, vSyncBefore;

        [SetUp]
        public void SetUp()
        {
            pipelineBefore = QualitySettings.renderPipeline;
            frameRateBefore = Application.targetFrameRate;
            vSyncBefore = QualitySettings.vSyncCount;
            fx = new GameFixture();
            player = fx.CreatePlayer();
            var go = fx.Track(new GameObject("GameSystems"));
            applier = go.AddComponent<SettingsApplier>();
            backend = new MemorySettingsBackend();
            applier.Backend = backend;
            applier.FindPlayer = () => player;
            applier.FindDirector = () => null;
            menu = go.AddComponent<GameMenu>();
            effects = new FakePauseEffects();
            menu.PauseEffects = effects;
            menu.FindPlayer = () => player;
            menu.ListResolutions = () => new[] { new Vector2Int(1920, 1080) };
            quits = 0;
            menu.QuitAction = () => quits++;
            applier.Initialize();
            menu.Initialize();
            root = new VisualElement();
            menu.Build(root);
        }

        [TearDown]
        public void TearDown()
        {
            menu.Shutdown();
            applier.Shutdown();
            fx.Dispose();
            if (QualitySettings.renderPipeline != pipelineBefore) QualitySettings.renderPipeline = pipelineBefore;
            if (QualitySettings.vSyncCount != vSyncBefore) QualitySettings.vSyncCount = vSyncBefore;
            Application.targetFrameRate = frameRateBefore;
        }

        static DisplayStyle Display(VisualElement e) => e.style.display.value;

        [Test]
        public void Build_StartsClosed_FpsHidden()
        {
            Assert.IsTrue(menu.IsBuilt);
            Assert.AreEqual(MenuPage.Closed, menu.Navigator.Page);
            Assert.AreEqual(DisplayStyle.None, Display(root.Q(PauseMenuView.OverlayName)));
            menu.Tick(0.5f);
            Assert.AreEqual(DisplayStyle.None, Display(root.Q(FpsCounterView.LabelName)));
        }

        [Test]
        public void Escape_OpensPauseAndFreezes()
        {
            menu.Escape();
            Assert.AreEqual(MenuPage.Main, menu.Navigator.Page);
            Assert.IsTrue(effects.Paused);
            Assert.AreEqual(DisplayStyle.Flex, Display(root.Q(PauseMenuView.OverlayName)));
            Assert.IsFalse(player.Locks.Allows(PlayerAbility.Move));
            Assert.IsFalse(player.EscapeUnlocksCursor);
        }

        [Test]
        public void Escape_Twice_Resumes()
        {
            menu.Escape();
            menu.Escape();
            Assert.AreEqual(MenuPage.Closed, menu.Navigator.Page);
            Assert.IsFalse(effects.Paused);
            Assert.IsTrue(player.Locks.Allows(PlayerAbility.Move));
        }

        [Test]
        public void SettingsEdits_ApplyLive_AndSaveWhenThePageCloses()
        {
            menu.Escape();
            menu.OpenSettings();
            Assert.AreEqual(MenuPage.Settings, menu.Navigator.Page);
            menu.Presenter.Edit(s => s.fieldOfView = 90);
            Assert.AreEqual(90, applier.Current.fieldOfView, "langsung diterapkan");
            Assert.AreEqual(90f, player.View.GetComponent<Camera>().fieldOfView, 1e-3f);
            Assert.AreEqual(0, backend.Writes, "belum disimpan");
            menu.Escape();
            Assert.AreEqual(MenuPage.Main, menu.Navigator.Page);
            Assert.AreEqual(1, backend.Writes);
            StringAssert.Contains("\"fieldOfView\":90", backend.Json);
        }

        [Test]
        public void OpeningSettings_ShowsTheAppliedValues()
        {
            applier.Apply(new SettingsData { viewDistance = 500 });
            menu.Escape();
            menu.OpenSettings();
            Assert.AreEqual(500, root.Q<SliderInt>(SettingsView.ViewDistanceName).value);
        }

        [Test]
        public void Quit_NeedsConfirmation()
        {
            menu.Escape();
            menu.RequestQuit();
            Assert.AreEqual(MenuPage.ConfirmQuit, menu.Navigator.Page);
            Assert.AreEqual(0, quits);
            menu.CancelQuit();
            Assert.AreEqual(MenuPage.Main, menu.Navigator.Page);
            menu.RequestQuit();
            menu.ConfirmQuit();
            Assert.AreEqual(1, quits);
        }

        [Test]
        public void FpsCounter_FollowsTheSetting()
        {
            applier.Apply(new SettingsData { showFps = true });
            for (int i = 0; i < 4; i++) menu.Tick(0.125f);
            var label = root.Q<Label>(FpsCounterView.LabelName);
            Assert.AreEqual(DisplayStyle.Flex, Display(label));
            Assert.AreEqual("8 FPS", label.text);
        }

        [Test]
        public void Shutdown_WhilePaused_Resumes()
        {
            menu.Escape();
            menu.Shutdown();
            Assert.IsFalse(effects.Paused);
            Assert.IsTrue(player.Locks.Allows(PlayerAbility.Move));
        }
    }
}
```

- [ ] **Step 2: Jalankan tes, pastikan gagal**

Procedure R+T, `Group = "GameMenuTests"`.
Expected: `COMPILE FAILED` (`GameMenu` belum ada).

- [ ] **Step 3: Implementasi**

`Assets/GameSettings/Runtime/Menu/GameMenu.cs`:
```csharp
using System;
using System.Collections.Generic;
using TycoWorld.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace TycoWorld.GameSettings
{
    /// <summary>
    /// The pause menu (spec #4 §6): Esc opens and closes it, it pauses the game, hosts the settings page (changes apply
    /// live and are saved when the page closes), shows the FPS counter, and never leaves the game paused when destroyed.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public sealed class GameMenu : MonoBehaviour
    {
        readonly MenuNavigator navigator = new MenuNavigator();
        readonly FrameStats fpsStats = new FrameStats(0.5f);
        SettingsApplier applier;
        PauseController pause;
        SettingsPresenter presenter;
        PauseMenuView pauseView;
        SettingsView settingsView;
        FpsCounterView fpsView;

        /// <summary>Pause side effects; null (default) = the real Time.timeScale and AudioListener. Tests set a fake.</summary>
        public IPauseEffects PauseEffects { get; set; }
        public Func<FirstPersonPlayer> FindPlayer { get; set; } = () => UnityEngine.Object.FindAnyObjectByType<FirstPersonPlayer>();
        public Func<IReadOnlyList<Vector2Int>> ListResolutions { get; set; } = ScreenResolutions;
        /// <summary>What "Ya, keluar" does; the default stops Play in the editor and quits a build.</summary>
        public Action QuitAction { get; set; } = QuitGame;

        public MenuNavigator Navigator => navigator;
        public PauseController Pause => pause;
        public SettingsPresenter Presenter => presenter;
        public PauseMenuView PauseView => pauseView;
        public SettingsView SettingsView => settingsView;
        public bool IsBuilt => pauseView != null;

        void Start()
        {
            if (Application.isPlaying) Initialize();
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            if (pauseView == null)
            {
                var document = GetComponent<UIDocument>();
                if (document != null && document.rootVisualElement != null) Build(document.rootVisualElement);
            }
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Escape();
            Tick(Time.unscaledDeltaTime);
        }

        void OnDestroy() => Shutdown();

        public void Initialize()
        {
            if (pause != null) return;
            applier = GetComponent<SettingsApplier>();
            pause = new PauseController(PauseEffects != null ? PauseEffects : new UnityPauseEffects());
            pause.Attach(FindPlayer != null ? FindPlayer() : null);
            navigator.PageChanged += OnPageChanged;
        }

        public void Build(VisualElement root)
        {
            if (pauseView != null) return;
            Initialize();
            root.style.flexGrow = 1f;
            root.pickingMode = PickingMode.Ignore;
            fpsView = new FpsCounterView(root);
            pauseView = new PauseMenuView(root);
            presenter = new SettingsPresenter(CurrentSettings());
            presenter.Changed += OnSettingsEdited;
            settingsView = new SettingsView(pauseView.SettingsHost, presenter, ListResolutions != null ? ListResolutions() : null);
            pauseView.ResumeClicked += Resume;
            pauseView.SettingsClicked += OpenSettings;
            pauseView.QuitClicked += RequestQuit;
            pauseView.QuitCancelled += CancelQuit;
            pauseView.QuitConfirmed += ConfirmQuit;
            settingsView.BackClicked += BackFromSettings;
            pauseView.Show(navigator.Page);
        }

        public void Escape()
        {
            Initialize();
            navigator.Escape();
        }

        public void Resume() => navigator.Go(MenuPage.Closed);

        public void OpenSettings() => navigator.Go(MenuPage.Settings);

        public void RequestQuit() => navigator.Go(MenuPage.ConfirmQuit);

        public void CancelQuit() => navigator.Go(MenuPage.Main);

        public void BackFromSettings() => navigator.Go(MenuPage.Main);

        public void ConfirmQuit()
        {
            if (QuitAction != null) QuitAction();
        }

        public void Tick(float unscaledDeltaTime)
        {
            fpsStats.Sample(unscaledDeltaTime);
            if (fpsView != null) fpsView.Render(applier != null && applier.Current.showFps, fpsStats.HasResult ? fpsStats.AverageFps : 0f);
        }

        /// <summary>Unpauses when the menu goes away (Play stopped, scene unloaded).</summary>
        public void Shutdown()
        {
            if (pause != null) pause.Shutdown();
        }

        void OnPageChanged(MenuPage from, MenuPage to)
        {
            if (from == MenuPage.Closed) pause.Pause();
            if (to == MenuPage.Closed) pause.Resume();
            if (from == MenuPage.Settings && applier != null) applier.Save();
            if (to == MenuPage.Settings && presenter != null)
            {
                presenter.Load(CurrentSettings());
                settingsView.Refresh();
            }
            if (pauseView != null) pauseView.Show(to);
        }

        void OnSettingsEdited(SettingsData settings)
        {
            if (applier != null) applier.Apply(settings);
        }

        SettingsData CurrentSettings() => applier != null ? applier.Current : new SettingsData();

        static IReadOnlyList<Vector2Int> ScreenResolutions()
        {
            var sizes = new List<Vector2Int>();
            foreach (var resolution in Screen.resolutions) sizes.Add(new Vector2Int(resolution.width, resolution.height));
            return ResolutionOptions.Distinct(sizes);
        }

        static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
```

- [ ] **Step 4: Jalankan tes, pastikan lulus**

Procedure R+T, `Group = "GameMenuTests"`.
Expected: `passed=8 failed=0`.

- [ ] **Step 5: Seluruh tes GameSettings (regresi)**

Procedure R+T, `Group = "TycoWorld.GameSettings.Tests"`.
Expected: `failed=0`.

- [ ] **Step 6: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add Assets/GameSettings
git -C "D:/UNITY/project/Tyco World" commit -m "feat(settings): pause menu component with live settings and FPS counter

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

---

### Task 13: Menu "Tyco World/Game Settings Setup", wiring `World.unity`, dan spike input UI

**Files:**
- Create: `Assets/GameSettings/Editor/GameSettingsSceneSetup.cs`
- Create: `Assets/GameSettings/Tests/GameSettingsSetupTests.cs`
- Create: `Assets/GameSettings/Tests/McpGameDriver.cs`
- Modify (lewat menu): `Assets/Scenes/World.unity`

**Interfaces:**
- Consumes:
  - `SettingsApplier`, `GameMenu` (Task 11–12), `DistanceCuller` (Task 4), `NoSsaoRendererBuilder` (Task 7);
  - `WorldScenes.CheckSaved/EnsureWorldSceneOpen/OpenGeneratedSceneAdditive` (WorldGen.Editor);
  - `PlayerPaths.PanelSettingsAsset` (Player.Editor).
- Produces:
  - `GameSettingsSceneSetup.Run(bool interactive, out string error) → bool` dan `Wire(Scene world, PanelSettings panel) → GameObject`;
  - konstanta `RootName = "GameSystems"`, `MenuSortingOrder = 10`, `MissingPanelError`.
  - `McpGameDriver` (helper Play): `Status()`, `Escape()`, `FocusGameView()`, `Click(string elementName, bool press)`, `ApplyPreset(string, int fpsLimit)`, `SetViewDistance(int)`, `SetGpuOcclusion(bool)`, `Shot(string path)`.

Setup aman dijalankan berulang (pola hasil perbaikan Day Cycle Setup):
- `panelSettings` diisi hanya kalau kosong;
- `sortingOrder` diatur hanya saat `UIDocument` baru dibuat;
- komponen hanya ditambahkan kalau belum ada.

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/GameSettings/Tests/GameSettingsSetupTests.cs`:
```csharp
using System.Collections.Generic;
using NUnit.Framework;
using TycoWorld.GameSettings.Authoring;
using TycoWorld.WorldGen;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace TycoWorld.GameSettings.Tests
{
    public class GameSettingsSetupTests
    {
        readonly List<Object> created = new List<Object>();
        readonly HashSet<GameObject> rootsBefore = new HashSet<GameObject>();
        Scene scratch;

        /// <summary>The untitled scene the test runner opens stands in for World.unity; every root a test adds is destroyed in TearDown.</summary>
        Scene Scratch()
        {
            scratch = SceneManager.GetActiveScene();
            Assert.IsTrue(string.IsNullOrEmpty(scratch.path), "Tes setup hanya boleh memakai scene untitled, bukan " + scratch.path);
            rootsBefore.Clear();
            rootsBefore.UnionWith(scratch.GetRootGameObjects());
            return scratch;
        }

        PanelSettings NewPanel()
        {
            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            created.Add(panel);
            return panel;
        }

        [TearDown]
        public void TearDown()
        {
            if (scratch.IsValid() && scratch.isLoaded)
                foreach (var go in scratch.GetRootGameObjects())
                    if (!rootsBefore.Contains(go)) Object.DestroyImmediate(go);
            scratch = default;
            rootsBefore.Clear();
            foreach (var obj in created) if (obj != null) Object.DestroyImmediate(obj);
            created.Clear();
        }

        [Test]
        public void Wire_FirstRun_CreatesTheSystemsObject()
        {
            var scene = Scratch();
            var panel = NewPanel();
            var root = GameSettingsSceneSetup.Wire(scene, panel);
            Assert.AreEqual(GameSettingsSceneSetup.RootName, root.name);
            Assert.AreEqual(scene, root.scene);
            Assert.IsNotNull(root.GetComponent<SettingsApplier>());
            Assert.IsNotNull(root.GetComponent<DistanceCuller>());
            Assert.IsNotNull(root.GetComponent<GameMenu>());
            var document = root.GetComponent<UIDocument>();
            Assert.AreSame(panel, document.panelSettings);
            Assert.AreEqual(GameSettingsSceneSetup.MenuSortingOrder, (int)document.sortingOrder);
        }

        [Test]
        public void Wire_Rerun_KeepsUserChoices_NoDuplicates()
        {
            var scene = Scratch();
            var panel = NewPanel();
            var other = NewPanel();
            var root = GameSettingsSceneSetup.Wire(scene, panel);
            var document = root.GetComponent<UIDocument>();
            document.panelSettings = other;
            document.sortingOrder = 3;

            var again = GameSettingsSceneSetup.Wire(scene, panel);

            Assert.AreSame(root, again);
            Assert.AreSame(other, document.panelSettings);
            Assert.AreEqual(3, (int)document.sortingOrder);
            int systems = 0;
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == GameSettingsSceneSetup.RootName) systems++;
            Assert.AreEqual(1, systems);
            Assert.AreEqual(1, root.GetComponents<UIDocument>().Length);
            Assert.AreEqual(1, root.GetComponents<SettingsApplier>().Length);
            Assert.AreEqual(1, root.GetComponents<DistanceCuller>().Length);
            Assert.AreEqual(1, root.GetComponents<GameMenu>().Length);
        }

        [Test]
        public void Wire_Rerun_RefillsAMissingPanel()
        {
            var scene = Scratch();
            var panel = NewPanel();
            var root = GameSettingsSceneSetup.Wire(scene, panel);
            root.GetComponent<UIDocument>().panelSettings = null;
            GameSettingsSceneSetup.Wire(scene, panel);
            Assert.AreSame(panel, root.GetComponent<UIDocument>().panelSettings);
        }
    }
}
```

- [ ] **Step 2: Jalankan tes, pastikan gagal**

Procedure R+T, `Group = "GameSettingsSetupTests"`.
Expected: `COMPILE FAILED`.

- [ ] **Step 3: Implementasi setup**

`Assets/GameSettings/Editor/GameSettingsSceneSetup.cs`:
```csharp
using TycoWorld.Player.Authoring;
using TycoWorld.WorldGen;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace TycoWorld.GameSettings.Authoring
{
    /// <summary>"Tyco World/Game Settings Setup": adds GameSystems (settings, culling, pause menu) to World.unity; safe to re-run (spec #4 §6).</summary>
    public static class GameSettingsSceneSetup
    {
        public const string RootName = "GameSystems";
        public const int MenuSortingOrder = 10;
        public const string MissingPanelError = "PlayerHudPanel belum ada. Jalankan \"Tyco World/Player Setup\" dulu.";

        [MenuItem("Tyco World/Game Settings Setup")]
        static void SetupMenu()
        {
            if (!Run(true, out string error)) EditorUtility.DisplayDialog("Game Settings Setup gagal", error, "OK");
        }

        public static bool Run(bool interactive, out string error)
        {
            if (!WorldScenes.CheckSaved(interactive, out error)) return false;
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PlayerPaths.PanelSettingsAsset);
            if (panel == null)
            {
                error = MissingPanelError;
                return false;
            }
            if (NoSsaoRendererBuilder.Ensure() < 0)
            {
                error = NoSsaoRendererBuilder.MissingAssetsError;
                return false;
            }
            var world = WorldScenes.EnsureWorldSceneOpen();
            SceneManager.SetActiveScene(world);
            Wire(world, panel);
            EditorSceneManager.MarkSceneDirty(world);
            EditorSceneManager.SaveScene(world);
            WorldScenes.OpenGeneratedSceneAdditive();
            SceneManager.SetActiveScene(world);
            return true;
        }

        /// <summary>
        /// Creates or completes GameSystems in <paramref name="world"/>. A re-run keeps the panel and sort order already
        /// there and only adds missing components.
        /// </summary>
        public static GameObject Wire(Scene world, PanelSettings panel)
        {
            var root = FindRoot(world, RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
                SceneManager.MoveGameObjectToScene(root, world);
            }
            var document = GetOrAdd<UIDocument>(root, out bool newDocument);
            if (document.panelSettings == null) document.panelSettings = panel;
            if (newDocument) document.sortingOrder = MenuSortingOrder;
            GetOrAdd<SettingsApplier>(root, out _);
            GetOrAdd<DistanceCuller>(root, out _);
            GetOrAdd<GameMenu>(root, out _);
            EditorUtility.SetDirty(root);
            return root;
        }

        static GameObject FindRoot(Scene scene, string name)
        {
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == name) return go;
            return null;
        }

        static T GetOrAdd<T>(GameObject go, out bool added) where T : Component
        {
            var component = go.GetComponent<T>();
            added = component == null;
            if (added) component = go.AddComponent<T>();
            return component;
        }
    }
}
```

- [ ] **Step 4: Jalankan tes, pastikan lulus**

Procedure R+T, `Group = "GameSettingsSetupTests"`.
Expected: `passed=3 failed=0`.

- [ ] **Step 5: Tulis `McpGameDriver` (helper Play, bukan tes)**

`Assets/GameSettings/Tests/McpGameDriver.cs`:
```csharp
using System;
using System.Globalization;
using TycoWorld.DayCycle;
using TycoWorld.Player.Tests;
using TycoWorld.WorldGen;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace TycoWorld.GameSettings.Tests
{
    /// <summary>Dev helper for MCP checks of the pause menu and settings in Play mode (not a test). Keeps no static state.</summary>
    public static class McpGameDriver
    {
        static GameMenu Menu => Object.FindAnyObjectByType<GameMenu>();
        static SettingsApplier Applier => Object.FindAnyObjectByType<SettingsApplier>();

        public static string Status()
        {
            var menu = Menu;
            if (menu == null) return "NO MENU";
            var inv = CultureInfo.InvariantCulture;
            var applier = Applier;
            var director = Object.FindAnyObjectByType<DayCycleDirector>();
            var culler = Object.FindAnyObjectByType<DistanceCuller>();
            var cam = Camera.main;
            return "page=" + menu.Navigator.Page + " built=" + menu.IsBuilt
                   + " timeScale=" + Time.timeScale.ToString("F2", inv) + " audioPaused=" + AudioListener.pause
                   + " cursor=" + Cursor.lockState
                   + " t=" + (director != null && director.IsInitialized ? director.Clock.Display : "-")
                   + (applier != null ? " preset=" + applier.Current.preset + " view=" + applier.Current.viewDistance
                                        + " occlusion=" + applier.Current.gpuOcclusion : "")
                   + " far=" + (cam != null ? cam.farClipPlane.ToString("F0", inv) : "-")
                   + " hidden=" + (culler != null ? culler.HiddenCount + "/" + culler.Groups.Count : "-")
                   + " lampLights=" + (director != null && director.Lamps != null ? director.Lamps.LightsOn.ToString(inv) : "-");
        }

        public static string Escape()
        {
            var menu = Menu;
            if (menu == null) return "NO MENU";
            menu.Escape();
            return Status();
        }

        /// <summary>Pointer events reach the game only while the Game view has focus.</summary>
        public static string FocusGameView()
        {
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (type == null) return "NO GAME VIEW TYPE";
            EditorWindow.FocusWindowIfItsOpen(type);
            return "focused " + (EditorWindow.focusedWindow != null ? EditorWindow.focusedWindow.titleContent.text : "-");
        }

        /// <summary>Queues a real left-button press or release at the centre of a menu element (through the Input System).</summary>
        public static string Click(string elementName, bool press)
        {
            var menu = Menu;
            if (menu == null) return "NO MENU";
            var root = menu.GetComponent<UIDocument>().rootVisualElement;
            var element = root != null ? root.Q(elementName) : null;
            if (element == null || element.panel == null) return "NO ELEMENT " + elementName;
            var mouse = Mouse.current;
            if (mouse == null) return "NO MOUSE";
            var screen = PanelToScreen(element.panel, element.worldBound.center);
            var state = new MouseState { position = screen };
            if (press) state = state.WithButton(MouseButton.Left, true);
            InputSystem.QueueStateEvent(mouse, state);
            var inv = CultureInfo.InvariantCulture;
            return "queued " + (press ? "down" : "up") + " at (" + screen.x.ToString("F0", inv) + "," + screen.y.ToString("F0", inv) + ")";
        }

        public static string ApplyPreset(string preset, int fpsLimit)
        {
            var applier = Applier;
            if (applier == null) return "NO APPLIER";
            if (!Enum.TryParse(preset, out QualityPreset parsed)) return "BAD PRESET " + preset;
            var s = applier.Current.Clone();
            QualityPresets.Apply(parsed, s);
            s.fpsLimit = fpsLimit;
            applier.Apply(s);
            return Status();
        }

        public static string SetViewDistance(int meters)
        {
            var applier = Applier;
            if (applier == null) return "NO APPLIER";
            var s = applier.Current.Clone();
            s.viewDistance = meters;
            applier.Apply(s);
            return Status();
        }

        public static string SetGpuOcclusion(bool enabled)
        {
            var applier = Applier;
            if (applier == null) return "NO APPLIER";
            var s = applier.Current.Clone();
            s.gpuOcclusion = enabled;
            applier.Apply(s);
            return Status();
        }

        public static string Shot(string path) => McpRender.SavePng(Camera.main, path, 1280, 720);

        /// <summary>RuntimePanelUtils.ScreenToPanel is linear (with a y flip); invert it from two samples.</summary>
        static Vector2 PanelToScreen(IPanel panel, Vector2 panelPoint)
        {
            var origin = RuntimePanelUtils.ScreenToPanel(panel, Vector2.zero);
            var unit = RuntimePanelUtils.ScreenToPanel(panel, Vector2.one) - origin;
            return new Vector2((panelPoint.x - origin.x) / unit.x, (panelPoint.y - origin.y) / unit.y);
        }
    }
}
```

- [ ] **Step 6: Compile**

Procedure R+T, `Group = "GameSettingsSmokeTests"`.
Expected: `passed=1 failed=0`.

- [ ] **Step 7: Jalankan menu Setup pada `World.unity`**

Pastikan `World.unity` terbuka dan tersimpan, lalu:
```csharp
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        bool ok = TycoWorld.GameSettings.Authoring.GameSettingsSceneSetup.Run(false, out string error);
        if (ok) result.Log("setup ok"); else result.LogError(error);
    }
}
```
Expected: `setup ok`. Jalankan **dua kali**, lalu cek dengan snippet:
```csharp
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var world = SceneManager.GetSceneByPath("Assets/Scenes/World.unity");
        int systems = 0;
        GameObject root = null;
        foreach (var go in world.GetRootGameObjects())
            if (go.name == "GameSystems") { systems++; root = go; }
        var doc = root != null ? root.GetComponent<UnityEngine.UIElements.UIDocument>() : null;
        result.Log("systems=" + systems + " panel=" + (doc != null && doc.panelSettings != null ? doc.panelSettings.name : "-")
                   + " order=" + (doc != null ? doc.sortingOrder.ToString() : "-")
                   + " applier=" + (root != null && root.GetComponent<TycoWorld.GameSettings.SettingsApplier>() != null)
                   + " culler=" + (root != null && root.GetComponent<TycoWorld.WorldGen.DistanceCuller>() != null)
                   + " menu=" + (root != null && root.GetComponent<TycoWorld.GameSettings.GameMenu>() != null));
    }
}
```
Expected: `systems=1 panel=PlayerHudPanel order=10 applier=True culler=True menu=True`.

- [ ] **Step 8: Spike input UI di Play (tanpa EventSystem)**

1. Masuk Play (Procedure P) sampai `READY`.
2. Satu panggilan MCP per baris, berurutan:
   1. `McpGameDriver.Escape()`. Expected: `page=Main … timeScale=0.00 audioPaused=True cursor=None`.
   2. `McpGameDriver.FocusGameView()`.
   3. `McpGameDriver.Click("resume", true)`.
   4. `McpGameDriver.Click("resume", false)`.
   5. `McpGameDriver.Status()`. **Lulus** kalau `page=Closed timeScale=1.00`.
3. Kalau lulus: tidak perlu EventSystem. Catat di `progress.md`: `Task 13: Ruling: UI Toolkit menerima klik lewat InputSystemProvider tanpa EventSystem — cost if wrong: menu tidak bisa diklik di build`.
4. Kalau `page` masih `Main`:
   1. Keluar Play.
   2. Tambahkan EventSystem satu kali lewat MCP:
      ```csharp
      using UnityEditor;
      using UnityEditor.SceneManagement;
      using UnityEngine;
      using UnityEngine.SceneManagement;

      internal class CommandScript : IRunCommand
      {
          public void Execute(ExecutionResult result)
          {
              var world = SceneManager.GetSceneByPath("Assets/Scenes/World.unity");
              var go = new GameObject("EventSystem");
              SceneManager.MoveGameObjectToScene(go, world);
              go.AddComponent<UnityEngine.EventSystems.EventSystem>();
              go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
              EditorSceneManager.MarkSceneDirty(world);
              EditorSceneManager.SaveScene(world);
              result.Log("event system added");
          }
      }
      ```
   3. Ulangi langkah 2. Kalau sekarang lulus, catat ruling *"EventSystem dibutuhkan"* di `progress.md`, dan tambahkan baris di §11 spec (Catatan implementasi).
   4. Kalau tetap gagal, kemungkinan event pointer tidak sampai karena fokus editor. Hapus lagi objek `EventSystem`, catat *"spike tidak konklusif; klik diverifikasi manual"*, dan pastikan item klik ada di checklist QA manual (Task 14).
5. Keluar Play (Procedure P).

- [ ] **Step 9: Cek git status**

```bash
git -C "D:/UNITY/project/Tyco World" status --short
```
Expected: `Assets/Scenes/World.unity` (diubah Setup), file Task 13, dan `ProjectSettings/ProjectSettings.asset` (milik user). Tidak ada `QualitySettings.asset`, `TimeManager.asset`, atau `PC_RPAsset.asset`.

- [ ] **Step 10: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add Assets/GameSettings Assets/Scenes/World.unity
git -C "D:/UNITY/project/Tyco World" commit -m "feat(settings): Game Settings Setup menu and World.unity wiring

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

---

### Task 14: Verifikasi Play, pengukuran performa, dan catatan verifikasi

**Files:**
- Create: `docs/superpowers/notes/2026-10-14-performance-verification.md`
- Modify (hanya kalau keputusan GPU occlusion di Step 6 = matikan): `Assets/GameSettings/Runtime/Settings/QualityPresets.cs`, `SettingsData.cs`, `Assets/GameSettings/Tests/QualityPresetsTests.cs`, `SettingsViewTests.cs`, `Assets/GameSettings/Tests/QualityApplierTests.cs`

- [ ] **Step 1: Seluruh tes**

Procedure R+T, `Group = "."`.
Expected: `failed=0`. Catat jumlah `passed` (baseline sebelum sub-proyek ini: 550).

- [ ] **Step 2: Rekam git status sebelum Play**

```bash
git -C "D:/UNITY/project/Tyco World" status --short > "/d/UNITY/project/Tyco World/.superpowers/sdd/2026-10-14-performance-settings/status_before_play.txt"
```

- [ ] **Step 3: Ukur tiap preset**

Masuk Play (Procedure P) sampai `READY`. Lalu ulangi untuk `preset` = `Low`, `Medium`, `High`:
1. `McpGameDriver.ApplyPreset("<preset>", 0)`. Batas FPS 0 dipakai supaya angka tidak terpotong di 60.
2. Untuk empat titik (`kos`/12, `road`/12, `road`/21, `high`/12), jalankan urutan yang sama seperti Task 1 Step 12: `Place` → `SetTime` → `Begin(5f)` → `Report` (sampai bukan `PENDING`). Label: `<preset>-kos-day`, `<preset>-road-day`, `<preset>-road-night`, `<preset>-high-day`.

Simpan 12 baris ke `.superpowers/sdd/2026-10-14-performance-settings/perf-after.md`.

**Cek kriteria (spec §8):**
- `Medium-road-night` harus menunjukkan `lights=` ≤ 16 + 3 (matahari/bulan/senter);
- FPS-nya harus lebih tinggi dari `base-road-night`.

Kalau salah satu tidak terpenuhi, **berhenti dan laporkan** angka-angkanya. Jangan melonggarkan kriteria.

- [ ] **Step 4: Pembanding GPU occlusion**

Masih di Play:
1. `McpGameDriver.ApplyPreset("Medium", 0)`, lalu `McpGameDriver.SetGpuOcclusion(false)`.
2. Ukur `road`/12 dan `high`/12 dengan label `medium-noocc-road-day` dan `medium-noocc-high-day`.
3. Tambahkan hasilnya ke `perf-after.md`.

- [ ] **Step 5: Pause, view distance, dan screenshot**

Masih di Play:
1. `McpGameDriver.ApplyPreset("Medium", 60)`.
2. `McpGameDriver.Escape()`. Catat `t=`, panggil `Status()` lagi minimal 3 detik kemudian, lalu cek: `t` sama, `timeScale=0.00`, `audioPaused=True`.
3. `McpGameDriver.Escape()`. Beberapa detik kemudian `Status()` harus menunjukkan `page=Closed timeScale=1.00`, dengan `t` maju.
4. `McpPerfProbe.Place("road")` dan `McpPerfProbe.SetTime(12f)`, lalu:
   1. `McpGameDriver.SetViewDistance(150)`, lalu `McpGameDriver.Shot("D:/UNITY/project/Tyco World/.superpowers/sdd/2026-10-14-performance-settings/shot_view150.png")`;
   2. `McpGameDriver.SetViewDistance(800)`, lalu `Shot(".../shot_view800.png")`.
5. `McpPerfProbe.Release()`, lalu keluar Play (Procedure P).

- [ ] **Step 6: Keputusan default GPU occlusion**

Bandingkan `Medium-road-day` dengan `medium-noocc-road-day`, dan `Medium-high-day` dengan `medium-noocc-high-day`.

**Kalau tanpa occlusion ≥ 5 % lebih cepat di kedua titik**, matikan default-nya:
- `QualityPresets.Apply`: argumen `gpuOcclusion` ketiga preset diganti `false`.
- `SettingsData.gpuOcclusion = false`.
- Tes:
  - `QualityPresetsTests.Apply_FillsTheSpecTable`: `Assert.IsTrue(s.gpuOcclusion)` → `Assert.IsFalse(s.gpuOcclusion)`;
  - `SettingsViewTests.Refresh_ShowsTheDefaults`: `Assert.IsTrue(Q<Toggle>(SettingsView.GpuOcclusionName).value)` → `Assert.IsFalse(…)`;
  - `QualityApplierTests.Apply_WritesTheCopyOnly`: `Assert.IsTrue(copy.gpuResidentDrawerEnableOcclusionCullingInCameras)` → `Assert.IsFalse(…)`.
- Spec §4: baris GPU occlusion jadi "mati" di ketiga preset, plus catatan di §11.
- Jalankan Procedure R+T `Group = "."` (failed=0), lalu commit `fix(settings): GPU occlusion off by default (measured slower)`.

**Kalau tidak**, biarkan default-nya, dan catat ruling di `progress.md`: `Task 14: Ruling: GPU occlusion tetap nyala — <angka> — cost if wrong: sedikit beban GPU di mesin lemah`.

- [ ] **Step 7: Console dan git status**

- Console: `mcp__unity-mcp__Unity_GetConsoleLogs` dengan `logTypes: "error,warning"`. Expected: tidak ada error/warning baru dari sesi Play.
- Bandingkan git status:
  ```bash
  cd "/d/UNITY/project/Tyco World" && git status --short > /tmp/after.txt; diff .superpowers/sdd/2026-10-14-performance-settings/status_before_play.txt /tmp/after.txt && echo IDENTICAL
  ```
  Expected: `IDENTICAL`. Ini membuktikan tidak ada `QualitySettings.asset`, `PC_RPAsset.asset`, `TimeManager.asset`, atau scene yang ikut berubah.

- [ ] **Step 8: Tulis catatan verifikasi**

`docs/superpowers/notes/2026-10-14-performance-verification.md` (isi dengan angka nyata dari `perf-baseline.md` dan `perf-after.md`; jangan mengarang):
```markdown
# Performa + Pause/Settings — verifikasi (2026-10-14)

- **Tes EditMode:** `group=. passed=<N> failed=0`.
- **Spike input UI:** <lulus tanpa EventSystem / perlu EventSystem / tidak konklusif + alasan>.
- **Pause:** jam game berhenti (<t>) selama menu terbuka, `timeScale=0`, audio di-pause; Lanjutkan → jalan lagi.
- **git status sebelum/sesudah Play:** identik. Console bersih.

## Pengukuran (editor, batas FPS dimatikan)

| Titik | Baseline | Rendah | Sedang | Tinggi |
|---|---|---|---|---|
| kos siang | <fps> fps · <batches> batch · <lights> light | … | … | … |
| jalan siang | … | … | … | … |
| jalan malam 21:00 | … | … | … | … |
| atas 30 m | … | … | … | … |

- Renderer aktif (jalan siang): baseline <a>/<b> → Sedang <c>/<b>.
- GPU occlusion (Sedang): nyala <x> fps vs mati <y> fps → keputusan: <tetap nyala / dimatikan>.
- Screenshot `shot_view150.png` dan `shot_view800.png`: <apa yang terlihat di tepi pandang>.
- Angka editor tidak sama dengan build. Penghitung FPS (Tampilan) dipakai untuk cek di build.

## Checklist QA manual (input asli tidak bisa diotomasi)
- [ ] Esc membuka menu Jeda: jam, langkah, dan suara berhenti. Esc lagi atau Lanjutkan → kursor terkunci lagi.
- [ ] Klik semua tombol dan tab, geser slider, buka dan pilih dropdown dengan mouse asli.
- [ ] View distance 100 ↔ 800: batas pandang tertutup kabut, *pop-in* gedung tidak mencolok.
- [ ] Preset Rendah (70 % + STP) masih enak dilihat; Tinggi tampak lebih tajam dengan SSAO.
- [ ] Sensitivitas mouse, invert Y, dan FOV terasa sesuai.
- [ ] Volume Master, Efek (langkah, pagar), dan Ambience (jangkrik malam) berubah sesuai slider.
- [ ] Tampilan: penghitung FPS dan jam bisa dinyalakan/dimatikan.
- [ ] Stop Play lalu Play lagi: pengaturan masih tersimpan.
- [ ] Malam: lampu jalan di sekitar pemain menyala halus saat berjalan; lampu jauh hanya kepalanya yang menyala.
- [ ] Keluar → konfirmasi "Keluar dari game?" → Play berhenti.
- [ ] Build Windows: mode layar, resolusi, dan penghitung FPS.
```

- [ ] **Step 9: Kirim screenshot ke user**

Kirim `shot_view150.png` dan `shot_view800.png` dengan `SendUserFile`.

- [ ] **Step 10: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add docs/superpowers/notes/2026-10-14-performance-verification.md
git -C "D:/UNITY/project/Tyco World" commit -m "docs(settings): performance measurements and verification notes

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```
