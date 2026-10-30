using System.Collections.Generic;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    public class Projectile : MonoBehaviour
    {
        [Header("Flight (data)")]
        [SerializeField] private float speed = 38f;
        [SerializeField] private float gravityScale = 0.35f;
        [SerializeField] private float radius = 0.02f;
        [SerializeField] private float maxLifetime = 4f;
        [SerializeField] private float maxRange = 60f;

        [Header("Impact (data)")]
        [SerializeField] private float impulse = 6f;
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField] private float upwardBias = 0.15f;

        [Header("Ricochet (data)")]
        [SerializeField] private int maxBounces = 1;
        [SerializeField, Range(0f, 1f)] private float bounceSpeedKept = 0.6f;
        [SerializeField] private AudioClip[] ricochetClips;
        [SerializeField, Range(0f, 1f)] private float ricochetVolume = 0.7f;
        [SerializeField, Tooltip("The tracer after a bounce, so a ricochet can be followed by eye")]
        private Color ricochetTracer = new Color(1f, 0.95f, 0.6f, 1f);

        public bool Live { get { return _live; } }
        public float Impulse { get { return impulse; } }
        public float Speed { get { return speed; } }
        public float GravityScale { get { return gravityScale; } }

        private TrailRenderer _trail;
        private Vector3 _velocity;
        private float _distance;
        private float _age;
        private bool _live;
        private int _bounces;

        // What this round has already gone through: the reaction, or the collider of a loose piece. By reaction, so a
        // stand with a collider on every sword is struck once, not once per sword, and not again on the next step
        private readonly List<Object> _passed = new List<Object>();

        // Shared by every round: they step one at a time, and a buffer each would be 32 arrays for nothing
        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        private void Awake()
        {
            _trail = GetComponent<TrailRenderer>();
        }

        public void Fire(Vector3 origin, Vector3 direction, float speedScale, Color tracer)
        {
            transform.position = origin;
            _velocity = direction.normalized * speed * Mathf.Max(0.05f, speedScale);
            _age = 0f;
            _distance = 0f;
            _bounces = 0;
            _passed.Clear();
            _live = true;
            AimAlongVelocity();
            gameObject.SetActive(true);

            if (_trail == null) return;

            // Or it draws a streak from wherever this round last died
            _trail.Clear();
            _trail.startColor = tracer;
            _trail.endColor = new Color(tracer.r, tracer.g, tracer.b, 0f);
        }

        private void FixedUpdate()
        {
            if (!_live) return;

            _age += Time.fixedDeltaTime;
            if (_age >= maxLifetime || _distance >= maxRange) { Finish(); return; }

            _velocity += Physics.gravity * gravityScale * Time.fixedDeltaTime;

            Vector3 step = _velocity * Time.fixedDeltaTime;
            float length = step.magnitude;
            if (length <= 0f) return;

            Vector3 from = transform.position;
            Vector3 along = step / length;
            int count = Physics.SphereCastNonAlloc(from, radius, along, Hits, length, hitMask, QueryTriggerInteraction.Ignore);
            SortNearestFirst(count);

            for (int i = 0; i < count; i++)
            {
                var hit = Hits[i];

                // Already inside it at the start of the step: the single cast this replaced never saw those either
                if (hit.distance <= 0f && hit.point == Vector3.zero) continue;

                var shootable = hit.collider.GetComponentInParent<IShootable>();
                Object struck = shootable != null ? (Object)(Component)shootable : hit.collider;
                if (_passed.Contains(struck)) continue;

                Strike(hit, shootable, along);

                // A prop or a loose piece in front of a sign must never save the sign: it reacts, and the round goes on
                if (PassesThrough(hit, shootable)) { _passed.Add(struck); continue; }

                Vector3 centre = from + along * hit.distance;
                _distance += hit.distance;

                if (shootable == null && _bounces < maxBounces)
                {
                    _bounces++;
                    _velocity = Vector3.Reflect(_velocity, hit.normal) * bounceSpeedKept;
                    Ricochet(centre);
                    transform.position = centre + hit.normal * 0.005f;
                    AimAlongVelocity();
                    return;
                }

                transform.position = centre;
                Finish();
                return;
            }

            transform.position += step;
            _distance += length;
            AimAlongVelocity();
        }

        private static bool PassesThrough(RaycastHit hit, IShootable shootable)
        {
            return shootable is RoomReaction || shootable is Knockable || IsLoose(hit, shootable);
        }

        /// Shards, and anything else knocked loose to physics. Kinematic things that don't react, like the coffin,
        /// are part of the room
        private static bool IsLoose(RaycastHit hit, IShootable shootable)
        {
            return shootable == null && hit.rigidbody != null && !hit.rigidbody.isKinematic;
        }

        private void Strike(RaycastHit hit, IShootable shootable, Vector3 direction)
        {
            // Things in the room bring their own push, sized for them; the round's would throw a plushie across the room
            var body = hit.rigidbody;
            if (body != null && !body.isKinematic && !(shootable is RoomReaction))
            {
                // Dead flat, the same impulse only slides a prop along the bench
                Vector3 push = (direction + Vector3.up * upwardBias).normalized * impulse;
                body.AddForceAtPosition(push, hit.point, ForceMode.Impulse);
            }

            // A room piece can do more than one thing at once: the armoured stand rocks and spins its helmet
            var room = shootable as RoomReaction;
            var sign = shootable as TrackMover;
            if (room != null)
            {
                foreach (var reaction in room.GetComponents<RoomReaction>()) reaction.OnShot(hit.point, direction, impulse);
            }
            else if (sign != null && _bounces > 0) sign.OnTrickShot(hit.point, direction);
            else if (shootable != null) shootable.OnShot(hit.point, direction, impulse);

            // Signs and bats bring their own puff and their own sound, the dust and the thud are for the room. Loose
            // pieces get neither: a round through a spray of shards would be a burst of thuds
            bool target = shootable is TrackMover || shootable is Bat;
            if (!target && !IsLoose(hit, shootable) && ImpactFX.Instance != null) ImpactFX.Instance.PlayImpact(hit.point, hit.normal, hit.collider);
            if (!target) TrackMover.NearMiss(hit.point);
        }

        private void Ricochet(Vector3 at)
        {
            if (ricochetClips != null && ricochetClips.Length > 0 && ImpactFX.Instance != null)
                ImpactFX.Instance.PlayClip(ricochetClips[Random.Range(0, ricochetClips.Length)], at, ricochetVolume);

            if (_trail == null) return;

            _trail.startColor = ricochetTracer;
            _trail.endColor = new Color(ricochetTracer.r, ricochetTracer.g, ricochetTracer.b, 0f);
        }

        /// The buffer comes back in no particular order, and it is never more than a handful long
        private static void SortNearestFirst(int count)
        {
            for (int i = 1; i < count; i++)
            {
                var hit = Hits[i];
                int j = i - 1;
                while (j >= 0 && Hits[j].distance > hit.distance) { Hits[j + 1] = Hits[j]; j--; }
                Hits[j + 1] = hit;
            }
        }

        private void Finish()
        {
            _live = false;
            gameObject.SetActive(false);

            if (ProjectilePool.Instance != null) ProjectilePool.Instance.Return(this);
        }

        private void AimAlongVelocity()
        {
            if (_velocity.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(_velocity);
        }
    }
}
