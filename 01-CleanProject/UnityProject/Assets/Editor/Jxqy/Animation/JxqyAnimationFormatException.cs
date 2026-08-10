using System;
using System.IO;

namespace Jxqy.Editor.Animation
{
    public sealed class JxqyAnimationFormatException : IOException
    {
        public JxqyAnimationFormatException(string message, int offset = -1)
            : base(offset < 0 ? message : $"{message} (offset 0x{offset:X})")
        {
            Offset = offset;
        }

        public int Offset { get; }
    }
}
