using System;
using System.Collections.Generic;
using Jxqy.Domain.Simulation;
using Jxqy.Domain.World;

namespace Jxqy.Domain.Presentation
{
    public readonly struct JxqyColor32 : IEquatable<JxqyColor32>
    {
        public JxqyColor32(byte red, byte green, byte blue, byte alpha = 255)
        {
            Red = red;
            Green = green;
            Blue = blue;
            Alpha = alpha;
        }

        public static JxqyColor32 White => new JxqyColor32(255, 255, 255);
        public static JxqyColor32 Gray => new JxqyColor32(128, 128, 128);
        public static JxqyColor32 Black => new JxqyColor32(0, 0, 0);

        public byte Red { get; }
        public byte Green { get; }
        public byte Blue { get; }
        public byte Alpha { get; }

        public bool Equals(JxqyColor32 other)
        {
            return Red == other.Red && Green == other.Green &&
                   Blue == other.Blue && Alpha == other.Alpha;
        }

        public override bool Equals(object obj)
        {
            return obj is JxqyColor32 other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Red;
                hash = hash * 397 ^ Green;
                hash = hash * 397 ^ Blue;
                return hash * 397 ^ Alpha;
            }
        }

        public static bool operator ==(JxqyColor32 left, JxqyColor32 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(JxqyColor32 left, JxqyColor32 right)
        {
            return !left.Equals(right);
        }
    }

    public enum JxqyWeatherParticleKind
    {
        Rain,
        Snow,
    }

    public readonly struct JxqyWeatherParticle
    {
        public JxqyWeatherParticle(
            JxqyWeatherParticleKind kind,
            JxqyFloat2 position,
            int variant,
            bool visible)
        {
            Kind = kind;
            Position = position;
            Variant = variant;
            Visible = visible;
        }

        public JxqyWeatherParticleKind Kind { get; }
        public JxqyFloat2 Position { get; }
        public int Variant { get; }
        public bool Visible { get; }
    }

    public sealed class JxqyPresentationEffects
    {
        private struct SnowParticle
        {
            public JxqyFloat2 Position;
            public JxqyFloat2 Direction;
            public float Speed;
            public float MovedY;
            public int Variant;
        }

        private struct RainParticle
        {
            public JxqyFloat2 Position;
            public bool Visible;
        }

        private readonly List<RainParticle> _rain =
            new List<RainParticle>();
        private readonly List<SnowParticle> _snow =
            new List<SnowParticle>();
        private readonly JxqyDeterministicRandom _random;
        private float _snowSpawnMilliseconds;
        private float _flashMilliseconds;
        private bool _isFlashing;
        private JxqyColor32 _mapBaseColor = JxqyColor32.White;
        private JxqyColor32 _spriteBaseColor = JxqyColor32.White;
        private JxqyFloat2 _cameraStart;
        private JxqyFloat2 _cameraDestination;
        private float _cameraMoveDuration;
        private float _cameraMoveElapsed;

        public JxqyPresentationEffects(
            JxqyDeterministicRandom random,
            int viewportWidth = JxqyLogicalViewport.OriginalWidth,
            int viewportHeight = JxqyLogicalViewport.OriginalHeight)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
            if (viewportWidth <= 0 || viewportHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(viewportWidth));
            ViewportWidth = viewportWidth;
            ViewportHeight = viewportHeight;
        }

        public event Action Thunder;
        public event Action<string> RainStarted;
        public event Action RainEnded;
        public event Action Changed;

        public int ViewportWidth { get; private set; }
        public int ViewportHeight { get; private set; }
        public int MapTime { get; set; }
        public bool IsRaining { get; private set; }
        public bool IsSnowing { get; private set; }
        public bool WaterEffectEnabled { get; set; }
        public string RainFileName { get; private set; } = string.Empty;
        public JxqyColor32 MapBaseColor => _mapBaseColor;
        public JxqyColor32 SpriteBaseColor => _spriteBaseColor;
        public JxqyColor32 MapColor =>
            IsRaining ? (_isFlashing ? JxqyColor32.White : JxqyColor32.Gray)
                      : _mapBaseColor;
        public JxqyColor32 SpriteColor =>
            IsRaining ? (_isFlashing ? JxqyColor32.White : JxqyColor32.Gray)
                      : _spriteBaseColor;
        public float FadeOpacity { get; private set; }
        public bool IsFadingIn { get; private set; }
        public bool IsFadingOut { get; private set; }
        public bool IsCameraMoving =>
            _cameraMoveDuration > 0 &&
            _cameraMoveElapsed < _cameraMoveDuration;
        public bool HasCameraOverride { get; private set; }
        public JxqyFloat2 CameraPosition { get; private set; }

