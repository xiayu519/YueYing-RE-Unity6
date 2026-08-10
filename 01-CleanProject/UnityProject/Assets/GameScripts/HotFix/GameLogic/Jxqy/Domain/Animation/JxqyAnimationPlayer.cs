using System;
using System.Linq;
using Jxqy.Domain.Content;

namespace Jxqy.Domain.Animation
{
    public sealed class JxqyAnimationPlayer
    {
        private readonly JxqyAnimationMetadata _metadata;
        private int _direction;
        private int _frameWithinDirection;
        private double _frameElapsedMilliseconds;

        public JxqyAnimationPlayer(JxqyAnimationMetadata metadata)
        {
            _metadata = metadata ??
                        throw new ArgumentNullException(nameof(metadata));
            if (metadata.Frames == null || metadata.Frames.Count == 0)
                throw new ArgumentException(
                    "Animation contains no frames.",
                    nameof(metadata));
            if (metadata.Directions == null ||
                metadata.Directions.Count == 0)
                throw new ArgumentException(
                    "Animation contains no directions.",
                    nameof(metadata));
        }

        public bool IsLooping { get; set; } = true;
        public bool IsReversed { get; set; }
        public bool IsFinished { get; private set; }
        public int Direction => _direction;
        public int FrameWithinDirection => _frameWithinDirection;
        public JxqyAnimationMetadata Metadata => _metadata;

        public void SetDirection(int direction)
        {
            int count = Math.Max(1, _metadata.DirectionCount);
            int normalized = direction % count;
            if (normalized < 0)
                normalized += count;
            _direction = normalized;
            JxqyAnimationDirectionMetadata info = GetDirection();
            if (_frameWithinDirection >= info.FrameCount)
                _frameWithinDirection = 0;
        }

        public void Restart()
        {
            _frameWithinDirection = IsReversed
                ? Math.Max(0, GetDirection().FrameCount - 1)
                : 0;
            _frameElapsedMilliseconds = 0;
            IsFinished = false;
        }

        public void SeekFrame(int frameWithinDirection)
        {
            JxqyAnimationDirectionMetadata direction = GetDirection();
            _frameWithinDirection = Math.Max(
                0,
                Math.Min(
                    frameWithinDirection,
                    direction.FrameCount - 1));
            _frameElapsedMilliseconds = 0;
            IsFinished = false;
        }

        public void PlayForward()
        {
            IsReversed = false;
            Restart();
        }

        public void PlayReverse()
        {
            IsReversed = true;
            Restart();
        }

        public void Advance(double elapsedSeconds)
        {
            if (elapsedSeconds < 0 || double.IsNaN(elapsedSeconds) ||
                double.IsInfinity(elapsedSeconds))
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            if (IsFinished)
                return;
            _frameElapsedMilliseconds += elapsedSeconds * 1000.0;
            JxqyAnimationDirectionMetadata direction = GetDirection();
            if (IsLooping)
            {
                double cycleMilliseconds = 0;
                int originalFrame = _frameWithinDirection;
                for (int index = 0; index < direction.FrameCount; index++)
                {
                    _frameWithinDirection = index;
                    cycleMilliseconds += Math.Max(
                        1,
                        CurrentFrame.DurationMilliseconds);
                }
                _frameWithinDirection = originalFrame;
                if (cycleMilliseconds > 0 &&
                    _frameElapsedMilliseconds >= cycleMilliseconds)
                {
                    _frameElapsedMilliseconds %= cycleMilliseconds;
                }
            }
            int safety = Math.Max(1, direction.FrameCount) + 1;
            while (safety-- > 0)
            {
                JxqyAnimationFrameMetadata frame = CurrentFrame;
                int duration = Math.Max(1, frame.DurationMilliseconds);
                if (_frameElapsedMilliseconds + 1e-9 < duration)
                    break;
                _frameElapsedMilliseconds -= duration;
                _frameWithinDirection += IsReversed ? -1 : 1;
                if (_frameWithinDirection >= 0 &&
                    _frameWithinDirection < direction.FrameCount)
                    continue;
                if (IsLooping)
                {
                    _frameWithinDirection = IsReversed
                        ? Math.Max(0, direction.FrameCount - 1)
                        : 0;
                }
                else
                {
                    _frameWithinDirection = IsReversed
                        ? 0
                        : Math.Max(0, direction.FrameCount - 1);
                    _frameElapsedMilliseconds = 0;
                    IsFinished = true;
                    break;
                }
            }
        }

        public JxqyAnimationFrameMetadata CurrentFrame
        {
            get
            {
                JxqyAnimationDirectionMetadata direction = GetDirection();
                int sourceIndex = direction.FirstFrameIndex +
                                  Math.Min(
                                      _frameWithinDirection,
                                      Math.Max(0, direction.FrameCount - 1));
                JxqyAnimationFrameMetadata exact = _metadata.Frames
                    .FirstOrDefault(frame =>
                        frame.SourceFrameIndex == sourceIndex);
                if (exact == null)
                    throw new InvalidOperationException(
                        $"Animation frame {sourceIndex} is missing.");
                return exact;
            }
        }

        public JxqyAnimationPose GetPose()
        {
            JxqyAnimationFrameMetadata frame = CurrentFrame;
            if (frame.AtlasPage < 0 ||
                frame.AtlasPage >= _metadata.AtlasAddresses.Count)
                throw new InvalidOperationException(
                    $"Animation frame {frame.SourceFrameIndex} has invalid atlas page {frame.AtlasPage}.");
            int anchorX = _metadata.GlobalWidth > 0 &&
                          frame.PixelWidth > 0
                ? frame.GetAtlasAnchorX(_metadata.AnchorLeft)
                : frame.AnchorX;
            int anchorY = _metadata.GlobalHeight > 0 &&
                          frame.PixelHeight > 0
                ? frame.GetAtlasAnchorY(_metadata.AnchorBottom)
                : frame.AnchorY;
            return new JxqyAnimationPose(
                _metadata.AtlasAddresses[frame.AtlasPage],
                frame.AtlasX,
                frame.AtlasY,
                frame.AtlasWidth,
                frame.AtlasHeight,
                anchorX,
                anchorY,
                frame.HasShadow,
                frame.ShadowFrameIndex);
        }

        private JxqyAnimationDirectionMetadata GetDirection()
        {
            JxqyAnimationDirectionMetadata info = _metadata.Directions
                .FirstOrDefault(direction =>
                    direction.DirectionIndex == _direction);
            if (info == null || info.FrameCount <= 0)
                throw new InvalidOperationException(
                    $"Animation direction {_direction} is missing or empty.");
            return info;
        }
    }

    public readonly struct JxqyAnimationPose
    {
        public JxqyAnimationPose(
            string atlasAddress,
            int atlasX,
            int atlasY,
            int width,
            int height,
            int anchorX,
            int anchorY,
            bool hasShadow,
            int shadowFrameIndex)
        {
            AtlasAddress = atlasAddress;
            AtlasX = atlasX;
            AtlasY = atlasY;
            Width = width;
            Height = height;
            AnchorX = anchorX;
            AnchorY = anchorY;
            HasShadow = hasShadow;
            ShadowFrameIndex = shadowFrameIndex;
        }

        public string AtlasAddress { get; }
        public int AtlasX { get; }
        public int AtlasY { get; }
        public int Width { get; }
        public int Height { get; }
        public int AnchorX { get; }
        public int AnchorY { get; }
        public bool HasShadow { get; }
        public int ShadowFrameIndex { get; }
    }
}
