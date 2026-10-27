using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    /// A row of bulbs along a rail that lights up in a run toward the launch point as a sign is about to
    /// come up there. One mesh and one draw call for the whole row; the Rail Bulbs shader does the lighting
    [RequireComponent(typeof(MeshRenderer))]
    public class RailBulbs : MonoBehaviour
    {
        [Header("Feel (data, seconds)")]
        [SerializeField, Tooltip("How long the lit spot holds on the launch point once it gets there")]
        private float holdSeconds = 0.3f;

        private static readonly int ChaseId = Shader.PropertyToID("_Chase");

        private MeshRenderer _renderer;
        private MaterialPropertyBlock _block;
        private float _started = -1f;
        private float _runSeconds;

        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            _block = new MaterialPropertyBlock();
            Set(-1f);
        }

        /// The lit spot runs from the far end to the launch point over seconds
        public void Chase(float seconds)
        {
            _runSeconds = Mathf.Max(0.05f, seconds);
            _started = Time.time;
        }

        private void Update()
        {
            if (_started < 0f) return;

            float age = Time.time - _started;
            if (age >= _runSeconds + holdSeconds)
            {
                _started = -1f;
                Set(-1f);
                return;
            }

            Set(1f - Mathf.Clamp01(age / _runSeconds));
        }

        private void Set(float chase)
        {
            _renderer.GetPropertyBlock(_block);
            _block.SetFloat(ChaseId, chase);
            _renderer.SetPropertyBlock(_block);
        }
    }
}
