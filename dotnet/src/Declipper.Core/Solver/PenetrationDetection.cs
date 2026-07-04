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
    /// Single detection path for every backend; the v1 capsule-specific Detect
    /// overload (which carried a closest-capsule index) does not carry over —
    /// representation-specific metadata is an SDF implementation concern, not
    /// part of the contract (docs/REARCHITECTURE.md §1 動機2).
    /// Ported from Assets/VRCloth-Declipper/Core/PenetrationDetection.cs.
    /// </summary>
    public static class PenetrationDetection
    {
        /// <summary>
        /// Every vertex with signed distance below <paramref name="margin"/>,
        /// in input order. Depth is the distance below the margin surface
        /// (<c>margin - signedDistance</c>, always positive for a hit).
        /// Embarrassingly parallel over vertices — the loop is kept flat so a
        /// parallel-for (and later Unity Jobs) drops in without reshaping.
        /// </summary>
        public static List<PenetrationHit> Scan(
            ReadOnlySpan<Vector3> positions, ISignedDistanceField body, float margin)
        {
            var hits = new List<PenetrationHit>();
            if (body == null)
            {
                return hits;
            }

            for (int v = 0; v < positions.Length; v++)
            {
                float distance = body.Sample(positions[v]).Distance;
                if (distance < margin)
                {
                    hits.Add(new PenetrationHit(v, margin - distance));
                }
            }
            return hits;
        }
    }
}
