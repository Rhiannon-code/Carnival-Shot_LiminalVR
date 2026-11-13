using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    /// Step 74 (ADR 0073): harder rounds and a punchier hit. The signs' pace (shared with step 19), louder sign voices and
    /// reactions, a bat puff no bigger than the bat, and on every sign hit an impact star, a ring, wood chunks, a little
    /// dust and the score rising off it. The effects are made once and then left alone: retune them in the Inspector
    public static partial class RangeSetup
    {
        private const float SignVoiceVolume = 0.8f;
        private const float SignReactionVolume = 0.8f;
        private const int SignSplinterTimes = 3;
        // The puff is about six times this across, against a bat's body of about 12 cm (52 cm wingspan, ADR 0055)
        private const float BatPopScale = 0.02f;
        private const string ScorePopupsName = "Score Popups";

        [MenuItem("Shooting Range/Setup/74 - Harder Rounds And Hit Feel", false, 97)]
        public static void HarderRoundsMenu()
        {
            Begin("Harder rounds and hit feel");
            PaceTheSigns();
            LouderSigns();
            SmallBatPuff();
            SignHitEffects();
            AddTheScorePopups();
            Dirty();
            End();
        }

        /// The director and the signs' holds, for steps 19 and 74 both
        private static void PaceTheSigns()
        {
            var director = Object.FindObjectOfType<TrackDirector>();
            if (director != null)
            {
                new Fields(director).Set("signsPerRound", new[] { 15, 19, 34, 40, 61 })
                                    .Set("waveInterval", 1.6f).Set("waveScalePerRound", 0.85f)
                                    .Set("minSpeed", CrossMinSpeed).Set("maxSpeed", CrossMaxSpeed)
                                    .Set("speedScalePerRound", 1.12f)
                                    .Set("concurrentTargets", 4).Set("extraConcurrentPerRound", 1)
                                    .Set("maxConcurrentTargets", 7).Apply();
                Did("Track Director: 15 / 19 / 34 / 40 / 61 signs, 4 / 5 / 6 / 7 / 7 up at once, speed x1.12 a round, " +
                    "waves every 1.6 s x0.85 a round, or a beat after the last sign up has gone");
            }
            else Fail("No TrackDirector in the scene");

            var prefab = OpenPrefab();
            var mover = prefab != null ? prefab.GetComponent<TrackMover>() : null;
            if (mover != null)
            {
                new Fields(mover).Set("minHold", 2f).Set("maxHold", 3.5f).Apply();
                ClosePrefab(true);
                Did("TrackMover.prefab: a sign that stands still is up 2 to 3.5 s");
            }
            else { Fail("No TrackMover on " + PrefabPath); ClosePrefab(false); }
        }

        private static void LouderSigns()
        {
            var prefab = OpenPrefab();
            if (prefab == null) return;

            var target = prefab.GetComponentInChildren<PopUpTarget>(true);
            var reactions = prefab.GetComponentInChildren<SignReactions>(true);
            if (target != null) new Fields(target).Set("voiceVolume", SignVoiceVolume).Apply();
            if (reactions != null) new Fields(reactions).Set("volume", SignReactionVolume).Apply();
            ClosePrefab(true);

            if (target != null) Did("TrackMover.prefab: sign voices at " + SignVoiceVolume + " (wood hit 0.9 is the code's default)");
            else Fail("No PopUpTarget in " + PrefabPath);
            if (reactions != null) Did("TrackMover.prefab: reaction sounds at " + SignReactionVolume);
            else Say("No SignReactions in " + PrefabPath + ", its volume left alone");
        }

        private static void SmallBatPuff()
        {
            var bats = Object.FindObjectOfType<BatSwarm>();
            if (bats == null) { Say("No BatSwarm in the scene"); return; }

            new Fields(bats).Set("popScale", BatPopScale).Apply();
            Did("Bats: puff at x" + BatPopScale + ", about 12 cm across at most, no bigger than the body");
        }

        private static void SignHitEffects()
        {
            var debris = Object.FindObjectOfType<Debris>();
            var holder = debris != null ? RangeSetupUtil.Child(debris.transform, DebrisName) : null;
            if (holder == null) { Fail("No " + DebrisName + " under Impact FX. Run step 41 first"); return; }

            var fields = new SerializedObject(debris);
            fields.FindProperty("signTimes").intValue = SignSplinterTimes;
            var list = fields.FindProperty("kinds");

            var splinter = ShaderMaterial(SplinterMaterial, SplinterShader, Color.white);
            var pow = FxMaterial("Assets/Materials/SR_MuzzleFlash.mat", "Mobile/Particles/Additive", FxTexture(FxTextures + "T_SR_MuzzlePow.png", 256));
            var smoke = FxMaterial("Assets/Materials/SR_MuzzleSmoke.mat", "Mobile/Particles/Alpha Blended", FxTexture(FxTextures + "T_SR_MuzzleSmoke_4x4.png", 512));
            var ring = EyeMaterial("SR_KegRing", "Mobile/Particles/Additive", Color.white);
            if (ring != null && ring.mainTexture == null)
            {
                ring.mainTexture = RangeSetupUtil.Load<Texture2D>(KegRingArt);
                EditorUtility.SetDirty(ring);
            }

            AddDebrisKind(list, holder, DebrisKind.Chunks, 3, 5, () => MakeDebrisSystem(holder, splinter, new DebrisLook
            {
                Kind = DebrisKind.Chunks, LifeMin = 0.6f, LifeMax = 1f, SpeedMin = 1.5f, SpeedMax = 3.5f, Gravity = 1.5f, ConeAngle = 45f,
                ColourA = new Color(0.8f, 0.63f, 0.4f), ColourB = new Color(0.5f, 0.34f, 0.2f),
                WidthMin = 0.02f, WidthMax = 0.035f, LengthMin = 0.03f, LengthMax = 0.06f, Tumble = 10f,
            }));
            AddDebrisKind(list, holder, DebrisKind.ImpactStar, 1, 1, () =>
                Flash(holder, "ImpactStar", pow, 0.12f, 0.22f, 0.3f, 0.6f, 1.3f, new Color(1f, 0.9f, 0.6f)));
            AddDebrisKind(list, holder, DebrisKind.ImpactRing, 1, 1, () =>
                Flash(holder, "ImpactRing", ring, 0.2f, 0.4f, 0.4f, 0.25f, 1f, new Color(1f, 0.85f, 0.55f, 0.8f)));
            AddDebrisKind(list, holder, DebrisKind.Dust, 2, 3, () => Dust(holder, smoke));

            fields.ApplyModifiedPropertiesWithoutUndo();
            Did("Debris: splinters x" + SignSplinterTimes + " off a sign");
        }

        private static void AddDebrisKind(SerializedProperty list, Transform holder, DebrisKind kind, int fewest, int most,
                                          System.Func<ParticleSystem> make)
        {
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).FindPropertyRelative("kind").intValue != (int)kind) continue;
                Say(kind + " is already there, left alone");
                return;
            }

            var system = make();
            if (system == null) { Fail(kind + ": its material is missing"); return; }

            list.arraySize++;
            var entry = list.GetArrayElementAtIndex(list.arraySize - 1);
            entry.FindPropertyRelative("kind").intValue = (int)kind;
            entry.FindPropertyRelative("system").objectReferenceValue = system;
            entry.FindPropertyRelative("fewest").intValue = fewest;
            entry.FindPropertyRelative("most").intValue = most;
            Did("Debris: " + kind + ", " + fewest + (most > fewest ? "-" + most : "") + " a sign hit");
        }

        /// Always playing with nothing to emit, like the splinters, so Debris can move it to a hit and emit
        private static ParticleSystem Emitter(Transform parent, string name, Material material, int max)
        {
            if (material == null) return null;

            var system = new GameObject(name).AddComponent<ParticleSystem>();
            system.transform.SetParent(parent, false);
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var emission = system.emission;
            emission.rateOverTime = 0f;

            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return system;
        }

        /// A still flash that grows and fades where the round struck
        private static ParticleSystem Flash(Transform parent, string name, Material material, float life,
                                            float sizeMin, float sizeMax, float growFrom, float growTo, Color colour)
        {
            var system = Emitter(parent, name, material, 6);
            if (system == null) return null;

            var main = system.main;
            main.startLifetime = life;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = colour;
            var shape = system.shape;
            shape.enabled = false;
            Grow(system, growFrom, growTo);
            FadeOut(system, 0f);
            return system;
        }

        /// Kept tiny on purpose (user, 2026-11-13: smoke must stay very small): 3-6 cm, growing to 8 cm at most, barely drifting
        private static ParticleSystem Dust(Transform parent, Material material)
        {
            var system = Emitter(parent, "Dust", material, 12);
            if (system == null) return null;

            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
            main.startColor = new Color(0.75f, 0.68f, 0.58f, 0.45f);

            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 30f;
            shape.radius = 0.01f;

            Grow(system, 0.8f, 1.4f);
            FadeOut(system, 0.1f);
            var sheet = system.textureSheetAnimation;
            sheet.enabled = true;
            sheet.numTilesX = 4;
            sheet.numTilesY = 4;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            return system;
        }

        private static void AddTheScorePopups()
        {
            var game = Object.FindObjectOfType<RangeGame>();
            if (game == null) { Fail("No RangeGame in the scene"); return; }
            if (Object.FindObjectOfType<ScorePopups>() != null) { Say("Score pop-ups are already there, left alone"); return; }

            var popups = new GameObject(ScorePopupsName).AddComponent<ScorePopups>();
            new Fields(popups).Set("game", game).Set("font", CardFont()).Apply();
            Did(ScorePopupsName + ": \"+points\" rises off each sign that is shot");
        }
    }
}
