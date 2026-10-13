using System.Collections;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    /// A coffin that opens and shuts by itself at random: a full creaking open, a quick peek, or a
    /// rattle as if something inside is knocking. The lid swings on the long edge it was modelled
    /// leaning against. Game time, so slow motion slows it with the rest of the world
    public class Coffin : MonoBehaviour
    {
        [Header("Measured by Fit The Lid (right-click the component; runs when it is added)")]
        [SerializeField] private Transform lid;
        [SerializeField] private Transform[] ridesTheLid;
        [SerializeField] private Vector3 hingePoint;
        [SerializeField] private Vector3 hingeAxis = Vector3.right;
        [SerializeField] private Vector3 closedPosition;
        [SerializeField] private Quaternion closedRotation = Quaternion.identity;
        [SerializeField] private Vector3[] riderPositions;
        [SerializeField] private Quaternion[] riderRotations;

        [Header("Moves (degrees and seconds)")]
        [SerializeField] private float openAngle = 105f;
        [SerializeField] private Vector2 openSeconds = new Vector2(0.9f, 1.4f);
        [SerializeField] private Vector2 stayOpenSeconds = new Vector2(1.5f, 4f);
        [SerializeField] private float slamSeconds = 0.3f;
        [SerializeField] private Vector2 peekAngle = new Vector2(12f, 25f);
        [SerializeField] private Vector2 peekSeconds = new Vector2(0.4f, 1.2f);
        [SerializeField] private float rattleAngle = 6f;
        [SerializeField] private Vector2Int rattleKnocks = new Vector2Int(3, 6);
        [SerializeField] private float bounceAngle = 3f;
        [SerializeField] private Vector2 shutSeconds = new Vector2(3f, 8f);
        [SerializeField] private Vector3 odds = new Vector3(0.4f, 0.35f, 0.25f);

        [Header("Sound")]
        [SerializeField] private AudioClip[] creaks;
        [SerializeField] private AudioClip[] thuds;
        [SerializeField] private float volume = 0.8f;
        [SerializeField] private float pitchJitter = 0.08f;
        [SerializeField] private float minAudioDistance = 1.5f;
        [SerializeField] private float maxAudioDistance = 20f;

        private AudioSource _audio;
        private float _angle;

        private void Awake()
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 1f;
            _audio.minDistance = minAudioDistance;
            _audio.maxDistance = maxAudioDistance;
        }

        private void OnEnable()
        {
            if (lid == null) return;

            Pose(0f);
            StartCoroutine(Haunt());
        }

        private IEnumerator Haunt()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(shutSeconds.x, shutSeconds.y));

                float pick = Random.value * (odds.x + odds.y + odds.z);
                if (pick < odds.x) yield return Open();
                else if (pick < odds.x + odds.y) yield return Peek();
                else yield return Rattle();
            }
        }

        private IEnumerator Open()
        {
            Play(creaks);
            yield return Swing(openAngle, Random.Range(openSeconds.x, openSeconds.y), Ease.InOut);
            yield return new WaitForSeconds(Random.Range(stayOpenSeconds.x, stayOpenSeconds.y));
            yield return Slam(slamSeconds);
        }

        private IEnumerator Peek()
        {
            Play(creaks);
            yield return Swing(Random.Range(peekAngle.x, peekAngle.y), 0.5f, Ease.InOut);
            yield return new WaitForSeconds(Random.Range(peekSeconds.x, peekSeconds.y));
            yield return Slam(0.15f);
        }

        private IEnumerator Rattle()
        {
            int knocks = Random.Range(rattleKnocks.x, rattleKnocks.y + 1);
            for (int i = 0; i < knocks; i++)
            {
                yield return Swing(rattleAngle * Random.Range(0.5f, 1f), 0.06f, Ease.Out);
                yield return Swing(0f, 0.08f, Ease.In);
                Play(thuds);
                yield return new WaitForSeconds(Random.Range(0.05f, 0.25f));
            }
        }

        /// Falls shut faster and faster, thuds, and bounces once
        private IEnumerator Slam(float seconds)
        {
            yield return Swing(0f, seconds, Ease.In);
            Play(thuds);
            yield return Swing(bounceAngle, 0.06f, Ease.Out);
            yield return Swing(0f, 0.1f, Ease.In);
        }

        private enum Ease { In, Out, InOut }

        private IEnumerator Swing(float to, float seconds, Ease ease)
        {
            float from = _angle;
            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, seconds))
            {
                float k = ease == Ease.In ? t * t : ease == Ease.Out ? 1f - (1f - t) * (1f - t) : Mathf.SmoothStep(0f, 1f, t);
                Pose(Mathf.LerpUnclamped(from, to, k));
                yield return null;
            }

            Pose(to);
        }

        /// The lid turned this many degrees open about the hinge, riders and all
        private void Pose(float angle)
        {
            _angle = angle;
            var swing = Quaternion.AngleAxis(angle, hingeAxis);
            lid.position = transform.TransformPoint(hingePoint + swing * (closedPosition - hingePoint));
            lid.rotation = transform.rotation * swing * closedRotation;

            if (ridesTheLid == null) return;

            for (int i = 0; i < ridesTheLid.Length && i < riderPositions.Length; i++)
            {
                if (ridesTheLid[i] == null) continue;

                ridesTheLid[i].position = lid.TransformPoint(riderPositions[i]);
                ridesTheLid[i].rotation = lid.rotation * riderRotations[i];
            }
        }

        private void Play(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return;

            _audio.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            _audio.PlayOneShot(clips[Random.Range(0, clips.Length)], volume);
        }

