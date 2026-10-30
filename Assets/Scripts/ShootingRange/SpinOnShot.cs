using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    /// Spins a part about the upright through its middle, the way the shot struck it, and lets it run down.
    /// Every hit adds more spin. It stops wherever it stops and squares up again next round
    public class SpinOnShot : RoomReaction
    {
        [SerializeField] private Renderer spinner;

        [Header("Feel (data)")]
        [SerializeField] private float spinPerHit = 900f;
        [SerializeField] private float drag = 1.2f;
        [SerializeField, Range(0f, 1f), Tooltip("A nearby shatter's push, as a share of Spin Per Hit")]
        private float joltShare = 0.3f;

        [Header("Audio")]
        [SerializeField] private AudioClip[] spinClips;
        [SerializeField, Range(0f, 1f)] private float spinVolume = 0.6f;

        private Vector3 _restPosition;
        private Quaternion _restRotation;
        private float _speed;

        private void Awake()
        {
            if (spinner == null) return;

            _restPosition = spinner.transform.localPosition;
            _restRotation = spinner.transform.localRotation;
        }

        public override void OnShot(Vector3 point, Vector3 direction, float impulse)
        {
            if (spinner == null) return;

            // Which side of the middle the round passed decides the way round, as it would on a real one
            var middle = spinner.bounds.center;
            float side = Mathf.Sign(Vector3.Dot(Vector3.Cross(point - middle, direction), Vector3.up));
            _speed += side * spinPerHit;

            PlayOne(spinClips, point, spinVolume);
        }

        protected override void Jolt(Vector3 from)
        {
            if (spinner == null) return;
            _speed += (Random.value < 0.5f ? -1f : 1f) * spinPerHit * joltShare;
        }

        private void Update()
        {
            if (_speed == 0f || spinner == null) return;

            spinner.transform.RotateAround(spinner.bounds.center, Vector3.up, _speed * Time.deltaTime);
            _speed *= Mathf.Exp(-drag * Time.deltaTime);
            if (Mathf.Abs(_speed) < 5f) _speed = 0f;
        }

        protected override void Restore()
        {
            _speed = 0f;
            if (spinner == null) return;

            spinner.transform.localPosition = _restPosition;
            spinner.transform.localRotation = _restRotation;
        }
    }
}
