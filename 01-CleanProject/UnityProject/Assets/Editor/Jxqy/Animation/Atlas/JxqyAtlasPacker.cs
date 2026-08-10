using System;
using System.Collections.Generic;
using System.Linq;

namespace Jxqy.Editor.Animation.Atlas
{
    public static class JxqyAtlasPacker
    {
        public static JxqyAtlasPackResult Pack(
            IReadOnlyList<JxqyAtlasFrameInput> inputs,
            JxqyAtlasPackSettings settings = null)
        {
            if (inputs == null)
                throw new ArgumentNullException(nameof(inputs));
            settings ??= new JxqyAtlasPackSettings();
            settings.Validate();

            var frames = inputs
                .Select(input => JxqyFrameTrimmer.Trim(input, settings.AlphaThreshold))
                .OrderByDescending(frame => Math.Max(frame.Width, frame.Height))
                .ThenByDescending(frame => frame.Width * frame.Height)
                .ThenBy(frame => frame.Input.Key, StringComparer.Ordinal)
                .ThenBy(frame => frame.Input.FrameIndex)
                .ToList();

            var result = new JxqyAtlasPackResult
            {
                SourcePixelCount = inputs.Sum(
                    input => (long)input.Frame.Width * input.Frame.Height),
                TrimmedPixelCount = frames.Sum(
                    frame => (long)frame.Width * frame.Height)
            };
            var pages = new List<PageBuilder>();
            foreach (JxqyTrimmedFrame frame in frames)
            {
                int packedWidth = checked(frame.Width + settings.Extrude * 2);
                int packedHeight = checked(frame.Height + settings.Extrude * 2);
                if (packedWidth > settings.MaximumPageSize ||
                    packedHeight > settings.MaximumPageSize)
                {
                    throw new InvalidOperationException(
                        $"Frame {frame.Input.Key}#{frame.Input.FrameIndex} requires " +
                        $"{packedWidth}x{packedHeight}, exceeding atlas limit " +
                        $"{settings.MaximumPageSize}.");
                }

                PlacementCandidate candidate = FindBestPlacement(
                    pages,
                    packedWidth,
                    packedHeight);
                if (candidate == null)
                {
                    var page = new PageBuilder(
                        pages.Count,
                        settings.MaximumPageSize,
                        settings.MaximumPageSize);
                    pages.Add(page);
                    candidate = FindBestPlacement(
                        pages,
                        packedWidth,
                        packedHeight);
                }

                Place(
                    candidate.Page,
                    candidate.FreeRectangle,
                    frame,
                    packedWidth,
                    packedHeight,
                    settings.Extrude,
                    result);
            }

            foreach (PageBuilder pageBuilder in pages)
            {
                JxqyAtlasPage page = pageBuilder.Build(settings.MinimumPageSize);
                result.Pages.Add(page);
                result.AtlasPixelCount += (long)page.Width * page.Height;
            }
            return result;
        }

        private static PlacementCandidate FindBestPlacement(
            IReadOnlyList<PageBuilder> pages,
            int width,
            int height)
        {
            PlacementCandidate best = null;
            foreach (PageBuilder page in pages)
            {
                foreach (AtlasRectangle free in page.FreeRectangles)
                {
                    if (width > free.Width || height > free.Height)
                        continue;

                    int shortSide = Math.Min(free.Width - width, free.Height - height);
                    int longSide = Math.Max(free.Width - width, free.Height - height);
                    if (best == null ||
                        shortSide < best.ShortSide ||
                        shortSide == best.ShortSide && longSide < best.LongSide ||
                        shortSide == best.ShortSide && longSide == best.LongSide &&
                        page.PageIndex < best.Page.PageIndex ||
                        shortSide == best.ShortSide && longSide == best.LongSide &&
                        page.PageIndex == best.Page.PageIndex && free.Y < best.FreeRectangle.Y ||
                        shortSide == best.ShortSide && longSide == best.LongSide &&
                        page.PageIndex == best.Page.PageIndex &&
                        free.Y == best.FreeRectangle.Y && free.X < best.FreeRectangle.X)
                    {
                        best = new PlacementCandidate
                        {
                            Page = page,
                            FreeRectangle = free,
                            ShortSide = shortSide,
                            LongSide = longSide
                        };
                    }
                }
            }
            return best;
        }

