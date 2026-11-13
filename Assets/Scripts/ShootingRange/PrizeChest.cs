using System;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    /// The chest on the bench holds the prize, like a real carnival game (user, 2026-11-02). It stays locked through the
    /// game; when the final score is up its lock glows and the barker says to shoot it. Shot, the lid flies open and the
    /// prize for the share of signs hit rises out and turns. Nobody shooting it still gets their prize, a little later
    public class PrizeChest : MonoBehaviour
    {
        [SerializeField] private PopOpenOnShot lid;
        [SerializeField] private Transform lockPart;
        [SerializeField] private Transform lockGlow;
        [SerializeField, Tooltip("Smallest to best, each hidden in the chest")] private Transform[] prizes;

        [Header("Which prize (data): the share of all the session's signs hit to earn each prize after the first")]
        [SerializeField] private float[] tierShares = { 0.2f, 0.4f, 0.6f, 0.75f, 0.9f };

        [Header("Reveal (data)")]
        [SerializeField, Tooltip("Metres the prize rises out of the chest")] private float riseHeight = 0.45f;
        [SerializeField] private float riseSeconds = 1.2f;
        [SerializeField, Tooltip("How much a prize grows as it rises, the same for every prize so each is seen properly (user, 2026-11-10)")]
        private float grow = 2.1f;
        [SerializeField] private float turnDegreesPerSecond = 45f;
        [SerializeField, Tooltip("How long the session holds on the prize before it ends")] private float revealSeconds = 4f;
        [SerializeField, Tooltip("Seconds the lock waits to be shot, from the drum roll starting, before the chest opens by itself. A drum roll that doesn't loop sets it: the chest opens on its climax")]
        private float claimSeconds = 15f;
        [Header("Showing where to shoot (data, user 2026-11-10: hard to know where to shoot)")]
        [SerializeField, Tooltip("A sign and arrow over the lock, shown while it waits; bobs")] private Transform prompt;
        [SerializeField] private float promptBob = 0.05f;
        [SerializeField] private float promptBobsPerSecond = 1.2f;
        [SerializeField, Range(0f, 1f)] private float glowPulse = 0.4f;
        [SerializeField] private float glowPulsesPerSecond = 1.5f;

        [Header("Audio")]
        [SerializeField] private AudioClip unlockClip;
        [SerializeField, Tooltip("Smallest to best. Spread over the prizes when there are fewer of these than prizes")] private AudioClip[] fanfares;
        [SerializeField, Range(0f, 1f)] private float volume = 0.8f;
        [Header("Celebration when it opens (user, 2026-11-10)")]
        [SerializeField] private ParticleSystem confetti;
        [SerializeField, Tooltip("Follows the prize up as it rises")] private ParticleSystem sparkle;
        [SerializeField, Range(0f, 1f), Tooltip("How far through the fanfare the barker names the prize, so he doesn't talk over its start")]
        private float lineAfterFanfare = 0.6f;

        [Header("Drum roll while the lock waits to be shot (user, 2026-11-10)")]
        [SerializeField, Tooltip("Plays until the chest opens; looped only if it's shorter than the wait")] private AudioClip drumRoll;
        [SerializeField, Tooltip("Cuts the roll off as the lid flies open")] private AudioClip drumRollEnd;
        [SerializeField, Range(0f, 1f)] private float drumRollVolume = 0.7f;

        public static PrizeChest Instance { get; private set; }

        /// The lid has flown open: the room celebrates (CarnivalLights, CountdownSkulls)
        public static event Action Opened;

        /// Waiting for its lock to be shot, or still showing the prize
        public bool Busy { get { return _waiting || _lineBand >= 0 || (_openedAt >= 0f && Time.unscaledTime < _openedAt + revealSeconds); } }

        private RangeGame _game;
        private TrackDirector _director;
        private AudioSource _audio;
        private AudioSource _roll;
        private Transform _prize;
        private Vector3 _prizeRest;
        private Vector3 _prizeSize;
        private Vector3 _glowSize;
        private float _grow = 1f;
        private float _endedAt;
        private float _claimAt = float.MaxValue;
        private Vector3 _promptRest;
        private float _openedAt = -1f;
        private bool _waiting;
        private int _lineBand = -1;
        private bool _rollWaiting;
        private float _lineAt;
        private int _signs;
        private int _hits;

        private void Awake()
        {
            Instance = this;
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0.5f;
            _roll = gameObject.AddComponent<AudioSource>();
            _roll.playOnAwake = false;
            _roll.loop = true;
            _roll.spatialBlend = 0.5f;
            if (lid != null) lid.Locked = true;
            if (lockGlow != null) { _glowSize = lockGlow.localScale; lockGlow.gameObject.SetActive(false); }
            if (prompt != null) { _promptRest = prompt.localPosition; prompt.gameObject.SetActive(false); }
            foreach (var prize in prizes) if (prize != null) prize.gameObject.SetActive(false);
        }

        private void Start()
        {
            _game = RangeGame.Instance;
            _director = FindObjectOfType<TrackDirector>();
            if (_game != null) _game.StateChanged += OnStateChanged;
            if (_director != null) _director.TargetResolved += OnTargetResolved;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_game != null) _game.StateChanged -= OnStateChanged;
            if (_director != null) _director.TargetResolved -= OnTargetResolved;
        }

        private void OnTargetResolved(bool shot)
        {
            _signs++;
            if (shot) _hits++;
        }

        private void OnStateChanged(RangeGame.State state)
        {
            if (state != RangeGame.State.Ended || _openedAt >= 0f) return;

            _waiting = true;
            _endedAt = Time.unscaledTime;
            if (lockGlow != null) lockGlow.gameObject.SetActive(true);
            if (prompt != null) prompt.gameObject.SetActive(true);
            _rollWaiting = true;
            _claimAt = float.MaxValue;
            if (CarnyVoice.Instance != null) CarnyVoice.Instance.AskForTheLock();
        }

        /// After his "shoot the lock" line, not under it (user, 2026-11-10)
        /// The clock to open by itself starts here, after his line, so the player has the whole roll to shoot
        private void StartTheRoll()
        {
            _rollWaiting = false;
            if (drumRoll == null) { _claimAt = Time.unscaledTime + claimSeconds; return; }

            _claimAt = Time.unscaledTime + (drumRoll.length >= claimSeconds ? drumRoll.length : claimSeconds);
            _roll.clip = drumRoll;
            // A crescendo that outlasts the wait plays once; looped it would drop back to quiet
            _roll.loop = drumRoll.length < claimSeconds;
            _roll.volume = drumRollVolume;
            _roll.Play();
        }

        /// The lock was shot, or nobody shot it in time
        public void Unlock()
        {
            if (!_waiting) return;

            _waiting = false;
            _openedAt = Time.unscaledTime;
            if (lockPart != null) lockPart.gameObject.SetActive(false);
            if (lockGlow != null) lockGlow.gameObject.SetActive(false);
            if (prompt != null) prompt.gameObject.SetActive(false);
            _rollWaiting = false;
            _roll.Stop();
            if (drumRollEnd != null) _audio.PlayOneShot(drumRollEnd, drumRollVolume);
            if (unlockClip != null) _audio.PlayOneShot(unlockClip, volume);
            if (lid != null) lid.OpenEmpty();

            int tier = Mathf.Min(Tier(), prizes.Length - 1);
            if (tier < 0) return;

            _prize = prizes[tier];
            _prizeRest = _prize.localPosition;
            _prizeSize = _prize.localScale;
            _grow = grow;
            _prize.gameObject.SetActive(true);
            float fanfareSeconds = 0f;
            if (fanfares != null && fanfares.Length > 0)
            {
                var fanfare = fanfares[Band(tier, fanfares.Length)];
                if (fanfare != null) { _audio.PlayOneShot(fanfare, volume); fanfareSeconds = fanfare.length; }
            }
            _lineBand = Band(tier, CarnyVoice.PrizeBands);
            _lineAt = Time.unscaledTime + fanfareSeconds * lineAfterFanfare;

            if (confetti != null) confetti.Play(true);
            if (sparkle != null) sparkle.Play(true);
            if (Opened != null) Opened();
        }

        private int Tier()
        {
            float share = _signs > 0 ? (float)_hits / _signs : 0f;
            int tier = 0;
            for (int i = 0; i < tierShares.Length; i++) if (share >= tierShares[i]) tier = i + 1;
            return tier;
        }

        /// Which of a smaller set (fanfares, barker lines) goes with this prize: the best prize always gets the best one
        private int Band(int tier, int bands)
        {
            if (prizes.Length <= 1) return bands - 1;
            return Mathf.RoundToInt((float)tier / (prizes.Length - 1) * (bands - 1));
        }

        private void Update()
        {
            if (_waiting)
            {
                if (lockGlow != null)
                    lockGlow.localScale = _glowSize * (1f + glowPulse * Mathf.Sin((Time.unscaledTime - _endedAt) * glowPulsesPerSecond * 2f * Mathf.PI));
                if (prompt != null)
                    prompt.localPosition = _promptRest + Vector3.up * (promptBob * Mathf.Sin((Time.unscaledTime - _endedAt) * promptBobsPerSecond * 2f * Mathf.PI));
                if (_rollWaiting && (CarnyVoice.Instance == null || !CarnyVoice.Instance.Talking)) StartTheRoll();
                if (Time.unscaledTime >= _claimAt) Unlock();
                return;
            }

            if (_lineBand >= 0 && Time.unscaledTime >= _lineAt)
            {
                if (CarnyVoice.Instance != null) CarnyVoice.Instance.Prize(_lineBand);
                _lineBand = -1;
            }

            if (_prize == null) return;

            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.unscaledTime - _openedAt) / riseSeconds));
            _prize.localPosition = _prizeRest + Vector3.up * riseHeight * t;
            _prize.localScale = _prizeSize * Mathf.Lerp(1f, _grow, t);
            _prize.Rotate(Vector3.up, turnDegreesPerSecond * Time.unscaledDeltaTime, Space.World);
            if (sparkle != null) sparkle.transform.position = _prize.position;
        }
    }
}
