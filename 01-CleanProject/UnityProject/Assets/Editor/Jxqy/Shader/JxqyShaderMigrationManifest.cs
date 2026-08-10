using System;
using System.Collections.Generic;

namespace Jxqy.Editor.Shader
{
    [Serializable]
    public sealed class JxqyShaderMigrationInput
    {
        public string Name = string.Empty;
        public string SourceRelativePath = string.Empty;
        public string SourceSha256 = string.Empty;
        public long SourceSize;
        public string RegisteredAssetPath = string.Empty;
        public string CompiledXnbRelativePath = string.Empty;
        public string CompiledXnbSha256 = string.Empty;
        public bool CompiledXnbExcluded;
        public string ExclusionReason = string.Empty;
    }

    [Serializable]
    public sealed class JxqyShaderMigrationDependency
    {
        public string SourceRelativePath = string.Empty;
        public string SourceSha256 = string.Empty;
        public long SourceSize;
        public string RegisteredAssetPath = string.Empty;
        public string CompiledXnbRelativePath = string.Empty;
    }

    [Serializable]
    public sealed class JxqyShaderMigrationManifest
    {
        public int SchemaVersion = 1;
        public string GeneratorVersion = string.Empty;
        public string GeneratedUtc = string.Empty;
        public int ShaderSourceCount;
        public int ExcludedCompiledEffectCount;
        public int DependencyCount;
        public List<JxqyShaderMigrationInput> ShaderSources = new();
        public List<JxqyShaderMigrationDependency> Dependencies = new();
        public List<string> Errors = new();
    }
}
