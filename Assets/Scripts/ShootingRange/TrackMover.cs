using System;
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
        private PowerUpKind? _special;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _body.isKinematic = true;

            // Movement is stepped at 33 Hz, without this the targets visibly stutter across the range
            _body.interpolation = RigidbodyInterpolation.Interpolate;

            if (target == null) target = GetComponent<PopUpTarget>();
            if (target != null) target.Spent += Retire;
        }

        private void OnDestroy()
        {
            if (target != null) target.Spent -= Retire;
        }

        /// holdSeconds above zero is one that stands still instead of travelling, for that long. The
        /// booking is made from the same number, so it cannot be told one thing and the mover another
        /// special is the power up a glowing sign hands over when it is shot, or null for an ordinary one
        public void Launch(TrackRail rail, float speed, float holdSeconds, PowerUpKind? special)
        {
            _special = special;
            _rail = rail;
            _speed = speed;
            _distance = 0f;
            _held = 0f;
            _hold = holdSeconds;
            _stationary = holdSeconds > 0f;
            _running = true;

            transform.position = rail.PointAt(0f);

            // A wall riser runs straight up, and LookRotation with world up as its reference is
            // degenerate there
            Vector3 reference = Mathf.Abs(Vector3.Dot(rail.Direction, Vector3.up)) > 0.99f
                              ? Vector3.forward
                              : Vector3.up;
            transform.rotation = Quaternion.LookRotation(rail.Direction, reference);

            gameObject.SetActive(true);

            if (target == null) return;

            target.Present(rail.ShownOffset, rail.SignOffset, rail.Hinge, rail.Cover);
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
                if (_held >= _hold) ReachEnd();
                return;
            }

            _distance += _speed * Time.fixedDeltaTime;

            if (_distance >= _rail.Length) { ReachEnd(); return; }

            _body.MovePosition(_rail.PointAt(_distance));
        }

        public void OnShot(Vector3 point, Vector3 direction, float impulse)
        {
            if (!Exposed) return;

            if (RangeGame.Instance != null) RangeGame.Instance.Scored(hitScore, point, false, true);
            if (_special.HasValue && PowerUps.Instance != null) PowerUps.Instance.Grant(_special.Value);
            bool special = _special.HasValue;

            // Counted the moment it is hit, so the HUD and the combo never wait for the figure to
            // finish spinning or falling
            Resolve(true);

            // A special always shatters, the biggest reaction there is for the hardest shot
            if (target != null) target.Hit(point, direction, special);
            else Retire();
        }

        /// Taken off the range between rounds, which is not an outcome, so nobody hears about it
        public void Stop()
        {
            _running = false;
            _rail = null;
            Retire();
        }

        private void ReachEnd()
        {
            if (!_stationary && _rail.ReachesPlayer && RangeGame.Instance != null)
                RangeGame.Instance.HitPlayer(TargetPoint);

            Resolve(false);

            // It stops at the end and drops out of sight rather than blinking away
            if (target != null) target.Lower();
            else Retire();
        }

        private void Resolve(bool shot)
        {
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
