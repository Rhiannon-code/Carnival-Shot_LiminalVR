using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    /// Step 33: a row of bulbs along every rail that runs toward the launch point as a sign comes up (ADR 0063)
    public static partial class RangeSetup
    {
        private const string BulbShader = "CrystalCatch/Rail Bulbs";
        private const string BulbMaterialPath = "Assets/Materials/SR_RailBulbs.mat";
        private const string BulbMeshDir = "Assets/Meshes/RailBulbs";
        private const string BulbRowName = "Bulbs";
        private const float RailBulbSpacing = 0.5f;
        private const float RailBulbSize = 0.025f;
        private const float RailBulbFront = 0.15f;

        [MenuItem("Shooting Range/Setup/33 - String The Rail Bulbs", false, 56)]
        public static void RailBulbsMenu() { Begin("Rail bulbs"); StringTheRailBulbs(); End(); }

        /// Fills blanks: a rail whose Bulbs are already wired is left alone. Delete its Bulbs child and clear the
        /// field to lay that row again, after moving the rail
        private static void StringTheRailBulbs()
        {
            var material = BulbMaterial();
            if (material == null) return;

            System.IO.Directory.CreateDirectory(BulbMeshDir);

            var figure = PrefabFigure();
            if (figure == null) return;
            var posing = new Posing(figure);
            ClosePrefab(false);

            var eye = EyePoint();
            int strung = 0;

            foreach (var rail in Object.FindObjectsOfType<TrackRail>())
            {
                var fields = new Fields(rail);
                if (fields.Ref("bulbs") != null) { Say(rail.name + " already has its bulbs, left alone"); continue; }

                var row = new GameObject(BulbRowName);
                row.transform.SetParent(rail.transform, false);

                int count;
                var mesh = BulbRow(rail, row.transform, eye, posing.StandLength, posing.Gap, out count);
                AssetDatabase.CreateAsset(mesh, BulbMeshDir + "/" + rail.name.Replace(" ", "") + ".asset");

                row.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = row.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

                fields.Set("bulbs", row.AddComponent<RailBulbs>()).Apply();
                Did(rail.name + ": " + count + " bulbs");
                strung++;
            }

            if (strung > 0) Dirty();
        }

        private static Material BulbMaterial()
        {
            var material = RangeSetupUtil.Load<Material>(BulbMaterialPath);
            if (material != null) return material;

            var shader = Shader.Find(BulbShader);
            if (shader == null) { Fail(BulbShader + " has not compiled. Check the console for shader errors"); return null; }

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, BulbMaterialPath);
            Did("Made " + System.IO.Path.GetFileName(BulbMaterialPath) + ", tune the bulbs' colour and glow there");
            return material;
        }

        /// Small diamonds where the rail's signs stand: at their feet on the floor, on the rail under the roof,
        /// at the pillar's edge for a side sign. A little toward the player, so the sign never hides them
        private static Mesh BulbRow(TrackRail rail, Transform row, Vector3 eye, float standLength, float gap, out int count)
        {
            var middle = rail.PointAt(rail.Length * 0.5f);
            var front = eye - middle;
            front.y = 0f;
            front = front.sqrMagnitude > 1e-6f ? front.normalized : Vector3.back;

            Bounds mount;
            bool mounted = rail.Mounted(out mount) && rail.Hinge == Hinge.Sideways;
            var door = mounted ? PopUpTarget.DoorEdge(mount, eye, gap) : Vector3.zero;
            float lift = rail.Hinge == Hinge.Up || rail.Hinge == Hinge.None ? standLength : 0f;

            count = Mathf.Max(2, Mathf.RoundToInt(rail.Length / RailBulbSpacing) + 1);
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            for (int i = 0; i < count; i++)
            {
                float share = i / (float)(count - 1);
                var at = rail.PointAt(rail.Length * share) + Vector3.up * lift;
                if (mounted) at = new Vector3(door.x, at.y, door.z);
                at += front * RailBulbFront;

                Diamond(row.InverseTransformPoint(at), RailBulbSize, share, vertices, uvs, triangles);
            }

            var mesh = new Mesh { name = "RailBulbs " + rail.name };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void Diamond(Vector3 at, float size, float share, List<Vector3> vertices, List<Vector2> uvs, List<int> triangles)
        {
            int first = vertices.Count;
            vertices.Add(at + Vector3.up * size);
            vertices.Add(at + Vector3.down * size);
            vertices.Add(at + Vector3.right * size);
            vertices.Add(at + Vector3.forward * size);
            vertices.Add(at + Vector3.left * size);
            vertices.Add(at + Vector3.back * size);
            for (int i = 0; i < 6; i++) uvs.Add(new Vector2(share, 0f));

            for (int side = 0; side < 4; side++)
            {
                int a = first + 2 + side;
                int b = first + 2 + (side + 1) % 4;
                triangles.Add(first); triangles.Add(a); triangles.Add(b);
                triangles.Add(first + 1); triangles.Add(b); triangles.Add(a);
            }
        }
    }
}
