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
