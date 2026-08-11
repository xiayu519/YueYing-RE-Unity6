using System;
using System.Collections.Generic;
using Jxqy.Domain.Simulation;
using Jxqy.Domain.World;

namespace Jxqy.Domain.Presentation
{
    public enum JxqyUiScreen
    {
        Title,
        Hud,
        Dialogue,
        Selection,
        Status,
        Inventory,
        Equipment,
        Training,
        Skills,
        Memo,
        LittleMap,
        Trade,
        Menu,
        SaveLoad,
    }

    public enum JxqyDialoguePresentation
    {
        Dialogue,
        Selection,
    }

    public enum JxqySaveUiAction
    {
        Save,
        Load,
    }

    public enum JxqyUiSound
    {
        DragUp,
        DragDrop,
        WindowOpen,
        WindowClose,
        UseGoods,
        BuyGoods,
        LargeButton,
        Button,
        Browse,
        MainMenu,
    }

    public sealed class JxqyDialogueChoice
    {
        public JxqyDialogueChoice(string text, string value)
        {
            Text = text ?? string.Empty;
            Value = value ?? string.Empty;
        }

        public string Text { get; }
        public string Value { get; }
    }

    public sealed class JxqyDialoguePage
    {
        public string Speaker { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string PortraitFileName { get; set; } = string.Empty;
        public int SelectionCount { get; set; } = 1;
        public int SelectionColumns { get; set; } = 1;
        public JxqyDialoguePresentation Presentation { get; set; } =
            JxqyDialoguePresentation.Dialogue;
        public List<JxqyDialogueChoice> Choices { get; } =
            new List<JxqyDialogueChoice>();
    }

    public sealed class JxqyDialogue
    {
        private readonly List<JxqyDialoguePage> _pages =
            new List<JxqyDialoguePage>();
        private readonly List<string> _selectedChoiceValues =
            new List<string>();

        public IReadOnlyList<JxqyDialoguePage> Pages => _pages;
        public int PageIndex { get; private set; }
        public int ChoiceIndex { get; private set; }
        public bool IsComplete { get; private set; }
        public JxqyDialoguePage Current =>
            _pages.Count == 0 || IsComplete ? null : _pages[PageIndex];
        public IReadOnlyList<string> SelectedChoiceValues =>
            _selectedChoiceValues;

        public void Add(JxqyDialoguePage page)
        {
            if (page == null)
                throw new ArgumentNullException(nameof(page));
            if (IsComplete)
                throw new InvalidOperationException("对话已经结束。");
            _pages.Add(page);
        }

        public void MoveChoice(int offset)
        {
            int count = Current?.Choices.Count ?? 0;
            if (count == 0)
                return;
            ChoiceIndex = (ChoiceIndex + offset) % count;
            if (ChoiceIndex < 0)
                ChoiceIndex += count;
        }

        public string Confirm()
        {
            if (Current == null)
                return null;
            if (Current.Choices.Count > 0)
            {
                string value = Current.Choices[ChoiceIndex].Value;
                int required = Math.Max(1, Current.SelectionCount);
                if (required > 1)
                {
                    if (_selectedChoiceValues.Contains(value))
                    {
                        _selectedChoiceValues.Remove(value);
                        return null;
                    }
                    _selectedChoiceValues.Add(value);
                    if (_selectedChoiceValues.Count < required)
                        return null;
                    value = string.Join(",", _selectedChoiceValues);
                }
                IsComplete = true;
                return value;
            }
            PageIndex++;
            ChoiceIndex = 0;
            if (PageIndex >= _pages.Count)
                IsComplete = true;
            return null;
        }
    }

    public sealed class JxqySaveSlotView
    {
        public int Slot { get; set; }
        public bool Exists { get; set; }
        public string Description { get; set; } = "空存档";
        public string SavedAt { get; set; } = string.Empty;
        public byte[] SnapshotPng { get; set; }
    }

    public sealed class JxqyUiSession
    {
        private readonly List<JxqyUiScreen> _stack =
            new List<JxqyUiScreen>();
        private int _selection;

        public event Action Changed;
        public event Action<int> SaveRequested;
        public event Action<int> LoadRequested;
        public event Action NewGameRequested;
        public event Action CreditsRequested;
        public event Action QuitRequested;
        public event Action<string> DialogueCompleted;
        public event Action<JxqyItemDefinition> ItemUsed;
        public event Action<JxqyInventoryEntry> ItemScriptRequested;
        public event Action<JxqyUiSound> SoundRequested;

