using NUnit.Framework;
using UnityEngine;

namespace VRClothDeclipper.Tests
{
    /// <summary>
    /// The bone-name thesaurus resolves a garment bone's name to a Humanoid bone
    /// across rig naming conventions (so 衣装採寸 can re-bind onto a body whose bones
    /// are named differently). Mirrors the limb/torso subset of MA's HeuristicBoneMapper.
    /// </summary>
    public class BoneNameThesaurusTests
    {
        [Test]
        public void Resolve_KnownAliases_MapToHumanoidBone()
        {
            AssertBone("Hips", HumanBodyBones.Hips);
            AssertBone("Foot_L", HumanBodyBones.LeftFoot);
            AssertBone("Foot_R", HumanBodyBones.RightFoot);
            AssertBone("Thigh_L", HumanBodyBones.LeftUpperLeg);   // garment uses Thigh, body may use UpperLeg
            AssertBone("UpperLeg_R", HumanBodyBones.RightUpperLeg);
            AssertBone("shin.L", HumanBodyBones.LeftLowerLeg);
            AssertBone("LowerLeg_R", HumanBodyBones.RightLowerLeg);
            AssertBone("Bust", HumanBodyBones.Chest);   // digits are stripped, so "spine02" collides with "spine01"→Spine
            AssertBone("Spine", HumanBodyBones.Spine);
            AssertBone("LowerArm_L", HumanBodyBones.LeftLowerArm);
            AssertBone("Hand_R", HumanBodyBones.RightHand);
            AssertBone("Shoulder_L", HumanBodyBones.LeftShoulder);
        }

        [Test]
        public void Resolve_IsCaseAndSeparatorInsensitive()
        {
            // "Foot_L", "foot.l", "FOOT L" must collapse to the same bone.
            Assert.IsTrue(BoneNameThesaurus.TryResolve("foot.l", out var a));
            Assert.IsTrue(BoneNameThesaurus.TryResolve("FOOT L", out var b));
            Assert.IsTrue(BoneNameThesaurus.TryResolve("Foot_L", out var c));
            Assert.AreEqual(HumanBodyBones.LeftFoot, a);
            Assert.AreEqual(a, b);
            Assert.AreEqual(b, c);
        }

        [Test]
        public void Resolve_SideSwapVariant_Matches()
        {
            // HeuristicBoneMapper also registers the "L.Foot" form of "Foot_L".
            Assert.IsTrue(BoneNameThesaurus.TryResolve("L.Foot", out var bone));
            Assert.AreEqual(HumanBodyBones.LeftFoot, bone);
        }

        [Test]
        public void Resolve_UnknownNames_ReturnFalse()
        {
            // Accessories / non-skeletal bones are not in the thesaurus (→ pass 3 handles them).
            Assert.IsFalse(BoneNameThesaurus.TryResolve("Ribbon_01", out _));
            Assert.IsFalse(BoneNameThesaurus.TryResolve("Necklace", out _));
            Assert.IsFalse(BoneNameThesaurus.TryResolve("", out _));
        }

        static void AssertBone(string name, HumanBodyBones expected)
        {
            Assert.IsTrue(BoneNameThesaurus.TryResolve(name, out var bone), $"'{name}' should resolve");
            Assert.AreEqual(expected, bone, $"'{name}'");
        }
    }
}
