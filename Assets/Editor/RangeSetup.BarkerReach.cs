using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    /// Step 88: the barker comes right out (user, 2026-11-13: "much further so he ends up hanging in the air next to the
    /// player"), to their side, not in front (the first try ended him 1 m in front and he slammed into them). In play he
    /// aims at the player's real head; this measures his fallback from the scene: his skull and the eyes it assumes. Reports anything solid on his way, as he passes through it rather than
    /// stopping
    public static partial class RangeSetup
    {
        // Front-left, in view: about 1.5 m away at 45° (user, 2026-11-13: 2 m to the side felt behind them)
        private const float BarkerBesidePlayer = 1.1f;
        private const float BarkerAheadOfPlayer = 1f;
        private const float BarkerLungeSeconds = 0.8f;
        // Back in at about the same speed as the old 1 m yank, not three times it
        private const float BarkerYankSeconds = 0.6f;
        private const int BarkerPathSamples = 12;

        [MenuItem("Shooting Range/Setup/88 - Barker Comes Right Out", false, 112)]
        public static void BarkerComesRightOutMenu()
        {
            Begin("Barker comes right out");
            try { BarkerComesRightOut(); }
            catch (System.Exception e) { Fail("Stopped by an error: " + e); }
            finally { End(); }
        }

        private static void BarkerComesRightOut()
        {
            var barker = Object.FindObjectOfType<SkeletonBarker>();
            if (barker == null) { Fail("No Skeleton Barker in the scene. Run step 64"); return; }

            var fields = new Fields(barker);
            var skull = fields.Ref("skull") as Rigidbody;
            var stand = fields.Ref("stand") as Rigidbody;
            if (skull == null || stand == null) { Fail("The barker has no skull or stand wired. Run step 65"); return; }

            var skullRenderer = skull.GetComponentInChildren<Renderer>();
            Vector3 head = skullRenderer != null ? skullRenderer.bounds.center : skull.position;
            Vector3 eye = EyePoint();

            // The barker's own numbers: in play he aims at the player's real head with them, and this is only his fallback.
            fields.Set("besidePlayer", BarkerBesidePlayer).Set("aheadOfPlayer", BarkerAheadOfPlayer);
            float beside = BarkerBesidePlayer, ahead = BarkerAheadOfPlayer;
            float above = fields.Find("aboveEyes").floatValue;
            var faces = Vector3.ProjectOnPlane(fields.Find("playerFaces").vector3Value, Vector3.up).normalized;
            var side = Vector3.Cross(Vector3.up, faces);
            if (Vector3.Dot(head - eye, side) < 0f) side = -side;
            Vector3 end = eye + side * beside + faces * ahead + Vector3.up * above;
            Vector3 carry = end - head;

            fields.Set("carry", carry).Set("shoveSeconds", BarkerLungeSeconds).Set("yankSeconds", BarkerYankSeconds).Apply();
            Dirty();

            Did("Carried " + carry.magnitude.ToString("0.00") + " m, in " + BarkerLungeSeconds + " s, slowing into the stop; back in " +
                BarkerYankSeconds + " s. His head ends at " + end.ToString("0.00") + ": " + beside + " m to the player's " +
                (Vector3.Dot(side, Vector3.Cross(faces, Vector3.up)) > 0f ? "left" : "right") + ", " + ahead + " m ahead, " + above +
                " m above their eyes (at " + eye.ToString("0.00") + "). In play he aims at the player's real head instead; this " +
                "is the fallback if it can't be found");
            SayWhatTheBarkerPasses(barker, carry);
        }

        /// He passes through things rather than hitting them, so this only says what he would pass through on his way out
        private static void SayWhatTheBarkerPasses(SkeletonBarker barker, Vector3 trip)
        {
            var renderers = barker.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var body = BoundsOf(renderers);
            var coffin = Object.FindObjectOfType<Coffin>();

            var passed = new HashSet<string>();
            for (int i = 1; i <= BarkerPathSamples; i++)
            {
                Vector3 at = body.center + trip * ((float)i / BarkerPathSamples);
                foreach (var hit in Physics.OverlapBox(at, body.extents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.transform.IsChildOf(barker.transform)) continue;
                    if (coffin != null && hit.transform.IsChildOf(coffin.transform)) continue;
                    passed.Add(hit.name);
                }
            }

            if (passed.Count == 0) Did("Nothing solid in his way out");
            else Say("On his way out he passes through (his whole outline, so arms and hat count): " + string.Join(", ", passed.OrderBy(n => n)));
        }
    }
}
