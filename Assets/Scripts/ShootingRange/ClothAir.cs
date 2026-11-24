using System.Collections.Generic;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    [RequireComponent(typeof(Renderer))]
    public class ClothAir : MonoBehaviour
    {
        [SerializeField] private float reach = 0.6f;
        [SerializeField] private float gap = 0.05f;

        private const int Remembered = 4;
        private static readonly int AirId = Shader.PropertyToID("_ClothAir");
        private static readonly List<ClothAir> All = new List<ClothAir>();
        private static readonly Vector4[] Pushes = new Vector4[Remembered];
        private static int _next;

        private Bounds _near;
        private float _quietUntil;

        private void OnEnable()
        {
            if (All.Count == 0)
            {
                for (int i = 0; i < Remembered; i++) Pushes[i] = new Vector4(0f, 0f, 0f, -100f);
                Shader.SetGlobalVectorArray(AirId, Pushes);
            }
            All.Add(this);

            // The cloth only ever moves in the shader, so where it is can be worked out once
            _near = GetComponent<Renderer>().bounds;
            _near.Expand(reach * 2f);
        }

        private void OnDisable() { All.Remove(this); }

        /// A round moving from 'from', 'length' metres along 'along' this step
        public static void Passing(Vector3 from, Vector3 along, float length)
        {
            for (int a = 0; a < All.Count; a++) All[a].Stir(from, along, length);
        }

        private void Stir(Vector3 from, Vector3 along, float length)
        {
            float enter;
            if (Time.time < _quietUntil || !_near.IntersectRay(new Ray(from, along), out enter) || enter > length) return;

            _quietUntil = Time.time + gap;
            float closest = Mathf.Clamp(Vector3.Dot(_near.center - from, along), 0f, length);
            Vector3 at = from + along * closest;
            Pushes[_next] = new Vector4(at.x, at.y, at.z, Time.timeSinceLevelLoad);
            _next = (_next + 1) % Remembered;
            Shader.SetGlobalVectorArray(AirId, Pushes);
        }
    }
}
