using System;
using System.Numerics;

namespace Declipper.Core.Solver
{
    /// <summary>
    /// STUB — S1 port target. Vertex neighborhood over the triangle topology,
    /// with coincident vertices (UV/normal seam duplicates) welded by position
    /// so smoothing does not tear seams.
    /// Port from Assets/VRCloth-Declipper/Core/VertexAdjacency.cs, keeping its
    /// welding semantics exactly (golden-test on a seamed fixture mesh).
    /// </summary>
    public sealed class VertexAdjacency
    {
        /// <summary>Neighbor vertex indices of <paramref name="vertex"/>.</summary>
        public ReadOnlySpan<int> Neighbors(int vertex)
        {
            throw new NotImplementedException("S1: port VertexAdjacency.");
        }

        public static VertexAdjacency Build(Vector3[] positions, int[] triangles)
        {
            throw new NotImplementedException("S1: port VertexAdjacency.");
        }
    }
}
