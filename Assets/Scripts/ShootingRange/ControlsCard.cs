using UnityEngine;
using UnityEngine.UI;

namespace IntuitiveDesigns.ShootingRange
{
    public class ControlsCard : MonoBehaviour, IShootable
    {
        [Header("Refs")]
        [SerializeField] private RangeGame game;
        [SerializeField] private HangingSign sign;
        [SerializeField] private OpeningTitle opening;
        [SerializeField] private Graphic[] bulbs;
        [SerializeField] private RectTransform bullseye;
        [SerializeField] private Graphic bullseyeGlow;

        [Header("The controller: at rest, half and fully pressed")]
        [SerializeField] private RawImage controller;
        [SerializeField] private RawImage buttonLight;
        [SerializeField] private Texture2D[] triggerPoses;
        [SerializeField] private Texture2D[] triggerLights;
        [SerializeField] private Graphic triggerPlateGlow;
        [SerializeField] private Texture2D[] gripPoses;
        [SerializeField] private Texture2D[] gripLights;
        [SerializeField] private Graphic gripPlateGlow;

        [Header("Look (data)")]
        [SerializeField] private float pressSeconds = 0.45f;
        [SerializeField] private float pressGapSeconds = 0.15f;
        [SerializeField] private int pressesPerTurn = 2;
        [SerializeField] private float turnRestSeconds = 0.6f;
        [SerializeField] private Color bulbOn = new Color(1f, 0.88f, 0.54f);
        [SerializeField] private Color bulbOff = new Color(0.42f, 0.29f, 0.12f);
        [SerializeField] private int bulbSpacing = 3;
        [SerializeField] private float bulbStepsPerSecond = 6f;
        [SerializeField] private float bullseyePulseScale = 0.06f;
        [SerializeField] private float bullseyePulsesPerSecond = 1.5f;
        [SerializeField] private float hitPunch = 0.25f;

        [Header("Audio")]
        [SerializeField] private AudioClip shotClip;
        [SerializeField, Range(0f, 1f)] private float volume = 0.8f;

        private Collider _hitBox;
        private float _shownAt;
        private float _shotAt = -1f;
        private bool _showing;
        private int _bulbStep = -1;

        private void Awake()
        {
            // It hangs in front of the range, so while it's away it must not catch the rounds meant for the signs
            _hitBox = GetComponent<Collider>();
            if (_hitBox != null) _hitBox.enabled = false;
        }

        private void OnEnable()
        {
            if (opening != null) opening.Leaving += Refresh;
            if (game == null) return;
            game.StateChanged += OnStateChanged;
            game.ReadyToStart += Refresh;
        }

        private void OnDisable()
        {
            if (opening != null) opening.Leaving -= Refresh;
            if (game == null) return;
            game.StateChanged -= OnStateChanged;
            game.ReadyToStart -= Refresh;
        }

        private void Start() { Refresh(); }

        private void OnStateChanged(RangeGame.State state) { Refresh(); }

        /// Down as the logo goes up (or once the triggers work, with no opening), away once the game begins
        private void Refresh()
        {
            bool cue = opening != null ? opening.LogoLeaving : game != null && game.TriggersLive;
            bool show = game != null && game.Current == RangeGame.State.HowToPlay && cue;
            if (show == _showing) return;

            _showing = show;
            if (_hitBox != null) _hitBox.enabled = show;
            if (!show)
            {
                sign.Raise();
                return;
            }

            _shownAt = Time.unscaledTime;
            _shotAt = -1f;
            _bulbStep = -1;
            sign.Drop();
        }

        public void OnShot(Vector3 point, Vector3 direction, float impulse)
        {
            if (!_showing || !sign.IsDown || !game.TriggersLive || _shotAt >= 0f) return;

            _shotAt = Time.unscaledTime;
            if (shotClip != null && ImpactFX.Instance != null) ImpactFX.Instance.PlayClip(shotClip, point, volume);
            game.Begin();
        }

        private void Update()
        {
            if (sign.IsUp) return;

            float t = Time.unscaledTime - _shownAt;
            Chase(t);
            Demonstrate(t);
            PulseTheBullseye();
        }

        /// The trigger's turn, then the grip's: the button lit and pressed pressesPerTurn times, its label glowing
        private void Demonstrate(float t)
        {
            float press = pressSeconds + pressGapSeconds;
            float turn = pressesPerTurn * press + turnRestSeconds;
            float into = Mathf.Repeat(t, 2f * turn);
            bool trigger = into < turn;
            float local = trigger ? into : into - turn;
            int pressing = Mathf.FloorToInt(local / press);
            float through = (local - pressing * press) / Mathf.Max(0.01f, pressSeconds);
            float depth = pressing < pressesPerTurn && through < 1f ? Mathf.Sin(through * Mathf.PI) : 0f;

            var poses = trigger ? triggerPoses : gripPoses;
            var lights = trigger ? triggerLights : gripLights;
            int frame = Mathf.Min(Mathf.RoundToInt(depth * (poses.Length - 1)), poses.Length - 1);
            controller.texture = poses[frame];
            buttonLight.texture = lights[frame];
            SetAlpha(triggerPlateGlow, trigger ? 1f : 0f);
            SetAlpha(gripPlateGlow, trigger ? 0f : 1f);
        }

        /// Pulsing to be shot; a hit makes it jump
        private void PulseTheBullseye()
        {
            float now = Time.unscaledTime;
            float pulse = Mathf.Sin(now * bullseyePulsesPerSecond * 2f * Mathf.PI);
            SetAlpha(bullseyeGlow, 0.6f + 0.4f * pulse);
            if (bullseye == null) return;

            float scale = _shotAt >= 0f ? 1f + hitPunch * Mathf.Exp(-(now - _shotAt) * 8f) : 1f + bullseyePulseScale * pulse;
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

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            if (graphic == null) return;
            var colour = graphic.color;
            colour.a = alpha;
            graphic.color = colour;
        }
    }
}
