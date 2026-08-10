using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Jxqy.Editor.Scanning
{
    public static class JxqyImportCommands
    {
        private const string ManifestDirectory = "Assets/AssetRaw/Jxqy/Manifests";
        private const string ManifestPath = ManifestDirectory + "/source-manifest.json";
        private const string DependencyPath = ManifestDirectory + "/dependency-graph.json";
        private const string CommandPath = ManifestDirectory + "/script-command-report.json";

        [MenuItem("TEngine/Jxqy/Scan Source Resources")]
        public static void ScanAll()
        {
            var settings = new JxqySourceSettings();
            var scanner = new JxqySourceScanner();

            try
            {
                JxqySourceManifest previousManifest = LoadJsonAsset<JxqySourceManifest>(ManifestPath);
                JxqySourceManifest manifest = scanner.Scan(settings, previousManifest);
                var dependencyScanner = new JxqyDependencyScanner();
                JxqyDependencyGraph dependencies = dependencyScanner.Scan(
                    settings.GameSourceRoot,
                    manifest.Files);
                var commandScanner = new JxqyScriptCommandScanner();
                JxqyScriptCommandReport commands = commandScanner.Scan(
                    Path.Combine(settings.GameSourceRoot, "script"),
                    Path.Combine(settings.ReferenceSourceRoot, "Engine", "Script", "ScriptRunner.cs"));

                WriteJsonAsset(ManifestPath, manifest);
                WriteJsonAsset(DependencyPath, dependencies);
                WriteJsonAsset(CommandPath, commands);

                string summary =
                    $"Jxqy source scan complete. Files={manifest.Files.Count}, " +
                    $"Bytes={manifest.TotalBytes}, DuplicateNames={manifest.DuplicateFileNames.Count}, " +
                    $"AddressCollisions={manifest.AddressCollisions.Count}, Errors={manifest.Errors.Count}, " +
                    $"References={dependencies.References.Count}, Commands={commands.Commands.Count}";

                if (manifest.IsValid && dependencies.ParseErrors.Count == 0)
                    Debug.Log(summary);
                else
                    Debug.LogError(summary);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode)
                    throw;
            }
        }

        private static void WriteJsonAsset<T>(string assetPath, T value)
        {
            string absolutePath = GetAbsoluteAssetPath(assetPath);
            string absoluteDirectory = Path.GetDirectoryName(absolutePath);

            Directory.CreateDirectory(absoluteDirectory);
            string json = JsonUtility.ToJson(value, true);
            string temporaryPath = absolutePath + ".tmp";
            File.WriteAllText(temporaryPath, json, new System.Text.UTF8Encoding(false));

            if (File.Exists(absolutePath))
                File.Delete(absolutePath);
            File.Move(temporaryPath, absolutePath);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static T LoadJsonAsset<T>(string assetPath) where T : class
        {
            string absolutePath = GetAbsoluteAssetPath(assetPath);
            if (!File.Exists(absolutePath))
                return null;

            return JsonUtility.FromJson<T>(File.ReadAllText(absolutePath));
        }

        private static string GetAbsoluteAssetPath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        }
    }
}
