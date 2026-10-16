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

            bool empty = pistol.IsEmpty;
            _text.color = empty ? promptColour : countColour;
            _text.text = empty ? reloadPrompt : pistol.RoundsLeft.ToString();
        }

        private void Update()
        {
            // Picking the gun up raises no ammo event, so the count appears on the first held frame
            if (pistol != null && pistol.IsHeld && _text.text.Length == 0 && !pistol.IsReloading) Show();
        }
    }
}
