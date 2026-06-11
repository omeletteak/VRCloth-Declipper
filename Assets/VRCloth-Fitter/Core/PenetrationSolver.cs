using System.Collections.Generic;
using UnityEngine;

namespace VRClothFitter
{
    /// <summary>
    /// The fitting core: alternates push-out and region-restricted Laplacian
    /// smoothing until nothing penetrates, ending with a push-out so the
    /// final state is guaranteed to sit on or above the margin surface.
    /// Operates on one in-memory position array — nothing is persisted.
    /// </summary>
    public static class PenetrationSolver
    {
        public struct Result
        {
            /// <summary>Penetrating vertices before the first pass.</summary>
            public int initialHitCount;

            /// <summary>Push+smooth passes actually executed.</summary>
            public int passes;

            /// <summary>
            /// Vertices still meaningfully penetrating at the end (measured
            /// with a small tolerance below the margin surface). Expected 0.
            /// </summary>
            public int finalHitCount;
        }

        // MVP tuning, exposed as parameters only for tests. Defaults follow
        // the roadmap: 2-3 rings, a few iterations, push->smooth 2-3 times.
        public static Result Solve(
            Vector3[] positions,
            int[] triangles,
            IReadOnlyList<BodyCapsule> capsules,
            float margin,
            float lambda = 0.5f,
            int smoothingIterations = 2,
            int rings = 2,
            int maxPasses = 3)
        {
            var result = new Result();
            if (positions == null || positions.Length == 0 || capsules == null || capsules.Count == 0)
            {
                return result;
            }

            var hits = PenetrationDetection.Scan(positions, capsules, margin);
            result.initialHitCount = hits.Count;
            if (hits.Count == 0)
            {
                return result;
            }

            var adjacency = VertexAdjacency.Build(positions, triangles);
            var seeds = new HashSet<int>();

            while (hits.Count > 0 && result.passes < maxPasses)
            {
                result.passes++;
                PenetrationPushOut.Apply(positions, hits, capsules, margin);
                foreach (var hit in hits)
                {
                    seeds.Add(hit.vertexIndex);
                }

                var region = LaplacianSmoothing.ExpandRegion(adjacency, seeds, rings);
                LaplacianSmoothing.Smooth(positions, adjacency, region, lambda, smoothingIterations);

                hits = PenetrationDetection.Scan(positions, capsules, margin);
            }

            // Smoothing ran last inside the loop, so push whatever it sank
            // back below the surface; correctness wins over the last bit of
            // smoothness.
            if (hits.Count > 0)
            {
                PenetrationPushOut.Apply(positions, hits, capsules, margin);
            }

            result.finalHitCount = PenetrationDetection.Scan(positions, capsules, margin - 1e-4f).Count;
            return result;
        }
    }
}
