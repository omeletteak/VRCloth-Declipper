using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace VRClothDeclipper
{
    /// <summary>
    /// Maps a garment bone's name to a Humanoid bone even when the garment uses a
    /// different naming convention than the body (e.g. the garment's "Foot_L" vs a
    /// body whose foot bone has another name). With this, a garment bone can be
    /// re-bound onto the body's Humanoid bone via <c>Animator.GetBoneTransform</c> —
    /// regardless of the body bone's actual name — which is what lets 衣装採寸 align
    /// across different rigs (docs/MEASUREMENT_SPEC.md §4).
    ///
    /// The alias table is the same heuristic Modular Avatar's HeuristicBoneMapper
    /// uses, trimmed to the limb/torso bones the proxy capsules cover (fingers are
    /// not measured). Alias data originally from
    /// https://github.com/HhotateA/AvatarModifyTools (c) 2021 @HhotateA_xR and
    /// https://github.com/Azukimochi/BoneRenamer (c) 2023 Azukimochi — both MIT,
    /// compatible with this project's MIT license.
    /// </summary>
    public static class BoneNameThesaurus
    {
        // Canonical Humanoid bone + its known aliases across rig conventions.
        static readonly (HumanBodyBones bone, string[] names)[] Patterns =
        {
            (HumanBodyBones.Hips, new[] { "Hips", "Hip", "pelvis" }),
            (HumanBodyBones.Spine, new[] { "Spine", "spine01" }),
            (HumanBodyBones.Chest, new[] { "Chest", "Bust", "spine02", "upper_chest" }),
            (HumanBodyBones.UpperChest, new[] { "UpperChest", "UChest" }),
            (HumanBodyBones.Neck, new[] { "Neck" }),
            (HumanBodyBones.Head, new[] { "Head" }),
            (HumanBodyBones.LeftShoulder, new[] { "LeftShoulder", "Shoulder_Left", "Shoulder_L" }),
            (HumanBodyBones.RightShoulder, new[] { "RightShoulder", "Shoulder_Right", "Shoulder_R" }),
            (HumanBodyBones.LeftUpperArm, new[] { "LeftUpperArm", "UpperArm_Left", "UpperArm_L", "Arm_Left", "Arm_L", "UArm_L", "Left arm", "UpperLeftArm" }),
            (HumanBodyBones.RightUpperArm, new[] { "RightUpperArm", "UpperArm_Right", "UpperArm_R", "Arm_Right", "Arm_R", "UArm_R", "Right arm", "UpperRightArm" }),
            (HumanBodyBones.LeftLowerArm, new[] { "LeftLowerArm", "LowerArm_Left", "LowerArm_L", "LArm_L", "Left elbow", "LeftForeArm", "Elbow_L", "forearm_L", "ForArm_L" }),
            (HumanBodyBones.RightLowerArm, new[] { "RightLowerArm", "LowerArm_Right", "LowerArm_R", "LArm_R", "Right elbow", "RightForeArm", "Elbow_R", "forearm_R", "ForArm_R" }),
            (HumanBodyBones.LeftHand, new[] { "LeftHand", "Hand_Left", "Hand_L", "Left wrist", "Wrist_L" }),
            (HumanBodyBones.RightHand, new[] { "RightHand", "Hand_Right", "Hand_R", "Right wrist", "Wrist_R" }),
            (HumanBodyBones.LeftUpperLeg, new[] { "LeftUpperLeg", "UpperLeg_Left", "UpperLeg_L", "Leg_Left", "Leg_L", "ULeg_L", "Left leg", "LeftUpLeg", "UpLeg.L", "Thigh_L" }),
            (HumanBodyBones.RightUpperLeg, new[] { "RightUpperLeg", "UpperLeg_Right", "UpperLeg_R", "Leg_Right", "Leg_R", "ULeg_R", "Right leg", "RightUpLeg", "UpLeg.R", "Thigh_R" }),
            (HumanBodyBones.LeftLowerLeg, new[] { "LeftLowerLeg", "LowerLeg_Left", "LowerLeg_L", "Knee_Left", "Knee_L", "LLeg_L", "Left knee", "LeftLeg", "leg_L", "shin.L" }),
            (HumanBodyBones.RightLowerLeg, new[] { "RightLowerLeg", "LowerLeg_Right", "LowerLeg_R", "Knee_Right", "Knee_R", "LLeg_R", "Right knee", "RightLeg", "leg_R", "shin.R" }),
            (HumanBodyBones.LeftFoot, new[] { "LeftFoot", "Foot_Left", "Foot_L", "Ankle_L", "Foot.L.001", "Left ankle", "heel.L" }),
            (HumanBodyBones.RightFoot, new[] { "RightFoot", "Foot_Right", "Foot_R", "Ankle_R", "Foot.R.001", "Right ankle", "heel.R" }),
            (HumanBodyBones.LeftToes, new[] { "LeftToes", "Toes_Left", "Toe_Left", "ToeIK_L", "Toes_L", "Toe_L", "Foot.L.002", "Left Toe", "LeftToeBase" }),
            (HumanBodyBones.RightToes, new[] { "RightToes", "Toes_Right", "Toe_Right", "ToeIK_R", "Toes_R", "Toe_R", "Foot.R.002", "Right Toe", "RightToeBase" }),
        };

        static readonly Dictionary<string, HumanBodyBones> NameToBone;

        static BoneNameThesaurus()
        {
            NameToBone = new Dictionary<string, HumanBodyBones>();
            foreach (var (bone, names) in Patterns)
            {
                foreach (var name in names)
                {
                    // Register the alias and its "X.Side" variant (e.g. Foot_L → L.Foot),
                    // matching HeuristicBoneMapper's side-swap, after normalization.
                    Register(Normalize(name), bone);
                    Match side = Regex.Match(name, @"[_\.]([LR])$");
                    if (side.Success)
                    {
                        string alt = side.Groups[1].Value + "." + name.Substring(0, name.Length - 2);
                        Register(Normalize(alt), bone);
                    }
                }
            }
        }

        static void Register(string key, HumanBodyBones bone)
        {
            if (!string.IsNullOrEmpty(key) && !NameToBone.ContainsKey(key))
            {
                NameToBone[key] = bone;
            }
        }

        /// <summary>Lower-cases and strips digits, spaces and separators, so "Foot_L",
        /// "foot.l" and "Foot L" collapse to the same key (matches MA's NormalizeName).</summary>
        public static string Normalize(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "";
            }
            name = name.ToLowerInvariant();
            name = Regex.Replace(name, "^bone_|[0-9 ._]", "");
            return name;
        }

        /// <summary>Resolves a bone name (any rig convention) to a Humanoid bone.
        /// Returns false for names not in the limb/torso thesaurus (fingers, accessories).</summary>
        public static bool TryResolve(string boneName, out HumanBodyBones bone)
        {
            return NameToBone.TryGetValue(Normalize(boneName), out bone);
        }
    }
}
