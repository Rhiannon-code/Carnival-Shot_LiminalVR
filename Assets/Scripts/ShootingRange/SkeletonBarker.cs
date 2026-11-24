using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Liminal.SDK.VR.Avatars;
using UnityEngine;
using Random = UnityEngine.Random;

namespace IntuitiveDesigns.ShootingRange
{
    public class SkeletonBarker : RoomReaction
    {
        [SerializeField] private RangeGame game;
        [SerializeField] private TrackDirector director;
        [SerializeField] private Coffin coffin;
        [SerializeField] private Rigidbody stand;
        [SerializeField] private Rigidbody skull;
        [SerializeField] private Transform jaw;
        [SerializeField] private Rigidbody hat;
        [SerializeField] private Vector3 outward = Vector3.forward;

        [Header("In and out of the coffin (data): limp, so he flops")]
        [Header("Where he stops (data): beside the player's head as it is when he comes out")]
        [SerializeField] private float besidePlayer = 1.1f;
        [SerializeField] private float aheadOfPlayer = 1f;
        [SerializeField] private float aboveEyes = 0.15f;
        [SerializeField] private float feetAboveFloor = 0.05f;
        [SerializeField]
        private Vector3 playerFaces = Vector3.forward;
        [SerializeField]
        private Vector3 carry = Vector3.forward;
        [SerializeField] private float shoveSeconds = 0.45f;
        [SerializeField] private float yankSeconds = 0.35f;
        [SerializeField] private float settleSeconds = 0.6f;
        [SerializeField, Range(0f, 0.2f)]
        private float lungeWindUp = 0.05f;
        [SerializeField]
        private float boingHeight = 0.15f;
        [SerializeField] private int boingWobbles = 3;
        [SerializeField] private float engageSeconds = 0.5f;
        [SerializeField] private float homeEngageSeconds = 0.2f;
        [SerializeField] private float holdStrength = 150f;
        [SerializeField] private float holdDamping = 18f;
        [SerializeField] private float leanDegrees = 8f;

        [Header("Cartoon servo")]
        [SerializeField, Range(0f, 0.3f)] private float anticipation = 0.12f;
        [SerializeField, Range(0f, 0.5f)] private float overshoot = 0.25f;
        [SerializeField] private int wobbles = 2;
        [SerializeField, Range(0f, 0.05f)] private float motorShake = 0.008f;
        [SerializeField] private Vector2Int hitchesPerMove = new Vector2Int(0, 1);
        [SerializeField, Tooltip("Seconds")] private Vector2 hitchSeconds = new Vector2(0.06f, 0.18f);
        [SerializeField] private Vector2 hitchBetween = new Vector2(0.15f, 0.85f);
        [SerializeField] private AudioClip[] clunkClips;
        [SerializeField, Range(0f, 1f)] private float clunkVolume = 0.5f;
        [SerializeField] private AudioClip[] whirClips;
        [SerializeField, Range(0f, 1f)] private float whirVolume = 0.25f;
        [SerializeField] private AudioClip[] stopClips;
        [SerializeField, Range(0f, 1f)] private float stopVolume = 0.06f;

        [Header("Looking at the player (data)")]
        [SerializeField] private float lookEverySeconds = 0.8f;
        [SerializeField] private float lookTurnSeconds = 0.35f;
        [SerializeField] private float headTilt = 12f;
        [SerializeField] private float lookLimit = 45f;
        [SerializeField]
        private float lookBelow = 0.25f;
        [SerializeField] private float lookStrength = 150f;
        [SerializeField] private float lookDamping = 18f;

        [Header("Hat tip (data): only after a good round")]
        [SerializeField, Range(0f, 1f)] private float tipShare = 0.75f;
        [SerializeField] private float reachSeconds = 0.6f;
        [SerializeField] private float sweepSeconds = 0.6f;
        [SerializeField] private float holdSeconds = 0.6f;
        [SerializeField]
        private Vector2 clearOfHim = new Vector2(0.18f, 0.15f);
        [SerializeField]
        private Vector3 sweepTo = new Vector3(0.25f, -0.35f, 0.4f);
        [SerializeField] private float bowDegrees = 15f;

        [Header("Mid-round gag (data): an arm out sideways through the cracked lid, waving")]
        [SerializeField, Range(0f, 1f)] private float waveChance = 0.6f;
        [SerializeField, Range(0.5f, 1f)] private float waveReach = 0.95f;
        [SerializeField] private Vector2 waveLean = new Vector2(0.15f, 0.15f);
        [SerializeField] private float waveDegrees = 40f;
        [SerializeField] private float waveGapDegrees = 30f;
        [SerializeField]
        private float palmBackDegrees = 33f;
        [SerializeField] private int waves = 3;
        [SerializeField] private float waveSeconds = 0.4f;
        [SerializeField] private float waveReachSeconds = 0.5f;

        [Header("Miming (data)")]
        [SerializeField] private float jawOpenDegrees = 22f;
        [SerializeField] private float fullOpenLoudness = 0.12f;
        [SerializeField, Tooltip("Seconds")] private float jawSmoothing = 0.05f;

        [Header("Eye glow (data): steady in his sockets, brighter with each word")]
        [SerializeField] private Renderer[] eyeGlows;
        [SerializeField] private float eyeGlowQuiet = 0.9f;
        [SerializeField] private float eyeGlowLoud = 2.4f;

