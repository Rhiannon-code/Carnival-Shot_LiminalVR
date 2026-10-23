using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    /// A knock each time this lands on or strikes something, louder the harder it hit
    public class Clatter : MonoBehaviour
    {
        [SerializeField] private AudioClip[] clips;
        [SerializeField] private float quietestSpeed = 1f;
        [SerializeField] private float loudestSpeed = 5f;
        [SerializeField, Range(0f, 1f)] private float volume = 0.6f;
        [SerializeField] private float rearmSeconds = 0.08f;

        private float _rearmAt;

        private void OnCollisionEnter(Collision collision)
        {
            if (Time.time < _rearmAt || clips == null || clips.Length == 0 || ImpactFX.Instance == null) return;

            float speed = collision.relativeVelocity.magnitude;
            if (speed < quietestSpeed) return;

            _rearmAt = Time.time + rearmSeconds;

            float loudness = Mathf.InverseLerp(quietestSpeed, loudestSpeed, speed);
            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            ImpactFX.Instance.PlayClip(clips[Random.Range(0, clips.Length)], point, volume * Mathf.Lerp(0.35f, 1f, loudness));
        }
    }
}
