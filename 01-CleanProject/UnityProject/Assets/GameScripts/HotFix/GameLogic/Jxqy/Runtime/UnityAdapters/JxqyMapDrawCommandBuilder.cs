using System;
using System.Collections.Generic;
using Jxqy.Domain.Content;
using Jxqy.Domain.World;
using Jxqy.Ports;
using UnityEngine;

namespace Jxqy.UnityAdapters
{
    public sealed class JxqyMapDrawCommandBuilder
    {
        private readonly JxqyMapMetadata _mapMetadata;
        private readonly JxqyRuntimeMapData _map;
        private readonly IReadOnlyDictionary<string, JxqyAnimationMetadata>
            _animations;

        public JxqyMapDrawCommandBuilder(
            JxqyMapMetadata mapMetadata,
            JxqyRuntimeMapData map,
            IReadOnlyDictionary<string, JxqyAnimationMetadata> animations)
        {
            _mapMetadata = mapMetadata ??
                           throw new ArgumentNullException(
                               nameof(mapMetadata));
            _map = map ??
                   throw new ArgumentNullException(nameof(map));
            _animations = animations ??
                          throw new ArgumentNullException(
                              nameof(animations));
        }

        public List<JxqyDrawCommand> Build(
            JxqyIntRect camera,
            double simulationSeconds,
            Color drawColor,
            int playerTileRow = int.MaxValue)
        {
            var result = new List<JxqyDrawCommand>();
            Build(
                camera,
                simulationSeconds,
                drawColor,
                result,
                useWorldPositions: false,
                playerTileRow);
            return result;
        }

        public void BuildWorld(
            JxqyIntRect camera,
            double simulationSeconds,
            Color drawColor,
            List<JxqyDrawCommand> result,
            int playerTileRow = int.MaxValue)
        {
            Build(
                camera,
                simulationSeconds,
                drawColor,
                result,
                useWorldPositions: true,
                playerTileRow);
        }

        private void Build(
            JxqyIntRect camera,
            double simulationSeconds,
            Color drawColor,
            List<JxqyDrawCommand> result,
            bool useWorldPositions,
            int playerTileRow = int.MaxValue)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            // Original Jxqy treats exact black as a sentinel that switches
            // map layers to its grayscale effect. It is not a black vertex
            // tint; actors continue to use the independently configured ASF
            // color. Keep this rule shared by every scripted map transition.
            bool usesLegacyGrayscale = IsLegacyGrayscale(drawColor);
            Color mapDrawColor = usesLegacyGrayscale
                ? Color.white
                : drawColor;
            JxqyTileRange visible =
                JxqyIsometricMapMath.CalculateVisibleTileRange(
                    camera,
                    _map.Columns,
                    _map.Rows);
            result.Clear();
            for (int layer = 0; layer < 3; layer++)
            {
                for (int row = visible.StartRow;
                     row < visible.EndRowExclusive;
                     row++)
                {
                    for (int column = visible.StartColumn;
                         column < visible.EndColumnExclusive;
                         column++)
                    {
                        JxqyRuntimeMapTile tile =
                            _map.GetTile(column, row);
                        byte mpcNumber = tile.GetMpc(layer);
                        if (mpcNumber == 0)
                            continue;
                        int mpcIndex = mpcNumber - 1;
                        if (mpcIndex < 0 ||
                            mpcIndex >= _mapMetadata.MpcTable.Count)
                        {
                            continue;
                        }
                        JxqyMapMpcMetadata entry =
                            _mapMetadata.MpcTable[mpcIndex];
                        if (string.IsNullOrWhiteSpace(entry.FileName))
                            continue;
                        string stableId = CreateMpcStableId(
                            _mapMetadata.MpcDirectory,
                            entry.FileName);
                        if (!_animations.TryGetValue(
                                stableId,
                                out JxqyAnimationMetadata animation))
                        {
                            continue;
                        }
                        JxqyAnimationFrameMetadata frame = SelectFrame(
                            animation,
                            tile.GetFrame(layer),
                            entry.IsLooping,
                            simulationSeconds);
                        if (frame == null ||
                            frame.AtlasPage < 0 ||
                            frame.AtlasPage >=
                            animation.AtlasAddresses.Count)
                        {
                            continue;
                        }
                        JxqyIntPoint world =
                            JxqyIsometricMapMath.TileToWorldPixel(
                                column,
                                row);
                        JxqyIntPoint view =
                            JxqyIsometricMapMath.WorldToView(
                                world,
                                camera);
                        Vector2 position = useWorldPositions
                            ? new Vector2(world.X, world.Y)
                            : new Vector2(view.X, view.Y);
                        int tileOrder =
                            row * _map.Columns + column;
                        int depth = layer switch
                        {
                            0 => tileOrder,
                            1 => JxqyWorldDepth.RowInterleavedBase +
                                 tileOrder * 10,
                            // The legacy renderer draws the complete third
                            // map layer after row-interleaved NPCs, objects and
                            // magic. Do not infer exceptions from MPC names:
                            // one building is commonly split across differently
                            // named records, which would interleave its pixels
                            // with a single actor or death animation.
                            2 => JxqyWorldDepth.UpperMapBase + tileOrder,
                            _ => tileOrder
                        };
                        bool writesPlayerOcclusion =
                            layer == 2 ||
                            layer == 1 && row > playerTileRow;
                        result.Add(new JxqyDrawCommand(
                            animation.AtlasAddresses[frame.AtlasPage],
                            new Rect(
                                frame.AtlasX,
                                frame.AtlasY,
                                frame.AtlasWidth,
                                frame.AtlasHeight),
                            position,
                            new Vector2(
                                frame.GetMapAnchorX(),
                                frame.GetMapAnchorY()),
                            mapDrawColor,
                            depth,
                            writesPlayerOcclusion
                                ? usesLegacyGrayscale
                                    ? "occluder-grayscale"
                                    : "occluder"
                                : usesLegacyGrayscale
                                    ? "grayscale"
                                    : "default"));
                    }
                }
            }
        }

        private static bool IsLegacyGrayscale(Color drawColor)
        {
            return drawColor.r == 0f &&
                   drawColor.g == 0f &&
                   drawColor.b == 0f;
        }

        public static string CreateMpcStableId(
            string directory,
            string fileName)
        {
            string path =
                $"{directory.Trim('/', '\\')}/{fileName.TrimStart('/', '\\')}"
                    .Replace('\\', '/')
                    .ToLowerInvariant();
            return "mpc:" + path;
        }

        private static JxqyAnimationFrameMetadata SelectFrame(
            JxqyAnimationMetadata animation,
            int initialFrame,
            bool looping,
            double simulationSeconds)
        {
            int frameIndex = initialFrame;
            if (looping && animation.FrameCount > 0)
            {
                int interval = Math.Max(
                    1,
                    animation.IntervalMilliseconds);
                long elapsedFrames = (long)Math.Floor(
                    simulationSeconds * 1000.0 / interval);
                frameIndex = (int)(
                    (initialFrame + elapsedFrames) %
                    animation.FrameCount);
            }
            for (int index = 0; index < animation.Frames.Count; index++)
            {
                JxqyAnimationFrameMetadata frame =
                    animation.Frames[index];
                if (frame.SourceFrameIndex == frameIndex)
                    return frame;
            }
            return null;
        }
    }
}
