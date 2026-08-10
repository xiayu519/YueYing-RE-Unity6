using System;
using System.Collections.Generic;

namespace Jxqy.Domain.Content
{
    [Serializable]
    public sealed class JxqyTextAssetMetadata
    {
        public int SchemaVersion = 1;
        public string ConverterVersion = string.Empty;
        public string SourceStableId = string.Empty;
        public string SourceRelativePath = string.Empty;
        public string SourceAddress = string.Empty;
        public string SourceSha256 = string.Empty;
        public string SourceKind = string.Empty;
        public string OriginalEncoding = string.Empty;
        public string ContentAddress = string.Empty;
        public string Utf8Sha256 = string.Empty;
        public string NewLineStyle = string.Empty;
        public int CharacterCount;
        public int LineCount;
        public int NonEmptyLineCount;
        public int UnparsedStructuredLineCount;
        public List<JxqyTextSectionMetadata> Sections = new();
    }

    [Serializable]
    public sealed class JxqyTextSectionMetadata
    {
        public string Name = string.Empty;
        public int SourceLine;
        public List<JxqyTextPropertyMetadata> Properties = new();
    }

    [Serializable]
    public sealed class JxqyTextPropertyMetadata
    {
        public string Key = string.Empty;
        public string Value = string.Empty;
        public int SourceLine;
    }
}
