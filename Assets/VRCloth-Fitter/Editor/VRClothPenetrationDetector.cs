using System.Collections.Generic;

namespace VRClothFitter
{
    public static class VRClothPenetrationDetector
    {
        /// <summary>
        /// Scans every captured cloth renderer against the proxy capsules.
        /// Fills <see cref="ClothSnapshot.hits"/> per renderer and returns all
        /// hits flattened, for logging and the scene-view heatmap. Vertex
        /// indices in the flattened list are local to their snapshot.
        /// </summary>
        public static List<PenetrationHit> Detect(IReadOnlyList<ClothSnapshot> cloth, IReadOnlyList<BodyCapsule> capsules, float margin)
        {
            var allHits = new List<PenetrationHit>();
            if (cloth == null)
            {
                return allHits;
            }

            foreach (var snapshot in cloth)
            {
                snapshot.hits = PenetrationDetection.Scan(snapshot.worldVertices, capsules, margin);
                allHits.AddRange(snapshot.hits);
            }
            return allHits;
        }
    }
}
