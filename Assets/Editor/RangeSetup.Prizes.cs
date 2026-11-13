using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    /// Step 69: the six prizes (user, 2026-11-10). Each is a room target that is knocked flying with a squeak when shot and
    /// back next round, standing on a shelf along the booth top, and the same six, stripped, are hidden in the chest. The
    /// posed models come from tools/prize_pose.py (Blender), baked still out of their T-poses
    public static partial class RangeSetup
    {
        private const string PrizeModelDir = "Assets/Models/Prizes";
        private const string PrizeMaterialDir = "Assets/Materials/Prizes";
        private const string PrizeShelfName = "Prize Shelf";
        private const string BoothTopName = "Booth Top";
        // Every prize's biggest side, metres, on the shelf: one size so they all read (user, 2026-11-10)
        private const float PrizeSize = 0.4f;
        private const float ShelfUse = 0.85f;
        private const int PrizeTextureLimit = 1024;

        private class PrizeSkin
        {
            public string slot;      // part of the imported material's name, "" for any
            public string albedo;
            public string normal;
            public Color colour = Color.white;
        }

        private class PrizeModel
        {
            public string name;
            public PrizeSkin[] skins;
        }

        private const string ChibiTextures = "Assets/Chibi Monster Halloween Film Classics/Textures/";

        private static readonly PrizeModel[] PrizeModels =
        {
            new PrizeModel { name = "Ghost", skins = new[] { new PrizeSkin { slot = "", albedo = "Assets/GhostCharacter_Free/Textures/texture_ghost.png" } } },
            new PrizeModel { name = "Mummy", skins = new[]
            {
                new PrizeSkin { slot = "Eye", colour = new Color(0.87f, 0.52f, 0.004f) },
                new PrizeSkin { slot = "", albedo = "Assets/amusedART/Mummy_Mon/Textures/Mummy_albedo.psd", normal = "Assets/amusedART/Mummy_Mon/Textures/Mummy_normals.psd" },
            } },
            new PrizeModel { name = "Bride", skins = new[] { new PrizeSkin { slot = "", albedo = ChibiTextures + "Bride/Bride_Bride_AlbedoTransparency.png", normal = ChibiTextures + "Bride/Bride_Bride_Normal.png" } } },
            new PrizeModel { name = "Vamp", skins = new[]
            {
                // The 30 faces on Material.002 are the teeth
                new PrizeSkin { slot = "Material.002", albedo = ChibiTextures + "Vamp/Vamp_VamoTeeth_AlbedoTransparency.png" },
                new PrizeSkin { slot = "", albedo = ChibiTextures + "Vamp/Vamp_Vamp_AlbedoTransparency.png", normal = ChibiTextures + "Vamp/Vamp_Vamp_Normal.png" },
            } },
            new PrizeModel { name = "Skeleton", skins = new[] { new PrizeSkin { slot = "", albedo = "Assets/PolyOne/Cartoon Skeleton/Texture/Texture_Cartoon Skeleton_BaseColor.png" } } },
        };

        [MenuItem("Shooting Range/Setup/69 - Build The Prizes", false, 92)]
        public static void PrizesMenu()
        {
            if (!EditorUtility.DisplayDialog("Build the prizes",
                    "This makes the five Room_Prize prefabs (only those missing), adds a Prize Shelf on the booth top if there " +
                    "isn't one, and then rebuilds Room_Chest's lock and prizes as step 62 does, so hand changes to those " +
                    "parts of the chest are lost.", "Build", "Cancel"))
                return;

            Begin("Prizes");
            BuildThePrizes();
            BuildThePrizeShelf();
            BuildThePrizeChest();
            End();
        }

        private static void BuildThePrizes()
        {
            var shader = Shader.Find(SignShader);
            if (shader == null) { Fail("The " + SignShader + " shader has not compiled"); return; }
            System.IO.Directory.CreateDirectory(PrizeMaterialDir);

            foreach (var model in PrizeModels)
            {
                string name = "Room_Prize_" + model.name;
                string path = RoomTargetDir + "/" + name + ".prefab";
                if (RangeSetupUtil.Load<GameObject>(path) != null) { Say(name + " is already there, left alone"); continue; }

                var source = RangeSetupUtil.Load<GameObject>(PrizeModelDir + "/Prize_" + model.name + ".fbx");
                if (source == null) { Fail(PrizeModelDir + "/Prize_" + model.name + ".fbx is missing"); continue; }

                var root = new GameObject(name);
                var figure = Object.Instantiate(source, root.transform, false);
                figure.name = model.name;
                foreach (var renderer in figure.GetComponentsInChildren<Renderer>(true)) Dress(renderer, model, shader);

                var size = BoundsOf(figure.GetComponentsInChildren<Renderer>()).size;
                figure.transform.localScale *= PrizeSize / Mathf.Max(0.001f, Mathf.Max(size.x, size.y, size.z));
                var now = BoundsOf(figure.GetComponentsInChildren<Renderer>());
                figure.transform.position -= new Vector3(now.center.x, now.min.y, now.center.z);

                MakeItFlop(root, BoundsOf(figure.GetComponentsInChildren<Renderer>()));
                SaveTarget(root, path, PrizeSize + " m, knocked flying with a squeak when shot, back next round");
            }

            var bat = RangeSetupUtil.Load<GameObject>(RoomTargetDir + "/Room_PlushieBat.prefab");
            if (bat == null) { Fail("No Room_PlushieBat. Step 28 builds it"); return; }
            var batRoot = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(bat));
            try
            {
                if (Fill(batRoot.GetComponent<ReleaseOnShot>(), "hitClips", "plushie_squeak") > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(batRoot, AssetDatabase.GetAssetPath(bat));
                    Did("Room_PlushieBat: squeaks when shot");
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(batRoot); }
        }

        /// The same flop as the plushie bat (step 28): kinematic until shot, then thrown, squeaking
        private static void MakeItFlop(GameObject root, Bounds bounds)
        {
            var box = root.AddComponent<BoxCollider>();
            box.center = root.transform.InverseTransformPoint(bounds.center);
            box.size = bounds.size;

            var body = root.AddComponent<Rigidbody>();
            body.mass = PlushieMass;
            body.isKinematic = true;

            var release = root.AddComponent<ReleaseOnShot>();
            new Fields(release).SetArray("pieces", new List<Object> { body })
                               .Set("impulse", PlushieImpulse).Set("nudge", 0f).Set("lift", 0.5f).Apply();
            Fill(release, "hitClips", "plushie_squeak");
        }

        /// One material per slot, on the sign shader so prizes light like the signs and stay readable in the dark
        private static void Dress(Renderer renderer, PrizeModel model, Shader shader)
        {
            var slots = renderer.sharedMaterials;
            for (int i = 0; i < slots.Length; i++)
            {
                string slotName = slots[i] != null ? slots[i].name : "";
                PrizeSkin skin = null;
                foreach (var candidate in model.skins)
                {
                    if (candidate.slot.Length == 0 || slotName.Contains(candidate.slot)) { skin = candidate; break; }
                }
                if (skin == null) continue;

                string path = PrizeMaterialDir + "/SR_Prize_" + model.name + (skin.slot.Length > 0 ? "_" + skin.slot.Replace(".", "") : "") + ".mat";
                var material = RangeSetupUtil.Load<Material>(path);
                if (material == null)
                {
                    material = new Material(shader);
                    material.color = skin.colour;
                    if (skin.albedo != null) material.SetTexture("_MainTex", PrizeTexture(skin.albedo, false));
                    if (skin.normal != null)
                    {
                        material.SetTexture("_BumpMap", PrizeTexture(skin.normal, true));
                        material.EnableKeyword("_NORMALMAP");
                    }
                    AssetDatabase.CreateAsset(material, path);
                    Did("Made " + path);
                }

                slots[i] = material;
            }

            renderer.sharedMaterials = slots;
        }

        /// No bigger than PrizeTextureLimit: a prize is 40 cm across, metres away
        private static Texture2D PrizeTexture(string path, bool normal)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { Fail("No texture " + path); return null; }

            bool change = false;
            if (importer.maxTextureSize > PrizeTextureLimit) { importer.maxTextureSize = PrizeTextureLimit; change = true; }
            if (normal && importer.textureType != TextureImporterType.NormalMap) { importer.textureType = TextureImporterType.NormalMap; change = true; }
            if (change) importer.SaveAndReimport();
            return RangeSetupUtil.Load<Texture2D>(path);
        }

        /// The six, worst to best, left to right along the top of the booth, facing the player. Fills blanks: once the
        /// shelf is in the scene it is left where it was put
        private static void BuildThePrizeShelf()
        {
            if (RangeSetupUtil.FindInScene(PrizeShelfName) != null) { Say(PrizeShelfName + " is already in the scene, left alone"); return; }

            var booth = RangeSetupUtil.FindInScene(BoothTopName);
            var renderers = booth != null ? booth.GetComponentsInChildren<Renderer>() : new Renderer[0];
            if (renderers.Length == 0) { Fail("No " + BoothTopName + " with a renderer to stand the prizes on"); return; }
            var top = BoundsOf(renderers);

            var shelf = new GameObject(PrizeShelfName).transform;
            shelf.position = new Vector3(top.center.x, top.max.y, top.center.z);

            float width = top.size.x * ShelfUse;
            for (int i = 0; i < PrizeOrder.Length; i++)
            {
                var prefab = RangeSetupUtil.Load<GameObject>(RoomTargetDir + "/" + PrizeOrder[i] + ".prefab");
                if (prefab == null) { Fail("No " + PrizeOrder[i] + ".prefab"); continue; }

                var prize = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                prize.transform.SetParent(shelf, false);
                prize.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

                var size = BoundsOf(prize.GetComponentsInChildren<Renderer>()).size;
                prize.transform.localScale *= PrizeSize / Mathf.Max(0.001f, Mathf.Max(size.x, size.y, size.z));

                float x = PrizeOrder.Length > 1 ? -width * 0.5f + width * i / (PrizeOrder.Length - 1) : 0f;
                prize.transform.localPosition = new Vector3(x, 0f, 0f);
                var now = BoundsOf(prize.GetComponentsInChildren<Renderer>());
                prize.transform.position += Vector3.up * (shelf.position.y - now.min.y);
            }

            Did(PrizeShelfName + ": " + PrizeOrder.Length + " prizes along " + BoothTopName + " at y " + top.max.y.ToString("0.00") +
                ", facing -Z. Move the shelf to place them; this step won't move it again");
            Dirty();
        }
    }
}
