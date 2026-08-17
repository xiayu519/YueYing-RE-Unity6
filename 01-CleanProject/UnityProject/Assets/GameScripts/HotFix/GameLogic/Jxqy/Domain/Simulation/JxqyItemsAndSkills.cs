using System;
using System.Collections.Generic;

namespace Jxqy.Domain.Simulation
{
    public enum JxqyItemKind
    {
        Drug,
        Equipment,
        Event,
    }

    public enum JxqyEquipmentSlot
    {
        None,
        Head,
        Neck,
        Body,
        Back,
        Hand,
        Wrist,
        Foot,
    }

    public enum JxqyItemEffectKind
    {
        None,
        ThewNotLoseWhenRun,
        ManaRestore,
        EnemyFrozen,
        ClearFrozen,
        EnemyPoisoned,
        ClearPoison,
        EnemyPetrified,
        ClearPetrifaction,
    }

    public sealed class JxqyStatModifiers
    {
        public int LifeMax { get; set; }
        public int ThewMax { get; set; }
        public int ManaMax { get; set; }
        public int Attack { get; set; }
        public int Attack2 { get; set; }
        public int Attack3 { get; set; }
        public int Defend { get; set; }
        public int Defend2 { get; set; }
        public int Defend3 { get; set; }
        public int Evade { get; set; }
        public int MoveSpeedPercent { get; set; }
    }

    public sealed class JxqyItemDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Introduction { get; set; } = string.Empty;
        public string ImageFileName { get; set; } = string.Empty;
        public string IconFileName { get; set; } = string.Empty;
        public JxqyItemKind Kind { get; set; }
        public JxqyEquipmentSlot Slot { get; set; }
        public JxqyItemEffectKind EffectKind { get; set; }
        public int Life { get; set; }
        public int Thew { get; set; }
        public int Mana { get; set; }
        public int MinimumUserLevel { get; set; }
        public int ExplicitCost { get; set; }
        public int ExplicitSellPrice { get; set; }
        public int CooldownMilliseconds { get; set; }
        public bool NoNeedToEquip { get; set; }
        public string UseScript { get; set; } = string.Empty;
        public JxqyStatModifiers Modifiers { get; } = new JxqyStatModifiers();
        public bool RestoresMana =>
            Kind == JxqyItemKind.Drug && Mana > 0;

        public int CostRaw
        {
            get
            {
                if (ExplicitCost > 0)
                    return ExplicitCost;
                int effectFold = EffectKind == JxqyItemEffectKind.None ? 1 : 2;
                if (Kind == JxqyItemKind.Drug)
                    return (Thew * 4 + Life * 2 + Mana * 2) * effectFold;
                if (Kind != JxqyItemKind.Equipment || NoNeedToEquip)
                    return 0;
                JxqyStatModifiers value = Modifiers;
                return (value.Attack * 20 + value.Attack2 * 20 +
                        value.Attack3 * 20 + value.Defend * 20 +
                        value.Defend2 * 20 + value.Defend3 * 20 +
                        value.Evade * 40 + value.LifeMax * 2 +
                        value.ThewMax * 3 + value.ManaMax * 2) * effectFold;
            }
        }

        public int GetBuyPrice(int percentage)
        {
            return Math.Max(0, CostRaw * Math.Max(0, percentage) / 100);
        }

