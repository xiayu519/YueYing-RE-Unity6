using System;
using System.Collections.Generic;

namespace Jxqy.Editor.Audio
{
    public sealed class JxqyDecodedSoundEffect
    {
        public char Platform;
        public byte XnbVersion;
        public byte Flags;
        public List<string> TypeReaders = new();
        public ushort FormatTag;
        public ushort Channels;
        public int SampleRate;
        public int AverageBytesPerSecond;
        public ushort BlockAlign;
        public ushort BitsPerSample;
        public byte[] WaveFormat = Array.Empty<byte>();
        public byte[] PcmData = Array.Empty<byte>();
        public int LoopStart;
        public int LoopLength;
        public int DurationMilliseconds;
    }
}
