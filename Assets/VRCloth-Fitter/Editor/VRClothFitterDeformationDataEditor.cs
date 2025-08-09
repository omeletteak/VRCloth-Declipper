using UnityEditor;
using UnityEngine;
using VRClothFitter;

[CustomEditor(typeof(VRClothFitterDeformationData))]
public class VRClothFitterDeformationDataEditor : Editor
{
    private static bool isEditMode = false;
    private static VRClothFitterDeformationData currentTarget;

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
                // Ensure the scene view repaints to activate our custom GUI
                SceneView.RepaintAll(); 
            }
            else
            {
                currentTarget = null;
            }
        }
        GUI.color = Color.white;

        if (isEditMode)
        {
            EditorGUILayout.HelpBox("Anchor Edit Mode is active. Click on the Avatar and Cloth meshes in the Scene View to place anchors.", MessageType.Info);
        }
    }

    private void OnSceneGUI()
    {
        // Only run our scene GUI logic if we are in edit mode and editing this specific component.
        if (!isEditMode || currentTarget != (VRClothFitterDeformationData)target)
        {
            return;
        }

        // This line "consumes" mouse events in the scene view, preventing default selection/manipulation.
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        // The logic for raycasting and placing anchors will be added here in the next step.
        
        // Ensure the scene view keeps updating while in edit mode.
        if (Event.current.type == EventType.Repaint)
        {
            // Custom drawing for anchors will go here.
        }
    }
}
