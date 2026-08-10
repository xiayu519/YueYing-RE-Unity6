using System;
using UnityEditor;
using UnityEngine;

namespace Jxqy.Editor.Media
{
    public static class JxqyMediaImportConfigurator
    {
        public static void ConfigureMusic(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
            if (importer == null)
                throw new InvalidOperationException(
                    $"AudioImporter not found for {assetPath}.");
            importer.forceToMono = false;
            importer.loadInBackground = true;
            importer.ambisonic = false;
            importer.defaultSampleSettings = new AudioImporterSampleSettings
            {
                loadType = AudioClipLoadType.Streaming,
                preloadAudioData = false,
                compressionFormat = AudioCompressionFormat.Vorbis,
                quality = 1f,
                sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate
            };
            importer.SaveAndReimport();
        }
    }
}
