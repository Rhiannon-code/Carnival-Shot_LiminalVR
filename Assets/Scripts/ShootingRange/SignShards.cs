using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    /// Ties a sign to the set of broken pieces cut from that same model. It lives on the sign rather
    /// than in a list somewhere, so reordering the variants cannot pair a coffin with a bat's shards
    public class SignShards : MonoBehaviour
    {
        [SerializeField] private GameObject brokenSet;

        public GameObject BrokenSet { get { return brokenSet; } }
    }
}
