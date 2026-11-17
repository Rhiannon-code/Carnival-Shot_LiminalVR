using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    /// Step 80: finds where the room is open to the outside round the tent ceiling (user, 2026-11-13: "gaps in the
    /// wall/ceiling"). Rays from inside the room to each wall and straight up; any that reach open space are a gap.
    /// Reads only; nothing in the scene changes
    public static partial class RangeSetup
    {
        private const float GapStep = 0.5f;
        private const float GapHeightStep = 0.1f;
        private const float GapLow = 3.0f;
        private const float GapHigh = 5.3f;
        private const float GapReach = 25f;
        // The room's middle line, inside every wall and pillar (step 77)
        private const float RoomMiddleX = 0.2f;
        private const float RoomMiddleZ = 5f;
        private const float RoomFrontZ = -2.9f;
        private const float RoomBackZ = 13.2f;
        private const float RoomLeftX = -6.2f;
        private const float RoomRightX = 6.6f;

        [MenuItem("Shooting Range/Setup/80 - Find Gaps Round The Tent (measure only)", false, 102)]
        public static void FindTentGapsMenu()
        {
            Begin("Gaps round the tent ceiling, measured only, nothing changed");
            bool backfaces = Physics.queriesHitBackfaces;
            try
            {
                // The tent's skirt and canvas are single-sided; a ray from the wrong side would read as a gap
                Physics.queriesHitBackfaces = true;
                FindTentGaps();
            }
            catch (System.Exception e) { Fail("Stopped by an error: " + e); }
            finally
            {
                Physics.queriesHitBackfaces = backfaces;
                End();
            }
        }

        private static void FindTentGaps()
        {
            int gaps = 0;
            gaps += SideGaps("Left wall (−x)", Vector3.left, false, RoomFrontZ, RoomBackZ);
            gaps += SideGaps("Right wall (+x)", Vector3.right, false, RoomFrontZ, RoomBackZ);
            gaps += SideGaps("Front wall (−z)", Vector3.back, true, RoomLeftX, RoomRightX);
            gaps += SideGaps("Back wall (+z)", Vector3.forward, true, RoomLeftX, RoomRightX);

            var open = new List<string>();
            for (float x = RoomLeftX; x <= RoomRightX; x += GapStep)
                for (float z = RoomFrontZ; z <= RoomBackZ; z += GapStep)
                    if (!Solid(new Vector3(x, GapLow, z), Vector3.up)) open.Add("(" + x.ToString("0.0") + ", " + z.ToString("0.0") + ")");

            if (open.Count > 0)
            {
                gaps += open.Count;
                Fail("Open to the sky at x, z: " + string.Join(" ", open.ToArray()) + "  (the skylight openings are meant to be, if their boards are off)");
            }
            else Say("Ceiling: closed everywhere");

            if (gaps == 0) Did("No gaps found round the tent");
        }

        /// One wall: rays from the room's middle line out to it, along its length and up its height. Reports each run of
        /// heights that reaches open space, joined along the wall
        private static int SideGaps(string label, Vector3 toward, bool alongX, float from, float to)
        {
            var runs = new List<string>();
            string current = null;
            float runStart = 0f, runEnd = 0f;

            for (float along = from; along <= to + 0.001f; along += GapStep)
            {
                float low = float.NaN, high = float.NaN;
                for (float y = GapLow; y <= GapHigh + 0.001f; y += GapHeightStep)
                {
                    var origin = alongX ? new Vector3(along, y, RoomMiddleZ) : new Vector3(RoomMiddleX, y, along);
                    if (Solid(origin, toward)) continue;
                    if (float.IsNaN(low)) low = y;
                    high = y;
                }

                string heights = float.IsNaN(low) ? null : "y " + low.ToString("0.0") + "-" + high.ToString("0.0");
                if (heights == current && heights != null) { runEnd = along; continue; }

                if (current != null) runs.Add((alongX ? "x " : "z ") + runStart.ToString("0.0") + " to " + runEnd.ToString("0.0") + ", " + current);
                current = heights;
                runStart = runEnd = along;
            }
            if (current != null) runs.Add((alongX ? "x " : "z ") + runStart.ToString("0.0") + " to " + runEnd.ToString("0.0") + ", " + current);

            if (runs.Count == 0) { Say(label + ": closed"); return 0; }

            foreach (var run in runs) Fail(label + " open at " + run);
            return runs.Count;
        }

        private static bool Solid(Vector3 origin, Vector3 direction)
        {
            foreach (var hit in Physics.RaycastAll(origin, direction, GapReach, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.rigidbody != null || hit.collider.GetComponentInParent<IShootable>() != null ||
                    hit.collider.GetComponentInParent<TrackMover>() != null) continue;
                return true;
            }
            return false;
        }
    }
}
