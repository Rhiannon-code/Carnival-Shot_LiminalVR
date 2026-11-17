using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    /// Step 82: for everything in the range room, what a shot does when it hits it (user, 2026-11-13: check Alex's new pieces
    /// for bullet holes and impact sounds). The same tests ImpactFX makes: a collider to hit, one that fits the mesh (a hole
    /// sits on the collider), its PhysicMaterial in ImpactFX's surfaces (sound and debris), and its object's name in
    /// holeSurfaces (bullet holes). Reads only; nothing in the scene changes
    public static partial class RangeSetup
    {
        // Alex's pieces from the 2026-11-13 merge (notes/merge-alex-scene-2026-11-13.md), and the tent
        private static readonly string[] NewPieces =
        {
            "lantern", "corner_skirt", "skirt", "step-counter", "step-wall", "wall_bats", "wood_corner", "wood_wall1", "wood_wall2",
            "wood_beam", "Canvas", "Valance",
        };
        private const float ColliderFits = 0.05f;
        // The range room, ceiling included (step 77's walls, 2026-11-13)
        private static readonly Bounds ShotRoom = new Bounds(new Vector3(0.25f, 2.45f, 5.2f), new Vector3(13.5f, 5.9f, 16.8f));

        [MenuItem("Shooting Range/Setup/82 - Check Shot Surfaces (measure only)", false, 105)]
        public static void CheckShotSurfacesMenu()
        {
            Begin("Shot surfaces in the range room, measured only, nothing changed");
            try { CheckShotSurfaces(); }
            catch (System.Exception e) { Fail("Stopped by an error: " + e); }
            finally { End(); }
        }

        private static void CheckShotSurfaces()
        {
            var impact = Object.FindObjectOfType<ImpactFX>();
            if (impact == null) { Fail("No Impact FX in the scene"); return; }

            var fields = new SerializedObject(impact);
            var known = new HashSet<Object>();
            var surfaces = fields.FindProperty("surfaces");
            for (int i = 0; i < surfaces.arraySize; i++) known.Add(surfaces.GetArrayElementAtIndex(i).FindPropertyRelative("material").objectReferenceValue);
            var holes = new HashSet<string>();
            var holeNames = fields.FindProperty("holeSurfaces");
            for (int i = 0; i < holeNames.arraySize; i++) holes.Add(holeNames.GetArrayElementAtIndex(i).stringValue);
            Say("Impact FX knows " + known.Count + " surface materials; holes go on: " + string.Join(", ", new List<string>(holes).ToArray()));

            var lines = new SortedDictionary<string, List<string>>();

            foreach (var renderer in Object.FindObjectsOfType<MeshRenderer>())
            {
                if (!renderer.enabled || !ShotRoom.Contains(renderer.bounds.center) || Moves(renderer.transform)) continue;

                string name = BaseName(renderer.name);
                // Not ??: in the Editor a missing component comes back as a stand-in that only Unity's == treats as null
                var collider = renderer.GetComponent<Collider>();
                if (collider == null) collider = renderer.GetComponentInParent<Collider>();
                string verdict;
                // Where it is, so each can be found and decided on (user, 2026-11-13: "ask me per prop")
                if (collider == null) verdict = "NO COLLIDER: shots go through, no sound, no hole; at " + renderer.bounds.center.ToString("F1") +
                                                " under '" + renderer.transform.root.name + "'";
                else
                {
                    var r = renderer.bounds;
                    var c = collider.bounds;
                    float off = Mathf.Max((r.min - c.min).magnitude, (r.max - c.max).magnitude);
                    var material = collider.sharedMaterial;
                    verdict = collider.GetType().Name + " on '" + collider.name + "'" +
                              (off > ColliderFits ? ", DOES NOT FIT the mesh by " + off.ToString("0.00") + " m (holes float)" : ", fits") +
                              ", " + (material == null ? "no PhysicMaterial: default thud, no debris"
                                     : known.Contains(material) ? material.name + ": its own sound and debris"
                                     : material.name + " NOT in Impact FX: default thud, no debris") +
                              ", " + (holes.Contains(BaseName(collider.name)) ? "takes holes" : "no holes");
                }

                List<string> group;
                if (!lines.TryGetValue(name, out group)) lines[name] = group = new List<string>();
                group.Add(verdict);
            }

            foreach (var pair in lines)
            {
                var counts = new Dictionary<string, int>();
                foreach (var verdict in pair.Value) counts[verdict] = counts.ContainsKey(verdict) ? counts[verdict] + 1 : 1;
                bool isNew = System.Array.IndexOf(NewPieces, pair.Key) >= 0;
                foreach (var count in counts)
                {
                    string line = (isNew ? "[new] " : "") + pair.Key + " x" + count.Value + ": " + count.Key;
                    if (count.Key.Contains("NO COLLIDER") || count.Key.Contains("DOES NOT FIT") || count.Key.Contains("NOT in")) Fail(line);
                    else Say(line);
                }
            }
        }

        private static string BaseName(string name)
        {
            int copy = name.IndexOf(" (", System.StringComparison.Ordinal);
            return copy >= 0 ? name.Substring(0, copy) : name;
        }

        /// Signs, props that fly, guns, string lights, UI and effects aren't room surfaces
        private static bool Moves(Transform t)
        {
            return t.GetComponentInParent<TrackMover>() != null || t.GetComponentInParent<Rigidbody>() != null ||
                   t.GetComponentInParent<IShootable>() != null || t.GetComponentInParent<Pistol>() != null ||
                   t.GetComponentInParent<LineRenderer>() != null || t.name == "Bulbs" || NotASurface(t);
        }

        /// The player's UI, the fog and the glow strips: nothing a shot should ever land on
        private static bool NotASurface(Transform t)
        {
            string root = t.root.name;
            return root == "[ExperienceApp]" || root == "GroundFog" || t.name.StartsWith("GlowStrip");
        }
    }
}
