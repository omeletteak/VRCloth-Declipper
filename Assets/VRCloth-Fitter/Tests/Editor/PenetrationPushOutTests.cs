using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace VRClothFitter.Tests
{
    public class PenetrationPushOutTests
    {
        const float Eps = 1e-5f;
        const float Margin = 0.005f;

        // Capsule along the Y axis from origin to (0,1,0), radius 0.25.
        static List<BodyCapsule> SingleCapsule()
        {
            return new List<BodyCapsule> { new BodyCapsule(Vector3.zero, new Vector3(0f, 1f, 0f), 0.25f) };
        }

        [Test]
        public void Apply_MovesHitVertexOntoMarginSurface()
        {
            var capsules = SingleCapsule();
            var positions = new[] { new Vector3(0.1f, 0.5f, 0f) };
            var hits = PenetrationDetection.Scan(positions, capsules, Margin);

            var offsets = PenetrationPushOut.Apply(positions, hits, capsules, Margin);

            // Pushed radially (+X) to radius + margin, keeping Y.
            AssertVector(new Vector3(0.255f, 0.5f, 0f), positions[0]);
            AssertVector(new Vector3(0.155f, 0f, 0f), offsets[0]);
            Assert.AreEqual(Margin, capsules[0].SignedDistance(positions[0]), Eps);
        }

        [Test]
        public void Apply_LeavesNonHitVerticesUntouched()
        {
            var capsules = SingleCapsule();
            var positions = new[]
            {
                new Vector3(1f, 0.5f, 0f),   // outside, no hit
                new Vector3(0.1f, 0.5f, 0f), // inside, hit
            };
            var hits = PenetrationDetection.Scan(positions, capsules, Margin);

            var offsets = PenetrationPushOut.Apply(positions, hits, capsules, Margin);

            AssertVector(new Vector3(1f, 0.5f, 0f), positions[0]);
            AssertVector(Vector3.zero, offsets[0]);
            Assert.AreNotEqual(Vector3.zero, offsets[1]);
        }

        [Test]
        public void Apply_ThenRescan_FindsNoPenetration()
        {
            var capsules = SingleCapsule();
            var positions = new[]
            {
                new Vector3(0f, 0.5f, 0f),      // on the axis
                new Vector3(0.1f, 0.5f, 0f),    // inside, sideways
                new Vector3(0.05f, 1.2f, 0.1f), // inside the end cap
                new Vector3(0.2f, -0.1f, -0.1f),
                new Vector3(0.3f, 0.5f, 0f),    // outside but within margin? sd = 0.05 > margin, no hit
            };
            var hits = PenetrationDetection.Scan(positions, capsules, Margin);
            Assert.Greater(hits.Count, 0);

            PenetrationPushOut.Apply(positions, hits, capsules, Margin);

            // Allow a tiny float tolerance below the exact margin surface.
            var remaining = PenetrationDetection.Scan(positions, capsules, Margin - 1e-4f);
            Assert.AreEqual(0, remaining.Count);
        }

        [Test]
        public void Apply_PushesFromCurrentPositionNotHitPosition()
        {
            var capsules = SingleCapsule();
            var positions = new[] { new Vector3(0.1f, 0.5f, 0f) };
            var hits = PenetrationDetection.Scan(positions, capsules, Margin);

            // Simulate smoothing moving the vertex before the re-push.
            positions[0] = new Vector3(0f, 0.5f, 0.1f);

            PenetrationPushOut.Apply(positions, hits, capsules, Margin);

            // Pushed along +Z (the current radial direction), not +X.
            AssertVector(new Vector3(0f, 0.5f, 0.255f), positions[0]);
        }

        [Test]
        public void Apply_NoHits_ChangesNothing()
        {
            var capsules = SingleCapsule();
            var positions = new[] { new Vector3(1f, 0.5f, 0f) };

            var offsets = PenetrationPushOut.Apply(positions, new List<PenetrationHit>(), capsules, Margin);

            AssertVector(new Vector3(1f, 0.5f, 0f), positions[0]);
            AssertVector(Vector3.zero, offsets[0]);
        }

        static void AssertVector(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, Eps, $"x of {actual}");
            Assert.AreEqual(expected.y, actual.y, Eps, $"y of {actual}");
            Assert.AreEqual(expected.z, actual.z, Eps, $"z of {actual}");
        }
    }
}
