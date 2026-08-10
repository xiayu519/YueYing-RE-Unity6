using System;
using System.IO;
using System.Text;

namespace Jxqy.Editor.Map
{
    public static class JxqyMapParser
    {
        public const int HeaderSize = 192;
        public const int MpcEntryCount = 255;
        public const int MpcEntrySize = 64;
        public const int TileRecordSize = 10;
        public const int TileDataOffset = HeaderSize + MpcEntryCount * MpcEntrySize;

        private const int MpcDirectoryOffset = 32;
        private const int MpcDirectoryCapacity = 32;
        private const int TileDataLengthOffset = 64;
        private const int ColumnCountOffset = 68;
        private const int RowCountOffset = 72;
        private const int TileWidthOffset = 76;
        private const int TileHeightOffset = 80;
        private const int MaximumMapDimension = 8192;
        private static readonly byte[] Signature = Encoding.ASCII.GetBytes("MAP File Ver");
        private static readonly Encoding LegacyEncoding = Encoding.GetEncoding(936);

        public static JxqyMapFile ParseFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("MAP path is empty.", nameof(filePath));
            return Parse(File.ReadAllBytes(filePath));
        }

        public static JxqyMapFile Parse(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length < TileDataOffset)
            {
                throw new JxqyMapFormatException(
                    $"MAP file is {bytes.Length} bytes, smaller than its " +
                    $"{TileDataOffset}-byte header and MPC table.");
            }

            for (int index = 0; index < Signature.Length; index++)
            {
                if (bytes[index] != Signature[index])
                    throw new JxqyMapFormatException("Invalid MAP signature.");
            }

            int columns = ReadInt32(bytes, ColumnCountOffset);
            int rows = ReadInt32(bytes, RowCountOffset);
            ValidateDimension(columns, nameof(columns));
            ValidateDimension(rows, nameof(rows));
            int tileCount;
            int expectedTileBytes;
            int expectedLength;
            try
            {
                tileCount = checked(columns * rows);
                expectedTileBytes = checked(tileCount * TileRecordSize);
                expectedLength = checked(TileDataOffset + expectedTileBytes);
            }
            catch (OverflowException exception)
            {
                throw new JxqyMapFormatException(
                    $"MAP dimensions overflow supported ranges: {exception.Message}");
            }

            int declaredTileBytes = ReadInt32(bytes, TileDataLengthOffset);
            if (declaredTileBytes != expectedTileBytes)
            {
                throw new JxqyMapFormatException(
                    $"MAP declares {declaredTileBytes} tile bytes, expected {expectedTileBytes} " +
                    $"for {columns}x{rows} tiles.");
            }
            if (bytes.Length != expectedLength)
            {
                throw new JxqyMapFormatException(
                    $"MAP length is {bytes.Length}, expected {expectedLength}.");
            }

            var result = new JxqyMapFile
            {
                Version = ReadNullTerminatedAscii(bytes, 0, 32),
                MpcDirectory = ReadNullTerminatedLegacy(
                    bytes,
                    MpcDirectoryOffset,
                    MpcDirectoryCapacity).TrimStart('\\', '/'),
                TileDataLength = declaredTileBytes,
                ColumnCount = columns,
                RowCount = rows,
                TileWidth = ReadInt32(bytes, TileWidthOffset),
                TileHeight = ReadInt32(bytes, TileHeightOffset),
                Header = Copy(bytes, 0, HeaderSize),
                MpcEntries = new JxqyMapMpcEntry[MpcEntryCount],
                Tiles = new JxqyMapTile[tileCount]
            };

            for (int index = 0; index < MpcEntryCount; index++)
            {
                int offset = HeaderSize + index * MpcEntrySize;
                byte[] rawRecord = Copy(bytes, offset, MpcEntrySize);
                result.MpcEntries[index] = new JxqyMapMpcEntry
                {
                    Index = index,
                    FileName = ReadNullTerminatedLegacy(rawRecord, 0, 32),
                    IsLooping = rawRecord[36] == 1,
                    RawRecord = rawRecord
                };
            }

