using System;
using System.IO;

namespace Jxqy.Editor.Scanning
{
    [Serializable]
    public sealed class JxqySourceSettings
    {
        public const string DefaultGameSourceRoot = @"D:\Games\Sword";
        public const string DefaultReferenceSourceRoot = @"D:\gitframework\JxqyHD";
        public const string DefaultOutputRoot = "Assets/AssetRaw/Jxqy";

        public string GameSourceRoot = DefaultGameSourceRoot;
        public string ReferenceSourceRoot = DefaultReferenceSourceRoot;
        public string OutputRoot = DefaultOutputRoot;
        public bool IncludeHashes = true;

        public void Validate()
        {
            ValidateReadOnlyDirectory(GameSourceRoot, nameof(GameSourceRoot));
            ValidateReadOnlyDirectory(ReferenceSourceRoot, nameof(ReferenceSourceRoot));

            string normalizedOutput = OutputRoot?.Replace('\\', '/').TrimEnd('/');
            if (string.IsNullOrWhiteSpace(normalizedOutput) ||
                !normalizedOutput.StartsWith("Assets/AssetRaw/Jxqy", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{nameof(OutputRoot)} must stay below Assets/AssetRaw/Jxqy.");
            }
        }

        private static void ValidateReadOnlyDirectory(string path, string settingName)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException($"{settingName} is empty.");

            if (!Path.IsPathRooted(path))
                throw new InvalidOperationException($"{settingName} must be an absolute path: {path}");

            if (!Directory.Exists(path))
                throw new DirectoryNotFoundException($"{settingName} does not exist: {path}");
        }
    }
}
