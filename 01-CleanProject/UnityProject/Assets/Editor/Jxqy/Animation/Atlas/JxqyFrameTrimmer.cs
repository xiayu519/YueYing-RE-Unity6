using System;

namespace Jxqy.Editor.Animation.Atlas
{
    internal static class JxqyFrameTrimmer
    {
        public static JxqyTrimmedFrame Trim(
            JxqyAtlasFrameInput input,
            byte alphaThreshold)
        {
            JxqyDecodedFrame frame = input.Frame;
            int minX = frame.Width;
            int minY = frame.Height;
            int maxX = -1;
            int maxY = -1;

            for (int y = 0; y < frame.Height; y++)
            {
                int row = y * frame.Width;
                for (int x = 0; x < frame.Width; x++)
                {
                    if (frame.Pixels[row + x].A <= alphaThreshold)
                        continue;
                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }
            }

            if (maxX < minX || maxY < minY)
            {
                return new JxqyTrimmedFrame
                {
                    Input = input,
                    Width = 1,
                    Height = 1,
                    Pixels = new JxqyRgba32[1]
                };
            }

            int width = maxX - minX + 1;
            int height = maxY - minY + 1;
            var pixels = new JxqyRgba32[checked(width * height)];
            for (int y = 0; y < height; y++)
            {
                Array.Copy(
                    frame.Pixels,
                    (minY + y) * frame.Width + minX,
                    pixels,
                    y * width,
                    width);
            }

            return new JxqyTrimmedFrame
            {
                Input = input,
                TrimLeft = minX,
                TrimTop = minY,
                TrimBottom = frame.Height - maxY - 1,
                Width = width,
                Height = height,
                Pixels = pixels
            };
        }
    }
}
