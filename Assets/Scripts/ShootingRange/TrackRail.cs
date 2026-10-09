using System.Collections.Generic;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{

    public enum TrackGroup
    {
        FloorAcross,
        FloorAlong,
        RoofAcross,
        RoofAlong,
        Riser,
    }

    public class TrackRail : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private TrackGroup group = TrackGroup.FloorAcross;

        [Header("Shape (data, metres)")]
        [SerializeField] private float length = 8f;

        [Header("Traffic (data)")]
        [SerializeField] private float minGap = 0.9f;

        [Header("Threat")]
        [SerializeField] private bool reachesPlayer;

        [Header("Presentation (data)")]
        [SerializeField] private float shownOffset;
        [SerializeField] private Vector3 signOffset;
        [SerializeField] private Hinge hinge = Hinge.None;

        [Header("What this rail hides behind (empty = it rides in the open)")]
        [SerializeField] private Renderer cover;

        [Header("Difficulty (1 = from the start, 2.5 = halfway through round 2)")]
        [SerializeField] private float fromRound = 1f;

        public TrackGroup Group { get { return group; } }
        public bool ReachesPlayer { get { return reachesPlayer; } }
        public float ShownOffset { get { return shownOffset; } }
        public Vector3 SignOffset { get { return signOffset; } }
        public Hinge Hinge { get { return hinge; } }
        public Renderer Cover { get { return cover; } }
        public float FromRound { get { return fromRound; } }
        public float Length { get { return length; } }
        public Vector3 Origin { get { return transform.position; } }
        public Vector3 Direction { get { return transform.forward; } }

        /// A booking on this rail. A sign stands at the origin while it swings into view, so a pass is
        /// parked from Depart until Moving and only travels after that. One that never travels keeps a
        /// Speed of zero and holds the origin until it Clears
        public struct Pass
        {
            public float Depart;
            public float Moving;
            public float Speed;
            public float Clear;
        }

        private readonly List<Pass> _passes = new List<Pass>();

        public Vector3 PointAt(float distance)
        {
            return Origin + Direction * Mathf.Clamp(distance, 0f, length);
        }

        /// holdSeconds above zero is one that stands still instead of travelling
        public Pass Book(float departTime, float speed, float settleSeconds, float holdSeconds)
        {
            float moving = departTime + Mathf.Max(0f, settleSeconds);

            if (holdSeconds > 0f)
                return new Pass { Depart = departTime, Moving = moving, Speed = 0f,
                                  Clear = moving + holdSeconds };

            float safe = Mathf.Max(0.01f, speed);
            return new Pass { Depart = departTime, Moving = moving, Speed = safe,
                              Clear = moving + length / safe };
        }

        public bool CanAccept(Pass candidate)
        {
            Prune(candidate.Depart);

            for (int i = 0; i < _passes.Count; i++)
            {
                if (!Separated(_passes[i], candidate)) return false;
            }

            return true;
        }

        public void Accept(Pass pass)
        {
            _passes.Add(pass);
        }

        public void ClearTraffic()
        {
            _passes.Clear();
        }

        private void Prune(float now)
        {
            for (int i = _passes.Count - 1; i >= 0; i--)
            {
                if (_passes[i].Clear < now) _passes.RemoveAt(i);
            }
        }

        private bool Separated(Pass a, Pass b)
        {
            float start = Mathf.Max(a.Depart, b.Depart);
            float end = Mathf.Min(a.Clear, b.Clear);
            if (end <= start) return true;

            // The gap bends at each of the two moments a pass starts travelling, and runs straight
            // between them, which is why testing the ends of those three stretches covers all of it
            float first = Mathf.Clamp(Mathf.Min(a.Moving, b.Moving), start, end);
            float second = Mathf.Clamp(Mathf.Max(a.Moving, b.Moving), start, end);

            return Clears(a, b, start, first) && Clears(a, b, first, second) &&
                   Clears(a, b, second, end);
        }

        private bool Clears(Pass a, Pass b, float from, float to)
        {
            if (to <= from) return true;

            float gapAtStart = Offset(a, from) - Offset(b, from);
            float gapAtEnd = Offset(a, to) - Offset(b, to);

            // The sign flipped, so somewhere in between the gap was zero, one overtook the other
            if (gapAtStart * gapAtEnd < 0f) return false;

            return Mathf.Min(Mathf.Abs(gapAtStart), Mathf.Abs(gapAtEnd)) >= minGap;
        }

        private static float Offset(Pass pass, float time)
        {
            return time <= pass.Moving ? 0f : (time - pass.Moving) * pass.Speed;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.8f);
            Vector3 a = Origin;
            Vector3 b = Origin + Direction * length;
            Gizmos.DrawLine(a, b);
            Gizmos.DrawWireSphere(a, 0.08f);
            Gizmos.DrawWireCube(b, Vector3.one * 0.1f);
        }
#endif
    }
}
