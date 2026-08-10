using System;
using System.Collections.Generic;

namespace Jxqy.Editor.Animation.Conversion
{
    [Serializable]
    public sealed class JxqyAnimationConversionFileReport
    {
        public string RelativePath = string.Empty;
        public string StableId = string.Empty;
        public string Status = string.Empty;
        public string OutputMetadataAssetPath = string.Empty;
        public string Error = string.Empty;
        public int FrameCount;
        public int AtlasPageCount;
        public int MaximumAtlasWidth;
        public int MaximumAtlasHeight;
        public long SourcePixelCount;
        public long TrimmedPixelCount;
        public long AtlasPixelCount;
        public long StandaloneBytes;
        public long ElapsedMilliseconds;
    }

    [Serializable]
    public sealed class JxqyAnimationConversionReport
    {
        public string ConverterVersion = string.Empty;
        public string SourceManifestConverterVersion = string.Empty;
        public string SourceRoot = string.Empty;
        public string OutputRoot = string.Empty;
        public string StartedUtc = string.Empty;
        public string UpdatedUtc = string.Empty;
        public string CompletedUtc = string.Empty;
        public string LastProcessedRelativePath = string.Empty;
        public bool IsComplete;
        public int InputFileCount;
        public int ProcessedFileCount;
        public int ConvertedFileCount;
        public int ReusedFileCount;
        public int FailedFileCount;
        public long TotalFrameCount;
        public long TotalAtlasPageCount;
        public int MaximumAtlasWidth;
        public int MaximumAtlasHeight;
        public long SourcePixelCount;
        public long TrimmedPixelCount;
        public long AtlasPixelCount;
        public long StandaloneBytes;
        public List<JxqyAnimationConversionFileReport> Files = new();

        public void Add(JxqyAnimationConversionFileReport file)
        {
            if (file == null)
                throw new ArgumentNullException(nameof(file));

            Files.Add(file);
            ProcessedFileCount++;
            LastProcessedRelativePath = file.RelativePath;
            switch (file.Status)
            {
                case JxqyAnimationConverter.ConvertedStatus:
                    ConvertedFileCount++;
                    break;
                case JxqyAnimationConverter.ReusedStatus:
                    ReusedFileCount++;
                    break;
                default:
                    FailedFileCount++;
                    break;
            }

            TotalFrameCount += file.FrameCount;
            TotalAtlasPageCount += file.AtlasPageCount;
            MaximumAtlasWidth = Math.Max(MaximumAtlasWidth, file.MaximumAtlasWidth);
            MaximumAtlasHeight = Math.Max(MaximumAtlasHeight, file.MaximumAtlasHeight);
            SourcePixelCount += file.SourcePixelCount;
            TrimmedPixelCount += file.TrimmedPixelCount;
            AtlasPixelCount += file.AtlasPixelCount;
            StandaloneBytes += file.StandaloneBytes;
        }
    }
}
