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
            DropLineMeshes(model.transform);
            if (spec.KeepRegion.HasValue) KeepRegion(model.transform, spec.KeepRegion.Value, spec.Id);
            // glTFast puts the file's Y-up turn on the root (Sketchfab: 270° about X); the yaw goes on top of it.
            model.transform.localRotation = Quaternion.Euler(0f, spec.YawFix, 0f) * model.transform.localRotation;
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
                    foreach (var n in spec.ExtraSteerNodes) Find(model.transform, n).SetParent(steer, true);
                    spinning.Add(Pivot(front, steer, "Spin_" + front.name, out radius));
                    foreach (var n in spec.RearWheels) spinning.Add(Pivot(Find(model.transform, n), lean, "Spin_" + n, out _));
                }
                else
                {
                    foreach (var n in spec.FrontWheels)
                    {
                        var wheel = Find(model.transform, n);
                        if (spec.SolidFrontAxle)
                        {
                            spinning.Add(Pivot(wheel, lean, "Spin_" + n, out radius));
                            continue;
                        }
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
            var tail = spec.TailLight ?? new Vector3(0f, bike ? b.max.y * 0.6f : b.max.y * 0.5f, b.min.z + 0.04f);
            if (!spec.Headlight.HasValue && Skin(model, head.x, head.y, 1f, out var headOnSkin)) head = headOnSkin;
            if (!spec.TailLight.HasValue && Skin(model, tail.x, tail.y, -1f, out var tailOnSkin)) tail = tailOnSkin;
            var headlight = NewChild(HeadlightName, lights).gameObject.AddComponent<Light>();
            headlight.transform.position = head;
            headlight.transform.localRotation = Quaternion.Euler(6f, 0f, 0f);
            headlight.type = LightType.Spot;
            MotorbikeVisuals.ConfigureHeadlight(headlight, tuning);
            headlight.color = VehicleMaterials.LensEmission;
            headlight.shadows = LightShadows.None;
            headlight.enabled = false;
            float lampSpread = bike ? 0f : b.extents.x - 0.25f;
            // Mesh names carry the id: SaveMeshes keys saved meshes by name, and each model's lamps differ.
            var lens = LampQuads(LensName, spec.Id, lights, model, head, lampSpread, 1f, materials.HeadlightLens);
            var tailLamp = LampQuads(TailName, spec.Id, lean, model, tail, lampSpread, -1f, materials.TailLight);

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

        /// <summary>
        /// Emissive lamp boxes (one on a bike, a pair on a car), each laid on the body's skin where a ray along
        /// <paramref name="facing"/> (+1 front, -1 back) meets the model, so none floats ahead of a rounded bumper.
        /// </summary>
        static Renderer LampQuads(string name, string id, Transform parent, GameObject model, Vector3 at, float spread, float facing,
            Material material)
        {
            const float Depth = 0.02f, Clearance = 0.002f;
            var go = NewChild(name, parent).gameObject;
            go.transform.position = at;
            var m = new TycoWorld.WorldGen.Authoring.MeshData(1);
            var xs = spread > 0f ? new[] { at.x - spread, at.x + spread } : new[] { at.x };
            bool onSkin = SkinRow(model, xs, at.y, facing, out float y, out var zs);
            for (int i = 0; i < xs.Length; i++)
            {
                var centre = onSkin ? new Vector3(xs[i], y, zs[i] + facing * (Depth * 0.5f + Clearance)) : new Vector3(xs[i], at.y, at.z);
                TycoWorld.WorldGen.Authoring.PlaceholderMeshes.AddBox(m, 0, centre - at, new Vector3(spread > 0f ? 0.28f : 0.15f, 0.1f, Depth));
            }
            go.AddComponent<MeshFilter>().sharedMesh = m.ToMesh("Vehicle_" + id + "_" + name, out _);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            return r;
        }

        /// <summary>
        /// The point on the model's front (or back) skin nearest in height to (x, y): the surface is looked for at y, then
        /// 5 cm lower and higher in turn, up to 40 cm away (lamp heights from the bounds can miss a scooter's narrow nose).
        /// </summary>
        static bool Skin(GameObject model, float x, float y, float facing, out Vector3 point)
        {
            bool found = SkinRow(model, new[] { x }, y, facing, out float rowY, out var zs);
            point = found ? new Vector3(x, rowY, zs[0]) : default;
            return found;
        }

        /// <summary>One height at which every x of a lamp pair meets the skin, so the pair stays level.</summary>
        static bool SkinRow(GameObject model, float[] xs, float y, float facing, out float rowY, out float[] zs)
        {
            zs = new float[xs.Length];
            for (int step = 0; step <= 16; step++)
            {
                rowY = y + (step + 1) / 2 * 0.05f * (step % 2 == 1 ? -1f : 1f);
                bool all = true;
                for (int i = 0; all && i < xs.Length; i++) all = SurfaceZ(model, xs[i], rowY, facing, out zs[i]);
                if (all) return true;
            }
            rowY = y;
            return false;
        }

        /// <summary>
        /// The outermost z of the model's surface at (x, y), seen from the front (<paramref name="facing"/> = +1) or the back
        /// (-1): the triangles are tested in the XY plane and the hit depth interpolated. False when no triangle covers the point.
        /// </summary>
        static bool SurfaceZ(GameObject model, float x, float y, float facing, out float z)
        {
            bool found = false;
            z = 0f;
            foreach (var f in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = f.sharedMesh;
                if (mesh == null) continue;
                var toWorld = f.transform.localToWorldMatrix;
                var v = mesh.vertices;
                for (int i = 0; i < v.Length; i++) v[i] = toWorld.MultiplyPoint3x4(v[i]);
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    if (mesh.GetTopology(s) != MeshTopology.Triangles) continue;
                    var tris = mesh.GetTriangles(s);
                    for (int t = 0; t < tris.Length; t += 3)
                    {
                        Vector3 a = v[tris[t]], b = v[tris[t + 1]], c = v[tris[t + 2]];
                        float d = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
                        if (Mathf.Abs(d) < 1e-9f) continue;
                        float u = ((b.y - c.y) * (x - c.x) + (c.x - b.x) * (y - c.y)) / d;
                        float w = ((c.y - a.y) * (x - c.x) + (a.x - c.x) * (y - c.y)) / d;
                        if (u < 0f || w < 0f || u + w > 1f) continue;
                        float hit = u * a.z + w * b.z + (1f - u - w) * c.z;
                        if (found && hit * facing <= z * facing) continue;
                        z = hit;
                        found = true;
                    }
                }
            }
            return found;
        }

        /// <summary>Line and point meshes (stray edges in some exports) draw as wires and have no triangles: drop their renderers.</summary>
        static void DropLineMeshes(Transform model)
        {
            foreach (var f in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = f.sharedMesh;
                bool triangles = mesh != null;
                for (int i = 0; triangles && i < mesh.subMeshCount; i++) triangles = mesh.GetTopology(i) == MeshTopology.Triangles;
                if (triangles) continue;
                var r = f.GetComponent<Renderer>();
                if (r != null) Object.DestroyImmediate(r);
                Object.DestroyImmediate(f);
            }
        }

        /// <summary>
        /// Keeps only the triangles whose centre lies in <paramref name="region"/> (the model's own space; the root is at the
        /// origin here). A mesh cut in part becomes a compact copy named after the id, saved later with the prefab's meshes.
        /// </summary>
        static void KeepRegion(Transform model, Bounds region, string id)
        {
            foreach (var f in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = f.sharedMesh;
                if (mesh == null) continue;
                var toWorld = f.transform.localToWorldMatrix;
                var vertices = mesh.vertices;
                var kept = new List<int>[mesh.subMeshCount];
                int keptCount = 0, total = 0;
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    kept[s] = new List<int>();
                    var tris = mesh.GetTriangles(s);
                    total += tris.Length;
                    for (int t = 0; t < tris.Length; t += 3)
                    {
                        var centre = (toWorld.MultiplyPoint3x4(vertices[tris[t]]) + toWorld.MultiplyPoint3x4(vertices[tris[t + 1]])
                                      + toWorld.MultiplyPoint3x4(vertices[tris[t + 2]])) / 3f;
                        if (!region.Contains(centre)) continue;
                        kept[s].Add(tris[t]);
                        kept[s].Add(tris[t + 1]);
                        kept[s].Add(tris[t + 2]);
                    }
                    keptCount += kept[s].Count;
                }
                if (keptCount == total) continue;
                if (keptCount == 0)
                {
                    var r = f.GetComponent<Renderer>();
                    if (r != null) Object.DestroyImmediate(r);
                    Object.DestroyImmediate(f);
                    continue;
                }
                f.sharedMesh = Compact(mesh, kept, id + "_" + mesh.name + "_Kept");
            }
        }

        /// <summary>A copy of <paramref name="mesh"/> with only the vertices the kept triangles use, so its bounds fit them.</summary>
        static Mesh Compact(Mesh mesh, List<int>[] kept, string name)
        {
            var map = new Dictionary<int, int>();
            var order = new List<int>();
            foreach (var list in kept)
                foreach (int i in list)
                    if (!map.ContainsKey(i))
                    {
                        map[i] = order.Count;
                        order.Add(i);
                    }
            T[] Pick<T>(T[] all) => all != null && all.Length == mesh.vertexCount ? order.Select(i => all[i]).ToArray() : null;
            var copy = new Mesh { name = name };
            if (order.Count > 65535) copy.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            copy.vertices = Pick(mesh.vertices);
            var normals = Pick(mesh.normals);
            if (normals != null) copy.normals = normals;
            var tangents = Pick(mesh.tangents);
            if (tangents != null) copy.tangents = tangents;
            var colors = Pick(mesh.colors32);
            if (colors != null) copy.colors32 = colors;
            for (int channel = 0; channel < 4; channel++)
            {
                var uv = new List<Vector2>();
                mesh.GetUVs(channel, uv);
                if (uv.Count == mesh.vertexCount) copy.SetUVs(channel, order.Select(i => uv[i]).ToList());
            }
            copy.subMeshCount = kept.Length;
            for (int s = 0; s < kept.Length; s++) copy.SetTriangles(kept[s].Select(i => map[i]).ToArray(), s);
            copy.RecalculateBounds();
            return copy;
        }

        static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            throw new System.ArgumentException("node not found: " + name);
        }

        /// <summary>
        /// Tight world bounds of the drawn meshes, from their vertices: Renderer.bounds boxes a node turned at an odd angle
        /// loosely (the Vario's handlebar made it 10% too long and off-centre).
        /// </summary>
        static Bounds DrawnBounds(GameObject go)
        {
            bool any = false;
            var b = new Bounds(go.transform.position, Vector3.zero);
            foreach (var f in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (f.sharedMesh == null || f.GetComponent<Renderer>() == null) continue;
                var toWorld = f.transform.localToWorldMatrix;
                foreach (var v in f.sharedMesh.vertices)
                {
                    var p = toWorld.MultiplyPoint3x4(v);
                    if (!any) b = new Bounds(p, Vector3.zero);
                    else b.Encapsulate(p);
                    any = true;
                }
            }
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
