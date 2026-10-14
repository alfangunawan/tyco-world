# Motor yang Bisa Dikendarai — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Motor di halaman Kos Raflesia 3 bisa dinaiki (E), dikendarai arcade sampai 60 km/j dengan kamera orang ketiga/pertama (V), lampu depan (F), klakson (H), suara mesin dan speedometer, lalu diturunkan dengan aman (E saat ≤ 5 km/j).

**Architecture:**
- Assembly baru `TycoWorld.Vehicles` di atas Player/WorldGen.
- Logika murni dites di EditMode: `BikeModel`, `RideCameraMath`, `EngineSound`, sumber intent skrip.
- Komponen `Motorbike` (orkestrator, `Tick`/`LateTick`) menggerakkan CharacterController + box cast, lalu menerapkan visual, audio, HUD, dan kamera.
- Pemain duduk lewat mode baru `FirstPersonPlayer.Seat/Unseat`. Kamera pemain yang sama dipindah oleh `RideCamera`, jadi Settings, culling, jatah lampu, dan AudioListener tidak perlu diubah.
- Aset dan prefab dibangun dari kode (menu "Tyco World/Vehicle Setup").

**Tech Stack:** Unity 6000.6.5f1, URP 17.6.0, Input System 1.20.0 (action map Vehicle dibuat lewat `InputActionAsset` API), UI Toolkit (HUD pemain), Unity Test Framework 1.8.0, Unity MCP, Bash.

**Spec:** `docs/superpowers/specs/2026-10-14-motorbike-design.md`

## Global Constraints

**Proyek dan branch**
- Project root `D:\UNITY\project\Tyco World`. Branch kerja `feature/motorbike` (sudah ada, dari `main` = `1a6b072`; spec di commit `2c11ac0`).
- Unity 6000.6.5f1, URP 17.6.0, `com.unity.inputsystem` 1.20.0, `com.unity.test-framework` 1.8.0.

**Assembly dan namespace**
- Runtime `TycoWorld.Vehicles` (namespace `TycoWorld.Vehicles`).
- Editor `TycoWorld.Vehicles.Editor` (namespace `TycoWorld.Vehicles.Authoring`).
- Tes `TycoWorld.Vehicles.Tests` (namespace `TycoWorld.Vehicles.Tests`).
- Arah dependensi: GameSettings → Vehicles → Player → WorldGen. Player, WorldGen, dan DayCycle tidak pernah mereferensikan Vehicles.

**Aturan C# dan API Unity**
- C# 9: tidak ada `record`, `init`, `^`.
- Semua format angka memakai `CultureInfo.InvariantCulture`.
- Unity 6.6:
  - pakai `Object.FindObjectsByType<T>()` / `Object.FindAnyObjectByType<T>()` (tanpa `FindObjectsSortMode`);
  - jangan pakai API instance-ID berbasis `int`.
