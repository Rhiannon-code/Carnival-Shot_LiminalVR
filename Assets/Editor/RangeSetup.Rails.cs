using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    /// Step 30: fits the rails to the steps and pillars that came in with Alex's room (ADR 0063)
    public static partial class RangeSetup
    {
        private static readonly string[] UpperTiers =
            { "Floor_Across_1", "Floor_Across_1 (1)", "Floor_Across_2", "Floor_Across_2 (1)" };

        // Side rail, then the pillar the merge left it without (notes/merge-alex-scene-2026-10-27.md)
        private static readonly string[,] SidePillars = { { "Riser_L_0", "pillar (1)" }, { "Riser_R_0", "pillar (4)" } };

        private const string StepName = "step";
        private const float FoldClearance = 0.02f;
        private const float RoofGap = 0.35f;

        [MenuItem("Shooting Range/Setup/30 - Fit The Rails To The Steps (measure only)", false, 52)]
        public static void FitTheRailsMeasureMenu()
        {
            Begin("Rails against the new steps, measured only, nothing moved");
            FitTheRailsToTheSteps(false);
            End();
        }

        [MenuItem("Shooting Range/Setup/30b - Fit The Rails To The Steps", false, 53)]
        public static void FitTheRailsMenu()
        {
            if (!EditorUtility.DisplayDialog("Fit the rails to the steps",
                    "This moves " + string.Join(", ", UpperTiers) + " up to the sight line over the steps in " +
                    "front of them, moves every roof rail to " + RoofGap + " m under the ceiling, caps how tall a " +
                    "sign those tiers carry, wires the new pillars into the side rails' empty mount slots, and " +
                    "DELETES RangeLighting/Underglow to lay it again from the moved rails. The scene is saved " +
                    "afterwards and the lighting needs baking again (27b). Run step 30 first to see the numbers.",
                    "Move them", "Cancel"))
                return;

            Begin("Rails against the new steps");
            FitTheRailsToTheSteps(true);
            End();
        }

        private static void FitTheRailsToTheSteps(bool apply)
        {
            var steps = StepRenderers();
            if (steps.Count == 0) { Fail("No objects called " + StepName + " in the scene, so nothing to measure against"); return; }

            var prefab = RangeSetupUtil.Load<GameObject>(PrefabPath);
            var target = prefab != null ? prefab.GetComponentInChildren<PopUpTarget>(true) : null;
            if (target == null) { Fail("No PopUpTarget on " + PrefabPath); return; }

            var targetFields = new Fields(target);
            var standModel = targetFields.Ref("stand") as Transform;
            float standLength = standModel != null ? PopUpTarget.LengthOf(standModel) : 0f;
            float mountGap = targetFields.Float("mountGap");

            var heights = SignHeights();
            if (heights.Count == 0) { Fail("No signs measured on the prefab. Run step 3"); return; }

            float roof = FitTheRoofRails(apply);
            bool moved = FitTheUpperTiers(steps, standLength, roof, heights, apply);
            WireThePillars(mountGap, apply);

            if (!apply) return;

            Dirty();
            if (moved) RelayTheUnderglow();
        }

        private static List<Renderer> StepRenderers()
        {
            var steps = new List<Renderer>();
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();

            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == StepName || root.name.StartsWith(StepName + " ("))
                    steps.AddRange(root.GetComponentsInChildren<MeshRenderer>());
            }

            return steps;
        }

        /// Measured down from the ceiling rather than added to the rail, so a second run moves nothing.
        /// Returns the lowest roof rail, the upper tiers' limit only where no ceiling is found above them.
        /// Tier signs reaching into the roof signs' band is how the range was before the merge too
        private static float FitTheRoofRails(bool apply)
        {
            float lowest = float.PositiveInfinity;

            foreach (var rail in Object.FindObjectsOfType<TrackRail>())
            {
                if (rail.Group != TrackGroup.RoofAcross && rail.Group != TrackGroup.RoofAlong) continue;

                float ceiling = RoomSurface(rail.PointAt(rail.Length * 0.5f), Vector3.up, float.NaN);
                if (float.IsNaN(ceiling))
                {
                    Fail(rail.name + ": no ceiling collider above it, so it was left where it is");
                    lowest = Mathf.Min(lowest, rail.Origin.y);
                    continue;
                }

                float y = ceiling - RoofGap;
                MoveRail(rail, y, "ceiling underside " + ceiling.ToString("0.00"), apply);
                lowest = Mathf.Min(lowest, y);
            }

            return lowest;
        }

        /// A folded sign lies at its rail plus its stand, so the rail goes that far under the
        /// sight line: folded it is out of sight, up it is all in view. No cover is wired, because a wired
        /// cover would put every sign only a peek over the step (ADR 0038)
        private static bool FitTheUpperTiers(List<Renderer> steps, float standLength, float roof,
                                             Dictionary<string, float> heights, bool apply)
        {
            var eye = EyePoint();
            float shortest = float.MaxValue;
            float tallest = 0f;
            foreach (var height in heights.Values)
            {
                shortest = Mathf.Min(shortest, height);
                tallest = Mathf.Max(tallest, height);
            }

            bool moved = false;

            foreach (var name in UpperTiers)
            {
                var found = RangeSetupUtil.FindInScene(name);
                var rail = found != null ? found.GetComponent<TrackRail>() : null;
                if (rail == null) { Fail("No rail called " + name); continue; }

                Renderer blocker;
                float line = LineOverSteps(rail, steps, eye, out blocker);
                if (float.IsNegativeInfinity(line)) { Say(name + ": no step stands between it and the player, left alone"); continue; }

                float y = line - standLength - FoldClearance;

                var middle = rail.PointAt(rail.Length * 0.5f);
                float ceiling = RoomSurface(new Vector3(middle.x, y + standLength, middle.z), Vector3.up, float.NaN);
                float limit = float.IsNaN(ceiling) ? roof : ceiling - CeilingClearance;
                float cap = limit - y - standLength;

                if (cap < shortest)
                {
                    Fail(name + ": at y " + y.ToString("0.00") + " even the shortest sign (" + shortest.ToString("0.00") +
                         " m) would reach past " + limit.ToString("0.00") + ", the roof rails or the ceiling. Left where it is");
                    continue;
                }

                moved |= MoveRail(rail, y, "sight line " + line.ToString("0.00") + " over " + blocker.transform.root.name, apply);

                float tallestSign = cap >= tallest ? 0f : Mathf.Floor(cap * 100f) / 100f;
                if (apply) new Fields(rail).Set("tallestSign", tallestSign).Apply();
                Say("   tallest sign " + (tallestSign > 0f
                    ? tallestSign.ToString("0.00") + " m (" + NamesUnder(heights, tallestSign) + ")"
                    : "any, every sign fits under " + limit.ToString("0.00")));
            }

            return moved;
        }

        private static float LineOverSteps(TrackRail rail, List<Renderer> steps, Vector3 eye, out Renderer blocker)
        {
            float best = float.NegativeInfinity;
            blocker = null;

            for (int i = 0; i <= Samples; i++)
            {
                Vector3 point = rail.PointAt(rail.Length * i / (float)Samples);
                foreach (var step in steps)
                {
                    float line = LineOverCover(step, point, eye);
                    if (line <= best) continue;

                    best = line;
                    blocker = step;
                }
            }

            return best;
        }

        private static bool MoveRail(TrackRail rail, float y, string why, bool apply)
        {
            float from = rail.Origin.y;
            if (Mathf.Abs(from - y) < 0.005f) { Say(rail.name + ": already at y " + y.ToString("0.00") + " (" + why + ")"); return false; }

            string line = rail.name + ": y " + from.ToString("0.00") + " -> " + y.ToString("0.00") + " (" + why + ")";
            if (!apply) { Say("would move " + line); return false; }

            var position = rail.transform.position;
            position.y = y;
            rail.transform.position = position;
            Did(line);
            return true;
        }

        /// Fills an empty mount slot only, and says where the side signs will hinge and whether the pillar
        /// reaches down as far as the rail does
        private static void WireThePillars(float mountGap, bool apply)
        {
            for (int i = 0; i < SidePillars.GetLength(0); i++)
            {
                var railObject = RangeSetupUtil.FindInScene(SidePillars[i, 0]);
                var pillarObject = RangeSetupUtil.FindInScene(SidePillars[i, 1]);
                var rail = railObject != null ? railObject.GetComponent<TrackRail>() : null;
                var pillar = pillarObject != null ? pillarObject.GetComponentInChildren<MeshRenderer>() : null;
                if (rail == null || pillar == null) { Fail("Missing " + SidePillars[i, 0] + " or " + SidePillars[i, 1]); continue; }

                var fields = new Fields(rail);
                var mount = fields.Find("mount");
                int empty = -1;
                bool wired = false;
                for (int slot = 0; slot < mount.arraySize; slot++)
                {
                    var part = mount.GetArrayElementAtIndex(slot).objectReferenceValue;
                    if (part == pillar) wired = true;
                    if (part == null && empty < 0) empty = slot;
                }

                if (wired) Say(rail.name + ": already mounted on " + pillarObject.name);
                else if (empty < 0) Say(rail.name + ": no empty mount slot, left alone");
                else if (!apply) Say("would mount " + rail.name + " on " + pillarObject.name);
                else
                {
                    mount.GetArrayElementAtIndex(empty).objectReferenceValue = pillar;
                    fields.Apply();
                    Did(rail.name + ": mounted on " + pillarObject.name);
                }

                var bounds = pillar.bounds;
                var door = PopUpTarget.DoorEdge(bounds, EyePoint(), mountGap);
                float low = Mathf.Min(rail.PointAt(0f).y, rail.PointAt(rail.Length).y);
                float high = Mathf.Max(rail.PointAt(0f).y, rail.PointAt(rail.Length).y);

                Say("   its signs hinge at x " + door.x.ToString("0.00") + ", z " + door.z.ToString("0.00") +
                    ", from y " + low.ToString("0.00") + " to " + high.ToString("0.00"));
                if (bounds.min.y > low + 0.05f)
                    Say("   ^ " + pillarObject.name + " only starts at y " + bounds.min.y.ToString("0.00") +
                        ", so a side sign lower than that has no pillar behind it");
            }
        }

        /// The underglow is laid once from the rails and then left alone, so after a move it is laid again
        private static void RelayTheUnderglow()
        {
            var lighting = RangeSetupUtil.FindInScene(LightingName);
            var glow = lighting != null ? RangeSetupUtil.Child(lighting.transform, "Underglow") : null;
            if (glow == null) return;

            Object.DestroyImmediate(glow.gameObject);
            Did("Deleted " + LightingName + "/Underglow to lay it again from the moved rails");
            LayTheLights();
            Say("Bake the lighting again (27b): the underglow moved with the rails");
        }
    }
}
