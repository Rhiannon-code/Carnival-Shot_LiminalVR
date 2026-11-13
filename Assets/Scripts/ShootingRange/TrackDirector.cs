using System;
using System.Collections.Generic;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    public class TrackDirector : MonoBehaviour
    {
        [Serializable]
        public class PatternUnlock
        {
            public TrackPatternKind pattern;
            public int fromRound = 1;
        }

        [Header("Refs")]
        [SerializeField] private TrackGrid grid;
        [SerializeField] private RangeGame game;
        [SerializeField] private TrackMover moverPrefab;

        [Header("Pool")]
        [SerializeField] private int poolSize = 32;

        [Header("Wave size (data)")]
        [SerializeField] private int[] signsPerRound = { 14, 18, 33, 39, 60 };

        [Header("Pace (data, round 1)")]
        [SerializeField] private float waveInterval = 2.2f;
        [SerializeField, Tooltip("Seconds from the last sign up going to the next batch, when that beats the wave interval")]
        private float clearBeatSeconds = 0.4f;
        [SerializeField] private float minSpeed = 0.7f;
        [SerializeField] private float maxSpeed = 1.3f;
        [SerializeField] private int concurrentTargets = 4;

        [Header("Escalation per round (data)")]
        [SerializeField] private float waveScalePerRound = 0.82f;
        [SerializeField] private float speedScalePerRound = 1.12f;
        [SerializeField] private int extraConcurrentPerRound = 2;
        [SerializeField] private int maxConcurrentTargets = 6;
        [SerializeField] private float minWaveInterval = 0.6f;

        [Header("How many stand still instead of travelling (data, 0-1)")]
        [SerializeField, Range(0f, 1f)] private float stationaryShare = 0.4f;

        [Header("Special power-up signs (data): how many each round, round 1 first, spread through it, never two out at once")]
        [SerializeField, Tooltip("A shorter list repeats its last entry. 0 is a round with no power-ups")]
        private int[] specialsPerRound = { 0, 0, 2, 2, 3 };
        [SerializeField, Range(0f, 0.5f), Tooltip("How far each can wander from its even spacing, as a share of the gap between them")]
        private float specialJitter = 0.3f;
        [SerializeField, Tooltip("A special's speed over an ordinary sign's, round 1 first. Falls as the rounds speed up, so late specials stay hittable")]
        private float[] specialSpeedPerRound = { 1.6f, 1.5f, 1.4f, 1.3f, 1.2f };

        [Header("Refusals (data)")]
        [SerializeField] private float retrySeconds = 0.75f;

        [Header("Patterns, and the round each is let loose")]
        [SerializeField]
        private PatternUnlock[] patterns =
        {
            new PatternUnlock { pattern = TrackPatternKind.Single,      fromRound = 1 },
            new PatternUnlock { pattern = TrackPatternKind.Gauntlet,    fromRound = 1 },
            new PatternUnlock { pattern = TrackPatternKind.Sweep,       fromRound = 2 },
            new PatternUnlock { pattern = TrackPatternKind.RoofRush,    fromRound = 3 },
            new PatternUnlock { pattern = TrackPatternKind.RiserVolley, fromRound = 4 },
            new PatternUnlock { pattern = TrackPatternKind.Crossfire,   fromRound = 4 },
            new PatternUnlock { pattern = TrackPatternKind.Cascade,     fromRound = 5 },
        };

        public event Action<int> TargetsLeftChanged;
        public event Action<bool> TargetResolved;

        public int Rounds { get { return signsPerRound.Length; } }
        public int ConcurrencyCap { get { return _concurrency; } }
        public TrackMover[] Movers { get { return _pool; } }
        public int WaveSize { get { return _waveSize; } }
        public int Killed { get { return _killed; } }
        public int TargetsLeft { get { return Mathf.Max(0, _waveSize - _resolved); } }

        /// Every sign of the round has been out and is off the range again, shot or escaped, reactions finished
        public bool RoundOver { get { return _launched >= _waveSize && ActiveCount() == 0; } }

        private struct Pending
        {
            public TrackRail Rail;
            public float Due;
            public float Speed;
        }

        private readonly List<TrackPattern.Launch> _launches = new List<TrackPattern.Launch>();
        private readonly List<Pending> _pending = new List<Pending>();
        private readonly List<TrackPatternKind> _unlocked = new List<TrackPatternKind>();
        private readonly List<TrackRail> _available = new List<TrackRail>();
        private readonly List<int> _specialAt = new List<int>();

        private TrackMover[] _pool;
        private float _nextWave;
        private float _waveInterval;
        private float _speedScale = 1f;
        private int _concurrency;
        private int _waveSize;
        private int _launched;
        private int _resolved;
        private int _killed;
        private int _nextSpecial;
        private int _owedSpecials;
        private float _specialSpeed = 1f;

        private void Awake()
        {
            if (moverPrefab == null)
            {
                Debug.LogError("[TrackDirector] No mover prefab assigned. Nothing will run the tracks.");
                enabled = false;
                return;
            }

            _waveInterval = waveInterval;
            _concurrency = concurrentTargets;

            _pool = new TrackMover[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                _pool[i] = Instantiate(moverPrefab, transform);
                _pool[i].gameObject.SetActive(false);
                _pool[i].Resolved += OnResolved;
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
            int steps = Mathf.Max(0, round - 1);

            int last = signsPerRound.Length - 1;
            _waveSize = last >= 0 ? Mathf.Max(1, signsPerRound[Mathf.Min(steps, last)]) : 1;
            _launched = 0;
            _resolved = 0;
            _killed = 0;

            _waveInterval = Mathf.Max(minWaveInterval, waveInterval * Mathf.Pow(waveScalePerRound, steps));
            _speedScale = Mathf.Pow(speedScalePerRound, steps);
            _concurrency = Mathf.Min(poolSize, maxConcurrentTargets, concurrentTargets + extraConcurrentPerRound * steps);

            RebuildUnlocked(round);
            RebuildAvailable(Stage());

            _pending.Clear();
            _nextWave = Time.time;
            SpreadTheSpecials(steps);
            _specialSpeed = specialSpeedPerRound != null && specialSpeedPerRound.Length > 0
                          ? specialSpeedPerRound[Mathf.Min(steps, specialSpeedPerRound.Length - 1)]
                          : 1f;
            RaiseTargetsLeft();

            if (Debug.isDebugBuild)
                Debug.Log("[TrackDirector] Round " + round + ": " + _waveSize + " targets, wave every " +
                          _waveInterval.ToString("0.00") + " s, speed x" + _speedScale.ToString("0.00") +
                          ", up to " + _concurrency + " at once, " + _unlocked.Count + " patterns over " +
                          _available.Count + " of " + grid.Rails.Length + " rails.");
        }

        /// Which launches carry a power-up: evenly through the round, each nudged a little
        private void SpreadTheSpecials(int steps)
        {
            _specialAt.Clear();
            _nextSpecial = 0;
            _owedSpecials = 0;

            int last = specialsPerRound != null ? specialsPerRound.Length - 1 : -1;
            int count = last >= 0 ? Mathf.Min(specialsPerRound[Mathf.Min(steps, last)], _waveSize) : 0;
            if (count <= 0) return;

            float gap = (float)_waveSize / count;
            for (int i = 0; i < count; i++)
            {
                float at = (i + 0.5f + UnityEngine.Random.Range(-specialJitter, specialJitter)) * gap;
                _specialAt.Add(Mathf.Clamp(Mathf.RoundToInt(at), 0, _waveSize - 1));
            }
        }

        /// Due by the count, or owed for one that got away, and no other power-up sign out. One held back goes on
        /// the next launch that's free
        private bool SpecialDue()
        {
            bool scheduled = _nextSpecial < _specialAt.Count && _launched >= _specialAt[_nextSpecial];
            if (!scheduled && _owedSpecials <= 0) return false;
            if (PowerUps.Instance == null) return false;

            for (int i = 0; i < _pool.Length; i++)
            {
                if (_pool[i].gameObject.activeSelf && _pool[i].CarriesPowerUp) return false;
            }

            return true;
        }

        private void OnRoundEnded(int round, int roundScore)
        {
            _pending.Clear();
            if (grid != null) grid.ClearTraffic();

            for (int i = 0; i < _pool.Length; i++) _pool[i].Stop();
        }

        /// How far into the session we are, as a number like 2.5 for halfway through round two. A rail
        /// can join partway through a round, so this is asked again before every wave
        private float Stage()
        {
            if (game == null) return 1f;

            float through = _waveSize > 0 ? Mathf.Clamp01((float)_launched / _waveSize) : 0f;

            return game.RoundNumber + through;
        }

        private void RebuildAvailable(float stage)
        {
            _available.Clear();
            var rails = grid != null ? grid.Rails : null;
            if (rails == null) return;

            for (int i = 0; i < rails.Length; i++)
            {
                if (rails[i] != null && stage >= rails[i].FromRound) _available.Add(rails[i]);
            }
        }

        private void RebuildUnlocked(int round)
        {
            _unlocked.Clear();
            if (patterns == null) return;

            for (int i = 0; i < patterns.Length; i++)
            {
                if (patterns[i] != null && round >= patterns[i].fromRound)
                    _unlocked.Add(patterns[i].pattern);
            }

            if (_unlocked.Count == 0) _unlocked.Add(TrackPatternKind.Single);
        }

        private void Update()
        {
            if (grid == null || game == null || game.Current != RangeGame.State.Playing) return;

            ReleaseDue();

            if (_launched + _pending.Count >= _waveSize) return;

            // Nothing left up, so the next batch comes a beat later rather than waiting out the interval
            if (_launched == _resolved && _pending.Count == 0)
                _nextWave = Mathf.Min(_nextWave, Time.time + clearBeatSeconds);

            if (Time.time < _nextWave || _pending.Count > 0) return;

            _nextWave = Time.time + _waveInterval;
            StartWave(_waveSize - _launched);
        }

        /// room is how many signs the wave still wants
        private void StartWave(int room)
        {
            RebuildAvailable(Stage());
            if (_unlocked.Count == 0) RebuildUnlocked(1);

            // The stage only moves on as signs launch, so with no rail open it never would: end the round, don't hang
            if (_available.Count == 0)
            {
                _waveSize = _launched;
                RaiseTargetsLeft();
                return;
            }

            var kind = _unlocked[UnityEngine.Random.Range(0, _unlocked.Count)];
            TrackPattern.Build(kind, _available, _launches);

            // A pattern can come up empty when the rails it wants are not in play yet
            if (_launches.Count == 0) TrackPattern.Build(TrackPatternKind.Single, _available, _launches);

            // The round's last pattern is cut short rather than running over its count
            int count = Mathf.Min(_launches.Count, room);
            if (count <= 0) return;

            float baseSpeed = UnityEngine.Random.Range(minSpeed, maxSpeed) * _speedScale;

            float now = Time.time;
            for (int i = 0; i < count; i++)
            {
                var launch = _launches[i];
                _pending.Add(new Pending
                {
                    Rail = launch.Rail,
                    Due = now + launch.Delay,
                    Speed = baseSpeed * launch.SpeedMultiplier,
                });
            }
        }

        private void ReleaseDue()
        {
            float now = Time.time;
            int cap = Mathf.Min(poolSize, _concurrency);

            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var due = _pending[i];
                if (due.Due > now) continue;

                // A launch waits as long as it takes for a slot, or a pattern bigger than the cap would
                // lose everything past its first few signs. Its retry clock starts once it has one
                if (ActiveCount() >= cap)
                {
                    due.Due = now;
                    _pending[i] = due;
                    continue;
                }

                var mover = TakeIdle();
                if (mover == null) continue;

                // Whether it travels is settled before the booking, not after, one that stands still
                // holds its spot the whole time it is up, and the booking has to know that.
                // A special always travels, and faster, so it is the hard shot that pays
                PowerUpKind? special = null;
                if (SpecialDue()) special = PowerUps.Instance.RollSpecial();

                float speed = (special.HasValue ? due.Speed * _specialSpeed : due.Speed) * due.Rail.SpeedScale;
                float hold = !special.HasValue && UnityEngine.Random.value < stationaryShare ? mover.RollHold() : 0f;

                // Somewhere new each time, in the player's sight. With nowhere to be seen from, it travels instead of hiding
                float spot = 0f;
                var head = PopUpTarget.Head();
                if (hold > 0f && head != null)
                {
                    spot = due.Rail.OpenSpot(head.position);
                    if (spot < 0f) { spot = 0f; hold = 0f; }
                }

                var pass = due.Rail.Book(now, speed, mover.SettleSeconds(due.Rail.Hinge), hold, mover.FallSeconds, spot);

                // Only the rail turning it away, again and again, gives up on a launch
                if (!grid.TryDispatch(due.Rail, pass, mover.MoverLength))
                {
                    if (now <= due.Due + retrySeconds) continue;

                    // Not counted as launched, so a later wave makes up the number
                    _pending.RemoveAt(i);
                    continue;
                }

                _pending.RemoveAt(i);
                _launched++;
                if (special.HasValue)
                {
                    if (_owedSpecials > 0) _owedSpecials--;
                    else _nextSpecial++;
                }
                mover.Launch(due.Rail, speed, hold, special, spot);
            }
        }

        private void OnResolved(TrackMover mover, bool shot)
        {
            _resolved++;
            if (shot) _killed++;
            // A missed power-up comes back on the next free launch rather than being lost (user, 2026-11-10)
            else if (mover.WasSpecial) _owedSpecials++;
            RaiseTargetsLeft();
            if (TargetResolved != null) TargetResolved(shot);
        }

        private void RaiseTargetsLeft()
        {
            if (TargetsLeftChanged != null) TargetsLeftChanged(TargetsLeft);
        }

        private int ActiveCount()
        {
            int count = 0;
            for (int i = 0; i < _pool.Length; i++)
            {
                if (_pool[i].gameObject.activeSelf) count++;
            }

            return count;
        }

        private TrackMover TakeIdle()
        {
            for (int i = 0; i < _pool.Length; i++)
            {
                if (!_pool[i].gameObject.activeSelf) return _pool[i];
            }

            return null;
        }
    }
}
