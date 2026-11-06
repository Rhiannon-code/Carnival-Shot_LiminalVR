using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    /// The red from under a floor rail, fading up the sign that rides it. A sign's baked light is one probe reading,
    /// which can't change from foot to head, so this is drawn over it as a second material (ADR 0064)
    public class SignUnderglow : MonoBehaviour
    {
        [SerializeField] private Material material;
        [SerializeField, Range(0f, 1f), Tooltip("How far up the sign the red reaches, as a share of its height")]
        private float reach = 0.5f;

        private static readonly int AmountId = Shader.PropertyToID("_Amount");
        private static readonly int FootId = Shader.PropertyToID("_FootY");
        private static readonly int FadeId = Shader.PropertyToID("_FadeHeight");

        private MeshRenderer[] _renderers;
        private MaterialPropertyBlock _block;
        private MaterialPropertyBlock _folding;
        private Transform _anchor;
        private bool _on;
        private float _upright = -1f;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _renderers = GetComponentsInChildren<MeshRenderer>(true);
            _folding = new MaterialPropertyBlock();

            // Probes are read at the middle of a renderer's bounds by default, and a folding sign swings that through very
            // different light, so its colour jumps. One point that only rides along the rail stops that
            _anchor = new GameObject("Probe Anchor").transform;
            _anchor.SetParent(transform, false);
            foreach (var renderer in _renderers) renderer.probeAnchor = _anchor;

            if (material == null) return;

            foreach (var renderer in _renderers)
            {
                var materials = renderer.sharedMaterials;
                var withGlow = new Material[materials.Length + 1];
                materials.CopyTo(withGlow, 0);
                withGlow[materials.Length] = material;
                renderer.sharedMaterials = withGlow;
            }
        }

        /// on is false for the rails with no red under them. Metres: footY in the world, the rest as lengths.
        /// standingMiddle is where the sign's middle will be once it is up, so it reads the light it will stand in
        public void Show(bool on, float footY, float standLength, float signHeight, Vector3 standingMiddle)
        {
            _on = on;
            _upright = -1f;
            _anchor.position = standingMiddle;
            _block.SetFloat(AmountId, on ? 1f : 0f);
            _block.SetFloat(FootId, footY);
            _block.SetFloat(FadeId, standLength + signHeight * reach);

            foreach (var renderer in _renderers) renderer.SetPropertyBlock(_block);
        }

        /// 1 standing, 0 folded. The fade is by height in the world, so a sign folding down would sweep through it all at
        /// once and flash red. So it only shows as the sign stands. Keeps what else is in the block, like a bullet hole
        public void Upright(float share)
        {
            if (!_on || Mathf.Abs(share - _upright) < 0.02f) return;

            _upright = share;
            foreach (var renderer in _renderers)
            {
                renderer.GetPropertyBlock(_folding);
                _folding.SetFloat(AmountId, share);
                renderer.SetPropertyBlock(_folding);
            }
        }
    }
}
