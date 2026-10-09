using System;
using UnityEngine;
using Liminal.SDK.VR.Avatars;

namespace IntuitiveDesigns.ShootingRange
{
    public enum Hinge { None, Up, Down, Sideways }

    public class PopUpTarget : MonoBehaviour
    {
        private enum Phase { Idle, Waiting, Rising, Settling, Up, Spinning, Falling }

        [Header("Refs")]
        [SerializeField] private Transform figure;
        [SerializeField] private Collider hitBox;

        [Header("Signs (one is picked per launch; each brings its own collider)")]
        [SerializeField] private Transform[] variants;

        [Header("Stand (optional; a sibling of the Figure, never under it, or the setup tool takes it for a sign)")]
        [SerializeField] private Transform stand;
        [SerializeField] private float standShows = 0.04f;

        [Header("Shape (data)")]
        [SerializeField] private bool sidewaysFlip;
        [SerializeField] private Vector3 hitCentreOffset = new Vector3(0f, 0.35f, 0f);

        [Header("How much of the sign clears its cover (data, metres)")]
        [SerializeField] private float peek = 0.12f;
        [SerializeField] private float nearestCover = 0.5f;

        [Header("Pop (data, seconds)")]
        [SerializeField] private float riseDelay = 0.15f;
        [SerializeField] private float riseSeconds = 0.35f;
        [SerializeField] private float fallSeconds = 0.3f;

        [Header("Wobble as it lands (data)")]
        [SerializeField] private float wobbleDegrees = 9f;
        [SerializeField] private float wobbleSeconds = 0.45f;
        [SerializeField] private float wobbleCycles = 2.5f;

        [Header("What a hit does (data, relative odds)")]
        [SerializeField] private float spinAndFallOdds = 6f;
        [SerializeField] private float fallOdds = 3f;
        [SerializeField] private float shatterOdds = 1f;

        [Header("Reaction (data)")]
        [SerializeField] private float spinTurns = 2f;
        [SerializeField] private float spinSeconds = 0.4f;

        [Header("Shatter (data)")]
        [SerializeField] private int fragments = 8;
        [SerializeField] private float fragmentImpulse = 1.4f;

        [Header("Audio (optional)")]
        [SerializeField] private AudioClip[] riseClips;
        [SerializeField] private AudioClip[] fallClips;
        [SerializeField] private AudioClip[] hitClips;
        [SerializeField, Range(0f, 1f)] private float clipVolume = 0.6f;
        [SerializeField, Range(0f, 1f)] private float voiceVolume = 0.55f;

        /// Shootable, swinging into view, settling, or up
        public bool Live
        {
            get { return _phase == Phase.Rising || _phase == Phase.Settling || _phase == Phase.Up; }
        }

        /// Far enough round to start travelling
        public bool Presented { get { return _phase == Phase.Settling || _phase == Phase.Up; } }

        /// How long from being asked to pop up to being all the way there, and how long back down
        public float TimeToRise { get { return riseDelay + riseSeconds; } }
        public float TimeToFall { get { return fallSeconds; } }

        /// The middle of the sign that is actually up, which is not a fixed spot: the signs are
        /// different sizes and only the top of one shows over its cover
        public Vector3 HitCentre
        {
            get
            {
                if (figure == null) return transform.position;

                return figure.TransformPoint(_measured ? _signMiddle : hitCentreOffset);
            }
        }

        /// The reaction has played out, so the carriage is free to go back to the pool
        public event Action Spent;

        private Collider _live;
        private Vector3 _restLocal;
        private Quaternion _facing;
        private Phase _phase;
        private Hinge _hinge;
        private float _timer;
        private float _shown;
        private Vector3 _signOffset;
        private float _swing;
        private float _fallFrom;
        private HingeShape _shape = new HingeShape { Axis = Vector3.right };
        private float _side = 1f;
        private GameObject _shards;
        private AudioClip[] _voice;
        private Transform _sign;
        private Vector3 _signMiddle;
        private float _signTop;
        private bool _measured;
        private Vector3 _standAt;
        private Quaternion _standTurn = Quaternion.identity;
        private float _standTop;

