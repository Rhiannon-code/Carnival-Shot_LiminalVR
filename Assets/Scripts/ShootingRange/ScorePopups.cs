using TMPro;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    /// "+150" off each sign that is shot, with the combo in it: it rises, faces the player and fades. A small pool,
    /// so a burst of hits reuses the oldest
    public class ScorePopups : MonoBehaviour
    {
        [SerializeField] private RangeGame game;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private int poolSize = 8;

        [Header("Look (data)")]
        [SerializeField, Tooltip("About 0.1 m of letter height per 1")] private float fontSize = 1.5f;
        [SerializeField] private Color colour = new Color(1f, 0.85f, 0.47f);
        [SerializeField] private float riseMetres = 0.35f;
        [SerializeField] private float seconds = 0.8f;
        [SerializeField, Tooltip("Seconds it takes to grow to full size")] private float popSeconds = 0.12f;
        [SerializeField, Tooltip("Metres toward the player it starts, so the sign doesn't cover it")] private float lead = 0.2f;

        private TextMeshPro[] _texts;
        private Vector3[] _from;
        private float[] _born;
        private int _next;

        private void Awake()
        {
            int count = Mathf.Max(1, poolSize);
            _texts = new TextMeshPro[count];
            _from = new Vector3[count];
            _born = new float[count];

            for (int i = 0; i < count; i++)
            {
                var text = new GameObject("Score " + i, typeof(RectTransform)).AddComponent<TextMeshPro>();
                text.transform.SetParent(transform, false);
                if (font != null) { text.font = font; text.fontSharedMaterial = font.material; }
                text.fontSize = fontSize;
                text.alignment = TextAlignmentOptions.Center;
                text.enableWordWrapping = false;
                text.gameObject.SetActive(false);
                _texts[i] = text;
            }
        }

        private void OnEnable()
        {
            if (game != null) game.SignScored += Show;
        }

        private void OnDisable()
        {
            if (game != null) game.SignScored -= Show;
        }

        private void Show(int points, Vector3 where)
        {
            var head = PopUpTarget.Head();
            Vector3 toward = head != null ? (head.position - where).normalized : Vector3.zero;

            int i = _next;
            _next = (_next + 1) % _texts.Length;

            _texts[i].text = "+" + points;
            _from[i] = where + toward * lead;
            _born[i] = Time.unscaledTime;
            _texts[i].gameObject.SetActive(true);
        }

        private void Update()
        {
            var head = PopUpTarget.Head();
            float now = Time.unscaledTime;

            for (int i = 0; i < _texts.Length; i++)
            {
                var text = _texts[i];
                if (!text.gameObject.activeSelf) continue;

                float t = (now - _born[i]) / Mathf.Max(0.01f, seconds);
                if (t >= 1f) { text.gameObject.SetActive(false); continue; }

                var position = _from[i] + Vector3.up * riseMetres * (1f - (1f - t) * (1f - t));
                text.transform.position = position;
                if (head != null) text.transform.rotation = Quaternion.LookRotation(position - head.position);

                float grow = Mathf.Clamp01((now - _born[i]) / Mathf.Max(0.01f, popSeconds));
                text.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1f, grow);

                var faded = colour;
                faded.a = colour.a * (1f - t * t);
                text.color = faded;
            }
        }
    }
}
