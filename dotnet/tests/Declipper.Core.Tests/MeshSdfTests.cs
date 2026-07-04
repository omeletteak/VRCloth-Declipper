using System;
using System.Collections.Generic;
using System.Numerics;
using Declipper.Core.Sdf;
using NUnit.Framework;

namespace Declipper.Core.Tests
{
    /// <summary>
    /// Tests the ported mesh SDF against its own brute-force reference on a
    /// synthetic closed box (procedural — no purchased geometry). The BVH is
    /// exact for distance and the Barnes–Hut winding recurses to exact leaves on
    /// a mesh this small, so the accelerated <see cref="MeshSdf.Sample"/> must
    /// agree with <see cref="MeshSdf.SignedDistanceBruteForce"/>.
    /// </summary>
    public class MeshSdfTests
    {
        // Axis-aligned box centered at the origin, half-extent `h`. Faces are
        // reoriented outward via the centroid so the winding number reads a
        // coherent ±1 inside (the box is convex, so outward == away-from-center).
        static void BuildBox(float h, out Vector3[] verts, out int[] tris)
        {
            verts = new[]
            {
                new Vector3(-h, -h, -h), new Vector3(h, -h, -h),
                new Vector3(h, h, -h),   new Vector3(-h, h, -h),
                new Vector3(-h, -h, h),  new Vector3(h, -h, h),
                new Vector3(h, h, h),    new Vector3(-h, h, h),
            };
            int[][] quads =
            {
                new[] { 0, 1, 2, 3 }, new[] { 4, 5, 6, 7 }, // z-, z+
                new[] { 0, 1, 5, 4 }, new[] { 3, 2, 6, 7 }, // y-, y+
                new[] { 0, 3, 7, 4 }, new[] { 1, 2, 6, 5 }, // x-, x+
            };
            var t = new List<int>();
            var center = Vector3.Zero;
            foreach (var q in quads)
            {
                AddOutward(t, verts, center, q[0], q[1], q[2]);
                AddOutward(t, verts, center, q[0], q[2], q[3]);
            }
            tris = t.ToArray();
        }

        static void AddOutward(List<int> tris, Vector3[] verts, Vector3 center, int i0, int i1, int i2)
        {
            Vector3 n = Vector3.Cross(verts[i1] - verts[i0], verts[i2] - verts[i0]);
            Vector3 faceCenter = (verts[i0] + verts[i1] + verts[i2]) / 3f;
            if (Vector3.Dot(n, faceCenter - center) < 0f)
            {
                (i1, i2) = (i2, i1); // flip so the normal points away from the box center
            }
            tris.Add(i0); tris.Add(i1); tris.Add(i2);
        }

        [Test]
        public void Sample_MatchesBruteForceReference()
        {
            BuildBox(0.5f, out var verts, out var tris);
            var sdf = new MeshSdf(verts, tris);

            var probes = new[]
            {
                new Vector3(0f, 0f, 0f),      // deep inside  -> negative
                new Vector3(0.2f, -0.1f, 0.3f), // inside
                new Vector3(2f, 0f, 0f),      // far outside  -> positive
                new Vector3(0.8f, 0.8f, 0.8f),  // outside near a corner
                new Vector3(0.5f, 0.1f, -0.2f), // near a face
                new Vector3(-0.9f, 0.4f, 0.1f), // outside a face
            };

            foreach (var p in probes)
            {
                float expected = MeshSdf.SignedDistanceBruteForce(verts, tris, p);
                SdfSample s = sdf.Sample(p);
                Assert.That(s.Distance, Is.EqualTo(expected).Within(1e-4f), $"distance at {p}");
                Assert.That(s.Gradient.Length(), Is.EqualTo(1f).Within(1e-4f), $"unit gradient at {p}");
            }
        }

        [Test]
        public void Sample_SignIsNegativeInsideAndPositiveOutside()
        {
            BuildBox(0.5f, out var verts, out var tris);
            var sdf = new MeshSdf(verts, tris);

            Assert.That(sdf.Sample(Vector3.Zero).Distance, Is.LessThan(0f), "center is inside");
            Assert.That(sdf.Sample(new Vector3(0f, 0f, 0f)).Distance, Is.EqualTo(-0.5f).Within(1e-4f),
                "center is 0.5 from every face");
            Assert.That(sdf.Sample(new Vector3(3f, 0f, 0f)).Distance, Is.GreaterThan(0f), "far point is outside");
        }

        [Test]
        public void Sample_GradientPushesOutward()
        {
            BuildBox(0.5f, out var verts, out var tris);
            var sdf = new MeshSdf(verts, tris);

            // Just outside the +x face: gradient should point roughly +x.
            SdfSample s = sdf.Sample(new Vector3(0.6f, 0f, 0f));
            Assert.That(s.Gradient.X, Is.GreaterThan(0.9f));

            // Just inside the +x face: gradient still points outward (+x), toward
            // the nearest surface exit.
            SdfSample inside = sdf.Sample(new Vector3(0.4f, 0f, 0f));
            Assert.That(inside.Gradient.X, Is.GreaterThan(0.9f));
            Assert.That(inside.Distance, Is.LessThan(0f));
        }

        [Test]
        public void EmptyMesh_IsAlwaysOutside()
        {
            var sdf = new MeshSdf(Array.Empty<Vector3>(), Array.Empty<int>());
            Assert.That(sdf.IsValid, Is.False);
            SdfSample s = sdf.Sample(Vector3.Zero);
            Assert.That(s.Distance, Is.EqualTo(float.MaxValue));
            Assert.That(s.Gradient.Length(), Is.EqualTo(1f).Within(1e-6f));
        }
    }
}
