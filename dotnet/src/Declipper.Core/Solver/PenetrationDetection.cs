using System;
using System.Collections.Generic;
using System.Numerics;
using Declipper.Core.Sdf;

namespace Declipper.Core.Solver
{
    /// <summary>A vertex found inside the margin-inflated body.</summary>
    public readonly struct PenetrationHit
    {
        public readonly int VertexIndex;

        /// <summary>Depth below the margin surface, in meters (positive).</summary>
        public readonly float Depth;

        public PenetrationHit(int vertexIndex, float depth)
        {
            VertexIndex = vertexIndex;
            Depth = depth;
        }
    }

    /// <summary>
    /// STUB — S1 port target (trivial). Single detection path for every
    /// backend; the v1 capsule-specific Detect overload does not carry over
    /// (docs/REARCHITECTURE.md §1 動機2).
    /// Port from Assets/VRCloth-Declipper/Core/PenetrationDetection.cs.
    /// </summary>
    public static class PenetrationDetection
    {
        /// <summary>
        /// Every vertex with signed distance below <paramref name="margin"/>.
        /// Embarrassingly parallel over vertices — keep the loop flat so a
        /// parallel-for (and later Unity Jobs) drops in without reshaping.
        /// </summary>
        public static List<PenetrationHit> Scan(
            ReadOnlySpan<Vector3> positions, ISignedDistanceField body, float margin)
        {
            throw new NotImplementedException("S1: port PenetrationDetection.Scan.");
        }
    }
}
