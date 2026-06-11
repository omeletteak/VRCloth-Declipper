using System.Collections.Generic;
using UnityEngine;

namespace VRClothFitter
{
    public static class VRClothPenetrationDetector
    {
        /// <summary>
        /// Marks cloth vertices that sit inside any body capsule. Receives the
        /// captured world-space cloth geometry and the proxy capsules; returns
        /// one <see cref="PenetrationHit"/> per penetrating vertex.
        /// Not implemented yet — always reports zero hits.
        /// </summary>
        public static List<PenetrationHit> Detect(IReadOnlyList<ClothSnapshot> cloth, IReadOnlyList<BodyCapsule> capsules)
        {
            Debug.Log("[VRClothFitter] Penetration detection is not implemented yet; reporting 0 hits.");
            return new List<PenetrationHit>();
        }
    }
}
