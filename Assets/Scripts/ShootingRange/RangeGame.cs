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

        public enum State { HowToPlay, Intro, Playing, RoundEnd, Ended }

        [Header("Refs")]
        [SerializeField] private Arena arena;
        [SerializeField] private ComboTracker combo;
        [SerializeField] private TrackDirector director;
        [SerializeField] private CarnyVoice barker;
        [SerializeField] private CarnivalLights lights;
        [SerializeField] private MusicBed music;

        [Header("Session (data)")]
        [SerializeField, Tooltip("Seconds after the triggers go live before the game starts by itself, if nobody shoots the card")]
        private float autoStartSeconds = 45f;
        [SerializeField] private int countdownFrom = 3;
        [SerializeField] private int betweenRoundsCountdownFrom = 3;
        [SerializeField] private float countdownStep = 1f;
        [SerializeField] private float roundTallySeconds = 3f;
        [SerializeField] private float endHoldSeconds = 3f;
        [SerializeField] private float longestPauseSeconds = 12f;
        [SerializeField] private float endFadeSeconds = 2f;

        [Header("Clearing (data)")]
        [SerializeField] private int clearBonus = 200;

        public State Current { get; private set; } = State.HowToPlay;
        public int RoundScore { get; private set; }
        public int TotalScore { get; private set; }
        public int RoundNumber { get; private set; }
        public int MaxRounds { get { return director != null ? director.Rounds : 0; } }
        public bool IsFinalRound { get { return MaxRounds > 0 && RoundNumber >= MaxRounds; } }
        public int NextRoundNumber { get { return RoundNumber + 1; } }

        /// The guns are in hand from the start but fire nothing until the carnival has wound up (user, 2026-11-10)
        public bool TriggersLive { get; private set; }

        public event Action<int> ScoreChanged;
        public event Action<int, Vector3> SignScored;
        public event Action<State> StateChanged;
        /// The wind-up is over: the triggers work and the controls card can be shot to start
        public event Action ReadyToStart;
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
            if (barker == null) barker = FindObjectOfType<CarnyVoice>();
            if (lights == null) lights = FindObjectOfType<CarnivalLights>();
            if (music == null) music = FindObjectOfType<MusicBed>();
            StartCoroutine(HowToPlay());
        }

        /// The controls card is up from the start. Once the lights are on and the music has wound up, the triggers
        /// work, and a shot on the card starts the game; nobody shooting it still starts it, later
        private IEnumerator HowToPlay()
        {
            SetState(State.HowToPlay);
            while ((lights != null && !lights.On) || (music != null && !music.WoundUp)) yield return null;

            TriggersLive = true;
            if (ReadyToStart != null) ReadyToStart();

            for (float waited = 0f; waited < autoStartSeconds && !_begun; waited += Time.unscaledDeltaTime) yield return null;
            Begin();
        }

        /// The controls card was shot
        public void Begin()
        {
            if (_begun || !TriggersLive) return;

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
            int scored = Mathf.RoundToInt(points * multiplier);

            AddScore(scored);
            if (linksCombo && SignScored != null) SignScored(scored, where);
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

            if (arena != null) arena.PresentRound(RoundNumber);
            if (combo != null) combo.Reset(true);

            SetState(State.Playing);
            if (RoundStarted != null) RoundStarted(RoundNumber);

            // A round is its signs: it ends when the last has been shot or got away and its reaction has played
            while (director != null && !director.RoundOver) yield return null;

            bool everySign = director != null && director.WaveSize > 0 && director.Killed >= director.WaveSize;
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

        private IEnumerator EndExperience()
        {
            SetState(State.Ended);
            if (SlowMotion.Instance != null) SlowMotion.Instance.Restore();
            if (FinalScore != null) FinalScore(TotalScore);

            // The prize: the chest waits for its lock to be shot, then shows what was won
            while (PrizeChest.Instance != null && PrizeChest.Instance.Busy) yield return null;

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
