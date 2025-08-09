using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class VRClothFitterWindow : EditorWindow
{
    private GameObject avatarObject;
    private GameObject clothObject;

    private Vector2 scrollPosition;

    private List<string> avatarBoneNames = new List<string>();
    private string[] avatarBoneNamesArray;
    private List<string> clothBoneNames = new List<string>();
    private int[] mappedBoneIndices;

    private const string NO_BONE_SELECTED = "[None]";

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
        EditorGUI.BeginChangeCheck();
        avatarObject = (GameObject)EditorGUILayout.ObjectField("Avatar", avatarObject, typeof(GameObject), true);
        clothObject = (GameObject)EditorGUILayout.ObjectField("Cloth", clothObject, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck())
        {
            UpdateBoneData();
        }

        EditorGUILayout.Space();

        // ボーンマッピングエリア
        GUILayout.Label("Bone Mapping", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Cloth Bone", EditorStyles.boldLabel);
        GUILayout.Label("->", GUILayout.Width(20));
        GUILayout.Label("Avatar Bone", EditorStyles.boldLabel);
        EditorGUILayout.EndHorizontal();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, EditorStyles.helpBox);
        {
            if (clothBoneNames.Count > 0 && avatarBoneNames.Count > 0)
            {
                for (int i = 0; i < clothBoneNames.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    {
                        // 衣装のボーン名を表示
                        EditorGUILayout.LabelField(clothBoneNames[i]);
                        GUILayout.Label("->", GUILayout.Width(20));
                        // アバターのボーンを選択するドロップダウン
                        mappedBoneIndices[i] = EditorGUILayout.Popup(mappedBoneIndices[i], avatarBoneNamesArray);
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
            else
            {
                GUILayout.Label("Please set Avatar and Cloth objects.");
            }
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();

        // 実行ボタン
        if (GUILayout.Button("Fit Cloth"))
        {
            FitCloth();
        }
    }

    private void UpdateBoneData()
    {
        // アバターのボーンリストを取得
        avatarBoneNames.Clear();
        if (avatarObject != null)
        {
            var renderer = avatarObject.GetComponentInChildren<SkinnedMeshRenderer>();
            if (renderer != null)
            {
                avatarBoneNames = renderer.bones.Select(b => b.name).ToList();
            }
        }
        avatarBoneNames.Insert(0, NO_BONE_SELECTED); // 未選択オプションを追加
        avatarBoneNamesArray = avatarBoneNames.ToArray();

        // 衣装のボーンリストを取得
        clothBoneNames.Clear();
        if (clothObject != null)
        {
            var renderer = clothObject.GetComponentInChildren<SkinnedMeshRenderer>();
            if (renderer != null)
            {
                clothBoneNames = renderer.bones.Select(b => b.name).ToList();
            }
        }

        // マッピングを初期化・自動設定
        mappedBoneIndices = new int[clothBoneNames.Count];
        for (int i = 0; i < clothBoneNames.Count; i++)
        {
            int foundIndex = avatarBoneNames.FindIndex(bName => bName == clothBoneNames[i]);
            mappedBoneIndices[i] = (foundIndex != -1) ? foundIndex : 0; // 見つからなければ[None]
        }
        
        Repaint();
    }

    private void FitCloth()
    {
        if (avatarObject == null || clothObject == null)
        {
            EditorUtility.DisplayDialog("Error", "AvatarとClothの両方を設定してください。", "OK");
            return;
        }

        var avatarRenderer = avatarObject.GetComponentInChildren<SkinnedMeshRenderer>();
        var clothRenderer = clothObject.GetComponentInChildren<SkinnedMeshRenderer>();

        if (avatarRenderer == null)
        {
            EditorUtility.DisplayDialog("Error", "AvatarにSkinnedMeshRendererが見つかりません。", "OK");
            return;
        }

        if (clothRenderer == null)
        {
            EditorUtility.DisplayDialog("Error", "ClothにSkinnedMeshRendererが見つかりません。", "OK");
            return;
        }

        // アバターのボーンを名前をキーにした辞書に変換
        var avatarBones = avatarRenderer.bones.ToDictionary(b => b.name, b => b);
        
        // UIのマッピング情報に基づいて新しいボーン配列を作成
        var newClothBones = new Transform[clothRenderer.bones.Length];
        bool allBonesFound = true;

        for (int i = 0; i < clothRenderer.bones.Length; i++)
        {
            int selectedIndex = mappedBoneIndices[i];
            if (selectedIndex > 0) // 0は[None]なので無視
            {
                string selectedBoneName = avatarBoneNamesArray[selectedIndex];
                if (avatarBones.TryGetValue(selectedBoneName, out Transform avatarBone))
                {
                    newClothBones[i] = avatarBone;
                }
            }
            else
            {
                Debug.LogWarning($"Clothのボーン '{clothBoneNames[i]}' に対応するAvatarのボーンが設定されていません。");
                newClothBones[i] = clothRenderer.bones[i]; // 元のボーンを維持
                allBonesFound = false;
            }
        }

        // 衣装のSkinnedMeshRendererに新しいボーン配列を設定
        clothRenderer.bones = newClothBones;

        // ルートボーンもアバターのものに合わせる
        var avatarRootBone = avatarRenderer.rootBone;
        if (avatarBones.ContainsKey(clothRenderer.rootBone.name))
        {
            avatarRootBone = avatarBones[clothRenderer.rootBone.name];
        }
        clothRenderer.rootBone = avatarRootBone;

        if (allBonesFound)
        {
            EditorUtility.DisplayDialog("Success", "衣装のボーンをアバターに合わせました。", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Warning", "いくつかのボーンが未設定です。処理は不完全かもしれません。詳細はConsoleを確認してください。", "OK");
        }
    }
}
