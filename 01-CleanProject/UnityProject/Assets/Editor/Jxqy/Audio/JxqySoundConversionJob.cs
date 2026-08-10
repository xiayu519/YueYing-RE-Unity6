using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Jxqy.Editor.Animation.Conversion;
using Jxqy.Editor.Scanning;
using UnityEditor;
using UnityEngine;

namespace Jxqy.Editor.Audio
{
    public static class JxqySoundConversionJob
    {
        private const string ManifestAssetPath =
            "Assets/AssetRaw/Jxqy/Manifests/source-manifest.json";
        private const string ReportAssetPath =
            "Assets/AssetRaw/Jxqy/Reports/sound-conversion-report.json";

        [MenuItem("TEngine/Jxqy/Convert Sound XNB To WAV")]
        public static void ConvertAll()
        {
            var report = new JxqySoundConversionReport
            {
                ConverterVersion = JxqySoundConverter.SoundConverterVersion,
                GeneratedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            };
            try
            {
                var settings = new JxqySourceSettings();
                settings.Validate();
                JxqySourceManifest manifest = JsonUtility.FromJson<JxqySourceManifest>(
                    File.ReadAllText(GetAbsoluteAssetPath(ManifestAssetPath)));
                var sources = manifest.Files
                    .Where(file =>
                        file.Kind == JxqyFileKind.Xnb &&
                        file.RelativePath.StartsWith(
                            "Content/sound/",
                            StringComparison.OrdinalIgnoreCase))
                    .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
                    .ToArray();
                report.InputFileCount = sources.Length;
                var converter = new JxqySoundConverter();
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
                        report.Add(new JxqySoundConversionFileReport
                        {
                            RelativePath = source.RelativePath,
                            StableId = source.StableId,
                            Status = JxqySoundConverter.FailedStatus,
                            Error = $"{exception.GetType().Name}: {exception.Message}"
                        });
                    }
                }
                JxqyAnimationConverter.WriteJsonAsset(
                    ReportAssetPath,
                    report,
                    true);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            string summary =
                $"Jxqy sound conversion complete. Inputs={report.InputFileCount}, " +
                $"Converted={report.ConvertedFileCount}, Reused={report.ReusedFileCount}, " +
                $"Failed={report.FailedFileCount}, PCMBytes={report.TotalPcmBytes}, " +
                $"DurationMs={report.TotalDurationMilliseconds}.";
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
