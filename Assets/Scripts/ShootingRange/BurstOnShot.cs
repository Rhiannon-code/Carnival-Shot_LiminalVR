using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    /// Each system either emits a burst there and then (count above 0) or plays its own run (count 0)
    public class BurstOnShot : RoomReaction
    {
        [SerializeField] private ParticleSystem[] systems;
        [SerializeField] private int[] counts;

        public override void OnShot(Vector3 point, Vector3 direction, float impulse)
        {
            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] == null) continue;

                int count = counts != null && i < counts.Length ? counts[i] : 0;
                if (count > 0) systems[i].Emit(count);
                else systems[i].Play(true);
            }
        }

        protected override void Restore()
        {
            foreach (var system in systems)
                if (system != null && !system.main.loop) system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
