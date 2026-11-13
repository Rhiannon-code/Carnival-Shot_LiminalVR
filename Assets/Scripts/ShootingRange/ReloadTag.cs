using Liminal.SDK.VR.Avatars;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    /// Over a dry gun's muzzle while it can't fire: "RELOAD", the controller with its grip glowing, and an arrow down at the
    /// gun, turned to face the player (client feedback, 2026-11-10: make the reload very clear). Double size until the
    /// player has reloaded once this session. Step 71 builds it on each gun
    public class ReloadTag : MonoBehaviour
    {
        [SerializeField] private Pistol pistol;
        [SerializeField] private RangeGame game;
        [SerializeField] private GameObject content;
        [SerializeField, Tooltip("Pulses to say squeeze here")] private Transform gripGlow;

        [Header("Look (data)")]
        [SerializeField, Tooltip("Metres over the muzzle")] private float above = 0.1f;
        [SerializeField, Tooltip("Size until the player has reloaded once")] private float firstSize = 2f;
        [SerializeField] private float pulsesPerSecond = 2f;
        [SerializeField, Range(0f, 0.5f)] private float pulse = 0.15f;

        private static bool _taught;
        private Vector3 _glowSize;
        private Transform _head;
        private bool _showing;

        private void Awake()
        {
            if (gripGlow != null) _glowSize = gripGlow.localScale;
            content.SetActive(false);
        }

        private void OnEnable()
        {
            if (pistol != null) pistol.AmmoChanged += OnAmmoChanged;
            if (game != null) game.StateChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            if (pistol != null) pistol.AmmoChanged -= OnAmmoChanged;
            if (game != null) game.StateChanged -= OnStateChanged;
        }

        /// A new session starts teaching again: statics outlive a scene inside the Liminal player
        private void OnStateChanged(RangeGame.State state)
        {
            if (state == RangeGame.State.HowToPlay) _taught = false;
        }

        private void OnAmmoChanged(int rounds, int size)
        {
            if (_showing && rounds > 0) _taught = true;
        }

        private void LateUpdate()
        {
            bool show = pistol != null && pistol.IsHeld && pistol.IsEmpty && game != null && game.Current == RangeGame.State.Playing;
            if (show != _showing)
            {
                _showing = show;
                content.SetActive(show);
            }
            if (!show) return;

            transform.position = pistol.SightOrigin + Vector3.up * above;
            if (_head != null || FindTheHead())
            {
                // Text Mesh Pro and quads read from behind their +Z, so +Z points away from the player
                Vector3 away = transform.position - _head.position;
                if (away.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(away, Vector3.up);
            }

            float size = _taught ? 1f : firstSize;
            transform.localScale = Vector3.one * size / Mathf.Max(1e-4f, transform.parent != null ? transform.parent.lossyScale.x : 1f);
            if (gripGlow != null)
                gripGlow.localScale = _glowSize * (1f + pulse * Mathf.Sin(Time.unscaledTime * pulsesPerSecond * 2f * Mathf.PI));
        }

        /// The avatar's head once the SDK has spawned the rig, or the camera before it has. Not ?. on VRAvatar: it's a
        /// UnityEngine.Object, and null propagation would sail past a destroyed one
        private bool FindTheHead()
        {
            var avatar = VRAvatar.Active;
            if (avatar != null)
            {
                var limb = avatar.GetLimb(VRAvatarLimbType.Head);
                if (limb != null) { _head = limb.Transform; return true; }
            }

            var cam = Camera.main;
            if (cam != null) _head = cam.transform;
            return _head != null;
        }
    }
}
