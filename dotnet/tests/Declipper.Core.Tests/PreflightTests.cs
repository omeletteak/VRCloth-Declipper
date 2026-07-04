using System.Collections.Generic;
using System.Numerics;
using Declipper.Core.Diagnostics;
using Declipper.Core.Sdf;
using Declipper.Core.Solver;
using NUnit.Framework;

namespace Declipper.Core.Tests
{
    /// <summary>
    /// Analytic tests for the ported preflight diagnostic. The verdict/RedCause
    /// logic is a deterministic function of the detection statistics, so hits
    /// are constructed directly against a sphere SDF (thickness = radius). No
    /// v1 golden dump required.
    /// </summary>
    public class PreflightTests
    {
        // Sphere radius 0.5 -> LocalThickness == 0.5 everywhere.
        static ISignedDistanceField Sphere() =>
            new CapsuleSetSdf(new[] { new Capsule(Vector3.Zero, Vector3.Zero, 0.5f) });

        const float Margin = 0.01f;

        // depth below the body surface -> the hit depth the detector records.
        static PenetrationHit Hit(int v, float surfaceDepth) =>
            new PenetrationHit(v, surfaceDepth + Margin);

        static void BuildGrid(int n, float half, out Vector3[] positions, out int[] triangles)
        {
            positions = new Vector3[n * n];
            for (int r = 0; r < n; r++)
            for (int c = 0; c < n; c++)
                positions[r * n + c] = new Vector3(-half + 2f * half * c / (n - 1),
                                                   -half + 2f * half * r / (n - 1), 0f);
            var tris = new List<int>();
            for (int r = 0; r < n - 1; r++)
            for (int c = 0; c < n - 1; c++)
            {
                int a = r * n + c, b = r * n + c + 1, cc = (r + 1) * n + c, d = (r + 1) * n + c + 1;
                tris.Add(a); tris.Add(cc); tris.Add(b);
                tris.Add(b); tris.Add(cc); tris.Add(d);
            }
            triangles = tris.ToArray();
        }

        // `count` disjoint triangles (3*count distinct vertices, no shared edges).
        static void BuildDisjointTriangles(int count, out Vector3[] positions, out int[] triangles)
        {
            positions = new Vector3[count * 3];
            triangles = new int[count * 3];
            for (int t = 0; t < count; t++)
            {
                float x = t * 10f; // far apart so nothing welds
                positions[t * 3 + 0] = new Vector3(x, 0f, 0f);
                positions[t * 3 + 1] = new Vector3(x + 1f, 0f, 0f);
                positions[t * 3 + 2] = new Vector3(x, 1f, 0f);
                triangles[t * 3 + 0] = t * 3 + 0;
                triangles[t * 3 + 1] = t * 3 + 1;
                triangles[t * 3 + 2] = t * 3 + 2;
            }
        }

        [Test]
        public void ShallowSparse_IsGreen()
        {
            var positions = new Vector3[100];
            var hits = new List<PenetrationHit>();
            for (int v = 0; v < 5; v++) hits.Add(Hit(v, 0.005f)); // 5% ratio, depth 5mm

            var report = PreflightDiagnostic.Evaluate(positions, null, hits, Sphere(), Margin);

            Assert.That(report.Verdict, Is.EqualTo(PreflightVerdict.Green));
            Assert.That(report.RedCause, Is.EqualTo(RedCause.None));
            Assert.That(report.PenetratingCount, Is.EqualTo(5));
        }

        [Test]
        public void NoHits_IsGreen()
        {
            var report = PreflightDiagnostic.Evaluate(
                new Vector3[10], null, new List<PenetrationHit>(), Sphere(), Margin);
            Assert.That(report.Verdict, Is.EqualTo(PreflightVerdict.Green));
            Assert.That(report.RedCause, Is.EqualTo(RedCause.None));
        }

        [Test]
        public void DeepAndClustered_IsRedCollapsedShapeKey()
        {
            BuildGrid(5, 0.3f, out var positions, out var triangles);
            var hits = new List<PenetrationHit>();
            for (int v = 0; v < positions.Length; v++) hits.Add(Hit(v, 0.06f)); // deep, one patch

            var report = PreflightDiagnostic.Evaluate(positions, triangles, hits, Sphere(), Margin);

            Assert.That(report.Verdict, Is.EqualTo(PreflightVerdict.Red));
            Assert.That(report.RedCause, Is.EqualTo(RedCause.CollapsedShapeKey));
            Assert.That(report.LargestPatchRatio, Is.EqualTo(1f).Within(1e-6f));
        }

        [Test]
        public void ModerateDepthClustered_IsRedRetargetingClass()
        {
            BuildGrid(5, 0.3f, out var positions, out var triangles);
            var hits = new List<PenetrationHit>();
            // Past RedDepth (0.03) but below CollapseDepth (0.05): a genuine
            // size-class difference, not a collapsed shape key.
            for (int v = 0; v < positions.Length; v++) hits.Add(Hit(v, 0.04f));

            var report = PreflightDiagnostic.Evaluate(positions, triangles, hits, Sphere(), Margin);

            Assert.That(report.Verdict, Is.EqualTo(PreflightVerdict.Red));
            Assert.That(report.RedCause, Is.EqualTo(RedCause.RetargetingClassDifference));
        }

        [Test]
        public void HighRatioDispersedShallow_IsRedThickGarmentInnerWall()
        {
            BuildDisjointTriangles(10, out var positions, out var triangles); // 30 verts, 10 patches
            var hits = new List<PenetrationHit>();
            for (int t = 0; t < 10; t++) hits.Add(Hit(t * 3, 0.02f)); // one per triangle, shallow

            var report = PreflightDiagnostic.Evaluate(positions, triangles, hits, Sphere(), Margin);

            Assert.That(report.PenetratingRatio, Is.EqualTo(10f / 30f).Within(1e-6f));
            Assert.That(report.Verdict, Is.EqualTo(PreflightVerdict.Red));
            Assert.That(report.RedCause, Is.EqualTo(RedCause.ThickGarmentInnerWall));
        }
    }
}
