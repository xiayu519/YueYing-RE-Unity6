using System;
using System.Collections.Generic;

namespace Jxqy.Domain.Persistence
{
    [Serializable]
    public sealed class JxqySaveGameData
    {
        public const int CurrentSchemaVersion = 9;
        public const int OldestSupportedSchemaVersion = 2;

        public int SchemaVersion = CurrentSchemaVersion;
        public string SavedUtc = string.Empty;
        public string SourceFormat = "UnityJson";
        public string ContentHash = string.Empty;
        public JxqySaveWorldState World = new();
        public JxqySavePlayerState Player = new();
        public JxqySavePresentationState Presentation = new();
        public List<JxqySaveVariable> Variables = new();
        public List<JxqySaveParallelScript> ParallelScripts = new();
        public List<string> Memos = new();
        public List<JxqyLegacySaveFile> LegacyFiles = new();
    }

    [Serializable]
    public sealed class JxqySaveWorldState
    {
        public string Map = string.Empty;
        public string NpcFile = string.Empty;
        public string ObjectFile = string.Empty;
        public string BackgroundMusic = string.Empty;
        public int MapTime;
        public bool IsSnowing;
        public string RainFile = string.Empty;
        public bool WaterEffectEnabled;
        public bool SaveDisabled;
        public bool DropGoodWhenDefeatEnemyDisabled;
        public bool NpcAiDisabled;
        public List<JxqySaveNpcState> Npcs = new();
        public List<JxqySaveObjectState> Objects = new();
        public List<JxqySaveTrapState> Traps = new();
        public List<JxqySaveNpcSnapshot> NpcSnapshots = new();
        public List<JxqySaveObjectSnapshot> ObjectSnapshots = new();
    }

    [Serializable]
    public sealed class JxqySaveNpcState
    {
        public string Name = string.Empty;
        public string NpcIniFile = string.Empty;
        public int Kind;
        public int Relation;
        public int TileColumn;
        public int TileRow;
        public int Direction;
        public int Life;
        public int LifeMax;
        public int Thew;
        public int ThewMax;
        public int Mana;
        public int ManaMax;
        public int Attack;
        public int Attack2;
        public int Attack3;
        public int Defend;
        public int Defend2;
        public int Defend3;
        public int Evade;
        public bool CanEvade = true;
        public int Level;
        public int AttackLevel = 1;
        public int DialogRadius = 1;
        public int Experience;
        public int LevelUpExperience;
        public int ExpBonus;
        public int Action;
        public int PathFinderMode;
        public string FixedPositionData = string.Empty;
        public int CurrentFixedPositionIndex;
        public int Group;
        public int VisionRadius;
        public int AttackRadius;
        public int IdleFrames;
        public int LightRadius;
        public int CharacterState;
        public float LifeMilliseconds;
        public string Script = string.Empty;
        public string DeathScript = string.Empty;
        public string MagicFile = string.Empty;
        public string MagicFile2 = string.Empty;
        public string RetaliationMagicFile = string.Empty;
        public int MagicDirectionWhenBeAttacked;
        public List<JxqySaveRangedMagicState> AdditionalBasicMagics =
            new();
        public int DestinationMapPosX;
        public int DestinationMapPosY;
        public int KeepAttackX;
        public int KeepAttackY;
        public int CanEquip;
        public int CanLevelUp;
        public string BodyFile = string.Empty;
        public bool IsBodyCreated;
        public float ReviveDelaySeconds;
        public float ReviveSecondsRemaining;
        public string EquipmentBackgroundFile = string.Empty;
        public string HeadEquip = string.Empty;
        public string NeckEquip = string.Empty;
        public string BodyEquip = string.Empty;
        public string BackEquip = string.Empty;
        public string HandEquip = string.Empty;
        public string WristEquip = string.Empty;
        public string FootEquip = string.Empty;
        public string ResourceFile = string.Empty;
        public string DropIni = string.Empty;
        public bool NoDropWhenDead;
        public bool IsVisible = true;
        public bool NoAutoAttackPlayer;
        public bool StopFindingTarget;
        public int ActionType;
        public float BlindMilliseconds;
        public bool Invincible;
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
    }

    [Serializable]
    public sealed class JxqySaveRangedMagicState
    {
        public string FileName = string.Empty;
        public int Distance;
    }

    [Serializable]
    public sealed class JxqySaveObjectState
    {
        public string Name = string.Empty;
        public string ResourceFile = string.Empty;
        public string WavFile = string.Empty;
        public int Kind;
        public int TileColumn;
        public int TileRow;
        public int Direction;
        public int Frame;
        public int OffsetX;
        public int OffsetY;
        public int Height;
        public int Damage;
        public int LightRadius;
        public string Script = string.Empty;
        public string RightScript = string.Empty;
        public string TimerScript = string.Empty;
        public int TimerScriptIntervalMilliseconds = 1000;
        public string ReviveNpcFile = string.Empty;
        public float MillisecondsToRemove;
        public bool IsVisible = true;
        public bool IsOpen;
        public bool IsRemoved;
    }

    [Serializable]
    public sealed class JxqySaveTrapState
    {
        public string MapName = string.Empty;
        public int Index;
        public string Script = string.Empty;
        public bool Triggered;
    }

    [Serializable]
    public sealed class JxqySavePlayerState
    {
        public int PlayerIndex;
        public int Direction;
        public int TileColumn;
        public int TileRow;
        public int Life;
        public int Mana;
        public int Thew;
        public string LevelFile = string.Empty;
        public string PlayerDataJson = string.Empty;
        public string InventoryDataJson = string.Empty;
        public string MagicDataJson = string.Empty;
        public List<JxqySavePlayerProfileState> Profiles = new();
    }

    [Serializable]
    public sealed class JxqySavePlayerProfileState
    {
        public int PlayerIndex;
        public int Direction;
        public int TileColumn;
        public int TileRow;
        public int Life;
        public int Mana;
        public int Thew;
        public int SelectedMagicLegacyIndex;
        public string LevelFile = string.Empty;
        public string PlayerDataJson = string.Empty;
        public string InventoryDataJson = string.Empty;
        public string MagicDataJson = string.Empty;
    }

    [Serializable]
    public sealed class JxqySaveNpcSnapshot
    {
        public string FileName = string.Empty;
        public List<JxqySaveNpcState> Npcs = new();
    }

    [Serializable]
    public sealed class JxqySaveObjectSnapshot
    {
        public string FileName = string.Empty;
        public List<JxqySaveObjectState> Objects = new();
    }

    [Serializable]
    public sealed class JxqySavePresentationState
    {
        public bool ScriptShowMapPosition;
        public string MapColorBgra = string.Empty;
        public string SpriteColorBgra = string.Empty;
        public bool TimerEnabled;
        public int TimerTotalSeconds;
        public bool TimerWindowVisible;
        public bool TimerScriptEnabled;
        public string TimerScript = string.Empty;
        public int TimerTriggerSeconds;
    }

    [Serializable]
    public sealed class JxqySaveVariable
    {
        public string Name = string.Empty;
        public string Value = string.Empty;
    }

    [Serializable]
    public sealed class JxqySaveParallelScript
    {
        public string FileName = string.Empty;
        public int RemainingDelayMilliseconds;
    }

    [Serializable]
    public sealed class JxqyLegacySaveFile
    {
        public string RelativePath = string.Empty;
        public string Utf8Text = string.Empty;
    }
}
