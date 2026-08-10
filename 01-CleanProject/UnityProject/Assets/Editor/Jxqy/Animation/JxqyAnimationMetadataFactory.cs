using Jxqy.Domain.Content;

namespace Jxqy.Editor.Animation
{
    internal static class JxqyAnimationMetadataFactory
    {
        public static void PopulateFrames(JxqyAnimationMetadata metadata)
        {
            int directionCount = metadata.DirectionCount;
            if (directionCount <= 0 || metadata.FrameCount < directionCount)
                directionCount = 1;

            metadata.DirectionCount = directionCount;
            metadata.FramesPerDirection = directionCount == 0
                ? metadata.FrameCount
                : metadata.FrameCount / directionCount;
            if (metadata.FramesPerDirection < 1)
                metadata.FramesPerDirection = 1;

            metadata.Directions.Clear();
            metadata.Frames.Clear();
            for (int direction = 0; direction < directionCount; direction++)
            {
                int firstFrame = direction * metadata.FramesPerDirection;
                int remaining = metadata.FrameCount - firstFrame;
                int frameCount = remaining <= 0
                    ? 0
                    : System.Math.Min(metadata.FramesPerDirection, remaining);
                metadata.Directions.Add(new JxqyAnimationDirectionMetadata
                {
                    DirectionIndex = direction,
                    FirstFrameIndex = firstFrame,
                    FrameCount = frameCount
                });
            }
        }

        public static void AddFrame(
            JxqyAnimationMetadata metadata,
            int frameIndex,
            int width,
            int height)
        {
            int framesPerDirection = metadata.FramesPerDirection < 1
                ? 1
                : metadata.FramesPerDirection;
            int directionIndex = System.Math.Min(
                metadata.DirectionCount - 1,
                frameIndex / framesPerDirection);
            metadata.Frames.Add(new JxqyAnimationFrameMetadata
            {
                SourceFrameIndex = frameIndex,
                DirectionIndex = System.Math.Max(0, directionIndex),
                AnimationFrameIndex = frameIndex % framesPerDirection,
                PixelWidth = width,
                PixelHeight = height,
                DurationMilliseconds = System.Math.Max(1, metadata.IntervalMilliseconds),
                AnchorX = metadata.AnchorLeft,
                AnchorY = metadata.AnchorBottom,
                AtlasWidth = width,
                AtlasHeight = height
            });
        }
    }
}
