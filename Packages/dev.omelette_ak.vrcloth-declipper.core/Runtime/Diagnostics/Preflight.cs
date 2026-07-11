using System;
using System.Collections.Generic;
using System.Numerics;
using Declipper.Core.Sdf;
using Declipper.Core.Solver;

namespace Declipper.Core.Diagnostics
{
    public enum PreflightVerdict
    {
        Green,
        Yellow,
        Red,
    }

    /// <summary>
    /// Named cause of a Red verdict — diagnostic honesty is a v1 invariant
    /// that carries over unchanged (docs/DIAGNOSTIC_HONESTY.md,
    /// docs/REARCHITECTURE.md §3).
    /// </summary>
    public enum RedCause
    {
        /// <summary>
        /// Not a Red verdict. Zero value so a default report reads as "no cause"
        /// rather than a real cause (v1 parity — the report always carries a
        /// RedCause field and needs a not-red sentinel).
        /// </summary>
        None,

        /// <summary>Body-shape difference beyond the supported envelope.</summary>
        RetargetingClassDifference,

        /// <summary>A shrink/hide blendshape folding cloth deep into the body.</summary>
        CollapsedShapeKey,

        /// <summary>Thick/enclosing garment inner wall reading as penetration (false-positive class).</summary>
        ThickGarmentInnerWall,
    }

    /// <summary>
    /// Per-renderer verdict and the statistics behind it. Same shape as v1
    /// PreflightReport so thresholds calibrated on v1 E2E carry over.
    /// </summary>
    public readonly struct PreflightReport
    {
        public readonly PreflightVerdict Verdict;
        public readonly RedCause RedCause;
        public readonly int VertexCount;
        public readonly int PenetratingCount;
        public readonly float PenetratingRatio;
        public readonly float MaxDepth;
        public readonly float P95Depth;
        public readonly float MaxDepthOverThickness;
        public readonly float LargestPatchRatio;

        public PreflightReport(
            PreflightVerdict verdict, RedCause redCause, int vertexCount, int penetratingCount,
            float penetratingRatio, float maxDepth, float p95Depth, float maxDepthOverThickness,
            float largestPatchRatio)
        {
            Verdict = verdict;
            RedCause = redCause;
            VertexCount = vertexCount;
            PenetratingCount = penetratingCount;
            PenetratingRatio = penetratingRatio;
            MaxDepth = maxDepth;
            P95Depth = p95Depth;
            MaxDepthOverThickness = maxDepthOverThickness;
            LargestPatchRatio = largestPatchRatio;
        }
    }

    /// <summary>
    /// Judges, before anything is applied, whether the detected penetration is
    /// inside the body-shape-difference envelope this tool supports. Depths are
    /// measured below the actual body surface — not the margin surface — so a
    /// well-fitting outfit that merely grazes the margin zone stays green.
    /// Ported from Assets/VRCloth-Declipper/Core/PreflightDiagnostic.cs; the
    /// single SDF path replaces v1's capsule/collider overloads, and local
    /// thickness comes from <see cref="ISignedDistanceField.LocalThickness"/>
    /// at the hit vertex's position (v2 hits carry no position, so it is read
    /// back from the <c>positions</c> array).
    /// </summary>
    public static class PreflightDiagnostic
    {
        // Initial thresholds from the DESIGN.md §9 table; to be calibrated
        // against real avatars during E2E.
        public const float GreenMaxDepth = 0.01f;
        public const float GreenMaxDepthOverThickness = 0.15f;
        public const float GreenMaxPenetratingRatio = 0.10f;
        public const float RedDepth = 0.03f;
        public const float RedPenetratingRatio = 0.30f;

        // Red-cause signature thresholds (provisional; calibrate with the
        // verdict thresholds during E2E). A collapsed shrink/hide blendshape
        // shows up as pathological depth — far past any plausible body-shape
        // difference — concentrated in a single connected patch.
        public const float CollapseDepth = 0.05f;       // >> RedDepth
        public const float CollapseClusterShare = 0.5f; // patch holds ≥ half the penetrating verts

