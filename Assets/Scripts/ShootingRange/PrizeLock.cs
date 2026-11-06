using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    /// The prize chest's padlock: shooting it opens the chest, once the game is over (PrizeChest)
    public class PrizeLock : MonoBehaviour, IShootable
    {
        [SerializeField] private PrizeChest chest;

        public void OnShot(Vector3 point, Vector3 direction, float impulse)
        {
            if (chest != null) chest.Unlock();
        }
    }
}
