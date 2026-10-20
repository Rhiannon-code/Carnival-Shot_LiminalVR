using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    /// What this sign says when it is hit. Lives on the sign, like SignShards, so reordering the
    /// variants can never hand a sign another one's voice
    public class SignVoice : MonoBehaviour
    {
        [SerializeField] private AudioClip[] clips;

        public AudioClip[] Clips { get { return clips; } }
    }
}
