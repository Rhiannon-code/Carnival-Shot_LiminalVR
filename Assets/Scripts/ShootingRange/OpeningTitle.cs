using System;
using System.Collections;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    public class OpeningTitle : MonoBehaviour
    {
        [SerializeField] private HangingSign logo;

        [Header("Haze (data)")]
        [SerializeField] private Color hazeColour = new Color(0.22f, 0.02f, 0.03f);
        [SerializeField] private float hazeDensity = 0.3f;
        [SerializeField] private float hazeClearSeconds = 4f;

        [Header("Timing (data, seconds)")]
        [SerializeField] private float darkBeforeLogo = 1.5f;
        [SerializeField] private float logoHoldSeconds = 3f;

        /// The logo has started to drop: CarnivalLights starts the carnival up
        public bool LogoDropped { get; private set; }
        /// The logo has started back up: the controls card drops
        public bool LogoLeaving { get; private set; }
        public event Action Leaving;

        private void Awake()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = hazeColour;
            RenderSettings.fogDensity = hazeDensity;
        }

        private void Start()
        {
            StartCoroutine(Present());
        }

        private IEnumerator Present()
        {
            yield return new WaitForSecondsRealtime(darkBeforeLogo);

            logo.Drop();
            LogoDropped = true;
            while (!logo.Settled) yield return null;
            yield return new WaitForSecondsRealtime(logoHoldSeconds);

            logo.Raise();
            LogoLeaving = true;
            if (Leaving != null) Leaving();

            for (float t = 0f; t < hazeClearSeconds; t += Time.unscaledDeltaTime)
            {
                RenderSettings.fogDensity = Mathf.Lerp(hazeDensity, 0f, t / hazeClearSeconds);
                yield return null;
            }

            RenderSettings.fog = false;
        }
    }
}
