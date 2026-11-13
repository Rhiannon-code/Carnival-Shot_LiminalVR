using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    public class RangeAudio : MonoBehaviour
    {
        [SerializeField] private RangeGame game;
        [SerializeField] private AudioClip countdownTickClip;
        [SerializeField] private AudioClip roundStartClip;
        [SerializeField] private AudioClip roundEndClip;
        [SerializeField, Tooltip("At the shooter, on every sign hit, so a hit is heard over the gunshot")] private AudioClip hitTickClip;
        [SerializeField, Range(0f, 1f)] private float hitTickVolume = 0.6f;
        [SerializeField, Range(0f, 1f)] private float volume = 0.75f;

        private AudioSource _audio;

        private void Awake()
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
        }

        private void OnEnable()
        {
            TrackMover.Struck += OnStruck;
            if (game == null) return;

            game.CountdownTick += OnTick;
            game.RoundStarted += OnRoundStarted;
            game.RoundEnded += OnRoundEnded;
        }

        private void OnDisable()
        {
            TrackMover.Struck -= OnStruck;
            if (game == null) return;

            game.CountdownTick -= OnTick;
            game.RoundStarted -= OnRoundStarted;
            game.RoundEnded -= OnRoundEnded;
        }

        private void OnTick(int n) { Play(countdownTickClip); }
        private void OnStruck() { if (hitTickClip != null && _audio != null) _audio.PlayOneShot(hitTickClip, hitTickVolume); }
        private void OnRoundStarted(int round) { Play(roundStartClip); }
        private void OnRoundEnded(int round, int roundScore) { Play(roundEndClip); }

        private void Play(AudioClip clip)
        {
            if (clip != null && _audio != null) _audio.PlayOneShot(clip, volume);
        }
    }
}
