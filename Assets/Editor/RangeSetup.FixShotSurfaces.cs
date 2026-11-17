using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    /// Step 83: what step 82 found on Alex's pieces and the tent, put right (user, 2026-11-13). Box colliders where a piece has
    /// none, a PhysicMaterial where a collider has none, the tent's cloth its own soft surface with no debris, and bullet holes on
    /// the flat wood. Fills blanks: a collider or material already there is left alone
    public static partial class RangeSetup
    {
        private enum ShotSurface { Wood, Metal, Cloth }

        private struct ShotPiece
        {
            public string Name;
            public bool Prefix;
            public ShotSurface Surface;
            public bool Holes;
            public ShotPiece(string name, ShotSurface surface, bool holes, bool prefix = false)
            {
                Name = name; Surface = surface; Holes = holes; Prefix = prefix;
            }
        }

        // Matched against an object's name or a parent's, without " (n)" (user's choices, 2026-11-13). The collider goes on the
        // matched object, so its name is what decides holes
        private static readonly ShotPiece[] ShotPieces =
        {
            new ShotPiece("step-counter", ShotSurface.Wood, true), new ShotPiece("step-wall", ShotSurface.Wood, true),
            new ShotPiece("wood_beam", ShotSurface.Wood, true), new ShotPiece("wood_corner", ShotSurface.Wood, true),
            new ShotPiece("skirt", ShotSurface.Wood, true), new ShotPiece("corner_skirt", ShotSurface.Wood, true),
            new ShotPiece("wall_bats", ShotSurface.Wood, false), new ShotPiece("lantern", ShotSurface.Metal, false),
            new ShotPiece("Canvas", ShotSurface.Cloth, false),
            new ShotPiece("Valance", ShotSurface.Cloth, false),
            new ShotPiece("vlwmahl_LOD0_TIER3_000", ShotSurface.Wood, true), new ShotPiece("stairs1", ShotSurface.Wood, true),
            new ShotPiece("handle", ShotSurface.Metal, false, true), new ShotPiece("helmet", ShotSurface.Metal, false),
            new ShotPiece("decoration2", ShotSurface.Wood, false), new ShotPiece("Beam", ShotSurface.Wood, true),
        };
        // The cloth sounds like the soft surface, with no stuffing flying off it
        private const string SoftSurface = "SR_Soft";

        [MenuItem("Shooting Range/Setup/83 - Fix Shot Surfaces", false, 106)]
        public static void FixShotSurfacesMenu()
        {
            Begin("Shot surfaces fixed");
            try { FixShotSurfaces(); }
            catch (System.Exception e) { Fail("Stopped by an error, so nothing after this point ran: " + e); }
            finally { End(); }
        }

        private static void FixShotSurfaces()
        {
            var impact = Object.FindObjectOfType<ImpactFX>();
            if (impact == null) { Fail("No Impact FX in the scene"); return; }

            var wood = Surface("Wood");
            var metal = Surface("Metal");
            var cloth = Surface("Canvas");
            var fields = new SerializedObject(impact);
            AddTheClothSurface(fields.FindProperty("surfaces"), cloth);
            AddHoleSurfaces(fields.FindProperty("holeSurfaces"));
            fields.ApplyModifiedPropertiesWithoutUndo();

            int boxes = 0, tagged = 0;
            foreach (var renderer in Object.FindObjectsOfType<MeshRenderer>())
            {
                if (!renderer.enabled || !ShotRoom.Contains(renderer.bounds.center) || Moves(renderer.transform)) continue;

                ShotPiece piece;
                var holder = MatchShotPiece(renderer.transform, out piece);
                if (holder == null) continue;

                var material = piece.Surface == ShotSurface.Wood ? wood : piece.Surface == ShotSurface.Metal ? metal : cloth;
                var collider = holder.GetComponent<Collider>();
                if (collider == null)
                {
                    collider = FittedBox(holder, renderer);
                    boxes++;
                }
                if (collider.sharedMaterial == null) { collider.sharedMaterial = material; tagged++; }
            }

            Dirty();
            Did(boxes + " box colliders added, " + tagged + " colliders given a surface. Run step 82 to check");
        }

        /// The cloth's own surface: the soft surface's sounds, no debris (user: "Soft canvas, no debris")
        private static void AddTheClothSurface(SerializedProperty surfaces, PhysicMaterial cloth)
        {
            SerializedProperty soft = null;
            for (int i = 0; i < surfaces.arraySize; i++)
            {
                var entry = surfaces.GetArrayElementAtIndex(i);
                var material = entry.FindPropertyRelative("material").objectReferenceValue;
                if (material == cloth) { Say("Impact FX already has " + cloth.name); return; }
                if (material != null && material.name == SoftSurface && soft == null) soft = entry;
            }

            surfaces.arraySize++;
            var added = surfaces.GetArrayElementAtIndex(surfaces.arraySize - 1);
            added.FindPropertyRelative("material").objectReferenceValue = cloth;
            added.FindPropertyRelative("debris").intValue = (int)DebrisKind.None;
            var clips = added.FindPropertyRelative("clips");
            clips.arraySize = soft != null ? soft.FindPropertyRelative("clips").arraySize : 0;
            for (int i = 0; i < clips.arraySize; i++)
                clips.GetArrayElementAtIndex(i).objectReferenceValue = soft.FindPropertyRelative("clips").GetArrayElementAtIndex(i).objectReferenceValue;

            if (soft == null) Fail("No " + SoftSurface + " in Impact FX to borrow sounds from: " + cloth.name + " is silent");
            else Did("Impact FX: " + cloth.name + ", " + clips.arraySize + " soft clips, no debris");
        }

        /// The renderer or the nearest parent named in ShotPieces
        private static Transform MatchShotPiece(Transform from, out ShotPiece match)
        {
            for (var t = from; t != null; t = t.parent)
            {
                string name = BaseName(t.name);
                foreach (var piece in ShotPieces)
                {
                    if (piece.Prefix ? !name.StartsWith(piece.Name) : name != piece.Name) continue;
                    match = piece;
                    return t;
                }
            }

            match = default(ShotPiece);
            return null;
        }

        /// A box round the visible mesh, on the object whose name decides holes. On the mesh's own object Unity sizes it as it is
        /// added; on a parent it is sized from the mesh's bounds in the parent's space
        private static BoxCollider FittedBox(Transform holder, Renderer renderer)
        {
            var box = holder.gameObject.AddComponent<BoxCollider>();
            if (holder == renderer.transform) return box;

            var world = renderer.bounds;
            var local = new Bounds(holder.InverseTransformPoint(world.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var corner = world.center + Vector3.Scale(world.extents, new Vector3(i % 2 == 0 ? -1 : 1, i / 2 % 2 == 0 ? -1 : 1, i / 4 == 0 ? -1 : 1));
                local.Encapsulate(holder.InverseTransformPoint(corner));
            }
            box.center = local.center;
            box.size = local.size;
            return box;
        }

        private static void AddHoleSurfaces(SerializedProperty holes)
        {
            var have = new HashSet<string>();
            for (int i = 0; i < holes.arraySize; i++) have.Add(holes.GetArrayElementAtIndex(i).stringValue);

            foreach (var piece in ShotPieces)
            {
                if (!piece.Holes || piece.Prefix || have.Contains(piece.Name)) continue;
                holes.arraySize++;
                holes.GetArrayElementAtIndex(holes.arraySize - 1).stringValue = piece.Name;
                Did("Bullet holes now go on " + piece.Name);
            }
        }
    }
}
