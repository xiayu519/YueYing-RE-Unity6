using System;
using System.Collections.Generic;

namespace Jxqy.Editor.Audio
{
    [Serializable]
    public sealed class JxqySoundConversionFileReport
    {
        public string RelativePath = string.Empty;
        public string StableId = string.Empty;
        public string Status = string.Empty;
        public string WavAssetPath = string.Empty;
        public string MetadataAssetPath = string.Empty;
        public string Error = string.Empty;
        public int Channels;
        public int SampleRate;
        public int BitsPerSample;
        public int PcmByteCount;
        public int DurationMilliseconds;
    }

    [Serializable]
    public sealed class JxqySoundConversionReport
    {
        public string ConverterVersion = string.Empty;
        public string GeneratedUtc = string.Empty;
        public int InputFileCount;
        public int ConvertedFileCount;
        public int ReusedFileCount;
        public int FailedFileCount;
        public long TotalPcmBytes;
        public long TotalDurationMilliseconds;
        public List<JxqySoundConversionFileReport> Files = new();

        public void Add(JxqySoundConversionFileReport file)
        {
            Files.Add(file);
            if (file.Status == JxqySoundConverter.ConvertedStatus)
                ConvertedFileCount++;
            else if (file.Status == JxqySoundConverter.ReusedStatus)
                ReusedFileCount++;
            else
                FailedFileCount++;
            TotalPcmBytes += file.PcmByteCount;
            TotalDurationMilliseconds += file.DurationMilliseconds;
        }
    }
}
