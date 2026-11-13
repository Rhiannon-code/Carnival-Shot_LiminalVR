using System.Collections.Generic;
using System.Linq;
using TMPro;
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
            BuildThePrizeChest();
            End();
        }

        /// Worst to best (user, 2026-11-10). The chest hides a stripped copy of each, and the prize shelf (step 69) shows the
        /// shootable ones
        private static readonly string[] PrizeOrder =
        {
            "Room_PlushieBat", "Room_Prize_Ghost", "Room_Prize_Mummy", "Room_Prize_Bride", "Room_Prize_Skeleton", "Room_Prize_Vamp",
        };

        private static void BuildThePrizeChest()
        {
            string path = RoomTargetDir + "/Room_Chest.prefab";
            if (RangeSetupUtil.Load<GameObject>(path) == null) { Fail("Room_Chest is not built yet. Step 28 builds it"); return; }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var lid = root.GetComponentInChildren<PopOpenOnShot>();
                var hinge = lid != null ? new Fields(lid).Ref("hinge") as Transform : null;
                if (hinge == null) { Fail("Room_Chest has no Pop Open On Shot with a hinge"); return; }

                foreach (var name in new[] { PrizeLockName, PrizeGlowName, PrizesName, ConfettiName, SparkleName, LockPromptName })
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
                // Twice the first lock: it was hard to find (user, 2026-11-10)
                lockPart.localScale = new Vector3(0.14f, 0.16f, 0.06f);
                var hitBox = lockPart.gameObject.AddComponent<BoxCollider>();
                hitBox.size = new Vector3(2f, 1.8f, 3f);

                var glowMaterial = EyeMaterial("SR_PrizeLockGlow", "Legacy Shaders/Particles/Additive", Gold);
                if (glowMaterial != null) glowMaterial.SetColor("_TintColor", Gold * 0.5f);
                var glow = PrizePart(PrimitiveType.Sphere, PrizeGlowName, root.transform, glowMaterial);
                glow.position = lockPart.position;
                glow.localScale = Vector3.one * 0.4f;
                var prompt = LockPrompt(root.transform, lockPart.position, front);

                var prizes = MakeThePrizes(lid.transform, box);
                var confetti = Confetti(root.transform, new Vector3(box.center.x, box.max.y, box.center.z));
                var sparkle = Sparkle(root.transform, box.center);

                var chest = root.AddComponent<PrizeChest>();
                new Fields(chest).Set("lid", lid).Set("lockPart", lockPart).Set("lockGlow", glow).SetArray("prizes", prizes)
                    .Set("confetti", confetti).Set("sparkle", sparkle).Set("prompt", prompt)
                    // The prize's bottom clears the rim, so all of it is seen (user, 2026-11-10: it didn't rise enough)
                    .Set("riseHeight", box.size.y + PrizeClearsTheRim)
                    .Set("unlockClip", Sound("World/prize_unlock_01"))
                    .Set("drumRoll", Sound("World/prize_drumroll_01"))
                    .Set("drumRollEnd", Sound("World/prize_drumroll_end_01"))
                    .SetArray("fanfares", new Object[] { Sound("World/prize_fanfare_small_01"), Sound("World/prize_fanfare_01"),
                                                         Sound("World/prize_fanfare_01"), Sound("World/prize_fanfare_top_01") })
                    .Apply();
                new Fields(lockPart.gameObject.AddComponent<PrizeLock>()).Set("chest", chest).Apply();

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Did("Room_Chest: locked through the game, a brass lock on its front to shoot at the end, " + prizes.Count +
                    " prizes inside, worst to best: " + string.Join(", ", PrizeOrder));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /// Worst to best, each a stripped copy of its prize prefab, all the same size in the box and hidden inside it
        private static List<Object> MakeThePrizes(Transform parent, Bounds box)
        {
            var holder = new GameObject(PrizesName).transform;
            holder.SetParent(parent, false);
            float room = Mathf.Min(box.size.x, box.size.y, box.size.z) * GagShareOfChest;

            var prizes = new List<Object>();
            for (int i = 0; i < PrizeOrder.Length; i++)
            {
                var source = RangeSetupUtil.Load<GameObject>(RoomTargetDir + "/" + PrizeOrder[i] + ".prefab");
                if (source == null) { Fail("No " + PrizeOrder[i] + ".prefab for prize " + (i + 1) + ". Step 69 builds the prizes"); continue; }

                var prize = Object.Instantiate(source, holder);
                prize.name = "Prize " + (i + 1) + " " + PrizeOrder[i].Replace("Room_Prize_", "").Replace("Room_", "");
                foreach (var part in prize.GetComponentsInChildren<Component>(true))
                {
                    if (part is Transform || part is MeshFilter || part is MeshRenderer || part is SkinnedMeshRenderer) continue;
                    Object.DestroyImmediate(part);
                }

                var size = BoundsOf(prize.GetComponentsInChildren<Renderer>()).size;
                prize.transform.localScale *= room / Mathf.Max(0.001f, Mathf.Max(size.x, size.y, size.z));
                var now = BoundsOf(prize.GetComponentsInChildren<Renderer>());
                prize.transform.position += new Vector3(box.center.x - now.center.x, box.min.y - now.min.y, box.center.z - now.center.z);
                prize.SetActive(false);
                prizes.Add(prize.transform);
            }

            return prizes;
        }

        private const string LockPromptName = "Lock Prompt";
        private const string LockArrowArt = "Assets/Textures/Prize/lock_arrow.png";
        private const float PromptAboveLock = 0.32f;

        /// "SHOOT THE LOCK" in the controls card's lettering with a painted arrow under it, over the lock and facing out of
        /// the chest's front. PrizeChest shows it while the lock waits and bobs it
        private static Transform LockPrompt(Transform parent, Vector3 lockAt, Vector3 front)
        {
            var holder = new GameObject(LockPromptName).transform;
            holder.SetParent(parent, false);
            holder.position = lockAt + front * 0.05f + Vector3.up * PromptAboveLock;
            // TextMesh Pro reads from behind its +Z, so +Z points away from whoever faces the chest's front
            holder.rotation = Quaternion.LookRotation(-front, Vector3.up);

            var font = CardFont();
            var text = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshPro>();
            text.transform.SetParent(holder, false);
            text.rectTransform.sizeDelta = new Vector2(0.7f, 0.16f);
            text.rectTransform.localPosition = new Vector3(0f, 0.1f, 0f);
            if (font != null) { text.font = font; text.fontSharedMaterial = font.material; }
            text.enableAutoSizing = true;
            text.fontSizeMin = 0.1f;
            text.fontSizeMax = 2f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(1f, 0.85f, 0.47f);
            text.text = "SHOOT THE LOCK";

            var importer = AssetImporter.GetAtPath(LockArrowArt) as TextureImporter;
            if (importer != null && (!importer.alphaIsTransparency || importer.wrapMode != TextureWrapMode.Clamp))
            {
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            var arrowMaterial = EyeMaterial("SR_LockArrow", "Unlit/Transparent", Color.white);
            if (arrowMaterial != null && arrowMaterial.mainTexture == null)
            {
                arrowMaterial.mainTexture = RangeSetupUtil.Load<Texture2D>(LockArrowArt);
                EditorUtility.SetDirty(arrowMaterial);
            }
            if (arrowMaterial == null || arrowMaterial.mainTexture == null) Fail("No " + LockArrowArt + ". Run tools/prize-lock-arrow.py");

            var arrow = GameObject.CreatePrimitive(PrimitiveType.Quad);
            arrow.name = "Arrow";
            Object.DestroyImmediate(arrow.GetComponent<Collider>());
            arrow.transform.SetParent(holder, false);
            arrow.transform.localPosition = new Vector3(0f, -0.08f, 0f);
            arrow.transform.localScale = Vector3.one * 0.14f;
            arrow.GetComponent<Renderer>().sharedMaterial = arrowMaterial;
            return holder;
        }

        private const float PrizeClearsTheRim = 0.08f;
        private const string ConfettiName = "Prize Confetti";
        private const string SparkleName = "Prize Sparkle";
        private static readonly Color[] ConfettiColours =
        {
            new Color(0.72f, 0.14f, 0.18f), new Color(0.96f, 0.92f, 0.83f), new Color(0.91f, 0.73f, 0.29f),
            new Color(0.3f, 0.75f, 0.35f), new Color(0.6f, 0.35f, 0.85f),
        };

        /// A burst of paper out of the open chest, fluttering down. Tune it on the chest prefab
        private static ParticleSystem Confetti(Transform parent, Vector3 at)
        {
            var system = NewParticles(ConfettiName, parent, at, Quaternion.Euler(-90f, 0f, 0f));
            var main = system.main;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 3.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.04f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0.5f;
            main.maxParticles = 200;
            var colours = new Gradient();
            var keys = new GradientColorKey[ConfettiColours.Length];
            for (int i = 0; i < keys.Length; i++) keys[i] = new GradientColorKey(ConfettiColours[i], i / (keys.Length - 1f));
            colours.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            main.startColor = new ParticleSystem.MinMaxGradient(colours) { mode = ParticleSystemGradientMode.RandomColor };

            var emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 140) });

            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 30f;
            shape.radius = 0.15f;

            var spin = system.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-6f, 6f);

            // Paper drifts rather than falling like stones
            var air = system.limitVelocityOverLifetime;
            air.enabled = true;
            air.limit = 1.5f;
            air.dampen = 0.15f;

            system.GetComponent<ParticleSystemRenderer>().sharedMaterial = EyeMaterial("SR_PrizeConfetti", "Mobile/Particles/Alpha Blended", Color.white);
            return system;
        }

        /// Gold glints round the prize as it rises; PrizeChest moves it with the prize
        private static ParticleSystem Sparkle(Transform parent, Vector3 at)
        {
            var system = NewParticles(SparkleName, parent, at, Quaternion.identity);
            var main = system.main;
            main.duration = 3f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.4f), Color.white);
            main.maxParticles = 120;

            var emission = system.emission;
            emission.rateOverTime = 35f;

            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;

            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var material = EyeMaterial("SR_PrizeSparkle", "Mobile/Particles/Additive", Color.white);
            if (material != null && material.mainTexture == null)
            {
                material.mainTexture = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
                EditorUtility.SetDirty(material);
            }
            system.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            return system;
        }

        private static ParticleSystem NewParticles(string name, Transform parent, Vector3 at, Quaternion turn)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(parent, false);
            holder.transform.position = at;
            holder.transform.rotation = turn;

            var system = holder.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            return system;
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
