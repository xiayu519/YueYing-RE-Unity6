using System;

namespace Jxqy.Domain.Content
{
    [Serializable]
    public sealed class JxqyAudioMetadata
    {
        public int SchemaVersion = 1;
        public string ConverterVersion = string.Empty;
        public string SourceStableId = string.Empty;
        public string SourceRelativePath = string.Empty;
        public string SourceAddress = string.Empty;
        public string SourceSha256 = string.Empty;
        public string WavAddress = string.Empty;
        public int FormatTag;
        public int Channels;
        public int SampleRate;
        public int BitsPerSample;
        public int PcmByteCount;
        public int LoopStart;
        public int LoopLength;
        public int DurationMilliseconds;
    }
}
