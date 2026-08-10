using System;
using System.IO;
using Jxqy.Domain.Content;
using Jxqy.Editor.Animation.Conversion;
using Jxqy.Editor.Scanning;
using Jxqy.Editor.YooAsset;
using UnityEditor;
using UnityEngine;

namespace Jxqy.Editor.Audio
{
    public sealed class JxqySoundConverter
    {
        public const string SoundConverterVersion = "0.1.0-sound-1";
        public const string ConvertedStatus = "Converted";
        public const string ReusedStatus = "Reused";
        public const string FailedStatus = "Failed";

        public JxqySoundConversionFileReport Convert(
            JxqySourceFileRecord source,
            string sourceRoot,
            string outputRoot)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            string relative = source.RelativePath.Replace('\\', '/').TrimStart('/');
            if (source.Kind != JxqyFileKind.Xnb ||
                !relative.StartsWith("Content/sound/", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "Source is not a Content/sound XNB.",
                    nameof(source));
            }

            string normalizedOutput = outputRoot.Replace('\\', '/').TrimEnd('/');
            string outputDirectory = $"{normalizedOutput}/Audio/Sound/{relative}";
            string wavAssetPath = outputDirectory + "/sound.wav";
            string metadataAssetPath = outputDirectory + "/metadata.json";
            if (CanReuse(metadataAssetPath, wavAssetPath, source, out var reused))
            {
                return CreateReport(
                    source,
                    wavAssetPath,
                    metadataAssetPath,
                    reused,
                    ReusedStatus);
            }

            string sourcePath = Path.GetFullPath(Path.Combine(
                sourceRoot,
                relative.Replace('/', Path.DirectorySeparatorChar)));
            var before = new FileInfo(sourcePath);
            if (!before.Exists)
                throw new FileNotFoundException("Sound XNB is missing.", sourcePath);
            if (before.Length != source.Size ||
                before.LastWriteTimeUtc.Ticks != source.LastWriteUtcTicks)
            {
                throw new IOException(
                    $"Source changed after manifest scan: {source.RelativePath}.");
            }

            JxqyDecodedSoundEffect sound =
                JxqyXnbSoundEffectDecoder.Decode(File.ReadAllBytes(sourcePath));
            var after = new FileInfo(sourcePath);
            if (before.Length != after.Length ||
                before.LastWriteTimeUtc.Ticks != after.LastWriteTimeUtc.Ticks)
            {
                throw new IOException(
                    $"Sound XNB changed while reading: {source.RelativePath}.");
            }

            WriteWavAsset(wavAssetPath, JxqyWaveWriter.WritePcmWave(sound));
            var metadata = new JxqyAudioMetadata
            {
                ConverterVersion = SoundConverterVersion,
                SourceStableId = source.StableId,
                SourceRelativePath = source.RelativePath,
                SourceAddress = source.SourceAddress,
                SourceSha256 = source.Sha256,
                WavAddress = JxqyAddressByRelativePath.CreateAddress(
                    wavAssetPath,
                    normalizedOutput),
                FormatTag = sound.FormatTag,
                Channels = sound.Channels,
                SampleRate = sound.SampleRate,
                BitsPerSample = sound.BitsPerSample,
                PcmByteCount = sound.PcmData.Length,
                LoopStart = sound.LoopStart,
                LoopLength = sound.LoopLength,
                DurationMilliseconds = sound.DurationMilliseconds
            };
            JxqyAnimationConverter.WriteJsonAsset(metadataAssetPath, metadata, true);
            return CreateReport(
                source,
                wavAssetPath,
                metadataAssetPath,
                metadata,
                ConvertedStatus);
        }

        private static bool CanReuse(
            string metadataAssetPath,
            string wavAssetPath,
            JxqySourceFileRecord source,
            out JxqyAudioMetadata metadata)
        {
            metadata = null;
            string absoluteMetadata = GetAbsoluteAssetPath(metadataAssetPath);
            if (!File.Exists(absoluteMetadata) ||
                !File.Exists(GetAbsoluteAssetPath(wavAssetPath)))
            {
                return false;
            }
            try
            {
                metadata = JsonUtility.FromJson<JxqyAudioMetadata>(
                    File.ReadAllText(absoluteMetadata));
                return metadata != null &&
                       metadata.ConverterVersion == SoundConverterVersion &&
                       metadata.SourceStableId == source.StableId &&
                       metadata.SourceSha256 == source.Sha256;
            }
            catch
            {
                return false;
            }
        }

        private static JxqySoundConversionFileReport CreateReport(
            JxqySourceFileRecord source,
            string wavAssetPath,
            string metadataAssetPath,
            JxqyAudioMetadata metadata,
            string status)
        {
            return new JxqySoundConversionFileReport
            {
                RelativePath = source.RelativePath,
                StableId = source.StableId,
                Status = status,
                WavAssetPath = wavAssetPath,
                MetadataAssetPath = metadataAssetPath,
                Channels = metadata.Channels,
                SampleRate = metadata.SampleRate,
                BitsPerSample = metadata.BitsPerSample,
                PcmByteCount = metadata.PcmByteCount,
                DurationMilliseconds = metadata.DurationMilliseconds
            };
        }

        private static void WriteWavAsset(string assetPath, byte[] bytes)
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
            JxqyAudioImportConfigurator.ConfigurePcmSoundEffect(assetPath);
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
