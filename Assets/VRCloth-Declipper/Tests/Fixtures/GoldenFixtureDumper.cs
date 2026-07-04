using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VRClothDeclipper.GoldenFixtures
{
    /// <summary>
    /// Dev-only tool: runs the v1 (Unity) pipeline on synthetic inputs and dumps
    /// its detection / solve / preflight outputs to <c>dotnet/tests/fixtures/*.json</c>.
    /// These are the golden gate the v2 (.NET) port is validated against, and the
    /// gate that must pass before v1 is removed in S2/S3 (docs/REARCHITECTURE.md
    /// §4 S1).
    ///
    /// Inputs are 100% procedural (a cloth tube / sheet over capsules and
    /// spheres) — no purchased or avatar-derived geometry — so committing both
    /// input and output respects the No Cache principle and redistribution
    /// limits. Run headless:
    ///
    ///   Unity.exe -batchmode -quit -projectPath &lt;repo&gt;
    ///     -executeMethod VRClothDeclipper.GoldenFixtures.GoldenFixtureDumper.DumpAll
    /// </summary>
    public static class GoldenFixtureDumper
    {
        /// <summary>
        /// One fixture: synthetic input plus the v1 outputs the v2 port must
        /// reproduce. Flattened primitive arrays (xyz triples) keep the JSON
        /// portable across Unity's JsonUtility and .NET's System.Text.Json.
        /// </summary>
        [System.Serializable]
        public class GoldenCase
        {
            public string name;
            public float margin;
            public float lambda;
            public int iterations;
            public int rings;

            // --- input (synthetic) ---
            public float[] clothVertices;  // flattened xyz
            public int[] clothTriangles;
            public float[] capsules;       // 7 floats each: sx,sy,sz,ex,ey,ez,r

            // --- v1 detection (on the original positions) ---
            public int[] detHitIndices;
            public float[] detHitDepths;

            // --- v1 SolveProjected (on a copy) ---
            public float[] solvedVertices; // flattened xyz
            public int initialHitCount;
            public int finalHitCount;

            // --- v1 preflight (on the original positions) ---
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

        /// <summary>Entry point for <c>-executeMethod</c>.</summary>
        public static void DumpAll()
        {
            try
            {
                string dir = FixturesDir();
                Directory.CreateDirectory(dir);
                foreach (var c in BuildCases())
                {
                    string json = JsonUtility.ToJson(c, true);
                    File.WriteAllText(Path.Combine(dir, c.name + ".json"), json);
                    Debug.Log($"[GoldenFixtureDumper] wrote {c.name}.json " +
                              $"(verts={c.clothVertices.Length / 3}, hits={c.detHitIndices.Length}, " +
                              $"verdict={c.pfVerdict}, redCause={c.pfRedCause})");
                }
                Debug.Log($"[GoldenFixtureDumper] done -> {dir}");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GoldenFixtureDumper] failed: {e}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        static string FixturesDir()
        {
            // Application.dataPath = <repo>/Assets
            string repo = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(repo, "dotnet", "tests", "fixtures");
        }

        static IEnumerable<GoldenCase> BuildCases()
        {
            yield return TubeOverCapsule();
            yield return SheetOverSphere();
            yield return TwoSpheres();
        }

        // --- cases ---------------------------------------------------------

        static GoldenCase TubeOverCapsule()
        {
            BuildTube(0.18f, 1.0f, 8, 16, out var verts, out var tris);
            var caps = new List<BodyCapsule>
            {
                new BodyCapsule(new Vector3(0f, -0.5f, 0f), new Vector3(0f, 0.5f, 0f), 0.2f),
            };
            var c = new GoldenCase { name = "tube_over_capsule", margin = 0.01f, lambda = 0.5f, iterations = 8, rings = 2 };
            Run(c, verts, tris, caps);
            return c;
        }

        static GoldenCase SheetOverSphere()
        {
            BuildGrid(9, 0.4f, out var verts, out var tris); // plane y=0
            var caps = new List<BodyCapsule>
            {
                new BodyCapsule(Vector3.zero, Vector3.zero, 0.3f), // sphere at origin
            };
            var c = new GoldenCase { name = "sheet_over_sphere", margin = 0.01f, lambda = 0.5f, iterations = 8, rings = 2 };
            Run(c, verts, tris, caps);
            return c;
        }

        static GoldenCase TwoSpheres()
        {
            BuildGrid(9, 0.6f, out var verts, out var tris);
            // Radius 0.23 (not 0.25) so no grid vertex lands tangent to a sphere
            // surface — a vertex at dist≈0 makes the strict penetratingCount test
            // ill-conditioned (v1 Mono vs v2 .NET disagree by ~1e-8 on the sign).
            var caps = new List<BodyCapsule>
            {
                new BodyCapsule(new Vector3(-0.35f, 0f, 0f), new Vector3(-0.35f, 0f, 0f), 0.23f),
                new BodyCapsule(new Vector3(0.35f, 0f, 0f), new Vector3(0.35f, 0f, 0f), 0.23f),
            };
            var c = new GoldenCase { name = "two_spheres", margin = 0.01f, lambda = 0.5f, iterations = 8, rings = 2 };
            Run(c, verts, tris, caps);
            return c;
        }

        // --- run v1 --------------------------------------------------------

        static void Run(GoldenCase c, Vector3[] verts, int[] tris, List<BodyCapsule> caps)
        {
            c.clothVertices = Flatten(verts);
            c.clothTriangles = tris;
            c.capsules = FlattenCapsules(caps);

            var hits = PenetrationDetection.Scan(verts, caps, c.margin);
            c.detHitIndices = new int[hits.Count];
            c.detHitDepths = new float[hits.Count];
            for (int i = 0; i < hits.Count; i++)
            {
                c.detHitIndices[i] = hits[i].vertexIndex;
                c.detHitDepths[i] = hits[i].depth;
            }

            var report = PreflightDiagnostic.Evaluate(verts, tris, hits, caps, c.margin);
            c.pfVerdict = (int)report.verdict;
            c.pfRedCause = (int)report.redCause;
            c.pfVertexCount = report.vertexCount;
            c.pfPenetratingCount = report.penetratingCount;
            c.pfPenetratingRatio = report.penetratingRatio;
            c.pfMaxDepth = report.maxDepth;
            c.pfP95Depth = report.p95Depth;
            c.pfMaxDepthOverThickness = report.maxDepthOverRadius;
            c.pfLargestPatchRatio = report.largestPatchRatio;

            var solved = (Vector3[])verts.Clone();
            var res = PenetrationSolver.SolveProjected(solved, tris, caps, c.margin, c.lambda, c.iterations, c.rings);
            c.solvedVertices = Flatten(solved);
            c.initialHitCount = res.initialHitCount;
            c.finalHitCount = res.finalHitCount;
        }

        // --- procedural meshes ---------------------------------------------

        static void BuildTube(float r, float h, int hSeg, int rSeg, out Vector3[] verts, out int[] tris)
        {
            verts = new Vector3[(hSeg + 1) * rSeg];
            for (int i = 0; i <= hSeg; i++)
            {
                float y = -h / 2f + h * i / hSeg;
                for (int j = 0; j < rSeg; j++)
                {
                    float a = 2f * Mathf.PI * j / rSeg;
                    verts[i * rSeg + j] = new Vector3(r * Mathf.Cos(a), y, r * Mathf.Sin(a));
                }
            }
            var t = new List<int>();
            for (int i = 0; i < hSeg; i++)
            {
                for (int j = 0; j < rSeg; j++)
                {
                    int j1 = (j + 1) % rSeg;
                    int a = i * rSeg + j, b = i * rSeg + j1, cc = (i + 1) * rSeg + j, d = (i + 1) * rSeg + j1;
                    t.Add(a); t.Add(cc); t.Add(b);
                    t.Add(b); t.Add(cc); t.Add(d);
                }
            }
            tris = t.ToArray();
        }

        static void BuildGrid(int n, float half, out Vector3[] verts, out int[] tris)
        {
            verts = new Vector3[n * n];
            for (int r = 0; r < n; r++)
            {
                for (int c = 0; c < n; c++)
                {
                    verts[r * n + c] = new Vector3(-half + 2f * half * c / (n - 1), 0f, -half + 2f * half * r / (n - 1));
                }
            }
            var t = new List<int>();
            for (int r = 0; r < n - 1; r++)
            {
                for (int c = 0; c < n - 1; c++)
                {
                    int a = r * n + c, b = r * n + c + 1, cc = (r + 1) * n + c, d = (r + 1) * n + c + 1;
                    t.Add(a); t.Add(cc); t.Add(b);
                    t.Add(b); t.Add(cc); t.Add(d);
                }
            }
            tris = t.ToArray();
        }

        static float[] Flatten(Vector3[] v)
        {
            var a = new float[v.Length * 3];
            for (int i = 0; i < v.Length; i++)
            {
                a[i * 3] = v[i].x;
                a[i * 3 + 1] = v[i].y;
                a[i * 3 + 2] = v[i].z;
            }
            return a;
        }

        static float[] FlattenCapsules(List<BodyCapsule> caps)
        {
            var a = new float[caps.Count * 7];
            for (int i = 0; i < caps.Count; i++)
            {
                var c = caps[i];
                int o = i * 7;
                a[o] = c.start.x; a[o + 1] = c.start.y; a[o + 2] = c.start.z;
                a[o + 3] = c.end.x; a[o + 4] = c.end.y; a[o + 5] = c.end.z;
                a[o + 6] = c.radius;
            }
            return a;
        }
    }
}
