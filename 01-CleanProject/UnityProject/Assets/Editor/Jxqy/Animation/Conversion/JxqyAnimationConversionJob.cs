using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Jxqy.Editor.Scanning;
using UnityEditor;
using UnityEngine;

namespace Jxqy.Editor.Animation.Conversion
{
    [InitializeOnLoad]
    public static class JxqyAnimationConversionJob
    {
        private const string ManifestAssetPath =
            "Assets/AssetRaw/Jxqy/Manifests/source-manifest.json";
        private const string ReportAssetPath =
            "Assets/AssetRaw/Jxqy/Reports/animation-conversion-report.json";
        private const int ReportWriteInterval = 10;
        private static readonly string RequestPath = Path.Combine(
            Path.GetDirectoryName(Application.dataPath) ?? string.Empty,
            "Temp",
            "JxqyConversion",
            "run-animation.request");

        private static readonly JxqyAnimationConverter Converter = new();
        private static List<JxqySourceFileRecord> _sources;
        private static JxqySourceSettings _settings;
        private static JxqyAnimationConversionReport _report;
        private static int _nextIndex;
        private static bool _isRunning;

        static JxqyAnimationConversionJob()
        {
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
        }

        public static bool IsRunning => _isRunning;

        [MenuItem("TEngine/Jxqy/Convert All Animations")]
        public static void Start()
        {
            if (_isRunning)
            {
                Debug.LogWarning("Jxqy animation conversion is already running.");
                return;
            }

            try
            {
                _settings = new JxqySourceSettings();
                _settings.Validate();
                JxqySourceManifest manifest = LoadManifest();
                if (!manifest.IsValid)
                {
                    throw new InvalidOperationException(
                        "Source manifest contains errors or address collisions. Rescan first.");
                }
                if (!manifest.IncludesHashes)
                    throw new InvalidOperationException("Source manifest must include SHA-256 hashes.");

                _sources = manifest.Files
                    .Where(file => file.Kind == JxqyFileKind.Asf ||
                                   file.Kind == JxqyFileKind.Mpc)
                    .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
                    .ToList();
                _nextIndex = 0;
                string now = UtcNow();
                _report = new JxqyAnimationConversionReport
                {
                    ConverterVersion = JxqyAnimationConverter.AnimationConverterVersion,
                    SourceManifestConverterVersion = manifest.ConverterVersion,
                    SourceRoot = manifest.SourceRoot,
                    OutputRoot = _settings.OutputRoot,
                    StartedUtc = now,
                    UpdatedUtc = now,
                    InputFileCount = _sources.Count
                };
                WriteReport(false);
                _isRunning = true;
                Debug.Log(
                    $"Jxqy full animation conversion started. Inputs={_sources.Count}, " +
                    $"Output={_settings.OutputRoot}/Animations");
            }
            catch (Exception exception)
            {
                _isRunning = false;
                Debug.LogException(exception);
                if (Application.isBatchMode)
                    throw;
            }
        }

        [MenuItem("TEngine/Jxqy/Cancel Animation Conversion")]
        public static void Cancel()
        {
            if (!_isRunning)
                return;

            _isRunning = false;
            _report.UpdatedUtc = UtcNow();
            WriteReport(true);
            Debug.LogWarning(
                $"Jxqy animation conversion cancelled after {_report.ProcessedFileCount}/" +
                $"{_report.InputFileCount}. Run it again to reuse completed outputs.");
        }

        private static void Update()
        {
            if (!_isRunning && File.Exists(RequestPath))
            {
                File.Delete(RequestPath);
                Start();
            }

            if (!_isRunning || EditorApplication.isCompiling ||
                EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (_nextIndex >= _sources.Count)
            {
                Complete();
                return;
            }

            JxqySourceFileRecord source = _sources[_nextIndex++];
            JxqyAnimationConversionFileReport fileReport;
            try
            {
                fileReport = Converter.Convert(
                    source,
                    _settings.GameSourceRoot,
                    _settings.OutputRoot);
            }
            catch (Exception exception)
            {
                fileReport = new JxqyAnimationConversionFileReport
                {
                    RelativePath = source.RelativePath,
                    StableId = source.StableId,
                    Status = JxqyAnimationConverter.FailedStatus,
                    Error = $"{exception.GetType().Name}: {exception.Message}"
                };
                Debug.LogError(
                    $"Jxqy animation conversion failed: {source.RelativePath}\n{exception}");
            }

            _report.Add(fileReport);
            _report.UpdatedUtc = UtcNow();
            if (_report.ProcessedFileCount % ReportWriteInterval == 0)
                WriteReport(false);
            if (_report.ProcessedFileCount % 50 == 0)
            {
                Debug.Log(
                    $"Jxqy animation conversion progress: {_report.ProcessedFileCount}/" +
                    $"{_report.InputFileCount}, converted={_report.ConvertedFileCount}, " +
                    $"reused={_report.ReusedFileCount}, failed={_report.FailedFileCount}");
            }
        }

        private static void Complete()
        {
            _isRunning = false;
            _report.IsComplete = true;
            _report.CompletedUtc = UtcNow();
            _report.UpdatedUtc = _report.CompletedUtc;
            WriteReport(true);
            string summary =
                $"Jxqy full animation conversion complete. Inputs={_report.InputFileCount}, " +
                $"Converted={_report.ConvertedFileCount}, Reused={_report.ReusedFileCount}, " +
                $"Failed={_report.FailedFileCount}, Frames={_report.TotalFrameCount}, " +
                $"AtlasPages={_report.TotalAtlasPageCount}.";
            if (_report.FailedFileCount == 0)
                Debug.Log(summary);
            else
                Debug.LogError(summary);
        }

        private static JxqySourceManifest LoadManifest()
        {
            string absolutePath = GetAbsoluteAssetPath(ManifestAssetPath);
            if (!File.Exists(absolutePath))
            {
                throw new FileNotFoundException(
                    "Jxqy source manifest is missing. Run source scan first.",
                    absolutePath);
            }

            JxqySourceManifest manifest = JsonUtility.FromJson<JxqySourceManifest>(
                File.ReadAllText(absolutePath));
            return manifest ?? throw new InvalidDataException(
                "Jxqy source manifest could not be parsed.");
        }

        private static void WriteReport(bool importAsset)
        {
            JxqyAnimationConverter.WriteJsonAsset(
                ReportAssetPath,
                _report,
                importAsset);
        }

        private static string GetAbsoluteAssetPath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string UtcNow()
        {
            return DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        }
    }
}
