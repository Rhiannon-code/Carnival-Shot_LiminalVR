using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    /// Something hung on a wall by one nail, the first hit swings it crooked, the side that was hit dropping,
    /// and the next knocks it off the wall to fall to physics
    public class HangingOnShot : RoomReaction
    {
        [SerializeField] private Rigidbody body;

        [Header("Feel (data)")]
        [SerializeField] private float crookedDegrees = 12f;
        [SerializeField] private float swingCycles = 2f;
        [SerializeField] private float swingSeconds = 0.9f;
        [SerializeField] private float fallImpulse = 0.5f;

        [Header("Audio")]
        [SerializeField] private AudioClip[] hitClips;
        [SerializeField, Range(0f, 1f)] private float hitVolume = 0.6f;

        private enum State { Straight, Crooked, Fallen }

        private Vector3 _restPosition;
        private Quaternion _restRotation;
        private Vector3 _nail;
        private Vector3 _facing;
        private Vector3 _middle;
        private float _target;
        private float _timer = -1f;
        private State _state;

        private void Awake()
        {
            _restPosition = transform.localPosition;
            _restRotation = transform.localRotation;
            if (body != null) body.isKinematic = true;

            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            var all = renderers[0].bounds;
            foreach (var item in renderers) all.Encapsulate(item.bounds);

            // Hung flat on a wall, its thinnest level side is the way it faces
            _facing = all.size.x < all.size.z ? Vector3.right : Vector3.forward;
            _middle = all.center;
            _nail = new Vector3(all.center.x, all.max.y, all.center.z);
        }

        public override void OnShot(Vector3 point, Vector3 direction, float impulse)
        {
            // Still swinging from the last hit, with a gun in each hand two rounds can land together, and the
            // second would knock it off the wall before it was ever seen hanging crooked
            if (_state == State.Fallen || _timer >= 0f) return;

            PlayOne(hitClips, point, hitVolume);

            if (_state == State.Straight)
            {
                // A positive turn about the facing lifts the side across from it, so the hit side drops on a negative
                var across = Vector3.Cross(Vector3.up, _facing);
                _target = -Mathf.Sign(Vector3.Dot(point - _middle, across)) * crookedDegrees;
                _timer = 0f;
                _state = State.Crooked;
                return;
            }

            _state = State.Fallen;
            _timer = -1f;
            if (body == null) return;

            body.isKinematic = false;
            body.AddForceAtPosition((direction.normalized + Vector3.up * 0.2f).normalized * fallImpulse, point, ForceMode.Impulse);
        }

        private void Update()
        {
            if (_timer < 0f) return;

            _timer += Time.deltaTime;
            float t = Mathf.Clamp01(_timer / Mathf.Max(0.01f, swingSeconds));

            // Starts level, overshoots and rings down onto the crooked angle
            float angle = _target - _target * Mathf.Cos(t * swingCycles * 2f * Mathf.PI) * (1f - t) * (1f - t);

            transform.localPosition = _restPosition;
            transform.localRotation = _restRotation;
            transform.RotateAround(_nail, _facing, angle);

            if (t >= 1f) _timer = -1f;
        }

        protected override void Restore()
        {
            _state = State.Straight;
            _timer = -1f;

            if (body != null)
            {
                if (!body.isKinematic)
                {
                    body.velocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }

                body.isKinematic = true;
            }

            transform.localPosition = _restPosition;
            transform.localRotation = _restRotation;
        }
    }
}