        public void SetViewportSize(int width, int height)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width));
            if (ViewportWidth == width && ViewportHeight == height)
                return;
            ViewportWidth = width;
            ViewportHeight = height;
            if (IsRaining)
                GenerateRain();
            if (IsSnowing)
            {
                _snow.Clear();
                _snowSpawnMilliseconds = 0;
            }
            Changed?.Invoke();
        }

        public void SetMapColor(int red, int green, int blue)
        {
            _mapBaseColor = Color(red, green, blue);
            Changed?.Invoke();
        }

        public void SetSpriteColor(int red, int green, int blue)
        {
            _spriteBaseColor = Color(red, green, blue);
            Changed?.Invoke();
        }

        public void BeginRain(string fileName)
        {
            RainFileName = fileName ?? string.Empty;
            IsRaining = true;
            GenerateRain();
            RainStarted?.Invoke(RainFileName);
            Changed?.Invoke();
        }

        public void EndRain()
        {
            bool wasRaining = IsRaining;
            IsRaining = false;
            _isFlashing = false;
            _flashMilliseconds = 0;
            if (wasRaining)
                RainEnded?.Invoke();
            Changed?.Invoke();
        }

        public void ShowSnow(bool show)
        {
            IsSnowing = show;
            _snow.Clear();
            _snowSpawnMilliseconds = 0;
            Changed?.Invoke();
        }

        public void FadeOut()
        {
            IsFadingOut = true;
            IsFadingIn = false;
            FadeOpacity = 0;
        }

        public void FadeIn()
        {
            IsFadingOut = false;
            IsFadingIn = true;
            FadeOpacity = 1;
        }

        public void MoveCameraTo(JxqyFloat2 destination, float seconds)
        {
            if (seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            HasCameraOverride = true;
            _cameraStart = CameraPosition;
            _cameraDestination = destination;
            _cameraMoveDuration = seconds;
            _cameraMoveElapsed = 0;
            if (seconds == 0)
                CameraPosition = destination;
        }

        public void SetCameraAnchor(JxqyFloat2 position)
        {
            if (!HasCameraOverride)
                CameraPosition = position;
        }

        public void SetCameraPositionPreservingMove(JxqyFloat2 position)
        {
            JxqyFloat2 translation = position - CameraPosition;
            CameraPosition = position;
            if (!HasCameraOverride || translation == JxqyFloat2.Zero)
                return;
            _cameraStart += translation;
            _cameraDestination += translation;
        }

        public void ReleaseCamera()
        {
            HasCameraOverride = false;
            _cameraMoveDuration = 0;
            _cameraMoveElapsed = 0;
        }

        public static JxqyFloat2 ApplyLegacyPlayerFollow(
            JxqyFloat2 cameraPosition,
            JxqyFloat2 previousPlayerPosition,
            JxqyFloat2 playerPosition,
            int viewportWidth,
            int viewportHeight)
        {
            if (viewportWidth <= 0 || viewportHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(viewportWidth));
            // Original Carmera.UpdatePlayerView deliberately keeps the
            // current view while the effective player is parked at (0, 0).
            if (playerPosition == JxqyFloat2.Zero)
                return cameraPosition;
            JxqyFloat2 offset = playerPosition - previousPlayerPosition;
            if (offset == JxqyFloat2.Zero)
                return cameraPosition;

            var halfView = new JxqyFloat2(
                viewportWidth * 0.5f,
                viewportHeight * 0.5f);
            JxqyFloat2 center = cameraPosition + halfView;
            float centerX = center.X;
            float centerY = center.Y;
            if ((offset.X > 0 && playerPosition.X > centerX) ||
                (offset.X < 0 && playerPosition.X < centerX))
            {
                centerX = playerPosition.X;
            }
            if ((offset.Y > 0 && playerPosition.Y > centerY) ||
                (offset.Y < 0 && playerPosition.Y < centerY))
            {
                centerY = playerPosition.Y;
            }
            return new JxqyFloat2(centerX, centerY) - halfView;
        }

        public void Tick(float elapsedSeconds)
        {
            if (elapsedSeconds < 0 || float.IsNaN(elapsedSeconds) ||
                float.IsInfinity(elapsedSeconds))
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            TickFade();
            TickCamera(elapsedSeconds);
            TickRain(elapsedSeconds);
            TickSnow(elapsedSeconds);
        }

        public IReadOnlyList<JxqyWeatherParticle> SnapshotParticles()
        {
            var result = new List<JxqyWeatherParticle>(
                _rain.Count + _snow.Count);
            SnapshotParticles(result);
            return result;
        }

        public void SnapshotParticles(
            List<JxqyWeatherParticle> result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            result.Clear();
            if (IsRaining)
            {
                foreach (RainParticle particle in _rain)
                {
                    result.Add(new JxqyWeatherParticle(
                        JxqyWeatherParticleKind.Rain,
                        particle.Position,
                        0,
                        particle.Visible));
                }
            }
            if (IsSnowing)
            {
                foreach (SnowParticle particle in _snow)
                {
                    result.Add(new JxqyWeatherParticle(
                        JxqyWeatherParticleKind.Snow,
                        particle.Position,
                        particle.Variant,
                        true));
                }
            }
        }

        private void TickFade()
        {
            const float step = 0.03f;
            if (IsFadingOut && FadeOpacity < 1)
            {
                FadeOpacity = Math.Min(1, FadeOpacity + step);
                if (FadeOpacity >= 1)
                    IsFadingOut = false;
            }
            else if (IsFadingIn && FadeOpacity > 0)
            {
                FadeOpacity = Math.Max(0, FadeOpacity - step);
                if (FadeOpacity <= 0)
                    IsFadingIn = false;
            }
        }

        private void TickCamera(float elapsedSeconds)
        {
            if (!IsCameraMoving)
                return;
            _cameraMoveElapsed = Math.Min(
                _cameraMoveDuration,
                _cameraMoveElapsed + elapsedSeconds);
            float t = _cameraMoveElapsed / _cameraMoveDuration;
            CameraPosition = _cameraStart +
                             (_cameraDestination - _cameraStart) * t;
        }

        private void TickRain(float elapsedSeconds)
        {
            if (!IsRaining)
                return;
            for (int index = 0; index < _rain.Count; index++)
            {
                RainParticle particle = _rain[index];
                particle.Visible = _random.Next(0, 5) == 0;
                _rain[index] = particle;
            }
            if (!_isFlashing && _random.Next(0, 300) == 50)
            {
                _isFlashing = true;
                _flashMilliseconds = 0;
                Thunder?.Invoke();
            }
            if (_isFlashing)
            {
                _flashMilliseconds += elapsedSeconds * 1000f;
                if (_flashMilliseconds >= 100)
                {
                    _isFlashing = false;
                    _flashMilliseconds = 0;
                }
            }
        }

        private void TickSnow(float elapsedSeconds)
        {
            if (!IsSnowing)
                return;
            _snowSpawnMilliseconds += elapsedSeconds * 1000f;
            while (_snowSpawnMilliseconds >= 300)
            {
                _snowSpawnMilliseconds -= 300;
                GenerateSnow();
            }
            for (int index = _snow.Count - 1; index >= 0; index--)
            {
                SnowParticle particle = _snow[index];
                JxqyFloat2 movement =
                    particle.Direction * (particle.Speed * elapsedSeconds);
                particle.Position += movement;
                particle.MovedY +=
                    particle.Speed * particle.Direction.Y * elapsedSeconds;
                if (particle.MovedY >= ViewportHeight)
                {
                    _snow.RemoveAt(index);
                    continue;
                }
                particle.Position = new JxqyFloat2(
                    Wrap(particle.Position.X, ViewportWidth),
                    Wrap(particle.Position.Y, ViewportHeight));
                _snow[index] = particle;
            }
        }

        private void GenerateRain()
        {
            _rain.Clear();
            for (int x = _random.Next(2, 10);
                x < ViewportWidth;
                x += _random.Next(2, 10))
            {
                for (int y = _random.Next(10, 100);
                    y < ViewportHeight;
                    y += _random.Next(10, 100))
                {
                    _rain.Add(new RainParticle
                    {
                        Position = new JxqyFloat2(x, y),
                    });
                }
            }
        }

        private void GenerateSnow()
        {
            for (int x = 0; x < ViewportWidth; x += 50)
            {
                var direction = new JxqyFloat2(
                    _random.Next(-10, 11),
                    10).Normalized;
                _snow.Add(new SnowParticle
                {
                    Position = new JxqyFloat2(x, 0),
                    Direction = direction,
                    Speed = 100 * _random.Next(1, 4),
                    Variant = _random.Next(0, 4),
                });
            }
        }

        private static JxqyColor32 Color(int red, int green, int blue)
        {
            return new JxqyColor32(
                (byte)Math.Max(0, Math.Min(255, red)),
                (byte)Math.Max(0, Math.Min(255, green)),
                (byte)Math.Max(0, Math.Min(255, blue)));
        }

        private static float Wrap(float value, float maximum)
        {
            value %= maximum;
            return value < 0 ? value + maximum : value;
        }
    }
}
