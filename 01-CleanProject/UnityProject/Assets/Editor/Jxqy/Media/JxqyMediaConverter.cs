using System;
using System.IO;
using Jxqy.Domain.Content;
using Jxqy.Editor.Animation.Conversion;
using Jxqy.Editor.Scanning;
using Jxqy.Editor.YooAsset;
using UnityEditor;
using UnityEngine;

namespace Jxqy.Editor.Media
{
    public sealed class JxqyMediaConverter
    {
        public const string MediaConverterVersion = "0.1.0-media-1";
        public const string ConvertedStatus = "Converted";
        public const string ReusedStatus = "Reused";
        public const string FailedStatus = "Failed";
        private readonly string _ffmpegPath;
        private readonly string _ffprobePath;

        public JxqyMediaConverter(string ffmpegPath, string ffprobePath)
        {
            _ffmpegPath = ffmpegPath;
            _ffprobePath = ffprobePath;
        }

        public JxqyMediaConversionFileReport Convert(
            JxqySourceFileRecord source,
            string sourceRoot,
            string outputRoot)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            bool isMusic = source.Kind == JxqyFileKind.Music &&
                           source.Extension == ".wma";
            bool isVideo = source.Kind == JxqyFileKind.Video &&
                           source.Extension == ".wmv";
            if (!isMusic && !isVideo)
                throw new ArgumentException("Source is not WMA or WMV.", nameof(source));

            string normalizedOutput = outputRoot.Replace('\\', '/').TrimEnd('/');
            string relative = source.RelativePath.Replace('\\', '/').TrimStart('/');
            string mediaFolder = isMusic ? "Music" : "Video";
            string fileName = isMusic ? "music.wav" : "video.mp4";
            string outputDirectory =
                $"{normalizedOutput}/Media/{mediaFolder}/{relative}";
            string outputAssetPath = outputDirectory + "/" + fileName;
            string metadataAssetPath = outputDirectory + "/metadata.json";

            string sourcePath = Path.GetFullPath(Path.Combine(
                sourceRoot,
                relative.Replace('/', Path.DirectorySeparatorChar)));
            var before = new FileInfo(sourcePath);
            if (!before.Exists)
                throw new FileNotFoundException("Media source is missing.", sourcePath);
            if (before.Length != source.Size ||
                before.LastWriteTimeUtc.Ticks != source.LastWriteUtcTicks)
            {
                throw new IOException(
                    $"Source changed after manifest scan: {source.RelativePath}.");
            }

            if (CanReuse(metadataAssetPath, outputAssetPath, source, out var reused))
            {
                return CreateReport(
                    source,
                    outputAssetPath,
                    metadataAssetPath,
                    reused,
                    ReusedStatus);
            }

            JxqyMediaProbeResult sourceProbe =
                JxqyMediaProbe.Probe(_ffprobePath, sourcePath);
            string absoluteOutput = GetAbsoluteAssetPath(outputAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteOutput));
            string temporaryOutput = absoluteOutput + (isMusic ? ".tmp.wav" : ".tmp.mp4");
            if (File.Exists(temporaryOutput))
                File.Delete(temporaryOutput);
            string arguments = isMusic
                ? "-hide_banner -nostdin -loglevel error -y -i " +
                  JxqyMediaProbe.Quote(sourcePath) +
                  " -map_metadata -1 -vn -c:a pcm_s16le " +
                  JxqyMediaProbe.Quote(temporaryOutput)
                : "-hide_banner -nostdin -loglevel error -y -i " +
                  JxqyMediaProbe.Quote(sourcePath) +
                  " -map_metadata -1 -c:v libx264 -preset medium -crf 18 " +
                  "-pix_fmt yuv420p -c:a aac -b:a 192k -movflags +faststart " +
                  JxqyMediaProbe.Quote(temporaryOutput);
            JxqyProcessResult process = JxqyProcessRunner.Run(
                _ffmpegPath,
                arguments,
                isMusic ? 10 * 60 * 1000 : 30 * 60 * 1000);
            if (process.ExitCode != 0 || !File.Exists(temporaryOutput))
            {
                throw new IOException(
                    $"ffmpeg failed for {source.RelativePath}: {process.StandardError}");
            }
            ReplaceFile(temporaryOutput, absoluteOutput);
            var after = new FileInfo(sourcePath);
            if (before.Length != after.Length ||
                before.LastWriteTimeUtc.Ticks != after.LastWriteTimeUtc.Ticks)
            {
                throw new IOException(
                    $"Media source changed while transcoding: {source.RelativePath}.");
            }

