using System.Collections.Generic;
using UnityEngine;

namespace VRClothDeclipper
{
    public enum PreflightVerdict
    {
        /// <summary>Within the designed envelope (docs/DESIGN.md §9).</summary>
        Green,

        /// <summary>Best effort: bulges, texture stretch or proxy shape may show.</summary>
        Yellow,

        /// <summary>
        /// Retargeting-class body difference, out of scope: the pipeline
        /// refuses to apply unless explicitly forced.
        /// </summary>
        Red,
    }

    /// <summary>
    /// Why a renderer came back <see cref="PreflightVerdict.Red"/>. RED has two
    /// very different root causes that need different user action, and the
    /// dangerous one (a shrink/hide blendshape folding cloth into the body) is
    /// hard to spot by eye — the collapsed part just looks like intended design
    /// (docs/DESIGN.md §9, ROADMAP phase 3). Naming the cause from the numeric
    /// signature lets the message point at the real fix.
    /// </summary>
    public enum RedCause
    {
        /// <summary>Not a Red verdict.</summary>
        None,

        /// <summary>
        /// Broad, size-class mismatch: penetration spread across the surface at
        /// plausible body-difference depths. Genuinely out of scope (a
        /// retargeting job), as the legacy message says.
        /// </summary>
        RetargetingClassDifference,

        /// <summary>
        /// Pathologically deep <em>and</em> concentrated in one patch — the
        /// signature of a shrink/hide blendshape collapsing vertices into the
        /// body core. Not a body-shape difference; the fix is to neutralize the
        /// blendshape, so the message must say so rather than blame retargeting.
        /// </summary>
        CollapsedShapeKey,

        /// <summary>
        /// High penetrating ratio but spread across many small patches at shallow
        /// depth — the signature of a thick/enclosing garment's inner wall or a
        /// body-hugging accessory (shoes, chokers, belts, frills) whose back faces
        /// read as body penetration. A known false-positive class
        /// (docs/DESIGN.md §8): the garment fits, so the action is "verify
        /// visually", NOT a retargeting job. Only distinguishable when mesh
        /// connectivity is known (needs the largest-patch signal).
        /// </summary>
        ThickGarmentInnerWall,
    }

    public struct PreflightReport
    {
        public int vertexCount;

        /// <summary>Vertices within the margin zone — what the solver acts on.</summary>
        public int hitCount;

        /// <summary>Vertices actually below the body surface (margin excluded).</summary>
        public int penetratingCount;

        /// <summary><see cref="penetratingCount"/> / <see cref="vertexCount"/>.</summary>
        public float penetratingRatio;

        /// <summary>Deepest point below the body surface, in meters.</summary>
        public float maxDepth;

        /// <summary>95th-percentile depth below the surface, in meters.</summary>
        public float p95Depth;

        /// <summary>Worst depth relative to the hit capsule's radius.</summary>
        public float maxDepthOverRadius;

        /// <summary>Largest connected penetrating patch / all vertices.</summary>
        public float largestPatchRatio;

        public PreflightVerdict verdict;

        /// <summary>
        /// Root cause when <see cref="verdict"/> is Red; <see cref="RedCause.None"/>
        /// otherwise. Drives the user-facing message (collapsed blendshape vs
        /// genuine retargeting-class difference).
        /// </summary>
        public RedCause redCause;
    }

    /// <summary>
    /// Judges, before anything is applied, whether the detected penetration
    /// is inside the body-shape-difference envelope this tool supports
    /// (docs/DESIGN.md §9). Depths are measured below the actual body
    /// surface — not the margin surface — so a well-fitting outfit that
    /// merely grazes the margin zone stays green.
    /// </summary>
    public static class PreflightDiagnostic
    {
        // Initial thresholds from the DESIGN.md §9 table; to be calibrated
        // against real avatars during E2E.
        public const float GreenMaxDepth = 0.01f;
        public const float GreenMaxDepthOverRadius = 0.15f;
        public const float GreenMaxPenetratingRatio = 0.10f;
        public const float RedDepth = 0.03f;
        public const float RedPenetratingRatio = 0.30f;

        // Red-cause signature thresholds (provisional; calibrate with the
        // verdict thresholds during E2E). A collapsed shrink/hide blendshape
        // shows up as pathological depth — far past any plausible body-shape
        // difference — concentrated in a single connected patch.
        public const float CollapseDepth = 0.05f;          // >> RedDepth
        public const float CollapseClusterShare = 0.5f;    // patch holds ≥ half the penetrating verts

        public static PreflightReport Evaluate(
            Vector3[] positions,
            int[] triangles,
            IReadOnlyList<PenetrationHit> hits,
            IReadOnlyList<BodyCapsule> capsules,
            float margin)
        {
            if (capsules == null)
            {
                return new PreflightReport
                {
                    vertexCount = positions != null ? positions.Length : 0,
                    hitCount = hits != null ? hits.Count : 0,
                    verdict = PreflightVerdict.Green,
                };
            }
            // The closest capsule's radius is the local thickness; depth/radius
            // keeps its capsule meaning.
            return Evaluate(positions, triangles, hits, margin,
                hit => capsules[hit.capsuleIndex].radius);
        }

