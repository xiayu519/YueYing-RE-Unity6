using System.IO;

namespace Jxqy.Editor.Map
{
    public sealed class JxqyMapFormatException : IOException
    {
        public JxqyMapFormatException(string message)
            : base(message)
        {
        }
    }
}
