using System;
using System.Collections.Generic;

namespace Jxqy.Editor.Scanning
{
    [Serializable]
    public sealed class JxqyDependencyReference
    {
        public string SourceStableId;
        public string SourceRelativePath;
        public string RawReference;
        public string TargetStableId;
        public string TargetRelativePath;
        public int LineNumber;
        public bool Resolved;
    }

    [Serializable]
    public sealed class JxqyDependencyGraph
    {
        public string ConverterVersion;
        public string GeneratedUtc;
        public List<JxqyDependencyReference> References = new List<JxqyDependencyReference>();
        public List<string> ParseErrors = new List<string>();

        public int ResolvedCount;
        public int UnresolvedCount;
    }
}