        private static void Place(
            PageBuilder page,
            AtlasRectangle free,
            JxqyTrimmedFrame frame,
            int packedWidth,
            int packedHeight,
            int extrude,
            JxqyAtlasPackResult result)
        {
            var used = new AtlasRectangle(free.X, free.Y, packedWidth, packedHeight);
            page.SplitFreeRectangles(used);

            var placement = new JxqyAtlasPlacement
            {
                Key = frame.Input.Key,
                FrameIndex = frame.Input.FrameIndex,
                PageIndex = page.PageIndex,
                X = used.X,
                Y = used.Y,
                Width = used.Width,
                Height = used.Height,
                ContentX = used.X + extrude,
                ContentY = used.Y + extrude,
                ContentWidth = frame.Width,
                ContentHeight = frame.Height,
                TrimLeft = frame.TrimLeft,
                TrimTop = frame.TrimTop,
                TrimBottom = frame.TrimBottom
            };
            page.Items.Add(new PageItem(frame, placement, extrude));
            result.Placements.Add(placement);

            frame.Input.Metadata.AtlasPage = placement.PageIndex;
            frame.Input.Metadata.AtlasX = placement.ContentX;
            frame.Input.Metadata.AtlasY = placement.ContentY;
            frame.Input.Metadata.AtlasWidth = placement.ContentWidth;
            frame.Input.Metadata.AtlasHeight = placement.ContentHeight;
            frame.Input.Metadata.TrimLeft = placement.TrimLeft;
            frame.Input.Metadata.TrimTop = placement.TrimTop;
            frame.Input.Metadata.TrimBottom = placement.TrimBottom;
            frame.Input.Metadata.AnchorX -= placement.TrimLeft;
            frame.Input.Metadata.AnchorY -= placement.TrimTop;
        }

        private sealed class PageBuilder
        {
            private readonly int _maximumWidth;
            private readonly int _maximumHeight;

            public PageBuilder(int pageIndex, int maximumWidth, int maximumHeight)
            {
                PageIndex = pageIndex;
                _maximumWidth = maximumWidth;
                _maximumHeight = maximumHeight;
                FreeRectangles.Add(new AtlasRectangle(0, 0, maximumWidth, maximumHeight));
            }

            public int PageIndex { get; }
            public List<AtlasRectangle> FreeRectangles { get; } = new();
            public List<PageItem> Items { get; } = new();

            public void SplitFreeRectangles(AtlasRectangle used)
            {
                for (int index = FreeRectangles.Count - 1; index >= 0; index--)
                {
                    AtlasRectangle free = FreeRectangles[index];
                    if (!Intersects(free, used))
                        continue;

                    FreeRectangles.RemoveAt(index);
                    if (used.X > free.X)
                    {
                        FreeRectangles.Add(new AtlasRectangle(
                            free.X,
                            free.Y,
                            used.X - free.X,
                            free.Height));
                    }
                    if (used.Right < free.Right)
                    {
                        FreeRectangles.Add(new AtlasRectangle(
                            used.Right,
                            free.Y,
                            free.Right - used.Right,
                            free.Height));
                    }
                    if (used.Y > free.Y)
                    {
                        FreeRectangles.Add(new AtlasRectangle(
                            free.X,
                            free.Y,
                            free.Width,
                            used.Y - free.Y));
                    }
                    if (used.Top < free.Top)
                    {
                        FreeRectangles.Add(new AtlasRectangle(
                            free.X,
                            used.Top,
                            free.Width,
                            free.Top - used.Top));
                    }
                }
                PruneFreeRectangles();
            }

