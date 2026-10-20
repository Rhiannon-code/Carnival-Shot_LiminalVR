using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    public class PowerUpTint : MonoBehaviour
    {
        [SerializeField] private PowerUps powerUps;
        [SerializeField] private Renderer[] renderers;
        [SerializeField] private Renderer[] glowShells;

        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [Header("More than one at once (data)")]
        [SerializeField] private Color stackedColour = new Color(1f, 0.15f, 0.55f);

        [Header("Picked up (data)")]
        [SerializeField] private float shimmerSeconds = 0.5f;

        [Header("Waiting to be picked up (data)")]
        [SerializeField] private Color beckonColour = new Color(1f, 1f, 1f, 1f);
        [SerializeField, Range(0f, 1f)] private float beckonPulse = 0.3f;
        [SerializeField] private float beckonSeconds = 1.6f;

        private MaterialPropertyBlock _block;
        private float _shimmer;
        private float _beckon;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            if (powerUps == null) return;

            powerUps.Granted += OnGranted;
            powerUps.Expired += OnExpired;
            Refresh();
        }

        private void OnDisable()
        {
            if (powerUps == null) return;

            powerUps.Granted -= OnGranted;
            powerUps.Expired -= OnExpired;
        }

        private void OnGranted(PowerUpKind kind, float seconds, bool stacked)
        {
            Refresh();
        }

        private void OnExpired(PowerUpKind kind)
        {
            Refresh();
        }

        /// A white flash of the glow shells that fades away, as the ghost hand becomes the glove
        public void Shimmer()
        {
            _shimmer = 1f;
            _beckon = 0f;
        }

        /// A soft white glow while the gun waits to be taken, 0 for none
        public void Beckon(float strength)
        {
            if (strength <= 0f && _beckon > 0f && _shimmer <= 0f) { _beckon = 0f; Refresh(); return; }
            _beckon = Mathf.Max(0f, strength);
        }

        private void Update()
        {
            if (_shimmer > 0f)
            {
                _shimmer = Mathf.MoveTowards(_shimmer, 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, shimmerSeconds));
                if (_shimmer <= 0f) { Refresh(); return; }

                Light(Color.white * _shimmer);
                return;
            }

            if (_beckon <= 0f) return;

            float pulse = 1f - beckonPulse * 0.5f * (1f + Mathf.Sin(Time.unscaledTime / Mathf.Max(0.1f, beckonSeconds) * 2f * Mathf.PI));
            Light(beckonColour * (_beckon * pulse));
        }

        // The shells are additive, so scaling the colour toward black fades them out
        private void Light(Color colour)
        {
            if (glowShells == null) return;

            _block.Clear();
            _block.SetColor(ColorId, colour);
            for (int i = 0; i < glowShells.Length; i++)
            {
                if (glowShells[i] == null) continue;

                glowShells[i].enabled = true;
                glowShells[i].SetPropertyBlock(_block);
            }
        }

        /// The colour the gun wears right now, or the fallback when no power up is running
        public Color Current(Color fallback)
        {
            PowerUpKind latest;
            if (powerUps == null || !powerUps.TryLatest(out latest)) return fallback;

            return powerUps.Stacked ? stackedColour : powerUps.Colour(latest);
        }

        private void Refresh()
        {
            PowerUpKind latest;
            bool tinted = powerUps != null && powerUps.TryLatest(out latest);
            Color colour = Current(Color.white);

            for (int i = 0; renderers != null && i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;

                _block.Clear();
                if (tinted) _block.SetColor(ColorId, colour);
                renderers[i].SetPropertyBlock(_block);
            }

            if (glowShells == null) return;

            _block.Clear();
            _block.SetColor(ColorId, colour);
            for (int i = 0; i < glowShells.Length; i++)
            {
                if (glowShells[i] == null) continue;

                glowShells[i].enabled = tinted;
                glowShells[i].SetPropertyBlock(_block);
            }
        }
    }
}
