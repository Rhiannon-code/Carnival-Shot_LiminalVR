using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    public class CountdownSkulls : MonoBehaviour
    {
        [SerializeField] private GameObject[] eyes;
        [SerializeField] private float goHoldSeconds = 1f;

        private RangeGame _game;
        private ShatterOnShot[] _skulls;
        private float _darkAt = -1f;

        private void Start()
        {
            _skulls = new ShatterOnShot[eyes.Length];
            for (int i = 0; i < eyes.Length; i++)
            {
                // The eyes start switched off, and the plain lookup skips anything inactive
                var found = eyes[i] != null ? eyes[i].GetComponentsInParent<ShatterOnShot>(true) : null;
                _skulls[i] = found != null && found.Length > 0 ? found[0] : null;
            }

            Light(-1);

            _game = RangeGame.Instance;
            if (_game == null) return;

            _game.CountdownTick += OnTick;
            _game.CountdownGo += OnGo;
        }

        private void OnDestroy()
        {
            if (_game == null) return;

            _game.CountdownTick -= OnTick;
            _game.CountdownGo -= OnGo;
        }

        /// The beat with n left lights the skull n from the end, so the last one is left for GO however long
        /// the count is
        private void OnTick(int n)
        {
            Protect(true);
            Light(eyes.Length - 1 - n);
            _darkAt = -1f;
        }

        private void OnGo()
        {
            Protect(false);
            Light(eyes.Length - 1);
            _darkAt = Time.unscaledTime + goHoldSeconds;
        }

        private void Protect(bool on)
        {
            foreach (var skull in _skulls)
            {
                if (skull != null) skull.SetProtected(on);
            }
        }

        private void Update()
        {
            if (_darkAt < 0f || Time.unscaledTime < _darkAt) return;

            _darkAt = -1f;
            Light(-1);
        }

        private void Light(int index)
        {
            for (int i = 0; i < eyes.Length; i++)
            {
                if (eyes[i] != null) eyes[i].SetActive(i == index);
            }
        }
    }
}
