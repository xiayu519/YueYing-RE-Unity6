using System;
using System.Globalization;
using System.IO;
using Cysharp.Threading.Tasks;
using Jxqy.Domain.Presentation;
using Jxqy.Domain.Scripting;
using Jxqy.Domain.World;
using Jxqy.Ports;

namespace Jxqy.UnityAdapters
{
    public sealed class JxqyPresentationScriptCommandPort :
        IJxqyLegacyScriptCommandPort
    {
        private readonly JxqyPresentationEffects _effects;
        private readonly IJxqyAudioPort _audio;
        private readonly IJxqyVideoPort _video;
        private readonly IJxqyLegacyScriptCommandPort _fallback;
        private readonly Func<int, int, JxqyFloat2> _tileCameraPosition;
        private readonly Action<string> _backgroundMusicChanged;
        private readonly Action _fadeOutRequested;
        private readonly Action _fadeInRequested;
        private readonly Func<bool> _fadeOutCompleted;
        private readonly Func<bool> _fadeInCompleted;

        public JxqyPresentationScriptCommandPort(
            JxqyPresentationEffects effects,
            IJxqyAudioPort audio = null,
            IJxqyVideoPort video = null,
            IJxqyLegacyScriptCommandPort fallback = null,
            Func<int, int, JxqyFloat2> tileCameraPosition = null,
            Action<string> backgroundMusicChanged = null,
            Action fadeOutRequested = null,
            Action fadeInRequested = null,
            Func<bool> fadeOutCompleted = null,
            Func<bool> fadeInCompleted = null)
        {
            _effects = effects ??
                       throw new ArgumentNullException(nameof(effects));
            _audio = audio;
            _video = video;
            _fallback = fallback;
            _tileCameraPosition = tileCameraPosition;
            _backgroundMusicChanged = backgroundMusicChanged;
            _fadeOutRequested = fadeOutRequested;
            _fadeInRequested = fadeInRequested;
            _fadeOutCompleted = fadeOutCompleted;
            _fadeInCompleted = fadeInCompleted;
        }

        public JxqyScriptStep Execute(
            JxqyScriptContext context,
            JxqyScriptInstruction instruction)
        {
            if (instruction == null)
                throw new ArgumentNullException(nameof(instruction));
            switch (instruction.Name.ToLowerInvariant())
            {
                case "beginrain":
                    _effects.BeginRain(Parameter(instruction, 0));
                    break;
                case "endrain":
                    _effects.EndRain();
                    break;
                case "showsnow":
                    _effects.ShowSnow(
                        instruction.Parameters.Count == 0 ||
                        Integer(instruction, 0) != 0);
                    break;
                case "changemapcolor":
                    _effects.SetMapColor(
                        Integer(instruction, 0),
                        Integer(instruction, 1),
                        Integer(instruction, 2));
                    break;
                case "changeasfcolor":
                    _effects.SetSpriteColor(
                        Integer(instruction, 0),
                        Integer(instruction, 1),
                        Integer(instruction, 2));
                    break;
                case "fadein":
                    if (_fadeInRequested != null)
                    {
                        _fadeInRequested();
                        return WaitForFade(_fadeInCompleted);
                    }
                    _effects.FadeIn();
                    break;
                case "fadeout":
                    if (_fadeOutRequested != null)
                    {
                        _fadeOutRequested();
                        return WaitForFade(_fadeOutCompleted);
                    }
                    _effects.FadeOut();
                    break;
                case "openwatereffect":
                    _effects.WaterEffectEnabled = true;
                    break;
                case "closewatereffect":
                    _effects.WaterEffectEnabled = false;
                    break;
                case "setmaptime":
                    _effects.MapTime = Integer(instruction, 0);
                    break;
                case "movescreen":
                {
                    int direction = Integer(instruction, 0);
                    int keepFrameCount = Math.Max(
                        0,
                        Integer(instruction, 1));
                    int speed = Math.Max(
                        0,
                        Integer(instruction, 2));
                    float distance = keepFrameCount * speed * 2f;
                    _effects.MoveCameraTo(
                        _effects.CameraPosition +
                        Direction(direction) * distance,
                        keepFrameCount / 60f);
                    return WaitForCamera();
                }
                case "movescreenex":
                {
                    int column = Integer(instruction, 0);
                    int row = Integer(instruction, 1);
                    float speed = instruction.Parameters.Count >= 3
                        ? Math.Max(1, (float)Number(instruction, 2))
                        : 2;
                    JxqyFloat2 destination =
                        _tileCameraPosition?.Invoke(column, row) ??
                        new JxqyFloat2(column, row);
                    float distance =
                        (destination - _effects.CameraPosition).Length;
                    _effects.MoveCameraTo(
                        destination,
                        MovementSeconds(distance, speed));
                    return WaitForCamera();
                }
                case "playmusic":
                {
                    string legacyPath = Parameter(instruction, 0);
                    string generatedAddress = MusicAddress(legacyPath);
                    if (!JxqyResourceAddressCatalog.TryResolveGeneratedAddress(
                            JxqyLegacyResourceKind.Music,
                            legacyPath,
                            generatedAddress,
                            out string address))
                    {
                        JxqyResourceAddressCatalog.ReportMissing(
                            "PlayMusic",
                            legacyPath,
                            generatedAddress);
                        break;
                    }
                    _backgroundMusicChanged?.Invoke(address);
                    _audio?.PlayMusicAsync(address, loop: true).Forget();
                    break;
                }
                case "playsound":
                {
                    string legacyPath = Parameter(instruction, 0);
                    string generatedAddress = SoundAddress(legacyPath);
                    if (!JxqyResourceAddressCatalog.TryResolveGeneratedAddress(
                            JxqyLegacyResourceKind.Sound,
                            legacyPath,
                            generatedAddress,
                            out string address))
                    {
                        JxqyResourceAddressCatalog.ReportMissing(
                            "PlaySound",
                            legacyPath,
                            generatedAddress);
                        break;
                    }
                    _audio?.PlaySoundAsync(address, 1f).Forget();
                    break;
                }
                case "stopmusic":
                    _backgroundMusicChanged?.Invoke(string.Empty);
                    _audio?.StopMusic();
                    break;
                case "playmovie":
                    if (_video == null)
                        break;
                    string legacyMovie = Parameter(instruction, 0);
                    string generatedMovieAddress =
                        VideoAddress(legacyMovie);
                    if (!JxqyResourceAddressCatalog.TryResolveGeneratedAddress(
                            JxqyLegacyResourceKind.Video,
                            legacyMovie,
                            generatedMovieAddress,
                            out string movieAddress))
                    {
                        JxqyResourceAddressCatalog.ReportMissing(
                            "PlayMovie",
                            legacyMovie,
                            generatedMovieAddress);
                        break;
                    }
                    var movie = new AsyncCommandOperation();
                    movie.RunAsync(
                            _video.PlayAsync(movieAddress))
                        .Forget();
                    return JxqyScriptStep.WaitFor(
                        new JxqyPredicateScriptWait(
                            movie.ThrowIfFailedOrReturnCompleted));
                default:
                    if (_fallback != null)
                        return _fallback.Execute(context, instruction);
                    throw new NotSupportedException(
                        $"Legacy presentation command " +
                        $"'{instruction.Name}' is not supported by this port.");
            }
            return JxqyScriptStep.Continue();
        }

        private JxqyScriptStep WaitForCamera()
        {
            return JxqyScriptStep.WaitFor(
                new JxqyPredicateScriptWait(
                    () => !_effects.IsCameraMoving));
        }

        private static JxqyScriptStep WaitForFade(
            Func<bool> isComplete)
        {
            return JxqyScriptStep.WaitFor(
                new JxqyPredicateScriptWait(
                    isComplete ?? (() => true)));
        }

        private static float MovementSeconds(
            float distance,
            float speed)
        {
            if (distance <= 0)
                return 0;
            return distance / (Math.Max(1, speed) * 60f);
        }

        private static JxqyFloat2 Direction(int direction)
        {
            const float diagonal = 0.70710678f;
            return (((direction % 8) + 8) % 8) switch
            {
                // Match Utils.GetDirection8 from the original engine. World
                // and camera Y increase downward, so direction 0 is down and
                // direction 4 is up.
                0 => new JxqyFloat2(0, 1),
                1 => new JxqyFloat2(-diagonal, diagonal),
                2 => new JxqyFloat2(-1, 0),
                3 => new JxqyFloat2(-diagonal, -diagonal),
                4 => new JxqyFloat2(0, -1),
                5 => new JxqyFloat2(diagonal, -diagonal),
                6 => new JxqyFloat2(1, 0),
                _ => new JxqyFloat2(diagonal, diagonal),
            };
        }

        public static string MusicAddress(string legacyPath)
        {
            return MediaAddress(
                legacyPath,
                "jxqy/media/music/content/music",
                ".wma",
                "music.wav");
        }

        public static string SoundAddress(string legacyPath)
        {
            return MediaAddress(
                legacyPath,
                "jxqy/audio/sound/content/sound",
                ".xnb",
                "sound.wav");
        }

        public static string VideoAddress(string legacyPath)
        {
            return MediaAddress(
                legacyPath,
                "jxqy/media/video/content/video",
                ".wmv",
                "video.mp4");
        }

        private static string MediaAddress(
            string legacyPath,
            string root,
            string sourceExtension,
            string outputFile)
        {
            if (string.IsNullOrWhiteSpace(legacyPath))
                throw new ArgumentException(
                    "Legacy media path is empty.",
                    nameof(legacyPath));
            string normalized = legacyPath
                .Trim()
                .Replace('\\', '/');
            if (normalized.StartsWith(
                    JxqyResourceLocations.AddressPrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                return normalized.ToLowerInvariant();
            }
            string fileName = Path.GetFileName(normalized);
            string stem = Path.GetFileNameWithoutExtension(fileName);
            return $"{root}/{stem}{sourceExtension}/{outputFile}"
                .ToLowerInvariant();
        }

        private static string Parameter(
            JxqyScriptInstruction instruction,
            int index)
        {
            if (instruction.Parameters.Count <= index)
                throw new FormatException(
                    $"{instruction.Name} requires parameter {index + 1}.");
            string value = instruction.Parameters[index].Trim();
            return value.Length >= 2 &&
                   value[0] == '"' &&
                   value[value.Length - 1] == '"'
                ? value.Substring(1, value.Length - 2)
                : value;
        }

        private static int Integer(
            JxqyScriptInstruction instruction,
            int index)
        {
            return int.Parse(
                Parameter(instruction, index),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture);
        }

        private static float Number(
            JxqyScriptInstruction instruction,
            int index)
        {
            return float.Parse(
                Parameter(instruction, index),
                NumberStyles.Float,
                CultureInfo.InvariantCulture);
        }

        private sealed class AsyncCommandOperation
        {
            private Exception _exception;
            private bool _completed;

            public async UniTask RunAsync(UniTask task)
            {
                try
                {
                    await task;
                }
                catch (Exception exception)
                {
                    _exception = exception;
                }
                finally
                {
                    _completed = true;
                }
            }

            public bool ThrowIfFailedOrReturnCompleted()
            {
                if (_exception != null)
                {
                    throw new InvalidOperationException(
                        "Legacy movie playback failed.",
                        _exception);
                }
                return _completed;
            }
        }
    }
}
