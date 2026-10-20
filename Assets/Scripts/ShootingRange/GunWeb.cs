using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    [RequireComponent(typeof(Pistol))]
    public class GunWeb : MonoBehaviour
    {
        [SerializeField] private RangeGame game;
        [SerializeField] private Material silk;

        [Header("Strands (data, metres)")]
        [SerializeField] private int wraps = 4;
        [SerializeField] private float width = 0.0025f;
        [SerializeField] private float stretch = 0.012f;
        [SerializeField] private float fadeSeconds = 0.3f;

        private Pistol _pistol;
        private LineRenderer _line;
        private float _until;
        private float _length;

        private void Awake()
        {
            _pistol = GetComponent<Pistol>();
            if (game == null) game = FindObjectOfType<RangeGame>();
        }

        private void OnEnable()
        {
            if (game != null) game.PlayerBitten += OnBitten;
        }

        private void OnDisable()
        {
            if (game != null) game.PlayerBitten -= OnBitten;
            if (_line != null) _line.enabled = false;
        }

        private void OnBitten(Vector3 from, float seconds)
        {
            _pistol.LockTrigger(seconds);

            if (silk == null) return;
            if (_line == null) _line = MakeLine();

            Wrap();
            _line.widthMultiplier = width;
            _length = seconds;
            _until = Time.unscaledTime + seconds;
            _line.enabled = true;
        }

        /// Shrinks to nothing over the last moment, so the gun is visibly free the instant it can fire
        private void Update()
        {
            if (_line == null || !_line.enabled) return;

            float left = _until - Time.unscaledTime;
            if (left <= 0f) { _line.enabled = false; return; }

            _line.widthMultiplier = width * Mathf.Clamp01(left / Mathf.Max(0.01f, Mathf.Min(fadeSeconds, _length)));
        }

        /// Riding the posed model, so it kicks and tilts with the gun
        private LineRenderer MakeLine()
        {
            var holder = new GameObject("Web");
            holder.transform.SetParent(_pistol.Visual, false);

            var line = holder.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.sharedMaterial = silk;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            return line;
        }

        /// One strand wound round the gun along its longest side and back again the other way round, a
        /// little proud of it, so the two windings cross like silk. Freshly tangled on every bite
        private void Wrap()
        {
            const int perTurn = 8;
            var box = LocalBounds(_line.transform);
            var size = box.extents;
            int length = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
            int across = (length + 1) % 3;
            int up = (length + 2) % 3;

            int half = wraps * perTurn;
            var points = new Vector3[half * 2 + 1];
            float angle = Random.value * 2f * Mathf.PI;

            for (int i = 0; i < points.Length; i++)
            {
                bool back = i > half;
                float along = back ? 1f - (float)(i - half) / half : (float)i / half;
                angle += (back ? -1f : 1f) * 2f * Mathf.PI / perTurn;
                float loose = 1f + Random.Range(0f, 0.25f);

                var offset = Vector3.zero;
                offset[length] = (along * 2f - 1f) * size[length];
                offset[across] = Mathf.Cos(angle) * (size[across] + stretch) * loose;
                offset[up] = Mathf.Sin(angle) * (size[up] + stretch) * loose;
                points[i] = box.center + offset;
            }

            _line.positionCount = points.Length;
            _line.SetPositions(points);
        }

        private Bounds LocalBounds(Transform space)
        {
            bool any = false;
            var box = new Bounds();
            foreach (var filter in _pistol.Visual.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;

                var mesh = filter.sharedMesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    var point = space.InverseTransformPoint(filter.transform.TransformPoint(new Vector3(
                        (corner & 1) == 0 ? mesh.min.x : mesh.max.x,
                        (corner & 2) == 0 ? mesh.min.y : mesh.max.y,
                        (corner & 4) == 0 ? mesh.min.z : mesh.max.z)));

                    if (!any) { box = new Bounds(point, Vector3.zero); any = true; }
                    else box.Encapsulate(point);
                }
            }

            return box;
        }
    }
}
