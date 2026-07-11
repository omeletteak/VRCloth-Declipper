using System.Collections.Generic;
using System.Numerics;
using Declipper.Core.Sdf;

namespace Declipper.Core.Solver
{
    public readonly struct SolverOptions
    {
        /// <summary>Clearance above the body surface, meters.</summary>
        public readonly float Margin;

        /// <summary>Laplacian blend factor per smoothing step.</summary>
        public readonly float Lambda;

        /// <summary>Smooth-and-reproject iterations.</summary>
        public readonly int Iterations;

        /// <summary>Rings the smoothing region grows around penetrating seeds.</summary>
        public readonly int Rings;

        public SolverOptions(float margin, float lambda = 0.5f, int iterations = 8, int rings = 2)
        {
            Margin = margin;
            Lambda = lambda;
            Iterations = iterations;
            Rings = rings;
        }
    }

    public readonly struct SolveResult
    {
        /// <summary>Penetrating vertices before the first iteration.</summary>
        public readonly int InitialHitCount;

        /// <summary>Iterations executed.</summary>
        public readonly int Iterations;

        /// <summary>
        /// Vertices still meaningfully penetrating at the end (measured with a
        /// small tolerance below the margin surface). Expected 0.
        /// </summary>
        public readonly int FinalHitCount;

        public SolveResult(int initialHitCount, int iterations, int finalHitCount)
        {
            InitialHitCount = initialHitCount;
            Iterations = iterations;
            FinalHitCount = finalHitCount;
        }
    }

    /// <summary>
    /// STUB — S1 port target. The one solver of v2: the constrained
    /// optimization "minimize the Laplacian energy of the displacement field
    /// subject to SDF(x) ≥ margin", solved by projected iteration
    /// (docs/REARCHITECTURE.md §2 柱3). The v1 coarse Solve (push → smooth →
    /// re-push with pass/λ tuning) does not carry over.
    ///
    /// Algorithm (functional port of v1 PenetrationSolver.SolveProjected,
    /// Assets/VRCloth-Declipper/Core/PenetrationSolver.cs):
    /// 1. Scan for penetrating vertices; push each out to the margin surface
    ///    along the SDF gradient, writing into a displacement field over the
    ///    untouched originals. Seed the smoothing region, grown by Rings.
    /// 2. Per iteration: one Laplacian step on the displacement field within
    ///    the region, compose originals + displacements, re-scan, and
    ///    immediately re-project every vertex smoothing sank back below the
    ///    margin. The projection is along the SDF gradient, so it restores the
    ///    normal component and preserves the tangential blend. New hits widen
    ///    the region.
    /// 3. Always end on a projection — the invariant is that the final state
    ///    sits on or above the margin surface.
    ///
    /// Positions serve as the in/out buffer (originals are cloned internally);
    /// nothing is persisted — No Cache holds. Keep all per-vertex loops flat
    /// arrays for later parallelization (Unity Burst/Jobs in S2+).
    /// </summary>
    public static class ProjectedSolver
    {
        /// <summary>Small tolerance below the margin surface for the final residual count.</summary>
        const float FinalResidualTolerance = 1e-4f;

        public static SolveResult Solve(
            Vector3[] positions, int[] triangles, ISignedDistanceField body, in SolverOptions options)
        {
            if (positions == null || positions.Length == 0 || body == null)
            {
                return new SolveResult(0, 0, 0);
            }

            float margin = options.Margin;
            var hits = PenetrationDetection.Scan(positions, body, margin);
            int initialHitCount = hits.Count;
            if (initialHitCount == 0)
            {
                return new SolveResult(0, 0, 0);
            }

            // `positions` becomes the scratch buffer for original + displacement;
            // the untouched clone is the reference the field is measured against.
            var originals = (Vector3[])positions.Clone();
            var displacements = new Vector3[originals.Length];
            var adjacency = VertexAdjacency.Build(originals, triangles);
            var seeds = new HashSet<int>();

            // Start the field on the margin surface, then grow the smoothing
            // region around the initial hits.
            ApplyPushOut(originals, displacements, hits, body, margin);
            AddSeeds(seeds, hits);
            var region = LaplacianSmoothing.ExpandRegion(adjacency, seeds, options.Rings);

            int passes = 0;
            for (int i = 0; i < options.Iterations; i++)
            {
                passes++;
                // One smoothing step blends the whole displacement — normal
                // height included, which it should not keep...
                LaplacianSmoothing.Smooth(displacements, adjacency, region, options.Lambda, 1);
                Compose(originals, displacements, positions);

                // ...so immediately project every vertex smoothing sank back to
                // the margin surface. The push is along the SDF gradient, so it
                // restores only the normal component and preserves the tangential
                // blend. New hits widen the region so re-penetration outside the
                // seed ring is caught.
                var reHits = PenetrationDetection.Scan(positions, body, margin);
                if (reHits.Count > 0)
                {
                    ApplyPushOut(originals, displacements, reHits, body, margin);
                    AddSeeds(seeds, reHits);
                    region = LaplacianSmoothing.ExpandRegion(adjacency, seeds, options.Rings);
                }
            }

            // Always end on a projection: the final state sits on or above the
            // margin surface (invariant).
            Compose(originals, displacements, positions);
            int finalHitCount =
                PenetrationDetection.Scan(positions, body, margin - FinalResidualTolerance).Count;
            return new SolveResult(initialHitCount, passes, finalHitCount);
        }

        /// <summary>
        /// Rewrites <paramref name="displacements"/> so that
        /// original + displacement sits <paramref name="margin"/> above the body
        /// surface, starting from each hit vertex's current displaced position
        /// (so the same hit list can drive a re-push after smoothing).
        /// </summary>
        static void ApplyPushOut(
            Vector3[] originals, Vector3[] displacements, List<PenetrationHit> hits,
            ISignedDistanceField body, float margin)
        {
            foreach (var hit in hits)
            {
                int v = hit.VertexIndex;
                Vector3 current = originals[v] + displacements[v];
                Vector3 target = body.PushOut(current, margin);
                displacements[v] = target - originals[v];
            }
        }

        static void AddSeeds(HashSet<int> seeds, List<PenetrationHit> hits)
        {
            foreach (var hit in hits)
            {
                seeds.Add(hit.VertexIndex);
            }
        }

        static void Compose(Vector3[] originals, Vector3[] displacements, Vector3[] target)
        {
            for (int v = 0; v < target.Length; v++)
            {
                target[v] = originals[v] + displacements[v];
            }
        }
    }
}
