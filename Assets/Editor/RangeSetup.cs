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
        private const float PuffSizeMin = 1.2f;
        private const float PuffSizeMax = 1.9f;
        private const float PuffSpeedMin = 1.5f;
        private const float PuffSpeedMax = 3f;
        private const float PuffRadius = 0.25f;
        private const float PuffBornAt = 0.8f;
        private const float PuffFullAt = 0.12f;
        private const float PuffFadeIn = 0.04f;

        private const int MilestoneChain = 12;
        private const float ComboWindow = 2f;
        private const float SecondsPerHit = 0.25f;
        private const float MaxLengthMultiple = 1.5f;
        private const int MinTargetsForSlow = 5;

        // If the fingers splay instead of curling, this is the axis to change

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
            FitTheGloves();
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


        [MenuItem("Shooting Range/Setup/10 - Tune The Power-Ups", false, 28)]
        public static void TunePowerUpsMenu() { Begin("Power-ups"); TuneThePowerUps(); End(); }

        [MenuItem("Shooting Range/Setup/11 - Build And Wire The Shards", false, 29)]
        public static void ShardsMenu() { Begin("Shards"); BuildTheShards(); End(); }

        [MenuItem("Shooting Range/Setup/12 - Build The Gun Effects", false, 30)]
        public static void GunEffectsMenu() { Begin("Gun effects"); BuildTheGunEffects(); End(); }

        [MenuItem("Shooting Range/Setup/13 - Wire The Range Audio", false, 31)]
        public static void RangeAudioMenu() { Begin("Range audio"); WireTheRangeAudio(); End(); }

        [MenuItem("Shooting Range/Setup/14 - Build The Power-Up Glow", false, 32)]
        public static void PowerUpGlowMenu() { Begin("Power-up glow"); BuildThePowerUpGlow(); End(); }

        [MenuItem("Shooting Range/Setup/15 - Fit The Pistol Model", false, 33)]
        public static void PistolModelMenu() { Begin("Pistol model"); FitThePistolModel(); End(); }

        [MenuItem("Shooting Range/Setup/16 - Fit The Gloves", false, 34)]
        public static void GlovesMenu() { Begin("Gloves"); FitTheGloves(); End(); }

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

        /// Overwrites, on purpose. The brown bullet impact is for the room and stays small; the grey pop
        /// is the hit itself and has to cover the whole sign the instant it lands
        private static void TrimTheHitEffects()
        {
            Trim("Assets/Prefabs/ShootingRange/FX_Impact.prefab", EffectSizeMin, EffectSizeMax, false);
            Trim("Assets/Prefabs/ShootingRange/FX_TargetPop.prefab", PuffSizeMin, PuffSizeMax, true);
        }

        private static void Trim(string path, float sizeMin, float sizeMax, bool puff)
        {
            if (RangeSetupUtil.Load<GameObject>(path) == null) { Say("No effect at " + path); return; }

            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var effect in contents.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = effect.main;
                    float was = main.startSize.constantMax;

                    main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);

                    Did(name + " / " + effect.name + ": particles " + was.ToString("0.00") + " m -> " +
                        sizeMin + "-" + sizeMax + " m");

                    if (!puff) continue;

                    // Fired at 8-15 m/s from a 1 m hemisphere, the smoke left the sign before it could cover it
                    main.startSpeed = new ParticleSystem.MinMaxCurve(PuffSpeedMin, PuffSpeedMax);
                    var shape = effect.shape;
                    shape.radius = PuffRadius;

                    // Born at a third of its size and full only halfway through its life, it bloomed long
                    // after the hit it was meant to cover
                    var grow = effect.sizeOverLifetime;
                    grow.enabled = true;
                    grow.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                        new Keyframe(0f, PuffBornAt), new Keyframe(PuffFullAt, 1f), new Keyframe(1f, 0.9f)));

                    var fade = effect.colorOverLifetime;
                    var gradient = fade.color.gradient;
                    if (gradient != null)
                    {
                        gradient.SetKeys(gradient.colorKeys, new[]
                        {
                            new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, PuffFadeIn), new GradientAlphaKey(0f, 1f),
                        });
                        fade.color = gradient;
                    }

                    Did(name + " / " + effect.name + ": speed " + PuffSpeedMin + "-" + PuffSpeedMax +
                        " m/s, spawn radius " + PuffRadius + " m, full size by " + PuffFullAt * 100f +
                        "% of its life, opaque by " + PuffFadeIn * 100f + "%");
                }

                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        private const string FxTextures = "Assets/Textures/ShootingRange/";
        private const string MuzzlePath = "Assets/Prefabs/ShootingRange/FX_MuzzleFlash.prefab";
        private const string RoundPath = "Assets/Prefabs/ShootingRange/Round.prefab";
        private const float TracerSeconds = 0.06f;
        private const float TracerWidth = 0.02f;
        private const string BulletModelPath = "Assets/Models/Bullet/SM_Bullet.fbx";
        private const float BulletLength = 0.055f;
        private static readonly Color Brass = new Color(0.86f, 0.64f, 0.28f);

        /// Overwrites, on purpose, like step 9. Quest 2 rules: mobile particle shaders, small textures, a
        /// handful of particles and no light. Flash and sparks are additive and take the power-up colour
        /// at runtime; the wisp is alpha blended and stays grey
        private static void BuildTheGunEffects()
        {
            var pow = FxTexture(FxTextures + "T_SR_MuzzlePow.png", 256);
            var smoke = FxTexture(FxTextures + "T_SR_MuzzleSmoke_4x4.png", 512);
            var tracer = FxTexture(FxTextures + "T_SR_Tracer.png", 64);
            if (pow == null || smoke == null || tracer == null) return;

            var flashMat = FxMaterial("Assets/Materials/SR_MuzzleFlash.mat", "Mobile/Particles/Additive", pow);
            var smokeMat = FxMaterial("Assets/Materials/SR_MuzzleSmoke.mat", "Mobile/Particles/Alpha Blended", smoke);
            var tracerMat = FxMaterial("Assets/Materials/SR_Tracer.mat", "Mobile/Particles/Additive", tracer);
            if (flashMat == null || smokeMat == null || tracerMat == null) return;

            if (!BuildTheMuzzle(flashMat, smokeMat)) return;
            BuildTheTracer(tracerMat);
            GiveTheRoundABullet();
            WireTheGunTint();
        }

        private static Texture2D FxTexture(string path, int maxSize)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { Fail("No texture at " + path + ". Click into Unity so it imports, then run again"); return null; }

            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = maxSize;
            importer.SaveAndReimport();

            Did(System.IO.Path.GetFileName(path) + ": clamped, alpha is transparency, max " + maxSize + " px");
            return RangeSetupUtil.Load<Texture2D>(path);
        }

        private static Material FxMaterial(string path, string shaderName, Texture texture)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) { Fail("Shader '" + shaderName + "' is missing from this editor"); return null; }

            var material = RangeSetupUtil.Load<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.mainTexture = texture;
            EditorUtility.SetDirty(material);

            Did(System.IO.Path.GetFileName(path) + ": " + shaderName + " with " + texture.name);
            return material;
        }

        private static bool BuildTheMuzzle(Material flashMat, Material smokeMat)
        {
            if (RangeSetupUtil.Load<GameObject>(MuzzlePath) == null) { Fail("No muzzle flash at " + MuzzlePath); return false; }

            var contents = PrefabUtility.LoadPrefabContents(MuzzlePath);
            try
            {
                var sparks = contents.GetComponent<ParticleSystem>();
                if (sparks == null) { Fail("FX_MuzzleFlash has no particle system on its root"); return false; }

                // The root stays the sparks so the pistols' existing reference still fires the lot
                var main = sparks.main;
                main.duration = 0.2f;
                main.loop = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 8f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.025f);
                main.startColor = Color.white;
                main.maxParticles = 10;
                var emission = sparks.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 5, 8) });
                var cone = sparks.shape;
                cone.shapeType = ParticleSystemShapeType.Cone;
                cone.angle = 18f;
                cone.radius = 0.005f;

                var flash = FxChild(contents.transform, "Flash", flashMat, ParticleSystemSimulationSpace.Local);
                main = flash.main;
                main.duration = 0.1f;
                main.startLifetime = 0.06f;
                main.startSpeed = 0f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.24f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = Color.white;
                main.maxParticles = 1;
                emission = flash.emission;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
                var shape = flash.shape;
                shape.enabled = false;
                Grow(flash, 0.8f, 1.25f);
                FadeOut(flash, 0f);

                var wisp = FxChild(contents.transform, "Wisp", smokeMat, ParticleSystemSimulationSpace.World);
                main = wisp.main;
                main.duration = 0.6f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.12f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = new Color(0.85f, 0.85f, 0.85f, 0.5f);
                main.maxParticles = 4;
                emission = wisp.emission;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 2, 3) });
                shape = wisp.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 10f;
                shape.radius = 0.005f;
                Grow(wisp, 0.6f, 2.2f);
                FadeOut(wisp, 0.1f);
                var sheet = wisp.textureSheetAnimation;
                sheet.enabled = true;
                sheet.numTilesX = 4;
                sheet.numTilesY = 4;
                sheet.animation = ParticleSystemAnimationType.WholeSheet;
                sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));

                PrefabUtility.SaveAsPrefabAsset(contents, MuzzlePath);
                Did("FX_MuzzleFlash: sparks (5-8, 0.05-0.12 s) on the root, a Flash pow (1, 0.06 s, 15-24 cm) and a " +
                    "grey Wisp (2-3 cartoon puffs, 8-12 cm, 0.35-0.55 s) under it. No light, on purpose");
                return true;
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        private static ParticleSystem FxChild(Transform root, string name, Material material,
                                              ParticleSystemSimulationSpace space)
        {
            var child = RangeSetupUtil.Child(root, name);
            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(root, false);
                child.gameObject.AddComponent<ParticleSystem>();
            }

            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
            child.gameObject.layer = root.gameObject.layer;

            var system = child.GetComponent<ParticleSystem>();
            var main = system.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = space;
            main.scalingMode = root.GetComponent<ParticleSystem>().main.scalingMode;
            var emission = system.emission;
            emission.rateOverTime = 0f;

            var renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return system;
        }

        private static void Grow(ParticleSystem system, float from, float to)
        {
            var grow = system.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, from, 1f, to));
        }

        private static void FadeOut(ParticleSystem system, float fadeIn)
        {
            var gradient = new Gradient();
            var alpha = fadeIn > 0f
                ? new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fadeIn), new GradientAlphaKey(0f, 1f) }
                : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) };
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, alpha);

            var fade = system.colorOverLifetime;
            fade.enabled = true;
            fade.color = gradient;
        }

        private static void BuildTheTracer(Material tracerMat)
        {
            if (RangeSetupUtil.Load<GameObject>(RoundPath) == null) { Fail("No round at " + RoundPath); return; }

            var contents = PrefabUtility.LoadPrefabContents(RoundPath);
            try
            {
                var trail = contents.GetComponentInChildren<TrailRenderer>(true);
                if (trail == null) { Fail("Round has no Trail Renderer"); return; }

                trail.sharedMaterial = tracerMat;
                trail.time = TracerSeconds;
                trail.widthMultiplier = 1f;
                trail.widthCurve = AnimationCurve.Linear(0f, TracerWidth, 1f, TracerWidth * 0.25f);
                trail.minVertexDistance = 0.05f;
                trail.textureMode = LineTextureMode.Stretch;
                trail.numCapVertices = 0;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;

                PrefabUtility.SaveAsPrefabAsset(contents, RoundPath);
                Did("Round: crisp additive tracer, " + TracerWidth * 100f + " cm wide, " + TracerSeconds +
                    " s long (about " + (TracerSeconds * 38f).ToString("0.0") + " m at 38 m/s). Its colour is set per shot");
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        /// The capsule was a scaled sphere on the root. The model goes on a child turned nose-forward,
        /// because the projectile points its root along the flight and would undo any turn put there
        private static void GiveTheRoundABullet()
        {
            Mesh mesh = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(BulletModelPath))
            {
                mesh = asset as Mesh;
                if (mesh != null) break;
            }
            if (mesh == null) { Fail("No bullet mesh at " + BulletModelPath + ". Click into Unity so it imports"); return; }

            var shader = Shader.Find("Unlit/Color");
            var material = RangeSetupUtil.Load<Material>("Assets/Materials/SR_Bullet.mat");
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, "Assets/Materials/SR_Bullet.mat");
            }
            material.shader = shader;
            material.color = Brass;
            EditorUtility.SetDirty(material);

            float length;
            Quaternion turn = NoseForward(mesh, out length);
            float scale = BulletLength / Mathf.Max(1e-5f, length);

            var contents = PrefabUtility.LoadPrefabContents(RoundPath);
            try
            {
                var root = contents.transform;
                root.localScale = Vector3.one;

                var oldRenderer = root.GetComponent<MeshRenderer>();
                if (oldRenderer != null) Object.DestroyImmediate(oldRenderer, true);
                var oldFilter = root.GetComponent<MeshFilter>();
                if (oldFilter != null) Object.DestroyImmediate(oldFilter, true);

                var model = RangeSetupUtil.Child(root, "Model");
                if (model == null)
                {
                    model = new GameObject("Model").transform;
                    model.SetParent(root, false);
                }
                model.gameObject.layer = root.gameObject.layer;
                model.localRotation = turn;
                model.localScale = Vector3.one * scale;
                model.localPosition = -(turn * (mesh.bounds.center * scale));

                var filter = RangeSetupUtil.Ensure<MeshFilter>(model.gameObject);
                filter.sharedMesh = mesh;
                var renderer = RangeSetupUtil.Ensure<MeshRenderer>(model.gameObject);
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                PrefabUtility.SaveAsPrefabAsset(contents, RoundPath);
                Did("Round: bullet model " + (BulletLength * 100f).ToString("0.0") + " cm long (x" +
                    scale.ToString("0.0") + "), flat brass Unlit/Color, nose along the flight. Root scale back to 1");
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        /// The long axis, pointed so the narrow end leads. The base carries the rim, so the widest
        /// ring of vertices sits at the back
        private static Quaternion NoseForward(Mesh mesh, out float length)
        {
            var box = mesh.bounds;
            var size = box.size;
            int axis = size.x >= size.y && size.x >= size.z ? 0 : (size.y >= size.z ? 1 : 2);
            length = size[axis];

            float lowEnd = 0f, highEnd = 0f;
            foreach (var vertex in mesh.vertices)
            {
                float along = (vertex[axis] - box.min[axis]) / Mathf.Max(1e-6f, length);
                var across = vertex - box.center;
                across[axis] = 0f;

                if (along < 0.1f) lowEnd = Mathf.Max(lowEnd, across.magnitude);
                else if (along > 0.9f) highEnd = Mathf.Max(highEnd, across.magnitude);
            }

            var nose = Vector3.zero;
            nose[axis] = highEnd < lowEnd ? 1f : -1f;
            return Quaternion.FromToRotation(nose, Vector3.forward);
        }

        private const string RangeSounds = "Assets/Sounds/ShootingRange";
        private const string SurfaceFolder = "Assets/Physics/ShootingRange";

        /// Overwrites the clip lists, on purpose. Clips are found by file prefix, so the audio script
        /// can add or drop variants without this needing to change
        private static void WireTheRangeAudio()
        {
            var signHits = Clips(RangeSounds + "/World", "sign_hit_0");
            var shatters = Clips(RangeSounds + "/World", "sign_shatter_0");
            if (signHits.Count == 0) { Fail("No sign_hit clips. Run tools/range-audio-halloween.sh, click into Unity, run again"); return; }

            WireTheSignVoices(signHits);

            var pool = Object.FindObjectOfType<ShatterPool>();
            if (pool != null)
            {
                new Fields(pool).SetArray("shatterClips", shatters).Apply();
                Dirty();
                Did("Shatter Pool: " + shatters.Count + " wooden break clips (the creature hiss is gone)");
            }
            else Say("No Shatter Pool in the open scene");

            WireTheSurfaces();
        }

        private static void WireTheSignVoices(List<Object> signHits)
        {
            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var popUp = contents.GetComponent<PopUpTarget>();
                var figure = popUp != null ? new Fields(popUp).Ref("figure") as Transform : null;
                if (figure == null) { Fail("Target prefab has no Pop Up Target figure. Run step 3 first"); return; }

                new Fields(popUp).SetArray("hitClips", signHits).Apply();
                Did("Every sign: " + signHits.Count + " wooden hit clips");

                for (int i = 0; i < figure.childCount; i++)
                {
                    var sign = figure.GetChild(i);
                    string prefix = VoiceFor(sign);
                    if (prefix == null) { Say(sign.name + ": no voice matches its model, left silent"); continue; }

                    var clips = Clips(RangeSounds + "/Voices", prefix);
                    var voice = RangeSetupUtil.Ensure<SignVoice>(sign.gameObject);
                    new Fields(voice).SetArray("clips", clips).Apply();
                    Did(sign.name + ": " + clips.Count + " " + prefix.TrimEnd('0', '_') + " clips");
                }

                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        /// By the model file, not the object name, like the shard pairing. Vampire3 is the woman
        private static string VoiceFor(Transform sign)
        {
            var filter = sign.GetComponentInChildren<MeshFilter>(true);
            if (filter == null || filter.sharedMesh == null) return null;

            string file = System.IO.Path.GetFileNameWithoutExtension(
                AssetDatabase.GetAssetPath(filter.sharedMesh)).ToLowerInvariant();

            if (file.Contains("vampire3")) return "vampire_female_0";
            if (file.Contains("vampire") || file.Contains("vimpire")) return "vampire_0";
            if (file.Contains("bat")) return "bat_0";
            if (file.Contains("coffin")) return "coffin_0";
            if (file.Contains("hand")) return "zombie_hand_0";
            return null;
        }

        private static void WireTheSurfaces()
        {
            var impact = Object.FindObjectOfType<ImpactFX>();
            if (impact == null) { Say("No Impact FX in the open scene"); return; }

            string[] names = { "Wood", "Metal", "Stone" };
            var so = new SerializedObject(impact);
            var surfaces = so.FindProperty("surfaces");
            surfaces.arraySize = names.Length;

            var materials = new Dictionary<string, PhysicMaterial>();
            for (int i = 0; i < names.Length; i++)
            {
                var material = Surface(names[i]);
                materials[names[i]] = material;

                var clips = Clips(RangeSounds + "/World", "env_" + names[i].ToLowerInvariant() + "_0");
                var element = surfaces.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("material").objectReferenceValue = material;
                SetClips(element.FindPropertyRelative("clips"), clips);
                Did("Impact FX: " + names[i] + " -> " + clips.Count + " clips");
            }

            var stone = Clips(RangeSounds + "/World", "env_stone_0");
            SetClips(so.FindProperty("impactClips"), stone);
            so.ApplyModifiedPropertiesWithoutUndo();
            Dirty();
            Did("Impact FX: anything without a surface sounds like stone");

            TagProp("Assets/Prefabs/ShootingRange/Prop_Crate.prefab", materials["Wood"]);
            TagProp("Assets/Prefabs/ShootingRange/Prop_Can.prefab", materials["Metal"]);
            TagProp("Assets/Prefabs/ShootingRange/Target_Plate.prefab", materials["Metal"]);

            ListTheUntagged();
        }

        private static PhysicMaterial Surface(string name)
        {
            if (!AssetDatabase.IsValidFolder(SurfaceFolder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Physics")) AssetDatabase.CreateFolder("Assets", "Physics");
                AssetDatabase.CreateFolder("Assets/Physics", "ShootingRange");
            }

            string path = SurfaceFolder + "/SR_" + name + ".physicMaterial";
            var material = RangeSetupUtil.Load<PhysicMaterial>(path);
            if (material != null) return material;

            // Unity's own defaults, so tagging a collider changes its sound and nothing about how it slides
            material = new PhysicMaterial("SR_" + name)
            {
                dynamicFriction = 0.6f,
                staticFriction = 0.6f,
                bounciness = 0f,
            };
            AssetDatabase.CreateAsset(material, path);
            Did("Made " + path);
            return material;
        }

        private static void TagProp(string path, PhysicMaterial material)
        {
            if (RangeSetupUtil.Load<GameObject>(path) == null) { Say("No prop at " + path); return; }

            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int tagged = 0;
                foreach (var collider in contents.GetComponentsInChildren<Collider>(true))
                {
                    collider.sharedMaterial = material;
                    tagged++;
                }

                PrefabUtility.SaveAsPrefabAsset(contents, path);
                Did(System.IO.Path.GetFileNameWithoutExtension(path) + ": " + tagged + " colliders -> " + material.name);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        /// The level is Alex's, so its colliders are tagged by hand; this only says which are left
        private static void ListTheUntagged()
        {
            var untagged = new List<string>();
            foreach (var collider in Object.FindObjectsOfType<Collider>())
            {
                if (collider.isTrigger || collider.sharedMaterial != null) continue;
                if (collider.GetComponentInParent<TrackMover>() != null || collider.GetComponentInParent<Pistol>() != null) continue;
                untagged.Add(collider.name);
            }

            untagged.Sort();
            if (untagged.Count == 0) { Did("Every environment collider has a surface"); return; }

            Say(untagged.Count + " environment colliders have no surface yet and will sound like stone: " +
                string.Join(", ", untagged.GetRange(0, Mathf.Min(30, untagged.Count)).ToArray()) +
                (untagged.Count > 30 ? ", ..." : ""));
        }

        private static List<Object> Clips(string folder, string prefix)
        {
            var clips = new List<Object>();
            if (!AssetDatabase.IsValidFolder(folder)) return clips;

            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!System.IO.Path.GetFileName(path).StartsWith(prefix)) continue;
                clips.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(path));
            }

            clips.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return clips;
        }

        private static void SetClips(SerializedProperty array, List<Object> clips)
        {
            array.arraySize = clips.Count;
            for (int i = 0; i < clips.Count; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
        }

        private const string PistolFolder = "Assets/Models/pistol/";
        private const string PistolModelPath = PistolFolder + "pistol_low.fbx";
        private const string PistolPivotName = "PistolModel";
        private const float PistolLength = 0.19f;

        /// Fits the artist's pistol once, then leaves its placement alone: re-running only refreshes the
        /// material, the slide and the glow, so nudging it by hand afterwards is safe
        private static void FitThePistolModel()
        {
            var importer = AssetImporter.GetAtPath(PistolModelPath) as ModelImporter;
            if (importer == null) { Fail("No pistol at " + PistolModelPath); return; }
            if (importer.importAnimation || importer.importMaterials)
            {
                importer.importAnimation = false;
                importer.importMaterials = false;
                importer.SaveAndReimport();
                Did("pistol_low.fbx: no animation, no imported materials");
            }

            var model = RangeSetupUtil.Load<GameObject>(PistolModelPath);
            var colour = PistolTexture("lambert3_Base_color.png", false);
            var normal = PistolTexture("lambert3_Normal_OpenGL.png", true);
            if (model == null || colour == null || normal == null) return;

            var shader = Shader.Find("Mobile/Bumped Diffuse");
            var material = RangeSetupUtil.Load<Material>("Assets/Materials/SR_Pistol.mat");
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, "Assets/Materials/SR_Pistol.mat");
            }
            material.shader = shader;
            material.mainTexture = colour;
            material.SetTexture("_BumpMap", normal);
            EditorUtility.SetDirty(material);
            Did("SR_Pistol.mat: Mobile/Bumped Diffuse, the artist's colour and OpenGL normal map");

            foreach (var pistol in Object.FindObjectsOfType<Pistol>())
            {
                var fields = new Fields(pistol);
                var pivot = RangeSetupUtil.Child(pistol.transform, PistolPivotName);
                bool fresh = false;
                if (pivot == null)
                {
                    pivot = new GameObject(PistolPivotName).transform;
                    pivot.SetParent(pistol.transform, false);
                    fresh = true;
                }

                var instance = pivot.childCount > 0 ? pivot.GetChild(0) : null;
                if (instance == null)
                {
                    instance = ((GameObject)PrefabUtility.InstantiatePrefab(model)).transform;
                    instance.SetParent(pivot, false);
                    fresh = true;
                }

                foreach (var part in instance.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = pistol.gameObject.layer;
                foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (renderer.name == GlowName) continue;
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }

                if (fresh) FitInTheHand(pistol, fields, instance);
                else Say(pistol.name + ": the pistol was already fitted, so your placement was kept");

                var old = fields.Ref("visual") as Transform;
                if (old != null && old != pivot)
                {
                    old.gameObject.SetActive(false);
                    Did(pistol.name + ": the greybox '" + old.name + "' is hidden, not deleted");
                }

                var slide = FindPart(instance, "top");
                fields.Set("visual", pivot).Set("slide", slide).Apply();
                Dirty();
                Did(pistol.name + ": recoil turns " + PistolPivotName + " about the hand; slide = " +
                    (slide != null ? slide.name : "none found"));
            }

            BuildThePowerUpGlow();
        }

        private static Texture2D PistolTexture(string file, bool isNormal)
        {
            string path = PistolFolder + file;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { Fail("No texture at " + path); return null; }

            var type = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (importer.textureType != type || importer.maxTextureSize != 1024)
            {
                importer.textureType = type;
                importer.maxTextureSize = 1024;
                importer.SaveAndReimport();
            }

            return RangeSetupUtil.Load<Texture2D>(path);
        }

        /// Measured, not assumed: the barrel is the long axis and points away from the magazine, the
        /// slide is up, and the magazine (the grip) sits on the hand point
        private static void FitInTheHand(Pistol pistol, Fields fields, Transform instance)
        {
            instance.localPosition = Vector3.zero;
            instance.localRotation = Quaternion.identity;
            instance.localScale = Vector3.one;

            Bounds all, grip, barrel, top;
            if (!PartBounds(instance, null, out all) || !PartBounds(instance, "magazine", out grip) ||
                !PartBounds(instance, "barrel", out barrel) || !PartBounds(instance, "top", out top))
            {
                Fail(pistol.name + ": the pistol is missing its barrel, magazine or top part, so it was not fitted");
                return;
            }

            var size = all.size;
            int along = size.x >= size.y && size.x >= size.z ? 0 : (size.y >= size.z ? 1 : 2);
            int upAxis = -1;
            for (int i = 0; i < 3; i++)
                if (i != along && (upAxis < 0 || size[i] > size[upAxis])) upAxis = i;

            var forward = Vector3.zero;
            forward[along] = barrel.center[along] >= grip.center[along] ? 1f : -1f;
            var up = Vector3.zero;
            up[upAxis] = top.center[upAxis] >= grip.center[upAxis] ? 1f : -1f;

            var turn = Quaternion.Inverse(Quaternion.LookRotation(forward, up));
            float scale = PistolLength / Mathf.Max(1e-5f, size[along]);

            instance.localRotation = turn;
            instance.localScale = Vector3.one * scale;
            instance.localPosition = -(turn * (grip.center * scale));

            var tip = barrel.center + forward * barrel.extents[along];
            var muzzle = fields.Ref("muzzle") as Transform;
            if (muzzle != null)
            {
                var inPistol = instance.localPosition + turn * (tip * scale) + Vector3.forward * 0.005f;
                muzzle.position = instance.parent.TransformPoint(inPistol);
                muzzle.rotation = pistol.transform.rotation;
            }

            Did(pistol.name + ": fitted at " + (PistolLength * 100f).ToString("0") + " cm (x" + scale.ToString("0.0000") +
                "), barrel along the aim, grip on the hand point, Muzzle moved to the barrel tip");
        }

        private static bool PartBounds(Transform root, string part, out Bounds bounds)
        {
            bounds = new Bounds();
            bool first = true;

            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.name == GlowName) continue;
                if (part != null && !filter.name.ToLowerInvariant().Contains(part)) continue;

                var box = filter.sharedMesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    var point = new Vector3(
                        (corner & 1) == 0 ? box.min.x : box.max.x,
                        (corner & 2) == 0 ? box.min.y : box.max.y,
                        (corner & 4) == 0 ? box.min.z : box.max.z);
                    point = root.InverseTransformPoint(filter.transform.TransformPoint(point));

                    if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                    else bounds.Encapsulate(point);
                }
            }

            return !first;
        }

        private static Transform FindPart(Transform root, string part)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child.name.ToLowerInvariant().Contains(part) && child.GetComponent<MeshFilter>() != null) return child;
            return null;
        }

        private const string GlowName = "PowerUpGlow";

        /// The gun keeps its own textures now; a glowing shell round the gun and the glove shows the
        /// power-up instead. Run again whenever the gun or glove model changes
        private static void BuildThePowerUpGlow()
        {
            var shader = Shader.Find("CrystalCatch/Power-Up Glow");
            if (shader == null) { Fail("The Power-Up Glow shader has not compiled. Check the console for shader errors"); return; }

            var material = RangeSetupUtil.Load<Material>("Assets/Materials/SR_PowerUpGlow.mat");
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, "Assets/Materials/SR_PowerUpGlow.mat");
                Did("Made SR_PowerUpGlow.mat");
            }
            material.shader = shader;
            EditorUtility.SetDirty(material);

            foreach (var pistol in Object.FindObjectsOfType<Pistol>())
            {
                var tint = pistol.GetComponent<PowerUpTint>();
                if (tint == null) { Say(pistol.name + " has no Power Up Tint, so nothing would switch a glow on"); continue; }

                var sources = new List<Renderer>();
                var visual = new Fields(pistol).Ref("visual") as Transform;
                if (visual != null)
                {
                    foreach (var mesh in visual.GetComponentsInChildren<MeshRenderer>(true))
                        if (mesh.name != GlowName) sources.Add(mesh);
                }
                foreach (var glove in pistol.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (glove.name != GlowName) sources.Add(glove);

                var shells = new List<Object>();
                foreach (var source in sources)
                {
                    var shell = Shell(source, material);
                    if (shell != null) shells.Add(shell);
                }

                var tintFields = new Fields(tint);
                var kept = new List<Object>();
                var tinted = tintFields.Find("renderers");
                for (int i = 0; i < tinted.arraySize; i++)
                {
                    var renderer = tinted.GetArrayElementAtIndex(i).objectReferenceValue as Renderer;
                    if (renderer != null && !sources.Contains(renderer)) kept.Add(renderer);
                }

                tintFields.SetArray("renderers", kept).SetArray("glowShells", shells).Apply();
                Dirty();
                Did(pistol.name + ": " + shells.Count + " glow shells (gun + glove); the tint now only colours " +
                    kept.Count + " laser parts");
            }
        }

        private static Renderer Shell(Renderer source, Material material)
        {
            var shell = RangeSetupUtil.Child(source.transform, GlowName);
            if (shell == null)
            {
                shell = new GameObject(GlowName).transform;
                shell.SetParent(source.transform, false);
            }
            shell.localPosition = Vector3.zero;
            shell.localRotation = Quaternion.identity;
            shell.localScale = Vector3.one;
            shell.gameObject.layer = source.gameObject.layer;

            Renderer renderer;
            var skinned = source as SkinnedMeshRenderer;
            if (skinned != null)
            {
                var copy = RangeSetupUtil.Ensure<SkinnedMeshRenderer>(shell.gameObject);
                copy.sharedMesh = skinned.sharedMesh;
                copy.bones = skinned.bones;
                copy.rootBone = skinned.rootBone;
                copy.localBounds = skinned.localBounds;
                renderer = copy;
            }
            else
            {
                var sourceFilter = source.GetComponent<MeshFilter>();
                if (sourceFilter == null || sourceFilter.sharedMesh == null) return null;

                var filter = RangeSetupUtil.Ensure<MeshFilter>(shell.gameObject);
                filter.sharedMesh = sourceFilter.sharedMesh;
                renderer = RangeSetupUtil.Ensure<MeshRenderer>(shell.gameObject);
            }

            var materials = new Material[Mathf.Max(1, source.sharedMaterials.Length)];
            for (int i = 0; i < materials.Length; i++) materials[i] = material;
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            return renderer;
        }

        /// Flash and sparks take the power-up colour; the wisp is smoke and stays grey
        private static void WireTheGunTint()
        {
            foreach (var pistol in Object.FindObjectsOfType<Pistol>())
            {
                var fields = new Fields(pistol);
                var root = fields.Ref("muzzleFlash") as ParticleSystem;
                if (root == null) { Say(pistol.name + " has no muzzle flash, so nothing to tint"); continue; }

                var flash = RangeSetupUtil.Child(root.transform, "Flash");
                var tinted = new List<Object> { root };
                if (flash != null) tinted.Add(flash.GetComponent<ParticleSystem>());
                else Say(pistol.name + ": the scene has not caught up with the new Flash child yet. Run this step once more");

                fields.SetArray("tintedByPowerUp", tinted).Apply();
                Dirty();
                Did(pistol.name + ": " + tinted.Count + " muzzle systems take the power-up colour");
            }
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

        private static readonly string[] FingerNames = { "thumb", "index", "middle", "ring", "pinky" };
        private static readonly float[] GhostGrip = { 10f, 12f, 18f, 20f, 22f };

        /// Replaces 7b, whose curl axis was a guess. Measures each joint's real hinge from the rig, curls
        /// each finger until it meets the grip, and places the palm on the grip the first time only.
        /// Ghost hands get the same rig at a relaxed curl and idle on their own
        private static void FitTheGloves()
        {
            foreach (var pistol in Object.FindObjectsOfType<Pistol>())
            {
                var pistolFields = new Fields(pistol);
                var pivot = pistolFields.Ref("visual") as Transform;
                Transform glove = null;
                foreach (var child in pistol.GetComponentsInChildren<Transform>(true))
                    if (child.name.ToLowerInvariant().Contains("glove")) { glove = child; break; }
                if (glove == null) { Say(pistol.name + " has no glove under it"); continue; }

                Bounds grip;
                Transform magazine = pivot != null ? FindPart(pivot, "magazine") : null;
                if (magazine == null || !GripBox(pistol, magazine, out grip))
                {
                    Fail(pistol.name + ": no fitted pistol model with a magazine part. Run step 15 first");
                    continue;
                }

                // The gun turns about PistolModel for recoil and the reload tilt; the hand has to turn with it
                if (glove.parent != pivot)
                {
                    glove.SetParent(pivot, true);
                    Did(pistol.name + ": " + glove.name + " now rides " + pivot.name + ", so it moves with the gun");
                }

                var rig = GloveRig.Read(glove);
                if (rig == null) { Fail(glove.name + ": not a SteamVR glove rig (no wrist or finger bones)"); continue; }
                rig.ToRest();

                var fingers = glove.GetComponent<GloveFingers>();
                bool fresh = fingers == null;
                bool rightHand = HandOf(pistolFields) == "RightHand";
                bool lost = OffTheGrip(pistol, rig, grip);
                if (fresh || lost) PalmOnTheGrip(pistol, glove, rig, grip, rightHand);
                else Say(glove.name + ": already on the grip, so its placement was kept");

                var joints = rig.Hinges();
                var curls = FitCurls(pistol, rig, joints, grip, rightHand);
                rig.ToRest();

                fingers = RangeSetupUtil.Ensure<GloveFingers>(glove.gameObject);
                fresh |= lost;
                if (fresh) fingers.SetUp(joints, pistol, curls);
                else fingers.SetUp(joints, pistol, CurrentGrip(fingers));
                EditorUtility.SetDirty(fingers);
                Dirty();
                Did(glove.name + ": " + joints.Length + " joints hinged toward the palm; grip thumb/index/middle/ring/pinky = " +
                    string.Join(" / ", System.Array.ConvertAll(fresh ? curls : CurrentGrip(fingers), c => c.ToString("0"))) +
                    (fresh ? "" : " (yours, kept)"));
            }

            var visuals = Object.FindObjectOfType<HandVisuals>();
            if (visuals == null) return;

            var hands = new Fields(visuals).Find("hands");
            for (int i = 0; i < hands.arraySize; i++)
            {
                var ghost = hands.GetArrayElementAtIndex(i).FindPropertyRelative("ghost").objectReferenceValue as Transform;
                if (ghost == null) continue;

                var rig = GloveRig.Read(ghost);
                if (rig == null) { Say(ghost.name + ": not a SteamVR glove rig, left still"); continue; }
                rig.ToRest();

                var fingers = RangeSetupUtil.Ensure<GloveFingers>(ghost.gameObject);
                bool fresh = !HasJoints(fingers);
                fingers.SetUp(rig.Hinges(), null, fresh ? GhostGrip : CurrentGrip(fingers));
                EditorUtility.SetDirty(fingers);
                Dirty();
                Did(ghost.name + ": idles with a relaxed curl and a slow sway");
            }
        }

        /// More than 15 cm from the grip is not a hand on the gun, whatever placed it there
        private static bool OffTheGrip(Pistol pistol, GloveRig rig, Bounds grip)
        {
            var centre = FromGripSpace(grip.center, GripAxes(pistol));
            return Vector3.Distance(rig.Knuckles, centre) > 0.15f;
        }

        private static bool HasJoints(GloveFingers fingers)
        {
            return new Fields(fingers).Find("joints").arraySize > 0;
        }

        private static float[] CurrentGrip(GloveFingers fingers)
        {
            var fields = new Fields(fingers);
            return new[] { fields.Float("thumb"), fields.Float("index"), fields.Float("middle"), fields.Float("ring"), fields.Float("pinky") };
        }

        private static string HandOf(Fields pistol)
        {
            var hand = pistol.Find("hand");
            return hand.enumNames[hand.enumValueIndex];
        }

        /// The grip in world space, as a box on the gun's own axes: right, forward along the grip face,
        /// and up the grip. Padded for the frame round the magazine
        private static bool GripBox(Pistol pistol, Transform magazine, out Bounds box)
        {
            box = new Bounds();
            var filter = magazine.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return false;

            var local = filter.sharedMesh.bounds;
            var axes = GripAxes(pistol);
            bool first = true;
            for (int corner = 0; corner < 8; corner++)
            {
                var world = magazine.TransformPoint(new Vector3(
                    (corner & 1) == 0 ? local.min.x : local.max.x,
                    (corner & 2) == 0 ? local.min.y : local.max.y,
                    (corner & 4) == 0 ? local.min.z : local.max.z));
                var onAxes = new Vector3(Vector3.Dot(world, axes[0]), Vector3.Dot(world, axes[1]), Vector3.Dot(world, axes[2]));
                if (first) { box = new Bounds(onAxes, Vector3.zero); first = false; }
                else box.Encapsulate(onAxes);
            }

            box.Expand(new Vector3(0.012f, 0.012f, 0f));
            return true;
        }

        /// The gun's own axes. Not the magazine's longest side: this magazine is 7.8 cm tall and 8.0 cm
        /// deep, so "longest" picked the depth, and every glove was fitted to the world origin
        private static Vector3[] GripAxes(Pistol pistol)
        {
            var gun = pistol.transform;
            return new[] { gun.right, gun.forward, gun.up };
        }

        private static Vector3 FromGripSpace(Vector3 point, Vector3[] axes)
        {
            return axes[0] * point.x + axes[1] * point.y + axes[2] * point.z;
        }

        /// Palm flat on the grip's side panel, fingers round the front strap, the index knuckle just
        /// under the trigger guard. A starting point for tuning by eye, not a final pose
        private static void PalmOnTheGrip(Pistol pistol, Transform glove, GloveRig rig, Bounds grip, bool rightHand)
        {
            var axes = GripAxes(pistol);
            float side = rightHand ? 1f : -1f;

            var palmTarget = -axes[0] * side;
            var fingersTarget = axes[1];
            var turn = Quaternion.LookRotation(fingersTarget, palmTarget) * Quaternion.Inverse(Quaternion.LookRotation(rig.Along, rig.Palm));
            glove.rotation = turn * glove.rotation;

            var knuckles = new Vector3(
                grip.center.x + side * (grip.extents.x + 0.012f),
                grip.center.y + grip.extents.y,
                grip.max.z - 0.03f);
            glove.position += FromGripSpace(knuckles, axes) - rig.Knuckles;

            Did(glove.name + ": palm on the " + (rightHand ? "right" : "left") + " of the grip, knuckles at the front strap");
        }

        /// Each finger closes until its tip reaches the grip. The index aims for the trigger, just ahead
        /// of the grip and up under the guard
        private static float[] FitCurls(Pistol pistol, GloveRig rig, GloveFingers.Joint[] joints, Bounds grip, bool rightHand)
        {
            var axes = GripAxes(pistol);
            var trigger = FromGripSpace(new Vector3(grip.center.x, grip.max.y + 0.02f, grip.max.z - 0.012f), axes);

            var curls = new float[5];
            for (int f = 0; f < 5; f++)
            {
                var finger = (GloveFingers.Finger)f;
                var tip = rig.Tip(f);
                if (tip == null) { curls[f] = GhostGrip[f]; continue; }

                // A thumb past ~45° hooks under the gun and an index past ~60° balls into the fist
                float limit = finger == GloveFingers.Finger.Thumb ? 45f : finger == GloveFingers.Finger.Index ? 60f : 90f;
                float best = finger == GloveFingers.Finger.Thumb ? 30f : finger == GloveFingers.Finger.Index ? 35f : 70f;
                float bestDistance = float.MaxValue;
                for (float angle = 0f; angle <= limit; angle += 2f)
                {
                    foreach (var joint in joints)
                        if (joint.finger == finger) joint.bone.localRotation = joint.rest * Quaternion.AngleAxis(angle, joint.axis);

                    var at = tip.position;
                    if (finger == GloveFingers.Finger.Index)
                    {
                        float distance = Vector3.Distance(at, trigger);
                        if (distance < bestDistance) { bestDistance = distance; best = angle; }
                        continue;
                    }

                    // Wrapped, not just touching: the tip has come round the front strap to the far half of
                    // the grip. Stopping at first contact left every fingertip sticking out of the far side
                    var inGrip = new Vector3(Vector3.Dot(at, axes[0]), Vector3.Dot(at, axes[1]), Vector3.Dot(at, axes[2]));
                    var reach = grip;
                    reach.Expand(0.016f);
                    bool farHalf = (rightHand ? 1f : -1f) * (inGrip.x - grip.center.x) < 0f;
                    if (reach.Contains(inGrip) && farHalf) { best = angle; break; }
                }

                curls[f] = best;
                foreach (var joint in joints)
                    if (joint.finger == finger) joint.bone.localRotation = joint.rest;
            }

            return curls;
        }

        /// A SteamVR glove's bones, read by name, at the model's own rest pose
        private class GloveRig
        {
            public Transform Wrist;
            public readonly Transform[][] Chains = new Transform[5][];
            public readonly Transform[] Ends = new Transform[5];
            public readonly Quaternion[][] Rest = new Quaternion[5][];

            public static GloveRig Read(Transform glove)
            {
                var rig = new GloveRig();
                var all = glove.GetComponentsInChildren<Transform>(true);
                foreach (var bone in all)
                    if (bone.name.StartsWith("wrist_")) rig.Wrist = bone;
                if (rig.Wrist == null) return null;

                for (int f = 0; f < 5; f++)
                {
                    var chain = new List<Transform>();
                    for (int j = 0; j < 3; j++)
                        foreach (var bone in all)
                            if (bone.name.StartsWith("finger_" + FingerNames[f] + "_" + j + "_")) chain.Add(bone);
                    foreach (var bone in all)
                        if (bone.name.StartsWith("finger_" + FingerNames[f] + "_") && bone.name.EndsWith("_end")) rig.Ends[f] = bone;

                    rig.Chains[f] = chain.ToArray();
                    rig.Rest[f] = new Quaternion[chain.Count];
                    for (int j = 0; j < chain.Count; j++)
                    {
                        var source = PrefabUtility.GetCorrespondingObjectFromSource(chain[j]) as Transform;
                        rig.Rest[f][j] = source != null ? source.localRotation : chain[j].localRotation;
                    }
                }

                return rig.Chains[1].Length > 0 && rig.Chains[4].Length > 0 ? rig : null;
            }

            public void ToRest()
            {
                for (int f = 0; f < 5; f++)
                    for (int j = 0; j < Chains[f].Length; j++) Chains[f][j].localRotation = Rest[f][j];
            }

            public Transform Tip(int finger) { return Ends[finger]; }

            public Vector3 Knuckles
            {
                get
                {
                    var sum = Vector3.zero;
                    for (int f = 1; f < 5; f++) sum += Chains[f][0].position;
                    return sum / 4f;
                }
            }

            public Vector3 Along { get { return (Knuckles - Wrist.position).normalized; } }

            /// The side the fingers close toward. Measured, not assumed: the cross product's sign flips
            /// between the two hands, and the thumb always sits on the palm side
            public Vector3 Palm
            {
                get
                {
                    var across = (Chains[4][0].position - Chains[1][0].position).normalized;
                    var normal = Vector3.Cross(Along, across).normalized;
                    var middle = (Wrist.position + Knuckles) * 0.5f;
                    return Ends[0] != null && Vector3.Dot(Ends[0].position - middle, normal) < 0f ? -normal : normal;
                }
            }

            /// Each joint hinges across its own bone and the palm; the sign is whichever way brings the
            /// fingertip toward the palm, tested by actually turning the joint
            public GloveFingers.Joint[] Hinges()
            {
                var palm = Palm;
                var joints = new List<GloveFingers.Joint>();
                for (int f = 0; f < 5; f++)
                {
                    for (int j = 0; j < Chains[f].Length; j++)
                    {
                        var bone = Chains[f][j];
                        var next = j + 1 < Chains[f].Length ? Chains[f][j + 1] : Ends[f];
                        if (next == null) continue;

                        var world = Vector3.Cross(next.position - bone.position, palm).normalized;
                        var tip = Ends[f] != null ? Ends[f] : next;
                        var before = tip.position;
                        var rest = bone.rotation;
                        bone.rotation = Quaternion.AngleAxis(10f, world) * rest;
                        bool towardPalm = Vector3.Dot(tip.position - before, palm) > 0f;
                        bone.rotation = rest;
                        if (!towardPalm) world = -world;

                        joints.Add(new GloveFingers.Joint
                        {
                            bone = bone,
                            finger = (GloveFingers.Finger)f,
                            rest = Rest[f][j],
                            axis = Quaternion.Inverse(rest) * world,
                        });
                    }
                }

                return joints.ToArray();
            }
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
