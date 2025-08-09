using UnityEditor;
using UnityEngine;
using VRClothFitter;

[CustomEditor(typeof(VRClothFitterDeformationData))]
public class VRClothFitterDeformationDataEditor : Editor
{
    private static bool isEditMode = false;
    private static VRClothFitterDeformationData currentTarget;

    // Temporary colliders for raycasting
    private static MeshCollider avatarCollider;
    private static MeshCollider clothCollider;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        string buttonText = isEditMode ? "Exit Anchor Edit Mode" : "Enter Anchor Edit Mode";
        GUI.color = isEditMode ? Color.yellow : Color.white;

        if (GUILayout.Button(buttonText))
        {
            isEditMode = !isEditMode;
            if (isEditMode)
            {
                currentTarget = (VRClothFitterDeformationData)target;
                SetupEditMode();
            }
            else
            {
                TeardownEditMode();
            }
        }
        GUI.color = Color.white;

        if (isEditMode)
        {
            EditorGUILayout.HelpBox("Anchor Edit Mode is active. Click on the Avatar and Cloth meshes in the Scene View to place anchors.", MessageType.Info);
        }
    }

    private void OnDisable()
    {
        TeardownEditMode();
    }

    private void SetupEditMode()
    {
        var data = (VRClothFitterDeformationData)target;
        if (data.avatarRoot == null)
        {
            Debug.LogError("Avatar Root is not set in the component.");
            isEditMode = false;
            return;
        }

        // Add temporary colliders for raycasting
        avatarCollider = AddTempCollider(data.avatarRoot);
        clothCollider = AddTempCollider(data.gameObject);

        SceneView.RepaintAll();
    }

    private void TeardownEditMode()
    {
        if (avatarCollider != null) DestroyImmediate(avatarCollider);
        if (clothCollider != null) DestroyImmediate(clothCollider);
        isEditMode = false;
        currentTarget = null;
        SceneView.RepaintAll();
    }

    private MeshCollider AddTempCollider(GameObject obj)
    {
        var renderer = obj.GetComponentInChildren<SkinnedMeshRenderer>();
        if (renderer == null) return null;

        var collider = renderer.gameObject.AddComponent<MeshCollider>();
        collider.sharedMesh = renderer.sharedMesh;
        collider.hideFlags = HideFlags.HideAndDontSave; // This is a temporary component
        return collider;
    }

    private void OnSceneGUI()
    {
        if (!isEditMode || currentTarget != (VRClothFitterDeformationData)target) return;

        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            
            if (clothCollider != null && clothCollider.Raycast(ray, out RaycastHit clothHit, 100f))
            {
                Vector3 localPos = clothCollider.transform.InverseTransformPoint(clothHit.point);
                Debug.Log($"[Cloth] Hit at local position: {localPos}");
                // Anchor creation logic will go here
            }
            else if (avatarCollider != null && avatarCollider.Raycast(ray, out RaycastHit avatarHit, 100f))
            {
                Vector3 localPos = avatarCollider.transform.InverseTransformPoint(avatarHit.point);
                Debug.Log($"[Avatar] Hit at local position: {localPos}");
                // Anchor creation logic will go here
            }
            
            Event.current.Use();
        }

        if (Event.current.type == EventType.Repaint)
        {
            // Anchor drawing logic will go here
        }
    }
}
