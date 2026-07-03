using System;
using System.Collections.Generic;
using System.Numerics;

namespace Declipper.Core.Solver
{
    /// <summary>
    /// STUB — S1 port target. Smoothing operates on the displacement field,
    /// never on positions, so cloth detail survives (v1 invariant, kept).
    /// Port from Assets/VRCloth-Declipper/Core/LaplacianSmoothing.cs.
    /// </summary>
    public static class LaplacianSmoothing
    {
        /// <summary>
        /// The seed set grown <paramref name="rings"/> rings outward along the
        /// adjacency — the region smoothing is allowed to touch.
        /// </summary>
        public static HashSet<int> ExpandRegion(VertexAdjacency adjacency, HashSet<int> seeds, int rings)
        {
            throw new NotImplementedException("S1: port LaplacianSmoothing.ExpandRegion.");
        }

        /// <summary>
        /// In-place Laplacian relaxation of <paramref name="displacements"/>
        /// restricted to <paramref name="region"/>: each step blends a vertex
        /// toward its neighbor average by <paramref name="lambda"/>.
        /// </summary>
        public static void Smooth(
            Vector3[] displacements, VertexAdjacency adjacency, HashSet<int> region,
            float lambda, int iterations)
        {
            throw new NotImplementedException("S1: port LaplacianSmoothing.Smooth.");
        }
    }
}
