using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    /// A round landing low pushes a clear ring through the ground fog, which fills back in. The shader only ever
    /// knows the two latest, as global vectors, so it stays cheap at any rate of fire (ADR 0064)
    public class FogStir : MonoBehaviour
    {
        public static FogStir Instance { get; private set; }

        [Header("The ring (data, metres and seconds)")]
        [SerializeField] private float startRadius = 0.2f;
        [SerializeField] private float endRadius = 1.2f;
        [SerializeField] private float spreadSeconds = 0.6f;
        [SerializeField] private float settleSeconds = 2.5f;
        [SerializeField, Tooltip("Metres above the floor a hit can be and still stir it")] private float reach = 0.6f;

        private static readonly int[] StirIds = { Shader.PropertyToID("_FogStir0"), Shader.PropertyToID("_FogStir1") };

        private readonly Vector2[] _at = new Vector2[2];
        private readonly float[] _startedAt = { -100f, -100f };
        private int _next;

        private void Awake() { Instance = this; }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            foreach (var id in StirIds) Shader.SetGlobalVector(id, Vector4.zero);
        }

        public void Stir(Vector3 point)
        {
            if (point.y > reach) return;

            _at[_next] = new Vector2(point.x, point.z);
            _startedAt[_next] = Time.time;
            _next = (_next + 1) % _at.Length;
        }

        private void Update()
        {
            for (int i = 0; i < _at.Length; i++)
            {
                float age = Time.time - _startedAt[i];
                float radius = Mathf.Lerp(startRadius, endRadius, Mathf.SmoothStep(0f, 1f, age / Mathf.Max(0.01f, spreadSeconds)));
                float strength = 1f - Mathf.Clamp01(age / Mathf.Max(0.01f, settleSeconds));
                Shader.SetGlobalVector(StirIds[i], new Vector4(_at[i].x, _at[i].y, radius, strength));
            }
        }
    }
}
