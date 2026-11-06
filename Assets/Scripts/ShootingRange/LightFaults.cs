using System.Collections.Generic;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    public class LightFaults : MonoBehaviour
    {
        private struct Fault
        {
            public int AtLeft;
            public bool Blackout;
        }

        [SerializeField] private RangeGame game;
        [SerializeField] private TrackDirector director;
        [SerializeField] private CarnivalLights lights;

        [Header("Blackouts (data): never in round 1")]
        [SerializeField] private int fewestBlackouts = 2;
        [SerializeField] private int mostBlackouts = 3;
        [SerializeField] private float shortestBlackout = 3f;
        [SerializeField] private float longestBlackout = 5f;

        [Header("Flickers a round, at most (data)")]
        [SerializeField] private int firstRoundFlickers = 2;
        [SerializeField] private int laterRoundFlickers = 3;

        [Header("Flicker steps (data, seconds): each swaps off and on, and it ends back on")]
        [SerializeField] private float[] smallFlicker = { 0.05f, 0.08f, 0.04f };
        [SerializeField] private float[] flicker = { 0.08f, 0.14f, 0.05f, 0.22f, 0.06f, 0.1f };

        [Header("Where in a round's signs they can come (data, 0-1)")]
        [SerializeField, Range(0f, 1f)] private float earliest = 0.15f;
        [SerializeField, Range(0f, 1f)] private float latest = 0.85f;

        private readonly List<Fault> _due = new List<Fault>();
        private int _blackoutsOwed;
        private int _round;

        /// Start, not OnEnable, so the director has its round's sign count before this reads it
        private void Start()
        {
            _blackoutsOwed = Random.Range(fewestBlackouts, mostBlackouts + 1);

            if (game != null)
            {
                game.RoundStarted += OnRoundStarted;
                game.RoundEnded += OnRoundEnded;
            }

            if (director != null) director.TargetsLeftChanged += OnTargetsLeftChanged;
        }

        private void OnDestroy()
        {
            if (game != null)
            {
                game.RoundStarted -= OnRoundStarted;
                game.RoundEnded -= OnRoundEnded;
            }

            if (director != null) director.TargetsLeftChanged -= OnTargetsLeftChanged;
        }

        private void OnRoundStarted(int round)
        {
            _round = round;
            _due.Clear();
            if (director == null || lights == null) return;

            int flickers = Random.Range(1, (round == 1 ? firstRoundFlickers : laterRoundFlickers) + 1);
            for (int i = 0; i < flickers; i++) Schedule(false);

            // Owed over the rounds left, this one included, so the count always comes out whole by the last round
            int roundsLeft = game.MaxRounds - round + 1;
            if (round > 1 && Random.value < (float)_blackoutsOwed / roundsLeft)
            {
                _blackoutsOwed--;
                Schedule(true);
            }
        }

        private void Schedule(bool blackout)
        {
            int size = director.WaveSize;
            _due.Add(new Fault { AtLeft = size - Mathf.RoundToInt(size * Random.Range(earliest, latest)), Blackout = blackout });
        }

        private void OnTargetsLeftChanged(int left)
        {
            for (int i = _due.Count - 1; i >= 0; i--)
            {
                if (left > _due[i].AtLeft) continue;

                if (_due[i].Blackout) lights.Blackout(Random.Range(shortestBlackout, longestBlackout));
                else lights.Stutter(_round == 1 ? smallFlicker : flicker);
                _due.RemoveAt(i);
            }
        }

        /// A blackout still running comes back on for the tally
        private void OnRoundEnded(int round, int roundScore)
        {
            _due.Clear();
            if (lights != null) lights.FlickerTo(true);
        }
    }
}