            public JxqyAtlasPage Build(int minimumSize)
            {
                int usedWidth = Items.Count == 0 ? minimumSize : Items.Max(item => item.Placement.X + item.Placement.Width);
                int usedHeight = Items.Count == 0 ? minimumSize : Items.Max(item => item.Placement.Y + item.Placement.Height);
                int width = Math.Min(_maximumWidth, NextPowerOfTwo(Math.Max(minimumSize, usedWidth)));
                int height = Math.Min(_maximumHeight, NextPowerOfTwo(Math.Max(minimumSize, usedHeight)));
                var page = new JxqyAtlasPage
                {
                    PageIndex = PageIndex,
                    Width = width,
                    Height = height,
                    Pixels = new JxqyRgba32[checked(width * height)]
                };

                foreach (PageItem item in Items)
                {
                    BlitExtruded(page, item);
                    page.Placements.Add(item.Placement);
                }
                return page;
            }

            private void PruneFreeRectangles()
            {
                for (int first = FreeRectangles.Count - 1; first >= 0; first--)
                {
                    AtlasRectangle left = FreeRectangles[first];
                    if (left.Width <= 0 || left.Height <= 0)
                    {
                        FreeRectangles.RemoveAt(first);
                        continue;
                    }

                    for (int second = FreeRectangles.Count - 1; second >= 0; second--)
                    {
                        if (first == second)
                            continue;
                        if (!Contains(FreeRectangles[second], left))
                            continue;
                        FreeRectangles.RemoveAt(first);
                        break;
                    }
                }
            }
        }

        private static void BlitExtruded(JxqyAtlasPage page, PageItem item)
        {
            JxqyTrimmedFrame frame = item.Frame;
            int extrude = item.Extrude;
            for (int localY = -extrude; localY < frame.Height + extrude; localY++)
            {
                int clampedBottomY = Math.Max(0, Math.Min(frame.Height - 1, localY));
                int sourceTopY = frame.Height - clampedBottomY - 1;
                for (int localX = -extrude; localX < frame.Width + extrude; localX++)
                {
                    int sourceX = Math.Max(0, Math.Min(frame.Width - 1, localX));
                    int destinationX = item.Placement.ContentX + localX;
                    int destinationY = item.Placement.ContentY + localY;
                    page.Pixels[destinationY * page.Width + destinationX] =
                        frame.Pixels[sourceTopY * frame.Width + sourceX];
                }
            }
        }

        private static bool Intersects(AtlasRectangle left, AtlasRectangle right)
        {
            return left.X < right.Right &&
                   left.Right > right.X &&
                   left.Y < right.Top &&
                   left.Top > right.Y;
        }

        private static bool Contains(AtlasRectangle outer, AtlasRectangle inner)
        {
            return inner.X >= outer.X &&
                   inner.Y >= outer.Y &&
                   inner.Right <= outer.Right &&
                   inner.Top <= outer.Top;
        }

        private static int NextPowerOfTwo(int value)
        {
            int power = 1;
            while (power < value)
                power <<= 1;
            return power;
        }

        private sealed class PlacementCandidate
        {
            public PageBuilder Page;
            public AtlasRectangle FreeRectangle;
            public int ShortSide;
            public int LongSide;
        }

        private readonly struct AtlasRectangle
        {
            public AtlasRectangle(int x, int y, int width, int height)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
            }

            public int X { get; }
            public int Y { get; }
            public int Width { get; }
            public int Height { get; }
            public int Right => X + Width;
            public int Top => Y + Height;
        }

        private sealed class PageItem
        {
            public PageItem(
                JxqyTrimmedFrame frame,
                JxqyAtlasPlacement placement,
                int extrude)
            {
                Frame = frame;
                Placement = placement;
                Extrude = extrude;
            }

            public JxqyTrimmedFrame Frame { get; }
            public JxqyAtlasPlacement Placement { get; }
            public int Extrude { get; }
        }
    }
}
