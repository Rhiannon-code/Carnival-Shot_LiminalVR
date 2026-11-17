using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    public static partial class RangeSetup
    {
        private static readonly string[] UpperTiers =
            { "Floor_Across_1", "Floor_Across_1 (1)", "Floor_Across_2", "Floor_Across_2 (1)" };

        // Side rail, then the pillar the merge left it without (notes/merge-alex-scene-2026-10-27.md)
        private static readonly string[,] SidePillars = { { "Riser_L_0", "pillar (1)" }, { "Riser_R_0", "pillar (4)" } };

        // Alex's step art (merge of 2026-11-13); the old "step" boxes under Environment/Steps are not what the player sees
        private static readonly string[] StepNames = { "step-counter", "step-wall" };
        private const float FoldClearance = 0.02f;
        private const float RoofGap = 0.35f;
        // Either side of a lane that counts as above it: a sign is a flat board on the rail, plus a hand's width
        private const float LaneMargin = 0.15f;
        // Halfway between seated and standing (user, 2026-11-13), so a seated player loses only a few cm at the back
        private const float FitEyeHeight = 1.45f;
        // For comparison in the measure-only report: seated (an adult in a chair, about 1.2 m, INFERRED) and standing
        private static readonly float[] OtherEyeHeights = { 1.2f, 1.7f };

        [MenuItem("Shooting Range/Setup/30 - Fit The Rails To The Steps (measure only)", false, 52)]
        public static void FitTheRailsMeasureMenu()
        {
            Begin("Rails against the new steps, measured only, nothing moved");
            FitTheRailsSafely(false);
        }

        [MenuItem("Shooting Range/Setup/30b - Fit The Rails To The Steps", false, 53)]
        public static void FitTheRailsMenu()
        {
            if (!EditorUtility.DisplayDialog("Fit the rails to the steps",
                    "This moves " + string.Join(", ", UpperTiers) + " up to the sight line over the steps in " +
                    "front of them, moves every roof rail to " + RoofGap + " m under the ceiling, caps how tall a " +
                    "sign those tiers carry, and wires the new pillars into the side rails' empty mount slots. The " +
                    "underglow lights are left where they are. The scene is saved afterwards and the lighting needs " +
                    "baking again (27b). Run step 30 first to see the numbers.",
                    "Move them", "Cancel"))
                return;

            Begin("Rails against the new steps");
            FitTheRailsSafely(true);
        }

        /// The report always prints, with whatever went wrong in it, so a failure can't pass as silence
        private static void FitTheRailsSafely(bool apply)
        {
            try { FitTheRailsToTheSteps(apply); }
            catch (System.Exception e) { Fail("Stopped by an error, so nothing after this point ran: " + e); }
            finally { End(); }
        }

        private static void FitTheRailsToTheSteps(bool apply)
        {
            var steps = StepRenderers();
            if (steps.Count == 0) { Fail("No objects called " + string.Join(" or ", StepNames) + " in the scene, so nothing to measure against"); return; }

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
            if (moved) Say("The underglow lights were placed by hand (2026-11-13), so they stay where they are. Check them " +
                           "against the moved rails, then bake the lighting again (27b)");
        }

        /// Anywhere in the scene, since the room is grouped under parents now
        private static List<Renderer> StepRenderers()
        {
            var steps = new List<Renderer>();

            foreach (var renderer in Object.FindObjectsOfType<MeshRenderer>())
            {
                for (var holder = renderer.transform; holder != null; holder = holder.parent)
                {
                    if (!IsStep(holder.name)) continue;

                    steps.Add(renderer);
                    break;
                }
            }

            return steps;
        }

        private static bool IsStep(string name)
        {
            foreach (var step in StepNames)
            {
                if (name == step || name.StartsWith(step + " (")) return true;
            }

            return false;
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

        private static bool FitTheUpperTiers(List<Renderer> steps, float standLength, float roof,
                                             Dictionary<string, float> heights, bool apply)
        {
            var eye = EyePoint() + Vector3.down * (EyeHeight - FitEyeHeight);
            Say("Fitted for an eye " + FitEyeHeight + " m up where the player stands");
            float shortest = float.MaxValue;
            float tallest = 0f;
            foreach (var height in heights.Values)
            {
                shortest = Mathf.Min(shortest, height);
                tallest = Mathf.Max(tallest, height);
            }

            var lines = new Dictionary<string, float>();
            var blockers = new Dictionary<string, Renderer>();
            foreach (var name in UpperTiers)
            {
                var found = RangeSetupUtil.FindInScene(name);
                var rail = found != null ? found.GetComponent<TrackRail>() : null;
                if (rail == null) { Fail("No rail called " + name); continue; }

                Renderer blocker;
                float line = LineOverSteps(rail, steps, eye, out blocker);
                if (float.IsNegativeInfinity(line)) { Say(name + ": no step stands between it and the player, left alone"); continue; }

                lines[name] = line;
                blockers[name] = blocker;
            }

            bool moved = false;

            foreach (var name in lines.Keys)
            {
                var rail = RangeSetupUtil.FindInScene(name).GetComponent<TrackRail>();
                var blocker = blockers[name];
                float line = lines[name];

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

                if (!apply)
                {
                    foreach (var height in OtherEyeHeights) SayOtherEye(height, rail, steps, eye, standLength, limit, shortest, heights);
                    SayVisibleRoom(rail, y, standLength, heights);
                }
            }

            return moved;
        }

        /// Measure only: where the rail would go for another eye height (user, 2026-11-13: back row hidden from a chair),
        /// and which signs would still fit under the roof there. Moves nothing
        private static void SayOtherEye(float height, TrackRail rail, List<Renderer> steps, Vector3 eye, float standLength,
                                        float limit, float shortest, Dictionary<string, float> heights)
        {
            Renderer blocker;
            var other = eye + Vector3.up * (height - FitEyeHeight);
            float line = LineOverSteps(rail, steps, other, out blocker);
            if (float.IsNegativeInfinity(line)) { Say("   eye " + height + " m: no step in the way"); return; }

            float y = line - standLength - FoldClearance;
            float cap = limit - y - standLength;
            Say("   eye " + height + " m: rail at y " + y.ToString("0.00") + " (" +
                (y - rail.Origin.y).ToString("+0.00;-0.00") + "), " + (cap < shortest
                    ? "no sign fits under " + limit.ToString("0.00")
                    : "signs up to " + cap.ToString("0.00") + " m fit (" + NamesUnder(heights, cap) + ")"));
        }

        /// Measure only: the lowest mesh the player can see above a lane, collider or not (user, 2026-11-13: the back row
        /// clipped the ceiling the collider check had cleared). Renderer bounds, so a slanted piece reads lower than it is
        private static void SayVisibleRoom(TrackRail rail, float y, float standLength, Dictionary<string, float> heights)
        {
            float feet = y + standLength;
            var lane = new Bounds(rail.PointAt(0f), Vector3.zero);
            lane.Encapsulate(rail.PointAt(rail.Length));
            lane.Expand(new Vector3(LaneMargin, 0f, LaneMargin) * 2f);

            Renderer lowest = null;
            float under = float.PositiveInfinity;
            foreach (var renderer in Object.FindObjectsOfType<MeshRenderer>())
            {
                if (!renderer.enabled || renderer.GetComponentInParent<TrackMover>() != null) continue;

                var bounds = renderer.bounds;
                if (bounds.min.y <= feet || bounds.min.y >= under) continue;
                if (bounds.max.x < lane.min.x || bounds.min.x > lane.max.x || bounds.max.z < lane.min.z || bounds.min.z > lane.max.z) continue;

                under = bounds.min.y;
                lowest = renderer;
            }

            if (lowest == null) { Say("   visible: nothing above it"); return; }

            float room = under - feet;
            Say("   visible: lowest above it is " + lowest.name + " (" + lowest.transform.root.name + ") at " + under.ToString("0.00") +
                ", leaving " + room.ToString("0.00") + " m over a sign's feet at " + feet.ToString("0.00") + " (fits: " +
                NamesUnder(heights, room) + ")");
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

        [MenuItem("Shooting Range/Setup/45 - Clear The Rail Boxes", false, 68)]
        public static void ClearTheRailBoxesMenu()
        {
            if (!EditorUtility.DisplayDialog("Clear the rail boxes",
                    "This unticks the solid box collider on every rail that has one. They are invisible, 1 m wide at each rail's " +
                    "origin, and stop rounds aimed at a sign there. Nothing is deleted. The scene is saved afterwards; no bake needed.",
                    "Untick them", "Cancel"))
                return;

            Begin("Rail boxes");
            int cleared = 0;
            foreach (var rail in Object.FindObjectsOfType<TrackRail>())
            {
                foreach (var box in rail.GetComponents<BoxCollider>())
                {
                    if (!box.enabled) continue;

                    box.enabled = false;
                    cleared++;
                    Did("Unticked the box collider on " + rail.name);
                }
            }

            if (cleared > 0) Dirty();
            else Say("No rail has a box collider switched on");
            End();
        }

    }
}
