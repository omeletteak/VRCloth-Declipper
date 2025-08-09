using UnityEditor;
using UnityEngine;
using System.Linq;

public class VRClothFitterWindow : EditorWindow
{
    private GameObject avatarObject;
    private GameObject clothObject;

    [MenuItem("Tools/VRCloth Fitter")]
    public static void ShowWindow()
    {
        GetWindow<VRClothFitterWindow>("VRCloth Fitter");
    }

    private void OnGUI()
    {
        GUILayout.Label("VRCloth Fitter", EditorStyles.boldLabel);
        
        EditorGUILayout.Space();

        // アバターと衣装の設定
        avatarObject = (GameObject)EditorGUILayout.ObjectField("Avatar", avatarObject, typeof(GameObject), true);
        clothObject = (GameObject)EditorGUILayout.ObjectField("Cloth", clothObject, typeof(GameObject), true);

        EditorGUILayout.Space();

        // 実行ボタン
        if (GUILayout.Button("Fit Cloth"))
        {
            FitCloth();
        }
    }

    private void FitCloth()
    {
        if (avatarObject == null || clothObject == null)
        {
            Debug.LogError("AvatarとClothの両方を設定してください。");
            return;
        }

        var avatarRenderer = avatarObject.GetComponentInChildren<SkinnedMeshRenderer>();
        var clothRenderer = clothObject.GetComponentInChildren<SkinnedMeshRenderer>();

        if (avatarRenderer == null)
        {
            Debug.LogError("AvatarにSkinnedMeshRendererが見つかりません。");
            return;
        }

        if (clothRenderer == null)
        {
            Debug.LogError("ClothにSkinnedMeshRendererが見つかりません。");
            return;
        }

        var avatarBones = avatarRenderer.bones.ToDictionary(b => b.name, b => b);
        var clothBones = clothRenderer.bones.ToDictionary(b => b.name, b => b);

        Debug.Log("--- Avatar Bones ---");
        foreach (var bone in avatarBones.Keys)
        {
            Debug.Log(bone);
        }

        Debug.Log("--- Cloth Bones ---");
        foreach (var bone in clothBones.Keys)
        {
            Debug.Log(bone);
        }
    }
}
