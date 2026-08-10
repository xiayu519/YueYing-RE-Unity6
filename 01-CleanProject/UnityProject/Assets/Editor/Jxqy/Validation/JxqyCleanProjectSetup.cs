using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using YooAsset;

namespace Jxqy.Editor.Validation
{
    [InitializeOnLoad]
    public static class JxqyCleanProjectSetup
    {
        private const string ResourceRoot = "Assets/AssetRaw/Jxqy";
        private const string RequestRelativePath =
            "Temp/JxqyCleanSetup/setup.request";
        private const string ResultRelativePath =
            "Temp/JxqyCleanSetup/setup.result";
        private const string ProgressRelativePath =
            "Temp/JxqyCleanSetup/setup.progress";
        private static bool _running;

        static JxqyCleanProjectSetup()
        {
            EditorApplication.update += Poll;
        }

        [MenuItem("TEngine/Jxqy/Configure Clean Project Resources")]
        public static void ConfigureFromMenu()
        {
            Configure(writeResult: true);
        }

        public static void ConfigureFromCommandLine()
        {
            Configure(writeResult: true);
        }

        private static void Poll()
        {
            if (_running || EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
                return;

            string request = ProjectPath(RequestRelativePath);
            if (!File.Exists(request))
                return;

            File.Delete(request);
            EditorApplication.delayCall += () => Configure(writeResult: true);
        }

        private static void Configure(bool writeResult)
        {
            if (_running)
                return;

            _running = true;
            string resultPath = ProjectPath(ResultRelativePath);
            string progressPath = ProjectPath(ProgressRelativePath);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(resultPath));
                WriteProgress(progressPath, "Checking imported resources...");
                ValidateResourceTree();
                const string startupScene = "Assets/Scenes/main.unity";
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(startupScene) == null)
                    throw new FileNotFoundException(
                        "Startup scene is missing.",
                        startupScene);

                EditorPrefs.SetInt(
                    "EditorPlayMode",
                    (int)EPlayMode.EditorSimulateMode);

                WriteProgress(
                    progressPath,
                    "Refreshing DefaultPackage simulation manifest...");
                PackageInvokeBuildResult defaultPackage =
                    EditorSimulateModeHelper.SimulateBuild("DefaultPackage");
                ValidateSimulationResult("DefaultPackage", defaultPackage);

                WriteProgress(
                    progressPath,
                    "Refreshing JxqyPackage simulation manifest...");
                PackageInvokeBuildResult jxqyPackage =
                    EditorSimulateModeHelper.SimulateBuild("JxqyPackage");
                ValidateSimulationResult("JxqyPackage", jxqyPackage);

                string result = string.Join(
                    Environment.NewLine,
                    "SUCCESS",
                    "UnityVersion=" + Application.unityVersion,
                    "EditorPlayMode=" + EPlayMode.EditorSimulateMode,
                    "StartupScene=" + startupScene,
                    "DefaultPackageManifest=" +
                    defaultPackage.PackageRootDirectory,
                    "JxqyPackageManifest=" +
                    jxqyPackage.PackageRootDirectory,
                    "SimulationManifest=Refreshed",
                    "BundleBuild=Skipped");
                if (writeResult)
                    File.WriteAllText(resultPath, result + Environment.NewLine);
                Debug.Log("[JxqyCleanProjectSetup] " + result.Replace(
                    Environment.NewLine,
                    "; "));
                WriteProgress(progressPath, "SUCCESS");
            }
            catch (Exception exception)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(resultPath));
                if (writeResult)
                {
                    File.WriteAllText(
                        resultPath,
                        "FAILED" + Environment.NewLine + exception +
                        Environment.NewLine);
                }
                Debug.LogException(exception);
                throw;
            }
            finally
            {
                _running = false;
            }
        }

        private static void ValidateSimulationResult(
            string packageName,
            PackageInvokeBuildResult result)
        {
            if (result == null ||
                string.IsNullOrWhiteSpace(result.PackageRootDirectory) ||
                !Directory.Exists(result.PackageRootDirectory))
            {
                throw new InvalidOperationException(
                    packageName + " simulation manifest refresh failed.");
            }
        }

        private static void WriteProgress(string path, string message)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, message + Environment.NewLine);
        }

        private static void ValidateResourceTree()
        {
            string resourcePath = ProjectPath(ResourceRoot);
            if (!Directory.Exists(resourcePath))
                throw new DirectoryNotFoundException(
                    "Import JxqyResources.unitypackage before setup: " +
                    resourcePath);

            string[] requiredDirectories =
            {
                "Animations",
                "Audio",
                "Maps",
                "Media",
                "Scenes",
                "Text",
                "UI",
            };
            foreach (string directory in requiredDirectories)
            {
                if (!Directory.Exists(Path.Combine(resourcePath, directory)))
                    throw new InvalidOperationException(
                        "Resource package is incomplete. Missing: " + directory);
            }

            string[] forbiddenExtensions =
            {
                ".cs",
                ".dll",
                ".asmdef",
                ".rsp",
            };
            string forbidden = Directory.EnumerateFiles(
                    resourcePath,
                    "*",
                    SearchOption.AllDirectories)
                .FirstOrDefault(path => forbiddenExtensions.Contains(
                    Path.GetExtension(path),
                    StringComparer.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(forbidden))
                throw new InvalidOperationException(
                    "Resource package contains forbidden code: " + forbidden);
        }

        private static string ProjectPath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                relativePath));
        }
    }
}
