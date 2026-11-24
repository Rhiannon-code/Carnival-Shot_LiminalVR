using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    public class FogClearings : MonoBehaviour
    {
        private const int Most = 6;
        private static readonly int HolesId = Shader.PropertyToID("_PropHoles");

        [SerializeField] private Transform[] props;
        [SerializeField] private float margin = 0.12f;

        private Renderer[][] _looks;
        private readonly Vector4[] _holes = new Vector4[Most];

        private void Awake()
        {
            int count = Mathf.Min(props.Length, Most);
            _looks = new Renderer[count][];
            for (int i = 0; i < count; i++)
                _looks[i] = props[i] != null ? props[i].GetComponentsInChildren<MeshRenderer>() : new Renderer[0];
        }

        private void LateUpdate()
        {
            for (int i = 0; i < Most; i++) _holes[i] = Vector4.zero;
            for (int i = 0; i < _looks.Length; i++)
            {
                if (_looks[i].Length == 0 || !props[i].gameObject.activeInHierarchy) continue;

                var box = _looks[i][0].bounds;
                foreach (var look in _looks[i]) box.Encapsulate(look.bounds);
                float radius = Mathf.Max(box.extents.x, box.extents.z) + margin;
                _holes[i] = new Vector4(box.center.x, box.center.z, radius, margin);
            }
            Shader.SetGlobalVectorArray(HolesId, _holes);
        }

        private void OnDisable()
        {
            for (int i = 0; i < Most; i++) _holes[i] = Vector4.zero;
            Shader.SetGlobalVectorArray(HolesId, _holes);
        }
    }
}
