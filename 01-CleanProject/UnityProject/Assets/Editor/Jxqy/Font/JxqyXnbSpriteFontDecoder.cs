using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Jxqy.Editor.Font
{
    public static class JxqyXnbSpriteFontDecoder
    {
        private const int HeaderSize = 10;
        private const int Dxt3SurfaceFormat = 5;
        private const int MaximumTypeReaders = 64;
        private const int MaximumCollectionCount = 65536;
        private static readonly UTF8Encoding StrictUtf8 = new(false, true);

        public static JxqyDecodedSpriteFont DecodeFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("XNB path is empty.", nameof(filePath));
            return Decode(File.ReadAllBytes(filePath));
        }

        public static JxqyDecodedSpriteFont Decode(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));
            var reader = new Reader(bytes);
            if (bytes.Length < HeaderSize || reader.ReadAscii(0, 3) != "XNB")
                throw new JxqySpriteFontFormatException("Invalid XNB header.");

            var decoded = new JxqyDecodedSpriteFont
            {
                Platform = (char)bytes[3],
                XnbVersion = bytes[4],
                Flags = bytes[5]
            };
            if (decoded.Platform != 'w' && decoded.Platform != 'x' &&
                decoded.Platform != 'm' && decoded.Platform != 'a')
                throw new JxqySpriteFontFormatException(
                    $"Unsupported XNB platform '{decoded.Platform}'.");
            if (decoded.XnbVersion < 4 || decoded.XnbVersion > 5)
                throw new JxqySpriteFontFormatException(
                    $"Unsupported XNB version {decoded.XnbVersion}.");
            if ((decoded.Flags & 0xC0) != 0)
                throw new JxqySpriteFontFormatException(
                    "Compressed SpriteFont XNB is not supported.");
            if (reader.ReadInt32At(6) != bytes.Length)
                throw new JxqySpriteFontFormatException(
                    "XNB declared size does not match its file size.");

            int offset = HeaderSize;
            int readerCount = reader.Read7BitEncodedInt(ref offset);
            if (readerCount <= 0 || readerCount > MaximumTypeReaders)
                throw new JxqySpriteFontFormatException(
                    $"Invalid type reader count {readerCount}.");
            for (int index = 0; index < readerCount; index++)
            {
                decoded.TypeReaders.Add(reader.ReadString(ref offset));
                reader.ReadInt32(ref offset);
            }

            int sharedResourceCount = reader.Read7BitEncodedInt(ref offset);
            if (sharedResourceCount != 0)
                throw new JxqySpriteFontFormatException(
                    $"SpriteFont declares {sharedResourceCount} shared resources.");
            RequireType(reader, decoded, ref offset, ".SpriteFontReader");
            RequireType(reader, decoded, ref offset, ".Texture2DReader");

            decoded.SurfaceFormat = reader.ReadInt32(ref offset);
            decoded.TextureWidth = reader.ReadInt32(ref offset);
            decoded.TextureHeight = reader.ReadInt32(ref offset);
            int mipCount = reader.ReadInt32(ref offset);
            if (decoded.SurfaceFormat != Dxt3SurfaceFormat)
                throw new JxqySpriteFontFormatException(
                    $"Only DXT3 SpriteFont textures are supported, format={decoded.SurfaceFormat}.");
            if (decoded.TextureWidth <= 0 || decoded.TextureHeight <= 0 ||
                decoded.TextureWidth > 4096 || decoded.TextureHeight > 4096)
                throw new JxqySpriteFontFormatException(
                    $"Invalid font texture size {decoded.TextureWidth}x{decoded.TextureHeight}.");
            if (mipCount != 1)
                throw new JxqySpriteFontFormatException(
                    $"Expected one SpriteFont mip level, found {mipCount}.");
            int textureByteCount = reader.ReadInt32(ref offset);
            byte[] textureBytes = reader.ReadBytes(ref offset, textureByteCount);
            decoded.TextureRgba = DecodeDxt3(
                textureBytes,
                decoded.TextureWidth,
                decoded.TextureHeight);

            List<JxqySpriteFontRect> glyphs = ReadRectangles(
                reader, decoded, ref offset);
            List<JxqySpriteFontRect> cropping = ReadRectangles(
                reader, decoded, ref offset);
            List<int> characters = ReadCharacters(reader, decoded, ref offset);

            decoded.LineSpacing = reader.ReadInt32(ref offset);
            decoded.Spacing = reader.ReadSingle(ref offset);
            List<Float3> kernings = ReadKernings(reader, decoded, ref offset);
            bool hasDefaultCharacter = reader.ReadBoolean(ref offset);
            if (hasDefaultCharacter)
                decoded.DefaultCharacter = reader.ReadCharacter(ref offset);

            if (glyphs.Count != cropping.Count ||
                glyphs.Count != characters.Count ||
                glyphs.Count != kernings.Count)
                throw new JxqySpriteFontFormatException(
                    "SpriteFont glyph collection lengths do not match.");
            for (int index = 0; index < glyphs.Count; index++)
            {
                JxqySpriteFontRect glyph = glyphs[index];
                if (glyph.X < 0 || glyph.Y < 0 ||
                    glyph.Width < 0 || glyph.Height < 0 ||
                    (long)glyph.X + glyph.Width > decoded.TextureWidth ||
                    (long)glyph.Y + glyph.Height > decoded.TextureHeight)
                    throw new JxqySpriteFontFormatException(
                        $"Glyph {index} lies outside the font texture.");
                Float3 kerning = kernings[index];
                decoded.Glyphs.Add(new JxqySpriteFontGlyph
                {
                    Character = characters[index],
                    Glyph = glyph,
                    Cropping = cropping[index],
                    KerningLeft = kerning.X,
                    KerningWidth = kerning.Y,
                    KerningRight = kerning.Z
                });
            }
            if (offset != bytes.Length)
                throw new JxqySpriteFontFormatException(
                    $"SpriteFont XNB has {bytes.Length - offset} trailing byte(s).");
            return decoded;
        }

        public static byte[] DecodeDxt3(byte[] bytes, int width, int height)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(width), "Texture dimensions must be positive.");
            int blockColumns = (width + 3) / 4;
            int blockRows = (height + 3) / 4;
            int expected = checked(blockColumns * blockRows * 16);
            if (bytes.Length != expected)
                throw new JxqySpriteFontFormatException(
                    $"DXT3 payload is {bytes.Length} bytes, expected {expected}.");

            var rgba = new byte[checked(width * height * 4)];
            int source = 0;
            for (int blockY = 0; blockY < blockRows; blockY++)
            {
                for (int blockX = 0; blockX < blockColumns; blockX++)
                {
                    ulong alphaBits = ReadUInt64(bytes, source);
                    ushort color0 = ReadUInt16(bytes, source + 8);
                    ushort color1 = ReadUInt16(bytes, source + 10);
                    uint colorBits = ReadUInt32(bytes, source + 12);
                    source += 16;
                    byte[,] colors = CreateDxtColors(color0, color1);
                    for (int pixel = 0; pixel < 16; pixel++)
                    {
                        int x = blockX * 4 + pixel % 4;
                        int y = blockY * 4 + pixel / 4;
                        if (x >= width || y >= height)
                            continue;
                        int colorIndex = (int)((colorBits >> (pixel * 2)) & 3);
                        int target = (y * width + x) * 4;
                        rgba[target] = colors[colorIndex, 0];
                        rgba[target + 1] = colors[colorIndex, 1];
                        rgba[target + 2] = colors[colorIndex, 2];
                        rgba[target + 3] =
                            (byte)(((alphaBits >> (pixel * 4)) & 15) * 17);
                    }
                }
            }
            return rgba;
        }

        private static List<JxqySpriteFontRect> ReadRectangles(
            Reader reader,
            JxqyDecodedSpriteFont decoded,
            ref int offset)
        {
            RequireType(reader, decoded, ref offset, ".ListReader`1");
            int count = ReadCollectionCount(reader, ref offset);
            var result = new List<JxqySpriteFontRect>(count);
            for (int index = 0; index < count; index++)
            {
                result.Add(new JxqySpriteFontRect
                {
                    X = reader.ReadInt32(ref offset),
                    Y = reader.ReadInt32(ref offset),
                    Width = reader.ReadInt32(ref offset),
                    Height = reader.ReadInt32(ref offset)
                });
            }
            return result;
        }

        private static List<int> ReadCharacters(
            Reader reader,
            JxqyDecodedSpriteFont decoded,
            ref int offset)
        {
            RequireType(reader, decoded, ref offset, ".ListReader`1");
            int count = ReadCollectionCount(reader, ref offset);
            var result = new List<int>(count);
            for (int index = 0; index < count; index++)
                result.Add(reader.ReadCharacter(ref offset));
            return result;
        }

        private static List<Float3> ReadKernings(
            Reader reader,
            JxqyDecodedSpriteFont decoded,
            ref int offset)
        {
            RequireType(reader, decoded, ref offset, ".ListReader`1");
            int count = ReadCollectionCount(reader, ref offset);
            var result = new List<Float3>(count);
            for (int index = 0; index < count; index++)
            {
                result.Add(new Float3
                {
                    X = reader.ReadSingle(ref offset),
                    Y = reader.ReadSingle(ref offset),
                    Z = reader.ReadSingle(ref offset)
                });
            }
            return result;
        }

        private static int ReadCollectionCount(Reader reader, ref int offset)
        {
            int count = reader.ReadInt32(ref offset);
            if (count < 0 || count > MaximumCollectionCount)
                throw new JxqySpriteFontFormatException(
                    $"Invalid SpriteFont collection count {count}.");
            return count;
        }

        private static void RequireType(
            Reader reader,
            JxqyDecodedSpriteFont decoded,
            ref int offset,
            string suffix)
        {
            int typeIndex = reader.Read7BitEncodedInt(ref offset);
            if (typeIndex <= 0 || typeIndex > decoded.TypeReaders.Count)
                throw new JxqySpriteFontFormatException(
                    $"Invalid type reader index {typeIndex}.");
            string typeReader = decoded.TypeReaders[typeIndex - 1];
            if (typeReader.IndexOf(suffix, StringComparison.Ordinal) < 0)
                throw new JxqySpriteFontFormatException(
                    $"Expected {suffix}, found {typeReader}.");
        }

        private static byte[,] CreateDxtColors(ushort packed0, ushort packed1)
        {
            var result = new byte[4, 3];
            DecodeRgb565(packed0, result, 0);
            DecodeRgb565(packed1, result, 1);
            for (int channel = 0; channel < 3; channel++)
            {
                result[2, channel] =
                    (byte)((2 * result[0, channel] + result[1, channel]) / 3);
                result[3, channel] =
                    (byte)((result[0, channel] + 2 * result[1, channel]) / 3);
            }
            return result;
        }

        private static void DecodeRgb565(ushort packed, byte[,] target, int row)
        {
            int r = (packed >> 11) & 31;
            int g = (packed >> 5) & 63;
            int b = packed & 31;
            target[row, 0] = (byte)((r << 3) | (r >> 2));
            target[row, 1] = (byte)((g << 2) | (g >> 4));
            target[row, 2] = (byte)((b << 3) | (b >> 2));
        }

        private static ushort ReadUInt16(byte[] bytes, int offset)
        {
            return (ushort)(bytes[offset] | bytes[offset + 1] << 8);
        }

        private static uint ReadUInt32(byte[] bytes, int offset)
        {
            return (uint)(bytes[offset] |
                          bytes[offset + 1] << 8 |
                          bytes[offset + 2] << 16 |
                          bytes[offset + 3] << 24);
        }

        private static ulong ReadUInt64(byte[] bytes, int offset)
        {
            return ReadUInt32(bytes, offset) |
                   (ulong)ReadUInt32(bytes, offset + 4) << 32;
        }

        private struct Float3
        {
            public float X;
            public float Y;
            public float Z;
        }

        private sealed class Reader
        {
            private readonly byte[] _bytes;

            public Reader(byte[] bytes)
            {
                _bytes = bytes;
            }

            public int ReadInt32At(int offset)
            {
                EnsureAvailable(offset, 4);
                return _bytes[offset] |
                       _bytes[offset + 1] << 8 |
                       _bytes[offset + 2] << 16 |
                       _bytes[offset + 3] << 24;
            }

            public int ReadInt32(ref int offset)
            {
                int value = ReadInt32At(offset);
                offset += 4;
                return value;
            }

            public float ReadSingle(ref int offset)
            {
                EnsureAvailable(offset, 4);
                float value = BitConverter.ToSingle(_bytes, offset);
                offset += 4;
                if (float.IsNaN(value) || float.IsInfinity(value))
                    throw new JxqySpriteFontFormatException(
                        "SpriteFont contains a non-finite float.");
                return value;
            }

            public bool ReadBoolean(ref int offset)
            {
                EnsureAvailable(offset, 1);
                byte value = _bytes[offset++];
                if (value > 1)
                    throw new JxqySpriteFontFormatException(
                        $"Invalid Boolean value {value}.");
                return value != 0;
            }

            public int ReadCharacter(ref int offset)
            {
                EnsureAvailable(offset, 1);
                byte first = _bytes[offset];
                int length;
                if ((first & 0x80) == 0)
                    length = 1;
                else if ((first & 0xE0) == 0xC0)
                    length = 2;
                else if ((first & 0xF0) == 0xE0)
                    length = 3;
                else
                    throw new JxqySpriteFontFormatException(
                        "SpriteFont character is not a BMP UTF-8 character.");
                EnsureAvailable(offset, length);
                string value;
                try
                {
                    value = StrictUtf8.GetString(_bytes, offset, length);
                }
                catch (DecoderFallbackException exception)
                {
                    throw new JxqySpriteFontFormatException(
                        $"Invalid UTF-8 character: {exception.Message}");
                }
                if (value.Length != 1)
                    throw new JxqySpriteFontFormatException(
                        "SpriteFont character does not decode to one UTF-16 code unit.");
                offset += length;
                return value[0];
            }

            public int Read7BitEncodedInt(ref int offset)
            {
                int value = 0;
                int shift = 0;
                for (int index = 0; index < 5; index++)
                {
                    EnsureAvailable(offset, 1);
                    byte current = _bytes[offset++];
                    value |= (current & 0x7F) << shift;
                    if ((current & 0x80) == 0)
                        return value;
                    shift += 7;
                }
                throw new JxqySpriteFontFormatException(
                    "Invalid 7-bit encoded integer.");
            }

            public string ReadString(ref int offset)
            {
                int length = Read7BitEncodedInt(ref offset);
                if (length < 0 || length > 16384)
                    throw new JxqySpriteFontFormatException(
                        $"Invalid string byte length {length}.");
                EnsureAvailable(offset, length);
                string result = StrictUtf8.GetString(_bytes, offset, length);
                offset += length;
                return result;
            }

            public byte[] ReadBytes(ref int offset, int count)
            {
                EnsureAvailable(offset, count);
                var result = new byte[count];
                Buffer.BlockCopy(_bytes, offset, result, 0, count);
                offset += count;
                return result;
            }

            public string ReadAscii(int offset, int count)
            {
                EnsureAvailable(offset, count);
                return Encoding.ASCII.GetString(_bytes, offset, count);
            }

            private void EnsureAvailable(int offset, int count)
            {
                if (offset < 0 || count < 0 ||
                    (long)offset + count > _bytes.Length)
                    throw new JxqySpriteFontFormatException(
                        $"Read outside XNB bounds at {offset} for {count} byte(s).");
            }
        }
    }
}
