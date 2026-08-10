using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jxqy.Domain.Simulation;

namespace Jxqy.Domain.Scripting
{
    public sealed class JxqyScriptVariableStore
    {
        private readonly Dictionary<string, int> _values =
            new Dictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> Values => _values;

        public int Get(string name)
        {
            string key = Normalize(name);
            return _values.TryGetValue(key, out int value) ? value : 0;
        }

        public void Set(string name, int value)
        {
            _values[Normalize(name)] = value;
        }

        public int Add(string name, int amount)
        {
            string key = Normalize(name);
            int value = Get(key) + amount;
            _values[key] = value;
            return value;
        }

        public bool Remove(string name)
        {
            return _values.Remove(Normalize(name));
        }

        public void Clear()
        {
            _values.Clear();
        }

        private static string Normalize(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException(
                    "Variable name is required.",
                    nameof(name));
            string trimmed = name.Trim();
            return trimmed[0] == '$' ? trimmed : "$" + trimmed;
        }
    }

    public interface IJxqyLegacyScriptCommandPort
    {
        JxqyScriptStep Execute(
            JxqyScriptContext context,
            JxqyScriptInstruction instruction);
    }

    public static class JxqyLegacyScriptCommands
    {
        public static readonly string[] Names =
        {
            "Add",
            "AddAttack",
            "AddDefend",
            "AddExp",
            "AddEvade",
            "AddFlyInis",
            "AddGoods",
            "AddLife",
            "AddLifeMax",
            "AddMagic",
            "AddMana",
            "AddManaMax",
            "AddMoney",
            "AddMoveSpeedPercent",
            "AddNpc",
            "AddNpcProperty",
            "AddObj",
            "AddRandGoods",
            "AddRandMoney",
            "AddThew",
            "AddThewMax",
            "AddToMemo",
            "Assign",
            "BeginRain",
            "BuyGoods",
            "BuyGoodsOnly",
            "ChangeAsfColor",
            "ChangeMapColor",
            "Choose",
            "ChooseEx",
            "ChooseMultiple",
            "ChangeFlyIni",
            "ChangeFlyIni2",
            "CheckFreeGoodsSpace",
            "CheckFreeMagicSpace",
            "ClearAllSave",
            "ClearAllVar",
            "ClearBody",
            "ClearGoods",
            "ClearMagic",
            "CloseBox",
            "CloseTimeLimit",
            "CloseWaterEffect",
            "DelCurObj",
            "DelGoodByName",
            "DelGoods",
            "DelMagic",
            "DelMemo",
            "DelNpc",
            "DelObj",
            "DisableInput",
            "DisableDrop",
            "DisableFight",
            "DisableJump",
            "DisableNpcAI",
            "DisableRun",
            "DisableSave",
            "DisplayMessage",
            "EnabelDrop",
            "EnableFight",
            "EnableInput",
            "EnableJump",
            "EnableNpcAI",
            "EnableRun",
            "EnableSave",
            "EndRain",
            "EquipGoods",
            "FadeIn",
            "FadeOut",
            "FollowNpc",
            "FreeMap",
            "FrozenMillisecond",
            "FullLife",
            "FullMana",
            "FullThew",
            "GetGoodsNum",
            "GetGoodsNumByName",
            "GetExp",
            "GetMoneyNum",
            "GetNpcCount",
            "GetPartnerIdx",
            "GetPlayerMagicLevel",
            "GetPlayerState",
            "GetRandNum",
            "Goto",
            "HideTimerWnd",
            "IsEquipWeapon",
            "LimitMana",
            "LoadGame",
            "LoadMap",
            "LoadNpc",
            "LoadOneNpc",
            "LoadObj",
            "Memo",
            "MergeNpc",
            "MoveScreen",
            "MoveScreenEx",
            "NpcAttack",
            "NpcGoto",
            "NpcGotoDir",
            "NpcGotoEx",
            "NpcSpecialAction",
            "NpcSpecialActionEx",
            "OpenBox",
            "OpenObj",
            "OpenTimeLimit",
            "OpenWaterEffect",
            "PlayMovie",
            "PlayMusic",
            "PlaySound",
            "PlayerChange",
            "PlayerGoto",
            "PlayerGotoDir",
            "PlayerGotoEx",
            "PlayerJumpTo",
            "PlayerRunTo",
            "PlayerRunToEx",
            "PetrifyMillisecond",
            "PoisonMillisecond",
            "RandRun",
            "Return",
            "ReturnToTitle",
            "RunScript",
            "RunParallelScript",
            "SaveMapTrap",
            "SaveNpc",
            "SaveObj",
            "Say",
            "Select",
            "SellGoods",
            "SetLevelFile",
            "SetAllNpcDeathScript",
            "SetAllNpcScript",
            "SetDropIni",
            "SetKeepAttack",
            "SetMagicLevel",
            "SetMapPos",
            "SetMapTime",
            "SetMapTrap",
            "SetMoneyNum",
            "SetNpcAction",
            "SetNpcActionFile",
            "SetNpcActionType",
            "SetNpcDeathScript",
            "SetNpcDir",
            "SetNpcKind",
            "SetNpcLevel",
            "SetNpcMagicFile",
            "SetNpcMagicToUseWhenBeAttacked",
            "SetNpcDestination",
            "SetNpcPos",
            "SetNpcRelation",
            "SetNpcRes",
            "SetNpcScript",
            "SetObjOfs",
            "SetObjScript",
            "SetPlayerDir",
            "SetPlayerMagicToUseWhenBeAttacked",
            "SetPlayerPos",
            "SetPlayerScn",
            "SetPlayerState",
            "SetTimeScript",
            "SetTrap",
            "SetShowMapPos",
            "SetWalkIsRun",
            "SetmapTrap",
            "ShowMessage",
            "ShowNpc",
            "ShowSnow",
            "ShowSystemMsg",
            "Sleep",
            "StopMusic",
            "StopSound",
            "Talk",
            "Watch",
            "UseMagic",
        };

