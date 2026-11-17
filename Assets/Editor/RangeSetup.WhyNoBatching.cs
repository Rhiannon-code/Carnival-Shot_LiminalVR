using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    public static partial class RangeSetup
    {
        [MenuItem("Shooting Range/Setup/90 - Why No Batching (measure only, in play)", false, 114)]
        public static void WhyNoBatchingMenu()
        {
            Begin("Why no batching, measured only, nothing changed");
            try { WhyNoBatching(); }
            catch (System.Exception e) { Fail("Stopped by an error: " + e); }
            finally { End(); }
        }

        private static void WhyNoBatching()
        {
            if (!Application.isPlaying) Say("Not in play mode: Unity batches when play starts, so 'batched' below is always 0. Run it again while playing");

            var flagged = Object.FindObjectsOfType<MeshRenderer>()
                .Where(r => r.enabled && r.gameObject.activeInHierarchy &&
                            (GameObjectUtility.GetStaticEditorFlags(r.gameObject) & StaticEditorFlags.BatchingStatic) != 0)
                .ToList();
            Say(flagged.Count + " active renderers are batching-static; " + flagged.Count(r => r.isPartOfStaticBatch) + " of them were batched");

            var blocked = flagged.Where(r => r.HasPropertyBlock()).ToList();
            if (blocked.Count > 0) Say(blocked.Count + " carry a property block: " + Names(blocked));

            var mirrored = flagged.Where(r => Mirrored(r.transform)).ToList();
            if (mirrored.Count > 0) Say(mirrored.Count + " are mirrored (negative scale), so batch only with other mirrored pieces: " + Names(mirrored));

            var maps = flagged.GroupBy(r => r.lightmapIndex).OrderBy(g => g.Key);
            Say("Lightmaps they use: " + string.Join(", ", maps.Select(g => (g.Key < 0 ? "none" : g.Key.ToString()) + " (" + g.Count() + ")")));

            var multi = flagged.Where(r => r.sharedMaterials.Length > 1).ToList();
            if (multi.Count > 0) Say(multi.Count + " have more than one material (one draw each): " + Names(multi));

            // Copies of one material don't batch with each other: same name, different asset or instance
            var byMaterial = flagged.SelectMany(r => r.sharedMaterials.Where(m => m != null).Select(m => new { r, m }))
                                    .GroupBy(x => x.m).OrderByDescending(g => g.Count()).ToList();
            Say(byMaterial.Count + " different materials among them");
            foreach (var g in byMaterial.Take(DrawCostRows))
            {
                int batched = g.Count(x => x.r.isPartOfStaticBatch);
                Say("material " + g.Key.name + " (" + g.Key.shader.name + "): " + g.Count() + " renderers, " + batched + " batched" +
                    (g.Count() > 1 && batched == 0 ? ". Not batched, e.g. " + Names(g.Select(x => x.r).Take(4).ToList()) : ""));
            }

            foreach (var twins in byMaterial.GroupBy(g => g.Key.name.Replace(" (Instance)", "")).Where(n => n.Count() > 1))
                Say("'" + twins.Key + "' is " + twins.Count() + " separate materials of the same name, which can't batch together");
        }

        private static bool Mirrored(Transform t)
        {
            var s = t.lossyScale;
            return s.x * s.y * s.z < 0f;
        }

        private static string Names(List<MeshRenderer> renderers)
        {
            var names = renderers.Take(12).Select(r => r.name).ToArray();
            return string.Join(", ", names) + (renderers.Count > 12 ? " and " + (renderers.Count - 12) + " more" : "");
        }
    }
}
