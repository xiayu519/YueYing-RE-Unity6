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

namespace Jxqy.Editor.Preload
{
    public static class JxqyPreloadManifestJob
    {
        public const string GeneratorVersion =
            "0.4.0-scene-resource-keys";
        public const string ManifestAssetPath =
            "Assets/AssetRaw/Jxqy/Manifests/preload-manifest.json";
        private const string SourceRoot = "Assets/AssetRaw/Jxqy";
        private const string AutomationRequestPath =
            "Temp/JxqyValidation/generate-preload.request";

        [InitializeOnLoadMethod]
        private static void RunRequestedGeneration()
        {
            string requestPath = GetAbsoluteAssetPath(
                AutomationRequestPath);
            if (!File.Exists(requestPath))
                return;
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(requestPath))
                    return;
                File.Delete(requestPath);
                Generate();
            };
        }

        [MenuItem("TEngine/Jxqy/Generate Preload Manifest")]
        public static void Generate()
        {
            var manifest = new JxqyPreloadManifest
            {
                GeneratorVersion = GeneratorVersion,
                GeneratedUtc = DateTime.UtcNow.ToString(
                    "O",
                    CultureInfo.InvariantCulture)
            };
            try
            {
                var animationIndex = LoadAnimationIndex();
                var imageIndex = LoadImageIndex();
                BuildMapGroups(
                    manifest,
                    animationIndex,
                    imageIndex);
                BuildSharedGameplayGroup(manifest, animationIndex);
                BuildUiGroup(manifest, animationIndex, imageIndex);
                BuildAudioGroup(manifest);
                BuildVideoGroup(manifest);
                BuildDynamicTextGroup(manifest);
                manifest.IntentionalExclusions.Add(
                    "Content/effect/*.xnb: XNA Effect bytecode is excluded; use shader-migration-inputs.json.");
                manifest.IntentionalExclusions.Add(
                    "Two byte-identical corrupt and unreferenced ASF files under asf/未找到 are excluded by animation conversion validation.");
                FinalizeAndValidate(manifest);
                JxqyAnimationConverter.WriteJsonAsset(
                    ManifestAssetPath,
                    manifest,
                    true);
            }
            catch (Exception exception)
            {
                manifest.Errors.Add(
                    $"{exception.GetType().Name}: {exception.Message}");
                JxqyAnimationConverter.WriteJsonAsset(
                    ManifestAssetPath,
                    manifest,
                    true);
                Debug.LogException(exception);
            }

            string summary =
                $"Jxqy preload manifest generated. Groups={manifest.GroupCount}, " +
                $"Maps={manifest.MapGroupCount}, Entries={manifest.ResourceEntryCount}, " +
                $"ReferencedBytes={manifest.ReferencedFileBytes}, Errors={manifest.Errors.Count}.";
            if (manifest.Errors.Count == 0)
                Debug.Log(summary);
            else
                Debug.LogError(summary);
        }

        private static Dictionary<string, AnimationEntry> LoadAnimationIndex()
        {
            string root = GetAbsoluteAssetPath(
                $"{SourceRoot}/Animations");
            var result = new Dictionary<string, AnimationEntry>(
                StringComparer.OrdinalIgnoreCase);
            foreach (string path in Directory.EnumerateFiles(
                         root,
                         "animation.json",
                         SearchOption.AllDirectories))
            {
                JxqyAnimationMetadata metadata =
                    JsonUtility.FromJson<JxqyAnimationMetadata>(
                        File.ReadAllText(path));
                string assetPath = ToAssetPath(path);
                result.Add(
                    metadata.SourceStableId,
                    new AnimationEntry(assetPath, metadata));
            }
            return result;
        }

        private static Dictionary<string, ImageEntry> LoadImageIndex()
        {
            const string reportPath =
                "Assets/AssetRaw/Jxqy/Reports/image-conversion-report.json";
            Jxqy.Editor.Image.JxqyStaticImageConversionReport report =
                JsonUtility.FromJson<
                    Jxqy.Editor.Image.JxqyStaticImageConversionReport>(
                    File.ReadAllText(GetAbsoluteAssetPath(reportPath)));
            return report.Files
                .Where(file =>
                    file.Status != Jxqy.Editor.Image
                        .JxqyStaticImageConverter.FailedStatus)
                .ToDictionary(
                    file => file.StableId,
                    file => new ImageEntry(
                        file.StableId,
                        file.AssetPath,
                        file.Address,
                        file.RelativePath),
                    StringComparer.OrdinalIgnoreCase);
        }

        private static void BuildMapGroups(
            JxqyPreloadManifest manifest,
            Dictionary<string, AnimationEntry> animationIndex,
            Dictionary<string, ImageEntry> imageIndex)
        {
            string mapsRoot = GetAbsoluteAssetPath($"{SourceRoot}/Maps");
            foreach (string metadataPath in Directory
                         .EnumerateFiles(
                             mapsRoot,
                             "map.json",
                             SearchOption.AllDirectories)
                         .OrderBy(
                             path => path,
                             StringComparer.OrdinalIgnoreCase))
            {
                JxqyMapMetadata metadata =
                    JsonUtility.FromJson<JxqyMapMetadata>(
                        File.ReadAllText(metadataPath));
                var group = new JxqyPreloadGroup
                {
                    Id = $"map:{metadata.SourceStableId}",
                    Kind = "Map",
                    ResourceNamespace = "scene",
                    SceneKey = metadata.SourceStableId,
                    OwnerStableId = metadata.SourceStableId,
                    OwnerRelativePath = metadata.SourceRelativePath
                };
                AddAssetPath(
                    group,
                    ToAssetPath(metadataPath),
                    "MapMetadata",
                    metadata.SourceStableId);
                AddAddress(
                    group,
                    metadata.DataAddress,
                    "MapData",
                    metadata.SourceStableId);
                string miniMapStableId =
                    "image:map/littlemap/" +
                    Path.GetFileNameWithoutExtension(
                        metadata.SourceRelativePath) +
                    ".png";
                if (imageIndex.TryGetValue(
                        miniMapStableId,
                        out ImageEntry miniMap))
                    AddAssetPath(
                        group,
                        miniMap.AssetPath,
                        "MiniMapTexture",
                        miniMapStableId);
                foreach (JxqyMapMpcMetadata entry in
                         metadata.MpcTable)
                {
                    if (string.IsNullOrWhiteSpace(entry.FileName))
                        continue;
                    string stableId =
                        $"mpc:{metadata.MpcDirectory}/" +
                        entry.FileName;
                    if (animationIndex.TryGetValue(
                            stableId,
                            out AnimationEntry animation))
                    {
                        AddAnimation(group, animation);
                    }
                    else
                    {
                        manifest.Errors.Add(
                            $"{metadata.SourceStableId}: scene animation " +
                            $"is absent from the conversion index: " +
                            $"{stableId}.");
                    }
                }
                manifest.Groups.Add(group);
            }
        }

        private static void BuildSharedGameplayGroup(
            JxqyPreloadManifest manifest,
            Dictionary<string, AnimationEntry> animationIndex)
        {
            var group = new JxqyPreloadGroup
            {
                Id = "shared:characters-gameplay",
                Kind = "SharedCharacters",
                ResourceNamespace = "shared",
                OwnerStableId = "shared:characters-gameplay",
                OwnerRelativePath =
                    "asf/character,effect,goods,interlude,magic,object,portrait"
            };
            foreach (AnimationEntry animation in animationIndex.Values
                         .Where(entry =>
                             entry.Metadata.SourceRelativePath.StartsWith(
                                 "asf/",
                                 StringComparison.OrdinalIgnoreCase) &&
                             !entry.Metadata.SourceRelativePath.StartsWith(
                                 "asf/ui/",
                                 StringComparison.OrdinalIgnoreCase))
                         .OrderBy(
                             entry => entry.Metadata.SourceRelativePath,
                             StringComparer.OrdinalIgnoreCase))
                AddAnimation(group, animation);
            manifest.Groups.Add(group);
        }

        private static void BuildUiGroup(
            JxqyPreloadManifest manifest,
            Dictionary<string, AnimationEntry> animationIndex,
            Dictionary<string, ImageEntry> imageIndex)
        {
            var group = new JxqyPreloadGroup
            {
                Id = "shared:ui",
                Kind = "UI",
                ResourceNamespace = "shared",
                OwnerStableId = "shared:ui",
                OwnerRelativePath = "asf/ui,Content/ui,font,img,map/littlemap"
            };
            foreach (AnimationEntry animation in animationIndex.Values
                         .Where(entry =>
                             entry.Metadata.SourceRelativePath.StartsWith(
                                 "asf/ui/",
                                 StringComparison.OrdinalIgnoreCase))
                         .OrderBy(
                             entry => entry.Metadata.SourceRelativePath,
                             StringComparer.OrdinalIgnoreCase))
                AddAnimation(group, animation);
            foreach (ImageEntry image in imageIndex.Values
                         .OrderBy(
                             entry => entry.RelativePath,
                             StringComparer.OrdinalIgnoreCase))
                AddAssetPath(
                    group,
                    image.AssetPath,
                    "UiTexture",
                    image.StableId);
            foreach (string fontPath in Directory.EnumerateFiles(
                         GetAbsoluteAssetPath($"{SourceRoot}/Fonts"),
                         "font.json",
                         SearchOption.AllDirectories))
            {
                JxqySpriteFontMetadata font =
                    JsonUtility.FromJson<JxqySpriteFontMetadata>(
                        File.ReadAllText(fontPath));
                AddAssetPath(
                    group,
                    ToAssetPath(fontPath),
                    "FontMetadata",
                    font.SourceStableId);
                AddAddress(
                    group,
                    font.TextureAddress,
                    "FontTexture",
                    font.SourceStableId);
            }
            string uiSettings =
                $"{SourceRoot}/Text/Content/ui/UI_Settings.ini";
            if (Directory.Exists(GetAbsoluteAssetPath(uiSettings)))
            {
                AddAssetPath(
                    group,
                    uiSettings + "/content.txt",
                    "UiConfiguration",
                    "ini:content/ui/ui_settings.ini");
                AddAssetPath(
                    group,
                    uiSettings + "/metadata.json",
                    "UiConfigurationMetadata",
                    "ini:content/ui/ui_settings.ini");
            }
            manifest.Groups.Add(group);
        }

        private static void BuildAudioGroup(JxqyPreloadManifest manifest)
        {
            var group = new JxqyPreloadGroup
            {
                Id = "shared:audio",
                Kind = "Audio",
                ResourceNamespace = "shared",
                OwnerStableId = "shared:audio",
                OwnerRelativePath = "Content/sound,Content/music"
            };
            string soundRoot = GetAbsoluteAssetPath($"{SourceRoot}/Audio");
            foreach (string path in Directory.EnumerateFiles(
                         soundRoot,
                         "metadata.json",
                         SearchOption.AllDirectories))
            {
                JxqyAudioMetadata metadata =
                    JsonUtility.FromJson<JxqyAudioMetadata>(
                        File.ReadAllText(path));
                AddAssetPath(
                    group,
                    ToAssetPath(path),
                    "SoundMetadata",
                    metadata.SourceStableId);
                AddAddress(
                    group,
                    metadata.WavAddress,
                    "SoundClip",
                    metadata.SourceStableId);
            }
            string mediaRoot = GetAbsoluteAssetPath(
                $"{SourceRoot}/Media/Music");
            foreach (string path in Directory.EnumerateFiles(
                         mediaRoot,
                         "metadata.json",
                         SearchOption.AllDirectories))
            {
                JxqyMediaMetadata metadata =
                    JsonUtility.FromJson<JxqyMediaMetadata>(
                        File.ReadAllText(path));
                AddAssetPath(
                    group,
                    ToAssetPath(path),
                    "MusicMetadata",
                    metadata.SourceStableId);
                AddAddress(
                    group,
                    metadata.OutputAddress,
                    "MusicClip",
                    metadata.SourceStableId);
            }
            manifest.Groups.Add(group);
        }

        private static void BuildVideoGroup(JxqyPreloadManifest manifest)
        {
            var group = new JxqyPreloadGroup
            {
                Id = "shared:video",
                Kind = "Video",
                ResourceNamespace = "shared",
                OwnerStableId = "shared:video",
                OwnerRelativePath = "Content/video"
            };
            string root = GetAbsoluteAssetPath(
                $"{SourceRoot}/Media/Video");
            foreach (string path in Directory.EnumerateFiles(
                         root,
                         "metadata.json",
                         SearchOption.AllDirectories))
            {
                JxqyMediaMetadata metadata =
                    JsonUtility.FromJson<JxqyMediaMetadata>(
                        File.ReadAllText(path));
                AddAssetPath(
                    group,
                    ToAssetPath(path),
                    "VideoMetadata",
                    metadata.SourceStableId);
                AddAddress(
                    group,
                    metadata.OutputAddress,
                    "VideoClip",
                    metadata.SourceStableId);
            }
            manifest.Groups.Add(group);
        }

        private static void BuildDynamicTextGroup(
            JxqyPreloadManifest manifest)
        {
            var group = new JxqyPreloadGroup
            {
                Id = "shared:dynamic-text",
                Kind = "DynamicText",
                ResourceNamespace = "shared",
                OwnerStableId = "shared:dynamic-text",
                OwnerRelativePath = "ini",
            };
            string root = GetAbsoluteAssetPath(
                $"{SourceRoot}/Text/ini");
            foreach (string path in Directory.EnumerateFiles(
                         root,
                         "content.txt",
                         SearchOption.AllDirectories)
                     .OrderBy(
                         value => value,
                         StringComparer.OrdinalIgnoreCase))
            {
                string stableId = ReadTextStableId(path);
                AddAssetPath(
                    group,
                    ToAssetPath(path),
                    "DynamicText",
                    stableId);
            }
            manifest.Groups.Add(group);
        }

        private static void AddAnimation(
            JxqyPreloadGroup group,
            AnimationEntry animation)
        {
            AddAssetPath(
                group,
                animation.MetadataAssetPath,
                "AnimationMetadata",
                animation.Metadata.SourceStableId);
            foreach (string address in animation.Metadata.AtlasAddresses)
                AddAddress(
                    group,
                    address,
                    "AnimationAtlas",
                    animation.Metadata.SourceStableId);
        }

        private static void AddAssetPath(
            JxqyPreloadGroup group,
            string assetPath,
            string kind,
            string stableId)
        {
            AddAddress(
                group,
                JxqyAddressByRelativePath.CreateAddress(
                    assetPath,
                    SourceRoot),
                kind,
                stableId);
        }

        private static void AddAddress(
            JxqyPreloadGroup group,
            string address,
            string kind,
            string stableId)
        {
            if (string.IsNullOrWhiteSpace(address) ||
                group.Resources.Any(resource => string.Equals(
                    resource.Address,
                    address,
                    StringComparison.OrdinalIgnoreCase)))
                return;
            string assetPath = AddressToAssetPath(address);
            long fileBytes = 0;
            string absolute = GetAbsoluteAssetPath(assetPath);
            if (File.Exists(absolute))
                fileBytes = new FileInfo(absolute).Length;
            group.Resources.Add(new JxqyPreloadResource
            {
                Address = address,
                LogicalKey = CreateLogicalKey(
                    group,
                    kind,
                    stableId,
                    address),
                PackageName = "JxqyPackage",
                ResourceKind = kind,
                SourceStableId = stableId,
                FileBytes = fileBytes
            });
        }

        private static void FinalizeAndValidate(
            JxqyPreloadManifest manifest)
        {
            manifest.MapGroupCount = manifest.Groups.Count(group =>
                group.Kind == "Map");
            manifest.GroupCount = manifest.Groups.Count;
            foreach (JxqyPreloadGroup group in manifest.Groups)
            {
                if (string.Equals(
                        group.Kind,
                        "Map",
                        StringComparison.OrdinalIgnoreCase) &&
                    string.IsNullOrWhiteSpace(group.SceneKey))
                {
                    manifest.Errors.Add(
                        $"{group.Id}: map group has no SceneKey.");
                }
                group.Resources.Sort((left, right) =>
                    StringComparer.Ordinal.Compare(
                        left.Address,
                        right.Address));
                group.ResourceCount = group.Resources.Count;
                group.ReferencedFileBytes = group.Resources.Sum(
                    resource => resource.FileBytes);
                foreach (JxqyPreloadResource resource in group.Resources)
                {
                    if (string.IsNullOrWhiteSpace(
                            resource.LogicalKey))
                    {
                        manifest.Errors.Add(
                            $"{group.Id}: resource has no logical key: " +
                            $"{resource.Address}.");
                    }
                    if (resource.Address.IndexOf(
                            "/content/effect/",
                            StringComparison.OrdinalIgnoreCase) >= 0 &&
                        resource.Address.EndsWith(
                            ".xnb",
                            StringComparison.OrdinalIgnoreCase))
                        manifest.Errors.Add(
                            $"{group.Id}: compiled Effect XNB leaked into preload manifest: {resource.Address}.");
                    if (!File.Exists(GetAbsoluteAssetPath(
                            AddressToAssetPath(resource.Address))))
                        manifest.Errors.Add(
                            $"{group.Id}: address does not resolve to an asset: {resource.Address}.");
                }
            }
            if (manifest.MapGroupCount != 68)
                manifest.Errors.Add(
                    $"Expected 68 map groups, found {manifest.MapGroupCount}.");
            manifest.ResourceEntryCount = manifest.Groups.Sum(
                group => group.ResourceCount);
            manifest.ReferencedFileBytes = manifest.Groups.Sum(
                group => group.ReferencedFileBytes);
            string[] duplicateKeys = manifest.Groups
                .SelectMany(group => group.Resources)
                .GroupBy(
                    resource => resource.LogicalKey,
                    StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();
            foreach (string duplicateKey in duplicateKeys)
            {
                manifest.Errors.Add(
                    $"Duplicate logical resource key: {duplicateKey}.");
            }
        }

        private static string CreateLogicalKey(
            JxqyPreloadGroup group,
            string kind,
            string stableId,
            string address)
        {
            string localId =
                string.IsNullOrWhiteSpace(stableId)
                    ? address
                    : $"{stableId}/{Path.GetFileName(address)}";
            if (string.Equals(
                    group.ResourceNamespace,
                    "scene",
                    StringComparison.OrdinalIgnoreCase))
            {
                return JxqyResourceKey.Scene(
                        new JxqySceneKey(group.SceneKey),
                        kind,
                        localId)
                    .ToString();
            }
            return JxqyResourceKey.Shared(
                    group.OwnerStableId,
                    kind,
                    localId)
                .ToString();
        }

        private static string AddressToAssetPath(string address)
        {
            if (!address.StartsWith(
                    JxqyAddressByRelativePath.AddressPrefix,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Not a Jxqy YooAsset address: {address}");
            return SourceRoot + "/" +
                   address.Substring(
                       JxqyAddressByRelativePath.AddressPrefix.Length);
        }

        private static string ReadTextStableId(string contentPath)
        {
            string metadataPath = Path.Combine(
                Path.GetDirectoryName(contentPath) ?? string.Empty,
                "metadata.json");
            if (!File.Exists(metadataPath))
                throw new FileNotFoundException(
                    "Converted text metadata is missing.",
                    metadataPath);
            JxqyTextAssetMetadata metadata =
                JsonUtility.FromJson<JxqyTextAssetMetadata>(
                    File.ReadAllText(metadataPath));
            if (metadata == null ||
                string.IsNullOrWhiteSpace(metadata.SourceStableId))
            {
                throw new InvalidDataException(
                    $"Converted text metadata is invalid: {metadataPath}");
            }
            return metadata.SourceStableId;
        }

        private static string ToAssetPath(string absolutePath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                ".."));
            string full = Path.GetFullPath(absolutePath);
            if (!full.StartsWith(
                    projectRoot + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Path is outside Unity project: {absolutePath}");
            return full.Substring(projectRoot.Length + 1)
                .Replace('\\', '/');
        }

        private static string GetAbsoluteAssetPath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private sealed class AnimationEntry
        {
            public AnimationEntry(
                string metadataAssetPath,
                JxqyAnimationMetadata metadata)
            {
                MetadataAssetPath = metadataAssetPath;
                Metadata = metadata;
            }

            public string MetadataAssetPath { get; }
            public JxqyAnimationMetadata Metadata { get; }
        }

        private sealed class ImageEntry
        {
            public ImageEntry(
                string stableId,
                string assetPath,
                string address,
                string relativePath)
            {
                StableId = stableId;
                AssetPath = assetPath;
                Address = address;
                RelativePath = relativePath;
            }

            public string StableId { get; }
            public string AssetPath { get; }
            public string Address { get; }
            public string RelativePath { get; }
        }
    }
}
