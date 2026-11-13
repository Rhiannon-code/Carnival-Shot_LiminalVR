using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    public static partial class RangeSetup
    {
        private const string SignUnderglowShader = "CrystalCatch/Sign Underglow";
        private const string SignUnderglowPath = "Assets/Materials/SR_SignUnderglow.mat";
        private const string BellClip = "Assets/Sounds/ShootingRange/Voices/Carny/carny_timeUp_02.wav";
        private const int PuffParticles = 25;
        private const float SignUnderglowStrength = 0.3f;
        private const float GlowStripStrength = 1.6f;
        private const int ShadowBlurTexels = 3;
        private const float ReflectionsLit = 0.35f;
        private static readonly string[] RoomFloodingLights = { "Point Light (1)", "Directional Light" };
        private static readonly string[] TimerParts = { "TimerText", "TimeCaption" };

        [MenuItem("Shooting Range/Setup/40 - Red On The Signs And No Timer", false, 63)]
        public static void RedOnTheSignsMenu()
        {
            if (!EditorUtility.DisplayDialog("Red on the signs and no timer",
                    "This DELETES and lays out again, from the rails where they are now:\n" +
                    "- " + LightingName + "/Underglow (any hand edits to those lights go, including No Shadows on two of them)\n" +
                    "- " + LightingName + "/Probes\n\n" +
                    "It DELETES the HUD's " + string.Join(" and ", TimerParts) + ".\n\n" +
                    "It also changes: the string lights' brightness, the ambient colours, " +
                    "FX_TargetPop's particle count, the barker's Time Up lines (only \"That's the bell!\" stays), and adds " +
                    "Sign Underglow to the target prefab and Light Faults to " + LightingName + ".\n\n" +
                    "The scene is saved afterwards; bake with step 39.", "Do it", "Cancel"))
                return;

            Begin("Red on the signs and no timer");
            RelayTheRedAndTheProbes();
            StringTheCarnivalLights();
            DeepenTheShadows();
            AddTheSignUnderglow();
            AddTheLightFaults();
            TakeOutTheTimer();
            ThinThePuff();
            End();
        }

        [MenuItem("Shooting Range/Setup/44 - Moody Lighting", false, 67)]
        public static void MoodyLightingMenu()
        {
            if (!EditorUtility.DisplayDialog("Moody lighting",
                    "This makes the string lights the room's only real light, as the red only touches the signs and steps:\n\n" +
                    "- SWITCHES OFF " + string.Join(" and ", RoomFloodingLights) + " (unticked, not deleted)\n" +
                    "- DELETES and re-lays " + LightingName + "/Underglow and Probes, with weaker, shorter red lights\n" +
                    "- changes the string lights, ambient and glow strip strength\n" +
                    "- wires the fog and reflections to dim with the lights\n\n" +
                    "The scene is saved afterwards; bake with step 39.", "Do it", "Cancel"))
                return;

            Begin("Moody lighting");
            foreach (var name in RoomFloodingLights)
            {
                var found = RangeSetupUtil.FindInScene(name);
                var light = found != null ? found.GetComponent<Light>() : null;
                if (light == null) { Say(name + " is not in the scene"); continue; }

                light.enabled = false;
                Did("Switched off " + name + ": tick it again on its Light to undo");
            }

            Dirty();
            RelayTheRedAndTheProbes();
            StringTheCarnivalLights();
            DeepenTheShadows();

            var strip = RangeSetupUtil.Load<Material>(GlowStripPath);
            if (strip != null)
            {
                strip.SetFloat("_Strength", GlowStripStrength);
                EditorUtility.SetDirty(strip);
                Did("Glow strips at strength " + GlowStripStrength);
            }

            End();
        }

        [MenuItem("Shooting Range/Setup/47 - Brighter Strings Clearer Signs", false, 70)]
        public static void BrighterStringsMenu()
        {
            if (!EditorUtility.DisplayDialog("Brighter strings, clearer signs",
                    "This raises the string lights and the lit ambient by about a third and weakens the red fade on the signs. The red " +
                    "underglow lights aren't touched. The scene is saved afterwards. Try 39d for the look, then step 39 for the intro.",
                    "Do it", "Cancel"))
                return;

            Begin("Brighter strings, clearer signs");
            var found = RangeSetupUtil.FindInScene(LightingName);
            var strings = found != null ? RangeSetupUtil.Child(found.transform, StringLightsName) : null;
            if (strings == null) { Fail("No " + LightingName + "/" + StringLightsName + ". Run step 37"); End(); return; }

            SetStringBrightness(strings);

            // Set outright: step 38 only ever lowers these
            RenderSettings.ambientSkyColor = DarkSky;
            RenderSettings.ambientEquatorColor = DarkEquator;
            RenderSettings.ambientGroundColor = DarkGround;
            Did("Ambient sky " + DarkSky);
            Dirty();

            var fade = RangeSetupUtil.Load<Material>(SignUnderglowPath);
            if (fade != null)
            {
                fade.SetFloat("_Strength", SignUnderglowStrength);
                EditorUtility.SetDirty(fade);
                Did("Red fade on the signs at strength " + SignUnderglowStrength);
            }

            End();
        }

        [MenuItem("Shooting Range/Setup/63 - Red Up, Strings Down", false, 86)]
        public static void RedUpStringsDownMenu()
        {
            Begin("Red up, strings down");
            var found = RangeSetupUtil.FindInScene(LightingName);
            var underglow = found != null ? RangeSetupUtil.Child(found.transform, "Underglow") : null;
            var strings = found != null ? RangeSetupUtil.Child(found.transform, StringLightsName) : null;
            if (underglow == null || strings == null)
            {
                Fail("No " + LightingName + "/Underglow or " + StringLightsName + ". Run steps 44 and 37");
                End();
                return;
            }

            var reds = underglow.GetComponentsInChildren<Light>(true);
            foreach (var light in reds)
            {
                light.intensity = GlowIntensity;
                light.range = GlowRange;
            }

            Dirty();
            Did(reds.Length + " red lights at intensity " + GlowIntensity + ", range " + GlowRange + " m");

            SetStringBrightness(strings);
            int fills = 0;
            foreach (var light in strings.GetComponentsInChildren<Light>(true))
            {
                if (light.name != FanFillName) continue;

                light.intensity = FanFillIntensity;
                fills++;
            }

            Did(fills + " fill lights at " + FanFillIntensity + ", fan strings at " + FanLightIntensity + ". All of it shows after a bake: step 39");

            var strip = RangeSetupUtil.Load<Material>(GlowStripPath);
            if (strip != null)
            {
                strip.SetFloat("_Strength", GlowStripStrength);
                EditorUtility.SetDirty(strip);
                Did("Glow strips at strength " + GlowStripStrength + ". Shows at once");
            }

            var fade = RangeSetupUtil.Load<Material>(SignUnderglowPath);
            if (fade != null)
            {
                fade.SetFloat("_Strength", SignUnderglowStrength);
                EditorUtility.SetDirty(fade);
                Did("Red fade on the signs with the room lit at " + SignUnderglowStrength + ". Shows at once");
            }

            End();
        }

        [MenuItem("Shooting Range/Setup/50 - Softer Shadows More Surface", false, 73)]
        public static void SofterShadowsMenu()
        {
            Begin("Softer shadows, more surface");
            LightmapEditorSettings.filteringMode = LightmapEditorSettings.FilterMode.Advanced;
            LightmapEditorSettings.denoiserTypeDirect = LightmapEditorSettings.DenoiserType.None;
            LightmapEditorSettings.filterTypeDirect = LightmapEditorSettings.FilterType.Gaussian;
            LightmapEditorSettings.filteringGaussRadiusDirect = ShadowBlurTexels;
            Did("Baked shadow edges blurred over " + ShadowBlurTexels + " lightmap pixels (was 1). Shows after a bake");

            var lights = Object.FindObjectOfType<CarnivalLights>();
            if (lights != null)
            {
                new Fields(lights).Set("reflectionsLit", ReflectionsLit).Apply();
                Did("Reflections with the room lit: " + ReflectionsLit + " (was 0.15), which is what brings normal maps out. Shows in Play at once");
            }

            Dirty();
            End();
        }

        private static void RelayTheRedAndTheProbes()
        {
            var lighting = RangeSetupUtil.FindInScene(LightingName);
            if (lighting == null) { Fail("No " + LightingName + " in the scene. Run step 27"); return; }

            foreach (var part in new[] { "Underglow", "Probes" })
            {
                var old = RangeSetupUtil.Child(lighting.transform, part);
                if (old == null) continue;

                Object.DestroyImmediate(old.gameObject);
                Did("Deleted " + LightingName + "/" + part + " to lay it again");
            }

            LayTheLights();
        }

        private static void AddTheSignUnderglow()
        {
            var material = ShaderMaterial(SignUnderglowPath, SignUnderglowShader, GlowColour);
            if (material == null) return;

            material.SetFloat("_Strength", SignUnderglowStrength);
            EditorUtility.SetDirty(material);

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                new Fields(RangeSetupUtil.Ensure<SignUnderglow>(root)).Set("material", material).Apply();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Did("Sign Underglow on " + PrefabPath + ": tune the colour and strength on " + SignUnderglowPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void AddTheLightFaults()
        {
            var lights = Object.FindObjectOfType<CarnivalLights>();
            if (lights == null) { Fail("No Carnival Lights in the scene. Run step 37"); return; }

            new Fields(RangeSetupUtil.Ensure<LightFaults>(lights.gameObject))
                .Set("game", Object.FindObjectOfType<RangeGame>())
                .Set("director", Object.FindObjectOfType<TrackDirector>())
                .Set("lights", lights).Apply();
            Dirty();
            Did("Light Faults on " + lights.name + ": small flickers from round 1, blackouts in the middle rounds");
        }

        private static void TakeOutTheTimer()
        {
            foreach (var part in TimerParts)
            {
                var found = RangeSetupUtil.FindInScene(part);
                if (found == null) { Say(part + " is already gone"); continue; }

                Object.DestroyImmediate(found);
                Did("Deleted the HUD's " + part);
            }

            Dirty();

            // "Time's up" means nothing with no clock
            var voice = Object.FindObjectOfType<CarnyVoice>();
            var bell = RangeSetupUtil.Load<AudioClip>(BellClip);
            if (voice == null || bell == null) { Fail("No CarnyVoice in the scene, or no " + BellClip); return; }

            new Fields(voice).SetArray("timeUp", new List<Object> { bell }).Apply();
            Did("The barker's Time Up slot is now only \"That's the bell!\"");
        }

        private static void ThinThePuff()
        {
            const string path = "Assets/Prefabs/ShootingRange/FX_TargetPop.prefab";
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var effect in contents.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var emission = effect.emission;
                    if (emission.burstCount == 0) continue;

                    var burst = emission.GetBurst(0);
                    burst.count = PuffParticles;
                    emission.SetBurst(0, burst);
                    Did("FX_TargetPop / " + effect.name + ": burst of " + PuffParticles + " particles");
                }

                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }
    }
}
