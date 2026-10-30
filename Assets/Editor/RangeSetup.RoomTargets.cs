using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using MeshDestroyer = CyberRevolution.BreakableObjectsSystem.Scripts.Editor.MeshDestroyer;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    public static partial class RangeSetup
    {
        [MenuItem("Shooting Range/Setup/28 - Build The Room Targets", false, 50)]
        public static void RoomTargetsMenu() { Begin("Room targets"); BuildTheRoomTargets(); End(); }

        [MenuItem("Shooting Range/Setup/28b - Update The Room Targets", false, 50)]
        public static void UpdateRoomTargetsMenu()
        {
            if (!EditorUtility.DisplayDialog("Update the room targets (ADR 0063)",
                    "Skulls: this REMOVES the shatter from the Room_Skull prefab and from the " +
                    string.Join(", ", CountdownSkullNames) + " in this scene, and makes each one get knocked " +
                    "flying instead, like the plushie bat. Their cut pieces stay on disk.\n\n" +
                    "Armoured stand: Room_ArmourStandArmoured's one-piece model is hidden and its collider REMOVED, " +
                    "and the castle kit's separate stand, armour and helmet go in its place, so the helmet can spin. " +
                    "Where it stands in the scene is kept.\n\nChest: a skull, the plushie bat and a vampire go in " +
                    "Room_Chest, hidden, one of them to spring out each time it opens.\n\nThe scene is saved afterwards.",
                    "Update them", "Cancel"))
                return;

            Begin("Room targets, updated");
            var knock = Clips(WorldSounds + "env_wood_0", 4);
            FloppySkull(knock);
            FlopTheCountdownSkulls(knock);
            LooseHelmetOnTheArmouredStand();
            GagsInTheChest();
            End();
        }

        private const string RoomTargetDir = "Assets/Prefabs/ShootingRange/Room";
        private const string RoomShardDir = ShardPrefabDir + "/Room";
        private const string CastlePrefabs = "Assets/Modular Castle/Assets/prefabs2/";
        private const string StandWithArmour = CastlePrefabs + "mannequin 1.prefab";
        private const string StandWithHelmet = CastlePrefabs + "mannequin.prefab";
        private const string CastleModel = "Assets/Modular Castle/Assets/models/castle.fbx";
        private const string FlyingBatModel = "Assets/Models/New Bat/source/bat_animflapping.fbx";
        private const string FlyingBatPrefab = "Assets/Prefabs/ShootingRange/Bat.prefab";
        private const string WorldSounds = "Assets/Sounds/ShootingRange/World/";
        private const string GlassSounds = "Assets/Sounds/Rhiannon's effects/Heavy Glass Shatter ";
        private const int RoomShardCuts = 3;
        private const float WeaponMass = 1.2f;
        private const float SkullMass = 0.3f;
        private const float SkullImpulse = 0.3f;
        private const float PlushieMass = 0.15f;
        private const float PlushieImpulse = 0.2f;
        private const float FirstPlushieImpulse = 0.6f;
        private const float PaintingMass = 2f;
        private const float FloorThickest = 0.25f;
        private const float FloorHighest = 0.6f;

        private static readonly string[] Wobblers = { "barrel", "mannequin", "vase", "candleholder", "torch" };

        private static void BuildTheRoomTargets()
        {
            System.IO.Directory.CreateDirectory(RoomTargetDir);

            var glass = Clips(GlassSounds, 8);
            var clatter = Clips(WorldSounds + "clatter_0", 4);
            var wood = Clips(WorldSounds + "env_wood_0", 4);

            FloppySkull(wood);
            Shatterable("Room_FlaskTriangular", "Assets/Models/TriangularFlask/flask_low.fbx", "triangularflask", glass);
            Shatterable("Room_FlaskGreen", "Assets/Models/flaskGreen/flask_low.fbx", "flaskgreen", glass);
            ArmourStand();
            ArmouredStand();
            Chest();
            GagsInTheChest();
            SwordStand(clatter);
            PlushieBat();

            FloorsCatchFallingThings();
            WobbleTheRoomProps();
            HangThePainting(wood);

            Say("New prefabs are in " + RoomTargetDir + ": drag them into the scene where you want them");
        }

        // ---------------------------------------------------------------- shatter

        private static void Shatterable(string name, string modelPath, string piecesKey, List<Object> clips)
        {
            string path = RoomTargetDir + "/" + name + ".prefab";
            var prefab = RangeSetupUtil.Load<GameObject>(path);

            if (prefab == null)
            {
                var model = RangeSetupUtil.Load<GameObject>(modelPath);
                if (model == null) { Fail(modelPath + " is missing, so there is no " + name); return; }

                var copy = (GameObject)PrefabUtility.InstantiatePrefab(model);
                copy.name = name;
                BoxEachMesh(copy);

                var shatter = copy.AddComponent<ShatterOnShot>();
                if (clips != null && clips.Count > 0) new Fields(shatter).SetArray("shatterClips", clips).Apply();

                prefab = SaveTarget(copy, path, "shatters when shot");
                if (prefab == null) return;
            }
            else Say(name + " is already there, left alone");

            WireBrokenSet(prefab, piecesKey, modelPath);
        }

        // ---------------------------------------------------------------- skulls

        /// Knocked flying and bouncing like the plushie bat rather than shattering (ADR 0063)
        private static void FloppySkull(List<Object> knock)
        {
            const string name = "Room_Skull";
            string path = RoomTargetDir + "/" + name + ".prefab";

            if (RangeSetupUtil.Load<GameObject>(path) == null)
            {
                var model = RangeSetupUtil.Load<GameObject>(SkullModel);
                if (model == null) { Fail(SkullModel + " is missing, so there is no " + name); return; }

                var copy = (GameObject)PrefabUtility.InstantiatePrefab(model);
                copy.name = name;
                BoxEachMesh(copy);
                MakeItFlop(copy, knock);
                SaveTarget(copy, path, "knocked flying when shot");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponent<ReleaseOnShot>() != null) { Say(name + " already flops, left alone"); return; }

                MakeItFlop(root, knock);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Did(name + ": shatters -> knocked flying");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void FlopTheCountdownSkulls(List<Object> knock)
        {
            foreach (var name in CountdownSkullNames)
            {
                var skull = RangeSetupUtil.FindInScene(name);
                if (skull == null) { Fail("No '" + name + "' in the scene"); continue; }
                if (skull.GetComponent<ReleaseOnShot>() != null) { Say(name + " already flops, left alone"); continue; }

                BoxEachMesh(skull);
                MakeItFlop(skull, knock);
                Did(name + ": shatters -> knocked flying");
            }

            Dirty();
        }

        /// Swaps a shatter for the plushie's knock: one rigidbody the shot throws, put back next round
        private static void MakeItFlop(GameObject root, List<Object> knock)
        {
            var shatter = root.GetComponent<ShatterOnShot>();
            if (shatter != null) Object.DestroyImmediate(shatter);

            var body = RangeSetupUtil.Ensure<Rigidbody>(root);
            body.mass = SkullMass;
            body.isKinematic = true;

            var fields = new Fields(root.AddComponent<ReleaseOnShot>())
                .SetArray("pieces", new List<Object> { body })
                .Set("impulse", SkullImpulse).Set("nudge", 0f).Set("lift", 0.5f);
            if (knock.Count > 0) fields.SetArray("hitClips", knock);
            fields.Apply();
        }

        /// Pieces are found by the model's name, so the order things were cut in can never pair a skull with a
        /// flask's pieces. None yet, and they are cut here
        private static void WireBrokenSet(GameObject prefab, string key, string modelPath)
        {
            var shatter = prefab.GetComponent<ShatterOnShot>();
            if (shatter == null) return;

            var fields = new Fields(shatter);
            var set = fields.Ref("brokenSet") as GameObject;

            if (set == null)
            {
                set = FindBrokenSet(key) ?? CutPieces(modelPath, key, null);
                if (set == null) return;

                fields.Set("brokenSet", set).Apply();
                EditorUtility.SetDirty(prefab);
                Did(prefab.name + " breaks into " + set.name);
            }

            AddToPool(set);
        }

        /// By exact name, Broken<key>_0, so a red skull's pieces can never be taken for the white one's
        private static GameObject FindBrokenSet(string key)
        {
            string wanted = "broken" + key.ToLowerInvariant() + "_0";
            foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { ShardPrefabDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant() == wanted)
                    return RangeSetupUtil.Load<GameObject>(path);
            }

            return null;
        }

        /// The Breakable Objects cutter, run directly: its window opens off-screen on this machine. Each cut halves
        /// every piece along a random plane, so three cuts give about eight. skin, when given, replaces the model's
        /// own material, so a coloured copy breaks into pieces of its colour
        private static GameObject CutPieces(string modelPath, string key, Material skin)
        {
            var model = RangeSetupUtil.Load<GameObject>(modelPath);
            if (model == null) return null;

            string name = "Broken" + key + "_0";
            string dir = RoomShardDir + "/" + name;
            System.IO.Directory.CreateDirectory(dir);

            var copy = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(copy, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var root = new GameObject(name);
            int prop = RangeSetupUtil.Layer("Prop");
            int count = 0;

            try
            {
                foreach (var filter in copy.GetComponentsInChildren<MeshFilter>())
                {
                    var parts = new List<GameObject> { filter.gameObject };
                    for (int cut = 0; cut < RoomShardCuts; cut++)
                    {
                        var next = new List<GameObject>();
                        foreach (var part in parts)
                        {
                            var cutter = new MeshDestroyer(part.GetComponent<MeshFilter>(), 1, true);
                            cutter.DestroyMesh();
                            next.AddRange(cutter.Parts);
                            if (part != filter.gameObject) Object.DestroyImmediate(part);
                        }

                        parts = next;
                    }

                    foreach (var part in parts)
                    {
                        part.transform.SetParent(root.transform, true);
                        part.name = name + "_part_" + count;

                        if (skin != null)
                        {
                            var renderer = part.GetComponent<MeshRenderer>();
                            var materials = renderer.sharedMaterials;
                            for (int m = 0; m < materials.Length; m++) materials[m] = skin;
                            renderer.sharedMaterials = materials;
                        }
                        if (prop >= 0) part.layer = prop;

                        AssetDatabase.CreateAsset(part.GetComponent<MeshFilter>().sharedMesh, dir + "/" + part.name + ".asset");

                        // The cutter leaves convex mesh colliders; boxes are what the sign pieces use, and cheaper
                        Object.DestroyImmediate(part.GetComponent<Collider>());
                        part.AddComponent<BoxCollider>();
                        part.AddComponent<Rigidbody>().mass = ShardMass;
                        count++;
                    }
                }
            }
            finally { Object.DestroyImmediate(copy); }

            if (count == 0) { Object.DestroyImmediate(root); Fail("Could not cut " + modelPath); return null; }

            var saved = PrefabUtility.SaveAsPrefabAsset(root, dir + "/" + name + ".prefab");
            Object.DestroyImmediate(root);
            if (saved != null) Did(name + ": " + count + " pieces cut from " + modelPath);
            return saved;
        }

        /// The pool only throws pieces it built at load; a set it was never given falls back to plain fragments
        private static void AddToPool(GameObject set)
        {
            var pool = Object.FindObjectOfType<ShatterPool>();
            if (pool == null) { Fail("No ShatterPool in the scene, so " + set.name + " can't be thrown"); return; }

            var fields = new Fields(pool);
            var sets = fields.Find("shardSets");
            for (int i = 0; i < sets.arraySize; i++)
            {
                if (sets.GetArrayElementAtIndex(i).objectReferenceValue == set) return;
            }

            sets.arraySize++;
            sets.GetArrayElementAtIndex(sets.arraySize - 1).objectReferenceValue = set;
            fields.Apply();
            Dirty();
            Did("ShatterPool pre-warms " + set.name);
        }

        // ---------------------------------------------------------------- stands

        private static void ArmourStand()
        {
            const string name = "Room_ArmourStand";
            string path = RoomTargetDir + "/" + name + ".prefab";
            if (RangeSetupUtil.Load<GameObject>(path) != null) { Say(name + " is already there, left alone"); return; }

            var source = RangeSetupUtil.Load<GameObject>(StandWithHelmet);
            if (source == null) { Fail(StandWithHelmet + " is missing"); return; }

            var copy = (GameObject)PrefabUtility.InstantiatePrefab(source);
            copy.name = name;

            var helmet = FindPart(copy.transform, "helmet");
            if (helmet == null) { Object.DestroyImmediate(copy); Fail("The mannequin has no helmet to spin"); return; }

            BoxEachMesh(copy);
            var spin = copy.AddComponent<SpinOnShot>();
            new Fields(spin).Set("spinner", helmet.GetComponent<MeshRenderer>()).Apply();

            SaveTarget(copy, path, "the helmet spins when the stand is shot");
        }

        private const string ArmouredStandName = "Room_ArmourStandArmoured";
        private const string ArmourParts = "Parts";

        private static void ArmouredStand()
        {
            string path = RoomTargetDir + "/" + ArmouredStandName + ".prefab";
            if (RangeSetupUtil.Load<GameObject>(path) != null) { Say(ArmouredStandName + " is already there, left alone"); return; }

            var copy = new GameObject(ArmouredStandName);
            var helmet = ArmourWithLooseHelmet(copy.transform);
            if (helmet == null) { Object.DestroyImmediate(copy); return; }

            copy.AddComponent<WobbleOnShot>();
            new Fields(copy.AddComponent<SpinOnShot>()).Set("spinner", helmet).Apply();

            SaveTarget(copy, path, "rocks back and forth and spins its helmet when shot");
        }

        /// Built before 2026-10-27 from the kit's one-piece armoured mannequin, whose helmet can't turn on its own.
        /// The pieces go in under the same root, lined up with the old model, so its place in the scene is kept
        private static void LooseHelmetOnTheArmouredStand()
        {
            string path = RoomTargetDir + "/" + ArmouredStandName + ".prefab";
            if (RangeSetupUtil.Load<GameObject>(path) == null) { Say(ArmouredStandName + " is not built yet. Step 28 builds it"); return; }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponent<SpinOnShot>() != null) { Say(ArmouredStandName + " already spins its helmet, left alone"); return; }

                var helmet = ArmourWithLooseHelmet(root.transform);
                if (helmet == null) return;

                var old = root.GetComponent<MeshRenderer>();
                if (old != null)
                {
                    var parts = RangeSetupUtil.Child(root.transform, ArmourParts);
                    var was = old.bounds;
                    var now = BoundsOf(parts.GetComponentsInChildren<Renderer>());
                    parts.position += new Vector3(was.center.x - now.center.x, was.min.y - now.min.y, was.center.z - now.center.z);
                    old.enabled = false;
                }

                foreach (var box in root.GetComponents<Collider>()) Object.DestroyImmediate(box);
                new Fields(root.AddComponent<SpinOnShot>()).Set("spinner", helmet).Apply();

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Did(ArmouredStandName + ": one-piece model swapped for the kit's stand, armour and a helmet that spins");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /// The kit's stand-and-armour with the helmet off the plain stand set on top, under parent, each part boxed
        /// for rounds. Returns the helmet, or null with the reason reported
        private static MeshRenderer ArmourWithLooseHelmet(Transform parent)
        {
            var armoured = RangeSetupUtil.Load<GameObject>(StandWithArmour);
            var helmeted = RangeSetupUtil.Load<GameObject>(StandWithHelmet);
            var source = helmeted != null ? FindPart(helmeted.transform, "helmet") : null;
            if (armoured == null || source == null) { Fail(StandWithArmour + " or the helmet in " + StandWithHelmet + " is missing"); return null; }

            var parts = (GameObject)PrefabUtility.InstantiatePrefab(armoured);
            parts.name = ArmourParts;
            parts.transform.SetParent(parent, false);

            // The helmet sits on the plain stand in its own prefab, and this stand is the same mesh
            var stand = FindPart(parts.transform, "mannequin");
            var helmet = new GameObject("helmet");
            helmet.transform.SetParent(stand != null ? stand : parts.transform, false);
            helmet.transform.localPosition = source.localPosition;
            helmet.transform.localRotation = source.localRotation;
            helmet.transform.localScale = source.localScale;
            helmet.AddComponent<MeshFilter>().sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
            var renderer = helmet.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials;

            BoxEachMesh(parts);
            return renderer;
        }

        // ---------------------------------------------------------------- chest

        private const float GagShareOfChest = 0.8f;
        private static readonly string[] GagModels = { SkullModel, FlyingBatModel, "Assets/Models/Vampire/vimpire.fbx" };

        /// A skull, the plushie bat or a vampire, one picked each time the lid flies open (ADR 0063). Each is
        /// sized to the box and hidden inside it. Fills blanks: gags already wired are left alone
        private static void GagsInTheChest()
        {
            const string name = "Room_Chest";
            string path = RoomTargetDir + "/" + name + ".prefab";
            if (RangeSetupUtil.Load<GameObject>(path) == null) { Say(name + " is not built yet. Step 28 builds it"); return; }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var open = root.GetComponentInChildren<PopOpenOnShot>();
                if (open == null) { Fail(name + " has no Pop Open On Shot"); return; }

                var fields = new Fields(open);
                if (fields.Find("gags").arraySize > 0) { Say(name + " already has its gags, left alone"); return; }

                var box = BoundsOf(root.GetComponentsInChildren<Renderer>());
                float room = Mathf.Min(box.size.x, box.size.y, box.size.z) * GagShareOfChest;

                // On the chest itself, not the lid, so they stay put as the lid swings
                var holder = new GameObject("Gags").transform;
                holder.SetParent(open.transform, false);

                var gags = new List<Object>();
                foreach (var modelPath in GagModels)
                {
                    var model = RangeSetupUtil.Load<GameObject>(modelPath);
                    if (model == null) { Say(modelPath + " is missing, so that one is left out"); continue; }

                    var gag = (GameObject)PrefabUtility.InstantiatePrefab(model);
                    gag.transform.SetParent(holder, false);
                    foreach (var animator in gag.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                    foreach (var animation in gag.GetComponentsInChildren<Animation>(true)) animation.enabled = false;
                    foreach (var collider in gag.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);

                    var size = BoundsOf(gag.GetComponentsInChildren<Renderer>()).size;
                    gag.transform.localScale *= room / Mathf.Max(0.001f, Mathf.Max(size.x, size.y, size.z));

                    var now = BoundsOf(gag.GetComponentsInChildren<Renderer>());
                    gag.transform.position += new Vector3(box.center.x - now.center.x, box.min.y - now.min.y,
                                                          box.center.z - now.center.z);
                    gag.SetActive(false);
                    gags.Add(gag.transform);
                }

                fields.SetArray("gags", gags).Set("springHeight", box.size.y).Apply();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Did(name + ": " + gags.Count + " things that can spring out. Turn each under Gags to face the front if it doesn't");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /// The kit has the chest's lid and body as separate meshes, but only inside the whole castle model, so
        /// those two are lifted out of it and the rest thrown away
        private static void Chest()
        {
            const string name = "Room_Chest";
            string path = RoomTargetDir + "/" + name + ".prefab";

            if (RangeSetupUtil.Load<GameObject>(path) != null)
            {
                EditPrefab(path, MoveChestHinge);
                return;
            }

            var model = RangeSetupUtil.Load<GameObject>(CastleModel);
            if (model == null) { Fail(CastleModel + " is missing"); return; }

            var castle = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(castle, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            var body = FindPart(castle.transform, "box_lower");
            var lid = FindPart(castle.transform, "box_upper");
            if (body == null || lid == null)
            {
                Object.DestroyImmediate(castle);
                Fail("castle.fbx has no box_lower / box_upper");
                return;
            }

            var root = new GameObject(name).transform;
            body.SetParent(root, true);
            lid.SetParent(root, true);
            Object.DestroyImmediate(castle);

            // Stands on its own base at the prefab's origin, wherever the kit happened to lay it out
            var all = body.GetComponent<Renderer>().bounds;
            all.Encapsulate(lid.GetComponent<Renderer>().bounds);
            var foot = new Vector3(all.center.x, all.min.y, all.center.z);
            body.position -= foot;
            lid.position -= foot;

            var hinge = PlaceHinge(root, lid);
            BoxEachMesh(root.gameObject);
            var pop = root.gameObject.AddComponent<PopOpenOnShot>();
            new Fields(pop).Set("hinge", hinge).SetArray("openClips", Clips(WorldSounds + "coffin_creak_0", 4)).Apply();

            SaveTarget(root.gameObject, path, "the lid pops open");
        }

        /// The kit's chest has its latch on +Z (it was turned to face the player that way round), so the hinge
        /// runs along the lid's long side at the back: -Z, or -X for a chest laid out sideways. Turned so that
        /// opening is a positive turn about the hinge's X, which lifts the latch side
        private static Quaternion HingeTurn(Bounds top)
        {
            return top.size.x >= top.size.z ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.Euler(0f, -90f, 0f);
        }

        private static Transform PlaceHinge(Transform root, Transform lid)
        {
            var top = lid.GetComponent<Renderer>().bounds;
            var turn = HingeTurn(top);
            float depth = top.size.x >= top.size.z ? top.size.z : top.size.x;

            var hinge = new GameObject("Hinge").transform;
            hinge.SetParent(root, false);
            hinge.localRotation = turn;
            hinge.localPosition = new Vector3(top.center.x, top.min.y, top.center.z) + turn * Vector3.forward * (depth * 0.5f);
            lid.SetParent(hinge, true);
            return hinge;
        }

        /// Chests built before 2026-10-23 had the hinge on the latch side
        private static bool MoveChestHinge(GameObject root)
        {
            var pop = root.GetComponent<PopOpenOnShot>();
            var lid = FindPart(root.transform, "box_upper");
            var old = pop != null ? new Fields(pop).Ref("hinge") as Transform : null;
            if (lid == null || old == null) { Fail("Room_Chest has no hinge or lid to fix"); return false; }

            if (Quaternion.Angle(old.localRotation, HingeTurn(lid.GetComponent<Renderer>().bounds)) < 1f)
            {
                Say("Room_Chest is already there, left alone");
                return false;
            }

            lid.SetParent(root.transform, true);
            Object.DestroyImmediate(old.gameObject);
            new Fields(pop).Set("hinge", PlaceHinge(root.transform, lid)).Apply();
            Did("Room_Chest: hinge moved to the back, away from the latch");
            return true;
        }

        // ---------------------------------------------------------------- things that fall

        private static void SwordStand(List<Object> clatter)
        {
            const string name = "Room_SwordStand";
            string path = RoomTargetDir + "/" + name + ".prefab";

            if (RangeSetupUtil.Load<GameObject>(path) != null)
            {
                EditPrefab(path, root =>
                {
                    bool changed = false;
                    foreach (var piece in root.GetComponentsInChildren<Rigidbody>(true)) changed |= AddClatter(piece.gameObject, clatter);
                    if (changed) Did(name + ": the weapons clatter when they land");
                    else Say(name + " is already there, left alone");
                    return changed;
                });
                return;
            }

            var source = RangeSetupUtil.Load<GameObject>(CastlePrefabs + "stand for weapons.prefab");
            if (source == null) { Fail(CastlePrefabs + "stand for weapons.prefab is missing"); return; }

            var copy = (GameObject)PrefabUtility.InstantiatePrefab(source);
            copy.name = name;
            BoxEachMesh(copy);

            var weapons = new List<Object>();
            foreach (var filter in copy.GetComponentsInChildren<MeshFilter>(true))
            {
                string part = filter.name.ToLowerInvariant();
                if (!part.StartsWith("sword") && !part.StartsWith("axe")) continue;

                var piece = filter.gameObject.AddComponent<Rigidbody>();
                piece.mass = WeaponMass;
                piece.isKinematic = true;
                AddClatter(filter.gameObject, clatter);
                weapons.Add(piece);
            }

            if (weapons.Count == 0) { Object.DestroyImmediate(copy); Fail("The weapon stand has no swords or axes on it"); return; }

            var release = copy.AddComponent<ReleaseOnShot>();
            new Fields(release).SetArray("pieces", weapons).Apply();

            SaveTarget(copy, path, weapons.Count + " weapons fall and clatter when the stand is shot");
        }

        /// The swarm's own bat, held still. Its size is read off the flying bat so the two always match
        private static void PlushieBat()
        {
            const string name = "Room_PlushieBat";
            string path = RoomTargetDir + "/" + name + ".prefab";

            var existing = RangeSetupUtil.Load<GameObject>(path);
            if (existing != null)
            {
                // Built before 2026-10-23 with a push that threw it across the room
                var fields = new Fields(existing.GetComponent<ReleaseOnShot>());
                if (Mathf.Approximately(fields.Float("impulse"), FirstPlushieImpulse))
                {
                    fields.Set("impulse", PlushieImpulse).Apply();
                    EditorUtility.SetDirty(existing);
                    Did(name + ": push " + FirstPlushieImpulse + " -> " + PlushieImpulse + " N·s");
                }
                else Say(name + " is already there, left alone");
                return;
            }

            var model = RangeSetupUtil.Load<GameObject>(FlyingBatModel);
            if (model == null) { Fail(FlyingBatModel + " is missing"); return; }

            var root = new GameObject(name);
            var bat = (GameObject)PrefabUtility.InstantiatePrefab(model);
            bat.transform.SetParent(root.transform, false);

            var flying = RangeSetupUtil.Load<GameObject>(FlyingBatPrefab);
            var flyingModel = flying != null ? RangeSetupUtil.Child(flying.transform, "Model") : null;
            if (flyingModel != null) bat.transform.localScale = flyingModel.localScale;
            else Say("No Model under " + FlyingBatPrefab + " to copy the size from; the plushie is at the model's own scale");

            foreach (var animator in bat.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            foreach (var animation in bat.GetComponentsInChildren<Animation>(true))
            {
                animation.playAutomatically = false;
                animation.enabled = false;
            }

            var renderer = bat.GetComponentInChildren<Renderer>();
            var box = root.AddComponent<BoxCollider>();
            if (renderer != null)
            {
                box.center = renderer.bounds.center;
                box.size = renderer.bounds.size;
            }

            var body = root.AddComponent<Rigidbody>();
            body.mass = PlushieMass;
            body.isKinematic = true;

            var release = root.AddComponent<ReleaseOnShot>();
            new Fields(release).SetArray("pieces", new List<Object> { body })
                               .Set("impulse", PlushieImpulse).Set("nudge", 0f).Set("lift", 0.5f).Apply();

            SaveTarget(root, path, "knocked flying when shot. Its Hit Clips are empty: add a squeak");
        }

        /// The room's floor meshes had no colliders, so anything that fell went straight through. Only flat,
        /// low room meshes count: the floor tiles, carpets and platforms, not walls or the roof
        private static void FloorsCatchFallingThings()
        {
            int added = 0;
            foreach (var renderer in Object.FindObjectsOfType<MeshRenderer>())
            {
                if (renderer.GetComponent<Collider>() != null || !IsRoom(renderer)) continue;

                var size = renderer.bounds.size;
                if (size.y > FloorThickest || renderer.bounds.max.y > FloorHighest || size.x * size.z < 0.5f) continue;

                renderer.gameObject.AddComponent<BoxCollider>();
                added++;
            }

            if (added == 0) { Say("Every floor mesh already has a collider"); return; }

            Dirty();
            Did(added + " floor meshes given box colliders, so falling things land (and bullets now leave holes there)");
        }

        // ---------------------------------------------------------------- props already in the room

        /// Barrels, mannequins, the vase, candleholders and torches rock where they stand. Anything that already
        /// reacts to a shot is skipped, which keeps the armour stand prefabs' own mannequins out of this
        private static void WobbleTheRoomProps()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            var names = new List<string>();

            foreach (var top in scene.GetRootGameObjects())
            {
                foreach (var item in top.GetComponentsInChildren<Transform>(true))
                {
                    if (!IsWobbler(item.name) || (item.parent != null && IsWobbler(item.parent.name))) continue;
                    if (item.GetComponentInParent<IShootable>() != null || item.GetComponentInChildren<Renderer>() == null) continue;

                    BoxEachMesh(item.gameObject);
                    item.gameObject.AddComponent<WobbleOnShot>();
                    names.Add(item.name);
                }
            }

            if (names.Count == 0) { Say("The room's barrels, mannequins, vase, candleholders and torches already wobble"); return; }

            Dirty();
            Did(names.Count + " props wobble when shot: " + string.Join(", ", names.ToArray()));
        }

        private static bool IsWobbler(string name)
        {
            string low = name.ToLowerInvariant();
            foreach (var key in Wobblers)
            {
                if (low.StartsWith(key)) return true;
            }

            return false;
        }

        private static void HangThePainting(List<Object> wood)
        {
            var painting = RangeSetupUtil.FindInScene("painting");
            if (painting == null) { Say("No painting in the scene to hang"); return; }
            if (painting.GetComponent<HangingOnShot>() != null) { Say("The painting already hangs, left alone"); return; }

            BoxEachMesh(painting);

            var body = RangeSetupUtil.Ensure<Rigidbody>(painting);
            body.mass = PaintingMass;
            body.isKinematic = true;

            var hang = painting.AddComponent<HangingOnShot>();
            new Fields(hang).Set("body", body).Apply();
            AddClatter(painting, wood);

            Dirty();
            Did("The painting swings crooked on the first hit and falls on the next");
        }

        // ---------------------------------------------------------------- shared

        private static bool AddClatter(GameObject piece, List<Object> clips)
        {
            if (piece.GetComponent<Clatter>() != null) return false;

            new Fields(piece.AddComponent<Clatter>()).SetArray("clips", clips).Apply();
            return true;
        }

        /// Changes to a prefab that is already placed go through its own contents, so the scene's copies stay
        /// linked and keep where they were put
        private static void EditPrefab(string path, System.Func<GameObject, bool> edit)
        {
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (edit(contents)) PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        private static GameObject SaveTarget(GameObject copy, string path, string what)
        {
            var saved = PrefabUtility.SaveAsPrefabAsset(copy, path);
            Object.DestroyImmediate(copy);

            if (saved == null) Fail("Could not save " + path);
            else Did(System.IO.Path.GetFileNameWithoutExtension(path) + ": " + what);

            return saved;
        }

        private static void BoxEachMesh(GameObject root)
        {
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.GetComponent<Collider>() == null) filter.gameObject.AddComponent<BoxCollider>();
            }
        }

        /// prefix + 1 .. count + ".wav", whichever of them exist
        private static List<Object> Clips(string prefix, int count)
        {
            var clips = new List<Object>();
            for (int i = 1; i <= count; i++)
            {
                var clip = RangeSetupUtil.Load<AudioClip>(prefix + i + ".wav");
                if (clip != null) clips.Add(clip);
            }

            return clips;
        }
    }
}
