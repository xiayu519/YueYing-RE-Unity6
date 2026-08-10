using System;
using UnityEditor;
using UnityEngine;

namespace Jxqy.Editor.Animation.Atlas
{
    public static class JxqyTextureImportConfigurator
    {
        public static void ConfigureAtlas(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                throw new ArgumentException("Texture asset path is empty.", nameof(assetPath));

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException($"TextureImporter not found for {assetPath}.");

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

            importer.SetPlatformTextureSettings(CreatePlatformSettings(
                "Standalone",
                TextureImporterFormat.RGBA32,
                TextureImporterCompression.Uncompressed));
            importer.SaveAndReimport();
        }

        private static TextureImporterPlatformSettings CreatePlatformSettings(
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
    }
}
