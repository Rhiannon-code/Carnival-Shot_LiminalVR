using System.Collections.Generic;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    /// Each round the room's loose props trade places with others of their size on the same shelf, then each turns and shifts
    /// a little, so the room doesn't look the same every round. It runs once the round's restores have put them all back
    public class RoundShuffle : MonoBehaviour
    {
        [SerializeField] private Transform[] props;

        [Header("Feel (data)")]
        [SerializeField] private float turnDegrees = 30f;
        [SerializeField] private float shiftMetres = 0.03f;
        [SerializeField] private float shelfTolerance = 0.15f;
        [SerializeField] private float sizeRatio = 1.5f;

        private Vector3[] _feet;
        private float[] _heights;
        private Vector3[] _offsets;
        private Quaternion[] _rotations;
        private readonly List<List<int>> _shelves = new List<List<int>>();
        private RangeGame _game;
        private bool _pending;

        private void Start()
        {
            _feet = new Vector3[props.Length];
            _heights = new float[props.Length];
            _offsets = new Vector3[props.Length];
            _rotations = new Quaternion[props.Length];

            for (int i = 0; i < props.Length; i++)
            {
                if (props[i] == null) continue;

                var b = Footprint(props[i]);
                _feet[i] = new Vector3(b.center.x, b.min.y, b.center.z);
                _heights[i] = b.size.y;
                _offsets[i] = props[i].position - _feet[i];
                _rotations[i] = props[i].rotation;
            }

            GroupOnShelves();

            _game = RangeGame.Instance;
            if (_game != null) _game.RoundStarted += OnRoundStarted;
        }

        private void OnDestroy()
        {
            if (_game != null) _game.RoundStarted -= OnRoundStarted;
        }

        // The restores run in the same event: shuffling straight away could be undone by one that runs after
        private void OnRoundStarted(int round) { _pending = true; }

        private void LateUpdate()
        {
            if (!_pending) return;

            _pending = false;
            Shuffle();
        }

        private void GroupOnShelves()
        {
            for (int i = 0; i < props.Length; i++)
            {
                if (props[i] == null) continue;

                List<int> shelf = null;
                foreach (var group in _shelves)
                {
                    int first = group[0];
                    float taller = Mathf.Max(_heights[i], _heights[first]) / Mathf.Max(0.001f, Mathf.Min(_heights[i], _heights[first]));
                    if (Mathf.Abs(_feet[i].y - _feet[first].y) <= shelfTolerance && taller <= sizeRatio) { shelf = group; break; }
                }

                if (shelf == null) { shelf = new List<int>(); _shelves.Add(shelf); }
                shelf.Add(i);
            }
        }

        private void Shuffle()
        {
            foreach (var shelf in _shelves)
            {
                var spots = new List<int>(shelf);
                for (int k = spots.Count - 1; k > 0; k--)
                {
                    int swap = Random.Range(0, k + 1);
                    int keep = spots[k];
                    spots[k] = spots[swap];
                    spots[swap] = keep;
                }

                for (int k = 0; k < shelf.Count; k++) Place(shelf[k], spots[k]);
            }
        }

        private void Place(int prop, int spot)
        {
            var item = props[prop];
            if (item == null || !item.gameObject.activeInHierarchy) return;

            var turn = Quaternion.AngleAxis(Random.Range(-turnDegrees, turnDegrees), Vector3.up);
            var shift = Random.insideUnitCircle * shiftMetres;
            item.SetPositionAndRotation(_feet[spot] + new Vector3(shift.x, 0f, shift.y) + turn * _offsets[prop], turn * _rotations[prop]);

            foreach (var reaction in item.GetComponents<RoomReaction>()) reaction.Rehome();
        }

        private static Bounds Footprint(Transform item)
        {
            var renderers = item.GetComponentsInChildren<Renderer>();
            var all = new Bounds(item.position, Vector3.zero);
            bool any = false;
            foreach (var renderer in renderers)
            {
                if (renderer is ParticleSystemRenderer) continue;
                if (!any) { all = renderer.bounds; any = true; }
                else all.Encapsulate(renderer.bounds);
            }

            return all;
        }
    }
}
