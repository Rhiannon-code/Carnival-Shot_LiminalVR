using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    public static partial class RangeSetup
    {
        [MenuItem("Shooting Range/Setup/27 - Light The Range", false, 48)]
        public static void LightingMenu() { Begin("Lighting"); LightTheRange(); End(); }

        [MenuItem("Shooting Range/Setup/27b - Bake The Lighting", false, 49)]
        public static void BakeMenu()
        {
            if (Lightmapping.isRunning) { Debug.LogWarning("[RangeSetup] A bake is already running"); return; }

            Lightmapping.completed -= SaveTheBake;
            Lightmapping.completed += SaveTheBake;

            if (Lightmapping.BakeAsync())
                Debug.Log("[RangeSetup] Baking the lighting. Progress shows bottom right; the scene saves itself when it's done");
            else
            {
                Lightmapping.completed -= SaveTheBake;
                Debug.LogWarning("[RangeSetup] The bake did not start. Check the console for lightmapper errors");
            }
        }

        /// The lighting data is only linked to the scene once the scene is saved, so a bake left unsaved is lost
        private static void SaveTheBake()
        {
            Lightmapping.completed -= SaveTheBake;

            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene))
                Debug.Log("[RangeSetup] Lighting baked and the scene saved");
            else
                Debug.LogWarning("[RangeSetup] Lighting baked, but the scene could not be saved. Save it by hand or the bake is lost");
        }

        private const string LightingName = "RangeLighting";
        private const string GlowStripPath = "Assets/Materials/SR_GlowStrip.mat";
        private const float AssumedCeiling = 3.8f;

        private static readonly Color BulbColour = new Color(1f, 0.86f, 0.68f);
        private const float BulbIntensity = 1f;
        private const float BulbRange = 6f;
        private const float BulbSoftness = 0.5f;
        private const float BulbSpacing = 3f;
        private const float BulbRowX = 2.5f;
        private const float BulbBelowCeiling = 0.4f;

        private static readonly Color GlowColour = new Color(1f, 0.15f, 0.1f);
        private const float GlowIntensity = 2.5f;
        private const float GlowRange = 1.8f;
        private const float GlowSoftness = 0.2f;
        private const float GlowSpacing = 2f;
        private const float GlowAboveRail = 0.15f;
        private const float GlowStripHeight = 0.4f;
        private const float GlowStripBehindRail = 0.12f;
        private const float LevelTolerance = 0.25f;

        private const float ProbeSpacing = 1.5f;
        private static readonly float[] ProbeHeights = { 0.3f, 1.1f, 1.9f, 2.7f, 3.4f };
        private static readonly float[] RailProbeHeights = { 0.25f, 0.75f };

        private const int UnityDefaultBakeResolution = 40;
        private const int QuestBakeResolution = 15;

        private static void LightTheRange()
        {
            SetTheBake();
            SetTheAmbient();
            var room = MarkTheRoomStatic();
            GenerateLightmapUVs(room);
            LayTheLights();
            ReportOtherLights();
            Say("Next: 27b - Bake The Lighting. Nothing is lit until that bake runs");
        }

        /// Only what is still at Unity's default is changed, so a hand-tuned bake survives a re-run
        private static void SetTheBake()
        {
            if (Lightmapping.realtimeGI)
            {
                Lightmapping.realtimeGI = false;
                Did("Realtime GI off: nothing in the room moves, and Enlighten costs CPU on Quest every frame");
            }

            if (!Lightmapping.bakedGI)
            {
                Lightmapping.bakedGI = true;
                Did("Baked GI on");
            }

            if (Mathf.RoundToInt(LightmapEditorSettings.bakeResolution) == UnityDefaultBakeResolution)
            {
                LightmapEditorSettings.bakeResolution = QuestBakeResolution;
                Did("Lightmap resolution " + UnityDefaultBakeResolution + " -> " + QuestBakeResolution +
                    " texels per metre: smaller lightmaps for Quest, still sharp for a room this size");
            }
            else Say("Lightmap resolution is " + LightmapEditorSettings.bakeResolution + ", left alone");

            if (LightmapEditorSettings.lightmapsMode == LightmapsMode.CombinedDirectional)
            {
                LightmapEditorSettings.lightmapsMode = LightmapsMode.NonDirectional;
                Did("Lightmaps non-directional: half the texture memory, and the Standard shader's normal maps " +
                    "barely show at this resolution anyway");
            }

            if (!LightmapEditorSettings.enableAmbientOcclusion)
            {
                LightmapEditorSettings.enableAmbientOcclusion = true;
                Did("Baked ambient occlusion on, so props sit on the floor instead of floating");
            }

            if (Lightmapping.giWorkflowMode == Lightmapping.GIWorkflowMode.Iterative)
                Say("Auto Generate is on, so the editor re-bakes after every change. Untick it in the Lighting " +
                    "window if that gets slow");

            Dirty();
        }

        /// Indoors under a roof, the sky barely reaches the room in a bake; this is the floor the room never
        /// drops below, so nothing goes black between the bulbs
        private static void SetTheAmbient()
        {
            if (RenderSettings.ambientMode != AmbientMode.Skybox)
            {
                Say("Ambient is already " + RenderSettings.ambientMode + ", left alone");
                return;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.40f, 0.50f);
            RenderSettings.ambientEquatorColor = new Color(0.30f, 0.27f, 0.33f);
            RenderSettings.ambientGroundColor = new Color(0.16f, 0.13f, 0.15f);
            Dirty();
            Did("Ambient set to a soft lavender gradient (Lighting window > Environment Lighting to tune)");
        }

        /// The room is every opaque mesh with nothing of ours, the SDK's or the physics' above it. Signs, guns,
        /// props, the HUD and the coffin all carry one of those, so they stay dynamic and are lit by the probes
        private static List<MeshRenderer> MarkTheRoomStatic()
        {
            var room = new List<MeshRenderer>();
            int marked = 0;

            foreach (var renderer in Object.FindObjectsOfType<MeshRenderer>())
            {
                if (!IsRoom(renderer)) continue;
                room.Add(renderer);

                var go = renderer.gameObject;
                var flags = GameObjectUtility.GetStaticEditorFlags(go);
                if ((flags & StaticEditorFlags.LightmapStatic) != 0) continue;

                GameObjectUtility.SetStaticEditorFlags(go, flags | StaticEditorFlags.LightmapStatic);
                marked++;
            }

            if (marked > 0) Dirty();
            Did(room.Count + " room meshes are lightmapped (" + marked + " newly marked Lightmap Static)");
            return room;
        }

        private static bool IsRoom(MeshRenderer renderer)
        {
            foreach (var material in renderer.sharedMaterials)
            {
                if (material != null && material.renderQueue >= (int)RenderQueue.Transparent) return false;
            }

            for (var t = renderer.transform; t != null; t = t.parent)
            {
                if (t.GetComponent<Rigidbody>() != null || t.GetComponent<Canvas>() != null ||
                    t.GetComponent<Animator>() != null || t.GetComponent<Animation>() != null) return false;

                foreach (var behaviour in t.GetComponents<MonoBehaviour>())
                {
                    if (behaviour == null) continue;

                    string space = behaviour.GetType().Namespace ?? "";
                    if (space.StartsWith("IntuitiveDesigns") || space.StartsWith("Liminal")) return false;
                }
            }

            return true;
        }

        /// The room's models carry one UV set, laid out for their textures with overlaps, which a lightmap
        /// can't use. Unity unwraps a second set on import; unticking Generate Lightmap UVs undoes it
        private static void GenerateLightmapUVs(List<MeshRenderer> room)
        {
            var importers = new HashSet<ModelImporter>();
            foreach (var renderer in room)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;

                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(filter.sharedMesh)) as ModelImporter;
                if (importer != null && !importer.generateSecondaryUV) importers.Add(importer);
            }

            foreach (var importer in importers)
            {
                importer.generateSecondaryUV = true;
                importer.SaveAndReimport();
                Did("Generate Lightmap UVs on " + importer.assetPath);
            }
        }

        /// Bulbs, Underglow and Probes under RangeLighting are each made once and then left alone, like the ground
        /// fog: retune them by hand, or delete one and re-run to lay just that part out again from the rails
        private static void LayTheLights()
        {
            var rails = new List<TrackRail>();
            foreach (var rail in Object.FindObjectsOfType<TrackRail>())
            {
                if (rail.Group == TrackGroup.FloorAcross || rail.Group == TrackGroup.FloorAlong) rails.Add(rail);
            }

            if (rails.Count == 0) { Fail("No floor rails in the scene, so there is nowhere to put the underglow"); return; }

            var eye = EyePoint();
            float near = eye.z, far = eye.z, width = 0f;
            foreach (var rail in rails)
            {
                foreach (var end in new[] { rail.PointAt(0f), rail.PointAt(rail.Length) })
                {
                    far = Mathf.Max(far, end.z);
                    width = Mathf.Max(width, Mathf.Abs(end.x));
                }
            }

            var found = RangeSetupUtil.FindInScene(LightingName);
            var root = found != null ? found.transform : new GameObject(LightingName).transform;

            float ceiling = RoomSurface(new Vector3(0f, eye.y, (near + far) * 0.5f), Vector3.up, float.NaN);
            if (float.IsNaN(ceiling))
            {
                ceiling = AssumedCeiling;
                Say("No ceiling collider over the range, so the bulbs hang from an assumed " + AssumedCeiling + " m roof");
            }

            if (Part(root, "Bulbs")) LayTheBulbs(root, near, far, ceiling);
            if (Part(root, "Underglow")) LayTheUnderglow(root, FrontRailPerLevel(rails, eye), eye);
            if (Part(root, "Probes")) LayTheProbes(root, rails, near, far, width, ceiling);
        }

        /// True when this part of RangeLighting still needs making
        private static bool Part(Transform root, string name)
        {
            if (RangeSetupUtil.Child(root, name) == null) return true;

            Say(LightingName + "/" + name + " is already there, left alone. Delete it and re-run to lay it out again");
            return false;
        }

        private static void LayTheBulbs(Transform root, float near, float far, float ceiling)
        {
            var bulbs = new GameObject("Bulbs").transform;
            bulbs.SetParent(root, false);
            int count = 0;
            for (float z = near; z <= far + 0.01f; z += BulbSpacing)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    BakedLight(bulbs, "Bulb", new Vector3(side * BulbRowX, ceiling - BulbBelowCeiling, z),
                               BulbColour, BulbIntensity, BulbRange, BulbSoftness);
                    count++;
                }
            }

            Dirty();
            Did(count + " warm bulbs under the roof");
        }

        /// One level is every floor rail at about the same height. Only the rail nearest the player on each level
        /// glows, so each tier gets a single red line rather than one per rail
        private static List<TrackRail> FrontRailPerLevel(List<TrackRail> rails, Vector3 eye)
        {
            var front = new List<TrackRail>();
            foreach (var rail in rails)
            {
                int level = front.FindIndex(other => Mathf.Abs(other.Origin.y - rail.Origin.y) < LevelTolerance);
                if (level < 0) front.Add(rail);
                else if (FlatDistance(rail, eye) < FlatDistance(front[level], eye)) front[level] = rail;
            }

            return front;
        }

        private static float FlatDistance(TrackRail rail, Vector3 eye)
        {
            var offset = rail.PointAt(rail.Length * 0.5f) - eye;
            offset.y = 0f;
            return offset.magnitude;
        }

        private static void LayTheUnderglow(Transform root, List<TrackRail> rails, Vector3 eye)
        {
            var strip = GlowStripMaterial();
            if (strip == null) { Fail("The Glow Strip shader has not compiled. Check the console for shader errors"); return; }

            var glow = new GameObject("Underglow").transform;
            glow.SetParent(root, false);
            int count = 0;
            var buried = new List<string>();
            foreach (var rail in rails)
            {
                int lights = Mathf.Max(1, Mathf.RoundToInt(rail.Length / GlowSpacing));
                for (int i = 0; i < lights; i++)
                {
                    var at = rail.PointAt((i + 0.5f) * rail.Length / lights) + Vector3.up * GlowAboveRail;
                    BakedLight(glow, "Red", at, GlowColour, GlowIntensity, GlowRange, GlowSoftness);
                    count++;

                    if (Physics.CheckSphere(at, 0.05f, ~0, QueryTriggerInteraction.Ignore)) buried.Add(rail.name);
                }

                GlowStrip(glow, rail, eye, strip);
            }

            var names = rails.ConvertAll(rail => rail.name).ToArray();
            Dirty();
            Did("Underglow on the front floor rail of each level (" + string.Join(", ", names) + "): " + count +
                " red lights and " + rails.Count + " glow strips");
            if (buried.Count > 0)
                Say("   ^ some red lights sit inside a collider on " + string.Join(", ", buried.ToArray()) +
                    ". A light inside a mesh bakes nothing; lift those a little");
        }

        private static void BakedLight(Transform parent, string name, Vector3 position, Color colour,
                                       float intensity, float range, float softness)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = colour;
            light.intensity = intensity;
            light.range = range;
            light.lightmapBakeType = LightmapBakeType.Baked;
            light.shadows = LightShadows.Soft;
            light.shadowRadius = softness;
        }

        /// Stands along the rail just behind the sign's plane, so a rising sign comes up in front of its glow,
        /// and the cover in front hides all but what spills over the top
        private static void GlowStrip(Transform parent, TrackRail rail, Vector3 eye, Material material)
        {
            var across = Vector3.Cross(rail.Direction, Vector3.up).normalized;
            var middle = rail.PointAt(rail.Length * 0.5f);
            var away = middle - eye;
            away.y = 0f;
            if (Vector3.Dot(across, away) < 0f) across = -across;

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.name = "GlowStrip " + rail.name;
            quad.transform.SetParent(parent, false);
            quad.transform.position = middle + across * GlowStripBehindRail + Vector3.up * GlowStripHeight * 0.5f;
            quad.transform.rotation = Quaternion.LookRotation(across, Vector3.up);
            quad.transform.localScale = new Vector3(rail.Length, GlowStripHeight, 1f);

            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static Material GlowStripMaterial()
        {
            var shader = Shader.Find("CrystalCatch/Glow Strip");
            if (shader == null) return null;

            var material = RangeSetupUtil.Load<Material>(GlowStripPath);
            if (material != null) return material;

            material = new Material(shader);
            material.SetColor("_Color", GlowColour);
            AssetDatabase.CreateAsset(material, GlowStripPath);
            Did("Made " + System.IO.Path.GetFileName(GlowStripPath) + ", tune the strips' colour and strength there");
            return material;
        }

        /// Signs, guns and gloves move, so they take their light from probes. A coarse grid covers the room
        /// and a tighter row runs along each floor rail, where a sign picks up the red as it rises
        private static void LayTheProbes(Transform root, List<TrackRail> rails, float near, float far, float width,
                                         float ceiling)
        {
            var positions = new List<Vector3>();
            float floor = FloorUnder(new Vector3(0f, 2f, (near + far) * 0.5f));
            float half = width + 1f;

            for (float z = near - 1f; z <= far + 1.51f; z += ProbeSpacing)
            {
                for (float x = -half; x <= half + 0.01f; x += ProbeSpacing)
                {
                    foreach (float height in ProbeHeights)
                    {
                        if (floor + height < ceiling - 0.1f) positions.Add(new Vector3(x, floor + height, z));
                    }
                }
            }

            foreach (var rail in rails)
            {
                for (float d = 0f; d <= rail.Length + 0.01f; d += 1f)
                {
                    foreach (float height in RailProbeHeights) positions.Add(rail.PointAt(d) + Vector3.up * height);
                }
            }

            var group = new GameObject("Probes");
            group.transform.SetParent(root, false);
            group.AddComponent<LightProbeGroup>().probePositions = positions.ToArray();

            Dirty();
            Did(positions.Count + " light probes for the signs and guns");
        }

        private static void ReportOtherLights()
        {
            foreach (var light in Object.FindObjectsOfType<Light>())
            {
                if (InLighting(light.transform)) continue;

                string problem = light.type == LightType.Directional
                    ? "a directional light, which the roof blocks in a bake"
                    : light.range < 0.5f ? "range " + light.range + ", so it lights next to nothing" : "";
                Say("Also in the scene, left alone: " + light.name + " (" + light.lightmapBakeType + ")" +
                    (problem.Length > 0 ? ", " + problem : ". It bakes along with the rest, switch it off if it fights the new lights"));
            }
        }

        private static bool InLighting(Transform item)
        {
            for (var t = item; t != null; t = t.parent)
            {
                if (t.name == LightingName) return true;
            }

            return false;
        }
    }
}
