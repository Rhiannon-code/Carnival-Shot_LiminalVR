using System;
using UnityEngine;
using Liminal.SDK.VR.Avatars;
using Liminal.SDK.VR.Input;
using IntuitiveDesigns.CrystalCatch;

namespace IntuitiveDesigns.ShootingRange
{
    public class Pistol : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Transform muzzle;
        [SerializeField] private Transform visual;
        [SerializeField] private ParticleSystem muzzleFlash;

        [Header("Slide")]
        [SerializeField] private Transform slide;
        [SerializeField] private float slideKickTravel = 0.03f;
        [SerializeField] private float slideLockTravel = 0.045f;
        [SerializeField] private float slideSeconds = 0.1f;

        [Header("Reload you can see (data)")]
        [SerializeField] private Vector2 reloadTilt = new Vector2(20f, 35f);
        [SerializeField] private float tiltInAndOutSeconds = 0.3f;
        [SerializeField] private float slideSlamKick = 0.5f;

        [Header("Magazine drop, then a twirl (data, fractions of the reload)")]
        [SerializeField] private Vector3 magazineSlide = new Vector3(0f, -0.895f, -0.447f);
        [SerializeField] private float magazineDrop = 0.25f;
        [SerializeField] private float magazineRise = 0.08f;
        [SerializeField] private Vector2 magazineFalls = new Vector2(0.06f, 0.26f);
        [SerializeField] private Vector2 magazineRises = new Vector2(0.3f, 0.42f);
        [SerializeField] private Vector2 twirlSpan = new Vector2(0.52f, 0.82f);
        [SerializeField] private float twirlTurns = 1f;
        [SerializeField] private float letGoSpan = 0.08f;
        [SerializeField] private float fingerCloseSpan = 0.07f;
        [SerializeField] private float insertKick = 0.3f;
        [SerializeField] private Vector2 wristFlick = new Vector2(18f, 8f);
        [SerializeField] private ParticleSystem[] tintedByPowerUp;
        [SerializeField] private Color gunfireColour = new Color(1f, 0.82f, 0.45f);

        [Header("Hold")]
        [SerializeField] private VRAvatarLimbType hand = VRAvatarLimbType.RightHand;
        [SerializeField] private bool followHandTransform = true;
        [SerializeField] private bool fallBackToPrimaryHand = true;
        [SerializeField] private bool startHeld;
        [SerializeField] private Vector3 gripLocalPosition = Vector3.zero;
        [SerializeField] private Vector3 gripLocalEuler = Vector3.zero;
        [SerializeField] private bool holsterToHead = true;
        [SerializeField] private Vector3 holsterLocalPosition = new Vector3(0.3f, 1.05f, 0.35f);
        [SerializeField] private Vector3 holsterLocalEuler = new Vector3(-20f, 0f, 0f);

        [Header("Fire (data)")]
        [SerializeField] private bool automatic;
        [SerializeField] private float fireInterval = 0.18f;
        [SerializeField] private float spreadDegrees = 0.4f;

        [Header("Magazine (data, 0 = unlimited). Empty, it only clicks until the grip is squeezed")]
        [SerializeField] private int magazineSize = 12;
        [SerializeField] private float reloadSeconds = 2f;

        [Header("Recoil (data, degrees and metres)")]
        [SerializeField] private float recoilKick = 7f;
        [SerializeField] private float recoilRecovery = 14f;
        [SerializeField] private float recoilKickBack = 0.035f;
        [SerializeField] private float recoilShake = 3f;
        [SerializeField, Range(0f, 1f)] private float autoKick = 0.35f;
        [SerializeField] private float maxStackedKicks = 1.5f;

        [Header("Taking it off the bench (data)")]
        [SerializeField] private float grabSeconds = 0.15f;

        [Header("Audio")]
        [SerializeField] private AudioClip[] fireClips;
        [SerializeField] private AudioClip reloadClip;
        [SerializeField] private AudioClip magazineOutClip;
        [SerializeField] private AudioClip twirlClip;
        [SerializeField] private AudioClip slideReleaseClip;
        [SerializeField] private AudioClip[] emptyClips;
        [SerializeField, Range(0f, 1f)] private float fireVolume = 0.8f;
        [SerializeField, Range(0f, 0.3f)] private float pitchJitter = 0.05f;

        [Header("Haptics")]
        [SerializeField, Range(0f, 1f)] private float fireHaptic = 0.75f;
        [SerializeField, Range(0f, 1f)] private float scatterHaptic = 1f;

