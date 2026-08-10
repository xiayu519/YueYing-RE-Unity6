using System;
using System.Text;

namespace Jxqy.Editor.Text
{
    public static class JxqyLegacyTextDecoder
    {
        private static readonly Encoding StrictGb936 = Encoding.GetEncoding(
            936,
            EncoderFallback.ExceptionFallback,
            DecoderFallback.ExceptionFallback);
        private static readonly UTF8Encoding StrictUtf8 = new(false, true);
        private static readonly UnicodeEncoding StrictUtf16Le = new(false, true, true);
        private static readonly UnicodeEncoding StrictUtf16Be = new(true, true, true);

        public static string Decode(byte[] bytes, out string encodingName)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            if (HasPrefix(bytes, 0xEF, 0xBB, 0xBF))
            {
                encodingName = "UTF-8 BOM";
                return StrictUtf8.GetString(bytes, 3, bytes.Length - 3);
            }
            if (HasPrefix(bytes, 0xFF, 0xFE))
            {
                encodingName = "UTF-16 LE";
                return StrictUtf16Le.GetString(bytes, 2, bytes.Length - 2);
            }
            if (HasPrefix(bytes, 0xFE, 0xFF))
            {
                encodingName = "UTF-16 BE";
                return StrictUtf16Be.GetString(bytes, 2, bytes.Length - 2);
            }

            encodingName = "GB936";
            return StrictGb936.GetString(bytes);
        }

        private static bool HasPrefix(byte[] bytes, params byte[] prefix)
        {
            if (bytes.Length < prefix.Length)
                return false;
            for (int index = 0; index < prefix.Length; index++)
            {
                if (bytes[index] != prefix[index])
                    return false;
            }
            return true;
        }
    }
}
