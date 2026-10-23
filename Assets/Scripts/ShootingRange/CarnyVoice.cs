using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    public class CarnyVoice : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private RangeGame game;
        [SerializeField] private TrackDirector director;
        [SerializeField] private PowerUps powerUps;
        [SerializeField] private ComboTracker combo;
        [SerializeField] private SpiderNest spiders;
        [SerializeField] private BatSwarm bats;

        [Header("Welcome, as the guns appear (the second part follows the first)")]
        [SerializeField] private AudioClip[] welcome;
        [SerializeField] private AudioClip[] welcomeThen;
        [SerializeField] private float welcomeAfterSeconds = 1f;

        [Header("Between rounds (the second part follows the first)")]
        [SerializeField] private AudioClip[] nextRound;
        [SerializeField] private AudioClip[] nextRoundThen;
        [SerializeField] private AudioClip[] finalRound;
        [Tooltip("Element 0 says one, element 1 two, element 2 three")]
        [SerializeField] private AudioClip[] countdown;
        [SerializeField] private AudioClip[] go;

        [Header("Verdict on each round, by the share of its signs hit")]
        [SerializeField] private AudioClip[] timeUp;
        [SerializeField] private AudioClip[] verdictTerrible;
        [SerializeField] private AudioClip[] verdictPoor;
        [SerializeField] private AudioClip[] verdictGood;
        [SerializeField] private AudioClip[] verdictGreat;
        [SerializeField] private Vector3 verdictSteps = new Vector3(0.25f, 0.5f, 0.8f);
        [SerializeField] private AudioClip[] goodbye;

        [Header("Warnings, the first time each turns up (they cut in: the spider bites, the bat is coming)")]
        [SerializeField] private AudioClip[] spiderWarning;
        [SerializeField] private AudioClip[] batWarning;

        [Header("Reactions (only while he is quiet)")]
        [SerializeField] private AudioClip[] missStreak;
        [SerializeField] private AudioClip[] powerUp;
        [SerializeField] private AudioClip[] stacked;
        [SerializeField] private AudioClip[] slowMotion;
        [SerializeField] private AudioClip[] comboStreak;
        [SerializeField] private int missesInARow = 4;
        [SerializeField] private int comboEvery = 8;
        [SerializeField, Range(0f, 1f)] private float reactChance = 0.7f;
        [SerializeField] private float quietSeconds = 4f;
        [SerializeField, Range(0f, 1f)] private float volume = 1f;

        /// Mid-line, or with lines still waiting their turn
        public bool Talking { get { return Time.unscaledTime < _busyUntil || _queue.Count > 0; } }

        private readonly Queue<AudioClip> _queue = new Queue<AudioClip>();
        private AudioSource _audio;
        private int _misses;
        private float _busyUntil;
        private float _quietUntil;
        private bool _cleared;
        private bool _warnedOfSpiders;
        private bool _warnedOfBats;

        private void Awake()
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
            if (combo == null) combo = FindObjectOfType<ComboTracker>();
            if (spiders == null) spiders = FindObjectOfType<SpiderNest>();
            if (bats == null) bats = FindObjectOfType<BatSwarm>();
        }

        private void OnEnable()
        {
            if (game != null)
            {
                game.CountdownTick += OnCountdownTick;
                game.CountdownGo += OnCountdownGo;
                game.RoundStarted += OnRoundStarted;
                game.RoundCleared += OnRoundCleared;
                game.RoundEnded += OnRoundEnded;
                game.FinalScore += OnFinalScore;
            }

            if (director != null) director.TargetResolved += OnTargetResolved;
            if (powerUps != null) powerUps.Granted += OnGranted;
            if (combo != null) combo.Changed += OnComboChanged;
            if (spiders != null) spiders.Dropped += OnSpiderDropped;
            if (bats != null) bats.Launched += OnBatLaunched;
        }

        private void OnDisable()
        {
            if (game != null)
            {
                game.CountdownTick -= OnCountdownTick;
                game.CountdownGo -= OnCountdownGo;
                game.RoundStarted -= OnRoundStarted;
                game.RoundCleared -= OnRoundCleared;
                game.RoundEnded -= OnRoundEnded;
                game.FinalScore -= OnFinalScore;
            }

            if (director != null) director.TargetResolved -= OnTargetResolved;
            if (powerUps != null) powerUps.Granted -= OnGranted;
            if (combo != null) combo.Changed -= OnComboChanged;
            if (spiders != null) spiders.Dropped -= OnSpiderDropped;
            if (bats != null) bats.Launched -= OnBatLaunched;
        }

        private IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(welcomeAfterSeconds);

            // A grab inside that first second has already started the countdown
            if (game != null && game.Current != RangeGame.State.HowToPlay &&
                game.Current != RangeGame.State.WaitingForPickup) yield break;
            Queue(welcome);
            Queue(welcomeThen);
        }

        private void Update()
        {
            if (_queue.Count > 0 && Time.unscaledTime >= _busyUntil) Play(_queue.Dequeue());
        }

        /// The count has to land on the beat, so it is the one thing that cuts him off
        private void OnCountdownTick(int n)
        {
            if (countdown != null && n >= 1 && n <= countdown.Length) Interrupt(countdown[n - 1]);
        }

        private void OnCountdownGo()
        {
            Interrupt(Pick(go));
        }

        private void OnRoundStarted(int round)
        {
            _misses = 0;
            _cleared = false;
        }

        private void OnRoundCleared(int round)
        {
            _cleared = true;
        }

        /// A clean sweep goes straight to the cheer; anything less hears the bell called first
        private void OnRoundEnded(int round, int roundScore)
        {
            if (!_cleared) Queue(timeUp);
            Queue(Verdict());

            if (game.IsFinalRound) return;

            if (game.MaxRounds > 0 && game.NextRoundNumber >= game.MaxRounds)
            {
                Queue(finalRound);
            }
            else
            {
                Queue(nextRound);
                Queue(nextRoundThen);
            }
        }

        private AudioClip[] Verdict()
        {
            float hit = director != null && director.WaveSize > 0 ? (float)director.Killed / director.WaveSize : 0f;
            if (hit >= verdictSteps.z) return verdictGreat;
            if (hit >= verdictSteps.y) return verdictGood;
            if (hit >= verdictSteps.x) return verdictPoor;
            return verdictTerrible;
        }

        private void OnFinalScore(int total)
        {
            Queue(goodbye);
        }

        private void OnTargetResolved(bool shot)
        {
            if (shot) { _misses = 0; return; }

            if (++_misses < missesInARow) return;

            _misses = 0;
            React(missStreak);
        }

        private void OnSpiderDropped()
        {
            if (_warnedOfSpiders) return;

            _warnedOfSpiders = true;
            Interrupt(Pick(spiderWarning));
        }

        private void OnBatLaunched()
        {
            if (_warnedOfBats) return;

            _warnedOfBats = true;
            Interrupt(Pick(batWarning));
        }

        private void OnGranted(PowerUpKind kind, float seconds, bool stack)
        {
            if (kind == PowerUpKind.SlowMotion) React(slowMotion);
            else React(stack ? stacked : powerUp);
        }

        private void OnComboChanged(float multiplier, int chain)
        {
            if (comboEvery > 0 && chain > 0 && chain % comboEvery == 0) React(comboStreak);
        }

        private void Queue(AudioClip[] lines)
        {
            var clip = Pick(lines);
            if (clip != null) _queue.Enqueue(clip);
        }

        /// Heckles and cheers never queue or talk over anything, and are skipped now and then
        private void React(AudioClip[] lines)
        {
            if (Talking || Time.unscaledTime < _quietUntil || Random.value > reactChance) return;

            var clip = Pick(lines);
            if (clip == null) return;

            Play(clip);
            _quietUntil = _busyUntil + quietSeconds;
        }

        private void Interrupt(AudioClip clip)
        {
            if (clip == null) return;

            _queue.Clear();
            _audio.Stop();
            Play(clip);
        }

        private void Play(AudioClip clip)
        {
            _audio.PlayOneShot(clip, volume);
            _busyUntil = Time.unscaledTime + clip.length;
        }

        private static AudioClip Pick(AudioClip[] lines)
        {
            return lines != null && lines.Length > 0 ? lines[Random.Range(0, lines.Length)] : null;
        }

