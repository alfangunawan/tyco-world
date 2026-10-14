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
                   + " cursor=" + UnityEngine.Cursor.lockState
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
            screen.y = Screen.height - screen.y; // ScreenToPanel keeps the top-left origin of the panel; Input System positions start at the bottom
            var state = new MouseState { position = screen };
            if (press) state = state.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left, true);
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

        /// <summary>RuntimePanelUtils.ScreenToPanel is linear and scales only (no y flip here); invert it from two samples.</summary>
        static Vector2 PanelToScreen(IPanel panel, Vector2 panelPoint)
        {
            var origin = RuntimePanelUtils.ScreenToPanel(panel, Vector2.zero);
            var unit = RuntimePanelUtils.ScreenToPanel(panel, Vector2.one) - origin;
            return new Vector2((panelPoint.x - origin.x) / unit.x, (panelPoint.y - origin.y) / unit.y);
        }
    }
}
