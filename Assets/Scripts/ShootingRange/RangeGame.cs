using System;
using System.Collections;
using UnityEngine;
using Liminal.SDK.Core;
using Liminal.Core.Fader;

namespace IntuitiveDesigns.ShootingRange
{
    public class RangeGame : MonoBehaviour
    {
        public static RangeGame Instance { get; private set; }

        public enum State { HowToPlay, WaitingForPickup, Intro, Playing, RoundEnd, Ended }

        [Header("Refs")]
        [SerializeField] private Arena arena;
        [SerializeField] private ComboTracker combo;
        [SerializeField] private TrackDirector director;
        [SerializeField] private CarnyVoice barker;

        [Header("Session (data)")]
        [SerializeField] private float[] secondsPerRound = { 30f, 30f, 45f, 45f, 60f };
        [SerializeField] private float controlsSeconds = 5f;
        [SerializeField] private int countdownFrom = 3;
        [SerializeField] private int betweenRoundsCountdownFrom = 3;
        [SerializeField] private float countdownStep = 1f;
        [SerializeField] private float roundTallySeconds = 3f;
        [SerializeField] private float endHoldSeconds = 3f;
        [SerializeField] private float longestPauseSeconds = 12f;
        [SerializeField] private float endFadeSeconds = 2f;

        [Header("Start")]
        [SerializeField] private bool requirePickupToStart = true;

        [Header("Clearing (data)")]
        [SerializeField] private int clearBonus = 200;
        [SerializeField] private bool endRoundOnClear = true;

        public State Current { get; private set; } = State.HowToPlay;
        public float TimeRemaining { get; private set; }
        public int RoundScore { get; private set; }
        public int TotalScore { get; private set; }
        public int RoundNumber { get; private set; }
        public int MaxRounds { get { return secondsPerRound.Length; } }
        public float RoundSeconds { get { return SecondsFor(RoundNumber); } }
        public bool IsFinalRound { get { return MaxRounds > 0 && RoundNumber >= MaxRounds; } }
        public int NextRoundNumber { get { return RoundNumber + 1; } }

        public event Action<int> ScoreChanged;
        public event Action<float> TimeChanged;
        public event Action<State> StateChanged;
        public event Action<int> CountdownTick;
        public event Action CountdownGo;
        public event Action<int> RoundStarted;
        public event Action<int, int> RoundEnded;
        /// Every sign in the wave was shot
        public event Action<int> RoundCleared;
        public event Action<int> FinalScore;
        public event Action<Vector3> PlayerHit;
        /// The spider got the player: the trigger is dead for that many seconds
        public event Action<Vector3, float> PlayerBitten;

        private bool _begun;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            TimeRemaining = SecondsFor(1);
            if (barker == null) barker = FindObjectOfType<CarnyVoice>();

            if (requirePickupToStart && FindObjectOfType<PistolPickup>() == null)
            {
                Debug.LogWarning("[RangeGame] requirePickupToStart is on but there is no active " +
                                 "PistolPickup in the scene. Starting immediately instead.");
                requirePickupToStart = false;
            }

            StartCoroutine(HowToPlay());
        }

        /// The controls card has the board through the barker's welcome, then the guns come up to be taken
        private IEnumerator HowToPlay()
        {
            SetState(State.HowToPlay);
            yield return StartCoroutine(Pause(controlsSeconds));

            if (requirePickupToStart) SetState(State.WaitingForPickup);
            else BeginFromPickup();
        }

        public void BeginFromPickup()
        {
            if (_begun) return;

            _begun = true;
            StartCoroutine(RunSession());
        }

        /// Only sign hits link the combo. A toppling prop stack scores a link per prop, which hands
        /// out milestones nobody aimed for
        public void Scored(int points, Vector3 where, bool chained, bool linksCombo)
        {
            if (Current != State.Playing) return;

            if (combo != null && linksCombo) combo.Register(chained);
            float multiplier = combo != null ? combo.Multiplier : 1f;

            AddScore(Mathf.RoundToInt(points * multiplier));
        }

        /// A vampire or a bat got to the player. It costs the combo and nothing else
        public void HitPlayer(Vector3 from)
        {
            if (Current != State.Playing) return;

            if (combo != null) combo.Reset(true);
            if (PlayerHit != null) PlayerHit(from);
        }

