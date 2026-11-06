using System.Collections;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    public class MusicBed : MonoBehaviour
    {
        [SerializeField] private RangeGame game;
        [SerializeField] private AudioClip clip;
        [SerializeField, Range(0f, 1f)] private float volume = 0.7f;
        [SerializeField] private float fadeOutSeconds = 4f;

        [Header("Winds up as the lights come on (data)")]
        [SerializeField] private float windUpSeconds = 4f;
        [SerializeField, Range(0f, 1f)] private float windUpVolume = 0.1f;
        [SerializeField, Range(0.1f, 1f)] private float windUpPitch = 0.55f;
        [SerializeField, Tooltip("Something mechanical being cranked up, once, under the music as it winds up")] private AudioClip windUpSound;
        [SerializeField, Range(0f, 1f)] private float windUpSoundVolume = 0.5f;

        /// 0 at the start of the wind-up to 1 when it's done. The barker follows it, so he and the music wind up as one
        public float WindUpThrough { get; private set; } = 1f;
        public float Pitch { get { return _audio != null ? _audio.pitch : 1f; } }

        private AudioSource _audio;
        private AudioSource _crank;

        private void Awake()
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.clip = clip;
            _audio.loop = true;
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
            _audio.volume = volume;

            _crank = gameObject.AddComponent<AudioSource>();
            _crank.playOnAwake = false;
            _crank.spatialBlend = 0f;
        }

        private void OnEnable()
        {
            if (game != null) game.StateChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            if (game != null) game.StateChanged -= OnStateChanged;
        }

        /// Silent in the dark until the start-up calls this. A scene with no carnival lights winds up straight away
        private void Start()
        {
            if (FindObjectOfType<CarnivalLights>() == null) WindUp();
        }

        public void WindUp()
        {
            if (clip == null || _audio.isPlaying) return;

            WindUpThrough = 0f;
            _audio.Play();
            if (windUpSound != null) _crank.PlayOneShot(windUpSound, windUpSoundVolume);
            StartCoroutine(WindingUp());
        }

        private void OnStateChanged(RangeGame.State state)
        {
            if (state == RangeGame.State.Ended) StartCoroutine(FadeOut());
        }

        private IEnumerator WindingUp()
        {
            for (float t = 0f; t < windUpSeconds; t += Time.unscaledDeltaTime)
            {
                float through = Mathf.SmoothStep(0f, 1f, t / windUpSeconds);
                WindUpThrough = through;
                _audio.volume = Mathf.Lerp(windUpVolume, volume, through);
                _audio.pitch = Mathf.Lerp(windUpPitch, 1f, through);
                yield return null;
            }

            WindUpThrough = 1f;
            _audio.volume = volume;
            _audio.pitch = 1f;
        }

        private IEnumerator FadeOut()
        {
            float start = _audio.volume;

            for (float t = 0f; t < fadeOutSeconds; t += Time.unscaledDeltaTime)
            {
                _audio.volume = Mathf.Lerp(start, 0f, t / fadeOutSeconds);
                yield return null;
            }

            _audio.Stop();
        }
    }
}
