using System;
using System.Collections.Generic;
using Jxqy.Domain.Persistence;
using Jxqy.Domain.Simulation;
using Jxqy.Domain.World;
using UnityEngine;

namespace Jxqy.UnityAdapters
{
    public sealed class JxqyRestoredGameplayState
    {
        public JxqyInventory Inventory { get; set; }
        public JxqyEquipmentManager Equipment { get; set; }
        public JxqySkillManager Skills { get; set; }
    }

    public static class JxqyRuntimeSaveCodec
    {
        public static JxqySaveGameData Capture(
            string mapStableId,
            JxqyPlayer player,
            JxqyInventory inventory,
            JxqyEquipmentManager equipment,
            JxqySkillManager skills)
        {
            if (player == null)
                throw new ArgumentNullException(nameof(player));
            if (inventory == null)
                throw new ArgumentNullException(nameof(inventory));
            if (equipment == null)
                throw new ArgumentNullException(nameof(equipment));
            if (skills == null)
                throw new ArgumentNullException(nameof(skills));

            var save = new JxqySaveGameData
            {
                SourceFormat = "UnityJson",
            };
            save.World.Map = mapStableId ?? string.Empty;
            save.Player.Direction = player.CurrentDirection;
            save.Player.TileColumn = player.TilePosition.X;
            save.Player.TileRow = player.TilePosition.Y;
            save.Player.Life = player.Life;
            save.Player.Mana = player.Mana;
            save.Player.Thew = player.Thew;
            save.Player.PlayerDataJson = JsonUtility.ToJson(
                SavedPlayer.From(player, equipment));
            save.Player.InventoryDataJson = JsonUtility.ToJson(
                SavedInventory.From(inventory));
            save.Player.MagicDataJson = JsonUtility.ToJson(
                SavedSkills.From(skills));
            return save;
        }

        public static JxqyRestoredGameplayState Restore(
            JxqySaveGameData save,
            JxqyPlayer player)
        {
            if (save == null)
                throw new ArgumentNullException(nameof(save));
            if (player == null)
                throw new ArgumentNullException(nameof(player));

            SavedPlayer playerData = Deserialize<SavedPlayer>(
                save.Player.PlayerDataJson,
                "player");
            SavedInventory inventoryData = Deserialize<SavedInventory>(
                save.Player.InventoryDataJson,
                "inventory");
            SavedSkills skillsData = Deserialize<SavedSkills>(
                save.Player.MagicDataJson,
                "skills");

            player.PrepareForRestore();
            playerData.ApplyTo(player);
            player.TilePosition = new JxqyIntPoint(
                save.Player.TileColumn,
                save.Player.TileRow);
            player.CurrentDirection = save.Player.Direction;
            player.Life = save.Player.Life;
            player.Mana = save.Player.Mana;
            player.Thew = save.Player.Thew;

            var inventory = new JxqyInventory();
            foreach (SavedInventoryEntry entry in inventoryData.Entries)
            {
                inventory.RestoreEntry(
                    entry.Item.ToDefinition(),
                    Math.Max(1, entry.Count),
                    entry.CooldownMilliseconds,
                    entry.LegacyListIndex);
            }

            var equipment = new JxqyEquipmentManager();
            if (playerData.EquippedEntries != null &&
                playerData.EquippedEntries.Count > 0)
            {
                foreach (SavedInventoryEntry entry in
                         playerData.EquippedEntries)
                {
                    equipment.RestoreEquippedEntry(
                        entry.Item.ToDefinition(),
                        Math.Max(1, entry.Count),
                        entry.CooldownMilliseconds,
                        entry.LegacyListIndex);
                }
            }
            else
            {
                foreach (SavedItem item in playerData.Equipped)
                    equipment.RestoreEquipped(item.ToDefinition());
            }

            var skills = new JxqySkillManager();
            foreach (SavedSkill entry in skillsData.Entries)
            {
                skills.RestoreEntry(
                    entry.Magic.ToDefinition(),
                    entry.Level,
                    entry.Experience,
                    entry.CooldownMilliseconds,
                    entry.HideCount,
                    entry.LegacyListIndex);
            }

            return new JxqyRestoredGameplayState
            {
                Inventory = inventory,
                Equipment = equipment,
                Skills = skills,
            };
        }

