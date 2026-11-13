using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

namespace IntuitiveDesigns.ShootingRange
{
    public class CarnivalLights : MonoBehaviour
    {
        /// The room baked with some of the strings on, so it can build up with them in the start-up
        [Serializable]
        public class Stage
        {
            public Texture2D[] colour;
            public Texture2D[] direction;
            public LightProbes probes;
        }

        [SerializeField] private RangeGame game;
        [SerializeField] private Renderer[] bulbs;

        [Header("The two bakes (filled by setup step 39)")]
        [SerializeField] private Texture2D[] litColour;
        [SerializeField] private Texture2D[] litDirection;
        [SerializeField] private LightProbes litProbes;
        [SerializeField] private Texture2D[] darkColour;
        [SerializeField] private Texture2D[] darkDirection;
        [SerializeField] private LightProbes darkProbes;
        [SerializeField, Tooltip("One string on, two strings on, and so on, up to one short of all of them")] private Stage[] stages;

        [Header("Flicker (data, seconds): each step swaps between on and off, and it ends where it was headed")]
        [SerializeField] private float[] comingOn = { 0.12f, 0.2f, 0.08f, 0.35f, 0.1f, 0.15f };
        [SerializeField] private float[] goingOut = { 0.25f, 0.1f, 0.15f, 0.08f, 0.4f, 0.06f };
        [SerializeField] private float[] blackoutLeadIn = { 0.1f, 0.2f, 0.06f, 0.3f, 0.08f, 0.15f, 0.05f, 0.4f };
        [SerializeField, Range(0f, 1f)] private float darkGlow = 0.04f;
        [Header("When the prize chest opens: the strings blink in turn, like a theatre marquee (data)")]
        [SerializeField] private float celebrateSeconds = 6f;
        [SerializeField] private float marqueeStepsPerSecond = 6f;

        [Header("Start-up (data, seconds): dark, the coin goes in, then the strings stutter on one after another, each bringing its share of the room's light")]
        [SerializeField] private float darkHoldSeconds = 3f;
        [SerializeField] private AudioClip coinClip;
        [SerializeField] private float[] stringComingOn = { 0.06f, 0.09f, 0.04f, 0.12f };
        [SerializeField] private float stringGap = 0.09f;

        [Header("What a bake can't darken (data): unlit fog and sky reflections would glow in a dark room")]
        [SerializeField] private Renderer[] fog;
        [SerializeField] private Color fogLit = new Color(0.45f, 0.42f, 0.5f);
        [SerializeField] private Color fogDark = new Color(0.1f, 0.025f, 0.025f);
        [SerializeField, Range(0f, 1f)] private float reflectionsLit = 0.35f;
        [SerializeField, Range(0f, 1f)] private float reflectionsDark = 0f;

        [Header("Audio")]
        [SerializeField] private AudioClip[] clunkClips;
        [SerializeField] private AudioClip[] buzzClips;
        [SerializeField, Range(0f, 1f)] private float buzzVolume = 0.15f;
        [SerializeField] private AudioClip litHum;
        [SerializeField, Range(0f, 1f)] private float litHumVolume = 0.08f;
        [SerializeField] private AudioClip[] popClips;
        [SerializeField] private AudioClip humClip;
        [SerializeField, Range(0f, 1f)] private float humVolume = 0.25f;
        [SerializeField, Range(0.1f, 3f)] private float humPitch = 0.7f;
        [SerializeField, Range(0f, 1f)] private float volume = 0.8f;

        private static readonly int GlowId = Shader.PropertyToID("_Glow");
        private static readonly int FogColourId = Shader.PropertyToID("_Color");
        private static readonly int RoomLightId = Shader.PropertyToID("_RoomLight");

        private LightmapData[] _lit;
        private LightmapData[] _dark;
        private LightmapData[][] _stageMaps;
        private MaterialPropertyBlock _block;
        private AudioSource _audio;
        private AudioSource _hum;
        private AudioSource _litHum;
        private CarnyVoice _voice;
        private MusicBed _music;
        private Coroutine _flicker;
        private bool _on = true;
        private bool _headedOn = true;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;

            _hum = gameObject.AddComponent<AudioSource>();
            _hum.playOnAwake = false;
            _hum.spatialBlend = 0f;
            _hum.loop = true;
            _hum.clip = humClip;
            _hum.volume = humVolume;
            _hum.pitch = humPitch;

