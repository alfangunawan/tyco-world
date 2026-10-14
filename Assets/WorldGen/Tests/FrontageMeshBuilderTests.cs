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

        RoadChain Chain(params Vector2[] pts)
        {
            var rule = settings.roadRules.Single(r => r.highway == "residential");
            var road = new RoadFeature { Id = 2, Highway = "residential", Rule = rule, Width = 5f, Y = 0.08f, Tags = TestOsm.Tags(("highway", "residential")) };
            road.Pieces.Add(new LinePiece(new List<Vector2>(pts), false, false));
            return new RoadNetwork(new[] { road }, new Polygon[0], settings.roadEdges).Chains[0];
        }

        [Test]
        public void GentleBend_DoesNotStackPilasters()
        {
            // 40 m arc, a vertex every 1 m turning 1.5 deg each: no interior vertex reaches the 20 deg threshold.
            var pts = new List<Vector2> { Vector2.zero };
            float heading = 0f;
            for (int i = 0; i < 40; i++)
            {
                heading += 1.5f * Mathf.Deg2Rad;
                pts.Add(pts[pts.Count - 1] + new Vector2(Mathf.Cos(heading), Mathf.Sin(heading)));
            }
            var chain = Chain(pts.ToArray());
            var r = Build(Run(chain, WallStyle.Plaster, 1.8f, 0, 0f, chain.Length));
            var p = r.PilasterPositions;
            for (int i = 0; i < p.Count; i++)
                for (int j = i + 1; j < p.Count; j++)
                    Assert.GreaterOrEqual(Vector2.Distance(p[i], p[j]), settings.frontage.pilasterMinGap - 1e-3f, i + " vs " + j);
            Assert.Less(r.Pilasters, 25, "about one per 1 m segment would be ~41");
            Assert.GreaterOrEqual(r.Pilasters, 12, "the arc still gets a pilaster about every 3 m (spacing along the whole piece)");
        }

        [Test]
        public void SharpCorner_KeepsItsPilaster()
        {
            var chain = Chain(new Vector2(0f, 0f), new Vector2(20f, 0f), new Vector2(20f, -20f));
            var r = Build(Run(chain, WallStyle.Plaster, 1.8f, 0, 2f, 38f));
            // East then south is a right turn, so the wall on the right (-1) side is on the inside of the 90 deg corner: a pilaster must stand on the offset corner vertex.
            var along = new List<float>();
            var line = Polyline2D.Offset(Polyline2D.Slice(chain.Points, chain.Cum, 2f, 38f, along), along, -(FrontagePlanner.LineOffset(chain, EdgeProfile.Gutter, settings.roadEdges, settings.frontage) + settings.frontage.wallThickness * 0.5f), new List<float>());
            var corner = line[1];
            Assert.IsTrue(r.PilasterPositions.Any(q => Vector2.Distance(q, corner) < 0.3f));
        }

        [Test]
        public void Door_KeepsBothPosts()
        {
            var r = Build(Run(Straight(), WallStyle.Plaster, 1.8f, 0, 2f, 18f, new WallOpening { S = 10f, Width = 0.9f, IsGate = false }));
            Assert.IsTrue(r.PilasterPositions.Any(q => Mathf.Abs(q.x - 9.55f) < 0.01f));
            Assert.IsTrue(r.PilasterPositions.Any(q => Mathf.Abs(q.x - 10.45f) < 0.01f));
            Assert.AreEqual(1, r.Doors);
        }

        /// <summary>A right-angle-free bend of 30°; turn +1 turns left, so the right-hand (-1) wall is on the outside.</summary>
        RoadChain Bend(float turn)
        {
            float a = 30f * Mathf.Deg2Rad;
            return Chain(new Vector2(0f, 0f), new Vector2(20f, 0f), new Vector2(20f + 20f * Mathf.Cos(a), turn * 20f * Mathf.Sin(a)));
        }

        [Test]
        public void GateOnTheInsideOfABend_FoldsAway_AndTheWallRunsOn()
        {
            var chain = Bend(-1f);
            var r = Build(Run(chain, WallStyle.Plaster, 1.8f, 0, 2f, chain.Length - 2f, Gate(20f)));
            Assert.AreEqual(0, r.Gates, "the 2 m span folds to nothing on the inside, so no gate is drawn");
            Assert.AreEqual(0, r.Slabs);
            // The wall is one unbroken line: every blocker end but the two run ends meets another blocker.
            var ends = r.Blockers.SelectMany(b => new[] { b.A, b.B }).ToList();
            int loose = ends.Count(p => ends.Count(o => Vector2.Distance(o, p) < 0.01f) < 2);
            Assert.AreEqual(2, loose, "no gap where the gate would have been");
            var corner = r.PilasterPositions.Where(p => Vector2.Distance(p, new Vector2(20f, 0f)) < 6f).ToList();
            for (int i = 0; i < corner.Count; i++)
                for (int j = i + 1; j < corner.Count; j++)
                    Assert.GreaterOrEqual(Vector2.Distance(corner[i], corner[j]), settings.frontage.pilasterMinGap - 1e-3f, "posts bunched at the corner");
        }

        [Test]
        public void GateOnTheOutsideOfABend_MeetsItsWallPiecesAndStaysBehindTheLine()
        {
            var chain = Bend(1f);
            var r = Build(Run(chain, WallStyle.Plaster, 1.8f, 0, 2f, chain.Length - 2f, Gate(20f)));
            var f = settings.frontage;
            float lineOffset = FrontagePlanner.LineOffset(chain, EdgeProfile.Gutter, settings.roadEdges, f);
            var along = new List<float>();
            var span = Polyline2D.Offset(Polyline2D.Slice(chain.Points, chain.Cum, 19f, 21f, along), along, -(lineOffset + f.wallThickness * 0.5f), new List<float>());
            Vector2 g0 = span[0], g1 = span[span.Count - 1];
            Assert.AreEqual(1, r.Gates);
            Assert.IsTrue(r.PilasterPositions.Any(q => Vector2.Distance(q, g0) < 0.01f), "wall piece ends at the gate start");
            Assert.IsTrue(r.PilasterPositions.Any(q => Vector2.Distance(q, g1) < 0.01f), "wall piece ends at the gate end");
            Assert.IsTrue(r.Blockers.Any(b => Vector2.Distance(b.A, g0) < 0.01f), "leaf starts on the wall end");
            Assert.IsTrue(r.Blockers.Any(b => Vector2.Distance(b.B, g1) < 0.01f), "leaf ends on the wall end");
            foreach (var m in r.Render.Values)
                foreach (var v in Verts(m, FrontageMeshBuilder.SubMetal))
                    Assert.GreaterOrEqual(Polyline2D.DistanceTo(chain.Points, new Vector2(v.x, v.z)), lineOffset - 0.01f, "leaf render vertex " + v);
            foreach (var bl in r.Blockers)
                foreach (var q in new[] { bl.A, bl.B })
                    if (Vector2.Distance(q, g0) < 2.6f)
                        Assert.GreaterOrEqual(Polyline2D.DistanceTo(chain.Points, q), lineOffset - 0.01f, "blocker end " + q);
        }

        [Test]
        public void ProfileChange_JoinsTheTwoWallLinesWithAReturn()
        {
            var chain = Straight();
            var sidewalk = Run(chain, WallStyle.Plaster, 1.8f, 0, 2f, 10f);
            sidewalk.Profile = EdgeProfile.Sidewalk;
            var gutter = Run(chain, WallStyle.Plaster, 1.8f, 0, 10f, 18f);
            var r = Build(sidewalk, gutter);
            var f = settings.frontage;
            float half = f.wallThickness * 0.5f;
            var a = new Vector2(10f, -(FrontagePlanner.LineOffset(chain, EdgeProfile.Sidewalk, settings.roadEdges, f) + half));
            var b = new Vector2(10f, -(FrontagePlanner.LineOffset(chain, EdgeProfile.Gutter, settings.roadEdges, f) + half));
            Assert.Greater(Vector2.Distance(a, b), 0.6f, "the property line jogs 0.7 m");
            Assert.IsTrue(r.Blockers.Any(s => (Vector2.Distance(s.A, a) < 0.01f && Vector2.Distance(s.B, b) < 0.01f) ||
                                             (Vector2.Distance(s.A, b) < 0.01f && Vector2.Distance(s.B, a) < 0.01f)), "a return wall closes the jog");
            var mid = 0.5f * (a + b);
            Assert.IsTrue(r.Collider.Values.Any(m => m.Vertices.Any(v => Mathf.Abs(v.x - mid.x) < 0.2f && Mathf.Abs(v.z - mid.y) < 0.5f)), "and blocks the player");
        }

        [Test]
        public void SameProfileNeighbours_NeedNoReturn()
        {
            var chain = Straight();
            var r = Build(Run(chain, WallStyle.Plaster, 1.8f, 0, 2f, 10f), Run(chain, WallStyle.Plaster, 1.8f, 0, 10f, 18f));
            Assert.IsFalse(r.Blockers.Any(s => Mathf.Abs(s.A.x - s.B.x) < 1e-3f), "no blocker runs across the road direction");
        }

        [Test, Timeout(20000)]
        public void ZeroPilasterSpacing_StillBuilds()
        {
            settings.frontage.pilasterSpacing = 0f;
            var r = Build(Run(Straight(), WallStyle.Plaster, 1.8f, 0, 2f, 18f));
            Assert.AreEqual(2, r.Pilasters, "only the two ends");
        }

        [Test]
        public void GentleBend_RendersNoHiddenFacesButKeepsClosedColliders()
        {
            var pts = new List<Vector2> { Vector2.zero };
            float heading = 0f;
            for (int i = 0; i < 10; i++)
            {
                heading += 2f * Mathf.Deg2Rad;
                pts.Add(pts[pts.Count - 1] + new Vector2(Mathf.Cos(heading), Mathf.Sin(heading)));
            }
            var chain = Chain(pts.ToArray());
            var r = Build(Run(chain, WallStyle.Plaster, 1.8f, 0, 0f, chain.Length));
            var wall = r.Render.Values.Single().Indices(FrontageMeshBuilder.SubWall).Count / 3;
            Assert.AreEqual(2 * (2 * 10 + 2), wall, "two side quads per segment plus the two piece-end faces, no top");
            int collider = r.Collider.Values.Single().Indices(0).Count / 3;
            Assert.GreaterOrEqual(collider, 10 * 10, "collider boxes keep all five faces");
        }
    }
}
