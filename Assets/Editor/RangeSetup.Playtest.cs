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
                    "hand changes to it are lost.", "Rebuild", "Cancel"))
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
        private const string CardFontFile = "Assets/Font/Rye/Rye-Regular.ttf";
        private const string CardFontAsset = "Assets/Font/Rye/Rye-Regular SDF.asset";
        // 1.2x the first card (user, 2026-11-10: "the sign is too small"); any bigger and its top reaches the booth top, where
        // the prize shelf stands
        private const float CardWidth = 864f;
        private const float BulbSize = 22f;
        private const float BulbGap = 46f;

        private static readonly Color CardRed = new Color(0.56f, 0.09f, 0.13f);
        private static readonly Color Gold = new Color(1f, 0.85f, 0.47f);
        private static readonly Color Cream = new Color(0.96f, 0.92f, 0.83f);
        private static readonly Color BrownInk = new Color(0.18f, 0.1f, 0.05f);

        [System.Serializable] private class CardRect { public float x, y, w, h; }
        [System.Serializable] private class CardPoint { public float x, y; }
        [System.Serializable] private class CardSize { public float w, h; }

        /// Where tools/controls-card-art.py put everything, in its design units, top-left origin. It writes this file
        /// with the art, so the two can't disagree
        [System.Serializable]
        private class CardLayout
        {
            public CardSize size;
            public CardRect frame, title, controller, triggerLabel, gripLabel, bullseye, startLabel;
            public CardPoint triggerHinge;
            public CardRect[] hints;
        }

        private static readonly string[][] CardHints =
        {
            new[] { "SWAT THE SPIDER", "OR SHOOT IT" },
            new[] { "SHOOT GLOWING SIGNS", "FOR POWER-UPS" },
            new[] { "DODGE THE BATS", "OR SHOOT 'EM" },
        };

        /// The card on the announcer board, painted as a carnival sign: up from the start, shot to begin. Built from the
        /// art, its layout file and Rye; ControlsCard does the moving
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
            var layout = LoadCardLayout();
            var font = CardFont();
            if (art == null || layout == null || font == null) return;

            float k = CardWidth / layout.size.w;
            var size = new Vector2(layout.size.w, layout.size.h) * k;
            System.Func<CardRect, Vector2> at = r => new Vector2((r.x + r.w * 0.5f - layout.size.w * 0.5f) * k,
                                                                 (layout.size.h * 0.5f - r.y - r.h * 0.5f) * k);
            System.Func<CardRect, Vector2> span = r => new Vector2(r.w, r.h) * k;

            var card = NewRect(CardName, board, size, Vector2.zero);
            var group = card.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            // The whole card takes the shot, not only the bullseye: a forgiving target in VR. Rounds skip only NoShoot
            var hitBox = card.gameObject.AddComponent<BoxCollider>();
            hitBox.size = new Vector3(size.x, size.y, 20f);

            var content = NewRect("Content", card, size, Vector2.zero);
            NewImage("Board", content, art["card_board"], size, Vector2.zero);
            var bulbs = LayBulbs(content, art["card_bulb"], layout, k);

            var title = CardText("Title", content, font, 36f, CardRed, layout.title, at, span);
            title.text = "HOW TO PLAY";

            var controller = NewRect("Controller", content, span(layout.controller), at(layout.controller));
            var csize = span(layout.controller);
            var glows = new List<Object>
            {
                NewImage("TriggerGlow", controller, art["controller_trigger_glow"], csize, Vector2.zero),
                NewImage("GripGlow", controller, art["controller_grip_glow"], csize, Vector2.zero),
            };
            NewImage("Body", controller, art["controller_body"], csize, Vector2.zero);
            var trigger = NewImage("Trigger", controller, art["controller_trigger"], csize, Vector2.zero).rectTransform;
            var hinge = new Vector2(layout.triggerHinge.x, layout.triggerHinge.y);
            trigger.pivot = hinge;
            trigger.anchoredPosition = (hinge - Vector2.one * 0.5f) * csize.x;
            NewImage("Grip", controller, art["controller_grip"], csize, Vector2.zero);

            CardText("TriggerLabel", content, font, 28f, BrownInk, layout.triggerLabel, at, span).text = "TRIGGER\n<size=58%>TO SHOOT</size>";
            CardText("GripLabel", content, font, 28f, BrownInk, layout.gripLabel, at, span).text = "GRIP\n<size=58%>TO RELOAD</size>";
            for (int i = 0; i < CardHints.Length && i < layout.hints.Length; i++)
                CardText("Hint" + i, content, font, 13f, BrownInk, layout.hints[i], at, span).text = CardHints[i][0] + "\n" + CardHints[i][1];

            var bullseyeGlow = NewImage("BullseyeGlow", content, art["card_bullseye_glow"], span(layout.bullseye) * 1.5f, at(layout.bullseye));
            var bullseye = NewImage("Bullseye", content, art["card_bullseye"], span(layout.bullseye), at(layout.bullseye)).rectTransform;
            var start = CardText("StartLabel", content, font, 30f, Cream, layout.startLabel, at, span);
            start.text = "SHOOT TO START";

            var component = card.gameObject.AddComponent<ControlsCard>();
            new Fields(component).Set("game", Object.FindObjectOfType<RangeGame>())
                                 .Set("content", content.gameObject)
                                 .Set("group", group)
                                 .SetArray("buttonGlows", glows)
                                 .SetArray("bulbs", bulbs)
                                 .Set("bullseye", bullseye)
                                 .Set("bullseyeGlow", bullseyeGlow)
                                 .Set("startLabel", start)
                                 .Apply();

            content.gameObject.SetActive(false);
            Did(CardName + " on the announcer board, " + size.x + " x " + size.y + " canvas units: painted sign, controller with " +
                "trigger and grip glowing, three hints, " + bulbs.Count + " chasing bulbs, shot anywhere to start");
            Dirty();
        }

        private static TextMeshProUGUI CardText(string name, Transform parent, TMP_FontAsset font, float size, Color colour,
                                                CardRect where, System.Func<CardRect, Vector2> at, System.Func<CardRect, Vector2> span)
        {
            var holder = new GameObject(name, typeof(RectTransform));
            holder.layer = parent.gameObject.layer;
            holder.transform.SetParent(parent, false);
            Place((RectTransform)holder.transform, span(where), at(where));

            var text = holder.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSharedMaterial = font.material;
            text.fontSize = size;
            text.enableAutoSizing = true;
            text.fontSizeMax = size;
            text.fontSizeMin = size * 0.5f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = colour;
            text.raycastTarget = false;
            return text;
        }

        /// Rye as a TextMesh Pro font, made the way TMP's own Create > TextMeshPro > Font Asset makes it (dynamic, 1024
        /// atlas, SDFAA), once
        private static TMP_FontAsset CardFont()
        {
            var font = RangeSetupUtil.Load<TMP_FontAsset>(CardFontAsset);
            if (font != null) return font;

            var source = RangeSetupUtil.Load<Font>(CardFontFile);
            if (source == null) { Fail("No " + CardFontFile); return null; }

            var selected = Selection.activeObject;
            Selection.activeObject = source;
            TMP_FontAsset_CreationMenu.CreateFontAsset();
            Selection.activeObject = selected;

            font = RangeSetupUtil.Load<TMP_FontAsset>(CardFontAsset);
            if (font == null) { Fail("TextMesh Pro did not make " + CardFontAsset); return null; }
            Did("Made " + CardFontAsset + " from Rye (SIL Open Font Licence, OFL.txt beside it)");
            return font;
        }

        private static CardLayout LoadCardLayout()
        {
            var file = RangeSetupUtil.Load<TextAsset>(CardArt + "card_layout.json");
            if (file == null) { Fail("No " + CardArt + "card_layout.json. Run tools/controls-card-art.py, click into Unity, run this again"); return null; }
            return JsonUtility.FromJson<CardLayout>(file.text);
        }

        /// Every piece of art, clamped, with mipmaps for the distance, at its real size and uncompressed: flat painted
        /// art bands and blocks when compressed (user, 2026-11-10)
        private static Dictionary<string, Texture2D> LoadCardArt()
        {
            var art = new Dictionary<string, Texture2D>();
            foreach (var name in new[] { "card_board", "card_bulb", "controller_body", "controller_trigger", "controller_grip",
                                         "controller_trigger_glow", "controller_grip_glow", "card_bullseye", "card_bullseye_glow" })
            {
                string path = CardArt + name + ".png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    Fail("No " + path + ". Run tools/controls-card-art.py, click into Unity, run this again");
                    return null;
                }

                if (!importer.alphaIsTransparency || importer.npotScale != TextureImporterNPOTScale.None ||
                    importer.wrapMode != TextureWrapMode.Clamp || importer.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    importer.alphaIsTransparency = true;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.mipmapEnabled = true;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                }

                art[name] = RangeSetupUtil.Load<Texture2D>(path);
            }

            return art;
        }

        /// Bulbs round the gold frame, one way round so the chase runs round it, leaving a gap where the ribbon covers the top
        private static List<Object> LayBulbs(RectTransform content, Texture2D bulb, CardLayout layout, float k)
        {
            var f = layout.frame;
            float left = (f.x - layout.size.w * 0.5f) * k, right = (f.x + f.w - layout.size.w * 0.5f) * k;
            float top = (layout.size.h * 0.5f - f.y) * k, bottom = (layout.size.h * 0.5f - f.y - f.h) * k;
            float ribbonLeft = (layout.title.x - 60f - layout.size.w * 0.5f) * k;
            float ribbonRight = (layout.title.x + layout.title.w + 60f - layout.size.w * 0.5f) * k;

            var points = new List<Vector2>();
            for (float y = top; y > bottom; y -= BulbGap) points.Add(new Vector2(left, y));
            for (float x = left; x < right; x += BulbGap) points.Add(new Vector2(x, bottom));
            for (float y = bottom; y < top; y += BulbGap) points.Add(new Vector2(right, y));
            for (float x = right; x > left; x -= BulbGap)
                if (x < ribbonLeft || x > ribbonRight) points.Add(new Vector2(x, top));

            var bulbs = new List<Object>();
            for (int i = 0; i < points.Count; i++)
                bulbs.Add(NewImage("Bulb" + i, content, bulb, Vector2.one * BulbSize, points[i]));
            return bulbs;
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
