using NUnit.Framework;
using UnityEngine;

namespace VRClothDeclipper.Tests
{
    /// <summary>
    /// Pins which vertices count as "collapsed" (folded to ~zero area by a
    /// shrink/hide blendshape) and are therefore excluded from detection.
    /// </summary>
    public class CollapsedVerticesTests
    {
        [Test]
        public void FoldedPatch_IsFlagged_WhileValidAndIsolatedVerticesAreNot()
        {
            var positions = new[]
            {
                // 0,1,2: folded to ~zero area (sub-micrometre triangle).
                new Vector3(0f, 0f, 0f),
                new Vector3(1e-6f, 0f, 0f),
                new Vector3(0f, 1e-6f, 0f),
                // 3,4,5: a normal 0.005 m^2 triangle.
                new Vector3(1f, 0f, 0f),
                new Vector3(1.1f, 0f, 0f),
                new Vector3(1f, 0.1f, 0f),
                // 6: no incident triangle.
                new Vector3(5f, 5f, 5f),
            };
            var triangles = new[] { 0, 1, 2, 3, 4, 5 };

            var collapsed = CollapsedVertices.Find(positions, triangles);

            Assert.IsTrue(collapsed.Contains(0) && collapsed.Contains(1) && collapsed.Contains(2),
                "all vertices of the folded triangle are collapsed");
            Assert.IsFalse(collapsed.Contains(3) || collapsed.Contains(4) || collapsed.Contains(5),
                "a valid triangle's vertices are kept");
            Assert.IsFalse(collapsed.Contains(6), "a vertex with no triangle is never flagged");
            Assert.AreEqual(3, collapsed.Count);
        }

        [Test]
        public void VertexSharedWithAGoodTriangle_IsNotFlagged()
        {
            var positions = new[]
            {
                new Vector3(0f, 0f, 0f),       // 0: only the folded triangle
                new Vector3(1e-6f, 0f, 0f),    // 1: only the folded triangle
                new Vector3(0f, 1e-6f, 0f),    // 2: folded triangle AND the good one
                new Vector3(0.5f, 0.5f, 0f),   // 3: good
                new Vector3(0.5f, -0.5f, 0f),  // 4: good
            };
            var triangles = new[]
            {
                0, 1, 2, // degenerate
                2, 3, 4, // valid, shares vertex 2
            };

            var collapsed = CollapsedVertices.Find(positions, triangles);

            Assert.IsTrue(collapsed.Contains(0) && collapsed.Contains(1));
            Assert.IsFalse(collapsed.Contains(2), "one valid incident triangle keeps the vertex in play");
            Assert.AreEqual(2, collapsed.Count);
        }

        [Test]
        public void NullOrEmptyInputs_ReturnEmpty()
        {
            Assert.AreEqual(0, CollapsedVertices.Find(null, new[] { 0, 1, 2 }).Count);
            Assert.AreEqual(0, CollapsedVertices.Find(new[] { Vector3.zero }, null).Count);
            Assert.AreEqual(0, CollapsedVertices.Find(new Vector3[0], new int[0]).Count);
        }
    }
}
