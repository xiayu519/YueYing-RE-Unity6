using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Jxqy.Editor.Scanning
{
    public sealed class JxqyDependencyScanner
    {
        private static readonly Regex PathReferenceRegex = new Regex(
            @"(?<path>[^""'\s,;=\[\]\(\)]+?\.(?:asf|mpc|map|npc|obj|ini|txt|xnb|wav|wma|wmv|png|jpg))",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly Encoding _legacyEncoding;

        public JxqyDependencyScanner()
        {
            _legacyEncoding = Encoding.GetEncoding(936);
        }

        public JxqyDependencyGraph Scan(
            string sourceRoot,
            IReadOnlyList<JxqySourceFileRecord> files)
        {
            if (string.IsNullOrWhiteSpace(sourceRoot))
                throw new ArgumentException("Source root is empty.", nameof(sourceRoot));
            if (files == null)
                throw new ArgumentNullException(nameof(files));

            var graph = new JxqyDependencyGraph
            {
                ConverterVersion = JxqyImporterAssembly.ConverterVersion,
                GeneratedUtc = DateTime.UtcNow.ToString("O")
            };
            var index = files.ToDictionary(
                file => file.RelativePath.ToLowerInvariant(),
                file => file,
                StringComparer.Ordinal);
            var fileNameIndex = files
                .GroupBy(
                    file => Path.GetFileName(file.RelativePath),
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.ToArray(),
                    StringComparer.OrdinalIgnoreCase);

            foreach (JxqySourceFileRecord source in files)
            {
                string absolutePath = Path.Combine(
                    sourceRoot,
                    source.RelativePath.Replace('/', Path.DirectorySeparatorChar));

                try
                {
                    switch (source.Kind)
                    {
                        case JxqyFileKind.Map:
                            ScanMap(absolutePath, source, index, fileNameIndex, graph);
                            break;
                        case JxqyFileKind.Ini:
                        case JxqyFileKind.Npc:
                        case JxqyFileKind.Obj:
                        case JxqyFileKind.Script:
                            ScanText(absolutePath, source, index, fileNameIndex, graph);
                            break;
                    }
                }
                catch (Exception exception)
                {
                    graph.ParseErrors.Add(
                        $"{source.RelativePath}: {exception.GetType().Name}: {exception.Message}");
                }
            }

            graph.ResolvedCount = graph.References.Count(reference => reference.Resolved);
            graph.UnresolvedCount = graph.References.Count - graph.ResolvedCount;
            return graph;
        }

        private void ScanMap(
            string absolutePath,
            JxqySourceFileRecord source,
            IReadOnlyDictionary<string, JxqySourceFileRecord> index,
            IReadOnlyDictionary<string, JxqySourceFileRecord[]> fileNameIndex,
            JxqyDependencyGraph graph)
        {
            byte[] bytes = File.ReadAllBytes(absolutePath);
            if (bytes.Length < 16512)
                throw new InvalidDataException($"MAP is smaller than its fixed header: {bytes.Length}");

            string magic = _legacyEncoding.GetString(bytes, 0, "MAP File Ver".Length);
            if (!string.Equals(magic, "MAP File Ver", StringComparison.Ordinal))
                throw new InvalidDataException($"Unexpected MAP magic '{magic}'.");

            string mapName = Path.GetFileNameWithoutExtension(source.RelativePath);
            string mpcDirectory = ReadMapMpcDirectory(bytes);
            if (string.IsNullOrEmpty(mpcDirectory))
                mpcDirectory = $"mpc/map/{mapName}";

            for (int indexEntry = 0; indexEntry < 255; indexEntry++)
            {
                int offset = 192 + indexEntry * 64;
                string fileName = ReadNullTerminated(bytes, offset, 32);
                if (string.IsNullOrEmpty(fileName))
                    continue;

                string rawReference = $"{mpcDirectory}/{fileName}".Replace('\\', '/');
                AddReference(source, rawReference, 0, index, fileNameIndex, graph);
            }
        }

        private void ScanText(
            string absolutePath,
            JxqySourceFileRecord source,
            IReadOnlyDictionary<string, JxqySourceFileRecord> index,
            IReadOnlyDictionary<string, JxqySourceFileRecord[]> fileNameIndex,
            JxqyDependencyGraph graph)
        {
            string[] lines = File.ReadAllLines(absolutePath, _legacyEncoding);
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                foreach (Match match in PathReferenceRegex.Matches(lines[lineIndex]))
                {
                    string rawReference = match.Groups["path"].Value.Trim('"', '\'');
                    AddReference(
                        source,
                        rawReference,
                        lineIndex + 1,
                        index,
                        fileNameIndex,
                        graph);
                }
            }
        }

        private static void AddReference(
            JxqySourceFileRecord source,
            string rawReference,
            int lineNumber,
            IReadOnlyDictionary<string, JxqySourceFileRecord> index,
            IReadOnlyDictionary<string, JxqySourceFileRecord[]> fileNameIndex,
            JxqyDependencyGraph graph)
        {
            string normalizedReference = rawReference
                .Replace('\\', '/')
                .TrimStart('.', '/')
                .Normalize(NormalizationForm.FormC);

            JxqySourceFileRecord target = Resolve(
                source.RelativePath,
                normalizedReference,
                index,
                fileNameIndex);

            graph.References.Add(new JxqyDependencyReference
            {
                SourceStableId = source.StableId,
                SourceRelativePath = source.RelativePath,
                RawReference = rawReference,
                TargetStableId = target?.StableId ?? string.Empty,
                TargetRelativePath = target?.RelativePath ?? string.Empty,
                LineNumber = lineNumber,
                Resolved = target != null
            });
        }

        private static JxqySourceFileRecord Resolve(
            string sourceRelativePath,
            string reference,
            IReadOnlyDictionary<string, JxqySourceFileRecord> index,
            IReadOnlyDictionary<string, JxqySourceFileRecord[]> fileNameIndex)
        {
            var candidates = new List<string>
            {
                reference,
                CombineRelative(Path.GetDirectoryName(sourceRelativePath)?.Replace('\\', '/'), reference)
            };

            string lower = reference.ToLowerInvariant();
            if (lower.StartsWith("music/", StringComparison.Ordinal) ||
                lower.StartsWith("sound/", StringComparison.Ordinal) ||
                lower.StartsWith("video/", StringComparison.Ordinal) ||
                lower.StartsWith("effect/", StringComparison.Ordinal) ||
                lower.StartsWith("font/", StringComparison.Ordinal))
            {
                candidates.Add("Content/" + reference);
            }

            foreach (string candidate in candidates.Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                string key = CollapseSegments(candidate).ToLowerInvariant();
                if (index.TryGetValue(key, out JxqySourceFileRecord target))
                    return target;
            }

            string fileName = Path.GetFileName(reference);
            if (!string.IsNullOrEmpty(fileName) &&
                fileNameIndex.TryGetValue(
                    fileName,
                    out JxqySourceFileRecord[] byName) &&
                byName.Length == 1)
            {
                return byName[0];
            }

            return null;
        }

        private static string CombineRelative(string directory, string reference)
        {
            return string.IsNullOrEmpty(directory) ? reference : directory + "/" + reference;
        }

        private static string CollapseSegments(string path)
        {
            var segments = new List<string>();
            foreach (string segment in path.Replace('\\', '/').Split('/'))
            {
                if (string.IsNullOrEmpty(segment) || segment == ".")
                    continue;
                if (segment == "..")
                {
                    if (segments.Count > 0)
                        segments.RemoveAt(segments.Count - 1);
                    continue;
                }
                segments.Add(segment);
            }
            return string.Join("/", segments);
        }

        private string ReadMapMpcDirectory(byte[] bytes)
        {
            int length = 0;
            while (length < 32 && bytes[32 + length] != 0)
                length++;

            if (length <= 1)
                return string.Empty;

            return _legacyEncoding.GetString(bytes, 33, length - 1)
                .Replace('\\', '/')
                .Trim('/');
        }

        private string ReadNullTerminated(byte[] bytes, int offset, int maximumLength)
        {
            int length = 0;
            while (length < maximumLength && bytes[offset + length] != 0)
                length++;
            return length == 0 ? string.Empty : _legacyEncoding.GetString(bytes, offset, length);
        }
    }
}
