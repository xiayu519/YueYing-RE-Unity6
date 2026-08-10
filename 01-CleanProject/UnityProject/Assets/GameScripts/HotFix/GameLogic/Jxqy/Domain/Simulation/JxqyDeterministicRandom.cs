using System;

namespace Jxqy.Domain.Simulation
{
    public sealed class JxqyDeterministicRandom
    {
        private uint _state;

        public JxqyDeterministicRandom(int seed)
        {
            _state = unchecked((uint)seed);
            if (_state == 0)
                _state = 0x6D2B79F5u;
        }

        public uint State => _state;

        public int Next(int minimumInclusive, int maximumExclusive)
        {
            if (minimumInclusive >= maximumExclusive)
                throw new ArgumentOutOfRangeException(
                    nameof(maximumExclusive));
            uint range = checked(
                (uint)(maximumExclusive - minimumInclusive));
            return minimumInclusive + (int)(NextUInt32() % range);
        }

        public float NextSingle()
        {
            return (NextUInt32() >> 8) * (1f / 16777216f);
        }

        private uint NextUInt32()
        {
            uint value = _state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            _state = value;
            return value;
        }
    }
}
