using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace IntuitiveDesigns.ShootingRange
{
    /// How to play, on the announcer board before the guns come up, through the barker's welcome. Both controllers side on, the trigger and
    /// then the grip pressing and glowing in turn as their labels light, bulbs chasing round the edge,
    /// and one-line hints scrolling along the bottom. Step 24 builds it
    public class ControlsCard : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private RangeGame game;
        [SerializeField] private GameObject content;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform[] triggers;
        [SerializeField] private RectTransform[] grips;
        [SerializeField] private Graphic[] triggerGlows;
        [SerializeField] private Graphic[] gripGlows;
        [SerializeField] private TMP_Text triggerLabel;
        [SerializeField] private TMP_Text gripLabel;
        [SerializeField] private Graphic[] bulbs;
        [SerializeField] private TMP_Text[] hintTexts;

        [Header("Hints (data, each scrolls in from the right in turn)")]
        [SerializeField] private string[] hints =
        {
            "SWAT THE SPIDER AWAY, OR SHOOT IT",
            "SHOOT GLOWING SIGNS FOR POWER-UPS",
            "DODGE THE BATS, OR SHOOT 'EM",
        };
        [SerializeField] private float hintSeconds = 1.6f;
        [SerializeField] private float scrollSeconds = 0.3f;

        [Header("Presses (data: each control gets a turn of two taps)")]
        [SerializeField] private float turnSeconds = 1.25f;
        [SerializeField] private float triggerPullDegrees = 14f;
        [SerializeField] private float gripTravel = 5f;

        [Header("Look (data)")]
        [SerializeField] private float fadeSeconds = 0.3f;
        [SerializeField] private Color labelIdle = new Color(0.96f, 0.92f, 0.83f);
        [SerializeField] private Color labelLit = new Color(1f, 0.85f, 0.47f);
        [SerializeField] private Color bulbOn = new Color(1f, 0.88f, 0.54f);
        [SerializeField] private Color bulbOff = new Color(0.42f, 0.29f, 0.12f);
        [SerializeField] private int bulbSpacing = 3;
        [SerializeField] private float bulbStepsPerSecond = 6f;

        private Quaternion[] _triggerRest;
        private Vector2[] _gripRest;
        private float _shownAt;
        private float _alpha;
        private bool _showing;
        private int _hint = -1;
        private int _bulbStep = -1;

        private void Awake()
        {
            _triggerRest = new Quaternion[triggers.Length];
            for (int i = 0; i < triggers.Length; i++) _triggerRest[i] = triggers[i].localRotation;

            _gripRest = new Vector2[grips.Length];
            for (int i = 0; i < grips.Length; i++) _gripRest[i] = grips[i].anchoredPosition;

            content.SetActive(false);
            group.alpha = 0f;
        }

        private void OnEnable()
        {
            if (game != null) game.StateChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            if (game != null) game.StateChanged -= OnStateChanged;
        }

        /// The game opens on the card, so there may be no change of state to hear about
        private void Start()
        {
            if (game != null) OnStateChanged(game.Current);
        }

        private void OnStateChanged(RangeGame.State state)
        {
            bool show = state == RangeGame.State.HowToPlay;
            if (show == _showing) return;

            _showing = show;
            if (!show) return;

            _shownAt = Time.unscaledTime;
            _hint = -1;
            _bulbStep = -1;
            content.SetActive(true);
        }

        private void Update()
        {
            if (!content.activeSelf) return;

            _alpha = Mathf.MoveTowards(_alpha, _showing ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeSeconds));
            group.alpha = _alpha;
            if (_alpha <= 0f && !_showing) { content.SetActive(false); return; }

            float t = Time.unscaledTime - _shownAt;
            Press(t);
            Chase(t);
            Scroll(t);
        }

        /// The trigger has a turn, then the grip: its label lights, it glows and taps twice
        private void Press(float t)
        {
            int turn = Mathf.FloorToInt(t / turnSeconds);
            float u = t / turnSeconds - turn;
            bool triggerTurn = turn % 2 == 0;
            float tap = Mathf.Max(Bump(u, 0.1f, 0.4f), Bump(u, 0.55f, 0.85f));

            float pull = triggerTurn ? tap : 0f;
            float squeeze = triggerTurn ? 0f : tap;

            for (int i = 0; i < triggers.Length; i++)
                triggers[i].localRotation = _triggerRest[i] * Quaternion.Euler(0f, 0f, -triggerPullDegrees * pull);

            for (int i = 0; i < grips.Length; i++)
                grips[i].anchoredPosition = _gripRest[i] + Vector2.right * gripTravel * squeeze;

            SetAlpha(triggerGlows, triggerTurn ? 0.45f + 0.55f * tap : 0f);
            SetAlpha(gripGlows, triggerTurn ? 0f : 0.45f + 0.55f * tap);

            triggerLabel.color = triggerTurn ? labelLit : labelIdle;
            gripLabel.color = triggerTurn ? labelIdle : labelLit;
        }

        /// Every bulbSpacing-th bulb is lit and the lit ones step round the edge
        private void Chase(float t)
        {
            int step = Mathf.FloorToInt(t * bulbStepsPerSecond);
            if (step == _bulbStep) return;

            _bulbStep = step;
            int spacing = Mathf.Max(2, bulbSpacing);
            for (int i = 0; i < bulbs.Length; i++)
                bulbs[i].color = (i + step) % spacing == 0 ? bulbOn : bulbOff;
        }

        /// One hint at a time. Near the end of its turn it slides out to the left as the next comes in
        /// from the right, so two texts take turns being the one leaving
        private void Scroll(float t)
        {
            if (hints == null || hints.Length == 0 || hintTexts.Length < 2) return;

            int index = Mathf.FloorToInt(t / hintSeconds);
            float into = t - index * hintSeconds;
            var leaving = hintTexts[index % 2];
            var coming = hintTexts[(index + 1) % 2];

            if (index != _hint)
            {
                _hint = index;
                leaving.text = hints[index % hints.Length];
                coming.text = hints[(index + 1) % hints.Length];
            }

            float width = ((RectTransform)leaving.transform.parent).rect.width;
            float slide = Mathf.Clamp01((into - (hintSeconds - scrollSeconds)) / Mathf.Max(0.01f, scrollSeconds));
            slide = slide * slide * (3f - 2f * slide);

            leaving.rectTransform.anchoredPosition = new Vector2(-width * slide, 0f);
            coming.rectTransform.anchoredPosition = new Vector2(width * (1f - slide), 0f);
        }

        private static float Bump(float u, float from, float to)
        {
            return u <= from || u >= to ? 0f : Mathf.Sin(Mathf.PI * (u - from) / (to - from));
        }

        private static void SetAlpha(Graphic[] graphics, float alpha)
        {
            for (int i = 0; i < graphics.Length; i++)
            {
                var colour = graphics[i].color;
                colour.a = alpha;
                graphics[i].color = colour;
            }
        }
    }
}
