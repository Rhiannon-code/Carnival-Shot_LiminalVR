using System;
using UnityEngine;
using Liminal.SDK.VR;
using Liminal.SDK.VR.Avatars;
using Liminal.SDK.VR.Avatars.Controllers;
using Liminal.SDK.VR.Input;
using Liminal.SDK.VR.Pointers;
using IntuitiveDesigns.CrystalCatch;

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

            /// The gun this hand picks up. Its ghost stays until that gun is in hand; empty falls back
            /// to the game state, ghosts until both guns are up
            public Pistol pistol;

            /// Off when the armed visual already rides something that moves, such as a glove parented
            /// to the pistol it is gripping
            public bool followArmed;
            public Vector3 localPosition;
            public Vector3 localEuler;

            [NonSerialized] public bool wasArmed;
            [NonSerialized] public GloveFingers ghostFingers;
            [NonSerialized] public GloveFingers armedFingers;
            [NonSerialized] public PistolPickup pickup;
            [NonSerialized] public bool onGlove;
            [NonSerialized] public Vector3 gloveOffset;
            [NonSerialized] public Quaternion gloveTurn;
            [NonSerialized] public PowerUpTint tint;
            [NonSerialized] public bool snapped;
            [NonSerialized] public float snap;
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

        [Header("Ghost hand onto its gun (data)")]
        [SerializeField] private float snapSeconds = 0.1f;
        [SerializeField, Range(0f, 1f)] private float snapHaptic = 0.3f;
        [SerializeField] private float snapHapticSeconds = 0.04f;
        [SerializeField, Range(0f, 1f)] private float waitingGlow = 0.35f;

        [Header("The SDK's controller models and laser pointer, which the gloves replace")]
        [SerializeField] private bool hideControllers = true;

        private float _nextSweep;
        private int _hiddenControllers;
        private IVRDevice _listeningTo;

        private void Start()
        {
            for (int i = 0; i < hands.Length; i++)
            {
                var hand = hands[i];
                if (hand == null) continue;

                hand.ghostFingers = hand.ghost != null ? hand.ghost.GetComponent<GloveFingers>() : null;
                hand.armedFingers = hand.armed != null ? hand.armed.GetComponent<GloveFingers>() : null;
                hand.pickup = hand.pistol != null ? hand.pistol.GetComponent<PistolPickup>() : null;
                hand.tint = hand.pistol != null ? hand.pistol.GetComponent<PowerUpTint>() : null;

                // Measured off the gun on the bench, before anything has moved it
                hand.onGlove = hand.pistol != null && hand.armed != null;
                if (!hand.onGlove) continue;

                var gun = hand.pistol.transform;
                hand.gloveOffset = Quaternion.Inverse(gun.rotation) * (hand.armed.position - gun.position);
                hand.gloveTurn = Quaternion.Inverse(gun.rotation) * hand.armed.rotation;
            }
        }

        private void LateUpdate()
        {
            bool waiting = game == null || game.Current == RangeGame.State.HowToPlay ||
                           game.Current == RangeGame.State.WaitingForPickup;

            for (int i = 0; i < hands.Length; i++)
            {
                var hand = hands[i];
                if (hand == null) continue;

                Place(hand, hand.pistol != null ? !hand.pistol.IsHeld : waiting);
            }

            HideControllers();
        }

        private void Place(Hand hand, bool empty)
        {
            Show(hand.ghost, empty);
            Show(hand.armed, !empty);

            if (!empty && !hand.wasArmed) PhaseIn(hand);
            hand.wasArmed = !empty;

            var rig = Rig(hand.limb);
            if (rig == null || rig.Transform == null) return;

            if (empty)
            {
                bool over = hand.pickup != null &&
                            Vector3.Distance(rig.Transform.position, hand.pistol.GripPosition) <= hand.pickup.GrabRadius;
                Snap(hand, over);

                if (hand.onGlove) Haunt(hand, rig.Transform);
                else Follow(hand.ghost, rig.Transform, hand);
                Close(hand, rig, over);

                // The glow says "take me", so it goes once the hand has hold of the gun
                if (hand.tint != null) hand.tint.Beckon(waitingGlow * (1f - hand.snap));
            }
            else if (hand.followArmed)
            {
                Follow(hand.armed, rig.Transform, hand);
            }
        }

        /// In reach of its gun the ghost is drawn onto it, already in the glove's grip, with a tick in
        /// the controller and the gun's glow fading, so the player knows a squeeze of the grip will take it
        private void Snap(Hand hand, bool over)
        {
            if (over && !hand.snapped && HapticPulse.Instance != null)
                HapticPulse.Instance.Pulse(hand.limb, snapHaptic, snapHapticSeconds);

            hand.snapped = over;
            hand.snap = Mathf.MoveTowards(hand.snap, over ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, snapSeconds));
        }

        /// Away from the gun the grip makes a fist; on it the fingers fold into the very grip the glove has
        private static void Close(Hand hand, IVRAvatarHand rig, bool over)
        {
            if (hand.ghostFingers == null) return;

            var device = rig.InputDevice;
            bool grip = device != null && device.GetButton(PistolPickup.GripButton);
            bool trigger = device != null && device.GetButton(VRButton.Trigger);

            hand.ghostFingers.Hold(grip || over ? 1f : 0f, trigger ? 1f : 0f, over ? hand.armedFingers : null);
        }

        private static void PhaseIn(Hand hand)
        {
            if (hand.tint != null) hand.tint.Shimmer();
        }

        /// The SDK spawns its controller models and laser when a controller connects, which can be
        /// after the first frame, so this looks again once a second until both are gone, and again
        /// whenever a controller reconnects. Only active ones are found
        private void HideControllers()
        {
            if (!hideControllers) return;

            // The device can come up after this does, so it is listened to as soon as there is one
            if (_listeningTo == null && VRDevice.Device != null)
            {
                _listeningTo = VRDevice.Device;
                _listeningTo.InputDeviceConnected += OnControllerConnected;
            }

            if (_hiddenControllers >= 2 || Time.unscaledTime < _nextSweep) return;
            _nextSweep = Time.unscaledTime + 1f;

            foreach (var visual in FindObjectsOfType<VRControllerVisual>())
            {
                visual.gameObject.SetActive(false);
                _hiddenControllers++;
            }
            foreach (var pointer in FindObjectsOfType<BasePointerVisual>()) pointer.gameObject.SetActive(false);
        }

        private void OnControllerConnected(IVRDevice device, IVRInputDevice input)
        {
            _hiddenControllers = 0;
        }

        private void OnDestroy()
        {
            if (_listeningTo != null) _listeningTo.InputDeviceConnected -= OnControllerConnected;
        }

        /// The ghost sits where its glove will be once the gun is in this hand, and slides onto the
        /// floating gun's grip as the hand comes in reach
        private static void Haunt(Hand hand, Transform rig)
        {
            if (hand.ghost == null) return;

            Vector3 position;
            Quaternion rotation;
            hand.pistol.HeldAt(rig, out position, out rotation);

            var gun = hand.pistol.transform;
            float t = Mathf.SmoothStep(0f, 1f, hand.snap);
            hand.ghost.position = Vector3.Lerp(position + rotation * hand.gloveOffset,
                                               gun.position + gun.rotation * hand.gloveOffset, t);
            hand.ghost.rotation = Quaternion.Slerp(rotation * hand.gloveTurn, gun.rotation * hand.gloveTurn, t);
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