            JxqyMediaProbeResult outputProbe =
                JxqyMediaProbe.Probe(_ffprobePath, absoluteOutput);
            AssetDatabase.ImportAsset(
                outputAssetPath,
                ImportAssetOptions.ForceSynchronousImport);
            if (isMusic)
                JxqyMediaImportConfigurator.ConfigureMusic(outputAssetPath);
            var metadata = new JxqyMediaMetadata
            {
                ConverterVersion = MediaConverterVersion,
                SourceStableId = source.StableId,
                SourceRelativePath = source.RelativePath,
                SourceAddress = source.SourceAddress,
                SourceSha256 = source.Sha256,
                MediaKind = isMusic ? "Music" : "Video",
                OutputAddress = JxqyAddressByRelativePath.CreateAddress(
                    outputAssetPath,
                    normalizedOutput),
                TranscodeProfile = isMusic
                    ? "PCM s16le WAV; Unity Streaming Vorbis quality 1"
                    : "H.264 CRF18 yuv420p + AAC 192k MP4 faststart",
                SourceVideoCodec = sourceProbe.VideoCodec,
                SourceAudioCodec = sourceProbe.AudioCodec,
                OutputVideoCodec = outputProbe.VideoCodec,
                OutputAudioCodec = outputProbe.AudioCodec,
                Width = outputProbe.Width,
                Height = outputProbe.Height,
                FrameRate = outputProbe.FrameRate,
                SampleRate = outputProbe.SampleRate,
                Channels = outputProbe.Channels,
                SourceDurationSeconds = sourceProbe.DurationSeconds,
                OutputDurationSeconds = outputProbe.DurationSeconds
            };
            JxqyAnimationConverter.WriteJsonAsset(metadataAssetPath, metadata, true);
            return CreateReport(
                source,
                outputAssetPath,
                metadataAssetPath,
                metadata,
                ConvertedStatus);
        }

        private static bool CanReuse(
            string metadataAssetPath,
            string outputAssetPath,
            JxqySourceFileRecord source,
            out JxqyMediaMetadata metadata)
        {
            metadata = null;
            string absoluteMetadata = GetAbsoluteAssetPath(metadataAssetPath);
            if (!File.Exists(absoluteMetadata) ||
                !File.Exists(GetAbsoluteAssetPath(outputAssetPath)))
            {
                return false;
            }
            try
            {
                metadata = JsonUtility.FromJson<JxqyMediaMetadata>(
                    File.ReadAllText(absoluteMetadata));
                return metadata != null &&
                       metadata.ConverterVersion == MediaConverterVersion &&
                       metadata.SourceStableId == source.StableId &&
                       metadata.SourceSha256 == source.Sha256;
            }
            catch
            {
                return false;
            }
        }

        private static JxqyMediaConversionFileReport CreateReport(
            JxqySourceFileRecord source,
            string outputAssetPath,
            string metadataAssetPath,
            JxqyMediaMetadata metadata,
            string status)
        {
            return new JxqyMediaConversionFileReport
            {
                RelativePath = source.RelativePath,
                StableId = source.StableId,
                Kind = metadata.MediaKind,
                Status = status,
                OutputAssetPath = outputAssetPath,
                MetadataAssetPath = metadataAssetPath,
                SourceDurationSeconds = metadata.SourceDurationSeconds,
                OutputDurationSeconds = metadata.OutputDurationSeconds,
                OutputBytes = new FileInfo(
                    GetAbsoluteAssetPath(outputAssetPath)).Length
            };
        }

        private static void ReplaceFile(string temporaryPath, string finalPath)
        {
            string backupPath = finalPath + ".bak";
            if (File.Exists(finalPath))
            {
                File.Replace(temporaryPath, finalPath, backupPath);
                if (File.Exists(backupPath))
                    File.Delete(backupPath);
            }
            else
            {
                File.Move(temporaryPath, finalPath);
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
