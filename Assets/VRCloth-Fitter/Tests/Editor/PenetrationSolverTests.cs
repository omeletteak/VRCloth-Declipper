using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace VRClothFitter.Tests
{
    public class PenetrationSolverTests
    {
        const float Margin = 0.005f;

        // Capsule along the Y axis from origin to (0,1,0), radius 0.25.
        static List<BodyCapsule> SingleCapsule()
        {
            return new List<BodyCapsule> { new BodyCapsule(Vector3.zero, new Vector3(0f, 1f, 0f), 0.25f) };
        }

        // A flat cloth sheet in the XZ plane at y=0.5, centered on the
        // capsule axis, so its middle vertices penetrate the capsule.
        static void MakeSheet(int n, float spacing, out Vector3[] positions, out int[] triangles)
        {
            positions = new Vector3[n * n];
            float half = (n - 1) * spacing * 0.5f;
            for (int row = 0; row < n; row++)
            {
                for (int col = 0; col < n; col++)
                {
                    positions[row * n + col] = new Vector3(col * spacing - half, 0.5f, row * spacing - half);
                }
            }

            var tris = new List<int>();
            for (int row = 0; row < n - 1; row++)
            {
                for (int col = 0; col < n - 1; col++)
                {
                    int v = row * n + col;
                    tris.AddRange(new[] { v, v + 1, v + n });
                    tris.AddRange(new[] { v + n, v + 1, v + n + 1 });
                }
            }
            triangles = tris.ToArray();
        }

        [Test]
        public void Solve_ResolvesAllPenetration()
        {
            MakeSheet(21, 0.05f, out var positions, out var triangles);
            var capsules = SingleCapsule();

            var result = PenetrationSolver.Solve(positions, triangles, capsules, Margin);

            Assert.Greater(result.initialHitCount, 0);
            Assert.AreEqual(0, result.finalHitCount);
            Assert.AreEqual(0, PenetrationDetection.Scan(positions, capsules, Margin - 1e-4f).Count);
        }

        [Test]
        public void Solve_LeavesFarVerticesUntouched()
        {
            MakeSheet(21, 0.05f, out var positions, out var triangles);
            Vector3 corner = positions[0];

            PenetrationSolver.Solve(positions, triangles, SingleCapsule(), Margin);

            Assert.AreEqual(corner, positions[0]);
        }

        [Test]
        public void Solve_ProducesSmootherResultThanPushOutAlone()
        {
            MakeSheet(21, 0.05f, out var pushed, out var triangles);
            MakeSheet(21, 0.05f, out var solved, out _);
            var capsules = SingleCapsule();

            var hits = PenetrationDetection.Scan(pushed, capsules, Margin);
            PenetrationPushOut.Apply(pushed, hits, capsules, Margin);
            PenetrationSolver.Solve(solved, triangles, capsules, Margin);

            float pushedMaxEdge = MaxEdgeLength(pushed, triangles);
            float solvedMaxEdge = MaxEdgeLength(solved, triangles);
            Assert.Less(solvedMaxEdge, pushedMaxEdge,
                $"solver should shorten the longest stretched edge (push-out only: {pushedMaxEdge}, solver: {solvedMaxEdge})");
        }

        [Test]
        public void Solve_WithoutPenetration_ReportsZeroAndChangesNothing()
        {
            MakeSheet(5, 0.05f, out var positions, out var triangles);
            for (int i = 0; i < positions.Length; i++)
            {
                positions[i] += new Vector3(0f, 2f, 0f); // far above the capsule
            }
            var before = (Vector3[])positions.Clone();

            var result = PenetrationSolver.Solve(positions, triangles, SingleCapsule(), Margin);

            Assert.AreEqual(0, result.initialHitCount);
            Assert.AreEqual(0, result.passes);
            CollectionAssert.AreEqual(before, positions);
        }

        static float MaxEdgeLength(Vector3[] positions, int[] triangles)
        {
            float max = 0f;
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                max = Mathf.Max(max, Vector3.Distance(positions[triangles[t]], positions[triangles[t + 1]]));
                max = Mathf.Max(max, Vector3.Distance(positions[triangles[t + 1]], positions[triangles[t + 2]]));
                max = Mathf.Max(max, Vector3.Distance(positions[triangles[t + 2]], positions[triangles[t]]));
            }
            return max;
        }
    }
}
