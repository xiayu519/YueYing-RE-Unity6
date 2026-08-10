using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Jxqy.Editor.Animation.Conversion;
using Jxqy.Editor.Scanning;
using UnityEditor;
using UnityEngine;

namespace Jxqy.Editor.Shader
{
    public static class JxqyShaderMigrationJob
    {
        public const string GeneratorVersion = "0.1.0-shader-input-1";
        public const string ManifestAssetPath =
            "Assets/AssetRaw/Jxqy/Manifests/shader-migration-inputs.json";
        private const string SourceManifestAssetPath =
            "Assets/AssetRaw/Jxqy/Manifests/source-manifest.json";
        private const string RegisteredRoot =
            "Assets/AssetRaw/Jxqy/ShaderInputs/JxqyContent/effect";

        [MenuItem("TEngine/Jxqy/Register Shader Migration Inputs")]
        public static void Register()
        {
            var manifest = new JxqyShaderMigrationManifest
            {
                GeneratorVersion = GeneratorVersion,
                GeneratedUtc = DateTime.UtcNow.ToString(
                    "O",
                    CultureInfo.InvariantCulture)
            };
            try
            {
                var settings = new JxqySourceSettings();
                settings.Validate();
                JxqySourceManifest sourceManifest =
                    JsonUtility.FromJson<JxqySourceManifest>(
                        File.ReadAllText(
                            GetAbsoluteAssetPath(SourceManifestAssetPath)));
                string referenceEffectRoot = Path.Combine(
                    settings.ReferenceSourceRoot,
                    "JxqyContent",
                    "effect");
                foreach (string sourcePath in Directory
                             .EnumerateFiles(
                                 referenceEffectRoot,
                                 "*.fx",
                                 SearchOption.TopDirectoryOnly)
                             .OrderBy(
                                 path => path,
                                 StringComparer.OrdinalIgnoreCase))
                {
                    string fileName = Path.GetFileName(sourcePath);
                    string compiledRelative =
                        $"Content/effect/{Path.GetFileNameWithoutExtension(fileName)}.xnb";
                    JxqySourceFileRecord compiled = sourceManifest.Files
                        .SingleOrDefault(file => string.Equals(
                            file.RelativePath,
                            compiledRelative,
                            StringComparison.OrdinalIgnoreCase));
                    if (compiled == null)
                    {
                        manifest.Errors.Add(
                            $"Compiled Effect XNB not found: {compiledRelative}");
                        continue;
                    }
                    string registeredPath =
                        $"{RegisteredRoot}/{fileName}.txt";
                    CopyReadOnlyInput(sourcePath, registeredPath);
                    manifest.ShaderSources.Add(new JxqyShaderMigrationInput
                    {
                        Name = Path.GetFileNameWithoutExtension(fileName),
                        SourceRelativePath =
                            $"JxqyContent/effect/{fileName}",
                        SourceSha256 = ComputeSha256(sourcePath),
                        SourceSize = new FileInfo(sourcePath).Length,
                        RegisteredAssetPath = registeredPath,
                        CompiledXnbRelativePath = compiled.RelativePath,
                        CompiledXnbSha256 = compiled.Sha256,
                        CompiledXnbExcluded = true,
                        ExclusionReason =
                            "XNA Effect XNB contains platform-compiled shader bytecode and cannot be loaded by Unity; port from the registered FX source."
                    });
                }

                RegisterTextureDependency(
                    referenceEffectRoot,
                    sourceManifest,
                    manifest);
                manifest.ShaderSourceCount = manifest.ShaderSources.Count;
                manifest.ExcludedCompiledEffectCount = manifest.ShaderSources
                    .Count(input => input.CompiledXnbExcluded);
                manifest.DependencyCount = manifest.Dependencies.Count;
                JxqyAnimationConverter.WriteJsonAsset(
                    ManifestAssetPath,
                    manifest,
                    true);
            }
            catch (Exception exception)
            {
                manifest.Errors.Add(
                    $"{exception.GetType().Name}: {exception.Message}");
                JxqyAnimationConverter.WriteJsonAsset(
                    ManifestAssetPath,
                    manifest,
                    true);
                Debug.LogException(exception);
            }

            string summary =
                $"Jxqy shader inputs registered. FX={manifest.ShaderSourceCount}, " +
                $"ExcludedEffectXnb={manifest.ExcludedCompiledEffectCount}, " +
                $"Dependencies={manifest.DependencyCount}, Errors={manifest.Errors.Count}.";
            if (manifest.Errors.Count == 0)
                Debug.Log(summary);
            else
                Debug.LogError(summary);
        }

        private static void RegisterTextureDependency(
            string referenceEffectRoot,
            JxqySourceManifest sourceManifest,
            JxqyShaderMigrationManifest manifest)
        {
            const string fileName = "waterfall.jpg";
            const string compiledRelative = "Content/effect/waterfall.xnb";
            string sourcePath = Path.Combine(referenceEffectRoot, fileName);
            if (!File.Exists(sourcePath))
            {
                manifest.Errors.Add($"Shader dependency missing: {sourcePath}");
                return;
            }
            JxqySourceFileRecord compiled = sourceManifest.Files
                .SingleOrDefault(file => string.Equals(
                    file.RelativePath,
                    compiledRelative,
                    StringComparison.OrdinalIgnoreCase));
            if (compiled == null)
            {
                manifest.Errors.Add(
                    $"Compiled texture XNB not found: {compiledRelative}");
                return;
            }
            string registeredPath = $"{RegisteredRoot}/{fileName}";
            CopyReadOnlyInput(sourcePath, registeredPath);
            ConfigureDisplacementTexture(registeredPath);
            manifest.Dependencies.Add(new JxqyShaderMigrationDependency
            {
                SourceRelativePath = $"JxqyContent/effect/{fileName}",
                SourceSha256 = ComputeSha256(sourcePath),
                SourceSize = new FileInfo(sourcePath).Length,
                RegisteredAssetPath = registeredPath,
                CompiledXnbRelativePath = compiled.RelativePath
            });
        }

        private static void ConfigureDisplacementTexture(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException(
                    $"TextureImporter not found for {assetPath}.");
            }

            // waterfall.jpg stores signed UV displacement in its R/G channels.
            // Importing it as an sRGB sprite gamma-decodes those data values and
            // produces the exaggerated, artificial-looking refraction.
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static void CopyReadOnlyInput(
            string sourcePath,
            string assetPath)
        {
            string absoluteTarget = GetAbsoluteAssetPath(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteTarget));
            byte[] sourceBytes = File.ReadAllBytes(sourcePath);
            if (!File.Exists(absoluteTarget) ||
                !sourceBytes.SequenceEqual(File.ReadAllBytes(absoluteTarget)))
                File.WriteAllBytes(absoluteTarget, sourceBytes);
            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceSynchronousImport);
        }

        private static string ComputeSha256(string path)
        {
            using var stream = File.OpenRead(path);
            using var algorithm = SHA256.Create();
            return BitConverter.ToString(algorithm.ComputeHash(stream))
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }

        private static string GetAbsoluteAssetPath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }
    }
}
