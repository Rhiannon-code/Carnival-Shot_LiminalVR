using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Liminal.SDK.VR.Avatars;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    /// Wires up what the setup guide asks for, in the scene that is already open. It only ever adds
    /// what is missing and fills in fields still at their defaults, so anything hand-tuned survives.
    /// Nothing here generates a scene, and nothing here deletes your work
    public static class RangeSetup
    {
        private const string PrefabPath = "Assets/Prefabs/ShootingRange/TrackMover.prefab";
        private const string GhostMaterial = "Assets/GhostlyHand/Built-In Render Pipeline/EXAMPLE HAND1.mat";
        private const string LeftGlove = "Assets/Hands/vr_glove_left_model_slim.fbx";
        private const string RightGlove = "Assets/Hands/vr_glove_right_model_slim.fbx";

        private const float EyeHeight = 1.7f;
        private const float SignTarget = 0.7f;
        private const float SignPeek = 0.12f;
        private const float NearestCover = 0.5f;

        private const float CrossMinSpeed = 0.7f;
        private const float CrossMaxSpeed = 1.3f;
        private const float RiseDelay = 0.15f;
        private const float RiseSeconds = 0.35f;
        private const float FallSeconds = 0.3f;
        private const float CoverThickness = 0.22f;
        private const int Samples = 8;
        private const float RiserFromRound = 2.5f;
        private const float EffectSizeMin = 0.10f;
        private const float EffectSizeMax = 0.22f;

        private const int MilestoneChain = 12;
        private const float ComboWindow = 2f;
        private const float SecondsPerHit = 0.25f;
        private const float MaxLengthMultiple = 1.5f;
        private const int MinTargetsForSlow = 5;

        // If the fingers splay instead of curling, this is the axis to change
        private static readonly Vector3 CurlAxis = Vector3.right;
        private const float FingerCurl = 55f;
        private const float ThumbCurl = 35f;

        private const string ShardSourceDir = "Assets/Models/Shards/FBX";
        private const string ShardPrefabDir = "Assets/Prefabs/ShootingRange/Shards";
        private const float ShardMass = 0.2f;

        private static readonly Vector3 WallPosition = new Vector3(0f, 0.65f, 5.2f);
        private static readonly Vector3 WallScale = new Vector3(9.2f, 1.3f, 0.2f);

        private static StringBuilder _report;

        [MenuItem("Shooting Range/Setup/Run Everything", false, 0)]
        public static void RunEverything()
        {
            Begin("Run everything");
            SizeTheSigns();
            SkinTheSigns();
            FixTheTargetPrefab();
            StraightenTheSigns();
            AddTheGalleryWall();
            BuildRailCover();
            SetUpTheRails();
            RaiseTheChargers();
            GunsAndGhostHands();
            PairTheTiers();
            SetThePace();
            TrimTheHitEffects();
            TuneThePowerUps();
            PoseTheGloves();
            BuildTheShards();
            End();
        }

        [MenuItem("Shooting Range/Setup/2 - Size The Signs", false, 19)]
        public static void SizeTheSignsMenu() { Begin("Size the signs"); SizeTheSigns(); End(); }

        [MenuItem("Shooting Range/Setup/3b - Straighten And Line Up The Signs", false, 21)]
        public static void StraightenMenu() { Begin("Straighten the signs"); StraightenTheSigns(); End(); }

        [MenuItem("Shooting Range/Setup/2 - Skin The Signs", false, 20)]
        public static void SkinTheSignsMenu() { Begin("Skin the signs"); SkinTheSigns(); End(); }

        [MenuItem("Shooting Range/Setup/3 - Fix Up The Target Prefab", false, 21)]
        public static void FixThePrefabMenu() { Begin("Target prefab"); FixTheTargetPrefab(); End(); }

        [MenuItem("Shooting Range/Setup/4 - Add The Gallery Wall", false, 22)]
        public static void AddTheWallMenu() { Begin("Gallery wall"); AddTheGalleryWall(); End(); }

        [MenuItem("Shooting Range/Setup/5 - Set Up The Rails", false, 23)]
        public static void SetUpTheRailsMenu() { Begin("Rails"); SetUpTheRails(); End(); }

        [MenuItem("Shooting Range/Setup/6 - Raise The Chargers", false, 24)]
        public static void RaiseTheChargersMenu() { Begin("Chargers"); RaiseTheChargers(); End(); }

        [MenuItem("Shooting Range/Setup/7 - Guns And Ghost Hands", false, 25)]
        public static void GunsAndHandsMenu() { Begin("Guns and hands"); GunsAndGhostHands(); End(); }

        [MenuItem("Shooting Range/Setup/4b - Box In The Roof And Wall Rails", false, 23)]
        public static void BuildCoverMenu() { Begin("Rail cover"); BuildRailCover(); End(); }

        [MenuItem("Shooting Range/Setup/8 - Set The Pace", false, 26)]
        public static void SetThePaceMenu() { Begin("Pace"); SetThePace(); End(); }

        [MenuItem("Shooting Range/Setup/9 - Trim The Hit Effects", false, 27)]
        public static void TrimEffectsMenu() { Begin("Hit effects"); TrimTheHitEffects(); End(); }

        [MenuItem("Shooting Range/Setup/5b - Pair The Tiers Both Ways", false, 24)]
        public static void PairTiersMenu() { Begin("Tier directions"); PairTheTiers(); End(); }

        [MenuItem("Shooting Range/Setup/7b - Pose The Gloves On The Grips", false, 26)]
        public static void PoseGlovesMenu() { Begin("Glove grip"); PoseTheGloves(); End(); }

        [MenuItem("Shooting Range/Setup/10 - Tune The Power-Ups", false, 28)]
        public static void TunePowerUpsMenu() { Begin("Power-ups"); TuneThePowerUps(); End(); }

        [MenuItem("Shooting Range/Setup/11 - Build And Wire The Shards", false, 29)]
        public static void ShardsMenu() { Begin("Shards"); BuildTheShards(); End(); }

        [MenuItem("Shooting Range/Setup/Measure And Check", false, 40)]
        public static void MeasureAndCheck()
        {
            Begin("Measure and check");
            MeasureSigns(true);
            CheckScene();
            End();
        }

        [MenuItem("Shooting Range/Setup/Check Sign Heights", false, 41)]
        public static void CheckSignHeightsMenu()
        {
            Begin("Sign heights, rail by rail");
            CheckSignHeights();
            End();
        }

        // ---------------------------------------------------------------- steps

        /// Sets each model's import Scale Factor so every sign's largest side is the same. Measured
        /// rather than assumed, so running it twice changes nothing
        private static void SizeTheSigns()
        {
            var figure = PrefabFigure();
            if (figure == null) return;

            var wanted = new Dictionary<string, float>();

            try
            {
                for (int i = 0; i < figure.childCount; i++)
                {
                    var sign = figure.GetChild(i);
                    if (sign.localScale != Vector3.one)
                    {
                        Bounds sized;
                        string measured = RangeSetupUtil.MeshBoundsIn(null, sign, out sized)
                                        ? " (" + sized.size.x.ToString("0.00") + " x " +
                                          sized.size.y.ToString("0.00") + " m as it stands)"
                                        : "";

                        Say(sign.name + " is scaled " + sign.localScale + " by hand, so its import size " +
                            "is left alone" + measured);

                        if (Mathf.Abs(sign.localScale.x - sign.localScale.y) > 0.001f)
                            Say("   ^ that scale is uneven, so this one is stretched against the rest of " +
                                "the set. Reset it to 1,1,1 and run this step again to have it sized to " +
                                "match, or leave it if the stretch is what you want");

                        continue;
                    }

                    Bounds bounds;
                    if (!RangeSetupUtil.MeshBoundsIn(figure, sign, out bounds)) continue;

                    float largest = Mathf.Max(bounds.size.x, bounds.size.y);
                    if (largest <= 0.0001f) continue;

                    var filter = sign.GetComponentInChildren<MeshFilter>(true);
                    string path = AssetDatabase.GetAssetPath(filter.sharedMesh);
                    var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                    if (importer == null) { Say("No model importer behind " + sign.name); continue; }

                    wanted[path] = importer.globalScale * (SignTarget / largest);
                }
            }
            finally { ClosePrefab(false); }

            foreach (var item in wanted)
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(item.Key);
                if (Mathf.Abs(importer.globalScale - item.Value) < 0.001f)
                {
                    Say(System.IO.Path.GetFileName(item.Key) + " is already the right size");
                    continue;
                }

                Did(System.IO.Path.GetFileName(item.Key) + ": Scale Factor " +
                    importer.globalScale.ToString("0.###") + " -> " + item.Value.ToString("0.###") +
                    " so its longest side is " + SignTarget + " m");

                importer.globalScale = item.Value;
                importer.SaveAndReimport();
            }
        }

        /// Stands every sign on the figure's origin and centres it. It does NOT touch how a sign is
        /// turned: which way a flat cutout's front and top face cannot be measured off its bounds, so
        /// that is a hand call. It used to clear rotations to identity and turned the zombie hand
        /// upside down and backwards doing it
        private static void StraightenTheSigns()
        {
            var figure = PrefabFigure();
            if (figure == null) return;

            for (int i = 0; i < figure.childCount; i++)
            {
                var sign = figure.GetChild(i);

                if (sign.localRotation != Quaternion.identity)
                    Say(sign.name + " is turned " + sign.localEulerAngles + ", left as you set it");

                Bounds bounds;
                if (!RangeSetupUtil.MeshBoundsIn(figure, sign, out bounds)) continue;

                var position = sign.localPosition;
                position.x -= bounds.center.x;
                position.y -= bounds.min.y;

                if ((position - sign.localPosition).sqrMagnitude > 0.000001f)
                {
                    sign.localPosition = position;

                    // Measured inside the figure, which carries the scale, so it is put back into
                    // metres rather than reported at half size
                    Did(sign.name + ": stood on its feet, centred, now " +
                        (bounds.size.y * figure.localScale.y).ToString("0.00") + " m tall");
                }
            }

            ClosePrefab(true);
            Say("Feet and centring only. If a sign faces the wrong way or stands on its head, turn it by " +
                "hand and run this again to re-stand it");
        }
        private static void SkinTheSigns()
        {
            var figure = PrefabFigure();
            if (figure == null) return;

            try
            {
                for (int i = 0; i < figure.childCount; i++)
                {
                    var sign = figure.GetChild(i);
                    var renderer = sign.GetComponentInChildren<MeshRenderer>(true);
                    if (renderer == null) continue;

                    var material = renderer.sharedMaterial;
                    if (material == null || material.shader == null) continue;

                    if (material.shader.name == "Standard")
                        Say(sign.name + " still uses the Standard shader. Unlit/Texture is the Quest-friendly one");
                    else
                        Say(sign.name + " material: " + material.name + " (" + material.shader.name + ")");
                }
            }
            finally { ClosePrefab(false); }
        }

        private static void FixTheTargetPrefab()
        {
            var root = OpenPrefab();
            if (root == null) return;

            bool changed = false;

            var rootScale = root.transform.localScale;
            if (rootScale != Vector3.one)
            {
                // The figure turns to face the player, and a rotating child under a parent scaled
                // unevenly is sheared: the sign looks wider head on than it does side on. Moving the
                // scale onto the figure keeps the size and loses the shear, because the figure's own
                // scale is applied before its own rotation
                var lift = RangeSetupUtil.Child(root.transform, "Figure");
                if (lift != null) lift.localScale = Vector3.Scale(lift.localScale, rootScale);

                root.transform.localScale = Vector3.one;
                Did("Root scale " + rootScale + " moved onto the Figure and reset to 1,1,1. That is what " +
                    "was making the signs change shape as they turned");
                changed = true;
            }

            changed |= Strip<MeshFilter>(root) | Strip<MeshRenderer>(root) | Strip<Collider>(root);

            var figure = RangeSetupUtil.Child(root.transform, "Figure");
            if (figure == null)
            {
                var made = new GameObject("Figure");
                made.transform.SetParent(root.transform, false);
                figure = made.transform;
                Did("Created the Figure child. It has no signs under it yet");
                changed = true;
            }

            int prop = RangeSetupUtil.Layer("Prop");
            var signs = new List<Object>();

            for (int i = 0; i < figure.childCount; i++)
            {
                var sign = figure.GetChild(i);
                signs.Add(sign);

                if (sign.GetComponentInChildren<Collider>(true) == null)
                {
                    var filter = sign.GetComponentInChildren<MeshFilter>(true);
                    var host = filter != null ? filter.gameObject : sign.gameObject;
                    host.AddComponent<BoxCollider>();
                    Did("Box collider added to " + sign.name);
                    changed = true;
                }

                if (prop >= 0) changed |= SetLayer(sign, prop);
            }

            var popUp = root.GetComponent<PopUpTarget>();
            if (popUp == null)
            {
                popUp = root.AddComponent<PopUpTarget>();
                Did("Pop Up Target added to the root");
                changed = true;
            }

            var popUpFields = new Fields(popUp);
            int had = popUpFields.Find("variants").arraySize;
            popUpFields.Set("figure", figure).SetArray("variants", signs).Apply();

            Did("Pop Up Target wired: figure, and " + signs.Count + " signs in the pool" +
                (signs.Count > had ? ", " + (signs.Count - had) + " of them new" : ""));

            for (int i = 0; i < signs.Count; i++)
            {
                var sign = (Transform)signs[i];

                Bounds bounds;
                Say("   " + sign.name.PadRight(16) +
                    (RangeSetupUtil.MeshBoundsIn(null, sign, out bounds)
                     ? bounds.size.x.ToString("0.00") + " x " + bounds.size.y.ToString("0.00") + " m"
                     : "no mesh, so it can never be measured or shot"));
            }

            var mover = root.GetComponent<TrackMover>();
            if (mover != null) new Fields(mover).Set("target", popUp).Apply();

            ClosePrefab(true);
            if (!changed) Say("Prefab was already in shape; only the wiring was refreshed");
        }

        private static void AddTheGalleryWall()
        {
            float needed = WallTopNeeded();

            var wall = RangeSetupUtil.FindInScene("GalleryWall");
            if (wall != null)
            {
                float top = wall.transform.position.y + wall.transform.lossyScale.y * 0.5f;
                Say("GalleryWall is already there at " + wall.transform.position + ", left alone");

                if (!float.IsNegativeInfinity(needed) && top < needed - 0.01f)
                    Say("   ^ its top is at " + top.ToString("0.00") + " m and the furthest floor rail " +
                        "needs " + needed.ToString("0.00") + " m. Below that, a tall sign has to drop " +
                        "through the floor to leave only a sliver showing");
                return;
            }

            var scale = WallScale;
            var position = WallPosition;

            if (!float.IsNegativeInfinity(needed) && needed > position.y + scale.y * 0.5f)
            {
                scale.y = needed;
                position.y = needed * 0.5f;
                Say("Raised to " + needed.ToString("0.00") + " m so the furthest floor rail can still hide " +
                    "the tallest sign. Looking down over a wall, the sight line drops the further back " +
                    "the rail is, and a shorter wall runs the signs out of room above the floor");
            }

            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "GalleryWall";
            wall.transform.position = position;
            wall.transform.localScale = scale;

            var material = RangeSetupUtil.Load<Material>("Assets/Materials/SR_Wall.mat");
            if (material != null) wall.GetComponent<MeshRenderer>().sharedMaterial = material;

            Did("GalleryWall created at " + position + ", " + scale.y.ToString("0.00") + " m high");
            Dirty();
        }

        /// How high the wall's top has to be for the furthest floor rail to hide the tallest sign with
        /// only its peek showing. The sight line over a wall below eye level falls away with distance,
        /// so the far rails are what sets this, not the near ones
        private static float WallTopNeeded()
        {
            float signTop, signBottom;
            if (!MeasureSigns(false, out signTop, out signBottom)) return float.NegativeInfinity;

            Vector3 eye = EyePoint();
            float wallFar = WallPosition.z + WallScale.z * 0.5f;

            float needed = float.NegativeInfinity;

            foreach (var rail in Object.FindObjectsOfType<TrackRail>())
            {
                if (rail.Group != TrackGroup.FloorAcross) continue;

                for (int i = 0; i <= Samples; i++)
                {
                    Vector3 point = rail.PointAt(rail.Length * i / (float)Samples);

                    Vector3 flat = point - eye;
                    flat.y = 0f;

                    float reach = flat.magnitude;
                    float span = wallFar - eye.z;
                    if (reach <= span || span <= 0.01f) continue;

                    // Turn "the line has to clear the sign's height, less its peek" back into a top
                    needed = Mathf.Max(needed, eye.y + (signTop - SignPeek - eye.y) * span / reach);
                }
            }

            // A wall at eye level or above stops being something you look over
            return float.IsNegativeInfinity(needed) ? needed : Mathf.Min(needed, eye.y - 0.1f);
        }

        /// Offsets are derived from the geometry, not typed by hand, so this one recomputes every run
        private static void SetUpTheRails()
        {
            float signTop, signBottom;
            if (!MeasureSigns(false, out signTop, out signBottom)) return;

            float signHeight = signTop - Mathf.Min(0f, signBottom);
            Say("Tallest sign measures " + signHeight.ToString("0.00") + " m in real metres");

            float shortestTop = ShortestSignTop();
            Say("Shortest sign's top edge is " + shortestTop.ToString("0.00") +
                " m above its feet. The offsets written here suit that one, so nothing ever ends up " +
                "hidden altogether; at Play each sign works its own out from its own height");

            float ceilingUnder = CeilingBottom();
            float lateRound = LateRound();

            foreach (var rail in Object.FindObjectsOfType<TrackRail>())
            {
                var fields = new Fields(rail);

                if (rail.Group == TrackGroup.Riser)
                {
                    fields.Set("shownOffset", 0f).Set("hinge", (int)Hinge.Sideways)
                          .Set("fromRound", RiserFromRound).Apply();
                    Did(rail.name + ": swings out sideways off the wall, from halfway through round " +
                        Mathf.FloorToInt(RiserFromRound));
                    continue;
                }

                if (rail.Group == TrackGroup.RoofAcross || rail.Group == TrackGroup.RoofAlong)
                {
                    fields.Set("shownOffset", 0f)
                          .Set("hinge", (int)Hinge.Down).Set("fromRound", lateRound).Apply();
                    Did(rail.name + ": hangs upside down by its feet from the rail, from round " +
                        lateRound.ToString("0.#"));
                    continue;
                }

                if (rail.Group != TrackGroup.FloorAcross) continue;

                var cover = CoverFor(rail);

                int covered;
                float shown = RequiredShow(rail, shortestTop, cover, out covered);

                if (covered == 0)
                {
                    // Nothing to measure against, so there is nothing to derive: the offset and the
                    // hinge stay whatever they were set to by hand, and only the cover is cleared
                    float had = fields.Float("shownOffset");
                    var hinge = (Hinge)fields.Int("hinge");

                    fields.Set("cover", (Object)null).Apply();

                    Say(rail.name + (cover == null ? " has no cover" : " is not behind " + cover.name) +
                        ", so its Shown Offset (" + had.ToString("0.00") + ") and Hinge (" + hinge +
                        ") are left exactly as you set them");
                    continue;
                }

                fields.Set("shownOffset", shown).Set("hinge", (int)Hinge.Up)
                      .Set("cover", cover).Apply();

                Did(rail.name + ": hides behind " + cover.name + ", shown " + shown.ToString("0.00") +
                    ", showing " + SignPeek.ToString("0.00") + " m of the sign over it at " + covered +
                    " of " + (Samples + 1) + " points along it" +
                    (covered <= Samples ? " (the rest of it is in the open)" : ""));

                float reach = HighestPoint(rail) + shown + signTop;
                if (reach > ceilingUnder)
                    Say("   ^ its sign would poke " + (reach - ceilingUnder).ToString("0.00") +
                        " m through the ceiling. Lower the cover in front of this rail by about that much");

                // A rail far behind a low wall has to drop its signs a long way to leave only a sliver
                // showing, and past a point there is not enough room under the sight line to put them
                float foot = LowestPoint(rail) + shown;
                if (foot < 0f)
                    Say("   ^ the tallest sign's feet would be " + (-foot).ToString("0.00") +
                        " m under the floor. This rail is too far back for " + cover.name +
                        ": raise that cover, or bring the rail nearer the player");
            }

            Dirty();
            ClearMissingRails();
        }

        private static float HighestPoint(TrackRail rail)
        {
            float best = float.NegativeInfinity;
            for (int i = 0; i <= Samples; i++)
                best = Mathf.Max(best, rail.PointAt(rail.Length * i / (float)Samples).y);

            return best;
        }

        private static float LowestPoint(TrackRail rail)
        {
            float best = float.PositiveInfinity;
            for (int i = 0; i <= Samples; i++)
                best = Mathf.Min(best, rail.PointAt(rail.Length * i / (float)Samples).y);

            return best;
        }

        private static float RequiredShow(TrackRail rail, float signTop, Renderer cover, out int covered)
        {
            float best = float.NegativeInfinity;
            covered = 0;

            Vector3 eye = EyePoint();

            for (int i = 0; i <= Samples; i++)
            {
                Vector3 point = rail.PointAt(rail.Length * i / (float)Samples);

                float line = LineOverCover(cover, point, eye);
                if (float.IsNegativeInfinity(line)) continue;

                covered++;
                best = Mathf.Max(best, line + SignPeek - signTop - point.y);
            }

            return covered == 0 ? 0f : best;
        }

        private static Renderer CoverFor(TrackRail rail)
        {
            var named = RangeSetupUtil.FindInScene("Cover_" + rail.name);
            if (named == null) named = RangeSetupUtil.FindInScene("GalleryWall");

            return named != null ? named.GetComponentInChildren<MeshRenderer>() : null;
        }

        /// Where the player's eyes are. The rig in the scene if there is one, so the sums match what the
        /// headset will do, rather than an eye assumed at the origin behind where they actually stand
        private static Vector3 EyePoint()
        {
            var head = HeadTransform();
            if (head != null) return head.position;

            var floor = RangeSetupUtil.FindInScene("PlayerFloor");
            return floor != null
                 ? floor.transform.position + Vector3.up * EyeHeight
                 : new Vector3(0f, EyeHeight, 0f);
        }

        private static float LineOverCover(Renderer cover, Vector3 point, Vector3 eye)
        {
            return cover == null
                 ? float.NegativeInfinity
                 : PopUpTarget.SightLineOver(cover.bounds, point, eye, NearestCover);
        }

        private static void BuildRailCover()
        {
            int built = 0;

            foreach (var rail in Object.FindObjectsOfType<TrackRail>())
            {
                bool roof = rail.Group == TrackGroup.RoofAcross || rail.Group == TrackGroup.RoofAlong;
                if (!roof && rail.Group != TrackGroup.Riser) continue;

                string name = "Cover_" + rail.name;
                if (RangeSetupUtil.FindInScene(name) != null) { Say(name + " is already there"); continue; }

                var cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cover.name = name;
                cover.transform.SetParent(rail.transform.parent, false);
                cover.transform.position = rail.PointAt(rail.Length * 0.5f);
                cover.transform.rotation = rail.transform.rotation;
                cover.transform.localScale = new Vector3(CoverThickness, CoverThickness, rail.Length + 0.2f);

                var material = RangeSetupUtil.Load<Material>("Assets/Materials/SR_Wall.mat");
                if (material != null) cover.GetComponent<MeshRenderer>().sharedMaterial = material;

                built++;
            }

            if (built > 0) { Did("Boxed in " + built + " roof and wall rails"); Dirty(); }
        }

        private static void RaiseTheChargers()
        {
            var wall = RangeSetupUtil.FindInScene("GalleryWall");
            if (wall == null) { Fail("No GalleryWall in the scene. Run step 4 first"); return; }

            float wallTop = wall.transform.position.y + wall.transform.lossyScale.y * 0.5f;
            float target = wallTop + 0.1f;
            int moved = 0;

            foreach (var rail in Object.FindObjectsOfType<TrackRail>())
            {
                if (rail.Group != TrackGroup.FloorAlong) continue;
                if (rail.transform.position.y >= wallTop + 0.05f) { Say(rail.name + " already clears the wall"); continue; }

                var position = rail.transform.position;
                Did(rail.name + ": y " + position.y.ToString("0.00") + " -> " + target.ToString("0.00"));
                position.y = target;
                rail.transform.position = position;
                moved++;
            }

            if (moved > 0) Dirty();
        }

        private static void GunsAndGhostHands()
        {
            var game = Object.FindObjectOfType<RangeGame>();
            if (game == null) { Fail("No RangeGame in the scene"); return; }

            var gameFields = new Fields(game);
            if (!gameFields.Bool("requirePickupToStart"))
            {
                gameFields.Set("requirePickupToStart", true).Apply();
                Did("Require Pickup To Start switched on");
            }

            float benchTop = BenchTop();
            var bench = RangeSetupUtil.FindInScene("ShootingBench");
            float benchZ = bench != null ? bench.transform.position.z : 0.55f;

            foreach (var pistol in Object.FindObjectsOfType<Pistol>())
            {
                var fields = new Fields(pistol);
                if (fields.Bool("holsterToHead"))
                {
                    fields.Set("holsterToHead", false).Apply();
                    Did(pistol.name + ": Holster To Head off, so it stays on the table");
                }

                bool left = pistol.Hand == VRAvatarLimbType.LeftHand;
                var resting = new Vector3(left ? -0.25f : 0.25f, benchTop + 0.03f, benchZ);

                if (Mathf.Abs(pistol.transform.position.y - resting.y) > 0.15f)
                {
                    pistol.transform.position = resting;
                    Did(pistol.name + " placed on the bench at " + resting);
                }

                if (pistol.GetComponent<PistolPickup>() == null)
                {
                    var pickup = pistol.gameObject.AddComponent<PistolPickup>();
                    new Fields(pickup).Set("game", game).Set("pistol", pistol)
                                      .Set("onlyItsOwnHand", true).Apply();
                    Did("Pistol Pickup added to " + pistol.name + " (" + pistol.Hand + " only)");
                }
            }

            var ghostLeft = EnsureGhostHand("GhostHandLeft", LeftGlove);
            var ghostRight = EnsureGhostHand("GhostHandRight", RightGlove);

            var visuals = Object.FindObjectOfType<HandVisuals>();
            if (visuals == null)
            {
                visuals = game.gameObject.AddComponent<HandVisuals>();
                Did("Hand Visuals added to " + game.name);
            }

            var so = new SerializedObject(visuals);
            so.FindProperty("game").objectReferenceValue = game;

            var hands = so.FindProperty("hands");
            hands.arraySize = 2;
            WireHand(hands.GetArrayElementAtIndex(0), VRAvatarLimbType.LeftHand, ghostLeft);
            WireHand(hands.GetArrayElementAtIndex(1), VRAvatarLimbType.RightHand, ghostRight);
            so.ApplyModifiedPropertiesWithoutUndo();

            Did("Hand Visuals wired: ghost hands before the pickup, Armed left empty for the gloves");
            Dirty();
        }

        /// Overwrites, on purpose. A sign rises, then travels, then drops at the end, and these are the
        /// numbers that make that read as one motion rather than a blur
        private static void SetThePace()
        {
            var director = Object.FindObjectOfType<TrackDirector>();
            if (director == null) Fail("No TrackDirector in the scene");
            else
            {
                new Fields(director).Set("minSpeed", CrossMinSpeed).Set("maxSpeed", CrossMaxSpeed).Apply();
                Did("Crossing speed set to " + CrossMinSpeed + "-" + CrossMaxSpeed + " m/s in round 1, " +
                    "about 6 to 11 seconds to cross an 8 m rail");
                Dirty();
            }

            var root = OpenPrefab();
            if (root == null) return;

            var popUp = root.GetComponent<PopUpTarget>();
            if (popUp == null) { Fail("The prefab has no Pop Up Target yet. Run step 3"); return; }

            new Fields(popUp).Set("riseDelay", RiseDelay).Set("riseSeconds", RiseSeconds)
                             .Set("fallSeconds", FallSeconds).Set("peek", SignPeek)
                             .Set("nearestCover", NearestCover).Apply();

            Did("Sign timing: waits " + RiseDelay + " s, rises over " + RiseSeconds + " s, drops over " +
                FallSeconds + " s");
            Did("Signs show " + SignPeek.ToString("0.00") + " m of themselves over their cover, the same " +
                "number this tool measures the rails against");

            ClosePrefab(true);
        }

        /// Overwrites, on purpose. The burst was two to four metres across, several times the size of
        /// the sign it went off on, so it buried the spin and the fold and the shatter behind it
        private static void TrimTheHitEffects()
        {
            Trim("Assets/Prefabs/ShootingRange/FX_Impact.prefab");
            Trim("Assets/Prefabs/ShootingRange/FX_TargetPop.prefab");
        }

        private static void Trim(string path)
        {
            if (RangeSetupUtil.Load<GameObject>(path) == null) { Say("No effect at " + path); return; }

            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var effect in contents.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = effect.main;
                    float was = main.startSize.constantMax;

                    main.startSize = new ParticleSystem.MinMaxCurve(EffectSizeMin, EffectSizeMax);

                    Did(System.IO.Path.GetFileNameWithoutExtension(path) + " / " + effect.name +
                        ": particles " + was.ToString("0.00") + " m -> " + EffectSizeMin + "-" +
                        EffectSizeMax + " m");
                }

                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        /// Every other rail in a group is turned to run the other way, so a tier of two carries traffic
        /// in both directions at once. Which rail ends up which way is decided by position, not by
        /// toggling, so running it twice changes nothing
        private static void PairTheTiers()
        {
            foreach (TrackGroup group in System.Enum.GetValues(typeof(TrackGroup)))
            {
                if (group == TrackGroup.Riser) continue;

                var inGroup = new List<TrackRail>();
                foreach (var rail in Object.FindObjectsOfType<TrackRail>())
                {
                    if (rail.Group == group) inGroup.Add(rail);
                }

                if (inGroup.Count < 2) continue;

                inGroup.Sort((a, b) =>
                {
                    var pa = a.transform.position;
                    var pb = b.transform.position;
                    int byZ = pa.z.CompareTo(pb.z);
                    return byZ != 0 ? byZ : pa.x.CompareTo(pb.x);
                });

                Vector3 reference = inGroup[0].Direction;

                for (int i = 0; i < inGroup.Count; i++)
                {
                    bool wantsReference = i % 2 == 0;
                    bool runsReference = Vector3.Dot(inGroup[i].Direction, reference) > 0f;
                    if (wantsReference == runsReference) continue;

                    var rail = inGroup[i];
                    Vector3 far = rail.PointAt(rail.Length);
                    Vector3 back = -rail.Direction;

                    rail.transform.position = far;
                    rail.transform.rotation = Quaternion.LookRotation(back, Vector3.up);
                    Did(rail.name + " turned to run the other way along " + group);
                }
            }

            Dirty();
        }

        /// Curls the glove's fingers round the grip. Absolute, taken from the model's own bone pose, so
        /// it cannot creep further closed each time it is run
        private static void PoseTheGloves()
        {
            int posed = 0;

            foreach (var pistol in Object.FindObjectsOfType<Pistol>())
            {
                Transform glove = null;
                foreach (var child in pistol.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name.ToLowerInvariant().Contains("glove")) { glove = child; break; }
                }

                if (glove == null) { Say(pistol.name + " has no glove under it"); continue; }

                int bones = 0;
                foreach (var bone in glove.GetComponentsInChildren<Transform>(true))
                {
                    string name = bone.name.ToLowerInvariant();
                    if (!name.StartsWith("finger_") || name.EndsWith("_end") || name.Contains("meta")) continue;

                    var source = PrefabUtility.GetCorrespondingObjectFromSource(bone) as Transform;
                    Quaternion rest = source != null ? source.localRotation : bone.localRotation;

                    float curl = name.Contains("thumb") ? ThumbCurl : FingerCurl;
                    bone.localRotation = rest * Quaternion.AngleAxis(curl, CurlAxis);
                    bones++;
                }

                Did(pistol.name + ": curled " + bones + " finger joints on " + glove.name);
                posed++;
            }

            if (posed > 0) Dirty();
            Say("If they splay rather than curl, change CurlAxis in RangeSetup.cs and run this again");
        }

        /// Overwrites, on purpose. Milestones come further apart, the window is shorter and a combo
        /// buys less time, so a stack has to be worked for
        private static void TuneThePowerUps()
        {
            var combo = Object.FindObjectOfType<ComboTracker>();
            if (combo == null) Fail("No ComboTracker in the scene");
            else
            {
                new Fields(combo).Set("chainPerMilestone", MilestoneChain)
                                 .Set("windowSeconds", ComboWindow).Apply();
                Did("Combo: a power-up every " + MilestoneChain + " links, window " + ComboWindow + " s");
            }

            var powerUps = Object.FindObjectOfType<PowerUps>();
            if (powerUps == null) { Fail("No PowerUps in the scene"); return; }

            var director = Object.FindObjectOfType<TrackDirector>();

            new Fields(powerUps).Set("secondsPerHit", SecondsPerHit)
                                .Set("maxLengthMultiple", MaxLengthMultiple)
                                .Set("minTargetsForSlowMotion", MinTargetsForSlow)
                                .Set("director", director).Apply();

            Did("Power-ups: " + SecondsPerHit + " s per combo hit, capped at x" + MaxLengthMultiple +
                ", slow motion only drawn with " + MinTargetsForSlow + "+ signs up" +
                (director == null ? " (NO TrackDirector found, so it will never draw slow motion)" : ""));

            Dirty();
        }

        /// Turns each exported broken FBX into a prefab the shatter can throw: every piece gets a body
        /// and a box, wearing the same material as the sign it was cut from. Then hangs each set off
        /// its own sign and hands the lot to the pool
        private static void BuildTheShards()
        {
            var sources = AssetDatabase.FindAssets("t:GameObject", new[] { ShardSourceDir });
            if (sources.Length == 0) { Fail("No broken FBX under " + ShardSourceDir); return; }

            var skins = SignMaterials();
            int prop = RangeSetupUtil.Layer("Prop");

            System.IO.Directory.CreateDirectory(ShardPrefabDir);
            var built = new Dictionary<string, GameObject>();

            foreach (var guid in sources)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var model = RangeSetupUtil.Load<GameObject>(path);
                if (model == null) continue;

                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                string key = MatchKey(file, skins.Keys);

                var copy = (GameObject)PrefabUtility.InstantiatePrefab(model);
                int pieces = 0;

                foreach (var filter in copy.GetComponentsInChildren<MeshFilter>(true))
                {
                    var piece = filter.gameObject;
                    if (prop >= 0) piece.layer = prop;

                    if (piece.GetComponent<Collider>() == null) piece.AddComponent<BoxCollider>();

                    var body = piece.GetComponent<Rigidbody>();
                    if (body == null) body = piece.AddComponent<Rigidbody>();
                    body.mass = ShardMass;

                    if (key != null)
                    {
                        var renderer = piece.GetComponent<MeshRenderer>();
                        if (renderer != null) renderer.sharedMaterial = skins[key];
                    }

                    pieces++;
                }

                string target = ShardPrefabDir + "/" + file + ".prefab";
                var saved = PrefabUtility.SaveAsPrefabAsset(copy, target);
                Object.DestroyImmediate(copy);

                if (saved == null) { Fail("Could not save " + target); continue; }

                built[file] = saved;
                Did(file + ": " + pieces + " pieces, bodies and boxes on" +
                    (key != null ? ", wearing " + skins[key].name : ", NO matching sign material"));
            }

            if (built.Count == 0) return;

            WireShardsToSigns(built);
            WireShardsToPool(built);
        }

        /// Each sign, keyed by the model it was built from, so a rename cannot mis-pair them
        private static Dictionary<string, Material> SignMaterials()
        {
            var skins = new Dictionary<string, Material>();

            var figure = PrefabFigure();
            if (figure == null) return skins;

            try
            {
                for (int i = 0; i < figure.childCount; i++)
                {
                    var sign = figure.GetChild(i);
                    var filter = sign.GetComponentInChildren<MeshFilter>(true);
                    var renderer = sign.GetComponentInChildren<MeshRenderer>(true);
                    if (filter == null || filter.sharedMesh == null || renderer == null) continue;

                    string source = System.IO.Path.GetFileNameWithoutExtension(
                        AssetDatabase.GetAssetPath(filter.sharedMesh)).ToLowerInvariant();

                    if (!string.IsNullOrEmpty(source) && !skins.ContainsKey(source))
                        skins.Add(source, renderer.sharedMaterial);
                }
            }
            finally { ClosePrefab(false); }

            return skins;
        }

        private static void WireShardsToSigns(Dictionary<string, GameObject> built)
        {
            var figure = PrefabFigure();
            if (figure == null) return;

            for (int i = 0; i < figure.childCount; i++)
            {
                var sign = figure.GetChild(i);
                var filter = sign.GetComponentInChildren<MeshFilter>(true);
                if (filter == null || filter.sharedMesh == null) continue;

                string source = System.IO.Path.GetFileNameWithoutExtension(
                    AssetDatabase.GetAssetPath(filter.sharedMesh)).ToLowerInvariant();

                GameObject set = null;
                foreach (var pair in built)
                {
                    if (pair.Key.ToLowerInvariant().Contains(source)) { set = pair.Value; break; }
                }

                if (set == null) { Say(sign.name + " has no broken set cut from " + source); continue; }

                var link = sign.GetComponent<SignShards>();
                if (link == null) link = sign.gameObject.AddComponent<SignShards>();
                new Fields(link).Set("brokenSet", set).Apply();

                Did(sign.name + " shatters into " + set.name);
            }

            ClosePrefab(true);
        }

        private static void WireShardsToPool(Dictionary<string, GameObject> built)
        {
            var pool = Object.FindObjectOfType<ShatterPool>();
            if (pool == null) { Fail("No ShatterPool in the scene"); return; }

            var sets = new List<Object>();
            foreach (var pair in built) sets.Add(pair.Value);

            new Fields(pool).SetArray("shardSets", sets).Apply();
            Did("ShatterPool pre-warms " + sets.Count + " broken sets");
            Dirty();
        }

        private static string MatchKey(string file, Dictionary<string, Material>.KeyCollection keys)
        {
            string low = file.ToLowerInvariant();
            foreach (var key in keys)
            {
                if (low.Contains(key)) return key;
            }

            return null;
        }

        // ---------------------------------------------------------------- the temporary preview

        private const string PreviewName = "SignPreview (temporary)";
        private const string PreviewSignKey = "RangeSetup.PreviewSign";
        private const string PreviewFoldedKey = "RangeSetup.PreviewFolded";

        [MenuItem("Shooting Range/Setup/Preview/Put One Of Each On Every Rail", false, 60)]
        public static void PreviewMenu()
        {
            EditorPrefs.SetInt(PreviewSignKey, -1);
            EditorPrefs.SetBool(PreviewFoldedKey, false);
            Begin("Sign preview");
            BuildPreview();
            End();
        }

        [MenuItem("Shooting Range/Setup/Preview/Next Sign &n", false, 61)]
        public static void PreviewNextMenu()
        {
            EditorPrefs.SetInt(PreviewSignKey, EditorPrefs.GetInt(PreviewSignKey, -1) + 1);
            Begin("Sign preview");
            BuildPreview();
            End();
        }

        [MenuItem("Shooting Range/Setup/Preview/Fold And Unfold &f", false, 62)]
        public static void PreviewFoldMenu()
        {
            EditorPrefs.SetBool(PreviewFoldedKey, !EditorPrefs.GetBool(PreviewFoldedKey, false));
            Begin("Sign preview");
            BuildPreview();
            End();
        }

        [MenuItem("Shooting Range/Setup/Preview/Clear It Away", false, 63)]
        public static void PreviewClearMenu()
        {
            Begin("Clear the sign preview");
            if (ClearPreview()) Did("Preview removed");
            else Say("There was no preview to remove");
            End();
        }

        /// Stands the signs on the rails exactly where the game would put them, so the offsets and the
        /// hinges can be judged by eye. Everything lands under one object you can delete, nothing is
        /// marked dirty, and the whole lot is rebuilt from scratch on every step rather than patched
        private static void BuildPreview()
        {
            bool folded = EditorPrefs.GetBool(PreviewFoldedKey, false);

            ClearPreview();

            var figure = PrefabFigure();
            if (figure == null) return;

            var popUp = figure.parent != null ? figure.parent.GetComponent<PopUpTarget>() : null;
            bool flip = popUp != null && new Fields(popUp).Bool("sidewaysFlip");

            int count = figure.childCount;
            if (count == 0) { Fail("The Figure has no signs under it"); ClosePrefab(false); return; }

            int wanted = EditorPrefs.GetInt(PreviewSignKey, -1);
            if (wanted >= count) { wanted = -1; EditorPrefs.SetInt(PreviewSignKey, -1); }

            var root = new GameObject(PreviewName);
            var player = PlayerTransform();
            int placed = 0;

            try
            {
                foreach (var rail in Object.FindObjectsOfType<TrackRail>())
                {
                    var shelf = new GameObject(rail.name);
                    shelf.transform.SetParent(root.transform, false);

                    for (int i = 0; i < count; i++)
                    {
                        if (wanted >= 0 && i != wanted) continue;

                        // Spread along the rail when they are all out, otherwise stand the one being
                        // looked at in the middle of it
                        float along = wanted >= 0
                                    ? rail.Length * 0.5f
                                    : rail.Length * (i + 0.5f) / count;

                        Stand(figure.GetChild(i), figure.localScale, shelf.transform, rail, along,
                              player, flip, folded);
                        placed++;
                    }
                }
            }
            finally { ClosePrefab(false); }

            Did(placed + " signs stood on " + root.transform.childCount + " rails, " +
                (wanted >= 0 ? "showing " + figure.GetChild(wanted).name + " only" : "one of each") +
                ", " + (folded ? "folded away" : "up"));

            Say("Alt+N steps to the next sign and round again to all of them, Alt+F folds and unfolds. " +
                "Adjust a rail's Shown Offset or Hinge, then press either to see it again");
            Say("Nothing here is marked dirty, so it will not be saved with the scene unless you save it " +
                "yourself. Clear It Away removes the lot");

            Selection.activeGameObject = root;
        }

        /// One sign, posed the way PopUpTarget would pose it on this rail
        private static void Stand(Transform sign, Vector3 figureScale, Transform parent, TrackRail rail,
                                  float along, Transform player, bool flip, bool folded)
        {
            Vector3 home = rail.PointAt(along) + Vector3.up * rail.ShownOffset + rail.SignOffset;

            var stage = new GameObject(sign.name);
            stage.transform.SetParent(parent, false);
            stage.transform.localScale = figureScale;

            var copy = Object.Instantiate(sign.gameObject, stage.transform);
            copy.name = sign.name;
            copy.transform.localPosition = sign.localPosition;
            copy.transform.localRotation = sign.localRotation;
            copy.transform.localScale = sign.localScale;
            copy.SetActive(true);

            foreach (var collider in copy.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);

            Vector3 low, high;
            PopUpTarget.Measure(sign, out low, out high);

            float side = player != null ? PopUpTarget.SideOf(home, player) : 1f;
            var shape = PopUpTarget.ShapeFor(rail.Hinge, low, high, side, flip);
            var facing = player != null ? PopUpTarget.SquareOn(player) : Quaternion.identity;

            PopUpTarget.PlaceFigure(stage.transform, home, facing, shape, folded ? shape.Folded : 0f, 0f);
        }

        private static Transform PlayerTransform()
        {
            var player = RangeSetupUtil.FindInScene("VRAvatar");
            return player != null ? player.transform : null;
        }

        private static Transform HeadTransform()
        {
            var head = RangeSetupUtil.FindInScene("Head");
            return head != null ? head.transform : null;
        }

        private static bool ClearPreview()
        {
            var old = RangeSetupUtil.FindInScene(PreviewName);
            if (old == null) return false;

            Object.DestroyImmediate(old);
            return true;
        }

        // ---------------------------------------------------------------- measuring and checking

        private static bool MeasureSigns(bool talk) { float a, b; return MeasureSigns(talk, out a, out b); }

        /// The shortest sign in the set, feet to top. A rail carries one offset for every sign it might pick, so
        /// that offset is built around the shortest one and the taller ones show a little more
        private static float ShortestSignTop()
        {
            var figure = PrefabFigure();
            if (figure == null) return 0f;

            float shortest = float.PositiveInfinity;

            try
            {
                for (int i = 0; i < figure.childCount; i++)
                {
                    Bounds bounds;
                    if (!RangeSetupUtil.MeshBoundsIn(null, figure.GetChild(i), out bounds)) continue;

                    shortest = Mathf.Min(shortest, bounds.size.y);
                }
            }
            finally { ClosePrefab(false); }

            return float.IsPositiveInfinity(shortest) ? 0f : shortest;
        }

        private static bool MeasureSigns(bool talk, out float top, out float bottom)
        {
            top = 0f;
            bottom = 0f;

            var figure = PrefabFigure();
            if (figure == null) return false;

            try
            {
                if (figure.childCount == 0) { Fail("The Figure has no signs under it"); return false; }

                bool first = true;
                for (int i = 0; i < figure.childCount; i++)
                {
                    var sign = figure.GetChild(i);
                    Bounds bounds;
                    if (!RangeSetupUtil.MeshBoundsIn(null, sign, out bounds))
                    {
                        Say(sign.name + " has no mesh to measure");
                        continue;
                    }

                    if (first) { top = bounds.max.y; bottom = bounds.min.y; first = false; }
                    else { top = Mathf.Max(top, bounds.max.y); bottom = Mathf.Min(bottom, bounds.min.y); }

                    if (!talk) continue;

                    Say(string.Format("{0}: {1:0.00} wide x {2:0.00} tall, feet at {3:0.00}, top at {4:0.00}",
                        sign.name, bounds.size.x, bounds.size.y, bounds.min.y, bounds.max.y));

                    if (bounds.min.y < -0.02f)
                        Say("   ^ hangs below the figure's origin. Nudge its local Y up by " +
                            (-bounds.min.y).ToString("0.00") + " so it stands on its feet");
                }

                if (first) { Fail("None of the signs had a mesh"); return false; }

                if (talk) Say("Tallest sign is " + (top - Mathf.Min(0f, bottom)).ToString("0.00") +
                              " m in real metres, carriage scale and all");
                return true;
            }
            finally { ClosePrefab(false); }
        }

        /// Every sign on every rail, in real metres off the floor. The rail only says roughly where a
        /// sign goes; the sign's own size decides what you actually see over the cover
        private static void CheckSignHeights()
        {
            var feet = new List<string>();
            var low = new List<float>();
            var high = new List<float>();

            var figure = PrefabFigure();
            if (figure == null) return;

            try
            {
                for (int i = 0; i < figure.childCount; i++)
                {
                    var sign = figure.GetChild(i);

                    Bounds bounds;
                    if (!RangeSetupUtil.MeshBoundsIn(null, sign, out bounds)) continue;

                    feet.Add(sign.name);
                    low.Add(bounds.min.y);
                    high.Add(bounds.max.y);

                    if (Mathf.Abs(bounds.min.y) > 0.02f)
                        Say(sign.name + " does not stand on the figure: its base is " +
                            bounds.min.y.ToString("0.00") + " m off. Run 3b to line it up");
                }
            }
            finally { ClosePrefab(false); }

            if (feet.Count == 0) { Fail("No signs to measure"); return; }

            Say("Sight lines are taken from an eye " + EyeHeight + " m up at the origin. In the headset the " +
                "same sums are redone from wherever the player's head actually is, so a crouch lowers the " +
                "line and the signs stay only just in view");

            float ceiling = CeilingBottom();

            foreach (var rail in Object.FindObjectsOfType<TrackRail>())
            {
                var fields = new Fields(rail);
                float shown = fields.Float("shownOffset");
                float railY = rail.PointAt(rail.Length * 0.5f).y;

                if (rail.Hinge == Hinge.Sideways)
                {
                    Say(rail.name + " swings out sideways, so height does not gate it");
                    continue;
                }

                if (rail.Hinge == Hinge.None)
                {
                    Say(rail.name + " never hides its sign");
                    continue;
                }

                bool roof = rail.Hinge == Hinge.Down;
                float line = roof ? float.NegativeInfinity : CoverLine(rail);

                Say(rail.name + (float.IsNegativeInfinity(line)
                    ? (roof ? " (hangs from the ceiling)" : " has no cover in front of it")
                    : " hides anything below " + line.ToString("0.00") + " m, behind " +
                      (rail.Cover != null ? rail.Cover.name : "NOTHING WIRED, so it falls back to its offset")));

                for (int i = 0; i < feet.Count; i++)
                {
                    // At Play a covered rail stops using its own offset and puts each sign's top edge
                    // the same sliver over the sight line, so that is what is reported here
                    bool perSign = rail.Hinge == Hinge.Up && rail.Cover != null &&
                                   !float.IsNegativeInfinity(line);

                    float tall = high[i] - low[i];
                    float lift = perSign ? line + SignPeek - tall - railY : shown;
                    float standsAt = roof ? railY + lift - tall : railY + lift;
                    float topsAt = standsAt + tall;

                    string note = "  " + tall.ToString("0.00") + " m tall";

                    if (!float.IsNegativeInfinity(line))
                    {
                        note += ", " + (topsAt - Mathf.Max(line, standsAt)).ToString("0.00") + " m of it in view";

                        if (topsAt <= line + 0.01f) note += "  SHOWS NOTHING AT ALL";
                        if (standsAt > line) note += "  ITS HINGE EDGE SITS ABOVE THE COVER";
                    }

                    if (topsAt > ceiling) note += "  THROUGH THE CEILING by " + (topsAt - ceiling).ToString("0.00");

                    Say("   " + feet[i].PadRight(16) + " base " + standsAt.ToString("0.00") +
                        "  top " + topsAt.ToString("0.00") + note);
                }
            }
        }

        private static float CoverLine(TrackRail rail)
        {
            var cover = rail.Cover;
            if (cover == null) return float.NegativeInfinity;

            Vector3 eye = EyePoint();
            float best = float.NegativeInfinity;

            for (int i = 0; i <= Samples; i++)
                best = Mathf.Max(best, LineOverCover(cover, rail.PointAt(rail.Length * i / (float)Samples), eye));

            return best;
        }

        private static void CheckScene()
        {
            var wall = RangeSetupUtil.FindInScene("GalleryWall");
            Say(wall == null ? "MISSING: GalleryWall (step 4)" : "OK: GalleryWall");

            var game = Object.FindObjectOfType<RangeGame>();
            if (game == null) { Fail("MISSING: RangeGame"); return; }

            Say(new Fields(game).Bool("requirePickupToStart")
                ? "OK: the round waits for the guns"
                : "MISSING: Require Pickup To Start is off, so the round starts on its own (step 7)");

            int pickups = Object.FindObjectsOfType<PistolPickup>().Length;
            int pistols = Object.FindObjectsOfType<Pistol>().Length;
            Say(pickups == pistols && pistols > 0
                ? "OK: " + pickups + " of " + pistols + " guns can be picked up"
                : "MISSING: " + pickups + " pickups for " + pistols + " guns (step 7)");

            Say(Object.FindObjectOfType<HandVisuals>() == null
                ? "MISSING: Hand Visuals, so no ghost hands (step 7)"
                : "OK: Hand Visuals");

            int atPlayer = 0;
            int configured = 0;
            var rails = Object.FindObjectsOfType<TrackRail>();

            foreach (var rail in rails)
            {
                if (rail.Direction.z < -0.3f) atPlayer++;
            }

            if (atPlayer > 0)
                Say(atPlayer + " of " + rails.Length + " rails run at the player. Delete those objects if " +
                    "everything is meant to travel sideways now");
            foreach (var rail in rails)
            {
                var fields = new Fields(rail);
                if (rail.Hinge != Hinge.None) configured++;
            }

            Say(configured + " of " + rails.Length + " rails present their sign; the rest ride in the open");
        }

        // ---------------------------------------------------------------- odds and ends

        private static Transform EnsureGhostHand(string name, string modelPath)
        {
            var existing = RangeSetupUtil.FindInScene(name);
            if (existing != null) return existing.transform;

            var model = RangeSetupUtil.Load<GameObject>(modelPath);
            if (model == null) { Fail("No hand model at " + modelPath); return null; }

            var hand = (GameObject)PrefabUtility.InstantiatePrefab(model);
            hand.name = name;

            var ghost = RangeSetupUtil.Load<Material>(GhostMaterial);
            if (ghost != null)
            {
                foreach (var renderer in hand.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterial = ghost;
            }

            Did("Created " + name);
            return hand.transform;
        }

        private static void WireHand(SerializedProperty element, VRAvatarLimbType limb, Transform ghost)
        {
            element.FindPropertyRelative("limb").intValue = (int)limb;
            element.FindPropertyRelative("ghost").objectReferenceValue = ghost;
        }

        /// A deleted rail leaves a hole in the grid's list, and the list only refills itself when empty
        private static void ClearMissingRails()
        {
            var grid = Object.FindObjectOfType<TrackGrid>();
            if (grid == null) return;

            var array = new Fields(grid).Find("rails");
            for (int i = 0; i < array.arraySize; i++)
            {
                if (array.GetArrayElementAtIndex(i).objectReferenceValue != null) continue;

                var fields = new Fields(grid);
                fields.Find("rails").arraySize = 0;
                fields.Apply();

                Did("The grid's rail list had holes in it from deleted rails. Emptied it, so it rebuilds " +
                    "from the rails actually under Tracks at Play");
                Dirty();
                return;
            }
        }

        private static float CeilingBottom()
        {
            var ceiling = RangeSetupUtil.FindInScene("Ceiling");
            return ceiling != null
                 ? ceiling.transform.position.y - ceiling.transform.lossyScale.y * 0.5f
                 : 3.4f;
        }

        private static float BenchTop()
        {
            var bench = RangeSetupUtil.FindInScene("ShootingBench");
            if (bench != null) return bench.transform.position.y + bench.transform.lossyScale.y * 0.5f;

            Say("No object called ShootingBench, assuming a 1 m table");
            return 1f;
        }

        private static float LateRound()
        {
            var game = Object.FindObjectOfType<RangeGame>();
            return game != null ? Mathf.Max(1, game.MaxRounds - 1) : 4f;
        }

        private static bool SetLayer(Transform item, int layer)
        {
            bool changed = false;
            foreach (var child in item.GetComponentsInChildren<Transform>(true))
            {
                if (child.gameObject.layer == layer) continue;

                child.gameObject.layer = layer;
                changed = true;
            }

            return changed;
        }

        private static bool Strip<T>(GameObject root) where T : Component
        {
            var component = root.GetComponent<T>();
            if (component == null) return false;

            Object.DestroyImmediate(component, true);
            Did("Removed " + typeof(T).Name + " from the carriage, which has to stay invisible");
            return true;
        }

        // ---------------------------------------------------------------- prefab session

        private static GameObject _prefab;

        private static GameObject OpenPrefab()
        {
            if (_prefab != null) return _prefab;

            if (RangeSetupUtil.Load<GameObject>(PrefabPath) == null)
            {
                Fail("No target prefab at " + PrefabPath);
                return null;
            }

            _prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            return _prefab;
        }

        private static Transform PrefabFigure()
        {
            var root = OpenPrefab();
            if (root == null) return null;

            var figure = RangeSetupUtil.Child(root.transform, "Figure");
            if (figure == null) { Fail("The prefab has no Figure child yet. Run step 3"); ClosePrefab(false); }

            return figure;
        }

        private static void ClosePrefab(bool save)
        {
            if (_prefab == null) return;

            if (save) PrefabUtility.SaveAsPrefabAsset(_prefab, PrefabPath);
            PrefabUtility.UnloadPrefabContents(_prefab);
            _prefab = null;
        }

        // ---------------------------------------------------------------- report

        private static void Begin(string what)
        {
            _sceneTouched = false;
            _report = new StringBuilder("[RangeSetup] " + what + "\n");
        }

        private static void Did(string line) { _report.Append("  done  ").Append(line).Append('\n'); }
        private static void Say(string line) { _report.Append("  note  ").Append(line).Append('\n'); }
        private static void Fail(string line) { _report.Append("  STOP  ").Append(line).Append('\n'); }

        private static void End()
        {
            ClosePrefab(false);
            SaveScene();
            AssetDatabase.SaveAssets();

            var text = _report.ToString();
            if (text.Contains("  STOP  ")) Debug.LogWarning(text);
            else Debug.Log(text);
        }

        private static bool _sceneTouched;

        private static void Dirty()
        {
            _sceneTouched = true;
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        }

        /// Prefab edits are saved as they are made, but scene edits were only marked dirty, so a gallery
        /// wall this tool reported building went away again on the next editor reload. What the report
        /// claims now matches what is on disk
        private static void SaveScene()
        {
            if (!_sceneTouched) return;
            _sceneTouched = false;

            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (!scene.isDirty) return;

            if (UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene))
                Did("Scene saved, so what this step changed is on disk");
            else
                Fail("The scene could not be saved. Save it by hand before reloading, or this step's " +
                     "work is lost");
        }
    }
}