        public void Bitten(Vector3 from, float lockSeconds)
        {
            if (Current != State.Playing) return;

            if (combo != null) combo.Reset(true);
            if (PlayerBitten != null) PlayerBitten(from, lockSeconds);
        }

        private void AddScore(int points)
        {
            RoundScore += points;
            TotalScore += points;
            if (ScoreChanged != null) ScoreChanged(TotalScore);
        }

        private IEnumerator RunSession()
        {
            SetState(State.Intro);
            yield return StartCoroutine(Pause(0f));
            yield return StartCoroutine(Countdown(countdownFrom));

            for (RoundNumber = 1; RoundNumber <= MaxRounds; RoundNumber++)
            {
                yield return StartCoroutine(RunRound());

                if (RoundNumber >= MaxRounds) break;

                SetState(State.RoundEnd);
                yield return StartCoroutine(Pause(roundTallySeconds));
                yield return StartCoroutine(Countdown(betweenRoundsCountdownFrom));
            }

            yield return StartCoroutine(EndExperience());
        }

        /// At least this long, and longer while the barker is still talking, up to longestPauseSeconds
        private IEnumerator Pause(float least)
        {
            float waited = 0f;
            float most = Mathf.Max(least, longestPauseSeconds);
            while (waited < least || (barker != null && barker.Talking && waited < most))
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private IEnumerator Countdown(int from)
        {
            for (int n = from; n >= 1; n--)
            {
                if (CountdownTick != null) CountdownTick(n);
                yield return new WaitForSecondsRealtime(countdownStep);
            }

            if (CountdownGo != null) CountdownGo();
        }

        private IEnumerator RunRound()
        {
            RoundScore = 0;
            TimeRemaining = RoundSeconds;

            if (arena != null) arena.PresentRound(RoundNumber);
            if (combo != null) combo.Reset(true);

            SetState(State.Playing);
            if (RoundStarted != null) RoundStarted(RoundNumber);
            if (TimeChanged != null) TimeChanged(TimeRemaining);

            bool shelfCleared = false;

            while (TimeRemaining > 0f)
            {
                TimeRemaining -= Time.unscaledDeltaTime;
                if (TimeChanged != null) TimeChanged(Mathf.Max(0f, TimeRemaining));

                // A practice shelf can end on its plates. A range with traffic always runs to the bell:
                // its signs are paced to the clock and the last one drops away as it goes
                if (director == null && endRoundOnClear && arena != null && arena.IsCleared)
                {
                    shelfCleared = true;
                    break;
                }

                yield return null;
            }

            TimeRemaining = Mathf.Max(0f, TimeRemaining);

            bool everySign = director != null
                           ? director.WaveSize > 0 && director.Killed >= director.WaveSize
                           : shelfCleared;
            if (everySign)
            {
                AddScore(clearBonus);
                if (RoundCleared != null) RoundCleared(RoundNumber);
            }
            // A combo must not survive the break. Carried over, its next link could land on a
            // milestone and hand out a power up before the player has fired a shot
            if (combo != null) combo.Reset(true);

            if (RoundEnded != null) RoundEnded(RoundNumber, RoundScore);
        }

        private float SecondsFor(int round)
        {
            if (MaxRounds == 0) return 0f;
            return secondsPerRound[Mathf.Clamp(round - 1, 0, MaxRounds - 1)];
        }

        private IEnumerator EndExperience()
        {
            SetState(State.Ended);
            if (SlowMotion.Instance != null) SlowMotion.Instance.Restore();
            if (FinalScore != null) FinalScore(TotalScore);

            yield return StartCoroutine(Pause(endHoldSeconds));

            if (ScreenFader.Instance != null) ScreenFader.Instance.FadeTo(Color.black, endFadeSeconds);
            yield return new WaitForSecondsRealtime(endFadeSeconds);

            if (!ExperienceApp.IsEnding) ExperienceApp.End();
        }

        private void SetState(State next)
        {
            if (Current == next) return;

            Current = next;
            if (StateChanged != null) StateChanged(next);
        }
    }
}
