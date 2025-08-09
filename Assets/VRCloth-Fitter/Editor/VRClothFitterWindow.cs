using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using VRClothFitter;
// Modular Avatarのnamespaceを追加
using nadena.dev.modular_avatar.core;

public class VRClothFitterWindow : EditorWindow
{
    private GameObject avatarObject;
    private GameObject clothObject;

    private Vector2 scrollPositionBones;
    private Vector2 scrollPositionBlendshapes;

    // Bone mapping variables
    private List<string> avatarBoneNames = new List<string>();
    private string[] avatarBoneNamesArray;
    private List<string> clothBoneNames = new List<string>();
    private int[] mappedBoneIndices;

    // Blendshape mapping variables
    private List<string> avatarBlendshapeNames = new List<string>();
    private string[] avatarBlendshapeNamesArray;
    private List<string> clothBlendshapeNames = new List<string>();
    private int[] mappedBlendshapeIndices;

    private bool isPreviewing = false;
    private Dictionary<Transform, Vector3> originalBoneScales;

    private const string NO_BONE_SELECTED = "[None]";
    private const string NO_BLENDSHAPE_SELECTED = "[None]";

    [MenuItem("Tools/VRCloth Fitter")]
    public static void ShowWindow()
    {
        GetWindow<VRClothFitterWindow>("VRCloth Fitter");
    }

    private void OnDisable()
    {
        StopPreview();
    }

