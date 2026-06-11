using System.Collections.Generic;
using UnityEngine;

namespace VRClothFitter
{
    /// <summary>
    /// Push-out step: moves penetrating vertices onto their closest capsule's
    /// margin surface, radially away from the capsule axis. Minimal strategy
    /// by design — normal/projection hybrids are a later quality improvement.
    /// </summary>
    public static class PenetrationPushOut
    {
        /// <summary>
        /// Moves every hit vertex to sit <paramref name="margin"/> above its
        /// closest capsule's surface, updating <paramref name="positions"/>
        /// in place. Returns the per-vertex displacement that was applied,
        /// zero for untouched vertices. The push starts from the vertex's
        /// current position (not the position recorded in the hit), so the
        /// same hit list can drive a re-push after smoothing moved vertices.
        /// </summary>
        public static Vector3[] Apply(Vector3[] positions, IReadOnlyList<PenetrationHit> hits, IReadOnlyList<BodyCapsule> capsules, float margin)
        {
            var offsets = new Vector3[positions != null ? positions.Length : 0];
            if (positions == null || hits == null || capsules == null)
            {
                return offsets;
            }

            foreach (var hit in hits)
            {
                Vector3 current = positions[hit.vertexIndex];
                Vector3 target = capsules[hit.capsuleIndex].PushOut(current, margin);
                offsets[hit.vertexIndex] = target - current;
                positions[hit.vertexIndex] = target;
            }
            return offsets;
        }
    }
}
