using System;

namespace DontGetSidetracked.Core
{
    [Serializable]
    public readonly struct FixedPoint2 : IEquatable<FixedPoint2>
    {
        public const int Scale = 1_000_000;
        public readonly int X;
        public readonly int Y;

        public FixedPoint2(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static FixedPoint2 FromNormalized(double x, double y) =>
            new FixedPoint2(
                (int)Math.Round(Clamp01(x) * Scale),
                (int)Math.Round(Clamp01(y) * Scale));

        public double NormalizedX => X / (double)Scale;
        public double NormalizedY => Y / (double)Scale;

        public long DistanceSquared(FixedPoint2 other)
        {
            long dx = (long)X - other.X;
            long dy = (long)Y - other.Y;
            return dx * dx + dy * dy;
        }

        public bool Equals(FixedPoint2 other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is FixedPoint2 other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ Y;
        public override string ToString() => $"({X},{Y})";

        private static double Clamp01(double value) => value < 0 ? 0 : value > 1 ? 1 : value;
    }

    [Serializable]
    public readonly struct RecordedPoint
    {
        public readonly FixedPoint2 Position;
        public readonly long TimestampMs;

        public RecordedPoint(FixedPoint2 position, long timestampMs)
        {
            Position = position;
            TimestampMs = timestampMs;
        }
    }

    public sealed class DeterministicRandom
    {
        private ulong _state;

        public DeterministicRandom(long seed, int generatorVersion)
        {
            unchecked
            {
                _state = (ulong)seed ^ ((ulong)(uint)generatorVersion * 0xD1B54A32D192ED03UL);
                _state += 0x9E3779B97F4A7C15UL;
            }
        }

        public ulong NextUInt64()
        {
            unchecked
            {
                _state += 0x9E3779B97F4A7C15UL;
                ulong z = _state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        public int NextInt(int minInclusive, int maxInclusive)
        {
            if (maxInclusive < minInclusive) throw new ArgumentOutOfRangeException(nameof(maxInclusive));
            ulong range = (ulong)((long)maxInclusive - minInclusive + 1L);
            return minInclusive + (int)(NextUInt64() % range);
        }

        public long ForkSeed(int index)
        {
            unchecked
            {
                ulong value = NextUInt64() ^ ((ulong)(uint)index * 0x9E3779B97F4A7C15UL);
                return (long)(value & 0x7FFFFFFFUL);
            }
        }
    }
}
