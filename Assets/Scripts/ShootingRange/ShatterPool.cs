using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    public class ShatterPool : MonoBehaviour
    {
        public static ShatterPool Instance { get; private set; }

        [SerializeField] private Rigidbody fragmentPrefab;
        [SerializeField] private int poolSize = 60;

        [Header("Broken sign sets (data)")]
        [SerializeField] private GameObject[] shardSets;
        [SerializeField] private int copiesPerSet = 2;

        [Header("Burst (data)")]
        [SerializeField] private float lifetime = 2.5f;
        [SerializeField] private float scatter = 0.14f;
        [SerializeField] private float sidewaysShare = 0.6f;
        [SerializeField] private float spin = 4f;

        [Header("Pop effect")]
        [SerializeField] private ParticleSystem popPrefab;
        [SerializeField] private int popCopies = 6;
        [SerializeField] private float popLifetime = 1.2f;
        [SerializeField] private float popLead = 0.15f;

        [Header("Audio")]
        [SerializeField] private AudioClip[] shatterClips;
        [SerializeField, Range(0f, 1f)] private float shatterVolume = 0.7f;

        private class Pieces
        {
            public GameObject Root;
            public Rigidbody[] Bodies;
            public Vector3[] Home;
            public Quaternion[] Facing;
        }

        private readonly Dictionary<GameObject, Queue<Pieces>> _sets =
            new Dictionary<GameObject, Queue<Pieces>>();

        private readonly Queue<Rigidbody> _idle = new Queue<Rigidbody>();
        private readonly Queue<ParticleSystem> _pops = new Queue<ParticleSystem>();

        private void Awake()
        {
            Instance = this;

            if (popPrefab != null)
            {
                for (int i = 0; i < popCopies; i++)
                {
                    var pop = Instantiate(popPrefab, transform);
                    pop.gameObject.SetActive(false);
                    _pops.Enqueue(pop);
                }
            }

            BuildSets();

            if (fragmentPrefab == null)
            {
                Debug.LogWarning("[ShatterPool] No fragment prefab assigned. Targets will vanish without debris.");
                return;
            }

            for (int i = 0; i < poolSize; i++)
            {
                var piece = Instantiate(fragmentPrefab, transform);
                piece.gameObject.SetActive(false);
                _idle.Enqueue(piece);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// The broken pieces of the sign that was actually hit. They arrive where the sign was, so they
        /// line up with what vanished, and are thrown from the hole the round made
        public void Burst(GameObject set, Transform sign, Vector3 at, Vector3 direction, float impulse)
        {
            Queue<Pieces> spare;
            if (set == null || sign == null || !_sets.TryGetValue(set, out spare) || spare.Count == 0)
            {
                Burst(at, 8, direction, impulse);
                return;
            }

            var pieces = spare.Dequeue();

            pieces.Root.transform.position = sign.position;
            pieces.Root.transform.rotation = sign.rotation;
            pieces.Root.transform.localScale = sign.lossyScale;
            pieces.Root.SetActive(true);

            for (int i = 0; i < pieces.Bodies.Length; i++)
            {
                var body = pieces.Bodies[i];
                if (body == null) continue;

                body.transform.localPosition = pieces.Home[i];
                body.transform.localRotation = pieces.Facing[i];
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;

                Vector3 away = body.worldCenterOfMass - at;
                if (away.sqrMagnitude < 1e-4f) away = direction;

                body.AddForce((away.normalized + direction * 0.5f).normalized * impulse, ForceMode.Impulse);
                body.AddTorque(Random.insideUnitSphere * spin, ForceMode.Impulse);
            }

            PlayPop(at, direction);

            if (ImpactFX.Instance != null && shatterClips != null && shatterClips.Length > 0)
                ImpactFX.Instance.PlayClip(shatterClips[Random.Range(0, shatterClips.Length)], at, shatterVolume);

            StartCoroutine(ReclaimSet(set, pieces));
        }

        public void Burst(Vector3 at, int count, Vector3 direction, float impulse)
        {
            int thrown = Mathf.Min(count, _idle.Count);

            for (int i = 0; i < thrown; i++)
            {
                var piece = _idle.Dequeue();

                piece.transform.position = at + Random.insideUnitSphere * scatter;
                piece.transform.rotation = Random.rotation;
                piece.gameObject.SetActive(true);

                piece.velocity = Vector3.zero;
                piece.angularVelocity = Vector3.zero;

                Vector3 push = (direction + Random.insideUnitSphere * sidewaysShare).normalized * impulse;
                piece.AddForce(push, ForceMode.Impulse);
                piece.AddTorque(Random.insideUnitSphere * spin, ForceMode.Impulse);

                StartCoroutine(Reclaim(piece));
            }

            PlayPop(at, direction);

            if (ImpactFX.Instance != null && shatterClips != null && shatterClips.Length > 0)
                ImpactFX.Instance.PlayClip(shatterClips[Random.Range(0, shatterClips.Length)], at, shatterVolume);
        }

        /// Built once at Awake. Instantiating eight rigidbodies the moment a sign is shot would hitch
        private void BuildSets()
        {
            if (shardSets == null) return;

            for (int i = 0; i < shardSets.Length; i++)
            {
                var set = shardSets[i];
                if (set == null || _sets.ContainsKey(set)) continue;

                var spare = new Queue<Pieces>();
                for (int copy = 0; copy < Mathf.Max(1, copiesPerSet); copy++)
                {
                    var root = Instantiate(set, transform);
                    var bodies = root.GetComponentsInChildren<Rigidbody>(true);

                    var pieces = new Pieces
                    {
                        Root = root,
                        Bodies = bodies,
                        Home = new Vector3[bodies.Length],
                        Facing = new Quaternion[bodies.Length],
                    };

                    for (int b = 0; b < bodies.Length; b++)
                    {
                        pieces.Home[b] = bodies[b].transform.localPosition;
                        pieces.Facing[b] = bodies[b].transform.localRotation;
                    }

                    root.SetActive(false);
                    spare.Enqueue(pieces);
                }

                _sets.Add(set, spare);
            }
        }

        private IEnumerator ReclaimSet(GameObject set, Pieces pieces)
        {
            yield return new WaitForSeconds(lifetime);

            for (int i = 0; i < pieces.Bodies.Length; i++)
            {
                if (pieces.Bodies[i] == null) continue;

                pieces.Bodies[i].velocity = Vector3.zero;
                pieces.Bodies[i].angularVelocity = Vector3.zero;
            }

            pieces.Root.SetActive(false);
            _sets[set].Enqueue(pieces);
        }

        /// Opens back toward the shooter, in front of what was hit. Left facing its own way it puffed
        /// upward from behind the sign, which hid half of it until the sign spun or fell out of the way
        public void PlayPop(Vector3 at, Vector3 direction)
        {
            if (_pops.Count == 0) return;

            var pop = _pops.Dequeue();
            Vector3 towardShooter = direction.sqrMagnitude > 1e-6f ? -direction.normalized : Vector3.up;
            Vector3 opens = Quaternion.Euler(pop.shape.rotation) * Vector3.forward;

            pop.transform.rotation = Quaternion.FromToRotation(opens, towardShooter);
            pop.transform.position = at + towardShooter * popLead;
            pop.gameObject.SetActive(true);
            pop.Play(true);
            StartCoroutine(ReclaimPop(pop));
        }

        private IEnumerator Reclaim(Rigidbody piece)
        {
            yield return new WaitForSeconds(lifetime);

            piece.velocity = Vector3.zero;
            piece.angularVelocity = Vector3.zero;
            piece.gameObject.SetActive(false);
            _idle.Enqueue(piece);
        }

        private IEnumerator ReclaimPop(ParticleSystem pop)
        {
            yield return new WaitForSeconds(popLifetime);

            pop.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            pop.gameObject.SetActive(false);
            _pops.Enqueue(pop);
        }
    }
}
