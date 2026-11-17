using Liminal.SDK.VR.Avatars;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    public class ReloadTag : MonoBehaviour
    {
        [SerializeField] private Pistol pistol;
        [SerializeField] private RangeGame game;
        [SerializeField] private GameObject content;
        [SerializeField] private Transform gripGlow;

        [Header("Look (data)")]
        [SerializeField] private float above = 0.1f;
        [SerializeField]
        private float size = 0.5f;
        [SerializeField] private float pulsesPerSecond = 2f;
        [SerializeField, Range(0f, 0.5f)] private float pulse = 0.15f;

        private Vector3 _glowSize;
        private Transform _head;
        private bool _showing;

        private void Awake()
        {
            if (gripGlow != null) _glowSize = gripGlow.localScale;
            content.SetActive(false);
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
