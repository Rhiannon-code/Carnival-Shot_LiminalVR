using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Liminal.SDK.VR.Avatars;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    public static partial class RangeSetup
    {
        private const string StringLightsName = "StringLights";
        private const string StringBulbShader = "CrystalCatch/String Bulbs";
        private const string StringBulbMaterialPath = "Assets/Materials/SR_StringBulbs.mat";
        private const string WireMaterialPath = "Assets/Materials/SR_StringWire.mat";
        private const string StringMeshDir = "Assets/Meshes/StringLights";
        private const string DarkBakeDir = "Assets/Lighting/Dark";
        private const string PartBakeDir = "Assets/Lighting/Strings";
        private const string BakeFailureText = "Gather resources failure";
        private const int BakeLimitSeconds = 8 * 60;
        private const double LogCheckSeconds = 2;
        private const int LogReadBytes = 1 << 20;
        private const float StartUpStringGap = 0.15f;
        private const float StartUpMusicSeconds = 7.5f;
        private const string HumPlaceholder = "Assets/Sounds/ShootingRange/World/lights_buzz_01.wav";

        private static readonly Color StringColour = new Color(1f, 0.78f, 0.45f);
        private const float StringLightIntensity = 1f;
        private const float StringLightRange = 4f;
        private const float StringLightSoftness = 0.3f;
        private const string FanPrefix = "Fan ";
        private const string BoothPrefix = "Booth ";
        private const float BoothLightRange = 2.5f;
        private const float FanLightIntensity = 0.65f;
        private const float FanLightRange = 3.5f;
        private const float FanBelowCeiling = 0.35f;
        private const float FanMeetGap = 0.1f;
        private const int FanFirstLight = 7;
        private const string FanFillName = "Fill";
        private const float FanFillY = 2.2f;
        private const float FanFillSideX = 3f;
        private const float FanFillSideZ = 0.5f;
        private const float FanFillMiddleZ = 1f;
        private const float FanFillIntensity = 1.2f;
        private const float FanFillRange = 4.25f;
        private const float FanSideX = 4.3f;
        private const float FanSideZ = 3.5f;
        private const float FanBackX = 3.6f;
        private const float FanOffBackWall = 0.4f;
        private const float FanBackFallbackZ = -4.4f;
        private const int StringLightEvery = 4;

        private const float StringBulbSpacing = 0.3f;
        private const float StringBulbSize = 0.03f;
        private const float StringWireWidth = 0.01f;
        private const float StringSagPerMetre = 0.06f;
        private const float StringHookSpacing = 2f;
        private const float StringFront = 0.06f;
        private const float BoothStringBelowCeiling = 0.25f;
        private const float BoothStringBottom = 1.2f;
        private const float CeilingSearchFrom = 3.2f;
        private const float RangeStringX = 2.5f;
        private const float RangeStringBelowCeiling = 0.4f;

        private static readonly Color DarkSky = new Color(0.04f, 0.036f, 0.045f);
        private static readonly Color DarkEquator = new Color(0.027f, 0.024f, 0.029f);
        private static readonly Color DarkGround = new Color(0.016f, 0.013f, 0.015f);

        private const float DarkBakeAmbient = 0.15f;
        private const float AODirect = 0.6f;

        [MenuItem("Shooting Range/Setup/37 - String The Carnival Lights", false, 60)]
        public static void StringLightsMenu()
        {
            if (!EditorUtility.DisplayDialog("String the carnival lights",
                    "This hangs warm string lights round the booth front (along the top of the opening and down both " +
                    "pillars) and two strings down the range, each with baked lights along it, and DELETES " +
                    LightingName + "/Bulbs, the invisible warm lights under the roof they replace. The scene is saved " +
                    "afterwards; bake with step 39.", "String them", "Cancel"))
                return;

            Begin("String lights");
            StringTheCarnivalLights();
            End();
        }

        private static void StringTheCarnivalLights()
        {
            var found = RangeSetupUtil.FindInScene(LightingName);
            var root = found != null ? found.transform : new GameObject(LightingName).transform;
            var hung = RangeSetupUtil.Child(root, StringLightsName);
            if (hung != null)
            {
                Say(LightingName + "/" + StringLightsName + " is already there, so only its brightness is brought up to date. " +
                    "Delete it and re-run to hang them again");
                SetStringBrightness(hung);
                WireTheCarnivalLights(root);
                return;
            }

            var bulbMaterial = ShaderMaterial(StringBulbMaterialPath, StringBulbShader, StringColour);
            var wireMaterial = ShaderMaterial(WireMaterialPath, "Unlit/Color", new Color(0.05f, 0.04f, 0.04f));
            if (bulbMaterial == null || wireMaterial == null) return;

            var left = RangeSetupUtil.FindInScene(SidePillars[0, 1]);
            var right = RangeSetupUtil.FindInScene(SidePillars[1, 1]);
            var leftBox = left != null ? left.GetComponentInChildren<MeshRenderer>() : null;
            var rightBox = right != null ? right.GetComponentInChildren<MeshRenderer>() : null;
            if (leftBox == null || rightBox == null) { Fail("No " + SidePillars[0, 1] + " and " + SidePillars[1, 1] + " to hang the booth front from"); return; }

            var old = RangeSetupUtil.Child(root, "Bulbs");
            if (old != null) { Object.DestroyImmediate(old.gameObject); Did("Deleted " + LightingName + "/Bulbs: the string lights replace them"); }

            System.IO.Directory.CreateDirectory(StringMeshDir);
            var strings = new GameObject(StringLightsName).transform;
            strings.SetParent(root, false);

            // The inner front corners of the opening, just in front of the pillars
            float frontZ = Mathf.Min(leftBox.bounds.min.z, rightBox.bounds.min.z) - StringFront;
            var leftTop = UnderCeiling(new Vector3(leftBox.bounds.max.x, 0f, frontZ), BoothStringBelowCeiling);
            var rightTop = UnderCeiling(new Vector3(rightBox.bounds.min.x, 0f, frontZ), BoothStringBelowCeiling);

            int lights = 0;
            lights += HangString(strings, "Booth Top", Hooks(leftTop, rightTop, BoothStringBelowCeiling), bulbMaterial, wireMaterial, StringLightIntensity, BoothLightRange);
            lights += HangString(strings, "Booth Left", new List<Vector3> { leftTop, new Vector3(leftTop.x, BoothStringBottom, frontZ) }, bulbMaterial, wireMaterial, StringLightIntensity, BoothLightRange);
            lights += HangString(strings, "Booth Right", new List<Vector3> { rightTop, new Vector3(rightTop.x, BoothStringBottom, frontZ) }, bulbMaterial, wireMaterial, StringLightIntensity, BoothLightRange);

            lights += HangTheRangeStrings(strings, frontZ, bulbMaterial, wireMaterial);

            Did("String lights hung with " + lights + " baked warm lights. Bake with step 39");
            WireTheCarnivalLights(root);
            Dirty();
        }

        private static int HangTheRangeStrings(Transform strings, float frontZ, Material bulbMaterial, Material wireMaterial)
        {
            float backZ = frontZ;
            foreach (var rail in Object.FindObjectsOfType<TrackRail>())
                backZ = Mathf.Max(backZ, Mathf.Max(rail.PointAt(0f).z, rail.PointAt(rail.Length).z));

            int lights = 0;
            for (int side = -1; side <= 1; side += 2)
            {
                var from = UnderCeiling(new Vector3(side * RangeStringX, 0f, frontZ), RangeStringBelowCeiling);
                var to = UnderCeiling(new Vector3(side * RangeStringX, 0f, backZ), RangeStringBelowCeiling);
                lights += HangString(strings, side < 0 ? "Range Left" : "Range Right", Hooks(from, to, RangeStringBelowCeiling),
                                     bulbMaterial, wireMaterial);
            }

            return lights;
        }

        [MenuItem("Shooting Range/Setup/46 - Fan The Strings Over The Room", false, 69)]
        public static void FanTheStringsMenu()
        {
            if (!EditorUtility.DisplayDialog("Fan the strings over the room",
                    "This DELETES and hangs again every Fan string and the Range Left and Range Right strings: 5 strings fan out from " +
                    "the top of the booth, to the sides and right back to the back wall. It also tightens the start-up timing to " +
                    "suit. The scene is saved afterwards; try 39d for the look, then step 39, which is now about 30 minutes.",
                    "Hang them", "Cancel"))
                return;

            Begin("Fan the strings");
            FanTheStrings();
            End();
        }

        private static void FanTheStrings()
        {
            var found = RangeSetupUtil.FindInScene(LightingName);
            var strings = found != null ? RangeSetupUtil.Child(found.transform, StringLightsName) : null;
            var boothTop = strings != null ? RangeSetupUtil.Child(strings, "Booth Top") : null;
            var marker = boothTop != null ? boothTop.GetComponentInChildren<Light>() : null;
            if (marker == null) { Fail("No " + StringLightsName + "/Booth Top to fan out from. Run step 37"); return; }

            var bulbMaterial = ShaderMaterial(StringBulbMaterialPath, StringBulbShader, StringColour);
            var wireMaterial = ShaderMaterial(WireMaterialPath, "Unlit/Color", new Color(0.05f, 0.04f, 0.04f));
            if (bulbMaterial == null || wireMaterial == null) return;

            float frontZ = marker.transform.position.z;
            foreach (var name in new[] { "Range Left", "Range Right" })
            {
                var old = RangeSetupUtil.Child(strings, name);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }

            for (int i = strings.childCount - 1; i >= 0; i--)
            {
                var holder = strings.GetChild(i);
                if (holder.name.StartsWith(FanPrefix, System.StringComparison.Ordinal)) Object.DestroyImmediate(holder.gameObject);
            }

            int lights = HangTheRangeStrings(strings, frontZ, bulbMaterial, wireMaterial);
            Did("Range strings hung again, under the real ceiling");

            float backZ = BackWallZ();
            var fan = new[]
            {
                new KeyValuePair<string, Vector2>(FanPrefix + "Side Left", new Vector2(-FanSideX, FanSideZ)),
                new KeyValuePair<string, Vector2>(FanPrefix + "Back Left", new Vector2(-FanBackX, backZ)),
                new KeyValuePair<string, Vector2>(FanPrefix + "Back Middle", new Vector2(0f, backZ)),
                new KeyValuePair<string, Vector2>(FanPrefix + "Back Right", new Vector2(FanBackX, backZ)),
                new KeyValuePair<string, Vector2>(FanPrefix + "Side Right", new Vector2(FanSideX, FanSideZ)),
            };

            foreach (var pair in fan)
            {
                var meet = UnderCeiling(new Vector3(pair.Value.x < 0f ? -FanMeetGap : FanMeetGap, 0f, frontZ), FanBelowCeiling);
                var end = UnderCeiling(new Vector3(pair.Value.x, 0f, pair.Value.y), FanBelowCeiling);
                lights += HangString(strings, pair.Key, Hooks(meet, end, FanBelowCeiling), bulbMaterial, wireMaterial,
                                     FanLightIntensity, FanLightRange, FanFirstLight);
            }

            HangTheRoomFill(strings);
            Did(lights + " baked warm lights hung");
            SetStringBrightness(strings);
            WireTheCarnivalLights(found.transform);

            new Fields(found.GetComponent<CarnivalLights>()).Set("stringGap", StartUpStringGap).Apply();
            var music = Object.FindObjectOfType<MusicBed>();
            if (music != null) new Fields(music).Set("windUpSeconds", StartUpMusicSeconds).Apply();
            Did("Start-up: " + StartUpStringGap + " s between strings, music winds up over " + StartUpMusicSeconds + " s");
            Dirty();
        }

        /// The fan hangs too high to reach the floor round the player, and giving its lights the range to would make them
        /// all overlap at the booth, where the lightmapper fails. These few sit low, and come on with the back strings
        private static void HangTheRoomFill(Transform strings)
        {
            var fills = new[]
            {
                new KeyValuePair<string, Vector3>(FanPrefix + "Back Left", new Vector3(-FanFillSideX, FanFillY, FanFillSideZ)),
                new KeyValuePair<string, Vector3>(FanPrefix + "Back Middle", new Vector3(0f, FanFillY, FanFillMiddleZ)),
                new KeyValuePair<string, Vector3>(FanPrefix + "Back Right", new Vector3(FanFillSideX, FanFillY, FanFillSideZ)),
            };

            foreach (var fill in fills)
            {
                var holder = RangeSetupUtil.Child(strings, fill.Key);
                var parent = holder != null ? RangeSetupUtil.Child(holder, "Lights") : null;
                if (parent == null) { Say("No " + fill.Key + " lights to hang a fill light with"); continue; }

                BakedLight(parent, FanFillName, fill.Value, StringColour, FanFillIntensity, FanFillRange, StringLightSoftness);
            }

            Did(fills.Length + " fill lights low over the player's area");
        }

        /// Straight back from the middle of the room, above the props, to the first solid thing behind the player
        private static float BackWallZ()
        {
            float nearest = float.MaxValue;
            float wall = FanBackFallbackZ;
            foreach (var hit in Physics.RaycastAll(new Vector3(0f, 2.8f, 0f), Vector3.back, 15f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance >= nearest || hit.rigidbody != null || hit.collider.GetComponentInParent<IShootable>() != null) continue;

                nearest = hit.distance;
                wall = hit.point.z + FanOffBackWall;
            }

            Did(nearest < float.MaxValue ? "Back wall found, fan strings end at z " + wall.ToString("0.00")
                                         : "No back wall found, so fan strings end at the fallback z " + wall);
            return wall;
        }

        private static void SetStringBrightness(Transform strings)
        {
            int changed = 0;
            foreach (Transform holder in strings)
            {
                bool fan = holder.name.StartsWith(FanPrefix, System.StringComparison.Ordinal);
                float intensity = fan ? FanLightIntensity : StringLightIntensity;
                float range = fan ? FanLightRange
                            : holder.name.StartsWith(BoothPrefix, System.StringComparison.Ordinal) ? BoothLightRange : StringLightRange;

                foreach (var light in holder.GetComponentsInChildren<Light>(true))
                {
                    if (light.name == FanFillName) continue;
                    if (Mathf.Approximately(light.intensity, intensity) && Mathf.Approximately(light.range, range)) continue;

                    light.intensity = intensity;
                    light.range = range;
                    changed++;
                }
            }

            if (changed == 0) { Say("The string lights are already at " + StringLightIntensity); return; }

            Dirty();
            Did(changed + " string lights set to " + StringLightIntensity + ". Bake again (step 39) to see it");
        }

        /// Hook points every couple of metres, each just under the ceiling above it, so a string follows the roof
        private static List<Vector3> Hooks(Vector3 from, Vector3 to, float below)
        {
            var hooks = new List<Vector3>();
            int spans = Mathf.Max(1, Mathf.RoundToInt(Vector3.Distance(from, to) / StringHookSpacing));
            for (int i = 0; i <= spans; i++)
                hooks.Add(i == 0 ? from : i == spans ? to : UnderCeiling(Vector3.Lerp(from, to, i / (float)spans), below));
            return hooks;
        }

        private static Vector3 UnderCeiling(Vector3 at, float below)
        {
            // From above the tallest step: from 2 m, the underside of a step passed for the ceiling and a string dipped to it
            float ceiling = RoomSurface(new Vector3(at.x, CeilingSearchFrom, at.z), Vector3.up, AssumedCeiling);
            return new Vector3(at.x, ceiling - below, at.z);
        }

        /// The wire sags between hooks; a straight drop (down a pillar) hangs straight. Returns how many lights it has
        private static int HangString(Transform parent, string name, List<Vector3> hooks, Material bulbMaterial, Material wireMaterial,
                                      float intensity = StringLightIntensity, float range = StringLightRange, int firstLight = 0)
        {
            var path = new List<Vector3>();
            for (int h = 0; h + 1 < hooks.Count; h++)
            {
                var a = hooks[h];
                var b = hooks[h + 1];
                float across = new Vector2(b.x - a.x, b.z - a.z).magnitude;
                float sag = StringSagPerMetre * across;
                for (int s = h == 0 ? 0 : 1; s <= 20; s++)
                {
                    float t = s / 20f;
                    path.Add(Vector3.Lerp(a, b, t) + Vector3.down * 4f * sag * t * (1f - t));
                }
            }

            var holder = new GameObject(name).transform;
            holder.SetParent(parent, false);

            var wire = new GameObject("Wire").AddComponent<LineRenderer>();
            wire.transform.SetParent(holder, false);
            wire.useWorldSpace = true;
            wire.positionCount = path.Count;
            wire.SetPositions(path.ToArray());
            wire.widthMultiplier = StringWireWidth;
            wire.sharedMaterial = wireMaterial;
            wire.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            wire.receiveShadows = false;

            var spots = Along(path, StringBulbSpacing);
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            foreach (var spot in spots) Diamond(spot, StringBulbSize, 0f, vertices, uvs, triangles);

            var mesh = new Mesh { name = "StringBulbs " + name };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, StringMeshDir + "/" + name.Replace(" ", "") + ".asset");

            var bulbs = new GameObject("Bulbs");
            bulbs.transform.SetParent(holder, false);
            bulbs.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = bulbs.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = bulbMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            var lights = new GameObject("Lights").transform;
            lights.SetParent(holder, false);
            int count = 0;
            for (int i = firstLight; i < spots.Count; i += StringLightEvery)
            {
                BakedLight(lights, "Light", spots[i] + Vector3.down * 0.05f, StringColour, intensity, range, StringLightSoftness);
                count++;
            }

            return count;
        }

        /// Evenly spaced points along a polyline
        private static List<Vector3> Along(List<Vector3> path, float spacing)
        {
            var spots = new List<Vector3> { path[0] };
            float carried = 0f;
            for (int i = 1; i < path.Count; i++)
            {
                var a = path[i - 1];
                var b = path[i];
                float length = Vector3.Distance(a, b);
                float d = spacing - carried;
                while (d <= length)
                {
                    spots.Add(Vector3.Lerp(a, b, d / length));
                    d += spacing;
                }

                carried = length - (d - spacing);
            }

            return spots;
        }

        private static void WireTheCarnivalLights(Transform root)
        {
            var lights = RangeSetupUtil.Ensure<CarnivalLights>(root.gameObject);
            var bulbs = new List<Object>();
            var strings = RangeSetupUtil.Child(root, StringLightsName);
            if (strings != null)
            {
                foreach (var renderer in strings.GetComponentsInChildren<MeshRenderer>(true)) bulbs.Add(renderer);
            }

            var fogPlanes = new List<Object>();
            var groundFog = RangeSetupUtil.FindInScene(FogName);
            if (groundFog != null)
            {
                foreach (var renderer in groundFog.GetComponentsInChildren<Renderer>(true)) fogPlanes.Add(renderer);
            }

            var fields = new Fields(lights).SetArray("bulbs", bulbs).SetArray("fog", fogPlanes);
            if (fields.Ref("game") == null) fields.Set("game", Object.FindObjectOfType<RangeGame>());
            fields.Apply();

            WireLightSounds(lights);
            Did("Carnival Lights on " + root.name + " drives " + bulbs.Count + " strings");
        }

        private static void WireLightSounds(CarnivalLights lights)
        {
            int wired = Fill(lights, "clunkClips", "lights_clunk") + Fill(lights, "buzzClips", "lights_buzz") +
                        Fill(lights, "popClips", "lights_pop");
            if (wired > 0) Did("Carnival Lights: " + wired + " sound slots filled");

            var fields = new Fields(lights);
            if (fields.Ref("humClip") == null)
            {
                fields.Set("humClip", RangeSetupUtil.Load<AudioClip>(HumPlaceholder)).Apply();
                Did("Carnival Lights: start-up hum is " + HumPlaceholder + " as a placeholder, slowed down");
            }
        }

        private static Material ShaderMaterial(string path, string shaderName, Color colour)
        {
            var material = RangeSetupUtil.Load<Material>(path);
            if (material != null) return material;

            var shader = Shader.Find(shaderName);
            if (shader == null) { Fail(shaderName + " has not compiled. Check the console for shader errors"); return null; }

            material = new Material(shader) { color = colour };
            AssetDatabase.CreateAsset(material, path);
            Did("Made " + System.IO.Path.GetFileName(path));
            return material;
        }

        [MenuItem("Shooting Range/Setup/38 - Deepen The Shadows", false, 61)]
        public static void DeepenShadowsMenu()
        {
            if (!EditorUtility.DisplayDialog("Deepen the shadows",
                    "This changes the scene's lighting settings, each logged old -> new in the Console:\n\n" +
                    "- ambient sky, equator and ground colours down to about a third\n" +
                    "- baked ambient occlusion also darkens direct light (exponent " + AODirect + "), for the shadows under props\n" +
                    "- room props that stay put are marked Lightmap Static, so they cast baked shadows. Anything that is " +
                    "thrown, shatters or moves on a rail stays dynamic.\n\nThe scene is saved afterwards; bake with step 39.",
                    "Deepen them", "Cancel"))
                return;

            Begin("Deeper shadows");
            DeepenTheShadows();
            End();
        }

        private static void DeepenTheShadows()
        {
            SetColour("Ambient sky", RenderSettings.ambientSkyColor, DarkSky, c => RenderSettings.ambientSkyColor = c);
            SetColour("Ambient equator", RenderSettings.ambientEquatorColor, DarkEquator, c => RenderSettings.ambientEquatorColor = c);
            SetColour("Ambient ground", RenderSettings.ambientGroundColor, DarkGround, c => RenderSettings.ambientGroundColor = c);

            if (!LightmapEditorSettings.enableAmbientOcclusion)
            {
                LightmapEditorSettings.enableAmbientOcclusion = true;
                Did("Baked ambient occlusion on");
            }

            if (LightmapEditorSettings.aoExponentDirect < AODirect)
            {
                Did("Ambient occlusion on direct light " + LightmapEditorSettings.aoExponentDirect + " -> " + AODirect);
                LightmapEditorSettings.aoExponentDirect = AODirect;
            }
            else Say("Ambient occlusion on direct light is " + LightmapEditorSettings.aoExponentDirect + ", left alone");

            int marked = 0;
            var noSecondUV = new List<string>();
            foreach (var renderer in Object.FindObjectsOfType<MeshRenderer>())
            {
                if (!StaysPut(renderer)) continue;

                var go = renderer.gameObject;
                var flags = GameObjectUtility.GetStaticEditorFlags(go);
                if ((flags & StaticEditorFlags.LightmapStatic) != 0) continue;

                GameObjectUtility.SetStaticEditorFlags(go, flags | StaticEditorFlags.LightmapStatic);
                marked++;

                var filter = renderer.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null && filter.sharedMesh.uv2.Length == 0 && noSecondUV.Count < 15)
                    noSecondUV.Add(go.name);
            }

            Did(marked + " room props marked Lightmap Static");
            if (noSecondUV.Count > 0)
                Say("These have no lightmap UVs, so they will bake blotchy: tick Generate Lightmap UVs on their model's import " +
                    "settings: " + string.Join(", ", noSecondUV.ToArray()));

            Dirty();
        }

        /// Part of the room, not something that is thrown, shatters, rides a rail, is held, or is only there to glow
        private static bool StaysPut(Renderer renderer)
        {
            var t = renderer.transform;
            if (t.root.name == PreviewName || t.root.name == LightingName) return false;
            if (renderer.GetComponentInParent<TrackMover>() != null || renderer.GetComponentInParent<Pistol>() != null) return false;
            if (renderer.GetComponentInParent<ReleaseOnShot>() != null || renderer.GetComponentInParent<ShatterOnShot>() != null) return false;
            if (renderer.GetComponentInParent<Bat>() != null || renderer.GetComponentInParent<Spider>() != null) return false;
            if (renderer.GetComponentInParent<BatBurst>() != null || renderer.GetComponentInParent<VRAvatar>() != null) return false;
            if (renderer.GetComponentInParent<RailBulbs>() != null || renderer.GetComponentInParent<Skylight>() != null) return false;

            var body = renderer.GetComponentInParent<Rigidbody>();
            return body == null || body.isKinematic;
        }

        private static void SetColour(string what, Color now, Color wanted, System.Action<Color> set)
        {
            if (now.r <= wanted.r + 0.005f && now.g <= wanted.g + 0.005f && now.b <= wanted.b + 0.005f)
            {
                Say(what + " is already " + now + ", left alone");
                return;
            }

            set(wanted);
            Did(what + " " + now + " -> " + wanted);
        }

        // ---------------------------------------------------------------- 39

        private enum BakeStage { None, Dark, Part, Lit, Test }

        private static BakeStage _bakeStage;
        private struct KeptBake
        {
            public Object[] Colour;
            public Object[] Direction;
            public LightProbes Probes;
        }

        private static readonly List<Light> _otherLights = new List<Light>();
        private static readonly List<Light[]> _stringLights = new List<Light[]>();
        private static readonly List<KeptBake> _partBakes = new List<KeptBake>();
        private static int _bakingStrings;
        private static Color _litSky;
        private static Color _litEquator;
        private static Color _litGround;
        private static readonly Dictionary<Renderer, Vector4> _darkLayout = new Dictionary<Renderer, Vector4>();
        private static KeptBake _darkBake;
        private static double _bakeStartedAt;
        private static double _logCheckedAt;
        private static long _logReadTo;
        private static bool _bakeSeenRunning;
        private static double _bakeStoppedAt = -1;

        [MenuItem("Shooting Range/Setup/39 - Bake Lit And Dark", false, 62)]
        public static void BakeLitAndDarkMenu()
        {
            if (Lightmapping.isRunning) { Debug.LogWarning("[RangeSetup] A bake is already running"); return; }

            var strings = RangeSetupUtil.FindInScene(StringLightsName);
            if (strings == null) { Debug.LogWarning("[RangeSetup] No string lights to bake. Run step 37 first"); return; }

            if (!EditorUtility.DisplayDialog("Bake lit and dark",
                    "This bakes the lighting " + (StringCountIn(strings.transform) + 1) + " TIMES, replacing the current bakes: first with " +
                    "only the red underglow (every other light switched off, ambient turned down), then adding one string at a " +
                    "time, then with everything on. The copies go in " + DarkBakeDir + " and " + PartBakeDir + "1 and up. Carnival " +
                    "Lights swaps between them in play. Each takes as long as one bake, about " + (StringCountIn(strings.transform) + 1) * 3 + " minutes in all; the scene saves " +
                    "itself at the end. Don't edit the scene while it runs, or they won't line up.", "Bake them all", "Cancel"))
                return;

            ReadyTheBake(strings.transform);
            SetStringsOn(0);
            StartBake(BakeStage.Dark);
        }

        /// Picks up after the last bake that finished, from the copies on disk, once a bake has failed or been stopped
        [MenuItem("Shooting Range/Setup/39b - Resume The Bake", false, 62)]
        public static void ResumeTheBakeMenu()
        {
            if (Lightmapping.isRunning) { Debug.LogWarning("[RangeSetup] A bake is already running"); return; }

            var strings = RangeSetupUtil.FindInScene(StringLightsName);
            if (strings == null) { Debug.LogWarning("[RangeSetup] No string lights to bake. Run step 37 first"); return; }

            int count = StringCountIn(strings.transform);
            int maps = LightmapSettings.lightmaps.Length;
            if (maps == 0 || LoadBaked(DarkBakeDir, "Lightmap-0_light") == null)
            {
                Debug.LogWarning("[RangeSetup] No finished bakes to resume from. Run step 39");
                return;
            }

            int done = 0;
            while (done + 1 < count && LoadBaked(PartBakeDir + (done + 1), "Lightmap-0_light") != null) done++;

            if (!EditorUtility.DisplayDialog("Resume the bake",
                    "Found the dark bake and " + done + " of the " + (count - 1) + " part bakes, so this bakes the remaining " +
                    (count - done) + ", about " + (count - done) * 3 + " minutes. Only use this if no light, static flag or model " +
                    "in the room has changed since those were baked. If any has, run step 39 instead.", "Resume", "Cancel"))
                return;

            ReadyTheBake(strings.transform);
            _darkBake = LoadTheBake(DarkBakeDir, maps);
            for (int i = 1; i <= done; i++) _partBakes.Add(LoadTheBake(PartBakeDir + i, maps));
            RecordTheLayout();

            SetStringsOn(done + 1);
            StartBake(_bakingStrings < _stringLights.Count ? BakeStage.Part : BakeStage.Lit);
        }

        /// One bake with everything on, the heaviest of the set. If this gets through, the rest should, so it's worth its
        /// few minutes before a long one. It replaces the lit lightmap only, so step 39 is still needed after it
        [MenuItem("Shooting Range/Setup/39d - Test Bake (All Lights)", false, 62)]
        public static void TestBakeMenu()
        {
            if (Lightmapping.isRunning) { Debug.LogWarning("[RangeSetup] A bake is already running"); return; }

            var strings = RangeSetupUtil.FindInScene(StringLightsName);
            if (strings == null) { Debug.LogWarning("[RangeSetup] No string lights to bake. Run step 37 first"); return; }

            ReadyTheBake(strings.transform);
            SetStringsOn(_stringLights.Count);
            StartBake(BakeStage.Test);
        }

        /// From the menu, because Unity's own cancel lives on a window that can end up hidden behind the editor
        [MenuItem("Shooting Range/Setup/39c - Stop The Bake", false, 62)]
        public static void StopTheBakeMenu()
        {
            if (Lightmapping.isRunning) Lightmapping.Cancel();

            if (_bakeStage != BakeStage.None) StopBaking("you stopped it. Finished ones are kept, so 39b picks up from there");
            else Debug.Log("[RangeSetup] Sent the lightmapper a cancel; no setup bake was running");
        }

        /// Every light but the red goes off for the dark bake, so nothing the setup steps don't know about can leak in
        private static void ReadyTheBake(Transform strings)
        {
            var underglow = RangeSetupUtil.FindInScene("Underglow");
            _otherLights.Clear();
            foreach (var light in Object.FindObjectsOfType<Light>())
            {
                if (!light.enabled || light.transform.IsChildOf(strings)) continue;
                if (underglow == null || !light.transform.IsChildOf(underglow.transform)) _otherLights.Add(light);
            }

            _stringLights.Clear();
            foreach (Transform holder in strings)
            {
                var lights = holder.GetComponentsInChildren<Light>();
                if (lights.Length > 0) _stringLights.Add(lights);
            }

            _partBakes.Clear();
            // From the constants, not the scene: a bake stopped part way leaves the scene's ambient turned down, and
            // reading that back baked the next lit set at a seventh of its light (audit, 2026-11-02)
            _litSky = DarkSky;
            _litEquator = DarkEquator;
            _litGround = DarkGround;
        }

        private static KeptBake LoadTheBake(string folder, int maps)
        {
            var kept = new KeptBake { Colour = new Object[maps], Direction = new Object[maps] };
            for (int i = 0; i < maps; i++)
            {
                kept.Colour[i] = LoadBaked(folder, "Lightmap-" + i + "_light");
                kept.Direction[i] = LoadBaked(folder, "Lightmap-" + i + "_dir");
            }

            kept.Probes = AssetDatabase.LoadAssetAtPath<LightProbes>(folder + "/LightProbes.asset");
            return kept;
        }

        private static Texture2D LoadBaked(string folder, string name)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return null;

            var found = AssetDatabase.FindAssets(name + " t:Texture2D", new[] { folder });
            return found.Length > 0 ? AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(found[0])) : null;
        }

        private static void StartBake(BakeStage stage)
        {
            _bakeStage = stage;
            _bakeSeenRunning = false;
            _bakeStoppedAt = -1;
            _bakeStartedAt = EditorApplication.timeSinceStartup;
            _logReadTo = LogLength();
            _logCheckedAt = _bakeStartedAt;
            Lightmapping.completed -= OnBakeDone;
            Lightmapping.completed += OnBakeDone;
            EditorApplication.update -= WatchTheBake;
            EditorApplication.update += WatchTheBake;

            if (Lightmapping.BakeAsync())
            {
                Debug.Log("[RangeSetup] Baking the lighting with " + _bakingStrings + " of " + _stringLights.Count +
                          " strings on. Progress shows bottom right");
                return;
            }

            StopBaking("the " + stage + " bake did not start. Check the console for lightmapper errors");
        }

        /// A bake cancelled from the progress bar never calls completed, which would leave the string lights off
        private static void WatchTheBake()
        {
            if (_bakeStage == BakeStage.None) { EditorApplication.update -= WatchTheBake; return; }

            // It retries a failed lightmap for ever, the ETA climbing all the while, so it has to be told to stop
            double now = EditorApplication.timeSinceStartup;
            bool failing = false;
            if (now - _logCheckedAt > LogCheckSeconds)
            {
                _logCheckedAt = now;
                failing = LogShowsBakeFailing();
            }

            if (failing || now - _bakeStartedAt > BakeLimitSeconds)
            {
                Lightmapping.Cancel();
                StopBaking(failing ? "the lightmapper is failing with \"" + BakeFailureText + "\""
                                   : "one bake passed " + BakeLimitSeconds / 60 + " minutes");
                return;
            }

            if (Lightmapping.isRunning) { _bakeSeenRunning = true; _bakeStoppedAt = -1; return; }
            if (!_bakeSeenRunning) return;

            // Give completed a moment to arrive before calling it a cancel
            if (_bakeStoppedAt < 0) _bakeStoppedAt = EditorApplication.timeSinceStartup;
            else if (EditorApplication.timeSinceStartup - _bakeStoppedAt > 3.0)
                StopBaking("the " + _bakeStage + " bake stopped before it finished");
        }

        private static long LogLength()
        {
            var info = new System.IO.FileInfo(Application.consoleLogPath);
            return info.Exists ? info.Length : 0;
        }

        /// Reads the log itself: this error goes to the Editor log, and the log callback didn't report it (2026-11-02)
        private static bool LogShowsBakeFailing()
        {
            try
            {
                using (var log = new System.IO.FileStream(Application.consoleLogPath, System.IO.FileMode.Open,
                                                          System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite))
                {
                    if (log.Length <= _logReadTo) return false;

                    log.Seek(_logReadTo, System.IO.SeekOrigin.Begin);
                    var bytes = new byte[(int)System.Math.Min(log.Length - _logReadTo, LogReadBytes)];
                    int read = log.Read(bytes, 0, bytes.Length);
                    _logReadTo += read;
                    return System.Text.Encoding.ASCII.GetString(bytes, 0, read).Contains(BakeFailureText);
                }
            }
            catch (System.IO.IOException) { return false; }
        }

        private static void OnBakeDone()
        {
            _bakeSeenRunning = false;
            try
            {
                if (_bakeStage == BakeStage.Test)
                {
                    StopBaking(null);
                    Debug.Log("[RangeSetup] Test bake got through with every light on. Run step 39 for the full set");
                    return;
                }

                if (_bakeStage == BakeStage.Dark) _darkBake = KeepTheBake(DarkBakeDir, true);
                else if (_bakeStage == BakeStage.Part) _partBakes.Add(KeepTheBake(PartBakeDir + _bakingStrings, false));
                else
                {
                    FinishTheBakes();
                    StopBaking(null);
                    return;
                }

                SetStringsOn(_bakingStrings + 1);
                StartBake(_bakingStrings < _stringLights.Count ? BakeStage.Part : BakeStage.Lit);
            }
            catch (System.Exception problem)
            {
                StopBaking(problem.Message);
                Debug.LogException(problem);
            }
        }

        private static KeptBake KeepTheBake(string folder, bool setsTheLayout)
        {
            var maps = LightmapSettings.lightmaps;
            if (!setsTheLayout) CheckTheLayout(maps);

            System.IO.Directory.CreateDirectory(folder);
            var kept = new KeptBake { Colour = new Object[maps.Length], Direction = new Object[maps.Length] };

            for (int i = 0; i < maps.Length; i++)
            {
                kept.Colour[i] = CopyBaked(maps[i].lightmapColor, folder, "Lightmap-" + i + "_light");
                kept.Direction[i] = CopyBaked(maps[i].lightmapDir, folder, "Lightmap-" + i + "_dir");
            }

            if (LightmapSettings.lightProbes != null)
            {
                string path = folder + "/LightProbes.asset";
                AssetDatabase.DeleteAsset(path);
                kept.Probes = Object.Instantiate(LightmapSettings.lightProbes);
                AssetDatabase.CreateAsset(kept.Probes, path);
            }

            if (setsTheLayout) RecordTheLayout();

            AssetDatabase.SaveAssets();
            Debug.Log("[RangeSetup] Bake kept: " + maps.Length + " lightmaps" + (kept.Probes != null ? " and the light probes" : "") +
                      " in " + folder);
            return kept;
        }

        private static void RecordTheLayout()
        {
            _darkLayout.Clear();
            foreach (var renderer in Object.FindObjectsOfType<MeshRenderer>())
            {
                if (renderer.lightmapIndex >= 0)
                    _darkLayout[renderer] = new Vector4(renderer.lightmapIndex, renderer.lightmapScaleOffset.x,
                                                        renderer.lightmapScaleOffset.z, renderer.lightmapScaleOffset.w);
            }
        }

        private static Texture2D CopyBaked(Texture2D texture, string folder, string name)
        {
            if (texture == null) return null;

            string from = AssetDatabase.GetAssetPath(texture);
            string to = folder + "/" + name + System.IO.Path.GetExtension(from);
            AssetDatabase.DeleteAsset(to);
            if (!AssetDatabase.CopyAsset(from, to)) throw new System.Exception("could not copy " + from + " to " + to);

            return AssetDatabase.LoadAssetAtPath<Texture2D>(to);
        }

        /// The swap only works if every bake packed every object into the same place on the same lightmap
        private static void CheckTheLayout(LightmapData[] maps)
        {
            int moved = 0;
            foreach (var pair in _darkLayout)
            {
                var r = pair.Key;
                if (r == null) continue;
                var now = new Vector4(r.lightmapIndex, r.lightmapScaleOffset.x, r.lightmapScaleOffset.z, r.lightmapScaleOffset.w);
                if ((now - pair.Value).sqrMagnitude > 1e-8f) moved++;
            }

            if (maps.Length != _darkBake.Colour.Length || moved > 0)
                throw new System.Exception("the bakes don't line up (" + maps.Length + " lightmaps against " +
                                           _darkBake.Colour.Length + " dark, " + moved + " objects packed differently), so " +
                                           "nothing was wired. Bake again without touching the scene in between");
        }

        private static void FinishTheBakes()
        {
            var maps = LightmapSettings.lightmaps;
            CheckTheLayout(maps);

            var lit = new Object[maps.Length];
            var litDirection = new Object[maps.Length];
            for (int i = 0; i < maps.Length; i++)
            {
                lit[i] = maps[i].lightmapColor;
                litDirection[i] = maps[i].lightmapDir;
            }

            var lights = Object.FindObjectOfType<CarnivalLights>();
            if (lights == null) throw new System.Exception("no Carnival Lights in the scene to hand the bakes to. Run step 37");

            new Fields(lights).SetArray("litColour", lit).SetArray("litDirection", litDirection)
                              .SetArray("darkColour", _darkBake.Colour).SetArray("darkDirection", _darkBake.Direction)
                              .Set("litProbes", LightmapSettings.lightProbes).Set("darkProbes", _darkBake.Probes).Apply();
            WireThePartBakes(lights);

            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log("[RangeSetup] All the bakes are done and handed to Carnival Lights; the scene is saved. In play the range " +
                      "opens dark and the strings come on with the welcome");
        }

        private static void WireThePartBakes(CarnivalLights lights)
        {
            var target = new SerializedObject(lights);
            var stages = target.FindProperty("stages");
            stages.arraySize = _partBakes.Count;
            for (int i = 0; i < _partBakes.Count; i++)
            {
                var stage = stages.GetArrayElementAtIndex(i);
                FillTextures(stage.FindPropertyRelative("colour"), _partBakes[i].Colour);
                FillTextures(stage.FindPropertyRelative("direction"), _partBakes[i].Direction);
                stage.FindPropertyRelative("probes").objectReferenceValue = _partBakes[i].Probes;
            }

            target.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void FillTextures(SerializedProperty list, Object[] textures)
        {
            list.arraySize = textures.Length;
            for (int i = 0; i < textures.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = textures[i];
        }

        private static int StringCountIn(Transform strings)
        {
            int count = 0;
            foreach (Transform holder in strings)
            {
                if (holder.GetComponentInChildren<Light>() != null) count++;
            }

            return count;
        }

        /// problem null means it all went through
        private static void StopBaking(string problem)
        {
            Lightmapping.completed -= OnBakeDone;
            EditorApplication.update -= WatchTheBake;
            _bakeStage = BakeStage.None;
            SetStringsOn(_stringLights.Count);

            if (problem != null) Debug.LogWarning("[RangeSetup] Bakes stopped: " + problem + ". All the lights are back on. Finished bakes are kept: restart Unity if it failed, then 39b resumes");
        }

        /// The first count strings on and the rest off, the ambient in step with them, and other lights on only with all of them
        private static void SetStringsOn(int count)
        {
            _bakingStrings = count;
            bool all = count >= _stringLights.Count;

            foreach (var light in _otherLights) if (light != null) light.enabled = all;
            for (int i = 0; i < _stringLights.Count; i++)
            {
                foreach (var light in _stringLights[i]) if (light != null) light.enabled = i < count;
            }

            float ambient = _stringLights.Count > 0 ? Mathf.Lerp(DarkBakeAmbient, 1f, (float)count / _stringLights.Count) : 1f;
            RenderSettings.ambientSkyColor = _litSky * ambient;
            RenderSettings.ambientEquatorColor = _litEquator * ambient;
            RenderSettings.ambientGroundColor = _litGround * ambient;
        }
    }
}
