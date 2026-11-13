using TMPro;
using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    /// Step 71: the reload tag over each gun (ReloadTag). Built from the controls card's controller art and lettering and the
    /// lock's arrow, so it reads as part of the same sign set. Fills blanks: a gun that has one is left alone
    public static partial class RangeSetup
    {
        private const string ReloadTagName = "Reload Tag";

        [MenuItem("Shooting Range/Setup/71 - Reload Tag On The Guns", false, 94)]
        public static void ReloadTagMenu()
        {
            Begin("Reload tag");
            var game = Object.FindObjectOfType<RangeGame>();
            var font = CardFont();
            foreach (var pistol in Object.FindObjectsOfType<Pistol>())
            {
                if (RangeSetupUtil.Child(pistol.transform, ReloadTagName) != null) { Say(pistol.name + " already has a reload tag"); continue; }
                BuildAReloadTag(pistol, game, font);
                Did(pistol.name + ": reload tag over the muzzle, shown while it's dry, double size until the first reload");
            }
            Dirty();
            End();
        }

        private static void BuildAReloadTag(Pistol pistol, RangeGame game, TMP_FontAsset font)
        {
            var holder = new GameObject(ReloadTagName).transform;
            holder.SetParent(pistol.transform, false);
            var content = new GameObject("Content").transform;
            content.SetParent(holder, false);

            var text = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshPro>();
            text.transform.SetParent(content, false);
            text.rectTransform.sizeDelta = new Vector2(0.14f, 0.06f);
            text.rectTransform.localPosition = new Vector3(0.035f, 0.05f, 0f);
            if (font != null) { text.font = font; text.fontSharedMaterial = font.material; }
            text.enableAutoSizing = true;
            text.fontSizeMin = 0.05f;
            text.fontSizeMax = 1f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(1f, 0.85f, 0.47f);
            text.text = "RELOAD\n<size=55%>SQUEEZE GRIP</size>";

            // The card's controller, its grip lit; the art is the right controller, mirrored for the left hand
            var icon = new GameObject("Controller").transform;
            icon.SetParent(content, false);
            icon.localPosition = new Vector3(-0.075f, 0.05f, 0f);
            if (pistol.Hand == Liminal.SDK.VR.Avatars.VRAvatarLimbType.LeftHand) icon.localScale = new Vector3(-1f, 1f, 1f);
            var glow = TagQuad("Grip Glow", icon, "controller_grip_glow", 0.07f, 0.002f);
            TagQuad("Body", icon, "controller_body", 0.07f, 0.001f);
            TagQuad("Grip", icon, "controller_grip", 0.07f, 0f);

            var arrow = TagQuad("Arrow", content, "", 0.035f, 0f);
            arrow.localPosition = new Vector3(0f, 0.005f, 0f);
            var arrowMaterial = EyeMaterial("SR_LockArrow", "Unlit/Transparent", Color.white);
            if (arrowMaterial != null && arrowMaterial.mainTexture == null)
            {
                arrowMaterial.mainTexture = RangeSetupUtil.Load<Texture2D>(LockArrowArt);
                EditorUtility.SetDirty(arrowMaterial);
            }
            arrow.GetComponent<Renderer>().sharedMaterial = arrowMaterial;

            new Fields(holder.gameObject.AddComponent<ReloadTag>()).Set("pistol", pistol).Set("game", game)
                .Set("content", content.gameObject).Set("gripGlow", glow).Apply();
        }

        /// A quad of the card's art; behind is metres pushed back so the layers stack. Empty art leaves the material for the caller
        private static Transform TagQuad(string name, Transform parent, string art, float size, float behind)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = new Vector3(0f, 0f, behind);
            quad.transform.localScale = Vector3.one * size;
            if (art.Length == 0) return quad.transform;

            var material = EyeMaterial("SR_ReloadTag_" + art, "Unlit/Transparent", Color.white);
            if (material != null && material.mainTexture == null)
            {
                material.mainTexture = RangeSetupUtil.Load<Texture2D>(CardArt + art + ".png");
                EditorUtility.SetDirty(material);
            }
            quad.GetComponent<Renderer>().sharedMaterial = material;
            return quad.transform;
        }
    }
}
