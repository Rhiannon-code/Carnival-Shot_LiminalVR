using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    /// Step 53 measures, step 54 sets the evening pass's values: the shorter start-up, the reload with no spin, the new
    /// sounds and the single blaze (plans/build-2026-11-02-evening.md)
    public static partial class RangeSetup
    {
        private const string SoundDir = "Assets/Sounds/ShootingRange/";
        private const float FoldSweepStep = 10f;

        [MenuItem("Shooting Range/Setup/53 - Report Rails, Boards And Lightmap (changes nothing)", false, 76)]
        public static void ReportRailsAndLightmapMenu()
        {
            Begin("Rails, boards and lightmap (a report, nothing is changed)");
            ReportTheFolds();
            ReportTheLightmap();
            End();
        }

        /// Each floor rail's sign standing, folding and folded, against every step board. A fold swings away from the
        /// player about the hinge, so it is sampled as a quarter circle
        private static void ReportTheFolds()
        {
            var boards = StepRenderers();
            var prefab = RangeSetupUtil.Load<GameObject>(PrefabPath);
            var target = prefab != null ? prefab.GetComponentInChildren<PopUpTarget>(true) : null;
            if (target == null || boards.Count == 0) { Fail("No step boards, or no PopUpTarget on " + PrefabPath); return; }

            var standModel = new Fields(target).Ref("stand") as Transform;
            float standLength = standModel != null ? PopUpTarget.LengthOf(standModel) : 0f;
            var heights = SignHeights();
            float tallest = heights.Count > 0 ? heights.Values.Max() : 0f;
            Say("Stand " + standLength.ToString("0.00") + " m, tallest sign " + tallest.ToString("0.00") + " m, eye " + EyePoint().ToString("0.00"));

            foreach (var board in boards)
                Say("   board " + board.transform.root.name + "  x " + Range(board.bounds.min.x, board.bounds.max.x) + "  y " +
                    Range(board.bounds.min.y, board.bounds.max.y) + "  z " + Range(board.bounds.min.z, board.bounds.max.z));

            foreach (var rail in Object.FindObjectsOfType<TrackRail>().OrderBy(r => r.name))
            {
                if (rail.Hinge != Hinge.Up) continue;

                float sign = rail.TallestSign > 0f ? rail.TallestSign : tallest;
                Vector3 start = rail.Origin, end = rail.PointAt(rail.Length);
                float hingeY = start.y + rail.ShownOffset + standLength;
                float z = start.z;

                var hits = new List<string>();
                for (float angle = 0f; angle <= 90f; angle += FoldSweepStep)
                {
                    foreach (var board in boards)
                    {
                        if (!FoldTouches(board.bounds, Mathf.Min(start.x, end.x), Mathf.Max(start.x, end.x), hingeY, z, sign, angle)) continue;
                        hits.Add(angle.ToString("0") + "° " + board.transform.root.name);
                    }
                }

                string line = rail.name + ": hinge y " + hingeY.ToString("0.00") + " z " + z.ToString("0.00") + ", sign " + sign.ToString("0.00") +
                              " m, folds to z " + (z + sign).ToString("0.00");
                if (hits.Count == 0) Did(line + ". Clear of every board, up, folding and down");
                else Fail(line + ". Passes through: " + string.Join(", ", hits.Distinct().ToArray()));

                RaycastHit above;
                var middle = rail.PointAt(rail.Length * 0.5f);
                if (Physics.Raycast(new Vector3(middle.x, hingeY, middle.z), Vector3.up, out above, 5f, ~0, QueryTriggerInteraction.Ignore))
                    Say("   above its middle: " + PathOf(above.collider.transform) + " at y " + above.point.y.ToString("0.00"));
            }
        }

        /// The sign as a line from the hinge, angle 0 standing and 90 lying away from the player, tested at points along it
        private static bool FoldTouches(Bounds board, float fromX, float toX, float hingeY, float z, float length, float angle)
        {
            if (toX < board.min.x || fromX > board.max.x) return false;

            float radians = angle * Mathf.Deg2Rad;
            for (float along = 0.05f; along <= length; along += 0.05f)
            {
                float y = hingeY + Mathf.Cos(radians) * along;
                float zAt = z + Mathf.Sin(radians) * along;
                if (y > board.min.y && y < board.max.y && zAt > board.min.z && zAt < board.max.z) return true;
            }

            return false;
        }

        /// Who takes the most lightmap. A share is of one atlas; the flat areas in the bake are matched against these
        private static void ReportTheLightmap()
        {
            var users = Object.FindObjectsOfType<MeshRenderer>()
                              .Where(r => r.lightmapIndex >= 0 && r.lightmapIndex < 0xFFFE)
                              .OrderByDescending(r => r.lightmapScaleOffset.x * r.lightmapScaleOffset.y)
                              .ToList();

            Say(users.Count + " renderers in the lightmap. The biggest:");
            foreach (var r in users.Take(30))
            {
                var so = r.lightmapScaleOffset;
                Say("   " + (so.x * so.y * 100f).ToString("0.0") + "%  " + PathOf(r.transform) + "  map " + r.lightmapIndex + " st " +
                    so.ToString("0.0000") + "  scale " + new SerializedObject(r).FindProperty("m_ScaleInLightmap").floatValue.ToString("0.##") +
                    "  size " + r.bounds.size.ToString("0.0"));
            }
        }

        private static string Range(float from, float to) { return from.ToString("0.00") + ".." + to.ToString("0.00"); }

        private static string PathOf(Transform t)
        {
            string path = t.name;
            for (var p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
            return path;
        }

        [MenuItem("Shooting Range/Setup/54 - Shorter Start-Up, Reload, New Sounds", false, 77)]
        public static void EveningPassMenu()
        {
            Begin("Shorter start-up, reload with no spin, new sounds");

            var lights = Object.FindObjectOfType<CarnivalLights>();
            if (lights != null)
            {
                new Fields(lights).Set("stringComingOn", new[] { 0.06f, 0.09f, 0.04f, 0.12f }).Set("stringGap", 0.09f)
                                  .Set("coinClip", Sound("World/coin_slot_01")).Set("litHum", Sound("World/lights_lit_hum_loop"))
                                  .SetArray("buzzClips", new Object[] { Sound("World/lights_startup_buzz_02") })
                                  .Apply();
                Did("Carnival Lights: the strings come on in about 4 s, the coin, and a low rotor hum that stays while they're lit");
            }
            else Fail("No Carnival Lights in the scene");

            var music = Object.FindObjectOfType<MusicBed>();
            if (music != null)
            {
                new Fields(music).Set("windUpSeconds", 4f).Set("windUpSound", Sound("World/windup_crank_01")).Apply();
                Did("Music winds up in 4 s, from the coin, over a crank turning faster");
            }

            var audio = Object.FindObjectOfType<RangeAudio>();
            if (audio != null) { new Fields(audio).Set("hitTickClip", Sound("World/hit_tick_01")).Apply(); Did("Hit tick wired"); }
            else Fail("No Range Audio in the scene, so no hit tick");

            foreach (var pistol in Object.FindObjectsOfType<Pistol>())
            {
                new Fields(pistol).Set("reloadSeconds", 1.3f)
                                  .Set("magazineFalls", new Vector2(0.04f, 0.3f)).Set("magazineRises", new Vector2(0.4f, 0.6f))
                                  .Set("magazineOutClip", Sound("Gun/mag_out_01")).Set("reloadClip", Sound("Gun/mag_in_01"))
                                  .Set("slideReleaseClip", Sound("Gun/slide_01"))
                                  .Apply();
                Did(pistol.name + ": 1.3 s reload, magazine out, in, slide");
            }

            var impacts = Object.FindObjectOfType<ImpactFX>();
            if (impacts != null) { new Fields(impacts).Set("impactVolume", 0.35f).Apply(); Did("Room hits at 0.35, no closer together than 0.06 s"); }

            Dirty();

            WirePrefab(RoundPrefab, root =>
            {
                var round = root.GetComponent<Projectile>();
                if (round == null) return 0;
                new Fields(round).Set("ricochetVolume", 0.3f).Apply();
                return 1;
            });

            WirePrefab(PrefabPath, root =>
            {
                var target = root.GetComponentInChildren<PopUpTarget>(true);
                if (target == null) return 0;
                new Fields(target).Set("flameEvery", 0.1f).Apply();
                return 1;
            });
            End();
        }

        private static AudioClip Sound(string name)
        {
            var clip = RangeSetupUtil.Load<AudioClip>(SoundDir + name + ".wav");
            if (clip == null) Fail("No " + SoundDir + name + ".wav");
            return clip;
        }

        private const string SideRailPrefix = "SideWall_Bats_";

        /// The bonus bats came out (user, 2026-11-02: "nothing makes it noticeable"): their two rails go with them
        [MenuItem("Shooting Range/Setup/61 - Remove The Side Bat Rails", false, 84)]
        public static void RemoveSideBatRailsMenu()
        {
            Begin("Remove the side bat rails");
            var grid = Object.FindObjectOfType<TrackGrid>();
            foreach (var side in new[] { "L", "R" })
            {
                var rail = RangeSetupUtil.FindInScene(SideRailPrefix + side);
                if (rail == null) { Say(SideRailPrefix + side + " is already gone"); continue; }

                if (grid != null) new Fields(grid).SetArray("rails", grid.Rails.Where(r => r != null && r.gameObject != rail).Cast<Object>().ToList()).Apply();
                Undo.DestroyObjectImmediate(rail);
                Did("Deleted " + SideRailPrefix + side + " and took it off the track grid");
            }

            Dirty();
            End();
        }

        private static readonly string[] UnseenPlanes = { "Plane", "Plane (1)", "Plane (2)", "Plane (3)" };
        private const int SharperDirectSamples = 128;
        private const int SharperShadowBlur = 1;

        /// Splotchy shadows (audit 2026-11-02, step 53): four planes nobody sees took 8% of the one lightmap and baked to one
        /// flat colour, and step 50's blur smeared the rest. Taken out of the bake, not deleted; more samples so less blur is needed
        [MenuItem("Shooting Range/Setup/58 - Sharper Lightmap", false, 81)]
        public static void SharperLightmapMenu()
        {
            Begin("Sharper lightmap");
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (!UnseenPlanes.Contains(root.name)) continue;

                var flags = GameObjectUtility.GetStaticEditorFlags(root);
                if ((flags & StaticEditorFlags.LightmapStatic) == 0) { Say(root.name + " is already out of the bake"); continue; }

                GameObjectUtility.SetStaticEditorFlags(root, flags & ~StaticEditorFlags.LightmapStatic);
                Did(root.name + " at " + root.transform.position.ToString("0.0") + ": out of the lightmap (Contribute GI off), still drawn");
            }

            LightmapEditorSettings.directSampleCount = SharperDirectSamples;
            LightmapEditorSettings.filteringGaussRadiusDirect = SharperShadowBlur;
            Did("Direct samples " + SharperDirectSamples + ", shadow blur " + SharperShadowBlur + " texel. Bakes take about twice as long");
            Dirty();
            End();
        }

        private static readonly string[] RowB = { "step (7)", "step (8)", "step (9)", "step (16)", "step (17)", "step (18)" };
        private static readonly string[] RowC = { "step (10)", "step (11)", "step (12)", "step (13)", "step (14)", "step (15)" };
        private static readonly string[] RideWithRowB = { "Red Skull", "Green Skull", "Orange Skull", "pillar (6)", "pillar (7)" };
        private static readonly string[] RideWithRowC = { "pillar (3)", "pillar (5)" };
        private static readonly string[,] TierPairs =
        {
            { "Floor_Across_0", "Floor_Across_0 (1)", "" },
            { "Floor_Across_1", "Floor_Across_1 (1)", "Skylight Floor_Across_1" },
            { "Floor_Across_2", "Floor_Across_2 (1)", "Skylight Floor_Across_2" },
        };
        private const string Backdrop = "backdrop";
        private const float BehindBoard = 0.06f;
        private const float FoldGap = 0.05f;

        /// Each gap between board rows holds a pair of rails, and every sign must fold back clear of the next board and of
        /// the other rail's signs (user, 2026-11-02, from step 53). So the rows behind move back, taking what sits on them
        [MenuItem("Shooting Range/Setup/59 - Move The Step Rows Back So Signs Fold Clear", false, 82)]
        public static void MoveTheRowsMenu()
        {
            if (!EditorUtility.DisplayDialog("Move the step rows back",
                    "This moves board rows B and C back, with the countdown skulls and pillars 3, 5, 6 and 7, the backdrop, the six " +
                    "floor rails and the two skylights, then refits the rails' heights.\n\nIt DELETES and re-lays RangeLighting/Underglow " +
                    "(the red lights and glow strips) and RangeLighting/Probes from the moved rails.\n\nThe moves can be undone; the re-laid " +
                    "lights are generated, so they can't, but re-running this lays them again. The full bake (step 39) is needed after.", "Move them", "Cancel"))
                return;

            Begin("Move the step rows back");
            float fold = SignHeights().Values.DefaultIfEmpty(0f).Max() + FoldGap;
            var rowA = StepRenderers().Where(r => r.bounds.size.x > r.bounds.size.z && !RowB.Contains(r.transform.root.name) &&
                                                  !RowC.Contains(r.transform.root.name)).ToList();
            var rowB = Roots(RowB);
            var rowC = Roots(RowC);
            if (rowA.Count == 0 || rowB.Count != RowB.Length || rowC.Count != RowC.Length || fold <= FoldGap) { Fail("Missing boards or signs"); End(); return; }

            float pairA = rowA.Max(r => r.bounds.max.z) + BehindBoard;
            float shiftB = Mathf.Max(0f, pairA + 2f * fold - Front(rowB));
            Shift(rowB.Concat(Roots(RideWithRowB)), shiftB);

            float pairB = Back(rowB) + BehindBoard;
            float shiftC = Mathf.Max(0f, pairB + 2f * fold - Front(rowC));
            Shift(rowC.Concat(Roots(RideWithRowC)), shiftC);

            float pairC = Back(rowC) + BehindBoard;
            var backdrop = Roots(new[] { Backdrop });
            if (backdrop.Count == 1) Shift(backdrop, Mathf.Max(0f, pairC + 2f * fold - Front(backdrop)));
            else Fail("No " + Backdrop + ", so nothing stops the back pair folding into what's behind");

            var fronts = new[] { pairA, pairB, pairC };
            for (int tier = 0; tier < fronts.Length; tier++)
            {
                SetRailZ(TierPairs[tier, 0], fronts[tier]);
                SetRailZ(TierPairs[tier, 1], fronts[tier] + fold);
                if (TierPairs[tier, 2].Length > 0) SetRailZ(TierPairs[tier, 2], fronts[tier] + fold * 0.5f);
            }

            // Moved colliders only reach the physics scene at the next simulation step, and the fit below raycasts
            Physics.SyncTransforms();
            FitTheRailsToTheSteps(true);
            RelayTheRedAndTheProbes();
            Say("Run step 53 to check every fold, then the full bake (step 39)");
            Dirty();
            End();
        }

        private static List<Renderer> Roots(IEnumerable<string> names)
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            return scene.GetRootGameObjects().Where(g => names.Contains(g.name))
                        .Select(g => g.GetComponentInChildren<Renderer>()).Where(r => r != null).ToList();
        }

        private static float Front(List<Renderer> row) { return row.Min(r => r.bounds.min.z); }
        private static float Back(List<Renderer> row) { return row.Max(r => r.bounds.max.z); }

        private static void Shift(IEnumerable<Renderer> parts, float by)
        {
            if (by <= 0.001f) return;

            foreach (var part in parts)
            {
                var root = part.transform.root;
                Undo.RecordObject(root, "Move the step rows back");
                float from = root.position.z;
                root.position += Vector3.forward * by;
                Did(root.name + ": z " + from.ToString("0.00") + " -> " + root.position.z.ToString("0.00"));
            }
        }

        private static void SetRailZ(string name, float z)
        {
            var found = RangeSetupUtil.FindInScene(name);
            if (found == null) { Fail("No " + name); return; }

            Undo.RecordObject(found.transform, "Move the step rows back");
            var position = found.transform.position;
            Did(name + ": z " + position.z.ToString("0.00") + " -> " + z.ToString("0.00"));
            position.z = z;
            found.transform.position = position;
        }
    }
}
