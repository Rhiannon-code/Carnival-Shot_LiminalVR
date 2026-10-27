using System;
using UnityEngine;
using Liminal.SDK.VR.Avatars;
using Random = UnityEngine.Random;

namespace IntuitiveDesigns.ShootingRange
{
    /// Lets one spider down from the ceiling right in front of the player, and watches the hands and
    /// guns for a swipe that bats it out of the way. Two at once could not both be dealt with
    public class SpiderNest : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private RangeGame game;
        [SerializeField] private Spider spiderPrefab;

        [Header("Where, from the player's eyes (data, metres)")]
        [SerializeField] private float inFront = 0.7f;
        [SerializeField] private float belowEyes = 0.1f;
        [SerializeField] private LayerMask ceilingMask = ~0;
        [SerializeField] private float ceilingFallback = 3.5f;

        [Header("Release (data: the gap is counted from when the last one has gone)")]
        [SerializeField] private int firstRound = 1;
        [SerializeField] private float releaseInterval = 6f;
        [SerializeField] private float intervalScalePerRound = 0.85f;

        [Header("Swipe (data)")]
        [SerializeField] private float knockRadius = 0.2f;
        [SerializeField] private float knockSpeed = 1f;

        [Header("Death (data)")]
        [SerializeField] private int fragments = 4;
        [SerializeField] private float fragmentImpulse = 0.8f;
        [SerializeField, Range(0f, 1f), Tooltip("Smoke puff size against a sign's (1 = the same). It goes off close to the face")]
        private float popScale = 0.5f;

        [Header("Audio")]
        [SerializeField] private AudioClip[] dropClips;
        [SerializeField] private AudioClip[] deathClips;
        [SerializeField] private AudioClip[] swatClips;
        [SerializeField, Range(0f, 1f)] private float volume = 0.9f;

        [Header("Leg scratching while he hangs there (loops)")]
        [SerializeField] private AudioClip dangleLoop;
        [SerializeField, Range(0f, 1f)] private float dangleVolume = 0.35f;

        /// A spider is on its way down in front of the player
        public event Action Dropped;

        private static readonly RaycastHit[] Hits = new RaycastHit[8];

        private Spider _spider;
        private AudioSource _dangle;
        private Pistol[] _guns;
        private readonly Vector3[] _swatterWas = new Vector3[4];
        private readonly bool[] _swatterSeen = new bool[4];
        private float _nextRelease;
        private float _interval;

        private void Awake()
        {
            if (spiderPrefab == null)
            {
                Debug.LogError("[SpiderNest] No spider prefab assigned. No spiders will drop.");
                enabled = false;
                return;
            }

            _spider = Instantiate(spiderPrefab, transform);
            _spider.gameObject.SetActive(false);

            if (dangleLoop == null) return;

            _dangle = _spider.gameObject.AddComponent<AudioSource>();
            _dangle.clip = dangleLoop;
            _dangle.loop = true;
            _dangle.playOnAwake = false;
            _dangle.spatialBlend = 1f;
            _dangle.volume = dangleVolume;
        }

        private void Start()
        {
            _guns = FindObjectsOfType<Pistol>();
        }

        private void OnEnable()
        {
            if (game == null) return;

            game.RoundStarted += OnRoundStarted;
            game.RoundEnded += OnRoundEnded;
        }

        private void OnDisable()
        {
            if (game == null) return;

            game.RoundStarted -= OnRoundStarted;
            game.RoundEnded -= OnRoundEnded;
        }

        private void OnRoundStarted(int round)
        {
            _interval = releaseInterval * Mathf.Pow(intervalScalePerRound, Mathf.Max(0, round - firstRound));
            _nextRelease = Time.time + _interval * 0.5f;
        }

        private void OnRoundEnded(int round, int roundScore)
        {
            _spider.Stop();
        }

        private void Update()
        {
            WatchForSwipes();
            Scratch();

            if (game == null || game.Current != RangeGame.State.Playing || game.RoundNumber < firstRound) return;

            if (_spider.Active) { _nextRelease = Time.time + _interval; return; }
            if (Time.time >= _nextRelease) Release();
        }

