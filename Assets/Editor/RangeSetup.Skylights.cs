using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    public static partial class RangeSetup
    {
        private const string SkylightWindow = "Assets/Mega Fantasy Props Pack/FBX/tower_window.000.fbx";
        private const string SunbeamShader = "CrystalCatch/Sunbeam";
        private const string SunbeamMaterialPath = "Assets/Materials/SR_Sunbeam.mat";
        private const string BoardMaterialPath = "Assets/Mega Fantasy Props Pack/Materials/wood_2_d.mat";
        private const string GlassTexturePath = "Assets/Textures/SR_GrimyGlass.png";
        private const string GlassMaterialPath = "Assets/Materials/SR_GrimyGlass.mat";
        private const string GlassName = "Glass";
        private const float SkylightSize = 1f;
        private const float SunbeamRadius = 0.5f;
        private const float BoardThickness = 0.04f;
        private const float BoardMass = 1f;
        private const float BoardImpulse = 0.6f;
        private const int BoardCount = 3;

        private static readonly string[][] SkylightTiers =
        {
            new[] { "Floor_Across_1", "Floor_Across_1 (1)" },
            new[] { "Floor_Across_2", "Floor_Across_2 (1)" },
        };

        [MenuItem("Shooting Range/Setup/35 - Open The Skylights", false, 58)]
        public static void SkylightsMenu() { Begin("Skylights"); OpenTheSkylights(); End(); }

        /// Fills blanks: a skylight already over its tier only has its boards brought up to date. Delete it and
        /// re-run to lay it out again
        private static void OpenTheSkylights()
        {
            var material = SunbeamMaterial();
            if (material == null) return;

            var boardMaterial = RangeSetupUtil.Load<Material>(BoardMaterialPath);
            if (boardMaterial == null) Say(BoardMaterialPath + " is missing, so the boards stay grey");

            var window = RangeSetupUtil.Load<GameObject>(SkylightWindow);
            if (window == null) Say(SkylightWindow + " is missing, so the skylights are boards and a beam only");

            foreach (var tier in SkylightTiers)
            {
                string name = "Skylight " + tier[0];
                var existing = RangeSetupUtil.FindInScene(name);
                if (existing != null) { UpdateTheBoards(existing, boardMaterial); continue; }

                var middles = new List<Vector3>();
                foreach (var railName in tier)
                {
                    var found = RangeSetupUtil.FindInScene(railName);
                    var rail = found != null ? found.GetComponent<TrackRail>() : null;
                    if (rail != null) middles.Add(rail.PointAt(rail.Length * 0.5f));
                }

                if (middles.Count == 0) { Fail("No " + string.Join(" or ", tier) + " in the scene"); continue; }

                var middle = Vector3.zero;
                foreach (var point in middles) middle += point / middles.Count;

                float ceiling = RoomSurface(middle + Vector3.up * 0.5f, Vector3.up, float.NaN);
                if (float.IsNaN(ceiling)) { Fail(name + ": no ceiling collider above " + tier[0]); continue; }

                float floor = RoomSurface(new Vector3(middle.x, ceiling - 0.05f, middle.z), Vector3.down, 0f);
                BuildSkylight(name, new Vector3(middle.x, ceiling, middle.z), ceiling - floor, window, material, boardMaterial);
            }
        }

        private static void BuildSkylight(string name, Vector3 at, float drop, GameObject window, Material material,
                                          Material boardMaterial)
        {
            var root = new GameObject(name);
            root.transform.position = at;

            if (window != null)
            {
                var frame = (GameObject)PrefabUtility.InstantiatePrefab(window);
                frame.transform.SetParent(root.transform, false);
                frame.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                foreach (var collider in frame.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);

                var size = BoundsOf(frame.GetComponentsInChildren<Renderer>());
                frame.transform.localScale *= SkylightSize / Mathf.Max(0.001f, Mathf.Max(size.size.x, size.size.z));
                size = BoundsOf(frame.GetComponentsInChildren<Renderer>());
                frame.transform.position += at - new Vector3(size.center.x, size.max.y, size.center.z);
            }

            Glaze(root);

            var bodies = new List<Object>();
            for (int i = 0; i < BoardCount; i++)
            {
                var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
                board.name = "Board " + (i + 1);
                board.transform.SetParent(root.transform, false);
                board.transform.localPosition = new Vector3(0f, -BoardThickness * 0.5f - 0.01f,
                                                            (i - (BoardCount - 1) * 0.5f) * SkylightSize / BoardCount);
                board.transform.localScale = new Vector3(SkylightSize, BoardThickness, SkylightSize / BoardCount * 0.9f);

                if (boardMaterial != null) board.GetComponent<MeshRenderer>().sharedMaterial = boardMaterial;

                var body = board.AddComponent<Rigidbody>();
                body.mass = BoardMass;
                body.isKinematic = true;
                bodies.Add(body);
            }

            var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(beam.GetComponent<Collider>());
            beam.name = "Sunbeam";
            beam.transform.SetParent(root.transform, false);
            beam.transform.localPosition = Vector3.down * drop * 0.5f;
            beam.transform.localScale = new Vector3(SunbeamRadius * 2f, drop * 0.5f, SunbeamRadius * 2f);

            var renderer = beam.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            new Fields(root.AddComponent<ReleaseOnShot>()).SetArray("pieces", bodies)
                .Set("impulse", BoardImpulse).Set("nudge", 0.2f).Set("lift", 0f).Set("oneAtATime", true).Apply();
            new Fields(root.AddComponent<Skylight>()).Set("beam", renderer).Set("radius", SunbeamRadius).Apply();

            Dirty();
            Did(name + " at " + at.ToString("F2") + ", its beam " + drop.ToString("0.00") +
                " m down. Greybox boards; check the window sits flat in the roof");
        }

        /// Built before 2026-10-27's second pass: every board came off at the first hit, and they were untextured
        private static void UpdateTheBoards(GameObject skylight, Material boardMaterial)
        {
            var release = skylight.GetComponent<ReleaseOnShot>();
            if (release != null && !new Fields(release).Bool("oneAtATime"))
            {
                new Fields(release).Set("oneAtATime", true).Apply();
                Did(skylight.name + ": its boards now come off one a shot, so it takes " + BoardCount + " to let the sun in");
                Dirty();
            }

            if (RangeSetupUtil.Child(skylight.transform, GlassName) == null && Glaze(skylight))
            {
                Did(skylight.name + ": glazed, behind the boards");
                Dirty();
            }

            if (boardMaterial == null) return;

            int dressed = 0;
            foreach (Transform child in skylight.transform)
            {
                var renderer = child.name.StartsWith("Board") ? child.GetComponent<MeshRenderer>() : null;
                if (renderer == null || renderer.sharedMaterial == boardMaterial) continue;

                renderer.sharedMaterial = boardMaterial;
                dressed++;
            }

            if (dressed > 0) { Did(skylight.name + ": " + dressed + " boards in " + boardMaterial.name); Dirty(); }
            else Say(skylight.name + " is already up to date, left alone");
        }

        /// Old grimy glass in the frame, just above the boards, facing down at the player
        private static bool Glaze(GameObject skylight)
        {
            var material = GlassMaterial();
            if (material == null) return false;

            var pane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(pane.GetComponent<Collider>());
            pane.name = GlassName;
            pane.transform.SetParent(skylight.transform, false);
            pane.transform.localPosition = Vector3.down * 0.005f;
            pane.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            pane.transform.localScale = Vector3.one * SkylightSize * 0.9f;

            var renderer = pane.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return true;
        }

        /// Standard, set to Fade the way its own Inspector does it, so the grime's alpha shows
        private static Material GlassMaterial()
        {
            var material = RangeSetupUtil.Load<Material>(GlassMaterialPath);
            if (material != null) return material;

            var texture = RangeSetupUtil.Load<Texture2D>(GlassTexturePath);
            if (texture == null) { Fail(GlassTexturePath + " is missing, so the skylights have no glass"); return null; }

            material = new Material(Shader.Find("Standard")) { mainTexture = texture };
            material.SetFloat("_Mode", 2f);
            material.SetFloat("_Glossiness", 0.8f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            AssetDatabase.CreateAsset(material, GlassMaterialPath);
            Did("Made " + System.IO.Path.GetFileName(GlassMaterialPath));
            return material;
        }

        private static Material SunbeamMaterial()
        {
            var material = RangeSetupUtil.Load<Material>(SunbeamMaterialPath);
            if (material != null) return material;

            var shader = Shader.Find(SunbeamShader);
            if (shader == null) { Fail(SunbeamShader + " has not compiled. Check the console for shader errors"); return null; }

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, SunbeamMaterialPath);
            Did("Made " + System.IO.Path.GetFileName(SunbeamMaterialPath) + ", tune the sunlight's colour there");
            return material;
        }
    }
}
