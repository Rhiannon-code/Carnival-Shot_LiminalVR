using System.Collections;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    public class CarnivalLights : MonoBehaviour
    {
        [SerializeField] private RangeGame game;
        [SerializeField] private Renderer[] bulbs;

        [Header("The two bakes (filled by setup step 39)")]
        [SerializeField] private Texture2D[] litColour;
        [SerializeField] private Texture2D[] litDirection;
        [SerializeField] private LightProbes litProbes;
        [SerializeField] private Texture2D[] darkColour;
        [SerializeField] private Texture2D[] darkDirection;
        [SerializeField] private LightProbes darkProbes;

        [Header("Flicker (data, seconds): each step swaps between on and off, and it ends where it was headed")]
        [SerializeField] private float[] comingOn = { 0.12f, 0.2f, 0.08f, 0.35f, 0.1f, 0.15f };
        [SerializeField] private float[] goingOut = { 0.25f, 0.1f, 0.15f, 0.08f, 0.4f, 0.06f };
        [SerializeField, Range(0f, 1f)] private float darkGlow = 0.04f;

        [Header("Audio")]
        [SerializeField] private AudioClip[] clunkClips;
        [SerializeField] private AudioClip[] buzzClips;
        [SerializeField] private AudioClip[] popClips;
        [SerializeField, Range(0f, 1f)] private float volume = 0.8f;

        private static readonly int GlowId = Shader.PropertyToID("_Glow");

        private LightmapData[] _lit;
        private LightmapData[] _dark;
        private MaterialPropertyBlock _block;
        private AudioSource _audio;
        private CarnyVoice _voice;
        private Coroutine _flicker;
        private bool _on = true;
        private bool _headedOn = true;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;

            _lit = Maps(litColour, litDirection);
            _dark = Maps(darkColour, darkDirection);
            if (_lit == null || _dark == null || _lit.Length != _dark.Length)
            {
                Debug.LogWarning("[CarnivalLights] The lit and dark bakes are missing or don't match. Run setup step 39. " +
                                 "Until then only the bulbs flicker; the room's light stays as it was baked");
                _lit = _dark = null;
            }
        }

        private void Start()
        {
            Set(false);
            _headedOn = false;

            _voice = CarnyVoice.Instance;
            if (_voice != null) _voice.Welcoming += OnWelcoming;
            if (game != null) game.RoundStarted += OnRoundStarted;
        }

        private void OnDestroy()
        {
            if (_voice != null) _voice.Welcoming -= OnWelcoming;
            if (game != null) game.RoundStarted -= OnRoundStarted;
        }

        private void OnWelcoming() { FlickerTo(true); }

        /// On for every round but the last, even if a quick grab skipped the welcome, and out for the final one
        private void OnRoundStarted(int round) { FlickerTo(game == null || round < game.MaxRounds); }

        public void FlickerTo(bool on)
        {
            if (_headedOn == on) return;

            _headedOn = on;
            if (_flicker != null) StopCoroutine(_flicker);
            _flicker = StartCoroutine(Flicker(on, on ? comingOn : goingOut));
        }

        private IEnumerator Flicker(bool target, float[] steps)
        {
            Play(buzzClips);

            // Even steps land on the target, odd ones leave it, so a run of any length stutters before it settles
            for (int i = 0; i < steps.Length; i++)
            {
                Set(i % 2 == 0 ? target : !target);
                yield return new WaitForSecondsRealtime(steps[i]);
            }

            Set(target);
            Play(target ? clunkClips : popClips);
            _flicker = null;
        }

        private void Set(bool on)
        {
            _on = on;

            if (_lit != null)
            {
                LightmapSettings.lightmaps = on ? _lit : _dark;
                var probes = on ? litProbes : darkProbes;
                if (probes != null) LightmapSettings.lightProbes = probes;
            }

            if (bulbs == null) return;
            foreach (var bulb in bulbs)
            {
                if (bulb == null) continue;

                bulb.GetPropertyBlock(_block);
                _block.SetFloat(GlowId, on ? 1f : darkGlow);
                bulb.SetPropertyBlock(_block);
            }
        }

        public bool On { get { return _on; } }

        private static LightmapData[] Maps(Texture2D[] colour, Texture2D[] direction)
        {
            if (colour == null || colour.Length == 0) return null;

            var maps = new LightmapData[colour.Length];
            for (int i = 0; i < colour.Length; i++)
            {
                maps[i] = new LightmapData
                {
                    lightmapColor = colour[i],
                    lightmapDir = direction != null && i < direction.Length ? direction[i] : null,
                };
            }

            return maps;
        }

        private void Play(AudioClip[] clips)
        {
            if (clips != null && clips.Length > 0) _audio.PlayOneShot(clips[Random.Range(0, clips.Length)], volume);
        }
    }
}
