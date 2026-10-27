using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    public static partial class RangeSetup
    {
        [MenuItem("Shooting Range/Setup/29 - Light The Countdown Skulls", false, 51)]
        public static void CountdownSkullsMenu() { Begin("Countdown skulls"); LightTheCountdownSkulls(); End(); }

        private static readonly string[] CountdownSkullNames = { "Red Skull", "Orange Skull", "Green Skull" };
        private const string SkullModel = "Assets/Models/scull/scull_low.fbx";

        private const float EyeAcross = 0.218f;
        private const float EyeUp = 0.37f;
        private const float EyeWide = 0.31f;
        private const float EyeTall = 0.30f;
        private const float EyeDeep = 0.08f;
        private const float EyeIn = 0.80f;
        private const float FaceAt = 0.965f;
        private const float HaloSize = 0.45f;

        private const int CountdownBeats = 2;
        private const int DefaultCountdownBeats = 3;

        private static void LightTheCountdownSkulls()
        {
            var eyes = new List<Object>();
            foreach (var name in CountdownSkullNames)
            {
                var skull = RangeSetupUtil.FindInScene(name);
                if (skull == null) { Fail("No '" + name + "' in the scene. The three skulls are found by these names"); return; }

                var face = FindPart(skull.transform, "scull");
                if (face == null) { Fail(name + " has no scull_low mesh under it"); return; }

                if (skull.GetComponent<ShatterOnShot>() != null)
                    Say(name + " still shatters. Run 28b to make it flop like the others");
                else if (skull.GetComponent<ReleaseOnShot>() == null)
                {
                    BoxEachMesh(skull);
                    MakeItFlop(skull, Clips(WorldSounds + "env_wood_0", 4));
                    Did(name + " is knocked flying when shot");
                }

                var lit = Eyes(name, face);
                if (lit == null) return;
                eyes.Add(lit);
            }

            var driver = Object.FindObjectOfType<CountdownSkulls>();
            if (driver == null)
            {
                driver = new GameObject("CountdownSkulls").AddComponent<CountdownSkulls>();
                Did("CountdownSkulls added to the scene");
            }

            new Fields(driver).SetArray("eyes", eyes).Apply();
            Did("Countdown lights " + string.Join(" -> ", CountdownSkullNames) + " (the last on GO)");

            ShortenTheCountdown();
            Dirty();
        }

        private static GameObject Eyes(string skullName, Transform face)
        {
            var colour = face.GetComponent<Renderer>().sharedMaterial.color;
            string tag = skullName.Replace(" ", "");
            var bulb = EyeMaterial("SR_Eye_" + tag, "Unlit/Color", colour);
            var halo = EyeMaterial("SR_EyeGlow_" + tag, "CrystalCatch/Eye Glow", colour);
            if (bulb == null || halo == null) return null;

            var existing = RangeSetupUtil.Child(face, "Eyes");
            GameObject eyes;
            if (existing == null)
            {
                eyes = new GameObject("Eyes");
                eyes.transform.SetParent(face, false);
                eyes.SetActive(false);
            }
            else if (FirstFit(existing))
            {
                eyes = existing.gameObject;
                for (int i = existing.childCount - 1; i >= 0; i--) Object.DestroyImmediate(existing.GetChild(i).gameObject);
                existing.localPosition = Vector3.zero;
                existing.localRotation = Quaternion.identity;
                existing.localScale = Vector3.one;
            }
            else
            {
                Say(skullName + "'s eyes are already there, left alone");
                return existing.gameObject;
            }

            var size = face.GetComponent<MeshFilter>().sharedMesh.bounds;
            var oval = new Vector3(EyeWide * size.size.x, EyeTall * size.size.y, EyeDeep * size.size.z);

            for (int side = -1; side <= 1; side += 2)
            {
                float x = size.center.x + side * EyeAcross * size.size.x;
                float y = size.min.y + EyeUp * size.size.y;

                EyePart(PrimitiveType.Sphere, eyes.transform, new Vector3(x, y, size.min.z + EyeIn * size.size.z), oval, bulb);
                EyePart(PrimitiveType.Quad, eyes.transform, new Vector3(x, y, size.min.z + FaceAt * size.size.z),
                        Vector3.one * HaloSize * size.size.x, halo);
            }

            Did(skullName + (existing == null ? ": glowing eyes filling the sockets" : ": eyes refitted to fill the sockets"));
            return eyes;
        }

        /// The first fit used round balls, the refit's are ovals
        private static bool FirstFit(Transform eyes)
        {
            foreach (Transform part in eyes)
            {
                if (part.name == "Sphere") return Mathf.Approximately(part.localScale.x, part.localScale.y);
            }

            return true;
        }

        private static void EyePart(PrimitiveType shape, Transform parent, Vector3 at, Vector3 size, Material material)
        {
            var part = GameObject.CreatePrimitive(shape);
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = at;
            part.transform.localScale = size;

            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        }

        /// Made once and then left alone, so the glow can be tuned on the material
        private static Material EyeMaterial(string file, string shaderName, Color colour)
        {
            string path = "Assets/Materials/" + file + ".mat";
            var material = RangeSetupUtil.Load<Material>(path);
            if (material != null) return material;

            var shader = Shader.Find(shaderName);
            if (shader == null) { Fail("The " + shaderName + " shader has not compiled. Check the console for shader errors"); return null; }

            material = new Material(shader) { color = colour };
            AssetDatabase.CreateAsset(material, path);
            Did("Made " + file + ".mat");
            return material;
        }

        /// Three skulls, three beats, red, orange, then green on GO, so the count is one beat shorter than 3-2-1-GO
        private static void ShortenTheCountdown()
        {
            var game = Object.FindObjectOfType<RangeGame>();
            if (game == null) { Fail("No RangeGame in the scene"); return; }

            var fields = new Fields(game);
            foreach (var field in new[] { "countdownFrom", "betweenRoundsCountdownFrom" })
            {
                int beats = fields.Int(field);
                if (beats == DefaultCountdownBeats)
                {
                    fields.Set(field, CountdownBeats);
                    Did("RangeGame " + field + " " + DefaultCountdownBeats + " -> " + CountdownBeats);
                }
                else Say("RangeGame " + field + " is " + beats + ", left alone");
            }

            fields.Apply();
        }
    }
}
