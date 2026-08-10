using System;
using System.Collections.Generic;
using System.Text;
using Jxqy.Domain.Persistence;
using Jxqy.Domain.Presentation;
using Jxqy.Domain.World;

namespace Jxqy.Domain.Simulation
{
    public enum JxqyVerticalSliceStage
    {
        NotStarted,
        Exploring,
        FirstCombat,
        Dialogue,
        AwaitingMapSwitch,
        ExploringSecondMap,
        SaveCreated,
        StateMutated,
        Completed,
    }

    public enum JxqyVerticalSliceCheckpointKind
    {
        NewGame,
        FirstCombat,
        Dialogue,
        MapSwitch,
        Item,
        Skill,
        Shop,
        Save,
        Load,
    }

    public sealed class JxqyVerticalSliceCheckpoint
    {
        public JxqyVerticalSliceCheckpoint(
            JxqyVerticalSliceCheckpointKind kind,
            string expected,
            string actual,
            bool passed)
        {
            Kind = kind;
            Expected = expected ?? string.Empty;
            Actual = actual ?? string.Empty;
            Passed = passed;
        }

        public JxqyVerticalSliceCheckpointKind Kind { get; }
        public string Expected { get; }
        public string Actual { get; }
        public bool Passed { get; }
    }

    public sealed class JxqyVerticalSlice
    {
        public const string FirstMapStableId =
            "map:map/map_001_凌绝峰连接地图.map";
        public const string SecondMapStableId =
            "map:map/map_002_凌绝峰峰顶.map";

        private readonly List<JxqyVerticalSliceCheckpoint> _checkpoints =
            new List<JxqyVerticalSliceCheckpoint>();
        private readonly JxqyDeterministicRandom _random =
            new JxqyDeterministicRandom(0x4A585159);
        private JxqyCharacter _firstEnemy;
        private int _savedMoney;
        private bool _gameplayChecksCompleted;

        public JxqyVerticalSlice()
        {
            Player = new JxqyPlayer();
            Inventory = new JxqyInventory();
            Equipment = new JxqyEquipmentManager();
            Skills = new JxqySkillManager();
            Shop = new JxqyShop();
            Ui = new JxqyUiSession
            {
                Player = Player,
                Inventory = Inventory,
                Equipment = Equipment,
                Skills = Skills,
                Shop = Shop,
            };
            Ui.DialogueCompleted += CompleteDialogue;
        }

        public event Action<string> MapSwitchRequested;

        public JxqyVerticalSliceStage Stage { get; private set; }
        public string CurrentMapStableId { get; private set; } = string.Empty;
        public JxqyPlayer Player { get; }
        public JxqyInventory Inventory { get; }
        public JxqyEquipmentManager Equipment { get; }
        public JxqySkillManager Skills { get; }
        public JxqyShop Shop { get; }
        public JxqyUiSession Ui { get; }
        public JxqyDrop FirstDrop { get; private set; }
        public IReadOnlyList<JxqyVerticalSliceCheckpoint> Checkpoints =>
            _checkpoints;
        public bool IsComplete =>
            Stage == JxqyVerticalSliceStage.Completed &&
            _checkpoints.Count == 9 &&
            _checkpoints.TrueForAll(value => value.Passed);

        public void StartNewGame()
        {
            if (Stage != JxqyVerticalSliceStage.NotStarted)
                throw new InvalidOperationException("垂直切片已经启动。");
            Player.Name = "南宫飞云";
            Player.Kind = JxqyCharacterKind.Player;
            Player.Relation = JxqyRelationType.Friend;
            Player.Level = 1;
            Player.LifeMax = 100;
            Player.Life = 100;
            Player.ManaMax = 50;
            Player.Mana = 50;
            Player.ThewMax = 50;
            Player.Thew = 50;
            Player.Money = 100;
            Player.Attack = 20;
            Player.Evade = 200;
            Player.TilePosition = new JxqyIntPoint(4, 4);
            CurrentMapStableId = FirstMapStableId;
            Stage = JxqyVerticalSliceStage.Exploring;
            Record(
                JxqyVerticalSliceCheckpointKind.NewGame,
                FirstMapStableId + "|100/50/50",
                CurrentMapStableId +
                $"|{Player.Life}/{Player.Mana}/{Player.Thew}");
        }

