using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    public class PrizeLock : MonoBehaviour, IShootable
    {
        [SerializeField] private PrizeChest chest;

        public void OnShot(Vector3 point, Vector3 direction, float impulse)
        {
            if (chest != null) chest.Unlock();
        }
    }
}
