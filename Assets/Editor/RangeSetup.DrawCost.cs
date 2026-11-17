using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    /// Step 85: where the triangles and draw calls go, by top-level group (user, 2026-11-13: "performance seems to have
    /// plummeted", 227k triangles and 384 batches in the Game view). Counts every active renderer, whether or not the camera
    /// sees it. Reads only; nothing in the scene changes
    public static partial class RangeSetup
    {
        private const int DrawCostRows = 20;

        [MenuItem("Shooting Range/Setup/85 - Measure Draw Cost (measure only)", false, 109)]
        public static void MeasureDrawCostMenu()
        {
            Begin("Draw cost by group, measured only, nothing changed");
            try { MeasureDrawCost(); }
            catch (System.Exception e) { Fail("Stopped by an error: " + e); }
            finally { End(); }
        }

        private class DrawCost
        {
            public long Triangles;
            public int Draws;
            public int Renderers;
            public int Static;
            public readonly HashSet<Material> Materials = new HashSet<Material>();
        }

        private static void MeasureDrawCost()
        {
            var groups = new Dictionary<string, DrawCost>();
            long allTriangles = 0;
            int allDraws = 0;

            foreach (var renderer in Object.FindObjectsOfType<Renderer>())
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;

                Mesh mesh = null;
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter != null) mesh = filter.sharedMesh;
                var skinned = renderer as SkinnedMeshRenderer;
                if (skinned != null) mesh = skinned.sharedMesh;

                long triangles = 0;
                if (mesh != null)
                    for (int i = 0; i < mesh.subMeshCount; i++) triangles += (long)mesh.GetIndexCount(i) / 3;

                string group = GroupOf(renderer.transform);
                DrawCost cost;
                if (!groups.TryGetValue(group, out cost)) groups[group] = cost = new DrawCost();
                cost.Triangles += triangles;
                cost.Draws += Mathf.Max(1, renderer.sharedMaterials.Length);
                cost.Renderers++;
                if ((GameObjectUtility.GetStaticEditorFlags(renderer.gameObject) & StaticEditorFlags.BatchingStatic) != 0) cost.Static++;
                foreach (var material in renderer.sharedMaterials) if (material != null) cost.Materials.Add(material);

                allTriangles += triangles;
                allDraws += Mathf.Max(1, renderer.sharedMaterials.Length);
            }

            Say("All active renderers: " + allTriangles.ToString("N0") + " triangles, about " + allDraws + " draws before batching");
            foreach (var pair in groups.OrderByDescending(p => p.Value.Triangles).Take(DrawCostRows))
                Say(DrawCostLine("by triangles", pair.Key, pair.Value));
            foreach (var pair in groups.OrderByDescending(p => p.Value.Draws).Take(DrawCostRows))
                Say(DrawCostLine("by draws", pair.Key, pair.Value));
        }

        /// Its top-level object, or the first level under a big container (Environment, RangeLighting) so those split up
        private static string GroupOf(Transform t)
        {
            var root = t.root;
            if (t == root) return root.name;

            var under = t;
            while (under.parent != root) under = under.parent;
            bool container = root.name == "Environment" || root.name == LightingName;
            return container ? root.name + "/" + BaseName(under.name) : root.name;
        }

        private static string DrawCostLine(string order, string group, DrawCost cost)
        {
            return order + ": " + group + ": " + cost.Triangles.ToString("N0") + " triangles, " + cost.Draws + " draws, " + cost.Renderers +
                   " renderers (" + cost.Static + " batching static), " + cost.Materials.Count + " materials";
        }
    }
}
