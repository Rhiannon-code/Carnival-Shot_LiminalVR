using UnityEditor;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    public static partial class RangeSetup
    {
        [MenuItem("Shooting Range/Setup/76 - Splintering Sign Hits", false, 99)]
        public static void SignHitsMenu()
        {
            Begin("Splintering sign hits");
            var hits = Clips(RangeSounds + "/World", "sign_hit_0");
            var prefab = hits.Count > 0 ? OpenPrefab() : null;
            var target = prefab != null ? prefab.GetComponent<PopUpTarget>() : null;

            if (hits.Count == 0) Fail("No sign_hit clips. Let Unity import them, then run this again");
            else if (target == null) { Fail("No PopUpTarget on " + PrefabPath); ClosePrefab(false); }
            else
            {
                new Fields(target).SetArray("hitClips", hits).Apply();
                ClosePrefab(true);
                Did("TrackMover.prefab: " + hits.Count + " splintering wood hits, one at random per hit");
            }
            End();
        }
    }
}
