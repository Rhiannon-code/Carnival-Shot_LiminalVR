using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    /// Rocks back and forth on its base, away from the shot first, and settles where it started. Never falls
    public class WobbleOnShot : RoomReaction
    {
        [Header("Feel (data)")]
        [SerializeField] private float wobbleDegrees = 6f;
        [SerializeField] private float wobbleCycles = 3f;
        [SerializeField] private float wobbleSeconds = 1.2f;

        [Header("Audio")]
        [SerializeField] private AudioClip[] hitClips;
        [SerializeField, Range(0f, 1f)] private float hitVolume = 0.6f;

        private Vector3 _restPosition;
        private Quaternion _restRotation;
        private Vector3 _base;
        private Vector3 _axis;
        private float _timer = -1f;

        private void Awake()
        {
            _restPosition = transform.localPosition;
            _restRotation = transform.localRotation;

            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { _base = transform.position; return; }

            var all = renderers[0].bounds;
            foreach (var item in renderers) all.Encapsulate(item.bounds);
            _base = new Vector3(all.center.x, all.min.y, all.center.z);
        }

        public override void OnShot(Vector3 point, Vector3 direction, float impulse)
        {
            var flat = new Vector3(direction.x, 0f, direction.z);
            if (flat.sqrMagnitude < 1e-6f) return;

            _axis = Vector3.Cross(Vector3.up, flat.normalized);
            _timer = 0f;
            PlayOne(hitClips, point, hitVolume);
        }

        private void Update()
        {
            if (_timer < 0f) return;

            _timer += Time.deltaTime;
            float t = _timer / Mathf.Max(0.01f, wobbleSeconds);

            Rest();
            if (t >= 1f) { _timer = -1f; return; }

            float angle = wobbleDegrees * Mathf.Sin(t * wobbleCycles * 2f * Mathf.PI) * (1f - t) * (1f - t);
            transform.RotateAround(_base, _axis, angle);
        }

        protected override void Restore()
        {
            _timer = -1f;
            Rest();
        }

        private void Rest()
        {
            transform.localPosition = _restPosition;
            transform.localRotation = _restRotation;
        }
    }
}