#if UNITY_EDITOR
        /// Fills each slot from the built clips, named carny_<slot>_<nn>, in number order, so the countdown
        /// lands one, two, three. Re-run after adding lines; only the slots it finds clips for change
        [ContextMenu("Fill Lines From Folder")]
        private void FillLinesFromFolder()
        {
            const string folder = "Assets/Sounds/ShootingRange/Voices/Carny";
            var paths = new List<string>();
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("carny_ t:AudioClip", new[] { folder }))
                paths.Add(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            paths.Sort(System.StringComparer.Ordinal);

            var slots = new Dictionary<string, List<AudioClip>>();
            foreach (var path in paths)
            {
                var name = System.IO.Path.GetFileNameWithoutExtension(path);
                int number = name.LastIndexOf('_');
                if (!name.StartsWith("carny_") || number <= 6) continue;

                var slot = name.Substring(6, number - 6);
                if (!slots.ContainsKey(slot)) slots[slot] = new List<AudioClip>();
                slots[slot].Add(UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path));
            }

            var so = new UnityEditor.SerializedObject(this);
            int filled = 0, clips = 0;
            foreach (var pair in slots)
            {
                var property = so.FindProperty(pair.Key);
                if (property == null || !property.isArray)
                {
                    Debug.LogWarning("[CarnyVoice] No slot called '" + pair.Key + "' for its clips", this);
                    continue;
                }

                property.arraySize = pair.Value.Count;
                for (int i = 0; i < pair.Value.Count; i++)
                    property.GetArrayElementAtIndex(i).objectReferenceValue = pair.Value[i];

                filled++;
                clips += pair.Value.Count;
            }

            so.ApplyModifiedProperties();
            Debug.Log("[CarnyVoice] Filled " + filled + " slots with " + clips + " clips from " + folder, this);
        }
#endif
    }
}
