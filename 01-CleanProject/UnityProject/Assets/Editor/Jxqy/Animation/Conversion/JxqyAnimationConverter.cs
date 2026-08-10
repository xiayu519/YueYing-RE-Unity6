using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Jxqy.Domain.Content;
using Jxqy.Editor.Animation.Atlas;
using Jxqy.Editor.Scanning;
using Jxqy.Editor.YooAsset;
using UnityEditor;
using UnityEngine;

namespace Jxqy.Editor.Animation.Conversion
{
    public sealed class JxqyAnimationConverter
    {
        public const string AnimationConverterVersion = "0.1.0-animation-1";
        public const string ConvertedStatus = "Converted";
        public const string ReusedStatus = "Reused";
        public const string FailedStatus = "Failed";

        public JxqyAnimationConversionFileReport Convert(
            JxqySourceFileRecord source,
            string sourceRoot,
            string outputRoot)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (source.Kind != JxqyFileKind.Asf && source.Kind != JxqyFileKind.Mpc)
                throw new ArgumentException("Source is not an ASF or MPC animation.", nameof(source));

            var stopwatch = Stopwatch.StartNew();
            string normalizedOutputRoot = NormalizeOutputRoot(outputRoot);
            string sourcePath = ResolveSourcePath(sourceRoot, source.RelativePath);
            string outputDirectory =
                $"{normalizedOutputRoot}/Animations/{NormalizeRelativePath(source.RelativePath)}";
            string metadataAssetPath = outputDirectory + "/animation.json";

            if (CanReuse(metadataAssetPath, outputDirectory, source, out JxqyAnimationMetadata reused))
            {
                JxqyAnimationConversionFileReport reusedReport = CreateReportFromAssets(
                    source,
                    metadataAssetPath,
                    outputDirectory,
                    reused,
                    ReusedStatus);
                stopwatch.Stop();
                reusedReport.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                return reusedReport;
            }

            FileInfo before = ValidateSourceFingerprint(sourcePath, source);
            byte[] bytes = File.ReadAllBytes(sourcePath);
            ValidateSourceUnchanged(sourcePath, before);

            JxqyDecodedAnimation decoded = JxqyAnimationDecoder.Decode(bytes);
            JxqyAnimationMetadata metadata = decoded.Metadata;
            metadata.ConverterVersion = AnimationConverterVersion;
            metadata.SourceStableId = source.StableId;
            metadata.SourceRelativePath = source.RelativePath;
            metadata.SourceAddress = source.SourceAddress;
            metadata.SourceSha256 = source.Sha256;

            var inputs = new List<JxqyAtlasFrameInput>(decoded.Frames.Count);
            for (int index = 0; index < decoded.Frames.Count; index++)
            {
                inputs.Add(new JxqyAtlasFrameInput(
                    source.StableId,
                    index,
                    decoded.Frames[index],
                    metadata.Frames[index]));
            }

            JxqyAtlasPackResult packed = JxqyAtlasPacker.Pack(
                inputs,
                new JxqyAtlasPackSettings
                {
                    MaximumPageSize = JxqyTextureBudget.CrossPlatformMaximumSize,
                    MinimumPageSize = 4,
                    Extrude = 2
                });
            JxqyTextureBudgetReport budget = JxqyTextureBudget.Evaluate(packed.Pages);
            List<string> atlasAssetPaths = JxqyAtlasAssetWriter.WritePages(
                outputDirectory,
                "animation",
                packed.Pages);
            CleanupStaleAtlasAssets(outputDirectory, atlasAssetPaths);

            metadata.AtlasAddresses.Clear();
            foreach (string atlasAssetPath in atlasAssetPaths)
            {
                metadata.AtlasAddresses.Add(
                    JxqyAddressByRelativePath.CreateAddress(
                        atlasAssetPath,
                        normalizedOutputRoot));
            }
            WriteJsonAsset(metadataAssetPath, metadata, true);

            stopwatch.Stop();
            return new JxqyAnimationConversionFileReport
            {
                RelativePath = source.RelativePath,
                StableId = source.StableId,
                Status = ConvertedStatus,
                OutputMetadataAssetPath = metadataAssetPath,
                FrameCount = metadata.FrameCount,
                AtlasPageCount = packed.Pages.Count,
                MaximumAtlasWidth = budget.MaximumWidth,
                MaximumAtlasHeight = budget.MaximumHeight,
                SourcePixelCount = packed.SourcePixelCount,
                TrimmedPixelCount = packed.TrimmedPixelCount,
                AtlasPixelCount = packed.AtlasPixelCount,
                StandaloneBytes = budget.StandaloneBytes,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
            };
        }

