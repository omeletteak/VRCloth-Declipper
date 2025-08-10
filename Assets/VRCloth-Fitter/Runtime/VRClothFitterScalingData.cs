using UnityEngine;
using System.Collections.Generic;
using System;

namespace VRClothFitter
{
    [Serializable]
    public struct BoneScaleInfo
    {
        public string boneName;
        public Vector3 scale;
    }

    [AddComponentMenu("VRCloth Fitter/Scaling Data")]
    public class VRClothFitterScalingData : MonoBehaviour
    {
        public List<BoneScaleInfo> boneScales = new List<BoneScaleInfo>();
    }
}
