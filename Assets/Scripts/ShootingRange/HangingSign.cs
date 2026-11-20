using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    public class HangingSign : MonoBehaviour
    {
        [SerializeField] private GameObject content;
        [SerializeField] private CanvasGroup group;

        [Header("Chains lays the chain pictures to this length")]
        [SerializeField] private float chainLength = 1000f;

        [Header("Motion (data)")]
        [SerializeField] private float dropHeight = 700f;
        [SerializeField] private float dropSeconds = 0.8f;
        [SerializeField] private float swingDegrees = 5f;
        [SerializeField] private float swingsPerSecond = 0.7f;
        [SerializeField] private float swingSettleSeconds = 3f;
        [SerializeField] private float raiseSeconds = 1.2f;
        [SerializeField, Range(0.05f, 1f)] private float fadeShare = 0.3f;

        private enum Phase { Up, Dropping, Down, Raising }

        private Phase _phase = Phase.Up;
        private float _since;
        private Vector3 _rest;
        private Quaternion _restRotation;
        private float _halfHeight;

        public float ChainLength { get { return chainLength; } }
        public bool IsUp { get { return _phase == Phase.Up; } }
        public bool IsDown { get { return _phase == Phase.Down; } }
        /// Down and done swinging
        public bool Settled { get { return IsDown && Time.unscaledTime - _since >= swingSettleSeconds; } }

        private void Awake()
        {
            _rest = transform.localPosition;
            _restRotation = transform.localRotation;
            var rect = transform as RectTransform;
            _halfHeight = rect != null ? rect.rect.height * 0.5f : 0f;
            content.SetActive(false);
            group.alpha = 0f;
        }

        public void Drop()
        {
            if (_phase == Phase.Dropping || _phase == Phase.Down) return;

            content.SetActive(true);
            _phase = Phase.Dropping;
            _since = Time.unscaledTime;
            Update();
        }

        public void Raise()
        {
            if (_phase == Phase.Up || _phase == Phase.Raising) return;

            _phase = Phase.Raising;
            _since = Time.unscaledTime;
        }

        private void Update()
        {
            if (_phase == Phase.Up) return;

            float t = Time.unscaledTime - _since;
            float lift = 0f, swing = 0f, alpha = 1f;
            switch (_phase)
            {
                case Phase.Dropping:
                    float fallen = Mathf.Clamp01(t / Mathf.Max(0.01f, dropSeconds));
                    lift = dropHeight * (1f - fallen * fallen);
                    alpha = Mathf.Clamp01(fallen / fadeShare);
                    if (fallen >= 1f)
                    {
                        _phase = Phase.Down;
                        _since = Time.unscaledTime;
                    }
                    break;
                case Phase.Down:
                    // Caught by the chains: it swings from their top and dies away
                    swing = swingDegrees * Mathf.Exp(-4f * t / Mathf.Max(0.01f, swingSettleSeconds)) *
                            Mathf.Sin(2f * Mathf.PI * swingsPerSecond * t);
                    break;
                case Phase.Raising:
                    float raised = Mathf.Clamp01(t / Mathf.Max(0.01f, raiseSeconds));
                    lift = dropHeight * Mathf.SmoothStep(0f, 1f, raised);
                    alpha = Mathf.Clamp01((1f - raised) / fadeShare);
                    if (raised >= 1f)
                    {
                        _phase = Phase.Up;
                        content.SetActive(false);
                    }
                    break;
            }

            var hang = _rest + Vector3.up * (_halfHeight + chainLength);
            var turn = Quaternion.Euler(0f, 0f, swing);
            transform.localPosition = hang + turn * (_rest - hang) + Vector3.up * lift;
            transform.localRotation = turn * _restRotation;
            group.alpha = alpha;
        }
    }
}
