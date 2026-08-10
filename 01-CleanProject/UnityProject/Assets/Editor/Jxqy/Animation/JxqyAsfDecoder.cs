using System;
using System.Collections.Generic;
using System.IO;
using Jxqy.Domain.Content;

namespace Jxqy.Editor.Animation
{
    public static class JxqyAsfDecoder
    {
        private const int HeaderSize = 64;
        private const int MaximumPaletteColors = 256;
        private const int MaximumDimension = 8192;

        public static JxqyDecodedAnimation DecodeFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("ASF path is empty.", nameof(filePath));
            return Decode(File.ReadAllBytes(filePath));
        }

        public static JxqyDecodedAnimation Decode(byte[] data)
        {
            var reader = new JxqyBinaryReader(data);
            if (reader.Length < HeaderSize)
                throw new JxqyAnimationFormatException("ASF file is smaller than its header.");
            if (!string.Equals(reader.ReadAscii(0, 7), "ASF 1.0", StringComparison.Ordinal))
                throw new JxqyAnimationFormatException("Invalid ASF signature.", 0);

            int offset = 16;
            int width = reader.ReadInt32(ref offset);
            int height = reader.ReadInt32(ref offset);
            int frameCount = reader.ReadInt32(ref offset);
            int directionCount = reader.ReadInt32(ref offset);
            int colorCount = reader.ReadInt32(ref offset);
            int interval = reader.ReadInt32(ref offset);
            int left = reader.ReadInt32(ref offset);
            int bottom = reader.ReadInt32(ref offset);
            ValidateHeader(width, height, frameCount, colorCount);

            offset = HeaderSize;
            JxqyRgba32[] palette = ReadPalette(reader, ref offset, colorCount);
            var frameOffsets = new int[frameCount];
            var frameLengths = new int[frameCount];
            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                frameOffsets[frameIndex] = reader.ReadInt32(ref offset);
                frameLengths[frameIndex] = reader.ReadInt32(ref offset);
                if (frameOffsets[frameIndex] < 0 || frameLengths[frameIndex] < 0)
                {
                    throw new JxqyAnimationFormatException(
                        $"ASF frame {frameIndex} has a negative offset or length.",
                        offset - 8);
                }
                reader.EnsureAvailable(frameOffsets[frameIndex], frameLengths[frameIndex]);
            }

            var metadata = new JxqyAnimationMetadata
            {
                Format = JxqyAnimationFormat.Asf,
                GlobalWidth = width,
                GlobalHeight = height,
                FrameCount = frameCount,
                DirectionCount = directionCount,
                IntervalMilliseconds = interval,
                AnchorLeft = left,
                AnchorBottom = bottom,
                UsesStraightAlpha = true
            };
            JxqyAnimationMetadataFactory.PopulateFrames(metadata);

            var frames = new List<JxqyDecodedFrame>(frameCount);
            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                frames.Add(DecodeFrame(
                    reader,
                    palette,
                    width,
                    height,
                    frameOffsets[frameIndex],
                    frameLengths[frameIndex],
                    frameIndex));
                JxqyAnimationMetadataFactory.AddFrame(metadata, frameIndex, width, height);
            }

            return new JxqyDecodedAnimation(metadata, frames);
        }

        private static JxqyDecodedFrame DecodeFrame(
            JxqyBinaryReader reader,
            IReadOnlyList<JxqyRgba32> palette,
            int width,
            int height,
            int offset,
            int length,
            int frameIndex)
        {
            var pixels = new JxqyRgba32[checked(width * height)];
            int end = checked(offset + length);
            int pixelIndex = 0;
            while (offset < end && pixelIndex < pixels.Length)
            {
                if (end - offset < 2)
                {
                    throw new JxqyAnimationFormatException(
                        $"ASF frame {frameIndex} ends inside an RLE run.",
                        offset);
                }

                byte pixelCount = reader.ReadByte(ref offset);
                byte alpha = reader.ReadByte(ref offset);
                if (pixelIndex + pixelCount > pixels.Length)
                {
                    throw new JxqyAnimationFormatException(
                        $"ASF frame {frameIndex} RLE expands beyond {pixels.Length} pixels.",
                        offset - 2);
                }

                if (alpha == 0)
                {
                    pixelIndex += pixelCount;
                    continue;
                }

                reader.EnsureAvailable(offset, pixelCount);
                if (offset + pixelCount > end)
                {
                    throw new JxqyAnimationFormatException(
                        $"ASF frame {frameIndex} palette indexes exceed the frame block.",
                        offset);
                }

                for (int runIndex = 0; runIndex < pixelCount; runIndex++)
                {
                    int paletteIndex = reader.ReadByte(ref offset);
                    if (paletteIndex >= palette.Count)
                    {
                        throw new JxqyAnimationFormatException(
                            $"ASF frame {frameIndex} uses palette index {paletteIndex}, " +
                            $"but only {palette.Count} colors exist.",
                            offset - 1);
                    }

                    JxqyRgba32 color = palette[paletteIndex];
                    pixels[pixelIndex++] = new JxqyRgba32(color.R, color.G, color.B, alpha);
                }
            }

            return new JxqyDecodedFrame(width, height, pixels);
        }

        private static JxqyRgba32[] ReadPalette(
            JxqyBinaryReader reader,
            ref int offset,
            int colorCount)
        {
            var palette = new JxqyRgba32[colorCount];
            for (int colorIndex = 0; colorIndex < colorCount; colorIndex++)
            {
                byte blue = reader.ReadByte(ref offset);
                byte green = reader.ReadByte(ref offset);
                byte red = reader.ReadByte(ref offset);
                reader.ReadByte(ref offset);
                palette[colorIndex] = new JxqyRgba32(red, green, blue, byte.MaxValue);
            }
            return palette;
        }

        private static void ValidateHeader(
            int width,
            int height,
            int frameCount,
            int colorCount)
        {
            if (width <= 0 || width > MaximumDimension)
                throw new JxqyAnimationFormatException($"Invalid ASF width {width}.", 16);
            if (height <= 0 || height > MaximumDimension)
                throw new JxqyAnimationFormatException($"Invalid ASF height {height}.", 20);
            if (frameCount <= 0)
                throw new JxqyAnimationFormatException($"Invalid ASF frame count {frameCount}.", 24);
            if (colorCount <= 0 || colorCount > MaximumPaletteColors)
            {
                throw new JxqyAnimationFormatException(
                    $"Invalid ASF palette size {colorCount}.",
                    32);
            }

            try
            {
                checked
                {
                    _ = width * height;
                    _ = frameCount * 8;
                    _ = colorCount * 4;
                }
            }
            catch (OverflowException exception)
            {
                throw new JxqyAnimationFormatException(
                    $"ASF dimensions overflow supported ranges: {exception.Message}");
            }
        }
    }
}
