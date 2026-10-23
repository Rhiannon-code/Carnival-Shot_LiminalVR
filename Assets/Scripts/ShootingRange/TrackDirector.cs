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

        [Header("Special power-up signs (data)")]
        [SerializeField, Range(0f, 1f)] private float specialShare = 0.1f;
        [SerializeField] private float specialSpeed = 1.6f;

        [Header("Paced to the bell (data, seconds, the last sign's run ends on it, the rest lead up to it)")]
        [SerializeField] private float shortestShowSeconds = 1f;
        [SerializeField] private float mostHurry = 2f;

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

        public int ConcurrencyCap { get { return _concurrency; } }
        public TrackMover[] Movers { get { return _pool; } }
        public int WaveSize { get { return _waveSize; } }
        public int Killed { get { return _killed; } }
        public int TargetsLeft { get { return Mathf.Max(0, _waveSize - _resolved); } }

        private struct Pending
        {
            public TrackRail Rail;
            public float Due;
            public float Speed;
            public bool Final;
        }

        private readonly List<TrackPattern.Launch> _launches = new List<TrackPattern.Launch>();
        private readonly List<Pending> _pending = new List<Pending>();
        private readonly List<TrackPatternKind> _unlocked = new List<TrackPatternKind>();
        private readonly List<TrackRail> _available = new List<TrackRail>();

        private TrackMover[] _pool;
        private float _nextWave;
        private float _waveInterval;
        private float _speedScale = 1f;
        private int _concurrency;
        private int _waveSize;
        private int _launched;
        private int _resolved;
        private int _killed;
        private TrackRail _finalRail;
        private float _finalSpeed;
        private float _finalAt;
        private bool _finalQueued;

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
            PlanTheFinalSign(round);
            RebuildAvailable(Stage());

            _pending.Clear();
            _finalQueued = false;
            _nextWave = Time.time;
            RaiseTargetsLeft();

            if (Debug.isDebugBuild)
                Debug.Log("[TrackDirector] Round " + round + ": " + _waveSize + " targets, wave every " +
                          _waveInterval.ToString("0.00") + " s, speed x" + _speedScale.ToString("0.00") +
                          ", up to " + _concurrency + " at once, " + _unlocked.Count + " patterns over " +
                          _available.Count + " of " + grid.Rails.Length + " rails.");
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

            float through = game.RoundSeconds > 0f
                          ? Mathf.Clamp01(1f - game.TimeRemaining / game.RoundSeconds)
                          : 0f;

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

            // The signs before the last, out or waiting. The last queues as soon as they all have, and
            // waits in ReleaseDue for its moment
            int before = _launched + _pending.Count - (_finalQueued ? 1 : 0);
            if (before >= _waveSize - 1)
            {
                if (!_finalQueued && before + 1 <= _waveSize) QueueTheFinalSign();
                return;
            }

            // Never ahead of the clock, so a quick shot cannot empty the wave before the bell
            if (Time.time < _nextWave || before >= OnTheClock(_waveInterval)) return;

            _nextWave = Time.time + (Behind() > 0 ? minWaveInterval : _waveInterval);
            StartWave(_waveSize - 1 - before);
        }

        /// room is how many signs the wave still wants before its last
        private void StartWave(int room)
        {
            RebuildAvailable(Stage());
            if (_unlocked.Count == 0) RebuildUnlocked(1);

            var kind = _unlocked[UnityEngine.Random.Range(0, _unlocked.Count)];
            TrackPattern.Build(kind, _available, _launches);

            // A pattern can come up empty when the rails it wants are not in play yet
            if (_launches.Count == 0) TrackPattern.Build(TrackPatternKind.Single, _available, _launches);

            // The last pattern before the final sign is cut short rather than taking its place
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
            int cap = Mathf.Min(poolSize, _concurrency + Behind());

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

                // The wave's last sign waits until a run at its own pace would end on the bell, and its
                // retry clock starts from then
                bool last = due.Final;
                if (last && Lifetime(due.Rail, mover, due.Speed * due.Rail.SpeedScale, 0f) < game.TimeRemaining)
                {
                    due.Due = now;
                    _pending[i] = due;
                    continue;
                }

                // Whether it travels is settled before the booking, not after, one that stands still
                // holds the rail's origin the whole time it is up, and the booking has to know that.
                // A special always travels, and faster, so it is the hard shot that pays. The last sign
                // always travels to the end of its rail and is never a special: fitted to the bell it
                // could come out slower than a special should
                var powerUps = PowerUps.Instance;
                PowerUpKind? special = null;
                if (powerUps != null && !last && UnityEngine.Random.value < specialShare) special = powerUps.RollSpecial();

                float speed = (special.HasValue ? due.Speed * specialSpeed : due.Speed) * due.Rail.SpeedScale;
                float hold = !special.HasValue && !last && UnityEngine.Random.value < stationaryShare ? mover.RollHold() : 0f;

                float deadline;
                if (!FitToTheBell(due.Rail, mover, last, ref speed, ref hold, out deadline))
                {
                    // Too late to show it fairly, so the wave is a sign shorter rather than one that
                    // cannot be cleared
                    _pending.RemoveAt(i);
                    if (last) _finalQueued = false;
                    _waveSize--;
                    RaiseTargetsLeft();
                    continue;
                }

                var pass = due.Rail.Book(now, speed, mover.SettleSeconds(due.Rail.Hinge), hold, mover.FallSeconds);

                // Only the rail turning it away, again and again, gives up on a launch
                if (!grid.TryDispatch(due.Rail, pass, mover.MoverLength))
                {
                    if (now <= due.Due + retrySeconds) continue;

                    // A turned-away last sign is queued again, on any open rail
                    _pending.RemoveAt(i);
                    if (last) _finalQueued = false;
                    continue;
                }

                _pending.RemoveAt(i);
                _launched++;
                mover.Launch(due.Rail, speed, hold, special, deadline);
            }
        }

        /// From launch to out of sight: swinging up, the run or the stand, and dropping away
        private static float Lifetime(TrackRail rail, TrackMover mover, float speed, float hold)
        {
            float run = hold > 0f ? hold : rail.Length / Mathf.Max(0.01f, speed);
            return mover.SettleSeconds(rail.Hinge) + run + mover.FallSeconds;
        }

        /// Every sign is out of sight by the bell. One that would still be up is hurried, or turned into
        /// one that stands for the time there is when hurrying it would be unfair, and the last sign of
        /// the wave is always fitted so it drops away exactly as the bell goes. False when there is too
        /// little time left to show it at all. deadline is when its run has to end, unscaled, or 0
        private bool FitToTheBell(TrackRail rail, TrackMover mover, bool last,
                                  ref float speed, ref float hold, out float deadline)
        {
            deadline = 0f;
            float left = game.TimeRemaining;
            if (!last && Lifetime(rail, mover, speed, hold) <= left) return true;

            float room = left - mover.SettleSeconds(rail.Hinge) - mover.FallSeconds;
            if (room < shortestShowSeconds) return false;

            if (hold <= 0f && rail.Length / room > speed * mostHurry) hold = room;

            if (hold > 0f) hold = room;
            else speed = rail.Length / room;

            deadline = Time.unscaledTime + left - mover.FallSeconds;
            return true;
        }

        /// The rail and pace of the round's last sign, chosen as the round starts so every launch before
        /// it can be spread to lead into it. It goes when that run would end on the bell, so the round
        /// closes with it being shot or dropping away at the end of its rail as the clock hits 0
        private void PlanTheFinalSign(int round)
        {
            // Every rail that will be in play by the end of the round, not just the ones open now
            RebuildAvailable(round + 0.99f);
            _finalRail = _available.Count > 0 ? _available[UnityEngine.Random.Range(0, _available.Count)] : null;
            _finalSpeed = UnityEngine.Random.Range(minSpeed, maxSpeed) * _speedScale;
            _finalAt = _finalRail != null && game != null
                     ? Mathf.Max(0f, game.RoundSeconds - Lifetime(_finalRail, _pool[0], _finalSpeed * _finalRail.SpeedScale, 0f))
                     : 0f;
        }

        /// The planned last sign, or any open rail if the plan was turned away and this is a second try
        private void QueueTheFinalSign()
        {
            var rail = _finalRail;
            _finalRail = null;

            if (rail == null)
            {
                RebuildAvailable(Stage());
                if (_available.Count == 0) return;
                rail = _available[UnityEngine.Random.Range(0, _available.Count)];
            }

            _pending.Add(new Pending { Rail = rail, Due = Time.time, Speed = _finalSpeed, Final = true });
            _finalQueued = true;
        }

        /// How many of the signs before the last the clock wants out by now, plus lookAhead seconds. They
        /// go at an even gap from the start of the round, and one more gap brings the last sign's launch
        private int OnTheClock(float lookAhead)
        {
            int spread = Mathf.Max(0, _waveSize - 1);
            if (game == null || spread == 0 || _finalAt <= 0f) return spread;

            float gap = _finalAt / spread;
            float elapsed = game.RoundSeconds - game.TimeRemaining + lookAhead;
            return Mathf.Clamp(Mathf.FloorToInt(elapsed / gap) + 1, 0, spread);
        }

        /// How many launches short of the clock this round is. Behind it, waves come at the shortest gap
        /// and the cap on targets up at once lifts by the shortfall until it catches up
        private int Behind()
        {
            return Mathf.Max(0, OnTheClock(0f) - _launched);
        }

        private void OnResolved(TrackMover mover, bool shot)
        {
            _resolved++;
            if (shot) _killed++;
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
