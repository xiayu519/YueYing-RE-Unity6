using System;
using System.IO;
using System.Text;

namespace Jxqy.Editor.Scanning
{
    public static class JxqyPathUtility
    {
        public static string NormalizeRelativePath(string rootPath, string fullPath)
        {
            string root = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string candidate = Path.GetFullPath(fullPath);
            string prefix = root + Path.DirectorySeparatorChar;

            if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"Path '{candidate}' is outside source root '{root}'.",
                    nameof(fullPath));
            }

            string relative = candidate.Substring(prefix.Length)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/')
                .Normalize(NormalizationForm.FormC);

            if (relative.Length == 0 || relative.StartsWith("/", StringComparison.Ordinal))
                throw new ArgumentException($"Invalid relative path: {relative}", nameof(fullPath));

            return relative;
        }

        public static string CreateStableId(JxqyFileKind kind, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new ArgumentException("Relative path is empty.", nameof(relativePath));

            string normalized = relativePath
                .Replace('\\', '/')
                .TrimStart('/')
                .Normalize(NormalizationForm.FormC)
                .ToLowerInvariant();

            return $"{kind.ToString().ToLowerInvariant()}:{normalized}";
        }

        public static string CreateSourceAddress(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new ArgumentException("Relative path is empty.", nameof(relativePath));

            return "jxqy/source/" + relativePath
                .Replace('\\', '/')
                .TrimStart('/')
                .Normalize(NormalizationForm.FormC)
                .ToLowerInvariant();
        }

        public static string FindPortabilityIssue(string relativePath)
        {
            string[] segments = relativePath.Replace('\\', '/').Split('/');
            foreach (string segment in segments)
            {
                if (segment.Length == 0)
                    return "empty path segment";
                if (segment.EndsWith(" ", StringComparison.Ordinal) ||
                    segment.EndsWith(".", StringComparison.Ordinal))
                    return "segment ends with a space or period";

                foreach (char character in segment)
                {
                    if (character < 32)
                        return "control character in path";
                }

                string stem = Path.GetFileNameWithoutExtension(segment);
                if (IsReservedWindowsName(stem))
                    return $"reserved device name '{stem}'";
            }

            return string.Empty;
        }

        private static bool IsReservedWindowsName(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            string upper = value.ToUpperInvariant();
            if (upper is "CON" or "PRN" or "AUX" or "NUL")
                return true;

            if (upper.Length == 4 &&
                (upper.StartsWith("COM", StringComparison.Ordinal) ||
                 upper.StartsWith("LPT", StringComparison.Ordinal)) &&
                upper[3] >= '1' && upper[3] <= '9')
            {
                return true;
            }

            return false;
        }
    }
}
