using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Jxqy.Domain.Content;
using Jxqy.Editor.Animation.Conversion;
using Jxqy.Editor.Scanning;
using Jxqy.Editor.YooAsset;
using UnityEditor;
using UnityEngine;

namespace Jxqy.Editor.Text
{
    public static class JxqyScriptCatalogBuilder
    {
        public const string CatalogConverterVersion = "0.1.0-script-catalog-1";
        private const string ManifestAssetPath =
            "Assets/AssetRaw/Jxqy/Manifests/source-manifest.json";
        private const string CatalogAssetPath =
            "Assets/AssetRaw/Jxqy/Manifests/script-catalog.json";

        [MenuItem("TEngine/Jxqy/Build Script Catalog")]
        public static void BuildAsset()
        {
            var settings = new JxqySourceSettings();
            settings.Validate();
            JxqySourceManifest manifest = JsonUtility.FromJson<JxqySourceManifest>(
                File.ReadAllText(GetAbsoluteAssetPath(ManifestAssetPath)));
            JxqyScriptCatalog catalog = Build(manifest, settings.OutputRoot);
            JxqyAnimationConverter.WriteJsonAsset(CatalogAssetPath, catalog, true);

            string summary =
                $"Jxqy script catalog built. Entries={catalog.Entries.Count}, " +
                $"MapLocal={catalog.MapLocalEntryCount}, Common={catalog.CommonEntryCount}, " +
                $"Goods={catalog.GoodsEntryCount}, DuplicateNames=" +
                $"{catalog.DuplicateFileNameGroupCount}, Errors={catalog.Errors.Count}.";
            if (catalog.Errors.Count == 0)
                Debug.Log(summary);
            else
                Debug.LogError(summary);
        }

        public static JxqyScriptCatalog Build(
            JxqySourceManifest manifest,
            string outputRoot)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));
            string normalizedOutput = outputRoot.Replace('\\', '/').TrimEnd('/');
            var catalog = new JxqyScriptCatalog
            {
                ConverterVersion = CatalogConverterVersion,
                GeneratedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            };
            var lookupKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            JxqySourceFileRecord[] scripts = manifest.Files
                .Where(file => file.Kind == JxqyFileKind.Script)
                .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
                .ToArray();
            foreach (JxqySourceFileRecord script in scripts)
            {
                string relative = Normalize(script.RelativePath);
                string contentAssetPath =
                    $"{normalizedOutput}/Text/{relative}/content.txt";
                string lookupKey = relative.ToLowerInvariant();
                if (!lookupKeys.Add(lookupKey))
                {
                    catalog.Errors.Add(
                        $"Duplicate case-insensitive script path: {script.RelativePath}");
                    continue;
                }
                if (!File.Exists(GetAbsoluteAssetPath(contentAssetPath)))
                {
                    catalog.Errors.Add(
                        $"Converted script content is missing: {script.RelativePath}");
                }

                catalog.Entries.Add(new JxqyScriptCatalogEntry
                {
                    SourceStableId = script.StableId,
                    SourceRelativePath = relative,
                    LookupKey = lookupKey,
                    ContentAddress = JxqyAddressByRelativePath.CreateAddress(
                        contentAssetPath,
                        normalizedOutput),
                    SourceSha256 = script.Sha256
                });
                if (relative.StartsWith("script/map/", StringComparison.OrdinalIgnoreCase))
                    catalog.MapLocalEntryCount++;
                else if (relative.StartsWith("script/common/", StringComparison.OrdinalIgnoreCase))
                    catalog.CommonEntryCount++;
                else if (relative.StartsWith("script/goods/", StringComparison.OrdinalIgnoreCase))
                    catalog.GoodsEntryCount++;
                else
                    catalog.OtherEntryCount++;
            }

            catalog.DuplicateFileNameGroupCount = scripts
                .GroupBy(
                    file => Path.GetFileName(file.RelativePath),
                    StringComparer.OrdinalIgnoreCase)
                .Count(group => group.Count() > 1);
            return catalog;
        }

        private static string Normalize(string path)
        {
            return path.Replace('\\', '/').TrimStart('/');
        }

        private static string GetAbsoluteAssetPath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }
    }
}