        private void Awake()
        {
            if (figure == null) figure = transform;
            if (hitBox == null) hitBox = GetComponentInChildren<Collider>();
            _live = hitBox;

            _restLocal = figure.localPosition;
            _facing = figure.rotation;

            if (stand != null) _standTop = TopOf(stand);
        }

        /// shown is how far above the carriage the sign's hinge edge sits. The hinge decides which
        /// edge that is, a floor sign tips up off its feet, a roof sign hangs upside down by its feet,
        /// a wall sign swings out on its side like a door
        public void Present(float shown, Vector3 signOffset, Hinge hinge, Renderer cover)
        {
            _shown = shown;
            _signOffset = signOffset;
            _hinge = hinge;

            var player = Player();
            if (player != null)
            {
                _facing = SquareOn(player);
                _side = SideOf(RestWorld(), player);
            }

            PickVariant();

            if (hinge == Hinge.Up) _shown = ShowJustOver(cover, shown);

            if (hinge == Hinge.None)
            {
                _swing = 0f;
                Place(0f);
                SetHitBox(true);
                _phase = Phase.Up;
                return;
            }

            _swing = _shape.Folded;
            Place(_swing);
            SetHitBox(false);

            _timer = 0f;
            _phase = Phase.Waiting;
        }

        public void Hit(Vector3 point, Vector3 direction)
        {
            SetHitBox(false);
            Play(hitClips);
            Play(_voice, voiceVolume);

            if (_hinge == Hinge.None) { Burst(point, direction); Finish(); return; }

            float odds = Mathf.Max(0.0001f, spinAndFallOdds + fallOdds + shatterOdds);
            float roll = UnityEngine.Random.value * odds;

            if (roll >= spinAndFallOdds + fallOdds) { Burst(point, direction); Finish(); return; }

            Begin(roll < spinAndFallOdds ? Phase.Spinning : Phase.Falling);
            if (ShatterPool.Instance != null) ShatterPool.Instance.PlayPop(HitCentre, direction);
        }

        /// Done with, but not shot, swings back the way it came, no spin and no shatter
        public void Lower()
        {
            SetHitBox(false);

            if (_hinge == Hinge.None) { Finish(); return; }

            Begin(Phase.Falling);
        }

        /// Off the range between rounds, and nobody hears about it
        public void Cancel()
        {
            SetHitBox(false);
            _swing = _hinge == Hinge.None ? 0f : _shape.Folded;
            Place(_swing);
            _phase = Phase.Idle;
        }

        private void Update()
        {
            switch (_phase)
            {
                case Phase.Waiting:
                    _timer += Time.deltaTime;
                    Place(_swing);
                    if (_timer >= riseDelay) StartRising();
                    break;

                case Phase.Rising:
                {
                    float t = Advance(riseSeconds);
                    _swing = Mathf.Lerp(_shape.Folded, 0f, t * t * (3f - 2f * t));
                    Place(_swing);
                    if (t >= 1f) Begin(Phase.Settling);
                    break;
                }

                case Phase.Settling:
                {
                    // Overshoots and rings down, so it arrives like a board on a spring rather than
                    // stopping dead on the mark
                    float t = Advance(wobbleSeconds);
                    float ring = Mathf.Sin(t * wobbleCycles * 2f * Mathf.PI) * (1f - t) * (1f - t);
                    _swing = Mathf.Sign(_shape.Folded) * wobbleDegrees * ring;
                    Place(_swing);
                    if (t >= 1f) { _swing = 0f; Place(0f); _phase = Phase.Up; }
                    break;
                }

                case Phase.Spinning:
                {
                    float t = Advance(spinSeconds);
                    _swing = 0f;
                    Place(0f, 360f * spinTurns * t);
                    if (t >= 1f) Begin(Phase.Falling);
                    break;
                }

                case Phase.Falling:
                {
                    float t = Advance(fallSeconds);
                    _swing = Mathf.Lerp(_fallFrom, _shape.Folded, t * t);
                    Place(_swing);
                    if (t >= 1f) Finish();
                    break;
                }
            }
        }

        private void StartRising()
        {
            Begin(Phase.Rising);
            SetHitBox(true);
            Play(riseClips);
        }

