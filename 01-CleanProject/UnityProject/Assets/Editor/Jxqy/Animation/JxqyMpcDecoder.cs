using System;
using System.Collections.Generic;
using System.IO;
using Jxqy.Domain.Content;

namespace Jxqy.Editor.Animation
{
    public static class JxqyMpcDecoder
    {
        private const int HeaderSize = 128;
        private const int FrameHeaderSize = 20;
        private const int MaximumPaletteColors = 256;
        private const int MaximumDimension = 8192;

        public static JxqyDecodedAnimation DecodeFile(
            string filePath,
            string shadowFilePath = null)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("MPC path is empty.", nameof(filePath));

            JxqyDecodedAnimation shadow = string.IsNullOrWhiteSpace(shadowFilePath)
                ? null
                : DecodeShd(File.ReadAllBytes(shadowFilePath));
            return Decode(File.ReadAllBytes(filePath), shadow);
        }

        public static JxqyDecodedAnimation Decode(
            byte[] data,
            JxqyDecodedAnimation shadow = null)
        {
            return DecodeCore(data, false, shadow);
        }

        public static JxqyDecodedAnimation DecodeShd(byte[] data)
        {
            return DecodeCore(data, true, null);
        }

        private static JxqyDecodedAnimation DecodeCore(
            byte[] data,
            bool shadowOnly,
            JxqyDecodedAnimation shadow)
        {
            var reader = new JxqyBinaryReader(data);
            if (reader.Length < HeaderSize)
                throw new JxqyAnimationFormatException("MPC/SHD file is smaller than its header.");

            string expectedSignature = shadowOnly ? "SHD File Ver" : "MPC File Ver";
            if (!string.Equals(
                    reader.ReadAscii(0, expectedSignature.Length),
                    expectedSignature,
                    StringComparison.Ordinal))
            {
                throw new JxqyAnimationFormatException(
                    $"Invalid {(shadowOnly ? "SHD" : "MPC")} signature.",
                    0);
            }

            int offset = 64;
            int framesDataLength = reader.ReadInt32(ref offset);
            int globalWidth = reader.ReadInt32(ref offset);
            int globalHeight = reader.ReadInt32(ref offset);
            int frameCount = reader.ReadInt32(ref offset);
            int directionCount = reader.ReadInt32(ref offset);
            int colorCount = reader.ReadInt32(ref offset);
            int interval = reader.ReadInt32(ref offset);
            int rawBottom = reader.ReadInt32(ref offset);
            ValidateHeader(
                globalWidth,
                globalHeight,
                frameCount,
                colorCount,
                shadowOnly);

            int left = globalWidth / 2;
            int bottom = globalHeight >= 16
                ? globalHeight - 16 - rawBottom
                : 16 - globalHeight - rawBottom;

            offset = HeaderSize;
            JxqyRgba32[] palette = shadowOnly
                ? Array.Empty<JxqyRgba32>()
                : ReadPalette(reader, ref offset, colorCount);
            var frameOffsets = new int[frameCount];
            int frameDataStart = checked(offset + frameCount * 4);
            reader.EnsureAvailable(offset, frameCount * 4);
            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                frameOffsets[frameIndex] = reader.ReadInt32(ref offset);
                if (frameOffsets[frameIndex] < 0)
                {
                    throw new JxqyAnimationFormatException(
                        $"{expectedSignature} frame {frameIndex} has a negative data offset.",
                        offset - 4);
                }
            }

            if (framesDataLength < 0)
            {
                throw new JxqyAnimationFormatException(
                    $"{expectedSignature} has a negative frame data length.",
                    64);
            }

            var metadata = new JxqyAnimationMetadata
            {
                Format = shadowOnly ? JxqyAnimationFormat.Shd : JxqyAnimationFormat.Mpc,
                GlobalWidth = globalWidth,
                GlobalHeight = globalHeight,
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
                JxqyDecodedFrame shadowFrame = GetCompatibleShadowFrame(
                    shadow,
                    frameIndex,
                    frameCount);
                JxqyDecodedFrame frame = DecodeFrame(
                    reader,
                    palette,
                    frameDataStart,
                    frameOffsets[frameIndex],
                    frameIndex,
                    shadowOnly,
                    shadowFrame);
                frames.Add(frame);
                JxqyAnimationMetadataFactory.AddFrame(
                    metadata,
                    frameIndex,
                    frame.Width,
                    frame.Height);
                if (shadowFrame != null)
                {
                    JxqyAnimationFrameMetadata frameMetadata = metadata.Frames[frameIndex];
                    frameMetadata.HasShadow = true;
                    frameMetadata.ShadowFrameIndex = frameIndex;
                }
            }