        public static JxqyScriptCommandRegistry CreateRegistry(
            IJxqyLegacyScriptCommandPort port,
            JxqyScriptVariableStore variables,
            JxqyDeterministicRandom random = null)
        {
            if (port == null)
                throw new ArgumentNullException(nameof(port));
            if (variables == null)
                throw new ArgumentNullException(nameof(variables));
            random ??= new JxqyDeterministicRandom(1);

            var registry = new JxqyScriptCommandRegistry();
            foreach (string name in Names)
            {
                if (registry.Contains(name))
                    continue;
                switch (name)
                {
                    case "Add":
                        registry.Register(name, (_, instruction) =>
                            Add(variables, instruction));
                        break;
                    case "Assign":
                        registry.Register(name, (_, instruction) =>
                            Assign(variables, instruction));
                        break;
                    case "GetRandNum":
                        registry.Register(name, (_, instruction) =>
                            GetRandom(variables, random, instruction));
                        break;
                    case "Sleep":
                        registry.Register(name, (_, instruction) =>
                            Sleep(instruction));
                        break;
                    case "Goto":
                        registry.Register(name, (_, instruction) =>
                            JxqyScriptStep.JumpTo(
                                RequireParameter(instruction, 0)));
                        break;
                    case "Return":
                        registry.Register(name, (_, __) =>
                            JxqyScriptStep.Return());
                        break;
                    default:
                        registry.Register(name, port.Execute);
                        break;
                }
            }
            registry.Register("If", (_, instruction) =>
                If(variables, instruction));
            return registry;
        }

