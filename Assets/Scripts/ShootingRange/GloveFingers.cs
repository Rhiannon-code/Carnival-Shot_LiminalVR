using System;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    /// Curls a SteamVR glove's fingers. On a pistol it grips, squeezes the trigger on every shot and
    /// loosens through the reload, with no pistol it idles, so the ghost hands never sit like statues
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
        [SerializeField] private float squeezeSeconds = 0.12f;
        [SerializeField] private float reloadLoosen = 30f;
        [SerializeField] private float reloadThumbFlick = 35f;

        [Header("Idle, for the ghost hands")]
        [SerializeField] private float idleSway = 6f;
        [SerializeField] private float idleSeconds = 3f;

        private float _squeeze;

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
            _squeeze = 1f;
        }

        // Real time, like the gun, the player is never slowed
        private void LateUpdate()
        {
            if (joints == null) return;

            _squeeze = Mathf.MoveTowards(_squeeze, 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, squeezeSeconds));
            float reload = pistol != null ? pistol.ReloadBlend : 0f;

            for (int i = 0; i < joints.Length; i++)
            {
                var joint = joints[i];
                if (joint == null || joint.bone == null) continue;

                float angle = Grip(joint.finger);
                if (pistol == null)
                    angle += idleSway * Mathf.Sin((Time.unscaledTime / Mathf.Max(0.1f, idleSeconds) + (int)joint.finger * 0.17f) * 2f * Mathf.PI);
                else if (joint.finger == Finger.Index)
                    angle += triggerSqueeze * _squeeze;
                else if (joint.finger == Finger.Thumb)
                    angle -= reloadThumbFlick * reload;
                else
                    angle -= reloadLoosen * reload;

                joint.bone.localRotation = joint.rest * Quaternion.AngleAxis(angle, joint.axis);
            }
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
