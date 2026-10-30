using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    /// Steps 22 to 26: side signs that open off their pillars, the reload prompt, the controls card, the
    /// spider's sounds and bullet holes on the room
    public static partial class RangeSetup
    {
        [MenuItem("Shooting Range/Setup/22 - Fit The Side Signs", false, 42)]
        public static void SideSignsMenu() { Begin("Side signs"); FitTheSideSigns(); End(); }

        [MenuItem("Shooting Range/Setup/23 - Add The Reload Prompt", false, 43)]
        public static void ReloadPromptMenu() { Begin("Reload prompt"); AddTheReloadPrompt(); End(); }

        [MenuItem("Shooting Range/Setup/24 - Build The Controls Card", false, 44)]
        public static void ControlsCardMenu() { Begin("Controls card"); BuildTheControlsCard(false); End(); }

        [MenuItem("Shooting Range/Setup/24b - Rebuild The Controls Card", false, 45)]
        public static void ControlsCardRebuildMenu()
        {
            if (!EditorUtility.DisplayDialog("Rebuild the controls card",
                    "This deletes the ControlsCard under the announcer and builds it again from the art, so any " +
                    "hand changes to it are lost. Commit first.", "Rebuild", "Cancel"))
                return;

            Begin("Controls card, rebuilt");
            BuildTheControlsCard(true);
            End();
        }

        [MenuItem("Shooting Range/Setup/25 - Wire The Spider Sounds", false, 46)]
        public static void SpiderSoundsMenu() { Begin("Spider sounds"); WireTheSpiderSounds(); End(); }

        [MenuItem("Shooting Range/Setup/26 - Add The Shot Decals", false, 47)]
        public static void ShotDecalsMenu() { Begin("Shot decals"); AddTheShotDecals(); End(); }

        // 22, side signs

        private const float SideSignTallest = 0.6f;
        private const float CeilingClearance = 0.05f;
        private const float LargestMountPart = 2.5f;
        private const float PanelReach = 0.1f;

        /// Only short signs ride the pillars, each folds flat against its own pillar and any panel on it,
        /// and a rail that would carry even those into the ceiling is cut short at the top. Fills blanks:
        /// a mount or a tallest sign already set is left alone
        private static void FitTheSideSigns()
        {
            var heights = SignHeights();
            float ceiling = CeilingBottom();
            int fitted = 0;

            foreach (var rail in Object.FindObjectsOfType<TrackRail>())
            {
                if (rail.Hinge != Hinge.Sideways) continue;
                fitted++;

                var fields = new Fields(rail);
                if (fields.Float("tallestSign") <= 0f) fields.Set("tallestSign", SideSignTallest);

                if (fields.Find("mount").arraySize == 0)
                {
                    var parts = FindMount(rail);
                    if (parts.Count > 0) fields.SetArray("mount", parts);
                    else Fail(rail.name + ": no pillar found around it. Drag the pillar into its Mount by hand");
                }
                fields.Apply();

                Bounds mount;
                if (rail.Mounted(out mount)) Did(rail.name + " folds against " + MountNames(rail) + OverhangNote(rail));

                float tallest = TallestUnder(heights, rail.TallestSign);
                if (tallest <= 0f) { Fail(rail.name + ": no sign is " + rail.TallestSign.ToString("0.00") + " m or shorter"); continue; }

                Did(rail.name + ": signs up to " + rail.TallestSign.ToString("0.00") + " m (" + NamesUnder(heights, rail.TallestSign) + ")");
                LowerTheTop(rail, ceiling - CeilingClearance - rail.ShownOffset - tallest);
            }

            if (fitted == 0) Say("No sideways rails in the scene");
            Dirty();
        }

        /// Every sign under the prefab's Figure, by name, in metres tall
        private static Dictionary<string, float> SignHeights()
        {
            var heights = new Dictionary<string, float>();
            var figure = PrefabFigure();
            if (figure == null) return heights;

            try
            {
                for (int i = 0; i < figure.childCount; i++)
                {
                    Bounds bounds;
                    var sign = figure.GetChild(i);
                    if (RangeSetupUtil.MeshBoundsIn(null, sign, out bounds)) heights[sign.name] = bounds.size.y;
                }
            }
            finally { ClosePrefab(false); }

            return heights;
        }

        private static float TallestUnder(Dictionary<string, float> heights, float limit)
        {
            float tallest = 0f;
            foreach (var pair in heights)
            {
                if (pair.Value <= limit) tallest = Mathf.Max(tallest, pair.Value);
            }

            return tallest;
        }

        private static string NamesUnder(Dictionary<string, float> heights, float limit)
        {
            var names = new List<string>();
            foreach (var pair in heights)
            {
                if (pair.Value <= limit) names.Add(pair.Key + " " + pair.Value.ToString("0.00"));
            }

            return string.Join(", ", names.ToArray());
        }

        /// Takes the rail's top end down to top, by moving its origin when it runs downward and by
        /// shortening it either way
        private static void LowerTheTop(TrackRail rail, float top)
        {
            float start = rail.PointAt(0f).y;
            float end = rail.PointAt(rail.Length).y;
            float over = Mathf.Max(start, end) - top;
            if (over <= 0.005f)
            {
                Say(rail.name + ": its top, " + Mathf.Max(start, end).ToString("0.00") + " m, already clears the ceiling");
                return;
            }

            if (over >= rail.Length - 0.3f) { Fail(rail.name + ": would have to lose " + over.ToString("0.00") + " m, which leaves no rail"); return; }

            if (start > end) rail.transform.position += rail.Direction * over;
            new Fields(rail).Set("length", rail.Length - over).Apply();
            Did(rail.name + ": top lowered " + over.ToString("0.00") + " m to " + top.ToString("0.00") +
                " m, so the tallest sign it carries stops " + CeilingClearance.ToString("0.00") + " m under the ceiling");
        }

        /// The pillar the rail runs up, which is whatever stands around its middle the whole height of the
        /// rail, and any panel laid flat on its faces. Floors, walls and the ceiling are too big to count,
        /// and a lamp or a bracket on the pillar is too small to be a panel
        private static List<Object> FindMount(TrackRail rail)
        {
            var parts = new List<Object>();
            Vector3 middle = rail.PointAt(rail.Length * 0.5f);
            float low = Mathf.Min(rail.PointAt(0f).y, rail.PointAt(rail.Length).y);
            float high = Mathf.Max(rail.PointAt(0f).y, rail.PointAt(rail.Length).y);

            var candidates = new List<Renderer>();
            foreach (var renderer in Object.FindObjectsOfType<Renderer>())
            {
                if (!(renderer is MeshRenderer) || IsGameplay(renderer.transform)) continue;

                var box = renderer.bounds;
                if (box.size.x > LargestMountPart || box.size.z > LargestMountPart) continue;
                if (box.max.y < low || box.min.y > high) continue;
                candidates.Add(renderer);
            }

            Bounds pillar = new Bounds();
            bool found = false;
            foreach (var renderer in candidates)
            {
                var box = renderer.bounds;
                if (middle.x < box.min.x || middle.x > box.max.x || middle.z < box.min.z || middle.z > box.max.z) continue;
                if (box.min.y > low + 0.3f || box.max.y < high - 0.3f) continue;

                parts.Add(renderer);
                if (found) pillar.Encapsulate(box);
                else { pillar = box; found = true; }
            }

            if (!found) return parts;

            var reach = new Bounds(pillar.center, pillar.size + new Vector3(PanelReach, 0f, PanelReach) * 2f);
            foreach (var renderer in candidates)
            {
                if (parts.Contains(renderer)) continue;

                var box = renderer.bounds;
                bool beside = box.max.x >= reach.min.x && box.min.x <= reach.max.x &&
                              box.max.z >= reach.min.z && box.min.z <= reach.max.z;
                bool flat = Mathf.Min(box.size.x, box.size.z) <= PanelReach;
                bool covers = box.size.y >= pillar.size.y * 0.5f;

                if (beside && flat && covers) parts.Add(renderer);
            }

            return parts;
        }

        private static bool IsGameplay(Transform item)
        {
            return item.GetComponentInParent<TrackRail>() != null || item.GetComponentInParent<TrackMover>() != null ||
                   item.GetComponentInParent<TrackDirector>() != null || item.GetComponentInParent<Canvas>() != null ||
                   item.GetComponentInParent<Pistol>() != null || item.GetComponentInParent<SpiderNest>() != null ||
                   item.GetComponentInParent<BatSwarm>() != null;
        }

        private static string MountNames(TrackRail rail)
        {
            var mount = new Fields(rail).Find("mount");
            var names = new List<string>();
            for (int i = 0; i < mount.arraySize; i++)
            {
                var part = mount.GetArrayElementAtIndex(i).objectReferenceValue;
                if (part != null) names.Add(part.name);
            }

            return string.Join(" + ", names.ToArray());
        }

        /// A panel wider than the pillar it sits on leaves the folded sign standing off the pillar's side
        /// by the overhang, which is worth knowing about
        private static string OverhangNote(TrackRail rail)
        {
            var mount = new Fields(rail).Find("mount");
            if (mount.arraySize < 2) return "";

            var first = mount.GetArrayElementAtIndex(0).objectReferenceValue as Renderer;
            Bounds all;
            if (first == null || !rail.Mounted(out all)) return "";

            float overhang = Mathf.Max(first.bounds.min.x - all.min.x, all.max.x - first.bounds.max.x,
                                       first.bounds.min.z - all.min.z, all.max.z - first.bounds.max.z);
            return overhang > 0.05f
                 ? ". NOTE: something on it overhangs " + first.name + " by " + overhang.ToString("0.00") +
                   " m, so a folded sign stands that far off the pillar's side"
                 : "";
        }

        // ---------------------------------------------------------------- 23, reload prompt

        private const string ReloadPromptName = "ReloadPrompt";
        private static readonly Color PromptRed = new Color(1f, 0.3f, 0.2f);

        /// Big in the middle of the announcer board, over the round's centre text, which is empty while
        /// a round is being played
        private static void AddTheReloadPrompt()
        {
            var centre = AnnouncerCentre();
            if (centre == null) return;

            if (RangeSetupUtil.Child(centre.transform.parent, ReloadPromptName) != null)
            {
                Say("There is already a " + ReloadPromptName + " on the announcer, left as it is");
                return;
            }

            var text = NewText(ReloadPromptName, centre.transform.parent, centre, centre.fontSize);
            var rect = text.rectTransform;
            var from = centre.rectTransform;
            rect.anchorMin = from.anchorMin;
            rect.anchorMax = from.anchorMax;
            rect.pivot = from.pivot;
            rect.anchoredPosition = from.anchoredPosition;
            rect.sizeDelta = from.sizeDelta;
            text.color = PromptRed;

            new Fields(text.gameObject.AddComponent<ReloadPrompt>()).Set("game", Object.FindObjectOfType<RangeGame>()).Apply();
            Did(ReloadPromptName + " on the announcer board: RELOAD with L GRIP, R GRIP or L + R GRIP under it, " +
                "pulsing while a gun is dry");
            Dirty();
        }

        private static TMP_Text AnnouncerCentre()
        {
            var announcer = Object.FindObjectOfType<RangeAnnouncer>();
            var centre = announcer != null ? new Fields(announcer).Ref("centerText") as TMP_Text : null;
            if (centre == null) Fail("No RangeAnnouncer with a Center Text in the scene");
            return centre;
        }

        /// Text in the announcer's own font, so everything on the board matches
        private static TextMeshProUGUI NewText(string name, Transform parent, TMP_Text style, float size)
        {
            var holder = new GameObject(name, typeof(RectTransform));
            holder.layer = parent.gameObject.layer;
            holder.transform.SetParent(parent, false);

            var text = holder.AddComponent<TextMeshProUGUI>();
            text.font = style.font;
            text.fontSharedMaterial = style.fontSharedMaterial;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            return text;
        }

        // ---------------------------------------------------------------- 24, controls card

        private const string CardName = "ControlsCard";
        private const string CardArt = "Assets/Textures/ControlsCard/";
        private static readonly Vector2 CardSize = new Vector2(720f, 500f);
        private const float ControllerSize = 200f;
        private const float ControllerX = 230f;
        private const float ControllerY = 0f;
        private const float BulbSize = 22f;
        private const float BulbGap = 46f;

        // Where the art puts things, as a fraction of its frame: tools/controls-card-art.py draws them there
        private static readonly Vector2 TriggerHinge = new Vector2(226f / 512f, 1f - 222f / 512f);
        private const float BorderInset = 48f / 1440f;
        private const float BorderTop = 200f / 1000f;
        private const float StripTop = 790f / 1000f;
        private const float StripHeight = 130f / 1000f;
        private const float StripInset = 120f / 1440f;

        private static readonly Color Gold = new Color(1f, 0.85f, 0.47f);
        private static readonly Color Cream = new Color(0.96f, 0.92f, 0.83f);
        private static readonly Color BrownInk = new Color(0.18f, 0.1f, 0.05f);

        /// The card on the announcer board, hidden until the intro. Built from the art and the
        /// announcer's font; ControlsCard does the moving
        private static void BuildTheControlsCard(bool rebuild)
        {
            var centre = AnnouncerCentre();
            if (centre == null) return;

            var board = centre.transform.parent;
            var existing = RangeSetupUtil.Child(board, CardName);
            if (existing != null)
            {
                if (!rebuild) { Say("There is already a " + CardName + " on the announcer, left as it is. 24b rebuilds it"); return; }
                Object.DestroyImmediate(existing.gameObject);
                Did("Old " + CardName + " removed");
            }

            var art = LoadCardArt();
            if (art == null) return;

            var card = NewRect(CardName, board, CardSize, Vector2.zero);
            var group = card.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            var content = NewRect("Content", card, CardSize, Vector2.zero);
            NewImage("Board", content, art["card_board"], CardSize, Vector2.zero);

            var bulbs = LayBulbs(content, art["card_bulb"]);

            var title = NewText("Title", content, centre, 52f);
            Place(title.rectTransform, new Vector2(CardSize.x - 120f, 60f), new Vector2(0f, 112f));
            title.text = "HOW TO PLAY";
            title.color = Gold;

            var triggers = new List<Object>();
            var grips = new List<Object>();
            var triggerGlows = new List<Object>();
            var gripGlows = new List<Object>();
            for (int side = 0; side < 2; side++)
            {
                bool left = side == 0;
                var holder = NewRect(left ? "LeftController" : "RightController", content,
                                     Vector2.one * ControllerSize, new Vector2(left ? -ControllerX : ControllerX, ControllerY));

                // The art is the right controller, front to the left; mirrored it is the left one, front
                // to the right, so both point in at the labels between them
                if (left) holder.localScale = new Vector3(-1f, 1f, 1f);

                NewImage("Body", holder, art["controller_body"], Vector2.one * ControllerSize, Vector2.zero);
                triggerGlows.Add(NewImage("TriggerGlow", holder, art["controller_trigger_glow"], Vector2.one * ControllerSize, Vector2.zero));
                gripGlows.Add(NewImage("GripGlow", holder, art["controller_grip_glow"], Vector2.one * ControllerSize, Vector2.zero));

                var trigger = NewImage("Trigger", holder, art["controller_trigger"], Vector2.one * ControllerSize, Vector2.zero).rectTransform;
                trigger.pivot = TriggerHinge;
                trigger.anchoredPosition = (TriggerHinge - Vector2.one * 0.5f) * ControllerSize;
                triggers.Add(trigger);

                grips.Add(NewImage("Grip", holder, art["controller_grip"], Vector2.one * ControllerSize, Vector2.zero).rectTransform);

                var hand = NewText(left ? "LeftLabel" : "RightLabel", content, centre, 34f);
                Place(hand.rectTransform, new Vector2(120f, 44f), new Vector2(left ? -ControllerX : ControllerX, ControllerY - ControllerSize * 0.55f));
                hand.text = left ? "LEFT" : "RIGHT";
                hand.color = Cream;
            }

            var triggerLabel = NewText("TriggerLabel", content, centre, 44f);
            Place(triggerLabel.rectTransform, new Vector2(230f, 100f), new Vector2(0f, 40f));
            triggerLabel.text = "TRIGGER\n<size=70%>SHOOT</size>";

            var gripLabel = NewText("GripLabel", content, centre, 44f);
            Place(gripLabel.rectTransform, new Vector2(230f, 100f), new Vector2(0f, -60f));
            gripLabel.text = "GRIP\n<size=70%>RELOAD</size>";

            var hints = HintStrip(content, centre);

            var component = card.gameObject.AddComponent<ControlsCard>();
            new Fields(component).Set("game", Object.FindObjectOfType<RangeGame>())
                                 .Set("content", content.gameObject)
                                 .Set("group", group)
                                 .SetArray("triggers", triggers)
                                 .SetArray("grips", grips)
                                 .SetArray("triggerGlows", triggerGlows)
                                 .SetArray("gripGlows", gripGlows)
                                 .Set("triggerLabel", triggerLabel)
                                 .Set("gripLabel", gripLabel)
                                 .SetArray("bulbs", bulbs)
                                 .SetArray("hintTexts", hints)
                                 .Apply();

            content.gameObject.SetActive(false);
            Did(CardName + " on the announcer board, " + CardSize.x + " x " + CardSize.y + " canvas units: both Quest 3 " +
                "controllers pressing trigger then grip, " + bulbs.Count + " chasing bulbs, three scrolling hints. " +
                "Hidden until the intro");
            Dirty();
        }

        /// Every piece of art, imported clamped, with mipmaps for the distance and at its real size
        private static Dictionary<string, Texture2D> LoadCardArt()
        {
            var art = new Dictionary<string, Texture2D>();
            foreach (var name in new[] { "card_board", "card_bulb", "controller_body", "controller_trigger",
                                         "controller_grip", "controller_trigger_glow", "controller_grip_glow" })
            {
                string path = CardArt + name + ".png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    Fail("No " + path + ". Run tools/controls-card-art.py, click into Unity, run this again");
                    return null;
                }

                if (!importer.alphaIsTransparency || importer.npotScale != TextureImporterNPOTScale.None ||
                    importer.wrapMode != TextureWrapMode.Clamp)
                {
                    importer.alphaIsTransparency = true;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.mipmapEnabled = true;
                    importer.SaveAndReimport();
                }

                art[name] = RangeSetupUtil.Load<Texture2D>(path);
            }

            return art;
        }

        /// Bulbs down both sides and along the bottom, on the gold border. The top is under the awning
        private static List<Object> LayBulbs(RectTransform content, Texture2D bulb)
        {
            var bulbs = new List<Object>();
            float left = -CardSize.x * 0.5f + CardSize.x * BorderInset;
            float right = -left;
            float top = CardSize.y * 0.5f - CardSize.y * BorderTop;
            float bottom = -CardSize.y * 0.5f + CardSize.x * BorderInset;

            // Round the edge in one direction, so the chase runs round it rather than jumping
            var points = new List<Vector2>();
            for (float y = top; y > bottom; y -= BulbGap) points.Add(new Vector2(left, y));
            for (float x = left; x < right; x += BulbGap) points.Add(new Vector2(x, bottom));
            for (float y = bottom; y <= top; y += BulbGap) points.Add(new Vector2(right, y));

            for (int i = 0; i < points.Count; i++)
                bulbs.Add(NewImage("Bulb" + i, content, bulb, Vector2.one * BulbSize, points[i]));

            return bulbs;
        }

        /// The cream strip along the bottom, masked so a hint scrolling in or out is cut at its ends
        private static List<Object> HintStrip(RectTransform content, TMP_Text style)
        {
            float height = CardSize.y * StripHeight;
            float y = CardSize.y * 0.5f - CardSize.y * StripTop - height * 0.5f;
            var strip = NewRect("Hints", content, new Vector2(CardSize.x * (1f - 2f * StripInset) - 40f, height - 10f), new Vector2(0f, y));
            strip.gameObject.AddComponent<RectMask2D>();

            var texts = new List<Object>();
            for (int i = 0; i < 2; i++)
            {
                var text = NewText("Hint" + i, strip, style, 30f);
                Place(text.rectTransform, strip.sizeDelta, Vector2.zero);
                text.color = BrownInk;
                text.text = "";
                texts.Add(text);
            }

            return texts;
        }

        private static RectTransform NewRect(string name, Transform parent, Vector2 size, Vector2 at)
        {
            var holder = new GameObject(name, typeof(RectTransform));
            holder.layer = parent.gameObject.layer;
            var rect = (RectTransform)holder.transform;
            rect.SetParent(parent, false);
            Place(rect, size, at);
            return rect;
        }

        private static RawImage NewImage(string name, Transform parent, Texture2D texture, Vector2 size, Vector2 at)
        {
            var image = NewRect(name, parent, size, at).gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            return image;
        }

        private static void Place(RectTransform rect, Vector2 size, Vector2 at)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = at;
        }

        // 25, spider sounds

        /// The clips tools/range-audio-halloween.sh builds into World/spider_*. Fills blanks only
        private static void WireTheSpiderSounds()
        {
            var nest = Object.FindObjectOfType<SpiderNest>();
            if (nest == null) { Fail("No SpiderNest in the scene. Run step 17 first"); return; }

            var fields = new Fields(nest);
            string folder = RangeSounds + "/World";
            FillClips(fields, "dropClips", Clips(folder, "spider_drop_"));
            FillClips(fields, "deathClips", Clips(folder, "spider_death_"));
            FillClips(fields, "swatClips", Clips(folder, "spider_swat_"));

            var dangle = Clips(folder, "spider_dangle_");
            if (fields.Ref("dangleLoop") != null) Say("Dangle Loop already set, left as it is");
            else if (dangle.Count == 0) Fail("No spider_dangle clip in " + folder + ". Run tools/range-audio-halloween.sh with ONLY=spider_");
            else { fields.Set("dangleLoop", dangle[0]); Did("Dangle Loop: " + dangle[0].name); }

            fields.Apply();
            Dirty();
        }

        private static void FillClips(Fields fields, string slot, List<Object> clips)
        {
            if (fields.Find(slot).arraySize > 0) { Say(slot + " already filled, left as it is"); return; }
            if (clips.Count == 0) { Fail("No clips for " + slot + ". Run tools/range-audio-halloween.sh with ONLY=spider_"); return; }

            fields.SetArray(slot, clips);
            Did(slot + ": " + clips.Count + " clips");
        }

        // 26, shot decals

        private const string DecalArt = "Assets/Textures/ShotDecals/shot_holes.png";
        private const string DecalMaterial = "Assets/Materials/SR_ShotDecal.mat";
        private const string DecalShader = "IntuitiveDesigns/Shot Decal";

        /// Bullet holes where a shot hits the room, on the Impact FX object that already plays the dust and
        /// the surface sound. Fills blanks only
        private static void AddTheShotDecals()
        {
            var impact = Object.FindObjectOfType<ImpactFX>();
            if (impact == null) { Fail("No Impact FX in the open scene"); return; }

            var importer = AssetImporter.GetAtPath(DecalArt) as TextureImporter;
            if (importer == null) { Fail("No " + DecalArt + ". Run tools/shot-decal-art.py, click into Unity, run this again"); return; }

            if (!importer.alphaIsTransparency || importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
                Did(DecalArt + ": clamped, alpha is transparency, mipmaps");
            }

            var shader = Shader.Find(DecalShader);
            if (shader == null) { Fail("No shader " + DecalShader + " (Assets/Shaders/ShotDecal.shader)"); return; }

            var material = RangeSetupUtil.Load<Material>(DecalMaterial);
            if (material == null)
            {
                material = new Material(shader) { mainTexture = RangeSetupUtil.Load<Texture2D>(DecalArt) };
                AssetDatabase.CreateAsset(material, DecalMaterial);
                Did("Made " + DecalMaterial + " (" + DecalShader + ")");
            }
            else if (material.shader != shader)
            {
                // The first version floated its holes a centimetre off the wall on a particle shader;
                // this one sits them on it and wins the depth test with a polygon offset instead
                material.shader = shader;
                EditorUtility.SetDirty(material);
                Did(DecalMaterial + " moved onto " + DecalShader);
            }

            var decals = new Fields(RangeSetupUtil.Ensure<ShotDecals>(impact.gameObject));
            if (decals.Ref("material") == null) decals.Set("material", material);
            if (decals.Ref("game") == null) decals.Set("game", Object.FindObjectOfType<RangeGame>());
            decals.Apply();

            var fx = new Fields(impact);
            if (fx.Ref("decals") == null) fx.Set("decals", impact.GetComponent<ShotDecals>()).Apply();

            Did("Shot Decals on " + impact.name + ": up to 30 holes, the oldest fading, all fading as a round ends. " +
                "Only static colliders without a Rigidbody take one: the room needs colliders (notes/environment-colliders-todo.md)");
            Dirty();
        }
    }
}
