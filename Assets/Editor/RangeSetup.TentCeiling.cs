using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    /// Step 79: swaps in the tent ceiling (plan: LiminalVR/CrystalCatch/plans/tent-ceiling-2026-11-13.md). The model is built
    /// by art/tent-ceiling/build_tent_ceiling.py in Local Docs; its axes were checked on 2026-11-13. Alex's ceiling is
    /// switched off, not deleted. Safe to run again: it only fills in what is missing and re-places the skylights
    public static partial class RangeSetup
    {
        private const string TentFolder = "Assets/Models/TentCeiling";
        private const string TentPath = TentFolder + "/TentCeiling.fbx";
        private const string TentName = "TentCeiling";
        private static readonly string[] TentParts = { "Canvas", "Valance" };
        // The build script's materials, and what each becomes in Unity
        private static readonly string[,] TentMaterials =
            { { "M_TentCanvas", "SR_TentCanvas" }, { "M_TentWood", "SR_TentWood" }, { "M_SkylightSky", "SR_SkylightSky" } };
        private static readonly Color TentWood = new Color(0.12f, 0.07f, 0.04f);
        // Daylight beyond each skylight's glass, unlit so it reads as outside (the lid closing the opening)
        private static readonly Color SkylightSky = new Color(0.62f, 0.68f, 0.74f);
        private const string ClothShader = "CrystalCatch/Cloth Ripple";
        private const string ClothTestCanvas = "Assets/Materials/SR_ClothTest_Canvas.mat";
        private const float TentSoot = 0.55f;
        // Alex's ceiling: his planes at 4.18 m and the beams under them, inside the range room only
        private static readonly Bounds AlexCeiling = new Bounds(new Vector3(0.2f, 4.2f, 5.3f), new Vector3(16f, 1.2f, 21f));

        [MenuItem("Shooting Range/Setup/79 - Swap In The Tent Ceiling", false, 101)]
        public static void SwapInTheTentMenu()
        {
            if (!EditorUtility.DisplayDialog("Swap in the tent ceiling",
                    "This puts the tent ceiling in at the origin, sets its materials, lightmap UVs, colliders and static flags, " +
                    "SWITCHES OFF Alex's ceiling planes (floor (6)-(17)) and ceiling wood beams (not deleted), and MOVES both " +
                    "skylights into the tent's openings, lengthening their sunbeams to match. The scene is saved afterwards. " +
                    "Then: 30b, 46, and the bake (39).", "Swap it in", "Cancel"))
                return;

            Begin("Tent ceiling swapped in");
            try { SwapInTheTent(); }
            catch (System.Exception e) { Fail("Stopped by an error, so nothing after this point ran: " + e); }
            finally { End(); }
        }

        private static void SwapInTheTent()
        {
            if (!SetUpTheTentImport()) return;

            var model = RangeSetupUtil.Load<GameObject>(TentPath);
            var tent = RangeSetupUtil.FindInScene(TentName);
            if (tent == null)
            {
                tent = (GameObject)PrefabUtility.InstantiatePrefab(model);
                tent.name = TentName;
                tent.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Did(TentName + " added at the origin");
            }

            foreach (var part in TentParts)
            {
                var child = RangeSetupUtil.Child(tent.transform, part);
                if (child == null) { Fail(TentName + " has no " + part); continue; }

                GameObjectUtility.SetStaticEditorFlags(child.gameObject,
                    GameObjectUtility.GetStaticEditorFlags(child.gameObject) | StaticEditorFlags.LightmapStatic | StaticEditorFlags.BatchingStatic);
                // The setup steps find the ceiling by collider: the roof rails (30b), the string lights (46), the skylights (35)
                if (child.GetComponent<MeshCollider>() == null) { child.gameObject.AddComponent<MeshCollider>(); Did(part + ": collider"); }
            }

            SwitchOffAlexsCeiling();
            LayAlexsBeams(tent.transform);
            FrameTheSkylights(tent.transform);
            StackThePillars();
            PutTheSkylightsInTheTent(tent.transform);
            Dirty();
            Say("Next: 30b (roof rails and lanes to the new ceiling), 46 (strings 0.2 m under it), then bake (39, about 30 minutes)");
        }

        /// Lightmap UVs and the two materials live on the model's importer, so every copy of it gets them
        private static bool SetUpTheTentImport()
        {
            var importer = AssetImporter.GetAtPath(TentPath) as ModelImporter;
            if (importer == null) { Fail("No " + TentPath + ". Let Unity import it, then run this again"); return false; }

            bool changed = false;
            if (!importer.generateSecondaryUV) { importer.generateSecondaryUV = true; changed = true; }

            for (int i = 0; i < TentMaterials.GetLength(0); i++)
            {
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), TentMaterials[i, 0]);
                var material = TentMaterial(TentMaterials[i, 1]);
                Object current;
                if (importer.GetExternalObjectMap().TryGetValue(id, out current) && current == material) continue;

                importer.AddRemap(id, material);
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
                Did(TentPath + ": lightmap UVs on, materials " + TentMaterials[0, 1] + " and " + TentMaterials[1, 1]);
            }

            return true;
        }

        private static Material TentMaterial(string name)
        {
            string path = TentFolder + "/" + name + ".mat";
            var material = RangeSetupUtil.Load<Material>(path);
            bool canvas = name == TentMaterials[0, 1];

            if (material == null)
            {
                bool sky = name == TentMaterials[2, 1];
                material = new Material(Shader.Find(sky ? "Unlit/Color" : "Standard"));
                if (sky) material.color = SkylightSky;
                else if (!canvas) { material.color = TentWood; material.SetFloat("_Glossiness", 0.05f); }
                AssetDatabase.CreateAsset(material, path);
                Did("Made " + path + ", tune it there");
            }

            if (canvas) MakeItCloth(material);
            return material;
        }

        /// The canvas ripples like the cloth test (user, 2026-11-13: "the material we used for the canvas test"): the same
        /// shader and the test's tuned ripple, with the stripes on. The model's vertex red says how free each point is
        private static void MakeItCloth(Material material)
        {
            var shader = Shader.Find(ClothShader);
            if (shader == null) { Fail(ClothShader + " has not compiled. Check the console for shader errors"); return; }
            if (material.shader == shader) return;

            var test = RangeSetupUtil.Load<Material>(ClothTestCanvas);
            material.shader = shader;
            if (test != null) material.CopyPropertiesFromMaterial(test);
            material.mainTexture = RangeSetupUtil.Load<Texture2D>(TentFolder + "/T_TentStripes.png");
            material.color = Color.white;
            // Soot where the canvas is pinned at the beams and walls (user, 2026-11-13: "too clean")
            material.SetFloat("_Soot", TentSoot);
            EditorUtility.SetDirty(material);
            Did(material.name + ": " + ClothShader + (test != null ? ", ripple copied from " + ClothTestCanvas : ", default ripple (no test material found)") +
                ", stripes on");
        }

        private static void SwitchOffAlexsCeiling()
        {
            var off = new List<string>();
            foreach (var renderer in Object.FindObjectsOfType<MeshRenderer>())
            {
                var holder = renderer.transform;
                while (holder.parent != null && !IsAlexsCeiling(holder.name)) holder = holder.parent;
                if (!IsAlexsCeiling(holder.name) || !holder.gameObject.activeSelf) continue;

                var bounds = renderer.bounds;
                bool ceilingHigh = bounds.min.y > 3.5f;
                if (!ceilingHigh || !AlexCeiling.Contains(bounds.center)) continue;

                holder.gameObject.SetActive(false);
                off.Add(holder.name);
            }

            if (off.Count > 0) Did("Switched off (not deleted) " + off.Count + " of Alex's ceiling pieces: " + string.Join(", ", off.ToArray()));
            else Say("Alex's ceiling is already off");
        }

        private const string AlexsBeamPath = "Assets/Mesh/wood_beam/wood_beam.fbx";
        private const string TentBeamsName = "Tent Beams";

        /// Alex's wood_beam end to end along each beam line of the tent, as many as cover it, stretched a little to fit (user,
        /// 2026-11-13: "we should be using Alex's wood beams"). Laid again on every run: generated from the BEAM_n/BEAMEND_n empties
        private static void LayAlexsBeams(Transform tent)
        {
            var model = RangeSetupUtil.Load<GameObject>(AlexsBeamPath);
            if (model == null) { Fail("No " + AlexsBeamPath); return; }

            var old = RangeSetupUtil.FindInScene(TentBeamsName);
            if (old != null) Object.DestroyImmediate(old);
            var holder = new GameObject(TentBeamsName).transform;

            // Measured, not assumed: where his beam sits round its own pivot
            var probe = (GameObject)PrefabUtility.InstantiatePrefab(model);
            probe.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var size = BoundsOf(probe.GetComponentsInChildren<Renderer>());
            Object.DestroyImmediate(probe);
            if (size.size.x < size.size.z) { Fail(AlexsBeamPath + " does not run along x at no rotation, so it was not laid"); return; }

            int laid = 0;
            for (int n = 1; ; n++)
            {
                var start = RangeSetupUtil.Child(tent, "BEAM_" + n);
                var end = RangeSetupUtil.Child(tent, "BEAMEND_" + n);
                if (start == null || end == null) break;

                float length = end.position.x - start.position.x;
                int pieces = Mathf.Max(1, Mathf.CeilToInt(length / size.size.x - 0.05f));
                float stretch = length / (pieces * size.size.x);
                for (int k = 0; k < pieces; k++)
                {
                    var beam = (GameObject)PrefabUtility.InstantiatePrefab(model, holder);
                    beam.name = "wood_beam (tent " + n + "." + (k + 1) + ")";
                    beam.transform.localScale = new Vector3(stretch, 1f, 1f);
                    beam.transform.position = new Vector3(start.position.x + k * size.size.x * stretch - size.min.x * stretch,
                                                          start.position.y - size.min.y, start.position.z - size.center.z);
                    GameObjectUtility.SetStaticEditorFlags(beam, StaticEditorFlags.LightmapStatic | StaticEditorFlags.BatchingStatic);
                    laid++;
                }
            }

            Did(TentBeamsName + ": " + laid + " of Alex's beams laid along the tent's beam lines. Run step 83 to give them colliders");
        }

        private const string PillarTopsName = "Tent Pillar Tops";
        // The old ceiling's pillars: their tops met it (step 77)
        private const float PillarMetCeilingFrom = 3.9f;

        /// A copy of Alex's own pillar on each pillar that met the old ceiling, upside down with its top on the pillar's top, so the
        /// two caps meet like a reflection and the copy rises on into the roof (user, 2026-11-13: stacked inside each other they
        /// "don't look right"; "flip them upside down and line them up at their tops"). Laid again on every run
        private static void StackThePillars()
        {
            var old = RangeSetupUtil.FindInScene(PillarTopsName);
            if (old != null) Object.DestroyImmediate(old);
            var holder = new GameObject(PillarTopsName).transform;

            int stacked = 0;
            var done = new HashSet<Transform>();
            foreach (var renderer in Object.FindObjectsOfType<MeshRenderer>())
            {
                var pillar = PillarOf(renderer.transform);
                if (pillar == null || pillar.IsChildOf(holder) || !done.Add(pillar)) continue;

                var below = BoundsOf(pillar.GetComponentsInChildren<Renderer>());
                if (below.max.y < PillarMetCeilingFrom || below.max.y > OldCeiling + 0.05f)
                {
                    // Said, not silent: two of six went without a copy and nothing told why (step 85, 2026-11-13)
                    if (below.max.y > PillarMetCeilingFrom - 1f) Say(pillar.name + ": its top (with anything on it) is at " + below.max.y.ToString("0.00") + ", not at the old ceiling, so no copy");
                    continue;
                }

                var source = PrefabUtility.GetCorrespondingObjectFromSource(pillar.gameObject);
                if (source == null) { Fail(pillar.name + " is not a prefab or model copy, so it was not stacked"); continue; }

                var copy = (GameObject)PrefabUtility.InstantiatePrefab(source, holder);
                copy.name = "pillar (tent " + ++stacked + ")";
                copy.transform.SetPositionAndRotation(pillar.position, pillar.rotation * Quaternion.AngleAxis(180f, Vector3.right));
                copy.transform.localScale = pillar.lossyScale;

                var above = BoundsOf(copy.GetComponentsInChildren<Renderer>());
                copy.transform.position += new Vector3(below.center.x - above.center.x, below.max.y - above.min.y, below.center.z - above.center.z);
                GameObjectUtility.SetStaticEditorFlags(copy, StaticEditorFlags.LightmapStatic | StaticEditorFlags.BatchingStatic);
                Did(pillar.name + ": a copy upside down on its top, caps meeting at " + below.max.y.ToString("0.00"));
            }

            if (stacked == 0) Say("No pillar met the old ceiling");
        }

        private static Transform PillarOf(Transform t)
        {
            for (; t != null; t = t.parent)
                if (BaseName(t.name) == "pillar") return t;
            return null;
        }

        private const string SkylightFramesName = "Skylight Frames";

        /// Four short pieces of Alex's beam round each skylight opening, inner faces on its edge, from below the boards up past
        /// the canvas (user, 2026-11-13: in place of my collars). Laid again on every run, from the SKYLIGHTFRAME_n/SKYLIGHTTOP_n empties
        private static void FrameTheSkylights(Transform tent)
        {
            var model = RangeSetupUtil.Load<GameObject>(AlexsBeamPath);
            if (model == null) { Fail("No " + AlexsBeamPath); return; }

            var old = RangeSetupUtil.FindInScene(SkylightFramesName);
            if (old != null) Object.DestroyImmediate(old);
            var holder = new GameObject(SkylightFramesName).transform;

            var probe = (GameObject)PrefabUtility.InstantiatePrefab(model);
            probe.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var size = BoundsOf(probe.GetComponentsInChildren<Renderer>());
            Object.DestroyImmediate(probe);

            int framed = 0;
            for (int n = 1; ; n++)
            {
                var bottom = RangeSetupUtil.Child(tent, "SKYLIGHTFRAME_" + n);
                var top = RangeSetupUtil.Child(tent, "SKYLIGHTTOP_" + n);
                if (bottom == null || top == null) break;

                float half = SkylightSize * 0.5f, depth = size.size.z;
                float tall = top.position.y - bottom.position.y;
                var middle = bottom.position;
                // Front and back run along x over the corners; left and right run along z between them
                var sides = new[]
                {
                    new { At = middle + Vector3.back * (half + depth * 0.5f), Long = SkylightSize + 2f * depth, AlongZ = false },
                    new { At = middle + Vector3.forward * (half + depth * 0.5f), Long = SkylightSize + 2f * depth, AlongZ = false },
                    new { At = middle + Vector3.left * (half + depth * 0.5f), Long = SkylightSize, AlongZ = true },
                    new { At = middle + Vector3.right * (half + depth * 0.5f), Long = SkylightSize, AlongZ = true },
                };

                int k = 0;
                foreach (var side in sides)
                {
                    var piece = (GameObject)PrefabUtility.InstantiatePrefab(model, holder);
                    piece.name = "wood_beam (skylight " + n + "." + ++k + ")";
                    piece.transform.rotation = side.AlongZ ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
                    piece.transform.localScale = new Vector3(side.Long / size.size.x, tall / size.size.y, 1f);
                    var placed = BoundsOf(piece.GetComponentsInChildren<Renderer>());
                    piece.transform.position += new Vector3(side.At.x - placed.center.x, middle.y - placed.min.y, side.At.z - placed.center.z);
                    GameObjectUtility.SetStaticEditorFlags(piece, StaticEditorFlags.LightmapStatic | StaticEditorFlags.BatchingStatic);
                }
                framed++;
            }

            Did(SkylightFramesName + ": " + framed + " skylights framed with Alex's beams. Run step 83 to give them colliders");
        }

        private static bool IsAlexsCeiling(string name)
        {
            return name.StartsWith("floor (") || name.StartsWith("wood_beam");
        }

        /// Onto the build script's SKYLIGHT_n empties: the skylight's own position is its burn axis, and its beam grows by
        /// however far it rose, so it still reaches the floor
        private static void PutTheSkylightsInTheTent(Transform tent)
        {
            var pairs = new[] { new[] { "SKYLIGHT_1", "Skylight Floor_Across_1" }, new[] { "SKYLIGHT_2", "Skylight Floor_Across_2" } };
            foreach (var pair in pairs)
            {
                var anchor = RangeSetupUtil.Child(tent, pair[0]);
                var found = RangeSetupUtil.FindInScene(pair[1]);
                if (anchor == null || found == null) { Fail("Missing " + pair[0] + " in the tent or " + pair[1] + " in the scene"); continue; }

                var skylight = found.transform;
                var from = skylight.position;
                if (Vector3.Distance(from, anchor.position) < 0.005f) { Say(pair[1] + " is already in its opening"); continue; }

                float rise = anchor.position.y - from.y;
                skylight.position = anchor.position;

                var beam = RangeSetupUtil.Child(skylight, "Sunbeam");
                if (beam != null)
                {
                    float drop = beam.localScale.y * 2f + rise;
                    beam.localPosition = Vector3.down * drop * 0.5f;
                    beam.localScale = new Vector3(beam.localScale.x, drop * 0.5f, beam.localScale.z);
                }

                Did(pair[1] + ": " + from.ToString("F2") + " -> " + anchor.position.ToString("F2") +
                    (beam != null ? ", sunbeam " + (beam.localScale.y * 2f).ToString("0.00") + " m" : ", no Sunbeam child found"));
            }
        }
    }
}
