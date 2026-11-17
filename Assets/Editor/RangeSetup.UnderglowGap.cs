using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    /// Step 87: one more red light at the right end of the bottom tier (user, 2026-11-13: "a gap in the red lighting on the
    /// bottom right"). The user's three hand-placed lights there stop about 2.2 m short of the glow strip's right end. A copy of
    /// the rightmost, so it bakes the same, one spacing further on. Fills blanks: does nothing once a light is there
    public static partial class RangeSetup
    {
        // Lights within this of each other's height are one tier
        private const float UnderglowTierTolerance = 0.1f;

        [MenuItem("Shooting Range/Setup/87 - Fill The Bottom Underglow Gap", false, 111)]
        public static void FillTheUnderglowGapMenu()
        {
            if (Lightmapping.isRunning) { Debug.LogWarning("[RangeSetup] A bake is running. Stop it with 39c first"); return; }

            Begin("Bottom underglow gap");
            try { FillTheUnderglowGap(); }
            catch (System.Exception e) { Fail("Stopped by an error: " + e); }
            finally { End(); }
        }

        private static void FillTheUnderglowGap()
        {
            var underglow = RangeSetupUtil.FindInScene("Underglow");
            if (underglow == null) { Fail("No Underglow in the scene"); return; }

            var lights = underglow.GetComponentsInChildren<Light>(true);
            if (lights.Length == 0) { Fail("No lights under Underglow"); return; }

            float lowest = lights.Min(l => l.transform.position.y);
            var tier = lights.Where(l => l.transform.position.y - lowest < UnderglowTierTolerance)
                             .OrderBy(l => l.transform.position.x).ToArray();
            if (tier.Length < 2) { Fail("Fewer than 2 lights on the bottom tier, so there is no spacing to follow"); return; }

            var last = tier[tier.Length - 1];
            float spacing = last.transform.position.x - tier[tier.Length - 2].transform.position.x;
            var at = last.transform.position + Vector3.right * spacing;

            if (lights.Any(l => Vector3.Distance(l.transform.position, at) < spacing * 0.5f))
            {
                Say("A light is already near " + at.ToString("0.00") + ", left as it is");
                return;
            }

            var added = Object.Instantiate(last.gameObject, last.transform.parent);
            added.name = last.name;
            added.transform.position = at;
            Undo.RegisterCreatedObjectUndo(added, "Fill the bottom underglow gap");
            Dirty();

            Did("Bottom tier: a copy of " + last.name + " at " + at.ToString("0.00") + ", " + spacing.ToString("0.00") +
                " m on from the rightmost, range " + last.range + ". " + tier.Length + " were there; the others are untouched");
            Say("Next: 39 to bake it in");
        }
    }
}
