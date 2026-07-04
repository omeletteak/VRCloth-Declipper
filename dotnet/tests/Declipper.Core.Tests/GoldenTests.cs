using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;
using Declipper.Core.Diagnostics;
using Declipper.Core.Sdf;
using Declipper.Core.Solver;
using NUnit.Framework;

namespace Declipper.Core.Tests
{
    /// <summary>
    /// The golden gate (docs/REARCHITECTURE.md §4 S1): every fixture in
    /// dotnet/tests/fixtures/ is a synthetic input plus the v1 (Unity) pipeline's
    /// detection / solve / preflight output, dumped by
    /// Assets/VRCloth-Declipper/Tests/Fixtures/GoldenFixtureDumper.cs. Here the
    /// v2 (.NET) core must reproduce that output. This is what licenses removing
    /// v1 in S2/S3.
    ///
    /// Tolerances: detection depth and preflight statistics are pure SDF math and
    /// compared tightly (1e-5). Solved positions run 8 smooth/re-project
    /// iterations; the smoothing lerp is matched to Unity's bit-for-bit, but
    /// HashSet neighbor-iteration order can still differ across runtimes and
    /// float addition is non-associative, so a 1e-3 (1 mm) band absorbs that
    /// while still catching any real porting divergence (which would be ≥ mm).
    /// </summary>
    public class GoldenTests
    {
        const float StatTolerance = 1e-5f;
        const float SolvedTolerance = 1e-3f;

        public static IEnumerable<string> Fixtures()
        {
            string? dir = FixturesDir();
            if (dir == null) yield break;
            foreach (var f in Directory.GetFiles(dir, "*.json"))
            {
                // mesh* fixtures have their own schemas + tests (MeshSdf_ReproducesV1,
                // MeshSolve_ReproducesV1).
                if (Path.GetFileName(f).StartsWith("mesh")) continue;
                yield return f;
            }
        }

        [Test]
        public void FixturesDirectoryExists()
        {
            Assert.That(FixturesDir(), Is.Not.Null,
                "dotnet/tests/fixtures not found — run the Unity GoldenFixtureDumper first.");
        }

        [TestCaseSource(nameof(Fixtures))]
        public void V2_ReproducesV1(string path)
        {
            GoldenCase g = Load(path);
            var capsules = ParseCapsules(g.capsules);
            var sdf = new CapsuleSetSdf(capsules);
            Vector3[] positions = ParseVerts(g.clothVertices);
            int[] triangles = g.clothTriangles;

            // --- detection ---
            List<PenetrationHit> hits = PenetrationDetection.Scan(positions, sdf, g.margin);
            Assert.That(hits.Count, Is.EqualTo(g.detHitIndices.Length), "detection hit count");
            for (int i = 0; i < hits.Count; i++)
            {
                Assert.That(hits[i].VertexIndex, Is.EqualTo(g.detHitIndices[i]), $"hit[{i}] index");
                Assert.That(hits[i].Depth, Is.EqualTo(g.detHitDepths[i]).Within(StatTolerance), $"hit[{i}] depth");
            }

            // --- preflight (on the original positions) ---
            PreflightReport report = PreflightDiagnostic.Evaluate(positions, triangles, hits, sdf, g.margin);
            Assert.That((int)report.Verdict, Is.EqualTo(g.pfVerdict), "verdict");
            Assert.That((int)report.RedCause, Is.EqualTo(g.pfRedCause), "red cause");
            Assert.That(report.VertexCount, Is.EqualTo(g.pfVertexCount), "vertex count");
            Assert.That(report.PenetratingCount, Is.EqualTo(g.pfPenetratingCount), "penetrating count");
            Assert.That(report.PenetratingRatio, Is.EqualTo(g.pfPenetratingRatio).Within(StatTolerance), "penetrating ratio");
            Assert.That(report.MaxDepth, Is.EqualTo(g.pfMaxDepth).Within(StatTolerance), "max depth");
            Assert.That(report.P95Depth, Is.EqualTo(g.pfP95Depth).Within(StatTolerance), "p95 depth");
            Assert.That(report.MaxDepthOverThickness, Is.EqualTo(g.pfMaxDepthOverThickness).Within(StatTolerance), "max depth/thickness");
            Assert.That(report.LargestPatchRatio, Is.EqualTo(g.pfLargestPatchRatio).Within(StatTolerance), "largest patch ratio");

            // --- solve ---
            var solved = (Vector3[])positions.Clone();
            SolveResult res = ProjectedSolver.Solve(
                solved, triangles, sdf, new SolverOptions(g.margin, g.lambda, g.iterations, g.rings));
            Assert.That(res.InitialHitCount, Is.EqualTo(g.initialHitCount), "initial hit count");
            Assert.That(res.FinalHitCount, Is.EqualTo(g.finalHitCount), "final hit count");

            Vector3[] expected = ParseVerts(g.solvedVertices);
            Assert.That(solved.Length, Is.EqualTo(expected.Length), "solved vertex count");
            float maxDelta = 0f;
            for (int i = 0; i < solved.Length; i++)
            {
                maxDelta = MathF.Max(maxDelta, (solved[i] - expected[i]).Length());
            }
            Assert.That(maxDelta, Is.LessThan(SolvedTolerance), $"max solved-vertex deviation {maxDelta}");
        }

