using TMPro;
using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    /// Step 71: the reload tag over each gun (ReloadTag): the controls card's lettering over the lock's arrow, the arrow pointing
    /// down at the gun's ammo counter (user, 2026-11-17: no controller icon, where the red "RELOAD · GRIP" was). Rebuilds the
    /// tags it made, so a change here reaches them
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
                var old = RangeSetupUtil.Child(pistol.transform, ReloadTagName);
                if (old != null) Object.DestroyImmediate(old.gameObject);
                BuildAReloadTag(pistol, game, font);
                Did(pistol.name + ": reload tag over its ammo counter, shown while it's dry");
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
            // Anchored, not localPosition: set through localPosition it stayed at 0 on one gun, over the arrow (2026-11-17)
            text.rectTransform.anchoredPosition = new Vector2(0f, 0.05f);
            if (font != null) { text.font = font; text.fontSharedMaterial = font.material; }
            text.enableAutoSizing = true;
            text.fontSizeMin = 0.05f;
            text.fontSizeMax = 1f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(1f, 0.85f, 0.47f);
            text.text = "RELOAD\n<size=55%>SQUEEZE GRIP</size>";

            // At the tag's own middle, so ReloadTag puts it just over what it points at
            var arrow = TagQuad("Arrow", content, 0.035f);
            var arrowMaterial = EyeMaterial("SR_LockArrow", "Unlit/Transparent", Color.white);
            if (arrowMaterial != null && arrowMaterial.mainTexture == null)
            {
                arrowMaterial.mainTexture = RangeSetupUtil.Load<Texture2D>(LockArrowArt);
                EditorUtility.SetDirty(arrowMaterial);
            }
            arrow.GetComponent<Renderer>().sharedMaterial = arrowMaterial;

            var counter = pistol.GetComponentInChildren<AmmoCounter>(true);
            if (counter == null) Say(pistol.name + " has no ammo counter: its reload tag points at the muzzle");
            new Fields(holder.gameObject.AddComponent<ReloadTag>()).Set("pistol", pistol).Set("game", game)
                .Set("content", content.gameObject).Set("pointAt", counter != null ? counter.transform : null).Apply();
        }

        private static Transform TagQuad(string name, Transform parent, float size)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent, false);
            quad.transform.localScale = Vector3.one * size;
            return quad.transform;
        }
    }
}
