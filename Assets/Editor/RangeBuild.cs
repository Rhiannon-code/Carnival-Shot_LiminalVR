using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    /// The Build Settings window does not open on this editor, so the scene list, the player settings
    /// and the output path all live here instead of being ticked in a window nobody can see
    public static class RangeBuild
    {
        private const string Scene = "Assets/Scenes/ShootingRangeCarnival.unity";
        private const string OutputDir = "Builds/Quest";
        private const string ApkName = "ShootingRangeCarnival-Quest.apk";

        private const string NdkLibs =
            "PlaybackEngines/AndroidPlayer/NDK/toolchains/llvm/prebuilt/linux-x86_64/lib64";

        [MenuItem("Shooting Range/Build/Configure Player Settings for Quest 2", false, 0)]
        public static void Configure()
        {
            // IL2CPP first: ARM64 is not a legal architecture under Mono, so setting them the other
            // way round silently leaves you on ARMv7
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.forceSDCardPermission = false;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,
                new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });

            // The SDK's legacy path, and the pair its own preflight gates on
            PlayerSettings.virtualRealitySupported = true;
            PlayerSettings.SetVirtualRealitySDKs(BuildTargetGroup.Android, new[] { "Oculus" });
            PlayerSettings.stereoRenderingPath = StereoRenderingPath.SinglePass;

            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            EditorUserBuildSettings.development = false;

            Debug.Log("[RangeBuild] Player settings set for Quest 2: IL2CPP, ARM64, Oculus VR, " +
                      "single pass, ASTC, Gradle. These are project wide, not per scene.");
        }

        [MenuItem("Shooting Range/Build/Build APK", false, 20)]
        public static void BuildApk() { Build(false); }

        [MenuItem("Shooting Range/Build/Build APK and Install to Headset", false, 21)]
        public static void BuildAndRun() { Build(true); }

        private static void Build(bool install)
        {
            if (!File.Exists(Scene))
            {
                Debug.LogError("[RangeBuild] " + Scene + " does not exist.");
                return;
            }

            // A build reads the scene off disk. Every unsaved change in the editor is simply absent
            // from the APK, which looks exactly like a change that did not work
            var open = EditorSceneManager.GetActiveScene();
            if (open.path != Scene)
                Debug.LogWarning("[RangeBuild] " + Path.GetFileName(Scene) + " is being built, but " +
                                 (string.IsNullOrEmpty(open.path) ? "an unsaved scene" : open.name) +
                                 " is the one you have open. Anything you changed in that one is not in this build.");

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[RangeBuild] Cancelled at the save prompt, nothing built.");
                return;
            }

            Configure();
            CheckToolchain();

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.Log("[RangeBuild] Switching to Android. The first switch reimports every asset " +
                          "and takes a while. Let it finish, then run this again.");

                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                return;
            }

            Directory.CreateDirectory(OutputDir);
            string path = Path.Combine(OutputDir, ApkName);

            // The scene list is passed explicitly rather than read from the Build Settings window, so
            // it cannot pick up whatever happens to be ticked in a window that will not open
            var options = new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                locationPathName = path,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = install ? BuildOptions.AutoRunPlayer : BuildOptions.None,
            };

            var summary = BuildPipeline.BuildPlayer(options).summary;

            if (summary.result == BuildResult.Succeeded)
                Debug.Log("[RangeBuild] Built " + path + " (" + (summary.totalSize / 1048576) + " MB) in " +
                          summary.totalTime.TotalSeconds.ToString("0") + " s." +
                          (install ? " Installing to the connected headset." : ""));
            else
                Debug.LogError("[RangeBuild] Build " + summary.result + " with " + summary.totalErrors +
                               " error(s). The cause is in the lines above, not in this one.");
        }

        /// The bundled NDK's clang wants libncurses.so.5 and libtinfo.so.5, which Arch stopped shipping.
        /// Without them an IL2CPP build dies deep in a toolchain log rather than saying what is wrong
        private static void CheckToolchain()
        {
            string lib64 = Path.Combine(EditorApplication.applicationContentsPath, NdkLibs);
            if (!Directory.Exists(lib64)) return;

            bool ncurses = File.Exists(Path.Combine(lib64, "libncurses.so.5"));
            bool tinfo = File.Exists(Path.Combine(lib64, "libtinfo.so.5"));
            if (ncurses && tinfo) return;

            Debug.LogWarning("[RangeBuild] The NDK is missing " +
                             (ncurses ? "" : "libncurses.so.5 ") + (tinfo ? "" : "libtinfo.so.5 ") +
                             "and IL2CPP will fail somewhere unhelpful. Symlink the system ones into\n  " +
                             lib64);
        }

        /// What the settings actually are, for when a build behaves oddly and the window that would
        /// normally tell you will not open
        [MenuItem("Shooting Range/Build/Log Current Build Settings", false, 40)]
        public static void LogSettings()
        {
            Debug.Log(
                "[RangeBuild] Scene:        " + Scene +
                "\n  Output:            " + Path.Combine(OutputDir, ApkName) +
                "\n  Active target:     " + EditorUserBuildSettings.activeBuildTarget +
                "\n  Scripting backend: " + PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android) +
                "\n  Architectures:     " + PlayerSettings.Android.targetArchitectures +
                "\n  Min SDK:           " + PlayerSettings.Android.minSdkVersion +
                "\n  VR supported:      " + PlayerSettings.virtualRealitySupported +
                "\n  Stereo path:       " + PlayerSettings.stereoRenderingPath +
                "\n  Texture format:    " + EditorUserBuildSettings.androidBuildSubtarget +
                "\n  Development build: " + EditorUserBuildSettings.development +
                "\n  Product name:      " + PlayerSettings.productName +
                "\n  Bundle id:         " + PlayerSettings.applicationIdentifier);
        }
    }
}
