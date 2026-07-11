using System.Collections.Generic;
using UnityEngine;
using V2Diag = Declipper.Core.Diagnostics;
using V2Sdf = Declipper.Core.Sdf;
using V2Solver = Declipper.Core.Solver;
using NumericsV3 = System.Numerics.Vector3;

namespace VRClothDeclipper
{
    /// <summary>
    /// S2 bridge (docs/REARCHITECTURE.md §4): routes the pipeline's geometry
    /// seams — detection, preflight and the projected solve — through the v2
    /// pure-.NET core (Declipper.Core), converting UnityEngine types only at
    /// this boundary. The v2 SDF is the single body representation; the legacy
    /// coarse solver sees the same SDF through <see cref="SdfBodyCollider"/>.
    /// v1 Core stays in place for the coarse solver's math, tests and the
    /// golden gate until S3. v1↔v2 equivalence of everything bridged here is
    /// pinned by the golden fixtures (dotnet/tests/fixtures).
    /// </summary>
    public static class VRClothV2Bridge
    {
        public static NumericsV3 ToNumerics(Vector3 v) => new NumericsV3(v.x, v.y, v.z);

        public static Vector3 ToUnity(NumericsV3 v) => new Vector3(v.X, v.Y, v.Z);

        public static NumericsV3[] ToNumerics(IReadOnlyList<Vector3> source)
        {
            var result = new NumericsV3[source?.Count ?? 0];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = ToNumerics(source[i]);
            }
            return result;
        }

        /// <summary>The bone-capsule proxy as the v2 SDF (min-union of capsules).</summary>
        public static V2Sdf.ISignedDistanceField BuildCapsuleSdf(IReadOnlyList<BodyCapsule> capsules)
        {
            var v2 = new List<V2Sdf.Capsule>(capsules?.Count ?? 0);
            if (capsules != null)
            {
                foreach (var c in capsules)
                {
                    v2.Add(new V2Sdf.Capsule(ToNumerics(c.start), ToNumerics(c.end), c.radius));
                }
            }
            return new V2Sdf.CapsuleSetSdf(v2);
        }

        /// <summary>
        /// The avatar's body mesh as the v2 SDF, gathered by the same
        /// resolution as the v1 collider (<see cref="VRClothBodySdfBuilder"/>).
        /// Null (after logging why) when no usable body mesh exists — callers
        /// fall back to capsules, exactly like the v1 path did.
        /// </summary>
        public static V2Sdf.ISignedDistanceField BuildBodySdf(VRClothDeclipper fitter)
        {
            if (!VRClothBodySdfBuilder.TryCollectBodyMesh(fitter, out var vertices, out var triangles, out var used))
            {
                return null;
            }

            var sdf = new V2Sdf.MeshSdf(ToNumerics(vertices), triangles);
            if (!sdf.IsValid)
            {
                Debug.LogWarning($"[VRClothDeclipper] Mesh-SDF collider: body mesh '{string.Join(", ", used)}' has no triangles — falling back to capsules.");
                return null;
            }

            Debug.Log($"[VRClothDeclipper] Mesh-SDF collider built from {used.Count} mesh(es) ({vertices.Length} verts, {triangles.Length / 3} tris): {string.Join(", ", used)}.");
            return sdf;
        }

        /// <summary>
        /// v2 detection with the v1 pipeline semantics preserved: fills
        /// <see cref="ClothSnapshot.hits"/> as v1 hits (world position read
        /// back, closest-capsule attribution when <paramref name="capsulesForAttribution"/>
        /// is given, collapsed-vertex exclusion) and returns them flattened.
        /// Pass null capsules for the mesh backend — its hits carry no capsule
        /// index (v1 parity).
        /// </summary>
        public static List<PenetrationHit> Detect(
            IReadOnlyList<ClothSnapshot> cloth, V2Sdf.ISignedDistanceField body,
            IReadOnlyList<BodyCapsule> capsulesForAttribution, float margin)
        {
            var allHits = new List<PenetrationHit>();
            if (cloth == null)
            {
                return allHits;
            }

            foreach (var snapshot in cloth)
            {
                var positions = ToNumerics(snapshot.worldVertices);
                var v2Hits = V2Solver.PenetrationDetection.Scan(positions, body, margin);
                var hits = new List<PenetrationHit>(v2Hits.Count);
                foreach (var hit in v2Hits)
                {
                    Vector3 position = snapshot.worldVertices[hit.VertexIndex];
                    hits.Add(new PenetrationHit(
                        hit.VertexIndex, position, hit.Depth,
                        ClosestCapsule(capsulesForAttribution, position)));
                }

                // Same collapsed-vertex exclusion as VRClothPenetrationDetector:
                // a shrink/hide blendshape's folded vertices are not a visible
                // surface (ROADMAP phase 3).
                var collapsed = CollapsedVertices.Find(snapshot.worldVertices, snapshot.triangles);
                if (collapsed.Count > 0)
                {
                    hits.RemoveAll(h => collapsed.Contains(h.vertexIndex));
                }

                snapshot.hits = hits;
                allHits.AddRange(hits);
            }
            return allHits;
        }

