using System.Collections.Generic;
using UnityEngine;

namespace VRClothFitter
{
    /// <summary>
    /// Brute-force penetration scan: every position against every capsule.
    /// Correctness first; spatial acceleration can replace the inner loop
    /// later without changing the contract.
    /// </summary>
    public static class PenetrationDetection
    {
        /// <summary>
        /// Returns one <see cref="PenetrationHit"/> for every position whose
        /// minimum signed distance to the capsules is below
        /// <paramref name="margin"/>. Each hit records the closest capsule
        /// and the depth below the margin surface (margin minus the signed
        /// distance), preserving the input position order.
        /// </summary>
        public static List<PenetrationHit> Scan(IReadOnlyList<Vector3> positions, IReadOnlyList<BodyCapsule> capsules, float margin)
        {
            var hits = new List<PenetrationHit>();
            if (positions == null || capsules == null || capsules.Count == 0)
            {
                return hits;
            }

            for (int v = 0; v < positions.Count; v++)
            {
                float minDistance = float.MaxValue;
                int closestCapsule = -1;
                for (int c = 0; c < capsules.Count; c++)
                {
                    float distance = capsules[c].SignedDistance(positions[v]);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestCapsule = c;
                    }
                }

                if (minDistance < margin)
                {
                    hits.Add(new PenetrationHit(v, positions[v], margin - minDistance, closestCapsule));
                }
            }
            return hits;
        }
    }
}
