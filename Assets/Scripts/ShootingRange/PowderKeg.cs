using IntuitiveDesigns.CrystalCatch;
using Liminal.SDK.VR.Avatars;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    /// A powder keg on a step board. It waits out of sight in the gap behind the board, rides up, and settles on the
    /// board's top; shot, it blows and takes every sign near it, each a kill as a shot is (user, 2026-11-02)
    public class PowderKeg : MonoBehaviour, IShootable
    {
        private enum State { Down, Rising, Up, Sinking }

        [SerializeField] private Transform keg;
        [SerializeField] private CapsuleCollider hitBox;

        [Header("The lift (data, seconds and metres)")]
        [SerializeField] private float riseSeconds = 0.6f;
        [SerializeField] private float slideSeconds = 0.3f;
        [SerializeField, Tooltip("How far over the board's top it rides before it slides onto it")] private float clearance = 0.04f;

        [Header("The blast (data)")]
        [SerializeField, Tooltip("Metres from the keg that a sign is caught")] private float blastRadius = 1.5f;
        [SerializeField, Tooltip("A flash and a shockwave ring along the ground, in place of the cartoon fireball (client feedback, 2026-11-10). Step 72 builds it")]
        private ParticleSystem blastFx;
        [SerializeField, Tooltip("Metres out that loose props are thrown, and how hard")] private float throwRadius = 2.5f;
        [SerializeField] private float throwForce = 6f;
        [SerializeField, Range(0f, 1f)] private float rumble = 1f;
        [SerializeField] private float rumbleSeconds = 0.35f;
        [SerializeField, Range(0f, 1f), Tooltip("0 heard from everywhere alike, 1 only from where it is")] private float blastSpatial = 0.4f;

        [Header("Audio")]
        [SerializeField] private AudioClip[] blastClips;
        [SerializeField] private AudioClip liftClip;
        [SerializeField] private AudioClip settleClip;
        [SerializeField, Range(0f, 1f)] private float volume = 0.9f;

        private State _state = State.Down;
        private float _timer;
        private Vector3 _waiting;
        private AudioSource _boom;
        private static readonly Collider[] Near = new Collider[64];

        public bool Busy { get { return _state != State.Down; } }

        private void Awake()
        {
            _boom = gameObject.AddComponent<AudioSource>();
            _boom.playOnAwake = false;
            _boom.spatialBlend = blastSpatial;
            Hide();
        }

        /// Comes up behind the board whose top's middle is boardTop, and settles there. Along is the board's run
        public void Raise(Vector3 boardTop, Quaternion along, float boardDepth)
        {
            transform.SetPositionAndRotation(boardTop, along);
            _waiting = new Vector3(0f, -hitBox.height - clearance, boardDepth * 0.5f + hitBox.radius + clearance);
            keg.localPosition = _waiting;
            keg.gameObject.SetActive(true);
            hitBox.enabled = true;
            Play(liftClip);
            Begin(State.Rising);
        }

        /// Back down behind its board, if it's still there
        public void Lower()
        {
            if (_state == State.Down || _state == State.Sinking) return;

            hitBox.enabled = false;
            Begin(State.Sinking);
        }

        public void OnShot(Vector3 point, Vector3 direction, float impulse)
        {
            if (_state == State.Down) return;

            Vector3 centre = hitBox.bounds.center;
            Hide();

            if (blastClips != null && blastClips.Length > 0)
            {
                _boom.transform.position = centre;
                _boom.PlayOneShot(blastClips[Random.Range(0, blastClips.Length)], volume);
            }
            if (blastFx != null)
            {
                blastFx.transform.position = centre;
                blastFx.Play(true);
            }
            if (Debris.Instance != null)
            {
                Debris.Instance.Play(DebrisKind.Splinters, centre, -direction, 10);
                Debris.Instance.Play(DebrisKind.Embers, centre, -direction, 8);
                Debris.Instance.Play(DebrisKind.Sparks, centre, -direction, 6);
            }
            if (HapticPulse.Instance != null)
            {
                HapticPulse.Instance.Pulse(VRAvatarLimbType.LeftHand, rumble, rumbleSeconds);
                HapticPulse.Instance.Pulse(VRAvatarLimbType.RightHand, rumble, rumbleSeconds);
            }

            var movers = TrackMover.Movers;
            for (int i = 0; i < movers.Count; i++)
            {
                if ((movers[i].TargetPoint - centre).sqrMagnitude <= blastRadius * blastRadius) movers[i].Blast(centre);
            }

            Throw(centre);
        }

        /// Loose props fly, and anything near that reacts to a shot gets one from the blast, another keg included
        private void Throw(Vector3 centre)
        {
            int count = Physics.OverlapSphereNonAlloc(centre, throwRadius, Near, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var body = Near[i].attachedRigidbody;
                if (body != null && !body.isKinematic) body.AddExplosionForce(throwForce, centre, throwRadius, 0.5f, ForceMode.Impulse);

                var shootable = Near[i].GetComponentInParent<IShootable>();
                if (shootable == null || shootable is TrackMover || ReferenceEquals(shootable, this)) continue;

                var point = Near[i].ClosestPoint(centre);
                shootable.OnShot(point, (point - centre).normalized, throwForce);
            }
        }

        private void Update()
        {
            if (_state == State.Down || _state == State.Up) return;

            _timer += Time.deltaTime;
            float t = Mathf.Clamp01(_timer / (riseSeconds + slideSeconds));
            keg.localPosition = Along(_state == State.Rising ? t : 1f - t);

            if (t < 1f) return;

            if (_state == State.Rising) { Play(settleClip); Begin(State.Up); }
            else Hide();
        }

        /// 0 waiting behind the board, up to over its top, then forward onto it at 1
        private Vector3 Along(float t)
        {
            float split = riseSeconds / (riseSeconds + slideSeconds);
            var over = new Vector3(0f, clearance, _waiting.z);
            if (t <= split) return Vector3.Lerp(_waiting, over, Mathf.SmoothStep(0f, 1f, t / split));

            return Vector3.Lerp(over, Vector3.zero, Mathf.SmoothStep(0f, 1f, (t - split) / (1f - split)));
        }

        private void Hide()
        {
            keg.gameObject.SetActive(false);
            hitBox.enabled = false;
            _state = State.Down;
        }

        private void Begin(State state)
        {
            _state = state;
            _timer = 0f;
        }

        private void Play(AudioClip clip)
        {
            if (clip != null && ImpactFX.Instance != null) ImpactFX.Instance.PlayClip(clip, transform.position, volume * 0.6f);
        }
    }
}