        private void Begin(Phase phase)
        {
            _phase = phase;
            _timer = 0f;

            if (phase != Phase.Falling) return;

            _fallFrom = _swing;
            Play(fallClips);
        }

        private float Advance(float duration)
        {
            _timer += Time.deltaTime;
            return duration <= 0f ? 1f : Mathf.Clamp01(_timer / duration);
        }

        private void Finish()
        {
            _swing = _hinge == Hinge.None ? 0f : _shape.Folded;
            Place(_swing);
            _phase = Phase.Idle;

            if (Spent != null) Spent();
        }

        private void Place(float swing) { Place(swing, 0f); }

        private void Place(float swing, float spin)
        {
            if (figure == null) return;

            PlaceFigure(figure, RestWorld() + Vector3.up * _shown + _signOffset, _facing, _shape, swing, spin);

            if (stand == null) return;

            stand.rotation = figure.rotation * _standTurn;
            stand.position = figure.TransformPoint(_standAt) - stand.TransformVector(0f, _standTop, 0f);
        }

        /// Puts the sign's hinge edge on home and swings the sign about it. Rotating a transform only
        /// ever turns it about its own origin, so the position is corrected by hand. Public and static so
        /// the setup tool can lay a preview out with these very sums rather than keep a second copy of
        /// them that would drift
        public static void PlaceFigure(Transform figure, Vector3 home, Quaternion facing,
                                       HingeShape shape, float swing, float spin)
        {
            if (figure == null) return;

            figure.rotation = facing * Quaternion.Euler(0f, spin, shape.Upturned ? 180f : 0f) *
                              Quaternion.AngleAxis(swing, shape.Axis);
            figure.position = home - figure.TransformVector(shape.Pivot);
        }

        private Vector3 RestWorld()
        {
            return figure.parent != null ? figure.parent.TransformPoint(_restLocal) : _restLocal;
        }

        private void PickVariant()
        {
            _measured = false;

            if (variants == null || variants.Length == 0) { SetHingeShape(Vector3.zero, Vector3.one); return; }

            int pick = UnityEngine.Random.Range(0, variants.Length);
            for (int i = 0; i < variants.Length; i++)
            {
                if (variants[i] != null) variants[i].gameObject.SetActive(i == pick);
            }

            _sign = variants[pick];

            var chosen = _sign != null ? _sign.GetComponentInChildren<Collider>() : null;
            _live = chosen != null ? chosen : hitBox;

            var broken = _sign != null ? _sign.GetComponentInChildren<SignShards>(true) : null;
            _shards = broken != null ? broken.BrokenSet : null;

            var voice = _sign != null ? _sign.GetComponentInChildren<SignVoice>(true) : null;
            _voice = voice != null ? voice.Clips : null;

            Vector3 low, high;
            _measured = Measure(variants[pick], out low, out high);
            SetHingeShape(low, high);

            // Metres, so they can be set against the room. The figure carries the scale, the carriage
            // above it does not
            _signMiddle = (low + high) * 0.5f;
            _signTop = (high.y - low.y) * figure.localScale.y;

            MountStand(low.y);
            LiftOffTheRail();
        }

        /// The stand sits in the middle of the hinge edge with its top against it and its up pointing
        /// into the sign, so it swings, spins and folds away with the sign
        private void MountStand(float feet)
        {
            if (_hinge != Hinge.Sideways)
            {
                _standAt = new Vector3(_signMiddle.x, feet, _signMiddle.z);
                _standTurn = Quaternion.identity;
                return;
            }

            _standAt = new Vector3(_shape.Pivot.x, _signMiddle.y, _signMiddle.z);
            _standTurn = Quaternion.FromToRotation(Vector3.up, _signMiddle.x > _shape.Pivot.x ? Vector3.right : Vector3.left);
        }

        /// The hinge drops into the stand, so the sign stands that far off its rail and the top of the
        /// stand shows. Metres, so the Figure's scale is divided back out
        private void LiftOffTheRail()
        {
            if (stand == null || standShows <= 0f) return;

            Vector3 up = _standTurn * Vector3.up;
            Vector3 scale = figure.localScale;
            _shape.Pivot -= new Vector3(up.x * standShows / scale.x, up.y * standShows / scale.y, 0f);
        }

