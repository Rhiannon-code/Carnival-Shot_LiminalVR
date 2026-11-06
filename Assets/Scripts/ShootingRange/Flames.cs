using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    /// Fire on whatever is burning. One particle system in world space that any number of burning signs emit into, so
    /// they share one draw and it needs no pool. Particles, not a light: no realtime lights on Quest (ADR 0064)
    public class Flames : MonoBehaviour
    {
        public static Flames Instance { get; private set; }

        [SerializeField] private ParticleSystem system;
        [SerializeField] private int perLick = 1;
        [SerializeField, Tooltip("Each flame's size, as a share of the burning thing's width")] private float cover = 1.3f;
        [SerializeField, Tooltip("How far from the middle a flame starts, as a share of the half-width")] private float spread = 0.3f;

        private void Awake() { Instance = this; }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// More flames on area, each nearly as wide as it, low on its middle, so they rise and merge into one blaze
        /// around it rather than a scatter of small fires
        public void Lick(Bounds area)
        {
            if (system == null) return;

            float width = 2f * Mathf.Max(area.extents.x, area.extents.z);
            for (int i = 0; i < perLick; i++)
            {
                var offset = Vector3.Scale(area.extents, new Vector3(Random.Range(-spread, spread), Random.Range(-0.4f, 0f), Random.Range(-spread, spread)));
                system.Emit(new ParticleSystem.EmitParams
                {
                    position = area.center + offset,
                    applyShapeToPosition = true,
                    startSize = width * cover * Random.Range(0.85f, 1.15f),
                }, 1);
            }
        }
    }
}
