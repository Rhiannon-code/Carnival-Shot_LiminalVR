using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    /// Steps 41 and 42: holes on the room, see-through holes in signs, wood splinters, the pause before a sign reacts,
    /// and the barker's warnings (ADR 0064)
    public static partial class RangeSetup
    {
        private const string DebrisName = "Debris";
        private const int RapidFireDust = 16;
        private const int RapidFireHoles = 60;
        private const int RapidFireDebris = 200;
        private const string FlamesName = "Flames";
        private const string FlameEffect = "Assets/Modular Castle/Assets/particles/fire1.prefab";
        private const string OldSplintersName = "Splinters";
        private const string SplinterShader = "CrystalCatch/Splinter";
        private const string SplinterMaterial = "Assets/Materials/SR_Splinter.mat";
        private const string HoleMask = "Assets/Textures/ShotDecals/sign_hole_mask.png";
        private const string SignShader = "CrystalCatch/Sign";

        [MenuItem("Shooting Range/Setup/41 - Holes And Splinters", false, 64)]
        public static void HolesAndSplintersMenu()
        {
            if (!EditorUtility.DisplayDialog("Holes and splinters",
                    "This moves the six sign materials from Standard onto " + SignShader + ", which keeps their values and also " +
                    "cuts bullet holes. The shatter shards share those materials. It also sets the room's hole size and " +
                    "adds what flies off a hit if it isn't there. " +
                    "The originals are in ~/backups/2026-11-02-lights-and-underglow/second-pass.", "Do it", "Cancel"))
                return;

            Begin("Holes and splinters");
            AddTheShotDecals();
            SetTheHoleSize();
            CutTheSignHoles();
            AddTheDebris();
            End();
        }

        [MenuItem("Shooting Range/Setup/42 - Wire The Barker's Warnings", false, 65)]
        public static void WireTheWarningsMenu()
        {
            Begin("The barker's warnings");
            var voice = Object.FindObjectOfType<CarnyVoice>();
            if (voice == null) { Fail("No CarnyVoice in the scene"); End(); return; }

            var fields = new Fields(voice);
            fields.SetArray("spiderWarning", WarningLines("spiderWarning"));
            fields.SetArray("batWarning", WarningLines("batWarning"));
            fields.Apply();
            Dirty();
            Did("Barker: the spider and bat warnings are wired");
            End();
        }

        private static System.Collections.Generic.List<Object> WarningLines(string slot)
        {
            var lines = new System.Collections.Generic.List<Object>();
            for (int i = 1; i <= 3; i++)
            {
                var clip = RangeSetupUtil.Load<AudioClip>("Assets/Sounds/ShootingRange/Voices/Carny/carny_" + slot + "_0" + i + ".wav");
                if (clip != null) lines.Add(clip);
                else Fail("No carny_" + slot + "_0" + i);
            }

            return lines;
        }

        [MenuItem("Shooting Range/Setup/43 - Room Objects Sound And Debris", false, 66)]
        public static void RoomObjectsMenu()
        {
            if (!EditorUtility.DisplayDialog("Room objects: sound and debris",
                    "This gives torches, candleholders, skulls, the vase, flasks and the plushie their own surface, REPLACING " +
                    "any physic material already on their colliders, and sorts every other collider with none. It DELETES the " +
                    "old Splinters system under Impact FX and makes one system per kind of debris. It fills empty sound slots " +
                    "on the wobblers, painting and skylight boards. The scene is saved afterwards. No rebake needed.", "Do it", "Cancel"))
                return;

            Begin("Room objects: sound and debris");
            SortTheRoomSurfaces();
            AddTheDebris();
            FillTheRoomObjectSounds();
            End();
        }

        [MenuItem("Shooting Range/Setup/48 - Vampires Burn In The Sun", false, 71)]
        public static void VampiresBurnMenu()
        {
            Begin("Vampires burn in the sun");
            var impact = Object.FindObjectOfType<ImpactFX>();
            var effect = RangeSetupUtil.Load<GameObject>(FlameEffect);
            if (impact == null || effect == null) { Fail("No Impact FX in the scene, or no " + FlameEffect); End(); return; }

            var old = RangeSetupUtil.Child(impact.transform, FlamesName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            new Fields(RangeSetupUtil.Ensure<Flames>(impact.gameObject)).Set("system", MakeTheFlames(impact.transform, effect)).Apply();
            Did(FlamesName + " on " + impact.name + ", from the torches' " + FlameEffect);

            Dirty();
            WirePrefab(PrefabPath, root => Fill(root.GetComponent<PopUpTarget>(), "igniteClips", "vampire_ignite"));
            End();
        }

        /// A copy of the torches' fire, so a burning sign matches them. It keeps its own material, animation and colour,
        /// and only changes to emit when told, stay where it was lit as the sign falls, and be sized for a sign. Its light goes
        private static ParticleSystem MakeTheFlames(Transform parent, GameObject effect)
        {
            var go = Object.Instantiate(effect, parent);
            go.name = FlamesName;

            // The effect brings its own realtime light, 15 m of orange that floods the room. No realtime lights on Quest
            foreach (var light in go.GetComponentsInChildren<Light>(true))
            {
                Object.DestroyImmediate(light.gameObject);
                Did("Took the realtime light out of the copy of " + effect.name);
            }

            var system = go.GetComponentInChildren<ParticleSystem>(true);
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 100;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.55f);

            var emission = system.emission;
            emission.rateOverTime = 0f;

            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            return system;
        }

        [MenuItem("Shooting Range/Setup/52 - Count From Three", false, 75)]
        public static void CountFromThreeMenu()
        {
            Begin("Count from three");
            var game = Object.FindObjectOfType<RangeGame>();
            if (game == null) { Fail("No RangeGame in the scene"); End(); return; }

            new Fields(game).Set("countdownFrom", 3).Set("betweenRoundsCountdownFrom", 3).Apply();
            Dirty();
            Did("The barker counts 3, 2, 1, GO before every round");
            End();
        }

        private const string FogWispShader = "CrystalCatch/Fog Wisp";
        private const string FogWispMaterial = "Assets/Materials/SR_FogWisp.mat";

        [MenuItem("Shooting Range/Setup/51 - Better Ground Fog", false, 74)]
        public static void BetterFogMenu()
        {
            if (!EditorUtility.DisplayDialog("Better ground fog",
                    "This DELETES everything under " + FogName + " and lays it again: 4 thin sheets at rising heights, drifting wisps " +
                    "over the floor and along each tier of steps, and rounds clearing rings through it. No bake needed. It costs more " +
                    "see-through drawing than the 2 sheets did, so check the frame rate on a Quest 2.", "Do it", "Cancel"))
                return;

            Begin("Better ground fog");
            LayTheGroundFog();

            var root = RangeSetupUtil.FindInScene(FogName);
            var shader = Shader.Find("CrystalCatch/Ground Fog");
            var noise = RangeSetupUtil.Load<Texture2D>(FogNoise);
            if (root == null || shader == null || noise == null) { Fail("The ground fog didn't lay. See above"); End(); return; }

            float start = RangeSetupUtil.Child(root.transform, "Low").position.z * 2f - FogEndZ;
            float floor = RangeSetupUtil.Child(root.transform, "Low").position.y - 0.05f;

            var mid = FogMaterial("Assets/Materials/SR_GroundFogMid.mat", shader, noise, 0.22f, 0.26f, 0.5f,
                                  new Vector4(0.04f, -0.03f), new Vector4(-0.05f, -0.02f));
            var top = FogMaterial("Assets/Materials/SR_GroundFogTop.mat", shader, noise, 0.14f, 0.17f, 0.37f,
                                  new Vector4(-0.03f, -0.035f), new Vector4(0.045f, 0.015f));
            FogLayer(root.transform, "Mid", mid, floor + 0.26f, start);
            FogLayer(root.transform, "Top", top, floor + 0.38f, start);

            // Layered, so each is fainter than the one sheet it replaces
            SetFogDensity("Assets/Materials/SR_GroundFogLow.mat", 0.38f);
            SetFogDensity("Assets/Materials/SR_GroundFogHigh.mat", 0.3f);
            Did("Fog: 4 sheets, up to 38 cm");

            var wispShader = Shader.Find(FogWispShader);
            if (wispShader == null) { Fail(FogWispShader + " has not compiled. Check the console"); End(); return; }
            var wisp = RangeSetupUtil.Load<Material>(FogWispMaterial);
            if (wisp == null) { wisp = new Material(wispShader); AssetDatabase.CreateAsset(wisp, FogWispMaterial); }

            MakeFogWisps(root.transform, "Wisps Floor", wisp, new Vector3(0f, floor + 0.3f, (start + FogEndZ) * 0.5f),
                         new Vector3(FogWidth, 0.2f, FogEndZ - start), 14, 1.6f, 1.2f, 2.2f, 0.16f);

            // Flat sheets can't work up here: the steps are at or above eye level, so one would be edge-on or out of sight
            var rails = new List<TrackRail>();
            foreach (var rail in Object.FindObjectsOfType<TrackRail>())
                if (rail.Group == TrackGroup.FloorAcross || rail.Group == TrackGroup.FloorAlong) rails.Add(rail);

            int tier = 0;
            var eye = EyePoint();
            foreach (var rail in FrontRailPerLevel(rails, eye))
            {
                var middle = rail.PointAt(rail.Length * 0.5f) + Vector3.up * 0.35f - AwayFromEye(rail, eye) * 0.2f;
                MakeFogWisps(root.transform, "Wisps Steps " + tier++, wisp, middle, new Vector3(rail.Length, 0.1f, 0.3f), 5, 0.5f, 0.5f, 0.9f, 0.1f);
            }

            Did("Fog: drifting wisps over the floor and along " + tier + " tiers. Faint on the steps, so they don't hide signs");

            RangeSetupUtil.Ensure<FogStir>(Object.FindObjectOfType<ImpactFX>().gameObject);
            Did("Fog Stir on Impact FX: low rounds clear a ring through the fog");

            var lighting = RangeSetupUtil.FindInScene(LightingName);
            if (lighting != null) WireTheCarnivalLights(lighting.transform);
            Dirty();
            End();
        }

        private static void SetFogDensity(string path, float density)
        {
            var material = RangeSetupUtil.Load<Material>(path);
            if (material == null) return;

            material.SetFloat("_Density", density);
            EditorUtility.SetDirty(material);
        }

        private static void MakeFogWisps(Transform root, string name, Material material, Vector3 at, Vector3 box, int most,
                                         float perSecond, float smallest, float largest, float alpha)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = at;

            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.loop = true;
            main.playOnAwake = true;
            main.prewarm = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = most;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(smallest, largest);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(1f, 1f, 1f, alpha);

            var emission = system.emission;
            emission.rateOverTime = perSecond;

            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;
            shape.randomDirectionAmount = 1f;

            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            var colour = system.colorOverLifetime;
            colour.enabled = true;
            colour.color = fade;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            system.Play();
        }

        /// Scattershot is 5 rounds a shot from each gun, and full auto never stops: at those rates the pools built for single
        /// shots ran dry, so dust and shards were skipped and holes were replaced almost as they landed (2026-11-02)
        [MenuItem("Shooting Range/Setup/49 - Room For Rapid Fire", false, 72)]
        public static void RoomForRapidFireMenu()
        {
            Begin("Room for rapid fire");
            var impact = Object.FindObjectOfType<ImpactFX>();
            if (impact == null) { Fail("No Impact FX in the scene"); End(); return; }

            new Fields(impact).Set("burstCopies", RapidFireDust).Apply();
            Did("Impact FX: " + RapidFireDust + " dust puffs at once");

            var decals = impact.GetComponent<ShotDecals>();
            if (decals != null) { new Fields(decals).Set("maxMarks", RapidFireHoles).Apply(); Did("Shot Decals: " + RapidFireHoles + " holes at once"); }

            foreach (var system in impact.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (system.transform.parent == null || system.transform.parent.name != DebrisName) continue;

                var main = system.main;
                main.maxParticles = RapidFireDebris;
            }

            Did("Debris: room for " + RapidFireDebris + " pieces of each kind");
            Dirty();
            End();
        }

        private static void FillTheRoomObjectSounds()
        {
            int filled = 0;
            foreach (var wobbler in Object.FindObjectsOfType<WobbleOnShot>()) filled += Fill(wobbler, "fallClips", "prop_thud");
            foreach (var painting in Object.FindObjectsOfType<HangingOnShot>()) filled += Fill(painting, "hitClips", "sign_creak");
            foreach (var released in Object.FindObjectsOfType<ReleaseOnShot>()) filled += Fill(released, "hitClips", "env_wood_0");

            if (filled > 0) { Dirty(); Did(filled + " empty sound slots filled on room objects"); }
        }

        private static void SetTheHoleSize()
        {
            var decals = Object.FindObjectOfType<ShotDecals>();
            if (decals == null) { Fail("No Shot Decals in the scene"); return; }

            new Fields(decals).Set("minSize", 0.08f).Set("maxSize", 0.12f).Apply();
            Dirty();
            Did("Shot Decals: holes 8-12 cm");
        }

        private static void CutTheSignHoles()
        {
            var importer = AssetImporter.GetAtPath(HoleMask) as TextureImporter;
            if (importer == null) { Fail("No " + HoleMask + ". Run tools/shot-decal-art.py, click into Unity, run this again"); return; }

            importer.sRGBTexture = false;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();

            var mask = RangeSetupUtil.Load<Texture2D>(HoleMask);
            var shader = Shader.Find(SignShader);
            if (shader == null) { Fail("No shader " + SignShader + ". Check the console for shader errors"); return; }

            var underglow = RangeSetupUtil.Load<Material>(SignUnderglowPath);
            if (underglow != null)
            {
                underglow.SetTexture("_HoleMask", mask);
                EditorUtility.SetDirty(underglow);
            }

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var figure = RangeSetupUtil.Child(root.transform, "Figure");
                if (figure == null) { Fail("No Figure on " + PrefabPath); return; }

                int moved = 0;
                foreach (var renderer in figure.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var material = renderer.sharedMaterial;
                    if (material == null) continue;

                    if (material.shader != shader) { material.shader = shader; moved++; }
                    material.SetTexture("_HoleMask", mask);
                    EditorUtility.SetDirty(material);
                }

                new Fields(RangeSetupUtil.Ensure<SignHole>(root)).Set("minSize", 0.18f).Set("maxSize", 0.24f).Apply();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Did(moved + " sign materials moved onto " + SignShader);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private struct DebrisLook
        {
            public DebrisKind Kind;
            public int Fewest, Most;
            public float LifeMin, LifeMax, SpeedMin, SpeedMax, Gravity, ConeAngle;
            public Color ColourA, ColourB;
            public float WidthMin, WidthMax, LengthMin, LengthMax, Tumble;
            public bool Streak;
        }

        // Small on purpose: these must never block a sign the way the first smoke did. Retune in the Inspector
        private static readonly DebrisLook[] DebrisLooks =
        {
            new DebrisLook { Kind = DebrisKind.Splinters, Fewest = 4, Most = 6, LifeMin = 0.5f, LifeMax = 0.85f, SpeedMin = 2f, SpeedMax = 4.5f,
                Gravity = 1.2f, ConeAngle = 40f, ColourA = new Color(0.8f, 0.63f, 0.4f), ColourB = new Color(0.5f, 0.34f, 0.2f),
                WidthMin = 0.008f, WidthMax = 0.018f, LengthMin = 0.05f, LengthMax = 0.12f, Tumble = 12f },
            new DebrisLook { Kind = DebrisKind.Sparks, Fewest = 6, Most = 10, LifeMin = 0.12f, LifeMax = 0.3f, SpeedMin = 3f, SpeedMax = 7f,
                Gravity = 0.6f, ConeAngle = 50f, ColourA = new Color(1f, 0.85f, 0.45f), ColourB = new Color(1f, 0.55f, 0.15f),
                WidthMin = 0.006f, WidthMax = 0.012f, LengthMin = 0.006f, LengthMax = 0.012f, Streak = true },
            new DebrisLook { Kind = DebrisKind.Chips, Fewest = 4, Most = 7, LifeMin = 0.4f, LifeMax = 0.7f, SpeedMin = 1.5f, SpeedMax = 3.5f,
                Gravity = 1.5f, ConeAngle = 45f, ColourA = new Color(0.55f, 0.5f, 0.45f), ColourB = new Color(0.33f, 0.3f, 0.28f),
                WidthMin = 0.01f, WidthMax = 0.03f, LengthMin = 0.01f, LengthMax = 0.03f, Tumble = 10f },
            new DebrisLook { Kind = DebrisKind.Embers, Fewest = 4, Most = 6, LifeMin = 0.8f, LifeMax = 1.4f, SpeedMin = 0.3f, SpeedMax = 0.9f,
                Gravity = -0.05f, ConeAngle = 60f, ColourA = new Color(1f, 0.6f, 0.15f), ColourB = new Color(0.9f, 0.25f, 0.05f),
                WidthMin = 0.006f, WidthMax = 0.012f, LengthMin = 0.006f, LengthMax = 0.012f, Tumble = 3f },
            new DebrisLook { Kind = DebrisKind.Stuffing, Fewest = 6, Most = 9, LifeMin = 0.9f, LifeMax = 1.4f, SpeedMin = 0.5f, SpeedMax = 1.4f,
                Gravity = 0.15f, ConeAngle = 55f, ColourA = new Color(0.9f, 0.88f, 0.82f), ColourB = new Color(0.7f, 0.68f, 0.62f),
                WidthMin = 0.015f, WidthMax = 0.03f, LengthMin = 0.015f, LengthMax = 0.03f, Tumble = 2f },
        };

        /// Made once and then left alone, like the ground fog: retune it in the Inspector, or delete it and re-run
        private static void AddTheDebris()
        {
            var impact = Object.FindObjectOfType<ImpactFX>();
            if (impact == null) { Fail("No Impact FX in the scene"); return; }

            var old = RangeSetupUtil.Child(impact.transform, OldSplintersName);
            if (old != null) { Object.DestroyImmediate(old.gameObject); Did("Deleted the old " + OldSplintersName + " system"); }

            if (RangeSetupUtil.Child(impact.transform, DebrisName) != null) { Say(DebrisName + " is already there, left alone"); return; }

            var material = ShaderMaterial(SplinterMaterial, SplinterShader, Color.white);
            if (material == null) return;

            var holder = new GameObject(DebrisName).transform;
            holder.SetParent(impact.transform, false);

            var kinds = new SerializedObject(RangeSetupUtil.Ensure<Debris>(impact.gameObject));
            var list = kinds.FindProperty("kinds");
            list.arraySize = DebrisLooks.Length;
            for (int i = 0; i < DebrisLooks.Length; i++)
            {
                var look = DebrisLooks[i];
                var entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("kind").intValue = (int)look.Kind;
                entry.FindPropertyRelative("system").objectReferenceValue = MakeDebrisSystem(holder, material, look);
                entry.FindPropertyRelative("fewest").intValue = look.Fewest;
                entry.FindPropertyRelative("most").intValue = look.Most;
            }
            kinds.ApplyModifiedPropertiesWithoutUndo();

            Dirty();
            Did(DebrisName + " on " + impact.name + ": splinters, sparks, chips, embers and stuffing");
        }

        private static ParticleSystem MakeDebrisSystem(Transform parent, Material material, DebrisLook look)
        {
            var go = new GameObject(look.Kind.ToString());
            go.transform.SetParent(parent, false);

            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            // Always playing with nothing to emit, so Debris has a running system to emit into
            var main = system.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;
            main.startLifetime = new ParticleSystem.MinMaxCurve(look.LifeMin, look.LifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(look.SpeedMin, look.SpeedMax);
            main.gravityModifier = look.Gravity;
            main.startColor = new ParticleSystem.MinMaxGradient(look.ColourA, look.ColourB);

            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(look.WidthMin, look.WidthMax);
            main.startSizeY = new ParticleSystem.MinMaxCurve(look.WidthMin, look.WidthMax);
            main.startSizeZ = new ParticleSystem.MinMaxCurve(look.LengthMin, look.LengthMax);

            var emission = system.emission;
            emission.rateOverTime = 0f;

            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = look.ConeAngle;
            shape.radius = 0.02f;

            var shrink = system.sizeOverLifetime;
            shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(0.7f, 1f), new Keyframe(1f, 0f)));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            if (look.Streak)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0.03f;
                renderer.lengthScale = 1f;
                return system;
            }

            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            var tumble = system.rotationOverLifetime;
            tumble.enabled = true;
            tumble.separateAxes = true;
            tumble.x = new ParticleSystem.MinMaxCurve(-look.Tumble, look.Tumble);
            tumble.y = new ParticleSystem.MinMaxCurve(-look.Tumble, look.Tumble);
            tumble.z = new ParticleSystem.MinMaxCurve(-look.Tumble, look.Tumble);

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = cube.GetComponent<MeshFilter>().sharedMesh;
            renderer.enableGPUInstancing = false;
            Object.DestroyImmediate(cube);

            return system;
        }
    }
}
