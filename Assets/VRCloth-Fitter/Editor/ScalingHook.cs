using UnityEngine;
using nadena.dev.modular_avatar.core;
using UnityEditor;

namespace VRClothFitter
{
    [DisallowMultipleComponent]
    internal class ScalingHook : MonoBehaviour, IEditorOnly
    {
        // This component is just a marker to trigger the hook.
    }

    [InitializeOnLoad]
    internal class ScalingHookProcessor : MergeArmatureHook
    {
        static ScalingHookProcessor()
        {
            // The static constructor is used to register the hook.
        }

        protected override void OnProcess(GameObject avatarGameObject, GameObject[] clothGameObjects)
        {
            foreach (var cloth in clothGameObjects)
            {
                if (cloth.GetComponent<ScalingHook>() == null) continue;

                var scalingData = cloth.GetComponent<VRClothFitterScalingData>();
                if (scalingData == null || scalingData.boneScales.Count == 0) continue;

                foreach (var boneScaleInfo in scalingData.boneScales)
                {
                    var bone = FindBone(cloth, boneScaleInfo.boneName);
                    if (bone != null)
                    {
                        bone.localScale = Vector3.Scale(bone.localScale, boneScaleInfo.scale);
                    }
                }
            }
        }

        private Transform FindBone(GameObject clothObject, string boneName)
        {
            var renderer = clothObject.GetComponent<SkinnedMeshRenderer>();
            if (renderer == null) return null;

            foreach (var bone in renderer.bones)
            {
                if (bone.name == boneName)
                {
                    return bone;
                }
            }
            return null;
        }
    }
}
