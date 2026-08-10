using System;
using System.Collections.Generic;

namespace Jxqy.Editor.Media
{
    [Serializable]
    public sealed class JxqyMediaConversionFileReport
    {
        public string RelativePath = string.Empty;
        public string StableId = string.Empty;
        public string Kind = string.Empty;
        public string Status = string.Empty;
        public string OutputAssetPath = string.Empty;
        public string MetadataAssetPath = string.Empty;
        public string Error = string.Empty;
        public double SourceDurationSeconds;
        public double OutputDurationSeconds;
        public long OutputBytes;
    }

    [Serializable]
    public sealed class JxqyMediaConversionReport
    {
        public string ConverterVersion = string.Empty;
        public string GeneratedUtc = string.Empty;
        public string FfmpegPath = string.Empty;
        public string FfprobePath = string.Empty;
        public int InputFileCount;
        public int ConvertedFileCount;
        public int ReusedFileCount;
        public int FailedFileCount;
        public double TotalSourceDurationSeconds;
        public double TotalOutputDurationSeconds;
        public long TotalOutputBytes;
        public List<JxqyMediaConversionFileReport> Files = new();

        public void Add(JxqyMediaConversionFileReport file)
        {
            Files.Add(file);
            if (file.Status == JxqyMediaConverter.ConvertedStatus)
                ConvertedFileCount++;
            else if (file.Status == JxqyMediaConverter.ReusedStatus)
                ReusedFileCount++;
            else
                FailedFileCount++;
            TotalSourceDurationSeconds += file.SourceDurationSeconds;
            TotalOutputDurationSeconds += file.OutputDurationSeconds;
            TotalOutputBytes += file.OutputBytes;
        }
    }
}
