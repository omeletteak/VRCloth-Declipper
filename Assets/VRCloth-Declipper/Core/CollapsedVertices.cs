using System.Collections.Generic;
using UnityEngine;

namespace VRClothDeclipper
{
    /// <summary>
    /// Finds cloth vertices that a shrink/hide blendshape has folded to ~zero
    /// area. The mesh has collapsed there, so the vertices sit deep in the body
    /// core but are not a real, visible surface. Detection excludes them so a
    /// collapsed region cannot drive a false RED that skips the whole renderer
    /// and leaves the visible part unfixed (ROADMAP phase 3, docs/DESIGN.md §9).
    /// Pure geometry on the baked (blendshape-applied) positions.
    /// </summary>
    public static class CollapsedVertices
    {
        // A triangle counts as collapsed below this area (m^2). Provisional;
        // calibrate in E2E. 1e-8 m^2 is a ~0.14 mm-sided triangle — only true
        // folds, never a normally small but valid triangle.
        public const float DegenerateTriangleArea = 1e-8f;

        /// <summary>
        /// Vertex indices whose every incident triangle is degenerate (folded).
        /// A vertex with at least one non-degenerate incident triangle — or with
        /// no incident triangle at all — is not flagged, so a lone valid
        /// neighbour keeps a vertex in play.
        /// </summary>
        public static HashSet<int> Find(IReadOnlyList<Vector3> positions, IReadOnlyList<int> triangles)
        {
            var collapsed = new HashSet<int>();
            if (positions == null || triangles == null || positions.Count == 0)
            {
                return collapsed;
            }

            int n = positions.Count;
            var incident = new int[n];
            var nonDegenerate = new int[n];
            for (int t = 0; t + 2 < triangles.Count; t += 3)
            {
                int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                if (a < 0 || b < 0 || c < 0 || a >= n || b >= n || c >= n)
                {
                    continue;
                }
                bool good = TriangleArea(positions[a], positions[b], positions[c]) >= DegenerateTriangleArea;
                incident[a]++; incident[b]++; incident[c]++;
                if (good)
                {
                    nonDegenerate[a]++; nonDegenerate[b]++; nonDegenerate[c]++;
                }
            }

            for (int i = 0; i < n; i++)
            {
                if (incident[i] > 0 && nonDegenerate[i] == 0)
                {
                    collapsed.Add(i);
                }
            }
            return collapsed;
        }

        static float TriangleArea(Vector3 a, Vector3 b, Vector3 c)
        {
            return 0.5f * Vector3.Cross(b - a, c - a).magnitude;
        }
    }
}
