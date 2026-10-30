using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    /// Step 36: the sounds for everything ADR 0063 added, cut from the Ovani packs by range-audio-halloween.sh.
    /// Fills blanks: a slot that already has clips keeps them, so a hand-picked sound is never replaced
    public static partial class RangeSetup
    {
        private const string SoundsWorld = "Assets/Sounds/ShootingRange/World";
        private const string RoundPrefab = "Assets/Prefabs/ShootingRange/Round.prefab";

        [MenuItem("Shooting Range/Setup/36 - Wire The New Sounds", false, 59)]
        public static void NewSoundsMenu() { Begin("New sounds"); WireTheNewSounds(); End(); }

        private static void WireTheNewSounds()
        {
            WirePrefab(PrefabPath, root =>
            {
                var reactions = root.GetComponent<SignReactions>();
                int wired = Fill(reactions, "spinClips", "sign_whoosh") + Fill(reactions, "foldClips", "sign_slap") +
                            Fill(reactions, "flipClips", "sign_whoosh") + Fill(reactions, "slapClips", "sign_slap") +
                            Fill(reactions, "boingClips", "sign_boing") + Fill(reactions, "hangClips", "sign_creak") +
                            Fill(reactions, "letGoClips", "sign_snap") + Fill(reactions, "throwClips", "sign_throw") +
                            Fill(reactions, "batClips", "bat_launch");

                var mover = root.GetComponent<TrackMover>();
                wired += Fill(mover, "launchClips", "rail_clunk") + Fill(mover, "stopClips", "rail_squeal");

                var clatter = mover != null ? new Fields(mover).Ref("clatter") as AudioSource : null;
                if (clatter != null && clatter.clip == null)
                {
                    var loop = Clips(SoundsWorld, "rail_clatter_loop");
                    if (loop.Count > 0) { clatter.clip = (AudioClip)loop[0]; wired++; }
                }

                return wired;
            });

            WirePrefab(RoundPrefab, root => Fill(root.GetComponent<Projectile>(), "ricochetClips", "ricochet_ping"));
            WirePrefab(RoomTargetDir + "/Room_ArmourStand.prefab", root => Fill(root.GetComponent<SpinOnShot>(), "spinClips", "helmet_clang"));
            WirePrefab(RoomTargetDir + "/" + ArmouredStandName + ".prefab", root => Fill(root.GetComponent<SpinOnShot>(), "spinClips", "helmet_clang"));
            WirePrefab(RoomTargetDir + "/Room_PlushieBat.prefab", root => Fill(root.GetComponent<ReleaseOnShot>(), "hitClips", "plushie_squeak"));
            WirePrefab(RoomTargetDir + "/Room_Chest.prefab", root => Fill(root.GetComponentInChildren<PopOpenOnShot>(), "springClips", "chest_spring"));

            int scene = 0;
            foreach (var wobbler in Object.FindObjectsOfType<WobbleOnShot>()) scene += Fill(wobbler, "fallClips", "prop_thud");
            foreach (var skylight in Object.FindObjectsOfType<Skylight>())
                scene += Fill(skylight, "openClips", "skylight_break") + Fill(skylight, "sizzleClips", "sun_sizzle");

            if (scene > 0) { Dirty(); Did(scene + " sound slots filled in the scene"); }

            Say("No clip for a sign that gets away (Taunt Clips on the target prefab): nothing in the Ovani packs fits yet");
        }

        private static void WirePrefab(string path, System.Func<GameObject, int> wire)
        {
            if (RangeSetupUtil.Load<GameObject>(path) == null) { Say(path + " is not there, skipped"); return; }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int wired = wire(root);
                if (wired == 0) { Say(System.IO.Path.GetFileNameWithoutExtension(path) + ": nothing empty to fill"); return; }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Did(System.IO.Path.GetFileNameWithoutExtension(path) + ": " + wired + " sound slots filled");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /// 1 when the slot was empty and is now filled, 0 otherwise
        private static int Fill(Component target, string field, string prefix)
        {
            if (target == null) return 0;

            var fields = new Fields(target);
            if (fields.Find(field).arraySize > 0) return 0;

            var clips = Clips(SoundsWorld, prefix);
            if (clips.Count == 0) { Fail("No " + prefix + " clips in " + SoundsWorld + ". Run range-audio-halloween.sh"); return 0; }

            fields.SetArray(field, clips).Apply();
            return 1;
        }
    }
}
