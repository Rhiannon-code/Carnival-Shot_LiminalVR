using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    /// Step 40b: the red under the signs, laid again from where the rails are now, onto step art that can take a bake (user,
    /// 2026-11-17: "the middle set of red lights isn't showing up on the texture when trying to bake"). The middle row's lights
    /// were 1-2 m behind both its rails, left from before step 30b moved them, and Alex's step-wall and step-counter were
    /// probe-lit only, so no bake could paint them. Nothing else about the lighting changes, unlike steps 40 and 44
    public static partial class RangeSetup
    {
        private static readonly string[] LightmappedStepArt = { "Assets/Mesh/step-wall/step-wall.fbx", "Assets/Mesh/step_counter/step-counter.fbx" };

        [MenuItem("Shooting Range/Setup/40b - Lay The Red Again Onto Lightmapped Step Art", false, 63)]
        public static void RedRelayMenu()
        {
            if (!EditorUtility.DisplayDialog("Lay the red again",
                    "This turns on Generate Lightmap UVs for Alex's step-wall and step-counter models (they reimport), makes every one " +
                    "of them in the scene Lightmap Static, and DELETES and lays out again, from the rails where they are now, " +
                    "the red lights in " + LightingName + "/Underglow (any hand edits to them go; the glow strips are kept where they are) and " +
                    LightingName + "/Probes. Nothing else " +
                    "about the lighting changes. Bake afterwards (step 39).", "Do it", "Cancel"))
                return;

            Begin("Red laid again onto lightmapped step art");
            try
            {
                LightmapUVsOnTheStepArt();
                LightmapStaticStepArt();
                var strips = TakeOutTheGlowStrips();
                RelayTheRedAndTheProbes();
                PutBackTheGlowStrips(strips);
            }
            catch (System.Exception e) { Fail("Stopped by an error, so nothing after this point ran: " + e); }
            finally { End(); }
        }

        /// Step 40c: the front row's red reached only the middle of the riser behind it, as its rail is short (user, 2026-11-17:
        /// "Add end lights"). Works from the lights where the user placed them, so it never moves one
        private const string RedEndName = "Red End";

        [MenuItem("Shooting Range/Setup/40c - Red Lights At The Front Row's Ends", false, 63)]
        public static void RedAtTheEndsMenu()
        {
            Begin("Red lights at the front row's ends");
            var lighting = RangeSetupUtil.FindInScene(LightingName);
            var underglow = lighting != null ? RangeSetupUtil.Child(lighting.transform, "Underglow") : null;
            if (underglow == null) { Fail("No " + LightingName + "/Underglow. Run step 40b"); End(); return; }

            var reds = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in underglow)
                if (child.GetComponent<Light>() != null) reds.Add(child);
            if (reds.Count == 0) { Fail("No red lights in " + LightingName + "/Underglow. Run step 40b"); End(); return; }

            float lowest = reds.Min(red => red.position.y);
            var row = reds.Where(red => red.position.y - lowest < LevelTolerance).OrderBy(red => red.position.x).ToList();
            float spacing = row.Count > 1 ? (row[row.Count - 1].position.x - row[0].position.x) / (row.Count - 1) : GlowSpacing;

            EndLight(underglow, reds, row[0], -spacing);
            EndLight(underglow, reds, row[row.Count - 1], spacing);
            End();
        }

        private static void EndLight(Transform underglow, System.Collections.Generic.List<Transform> reds, Transform end, float step)
        {
            if (end.name == RedEndName) { Say("The row already ends in a " + RedEndName + " at " + end.position.ToString("F2") + ", left alone"); return; }

            var at = end.position + Vector3.right * step;
            if (reds.Any(red => Vector3.Distance(red.position, at) < GlowSpacing * 0.5f))
            {
                Say("There's already a red light near " + at.ToString("F2") + ", left alone");
                return;
            }

            var added = Object.Instantiate(end.gameObject, underglow).transform;
            added.name = RedEndName;
            added.position = at;
            Dirty();
            Did("Red light added at " + at.ToString("F2") + ", a copy of the row's end light at " + end.position.ToString("F2"));
            if (Buried(at)) Say("   ^ it is inside a collider there. A light inside a mesh bakes nothing; move it by hand");
        }

        /// The strips stay where the user put them (2026-11-17: "don't move the glow strips they're where I want them to be")
        private static System.Collections.Generic.List<Transform> TakeOutTheGlowStrips()
        {
            var kept = new System.Collections.Generic.List<Transform>();
            var lighting = RangeSetupUtil.FindInScene(LightingName);
            var underglow = lighting != null ? RangeSetupUtil.Child(lighting.transform, "Underglow") : null;
            if (underglow == null) return kept;

            foreach (Transform child in underglow)
                if (child.name.StartsWith("GlowStrip")) kept.Add(child);
            foreach (var strip in kept) strip.SetParent(null, true);
            return kept;
        }

        private static void PutBackTheGlowStrips(System.Collections.Generic.List<Transform> kept)
        {
            if (kept.Count == 0) { Say("There were no glow strips to keep, so the new ones stay"); return; }

            var underglow = RangeSetupUtil.Child(RangeSetupUtil.FindInScene(LightingName).transform, "Underglow");
            var made = new System.Collections.Generic.List<GameObject>();
            foreach (Transform child in underglow)
                if (child.name.StartsWith("GlowStrip")) made.Add(child.gameObject);
            foreach (var strip in made) Object.DestroyImmediate(strip);
            foreach (var strip in kept) strip.SetParent(underglow, true);

            Dirty();
            Did(kept.Count + " glow strips kept exactly where they were");
        }

        private static void LightmapUVsOnTheStepArt()
        {
            foreach (var path in LightmappedStepArt)
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) { Fail("No model at " + path); continue; }
                if (importer.generateSecondaryUV) { Say(path + " already makes lightmap UVs"); continue; }

                importer.generateSecondaryUV = true;
                importer.SaveAndReimport();
                Did(path + ": Generate Lightmap UVs on, reimported");
            }
        }

        private static void LightmapStaticStepArt()
        {
            int set = 0;
            foreach (var filter in Object.FindObjectsOfType<MeshFilter>())
            {
                if (filter.sharedMesh == null || System.Array.IndexOf(LightmappedStepArt, AssetDatabase.GetAssetPath(filter.sharedMesh)) < 0) continue;

                var flags = GameObjectUtility.GetStaticEditorFlags(filter.gameObject);
                if (flags.HasFlag(StaticEditorFlags.LightmapStatic)) continue;

                GameObjectUtility.SetStaticEditorFlags(filter.gameObject, flags | StaticEditorFlags.LightmapStatic);
                set++;
            }
            Dirty();
            Did(set + " step-wall and step-counter pieces are Lightmap Static, so the bake paints the red onto them");
        }
    }
}