        public JxqyPlayer Player { get; set; }
        public JxqyCharacter CombatTarget { get; set; }
        public JxqyInventory Inventory { get; set; }
        public JxqyEquipmentManager Equipment { get; set; }
        public IReadOnlyList<JxqyNpc> Npcs { get; set; } =
            Array.Empty<JxqyNpc>();
        public string LittleMapTextureAddress { get; set; } = string.Empty;
        public string LittleMapName { get; set; } = string.Empty;
        public int LittleMapViewX { get; set; }
        public int LittleMapViewY { get; set; }
        public Func<JxqyFloat2, bool, bool> TryMoveFromLittleMap { get; set; }
        public JxqyNpc PartnerEquipmentTarget { get; private set; }
        public JxqyCharacter EquipmentOwner =>
            PartnerEquipmentTarget ?? (JxqyCharacter)Player;
        public JxqyEquipmentManager ActiveEquipment =>
            PartnerEquipmentTarget?.Equipment ?? Equipment;
        public JxqySkillManager Skills { get; set; }
        public JxqyShop Shop { get; set; }
        public IReadOnlyList<string> Memos { get; set; } =
            Array.Empty<string>();
        public Func<bool> CanSave { get; set; }
        public JxqyDialogue Dialogue { get; private set; }
        public JxqySaveUiAction SaveAction { get; set; }
        public string Notice { get; private set; } = string.Empty;
        public int NoticeSequence { get; private set; }
        public string Message { get; private set; } = string.Empty;
        public int MessageSequence { get; private set; }
        public string SystemMessage { get; private set; } = string.Empty;
        public int SystemMessageDurationMilliseconds { get; private set; }
        public int SystemMessageSequence { get; private set; }
        public bool TimerVisible { get; private set; }
        public int TimerSeconds { get; private set; }
        public List<JxqySaveSlotView> SaveSlots { get; } =
            new List<JxqySaveSlotView>();
        public JxqySkillEntry SelectedSkill { get; private set; }
        public JxqyUiScreen CurrentScreen =>
            ActiveModalScreen ??
            RightPanelScreen ??
            LeftPanelScreen ??
            JxqyUiScreen.Hud;
        public JxqyUiScreen? ActiveModalScreen =>
            _stack.Count == 0
                ? null
                : _stack[_stack.Count - 1];
        public JxqyUiScreen? LeftPanelScreen { get; private set; }
        public JxqyUiScreen? RightPanelScreen { get; private set; }
        public int Selection => _selection;
        public bool IsModal =>
            ActiveModalScreen.HasValue ||
            LeftPanelScreen.HasValue ||
            RightPanelScreen.HasValue;
        public bool IsSaveAllowed => CanSave?.Invoke() ?? true;
        public bool FadeVisible { get; private set; }
        public bool FadeUiReady { get; private set; }
        public float FadeOpacity { get; private set; }

        public void ShowFade(float opacity)
        {
            if (!FadeVisible)
                FadeUiReady = false;
            FadeVisible = true;
            FadeOpacity = Math.Max(0f, Math.Min(1f, opacity));
            Changed?.Invoke();
        }

        public void SetFadeOpacity(float opacity)
        {
            FadeOpacity = Math.Max(0f, Math.Min(1f, opacity));
        }

        public void NotifyFadeUiReady()
        {
            FadeUiReady = true;
        }

        public void NotifyInventoryChanged()
        {
            Changed?.Invoke();
        }

        public void HideFade()
        {
            FadeVisible = false;
            FadeUiReady = false;
            FadeOpacity = 0f;
            Changed?.Invoke();
        }

        public void ShowTitle()
        {
            _stack.Clear();
            _stack.Add(JxqyUiScreen.Title);
            LeftPanelScreen = null;
            RightPanelScreen = null;
            _selection = 0;
            ClearNotice();
            Changed?.Invoke();
        }

