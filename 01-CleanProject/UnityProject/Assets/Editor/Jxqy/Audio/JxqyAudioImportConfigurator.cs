using System;
using UnityEditor;
using UnityEngine;

namespace Jxqy.Editor.Audio
{
    public static class JxqyAudioImportConfigurator
    {
        public static void ConfigurePcmSoundEffect(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
            if (importer == null)
                throw new InvalidOperationException(
                    $"AudioImporter not found for {assetPath}.");

            importer.forceToMono = false;
            importer.loadInBackground = false;
            importer.ambisonic = false;
            importer.defaultSampleSettings = new AudioImporterSampleSettings
            {
                loadType = AudioClipLoadType.DecompressOnLoad,
                preloadAudioData = true,
                compressionFormat = AudioCompressionFormat.PCM,
                quality = 1f,
                sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate
            };
            importer.SaveAndReimport();
        }
    }
}