        public void BeginFirstCombat()
        {
            Require(JxqyVerticalSliceStage.Exploring);
            _firstEnemy = new JxqyCharacter
            {
                Name = "凌绝峰守卫",
                Relation = JxqyRelationType.Enemy,
                Kind = JxqyCharacterKind.Fighter,
                LifeMax = 15,
                Life = 15,
                Level = 1,
                Evade = 0,
            };
            _firstEnemy.TilePosition = new JxqyIntPoint(5, 4);
            _firstEnemy.Died += OnFirstEnemyDied;
            Stage = JxqyVerticalSliceStage.FirstCombat;
        }

        public JxqyDamageResult AttackFirstEnemy()
        {
            Require(JxqyVerticalSliceStage.FirstCombat);
            return JxqyDamageCalculator.Resolve(
                Player,
                _firstEnemy,
                Player.Attack,
                Player.Attack2,
                Player.Attack3,
                0,
                _random,
                guaranteedHit: true);
        }

        public void CompleteMapSwitch(string mapStableId)
        {
            Require(JxqyVerticalSliceStage.AwaitingMapSwitch);
            CurrentMapStableId = mapStableId ?? string.Empty;
            Stage = JxqyVerticalSliceStage.ExploringSecondMap;
            Record(
                JxqyVerticalSliceCheckpointKind.MapSwitch,
                SecondMapStableId,
                CurrentMapStableId);
        }

        public JxqySaveGameData CreateSave()
        {
            Require(JxqyVerticalSliceStage.ExploringSecondMap);
            if (!_gameplayChecksCompleted)
                throw new InvalidOperationException(
                    "Item, skill and shop checkpoints must pass before save.");
            var save = new JxqySaveGameData
            {
                SourceFormat = "UnityJson",
            };
            save.World.Map = CurrentMapStableId;
            save.Player.TileColumn = Player.TilePosition.X;
            save.Player.TileRow = Player.TilePosition.Y;
            save.Player.Life = Player.Life;
            save.Player.Mana = Player.Mana;
            save.Player.Thew = Player.Thew;
            save.Player.PlayerDataJson =
                $"name={Player.Name};level={Player.Level};money={Player.Money}";
            save.Player.InventoryDataJson = SerializeInventory();
            save.Player.MagicDataJson = SerializeSkills();
            _savedMoney = Player.Money;
            Stage = JxqyVerticalSliceStage.SaveCreated;
            Record(
                JxqyVerticalSliceCheckpointKind.Save,
                SecondMapStableId + "|" + _savedMoney,
                save.World.Map + "|" + ParseValue(
                    save.Player.PlayerDataJson,
                    "money"));
            return save;
        }

        public void CompleteItemSkillAndShopCheckpoints()
        {
            Require(JxqyVerticalSliceStage.ExploringSecondMap);
            if (_gameplayChecksCompleted)
                throw new InvalidOperationException(
                    "Item, skill and shop checkpoints already completed.");

            var skill = new JxqyMagicDefinition
            {
                Id = "tutorial-skill",
                Effect = 10,
                ManaCost = 2,
            };
            bool learned = Skills.Learn(skill);
            Record(
                JxqyVerticalSliceCheckpointKind.Skill,
                "tutorial-skill|1",
                learned && Skills.Find(skill.Id) != null
                    ? $"{Skills.Find(skill.Id).Magic.Id}|" +
                      Skills.Find(skill.Id).Level
                    : "missing");

            var medicine = new JxqyItemDefinition
            {
                Id = "tutorial-medicine",
                Name = "Tutorial Medicine",
                Kind = JxqyItemKind.Drug,
                Life = 10,
                ExplicitCost = 10,
            };
            Shop.AddStock(medicine, 1);
            bool bought = Shop.Buy(
                medicine.Id,
                1,
                Player,
                Inventory);
            Record(
                JxqyVerticalSliceCheckpointKind.Shop,
                "money=90;item=1",
                $"money={Player.Money};item=" +
                $"{(bought ? Inventory.Count(medicine.Id) : 0)}");

            Player.Life = 80;
            bool used = Inventory.Use(medicine.Id, Player);
            Record(
                JxqyVerticalSliceCheckpointKind.Item,
                "life=90;item=0",
                $"life={Player.Life};item=" +
                $"{(used ? Inventory.Count(medicine.Id) : -1)}");
            _gameplayChecksCompleted = true;
        }

