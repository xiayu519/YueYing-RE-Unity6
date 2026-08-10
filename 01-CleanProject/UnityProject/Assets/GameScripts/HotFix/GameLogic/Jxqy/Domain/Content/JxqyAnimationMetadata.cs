using System;
using System.Collections.Generic;

namespace Jxqy.Domain.Content
{
    public enum JxqyAnimationFormat
    {
        Unknown = 0,
        Asf = 1,
        Mpc = 2,
        Shd = 3
    }

    [Serializable]
    public sealed class JxqyAnimationMetadata
    {
        public int SchemaVersion = 1;
        public string ConverterVersion = string.Empty;
        public string SourceStableId = string.Empty;
        public string SourceRelativePath = string.Empty;
        public string SourceAddress = string.Empty;
        public string SourceSha256 = string.Empty;
        public JxqyAnimationFormat Format;
        public int GlobalWidth;
        public int GlobalHeight;
        public int FrameCount;
        public int DirectionCount;
        public int FramesPerDirection;
        public int IntervalMilliseconds;
        public int AnchorLeft;
        public int AnchorBottom;
        public bool UsesStraightAlpha = true;
        public List<string> AtlasAddresses = new();
        public List<JxqyAnimationDirectionMetadata> Directions = new();
        public List<JxqyAnimationFrameMetadata> Frames = new();
    }

    [Serializable]
    public sealed class JxqyAnimationDirectionMetadata
    {
        public int DirectionIndex;
        public int FirstFrameIndex;
        public int FrameCount;
    }

    [Serializable]
    public sealed class JxqyAnimationFrameMetadata
    {
        public int SourceFrameIndex;
        public int DirectionIndex;
        public int AnimationFrameIndex;
        public int PixelWidth;
        public int PixelHeight;
        public int DurationMilliseconds;
        public int AnchorX;
        public int AnchorY;
        public bool HasShadow;
        public int ShadowFrameIndex = -1;
        public int AtlasPage = -1;
        public int AtlasX;
        public int AtlasY;
        public int AtlasWidth;
        public int AtlasHeight;
        public int TrimLeft;
        public int TrimTop;
        public int TrimBottom;
        public bool Rotated;

        public int EffectiveTrimTop
        {
            get
            {
                if (TrimTop > 0)
                    return TrimTop;
                return Math.Max(
                    0,
                    PixelHeight - AtlasHeight - TrimBottom);
            }
        }

        public int GetAtlasAnchorX(int untrimmedAnchorX)
        {
            return untrimmedAnchorX - TrimLeft;
        }

        public int GetAtlasAnchorY(int untrimmedAnchorY)
        {
            return untrimmedAnchorY - EffectiveTrimTop;
        }

        public int GetMapAnchorX()
        {
            return PixelWidth / 2 - TrimLeft;
        }

        public int GetMapAnchorY()
        {
            return PixelHeight - 16 - EffectiveTrimTop;
        }
    }
}
