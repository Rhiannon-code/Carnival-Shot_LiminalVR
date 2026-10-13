using System;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    public class ComboTracker : MonoBehaviour
    {
        [Header("Window (data)")]
        [SerializeField] private float windowSeconds = 2.5f;

        [Header("Growth (data)")]
        [SerializeField] private float perHit = 0.1f;
        [SerializeField] private float perChain = 0.25f;
        [SerializeField] private float maxMultiplier = 5f;

        public float Multiplier { get; private set; }
        public int Chain { get; private set; }
        public float WindowRemaining { get { return Mathf.Max(0f, _expiresAt - Time.unscaledTime); } }
        public float WindowSeconds { get { return windowSeconds; } }

        public event Action<float, int> Changed;

        private float _expiresAt;

        private void Awake()
        {
            Multiplier = 1f;
        }

        public void Register(bool chained)
        {
            if (Time.unscaledTime > _expiresAt) Reset(false);

            Chain++;
            Multiplier = Mathf.Min(maxMultiplier, Multiplier + (chained ? perChain : perHit));
            _expiresAt = Time.unscaledTime + windowSeconds;

            Raise();
        }

        public void Reset(bool raise)
        {
            Chain = 0;
            Multiplier = 1f;
            _expiresAt = 0f;
            if (raise) Raise();
        }

        private void Update()
        {
            if (Chain == 0 || Time.unscaledTime <= _expiresAt) return;
            Reset(true);
        }

        private void Raise()
        {
            if (Changed != null) Changed(Multiplier, Chain);
        }
    }
}
