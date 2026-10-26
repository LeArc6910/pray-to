using System;
using System.Globalization;

namespace Game.Core
{
    // World positions are in tiles: tile (x, y) covers x..x+1 and y..y+1, with y pointing up as in Unity.
    public readonly struct Vec2 : IEquatable<Vec2>
    {
        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; }
        public float Y { get; }

        public static Vec2 Zero => new Vec2(0, 0);

        public float LengthSquared => X * X + Y * Y;
        public float Length => MathF.Sqrt(LengthSquared);
        public bool IsFinite => !float.IsNaN(X) && !float.IsInfinity(X) && !float.IsNaN(Y) && !float.IsInfinity(Y);

        public Vec2 Normalized
        {
            get
            {
                float length = Length;
                return length > 1e-6f ? new Vec2(X / length, Y / length) : Zero;
            }
        }

        public Vec2 ClampLength(float max)
        {
            float length = Length;
            return length > max ? this * (max / length) : this;
        }

        public static Vec2 FromAngle(float radians) => new Vec2(MathF.Cos(radians), MathF.Sin(radians));

        public float Angle => MathF.Atan2(Y, X);

        public static float Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;

        public static float Distance(Vec2 a, Vec2 b) => (a - b).Length;

        public static Vec2 Lerp(Vec2 a, Vec2 b, float t) => a + (b - a) * t;

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);

        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);

        public static Vec2 operator -(Vec2 a) => new Vec2(-a.X, -a.Y);

        public static Vec2 operator *(Vec2 a, float scale) => new Vec2(a.X * scale, a.Y * scale);

        public static Vec2 operator /(Vec2 a, float scale) => new Vec2(a.X / scale, a.Y / scale);

        public bool Equals(Vec2 other) => X == other.X && Y == other.Y;

        public override bool Equals(object obj) => obj is Vec2 other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y);

        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "({0:0.###}, {1:0.###})", X, Y);
    }
}
