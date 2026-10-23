using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    /// Parts that sit still until shot, then fall to physics: swords toppling off their stand, a plushie
    /// knocked off its perch. The part nearest the hit takes the shot; the rest get a nudge so they go too
    public class ReleaseOnShot : RoomReaction
    {
        [SerializeField] private Rigidbody[] pieces;

        [Header("Feel (data)")]
        [SerializeField] private float impulse = 1.5f;

        [SerializeField] private float nudge = 0.3f;
        [SerializeField, Range(0f, 1f)] private float lift = 0.3f;
        [SerializeField] private float unstickSpeed = 0.5f;

        [Header("Audio")]
        [SerializeField] private AudioClip[] hitClips;
        [SerializeField, Range(0f, 1f)] private float hitVolume = 0.7f;

        private Vector3[] _restPositions;
        private Quaternion[] _restRotations;
        private bool _released;

        private void Awake()
        {
            _restPositions = new Vector3[pieces.Length];
            _restRotations = new Quaternion[pieces.Length];

            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i] == null) continue;

                _restPositions[i] = pieces[i].transform.localPosition;
                _restRotations[i] = pieces[i].transform.localRotation;
                pieces[i].isKinematic = true;
                pieces[i].maxDepenetrationVelocity = unstickSpeed;

                // Swords racked side by side overlap; left to collide they would burst apart when let go
                var mine = pieces[i].GetComponentsInChildren<Collider>();
                for (int j = 0; j < i; j++)
                {
                    if (pieces[j] == null) continue;
                    foreach (var a in mine)
                    {
                        foreach (var b in pieces[j].GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(a, b);
                    }
                }
            }
        }

        public override void OnShot(Vector3 point, Vector3 direction, float impulse)
        {
            Rigidbody nearest = null;
            float best = float.MaxValue;
            foreach (var piece in pieces)
            {
                if (piece == null) continue;

                float distance = (piece.worldCenterOfMass - point).sqrMagnitude;
                if (distance < best) { best = distance; nearest = piece; }
            }

            if (nearest == null) return;

            var push = (direction.normalized + Vector3.up * lift).normalized;

            if (!_released)
            {
                _released = true;
                foreach (var piece in pieces)
                {
                    if (piece == null) continue;

                    piece.isKinematic = false;
                    if (piece != nearest) piece.AddForce(push * nudge, ForceMode.Impulse);
                }
            }

            nearest.AddForceAtPosition(push * this.impulse, point, ForceMode.Impulse);
            PlayOne(hitClips, point, hitVolume);
        }

        protected override void Restore()
        {
            _released = false;

            for (int i = 0; i < pieces.Length; i++)
            {
                var piece = pieces[i];
                if (piece == null) continue;

                if (!piece.isKinematic)
                {
                    piece.velocity = Vector3.zero;
                    piece.angularVelocity = Vector3.zero;
                }

                piece.isKinematic = true;
                piece.transform.localPosition = _restPositions[i];
                piece.transform.localRotation = _restRotations[i];
            }
        }
    }
}
