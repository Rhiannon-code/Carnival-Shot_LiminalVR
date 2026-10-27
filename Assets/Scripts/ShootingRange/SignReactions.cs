using System;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    public enum SignReaction { Spin, Fold, Shatter, Flip, Boing, KnockedOff, Bats, SwingAndDrop }

    /// What a shot sign does, picked by where the round struck it, and the reactions PopUpTarget hands over to
    /// this: the knock-down flip, the springy stand, the roof sign that swings on one corner and lets go,
    /// knocked clean off the rail, and bursting into bats (ADR 0063). Scoring never waits for any of it
    [RequireComponent(typeof(PopUpTarget))]
    public class SignReactions : MonoBehaviour
    {
        [Serializable]
        public struct Odds
        {
            public SignReaction reaction;
            public float odds;

            public Odds(SignReaction reaction, float odds)
            {
                this.reaction = reaction;
                this.odds = odds;
            }
        }

        [Header("Where it was hit (data, shares of the sign)")]
        [SerializeField, Range(0f, 1f), Tooltip("The top this share of its height counts as the head")]
        private float headShare = 0.25f;
        [SerializeField, Range(0f, 1f), Tooltip("Further from the middle than this share of its half-width counts as the edge")]
        private float edgeShare = 0.6f;

        [Header("What each part does (data, relative odds; ones a sign can't do are skipped)")]
        [SerializeField] private Odds[] head =
        {
            new Odds(SignReaction.KnockedOff, 3f), new Odds(SignReaction.Bats, 3f), new Odds(SignReaction.Shatter, 2f),
        };
        [SerializeField] private Odds[] edge =
        {
            new Odds(SignReaction.Spin, 6f), new Odds(SignReaction.SwingAndDrop, 4f),
        };
        [SerializeField] private Odds[] body =
        {
            new Odds(SignReaction.Fold, 3f), new Odds(SignReaction.Flip, 4f), new Odds(SignReaction.Boing, 3f),
            new Odds(SignReaction.Spin, 2f), new Odds(SignReaction.SwingAndDrop, 3f), new Odds(SignReaction.Bats, 1f),
        };

        [Header("Knock-down flip (data)")]
        [SerializeField] private float flipSeconds = 0.25f;
        [SerializeField] private float bounceDegrees = 12f;
        [SerializeField] private float bounceSeconds = 0.25f;

        [Header("Springy stand (data)")]
        [SerializeField] private float boingDegrees = 35f;
        [SerializeField] private float boingCycles = 4f;
        [SerializeField] private float boingSeconds = 0.8f;

        [Header("Roof sign swings on one corner, then lets go (data)")]
        [SerializeField] private float hangDegrees = 30f;
        [SerializeField] private float hangCycles = 1.5f;
        [SerializeField] private float hangSeconds = 0.5f;
        [SerializeField] private float dropSeconds = 0.6f;
        [SerializeField, Tooltip("Degrees a second it tumbles as it drops")] private float dropSpin = 90f;

        [Header("Knocked off the rail (data, metres and degrees a second)")]
        [SerializeField] private float throwSpeed = 2.5f;
        [SerializeField] private float throwLift = 2.5f;
        [SerializeField] private float throwSpin = 540f;
        [SerializeField] private float throwSeconds = 0.9f;

        [Header("Bursts into bats (data)")]
        [SerializeField] private int bats = 4;

        [Header("Sounds, a set per reaction (empty = silent)")]
        [SerializeField] private AudioClip[] spinClips;
        [SerializeField] private AudioClip[] foldClips;
        [SerializeField] private AudioClip[] flipClips;
        [SerializeField] private AudioClip[] slapClips;
        [SerializeField] private AudioClip[] boingClips;
        [SerializeField] private AudioClip[] hangClips;
        [SerializeField] private AudioClip[] letGoClips;
        [SerializeField] private AudioClip[] throwClips;
        [SerializeField] private AudioClip[] batClips;
        [SerializeField, Range(0f, 1f)] private float volume = 0.6f;

        private PopUpTarget _sign;
        private SignReaction _playing;
        private bool _active;
        private float _timer;
        private float _startSwing;
        private bool _landed;
        private Vector3 _from;
        private Quaternion _fromRotation;
        private Vector3 _pivot;
        private Vector3 _axis;
        private float _way;
        private Vector3 _velocity;
        private Vector3 _tumble;

        public SignReaction Pick(PopUpTarget sign, Vector3 point)
        {
            var box = sign.SignBounds;
            Vector3 right = sign.Facing * Vector3.right;
            float half = Mathf.Max(0.01f, Mathf.Abs(right.x) * box.extents.x + Mathf.Abs(right.z) * box.extents.z);

            float up = Mathf.InverseLerp(box.min.y, box.max.y, point.y);
            float across = Mathf.Abs(Vector3.Dot(point - box.center, right)) / half;

            var table = up >= 1f - headShare ? head : across >= edgeShare ? edge : body;
            return Roll(table, sign.Hinge);
        }

        public void Sound(SignReaction reaction, Vector3 at)
        {
            switch (reaction)
            {
                case SignReaction.Spin: PlayOne(spinClips, at); break;
                case SignReaction.Fold: PlayOne(foldClips, at); break;
                case SignReaction.Flip: PlayOne(flipClips, at); break;
                case SignReaction.Boing: PlayOne(boingClips, at); break;
                case SignReaction.SwingAndDrop: PlayOne(hangClips, at); break;
                case SignReaction.KnockedOff: PlayOne(throwClips, at); break;
                case SignReaction.Bats: PlayOne(batClips, at); break;
            }
        }

        public void Play(SignReaction reaction, PopUpTarget sign, Vector3 point, Vector3 direction)
        {
            _sign = sign;
            _playing = reaction;
            _timer = 0f;
            _landed = false;
            _active = true;

            var figure = sign.Figure;
            _from = figure.position;
            _fromRotation = figure.rotation;
            _startSwing = sign.Swing;

            var away = new Vector3(direction.x, 0f, direction.z);
            away = away.sqrMagnitude > 1e-6f ? away.normalized : sign.Facing * Vector3.back;

            switch (reaction)
            {
                case SignReaction.Bats:
                    if (BatBurst.Instance != null) BatBurst.Instance.Burst(sign.HitCentre, bats, away);
                    End();
                    break;

                case SignReaction.KnockedOff:
                    _velocity = away * throwSpeed + Vector3.up * throwLift;
                    _tumble = UnityEngine.Random.onUnitSphere;
                    break;

                case SignReaction.SwingAndDrop:
                {
                    // It hangs on by the top corner away from the hit, and the side that was hit drops
                    var box = sign.SignBounds;
                    Vector3 right = sign.Facing * Vector3.right;
                    float half = Mathf.Abs(right.x) * box.extents.x + Mathf.Abs(right.z) * box.extents.z;
                    float side = Mathf.Sign(Vector3.Dot(point - box.center, right));
                    if (side == 0f) side = 1f;

                    _pivot = box.center - right * side * half + Vector3.up * box.extents.y;
                    _axis = away;
                    var hitSide = box.center + right * side * half;
                    _way = Vector3.Dot(Vector3.Cross(_axis, hitSide - _pivot), Vector3.up) < 0f ? 1f : -1f;
                    break;
                }
            }
        }

        public void Stop() { _active = false; }

        private void Update()
        {
            if (!_active || _sign == null) return;

            _timer += Time.deltaTime;

            switch (_playing)
            {
                case SignReaction.Flip: Flip(); break;
                case SignReaction.Boing: Boing(); break;
                case SignReaction.SwingAndDrop: SwingAndDrop(); break;
                case SignReaction.KnockedOff: KnockedOff(); break;
            }
        }

        /// Slapped flat on its back faster than a fold, with one bounce off the deck
        private void Flip()
        {
            float folded = _sign.Folded;
            if (_timer < flipSeconds)
            {
                float t = _timer / flipSeconds;
                _sign.Pose(Mathf.Lerp(_startSwing, folded, t * t));
                return;
            }

            if (!_landed)
            {
                _landed = true;
                PlayOne(slapClips, _sign.HitCentre);
            }

            float b = (_timer - flipSeconds) / Mathf.Max(0.01f, bounceSeconds);
            if (b >= 1f) { _sign.Pose(folded); End(); return; }

            _sign.Pose(folded - Mathf.Sign(folded) * bounceDegrees * Mathf.Sin(b * Mathf.PI));
        }

        /// Whipped back and forth on its stand like a door stop, then folds away as usual
        private void Boing()
        {
            float t = _timer / Mathf.Max(0.01f, boingSeconds);
            if (t >= 1f)
            {
                _active = false;
                _sign.Fold();
                return;
            }

            _sign.Pose(Mathf.Sign(_sign.Folded) * boingDegrees * Mathf.Sin(t * boingCycles * 2f * Mathf.PI) * (1f - t));
        }

        private void SwingAndDrop()
        {
            if (_timer < hangSeconds)
            {
                // Swings past the hang and rings back to it
                float t = _timer / hangSeconds;
                float angle = hangDegrees * (1f - Mathf.Cos(t * hangCycles * 2f * Mathf.PI) * (1f - t) * (1f - t));
                Hang(angle, Vector3.zero);
                return;
            }

            if (!_landed)
            {
                _landed = true;
                PlayOne(letGoClips, _sign.HitCentre);
            }

            float d = _timer - hangSeconds;
            if (d >= dropSeconds) { End(); return; }

            Hang(hangDegrees + dropSpin * d, 0.5f * Physics.gravity * d * d);
        }

        private void Hang(float angle, Vector3 fallen)
        {
            var figure = _sign.Figure;
            figure.SetPositionAndRotation(_from, _fromRotation);
            figure.RotateAround(_pivot, _axis, _way * angle);
            figure.position += fallen;
        }

        /// Torn off its stand and thrown to the back of the booth; the stand stays on the carriage
        private void KnockedOff()
        {
            if (_timer >= throwSeconds) { End(); return; }

            var figure = _sign.Figure;
            figure.position = _from + _velocity * _timer + 0.5f * Physics.gravity * _timer * _timer;
            figure.rotation = Quaternion.AngleAxis(throwSpin * _timer, _tumble) * _fromRotation;
        }

        private void End()
        {
            _active = false;
            _sign.Done();
        }

        private static SignReaction Roll(Odds[] table, Hinge hinge)
        {
            float total = 0f;
            foreach (var entry in table)
            {
                if (Can(entry.reaction, hinge)) total += Mathf.Max(0f, entry.odds);
            }

            if (total <= 0f) return hinge == Hinge.None ? SignReaction.Shatter : SignReaction.Fold;

            float roll = UnityEngine.Random.value * total;
            var picked = SignReaction.Fold;
            foreach (var entry in table)
            {
                if (!Can(entry.reaction, hinge) || entry.odds <= 0f) continue;

                picked = entry.reaction;
                roll -= entry.odds;
                if (roll < 0f) break;
            }

            return picked;
        }

        private static bool Can(SignReaction reaction, Hinge hinge)
        {
            switch (reaction)
            {
                case SignReaction.Flip:
                case SignReaction.Boing: return hinge == Hinge.Up;
                case SignReaction.SwingAndDrop: return hinge == Hinge.Down;
                case SignReaction.KnockedOff: return hinge == Hinge.Up || hinge == Hinge.Down;
                case SignReaction.Fold: return hinge != Hinge.None;
                default: return true;
            }
        }

        private void PlayOne(AudioClip[] clips, Vector3 at)
        {
            if (clips == null || clips.Length == 0 || ImpactFX.Instance == null) return;
            ImpactFX.Instance.PlayClip(clips[UnityEngine.Random.Range(0, clips.Length)], at, volume);
        }
    }
}
