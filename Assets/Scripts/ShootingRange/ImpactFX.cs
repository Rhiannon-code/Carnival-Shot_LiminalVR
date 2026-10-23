using System.Collections.Generic;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    public class ImpactFX : MonoBehaviour
    {
        public static ImpactFX Instance { get; private set; }

        [System.Serializable]
        public class Surface
        {
            public PhysicMaterial material;
            public AudioClip[] clips;
        }

        [Header("Impact burst")]
        [SerializeField] private ParticleSystem impactPrefab;
        [SerializeField] private int burstCopies = 8;
        [SerializeField] private float burstLifetime = 1f;

        [Header("Impact audio")]
        [SerializeField] private AudioClip[] impactClips;
        [SerializeField] private Surface[] surfaces;
        [SerializeField, Range(0f, 1f)] private float impactVolume = 0.55f;

        [Header("Bullet holes on the room (optional)")]
        [SerializeField] private ShotDecals decals;

        [Header("Shared voices")]
        [SerializeField] private int voices = 12;
        [SerializeField, Range(0f, 0.3f)] private float pitchJitter = 0.08f;
        [SerializeField] private float maxAudioDistance = 25f;
        // Unity's default is 1 m, which put a sign 8 m down the range 18 dB under the gun in your hand
        [SerializeField] private float minAudioDistance = 4f;

        // Unity never pitches audio with timeScale, so the world's slow motion has to be heard on purpose
        [SerializeField, Range(0.1f, 1f)] private float minSlowPitch = 0.5f;

        private struct Playing
        {
            public ParticleSystem System;
            public float Until;
        }

        private readonly Queue<ParticleSystem> _bursts = new Queue<ParticleSystem>();
        private readonly List<Playing> _playing = new List<Playing>();
        private AudioSource[] _voices;
        private int _nextVoice;

        private void Awake()
        {
            Instance = this;

            if (impactPrefab != null)
            {
                for (int i = 0; i < burstCopies; i++)
                {
                    var ps = Instantiate(impactPrefab, transform);
                    var main = ps.main;
                    main.loop = false;
                    main.playOnAwake = false;
                    ps.gameObject.SetActive(false);
                    _bursts.Enqueue(ps);
                }
            }

            _voices = new AudioSource[Mathf.Max(1, voices)];
            for (int i = 0; i < _voices.Length; i++)
            {
                // Each voice needs its own transform. Sharing this object's would move the pooled
                // particles parented to it every time a crate clattered somewhere else
                var holder = new GameObject("Voice " + i);
                holder.transform.SetParent(transform, false);

                var src = holder.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 1f;
                src.minDistance = minAudioDistance;
                src.maxDistance = maxAudioDistance;
                _voices[i] = src;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// One sweep for every burst still playing; a coroutine per impact is garbage at full auto
        private void Update()
        {
            for (int i = _playing.Count - 1; i >= 0; i--)
            {
                if (Time.time < _playing[i].Until) continue;

                var ps = _playing[i].System;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.gameObject.SetActive(false);
                _bursts.Enqueue(ps);
                _playing.RemoveAt(i);
            }
        }

        public void PlayImpact(Vector3 point, Vector3 normal, Collider struck)
        {
            PlayBurst(point, normal);
            PlayClip(Pick(ClipsFor(struck)), point, impactVolume);
            if (decals != null && StaysPut(struck)) decals.Place(point, normal);
        }

        /// A hole only goes on what never moves: not a prop that can be knocked about, anything shootable,
        /// or the coffin, whose lid swings open and would leave the mark in the air
        private static bool StaysPut(Collider struck)
        {
            return struck != null && struck.attachedRigidbody == null &&
                   struck.GetComponentInParent<IShootable>() == null && struck.GetComponentInParent<Coffin>() == null;
        }

        private AudioClip[] ClipsFor(Collider struck)
        {
            var material = struck != null ? struck.sharedMaterial : null;
            if (material == null || surfaces == null) return impactClips;

            for (int i = 0; i < surfaces.Length; i++)
            {
                if (surfaces[i] != null && surfaces[i].material == material && surfaces[i].clips != null &&
                    surfaces[i].clips.Length > 0)
                    return surfaces[i].clips;
            }

            return impactClips;
        }

        public void PlayClip(AudioClip clip, Vector3 point, float volume)
        {
            if (clip == null || _voices == null) return;

            var src = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;

            src.transform.position = point;
            src.pitch = (1f + (pitchJitter > 0f ? Random.Range(-pitchJitter, pitchJitter) : 0f)) *
                        Mathf.Max(minSlowPitch, Time.timeScale);
            src.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        private void PlayBurst(Vector3 point, Vector3 normal)
        {
            if (_bursts.Count == 0) return;

            var ps = _bursts.Dequeue();
            ps.transform.position = point;
            ps.transform.rotation = normal.sqrMagnitude > 1e-6f
                                  ? Quaternion.LookRotation(normal)
                                  : Quaternion.identity;
            ps.gameObject.SetActive(true);
            ps.Play(true);
            _playing.Add(new Playing { System = ps, Until = Time.time + burstLifetime });
        }

        private static AudioClip Pick(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return null;
            return clips[Random.Range(0, clips.Length)];
        }
    }
}
