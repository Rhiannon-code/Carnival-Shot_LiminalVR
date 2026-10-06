using System;
using UnityEngine;
using Liminal.SDK.VR.Avatars;

namespace IntuitiveDesigns.ShootingRange
{
    public class HandVisuals : MonoBehaviour
    {
        [Serializable]
        public class Hand
        {
            public VRAvatarLimbType limb = VRAvatarLimbType.LeftHand;
            public Transform ghost;
            public Transform armed;

            /// Off when the armed visual already rides something that moves, such as a glove parented
            /// to the pistol it is gripping
            public bool followArmed;
            public Vector3 localPosition;
            public Vector3 localEuler;
        }

        [Header("Refs")]
        [SerializeField] private RangeGame game;

        [Header("Hands (armed may be empty until the gloves exist)")]
        [SerializeField]
        private Hand[] hands =
        {
            new Hand { limb = VRAvatarLimbType.LeftHand },
            new Hand { limb = VRAvatarLimbType.RightHand },
        };

        private void LateUpdate()
        {
            bool empty = game == null || game.Current == RangeGame.State.WaitingForPickup;

            for (int i = 0; i < hands.Length; i++)
            {
                Place(hands[i], empty);
            }
        }

        private void Place(Hand hand, bool empty)
        {
            if (hand == null) return;

            Show(hand.ghost, empty);
            Show(hand.armed, !empty);

            var rig = Rig(hand.limb);
            if (rig == null || rig.Transform == null) return;

            if (empty) Follow(hand.ghost, rig.Transform, hand);
            else if (hand.followArmed) Follow(hand.armed, rig.Transform, hand);
        }

        private static void Show(Transform t, bool on)
        {
            if (t != null && t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
        }

        private static void Follow(Transform t, Transform rig, Hand hand)
        {
            if (t == null) return;

            t.position = rig.TransformPoint(hand.localPosition);
            t.rotation = rig.rotation * Quaternion.Euler(hand.localEuler);
        }

        private static IVRAvatarHand Rig(VRAvatarLimbType limb)
        {
            var avatar = VRAvatar.Active;
            if (avatar == null) return null;

            if (avatar.PrimaryHand != null && avatar.PrimaryHand.LimbType == limb) return avatar.PrimaryHand;
            if (avatar.SecondaryHand != null && avatar.SecondaryHand.LimbType == limb) return avatar.SecondaryHand;

            return null;
        }
    }
}
