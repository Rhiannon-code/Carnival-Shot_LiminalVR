using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    /// Step 84: what step 82 found on 2026-11-17, after Alex's props came in, put right (user's picks). His new props react to a
    /// shot, the bare flasks become the shattering ones, the old steps that can be seen take shots, the floors and backdrop get
    /// a surface, and the lighting rig and skylight lids stop shots, unless they would stand in a sign's way. Fills blanks:
    /// anything already set is left alone
    public static partial class RangeSetup
    {
        private enum PropReaction { Hang, Shatter, Wobble, Fall, Spin }

        private struct ShootableProp
        {
            public string Model;
            public PropReaction Reaction;
            public string Surface;
            public ShootableProp(string model, PropReaction reaction, string surface) { Model = model; Reaction = reaction; Surface = surface; }
        }

        // Keyed by the model the mesh comes from, so Alex's grouping and names don't matter
        private static readonly ShootableProp[] ShootableProps =
        {
            new ShootableProp("Assets/Mesh/historic_medieval_farm_lamp_iron_hanging_02_yf1jdjr_low/Historic_Medieval_Farm_Lamp_Iron_Hanging_02_yf1jdjr_Low.fbx", PropReaction.Hang, "Metal"),
            new ShootableProp("Assets/Mesh/metal_hook_wcvoef3_low/Metal_Hook_wcvoef3_Low.fbx", PropReaction.Hang, "Metal"),
            new ShootableProp("Assets/Mesh/Lantern1/lantern.fbx", PropReaction.Hang, "Metal"),
            new ShootableProp("Assets/Mesh/mirror/mirror.fbx", PropReaction.Shatter, "Ceramic"),
            new ShootableProp("Assets/Mesh/cobweb/cobweb.fbx", PropReaction.Wobble, "Canvas"),
            new ShootableProp("Assets/Mesh/historic_medieval_library_candle_wax_used_06_xmtlddf_low/Historic_Medieval_Library_Candle_Wax_Used_06_xmtlddf_Low.fbx", PropReaction.Fall, "Soft"),
            new ShootableProp("Assets/Mesh/wall_bats/wall_bats.fbx", PropReaction.Spin, "Wood"),
        };

        private static readonly Dictionary<string, string> FlaskSwaps = new Dictionary<string, string>
        {
            { "Assets/Models/TriangularFlask/flask_low.fbx", RoomTargetDir + "/Room_FlaskTriangular.prefab" },
            { "Assets/Models/flaskGreen/flask_low.fbx", RoomTargetDir + "/Room_FlaskGreen.prefab" },
        };

        // Name, surface: the rig's glow strips and edge are metal, its bunting fans and swags and the skylight lids are cloth
        private static readonly string[,] RigPieces =
        {
            { "Booth Left", "Metal" }, { "Booth Right", "Metal" }, { "Booth Top", "Metal" }, { "Range Left", "Metal" },
            { "Range Right", "Metal" }, { "Room Edge", "Metal" }, { "Fan Back Left", "Canvas" }, { "Fan Back Middle", "Canvas" },
            { "Fan Back Right", "Canvas" }, { "Fan Side Left", "Canvas" }, { "Fan Side Right", "Canvas" },
            { "Beam Swag 1", "Canvas" }, { "Beam Swag 2", "Canvas" }, { "SkylightLids", "Canvas" },
        };

        private static readonly string[,] BareSurfaces = { { "backdrop", "Wood" }, { "carpet1", "Soft" }, { "Plane", "Wood" } };

        private const float HangingPropMass = 1f;
        private const float CandleMass = 0.2f;
        private const float CandleImpulse = 0.4f;
        // Room round a sign's path that a new collider must keep clear of, so it never blocks a shot at a target
        private const float SignPathMargin = 0.7f;
        // Signs are up to about 1.15 m tall (step 30's report); used where a rail has no tallest sign set
        private const float SignTallestDefault = 1.2f;

        [MenuItem("Shooting Range/Setup/84 - Make The Room Shootable", false, 107)]
        public static void RoomShotsMenu()
        {
            if (!EditorUtility.DisplayDialog("Make the room shootable",
                    "This DELETES the bare flask_low models under Flasks and puts the shattering Room_FlaskGreen or " +
                    "Room_FlaskTriangular in each one's place. It also adds colliders, surfaces and a reaction to Alex's new props, " +
                    "colliders to the old steps you can see, the lighting rig and the skylight lids, and surfaces to the backdrop, " +
                    "carpets and floor. Anything already set is left alone.", "Do it", "Cancel"))
                return;

            Begin("Room shootable");
            try { MakeTheRoomShootable(); }
            catch (System.Exception e) { Fail("Stopped by an error, so nothing after this point ran: " + e); }
            finally { End(); }
        }

        private static void MakeTheRoomShootable()
        {
            Physics.SyncTransforms();
            ReactingProps();
            SwapTheBareFlasks();
            OldStepsYouCanSee();
            SurfacesOnBareColliders();
            CollidersOnTheRig();
            Dirty();
        }

        private static PhysicMaterial PropSurface(string name)
        {
            // The ceramic and soft surfaces live with the room targets' (Materials/Surfaces), the rest with step 83's
            return name == "Ceramic" || name == "Soft" ? SurfaceMaterial(name) : Surface(name);
        }

        private static void Surfaced(GameObject root, string surface)
        {
            var material = PropSurface(surface);
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
                if (collider.sharedMaterial == null) collider.sharedMaterial = material;
        }

        private static void ReactingProps()
        {
            var done = new HashSet<GameObject>();
            var metal = Clips(WorldSounds + "clatter_0", 4);
            var wood = Clips(WorldSounds + "env_wood_0", 4);
            var glass = Clips(GlassSounds, 8);

            foreach (var filter in Object.FindObjectsOfType<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                string model = AssetDatabase.GetAssetPath(filter.sharedMesh);
                var prop = ShootableProps.FirstOrDefault(p => p.Model == model);
                if (prop.Model == null) continue;

                var root = PrefabUtility.GetOutermostPrefabInstanceRoot(filter.gameObject) ?? filter.gameObject;
                if (!done.Add(root)) continue;
                if (root.GetComponentInParent<IShootable>() != null) { Say(root.name + " already reacts to a shot, left alone"); continue; }

                BoxEachMesh(root);
                Surfaced(root, prop.Surface);
                switch (prop.Reaction)
                {
                    case PropReaction.Hang:
                        var hung = RangeSetupUtil.Ensure<Rigidbody>(root);
                        hung.mass = HangingPropMass;
                        hung.isKinematic = true;
                        new Fields(root.AddComponent<HangingOnShot>()).Set("body", hung).SetArray("hitClips", metal).Apply();
                        break;
                    case PropReaction.Shatter:
                        new Fields(root.AddComponent<ShatterOnShot>()).SetArray("shatterClips", glass).Apply();
                        WireBrokenSet(root, System.IO.Path.GetFileNameWithoutExtension(prop.Model), prop.Model);
                        break;
                    case PropReaction.Wobble:
                        root.AddComponent<WobbleOnShot>();
                        break;
                    case PropReaction.Fall:
                        var body = RangeSetupUtil.Ensure<Rigidbody>(root);
                        body.mass = CandleMass;
                        body.isKinematic = true;
                        new Fields(root.AddComponent<ReleaseOnShot>()).SetArray("pieces", new List<Object> { body })
                            .Set("impulse", CandleImpulse).SetArray("hitClips", wood).Apply();
                        break;
                    case PropReaction.Spin:
                        new Fields(root.AddComponent<SpinOnShot>()).Set("spinner", filter.GetComponent<Renderer>()).Apply();
                        break;
                }
                Did(root.name + ": " + prop.Reaction + " when shot, " + prop.Surface.ToLowerInvariant() + " surface");
            }
        }

        /// The same flask model, set up to shatter, in each bare one's place, order and scale
        private static void SwapTheBareFlasks()
        {
            var flasks = RangeSetupUtil.FindInScene("Flasks");
            if (flasks == null) { Say("No Flasks group in the scene"); return; }

            foreach (var bare in flasks.GetComponentsInChildren<Transform>(true).Where(t => t.parent == flasks.transform).ToArray())
            {
                if (bare.GetComponentInChildren<IShootable>() != null) continue;
                var filter = bare.GetComponentInChildren<MeshFilter>();
                string prefabPath;
                if (filter == null || filter.sharedMesh == null || !FlaskSwaps.TryGetValue(AssetDatabase.GetAssetPath(filter.sharedMesh), out prefabPath)) continue;

                var prefab = RangeSetupUtil.Load<GameObject>(prefabPath);
                if (prefab == null) { Fail(prefabPath + " is missing. Step 28 builds it"); continue; }

                var swap = (GameObject)PrefabUtility.InstantiatePrefab(prefab, flasks.transform);
                swap.transform.localPosition = bare.localPosition;
                swap.transform.localRotation = bare.localRotation;
                swap.transform.localScale = bare.localScale;
                swap.transform.SetSiblingIndex(bare.GetSiblingIndex());
                string was = bare.name;
                swap.name = prefab.name + was.Substring(was.IndexOf(" (") >= 0 ? was.IndexOf(" (") : was.Length);
                Object.DestroyImmediate(bare.gameObject);
                Did(was + " is now " + swap.name + ", which shatters");
            }
        }

        /// An old step box gets a collider only if some of it can be seen: buried inside Alex's step art, a collider would
        /// stop shots on art that has its own
        private static void OldStepsYouCanSee()
        {
            foreach (var renderer in Object.FindObjectsOfType<MeshRenderer>())
            {
                if (BaseName(renderer.name) != "step" || !renderer.enabled) continue;
                var root = PrefabUtility.GetOutermostPrefabInstanceRoot(renderer.gameObject) ?? renderer.gameObject;
                if (root.GetComponentInChildren<Collider>() != null) continue;

                if (Buried(renderer.bounds)) { Say(PathOf(root.transform) + " is inside other colliders, so it gets none"); continue; }

                BoxEachMesh(root);
                Surfaced(root, "Wood");
                Did(PathOf(root.transform) + ": a wood collider, takes holes");
            }
        }

        /// Its middle and the four corners of its top, a little in, all inside something else's collider
        private static bool Buried(Bounds b)
        {
            var inset = b.extents * 0.8f;
            var points = new[]
            {
                b.center, b.center + new Vector3(inset.x, inset.y, inset.z), b.center + new Vector3(-inset.x, inset.y, inset.z),
                b.center + new Vector3(inset.x, inset.y, -inset.z), b.center + new Vector3(-inset.x, inset.y, -inset.z),
            };
            return points.All(p => Physics.OverlapSphere(p, 0.001f, ~0, QueryTriggerInteraction.Ignore).Length > 0);
        }

        private static void SurfacesOnBareColliders()
        {
            for (int i = 0; i < BareSurfaces.GetLength(0); i++)
            {
                var material = PropSurface(BareSurfaces[i, 1]);
                foreach (var collider in Object.FindObjectsOfType<Collider>())
                {
                    if (BaseName(collider.name) != BareSurfaces[i, 0] || collider.sharedMaterial != null) continue;
                    collider.sharedMaterial = material;
                    Did(PathOf(collider.transform) + ": " + material.name);
                }
            }
        }

        private static void CollidersOnTheRig()
        {
            for (int i = 0; i < RigPieces.GetLength(0); i++)
            {
                var piece = RangeSetupUtil.FindInScene(RigPieces[i, 0]);
                if (piece == null) { Say("No " + RigPieces[i, 0] + " in the scene"); continue; }
                if (piece.GetComponentInChildren<Collider>() != null) continue;

                var renderers = piece.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) continue;
                var box = BoundsOf(renderers);
                var blocked = BlockingRail(box);
                if (blocked != null) { Fail(piece.name + " would stand in " + blocked + "'s signs' way, so it gets no collider"); continue; }

                BoxEachMesh(piece);
                Surfaced(piece, RigPieces[i, 1]);
                Did(piece.name + ": a collider, " + RigPieces[i, 1].ToLowerInvariant());
            }
        }

        /// Which rail's signs a box would get in the way of, or null. Each rail is walked every 25 cm, with the column a sign
        /// fills there (down from a roof rail, which hangs them upside down, ADR 0039; up from the rest), and the box must keep
        /// SignPathMargin from every point of it. One box round a whole rail would cover half the room
        private static string BlockingRail(Bounds box)
        {
            foreach (var rail in Object.FindObjectsOfType<TrackRail>())
            {
                float tall = Mathf.Max(rail.TallestSign, SignTallestDefault);
                bool hangs = rail.Group == TrackGroup.RoofAcross || rail.Group == TrackGroup.RoofAlong;
                for (float d = 0f; d <= rail.Length + 0.001f; d += 0.25f)
                {
                    var at = rail.PointAt(Mathf.Min(d, rail.Length));
                    for (float h = 0f; h <= tall + 0.001f; h += 0.25f)
                        if (box.SqrDistance(at + (hangs ? Vector3.down : Vector3.up) * h) < SignPathMargin * SignPathMargin) return rail.name;
                }
            }
            return null;
        }
    }
}
