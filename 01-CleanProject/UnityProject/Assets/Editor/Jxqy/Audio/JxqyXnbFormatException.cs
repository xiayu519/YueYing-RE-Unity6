using System.IO;

namespace Jxqy.Editor.Audio
{
    public sealed class JxqyXnbFormatException : IOException
    {
        public JxqyXnbFormatException(string message)
            : base(message)
        {
        }
    }
}
