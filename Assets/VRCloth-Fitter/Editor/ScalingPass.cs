using nadena.dev.ndmf;
using UnityEngine;
using VRClothFitter;

[assembly: ExportsPlugin(typeof(ScalingPassPlugin))]

namespace VRClothFitter
{
    public class ScalingPassPlugin : Plugin<ScalingPassPlugin>
    {
        public override string QualifiedName => "dev.omelette.vrcloth-fitter.scaling-pass";
        public override string DisplayName => "VRCloth Fitter Scaling Pass";

        protected override void Configure()
        {
            // This pass runs after Modular Avatar has merged the armatures,
            // ensuring we can find the bones on the final avatar structure.
            InPhase(BuildPhase.Transforming)
                .BeforePlugin("nadena.dev.modular-avatar")
                .Run("Apply bone scaling", ctx =>
                {
                    var scalingDataComponents = ctx.AvatarRootObject.GetComponentsInChildren<VRClothFitterScalingData>(true);

                    foreach (var data in scalingDataComponents)
                    {
                        // Find the root of the cloth's armature within the avatar
                        var clothRoot = data.transform;
                        
                        foreach (var boneInfo in data.boneScales)
                        {
                            // Find the bone transform by name within the cloth's hierarchy
                            var boneTransform = FindDeepChild(clothRoot, boneInfo.boneName);
                            if (boneTransform != null)
                            {
                                // Apply the stored scale
                                boneTransform.localScale = Vector3.Scale(boneTransform.localScale, boneInfo.scale);
                            }
                            else
                            {
                                Debug.LogWarning($"[VRCloth Fitter] Could not find bone '{boneInfo.boneName}' on {clothRoot.name} to apply scaling.");
                            }
                        }
                        
                        // Clean up the component after applying the data
                        Object.DestroyImmediate(data);
                    }
                });
        }
        
        // Helper to find a child transform by name, even if it's deeply nested.
        private static Transform FindDeepChild(Transform aParent, string aName)
        {
            Queue<Transform> queue = new Queue<Transform>();
            queue.Enqueue(aParent);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                if (c.name == aName)
                    return c;
                foreach(Transform t in c)
                    queue.Enqueue(t);
            }
            return null;
        }
    }
}
