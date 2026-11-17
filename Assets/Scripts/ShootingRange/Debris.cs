using System;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    public enum DebrisKind { None, Splinters, Sparks, Chips, Embers, Stuffing, Chunks, ImpactStar, ImpactRing, Dust }

    public class Debris : MonoBehaviour
    {
        [Serializable]
        public class Kind
        {
            public DebrisKind kind;
            public ParticleSystem system;
            public int fewest = 4;
            public int most = 6;
        }

        public static Debris Instance { get; private set; }

        [SerializeField] private Kind[] kinds;
        [SerializeField] private int signTimes = 2;
        [SerializeField] private DebrisKind[] alsoOnSign = { DebrisKind.Chunks, DebrisKind.ImpactStar, DebrisKind.ImpactRing, DebrisKind.Dust };

        private void Awake() { Instance = this; }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void OnSign(Vector3 point, Vector3 towardShooter)
        {
            Play(DebrisKind.Splinters, point, towardShooter, signTimes);
            foreach (var kind in alsoOnSign) Play(kind, point, towardShooter);
        }

        public void Play(DebrisKind kind, Vector3 point, Vector3 facing, int times = 1)
        {
            if (kinds == null || kind == DebrisKind.None || facing.sqrMagnitude < 1e-6f) return;

            foreach (var entry in kinds)
            {
                if (entry.kind != kind || entry.system == null) continue;

                entry.system.transform.SetPositionAndRotation(point, Quaternion.LookRotation(facing));
                entry.system.Emit(UnityEngine.Random.Range(entry.fewest, entry.most + 1) * times);
                return;
            }
        }
    }
}
