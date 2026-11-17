using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    /// Step 81: the string lights as one connected set (user, 2026-11-13: "physically connected by their strings so they become a
    /// single set"). The set is a model, built by art/string-lights/build_string_lights.py in Local Docs from the tent's own
    /// numbers: every run hangs from a beam or the room-edge loop and shares its hooks with the runs it meets. Each run is its
    /// own part, so CarnivalLights still chases and flickers them run by run. Plan: LiminalVR/CrystalCatch/plans/tent-ceiling-2026-11-13.md
    public static partial class RangeSetup
    {
        private const string StringLightsModel = "Assets/Models/StringLights/StringLights.fbx";
        private const string LightMarkerPrefix = "LIGHT ";
        // The model's materials, and what each becomes in Unity
        private const string ModelGlass = "M_BulbGlass";
        private const string ModelWire = "M_StringWire";
        private const string ModelHalo = "M_BulbHalo";
        private const string HaloShader = "CrystalCatch/Bulb Halo";
        private const string HaloMaterialPath = "Assets/Materials/SR_BulbHalo.mat";
        // Gnats round only some bulbs (user, 2026-11-13: "maybe little gnats would be better" than moths): these runs, the ones
        // round the player's own space and down the range (user, 2026-11-13: "not enough gnats on the range right/left strings")
        private static readonly string[] GnatRuns = { "Booth Top", "Beam Swag 1", "Beam Swag 2", "Range Left", "Range Right" };
        private const string GnatMaterialName = "SR_Gnat";
        // Bright, as if catching the light: dark specks didn't show against the dark ceiling, and gold ones vanished into the
        // bulbs' glow, so cool against the warm bulbs (user, 2026-11-13)
        private const string GnatShader = "Mobile/Particles/Additive";
        private static readonly Color GnatColour = new Color(0.7f, 0.85f, 1f, 0.9f);
        private const float GnatSmallest = 0.03f;
        private const float GnatLargest = 0.05f;
        // About 6 round each bulb at any moment (user, 2026-11-13: "not enough of them", chose 5-8)
        private const float GnatsPerBulb = 6f;
        private const float GnatShortestLife = 0.8f;
        private const float GnatLongestLife = 1.6f;
        // Short lives, a small scatter and gentle noise keep each cloud within about 15 cm of its bulb
        private const float GnatScatter = 0.08f;
        private const float GnatJitter = 0.12f;

        [MenuItem("Shooting Range/Setup/81 - Hang The String Lights", false, 104)]
        public static void HangTheStringLightsMenu()
        {
            if (!EditorUtility.DisplayDialog("Hang the string lights",
                    "This DELETES " + LightingName + "/" + StringLightsName + " (every string light) and puts in the connected set " +
                    "from " + StringLightsModel + ", with a baked light at each of its markers. The scene is saved afterwards; then bake.",
                    "Hang them", "Cancel"))
                return;

            Begin("String lights hung");
            try { HangTheStringLights(); }
            catch (System.Exception e) { Fail("Stopped by an error, so nothing after this point ran: " + e); }
            finally { End(); }
        }

        private static void HangTheStringLights()
        {
            var glass = ShaderMaterial(StringBulbMaterialPath, StringBulbShader, StringColour);
            var wire = WireMaterial();
            var halo = ShaderMaterial(HaloMaterialPath, HaloShader, new Color(1f, 0.7f, 0.35f));
            if (glass == null || wire == null || halo == null || !RemapStringMaterials(glass, wire, halo)) return;

            var model = RangeSetupUtil.Load<GameObject>(StringLightsModel);
            var found = RangeSetupUtil.FindInScene(LightingName);
            var root = found != null ? found.transform : new GameObject(LightingName).transform;
            var old = RangeSetupUtil.Child(root, StringLightsName);
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
                Did("Deleted " + LightingName + "/" + StringLightsName);
            }

            var strings = ((GameObject)PrefabUtility.InstantiatePrefab(model, root)).transform;
            strings.name = StringLightsName;
            strings.localPosition = Vector3.zero;
            strings.localRotation = Quaternion.identity;

            int runs = 0, lights = 0;
            foreach (Transform run in strings)
            {
                var renderer = run.GetComponent<MeshRenderer>();
                if (renderer == null) continue;

                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

                var holder = new GameObject("Lights").transform;
                holder.SetParent(run, false);
                foreach (Transform marker in run)
                {
                    if (!marker.name.StartsWith(LightMarkerPrefix)) continue;
                    BakedLight(holder, "Light", marker.position, StringColour, StringLightIntensity, StringLightRange, StringLightSoftness);
                    lights++;
                }
                runs++;
            }

            Did(runs + " runs, " + lights + " baked lights, from " + StringLightsModel);
            SetStringBrightness(strings);
            HangTheRoomFill(strings);
            AddTheGnats(strings);
            WireTheCarnivalLights(root);
            Dirty();
            Say("Next: bake. Each run is one pass of step 39");
        }

        /// Gnats buzzing in tight clouds round the bulbs of a few runs: warm specks born at the bulbs (the halo quads, which sit
        /// at each bulb's middle), jittered by fast noise, each living about a second. CarnivalLights stops them in the dark
        private static void AddTheGnats(Transform strings)
        {
            var material = EyeMaterial(GnatMaterialName, GnatShader, Color.white);
            if (material == null) return;
            material.shader = Shader.Find(GnatShader);
            material.mainTexture = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
            EditorUtility.SetDirty(material);

            int added = 0;
            foreach (var name in GnatRuns)
            {
                var run = RangeSetupUtil.Child(strings, name);
                var renderer = run != null ? run.GetComponent<MeshRenderer>() : null;
                if (renderer == null) { Say("No " + name + " run to put gnats round"); continue; }
                if (HaloSubmesh(renderer) < 0) { Say(name + " has no halo material to find its bulbs by, so no gnats"); continue; }

                var gnats = new GameObject("Gnats").AddComponent<ParticleSystem>();
                gnats.transform.SetParent(run, false);
                gnats.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                var main = gnats.main;
                main.loop = true;
                main.playOnAwake = true;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = new ParticleSystem.MinMaxCurve(GnatShortestLife, GnatLongestLife);
                main.startSpeed = 0f;
                main.startSize = new ParticleSystem.MinMaxCurve(GnatSmallest, GnatLargest);
                main.startColor = GnatColour;
                int bulbs = BulbsOn(renderer);
                main.maxParticles = Mathf.CeilToInt(bulbs * GnatsPerBulb * 2f);
                main.gravityModifier = 0f;

                var emission = gnats.emission;
                emission.rateOverTime = bulbs * GnatsPerBulb * 2f / (GnatShortestLife + GnatLongestLife);

                var shape = gnats.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.MeshRenderer;
                shape.meshRenderer = renderer;
                shape.meshShapeType = ParticleSystemMeshShapeType.Vertex;
                shape.useMeshMaterialIndex = true;
                shape.meshMaterialIndex = HaloSubmesh(renderer);
                shape.randomPositionAmount = GnatScatter;
                // Off, or each gnat takes the mesh's vertex colour, which is its sway freedom in red alone: they glowed red
                shape.useMeshColors = false;

                var noise = gnats.noise;
                noise.enabled = true;
                noise.strength = GnatJitter;
                noise.frequency = 8f;
                noise.scrollSpeed = 2f;

                var fade = new Gradient();
                fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                             new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(1f, 0.9f), new GradientAlphaKey(0f, 1f) });
                var colour = gnats.colorOverLifetime;
                colour.enabled = true;
                colour.color = fade;

                var particles = gnats.GetComponent<ParticleSystemRenderer>();
                particles.sharedMaterial = material;
                particles.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                particles.receiveShadows = false;
                gnats.Play();
                added++;
            }

            Did("Gnats round " + added + " runs (" + string.Join(", ", GnatRuns) + ")");
        }

        /// One halo quad (two triangles) at each bulb
        private static int BulbsOn(Renderer run)
        {
            var mesh = run.GetComponent<MeshFilter>().sharedMesh;
            return (int)(mesh.GetIndexCount(HaloSubmesh(run)) / 6);
        }

        /// Which of the run's materials is the halo, whose quads sit at the bulbs' middles; -1 if none
        private static int HaloSubmesh(Renderer run)
        {
            var materials = run.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
                if (materials[i] != null && materials[i].shader.name == HaloShader) return i;
            return -1;
        }

        /// The model's materials onto the bulb glass (StringBulbs, which CarnivalLights dims) and the swaying wire
        private static bool RemapStringMaterials(Material glass, Material wire, Material halo)
        {
            var importer = AssetImporter.GetAtPath(StringLightsModel) as ModelImporter;
            if (importer == null) { Fail("No " + StringLightsModel + ". Let Unity import it, then run this again"); return false; }

            // The gnats are emitted from these meshes, and a particle shape's mesh must be read/write enabled
            // (Manual/PartSysShapeModule.html), or they stay empty in a build
            bool changed = !importer.isReadable;
            importer.isReadable = true;
            foreach (var pair in new[] { new { Name = ModelGlass, Material = glass }, new { Name = ModelWire, Material = wire },
                                         new { Name = ModelHalo, Material = halo } })
            {
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Name);
                Object current;
                if (importer.GetExternalObjectMap().TryGetValue(id, out current) && current == pair.Material) continue;
                importer.AddRemap(id, pair.Material);
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
                Did(StringLightsModel + ": glass " + glass.name + ", wire " + wire.name + ", halo " + halo.name + ", read/write on for the gnats");
            }
            return true;
        }
    }
}
