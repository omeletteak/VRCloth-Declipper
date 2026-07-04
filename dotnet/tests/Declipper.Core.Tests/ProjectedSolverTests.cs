using System.Collections.Generic;
using System.Numerics;
using Declipper.Core.Sdf;
using Declipper.Core.Solver;
using NUnit.Framework;

namespace Declipper.Core.Tests
{
    /// <summary>
    /// Functional tests for the ported projected solver. The core invariant —
    /// "always ends on or above the margin surface" — is analytic (no v1 golden
    /// dump needed): after solving, no vertex may sit below the margin. Inputs
    /// are synthetic (a procedural grid vs. an analytic sphere SDF), so nothing
    /// here touches purchased asset geometry.
    /// </summary>
    public class ProjectedSolverTests
    {
        // n×n grid on the z=0 plane spanning [-half, half]^2, row-major.
        static void BuildGrid(int n, float half, out Vector3[] positions, out int[] triangles)
        {
            positions = new Vector3[n * n];
            for (int r = 0; r < n; r++)
            {
                for (int c = 0; c < n; c++)
                {
                    float x = -half + 2f * half * c / (n - 1);
                    float y = -half + 2f * half * r / (n - 1);
                    positions[r * n + c] = new Vector3(x, y, 0f);
                }
            }

            var tris = new List<int>();
            for (int r = 0; r < n - 1; r++)
            {
                for (int c = 0; c < n - 1; c++)
                {
                    int a = r * n + c, b = r * n + c + 1;
                    int cc = (r + 1) * n + c, d = (r + 1) * n + c + 1;
                    tris.Add(a); tris.Add(cc); tris.Add(b);
                    tris.Add(b); tris.Add(cc); tris.Add(d);
                }
            }
            triangles = tris.ToArray();
        }

        [Test]
        public void Solve_PushesEveryVertexToOrAboveMarginSurface()
        {
            // 5×5 grid inside a radius-0.5 sphere: every vertex (max radius
            // ~0.42 < 0.5) starts penetrating.
            BuildGrid(5, 0.3f, out var positions, out var triangles);
            var body = new CapsuleSetSdf(new[] { new Capsule(Vector3.Zero, Vector3.Zero, 0.5f) });
            var options = new SolverOptions(margin: 0.02f, lambda: 0.5f, iterations: 8, rings: 2);

            SolveResult result = ProjectedSolver.Solve(positions, triangles, body, options);

            Assert.That(result.InitialHitCount, Is.EqualTo(25), "all grid vertices start inside");
            Assert.That(result.FinalHitCount, Is.EqualTo(0), "invariant: ends on/above margin");

            // Independently re-verify every vertex clears the margin surface.
            const float margin = 0.02f;
            for (int v = 0; v < positions.Length; v++)
            {
                float d = body.Sample(positions[v]).Distance;
                Assert.That(d, Is.GreaterThanOrEqualTo(margin - 1e-4f),
                    $"vertex {v} still penetrating: d={d}");
            }
        }

        [Test]
        public void Solve_LeavesNonPenetratingClothUntouched()
        {
            // Grid parked well outside the sphere: no hits, no motion.
            BuildGrid(3, 0.2f, out var positions, out var triangles);
            for (int v = 0; v < positions.Length; v++)
            {
                positions[v] += new Vector3(0f, 0f, 2f);
            }
            var before = (Vector3[])positions.Clone();
            var body = new CapsuleSetSdf(new[] { new Capsule(Vector3.Zero, Vector3.Zero, 0.5f) });

            SolveResult result = ProjectedSolver.Solve(
                positions, triangles, body, new SolverOptions(margin: 0.02f));

            Assert.That(result.InitialHitCount, Is.EqualTo(0));
            Assert.That(result.FinalHitCount, Is.EqualTo(0));
            for (int v = 0; v < positions.Length; v++)
            {
                Assert.That(positions[v], Is.EqualTo(before[v]));
            }
        }
    }
}