        [Test]
        public void MeshSdf_ReproducesV1()
        {
            string? dir = FixturesDir();
            Assert.That(dir, Is.Not.Null);
            string path = Path.Combine(dir!, "meshsdf_sphere.json");
            Assert.That(File.Exists(path), $"missing {path}");

            MeshSdfCase g = JsonSerializer.Deserialize<MeshSdfCase>(File.ReadAllText(path), JsonOpts)!;
            var sdf = new MeshSdf(ParseVerts(g.bodyVertices), g.bodyTriangles);
            Vector3[] probes = ParseVerts(g.probes);
            Vector3[] v1Gradients = ParseVerts(g.gradients);

            float maxDistDelta = 0f;
            float minGradDot = 1f;
            int gradChecked = 0, gradTight = 0;
            for (int i = 0; i < probes.Length; i++)
            {
                SdfSample s = sdf.Sample(probes[i]);
                maxDistDelta = MathF.Max(maxDistDelta, MathF.Abs(s.Distance - g.distances[i]));

                Assert.That(s.Gradient.Length(), Is.EqualTo(1f).Within(1e-4f), $"probe {i} unit gradient");
                // Compare gradient direction only where the sign is stable (well
                // clear of the surface). Near the surface both the sign (winding≈0.5)
                // and the outward direction (|p - surface|≈0) are ill-conditioned.
                if (MathF.Abs(g.distances[i]) > 0.03f)
                {
                    float dot = Vector3.Dot(s.Gradient, v1Gradients[i]);
                    minGradDot = MathF.Min(minGradDot, dot);
                    gradChecked++;
                    if (dot > 0.999f) gradTight++;
                }
            }
            // BVH distance is exact; the scalar field reproduces tightly.
            Assert.That(maxDistDelta, Is.LessThan(1e-3f), $"max |v2 - v1| mesh SDF distance = {maxDistDelta}");
            // The gradient direction, however, is only reproducible up to facet
            // conditioning: a mesh SDF's gradient is discontinuous across
            // facet-cell boundaries, and v1(Mono)/v2(.NET) break the near-tie
            // differently at float precision. So the vast majority agree tightly,
            // a few boundary probes deviate, but none ever flip sign.
            Assert.That(gradChecked, Is.GreaterThan(0));
            Assert.That(gradTight, Is.GreaterThanOrEqualTo((int)(0.9f * gradChecked)),
                $"only {gradTight}/{gradChecked} gradients matched tightly");
            Assert.That(minGradDot, Is.GreaterThan(0.5f), $"a gradient nearly flipped: min dot = {minGradDot}");
        }

        [Test]
        public void MeshSolve_ReproducesV1()
        {
            string? dir = FixturesDir();
            Assert.That(dir, Is.Not.Null);
            string path = Path.Combine(dir!, "meshsolve_shell_in_sphere.json");
            Assert.That(File.Exists(path), $"missing {path}");

            MeshSolveCase g = JsonSerializer.Deserialize<MeshSolveCase>(File.ReadAllText(path), JsonOpts)!;
            var sdf = new MeshSdf(ParseVerts(g.bodyVertices), g.bodyTriangles);
            Vector3[] positions = ParseVerts(g.clothVertices);
            int[] triangles = g.clothTriangles;

            // --- detection ---
            List<PenetrationHit> hits = PenetrationDetection.Scan(positions, sdf, g.margin);
            Assert.That(hits.Count, Is.EqualTo(g.detHitIndices.Length), "detection hit count");
            for (int i = 0; i < hits.Count; i++)
            {
                Assert.That(hits[i].VertexIndex, Is.EqualTo(g.detHitIndices[i]), $"hit[{i}] index");
                Assert.That(hits[i].Depth, Is.EqualTo(g.detHitDepths[i]).Within(1e-4f), $"hit[{i}] depth");
            }

            // --- preflight ---
            PreflightReport report = PreflightDiagnostic.Evaluate(positions, triangles, hits, sdf, g.margin);
            Assert.That((int)report.Verdict, Is.EqualTo(g.pfVerdict), "verdict");
            Assert.That((int)report.RedCause, Is.EqualTo(g.pfRedCause), "red cause");
            Assert.That(report.PenetratingCount, Is.EqualTo(g.pfPenetratingCount), "penetrating count");
            Assert.That(report.MaxDepth, Is.EqualTo(g.pfMaxDepth).Within(1e-4f), "max depth");

            // --- solve ---
            var solved = (Vector3[])positions.Clone();
            SolveResult res = ProjectedSolver.Solve(
                solved, triangles, sdf, new SolverOptions(g.margin, g.lambda, g.iterations, g.rings));
            Assert.That(res.InitialHitCount, Is.EqualTo(g.initialHitCount), "initial hit count");
            Assert.That(res.FinalHitCount, Is.EqualTo(g.finalHitCount), "final hit count");

            Vector3[] expected = ParseVerts(g.solvedVertices);
            float maxDelta = 0f;
            int worst = -1, over = 0;
            for (int i = 0; i < solved.Length; i++)
            {
                float d = (solved[i] - expected[i]).Length();
                if (d > 1e-3f) over++;
                if (d > maxDelta) { maxDelta = d; worst = i; }
            }
            TestContext.WriteLine($"[meshsolve] over-1e-3={over}/{solved.Length} worst=v{worst} " +
                $"delta={maxDelta} v1={expected[worst]} v2={solved[worst]}");
            Assert.That(maxDelta, Is.LessThan(SolvedTolerance), $"max solved-vertex deviation {maxDelta}");
        }

