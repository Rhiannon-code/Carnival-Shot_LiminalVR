using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    /// Step 70: small fixes from playing it (user, 2026-11-10). Each value changes only while it's still at its old default,
    /// so anything tuned by hand since is left alone
    public static partial class RangeSetup
    {
        // 0.5 at first, then 0.25 (still far too big for a bat: the puff is sized to cover a sign), now 0.1
        private static readonly float[] OldBatPops = { 0.5f, 0.25f };
        private const float BatPop = 0.1f;
        private const float OldGlassVolume = 0.7f;
        private const float GlassVolume = 0.35f;
        // A worm squeal pitched up into a rising tone: it sounded like a video game's one-up chime
        private const string ChimeClip = "zombie_hand_04";
        // The sign rim went too faint to see at 0.08 / 1.2 cm (user, 2026-11-10: "we lost the rim light")
        private const float OldRimStrength = 0.08f, RimStrength = 0.15f;
        private const float OldRimBorder = 0.012f, RimBorder = 0.015f;
        // Ducked to a quarter, the lights' hum stood out (user, 2026-11-10)
        private const float OldEndDuck = 0.25f, EndDuck = 0.6f;

        [MenuItem("Shooting Range/Setup/70 - Fixes From Playing", false, 93)]
        public static void TweaksMenu()
        {
            Begin("Feedback tweaks");
            SmallerBatSmoke();
            QuieterGlass();
            NoChime();
            RimBack();
            GentlerDuck();
            End();
        }

        private static void SmallerBatSmoke()
        {
            var swarm = Object.FindObjectOfType<BatSwarm>();
            if (swarm == null) { Fail("No BatSwarm in the open scene"); return; }

            var fields = new Fields(swarm);
            float was = fields.Float("popScale");
            if (!System.Array.Exists(OldBatPops, old => Mathf.Approximately(old, was))) { Say("Bat smoke is " + was + ", left alone"); return; }
            fields.Set("popScale", BatPop).Apply();
            Did("Bat smoke " + was + " -> " + BatPop);
            Dirty();
        }

        /// Every glass shatter, in the scene and on the room prefabs
        private static void QuieterGlass()
        {
            int changed = 0;
            foreach (var shatter in Object.FindObjectsOfType<ShatterOnShot>()) changed += Quieter(shatter);
            if (changed > 0) Dirty();

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { RoomTargetDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int here = 0;
                    foreach (var shatter in root.GetComponentsInChildren<ShatterOnShot>(true)) here += Quieter(shatter);
                    if (here > 0) { PrefabUtility.SaveAsPrefabAsset(root, path); changed += here; }
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            if (changed > 0) Did(changed + " glass shatter(s) " + OldGlassVolume + " -> " + GlassVolume);
            else Say("No glass shatter at " + OldGlassVolume + ", left alone");
        }

        private static int Quieter(ShatterOnShot shatter)
        {
            var fields = new Fields(shatter);
            if (!Mathf.Approximately(fields.Float("shatterVolume"), OldGlassVolume) || !PlaysGlass(fields)) return 0;
            fields.Set("shatterVolume", GlassVolume).Apply();
            return 1;
        }

        private static bool PlaysGlass(Fields fields)
        {
            var clips = fields.Find("shatterClips");
            for (int i = 0; i < clips.arraySize; i++)
            {
                var clip = clips.GetArrayElementAtIndex(i).objectReferenceValue;
                if (clip != null && clip.name.ToLowerInvariant().Contains("glass")) return true;
            }
            return false;
        }

        /// Takes the chime out of the zombie hand's voice on the sign prefab; its three groans stay
        private static void NoChime()
        {
            const string path = "Assets/Prefabs/ShootingRange/TrackMover.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int removed = 0;
                foreach (var voice in root.GetComponentsInChildren<SignVoice>(true))
                {
                    var fields = new Fields(voice);
                    var clips = fields.Find("clips");
                    var kept = new List<Object>();
                    for (int i = 0; i < clips.arraySize; i++)
                    {
                        var clip = clips.GetArrayElementAtIndex(i).objectReferenceValue;
                        if (clip != null && clip.name == ChimeClip) removed++;
                        else kept.Add(clip);
                    }
                    if (kept.Count != clips.arraySize) fields.SetArray("clips", kept).Apply();
                }

                if (removed > 0) { PrefabUtility.SaveAsPrefabAsset(root, path); Did("Zombie hand: " + ChimeClip + " (the chime) taken out of its voice"); }
                else Say("No " + ChimeClip + " on the signs, left alone");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void RimBack()
        {
            const string path = "Assets/Prefabs/ShootingRange/TrackMover.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var target = root.GetComponentInChildren<PopUpTarget>(true);
                if (target == null) { Fail("No PopUpTarget on " + path); return; }

                var fields = new Fields(target);
                bool strength = Mathf.Approximately(fields.Float("rimStrength"), OldRimStrength);
                bool border = Mathf.Approximately(fields.Float("rimBorder"), OldRimBorder);
                if (!strength && !border) { Say("Sign rim is " + fields.Float("rimStrength") + ", left alone"); return; }

                if (strength) fields.Set("rimStrength", RimStrength);
                if (border) fields.Set("rimBorder", RimBorder);
                fields.Apply();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Did("Sign rim back to " + RimStrength + " strength, " + RimBorder * 100f + " cm border");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void GentlerDuck()
        {
            var music = Object.FindObjectOfType<MusicBed>();
            if (music == null) { Fail("No MusicBed in the open scene"); return; }

            var fields = new Fields(music);
            if (!Mathf.Approximately(fields.Float("endDuck"), OldEndDuck)) { Say("End duck is " + fields.Float("endDuck") + ", left alone"); return; }
            fields.Set("endDuck", EndDuck).Apply();
            Did("Music at the end ducks to " + EndDuck + " instead of " + OldEndDuck + ", so the hum stays in the background");
            Dirty();
        }
    }
}
