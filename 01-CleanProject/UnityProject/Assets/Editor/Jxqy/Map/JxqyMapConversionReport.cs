using System;
using System.Collections.Generic;

namespace Jxqy.Editor.Map
{
    [Serializable]
    public sealed class JxqyMapConversionFileReport
    {
        public string RelativePath = string.Empty;
        public string StableId = string.Empty;
        public string Status = string.Empty;
        public string OutputMetadataAssetPath = string.Empty;
        public string OutputDataAssetPath = string.Empty;
        public string Error = string.Empty;
        public int ColumnCount;
        public int RowCount;
        public int TileCount;
        public int UsedMpcCount;
        public int LoopingMpcCount;
        public int Layer1TileCount;
        public int Layer2TileCount;
        public int Layer3TileCount;
        public int BarrierTileCount;
        public int TrapTileCount;
    }

    [Serializable]
    public sealed class JxqyMapConversionReport
    {
        public string ConverterVersion = string.Empty;
        public string GeneratedUtc = string.Empty;
        public int InputFileCount;
        public int ConvertedFileCount;
        public int ReusedFileCount;
        public int FailedFileCount;
        public long TotalTileCount;
        public long UsedMpcCount;
        public long BarrierTileCount;
        public long TrapTileCount;
        public List<JxqyMapConversionFileReport> Files = new();

        public void Add(JxqyMapConversionFileReport file)
        {
            Files.Add(file);
            if (file.Status == JxqyMapConverter.ConvertedStatus)
                ConvertedFileCount++;
            else if (file.Status == JxqyMapConverter.ReusedStatus)
                ReusedFileCount++;
            else
                FailedFileCount++;
            TotalTileCount += file.TileCount;
            UsedMpcCount += file.UsedMpcCount;
            BarrierTileCount += file.BarrierTileCount;
            TrapTileCount += file.TrapTileCount;
        }
    }
}