            int tileOffset = TileDataOffset;
            for (int index = 0; index < tileCount; index++)
            {
                result.Tiles[index] = new JxqyMapTile
                {
                    Layer1Frame = bytes[tileOffset],
                    Layer1MpcIndex = bytes[tileOffset + 1],
                    Layer2Frame = bytes[tileOffset + 2],
                    Layer2MpcIndex = bytes[tileOffset + 3],
                    Layer3Frame = bytes[tileOffset + 4],
                    Layer3MpcIndex = bytes[tileOffset + 5],
                    BarrierType = bytes[tileOffset + 6],
                    TrapIndex = bytes[tileOffset + 7],
                    Reserved0 = bytes[tileOffset + 8],
                    Reserved1 = bytes[tileOffset + 9]
                };
                tileOffset += TileRecordSize;
            }
            return result;
        }

        public static byte[] EncodeOriginal(JxqyMapFile map)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));
            if (map.Header == null || map.Header.Length != HeaderSize)
                throw new JxqyMapFormatException("MAP header must contain exactly 192 bytes.");
            if (map.MpcEntries == null || map.MpcEntries.Length != MpcEntryCount)
                throw new JxqyMapFormatException("MAP must contain exactly 255 MPC entries.");

            int expectedTiles = checked(map.ColumnCount * map.RowCount);
            if (map.Tiles == null || map.Tiles.Length != expectedTiles)
            {
                throw new JxqyMapFormatException(
                    $"MAP must contain exactly {expectedTiles} tiles.");
            }

            var bytes = new byte[checked(TileDataOffset + expectedTiles * TileRecordSize)];
            Buffer.BlockCopy(map.Header, 0, bytes, 0, HeaderSize);
            for (int index = 0; index < MpcEntryCount; index++)
            {
                JxqyMapMpcEntry entry = map.MpcEntries[index];
                if (entry?.RawRecord == null || entry.RawRecord.Length != MpcEntrySize)
                {
                    throw new JxqyMapFormatException(
                        $"MAP MPC entry {index} must contain exactly 64 raw bytes.");
                }
                Buffer.BlockCopy(
                    entry.RawRecord,
                    0,
                    bytes,
                    HeaderSize + index * MpcEntrySize,
                    MpcEntrySize);
            }

            int offset = TileDataOffset;
            foreach (JxqyMapTile tile in map.Tiles)
            {
                bytes[offset] = tile.Layer1Frame;
                bytes[offset + 1] = tile.Layer1MpcIndex;
                bytes[offset + 2] = tile.Layer2Frame;
                bytes[offset + 3] = tile.Layer2MpcIndex;
                bytes[offset + 4] = tile.Layer3Frame;
                bytes[offset + 5] = tile.Layer3MpcIndex;
                bytes[offset + 6] = tile.BarrierType;
                bytes[offset + 7] = tile.TrapIndex;
                bytes[offset + 8] = tile.Reserved0;
                bytes[offset + 9] = tile.Reserved1;
                offset += TileRecordSize;
            }
            return bytes;
        }

        private static void ValidateDimension(int value, string name)
        {
            if (value <= 0 || value > MaximumMapDimension)
                throw new JxqyMapFormatException($"Invalid MAP {name}: {value}.");
        }

        private static int ReadInt32(byte[] bytes, int offset)
        {
            return bytes[offset] |
                   bytes[offset + 1] << 8 |
                   bytes[offset + 2] << 16 |
                   bytes[offset + 3] << 24;
        }

        private static string ReadNullTerminatedAscii(
            byte[] bytes,
            int offset,
            int capacity)
        {
            return ReadNullTerminated(bytes, offset, capacity, Encoding.ASCII);
        }

        private static string ReadNullTerminatedLegacy(
            byte[] bytes,
            int offset,
            int capacity)
        {
            return ReadNullTerminated(bytes, offset, capacity, LegacyEncoding);
        }

        private static string ReadNullTerminated(
            byte[] bytes,
            int offset,
            int capacity,
            Encoding encoding)
        {
            int end = offset;
            int limit = checked(offset + capacity);
            while (end < limit && bytes[end] != 0)
                end++;
            return encoding.GetString(bytes, offset, end - offset);
        }

        private static byte[] Copy(byte[] source, int offset, int count)
        {
            var result = new byte[count];
            Buffer.BlockCopy(source, offset, result, 0, count);
            return result;
        }
    }
}
