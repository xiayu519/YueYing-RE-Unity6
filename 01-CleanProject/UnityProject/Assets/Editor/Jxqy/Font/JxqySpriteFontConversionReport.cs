using System;
using System.Collections.Generic;

namespace Jxqy.Editor.Font
{
    [Serializable]
    public sealed class JxqySpriteFontConversionFileReport
    {
        public string RelativePath = string.Empty;
        public string StableId = string.Empty;
        public string Status = string.Empty;
        public string TextureAssetPath = string.Empty;
        public string MetadataAssetPath = string.Empty;
        public string ReferenceAssetPath = string.Empty;
        public string Error = string.Empty;
        public int TextureWidth;
        public int TextureHeight;
        public int GlyphCount;
    }

    [Serializable]
    public sealed class JxqySpriteFontConversionReport
    {
        public string ConverterVersion = string.Empty;
        public string GeneratedUtc = string.Empty;
        public int InputFileCount;
        public int ConvertedFileCount;
        public int ReusedFileCount;
        public int FailedFileCount;
        public int TotalGlyphCount;
        public List<JxqySpriteFontConversionFileReport> Files = new();

        public void Add(JxqySpriteFontConversionFileReport file)
        {
            Files.Add(file);
            if (file.Status == JxqySpriteFontConverter.ConvertedStatus)
                ConvertedFileCount++;
            else if (file.Status == JxqySpriteFontConverter.ReusedStatus)
                ReusedFileCount++;
            else
                FailedFileCount++;
            TotalGlyphCount += file.GlyphCount;
        }
    }
}
