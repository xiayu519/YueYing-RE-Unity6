using System;
using System.Collections.Generic;

namespace Jxqy.Editor.Scanning
{
    public enum JxqyFileKind
    {
        Unknown,
        Asf,
        Mpc,
        Mpi,
        Map,
        Npc,
        Obj,
        Ini,
        Script,
        Image,
        Xnb,
        Music,
        Video,
        Save,
        Ignored,
        Binary
    }

    [Serializable]
    public sealed class JxqySourceFileRecord
    {
        public string StableId;
        public string RelativePath;
        public string SourceAddress;
        public string Extension;
        public JxqyFileKind Kind;
        public long Size;
        public long LastWriteUtcTicks;
        public string Sha256;
    }

    [Serializable]
    public sealed class JxqyConflictGroup
    {
        public string Key;
        public List<string> RelativePaths = new List<string>();
    }

    [Serializable]
    public sealed class JxqySourceManifest
    {
        public string ConverterVersion;
        public string SourceRoot;
        public string GeneratedUtc;
        public bool IncludesHashes;
        public long TotalBytes;
        public int ComputedHashCount;
        public int ReusedHashCount;
        public List<JxqySourceFileRecord> Files = new List<JxqySourceFileRecord>();
        public List<JxqyConflictGroup> DuplicateFileNames = new List<JxqyConflictGroup>();
        public List<JxqyConflictGroup> AddressCollisions = new List<JxqyConflictGroup>();
        public List<JxqyConflictGroup> CaseInsensitivePathCollisions = new List<JxqyConflictGroup>();
        public List<string> PortabilityWarnings = new List<string>();
        public List<string> Errors = new List<string>();

        public bool IsValid => Errors.Count == 0 && AddressCollisions.Count == 0 &&
                               CaseInsensitivePathCollisions.Count == 0;
    }
}
