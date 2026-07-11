using System.Numerics;

namespace Declipper.Core.Sdf
{
    /// <summary>
    /// A body part approximated by a capsule. Port of v1 BodyCapsule
    /// (Assets/VRCloth-Declipper/Core/BodyCapsule.cs) minus the label —
    /// per-capsule attribution is diagnostic metadata, not core contract
    /// (docs/REARCHITECTURE.md §2 柱2).
    /// </summary>
    public readonly struct Capsule
    {
        public readonly Vector3 Start;
        public readonly Vector3 End;
        public readonly float Radius;

        public Capsule(Vector3 start, Vector3 end, float radius)
        {
            Start = start;
            End = end;
            Radius = radius;
        }

        /// <summary>
        /// The point on the axis segment closest to <paramref name="point"/>.
        /// A zero-length axis makes the capsule act as a sphere.
        /// </summary>
        public Vector3 ClosestPointOnAxis(in Vector3 point)
        {
            Vector3 axis = End - Start;
            float lengthSq = axis.LengthSquared();
            if (lengthSq < 1e-12f)
            {
                return Start;
            }
            float t = Vector3.Dot(point - Start, axis) / lengthSq;
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            return Start + axis * t;
        }

        /// <summary>
        /// Signed distance and unit gradient at <paramref name="point"/>.
        /// The gradient's only singularity (a point exactly on the axis)
        /// falls back to a fixed perpendicular so callers always receive a
        /// usable unit vector.
        /// </summary>
        public SdfSample Sample(in Vector3 point)
        {
            Vector3 onAxis = ClosestPointOnAxis(point);
            Vector3 direction = point - onAxis;
            float axisDistance = direction.Length();
            Vector3 gradient;
            if (axisDistance >= 1e-9f)
            {
                gradient = direction / axisDistance;
            }
            else
            {
                Vector3 axis = End - Start;
                gradient = Vector3.Cross(axis, Vector3.UnitY);
                if (gradient.LengthSquared() < 1e-12f)
                {
                    gradient = Vector3.Cross(axis, Vector3.UnitX);
                }
                if (gradient.LengthSquared() < 1e-12f)
                {
                    gradient = Vector3.UnitX;
                }
                gradient = Vector3.Normalize(gradient);
            }
            return new SdfSample(axisDistance - Radius, gradient);
        }
    }
}
