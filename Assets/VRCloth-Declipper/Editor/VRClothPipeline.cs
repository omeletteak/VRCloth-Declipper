using System.Collections.Generic;
using UnityEngine;

namespace VRClothDeclipper
{
    public static class VRClothPipeline
    {
        /// <summary>
        /// Everything the pipeline computes before solving: the captured cloth,
        /// the body proxy, the chosen collider backend, the detected hits and the
        /// per-renderer preflight reports. Shared by <see cref="Run"/> and the
        /// headless preflight CLI (<see cref="VRClothPreflightCli"/>) so both
        /// judge identically.
        /// </summary>
        public class PreflightResult
        {
            public List<ClothSnapshot> cloth;
            public List<BodyCapsule> capsules;
            public IBodyCollider collider;
            public string backend;
            public List<PenetrationHit> hits;
            public PreflightReport[] reports;
        }

        /// <summary>
        /// Capture → proxy → detect → preflight, with no solve and no write-back.
        /// Returns null (after logging the reason) when inputs are missing or
        /// nothing is capturable. Nothing is serialized — No Cache holds.
        /// </summary>
        public static PreflightResult CaptureAndPreflight(VRClothDeclipper fitter)
        {
            if (fitter == null || fitter.targetAvatar == null || fitter.clothToDeform == null)
            {
                Debug.LogError("VRClothDeclipper: Target Avatar or Cloth is not set.");
                return null;
            }

            string modeStr = fitter.mode.ToString();
            Debug.Log($"[VRClothDeclipper] Running in {modeStr} mode...");
            Debug.Log($"Target Avatar: {fitter.targetAvatar.name}, Cloth: {fitter.clothToDeform.name}");
            if (fitter.sourceAvatar != null)
            {
                Debug.Log($"Source Avatar: {fitter.sourceAvatar.name}");
            }

            GameObject clothRoot = fitter.clothRoot != null ? fitter.clothRoot : fitter.clothToDeform.gameObject;
            List<ClothSnapshot> cloth = VRClothMeshCapture.Capture(clothRoot);
            if (cloth.Count == 0)
            {
                Debug.LogError("VRClothDeclipper: No active SkinnedMeshRenderer found under the cloth root. Aborting.");
                return null;
            }

            int totalVertices = 0;
            foreach (var snapshot in cloth)
            {
                totalVertices += snapshot.VertexCount;
            }
            Debug.Log($"[VRClothDeclipper] Captured {cloth.Count} renderer(s), {totalVertices} vertices in world space.");

            var capsules = VRClothProxyGenerator.Generate(fitter.targetAvatar);
            if (capsules == null)
            {
                Debug.LogError("Failed to generate proxy capsules. Aborting.");
                return null;
            }
            if (fitter.estimateRadiiFromBody)
            {
                capsules = VRClothBodyRadiusEstimator.Apply(fitter, capsules).capsules;
            }
            // Pick the collision backend: the mesh-SDF collider when requested
            // and a body mesh is available, otherwise the bone capsules
            // (docs/DESIGN.md §6). Detection differs (the mesh has no capsule
            // index); preflight and the solver run through the IBodyCollider
            // abstraction either way.
            IBodyCollider collider;
            List<PenetrationHit> hits;
            string backend;
            MeshSdfCollider sdf = fitter.useMeshSdfCollider ? VRClothBodySdfBuilder.Build(fitter) : null;
            if (sdf != null)
            {
                collider = sdf;
                backend = "mesh";
                VRClothDebugVisualizer.SetCapsules(System.Array.Empty<BodyCapsule>());
                hits = VRClothPenetrationDetector.Detect(cloth, collider, fitter.margin);
            }
            else
            {
                if (fitter.useMeshSdfCollider)
                {
                    Debug.LogWarning("[VRClothDeclipper] Mesh-SDF collider unavailable — falling back to bone capsules for this run.");
                }
                collider = new CapsuleBodyCollider(capsules);
                backend = "capsule";
                VRClothDebugVisualizer.SetCapsules(capsules);
                hits = VRClothPenetrationDetector.Detect(cloth, capsules, fitter.margin);
            }
            VRClothDebugVisualizer.SetHits(hits);
            Debug.Log($"[VRClothDeclipper] Detected {hits.Count} penetrating vertices (margin {fitter.margin:F3} m, {backend} backend).");

            // Preflight: judge per renderer whether the body-shape difference
            // is within the supported envelope (docs/DESIGN.md §9).
            var reports = new PreflightReport[cloth.Count];
            for (int i = 0; i < cloth.Count; i++)
            {
                var snapshot = cloth[i];
                reports[i] = PreflightDiagnostic.Evaluate(
                    snapshot.worldVertices, snapshot.triangles, snapshot.hits, collider, fitter.margin);
                Debug.Log(FormatPreflight(snapshot.renderer.name, reports[i]));
            }

            return new PreflightResult
            {
                cloth = cloth,
                capsules = capsules,
                collider = collider,
                backend = backend,
                hits = hits,
                reports = reports,
            };
        }

