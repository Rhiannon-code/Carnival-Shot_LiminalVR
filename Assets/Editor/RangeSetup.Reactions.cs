using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    /// Step 32: where a sign is hit picks what it does, and the bats a sign can burst into (ADR 0063)
    public static partial class RangeSetup
    {
        private const float RailClatterVolume = 0.35f;
        private const float RailClatterReach = 15f;

        [MenuItem("Shooting Range/Setup/32 - Add The Sign Reactions", false, 55)]
        public static void SignReactionsMenu() { Begin("Sign reactions"); AddTheSignReactions(); End(); }

        /// Fills blanks: Sign Reactions already on the prefab, or a BatBurst already in the scene, is left alone
        private static void AddTheSignReactions()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                bool changed = false;
                if (root.GetComponent<SignReactions>() != null) Say("The target prefab already has Sign Reactions, left alone");
                else
                {
                    root.AddComponent<SignReactions>();
                    changed = true;
                    Did("Sign Reactions added to " + PrefabPath + ": where a sign is hit picks what it does");
                }

                var mover = new Fields(root.GetComponent<TrackMover>());
                if (mover.Ref("clatter") != null) Say("The carriage already has its clatter, left alone");
                else
                {
                    var clatter = root.AddComponent<AudioSource>();
                    clatter.loop = true;
                    clatter.playOnAwake = false;
                    clatter.spatialBlend = 1f;
                    clatter.volume = RailClatterVolume;
                    clatter.rolloffMode = AudioRolloffMode.Linear;
                    clatter.maxDistance = RailClatterReach;
                    mover.Set("clatter", clatter).Apply();
                    changed = true;
                    Did("The carriage has a rail clatter source; its clip goes in with the sound pass");
                }

                if (changed) PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            if (Object.FindObjectOfType<BatBurst>() != null) { Say("BatBurst is already in the scene, left alone"); return; }

            var bat = RangeSetupUtil.Load<GameObject>(FlyingBatPrefab);
            var model = bat != null ? RangeSetupUtil.Child(bat.transform, "Model") : null;
            if (model == null) { Fail("No Model under " + FlyingBatPrefab + " to make the bats from"); return; }

            var burst = new GameObject("BatBurst").AddComponent<BatBurst>();
            new Fields(burst).Set("batModel", model.gameObject).Apply();
            Dirty();
            Did("BatBurst added to the scene, flying copies of " + FlyingBatPrefab + "'s Model");
        }
    }
}
