using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    /// Step 72: a flash and a shockwave ring for each powder keg, in place of the cartoon fireball (client feedback,
    /// 2026-11-10). Particle systems on the keg, tunable there. Fills blanks: a keg that has one is left alone
    public static partial class RangeSetup
    {
        private const string KegRingArt = "Assets/Textures/Prize/keg_ring.png";
        private const string BlastFxName = "Blast FX";

        [MenuItem("Shooting Range/Setup/72 - Keg Blast Flash And Ring", false, 95)]
        public static void KegBlastMenu()
        {
            Begin("Keg blast");
            int built = 0;
            foreach (var keg in Object.FindObjectsOfType<PowderKeg>())
            {
                var fields = new Fields(keg);
                if (fields.Ref("blastFx") != null) { Say(keg.name + " already has its blast"); continue; }

                fields.Set("blastFx", KegBlast(keg.transform)).Apply();
                built++;
            }
            if (built > 0) { Did(built + " keg(s): a flash and a shockwave ring, no fireball"); Dirty(); }
            End();
        }

        private static ParticleSystem KegBlast(Transform keg)
        {
            var flash = NewParticles(BlastFxName, keg, keg.position, Quaternion.identity);
            var main = flash.main;
            main.duration = 0.2f;
            main.startLifetime = 0.15f;
            main.startSpeed = 0f;
            main.startSize = 1.6f;
            main.startColor = new Color(1f, 0.85f, 0.55f);
            var emission = flash.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var shape = flash.shape;
            shape.enabled = false;
            Fade(flash);
            var flashMaterial = EyeMaterial("SR_KegFlash", "Mobile/Particles/Additive", Color.white);
            if (flashMaterial != null && flashMaterial.mainTexture == null)
            {
                flashMaterial.mainTexture = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
                EditorUtility.SetDirty(flashMaterial);
            }
            flash.GetComponent<ParticleSystemRenderer>().sharedMaterial = flashMaterial;

            // The ring lies flat and spreads out along the ground from the keg
            var ring = NewParticles("Shockwave", flash.transform, keg.position, Quaternion.identity);
            var ringMain = ring.main;
            ringMain.duration = 0.5f;
            ringMain.startLifetime = 0.4f;
            ringMain.startSpeed = 0f;
            ringMain.startSize = 3.5f;
            ringMain.startColor = new Color(1f, 0.6f, 0.25f);
            var ringEmission = ring.emission;
            ringEmission.rateOverTime = 0f;
            ringEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var ringShape = ring.shape;
            ringShape.enabled = false;
            var grow = ring.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.1f, 1f, 1f));
            Fade(ring);
            var ringRenderer = ring.GetComponent<ParticleSystemRenderer>();
            ringRenderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;

            var importer = AssetImporter.GetAtPath(KegRingArt) as TextureImporter;
            if (importer == null) Fail("No " + KegRingArt + ". Run tools/keg-blast-art.py");
            else if (!importer.alphaIsTransparency || importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            var ringMaterial = EyeMaterial("SR_KegRing", "Mobile/Particles/Additive", Color.white);
            if (ringMaterial != null && ringMaterial.mainTexture == null)
            {
                ringMaterial.mainTexture = RangeSetupUtil.Load<Texture2D>(KegRingArt);
                EditorUtility.SetDirty(ringMaterial);
            }
            ringRenderer.sharedMaterial = ringMaterial;
            return flash;
        }

        private static void Fade(ParticleSystem system)
        {
            var colour = system.colorOverLifetime;
            colour.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            colour.color = new ParticleSystem.MinMaxGradient(fade);
        }
    }
}
