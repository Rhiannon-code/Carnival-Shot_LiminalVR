using UnityEngine;
using TMPro;
using Liminal.SDK.VR.Avatars;

namespace IntuitiveDesigns.ShootingRange
{
    /// Big on the announcer board while a gun is dry, naming the grip that reloads it. The count on the
    /// gun itself is easy to miss with the eyes out on the range
    [RequireComponent(typeof(TMP_Text))]
    public class ReloadPrompt : MonoBehaviour
    {
        [SerializeField] private RangeGame game;

        [Header("Copy (data)")]
        [SerializeField] private string headline = "RELOAD";
        [SerializeField] private string leftGrip = "L GRIP";
        [SerializeField] private string rightGrip = "R GRIP";
        [SerializeField] private string bothGrips = "L + R GRIP";

        [Header("Pulse (data)")]
        [SerializeField] private float pulseScale = 0.08f;
        [SerializeField] private float pulsesPerSecond = 2f;

        private TMP_Text _text;
        private Pistol[] _guns;
        private string _leftText;
        private string _rightText;
        private string _bothText;
        private string _shown;

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();

            // Built once, so a dry gun costs no string per frame
            _leftText = headline + "\n<size=60%>" + leftGrip + "</size>";
            _rightText = headline + "\n<size=60%>" + rightGrip + "</size>";
            _bothText = headline + "\n<size=60%>" + bothGrips + "</size>";
            Show(string.Empty);
        }

        private void Start()
        {
            if (game == null) game = RangeGame.Instance;
            _guns = FindObjectsOfType<Pistol>();
        }

        private void Update()
        {
            bool left = false;
            bool right = false;

            if (game != null && game.Current == RangeGame.State.Playing && _guns != null)
            {
                for (int i = 0; i < _guns.Length; i++)
                {
                    var gun = _guns[i];
                    if (gun == null || !gun.IsHeld || !gun.IsEmpty) continue;

                    if (gun.Hand == VRAvatarLimbType.LeftHand) left = true;
                    else right = true;
                }
            }

            Show(left && right ? _bothText : left ? _leftText : right ? _rightText : string.Empty);
            if (_shown.Length == 0) return;

            float pulse = 1f + pulseScale * Mathf.Sin(Time.unscaledTime * pulsesPerSecond * 2f * Mathf.PI);
            transform.localScale = Vector3.one * pulse;
        }

        private void Show(string text)
        {
            if (ReferenceEquals(text, _shown)) return;

            _shown = text;
            _text.text = text;
            transform.localScale = Vector3.one;
        }
    }
}
