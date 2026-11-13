using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace IntuitiveDesigns.ShootingRange
{
    /// How to play, painted as a carnival sign on the announcer board (user, 2026-11-10). Everything is on it at once: the
    /// controller with its trigger and grip glowing, the three hints, and a bullseye. The bullseye wakes once the carnival
    /// has wound up, and shooting it starts the game. Step 24 builds it
    public class ControlsCard : MonoBehaviour, IShootable
    {
        [Header("Refs")]
        [SerializeField] private RangeGame game;
        [SerializeField] private GameObject content;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Graphic[] buttonGlows;
        [SerializeField] private Graphic[] bulbs;
        [SerializeField] private RectTransform bullseye;
        [SerializeField] private Graphic bullseyeGlow;
        [SerializeField] private TMP_Text startLabel;

        [Header("Look (data)")]
        [SerializeField] private float fadeSeconds = 0.3f;
        [SerializeField] private float glowPulsesPerSecond = 0.8f;
        [SerializeField] private Color bulbOn = new Color(1f, 0.88f, 0.54f);
        [SerializeField] private Color bulbOff = new Color(0.42f, 0.29f, 0.12f);
        [SerializeField] private int bulbSpacing = 3;
        [SerializeField] private float bulbStepsPerSecond = 6f;
        [SerializeField, Range(0f, 1f), Tooltip("How much of the start label shows before the triggers are live")]
        private float startLabelAsleep = 0.35f;
        [SerializeField] private float bullseyePulseScale = 0.06f;
        [SerializeField] private float bullseyePulsesPerSecond = 1.5f;
        [SerializeField, Tooltip("How far the bullseye jumps when it's shot, as a share of its size")] private float hitPunch = 0.25f;

        [Header("Audio")]
        [SerializeField] private AudioClip shotClip;
        [SerializeField, Range(0f, 1f)] private float volume = 0.8f;

        private Collider _hitBox;
        private float _shownAt;
        private float _alpha;
        private float _shotAt = -1f;
        private bool _showing;
        private int _bulbStep = -1;

        private void Awake()
        {
            content.SetActive(false);
            group.alpha = 0f;

            // It hangs in front of the range, so once it's gone it must not catch the rounds meant for the signs
            _hitBox = GetComponent<Collider>();
            if (_hitBox != null) _hitBox.enabled = false;
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

        public void OnShot(Vector3 point, Vector3 direction, float impulse)
        {
            if (!_showing || game == null || !game.TriggersLive || _shotAt >= 0f) return;

            _shotAt = Time.unscaledTime;
            if (shotClip != null && ImpactFX.Instance != null) ImpactFX.Instance.PlayClip(shotClip, point, volume);
            game.Begin();
        }

        private void OnStateChanged(RangeGame.State state)
        {
            bool show = state == RangeGame.State.HowToPlay;
            if (show == _showing) return;

            _showing = show;
            if (_hitBox != null) _hitBox.enabled = show;
            if (!show) return;

            _shownAt = Time.unscaledTime;
            _shotAt = -1f;
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
            Chase(t);
            SetAlpha(buttonGlows, 0.75f + 0.25f * Mathf.Sin(t * glowPulsesPerSecond * 2f * Mathf.PI));
            WakeTheBullseye();
        }

        /// Asleep through the wind-up, then pulsing to be shot; a hit makes it jump
        private void WakeTheBullseye()
        {
            bool live = game != null && game.TriggersLive;
            float now = Time.unscaledTime;
            float pulse = Mathf.Sin(now * bullseyePulsesPerSecond * 2f * Mathf.PI);

            if (startLabel != null)
            {
                var colour = startLabel.color;
                colour.a = live ? 1f : startLabelAsleep;
                startLabel.color = colour;
            }

            SetAlpha(bullseyeGlow, live ? 0.6f + 0.4f * pulse : 0f);
            if (bullseye == null) return;

            float scale = 1f;
            if (_shotAt >= 0f) scale += hitPunch * Mathf.Exp(-(now - _shotAt) * 8f);
            else if (live) scale += bullseyePulseScale * pulse;
            bullseye.localScale = Vector3.one * scale;
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

        private static void SetAlpha(Graphic[] graphics, float alpha)
        {
            for (int i = 0; i < graphics.Length; i++) SetAlpha(graphics[i], alpha);
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            if (graphic == null) return;
            var colour = graphic.color;
            colour.a = alpha;
            graphic.color = colour;
        }
    }
}
