using System;
using System.Collections.Generic;

namespace Jxqy.Editor.Scanning
{
    [Serializable]
    public sealed class JxqyScriptCommandOccurrence
    {
        public string ScriptPath;
        public int LineNumber;
        public int ParameterCount;
        public string Literal;
    }

    [Serializable]
    public sealed class JxqyScriptCommandRecord
    {
        public string Name;
        public int Count;
        public bool ImplementedByReferenceRunner;
        public List<int> ParameterCounts = new List<int>();
        public List<JxqyScriptCommandOccurrence> Occurrences = new List<JxqyScriptCommandOccurrence>();
    }

    [Serializable]
    public sealed class JxqyScriptCommandReport
    {
        public string ConverterVersion;
        public string GeneratedUtc;
        public int ScriptCount;
        public int ParsedCommandCount;
        public List<JxqyScriptCommandRecord> Commands = new List<JxqyScriptCommandRecord>();
        public List<string> ParseWarnings = new List<string>();
    }
}