        [Header("Desktop testing (left click fires, right click reloads)")]
        [SerializeField] private bool mouseFires = true;

        public bool IsHeld { get; private set; }
        public bool ExternallyDriven { get; set; }
        public VRAvatarLimbType Hand { get { return hand; } }
        public Vector3 GripPosition { get { return transform.position; } }
        public Transform Muzzle { get { return muzzle != null ? muzzle : transform; } }
        /// The model as posed: recoil, reload tilt and twirl all move this, not the gun's own transform
        public Transform Visual { get { return visual != null ? visual : transform; } }
        public int RoundsLeft { get { return magazineSize > 0 ? _rounds : -1; } }
        public int MagazineSize { get { return magazineSize; } }
        public bool IsReloading { get { return _reloadRemaining > 0f; } }
        public bool IsEmpty { get { return magazineSize > 0 && _rounds <= 0 && !IsReloading; } }

        public event Action<int, int> AmmoChanged;
        public event Action Fired;

        private AudioSource _audio;
        private AimSight _sight;
        private PowerUpTint _tint;
        private Quaternion _visualRest = Quaternion.identity;
        private Vector3 _visualRestPosition;
        private Vector3 _visualBack;
        private Vector2 _shake;
        private float _cooldown;
        private float _recoil;
        private float _recoilVelocity;
        private float _reloadRemaining;
        private float _lockedUntil;
        private Transform _magazine;
        private Vector3 _magazineRest;
        private Vector3 _magazineScale;
        private Vector3 _magazineDown;
        private Vector3 _twirlPivot;
        private Vector3 _sightFrom;
        private Vector3 _sightAlong;
        private float _grab = 1f;
        private Vector3 _grabFrom;
        private Quaternion _grabFromRotation;
        private Vector3 _slideRest;
        private Vector3 _slideBack;
        private float _slideKick;
        private int _rounds;

        private void Awake()
        {
            IsHeld = startHeld;
            _rounds = magazineSize;

            if (visual != null)
            {
                _visualRest = visual.localRotation;
                _visualRestPosition = visual.localPosition;
                _visualBack = visual.parent.InverseTransformVector(-transform.forward);
                MuzzleOnTheBarrel();
                _sightFrom = visual.InverseTransformPoint(Muzzle.position);
                _sightAlong = visual.InverseTransformDirection(Muzzle.forward);
                FindTheMagazine();
            }

            if (slide != null)
            {
                _slideRest = slide.localPosition;
                _slideBack = slide.parent.InverseTransformVector(-transform.forward);
            }
            _sight = GetComponent<AimSight>();
            _tint = GetComponent<PowerUpTint>();

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;   // It is in your own hand, panning your own gun sounds wrong
        }

        private void Start()
        {
            RaiseAmmo();
        }

        public void SetHeld(bool held)
        {
            IsHeld = held;
            _cooldown = 0f;
            _reloadRemaining = 0f;
            _lockedUntil = 0f;

            _grab = held ? 0f : 1f;
            _grabFrom = transform.position;
            _grabFromRotation = transform.rotation;
        }

        public void AssignHand(VRAvatarLimbType limbType)
        {
            hand = limbType;
        }

        // The player is never slowed by slow motion, only the world is
        private void Update()
        {
            if (!IsHeld) { Rest(); return; }

            if (followHandTransform && !ExternallyDriven) FollowHand();

            if (_cooldown > 0f) _cooldown -= Time.unscaledDeltaTime;
            TickReload();

            if (WantsToReload()) BeginReload();
            if (WantsToFire()) TryFire();
        }

        /// The trigger does nothing for this long, a spider's bite. The grip still reloads
        public void LockTrigger(float seconds)
        {
            _lockedUntil = Mathf.Max(_lockedUntil, Time.unscaledTime + seconds);
        }

        private void LateUpdate()
        {
            MoveTheSlide();
            PoseTheModel();

            // Drawn after the model is posed, from the muzzle as the model holds it, so the beam
            // kicks, tilts and spins with the gun instead of hanging where the gun was
            if (_sight != null) _sight.Draw();
        }

        /// The muzzle where the model has it right now. Shots still leave from the steady muzzle,
        /// so recoil and shake never throw the aim off
        public Vector3 SightOrigin { get { return visual != null ? visual.TransformPoint(_sightFrom) : Muzzle.position; } }
        public Vector3 SightForward { get { return visual != null ? visual.TransformDirection(_sightAlong) : Muzzle.forward; } }

