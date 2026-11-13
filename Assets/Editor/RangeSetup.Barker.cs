using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    public static partial class RangeSetup
    {
        private const string BarkerName = "Barker";
        private const string SkeletonPath = "Assets/Models/skeleton/skeleton.fbx";
        private const string TopHatPath = "Assets/Models/TopHat/TopHat.fbx";
        private const string SkeletonTextures = "Assets/Models/skeleton/lambert13_";
        private const string TopHatTextures = "Assets/Models/TopHat/TopHat_lambert2SG1_";
        private const string SkeletonMaterialPath = "Assets/Materials/SR_Skeleton.mat";
        private const string TopHatMaterialPath = "Assets/Materials/SR_TopHat.mat";
        private const float BarkerGloss = 0.3f;
        private const float BarkerFillsLength = 0.85f;
        private const float BarkerFillsDepth = 0.9f;
        // The torso piece carries the pelvis, so the waist bends low on it and a bow leaves the legs where they are
        private const float WaistUpTorso = 0.12f;
        // A joint sits in the middle of the top few percent of the piece hanging from it
        private const float JointTopShare = 0.05f;

        // Maya's default names, matched to the parts by their bounds (plans/skeleton-barker.md). L is his left
        private static readonly Dictionary<string, string> BarkerPieces = new Dictionary<string, string>
        {
            { "Skull", "polySurface353" }, { "Jaw", "polySurface354" }, { "Torso", "polySurface440" },
            { "Upper Arm L", "polySurface407" }, { "Forearm L", "polySurface409" }, { "Hand L", "polySurface419" },
            { "Upper Arm R", "polySurface373" }, { "Forearm R", "polySurface374" }, { "Hand R", "polySurface385" },
            { "Thigh L", "polySurface427" }, { "Shin L", "polySurface423" }, { "Foot L", "polySurface431" },
            { "Thigh R", "polySurface393" }, { "Shin R", "polySurface391" }, { "Foot R", "polySurface398" },
        };

        private static readonly string[] BarkerLegs = { "Thigh L", "Shin L", "Foot L", "Thigh R", "Shin R", "Foot R" };

        [MenuItem("Shooting Range/Setup/64 - Skeleton Barker In The Coffin", false, 87)]
        public static void SkeletonBarkerMenu()
        {
            var coffin = Object.FindObjectOfType<Coffin>();
            if (coffin == null) { Debug.LogWarning("[RangeSetup] No Coffin in the scene"); return; }

            if (!EditorUtility.DisplayDialog("Skeleton barker in the coffin",
                    "This stands the skeleton and his top hat in the coffin, on joints placed from the model's own pieces, and adds " +
                    "Skeleton Barker to him.\n\n" +
                    "It DELETES any '" + BarkerName + "' already in the scene, with any hand edits to it and anything attached under it, " +
                    "his stand included.\n\n" +
                    "It makes " + Path.GetFileName(SkeletonMaterialPath) + " and " + Path.GetFileName(TopHatMaterialPath) + " once (kept " +
                    "after), and sets both models' Normal_OpenGL textures to Normal Map. The scene is saved afterwards.",
                    "Do it", "Cancel"))
                return;

            Begin("Skeleton barker in the coffin");
            BuildTheBarker(coffin);
            End();
        }

        private static void BuildTheBarker(Coffin coffin)
        {
            var skeleton = RangeSetupUtil.Load<GameObject>(SkeletonPath);
            var topHat = RangeSetupUtil.Load<GameObject>(TopHatPath);
            var bottom = CoffinPart(coffin, "bottom");
            var lid = CoffinPart(coffin, "lid");
            if (skeleton == null || topHat == null) { Fail("No " + SkeletonPath + " or " + TopHatPath); return; }
            if (bottom == null || lid == null) { Fail("The coffin has no mesh named bottom or lid to fit him to"); return; }

            var old = RangeSetupUtil.FindInScene(BarkerName);
            if (old != null) { Object.DestroyImmediate(old); Did("Deleted the old " + BarkerName); }

            // Built upright at the origin in the model's own space, then placed
            var root = new GameObject(BarkerName).transform;
            var model = Object.Instantiate(skeleton, root, false);
            var hatModel = Object.Instantiate(topHat, root, false);

            var pieces = new Dictionary<string, Transform>();
            var filters = model.GetComponentsInChildren<MeshFilter>(true);
            foreach (var pair in BarkerPieces)
            {
                var found = filters.FirstOrDefault(f => f.name == pair.Value);
                if (found == null)
                {
                    Fail("The skeleton has no " + pair.Value + " (his " + pair.Key + ")");
                    Object.DestroyImmediate(root.gameObject);
                    return;
                }

                found.name = pair.Key;
                pieces[pair.Key] = found.transform;
            }

            var hatPiece = hatModel.GetComponentInChildren<MeshFilter>(true);
            if (hatPiece == null) { Fail("The top hat has no mesh"); Object.DestroyImmediate(root.gameObject); return; }

            // The step out and the lean assume he faces +Z, so a model facing the other way is turned round first
            if (PieceBox(pieces["Foot L"], pieces["Foot R"]).center.z < PieceBox(pieces["Torso"]).center.z)
            {
                model.transform.Rotate(0f, 180f, 0f, Space.World);
                hatModel.transform.Rotate(0f, 180f, 0f, Space.World);
            }

            var feet = PieceBox(pieces["Foot L"], pieces["Foot R"]);
            var torso = PieceBox(pieces["Torso"]);
            var skull = PieceBox(pieces["Skull"]);
            var jawBox = PieceBox(pieces["Jaw"]);

            var body = Joint("Body", root, new Vector3(feet.center.x, feet.min.y, feet.center.z));
            var waist = Joint("Waist", body, new Vector3(torso.center.x, torso.min.y + torso.size.y * WaistUpTorso, torso.center.z));
            var neck = Joint("Neck", waist, new Vector3(skull.center.x, skull.min.y, skull.center.z));
            var jaw = Joint("Jaw Hinge", neck, new Vector3(jawBox.center.x, jawBox.max.y, jawBox.min.z));
            foreach (var leg in BarkerLegs) pieces[leg].SetParent(body, true);
            pieces["Torso"].SetParent(waist, true);
            pieces["Skull"].SetParent(neck, true);
            pieces["Jaw"].SetParent(jaw, true);
            hatModel.name = "Hat";
            hatModel.transform.SetParent(neck, true);

            foreach (var side in new[] { "L", "R" })
            {
                var upper = pieces["Upper Arm " + side];
                var fore = pieces["Forearm " + side];
                var hand = pieces["Hand " + side];
                var shoulder = Joint("Shoulder " + side, waist, TopOf(upper));
                var elbow = Joint("Elbow " + side, shoulder, TopOf(fore));
                var wrist = Joint("Wrist " + side, elbow, TopOf(hand));
                upper.SetParent(shoulder, true);
                fore.SetParent(elbow, true);
                hand.SetParent(wrist, true);
            }

            Object.DestroyImmediate(model);

            var all = pieces.Values.Concat(new[] { hatPiece.transform }).SelectMany(piece => PiecePoints(piece)).ToArray();
            float low = all.Min(p => p.y), height = all.Max(p => p.y) - low;
            float back = all.Min(p => p.z), depth = all.Max(p => p.z) - back;
            float middle = (all.Min(p => p.x) + all.Max(p => p.x)) * 0.5f;

            // The coffin's length runs up, its depth runs out toward the lid, measured off the bottom's own axes
            var shell = PiecePoints(bottom).ToArray();
            var axes = new[] { bottom.right, bottom.up, bottom.forward };
            var spans = axes.Select(axis => shell.Max(p => Vector3.Dot(p, axis)) - shell.Min(p => Vector3.Dot(p, axis))).ToArray();
            Vector3 head = axes[System.Array.IndexOf(spans, spans.Max())];
            Vector3 outward = axes[System.Array.IndexOf(spans, spans.Min())];
            if (Vector3.Dot(head, Vector3.up) < 0f) head = -head;

            var lidPoints = PiecePoints(lid).ToArray();
            Vector3 shellMiddle = shell.Aggregate(Vector3.zero, (sum, p) => sum + p) / shell.Length;
            Vector3 lidMiddle = lidPoints.Aggregate(Vector3.zero, (sum, p) => sum + p) / lidPoints.Length;
            if (Vector3.Dot(lidMiddle - shellMiddle, outward) < 0f) outward = -outward;
            Vector3 across = Vector3.Cross(head, outward).normalized;

            float headMin = shell.Min(p => Vector3.Dot(p, head)), length = shell.Max(p => Vector3.Dot(p, head)) - headMin;
            float outMin = shell.Min(p => Vector3.Dot(p, outward)), inside = shell.Max(p => Vector3.Dot(p, outward)) - outMin;
            float acrossMiddle = (shell.Min(p => Vector3.Dot(p, across)) + shell.Max(p => Vector3.Dot(p, across))) * 0.5f;

            float byLength = BarkerFillsLength * length / height;
            float byDepth = BarkerFillsDepth * inside / depth;
            float scale = Mathf.Min(byLength, byDepth);
            var rotation = Quaternion.LookRotation(outward, head);
            root.localScale = Vector3.one * scale;
            root.rotation = rotation;
            root.position = across * acrossMiddle + head * (headMin + (length - height * scale) * 0.5f) +
                            outward * (outMin + (inside - depth * scale) * 0.5f) - rotation * (new Vector3(middle, low, back) * scale);
            root.SetParent(coffin.transform.parent, true);

            Did("Stands " + (height * scale).ToString("0.00") + " m tall in the coffin (" + length.ToString("0.00") + " m long, " +
                inside.ToString("0.00") + " m deep), facing out of it");
            if (byDepth < byLength)
                Say("   ^ held to that by the coffin's depth: filling " + BarkerFillsLength * 100f + "% of its length would make him " +
                    (height * byLength).ToString("0.00") + " m and push him through the shut lid");

            var bone = PbrMaterial(SkeletonMaterialPath, SkeletonTextures);
            var felt = PbrMaterial(TopHatMaterialPath, TopHatTextures);
            foreach (var piece in pieces.Values)
            {
                Paint(piece, bone);
                piece.gameObject.AddComponent<BoxCollider>();
            }

            Paint(hatPiece.transform, felt);
            hatPiece.gameObject.AddComponent<BoxCollider>();

            jaw.SetParent(pieces["Skull"], true);
            hatModel.transform.SetParent(pieces["Skull"], true);

            var barker = root.gameObject.AddComponent<SkeletonBarker>();
            new Fields(barker)
                .Set("game", Object.FindObjectOfType<RangeGame>()).Set("coffin", coffin).Set("jaw", jaw)
                .SetArray("hitClips", Enumerable.Range(1, 4).Select(i => (Object)Sound("World/env_bone_0" + i)).ToList())
                .SetArray("clunkClips", new List<Object> { Sound("World/rail_clunk_01") })
                .Apply();

            Dirty();
            Did("Joint markers: body at his feet, waist, neck, jaw, and shoulder, elbow and wrist on each arm. Box colliders on every " +
                "piece and the hat. Attach his stand under " + BarkerName + ", then run step 65 to hang him on it");
        }

        private static Transform CoffinPart(Coffin coffin, string named)
        {
            var found = coffin.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.name.ToLowerInvariant().Contains(named));
            return found != null ? found.transform : null;
        }

        private static Transform Joint(string name, Transform parent, Vector3 at)
        {
            var joint = new GameObject(name).transform;
            joint.SetParent(parent, false);
            joint.position = at;
            return joint;
        }

        private static IEnumerable<Vector3> PiecePoints(Transform piece)
        {
            return piece.GetComponent<MeshFilter>().sharedMesh.vertices.Select(v => piece.TransformPoint(v));
        }

        private static Bounds PieceBox(params Transform[] pieces)
        {
            var points = pieces.SelectMany(piece => PiecePoints(piece)).ToArray();
            var box = new Bounds(points[0], Vector3.zero);
            foreach (var point in points) box.Encapsulate(point);
            return box;
        }

        /// Where a piece hangs from: its top end, as every limb piece hangs down from its joint in the model's pose
        private static Vector3 TopOf(Transform piece)
        {
            var points = PiecePoints(piece).ToArray();
            float top = points.Max(p => p.y);
            float cut = top - (top - points.Min(p => p.y)) * JointTopShare;
            var near = points.Where(p => p.y >= cut).ToArray();
            return near.Aggregate(Vector3.zero, (sum, p) => sum + p) / near.Length;
        }

        /// Standard, from the textures the model came with. Made once and kept: tune it there
        private static Material PbrMaterial(string path, string textures)
        {
            var material = RangeSetupUtil.Load<Material>(path);
            if (material != null) return material;

            string normalPath = textures + "Normal_OpenGL.png";
            var importer = AssetImporter.GetAtPath(normalPath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }

            material = new Material(Shader.Find("Standard"));
            material.SetTexture("_MainTex", RangeSetupUtil.Load<Texture2D>(textures + "Base_color.png"));
            material.SetTexture("_OcclusionMap", RangeSetupUtil.Load<Texture2D>(textures + "Mixed_AO.png"));
            material.SetFloat("_Glossiness", BarkerGloss);

            var normal = RangeSetupUtil.Load<Texture2D>(normalPath);
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }

            AssetDatabase.CreateAsset(material, path);
            Did("Made " + Path.GetFileName(path) + " (Standard: colour, normal and occlusion from the model's textures); tune it there");
            return material;
        }

        private static void Paint(Transform piece, Material material)
        {
            var renderer = piece.GetComponent<MeshRenderer>();
            if (renderer == null || material == null) return;

            renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
        }
    }
}
