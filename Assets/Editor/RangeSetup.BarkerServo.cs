using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    public static partial class RangeSetup
    {
        private const string ServoSounds = "Assets/Sounds/ShootingRange/World";

        [MenuItem("Shooting Range/Setup/75 - Barker Servo Sounds", false, 98)]
        public static void BarkerServoMenu()
        {
            Begin("Barker servo sounds");
            var barker = Object.FindObjectOfType<SkeletonBarker>();
            var whirs = Clips(ServoSounds, "barker_whir_");
            var stops = Clips(ServoSounds, "barker_servo_stop_");

            if (barker == null) Fail("No SkeletonBarker in the scene");
            else if (whirs.Count == 0 || stops.Count == 0) Fail("Clips missing under " + ServoSounds + ". Let Unity import them, then run this again");
            else
            {
                new Fields(barker).SetArray("whirClips", whirs).SetArray("stopClips", stops).Apply();
                Did(barker.name + ": " + whirs.Count + " servo whirs and a soft stop, no clunk on each hitch");
                Dirty();
            }
            End();
        }
    }
}
