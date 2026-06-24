using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace VRClothDeclipper
{
    /// <summary>
    /// Canonical content hash of a mesh — the version key for a measurement
    /// (docs/MEASUREMENT_SPEC.md §6, ECOSYSTEM_VISION.md §6). Identifies "which
    /// asset version was measured" for provenance / staleness / re-verification.
    /// One-way (SHA-256): a fingerprint, NOT the shape — it cannot be inverted to
    /// vertices, so it leaks no geometry (No Cache holds). Pure and
    /// editor-independent, so it is unit-testable like the rest of Core.
    ///
    /// Canonicalization: vertices are quantized to <see cref="DefaultQuantum"/>
    /// (0.1 mm) before hashing, so float jitter from re-import or platform does not
    /// change the hash; mesh-index order and triangle indices are part of the
    /// stream. Feed the bind-pose <c>sharedMesh</c> (not a baked/posed mesh) so the
    /// hash is stable across scene pose and scale.
    ///
    /// v1 scope / deferred (docs/MEASUREMENT_SPEC.md §6 保留): hashes base-mesh
    /// geometry only — blendshape frames, a scale-invariance pass beyond using
    /// sharedMesh, and cross-platform byte-order determinism are not yet included.
    /// Hash-equality means "same exact asset", NOT "same avatar" across users
    /// (import settings/mods differ); cross-user comparison uses the measured
    /// scalars, not this hash.
    /// </summary>
    public static class MeshFingerprint
    {
        /// <summary>Quantization step before hashing, in meters (0.1 mm).</summary>
        public const float DefaultQuantum = 0.0001f;

        /// <summary>
        /// SHA-256 (hex) over quantized vertices + triangle indices of one mesh.
        /// Deterministic for identical geometry; stable under sub-quantum jitter.
        /// </summary>
        public static string Compute(Vector3[] vertices, int[] triangles, float quantum = DefaultQuantum)
        {
            vertices = vertices ?? System.Array.Empty<Vector3>();
            triangles = triangles ?? System.Array.Empty<int>();
            float inv = quantum > 0f ? 1f / quantum : 1f;

            using (var ms = new System.IO.MemoryStream())
            using (var bw = new System.IO.BinaryWriter(ms))
            {
                bw.Write(vertices.Length);
                for (int i = 0; i < vertices.Length; i++)
                {
                    bw.Write(Mathf.RoundToInt(vertices[i].x * inv));
                    bw.Write(Mathf.RoundToInt(vertices[i].y * inv));
                    bw.Write(Mathf.RoundToInt(vertices[i].z * inv));
                }
                bw.Write(triangles.Length);
                for (int i = 0; i < triangles.Length; i++)
                {
                    bw.Write(triangles[i]);
                }
                bw.Flush();
                using (var sha = SHA256.Create())
                {
                    return ToHex(sha.ComputeHash(ms.ToArray()));
                }
            }
        }

        /// <summary>
        /// Combine per-part fingerprints into one, order-independent (the body is
        /// split across several meshes, docs §3): sort the part hashes, then hash
        /// their concatenation. Same set of parts → same combined hash regardless
        /// of enumeration order.
        /// </summary>
        public static string Combine(IReadOnlyList<string> partHashes)
        {
            var sorted = new List<string>(partHashes ?? new string[0]);
            sorted.Sort(System.StringComparer.Ordinal);
            using (var sha = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(string.Join("\n", sorted));
                return ToHex(sha.ComputeHash(bytes));
            }
        }

        static string ToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
            {
                sb.Append(bytes[i].ToString("x2"));
            }
            return sb.ToString();
        }
    }
}
