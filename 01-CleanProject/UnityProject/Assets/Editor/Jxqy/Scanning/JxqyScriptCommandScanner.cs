using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Jxqy.Editor.Scanning
{
    public sealed class JxqyScriptCommandScanner
    {
        private static readonly Regex CommandRegex = new Regex(
            @"^(?<name>[A-Za-z]+)\s*(?:\((?<parameters>.*)\))?\s*;?\s*(?:@[A-Za-z0-9]+)?\s*$",
            RegexOptions.Compiled);
        private static readonly Regex RunnerCaseRegex = new Regex(
            @"case\s+""(?<name>[A-Za-z][A-Za-z0-9]*)""\s*:",
            RegexOptions.Compiled);

        private readonly Encoding _legacyEncoding = Encoding.GetEncoding(936);

        public JxqyScriptCommandReport Scan(string scriptRoot, string referenceRunnerPath)
        {
            if (!Directory.Exists(scriptRoot))
                throw new DirectoryNotFoundException(scriptRoot);
            if (!File.Exists(referenceRunnerPath))
                throw new FileNotFoundException("Reference ScriptRunner was not found.", referenceRunnerPath);

            var implemented = new HashSet<string>(
                RunnerCaseRegex.Matches(File.ReadAllText(referenceRunnerPath))
                    .Cast<Match>()
                    .Select(match => match.Groups["name"].Value),
                StringComparer.Ordinal);
            var commands = new Dictionary<string, JxqyScriptCommandRecord>(StringComparer.Ordinal);
            var report = new JxqyScriptCommandReport
            {
                ConverterVersion = JxqyImporterAssembly.ConverterVersion,
                GeneratedUtc = DateTime.UtcNow.ToString("O")
            };

            string[] scripts = Directory.GetFiles(scriptRoot, "*.txt", SearchOption.AllDirectories);
            Array.Sort(scripts, StringComparer.OrdinalIgnoreCase);
            report.ScriptCount = scripts.Length;

            foreach (string script in scripts)
            {
                string relative = JxqyPathUtility.NormalizeRelativePath(scriptRoot, script);
                string[] lines = File.ReadAllLines(script, _legacyEncoding);
                for (int index = 0; index < lines.Length; index++)
                {
                    string literal = lines[index];
                    string trimmed = literal.Trim();
                    if (trimmed.Length == 0 ||
                        trimmed.StartsWith("//", StringComparison.Ordinal) ||
                        trimmed.StartsWith("@", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    Match match = CommandRegex.Match(trimmed);
                    if (!match.Success)
                    {
                        report.ParseWarnings.Add($"{relative}:{index + 1}: {literal}");
                        continue;
                    }

                    string name = match.Groups["name"].Value;
                    List<string> parameters = ParseParameters(match.Groups["parameters"].Value);
                    if (!commands.TryGetValue(name, out JxqyScriptCommandRecord record))
                    {
                        record = new JxqyScriptCommandRecord
                        {
                            Name = name,
                            ImplementedByReferenceRunner = implemented.Contains(name)
                        };
                        commands.Add(name, record);
                    }

                    record.Count++;
                    if (!record.ParameterCounts.Contains(parameters.Count))
                        record.ParameterCounts.Add(parameters.Count);
                    record.Occurrences.Add(new JxqyScriptCommandOccurrence
                    {
                        ScriptPath = relative,
                        LineNumber = index + 1,
                        ParameterCount = parameters.Count,
                        Literal = literal
                    });
                    report.ParsedCommandCount++;
                }
            }

            report.Commands = commands.Values
                .OrderBy(command => command.Name, StringComparer.Ordinal)
                .ToList();
            foreach (JxqyScriptCommandRecord command in report.Commands)
                command.ParameterCounts.Sort();
            return report;
        }

        internal static List<string> ParseParameters(string value)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(value))
                return result;

            var current = new StringBuilder();
            bool inQuotes = false;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (character == '"')
                {
                    inQuotes = !inQuotes;
                    current.Append(character);
                    continue;
                }

                if (!inQuotes && (character == ',' || character == '，'))
                {
                    AddParameter(result, current);
                    continue;
                }

                current.Append(character);
            }
            AddParameter(result, current);
            return result;
        }

        private static void AddParameter(List<string> result, StringBuilder current)
        {
            string parameter = current.ToString().Trim();
            if (parameter.Length > 0)
                result.Add(parameter);
            current.Clear();
        }
    }
}
