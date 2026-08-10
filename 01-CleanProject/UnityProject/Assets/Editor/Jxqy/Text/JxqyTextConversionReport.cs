using System;
using System.Collections.Generic;

namespace Jxqy.Editor.Text
{
    [Serializable]
    public sealed class JxqyTextConversionFileReport
    {
        public string RelativePath = string.Empty;
        public string StableId = string.Empty;
        public string Kind = string.Empty;
        public string Status = string.Empty;
        public string Encoding = string.Empty;
        public string ContentAssetPath = string.Empty;
        public string MetadataAssetPath = string.Empty;
        public string Error = string.Empty;
        public int LineCount;
        public int SectionCount;
        public int PropertyCount;
        public int UnparsedStructuredLineCount;
    }

    [Serializable]
    public sealed class JxqyTextConversionReport
    {
        public string ConverterVersion = string.Empty;
        public string GeneratedUtc = string.Empty;
        public int InputFileCount;
        public int ConvertedFileCount;
        public int ReusedFileCount;
        public int FailedFileCount;
        public long TotalLineCount;
        public long TotalSectionCount;
        public long TotalPropertyCount;
        public long UnparsedStructuredLineCount;
        public List<JxqyTextConversionFileReport> Files = new();

        public void Add(JxqyTextConversionFileReport file)
        {
            Files.Add(file);
            if (file.Status == JxqyTextConverter.ConvertedStatus)
                ConvertedFileCount++;
            else if (file.Status == JxqyTextConverter.ReusedStatus)
                ReusedFileCount++;
            else
                FailedFileCount++;
            TotalLineCount += file.LineCount;
            TotalSectionCount += file.SectionCount;
            TotalPropertyCount += file.PropertyCount;
            UnparsedStructuredLineCount += file.UnparsedStructuredLineCount;
        }
    }
}