- Jangan pakai `??` / `?.` / `??=` pada `UnityEngine.Object` (pada objek C# biasa boleh).
- File yang punya `using System;` dan `using UnityEngine;` sekaligus menulis `UnityEngine.Object` secara eksplisit.

**Runtime**
- **Tidak boleh mengubah asset saat runtime.** Lampu belakang dan lensa lampu depan memakai salinan material `HideFlags.DontSave` (`RuntimeEmission`). `git status` tidak boleh berubah setelah Play.
- Domain reload saat Play mati: **tidak ada state `static` yang bisa berubah**; event ada di instance.
- Komponen yang dites punya method eksplisit (`Initialize`, `Tick(dt)`, `LateTick()`, `Mount`, `Dismount`, `Shutdown`). `Update`/`LateUpdate` hanya bekerja kalau `Application.isPlaying`.
- Motor **tidak pernah mengunci Look**, supaya `PauseController.Resume` (#4) tetap mengunci kursor lagi. Yang dikunci saat naik hanya `Move | Interact | Flashlight`.

**Tes**
- Fixture fisika Vehicles berada di (7000, 1000, 7000), jauh dari dunia dan dari fixture Player (5000).
- Fixture GameObject dihancurkan di `TearDown`. Tes tidak mengubah `Time.timeScale`.

**Teks yang tampil ke pemain** (tulis persis):
- `"[E] Naik motor"`, `"[E] Turun"`;
- `"Pelankan motor dulu."`, `"Tidak ada ruang untuk turun."`;
- `"km/j"`, `"LAMPU"`.

**Nilai**
- Semua angka kendali, kamera, lampu, dan mesin hanya ditulis sebagai initializer field di `BikeTuning`, disalin dari spec §4.

**File proyek**
- Jangan mengubah ProjectSettings. Jangan pernah `git add ProjectSettings`, `git add -A`, atau `git add .` (`ProjectSettings/ProjectSettings.asset` punya perubahan user yang belum di-commit).
- `Assets/InputSystem_Actions.inputactions` hanya diubah lewat `VehicleInputActions.Ensure` (Task 1).

**Commit**
- Setiap commit diakhiri baris `Co-Authored-By: <model yang menulis kode> <noreply@anthropic.com>` dan `Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5`.

**Deviasi kecil yang disengaja dari spec**
- Spec §4 menulis titik tatap kamera orang ketiga "1,2 m di atas kursi". Plan ini memakai **1,2 m di atas akar motor (tanah)**. Dengan kamera di 1,9 m, titik tatap di atas kursi (±2,05 m) membuat kamera menengadah, sedangkan 1,2 m dari tanah memberi sudut pandang ±8,8° menunduk seperti yang dimaksud.
- `HudModel.SetVehicle` mendapat parameter keempat `canExit`, dan hint "[E] Turun" menjadi bagian panel kendaraan. Alasannya, `Interactor` mengosongkan prompt HUD setiap frame selama Interact terkunci, jadi prompt biasa tidak bisa dipakai.
- Lampu depan otomatis mati saat turun (mesin mati).
- Intensitas lampu depan **60**, bukan 4 seperti spec awal. Nilainya dinaikkan di gelombang perbaikan akhir setelah cek Play malam, karena 4 nyaris tidak terlihat di URP proyek ini (spec §4 sudah diperbarui).

## Review Focus

1. **E untuk naik ditekan pada frame yang sama dengan Exit di map Vehicle.** Pemain tidak boleh langsung turun lagi (jeda 0,3 s). Dipaku oleh `MotorbikeTests.ExitPressedRightAfterMounting_IsIgnored` (Task 8).
2. **Teleport/respawn pemain (spawner, alat uji) atau motor dihancurkan saat sedang naik.** Pemain turun, semua kunci motor dilepas, HUD kendaraan hilang, dan kontrol tidak macet. Dipaku oleh:
   - `FirstPersonPlayerTests.Teleport_WhileSeated_UnseatsAndRaisesTheEvent` (Task 3);
   - `MotorbikeTests.RiderTeleported_ReleasesTheBike` dan `Shutdown_WhileRidden_FreesThePlayer` (Task 8).
3. **Menu pause dibuka saat melaju.** Motor diam (dt = 0, input diabaikan), dan Look tidak pernah dikunci motor. Dipaku oleh `MotorbikeTests.ZeroDt_DoesNothing` dan `Mount_SeatsThePlayer_LocksEverythingButLook` (Task 8).
4. **Menabrak tembok lurus pada 60 km/j dengan frame panjang (dt 0,1).** Motor berhenti tanpa menembus. Menabrak miring membuat motor meluncur di sepanjang tembok. Dipaku oleh `MotorbikeTests.WallAhead_StopsWithoutPenetrating_EvenWithLongFrames` dan `GlancingWall_SlidesAlong` (Task 8).
5. **Turun di gang sempit, di tanah tidak rata, atau saat masih melaju.** Pemain tetap duduk dengan pesan yang tepat, atau kakinya mendarat di permukaan tanah yang benar. Dipaku oleh:
   - `DismountFinderTests.*` (Task 6);
   - `MotorbikeTests.Dismount_WhenFast_*` dan `Dismount_NoRoom_*` (Task 8).

---

## Prosedur standar

Workspace ledger (diabaikan git): `D:\UNITY\project\Tyco World\.superpowers\sdd\2026-10-14-motorbike\`.

### Procedure R+T — refresh, compile, dan jalankan tes

Panggil `mcp__unity-mcp__Unity_RunCommand` dengan kode di bawah. Ganti `Group` (regex nama tes; `"."` = semua).

```csharp
using UnityEditor;

internal class CommandScript : IRunCommand
{
    const string Group = "VehiclesSmokeTests";
    const string ResultPath = "D:/UNITY/project/Tyco World/.superpowers/sdd/2026-10-14-motorbike/last-test.txt";

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
- "Could not find type … RunCommandMacroEvaluatorEntryPoint": ulangi.
- MCP pindah ke background (>2 menit): tunggu notifikasinya. Hasil juga ada di `last-test.txt`.
- Error/warning `[Player]`, `[DayCycle]`, `[WorldGen]`, `[Settings]`, `[Vehicle]` di payload berasal dari tes yang sengaja gagal (ditangkap `LogAssert`). Yang menentukan adalah baris `group=… failed=0`.
- Pipe MCP hilang (Unity crash): berhenti dan laporkan; jangan menjalankan ulang Unity.

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

Keluar Play: isi `Execute` diganti menjadi `UnityEngine.Application.runInBackground = false; EditorApplication.isPlaying = false; result.Log("exit");`.
- Tanpa `runInBackground`, frame Play tidak maju selama editor tidak fokus.
- Nilai yang di-commit adalah mati, jadi snippet keluar mematikannya lagi.

### Procedure C — commit

```bash
git -C "D:/UNITY/project/Tyco World" add <path...>
git -C "D:/UNITY/project/Tyco World" commit -m "<subject>

Co-Authored-By: <model> <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

- Ikutkan `.meta` (cukup add foldernya).
- Jangan pernah add ProjectSettings.

### Catatan snippet MCP

- Bentuknya wajib `internal class CommandScript : IRunCommand`.
- Tanpa `System.Reflection`.
- Format angka dengan `ToString("F2", CultureInfo.InvariantCulture)`.

---

## Peta file

```
Assets/Vehicles/Runtime/TycoWorld.Vehicles.asmdef                                              (Task 1)
Assets/Vehicles/Runtime/Input/VehicleIntent.cs, IVehicleIntentSource.cs,
    ScriptedVehicleIntentSource.cs, VehicleInputReader.cs                                      (Task 1)
Assets/Vehicles/Editor/TycoWorld.Vehicles.Editor.asmdef, VehiclePaths.cs, VehicleInputActions.cs (Task 1)
Assets/Vehicles/Tests/TycoWorld.Vehicles.Tests.asmdef, VehiclesSmokeTests.cs,
    ScriptedVehicleIntentSourceTests.cs, VehicleInputActionsTests.cs, VehicleInputReaderTests.cs (Task 1)
Assets/InputSystem_Actions.inputactions, Assets/WorldGen/Tests/McpTestRunner.cs  (ubah, Task 1)
Assets/Vehicles/Runtime/Motion/BikeTuning.cs, BikeModel.cs; Tests/BikeModelTests.cs            (Task 2)
Assets/Player/Runtime/Core/FirstPersonPlayer.cs, Equipment/Flashlight.cs        (ubah, Task 3)
Assets/Player/Runtime/UI/HudModel.cs, HudView.cs                                 (ubah, Task 4)
Assets/Vehicles/Runtime/Camera/RideCameraMode.cs, RideCameraMath.cs, RideCamera.cs;
    Tests/VehicleFixture.cs, RideCameraTests.cs                                                (Task 5)
Assets/Vehicles/Runtime/Motion/DismountFinder.cs; Tests/DismountFinderTests.cs                 (Task 6)
Assets/Vehicles/Runtime/Visuals/RuntimeEmission.cs, MotorbikeVisuals.cs;
    Runtime/Audio/EngineSound.cs, MotorbikeAudio.cs; Tests/RecordingSounds.cs,
    MotorbikeVisualsTests.cs, MotorbikeAudioTests.cs                                           (Task 7)
Assets/Vehicles/Runtime/Motorbike.cs, MotorbikeInteractable.cs;
    Tests/VehicleFixture.Bikes.cs, MotorbikeTests.cs                                           (Task 8)
Assets/Vehicles/Editor/EngineSynth.cs, VehicleAudioBuilder.cs, VehicleAudioImportRules.cs;
    Assets/Vehicles/Audio/* + SOURCES.md; Tests/EngineSynthTests.cs, VehicleAudioAssetsTests.cs (Task 9)
Assets/Player/Editor/PlaceholderPropFactory.cs                                   (ubah, Task 10)
Assets/Vehicles/Editor/VehicleMaterials.cs, MotorbikeBuilder.cs, VehicleAssetsBuilder.cs;
    Assets/Vehicles/{Config,Materials,Prefabs}/*;
    Tests/MotorbikeBuilderTests.cs, VehicleAssetsTests.cs                                      (Task 10)
Assets/Vehicles/Editor/VehicleSceneSetup.cs; Tests/VehicleSetupTests.cs;
    Assets/Scenes/World.unity (ubah)                                                           (Task 11)
Assets/GameSettings/Runtime/TycoWorld.GameSettings.asmdef, SettingsApplier.cs,
    Audio/EffectsVolume.cs; GameSettings Tests asmdef + 2 tes                      (ubah, Task 12)
Assets/Vehicles/Tests/McpVehicleDriver.cs;
    docs/superpowers/notes/2026-10-14-motorbike-verification.md                                (Task 13)
```

---

### Task 1: Scaffold assembly Vehicles, action map "Vehicle", dan sumber intent

**Files:**
- Create: `Assets/Vehicles/Runtime/TycoWorld.Vehicles.asmdef`
- Create: `Assets/Vehicles/Runtime/Input/VehicleIntent.cs`, `IVehicleIntentSource.cs`, `ScriptedVehicleIntentSource.cs`, `VehicleInputReader.cs`
- Create: `Assets/Vehicles/Editor/TycoWorld.Vehicles.Editor.asmdef`, `VehiclePaths.cs`, `VehicleInputActions.cs`
- Create: `Assets/Vehicles/Tests/TycoWorld.Vehicles.Tests.asmdef`, `VehiclesSmokeTests.cs`, `ScriptedVehicleIntentSourceTests.cs`, `VehicleInputActionsTests.cs`, `VehicleInputReaderTests.cs`
- Modify: `Assets/WorldGen/Tests/McpTestRunner.cs` (daftar `assemblyNames`)
- Modify (lewat `VehicleInputActions.Ensure`): `Assets/InputSystem_Actions.inputactions`

**Interfaces:**
- Produces:
  - `struct VehicleIntent { float Throttle; float Steer; bool Brake; bool Horn; bool ToggleHeadlight; bool ToggleCamera; bool Exit; Vector2 LookDegrees; }`.
  - `interface IVehicleIntentSource { VehicleIntent Read(float dt); }`.
  - `ScriptedVehicleIntentSource`:
    - `Hold(VehicleIntent, float seconds)` — Throttle/Steer/Brake ditahan; LookDegrees dibaca sebagai derajat per detik;
    - `Press(VehicleIntent)` — Horn/ToggleHeadlight/ToggleCamera/Exit + delta look instan pada Read berikutnya;
    - `Clear()` dan `IsDone`.
  - `VehicleInputReader(InputActionAsset asset, float degreesPerPixel)`:
    - `const string MapName = "Vehicle"`;
    - `CursorLocked`, `Enable()`, `Disable()`, `IsEnabled`, `Read(dt)`.
  - Editor:
    - `VehiclePaths` (Root, ConfigFolder, TuningAsset, MaterialsFolder, PrefabsFolder, MeshesFolder, MotorbikePrefab, AudioFolder, AudioSourcesFile);
    - `VehicleInputActions.Ensure(string assetPath) → bool`.

- [ ] **Step 1: Buat workspace ledger**

```bash
mkdir -p "/d/UNITY/project/Tyco World/.superpowers/sdd/2026-10-14-motorbike"
git -C "/d/UNITY/project/Tyco World" check-ignore -q .superpowers/sdd && echo ignored
```
Expected: `ignored`.

- [ ] **Step 2: Tulis tiga asmdef**

`Assets/Vehicles/Runtime/TycoWorld.Vehicles.asmdef`:
```json
{
    "name": "TycoWorld.Vehicles",
    "rootNamespace": "TycoWorld.Vehicles",
    "references": [
        "TycoWorld.Player",
        "TycoWorld.WorldGen",
        "Unity.InputSystem"
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

`Assets/Vehicles/Editor/TycoWorld.Vehicles.Editor.asmdef`:
```json
{
    "name": "TycoWorld.Vehicles.Editor",
    "rootNamespace": "TycoWorld.Vehicles.Authoring",
    "references": [
        "TycoWorld.Vehicles",
        "TycoWorld.Player",
        "TycoWorld.Player.Editor",
        "TycoWorld.WorldGen",
        "TycoWorld.WorldGen.Editor",
        "TycoWorld.DayCycle.Editor",
        "Unity.InputSystem"
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

`Assets/Vehicles/Tests/TycoWorld.Vehicles.Tests.asmdef`:
```json
{
    "name": "TycoWorld.Vehicles.Tests",
    "rootNamespace": "TycoWorld.Vehicles.Tests",
    "references": [
        "TycoWorld.Vehicles",
        "TycoWorld.Vehicles.Editor",
        "TycoWorld.Player",
        "TycoWorld.Player.Editor",
        "TycoWorld.Player.Tests",
        "TycoWorld.WorldGen",
        "TycoWorld.WorldGen.Editor",
        "TycoWorld.DayCycle",
        "TycoWorld.DayCycle.Editor",
        "TycoWorld.GameSettings",
        "TycoWorld.GameSettings.Tests",
        "Unity.InputSystem",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
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
Catatan: `TycoWorld.Player.Tests` dipakai untuk `McpRender`, dan `TycoWorld.GameSettings.Tests` untuk `McpPerfProbe`/`McpGameDriver` (Task 13).

- [ ] **Step 3: Tambahkan assembly tes baru ke runner**

Di `Assets/WorldGen/Tests/McpTestRunner.cs`, ganti baris:
```csharp
                    assemblyNames = new[] { "TycoWorld.WorldGen.Tests", "TycoWorld.Player.Tests", "TycoWorld.DayCycle.Tests", "TycoWorld.GameSettings.Tests" },
```
dengan:
```csharp
                    assemblyNames = new[] { "TycoWorld.WorldGen.Tests", "TycoWorld.Player.Tests", "TycoWorld.DayCycle.Tests", "TycoWorld.GameSettings.Tests", "TycoWorld.Vehicles.Tests" },
```

- [ ] **Step 4: Tulis tes yang gagal**

`Assets/Vehicles/Tests/VehiclesSmokeTests.cs`:
```csharp
using NUnit.Framework;
using TycoWorld.Vehicles.Authoring;

namespace TycoWorld.Vehicles.Tests
{
    public class VehiclesSmokeTests
    {
        [Test]
        public void Assemblies_Load()
        {
            Assert.AreEqual("TycoWorld.Vehicles", typeof(VehicleIntent).Assembly.GetName().Name);
            Assert.AreEqual("TycoWorld.Vehicles.Editor", typeof(VehiclePaths).Assembly.GetName().Name);
        }
    }
}
```

`Assets/Vehicles/Tests/ScriptedVehicleIntentSourceTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.Vehicles.Tests
{
    public class ScriptedVehicleIntentSourceTests
    {
        [Test]
        public void Hold_RepeatsForItsDuration_ThenIdles()
        {
            var source = new ScriptedVehicleIntentSource();
            source.Hold(new VehicleIntent { Throttle = 1f, Steer = -0.5f, Brake = true, LookDegrees = new Vector2(10f, 0f) }, 0.5f);
            var first = source.Read(0.25f);
            Assert.AreEqual(1f, first.Throttle);
            Assert.AreEqual(-0.5f, first.Steer);
            Assert.IsTrue(first.Brake);
            Assert.AreEqual(2.5f, first.LookDegrees.x, 1e-5f, "derajat per detik × dt");
            source.Read(0.25f);
            Assert.IsTrue(source.IsDone);
            Assert.AreEqual(0f, source.Read(0.25f).Throttle);
        }

        [Test]
        public void Press_FiresEdgesOnce()
        {
            var source = new ScriptedVehicleIntentSource();
            source.Press(new VehicleIntent { Horn = true, ToggleHeadlight = true, ToggleCamera = true, Exit = true, LookDegrees = new Vector2(0f, 5f) });
            var once = source.Read(0.02f);
            Assert.IsTrue(once.Horn && once.ToggleHeadlight && once.ToggleCamera && once.Exit);
            Assert.AreEqual(5f, once.LookDegrees.y, 1e-5f);
            var next = source.Read(0.02f);
            Assert.IsFalse(next.Horn || next.ToggleHeadlight || next.ToggleCamera || next.Exit);
            Assert.IsTrue(source.IsDone);
        }

        [Test]
        public void Clear_DropsEverything()
        {
            var source = new ScriptedVehicleIntentSource();
            source.Hold(new VehicleIntent { Throttle = 1f }, 5f);
            source.Press(new VehicleIntent { Exit = true });
            source.Clear();
            Assert.IsTrue(source.IsDone);
            Assert.IsFalse(source.Read(0.1f).Exit);
        }
    }
}
```

`Assets/Vehicles/Tests/VehicleInputActionsTests.cs`:
```csharp
using System.Linq;
using NUnit.Framework;
using TycoWorld.Player.Authoring;
using TycoWorld.Vehicles.Authoring;
using UnityEditor;
using UnityEngine.InputSystem;

namespace TycoWorld.Vehicles.Tests
{
    public class VehicleInputActionsTests
    {
        static InputActionAsset Load()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(PlayerPaths.InputActionsAsset);
            Assert.IsNotNull(asset);
            return asset;
        }

        static bool HasPath(InputAction action, string path) => action.bindings.Any(b => b.path == path);

        [Test]
        public void VehicleMap_HasEveryAction()
        {
            var map = Load().FindActionMap(VehicleInputReader.MapName, true);
            foreach (var name in new[] { "Throttle", "Steer", "Brake", "Horn", "Headlight", "Camera", "Exit", "Look" })
                Assert.IsNotNull(map.FindAction(name), name);
        }

        [Test]
        public void KeyboardBindings_MatchTheSpec()
        {
            var map = Load().FindActionMap(VehicleInputReader.MapName, true);
            Assert.IsTrue(HasPath(map.FindAction("Throttle"), "<Keyboard>/w"));
            Assert.IsTrue(HasPath(map.FindAction("Throttle"), "<Keyboard>/s"));
            Assert.IsTrue(HasPath(map.FindAction("Steer"), "<Keyboard>/a"));
            Assert.IsTrue(HasPath(map.FindAction("Steer"), "<Keyboard>/d"));
            Assert.IsTrue(HasPath(map.FindAction("Brake"), "<Keyboard>/space"));
            Assert.IsTrue(HasPath(map.FindAction("Horn"), "<Keyboard>/h"));
            Assert.IsTrue(HasPath(map.FindAction("Headlight"), "<Keyboard>/f"));
            Assert.IsTrue(HasPath(map.FindAction("Camera"), "<Keyboard>/v"));
            Assert.IsTrue(HasPath(map.FindAction("Exit"), "<Keyboard>/e"));
            Assert.IsTrue(HasPath(map.FindAction("Look"), "<Pointer>/delta"));
            Assert.AreEqual(InputActionType.Button, map.FindAction("Exit").type);
            Assert.AreEqual(InputActionType.Value, map.FindAction("Throttle").type);
        }

        [Test]
        public void PlayerMap_IsUntouched()
        {
            var player = Load().FindActionMap("Player", true);
            foreach (var name in new[] { "Move", "Look", "Sprint", "Jump", "Crouch", "Interact", "Flashlight", "Attack" })
                Assert.IsNotNull(player.FindAction(name), name);
            Assert.IsTrue(HasPath(player.FindAction("Interact"), "<Keyboard>/e"));
            Assert.IsNotNull(Load().FindActionMap("UI"));
        }

        [Test]
        public void Ensure_IsIdempotent() => Assert.IsFalse(VehicleInputActions.Ensure(PlayerPaths.InputActionsAsset));
    }
}
```

`Assets/Vehicles/Tests/VehicleInputReaderTests.cs`:
```csharp
using NUnit.Framework;
using TycoWorld.Player.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TycoWorld.Vehicles.Tests
{
    public class VehicleInputReaderTests
    {
        [Test]
        public void Constructor_FindsActions_IdleReadIsEmpty()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(PlayerPaths.InputActionsAsset);
            var reader = new VehicleInputReader(asset, 0.1f);
            Assert.IsFalse(reader.IsEnabled);
            var intent = reader.Read(0.016f);
            Assert.AreEqual(0f, intent.Throttle);
            Assert.AreEqual(0f, intent.Steer);
            Assert.AreEqual(Vector2.zero, intent.LookDegrees);
            Assert.IsFalse(intent.Brake || intent.Horn || intent.ToggleHeadlight || intent.ToggleCamera || intent.Exit);
        }
    }
}
```

- [ ] **Step 5: Jalankan tes, pastikan gagal**

Procedure R+T, `Group = "VehiclesSmokeTests|ScriptedVehicleIntentSourceTests|VehicleInputActionsTests|VehicleInputReaderTests"`.
Expected: `COMPILE FAILED` (tipe Vehicles belum ada).

- [ ] **Step 6: Implementasi runtime**

`Assets/Vehicles/Runtime/Input/VehicleIntent.cs`:
```csharp
using UnityEngine;

namespace TycoWorld.Vehicles
{
    /// <summary>One frame of rider input (spec #5 §5). Throttle +1 = gas (W), -1 = brake/reverse (S); Steer +1 = right (D).</summary>
    public struct VehicleIntent
    {
        public float Throttle;
        public float Steer;
        public bool Brake;
        public bool Horn;
        public bool ToggleHeadlight;
        public bool ToggleCamera;
        public bool Exit;
        public Vector2 LookDegrees;
    }
}
```

`Assets/Vehicles/Runtime/Input/IVehicleIntentSource.cs`:
```csharp
namespace TycoWorld.Vehicles
{
    /// <summary>Where a ridden bike's input comes from: the keyboard/gamepad reader or a scripted source (tests, MCP).</summary>
    public interface IVehicleIntentSource
    {
        VehicleIntent Read(float dt);
    }
}
```

`Assets/Vehicles/Runtime/Input/ScriptedVehicleIntentSource.cs`:
```csharp
using System.Collections.Generic;

namespace TycoWorld.Vehicles
{
    /// <summary>Replays held inputs and one-shot presses for tests and MCP verification (same shape as the player's scripted source).</summary>
    public sealed class ScriptedVehicleIntentSource : IVehicleIntentSource
    {
        struct Segment
        {
            public VehicleIntent Intent;
            public float Remaining;
        }

        readonly List<Segment> segments = new List<Segment>();
        VehicleIntent edges;
        bool hasEdges;

        public bool IsDone => segments.Count == 0 && !hasEdges;

        /// <summary>Holds Throttle, Steer and Brake for <paramref name="seconds"/>; LookDegrees is read as degrees per second. Edge flags are ignored.</summary>
        public void Hold(VehicleIntent held, float seconds)
        {
            if (seconds > 0f) segments.Add(new Segment { Intent = held, Remaining = seconds });
        }

        /// <summary>Fires Horn, ToggleHeadlight, ToggleCamera, Exit and an instant look delta on the next Read.</summary>
        public void Press(VehicleIntent pressed)
        {
            edges.Horn |= pressed.Horn;
            edges.ToggleHeadlight |= pressed.ToggleHeadlight;
            edges.ToggleCamera |= pressed.ToggleCamera;
            edges.Exit |= pressed.Exit;
            edges.LookDegrees += pressed.LookDegrees;
            hasEdges = true;
        }

        public void Clear()
        {
            segments.Clear();
            edges = default;
            hasEdges = false;
        }

        public VehicleIntent Read(float dt)
        {
            var result = new VehicleIntent();
            if (segments.Count > 0)
            {
                var segment = segments[0];
                result.Throttle = segment.Intent.Throttle;
                result.Steer = segment.Intent.Steer;
                result.Brake = segment.Intent.Brake;
                result.LookDegrees = segment.Intent.LookDegrees * dt;
                segment.Remaining -= dt;
                if (segment.Remaining <= 0f) segments.RemoveAt(0);
                else segments[0] = segment;
            }
            if (hasEdges)
            {
                result.Horn = edges.Horn;
                result.ToggleHeadlight = edges.ToggleHeadlight;
                result.ToggleCamera = edges.ToggleCamera;
                result.Exit = edges.Exit;
                result.LookDegrees += edges.LookDegrees;
                edges = default;
                hasEdges = false;
            }
            return result;
        }
    }
}
```

`Assets/Vehicles/Runtime/Input/VehicleInputReader.cs`:
```csharp
using UnityEngine;
using UnityEngine.InputSystem;

namespace TycoWorld.Vehicles
{
    /// <summary>Reads the project-wide "Vehicle" action map (spec #5 §5); the map is only enabled while a bike is ridden.</summary>
    public sealed class VehicleInputReader : IVehicleIntentSource
    {
        public const string MapName = "Vehicle";

        readonly InputActionMap map;
        readonly InputAction throttle, steer, brake, horn, headlight, cameraToggle, exit, look;
        readonly float degreesPerPixel;

        public VehicleInputReader(InputActionAsset asset, float degreesPerPixel)
        {
            map = asset.FindActionMap(MapName, true);
            throttle = map.FindAction("Throttle", true);
            steer = map.FindAction("Steer", true);
            brake = map.FindAction("Brake", true);
            horn = map.FindAction("Horn", true);
            headlight = map.FindAction("Headlight", true);
            cameraToggle = map.FindAction("Camera", true);
            exit = map.FindAction("Exit", true);
            look = map.FindAction("Look", true);
            this.degreesPerPixel = degreesPerPixel;
        }

        /// <summary>Mouse look only applies while the cursor is locked (pause menu, alt-tab).</summary>
        public bool CursorLocked { get; set; } = true;

        public bool IsEnabled => map.enabled;

        public void Enable() => map.Enable();

        public void Disable() => map.Disable();

        public VehicleIntent Read(float dt) => new VehicleIntent
        {
            Throttle = Mathf.Clamp(throttle.ReadValue<float>(), -1f, 1f),
            Steer = Mathf.Clamp(steer.ReadValue<float>(), -1f, 1f),
            Brake = brake.IsPressed(),
            Horn = horn.WasPressedThisFrame(),
            ToggleHeadlight = headlight.WasPressedThisFrame(),
            ToggleCamera = cameraToggle.WasPressedThisFrame(),
            Exit = exit.WasPressedThisFrame(),
            LookDegrees = CursorLocked ? look.ReadValue<Vector2>() * degreesPerPixel : Vector2.zero
        };
    }
}
```

- [ ] **Step 7: Implementasi editor**

`Assets/Vehicles/Editor/VehiclePaths.cs`:
```csharp
namespace TycoWorld.Vehicles.Authoring
{
    /// <summary>Project-relative paths of the vehicle sub-project (spec #5).</summary>
    public static class VehiclePaths
    {
        public const string Root = "Assets/Vehicles";
        public const string ConfigFolder = Root + "/Config";
        public const string TuningAsset = ConfigFolder + "/BikeTuning.asset";
        public const string MaterialsFolder = Root + "/Materials";
        public const string PrefabsFolder = Root + "/Prefabs";
        public const string MeshesFolder = PrefabsFolder + "/Meshes";
        public const string MotorbikePrefab = PrefabsFolder + "/Motorbike.prefab";
        public const string AudioFolder = Root + "/Audio";
        public const string AudioSourcesFile = AudioFolder + "/SOURCES.md";
    }
}
```

`Assets/Vehicles/Editor/VehicleInputActions.cs`:
```csharp
using System.IO;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TycoWorld.Vehicles.Authoring
{
    /// <summary>Adds the "Vehicle" action map (spec #5 §5) to the project-wide actions asset once; other maps are left as they are.</summary>
    public static class VehicleInputActions
    {
        public const string KeyboardGroup = "Keyboard&Mouse";
        public const string GamepadGroup = "Gamepad";

        /// <summary>True when the map was added and the file rewritten; false when it already existed.</summary>
        public static bool Ensure(string assetPath)
        {
            string fullPath = WorldGenPaths.ToAbsolute(assetPath);
            var asset = InputActionAsset.FromJson(File.ReadAllText(fullPath));
            try
            {
                if (asset.FindActionMap(VehicleInputReader.MapName) != null) return false;
                var map = asset.AddActionMap(VehicleInputReader.MapName);
                Axis(map, "Throttle", "<Keyboard>/w", "<Keyboard>/s", "<Keyboard>/upArrow", "<Keyboard>/downArrow");
                map.FindAction("Throttle").AddCompositeBinding("1DAxis")
                    .With("Positive", "<Gamepad>/rightTrigger", GamepadGroup)
                    .With("Negative", "<Gamepad>/leftTrigger", GamepadGroup);
                Axis(map, "Steer", "<Keyboard>/d", "<Keyboard>/a", "<Keyboard>/rightArrow", "<Keyboard>/leftArrow");
                map.FindAction("Steer").AddBinding("<Gamepad>/leftStick/x", groups: GamepadGroup);
                Button(map, "Brake", "<Keyboard>/space", "<Gamepad>/buttonSouth");
                Button(map, "Horn", "<Keyboard>/h", "<Gamepad>/leftStickPress");
                Button(map, "Headlight", "<Keyboard>/f", "<Gamepad>/dpad/up");
                Button(map, "Camera", "<Keyboard>/v", "<Gamepad>/buttonNorth");
                Button(map, "Exit", "<Keyboard>/e", "<Gamepad>/buttonWest");
                var look = map.AddAction("Look", InputActionType.Value, expectedControlLayout: "Vector2");
                look.AddBinding("<Pointer>/delta", groups: KeyboardGroup);
                look.AddBinding("<Gamepad>/rightStick", groups: GamepadGroup);
                File.WriteAllText(fullPath, asset.ToJson());
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            return true;
        }

        static void Axis(InputActionMap map, string name, string positive, string negative, string positiveAlt, string negativeAlt)
        {
            var action = map.AddAction(name, InputActionType.Value, expectedControlLayout: "Axis");
            action.AddCompositeBinding("1DAxis").With("Positive", positive, KeyboardGroup).With("Negative", negative, KeyboardGroup);
            action.AddCompositeBinding("1DAxis").With("Positive", positiveAlt, KeyboardGroup).With("Negative", negativeAlt, KeyboardGroup);
        }

        static void Button(InputActionMap map, string name, string key, string pad)
        {
            var action = map.AddAction(name, InputActionType.Button);
            action.AddBinding(key, groups: KeyboardGroup);
            action.AddBinding(pad, groups: GamepadGroup);
        }
    }
}
```

- [ ] **Step 8: Jalankan tes — map belum ada**

Procedure R+T, Group yang sama.
Expected: compile berhasil. `VehicleInputActionsTests.VehicleMap_HasEveryAction`, `KeyboardBindings_MatchTheSpec`, dan `Ensure_IsIdempotent` **gagal** karena map belum ditambahkan. `VehicleInputReaderTests` juga gagal (`FindActionMap` throw). Tes lain lulus.

- [ ] **Step 9: Tambahkan map Vehicle (MCP, sekali)**

```csharp
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        bool added = TycoWorld.Vehicles.Authoring.VehicleInputActions.Ensure(TycoWorld.Player.Authoring.PlayerPaths.InputActionsAsset);
        result.Log("vehicleMapAdded=" + added);
    }
}
```
Expected: `vehicleMapAdded=True`. Lalu cek perubahan file:
```bash
git -C "D:/UNITY/project/Tyco World" diff --stat -- Assets/InputSystem_Actions.inputactions
```
Isi map Player dan UI harus tetap sama secara makna. Kalau `ToJson` menulis ulang format seluruh file (indentasi atau urutan field), terima saja dan catat jumlah barisnya di laporan. Jangan edit JSON dengan tangan.

- [ ] **Step 10: Jalankan tes, pastikan lulus**

Procedure R+T, Group yang sama.
Expected: `passed=9 failed=0` (smoke 1 + scripted 3 + actions 4 + reader 1).

Lalu jalankan regresi Player (`Group = "TycoWorld.Player.Tests"`).
Expected: `failed=0`. `InputActionsAssetTests` harus tetap lulus.

- [ ] **Step 11: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add Assets/Vehicles Assets/Vehicles.meta Assets/WorldGen/Tests/McpTestRunner.cs Assets/InputSystem_Actions.inputactions
git -C "D:/UNITY/project/Tyco World" commit -m "feat(vehicles): scaffold vehicle assemblies, Vehicle action map and intent sources

Co-Authored-By: <model> <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

---

### Task 2: `BikeTuning` dan `BikeModel` (fisika arcade)

**Files:**
- Create: `Assets/Vehicles/Runtime/Motion/BikeTuning.cs`
- Create: `Assets/Vehicles/Runtime/Motion/BikeModel.cs`
- Test: `Assets/Vehicles/Tests/BikeModelTests.cs`

**Interfaces:**
- Consumes: `VehicleIntent` (Task 1).
- Produces:
  - `BikeTuning : ScriptableObject` dengan field publik huruf kecil (lihat kode; semua nilai dari spec §4) dan `const float KmhPerMs = 3.6f`.
  - `BikeModel(BikeTuning)`:
    - `Speed` (m/s bertanda, + maju), `SpeedKmh`, `Lean` (derajat, + = miring kanan), `Braking`;
    - `Step(VehicleIntent, float dt)`, `YawRate(float steer) → float` (derajat/detik, bertanda);
    - `Reset()`, `Stop()`, `Scale(float factor)`.

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/Vehicles/Tests/BikeModelTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.Vehicles.Tests
{
    public class BikeModelTests
    {
        const float Dt = 0.02f;
        BikeTuning tuning;
        BikeModel model;

        static VehicleIntent Gas => new VehicleIntent { Throttle = 1f };
        static VehicleIntent Back => new VehicleIntent { Throttle = -1f };

        [SetUp]
        public void SetUp()
        {
            tuning = ScriptableObject.CreateInstance<BikeTuning>();
            model = new BikeModel(tuning);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(tuning);

        void Run(BikeModel m, VehicleIntent intent, float seconds)
        {
            int frames = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < frames; i++) m.Step(intent, Dt);
        }

        float TimeToStop(BikeModel m, VehicleIntent intent)
        {
            float time = 0f;
            while (m.Speed > tuning.reverseThreshold && time < 10f)
            {
                m.Step(intent, Dt);
                time += Dt;
            }
            return time;
        }

        [Test]
        public void Defaults_MatchTheSpec()
        {
            Assert.AreEqual(16.67f, tuning.maxForwardSpeed, 1e-3f);
            Assert.AreEqual(2.5f, tuning.maxReverseSpeed, 1e-3f);
            Assert.AreEqual(4f, tuning.accelAtRest);
            Assert.AreEqual(1.5f, tuning.accelAtMax);
            Assert.AreEqual(7f, tuning.brakeDecel);
            Assert.AreEqual(10f, tuning.hardBrakeDecel);
            Assert.AreEqual(0.8f, tuning.coastDecel, 1e-5f);
            Assert.AreEqual(90f, tuning.yawRateLow);
            Assert.AreEqual(35f, tuning.yawRateHigh);
            Assert.AreEqual(25f, tuning.maxLean);
            Assert.AreEqual(1.4f, tuning.dismountMaxSpeed, 1e-5f);
            Assert.AreEqual(4.5f, tuning.chaseDistance, 1e-5f);
            Assert.AreEqual(30f, tuning.headlightRange);
            Assert.AreEqual(0.8f, tuning.idlePitch, 1e-5f);
            Assert.AreEqual(2f, tuning.maxPitch);
        }

        [Test]
        public void TopSpeed_IsCappedAt60Kmh()
        {
            Run(model, Gas, 20f);
            Assert.AreEqual(60f, model.SpeedKmh, 0.1f);
        }

        [Test]
        public void ZeroTo40_TakesAbout4Seconds()
        {
            float time = 0f;
            while (model.SpeedKmh < 40f && time < 10f)
            {
                model.Step(Gas, Dt);
                time += Dt;
            }
            Assert.That(time, Is.InRange(3.5f, 4.5f));
        }

        [Test]
        public void S_From60_BrakesToAStopWithin2Point5Seconds()
        {
            Run(model, Gas, 20f);
            model.Step(Back, Dt);
            Assert.IsTrue(model.Braking);
            Assert.Less(TimeToStop(model, Back), 2.5f);
        }

        [Test]
        public void Space_BrakesHarderThanS()
        {
            var other = new BikeModel(tuning);
            Run(model, Gas, 20f);
            Run(other, Gas, 20f);
            float withS = TimeToStop(model, Back);
            float withSpace = TimeToStop(other, new VehicleIntent { Brake = true });
            Assert.Less(withSpace, withS);
        }

        [Test]
        public void Coasting_SlowsDownGently()
        {
            Run(model, Gas, 20f);
            float before = model.Speed;
            Run(model, default, 1f);
            Assert.AreEqual(before - 0.8f, model.Speed, 0.02f);
            Assert.IsFalse(model.Braking);
        }

        [Test]
        public void S_AtStandstill_ReversesUpTo9Kmh()
        {
            Run(model, Back, 5f);
            Assert.AreEqual(-9f, model.SpeedKmh, 0.1f);
            Assert.IsFalse(model.Braking);
        }

        [Test]
        public void Gas_WhileReversing_BrakesFirst()
        {
            Run(model, Back, 3f);
            model.Step(Gas, Dt);
            Assert.IsTrue(model.Braking);
            Assert.Less(model.Speed, 0f);
        }

        [Test]
        public void YawRate_IsZeroWhenStopped() => Assert.AreEqual(0f, model.YawRate(1f));

        [Test]
        public void YawRate_FollowsSpeed()
        {
            Run(model, Gas, 1f);
            float v = model.Speed;
            Assert.Greater(v, tuning.turnInSpeed);
            Assert.AreEqual(Mathf.Lerp(90f, 35f, v / tuning.maxForwardSpeed), model.YawRate(1f), 0.01f);
            Run(model, Gas, 20f);
            Assert.AreEqual(35f, model.YawRate(1f), 0.01f);
            Assert.AreEqual(-35f, model.YawRate(-1f), 0.01f);
        }

        [Test]
        public void YawRate_ScalesInBelowTurnInSpeed()
        {
            Run(model, Gas, 0.2f);
            float v = model.Speed;
            Assert.Less(v, tuning.turnInSpeed);
            float expected = Mathf.Lerp(90f, 35f, v / tuning.maxForwardSpeed) * (v / tuning.turnInSpeed);
            Assert.AreEqual(expected, model.YawRate(1f), 0.01f);
        }

        [Test]
        public void YawRate_FlipsWhenReversing()
        {
            Run(model, Back, 3f);
            Assert.Less(model.YawRate(1f), 0f);
        }

        [Test]
        public void Lean_FollowsSteerAtSpeed_ButNotAtStandstill()
        {
            Run(model, new VehicleIntent { Throttle = 1f, Steer = 1f }, 4f);
            Assert.AreEqual(25f, model.Lean, 0.5f);
            model.Reset();
            Run(model, new VehicleIntent { Steer = 1f }, 1f);
            Assert.AreEqual(0f, model.Lean, 1e-3f);
        }

        [Test]
        public void StopAndScale_AdjustSpeed()
        {
            Run(model, Gas, 3f);
            float v = model.Speed;
            model.Scale(0.5f);
            Assert.AreEqual(v * 0.5f, model.Speed, 1e-4f);
            model.Stop();
            Assert.AreEqual(0f, model.Speed);
        }

        [Test]
        public void ZeroDt_ChangesNothing()
        {
            Run(model, Gas, 1f);
            float v = model.Speed;
            model.Step(Gas, 0f);
            Assert.AreEqual(v, model.Speed);
        }
    }
}
```

- [ ] **Step 2: Jalankan tes, pastikan gagal**

Procedure R+T, `Group = "BikeModelTests"`.
Expected: `COMPILE FAILED` (`BikeTuning` belum ada).

- [ ] **Step 3: Implementasi**

`Assets/Vehicles/Runtime/Motion/BikeTuning.cs`:
```csharp
using UnityEngine;

namespace TycoWorld.Vehicles
{
    /// <summary>Every tunable number of the motorbike (spec #5 §4). Defaults live only here.</summary>
    public sealed class BikeTuning : ScriptableObject
    {
        public const float KmhPerMs = 3.6f;

        [Header("Speed")]
        public float maxForwardSpeed = 16.67f;
        public float maxReverseSpeed = 2.5f;

        [Header("Throttle and brakes")]
        public float accelAtRest = 4f;
        public float accelAtMax = 1.5f;
        public float brakeDecel = 7f;
        public float hardBrakeDecel = 10f;
        public float coastDecel = 0.8f;
        public float reverseThreshold = 0.3f;
        public float reverseAccel = 2f;

        [Header("Steering")]
        public float yawRateLow = 90f;
        public float yawRateHigh = 35f;
        public float turnInSpeed = 1.5f;
        public float maxSteerAngle = 25f;

        [Header("Lean (visual only)")]
        public float maxLean = 25f;
        public float fullLeanSpeed = 8f;
        public float leanSmoothing = 6f;

        [Header("Body")]
        public float gravity = 20f;
        public float slopeLimit = 35f;
        public float stepOffset = 0.3f;
        public float bodyRadius = 0.5f;
        public float bodyHeight = 1.4f;
        public Vector3 blockHalfExtents = new Vector3(0.35f, 0.5f, 0.95f);
        public float headOnDot = 0.5f;
        public float bumpSpeed = 2.8f;
        public float glanceSpeedFactor = 0.8f;
        public float respawnBelowY = -20f;

        [Header("Getting off")]
        public float dismountMaxSpeed = 1.4f;
        public float dismountSideOffset = 1.1f;
        public float exitGuardSeconds = 0.3f;

        [Header("Third-person camera")]
        public float chaseDistance = 4.5f;
        public float chaseHeight = 1.9f;
        public float lookHeight = 1.2f;
        public float orbitYawLimit = 120f;
        public float orbitPitchMin = -10f;
        public float orbitPitchMax = 35f;
        public float returnDelay = 1.5f;
        public float returnMinSpeed = 2f;
        public float returnSpeed = 3f;
        public float followSmoothing = 5f;
        public float cameraRadius = 0.25f;

        [Header("First-person camera")]
        public float headHeight = 0.75f;
        public float headBack = 0.1f;
        public float lookYawLimit = 100f;
        public float lookPitchLimit = 60f;
        public float rollFactor = 0.5f;

        [Header("Headlight")]
        public float headlightRange = 30f;
        public float headlightAngle = 60f;
        public float headlightIntensity = 4f;

        [Header("Engine sound")]
        public float idlePitch = 0.8f;
        public float maxPitch = 2f;
        public float throttlePitch = 0.1f;
        public float idleVolume = 0.6f;
        public float maxVolume = 0.9f;
        public float hornCooldown = 0.4f;
    }
}
```

`Assets/Vehicles/Runtime/Motion/BikeModel.cs`:
```csharp
using UnityEngine;

namespace TycoWorld.Vehicles
{
    /// <summary>Arcade bike motion (spec #5 §4): signed speed, yaw rate and visual lean from one intent per step.</summary>
    public sealed class BikeModel
    {
        const float DeadZone = 0.01f;

        readonly BikeTuning t;

        public BikeModel(BikeTuning tuning) => t = tuning;

        /// <summary>Metres per second; positive is forward.</summary>
        public float Speed { get; private set; }
        public float SpeedKmh => Speed * BikeTuning.KmhPerMs;
        /// <summary>Visual lean in degrees; positive leans right.</summary>
        public float Lean { get; private set; }
        public bool Braking { get; private set; }

        public void Reset()
        {
            Speed = 0f;
            Lean = 0f;
            Braking = false;
        }

        public void Stop() => Speed = 0f;

        public void Scale(float factor) => Speed *= factor;

        public void Step(VehicleIntent intent, float dt)
        {
            if (dt <= 0f) return;
            float throttle = Mathf.Clamp(intent.Throttle, -1f, 1f);
            float v = Speed;
            Braking = false;
            if (intent.Brake)
            {
                Braking = Mathf.Abs(v) > DeadZone;
                v = Mathf.MoveTowards(v, 0f, t.hardBrakeDecel * dt);
            }
            else if (throttle > DeadZone)
            {
                if (v < -DeadZone)
                {
                    Braking = true;
                    v = Mathf.MoveTowards(v, 0f, t.brakeDecel * dt);
                }
                else v = Mathf.Min(t.maxForwardSpeed, v + Acceleration(v) * throttle * dt);
            }
            else if (throttle < -DeadZone)
            {
                if (v > t.reverseThreshold)
                {
                    Braking = true;
                    v = Mathf.MoveTowards(v, 0f, t.brakeDecel * dt);
                }
                else v = Mathf.Max(-t.maxReverseSpeed, v - t.reverseAccel * -throttle * dt);
            }
            else v = Mathf.MoveTowards(v, 0f, t.coastDecel * dt);
            Speed = v;
            float target = Mathf.Clamp(intent.Steer, -1f, 1f) * t.maxLean * Mathf.Clamp01(Mathf.Abs(v) / t.fullLeanSpeed);
            Lean = Mathf.Lerp(Lean, target, 1f - Mathf.Exp(-t.leanSmoothing * dt));
        }

        /// <summary>Degrees per second for <paramref name="steer"/> at the current speed: no turning in place, flipped when reversing.</summary>
        public float YawRate(float steer)
        {
            float speed = Mathf.Abs(Speed);
            if (speed < 1e-4f) return 0f;
            float rate = Mathf.Lerp(t.yawRateLow, t.yawRateHigh, Mathf.Clamp01(speed / t.maxForwardSpeed));
            return Mathf.Clamp(steer, -1f, 1f) * rate * Mathf.Clamp01(speed / t.turnInSpeed) * Mathf.Sign(Speed);
        }

        float Acceleration(float v) => Mathf.Lerp(t.accelAtRest, t.accelAtMax, Mathf.Clamp01(v / t.maxForwardSpeed));
    }
}
```

- [ ] **Step 4: Jalankan tes, pastikan lulus**

Procedure R+T, `Group = "BikeModelTests"`.
Expected: `passed=15 failed=0`.

- [ ] **Step 5: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add Assets/Vehicles
git -C "D:/UNITY/project/Tyco World" commit -m "feat(vehicles): bike tuning and arcade motion model

Co-Authored-By: <model> <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

---

### Task 3: Mode duduk di `FirstPersonPlayer` dan `Flashlight.TurnOff`

**Files:**
- Modify: `Assets/Player/Runtime/Core/FirstPersonPlayer.cs`
- Modify: `Assets/Player/Runtime/Equipment/Flashlight.cs`
- Test: `Assets/Player/Tests/FirstPersonPlayerTests.cs`, `Assets/Player/Tests/FlashlightTests.cs`

**Interfaces:**
- Produces (namespace `TycoWorld.Player`):
  - `FirstPersonPlayer.Seat(Transform seat)`, `Unseat(Vector3 feet, float yawDegrees)`, `IsSeated`, `CurrentSeat`, `event Action Unseated`.
  - `Teleport` selalu menurunkan pemain yang sedang duduk lebih dulu, dan `Unseated` dipanggil setelah teleport selesai.
  - Selama duduk, `Tick`:
    - pemain mengikuti posisi dan yaw kursi;
    - look, jongkok, gerak, langkah, head-bob, ground probe, dan cek jatuh dilewati;
    - **tidak menulis transform kamera**;
    - stamina, HUD, dan modul tetap di-tick.
  - `Flashlight.TurnOff()`: mematikan sorot tanpa bunyi klik.

Catatan: pemanggil (motor) memegang kunci `Move`. `Seat` juga mematikan CharacterController, dan `OnLocksChanged` tidak pernah menyalakannya lagi selama duduk.

- [ ] **Step 1: Tulis tes yang gagal**

Tambahkan ke kelas `FirstPersonPlayerTests` (`Assets/Player/Tests/FirstPersonPlayerTests.cs`), sebelum kurung tutup kelas:
```csharp
        Transform NewSeat(Vector3 local, float yaw)
        {
            var seat = fx.Track(new GameObject("Seat")).transform;
            seat.SetPositionAndRotation(PhysicsFixture.Origin + local, Quaternion.Euler(0f, yaw, 0f));
            return seat;
        }

        static void AssertNear(Vector3 expected, Vector3 actual) => Assert.Less(Vector3.Distance(expected, actual), 1e-3f, actual.ToString("F3"));

        [Test]
        public void Seat_FollowsTheSeat_WithTheBodyOff()
        {
            var rig = FloorRig();
            var seat = NewSeat(new Vector3(3f, 0.85f, 2f), 90f);
            rig.Player.Seat(seat);
            Assert.IsTrue(rig.Player.IsSeated);
            Assert.AreSame(seat, rig.Player.CurrentSeat);
            Assert.IsFalse(rig.Root.GetComponent<CharacterController>().enabled);
            seat.SetPositionAndRotation(seat.position + new Vector3(5f, 0f, 0f), Quaternion.Euler(0f, 120f, 0f));
            rig.Player.Tick(1f / 60f);
            AssertNear(seat.position, rig.Root.transform.position);
            Assert.AreEqual(120f, rig.Player.Yaw, 1e-3f);
        }

        [Test]
        public void Seated_IgnoresMoveAndLook_AndLeavesTheCameraAlone()
        {
            var rig = FloorRig();
            var seat = NewSeat(new Vector3(0f, 0.85f, 0f), 0f);
            rig.Player.Seat(seat);
            var cameraPose = PhysicsFixture.Origin + new Vector3(0f, 3f, -4f);
            rig.View.position = cameraPose;
            rig.Intents.Hold(new PlayerIntent { Move = Vector2.up, LookDegrees = new Vector2(90f, 30f) }, 1f);
            rig.Run(0.5f);
            AssertNear(cameraPose, rig.View.position);
            AssertNear(seat.position, rig.Root.transform.position);
            Assert.AreEqual(0, rig.Player.StepCount);
        }

        [Test]
        public void Seated_BelowTheWorld_DoesNotRespawn()
        {
            var rig = FloorRig();
            int fell = 0;
            rig.Player.FellOutOfWorld += () => fell++;
            rig.Player.Seat(NewSeat(new Vector3(0f, -1100f, 0f), 0f));
            rig.Run(0.1f);
            Assert.AreEqual(0, fell);
        }

        [Test]
        public void Unseat_RestoresControlAndTheCameraRig()
        {
            var rig = FloorRig();
            rig.Player.Seat(NewSeat(new Vector3(0f, 0.85f, 0f), 0f));
            rig.View.localPosition = new Vector3(1f, 2f, -3f);
            rig.View.localRotation = Quaternion.Euler(10f, 20f, 0f);
            int events = 0;
            rig.Player.Unseated += () => events++;
            rig.Player.Unseat(PhysicsFixture.Origin + new Vector3(2f, 0f, 0f), 45f);
            Assert.IsFalse(rig.Player.IsSeated);
            Assert.IsTrue(rig.Root.GetComponent<CharacterController>().enabled);
            Assert.AreEqual(Vector3.zero, rig.View.localPosition);
            Assert.AreEqual(Quaternion.identity, rig.View.localRotation);
            Assert.AreEqual(45f, rig.Player.Yaw, 1e-3f);
            AssertNear(PhysicsFixture.Origin + new Vector3(2f, 0f, 0f), rig.Root.transform.position);
            Assert.AreEqual(1, events);
            rig.Player.Unseat(PhysicsFixture.Origin, 0f);
            Assert.AreEqual(1, events, "Unseat tanpa duduk tidak memicu event");
        }

        [Test]
        public void Teleport_WhileSeated_UnseatsAndRaisesTheEvent()
        {
            var rig = FloorRig();
            rig.Player.Seat(NewSeat(new Vector3(0f, 0.85f, 0f), 0f));
            int events = 0;
            rig.Player.Unseated += () => events++;
            rig.Player.Teleport(PhysicsFixture.Origin + new Vector3(4f, 0f, 0f), 0f);
            Assert.IsFalse(rig.Player.IsSeated);
            Assert.AreEqual(1, events);
            rig.Player.Teleport(PhysicsFixture.Origin, 0f);
            Assert.AreEqual(1, events, "teleport biasa tidak memicu event");
        }

        [Test]
        public void LockChanges_WhileSeated_KeepTheBodyOff()
        {
            var rig = FloorRig();
            rig.Player.Seat(NewSeat(new Vector3(0f, 0.85f, 0f), 0f));
            var owner = new object();
            rig.Player.Locks.Acquire(owner, PlayerAbility.Flashlight);
            rig.Player.Locks.Release(owner);
            Assert.IsFalse(rig.Root.GetComponent<CharacterController>().enabled);
        }
```

Tambahkan ke kelas `FlashlightTests` (`Assets/Player/Tests/FlashlightTests.cs`), sebelum kurung tutup kelas:
```csharp
        [Test]
        public void TurnOff_SwitchesOffWithoutAClick()
        {
            var (_, flashlight, sound) = Rig();
            flashlight.Toggle();
            Assert.IsTrue(flashlight.IsOn);
            flashlight.TurnOff();
            Assert.IsFalse(flashlight.IsOn);
            Assert.AreEqual(1, sound.Calls.Count, "hanya klik saat menyalakan");
            flashlight.TurnOff();
            Assert.IsFalse(flashlight.IsOn);
        }
```

- [ ] **Step 2: Jalankan tes, pastikan gagal**

Procedure R+T, `Group = "FirstPersonPlayerTests|FlashlightTests"`.
Expected: `COMPILE FAILED` (`Seat`, `IsSeated`, `TurnOff` belum ada).

- [ ] **Step 3: Implementasi di `FirstPersonPlayer.cs`**

1. Ganti baris `float bobWeight;` dengan:
```csharp
        float bobWeight;
        Transform seat;
```
2. Ganti baris `public event Action FellOutOfWorld;` dengan:
```csharp
        public event Action FellOutOfWorld;

        /// <summary>Raised after the player left a vehicle seat (Unseat, or Teleport/respawn while seated).</summary>
        public event Action Unseated;
```
3. Ganti baris `public bool EscapeUnlocksCursor { get; set; } = true;` dengan:
```csharp
        public bool EscapeUnlocksCursor { get; set; } = true;

        /// <summary>True while sitting on a vehicle seat (spec #5 §3).</summary>
        public bool IsSeated => seat != null;
        public Transform CurrentSeat => seat;
```
4. Tepat sebelum baris `/// <summary>The only way to move the player instantly (autoSyncTransforms is off).</summary>` tambahkan:
```csharp
        /// <summary>
        /// Sits on a vehicle seat: the body follows the seat, and movement, look and the camera rig are left to the
        /// vehicle until Unseat or Teleport (spec #5 §3). The vehicle holds the Move lock.
        /// </summary>
        public void Seat(Transform seatTransform)
        {
            if (!IsInitialized) Initialize();
            if (seatTransform == null) return;
            seat = seatTransform;
            body.enabled = false;
            movement.ResetMotion();
            IsSprinting = false;
            FollowSeat();
        }

        /// <summary>Gets off at <paramref name="feet"/> facing <paramref name="yawDegrees"/>; does nothing when not seated.</summary>
        public void Unseat(Vector3 feet, float yawDegrees)
        {
            if (!IsSeated) return;
            Teleport(feet, yawDegrees);
        }

```
5. Ganti method `Teleport` (seluruhnya) dengan:
```csharp
        /// <summary>The only way to move the player instantly (autoSyncTransforms is off). A seated player gets off first.</summary>
        public void Teleport(Vector3 feet, float yawDegrees)
        {
            if (!IsInitialized) Initialize();
            bool wasSeated = ClearSeat();
            body.enabled = false;
            transform.SetPositionAndRotation(feet, Quaternion.Euler(0f, yawDegrees, 0f));
            yawPitch = new Vector2(Mathf.Repeat(yawDegrees, 360f), 0f);
            if (cameraPivot != null) cameraPivot.localRotation = Quaternion.identity;
            movement.ResetMotion();
            Physics.SyncTransforms();
            body.enabled = Locks.Allows(PlayerAbility.Move);
            if (wasSeated) Unseated?.Invoke();
        }
```
6. Di `Tick`, ganti baris `Intent = Filter(raw);` dengan:
```csharp
            Intent = Filter(raw);
            if (IsSeated)
            {
                TickSeated(dt);
                return;
            }
```
7. Di `OnLocksChanged`, ganti baris `bool canMove = Locks.Allows(PlayerAbility.Move);` dengan:
```csharp
            bool canMove = Locks.Allows(PlayerAbility.Move) && !IsSeated;
```
8. Tepat sebelum baris `bool HasHeadroomToStand()` tambahkan:
```csharp
        void TickSeated(float dt)
        {
            FollowSeat();
            StepsThisFrame = 0;
            JumpedThisFrame = false;
            LandedThisFrame = false;
            LandingImpact = 0f;
            IsSprinting = false;
            IsGrounded = false;
            Ground = default;
            Stamina.Tick(false, dt);
            Hud.Tick(dt, Stamina.Normalized);
            foreach (var module in modules) module.Tick(this, dt);
        }

        void FollowSeat()
        {
            float yaw = seat.eulerAngles.y;
            transform.SetPositionAndRotation(seat.position, Quaternion.Euler(0f, yaw, 0f));
            yawPitch = new Vector2(Mathf.Repeat(yaw, 360f), 0f);
        }

        /// <summary>Leaves the seat and gives the camera rig back to the player; true when the player was seated.</summary>
        bool ClearSeat()
        {
            if (seat == null) return false;
            seat = null;
            if (cameraView != null)
            {
                cameraView.localPosition = Vector3.zero;
                cameraView.localRotation = Quaternion.identity;
            }
            return true;
        }

```

- [ ] **Step 4: `Flashlight.TurnOff`**

Di `Assets/Player/Runtime/Equipment/Flashlight.cs`, setelah method `Toggle()` tambahkan:
```csharp

        /// <summary>Switches the beam off without the click (getting on a vehicle).</summary>
        public void TurnOff()
        {
            if (spot != null) spot.enabled = false;
        }
```

- [ ] **Step 5: Jalankan tes, pastikan lulus**

Procedure R+T, `Group = "FirstPersonPlayerTests|FlashlightTests"`.
Expected: `failed=0` (7 tes baru ikut lulus).

- [ ] **Step 6: Regresi Player dan GameSettings**

Procedure R+T, `Group = "TycoWorld.Player.Tests|TycoWorld.GameSettings.Tests"`.
Expected: `failed=0`.

- [ ] **Step 7: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add Assets/Player/Runtime Assets/Player/Tests
git -C "D:/UNITY/project/Tyco World" commit -m "feat(player): seated mode for vehicles and silent flashlight off

Co-Authored-By: <model> <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

---

### Task 4: Panel kendaraan di HUD (speedometer, lampu, "[E] Turun")

**Files:**
- Modify: `Assets/Player/Runtime/UI/HudModel.cs`
- Modify: `Assets/Player/Runtime/UI/HudView.cs`
- Test: `Assets/Player/Tests/HudModelTests.cs`, `Assets/Player/Tests/HudViewTests.cs`

**Interfaces:**
- Produces:
  - `HudModel.SetVehicle(bool visible, float speedKmh, bool headlightOn, bool canExit)`, dengan `VehicleVisible`, `VehicleSpeedKmh` (int, dibulatkan, nilai mutlak), `VehicleHeadlight`, dan `VehicleCanExit`.
  - `HudView`:
    - konstanta nama `VehicleName = "vehicle"`, `SpeedName = "vehicle-speed"`, `UnitName = "vehicle-unit"`, `LampName = "vehicle-lamp"`, `ExitName = "vehicle-exit"`;
    - konstanta teks `UnitText = "km/j"`, `LampText = "LAMPU"`, `ExitText = "[E] Turun"`.
  - Saat panel kendaraan tampil, bar stamina disembunyikan.

- [ ] **Step 1: Tulis tes yang gagal**

Tambahkan ke kelas `HudModelTests` (`Assets/Player/Tests/HudModelTests.cs`), sebelum kurung tutup kelas:
```csharp
        [Test]
        public void SetVehicle_RoundsSpeedAndHidesEverythingWhenOff()
        {
            var m = Make();
            Assert.IsFalse(m.VehicleVisible);
            m.SetVehicle(true, 42.6f, true, false);
            Assert.IsTrue(m.VehicleVisible);
            Assert.AreEqual(43, m.VehicleSpeedKmh);
            Assert.IsTrue(m.VehicleHeadlight);
            Assert.IsFalse(m.VehicleCanExit);
            m.SetVehicle(true, -8.6f, false, true);
            Assert.AreEqual(9, m.VehicleSpeedKmh, "mundur ditampilkan positif");
            Assert.IsTrue(m.VehicleCanExit);
            m.SetVehicle(false, 30f, true, true);
            Assert.IsFalse(m.VehicleVisible);
            Assert.AreEqual(0, m.VehicleSpeedKmh);
            Assert.IsFalse(m.VehicleHeadlight || m.VehicleCanExit);
        }
```

Tambahkan ke kelas `HudViewTests` (`Assets/Player/Tests/HudViewTests.cs`), sebelum kurung tutup kelas:
```csharp
        [Test]
        public void VehiclePanel_IsBuiltHidden_WithIndonesianTexts()
        {
            var (root, _, _) = Make();
            foreach (var name in new[] { HudView.VehicleName, HudView.SpeedName, HudView.UnitName, HudView.LampName, HudView.ExitName })
                Assert.IsNotNull(root.Q(name), name);
            Assert.AreEqual(DisplayStyle.None, root.Q(HudView.VehicleName).style.display.value);
            Assert.AreEqual("km/j", root.Q<Label>(HudView.UnitName).text);
            Assert.AreEqual("LAMPU", root.Q<Label>(HudView.LampName).text);
            Assert.AreEqual("[E] Turun", root.Q<Label>(HudView.ExitName).text);
        }

        [Test]
        public void Render_ShowsSpeedAndExitHint_HidesStamina()
        {
            var (root, view, model) = Make();
            model.Tick(0.1f, 0.5f);
            Assert.IsTrue(model.StaminaVisible);
            model.SetVehicle(true, 42.4f, false, true);
            view.Render(model);
            Assert.AreEqual(DisplayStyle.Flex, root.Q(HudView.VehicleName).style.display.value);
            Assert.AreEqual("42", root.Q<Label>(HudView.SpeedName).text);
            Assert.AreEqual(DisplayStyle.Flex, root.Q(HudView.ExitName).style.display.value);
            Assert.AreEqual(DisplayStyle.None, root.Q(HudView.StaminaName).style.display.value);
            model.SetVehicle(true, 30f, true, false);
            view.Render(model);
            Assert.AreEqual("30", root.Q<Label>(HudView.SpeedName).text);
            Assert.AreEqual(DisplayStyle.None, root.Q(HudView.ExitName).style.display.value);
            model.SetVehicle(false, 0f, false, false);
            view.Render(model);
            Assert.AreEqual(DisplayStyle.None, root.Q(HudView.VehicleName).style.display.value);
            Assert.AreEqual(DisplayStyle.Flex, root.Q(HudView.StaminaName).style.display.value);
        }

        [Test]
        public void Render_HeadlightOn_BrightensTheLampLabel()
        {
            var (root, view, model) = Make();
            model.SetVehicle(true, 10f, false, false);
            view.Render(model);
            float off = root.Q<Label>(HudView.LampName).style.color.value.a;
            model.SetVehicle(true, 10f, true, false);
            view.Render(model);
            float on = root.Q<Label>(HudView.LampName).style.color.value.a;
            Assert.Greater(on, off);
        }
```

- [ ] **Step 2: Jalankan tes, pastikan gagal**

Procedure R+T, `Group = "HudModelTests|HudViewTests"`.
Expected: `COMPILE FAILED`.

- [ ] **Step 3: Implementasi `HudModel`**

Di `Assets/Player/Runtime/UI/HudModel.cs`, ganti baris `public bool ClockVisible { get; private set; }` dengan:
```csharp
        public bool ClockVisible { get; private set; }
        public bool VehicleVisible { get; private set; }
        public int VehicleSpeedKmh { get; private set; }
        public bool VehicleHeadlight { get; private set; }
        public bool VehicleCanExit { get; private set; }

        /// <summary>Riding HUD (spec #5 §6): rounded speed in km/j, headlight state and the "[E] Turun" hint.</summary>
        public void SetVehicle(bool visible, float speedKmh, bool headlightOn, bool canExit)
        {
            VehicleVisible = visible;
            VehicleSpeedKmh = visible ? Mathf.RoundToInt(Mathf.Abs(speedKmh)) : 0;
            VehicleHeadlight = visible && headlightOn;
            VehicleCanExit = visible && canExit;
        }
```

- [ ] **Step 4: Implementasi `HudView`**

Di `Assets/Player/Runtime/UI/HudView.cs`:

1. Ganti `using UnityEngine;` (baris pertama) dengan:
```csharp
using System.Globalization;
using UnityEngine;
```
2. Ganti baris `public const string ClockName = "clock";` dengan:
```csharp
        public const string ClockName = "clock";
        public const string VehicleName = "vehicle";
        public const string SpeedName = "vehicle-speed";
        public const string UnitName = "vehicle-unit";
        public const string LampName = "vehicle-lamp";
        public const string ExitName = "vehicle-exit";
        public const string UnitText = "km/j";
        public const string LampText = "LAMPU";
        public const string ExitText = "[E] Turun";

        static readonly Color LampOnColor = new Color(1f, 0.92f, 0.55f, 1f);
        static readonly Color LampOffColor = new Color(0.92f, 0.92f, 0.88f, 0.3f);
```
3. Ganti baris `readonly VisualElement crosshair, staminaBar, staminaFill;` dengan:
```csharp
        readonly VisualElement crosshair, staminaBar, staminaFill, vehicle;
        int shownSpeed = -1;
```
4. Ganti baris `readonly Label prompt, message, clock;` dengan:
```csharp
        readonly Label prompt, message, clock, speed, lamp, exit;
```
5. Tepat sebelum baris `root.Add(crosshair);` tambahkan:
```csharp
            vehicle = new VisualElement { name = VehicleName, pickingMode = PickingMode.Ignore };
            Absolute(vehicle);
            vehicle.style.right = 36f;
            vehicle.style.bottom = 40f;
            vehicle.style.alignItems = Align.FlexEnd;
            vehicle.style.display = DisplayStyle.None;
            speed = VehicleLabel(SpeedName, "0", 56f);
            speed.style.unityFontStyleAndWeight = FontStyle.Bold;
            lamp = VehicleLabel(LampName, LampText, 18f);
            exit = VehicleLabel(ExitName, ExitText, 20f);
            vehicle.Add(speed);
            vehicle.Add(VehicleLabel(UnitName, UnitText, 20f));
            vehicle.Add(lamp);
            vehicle.Add(exit);

```
6. Ganti baris `root.Add(clock);` dengan:
```csharp
            root.Add(clock);
            root.Add(vehicle);
```
7. Di `Render`, ganti baris `staminaBar.style.display = model.StaminaVisible ? DisplayStyle.Flex : DisplayStyle.None;` dengan:
```csharp
            staminaBar.style.display = model.StaminaVisible && !model.VehicleVisible ? DisplayStyle.Flex : DisplayStyle.None;
            vehicle.style.display = model.VehicleVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (model.VehicleVisible)
            {
                if (shownSpeed != model.VehicleSpeedKmh)
                {
                    shownSpeed = model.VehicleSpeedKmh;
                    speed.text = shownSpeed.ToString(CultureInfo.InvariantCulture);
                }
                lamp.style.color = model.VehicleHeadlight ? LampOnColor : LampOffColor;
                exit.style.display = model.VehicleCanExit ? DisplayStyle.Flex : DisplayStyle.None;
            }
```
8. Tepat sebelum `static void Absolute(VisualElement e)` tambahkan:
```csharp
        static Label VehicleLabel(string name, string text, float size)
        {
            var label = new Label(text) { name = name, pickingMode = PickingMode.Ignore };
            label.style.fontSize = size;
            label.style.color = new Color(0.92f, 0.92f, 0.88f, 1f);
            label.style.unityTextAlign = TextAnchor.MiddleRight;
            return label;
        }

```

- [ ] **Step 5: Jalankan tes, pastikan lulus**

Procedure R+T, `Group = "HudModelTests|HudViewTests"`.
Expected: `failed=0` (4 tes baru ikut lulus).

- [ ] **Step 6: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add Assets/Player/Runtime/UI Assets/Player/Tests
git -C "D:/UNITY/project/Tyco World" commit -m "feat(player): riding HUD panel with speedometer, headlight and exit hint

Co-Authored-By: <model> <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

---

### Task 5: Kamera naik motor (`RideCameraMath`, `RideCamera`) dan fixture fisika Vehicles

**Files:**
- Create: `Assets/Vehicles/Runtime/Camera/RideCameraMode.cs`
- Create: `Assets/Vehicles/Runtime/Camera/RideCameraMath.cs`
- Create: `Assets/Vehicles/Runtime/Camera/RideCamera.cs`
- Create: `Assets/Vehicles/Tests/VehicleFixture.cs`
- Test: `Assets/Vehicles/Tests/RideCameraTests.cs`

**Interfaces:**
- Consumes: `BikeTuning` (Task 2), `PlayerLayers.QueryMask` (Player).
- Produces:
  - `enum RideCameraMode { ThirdPerson, FirstPerson }`.
  - `RideCameraMath` (statis):
    - `ChasePivot(Vector3 bikeRoot, BikeTuning)`, `DefaultElevation(BikeTuning)`, `ChaseRadius(BikeTuning)`;
    - `ChasePosition(Vector3 pivot, float followYaw, float orbitYaw, float orbitPitch, BikeTuning)`;
    - `LookAt(Vector3 from, Vector3 target)`;
    - `HeadPosition(Vector3 seat, float bikeYaw, BikeTuning)`, `HeadRotation(float bikeYaw, float lean, float lookYaw, float lookPitch, BikeTuning)`.
  - `RideCamera(BikeTuning)`:
    - `Mode`, `FollowYaw`, `OrbitYaw`, `OrbitPitch`, `LookYaw`, `LookPitch`;
    - `Reset(float bikeYaw)`, `Toggle()`, `Update(Vector2 lookDegrees, float bikeYaw, float speed, float dt)`;
    - `PoseThirdPerson(Vector3 bikeRoot, out Vector3, out Quaternion)`, `PoseFirstPerson(Vector3 seat, float bikeYaw, float lean, out Vector3, out Quaternion)`;
    - statis `PullIn(Vector3 pivot, Vector3 desired, float radius) → Vector3`.
  - Tes: `VehicleFixture` dengan `Origin` (7000, 1000, 7000), `Track`, `Box(name, local, size, yaw = 0)`, `Floor(size = 600, topY = 0)`, `Sync()`, dan `Dispose()`.

Konvensi:
- **Orang ketiga:** look.x menambah yaw orbit. Look.y positif (mouse naik) **menurunkan** elevasi kamera, jadi pandangan naik.
- **Orang pertama:** look.y positif menaikkan pandangan.
- **Titik tatap orang ketiga:** 1,2 m di atas akar motor (lihat Deviasi di Global Constraints).

- [ ] **Step 1: Tulis fixture dan tes yang gagal**

`Assets/Vehicles/Tests/VehicleFixture.cs`:
```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.Vehicles.Tests
{
    /// <summary>Physics scratch objects far outside the world (and away from the player tests' fixture), destroyed on Dispose.</summary>
    internal sealed partial class VehicleFixture : IDisposable
    {
        public static readonly Vector3 Origin = new Vector3(7000f, 1000f, 7000f);
        readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

        public T Track<T>(T obj) where T : UnityEngine.Object
        {
            created.Add(obj);
            return obj;
        }

        /// <summary>Box collider centred at Origin + local, rotated by <paramref name="yaw"/>.</summary>
        public GameObject Box(string name, Vector3 local, Vector3 size, float yaw = 0f)
        {
            var go = Track(new GameObject(name));
            go.transform.SetPositionAndRotation(Origin + local, Quaternion.Euler(0f, yaw, 0f));
            go.AddComponent<BoxCollider>().size = size;
            return go;
        }

        /// <summary>1 m thick square floor whose top face is at Origin.y + topY.</summary>
        public GameObject Floor(float size = 600f, float topY = 0f) =>
            Box("Floor", new Vector3(0f, topY - 0.5f, 0f), new Vector3(size, 1f, size));

        public void Sync() => Physics.SyncTransforms();

        public void Dispose()
        {
            DisposeExtras();
            for (int i = created.Count - 1; i >= 0; i--)
                if (created[i] != null) UnityEngine.Object.DestroyImmediate(created[i]);
            created.Clear();
        }

        partial void DisposeExtras();
    }
}
```

`Assets/Vehicles/Tests/RideCameraTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.Vehicles.Tests
{
    public class RideCameraTests
    {
        const float Dt = 0.02f;
        BikeTuning tuning;
        VehicleFixture fx;
        RideCamera cam;

        static Vector3 O => VehicleFixture.Origin;

        [SetUp]
        public void SetUp()
        {
            tuning = ScriptableObject.CreateInstance<BikeTuning>();
            fx = new VehicleFixture();
            cam = new RideCamera(tuning);
            cam.Reset(0f);
        }

        [TearDown]
        public void TearDown()
        {
            fx.Dispose();
            Object.DestroyImmediate(tuning);
        }

        static void AssertNear(Vector3 expected, Vector3 actual, float tolerance = 1e-3f) =>
            Assert.Less(Vector3.Distance(expected, actual), tolerance, actual.ToString("F3"));

        void Idle(float seconds, float speed)
        {
            for (int i = 0; i < Mathf.RoundToInt(seconds / Dt); i++) cam.Update(Vector2.zero, 0f, speed, Dt);
        }

        float Elevation()
        {
            cam.PoseThirdPerson(O, out var p, out _);
            var pivot = RideCameraMath.ChasePivot(O, tuning);
            return Mathf.Asin((p.y - pivot.y) / Vector3.Distance(p, pivot)) * Mathf.Rad2Deg;
        }

        [Test]
        public void DefaultChase_IsBehindAndAbove_LookingAtThePivot()
        {
            cam.PoseThirdPerson(O, out var p, out var r);
            AssertNear(O + new Vector3(0f, 1.9f, -4.5f), p);
            var pivot = O + Vector3.up * 1.2f;
            Assert.Less(Vector3.Angle(r * Vector3.forward, pivot - p), 0.01f);
        }

        [Test]
        public void Chase_FollowsTheBikeYawSmoothly()
        {
            cam.Update(Vector2.zero, 90f, 10f, Dt);
            Assert.That(cam.FollowYaw, Is.InRange(0.1f, 89.9f));
            for (int i = 0; i < 300; i++) cam.Update(Vector2.zero, 90f, 10f, Dt);
            Assert.AreEqual(90f, cam.FollowYaw, 0.01f);
        }

        [Test]
        public void Orbit_IsClamped()
        {
            cam.Update(new Vector2(500f, 0f), 0f, 0f, Dt);
            Assert.AreEqual(120f, cam.OrbitYaw, 1e-3f);
            cam.Update(new Vector2(0f, -500f), 0f, 0f, Dt);
            Assert.AreEqual(35f, Elevation(), 0.01f);
            cam.Update(new Vector2(0f, 1000f), 0f, 0f, Dt);
            Assert.AreEqual(-10f, Elevation(), 0.01f);
        }

        [Test]
        public void Orbit_ReturnsBehind_AfterIdlingWhileMoving()
        {
            cam.Update(new Vector2(60f, 0f), 0f, 5f, Dt);
            Idle(1f, 5f);
            Assert.AreEqual(60f, cam.OrbitYaw, 1e-3f, "belum 1,5 s");
            Idle(2f, 5f);
            Assert.Less(Mathf.Abs(cam.OrbitYaw), 5f);
        }

        [Test]
        public void Orbit_Stays_WhenTheBikeIsSlow()
        {
            cam.Update(new Vector2(60f, 0f), 0f, 1f, Dt);
            Idle(3f, 1f);
            Assert.AreEqual(60f, cam.OrbitYaw, 1e-3f);
        }

        [Test]
        public void Toggle_SwitchesModeAndResetsOffsets()
        {
            cam.Update(new Vector2(60f, 10f), 0f, 0f, Dt);
            Assert.AreEqual(RideCameraMode.ThirdPerson, cam.Mode);
            cam.Toggle();
            Assert.AreEqual(RideCameraMode.FirstPerson, cam.Mode);
            Assert.AreEqual(0f, cam.OrbitYaw);
            cam.Update(new Vector2(20f, 5f), 0f, 0f, Dt);
            Assert.AreEqual(20f, cam.LookYaw, 1e-3f);
            cam.Toggle();
            Assert.AreEqual(RideCameraMode.ThirdPerson, cam.Mode);
            Assert.AreEqual(0f, cam.LookYaw);
        }

        [Test]
        public void FirstPerson_HeadSitsAboveTheSeat_LookIsClamped()
        {
            cam.Toggle();
            cam.Update(new Vector2(500f, 500f), 0f, 0f, Dt);
            Assert.AreEqual(100f, cam.LookYaw, 1e-3f);
            Assert.AreEqual(60f, cam.LookPitch, 1e-3f);
            var seat = O + new Vector3(0f, 0.85f, 0f);
            cam.PoseFirstPerson(seat, 0f, 0f, out var p, out var r);
            AssertNear(seat + new Vector3(0f, 0.75f, -0.1f), p);
            var forward = r * Vector3.forward;
            Assert.Greater(forward.y, 0.8f, "menengadah 60°");
            Assert.Greater(forward.x, 0f, "menoleh ke kanan");
        }

        [Test]
        public void FirstPerson_RollsWithHalfTheLean()
        {
            cam.Toggle();
            cam.PoseFirstPerson(O, 0f, 20f, out _, out var r);
            Assert.AreEqual(-10f, Vector3.SignedAngle(Vector3.up, r * Vector3.up, Vector3.forward), 0.01f);
        }

        [Test]
        public void PullIn_StopsInFrontOfAWall()
        {
            fx.Box("Wall", new Vector3(0f, 1.5f, -2.5f), new Vector3(4f, 3f, 0.2f));
            fx.Sync();
            cam.PoseThirdPerson(O, out var p, out _);
            Assert.Greater(p.z, O.z - 2.4f, "di depan permukaan tembok");
            Assert.Less(p.z, O.z);
        }

        [Test]
        public void PullIn_WithoutObstacles_KeepsTheDesiredPoint()
        {
            var pivot = O + Vector3.up;
            var desired = pivot + new Vector3(0f, 0f, -5f);
            AssertNear(desired, RideCamera.PullIn(pivot, desired, 0.25f));
        }
    }
}
```

- [ ] **Step 2: Jalankan tes, pastikan gagal**

Procedure R+T, `Group = "RideCameraTests"`.
Expected: `COMPILE FAILED` (`RideCamera` belum ada).

- [ ] **Step 3: Implementasi**

`Assets/Vehicles/Runtime/Camera/RideCameraMode.cs`:
```csharp
namespace TycoWorld.Vehicles
{
    public enum RideCameraMode
    {
        ThirdPerson,
        FirstPerson
    }
}
```

`Assets/Vehicles/Runtime/Camera/RideCameraMath.cs`:
```csharp
using UnityEngine;

namespace TycoWorld.Vehicles
{
    /// <summary>Pure camera poses while riding (spec #5 §4): a chase orbit around a pivot above the bike, and the rider's head.</summary>
    public static class RideCameraMath
    {
        /// <summary>What the chase camera looks at: lookHeight above the bike root (the ground under the bike).</summary>
        public static Vector3 ChasePivot(Vector3 bikeRoot, BikeTuning t) => bikeRoot + Vector3.up * t.lookHeight;

        /// <summary>Elevation (degrees) of the default chase position (chaseDistance behind, chaseHeight up) seen from the pivot.</summary>
        public static float DefaultElevation(BikeTuning t) => Mathf.Atan2(t.chaseHeight - t.lookHeight, t.chaseDistance) * Mathf.Rad2Deg;

        public static float ChaseRadius(BikeTuning t)
        {
            float up = t.chaseHeight - t.lookHeight;
            return Mathf.Sqrt(t.chaseDistance * t.chaseDistance + up * up);
        }

        /// <summary>Chase position for a follow yaw plus orbit offsets; yaw within ±orbitYawLimit, elevation within [orbitPitchMin, orbitPitchMax].</summary>
        public static Vector3 ChasePosition(Vector3 pivot, float followYaw, float orbitYaw, float orbitPitch, BikeTuning t)
        {
            float elevation = Mathf.Clamp(DefaultElevation(t) + orbitPitch, t.orbitPitchMin, t.orbitPitchMax);
            float yaw = followYaw + Mathf.Clamp(orbitYaw, -t.orbitYawLimit, t.orbitYawLimit);
            return pivot + Quaternion.Euler(elevation, yaw, 0f) * new Vector3(0f, 0f, -ChaseRadius(t));
        }

        public static Quaternion LookAt(Vector3 from, Vector3 target) => Quaternion.LookRotation(target - from, Vector3.up);

        public static Vector3 HeadPosition(Vector3 seat, float bikeYaw, BikeTuning t) =>
            seat + Quaternion.Euler(0f, bikeYaw, 0f) * new Vector3(0f, t.headHeight, -t.headBack);

        /// <summary>Head rotation: bike yaw, then rollFactor × lean, then the clamped look (positive pitch looks up).</summary>
        public static Quaternion HeadRotation(float bikeYaw, float lean, float lookYaw, float lookPitch, BikeTuning t)
        {
            float yaw = Mathf.Clamp(lookYaw, -t.lookYawLimit, t.lookYawLimit);
            float pitch = Mathf.Clamp(lookPitch, -t.lookPitchLimit, t.lookPitchLimit);
            return Quaternion.Euler(0f, bikeYaw, 0f) * Quaternion.Euler(0f, 0f, -lean * t.rollFactor) * Quaternion.Euler(-pitch, yaw, 0f);
        }
    }
}
```

`Assets/Vehicles/Runtime/Camera/RideCamera.cs`:
```csharp
using TycoWorld.Player;
using UnityEngine;

namespace TycoWorld.Vehicles
{
    /// <summary>
    /// Camera state while riding: third-person chase with mouse orbit that drifts back behind the bike, or the
    /// rider's head with a limited look; the chase position is pulled in so it never goes through walls (spec #5 §4).
    /// </summary>
    public sealed class RideCamera
    {
        readonly BikeTuning t;
        float idle;

        public RideCamera(BikeTuning tuning) => t = tuning;

        public RideCameraMode Mode { get; private set; } = RideCameraMode.ThirdPerson;
        public float FollowYaw { get; private set; }
        public float OrbitYaw { get; private set; }
        /// <summary>Offset (degrees) added to the default chase elevation.</summary>
        public float OrbitPitch { get; private set; }
        public float LookYaw { get; private set; }
        public float LookPitch { get; private set; }

        public void Reset(float bikeYaw)
        {
            FollowYaw = bikeYaw;
            ClearOffsets();
        }

        public void Toggle()
        {
            Mode = Mode == RideCameraMode.ThirdPerson ? RideCameraMode.FirstPerson : RideCameraMode.ThirdPerson;
            ClearOffsets();
        }

        public void Update(Vector2 lookDegrees, float bikeYaw, float speed, float dt)
        {
            FollowYaw = Mathf.LerpAngle(FollowYaw, bikeYaw, 1f - Mathf.Exp(-t.followSmoothing * dt));
            if (Mode == RideCameraMode.FirstPerson)
            {
                LookYaw = Mathf.Clamp(LookYaw + lookDegrees.x, -t.lookYawLimit, t.lookYawLimit);
                LookPitch = Mathf.Clamp(LookPitch + lookDegrees.y, -t.lookPitchLimit, t.lookPitchLimit);
                return;
            }
            float baseElevation = RideCameraMath.DefaultElevation(t);
            OrbitYaw = Mathf.Clamp(OrbitYaw + lookDegrees.x, -t.orbitYawLimit, t.orbitYawLimit);
            OrbitPitch = Mathf.Clamp(OrbitPitch - lookDegrees.y, t.orbitPitchMin - baseElevation, t.orbitPitchMax - baseElevation);
            idle = lookDegrees.sqrMagnitude > 1e-6f ? 0f : idle + dt;
            if (idle >= t.returnDelay && Mathf.Abs(speed) > t.returnMinSpeed)
            {
                float k = 1f - Mathf.Exp(-t.returnSpeed * dt);
                OrbitYaw = Mathf.Lerp(OrbitYaw, 0f, k);
                OrbitPitch = Mathf.Lerp(OrbitPitch, 0f, k);
            }
        }

        public void PoseThirdPerson(Vector3 bikeRoot, out Vector3 position, out Quaternion rotation)
        {
            var pivot = RideCameraMath.ChasePivot(bikeRoot, t);
            var desired = RideCameraMath.ChasePosition(pivot, FollowYaw, OrbitYaw, OrbitPitch, t);
            position = PullIn(pivot, desired, t.cameraRadius);
            rotation = RideCameraMath.LookAt(position, pivot);
        }

        public void PoseFirstPerson(Vector3 seat, float bikeYaw, float lean, out Vector3 position, out Quaternion rotation)
        {
            position = RideCameraMath.HeadPosition(seat, bikeYaw, t);
            rotation = RideCameraMath.HeadRotation(bikeYaw, lean, LookYaw, LookPitch, t);
        }

        /// <summary>Moves the camera towards the pivot when something stands between them (sphere cast), keeping at least 0.3 m.</summary>
        public static Vector3 PullIn(Vector3 pivot, Vector3 desired, float radius)
        {
            var offset = desired - pivot;
            float distance = offset.magnitude;
            if (distance < 1e-4f) return desired;
            var direction = offset / distance;
            if (Physics.SphereCast(pivot, radius, direction, out var hit, distance, PlayerLayers.QueryMask, QueryTriggerInteraction.Ignore))
                return pivot + direction * Mathf.Max(hit.distance - 0.1f, 0.3f);
            return desired;
        }

        void ClearOffsets()
        {
            OrbitYaw = 0f;
            OrbitPitch = 0f;
            LookYaw = 0f;
            LookPitch = 0f;
            idle = 0f;
        }
    }
}
```

- [ ] **Step 4: Jalankan tes, pastikan lulus**

Procedure R+T, `Group = "RideCameraTests"`.
Expected: `passed=10 failed=0`.

- [ ] **Step 5: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add Assets/Vehicles
git -C "D:/UNITY/project/Tyco World" commit -m "feat(vehicles): third- and first-person ride camera with wall pull-in

Co-Authored-By: <model> <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

---

### Task 6: `DismountFinder` (tempat turun yang aman)

**Files:**
- Create: `Assets/Vehicles/Runtime/Motion/DismountFinder.cs`
- Test: `Assets/Vehicles/Tests/DismountFinderTests.cs`

**Interfaces:**
- Consumes: `PlayerLayers.QueryMask`, `VehicleFixture` (Task 5).
- Produces: `DismountFinder.TryFind(Transform bike, float sideOffset, float radius, float height, out Vector3 feet) → bool`. Urutannya kiri dulu (`-bike.right`), lalu kanan. Untuk tiap sisi:
  - tanah dicari dengan raycast dari 0,6 m di atas titik samping ke bawah sejauh 1,6 m (trotoar sampai 0,6 m tetap terjangkau, plafon rendah tidak pernah dianggap tanah);
  - kapsul berdiri (radius × 0,95, dari radius + 0,05 sampai tinggi − radius) harus muat.

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/Vehicles/Tests/DismountFinderTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.Vehicles.Tests
{
    public class DismountFinderTests
    {
        VehicleFixture fx;
        Transform bike;

        static Vector3 O => VehicleFixture.Origin;

        [SetUp]
        public void SetUp()
        {
            fx = new VehicleFixture();
            fx.Floor();
            bike = fx.Track(new GameObject("Bike")).transform;
            bike.SetPositionAndRotation(O, Quaternion.identity);
        }

        [TearDown]
        public void TearDown() => fx.Dispose();

        bool Find(out Vector3 feet)
        {
            fx.Sync();
            return DismountFinder.TryFind(bike, 1.1f, 0.35f, 1.8f, out feet);
        }

        [Test]
        public void OpenGround_GetsOffOnTheLeft()
        {
            Assert.IsTrue(Find(out var feet));
            Assert.Less(Vector3.Distance(O + new Vector3(-1.1f, 0f, 0f), feet), 1e-3f);
        }

        [Test]
        public void LeftBlocked_GetsOffOnTheRight()
        {
            fx.Box("WallLeft", new Vector3(-1.1f, 1f, 0f), new Vector3(0.4f, 2f, 3f));
            Assert.IsTrue(Find(out var feet));
            Assert.Greater(feet.x, O.x);
        }

        [Test]
        public void BothSidesBlocked_Fails()
        {
            fx.Box("WallLeft", new Vector3(-1.1f, 1f, 0f), new Vector3(0.4f, 2f, 3f));
            fx.Box("WallRight", new Vector3(1.1f, 1f, 0f), new Vector3(0.4f, 2f, 3f));
            Assert.IsFalse(Find(out _));
        }

        [Test]
        public void LowCeiling_Fails()
        {
            fx.Box("Ceiling", new Vector3(0f, 1.3f, 0f), new Vector3(6f, 0.2f, 6f));
            Assert.IsFalse(Find(out _));
        }

        [Test]
        public void RaisedGround_PutsTheFeetOnTop()
        {
            fx.Box("Curb", new Vector3(-1.1f, 0.1f, 0f), new Vector3(1f, 0.2f, 2f));
            Assert.IsTrue(Find(out var feet));
            Assert.AreEqual(O.y + 0.2f, feet.y, 1e-3f);
        }

        [Test]
        public void TurnedBike_UsesItsOwnLeftSide()
        {
            bike.rotation = Quaternion.Euler(0f, 90f, 0f);
            Assert.IsTrue(Find(out var feet));
            Assert.Less(Vector3.Distance(O + new Vector3(0f, 0f, 1.1f), feet), 1e-3f, "kiri motor yang menghadap +X adalah +Z");
        }
    }
}
```

- [ ] **Step 2: Jalankan tes, pastikan gagal**

Procedure R+T, `Group = "DismountFinderTests"`.
Expected: `COMPILE FAILED`.

- [ ] **Step 3: Implementasi**

`Assets/Vehicles/Runtime/Motion/DismountFinder.cs`:
```csharp
using TycoWorld.Player;
using UnityEngine;

namespace TycoWorld.Vehicles
{
    /// <summary>Where the rider can step off (spec #5 §4): beside the bike, left first, on ground with room for the standing capsule.</summary>
    public static class DismountFinder
    {
        public static bool TryFind(Transform bike, float sideOffset, float radius, float height, out Vector3 feet)
        {
            Physics.SyncTransforms();
            foreach (float side in new[] { -1f, 1f })
            {
                var spot = bike.position + bike.right * (side * sideOffset);
                if (!Physics.Raycast(spot + Vector3.up * 0.6f, Vector3.down, out var ground, 1.6f, PlayerLayers.QueryMask, QueryTriggerInteraction.Ignore))
                    continue;
                var candidate = ground.point;
                var bottom = candidate + Vector3.up * (radius + 0.05f);
                var top = candidate + Vector3.up * (height - radius);
                if (Physics.CheckCapsule(bottom, top, radius * 0.95f, PlayerLayers.QueryMask, QueryTriggerInteraction.Ignore)) continue;
                feet = candidate;
                return true;
            }
            feet = default;
            return false;
        }
    }
}
```

- [ ] **Step 4: Jalankan tes, pastikan lulus**

Procedure R+T, `Group = "DismountFinderTests"`.
Expected: `passed=6 failed=0`.

- [ ] **Step 5: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add Assets/Vehicles
git -C "D:/UNITY/project/Tyco World" commit -m "feat(vehicles): safe dismount spot finder

Co-Authored-By: <model> <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

---

### Task 7: Visual motor (`RuntimeEmission`, `MotorbikeVisuals`) dan audio motor (`EngineSound`, `MotorbikeAudio`)

**Files:**
- Create: `Assets/Vehicles/Runtime/Visuals/RuntimeEmission.cs`
- Create: `Assets/Vehicles/Runtime/Visuals/MotorbikeVisuals.cs`
- Create: `Assets/Vehicles/Runtime/Audio/EngineSound.cs`
- Create: `Assets/Vehicles/Runtime/Audio/MotorbikeAudio.cs`
- Create: `Assets/Vehicles/Tests/RecordingSounds.cs`
- Test: `Assets/Vehicles/Tests/MotorbikeVisualsTests.cs`, `Assets/Vehicles/Tests/MotorbikeAudioTests.cs`

**Interfaces:**
- Consumes: `BikeTuning` (Task 2), `ISoundPlayer` / `AudioSourceSoundPlayer` (Player).
- Produces:
  - `RuntimeEmission(Renderer, int materialIndex, float lowFactor, float highFactor) : IDisposable` dengan `Set(bool high)`, `IsHigh`, `Low`, `High`, dan `Dispose()` (mengembalikan material asli).
  - `MotorbikeVisuals : MonoBehaviour`:
    - `Configure(Transform leanPivot, Transform handlebar, Transform frontWheel, Transform rearWheel, Transform rider, Light headlight, Renderer tailLight, Renderer headlightLens)`;
    - `Initialize()`;
    - `Apply(float speed, float steerAngle, float lean, bool braking, bool headlightOn, bool riderVisible, float dt)`;
    - `Shutdown()`;
    - getter `LeanPivot`, `Handlebar`, `FrontWheel`, `RearWheel`, `Rider`, `Headlight`, `TailLight`, `HeadlightLens`, `TailBright`, `LensOn`.
  - `EngineSound.Pitch(float speed01, float throttle, BikeTuning)` dan `Volume(float speed01, BikeTuning)`.
  - `MotorbikeAudio : MonoBehaviour`:
    - `Configure(AudioSource engine, AudioSource oneShot, AudioClip engineLoop, AudioClip starter, AudioClip horn, AudioClip bump)`;
    - `Level` (0..1), `EngineRunning`, `EngineSource`, `OneShotSource`, `OneShots` (ISoundPlayer, bisa diganti untuk tes);
    - `StartEngine()`, `StopEngine()`, `Tick(float speed01, float throttle, float dt, BikeTuning)`, `Horn(BikeTuning) → bool`, `Bump(float strength01)`.
  - Tes: `RecordingSounds : ISoundPlayer` (`Calls`).

Aturan:
- Lampu belakang redup (×0,35) dan terang saat mengerem (×1).
- Lensa lampu depan mati (×0) atau nyala (×1).
- Semua lewat salinan `DontSave`; asset material tidak pernah diubah.
- Miring diterapkan sebagai `Euler(0, 0, -lean)` (positif = miring kanan).
- Roda berputar pada sumbu X lokal dengan sudut += speed / 0,3 m (rad/s).

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/Vehicles/Tests/RecordingSounds.cs`:
```csharp
using System.Collections.Generic;
using TycoWorld.Player;
using UnityEngine;

namespace TycoWorld.Vehicles.Tests
{
    /// <summary>Records one-shot requests instead of playing them.</summary>
    internal sealed class RecordingSounds : ISoundPlayer
    {
        public readonly List<(AudioClip Clip, float Volume, float Pitch)> Calls = new List<(AudioClip, float, float)>();

        public void Play(AudioClip clip, float volume, float pitch) => Calls.Add((clip, volume, pitch));
    }
}
```

`Assets/Vehicles/Tests/MotorbikeAudioTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.Vehicles.Tests
{
    public class MotorbikeAudioTests
    {
        VehicleFixture fx;
        BikeTuning tuning;
        MotorbikeAudio audio;
        RecordingSounds sounds;
        AudioClip engine, starter, horn, bump;

        [SetUp]
        public void SetUp()
        {
            fx = new VehicleFixture();
            tuning = fx.Track(ScriptableObject.CreateInstance<BikeTuning>());
            engine = fx.Track(AudioClip.Create("engine", 4410, 1, 44100, false));
            starter = fx.Track(AudioClip.Create("starter", 441, 1, 44100, false));
            horn = fx.Track(AudioClip.Create("horn", 441, 1, 44100, false));
            bump = fx.Track(AudioClip.Create("bump", 441, 1, 44100, false));
            var go = fx.Track(new GameObject("BikeAudio"));
            audio = go.AddComponent<MotorbikeAudio>();
            audio.Configure(go.AddComponent<AudioSource>(), go.AddComponent<AudioSource>(), engine, starter, horn, bump);
            sounds = new RecordingSounds();
            audio.OneShots = sounds;
        }

        [TearDown]
        public void TearDown() => fx.Dispose();

        [Test]
        public void EngineSound_FollowsSpeedAndThrottle()
        {
            Assert.AreEqual(0.8f, EngineSound.Pitch(0f, 0f, tuning), 1e-5f);
            Assert.AreEqual(2f, EngineSound.Pitch(1f, 0f, tuning), 1e-5f);
            Assert.AreEqual(2.1f, EngineSound.Pitch(1f, 1f, tuning), 1e-5f);
            Assert.AreEqual(2.1f, EngineSound.Pitch(3f, 5f, tuning), 1e-5f, "di-clamp");
            Assert.AreEqual(0.6f, EngineSound.Volume(0f, tuning), 1e-5f);
            Assert.AreEqual(0.9f, EngineSound.Volume(1f, tuning), 1e-5f);
        }

        [Test]
        public void StartEngine_PlaysTheStarterOnce_AndPreparesTheLoop()
        {
            audio.StartEngine();
            audio.StartEngine();
            Assert.IsTrue(audio.EngineRunning);
            Assert.AreEqual(1, sounds.Calls.Count);
            Assert.AreSame(starter, sounds.Calls[0].Clip);
            Assert.AreSame(engine, audio.EngineSource.clip);
            Assert.IsTrue(audio.EngineSource.loop);
            audio.StopEngine();
            Assert.IsFalse(audio.EngineRunning);
        }

        [Test]
        public void Tick_SetsPitchAndVolume_ScaledByLevel()
        {
            audio.Level = 0.5f;
            audio.StartEngine();
            audio.Tick(1f, 0f, 0.02f, tuning);
            Assert.AreEqual(2f, audio.EngineSource.pitch, 1e-4f);
            Assert.AreEqual(0.45f, audio.EngineSource.volume, 1e-4f);
        }

        [Test]
        public void Horn_HasACooldown()
        {
            audio.Level = 0.8f;
            Assert.IsTrue(audio.Horn(tuning));
            Assert.IsFalse(audio.Horn(tuning));
            audio.Tick(0f, 0f, 0.5f, tuning);
            Assert.IsTrue(audio.Horn(tuning));
            Assert.AreEqual(2, sounds.Calls.Count);
            Assert.AreSame(horn, sounds.Calls[0].Clip);
            Assert.AreEqual(0.8f, sounds.Calls[0].Volume, 1e-5f);
        }

        [Test]
        public void Bump_ScalesWithStrengthAndLevel()
        {
            audio.Level = 0.5f;
            audio.Bump(0.6f);
            Assert.AreSame(bump, sounds.Calls[0].Clip);
            Assert.AreEqual(0.3f, sounds.Calls[0].Volume, 1e-5f);
        }

        [Test]
        public void Level_IsClamped()
        {
            audio.Level = 3f;
            Assert.AreEqual(1f, audio.Level);
            audio.Level = -1f;
            Assert.AreEqual(0f, audio.Level);
        }
    }
}
```

`Assets/Vehicles/Tests/MotorbikeVisualsTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.Vehicles.Tests
{
    public class MotorbikeVisualsTests
    {
        static readonly Color TailEmission = new Color(1f, 0.16f, 0.1f, 1f);

        VehicleFixture fx;
        MotorbikeVisuals visuals;
        Transform lean, bar, front, rear, rider;
        Light headlight;
        Renderer tail, lens;
        Material tailMaterial, lensMaterial;

        [SetUp]
        public void SetUp()
        {
            fx = new VehicleFixture();
            var root = fx.Track(new GameObject("Bike"));
            lean = Child("LeanPivot", root.transform);
            bar = Child("Handlebar", lean);
            front = Child("FrontWheel", bar);
            rear = Child("RearWheel", lean);
            rider = Child("Rider", lean);
            headlight = Child("Headlight", bar).gameObject.AddComponent<Light>();
            headlight.type = LightType.Spot;
            headlight.enabled = false;
            tailMaterial = fx.Track(EmissiveMaterial(TailEmission));
            lensMaterial = fx.Track(EmissiveMaterial(Color.white));
            tail = Child("TailLight", lean).gameObject.AddComponent<MeshRenderer>();
            tail.sharedMaterial = tailMaterial;
            lens = Child("HeadlightLens", bar).gameObject.AddComponent<MeshRenderer>();
            lens.sharedMaterial = lensMaterial;
            visuals = root.AddComponent<MotorbikeVisuals>();
            visuals.Configure(lean, bar, front, rear, rider, headlight, tail, lens);
            visuals.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            visuals.Shutdown();
            fx.Dispose();
        }

        static Transform Child(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static Material EmissiveMaterial(Color emission)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
            m.SetColor("_EmissionColor", emission);
            return m;
        }

        [Test]
        public void Initialize_HidesTheRiderAndTheHeadlight()
        {
            Assert.IsFalse(rider.gameObject.activeSelf);
            Assert.IsFalse(headlight.enabled);
            Assert.IsFalse(visuals.TailBright);
            Assert.IsFalse(visuals.LensOn);
        }

        [Test]
        public void Apply_SpinsWheels_SteersAndLeans()
        {
            visuals.Apply(3f, 20f, 10f, false, false, true, 0.1f);
            Assert.AreEqual(20f, bar.localEulerAngles.y, 1e-3f);
            Assert.AreEqual(350f, lean.localEulerAngles.z, 1e-3f, "miring kanan = rotasi z negatif");
            float expected = 3f / 0.3f * Mathf.Rad2Deg * 0.1f;
            Assert.AreEqual(expected, Quaternion.Angle(Quaternion.identity, front.localRotation), 0.01f);
            Assert.AreEqual(expected, Quaternion.Angle(Quaternion.identity, rear.localRotation), 0.01f);
            Assert.IsTrue(rider.gameObject.activeSelf);
        }

        [Test]
        public void Headlight_SwitchesTheLightAndTheLens()
        {
            visuals.Apply(0f, 0f, 0f, false, true, false, 0.02f);
            Assert.IsTrue(headlight.enabled);
            Assert.IsTrue(visuals.LensOn);
            Assert.AreNotSame(lensMaterial, lens.sharedMaterial);
            visuals.Apply(0f, 0f, 0f, false, false, false, 0.02f);
            Assert.IsFalse(headlight.enabled);
            Assert.IsFalse(visuals.LensOn);
        }

        [Test]
        public void TailLight_BrightensWhenBraking_WithoutTouchingTheMaterial()
        {
            visuals.Apply(5f, 0f, 0f, true, false, true, 0.02f);
            Assert.IsTrue(visuals.TailBright);
            Assert.AreNotSame(tailMaterial, tail.sharedMaterial);
            Assert.AreEqual(TailEmission, tail.sharedMaterial.GetColor("_EmissionColor"));
            Assert.AreNotEqual(0, (int)(tail.sharedMaterial.hideFlags & HideFlags.DontSave));
            visuals.Apply(5f, 0f, 0f, false, false, true, 0.02f);
            Assert.IsFalse(visuals.TailBright);
            Assert.AreEqual(TailEmission * 0.35f, tail.sharedMaterial.GetColor("_EmissionColor"));
            Assert.AreEqual(TailEmission, tailMaterial.GetColor("_EmissionColor"), "material asli tidak berubah");
        }

        [Test]
        public void Shutdown_RestoresTheOriginalMaterials()
        {
            visuals.Apply(5f, 0f, 0f, true, true, true, 0.02f);
            visuals.Shutdown();
            Assert.AreSame(tailMaterial, tail.sharedMaterial);
            Assert.AreSame(lensMaterial, lens.sharedMaterial);
        }
    }
}
```

- [ ] **Step 2: Jalankan tes, pastikan gagal**

Procedure R+T, `Group = "MotorbikeAudioTests|MotorbikeVisualsTests"`.
Expected: `COMPILE FAILED`.

- [ ] **Step 3: Implementasi visual**

`Assets/Vehicles/Runtime/Visuals/RuntimeEmission.cs`:
```csharp
using System;
using UnityEngine;

namespace TycoWorld.Vehicles
{
    /// <summary>Swaps one renderer material between two DontSave copies with low/high emission; the shared asset is never modified.</summary>
    public sealed class RuntimeEmission : IDisposable
    {
        const string EmissionKeyword = "_EMISSION";
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        readonly Renderer renderer;
        Material[] original, lowSet, highSet;
        Material lowCopy, highCopy;
        int shown = -1;

        public RuntimeEmission(Renderer target, int materialIndex, float lowFactor, float highFactor)
        {
            renderer = target;
            if (renderer == null) return;
            var shared = renderer.sharedMaterials;
            if (materialIndex < 0 || materialIndex >= shared.Length || shared[materialIndex] == null) return;
            var source = shared[materialIndex];
            var baseEmission = source.HasProperty(EmissionId) ? source.GetColor(EmissionId) : Color.white;
            lowCopy = Copy(source, baseEmission * lowFactor, " (Runtime Low)");
            highCopy = Copy(source, baseEmission * highFactor, " (Runtime High)");
            original = shared;
            lowSet = Replace(shared, materialIndex, lowCopy);
            highSet = Replace(shared, materialIndex, highCopy);
        }

        public bool IsHigh => shown == 1;
        public Material Low => lowCopy;
        public Material High => highCopy;

        public void Set(bool high)
        {
            if (original == null || renderer == null) return;
            int want = high ? 1 : 0;
            if (shown == want) return;
            renderer.sharedMaterials = high ? highSet : lowSet;
            shown = want;
        }

        public void Dispose()
        {
            if (renderer != null && original != null) renderer.sharedMaterials = original;
            DestroyCopy(ref lowCopy);
            DestroyCopy(ref highCopy);
            original = null;
            shown = -1;
        }

        static Material Copy(Material source, Color emission, string suffix)
        {
            var copy = new Material(source) { name = source.name + suffix, hideFlags = HideFlags.DontSave };
            copy.EnableKeyword(EmissionKeyword);
            copy.SetColor(EmissionId, emission);
            return copy;
        }

        static Material[] Replace(Material[] materials, int index, Material material)
        {
            var copy = (Material[])materials.Clone();
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

`Assets/Vehicles/Runtime/Visuals/MotorbikeVisuals.cs`:
```csharp
using UnityEngine;

namespace TycoWorld.Vehicles
{
    /// <summary>Wheels, handlebar, lean, headlight, tail light and the rider model of the bike (spec #5 §3); no logic of its own.</summary>
    [DisallowMultipleComponent]
    public sealed class MotorbikeVisuals : MonoBehaviour
    {
        const float WheelRadius = 0.3f;
        const float TailDim = 0.35f;

        [SerializeField] Transform leanPivot;
        [SerializeField] Transform handlebar;
        [SerializeField] Transform frontWheel;
        [SerializeField] Transform rearWheel;
        [SerializeField] Transform rider;
        [SerializeField] Light headlight;
        [SerializeField] Renderer tailLight;
        [SerializeField] Renderer headlightLens;

        RuntimeEmission tail, lens;
        float wheelAngle;
        bool initialized;

        public Transform LeanPivot => leanPivot;
        public Transform Handlebar => handlebar;
        public Transform FrontWheel => frontWheel;
        public Transform RearWheel => rearWheel;
        public Transform Rider => rider;
        public Light Headlight => headlight;
        public Renderer TailLight => tailLight;
        public Renderer HeadlightLens => headlightLens;
        public bool TailBright => tail != null && tail.IsHigh;
        public bool LensOn => lens != null && lens.IsHigh;

        public void Configure(Transform lean, Transform bar, Transform front, Transform rear, Transform riderModel, Light light,
            Renderer tailRenderer, Renderer lensRenderer)
        {
            leanPivot = lean;
            handlebar = bar;
            frontWheel = front;
            rearWheel = rear;
            rider = riderModel;
            headlight = light;
            tailLight = tailRenderer;
            headlightLens = lensRenderer;
        }

        void OnDestroy() => Shutdown();

        public void Initialize()
        {
            if (initialized) return;
            tail = new RuntimeEmission(tailLight, 0, TailDim, 1f);
            lens = new RuntimeEmission(headlightLens, 0, 0f, 1f);
            initialized = true;
            Apply(0f, 0f, 0f, false, false, false, 0f);
        }

        /// <param name="speed">m/s, signed.</param>
        /// <param name="steerAngle">Handlebar yaw in degrees.</param>
        /// <param name="lean">Degrees, positive leans right.</param>
        public void Apply(float speed, float steerAngle, float lean, bool braking, bool headlightOn, bool riderVisible, float dt)
        {
            if (!initialized) Initialize();
            wheelAngle = Mathf.Repeat(wheelAngle + speed / WheelRadius * Mathf.Rad2Deg * dt, 360f);
            var spin = Quaternion.Euler(wheelAngle, 0f, 0f);
            if (frontWheel != null) frontWheel.localRotation = spin;
            if (rearWheel != null) rearWheel.localRotation = spin;
            if (handlebar != null) handlebar.localRotation = Quaternion.Euler(0f, steerAngle, 0f);
            if (leanPivot != null) leanPivot.localRotation = Quaternion.Euler(0f, 0f, -lean);
            if (headlight != null && headlight.enabled != headlightOn) headlight.enabled = headlightOn;
            if (rider != null && rider.gameObject.activeSelf != riderVisible) rider.gameObject.SetActive(riderVisible);
            if (tail != null) tail.Set(braking);
            if (lens != null) lens.Set(headlightOn);
        }

        public void Shutdown()
        {
            if (tail != null) tail.Dispose();
            if (lens != null) lens.Dispose();
            tail = null;
            lens = null;
            initialized = false;
        }
    }
}
```

- [ ] **Step 4: Implementasi audio**

`Assets/Vehicles/Runtime/Audio/EngineSound.cs`:
```csharp
using UnityEngine;

namespace TycoWorld.Vehicles
{
    /// <summary>Engine loop pitch and volume from speed (0..1 of top speed) and throttle (spec #5 §4).</summary>
    public static class EngineSound
    {
        public static float Pitch(float speed01, float throttle, BikeTuning t) =>
            Mathf.Lerp(t.idlePitch, t.maxPitch, Mathf.Clamp01(speed01)) + t.throttlePitch * Mathf.Clamp01(throttle);

        public static float Volume(float speed01, BikeTuning t) => Mathf.Lerp(t.idleVolume, t.maxVolume, Mathf.Clamp01(speed01));
    }
}
```

`Assets/Vehicles/Runtime/Audio/MotorbikeAudio.cs`:
```csharp
using TycoWorld.Player;
using UnityEngine;

namespace TycoWorld.Vehicles
{
    /// <summary>
    /// Engine loop (pitch and volume follow speed), starter, horn and bump one-shots (spec #5 §7). Level is the
    /// settings "Efek" volume; the engine volume is rewritten every frame, so the generic effects volume skips these sources.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MotorbikeAudio : MonoBehaviour
    {
        [SerializeField] AudioSource engineSource;
        [SerializeField] AudioSource oneShotSource;
        [SerializeField] AudioClip engineClip;
        [SerializeField] AudioClip starterClip;
        [SerializeField] AudioClip hornClip;
        [SerializeField] AudioClip bumpClip;

        float level = 1f;
        float starterLeft;
        float sinceHorn = float.MaxValue;
        ISoundPlayer oneShots;

        public float Level
        {
            get => level;
            set => level = Mathf.Clamp01(value);
        }

        public bool EngineRunning { get; private set; }
        public AudioSource EngineSource => engineSource;
        public AudioSource OneShotSource => oneShotSource;

        /// <summary>Where one-shots go; defaults to the one-shot AudioSource. Tests swap in a recorder.</summary>
        public ISoundPlayer OneShots
        {
            get
            {
                if (oneShots == null && oneShotSource != null) oneShots = new AudioSourceSoundPlayer(oneShotSource);
                return oneShots;
            }
            set => oneShots = value;
        }

        public void Configure(AudioSource engine, AudioSource oneShot, AudioClip engineLoop, AudioClip starter, AudioClip horn, AudioClip bump)
        {
            engineSource = engine;
            oneShotSource = oneShot;
            engineClip = engineLoop;
            starterClip = starter;
            hornClip = horn;
            bumpClip = bump;
            oneShots = null;
        }

        public void StartEngine()
        {
            if (EngineRunning) return;
            EngineRunning = true;
            Play(starterClip, 1f);
            starterLeft = starterClip != null ? starterClip.length : 0f;
            if (engineSource == null) return;
            engineSource.clip = engineClip;
            engineSource.loop = true;
        }

        public void StopEngine()
        {
            EngineRunning = false;
            starterLeft = 0f;
            if (engineSource != null && engineSource.isPlaying) engineSource.Stop();
        }

        public void Tick(float speed01, float throttle, float dt, BikeTuning t)
        {
            if (sinceHorn < float.MaxValue) sinceHorn += dt;
            if (engineSource == null) return;
            engineSource.pitch = EngineSound.Pitch(speed01, throttle, t);
            engineSource.volume = EngineSound.Volume(speed01, t) * level;
            if (!EngineRunning) return;
            if (starterLeft > 0f)
            {
                starterLeft -= dt;
                return;
            }
            if (Application.isPlaying && engineSource.clip != null && !engineSource.isPlaying) engineSource.Play();
        }

        /// <summary>Honks unless the last honk was less than hornCooldown ago.</summary>
        public bool Horn(BikeTuning t)
        {
            if (sinceHorn < t.hornCooldown) return false;
            sinceHorn = 0f;
            Play(hornClip, 1f);
            return true;
        }

        public void Bump(float strength01) => Play(bumpClip, Mathf.Clamp01(strength01));

        void Play(AudioClip clip, float volume)
        {
            var player = OneShots;
            if (player != null && clip != null) player.Play(clip, volume * level, 1f);
        }
    }
}
```

- [ ] **Step 5: Jalankan tes, pastikan lulus**

Procedure R+T, `Group = "MotorbikeAudioTests|MotorbikeVisualsTests"`.
Expected: `passed=11 failed=0` (audio 6 + visual 5).

- [ ] **Step 6: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add Assets/Vehicles
git -C "D:/UNITY/project/Tyco World" commit -m "feat(vehicles): bike visuals with runtime light materials and engine audio

Co-Authored-By: <model> <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

---

### Task 8: `Motorbike` (orkestrator) dan `MotorbikeInteractable`

**Files:**
- Create: `Assets/Vehicles/Runtime/Motorbike.cs`
- Create: `Assets/Vehicles/Runtime/MotorbikeInteractable.cs`
- Create: `Assets/Vehicles/Tests/VehicleFixture.Bikes.cs`
- Test: `Assets/Vehicles/Tests/MotorbikeTests.cs`

**Interfaces:**
- Consumes:
  - Task 1: `VehicleIntent`, `IVehicleIntentSource`, `ScriptedVehicleIntentSource`, `VehicleInputReader`.
  - Task 2: `BikeTuning`, `BikeModel`.
  - Task 3: `FirstPersonPlayer.Seat/Unseat/IsSeated/CurrentSeat/Unseated`, `Flashlight.TurnOff`.
  - Task 4: `HudModel.SetVehicle`.
  - Task 5: `RideCamera`, `RideCameraMode`.
  - Task 6: `DismountFinder`.
  - Task 7: `MotorbikeVisuals`, `MotorbikeAudio`.
  - Player: `PlayerInputReader.ApplyLookSettings`, `PlayerLayers.QueryMask`, `Interactable`.
- Produces:
  - `Motorbike : MonoBehaviour` (`[RequireComponent(typeof(CharacterController))]`):
    - konstanta `SlowDownMessage`, `NoRoomMessage`, `MissingTuningWarning`;
    - `Configure(BikeTuning, Transform seat, MotorbikeVisuals, MotorbikeAudio)`;
    - status: `Tuning`, `Seat`, `Visuals`, `Sound`, `IsInitialized`, `Rider`, `IsMounted`, `Model`, `CameraState` (RideCamera), `IntentSourceOverride`, `Speed`, `SpeedKmh`, `Yaw`, `HeadlightOn`, `CanDismount`, `Bumps`, `HomePosition`;
    - `static ConfigureBody(CharacterController, BikeTuning)` (dipakai builder prefab di Task 10);
    - perintah: `Initialize()`, `SetIntentSource(IVehicleIntentSource)` (null = keyboard/gamepad), `Mount(FirstPersonPlayer) → bool`, `Dismount() → bool`, `Teleport(Vector3, float yaw)`, `ResetToHome()`, `Tick(float dt)`, `LateTick()`, `Shutdown()`.
  - `MotorbikeInteractable : Interactable` dengan `const string PromptText = "[E] Naik motor"`.
  - Tes: `VehicleFixture.CreatePlayer(Vector3 local, float yaw = 0)` dan `CreateBike(Vector3 local, float yaw, BikeTuning)`.

Urutan `Tick(dt)`:
1. `dt <= 0` → tidak melakukan apa pun (pause).
2. Intent dibaca (hanya saat ada pengendara).
3. Jeda Exit berkurang.
4. Tombol diproses: lampu, kamera, klakson, turun.
5. `BikeModel.Step`, lalu yaw.
6. Gerak: box cast lalu `CharacterController.Move` dengan gravitasi.
7. Visual, lalu audio.
8. Kamera dan HUD (kalau ada pengendara).
9. Cek jatuh di bawah dunia.

`LateTick()` menempatkan **kamera pemain** (`Rider.View`) sesuai mode kamera.

- [ ] **Step 1: Tulis fixture dan tes yang gagal**

`Assets/Vehicles/Tests/VehicleFixture.Bikes.cs`:
```csharp
using TycoWorld.Player;
using UnityEngine;

namespace TycoWorld.Vehicles.Tests
{
    internal sealed partial class VehicleFixture
    {
        /// <summary>An initialized first-person player on the Ignore Raycast layer, at Origin + local.</summary>
        public FirstPersonPlayer CreatePlayer(Vector3 local, float yaw = 0f)
        {
            var tuning = Track(ScriptableObject.CreateInstance<PlayerTuning>());
            var root = Track(new GameObject("Player"));
            root.layer = PlayerLayers.Player;
            root.transform.SetPositionAndRotation(Origin + local, Quaternion.Euler(0f, yaw, 0f));
            var pivot = new GameObject("CameraPivot").transform;
            pivot.SetParent(root.transform, false);
            pivot.localPosition = new Vector3(0f, 1.6f, 0f);
            var view = new GameObject("Camera").transform;
            view.SetParent(pivot, false);
            var player = root.AddComponent<FirstPersonPlayer>();
            player.Configure(tuning, pivot, view);
            player.Initialize();
            player.SetIntentSource(new ScriptedIntentSource());
            player.Teleport(Origin + local, yaw);
            return player;
        }

        /// <summary>A code-built bike (CharacterController, seat, interactable; no visuals or audio) at Origin + local.</summary>
        public Motorbike CreateBike(Vector3 local, float yaw, BikeTuning tuning)
        {
            var root = Track(new GameObject("Motorbike"));
            root.transform.SetPositionAndRotation(Origin + local, Quaternion.Euler(0f, yaw, 0f));
            root.AddComponent<CharacterController>();
            var seat = new GameObject("Seat").transform;
            seat.SetParent(root.transform, false);
            seat.localPosition = new Vector3(0f, 0.86f, -0.2f);
            var bike = root.AddComponent<Motorbike>();
            bike.Configure(tuning, seat, null, null);
            root.AddComponent<MotorbikeInteractable>();
            bike.Initialize();
            return bike;
        }
    }
}
```

`Assets/Vehicles/Tests/MotorbikeTests.cs`:
```csharp
using NUnit.Framework;
using TycoWorld.Player;
using UnityEngine;
using UnityEngine.TestTools;

namespace TycoWorld.Vehicles.Tests
{
    public class MotorbikeTests
    {
        const float Dt = 0.02f;
        VehicleFixture fx;
        BikeTuning tuning;
        FirstPersonPlayer player;
        Motorbike bike;
        ScriptedVehicleIntentSource input;

        static Vector3 O => VehicleFixture.Origin;
        static VehicleIntent Gas => new VehicleIntent { Throttle = 1f };
        static VehicleIntent ExitPress => new VehicleIntent { Exit = true };

        [SetUp]
        public void SetUp()
        {
            Interactable.ResetStatics();
            fx = new VehicleFixture();
            fx.Floor(800f);
            tuning = fx.Track(ScriptableObject.CreateInstance<BikeTuning>());
            player = fx.CreatePlayer(new Vector3(-3f, 0f, 0f));
            bike = fx.CreateBike(Vector3.zero, 0f, tuning);
            input = new ScriptedVehicleIntentSource();
            bike.SetIntentSource(input);
            fx.Sync();
        }

        [TearDown]
        public void TearDown()
        {
            bike.Shutdown();
            fx.Dispose();
            Interactable.ResetStatics();
        }

        void Run(float seconds, float dt = Dt)
        {
            int frames = Mathf.Max(1, Mathf.RoundToInt(seconds / dt));
            for (int i = 0; i < frames; i++)
            {
                bike.Tick(dt);
                player.Tick(dt);
                bike.LateTick();
            }
        }

        static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        [Test]
        public void Mount_SeatsThePlayer_LocksEverythingButLook()
        {
            Assert.IsTrue(bike.Mount(player));
            Assert.IsTrue(bike.IsMounted);
            Assert.AreSame(player, bike.Rider);
            Assert.IsTrue(player.IsSeated);
            Assert.AreSame(bike.Seat, player.CurrentSeat);
            Assert.IsFalse(player.Locks.Allows(PlayerAbility.Move));
            Assert.IsFalse(player.Locks.Allows(PlayerAbility.Interact));
            Assert.IsFalse(player.Locks.Allows(PlayerAbility.Flashlight));
            Assert.IsTrue(player.Locks.Allows(PlayerAbility.Look), "Look tidak dikunci supaya kursor bisa dikunci lagi setelah pause");
            Assert.IsFalse(player.Hud.CrosshairVisible);
            Assert.IsTrue(player.Hud.VehicleVisible);
        }

        [Test]
        public void Mount_Twice_OrWithoutAPlayer_Fails()
        {
            Assert.IsFalse(bike.Mount(null));
            Assert.IsTrue(bike.Mount(player));
            var other = fx.CreatePlayer(new Vector3(3f, 0f, 0f));
            Assert.IsFalse(bike.Mount(other));
            Assert.AreSame(player, bike.Rider);
        }

        [Test]
        public void Mount_TurnsTheFlashlightOff()
        {
            var go = fx.Track(new GameObject("Flashlight"));
            go.transform.SetParent(player.View, false);
            var flashlight = go.AddComponent<Flashlight>();
            flashlight.Initialize(player);
            flashlight.Toggle();
            Assert.IsTrue(flashlight.IsOn);
            bike.Mount(player);
            Assert.IsFalse(flashlight.IsOn);
        }

        [Test]
        public void FullThrottle_ReachesTopSpeed_AndDrivesStraight()
        {
            bike.Mount(player);
            input.Hold(Gas, 10f);
            Run(10f);
            Assert.AreEqual(60f, bike.SpeedKmh, 0.5f);
            Assert.Greater(bike.transform.position.z - O.z, 100f);
            Assert.AreEqual(O.x, bike.transform.position.x, 0.01f);
            Assert.AreEqual(60, player.Hud.VehicleSpeedKmh, 1);
            Assert.Less(Flat(bike.Seat.position, player.transform.position), 0.01f, "pemain ikut kursi");
        }

        [Test]
        public void Steering_TurnsTheBike()
        {
            bike.Mount(player);
            input.Hold(new VehicleIntent { Throttle = 1f, Steer = 1f }, 2f);
            Run(2f);
            Assert.Greater(bike.Yaw, 30f);
        }

        [Test]
        public void WallAhead_StopsWithoutPenetrating_EvenWithLongFrames()
        {
            fx.Box("Wall", new Vector3(0f, 1.5f, 30f), new Vector3(10f, 3f, 0.5f));
            fx.Sync();
            bike.Mount(player);
            input.Hold(Gas, 8f);
            Run(8f, 0.1f);
            float front = bike.transform.position.z + tuning.blockHalfExtents.z;
            Assert.LessOrEqual(front, O.z + 29.75f + 0.01f);
            Assert.AreEqual(0f, bike.Speed, 1e-3f);
            Assert.GreaterOrEqual(bike.Bumps, 1);
        }

        [Test]
        public void GlancingWall_SlidesAlong()
        {
            fx.Box("Wall", new Vector3(3f, 1.5f, 15f), new Vector3(0.5f, 3f, 40f), -20f);
            fx.Sync();
            bike.Mount(player);
            input.Hold(Gas, 6f);
            Run(6f);
            Assert.Greater(bike.Speed, 2f, "menyerong tidak berhenti");
            Assert.AreEqual(0, bike.Bumps);
        }

        [Test]
        public void ExitPressedRightAfterMounting_IsIgnored()
        {
            bike.Mount(player);
            input.Press(ExitPress);
            Run(Dt);
            Assert.IsTrue(bike.IsMounted, "E yang sama tidak boleh langsung menurunkan");
            Run(0.4f);
            input.Press(ExitPress);
            Run(Dt);
            Assert.IsFalse(bike.IsMounted);
        }

        [Test]
        public void Dismount_WhenFast_ShowsAMessageAndStaysSeated()
        {
            bike.Mount(player);
            input.Hold(Gas, 2f);
            Run(2f);
            Assert.IsFalse(player.Hud.VehicleCanExit);
            input.Press(ExitPress);
            Run(Dt);
            Assert.IsTrue(bike.IsMounted);
            Assert.AreEqual(Motorbike.SlowDownMessage, player.Hud.Message);
        }

        [Test]
        public void Dismount_WhenStopped_PutsThePlayerOnTheLeft_AndFreesEverything()
        {
            bike.Mount(player);
            Run(0.5f);
            Assert.IsTrue(player.Hud.VehicleCanExit);
            Assert.IsTrue(bike.Dismount());
            Assert.IsFalse(bike.IsMounted);
            Assert.IsFalse(player.IsSeated);
            Assert.Less(Flat(bike.transform.position - bike.transform.right * 1.1f, player.transform.position), 0.05f);
            Assert.IsTrue(player.Locks.Allows(PlayerAbility.All));
            Assert.IsTrue(player.GetComponent<CharacterController>().enabled);
            Assert.IsFalse(player.Hud.VehicleVisible);
            Assert.IsTrue(player.Hud.CrosshairVisible);
            Assert.IsFalse(bike.HeadlightOn);
        }

        [Test]
        public void Dismount_NoRoom_ShowsAMessageAndStaysSeated()
        {
            fx.Box("WallLeft", new Vector3(-1.1f, 1f, 0f), new Vector3(0.4f, 2f, 3f));
            fx.Box("WallRight", new Vector3(1.1f, 1f, 0f), new Vector3(0.4f, 2f, 3f));
            fx.Sync();
            bike.Mount(player);
            Run(0.5f);
            Assert.IsFalse(bike.Dismount());
            Assert.IsTrue(bike.IsMounted);
            Assert.AreEqual(Motorbike.NoRoomMessage, player.Hud.Message);
        }

        [Test]
        public void RiderTeleported_ReleasesTheBike()
        {
            bike.Mount(player);
            player.Teleport(O + new Vector3(10f, 0f, 0f), 0f);
            Assert.IsFalse(bike.IsMounted);
            Assert.IsTrue(player.Locks.Allows(PlayerAbility.Move));
            Assert.IsFalse(player.Hud.VehicleVisible);
        }

        [Test]
        public void Shutdown_WhileRidden_FreesThePlayer()
        {
            bike.Mount(player);
            bike.Shutdown();
            Assert.IsFalse(player.IsSeated);
            Assert.IsFalse(bike.IsMounted);
            Assert.IsTrue(player.Locks.Allows(PlayerAbility.All));
        }

        [Test]
        public void ZeroDt_DoesNothing()
        {
            bike.Mount(player);
            input.Hold(Gas, 5f);
            var before = bike.transform.position;
            bike.Tick(0f);
            Assert.AreEqual(before, bike.transform.position);
            Assert.AreEqual(0f, bike.Speed);
        }

        [Test]
        public void Buttons_ToggleTheHeadlightAndTheCamera()
        {
            bike.Mount(player);
            Run(0.4f);
            input.Press(new VehicleIntent { ToggleHeadlight = true, ToggleCamera = true });
            Run(Dt);
            Assert.IsTrue(bike.HeadlightOn);
            Assert.IsTrue(player.Hud.VehicleHeadlight);
            Assert.AreEqual(RideCameraMode.FirstPerson, bike.CameraState.Mode);
        }

        [Test]
        public void LateTick_PutsTheCameraBehindAndAboveTheBike()
        {
            bike.Mount(player);
            Run(0.1f);
            var cam = player.View.position;
            Assert.Less(cam.z, bike.transform.position.z - 4f);
            Assert.Greater(cam.y, bike.transform.position.y + 1.5f);
        }

        [Test]
        public void FellBelowTheWorld_ReturnsHome_AndDropsTheRider()
        {
            tuning.respawnBelowY = O.y - 20f;
            bike.Mount(player);
            bike.Teleport(O + new Vector3(500f, -30f, 0f), 0f);
            bike.Tick(Dt);
            Assert.Less(Flat(O, bike.transform.position), 0.01f);
            Assert.IsFalse(bike.IsMounted);
            Assert.IsFalse(player.IsSeated);
        }

        [Test]
        public void Interactable_MountsAndIsUnavailableWhileRidden()
        {
            var interactable = bike.GetComponent<MotorbikeInteractable>();
            var context = new InteractionContext(player, player.Hud, player.View.position);
            Assert.AreEqual("[E] Naik motor", interactable.GetPrompt(context));
            Assert.IsTrue(interactable.CanInteract(context));
            Assert.IsTrue(interactable.Interact(context));
            Assert.IsTrue(bike.IsMounted);
            Assert.IsFalse(interactable.CanInteract(context));
        }

        [Test]
        public void MissingTuning_UsesDefaultsWithOneWarning()
        {
            var root = fx.Track(new GameObject("BareBike"));
            root.transform.position = O + new Vector3(20f, 0f, 0f);
            root.AddComponent<CharacterController>();
            var bare = root.AddComponent<Motorbike>();
            LogAssert.Expect(LogType.Warning, Motorbike.MissingTuningWarning);
            bare.Initialize();
            Assert.IsNotNull(bare.Tuning);
            Assert.DoesNotThrow(() => bare.Tick(Dt));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
```

- [ ] **Step 2: Jalankan tes, pastikan gagal**

Procedure R+T, `Group = "MotorbikeTests"`.
Expected: `COMPILE FAILED` (`Motorbike` belum ada).

- [ ] **Step 3: Implementasi**

`Assets/Vehicles/Runtime/Motorbike.cs`:
```csharp
using TycoWorld.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TycoWorld.Vehicles
{
    /// <summary>
    /// The rideable motorbike (spec #5 §3): mount and dismount, arcade motion with a CharacterController plus a box
    /// cast that stops the long body at walls, the ride camera, visuals, audio, headlight and the riding HUD.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public sealed class Motorbike : MonoBehaviour
    {
        public const string SlowDownMessage = "Pelankan motor dulu.";
        public const string NoRoomMessage = "Tidak ada ruang untuk turun.";
        public const string MissingTuningWarning = "[Vehicle] BikeTuning kosong; memakai nilai default.";
        const float Skin = 0.05f;
        const PlayerAbility RiderLocks = PlayerAbility.Move | PlayerAbility.Interact | PlayerAbility.Flashlight;

        [SerializeField] BikeTuning tuning;
        [SerializeField] Transform seat;
        [SerializeField] MotorbikeVisuals visuals;
        [SerializeField] MotorbikeAudio sound;

        CharacterController body;
        BikeModel model;
        RideCamera rideCamera;
        IVehicleIntentSource scriptedSource;
        VehicleInputReader reader;
        float verticalSpeed, exitGuard, steer;
        bool touchingWall;
        Vector3 homePosition;
        float homeYaw;

        public BikeTuning Tuning => tuning;
        public Transform Seat => seat;
        public MotorbikeVisuals Visuals => visuals;
        public MotorbikeAudio Sound => sound;
        public bool IsInitialized { get; private set; }
        public FirstPersonPlayer Rider { get; private set; }
        public bool IsMounted => Rider != null;
        public BikeModel Model => model;
        public RideCamera CameraState => rideCamera;
        public IVehicleIntentSource IntentSourceOverride => scriptedSource;
        public float Speed => model != null ? model.Speed : 0f;
        public float SpeedKmh => Speed * BikeTuning.KmhPerMs;
        public float Yaw => transform.eulerAngles.y;
        public bool HeadlightOn { get; private set; }
        public bool CanDismount => IsMounted && Mathf.Abs(Speed) <= tuning.dismountMaxSpeed;
        /// <summary>Head-on wall hits above bumpSpeed since Initialize.</summary>
        public int Bumps { get; private set; }
        public Vector3 HomePosition => homePosition;

        public void Configure(BikeTuning bikeTuning, Transform seatTransform, MotorbikeVisuals bikeVisuals, MotorbikeAudio bikeAudio)
        {
            tuning = bikeTuning;
            seat = seatTransform;
            visuals = bikeVisuals;
            sound = bikeAudio;
        }

        void Awake() => Initialize();

        void Update()
        {
            if (Application.isPlaying) Tick(Time.deltaTime);
        }

        void LateUpdate()
        {
            if (Application.isPlaying) LateTick();
        }

        void OnDestroy() => Shutdown();

        public void Initialize()
        {
            if (IsInitialized) return;
            if (tuning == null)
            {
                Debug.LogWarning(MissingTuningWarning);
                tuning = ScriptableObject.CreateInstance<BikeTuning>();
                tuning.hideFlags = HideFlags.DontSave;
            }
            body = GetComponent<CharacterController>();
            ConfigureBody(body, tuning);
            model = new BikeModel(tuning);
            rideCamera = new RideCamera(tuning);
            homePosition = transform.position;
            homeYaw = transform.eulerAngles.y;
            if (visuals != null) visuals.Initialize();
            IsInitialized = true;
        }

        /// <summary>CharacterController shape from the tuning (spec #5 §4); the prefab builder uses it too.</summary>
        public static void ConfigureBody(CharacterController controller, BikeTuning t)
        {
            controller.radius = t.bodyRadius;
            controller.height = t.bodyHeight;
            controller.center = new Vector3(0f, t.bodyHeight * 0.5f, 0f);
            controller.slopeLimit = t.slopeLimit;
            controller.stepOffset = t.stepOffset;
            controller.minMoveDistance = 0f;
        }

        /// <summary>Scripted input for tests and MCP verification; null restores the keyboard/gamepad reader.</summary>
        public void SetIntentSource(IVehicleIntentSource source) => scriptedSource = source;

        public bool Mount(FirstPersonPlayer player)
        {
            if (!IsInitialized) Initialize();
            if (IsMounted || player == null || !player.IsInitialized || seat == null) return false;
            Rider = player;
            player.Locks.Acquire(this, RiderLocks);
            var flashlight = player.GetComponentInChildren<Flashlight>(true);
            if (flashlight != null) flashlight.TurnOff();
            player.Seat(seat);
            player.Unseated += OnRiderUnseated;
            player.Hud.CrosshairVisible = false;
            player.Hud.SetVehicle(true, SpeedKmh, HeadlightOn, CanDismount);
            exitGuard = tuning.exitGuardSeconds;
            rideCamera.Reset(Yaw);
            if (sound != null) sound.StartEngine();
            return true;
        }

        public bool Dismount()
        {
            if (!IsMounted) return false;
            var player = Rider;
            if (!CanDismount)
            {
                player.Hud.ShowMessage(SlowDownMessage);
                return false;
            }
            var stance = player.Tuning.body;
            if (!DismountFinder.TryFind(transform, tuning.dismountSideOffset, stance.radius, stance.height, out var feet))
            {
                player.Hud.ShowMessage(NoRoomMessage);
                return false;
            }
            player.Unseat(feet, Yaw);
            return true;
        }

        /// <summary>Moves the bike instantly (autoSyncTransforms is off) and stops it.</summary>
        public void Teleport(Vector3 position, float yawDegrees)
        {
            if (!IsInitialized) Initialize();
            body.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yawDegrees, 0f));
            model.Reset();
            verticalSpeed = 0f;
            touchingWall = false;
            Physics.SyncTransforms();
            body.enabled = true;
            rideCamera.Reset(yawDegrees);
        }

        /// <summary>Back to the parking pose the bike had when the scene started; a rider is set down beside it.</summary>
        public void ResetToHome()
        {
            var player = Rider;
            Teleport(homePosition, homeYaw);
            if (player == null) return;
            var stance = player.Tuning.body;
            var feet = DismountFinder.TryFind(transform, tuning.dismountSideOffset, stance.radius, stance.height, out var spot)
                ? spot
                : transform.position - transform.right * tuning.dismountSideOffset;
            player.Unseat(feet, Yaw);
        }

        public void Tick(float dt)
        {
            if (!IsInitialized) Initialize();
            if (dt <= 0f) return;
            var intent = IsMounted ? ReadIntent(dt) : default;
            if (exitGuard > 0f) exitGuard -= dt;
            if (IsMounted) HandleButtons(intent);
            model.Step(intent, dt);
            steer = Mathf.Clamp(intent.Steer, -1f, 1f);
            float yawDelta = model.YawRate(steer) * dt;
            if (yawDelta != 0f) transform.rotation = Quaternion.Euler(0f, Yaw + yawDelta, 0f);
            Move(dt);
            bool riderVisible = IsMounted && rideCamera.Mode == RideCameraMode.ThirdPerson;
            if (visuals != null) visuals.Apply(model.Speed, steer * tuning.maxSteerAngle, model.Lean, model.Braking, HeadlightOn, riderVisible, dt);
            if (sound != null) sound.Tick(Mathf.Abs(model.Speed) / tuning.maxForwardSpeed, Mathf.Max(0f, intent.Throttle), dt, tuning);
            if (IsMounted)
            {
                rideCamera.Update(intent.LookDegrees, Yaw, model.Speed, dt);
                Rider.Hud.SetVehicle(true, SpeedKmh, HeadlightOn, CanDismount);
            }
            if (transform.position.y < tuning.respawnBelowY) ResetToHome();
        }

        /// <summary>Places the rider's camera (third-person chase or first-person head); call after every Tick of the frame.</summary>
        public void LateTick()
        {
            if (!IsMounted || Rider.View == null) return;
            Vector3 position;
            Quaternion rotation;
            if (rideCamera.Mode == RideCameraMode.ThirdPerson) rideCamera.PoseThirdPerson(transform.position, out position, out rotation);
            else rideCamera.PoseFirstPerson(seat.position, Yaw, model.Lean, out position, out rotation);
            Rider.View.SetPositionAndRotation(position, rotation);
        }

        /// <summary>Frees the rider (bike destroyed, scene unloaded) so the player is never left locked on a missing seat.</summary>
        public void Shutdown()
        {
            if (IsInitialized && Rider != null)
            {
                var player = Rider;
                if (player.IsSeated) player.Unseat(transform.position - transform.right * tuning.dismountSideOffset, Yaw);
                else ReleaseRider();
            }
            if (reader != null) reader.Disable();
        }

        void HandleButtons(VehicleIntent intent)
        {
            if (intent.ToggleHeadlight) HeadlightOn = !HeadlightOn;
            if (intent.ToggleCamera) rideCamera.Toggle();
            if (intent.Horn && sound != null) sound.Horn(tuning);
            if (intent.Exit && exitGuard <= 0f) Dismount();
        }

        VehicleIntent ReadIntent(float dt)
        {
            if (scriptedSource != null) return scriptedSource.Read(dt);
            var input = Reader;
            if (input == null) return default;
            input.CursorLocked = Cursor.lockState == CursorLockMode.Locked;
            var intent = input.Read(dt);
            intent.LookDegrees = PlayerInputReader.ApplyLookSettings(intent.LookDegrees, Rider.LookSensitivity, Rider.InvertY);
            return intent;
        }

        VehicleInputReader Reader
        {
            get
            {
                if (reader == null && Application.isPlaying && InputSystem.actions != null && Rider != null)
                    reader = new VehicleInputReader(InputSystem.actions, Rider.Tuning.look.degreesPerPixel);
                if (reader != null && IsMounted && !reader.IsEnabled) reader.Enable();
                return reader;
            }
        }

        void Move(float dt)
        {
            var motion = transform.forward * (model.Speed * dt);
            if (motion.sqrMagnitude > 1e-10f) motion = Block(motion);
            else touchingWall = false;
            if (body.isGrounded && verticalSpeed < 0f) verticalSpeed = -1f;
            verticalSpeed -= tuning.gravity * dt;
            body.Move(motion + Vector3.up * (verticalSpeed * dt));
        }

        /// <summary>Box cast of the long body along the motion: head-on stops at the wall, glancing slides along it (speed × glanceSpeedFactor once per contact).</summary>
        Vector3 Block(Vector3 motion)
        {
            float distance = motion.magnitude;
            var direction = motion / distance;
            var center = transform.position + Vector3.up * (tuning.stepOffset + tuning.blockHalfExtents.y);
            if (!Physics.BoxCast(center, tuning.blockHalfExtents, direction, out var hit, transform.rotation, distance + Skin,
                    PlayerLayers.QueryMask, QueryTriggerInteraction.Ignore))
            {
                touchingWall = false;
                return motion;
            }
            if (Vector3.Dot(hit.normal, -direction) > tuning.headOnDot)
            {
                float impact = Mathf.Abs(model.Speed);
                if (impact > tuning.bumpSpeed)
                {
                    Bumps++;
                    if (sound != null) sound.Bump(impact / tuning.maxForwardSpeed);
                }
                model.Stop();
                touchingWall = true;
                return direction * Mathf.Max(0f, hit.distance - Skin);
            }
            if (!touchingWall) model.Scale(tuning.glanceSpeedFactor);
            touchingWall = true;
            var slide = Vector3.ProjectOnPlane(motion, hit.normal);
            slide.y = 0f;
            return slide;
        }

        void OnRiderUnseated() => ReleaseRider();

        void ReleaseRider()
        {
            var player = Rider;
            if (player == null) return;
            player.Unseated -= OnRiderUnseated;
            Rider = null;
            player.Locks.Release(this);
            player.Hud.SetVehicle(false, 0f, false, false);
            player.Hud.CrosshairVisible = true;
            HeadlightOn = false;
            if (reader != null) reader.Disable();
            if (sound != null) sound.StopEngine();
            if (visuals != null) visuals.Apply(model.Speed, 0f, model.Lean, false, false, false, 0f);
        }
    }
}
```

`Assets/Vehicles/Runtime/MotorbikeInteractable.cs`:
```csharp
using TycoWorld.Player;
using UnityEngine;

namespace TycoWorld.Vehicles
{
    /// <summary>"[E] Naik motor" on the parked bike (spec #5 §6).</summary>
    [RequireComponent(typeof(Motorbike))]
    public sealed class MotorbikeInteractable : Interactable
    {
        public const string PromptText = "[E] Naik motor";

        Motorbike Bike => GetComponent<Motorbike>();

        public override string GetPrompt(in InteractionContext context) => PromptText;

        public override bool CanInteract(in InteractionContext context)
        {
            var bike = Bike;
            return bike != null && !bike.IsMounted && context.Player != null;
        }

        protected override void OnInteract(in InteractionContext context) => Bike.Mount(context.Player);
    }
}
```

- [ ] **Step 4: Jalankan tes, pastikan lulus**

Procedure R+T, `Group = "MotorbikeTests"`.
Expected: `passed=19 failed=0`.

Kalau `GlancingWall_SlidesAlong` gagal karena geometri uji (bukan logika), periksa posisi motor dan arah normal tembok di laporan. Jangan melonggarkan aturan head-on (`headOnDot`); yang boleh disesuaikan hanya sudut tembok uji, asalkan tetap jelas menyerong (≤ 30°).

- [ ] **Step 5: Regresi Vehicles dan Player**

Procedure R+T, `Group = "TycoWorld.Vehicles.Tests|TycoWorld.Player.Tests"`.
Expected: `failed=0`.

- [ ] **Step 6: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add Assets/Vehicles
git -C "D:/UNITY/project/Tyco World" commit -m "feat(vehicles): rideable motorbike with mount, dismount, wall blocking and ride camera

Co-Authored-By: <model> <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

---

### Task 9: Audio motor (CC0 dulu, sintesis sebagai cadangan)

**Files:**
- Create: `Assets/Vehicles/Editor/EngineSynth.cs`
- Create: `Assets/Vehicles/Editor/VehicleAudioBuilder.cs`
- Create: `Assets/Vehicles/Editor/VehicleAudioImportRules.cs`
- Create: `Assets/Vehicles/Audio/SOURCES.md` + klip audio (rekaman CC0 dan/atau `*_synth.wav`)
- Test: `Assets/Vehicles/Tests/EngineSynthTests.cs`, `Assets/Vehicles/Tests/VehicleAudioAssetsTests.cs`

**Interfaces:**
- Consumes: `AmbienceSynth.Loop/Normalize/SampleRate`, `WavWriter.Encode` (DayCycle.Editor), `WorldGenPaths.ToAbsolute`, `AssetFolders.Ensure` (WorldGen.Editor), `VehiclePaths` (Task 1).
- Produces:
  - `EngineSynth`: `SampleRate` (22050), `EngineLoop(float seconds = 1f)`, `Starter(float seconds = 0.9f)`, `Horn()`, dan `Bump(float seconds = 0.3f)`. Semuanya mengembalikan `float[]` mono yang deterministik.
  - `VehicleAudioBuilder`:
    - nama dasar `EngineName = "motor_engine_loop"`, `StarterName = "motor_starter"`, `HornName = "motor_horn"`, `BumpName = "motor_bump"`;
    - `Resolve(string baseName) → AudioClip` (rekaman `.ogg`/`.wav`/`.mp3` dulu, lalu `<name>_synth.wav`);
    - `HasRecording(string baseName)`, `WriteMissingClips()`.
  - `VehicleAudioImportRules`: semua audio di `Assets/Vehicles/Audio` dibuat mono, DecompressOnLoad, Vorbis kualitas 0,7.

- [ ] **Step 1: Cari rekaman CC0 (maksimal ±20 menit)**

Untuk tiap nama dasar (mesin langsam yang bisa di-loop, starter, klakson motor/skuter, benturan):
1. **Sumber pertama:** OpenGameArt advanced search dengan filter lisensi CC0. Kata kunci: `motorcycle`, `motorbike`, `scooter`, `engine loop`, `horn`, `car horn`, `impact thud`. Contoh: `https://opengameart.org/art-search-advanced?keys=motorcycle&field_art_type_tid%5B%5D=13&field_art_licenses_tid%5B%5D=4`.
2. **Hanya terima** kalau halaman aset menulis lisensi **CC0** secara eksplisit. CC-BY, OGA-BY, GPL, atau lisensi yang tidak jelas ditolak.
3. **Cara mengunduh:**
   - unduh ke **folder kosong baru** di scratchpad-mu;
   - jangan menjalankan apa pun dari folder itu;
   - periksa jenis file dan durasinya;
   - hanya file audio (`.ogg`, `.wav`, `.mp3`) yang disalin ke `Assets/Vehicles/Audio/<nama dasar><ekstensi asli>`.
   - Contoh: `Assets/Vehicles/Audio/motor_engine_loop.ogg`.
4. Mesin harus berupa langsam yang bisa di-loop, panjang 0,5–5 s, tanpa musik atau suara lain.

Kalau tidak menemukan yang layak, tidak apa-apa: klip itu memakai sintesis (Step 4–5). Catat semua yang kamu cek di laporan.

- [ ] **Step 2: Tulis tes yang gagal**

`Assets/Vehicles/Tests/EngineSynthTests.cs`:
```csharp
using System;
using NUnit.Framework;
using TycoWorld.Vehicles.Authoring;

namespace TycoWorld.Vehicles.Tests
{
    public class EngineSynthTests
    {
        static float Rms(float[] s, int from, int to)
        {
            double sum = 0;
            for (int i = from; i < to; i++) sum += s[i] * s[i];
            return (float)Math.Sqrt(sum / Math.Max(1, to - from));
        }

        static float Peak(float[] s)
        {
            float max = 0f;
            foreach (var v in s) max = Math.Max(max, Math.Abs(v));
            return max;
        }

        [Test]
        public void EngineLoop_IsDeterministicSeamlessAndCentred()
        {
            var a = EngineSynth.EngineLoop();
            var b = EngineSynth.EngineLoop();
            CollectionAssert.AreEqual(a, b);
            Assert.AreEqual(EngineSynth.SampleRate, a.Length);
            Assert.Less(Math.Abs(a[0] - a[a.Length - 1]), 0.2f, "sambungan loop halus");
            Assert.That(Peak(a), Is.InRange(0.5f, 0.8001f));
            double mean = 0;
            foreach (var v in a) mean += v;
            Assert.Less(Math.Abs(mean / a.Length), 0.02, "tanpa DC offset");
        }

        [Test]
        public void Starter_RampsUpAndFadesOut()
        {
            var s = EngineSynth.Starter();
            Assert.AreEqual((int)(0.9f * EngineSynth.SampleRate), s.Length);
            Assert.Less(Rms(s, 0, s.Length / 20), Rms(s, s.Length / 2, s.Length * 3 / 4));
            Assert.Less(Math.Abs(s[s.Length - 1]), 0.05f);
        }

        [Test]
        public void Horn_HasTwoBeepsWithASilentGap()
        {
            var s = EngineSynth.Horn();
            int beep = (int)(0.22f * EngineSynth.SampleRate);
            int gap = (int)(0.08f * EngineSynth.SampleRate);
            Assert.Greater(Rms(s, beep / 4, beep * 3 / 4), 0.1f);
            Assert.Less(Rms(s, beep + gap / 4, beep + gap * 3 / 4), 0.01f);
            Assert.Greater(Rms(s, beep + gap + beep / 4, beep + gap + beep * 3 / 4), 0.1f);
        }

        [Test]
        public void Bump_DecaysQuickly()
        {
            var s = EngineSynth.Bump();
            int tenth = s.Length / 10;
            Assert.Less(Rms(s, s.Length - tenth, s.Length), Rms(s, 0, tenth) * 0.2f);
        }
    }
}
```

`Assets/Vehicles/Tests/VehicleAudioAssetsTests.cs`:
```csharp
using System.IO;
using NUnit.Framework;
using TycoWorld.Vehicles.Authoring;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;

namespace TycoWorld.Vehicles.Tests
{
    public class VehicleAudioAssetsTests
    {
        static readonly string[] Names =
            { VehicleAudioBuilder.EngineName, VehicleAudioBuilder.StarterName, VehicleAudioBuilder.HornName, VehicleAudioBuilder.BumpName };

        [Test]
        public void EveryClipResolves_AsMono()
        {
            foreach (var name in Names)
            {
                var clip = VehicleAudioBuilder.Resolve(name);
                Assert.IsNotNull(clip, name);
                var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip));
                Assert.IsTrue(importer.forceToMono, name);
                Assert.AreEqual(UnityEngine.AudioClipLoadType.DecompressOnLoad, importer.defaultSampleSettings.loadType, name);
            }
        }

        [Test]
        public void SourcesFile_ListsEveryClip()
        {
            string text = File.ReadAllText(WorldGenPaths.ToAbsolute(VehiclePaths.AudioSourcesFile));
            foreach (var name in Names) StringAssert.Contains(name, text);
        }

        [Test]
        public void WriteMissingClips_DoesNotOverwrite()
        {
            string path = AssetDatabase.GetAssetPath(VehicleAudioBuilder.Resolve(VehicleAudioBuilder.EngineName));
            var before = File.GetLastWriteTimeUtc(WorldGenPaths.ToAbsolute(path));
            VehicleAudioBuilder.WriteMissingClips();
            Assert.AreEqual(before, File.GetLastWriteTimeUtc(WorldGenPaths.ToAbsolute(path)));
        }
    }
}
```

- [ ] **Step 3: Jalankan tes, pastikan gagal**

Procedure R+T, `Group = "EngineSynthTests|VehicleAudioAssetsTests"`.
Expected: `COMPILE FAILED`.

- [ ] **Step 4: Implementasi**

`Assets/Vehicles/Editor/EngineSynth.cs`:
```csharp
using System;
using TycoWorld.DayCycle.Authoring;

namespace TycoWorld.Vehicles.Authoring
{
    /// <summary>Deterministic placeholder motorbike sounds (spec #5 §7), used when no CC0 recording was found.</summary>
    public static class EngineSynth
    {
        public const int SampleRate = AmbienceSynth.SampleRate;
        const float Peak = 0.8f;
        const double BurstWidth = 0.15;
        const double BurstMean = BurstWidth * 0.5;

        /// <summary>Single-cylinder idle at 55 Hz (smooth firing bursts, two harmonics, a little noise) as a seamless loop.</summary>
        public static float[] EngineLoop(float seconds = 1f)
        {
            var rng = new Random(4021);
            int fade = SampleRate / 20;
            var raw = new float[(int)(seconds * SampleRate) + fade];
            const double f = 55.0;
            for (int i = 0; i < raw.Length; i++)
            {
                double t = (double)i / SampleRate;
                double phase = t * f - Math.Floor(t * f);
                raw[i] = (float)(0.7 * Burst(phase) + 0.25 * Math.Sin(2.0 * Math.PI * f * t)
                                 + 0.15 * Math.Sin(4.0 * Math.PI * f * t) + 0.04 * (rng.NextDouble() * 2.0 - 1.0));
            }
            var loop = AmbienceSynth.Loop(raw, fade);
            RemoveDc(loop);
            AmbienceSynth.Normalize(loop, Peak);
            return loop;
        }

        /// <summary>Zero-mean firing bursts that speed up from 18 to 55 Hz, fade in quickly and fade out to silence at the end.</summary>
        public static float[] Starter(float seconds = 0.9f)
        {
            var rng = new Random(733);
            var samples = new float[(int)(seconds * SampleRate)];
            double cycles = 0.0;
            for (int i = 0; i < samples.Length; i++)
            {
                double k = (double)i / samples.Length;
                cycles += (18.0 + 37.0 * k) / SampleRate;
                double phase = cycles - Math.Floor(cycles);
                double envelope = Math.Min(1.0, k * 4.0) * (k > 0.85 ? (1.0 - k) / 0.15 : 1.0);
                samples[i] = (float)(envelope * (0.8 * (Burst(phase) - BurstMean) + 0.1 * (rng.NextDouble() * 2.0 - 1.0)));
            }
            AmbienceSynth.Normalize(samples, Peak);
            return samples;
        }

        /// <summary>"Tin-tin": two 0.22 s two-tone beeps (420 + 525 Hz, softly clipped) with a 0.08 s gap.</summary>
        public static float[] Horn()
        {
            int beep = (int)(0.22f * SampleRate);
            int gap = (int)(0.08f * SampleRate);
            var samples = new float[beep * 2 + gap];
            AddBeep(samples, 0, beep);
            AddBeep(samples, beep + gap, beep);
            AmbienceSynth.Normalize(samples, Peak);
            return samples;
        }

        /// <summary>A low thud: 70 Hz sine and a noise burst, both decaying fast.</summary>
        public static float[] Bump(float seconds = 0.3f)
        {
            var rng = new Random(977);
            var samples = new float[(int)(seconds * SampleRate)];
            for (int i = 0; i < samples.Length; i++)
            {
                double t = (double)i / SampleRate;
                samples[i] = (float)(Math.Exp(-t * 18.0) * Math.Sin(2.0 * Math.PI * 70.0 * t)
                                     + 0.5 * Math.Exp(-t * 40.0) * (rng.NextDouble() * 2.0 - 1.0));
            }
            AmbienceSynth.Normalize(samples, Peak);
            return samples;
        }

        /// <summary>Raised-cosine firing burst over the first 15 % of a cycle, zero elsewhere: continuous, so the loop has no clicks.</summary>
        static double Burst(double phase) => phase < BurstWidth ? 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * phase / BurstWidth)) : 0.0;

        static void AddBeep(float[] samples, int start, int length)
        {
            int edge = SampleRate / 100;
            for (int i = 0; i < length && start + i < samples.Length; i++)
            {
                double t = (double)i / SampleRate;
                double envelope = Math.Min(1.0, Math.Min(i, length - 1 - i) / (double)edge);
                double tone = Math.Tanh(3.0 * Math.Sin(2.0 * Math.PI * 420.0 * t)) + Math.Tanh(3.0 * Math.Sin(2.0 * Math.PI * 525.0 * t));
                samples[start + i] += (float)(0.5 * envelope * tone);
            }
        }

        static void RemoveDc(float[] samples)
        {
            double mean = 0;
            foreach (var s in samples) mean += s;
            mean /= samples.Length;
            for (int i = 0; i < samples.Length; i++) samples[i] -= (float)mean;
        }
    }
}
```

`Assets/Vehicles/Editor/VehicleAudioBuilder.cs`:
```csharp
using System;
using System.IO;
using TycoWorld.DayCycle.Authoring;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.Vehicles.Authoring
{
    /// <summary>
    /// Motorbike clips (spec #5 §7): a CC0 recording named after the clip (.ogg, .wav or .mp3) wins; otherwise a
    /// synthesized "&lt;name&gt;_synth.wav" is written once (never overwritten).
    /// </summary>
    public static class VehicleAudioBuilder
    {
        public const string EngineName = "motor_engine_loop";
        public const string StarterName = "motor_starter";
        public const string HornName = "motor_horn";
        public const string BumpName = "motor_bump";
        const string SynthSuffix = "_synth.wav";

        static string[] RecordingExtensions() => new[] { ".ogg", ".wav", ".mp3" };

        public static AudioClip Resolve(string baseName)
        {
            foreach (var extension in RecordingExtensions())
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(VehiclePaths.AudioFolder + "/" + baseName + extension);
                if (clip != null) return clip;
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(SynthPath(baseName));
        }

        public static bool HasRecording(string baseName)
        {
            foreach (var extension in RecordingExtensions())
                if (File.Exists(WorldGenPaths.ToAbsolute(VehiclePaths.AudioFolder + "/" + baseName + extension))) return true;
            return false;
        }

        public static void WriteMissingClips()
        {
            AssetFolders.Ensure(VehiclePaths.AudioFolder);
            WriteIfMissing(EngineName, () => EngineSynth.EngineLoop());
            WriteIfMissing(StarterName, () => EngineSynth.Starter());
            WriteIfMissing(HornName, EngineSynth.Horn);
            WriteIfMissing(BumpName, () => EngineSynth.Bump());
        }

        static string SynthPath(string baseName) => VehiclePaths.AudioFolder + "/" + baseName + SynthSuffix;

        static void WriteIfMissing(string baseName, Func<float[]> make)
        {
            if (HasRecording(baseName)) return;
            string path = SynthPath(baseName);
            string full = WorldGenPaths.ToAbsolute(path);
            if (File.Exists(full)) return;
            File.WriteAllBytes(full, WavWriter.Encode(make(), EngineSynth.SampleRate));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }
    }
}
```

`Assets/Vehicles/Editor/VehicleAudioImportRules.cs`:
```csharp
using System;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.Vehicles.Authoring
{
    /// <summary>Motorbike clips are short 3D sounds: mono, decompressed on load, Vorbis.</summary>
    public sealed class VehicleAudioImportRules : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(VehiclePaths.AudioFolder + "/", StringComparison.Ordinal)) return;
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

- [ ] **Step 5: Tulis klip sintesis yang belum ada rekamannya, lalu reimport (MCP)**

```csharp
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        if (EditorApplication.isCompiling) { result.LogError("STILL COMPILING after refresh"); return; }
        TycoWorld.Vehicles.Authoring.VehicleAudioBuilder.WriteMissingClips();
        AssetDatabase.ImportAsset(TycoWorld.Vehicles.Authoring.VehiclePaths.AudioFolder, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        foreach (var name in new[] { "motor_engine_loop", "motor_starter", "motor_horn", "motor_bump" })
        {
            var clip = TycoWorld.Vehicles.Authoring.VehicleAudioBuilder.Resolve(name);
            result.Log(name + " -> " + (clip != null ? AssetDatabase.GetAssetPath(clip) + " " + clip.length.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "s" : "MISSING"));
        }
    }
}
```
Expected: keempat nama punya path (rekaman CC0 atau `_synth.wav`).

- [ ] **Step 6: Tulis `SOURCES.md`**

`Assets/Vehicles/Audio/SOURCES.md` (isi dengan hasil nyata Step 1):
```markdown
# Sumber audio motor (sub-proyek #5)

| File | Sumber | Lisensi |
|---|---|---|
| `motor_engine_loop<ekstensi>` | <judul aset, pembuat, URL OpenGameArt> atau Sintesis `EngineSynth.EngineLoop()` (55 Hz, seed 4021, 1 s loop) | CC0 1.0 (atau: CC0 1.0, buatan proyek ini) |
| `motor_starter<ekstensi>` | … atau Sintesis `EngineSynth.Starter()` (seed 733, 0,9 s) | … |
| `motor_horn<ekstensi>` | … atau Sintesis `EngineSynth.Horn()` (420 + 525 Hz, "tin-tin") | … |
| `motor_bump<ekstensi>` | … atau Sintesis `EngineSynth.Bump()` (70 Hz + noise, seed 977) | … |

Pencarian CC0 (2026-10-14): <kata kunci dan halaman yang dicek; kenapa diterima/ditolak>. Ganti klip kapan saja: taruh rekaman CC0 dengan nama dasar yang sama, lalu jalankan "Tyco World/Vehicle Setup".
```

- [ ] **Step 7: Jalankan tes, pastikan lulus**

Procedure R+T, `Group = "EngineSynthTests|VehicleAudioAssetsTests"`.
Expected: `passed=7 failed=0`.

- [ ] **Step 8: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add Assets/Vehicles
git -C "D:/UNITY/project/Tyco World" commit -m "feat(vehicles): motorbike audio clips (CC0 or synthesized) with import rules

Co-Authored-By: <model> <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

---

### Task 10: Material, rig, dan prefab `Motorbike`

**Files:**
- Modify: `Assets/Player/Editor/PlaceholderPropFactory.cs` (`AddWheel` jadi public; penyimpanan mesh diekstrak ke `SaveMeshes`)
- Create: `Assets/Vehicles/Editor/VehicleMaterials.cs`
- Create: `Assets/Vehicles/Editor/MotorbikeBuilder.cs`
- Create: `Assets/Vehicles/Editor/VehicleAssetsBuilder.cs`
- Create (lewat builder): `Assets/Vehicles/Config/BikeTuning.asset`, `Assets/Vehicles/Materials/*.mat`, `Assets/Vehicles/Prefabs/Motorbike.prefab`, `Assets/Vehicles/Prefabs/Meshes/*.asset`
- Test: `Assets/Vehicles/Tests/MotorbikeBuilderTests.cs`, `Assets/Vehicles/Tests/VehicleAssetsTests.cs`

**Interfaces:**
- Consumes:
  - Task 2: `BikeTuning`.
  - Task 7: `MotorbikeVisuals.Configure`, `MotorbikeAudio.Configure`.
  - Task 8: `Motorbike.Configure`, `Motorbike.ConfigureBody`, `MotorbikeInteractable`.
  - Task 9: `VehicleAudioBuilder.WriteMissingClips/Resolve` dan konstanta nama klip.
  - Player.Editor: `PlaceholderPropFactory`, `PlayerPaths.MaterialsFolder` (`M_Motor_Body.mat`, `M_Motor_Dark.mat`).
  - WorldGen.Editor: `MeshData`, `PlaceholderMeshes.AddBox`, `AssetFolders.Ensure`.
- Produces:
  - `PlaceholderPropFactory.AddWheel(MeshData, int sub, Vector3 center, float radius, float width, int sides)` (public) dan `PlaceholderPropFactory.SaveMeshes(GameObject, string folder)`.
  - `VehicleMaterialSet { Body, Dark, Chrome, TailLight, HeadlightLens, Shirt, Pants, Skin, Helmet }` dan `VehicleMaterials.LoadOrCreate() → VehicleMaterialSet`.
  - `VehicleClipSet { Engine, Starter, Horn, Bump }`.
  - `MotorbikeBuilder`:
    - konstanta nama `RootName "Motorbike"`, `LeanPivotName`, `BodyName`, `HandlebarName`, `FrontWheelName`, `RearWheelName`, `HeadlightName`, `LensName`, `TailLightName`, `SeatName`, `RiderName`, `AudioName`, dan `InteractionId "kos-motor"`;
    - `SeatPosition` (0, 0,86, -0,2);
    - `Build(GameObject root, BikeTuning, VehicleMaterialSet, VehicleClipSet)`: membangun atau memperbarui rig; anak dicari berdasarkan nama dan komponen dipakai ulang, jadi aman dipanggil berulang.
  - `VehicleAssets { Tuning, Materials, Clips, MotorbikePrefab }` dan `VehicleAssetsBuilder.BuildAll() → VehicleAssets` (menu "Tyco World/Vehicle/Build Vehicle Assets").

Hierarki rig (sumbu maju = +Z, akar di tanah):
```
Motorbike   CharacterController, Motorbike, MotorbikeVisuals, MotorbikeAudio, MotorbikeInteractable
├─ LeanPivot                 (0, 0, 0), dimiringkan MotorbikeVisuals
│  ├─ Body                   bodi, dek, kursi, spakbor, knalpot
│  ├─ Handlebar              (0, 0, 0,62), dibelokkan MotorbikeVisuals
│  │  ├─ FrontWheel          (0, 0,3, 0,03), berputar di sumbu X lokal
│  │  ├─ Headlight           Light spot, mati
│  │  └─ HeadlightLens       renderer lensa (material emisi)
│  ├─ RearWheel              (0, 0,3, -0,65)
│  ├─ TailLight              renderer lampu belakang (material emisi)
│  └─ Rider                  model pengendara primitif, nonaktif
├─ Seat                      (0, 0,86, -0,2) — tidak ikut miring
└─ Audio                     dua AudioSource 3D (mesin loop + one-shot)
```

Aturan rig:
- Satu-satunya collider adalah CharacterController. Model dan pengendara tanpa collider, supaya tidak mengganggu box cast motor, kamera, maupun raycast pemain.
- Prefab yang sudah ada **diperbarui di dalam isinya sendiri** (`LoadPrefabContents`), dan `Build` mencari anak berdasarkan nama serta memakai ulang komponen.
  - Hasilnya, file id semua objek tetap sama, jadi run ulang tidak mengubah file prefab.
  - Override pose instance di `World.unity` (Task 11) juga tidak hilang.
  - Menghapus lalu membuat ulang anak **dilarang**, karena setiap objek baru mendapat file id baru.

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/Vehicles/Tests/MotorbikeBuilderTests.cs`:
```csharp
using NUnit.Framework;
using TycoWorld.Vehicles.Authoring;
using UnityEngine;

namespace TycoWorld.Vehicles.Tests
{
    public class MotorbikeBuilderTests
    {
        VehicleFixture fx;
        BikeTuning tuning;
        VehicleMaterialSet materials;
        GameObject root;

        [SetUp]
        public void SetUp()
        {
            fx = new VehicleFixture();
            tuning = fx.Track(ScriptableObject.CreateInstance<BikeTuning>());
            var plain = fx.Track(new Material(Shader.Find("Universal Render Pipeline/Simple Lit")));
            var glow = fx.Track(new Material(plain));
            materials = new VehicleMaterialSet
            {
                Body = plain, Dark = plain, Chrome = plain, TailLight = glow, HeadlightLens = glow,
                Shirt = plain, Pants = plain, Skin = plain, Helmet = plain
            };
            root = fx.Track(new GameObject("Motorbike"));
            MotorbikeBuilder.Build(root, tuning, materials, new VehicleClipSet());
            TrackMeshes();
        }

        [TearDown]
        public void TearDown() => fx.Dispose();

        void TrackMeshes()
        {
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null) fx.Track(filter.sharedMesh);
        }

        [Test]
        public void Build_WiresTheRig()
        {
            var bike = root.GetComponent<Motorbike>();
            var visuals = root.GetComponent<MotorbikeVisuals>();
            Assert.IsNotNull(root.GetComponent<CharacterController>());
            Assert.AreSame(tuning, bike.Tuning);
            Assert.AreSame(visuals, bike.Visuals);
            Assert.AreSame(root.GetComponent<MotorbikeAudio>(), bike.Sound);
            Assert.AreEqual(MotorbikeBuilder.InteractionId, root.GetComponent<MotorbikeInteractable>().InteractionId);
            Assert.AreSame(root.transform, bike.Seat.parent, "kursi tidak ikut miring");
            Assert.AreEqual(new Vector3(0f, 0.86f, -0.2f), bike.Seat.localPosition);
            Assert.AreSame(root.transform, visuals.LeanPivot.parent);
            Assert.AreSame(visuals.LeanPivot, visuals.Handlebar.parent);
            Assert.AreSame(visuals.Handlebar, visuals.FrontWheel.parent);
            Assert.AreSame(visuals.LeanPivot, visuals.RearWheel.parent);
            Assert.AreSame(visuals.LeanPivot, visuals.Rider.parent);
            Assert.AreSame(visuals.Handlebar, visuals.Headlight.transform.parent);
            Assert.IsFalse(visuals.Rider.gameObject.activeSelf);
            Assert.AreSame(materials.TailLight, visuals.TailLight.sharedMaterial);
            Assert.AreSame(materials.HeadlightLens, visuals.HeadlightLens.sharedMaterial);
        }

        [Test]
        public void Build_Twice_ReusesTheSameObjects()
        {
            int children = root.transform.childCount;
            var seat = root.GetComponent<Motorbike>().Seat;
            var bar = root.GetComponent<MotorbikeVisuals>().Handlebar;
            var rider = root.GetComponent<MotorbikeVisuals>().Rider;
            MotorbikeBuilder.Build(root, tuning, materials, new VehicleClipSet());
            TrackMeshes();
            Assert.AreEqual(children, root.transform.childCount);
            Assert.AreSame(seat, root.GetComponent<Motorbike>().Seat, "anak dipakai ulang supaya file id prefab tetap");
            Assert.AreSame(bar, root.GetComponent<MotorbikeVisuals>().Handlebar);
            Assert.AreSame(rider, root.GetComponent<MotorbikeVisuals>().Rider);
            Assert.IsFalse(rider.gameObject.activeSelf);
            Assert.AreEqual(1, bar.GetComponents<MeshFilter>().Length);
            Assert.AreEqual(1, root.GetComponents<CharacterController>().Length);
            Assert.AreEqual(1, root.GetComponents<Motorbike>().Length);
            Assert.AreEqual(1, root.GetComponents<MotorbikeVisuals>().Length);
            Assert.AreEqual(1, root.GetComponents<MotorbikeAudio>().Length);
            Assert.AreEqual(1, root.GetComponents<MotorbikeInteractable>().Length);
            Assert.AreEqual(2, root.GetComponentsInChildren<AudioSource>(true).Length);
            Assert.AreEqual(1, root.GetComponentsInChildren<Light>(true).Length);
        }

        [Test]
        public void Wheels_SpinInPlace_AndTouchTheGround()
        {
            var visuals = root.GetComponent<MotorbikeVisuals>();
            foreach (var wheel in new[] { visuals.FrontWheel, visuals.RearWheel })
            {
                var bounds = wheel.GetComponent<MeshFilter>().sharedMesh.bounds;
                Assert.Less(bounds.center.magnitude, 0.01f, wheel.name + " berputar di porosnya");
                Assert.AreEqual(0.1f, bounds.size.x, 0.01f, wheel.name);
                Assert.AreEqual(0.6f, bounds.size.y, 0.01f, wheel.name);
                Assert.AreEqual(0.3f, wheel.position.y - root.transform.position.y, 0.01f, wheel.name + " menyentuh tanah");
            }
            Assert.AreEqual(1.3f, visuals.FrontWheel.position.z - visuals.RearWheel.position.z, 0.01f);
        }

        [Test]
        public void Headlight_AndAudio_FollowTheSpec()
        {
            var light = root.GetComponent<MotorbikeVisuals>().Headlight;
            Assert.AreEqual(LightType.Spot, light.type);
            Assert.AreEqual(30f, light.range, 1e-4f);
            Assert.AreEqual(60f, light.spotAngle, 1e-4f);
            Assert.AreEqual(4f, light.intensity, 1e-4f);
            Assert.AreEqual(LightShadows.None, light.shadows);
            Assert.IsFalse(light.enabled);
            var audio = root.GetComponent<MotorbikeAudio>();
            Assert.AreNotSame(audio.EngineSource, audio.OneShotSource);
            foreach (var source in new[] { audio.EngineSource, audio.OneShotSource })
            {
                Assert.IsFalse(source.playOnAwake);
                Assert.AreEqual(1f, source.spatialBlend);
                Assert.AreEqual(3f, source.minDistance, 1e-4f);
                Assert.AreEqual(40f, source.maxDistance, 1e-4f);
            }
            Assert.IsTrue(audio.EngineSource.loop);
        }

        [Test]
        public void OnlyTheBodyCollides()
        {
            var colliders = root.GetComponentsInChildren<Collider>(true);
            Assert.AreEqual(1, colliders.Length);
            Assert.IsInstanceOf<CharacterController>(colliders[0]);
            var body = (CharacterController)colliders[0];
            Assert.AreEqual(0.5f, body.radius, 1e-4f);
            Assert.AreEqual(1.4f, body.height, 1e-4f);
        }
    }
}
```

`Assets/Vehicles/Tests/VehicleAssetsTests.cs`:
```csharp
using NUnit.Framework;
using TycoWorld.Vehicles.Authoring;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.Vehicles.Tests
{
    /// <summary>The saved prefab and its assets (written by "Tyco World/Vehicle Setup" / VehicleAssetsBuilder.BuildAll).</summary>
    public class VehicleAssetsTests
    {
        static GameObject Prefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VehiclePaths.MotorbikePrefab);
            Assert.IsNotNull(prefab, "Prefab belum ada; jalankan VehicleAssetsBuilder.BuildAll");
            return prefab;
        }

        [Test]
        public void Prefab_UsesOnlySavedAssets()
        {
            var prefab = Prefab();
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                Assert.IsTrue(filter.sharedMesh != null && AssetDatabase.Contains(filter.sharedMesh), filter.name);
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                    Assert.IsTrue(material != null && AssetDatabase.Contains(material), renderer.name);
        }

        [Test]
        public void Prefab_HasTheTuningAndEveryClip()
        {
            var prefab = Prefab();
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<BikeTuning>(VehiclePaths.TuningAsset), prefab.GetComponent<Motorbike>().Tuning);
            var audio = new SerializedObject(prefab.GetComponent<MotorbikeAudio>());
            foreach (var field in new[] { "engineClip", "starterClip", "hornClip", "bumpClip" })
                Assert.IsNotNull(audio.FindProperty(field).objectReferenceValue, field);
        }

        [Test]
        public void LightMaterials_AreEmissive()
        {
            var visuals = Prefab().GetComponent<MotorbikeVisuals>();
            foreach (var renderer in new[] { visuals.TailLight, visuals.HeadlightLens })
            {
                var material = renderer.sharedMaterial;
                Assert.IsTrue(material.IsKeywordEnabled("_EMISSION"), material.name);
                Assert.Greater(material.GetColor("_EmissionColor").maxColorComponent, 0.5f, material.name);
            }
        }
    }
}
```

- [ ] **Step 2: Jalankan tes, pastikan gagal**

Procedure R+T, `Group = "MotorbikeBuilderTests|VehicleAssetsTests"`.
Expected: `COMPILE FAILED` (`MotorbikeBuilder` belum ada).

- [ ] **Step 3: Buka helper di `PlaceholderPropFactory`**

Di `Assets/Player/Editor/PlaceholderPropFactory.cs`, ganti seluruh method `SaveAsPrefab` dengan:
```csharp
        /// <summary>Saves non-asset meshes under Prefabs/Meshes, writes the prefab, destroys the scene object.</summary>
        public static GameObject SaveAsPrefab(GameObject go, string path)
        {
            SaveMeshes(go, PlayerPaths.MeshesFolder);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        /// <summary>
        /// Saves every non-asset mesh under <paramref name="folder"/> as "&lt;mesh name&gt;.asset" (an existing asset is
        /// updated in place) and points the filters at the saved meshes; a mesh shared by several filters is saved once.
        /// </summary>
        public static void SaveMeshes(GameObject go, string folder)
        {
            AssetFolders.Ensure(folder);
            var saved = new Dictionary<UnityEngine.Mesh, UnityEngine.Mesh>();
            foreach (var filter in go.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null || AssetDatabase.Contains(mesh)) continue;
                if (!saved.TryGetValue(mesh, out var asset))
                {
                    string meshPath = folder + "/" + mesh.name + ".asset";
                    asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(meshPath);
                    if (asset == null)
                    {
                        AssetDatabase.CreateAsset(mesh, meshPath);
                        asset = mesh;
                    }
                    else EditorUtility.CopySerialized(mesh, asset);
                    saved[mesh] = asset;
                }
                filter.sharedMesh = asset;
            }
            foreach (var pair in saved)
                if (pair.Key != pair.Value) Object.DestroyImmediate(pair.Key);
        }
```
Lalu ubah deklarasi `AddWheel` dari `static void AddWheel(` menjadi `public static void AddWheel(` (isinya tetap).

Perilaku untuk Player tidak berubah: mesh disimpan ke folder yang sama dengan nama yang sama.

- [ ] **Step 4: Implementasi material**

`Assets/Vehicles/Editor/VehicleMaterials.cs`:
```csharp
using System;
using TycoWorld.Player.Authoring;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.Vehicles.Authoring
{
    /// <summary>Every material the motorbike rig uses.</summary>
    public sealed class VehicleMaterialSet
    {
        public Material Body, Dark, Chrome, TailLight, HeadlightLens, Shirt, Pants, Skin, Helmet;
    }

    /// <summary>
    /// Bike paint and dark parts come from the player's placeholder motor (same look as before); the rest is created
    /// once in Assets/Vehicles/Materials and later edits by the user are kept.
    /// </summary>
    public static class VehicleMaterials
    {
        public const string BodyAsset = PlayerPaths.MaterialsFolder + "/M_Motor_Body.mat";
        public const string DarkAsset = PlayerPaths.MaterialsFolder + "/M_Motor_Dark.mat";
        const string EmissionKeyword = "_EMISSION";

        public static Color TailEmission => new Color(1f, 0.16f, 0.1f, 1f);
        public static Color LensEmission => new Color(1f, 0.96f, 0.86f, 1f);

        public static VehicleMaterialSet LoadOrCreate()
        {
            AssetFolders.Ensure(VehiclePaths.MaterialsFolder);
            return new VehicleMaterialSet
            {
                Body = Require(BodyAsset),
                Dark = Require(DarkAsset),
                Chrome = Create("M_Bike_Chrome", "B9BEC4", 0.8f, Color.black),
                TailLight = Create("M_Bike_TailLight", "5C0B08", 0.5f, TailEmission),
                HeadlightLens = Create("M_Bike_HeadlightLens", "D9D7CF", 0.8f, LensEmission),
                Shirt = Create("M_Rider_Shirt", "2F5D8A", 0.1f, Color.black),
                Pants = Create("M_Rider_Pants", "2A2C33", 0.1f, Color.black),
                Skin = Create("M_Rider_Skin", "C69A74", 0.2f, Color.black),
                Helmet = Create("M_Rider_Helmet", "17181C", 0.7f, Color.black)
            };
        }

        static Material Require(string path)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) throw new InvalidOperationException(path + " belum ada. Jalankan \"Tyco World/Player Setup\" dulu.");
            return material;
        }

        static Material Create(string name, string hex, float smoothness, Color emission)
        {
            string path = VehiclePaths.MaterialsFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            if (emission.maxColorComponent > 0f)
            {
                material.EnableKeyword(EmissionKeyword);
                material.SetColor("_EmissionColor", emission);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
```

- [ ] **Step 5: Implementasi rig**

`Assets/Vehicles/Editor/MotorbikeBuilder.cs`:
```csharp
using TycoWorld.Player.Authoring;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.Vehicles.Authoring
{
    /// <summary>The bike's clips (any may be null; the bike then stays silent for that sound).</summary>
    public sealed class VehicleClipSet
    {
        public AudioClip Engine, Starter, Horn, Bump;
    }

    /// <summary>
    /// Builds the primitive motorbike rig (spec #5 §1/§3): spinning wheels, steering handlebar, lean pivot, headlight,
    /// tail light, a seated rider and two 3D audio sources. Build is idempotent on the same root.
    /// </summary>
    public static class MotorbikeBuilder
    {
        public const string RootName = "Motorbike";
        public const string LeanPivotName = "LeanPivot";
        public const string BodyName = "Body";
        public const string HandlebarName = "Handlebar";
        public const string FrontWheelName = "FrontWheel";
        public const string RearWheelName = "RearWheel";
        public const string HeadlightName = "Headlight";
        public const string LensName = "HeadlightLens";
        public const string TailLightName = "TailLight";
        public const string SeatName = "Seat";
        public const string RiderName = "Rider";
        public const string AudioName = "Audio";
        public const string InteractionId = "kos-motor";
        const float WheelRadius = 0.3f;

        public static Vector3 SeatPosition => new Vector3(0f, 0.86f, -0.2f);

        /// <summary>
        /// Builds or refreshes the rig under <paramref name="root"/>. Children are found by name and components reused,
        /// so a re-run keeps the same objects (and their file ids inside the prefab); only meshes and settings are rewritten.
        /// </summary>
        public static void Build(GameObject root, BikeTuning tuning, VehicleMaterialSet materials, VehicleClipSet clips)
        {
            Motorbike.ConfigureBody(GetOrAdd<CharacterController>(root), tuning);

            var lean = Child(LeanPivotName, root.transform, Vector3.zero);
            AddMesh(Child(BodyName, lean, Vector3.zero), BodyMesh(), materials.Body, materials.Dark, materials.Chrome);

            var bar = Child(HandlebarName, lean, new Vector3(0f, 0f, 0.62f));
            AddMesh(bar, HandlebarMesh(), materials.Body, materials.Dark, materials.Chrome);
            var wheelMesh = WheelMesh();
            var front = Child(FrontWheelName, bar, new Vector3(0f, WheelRadius, 0.03f));
            AddMesh(front, wheelMesh, materials.Dark);
            var rear = Child(RearWheelName, lean, new Vector3(0f, WheelRadius, -0.65f));
            AddMesh(rear, wheelMesh, materials.Dark);

            var headlight = GetOrAdd<Light>(Child(HeadlightName, bar, new Vector3(0f, 0.97f, 0.13f)).gameObject);
            headlight.transform.localRotation = Quaternion.Euler(6f, 0f, 0f);
            headlight.type = LightType.Spot;
            headlight.range = tuning.headlightRange;
            headlight.spotAngle = tuning.headlightAngle;
            headlight.intensity = tuning.headlightIntensity;
            headlight.color = VehicleMaterials.LensEmission;
            headlight.shadows = LightShadows.None;
            headlight.enabled = false;
            var lens = Child(LensName, bar, new Vector3(0f, 0.97f, 0.112f));
            AddMesh(lens, Box("Motorbike_HeadlightLens", new Vector3(0.15f, 0.1f, 0.02f)), materials.HeadlightLens);
            var tail = Child(TailLightName, lean, new Vector3(0f, 0.72f, -0.82f));
            AddMesh(tail, Box("Motorbike_TailLight", new Vector3(0.16f, 0.07f, 0.03f)), materials.TailLight);

            var rider = Child(RiderName, lean, Vector3.zero);
            AddMesh(rider, RiderMesh(), materials.Shirt, materials.Pants, materials.Skin, materials.Helmet);
            rider.gameObject.SetActive(false);

            var seat = Child(SeatName, root.transform, SeatPosition);
            var audioGo = Child(AudioName, root.transform, new Vector3(0f, 0.5f, 0f)).gameObject;
            var sources = audioGo.GetComponents<AudioSource>();
            var engine = Configure3DSource(sources.Length > 0 ? sources[0] : audioGo.AddComponent<AudioSource>(), true);
            var oneShot = Configure3DSource(sources.Length > 1 ? sources[1] : audioGo.AddComponent<AudioSource>(), false);

            var visuals = GetOrAdd<MotorbikeVisuals>(root);
            visuals.Configure(lean, bar, front, rear, rider, headlight, tail.GetComponent<Renderer>(), lens.GetComponent<Renderer>());
            var sound = GetOrAdd<MotorbikeAudio>(root);
            sound.Configure(engine, oneShot, clips.Engine, clips.Starter, clips.Horn, clips.Bump);
            GetOrAdd<Motorbike>(root).Configure(tuning, seat, visuals, sound);
            GetOrAdd<MotorbikeInteractable>(root).SetInteractionId(InteractionId);
        }

        static Mesh BodyMesh()
        {
            var m = new MeshData(3);
            PlaceholderMeshes.AddBox(m, 0, new Vector3(0f, 0.55f, -0.05f), new Vector3(0.3f, 0.25f, 1f));
            PlaceholderMeshes.AddBox(m, 0, new Vector3(0f, 0.8f, 0.5f), new Vector3(0.38f, 0.5f, 0.22f));
            PlaceholderMeshes.AddBox(m, 0, new Vector3(0f, 0.66f, -0.6f), new Vector3(0.26f, 0.08f, 0.42f));
            PlaceholderMeshes.AddBox(m, 1, new Vector3(0f, 0.86f, -0.25f), new Vector3(0.3f, 0.1f, 0.65f));
            PlaceholderMeshes.AddBox(m, 1, new Vector3(0f, 0.44f, 0.15f), new Vector3(0.34f, 0.04f, 0.5f));
            AddBeam(m, 2, new Vector3(0.17f, 0.35f, -0.25f), new Vector3(0.17f, 0.45f, -0.8f), new Vector2(0.08f, 0.08f));
            AddBeam(m, 2, new Vector3(0f, WheelRadius, -0.65f), new Vector3(0f, 0.55f, -0.25f), new Vector2(0.05f, 0.05f));
            return m.ToMesh("Motorbike_Body", out _);
        }

        static Mesh HandlebarMesh()
        {
            var m = new MeshData(3);
            AddBeam(m, 2, new Vector3(0f, WheelRadius, 0.03f), new Vector3(0f, 1.05f, -0.05f), new Vector2(0.06f, 0.06f));
            PlaceholderMeshes.AddBox(m, 2, new Vector3(0f, 1.08f, -0.05f), new Vector3(0.62f, 0.035f, 0.035f));
            PlaceholderMeshes.AddBox(m, 1, new Vector3(-0.34f, 1.08f, -0.05f), new Vector3(0.1f, 0.045f, 0.045f));
            PlaceholderMeshes.AddBox(m, 1, new Vector3(0.34f, 1.08f, -0.05f), new Vector3(0.1f, 0.045f, 0.045f));
            PlaceholderMeshes.AddBox(m, 0, new Vector3(0f, 0.97f, 0.05f), new Vector3(0.2f, 0.16f, 0.12f));
            PlaceholderMeshes.AddBox(m, 0, new Vector3(0f, 0.64f, 0.03f), new Vector3(0.14f, 0.04f, 0.4f));
            return m.ToMesh("Motorbike_Handlebar", out _);
        }

        static Mesh WheelMesh()
        {
            var m = new MeshData(1);
            PlaceholderPropFactory.AddWheel(m, 0, Vector3.zero, WheelRadius, 0.1f, 12);
            return m.ToMesh("Motorbike_Wheel", out _);
        }

        /// <summary>Seated rider in bike coordinates: shirt, pants, skin, helmet/shoes.</summary>
        static Mesh RiderMesh()
        {
            var m = new MeshData(4);
            PlaceholderMeshes.AddBox(m, 1, new Vector3(0f, 0.99f, -0.3f), new Vector3(0.34f, 0.16f, 0.26f));
            AddBeam(m, 0, new Vector3(0f, 1.05f, -0.3f), new Vector3(0f, 1.52f, -0.18f), new Vector2(0.36f, 0.22f));
            PlaceholderMeshes.AddBox(m, 2, new Vector3(0f, 1.58f, -0.17f), new Vector3(0.1f, 0.08f, 0.1f));
            PlaceholderMeshes.AddBox(m, 3, new Vector3(0f, 1.73f, -0.15f), new Vector3(0.26f, 0.26f, 0.28f));
            PlaceholderMeshes.AddBox(m, 2, new Vector3(0f, 1.7f, 0f), new Vector3(0.18f, 0.14f, 0.02f));
            foreach (float side in new[] { -1f, 1f })
            {
                AddBeam(m, 1, new Vector3(0.12f * side, 0.98f, -0.25f), new Vector3(0.14f * side, 0.92f, 0.12f), new Vector2(0.14f, 0.14f));
                AddBeam(m, 1, new Vector3(0.14f * side, 0.92f, 0.12f), new Vector3(0.15f * side, 0.5f, 0.16f), new Vector2(0.11f, 0.11f));
                PlaceholderMeshes.AddBox(m, 3, new Vector3(0.15f * side, 0.48f, 0.2f), new Vector3(0.1f, 0.07f, 0.22f));
                AddBeam(m, 0, new Vector3(0.21f * side, 1.47f, -0.18f), new Vector3(0.27f * side, 1.22f, 0.18f), new Vector2(0.1f, 0.1f));
                AddBeam(m, 2, new Vector3(0.27f * side, 1.22f, 0.18f), new Vector3(0.33f * side, 1.1f, 0.55f), new Vector2(0.08f, 0.08f));
            }
            return m.ToMesh("Motorbike_Rider", out _);
        }

        static Mesh Box(string name, Vector3 size)
        {
            var m = new MeshData(1);
            PlaceholderMeshes.AddBox(m, 0, Vector3.zero, size);
            return m.ToMesh(name, out _);
        }

        /// <summary>Box from <paramref name="from"/> to <paramref name="to"/> with a <paramref name="section"/> (width, depth) cross-section.</summary>
        static void AddBeam(MeshData m, int sub, Vector3 from, Vector3 to, Vector2 section)
        {
            var axis = to - from;
            float length = axis.magnitude;
            if (length < 1e-4f) return;
            var forward = axis / length;
            var rotation = Quaternion.LookRotation(forward, Mathf.Abs(forward.y) > 0.99f ? Vector3.forward : Vector3.up);
            var center = (from + to) * 0.5f;
            var half = new Vector3(section.x, section.y, length) * 0.5f;
            foreach (var n in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
            {
                var u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
                var w = Vector3.Cross(n, u);
                Vector3 Corner(float su, float sw) => center + rotation * Vector3.Scale(n + u * su + w * sw, half);
                m.AddQuad(sub, Corner(-1f, -1f), Corner(1f, -1f), Corner(1f, 1f), Corner(-1f, 1f),
                    Vector2.zero, Vector2.right, Vector2.one, Vector2.up, rotation * n);
            }
        }

        /// <summary>Finds (also inactive) or creates the named child and resets its local pose.</summary>
        static Transform Child(string name, Transform parent, Vector3 localPosition)
        {
            var t = parent.Find(name);
            if (t == null)
            {
                t = new GameObject(name).transform;
                t.SetParent(parent, false);
            }
            t.localPosition = localPosition;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            return t;
        }

        static void AddMesh(Transform t, Mesh mesh, params Material[] materials)
        {
            GetOrAdd<MeshFilter>(t.gameObject).sharedMesh = mesh;
            GetOrAdd<MeshRenderer>(t.gameObject).sharedMaterials = materials;
        }

        static AudioSource Configure3DSource(AudioSource source, bool loop)
        {
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 1f;
            source.minDistance = 3f;
            source.maxDistance = 40f;
            source.rolloffMode = AudioRolloffMode.Linear;
            return source;
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }
    }
}
```
Catatan: file ini tidak memakai `using System;`, jadi `Object` dan `Mesh` merujuk ke `UnityEngine`.

- [ ] **Step 6: Implementasi builder aset**

`Assets/Vehicles/Editor/VehicleAssetsBuilder.cs`:
```csharp
using System.Collections.Generic;
using TycoWorld.Player.Authoring;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TycoWorld.Vehicles.Authoring
{
    public sealed class VehicleAssets
    {
        public BikeTuning Tuning;
        public VehicleMaterialSet Materials;
        public VehicleClipSet Clips;
        public GameObject MotorbikePrefab;
    }

    /// <summary>Creates or updates every vehicle asset in place (re-running is safe; tuning and materials keep user edits).</summary>
    public static class VehicleAssetsBuilder
    {
        [MenuItem("Tyco World/Vehicle/Build Vehicle Assets")]
        static void Menu() => BuildAll();

        public static VehicleAssets BuildAll()
        {
            // The first build creates a temporary object in the active scene; re-save scenes that were clean so Procedure R+T is not blocked.
            var cleanScenes = new List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (!s.isDirty && s.path.Length > 0) cleanScenes.Add(s);
            }
            foreach (var folder in new[] { VehiclePaths.ConfigFolder, VehiclePaths.MaterialsFolder, VehiclePaths.PrefabsFolder, VehiclePaths.MeshesFolder })
                AssetFolders.Ensure(folder);
            VehicleAudioBuilder.WriteMissingClips();
            var a = new VehicleAssets
            {
                Tuning = LoadOrCreateTuning(),
                Materials = VehicleMaterials.LoadOrCreate(),
                Clips = new VehicleClipSet
                {
                    Engine = VehicleAudioBuilder.Resolve(VehicleAudioBuilder.EngineName),
                    Starter = VehicleAudioBuilder.Resolve(VehicleAudioBuilder.StarterName),
                    Horn = VehicleAudioBuilder.Resolve(VehicleAudioBuilder.HornName),
                    Bump = VehicleAudioBuilder.Resolve(VehicleAudioBuilder.BumpName)
                }
            };
            a.MotorbikePrefab = SavePrefab(a);
            AssetDatabase.SaveAssets();
            foreach (var s in cleanScenes)
                if (s.IsValid() && s.isDirty) EditorSceneManager.SaveScene(s);
            return a;
        }

        static BikeTuning LoadOrCreateTuning()
        {
            var existing = AssetDatabase.LoadAssetAtPath<BikeTuning>(VehiclePaths.TuningAsset);
            if (existing != null) return existing;
            var created = ScriptableObject.CreateInstance<BikeTuning>();
            AssetDatabase.CreateAsset(created, VehiclePaths.TuningAsset);
            return created;
        }

        /// <summary>
        /// An existing prefab is rebuilt inside its own contents, so its root keeps its file id and the pose override of
        /// the instance in World.unity survives a re-run.
        /// </summary>
        static GameObject SavePrefab(VehicleAssets a)
        {
            string path = VehiclePaths.MotorbikePrefab;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                var root = new GameObject(MotorbikeBuilder.RootName);
                try
                {
                    MotorbikeBuilder.Build(root, a.Tuning, a.Materials, a.Clips);
                    PlaceholderPropFactory.SaveMeshes(root, VehiclePaths.MeshesFolder);
                    return PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    Object.DestroyImmediate(root);
                }
            }
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                MotorbikeBuilder.Build(contents, a.Tuning, a.Materials, a.Clips);
                PlaceholderPropFactory.SaveMeshes(contents, VehiclePaths.MeshesFolder);
                return PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
```

- [ ] **Step 7: Jalankan tes rig**

Procedure R+T, `Group = "MotorbikeBuilderTests|VehicleAssetsTests"`.
Expected: `MotorbikeBuilderTests` 5 lulus. `VehicleAssetsTests` 3 gagal karena prefab belum ada.

- [ ] **Step 8: Bangun aset (MCP), lalu cek idempoten**

```csharp
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var a = TycoWorld.Vehicles.Authoring.VehicleAssetsBuilder.BuildAll();
        result.Log("prefab=" + AssetDatabase.GetAssetPath(a.MotorbikePrefab) + " tuning=" + AssetDatabase.GetAssetPath(a.Tuning)
                   + " engine=" + (a.Clips.Engine != null ? AssetDatabase.GetAssetPath(a.Clips.Engine) : "NULL"));
    }
}
```
Expected: `prefab=Assets/Vehicles/Prefabs/Motorbike.prefab`, tuning ada, dan klip mesin tidak `NULL`.

Lalu stage hasilnya dan jalankan builder sekali lagi:
```bash
git -C "D:/UNITY/project/Tyco World" add Assets/Vehicles
```
Jalankan snippet MCP yang sama sekali lagi, lalu:
```bash
git -C "D:/UNITY/project/Tyco World" diff --stat -- Assets/Vehicles
git -C "D:/UNITY/project/Tyco World" status --short
```
Expected:
- `git diff --stat` kosong: run kedua tidak mengubah apa pun.
- Status tidak menyentuh `Assets/Scenes` dan tidak menampilkan file baru di luar `Assets/Vehicles`, `Assets/Player/Editor/PlaceholderPropFactory.cs`, dan `ProjectSettings/ProjectSettings.asset` (perubahan user yang lama).

Kalau run kedua menghasilkan diff, tulis file dan baris yang berubah di laporan. Jangan menambal dengan tangan.

- [ ] **Step 9: Jalankan tes, pastikan lulus**

Procedure R+T, `Group = "MotorbikeBuilderTests|VehicleAssetsTests"`.
Expected: `passed=8 failed=0`.

Lalu jalankan regresi Player, karena `SaveMeshes` diekstrak dari `SaveAsPrefab` (`Group = "TycoWorld.Player.Tests"`).
Expected: `failed=0`.

- [ ] **Step 10: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add Assets/Player/Editor/PlaceholderPropFactory.cs Assets/Vehicles
git -C "D:/UNITY/project/Tyco World" commit -m "feat(vehicles): primitive motorbike rig, materials and prefab builder

Co-Authored-By: <model> <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

---

### Task 11: Menu "Tyco World/Vehicle Setup" dan motor di `World.unity`

**Files:**
- Create: `Assets/Vehicles/Editor/VehicleSceneSetup.cs`
- Test: `Assets/Vehicles/Tests/VehicleSetupTests.cs`
- Modify (lewat menu): `Assets/Scenes/World.unity`

**Interfaces:**
- Consumes:
  - Task 10: `VehicleAssetsBuilder.BuildAll() → VehicleAssets` (`MotorbikePrefab`) dan `MotorbikeBuilder.RootName`.
  - Player: `PlaceholderVehicleInteractable`, `PlayerLayers.QueryMask`.
  - WorldGen: `WorldScenes.CheckSaved/EnsureWorldSceneOpen/OpenGeneratedSceneAdditive`, `GroundSurface`.
- Produces:
  - `VehicleSceneSetup.Run(bool interactive, out string error) → bool`.
  - `VehicleSceneSetup.Wire(Scene world, GameObject prefab, out string error) → GameObject`.
  - Konstanta `MissingPlaceholderError`, `OverlapWarning`.

Aturan `Wire`:
- Kalau ada `PlaceholderVehicleInteractable` di scene, motor `Motorbike` (akar scene, instance prefab) dibuat kalau belum ada, lalu diletakkan di pose placeholder **pertama** (posisi + yaw dunia). Setelah itu **semua** placeholder dihapus.
- Kalau tidak ada placeholder tapi motor sudah ada, motor dibiarkan (posenya tidak disentuh).
- Kalau keduanya tidak ada, hasilnya `null` dengan `MissingPlaceholderError`.
- Motor adalah **akar scene**, bukan anak `KosFrontyard`. Alasannya, motor bergerak ke seluruh dunia, dan "Re-place Kos Frontyard" menghapus seluruh `KosFrontyard`.
- Setelah "Re-place Kos Frontyard", jalankan Vehicle Setup lagi. Motor dipindah ke pose placeholder yang baru, lalu placeholder dihapus (spec §11).

- [ ] **Step 1: Tulis tes yang gagal**

`Assets/Vehicles/Tests/VehicleSetupTests.cs`:
```csharp
using System.Collections.Generic;
using NUnit.Framework;
using TycoWorld.Player;
using TycoWorld.Vehicles.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TycoWorld.Vehicles.Tests
{
    public class VehicleSetupTests
    {
        readonly HashSet<GameObject> rootsBefore = new HashSet<GameObject>();
        Scene scratch;
        GameObject prefab;

        /// <summary>The untitled scene the test runner opens stands in for World.unity; every root a test adds is destroyed in TearDown.</summary>
        [SetUp]
        public void SetUp()
        {
            scratch = SceneManager.GetActiveScene();
            Assert.IsTrue(string.IsNullOrEmpty(scratch.path), "Tes setup hanya boleh memakai scene untitled, bukan " + scratch.path);
            rootsBefore.Clear();
            rootsBefore.UnionWith(scratch.GetRootGameObjects());
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VehiclePaths.MotorbikePrefab);
            Assert.IsNotNull(prefab, "Prefab belum ada (Task 10)");
        }

        [TearDown]
        public void TearDown()
        {
            if (scratch.IsValid() && scratch.isLoaded)
                foreach (var go in scratch.GetRootGameObjects())
                    if (!rootsBefore.Contains(go)) Object.DestroyImmediate(go);
            rootsBefore.Clear();
        }

        /// <summary>A KosFrontyard stand-in rotated 180° with the placeholder motor as a child, like World.unity.</summary>
        static Transform Placeholder(Vector3 yardPosition, Vector3 local, float localYaw)
        {
            var yard = GameObject.Find("KosFrontyard");
            if (yard == null)
            {
                yard = new GameObject("KosFrontyard");
                yard.transform.SetPositionAndRotation(yardPosition, Quaternion.Euler(0f, 180f, 0f));
            }
            var motor = new GameObject("Motor_Placeholder");
            motor.transform.SetParent(yard.transform, false);
            motor.transform.localPosition = local;
            motor.transform.localRotation = Quaternion.Euler(0f, localYaw, 0f);
            motor.AddComponent<PlaceholderVehicleInteractable>();
            return motor.transform;
        }

        int Count<T>() where T : Component
        {
            int n = 0;
            foreach (var go in scratch.GetRootGameObjects()) n += go.GetComponentsInChildren<T>(true).Length;
            return n;
        }

        static void AssertPose(Vector3 position, float yaw, Transform actual)
        {
            Assert.Less(Vector3.Distance(position, actual.position), 1e-3f, "posisi");
            Assert.AreEqual(0f, Mathf.DeltaAngle(yaw, actual.eulerAngles.y), 1e-2f, "yaw");
            Assert.AreEqual(0f, Mathf.DeltaAngle(0f, actual.eulerAngles.x), 1e-2f, "tegak");
            Assert.AreEqual(0f, Mathf.DeltaAngle(0f, actual.eulerAngles.z), 1e-2f, "tegak");
        }

        [Test]
        public void Wire_ReplacesThePlaceholder_AtItsWorldPose()
        {
            var placeholder = Placeholder(new Vector3(10f, 0f, 20f), new Vector3(4.7f, 0.12f, 2.7f), -90f);
            var pose = placeholder.position;
            float yaw = placeholder.eulerAngles.y;

            var bike = VehicleSceneSetup.Wire(scratch, prefab, out string error);

            Assert.IsNull(error);
            Assert.IsNotNull(bike);
            Assert.AreEqual(MotorbikeBuilder.RootName, bike.name);
            Assert.IsNull(bike.transform.parent, "akar scene");
            Assert.AreEqual(scratch, bike.scene);
            Assert.AreSame(prefab, PrefabUtility.GetCorrespondingObjectFromSource(bike));
            AssertPose(pose, yaw, bike.transform);
            Assert.AreEqual(0, Count<PlaceholderVehicleInteractable>());
            Assert.IsNotNull(GameObject.Find("KosFrontyard"), "halaman kos tetap ada");
        }

        [Test]
        public void Wire_Rerun_DoesNotDuplicate()
        {
            Placeholder(Vector3.zero, new Vector3(1f, 0.12f, 2f), 0f);
            var first = VehicleSceneSetup.Wire(scratch, prefab, out _);
            var again = VehicleSceneSetup.Wire(scratch, prefab, out string error);
            Assert.IsNull(error);
            Assert.AreSame(first, again);
            Assert.AreEqual(1, Count<Motorbike>());
        }

        [Test]
        public void Wire_WithoutAPlaceholder_KeepsAMovedBike()
        {
            Placeholder(Vector3.zero, new Vector3(1f, 0.12f, 2f), 0f);
            var bike = VehicleSceneSetup.Wire(scratch, prefab, out _);
            bike.transform.SetPositionAndRotation(new Vector3(30f, 0f, -5f), Quaternion.Euler(0f, 45f, 0f));
            VehicleSceneSetup.Wire(scratch, prefab, out _);
            AssertPose(new Vector3(30f, 0f, -5f), 45f, bike.transform);
        }

        [Test]
        public void Wire_AfterTheYardWasRebuilt_MovesTheBike_AndRemovesTheNewPlaceholder()
        {
            Placeholder(Vector3.zero, new Vector3(1f, 0.12f, 2f), 0f);
            var bike = VehicleSceneSetup.Wire(scratch, prefab, out _);
            var rebuilt = Placeholder(Vector3.zero, new Vector3(-3f, 0.12f, 4f), 90f);
            var pose = rebuilt.position;
            float yaw = rebuilt.eulerAngles.y;
            var again = VehicleSceneSetup.Wire(scratch, prefab, out _);
            Assert.AreSame(bike, again);
            AssertPose(pose, yaw, bike.transform);
            Assert.AreEqual(0, Count<PlaceholderVehicleInteractable>());
        }

        [Test]
        public void Wire_WithNothingToReplace_Fails()
        {
            var bike = VehicleSceneSetup.Wire(scratch, prefab, out string error);
            Assert.IsNull(bike);
            Assert.AreEqual(VehicleSceneSetup.MissingPlaceholderError, error);
            Assert.AreEqual(0, Count<Motorbike>());
        }
    }
}
```

- [ ] **Step 2: Jalankan tes, pastikan gagal**

Procedure R+T, `Group = "VehicleSetupTests"`.
Expected: `COMPILE FAILED` (`VehicleSceneSetup` belum ada).

- [ ] **Step 3: Implementasi**

`Assets/Vehicles/Editor/VehicleSceneSetup.cs`:
```csharp
using System.Collections.Generic;
using System.Text;
using TycoWorld.Player;
using TycoWorld.WorldGen;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TycoWorld.Vehicles.Authoring
{
    /// <summary>
    /// "Tyco World/Vehicle Setup" (spec #5 §2): builds the vehicle assets and puts the rideable Motorbike into World.unity
    /// where the kos placeholder motor stood. Safe to re-run; run it again after "Re-place Kos Frontyard".
    /// </summary>
    public static class VehicleSceneSetup
    {
        public const string MissingPlaceholderError =
            "Motor placeholder di KosFrontyard tidak ditemukan. Jalankan \"Tyco World/Player Setup\" dulu.";
        public const string OverlapWarning = "[Vehicle] Motor menembus objek lain di posisi parkirnya: ";

        [MenuItem("Tyco World/Vehicle Setup")]
        static void SetupMenu()
        {
            if (!Run(true, out string error)) EditorUtility.DisplayDialog("Vehicle Setup gagal", error, "OK");
        }

        public static bool Run(bool interactive, out string error)
        {
            if (!WorldScenes.CheckSaved(interactive, out error)) return false;
            var world = WorldScenes.EnsureWorldSceneOpen();
            var assets = VehicleAssetsBuilder.BuildAll();
            SceneManager.SetActiveScene(world);
            var bike = Wire(world, assets.MotorbikePrefab, out error);
            if (bike == null) return false;
            EditorSceneManager.MarkSceneDirty(world);
            EditorSceneManager.SaveScene(world);
            WorldScenes.OpenGeneratedSceneAdditive();
            SceneManager.SetActiveScene(world);
            WarnOverlaps(bike);
            return true;
        }

        /// <summary>
        /// Creates the Motorbike root (a prefab instance) if needed, moves it to the first placeholder motor's world pose
        /// (yaw only) and deletes every placeholder. Without a placeholder an existing bike keeps its pose.
        /// </summary>
        public static GameObject Wire(Scene world, GameObject prefab, out string error)
        {
            error = null;
            var placeholders = new List<PlaceholderVehicleInteractable>();
            foreach (var go in world.GetRootGameObjects())
                placeholders.AddRange(go.GetComponentsInChildren<PlaceholderVehicleInteractable>(true));
            var bike = FindRoot(world, MotorbikeBuilder.RootName);
            if (bike == null && placeholders.Count == 0)
            {
                error = MissingPlaceholderError;
                return null;
            }
            if (bike == null)
            {
                bike = (GameObject)PrefabUtility.InstantiatePrefab(prefab, world);
                bike.name = MotorbikeBuilder.RootName;
            }
            if (placeholders.Count > 0)
            {
                var anchor = placeholders[0].transform;
                bike.transform.SetPositionAndRotation(anchor.position, Quaternion.Euler(0f, anchor.eulerAngles.y, 0f));
                foreach (var placeholder in placeholders)
                {
                    if (placeholder == null) continue;
                    var instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(placeholder.gameObject);
                    Object.DestroyImmediate(instanceRoot != null ? instanceRoot : placeholder.gameObject);
                }
            }
            EditorUtility.SetDirty(bike);
            return bike;
        }

        static GameObject FindRoot(Scene scene, string name)
        {
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == name) return go;
            return null;
        }

        /// <summary>Warns when the parked bike's body overlaps generated or yard geometry above step height (it is not moved).</summary>
        static void WarnOverlaps(GameObject bike)
        {
            Physics.SyncTransforms();
            var body = bike.GetComponent<CharacterController>();
            var t = bike.transform;
            var bottom = t.position + Vector3.up * (body.radius + body.stepOffset);
            var top = t.position + Vector3.up * (body.height - body.radius);
            var hits = Physics.OverlapCapsule(bottom, top, body.radius * 0.95f, PlayerLayers.QueryMask, QueryTriggerInteraction.Ignore);
            var sb = new StringBuilder();
            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(t)) continue;
                if (hit.GetComponentInParent<GroundSurface>() != null) continue;
                sb.Append(hit.transform.root.name).Append('/').Append(hit.name).Append("; ");
            }
            if (sb.Length > 0) Debug.LogWarning(OverlapWarning + sb);
        }
    }
}
```

Kalau `GetOutermostPrefabInstanceRoot` mengembalikan instance yang lebih besar dari placeholder (misalnya seluruh `KosFrontyard` ternyata satu prefab instance), jangan hapus. Hentikan pekerjaan dan laporkan, karena di `World.unity` `KosFrontyard` seharusnya objek biasa dan `Motor_Placeholder` adalah instance-nya sendiri. Cek dengan Step 5.

- [ ] **Step 4: Jalankan tes, pastikan lulus**

Procedure R+T, `Group = "VehicleSetupTests"`.
Expected: `passed=5 failed=0`.

- [ ] **Step 5: Jalankan Vehicle Setup pada `World.unity` (MCP)**

Sebelum menjalankan menu, catat pose placeholder dan pastikan dia instance-nya sendiri:
```csharp
using UnityEditor;
using UnityEngine;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        foreach (var p in Object.FindObjectsByType<TycoWorld.Player.PlaceholderVehicleInteractable>())
        {
            var root = PrefabUtility.GetOutermostPrefabInstanceRoot(p.gameObject);
            result.Log(p.name + " parent=" + (p.transform.parent != null ? p.transform.parent.name : "-")
                       + " instanceRoot=" + (root != null ? root.name : "-")
                       + " pos=" + p.transform.position.ToString("F2") + " yaw=" + p.transform.eulerAngles.y.ToString("F2", inv));
        }
    }
}
```
Expected: satu baris `Motor_Placeholder parent=KosFrontyard instanceRoot=Motor_Placeholder pos=… yaw=…`. Kalau `instanceRoot` bukan `Motor_Placeholder`, berhenti dan laporkan.

Lalu jalankan:
```csharp
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (TycoWorld.Vehicles.Authoring.VehicleSceneSetup.Run(false, out string error))
        {
            var bike = UnityEngine.GameObject.Find("Motorbike");
            result.Log("ok pos=" + bike.transform.position.ToString("F2") + " yaw="
                       + bike.transform.eulerAngles.y.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                       + " placeholders=" + UnityEngine.Object.FindObjectsByType<TycoWorld.Player.PlaceholderVehicleInteractable>().Length);
        }
        else result.LogError(error);
    }
}
```
Expected:
- `ok`, dengan pose sama dengan placeholder yang dicatat tadi (toleransi 0,01 m / 0,1°);
- `placeholders=0`.

Lalu cek Console (`mcp__unity-mcp__Unity_GetConsoleLogs`, `logTypes: "warning,error"`). Expected: tidak ada `[Vehicle] Motor menembus …`. Kalau ada, catat isinya di laporan dan **jangan** memindahkan motor dengan tangan; controller yang memutuskan.

Jalankan snippet ini sekali lagi, lalu:
```bash
git -C "D:/UNITY/project/Tyco World" status --short
git -C "D:/UNITY/project/Tyco World" diff --stat -- Assets/Scenes/World.unity
```
Expected:
- hanya `Assets/Scenes/World.unity` dan file Task 11 yang berubah (plus `ProjectSettings/ProjectSettings.asset` milik user, yang tidak di-stage);
- run kedua tidak menambah perubahan lagi.

Kalau `ProjectSettings/EditorBuildSettings.asset` ikut berubah, jangan di-stage; catat di laporan.

- [ ] **Step 6: Regresi Vehicles + Player**

Procedure R+T, `Group = "TycoWorld.Vehicles.Tests|TycoWorld.Player.Tests"`.
Expected: `failed=0`.

- [ ] **Step 7: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add Assets/Vehicles Assets/Scenes/World.unity
git -C "D:/UNITY/project/Tyco World" commit -m "feat(vehicles): Vehicle Setup menu puts the rideable motorbike at the kos

Co-Authored-By: <model> <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

---

### Task 12: Volume mesin mengikuti slider Efek (GameSettings)

**Files:**
- Modify: `Assets/GameSettings/Runtime/TycoWorld.GameSettings.asmdef` (tambah referensi `TycoWorld.Vehicles`)
- Modify: `Assets/GameSettings/Runtime/Audio/EffectsVolume.cs`
- Modify: `Assets/GameSettings/Runtime/SettingsApplier.cs` (`ApplyAll`)
- Modify: `Assets/GameSettings/Tests/TycoWorld.GameSettings.Tests.asmdef` (tambah referensi `TycoWorld.Vehicles`)
- Test: `Assets/GameSettings/Tests/EffectsVolumeTests.cs`, `Assets/GameSettings/Tests/SettingsApplierTests.cs` (masing-masing 1 tes baru)

**Interfaces:**
- Consumes: `MotorbikeAudio` dan `MotorbikeAudio.Level` (Task 7).
- Produces:
  - `SettingsApplier.ApplyAll` menyetel `Level = effectsVolume / 100` pada setiap `MotorbikeAudio`.
  - `EffectsVolume.Apply` melewati AudioSource yang punya `MotorbikeAudio` di parent-nya (sama seperti `AmbienceAudio`).

Alasan: volume mesin ditulis ulang oleh `MotorbikeAudio.Tick` setiap frame, dan one-shot memakai `Level`. Kalau `EffectsVolume` ikut mengalikan volume source-nya, level Efek dikenakan dua kali.

- [ ] **Step 1: Tambah referensi asmdef tes**

Di `Assets/GameSettings/Tests/TycoWorld.GameSettings.Tests.asmdef`, tambahkan `"TycoWorld.Vehicles"` ke `references` tepat setelah `"TycoWorld.WorldGen.Editor"`:
```json
        "TycoWorld.WorldGen.Editor",
        "TycoWorld.Vehicles",
        "Unity.InputSystem",
```

- [ ] **Step 2: Tulis tes yang gagal**

Di `Assets/GameSettings/Tests/EffectsVolumeTests.cs`, tambahkan `using TycoWorld.Vehicles;` lalu tes ini setelah `Apply_SkipsAmbienceLayers`:
```csharp
        [Test]
        public void Apply_SkipsMotorbikeSources()
        {
            var bike = fx.Track(new GameObject("Motorbike"));
            bike.AddComponent<MotorbikeAudio>();
            var audio = new GameObject("Audio");
            audio.transform.SetParent(bike.transform, false);
            var source = audio.AddComponent<AudioSource>();
            source.volume = 0.7f;
            effects.Apply(new[] { source }, 0.5f);
            Assert.AreEqual(0.7f, source.volume, 1e-5f, "volume mesin diatur MotorbikeAudio.Level");
        }
```

Di `Assets/GameSettings/Tests/SettingsApplierTests.cs`, tambahkan `using TycoWorld.Vehicles;` lalu tes ini setelah `Apply_SetsMasterAndEffectsVolumes_LeavesAmbience`:
```csharp
        [Test]
        public void Apply_SetsTheMotorbikeLevel_FromEffects()
        {
            applier.Initialize();
            var bike = fx.Track(new GameObject("Motorbike")).AddComponent<MotorbikeAudio>();
            applier.Apply(new SettingsData { effectsVolume = 40 });
            Assert.AreEqual(0.4f, bike.Level, 1e-5f);
            applier.Apply(new SettingsData { effectsVolume = 100 });
            Assert.AreEqual(1f, bike.Level, 1e-5f);
        }
```

- [ ] **Step 3: Jalankan tes, pastikan gagal**

Procedure R+T, `Group = "EffectsVolumeTests|SettingsApplierTests"`.
Expected: compile berhasil (asmdef tes sudah mereferensikan Vehicles). Dua tes baru **gagal**:
- `Apply_SkipsMotorbikeSources`: volume menjadi 0,35;
- `Apply_SetsTheMotorbikeLevel_FromEffects`: Level tetap 1.

- [ ] **Step 4: Implementasi**

Di `Assets/GameSettings/Runtime/TycoWorld.GameSettings.asmdef`, tambahkan `"TycoWorld.Vehicles"` setelah `"TycoWorld.DayCycle"`:
```json
        "TycoWorld.DayCycle",
        "TycoWorld.Vehicles",
        "Unity.InputSystem",
```

Di `Assets/GameSettings/Runtime/Audio/EffectsVolume.cs`, tambahkan `using TycoWorld.Vehicles;`, lalu ganti baris:
```csharp
                if (source == null || source.GetComponentInParent<AmbienceAudio>(true) != null) continue;
```
dengan:
```csharp
                if (source == null || source.GetComponentInParent<AmbienceAudio>(true) != null) continue;
                // The bike rewrites its engine volume every frame and scales one-shots by MotorbikeAudio.Level.
                if (source.GetComponentInParent<MotorbikeAudio>(true) != null) continue;
```
Kalau summary XML kelas menyebut ambience sebagai satu-satunya pengecualian, tambahkan "and motorbike sources (MotorbikeAudio.Level)".

Di `Assets/GameSettings/Runtime/SettingsApplier.cs`, tambahkan `using TycoWorld.Vehicles;`, lalu ganti baris:
```csharp
            effects.Apply(UnityEngine.Object.FindObjectsByType<AudioSource>(), s.effectsVolume / 100f);
```
dengan:
```csharp
            effects.Apply(UnityEngine.Object.FindObjectsByType<AudioSource>(), s.effectsVolume / 100f);
            foreach (var bike in UnityEngine.Object.FindObjectsByType<MotorbikeAudio>()) bike.Level = s.effectsVolume / 100f;
```

- [ ] **Step 5: Jalankan tes, pastikan lulus**

Procedure R+T, `Group = "EffectsVolumeTests|SettingsApplierTests"`.
Expected: `failed=0`, dengan 2 tes baru lulus.

Lalu regresi seluruh GameSettings dan Vehicles (`Group = "TycoWorld.GameSettings.Tests|TycoWorld.Vehicles.Tests"`).
Expected: `failed=0`.

- [ ] **Step 6: Commit**

```bash
git -C "D:/UNITY/project/Tyco World" add Assets/GameSettings/Runtime/TycoWorld.GameSettings.asmdef Assets/GameSettings/Runtime/Audio/EffectsVolume.cs Assets/GameSettings/Runtime/SettingsApplier.cs Assets/GameSettings/Tests/TycoWorld.GameSettings.Tests.asmdef Assets/GameSettings/Tests/EffectsVolumeTests.cs Assets/GameSettings/Tests/SettingsApplierTests.cs
git -C "D:/UNITY/project/Tyco World" commit -m "feat(settings): engine and bike one-shots follow the effects volume

Co-Authored-By: <model> <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

---

### Task 13: Verifikasi Play lewat MCP dan catatan verifikasi

**Files:**
- Create: `Assets/Vehicles/Tests/McpVehicleDriver.cs`
- Create: `docs/superpowers/notes/2026-10-14-motorbike-verification.md`

**Interfaces:**
- Consumes:
  - `Motorbike` (Task 8): `Teleport`, `SetIntentSource`, `IntentSourceOverride`, `IsMounted`, `SpeedKmh`, `Yaw`, `Bumps`, `HeadlightOn`, `CameraState`, `Sound`, `Tuning`.
  - `ScriptedVehicleIntentSource` (Task 1), `DismountFinder.TryFind` (Task 6).
  - Player.Tests: `McpPlayerDriver` (`Install`, `Face`, `Press`, `Hold`, `TeleportTo`, `Spawn`) dan `McpRender` (`SavePng`, `CenterLuminance`).
  - GameSettings.Tests: `McpPerfProbe` (`Status`, `SetTime`, `Begin`, `Report`, `Release`) dan `McpGameDriver` (`Escape`, `ApplyPreset`, `Status`).
- Produces: `McpVehicleDriver` (alat bantu dev, bukan tes):
  - `Status`, `Install`, `Release`;
  - `Hold(throttle, steer, brake, seconds)`, `Press(horn, headlight, camera, exit)`;
  - `StandBeside`, `OpenGate`, `AimAtGate`, `PlaceAt(x, z, yaw)`, `TestTrack`;
  - `Shot(path)`, `Luminance`.

Folder screenshot (diabaikan git): `D:/UNITY/project/Tyco World/.superpowers/sdd/2026-10-14-motorbike/shots/`.

**Procedure D — panggil driver (MCP).** Setiap langkah di bawah memanggil satu ekspresi lewat snippet ini, dengan `<EXPR>` diganti:
```csharp
using UnityEditor;
using UnityEngine;
using TycoWorld.Vehicles.Tests;
using TycoWorld.Player.Tests;
using TycoWorld.GameSettings.Tests;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        result.Log(<EXPR>);
    }
}
```
- "Tunggu sampai `script=done`" berarti memanggil `McpVehicleDriver.Status()` berulang sampai statusnya memuat `script=done`.
- Driver tidak memakai `static` yang bisa berubah: input skrip disimpan di motor, dan lintasan uji adalah objek scene.

- [ ] **Step 1: Tulis driver**

`Assets/Vehicles/Tests/McpVehicleDriver.cs`:
```csharp
using System.Globalization;
using TycoWorld.Player;
using TycoWorld.Player.Tests;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.Vehicles.Tests
{
    /// <summary>
    /// Dev helper for MCP checks of the motorbike in Play mode (not a test). Keeps no static state: the scripted input
    /// lives on the bike (IntentSourceOverride) and the test track is a scene object destroyed by Release or by
    /// leaving Play.
    /// </summary>
    public static class McpVehicleDriver
    {
        public const string TrackName = "VehicleTestTrack";
        /// <summary>Flat test track far from the world (same spot as the EditMode fixture); its wall face is 150 m ahead (+z).</summary>
        public static Vector3 TrackStart => new Vector3(7000f, 1000f, 7000f);
        public const float TrackWallZ = 7150f;

        static Motorbike Bike => Object.FindAnyObjectByType<Motorbike>();
        static FirstPersonPlayer Player => Object.FindAnyObjectByType<FirstPersonPlayer>();

        public static string Status()
        {
            if (!EditorApplication.isPlaying) return "NOT PLAYING";
            var bike = Bike;
            if (bike == null) return "NO BIKE";
            var inv = CultureInfo.InvariantCulture;
            var script = bike.IntentSourceOverride as ScriptedVehicleIntentSource;
            var s = "mounted=" + bike.IsMounted
                    + " kmh=" + bike.SpeedKmh.ToString("F1", inv)
                    + " pos=" + Vec(bike.transform.position)
                    + " yaw=" + bike.Yaw.ToString("F1", inv)
                    + " bumps=" + bike.Bumps
                    + " headlight=" + bike.HeadlightOn
                    + " cam=" + (bike.CameraState != null ? bike.CameraState.Mode.ToString() : "-")
                    + " engine=" + (bike.Sound != null && bike.Sound.EngineRunning)
                    + " level=" + (bike.Sound != null ? bike.Sound.Level.ToString("F2", inv) : "-")
                    + " script=" + (script == null ? "none" : script.IsDone ? "done" : "busy")
                    + " timeScale=" + Time.timeScale.ToString("F2", inv);
            var player = Player;
            if (player == null || !player.IsInitialized) return s + " player=-";
            return s + " seated=" + player.IsSeated
                     + " lookFree=" + player.Locks.Allows(PlayerAbility.Look)
                     + " moveFree=" + player.Locks.Allows(PlayerAbility.Move)
                     + " feet=" + Vec(player.transform.position)
                     + " hudVehicle=" + player.Hud.VehicleVisible
                     + " hudKmh=" + player.Hud.VehicleSpeedKmh
                     + " hudExit=" + player.Hud.VehicleCanExit
                     + " crosshair=" + player.Hud.CrosshairVisible
                     + " msg=\"" + player.Hud.Message + "\"";
        }

        /// <summary>Gives the bike a scripted input source (until Release).</summary>
        public static string Install()
        {
            var bike = Bike;
            if (bike == null) return "NO BIKE";
            bike.SetIntentSource(new ScriptedVehicleIntentSource());
            return Status();
        }

        /// <summary>Back to keyboard/gamepad input; removes the test track.</summary>
        public static string Release()
        {
            var bike = Bike;
            if (bike != null) bike.SetIntentSource(null);
            var track = GameObject.Find(TrackName);
            if (track != null) Object.Destroy(track);
            return Status();
        }

        public static string Hold(float throttle, float steer, bool brake, float seconds)
        {
            var script = Script();
            if (script == null) return "NOT INSTALLED";
            script.Hold(new VehicleIntent { Throttle = throttle, Steer = steer, Brake = brake }, seconds);
            return Status();
        }

        public static string Press(bool horn, bool headlight, bool camera, bool exit)
        {
            var script = Script();
            if (script == null) return "NOT INSTALLED";
            script.Press(new VehicleIntent { Horn = horn, ToggleHeadlight = headlight, ToggleCamera = camera, Exit = exit });
            return Status();
        }

        /// <summary>Puts the player where they would get off (left, else right), facing the bike; then McpPlayerDriver.Face + Press(interact).</summary>
        public static string StandBeside()
        {
            var bike = Bike;
            var player = Player;
            if (bike == null || player == null || !player.IsInitialized) return "NO BIKE OR PLAYER";
            var stance = player.Tuning.body;
            if (!DismountFinder.TryFind(bike.transform, bike.Tuning.dismountSideOffset, stance.radius, stance.height, out var feet))
                return "NO ROOM";
            var toBike = bike.transform.position - feet;
            player.Teleport(feet, Mathf.Atan2(toBike.x, toBike.z) * Mathf.Rad2Deg);
            return "feet=" + Vec(feet) + " face=" + Vec(bike.transform.position + Vector3.up * 0.8f);
        }

        /// <summary>Opens the kos gate as if the player standing at the bike pressed E on it.</summary>
        public static string OpenGate()
        {
            var gate = Object.FindAnyObjectByType<GateInteractable>();
            var player = Player;
            if (gate == null || player == null) return "NO GATE OR PLAYER";
            bool done = gate.Interact(new InteractionContext(player, player.Hud, player.View.position));
            return "gate interact=" + done + " state=" + gate.State;
        }

        /// <summary>Turns the bike on the spot towards the gate (a verification shortcut) and returns the gate position.</summary>
        public static string AimAtGate()
        {
            var gate = Object.FindAnyObjectByType<GateInteractable>();
            var bike = Bike;
            if (gate == null || bike == null) return "NO GATE OR BIKE";
            var to = gate.transform.position - bike.transform.position;
            bike.Teleport(bike.transform.position, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
            return "gate=" + Vec(gate.transform.position) + " dist=" + new Vector2(to.x, to.z).magnitude.ToString("F2", CultureInfo.InvariantCulture) + " " + Status();
        }

        /// <summary>Drops the bike (and its rider) on the ground at x, z.</summary>
        public static string PlaceAt(float x, float z, float yaw)
        {
            var bike = Bike;
            if (bike == null) return "NO BIKE";
            if (!Physics.Raycast(new Vector3(x, 300f, z), Vector3.down, out var hit, 600f, PlayerLayers.QueryMask, QueryTriggerInteraction.Ignore))
                return "NO GROUND";
            bike.Teleport(hit.point, yaw);
            return Status();
        }

        /// <summary>40 × 400 m floor with a wall whose face is at z = TrackWallZ; puts the bike at TrackStart facing +z.</summary>
        public static string TestTrack()
        {
            var bike = Bike;
            if (bike == null) return "NO BIKE";
            if (GameObject.Find(TrackName) == null)
            {
                var track = new GameObject(TrackName);
                var start = TrackStart;
                Block(track.transform, "Floor", start + new Vector3(0f, -0.5f, 150f), new Vector3(40f, 1f, 400f));
                Block(track.transform, "Wall", new Vector3(start.x, start.y + 1.5f, TrackWallZ + 0.25f), new Vector3(20f, 3f, 0.5f));
                Physics.SyncTransforms();
            }
            bike.Teleport(TrackStart, 0f);
            return Status();
        }

        public static string Shot(string path) => McpRender.SavePng(Camera.main, path, 1280, 720);

        public static string Luminance() => McpRender.CenterLuminance(Camera.main).ToString("F4", CultureInfo.InvariantCulture);

        static ScriptedVehicleIntentSource Script()
        {
            var bike = Bike;
            return bike != null ? bike.IntentSourceOverride as ScriptedVehicleIntentSource : null;
        }

        static void Block(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = size;
        }

        static string Vec(Vector3 v) =>
            string.Format(CultureInfo.InvariantCulture, "({0:F2},{1:F2},{2:F2})", v.x, v.y, v.z);
    }
}
```

Lalu jalankan Procedure R+T dengan `Group = "VehiclesSmokeTests"` untuk memastikan semuanya ter-compile.
Expected: `passed=1 failed=0`.

- [ ] **Step 2: Catat kondisi sebelum Play**

```bash
git -C "D:/UNITY/project/Tyco World" status --porcelain > "D:/UNITY/project/Tyco World/.superpowers/sdd/2026-10-14-motorbike/status-before.txt"
mkdir -p "D:/UNITY/project/Tyco World/.superpowers/sdd/2026-10-14-motorbike/shots"
```
Catat error/warning yang sudah ada di Console sebelum Play lewat `mcp__unity-mcp__Unity_GetConsoleLogs` (`logTypes: "warning,error"`), supaya Step 8 bisa membedakan yang baru.

- [ ] **Step 3: Masuk Play dan naik motor di kos**

1. Procedure P (masuk Play) sampai `READY`.
2. `McpPerfProbe.SetTime(10f)` → `t=10:00`.
3. `McpVehicleDriver.Status()` → `mounted=False`, `engine=False`. Posisi motor = pose parkir dari Task 11.
4. `McpVehicleDriver.StandBeside()` → `feet=… face=(x,y,z)`.
5. `McpPlayerDriver.Install()`, lalu `McpPlayerDriver.Face(x, y, z)` memakai angka `face` dari langkah 4.
6. `McpPlayerDriver.Press(true, false, false, false)`. Tunggu sampai `Status()` menampilkan `mounted=True`.

Expected:
- `seated=True`, `moveFree=False`, `lookFree=True`, `engine=True`;
- `hudVehicle=True`, `crosshair=False`, `cam=ThirdPerson`.

Ambil screenshot: `McpVehicleDriver.Shot(".../shots/kos-third.png")`.

- [ ] **Step 4: Keluar lewat pagar (upaya terbaik)**

1. `McpVehicleDriver.Install()`.
2. `McpVehicleDriver.OpenGate()` → `state=Opening` atau `Open`. Tunggu 2 s game, cek dengan `Status()` berulang.
3. `McpVehicleDriver.AimAtGate()` → catat `dist`.
4. `McpVehicleDriver.Hold(0.35f, 0f, false, 6f)`, lalu tunggu `script=done`.

Expected:
- motor melewati garis pagar (jarak dari posisi parkir > `dist` + 1 m);
- `bumps=0`.

Kalau motor tertahan, catat posisi dan `bumps` di laporan, lalu lanjut. Ini bukan syarat lulus; QA manual mengecek lewat pagar dengan keyboard asli.

- [ ] **Step 5: Lintasan uji — kecepatan puncak dan tembok**

1. `McpVehicleDriver.TestTrack()` → `pos=(7000.00,1000.00,7000.00)`.
2. `McpVehicleDriver.Hold(1f, 0f, false, 10f)`, lalu tunggu `script=done`.
   Expected: `kmh` 59,0–60,0; `pos.z − 7000` antara 105 dan 135 m (perhitungan: ±121 m); `hudKmh` sama dengan `kmh` yang dibulatkan; `bumps=0`.
3. `McpVehicleDriver.Hold(1f, 0f, false, 4f)`, lalu tunggu `script=done`.
   Expected: `kmh=0.0`; `bumps=1`; ujung depan `pos.z + 0.95 ≤ 7150.01` (tidak menembus tembok).

- [ ] **Step 6: Jalan Sukabirus — kamera, pause, malam, performa**

1. `McpVehicleDriver.PlaceAt(-287.7f, -633.3f, 57f)` → `mounted=True`.
2. `McpVehicleDriver.Hold(0.6f, 0f, false, 3f)`, tunggu `script=done`, lalu `Shot(".../shots/road-third.png")`.
3. `McpVehicleDriver.Press(false, false, true, false)` → setelah 1 frame `cam=FirstPerson`. Lalu `Shot(".../shots/road-first.png")` dan `Press(false, false, true, false)` lagi → `cam=ThirdPerson`.
4. **Pause:**
   - `McpVehicleDriver.Hold(1f, 0f, false, 6f)`, lalu satu `Status()`;
   - `McpGameDriver.Escape()` → `timeScale=0.00`, `audioPaused=True`;
   - dua `McpVehicleDriver.Status()` berselang ±2 s → `pos` dan `kmh` sama persis;
   - `McpGameDriver.Escape()` → `timeScale=1.00`, dan `Status()` berikutnya menunjukkan `pos` berubah lagi;
   - `lookFree=True` di semua status.
5. **Malam:**
   - `McpPerfProbe.SetTime(21f)`, lalu `McpVehicleDriver.Hold(0f, 0f, true, 2f)` dan tunggu `script=done`;
   - `McpVehicleDriver.Luminance()` → catat sebagai `off`;
   - `McpVehicleDriver.Press(false, true, false, false)` → `headlight=True`;
   - `Luminance()` → catat sebagai `on`;
   - `Shot(".../shots/night-headlight.png")`.
   Expected: `on > off`.
6. **Performa (preset Sedang, malam, melaju):**
   - `McpGameDriver.ApplyPreset("Medium", 0)`;
   - `McpVehicleDriver.Hold(0.5f, 0f, false, 9f)`;
   - `McpPerfProbe.Begin(5f)`, lalu `McpPerfProbe.Report("ride-medium-night")` berulang sampai bukan `PENDING`.
   Catat `fps`, `ms`, `worst`, dan `lights`. Bandingkan dengan catatan #4 (Medium malam 203,9 fps).
   **Jangan** panggil `McpPerfProbe.Release()` di sini, karena fungsi itu mematikan `runInBackground`.

- [ ] **Step 7: Turun, lalu teleport saat naik**

1. `McpVehicleDriver.Hold(0f, 0f, true, 3f)`, tunggu `script=done` → `kmh` ≤ 5, `hudExit=True`.
2. `McpVehicleDriver.Press(false, false, false, true)`, tunggu 1 frame.
   Expected:
   - `mounted=False`, `seated=False`, `moveFree=True`, `hudVehicle=False`, `crosshair=True`;
   - `engine=False`, `headlight=False`;
   - `feet` berjarak ±1,1 m dari `pos` motor.
3. `McpPlayerDriver.Hold(0f, 1f, false, 1f)`, lalu satu detik kemudian `McpPlayerDriver.Status()` → posisi pemain berubah (bisa jalan lagi).
4. Naik lagi dengan Step 3 butir 4–6. Lalu `McpPlayerDriver.Spawn()` untuk mendapatkan koordinat spawn, dan `McpPlayerDriver.TeleportTo(x, y, z, yaw)` ke koordinat itu.
   Expected `McpVehicleDriver.Status()`: `mounted=False`, `seated=False`, `moveFree=True`, `hudVehicle=False`, `engine=False`.

- [ ] **Step 8: Keluar Play dan cek kebersihan**

1. `McpVehicleDriver.Release()`, lalu `McpPerfProbe.Release()`.
2. Procedure P (keluar Play).
3. Bandingkan status git:
   ```bash
   git -C "D:/UNITY/project/Tyco World" status --porcelain > "D:/UNITY/project/Tyco World/.superpowers/sdd/2026-10-14-motorbike/status-after.txt"
   diff "D:/UNITY/project/Tyco World/.superpowers/sdd/2026-10-14-motorbike/status-before.txt" "D:/UNITY/project/Tyco World/.superpowers/sdd/2026-10-14-motorbike/status-after.txt" && echo SAME
   ```
   Expected: `SAME`.
4. `mcp__unity-mcp__Unity_GetConsoleLogs` (`logTypes: "warning,error"`). Expected: tidak ada error, dan tidak ada warning `[Vehicle]`/`[Player]` baru. Warning bawaan Unity yang sudah ada sebelum Play boleh dicatat tanpa diperbaiki.

Kalau ada kriteria yang gagal, **jangan** mengubah kode produk di task ini. Catat angkanya di laporan dengan status `DONE_WITH_CONCERNS`.

- [ ] **Step 9: Tulis catatan verifikasi**

`docs/superpowers/notes/2026-10-14-motorbike-verification.md` (isi angka nyata dari Step 3–8):
```markdown
# Motor yang bisa dikendarai — verifikasi (2026-10-14)

Spec: `docs/superpowers/specs/2026-10-14-motorbike-design.md`. Plan: `docs/superpowers/plans/2026-10-14-motorbike.md`.

## Tes otomatis
- `McpTestRunner.Run(".")`: <passed> lulus, 0 gagal (<jumlah> tes Vehicles).

## Play lewat MCP (input skrip)
| Cek | Hasil |
|---|---|
| Naik di kos (E lewat Interactor) | <mounted, kunci, HUD> |
| Keluar lewat pagar | <berhasil / tertahan di …> |
| Kecepatan setelah 10 s gas penuh | <kmh> km/j, jarak <m> m |
| Tembok lurus 60 km/j | berhenti di z <…> (muka tembok 7150), bumps <n> |
| Pause saat melaju | posisi tetap, audio pause, lanjut normal |
| Kamera V | orang ketiga ↔ orang pertama |
| Lampu depan malam | luminansi tengah <off> → <on> |
| Performa Sedang, malam, melaju | <fps> fps (<ms> ms, terburuk <worst> ms), lampu aktif <n> |
| Turun saat berhenti | kaki di <sisi>, kontrol kembali |
| Teleport saat naik | otomatis turun, kunci dilepas |
| `git status` sebelum/sesudah Play | sama |

Screenshot: `kos-third.png`, `road-third.png`, `road-first.png`, `night-headlight.png` (dikirim ke user, tidak di-commit).

## Catatan
- Setelah "Tyco World/Re-place Kos Frontyard", jalankan "Tyco World/Vehicle Setup" lagi supaya motor pindah ke halaman yang baru.
- Audio: <rekaman CC0 atau sintesis, lihat `Assets/Vehicles/Audio/SOURCES.md`>.

## Checklist QA manual (input asli tidak bisa diotomasi)
- [ ] Jalan ke motor, "[E] Naik motor" muncul; E → duduk, mesin starter lalu langsam, HUD speedometer tampil.
- [ ] W/S/A/D dan panah: gas, rem, mundur pelan saat diam, belok terasa wajar; Space rem keras.
- [ ] Mouse memutar kamera orang ketiga; setelah ±1,5 s kamera kembali ke belakang saat melaju.
- [ ] V: orang pertama (bisa menoleh, ada roll saat belok) ↔ orang ketiga.
- [ ] Gang sempit: kamera maju mendekat, tidak menembus tembok.
- [ ] Lewat pagar kos, naik turun undakan jalan, tanjakan.
- [ ] Menabrak tembok: berhenti + bunyi benturan; menyerong: meluncur di sepanjang tembok.
- [ ] H klakson ("tin-tin"), tidak bisa di-spam lebih cepat dari 0,4 s.
- [ ] F lampu depan; "LAMPU" di HUD terang/redup; malam jalan di depan terang.
- [ ] Rem: lampu belakang lebih terang.
- [ ] E saat melaju → "Pelankan motor dulu."; E saat diam → turun di kiri (atau kanan), "[E] Turun" hanya tampil saat boleh turun.
- [ ] Esc saat melaju → pause (motor diam, suara berhenti); lanjut → kursor terkunci lagi dan kontrol normal.
- [ ] Slider Efek mengubah volume mesin, klakson, dan benturan; Master mengubah semuanya.
- [ ] Gamepad (kalau ada): trigger gas/rem, stik kiri belok, stik kanan kamera.
```

- [ ] **Step 10: Regresi penuh dan commit**

Procedure R+T, `Group = "."`.
Expected: `failed=0`. Catat jumlah `passed` di catatan.

```bash
git -C "D:/UNITY/project/Tyco World" add Assets/Vehicles/Tests/McpVehicleDriver.cs Assets/Vehicles/Tests/McpVehicleDriver.cs.meta docs/superpowers/notes/2026-10-14-motorbike-verification.md
git -C "D:/UNITY/project/Tyco World" commit -m "test(vehicles): MCP ride driver and motorbike verification notes

Co-Authored-By: <model> <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Co5wtHmC6pVpdY73ZWdXu5"
```

Laporan task ini menyertakan path keempat screenshot, supaya controller bisa mengirimkannya ke user.
