using System;
using System.IO;
using Jxqy.Domain.Content;
using Jxqy.Editor.Animation.Conversion;
using Jxqy.Editor.Scanning;
using Jxqy.Editor.YooAsset;
using UnityEditor;
using UnityEngine;

namespace Jxqy.Editor.Font
{
    public sealed class JxqySpriteFontConverter
    {
        public const string FontConverterVersion = "0.1.0-font-1";
        public const string ConvertedStatus = "Converted";
        public const string ReusedStatus = "Reused";
        public const string FailedStatus = "Failed";

        public JxqySpriteFontConversionFileReport Convert(
            JxqySourceFileRecord source,
            string sourceRoot,
            string outputRoot)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            string relative = source.RelativePath.Replace('\\', '/').TrimStart('/');
            if (source.Kind != JxqyFileKind.Xnb ||
                !relative.StartsWith(
                    "Content/font/",
                    StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(
                    "Source is not a Content/font XNB.",
                    nameof(source));

            string normalizedOutput = outputRoot.Replace('\\', '/').TrimEnd('/');
            string outputDirectory = $"{normalizedOutput}/Fonts/{relative}";
            string textureAssetPath = outputDirectory + "/font.png";
            string metadataAssetPath = outputDirectory + "/font.json";
            string referenceAssetPath = outputDirectory + "/reference-text.png";
            if (CanReuse(
                    metadataAssetPath,
                    textureAssetPath,
                    referenceAssetPath,
                    source,
                    out JxqySpriteFontMetadata reused))
                return CreateReport(
                    source,
                    textureAssetPath,
                    metadataAssetPath,
                    referenceAssetPath,
                    reused,
                    ReusedStatus);

            string sourcePath = Path.GetFullPath(Path.Combine(
                sourceRoot,
                relative.Replace('/', Path.DirectorySeparatorChar)));
            var before = new FileInfo(sourcePath);
            if (!before.Exists)
                throw new FileNotFoundException(
                    "SpriteFont XNB is missing.",
                    sourcePath);
            if (before.Length != source.Size ||
                before.LastWriteTimeUtc.Ticks != source.LastWriteUtcTicks)
                throw new IOException(
                    $"Source changed after manifest scan: {source.RelativePath}.");

            JxqyDecodedSpriteFont font =
                JxqyXnbSpriteFontDecoder.Decode(File.ReadAllBytes(sourcePath));
            var after = new FileInfo(sourcePath);
            if (before.Length != after.Length ||
                before.LastWriteTimeUtc.Ticks != after.LastWriteTimeUtc.Ticks)
                throw new IOException(
                    $"SpriteFont XNB changed while reading: {source.RelativePath}.");

            WriteTexture(
                textureAssetPath,
                JxqySpriteFontTextureWriter.EncodePng(
                    font.TextureRgba,
                    font.TextureWidth,
                    font.TextureHeight),
                true);
            WriteTexture(
                referenceAssetPath,
                JxqySpriteFontTextureWriter.RenderReferencePng(
                    font,
                    SelectReferenceText(font)),
                false);
            var metadata = CreateMetadata(
                source,
                normalizedOutput,
                textureAssetPath,
                font);
            JxqyAnimationConverter.WriteJsonAsset(
                metadataAssetPath,
                metadata,
                true);
            return CreateReport(
                source,
                textureAssetPath,
                metadataAssetPath,
                referenceAssetPath,
                metadata,
                ConvertedStatus);
        }

        private static JxqySpriteFontMetadata CreateMetadata(
            JxqySourceFileRecord source,
            string outputRoot,
            string textureAssetPath,
            JxqyDecodedSpriteFont font)
        {
            var metadata = new JxqySpriteFontMetadata
            {
                ConverterVersion = FontConverterVersion,
                SourceStableId = source.StableId,
                SourceRelativePath = source.RelativePath,
                SourceAddress = source.SourceAddress,
                SourceSha256 = source.Sha256,
                TextureAddress = JxqyAddressByRelativePath.CreateAddress(
                    textureAssetPath,
                    outputRoot),
                TextureWidth = font.TextureWidth,
                TextureHeight = font.TextureHeight,
                LineSpacing = font.LineSpacing,
                Spacing = font.Spacing,
                DefaultCharacter = font.DefaultCharacter
            };
            foreach (JxqySpriteFontGlyph glyph in font.Glyphs)
            {
                metadata.Glyphs.Add(new JxqySpriteFontGlyphMetadata
                {
                    Character = glyph.Character,
                    Glyph = Convert(glyph.Glyph),
                    Cropping = Convert(glyph.Cropping),
                    KerningLeft = glyph.KerningLeft,
                    KerningWidth = glyph.KerningWidth,
                    KerningRight = glyph.KerningRight
                });
            }
            return metadata;
        }

        private static JxqySpriteFontRectangle Convert(
            JxqySpriteFontRect rectangle)
        {
            return new JxqySpriteFontRectangle
            {
                X = rectangle.X,
                Y = rectangle.Y,
                Width = rectangle.Width,
                Height = rectangle.Height
            };
        }

        private static bool CanReuse(
            string metadataAssetPath,
            string textureAssetPath,
            string referenceAssetPath,
            JxqySourceFileRecord source,
            out JxqySpriteFontMetadata metadata)
        {
            metadata = null;
            string absoluteMetadata = GetAbsoluteAssetPath(metadataAssetPath);
            if (!File.Exists(absoluteMetadata) ||
                !File.Exists(GetAbsoluteAssetPath(textureAssetPath)) ||
                !File.Exists(GetAbsoluteAssetPath(referenceAssetPath)))
                return false;
            try
            {
                metadata = JsonUtility.FromJson<JxqySpriteFontMetadata>(
                    File.ReadAllText(absoluteMetadata));
                return metadata != null &&
                       metadata.ConverterVersion == FontConverterVersion &&
                       metadata.SourceStableId == source.StableId &&
                       metadata.SourceSha256 == source.Sha256 &&
                       metadata.Glyphs != null &&
                       metadata.Glyphs.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        private static JxqySpriteFontConversionFileReport CreateReport(
            JxqySourceFileRecord source,
            string textureAssetPath,
            string metadataAssetPath,
            string referenceAssetPath,
            JxqySpriteFontMetadata metadata,
            string status)
        {
            return new JxqySpriteFontConversionFileReport
            {
                RelativePath = source.RelativePath,
                StableId = source.StableId,
                Status = status,
                TextureAssetPath = textureAssetPath,
                MetadataAssetPath = metadataAssetPath,
                ReferenceAssetPath = referenceAssetPath,
                TextureWidth = metadata.TextureWidth,
                TextureHeight = metadata.TextureHeight,
                GlyphCount = metadata.Glyphs.Count
            };
        }

        private static string SelectReferenceText(JxqyDecodedSpriteFont font)
        {
            foreach (JxqySpriteFontGlyph glyph in font.Glyphs)
            {
                if (glyph.Character == '剑')
                    return "剑侠情缘 杨影枫 月眉儿 123";
            }
            return "Sword JXQY 123";
        }

        private static void WriteTexture(
            string assetPath,
            byte[] bytes,
            bool configureFontTexture)
        {
            string absolutePath = GetAbsoluteAssetPath(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
            string temporaryPath = absolutePath + ".tmp";
            string backupPath = absolutePath + ".bak";
            File.WriteAllBytes(temporaryPath, bytes);
            if (File.Exists(absolutePath))
            {
                File.Replace(temporaryPath, absolutePath, backupPath);
                if (File.Exists(backupPath))
                    File.Delete(backupPath);
            }
            else
            {
                File.Move(temporaryPath, absolutePath);
            }
            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceSynchronousImport);
            if (configureFontTexture)
                JxqySpriteFontImportConfigurator.Configure(assetPath);
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
