using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    /// Step 56: the powder kegs, waiting behind the step boards to ride up onto their tops (user, 2026-11-02)
    public static partial class RangeSetup
    {
        private const string KegsName = "Powder Kegs";
        private const string BarrelPrefab = "Assets/Modular Castle/Assets/prefabs/barrel.prefab";
        private const int KegCount = 3;
        private const float KegWide = 0.45f;
        private static readonly Color KegRed = new Color(0.62f, 0.08f, 0.05f);

        [MenuItem("Shooting Range/Setup/56 - Powder Kegs Behind The Step Boards", false, 79)]
        public static void PowderKegsMenu()
        {
            Begin("Powder kegs");
            var game = Object.FindObjectOfType<RangeGame>();
            var barrel = RangeSetupUtil.Load<GameObject>(BarrelPrefab);
            var boards = StepRenderers().Where(r => r.bounds.size.x > r.bounds.size.z).ToArray();
            if (game == null || barrel == null || boards.Length == 0) { Fail("Needs the RangeGame, " + BarrelPrefab + " and the step boards"); End(); return; }

            var old = RangeSetupUtil.FindInScene(KegsName);
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject(KegsName);
            Undo.RegisterCreatedObjectUndo(root, KegsName);
            var band = EyeMaterial("SR_KegBandBlack", "Standard", new Color(0.03f, 0.03f, 0.03f));
            var blasts = new Object[] { Sound("World/keg_blast_01"), Sound("World/keg_blast_02") };

            var kegs = new PowderKeg[KegCount];
            for (int i = 0; i < KegCount; i++)
                kegs[i] = MakeAKeg(root.transform, "Powder Keg " + (i + 1), barrel, band, blasts);

            new Fields(root.AddComponent<PowderKegs>()).Set("game", game).SetArray("kegs", kegs).SetArray("boards", boards).Apply();
            Did(KegCount + " kegs on " + boards.Length + " step boards, 2-3 up a round");
            Dirty();
            End();
        }

        private static PowderKeg MakeAKeg(Transform parent, string name, GameObject barrel, Material band, Object[] blasts)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);

            var keg = new GameObject("Keg").transform;
            keg.SetParent(root.transform, false);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(barrel);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.transform.SetParent(keg, false);
            foreach (var collider in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);

            Bounds size;
            RangeSetupUtil.MeshBoundsIn(keg, model.transform, out size);
            float scale = KegWide / Mathf.Max(0.01f, Mathf.Max(size.size.x, size.size.z));
            model.transform.localScale *= scale;
            RangeSetupUtil.MeshBoundsIn(keg, model.transform, out size);
            model.transform.localPosition -= new Vector3(size.center.x, size.min.y, size.center.z);

            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials = renderer.sharedMaterials.Select(KegMaterial).ToArray();

            foreach (float height in new[] { 0.25f, 0.75f })
            {
                var ring = Part(PrimitiveType.Cylinder, "Band", keg, band);
                ring.localPosition = Vector3.up * size.size.y * height;
                ring.localScale = new Vector3(KegWide * 1.03f, 0.02f, KegWide * 1.03f);
            }

            var box = keg.gameObject.AddComponent<CapsuleCollider>();
            box.center = Vector3.up * size.size.y * 0.5f;
            box.radius = KegWide * 0.5f;
            box.height = size.size.y;

            // Waits out of sight until a round puts it up; left showing, it sits at the room's origin in the Scene view
            keg.gameObject.SetActive(false);
            new Fields(root.AddComponent<PowderKeg>()).Set("keg", keg).Set("hitBox", box).Set("blastFx", KegBlast(root.transform))
                .SetArray("blastClips", blasts).Set("liftClip", Sound("World/keg_lift_01")).Set("settleClip", Sound("World/keg_hatch_01"))
                .Apply();
            return root.GetComponent<PowderKeg>();
        }

        private static Transform Part(PrimitiveType type, string name, Transform parent, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part.transform;
        }

        /// The barrel's own material painted red, as SR_PowderKegRed, made once and then left to tune
        private static Material KegMaterial(Material from)
        {
            const string path = "Assets/Materials/SR_PowderKegRed.mat";
            var material = RangeSetupUtil.Load<Material>(path);
            if (material != null || from == null) return material != null ? material : from;

            material = new Material(from) { color = KegRed };
            AssetDatabase.CreateAsset(material, path);
            Did("Made SR_PowderKegRed.mat from " + from.name);
            return material;
        }
    }
}