        /// <summary>
        /// Collider-backend preflight. Depth is measured the same way; the
        /// local thickness used to normalize it comes from the collider
        /// (capsule radius or a nominal mesh thickness, docs/DESIGN.md §9).
        /// </summary>
        public static PreflightReport Evaluate(
            Vector3[] positions,
            int[] triangles,
            IReadOnlyList<PenetrationHit> hits,
            IBodyCollider collider,
            float margin)
        {
            if (collider == null)
            {
                return new PreflightReport
                {
                    vertexCount = positions != null ? positions.Length : 0,
                    hitCount = hits != null ? hits.Count : 0,
                    verdict = PreflightVerdict.Green,
                };
            }
            return Evaluate(positions, triangles, hits, margin,
                hit => collider.LocalThickness(hit.position));
        }

        static PreflightReport Evaluate(
            Vector3[] positions,
            int[] triangles,
            IReadOnlyList<PenetrationHit> hits,
            float margin,
            System.Func<PenetrationHit, float> localThicknessOf)
        {
            var report = new PreflightReport
            {
                vertexCount = positions != null ? positions.Length : 0,
                hitCount = hits != null ? hits.Count : 0,
                verdict = PreflightVerdict.Green,
            };
            if (report.vertexCount == 0 || hits == null)
            {
                return report;
            }

            var depths = new List<float>(hits.Count);
            var penetratingVertices = new HashSet<int>();
            foreach (var hit in hits)
            {
                float surfaceDepth = hit.depth - margin;
                if (surfaceDepth <= 0f)
                {
                    continue; // margin-zone graze, not a body penetration
                }
                depths.Add(surfaceDepth);
                penetratingVertices.Add(hit.vertexIndex);
                report.maxDepth = Mathf.Max(report.maxDepth, surfaceDepth);

                float thickness = localThicknessOf(hit);
                if (thickness > 1e-6f)
                {
                    report.maxDepthOverRadius = Mathf.Max(report.maxDepthOverRadius, surfaceDepth / thickness);
                }
            }

            report.penetratingCount = penetratingVertices.Count;
            report.penetratingRatio = (float)report.penetratingCount / report.vertexCount;
            report.p95Depth = Percentile95(depths);
            report.largestPatchRatio = LargestPatchRatio(positions, triangles, penetratingVertices);
            report.verdict = Judge(report);
            report.redCause = ClassifyRedCause(report);
            return report;
        }

        static PreflightVerdict Judge(PreflightReport report)
        {
            if (report.maxDepth > RedDepth || report.penetratingRatio > RedPenetratingRatio)
            {
                return PreflightVerdict.Red;
            }
            if (report.maxDepth <= GreenMaxDepth
                && report.maxDepthOverRadius <= GreenMaxDepthOverRadius
                && report.penetratingRatio <= GreenMaxPenetratingRatio)
            {
                return PreflightVerdict.Green;
            }
            return PreflightVerdict.Yellow;
        }

        /// <summary>
        /// Names the root cause of a Red from its numeric signature, so the
        /// message points at the real fix:
        /// <list type="bullet">
        /// <item>deep <em>and</em> concentrated in one patch → a collapsed
        /// shrink/hide blendshape (neutralize the shape);</item>
        /// <item>high ratio but spread across many small patches at shallow depth
        /// → a thick/enclosing garment's inner wall, a §8 false positive (verify
        /// visually, do not retarget);</item>
        /// <item>otherwise → a genuine retargeting-class body difference.</item>
        /// </list>
        /// Patch concentration is measured relative to how much penetrates, so a
        /// small-but-deep folded clump still reads as collapsed even when the
        /// overall penetrating ratio is modest. The inner-wall case needs mesh
        /// connectivity, so it only fires when the largest-patch signal exists.
        /// </summary>
        static RedCause ClassifyRedCause(PreflightReport report)
        {
            if (report.verdict != PreflightVerdict.Red)
            {
                return RedCause.None;
            }
            bool clustered = report.penetratingRatio > 0f
                && report.largestPatchRatio >= report.penetratingRatio * CollapseClusterShare;
            if (report.maxDepth >= CollapseDepth && clustered)
            {
                return RedCause.CollapsedShapeKey;
            }
            // Dispersed (high ratio, no single concentrated patch) and not a deep
            // poke: an enclosing garment's inner wall / body-hugging accessory
            // reading as penetration — a §8 false positive, not a body-shape
            // difference. Requires connectivity, so only when largestPatchRatio > 0.
            bool dispersed = report.largestPatchRatio > 0f && !clustered;
            if (dispersed && report.maxDepth <= RedDepth)
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
            int index = Mathf.CeilToInt(0.95f * depths.Count) - 1;
            return depths[Mathf.Clamp(index, 0, depths.Count - 1)];
        }

        /// <summary>
        /// Size of the largest edge-connected component among penetrating
        /// vertices, as a fraction of all vertices. Vertex count stands in
        /// for surface area — adequate for a coarse scope gate.
        /// </summary>
        static float LargestPatchRatio(Vector3[] positions, int[] triangles, HashSet<int> penetratingVertices)
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
                largest = Mathf.Max(largest, memberCount);
            }
            return (float)largest / positions.Length;
        }
    }
}
