using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    /// Step 77: measures what the tent ceiling has to fit (plan: LiminalVR/CrystalCatch/plans/tent-ceiling-2026-11-13.md).
    /// Reads only; nothing in the scene changes
    public static partial class RangeSetup
    {
        // Just under Alex's 4.18 m ceiling planes, so the casts find the walls and not the planes
        private const float RoomProbeHeight = 3.9f;
        private static readonly float[] RoomProbeX = { -4f, 0f, 4f };
        private static readonly float[] RoomProbeZ = { -4f, 0f, 4f, 8f, 12f };

        [MenuItem("Shooting Range/Setup/77 - Measure The Room For The Tent (measure only)", false, 100)]
        public static void MeasureTheRoomMenu()
        {
            Begin("Room for the tent ceiling, measured only, nothing changed");
            try { MeasureTheRoom(); }
            catch (System.Exception e) { Fail("Stopped by an error: " + e); }
            finally { End(); }
        }

        private static void MeasureTheRoom()
        {
            var eye = EyePoint();
            Say("Eye (fitting) at " + eye.ToString("F2"));
            SayBoundsOf("Ceiling planes (Alex)", "floor (", 3f);
            SayBoundsOf("Wood beams (Alex)", "wood_beam", 3f);
            SayBoundsOf("Lantern (Alex)", "lantern", 0f);

            foreach (float z in RoomProbeZ)
            {
                Say("At z " + z.ToString("0") + ", y " + RoomProbeHeight + ": left wall " + Probe(new Vector3(0f, RoomProbeHeight, z), Vector3.left) +
                    ", right wall " + Probe(new Vector3(0f, RoomProbeHeight, z), Vector3.right));
            }

            foreach (float x in RoomProbeX)
            {
                Say("At x " + x.ToString("0") + ", y " + RoomProbeHeight + ": front (−z) " + Probe(new Vector3(x, RoomProbeHeight, 4f), Vector3.back) +
                    ", back (+z) " + Probe(new Vector3(x, RoomProbeHeight, 4f), Vector3.forward) +
                    ", ceiling above " + Probe(new Vector3(x, RoomProbeHeight, 4f), Vector3.up));
            }

            foreach (var rail in Object.FindObjectsOfType<TrackRail>())
            {
                if (rail.Group != TrackGroup.RoofAcross && rail.Group != TrackGroup.RoofAlong) continue;
                Say("Roof rail " + rail.name + ": " + rail.PointAt(0f).ToString("F2") + " to " + rail.PointAt(rail.Length).ToString("F2"));
            }

            foreach (var skylight in new[] { "Skylight Floor_Across_1", "Skylight Floor_Across_2" })
            {
                var found = RangeSetupUtil.FindInScene(skylight);
                Say(skylight + (found != null ? " at " + found.transform.position.ToString("F2") : ": not in the scene"));
            }

            var bench = RangeSetupUtil.FindInScene("ShootingBench");
            if (bench != null) Say("ShootingBench bounds " + BoundsText(bench.GetComponentsInChildren<Renderer>()));

            SayWhatMetTheOldCeiling();
        }

        // The old ceiling's underside, and how close below it a top counts as having met it
        private const float OldCeiling = 4.18f;
        private const float MetCeilingWithin = 0.35f;

        /// Pillars and anything else inside the room whose top reached Alex's ceiling: with the tent higher they stop short
        /// (user, 2026-11-13). One line each, so the tent model can carry a post from each top up to the beam or canvas
        private static void SayWhatMetTheOldCeiling()
        {
            var room = new Bounds();
            room.SetMinMax(new Vector3(-6.2f, 0f, -2.9f), new Vector3(6.6f, 10f, 13.2f));
            var seen = new HashSet<Transform>();

            foreach (var renderer in Object.FindObjectsOfType<MeshRenderer>())
            {
                if (!renderer.enabled || renderer.GetComponentInParent<TrackMover>() != null) continue;

                var bounds = renderer.bounds;
                if (bounds.max.y < OldCeiling - MetCeilingWithin || bounds.max.y > OldCeiling + 0.05f) continue;
                if (!room.Contains(new Vector3(bounds.center.x, 1f, bounds.center.z))) continue;

                var holder = renderer.transform.root == renderer.transform ? renderer.transform : renderer.transform.parent;
                if (!seen.Add(renderer.transform)) continue;
                Say("Met the old ceiling: " + renderer.name + " (" + holder.name + ") " + BoundsText(bounds));
            }
        }

        /// The nearest solid surface along a direction, ignoring signs, props that move and anything shootable
        private static string Probe(Vector3 from, Vector3 direction)
        {
            float nearest = float.MaxValue;
            RaycastHit best = default(RaycastHit);
            foreach (var hit in Physics.RaycastAll(from, direction, 30f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance >= nearest || hit.rigidbody != null || hit.collider.GetComponentInParent<IShootable>() != null ||
                    hit.collider.GetComponentInParent<TrackMover>() != null) continue;

                nearest = hit.distance;
                best = hit;
            }

            return nearest < float.MaxValue
                 ? best.point.ToString("F2") + " (" + best.collider.name + ")"
                 : "nothing within 30 m";
        }

        private static void SayBoundsOf(string label, string prefix, float above)
        {
            int count = 0;
            var all = new Bounds();
            foreach (var renderer in Object.FindObjectsOfType<MeshRenderer>())
            {
                if (!renderer.enabled || renderer.bounds.max.y < above) continue;

                bool named = false;
                for (var t = renderer.transform; t != null && !named; t = t.parent) named = t.name.StartsWith(prefix);
                if (!named) continue;

                if (count++ == 0) all = renderer.bounds;
                else all.Encapsulate(renderer.bounds);
            }

            Say(label + ": " + (count == 0 ? "none" : count + " meshes, " + BoundsText(all)));
        }

        private static string BoundsText(Renderer[] renderers)
        {
            return renderers.Length == 0 ? "none" : BoundsText(BoundsOf(renderers));
        }

        private static string BoundsText(Bounds bounds)
        {
            return "x " + bounds.min.x.ToString("0.00") + " to " + bounds.max.x.ToString("0.00") +
                   ", y " + bounds.min.y.ToString("0.00") + " to " + bounds.max.y.ToString("0.00") +
                   ", z " + bounds.min.z.ToString("0.00") + " to " + bounds.max.z.ToString("0.00");
        }
    }
}