    private void OnGUI()
    {
        GUILayout.Label("VRCloth Fitter", EditorStyles.boldLabel);
        
        EditorGUILayout.Space();

        EditorGUI.BeginChangeCheck();
        avatarObject = (GameObject)EditorGUILayout.ObjectField("Avatar", avatarObject, typeof(GameObject), true);
        clothObject = (GameObject)EditorGUILayout.ObjectField("Cloth", clothObject, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck())
        {
            StopPreview();
            UpdateAllData();
        }

        EditorGUILayout.Space();

        // --- Bone Mapping UI ---
        GUILayout.Label("Bone Mapping", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Cloth Bone", EditorStyles.boldLabel);
        GUILayout.Label("->", GUILayout.Width(20));
        GUILayout.Label("Avatar Bone", EditorStyles.boldLabel);
        EditorGUILayout.EndHorizontal();

        scrollPositionBones = EditorGUILayout.BeginScrollView(scrollPositionBones, EditorStyles.helpBox, GUILayout.Height(150));
        {
            if (clothBoneNames.Count > 0 && avatarBoneNames.Count > 0)
            {
                for (int i = 0; i < clothBoneNames.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(clothBoneNames[i]);
                    GUILayout.Label("->", GUILayout.Width(20));
                    mappedBoneIndices[i] = EditorGUILayout.Popup(mappedBoneIndices[i], avatarBoneNamesArray);
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

        // --- Blendshape Sync UI ---
        GUILayout.Label("Blendshape Sync", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Cloth Blendshape", EditorStyles.boldLabel);
        GUILayout.Label("->", GUILayout.Width(20));
        GUILayout.Label("Avatar Blendshape", EditorStyles.boldLabel);
        EditorGUILayout.EndHorizontal();

        scrollPositionBlendshapes = EditorGUILayout.BeginScrollView(scrollPositionBlendshapes, EditorStyles.helpBox, GUILayout.Height(150));
        {
            if (clothBlendshapeNames.Count > 0 && avatarBlendshapeNames.Count > 0)
            {
                for (int i = 0; i < clothBlendshapeNames.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(clothBlendshapeNames[i]);
                    GUILayout.Label("->", GUILayout.Width(20));
                    mappedBlendshapeIndices[i] = EditorGUILayout.Popup(mappedBlendshapeIndices[i], avatarBlendshapeNamesArray);
                    EditorGUILayout.EndHorizontal();
                }
            }
            else
            {
                GUILayout.Label("No blendshapes found or objects not set.");
            }
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();

        // --- Action Buttons ---
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Fit Bones")) FitBones();
        if (GUILayout.Button("Calculate & Save Scale")) CalculateAndSaveScale();
        if (GUILayout.Button("Apply Blendshape Sync")) ApplyBlendshapeSync();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        GUI.color = isPreviewing ? Color.yellow : Color.white;
        if (GUILayout.Button(isPreviewing ? "Stop Preview" : "Toggle Preview")) TogglePreview();
        GUI.color = Color.white;
    }

    private void UpdateAllData()
    {
        // --- Bones ---
        avatarBoneNames.Clear();
        clothBoneNames.Clear();
        var avatarRenderer = avatarObject?.GetComponentInChildren<SkinnedMeshRenderer>();
        if (avatarRenderer != null)
        {
            avatarBoneNames = renderer.bones.Select(b => b.name).ToList();
        }
        avatarBoneNames.Insert(0, NO_BONE_SELECTED);
        avatarBoneNamesArray = avatarBoneNames.ToArray();

        var clothRenderer = clothObject?.GetComponentInChildren<SkinnedMeshRenderer>();
        if (clothRenderer != null)
        {
            clothBoneNames = clothRenderer.bones.Select(b => b.name).ToList();
        }
        mappedBoneIndices = new int[clothBoneNames.Count];
        for (int i = 0; i < clothBoneNames.Count; i++)
        {
            int foundIndex = avatarBoneNames.FindIndex(bName => bName == clothBoneNames[i]);
            mappedBoneIndices[i] = (foundIndex != -1) ? foundIndex : 0;
        }

        // --- Blendshapes ---
        avatarBlendshapeNames.Clear();
        clothBlendshapeNames.Clear();
        if (avatarRenderer?.sharedMesh != null)
        {
            for (int i = 0; i < avatarRenderer.sharedMesh.blendShapeCount; i++)
            {
                avatarBlendshapeNames.Add(avatarRenderer.sharedMesh.GetBlendShapeName(i));
            }
        }
        avatarBlendshapeNames.Insert(0, NO_BLENDSHAPE_SELECTED);
        avatarBlendshapeNamesArray = avatarBlendshapeNames.ToArray();

        if (clothRenderer?.sharedMesh != null)
        {
            for (int i = 0; i < clothRenderer.sharedMesh.blendShapeCount; i++)
            {
                clothBlendshapeNames.Add(clothRenderer.sharedMesh.GetBlendShapeName(i));
            }
        }
        mappedBlendshapeIndices = new int[clothBlendshapeNames.Count];
        for (int i = 0; i < clothBlendshapeNames.Count; i++)
        {
            int foundIndex = avatarBlendshapeNames.FindIndex(bName => bName == clothBlendshapeNames[i]);
            mappedBlendshapeIndices[i] = (foundIndex != -1) ? foundIndex : 0;
        }
        
        Repaint();
    }

    private void ApplyBlendshapeSync()
    {
        if (clothObject == null) return;

        var syncComponent = clothObject.GetComponent<ModularAvatarBlendshapeSync>();
        if (syncComponent == null)
        {
            syncComponent = Undo.AddComponent<ModularAvatarBlendshapeSync>(clothObject);
        }
        Undo.RecordObject(syncComponent, "Apply Blendshape Sync");

        syncComponent.syncs.Clear();

        for (int i = 0; i < clothBlendshapeNames.Count; i++)
        {
            int selectedIndex = mappedBlendshapeIndices[i];
            if (selectedIndex > 0) // 0 is [None]
            {
                string clothBsName = clothBlendshapeNames[i];
                string avatarBsName = avatarBlendshapeNamesArray[selectedIndex];
                
                syncComponent.syncs.Add(new BlendshapeBinding
                {
                    sourceBlendshape = avatarBsName,
                    targetBlendshape = clothBsName
                });
            }
        }
        
        EditorUtility.DisplayDialog("Success", $"Applied {syncComponent.syncs.Count} blendshape sync rules.", "OK");
    }

    // ... (The rest of the methods: GetRenderers, FitBones, CalculateScale, etc. remain the same)
    // ... (To save space, I'm omitting the unchanged methods from this diff)
}