        private static float TopOf(Transform root)
        {
            float top = float.NegativeInfinity;

            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;

                var box = filter.sharedMesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    var point = new Vector3(
                        (corner & 1) == 0 ? box.min.x : box.max.x,
                        (corner & 2) == 0 ? box.min.y : box.max.y,
                        (corner & 4) == 0 ? box.min.z : box.max.z);

                    top = Mathf.Max(top, root.InverseTransformPoint(filter.transform.TransformPoint(point)).y);
                }
            }

            return float.IsNegativeInfinity(top) ? 0f : top;
        }

        /// The sign stays behind its cover and shows only its top edge. Every sign is a different
        /// height and the line over the cover depends on where the player is standing, so the offset is
        /// worked out for the sign that was picked, at the moment it is asked to pop up
        private float ShowJustOver(Renderer cover, float fallback)
        {
            if (cover == null || !_measured || _signTop <= 0f) return fallback;

            float line = SightLineOver(cover);
            if (float.IsNegativeInfinity(line)) return fallback;

            return line + peek - RestWorld().y - _signTop;
        }

        private float SightLineOver(Renderer cover)
        {
            var head = Head();
            if (head == null) return float.NegativeInfinity;

            return SightLineOver(cover.bounds, RestWorld(), head.position, nearestCover);
        }

        /// How high the eye can see at a point, looking over a cover. Below that line the sign is out of
        /// sight; a sliver above it is all the warning the player gets. Public and static because the
        /// setup tool measures the rails with these very same sums, from an assumed eye rather than a
        /// real one, and the two answers have to agree
        public static float SightLineOver(Bounds cover, Vector3 point, Vector3 eye, float nearest)
        {
            Vector3 toPoint = point - eye;
            toPoint.y = 0f;

            float reach = toPoint.magnitude;
            if (reach <= 0.01f) return float.NegativeInfinity;

            float near, far;
            if (!Crosses(cover, eye, toPoint / reach, out near, out far)) return float.NegativeInfinity;

            // Nothing in the player's face is cover, and neither is anything reaching past the sign
            if (near <= nearest || far >= reach) return float.NegativeInfinity;

            // Which edge of the cover blocks the view depends on whether its top is above the eye or
            // below it: looking up, the near edge is in the way, looking down, the far one. Rather than
            // ask which, both are worked out and the one that hides less wins
            return Mathf.Max(eye.y + (cover.max.y - eye.y) * reach / near,
                             eye.y + (cover.max.y - eye.y) * reach / far);
        }

        /// Where the level line of sight enters and leaves the cover. Only the two level axes count:
        /// the question is how far along the ground the cover lies, not how tall it stands. Measuring
        /// the whole box against the line instead would call a wide wall seen at an angle metres thick
        private static bool Crosses(Bounds box, Vector3 eye, Vector3 direction,
                                    out float near, out float far)
        {
            near = float.NegativeInfinity;
            far = float.PositiveInfinity;

            return Slab(eye.x, direction.x, box.min.x, box.max.x, ref near, ref far) &&
                   Slab(eye.z, direction.z, box.min.z, box.max.z, ref near, ref far);
        }

        private static bool Slab(float from, float along, float low, float high,
                                 ref float near, ref float far)
        {
            if (Mathf.Abs(along) < 1e-5f) return from >= low && from <= high;

            float enter = (low - from) / along;
            float leave = (high - from) / along;
            if (enter > leave) { float swap = enter; enter = leave; leave = swap; }

            near = Mathf.Max(near, enter);
            far = Mathf.Min(far, leave);

            return far >= near;
        }

        private void SetHingeShape(Vector3 low, Vector3 high)
        {
            _shape = ShapeFor(_hinge, low, high, _side, sidewaysFlip);
        }

        public struct HingeShape
        {
            public Vector3 Pivot;
            public Vector3 Axis;
            public float Folded;
            public bool Upturned;
        }

        /// Where this sign's hinge edge is, and which way it swings. Each sign is a different size, so
        /// the edge is measured off the one that was picked rather than assumed
        public static HingeShape ShapeFor(Hinge hinge, Vector3 low, Vector3 high, float side, bool flip)
        {
            switch (hinge)
            {
                case Hinge.Up:
                    return new HingeShape
                    {
                        Pivot = new Vector3(0f, low.y, 0f), Axis = Vector3.right, Folded = -90f,
                    };

                case Hinge.Down:
                    // Hangs upside down by its feet, so it drops off the ceiling exactly the way a floor
                    // sign tips up off the floor
                    return new HingeShape
                    {
                        Pivot = new Vector3(0f, low.y, 0f), Axis = Vector3.right, Folded = -90f,
                        Upturned = true,
                    };

                case Hinge.Sideways:
                    // Hinged on its outer edge so it opens inward, which mirrors the two walls without
                    // either of them needing to know it is the left one or the right one. Facing the
                    // player, a sign's own +x points to the player's left, so the right-hand wall's outer
                    // edge is its low x
                    return new HingeShape
                    {
                        Pivot = new Vector3(side >= 0f ? low.x : high.x, 0f, 0f),
                        Axis = Vector3.up,
                        Folded = 90f * (side >= 0f ? 1f : -1f) * (flip ? -1f : 1f),
                    };

                default:
                    return new HingeShape { Pivot = Vector3.zero, Axis = Vector3.right, Folded = 0f };
            }
        }

        /// Every sign faces straight back down the range, like the boards in a carnival gallery. Taken
        /// from the body rather than the head, so a glance sideways never turns the signs with it
        public static Quaternion SquareOn(Transform player)
        {
            Vector3 back = Vector3.ProjectOnPlane(-player.forward, Vector3.up);

            return back.sqrMagnitude < 1e-4f ? Quaternion.identity : Quaternion.LookRotation(back, Vector3.up);
        }

        /// Which hand of the player a point is on, so a wall sign swings the way that wall should
        public static float SideOf(Vector3 point, Transform player)
        {
            return Vector3.Dot(point - player.position, player.right) >= 0f ? 1f : -1f;
        }

        public static bool Measure(Transform sign, out Vector3 low, out Vector3 high)
        {
            low = Vector3.zero;
            high = Vector3.one;
            if (sign == null) return false;

            var filter = sign.GetComponentInChildren<MeshFilter>(true);
            if (filter == null || filter.sharedMesh == null) return false;

            var box = filter.sharedMesh.bounds;
            bool first = true;

            for (int corner = 0; corner < 8; corner++)
            {
                var point = new Vector3(
                    (corner & 1) == 0 ? box.min.x : box.max.x,
                    (corner & 2) == 0 ? box.min.y : box.max.y,
                    (corner & 4) == 0 ? box.min.z : box.max.z);

                point = sign.parent.InverseTransformPoint(filter.transform.TransformPoint(point));

                if (first) { low = point; high = point; first = false; }
                else { low = Vector3.Min(low, point); high = Vector3.Max(high, point); }
            }

            return true;
        }

        private void Burst(Vector3 at, Vector3 direction)
        {
            if (ShatterPool.Instance == null) return;

            // The sign's own broken pieces where it has them, generic debris where it does not
            ShatterPool.Instance.Burst(_shards, _sign, at, direction, fragmentImpulse);
        }

        private void SetHitBox(bool on)
        {
            if (_live != null) _live.enabled = on;
        }

        private void Play(AudioClip[] clips) { Play(clips, clipVolume); }

        private void Play(AudioClip[] clips, float volume)
        {
            if (clips == null || clips.Length == 0 || ImpactFX.Instance == null) return;

            ImpactFX.Instance.PlayClip(clips[UnityEngine.Random.Range(0, clips.Length)], HitCentre, volume);
        }

        private static Transform Head()
        {
            var avatar = VRAvatar.Active;
            return avatar == null || avatar.Head == null ? null : avatar.Head.Transform;
        }

        private static Transform Player()
        {
            var avatar = VRAvatar.Active;
            return avatar == null ? null : avatar.Transform;
        }
    }
}
