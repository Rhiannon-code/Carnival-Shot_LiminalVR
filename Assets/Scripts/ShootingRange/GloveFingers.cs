using System;
using UnityEngine;
using Liminal.SDK.VR.Avatars;

namespace IntuitiveDesigns.ShootingRange
{
    /// Curls a SteamVR glove's fingers. On a pistol it grips, pulls the trigger as the player does and
    /// loosens through the reload. With no pistol it is a ghost hand: it idles, closes when the grip
    /// is held, and folds into the gun glove's own grip as it reaches the gun
    public class GloveFingers : MonoBehaviour
    {
        public enum Finger { Thumb, Index, Middle, Ring, Pinky }

        [Serializable]
        public class Joint
        {
            public Transform bone;
            public Finger finger;
            public Quaternion rest;
            public Vector3 axis;

            /// This joint's share of the finger's curl. A trigger finger bends little at the knuckle and
            /// most at the middle joint; 1 everywhere bends a finger evenly
            public float weight = 1f;
        }

        [Header("Measured by setup step 16")]
        [SerializeField] private Joint[] joints;

        [Header("Grip, degrees per joint (tweak live in Play mode)")]
        [SerializeField] private float thumb = 30f;
        [SerializeField] private float index = 25f;
        [SerializeField] private float middle = 70f;
        [SerializeField] private float ring = 75f;
        [SerializeField] private float pinky = 80f;

        [Header("On a pistol (empty = idle)")]
        [SerializeField] private Pistol pistol;
        [SerializeField] private float triggerSqueeze = 25f;
        [SerializeField] private float squeezeHold = 0.05f;
        [SerializeField] private float squeezeEase = 0.035f;
        [SerializeField] private float reloadLoosen = 30f;
        [SerializeField] private float releasePress = 30f;
        [SerializeField, Range(0f, 1f)] private float twirlLoose = 0.3f;
        [SerializeField] private float twirlHook = 30f;
        [SerializeField] private float regripLag = 0.025f;

        [Header("Idle, for the ghost hands")]
        [SerializeField] private float idleSway = 6f;
        [SerializeField] private float idleSeconds = 3f;
        [SerializeField] private float fist = 55f;
        [SerializeField] private float closeSeconds = 0.12f;

        private float _sinceShot = float.MaxValue;
        private float _pull;
        private float _pullVelocity;
        private float _close;
        private float _closeTarget;
        private float _trigger;
        private float _triggerTarget;
        private GloveFingers _foldInto;
        private Transform _triggerKnuckle;
        private Vector3 _restPosition;
        private Quaternion _restRotation;

        public void SetUp(Joint[] measured, Pistol on, float[] grip)
        {
            joints = measured;
            pistol = on;
            thumb = grip[0];
            index = grip[1];
            middle = grip[2];
            ring = grip[3];
            pinky = grip[4];
        }

        private void Awake()
        {
            _restPosition = transform.localPosition;
            _restRotation = transform.localRotation;
            _triggerKnuckle = IndexJoint(1);
        }

        /// The index finger's second bone, the one that lies on the trigger
        private Transform IndexJoint(int which)
        {
            if (joints == null) return null;

            int seen = 0;
            for (int i = 0; i < joints.Length; i++)
            {
                if (joints[i] == null || joints[i].finger != Finger.Index) continue;
                if (seen++ == which) return joints[i].bone;
            }

            return null;
        }

        /// Ghost hands only. close is how far the hand has shut, 0 to 1; with a glove to fold into,
        /// shut means that glove's grip on its gun, otherwise a fist
        public void Hold(float close, float trigger, GloveFingers foldInto)
        {
            _closeTarget = Mathf.Clamp01(close);
            _triggerTarget = Mathf.Clamp01(trigger);
            _foldInto = foldInto;
        }

        private void OnEnable()
        {
            if (pistol != null) pistol.Fired += OnFired;
        }

        private void OnDisable()
        {
            if (pistol != null) pistol.Fired -= OnFired;
        }

