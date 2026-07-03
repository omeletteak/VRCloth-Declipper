using System;
using System.Numerics;

namespace Declipper.Core.Sdf
{
    /// <summary>
    /// STUB — S1 port target. The body-mesh SDF: exact closest-triangle
    /// distance via a BVH (branch-and-bound) plus sign from generalized
    /// winding number with Barnes–Hut far-field clustering (dipole
    /// approximation for far clusters, exact near the surface), robust to
    /// non-watertight meshes.
    ///
    /// Port from Assets/VRCloth-Declipper/Core/MeshSdfCollider.cs — the v1
    /// implementation is already accelerated (~63 ms per 2k-vertex scan
    /// against a 50k-triangle body) and its math carries over unchanged; only
    /// UnityEngine.Vector3 → System.Numerics.Vector3 changes. Golden-test
    /// against v1 outputs on fixture meshes before removal of v1
    /// (docs/REARCHITECTURE.md §4 S1). Keep the whole build in memory — no
    /// serialization API, No Cache holds.
    /// </summary>
    public sealed class MeshSdf : ISignedDistanceField
    {
        /// <param name="vertices">Body mesh vertices, world space, meters.</param>
        /// <param name="triangles">Index triples into <paramref name="vertices"/>.</param>
        public MeshSdf(Vector3[] vertices, int[] triangles)
        {
            throw new NotImplementedException("S1: port MeshSdfCollider (BVH + Barnes–Hut winding number).");
        }

        public SdfSample Sample(in Vector3 position)
        {
            throw new NotImplementedException("S1: port MeshSdfCollider (BVH + Barnes–Hut winding number).");
        }

        /// <summary>
        /// Nominal local body thickness. v1 derives this from the local
        /// geometry (see MeshSdfCollider.LocalThickness); keep the same
        /// definition so preflight thresholds calibrated on v1 carry over.
        /// </summary>
        public float LocalThickness(in Vector3 position)
        {
            throw new NotImplementedException("S1: port MeshSdfCollider (BVH + Barnes–Hut winding number).");
        }
    }
}
