using UnityEngine;

namespace VRClothFitter
{
    /// <summary>
    /// The main component to be attached to a cloth GameObject.
    /// It holds the reference to the target avatar and acts as an entry point for the custom editor.
    /// </summary>
    [AddComponentMenu("VRCloth Fitter/VRCloth Fitter")]
    public class VRClothFitter : MonoBehaviour
    {
        [Tooltip("The avatar you want to fit the cloth to.")]
        public GameObject targetAvatarObject;
        [Tooltip("(Optional) The original avatar the cloth was made for. Providing this enables High-Precision mode.")]
        public GameObject sourceAvatarObject;
    }
}
