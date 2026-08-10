using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Jxqy.Editor.Scanning
{
    public sealed class JxqySourceScanner
    {
        public JxqySourceManifest Scan(
            JxqySourceSettings settings,
            JxqySourceManifest previousManifest = null)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            settings.Validate();

            string sourceRoot = Path.GetFullPath(settings.GameSourceRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string[] sourceFiles = Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories);
            Array.Sort(sourceFiles, StringComparer.OrdinalIgnoreCase);

            var manifest = new JxqySourceManifest
            {
                ConverterVersion = JxqyImporterAssembly.ConverterVersion,
                SourceRoot = sourceRoot,
                GeneratedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                IncludesHashes = settings.IncludeHashes
            };
            var previousIndex = previousManifest?.Files?.ToDictionary(
                file => file.RelativePath,
                file => file,
                StringComparer.OrdinalIgnoreCase);

            foreach (string sourceFile in sourceFiles)
            {
                try
                {
                    string relativePath = JxqyPathUtility.NormalizeRelativePath(sourceRoot, sourceFile);
                    JxqySourceFileRecord previousRecord = null;
                    previousIndex?.TryGetValue(relativePath, out previousRecord);
                    JxqySourceFileRecord record = CreateRecord(
                        sourceRoot,
                        sourceFile,
                        settings.IncludeHashes,
                        previousRecord,
                        out bool reusedHash);
                    manifest.Files.Add(record);
                    manifest.TotalBytes += record.Size;
                    if (settings.IncludeHashes)
                    {
                        if (reusedHash)
                            manifest.ReusedHashCount++;
                        else
                            manifest.ComputedHashCount++;
                    }

                    string issue = JxqyPathUtility.FindPortabilityIssue(record.RelativePath);
                    if (!string.IsNullOrEmpty(issue))
                        manifest.PortabilityWarnings.Add($"{record.RelativePath}: {issue}");
                }
                catch (Exception exception)
                {
                    string relative;
                    try
                    {
                        relative = JxqyPathUtility.NormalizeRelativePath(sourceRoot, sourceFile);
                    }
                    catch
                    {
                        relative = sourceFile;
                    }

                    manifest.Errors.Add($"{relative}: {exception.GetType().Name}: {exception.Message}");
                }
            }

            manifest.DuplicateFileNames = FindGroups(
                manifest.Files,
                record => Path.GetFileName(record.RelativePath).ToLowerInvariant());
            manifest.AddressCollisions = FindGroups(
                manifest.Files,
                record => record.SourceAddress);
            manifest.CaseInsensitivePathCollisions = FindGroups(
                manifest.Files,
                record => record.RelativePath.ToLowerInvariant());

            if (IsUnchanged(previousManifest, manifest))
                manifest.GeneratedUtc = previousManifest.GeneratedUtc;

            return manifest;
        }

        private static JxqySourceFileRecord CreateRecord(
            string sourceRoot,
            string sourceFile,
            bool includeHash,
            JxqySourceFileRecord previousRecord,
            out bool reusedHash)
        {
            var before = new FileInfo(sourceFile);
            string relativePath = JxqyPathUtility.NormalizeRelativePath(sourceRoot, sourceFile);
            JxqyFileKind kind = Classify(relativePath);

            reusedHash = includeHash &&
                         previousRecord != null &&
                         previousRecord.Size == before.Length &&
                         previousRecord.LastWriteUtcTicks == before.LastWriteTimeUtc.Ticks &&
                         !string.IsNullOrEmpty(previousRecord.Sha256);
            string hash = includeHash
                ? reusedHash
                    ? previousRecord.Sha256
                    : ComputeSha256(sourceFile)
                : string.Empty;

            var after = new FileInfo(sourceFile);
            if (before.Length != after.Length || before.LastWriteTimeUtc != after.LastWriteTimeUtc)
                throw new IOException("Source file changed while it was being scanned.");

            return new JxqySourceFileRecord
            {
                StableId = JxqyPathUtility.CreateStableId(kind, relativePath),
                RelativePath = relativePath,
                SourceAddress = JxqyPathUtility.CreateSourceAddress(relativePath),
                Extension = after.Extension.ToLowerInvariant(),
                Kind = kind,
                Size = after.Length,
                LastWriteUtcTicks = after.LastWriteTimeUtc.Ticks,
                Sha256 = hash
            };
        }

        private static bool IsUnchanged(
            JxqySourceManifest previous,
            JxqySourceManifest current)
        {
            if (previous == null ||
                previous.ConverterVersion != current.ConverterVersion ||
                previous.IncludesHashes != current.IncludesHashes ||
                previous.Files.Count != current.Files.Count)
            {
                return false;
            }

            for (int index = 0; index < current.Files.Count; index++)
            {
                JxqySourceFileRecord left = previous.Files[index];
                JxqySourceFileRecord right = current.Files[index];
                if (!string.Equals(left.RelativePath, right.RelativePath, StringComparison.Ordinal) ||
                    left.Kind != right.Kind ||
                    !string.Equals(left.Extension, right.Extension, StringComparison.Ordinal) ||
                    !string.Equals(left.SourceAddress, right.SourceAddress, StringComparison.Ordinal) ||
                    left.Size != right.Size ||
                    left.LastWriteUtcTicks != right.LastWriteUtcTicks ||
                    !string.Equals(left.Sha256, right.Sha256, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static string ComputeSha256(string filePath)
        {
            using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                1024 * 1024,
                FileOptions.SequentialScan);
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static List<JxqyConflictGroup> FindGroups(
            IEnumerable<JxqySourceFileRecord> records,
            Func<JxqySourceFileRecord, string> keySelector)
        {
            return records
                .GroupBy(keySelector, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new JxqyConflictGroup
                {
                    Key = group.Key,
                    RelativePaths = group
                        .Select(record => record.RelativePath)
                        .OrderBy(path => path, StringComparer.Ordinal)
                        .ToList()
                })
                .ToList();
        }

        internal static JxqyFileKind Classify(string relativePath)
        {
            string extension = Path.GetExtension(relativePath).ToLowerInvariant();
            string normalized = relativePath.Replace('\\', '/').ToLowerInvariant();

            return extension switch
            {
                ".asf" => JxqyFileKind.Asf,
                ".mpc" => JxqyFileKind.Mpc,
                ".mpi" => JxqyFileKind.Mpi,
                ".map" => JxqyFileKind.Map,
                ".npc" => JxqyFileKind.Npc,
                ".obj" => JxqyFileKind.Obj,
                ".ini" when normalized.StartsWith("save/", StringComparison.Ordinal) => JxqyFileKind.Save,
                ".ini" => JxqyFileKind.Ini,
                ".txt" when normalized.StartsWith("script/", StringComparison.Ordinal) => JxqyFileKind.Script,
                ".txt" when normalized.StartsWith("mpc/", StringComparison.Ordinal) => JxqyFileKind.Binary,
                ".txt" => JxqyFileKind.Ini,
                ".png" or ".jpg" or ".jpeg" => JxqyFileKind.Image,
                ".xnb" => JxqyFileKind.Xnb,
                ".wma" or ".mp3" or ".ogg" or ".wav" => JxqyFileKind.Music,
                ".wmv" or ".mp4" => JxqyFileKind.Video,
                ".scc" or ".7z" or ".cab" or ".exe" or ".dll" or ".pdb" => JxqyFileKind.Ignored,
                _ => JxqyFileKind.Unknown
            };
        }
    }
}
