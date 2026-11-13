using UnityEngine;
using TMPro;

namespace IntuitiveDesigns.ShootingRange
{
    /// A small count on the gun itself, where the player is already looking. Empty, it says how to reload
    [RequireComponent(typeof(TMP_Text))]
    public class AmmoCounter : MonoBehaviour
    {
        [SerializeField] private Pistol pistol;

        [Header("Copy (data)")]
        [SerializeField] private string reloadPrompt = "RELOAD · GRIP";
        [SerializeField, Tooltip("Shown while a power-up gives the gun a bottomless magazine")] private string bottomless = "\u221E";
        [SerializeField] private Color countColour = new Color(1f, 0.92f, 0.75f);
        [SerializeField] private Color promptColour = new Color(1f, 0.3f, 0.2f);

        private TMP_Text _text;

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
            if (pistol == null) pistol = GetComponentInParent<Pistol>();
        }

        private void OnEnable()
        {
            if (pistol != null) pistol.AmmoChanged += OnAmmoChanged;
        }

        private void OnDisable()
        {
            if (pistol != null) pistol.AmmoChanged -= OnAmmoChanged;
        }

        private void Start()
        {
            Show();
            if (PowerUps.Instance != null) PowerUps.Instance.Expired += OnPowerUpExpired;
        }

        private void OnDestroy()
        {
            if (PowerUps.Instance != null) PowerUps.Instance.Expired -= OnPowerUpExpired;
        }

        /// A grant refills the gun, which already redraws the count; the end of one changes nothing on the gun
        private void OnPowerUpExpired(PowerUpKind kind)
        {
            Show();
        }

        private void OnAmmoChanged(int rounds, int size)
        {
            Show();
        }

        /// Nothing while it reloads or is not in hand; the gun says that itself
        private void Show()
        {
            if (pistol == null || !pistol.IsHeld || pistol.IsReloading || pistol.RoundsLeft < 0)
            {
                _text.text = string.Empty;
                return;
            }

            var powerUps = PowerUps.Instance;
            if (powerUps != null && powerUps.BottomlessMagazine)
            {
                _text.color = countColour;
                _text.text = bottomless;
                return;
            }

            bool empty = pistol.IsEmpty;
            _text.color = empty ? promptColour : countColour;
            _text.text = empty ? reloadPrompt : pistol.RoundsLeft.ToString();
        }

        private void Update()
        {
            // Being held or not raises no ammo event, so the count follows the hold here
            if (pistol == null || pistol.IsReloading) return;

            bool showing = _text.text.Length > 0;
            if (pistol.IsHeld != showing) Show();
        }
    }
}
