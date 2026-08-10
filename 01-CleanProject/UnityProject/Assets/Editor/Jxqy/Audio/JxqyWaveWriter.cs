using System;
using System.IO;
using System.Text;

namespace Jxqy.Editor.Audio
{
    public static class JxqyWaveWriter
    {
        public static byte[] WritePcmWave(JxqyDecodedSoundEffect sound)
        {
            if (sound == null)
                throw new ArgumentNullException(nameof(sound));
            if (sound.FormatTag != 1)
                throw new ArgumentException("Sound effect is not PCM.", nameof(sound));

            int padding = sound.PcmData.Length & 1;
            using var stream = new MemoryStream(44 + sound.PcmData.Length + padding);
            using var writer = new BinaryWriter(stream, Encoding.ASCII, true);
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(checked(36 + sound.PcmData.Length + padding));
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write(sound.FormatTag);
            writer.Write(sound.Channels);
            writer.Write(sound.SampleRate);
            writer.Write(sound.AverageBytesPerSecond);
            writer.Write(sound.BlockAlign);
            writer.Write(sound.BitsPerSample);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(sound.PcmData.Length);
            writer.Write(sound.PcmData);
            if (padding != 0)
                writer.Write((byte)0);
            writer.Flush();
            return stream.ToArray();
        }
    }
}
