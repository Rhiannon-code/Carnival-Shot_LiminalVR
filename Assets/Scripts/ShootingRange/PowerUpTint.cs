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
        private MaterialPropertyBlock _block;

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
            bool tinted = powerUps.TryLatest(out latest);
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