        private void PoseTheModel()
        {
            if (visual == null) return;

            _recoil = Mathf.SmoothDamp(_recoil, 0f, ref _recoilVelocity, 1f / Mathf.Max(0.01f, recoilRecovery),
                                       Mathf.Infinity, Time.unscaledDeltaTime);
            float tilt = ReloadBlend;
            float inward = hand == VRAvatarLimbType.LeftHand ? 1f : -1f;
            // Full auto stacks kicks faster than they recover, so the shove is capped or the gun leaves the hand
            float kick = Mathf.Min(1.6f, _recoil / Mathf.Max(0.01f, recoilKick));
            var pose = _visualRest * Quaternion.Euler(-_recoil - reloadTilt.x * tilt + Flick(), _shake.x * kick,
                                                      inward * reloadTilt.y * tilt + _shake.y * kick);

            // The twirl turns the gun about the trigger, where the finger is, not about its own pivot
            var spin = Twirl;
            visual.localRotation = pose * spin;
            visual.localPosition = _visualRestPosition + _visualBack * recoilKickBack * kick +
                                   pose * Vector3.Scale(visual.localScale, _twirlPivot - spin * _twirlPivot);

            MoveTheMagazine();
        }

        /// How far round the twirl is, in the visual's own space. The glove undoes it, so the hand stays
        /// put while the gun spins in it
        public Quaternion Twirl
        {
            get
            {
                float t = Phase(twirlSpan);
                return t > 0f && t < 1f
                     ? Quaternion.AngleAxis(360f * twirlTurns * Mathf.SmoothStep(0f, 1f, t), Vector3.right)
                     : Quaternion.identity;
            }
        }

        public Vector3 TwirlPivot { get { return _twirlPivot; } }

        /// The glove reports its trigger finger here while the gun is gripped, so the twirl turns on
        /// that finger. Without a glove the estimate from the magazine stands
        public void SpinOn(Vector3 worldPoint)
        {
            if (visual != null && !IsReloading) _twirlPivot = visual.InverseTransformPoint(worldPoint);
        }

        /// Up the grip, along its rake, in the gun's own space. Setup step 16 fits the glove to it
        public Vector3 GripUp { get { return -magazineSlide.normalized; } }

        /// 0 to 1: how far a finger has let go so the gun can spin on the trigger finger. The hand
        /// opens as the twirl starts; as the gun comes round, each finger closes lag later than the first
        public float TwirlOpen(float lag)
        {
            float u = Through;
            float opening = Mathf.InverseLerp(twirlSpan.x - letGoSpan * 0.5f, twirlSpan.x + letGoSpan * 0.5f, u);
            float closing = Mathf.InverseLerp(twirlSpan.y - fingerCloseSpan * 0.5f, twirlSpan.y + fingerCloseSpan * 0.5f, u - lag);
            return Mathf.SmoothStep(0f, 1f, opening) * (1f - Mathf.SmoothStep(0f, 1f, closing));
        }

        /// 0 to 1: the grip slackens once the magazine is falling and clamps again as the new one seats
        public float MagSwap
        {
            get
            {
                float u = Through;
                float slack = Mathf.Clamp01((u - magazineFalls.x) / 0.08f);
                float clamp = Mathf.Clamp01((u - magazineRises.y + 0.02f) / 0.06f);
                return Mathf.SmoothStep(0f, 1f, slack) * (1f - Mathf.SmoothStep(0f, 1f, clamp));
            }
        }

        /// 0 to 1 and back: the thumb pressing the magazine release, just as the magazine lets go
        public float ReleasePress { get { return Hump(Through, magazineFalls.x - 0.06f, magazineFalls.x + 0.05f); } }

        /// The wrist throwing the gun round the finger as the twirl starts, then a smaller catch back the
        /// other way as it comes round. Hand and gun move together; only the spin itself is the gun's
        private float Flick()
        {
            float u = Through;
            float span = Mathf.Max(0.01f, (twirlSpan.y - twirlSpan.x) * 0.25f);
            return wristFlick.x * Hump(u, twirlSpan.x - span * 0.5f, twirlSpan.x + span) -
                   wristFlick.y * Hump(u, twirlSpan.y - span * 0.5f, twirlSpan.y + span * 0.5f);
        }