        public void Open(JxqyUiScreen screen)
        {
            bool closeWindow = false;
            bool openWindow = false;
            if (screen == JxqyUiScreen.Hud)
            {
                closeWindow = HasSoundWindowOpen();
                _stack.Clear();
                LeftPanelScreen = null;
                RightPanelScreen = null;
            }
            else if (screen == JxqyUiScreen.LittleMap)
            {
                // The original closes every side panel before showing the
                // Tab map. It never layers the system menu over the map.
                _stack.Clear();
                LeftPanelScreen = null;
                RightPanelScreen = null;
                _stack.Add(screen);
            }
            else if (IsLeftPanelScreen(screen))
            {
                closeWindow = LeftPanelScreen.HasValue &&
                              LeftPanelScreen != screen;
                openWindow = LeftPanelScreen != screen;
                LeftPanelScreen = screen;
            }
            else if (IsRightPanelScreen(screen))
            {
                closeWindow = RightPanelScreen.HasValue &&
                              RightPanelScreen != screen;
                openWindow = RightPanelScreen != screen;
                RightPanelScreen = screen;
            }
            else if (ActiveModalScreen != screen)
            {
                _stack.Add(screen);
                openWindow = IsSoundWindow(screen);
            }
            _selection = 0;
            ClearNotice();
            if (closeWindow)
                RequestSound(JxqyUiSound.WindowClose);
            if (openWindow)
                RequestSound(JxqyUiSound.WindowOpen);
            Changed?.Invoke();
        }

        public void Toggle(JxqyUiScreen screen)
        {
            if (IsLeftPanelScreen(screen))
            {
                bool closeWindow = LeftPanelScreen == screen;
                bool replaceWindow = LeftPanelScreen.HasValue &&
                                     !closeWindow;
                LeftPanelScreen =
                    closeWindow ? null : screen;
                _selection = 0;
                ClearNotice();
                if (closeWindow || replaceWindow)
                    RequestSound(JxqyUiSound.WindowClose);
                if (!closeWindow)
                    RequestSound(JxqyUiSound.WindowOpen);
                Changed?.Invoke();
                return;
            }
            if (IsRightPanelScreen(screen))
            {
                bool closeWindow = RightPanelScreen == screen;
                bool replaceWindow = RightPanelScreen.HasValue &&
                                     !closeWindow;
                RightPanelScreen =
                    closeWindow ? null : screen;
                _selection = 0;
                ClearNotice();
                if (closeWindow || replaceWindow)
                    RequestSound(JxqyUiSound.WindowClose);
                if (!closeWindow)
                    RequestSound(JxqyUiSound.WindowOpen);
                Changed?.Invoke();
                return;
            }
            Open(ActiveModalScreen == screen
                ? JxqyUiScreen.Hud
                : screen);
        }

        public bool IsOpen(JxqyUiScreen screen)
        {
            return _stack.Contains(screen) ||
                   LeftPanelScreen == screen ||
                   RightPanelScreen == screen;
        }

        public void Close(JxqyUiScreen screen)
        {
            bool changed = _stack.RemoveAll(
                               value => value == screen) > 0;
            if (LeftPanelScreen == screen)
            {
                LeftPanelScreen = null;
                changed = true;
            }
            if (RightPanelScreen == screen)
            {
                RightPanelScreen = null;
                changed = true;
            }
            if (!changed)
                return;
            _selection = 0;
            ClearNotice();
            if (IsSoundWindow(screen))
                RequestSound(JxqyUiSound.WindowClose);
            Changed?.Invoke();
        }

        public void OpenPlayerEquipment()
        {
            PartnerEquipmentTarget = null;
            Toggle(JxqyUiScreen.Equipment);
        }

        public void OpenPartnerEquipment(JxqyNpc partner)
        {
            if (partner == null ||
                partner.Kind != JxqyCharacterKind.Follower ||
                partner.CanEquip <= 0)
            {
                return;
            }
            PartnerEquipmentTarget = partner;
            _stack.Clear();
            LeftPanelScreen = JxqyUiScreen.Equipment;
            RightPanelScreen = JxqyUiScreen.Inventory;
            _selection = 0;
            ClearNotice();
            RequestSound(JxqyUiSound.WindowOpen);
            Changed?.Invoke();
        }

        public void StartDialogue(JxqyDialogue dialogue)
        {
            Dialogue = dialogue ?? throw new ArgumentNullException(nameof(dialogue));
            Open(dialogue.Current?.Presentation ==
                 JxqyDialoguePresentation.Selection
                ? JxqyUiScreen.Selection
                : JxqyUiScreen.Dialogue);
        }

