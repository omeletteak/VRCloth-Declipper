using System;
using System.Collections.Generic;
using System.Numerics;
using Declipper.Core.Sdf;

namespace Declipper.Core.Surface
{
    /// <summary>
    /// <see cref="IBodySurface"/> over a set of capsules — the capsule-set
    /// counterpart of <see cref="CapsuleSetSdf"/>. New in v2 (v1 had no surface
    /// binding); it is the concrete anchor for the §2 柱4 primitive.
    ///
    /// Coordinate encoding: <c>Coordinates = (t, na, θ)</c> where t is the axis
    /// segment parameter of the closest axis point, na is the axial component of
    /// the outward surface normal (cos of the elevation from the axis), and θ is
    /// the azimuth of the normal's radial part in a capsule-fixed frame. The
    /// REARCHITECTURE stub sketched "(t, azimuth, unused)", but (t, azimuth) is
    /// only two DOF and cannot represent the hemispherical caps, so the
    /// round-trip identity would break there; na fills the "unused" slot and
    /// makes <c>Evaluate(Bind(p)) == p</c> exact everywhere (caps included).
    /// </summary>
    public sealed class CapsuleSurface : IBodySurface
    {
        readonly Capsule[] capsules;
        readonly Frame[] frames;

        readonly struct Frame
        {
            public readonly Vector3 AxisDir; // unit axis (or a fixed axis for a sphere)
            public readonly Vector3 U, V;    // orthonormal basis spanning the radial plane
            public readonly float Length;    // axis length (0 for a sphere)

            public Frame(Vector3 axisDir, Vector3 u, Vector3 v, float length)
            {
                AxisDir = axisDir;
                U = u;
                V = v;
                Length = length;
            }
        }

        public CapsuleSurface(IReadOnlyList<Capsule> capsules)
        {
            if (capsules == null || capsules.Count == 0)
            {
                throw new ArgumentException("CapsuleSurface requires at least one capsule.", nameof(capsules));
            }
            this.capsules = new Capsule[capsules.Count];
            this.frames = new Frame[capsules.Count];
            for (int i = 0; i < capsules.Count; i++)
            {
                this.capsules[i] = capsules[i];
                this.frames[i] = BuildFrame(capsules[i]);
            }
        }

        static Frame BuildFrame(Capsule capsule)
        {
            Vector3 axis = capsule.End - capsule.Start;
            float length = axis.Length();
            Vector3 axisDir = length >= 1e-9f ? axis / length : Vector3.UnitY;

            // A deterministic radial basis perpendicular to the axis.
            Vector3 u = Vector3.Cross(axisDir, Vector3.UnitY);
            if (u.LengthSquared() < 1e-12f)
            {
                u = Vector3.Cross(axisDir, Vector3.UnitX);
            }
            u = Vector3.Normalize(u);
            Vector3 v = Vector3.Cross(axisDir, u); // unit: axisDir ⊥ u, both unit
            return new Frame(axisDir, u, v, length);
        }

        public SurfaceBinding Bind(in Vector3 position)
        {
            int best = Nearest(position);
            Capsule capsule = capsules[best];
            Frame frame = frames[best];

            float t = 0f;
            if (frame.Length >= 1e-9f)
            {
                t = Vector3.Dot(position - capsule.Start, frame.AxisDir) / frame.Length;
                t = t < 0f ? 0f : (t > 1f ? 1f : t);
            }

            SdfSample sample = capsule.Sample(position);
            Vector3 normal = sample.Gradient;
            float na = Vector3.Dot(normal, frame.AxisDir);
            Vector3 radial = normal - na * frame.AxisDir;
            float theta = MathF.Atan2(Vector3.Dot(radial, frame.V), Vector3.Dot(radial, frame.U));

            return new SurfaceBinding(best, new Vector3(t, na, theta), sample.Distance);
        }

        public Vector3 Evaluate(in SurfaceBinding binding)
        {
            Capsule capsule = capsules[binding.FeatureId];
            Frame frame = frames[binding.FeatureId];

            float t = binding.Coordinates.X;
            float na = binding.Coordinates.Y;
            float theta = binding.Coordinates.Z;

            Vector3 onAxis = capsule.Start + (capsule.End - capsule.Start) * t;

            float radialMag = MathF.Sqrt(MathF.Max(0f, 1f - na * na));
            Vector3 e = MathF.Cos(theta) * frame.U + MathF.Sin(theta) * frame.V;
            Vector3 normal = na * frame.AxisDir + radialMag * e;

            return onAxis + normal * (capsule.Radius + binding.NormalOffset);
        }

        int Nearest(in Vector3 position)
        {
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < capsules.Length; i++)
            {
                float d = capsules[i].Sample(position).Distance;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }
            return best;
        }
    }
}