            _litHum = gameObject.AddComponent<AudioSource>();
            _litHum.playOnAwake = false;
            _litHum.spatialBlend = 0f;
            _litHum.loop = true;
            _litHum.clip = litHum;

            _lit = Maps(litColour, litDirection);
            _dark = Maps(darkColour, darkDirection);
            if (_lit == null || _dark == null || _lit.Length != _dark.Length)
            {
                Debug.LogWarning("[CarnivalLights] The lit and dark bakes are missing or don't match. Run setup step 39. " +
                                 "Until then only the bulbs flicker; the room's light stays as it was baked");
                _lit = _dark = null;
            }

            int count = stages != null ? stages.Length : 0;
            _stageMaps = new LightmapData[count][];
            for (int i = 0; i < count; i++)
            {
                var maps = Maps(stages[i].colour, stages[i].direction);
                _stageMaps[i] = _lit != null && maps != null && maps.Length == _lit.Length ? maps : null;
            }
        }

        private void Start()
        {
            Set(false);
            _headedOn = false;
            if (humClip != null) _hum.Play();

            _voice = CarnyVoice.Instance;
            _music = FindObjectOfType<MusicBed>();
            if (game != null) game.RoundStarted += OnRoundStarted;
            PrizeChest.Opened += Celebrate;
            StartCoroutine(StartUp());
        }

        private void OnDestroy()
        {
            if (game != null) game.RoundStarted -= OnRoundStarted;
            PrizeChest.Opened -= Celebrate;
        }

        private void Celebrate()
        {
            if (_on) Run(Marquee());
        }

        private IEnumerator Marquee()
        {
            for (float t = 0f; t < celebrateSeconds; t += Time.unscaledDeltaTime)
            {
                int step = Mathf.FloorToInt(t * marqueeStepsPerSecond);
                for (int i = 0; bulbs != null && i < bulbs.Length; i++) Glow(bulbs[i], (i + step) % 2 == 0 ? 1f : darkGlow);
                yield return null;
            }

            foreach (var bulb in bulbs) Glow(bulb, 1f);
            _flicker = null;
        }

        /// The player sits in the dark with the red and the hum, then a coin drops and the carnival starts up: the
        /// barker, the music and the strings together. Grabbing a gun in the dark cuts the wait short
        private IEnumerator StartUp()
        {
            for (float t = 0f; t < darkHoldSeconds && StillWelcoming(); t += Time.unscaledDeltaTime) yield return null;

            if (coinClip != null)
            {
                _audio.PlayOneShot(coinClip, volume);
                yield return new WaitForSecondsRealtime(coinClip.length);
            }

            if (_voice != null) _voice.Welcome();
            if (_music != null) _music.WindUp();
            if (_headedOn) yield break;

            _headedOn = true;
            Run(PowerUp());
        }

        private bool StillWelcoming()
        {
            return game == null || game.Current == RangeGame.State.HowToPlay;
        }

        /// On for every round, even if a quick grab skipped the welcome
        private void OnRoundStarted(int round) { FlickerTo(true); }

        public void FlickerTo(bool on)
        {
            if (_headedOn == on) return;

            _headedOn = on;
            Run(Flicker(on, on ? comingOn : goingOut));
        }

        /// A short stutter that ends back on. Skipped if the lights are already doing something
        public void Stutter(float[] steps)
        {
            if (_flicker == null && _on && _headedOn) Run(Stuttering(steps));
        }

        /// Flickers, goes out for seconds, leaving the red, then comes back on
        public void Blackout(float seconds)
        {
            if (!_headedOn) return;

            _headedOn = false;
            Run(OutAndBack(seconds));
        }

        private void Run(IEnumerator sequence)
        {
            if (_flicker != null) StopCoroutine(_flicker);
            _flicker = StartCoroutine(sequence);
        }

        private IEnumerator Flicker(bool target, float[] steps)
        {
            Play(buzzClips, buzzVolume);
            foreach (var wait in Steps(target, steps)) yield return wait;

            Set(target);
            Play(target ? clunkClips : popClips, volume);
            _flicker = null;
        }

        private IEnumerator Stuttering(float[] steps)
        {
            Play(buzzClips, buzzVolume);
            foreach (var wait in Steps(false, steps)) yield return wait;

            Set(true);
            _flicker = null;
        }

