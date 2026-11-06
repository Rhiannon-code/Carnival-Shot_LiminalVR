using System;
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
        [SerializeField] private float speedScale = 1f;

        [Header("Threat")]
        [SerializeField] private bool reachesPlayer;

        [Header("Presentation (data)")]
        [SerializeField] private float shownOffset;
        [SerializeField] private Vector3 signOffset;
        [SerializeField] private Hinge hinge = Hinge.None;

        [Header("What this rail hides behind (empty = it rides in the open)")]
        [SerializeField] private Renderer cover;

        [Header("What a side sign folds flat against: the pillar and anything on its face (Sideways only)")]
        [SerializeField] private Renderer[] mount;

        [Header("Tallest sign this rail carries (data, metres, 0 = any)")]
        [SerializeField] private float tallestSign;

        [Header("The bulbs that run toward the launch point as a sign comes up (optional)")]
        [SerializeField] private RailBulbs bulbs;

        [Header("Where a sign that stands still comes up (data, metres)")]
        [SerializeField, Tooltip("Stays this far from both ends of the rail")] private float spotMargin = 0.5f;
        [SerializeField, Tooltip("How far off the rail to check the player can see: about the middle of a sign")] private float sightHeight = 0.8f;
        [SerializeField, Tooltip("How far either side of that to check, so half a sign can't be behind a pillar")] private float sightWidth = 0.3f;

        [Header("Difficulty (1 = from the start, 2.5 = halfway through round 2)")]
        [SerializeField] private float fromRound = 1f;

        public TrackGroup Group { get { return group; } }
        public bool ReachesPlayer { get { return reachesPlayer; } }
        public float ShownOffset { get { return shownOffset; } }
        public Vector3 SignOffset { get { return signOffset; } }
        public Hinge Hinge { get { return hinge; } }
        public Renderer Cover { get { return cover; } }
        public float TallestSign { get { return tallestSign; } }
        public float FromRound { get { return fromRound; } }
        public float Length { get { return length; } }
        public float SpeedScale { get { return speedScale; } }
        public Vector3 Origin { get { return transform.position; } }
        public Vector3 Direction { get { return transform.forward; } }

        /// A booking on this rail. A sign stands at the origin while it swings into view, so a pass is
        /// parked from Depart until Moving and only travels after that. One that never travels keeps a
        /// Speed of zero and holds its Spot, metres along the rail, until it Clears
        public struct Pass
        {
            public float Depart;
            public float Moving;
            public float Speed;
            public float Clear;
            public float Spot;
        }

        private const int SpotTries = 6;
        private const int NoSpot = -1;
        private const float IgnoreNearHead = 0.5f;
        private static readonly RaycastHit[] SightHits = new RaycastHit[16];

        private readonly List<Pass> _passes = new List<Pass>();

        /// Everything the side sign lies against, as one box, or false when nothing is wired
        public bool Mounted(out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;
            if (mount == null) return false;

            for (int i = 0; i < mount.Length; i++)
            {
                if (mount[i] == null) continue;
                if (any) bounds.Encapsulate(mount[i].bounds);
                else { bounds = mount[i].bounds; any = true; }
            }

            return any;
        }

        /// A sign is about to come up at the origin, seconds from now
        public void Warn(float seconds)
        {
            if (bulbs != null) bulbs.Chase(seconds);
        }

        /// A random spot for a sign that stands still, metres along the rail, that the player can see from head. Below
        /// zero when none turned up, so it can travel instead of standing hidden. Only floor and roof rails: a side
        /// sign's place is on its pillar, so it stays at 0
        public float OpenSpot(Vector3 head)
        {
            if (hinge != Hinge.Up && hinge != Hinge.Down) return 0f;

            Vector3 toSign = (hinge == Hinge.Up ? Vector3.up : Vector3.down) * sightHeight;
            for (int i = 0; i < SpotTries; i++)
            {
                float spot = UnityEngine.Random.Range(spotMargin, Mathf.Max(spotMargin, length - spotMargin));
                Vector3 middle = PointAt(spot) + toSign;
                if (InSight(head, middle) && InSight(head, middle + Direction * sightWidth) && InSight(head, middle - Direction * sightWidth))
                    return spot;
            }

            return NoSpot;
        }

        /// Only solid room counts: rounds go on through props and other signs, and the guns in the hands don't block anything
        private static bool InSight(Vector3 from, Vector3 to)
        {
            Vector3 line = to - from;
            float distance = line.magnitude;
            int count = Physics.RaycastNonAlloc(from, line / distance, SightHits, distance, ~0, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                var hit = SightHits[i];
                if (hit.distance < IgnoreNearHead || hit.rigidbody != null) continue;
                if (hit.collider.GetComponentInParent<IShootable>() == null) return false;
            }

            return true;
        }

        public Vector3 PointAt(float distance)
        {
            return Origin + Direction * Mathf.Clamp(distance, 0f, length);
        }

        /// holdSeconds above zero is one that stands still at spot instead of travelling. Either way the sign
        /// drops out of sight where it stops, and the spot is not free until it has
        public Pass Book(float departTime, float speed, float settleSeconds, float holdSeconds, float fallSeconds, float spot)
        {
            float moving = departTime + Mathf.Max(0f, settleSeconds);
            float fall = Mathf.Max(0f, fallSeconds);

            if (holdSeconds > 0f)
                return new Pass { Depart = departTime, Moving = moving, Speed = 0f,
                                  Clear = moving + holdSeconds + fall, Spot = spot };

            float safe = Mathf.Max(0.01f, speed);
            return new Pass { Depart = departTime, Moving = moving, Speed = safe,
                              Clear = moving + length / safe + fall };
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

        private static readonly float[] Bends = new float[4];

        private bool Separated(Pass a, Pass b)
        {
            float start = Mathf.Max(a.Depart, b.Depart);
            float end = Mathf.Min(a.Clear, b.Clear);
            if (end <= start) return true;

            // The gap bends where either pass starts travelling and where either stops at the end to
            // fall, and runs straight between, which is why testing the ends of those stretches covers it
            Bends[0] = a.Moving;
            Bends[1] = b.Moving;
            Bends[2] = Arrival(a);
            Bends[3] = Arrival(b);
            Array.Sort(Bends);

            float from = start;
            for (int i = 0; i < Bends.Length; i++)
            {
                float to = Mathf.Clamp(Bends[i], start, end);
                if (!Clears(a, b, from, to)) return false;
                from = to;
            }

            return Clears(a, b, from, end);
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

        private float Arrival(Pass pass)
        {
            return pass.Speed > 0f ? pass.Moving + length / pass.Speed : pass.Moving;
        }

        private float Offset(Pass pass, float time)
        {
            if (pass.Speed <= 0f) return pass.Spot;

            return time <= pass.Moving ? 0f : Mathf.Min(length, (time - pass.Moving) * pass.Speed);
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
