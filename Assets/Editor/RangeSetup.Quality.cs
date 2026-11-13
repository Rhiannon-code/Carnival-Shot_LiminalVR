using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    /// Step 67: textures and lightmaps that look sharper on Quest and in the editor (user, 2026-11-10). The controls card's
    /// own art is made uncompressed by step 24
    public static partial class RangeSetup
    {
        [MenuItem("Shooting Range/Setup/67 - Sharpen Textures And Lightmaps", false, 90)]
        public static void SharpenMenu()
        {
            bool astc = EditorUserBuildSettings.androidBuildSubtarget != MobileTextureSubtarget.ASTC;
            if (!EditorUtility.DisplayDialog("Sharpen textures and lightmaps",
                    "This changes three project settings:\n\n" +
                    "- Android texture compression to ASTC" + (astc ? ". Unity then reimports every texture, which takes a long time." : " (already set).") + "\n" +
                    "- Anisotropic filtering forced on for every texture.\n" +
                    "- Lightmap resolution to " + QuestBakeResolution + " texels per metre. It does not bake: run step 39 after, " +
                    "which replaces every lightmap.\n\nNothing is deleted.", "Change them", "Cancel"))
                return;

            Begin("Sharpen textures and lightmaps");
            SharpenTexturesAndLightmaps();
            End();
        }

        private const string PlayPanelName = "PlayPanel";

        [MenuItem("Shooting Range/Setup/68 - Remove The HUD Play Panel", false, 91)]
        public static void RemovePlayPanelMenu()
        {
            if (!EditorUtility.DisplayDialog("Remove the HUD play panel",
                    "This deletes HUD/" + PlayPanelName + " from the open scene: the round, targets left, combo, active " +
                    "power-up and score readouts. It also strips the HUD's component whose script (RangeHUD) is gone. " +
                    "The power-up callout on the announcer and the perf readout stay.", "Delete it", "Cancel"))
                return;

            Begin("HUD play panel");
            RemoveThePlayPanel();
            End();
        }

        /// The score is kept for the end now and most messages are on the announcer or the gun (user, 2026-11-10)
        private static void RemoveThePlayPanel()
        {
            var hud = Object.FindObjectOfType<PowerUpHUD>();
            if (hud == null) { Fail("No PowerUpHUD in the open scene"); return; }

            var panel = RangeSetupUtil.Child(hud.transform, PlayPanelName);
            if (panel != null)
            {
                Object.DestroyImmediate(panel.gameObject);
                Did("Deleted HUD/" + PlayPanelName);
            }
            else Say("No HUD/" + PlayPanelName + ", nothing to delete");

            int stale = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(hud.gameObject);
            if (stale > 0) Did("HUD: removed " + stale + " component(s) whose script is gone (RangeHUD)");
            Dirty();
        }

        private static void SharpenTexturesAndLightmaps()
        {
            if (QualitySettings.anisotropicFiltering != AnisotropicFiltering.ForceEnable)
            {
                QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
                Did("Anisotropic filtering forced on (quality level " + QualitySettings.names[QualitySettings.GetQualityLevel()] + ")");
            }
            else Say("Anisotropic filtering already forced on");

            if (Mathf.RoundToInt(LightmapEditorSettings.bakeResolution) != QuestBakeResolution)
            {
                Did("Lightmap resolution " + LightmapEditorSettings.bakeResolution + " -> " + QuestBakeResolution +
                    " texels per metre. Run step 39 to bake it");
                LightmapEditorSettings.bakeResolution = QuestBakeResolution;
                Dirty();
            }
            else Say("Lightmap resolution already " + QuestBakeResolution);

            // Last, as it starts the long reimport
            if (EditorUserBuildSettings.androidBuildSubtarget != MobileTextureSubtarget.ASTC)
            {
                EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
                Did("Android texture compression -> ASTC. Every texture reimports; check the Liminal player loads it on device");
            }
            else Say("Android texture compression already ASTC");
        }
    }
}