        [Header("Shot (data)")]
        [SerializeField] private float shotImpulse = 0.8f;
        [SerializeField] private float joltDegrees = 6f;
        [SerializeField] private float joltSeconds = 0.25f;
        [Header("Shot while out on his stand: snapped back away from the shot, then eased upright")]
        [SerializeField] private float recoilDegrees = 25f;
        [SerializeField] private float recoilSeconds = 0.6f;
        [SerializeField, Range(0.05f, 0.5f)] private float recoilSnapShare = 0.15f;
        [SerializeField] private AudioClip[] hitClips;
        [SerializeField, Range(0f, 1f)] private float hitVolume = 0.6f;

        private const int Samples = 256;
        // An explicit spring blows up once a step overshoots: damping past about 2/dt, or stiffness past about 1/dt². These
        // keep the hold and the look well inside both at whatever step the physics runs (0.03 s here, less in slow motion)
        private const float SafeDamping = 0.8f;
        private const float SafeStiffness = 0.5f;

        private class Arm
        {
            public Transform Shoulder, Elbow, Wrist, Hand;
            public Quaternion ShoulderRest, ElbowRest, WristRest;
            public float Upper, Fore;
            public float Side;
            /// Where the wrist is in the built pose this frame: set each frame before the arm is posed
            public Vector3 RestWrist;
            /// The palm's facing and the fingers' direction, in the wrist's own space. In the built pose the hands hang along the forearm with their palms to his body, turned a little toward his back
            public Vector3 PalmInWrist, FingersInWrist;
        }

        private readonly float[] _samples = new float[Samples];
        private readonly List<Rigidbody> _bones = new List<Rigidbody>();
        private Vector3[] _boneLocalPositions;
        private Quaternion[] _boneLocalRotations;
        private Vector3[] _engageFromPositions;
        private Quaternion[] _engageFromRotations;
        private float _engage = 1f;
        private Collider[] _colliders;
        private CharacterJoint[] _joints;
        private Rigidbody[] _jointBodies;
        private Quaternion[] _restInParent;
        private Transform _body, _waist, _neck;
        private Transform[] _markers;
        private Quaternion[] _markerRest;
        private Vector3 _bodyRest, _standRest;
        private Quaternion _waistRest, _neckRest, _jawRest;
        private Vector3 _faceInNeck, _faceInSkull;
        // The middle of the skull mesh, in the skull's space: its pivot is down at his feet, so aiming from skull.position
        // looked up at the player's chin and he gazed straight ahead
        private Vector3 _headInSkull;
        private float _headAboveFeet;
        private Arm _tipArm, _waveArm;
        private Vector3 _brimInSkull;
        private Vector3 _hatPosition;
        private Quaternion _hatRotation;
        private bool _hatOn = true;
        private bool _hatInHand;
        private float _shove, _boing, _bow, _wave, _waveRock;
        private Vector3 _carry;
        private Func<Vector3> _handTo;
        private Quaternion _look = Quaternion.identity, _lookFrom = Quaternion.identity, _lookTarget = Quaternion.identity;
        private float _nextLook, _lookStartedAt = -1f;
        private float _whirredAt = float.MinValue;
        private float _stoppedAt = float.MinValue;
        private Vector3 _joltAxis = Vector3.right;
        private float _joltAt = -1f;
        private bool _recoil;
        private float _mouth, _mouthSpeed;
        private MaterialPropertyBlock _eyeGlow;
        private static readonly int EyeStrengthId = Shader.PropertyToID("_Strength");
        private bool _out, _limp, _didWell, _waving;
        private Transform _head;
        private Coroutine _act;

        private void Awake()
        {
            _body = Marker("Body");
            _waist = Marker("Waist");
            _neck = Marker("Neck");
            if (stand == null || skull == null || hat == null || jaw == null || _body == null || _waist == null || _neck == null)
            {
                Debug.LogWarning("[SkeletonBarker] Missing his stand, skull, hat, jaw or joint markers", this);
                enabled = false;
                return;
            }

            foreach (var body in GetComponentsInChildren<Rigidbody>(true))
                if (body != stand && body != hat) _bones.Add(body);
            _boneLocalPositions = _bones.Select(b => b.transform.localPosition).ToArray();
            _boneLocalRotations = _bones.Select(b => b.transform.localRotation).ToArray();
            _engageFromPositions = new Vector3[_bones.Count];
            _engageFromRotations = new Quaternion[_bones.Count];

            // Every joint marker, to put back when he's shut away
            _markers = new[] { "Waist", "Neck", "Shoulder L", "Elbow L", "Wrist L", "Shoulder R", "Elbow R", "Wrist R" }
                       .Select(Marker).Where(m => m != null).ToArray();
            _markerRest = _markers.Select(m => m.localRotation).ToArray();

            _standRest = stand.transform.position;
            BuildTheReach();
            _bodyRest = _body.position;
            _waistRest = _waist.localRotation;
            _neckRest = _neck.localRotation;
            _jawRest = jaw.localRotation;
            _faceInNeck = _neck.InverseTransformDirection(transform.forward);
            _faceInSkull = skull.transform.InverseTransformDirection(transform.forward);
            var skullMesh = skull.GetComponent<Renderer>();
            _headInSkull = skull.transform.InverseTransformPoint(skullMesh != null ? skullMesh.bounds.center : _neck.position);
            _headAboveFeet = Vector3.Distance(HeadCentre(), _body.position);
            _hatPosition = hat.transform.localPosition;
            _hatRotation = hat.transform.localRotation;
            WearTheHat();

            // Measured in his built pose, which every joint is pulled back to while he's limp
            _joints = GetComponentsInChildren<CharacterJoint>(true).Where(j => j.connectedBody != null).ToArray();
            _jointBodies = _joints.Select(j => j.GetComponent<Rigidbody>()).ToArray();
            _restInParent = _joints.Select(j => Quaternion.Inverse(j.connectedBody.rotation) * j.transform.rotation).ToArray();

            var left = ArmOf("L");
            var right = ArmOf("R");
            // The tipping hand is the one nearer the player; the waving one is out by the lid's free edge, away from its hinge
            _tipArm = Nearer(left, right, stand.position + outward * 10f);
            _waveArm = coffin != null ? (Nearer(left, right, coffin.Hinge) == left ? right : left) : null;
            _brimInSkull = skull.transform.InverseTransformPoint(BrimAtRest());

            // He passes through the coffin and his own pieces: colliding inside a coffin this tight would only make him
            // jitter. Shots are raycasts, so they still find him
            _colliders = GetComponentsInChildren<Collider>(true);
            var coffinColliders = coffin != null ? coffin.GetComponentsInChildren<Collider>(true) : new Collider[0];
            for (int i = 0; i < _colliders.Length; i++)
            {
                for (int j = i + 1; j < _colliders.Length; j++) Physics.IgnoreCollision(_colliders[i], _colliders[j]);
                foreach (var shell in coffinColliders) Physics.IgnoreCollision(_colliders[i], shell);
            }

            Stiffen();
        }

