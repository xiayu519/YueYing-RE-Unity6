using System;
using System.Collections.Generic;

namespace Jxqy.Editor.Image
{
    [Serializable]
    public sealed class JxqyStaticImageFileReport
    {
        public string RelativePath = string.Empty;
        public string StableId = string.Empty;
        public string Status = string.Empty;
        public string AssetPath = string.Empty;
        public string Address = string.Empty;
        public string Error = string.Empty;
        public long ByteCount;
    }

    [Serializable]
    public sealed class JxqyStaticImageConversionReport
    {
        public string ConverterVersion = string.Empty;
        public string GeneratedUtc = string.Empty;
        public int InputFileCount;
        public int ConvertedFileCount;
        public int ReusedFileCount;
        public int FailedFileCount;
        public long TotalBytes;
        public List<JxqyStaticImageFileReport> Files = new();

        public void Add(JxqyStaticImageFileReport file)
        {
            Files.Add(file);
            if (file.Status == JxqyStaticImageConverter.ConvertedStatus)
                ConvertedFileCount++;
            else if (file.Status == JxqyStaticImageConverter.ReusedStatus)
                ReusedFileCount++;
            else
                FailedFileCount++;
            TotalBytes += file.ByteCount;
        }
    }
}