        public int GetSellPrice(int percentage)
        {
            int basis = ExplicitSellPrice > 0
                ? ExplicitSellPrice
                : CostRaw / 2;
            return Math.Max(0, basis * Math.Max(0, percentage) / 100);
        }
    }

    public sealed class JxqyDefinitionCatalog<T> where T : class
    {
        private readonly Dictionary<string, T> _values =
            new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        private readonly Func<T, string> _getId;

        public JxqyDefinitionCatalog(Func<T, string> getId)
        {
            _getId = getId ?? throw new ArgumentNullException(nameof(getId));
        }

        public int Count => _values.Count;
        public IEnumerable<T> Values => _values.Values;

        public void Register(T value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            string id = _getId(value);
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("定义必须包含非空 Id。", nameof(value));
            if (!_values.TryAdd(id, value))
                throw new InvalidOperationException($"重复定义：{id}");
        }

        public bool TryGet(string id, out T value)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                value = null;
                return false;
            }
            return _values.TryGetValue(id, out value);
        }

        public T GetRequired(string id)
        {
            if (!TryGet(id, out T value))
                throw new KeyNotFoundException($"未找到定义：{id}");
            return value;
        }
    }

    public sealed class JxqyGameplayConfigManager
    {
        public JxqyGameplayConfigManager()
        {
            Items = new JxqyDefinitionCatalog<JxqyItemDefinition>(
                value => value.Id);
            Magics = new JxqyDefinitionCatalog<JxqyMagicDefinition>(
                value => value.Id);
        }

        public JxqyDefinitionCatalog<JxqyItemDefinition> Items { get; }
        public JxqyDefinitionCatalog<JxqyMagicDefinition> Magics { get; }
    }

    public sealed class JxqyInventoryEntry
    {
        internal JxqyInventoryEntry(
            JxqyItemDefinition definition,
            int count,
            int legacyListIndex)
        {
            Definition = definition;
            Count = count;
            LegacyListIndex = legacyListIndex;
        }

        public JxqyItemDefinition Definition { get; }
        public int Count { get; internal set; }
        public float CooldownMilliseconds { get; internal set; }
        public int LegacyListIndex { get; internal set; }
    }

    public sealed class JxqyInventory
    {
        private readonly List<JxqyInventoryEntry> _entries =
            new List<JxqyInventoryEntry>();

        public JxqyInventory(int capacity = 198, bool stackByItemType = true)
        {
            if (capacity < 1)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
            StackByItemType = stackByItemType;
        }

        public int Capacity { get; }
        public bool StackByItemType { get; }
        public IReadOnlyList<JxqyInventoryEntry> Entries => _entries;
        public bool HasFreeSpace =>
            FindFirstFreeStoreLegacyIndex() > 0;

        public bool Add(
            JxqyItemDefinition definition,
            int count = 1,
            int legacyListIndex = 0)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            if (count < 1)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (legacyListIndex < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(legacyListIndex));
            bool explicitLegacyListIndex = legacyListIndex > 0;
            if (!explicitLegacyListIndex)
                legacyListIndex = FindFirstFreeLegacyListIndex();
            if (legacyListIndex < 1)
                return false;
            if (StackByItemType)
            {
                JxqyInventoryEntry current = Find(definition.Id);
                if (current != null)
                {
                    current.Count = checked(current.Count + count);
                    if (current.LegacyListIndex == 0)
                        current.LegacyListIndex = legacyListIndex;
                    return true;
                }
                if (!explicitLegacyListIndex && !HasFreeSpace)
                    return false;
                if (FindAtLegacyIndex(legacyListIndex) != null)
                    return false;
                _entries.Add(new JxqyInventoryEntry(
                    definition,
                    count,
                    legacyListIndex));
                SortByLegacyListIndex();
                return true;
            }
            if (explicitLegacyListIndex)
            {
                if (FindAtLegacyIndex(legacyListIndex) != null)
                    return false;
                _entries.Add(new JxqyInventoryEntry(
                    definition,
                    count,
                    legacyListIndex));
                SortByLegacyListIndex();
                return true;
            }
            for (int index = 0; index < count; index++)
            {
                legacyListIndex = FindFirstFreeLegacyListIndex();
                if (legacyListIndex < 1 ||
                    legacyListIndex > Capacity)
                {
                    return false;
                }
                _entries.Add(new JxqyInventoryEntry(
                    definition,
                    1,
                    legacyListIndex));
            }
            SortByLegacyListIndex();
            return true;
        }

        public bool RestoreEntry(
            JxqyItemDefinition definition,
            int count,
            float cooldownMilliseconds,
            int legacyListIndex = 0)
        {
            if (!Add(definition, count, legacyListIndex))
                return false;
            JxqyInventoryEntry entry = Find(definition.Id);
            if (entry != null)
            {
                entry.CooldownMilliseconds = Math.Max(
                    0,
                    cooldownMilliseconds);
            }
            return entry != null;
        }

        public JxqyInventoryEntry FindAtLegacyIndex(int legacyListIndex)
        {
            return _entries.Find(entry =>
                entry.LegacyListIndex == legacyListIndex);
        }

        public bool MoveEntryToLegacyIndex(
            int entryIndex,
            int targetLegacyListIndex)
        {
            if (entryIndex < 0 ||
                entryIndex >= _entries.Count ||
                targetLegacyListIndex < 1)
            {
                return false;
            }
            JxqyInventoryEntry source = _entries[entryIndex];
            JxqyInventoryEntry target =
                FindAtLegacyIndex(targetLegacyListIndex);
            int sourceLegacyListIndex = source.LegacyListIndex;
            source.LegacyListIndex = targetLegacyListIndex;
            if (target != null)
                target.LegacyListIndex = sourceLegacyListIndex;
            SortByLegacyListIndex();
            return true;
        }

        internal bool ExchangeEntryAtLegacyIndex(
            int legacyListIndex,
            JxqyInventoryEntry replacement,
            out JxqyInventoryEntry displaced)
        {
            if (legacyListIndex < 1 || legacyListIndex > Capacity)
            {
                displaced = null;
                return false;
            }

            displaced = FindAtLegacyIndex(legacyListIndex);
            if (displaced == null && replacement == null)
                return false;
            if (replacement != null && _entries.Contains(replacement))
                return false;

            if (displaced != null)
                _entries.Remove(displaced);
            if (replacement != null)
            {
                replacement.LegacyListIndex = legacyListIndex;
                _entries.Add(replacement);
            }
            SortByLegacyListIndex();
            return true;
        }

        internal int FindFirstFreeStoreLegacyIndex()
        {
            for (int legacyListIndex = 1;
                 legacyListIndex <= Capacity;
                 legacyListIndex++)
            {
                if (FindAtLegacyIndex(legacyListIndex) == null)
                    return legacyListIndex;
            }
            return 0;
        }

        private void SortByLegacyListIndex()
        {
            _entries.Sort((left, right) =>
                left.LegacyListIndex.CompareTo(right.LegacyListIndex));
        }

        public bool ExchangeEntries(int firstIndex, int secondIndex)
        {
            if (firstIndex < 0 ||
                firstIndex >= _entries.Count ||
                secondIndex < 0 ||
                secondIndex >= _entries.Count)
            {
                return false;
            }
            return MoveEntryToLegacyIndex(
                firstIndex,
                _entries[secondIndex].LegacyListIndex);
        }

        private int FindFirstFreeLegacyListIndex()
        {
            return FindFirstFreeStoreLegacyIndex();
        }

        public bool Remove(string itemId, int count = 1)
        {
            if (count < 1)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (Count(itemId) < count)
                return false;
            for (int index = _entries.Count - 1;
                index >= 0 && count > 0;
                index--)
            {
                JxqyInventoryEntry entry = _entries[index];
                if (!string.Equals(
                    entry.Definition.Id,
                    itemId,
                    StringComparison.OrdinalIgnoreCase))
                    continue;
                int removed = Math.Min(entry.Count, count);
                entry.Count -= removed;
                count -= removed;
                if (entry.Count == 0)
                    _entries.RemoveAt(index);
            }
            return true;
        }

        public int Count(string itemId)
        {
            int total = 0;
            foreach (JxqyInventoryEntry entry in _entries)
            {
                if (string.Equals(
                    entry.Definition.Id,
                    itemId,
                    StringComparison.OrdinalIgnoreCase))
                    total += entry.Count;
            }
            return total;
        }

        public void Clear()
        {
            _entries.Clear();
        }

        public bool Use(string itemId, JxqyCharacter target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            JxqyInventoryEntry entry = Find(itemId);
            if (entry == null || entry.CooldownMilliseconds > 0 ||
                entry.Definition.Kind != JxqyItemKind.Drug ||
                target.Level < entry.Definition.MinimumUserLevel)
                return false;
            JxqyItemDefinition item = entry.Definition;
            if (target is JxqyPlayer player &&
                player.ManaLimit &&
                item.RestoresMana)
            {
                return false;
            }
            target.LifeMax += item.Modifiers.LifeMax;
            target.ThewMax += item.Modifiers.ThewMax;
            target.ManaMax += item.Modifiers.ManaMax;
            target.AddLife(item.Life);
            target.Thew += item.Thew;
            target.Mana += item.Mana;
            ClearStatusFromItem(target, item.EffectKind);
            entry.CooldownMilliseconds = Math.Max(0, item.CooldownMilliseconds);
            return Remove(item.Id);
        }

        public void Tick(float elapsedMilliseconds)
        {
            if (elapsedMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(elapsedMilliseconds));
            foreach (JxqyInventoryEntry entry in _entries)
            {
                entry.CooldownMilliseconds = Math.Max(
                    0,
                    entry.CooldownMilliseconds - elapsedMilliseconds);
            }
        }

        private JxqyInventoryEntry Find(string itemId)
        {
            foreach (JxqyInventoryEntry entry in _entries)
            {
                if (string.Equals(
                    entry.Definition.Id,
                    itemId,
                    StringComparison.OrdinalIgnoreCase))
                    return entry;
            }
            return null;
        }

        private static void ClearStatusFromItem(
            JxqyCharacter target,
            JxqyItemEffectKind effect)
        {
            switch (effect)
            {
                case JxqyItemEffectKind.ClearFrozen:
                    target.ClearStatus(JxqyStatusKind.Frozen);
                    break;
                case JxqyItemEffectKind.ClearPoison:
                    target.ClearStatus(JxqyStatusKind.Poisoned);
                    break;
                case JxqyItemEffectKind.ClearPetrifaction:
                    target.ClearStatus(JxqyStatusKind.Petrified);
                    break;
            }
        }
    }

    public sealed class JxqyEquipmentManager
    {
        private readonly Dictionary<JxqyEquipmentSlot, JxqyItemDefinition>
            _equipped =
                new Dictionary<JxqyEquipmentSlot, JxqyItemDefinition>();
        private readonly Dictionary<JxqyEquipmentSlot, JxqyInventoryEntry>
            _equippedEntries =
                new Dictionary<JxqyEquipmentSlot, JxqyInventoryEntry>();

        public IReadOnlyDictionary<JxqyEquipmentSlot, JxqyItemDefinition>
            Equipped => _equipped;
        public IReadOnlyDictionary<JxqyEquipmentSlot, JxqyInventoryEntry>
            EquippedEntries => _equippedEntries;

        public bool Equip(
            JxqyCharacter character,
            JxqyInventory inventory,
            string itemId)
        {
            if (character == null)
                throw new ArgumentNullException(nameof(character));
            if (inventory == null)
                throw new ArgumentNullException(nameof(inventory));
            JxqyInventoryEntry entry = null;
            foreach (JxqyInventoryEntry candidate in inventory.Entries)
            {
                if (string.Equals(
                    candidate.Definition.Id,
                    itemId,
                    StringComparison.OrdinalIgnoreCase))
                {
                    entry = candidate;
                    break;
                }
            }
            if (entry == null ||
                entry.Definition.Kind != JxqyItemKind.Equipment ||
                entry.Definition.Slot == JxqyEquipmentSlot.None ||
                character.Level < entry.Definition.MinimumUserLevel)
                return false;

            return ExchangeWithInventory(
                character,
                inventory,
                entry.Definition.Slot,
                entry.LegacyListIndex);
        }

        public bool ExchangeWithInventory(
            JxqyCharacter character,
            JxqyInventory inventory,
            JxqyEquipmentSlot slot,
            int inventoryLegacyListIndex)
        {
            if (character == null)
                throw new ArgumentNullException(nameof(character));
            if (inventory == null)
                throw new ArgumentNullException(nameof(inventory));
            if (slot == JxqyEquipmentSlot.None ||
                inventoryLegacyListIndex < 1)
            {
                return false;
            }

            _equippedEntries.TryGetValue(
                slot,
                out JxqyInventoryEntry equippedEntry);
            JxqyInventoryEntry target =
                inventory.FindAtLegacyIndex(inventoryLegacyListIndex);
            JxqyItemDefinition inventoryItem = target?.Definition;
            if (inventoryItem != null &&
                (inventoryItem.Kind != JxqyItemKind.Equipment ||
                 inventoryItem.Slot != slot ||
                 character.Level < inventoryItem.MinimumUserLevel))
            {
                return false;
            }
            if (equippedEntry == null && inventoryItem == null)
                return false;

            if (!inventory.ExchangeEntryAtLegacyIndex(
                    inventoryLegacyListIndex,
                    equippedEntry,
                    out JxqyInventoryEntry exchangedInventoryEntry))
            {
                return false;
            }

            if (equippedEntry != null)
            {
                Apply(character, equippedEntry.Definition.Modifiers, -1);
                ApplyPassive(character, equippedEntry.Definition, false);
            }

            if (exchangedInventoryEntry == null)
            {
                _equipped.Remove(slot);
                _equippedEntries.Remove(slot);
                return true;
            }

            exchangedInventoryEntry.LegacyListIndex =
                GetLegacyListIndex(slot);
            _equipped[slot] = exchangedInventoryEntry.Definition;
            _equippedEntries[slot] = exchangedInventoryEntry;
            Apply(character, exchangedInventoryEntry.Definition.Modifiers, 1);
            ApplyPassive(character, exchangedInventoryEntry.Definition, true);
            return true;
        }

        public bool Unequip(
            JxqyCharacter character,
            JxqyInventory inventory,
            JxqyEquipmentSlot slot)
        {
            if (!_equippedEntries.TryGetValue(
                    slot,
                    out JxqyInventoryEntry entry))
                return false;
            int targetLegacyListIndex =
                inventory.FindFirstFreeStoreLegacyIndex();
            return targetLegacyListIndex > 0 &&
                   ExchangeWithInventory(
                       character,
                       inventory,
                       slot,
                       targetLegacyListIndex);
        }

        public bool RestoreEquipped(JxqyItemDefinition item)
        {
            return RestoreEquippedEntry(
                item,
                1,
                0f,
                GetLegacyListIndex(item?.Slot ?? JxqyEquipmentSlot.None));
        }

        public bool RestoreEquippedEntry(
            JxqyItemDefinition item,
            int count,
            float cooldownMilliseconds,
            int legacyListIndex)
        {
            if (item == null ||
                item.Kind != JxqyItemKind.Equipment ||
                item.Slot == JxqyEquipmentSlot.None ||
                _equipped.ContainsKey(item.Slot))
                return false;
            _equipped.Add(item.Slot, item);
            _equippedEntries.Add(
                item.Slot,
                new JxqyInventoryEntry(
                    item,
                    Math.Max(1, count),
                    legacyListIndex > 0
                        ? legacyListIndex
                        : GetLegacyListIndex(item.Slot))
                {
                    CooldownMilliseconds =
                        Math.Max(0f, cooldownMilliseconds),
                });
            return true;
        }

        public static int GetLegacyListIndex(JxqyEquipmentSlot slot)
        {
            return slot switch
            {
                JxqyEquipmentSlot.Head => 201,
                JxqyEquipmentSlot.Neck => 202,
                JxqyEquipmentSlot.Body => 203,
                JxqyEquipmentSlot.Back => 204,
                JxqyEquipmentSlot.Hand => 205,
                JxqyEquipmentSlot.Wrist => 206,
                JxqyEquipmentSlot.Foot => 207,
                _ => 0,
            };
        }

        public static bool TryGetSlotByLegacyListIndex(
            int legacyListIndex,
            out JxqyEquipmentSlot slot)
        {
            slot = legacyListIndex switch
            {
                201 => JxqyEquipmentSlot.Head,
                202 => JxqyEquipmentSlot.Neck,
                203 => JxqyEquipmentSlot.Body,
                204 => JxqyEquipmentSlot.Back,
                205 => JxqyEquipmentSlot.Hand,
                206 => JxqyEquipmentSlot.Wrist,
                207 => JxqyEquipmentSlot.Foot,
                _ => JxqyEquipmentSlot.None,
            };
            return slot != JxqyEquipmentSlot.None;
        }

        public JxqyMagicAdditionalEffect GetAdditionalAttackEffect()
        {
            if (!_equipped.TryGetValue(
                    JxqyEquipmentSlot.Hand,
                    out JxqyItemDefinition item))
            {
                return JxqyMagicAdditionalEffect.None;
            }
            return item.EffectKind switch
            {
                JxqyItemEffectKind.EnemyFrozen =>
                    JxqyMagicAdditionalEffect.Frozen,
                JxqyItemEffectKind.EnemyPoisoned =>
                    JxqyMagicAdditionalEffect.Poisoned,
                JxqyItemEffectKind.EnemyPetrified =>
                    JxqyMagicAdditionalEffect.Petrified,
                _ => JxqyMagicAdditionalEffect.None,
            };
        }

        public JxqyMagicAdditionalEffect GetAdditionalAttackEffect(
            JxqyCharacter character,
            JxqyMagicDefinition selectedMagic)
        {
            if (character == null || selectedMagic == null ||
                !ReferenceEquals(selectedMagic, character.BasicMagic) &&
                !ReferenceEquals(selectedMagic, character.BasicMagic2))
            {
                return JxqyMagicAdditionalEffect.None;
            }
            return GetAdditionalAttackEffect();
        }

        private static void Apply(
            JxqyCharacter target,
            JxqyStatModifiers value,
            int sign)
        {
            target.Attack += value.Attack * sign;
            target.Attack2 += value.Attack2 * sign;
            target.Attack3 += value.Attack3 * sign;
            target.Defend += value.Defend * sign;
            target.Defend2 += value.Defend2 * sign;
            target.Defend3 += value.Defend3 * sign;
            target.Evade += value.Evade * sign;
            target.LifeMax += value.LifeMax * sign;
            target.ThewMax += value.ThewMax * sign;
            target.ManaMax += value.ManaMax * sign;
            target.AddMoveSpeedPercent += value.MoveSpeedPercent * sign;
        }

        private static void ApplyPassive(
            JxqyCharacter target,
            JxqyItemDefinition item,
            bool enabled)
        {
            if (!(target is JxqyPlayer player))
                return;
            switch (item.EffectKind)
            {
                case JxqyItemEffectKind.ThewNotLoseWhenRun:
                    player.IsNotUseThewWhenRun = enabled;
                    break;
                case JxqyItemEffectKind.ManaRestore:
                    player.IsManaRestore = enabled;
                    break;
            }
        }
    }

    public sealed class JxqySkillEntry
    {
        internal JxqySkillEntry(
            JxqyMagicDefinition magic,
            int legacyListIndex)
        {
            Magic = magic;
            LegacyListIndex = legacyListIndex;
        }

        public JxqyMagicDefinition Magic { get; internal set; }
        public int Level { get; internal set; } = 1;
        public int Experience { get; internal set; }
        public float CooldownMilliseconds { get; internal set; }
        public int HideCount { get; internal set; }
        public int LegacyListIndex { get; internal set; }
    }

    public sealed class JxqySkillManager
    {
        private readonly List<JxqySkillEntry> _skills =
            new List<JxqySkillEntry>();

        public JxqySkillManager(int capacity = 49)
        {
            if (capacity < 1)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
        }

        public int Capacity { get; }
        public IReadOnlyList<JxqySkillEntry> Skills => _skills;

        public bool Learn(
            JxqyMagicDefinition magic,
            int legacyListIndex = 0)
        {
            if (magic == null)
                throw new ArgumentNullException(nameof(magic));
            if (Find(magic.Id) != null || _skills.Count >= Capacity)
                return false;
            if (legacyListIndex == 0)
                legacyListIndex = FindFirstFreeLegacyListIndex();
            _skills.Add(new JxqySkillEntry(magic, legacyListIndex));
            return true;
        }

        public bool SetLevel(string id, int level)
        {
            JxqySkillEntry entry = Find(id);
            if (entry == null)
                return false;
            int targetLevel = Math.Max(
                1,
                Math.Min(entry.Magic.MaximumLevel, level));
            entry.Level = targetLevel;
            entry.Magic.ApplyLevel(targetLevel);
            entry.Experience = targetLevel > 1
                ? entry.Magic.GetLevelUpExperience(targetLevel - 1)
                : 0;
            return true;
        }

        public bool AddExperience(
            string id,
            int amount,
            out bool leveledUp)
        {
            leveledUp = false;
            JxqySkillEntry entry = Find(id);
            if (entry == null || amount <= 0)
                return false;
            int threshold =
                entry.Magic.GetLevelUpExperience(entry.Level);
            if (threshold <= 0)
                return false;
            entry.Experience = checked(entry.Experience + amount);
            if (entry.Experience < threshold)
                return true;

            int nextLevel = Math.Min(
                entry.Magic.MaximumLevel,
                entry.Level + 1);
            if (nextLevel == entry.Level)
            {
                entry.Experience = threshold;
                return true;
            }
            entry.Level = nextLevel;
            entry.Magic.ApplyLevel(nextLevel);
            if (entry.Magic.GetLevelUpExperience(nextLevel) == 0)
                entry.Experience = threshold;
            leveledUp = true;
            return true;
        }

        public bool ReplaceDefinition(
            string id,
            JxqyMagicDefinition magic)
        {
            if (magic == null)
                throw new ArgumentNullException(nameof(magic));
            JxqySkillEntry entry = Find(id);
            if (entry == null)
                return false;
            magic.ApplyLevel(entry.Level);
            entry.Magic = magic;
            return true;
        }

        public bool RestoreEntry(
            JxqyMagicDefinition magic,
            int level,
            int experience,
            float cooldownMilliseconds,
            int hideCount,
            int legacyListIndex = 0)
        {
            if (!Learn(magic, legacyListIndex))
                return false;
            JxqySkillEntry entry = Find(magic.Id);
            entry.Level = Math.Max(
                1,
                Math.Min(magic.MaximumLevel, level));
            entry.Magic.ApplyLevel(entry.Level);
            entry.Experience = Math.Max(0, experience);
            entry.CooldownMilliseconds = Math.Max(
                0,
                cooldownMilliseconds);
            entry.HideCount = Math.Max(0, hideCount);
            return true;
        }

        public JxqySkillEntry FindAtLegacyIndex(int legacyListIndex)
        {
            return _skills.Find(entry =>
                entry.LegacyListIndex == legacyListIndex);
        }

        public bool MoveEntryToLegacyIndex(
            int entryIndex,
            int targetLegacyListIndex)
        {
            if (entryIndex < 0 ||
                entryIndex >= _skills.Count ||
                targetLegacyListIndex < 1)
            {
                return false;
            }
            JxqySkillEntry source = _skills[entryIndex];
            JxqySkillEntry target =
                FindAtLegacyIndex(targetLegacyListIndex);
            int sourceLegacyListIndex = source.LegacyListIndex;
            source.LegacyListIndex = targetLegacyListIndex;
            if (target != null)
                target.LegacyListIndex = sourceLegacyListIndex;
            _skills.Sort((left, right) =>
                left.LegacyListIndex.CompareTo(right.LegacyListIndex));
            return true;
        }

        public bool ExchangeEntries(int firstIndex, int secondIndex)
        {
            if (firstIndex < 0 ||
                firstIndex >= _skills.Count ||
                secondIndex < 0 ||
                secondIndex >= _skills.Count)
            {
                return false;
            }
            return MoveEntryToLegacyIndex(
                firstIndex,
                _skills[secondIndex].LegacyListIndex);
        }

        private int FindFirstFreeLegacyListIndex()
        {
            int storeEnd = Math.Min(36, Capacity);
            for (int index = 1; index <= storeEnd; index++)
            {
                if (FindAtLegacyIndex(index) == null)
                    return index;
            }
            for (int index = 40; index <= Math.Min(44, Capacity); index++)
            {
                if (FindAtLegacyIndex(index) == null)
                    return index;
            }
            return Math.Min(49, Capacity);
        }

        public bool BeginCooldown(string id, float milliseconds)
        {
            JxqySkillEntry entry = Find(id);
            if (entry == null || entry.CooldownMilliseconds > 0)
                return false;
            entry.CooldownMilliseconds = Math.Max(0, milliseconds);
            return true;
        }

        public void Tick(float elapsedMilliseconds)
        {
            foreach (JxqySkillEntry entry in _skills)
            {
                entry.CooldownMilliseconds = Math.Max(
                    0,
                    entry.CooldownMilliseconds - elapsedMilliseconds);
            }
        }

        public bool Forget(string id)
        {
            JxqySkillEntry entry = Find(id);
            return entry != null && _skills.Remove(entry);
        }

        public void Clear()
        {
            _skills.Clear();
        }

        public JxqySkillEntry Find(string id)
        {
            foreach (JxqySkillEntry entry in _skills)
            {
                if (string.Equals(
                    entry.Magic.Id,
                    id,
                    StringComparison.OrdinalIgnoreCase))
                    return entry;
            }
            return null;
        }
    }

    public sealed class JxqyShopStock
    {
        public JxqyShopStock(JxqyItemDefinition item, int count = -1)
        {
            Item = item ?? throw new ArgumentNullException(nameof(item));
            Count = count;
        }

        public JxqyItemDefinition Item { get; }
        public int Count { get; internal set; }
        public bool IsUnlimited => Count < 0;
    }

    public sealed class JxqyShop
    {
        private readonly Dictionary<string, JxqyShopStock> _stock =
            new Dictionary<string, JxqyShopStock>(
                StringComparer.OrdinalIgnoreCase);

        public int BuyPercentage { get; set; } = 100;
        public int RecyclePercentage { get; set; } = 100;
        public bool CanSellPlayerGoods { get; set; } = true;
        public IEnumerable<JxqyShopStock> Stock => _stock.Values;

        public void AddStock(JxqyItemDefinition item, int count = -1)
        {
            if (_stock.TryGetValue(item.Id, out JxqyShopStock current))
            {
                if (!current.IsUnlimited && count >= 0)
                    current.Count = checked(current.Count + count);
                return;
            }
            _stock.Add(item.Id, new JxqyShopStock(item, count));
        }

        public bool Buy(
            string itemId,
            int count,
            JxqyPlayer player,
            JxqyInventory inventory)
        {
            if (count < 1 || !_stock.TryGetValue(itemId, out JxqyShopStock stock) ||
                !stock.IsUnlimited && stock.Count < count)
                return false;
            int price = checked(stock.Item.GetBuyPrice(BuyPercentage) * count);
            if (player.Money < price)
                return false;
            if (!inventory.Add(stock.Item, count))
                return false;
            player.Money -= price;
            if (!stock.IsUnlimited)
                stock.Count -= count;
            return true;
        }

        public bool Sell(
            string itemId,
            int count,
            JxqyPlayer player,
            JxqyInventory inventory)
        {
            if (!CanSellPlayerGoods || count < 1 ||
                inventory.Count(itemId) < count)
                return false;
            JxqyItemDefinition item = null;
            foreach (JxqyInventoryEntry entry in inventory.Entries)
            {
                if (string.Equals(
                    entry.Definition.Id,
                    itemId,
                    StringComparison.OrdinalIgnoreCase))
                {
                    item = entry.Definition;
                    break;
                }
            }
            if (item == null || !inventory.Remove(itemId, count))
                return false;
            player.Money += checked(item.GetSellPrice(RecyclePercentage) * count);
            AddStock(item, count);
            return true;
        }
    }
}
