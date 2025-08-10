using UnityEditor;
using UnityEngine;
using nadena.dev.modular_avatar.core;

namespace VRClothFitter
{
    public class VRClothFitterSetup
    {
        private const string MenuPath = "Assets/VRCloth-Fitter/Setup Outfit";

        [MenuItem(MenuPath, true)]
        private static bool ValidateSetupOutfit()
        {
            // 選択されているオブジェクトがGameObject（プレハブなど）であるかを確認
            return Selection.activeObject is GameObject;
        }

        [MenuItem(MenuPath, false, 1000)]
        private static void SetupOutfit()
        {
            GameObject selectedObject = Selection.activeObject as GameObject;

            if (selectedObject == null)
            {
                EditorUtility.DisplayDialog("Error", "Please select a cloth prefab from the Project window.", "OK");
                return;
            }

            // プレハブアセットのパスを取得
            string path = AssetDatabase.GetAssetPath(selectedObject);
            if (string.IsNullOrEmpty(path) || !AssetDatabase.IsMainAsset(selectedObject))
            {
                 EditorUtility.DisplayDialog("Error", "This menu works with Prefab assets. Please select a prefab in the Project window.", "OK");
                return;
            }

            // プレハブを編集モードで開く
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);

            // VRClothFitterコンポーネントを追加
            if (prefabRoot.GetComponent<VRClothFitter>() == null)
            {
                Undo.AddComponent<VRClothFitter>(prefabRoot);
                Debug.Log("Added VRClothFitter component.", prefabRoot);
            }
            
            // 変更をプレハブに保存
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
            // 編集モードを終了
            PrefabUtility.UnloadPrefabContents(prefabRoot);

            EditorUtility.DisplayDialog("Success", $"'{selectedObject.name}' has been set up for VRCloth-Fitter.", "OK");
        }
    }
}
