using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    /// Step 57: greybox for the fake-cloth test (notes/fake-cloth-test-plan-2026-11-02.md). A canvas roof over the whole room
    /// that rests on the beams and sags between them, always above the lights, open where the roof signs swing through
    /// (user, 2026-11-02), and a banner on each front pillar's
    /// inner face. Lightmap Static, so it bakes; it only ripples in the shader. Delete "Cloth Test (greybox)" to undo it
    public static partial class RangeSetup
    {
        private const string ClothTestName = "Cloth Test (greybox)";
        private const string ClothMeshDir = "Assets/Meshes/";
        private const float CanvasFrontFallback = 13f;
        private const float SlotMargin = 0.1f;
        private const float CanvasMostSag = 0.25f;
        private const float CanvasOverLights = 0.12f;
        private const float BannerWide = 0.45f;
        private const float BannerTall = 1f;
        private const float CanvasSway = 0.06f;
        // Small: the side signs fold flat a centimetre off the banner, and more would poke through them
        private const float BannerSway = 0.008f;

        [MenuItem("Shooting Range/Setup/57 - Fake Cloth Test (greybox canvas and banners)", false, 80)]
        public static void ClothTestMenu()
        {
            Begin("Fake cloth test");
            var old = RangeSetupUtil.FindInScene(ClothTestName);
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject(ClothTestName);
            Undo.RegisterCreatedObjectUndo(root, ClothTestName);

            var canvas = EyeMaterial("SR_ClothTest_Canvas", "CrystalCatch/Cloth Ripple", new Color(0.8f, 0.75f, 0.65f));
            var banner = EyeMaterial("SR_ClothTest_Banner", "CrystalCatch/Cloth Ripple", new Color(0.55f, 0.08f, 0.06f));
            if (canvas == null || banner == null) { End(); return; }
            canvas.SetFloat("_Amplitude", CanvasSway);
            canvas.SetFloat("_Speed", 0.8f);
            banner.SetFloat("_Amplitude", BannerSway);

            HangTheCanvas(root.transform, canvas);
            HangTheBanners(root.transform, banner);
            foreach (var piece in root.GetComponentsInChildren<Renderer>()) piece.gameObject.AddComponent<ClothAir>();
            Did("Passing rounds stir the cloth near them (Cloth Air on each piece, so a copy stirs too)");

            Say("Lightmap Static: run 39d to see it baked, and the full step 39 before judging the start-up");
            Dirty();
            End();
        }

        private static void HangTheCanvas(Transform parent, Material material)
        {
            var strings = RangeSetupUtil.FindInScene(StringLightsName);
            if (strings == null) { Fail("No " + StringLightsName + ", so no way to keep the canvas above the lights"); return; }
            float lights = strings.GetComponentsInChildren<Light>(true).Max(l => l.transform.position.y);

            var backdrop = RangeSetupUtil.FindInScene(Backdrop);
            var backdropRenderer = backdrop != null ? backdrop.GetComponentInChildren<Renderer>() : null;
            float back = WallAt(new Vector3(0f, 3.9f, 0f), Vector3.back, 2.6f).z;
            float front = backdropRenderer != null ? backdropRenderer.bounds.max.z : CanvasFrontFallback;
            float ceiling = RoomSurface(new Vector3(0f, 3f, 0f), Vector3.up, 4.18f);
            float pinY = ceiling - 0.02f;
            float sag = Mathf.Min(CanvasMostSag, pinY - lights - CanvasOverLights);
            if (sag < 0.03f) { Fail("Only " + (pinY - lights).ToString("0.00") + " m between the lights and the ceiling: no room to drape"); return; }

            var pins = new List<float> { back, front };
            pins.AddRange(Beams(back, front, ceiling));
            pins = pins.Distinct().OrderBy(z => z).ToList();
            float longest = 0f;
            for (int i = 1; i < pins.Count; i++) longest = Mathf.Max(longest, pins[i] - pins[i - 1]);

            var slots = RoofSignSlots();

            int across = 24;
            int along = Mathf.CeilToInt((front - back) * 4f);
            var points = new Vector3[(across + 1) * (along + 1)];
            var free = new Color[points.Length];
            for (int j = 0; j <= along; j++)
            {
                float z = Mathf.Lerp(back, front, j / (float)along);
                float left = WallAt(new Vector3(0f, 3.9f, z), Vector3.left, 4.5f).x;
                float right = WallAt(new Vector3(0f, 3.9f, z), Vector3.right, 4.5f).x;
                int span = Mathf.Clamp(pins.FindLastIndex(p => p <= z), 0, pins.Count - 2);
                float u = Mathf.InverseLerp(pins[span], pins[span + 1], z);
                float droop = 4f * u * (1f - u) * (pins[span + 1] - pins[span]) / longest;
                for (int i = 0; i <= across; i++)
                {
                    float v = i / (float)across;
                    float sideways = 4f * v * (1f - v);
                    points[j * (across + 1) + i] = new Vector3(Mathf.Lerp(left, right, v), pinY - sag * Mathf.Max(droop, 0.2f * sideways), z);
                    free[j * (across + 1) + i] = new Color(Mathf.Min(droop, sideways), 0f, 0f);
                }
            }

            // A quad whose middle is in a slot is left out, so the roof signs drop through the gap
            System.Func<int, int, bool> open = (i, j) =>
            {
                var middle = (points[j * (across + 1) + i] + points[(j + 1) * (across + 1) + i + 1]) * 0.5f;
                return slots.Any(slot => slot.Contains(new Vector3(middle.x, slot.center.y, middle.z)));
            };

            Place(parent, "Canvas", Grid(points, free, across, along, Vector3.down, "ClothTest_Canvas", open), material);
            Did("Canvas over the whole room, z " + back.ToString("0.00") + ".." + front.ToString("0.00") + ", pinned at y " + pinY.ToString("0.00") +
                " on " + (pins.Count - 2) + " beams, sagging " + sag.ToString("0.00") + " m, the lowest " + (pinY - sag - lights).ToString("0.00") +
                " m over the highest light, open over " + slots.Count + " roof rails");
        }

        /// Where each roof sign swings through on its way down: from just in front of its rail to a sign's height behind it,
        /// the way PopUpTarget folds a hanging sign, away from the player
        private static List<Bounds> RoofSignSlots()
        {
            float tallest = SignHeights().Values.DefaultIfEmpty(1.2f).Max();
            var slots = new List<Bounds>();
            foreach (var rail in Object.FindObjectsOfType<TrackRail>())
            {
                if (rail.Group != TrackGroup.RoofAcross && rail.Group != TrackGroup.RoofAlong) continue;

                float sign = rail.TallestSign > 0f ? rail.TallestSign : tallest;
                var slot = new Bounds(rail.Origin, Vector3.zero);
                slot.Encapsulate(rail.PointAt(rail.Length));
                slot.Encapsulate(rail.PointAt(rail.Length) + Vector3.forward * (sign + SlotMargin));
                slot.Expand(new Vector3(SlotMargin * 2f, 1f, SlotMargin * 2f));
                slots.Add(slot);
                Say("   open over " + rail.name + ": z " + slot.min.z.ToString("0.00") + ".." + slot.max.z.ToString("0.00"));
            }

            return slots;
        }

        private static Vector3 WallAt(Vector3 from, Vector3 direction, float fallback)
        {
            RaycastHit hit;
            return Physics.Raycast(from, direction, out hit, 10f, ~0, QueryTriggerInteraction.Ignore) ? hit.point : from + direction * fallback;
        }

        /// Beam bottoms found by looking up along the middle of the room: anything well under the ceiling
        private static IEnumerable<float> Beams(float from, float to, float ceiling)
        {
            bool inBeam = false;
            float start = 0f;
            for (float z = from; z <= to; z += 0.05f)
            {
                bool beam = RoomSurface(new Vector3(0f, 3f, z), Vector3.up, ceiling) < ceiling - 0.05f;
                if (beam && !inBeam) start = z;
                if (!beam && inBeam) yield return (start + z) * 0.5f;
                inBeam = beam;
            }
        }

        private static void HangTheBanners(Transform parent, Material material)
        {
            for (int i = 0; i < SidePillars.GetLength(0); i++)
            {
                var railObject = RangeSetupUtil.FindInScene(SidePillars[i, 0]);
                var pillarObject = RangeSetupUtil.FindInScene(SidePillars[i, 1]);
                var pillar = pillarObject != null ? pillarObject.GetComponentInChildren<MeshRenderer>() : null;
                var rail = railObject != null ? railObject.GetComponent<TrackRail>() : null;
                if (pillar == null || rail == null) { Fail("Missing " + SidePillars[i, 1] + " or " + SidePillars[i, 0]); continue; }

                var b = pillar.bounds;
                float inward = b.center.x < 0f ? 1f : -1f;
                float face = (inward > 0f ? b.max.x : b.min.x) + inward * 0.01f;
                float wide = Mathf.Min(BannerWide, b.size.z * 0.8f);
                float top = b.max.y - 0.2f;

                int across = 3, down = 10;
                var points = new Vector3[(across + 1) * (down + 1)];
                var free = new Color[points.Length];
                for (int j = 0; j <= down; j++)
                {
                    for (int k = 0; k <= across; k++)
                    {
                        points[j * (across + 1) + k] = new Vector3(face, top - BannerTall * j / down, b.center.z + wide * (k / (float)across - 0.5f));
                        free[j * (across + 1) + k] = new Color(j / (float)down, 0f, 0f);
                    }
                }

                var hung = Place(parent, "Banner " + pillarObject.name, Grid(points, free, across, down, Vector3.right * inward, "ClothTest_Banner_" + i), material);

                // The side signs fold flat against the pillar and whatever is on its face, so they stand off the banner too
                // A re-run deleted the last banner, which leaves an empty slot: those go, the pillar stays
                var parts = new Fields(rail).Find("mount");
                var kept = new List<Object>();
                for (int slot = 0; slot < parts.arraySize; slot++)
                {
                    var part = parts.GetArrayElementAtIndex(slot).objectReferenceValue;
                    if (part != null) kept.Add(part);
                }
                kept.Add(hung);
                new Fields(rail).SetArray("mount", kept).Apply();
                Did("Banner on " + pillarObject.name + "'s inner face, top y " + top.ToString("0.00") + ", wired into " + rail.name + "'s mount");
            }
        }

        /// A grid mesh saved as an asset, with lightmap UVs, its faces turned to face
        private static Mesh Grid(Vector3[] points, Color[] free, int across, int along, Vector3 face, string name,
                                 System.Func<int, int, bool> open = null)
        {
            var triangles = new List<int>();
            var uv = new Vector2[points.Length];
            for (int j = 0; j <= along; j++)
                for (int i = 0; i <= across; i++) uv[j * (across + 1) + i] = new Vector2(i / (float)across, j / (float)along);

            for (int j = 0; j < along; j++)
            {
                for (int i = 0; i < across; i++)
                {
                    if (open != null && open(i, j)) continue;
                    int a = j * (across + 1) + i, b = a + 1, c = a + across + 1, d = c + 1;
                    triangles.AddRange(new[] { a, c, b, b, c, d });
                }
            }

            var mesh = new Mesh { name = name, vertices = points, colors = free, uv = uv };
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            if (Vector3.Dot(mesh.normals.Aggregate(Vector3.zero, (sum, n) => sum + n), face) < 0f)
            {
                triangles.Reverse();
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
            }

            mesh.RecalculateBounds();
            Unwrapping.GenerateSecondaryUVSet(mesh);

            string path = ClothMeshDir + name + ".asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static Renderer Place(Transform parent, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.LightmapStatic);
            return renderer;
        }
    }
}
