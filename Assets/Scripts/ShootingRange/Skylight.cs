using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    /// A boarded skylight. Once its last board is shot off, a beam of daylight falls for a few seconds, then a
    /// cloud passes, once a round. Vampire signs caught under it smoke, sizzle and stagger, but still have to be
    /// shot: the sun weakens them, it never scores (ADR 0063). The boards are a ReleaseOnShot on the same object,
    /// set to one at a time
    public class Skylight : RoomReaction
    {
        [SerializeField] private Renderer beam;

        [Header("Beam (data, seconds and metres)")]
        [SerializeField] private float beamSeconds = 5f;
        [SerializeField] private float fadeInSeconds = 0.3f;
        [SerializeField, Tooltip("How long the cloud takes to cover it")] private float fadeOutSeconds = 1f;
        [SerializeField] private float strength = 0.6f;
        [SerializeField] private float radius = 0.5f;

        [Header("Who it burns: a sign whose name holds any of these")]
        [SerializeField] private string[] burns = { "vampire", "vimpire" };

        [Header("Audio")]
        [SerializeField] private AudioClip[] openClips;
        [SerializeField] private AudioClip[] sizzleClips;
        [SerializeField, Range(0f, 1f)] private float volume = 0.7f;

        private static readonly int StrengthId = Shader.PropertyToID("_Strength");

        private MaterialPropertyBlock _block;
        private float _openedAt = -1f;
        private bool _used;

        private ReleaseOnShot _boards;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            Shine(0f);

            _boards = GetComponent<ReleaseOnShot>();
            if (_boards != null) _boards.AllLoose += Open;
        }

        protected override void OnDestroy()
        {
            if (_boards != null) _boards.AllLoose -= Open;
            base.OnDestroy();
        }

        /// The boards decide when it opens; without any, the first hit does
        public override void OnShot(Vector3 point, Vector3 direction, float impulse)
        {
            if (_boards == null) Open();
        }

        private void Open()
        {
            if (_used) return;

            _used = true;
            _openedAt = Time.time;
            PlayOne(openClips, transform.position, volume);
        }

        private void Update()
        {
            if (_openedAt < 0f) return;

            float age = Time.time - _openedAt;
            if (age >= beamSeconds + fadeOutSeconds)
            {
                _openedAt = -1f;
                Shine(0f);
                return;
            }

            float on = age < beamSeconds
                     ? Mathf.Clamp01(age / Mathf.Max(0.01f, fadeInSeconds))
                     : 1f - (age - beamSeconds) / Mathf.Max(0.01f, fadeOutSeconds);
            Shine(strength * on);

            if (age < beamSeconds) Burn();
        }

        private void Burn()
        {
            var movers = TrackMover.Movers;
            var axis = transform.position;

            for (int i = 0; i < movers.Count; i++)
            {
                var mover = movers[i];
                if (!mover.Exposed || mover.Target == null) continue;

                var offset = mover.TargetPoint - axis;
                offset.y = 0f;
                if (offset.sqrMagnitude > radius * radius || !Burns(mover.Target.SignName)) continue;

                if (mover.Target.Scorch()) PlayOne(sizzleClips, mover.TargetPoint, volume);
            }
        }

        private bool Burns(string sign)
        {
            sign = sign.ToLowerInvariant();
            foreach (var word in burns)
            {
                if (!string.IsNullOrEmpty(word) && sign.Contains(word.ToLowerInvariant())) return true;
            }

            return false;
        }

        private void Shine(float amount)
        {
            if (beam == null) return;

            beam.enabled = amount > 0f;
            beam.GetPropertyBlock(_block);
            _block.SetFloat(StrengthId, amount);
            beam.SetPropertyBlock(_block);
        }

        protected override void Restore()
        {
            _used = false;
            _openedAt = -1f;
            Shine(0f);
        }
    }
}
