using System;
using System.Collections.Generic;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    [RequireComponent(typeof(Rigidbody))]
    public class TrackMover : MonoBehaviour, IShootable
    {
        [Header("Refs")]
        [SerializeField] private PopUpTarget target;

        [Header("Scoring (data)")]
        [SerializeField] private int hitScore = 100;

        [Header("Shape (data, metres)")]
        [SerializeField] private float moverLength = 0.4f;

        [Header("Standing still (data, seconds)")]
        [SerializeField] private float minHold = 2f;
        [SerializeField] private float maxHold = 4f;

        [Header("Near misses (data): a round striking the room this close makes the sign flinch")]
        [SerializeField] private float flinchRadius = 0.6f;
        [SerializeField, Tooltip("Degrees a second of twitch it starts with")] private float flinchKick = 150f;

        [Header("Rail sounds (optional): a clatter that follows the speed, a clunk off the mark, a squeal at the end")]
        [SerializeField] private AudioSource clatter;
        [SerializeField] private AudioClip[] launchClips;
        [SerializeField] private AudioClip[] stopClips;
        [SerializeField, Range(0f, 1f)] private float clipVolume = 0.5f;
        [SerializeField, Tooltip("Speed, m/s, at which the clatter plays at its own pitch")] private float clatterSpeed = 1.2f;
        [SerializeField] private float lowestPitch = 0.8f;
        [SerializeField] private float highestPitch = 1.4f;

        public float MoverLength { get { return moverLength; } }

        /// How long it stands on the rail's origin before it sets off, so the booking knows the spot is
        /// taken while the sign is still swinging up. A sign with no hinge is up at once
        public float SettleSeconds(Hinge hinge)
        {
            return target != null && hinge != Hinge.None ? target.TimeToRise : 0f;
        }

        /// How long the sign takes to drop out of sight once it stops
        public float FallSeconds { get { return target != null ? target.TimeToFall : 0f; } }

        /// How long one that is not going to travel would stand there
        public float RollHold() { return UnityEngine.Random.Range(minHold, maxHold); }

        /// Riding a rail with its figure in view, shootable, and worth launching a bat from
        public bool Exposed { get { return _running && (target == null || target.Live); } }

        /// Where the figure is, which is not where the carriage is once it has popped up
        public Vector3 TargetPoint { get { return target != null ? target.HitCentre : transform.position; } }

        /// Shot (true), or reached the end of its rail (false)
        public event Action<TrackMover, bool> Resolved;

        private Rigidbody _body;
        private TrackRail _rail;
        private float _speed;
        private float _distance;
        private float _held;
        private float _hold;
        private bool _stationary;
        private bool _running;
        private float _deadline;
        private PowerUpKind? _special;
        private bool _rolling;

        private static readonly List<TrackMover> All = new List<TrackMover>();

        /// Every carriage in the pool, up or not
        public static IReadOnlyList<TrackMover> Movers { get { return All; } }

        public PopUpTarget Target { get { return target; } }

        /// A round struck the room at this point: every sign up close enough to it flinches
        public static void NearMiss(Vector3 at)
        {
            for (int i = 0; i < All.Count; i++) All[i].Flinch(at);
        }

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _body.isKinematic = true;

            // Movement is stepped at 33 Hz, without this the targets visibly stutter across the range
            _body.interpolation = RigidbodyInterpolation.Interpolate;

            All.Add(this);
            if (target == null) target = GetComponent<PopUpTarget>();
            if (target != null) target.Spent += Retire;
        }

        private void OnDestroy()
        {
            All.Remove(this);
            if (target != null) target.Spent -= Retire;
        }

        /// holdSeconds above zero is one that stands still instead of travelling, for that long. The
        /// booking is made from the same number, so it cannot be told one thing and the mover another
        /// special is the power up a glowing sign hands over when it is shot, or null for an ordinary one.
        /// deadline is when, on the unscaled clock, it has to reach its end, or 0 for no hurry
        public void Launch(TrackRail rail, float speed, float holdSeconds, PowerUpKind? special, float deadline)
        {
            _special = special;
            _deadline = deadline;
            _rail = rail;
            _speed = speed;
            _distance = 0f;
            _held = 0f;
            _hold = holdSeconds;
            _stationary = holdSeconds > 0f;
            _running = true;
            _rolling = false;

            transform.position = rail.PointAt(0f);

            // A wall riser runs straight up, and LookRotation with world up as its reference is
            // degenerate there
            Vector3 reference = Mathf.Abs(Vector3.Dot(rail.Direction, Vector3.up)) > 0.99f
                              ? Vector3.forward
                              : Vector3.up;
            transform.rotation = Quaternion.LookRotation(rail.Direction, reference);

            gameObject.SetActive(true);
            rail.Warn(SettleSeconds(rail.Hinge));

            if (target == null) return;

            target.Present(rail);
            var powerUps = PowerUps.Instance;
            target.Glow(special.HasValue && powerUps != null ? powerUps.Colour(special.Value) : (Color?)null);
        }

        private void FixedUpdate()
        {
            if (!_running || _rail == null) return;

            // Nothing moves until its sign is up, and every sign takes the same time to get there
            if (target != null && !target.Presented) return;

            // Some of them never travel at all, up, a few seconds in the open, then back down
            if (_stationary)
            {
                _held += Time.fixedDeltaTime;
                if (_held >= _hold || (_deadline > 0f && Time.unscaledTime >= _deadline)) ReachEnd();
                return;
            }

            if (_deadline > 0f) _speed = SpeedToDeadline();
            _distance += _speed * Time.fixedDeltaTime;
            Rattle();

            if (_distance >= _rail.Length) { ReachEnd(); return; }

            _body.MovePosition(_rail.PointAt(_distance));
        }

        public void OnShot(Vector3 point, Vector3 direction, float impulse) { Shot(point, direction, false); }

        /// A ricochet that found this sign: the biggest reaction there is, and the barker notices. Worth no
        /// more than any other hit (ADR 0063)
        public void OnTrickShot(Vector3 point, Vector3 direction) { Shot(point, direction, true); }

        private void Flinch(Vector3 at)
        {
            if (!Exposed || target == null) return;

            var offset = TargetPoint - at;
            if (offset.sqrMagnitude > flinchRadius * flinchRadius) return;

            // Away from where the round struck
            float away = Vector3.Dot(offset, target.Facing * Vector3.right) >= 0f ? 1f : -1f;
            target.Flinch(away * flinchKick);
        }

        private void Shot(Vector3 point, Vector3 direction, bool trick)
        {
            if (!Exposed) return;

            if (RangeGame.Instance != null) RangeGame.Instance.Scored(hitScore, point, false, true);
            if (_special.HasValue && PowerUps.Instance != null) PowerUps.Instance.Grant(_special.Value);
            bool special = _special.HasValue;

            // Counted the moment it is hit, so the HUD and the combo never wait for the figure to
            // finish spinning or falling
            Resolve(true);

            if (trick && CarnyVoice.Instance != null) CarnyVoice.Instance.TrickShot();

            // A special or a trick shot always shatters, the biggest reaction there is for the hardest shots
            if (target != null) target.Hit(point, direction, special || trick);
            else Retire();
        }

        /// The speed that ends the run on its deadline. The deadline is on the round's unscaled clock and
        /// the run is in scaled time, so it is asked again every step and a slow motion mid-run cannot
        /// carry the sign past the bell
        private float SpeedToDeadline()
        {
            float left = Mathf.Max(Time.fixedDeltaTime, (_deadline - Time.unscaledTime) * Time.timeScale);
            return (_rail.Length - _distance) / left;
        }

        /// Taken off the range when the round ends, which is not an outcome, so nobody hears about it.
        /// One still up drops out of sight and one already going down finishes, rather than blinking away
        public void Stop()
        {
            Quiet();
            _running = false;
            _rail = null;
            _special = null;

            if (target != null && target.Live) target.Lower();
            else if (target == null || !target.Leaving) Retire();
        }

        private void ReachEnd()
        {
            if (!_stationary && _rail.ReachesPlayer && RangeGame.Instance != null)
                RangeGame.Instance.HitPlayer(TargetPoint);

            if (_rolling) PlayOne(stopClips);
            Resolve(false);

            // It got away, so it gloats before it drops out of sight rather than blinking away
            if (target != null) target.Escape();
            else Retire();
        }

        private void Rattle()
        {
            if (!_rolling)
            {
                _rolling = true;
                PlayOne(launchClips);
                if (clatter != null) clatter.Play();
            }

            if (clatter != null) clatter.pitch = Mathf.Clamp(_speed / Mathf.Max(0.01f, clatterSpeed), lowestPitch, highestPitch);
        }

        private void Quiet()
        {
            _rolling = false;
            if (clatter != null) clatter.Stop();
        }

        private void PlayOne(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0 || ImpactFX.Instance == null) return;
            ImpactFX.Instance.PlayClip(clips[UnityEngine.Random.Range(0, clips.Length)], transform.position, clipVolume);
        }

        private void Resolve(bool shot)
        {
            Quiet();
            _running = false;
            _special = null;
            _rail = null;

            if (Resolved != null) Resolved(this, shot);
        }

        private void Retire()
        {
            if (target != null) target.Cancel();
            gameObject.SetActive(false);
        }
    }
}
