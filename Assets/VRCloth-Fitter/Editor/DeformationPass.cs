using nadena.dev.ndmf;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[assembly: ExportsPlugin(typeof(VRClothFitter.DeformationPassPlugin))]

namespace VRClothFitter
{
    /// <summary>
    /// This NDMF Plugin runs during the avatar build process.
    /// It creates a new, deformed mesh asset based on the anchor data
    /// without modifying the original, ensuring a non-destructive workflow.
    /// </summary>
    public class DeformationPassPlugin : Plugin<DeformationPassPlugin>
    {
        public override string QualifiedName => "dev.omelette.vrcloth-fitter.deformation-pass";
        public override string DisplayName => "VRCloth Fitter Deformation Pass";

        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming)
                .AfterPlugin("nadena.dev.modular-avatar")
                .Run("Deform cloth mesh", ctx =>
                {
                    var deformationDataComponents = ctx.AvatarRootObject.GetComponentsInChildren<VRClothFitterDeformationData>(true);

                    foreach (var data in deformationDataComponents)
                    {
                        var renderer = data.GetComponent<SkinnedMeshRenderer>();
                        if (renderer == null || renderer.sharedMesh == null || data.anchorPairs.Count == 0)
                        {
                            continue;
                        }

                        var validAnchorPairs = data.anchorPairs.Where(p => p.clothAnchor != null && p.avatarAnchor != null).ToList();
                        if (validAnchorPairs.Count == 0) continue;

                        var originalMesh = renderer.sharedMesh;
                        var newMesh = Object.Instantiate(originalMesh);
                        newMesh.name = $"{originalMesh.name} (Deformed)";
                        
                        var vertices = originalMesh.vertices;
                        var normals = originalMesh.normals;
                        var newVertices = new Vector3[vertices.Length];
                        var newNormals = new Vector3[normals.Length];

                        var deformationMatrices = validAnchorPairs.Select(p => 
                            p.avatarAnchor.localToWorldMatrix * p.clothAnchor.worldToLocalMatrix
                        ).ToList();

                        for (int i = 0; i < vertices.Length; i++)
                        {
                            var clothTransform = renderer.transform;
                            var worldVertex = clothTransform.TransformPoint(vertices[i]);
                            
                            float totalWeight = 0f;
                            var avgTranslation = Vector3.zero;
                            var avgRotation = new Quaternion(0, 0, 0, 0);
                            var avgScale = Vector3.zero;

                            for (int j = 0; j < validAnchorPairs.Count; j++)
                            {
                                var clothAnchorWorldPos = validAnchorPairs[j].clothAnchor.position;
                                float dist = Vector3.Distance(worldVertex, clothAnchorWorldPos);
                                float weight = 1.0f / (dist * dist + 0.0001f);

                                var matrix = deformationMatrices[j];
                                avgTranslation += (Vector3)matrix.GetColumn(3) * weight;
                                avgRotation.x += matrix.rotation.x * weight;
                                avgRotation.y += matrix.rotation.y * weight;
                                avgRotation.z += matrix.rotation.z * weight;
                                avgRotation.w += matrix.rotation.w * weight;
                                avgScale += matrix.lossyScale * weight;
                                
                                totalWeight += weight;
                            }

                            if (totalWeight > 0)
                            {
                                avgTranslation /= totalWeight;
                                avgRotation.x /= totalWeight;
                                avgRotation.y /= totalWeight;
                                avgRotation.z /= totalWeight;
                                avgRotation.w /= totalWeight;
                                avgScale /= totalWeight;

                                var finalMatrix = Matrix4x4.TRS(avgTranslation, avgRotation.normalized, avgScale);
                                newVertices[i] = finalMatrix.MultiplyPoint3x4(vertices[i]);
                                newNormals[i] = finalMatrix.inverse.transpose.MultiplyVector(normals[i]).normalized;
                            }
                            else
                            {
                                newVertices[i] = vertices[i];
                                newNormals[i] = normals[i];
                            }
                        }

                        newMesh.vertices = newVertices;
                        newMesh.normals = newNormals;
                        newMesh.RecalculateBounds();
                        newMesh.RecalculateTangents();
                        
                        AssetDatabase.AddObjectToAsset(newMesh, ctx.AssetContainer);
                        renderer.sharedMesh = newMesh;
                        Object.DestroyImmediate(data);
                    }
                });
        }
    }
}