        public static IReadOnlyList<string> FindUnregistered(
            IEnumerable<string> commandNames,
            JxqyScriptCommandRegistry registry)
        {
            if (commandNames == null)
                throw new ArgumentNullException(nameof(commandNames));
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));
            return commandNames
                .Where(name =>
                    !name.Equals(
                        "If",
                        StringComparison.OrdinalIgnoreCase) &&
                    !registry.Contains(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static JxqyScriptStep Add(
            JxqyScriptVariableStore variables,
            JxqyScriptInstruction instruction)
        {
            variables.Add(
                RequireParameter(instruction, 0),
                ParseInt(RequireParameter(instruction, 1)));
            return JxqyScriptStep.Continue();
        }

        private static JxqyScriptStep Assign(
            JxqyScriptVariableStore variables,
            JxqyScriptInstruction instruction)
        {
            variables.Set(
                RequireParameter(instruction, 0),
                ParseInt(RequireParameter(instruction, 1)));
            return JxqyScriptStep.Continue();
        }

        private static JxqyScriptStep GetRandom(
            JxqyScriptVariableStore variables,
            JxqyDeterministicRandom random,
            JxqyScriptInstruction instruction)
        {
            int minimum = ParseInt(RequireParameter(instruction, 1));
            int maximumInclusive =
                ParseInt(RequireParameter(instruction, 2));
            if (maximumInclusive < minimum)
                throw new FormatException(
                    "GetRandNum maximum must be >= minimum.");
            int maximumExclusive = checked(maximumInclusive + 1);
            variables.Set(
                RequireParameter(instruction, 0),
                random.Next(minimum, maximumExclusive));
            return JxqyScriptStep.Continue();
        }

        private static JxqyScriptStep Sleep(
            JxqyScriptInstruction instruction)
        {
            double milliseconds = double.Parse(
                RequireParameter(instruction, 0),
                NumberStyles.Float,
                CultureInfo.InvariantCulture);
            return JxqyScriptStep.WaitFor(
                new JxqyTimedScriptWait(milliseconds));
        }

        private static JxqyScriptStep If(
            JxqyScriptVariableStore variables,
            JxqyScriptInstruction instruction)
        {
            string expression = RequireParameter(instruction, 0);
            if (!TryParseComparison(
                    expression,
                    out string variable,
                    out string comparison,
                    out int expected))
                throw new FormatException(
                    $"Invalid If expression '{expression}'.");
            int actual = variables.Get(variable);
            bool matched = comparison switch
            {
                "==" => actual == expected,
                ">>" => actual > expected,
                ">=" => actual >= expected,
                "<<" => actual < expected,
                "<=" => actual <= expected,
                "<>" => actual != expected,
                _ => false,
            };
            if (!matched)
                return JxqyScriptStep.Continue();
            return instruction.ResultLabel.Equals(
                "Return",
                StringComparison.OrdinalIgnoreCase)
                ? JxqyScriptStep.Return()
                : JxqyScriptStep.JumpTo(instruction.ResultLabel);
        }

        private static bool TryParseComparison(
            string expression,
            out string variable,
            out string comparison,
            out int expected)
        {
            variable = string.Empty;
            comparison = string.Empty;
            expected = 0;
            string[] operators = { "==", ">>", ">=", "<<", "<=", "<>" };
            foreach (string candidate in operators)
            {
                int index = expression.IndexOf(
                    candidate,
                    StringComparison.Ordinal);
                if (index <= 0)
                    continue;
                variable = expression.Substring(0, index).Trim();
                comparison = candidate;
                string right = expression.Substring(index + candidate.Length)
                    .Trim();
                return int.TryParse(
                    right,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out expected);
            }
            return false;
        }

        private static string RequireParameter(
            JxqyScriptInstruction instruction,
            int index)
        {
            if (instruction.Parameters.Count <= index)
                throw new FormatException(
                    $"{instruction.Name} requires parameter {index + 1}.");
            return instruction.Parameters[index];
        }

        private static int ParseInt(string value)
        {
            return int.Parse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture);
        }
    }
}
