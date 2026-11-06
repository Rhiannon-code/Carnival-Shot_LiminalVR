using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    /// Step 62: the chest on the bench becomes the prize chest (user, 2026-11-02). Added to the Room_Chest prefab; its own
    /// lid and gags are left as step 28 made them. Re-running replaces only what this step added, nothing else
    public static partial class RangeSetup
    {
        private const string PrizeLockName = "Prize Lock";
        private const string PrizeGlowName = "Prize Lock Glow";
        private const string PrizesName = "Prizes";

        [MenuItem("Shooting Range/Setup/62 - Prize Chest", false, 85)]
        public static void PrizeChestMenu()
        {
            Begin("Prize chest");
            string path = RoomTargetDir + "/Room_Chest.prefab";
            if (RangeSetupUtil.Load<GameObject>(path) == null) { Fail("Room_Chest is not built yet. Step 28 builds it"); End(); return; }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var lid = root.GetComponentInChildren<PopOpenOnShot>();
                var hinge = lid != null ? new Fields(lid).Ref("hinge") as Transform : null;
                if (hinge == null) { Fail("Room_Chest has no Pop Open On Shot with a hinge"); return; }

                foreach (var name in new[] { PrizeLockName, PrizeGlowName, PrizesName })
                {
                    var old = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
                    if (old != null) Object.DestroyImmediate(old.gameObject);
                }
                var oldChest = root.GetComponent<PrizeChest>();
                if (oldChest != null) Object.DestroyImmediate(oldChest);

                var box = BoundsOf(root.GetComponentsInChildren<Renderer>()
                                      .Where(r => !r.transform.GetComponentsInParent<Transform>(true).Any(t => t.name == "Gags")).ToArray());
                var front = Vector3.ProjectOnPlane(box.center - hinge.position, Vector3.up).normalized;
                float reach = Mathf.Abs(Vector3.Dot(box.extents, new Vector3(Mathf.Abs(front.x), 0f, Mathf.Abs(front.z))));

                var lockPart = PrizePart(PrimitiveType.Cube, PrizeLockName, root.transform, EyeMaterial("SR_PrizeLock", "Standard", Brass));
                lockPart.position = box.center + front * (reach + 0.015f) + Vector3.up * (hinge.position.y - 0.04f - box.center.y);
                lockPart.rotation = Quaternion.LookRotation(front);
                lockPart.localScale = new Vector3(0.07f, 0.08f, 0.03f);
                var hitBox = lockPart.gameObject.AddComponent<BoxCollider>();
                hitBox.size = new Vector3(2f, 1.8f, 3f);

                var glowMaterial = EyeMaterial("SR_PrizeLockGlow", "Legacy Shaders/Particles/Additive", Gold);
                if (glowMaterial != null) glowMaterial.SetColor("_TintColor", Gold * 0.5f);
                var glow = PrizePart(PrimitiveType.Sphere, PrizeGlowName, root.transform, glowMaterial);
                glow.position = lockPart.position;
                glow.localScale = Vector3.one * 0.2f;

                var prizes = MakeThePrizes(lid.transform, box);

                var chest = root.AddComponent<PrizeChest>();
                new Fields(chest).Set("lid", lid).Set("lockPart", lockPart).Set("lockGlow", glow).SetArray("prizes", prizes)
                    .Set("unlockClip", Sound("World/prize_unlock_01"))
                    .SetArray("fanfares", new Object[] { Sound("World/prize_toot_01"), Sound("World/prize_fanfare_01"),
                                                         Sound("World/prize_fanfare_01"), Sound("World/prize_fanfare_top_01") })
                    .Apply();
                new Fields(lockPart.gameObject.AddComponent<PrizeLock>()).Set("chest", chest).Apply();

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Did("Room_Chest: locked through the game, a brass lock on its front to shoot at the end, " + prizes.Count +
                    " prizes inside (skull, plush bat, big plush bat, golden plush bat)");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            End();
        }

        /// Smallest to best, each a stripped copy of a room prop, sized to the box and hidden inside it
        private static List<Object> MakeThePrizes(Transform parent, Bounds box)
        {
            var holder = new GameObject(PrizesName).transform;
            holder.SetParent(parent, false);
            float room = Mathf.Min(box.size.x, box.size.y, box.size.z) * GagShareOfChest;

            var gold = EyeMaterial("SR_PrizeGold", "Standard", Gold);
            if (gold != null)
            {
                gold.SetFloat("_Metallic", 0.8f);
                gold.SetFloat("_Glossiness", 0.6f);
                gold.EnableKeyword("_EMISSION");
                gold.SetColor("_EmissionColor", new Color(0.5f, 0.35f, 0.05f));
            }

            var prizes = new List<Object>();
            var picks = new[] { ("Room_Skull", 0.6f, (Material)null), ("Room_PlushieBat", 0.85f, null), ("Room_PlushieBat", 1f, null), ("Room_PlushieBat", 1f, gold) };
            for (int i = 0; i < picks.Length; i++)
            {
                var source = RangeSetupUtil.Load<GameObject>(RoomTargetDir + "/" + picks[i].Item1 + ".prefab");
                if (source == null) { Fail("No " + picks[i].Item1 + ".prefab for prize " + (i + 1)); continue; }

                var prize = Object.Instantiate(source, holder);
                prize.name = "Prize " + (i + 1) + " " + picks[i].Item1.Replace("Room_", "");
                foreach (var part in prize.GetComponentsInChildren<Component>(true))
                {
                    if (part is Transform || part is MeshFilter || part is MeshRenderer || part is SkinnedMeshRenderer) continue;
                    Object.DestroyImmediate(part);
                }
                if (picks[i].Item3 != null)
                    foreach (var renderer in prize.GetComponentsInChildren<Renderer>(true))
                        renderer.sharedMaterials = renderer.sharedMaterials.Select(m => picks[i].Item3).ToArray();

                var size = BoundsOf(prize.GetComponentsInChildren<Renderer>()).size;
                prize.transform.localScale *= room * picks[i].Item2 / Mathf.Max(0.001f, Mathf.Max(size.x, size.y, size.z));
                var now = BoundsOf(prize.GetComponentsInChildren<Renderer>());
                prize.transform.position += new Vector3(box.center.x - now.center.x, box.min.y - now.min.y, box.center.z - now.center.z);
                prize.SetActive(false);
                prizes.Add(prize.transform);
            }

            return prizes;
        }

        private static Transform PrizePart(PrimitiveType type, string name, Transform parent, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part.transform;
        }
    }
}