        public void Popped(Vector3 at, Vector3 direction)
        {
            if (ShatterPool.Instance != null) ShatterPool.Instance.Burst(at, fragments, direction, fragmentImpulse, null, -1f, popScale);
            PlayAt(deathClips, at);
        }

        private void Release()
        {
            var head = Head();
            if (head == null) return;

            var ahead = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (ahead.sqrMagnitude < 1e-4f) ahead = Vector3.forward;

            var spot = head.position + ahead.normalized * inFront;
            Vector3 anchor = Ceiling(spot);
            float depth = Mathf.Max(0.3f, anchor.y - (head.position.y - belowEyes));

            _spider.Drop(this, anchor, depth, head);
            PlayAt(dropClips, anchor);
            if (Dropped != null) Dropped();
        }

        /// His legs scratch while he is in the way, from a random point in the loop so no two visits
        /// sound the same
        private void Scratch()
        {
            if (_dangle == null) return;

            bool inTheWay = _spider.InTheWay;
            if (inTheWay == _dangle.isPlaying) return;

            if (!inTheWay) { _dangle.Stop(); return; }

            _dangle.time = Random.Range(0f, dangleLoop.length);
            _dangle.Play();
        }

        /// Both hands and both muzzles, each tested along the whole of its last frame's path so a fast
        /// swipe cannot pass through between two frames. The player is never slowed, so neither is this
        private void WatchForSwipes()
        {
            for (int i = 0; i < _swatterWas.Length; i++)
            {
                var swatter = Swatter(i);
                if (swatter == null) { _swatterSeen[i] = false; continue; }

                Vector3 now = swatter.position;
                Vector3 was = _swatterWas[i];
                bool seen = _swatterSeen[i];
                _swatterWas[i] = now;
                _swatterSeen[i] = true;

                if (!seen || !_spider.InTheWay || Time.unscaledDeltaTime <= 0f) continue;

                Vector3 velocity = (now - was) / Time.unscaledDeltaTime;
                if (velocity.magnitude < knockSpeed) continue;
                if (Bat.DistanceToSegment(_spider.transform.position, was, now) > knockRadius) continue;

                _spider.KnockAway(velocity);
                PlayAt(swatClips, _spider.transform.position);
            }
        }

        private Transform Swatter(int i)
        {
            if (i >= 2)
            {
                var gun = _guns != null && i - 2 < _guns.Length ? _guns[i - 2] : null;
                return gun != null && gun.IsHeld ? gun.Muzzle : null;
            }

            var avatar = VRAvatar.Active;
            if (avatar == null) return null;

            var hand = i == 0 ? avatar.PrimaryHand : avatar.SecondaryHand;
            return hand != null ? hand.Transform : null;
        }

        /// Straight up from eye height to the nearest thing overhead that is part of the room. A roof
        /// sign, a bat or a prop in the way is not the ceiling; with nothing found the fallback is used
        private Vector3 Ceiling(Vector3 spot)
        {
            float nearest = float.MaxValue;
            int count = Physics.RaycastNonAlloc(spot, Vector3.up, Hits, 10f, ceilingMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (Hits[i].distance >= nearest || PartOfTheGame(Hits[i].collider)) continue;
                nearest = Hits[i].distance;
            }

            return nearest < float.MaxValue
                 ? spot + Vector3.up * (nearest - 0.02f)
                 : new Vector3(spot.x, ceilingFallback, spot.z);
        }

        private static bool PartOfTheGame(Collider c)
        {
            return c.GetComponentInParent<TrackMover>() != null || c.GetComponentInParent<Spider>() != null ||
                   c.GetComponentInParent<Bat>() != null || c.GetComponentInParent<Knockable>() != null;
        }

        /// The headset, or the desktop camera when there is none
        private static Transform Head()
        {
            var avatar = VRAvatar.Active;
            if (avatar != null && avatar.Head != null && avatar.Head.Transform != null) return avatar.Head.Transform;

            return Camera.main != null ? Camera.main.transform : null;
        }

        private void PlayAt(AudioClip[] clips, Vector3 at)
        {
            if (ImpactFX.Instance == null || clips == null || clips.Length == 0) return;
            ImpactFX.Instance.PlayClip(clips[Random.Range(0, clips.Length)], at, volume);
        }
    }
}