#if UNITY_EDITOR
        private void Reset()
        {
            FitTheLid();
        }

        /// Measures where the lid sits closed from the meshes, with the lid still in its modelled
        /// open pose: flat on the bottom's rim, head end over head end, outer face (the cross's side) up,
        /// hinged on the long edge it leans against. Re-run it after moving the lid by hand
        [ContextMenu("Fit The Lid")]
        private void FitTheLid()
        {
            var bottom = Child("bottom");
            if (lid == null) lid = Child("lid");
            if (ridesTheLid == null || ridesTheLid.Length == 0)
            {
                var cross = Child("cross");
                ridesTheLid = cross != null ? new[] { cross } : new Transform[0];
            }

            if (lid == null || bottom == null)
            {
                Debug.LogWarning("[Coffin] Needs a child named 'lid' and one named 'bottom' to fit to", this);
                return;
            }

            var b = new Box(this, bottom);
            var l = new Box(this, lid);

            // Outer face up: the riders sit on the outside
            Vector3 lidNormal = l.up;
            if (ridesTheLid.Length > 0 && ridesTheLid[0] != null &&
                Vector3.Dot(Local(ridesTheLid[0].position) - l.centre, lidNormal) < 0f)
                lidNormal = -lidNormal;

            Vector3 up = Vector3.Dot(l.centre - b.centre, b.up) >= 0f ? b.up : -b.up;
            var turn = Quaternion.LookRotation(up, b.Head(up)) *
                       Quaternion.Inverse(Quaternion.LookRotation(lidNormal, l.Head(lidNormal)));

            Vector3 lidPivot = Local(lid.position);
            Vector3 seat = b.centre + up * (b.Reach(up) + l.Reach(-lidNormal));
            closedPosition = seat - turn * (l.centre - lidPivot);
            closedRotation = turn * (Quaternion.Inverse(transform.rotation) * lid.rotation);

            // Hinged on the side the lid leans on now, turning so positive angles swing it over that side
            Vector3 across = Vector3.Cross(up, b.long_).normalized;
            float side = Mathf.Sign(Vector3.Dot(l.centre - b.centre, across));
            hingePoint = b.centre + up * b.Reach(up) + across * (side * b.Reach(across));
            hingeAxis = -side * b.long_;

            float modelled = Vector3.Angle(up, lidNormal);
            if (modelled > 60f && modelled < 170f) openAngle = modelled;

            riderPositions = new Vector3[ridesTheLid.Length];
            riderRotations = new Quaternion[ridesTheLid.Length];
            for (int i = 0; i < ridesTheLid.Length; i++)
            {
                if (ridesTheLid[i] == null) continue;

                riderPositions[i] = lid.InverseTransformPoint(ridesTheLid[i].position);
                riderRotations[i] = Quaternion.Inverse(lid.rotation) * ridesTheLid[i].rotation;
            }

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[Coffin] Lid fitted: opens " + openAngle.ToString("0") + " degrees on the " +
                      (side > 0f ? "+" : "-") + " side", this);
        }

        private Transform Child(string named)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t != transform && t.name.ToLowerInvariant().Contains(named)) return t;
            return null;
        }

        private Vector3 Local(Vector3 world)
        {
            return transform.InverseTransformPoint(world);
        }

        /// A part's mesh measured along its own three axes, in the coffin's space
        private struct Box
        {
            public Vector3 centre, long_, up;
            private readonly Vector3[] _points;

            public Box(Coffin coffin, Transform part)
            {
                var mesh = part.GetComponent<MeshFilter>().sharedMesh.vertices;
                _points = new Vector3[mesh.Length];
                for (int i = 0; i < mesh.Length; i++) _points[i] = coffin.Local(part.TransformPoint(mesh[i]));

                var axes = new Vector3[3];
                var size = new float[3];
                centre = Vector3.zero;
                for (int a = 0; a < 3; a++)
                {
                    var unit = new Vector3(a == 0 ? 1f : 0f, a == 1 ? 1f : 0f, a == 2 ? 1f : 0f);
                    axes[a] = coffin.transform.InverseTransformDirection(part.TransformDirection(unit)).normalized;
                    float min = float.MaxValue, max = float.MinValue;
                    foreach (var p in _points)
                    {
                        float d = Vector3.Dot(p, axes[a]);
                        min = Mathf.Min(min, d);
                        max = Mathf.Max(max, d);
                    }

                    size[a] = max - min;
                    centre += axes[a] * (min + max) * 0.5f;
                }

                int longest = 0, thinnest = 0;
                for (int a = 1; a < 3; a++)
                {
                    if (size[a] > size[longest]) longest = a;
                    if (size[a] < size[thinnest]) thinnest = a;
                }

                long_ = axes[longest];
                up = axes[thinnest];
            }

            /// How far the part reaches from its centre along a direction
            public float Reach(Vector3 direction)
            {
                float most = 0f;
                foreach (var p in _points) most = Mathf.Max(most, Vector3.Dot(p - centre, direction));
                return most;
            }

            /// Along the length toward the head: a coffin is widest at the shoulders, nearer the head
            public Vector3 Head(Vector3 normal)
            {
                var across = Vector3.Cross(normal, long_).normalized;
                float widest = -1f, at = 0f;
                foreach (var p in _points)
                {
                    float w = Mathf.Abs(Vector3.Dot(p - centre, across));
                    if (w > widest + 1e-6f)
                    {
                        widest = w;
                        at = Vector3.Dot(p - centre, long_);
                    }
                }

                return at >= 0f ? long_ : -long_;
            }
        }
#endif
    }
}
