using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using VRClothFitter;

[CustomEditor(typeof(VRClothFitterDeformationData))]
public class VRClothFitterDeformationDataEditor : Editor
{
    private ReorderableList anchorList;
    private VRClothFitterDeformationData data;

    private void OnEnable()
    {
        data = (VRClothFitterDeformationData)target;
        
        anchorList = new ReorderableList(serializedObject, 
            serializedObject.FindProperty("anchorPairs"), 
            true, true, true, true);

        anchorList.drawHeaderCallback = (Rect rect) => { 
            EditorGUI.LabelField(rect, VRClothFitterLocalization.Tr("Anchor Points")); 
        };

        anchorList.elementHeightCallback = (index) => {
            return (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * 3;
        };

        anchorList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => { 
            var element = anchorList.serializedProperty.GetArrayElementAtIndex(index);
            rect.y += EditorGUIUtility.standardVerticalSpacing;
            rect.height = EditorGUIUtility.singleLineHeight;

            EditorGUI.PropertyField(
                new Rect(rect.x, rect.y, rect.width, rect.height),
                element.FindPropertyRelative("name"), new GUIContent(VRClothFitterLocalization.Tr("Name")));

            rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            EditorGUI.PropertyField(
                new Rect(rect.x, rect.y, rect.width, rect.height),
                element.FindPropertyRelative("clothAnchor"), new GUIContent(VRClothFitterLocalization.Tr("Cloth Anchor")));

            rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            EditorGUI.PropertyField(
                new Rect(rect.x, rect.y, rect.width, rect.height),
                element.FindPropertyRelative("avatarAnchor"), new GUIContent(VRClothFitterLocalization.Tr("Avatar Anchor")));
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        EditorGUILayout.PropertyField(serializedObject.FindProperty("avatarRoot"), new GUIContent(VRClothFitterLocalization.Tr("Avatar")));
        
        EditorGUILayout.Space();
        
        anchorList.DoLayoutList();
        
        serializedObject.ApplyModifiedProperties();
    }

    private void OnSceneGUI()
    {
        if (data == null || data.avatarRoot == null) return;

        for (int i = 0; i < data.anchorPairs.Count; i++)
        {
            var pair = data.anchorPairs[i];
            if (pair.clothAnchor == null || pair.avatarAnchor == null) continue;

            var clothWorldPos = pair.clothAnchor.position;
            var avatarWorldPos = pair.avatarAnchor.position;

            Handles.color = Color.green;
            Handles.SphereHandleCap(0, clothWorldPos, Quaternion.identity, 0.02f, EventType.Repaint);
            
            Handles.color = Color.blue;
            Handles.SphereHandleCap(0, avatarWorldPos, Quaternion.identity, 0.02f, EventType.Repaint);
            
            Handles.color = Color.yellow;
            Handles.DrawLine(avatarWorldPos, clothWorldPos);
        }
    }
}