        public void Cancel()
        {
            if (CurrentScreen == JxqyUiScreen.Title)
                return;
            bool closeWindow = IsSoundWindow(CurrentScreen);
            if (_stack.Count > 0)
                _stack.RemoveAt(_stack.Count - 1);
            else if (RightPanelScreen.HasValue)
                RightPanelScreen = null;
            else if (LeftPanelScreen.HasValue)
                LeftPanelScreen = null;
            _selection = 0;
            ClearNotice();
            if (closeWindow)
                RequestSound(JxqyUiSound.WindowClose);
            Changed?.Invoke();
        }

        public void RequestSound(JxqyUiSound sound)
        {
            SoundRequested?.Invoke(sound);
        }

        private static bool IsLeftPanelScreen(JxqyUiScreen screen)
        {
            return screen == JxqyUiScreen.Status ||
                   screen == JxqyUiScreen.Equipment ||
                   screen == JxqyUiScreen.Training;
        }

        private static bool IsRightPanelScreen(JxqyUiScreen screen)
        {
            return screen == JxqyUiScreen.Inventory ||
                   screen == JxqyUiScreen.Skills ||
                   screen == JxqyUiScreen.Memo;
        }

        private bool HasSoundWindowOpen()
        {
            return IsSoundWindow(ActiveModalScreen) ||
                   IsSoundWindow(LeftPanelScreen) ||
                   IsSoundWindow(RightPanelScreen);
        }

        private static bool IsSoundWindow(JxqyUiScreen? screen)
        {
            return screen.HasValue && IsSoundWindow(screen.Value);
        }

        private static bool IsSoundWindow(JxqyUiScreen screen)
        {
            return screen == JxqyUiScreen.Status ||
                   screen == JxqyUiScreen.Inventory ||
                   screen == JxqyUiScreen.Equipment ||
                   screen == JxqyUiScreen.Training ||
                   screen == JxqyUiScreen.Skills ||
                   screen == JxqyUiScreen.Memo ||
                   screen == JxqyUiScreen.Trade ||
                   screen == JxqyUiScreen.Menu ||
                   screen == JxqyUiScreen.SaveLoad;
        }

        public void ShowOptionsNotice()
        {
            SetNotice("请用游戏设置程序进行设置");
        }

        public void SetNotice(string notice)
        {
            Notice = notice ?? string.Empty;
            NoticeSequence = checked(NoticeSequence + 1);
            Changed?.Invoke();
        }

        private void ClearNotice()
        {
            if (string.IsNullOrEmpty(Notice))
                return;
            Notice = string.Empty;
            NoticeSequence = checked(NoticeSequence + 1);
        }

        public void ShowMessage(string message)
        {
            Message = message ?? string.Empty;
            MessageSequence = checked(MessageSequence + 1);
            Changed?.Invoke();
        }

        public void ShowSystemMessage(
            string message,
            int durationMilliseconds = 3000)
        {
            SystemMessage = message ?? string.Empty;
            SystemMessageDurationMilliseconds = Math.Max(
                0,
                durationMilliseconds);
            SystemMessageSequence = checked(SystemMessageSequence + 1);
            Changed?.Invoke();
        }

        public void SetTimer(bool visible, int seconds)
        {
            int normalizedSeconds = Math.Max(0, seconds);
            if (TimerVisible == visible &&
                TimerSeconds == normalizedSeconds)
            {
                return;
            }
            TimerVisible = visible;
            TimerSeconds = normalizedSeconds;
            Changed?.Invoke();
        }

        public void Refresh()
        {
            Changed?.Invoke();
        }

        public void MoveSelection(int offset)
        {
            int count = GetRows().Count;
            if (IsDialogueScreen(CurrentScreen))
            {
                Dialogue?.MoveChoice(offset);
                Changed?.Invoke();
                return;
            }
            if (count == 0)
                return;
            _selection = (_selection + offset) % count;
            if (_selection < 0)
                _selection += count;
            Changed?.Invoke();
        }

        public void Select(int index)
        {
            int count = GetRows().Count;
            if (count == 0)
                return;
            _selection = Math.Max(0, Math.Min(index, count - 1));
            if (IsDialogueScreen(CurrentScreen) &&
                Dialogue?.Current != null)
            {
                int choiceOffset =
                    _selection - Dialogue.ChoiceIndex;
                Dialogue.MoveChoice(choiceOffset);
            }
            Changed?.Invoke();
        }

