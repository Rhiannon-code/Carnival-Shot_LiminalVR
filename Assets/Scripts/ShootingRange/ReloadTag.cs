using Liminal.SDK.VR.Avatars;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    public class ReloadTag : MonoBehaviour
    {
        [SerializeField] private Pistol pistol;
        [SerializeField] private RangeGame game;
        [SerializeField] private GameObject content;
        [SerializeField, Tooltip("What the arrow points down at: the gun's ammo counter. Empty, the muzzle")]
        private Transform pointAt;

        [Header("Look (data)")]
        [SerializeField, Tooltip("Metres from what it points at up to the arrow's middle")] private float above = 0.03f;
        [SerializeField]
        private float size = 0.5f;

        private Transform _head;
        private bool _showing;

        private void Awake()
        {
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

            transform.position = (pointAt != null ? pointAt.position : pistol.SightOrigin) + Vector3.up * above;
            if (_head != null || FindTheHead())
            {
                // Text Mesh Pro and quads read from behind their +Z, so +Z points away from the player
                Vector3 away = transform.position - _head.position;
                if (away.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(away, Vector3.up);
            }

            transform.localScale = Vector3.one * size / Mathf.Max(1e-4f, transform.parent != null ? transform.parent.lossyScale.x : 1f);
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
