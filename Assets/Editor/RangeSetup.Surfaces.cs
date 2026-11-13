using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    /// Step 34: what the room is made of, so a round thuds into wood, cracks on stone and clangs off metal (ADR 0063).
    /// ImpactFX picks its sound by the struck collider's physic material
    public static partial class RangeSetup
    {
        private const string SurfaceDir = "Assets/Materials/Surfaces";

        private struct SurfaceKind
        {
            public string Name;
            public string Clips;
            public string[] Words;
            public DebrisKind Debris;
            public bool Replaces;
        }

        // Checked in this order, so an armour stand is metal before its stand makes it wood. Replaces: these came after the
        // first three, so they take over from what a word like "stand" or "metal" gave the object (ADR 0064)
        private static readonly SurfaceKind[] SurfaceKinds =
        {
            new SurfaceKind { Name = "Flame", Clips = "env_metal_0", Debris = DebrisKind.Embers, Replaces = true,
                          Words = new[] { "torch", "candle" } },
            new SurfaceKind { Name = "Bone", Clips = "env_bone_0", Debris = DebrisKind.Chips, Replaces = true,
                          Words = new[] { "skull", "scull" } },
            new SurfaceKind { Name = "Ceramic", Clips = "env_ceramic_0", Debris = DebrisKind.Chips, Replaces = true,
                          Words = new[] { "vase", "flask" } },
            new SurfaceKind { Name = "Soft", Clips = "env_soft_0", Debris = DebrisKind.Stuffing, Replaces = true,
                          Words = new[] { "plush" } },
            new SurfaceKind { Name = "Metal", Clips = "env_metal_0", Debris = DebrisKind.Sparks,
                          Words = new[] { "armor", "armour", "helmet", "sword", "axe", "lamp", "metal", "iron", "chain" } },
            new SurfaceKind { Name = "Wood", Clips = "env_wood_0", Debris = DebrisKind.Splinters,
                          Words = new[] { "wood", "beam", "plank", "shelf", "barrel", "table", "crate", "box", "chest", "coffin",
                                          "step", "stand", "bench", "door", "stall", "mannequin", "board", "painting" } },
            new SurfaceKind { Name = "Stone", Clips = "env_stone_0", Debris = DebrisKind.Chips,
                          Words = new[] { "floor", "wall", "pillar", "roof", "brick", "stone", "castle", "tile", "arch", "column" } },
        };

        [MenuItem("Shooting Range/Setup/34 - Sort The Room Surfaces", false, 57)]
        public static void SurfacesMenu() { Begin("Room surfaces"); SortTheRoomSurfaces(); End(); }

        /// Fills blanks: a collider that already has a physic material keeps it, unless its kind Replaces. Named by its
        /// own object or the nearest parent whose name says what it is
        private static void SortTheRoomSurfaces()
        {
            var impact = Object.FindObjectOfType<ImpactFX>();
            if (impact == null) { Fail("No ImpactFX in the scene"); return; }

            System.IO.Directory.CreateDirectory(SurfaceDir);
            var materials = new PhysicMaterial[SurfaceKinds.Length];
            var surfaces = new SerializedObject(impact).FindProperty("surfaces");

            for (int i = 0; i < SurfaceKinds.Length; i++)
            {
                materials[i] = SurfaceMaterial(SurfaceKinds[i].Name);
                RegisterSurface(surfaces, materials[i], Clips(WorldSounds + SurfaceKinds[i].Clips, 4));
                SetSurfaceDebris(surfaces, materials[i].name, SurfaceKinds[i].Debris);
            }
            surfaces.serializedObject.ApplyModifiedPropertiesWithoutUndo();

            var counts = new int[SurfaceKinds.Length];
            var unsorted = new List<string>();

            foreach (var collider in Object.FindObjectsOfType<Collider>())
            {
                if (collider.isTrigger) continue;
                if (collider.GetComponentInParent<TrackMover>() != null || collider.GetComponentInParent<Pistol>() != null) continue;
                if (collider.attachedRigidbody != null && !collider.attachedRigidbody.isKinematic) continue;

                int kind = KindOf(collider.transform);
                if (collider.sharedMaterial != null && (kind < 0 || !SurfaceKinds[kind].Replaces)) continue;
                if (kind < 0)
                {
                    if (unsorted.Count < 20) unsorted.Add(collider.name);
                    continue;
                }

                collider.sharedMaterial = materials[kind];
                counts[kind]++;
            }

            for (int i = 0; i < SurfaceKinds.Length; i++)
                if (counts[i] > 0) Did(counts[i] + " colliders are " + SurfaceKinds[i].Name.ToLowerInvariant());

            if (unsorted.Count > 0)
                Say("Not sorted, so they play the plain impact (give each a material from " + SurfaceDir + " by hand): " +
                    string.Join(", ", unsorted.ToArray()));

            Dirty();
        }

        private static int KindOf(Transform item)
        {
            for (var at = item; at != null; at = at.parent)
            {
                string name = at.name.ToLowerInvariant();
                for (int i = 0; i < SurfaceKinds.Length; i++)
                {
                    foreach (var word in SurfaceKinds[i].Words)
                    {
                        if (name.Contains(word)) return i;
                    }
                }
            }

            return -1;
        }

        private static PhysicMaterial SurfaceMaterial(string name)
        {
            string path = SurfaceDir + "/SR_" + name + ".physicMaterial";
            var material = RangeSetupUtil.Load<PhysicMaterial>(path);
            if (material != null) return material;

            material = new PhysicMaterial("SR_" + name);
            AssetDatabase.CreateAsset(material, path);
            Did("Made " + path);
            return material;
        }

        /// By name: the project has a second set of these materials from before this step, already on some colliders
        private static void SetSurfaceDebris(SerializedProperty surfaces, string materialName, DebrisKind debris)
        {
            for (int i = 0; i < surfaces.arraySize; i++)
            {
                var entry = surfaces.GetArrayElementAtIndex(i);
                var material = entry.FindPropertyRelative("material").objectReferenceValue;
                if (material != null && material.name == materialName)
                    entry.FindPropertyRelative("debris").intValue = (int)debris;
            }
        }

        /// One entry per material: an existing one keeps its clips
        private static void RegisterSurface(SerializedProperty surfaces, PhysicMaterial material, List<Object> clips)
        {
            for (int i = 0; i < surfaces.arraySize; i++)
            {
                if (surfaces.GetArrayElementAtIndex(i).FindPropertyRelative("material").objectReferenceValue == material) return;
            }

            surfaces.arraySize++;
            var entry = surfaces.GetArrayElementAtIndex(surfaces.arraySize - 1);
            entry.FindPropertyRelative("material").objectReferenceValue = material;

            var list = entry.FindPropertyRelative("clips");
            list.arraySize = clips.Count;
            for (int i = 0; i < clips.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];

            Did("ImpactFX plays " + clips.Count + " clips on anything that is " + material.name);
        }
    }
}
