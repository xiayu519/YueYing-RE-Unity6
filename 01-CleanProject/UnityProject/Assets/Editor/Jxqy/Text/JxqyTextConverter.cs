using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Jxqy.Domain.Content;
using Jxqy.Editor.Animation.Conversion;
using Jxqy.Editor.Scanning;
using Jxqy.Editor.YooAsset;
using UnityEngine;

namespace Jxqy.Editor.Text
{
    public sealed class JxqyTextConverter
    {
        public const string TextConverterVersion = "0.1.0-text-2";
        public const string ConvertedStatus = "Converted";
        public const string ReusedStatus = "Reused";
        public const string FailedStatus = "Failed";
        private static readonly UTF8Encoding Utf8NoBom = new(false);

        public JxqyTextConversionFileReport Convert(
            JxqySourceFileRecord source,
            string sourceRoot,
            string outputRoot)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (!IsTextKind(source.Kind))
                throw new ArgumentException("Source is not a supported text kind.", nameof(source));

            string normalizedOutput = outputRoot.Replace('\\', '/').TrimEnd('/');
            string relative = source.RelativePath.Replace('\\', '/').TrimStart('/');
            string outputDirectory = $"{normalizedOutput}/Text/{relative}";
            string contentAssetPath = outputDirectory + "/content.txt";
            string metadataAssetPath = outputDirectory + "/metadata.json";

            if (CanReuse(metadataAssetPath, contentAssetPath, source, out var reused))
            {
                return CreateReport(
                    source,
                    contentAssetPath,
                    metadataAssetPath,
                    reused,
                    ReusedStatus);
            }

            string sourcePath = Path.GetFullPath(Path.Combine(
                sourceRoot,
                relative.Replace('/', Path.DirectorySeparatorChar)));
            var before = new FileInfo(sourcePath);
            if (!before.Exists)
                throw new FileNotFoundException("Text source is missing.", sourcePath);
            if (before.Length != source.Size ||
                before.LastWriteTimeUtc.Ticks != source.LastWriteUtcTicks)
            {
                throw new IOException(
                    $"Source changed after manifest scan: {source.RelativePath}.");
            }

            byte[] sourceBytes = File.ReadAllBytes(sourcePath);
            string text = JxqyLegacyTextDecoder.Decode(sourceBytes, out string encodingName);
            var after = new FileInfo(sourcePath);
            if (before.Length != after.Length ||
                before.LastWriteTimeUtc.Ticks != after.LastWriteTimeUtc.Ticks)
            {
                throw new IOException(
                    $"Text source changed while reading: {source.RelativePath}.");
            }

            byte[] utf8Bytes = Utf8NoBom.GetBytes(text);
            var metadata = new JxqyTextAssetMetadata
            {
                ConverterVersion = TextConverterVersion,
                SourceStableId = source.StableId,
                SourceRelativePath = source.RelativePath,
                SourceAddress = source.SourceAddress,
                SourceSha256 = source.Sha256,
                SourceKind = source.Kind.ToString(),
                OriginalEncoding = encodingName,
                ContentAddress = JxqyAddressByRelativePath.CreateAddress(
                    contentAssetPath,
                    normalizedOutput),
                Utf8Sha256 = ComputeSha256(utf8Bytes),
                NewLineStyle = JxqyStructuredTextParser.DetectNewLineStyle(text),
                CharacterCount = text.Length
            };
            string[] lines = JxqyStructuredTextParser.SplitLines(text);
            metadata.LineCount = lines.Length;
            metadata.NonEmptyLineCount = lines.Count(line => !string.IsNullOrWhiteSpace(line));
            if (IsStructuredKind(source))
            {
                metadata.LineCount = 0;
                metadata.NonEmptyLineCount = 0;
                JxqyStructuredTextParser.Populate(text, metadata);
            }

            WriteBytesAsset(contentAssetPath, utf8Bytes);
            JxqyAnimationConverter.WriteJsonAsset(metadataAssetPath, metadata, false);
            return CreateReport(
                source,
                contentAssetPath,
                metadataAssetPath,
                metadata,
                ConvertedStatus);
        }

        public static bool IsTextKind(JxqyFileKind kind)
        {
            return kind == JxqyFileKind.Npc ||
                   kind == JxqyFileKind.Obj ||
                   kind == JxqyFileKind.Ini ||
                   kind == JxqyFileKind.Script ||
                   kind == JxqyFileKind.Save;
        }

        private static bool IsStructuredKind(JxqySourceFileRecord source)
        {
            return source.Kind == JxqyFileKind.Npc ||
                   source.Kind == JxqyFileKind.Obj ||
                   source.Kind == JxqyFileKind.Save ||
                   source.Kind == JxqyFileKind.Ini &&
                   string.Equals(source.Extension, ".ini", StringComparison.OrdinalIgnoreCase);
        }

        private static bool CanReuse(
            string metadataAssetPath,
            string contentAssetPath,
            JxqySourceFileRecord source,
            out JxqyTextAssetMetadata metadata)
        {
            metadata = null;
            string absoluteMetadata = GetAbsoluteAssetPath(metadataAssetPath);
            string absoluteContent = GetAbsoluteAssetPath(contentAssetPath);
            if (!File.Exists(absoluteMetadata) || !File.Exists(absoluteContent))
                return false;
            try
            {
                metadata = JsonUtility.FromJson<JxqyTextAssetMetadata>(
                    File.ReadAllText(absoluteMetadata));
                if (metadata == null ||
                    metadata.ConverterVersion != TextConverterVersion ||
                    metadata.SourceStableId != source.StableId ||
                    metadata.SourceSha256 != source.Sha256)
                {
                    return false;
                }
                return ComputeSha256(File.ReadAllBytes(absoluteContent)) ==
                       metadata.Utf8Sha256;
            }
            catch
            {
                return false;
            }
        }

        private static JxqyTextConversionFileReport CreateReport(
            JxqySourceFileRecord source,
            string contentAssetPath,
            string metadataAssetPath,
            JxqyTextAssetMetadata metadata,
            string status)
        {
            return new JxqyTextConversionFileReport
            {
                RelativePath = source.RelativePath,
                StableId = source.StableId,
                Kind = source.Kind.ToString(),
                Status = status,
                Encoding = metadata.OriginalEncoding,
                ContentAssetPath = contentAssetPath,
                MetadataAssetPath = metadataAssetPath,
                LineCount = metadata.LineCount,
                SectionCount = metadata.Sections?.Count ?? 0,
                PropertyCount = metadata.Sections?.Sum(section => section.Properties.Count) ?? 0,
                UnparsedStructuredLineCount = metadata.UnparsedStructuredLineCount
            };
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using SHA256 sha256 = SHA256.Create();
            return BitConverter.ToString(sha256.ComputeHash(bytes))
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }

        private static void WriteBytesAsset(string assetPath, byte[] bytes)
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