        private Transform Marker(string name)
        {
            foreach (var item in GetComponentsInChildren<Transform>(true))
                if (item.name == name) return item;
            return null;
        }

        private Arm ArmOf(string side)
        {
            var arm = new Arm
            {
                Shoulder = Marker("Shoulder " + side), Elbow = Marker("Elbow " + side), Wrist = Marker("Wrist " + side),
                Hand = Marker("Hand " + side),
            };
            if (arm.Shoulder == null || arm.Elbow == null || arm.Wrist == null) return null;

            arm.ShoulderRest = arm.Shoulder.localRotation;
            arm.ElbowRest = arm.Elbow.localRotation;
            arm.WristRest = arm.Wrist.localRotation;
            arm.Upper = Vector3.Distance(arm.Shoulder.position, arm.Elbow.position);
            arm.Fore = Vector3.Distance(arm.Elbow.position, arm.Wrist.position);
            arm.Side = Mathf.Sign(Vector3.Dot(arm.Shoulder.position - _waist.position, transform.right));
            arm.RestWrist = arm.Wrist.position;
            Vector3 forearm = (arm.Wrist.position - arm.Elbow.position).normalized;
            Vector3 inward = Vector3.ProjectOnPlane(-transform.right * arm.Side, forearm).normalized;
            Vector3 back = Vector3.ProjectOnPlane(-transform.forward, forearm).normalized;
            Vector3 palm = inward * Mathf.Cos(palmBackDegrees * Mathf.Deg2Rad) + back * Mathf.Sin(palmBackDegrees * Mathf.Deg2Rad);
            arm.PalmInWrist = Quaternion.Inverse(arm.Wrist.rotation) * palm.normalized;
            arm.FingersInWrist = Quaternion.Inverse(arm.Wrist.rotation) * forearm;
            return arm;
        }

        private static Arm Nearer(Arm a, Arm b, Vector3 point)
        {
            if (a == null) return b;
            if (b == null) return a;
            return (a.Wrist.position - point).sqrMagnitude <= (b.Wrist.position - point).sqrMagnitude ? a : b;
        }

        private Vector3 BrimAtRest()
        {
            var filter = hat.GetComponentInChildren<MeshFilter>();
            var box = filter.sharedMesh.bounds;
            Vector3 middle = filter.transform.TransformPoint(box.center);
            Vector3 side = transform.right * (_tipArm != null ? _tipArm.Side : 1f);
            float reach = 0f, low = 0f;
            for (int corner = 0; corner < 8; corner++)
            {
                var local = box.center + Vector3.Scale(box.extents, new Vector3(corner & 1, (corner >> 1) & 1, (corner >> 2) & 1) * 2f - Vector3.one);
                Vector3 offset = filter.transform.TransformPoint(local) - middle;
                reach = Mathf.Max(reach, Vector3.Dot(offset, side));
                low = Mathf.Min(low, Vector3.Dot(offset, transform.up));
            }

            return middle + side * reach + transform.up * low;
        }

        protected override void Start()
        {
            base.Start();
            if (!enabled) return;

            if (coffin != null) coffin.Peeking += OnPeeking;
            if (game == null) return;

            game.RoundEnded += OnRoundEnded;
            game.CountdownGo += OnCountdownGo;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (coffin != null) coffin.Peeking -= OnPeeking;
            if (game == null) return;

            game.RoundEnded -= OnRoundEnded;
            game.CountdownGo -= OnCountdownGo;
        }

        private void OnPeeking()
        {
            if (!_out && _waveArm != null && Random.value < waveChance) Run(Wave());
        }

        private IEnumerator Wave()
        {
            _waving = true;
            coffin.KeepPeeking = true;
            yield return new WaitForSeconds(Coffin.PeekSwingSeconds);
            coffin.WidenPeek(waveGapDegrees);
            yield return Tween(waveReachSeconds, t => _wave = t);

            for (float t = 0f; t < waves * waveSeconds; t += Time.deltaTime)
            {
                _waveRock = Mathf.Sin(t / Mathf.Max(0.01f, waveSeconds) * 2f * Mathf.PI);
                yield return null;
            }

            _waveRock = 0f;
            yield return Tween(waveReachSeconds, t => _wave = 1f - t);
            StopWaving();
            _act = null;
        }

