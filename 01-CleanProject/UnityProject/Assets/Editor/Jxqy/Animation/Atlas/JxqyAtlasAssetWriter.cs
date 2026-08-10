using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Jxqy.Editor.Animation.Atlas
{
    public static class JxqyAtlasAssetWriter
    {
        public static List<string> WritePages(
            string assetDirectory,
            string baseName,
            IReadOnlyList<JxqyAtlasPage> pages)
        {
            if (string.IsNullOrWhiteSpace(assetDirectory) ||
                !assetDirectory.Replace('\\', '/').StartsWith(
                    "Assets/",
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Atlas output directory must be below Assets.",
                    nameof(assetDirectory));
            }
            if (string.IsNullOrWhiteSpace(baseName))
                throw new ArgumentException("Atlas base name is empty.", nameof(baseName));
            if (pages == null)
                throw new ArgumentNullException(nameof(pages));

            string normalizedDirectory = assetDirectory.Replace('\\', '/').TrimEnd('/');
            string absoluteDirectory = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                normalizedDirectory));
            Directory.CreateDirectory(absoluteDirectory);

            var assetPaths = new List<string>(pages.Count);
            foreach (JxqyAtlasPage page in pages)
            {
                string fileName = $"{baseName}.atlas.{page.PageIndex:D3}.png";
                string assetPath = $"{normalizedDirectory}/{fileName}";
                string absolutePath = Path.Combine(absoluteDirectory, fileName);
                WritePagePng(absolutePath, page);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                JxqyTextureImportConfigurator.ConfigureAtlas(assetPath);
                assetPaths.Add(assetPath);
            }
            return assetPaths;
        }

        private static void WritePagePng(string absolutePath, JxqyAtlasPage page)
        {
            if (page.Pixels == null ||
                page.Pixels.Length != checked(page.Width * page.Height))
            {
                throw new InvalidOperationException(
                    $"Atlas page {page.PageIndex} pixel count is invalid.");
            }

            var texture = new Texture2D(
                page.Width,
                page.Height,
                TextureFormat.RGBA32,
                false,
                false)
            {
                name = Path.GetFileNameWithoutExtension(absolutePath),
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            try
            {
                var colors = new Color32[page.Pixels.Length];
                for (int index = 0; index < colors.Length; index++)
                {
                    JxqyRgba32 pixel = page.Pixels[index];
                    colors[index] = new Color32(pixel.R, pixel.G, pixel.B, pixel.A);
                }
                texture.SetPixels32(colors);
                texture.Apply(false, false);
                File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
