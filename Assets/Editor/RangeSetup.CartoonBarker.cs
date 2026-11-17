using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    /// Step 89: the barker moves like a cartoon (user, 2026-11-13: "meant to be animated in a cartoony way"; chose springy
    /// everywhere). The scene still holds the animatronic servo's values, which outrank the script's new defaults: a 5%
    /// overshoot, and hitches that stall a move halfway with a clunk, which would break the snap
    public static partial class RangeSetup
    {
        private const float CartoonOvershoot = 0.25f;

        [MenuItem("Shooting Range/Setup/89 - Cartoony Barker", false, 113)]
        public static void CartoonyBarkerMenu()
        {
            Begin("Cartoony barker");
            var barker = Object.FindObjectOfType<SkeletonBarker>();
            if (barker == null) { Fail("No Skeleton Barker in the scene. Run step 64"); End(); return; }

            var fields = new Fields(barker).Set("overshoot", CartoonOvershoot);
            fields.Find("hitchesPerMove").vector2IntValue = Vector2Int.zero;
            fields.Apply();
            Dirty();
            Did("Every move winds up, snaps " + (CartoonOvershoot * 100f).ToString("0") + "% past its end and wobbles to rest; no " +
                "hitches. Tune the Cartoon Servo, Looking and In And Out values on the barker");
            End();
        }
    }
}