        public bool UseInventoryItem(int index)
        {
            if (Inventory == null || Player == null ||
                Player.IsDead ||
                index < 0 || index >= Inventory.Entries.Count)
                return false;
            _selection = index;
            JxqyInventoryEntry entry = Inventory.Entries[index];
            JxqyItemDefinition item = entry.Definition;
            bool result;
            switch (item.Kind)
            {
                case JxqyItemKind.Equipment:
                    result = ActiveEquipment != null &&
                             EquipmentOwner != null &&
                             ActiveEquipment.Equip(
                                 EquipmentOwner, Inventory, item.Id);
                    break;
                case JxqyItemKind.Drug:
                    result = Inventory.Use(item.Id, Player);
                    break;
                case JxqyItemKind.Event:
                    result = !string.IsNullOrWhiteSpace(item.UseScript);
                    if (result)
                        ItemScriptRequested?.Invoke(entry);
                    break;
                default:
                    result = false;
                    break;
            }
            if (result && item.Kind == JxqyItemKind.Drug)
            {
                ItemUsed?.Invoke(item);
                RequestSound(JxqyUiSound.UseGoods);
            }
            else if (!result && item.Kind == JxqyItemKind.Drug)
            {
                if (entry.CooldownMilliseconds > 0)
                    SetNotice("\u7269\u54c1\u5c1a\u672a\u51b7\u5374");
                else if (Player.Level < item.MinimumUserLevel)
                    SetNotice("\u7b49\u7ea7\u4e0d\u8db3\uff0c\u65e0\u6cd5\u4f7f\u7528");
                else
                    SetNotice("\u5f53\u524d\u65e0\u6cd5\u4f7f\u7528\u8be5\u7269\u54c1");
                return false;
            }
            Changed?.Invoke();
            return result;
        }

        public bool EquipInventoryItem(int index)
        {
            if (ActiveEquipment == null || Inventory == null ||
                EquipmentOwner == null ||
                Player?.IsDead == true ||
                index < 0 || index >= Inventory.Entries.Count)
                return false;
            _selection = index;
            bool result = ActiveEquipment.Equip(
                EquipmentOwner,
                Inventory,
                Inventory.Entries[index].Definition.Id);
            Changed?.Invoke();
            return result;
        }

        public bool Unequip(JxqyEquipmentSlot slot)
        {
            if (ActiveEquipment == null || Inventory == null ||
                EquipmentOwner == null || Player?.IsDead == true)
                return false;
            bool result = ActiveEquipment.Unequip(
                EquipmentOwner, Inventory, slot);
            Changed?.Invoke();
            return result;
        }

        public bool ExchangeEquipmentWithInventory(
            JxqyEquipmentSlot slot,
            int inventoryLegacyListIndex)
        {
            if (ActiveEquipment == null || Inventory == null ||
                EquipmentOwner == null || Player?.IsDead == true)
                return false;
            bool result = ActiveEquipment.ExchangeWithInventory(
                EquipmentOwner,
                Inventory,
                slot,
                inventoryLegacyListIndex);
            Changed?.Invoke();
            return result;
        }

        public bool MoveInventoryEntry(
            int sourceIndex,
            int targetIndex)
        {
            if (Inventory == null)
                return false;
            bool result =
                Inventory.ExchangeEntries(sourceIndex, targetIndex);
            if (result)
                _selection = Math.Max(0, targetIndex);
            Changed?.Invoke();
            return result;
        }

        public bool MoveInventoryEntryToLegacyIndex(
            int sourceIndex,
            int targetLegacyListIndex)
        {
            if (Inventory == null)
                return false;
            bool result = Inventory.MoveEntryToLegacyIndex(
                sourceIndex,
                targetLegacyListIndex);
            Changed?.Invoke();
            return result;
        }

        public bool SelectSkill(int index)
        {
            if (Skills == null || index < 0 || index >= Skills.Skills.Count)
                return false;
            _selection = index;
            SelectedSkill = Skills.Skills[index];
            Changed?.Invoke();
            return true;
        }

        public void ClearSelectedSkill()
        {
            SelectedSkill = null;
            _selection = 0;
            Changed?.Invoke();
        }