        public static void Run(VRClothDeclipper fitter)
        {
            var pf = CaptureAndPreflight(fitter);
            if (pf == null)
            {
                return;
            }
            var cloth = pf.cloth;
            var hits = pf.hits;
            var reports = pf.reports;

            // Preflight RED is apply-specific: warn here, and (unless forced)
            // skip the renderer in the solve loop below (docs/DESIGN.md §9).
            for (int i = 0; i < cloth.Count; i++)
            {
                if (reports[i].verdict == PreflightVerdict.Red)
                {
                    string cause = DescribeRedCause(reports[i].redCause);
                    Debug.LogWarning(fitter.forceApplyOutOfRange
                        ? $"[VRClothDeclipper] {cloth[i].renderer.name}: RED ({cause}), but Force Apply (Out of Range) is enabled — applying anyway. Expect artifacts (docs/DESIGN.md §9)."
                        : $"[VRClothDeclipper] {cloth[i].renderer.name}: RED — {cause} Apply will be skipped (docs/DESIGN.md §9). Enable 'Force Apply (Out of Range)' to override.");
                }
            }

            var solve = new VRClothRunLog.SolveSummary();
            // Pick the solver: the prototype normal/tangent-split SolveProjected
            // when requested, otherwise the current coarse-pass Solve. Both run
            // through the IBodyCollider abstraction and return the same Result
            // (docs/DEFORMATION_METHODS.md §3.1).
            string solverName = fitter.useProjectedSolver ? "projected" : "coarse";
            if (hits.Count > 0)
            {
                for (int i = 0; i < cloth.Count; i++)
                {
                    if (reports[i].verdict == PreflightVerdict.Red && !fitter.forceApplyOutOfRange)
                    {
                        solve.skippedRenderers++;
                        continue;
                    }
                    var snapshot = cloth[i];
                    var result = fitter.useProjectedSolver
                        ? PenetrationSolver.SolveProjected(snapshot.worldVertices, snapshot.triangles, pf.collider, fitter.margin)
                        : PenetrationSolver.Solve(snapshot.worldVertices, snapshot.triangles, pf.collider, fitter.margin);
                    solve.passes = Mathf.Max(solve.passes, result.passes);
                    solve.remainingPenetrating += result.finalHitCount;
                    if (result.initialHitCount > 0)
                    {
                        VRClothMeshApplier.Apply(snapshot);
                        solve.appliedRenderers++;
                    }
                }
                Debug.Log($"[VRClothDeclipper] Push-out + smoothing finished after {solve.passes} pass(es) ({solverName} solver); {solve.remainingPenetrating} vertices still penetrating.");
                Debug.Log($"[VRClothDeclipper] Applied fitted mesh copies to {solve.appliedRenderers} renderer(s)"
                    + (solve.skippedRenderers > 0 ? $", skipped {solve.skippedRenderers} out-of-range renderer(s)" : "")
                    + ". Originals untouched; Undo (Ctrl+Z) restores.");
            }

            // The run log records which backend produced these hits so capsule
            // and mesh-SDF runs can be compared (docs/DESIGN.md §6). Capsule
            // geometry is still logged; per-capsule attribution is skipped for
            // mesh hits, which carry no capsule index.
            VRClothRunLog.Write(fitter, cloth, pf.capsules, hits, reports, solve, pf.backend);
            Debug.Log("[VRClothDeclipper] Process complete.");
        }

        static string FormatPreflight(string rendererName, PreflightReport report)
        {
            string verdict = report.verdict.ToString().ToUpperInvariant();
            if (report.redCause == RedCause.CollapsedShapeKey)
            {
                verdict += " (collapsed blendshape?)";
            }
            return $"[VRClothDeclipper] Preflight {rendererName}: {verdict} — "
                + $"penetrating {report.penetratingCount}/{report.vertexCount} verts ({report.penetratingRatio:P1}), "
                + $"max {report.maxDepth * 1000f:F1} mm below surface ({report.maxDepthOverRadius:P0} of capsule radius), "
                + $"p95 {report.p95Depth * 1000f:F1} mm, largest patch {report.largestPatchRatio:P1}, "
                + $"margin-zone hits {report.hitCount}.";
        }

        /// <summary>
        /// User-facing reason for a Red verdict. The collapsed-blendshape case
        /// is named explicitly because it is otherwise hard to discover — the
        /// folded cloth reads as intended design (ROADMAP phase 3).
        /// </summary>
        static string DescribeRedCause(RedCause cause)
        {
            switch (cause)
            {
                case RedCause.CollapsedShapeKey:
                    return "likely a shrink/hide blendshape folding cloth deep into the body, "
                        + "not a body-shape difference — check this mesh's blendshapes (neutralize the shrink/hide shape).";
                case RedCause.RetargetingClassDifference:
                default:
                    return "body-shape difference exceeds the supported range (retargeting-class).";
            }
        }
    }
}
