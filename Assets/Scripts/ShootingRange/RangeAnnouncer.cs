using System.Collections;
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

        [Header("Copy (data)")]
        [SerializeField] private string pickupPrompt = "TAKE THE PISTOL";
        [SerializeField] private string clearedMessage = "WAVE CLEARED";
        [SerializeField] private string waveOverMessage = "WAVE OVER";
        [SerializeField] private string timeUpMessage = "TIME UP";
        [SerializeField] private string slainLabel = "SLAIN";
        [SerializeField] private float messageSeconds = 1.6f;

        private Coroutine _message;
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
        }

        private void OnCountdownTick(int n)
        {
            Show("ROUND " + game.NextRoundNumber + "\n" + n);
        }

        private void OnGo()
        {
            Flash("GO");
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
            Show(RoundResult());
        }

        private string RoundResult()
        {
            if (director == null) return _cleared ? clearedMessage : timeUpMessage;

            bool everyOne = director.Killed >= director.WaveSize;
            string headline = !_cleared ? timeUpMessage : everyOne ? clearedMessage : waveOverMessage;
            return headline + "\n" + director.Killed + " / " + director.WaveSize + " " + slainLabel;
        }

        private void Show(string text)
        {
            if (_message != null)
            {
                StopCoroutine(_message);
                _message = null;
            }

            if (centerText != null) centerText.text = text;
        }

        private void Flash(string text)
        {
            if (centerText == null) return;

            Show(text);
            _message = StartCoroutine(ClearAfter(messageSeconds));
        }

        private IEnumerator ClearAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            centerText.text = string.Empty;
            _message = null;
        }
    }
}
