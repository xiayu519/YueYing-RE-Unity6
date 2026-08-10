using System;
using System.IO;
using System.Text;

namespace Jxqy.Editor.Audio
{
    public static class JxqyXnbSoundEffectDecoder
    {
        private const int HeaderSize = 10;
        private const int MaximumTypeReaders = 64;
        private const int MaximumStringBytes = 16 * 1024;
        private static readonly UTF8Encoding StrictUtf8 = new(false, true);

        public static JxqyDecodedSoundEffect DecodeFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("XNB path is empty.", nameof(filePath));
            return Decode(File.ReadAllBytes(filePath));
        }

        public static JxqyDecodedSoundEffect Decode(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));
            var reader = new Reader(bytes);
            if (bytes.Length < HeaderSize)
                throw new JxqyXnbFormatException("XNB is smaller than its 10-byte header.");
            if (reader.ReadAscii(0, 3) != "XNB")
                throw new JxqyXnbFormatException("Invalid XNB signature.");

            char platform = (char)bytes[3];
            if (platform != 'w' && platform != 'x' &&
                platform != 'm' && platform != 'a')
            {
                throw new JxqyXnbFormatException(
                    $"Unsupported XNB platform '{platform}'.");
            }
            byte version = bytes[4];
            if (version < 4 || version > 5)
                throw new JxqyXnbFormatException($"Unsupported XNB version {version}.");
            byte flags = bytes[5];
            if ((flags & 0xC0) != 0)
                throw new JxqyXnbFormatException("Compressed XNB is not supported.");
            int declaredLength = reader.ReadInt32At(6);
            if (declaredLength != bytes.Length)
            {
                throw new JxqyXnbFormatException(
                    $"XNB declares {declaredLength} bytes but contains {bytes.Length}.");
            }

            int offset = HeaderSize;
            int typeReaderCount = reader.Read7BitEncodedInt(ref offset);
            if (typeReaderCount <= 0 || typeReaderCount > MaximumTypeReaders)
                throw new JxqyXnbFormatException(
                    $"Invalid XNB type reader count {typeReaderCount}.");
            var decoded = new JxqyDecodedSoundEffect
            {
                Platform = platform,
                XnbVersion = version,
                Flags = flags
            };
            for (int index = 0; index < typeReaderCount; index++)
            {
                decoded.TypeReaders.Add(reader.ReadString(ref offset));
                reader.ReadInt32(ref offset);
            }

            int sharedResourceCount = reader.Read7BitEncodedInt(ref offset);
            if (sharedResourceCount != 0)
                throw new JxqyXnbFormatException(
                    $"SoundEffect XNB declares {sharedResourceCount} shared resources.");
            int primaryTypeReader = reader.Read7BitEncodedInt(ref offset);
            if (primaryTypeReader <= 0 || primaryTypeReader > decoded.TypeReaders.Count)
                throw new JxqyXnbFormatException(
                    $"Invalid primary type reader index {primaryTypeReader}.");
            string primaryReader = decoded.TypeReaders[primaryTypeReader - 1];
            if (!primaryReader.EndsWith(
                    ".SoundEffectReader",
                    StringComparison.Ordinal))
            {
                throw new JxqyXnbFormatException(
                    $"XNB primary reader is not SoundEffectReader: {primaryReader}");
            }

            int formatSize = reader.ReadInt32(ref offset);
            if (formatSize < 16 || formatSize > 256)
                throw new JxqyXnbFormatException(
                    $"Invalid WAVEFORMATEX size {formatSize}.");
            decoded.WaveFormat = reader.ReadBytes(ref offset, formatSize);
            decoded.FormatTag = ReadUInt16(decoded.WaveFormat, 0);
            decoded.Channels = ReadUInt16(decoded.WaveFormat, 2);
            decoded.SampleRate = ReadInt32(decoded.WaveFormat, 4);
            decoded.AverageBytesPerSecond = ReadInt32(decoded.WaveFormat, 8);
            decoded.BlockAlign = ReadUInt16(decoded.WaveFormat, 12);
            decoded.BitsPerSample = ReadUInt16(decoded.WaveFormat, 14);
            ValidateWaveFormat(decoded);

            int pcmByteCount = reader.ReadInt32(ref offset);
            if (pcmByteCount < 0)
                throw new JxqyXnbFormatException(
                    $"Negative PCM byte count {pcmByteCount}.");
            decoded.PcmData = reader.ReadBytes(ref offset, pcmByteCount);
            decoded.LoopStart = reader.ReadInt32(ref offset);
            decoded.LoopLength = reader.ReadInt32(ref offset);
            decoded.DurationMilliseconds = reader.ReadInt32(ref offset);
            if (offset != bytes.Length)
            {
                throw new JxqyXnbFormatException(
                    $"SoundEffect XNB has {bytes.Length - offset} trailing byte(s).");
            }
            if (decoded.LoopStart < 0 || decoded.LoopLength < 0 ||
                decoded.DurationMilliseconds < 0)
            {
                throw new JxqyXnbFormatException(
                    "SoundEffect loop or duration metadata is negative.");
            }
            return decoded;
        }

        private static void ValidateWaveFormat(JxqyDecodedSoundEffect sound)
        {
            if (sound.FormatTag != 1)
                throw new JxqyXnbFormatException(
                    $"Only PCM SoundEffect XNB is supported, format tag={sound.FormatTag}.");
            if (sound.Channels <= 0 || sound.Channels > 8)
                throw new JxqyXnbFormatException(
                    $"Invalid channel count {sound.Channels}.");
            if (sound.SampleRate <= 0 || sound.SampleRate > 384000)
                throw new JxqyXnbFormatException(
                    $"Invalid sample rate {sound.SampleRate}.");
            if (sound.BitsPerSample != 8 &&
                sound.BitsPerSample != 16 &&
                sound.BitsPerSample != 24 &&
                sound.BitsPerSample != 32)
            {
                throw new JxqyXnbFormatException(
                    $"Unsupported PCM bit depth {sound.BitsPerSample}.");
            }
            int expectedBlockAlign =
                sound.Channels * sound.BitsPerSample / 8;
            if (sound.BlockAlign != expectedBlockAlign)
                throw new JxqyXnbFormatException(
                    $"PCM block align is {sound.BlockAlign}, expected {expectedBlockAlign}.");
            if (sound.AverageBytesPerSecond !=
                checked(sound.SampleRate * sound.BlockAlign))
            {
                throw new JxqyXnbFormatException(
                    "PCM average byte rate does not match sample rate and block align.");
            }
        }

        private static ushort ReadUInt16(byte[] bytes, int offset)
        {
            return (ushort)(bytes[offset] | bytes[offset + 1] << 8);
        }

        private static int ReadInt32(byte[] bytes, int offset)
        {
            return bytes[offset] |
                   bytes[offset + 1] << 8 |
                   bytes[offset + 2] << 16 |
                   bytes[offset + 3] << 24;
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
                return JxqyXnbSoundEffectDecoder.ReadInt32(_bytes, offset);
            }

            public int ReadInt32(ref int offset)
            {
                int value = ReadInt32At(offset);
                offset += 4;
                return value;
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
                throw new JxqyXnbFormatException("Invalid 7-bit encoded integer.");
            }

            public string ReadString(ref int offset)
            {
                int length = Read7BitEncodedInt(ref offset);
                if (length < 0 || length > MaximumStringBytes)
                    throw new JxqyXnbFormatException(
                        $"Invalid XNB string byte length {length}.");
                EnsureAvailable(offset, length);
                string value = StrictUtf8.GetString(_bytes, offset, length);
                offset += length;
                return value;
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
                {
                    throw new JxqyXnbFormatException(
                        $"Read outside XNB bounds at {offset} for {count} byte(s).");
                }
            }
        }
    }
}
