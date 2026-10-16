using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    [RequireComponent(typeof(Rigidbody))]
    public class Bat : MonoBehaviour, IShootable
    {
        [Header("Scoring (data)")]
        [SerializeField] private int hitScore = 50;

        [Header("Reach (data, metres)")]
        [SerializeField] private float biteRadius = 0.35f;
        [SerializeField] private float flyPast = 1.5f;

        [Header("Wings")]
        [SerializeField] private Animation flapper;
        [SerializeField] private float flapSpeed = 0.75f;
        [SerializeField] private AudioSource wingLoop;

        private Rigidbody _body;
        private BatSwarm _swarm;
        private Transform _head;
        private Vector3 _position;
        private Vector3 _velocity;
        private float _travelled;
        private float _range;
        private bool _flying;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _body.isKinematic = true;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        public void Launch(BatSwarm swarm, Vector3 from, Transform head, float speed)
        {
            _swarm = swarm;
            _head = head;

            Vector3 toHead = head.position - from;
            _velocity = toHead.normalized * speed;
            _range = toHead.magnitude + flyPast;
            _position = from;
            _travelled = 0f;
            _flying = true;

            transform.position = from;
            transform.rotation = Quaternion.LookRotation(_velocity);
            gameObject.SetActive(true);

            // Out of step with the rest of the swarm
            if (flapper != null && flapper.clip != null)
            {
                var flap = flapper[flapper.clip.name];
                flap.speed = flapSpeed;
                flap.time = Random.value * flap.length;
                flapper.Play(flapper.clip.name);
            }

            if (wingLoop != null) wingLoop.Play();
        }

        public void Stop()
        {
            _flying = false;
            gameObject.SetActive(false);
        }

        private void FixedUpdate()
        {
            if (!_flying) return;

            Vector3 step = _velocity * Time.fixedDeltaTime;
            Vector3 next = _position + step;

            // Tested along the whole step, or a fast bat can pass the head between two physics ticks
            if (_head != null && DistanceToSegment(_head.position, _position, next) <= biteRadius)
            {
                _flying = false;
                if (RangeGame.Instance != null) RangeGame.Instance.HitPlayer(next);
                _swarm.Return(this);
                return;
            }

            _travelled += step.magnitude;
            if (_travelled >= _range)
            {
                _flying = false;
                _swarm.Return(this);
                return;
            }

            _position = next;
            _body.MovePosition(next);
        }

        public void OnShot(Vector3 point, Vector3 direction, float impulse)
        {
            if (!_flying) return;

            _flying = false;
            if (RangeGame.Instance != null) RangeGame.Instance.Scored(hitScore, point, false, false);
            _swarm.Popped(this, point, direction);
        }

        public static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector3.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector3.Distance(point, a + ab * t);
        }
    }
}
