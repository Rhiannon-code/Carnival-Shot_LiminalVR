using System.Collections.Generic;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    /// Bats that burst out of a shot sign and flutter off into the dark. For show only: they hit nothing and
    /// nothing can hit them. Built once at Awake and pooled
    public class BatBurst : MonoBehaviour
    {
        public static BatBurst Instance { get; private set; }

        [SerializeField] private GameObject batModel;
        [SerializeField] private int poolSize = 12;

        [Header("Flight (data, metres and seconds)")]
        [SerializeField] private float speed = 2.5f;
        [SerializeField] private float climb = 1.5f;
        [SerializeField] private float spread = 0.6f;
        [SerializeField] private float flutter = 0.15f;
        [SerializeField] private float seconds = 1.2f;
        [SerializeField, Range(0f, 1f)]
        private float shrinkFrom = 0.7f;

        private struct Flying
        {
            public Transform Bat;
            public Vector3 From;
            public Vector3 Velocity;
            public float Born;
            public float Phase;
        }

        private readonly Queue<Transform> _idle = new Queue<Transform>();
        private readonly List<Flying> _flying = new List<Flying>();
        private Vector3 _scale;

        private void Awake()
        {
            Instance = this;
            if (batModel == null) return;

            _scale = batModel.transform.localScale;
            for (int i = 0; i < poolSize; i++)
            {
                var bat = Instantiate(batModel, transform).transform;
                foreach (var collider in bat.GetComponentsInChildren<Collider>(true)) Destroy(collider);

                bat.gameObject.SetActive(false);
                _idle.Enqueue(bat);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Burst(Vector3 at, int count, Vector3 away)
        {
            for (int i = 0; i < count && _idle.Count > 0; i++)
            {
                var bat = _idle.Dequeue();
                var heading = (away + Random.insideUnitSphere * spread).normalized;
                heading.y = Mathf.Abs(heading.y);
                var velocity = heading * speed + Vector3.up * climb;

                bat.position = at + Random.insideUnitSphere * 0.1f;
                bat.rotation = Quaternion.LookRotation(new Vector3(velocity.x, 0f, velocity.z));
                bat.localScale = _scale;
                bat.gameObject.SetActive(true);

                _flying.Add(new Flying
                {
                    Bat = bat,
                    From = bat.position,
                    Velocity = velocity,
                    Born = Time.time,
                    Phase = Random.value * 10f,
                });
            }
        }

        private void Update()
        {
            for (int i = _flying.Count - 1; i >= 0; i--)
            {
                var flying = _flying[i];
                float age = Time.time - flying.Born;
                float t = age / Mathf.Max(0.01f, seconds);

                if (t >= 1f)
                {
                    flying.Bat.gameObject.SetActive(false);
                    _idle.Enqueue(flying.Bat);
                    _flying.RemoveAt(i);
                    continue;
                }

                var side = Vector3.Cross(Vector3.up, flying.Velocity).normalized;
                flying.Bat.position = flying.From + flying.Velocity * age +
                                      side * Mathf.Sin(age * 12f + flying.Phase) * flutter;
                flying.Bat.localScale = _scale * (1f - Mathf.InverseLerp(shrinkFrom, 1f, t));
            }
        }
    }
}
