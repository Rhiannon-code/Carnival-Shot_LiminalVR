using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    public static partial class RangeSetup
    {
        // The sign's usual colour map, then the scared one. Sign_Vampire's material reads a copy under ZombieHand, the same image
        private static readonly string[,] ScaredFaces =
        {
            { "Assets/Models/ZombieHand/Vampire/openPBR_shader1_Base_color.png", "Assets/Models/AScaredVampire1/openPBR_shader1_Base_color.png" },
            { "Assets/Models/Vampire2/svgOpenPBRSurface1_Base_color.png", "Assets/Models/AScaredVampire/svgOpenPBRSurface1_Base_color.png" },
            { "Assets/Models/Vampire3/svgOpenPBRSurface2_Base_color.png", "Assets/Models/AScaredVampire3/svgOpenPBRSurface2_Base_color.png" },
        };

        [MenuItem("Shooting Range/Setup/66 - Scared Vampire Faces", false, 89)]
        public static void ScaredFacesMenu()
        {
            Begin("Scared vampire faces");
            var faces = new List<Object>();
            var scared = new List<Object>();
            for (int i = 0; i < ScaredFaces.GetLength(0); i++)
            {
                var face = RangeSetupUtil.Load<Texture2D>(ScaredFaces[i, 0]);
                var fright = RangeSetupUtil.Load<Texture2D>(ScaredFaces[i, 1]);
                if (face == null || fright == null) { Fail("Missing " + (face == null ? ScaredFaces[i, 0] : ScaredFaces[i, 1])); continue; }

                faces.Add(face);
                scared.Add(fright);
            }

            if (faces.Count == 0) { End(); return; }

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                new Fields(RangeSetupUtil.Ensure<SignScaredFace>(root)).SetArray("faces", faces).SetArray("scared", scared).Apply();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Did("Sign Scared Face on " + PrefabPath + ": " + string.Join(", ", faces.Select(f => f.name).ToArray()) +
                    " swap to their scared sheets when shot or burnt, and back at the next launch");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            End();
        }
    }
}