        /// <summary>
        /// Closest-capsule attribution for the run log — the diagnostic
        /// metadata the v2 core deliberately dropped from its contract
        /// (docs/REARCHITECTURE.md §2 柱2). Same min-distance choice as the v1
        /// capsule scan. −1 without capsules (mesh backend, v1 parity).
        /// </summary>
        static int ClosestCapsule(IReadOnlyList<BodyCapsule> capsules, Vector3 position)
        {
            if (capsules == null || capsules.Count == 0)
            {
                return -1;
            }
            int closest = 0;
            float minDistance = float.MaxValue;
            for (int i = 0; i < capsules.Count; i++)
            {
                float distance = capsules[i].SignedDistance(position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closest = i;
                }
            }
            return closest;
        }

        /// <summary>
        /// v2 preflight over a snapshot's detected hits, returned as the v1
        /// report (thresholds and verdict/cause semantics are the same — the
        /// golden gate pins them).
        /// </summary>
        public static PreflightReport Evaluate(ClothSnapshot snapshot, V2Sdf.ISignedDistanceField body, float margin)
        {
            var positions = ToNumerics(snapshot.worldVertices);
            var v1Hits = snapshot.hits ?? new List<PenetrationHit>();
            var v2Hits = new List<V2Solver.PenetrationHit>(v1Hits.Count);
            foreach (var hit in v1Hits)
            {
                v2Hits.Add(new V2Solver.PenetrationHit(hit.vertexIndex, hit.depth));
            }

            var report = V2Diag.PreflightDiagnostic.Evaluate(positions, snapshot.triangles, v2Hits, body, margin);
            return new PreflightReport
            {
                vertexCount = report.VertexCount,
                hitCount = v1Hits.Count,
                penetratingCount = report.PenetratingCount,
                penetratingRatio = report.PenetratingRatio,
                maxDepth = report.MaxDepth,
                p95Depth = report.P95Depth,
                maxDepthOverRadius = report.MaxDepthOverThickness,
                largestPatchRatio = report.LargestPatchRatio,
                verdict = ToV1(report.Verdict),
                redCause = ToV1(report.RedCause),
            };
        }

        static PreflightVerdict ToV1(V2Diag.PreflightVerdict verdict) => verdict switch
        {
            V2Diag.PreflightVerdict.Green => PreflightVerdict.Green,
            V2Diag.PreflightVerdict.Yellow => PreflightVerdict.Yellow,
            _ => PreflightVerdict.Red,
        };

        static RedCause ToV1(V2Diag.RedCause cause) => cause switch
        {
            V2Diag.RedCause.None => RedCause.None,
            V2Diag.RedCause.RetargetingClassDifference => RedCause.RetargetingClassDifference,
            V2Diag.RedCause.CollapsedShapeKey => RedCause.CollapsedShapeKey,
            _ => RedCause.ThickGarmentInnerWall,
        };

        /// <summary>
        /// v2 projected solve, mutating <paramref name="worldVertices"/> in
        /// place (the v1 contract the mesh applier relies on).
        /// </summary>
        public static PenetrationSolver.Result SolveProjected(
            Vector3[] worldVertices, int[] triangles, V2Sdf.ISignedDistanceField body, float margin)
        {
            var positions = ToNumerics(worldVertices);
            var result = V2Solver.ProjectedSolver.Solve(
                positions, triangles, body, new V2Solver.SolverOptions(margin));
            for (int i = 0; i < worldVertices.Length; i++)
            {
                worldVertices[i] = ToUnity(positions[i]);
            }
            return new PenetrationSolver.Result
            {
                initialHitCount = result.InitialHitCount,
                passes = result.Iterations,
                finalHitCount = result.FinalHitCount,
            };
        }

        /// <summary>
        /// The v2 SDF seen through the v1 <see cref="IBodyCollider"/> contract,
        /// so legacy IBodyCollider consumers (the coarse solver) run against
        /// the same geometry as the bridged seams — one body build per run,
        /// no double BVH.
        /// </summary>
        public sealed class SdfBodyCollider : IBodyCollider
        {
            readonly V2Sdf.ISignedDistanceField sdf;

            public SdfBodyCollider(V2Sdf.ISignedDistanceField sdf)
            {
                this.sdf = sdf;
            }

            public float SignedDistance(Vector3 point) => sdf.Sample(ToNumerics(point)).Distance;

            public Vector3 Gradient(Vector3 point) => ToUnity(sdf.Sample(ToNumerics(point)).Gradient);

            public float LocalThickness(Vector3 point) => sdf.LocalThickness(ToNumerics(point));
        }
    }
}
