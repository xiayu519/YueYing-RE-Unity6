using System;
using System.Text;

namespace Jxqy.Editor.Animation
{
    internal sealed class JxqyBinaryReader
    {
        private readonly byte[] _data;

        public JxqyBinaryReader(byte[] data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public int Length => _data.Length;

        public byte ReadByte(ref int offset)
        {
            EnsureAvailable(offset, 1);
            return _data[offset++];
        }

        public int ReadInt32(ref int offset)
        {
            EnsureAvailable(offset, 4);
            int value = _data[offset] |
                        (_data[offset + 1] << 8) |
                        (_data[offset + 2] << 16) |
                        (_data[offset + 3] << 24);
            offset += 4;
            return value;
        }

        public string ReadAscii(int offset, int length)
        {
            EnsureAvailable(offset, length);
            return Encoding.ASCII.GetString(_data, offset, length);
        }

        public void EnsureAvailable(int offset, int count)
        {
            if (offset < 0 || count < 0 || (long)offset + count > _data.Length)
            {
                throw new JxqyAnimationFormatException(
                    $"Requested {count} byte(s), but the file length is {_data.Length}.",
                    offset);
            }
        }
    }
}
