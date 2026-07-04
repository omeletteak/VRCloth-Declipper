using System.Numerics;
using Declipper.Core.Sdf;
using Declipper.Core.Surface;
using NUnit.Framework;

namespace Declipper.Core.Tests
{
    /// <summary>
    /// The round-trip identity Evaluate(Bind(p)) == p is the defining contract of
    /// <see cref="IBodySurface"/>, exercised across the cylinder body and both
    /// hemispherical caps (where a (t, azimuth)-only encoding would fail). Also
    /// checks the "penetration fixing = clamp NormalOffset" view against the SDF.
    /// </summary>
    public class CapsuleSurfaceTests
    {
        static Capsule Segment() => new Capsule(new Vector3(0f, -0.5f, 0f), new Vector3(0f, 0.5f, 0f), 0.2f);

        [Test]
        public void RoundTrip_HoldsAcrossBodyAndCaps()
        {
            var surface = new CapsuleSurface(new[] { Segment() });

            var probes = new[]
            {
                new Vector3(0.5f, 0f, 0f),    // beside the cylinder body
                new Vector3(0.05f, 0.1f, 0f), // inside the body
                new Vector3(0.3f, 0.2f, 0.1f),// off-axis, general
                new Vector3(0f, 1.2f, 0f),    // beyond the top pole (cap region)
                new Vector3(0.1f, -1f, 0.1f), // beyond the bottom pole (cap region)
                new Vector3(0.15f, 0.6f, 0.15f), // over the top cap shoulder
            };

            foreach (var p in probes)
            {
                Vector3 rt = surface.Evaluate(surface.Bind(p));
                Assert.That((rt - p).Length(), Is.LessThan(1e-4f), $"round-trip at {p} -> {rt}");
            }
        }

        [Test]
        public void RoundTrip_HoldsForSphere()
        {
            var surface = new CapsuleSurface(new[] { new Capsule(Vector3.Zero, Vector3.Zero, 0.3f) });
            foreach (var p in new[]
            {
                new Vector3(1f, 0f, 0f), new Vector3(0.1f, 0.1f, 0.1f),
                new Vector3(-0.4f, 0.2f, -0.5f), new Vector3(0f, -0.8f, 0f),
            })
            {
                Vector3 rt = surface.Evaluate(surface.Bind(p));
                Assert.That((rt - p).Length(), Is.LessThan(1e-4f), $"sphere round-trip at {p}");
            }
        }

        [Test]
        public void Bind_PicksNearestCapsuleAndRoundTrips()
        {
            var left = new Capsule(new Vector3(-1f, -0.5f, 0f), new Vector3(-1f, 0.5f, 0f), 0.2f);
            var right = new Capsule(new Vector3(1f, -0.5f, 0f), new Vector3(1f, 0.5f, 0f), 0.3f);
            var surface = new CapsuleSurface(new[] { left, right });

            var nearRight = new Vector3(0.7f, 0.1f, 0f);
            SurfaceBinding b = surface.Bind(nearRight);
            Assert.That(b.FeatureId, Is.EqualTo(1));
            Assert.That((surface.Evaluate(b) - nearRight).Length(), Is.LessThan(1e-4f));
        }

        [Test]
        public void ClampingNormalOffset_LandsOnMarginSurface()
        {
            // "Penetration fixing = clamp NormalOffset to margin": re-evaluating a
            // binding at a fixed offset must land exactly margin above the body,
            // cross-checked against the SDF built from the same capsule.
            var capsule = Segment();
            var surface = new CapsuleSurface(new[] { capsule });
            var sdf = new CapsuleSetSdf(new[] { capsule });
            const float margin = 0.01f;

            Vector3 penetrating = new Vector3(0.05f, 0.1f, 0f); // inside the body
            SurfaceBinding clamped = surface.Bind(penetrating).WithOffset(margin);
            Vector3 onMargin = surface.Evaluate(clamped);

            Assert.That(sdf.Sample(onMargin).Distance, Is.EqualTo(margin).Within(1e-5f));
        }
    }
}