        public static PreflightReport Evaluate(
            Vector3[]? positions, int[]? triangles, List<PenetrationHit>? hits,
            ISignedDistanceField? body, float margin)
        {
            if (body == null || positions == null || positions.Length == 0 || hits == null)
            {
                return new PreflightReport(
                    PreflightVerdict.Green, RedCause.None, positions?.Length ?? 0,
                    0, 0f, 0f, 0f, 0f, 0f);
            }

            int vertexCount = positions.Length;
            var depths = new List<float>(hits.Count);
            var penetratingVertices = new HashSet<int>();
            float maxDepth = 0f;
            float maxDepthOverThickness = 0f;

            foreach (var hit in hits)
            {
                // hit.Depth is below the margin surface; subtract the margin to
                // measure below the actual body surface.
                float surfaceDepth = hit.Depth - margin;
                if (surfaceDepth <= 0f)
                {
                    continue; // margin-zone graze, not a body penetration
                }
                depths.Add(surfaceDepth);
                penetratingVertices.Add(hit.VertexIndex);
                maxDepth = MathF.Max(maxDepth, surfaceDepth);

                float thickness = body.LocalThickness(positions[hit.VertexIndex]);
                if (thickness > 1e-6f)
                {
                    maxDepthOverThickness = MathF.Max(maxDepthOverThickness, surfaceDepth / thickness);
                }
            }

            int penetratingCount = penetratingVertices.Count;
            float penetratingRatio = (float)penetratingCount / vertexCount;
            float p95Depth = Percentile95(depths);
            float largestPatchRatio = LargestPatchRatio(positions, triangles, penetratingVertices);

            PreflightVerdict verdict = Judge(maxDepth, maxDepthOverThickness, penetratingRatio);
            RedCause redCause = ClassifyRedCause(verdict, maxDepth, penetratingRatio, largestPatchRatio);

            return new PreflightReport(
                verdict, redCause, vertexCount, penetratingCount, penetratingRatio,
                maxDepth, p95Depth, maxDepthOverThickness, largestPatchRatio);
        }

        static PreflightVerdict Judge(float maxDepth, float maxDepthOverThickness, float penetratingRatio)
        {
            if (maxDepth > RedDepth || penetratingRatio > RedPenetratingRatio)
            {
                return PreflightVerdict.Red;
            }
            if (maxDepth <= GreenMaxDepth
                && maxDepthOverThickness <= GreenMaxDepthOverThickness
                && penetratingRatio <= GreenMaxPenetratingRatio)
            {
                return PreflightVerdict.Green;
            }
            return PreflightVerdict.Yellow;
        }

        /// <summary>
        /// Names the root cause of a Red from its numeric signature: deep AND
        /// concentrated in one patch → a collapsed shrink/hide blendshape;
        /// high ratio spread across many small patches at shallow depth → a
        /// thick/enclosing garment's inner wall (a §8 false positive, verify
        /// visually, do not retarget); otherwise → a genuine retargeting-class
        /// difference. The inner-wall case needs mesh connectivity, so it only
        /// fires when the largest-patch signal exists.
        /// </summary>
        static RedCause ClassifyRedCause(
            PreflightVerdict verdict, float maxDepth, float penetratingRatio, float largestPatchRatio)
        {
            if (verdict != PreflightVerdict.Red)
            {
                return RedCause.None;
            }
            bool clustered = penetratingRatio > 0f
                && largestPatchRatio >= penetratingRatio * CollapseClusterShare;
            if (maxDepth >= CollapseDepth && clustered)
            {
                return RedCause.CollapsedShapeKey;
            }
            bool dispersed = largestPatchRatio > 0f && !clustered;
            if (dispersed && maxDepth <= RedDepth)
            {
                return RedCause.ThickGarmentInnerWall;
            }
            return RedCause.RetargetingClassDifference;
        }

        static float Percentile95(List<float> depths)
        {
            if (depths.Count == 0)
            {
                return 0f;
            }
            depths.Sort();
            int index = (int)MathF.Ceiling(0.95f * depths.Count) - 1;
            return depths[Math.Clamp(index, 0, depths.Count - 1)];
        }

        /// <summary>
        /// Size of the largest edge-connected component among penetrating
        /// vertices, as a fraction of all vertices. Vertex count stands in for
        /// surface area — adequate for a coarse scope gate.
        /// </summary>
        static float LargestPatchRatio(Vector3[] positions, int[]? triangles, HashSet<int> penetratingVertices)
        {
            if (penetratingVertices.Count == 0 || triangles == null)
            {
                return 0f;
            }

            var adjacency = VertexAdjacency.Build(positions, triangles);
            var penetratingReps = new HashSet<int>();
            foreach (int vertex in penetratingVertices)
            {
                penetratingReps.Add(adjacency.RepresentativeOf(vertex));
            }

            int largest = 0;
            var visited = new HashSet<int>();
            var stack = new Stack<int>();
            foreach (int start in penetratingReps)
            {
                if (!visited.Add(start))
                {
                    continue;
                }
                int memberCount = 0;
                stack.Push(start);
                while (stack.Count > 0)
                {
                    int rep = stack.Pop();
                    memberCount += adjacency.MembersOf(rep).Count;
                    foreach (int neighbor in adjacency.NeighborsOf(rep))
                    {
                        if (penetratingReps.Contains(neighbor) && visited.Add(neighbor))
                        {
                            stack.Push(neighbor);
                        }
                    }
                }
                largest = Math.Max(largest, memberCount);
            }
            return (float)largest / positions.Length;
        }
    }
}
