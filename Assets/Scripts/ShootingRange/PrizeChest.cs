using System;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    public class PrizeChest : MonoBehaviour
    {
        [SerializeField] private PopOpenOnShot lid;
        [SerializeField] private Transform lockPart;
        [SerializeField] private Transform lockGlow;
        [SerializeField] private Transform[] prizes;

        [Header("Which prize (data): the session's points to earn each prize after the first")]
        [SerializeField] private int[] tierScores = { 6000, 12000, 18000, 24000, 30000 };
        [SerializeField] private Color[] tierGlows =
        {
            new Color(1f, 1f, 1f), new Color(0.3f, 1f, 0.35f), new Color(0.3f, 0.55f, 1f),
            new Color(0.7f, 0.35f, 1f), new Color(1f, 0.55f, 0.15f), new Color(1f, 0.84f, 0.3f),
        };

        [Header("Reveal (data)")]
        [SerializeField] private float riseTo = 1.6f;
        [SerializeField] private float riseSeconds = 1.2f;
        [SerializeField]
        private float grow = 2.1f;
        [SerializeField] private float turnDegreesPerSecond = 45f;
        [SerializeField] private float revealSeconds = 4f;
        [SerializeField]
        private float claimSeconds = 15f;
        [Header("Showing where to shoot hard to know where to shoot)")]
        [SerializeField] private Transform prompt;
        [SerializeField] private float promptBob = 0.05f;
        [SerializeField] private float promptBobsPerSecond = 1.2f;
        [SerializeField, Range(0f, 1f)] private float glowPulse = 0.4f;
        [SerializeField] private float glowPulsesPerSecond = 1.5f;

        [Header("Audio")]
        [SerializeField] private AudioClip unlockClip;
        [SerializeField] private AudioClip[] fanfares;
        [SerializeField, Range(0f, 1f)] private float volume = 0.8f;
        [Header("Celebration when it opens")]
        [SerializeField] private ParticleSystem confetti;
        [SerializeField] private ParticleSystem sparkle;
        [SerializeField, Range(0f, 1f)]
        private float lineAfterFanfare = 0.6f;

        [Header("Soft glow behind the risen prize: one particle, its size and colour set on the particle system")]
        [SerializeField] private ParticleSystem halo;
        [SerializeField, Range(0f, 1f)] private float haloPulse = 0.12f;
        [SerializeField] private float haloPulsesPerSecond = 0.5f;
        [SerializeField] private float haloBehind = 0.1f;

        [Header("Drum roll while the lock waits to be shot")]
        [SerializeField] private AudioClip drumRoll;
        [SerializeField] private AudioClip drumRollEnd;
        [SerializeField, Range(0f, 1f)] private float drumRollVolume = 1f;

        public static PrizeChest Instance { get; private set; }

        public static event Action Opened;

        public static event Action LockOffered;

        /// Asking for the lock, waiting for it to be shot, or still showing the prize
        public bool Busy
        {
            get { return _asking || _waiting || _lineBand >= 0 || (_openedAt >= 0f && Time.unscaledTime < _openedAt + revealSeconds); }
        }

        private RangeGame _game;
        private AudioSource _audio;
        private AudioSource _roll;
        private Transform _prize;
        private Transform _view;
        private Renderer[] _prizeParts;
        private Vector3 _prizeRest;
        private Vector3 _prizeRisen;
        private Vector3 _prizeSize;
        private Vector3 _glowSize;
        private Vector3 _haloSize;
        private float _grow = 1f;
        private float _offeredAt;
        private float _claimAt = float.MaxValue;
        private Vector3 _promptRest;
        private float _openedAt = -1f;
        private bool _asking;
        private bool _waiting;
        private int _lineBand = -1;
        private bool _rollWaiting;
        private bool _rollUntilTally;
        private bool _rollEnded;
        private float _lineAt;

        private void Awake()
        {
            Instance = this;
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0.5f;
            _roll = gameObject.AddComponent<AudioSource>();
            _roll.playOnAwake = false;
            _roll.loop = true;
            // Flat, like the barker it answers: half placed at the chest it was lost under his voice
            _roll.spatialBlend = 0f;
            if (lid != null) lid.Locked = true;
            if (lockGlow != null) { _glowSize = lockGlow.localScale; lockGlow.gameObject.SetActive(false); }
            if (prompt != null) { _promptRest = prompt.localPosition; prompt.gameObject.SetActive(false); }
            if (halo != null) _haloSize = halo.transform.localScale;
            foreach (var prize in prizes) if (prize != null) prize.gameObject.SetActive(false);
        }

        private void Start()
        {
            _game = RangeGame.Instance;
            if (_game != null) _game.StateChanged += OnStateChanged;
            _rollUntilTally = FindObjectOfType<RangeAnnouncer>() != null;
            RangeAnnouncer.TallyFinished += EndTheRoll;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_game != null) _game.StateChanged -= OnStateChanged;
            RangeAnnouncer.TallyFinished -= EndTheRoll;
        }

        private void OnStateChanged(RangeGame.State state)
        {
            if (state != RangeGame.State.Ended || _openedAt >= 0f || _asking || _waiting) return;

            _asking = true;
            _rollWaiting = true;
            _claimAt = float.MaxValue;
            if (CarnyVoice.Instance != null) CarnyVoice.Instance.AskForTheLock();
        }

        /// Once he has finished saying to shoot it: before then a shot would skip his line and the roll
        private void OfferTheLock()
        {
            _asking = false;
            _waiting = true;
            _offeredAt = Time.unscaledTime;
            if (lockGlow != null) lockGlow.gameObject.SetActive(true);
            if (prompt != null) prompt.gameObject.SetActive(true);
            if (_rollWaiting) StartTheRoll();
            if (LockOffered != null) LockOffered();
        }

        /// On his "Drum roll, please!", or with the lock if he never says it.
        /// The clock to open by itself starts here, so the player has the whole roll to shoot
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

        public void Unlock()
        {
            if (!_waiting) return;

            _waiting = false;
            _openedAt = Time.unscaledTime;
            if (lockPart != null) lockPart.gameObject.SetActive(false);
            if (lockGlow != null) lockGlow.gameObject.SetActive(false);
            if (prompt != null) prompt.gameObject.SetActive(false);
            _rollWaiting = false;
            if (!_rollUntilTally) EndTheRoll();
            if (unlockClip != null) _audio.PlayOneShot(unlockClip, volume);
            if (lid != null) lid.OpenEmpty();

            int tier = Mathf.Min(Tier(), prizes.Length - 1);
            if (tier < 0) return;

            _prize = prizes[tier];
            _prizeRest = _prize.localPosition;
            _prizeSize = _prize.localScale;
            _grow = grow;
            _prize.gameObject.SetActive(true);
            _prizeParts = _prize.GetComponentsInChildren<Renderer>();
            _prizeRisen = Risen();
            if (halo != null)
            {
                TintTheGlow(tier);
                halo.transform.position = BehindThePrize();
                halo.transform.localScale = Vector3.zero;
                halo.Play(true);
            }
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

        private void TintTheGlow(int tier)
        {
            if (tierGlows == null || tierGlows.Length == 0) return;

            var colour = tierGlows[Mathf.Clamp(tier, 0, tierGlows.Length - 1)];
            foreach (var system in halo.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = system.main;
                var tinted = colour;
                tinted.a = main.startColor.color.a;
                main.startColor = tinted;
            }
        }

        /// Where the prize's pivot ends up, in its parent, for its middle at full size to sit riseTo above the floor. It
        /// grows about its pivot and turns about the vertical, so the middle's height over the pivot just scales by grow
        private Vector3 Risen()
        {
            Vector3 pivot = _prize.position;
            float middleOverPivot = (Middle().y - pivot.y) * _grow;
            var risen = new Vector3(pivot.x, riseTo - middleOverPivot, pivot.z);
            return _prize.parent.InverseTransformPoint(risen);
        }

        private Vector3 Middle() { return Whole().center; }

        private Bounds Whole()
        {
            if (_prizeParts == null || _prizeParts.Length == 0) return new Bounds(_prize.position, Vector3.zero);

            var bounds = _prizeParts[0].bounds;
            for (int i = 1; i < _prizeParts.Length; i++) bounds.Encapsulate(_prizeParts[i].bounds);
            return bounds;
        }

        /// Where the glow goes: past the prize on the player's line of sight. At the prize's middle its flat card cut through
        /// the prize, so it lay over one half and hid behind the other, and slid about as the prize turned
        private Vector3 BehindThePrize()
        {
            var bounds = Whole();
            if (_view == null && Camera.main != null) _view = Camera.main.transform;
            if (_view == null) return bounds.center;

            Vector3 away = bounds.center - _view.position;
            if (away.sqrMagnitude < 1e-6f) return bounds.center;

            float reach = new Vector2(bounds.extents.x, bounds.extents.z).magnitude;
            return bounds.center + away.normalized * (reach + haloBehind);
        }

        /// The roll goes on under the score counting up, and the crash lands on the total; with no board to
        /// count it, on the shot
        private void EndTheRoll()
        {
            if (_openedAt < 0f || _rollEnded) return;

            _rollEnded = true;
            _roll.Stop();
            if (drumRollEnd != null) _audio.PlayOneShot(drumRollEnd, drumRollVolume);
        }

        private int Tier()
        {
            int score = _game != null ? _game.TotalScore : 0;
            int tier = 0;
            for (int i = 0; i < tierScores.Length; i++) if (score >= tierScores[i]) tier = i + 1;
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
            if (_asking)
            {
                var voice = CarnyVoice.Instance;
                if (_rollWaiting && voice != null && voice.CalledForTheDrumRoll) StartTheRoll();
                if (voice == null || !voice.Talking) OfferTheLock();
                return;
            }

            if (_waiting)
            {
                if (lockGlow != null)
                    lockGlow.localScale = _glowSize * (1f + glowPulse * Mathf.Sin((Time.unscaledTime - _offeredAt) * glowPulsesPerSecond * 2f * Mathf.PI));
                if (prompt != null)
                    prompt.localPosition = _promptRest + Vector3.up * (promptBob * Mathf.Sin((Time.unscaledTime - _offeredAt) * promptBobsPerSecond * 2f * Mathf.PI));
                if (Time.unscaledTime >= _claimAt) Unlock();
                return;
            }

            if (_lineBand >= 0 && Time.unscaledTime >= _lineAt)
            {
                if (CarnyVoice.Instance != null) CarnyVoice.Instance.Prize(_lineBand);
                _lineBand = -1;
            }

            if (_prize == null) return;

            float shown = Time.unscaledTime - _openedAt;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(shown / riseSeconds));
            _prize.localPosition = Vector3.Lerp(_prizeRest, _prizeRisen, t);
            _prize.localScale = _prizeSize * Mathf.Lerp(1f, _grow, t);
            _prize.Rotate(Vector3.up, turnDegreesPerSecond * Time.unscaledDeltaTime, Space.World);
            if (sparkle != null) sparkle.transform.position = _prize.position;
            if (halo != null)
            {
                halo.transform.position = BehindThePrize();
                halo.transform.localScale = _haloSize * t * (1f + haloPulse * Mathf.Sin(shown * haloPulsesPerSecond * 2f * Mathf.PI));
            }
        }
    }
}