        /// 0 up to 1 and back, starting and stopping at rest so nothing lurches into or out of it
        private static float Hump(float u, float from, float to)
        {
            float s = Mathf.Sin(Mathf.InverseLerp(from, to, u) * Mathf.PI);
            return s * s;
        }

        /// How far through the reload, or -1 when not reloading
        private float Through { get { return IsReloading ? 1f - _reloadRemaining / Mathf.Max(0.01f, reloadSeconds) : -1f; } }

        /// 0 before the window, 1 after it, and the share of the way through it in between
        private float Phase(Vector2 window)
        {
            if (!IsReloading) return 0f;

            float through = 1f - _reloadRemaining / Mathf.Max(0.01f, reloadSeconds);
            return Mathf.Clamp01((through - window.x) / Mathf.Max(0.01f, window.y - window.x));
        }

        /// Falls out of the grip, shrinking away as it goes, then a fresh one grows in as it slides up into place
        private void MoveTheMagazine()
        {
            if (_magazine == null) return;

            float drop = Phase(magazineFalls);
            float rise = Phase(magazineRises);

            if (!IsReloading || rise >= 1f)
            {
                _magazine.localPosition = _magazineRest;
                _magazine.localScale = _magazineScale;
            }
            else if (rise > 0f)
            {
                float t = 1f - (1f - rise) * (1f - rise);
                _magazine.localPosition = _magazineRest + _magazineDown * magazineRise * (1f - t);
                _magazine.localScale = _magazineScale * Mathf.Clamp01(rise / 0.4f);
            }
            else
            {
                _magazine.localPosition = _magazineRest + _magazineDown * magazineDrop * drop * drop;
                _magazine.localScale = _magazineScale * Mathf.Clamp01((1f - drop) / 0.4f);
            }
        }

        /// The muzzle was placed for the old stand-in gun. It moves to the middle of the fitted model's barrel
        /// mouth, so the laser, the bullets and the flash (a child of it) all leave from the bore
        private void MuzzleOnTheBarrel()
        {
            if (muzzle == null || muzzle == transform) return;

            MeshFilter barrel = null;
            foreach (var part in visual.GetComponentsInChildren<MeshFilter>(true))
            {
                if (part.sharedMesh != null && part.name.ToLowerInvariant().Contains("barrel")) { barrel = part; break; }
            }
            if (barrel == null) return;

            var box = barrel.sharedMesh.bounds;
            var low = Vector3.one * float.MaxValue;
            var high = Vector3.one * float.MinValue;
            for (int corner = 0; corner < 8; corner++)
            {
                var onGun = transform.InverseTransformPoint(barrel.transform.TransformPoint(new Vector3(
                    (corner & 1) == 0 ? box.min.x : box.max.x,
                    (corner & 2) == 0 ? box.min.y : box.max.y,
                    (corner & 4) == 0 ? box.min.z : box.max.z)));
                low = Vector3.Min(low, onGun);
                high = Vector3.Max(high, onGun);
            }

            muzzle.position = transform.TransformPoint(new Vector3((low.x + high.x) * 0.5f, (low.y + high.y) * 0.5f, high.z));
        }

        /// The fitted model's magazine part, found by name as setup step 16 finds it. The trigger sits
        /// just above its front top edge, so that is where the twirl turns
        private void FindTheMagazine()
        {
            foreach (var part in visual.GetComponentsInChildren<Transform>(true))
            {
                if (!part.name.ToLowerInvariant().Contains("magazine")) continue;

                var filter = part.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;

                _magazine = part;
                break;
            }

            if (_magazine == null) return;

            _magazineRest = _magazine.localPosition;
            _magazineScale = _magazine.localScale;
            _magazineDown = _magazine.parent.InverseTransformVector(transform.TransformDirection(magazineSlide.normalized));

            var box = _magazine.GetComponent<MeshFilter>().sharedMesh.bounds;
            float top = float.NegativeInfinity;
            float front = float.NegativeInfinity;
            Vector3 middle = _magazine.TransformPoint(box.center);
            for (int corner = 0; corner < 8; corner++)
            {
                var world = _magazine.TransformPoint(new Vector3(
                    (corner & 1) == 0 ? box.min.x : box.max.x,
                    (corner & 2) == 0 ? box.min.y : box.max.y,
                    (corner & 4) == 0 ? box.min.z : box.max.z));
                top = Mathf.Max(top, Vector3.Dot(world - middle, transform.up));
                front = Mathf.Max(front, Vector3.Dot(world - middle, transform.forward));
            }

            var trigger = middle + transform.up * (top + 0.02f) + transform.forward * (front - 0.012f);
            _twirlPivot = visual.InverseTransformPoint(trigger);
        }

