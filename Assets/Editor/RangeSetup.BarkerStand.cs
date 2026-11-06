using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    public static partial class RangeSetup
    {
        private const float BarkerDrag = 0.2f;
        private const float BarkerAngularDrag = 1.5f;
        private const int BarkerSolverIterations = 12;
        private const float HatMass = 0.3f;

        private static readonly string[] BarkerMarkers =
            { "Body", "Waist", "Neck", "Jaw Hinge", "Hat", "Shoulder L", "Elbow L", "Wrist L", "Shoulder R", "Elbow R", "Wrist R" };

        /// Piece, what it hangs from, its mass (kg), swing and twist limits (degrees). Floppy, but no limb folds through itself
        private struct BarkerBone
        {
            public string Piece, Parent;
            public float Mass, Swing, Twist;
            public BarkerBone(string piece, string parent, float mass, float swing, float twist)
            {
                Piece = piece; Parent = parent; Mass = mass; Swing = swing; Twist = twist;
            }
        }

        private static readonly BarkerBone[] BarkerBones =
        {
            new BarkerBone("Torso", null, 4f, 45f, 15f),
            new BarkerBone("Skull", "Torso", 1.2f, 40f, 25f),
            new BarkerBone("Upper Arm L", "Torso", 0.6f, 90f, 40f), new BarkerBone("Forearm L", "Upper Arm L", 0.4f, 80f, 20f),
            new BarkerBone("Hand L", "Forearm L", 0.2f, 40f, 15f),
            new BarkerBone("Upper Arm R", "Torso", 0.6f, 90f, 40f), new BarkerBone("Forearm R", "Upper Arm R", 0.4f, 80f, 20f),
            new BarkerBone("Hand R", "Forearm R", 0.2f, 40f, 15f),
            new BarkerBone("Thigh L", "Torso", 1f, 70f, 20f), new BarkerBone("Shin L", "Thigh L", 0.6f, 70f, 10f),
            new BarkerBone("Foot L", "Shin L", 0.3f, 30f, 10f),
            new BarkerBone("Thigh R", "Torso", 1f, 70f, 20f), new BarkerBone("Shin R", "Thigh R", 0.6f, 70f, 10f),
            new BarkerBone("Foot R", "Shin R", 0.3f, 30f, 10f),
        };

        // Where each piece hangs from, by step 64's markers. The legs have none, so they're read from the piece itself
        private static readonly Dictionary<string, string> BarkerPivots = new Dictionary<string, string>
        {
            { "Torso", "Waist" }, { "Skull", "Neck" },
            { "Upper Arm L", "Shoulder L" }, { "Forearm L", "Elbow L" }, { "Hand L", "Wrist L" },
            { "Upper Arm R", "Shoulder R" }, { "Forearm R", "Elbow R" }, { "Hand R", "Wrist R" },
        };

        [MenuItem("Shooting Range/Setup/65 - Floppy Barker On His Stand", false, 88)]
        public static void FloppyBarkerMenu()
        {
            Begin("Floppy barker on his stand");
            HangTheBarker();
            End();
        }

        private static void HangTheBarker()
        {
            var barker = Object.FindObjectOfType<SkeletonBarker>();
            if (barker == null) { Fail("No Skeleton Barker in the scene. Run step 64"); return; }

            var root = barker.transform;
            var stand = FindTheStand(root);
            if (stand == null) return;

            var all = root.GetComponentsInChildren<Transform>(true);
            var named = new Dictionary<string, Transform>();
            foreach (var item in all)
                if (!named.ContainsKey(item.name)) named[item.name] = item;

            var missing = BarkerBones.Select(b => b.Piece).Concat(BarkerPivots.Values).Concat(new[] { "Jaw Hinge", "Hat" })
                                     .Where(n => !named.ContainsKey(n)).ToArray();
            if (missing.Length > 0) { Fail("The barker has no " + string.Join(", ", missing) + ". Run step 64 again"); return; }

            named["Jaw Hinge"].SetParent(named["Skull"], true);
            named["Hat"].SetParent(named["Skull"], true);
            var rootBody = root.GetComponent<Rigidbody>();
            if (rootBody != null) Object.DestroyImmediate(rootBody);

            var standBody = RangeSetupUtil.Ensure<Rigidbody>(stand.gameObject);
            standBody.isKinematic = true;
            standBody.useGravity = false;
            standBody.interpolation = RigidbodyInterpolation.Interpolate;

            foreach (var bone in BarkerBones)
            {
                var piece = named[bone.Piece];
                var body = RangeSetupUtil.Ensure<Rigidbody>(piece.gameObject);
                body.mass = bone.Mass;
                body.drag = BarkerDrag;
                body.angularDrag = BarkerAngularDrag;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.solverIterations = BarkerSolverIterations;
            }

            foreach (var bone in BarkerBones)
            {
                var piece = named[bone.Piece];
                string marker;
                Vector3 pivot = BarkerPivots.TryGetValue(bone.Piece, out marker) ? named[marker].position : TopIn(root, piece);
                var parent = bone.Parent != null ? named[bone.Parent].GetComponent<Rigidbody>() : standBody;
                HangFrom(piece, parent, pivot, root, bone);
            }

            var hat = RangeSetupUtil.Ensure<Rigidbody>(named["Hat"].gameObject);
            hat.isKinematic = true;
            hat.mass = HatMass;
            hat.interpolation = RigidbodyInterpolation.None;

            var outward = Vector3.ProjectOnPlane(EyePoint() - stand.position, Vector3.up).normalized;
            new Fields(barker).Set("stand", standBody).Set("skull", named["Skull"].GetComponent<Rigidbody>()).Set("hat", hat)
                .Set("director", Object.FindObjectOfType<TrackDirector>()).Set("outward", outward).Apply();

            Dirty();
            Did("Stand: " + stand.name + ", a kinematic body the barker moves. He hangs from it by the pelvis");
            Did(BarkerBones.Length + " pieces on character joints, from " + BarkerBones.Min(b => b.Swing) + " to " +
                BarkerBones.Max(b => b.Swing) + " degrees of swing. Tune their limits, mass and drag on the pieces");
            Did("The hat rides the skull and can be thrown off; the jaw rides it and talks. He's pushed toward the player at " +
                stand.position.ToString("0.0"));
        }

        private static Transform FindTheStand(Transform root)
        {
            var built = new HashSet<string>(BarkerMarkers.Concat(BarkerPieces.Keys));
            var found = root.GetComponentsInChildren<Transform>(true).Where(t => t != root && AttachedBy(t, root, built)).ToArray();

            if (found.Length > 1)
            {
                Fail("More than one thing is attached to the barker (" + string.Join(", ", found.Select(t => t.name).ToArray()) +
                     "). Put the stand's parts under one object");
                return null;
            }

            Transform stand = found.Length == 1 ? found[0] : null;
            if (stand == null && root.parent != null && root.parent.GetComponentInChildren<MeshRenderer>(true) != null &&
                root.parent.GetComponent<Coffin>() == null)
            {
                stand = root.parent;
                root.SetParent(stand.parent, true);
                Did("The barker was under " + stand.name + "; he's moved out from under it so it can carry him");
            }

            if (stand == null) { Fail("No stand attached. Put it under " + root.name + " and run this again"); return null; }

            if (stand.parent != root)
            {
                stand.SetParent(root, true);
                Did(stand.name + " moved up to sit directly under " + root.name + ", where it can move without dragging him");
            }

            return stand;
        }

        private static bool AttachedBy(Transform item, Transform root, HashSet<string> built)
        {
            if (built.Contains(item.name)) return false;

            for (var above = item.parent; above != root; above = above.parent)
                if (above == null || above.name == "Hat" || !built.Contains(above.name)) return false;

            return true;
        }

        private static void HangFrom(Transform piece, Rigidbody parent, Vector3 pivot, Transform root, BarkerBone bone)
        {
            var joint = RangeSetupUtil.Ensure<CharacterJoint>(piece.gameObject);
            joint.connectedBody = parent;
            joint.autoConfigureConnectedAnchor = true;
            joint.anchor = piece.InverseTransformPoint(pivot);

            var box = PieceBox(piece);
            Vector3 along = (box.center - pivot).normalized;
            Vector3 swing = Vector3.ProjectOnPlane(root.right, along);
            if (swing.sqrMagnitude < 1e-4f) swing = Vector3.ProjectOnPlane(root.forward, along);

            joint.axis = piece.InverseTransformDirection(along);
            joint.swingAxis = piece.InverseTransformDirection(swing.normalized);
            joint.lowTwistLimit = new SoftJointLimit { limit = -bone.Twist };
            joint.highTwistLimit = new SoftJointLimit { limit = bone.Twist };
            joint.swing1Limit = new SoftJointLimit { limit = bone.Swing };
            joint.swing2Limit = new SoftJointLimit { limit = bone.Swing };
            joint.enableProjection = true;
        }

        /// The top end of a piece in the barker's own up, which a coffin tilted back would skew in the world's
        private static Vector3 TopIn(Transform space, Transform piece)
        {
            var points = PiecePoints(piece).Select(p => space.InverseTransformPoint(p)).ToArray();
            float top = points.Max(p => p.y);
            float cut = top - (top - points.Min(p => p.y)) * JointTopShare;
            var near = points.Where(p => p.y >= cut).ToArray();
            return space.TransformPoint(near.Aggregate(Vector3.zero, (sum, p) => sum + p) / near.Length);
        }
    }
}