        private static bool CanReuse(
            string metadataAssetPath,
            string outputDirectory,
            JxqySourceFileRecord source,
            out JxqyAnimationMetadata metadata)
        {
            metadata = null;
            string absoluteMetadataPath = GetAbsoluteAssetPath(metadataAssetPath);
            if (!File.Exists(absoluteMetadataPath))
                return false;

            try
            {
                metadata = JsonUtility.FromJson<JxqyAnimationMetadata>(
                    File.ReadAllText(absoluteMetadataPath));
            }
            catch
            {
                return false;
            }

            if (metadata == null ||
                !string.Equals(
                    metadata.ConverterVersion,
                    AnimationConverterVersion,
                    StringComparison.Ordinal) ||
                !string.Equals(metadata.SourceStableId, source.StableId, StringComparison.Ordinal) ||
                !string.Equals(metadata.SourceSha256, source.Sha256, StringComparison.Ordinal) ||
                metadata.AtlasAddresses == null ||
                metadata.AtlasAddresses.Count == 0)
            {
                return false;
            }

            for (int page = 0; page < metadata.AtlasAddresses.Count; page++)
            {
                string atlasAssetPath = $"{outputDirectory}/animation.atlas.{page:D3}.png";
                if (!File.Exists(GetAbsoluteAssetPath(atlasAssetPath)))
                    return false;
            }
            return true;
        }

        private static JxqyAnimationConversionFileReport CreateReportFromAssets(
            JxqySourceFileRecord source,
            string metadataAssetPath,
            string outputDirectory,
            JxqyAnimationMetadata metadata,
            string status)
        {
            var report = new JxqyAnimationConversionFileReport
            {
                RelativePath = source.RelativePath,
                StableId = source.StableId,
                Status = status,
                OutputMetadataAssetPath = metadataAssetPath,
                FrameCount = metadata.FrameCount
            };

            foreach (JxqyAnimationFrameMetadata frame in metadata.Frames)
            {
                report.SourcePixelCount += checked((long)frame.PixelWidth * frame.PixelHeight);
                report.TrimmedPixelCount += checked((long)frame.AtlasWidth * frame.AtlasHeight);
            }

            for (int page = 0; page < metadata.AtlasAddresses.Count; page++)
            {
                string atlasAssetPath = $"{outputDirectory}/animation.atlas.{page:D3}.png";
                (int width, int height) = ReadPngDimensions(GetAbsoluteAssetPath(atlasAssetPath));
                report.AtlasPageCount++;
                report.MaximumAtlasWidth = Math.Max(report.MaximumAtlasWidth, width);
                report.MaximumAtlasHeight = Math.Max(report.MaximumAtlasHeight, height);
                report.AtlasPixelCount += checked((long)width * height);
                report.StandaloneBytes += checked((long)width * height * 4);
            }
            return report;
        }

        private static FileInfo ValidateSourceFingerprint(
            string sourcePath,
            JxqySourceFileRecord source)
        {
            var info = new FileInfo(sourcePath);
            if (!info.Exists)
                throw new FileNotFoundException("Animation source file does not exist.", sourcePath);
            if (info.Length != source.Size ||
                info.LastWriteTimeUtc.Ticks != source.LastWriteUtcTicks)
            {
                throw new IOException(
                    $"Source changed after manifest scan: {source.RelativePath}. Rescan before converting.");
            }
            return info;
        }

        private static void ValidateSourceUnchanged(string sourcePath, FileInfo before)
        {
            var after = new FileInfo(sourcePath);
            if (before.Length != after.Length ||
                before.LastWriteTimeUtc.Ticks != after.LastWriteTimeUtc.Ticks)
            {
                throw new IOException($"Source changed while it was being read: {sourcePath}");
            }
        }

        private static string ResolveSourcePath(string sourceRoot, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(sourceRoot) || !Path.IsPathRooted(sourceRoot))
                throw new ArgumentException("Source root must be absolute.", nameof(sourceRoot));

