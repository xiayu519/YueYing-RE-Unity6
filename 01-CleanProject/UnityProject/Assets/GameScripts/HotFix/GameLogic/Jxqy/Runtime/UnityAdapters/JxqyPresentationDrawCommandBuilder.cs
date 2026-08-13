using System;
using System.Collections.Generic;
using Jxqy.Domain.Presentation;
using Jxqy.Domain.World;
using Jxqy.Ports;
using UnityEngine;

namespace Jxqy.UnityAdapters
{
    public sealed class JxqyPresentationDrawCommandBuilder
    {
        public const int WeatherDepth = 5_000_000;
        public const int FadeDepth = 6_000_000;
        public const int RainTextureWidth = 1;
        public const int RainTextureHeight = 20;

        public string RainTextureAddress { get; set; } =
            "jxqy/shared/weather/rain";
        public IReadOnlyList<string> SnowTextureAddresses { get; set; } =
            new[]
            {
                "jxqy/shared/weather/snow-0",
                "jxqy/shared/weather/snow-1",
                "jxqy/shared/weather/snow-2",
                "jxqy/shared/weather/snow-3",
            };
        public string WhiteTextureAddress { get; set; } =
            "jxqy/shared/builtin/white";

        public List<JxqyDrawCommand> Build(JxqyPresentationEffects effects)
        {
            if (effects == null)
                throw new ArgumentNullException(nameof(effects));
            var commands = new List<JxqyDrawCommand>();
            var particles = new List<JxqyWeatherParticle>();
            Build(
                effects,
                new JxqyIntRect(
                    0,
                    0,
                    effects.ViewportWidth,
                    effects.ViewportHeight),
                commands,
                particles);
            return commands;
        }

        public void Build(
            JxqyPresentationEffects effects,
            JxqyIntRect camera,
            List<JxqyDrawCommand> commands,
            List<JxqyWeatherParticle> particles)
        {
            if (effects == null)
                throw new ArgumentNullException(nameof(effects));
            if (commands == null)
                throw new ArgumentNullException(nameof(commands));
            if (particles == null)
                throw new ArgumentNullException(nameof(particles));
            commands.Clear();
            effects.SnapshotParticles(particles);
            for (int index = 0; index < particles.Count; index++)
            {
                JxqyWeatherParticle particle = particles[index];
                if (!particle.Visible)
                    continue;
                string texture;
                float width;
                float height;
                if (particle.Kind == JxqyWeatherParticleKind.Rain)
                {
                    texture = RainTextureAddress;
                    width = RainTextureWidth;
                    height = RainTextureHeight;
                }
                else
                {
                    if (SnowTextureAddresses == null ||
                        SnowTextureAddresses.Count == 0)
                        continue;
                    int variant = Math.Max(
                        0,
                        Math.Min(
                            SnowTextureAddresses.Count - 1,
                            particle.Variant));
                    texture = SnowTextureAddresses[variant];
                    width = height = 16;
                }
                commands.Add(new JxqyDrawCommand(
                    texture,
                    new Rect(0, 0, width, height),
                    new Vector2(
                        camera.X + particle.Position.X,
                        camera.Y + particle.Position.Y),
                    Vector2.zero,
                    Color.white,
                    WeatherDepth + index,
                    "default"));
            }
        }

        public static Color ToUnityColor(JxqyColor32 color)
        {
            return new Color32(
                color.Red,
                color.Green,
                color.Blue,
                color.Alpha);
        }

        public static Color32[] CreateRainTexturePixels()
        {
            var pixels = new Color32[RainTextureHeight];
            var low = new Color32(128, 128, 128, 51);
            var bright = new Color32(128, 128, 128, 76);
            for (int y = 0; y < pixels.Length; y++)
                pixels[y] = y < 6 || y >= 15 ? low : bright;
            return pixels;
        }
    }
}
