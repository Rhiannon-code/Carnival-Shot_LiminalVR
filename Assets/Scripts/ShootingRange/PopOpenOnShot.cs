using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    /// Flips a lid open on its hinge with a springy overshoot, and something picked at random springs out. The
    /// hinge is an empty at the lid's back edge with the lid under it, turned so that opening is a positive turn
    /// about its own X
    public class PopOpenOnShot : RoomReaction
    {
        [SerializeField] private Transform hinge;

        [Header("Feel (data)")]
        [SerializeField] private float openDegrees = 110f;
        [SerializeField] private float openSeconds = 0.15f;
        [SerializeField] private float wobbleDegrees = 14f;
        [SerializeField] private float wobbleCycles = 2.5f;
        [SerializeField] private float wobbleSeconds = 0.6f;

        [Header("Springs out as it opens (optional, one picked at random, each hidden inside until then)")]
        [SerializeField] private Transform[] gags;
        [SerializeField] private float springHeight = 0.3f;
        [SerializeField] private float springSeconds = 0.8f;
        [SerializeField] private float springCycles = 3f;

        [Header("Audio")]
        [SerializeField] private AudioClip[] openClips;
        [SerializeField] private AudioClip[] springClips;
        [SerializeField, Range(0f, 1f)] private float openVolume = 0.7f;

        private Quaternion _closed;
        private float _timer;
        private bool _open;
        private Transform _gag;
        private Vector3 _gagRest;

        private void Awake()
        {
            if (hinge != null) _closed = hinge.localRotation;
            HideGags();
        }

        public override void OnShot(Vector3 point, Vector3 direction, float impulse)
        {
            if (_open || hinge == null) return;

            _open = true;
            _timer = 0f;
            PlayOne(openClips, point, openVolume);

            if (gags == null || gags.Length == 0) return;

            _gag = gags[Random.Range(0, gags.Length)];
            if (_gag == null) return;

            _gagRest = _gag.localPosition;
            _gag.gameObject.SetActive(true);
            PlayOne(springClips, point, openVolume);
        }

        private void Update()
        {
            if (!_open || hinge == null || _timer > openSeconds + Mathf.Max(wobbleSeconds, springSeconds)) return;

            _timer += Time.deltaTime;
            Spring();
            if (_timer > openSeconds + wobbleSeconds) return;

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

        /// Up out of the box as the lid clears it, overshooting and bobbing on its spring
        private void Spring()
        {
            if (_gag == null) return;

            float t = Mathf.Clamp01((_timer - openSeconds * 0.5f) / Mathf.Max(0.01f, springSeconds));
            float rise = springHeight * (1f - Mathf.Cos(t * springCycles * 2f * Mathf.PI) * (1f - t) * (1f - t));
            _gag.localPosition = _gagRest + Vector3.up * rise;
        }

        protected override void Restore()
        {
            _open = false;
            if (hinge != null) hinge.localRotation = _closed;
            if (_gag != null) _gag.localPosition = _gagRest;
            _gag = null;
            HideGags();
        }

        private void HideGags()
        {
            if (gags == null) return;
            foreach (var gag in gags)
            {
                if (gag != null) gag.gameObject.SetActive(false);
            }
        }
    }
}
