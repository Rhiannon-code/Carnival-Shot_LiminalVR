using UnityEngine;
using Liminal.SDK.VR.Avatars;

namespace IntuitiveDesigns.CrystalCatch
{
    public class HapticPulse : MonoBehaviour
    {
        public static HapticPulse Instance { get; private set; }

        [Header("Feel (data)")]
        [SerializeField] private float frequency = 1f;
        [SerializeField] private float hitSeconds = 0.07f;
        [SerializeField] private float minAmplitude = 0.45f;
        [SerializeField] private float maxAmplitude = 1f;
        [SerializeField] private bool enableHaptics = true;

        [Header("A gunshot's kick (data): full strength for the snap, then a share of it for the tail")]
        [SerializeField] private float kickSnapSeconds = 0.02f;
        [SerializeField] private float kickTailSeconds = 0.05f;
        [SerializeField, Range(0f, 1f)] private float kickTailShare = 0.35f;

        [Header("Both hands (data)")]
        [SerializeField, Range(0f, 1f)] private float offHandShare = 0.75f;

        [Header("Diagnostics")]
        [SerializeField] private bool logDeviceStateOnce = false;

        private float _leftStopAt;
        private float _rightStopAt;
        private float _leftTailAt;
        private float _rightTailAt;
        private float _leftTail;
        private float _rightTail;
        private bool _logged;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            StopAll();
            if (Instance == this) Instance = null;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) StopAll();
        }

        public void Hit(VRAvatarLimbType hand, float strength01)
        {
            if (!enableHaptics) return;

            float amplitude = Mathf.Lerp(minAmplitude, maxAmplitude, Mathf.Clamp01(strength01));
            Pulse(hand, amplitude, hitSeconds);
        }

        /// A gunshot: a hard snap, then a short weaker tail, which reads sharper than one flat buzz
        public void Kick(VRAvatarLimbType hand, float strength01)
        {
            if (!enableHaptics) return;

            float amplitude = Mathf.Lerp(minAmplitude, maxAmplitude, Mathf.Clamp01(strength01));
            Pulse(hand, maxAmplitude, kickSnapSeconds);

            if (hand == VRAvatarLimbType.LeftHand) { _leftTailAt = _leftStopAt; _leftTail = amplitude * kickTailShare; }
            else { _rightTailAt = _rightStopAt; _rightTail = amplitude * kickTailShare; }
        }

        public void HitBoth(VRAvatarLimbType swingingHand, float strength01)
        {
            if (!enableHaptics) return;

            float amplitude = Mathf.Lerp(minAmplitude, maxAmplitude, Mathf.Clamp01(strength01));

            Pulse(swingingHand, amplitude, hitSeconds);

            if (offHandShare <= 0.001f) return;

            var other = swingingHand == VRAvatarLimbType.LeftHand
                      ? VRAvatarLimbType.RightHand
                      : VRAvatarLimbType.LeftHand;

            Pulse(other, amplitude * offHandShare, hitSeconds);
        }

        public void Pulse(VRAvatarLimbType hand, float amplitude, float seconds)
        {
            if (!enableHaptics) return;

            bool left = hand == VRAvatarLimbType.LeftHand;
            var controller = left ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;

            // A newer pulse moves the stop, so an older one never cuts it short, and drops a tail still waiting
            float stopAt = Time.unscaledTime + seconds;
            if (left) { _leftStopAt = stopAt; _leftTailAt = 0f; }
            else { _rightStopAt = stopAt; _rightTailAt = 0f; }

            LogDeviceState();
            Vibrate(controller, frequency, Mathf.Clamp01(amplitude));
            LogPulse(controller, amplitude, seconds);
        }

        /// One check a frame instead of a coroutine per shot
        private void Update()
        {
            float now = Time.unscaledTime;

            if (_leftStopAt > 0f && now >= _leftStopAt)
            {
                bool tail = _leftTailAt > 0f;
                _leftStopAt = tail ? now + kickTailSeconds : 0f;
                _leftTailAt = 0f;
                Vibrate(OVRInput.Controller.LTouch, tail ? frequency : 0f, tail ? _leftTail : 0f);
            }

            if (_rightStopAt > 0f && now >= _rightStopAt)
            {
                bool tail = _rightTailAt > 0f;
                _rightStopAt = tail ? now + kickTailSeconds : 0f;
                _rightTailAt = 0f;
                Vibrate(OVRInput.Controller.RTouch, tail ? frequency : 0f, tail ? _rightTail : 0f);
            }
        }

        private static void Vibrate(OVRInput.Controller controller, float freq, float amp)
        {
            if (OVRPlugin.initialized)
                OVRPlugin.SetControllerVibration((uint)controller, freq, amp);
            else
                OVRInput.SetControllerVibration(freq, amp, controller);
        }

        private void LogDeviceState()
        {
            if (!logDeviceStateOnce || _logged) return;
            _logged = true;
            Debug.Log("[Haptic] OVRPlugin.initialized=" + OVRPlugin.initialized +
                      " loadedXRDevice=" + OVRManager.loadedXRDevice +
                      " connected=" + OVRInput.GetConnectedControllers() +
                      " active=" + OVRInput.GetActiveController());
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogPulse(OVRInput.Controller controller, float amplitude, float seconds)
        {
            Debug.Log("[Haptic] " + controller + " amp=" + amplitude.ToString("0.00") +
                      " for " + seconds.ToString("0.000") + "s");
        }

        [ContextMenu("Stop all vibration")]
        public void StopAll()
        {
            Vibrate(OVRInput.Controller.LTouch, 0f, 0f);
            Vibrate(OVRInput.Controller.RTouch, 0f, 0f);
        }
    }
}
