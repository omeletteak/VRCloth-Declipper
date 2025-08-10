using UnityEngine;
using System.Collections.Generic;

namespace VRClothFitter
{
    [System.Serializable]
    public class DeformationAnchorPair
    {
        [Tooltip("Name for reference in the editor.")]
        public string name;
        
        [Tooltip("Anchor transform on the Avatar.")]
        public Transform avatarAnchor;

        [Tooltip("Corresponding anchor transform on the Cloth.")]
        public Transform clothAnchor;
    }

    [System.Serializable]
    public class DeformationPreset
    {
        public string avatarName;
        public string clothName;
        public List<DeformationAnchorPair> anchorPairs = new List<DeformationAnchorPair>();
    }

    [AddComponentMenu("VRCloth Fitter/Deformation Data")]
    public class VRClothFitterDeformationData : MonoBehaviour
    {
        [Tooltip("The root of the avatar this cloth is intended for.")]
        public GameObject avatarRoot;

        [Tooltip("List of anchor pairs that define the mesh deformation.")]
        public List<DeformationAnchorPair> anchorPairs = new List<DeformationAnchorPair>();
    }
}