        /// 0 to 1 and back over the reload, eased at both ends so the gun swings up and settles back.
        /// The glove reads it too, so the hand loosens in step with the gun
        public float ReloadBlend
        {
            get
            {
                if (!IsReloading) return 0f;

                float into = (reloadSeconds - _reloadRemaining) / Mathf.Max(0.01f, tiltInAndOutSeconds);
                float outOf = _reloadRemaining / Mathf.Max(0.01f, tiltInAndOutSeconds);
                return Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, Mathf.Min(into, outOf)));
            }
        }

        /// Kicks back on every shot and stays locked back for the whole reload, as an empty pistol does
        private void MoveTheSlide()
        {
            if (slide == null) return;

            _slideKick = Mathf.MoveTowards(_slideKick, 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, slideSeconds));
            float back = IsReloading ? slideLockTravel : slideKickTravel * _slideKick;
            slide.localPosition = _slideRest + _slideBack * back;
        }

        private IVRAvatarHand HandRig()
        {
            var avatar = VRAvatar.Active;
            if (avatar == null) return null;

            if (avatar.PrimaryHand != null && avatar.PrimaryHand.LimbType == hand) return avatar.PrimaryHand;
            if (avatar.SecondaryHand != null && avatar.SecondaryHand.LimbType == hand) return avatar.SecondaryHand;
            return fallBackToPrimaryHand ? avatar.PrimaryHand : null;
        }

        private void FollowHand()
        {
            var rig = HandRig();
            if (rig == null || rig.Transform == null) { Rest(); return; }

            Vector3 position;
            Quaternion rotation;
            HeldAt(rig.Transform, out position, out rotation);

            // Flies the short way off the bench into the hand rather than teleporting there
            _grab = Mathf.MoveTowards(_grab, 1f, Time.unscaledDeltaTime / Mathf.Max(0.01f, grabSeconds));
            float t = Mathf.SmoothStep(0f, 1f, _grab);
            transform.position = Vector3.Lerp(_grabFrom, position, t);
            transform.rotation = Quaternion.Slerp(_grabFromRotation, rotation, t);
        }

        /// Where the gun sits in a hand whose controller is at rig
        public void HeldAt(Transform rig, out Vector3 position, out Quaternion rotation)
        {
            position = rig.TransformPoint(gripLocalPosition);
            rotation = rig.rotation * Quaternion.Euler(gripLocalEuler);
        }

        /// Where it sits when nobody is holding it. With the holster off it simply stays where the
        /// scene put it, which is how a gun waits on the table to be picked up
        private void Rest()
        {
            if (holsterToHead) HolsterPose();
        }

        private void HolsterPose()
        {
            var avatar = VRAvatar.Active;
            if (avatar == null || avatar.Head == null || avatar.Head.Transform == null) return;

            var head = avatar.Head.Transform;
            transform.position = head.TransformPoint(holsterLocalPosition);
            transform.rotation = head.rotation * Quaternion.Euler(holsterLocalEuler);
        }

        private bool WantsToFire()
        {
            var powerUps = PowerUps.Instance;
            bool auto = automatic || (powerUps != null && powerUps.FullAuto);

            var rig = HandRig();
            var device = rig != null ? rig.InputDevice : null;

            if (device != null)
            {
                if (auto ? device.GetButton(VRButton.Trigger) : device.GetButtonDown(VRButton.Trigger))
                    return true;
            }

            if (!mouseFires) return false;
            return auto ? Input.GetMouseButton(0) : Input.GetMouseButtonDown(0);
        }

        private bool WantsToReload()
        {
            var rig = HandRig();
            var device = rig != null ? rig.InputDevice : null;
            if (device != null && device.GetButtonDown(PistolPickup.GripButton)) return true;

            return mouseFires && Input.GetMouseButtonDown(1);
        }

        private void TickReload()
        {
            if (_reloadRemaining <= 0f) return;

            float before = 1f - _reloadRemaining / Mathf.Max(0.01f, reloadSeconds);
            _reloadRemaining -= Time.unscaledDeltaTime;
            float after = 1f - _reloadRemaining / Mathf.Max(0.01f, reloadSeconds);

            Cue(magazineOutClip, magazineFalls.x, before, after);
            Cue(reloadClip, magazineRises.x, before, after);
            Cue(twirlClip, twirlSpan.x, before, after);

            // The fresh magazine slapped home jolts the gun, and the hand riding it
            if (before < magazineRises.y && after >= magazineRises.y) _recoil += recoilKick * insertKick;

            if (_reloadRemaining > 0f) return;

            _reloadRemaining = 0f;
            _rounds = magazineSize;
            _recoil += recoilKick * slideSlamKick;
            Play(slideReleaseClip, 0.7f);
            RaiseAmmo();
        }

        private void Cue(AudioClip clip, float at, float before, float after)
        {
            if (before < at && after >= at) Play(clip, 0.7f);
        }

        private void TryFire()
        {
            if (_cooldown > 0f || _reloadRemaining > 0f || Time.unscaledTime < _lockedUntil) return;

            var powerUps = PowerUps.Instance;
            bool bottomless = powerUps != null && powerUps.BottomlessMagazine;

            // Dry. Reloading is the player's job, the click tells them so
            if (magazineSize > 0 && _rounds <= 0 && !bottomless)
            {
                _cooldown = fireInterval;
                Play(Pick(emptyClips), 0.5f);
                return;
            }

            int pellets = powerUps != null ? powerUps.Pellets : 1;
            float spread = pellets > 1 ? powerUps.PelletSpread : spreadDegrees;

            Transform from = Muzzle;
            var pool = ProjectilePool.Instance;
            Vector3 aim = _sight != null && pool != null
                        ? _sight.FireDirection(from.position, from.forward, pool.RoundSpeed, pool.RoundGravityScale)
                        : from.forward;
            int fired = 0;
            Color colour = _tint != null ? _tint.Current(gunfireColour) : gunfireColour;

            for (int i = 0; i < pellets; i++)
            {
                if (pool != null && pool.Fire(from.position, Scatter(aim, spread), 1f, colour)) fired++;
            }

            _cooldown = fireInterval;

            if (fired == 0)
            {
                Play(Pick(emptyClips), 0.5f);
                return;
            }

            if (magazineSize > 0 && !bottomless) _rounds--;
            bool auto = automatic || (powerUps != null && powerUps.FullAuto);
            float share = auto ? autoKick : 1f;
            _recoil = Mathf.Min(_recoil + recoilKick * share * (pellets > 1 ? 1.6f : 1f), recoilKick * maxStackedKicks);
            float shake = recoilShake * share;
            _shake = new Vector2(UnityEngine.Random.Range(-shake, shake), UnityEngine.Random.Range(-shake, shake));

            Flash(colour);
            _slideKick = 1f;
            Play(Pick(fireClips), fireVolume);

            if (HapticPulse.Instance != null) HapticPulse.Instance.Hit(hand, pellets > 1 ? scatterHaptic : fireHaptic);

            RaiseAmmo();
            if (Fired != null) Fired();
        }

        private void Flash(Color colour)
        {
            if (muzzleFlash == null) return;

            if (tintedByPowerUp != null)
            {
                for (int i = 0; i < tintedByPowerUp.Length; i++)
                {
                    if (tintedByPowerUp[i] == null) continue;

                    var main = tintedByPowerUp[i].main;
                    main.startColor = colour;
                }
            }

            muzzleFlash.Play(true);
        }

        private void BeginReload()
        {
            if (_reloadRemaining > 0f || magazineSize <= 0 || _rounds >= magazineSize) return;

            _reloadRemaining = reloadSeconds;
            RaiseAmmo();
        }

        private static Vector3 Scatter(Vector3 forward, float degrees)
        {
            if (degrees <= 0f) return forward;
            return Quaternion.Euler(UnityEngine.Random.Range(-degrees, degrees),
                                    UnityEngine.Random.Range(-degrees, degrees),
                                    0f) * forward;
        }

        private void Play(AudioClip clip, float volume)
        {
            if (clip == null || _audio == null) return;

            _audio.pitch = 1f + (pitchJitter > 0f ? UnityEngine.Random.Range(-pitchJitter, pitchJitter) : 0f);
            _audio.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        private void RaiseAmmo()
        {
            if (AmmoChanged != null) AmmoChanged(_rounds, magazineSize);
        }

        private static AudioClip Pick(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return null;
            return clips[UnityEngine.Random.Range(0, clips.Length)];
        }
    }
}
