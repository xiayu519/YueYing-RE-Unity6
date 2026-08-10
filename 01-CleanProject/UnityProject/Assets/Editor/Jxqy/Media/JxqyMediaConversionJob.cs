using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Jxqy.Editor.Animation.Conversion;
using Jxqy.Editor.Scanning;
using UnityEditor;
using UnityEngine;

namespace Jxqy.Editor.Media
{
    public static class JxqyMediaConversionJob
    {
        private const string ManifestAssetPath =
            "Assets/AssetRaw/Jxqy/Manifests/source-manifest.json";
        private const string ReportAssetPath =
            "Assets/AssetRaw/Jxqy/Reports/media-conversion-report.json";

        [MenuItem("TEngine/Jxqy/Transcode Music And Video")]
        public static void ConvertAll()
        {
            var report = new JxqyMediaConversionReport
            {
                ConverterVersion = JxqyMediaConverter.MediaConverterVersion,
                GeneratedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            };
            try
            {
                report.FfmpegPath = JxqyMediaProbe.ResolveExecutable("ffmpeg.exe");
                report.FfprobePath = JxqyMediaProbe.ResolveExecutable("ffprobe.exe");
                var settings = new JxqySourceSettings();
                settings.Validate();
                JxqySourceManifest manifest = JsonUtility.FromJson<JxqySourceManifest>(
                    File.ReadAllText(GetAbsoluteAssetPath(ManifestAssetPath)));
                var sources = manifest.Files
                    .Where(file =>
                        file.Kind == JxqyFileKind.Music && file.Extension == ".wma" ||
                        file.Kind == JxqyFileKind.Video && file.Extension == ".wmv")
                    .OrderBy(file => file.Kind)
                    .ThenBy(file => file.RelativePath, StringComparer.Ordinal)
                    .ToArray();
                report.InputFileCount = sources.Length;
                var converter = new JxqyMediaConverter(
                    report.FfmpegPath,
                    report.FfprobePath);
                foreach (JxqySourceFileRecord source in sources)
                {
                    try
                    {
                        report.Add(converter.Convert(
                            source,
                            settings.GameSourceRoot,
                            settings.OutputRoot));
                    }
                    catch (Exception exception)
                    {
                        report.Add(new JxqyMediaConversionFileReport
                        {
                            RelativePath = source.RelativePath,
                            StableId = source.StableId,
                            Kind = source.Kind.ToString(),
                            Status = JxqyMediaConverter.FailedStatus,
                            Error = $"{exception.GetType().Name}: {exception.Message}"
                        });
                    }
                    JxqyAnimationConverter.WriteJsonAsset(
                        ReportAssetPath,
                        report,
                        false);
                    Debug.Log(
                        $"Jxqy media progress {report.Files.Count}/{report.InputFileCount}: " +
                        $"{source.RelativePath}, failed={report.FailedFileCount}");
                }
                AssetDatabase.ImportAsset(
                    ReportAssetPath,
                    ImportAssetOptions.ForceSynchronousImport);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            string summary =
                $"Jxqy media conversion complete. Inputs={report.InputFileCount}, " +
                $"Converted={report.ConvertedFileCount}, Reused={report.ReusedFileCount}, " +
                $"Failed={report.FailedFileCount}, OutputBytes={report.TotalOutputBytes}.";
            if (report.FailedFileCount == 0)
                Debug.Log(summary);
            else
                Debug.LogError(summary);
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
