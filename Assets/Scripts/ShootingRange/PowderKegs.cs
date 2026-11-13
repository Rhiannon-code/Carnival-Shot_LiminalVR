using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    /// Puts 2-3 kegs up at random spots on the step boards each round, and takes them down at its end (user, 2026-11-02)
    public class PowderKegs : MonoBehaviour
    {
        [SerializeField] private RangeGame game;
        [SerializeField] private PowderKeg[] kegs;
        [SerializeField, Tooltip("The step boards that face the player, set by setup step 56")] private Renderer[] boards;

        [Header("Each round (data)")]
        [SerializeField] private int fewest = 2;
        [SerializeField] private int most = 3;
        [SerializeField, Tooltip("Metres a keg keeps from a board's ends and from another keg")] private float apart = 1.2f;
        [SerializeField, Tooltip("Seconds into the round they come up, at random in this range")] private Vector2 comeUpAfter = new Vector2(1f, 6f);

        private readonly List<Vector3> _taken = new List<Vector3>();

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
            if (boards == null || boards.Length == 0) return;

            _taken.Clear();
            int count = Mathf.Min(kegs.Length, Random.Range(fewest, most + 1));
            for (int i = 0; i < count; i++)
            {
                Vector3 spot;
                Renderer board;
                if (FindASpot(out spot, out board)) StartCoroutine(RaiseLater(kegs[i], spot, board));
            }
        }

        private void OnRoundEnded(int round, int roundScore)
        {
            StopAllCoroutines();
            foreach (var keg in kegs) keg.Lower();
        }

        private IEnumerator RaiseLater(PowderKeg keg, Vector3 spot, Renderer board)
        {
            yield return new WaitForSeconds(Random.Range(comeUpAfter.x, comeUpAfter.y));
            if (!keg.Busy) keg.Raise(spot, Quaternion.LookRotation(Vector3.forward), board.bounds.size.z);
        }

        /// The middle of a board's top, somewhere along it, clear of the ends and of the other kegs. A few tries, then none
        private bool FindASpot(out Vector3 spot, out Renderer board)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                board = boards[Random.Range(0, boards.Length)];
                var b = board.bounds;
                if (b.size.x < 2f * apart) continue;

                spot = new Vector3(Random.Range(b.min.x + apart, b.max.x - apart), b.max.y, b.center.z);
                bool clear = true;
                foreach (var taken in _taken) clear &= (taken - spot).sqrMagnitude > apart * apart;
                if (!clear) continue;

                _taken.Add(spot);
                return true;
            }

            spot = Vector3.zero;
            board = null;
            return false;
        }
    }
}
