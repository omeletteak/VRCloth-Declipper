using System;
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
        public static SolveResult Solve(
            Vector3[] positions, int[] triangles, ISignedDistanceField body, in SolverOptions options)
        {
            throw new NotImplementedException("S1: port PenetrationSolver.SolveProjected.");
        }
    }
}