        private IEnumerator OutAndBack(float seconds)
        {
            Play(buzzClips, buzzVolume);
            foreach (var wait in Steps(false, blackoutLeadIn)) yield return wait;

            Set(false);
            Play(popClips, volume);
            yield return new WaitForSecondsRealtime(seconds);

            _headedOn = true;
            Play(buzzClips, buzzVolume);
            foreach (var wait in Steps(true, comingOn)) yield return wait;

            Set(true);
            Play(clunkClips, volume);
            _flicker = null;
        }

        /// Each string stutters on, then brings its share of the room's light, which is one bake per string swapped in.
        /// Only once it settles: swapping the whole room's lightmaps and probes on every flicker made the start-up lag
        private IEnumerator PowerUp()
        {
            for (int i = 0; bulbs != null && i < bulbs.Length; i++)
            {
                LitHum((i + 1f) / bulbs.Length);
                for (int step = 0; step < stringComingOn.Length; step++)
                {
                    Glow(bulbs[i], step % 2 == 0 ? 1f : darkGlow);
                    yield return new WaitForSecondsRealtime(stringComingOn[step]);
                }

                Glow(bulbs[i], 1f);
                ShowRoom(i + 1);
                yield return new WaitForSecondsRealtime(stringGap);
            }

            Set(true);
            Play(clunkClips, volume);
            _flicker = null;
        }

        /// Even steps land on first, odd ones leave it, so a run of any length stutters before it settles
        private IEnumerable Steps(bool first, float[] steps)
        {
            for (int i = 0; i < steps.Length; i++)
            {
                Set(i % 2 == 0 ? first : !first);
                yield return new WaitForSecondsRealtime(steps[i]);
            }
        }

        private void Set(bool on)
        {
            _on = on;
            if (on) _hum.Stop();
            LitHum(on ? 1f : 0f);
            ShowRoom(on ? StringCount : 0);

            if (bulbs == null) return;
            foreach (var bulb in bulbs) Glow(bulb, on ? 1f : darkGlow);
        }

        private int StringCount { get { return bulbs != null ? bulbs.Length : 0; } }

        /// The strings hum while they're lit, louder the more of them are on, and fall silent when they go out. Once
        /// started it runs on and only its volume follows the lights: restarting it on every flicker step chopped it up
        private void LitHum(float share)
        {
            if (litHum == null) return;

            _litHum.volume = litHumVolume * share;
            if (share > 0f && !_litHum.isPlaying) _litHum.Play();
        }

        /// The room as it is with this many strings on. Without a bake for that many, it stays as it was
        private void ShowRoom(int stringsOn)
        {
            float through = StringCount > 0 ? (float)stringsOn / StringCount : 1f;
            RenderSettings.reflectionIntensity = Mathf.Lerp(reflectionsDark, reflectionsLit, through);

            // Signs and bullet holes glow a little by themselves, so they have to be told when the room goes dark
            Shader.SetGlobalFloat(RoomLightId, through);
            SetFog(Color.Lerp(fogDark, fogLit, through));

            if (_lit == null) return;

            LightmapData[] maps = null;
            LightProbes probes = null;
            if (stringsOn <= 0) { maps = _dark; probes = darkProbes; }
            else if (stringsOn >= StringCount) { maps = _lit; probes = litProbes; }
            else if (stringsOn - 1 < _stageMaps.Length && _stageMaps[stringsOn - 1] != null)
            {
                maps = _stageMaps[stringsOn - 1];
                probes = stages[stringsOn - 1].probes;
            }

            if (maps == null) return;

            LightmapSettings.lightmaps = maps;
            if (probes != null) LightmapSettings.lightProbes = probes;
        }

        private void SetFog(Color colour)
        {
            if (fog == null) return;

            foreach (var plane in fog)
            {
                if (plane == null) continue;

                plane.GetPropertyBlock(_block);
                _block.SetColor(FogColourId, colour);
                plane.SetPropertyBlock(_block);
            }
        }

        private void Glow(Renderer bulb, float glow)
        {
            if (bulb == null) return;

            bulb.GetPropertyBlock(_block);
            _block.SetFloat(GlowId, glow);
            bulb.SetPropertyBlock(_block);
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

        private void Play(AudioClip[] clips, float at)
        {
            if (clips != null && clips.Length > 0) _audio.PlayOneShot(clips[Random.Range(0, clips.Length)], at);
        }
    }
}
