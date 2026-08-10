using System;
using System.Collections.Generic;
using System.Globalization;

namespace Jxqy.Domain.Persistence
{
    public static class JxqyLegacySaveImporter
    {
        public static JxqySaveGameData ImportGameIni(string iniText)
        {
            if (iniText == null)
                throw new ArgumentNullException(nameof(iniText));
            Dictionary<string, Dictionary<string, string>> sections =
                ParseIni(iniText);
            var save = new JxqySaveGameData
            {
                SourceFormat = "LegacyIni",
                SavedUtc = DateTime.UtcNow.ToString(
                    "O",
                    CultureInfo.InvariantCulture)
            };
            Dictionary<string, string> state = GetSection(
                sections,
                "State");
            save.World.Map = Get(state, "Map");
            save.World.NpcFile = Get(state, "Npc");
            save.World.ObjectFile = Get(state, "Obj");
            save.World.BackgroundMusic = Get(state, "Bgm");
            save.Player.PlayerIndex = ParseInt(
                Get(state, "Chr"),
                0);
            save.Presentation.ScriptShowMapPosition =
                ParseInt(Get(state, "ScriptShowMapPos"), 0) != 0;

            Dictionary<string, string> option = GetSection(
                sections,
                "Option");
            save.World.MapTime = ParseInt(
                Get(option, "MapTime"),
                0);
            save.World.IsSnowing =
                ParseInt(Get(option, "SnowShow"), 0) != 0;
            save.World.RainFile = Get(option, "RainFile");
            save.World.WaterEffectEnabled =
                ParseInt(Get(option, "Water"), 0) != 0;
            save.World.SaveDisabled =
                ParseInt(Get(option, "SaveDisabled"), 0) != 0;
            save.World.DropGoodWhenDefeatEnemyDisabled =
                ParseInt(
                    Get(
                        option,
                        "IsDropGoodWhenDefeatEnemyDisabled"),
                    0) != 0;
            save.Presentation.MapColorBgra = Get(
                option,
                "MpcStyle");
            save.Presentation.SpriteColorBgra = Get(
                option,
                "AsfStyle");

            Dictionary<string, string> timer = GetSection(
                sections,
                "Timer");
            save.Presentation.TimerEnabled =
                ParseInt(Get(timer, "IsOn"), 0) != 0;
            save.Presentation.TimerTotalSeconds = ParseInt(
                Get(timer, "TotalSecond"),
                0);
            save.Presentation.TimerWindowVisible =
                ParseInt(
                    Get(timer, "IsTimerWindowShow"),
                    0) != 0;
            save.Presentation.TimerScriptEnabled =
                ParseInt(Get(timer, "IsScriptSet"), 0) != 0;
            save.Presentation.TimerScript = Get(
                timer,
                "TimerScript");
            save.Presentation.TimerTriggerSeconds = ParseInt(
                Get(timer, "TriggerTime"),
                0);

            foreach (KeyValuePair<string, string> variable in
                     GetSection(sections, "Var"))
            {
                save.Variables.Add(new JxqySaveVariable
                {
                    Name = variable.Key,
                    Value = variable.Value
                });
            }
            save.LegacyFiles.Add(new JxqyLegacySaveFile
            {
                RelativePath = "Game.ini",
                Utf8Text = iniText
            });
            return save;
        }

        public static Dictionary<string, Dictionary<string, string>>
            ParseIni(string text)
        {
            var result =
                new Dictionary<string, Dictionary<string, string>>(
                    StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> current = null;
            string normalized = text.Replace("\r\n", "\n")
                .Replace('\r', '\n');
            foreach (string rawLine in normalized.Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 ||
                    line.StartsWith(";", StringComparison.Ordinal) ||
                    line.StartsWith("#", StringComparison.Ordinal))
                    continue;
                if (line.StartsWith("[", StringComparison.Ordinal) &&
                    line.EndsWith("]", StringComparison.Ordinal) &&
                    line.Length > 2)
                {
                    string name = line.Substring(
                        1,
                        line.Length - 2).Trim();
                    if (!result.TryGetValue(name, out current))
                    {
                        current =
                            new Dictionary<string, string>(
                                StringComparer.OrdinalIgnoreCase);
                        result.Add(name, current);
                    }
                    continue;
                }
                int equals = line.IndexOf('=');
                if (current == null || equals <= 0)
                    continue;
                string key = line.Substring(0, equals).Trim();
                string value = line.Substring(equals + 1).Trim();
                current[key] = value;
            }
            return result;
        }

        private static Dictionary<string, string> GetSection(
            Dictionary<string, Dictionary<string, string>> sections,
            string name)
        {
            return sections.TryGetValue(
                name,
                out Dictionary<string, string> section)
                ? section
                : new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);
        }

        private static string Get(
            Dictionary<string, string> section,
            string key)
        {
            return section.TryGetValue(key, out string value)
                ? value
                : string.Empty;
        }

        private static int ParseInt(string value, int fallback)
        {
            return int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int result)
                ? result
                : fallback;
        }
    }
}
