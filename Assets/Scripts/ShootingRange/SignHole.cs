using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    /// A bullet hole cut through the sign that was just hit, so the room shows through it. The sign's shader clips it, from
    /// where this says the hole is in each mesh's own space, so it rides along through the reaction (ADR 0064)
    public class SignHole : MonoBehaviour
    {
        [Header("Mask size (data, metres): the see-through part is about half of it")]
        [SerializeField] private float minSize = 0.18f;
        [SerializeField] private float maxSize = 0.24f;

        private static readonly int HoleUId = Shader.PropertyToID("_HoleU");
        private static readonly int HoleVId = Shader.PropertyToID("_HoleV");

        private MaterialPropertyBlock _block;
        private MeshRenderer[] _cut;

        public void Show(Transform sign, Vector3 point, Vector3 normal)
        {
            if (sign == null) return;
            if (_block == null) _block = new MaterialPropertyBlock();

            float size = Random.Range(minSize, maxSize);
            var turn = Quaternion.AngleAxis(Random.Range(0f, 360f), normal);
            Vector3 across = turn * Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.forward).normalized;
            Vector3 up = Vector3.Cross(normal, across);

            _cut = sign.GetComponentsInChildren<MeshRenderer>();
            foreach (var renderer in _cut)
            {
                var toWorld = renderer.transform.localToWorldMatrix;
                Set(renderer, Axis(toWorld, across, point, size), Axis(toWorld, up, point, size));
            }
        }

        public void Clear()
        {
            if (_cut == null) return;

            foreach (var renderer in _cut)
            {
                if (renderer != null) Set(renderer, Vector4.zero, Vector4.zero);
            }

            _cut = null;
        }

        /// For a point p in the mesh's own space, dot(p, xyz) + w is how far along axis it is across the mask, 0 to 1
        private static Vector4 Axis(Matrix4x4 toWorld, Vector3 axis, Vector3 point, float size)
        {
            Vector3 inMesh = toWorld.transpose.MultiplyVector(axis) / size;
            Vector3 origin = toWorld.GetColumn(3);
            return new Vector4(inMesh.x, inMesh.y, inMesh.z, Vector3.Dot(origin - point, axis) / size + 0.5f);
        }

        /// Keeps what else is in the block: SignUnderglow writes to the same renderers
        private void Set(Renderer renderer, Vector4 u, Vector4 v)
        {
            renderer.GetPropertyBlock(_block);
            _block.SetVector(HoleUId, u);
            _block.SetVector(HoleVId, v);
            renderer.SetPropertyBlock(_block);
        }
    }
}
