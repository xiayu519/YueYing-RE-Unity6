using System;
using YooAsset.Editor;

namespace Jxqy.Editor.YooAsset
{
    /// <summary>
    /// Uses the complete path below the collector as the address.
    /// The extension is retained so assets with the same stem but different
    /// types cannot collide.
    /// </summary>
    [DisplayName("Jxqy: 完整相对路径")]
    public sealed class JxqyAddressByRelativePath : IAddressRule
    {
        public const string AddressPrefix = "jxqy/";

        public string GetAssetAddress(AddressRuleData data)
        {
            return CreateAddress(data.AssetPath, data.CollectPath);
        }

        public static string CreateAddress(
            string assetPath,
            string collectPath = "Assets/AssetRaw/Jxqy")
        {
            assetPath = Normalize(assetPath);
            collectPath = Normalize(collectPath).TrimEnd('/');

            if (!assetPath.StartsWith(collectPath + "/", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"Jxqy asset '{assetPath}' is outside collector '{collectPath}'.",
                    nameof(assetPath));
            }

            string relativePath = assetPath.Substring(collectPath.Length + 1);
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new ArgumentException(
                    "Jxqy asset address cannot be empty.",
                    nameof(assetPath));
            }

            return AddressPrefix + relativePath.ToLowerInvariant();
        }

        private static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Asset path cannot be empty.", nameof(path));

            return path.Replace('\\', '/').Trim();
        }
    }
}
