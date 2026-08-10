using System;
using System.Collections.Generic;
using Jxqy.Domain.Content;

namespace Jxqy.Editor.Animation.Atlas
{
    public sealed class JxqyAtlasPackSettings
    {
        public int MaximumPageSize = 4096;
        public int MinimumPageSize = 4;
        public int Extrude = 2;
        public byte AlphaThreshold;

        public void Validate()
        {
            if (MaximumPageSize < 4 || (MaximumPageSize & (MaximumPageSize - 1)) != 0)
                throw new ArgumentOutOfRangeException(nameof(MaximumPageSize));
            if (MinimumPageSize < 1 || MinimumPageSize > MaximumPageSize)
                throw new ArgumentOutOfRangeException(nameof(MinimumPageSize));
            if (Extrude < 0 || Extrude > 16)
                throw new ArgumentOutOfRangeException(nameof(Extrude));
        }
    }

    public sealed class JxqyAtlasFrameInput
    {
        public JxqyAtlasFrameInput(
            string key,
            int frameIndex,
            JxqyDecodedFrame frame,
            JxqyAnimationFrameMetadata metadata)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Atlas input key is empty.", nameof(key));
            Key = key;
            FrameIndex = frameIndex;
            Frame = frame ?? throw new ArgumentNullException(nameof(frame));
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        }

        public string Key { get; }
        public int FrameIndex { get; }
        public JxqyDecodedFrame Frame { get; }
        public JxqyAnimationFrameMetadata Metadata { get; }
    }

    public sealed class JxqyAtlasPlacement
    {
        public string Key;
        public int FrameIndex;
        public int PageIndex;
        public int X;
        public int Y;
        public int Width;
        public int Height;
        public int ContentX;
        public int ContentY;
        public int ContentWidth;
        public int ContentHeight;
        public int TrimLeft;
        public int TrimTop;
        public int TrimBottom;
    }

    public sealed class JxqyAtlasPage
    {
        public int PageIndex;
        public int Width;
        public int Height;
        public JxqyRgba32[] Pixels = Array.Empty<JxqyRgba32>();
        public List<JxqyAtlasPlacement> Placements = new();
    }

    public sealed class JxqyAtlasPackResult
    {
        public List<JxqyAtlasPage> Pages = new();
        public List<JxqyAtlasPlacement> Placements = new();
        public long SourcePixelCount;
        public long TrimmedPixelCount;
        public long AtlasPixelCount;
    }

    internal sealed class JxqyTrimmedFrame
    {
        public JxqyAtlasFrameInput Input;
        public int TrimLeft;
        public int TrimTop;
        public int TrimBottom;
        public int Width;
        public int Height;
        public JxqyRgba32[] Pixels;
    }
}