        public bool MoveSkillEntry(int sourceIndex, int targetIndex)
        {
            if (Skills == null)
                return false;
            bool result = Skills.ExchangeEntries(
                sourceIndex,
                targetIndex);
            if (result)
                _selection = Math.Max(0, targetIndex);
            Changed?.Invoke();
            return result;
        }

        public bool MoveSkillEntryToLegacyIndex(
            int sourceIndex,
            int targetLegacyListIndex)
        {
            if (Skills == null)
                return false;
            bool result = Skills.MoveEntryToLegacyIndex(
                sourceIndex,
                targetLegacyListIndex);
            if (result &&
                SelectedSkill != null &&
                (SelectedSkill.LegacyListIndex < 40 ||
                 SelectedSkill.LegacyListIndex > 44))
            {
                SelectedSkill = null;
            }
            Changed?.Invoke();
            return result;
        }

        public bool BuyShopItem(int index)
        {
            if (Shop == null || Inventory == null || Player == null ||
                Player.IsDead)
                return false;
            var stocks = new List<JxqyShopStock>(Shop.Stock);
            if (index < 0 || index >= stocks.Count)
                return false;
            bool result = Shop.Buy(
                stocks[index].Item.Id,
                1,
                Player,
                Inventory);
            if (result)
                RequestSound(JxqyUiSound.BuyGoods);
            Changed?.Invoke();
            return result;
        }

        public bool SellInventoryItem(int index)
        {
            if (Shop == null || Inventory == null || Player == null ||
                Player.IsDead ||
                index < 0 || index >= Inventory.Entries.Count)
                return false;
            bool result = Shop.Sell(
                Inventory.Entries[index].Definition.Id,
                1,
                Player,
                Inventory);
            if (result)
                RequestSound(JxqyUiSound.BuyGoods);
            Changed?.Invoke();
            return result;
        }

        public void OpenSaveLoad(JxqySaveUiAction action)
        {
            SaveAction = action;
            Open(JxqyUiScreen.SaveLoad);
        }

        public bool RequestSave(int slotIndex)
        {
            bool saveAllowed = IsSaveAllowed;
            if (slotIndex < 0 ||
                slotIndex >= SaveSlots.Count ||
                !saveAllowed)
            {
                if (!saveAllowed)
                {
                    Notice = "当前状态不能存档";
                    Changed?.Invoke();
                }
                return false;
            }
            SaveAction = JxqySaveUiAction.Save;
            _selection = slotIndex;
            SaveRequested?.Invoke(SaveSlots[slotIndex].Slot);
            Changed?.Invoke();
            return true;
        }

        public bool RequestLoad(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SaveSlots.Count ||
                !SaveSlots[slotIndex].Exists)
                return false;
            SaveAction = JxqySaveUiAction.Load;
            _selection = slotIndex;
            LoadRequested?.Invoke(SaveSlots[slotIndex].Slot);
            Changed?.Invoke();
            return true;
        }

        public void ReturnToTitle()
        {
            QuitRequested?.Invoke();
        }

        public bool Confirm()
        {
            bool changed;
            switch (CurrentScreen)
            {
                case JxqyUiScreen.Title:
                    changed = ConfirmTitle();
                    break;
                case JxqyUiScreen.Dialogue:
                case JxqyUiScreen.Selection:
                    changed = ConfirmDialogue();
                    break;
                case JxqyUiScreen.Inventory:
                    changed = UseSelectedItem();
                    break;
                case JxqyUiScreen.Equipment:
                    changed = EquipSelectedItem();
                    break;
                case JxqyUiScreen.Training:
                    changed = false;
                    break;
                case JxqyUiScreen.Skills:
                    changed = SelectSkill();
                    break;
                case JxqyUiScreen.Trade:
                    changed = BuySelectedItem();
                    break;
                case JxqyUiScreen.Menu:
                    changed = ConfirmMenu();
                    break;
                case JxqyUiScreen.SaveLoad:
                    changed = ConfirmSaveSlot();
                    break;
                default:
                    changed = false;
                    break;
            }
            Changed?.Invoke();
            return changed;
        }

