using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    /// Step 86: the room's fixed pieces marked batching-static (user, 2026-11-13, after step 85 found walls, floor, steps,
    /// pillars and skirts at ~70 draws with none batching). Only what is already lightmap-static, which the lighting steps
    /// marked because it stays put, and never anything that moves or can be shot loose. A batched mesh is merged when play
    /// starts and no longer follows its transform, so it also takes the flag back off anything that moves: the first run
    /// froze the coffin's lid shut (user, 2026-11-13: "the coffin didn't open")
    public static partial class RangeSetup
    {
        [MenuItem("Shooting Range/Setup/86 - Batch The Room", false, 110)]
        public static void BatchTheRoomMenu()
        {
            Begin("Room batched");
            int marked = 0, already = 0;
            var freed = new System.Collections.Generic.List<string>();
            foreach (var renderer in Object.FindObjectsOfType<MeshRenderer>())
            {
                var go = renderer.gameObject;
                var flags = GameObjectUtility.GetStaticEditorFlags(go);
                bool batched = (flags & StaticEditorFlags.BatchingStatic) != 0;

                if (Moves(go.transform) || Animated(go.transform))
                {
                    if (!batched) continue;
                    GameObjectUtility.SetStaticEditorFlags(go, flags & ~StaticEditorFlags.BatchingStatic);
                    freed.Add(go.name);
                    continue;
                }

                if ((flags & StaticEditorFlags.LightmapStatic) == 0) continue;
                if (batched) { already++; continue; }

                GameObjectUtility.SetStaticEditorFlags(go, flags | StaticEditorFlags.BatchingStatic);
                marked++;
            }

            Did(marked + " fixed pieces marked batching-static (" + already + " already were). Run step 85 again to compare; " +
                "Unity merges them when play starts and in the build");
            if (freed.Count > 0) Did("Batching-static taken off " + freed.Count + " moving pieces, so they move again: " + string.Join(", ", freed));
            Dirty();
            End();
        }

        /// Scripts that move their own pieces without being shootable, and Unity's own animation
        private static bool Animated(Transform t)
        {
            return t.GetComponentInParent<Coffin>() != null || t.GetComponentInParent<PrizeChest>() != null ||
                   t.GetComponentInParent<Animator>() != null || t.GetComponentInParent<Animation>() != null;
        }
    }
}
