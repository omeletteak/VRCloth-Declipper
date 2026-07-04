using System.Collections.Generic;
using System.Numerics;
using Declipper.Core.Sdf;
using Declipper.Core.Solver;
using NUnit.Framework;

namespace Declipper.Core.Tests
{
    /// <summary>
    /// Analytic tests for the S1 mechanical ports (VertexAdjacency,
    /// LaplacianSmoothing, PenetrationDetection). These pieces are pure
    /// combinatorial / vector logic, so constructive assertions pin them more
    /// tightly than a v1 golden dump would; the golden fixtures gate the
    /// float-heavy pieces (MeshSdf, full solver) and the S2 replacement.
    /// </summary>
    public class SolverPortTests
    {
        // A quad split into two triangles, sharing edge 0-1, with vertex 3 a
        // positional duplicate of vertex 1 (a UV/normal seam clone).
        //   p0=(0,0,0) p1=(1,0,0) p2=(0,1,0) p3=(1,0,0)==p1
        //   tri A: 0,1,2   tri B: 0,3,2
        static VertexAdjacency SeamedQuad()
        {
            var positions = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(0f, 1f, 0f),
                new Vector3(1f, 0f, 0f),
            };
            var triangles = new[] { 0, 1, 2, 0, 3, 2 };
            return VertexAdjacency.Build(positions, triangles);
        }

        [Test]
        public void Build_WeldsCoincidentVerticesAndMergesTopology()
        {
            var adj = SeamedQuad();

            Assert.That(adj.VertexCount, Is.EqualTo(4));
            // vertex 3 shares vertex 1's position -> same cluster.
            Assert.That(adj.RepresentativeOf(3), Is.EqualTo(1));
            Assert.That(adj.MembersOf(1), Is.EquivalentTo(new[] { 1, 3 }));
            // Both triangles' edges collapse onto the welded cluster, so 1's
            // neighborhood is just {0,2} (not a torn seam).
            Assert.That(adj.NeighborsOf(1), Is.EquivalentTo(new[] { 0, 2 }));
        }

        [Test]
        public void ExpandRegion_GrowsByRingsAndMapsSeedsToRepresentatives()
        {
            var adj = SeamedQuad();

            // Seed the clone (vertex 3); it maps to representative 1.
            var r0 = LaplacianSmoothing.ExpandRegion(adj, new[] { 3 }, 0);
            Assert.That(r0, Is.EquivalentTo(new[] { 1 }));

            var r1 = LaplacianSmoothing.ExpandRegion(adj, new[] { 3 }, 1);
            Assert.That(r1, Is.EquivalentTo(new[] { 1, 0, 2 }));
        }

        [Test]
        public void Smooth_BlendsTowardNeighborAverageAndWritesClonesTogether()
        {
            var adj = SeamedQuad();

            // Field: anchors on reps 0 and 2, the smoothed value lands on rep 1
            // and must be mirrored onto its clone (vertex 3).
            var displacements = new[]
            {
                new Vector3(2f, 0f, 0f), // rep 0 (anchor, outside region)
                Vector3.Zero,            // rep 1 (smoothed)
                new Vector3(4f, 0f, 0f), // rep 2 (anchor, outside region)
                Vector3.Zero,            // vertex 3 == clone of rep 1
            };
            var region = new HashSet<int> { 1 };

            LaplacianSmoothing.Smooth(displacements, adj, region, lambda: 1f, iterations: 1);

            // Lerp(0, avg(2,4)=3, 1) = 3, on both the rep and its clone.
            Assert.That(displacements[1], Is.EqualTo(new Vector3(3f, 0f, 0f)));
            Assert.That(displacements[3], Is.EqualTo(new Vector3(3f, 0f, 0f)));
            // Anchors untouched.
            Assert.That(displacements[0], Is.EqualTo(new Vector3(2f, 0f, 0f)));
            Assert.That(displacements[2], Is.EqualTo(new Vector3(4f, 0f, 0f)));
        }

        [Test]
        public void Scan_ReportsMarginHitsInOrderWithDepthBelowMarginSurface()
        {
            // Sphere of radius 0.5 at the origin.
            var body = new CapsuleSetSdf(new[] { new Capsule(Vector3.Zero, Vector3.Zero, 0.5f) });
            const float margin = 0.05f;

            var positions = new[]
            {
                new Vector3(0.2f, 0f, 0f),  // dist -0.3  -> hit, depth 0.35
                new Vector3(0.5f, 0f, 0f),  // dist  0.0  -> hit, depth 0.05
                new Vector3(0.54f, 0f, 0f), // dist  0.04 -> hit, depth 0.01
                new Vector3(0.6f, 0f, 0f),  // dist  0.10 -> no hit
            };

            List<PenetrationHit> hits = PenetrationDetection.Scan(positions, body, margin);

            Assert.That(hits.Count, Is.EqualTo(3));
            Assert.That(hits[0].VertexIndex, Is.EqualTo(0));
            Assert.That(hits[0].Depth, Is.EqualTo(0.35f).Within(1e-5f));
            Assert.That(hits[1].VertexIndex, Is.EqualTo(1));
            Assert.That(hits[1].Depth, Is.EqualTo(0.05f).Within(1e-5f));
            Assert.That(hits[2].VertexIndex, Is.EqualTo(2));
            Assert.That(hits[2].Depth, Is.EqualTo(0.01f).Within(1e-5f));
        }
    }
}
