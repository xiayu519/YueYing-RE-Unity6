using System;

namespace Jxqy.Editor.Map
{
    public sealed class JxqyMapMpcEntry
    {
        public int Index;
        public string FileName = string.Empty;
        public bool IsLooping;
        public byte[] RawRecord = Array.Empty<byte>();
    }

    public struct JxqyMapTile
    {
        public byte Layer1Frame;
        public byte Layer1MpcIndex;
        public byte Layer2Frame;
        public byte Layer2MpcIndex;
        public byte Layer3Frame;
        public byte Layer3MpcIndex;
        public byte BarrierType;
        public byte TrapIndex;
        public byte Reserved0;
        public byte Reserved1;
    }

    public sealed class JxqyMapFile
    {
        public string Version = string.Empty;
        public string MpcDirectory = string.Empty;
        public int TileDataLength;
        public int ColumnCount;
        public int RowCount;
        public int TileWidth;
        public int TileHeight;
        public byte[] Header = Array.Empty<byte>();
        public JxqyMapMpcEntry[] MpcEntries = Array.Empty<JxqyMapMpcEntry>();
        public JxqyMapTile[] Tiles = Array.Empty<JxqyMapTile>();
    }
}
