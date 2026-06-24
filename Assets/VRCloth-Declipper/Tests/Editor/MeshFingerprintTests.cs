using NUnit.Framework;
using UnityEngine;

namespace VRClothDeclipper.Tests
{
    /// <summary>
    /// Pins the measurement version key (docs/MEASUREMENT_SPEC.md §6): a canonical
    /// content hash that is stable under sub-quantum jitter, sensitive to real
    /// geometry change, and order-independent across a split body's parts.
    /// </summary>
    public class MeshFingerprintTests
    {
        static Vector3[] Verts(params float[] xyz)
        {
            var v = new Vector3[xyz.Length / 3];
            for (int i = 0; i < v.Length; i++)
            {
                v[i] = new Vector3(xyz[i * 3], xyz[i * 3 + 1], xyz[i * 3 + 2]);
            }
            return v;
        }

        [Test]
        public void SameGeometry_SameHash()
        {
            var a = MeshFingerprint.Compute(Verts(0, 0, 0, 1, 0, 0, 0, 1, 0), new[] { 0, 1, 2 });
            var b = MeshFingerprint.Compute(Verts(0, 0, 0, 1, 0, 0, 0, 1, 0), new[] { 0, 1, 2 });
            Assert.AreEqual(a, b);
        }

        [Test]
        public void SubQuantumJitter_SameHash()
        {
            // A shift smaller than the 0.1 mm quantum must not change the hash.
            var a = MeshFingerprint.Compute(Verts(0f, 0f, 0f, 1f, 0f, 0f), new[] { 0, 1, 0 });
            var b = MeshFingerprint.Compute(Verts(0.00002f, 0f, 0f, 1.00003f, 0f, 0f), new[] { 0, 1, 0 });
            Assert.AreEqual(a, b, "jitter below the quantum should be absorbed");
        }

        [Test]
        public void RealGeometryChange_DifferentHash()
        {
            var a = MeshFingerprint.Compute(Verts(0, 0, 0, 1, 0, 0), new[] { 0, 1, 0 });
            var b = MeshFingerprint.Compute(Verts(0, 0, 0, 1.5f, 0, 0), new[] { 0, 1, 0 }); // 50 cm: well past quantum
            Assert.AreNotEqual(a, b);
        }

        [Test]
        public void DifferentTopology_DifferentHash()
        {
            var verts = Verts(0, 0, 0, 1, 0, 0, 0, 1, 0);
            Assert.AreNotEqual(
                MeshFingerprint.Compute(verts, new[] { 0, 1, 2 }),
                MeshFingerprint.Compute(verts, new[] { 0, 2, 1 }));
        }

        [Test]
        public void Combine_IsOrderIndependent()
        {
            string h1 = "aaaa", h2 = "bbbb", h3 = "cccc";
            Assert.AreEqual(
                MeshFingerprint.Combine(new[] { h1, h2, h3 }),
                MeshFingerprint.Combine(new[] { h3, h1, h2 }),
                "the same set of part hashes must combine identically regardless of order");
        }

        [Test]
        public void Combine_DistinguishesDifferentSets()
        {
            Assert.AreNotEqual(
                MeshFingerprint.Combine(new[] { "aaaa", "bbbb" }),
                MeshFingerprint.Combine(new[] { "aaaa", "cccc" }));
        }

        [Test]
        public void Empty_IsStableAndHex()
        {
            string h = MeshFingerprint.Compute(null, null);
            Assert.AreEqual(MeshFingerprint.Compute(System.Array.Empty<Vector3>(), System.Array.Empty<int>()), h);
            Assert.AreEqual(64, h.Length, "SHA-256 hex is 64 chars");
        }
    }
}
