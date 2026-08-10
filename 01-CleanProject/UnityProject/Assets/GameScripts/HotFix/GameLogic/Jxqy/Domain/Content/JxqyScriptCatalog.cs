using System;
using System.Collections.Generic;

namespace Jxqy.Domain.Content
{
    [Serializable]
    public sealed class JxqyScriptCatalog
    {
        public int SchemaVersion = 1;
        public string ConverterVersion = string.Empty;
        public string GeneratedUtc = string.Empty;
        public int MapLocalEntryCount;
        public int CommonEntryCount;
        public int GoodsEntryCount;
        public int OtherEntryCount;
        public int DuplicateFileNameGroupCount;
        public List<JxqyScriptCatalogEntry> Entries = new();
        public List<string> Errors = new();
    }

    [Serializable]
    public sealed class JxqyScriptCatalogEntry
    {
        public string SourceStableId = string.Empty;
        public string SourceRelativePath = string.Empty;
        public string LookupKey = string.Empty;
        public string ContentAddress = string.Empty;
        public string SourceSha256 = string.Empty;
    }
}
