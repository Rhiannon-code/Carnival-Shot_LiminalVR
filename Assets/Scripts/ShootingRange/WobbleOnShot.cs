using System.Collections.Generic;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    /// Rocks back and forth on its base, away from the shot first, and settles where it started. Hit faster
    /// than it can settle, every hit rocks it further, and enough of them tip it over until the next round
    public class WobbleOnShot : RoomReaction
    {
        [Header("Feel (data)")]
        [SerializeField] private float wobbleDegrees = 6f;
        [SerializeField] private float wobbleCycles = 3f;
        [SerializeField] private float wobbleSeconds = 1.2f;

        [Header("Pushed over (data): this many hits inside the window tip it")]
        [SerializeField] private int hitsToTip = 8;
        [SerializeField, Tooltip("Seconds")] private float tipWindow = 2f;
        [SerializeField, Tooltip("Extra rock per recent hit, as a share of Wobble Degrees")] private float leanPerHit = 0.15f;
        [SerializeField] private float fallDegrees = 85f;
        [SerializeField] private float fallSeconds = 0.5f;
        [SerializeField, Range(0f, 1f), Tooltip("A nearby shatter's rock, as a share of Wobble Degrees. Never tips it")]
        private float joltShare = 0.5f;

        [Header("Audio")]
        [SerializeField] private AudioClip[] hitClips;
        [SerializeField, Range(0f, 1f)] private float hitVolume = 0.6f;
        [SerializeField] private AudioClip[] fallClips;
        [SerializeField, Range(0f, 1f)] private float fallVolume = 0.8f;

        private readonly Queue<float> _recent = new Queue<float>();
        private Vector3 _restPosition;
        private Quaternion _restRotation;
        private Vector3 _base;
        private Vector3 _axis;
        private float _amplitude;
        private float _timer = -1f;
        private float _falling = -1f;
        private bool _down;

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
            if (_down) return;

            var flat = new Vector3(direction.x, 0f, direction.z);
            if (flat.sqrMagnitude < 1e-6f) return;

            _axis = Vector3.Cross(Vector3.up, flat.normalized);
            PlayOne(hitClips, point, hitVolume);

            float now = Time.time;
            _recent.Enqueue(now);
            while (now - _recent.Peek() > tipWindow) _recent.Dequeue();

            if (_recent.Count >= hitsToTip)
            {
                _down = true;
                _timer = -1f;
                _falling = 0f;
                return;
            }

            _amplitude = wobbleDegrees * (1f + leanPerHit * (_recent.Count - 1));
            _timer = 0f;
        }

        protected override void Jolt(Vector3 from)
        {
            if (_down || _timer >= 0f) return;

            var flat = transform.position - from;
            flat.y = 0f;
            if (flat.sqrMagnitude < 1e-6f) return;

            _axis = Vector3.Cross(Vector3.up, flat.normalized);
            _amplitude = wobbleDegrees * joltShare;
            _timer = 0f;
        }

        private void Update()
        {
            if (_falling >= 0f) { Fall(); return; }
            if (_timer < 0f) return;

            _timer += Time.deltaTime;
            float t = _timer / Mathf.Max(0.01f, wobbleSeconds);

            Rest();
            if (t >= 1f) { _timer = -1f; return; }

            float angle = _amplitude * Mathf.Sin(t * wobbleCycles * 2f * Mathf.PI) * (1f - t) * (1f - t);
            transform.RotateAround(_base, _axis, angle);
        }

        /// Gathers speed as it goes, like something toppling, and stays where it lands
        private void Fall()
        {
            _falling += Time.deltaTime;
            float t = Mathf.Clamp01(_falling / Mathf.Max(0.01f, fallSeconds));

            Rest();
            transform.RotateAround(_base, _axis, fallDegrees * t * t);
            if (t < 1f) return;

            _falling = -1f;
            PlayOne(fallClips, _base, fallVolume);
        }

        protected override void Restore()
        {
            _recent.Clear();
            _down = false;
            _timer = -1f;
            _falling = -1f;
            Rest();
        }

        private void Rest()
        {
            transform.localPosition = _restPosition;
            transform.localRotation = _restRotation;
        }
    }
}
