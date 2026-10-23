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
        [SerializeField] private string pickupPrompt = "TAKE THE PISTOL";
        [SerializeField] private string clearedMessage = "WAVE CLEARED";
        [SerializeField] private string timeUpMessage = "TIME UP";
        [SerializeField] private string slainLabel = "SLAIN";
        [SerializeField] private string finalScoreLabel = "FINAL SCORE";

        private bool _cleared;

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
        }

        private void Start()
        {
            if (game != null) OnStateChanged(game.Current);
        }

        private void OnStateChanged(RangeGame.State state)
        {
            if (state == RangeGame.State.WaitingForPickup) Show(pickupPrompt);

            // The controls card has the board first; later the guns are in hand and the prompt is done
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

        private void OnFinalScore(int total)
        {
            Show(finalScoreLabel + "\n<size=200%>" + total + "</size>");
            if (finalBurst != null) finalBurst.Play(true);
        }

        private string RoundResult()
        {
            string headline = _cleared ? clearedMessage : timeUpMessage;
            if (director == null) return headline;

            return headline + "\n" + director.Killed + " / " + director.WaveSize + " " + slainLabel;
        }

        private void Show(string text)
        {
            if (centerText != null) centerText.text = text;
        }
    }
}
