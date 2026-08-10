using System;

namespace Jxqy.Domain.Content
{
    [Serializable]
    public sealed class JxqyMediaMetadata
    {
        public int SchemaVersion = 1;
        public string ConverterVersion = string.Empty;
        public string SourceStableId = string.Empty;
        public string SourceRelativePath = string.Empty;
        public string SourceAddress = string.Empty;
        public string SourceSha256 = string.Empty;
        public string MediaKind = string.Empty;
        public string OutputAddress = string.Empty;
        public string TranscodeProfile = string.Empty;
        public string SourceVideoCodec = string.Empty;
        public string SourceAudioCodec = string.Empty;
        public string OutputVideoCodec = string.Empty;
        public string OutputAudioCodec = string.Empty;
        public int Width;
        public int Height;
        public string FrameRate = string.Empty;
        public int SampleRate;
        public int Channels;
        public double SourceDurationSeconds;
        public double OutputDurationSeconds;
    }
}
