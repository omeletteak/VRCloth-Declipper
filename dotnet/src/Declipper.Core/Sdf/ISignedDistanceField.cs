using System.Numerics;

namespace Declipper.Core.Sdf
{
    /// <summary>
    /// One SDF query result: signed distance and unit gradient, computed
    /// together. v1 (IBodyCollider) exposed these as separate queries and the
    /// solver paid the closest-point search twice per vertex; the combined
    /// sample is the v2 contract (docs/REARCHITECTURE.md §2 柱2).
    /// </summary>
    public readonly struct SdfSample
    {
        /// <summary>
        /// Signed distance to the body surface in meters: negative inside,
        /// positive outside, zero on the surface.
        /// </summary>
        public readonly float Distance;

        /// <summary>
        /// Unit gradient of the distance field — the push-out direction (away
        /// from the surface) everywhere, inside and outside. Implementations
        /// must return a usable unit vector even at degenerate points (fall
        /// back to a fixed axis, never NaN / zero).
        /// </summary>
        public readonly Vector3 Gradient;

        public SdfSample(float distance, Vector3 gradient)
        {
            Distance = distance;
            Gradient = gradient;
        }
    }

    /// <summary>
    /// The single body representation of v2. Everything downstream —
    /// detection, the projected solver, preflight, measurement — talks to this
    /// and only this. Bone capsules (<see cref="CapsuleSetSdf"/>) and the mesh
    /// SDF (<see cref="MeshSdf"/>) are just builders of this interface;
    /// representation-specific metadata (e.g. which capsule was hit) is an
    /// implementation concern and not part of the contract.
    /// </summary>
    public interface ISignedDistanceField
    {
        /// <summary>Signed distance and unit gradient at <paramref name="position"/> (world meters).</summary>
        SdfSample Sample(in Vector3 position);

        /// <summary>
        /// Local feature scale at <paramref name="position"/> in meters, used
        /// by preflight to normalize penetration depth (capsule radius for the
        /// capsule backend, nominal body thickness for the mesh SDF). Port of
        /// v1 IBodyCollider.LocalThickness.
        /// </summary>
        float LocalThickness(in Vector3 position);
    }

    /// <summary>
    /// Push-out / containment math shared by every backend, derived from the
    /// sample so no implementation reinvents it.
    /// </summary>
    public static class SdfExtensions
    {
        /// <summary>
        /// The position <paramref name="point"/> should move to so it sits
        /// exactly <paramref name="margin"/> above the body surface, moving
        /// along the SDF gradient.
        /// </summary>
        public static Vector3 PushOut(this ISignedDistanceField sdf, in Vector3 point, float margin)
        {
            SdfSample s = sdf.Sample(point);
            return point + (margin - s.Distance) * s.Gradient;
        }

        /// <summary>True if <paramref name="point"/> is inside the body inflated by <paramref name="margin"/>.</summary>
        public static bool Contains(this ISignedDistanceField sdf, in Vector3 point, float margin = 0f)
        {
            return sdf.Sample(point).Distance < margin;
        }
    }
}
