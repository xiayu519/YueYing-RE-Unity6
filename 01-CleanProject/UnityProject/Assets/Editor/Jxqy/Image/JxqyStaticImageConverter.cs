using System;
using System.IO;
using System.Linq;
using Jxqy.Editor.Animation.Atlas;
using Jxqy.Editor.Scanning;
using Jxqy.Editor.YooAsset;
using UnityEditor;
using UnityEngine;

namespace Jxqy.Editor.Image
{
    public sealed class JxqyStaticImageConverter
    {
        public const string ConverterVersion = "0.1.0-image-1";
        public const string ConvertedStatus = "Converted";
        public const string ReusedStatus = "Reused";
        public const string FailedStatus = "Failed";

        public JxqyStaticImageFileReport Convert(
            JxqySourceFileRecord source,
            string sourceRoot,
            string outputRoot)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (source.Kind != JxqyFileKind.Image)
                throw new ArgumentException(
                    "Source is not a static image.",
                    nameof(source));
            string relative = source.RelativePath.Replace('\\', '/').TrimStart('/');
            string normalizedOutput = outputRoot.Replace('\\', '/').TrimEnd('/');
            string assetPath = $"{normalizedOutput}/Images/{relative}";
            string sourcePath = Path.GetFullPath(Path.Combine(
                sourceRoot,
                relative.Replace('/', Path.DirectorySeparatorChar)));
            string absoluteTarget = GetAbsoluteAssetPath(assetPath);
            byte[] sourceBytes = File.ReadAllBytes(sourcePath);
            string status = ReusedStatus;
            if (!File.Exists(absoluteTarget) ||
                !sourceBytes.SequenceEqual(File.ReadAllBytes(absoluteTarget)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(absoluteTarget));
                string temporaryPath = absoluteTarget + ".tmp";
                string backupPath = absoluteTarget + ".bak";
                File.WriteAllBytes(temporaryPath, sourceBytes);
                if (File.Exists(absoluteTarget))
                {
                    File.Replace(
                        temporaryPath,
                        absoluteTarget,
                        backupPath);
                    if (File.Exists(backupPath))
                        File.Delete(backupPath);
                }
                else
                {
                    File.Move(temporaryPath, absoluteTarget);
                }
                status = ConvertedStatus;
            }
            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceSynchronousImport);
            Configure(assetPath);
            return new JxqyStaticImageFileReport
            {
                RelativePath = source.RelativePath,
                StableId = source.StableId,
                Status = status,
                AssetPath = assetPath,
                Address = JxqyAddressByRelativePath.CreateAddress(
                    assetPath,
                    normalizedOutput),
                ByteCount = sourceBytes.LongLength
            };
        }

        private static void Configure(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException(
                    $"TextureImporter not found for {assetPath}.");
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = JxqyTextureBudget.CrossPlatformMaximumSize;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(CreatePlatform(
                "Standalone",
                TextureImporterFormat.RGBA32,
                TextureImporterCompression.Uncompressed));
            importer.SaveAndReimport();
        }

        private static TextureImporterPlatformSettings CreatePlatform(
            string platform,
            TextureImporterFormat format,
            TextureImporterCompression compression)
        {
            return new TextureImporterPlatformSettings
            {
                name = platform,
                overridden = true,
                maxTextureSize = JxqyTextureBudget.CrossPlatformMaximumSize,
                resizeAlgorithm = TextureResizeAlgorithm.Mitchell,
                format = format,
                textureCompression = compression,
                compressionQuality = 100,
                crunchedCompression = false,
                allowsAlphaSplitting = false
            };
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
