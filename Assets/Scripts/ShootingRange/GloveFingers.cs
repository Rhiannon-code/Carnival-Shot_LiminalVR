using System;
using UnityEngine;
using Liminal.SDK.VR.Avatars;

namespace IntuitiveDesigns.ShootingRange
{
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
            public float weight = 1f;
        }

        [SerializeField] private Joint[] joints;

        [Header("Grip, degrees per joint (tweak live in Play mode)")]
        [SerializeField] private float thumb = 30f;
        [SerializeField] private float index = 25f;
        [SerializeField] private float middle = 70f;
        [SerializeField] private float ring = 75f;
        [SerializeField] private float pinky = 80f;

        [Header("Hand on a pistol")]
        [SerializeField] private Vector3 wristTurn;
        [SerializeField] private Vector3 wristShift;
        [SerializeField] private Vector3 thumbTurn;

        [Header("On a pistol (empty = idle)")]
        [SerializeField] private Pistol pistol;
        [SerializeField] private float triggerSqueeze = 25f;
        [SerializeField] private float squeezeHold = 0.05f;
        [SerializeField] private float squeezeEase = 0.035f;
        [SerializeField] private float reloadLoosen = 30f;
        [SerializeField] private float releasePress = 30f;

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
        private Quaternion _fitted;
        private Vector3 _fittedAt;
        private Vector3 _wrist;

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
            _fitted = transform.localRotation;
            _fittedAt = transform.localPosition;
            _wrist = _fittedAt;
            foreach (var bone in GetComponentsInChildren<Transform>(true))
            {
                if (!bone.name.StartsWith("wrist_") || transform.parent == null) continue;
                _wrist = transform.parent.InverseTransformPoint(bone.position);
                break;
            }
        }

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

            if (pistol != null)
            {
                var turn = _fitted * Quaternion.Euler(wristTurn) * Quaternion.Inverse(_fitted);
                transform.localRotation = turn * _fitted;
                transform.localPosition = _wrist + turn * (_fittedAt - _wrist);
                transform.position += transform.parent.TransformDirection(wristShift);
            }

            float pull = pistol != null ? TriggerPull() : 0f;
            float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, closeSeconds);
            _close = Mathf.MoveTowards(_close, _closeTarget, step);
            _trigger = Mathf.MoveTowards(_trigger, _triggerTarget, step);
            float swap = pistol != null ? pistol.MagSwap : 0f;
            float press = pistol != null ? pistol.ReleasePress : 0f;

            bool thumbTurned = false;
            for (int i = 0; i < joints.Length; i++)
            {
                var joint = joints[i];
                if (joint == null || joint.bone == null) continue;

                float angle = Grip(joint.finger);
                if (pistol == null)
                    angle = Ghost(joint.finger, angle);
                else if (joint.finger == Finger.Index)
                    angle += triggerSqueeze * pull;
                else if (joint.finger == Finger.Thumb)
                    angle += releasePress * press;
                else
                    angle -= reloadLoosen * swap;

                var pose = joint.rest * Quaternion.AngleAxis(angle * joint.weight, joint.axis);
                if (joint.finger == Finger.Thumb && !thumbTurned)
                {
                    pose *= Quaternion.Euler(thumbTurn);
                    thumbTurned = true;
                }
                joint.bone.localRotation = pose;
            }
        }

        private float Ghost(Finger finger, float relaxed)
        {
            float sway = idleSway * Mathf.Sin((Time.unscaledTime / Mathf.Max(0.1f, idleSeconds) + (int)finger * 0.17f) * 2f * Mathf.PI);
            float shut = _foldInto != null ? _foldInto.Grip(finger) : relaxed + fist;
            float angle = Mathf.Lerp(relaxed + sway, shut, _close);

            return finger == Finger.Index ? angle + triggerSqueeze * _trigger : angle;
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
