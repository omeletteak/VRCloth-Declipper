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
                int passes = 0;
                int remaining = 0;
                foreach (var snapshot in cloth)
                {
                    var result = PenetrationSolver.Solve(snapshot.worldVertices, snapshot.triangles, capsules, fitter.margin);
                    passes = Mathf.Max(passes, result.passes);
                    remaining += result.finalHitCount;
                }
                Debug.Log($"[VRClothFitter] Push-out + smoothing finished after {passes} pass(es); {remaining} vertices still penetrating.");
            }

            Debug.Log("[VRClothFitter] Process complete.");
        }
    }
}
