using System;
using System.IO;
using YooAsset.Editor;

namespace Jxqy.Editor.YooAsset
{
    /// <summary>
    /// Keeps each Unity scene in its own bundle and splits large converted
    /// domains at their natural lifetime boundary. Terrain art follows its map,
    /// character art follows its character family, and media follows its source
    /// file. Small configuration domains remain grouped to avoid bundle spam.
    /// </summary>
    [DisplayName("Jxqy: 领域分包且场景独立")]
    public sealed class JxqyPackByDomainAndScene : IPackRule
    {
        public PackRuleResult GetPackRuleResult(PackRuleData data)
        {
            string assetPath = Normalize(data.AssetPath);
            if (assetPath.EndsWith(
                    ".unity",
                    StringComparison.OrdinalIgnoreCase))
            {
                return new PackRuleResult(
                    assetPath.Substring(
                        0,
                        assetPath.Length -
                        Path.GetExtension(assetPath).Length),
                    DefaultPackRule.AssetBundleFileExtension);
            }

            string collectPath = Normalize(data.CollectPath).TrimEnd('/');
            if (!assetPath.StartsWith(
                    collectPath + "/",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"Jxqy asset is outside collector: {assetPath}",
                    nameof(data));
            }
            string relativePath = assetPath.Substring(
                collectPath.Length).TrimStart('/');
            string[] segments = relativePath.Split('/');
            if (segments.Length < 2 ||
                Path.HasExtension(segments[0]))
            {
                throw new InvalidDataException(
                    $"Jxqy asset has no top-level domain: {assetPath}");
            }
            return new PackRuleResult(
                GetBundleRoot(collectPath, segments),
                DefaultPackRule.AssetBundleFileExtension);
        }

        private static string GetBundleRoot(
            string collectPath,
            string[] segments)
        {
            string domain = segments[0];
            if (domain.Equals(
                    "Animations",
                    StringComparison.OrdinalIgnoreCase))
            {
                return GetAnimationBundleRoot(
                    collectPath,
                    segments);
            }
            if (domain.Equals(
                    "Media",
                    StringComparison.OrdinalIgnoreCase))
            {
                // Media/Music/Content/music/<source.wma>/...
                // Media/Video/Content/video/<source.wmv>/...
                int sourceIndex = Math.Min(4, segments.Length - 2);
                return JoinThrough(
                    collectPath,
                    segments,
                    sourceIndex);
            }
            if (domain.Equals(
                    "Images",
                    StringComparison.OrdinalIgnoreCase))
            {
                // Keep title/static image families independent from map images.
                int familyIndex = Math.Min(2, segments.Length - 2);
                return JoinThrough(
                    collectPath,
                    segments,
                    familyIndex);
            }
            if (domain.Equals(
                    "Text",
                    StringComparison.OrdinalIgnoreCase))
            {
                // Text contains more than fourteen thousand imported files.
                // A single bundle makes Unity's builtin serializer retain the
                // complete object graph and also forces unrelated scripts into
                // memory at runtime. Keep configuration families independent,
                // and use the original map as the script load boundary.
                int boundary = segments.Length > 3 &&
                               segments[1].Equals(
                                   "script",
                                   StringComparison.OrdinalIgnoreCase) &&
                               segments[2].Equals(
                                   "map",
                                   StringComparison.OrdinalIgnoreCase)
                    ? 3
                    : Math.Min(2, segments.Length - 2);
                return JoinThrough(
                    collectPath,
                    segments,
                    boundary);
            }
            return $"{collectPath}/{domain}";
        }

        private static string GetAnimationBundleRoot(
            string collectPath,
            string[] segments)
        {
            if (segments.Length < 4)
                return $"{collectPath}/{segments[0]}";

            string format = segments[1];
            string category = segments[2];
            if (format.Equals(
                    "mpc",
                    StringComparison.OrdinalIgnoreCase))
            {
                // Animations/mpc/map/<map-name>/<source.mpc>/...
                // The map directory is the runtime load/release boundary.
                int boundary = category.Equals(
                        "map",
                        StringComparison.OrdinalIgnoreCase)
                    ? 3
                    : Math.Min(3, segments.Length - 2);
                return JoinThrough(
                    collectPath,
                    segments,
                    boundary);
            }

            if (!format.Equals(
                    "asf",
                    StringComparison.OrdinalIgnoreCase))
            {
                return JoinThrough(
                    collectPath,
                    segments,
                    Math.Min(2, segments.Length - 2));
            }

            if (category.Equals(
                    "character",
                    StringComparison.OrdinalIgnoreCase))
            {
                // npc006_st2.asf, npc006_wlk2.asf, ... share one family bundle.
                string source = segments[3];
                int separator = source.IndexOf('_');
                string family = separator > 0
                    ? source.Substring(0, separator)
                    : Path.GetFileNameWithoutExtension(source);
                return
                    $"{collectPath}/{segments[0]}/{format}/{category}/" +
                    family;
            }

            if (category.Equals(
                    "ui",
                    StringComparison.OrdinalIgnoreCase) ||
                category.Equals(
                    "goods",
                    StringComparison.OrdinalIgnoreCase) ||
                category.Equals(
                    "magic",
                    StringComparison.OrdinalIgnoreCase) ||
                category.Equals(
                    "portrait",
                    StringComparison.OrdinalIgnoreCase))
            {
                return JoinThrough(
                    collectPath,
                    segments,
                    2);
            }

            // Effects, interludes, objects and unresolved assets are independent
            // source animations and must not force an entire category into RAM.
            return JoinThrough(
                collectPath,
                segments,
                Math.Min(3, segments.Length - 2));
        }

        private static string JoinThrough(
            string collectPath,
            string[] segments,
            int inclusiveIndex)
        {
            if (inclusiveIndex < 0 ||
                inclusiveIndex >= segments.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(inclusiveIndex));
            }
            return collectPath + "/" +
                   string.Join(
                       "/",
                       segments,
                       0,
                       inclusiveIndex + 1);
        }

        private static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path is empty.", nameof(path));
            return path.Replace('\\', '/');
        }
    }
}
