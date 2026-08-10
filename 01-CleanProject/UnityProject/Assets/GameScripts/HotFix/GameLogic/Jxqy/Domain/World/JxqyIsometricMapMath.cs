using System;

namespace Jxqy.Domain.World
{
    public static class JxqyIsometricMapMath
    {
        public const int TileWidth = 64;
        public const int TileHeight = 32;
        public const int HalfTileWidth = 32;
        public const int HalfTileHeight = 16;
        public const int VisibilityPaddingTiles = 20;

        public static JxqyIntPoint TileToWorldPixel(
            int column,
            int row,
            bool boundCheck = true)
        {
            if (boundCheck && (column < 0 || row < 0))
                return new JxqyIntPoint(0, 0);
            int x = row % 2 * HalfTileWidth +
                    TileWidth * column;
            int y = HalfTileHeight * row;
            return new JxqyIntPoint(x, y);
        }

        public static JxqyIntPoint WorldPixelToTile(
            int pixelX,
            int pixelY,
            bool boundCheck = true)
        {
            if (boundCheck && (pixelX < 0 || pixelY < 0))
                return new JxqyIntPoint(0, 0);
            int column = pixelX / TileWidth;
            int row = 1 + pixelY / TileHeight * 2;
            float dx = pixelX - column * TileWidth;
            float dy = pixelY - row / 2 * TileHeight;
            if (dx < HalfTileWidth)
            {
                if (dy < (TileWidth / 2f - dx) / 2f)
                    row--;
                else if (dy > dx / 2f + HalfTileHeight)
                    row++;
            }
            if (dx > HalfTileWidth)
            {
                if (dy < (dx - HalfTileWidth) / 2f)
                {
                    column++;
                    row--;
                }
                else if (dy >
                         (TileWidth - dx) / 2f + HalfTileHeight)
                {
                    column++;
                    row++;
                }
            }
            return new JxqyIntPoint(column, row);
        }

        public static JxqyIntRect ClampCamera(
            int requestedX,
            int requestedY,
            int viewWidth,
            int viewHeight,
            int worldWidth,
            int worldHeight)
        {
            int safeWorldWidth = Math.Max(0, worldWidth);
            int safeWorldHeight = Math.Max(0, worldHeight);
            int safeViewWidth = Math.Min(
                Math.Max(0, viewWidth),
                safeWorldWidth);
            int safeViewHeight = Math.Min(
                Math.Max(0, viewHeight),
                safeWorldHeight);
            int maximumX = Math.Max(
                0,
                safeWorldWidth - safeViewWidth);
            int maximumY = Math.Max(
                0,
                safeWorldHeight - safeViewHeight);
            return new JxqyIntRect(
                Math.Max(0, Math.Min(requestedX, maximumX)),
                Math.Max(0, Math.Min(requestedY, maximumY)),
                safeViewWidth,
                safeViewHeight);
        }

        public static JxqyTileRange CalculateVisibleTileRange(
            JxqyIntRect camera,
            int mapColumns,
            int mapRows)
        {
            JxqyIntPoint start = WorldPixelToTile(
                camera.X,
                camera.Y);
            JxqyIntPoint end = WorldPixelToTile(
                camera.Right,
                camera.Bottom);
            return new JxqyTileRange(
                Math.Max(0, start.X - VisibilityPaddingTiles),
                Math.Max(0, start.Y - VisibilityPaddingTiles),
                Math.Min(
                    Math.Max(0, mapColumns),
                    end.X + VisibilityPaddingTiles),
                Math.Min(
                    Math.Max(0, mapRows),
                    end.Y + VisibilityPaddingTiles));
        }

        public static JxqyIntPoint WorldToView(
            JxqyIntPoint world,
            JxqyIntRect camera)
        {
            return new JxqyIntPoint(
                world.X - camera.X,
                world.Y - camera.Y);
        }

        public static JxqyIntPoint ViewToWorld(
            JxqyIntPoint view,
            JxqyIntRect camera)
        {
            return new JxqyIntPoint(
                view.X + camera.X,
                view.Y + camera.Y);
        }
    }

    [Serializable]
    public readonly struct JxqyTileRange
    {
        public JxqyTileRange(
            int startColumn,
            int startRow,
            int endColumnExclusive,
            int endRowExclusive)
        {
            StartColumn = startColumn;
            StartRow = startRow;
            EndColumnExclusive = endColumnExclusive;
            EndRowExclusive = endRowExclusive;
        }

        public int StartColumn { get; }
        public int StartRow { get; }
        public int EndColumnExclusive { get; }
        public int EndRowExclusive { get; }
    }
}