        private void StopWaving()
        {
            if (!_waving) return;

            _waving = false;
            _wave = 0f;
            _waveRock = 0f;
            coffin.KeepPeeking = false;
        }

        /// Every round's end, the last included, where he stays out for the final score
        private void OnRoundEnded(int round, int roundScore)
        {
            _didWell = director != null && director.WaveSize > 0 && director.Killed >= tipShare * director.WaveSize;
            if (!_out) Run(ComeOut());
        }

        private void OnCountdownGo()
        {
            if (_out) Run(GoBack());
        }

        private void Run(IEnumerator act)
        {
            if (_act != null) StopCoroutine(_act);
            _act = StartCoroutine(act);
        }

        /// Limp, shoved out fast but slowing into the stop, so he sways to a halt beside the player instead of flopping on into
        /// them (a 3 m lunge stopped dead slammed him into the player); then his servos catch him
        private IEnumerator ComeOut()
        {
            StopWaving();
            _out = true;
            if (coffin != null) yield return new WaitForSeconds(coffin.HoldOpen(true));

            _carry = CarryToThePlayer();
            GoLimp();
            yield return Shove(shoveSeconds, t => _shove = Lunge(t));
            yield return Shove(settleSeconds, t => _boing = boingHeight * Mathf.Sin(t * boingWobbles * 2f * Mathf.PI) * (1f - t) * (1f - t));
            _boing = 0f;
            yield return Engage();

            if (_didWell && _tipArm != null) yield return TipTheHat();
            _act = null;
        }

        private IEnumerator GoBack()
        {
            if (_hatInHand) WearTheHat();
            _handTo = null;
            _bow = 0f;

            GoLimp();
            float from = _shove;
            yield return Shove(yankSeconds, t => _shove = from * (t < WindUp ? 1f + lungeWindUp * Mathf.Sin(t / WindUp * Mathf.PI)
                                                                             : 1f - Squared((t - WindUp) / (1f - WindUp))));
            // Home: the ignored coffin colliders would let his swinging limbs carry on through its walls
            yield return Engage(homeEngageSeconds);

            if (coffin != null)
            {
                coffin.Knock(-outward);
                yield return new WaitForSeconds(coffin.HoldOpen(false));
            }

            Stiffen();
            _out = false;
            _act = null;
        }

        private IEnumerator Engage() { return Engage(engageSeconds); }

        private IEnumerator Engage(float seconds)
        {
            for (int i = 0; i < _bones.Count; i++)
            {
                _engageFromPositions[i] = _bones[i].position;
                _engageFromRotations[i] = _bones[i].rotation;
                _bones[i].isKinematic = true;
                _bones[i].interpolation = RigidbodyInterpolation.None;
            }

            _limp = false;
            stand.interpolation = RigidbodyInterpolation.None;
            _engage = 0f;
            _look = _lookFrom = _lookTarget = Quaternion.identity;
            PlayOne(clunkClips, stand.position, clunkVolume);
            yield return Tween(seconds, t => _engage = t);
            _engage = 1f;
            for (int i = 0; i < _bones.Count; i++)
            {
                _bones[i].transform.localPosition = _boneLocalPositions[i];
                _bones[i].transform.localRotation = _boneLocalRotations[i];
            }
        }

        private IEnumerator TipTheHat()
        {
            yield return Reach(SideOf, reachSeconds);
            yield return Reach(AboveBrim, reachSeconds);
            yield return Reach(Brim, reachSeconds * 0.5f);
            if (!_hatOn) { yield return PutTheArmDown(); yield break; }

            TakeTheHat();
            yield return Reach(AboveBrim, reachSeconds * 0.5f);
            yield return Both(Reach(Swept, sweepSeconds), Tween(sweepSeconds, t => _bow = t));
            yield return new WaitForSeconds(holdSeconds);
            if (!_hatInHand) { yield return PutTheArmDown(); yield break; }

            yield return Both(Reach(AboveBrim, sweepSeconds), Tween(sweepSeconds, t => _bow = 1f - t));
            yield return Reach(Brim, reachSeconds * 0.5f);
            if (_hatInHand) WearTheHat();
            yield return PutTheArmDown();
        }

        private IEnumerator PutTheArmDown()
        {
            yield return Reach(AboveBrim, reachSeconds * 0.5f);
            yield return Reach(SideOf, reachSeconds);
            yield return Reach(() => _tipArm.RestWrist, reachSeconds);
            _handTo = null;
            _bow = 0f;
        }

        private IEnumerator Reach(Func<Vector3> target, float seconds)
        {
            Vector3 from = _handTo != null ? _handTo() : _tipArm.RestWrist;
            yield return Tween(seconds, t => _handTo = () => Vector3.LerpUnclamped(from, target(), t));
            _handTo = target;
        }

        private IEnumerator Both(IEnumerator a, IEnumerator b)
        {
            var other = StartCoroutine(b);
            yield return a;
            yield return other;
        }

        /// For the limp shove and yank: the caller's own curve, no servo, so his weight carries him on
        private IEnumerator Shove(float seconds, Action<float> apply)
        {
            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, seconds))
            {
                apply(t);
                yield return null;
            }