        public static JxqySavePlayerProfileState CapturePlayerProfile(
            int playerIndex,
            JxqyPlayer player,
            JxqyInventory inventory,
            JxqyEquipmentManager equipment,
            JxqySkillManager skills)
        {
            JxqySaveGameData save = Capture(
                string.Empty,
                player,
                inventory,
                equipment,
                skills);
            return new JxqySavePlayerProfileState
            {
                PlayerIndex = playerIndex,
                Direction = save.Player.Direction,
                TileColumn = save.Player.TileColumn,
                TileRow = save.Player.TileRow,
                Life = save.Player.Life,
                Mana = save.Player.Mana,
                Thew = save.Player.Thew,
                PlayerDataJson = save.Player.PlayerDataJson,
                InventoryDataJson = save.Player.InventoryDataJson,
                MagicDataJson = save.Player.MagicDataJson,
            };
        }

        public static JxqyRestoredGameplayState RestorePlayerProfile(
            JxqySavePlayerProfileState profile,
            JxqyPlayer player)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));
            var save = new JxqySaveGameData();
            save.Player.Direction = profile.Direction;
            save.Player.TileColumn = profile.TileColumn;
            save.Player.TileRow = profile.TileRow;
            save.Player.Life = profile.Life;
            save.Player.Mana = profile.Mana;
            save.Player.Thew = profile.Thew;
            save.Player.PlayerDataJson = profile.PlayerDataJson;
            save.Player.InventoryDataJson = profile.InventoryDataJson;
            save.Player.MagicDataJson = profile.MagicDataJson;
            return Restore(save, player);
        }

        private static T Deserialize<T>(string json, string label)
            where T : class
        {
            if (string.IsNullOrWhiteSpace(json))
                return Activator.CreateInstance<T>();
            T value = JsonUtility.FromJson<T>(json);
            return value ?? throw new InvalidOperationException(
                $"Invalid {label} state in save.");
        }

        [Serializable]
        private sealed class SavedPlayer
        {
            public string Name = string.Empty;
            public int LifeMax;
            public int ThewMax;
            public int ManaMax;
            public int Attack;
            public int Attack2;
            public int Attack3;
            public int Defend;
            public int Defend2;
            public int Defend3;
            public int Evade;
            public int Relation;
            public int Experience;
            public int ExpBonus;
            public int LevelUpExperience;
            public int Level;
            public int AttackLevel = 1;
            public int DialogRadius = 1;
            public int Money;
            public bool WalkIsRun;
            public bool IsNotUseThewWhenRun;
            public bool IsManaRestore;
            public bool ManaLimit;
            public bool HasExtendedState;
            public bool HasActionState;
            public int State;
            public bool IsInFighting;
            public bool IsVisible = true;
            public bool Invincible;
            public bool NoDropWhenDead;
            public string DropIni = string.Empty;
            public string ScriptAddress = string.Empty;
            public string DeathScriptAddress = string.Empty;
            public string MagicFileName = string.Empty;
            public string MagicFileName2 = string.Empty;
            public string RetaliationMagicFileName = string.Empty;
            public int MagicDirectionWhenBeAttacked;
            public int AddLifeRestorePercent;
            public int AddThewRestorePercent;
            public int AddManaRestorePercent;
            public List<SavedRangedMagic> AdditionalBasicMagics = new();
            public bool IsPetrified;
            public float FrozenSeconds;
            public float PetrifiedSeconds;
            public float PoisonSeconds;
            public bool IsFrozenVisualEffect;
            public bool IsPoisonVisualEffect;
            public bool IsPetrifiedVisualEffect;
            public bool IsInTransport;
            public bool IsMovementDisabled;
            public bool IsRunDisabled;
            public bool IsJumpDisabled;
            public bool IsFightDisabled;
            public int AddMoveSpeedPercent;
            public int ChangeMoveSpeedPercent;
            public int RunSpeedFold;
            public int WalkSpeed;
            public List<SavedItem> Equipped = new();
            public List<SavedInventoryEntry> EquippedEntries = new();

            public static SavedPlayer From(
                JxqyPlayer player,
                JxqyEquipmentManager equipment)
            {
                var result = new SavedPlayer
                {
                    Name = player.Name,
                    LifeMax = player.LifeMax,
                    ThewMax = player.ThewMax,
                    ManaMax = player.ManaMax,
                    Attack = player.Attack,
                    Attack2 = player.Attack2,
                    Attack3 = player.Attack3,
                    Defend = player.Defend,
                    Defend2 = player.Defend2,
                    Defend3 = player.Defend3,
                    Evade = player.Evade,
                    Relation = (int)player.Relation,
                    Experience = player.Experience,
                    ExpBonus = player.ExpBonus,
                    LevelUpExperience = player.LevelUpExperience,
                    Level = player.Level,
                    AttackLevel = player.AttackLevel,
                    DialogRadius = player.DialogRadius,
                    Money = player.Money,
                    WalkIsRun = player.WalkIsRun,
                    IsNotUseThewWhenRun = player.IsNotUseThewWhenRun,
                    IsManaRestore = player.IsManaRestore,
                    ManaLimit = player.ManaLimit,
                    HasExtendedState = true,
                    HasActionState = true,
                    State = (int)player.State,
                    IsInFighting = player.IsInFighting,
                    IsVisible = player.IsVisible,
                    Invincible = player.Invincible,
                    NoDropWhenDead = player.NoDropWhenDead,
                    DropIni = player.DropIni,
                    ScriptAddress = player.ScriptAddress,
                    DeathScriptAddress = player.DeathScriptAddress,
                    MagicFileName = player.MagicFileName,
                    MagicFileName2 = player.MagicFileName2,
                    RetaliationMagicFileName =
                        player.RetaliationMagicFileName,
                    MagicDirectionWhenBeAttacked =
                        player.MagicDirectionWhenBeAttacked,
                    AddLifeRestorePercent =
                        player.AddLifeRestorePercent,
                    AddThewRestorePercent =
                        player.AddThewRestorePercent,
                    AddManaRestorePercent =
                        player.AddManaRestorePercent,
                    IsPetrified = player.IsPetrified,
                    FrozenSeconds = player.GetStatusSeconds(
                        JxqyStatusKind.Frozen),
                    PetrifiedSeconds = player.GetStatusSeconds(
                        JxqyStatusKind.Petrified),
                    PoisonSeconds = player.GetStatusSeconds(
                        JxqyStatusKind.Poisoned),
                    IsFrozenVisualEffect =
                        player.IsFrozenVisualEffect,
                    IsPoisonVisualEffect =
                        player.IsPoisonVisualEffect,
                    IsPetrifiedVisualEffect =
                        player.IsPetrifiedVisualEffect,
                    IsInTransport = player.IsInTransport,
                    IsMovementDisabled = player.IsMovementDisabled,
                    IsRunDisabled = player.IsRunDisabled,
                    IsJumpDisabled = player.IsJumpDisabled,
                    IsFightDisabled = player.IsFightDisabled,
                    AddMoveSpeedPercent = player.AddMoveSpeedPercent,
                    ChangeMoveSpeedPercent =
                        player.ChangeMoveSpeedPercent,
                    RunSpeedFold = player.RunSpeedFold,
                    WalkSpeed = player.WalkSpeed,
                };
                foreach (JxqyRangedMagicReference reference in
                         player.AdditionalBasicMagics)
                {
                    if (reference.Magic == null)
                        continue;
                    result.AdditionalBasicMagics.Add(
                        new SavedRangedMagic
                        {
                            FileName = reference.Magic.Id,
                            Distance = reference.Distance,
                        });
                }
                foreach (JxqyItemDefinition item in
                         equipment.Equipped.Values)
                    result.Equipped.Add(SavedItem.From(item));
                foreach (JxqyInventoryEntry entry in
                         equipment.EquippedEntries.Values)
                {
                    result.EquippedEntries.Add(
                        SavedInventoryEntry.From(entry));
                }
                return result;
            }

            public void ApplyTo(JxqyPlayer player)
            {
                player.Name = Name ?? string.Empty;
                player.LifeMax = LifeMax;
                player.ThewMax = ThewMax;
                player.ManaMax = ManaMax;
                player.Attack = Attack;
                player.Attack2 = Attack2;
                player.Attack3 = Attack3;
                player.Defend = Defend;
                player.Defend2 = Defend2;
                player.Defend3 = Defend3;
                player.Evade = Evade;
                player.Relation = (JxqyRelationType)Relation;
                player.Experience = Experience;
                player.ExpBonus = ExpBonus;
                player.LevelUpExperience = LevelUpExperience;
                player.Level = Math.Max(1, Level);
                player.AttackLevel = Math.Max(1, AttackLevel);
                player.DialogRadius = Math.Max(0, DialogRadius);
                player.Money = Money;
                player.WalkIsRun = WalkIsRun;
                player.IsNotUseThewWhenRun = IsNotUseThewWhenRun;
                player.IsManaRestore = IsManaRestore;
                player.ManaLimit = ManaLimit;
                player.ClearStatus(JxqyStatusKind.Frozen);
                player.ClearStatus(JxqyStatusKind.Petrified);
                player.ClearStatus(JxqyStatusKind.Poisoned);
                player.ClearStatus(JxqyStatusKind.MovementDisabled);
                player.ClearStatus(JxqyStatusKind.SkillDisabled);
                if (!HasExtendedState)
                    return;
                player.IsVisible = IsVisible;
                player.Invincible = Invincible;
                player.NoDropWhenDead = NoDropWhenDead;
                player.DropIni = DropIni ?? string.Empty;
                player.ScriptAddress = ScriptAddress ?? string.Empty;
                player.DeathScriptAddress =
                    DeathScriptAddress ?? string.Empty;
                player.MagicFileName = MagicFileName ?? string.Empty;
                player.MagicFileName2 =
                    MagicFileName2 ?? string.Empty;
                player.RetaliationMagicFileName =
                    RetaliationMagicFileName ?? string.Empty;
                player.MagicDirectionWhenBeAttacked =
                    MagicDirectionWhenBeAttacked;
                player.AddLifeRestorePercent =
                    AddLifeRestorePercent;
                player.AddThewRestorePercent =
                    AddThewRestorePercent;
                player.AddManaRestorePercent =
                    AddManaRestorePercent;
                player.AdditionalBasicMagics.Clear();
                foreach (SavedRangedMagic reference in
                         AdditionalBasicMagics ??
                         new List<SavedRangedMagic>())
                {
                    if (string.IsNullOrWhiteSpace(reference.FileName))
                        continue;
                    player.AdditionalBasicMagics.Add(
                        new JxqyRangedMagicReference
                        {
                            Magic = new JxqyMagicDefinition
                            {
                                Id = reference.FileName,
                            },
                            Distance = reference.Distance,
                        });
                }
                player.ApplyStatus(
                    JxqyStatusKind.Frozen,
                    FrozenSeconds);
                player.ApplyStatus(
                    JxqyStatusKind.Petrified,
                    PetrifiedSeconds);
                player.ApplyStatus(
                    JxqyStatusKind.Poisoned,
                    PoisonSeconds);
                player.RestoreStatusVisualEffects(
                    IsFrozenVisualEffect,
                    IsPoisonVisualEffect,
                    IsPetrifiedVisualEffect);
                player.IsInTransport = IsInTransport;
                player.IsMovementDisabled = IsMovementDisabled;
                player.IsRunDisabled = IsRunDisabled;
                player.IsJumpDisabled = IsJumpDisabled;
                player.IsFightDisabled = IsFightDisabled;
                player.AddMoveSpeedPercent = AddMoveSpeedPercent;
                player.ChangeMoveSpeedPercent =
                    ChangeMoveSpeedPercent;
                if (RunSpeedFold > 0)
                    player.RunSpeedFold = RunSpeedFold;
                if (WalkSpeed > 0)
                    player.WalkSpeed = WalkSpeed;
                if (HasActionState)
                {
                    player.RestoreActionState(
                        (JxqyCharacterState)State,
                        IsInFighting);
                }
            }
        }

        [Serializable]
        private sealed class SavedRangedMagic
        {
            public string FileName = string.Empty;
            public int Distance;
        }

        [Serializable]
        private sealed class SavedInventory
        {
            public List<SavedInventoryEntry> Entries = new();

            public static SavedInventory From(JxqyInventory inventory)
            {
                var result = new SavedInventory();
                foreach (JxqyInventoryEntry entry in inventory.Entries)
                    result.Entries.Add(SavedInventoryEntry.From(entry));
                return result;
            }
        }

        [Serializable]
        private sealed class SavedInventoryEntry
        {
            public SavedItem Item = new();
            public int Count;
            public float CooldownMilliseconds;
            public int LegacyListIndex;

            public static SavedInventoryEntry From(
                JxqyInventoryEntry entry)
            {
                return new SavedInventoryEntry
                {
                    Item = SavedItem.From(entry.Definition),
                    Count = entry.Count,
                    CooldownMilliseconds =
                        entry.CooldownMilliseconds,
                    LegacyListIndex = entry.LegacyListIndex,
                };
            }
        }

        [Serializable]
        private sealed class SavedSkills
        {
            public List<SavedSkill> Entries = new();

            public static SavedSkills From(JxqySkillManager skills)
            {
                var result = new SavedSkills();
                foreach (JxqySkillEntry entry in skills.Skills)
                {
                    result.Entries.Add(new SavedSkill
                    {
                        Magic = SavedMagic.From(entry.Magic),
                        Level = entry.Level,
                        Experience = entry.Experience,
                        CooldownMilliseconds =
                            entry.CooldownMilliseconds,
                        HideCount = entry.HideCount,
                        LegacyListIndex = entry.LegacyListIndex,
                    });
                }
                return result;
            }
        }

        [Serializable]
        private sealed class SavedSkill
        {
            public SavedMagic Magic = new();
            public int Level;
            public int Experience;
            public float CooldownMilliseconds;
            public int HideCount;
            public int LegacyListIndex;
        }

        [Serializable]
        private sealed class SavedItem
        {
            public string Id = string.Empty;
            public string Name = string.Empty;
            public string Introduction = string.Empty;
            public string ImageFileName = string.Empty;
            public string IconFileName = string.Empty;
            public JxqyItemKind Kind;
            public JxqyEquipmentSlot Slot;
            public JxqyItemEffectKind EffectKind;
            public int Life;
            public int Thew;
            public int Mana;
            public int MinimumUserLevel;
            public int ExplicitCost;
            public int ExplicitSellPrice;
            public int CooldownMilliseconds;
            public bool NoNeedToEquip;
            public string UseScript = string.Empty;
            public SavedModifiers Modifiers = new();

            public static SavedItem From(JxqyItemDefinition item)
            {
                return new SavedItem
                {
                    Id = item.Id,
                    Name = item.Name,
                    Introduction = item.Introduction,
                    ImageFileName = item.ImageFileName,
                    IconFileName = item.IconFileName,
                    Kind = item.Kind,
                    Slot = item.Slot,
                    EffectKind = item.EffectKind,
                    Life = item.Life,
                    Thew = item.Thew,
                    Mana = item.Mana,
                    MinimumUserLevel = item.MinimumUserLevel,
                    ExplicitCost = item.ExplicitCost,
                    ExplicitSellPrice = item.ExplicitSellPrice,
                    CooldownMilliseconds = item.CooldownMilliseconds,
                    NoNeedToEquip = item.NoNeedToEquip,
                    UseScript = item.UseScript,
                    Modifiers = SavedModifiers.From(item.Modifiers),
                };
            }

            public JxqyItemDefinition ToDefinition()
            {
                var item = new JxqyItemDefinition
                {
                    Id = Id ?? string.Empty,
                    Name = Name ?? string.Empty,
                    Introduction = Introduction ?? string.Empty,
                    ImageFileName = ImageFileName ?? string.Empty,
                    IconFileName = IconFileName ?? string.Empty,
                    Kind = Kind,
                    Slot = Slot,
                    EffectKind = EffectKind,
                    Life = Life,
                    Thew = Thew,
                    Mana = Mana,
                    MinimumUserLevel = MinimumUserLevel,
                    ExplicitCost = ExplicitCost,
                    ExplicitSellPrice = ExplicitSellPrice,
                    CooldownMilliseconds = CooldownMilliseconds,
                    NoNeedToEquip = NoNeedToEquip,
                    UseScript = UseScript ?? string.Empty,
                };
                Modifiers?.ApplyTo(item.Modifiers);
                return item;
            }
        }

        [Serializable]
        private sealed class SavedModifiers
        {
            public int LifeMax;
            public int ThewMax;
            public int ManaMax;
            public int Attack;
            public int Attack2;
            public int Attack3;
            public int Defend;
            public int Defend2;
            public int Defend3;
            public int Evade;
            public int MoveSpeedPercent;

            public static SavedModifiers From(JxqyStatModifiers value)
            {
                return new SavedModifiers
                {
                    LifeMax = value.LifeMax,
                    ThewMax = value.ThewMax,
                    ManaMax = value.ManaMax,
                    Attack = value.Attack,
                    Attack2 = value.Attack2,
                    Attack3 = value.Attack3,
                    Defend = value.Defend,
                    Defend2 = value.Defend2,
                    Defend3 = value.Defend3,
                    Evade = value.Evade,
                    MoveSpeedPercent = value.MoveSpeedPercent,
                };
            }

            public void ApplyTo(JxqyStatModifiers value)
            {
                value.LifeMax = LifeMax;
                value.ThewMax = ThewMax;
                value.ManaMax = ManaMax;
                value.Attack = Attack;
                value.Attack2 = Attack2;
                value.Attack3 = Attack3;
                value.Defend = Defend;
                value.Defend2 = Defend2;
                value.Defend3 = Defend3;
                value.Evade = Evade;
                value.MoveSpeedPercent = MoveSpeedPercent;
            }
        }

        [Serializable]
        private sealed class SavedMagic
        {
            public string Id = string.Empty;
            public string Name = string.Empty;
            public string Introduction = string.Empty;
            public string ImageFileName = string.Empty;
            public string IconFileName = string.Empty;
            public string FlyingImageFileName = string.Empty;
            public string FlyingSoundFileName = string.Empty;
            public string VanishImageFileName = string.Empty;
            public string VanishSoundFileName = string.Empty;
            public string SuperModeImageFileName = string.Empty;
            public string ActionFileName = string.Empty;
            public string AttackFileName = string.Empty;
            public int Belong;
            public int MoveKind = 2;
            public int EffectLevel = 1;
            public int Region;
            public int SpecialKind;
            public int SpecialKindValue;
            public int SpecialKindMilliseconds;
            public int NoSpecialKindEffect;
            public int NoInterruption;
            public int WaitFrame;
            public int LifeFrame;
            public int KeepMilliseconds;
            public int ColdMilliseconds;
            public int AlphaBlend;
            public int FlyingLum;
            public int VanishLum;
            public int Effect;
            public int EffectExt;
            public int Effect2;
            public int Effect3;
            public int EffectMana;
            public int LevelUpExperience;
            public int ManaCost;
            public int ThewCost;
            public int LifeCost;
            public float ProjectileSpeed;
            public float Range = 48f;
            public float Radius = 12f;
            public float LifeSeconds = 3f;
            public float FrozenSeconds;
            public float PetrifiedSeconds;
            public float PoisonSeconds;
            public float DisableMoveSeconds;
            public float DisableSkillSeconds;
            public int SideEffectPercent;
            public int SideEffectProbability;
            public int SideEffectType;
            public int RestoreType;
            public int RestorePercent;
            public int RestoreProbability;
            public bool DieAfterUse;
            public int LifeMax;
            public int ThewMax;
            public int ManaMax;
            public int Attack;
            public int Attack2;
            public int Attack3;
            public int Defend;
            public int Defend2;
            public int Defend3;
            public int Evade;
            public int AddLifeRestorePercent;
            public int AddThewRestorePercent;
            public int AddManaRestorePercent;
            public string FlyIni = string.Empty;
            public string FlyIni2 = string.Empty;
            public string MagicToUseWhenBeAttacked = string.Empty;
            public int MagicDirectionWhenBeAttacked;

            public static SavedMagic From(JxqyMagicDefinition magic)
            {
                return new SavedMagic
                {
                    Id = magic.Id,
                    Name = magic.Name,
                    Introduction = magic.Introduction,
                    ImageFileName = magic.ImageFileName,
                    IconFileName = magic.IconFileName,
                    FlyingImageFileName =
                        magic.FlyingImageFileName,
                    FlyingSoundFileName =
                        magic.FlyingSoundFileName,
                    VanishImageFileName =
                        magic.VanishImageFileName,
                    VanishSoundFileName =
                        magic.VanishSoundFileName,
                    SuperModeImageFileName =
                        magic.SuperModeImageFileName,
                    ActionFileName = magic.ActionFileName,
                    AttackFileName = magic.AttackFileName,
                    Belong = magic.Belong,
                    MoveKind = magic.MoveKind,
                    EffectLevel = magic.EffectLevel,
                    Region = magic.Region,
                    SpecialKind = magic.SpecialKind,
                    SpecialKindValue = magic.SpecialKindValue,
                    SpecialKindMilliseconds =
                        magic.SpecialKindMilliseconds,
                    NoSpecialKindEffect =
                        magic.NoSpecialKindEffect,
                    NoInterruption = magic.NoInterruption,
                    WaitFrame = magic.WaitFrame,
                    LifeFrame = magic.LifeFrame,
                    KeepMilliseconds = magic.KeepMilliseconds,
                    ColdMilliseconds = magic.ColdMilliseconds,
                    AlphaBlend = magic.AlphaBlend,
                    FlyingLum = magic.FlyingLum,
                    VanishLum = magic.VanishLum,
                    Effect = magic.Effect,
                    EffectExt = magic.EffectExt,
                    Effect2 = magic.Effect2,
                    Effect3 = magic.Effect3,
                    EffectMana = magic.EffectMana,
                    LevelUpExperience = magic.LevelUpExperience,
                    ManaCost = magic.ManaCost,
                    ThewCost = magic.ThewCost,
                    LifeCost = magic.LifeCost,
                    ProjectileSpeed = magic.ProjectileSpeed,
                    Range = magic.Range,
                    Radius = magic.Radius,
                    LifeSeconds = magic.LifeSeconds,
                    FrozenSeconds = magic.FrozenSeconds,
                    PetrifiedSeconds = magic.PetrifiedSeconds,
                    PoisonSeconds = magic.PoisonSeconds,
                    DisableMoveSeconds = magic.DisableMoveSeconds,
                    DisableSkillSeconds = magic.DisableSkillSeconds,
                    SideEffectPercent = magic.SideEffectPercent,
                    SideEffectProbability =
                        magic.SideEffectProbability,
                    SideEffectType = magic.SideEffectType,
                    RestoreType = magic.RestoreType,
                    RestorePercent = magic.RestorePercent,
                    RestoreProbability = magic.RestoreProbability,
                    DieAfterUse = magic.DieAfterUse,
                    LifeMax = magic.LifeMax,
                    ThewMax = magic.ThewMax,
                    ManaMax = magic.ManaMax,
                    Attack = magic.Attack,
                    Attack2 = magic.Attack2,
                    Attack3 = magic.Attack3,
                    Defend = magic.Defend,
                    Defend2 = magic.Defend2,
                    Defend3 = magic.Defend3,
                    Evade = magic.Evade,
                    AddLifeRestorePercent =
                        magic.AddLifeRestorePercent,
                    AddThewRestorePercent =
                        magic.AddThewRestorePercent,
                    AddManaRestorePercent =
                        magic.AddManaRestorePercent,
                    FlyIni = magic.FlyIni,
                    FlyIni2 = magic.FlyIni2,
                    MagicToUseWhenBeAttacked =
                        magic.MagicToUseWhenBeAttacked,
                    MagicDirectionWhenBeAttacked =
                        magic.MagicDirectionWhenBeAttacked,
                };
            }

            public JxqyMagicDefinition ToDefinition()
            {
                return new JxqyMagicDefinition
                {
                    Id = Id ?? string.Empty,
                    Name = Name ?? string.Empty,
                    Introduction = Introduction ?? string.Empty,
                    ImageFileName = ImageFileName ?? string.Empty,
                    IconFileName = IconFileName ?? string.Empty,
                    FlyingImageFileName =
                        FlyingImageFileName ?? string.Empty,
                    FlyingSoundFileName =
                        FlyingSoundFileName ?? string.Empty,
                    VanishImageFileName =
                        VanishImageFileName ?? string.Empty,
                    VanishSoundFileName =
                        VanishSoundFileName ?? string.Empty,
                    SuperModeImageFileName =
                        SuperModeImageFileName ?? string.Empty,
                    ActionFileName = ActionFileName ?? string.Empty,
                    AttackFileName = AttackFileName ?? string.Empty,
                    Belong = Belong,
                    MoveKind = MoveKind,
                    EffectLevel = EffectLevel,
                    Region = Region,
                    SpecialKind = SpecialKind,
                    SpecialKindValue = SpecialKindValue,
                    SpecialKindMilliseconds =
                        SpecialKindMilliseconds,
                    NoSpecialKindEffect = NoSpecialKindEffect,
                    NoInterruption = NoInterruption,
                    WaitFrame = WaitFrame,
                    LifeFrame = LifeFrame,
                    KeepMilliseconds = KeepMilliseconds,
                    ColdMilliseconds = ColdMilliseconds,
                    AlphaBlend = AlphaBlend,
                    FlyingLum = FlyingLum,
                    VanishLum = VanishLum,
                    Effect = Effect,
                    EffectExt = EffectExt,
                    Effect2 = Effect2,
                    Effect3 = Effect3,
                    EffectMana = EffectMana,
                    LevelUpExperience = LevelUpExperience,
                    ManaCost = ManaCost,
                    ThewCost = ThewCost,
                    LifeCost = LifeCost,
                    ProjectileSpeed = ProjectileSpeed,
                    Range = Range,
                    Radius = Radius,
                    LifeSeconds = LifeSeconds,
                    FrozenSeconds = FrozenSeconds,
                    PetrifiedSeconds = PetrifiedSeconds,
                    PoisonSeconds = PoisonSeconds,
                    DisableMoveSeconds = DisableMoveSeconds,
                    DisableSkillSeconds = DisableSkillSeconds,
                    SideEffectPercent = SideEffectPercent,
                    SideEffectProbability = SideEffectProbability,
                    SideEffectType = SideEffectType,
                    RestoreType = RestoreType,
                    RestorePercent = RestorePercent,
                    RestoreProbability = RestoreProbability,
                    DieAfterUse = DieAfterUse,
                    LifeMax = LifeMax,
                    ThewMax = ThewMax,
                    ManaMax = ManaMax,
                    Attack = Attack,
                    Attack2 = Attack2,
                    Attack3 = Attack3,
                    Defend = Defend,
                    Defend2 = Defend2,
                    Defend3 = Defend3,
                    Evade = Evade,
                    AddLifeRestorePercent =
                        AddLifeRestorePercent,
                    AddThewRestorePercent =
                        AddThewRestorePercent,
                    AddManaRestorePercent =
                        AddManaRestorePercent,
                    FlyIni = FlyIni ?? string.Empty,
                    FlyIni2 = FlyIni2 ?? string.Empty,
                    MagicToUseWhenBeAttacked =
                        MagicToUseWhenBeAttacked ??
                        string.Empty,
                    MagicDirectionWhenBeAttacked =
                        MagicDirectionWhenBeAttacked,
                };
            }
        }
    }
}
