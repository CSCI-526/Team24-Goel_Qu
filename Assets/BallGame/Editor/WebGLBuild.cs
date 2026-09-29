using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BallGame.Editor
{
    public static class WebGLBuild
    {
        const string ScenePath = "Assets/BallGame/Scenes/Ball Game 2D.unity";
        const string DefaultOutput = "Builds/WebGL";
        const string Template = "PROJECT:BallGame";

        [MenuItem("Ball Game/Build WebGL")]
        public static void BuildFromMenu()
        {
            Build(DefaultOutput, SourceCommit(null));
        }

        // Launch Unity with -buildTarget WebGL so platform symbols are compiled before this runs.
        public static void BuildBatch()
        {
            Build(Argument("-webglOutput") ?? DefaultOutput, SourceCommit(Argument("-sourceCommit")));
        }

        static void Build(string outputDirectory, string sourceCommit)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new BuildFailedException("Stop Play mode before building the browser game.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                throw new BuildFailedException("Install Web Build Support for Unity " + Application.unityVersion + " in Unity Hub.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                throw new BuildFailedException("Switch the active Build Profile to Web before building, or launch batch mode with -buildTarget WebGL.");

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (!File.Exists(Path.Combine(projectRoot, ScenePath)))
                throw new BuildFailedException("The main game scene is missing: " + ScenePath);
            if (!File.Exists(Path.Combine(Application.dataPath, "WebGLTemplates/BallGame/index.html")))
                throw new BuildFailedException("The BallGame browser template is missing.");
            string output = Path.GetFullPath(Path.IsPathRooted(outputDirectory)
                ? outputDirectory : Path.Combine(projectRoot, outputDirectory));
            if (output.TrimEnd(Path.DirectorySeparatorChar) == projectRoot.TrimEnd(Path.DirectorySeparatorChar))
                throw new BuildFailedException("The build output must be a separate folder, not the project root.");

            var previousCompression = PlayerSettings.WebGL.compressionFormat;
            bool previousFallback = PlayerSettings.WebGL.decompressionFallback;
            bool previousThreads = PlayerSettings.WebGL.threadsSupport;
            string previousTemplate = PlayerSettings.WebGL.template;
            try
            {
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
                PlayerSettings.WebGL.decompressionFallback = true;
                PlayerSettings.WebGL.threadsSupport = false;
                PlayerSettings.WebGL.template = Template;
                Directory.CreateDirectory(output);
                // A failed rebuild must not leave the previous success marker behind.
                File.Delete(Path.Combine(output, "build-info.json"));

                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = BuildTarget.WebGL,
                    targetGroup = BuildTargetGroup.WebGL,
                    options = BuildOptions.None
                });
                if (report == null || report.summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException(report == null ? "WebGL build returned no report."
                        : "WebGL build " + report.summary.result + " with " + report.summary.totalErrors + " error(s). See the Unity build log.");
                if (!File.Exists(Path.Combine(output, "index.html")))
                    throw new BuildFailedException("WebGL build did not produce index.html.");

                File.WriteAllText(Path.Combine(output, ".nojekyll"), string.Empty);
                var info = new BuildInfo
                {
                    sourceCommit = sourceCommit,
                    builtAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    unityVersion = Application.unityVersion,
                    scene = ScenePath
                };
                File.WriteAllText(Path.Combine(output, "build-info.json"), JsonUtility.ToJson(info, true) + "\n", new UTF8Encoding(false));
                Debug.Log("BALL GAME: WebGL release build completed at " + output + " (" + report.summary.totalSize + " bytes).");
            }
            finally
            {
                PlayerSettings.WebGL.compressionFormat = previousCompression;
                PlayerSettings.WebGL.decompressionFallback = previousFallback;
                PlayerSettings.WebGL.threadsSupport = previousThreads;
                PlayerSettings.WebGL.template = previousTemplate;
                AssetDatabase.SaveAssets();
            }
        }

        static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] != name) continue;
                if (i + 1 == args.Length || args[i + 1].StartsWith("-", StringComparison.Ordinal))
                    throw new BuildFailedException("Missing value for " + name + ".");
                return args[i + 1];
            }
            return null;
        }

        static string SourceCommit(string argument)
        {
            return argument ?? Environment.GetEnvironmentVariable("BALL_GAME_SOURCE_COMMIT")
                ?? Environment.GetEnvironmentVariable("GITHUB_SHA") ?? "unknown";
        }

        [Serializable]
        sealed class BuildInfo
        {
            public string sourceCommit;
            public string builtAtUtc;
            public string unityVersion;
            public string scene;
        }
    }
}