        // --- fixture loading ----------------------------------------------

        static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
        {
            IncludeFields = true,
            PropertyNameCaseInsensitive = true,
        };

        static GoldenCase Load(string path)
        {
            var g = JsonSerializer.Deserialize<GoldenCase>(File.ReadAllText(path), JsonOpts);
            Assert.That(g, Is.Not.Null, $"failed to parse {path}");
            return g!;
        }

        static Vector3[] ParseVerts(float[] flat)
        {
            var v = new Vector3[flat.Length / 3];
            for (int i = 0; i < v.Length; i++)
            {
                v[i] = new Vector3(flat[i * 3], flat[i * 3 + 1], flat[i * 3 + 2]);
            }
            return v;
        }

        static Capsule[] ParseCapsules(float[] flat)
        {
            var caps = new Capsule[flat.Length / 7];
            for (int i = 0; i < caps.Length; i++)
            {
                int o = i * 7;
                caps[i] = new Capsule(
                    new Vector3(flat[o], flat[o + 1], flat[o + 2]),
                    new Vector3(flat[o + 3], flat[o + 4], flat[o + 5]),
                    flat[o + 6]);
            }
            return caps;
        }

        static string? FixturesDir()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, "tests", "fixtures");
                if (Directory.Exists(candidate)) return candidate;
                candidate = Path.Combine(dir.FullName, "fixtures");
                if (Directory.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            return null;
        }

        // Mirrors GoldenFixtureDumper.GoldenCase (field names must match).
        // Fields are populated by System.Text.Json reflection, so the compiler
        // cannot see the assignment (CS0649).
#pragma warning disable CS0649
        class GoldenCase
        {
            public string name = "";
            public float margin;
            public float lambda;
            public int iterations;
            public int rings;
            public float[] clothVertices = Array.Empty<float>();
            public int[] clothTriangles = Array.Empty<int>();
            public float[] capsules = Array.Empty<float>();
            public int[] detHitIndices = Array.Empty<int>();
            public float[] detHitDepths = Array.Empty<float>();
            public float[] solvedVertices = Array.Empty<float>();
            public int initialHitCount;
            public int finalHitCount;
            public int pfVerdict;
            public int pfRedCause;
            public int pfVertexCount;
            public int pfPenetratingCount;
            public float pfPenetratingRatio;
            public float pfMaxDepth;
            public float pfP95Depth;
            public float pfMaxDepthOverThickness;
            public float pfLargestPatchRatio;
        }

        // Mirrors GoldenFixtureDumper.MeshSdfCase.
        class MeshSdfCase
        {
            public string name = "";
            public float[] bodyVertices = Array.Empty<float>();
            public int[] bodyTriangles = Array.Empty<int>();
            public float[] probes = Array.Empty<float>();
            public float[] distances = Array.Empty<float>();
            public float[] gradients = Array.Empty<float>();
        }

        // Mirrors GoldenFixtureDumper.MeshSolveCase.
        class MeshSolveCase
        {
            public float margin;
            public float lambda;
            public int iterations;
            public int rings;
            public float[] clothVertices = Array.Empty<float>();
            public int[] clothTriangles = Array.Empty<int>();
            public float[] bodyVertices = Array.Empty<float>();
            public int[] bodyTriangles = Array.Empty<int>();
            public int[] detHitIndices = Array.Empty<int>();
            public float[] detHitDepths = Array.Empty<float>();
            public float[] solvedVertices = Array.Empty<float>();
            public int initialHitCount;
            public int finalHitCount;
            public int pfVerdict;
            public int pfRedCause;
            public int pfVertexCount;
            public int pfPenetratingCount;
            public float pfPenetratingRatio;
            public float pfMaxDepth;
            public float pfP95Depth;
            public float pfMaxDepthOverThickness;
            public float pfLargestPatchRatio;
        }
#pragma warning restore CS0649
    }
}
