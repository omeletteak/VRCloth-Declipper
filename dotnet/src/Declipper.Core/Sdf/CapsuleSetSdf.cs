using System;
using System.Collections.Generic;
using System.Numerics;

namespace Declipper.Core.Sdf
{
    /// <summary>
    /// The cheap SDF builder: a union of bone capsules, for when no body mesh
    /// is available. Union semantics = the minimum signed distance over all
    /// capsules; gradient and thickness come from the winning capsule.
    /// Functional port of v1 CapsuleBodyCollider — S1 must golden-test this
    /// against the Unity implementation on identical inputs before v1 is
    /// removed (docs/REARCHITECTURE.md §4 S1).
    /// </summary>
    public sealed class CapsuleSetSdf : ISignedDistanceField
    {
        readonly Capsule[] capsules;

        public CapsuleSetSdf(IReadOnlyList<Capsule> capsules)
        {
            if (capsules == null || capsules.Count == 0)
            {
                throw new ArgumentException("CapsuleSetSdf requires at least one capsule.", nameof(capsules));
            }
            this.capsules = new Capsule[capsules.Count];
            for (int i = 0; i < capsules.Count; i++)
            {
                this.capsules[i] = capsules[i];
            }
        }

        public SdfSample Sample(in Vector3 position)
        {
            int best = Nearest(position);
            return capsules[best].Sample(position);
        }

        public float LocalThickness(in Vector3 position)
        {
            return capsules[Nearest(position)].Radius;
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
