using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    public class CountdownSkulls : MonoBehaviour
    {
        [SerializeField] private GameObject[] eyes;
        [SerializeField] private float goHoldSeconds = 1f;

        private RangeGame _game;
        private RoomReaction[] _skulls;
        private float _darkAt = -1f;

        private void Start()
        {
            _skulls = new RoomReaction[eyes.Length];
            for (int i = 0; i < eyes.Length; i++)
            {
                // The eyes start switched off, and the plain lookup skips anything inactive
                var found = eyes[i] != null ? eyes[i].GetComponentsInParent<RoomReaction>(true) : null;
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

        /// Red on 3, orange on 2, green on 1. Green stays lit through GO
        private void OnTick(int n)
        {
            Protect(true);
            Light(eyes.Length - n);
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
