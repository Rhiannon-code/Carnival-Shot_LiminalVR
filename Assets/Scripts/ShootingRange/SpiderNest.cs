using UnityEngine;
using Liminal.SDK.VR.Avatars;

namespace IntuitiveDesigns.ShootingRange
{
    /// Lets spiders down from the ceiling over the range, one at a time early on and more later
    public class SpiderNest : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private RangeGame game;
        [SerializeField] private Spider spiderPrefab;

        [Header("Pool")]
        [SerializeField] private int poolSize = 6;

        [Header("Where (data; the area is centred on this object, in metres)")]
        [SerializeField] private Vector2 area = new Vector2(5f, 3f);
        [SerializeField] private Vector2 hangHeight = new Vector2(1.4f, 2.5f);
        [SerializeField] private LayerMask ceilingMask = ~0;
        [SerializeField] private float ceilingFallback = 3.5f;
        [SerializeField] private float apart = 1.2f;

        [Header("Release (data)")]
        [SerializeField] private int firstRound = 1;
        [SerializeField] private float releaseInterval = 6f;
        [SerializeField] private int maxActive = 1;

        [Header("Escalation per round (data)")]
        [SerializeField] private float intervalScalePerRound = 0.85f;
        [SerializeField] private int extraActivePerRound = 1;

        [Header("Death (data)")]
        [SerializeField] private int fragments = 4;
        [SerializeField] private float fragmentImpulse = 0.8f;

        [Header("Audio")]
        [SerializeField] private AudioClip[] dropClips;
        [SerializeField] private AudioClip[] deathClips;
        [SerializeField, Range(0f, 1f)] private float volume = 0.9f;

        private Spider[] _pool;
        private float _nextRelease;
        private float _interval;
        private int _cap;

        private void Awake()
        {
            if (spiderPrefab == null)
            {
                Debug.LogError("[SpiderNest] No spider prefab assigned. No spiders will drop.");
                enabled = false;
                return;
            }

            _pool = new Spider[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                _pool[i] = Instantiate(spiderPrefab, transform);
                _pool[i].gameObject.SetActive(false);
            }
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
            int steps = Mathf.Max(0, round - firstRound);

            _interval = releaseInterval * Mathf.Pow(intervalScalePerRound, steps);
            _cap = Mathf.Min(poolSize, maxActive + extraActivePerRound * steps);
            _nextRelease = Time.time + _interval * 0.5f;
        }

        private void OnRoundEnded(int round, int roundScore)
        {
            for (int i = 0; i < _pool.Length; i++) _pool[i].Stop();
        }

        private void Update()
        {
            if (game == null || game.Current != RangeGame.State.Playing || game.RoundNumber < firstRound) return;
            if (Time.time < _nextRelease) return;

            _nextRelease = Time.time + _interval;
            if (ActiveCount() < _cap) Release();
        }

        public void Popped(Vector3 at, Vector3 direction)
        {
            if (ShatterPool.Instance != null) ShatterPool.Instance.Burst(at, fragments, direction, fragmentImpulse);
            PlayAt(deathClips, at);
        }

        private void Release()
        {
            var spider = TakeIdle();
            if (spider == null) return;

            Vector3 spot;
            if (!TrySpot(out spot)) return;

            Vector3 anchor = Ceiling(spot);
            float depth = Mathf.Max(0.3f, anchor.y - Random.Range(hangHeight.x, hangHeight.y));

            spider.Drop(this, anchor, depth, Player());
            PlayAt(dropClips, anchor);
        }

        /// A few tries at a spot clear of the spiders already hanging
        private bool TrySpot(out Vector3 spot)
        {
            for (int attempt = 0; attempt < 6; attempt++)
            {
                var local = new Vector3(Random.Range(-0.5f, 0.5f) * area.x, 0f, Random.Range(-0.5f, 0.5f) * area.y);
                spot = transform.TransformPoint(local);
                if (ClearOfOthers(spot)) return true;
            }

            spot = Vector3.zero;
            return false;
        }

        private bool ClearOfOthers(Vector3 spot)
        {
            for (int i = 0; i < _pool.Length; i++)
            {
                if (!_pool[i].Active) continue;

                var other = _pool[i].transform.position;
                if (new Vector2(other.x - spot.x, other.z - spot.z).sqrMagnitude < apart * apart) return false;
            }

            return true;
        }

        /// Straight up from about head height to the nearest thing overhead that is part of the room. A
        /// roof sign, a bat or a prop in the way is not the ceiling; with nothing found the fallback is used
        private Vector3 Ceiling(Vector3 spot)
        {
            var from = new Vector3(spot.x, 1.6f, spot.z);
            float nearest = float.MaxValue;
            foreach (var hit in Physics.RaycastAll(from, Vector3.up, 10f, ceilingMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance >= nearest || PartOfTheGame(hit.collider)) continue;
                nearest = hit.distance;
            }

            return nearest < float.MaxValue
                 ? from + Vector3.up * (nearest - 0.02f)
                 : new Vector3(spot.x, ceilingFallback, spot.z);
        }

        private static bool PartOfTheGame(Collider c)
        {
            return c.GetComponentInParent<TrackMover>() != null || c.GetComponentInParent<Spider>() != null ||
                   c.GetComponentInParent<Bat>() != null || c.GetComponentInParent<Knockable>() != null;
        }

        private static Transform Player()
        {
            var avatar = VRAvatar.Active;
            if (avatar == null || avatar.Head == null) return null;

            return avatar.Head.Transform;
        }

        private int ActiveCount()
        {
            int count = 0;
            for (int i = 0; i < _pool.Length; i++)
            {
                if (_pool[i].Active) count++;
            }

            return count;
        }

        private Spider TakeIdle()
        {
            for (int i = 0; i < _pool.Length; i++)
            {
                if (!_pool[i].Active) return _pool[i];
            }

            return null;
        }

        private void PlayAt(AudioClip[] clips, Vector3 at)
        {
            if (ImpactFX.Instance == null || clips == null || clips.Length == 0) return;
            ImpactFX.Instance.PlayClip(clips[Random.Range(0, clips.Length)], at, volume);
        }
    }
}