        public bool Secondary()
        {
            if (CurrentScreen != JxqyUiScreen.Trade)
                return false;
            IReadOnlyList<JxqyInventoryEntry> entries =
                Inventory?.Entries;
            if (entries == null || entries.Count == 0)
                return false;
            int index = Math.Min(_selection, entries.Count - 1);
            bool result = Shop.Sell(
                entries[index].Definition.Id,
                1,
                Player,
                Inventory);
            if (result)
                RequestSound(JxqyUiSound.BuyGoods);
            Changed?.Invoke();
            return result;
        }

        public IReadOnlyList<string> GetRows()
        {
            var rows = new List<string>();
            switch (CurrentScreen)
            {
                case JxqyUiScreen.Title:
                    rows.Add("开始游戏");
                    rows.Add("读取存档");
                    rows.Add("制作群");
                    rows.Add("退出游戏");
                    break;
                case JxqyUiScreen.Dialogue:
                case JxqyUiScreen.Selection:
                    if (Dialogue?.Current != null)
                    {
                        foreach (JxqyDialogueChoice choice in
                            Dialogue.Current.Choices)
                            rows.Add(choice.Text);
                    }
                    break;
                case JxqyUiScreen.Inventory:
                case JxqyUiScreen.Equipment:
                    if (Inventory != null)
                    {
                        foreach (JxqyInventoryEntry entry in Inventory.Entries)
                            rows.Add($"{entry.Definition.Name} ×{entry.Count}");
                    }
                    break;
                case JxqyUiScreen.Training:
                    JxqySkillEntry cultivation =
                        Skills?.FindAtLegacyIndex(49);
                    if (cultivation != null)
                    {
                        rows.Add(
                            $"{cultivation.Magic.Name}  " +
                            $"Lv.{cultivation.Level}");
                    }
                    break;
                case JxqyUiScreen.Skills:
                    if (Skills != null)
                    {
                        foreach (JxqySkillEntry entry in Skills.Skills)
                            rows.Add($"{entry.Magic.Id}  Lv.{entry.Level}");
                    }
                    break;
                case JxqyUiScreen.Memo:
                    if (Memos != null)
                    {
                        for (int index = Memos.Count - 1;
                             index >= 0;
                             index--)
                        {
                            rows.Add(Memos[index] ?? string.Empty);
                        }
                    }
                    break;
                case JxqyUiScreen.Trade:
                    if (Shop != null)
                    {
                        foreach (JxqyShopStock stock in Shop.Stock)
                        {
                            string count = stock.IsUnlimited
                                ? "∞"
                                : stock.Count.ToString();
                            rows.Add(
                                $"{stock.Item.Name}  {stock.Item.GetBuyPrice(Shop.BuyPercentage)} ({count})");
                        }
                    }
                    break;
                case JxqyUiScreen.Menu:
                    rows.Add("读取存储");
                    rows.Add("游戏选项");
                    rows.Add("退出游戏");
                    rows.Add("返回游戏");
                    break;
                case JxqyUiScreen.SaveLoad:
                    foreach (JxqySaveSlotView slot in SaveSlots)
                        rows.Add($"存档 {slot.Slot}: {slot.Description}");
                    break;
            }
            return rows;
        }

        public string GetTitle()
        {
            switch (CurrentScreen)
            {
                case JxqyUiScreen.Title:
                    return "剑侠情缘外传：月影传说";
                case JxqyUiScreen.Hud: return Player?.Name ?? "HUD";
                case JxqyUiScreen.Dialogue:
                case JxqyUiScreen.Selection:
                    return Dialogue?.Current?.Speaker ?? string.Empty;
                case JxqyUiScreen.Status: return "角色状态";
                case JxqyUiScreen.Inventory: return "物品";
                case JxqyUiScreen.Equipment: return "装备";
                case JxqyUiScreen.Training: return "武功修炼";
                case JxqyUiScreen.Skills: return "武功";
                case JxqyUiScreen.Memo: return "任务";
                case JxqyUiScreen.Trade: return "交易";
                case JxqyUiScreen.Menu: return "菜单";
                case JxqyUiScreen.SaveLoad:
                    return SaveAction == JxqySaveUiAction.Save
                        ? "保存游戏"
                        : "读取游戏";
                default: return string.Empty;
            }
        }

