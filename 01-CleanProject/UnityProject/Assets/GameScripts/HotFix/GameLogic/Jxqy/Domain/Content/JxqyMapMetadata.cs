using System;
using System.Collections.Generic;

namespace Jxqy.Domain.Content
{
    [Serializable]
    public sealed class JxqyMapMetadata
    {
        public int SchemaVersion = 1;
        public string ConverterVersion = string.Empty;
        public string SourceStableId = string.Empty;
        public string SourceRelativePath = string.Empty;
        public string SourceAddress = string.Empty;
        public string SourceSha256 = string.Empty;
        public string MpcDirectory = string.Empty;
        public int ColumnCount;
        public int RowCount;
        public int TileWidth;
        public int TileHeight;
        public int MapPixelWidth;
        public int MapPixelHeight;
        public string DataAddress = string.Empty;
        public List<JxqyMapMpcMetadata> MpcTable = new();
    }

    [Serializable]
    public sealed class JxqyMapMpcMetadata
    {
        public int Index;
        public string FileName = string.Empty;
        public bool IsLooping;
        public string RawRecordBase64 = string.Empty;
    }
}
