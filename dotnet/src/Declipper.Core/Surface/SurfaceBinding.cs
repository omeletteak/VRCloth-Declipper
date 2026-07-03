using System.Numerics;

namespace Declipper.Core.Surface
{
    /// <summary>
    /// The v2 core primitive (docs/REARCHITECTURE.md §2 柱4): a cloth vertex
    /// expressed as a point on the body surface plus a signed offset along the
    /// surface normal. Everything above is a view of this:
    /// penetration fixing = clamping <see cref="NormalOffset"/> to ≥ margin,
    /// pose/retarget-class deformation = Bind on the source body then Evaluate
    /// on the deformed/target body, measurement and preflight = statistics
    /// over bindings.
    /// </summary>
    public readonly struct SurfaceBinding
    {
        /// <summary>
        /// The surface feature the binding lives on — triangle index for a
        /// mesh surface, capsule index for a capsule set. Meaningful only to
        /// the <see cref="IBodySurface"/> that created it.
        /// </summary>
        public readonly int FeatureId;

        /// <summary>
        /// Position within the feature — barycentric coordinates for a
        /// triangle; (axis parameter t, azimuth, unused) for a capsule.
        /// Meaningful only to the creating surface.
        /// </summary>
        public readonly Vector3 Coordinates;

        /// <summary>
        /// Signed height above the surface along its normal, in meters.
        /// Negative = penetrating.
        /// </summary>
        public readonly float NormalOffset;

        public SurfaceBinding(int featureId, Vector3 coordinates, float normalOffset)
        {
            FeatureId = featureId;
            Coordinates = coordinates;
            NormalOffset = normalOffset;
        }

        /// <summary>The same binding at a different height.</summary>
        public SurfaceBinding WithOffset(float normalOffset)
        {
            return new SurfaceBinding(FeatureId, Coordinates, normalOffset);
        }
    }

    /// <summary>
    /// STUB (contract only) — S1 implements this for the capsule set, the mesh
    /// version can follow later. A body as a parameterized surface that points
    /// can be bound to and re-evaluated from.
    ///
    /// Contract: for any p, Evaluate(Bind(p)) == p up to float error (the
    /// round-trip identity — make it the first test of every implementation).
    /// Bind must be total (every point in space has a closest surface point);
    /// Evaluate must accept bindings with any NormalOffset, including offsets
    /// the original point never had (that is what clamping and retargeting do).
    /// </summary>
    public interface IBodySurface
    {
        /// <summary>Closest-point binding of <paramref name="position"/> onto this surface.</summary>
        SurfaceBinding Bind(in Vector3 position);

        /// <summary>The world position a binding denotes on this surface.</summary>
        Vector3 Evaluate(in SurfaceBinding binding);
    }
}
