using System;
using System.Text;
using Jxqy.Domain.Content;

namespace Jxqy.Domain.World
{
    public sealed class JxqyRuntimeMapData
    {
        private const int HeaderSize = 192;
        private const int MpcTableBytes = 255 * 64;
        private const int TileRecordSize = 10;
        private const int TileDataOffset = HeaderSize + MpcTableBytes;
        private static readonly byte[] Signature =
            Encoding.ASCII.GetBytes("MAP File Ver");

        private JxqyRuntimeMapData(
            int columns,
            int rows,
            JxqyRuntimeMapTile[] tiles)
        {
            Columns = columns;
            Rows = rows;
            Tiles = tiles;
        }

        public int Columns { get; }
        public int Rows { get; }
        public JxqyRuntimeMapTile[] Tiles { get; }

        public static JxqyRuntimeMapData Parse(
            byte[] bytes,
            JxqyMapMetadata metadata)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));
            if (metadata == null)
                throw new ArgumentNullException(nameof(metadata));
            int expected = checked(
                TileDataOffset +
                metadata.ColumnCount * metadata.RowCount *
                TileRecordSize);
            if (bytes.Length != expected)
                throw new FormatException(
                    $"Map data has {bytes.Length} bytes, expected {expected}.");
            for (int index = 0; index < Signature.Length; index++)
            {
                if (bytes[index] != Signature[index])
                    throw new FormatException("Invalid MAP signature.");
            }
            int columns = ReadInt32(bytes, 68);
            int rows = ReadInt32(bytes, 72);
            if (columns != metadata.ColumnCount ||
                rows != metadata.RowCount)
                throw new FormatException(
                    "MAP dimensions differ from converted metadata.");
            var tiles = new JxqyRuntimeMapTile[checked(columns * rows)];
            int offset = TileDataOffset;
            for (int index = 0; index < tiles.Length; index++)
            {
                tiles[index] = new JxqyRuntimeMapTile(
                    bytes[offset],
                    bytes[offset + 1],
                    bytes[offset + 2],
                    bytes[offset + 3],
                    bytes[offset + 4],
                    bytes[offset + 5],
                    bytes[offset + 6],
                    bytes[offset + 7]);
                offset += TileRecordSize;
            }
            return new JxqyRuntimeMapData(columns, rows, tiles);
        }

        public JxqyRuntimeMapTile GetTile(int column, int row)
        {
            if (column < 0 || column >= Columns ||
                row < 0 || row >= Rows)
                throw new ArgumentOutOfRangeException();
            return Tiles[column + row * Columns];
        }

        public bool IsObstacle(int column, int row)
        {
            return !IsInnerTile(column, row) ||
                   (GetTile(column, row).BarrierType & 0x80) != 0;
        }

        public bool IsObstacleForCharacter(int column, int row)
        {
            return !IsInnerTile(column, row) ||
                   (GetTile(column, row).BarrierType & 0xC0) != 0;
        }

        public bool IsObstacleForCharacterJump(int column, int row)
        {
            if (!IsInnerTile(column, row))
                return true;
            byte type = GetTile(column, row).BarrierType;
            return type != 0 && (type & 0x20) == 0;
        }

        public bool IsObstacleForMagic(int column, int row)
        {
            if (!IsInnerTile(column, row))
                return true;
            byte type = GetTile(column, row).BarrierType;
            return type != 0 && (type & 0x40) == 0;
        }

        public int GetTrapIndex(int column, int row)
        {
            return IsInnerTile(column, row)
                ? GetTile(column, row).TrapIndex
                : 0;
        }

        private bool IsInnerTile(int column, int row)
        {
            return column >= 0 && column < Columns &&
                   row > 0 && row < Rows - 1;
        }

        private static int ReadInt32(byte[] bytes, int offset)
        {
            return bytes[offset] |
                   bytes[offset + 1] << 8 |
                   bytes[offset + 2] << 16 |
                   bytes[offset + 3] << 24;
        }
    }

    public readonly struct JxqyRuntimeMapTile
    {
        public JxqyRuntimeMapTile(
            byte layer1Frame,
            byte layer1Mpc,
            byte layer2Frame,
            byte layer2Mpc,
            byte layer3Frame,
            byte layer3Mpc,
            byte barrierType,
            byte trapIndex)
        {
            Layer1Frame = layer1Frame;
            Layer1Mpc = layer1Mpc;
            Layer2Frame = layer2Frame;
            Layer2Mpc = layer2Mpc;
            Layer3Frame = layer3Frame;
            Layer3Mpc = layer3Mpc;
            BarrierType = barrierType;
            TrapIndex = trapIndex;
        }

        public byte Layer1Frame { get; }
        public byte Layer1Mpc { get; }
        public byte Layer2Frame { get; }
        public byte Layer2Mpc { get; }
        public byte Layer3Frame { get; }
        public byte Layer3Mpc { get; }
        public byte BarrierType { get; }
        public byte TrapIndex { get; }

        public byte GetFrame(int layer)
        {
            return layer switch
            {
                0 => Layer1Frame,
                1 => Layer2Frame,
                2 => Layer3Frame,
                _ => throw new ArgumentOutOfRangeException(nameof(layer))
            };
        }

        public byte GetMpc(int layer)
        {
            return layer switch
            {
                0 => Layer1Mpc,
                1 => Layer2Mpc,
                2 => Layer3Mpc,
                _ => throw new ArgumentOutOfRangeException(nameof(layer))
            };
        }
    }
}
