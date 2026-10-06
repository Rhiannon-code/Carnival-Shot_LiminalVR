using System.Collections.Generic;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    public class TrackGrid : MonoBehaviour
    {
        [Header("Rails (empty = every TrackRail below this object)")]
        [SerializeField] private TrackRail[] rails;

        [Header("Junctions (data, metres)")]
        [SerializeField] private float crossTolerance = 0.4f;
        [SerializeField] private float junctionClearance = 0.7f;
        [SerializeField] private bool logJunctionsOnAwake = true;

        public TrackRail[] Rails { get { return rails; } }
        public int JunctionCount { get { return _junctions.Count; } }

        private struct Window
        {
            public float Enter;
            public float Exit;
        }

        private class Junction
        {
            public TrackRail A;
            public TrackRail B;
            public float DistanceOnA;
            public float DistanceOnB;
            public readonly List<Window> Windows = new List<Window>();
        }

        private readonly List<Junction> _junctions = new List<Junction>();
        private readonly Dictionary<TrackRail, List<Junction>> _byRail =
            new Dictionary<TrackRail, List<Junction>>();

        private void Awake()
        {
            if (rails == null || rails.Length == 0) rails = GetComponentsInChildren<TrackRail>();

            FindJunctions();

            if (logJunctionsOnAwake)
                Debug.Log("[TrackGrid] " + rails.Length + " rails, " + _junctions.Count + " junctions.");
        }

        /// Books the rail and every junction on it, or books nothing at all
        public bool TryDispatch(TrackRail rail, TrackRail.Pass pass, float moverLength)
        {
            if (rail == null) return false;
            if (!rail.CanAccept(pass)) return false;

            List<Junction> onRoute;
            if (_byRail.TryGetValue(rail, out onRoute))
            {
                float half = (moverLength + junctionClearance) * 0.5f;

                for (int i = 0; i < onRoute.Count; i++)
                {
                    if (Conflicts(onRoute[i], rail, pass, half)) return false;
                }

                for (int i = 0; i < onRoute.Count; i++)
                {
                    Window window;
                    if (WindowFor(onRoute[i], rail, pass, half, out window))
                        onRoute[i].Windows.Add(window);
                }
            }

            rail.Accept(pass);
            return true;
        }

        public void ClearTraffic()
        {
            for (int i = 0; i < _junctions.Count; i++) _junctions[i].Windows.Clear();
            if (rails == null) return;

            for (int i = 0; i < rails.Length; i++)
            {
                if (rails[i] != null) rails[i].ClearTraffic();
            }
        }

        /// When this pass is inside the junction. One that never travels only ever occupies a junction
        /// it happens to be standing on, and holds that one for as long as it stands there
        private bool WindowFor(Junction junction, TrackRail rail, TrackRail.Pass pass, float half,
                               out Window window)
        {
            float distance = junction.A == rail ? junction.DistanceOnA : junction.DistanceOnB;

            if (pass.Speed <= 0f)
            {
                window = new Window { Enter = pass.Depart, Exit = pass.Clear };
                return distance <= half;
            }

            window = new Window
            {
                // It is standing on the origin before it sets off, so a junction that close is taken
                // from the moment it appears rather than from the moment it would have reached it
                Enter = Mathf.Max(pass.Depart, pass.Moving + (distance - half) / pass.Speed),
                Exit = pass.Moving + (distance + half) / pass.Speed,
            };

            return true;
        }

        private bool Conflicts(Junction junction, TrackRail rail, TrackRail.Pass pass, float half)
        {
            Window candidate;
            if (!WindowFor(junction, rail, pass, half, out candidate)) return false;

            for (int i = junction.Windows.Count - 1; i >= 0; i--)
            {
                if (junction.Windows[i].Exit < pass.Depart) { junction.Windows.RemoveAt(i); continue; }

                var existing = junction.Windows[i];
                if (candidate.Enter < existing.Exit && existing.Enter < candidate.Exit) return true;
            }

            return false;
        }

        private void FindJunctions()
        {
            _junctions.Clear();
            _byRail.Clear();

            for (int i = 0; i < rails.Length; i++)
            {
                for (int k = i + 1; k < rails.Length; k++)
                {
                    var a = rails[i];
                    var b = rails[k];
                    if (a == null || b == null) continue;

                    float onA, onB;
                    if (!Crosses(a, b, out onA, out onB)) continue;

                    var junction = new Junction { A = a, B = b, DistanceOnA = onA, DistanceOnB = onB };
                    _junctions.Add(junction);
                    Index(a, junction);
                    Index(b, junction);
                }
            }
        }

        private void Index(TrackRail rail, Junction junction)
        {
            List<Junction> list;
            if (!_byRail.TryGetValue(rail, out list))
            {
                list = new List<Junction>();
                _byRail.Add(rail, list);
            }

            list.Add(junction);
        }

        /// Closest approach between two segments. Directions are unit length, so the usual dot
        /// products collapse to this
        private bool Crosses(TrackRail a, TrackRail b, out float onA, out float onB)
        {
            onA = 0f;
            onB = 0f;

            Vector3 da = a.Direction;
            Vector3 db = b.Direction;
            Vector3 r = a.Origin - b.Origin;

            float dot = Vector3.Dot(da, db);
            float denominator = 1f - dot * dot;

            // Parallel rails have no single crossing point, and two of them sharing a line is a
            // layout mistake rather than a junction
            if (Mathf.Abs(denominator) < 1e-5f) return false;

            float c = Vector3.Dot(da, r);
            float f = Vector3.Dot(db, r);

            float s = (dot * f - c) / denominator;
            float t = (f - dot * c) / denominator;

            if (s < 0f || s > a.Length || t < 0f || t > b.Length) return false;
            if (Vector3.Distance(a.PointAt(s), b.PointAt(t)) > crossTolerance) return false;

            onA = s;
            onB = t;
            return true;
        }
    }
}
