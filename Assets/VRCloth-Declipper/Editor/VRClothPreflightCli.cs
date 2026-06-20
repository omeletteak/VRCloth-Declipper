using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VRClothDeclipper
{
    /// <summary>
    /// Headless preflight: run the diagnostic in batchmode without opening the
    /// editor GUI, so the numeric verdict (GREEN/YELLOW/RED + depths/ratios) can
    /// be produced by automation or CI. Visual acceptance stays a human gate
    /// (docs/E2E_TEST_GUIDE.md §3.1). Nothing is solved, applied or serialized
    /// back to assets — only a report JSON is written, so No Cache holds.
    ///
    /// Invoke (project must NOT be open in the editor GUI, and the tool must be
    /// installed in it via VPM or a junction):
    /// <code>
    /// Unity.exe -projectPath &lt;proj&gt; -batchmode -nographics
    ///   -executeMethod VRClothDeclipper.VRClothPreflightCli.Run
    ///   -vrclothScene "Assets/.../Scene.unity"   (optional; else the open scene)
    ///   -vrclothOut "C:/path/preflight.json"      (optional; else Temp)
    ///   -logFile "C:/path/log.txt"
    /// </code>
    /// Reads every active <see cref="VRClothDeclipper"/> in the scene. Exit code
    /// is 0 when the run completed (read the JSON for the verdicts) and non-zero
    /// only on error (no component, missing scene, exception) — the verdict
    /// itself does not change the exit code, so a RED result still exits 0.
    /// </summary>
    public static class VRClothPreflightCli
    {
        public static void Run()
        {
            int exitCode = 0;
            try
            {
                string scenePath = GetArg("-vrclothScene");
                string outPath = GetArg("-vrclothOut");

                if (!string.IsNullOrEmpty(scenePath))
                {
                    EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                }
                Scene scene = SceneManager.GetActiveScene();

                var fitters = UnityEngine.Object.FindObjectsByType<VRClothDeclipper>(FindObjectsSortMode.None);
                if (fitters == null || fitters.Length == 0)
                {
                    Debug.LogError($"[VRClothPreflightCli] No active VRClothDeclipper component found in scene '{scene.name}'. "
                        + "Add the component to the cloth root (re-add it after a Manual Bake — it is IEditorOnly) and save the scene.");
                    EditorApplication.Exit(2);
                    return;
                }

                var report = new CliReport
                {
                    scene = scene.path,
                    generatedAtUtc = DateTime.UtcNow.ToString("o"),
                    entries = BuildEntries(fitters),
                };
                report.worstVerdict = WorstVerdict(report.entries);

                if (string.IsNullOrEmpty(outPath))
                {
                    outPath = Path.Combine(Path.GetTempPath(), "vrcloth-preflight.json");
                }
                File.WriteAllText(outPath, JsonUtility.ToJson(report, true));
                Debug.Log($"[VRClothPreflightCli] Wrote {report.entries.Length} entr(ies) for {fitters.Length} fitter(s) to "
                    + $"'{outPath}'. Worst verdict: {report.worstVerdict}.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[VRClothPreflightCli] Failed: {e}");
                exitCode = 3;
            }
            EditorApplication.Exit(exitCode);
        }

        static CliEntry[] BuildEntries(VRClothDeclipper[] fitters)
        {
            var entries = new List<CliEntry>();
            foreach (var fitter in fitters)
            {
                var pf = VRClothPipeline.CaptureAndPreflight(fitter);
                if (pf == null)
                {
                    entries.Add(new CliEntry { fitter = fitter.name, renderer = "(aborted)", verdict = "ERROR" });
                    continue;
                }
                for (int i = 0; i < pf.cloth.Count; i++)
                {
                    var r = pf.reports[i];
                    entries.Add(new CliEntry
                    {
                        fitter = fitter.name,
                        renderer = pf.cloth[i].renderer != null ? pf.cloth[i].renderer.name : "(null)",
                        backend = pf.backend,
                        verdict = r.verdict.ToString().ToUpperInvariant(),
                        redCause = r.redCause.ToString(),
                        vertexCount = r.vertexCount,
                        penetratingCount = r.penetratingCount,
                        penetratingRatio = r.penetratingRatio,
                        maxDepthMm = r.maxDepth * 1000f,
                        p95DepthMm = r.p95Depth * 1000f,
                        maxDepthOverRadius = r.maxDepthOverRadius,
                        largestPatchRatio = r.largestPatchRatio,
                        marginZoneHits = r.hitCount,
                    });
                }
            }
            return entries.ToArray();
        }

        // Worst over GREEN < YELLOW < RED < ERROR, so a single line tells the
        // caller whether anything needs a look.
        static string WorstVerdict(CliEntry[] entries)
        {
            int worst = 0;
            foreach (var e in entries)
            {
                int rank = e.verdict == "ERROR" ? 3 : e.verdict == "RED" ? 2 : e.verdict == "YELLOW" ? 1 : 0;
                worst = Mathf.Max(worst, rank);
            }
            switch (worst)
            {
                case 3: return "ERROR";
                case 2: return "RED";
                case 1: return "YELLOW";
                default: return "GREEN";
            }
        }

        static string GetArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }
            return null;
        }

        [Serializable]
        class CliReport
        {
            public string scene;
            public string generatedAtUtc;
            public string worstVerdict;
            public CliEntry[] entries;
        }

        [Serializable]
        class CliEntry
        {
            public string fitter;
            public string renderer;
            public string backend;
            public string verdict;
            public string redCause;
            public int vertexCount;
            public int penetratingCount;
            public float penetratingRatio;
            public float maxDepthMm;
            public float p95DepthMm;
            public float maxDepthOverRadius;
            public float largestPatchRatio;
            public int marginZoneHits;
        }
    }
}
