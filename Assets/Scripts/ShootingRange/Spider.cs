using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    /// Drops from a web on the ceiling on a stretchy thread that bounces like a bungee cord, dangles
    /// swaying and twisting, then reels itself back up. A bonus shot: missing it costs nothing
    [RequireComponent(typeof(Rigidbody))]
    public class Spider : MonoBehaviour, IShootable
    {
        private enum Phase { Off, Dropping, Dangling, Climbing }

        [Header("Refs")]
        [SerializeField] private Transform model;
        [SerializeField] private Transform[] legs;
        [SerializeField] private LineRenderer thread;
        [SerializeField] private LineRenderer web;

        [Header("Scoring (data)")]
        [SerializeField] private int hitScore = 150;

        [Header("Bungee (data)")]
        [SerializeField] private float gravity = 9.8f;
        [SerializeField] private float stiffness = 40f;
        [SerializeField] private float damping = 2.5f;
        [Tooltip("Damping once the first rebound is over. Twice the square root of Stiffness settles with no bounce at all")]
        [SerializeField] private float settleDamping = 12.6f;
        [SerializeField] private float threadWidth = 0.006f;
        [SerializeField] private float attachHeight = 0.08f;

        [Header("Web (data, metres)")]
        [SerializeField] private float webRadius = 0.35f;
        [SerializeField] private float webGive = 0.05f;
        [SerializeField] private int webSpokes = 7;

        [Header("Dangling (data)")]
        [SerializeField] private Vector2 dangleSeconds = new Vector2(3f, 5f);
        [SerializeField] private float swayDegrees = 4f;
        [SerializeField] private float swaySeconds = 2.4f;
        [SerializeField] private float twistDegrees = 18f;
        [SerializeField] private float twistSeconds = 3.1f;
        [SerializeField] private float climbSpeed = 1.1f;

        [Header("Legs (data)")]
        [SerializeField] private float twitchDegrees = 10f;
        [SerializeField] private float twitchPerSecond = 2.5f;

        public bool Active { get { return _phase != Phase.Off; } }

        private Rigidbody _body;
        private SpiderNest _nest;
        private Transform _player;
        private Quaternion[] _legRest;
        private Vector3 _anchor;
        private Vector3[] _webPoints;
        private Phase _phase;
        private float _rest;
        private float _length;
        private float _speed;
        private float _clock;
        private float _dangleUntil;
        private float _seed;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _body.isKinematic = true;
            _body.interpolation = RigidbodyInterpolation.Interpolate;

            _legRest = new Quaternion[legs != null ? legs.Length : 0];
            for (int i = 0; i < _legRest.Length; i++) _legRest[i] = legs[i] != null ? legs[i].localRotation : Quaternion.identity;

            _webPoints = new Vector3[webSpokes * 3 + 2 * (webSpokes + 1)];
            if (web != null) web.positionCount = _webPoints.Length;
            if (thread != null) thread.positionCount = 2;
        }

        /// depth is how far below the anchor it settles. The thread's unstretched length is set short of
        /// that by the stretch its own weight puts in, so the bounces come to rest exactly there
        public void Drop(SpiderNest nest, Vector3 anchor, float depth, Transform player)
        {
            _nest = nest;
            _anchor = anchor;
            _player = player;
            _rest = Mathf.Max(0.1f, depth - gravity / Mathf.Max(0.1f, stiffness));
            _length = 0f;
            _speed = 0f;
            _clock = 0f;
            _seed = Random.value * 10f;
            _phase = Phase.Dropping;

            transform.position = anchor + Vector3.down * attachHeight;
            gameObject.SetActive(true);
            Draw();
        }

        public void Stop()
        {
            _phase = Phase.Off;
            gameObject.SetActive(false);
        }

        public void OnShot(Vector3 point, Vector3 direction, float impulse)
        {
            if (!Active) return;

            if (RangeGame.Instance != null) RangeGame.Instance.Scored(hitScore, point, false, false);
            Stop();
            if (_nest != null) _nest.Popped(point, direction);
        }

        private void FixedUpdate()
        {
            if (!Active) return;

            float dt = Time.fixedDeltaTime;
            _clock += dt;

            if (_phase == Phase.Climbing) _rest = Mathf.Max(0f, _rest - climbSpeed * dt);

            // A spring that only pulls once it is stretched: free fall, then the cord catches and bounces.
            // Only the drop is springy; from the top of the first rebound on it settles without bouncing
            float stretch = Mathf.Max(0f, _length - _rest);
            float drag = _phase == Phase.Dropping ? damping : settleDamping;
            float pull = stiffness * stretch + (stretch > 0f ? drag * _speed : 0f);
            _speed += (gravity - pull) * dt;
            _length = Mathf.Max(0f, _length + _speed * dt);

            if (_phase == Phase.Dropping && _clock > 1f && Mathf.Abs(_speed) < 0.15f)
            {
                _phase = Phase.Dangling;
                _dangleUntil = _clock + Random.Range(dangleSeconds.x, dangleSeconds.y);
            }
            else if (_phase == Phase.Dangling && _clock >= _dangleUntil)
            {
                _phase = Phase.Climbing;
            }
            else if (_phase == Phase.Climbing && _rest <= 0f && _length < 0.3f)
            {
                Stop();
                return;
            }

            _body.MovePosition(_anchor + Sway() * (Vector3.down * (_length + attachHeight)));
            _body.MoveRotation(Facing());
        }

        private void LateUpdate()
        {
            if (!Active) return;

            Draw();
            Twitch();
        }

        private Quaternion Sway()
        {
            float t = _clock + _seed;
            float grow = Mathf.Clamp01(_clock / 1.5f);
            float a = swayDegrees * grow * Mathf.Sin(t * 2f * Mathf.PI / Mathf.Max(0.1f, swaySeconds));
            float b = swayDegrees * 0.5f * grow * Mathf.Cos(t * 2f * Mathf.PI / Mathf.Max(0.1f, swaySeconds * 1.37f));
            return Quaternion.Euler(a, 0f, b);
        }

        /// Faces the player, twisting back and forth on the thread the way a hanging spider turns
        private Quaternion Facing()
        {
            Vector3 toPlayer = _player != null ? _player.position - transform.position : Vector3.back;
            toPlayer.y = 0f;
            var face = toPlayer.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(toPlayer) : Quaternion.identity;

            float twist = twistDegrees * Mathf.Sin((_clock + _seed) * 2f * Mathf.PI / Mathf.Max(0.1f, twistSeconds));
            return face * Quaternion.Euler(0f, twist, 0f);
        }

        /// The thread thins as it stretches, and the web's middle is dragged down by the pull on it
        private void Draw()
        {
            float stretch = Mathf.Max(0f, _length - _rest);
            float tension = stiffness * stretch / Mathf.Max(0.1f, gravity);
            float sag = Mathf.Min(webGive * 2f, webGive * tension);
            Vector3 centre = _anchor + Vector3.down * sag;

            if (thread != null)
            {
                thread.SetPosition(0, centre);
                thread.SetPosition(1, transform.position + transform.up * attachHeight);
                thread.widthMultiplier = threadWidth / Mathf.Sqrt(1f + stretch / Mathf.Max(0.1f, _rest));
            }

            if (web == null) return;

            int n = 0;
            for (int i = 0; i < webSpokes; i++)
            {
                _webPoints[n++] = centre;
                _webPoints[n++] = WebPoint(i, 1f, sag);
                _webPoints[n++] = centre;
            }

            for (int ring = 1; ring <= 2; ring++)
            {
                float r = ring / 3f + 0.15f;
                for (int i = 0; i <= webSpokes; i++) _webPoints[n++] = WebPoint(i % webSpokes, r, sag);
            }

            web.SetPositions(_webPoints);
        }

        private Vector3 WebPoint(int spoke, float along, float sag)
        {
            float angle = (float)spoke / webSpokes * 2f * Mathf.PI;
            var flat = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * webRadius * along;
            return _anchor + flat + Vector3.down * sag * (1f - along);
        }

        /// Legs flail while it is falling and bouncing, and only twitch once it is hanging still
        private void Twitch()
        {
            if (legs == null) return;

            float flail = 1f + Mathf.Min(3f, Mathf.Abs(_speed));
            for (int i = 0; i < legs.Length; i++)
            {
                if (legs[i] == null) continue;

                float wave = Mathf.Sin((_clock * twitchPerSecond + i * 0.37f + _seed) * 2f * Mathf.PI);
                legs[i].localRotation = _legRest[i] * Quaternion.Euler(twitchDegrees * flail * wave, 0f, 0f);
            }
        }
    }
}
