using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    /// One line per round for balancing on the headset (console or logcat), development builds only
    public class RoundLog : MonoBehaviour
    {
        [SerializeField] private RangeGame game;
        [SerializeField] private TrackDirector director;
        [SerializeField] private PowerUps powerUps;
        [SerializeField] private Pistol[] pistols;

        private float _startedAt;
        private float _bottomlessSeconds;
        private int _shots;
        private int _reloads;
        private int _powerUps;

        private void OnEnable()
        {
            if (!Debug.isDebugBuild || game == null || director == null)
            {
                enabled = false;
                return;
            }

            game.RoundStarted += OnRoundStarted;
            game.RoundEnded += OnRoundEnded;
            if (powerUps != null) powerUps.Granted += OnGranted;

            foreach (var pistol in pistols)
            {
                if (pistol == null) continue;
                pistol.Fired += OnFired;
                pistol.ReloadStarted += OnReloadStarted;
            }
        }

        private void OnDisable()
        {
            if (game == null || director == null) return;

            game.RoundStarted -= OnRoundStarted;
            game.RoundEnded -= OnRoundEnded;
            if (powerUps != null) powerUps.Granted -= OnGranted;

            foreach (var pistol in pistols)
            {
                if (pistol == null) continue;
                pistol.Fired -= OnFired;
                pistol.ReloadStarted -= OnReloadStarted;
            }
        }

        private void Update()
        {
            if (game.Current == RangeGame.State.Playing && powerUps != null && powerUps.BottomlessMagazine)
                _bottomlessSeconds += Time.unscaledDeltaTime;
        }

        private void OnRoundStarted(int round)
        {
            _startedAt = Time.unscaledTime;
            _bottomlessSeconds = 0f;
            _shots = 0;
            _reloads = 0;
            _powerUps = 0;
        }

        private void OnRoundEnded(int round, int roundScore)
        {
            float seconds = Time.unscaledTime - _startedAt;
            int hit = director.Killed;

            // Shots also go on bats, spiders, kegs and the room, so this is signs per shot, not true accuracy
            Debug.Log("[RoundLog] Round " + round + ": " + seconds.ToString("0.0") + " s, signs " + hit + "/" +
                      director.WaveSize + ", shots " + _shots + " (" + (_shots > 0 ? 100f * hit / _shots : 0f).ToString("0") +
                      "% signs per shot), reloads " + _reloads + ", power-ups " + _powerUps + ", bottomless " +
                      _bottomlessSeconds.ToString("0.0") + " s, score " + roundScore + ".");
        }

        private void OnFired() { _shots++; }
        private void OnReloadStarted() { _reloads++; }
        private void OnGranted(PowerUpKind kind, float seconds, bool stacked) { _powerUps++; }
    }
}
