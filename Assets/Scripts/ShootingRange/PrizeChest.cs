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

        [Header("Which prize (data): the share of all the session's signs hit for the 2nd, 3rd and top prize")]
        [SerializeField] private Vector3 tierSteps = new Vector3(0.25f, 0.5f, 0.8f);

        [Header("Reveal (data)")]
        [SerializeField, Tooltip("Metres the prize rises out of the chest")] private float riseHeight = 0.45f;
        [SerializeField] private float riseSeconds = 1.2f;
        [SerializeField, Tooltip("How much each prize grows as it rises, smallest to best")] private float[] grows = { 1f, 1.3f, 1.7f, 2.1f };
        [SerializeField] private float turnDegreesPerSecond = 45f;
        [SerializeField, Tooltip("How long the session holds on the prize before it ends")] private float revealSeconds = 4f;
        [SerializeField, Tooltip("Seconds the lock waits to be shot before the chest opens by itself")] private float claimSeconds = 15f;
        [SerializeField] private float glowPulsesPerSecond = 1.5f;

        [Header("Audio")]
        [SerializeField] private AudioClip unlockClip;
        [SerializeField, Tooltip("One per prize, smallest to best")] private AudioClip[] fanfares;
        [SerializeField, Range(0f, 1f)] private float volume = 0.8f;

        public static PrizeChest Instance { get; private set; }

        /// Waiting for its lock to be shot, or still showing the prize
        public bool Busy { get { return _waiting || (_openedAt >= 0f && Time.unscaledTime < _openedAt + revealSeconds); } }

        private RangeGame _game;
        private TrackDirector _director;
        private AudioSource _audio;
        private Transform _prize;
        private Vector3 _prizeRest;
        private Vector3 _prizeSize;
        private Vector3 _glowSize;
        private float _grow = 1f;
        private float _endedAt;
        private float _openedAt = -1f;
        private bool _waiting;
        private int _signs;
        private int _hits;

        private void Awake()
        {
            Instance = this;
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0.5f;
            if (lid != null) lid.Locked = true;
            if (lockGlow != null) { _glowSize = lockGlow.localScale; lockGlow.gameObject.SetActive(false); }
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
            if (CarnyVoice.Instance != null) CarnyVoice.Instance.AskForTheLock();
        }

        /// The lock was shot, or nobody shot it in time
        public void Unlock()
        {
            if (!_waiting) return;

            _waiting = false;
            _openedAt = Time.unscaledTime;
            if (lockPart != null) lockPart.gameObject.SetActive(false);
            if (lockGlow != null) lockGlow.gameObject.SetActive(false);
            if (unlockClip != null) _audio.PlayOneShot(unlockClip, volume);
            if (lid != null) lid.OpenEmpty();

            int tier = Mathf.Min(Tier(), prizes.Length - 1);
            if (tier < 0) return;

            _prize = prizes[tier];
            _prizeRest = _prize.localPosition;
            _prizeSize = _prize.localScale;
            _grow = tier < grows.Length ? grows[tier] : 1f;
            _prize.gameObject.SetActive(true);
            if (fanfares != null && tier < fanfares.Length && fanfares[tier] != null) _audio.PlayOneShot(fanfares[tier], volume);
            if (CarnyVoice.Instance != null) CarnyVoice.Instance.Prize(tier);
        }

        private int Tier()
        {
            float share = _signs > 0 ? (float)_hits / _signs : 0f;
            return share >= tierSteps.z ? 3 : share >= tierSteps.y ? 2 : share >= tierSteps.x ? 1 : 0;
        }

        private void Update()
        {
            if (_waiting)
            {
                if (lockGlow != null)
                    lockGlow.localScale = _glowSize * (1f + 0.25f * Mathf.Sin((Time.unscaledTime - _endedAt) * glowPulsesPerSecond * 2f * Mathf.PI));
                if (Time.unscaledTime >= _endedAt + claimSeconds) Unlock();
                return;
            }

            if (_prize == null) return;

            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.unscaledTime - _openedAt) / riseSeconds));
            _prize.localPosition = _prizeRest + Vector3.up * riseHeight * t;
            _prize.localScale = _prizeSize * Mathf.Lerp(1f, _grow, t);
            _prize.Rotate(Vector3.up, turnDegreesPerSecond * Time.unscaledDeltaTime, Space.World);
        }
    }
}
