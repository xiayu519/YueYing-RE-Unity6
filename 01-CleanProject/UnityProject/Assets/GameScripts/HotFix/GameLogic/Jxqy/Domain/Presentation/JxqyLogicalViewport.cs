using System;
using Jxqy.Domain.World;

namespace Jxqy.Domain.Presentation
{
    public static class JxqyLogicalViewport
    {
        public const int OriginalWidth = 800;
        public const int OriginalHeight = 600;

        public static JxqyViewportLayout Calculate(
            int screenWidth,
            int screenHeight,
            JxqyIntRect safeArea)
        {
            if (screenWidth <= 0 || screenHeight <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(screenWidth));
            int safeX = Math.Max(0, safeArea.X);
            int safeY = Math.Max(0, safeArea.Y);
            int safeWidth = Math.Min(
                screenWidth - safeX,
                Math.Max(0, safeArea.Width));
            int safeHeight = Math.Min(
                screenHeight - safeY,
                Math.Max(0, safeArea.Height));
            if (safeWidth <= 0 || safeHeight <= 0)
                throw new ArgumentException(
                    "Safe area is empty.",
                    nameof(safeArea));
            double scale = Math.Min(
                safeWidth / (double)OriginalWidth,
                safeHeight / (double)OriginalHeight);
            // Preserve the original pixel proportions while using the whole
            // safe area. Wider displays reveal more world horizontally;
            // narrower displays reveal more vertically instead of stretching
            // or cropping the original 800x600 presentation.
            int logicalWidth = Math.Max(
                OriginalWidth,
                (int)Math.Ceiling(safeWidth / scale));
            int logicalHeight = Math.Max(
                OriginalHeight,
                (int)Math.Ceiling(safeHeight / scale));
            return new JxqyViewportLayout(
                new JxqyIntRect(
                    safeX,
                    safeY,
                    safeWidth,
                    safeHeight),
                scale,
                logicalWidth,
                logicalHeight);
        }

        public static JxqyLogicalPoint ScreenToLogical(
            float screenX,
            float screenY,
            JxqyViewportLayout layout)
        {
            float x = (float)(
                (screenX - layout.PixelRect.X) /
                layout.Scale);
            float yFromTop =
                layout.PixelRect.Bottom - screenY;
            float y = (float)(yFromTop / layout.Scale);
            return new JxqyLogicalPoint(
                Clamp(x, 0, layout.LogicalWidth),
                Clamp(y, 0, layout.LogicalHeight));
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }

    public readonly struct JxqyViewportLayout
    {
        public JxqyViewportLayout(
            JxqyIntRect pixelRect,
            double scale,
            int logicalWidth,
            int logicalHeight)
        {
            PixelRect = pixelRect;
            Scale = scale;
            LogicalWidth = logicalWidth;
            LogicalHeight = logicalHeight;
        }

        public JxqyIntRect PixelRect { get; }
        public double Scale { get; }
        public int LogicalWidth { get; }
        public int LogicalHeight { get; }
    }

    public readonly struct JxqyLogicalPoint
    {
        public JxqyLogicalPoint(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; }
        public float Y { get; }
    }
}
