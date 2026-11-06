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
        [Header("Laid out by setup steps 64 and 65")]
        [SerializeField] private RangeGame game;
        [SerializeField] private TrackDirector director;
        [SerializeField] private Coffin coffin;
        [SerializeField] private Rigidbody stand;
        [SerializeField] private Rigidbody skull;
        [SerializeField] private Transform jaw;
        [SerializeField] private Rigidbody hat;
        [SerializeField, Tooltip("Flat, toward the player, set by step 65")] private Vector3 outward = Vector3.forward;

        [Header("The stand (data)")]
        [SerializeField] private float reachOut = 1f;
        [SerializeField] private float shoveSeconds = 0.45f;
        [SerializeField] private float yankSeconds = 0.35f;

        [Header("Animatronic (data): moves catch for a moment, like a sticking servo")]
        [SerializeField] private Vector2Int hitchesPerMove = new Vector2Int(1, 2);
        [SerializeField, Tooltip("Seconds")] private Vector2 hitchSeconds = new Vector2(0.06f, 0.18f);
        [SerializeField] private Vector2 hitchBetween = new Vector2(0.15f, 0.85f);
        [SerializeField] private AudioClip[] clunkClips;
        [SerializeField, Range(0f, 1f)] private float clunkVolume = 0.5f;

        [Header("Holding his pose (data): stiff enough that a shot is a short, snappy knock")]
        [SerializeField] private float holdStrength = 150f;
        [SerializeField] private float holdDamping = 18f;

        [Header("Looking at the player (data)")]
        [SerializeField] private float lookStrength = 150f;
        [SerializeField] private float lookDamping = 18f;

        [Header("Hat tip (data): only after a good round")]
        [SerializeField, Range(0f, 1f)] private float tipShare = 0.75f;
        [SerializeField, Tooltip("Seconds he swings before the string pulls")] private float settleSeconds = 0.6f;
        [SerializeField] private float pullRate = 8f;
        [SerializeField, Tooltip("Metres a second, at most")] private float pullSpeed = 2.5f;
        [SerializeField, Range(0f, 1f)]
        private float pullGrip = 0.3f;
        [SerializeField] private float grabReach = 0.12f;
        [SerializeField] private float reachTimeout = 1.5f;
        [SerializeField] private float sweepSeconds = 0.6f;
        [SerializeField] private float holdSeconds = 0.6f;
        [SerializeField]
        private Vector3 sweepTo = new Vector3(0.25f, -0.35f, 0.4f);

        [Header("Mid-round gag (data): a hand out of the cracked lid, waving")]
        [SerializeField, Range(0f, 1f)] private float waveChance = 0.6f;
        [SerializeField] private float waveLift = 70f;
        [SerializeField] private float waveBend = 50f;
        [SerializeField] private float waveDegrees = 25f;
        [SerializeField] private int waves = 3;
        [SerializeField] private float waveSeconds = 0.35f;
        [SerializeField] private float waveReachSeconds = 0.4f;

        [Header("Miming (data)")]
        [SerializeField] private float jawOpenDegrees = 22f;
        [SerializeField] private float fullOpenLoudness = 0.12f;
        [SerializeField, Tooltip("Seconds")] private float jawSmoothing = 0.05f;

        [Header("Shot (data)")]
        [SerializeField] private float shotImpulse = 0.8f;
        [SerializeField] private AudioClip[] hitClips;
        [SerializeField, Range(0f, 1f)] private float hitVolume = 0.6f;

        private const int Samples = 256;

        private readonly float[] _samples = new float[Samples];
        private readonly List<Rigidbody> _bones = new List<Rigidbody>();
        private Vector3[] _bonePositions;
        private Quaternion[] _boneRotations;
        private Collider[] _colliders;
        private Vector3 _standRest;
        private Quaternion _jawRest;
        private Vector3 _face;
        private Rigidbody _hand;
        private float _side;
        private Vector3 _brim;
        private Vector3 _hatPosition;
        private Quaternion _hatRotation;
        private bool _hatOn = true;
        private bool _hatInHand;
        private Func<Vector3> _pullTo;
        private float _pull;
        private float _shove;
        private float _mouth;
        private float _mouthSpeed;
        private bool _out;
        private bool _didWell;
        private Transform _head;
        private Coroutine _act;
        private CharacterJoint[] _joints;
        private Rigidbody[] _jointBodies;
        private Quaternion[] _restInParent;
        private Rigidbody[] _arm;
        private Rigidbody[] _waveArm;
        private Vector3[] _waveRestPositions;
        private Quaternion[] _waveRestRotations;
        private Vector3 _waveShoulder;
        private Vector3 _waveElbow;
        private Vector3 _waveLiftAxis;
        private float _waveOut;
        private float _waveRock;
        private bool _waving;

        private void Awake()
        {
            if (stand == null || skull == null || hat == null || jaw == null)
            {
                Debug.LogWarning("[SkeletonBarker] No stand, skull, hat or jaw wired. Attach his stand and run setup step 65", this);
                enabled = false;
                return;
            }

            foreach (var body in GetComponentsInChildren<Rigidbody>(true))
                if (body != stand && body != hat) _bones.Add(body);

            _bonePositions = new Vector3[_bones.Count];
            _boneRotations = new Quaternion[_bones.Count];
            for (int i = 0; i < _bones.Count; i++)
            {
                _bonePositions[i] = _bones[i].transform.localPosition;
                _boneRotations[i] = _bones[i].transform.localRotation;
            }

            _standRest = stand.position;
            _jawRest = jaw.localRotation;
            _face = skull.transform.InverseTransformDirection(transform.forward);
            _hatPosition = hat.transform.localPosition;
            _hatRotation = hat.transform.localRotation;
            WearTheHat();

            // Measured in his built pose, which every joint is pulled back to while he's out
            _joints = GetComponentsInChildren<CharacterJoint>(true).Where(j => j.connectedBody != null).ToArray();
            _jointBodies = _joints.Select(j => j.GetComponent<Rigidbody>()).ToArray();
            _restInParent = _joints.Select(j => Quaternion.Inverse(j.connectedBody.rotation) * j.transform.rotation).ToArray();

            _hand = NearerHand();
            _arm = ArmOf(_hand);
            ReadyTheWave();
            _side = _hand != null ? Mathf.Sign(Vector3.Dot(_hand.position - skull.position, transform.right)) : 1f;
            _brim = skull.transform.InverseTransformPoint(BrimAtRest());

            // He passes through the coffin and his own pieces: the joints hold him together, and colliding inside a
            // coffin this tight would only make him jitter. Shots are raycasts, so they still find him
            _colliders = GetComponentsInChildren<Collider>(true);
            var coffinColliders = coffin != null ? coffin.GetComponentsInChildren<Collider>(true) : new Collider[0];
            for (int i = 0; i < _colliders.Length; i++)
            {
                for (int j = i + 1; j < _colliders.Length; j++) Physics.IgnoreCollision(_colliders[i], _colliders[j]);
                foreach (var shell in coffinColliders) Physics.IgnoreCollision(_colliders[i], shell);
            }

            Stiffen(true);
        }

        /// The hand on the side nearer the player's seat, out along outward: the one that can tip the hat to them
        private Rigidbody NearerHand()
        {
            Rigidbody best = null;
            float nearest = float.MaxValue;
            foreach (var body in _bones)
            {
                if (!body.name.StartsWith("Hand", StringComparison.Ordinal)) continue;

                float distance = (body.position - (stand.position + outward * 10f)).sqrMagnitude;
                if (distance < nearest)
                {
                    nearest = distance;
                    best = body;
                }
            }

            return best;
        }

        /// The hat's rim on the hand's side, at the bottom of the brim
        private Vector3 BrimAtRest()
        {
            var filter = hat.GetComponentInChildren<MeshFilter>();
            var box = filter.sharedMesh.bounds;
            Vector3 middle = filter.transform.TransformPoint(box.center);
            Vector3 side = transform.right * _side;
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

        /// The arm on the side of the lid's free edge, where the gap opens, measured in his built pose. It swings up and out
        /// toward the gap and the player
        private void ReadyTheWave()
        {
            if (coffin == null) return;

            Rigidbody hand = null;
            float farthest = -1f;
            foreach (var body in _bones)
            {
                if (!body.name.StartsWith("Hand", StringComparison.Ordinal)) continue;

                float distance = (body.position - coffin.Hinge).sqrMagnitude;
                if (distance > farthest)
                {
                    farthest = distance;
                    hand = body;
                }
            }

            var arm = ArmOf(hand);
            if (arm.Length < 3) return;

            _waveArm = arm;
            _waveRestPositions = arm.Select(b => b.position).ToArray();
            _waveRestRotations = arm.Select(b => b.rotation).ToArray();
            _waveShoulder = Pivot(arm[2]);
            _waveElbow = Pivot(arm[1]);

            Vector3 down = (_waveElbow - _waveShoulder).normalized;
            Vector3 gap = Vector3.ProjectOnPlane(hand.position - coffin.Hinge, transform.up).normalized;
            _waveLiftAxis = Vector3.Cross(down, (outward + gap).normalized).normalized;
        }

        private static Vector3 Pivot(Rigidbody body)
        {
            var joint = body.GetComponent<CharacterJoint>();
            return joint.transform.TransformPoint(joint.anchor);
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

        /// Holds the lid open while his hand comes out of the gap, waves, and goes back in
        private IEnumerator Wave()
        {
            _waving = true;
            coffin.KeepPeeking = true;
            yield return new WaitForSeconds(Coffin.PeekSwingSeconds);
            yield return Tween(waveReachSeconds, t => _waveOut = t);

            for (float t = 0f; t < waves * waveSeconds; t += Time.deltaTime)
            {
                _waveRock = Mathf.Sin(t / Mathf.Max(0.01f, waveSeconds) * 2f * Mathf.PI);
                yield return null;
            }

            _waveRock = 0f;
            yield return Tween(waveReachSeconds, t => _waveOut = 1f - t);
            StopWaving();
            _act = null;
        }

        /// The arm back in its built pose and the lid let go, finished or cut short by the round ending
        private void StopWaving()
        {
            if (!_waving) return;

            _waving = false;
            _waveOut = 0f;
            _waveRock = 0f;
            coffin.KeepPeeking = false;
            Stiffen(true);
        }

        /// Kinematic while he's shut away, so the arm is posed outright: swung up and out at the shoulder, bent at the
        /// elbow, and rocked side to side
        private void PoseTheWave()
        {
            var lift = Quaternion.AngleAxis(waveLift * _waveOut, _waveLiftAxis);
            var turn = Quaternion.AngleAxis(waveDegrees * _waveRock * _waveOut, outward) *
                       Quaternion.AngleAxis(waveBend * _waveOut, _waveLiftAxis);
            Vector3 elbow = _waveShoulder + lift * (_waveElbow - _waveShoulder);

            // Hand, forearm, upper arm: the first two turn at the elbow as well
            for (int i = 0; i < _waveArm.Length; i++)
            {
                Vector3 position = _waveShoulder + lift * (_waveRestPositions[i] - _waveShoulder);
                Quaternion rotation = lift * _waveRestRotations[i];
                if (i < 2)
                {
                    position = elbow + turn * (position - elbow);
                    rotation = turn * rotation;
                }

                _waveArm[i].MovePosition(position);
                _waveArm[i].MoveRotation(rotation);
            }
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

        /// Speeding up all the way and stopping dead, so his own weight carries him on and he flops
        private IEnumerator ComeOut()
        {
            StopWaving();
            _out = true;
            if (coffin != null) yield return new WaitForSeconds(coffin.HoldOpen(true));

            Stiffen(false);
            yield return Tween(shoveSeconds, t => _shove = t * t);
            if (!_didWell || _hand == null) { _act = null; yield break; }

            yield return new WaitForSeconds(settleSeconds);
            yield return TipTheHat();
            _act = null;
        }

        /// The string pulls his hand to the brim, he takes the hat off and holds it out to the player, then puts it back.
        /// A hat shot off on the way ends it
        private IEnumerator TipTheHat()
        {
            _pullTo = Brim;
            yield return Tween(sweepSeconds, t => _pull = t);
            yield return HandReaches(Brim);
            if (!_hatOn) { yield return LetGo(); yield break; }

            TakeTheHat();
            yield return Tween(sweepSeconds, t => _pullTo = () => Vector3.Lerp(Brim(), Swept(), t));
            _pullTo = Swept;
            yield return new WaitForSeconds(holdSeconds);
            if (!_hatInHand) { yield return LetGo(); yield break; }

            yield return Tween(sweepSeconds, t => _pullTo = () => Vector3.Lerp(Swept(), Brim(), t));
            _pullTo = Brim;
            yield return HandReaches(Brim);
            if (_hatInHand) WearTheHat();
            yield return LetGo();
        }

        private IEnumerator HandReaches(Func<Vector3> target)
        {
            for (float waited = 0f; waited < reachTimeout && (_hand.position - target()).sqrMagnitude > grabReach * grabReach; waited += Time.deltaTime)
                yield return null;
        }

        private IEnumerator LetGo()
        {
            yield return Tween(sweepSeconds, t => _pull = 1f - t);
            _pullTo = null;
        }

        /// Yanked in from wherever the stand got to, the coffin rocking with the knock. Stiffened once the lid hides him
        private IEnumerator GoBack()
        {
            if (_hatInHand) WearTheHat();
            _pull = 0f;
            _pullTo = null;

            float from = _shove;
            yield return Tween(yankSeconds, t => _shove = from * (1f - t * t));

            if (coffin != null)
            {
                coffin.Knock(-outward);
                yield return new WaitForSeconds(coffin.HoldOpen(false));
            }

            Stiffen(true);
            _out = false;
            _act = null;
        }

        /// Linear from 0 to 1, catching once or twice on the way with a clunk. The caller eases it
        private IEnumerator Tween(float seconds, Action<float> apply)
        {
            var hitches = new float[Random.Range(hitchesPerMove.x, hitchesPerMove.y + 1)];
            for (int i = 0; i < hitches.Length; i++) hitches[i] = Random.Range(hitchBetween.x, hitchBetween.y);
            Array.Sort(hitches);

            int next = 0;
            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, seconds))
            {
                apply(t);
                if (next < hitches.Length && t >= hitches[next])
                {
                    next++;
                    PlayOne(clunkClips, stand.position, clunkVolume);
                    yield return new WaitForSeconds(Random.Range(hitchSeconds.x, hitchSeconds.y));
                }

                yield return null;
            }

            apply(1f);
        }

        private Vector3 Brim() { return skull.transform.TransformPoint(_brim); }

        private Vector3 Swept()
        {
            return skull.position + transform.right * (_side * sweepTo.x) + Vector3.up * sweepTo.y + outward * sweepTo.z;
        }

        /// Forces in the physics step: the stand moves so the joints feel how fast it went and how hard it stopped, the head
        /// turns to the player, and the string pulls the hand
        private void FixedUpdate()
        {
            stand.MovePosition(_standRest + outward * (reachOut * _shove));
            if (_waving) PoseTheWave();
            if (!_out) return;

            Hold();
            Look();
            if (_pullTo == null || _pull <= 0f) return;

            // Steering the hand's speed by a share of what's wrong each step can't overshoot at any step size, where the
            // force spring it replaces flung him apart on the headset. The arm floats meanwhile, so the hand doesn't sag
            Vector3 wanted = Vector3.ClampMagnitude((_pullTo() - _hand.worldCenterOfMass) * pullRate, pullSpeed);
            _hand.AddForce((wanted - _hand.velocity) * (pullGrip * _pull), ForceMode.VelocityChange);
            foreach (var body in _arm) body.AddForce(-Physics.gravity * _pull, ForceMode.Acceleration);
        }

        // An explicit spring blows up once a step overshoots: damping past about 2/dt, or stiffness past about 1/dt². These
        // keep the hold and the look well inside both at whatever step the physics runs (0.03 s here, less in slow motion)
        private const float SafeDamping = 0.8f;
        private const float SafeStiffness = 0.5f;

        private static float Damping(float wanted) { return Mathf.Min(wanted, SafeDamping / Time.fixedDeltaTime); }

        private static float Stiffness(float wanted)
        {
            return Mathf.Min(wanted, SafeStiffness / (Time.fixedDeltaTime * Time.fixedDeltaTime));
        }

        /// Each joint turned back toward his built pose against its parent, damped, so a knock is short and snaps back. Not
        /// the skull, which the look turns, and the string's arm gives way as the string takes it
        private void Hold()
        {
            float stiffness = Stiffness(holdStrength), damping = Damping(holdDamping);
            for (int i = 0; i < _joints.Length; i++)
            {
                var body = _jointBodies[i];
                if (body == skull) continue;

                float share = Array.IndexOf(_arm, body) >= 0 ? 1f - _pull : 1f;
                if (share <= 0f) continue;

                var parent = _joints[i].connectedBody;
                float angle;
                Vector3 axis;
                (parent.rotation * _restInParent[i] * Quaternion.Inverse(body.rotation)).ToAngleAxis(out angle, out axis);
                if (angle > 180f) angle -= 360f;

                Vector3 spin = body.angularVelocity - parent.angularVelocity;
                Vector3 torque = Mathf.Abs(angle) > 0.01f ? axis * (angle * Mathf.Deg2Rad * stiffness) - spin * damping : -spin * damping;
                body.AddTorque(torque * share, ForceMode.Acceleration);
            }
        }

        /// The hand, its forearm and its upper arm: what the string lifts
        private static Rigidbody[] ArmOf(Rigidbody hand)
        {
            var arm = new List<Rigidbody>();
            for (var body = hand; body != null && arm.Count < 3; )
            {
                arm.Add(body);
                var joint = body.GetComponent<CharacterJoint>();
                body = joint != null ? joint.connectedBody : null;
            }

            return arm.ToArray();
        }

        private void Look()
        {
            if (_head == null && !FindTheHead()) return;

            Vector3 face = skull.transform.TransformDirection(_face);
            Vector3 toPlayer = _head.position - skull.position;
            float angle;
            Vector3 axis;
            Quaternion.FromToRotation(face, toPlayer).ToAngleAxis(out angle, out axis);
            if (angle > 180f) angle -= 360f;

            skull.AddTorque(axis * (angle * Mathf.Deg2Rad * Stiffness(lookStrength)) - skull.angularVelocity * Damping(lookDamping),
                            ForceMode.Acceleration);
        }

        /// The avatar's head once the SDK has spawned the rig, or the camera before it has. Not ?. on VRAvatar: it's a
        /// UnityEngine.Object, and null propagation would sail past a destroyed one
        private bool FindTheHead()
        {
            var avatar = VRAvatar.Active;
            if (avatar != null)
            {
                var limb = avatar.GetLimb(VRAvatarLimbType.Head);
                if (limb != null) { _head = limb.Transform; return true; }
            }

            var cam = Camera.main;
            if (cam != null) _head = cam.transform;
            return _head != null;
        }

        /// Hanging free while he's out; stiff in his built pose, stand home, while he's shut away
        private void Stiffen(bool stiff)
        {
            if (stiff)
            {
                _shove = 0f;
                stand.position = _standRest;
            }

            for (int i = 0; i < _bones.Count; i++)
            {
                _bones[i].isKinematic = stiff;
                if (!stiff) continue;

                _bones[i].transform.localPosition = _bonePositions[i];
                _bones[i].transform.localRotation = _boneRotations[i];
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
            Carry(_hand.transform);
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

        private void LateUpdate()
        {
            Listen();
            jaw.localRotation = _jawRest * Quaternion.AngleAxis(jawOpenDegrees * _mouth, Vector3.right);
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
        }

        public override void OnShot(Vector3 point, Vector3 direction, float impulse)
        {
            if (!enabled) return;

            PlayOne(hitClips, point, hitVolume);

            var struck = BodyNearest(point);
            if (struck == null) return;

            Vector3 push = direction.normalized * shotImpulse;
            if (struck == hat && _hatOn) ThrowTheHat(push / Mathf.Max(0.01f, hat.mass));
            else if (!struck.isKinematic) struck.AddForceAtPosition(push, point, ForceMode.Impulse);
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

        /// Hat back on for the round, while he's shut away
        protected override void Restore()
        {
            if (enabled && !_hatOn) WearTheHat();
        }
    }
}
