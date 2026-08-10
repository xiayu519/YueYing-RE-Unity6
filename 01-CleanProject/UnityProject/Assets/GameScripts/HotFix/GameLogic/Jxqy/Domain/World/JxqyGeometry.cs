using System;

namespace Jxqy.Domain.World
{
    [Serializable]
    public readonly struct JxqyIntPoint : IEquatable<JxqyIntPoint>
    {
        public JxqyIntPoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }

        public bool Equals(JxqyIntPoint other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is JxqyIntPoint other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return X * 397 ^ Y;
            }
        }

        public override string ToString()
        {
            return $"({X}, {Y})";
        }
    }

    [Serializable]
    public readonly struct JxqyIntRect : IEquatable<JxqyIntRect>
    {
        public JxqyIntRect(int x, int y, int width, int height)
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
        public int Bottom => Y + Height;

        public bool Equals(JxqyIntRect other)
        {
            return X == other.X && Y == other.Y &&
                   Width == other.Width && Height == other.Height;
        }

        public override bool Equals(object obj)
        {
            return obj is JxqyIntRect other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = X;
                hash = hash * 397 ^ Y;
                hash = hash * 397 ^ Width;
                return hash * 397 ^ Height;
            }
        }
    }
}
