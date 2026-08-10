using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Jxqy.Domain.Content;

namespace Jxqy.Domain.Scripting
{
    public enum JxqyScriptCategory
    {
        Normal = 0,
        Good = 1
    }

    public sealed class JxqyScriptResolution
    {
        public bool Found;
        public string RelativePath = string.Empty;
        public string ContentAddress = string.Empty;
        public List<string> AttemptedPaths = new();
    }

    public sealed class JxqyScriptPathResolver
    {
        private readonly Dictionary<string, JxqyScriptCatalogEntry> _entries;

        public JxqyScriptPathResolver(IEnumerable<JxqyScriptCatalogEntry> entries)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));

            _entries = new Dictionary<string, JxqyScriptCatalogEntry>(
                StringComparer.OrdinalIgnoreCase);
            foreach (JxqyScriptCatalogEntry entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.SourceRelativePath))
                    continue;
                string key = NormalizeRelativePath(entry.SourceRelativePath);
                if (!_entries.TryAdd(key, entry))
                {
                    throw new ArgumentException(
                        $"Duplicate case-insensitive script path: {entry.SourceRelativePath}",
                        nameof(entries));
                }
            }
        }

        public JxqyScriptResolution Resolve(
            string fileName,
            string mapName = null,
            JxqyScriptCategory category = JxqyScriptCategory.Normal)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return new JxqyScriptResolution();

            string normalizedFileName = NormalizeRelativePath(fileName);
            if (normalizedFileName.StartsWith("script/", StringComparison.OrdinalIgnoreCase))
                return ResolveCandidates(new[] { normalizedFileName });

            switch (category)
            {
                case JxqyScriptCategory.Normal:
                {
                    string normalizedMap = NormalizeMapName(mapName);
                    var candidates = new List<string>();
                    if (!string.IsNullOrEmpty(normalizedMap))
                        candidates.Add($"script/map/{normalizedMap}/{normalizedFileName}");
                    candidates.Add($"script/common/{normalizedFileName}");
                    return ResolveCandidates(candidates);
                }
                case JxqyScriptCategory.Good:
                    return ResolveCandidates(
                        new[] { $"script/goods/{normalizedFileName}" });
                default:
                    throw new ArgumentOutOfRangeException(nameof(category), category, null);
            }
        }

        private JxqyScriptResolution ResolveCandidates(IEnumerable<string> candidates)
        {
            var result = new JxqyScriptResolution();
            foreach (string candidate in candidates)
            {
                string key = NormalizeRelativePath(candidate);
                result.AttemptedPaths.Add(key);
                if (!_entries.TryGetValue(key, out JxqyScriptCatalogEntry entry))
                    continue;
                result.Found = true;
                result.RelativePath = entry.SourceRelativePath;
                result.ContentAddress = entry.ContentAddress;
                return result;
            }
            return result;
        }

        private static string NormalizeMapName(string mapName)
        {
            if (string.IsNullOrWhiteSpace(mapName))
                return string.Empty;
            string normalized = NormalizeRelativePath(mapName);
            normalized = normalized.Trim('/');
            if (normalized.Contains('/'))
                normalized = normalized.Split('/').Last();
            if (normalized.EndsWith(".map", StringComparison.OrdinalIgnoreCase))
                normalized = Path.GetFileNameWithoutExtension(normalized);
            return normalized;
        }

        private static string NormalizeRelativePath(string path)
        {
            string normalized = path.Replace('\\', '/').Trim().TrimStart('/');
            string[] segments = normalized.Split('/');
            if (segments.Any(segment =>
                    string.IsNullOrEmpty(segment) ||
                    segment == "." ||
                    segment == ".."))
            {
                throw new ArgumentException($"Unsafe script path: {path}", nameof(path));
            }
            return string.Join("/", segments);
        }
    }
}
