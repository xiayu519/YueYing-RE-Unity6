using System;

namespace Jxqy.Domain.World
{
    [Serializable]
    public readonly struct JxqyFloat2 : IEquatable<JxqyFloat2>
    {
        public JxqyFloat2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static JxqyFloat2 Zero => new JxqyFloat2(0f, 0f);

        public float X { get; }
        public float Y { get; }
        public float LengthSquared => X * X + Y * Y;
        public float Length => (float)Math.Sqrt(LengthSquared);

        public JxqyFloat2 Normalized
        {
            get
            {
                float length = Length;
                return length <= float.Epsilon
                    ? Zero
                    : this / length;
            }
        }

        public static float Distance(JxqyFloat2 left, JxqyFloat2 right)
        {
            return (left - right).Length;
        }

        public bool Equals(JxqyFloat2 other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y);
        }

        public override bool Equals(object obj)
        {
            return obj is JxqyFloat2 other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return X.GetHashCode() * 397 ^ Y.GetHashCode();
            }
        }

        public override string ToString()
        {
            return $"({X}, {Y})";
        }

        public static JxqyFloat2 operator +(JxqyFloat2 left, JxqyFloat2 right)
        {
            return new JxqyFloat2(left.X + right.X, left.Y + right.Y);
        }

        public static JxqyFloat2 operator -(JxqyFloat2 left, JxqyFloat2 right)
        {
            return new JxqyFloat2(left.X - right.X, left.Y - right.Y);
        }

        public static JxqyFloat2 operator -(JxqyFloat2 value)
        {
            return new JxqyFloat2(-value.X, -value.Y);
        }

        public static JxqyFloat2 operator *(JxqyFloat2 value, float scale)
        {
            return new JxqyFloat2(value.X * scale, value.Y * scale);
        }

        public static JxqyFloat2 operator *(float scale, JxqyFloat2 value)
        {
            return value * scale;
        }

        public static JxqyFloat2 operator /(JxqyFloat2 value, float divisor)
        {
            if (Math.Abs(divisor) <= float.Epsilon)
                throw new DivideByZeroException();
            return new JxqyFloat2(value.X / divisor, value.Y / divisor);
        }

        public static bool operator ==(JxqyFloat2 left, JxqyFloat2 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(JxqyFloat2 left, JxqyFloat2 right)
        {
            return !left.Equals(right);
        }
    }

    public static class JxqyDirection
    {
        public static int GetIndex(JxqyFloat2 direction, int directionCount)
        {
            if (direction == JxqyFloat2.Zero || directionCount < 1)
                return 0;

            JxqyFloat2 normalized = direction.Normalized;
            double dot = Math.Max(-1d, Math.Min(1d, normalized.Y));
            double angle = Math.Acos(dot);
            if (normalized.X > 0)
                angle = Math.PI * 2d - angle;

            double halfAnglePerDirection = Math.PI / directionCount;
            int region = (int)(angle / halfAnglePerDirection);
            if (region % 2 != 0)
                region++;
            region %= 2 * directionCount;
            return region / 2;
        }
    }
}
