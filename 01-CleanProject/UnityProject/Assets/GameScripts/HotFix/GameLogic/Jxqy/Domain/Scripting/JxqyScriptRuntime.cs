using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Jxqy.Domain.Scripting
{
    public enum JxqyScriptDiagnosticSeverity
    {
        Info,
        Warning,
        Error,
    }

    public sealed class JxqyScriptDiagnostic
    {
        public JxqyScriptDiagnostic(
            string code,
            JxqyScriptDiagnosticSeverity severity,
            string message,
            string sourcePath,
            int lineNumber,
            string literal)
        {
            Code = code ?? string.Empty;
            Severity = severity;
            Message = message ?? string.Empty;
            SourcePath = sourcePath ?? string.Empty;
            LineNumber = lineNumber;
            Literal = literal ?? string.Empty;
        }

        public string Code { get; }
        public JxqyScriptDiagnosticSeverity Severity { get; }
        public string Message { get; }
        public string SourcePath { get; }
        public int LineNumber { get; }
        public string Literal { get; }

        public override string ToString()
        {
            return $"{Code} {SourcePath}:{LineNumber} {Message}";
        }
    }

    public enum JxqyScriptInstructionKind
    {
        Empty,
        Comment,
        Label,
        Command,
    }

    public sealed class JxqyScriptInstruction
    {
        public JxqyScriptInstruction(
            JxqyScriptInstructionKind kind,
            string name,
            IReadOnlyList<string> parameters,
            string resultLabel,
            int lineNumber,
            string literal)
        {
            Kind = kind;
            Name = name ?? string.Empty;
            Parameters = parameters ?? Array.Empty<string>();
            ResultLabel = resultLabel ?? string.Empty;
            LineNumber = lineNumber;
            Literal = literal ?? string.Empty;
        }

        public JxqyScriptInstructionKind Kind { get; }
        public string Name { get; }
        public IReadOnlyList<string> Parameters { get; }
        public string ResultLabel { get; }
        public int LineNumber { get; }
        public string Literal { get; }
    }

    public sealed class JxqyScriptDocument
    {
        private readonly IReadOnlyDictionary<string, IReadOnlyList<int>>
            _labelPositions;

        public JxqyScriptDocument(
            string sourcePath,
            IReadOnlyList<JxqyScriptInstruction> instructions,
            IReadOnlyDictionary<string, int> labels,
            IReadOnlyDictionary<string, IReadOnlyList<int>> labelPositions,
            IReadOnlyList<JxqyScriptDiagnostic> diagnostics)
        {
            SourcePath = sourcePath ?? string.Empty;
            Instructions = instructions ??
                           throw new ArgumentNullException(
                               nameof(instructions));
            Labels = labels ??
                     throw new ArgumentNullException(nameof(labels));
            _labelPositions = labelPositions ??
                              throw new ArgumentNullException(
                                  nameof(labelPositions));
            Diagnostics = diagnostics ??
                          throw new ArgumentNullException(
                              nameof(diagnostics));
        }

        public string SourcePath { get; }
        public IReadOnlyList<JxqyScriptInstruction> Instructions { get; }
        public IReadOnlyDictionary<string, int> Labels { get; }
        public IReadOnlyList<JxqyScriptDiagnostic> Diagnostics { get; }
        public bool IsValid => Diagnostics.All(item =>
            item.Severity != JxqyScriptDiagnosticSeverity.Error);

        public bool TryGetLabel(string label, out int instructionIndex)
        {
            return Labels.TryGetValue(
                NormalizeLabel(label),
                out instructionIndex);
        }

        public bool TryGetLabel(
            string label,
            int sourceInstructionIndex,
            out int instructionIndex)
        {
            if (!_labelPositions.TryGetValue(
                    NormalizeLabel(label),
                    out IReadOnlyList<int> positions) ||
                positions.Count == 0)
            {
                instructionIndex = -1;
                return false;
            }

            // Legacy content repeatedly copies small local branch blocks with
            // identical label names. Resolve those branches to the next local
            // declaration; retain the original first-label fallback for
            // backward jumps and scripts with one declaration.
            for (int index = 0; index < positions.Count; index++)
            {
                if (positions[index] > sourceInstructionIndex)
                {
                    instructionIndex = positions[index];
                    return true;
                }
            }

            instructionIndex = positions[0];
            return true;
        }

        internal static string NormalizeLabel(string label)
        {
            return (label ?? string.Empty).Trim().TrimStart('@').TrimEnd(':');
        }
    }

    public static class JxqyScriptParser
    {
        public static JxqyScriptDocument Parse(
            string source,
            string sourcePath = "")
        {
            source ??= string.Empty;
            string normalized = source
                .Replace("\r\n", "\n")
                .Replace('\r', '\n');
            string[] lines = normalized.Split('\n');
            var instructions = new List<JxqyScriptInstruction>(lines.Length);
            var labels = new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);
            var labelPositions = new Dictionary<string, List<int>>(
                StringComparer.OrdinalIgnoreCase);
            var diagnostics = new List<JxqyScriptDiagnostic>();

            for (int index = 0; index < lines.Length; index++)
            {
                JxqyScriptInstruction instruction = ParseLine(
                    lines[index],
                    index + 1,
                    sourcePath,
                    diagnostics);
                instructions.Add(instruction);
                if (instruction.Kind != JxqyScriptInstructionKind.Label)
                    continue;
                string label =
                    JxqyScriptDocument.NormalizeLabel(instruction.Name);
                if (!labelPositions.TryGetValue(
                        label,
                        out List<int> positions))
                {
                    positions = new List<int>();
                    labelPositions.Add(label, positions);
                }
                positions.Add(index);
                labels.TryAdd(label, index);
            }

            var readOnlyLabelPositions = labelPositions.ToDictionary(
                item => item.Key,
                item => (IReadOnlyList<int>)item.Value,
                StringComparer.OrdinalIgnoreCase);

            return new JxqyScriptDocument(
                sourcePath,
                instructions,
                labels,
                readOnlyLabelPositions,
                diagnostics);
        }

        private static JxqyScriptInstruction ParseLine(
            string literal,
            int lineNumber,
            string sourcePath,
            List<JxqyScriptDiagnostic> diagnostics)
        {
            string trimmed = (literal ?? string.Empty).Trim();
            if (trimmed.Length == 0)
            {
                return CreateSimple(
                    JxqyScriptInstructionKind.Empty,
                    lineNumber,
                    literal);
            }
            if (trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                return CreateSimple(
                    JxqyScriptInstructionKind.Comment,
                    lineNumber,
                    literal);
            }

            string code = StripInlineComment(trimmed);
            if (code.StartsWith("@", StringComparison.Ordinal))
            {
                int separator = code.IndexOf(':');
                if (separator < 0)
                {
                    separator = FindLegacyLabelSeparator(code);
                    if (separator > 1)
                    {
                        diagnostics.Add(new JxqyScriptDiagnostic(
                            "JXQY-SCRIPT-LEGACY-REPAIR",
                            JxqyScriptDiagnosticSeverity.Warning,
                            "Accepted legacy full-width/semicolon label " +
                            "terminator.",
                            sourcePath,
                            lineNumber,
                            literal));
                    }
                }
                string label = separator > 1
                    ? code.Substring(1, separator - 1).Trim()
                    : string.Empty;
                if (!IsIdentifier(label, allowLeadingDigit: true))
                {
                    AddParseError(
                        diagnostics,
                        sourcePath,
                        lineNumber,
                        literal,
                        "Invalid label.");
                }
                return new JxqyScriptInstruction(
                    JxqyScriptInstructionKind.Label,
                    label,
                    Array.Empty<string>(),
                    string.Empty,
                    lineNumber,
                    literal);
            }

            int open = code.IndexOf('(');
            if (open < 0 && TryRepairLegacyOpeningParenthesis(
                    code,
                    out string punctuationRepaired))
            {
                diagnostics.Add(new JxqyScriptDiagnostic(
                    "JXQY-SCRIPT-LEGACY-REPAIR",
                    JxqyScriptDiagnosticSeverity.Warning,
                    "Accepted legacy full-width command parenthesis.",
                    sourcePath,
                    lineNumber,
                    literal));
                code = punctuationRepaired;
                open = code.IndexOf('(');
            }
            string name;
            IReadOnlyList<string> parameters = Array.Empty<string>();
            string result = string.Empty;
            if (open < 0)
            {
                string statement = code.Trim().TrimEnd(';').Trim();
                int separator = IndexOfWhitespace(statement);
                if (separator < 0)
                {
                    name = statement;
                }
                else
                {
                    name = statement.Substring(0, separator).Trim();
                    string argument = statement.Substring(separator).Trim();
                    if (argument.Length > 0)
                        parameters = new[] { argument };
                }
            }
            else
            {
                name = code.Substring(0, open).Trim();
                int close = FindClosingParenthesis(code, open + 1);
                if (close < 0 &&
                    TryRepairLegacyUnderscoreTerminator(
                        code,
                        out string repaired))
                {
                    diagnostics.Add(new JxqyScriptDiagnostic(
                        "JXQY-SCRIPT-LEGACY-REPAIR",
                        JxqyScriptDiagnosticSeverity.Warning,
                        "Repaired legacy '_;' command terminator.",
                        sourcePath,
                        lineNumber,
                        literal));
                    code = repaired;
                    close = FindClosingParenthesis(code, open + 1);
                }
                if (close < 0)
                {
                    AddParseError(
                        diagnostics,
                        sourcePath,
                        lineNumber,
                        literal,
                        "Missing closing parenthesis or quote.");
                    close = code.Length;
                }
                string parameterText = code.Substring(
                    open + 1,
                    Math.Max(0, close - open - 1));
                parameters = ParseParameters(
                    parameterText,
                    sourcePath,
                    lineNumber,
                    literal,
                    diagnostics);
                if (close < code.Length)
                {
                    string tail = code.Substring(close + 1)
                        .Trim()
                        .TrimEnd(';')
                        .Trim();
                    if (tail.StartsWith("@", StringComparison.Ordinal))
                        result = JxqyScriptDocument.NormalizeLabel(tail);
                    else if (tail.Equals(
                                 "Return",
                                 StringComparison.OrdinalIgnoreCase))
                        result = "Return";
                }
            }

            if (!IsIdentifier(name, allowLeadingDigit: false))
            {
                AddParseError(
                    diagnostics,
                    sourcePath,
                    lineNumber,
                    literal,
                    "Invalid command name.");
            }
            return new JxqyScriptInstruction(
                JxqyScriptInstructionKind.Command,
                name,
                parameters,
                result,
                lineNumber,
                literal);
        }

        private static int IndexOfWhitespace(string value)
        {
            for (int index = 0; index < value.Length; index++)
            {
                if (char.IsWhiteSpace(value[index]))
                    return index;
            }
            return -1;
        }

        private static bool TryRepairLegacyUnderscoreTerminator(
            string value,
            out string repaired)
        {
            repaired = value;
            if (!value.EndsWith("_;", StringComparison.Ordinal))
                return false;
            repaired = value.Substring(0, value.Length - 2) + ");";
            return true;
        }

        private static int FindLegacyLabelSeparator(string value)
        {
            int fullWidthColon = value.IndexOf('：');
            if (fullWidthColon > 1)
                return fullWidthColon;
            int semicolon = value.IndexOf(';');
            return semicolon > 1 ? semicolon : -1;
        }

        private static bool TryRepairLegacyOpeningParenthesis(
            string value,
            out string repaired)
        {
            repaired = value;
            int fullWidthOpen = value.IndexOf('（');
            int firstQuote = value.IndexOf('"');
            if (fullWidthOpen <= 0 ||
                (firstQuote >= 0 && fullWidthOpen > firstQuote))
            {
                return false;
            }
            repaired = value.Substring(0, fullWidthOpen) + "(" +
                       value.Substring(fullWidthOpen + 1);
            return true;
        }

        private static IReadOnlyList<string> ParseParameters(
            string value,
            string sourcePath,
            int lineNumber,
            string literal,
            List<JxqyScriptDiagnostic> diagnostics)
        {
            var parameters = new List<string>();
            var current = new StringBuilder();
            bool quoted = false;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (character == '"')
                {
                    quoted = !quoted;
                    current.Append(character);
                    continue;
                }
                if (!quoted && (character == ',' || character == '，'))
                {
                    AddParameter(parameters, current);
                    continue;
                }
                if (quoted || !char.IsWhiteSpace(character))
                    current.Append(character);
            }
            if (quoted)
            {
                AddParseError(
                    diagnostics,
                    sourcePath,
                    lineNumber,
                    literal,
                    "Unterminated quoted parameter.");
            }
            AddParameter(parameters, current);
            return parameters;
        }

        private static void AddParameter(
            List<string> parameters,
            StringBuilder current)
        {
            if (current.Length > 0)
                parameters.Add(current.ToString());
            current.Clear();
        }

        private static int FindClosingParenthesis(string value, int start)
        {
            bool quoted = false;
            for (int index = start; index < value.Length; index++)
            {
                if (value[index] == '"')
                    quoted = !quoted;
                else if (!quoted && value[index] == ')')
                    return index;
            }
            return -1;
        }

        private static string StripInlineComment(string value)
        {
            bool quoted = false;
            for (int index = 0; index < value.Length - 1; index++)
            {
                if (value[index] == '"')
                    quoted = !quoted;
                if (!quoted && value[index] == '/' &&
                    value[index + 1] == '/')
                    return value.Substring(0, index).Trim();
            }
            return value;
        }

        private static bool IsIdentifier(
            string value,
            bool allowLeadingDigit)
        {
            if (string.IsNullOrEmpty(value))
                return false;
            if (!allowLeadingDigit &&
                !char.IsLetter(value[0]) &&
                value[0] != '_')
                return false;
            foreach (char character in value)
            {
                if (!char.IsLetterOrDigit(character) && character != '_')
                    return false;
            }
            return true;
        }

        private static JxqyScriptInstruction CreateSimple(
            JxqyScriptInstructionKind kind,
            int lineNumber,
            string literal)
        {
            return new JxqyScriptInstruction(
                kind,
                string.Empty,
                Array.Empty<string>(),
                string.Empty,
                lineNumber,
                literal);
        }

        private static void AddParseError(
            List<JxqyScriptDiagnostic> diagnostics,
            string sourcePath,
            int lineNumber,
            string literal,
            string message)
        {
            diagnostics.Add(new JxqyScriptDiagnostic(
                "JXQY-SCRIPT-PARSE",
                JxqyScriptDiagnosticSeverity.Error,
                message,
                sourcePath,
                lineNumber,
                literal));
        }
    }

    public sealed class JxqyScriptContext
    {
        private readonly Dictionary<Type, object> _services =
            new Dictionary<Type, object>();

        public object Owner { get; set; }

        public void SetService<T>(T service) where T : class
        {
            if (service == null)
                _services.Remove(typeof(T));
            else
                _services[typeof(T)] = service;
        }

        public T GetService<T>() where T : class
        {
            return _services.TryGetValue(typeof(T), out object service)
                ? (T)service
                : null;
        }
    }

    public abstract class JxqyScriptWait
    {
        public abstract bool Tick(double elapsedMilliseconds);
    }

    public sealed class JxqyTimedScriptWait : JxqyScriptWait
    {
        private double _remainingMilliseconds;

        public JxqyTimedScriptWait(double milliseconds)
        {
            if (milliseconds < 0 || double.IsNaN(milliseconds) ||
                double.IsInfinity(milliseconds))
                throw new ArgumentOutOfRangeException(nameof(milliseconds));
            _remainingMilliseconds = milliseconds;
        }

        public double RemainingMilliseconds =>
            Math.Max(0, _remainingMilliseconds);

        public override bool Tick(double elapsedMilliseconds)
        {
            if (elapsedMilliseconds < 0 ||
                double.IsNaN(elapsedMilliseconds) ||
                double.IsInfinity(elapsedMilliseconds))
                throw new ArgumentOutOfRangeException(
                    nameof(elapsedMilliseconds));
            _remainingMilliseconds -= elapsedMilliseconds;
            return _remainingMilliseconds <= 0;
        }
    }

    public sealed class JxqyPredicateScriptWait : JxqyScriptWait
    {
        private readonly Func<bool> _isComplete;

        public JxqyPredicateScriptWait(Func<bool> isComplete)
        {
            _isComplete = isComplete ??
                          throw new ArgumentNullException(
                              nameof(isComplete));
        }

        public override bool Tick(double elapsedMilliseconds)
        {
            return _isComplete();
        }
    }

    public enum JxqyScriptStepKind
    {
        Continue,
        Wait,
        Jump,
        Return,
        Fault,
    }

    public readonly struct JxqyScriptStep
    {
        private JxqyScriptStep(
            JxqyScriptStepKind kind,
            JxqyScriptWait wait,
            string label,
            string error)
        {
            Kind = kind;
            Wait = wait;
            Label = label ?? string.Empty;
            Error = error ?? string.Empty;
        }

        public JxqyScriptStepKind Kind { get; }
        public JxqyScriptWait Wait { get; }
        public string Label { get; }
        public string Error { get; }

        public static JxqyScriptStep Continue()
        {
            return new JxqyScriptStep(
                JxqyScriptStepKind.Continue,
                null,
                null,
                null);
        }

        public static JxqyScriptStep WaitFor(JxqyScriptWait wait)
        {
            return new JxqyScriptStep(
                JxqyScriptStepKind.Wait,
                wait ?? throw new ArgumentNullException(nameof(wait)),
                null,
                null);
        }

        public static JxqyScriptStep JumpTo(string label)
        {
            return new JxqyScriptStep(
                JxqyScriptStepKind.Jump,
                null,
                label,
                null);
        }

        public static JxqyScriptStep Return()
        {
            return new JxqyScriptStep(
                JxqyScriptStepKind.Return,
                null,
                null,
                null);
        }

        public static JxqyScriptStep Fault(string error)
        {
            return new JxqyScriptStep(
                JxqyScriptStepKind.Fault,
                null,
                null,
                error);
        }
    }

    public delegate JxqyScriptStep JxqyScriptCommandHandler(
        JxqyScriptContext context,
        JxqyScriptInstruction instruction);

    public sealed class JxqyScriptCommandRegistry
    {
        private readonly Dictionary<string, JxqyScriptCommandHandler> _handlers =
            new Dictionary<string, JxqyScriptCommandHandler>(
                StringComparer.OrdinalIgnoreCase);

        public IReadOnlyCollection<string> CommandNames => _handlers.Keys;

        public bool Contains(string commandName)
        {
            return !string.IsNullOrWhiteSpace(commandName) &&
                   _handlers.ContainsKey(commandName);
        }

        public void Register(
            string commandName,
            JxqyScriptCommandHandler handler)
        {
            if (string.IsNullOrWhiteSpace(commandName))
                throw new ArgumentException(
                    "Command name is required.",
                    nameof(commandName));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
            if (!_handlers.TryAdd(commandName, handler))
                throw new InvalidOperationException(
                    $"Command '{commandName}' is already registered.");
        }

        public bool TryExecute(
            JxqyScriptContext context,
            JxqyScriptInstruction instruction,
            out JxqyScriptStep result)
        {
            if (!_handlers.TryGetValue(
                    instruction.Name,
                    out JxqyScriptCommandHandler handler))
            {
                result = JxqyScriptStep.Fault(
                    $"Command '{instruction.Name}' is not registered.");
                return false;
            }
            result = handler(context, instruction);
            return true;
        }
    }

    public enum JxqyScriptRunnerState
    {
        Ready,
        Running,
        Waiting,
        Completed,
        Faulted,
    }

    public sealed class JxqyScriptRunner
    {
        private readonly JxqyScriptDocument _document;
        private readonly JxqyScriptCommandRegistry _registry;
        private readonly JxqyScriptContext _context;
        private readonly List<JxqyScriptDiagnostic> _diagnostics =
            new List<JxqyScriptDiagnostic>();
        private int _instructionIndex;
        private JxqyScriptWait _wait;

        public JxqyScriptRunner(
            JxqyScriptDocument document,
            JxqyScriptCommandRegistry registry,
            JxqyScriptContext context = null)
        {
            _document = document ??
                        throw new ArgumentNullException(nameof(document));
            _registry = registry ??
                        throw new ArgumentNullException(nameof(registry));
            _context = context ?? new JxqyScriptContext();
            _diagnostics.AddRange(document.Diagnostics);
            State = document.IsValid
                ? JxqyScriptRunnerState.Ready
                : JxqyScriptRunnerState.Faulted;
        }

        public JxqyScriptRunnerState State { get; private set; }
        public bool IsFinished =>
            State == JxqyScriptRunnerState.Completed ||
            State == JxqyScriptRunnerState.Faulted;
        public int InstructionIndex => _instructionIndex;
        public IReadOnlyList<JxqyScriptDiagnostic> Diagnostics => _diagnostics;

        public JxqyScriptRunnerState Tick(
            double elapsedMilliseconds,
            int instructionBudget = 1024)
        {
            if (elapsedMilliseconds < 0 ||
                double.IsNaN(elapsedMilliseconds) ||
                double.IsInfinity(elapsedMilliseconds))
                throw new ArgumentOutOfRangeException(
                    nameof(elapsedMilliseconds));
            if (instructionBudget <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(instructionBudget));
            if (IsFinished)
                return State;

            if (_wait != null)
            {
                bool waitComplete;
                try
                {
                    waitComplete = _wait.Tick(elapsedMilliseconds);
                }
                catch (Exception exception)
                {
                    AddDiagnostic(
                        "JXQY-SCRIPT-WAIT-ERROR",
                        JxqyScriptDiagnosticSeverity.Error,
                        exception.Message,
                        CurrentInstruction);
                    State = JxqyScriptRunnerState.Faulted;
                    return State;
                }
                if (!waitComplete)
                {
                    State = JxqyScriptRunnerState.Waiting;
                    return State;
                }
                _wait = null;
                _instructionIndex++;
            }

            State = JxqyScriptRunnerState.Running;
            int executed = 0;
            while (_instructionIndex < _document.Instructions.Count)
            {
                if (executed++ >= instructionBudget)
                {
                    AddDiagnostic(
                        "JXQY-SCRIPT-BUDGET",
                        JxqyScriptDiagnosticSeverity.Warning,
                        "Instruction budget exhausted; execution will resume next tick.",
                        CurrentInstruction);
                    return State;
                }

                JxqyScriptInstruction instruction = CurrentInstruction;
                if (instruction.Kind != JxqyScriptInstructionKind.Command)
                {
                    _instructionIndex++;
                    continue;
                }

                if (instruction.Name.Equals(
                        "Return",
                        StringComparison.OrdinalIgnoreCase))
                {
                    State = JxqyScriptRunnerState.Completed;
                    return State;
                }
                if (instruction.Name.Equals(
                        "Goto",
                        StringComparison.OrdinalIgnoreCase))
                {
                    string label = instruction.Parameters.Count > 0
                        ? instruction.Parameters[0]
                        : instruction.ResultLabel;
                    if (!JumpTo(label, instruction))
                        return State;
                    continue;
                }

                JxqyScriptStep step;
                try
                {
                    if (!_registry.TryExecute(
                            _context,
                            instruction,
                            out step))
                    {
                        AddDiagnostic(
                            "JXQY-SCRIPT-UNIMPLEMENTED",
                            JxqyScriptDiagnosticSeverity.Error,
                            step.Error,
                            instruction);
                        State = JxqyScriptRunnerState.Faulted;
                        return State;
                    }
                }
                catch (Exception exception)
                {
                    AddDiagnostic(
                        "JXQY-SCRIPT-COMMAND-ERROR",
                        JxqyScriptDiagnosticSeverity.Error,
                        exception.Message,
                        instruction);
                    State = JxqyScriptRunnerState.Faulted;
                    return State;
                }

                switch (step.Kind)
                {
                    case JxqyScriptStepKind.Continue:
                        _instructionIndex++;
                        break;
                    case JxqyScriptStepKind.Wait:
                        _wait = step.Wait;
                        State = JxqyScriptRunnerState.Waiting;
                        return State;
                    case JxqyScriptStepKind.Jump:
                        if (!JumpTo(step.Label, instruction))
                            return State;
                        break;
                    case JxqyScriptStepKind.Return:
                        State = JxqyScriptRunnerState.Completed;
                        return State;
                    case JxqyScriptStepKind.Fault:
                        AddDiagnostic(
                            "JXQY-SCRIPT-COMMAND-FAULT",
                            JxqyScriptDiagnosticSeverity.Error,
                            step.Error,
                            instruction);
                        State = JxqyScriptRunnerState.Faulted;
                        return State;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            State = JxqyScriptRunnerState.Completed;
            return State;
        }

        private JxqyScriptInstruction CurrentInstruction =>
            _document.Instructions[_instructionIndex];

        private bool JumpTo(
            string label,
            JxqyScriptInstruction instruction)
        {
            if (!_document.TryGetLabel(
                    label,
                    _instructionIndex,
                    out int target))
            {
                // The original runner scans to the end of the file when a
                // jump target is absent, which completes the script without
                // faulting. Several shipped scripts use that as an implicit
                // return (most commonly `Goto @end` without an @end label).
                _instructionIndex = _document.Instructions.Count;
                return true;
            }
            _instructionIndex = target + 1;
            return true;
        }

        private void AddDiagnostic(
            string code,
            JxqyScriptDiagnosticSeverity severity,
            string message,
            JxqyScriptInstruction instruction)
        {
            _diagnostics.Add(new JxqyScriptDiagnostic(
                code,
                severity,
                message,
                _document.SourcePath,
                instruction.LineNumber,
                instruction.Literal));
        }
    }

    public sealed class JxqyScriptScheduler
    {
        private readonly LinkedList<JxqyScriptRunner> _serial =
            new LinkedList<JxqyScriptRunner>();
        private readonly List<ParallelJob> _parallel =
            new List<ParallelJob>();

        public int SerialCount => _serial.Count;
        public int ParallelCount => _parallel.Count;
        public bool IsRunningSerialScript => _serial.Count > 0;

        public void RunSerial(JxqyScriptRunner runner)
        {
            if (runner == null)
                throw new ArgumentNullException(nameof(runner));
            _serial.AddLast(runner);
        }

        public void RunParallel(
            JxqyScriptRunner runner,
            double delayMilliseconds = 0)
        {
            if (runner == null)
                throw new ArgumentNullException(nameof(runner));
            if (delayMilliseconds < 0 ||
                double.IsNaN(delayMilliseconds) ||
                double.IsInfinity(delayMilliseconds))
                throw new ArgumentOutOfRangeException(
                    nameof(delayMilliseconds));
            _parallel.Add(new ParallelJob(runner, delayMilliseconds));
        }

        public void Tick(
            double elapsedMilliseconds,
            bool gameplayPaused = false)
        {
            while (_serial.First != null)
            {
                JxqyScriptRunner runner = _serial.First.Value;
                runner.Tick(elapsedMilliseconds);
                if (!runner.IsFinished)
                    break;
                _serial.RemoveFirst();
                elapsedMilliseconds = 0;
            }

            if (gameplayPaused)
                return;
            int countAtStart = _parallel.Count;
            for (int index = countAtStart - 1; index >= 0; index--)
            {
                ParallelJob job = _parallel[index];
                job.DelayMilliseconds -= elapsedMilliseconds;
                if (job.DelayMilliseconds > 0)
                    continue;
                job.Runner.Tick(elapsedMilliseconds);
                if (job.Runner.IsFinished)
                    _parallel.RemoveAt(index);
            }
        }

        public void Clear()
        {
            _serial.Clear();
            _parallel.Clear();
        }

        private sealed class ParallelJob
        {
            public ParallelJob(
                JxqyScriptRunner runner,
                double delayMilliseconds)
            {
                Runner = runner;
                DelayMilliseconds = delayMilliseconds;
            }

            public JxqyScriptRunner Runner { get; }
            public double DelayMilliseconds { get; set; }
        }
    }
}