            apply(1f);
        }

        private IEnumerator Tween(float seconds, Action<float> apply)
        {
            var hitches = new float[Random.Range(hitchesPerMove.x, hitchesPerMove.y + 1)];
            for (int i = 0; i < hitches.Length; i++) hitches[i] = Random.Range(hitchBetween.x, hitchBetween.y);
            Array.Sort(hitches);

            PlayOnce(whirClips, whirVolume, ref _whirredAt);

            int next = 0;
            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, seconds))
            {
                apply(Servo(t));
                if (next < hitches.Length && t >= hitches[next])
                {
                    next++;
                    yield return new WaitForSeconds(Random.Range(hitchSeconds.x, hitchSeconds.y));
                }

                yield return null;
            }

            apply(1f);
            PlayOnce(stopClips, stopVolume, ref _stoppedAt);
        }

        // Moves run in pairs (a reach and a bow together), and two of the same sound at once would flam
        private const float SameMoment = 0.1f;

        private void PlayOnce(AudioClip[] clips, float volume, ref float lastAt)
        {
            if (Time.time - lastAt < SameMoment) return;

            lastAt = Time.time;
            PlayOne(clips, stand.position, volume);
        }

        // Shares of a move: the wind-up ends at WindUp, the snap at Snap, and the rest wobbles to rest
        private const float WindUp = 0.2f;
        private const float Snap = 0.6f;

        private static float Squared(float t) { return t * t; }

        private float Servo(float t)
        {
            float shake = Random.Range(-motorShake, motorShake);
            if (t < WindUp) return -anticipation * Mathf.Sin(t / WindUp * Mathf.PI * 0.5f) + shake;
            if (t < Snap)
            {
                float u = (t - WindUp) / (Snap - WindUp);
                return Mathf.LerpUnclamped(-anticipation, 1f + overshoot, u * u * (3f - 2f * u)) + shake;
            }

            float settle = (t - Snap) / (1f - Snap);
            return 1f + overshoot * Mathf.Cos(settle * wobbles * 2f * Mathf.PI) * Mathf.Exp(-settle * 5f);
        }

        /// The trip out: ducks back into the coffin, then out fast, slowing into the stop so he never flies on at the player
        /// (a lunge stopped dead slammed him into them). The bounce on arrival is up and down only
        private float Lunge(float t)
        {
            if (t < WindUp) return -lungeWindUp * Mathf.Sin(t / WindUp * Mathf.PI);
            float u = (t - WindUp) / (1f - WindUp);
            return u * u * (3f - 2f * u);
        }

        private Vector3 Side { get { return transform.right * _tipArm.Side; } }
        private Vector3 Brim() { return skull.transform.TransformPoint(_brimInSkull); }
        private Vector3 AboveBrim() { return Brim() + Side * clearOfHim.x + Vector3.up * clearOfHim.y; }

        /// Straight out to his side at shoulder height, a little forward: the arm's first move, clear of his ribs
        private Vector3 SideOf()
        {
            float reach = (_tipArm.Upper + _tipArm.Fore) * 0.9f;
            return _tipArm.Shoulder.position + Side * reach + outward * 0.1f;
        }

        private Vector3 Swept()
        {
            return skull.position + Side * sweepTo.x + Vector3.up * sweepTo.y + outward * sweepTo.z;
        }

        private void FixedUpdate()
        {
            if (!_limp) return;

            stand.MovePosition(_standRest + ShoveOffset());
            Hold();
            LookLimp();
        }

        // The stand's bar, carried out with him, and a copy of it stretched from its back end to where that end sat in the coffin, so the bar always reaches back in. The stand itself isn't stretched: his joints hang from it
        private Transform _reach, _reachBar;
        private Vector3 _reachAnchor, _reachBackInStand, _reachAxis, _reachMiddle;
        private float _reachLength;

        private void BuildTheReach()
        {
            // The biggest mesh is the bar; the first found was one of its bolts, stretched into a thin rod
            var filter = stand.GetComponentsInChildren<MeshFilter>().Where(m => m.sharedMesh != null)
                              .OrderByDescending(m => Vector3.Scale(m.sharedMesh.bounds.size, m.transform.lossyScale).sqrMagnitude)
                              .FirstOrDefault();
            var bar = filter != null ? filter.GetComponent<MeshRenderer>() : null;
            if (bar == null || filter.sharedMesh == null) return;

            var bounds = filter.sharedMesh.bounds;
            var scaled = Vector3.Scale(bounds.size, filter.transform.lossyScale);
            int longest = scaled.x >= scaled.y && scaled.x >= scaled.z ? 0 : scaled.y >= scaled.z ? 1 : 2;
            _reachAxis = new Vector3(longest == 0 ? 1f : 0f, longest == 1 ? 1f : 0f, longest == 2 ? 1f : 0f);
            _reachMiddle = bounds.center;
            _reachLength = bounds.size[longest];

            // The end further from him is the back, in the coffin
            Vector3 a = filter.transform.TransformPoint(bounds.center + _reachAxis * (_reachLength * 0.5f));
            Vector3 b = filter.transform.TransformPoint(bounds.center - _reachAxis * (_reachLength * 0.5f));
            _reachAnchor = (a - _body.position).sqrMagnitude > (b - _body.position).sqrMagnitude ? a : b;
            _reachBackInStand = stand.transform.InverseTransformPoint(_reachAnchor);

            var copy = new GameObject("Stand Reach", typeof(MeshFilter), typeof(MeshRenderer));
            copy.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            copy.GetComponent<MeshRenderer>().sharedMaterials = bar.sharedMaterials;
            _reach = copy.transform;
            _reachBar = filter.transform;
            _reach.localScale = filter.transform.lossyScale;
            _reach.gameObject.SetActive(false);
        }

        private void StretchTheReach()
        {
            if (_reach == null) return;

            Vector3 back = stand.transform.TransformPoint(_reachBackInStand);
            Vector3 gap = _reachAnchor - back;
            float length = gap.magnitude;
            bool show = length > 0.01f;
            if (_reach.gameObject.activeSelf != show) _reach.gameObject.SetActive(show);
            if (!show) return;

            Vector3 along = _reachBar.TransformDirection(_reachAxis);
            _reach.rotation = Quaternion.FromToRotation(along, gap) * _reachBar.rotation;
            Vector3 scale = _reachBar.lossyScale;
            scale[_reachAxis.x > 0f ? 0 : _reachAxis.y > 0f ? 1 : 2] = length / Mathf.Max(1e-5f, _reachLength);
            _reach.localScale = scale;
            _reach.position = (back + _reachAnchor) * 0.5f - _reach.TransformVector(_reachMiddle);
        }

        private Vector3 ShoveOffset() { return _carry * _shove + Vector3.up * _boing; }

        /// From where his head is now, in the coffin, to beside the player's head as it is now. Falls back to the measured carry
        private Vector3 CarryToThePlayer()
        {
            if (_head == null && !FindTheHead()) return carry;

            Vector3 eyes = _head.position;
            Vector3 from = HeadCentre();
            // Where they're looking as he comes out, so he lands in view; the range direction only if they're looking straight up or down
            Vector3 ahead = Vector3.ProjectOnPlane(_head.forward, Vector3.up);
            ahead = ahead.sqrMagnitude > 0.01f ? ahead.normalized : Vector3.ProjectOnPlane(playerFaces, Vector3.up).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, ahead);
            if (Vector3.Dot(from - eyes, side) < 0f) side = -side;
            Vector3 to = eyes + side * besidePlayer + ahead * aheadOfPlayer + Vector3.up * aboveEyes;
            // He is taller than a short or seated player's eye line: level with them, his feet went through the floor
            to.y = Mathf.Max(to.y, _headAboveFeet + feetAboveFloor);
            return to - from;
        }

        private static float Damping(float wanted) { return Mathf.Min(wanted, SafeDamping / Time.fixedDeltaTime); }

        private static float Stiffness(float wanted)
        {
            return Mathf.Min(wanted, SafeStiffness / (Time.fixedDeltaTime * Time.fixedDeltaTime));
        }

        private void Hold()
        {
            float stiffness = Stiffness(holdStrength), damping = Damping(holdDamping);
            for (int i = 0; i < _joints.Length; i++)
            {
                var body = _jointBodies[i];
                if (body == skull) continue;

                var parent = _joints[i].connectedBody;
                float angle;
                Vector3 axis;
                (parent.rotation * _restInParent[i] * Quaternion.Inverse(body.rotation)).ToAngleAxis(out angle, out axis);
                if (angle > 180f) angle -= 360f;

                Vector3 spin = body.angularVelocity - parent.angularVelocity;
                Vector3 torque = Mathf.Abs(angle) > 0.01f ? axis * (angle * Mathf.Deg2Rad * stiffness) - spin * damping : -spin * damping;
                body.AddTorque(torque, ForceMode.Acceleration);
            }
        }

        private void LookLimp()
        {
            if (_head == null && !FindTheHead()) return;

            Vector3 face = skull.transform.TransformDirection(_faceInSkull);
            float angle;
            Vector3 axis;
            Quaternion.FromToRotation(face, LookPoint() - HeadCentre()).ToAngleAxis(out angle, out axis);
            if (angle > 180f) angle -= 360f;

            skull.AddTorque(axis * (angle * Mathf.Deg2Rad * Stiffness(lookStrength)) - skull.angularVelocity * Damping(lookDamping),
                            ForceMode.Acceleration);
        }

        private void LateUpdate()
        {
            if (!_limp) Pose();
            StretchTheReach();
            Listen();
            jaw.localRotation = _jawRest * Quaternion.AngleAxis(jawOpenDegrees * _mouth, Vector3.right);
        }

        private void Pose()
        {
            Vector3 offset = ShoveOffset();
            stand.transform.position = _standRest + offset;
            _body.position = _bodyRest + offset;

            // Turned in the parent's frame, so the lean is toward the player whatever the waist's own axes are
            Vector3 leanAxis = _waist.parent.InverseTransformDirection(Vector3.Cross(Vector3.up, outward));
            float lean = (_out ? leanDegrees * _engage : 0f) + bowDegrees * _bow;
            _waist.localRotation = Quaternion.AngleAxis(lean, leanAxis) * Jolt() * _waistRest;

            _neck.localRotation = _neckRest;
            if (_out) Look();

            if (_tipArm != null) PoseArm(_tipArm, _handTo, 0f);
            if (_waveArm != null && (_waveArm != _tipArm || _waving)) PoseArm(_waveArm, _waving ? (Func<Vector3>)WaveTarget : null, _waveRock);

            if (_engage >= 1f) return;
            for (int i = 0; i < _bones.Count; i++)
            {
                var piece = _bones[i].transform;
                Vector3 to = piece.parent.TransformPoint(_boneLocalPositions[i]);
                Quaternion turn = piece.parent.rotation * _boneLocalRotations[i];
                piece.position = Vector3.LerpUnclamped(_engageFromPositions[i], to, _engage);
                piece.rotation = Quaternion.SlerpUnclamped(_engageFromRotations[i], turn, _engage);
            }
        }

        private Quaternion Jolt()
        {
            if (_joltAt < 0f) return Quaternion.identity;

            float t = (Time.time - _joltAt) / Mathf.Max(0.01f, _recoil ? recoilSeconds : joltSeconds);
            if (t >= 1f) { _joltAt = -1f; return Quaternion.identity; }

            if (_recoil)
            {
                float back = t < recoilSnapShare ? Mathf.Sin(t / recoilSnapShare * Mathf.PI * 0.5f)
                                                 : Mathf.SmoothStep(1f, 0f, (t - recoilSnapShare) / (1f - recoilSnapShare));
                return Quaternion.AngleAxis(recoilDegrees * back, _joltAxis);
            }

            float amount = t < 0.3f ? 1f : t < 0.6f ? -0.4f : 0f;
            return Quaternion.AngleAxis(joltDegrees * amount, _joltAxis);
        }

        private void Look()
        {
            if (_head == null && !FindTheHead()) return;

            if (Time.time >= _nextLook)
            {
                _nextLook = Time.time + lookEverySeconds;
                Vector3 face = _neck.TransformDirection(_faceInNeck);
                var turn = Quaternion.FromToRotation(face, LookPoint() - HeadCentre());
                var aim = Quaternion.RotateTowards(Quaternion.identity, turn, lookLimit);
                float tilt = Random.value < 0.5f ? -headTilt : headTilt;
                _lookFrom = _look;
                _lookTarget = Quaternion.AngleAxis(tilt, aim * face) * aim;
                _lookStartedAt = Time.time;
            }

            float t = _lookStartedAt < 0f ? 1f : (Time.time - _lookStartedAt) / Mathf.Max(0.01f, lookTurnSeconds);
            _look = Quaternion.SlerpUnclamped(_lookFrom, _lookTarget, t < 1f ? Servo(t) : 1f);
            _neck.rotation = _look * _neck.rotation;
        }

        /// The arm back in its built pose, or reaching for a point: a two-bone solve with the elbow kept out to his side and
        /// down, so the arm folds outside him. wristShare tips the hand at the wrist, toward and away from the player
        private void PoseArm(Arm arm, Func<Vector3> target, float wristShare)
        {
            arm.Shoulder.localRotation = arm.ShoulderRest;
            arm.Elbow.localRotation = arm.ElbowRest;
            arm.Wrist.localRotation = arm.WristRest;
            arm.RestWrist = arm.Wrist.position;
            if (target == null) return;

            Vector3 shoulder = arm.Shoulder.position;
            Vector3 toTarget = target() - shoulder;
            float reach = Mathf.Clamp(toTarget.magnitude, 0.05f, (arm.Upper + arm.Fore) * 0.98f);
            Vector3 along = toTarget.normalized;

            Vector3 side = transform.right * arm.Side;
            Vector3 pole = Vector3.ProjectOnPlane(side - Vector3.up * 0.4f, along).normalized;
            float cos = (arm.Upper * arm.Upper + reach * reach - arm.Fore * arm.Fore) / (2f * arm.Upper * reach);
            float angle = Mathf.Acos(Mathf.Clamp(cos, -1f, 1f));
            Vector3 elbow = shoulder + (along * Mathf.Cos(angle) + pole * Mathf.Sin(angle)) * arm.Upper;
            Vector3 wrist = shoulder + along * reach;

            arm.Shoulder.rotation = Quaternion.FromToRotation(arm.Elbow.position - shoulder, elbow - shoulder) * arm.Shoulder.rotation;
            arm.Elbow.rotation = Quaternion.FromToRotation(arm.Wrist.position - arm.Elbow.position, wrist - arm.Elbow.position) * arm.Elbow.rotation;
            if (arm == _waveArm && _waving) PalmToThePlayer(arm);
            if (wristShare != 0f) arm.Wrist.rotation = Quaternion.AngleAxis(waveDegrees * wristShare, outward) * arm.Wrist.rotation;
        }

        /// The hand as people wave: fingers up, palm to the player's face, set outright as the arm reaches.
        /// Twisting it about the forearm failed once the arm came out of the gap pointing at the player
        private void PalmToThePlayer(Arm arm)
        {
            Vector3 toward = (_head != null ? _head.position : arm.Wrist.position + outward) - arm.Wrist.position;
            Vector3 palm = Vector3.ProjectOnPlane(toward, Vector3.up);
            if (palm.sqrMagnitude < 1e-6f) palm = outward;

            var wanted = Quaternion.LookRotation(palm.normalized, Vector3.up) *
                         Quaternion.Inverse(Quaternion.LookRotation(arm.PalmInWrist, arm.FingersInWrist));
            arm.Wrist.rotation = Quaternion.Slerp(arm.Wrist.rotation, wanted, Mathf.Clamp01(_wave));
        }

        /// Out through the lid's gap, a little toward the player and up, so it's well clear of the coffin and easy to see
        ///
        private Vector3 WaveTarget()
        {
            // To the middle of the lid's gap first, then on out the way the gap points from his shoulder, so the arm leaves
            // through the opening, not through the coffin's side
            Vector3 rest = _waveArm.RestWrist;
            Vector3 shoulder = _waveArm.Shoulder.position;
            Vector3 gap = coffin != null ? coffin.GapNear(shoulder) : shoulder + transform.right * _waveArm.Side * _waveArm.Upper;
            Vector3 reached = shoulder + (gap - shoulder).normalized * ((_waveArm.Upper + _waveArm.Fore) * waveReach) +
                              outward * waveLean.x + Vector3.up * waveLean.y;
            return _wave < 0.5f ? Vector3.LerpUnclamped(rest, gap, _wave * 2f) : Vector3.LerpUnclamped(gap, reached, _wave * 2f - 1f);
        }

        private Vector3 LookPoint() { return _head.position - Vector3.up * lookBelow; }

        private Vector3 HeadCentre() { return skull.transform.TransformPoint(_headInSkull); }

        /// The camera the player sees through, else the avatar's head. Camera first: in the editor the avatar's 'Head' stayed at its start while the view moved, so he aimed beside a spot the player wasn't at. Not ?. on VRAvatar: it's a UnityEngine.Object, and null propagation would sail past a destroyed one
        private bool FindTheHead()
        {
            var cam = Camera.main;
            if (cam != null) { _head = cam.transform; return true; }

            var avatar = VRAvatar.Active;
            if (avatar != null)
            {
                var limb = avatar.GetLimb(VRAvatarLimbType.Head);
                if (limb != null) _head = limb.Transform;
            }
            return _head != null;
        }

        private void GoLimp()
        {
            _limp = true;
            _engage = 1f;
            stand.interpolation = RigidbodyInterpolation.Interpolate;
            foreach (var bone in _bones)
            {
                bone.interpolation = RigidbodyInterpolation.Interpolate;
                bone.isKinematic = false;
            }
        }

        private void Stiffen()
        {
            _limp = false;
            _engage = 1f;
            _shove = 0f;
            stand.interpolation = RigidbodyInterpolation.None;
            stand.transform.position = _standRest;
            _body.position = _bodyRest;
            for (int i = 0; i < _markers.Length; i++) _markers[i].localRotation = _markerRest[i];

            for (int i = 0; i < _bones.Count; i++)
            {
                _bones[i].isKinematic = true;
                _bones[i].interpolation = RigidbodyInterpolation.None;
                _bones[i].transform.localPosition = _boneLocalPositions[i];
                _bones[i].transform.localRotation = _boneLocalRotations[i];
            }
        }

        /// Worn or held, the hat is a kinematic passenger. Not interpolated then: an interpolated body writes its own last
        /// pose over the parent's, which left it hanging in the air behind him
        private void Carry(Transform by)
        {
            hat.isKinematic = true;
            hat.interpolation = RigidbodyInterpolation.None;
            hat.transform.SetParent(by, true);
        }

        private void WearTheHat()
        {
            Carry(skull.transform);
            hat.transform.localPosition = _hatPosition;
            hat.transform.localRotation = _hatRotation;
            _hatOn = true;
            _hatInHand = false;
        }

        private void TakeTheHat()
        {
            Carry(_tipArm.Hand != null ? _tipArm.Hand : _tipArm.Wrist);
            _hatInHand = true;
        }

        private void ThrowTheHat(Vector3 velocity)
        {
            if (!_hatOn) return;

            _hatOn = false;
            _hatInHand = false;
            hat.transform.SetParent(transform, true);
            hat.interpolation = RigidbodyInterpolation.Interpolate;
            hat.isKinematic = false;
            hat.velocity = velocity;
        }

        /// The jaw opens with how loud the barker is, only while he's out where he can be seen
        private void Listen()
        {
            float loud = 0f;
            var voice = CarnyVoice.Instance;
            if (_out && voice != null && voice.Speaker != null)
            {
                voice.Speaker.GetOutputData(_samples, 0);
                float sum = 0f;
                for (int i = 0; i < Samples; i++) sum += _samples[i] * _samples[i];
                loud = Mathf.Sqrt(sum / Samples);
            }

            _mouth = Mathf.SmoothDamp(_mouth, Mathf.Clamp01(loud / fullOpenLoudness), ref _mouthSpeed, jawSmoothing);
            GlowTheEyes();
        }

        private void GlowTheEyes()
        {
            if (eyeGlows == null || eyeGlows.Length == 0) return;

            if (_eyeGlow == null) _eyeGlow = new MaterialPropertyBlock();
            _eyeGlow.SetFloat(EyeStrengthId, Mathf.Lerp(eyeGlowQuiet, eyeGlowLoud, _mouth));
            foreach (var eye in eyeGlows) if (eye != null) eye.SetPropertyBlock(_eyeGlow);
        }

        public override void OnShot(Vector3 point, Vector3 direction, float impulse)
        {
            if (!enabled) return;

            PlayOne(hitClips, point, hitVolume);
            var struck = BodyNearest(point);
            Vector3 push = direction.normalized * shotImpulse;
            if (struck == hat && _hatOn) { ThrowTheHat(push / Mathf.Max(0.01f, hat.mass)); return; }

            if (_limp)
            {
                if (struck != null && !struck.isKinematic) struck.AddForceAtPosition(push, point, ForceMode.Impulse);
                return;
            }

            PlayOne(clunkClips, point, clunkVolume);
            // Tipping about up x the shot's line takes the top of him along it, away from whoever fired
            _recoil = _out;
            _joltAxis = _waist.parent.InverseTransformDirection(Vector3.Cross(Vector3.up, direction).normalized);
            _joltAt = Time.time;
        }

        private Rigidbody BodyNearest(Vector3 point)
        {
            Collider struck = null;
            float nearest = float.MaxValue;
            foreach (var item in _colliders)
            {
                if (item == null || !item.enabled) continue;

                float distance = (item.ClosestPoint(point) - point).sqrMagnitude;
                if (distance < nearest)
                {
                    nearest = distance;
                    struck = item;
                }
            }

            return struck != null ? struck.attachedRigidbody : null;
        }

        protected override void Restore()
        {
            if (enabled && !_hatOn) WearTheHat();
        }
    }
}
