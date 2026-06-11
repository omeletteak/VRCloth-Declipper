using System.Collections.Generic;
using UnityEngine;

namespace VRClothFitter
{
    public static class VRClothPipeline
    {
        public static void Run(VRClothFitter fitter)
        {
            if (fitter == null || fitter.targetAvatar == null || fitter.clothToDeform == null)
            {
                Debug.LogError("VRClothFitter: Target Avatar or Cloth is not set.");
                return;
            }

            string modeStr = fitter.mode.ToString();
            Debug.Log($"[VRClothFitter] Running in {modeStr} mode...");
            Debug.Log($"Target Avatar: {fitter.targetAvatar.name}, Cloth: {fitter.clothToDeform.name}");
            if (fitter.sourceAvatar != null)
            {
                Debug.Log($"Source Avatar: {fitter.sourceAvatar.name}");
            }

            GameObject clothRoot = fitter.clothRoot != null ? fitter.clothRoot : fitter.clothToDeform.gameObject;
            List<ClothSnapshot> cloth = VRClothMeshCapture.Capture(clothRoot);
            if (cloth.Count == 0)
            {
                Debug.LogError("VRClothFitter: No active SkinnedMeshRenderer found under the cloth root. Aborting.");
                return;
            }

            int totalVertices = 0;
            foreach (var snapshot in cloth)
            {
                totalVertices += snapshot.VertexCount;
            }
            Debug.Log($"[VRClothFitter] Captured {cloth.Count} renderer(s), {totalVertices} vertices in world space.");

            var capsules = VRClothProxyGenerator.Generate(fitter.targetAvatar);
            if (capsules == null)
            {
                Debug.LogError("Failed to generate proxy capsules. Aborting.");
                return;
            }
            VRClothDebugVisualizer.SetCapsules(capsules);

            List<PenetrationHit> hits = VRClothPenetrationDetector.Detect(cloth, capsules, fitter.margin);
            VRClothDebugVisualizer.SetHits(hits);
            Debug.Log($"[VRClothFitter] Detected {hits.Count} penetrating vertices (margin {fitter.margin:F3} m).");

            if (hits.Count > 0)
            {
                foreach (var snapshot in cloth)
                {
                    if (snapshot.hits != null && snapshot.hits.Count > 0)
                    {
                        PenetrationPushOut.Apply(snapshot.worldVertices, snapshot.hits, capsules, fitter.margin);
                    }
                }

                // Self-check with a small tolerance below the exact margin
                // surface, where pushed vertices now sit.
                int remaining = 0;
                foreach (var snapshot in cloth)
                {
                    remaining += PenetrationDetection.Scan(snapshot.worldVertices, capsules, fitter.margin - 1e-4f).Count;
                }
                Debug.Log($"[VRClothFitter] Pushed out {hits.Count} vertices; {remaining} still penetrating.");
            }

            if (fitter.mode == VRClothFitter.QualityMode.Light)
            {
                VRClothLaplacian.Smooth();
            }

            Debug.Log("[VRClothFitter] Process complete.");
        }
    }
}
