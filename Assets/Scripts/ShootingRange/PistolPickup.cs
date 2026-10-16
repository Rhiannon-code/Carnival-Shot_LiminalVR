using System.Collections.Generic;
using UnityEngine;
using Liminal.SDK.VR.Avatars;
using Liminal.SDK.VR.Input;
using IntuitiveDesigns.CrystalCatch;

namespace IntuitiveDesigns.ShootingRange
{
    [RequireComponent(typeof(Pistol))]
    public class PistolPickup : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private RangeGame game;
        [SerializeField] private Pistol pistol;

        [Header("Grab (data)")]
        [SerializeField] private bool onlyItsOwnHand = true;
        [SerializeField] private float grabRadius = 0.35f;
        [SerializeField] private bool requireProximity = true;
        [SerializeField] private float proximityGraceSeconds = 12f;
        [SerializeField] private float autoStartSeconds = 45f;
        [SerializeField, Range(0f, 1f)] private float takeHaptic = 0.6f;

        [Header("Floating in front of the player until taken (data)")]
        [SerializeField] private bool floatInFront = true;
        [SerializeField] private Transform showUnder;
        [SerializeField] private float belowDegrees = 12f;
        [SerializeField] private Vector3 floatOffset = new Vector3(0.2f, -0.4f, 0.45f);
        [SerializeField] private float anchorAfterSeconds = 0.5f;
        [SerializeField] private float bobHeight = 0.03f;
        [SerializeField] private float bobSeconds = 2.4f;
        [SerializeField] private float swayDegrees = 10f;
        [SerializeField] private float turnInDegrees = 30f;

        [Header("Desktop testing")]
        [SerializeField] private bool editorMouseGrab = true;

        /// The grip: VRButton.Three on the OVR path, but Three is thumbstick touch on UnityXR, where a
        /// resting thumb would fire it (ADR 0015)
#if UNITY_XR
        public static readonly string GripButton = VRButton.Seconday;
#else
        public static readonly string GripButton = VRButton.Three;
#endif

        public float GrabRadius { get { return grabRadius; } }
        public bool Taken { get; private set; }
        public VRAvatarLimbType HeldBy { get; private set; }

        private static readonly List<PistolPickup> Registry = new List<PistolPickup>();

        /// Every gun on the table is in hand. One pistol down is not a start
        public static bool AllTaken
        {
            get
            {
                for (int i = 0; i < Registry.Count; i++)
                {
                    if (!Registry[i].Taken) return false;
                }

                return Registry.Count > 0;
            }
        }

        private float _waited;
        private bool _anchored;
        private Vector3 _anchor;
        private Quaternion _facing = Quaternion.identity;
        private Vector3 _ahead;

        private void Awake()
        {
            if (pistol == null) pistol = GetComponent<Pistol>();

            // Registered for life, not while enabled: Take() disables this and the count still has to
            // know about a gun that has already gone
            Registry.Add(this);
        }

        private void OnDestroy()
        {
            Registry.Remove(this);
        }

        private void Start()
        {
            if (pistol != null) pistol.SetHeld(false);
        }

        private void Update()
        {
            if (Taken) return;

            _waited += Time.deltaTime;
            if (floatInFront) Float();

            var avatar = VRAvatar.Active;
            if (avatar != null)
            {
                if (TryHand(avatar.PrimaryHand)) return;
                if (TryHand(avatar.SecondaryHand)) return;
            }

            if (editorMouseGrab && Application.isEditor && Input.GetMouseButtonDown(0))
            {
                Take(pistol != null ? pistol.Hand : VRAvatarLimbType.RightHand, "editor mouse");
                return;
            }

            if (autoStartSeconds > 0f && _waited >= autoStartSeconds)
                Take(pistol != null ? pistol.Hand : VRAvatarLimbType.RightHand, "auto start");
        }

        /// Hovers at chest height in front of the player, off to its own hand's side, bobbing so it
        /// reads as something to take. The two guns bob out of step
        private void Float()
        {
            if (!_anchored && !Anchor()) return;

            float side = pistol != null && pistol.Hand == VRAvatarLimbType.LeftHand ? -1f : 1f;
            float t = Time.unscaledTime / Mathf.Max(0.1f, bobSeconds) * 2f * Mathf.PI + (side < 0f ? Mathf.PI * 0.5f : 0f);

            var offset = new Vector3(floatOffset.x * side, bobHeight * Mathf.Sin(t), 0f);
            transform.position = _anchor + _ahead + _facing * offset;
            // Angled in toward the other gun, so the player sees them side on and knows them for pistols
            float yaw = -side * turnInDegrees + swayDegrees * Mathf.Sin(t * 0.5f);
            transform.rotation = _facing * Quaternion.Euler(0f, yaw, 0f);
        }

        /// Set once, from where the head is just after the start. Following the head instead would
        /// pull the gun away as the player leans in to take it
        private bool Anchor()
        {
            if (_waited < anchorAfterSeconds) return false;

            var avatar = VRAvatar.Active;
            var head = avatar != null && avatar.Head != null ? avatar.Head.Transform : null;
            if (head == null) return false;

            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            _facing = forward.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(forward, Vector3.up) : Quaternion.identity;
            _anchor = head.position;

            // Along the line from the eye to the prompt, tipped down under it, so the guns sit just
            // below the words whatever the player's height
            if (showUnder != null)
            {
                var below = Quaternion.AngleAxis(belowDegrees, _facing * Vector3.right);
                _ahead = below * (showUnder.position - _anchor).normalized * floatOffset.z;
            }
            else
            {
                _ahead = _facing * new Vector3(0f, floatOffset.y, floatOffset.z);
            }

            _anchored = true;
            return true;
        }

        private bool TryHand(IVRAvatarHand rig)
        {
            if (rig == null || rig.Transform == null) return false;
            if (onlyItsOwnHand && pistol != null && rig.LimbType != pistol.Hand) return false;

            bool near = !requireProximity ||
                        Vector3.Distance(rig.Transform.position, transform.position) <= grabRadius;

            // Reaching in is only required for a while. After that a press is a press, wherever the
            // hand is
            if (!near && _waited < proximityGraceSeconds) return false;

            // Squeezing the grip takes it, the way a hand closes on a real gun
            var device = rig.InputDevice;
            if (device == null || !device.GetButtonDown(GripButton)) return false;

            Take(rig.LimbType, "grip");
            return true;
        }

        private void Take(VRAvatarLimbType limb, string how)
        {
            if (Taken) return;

            Taken = true;
            HeldBy = limb;

            if (pistol != null)
            {
                pistol.AssignHand(limb);
                pistol.SetHeld(true);
            }

            if (HapticPulse.Instance != null) HapticPulse.Instance.Hit(limb, takeHaptic);

            bool ready = AllTaken;
            if (game == null) game = RangeGame.Instance;
            if (game == null) Debug.LogError("[PistolPickup] No RangeGame to start, in the scene or on " + name + ".", this);
            else if (ready) game.BeginFromPickup();

            if (Debug.isDebugBuild)
                Debug.Log("[PistolPickup] " + name + " taken in the " + limb + " by " + how + " after " +
                          _waited.ToString("0.0") + " s. " +
                          (ready ? "Both guns are up, starting." : "Waiting on the other gun."));

            enabled = false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Taken ? Color.grey : Color.cyan;
            Gizmos.DrawWireSphere(transform.position, grabRadius);
        }
#endif
    }
}
