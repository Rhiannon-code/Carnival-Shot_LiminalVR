using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    public class SignScaredFace : MonoBehaviour
    {
        [SerializeField] private Texture[] faces;
        [SerializeField] private Texture[] scared;

        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        private MaterialPropertyBlock _block;

        /// Keeps what else is in the block: the hole and the underglow write to the same renderers
        public void Show(Transform sign)
        {
            if (sign == null || faces == null || scared == null) return;
            if (_block == null) _block = new MaterialPropertyBlock();

            foreach (var renderer in sign.GetComponentsInChildren<MeshRenderer>())
            {
                var material = renderer.sharedMaterial;
                int index = material != null ? System.Array.IndexOf(faces, material.mainTexture) : -1;
                if (index < 0 || index >= scared.Length || scared[index] == null) continue;

                renderer.GetPropertyBlock(_block);
                _block.SetTexture(MainTexId, scared[index]);
                renderer.SetPropertyBlock(_block);
            }
        }
    }
}
