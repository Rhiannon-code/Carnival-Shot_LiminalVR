using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    /// Breaks into the pieces cut from its own model, or into plain fragments until those are cut
    public class ShatterOnShot : RoomReaction
    {
        [Header("Pieces (empty = plain fragments)")]
        [SerializeField] private GameObject brokenSet;

        [Header("Feel (data)")]
        [SerializeField] private float fragmentImpulse = 1.4f;

        [Header("Audio (empty = the sign shatter)")]
        [SerializeField] private AudioClip[] shatterClips;
        [SerializeField, Range(0f, 1f)] private float shatterVolume = 0.7f;

        private Renderer[] _renderers;
        private Collider[] _colliders;
        private bool _broken;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _colliders = GetComponentsInChildren<Collider>(true);
        }

        public override void OnShot(Vector3 point, Vector3 direction, float impulse)
        {
            if (_broken) return;

            _broken = true;
            Show(false);

            if (ShatterPool.Instance != null)
                ShatterPool.Instance.Burst(brokenSet, transform, point, direction, fragmentImpulse,
                                           shatterClips, shatterVolume);
        }

        protected override void Restore()
        {
            _broken = false;
            Show(true);
        }

        /// Whole and out of the way of rounds while protected: its colliders are off, so a shot goes straight through
        /// without a mark. The countdown skulls are protected while they count
        public void SetProtected(bool on)
        {
            if (on) Restore();
            foreach (var item in _colliders) item.enabled = !on && !_broken;
        }

        private void Show(bool on)
        {
            foreach (var item in _renderers) item.enabled = on;
            foreach (var item in _colliders) item.enabled = on;
        }
    }
}
