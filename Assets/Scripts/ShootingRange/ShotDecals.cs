using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    /// Bullet holes left on the room. Every mark is a quad in one shared mesh, one draw call however
    /// many there are. Past maxMarks the oldest fades out, and they all fade when a round ends so the
    /// next starts clean. The art is a 2 x 2 sheet of holes (tools/shot-decal-art.py), step 26 wires it
    public class ShotDecals : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private RangeGame game;
        [SerializeField] private Material material;

        [Header("Marks (data, metres and seconds)")]
        [SerializeField] private int maxMarks = 30;
        [SerializeField] private float minSize = 0.08f;
        [SerializeField] private float maxSize = 0.12f;
        [SerializeField] private float surfaceGap = 0.002f;
        [SerializeField] private float fadeSeconds = 0.4f;

        // A few more slots than marks, so the ones fading out are not taken over mid-fade
        private const int FadingSpare = 6;
        private const int SheetCells = 2;

        private Mesh _mesh;
        private Vector3[] _corners;
        private Vector2[] _uvs;
        private Color32[] _colours;
        private float[] _fadeFrom;
        private int _next;
        private int _shown;
        private bool _fading;

        private void Awake()
        {
            int slots = Mathf.Max(1, maxMarks) + FadingSpare;
            _corners = new Vector3[slots * 4];
            _uvs = new Vector2[slots * 4];
            _colours = new Color32[slots * 4];
            _fadeFrom = new float[slots];

            var triangles = new int[slots * 6];
            for (int i = 0; i < slots; i++)
            {
                _fadeFrom[i] = -1f;
                triangles[i * 6] = i * 4;
                triangles[i * 6 + 1] = i * 4 + 1;
                triangles[i * 6 + 2] = i * 4 + 2;
                triangles[i * 6 + 3] = i * 4;
                triangles[i * 6 + 4] = i * 4 + 2;
                triangles[i * 6 + 5] = i * 4 + 3;
            }

            _mesh = new Mesh { name = "Shot Decals" };
            _mesh.MarkDynamic();
            _mesh.vertices = _corners;
            _mesh.uv = _uvs;
            _mesh.colors32 = _colours;
            _mesh.triangles = triangles;

            // The quads go anywhere in the room, and the mesh's own bounds would cull them
            _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);

            var holder = new GameObject("Shot Decal Mesh");
            holder.transform.SetParent(transform, false);
            holder.transform.position = Vector3.zero;
            holder.transform.rotation = Quaternion.identity;
            holder.AddComponent<MeshFilter>().sharedMesh = _mesh;

            var renderer = holder.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }

        private void OnEnable()
        {
            if (game != null) game.RoundEnded += OnRoundEnded;
        }

        private void OnDisable()
        {
            if (game != null) game.RoundEnded -= OnRoundEnded;
        }

        private void OnRoundEnded(int round, int roundScore)
        {
            for (int i = 0; i < _fadeFrom.Length; i++)
            {
                if (_colours[i * 4].a > 0 && _fadeFrom[i] < 0f) FadeOut(i);
            }
        }

        /// A hole where a shot hit the room, flat on the surface, a random one of the four at a random
        /// turn and size
        public void Place(Vector3 point, Vector3 normal)
        {
            if (normal.sqrMagnitude < 1e-6f) return;
            normal.Normalize();

            int slot = _next;
            _next = (_next + 1) % _fadeFrom.Length;

            // Normally the slot taken is long faded; one still fully shown stops counting as it goes
            if (_colours[slot * 4].a > 0 && _fadeFrom[slot] < 0f) _shown--;
            if (++_shown > maxMarks) FadeOldest(slot);

            var turn = Quaternion.AngleAxis(Random.Range(0f, 360f), normal);
            Vector3 across = turn * Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.forward).normalized;
            Vector3 up = Vector3.Cross(across, normal);
            float half = Random.Range(minSize, maxSize) * 0.5f;
            Vector3 centre = point + normal * surfaceGap;

            int v = slot * 4;
            _corners[v] = centre - across * half - up * half;
            _corners[v + 1] = centre - across * half + up * half;
            _corners[v + 2] = centre + across * half + up * half;
            _corners[v + 3] = centre + across * half - up * half;

            int cell = Random.Range(0, SheetCells * SheetCells);
            float size = 1f / SheetCells;
            var low = new Vector2(cell % SheetCells, cell / SheetCells) * size;
            _uvs[v] = low;
            _uvs[v + 1] = low + new Vector2(0f, size);
            _uvs[v + 2] = low + new Vector2(size, size);
            _uvs[v + 3] = low + new Vector2(size, 0f);

            _fadeFrom[slot] = -1f;
            SetAlpha(slot, 255);

            _mesh.vertices = _corners;
            _mesh.uv = _uvs;
            _mesh.colors32 = _colours;
        }

        /// The mark longest on the wall that is not already going, counting back round the ring from
        /// the one just taken
        private void FadeOldest(int taken)
        {
            for (int step = 1; step < _fadeFrom.Length; step++)
            {
                int slot = (taken + step) % _fadeFrom.Length;
                if (_colours[slot * 4].a > 0 && _fadeFrom[slot] < 0f) { FadeOut(slot); return; }
            }
        }

        private void FadeOut(int slot)
        {
            _fadeFrom[slot] = Time.time;
            _shown--;
            _fading = true;
        }

        private void Update()
        {
            if (!_fading) return;

            _fading = false;
            for (int i = 0; i < _fadeFrom.Length; i++)
            {
                if (_fadeFrom[i] < 0f) continue;

                float t = (Time.time - _fadeFrom[i]) / Mathf.Max(0.01f, fadeSeconds);
                if (t >= 1f) { _fadeFrom[i] = -1f; SetAlpha(i, 0); continue; }

                SetAlpha(i, (byte)(255 * (1f - t)));
                _fading = true;
            }

            _mesh.colors32 = _colours;
        }

        private void SetAlpha(int slot, byte alpha)
        {
            var colour = new Color32(255, 255, 255, alpha);
            for (int corner = 0; corner < 4; corner++) _colours[slot * 4 + corner] = colour;
        }
    }
}
