using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    public static partial class RangeSetup
    {
        private const string FitMaskShader = "Hidden/RangeSetup/FitMask";
        private const string FitFolder = "RailFit";
        private const int MaskSize = 256;
        private const int SnapshotWidth = 1600;
        private const int SnapshotHeight = 900;
        private const float SnapshotFieldOfView = 75f;
        private const float NearEye = 0.2f;
        // A sign rises at the very start, and one that never travels stands there, so the ends are measured exactly
        private static readonly float[] FitStops = { 0f, 0.5f, 1f };

        [MenuItem("Shooting Range/Setup/31 - Check How Every Sign Fits", false, 54)]
        public static void FitMenu()
        {
            Begin("How every sign fits, from the player's eye");
            CheckTheFit();
            End();
        }

        private static void CheckTheFit()
        {
            var shader = Shader.Find(FitMaskShader);
            if (shader == null) { Fail(FitMaskShader + " has not compiled. Check the console for shader errors"); return; }

            int layer = FreeLayer();
            if (layer < 0) { Fail("No unnamed layer left to measure the signs on"); return; }

            var figure = PrefabFigure();
            if (figure == null) return;

            var posing = new Posing(figure);
            var player = PlayerTransform();
            var eye = EyePoint();

            var rails = new List<TrackRail>(Object.FindObjectsOfType<TrackRail>());
            rails.Sort((a, b) => a.Group != b.Group ? a.Group.CompareTo(b.Group) : a.Origin.y.CompareTo(b.Origin.y));

            var room = new List<Renderer>();
            foreach (var renderer in Object.FindObjectsOfType<MeshRenderer>())
            {
                if (renderer.transform.root.name != PreviewName) room.Add(renderer);
            }

            // The rig's own head would fill the view from inside it, and the lasers keep whatever line they
            // last drew in the editor, which in play is off until a gun is aimed (AimSight.Awake)
            var hide = new List<Renderer>();
            foreach (var renderer in Object.FindObjectsOfType<Renderer>())
            {
                var box = renderer.bounds;
                if (box.size.magnitude <= 1f && box.SqrDistance(eye) <= NearEye * NearEye) hide.Add(renderer);
            }

            foreach (var sight in Object.FindObjectsOfType<AimSight>())
            {
                var fields = new Fields(sight);
                var beam = fields.Ref("beam") as Renderer;
                var dot = fields.Ref("dot") as Transform;
                if (beam != null) hide.Add(beam);
                if (dot != null) hide.AddRange(dot.GetComponentsInChildren<Renderer>());
            }

            var hidden = new List<Renderer>();
            foreach (var renderer in hide)
            {
                if (!renderer.enabled || hidden.Contains(renderer)) continue;

                renderer.enabled = false;
                hidden.Add(renderer);
            }

            string folder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp", FitFolder);
            Directory.CreateDirectory(folder);

            var root = new GameObject("RangeSetup Fit") { hideFlags = HideFlags.DontSave };
            var camera = new GameObject("Eye", typeof(Camera)).GetComponent<Camera>();
            camera.transform.SetParent(root.transform, false);
            camera.transform.position = eye;
            camera.enabled = false;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 60f;

            var mask = new RenderTexture(MaskSize, MaskSize, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var read = new Texture2D(MaskSize, MaskSize, TextureFormat.RGBA32, false, true);

            try
            {
                Say("Eye at " + eye.ToString("F2") + ". In view = share of the sign the player sees; " +
                    "hidden to = how far up its height the cover reaches (0% = its bottom edge shows)");

                foreach (var rail in rails)
                {
                    Say(rail.name + " (" + rail.Group + ", y " + rail.Origin.y.ToString("0.00") + ")");

                    for (int i = 0; i < figure.childCount; i++)
                        FitOneSign(figure.GetChild(i), figure.localScale, rail, root.transform, camera, mask, read,
                                   shader, layer, player, posing, room);

                    Snapshot(figure, rail, root.transform, camera, player, posing, folder);
                }

                Say("Snapshots from the eye, one per rail: " + folder);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = null;
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(mask);
                Object.DestroyImmediate(read);
                foreach (var renderer in hidden) renderer.enabled = true;
                ClosePrefab(false);
            }
        }

        /// The sign at the start, middle and end of the rail, up and folded, reported by its worst case
        private static void FitOneSign(Transform sign, Vector3 figureScale, TrackRail rail, Transform root,
                                       Camera camera, RenderTexture mask, Texture2D read, Shader shader, int layer,
                                       Transform player, Posing posing, List<Renderer> room)
        {
            bool folds = rail.Hinge != Hinge.None;
            float leastSeen = 1f, hiddenTo = 0f, foldedSeen = 0f, tall = 0f;
            string stand = "";

            for (int stop = 0; stop < FitStops.Length; stop++)
            {
                for (int pose = 0; pose < (folds ? 2 : 1); pose++)
                {
                    bool folded = pose == 1;
                    var holder = new GameObject("pose").transform;
                    holder.SetParent(root, false);

                    var stage = Stand(sign, figureScale, holder, rail, rail.Length * FitStops[stop], player, posing, folded);
                    if (stage == null)
                    {
                        Object.DestroyImmediate(holder.gameObject);
                        Say("   " + sign.name.PadRight(16) + " too tall for this rail");
                        return;
                    }

                    float seen, lowest;
                    Look(camera, stage, mask, read, shader, layer, out seen, out lowest);

                    if (folded) foldedSeen = Mathf.Max(foldedSeen, seen);
                    else
                    {
                        leastSeen = Mathf.Min(leastSeen, seen);
                        hiddenTo = Mathf.Max(hiddenTo, lowest);
                        tall = BoundsOf(stage.GetComponentsInChildren<Renderer>()).size.y;
                        if (stop == FitStops.Length / 2) stand = StandGap(holder, rail, room);
                    }

                    Object.DestroyImmediate(holder.gameObject);
                }
            }

            Say("   " + sign.name.PadRight(16) + tall.ToString("0.00") + " m  up: " + Percent(leastSeen) + " in view, hidden to " +
                Percent(hiddenTo) + (folds ? "  folded: " + Percent(foldedSeen) + " in view" : "") + stand);
        }

        /// The share of the sign the player can see, and how far up its height its lowest visible point is
        private static void Look(Camera camera, GameObject stage, RenderTexture mask, Texture2D read, Shader shader,
                                 int layer, out float seen, out float lowest)
        {
            var renderers = stage.GetComponentsInChildren<Renderer>();
            var box = BoundsOf(renderers);

            var block = new MaterialPropertyBlock();
            block.SetFloat("_FitSign", 1f);
            block.SetFloat("_FitLow", box.min.y);
            block.SetFloat("_FitHigh", box.max.y);
            foreach (var renderer in renderers)
            {
                renderer.SetPropertyBlock(block);
                renderer.gameObject.layer = layer;
            }

            var toSign = box.center - camera.transform.position;
            camera.transform.rotation = Quaternion.LookRotation(toSign);
            camera.fieldOfView = Mathf.Clamp(2.4f * Mathf.Atan(box.extents.magnitude / toSign.magnitude) * Mathf.Rad2Deg, 2f, 120f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.targetTexture = mask;

            int visible, total;
            float ignored;

            camera.cullingMask = ~0;
            camera.RenderWithShader(shader, "RenderType");
            Count(mask, read, out visible, out lowest);

            camera.cullingMask = 1 << layer;
            camera.RenderWithShader(shader, "RenderType");
            Count(mask, read, out total, out ignored);

            seen = total > 0 ? (float)visible / total : 0f;
        }

        private static void Count(RenderTexture mask, Texture2D read, out int count, out float lowest)
        {
            RenderTexture.active = mask;
            read.ReadPixels(new Rect(0, 0, mask.width, mask.height), 0, 0);
            RenderTexture.active = null;

            count = 0;
            lowest = 1f;
            foreach (var pixel in read.GetPixels32())
            {
                if (pixel.g < 128) continue;

                count++;
                lowest = Mathf.Min(lowest, pixel.r / 255f);
            }
        }

        /// How far the stand's foot is from what it should rest on: whatever is under it, or the ceiling
        /// over a roof sign. Measured against bounding boxes, because the room's models have no colliders
        private static string StandGap(Transform holder, TrackRail rail, List<Renderer> room)
        {
            Transform bracket = null;
            foreach (Transform child in holder)
            {
                if (child.name.EndsWith(" stand")) bracket = child;
            }

            if (bracket == null) return "";

            bool roof = rail.Hinge == Hinge.Down;
            var box = BoundsOf(bracket.GetComponentsInChildren<Renderer>());
            var foot = new Vector3(box.center.x, roof ? box.max.y : box.min.y, box.center.z);

            float best = roof ? float.PositiveInfinity : float.NegativeInfinity;
            Renderer surface = null;
            foreach (var renderer in room)
            {
                if (renderer == null) continue;

                var b = renderer.bounds;
                if (foot.x < b.min.x || foot.x > b.max.x || foot.z < b.min.z || foot.z > b.max.z) continue;

                float face = roof ? b.min.y : b.max.y;
                bool beyond = roof ? face >= foot.y - 0.01f && face < best : face <= foot.y + 0.01f && face > best;
                if (!beyond) continue;

                best = face;
                surface = renderer;
            }

            if (surface == null) return "  stand: nothing " + (roof ? "over" : "under") + " it";

            string name = surface.transform.parent != null ? surface.transform.parent.name + "/" + surface.name : surface.name;
            return "  stand: foot " + Mathf.Abs(foot.y - best).ToString("0.00") + " m " + (roof ? "under " : "over ") + name;
        }

        /// One of each sign spread along the rail, up, as the player would see them
        private static void Snapshot(Transform figure, TrackRail rail, Transform root, Camera camera, Transform player,
                                     Posing posing, string folder)
        {
            var holder = new GameObject("snapshot").transform;
            holder.SetParent(root, false);

            int count = figure.childCount;
            for (int i = 0; i < count; i++)
                Stand(figure.GetChild(i), figure.localScale, holder, rail, rail.Length * (i + 0.5f) / count, player, posing, false);

            var renderers = holder.GetComponentsInChildren<Renderer>();
            var look = renderers.Length > 0 ? BoundsOf(renderers).center : rail.PointAt(rail.Length * 0.5f);

            var shot = new RenderTexture(SnapshotWidth, SnapshotHeight, 24);
            var image = new Texture2D(SnapshotWidth, SnapshotHeight, TextureFormat.RGB24, false);

            try
            {
                camera.transform.rotation = Quaternion.LookRotation(look - camera.transform.position);
                camera.fieldOfView = SnapshotFieldOfView;
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.cullingMask = ~0;
                camera.targetTexture = shot;
                camera.Render();

                RenderTexture.active = shot;
                image.ReadPixels(new Rect(0, 0, SnapshotWidth, SnapshotHeight), 0, 0);
                RenderTexture.active = null;

                File.WriteAllBytes(Path.Combine(folder, rail.name + ".png"), image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                Object.DestroyImmediate(shot);
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(holder.gameObject);
            }
        }

        private static Bounds BoundsOf(Renderer[] renderers)
        {
            var box = renderers.Length > 0 ? renderers[0].bounds : new Bounds();
            for (int i = 1; i < renderers.Length; i++) box.Encapsulate(renderers[i].bounds);
            return box;
        }

        private static int FreeLayer()
        {
            for (int i = 31; i >= 8; i--)
            {
                if (string.IsNullOrEmpty(LayerMask.LayerToName(i))) return i;
            }

            return -1;
        }

        private static string Percent(float share) { return Mathf.RoundToInt(share * 100f) + "%"; }
    }
}