            return new JxqyDecodedAnimation(metadata, frames);
        }

        private static JxqyDecodedFrame DecodeFrame(
            JxqyBinaryReader reader,
            IReadOnlyList<JxqyRgba32> palette,
            int frameDataStart,
            int relativeOffset,
            int frameIndex,
            bool shadowOnly,
            JxqyDecodedFrame shadowFrame)
        {
            int frameStart = checked(frameDataStart + relativeOffset);
            int offset = frameStart;
            int dataLength = reader.ReadInt32(ref offset);
            int width = reader.ReadInt32(ref offset);
            int height = reader.ReadInt32(ref offset);
            if (dataLength < FrameHeaderSize)
            {
                throw new JxqyAnimationFormatException(
                    $"Frame {frameIndex} has invalid data length {dataLength}.",
                    frameStart);
            }
            ValidateFrameDimensions(width, height, frameIndex, offset - 8);
            reader.EnsureAvailable(frameStart, dataLength);

            offset += 8;
            int dataEnd = checked(frameStart + dataLength);
            var pixels = new JxqyRgba32[checked(width * height)];
            if (shadowFrame != null)
            {
                if (shadowFrame.Width != width || shadowFrame.Height != height)
                {
                    throw new JxqyAnimationFormatException(
                        $"Frame {frameIndex} is {width}x{height}, but its SHD frame is " +
                        $"{shadowFrame.Width}x{shadowFrame.Height}.",
                        frameStart);
                }
                Array.Copy(shadowFrame.Pixels, pixels, pixels.Length);
            }

            int pixelIndex = 0;
            while (offset < dataEnd && pixelIndex < pixels.Length)
            {
                byte run = reader.ReadByte(ref offset);
                if (run > 0x80)
                {
                    int transparentCount = run - 0x80;
                    if (pixelIndex + transparentCount > pixels.Length)
                    {
                        throw new JxqyAnimationFormatException(
                            $"Frame {frameIndex} transparent run exceeds its pixel count.",
                            offset - 1);
                    }
                    pixelIndex += transparentCount;
                    continue;
                }

                int colorCount = run;
                if (pixelIndex + colorCount > pixels.Length)
                {
                    throw new JxqyAnimationFormatException(
                        $"Frame {frameIndex} color run exceeds its pixel count.",
                        offset - 1);
                }

                if (shadowOnly)
                {
                    for (int colorIndex = 0; colorIndex < colorCount; colorIndex++)
                        pixels[pixelIndex++] = new JxqyRgba32(0, 0, 0, 153);
                    continue;
                }

                if (offset + colorCount > dataEnd)
                {
                    throw new JxqyAnimationFormatException(
                        $"Frame {frameIndex} palette indexes exceed the frame block.",
                        offset);
                }
                reader.EnsureAvailable(offset, colorCount);
                for (int colorIndex = 0; colorIndex < colorCount; colorIndex++)
                {
                    int paletteIndex = reader.ReadByte(ref offset);
                    if (paletteIndex >= palette.Count)
                    {
                        throw new JxqyAnimationFormatException(
                            $"Frame {frameIndex} uses palette index {paletteIndex}, " +
                            $"but only {palette.Count} colors exist.",
                            offset - 1);
                    }
                    pixels[pixelIndex++] = palette[paletteIndex];
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

        private static JxqyDecodedFrame GetCompatibleShadowFrame(
            JxqyDecodedAnimation shadow,
            int frameIndex,
            int expectedFrameCount)
        {
            if (shadow == null)
                return null;
            if (shadow.Metadata.Format != JxqyAnimationFormat.Shd)
                throw new ArgumentException("Shadow animation must be decoded from SHD.", nameof(shadow));
            if (shadow.Frames.Count != expectedFrameCount)
            {
                throw new JxqyAnimationFormatException(
                    $"MPC has {expectedFrameCount} frames but SHD has {shadow.Frames.Count}.");
            }
            return shadow.Frames[frameIndex];
        }

        private static void ValidateHeader(
            int width,
            int height,
            int frameCount,
            int colorCount,
            bool shadowOnly)
        {
            if (width <= 0 || width > MaximumDimension)
                throw new JxqyAnimationFormatException($"Invalid global width {width}.", 68);
            if (height <= 0 || height > MaximumDimension)
                throw new JxqyAnimationFormatException($"Invalid global height {height}.", 72);
            if (frameCount <= 0)
                throw new JxqyAnimationFormatException($"Invalid frame count {frameCount}.", 76);
            if (!shadowOnly && (colorCount <= 0 || colorCount > MaximumPaletteColors))
            {
                throw new JxqyAnimationFormatException(
                    $"Invalid MPC palette size {colorCount}.",
                    84);
            }
            if (shadowOnly && colorCount != 0)
            {
                throw new JxqyAnimationFormatException(
                    $"SHD must not declare a palette, but declares {colorCount} colors.",
                    84);
            }
        }

        private static void ValidateFrameDimensions(
            int width,
            int height,
            int frameIndex,
            int offset)
        {
            if (width <= 0 || width > MaximumDimension ||
                height <= 0 || height > MaximumDimension)
            {
                throw new JxqyAnimationFormatException(
                    $"Frame {frameIndex} has invalid size {width}x{height}.",
                    offset);
            }

            try
            {
                checked
                {
                    _ = width * height;
                }
            }
            catch (OverflowException exception)
            {
                throw new JxqyAnimationFormatException(
                    $"Frame {frameIndex} dimensions overflow: {exception.Message}",
                    offset);
            }
        }
    }
}
