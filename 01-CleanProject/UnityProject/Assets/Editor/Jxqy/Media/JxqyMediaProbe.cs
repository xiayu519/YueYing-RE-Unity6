using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Jxqy.Editor.Media
{
    public sealed class JxqyMediaProbeResult
    {
        public string VideoCodec = string.Empty;
        public string AudioCodec = string.Empty;
        public int Width;
        public int Height;
        public string FrameRate = string.Empty;
        public int SampleRate;
        public int Channels;
        public double DurationSeconds;
    }

    public static class JxqyMediaProbe
    {
        public static JxqyMediaProbeResult Probe(
            string ffprobePath,
            string mediaPath)
        {
            string arguments =
                "-v error -show_entries " +
                "stream=codec_type,codec_name,width,height,r_frame_rate,sample_rate,channels " +
                "-show_entries format=duration -of json " +
                Quote(mediaPath);
            JxqyProcessResult process = JxqyProcessRunner.Run(
                ffprobePath,
                arguments,
                60 * 1000);
            if (process.ExitCode != 0)
                throw new IOException($"ffprobe failed: {process.StandardError}");

            ProbeDocument document = JsonUtility.FromJson<ProbeDocument>(
                process.StandardOutput);
            if (document == null)
                throw new IOException("ffprobe returned invalid JSON.");
            var result = new JxqyMediaProbeResult();
            if (document.streams != null)
            {
                foreach (ProbeStream stream in document.streams)
                {
                    if (stream.codec_type == "video" && string.IsNullOrEmpty(result.VideoCodec))
                    {
                        result.VideoCodec = stream.codec_name ?? string.Empty;
                        result.Width = stream.width;
                        result.Height = stream.height;
                        result.FrameRate = stream.r_frame_rate ?? string.Empty;
                    }
                    else if (stream.codec_type == "audio" &&
                             string.IsNullOrEmpty(result.AudioCodec))
                    {
                        result.AudioCodec = stream.codec_name ?? string.Empty;
                        int.TryParse(
                            stream.sample_rate,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out result.SampleRate);
                        result.Channels = stream.channels;
                    }
                }
            }
            if (document.format != null)
            {
                double.TryParse(
                    document.format.duration,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out result.DurationSeconds);
            }
            return result;
        }

        public static string ResolveExecutable(string executableName)
        {
            if (string.IsNullOrWhiteSpace(executableName))
                throw new ArgumentException("Executable name is empty.", nameof(executableName));
            JxqyProcessResult result = JxqyProcessRunner.Run(
                "where.exe",
                Quote(executableName),
                10 * 1000);
            string path = result.StandardOutput
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(File.Exists);
            if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(path))
                throw new FileNotFoundException($"{executableName} was not found on PATH.");
            return path.Trim();
        }

        internal static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        [Serializable]
        private sealed class ProbeDocument
        {
            public ProbeStream[] streams;
            public ProbeFormat format;
        }

        [Serializable]
        private sealed class ProbeStream
        {
            public string codec_name;
            public string codec_type;
            public int width;
            public int height;
            public string r_frame_rate;
            public string sample_rate;
            public int channels;
        }

        [Serializable]
        private sealed class ProbeFormat
        {
            public string duration;
        }
    }

    public sealed class JxqyProcessResult
    {
        public int ExitCode;
        public string StandardOutput = string.Empty;
        public string StandardError = string.Empty;
    }

    public static class JxqyProcessRunner
    {
        public static JxqyProcessResult Run(
            string executablePath,
            string arguments,
            int timeoutMilliseconds)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
                throw new IOException($"Failed to start {executablePath}.");
            string standardOutput = process.StandardOutput.ReadToEnd();
            string standardError = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(timeoutMilliseconds))
            {
                process.Kill();
                throw new TimeoutException(
                    $"{Path.GetFileName(executablePath)} exceeded " +
                    $"{timeoutMilliseconds} ms.");
            }
            return new JxqyProcessResult
            {
                ExitCode = process.ExitCode,
                StandardOutput = standardOutput,
                StandardError = standardError
            };
        }
    }
}
