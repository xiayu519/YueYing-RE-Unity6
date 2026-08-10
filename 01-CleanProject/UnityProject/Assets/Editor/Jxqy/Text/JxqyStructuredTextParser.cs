using System;
using Jxqy.Domain.Content;

namespace Jxqy.Editor.Text
{
    public static class JxqyStructuredTextParser
    {
        public static void Populate(string text, JxqyTextAssetMetadata metadata)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));
            if (metadata == null)
                throw new ArgumentNullException(nameof(metadata));

            string[] lines = SplitLines(text);
            metadata.LineCount = lines.Length;
            JxqyTextSectionMetadata currentSection = null;
            for (int index = 0; index < lines.Length; index++)
            {
                string line = lines[index];
                string trimmed = line.Trim();
                if (trimmed.Length == 0)
                    continue;
                metadata.NonEmptyLineCount++;
                if (IsComment(trimmed))
                    continue;

                if (trimmed[0] == '[' && trimmed[^1] == ']')
                {
                    currentSection = new JxqyTextSectionMetadata
                    {
                        Name = trimmed.Substring(1, trimmed.Length - 2).Trim(),
                        SourceLine = index + 1
                    };
                    metadata.Sections.Add(currentSection);
                    continue;
                }

                int equals = line.IndexOf('=');
                if (equals < 0)
                {
                    metadata.UnparsedStructuredLineCount++;
                    continue;
                }

                currentSection ??= CreateGlobalSection(metadata);
                currentSection.Properties.Add(new JxqyTextPropertyMetadata
                {
                    Key = line.Substring(0, equals).Trim(),
                    Value = line.Substring(equals + 1).Trim(),
                    SourceLine = index + 1
                });
            }
        }

        public static string[] SplitLines(string text)
        {
            if (text.Length == 0)
                return Array.Empty<string>();
            return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        }

        public static string DetectNewLineStyle(string text)
        {
            if (text.Contains("\r\n", StringComparison.Ordinal))
                return "CRLF";
            if (text.Contains('\n'))
                return "LF";
            if (text.Contains('\r'))
                return "CR";
            return "None";
        }

        private static JxqyTextSectionMetadata CreateGlobalSection(
            JxqyTextAssetMetadata metadata)
        {
            var section = new JxqyTextSectionMetadata
            {
                Name = string.Empty,
                SourceLine = 1
            };
            metadata.Sections.Add(section);
            return section;
        }

        private static bool IsComment(string trimmed)
        {
            return trimmed.StartsWith(";", StringComparison.Ordinal) ||
                   trimmed.StartsWith("#", StringComparison.Ordinal) ||
                   trimmed.StartsWith("//", StringComparison.Ordinal);
        }
    }
}