            string normalizedRoot = Path.GetFullPath(sourceRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string resolved = Path.GetFullPath(Path.Combine(
                normalizedRoot,
                NormalizeRelativePath(relativePath)
                    .Replace('/', Path.DirectorySeparatorChar)));
            if (!resolved.StartsWith(
                    normalizedRoot + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException($"Source path escapes its root: {relativePath}");
            }
            return resolved;
        }

        private static string NormalizeOutputRoot(string outputRoot)
        {
            string normalized = outputRoot?.Replace('\\', '/').TrimEnd('/');
            if (string.IsNullOrWhiteSpace(normalized) ||
                !normalized.StartsWith("Assets/AssetRaw/Jxqy", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Animation output root must stay below Assets/AssetRaw/Jxqy.",
                    nameof(outputRoot));
            }
            return normalized;
        }

        private static string NormalizeRelativePath(string relativePath)
        {
            string normalized = relativePath?.Replace('\\', '/').TrimStart('/');
            if (string.IsNullOrWhiteSpace(normalized) ||
                normalized.Equals("..", StringComparison.Ordinal) ||
                normalized.StartsWith("../", StringComparison.Ordinal) ||
                normalized.Contains("/../"))
            {
                throw new ArgumentException("Relative source path is invalid.", nameof(relativePath));
            }
            return normalized;
        }

        private static void CleanupStaleAtlasAssets(
            string outputDirectory,
            IReadOnlyCollection<string> desiredAssetPaths)
        {
            string absoluteDirectory = GetAbsoluteAssetPath(outputDirectory);
            if (!Directory.Exists(absoluteDirectory))
                return;

            var desired = new HashSet<string>(
                desiredAssetPaths.Select(path => path.Replace('\\', '/')),
                StringComparer.OrdinalIgnoreCase);
            foreach (string absoluteFile in Directory.GetFiles(
                         absoluteDirectory,
                         "animation.atlas.*.png",
                         SearchOption.TopDirectoryOnly))
            {
                string assetPath = ToAssetPath(absoluteFile);
                if (!desired.Contains(assetPath))
                    AssetDatabase.DeleteAsset(assetPath);
            }
        }

        internal static void WriteJsonAsset<T>(
            string assetPath,
            T value,
            bool importAsset)
        {
            string absolutePath = GetAbsoluteAssetPath(assetPath);
            string absoluteDirectory = Path.GetDirectoryName(absolutePath);
            Directory.CreateDirectory(absoluteDirectory);

            string temporaryPath = absolutePath + ".tmp";
            string backupPath = absolutePath + ".bak";
            File.WriteAllText(
                temporaryPath,
                JsonUtility.ToJson(value, true),
                new System.Text.UTF8Encoding(false));
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

            if (importAsset)
            {
                AssetDatabase.ImportAsset(
                    assetPath,
                    ImportAssetOptions.ForceSynchronousImport);
            }
        }

        private static (int Width, int Height) ReadPngDimensions(string absolutePath)
        {
            var header = new byte[24];
            using (var stream = new FileStream(
                       absolutePath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.Read))
            {
                if (stream.Read(header, 0, header.Length) != header.Length)
                    throw new IOException($"PNG header is truncated: {absolutePath}");
            }

            byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
            for (int index = 0; index < signature.Length; index++)
            {
                if (header[index] != signature[index])
                    throw new IOException($"Invalid PNG signature: {absolutePath}");
            }

            int width = ReadBigEndianInt32(header, 16);
            int height = ReadBigEndianInt32(header, 20);
            if (width <= 0 || height <= 0)
                throw new IOException($"Invalid PNG dimensions: {absolutePath}");
            return (width, height);
        }

        private static int ReadBigEndianInt32(byte[] bytes, int offset)
        {
            return bytes[offset] << 24 |
                   bytes[offset + 1] << 16 |
                   bytes[offset + 2] << 8 |
                   bytes[offset + 3];
        }

        private static string GetAbsoluteAssetPath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string ToAssetPath(string absolutePath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullPath = Path.GetFullPath(absolutePath);
            if (!fullPath.StartsWith(
                    projectRoot + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException($"Generated atlas is outside Unity project: {absolutePath}");
            }
            return fullPath.Substring(projectRoot.Length + 1).Replace('\\', '/');
        }
    }
}
