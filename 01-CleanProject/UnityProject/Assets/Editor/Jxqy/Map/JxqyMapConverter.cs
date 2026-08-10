using System;
using System.IO;
using System.Linq;
using Jxqy.Domain.Content;
using Jxqy.Editor.Animation.Conversion;
using Jxqy.Editor.Scanning;
using Jxqy.Editor.YooAsset;
using UnityEditor;
using UnityEngine;

namespace Jxqy.Editor.Map
{
    public sealed class JxqyMapConverter
    {
        public const string MapConverterVersion = "0.1.0-map-1";
        public const string ConvertedStatus = "Converted";
        public const string ReusedStatus = "Reused";
        public const string FailedStatus = "Failed";

        public JxqyMapConversionFileReport Convert(
            JxqySourceFileRecord source,
            string sourceRoot,
            string outputRoot)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (source.Kind != JxqyFileKind.Map)
                throw new ArgumentException("Source is not a MAP file.", nameof(source));

            string normalizedOutput = outputRoot.Replace('\\', '/').TrimEnd('/');
            string relative = source.RelativePath.Replace('\\', '/').TrimStart('/');
            string outputDirectory = $"{normalizedOutput}/Maps/{relative}";
            string metadataAssetPath = outputDirectory + "/map.json";
            string dataAssetPath = outputDirectory + "/map.bytes";

            string sourcePath = Path.GetFullPath(Path.Combine(
                sourceRoot,
                relative.Replace('/', Path.DirectorySeparatorChar)));
            var before = new FileInfo(sourcePath);
            if (!before.Exists)
                throw new FileNotFoundException("MAP source is missing.", sourcePath);
            if (before.Length != source.Size ||
                before.LastWriteTimeUtc.Ticks != source.LastWriteUtcTicks)
            {
                throw new IOException(
                    $"Source changed after manifest scan: {source.RelativePath}.");
            }

            if (CanReuse(metadataAssetPath, dataAssetPath, source))
            {
                return CreateReport(
                    source,
                    metadataAssetPath,
                    dataAssetPath,
                    JxqyMapParser.Parse(File.ReadAllBytes(GetAbsoluteAssetPath(dataAssetPath))),
                    ReusedStatus);
            }

            byte[] sourceBytes = File.ReadAllBytes(sourcePath);
            var after = new FileInfo(sourcePath);
            if (before.Length != after.Length ||
                before.LastWriteTimeUtc.Ticks != after.LastWriteTimeUtc.Ticks)
            {
                throw new IOException($"MAP source changed while reading: {source.RelativePath}.");
            }

            JxqyMapFile map = JxqyMapParser.Parse(sourceBytes);
            byte[] roundTrip = JxqyMapParser.EncodeOriginal(map);
            if (!sourceBytes.SequenceEqual(roundTrip))
            {
                throw new JxqyMapFormatException(
                    $"MAP parser round trip changed bytes: {source.RelativePath}.");
            }

            WriteBytesAsset(dataAssetPath, roundTrip);
            JxqyMapMetadata metadata = CreateMetadata(
                source,
                map,
                JxqyAddressByRelativePath.CreateAddress(
                    dataAssetPath,
                    normalizedOutput));
            JxqyAnimationConverter.WriteJsonAsset(
                metadataAssetPath,
                metadata,
                true);
            return CreateReport(
                source,
                metadataAssetPath,
                dataAssetPath,
                map,
                ConvertedStatus);
        }

        private static JxqyMapMetadata CreateMetadata(
            JxqySourceFileRecord source,
            JxqyMapFile map,
            string dataAddress)
        {
            var metadata = new JxqyMapMetadata
            {
                ConverterVersion = MapConverterVersion,
                SourceStableId = source.StableId,
                SourceRelativePath = source.RelativePath,
                SourceAddress = source.SourceAddress,
                SourceSha256 = source.Sha256,
                MpcDirectory = map.MpcDirectory.Replace('\\', '/'),
                ColumnCount = map.ColumnCount,
                RowCount = map.RowCount,
                TileWidth = map.TileWidth,
                TileHeight = map.TileHeight,
                MapPixelWidth = checked((map.ColumnCount - 1) * 64),
                MapPixelHeight = checked(((map.RowCount - 3) / 2 + 1) * 32),
                DataAddress = dataAddress
            };
            foreach (JxqyMapMpcEntry entry in map.MpcEntries)
            {
                metadata.MpcTable.Add(new JxqyMapMpcMetadata
                {
                    Index = entry.Index,
                    FileName = entry.FileName.Replace('\\', '/'),
                    IsLooping = entry.IsLooping,
                    RawRecordBase64 = System.Convert.ToBase64String(entry.RawRecord)
                });
            }
            return metadata;
        }

        private static JxqyMapConversionFileReport CreateReport(
            JxqySourceFileRecord source,
            string metadataAssetPath,
            string dataAssetPath,
            JxqyMapFile map,
            string status)
        {
            return new JxqyMapConversionFileReport
            {
                RelativePath = source.RelativePath,
                StableId = source.StableId,
                Status = status,
                OutputMetadataAssetPath = metadataAssetPath,
                OutputDataAssetPath = dataAssetPath,
                ColumnCount = map.ColumnCount,
                RowCount = map.RowCount,
                TileCount = map.Tiles.Length,
                UsedMpcCount = map.MpcEntries.Count(entry => !string.IsNullOrEmpty(entry.FileName)),
                LoopingMpcCount = map.MpcEntries.Count(entry => entry.IsLooping),
                Layer1TileCount = map.Tiles.Count(tile => tile.Layer1MpcIndex != 0),
                Layer2TileCount = map.Tiles.Count(tile => tile.Layer2MpcIndex != 0),
                Layer3TileCount = map.Tiles.Count(tile => tile.Layer3MpcIndex != 0),
                BarrierTileCount = map.Tiles.Count(tile => tile.BarrierType != 0),
                TrapTileCount = map.Tiles.Count(tile => tile.TrapIndex != 0)
            };
        }

        private static bool CanReuse(
            string metadataAssetPath,
            string dataAssetPath,
            JxqySourceFileRecord source)
        {
            string metadataPath = GetAbsoluteAssetPath(metadataAssetPath);
            if (!File.Exists(metadataPath) ||
                !File.Exists(GetAbsoluteAssetPath(dataAssetPath)))
            {
                return false;
            }
            try
            {
                JxqyMapMetadata metadata = JsonUtility.FromJson<JxqyMapMetadata>(
                    File.ReadAllText(metadataPath));
                return metadata != null &&
                       metadata.ConverterVersion == MapConverterVersion &&
                       metadata.SourceStableId == source.StableId &&
                       metadata.SourceSha256 == source.Sha256 &&
                       metadata.MpcTable?.Count == JxqyMapParser.MpcEntryCount;
            }
            catch
            {
                return false;
            }
        }

        private static void WriteBytesAsset(string assetPath, byte[] bytes)
        {
            string absolutePath = GetAbsoluteAssetPath(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
            string temporaryPath = absolutePath + ".tmp";
            string backupPath = absolutePath + ".bak";
            File.WriteAllBytes(temporaryPath, bytes);
            if (File.Exists(absolutePath))
            {
                File.Replace(temporaryPath, absolutePath, backupPath);
                if (File.Exists(backupPath))
                    File.Delete(backupPath);
            }
            else
            {
                File.Move(temporaryPath, absolutePath);
            }
            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceSynchronousImport);
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
