using UnityEditor;
using UnityEngine;

public class VRClothFitterWindow : EditorWindow
{
    private enum QualityMode { Lightweight, Medium, High }
    private QualityMode mode = QualityMode.Lightweight;

    private VRClothDiffAsset selectedDiff;
    private Mesh targetMesh;

    [MenuItem("Tools/VRCloth Fitter")]
    public static void ShowWindow()
    {
        GetWindow<VRClothFitterWindow>("VRCloth Fitter");
    }

    private void OnGUI()
    {
        GUILayout.Label("VRCloth Fitter", EditorStyles.boldLabel);

        mode = (QualityMode)EditorGUILayout.EnumPopup("Mode", mode);

        if (GUILayout.Button("Run"))
        {
            VRClothPipeline.Run(mode.ToString());
        }

        if (GUILayout.Button("Clear Cache"))
        {
            VRClothPipeline.ClearCache();
        }

        EditorGUILayout.Space();
        GUILayout.Label("Apply Saved Diff", EditorStyles.boldLabel);
        selectedDiff = (VRClothDiffAsset)EditorGUILayout.ObjectField("Diff Asset", selectedDiff, typeof(VRClothDiffAsset), false);
        targetMesh = (Mesh)EditorGUILayout.ObjectField("Target Mesh", targetMesh, typeof(Mesh), false);

        if (GUILayout.Button("Apply Diff") && selectedDiff != null && targetMesh != null)
        {
            VRClothDiffApplier.ApplyDiff(selectedDiff, targetMesh);
        }
    }
}