        public void MutateAfterSave()
        {
            Require(JxqyVerticalSliceStage.SaveCreated);
            Player.Money = 0;
            Player.Life = 1;
            Player.TilePosition = new JxqyIntPoint(0, 0);
            CurrentMapStableId = FirstMapStableId;
            Stage = JxqyVerticalSliceStage.StateMutated;
        }

        public void RestoreSave(JxqySaveGameData save)
        {
            Require(JxqyVerticalSliceStage.StateMutated);
            if (save == null)
                throw new ArgumentNullException(nameof(save));
            CurrentMapStableId = save.World.Map;
            Player.TilePosition = new JxqyIntPoint(
                save.Player.TileColumn,
                save.Player.TileRow);
            Player.Life = save.Player.Life;
            Player.Mana = save.Player.Mana;
            Player.Thew = save.Player.Thew;
            Player.Money = int.Parse(ParseValue(
                save.Player.PlayerDataJson,
                "money"));
            Stage = JxqyVerticalSliceStage.Completed;
            Record(
                JxqyVerticalSliceCheckpointKind.Load,
                SecondMapStableId + "|" + _savedMoney,
                CurrentMapStableId + "|" + Player.Money);
        }

        private void OnFirstEnemyDied(
            JxqyCharacter enemy,
            JxqyCharacter attacker)
        {
            FirstDrop = JxqyDropGenerator.Generate(enemy, _random);
            Record(
                JxqyVerticalSliceCheckpointKind.FirstCombat,
                "enemy=dead;player=alive",
                $"enemy={(enemy.IsDead ? "dead" : "alive")};" +
                $"player={(Player.IsDead ? "dead" : "alive")}");
            var dialogue = new JxqyDialogue();
            dialogue.Add(new JxqyDialoguePage
            {
                Speaker = "张琳心",
                Text = "前路已清，去凌绝峰顶吧。",
            });
            Stage = JxqyVerticalSliceStage.Dialogue;
            Ui.StartDialogue(dialogue);
        }

        private void CompleteDialogue(string choice)
        {
            if (Stage != JxqyVerticalSliceStage.Dialogue)
                return;
            Record(
                JxqyVerticalSliceCheckpointKind.Dialogue,
                "complete",
                "complete");
            Stage = JxqyVerticalSliceStage.AwaitingMapSwitch;
            MapSwitchRequested?.Invoke(SecondMapStableId);
        }

        private string SerializeInventory()
        {
            var builder = new StringBuilder();
            foreach (JxqyInventoryEntry entry in Inventory.Entries)
            {
                if (builder.Length > 0)
                    builder.Append(';');
                builder.Append(entry.Definition.Id)
                    .Append('=')
                    .Append(entry.Count);
            }
            return builder.ToString();
        }

        private string SerializeSkills()
        {
            var builder = new StringBuilder();
            foreach (JxqySkillEntry entry in Skills.Skills)
            {
                if (builder.Length > 0)
                    builder.Append(';');
                builder.Append(entry.Magic.Id)
                    .Append('=')
                    .Append(entry.Level);
            }
            return builder.ToString();
        }

        private static string ParseValue(string data, string key)
        {
            string prefix = key + "=";
            foreach (string pair in (data ?? string.Empty).Split(';'))
            {
                if (pair.StartsWith(prefix, StringComparison.Ordinal))
                    return pair.Substring(prefix.Length);
            }
            throw new FormatException($"缺少存档字段：{key}");
        }

        private void Require(JxqyVerticalSliceStage expected)
        {
            if (Stage != expected)
                throw new InvalidOperationException(
                    $"垂直切片阶段错误：期望 {expected}，实际 {Stage}。");
        }

        private void Record(
            JxqyVerticalSliceCheckpointKind kind,
            string expected,
            string actual)
        {
            _checkpoints.Add(new JxqyVerticalSliceCheckpoint(
                kind,
                expected,
                actual,
                string.Equals(expected, actual, StringComparison.Ordinal)));
        }
    }
}
