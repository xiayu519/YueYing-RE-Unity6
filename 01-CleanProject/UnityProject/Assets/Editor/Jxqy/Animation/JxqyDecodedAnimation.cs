using System;
using System.Collections.Generic;
using Jxqy.Domain.Content;

namespace Jxqy.Editor.Animation
{
    public readonly struct JxqyRgba32 : IEquatable<JxqyRgba32>
    {
        public JxqyRgba32(byte red, byte green, byte blue, byte alpha)
        {
            R = red;
            G = green;
            B = blue;
            A = alpha;
        }

        public byte R { get; }
        public byte G { get; }
        public byte B { get; }
        public byte A { get; }

        public bool Equals(JxqyRgba32 other)
        {
            return R == other.R && G == other.G && B == other.B && A == other.A;
        }

        public override bool Equals(object value)
        {
            return value is JxqyRgba32 other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = R;
                hash = (hash * 397) ^ G;
                hash = (hash * 397) ^ B;
                hash = (hash * 397) ^ A;
                return hash;
            }
        }
    }

    public sealed class JxqyDecodedFrame
    {
        public JxqyDecodedFrame(int width, int height, JxqyRgba32[] pixels)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height));
            if (pixels == null)
                throw new ArgumentNullException(nameof(pixels));
            if (pixels.Length != checked(width * height))
                throw new ArgumentException("Pixel count does not match the frame size.", nameof(pixels));

            Width = width;
            Height = height;
            Pixels = pixels;
        }

        public int Width { get; }
        public int Height { get; }
        public JxqyRgba32[] Pixels { get; }
    }

    public sealed class JxqyDecodedAnimation
    {
        public JxqyDecodedAnimation(
            JxqyAnimationMetadata metadata,
            IReadOnlyList<JxqyDecodedFrame> frames)
        {
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            Frames = frames ?? throw new ArgumentNullException(nameof(frames));
            if (Metadata.FrameCount != Frames.Count)
                throw new ArgumentException("Metadata frame count does not match decoded frames.", nameof(frames));
        }

        public JxqyAnimationMetadata Metadata { get; }
        public IReadOnlyList<JxqyDecodedFrame> Frames { get; }
    }
}
