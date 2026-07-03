using System.Numerics;
using Declipper.Core.Sdf;
using NUnit.Framework;

namespace Declipper.Core.Tests
{
    /// <summary>
    /// Tests for the one implemented skeleton piece. S1 golden tests (v1
    /// Unity output vs v2 on identical fixtures) replace these hand
    /// assertions as the source of truth; these pin the analytic basics.
    /// </summary>
    public class CapsuleSdfTests
    {
        [Test]
        public void SphereCase_DistanceIsRadialMinusRadius()
        {
            // Zero-length axis = a sphere of radius 0.5 at the origin.
            var capsule = new Capsule(Vector3.Zero, Vector3.Zero, 0.5f);

            Assert.That(capsule.Sample(new Vector3(2f, 0f, 0f)).Distance, Is.EqualTo(1.5f).Within(1e-6f));
            Assert.That(capsule.Sample(new Vector3(0.2f, 0f, 0f)).Distance, Is.EqualTo(-0.3f).Within(1e-6f));
            Assert.That(capsule.Sample(new Vector3(0.5f, 0f, 0f)).Distance, Is.EqualTo(0f).Within(1e-6f));
        }

        [Test]
        public void Gradient_IsUnitLength_EvenOnAxis()
        {
            var capsule = new Capsule(Vector3.Zero, new Vector3(0f, 1f, 0f), 0.1f);

            // Regular point.
            Assert.That(capsule.Sample(new Vector3(0.3f, 0.5f, 0f)).Gradient.Length(),
                Is.EqualTo(1f).Within(1e-5f));
            // Singular point exactly on the axis must still yield a unit vector.
            Assert.That(capsule.Sample(new Vector3(0f, 0.5f, 0f)).Gradient.Length(),
                Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void PushOut_LandsExactlyOnMarginSurface()
        {
            var sdf = new CapsuleSetSdf(new[]
            {
                new Capsule(Vector3.Zero, new Vector3(0f, 1f, 0f), 0.1f),
            });
            const float margin = 0.005f;

            Vector3 penetrating = new Vector3(0.05f, 0.5f, 0f);
            Vector3 pushed = sdf.PushOut(penetrating, margin);

            Assert.That(sdf.Sample(pushed).Distance, Is.EqualTo(margin).Within(1e-5f));
        }

        [Test]
        public void CapsuleSet_UnionPicksNearerCapsule()
        {
            var left = new Capsule(new Vector3(-1f, 0f, 0f), new Vector3(-1f, 1f, 0f), 0.2f);
            var right = new Capsule(new Vector3(1f, 0f, 0f), new Vector3(1f, 1f, 0f), 0.3f);
            var sdf = new CapsuleSetSdf(new[] { left, right });

            Vector3 nearRight = new Vector3(0.8f, 0.5f, 0f);
            Assert.That(sdf.Sample(nearRight).Distance,
                Is.EqualTo(right.Sample(nearRight).Distance).Within(1e-6f));
            Assert.That(sdf.LocalThickness(nearRight), Is.EqualTo(0.3f).Within(1e-6f));

            Vector3 nearLeft = new Vector3(-0.7f, 0.5f, 0f);
            Assert.That(sdf.Sample(nearLeft).Distance,
                Is.EqualTo(left.Sample(nearLeft).Distance).Within(1e-6f));
            Assert.That(sdf.LocalThickness(nearLeft), Is.EqualTo(0.2f).Within(1e-6f));
        }

        [Test]
        public void Contains_RespectsMargin()
        {
            var sdf = new CapsuleSetSdf(new[]
            {
                new Capsule(Vector3.Zero, Vector3.Zero, 0.5f),
            });

            Assert.That(sdf.Contains(new Vector3(0.52f, 0f, 0f)), Is.False);
            Assert.That(sdf.Contains(new Vector3(0.52f, 0f, 0f), margin: 0.05f), Is.True);
            Assert.That(sdf.Contains(new Vector3(0.3f, 0f, 0f)), Is.True);
        }
    }
}