        public string GetBody()
        {
            switch (CurrentScreen)
            {
                case JxqyUiScreen.Title:
                {
                    IReadOnlyList<string> titleRows = GetRows();
                    var titleLines = new List<string>(titleRows.Count);
                    for (int index = 0;
                         index < titleRows.Count;
                         index++)
                        titleLines.Add(
                            (index == _selection ? "▶ " : "  ") +
                            titleRows[index]);
                    return string.Join("\n", titleLines);
                }
                case JxqyUiScreen.Hud:
                case JxqyUiScreen.Status:
                    if (Player == null)
                        return string.Empty;
                    return
                        $"生命 {Player.Life}/{Player.LifeMax}\n" +
                        $"内力 {Player.Mana}/{Player.ManaMax}\n" +
                        $"体力 {Player.Thew}/{Player.ThewMax}\n" +
                        $"等级 {Player.Level}  金钱 {Player.Money}";
                case JxqyUiScreen.Dialogue:
                case JxqyUiScreen.Selection:
                    return Dialogue?.Current?.Text ?? string.Empty;
                default:
                    IReadOnlyList<string> rows = GetRows();
                    if (rows.Count == 0)
                        return "（空）";
                    var lines = new List<string>(rows.Count);
                    for (int index = 0; index < rows.Count; index++)
                        lines.Add((index == _selection ? "▶ " : "  ") + rows[index]);
                    return string.Join("\n", lines);
            }
        }

        private bool ConfirmDialogue()
        {
            if (Dialogue == null)
                return false;
            string choice = Dialogue.Confirm();
            if (Dialogue.IsComplete)
            {
                DialogueCompleted?.Invoke(choice);
                Dialogue = null;
                Cancel();
            }
            return true;
        }

        private static bool IsDialogueScreen(JxqyUiScreen screen)
        {
            return screen == JxqyUiScreen.Dialogue ||
                   screen == JxqyUiScreen.Selection;
        }

        private bool UseSelectedItem()
        {
            if (Inventory == null || Player == null ||
                _selection >= Inventory.Entries.Count)
                return false;
            return UseInventoryItem(_selection);
        }

        private bool EquipSelectedItem()
        {
            if (Equipment == null || Inventory == null || Player == null ||
                _selection >= Inventory.Entries.Count)
                return false;
            return Equipment.Equip(
                Player,
                Inventory,
                Inventory.Entries[_selection].Definition.Id);
        }

        private bool SelectSkill()
        {
            if (Skills == null || _selection >= Skills.Skills.Count)
                return false;
            SelectedSkill = Skills.Skills[_selection];
            Cancel();
            return true;
        }

        private bool BuySelectedItem()
        {
            if (Shop == null || Inventory == null || Player == null)
                return false;
            var stocks = new List<JxqyShopStock>(Shop.Stock);
            if (_selection >= stocks.Count)
                return false;
            bool result = Shop.Buy(
                stocks[_selection].Item.Id,
                1,
                Player,
                Inventory);
            if (result)
                RequestSound(JxqyUiSound.BuyGoods);
            return result;
        }

        private bool ConfirmMenu()
        {
            switch (_selection)
            {
                case 0:
                    SaveAction = JxqySaveUiAction.Load;
                    Open(JxqyUiScreen.SaveLoad);
                    return true;
                case 1:
                    ShowOptionsNotice();
                    return true;
                case 2:
                    QuitRequested?.Invoke();
                    return true;
                case 3:
                    Cancel();
                    return true;
                default:
                    return false;
            }
        }

        private bool ConfirmTitle()
        {
            switch (_selection)
            {
                case 0:
                    _stack.Clear();
                    _selection = 0;
                    NewGameRequested?.Invoke();
                    return true;
                case 1:
                    SaveAction = JxqySaveUiAction.Load;
                    Open(JxqyUiScreen.SaveLoad);
                    return true;
                case 2:
                    CreditsRequested?.Invoke();
                    return true;
                case 3:
                    QuitRequested?.Invoke();
                    return true;
                default:
                    return false;
            }
        }

        private bool ConfirmSaveSlot()
        {
            if (_selection >= SaveSlots.Count)
                return false;
            int slot = SaveSlots[_selection].Slot;
            if (SaveAction == JxqySaveUiAction.Save)
            {
                if (!IsSaveAllowed)
                {
                    Notice = "当前状态不能存档";
                    Changed?.Invoke();
                    return false;
                }
                SaveRequested?.Invoke(slot);
            }
            else if (SaveSlots[_selection].Exists)
                LoadRequested?.Invoke(slot);
            else
                return false;
            return true;
        }
    }
}
