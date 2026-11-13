using UnityEngine;
using TMPro;

namespace IntuitiveDesigns.ShootingRange
{
    /// Big messages out on the range, where the player is already looking
    public class RangeAnnouncer : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private RangeGame game;
        [SerializeField] private TrackDirector director;

        [Header("Texts")]
        [SerializeField] private TMP_Text centerText;

        [Header("Final score flourish")]
        [SerializeField] private ParticleSystem finalBurst;

        [Header("Copy (data)")]
        [SerializeField] private string clearedMessage = "WAVE CLEARED";
        [SerializeField] private string roundOverMessage = "ROUND OVER";
        [SerializeField] private string slainLabel = "SLAIN";
        [SerializeField] private string finalScoreLabel = "FINAL SCORE";
        [SerializeField, Tooltip("On the board while the prize chest waits to be shot; the score shows once it's open")]
        private string shootTheLock = "SHOOT THE LOCK!";
        [SerializeField, Tooltip("Seconds the final score takes to count up once the chest is open")] private float tallySeconds = 2.5f;

        private bool _cleared;
        private int _total;

        private void OnEnable()
        {
            if (game != null)
            {
                game.StateChanged += OnStateChanged;
                game.CountdownTick += OnCountdownTick;
                game.CountdownGo += OnGo;
                game.RoundStarted += OnRoundStarted;
                game.RoundCleared += OnRoundCleared;
                game.RoundEnded += OnRoundEnded;
                game.FinalScore += OnFinalScore;
            }
            PrizeChest.Opened += OnChestOpened;
        }

        private void OnDisable()
        {
            if (game != null)
            {
                game.StateChanged -= OnStateChanged;
                game.CountdownTick -= OnCountdownTick;
                game.CountdownGo -= OnGo;
                game.RoundStarted -= OnRoundStarted;
                game.RoundCleared -= OnRoundCleared;
                game.RoundEnded -= OnRoundEnded;
                game.FinalScore -= OnFinalScore;
            }
            PrizeChest.Opened -= OnChestOpened;
        }

        private void Start()
        {
            if (game != null) OnStateChanged(game.Current);
        }

        private void OnStateChanged(RangeGame.State state)
        {
            // The controls card has the board until the game starts
            if (state == RangeGame.State.HowToPlay || state == RangeGame.State.Intro) Show(string.Empty);
        }

        /// The count itself is on the skulls' eyes (CountdownSkulls); the board only says which round is coming
        private void OnCountdownTick(int n)
        {
            Show("ROUND " + game.NextRoundNumber);
        }

        private void OnGo()
        {
            Show(string.Empty);
        }

        private void OnRoundStarted(int round)
        {
            _cleared = false;
        }

        private void OnRoundCleared(int round)
        {
            _cleared = true;
        }

        private void OnRoundEnded(int round, int roundScore)
        {
            Show(RoundResult());
        }

        /// The score stays a surprise until the chest is shot open (user, 2026-11-10); with no chest it shows straight away
        private void OnFinalScore(int total)
        {
            _total = total;
            if (PrizeChest.Instance != null) { Show(shootTheLock); return; }
            ShowTheScore();
        }

        private void OnChestOpened()
        {
            StartCoroutine(Tally());
        }

        /// Counts up fast and slows into the total, then the burst
        private System.Collections.IEnumerator Tally()
        {
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(0.01f, tallySeconds))
            {
                float eased = 1f - (1f - t) * (1f - t);
                Show(finalScoreLabel + "\n<size=200%>" + Mathf.RoundToInt(_total * eased) + "</size>");
                yield return null;
            }

            ShowTheScore();
        }

        private void ShowTheScore()
        {
            Show(finalScoreLabel + "\n<size=200%>" + _total + "</size>");
            if (finalBurst != null) finalBurst.Play(true);
        }

        private string RoundResult()
        {
            string headline = _cleared ? clearedMessage : roundOverMessage;
            if (director == null) return headline;

            return headline + "\n" + director.Killed + " / " + director.WaveSize + " " + slainLabel;
        }

        private void Show(string text)
        {
            if (centerText != null) centerText.text = text;
        }
    }
}
