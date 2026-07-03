using System;
using System.Collections.Generic;
using System.Numerics;
using Declipper.Core.Sdf;
using Declipper.Core.Solver;

namespace Declipper.Core.Diagnostics
{
    public enum PreflightVerdict
    {
        Green,
        Yellow,
        Red,
    }

    /// <summary>
    /// Named cause of a Red verdict — diagnostic honesty is a v1 invariant
    /// that carries over unchanged (docs/DIAGNOSTIC_HONESTY.md,
    /// docs/REARCHITECTURE.md §3).
    /// </summary>
    public enum RedCause
    {
        /// <summary>Body-shape difference beyond the supported envelope.</summary>
        RetargetingClassDifference,

        /// <summary>A shrink/hide blendshape folding cloth deep into the body.</summary>
        CollapsedShapeKey,

        /// <summary>Thick/enclosing garment inner wall reading as penetration (false-positive class).</summary>
        ThickGarmentInnerWall,
    }

    /// <summary>
    /// Per-renderer verdict and the statistics behind it. Same shape as v1
    /// PreflightReport so thresholds calibrated on v1 E2E carry over.
    /// </summary>
    public readonly struct PreflightReport
    {
        public readonly PreflightVerdict Verdict;
        public readonly RedCause RedCause;
        public readonly int VertexCount;
        public readonly int PenetratingCount;
        public readonly float PenetratingRatio;
        public readonly float MaxDepth;
        public readonly float P95Depth;
        public readonly float MaxDepthOverThickness;
        public readonly float LargestPatchRatio;

        public PreflightReport(
            PreflightVerdict verdict, RedCause redCause, int vertexCount, int penetratingCount,
            float penetratingRatio, float maxDepth, float p95Depth, float maxDepthOverThickness,
            float largestPatchRatio)
        {
            Verdict = verdict;
            RedCause = redCause;
            VertexCount = vertexCount;
            PenetratingCount = penetratingCount;
            PenetratingRatio = penetratingRatio;
            MaxDepth = maxDepth;
            P95Depth = p95Depth;
            MaxDepthOverThickness = maxDepthOverThickness;
            LargestPatchRatio = largestPatchRatio;
        }
    }

    /// <summary>
    /// STUB — S1 port target. Judges whether the body-shape difference is
    /// within the supported envelope (green/yellow/red) from detection
    /// statistics, and names the Red cause.
    /// Port from Assets/VRCloth-Declipper/Core/PreflightDiagnostic.cs
    /// (thresholds are provisional pending E2E calibration — keep them as
    /// named constants in one place).
    /// </summary>
    public static class PreflightDiagnostic
    {
        public static PreflightReport Evaluate(
            Vector3[] positions, int[] triangles, List<PenetrationHit> hits,
            ISignedDistanceField body, float margin)
        {
            throw new NotImplementedException("S1: port PreflightDiagnostic.Evaluate.");
        }
    }
}
