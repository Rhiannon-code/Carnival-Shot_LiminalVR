using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    /// Flips a lid open on its hinge with a springy overshoot. The hinge is an empty at the lid's back edge
    /// with the lid under it, turned so that opening is a positive turn about its own X
    public class PopOpenOnShot : RoomReaction
    {
        [SerializeField] private Transform hinge;

        [Header("Feel (data)")]
        [SerializeField] private float openDegrees = 110f;
        [SerializeField] private float openSeconds = 0.15f;
        [SerializeField] private float wobbleDegrees = 14f;
        [SerializeField] private float wobbleCycles = 2.5f;
        [SerializeField] private float wobbleSeconds = 0.6f;

        [Header("Audio")]
        [SerializeField] private AudioClip[] openClips;
        [SerializeField, Range(0f, 1f)] private float openVolume = 0.7f;

        private Quaternion _closed;
        private float _timer;
        private bool _open;

        private void Awake()
        {
            if (hinge != null) _closed = hinge.localRotation;
        }

        public override void OnShot(Vector3 point, Vector3 direction, float impulse)
        {
            if (_open || hinge == null) return;

            _open = true;
            _timer = 0f;
            PlayOne(openClips, point, openVolume);
        }

        private void Update()
        {
            if (!_open || hinge == null || _timer > openSeconds + wobbleSeconds) return;

            _timer += Time.deltaTime;

            float angle;
            if (_timer < openSeconds)
            {
                float t = _timer / openSeconds;
                angle = openDegrees * t * (2f - t);
            }
            else
            {
                // Rings down about the open mark, like a lid thrown back against its stay
                float t = Mathf.Clamp01((_timer - openSeconds) / Mathf.Max(0.01f, wobbleSeconds));
                angle = openDegrees - wobbleDegrees * Mathf.Sin(t * wobbleCycles * 2f * Mathf.PI) * (1f - t) * (1f - t);
            }

            hinge.localRotation = _closed * Quaternion.AngleAxis(angle, Vector3.right);
        }

        protected override void Restore()
        {
            _open = false;
            if (hinge != null) hinge.localRotation = _closed;
        }
    }
}
