using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    /// Steps 84 and 84b: today's new pieces out of the bake and back in, to find what holds a bake at "Preparing bake" (user,
    /// 2026-11-13: even the one-bake test, 39d, never got past it). Only their Lightmap Static flag changes
    public static partial class RangeSetup
    {
        [MenuItem("Shooting Range/Setup/84 - Leave Today's Pieces Out Of The Bake (test)", false, 107)]
        public static void LeaveTodaysPiecesOutMenu()
        {
            Begin("Today's pieces out of the bake");
            int changed = SetTodaysPiecesInTheBake(false);
            Did(changed + " pieces out of the bake. Now run 39d; if it gets past \"Preparing bake\", one of these held it. 84b puts them back");
            Dirty();
            End();
        }

        [MenuItem("Shooting Range/Setup/84b - Put Today's Pieces Back In The Bake", false, 108)]
        public static void PutTodaysPiecesBackMenu()
        {
            Begin("Today's pieces back in the bake");
            Did(SetTodaysPiecesInTheBake(true) + " pieces back in the bake");
            Dirty();
            End();
        }

        /// The tent's canvas, collars and skirt, the beams laid along it and the pillar copies on Alex's pillars
        private static int SetTodaysPiecesInTheBake(bool baked)
        {
            var pieces = new List<GameObject>();
            var tent = RangeSetupUtil.FindInScene(TentName);
            if (tent != null)
            {
                foreach (var part in TentParts)
                {
                    var child = RangeSetupUtil.Child(tent.transform, part);
                    if (child != null) pieces.Add(child.gameObject);
                }
            }
            foreach (var holder in new[] { TentBeamsName, PillarTopsName })
            {
                var found = RangeSetupUtil.FindInScene(holder);
                if (found == null) continue;
                foreach (Transform piece in found.transform) pieces.Add(piece.gameObject);
            }

            int changed = 0;
            foreach (var piece in pieces)
            {
                foreach (var t in piece.GetComponentsInChildren<Transform>(true))
                {
                    var flags = GameObjectUtility.GetStaticEditorFlags(t.gameObject);
                    var wanted = baked ? flags | StaticEditorFlags.LightmapStatic : flags & ~StaticEditorFlags.LightmapStatic;
                    if (wanted == flags) continue;
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, wanted);
                    changed++;
                }
            }

            if (pieces.Count == 0) Fail("None of today's pieces are in the scene (" + TentName + ", " + TentBeamsName + ", " + PillarTopsName + ")");
            return changed;
        }
    }
}