        private void OnFired()
        {
            _sinceShot = 0f;
        }

        /// 0 to 1: how far the player's own trigger is pulled. The Liminal SDK reports no analog axes
        /// on Quest, so it is read from Oculus directly, as HapticPulse does for vibration
        private float TriggerPull()
        {
            var controller = pistol.Hand == VRAvatarLimbType.LeftHand ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
            if (OVRInput.IsControllerConnected(controller))
                return OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, controller);

            _sinceShot += Time.unscaledDeltaTime;
            float pulled = _sinceShot < squeezeHold ? 1f : 0f;
            _pull = Mathf.SmoothDamp(_pull, pulled, ref _pullVelocity, squeezeEase, Mathf.Infinity, Time.unscaledDeltaTime);
            return _pull;
        }

        // Real time, like the gun, the player is never slowed
        private void LateUpdate()
        {
            if (joints == null) return;

            float pull = pistol != null ? TriggerPull() : 0f;
            float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, closeSeconds);
            _close = Mathf.MoveTowards(_close, _closeTarget, step);
            _trigger = Mathf.MoveTowards(_trigger, _triggerTarget, step);
            float swap = pistol != null ? pistol.MagSwap : 0f;
            float press = pistol != null ? pistol.ReleasePress : 0f;

            if (pistol != null && pistol.IsReloading) HoldStillThroughTheTwirl();
            else if (pistol != null && _triggerKnuckle != null) pistol.SpinOn(_triggerKnuckle.position);

            for (int i = 0; i < joints.Length; i++)
            {
                var joint = joints[i];
                if (joint == null || joint.bone == null) continue;

                float angle = Grip(joint.finger);
                if (pistol == null)
                    angle = Ghost(joint.finger, angle);
                else if (joint.finger == Finger.Index)
                    angle += triggerSqueeze * pull + twirlHook * pistol.TwirlOpen(0f);
                else if (joint.finger == Finger.Thumb)
                    angle = Mathf.Lerp(angle + releasePress * press, angle * twirlLoose, pistol.TwirlOpen(Lag(joint.finger)));
                else
                    angle = Mathf.Lerp(angle - reloadLoosen * swap, angle * twirlLoose, pistol.TwirlOpen(Lag(joint.finger)));

                joint.bone.localRotation = joint.rest * Quaternion.AngleAxis(angle * joint.weight, joint.axis);
            }
        }

        /// Middle closes first, then ring, pinky and last the thumb
        private float Lag(Finger finger)
        {
            switch (finger)
            {
                case Finger.Middle: return 0f;
                case Finger.Ring: return regripLag;
                case Finger.Pinky: return regripLag * 2f;
                default: return regripLag * 3f;
            }
        }

        private float Ghost(Finger finger, float relaxed)
        {
            float sway = idleSway * Mathf.Sin((Time.unscaledTime / Mathf.Max(0.1f, idleSeconds) + (int)finger * 0.17f) * 2f * Mathf.PI);
            float shut = _foldInto != null ? _foldInto.Grip(finger) : relaxed + fist;
            float angle = Mathf.Lerp(relaxed + sway, shut, _close);

            return finger == Finger.Index ? angle + triggerSqueeze * _trigger : angle;
        }

        /// The glove rides the gun's visual, so the twirl would spin the hand with it. Undone here: the
        /// hand stays put and slackens, and the gun turns on the hooked trigger finger
        private void HoldStillThroughTheTwirl()
        {
            var spin = Quaternion.Inverse(pistol.Twirl);
            var pivot = pistol.TwirlPivot;

            transform.localPosition = pivot + spin * (_restPosition - pivot);
            transform.localRotation = spin * _restRotation;
        }

        private float Grip(Finger finger)
        {
            switch (finger)
            {
                case Finger.Thumb: return thumb;
                case Finger.Index: return index;
                case Finger.Middle: return middle;
                case Finger.Ring: return ring;
                default: return pinky;
            }
        }
    }
}
