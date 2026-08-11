using System;
using System.Collections.Generic;
using Jxqy.Domain.Presentation;
using Jxqy.Domain.Simulation;
using TEngine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameLogic
{
    public abstract class JxqySessionWindow : UIWindow
    {
        protected JxqyUiSession Session { get; private set; }
        protected virtual JxqyUiSound? DefaultButtonSound => null;

        protected sealed override void RegisterEvent()
        {
            AddUIEvent(IJxqyUI_Event.OnJxqyUiChanged, OnUiChanged);
        }

        protected override void OnCreate()
        {
            AttachSession();
            BindDefaultButtonSounds();
            RefreshView();
        }

        protected override void OnRefresh()
        {
            AttachSession();
            RefreshView();
        }

        protected abstract void RefreshView();

        private void AttachSession()
        {
            Session = UserData as JxqyUiSession;
        }

        private void OnUiChanged()
        {
            RefreshView();
        }

        protected void RequestUiSound(JxqyUiSound sound)
        {
            Session?.RequestSound(sound);
        }

        protected void BindButtonSound(
            Button button,
            JxqyUiSound sound)
        {
            if (button == null)
                return;
            button.onClick.AddListener(() => RequestUiSound(sound));
        }

        private void BindDefaultButtonSounds()
        {
            if (!DefaultButtonSound.HasValue)
                return;
            JxqyUiSound sound = DefaultButtonSound.Value;
            Button[] buttons = rectTransform == null
                ? Array.Empty<Button>()
                : rectTransform.GetComponentsInChildren<Button>(true);
            for (int index = 0; index < buttons.Length; index++)
            {
                Button button = buttons[index];
                if (button == null ||
                    button.GetComponent<JxqyListSlotEventRelay>() != null)
                {
                    continue;
                }
                button.onClick.AddListener(
                    () => RequestUiSound(sound));
            }
        }

        protected static void SetButtonVisible(Button button, bool visible)
        {
            if (button != null)
                button.gameObject.SetActive(visible);
        }

        protected static void ClearButton(Button button)
        {
            button?.onClick.RemoveAllListeners();
        }
    }

    [Window(
        UILayer.System,
        location: "jxqy/ui/prefabs/jxqyfadeui.prefab",
        packageName: "JxqyPackage")]
    public sealed class JxqyFadeUI : JxqySessionWindow
    {
        private Image _overlay;

        public float Opacity => Session?.FadeOpacity ?? 0f;

        protected override void ScriptGenerator()
        {
            _overlay = FindChildComponent<Image>("m_image_Overlay");
            // Script state and modal windows own input gating. The fade is a
            // visual transition only; leaving it as a raycast target can make
            // a stale or deliberately opaque fade block the UI that must
            // advance the script.
            if (_overlay != null)
                _overlay.raycastTarget = false;
        }

        protected override void RefreshView()
        {
            ApplyOpacity();
            Session?.NotifyFadeUiReady();
        }

        protected override void OnUpdate()
        {
            ApplyOpacity();
        }

        private void ApplyOpacity()
        {
            if (_overlay == null)
                return;
            Color color = _overlay.color;
            color.r = 0f;
            color.g = 0f;
            color.b = 0f;
            color.a = Opacity;
            _overlay.color = color;
        }
    }

    [Window(
        UILayer.Tips,
        location: "jxqy/ui/prefabs/jxqynoticeui.prefab",
        packageName: "JxqyPackage")]
    public sealed class JxqyNoticeUI : JxqySessionWindow
    {
        private Text _notice;
        private float _hideAt;

        protected override void ScriptGenerator()
        {
            _notice = FindChildComponent<Text>("m_text_Notice");
        }

        protected override void RefreshView()
        {
            if (string.IsNullOrWhiteSpace(Session?.Notice))
            {
                _hideAt = Time.unscaledTime;
                return;
            }
            if (_notice != null)
                _notice.text = Session.Notice;
            _hideAt = Time.unscaledTime + 2f;
        }

        protected override void OnUpdate()
        {
            if (string.IsNullOrWhiteSpace(Session?.Notice) ||
                Time.unscaledTime >= _hideAt)
                GameModule.UI.CloseUI<JxqyNoticeUI>();
        }
    }

    [Window(
        UILayer.Bottom,
        location: "jxqy/ui/prefabs/jxqytargetlifeui.prefab",
        packageName: "JxqyPackage")]
    public sealed class JxqyTargetLifeUI : JxqySessionWindow
    {
        private GameObject _group;
        private RectTransform _fill;
        private Text _text;

        protected override void ScriptGenerator()
        {
            _group = FindChild("m_group_TargetLife")?.gameObject;
            _fill = FindChildComponent<RectTransform>(
                "m_group_TargetLife/m_img_TargetLife");
            _text = FindChildComponent<Text>(
                "m_group_TargetLife/m_text_TargetLife");
            if (_group == null || _fill == null || _text == null)
                throw new InvalidOperationException(
                    "JxqyTargetLifeUI prefab hierarchy is incomplete.");
            _group.SetActive(false);
        }

        protected override void RefreshView()
        {
            if (_group == null)
                return;
            JxqyCharacter target = Session?.CombatTarget;
            bool visible = Session?.CurrentScreen != JxqyUiScreen.Title &&
                           target != null && !target.IsDead &&
                           target.IsVisible;
            _group.SetActive(visible);
            if (!visible)
                return;
            float percent = target.LifeMax <= 0
                ? 1f
                : Mathf.Clamp01(target.Life / (float)target.LifeMax);
            _fill.anchorMax = new Vector2(percent, 1f);
            _text.text = string.IsNullOrWhiteSpace(target.Name)
                ? $"{target.Life}/{target.LifeMax}"
                : $"{target.Name}  {target.Life}/{target.LifeMax}";
        }

        protected override void OnUpdate() => RefreshView();
    }

    [Window(
        UILayer.Bottom,
        location: "jxqy/ui/prefabs/jxqytimerui.prefab",
        packageName: "JxqyPackage")]
    public sealed class JxqyTimerUI : JxqySessionWindow
    {
        private GameObject _group;
        private Text _text;
        private JxqyUiAnimationBinding _background;

        protected override void ScriptGenerator()
        {
            _group = FindChild("m_group_Timer")?.gameObject;
            RawImage image = _group?.GetComponent<RawImage>();
            _text = FindChildComponent<Text>(
                "m_group_Timer/m_text_Timer");
            if (_group == null || image == null || _text == null)
                throw new InvalidOperationException(
                    "JxqyTimerUI prefab hierarchy is incomplete.");
            _background = new JxqyUiAnimationBinding(image);
            _background.Set(
                "timer", "window.asf", preserveNativeSize: false);
            _group.SetActive(false);
        }

        protected override void RefreshView()
        {
            if (_group == null)
                return;
            bool visible = Session?.TimerVisible == true;
            _group.SetActive(visible);
            if (!visible || _text == null)
                return;
            int totalSeconds = Math.Max(0, Session.TimerSeconds);
            _text.text = $"{totalSeconds / 60:00}分" +
                         $"{totalSeconds % 60:00}秒";
        }

        protected override void OnUpdate()
        {
            _background?.Tick(Time.unscaledDeltaTime);
            RefreshView();
        }

        protected override void OnDestroy()
        {
            _background?.Dispose();
            _background = null;
        }
    }

    [Window(
        UILayer.Tips,
        location: "jxqy/ui/prefabs/jxqymessageui.prefab",
        packageName: "JxqyPackage")]
    public sealed class JxqyMessageUI : JxqySessionWindow
    {
        private GameObject _group;
        private Text _text;
        private JxqyPointerClickRelay _clickRelay;
        private JxqyUiAnimationBinding _background;
        private int _sequence = -1;
        private float _hideAt;

        protected override void ScriptGenerator()
        {
            _group = FindChild("m_group_Message")?.gameObject;
            RawImage image = _group?.GetComponent<RawImage>();
            _clickRelay = _group?.GetComponent<JxqyPointerClickRelay>();
            _text = FindChildComponent<Text>(
                "m_group_Message/m_text_Message");
            if (_group == null || image == null || _text == null ||
                _clickRelay == null)
                throw new InvalidOperationException(
                    "JxqyMessageUI prefab hierarchy is incomplete.");
            _clickRelay.Clicked = CloseFromBackgroundClick;
            _background = new JxqyUiAnimationBinding(image);
            _background.Set(
                "message", "msgbox.asf", preserveNativeSize: false);
            _group.SetActive(false);
        }

        protected override void RefreshView()
        {
            if (Session == null || _group == null ||
                Session.MessageSequence == _sequence)
                return;
            _sequence = Session.MessageSequence;
            _text.text = Session.Message;
            _group.SetActive(!string.IsNullOrWhiteSpace(_text.text));
            _hideAt = Time.unscaledTime + 2f;
        }

        protected override void OnUpdate()
        {
            _background?.Tick(Time.unscaledDeltaTime);
            if (_group != null && _group.activeSelf &&
                Time.unscaledTime >= _hideAt)
                _group.SetActive(false);
        }

        protected override void OnDestroy()
        {
            if (_clickRelay != null)
                _clickRelay.Clicked = null;
            _clickRelay = null;
            _background?.Dispose();
            _background = null;
        }

        private void CloseFromBackgroundClick(PointerEventData eventData)
        {
            if (eventData != null &&
                eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }
            _group?.SetActive(false);
        }
    }

    [Window(
        UILayer.Tips,
        location: "jxqy/ui/prefabs/jxqysystemmessageui.prefab",
        packageName: "JxqyPackage")]
    public sealed class JxqySystemMessageUI : JxqySessionWindow
    {
        private sealed class Entry
        {
            public string Text;
            public float ExpiresAt;
        }

        private readonly List<Entry> _entries = new();
        private Text _text;
        private int _sequence = -1;

        protected override void ScriptGenerator()
        {
            _text = FindChildComponent<Text>("m_text_SystemMessages");
            if (_text == null)
                throw new InvalidOperationException(
                    "JxqySystemMessageUI prefab text is missing.");
        }

        protected override void RefreshView()
        {
            if (Session == null ||
                Session.SystemMessageSequence == _sequence)
                return;
            _sequence = Session.SystemMessageSequence;
            if (!string.IsNullOrWhiteSpace(Session.SystemMessage))
            {
                if (_entries.Count >= 15)
                    _entries.RemoveAt(0);
                _entries.Add(new Entry
                {
                    Text = Session.SystemMessage,
                    ExpiresAt = Time.unscaledTime +
                                Session.SystemMessageDurationMilliseconds /
                                1000f,
                });
            }
            RefreshText();
        }

        protected override void OnUpdate()
        {
            bool changed = false;
            for (int index = _entries.Count - 1; index >= 0; index--)
            {
                if (Time.unscaledTime < _entries[index].ExpiresAt)
                    continue;
                _entries.RemoveAt(index);
                changed = true;
            }
            if (changed)
                RefreshText();
        }

        private void RefreshText()
        {
            if (_text == null)
                return;
            var lines = new List<string>(_entries.Count);
            for (int index = 0; index < _entries.Count; index++)
                lines.Add(_entries[index].Text);
            _text.text = string.Join("\n", lines);
            _text.gameObject.SetActive(_entries.Count > 0);
        }
    }

    [Window(
        UILayer.Top,
        location: "jxqy/ui/prefabs/jxqytitleui.prefab",
        fullScreen: true,
        packageName: "JxqyPackage")]
    public sealed class JxqyTitleUI : JxqySessionWindow
    {
        private Button _newGame;
        private Button _loadGame;
        private Button _credits;
        private Button _exit;

        // The original plays this sound on pointer entry. For the localized
        // version it is intentionally click-only to avoid harsh repetition
        // while the pointer moves across the four title buttons.
        protected override JxqyUiSound? DefaultButtonSound =>
            JxqyUiSound.MainMenu;

        protected override void ScriptGenerator()
        {
            _newGame = FindChildComponent<Button>("m_btn_NewGame");
            _loadGame = FindChildComponent<Button>("m_btn_LoadGame");
            _credits = FindChildComponent<Button>("m_btn_Credits");
            _exit = FindChildComponent<Button>("m_btn_Exit");
            _newGame?.onClick.AddListener(() => ConfirmIndex(0));
            _loadGame?.onClick.AddListener(() => ConfirmIndex(1));
            _credits?.onClick.AddListener(() => ConfirmIndex(2));
            _exit?.onClick.AddListener(() => ConfirmIndex(3));
            Button[] buttons = { _newGame, _loadGame, _credits, _exit };
            for (int index = 0; index < buttons.Length; index++)
            {
                ConfigureTitleButton(buttons[index]);
            }
        }

        protected override void RefreshView()
        {
            // The original title has no persistent/default selection frame.
            // Frame 1 is shown only while the pointer is over a button.
        }

        protected override void OnDestroy()
        {
            ClearButton(_newGame);
            ClearButton(_loadGame);
            ClearButton(_credits);
            ClearButton(_exit);
        }

        private static void ConfigureTitleButton(Button button)
        {
            if (button == null)
                return;
            button.transition = Selectable.Transition.None;
            RawImage image = button.targetGraphic as RawImage ??
                             button.GetComponent<RawImage>();
            if (image == null)
                return;
            var relay =
                button.GetComponent<JxqyTitleButtonStateRelay>() ??
                button.gameObject.AddComponent<
                    JxqyTitleButtonStateRelay>();
            relay.Configure(image);
        }

        private void ConfirmIndex(int index)
        {
            Session?.Select(index);
            Session?.Confirm();
        }
    }

    [Window(
        UILayer.Bottom,
        location: "jxqy/ui/prefabs/jxqyhudui.prefab",
        packageName: "JxqyPackage")]
    public sealed class JxqyHudUI : JxqySessionWindow
    {
        private const int LegacyGoodsShortcutBegin = 221;
        private const int LegacyMagicShortcutBegin = 40;
        private readonly List<JxqyListSlotWidget> _shortcuts = new();
        private Button _status;
        private Button _equipment;
        private Button _training;
        private Button _inventory;
        private Button _skills;
        private Button _memo;
        private Button _menu;

        protected override JxqyUiSound? DefaultButtonSound =>
            JxqyUiSound.LargeButton;
        private RawImage _life;
        private RawImage _thew;
        private RawImage _mana;
        private JxqyUiAnimationBinding _lifeAnimation;
        private JxqyUiAnimationBinding _thewAnimation;
        private JxqyUiAnimationBinding _manaAnimation;
        private Text _lifeText;
        private Text _thewText;
        private Text _manaText;

        protected override void ScriptGenerator()
        {
            _status = FindChildComponent<Button>("m_btn_Status");
            _equipment = FindChildComponent<Button>("m_btn_Equipment");
            _training = FindChildComponent<Button>("m_btn_Training");
            _inventory = FindChildComponent<Button>("m_btn_Inventory");
            _skills = FindChildComponent<Button>("m_btn_Skills");
            _memo = FindChildComponent<Button>("m_btn_Memo");
            _menu = FindChildComponent<Button>("m_btn_Menu");
            _life = FindChildComponent<RawImage>("m_raw_Life");
            _thew = FindChildComponent<RawImage>("m_raw_Thew");
            _mana = FindChildComponent<RawImage>("m_raw_Mana");
            if (_life != null)
            {
                _lifeAnimation = new JxqyUiAnimationBinding(_life);
                _lifeAnimation.Set("column", "ColLife.asf");
            }
            if (_thew != null)
            {
                _thewAnimation = new JxqyUiAnimationBinding(_thew);
                _thewAnimation.Set("column", "ColThew.asf");
            }
            if (_mana != null)
            {
                _manaAnimation = new JxqyUiAnimationBinding(_mana);
                _manaAnimation.Set("column", "ColMana.asf");
            }
            _lifeText = FindChildComponent<Text>("m_text_Life");
            _thewText = FindChildComponent<Text>("m_text_Thew");
            _manaText = FindChildComponent<Text>("m_text_Mana");
            _status?.onClick.AddListener(
                () => Session?.Toggle(JxqyUiScreen.Status));
            _equipment?.onClick.AddListener(
                () => Session?.OpenPlayerEquipment());
            _training?.onClick.AddListener(
                () => Session?.Toggle(JxqyUiScreen.Training));
            _inventory?.onClick.AddListener(
                () => Session?.Toggle(JxqyUiScreen.Inventory));
            _skills?.onClick.AddListener(
                () => Session?.Toggle(JxqyUiScreen.Skills));
            _memo?.onClick.AddListener(
                ToggleMemo);
            _menu?.onClick.AddListener(
                () => Session?.Open(JxqyUiScreen.Menu));

            for (int index = 0; index < 8; index++)
            {
                JxqyListSlotWidget widget =
                    CreateWidget<JxqyListSlotWidget>(
                        $"m_item_Shortcut{index + 1}");
                if (widget != null)
                    _shortcuts.Add(widget);
            }
        }

        protected override void RefreshView()
        {
            RefreshMeters();

            for (int index = 0; index < _shortcuts.Count; index++)
            {
                bool itemSlot = index < 3;
                string name = string.Empty;
                string detail = string.Empty;
                string iconCategory = null;
                string iconFileName = null;
                float cooldownMilliseconds = 0f;
                if (itemSlot && Session.Inventory != null)
                {
                    JxqyInventoryEntry entry =
                        Session.Inventory.FindAtLegacyIndex(
                            LegacyGoodsShortcutBegin + index);
                    if (entry != null)
                    {
                        name = entry.Definition.Name;
                        detail = entry.Count.ToString();
                        iconCategory = "goods";
                        iconFileName = entry.Definition.IconFileName;
                        cooldownMilliseconds =
                            entry.CooldownMilliseconds;
                    }
                }
                else if (!itemSlot && Session.Skills != null)
                {
                    JxqySkillEntry entry =
                        Session.Skills.FindAtLegacyIndex(
                            LegacyMagicShortcutBegin + index - 3);
                    if (entry != null)
                    {
                        name = string.IsNullOrWhiteSpace(entry.Magic.Name)
                            ? entry.Magic.Id
                            : entry.Magic.Name;
                        detail = string.Empty;
                        iconCategory = "magic";
                        iconFileName = entry.Magic.IconFileName;
                        cooldownMilliseconds =
                            entry.CooldownMilliseconds;
                    }
                }
                _shortcuts[index].Bind(
                    index,
                    name,
                    detail,
                    !itemSlot &&
                    ReferenceEquals(
                        Session.SelectedSkill,
                        Session.Skills?.FindAtLegacyIndex(
                            LegacyMagicShortcutBegin + index - 3)),
                    true,
                    ShowShortcutDetail,
                    OnShortcut,
                    iconCategory: iconCategory,
                    iconFileName: iconFileName,
                    dragData: new JxqyListSlotWidget.DragData(
                        itemSlot
                            ? JxqyListSlotWidget.SlotKind.GoodsShortcut
                            : JxqyListSlotWidget.SlotKind.MagicShortcut,
                        itemSlot
                            ? LegacyGoodsShortcutBegin + index
                            : LegacyMagicShortcutBegin + index - 3),
                    dropped: OnShortcutDrop,
                    cooldownMilliseconds: cooldownMilliseconds,
                    soundRequested: RequestUiSound,
                    hovered: PreviewShortcut,
                    hoverExited: HideShortcutPreview);
            }
        }

        protected override void OnDestroy()
        {
            ClearButton(_status);
            ClearButton(_equipment);
            ClearButton(_training);
            ClearButton(_inventory);
            ClearButton(_skills);
            ClearButton(_memo);
            ClearButton(_menu);
            _lifeAnimation?.Dispose();
            _thewAnimation?.Dispose();
            _manaAnimation?.Dispose();
            _lifeAnimation = null;
            _thewAnimation = null;
            _manaAnimation = null;
            HideShortcutPreview();
        }

        protected override void OnUpdate()
        {
            float elapsedSeconds = Time.unscaledDeltaTime;
            _lifeAnimation?.Tick(elapsedSeconds);
            _thewAnimation?.Tick(elapsedSeconds);
            _manaAnimation?.Tick(elapsedSeconds);
            RefreshMeters();
        }

        private void RefreshMeters()
        {
            JxqyPlayer player = Session?.Player;
            if (player == null)
                return;
            SetMeter(_life, player.Life, player.LifeMax);
            SetMeter(_thew, player.Thew, player.ThewMax);
            SetMeter(_mana, player.Mana, player.ManaMax);
            if (_lifeText != null)
                _lifeText.text = $"{player.Life}/{player.LifeMax}";
            if (_thewText != null)
                _thewText.text = $"{player.Thew}/{player.ThewMax}";
            if (_manaText != null)
                _manaText.text = $"{player.Mana}/{player.ManaMax}";
        }


        private void OnShortcut(int index)
        {
            if (index < 3)
            {
                JxqyInventoryEntry entry =
                    Session?.Inventory?.FindAtLegacyIndex(
                        LegacyGoodsShortcutBegin + index);
                int inventoryIndex = FindInventoryIndex(entry);
                if (inventoryIndex >= 0)
                    Session.UseInventoryItem(inventoryIndex);
            }
            else
            {
                JxqySkillEntry entry =
                    Session?.Skills?.FindAtLegacyIndex(
                        LegacyMagicShortcutBegin + index - 3);
                int skillIndex = FindSkillIndex(entry);
                if (skillIndex >= 0)
                    Session.SelectSkill(skillIndex);
            }
        }

        private void PreviewShortcut(int index)
        {
            ShowShortcutDetail(index, true);
        }

        private void ShowShortcutDetail(int index)
        {
            ShowShortcutDetail(index, false);
        }

        private void ShowShortcutDetail(int index, bool isPreview)
        {
            if (index < 3)
            {
                JxqyInventoryEntry entry =
                    Session?.Inventory?.FindAtLegacyIndex(
                        LegacyGoodsShortcutBegin + index);
                if (entry != null)
                {
                    object data = isPreview
                        ? JxqyLegacyDetailRequest.Preview(
                            entry.Definition)
                        : entry.Definition;
                    GameModule.UI.ShowUIAsync<JxqyItemDetailUI>(
                        data);
                }
                return;
            }
            JxqySkillEntry skill = Session?.Skills?.FindAtLegacyIndex(
                LegacyMagicShortcutBegin + index - 3);
            if (skill != null)
            {
                object data = isPreview
                    ? JxqyLegacyDetailRequest.Preview(skill)
                    : skill;
                GameModule.UI.ShowUIAsync<JxqyMagicDetailUI>(
                    data);
            }
        }

        private static void HideShortcutPreview()
        {
            GameModule.UI.CloseUI<JxqyItemDetailUI>();
            GameModule.UI.CloseUI<JxqyMagicDetailUI>();
        }

        private void ToggleMemo()
        {
            if (Session == null)
                return;
            Session.Toggle(JxqyUiScreen.Memo);
        }

        private void OnShortcutDrop(
            JxqyListSlotWidget.DragData source,
            JxqyListSlotWidget.DragData target)
        {
            if (source == null || target == null || Session == null)
                return;
            if (target.Kind ==
                    JxqyListSlotWidget.SlotKind.GoodsShortcut &&
                IsInventorySlot(source.Kind))
            {
                int sourceIndex = FindInventoryIndex(
                    Session.Inventory?.FindAtLegacyIndex(source.Index));
                if (sourceIndex < 0)
                    return;
                Session.MoveInventoryEntryToLegacyIndex(
                    sourceIndex,
                    target.Index);
            }
            else if (target.Kind ==
                         JxqyListSlotWidget.SlotKind.MagicShortcut &&
                     IsSkillSlot(source.Kind))
            {
                int sourceIndex = FindSkillIndex(
                    Session.Skills?.FindAtLegacyIndex(source.Index));
                if (sourceIndex < 0)
                    return;
                Session.MoveSkillEntryToLegacyIndex(
                    sourceIndex,
                    target.Index);
            }
        }

        private static bool IsInventorySlot(
            JxqyListSlotWidget.SlotKind kind)
        {
            return kind == JxqyListSlotWidget.SlotKind.Inventory ||
                   kind == JxqyListSlotWidget.SlotKind.GoodsShortcut;
        }

        private static bool IsSkillSlot(
            JxqyListSlotWidget.SlotKind kind)
        {
            return kind == JxqyListSlotWidget.SlotKind.Skill ||
                   kind == JxqyListSlotWidget.SlotKind.MagicShortcut ||
                   kind == JxqyListSlotWidget.SlotKind.Cultivation;
        }

        private static void SetMeter(
            RawImage image,
            int value,
            int maximum)
        {
            if (image == null)
                return;
            float percent = maximum <= 0
                ? 0f
                : Mathf.Clamp01((float)value / maximum);
            if (image is JxqyFilledRawImage filled)
                filled.VerticalFill = percent;
        }

        private int FindInventoryIndex(JxqyInventoryEntry target)
        {
            if (target == null || Session?.Inventory == null)
                return -1;
            IReadOnlyList<JxqyInventoryEntry> entries =
                Session.Inventory.Entries;
            for (int index = 0; index < entries.Count; index++)
            {
                if (ReferenceEquals(entries[index], target))
                    return index;
            }
            return -1;
        }

        private int FindSkillIndex(JxqySkillEntry target)
        {
            if (target == null || Session?.Skills == null)
                return -1;
            IReadOnlyList<JxqySkillEntry> entries =
                Session.Skills.Skills;
            for (int index = 0; index < entries.Count; index++)
            {
                if (ReferenceEquals(entries[index], target))
                    return index;
            }
            return -1;
        }
    }

    [Window(
        UILayer.System,
        location: "jxqy/ui/prefabs/jxqydialogueui.prefab",
        packageName: "JxqyPackage")]
    public sealed class JxqyDialogueUI : JxqySessionWindow
    {
        private sealed class ChoiceButton
        {
            public GameObject GameObject;
            public RectTransform RectTransform;
            public Image Background;
            public Button Button;
            public Text Label;
            public JxqyChoiceButtonEventRelay HoverRelay;
        }

        private static readonly Color ChoiceNormalColor =
            new Color32(0, 0, 204, 255);
        private static readonly Color ChoiceHoverColor =
            new Color32(204, 0, 0, 255);
        private readonly List<ChoiceButton> _choices = new();
        private RawImage _portrait;
        protected override JxqyUiSound? DefaultButtonSound => null;
        private JxqyUiFrameBinding _portraitBinding;
        private Text _speaker;
        private Text _message;
        private Button _continue;
        private JxqyDialoguePage _messageSource;
        private string _messageSourceText = string.Empty;
        private IReadOnlyList<string> _messagePages =
            Array.Empty<string>();
        private int _messagePageIndex;

        protected override void ScriptGenerator()
        {
            _speaker = FindChildComponent<Text>("m_text_Speaker");
            _message = FindChildComponent<Text>("m_text_Message");
            _portrait =
                FindChildComponent<RawImage>("m_raw_Portrait");
            if (_portrait != null)
            {
                _portraitBinding =
                    new JxqyUiFrameBinding(_portrait);
            }
            _continue = FindChildComponent<Button>("m_btn_Continue");
            _continue?.onClick.AddListener(Continue);
            for (int index = 0; index < 2; index++)
            {
                Transform root = FindChild($"m_item_Choice{index}");
                if (root == null)
                    continue;
                Button button = root.GetComponent<Button>();
                Text label = FindChild(root, "m_text_Name")
                    ?.GetComponent<Text>();
                if (button == null || label == null)
                    continue;
                int choiceIndex = index;
                button.onClick.AddListener(
                    () => SelectChoice(choiceIndex));
                label.raycastTarget = false;
                Image background = root.GetComponent<Image>();
                button.targetGraphic = background;
                button.transition = Selectable.Transition.None;
                JxqyChoiceButtonEventRelay hoverRelay =
                    root.GetComponent<JxqyChoiceButtonEventRelay>() ??
                    root.gameObject.AddComponent<
                        JxqyChoiceButtonEventRelay>();
                hoverRelay.Configure(
                    label,
                    ChoiceNormalColor,
                    ChoiceHoverColor);
                _choices.Add(new ChoiceButton
                {
                    GameObject = root.gameObject,
                    RectTransform = root as RectTransform,
                    Background = background,
                    Button = button,
                    Label = label,
                    HoverRelay = hoverRelay,
                });
            }
        }

        protected override void RefreshView()
        {
            JxqyDialoguePage page = Session?.Dialogue?.Current;
            int count = page?.Choices.Count ?? 0;
            if (_speaker != null)
                _speaker.text = page?.Speaker ?? string.Empty;
            if (_message != null)
            {
                _message.supportRichText = true;
                string sourceText = page?.Text ?? string.Empty;
                if (!ReferenceEquals(_messageSource, page) ||
                    !string.Equals(
                        _messageSourceText,
                        sourceText,
                        StringComparison.Ordinal))
                {
                    _messageSource = page;
                    _messageSourceText = sourceText;
                    _messagePageIndex = 0;
                    _messagePages = count == 0
                        ? JxqyDialogueTextPaginator.Paginate(
                            _message,
                            sourceText)
                        : new[]
                        {
                            JxqyLegacyRichText.ToUnity(sourceText),
                        };
                }
                RenderMessagePage();
            }
            _portraitBinding?.Set(
                "portrait",
                page?.PortraitFileName);
            for (int index = 0; index < _choices.Count; index++)
            {
                bool visible = index < count;
                ChoiceButton choice = _choices[index];
                choice.GameObject.SetActive(visible);
                if (!visible)
                    continue;
                choice.Label.text = page.Choices[index].Text;
                choice.HoverRelay?.ResetVisual();
                choice.Button.interactable = true;
                if (choice.Background != null)
                    choice.Background.color = Color.clear;
            }
            if (_continue != null)
            {
                _continue.interactable = count == 0;
                _continue.gameObject.SetActive(count == 0);
            }
        }

        protected override void OnDestroy()
        {
            ClearButton(_continue);
            foreach (ChoiceButton choice in _choices)
                ClearButton(choice.Button);
            _choices.Clear();
            _portraitBinding?.Dispose();
            _portraitBinding = null;
        }

        private void Continue()
        {
            if (_messagePageIndex + 1 < _messagePages.Count)
            {
                _messagePageIndex++;
                RenderMessagePage();
                return;
            }
            Session?.Confirm();
        }

        private void RenderMessagePage()
        {
            if (_message == null)
                return;
            _message.text = _messagePageIndex >= 0 &&
                            _messagePageIndex < _messagePages.Count
                ? _messagePages[_messagePageIndex]
                : string.Empty;
        }

        private void SelectChoice(int index)
        {
            Session?.Select(index);
            Session?.Confirm();
        }
    }

    [Window(
        UILayer.System,
        location: "jxqy/ui/prefabs/jxqyselectionui.prefab",
        packageName: "JxqyPackage")]
    public sealed class JxqySelectionUI : JxqySessionWindow
    {
        private sealed class ChoiceView
        {
            public GameObject Root;
            public Button Button;
            public Text Label;
            public JxqyChoiceButtonEventRelay Hover;
        }

        private readonly List<ChoiceView> _choices = new();
        private RectTransform _panel;
        private RectTransform _choiceTemplateRect;
        private Text _message;
        private GameObject _choiceTemplate;
        private float _choiceAnchorLeft;
        private float _choiceAnchorRight;
        private float _multipleChoiceAnchorY;
        private float _choiceHeight;

        protected override void ScriptGenerator()
        {
            _panel = FindChildComponent<RectTransform>(
                "m_group_Selection");
            _message = FindChildComponent<Text>(
                "m_group_Selection/m_text_Message");
            _choiceTemplate = FindChild(
                "m_group_Selection/m_item_ChoiceTemplate")?.gameObject;
            _choiceTemplateRect =
                _choiceTemplate?.GetComponent<RectTransform>();
            if (_panel == null || _message == null ||
                _choiceTemplate == null || _choiceTemplateRect == null)
            {
                throw new InvalidOperationException(
                    "JxqySelectionUI prefab hierarchy is incomplete.");
            }
            _choiceAnchorLeft = _choiceTemplateRect.anchorMin.x;
            _choiceAnchorRight = _choiceTemplateRect.anchorMax.x;
            _multipleChoiceAnchorY = _choiceTemplateRect.anchorMin.y;
            _choiceHeight = _choiceTemplateRect.sizeDelta.y;
            _choiceTemplate.SetActive(false);
        }

        protected override void RefreshView()
        {
            JxqyDialogue dialogue = Session?.Dialogue;
            JxqyDialoguePage page = dialogue?.Current;
            if (_message != null)
                _message.text = JxqyLegacyRichText.ToUnity(
                    page?.Text ?? string.Empty);
            int count = page?.Choices.Count ?? 0;
            EnsureChoiceCount(count);
            int columns = Mathf.Max(1, page?.SelectionColumns ?? 1);
            bool multiple = (page?.SelectionCount ?? 1) > 1;
            int rows = Mathf.CeilToInt(count / (float)columns);
            float panelHeight = Mathf.Max(1f, _panel.rect.height);
            float rowSpacing = _choiceHeight / 3f;
            float rowStride = _choiceHeight + rowSpacing;
            float contentHeight = rows > 0
                ? rows * _choiceHeight + (rows - 1) * rowSpacing
                : 0f;
            float firstRowAnchorY = multiple
                ? _multipleChoiceAnchorY
                : 0.5f + contentHeight * 0.5f / panelHeight;
            float horizontalRange = Mathf.Max(
                0f,
                _choiceAnchorRight - _choiceAnchorLeft);
            float columnGap = columns > 1
                ? Mathf.Min(0.1f, horizontalRange / (columns * 4f))
                : 0f;
            float columnWidth = Mathf.Max(
                0f,
                (horizontalRange - columnGap * (columns - 1)) / columns);
            for (int index = 0; index < _choices.Count; index++)
            {
                ChoiceView view = _choices[index];
                bool visible = index < count;
                view.Root.SetActive(visible);
                if (!visible)
                    continue;
                RectTransform choiceRect =
                    view.Root.GetComponent<RectTransform>();
                int column = index % columns;
                int row = index / columns;
                float anchorLeft = _choiceAnchorLeft +
                                   column * (columnWidth + columnGap);
                float anchorY = firstRowAnchorY -
                                row * rowStride / panelHeight;
                choiceRect.anchorMin = new Vector2(anchorLeft, anchorY);
                choiceRect.anchorMax = new Vector2(
                    anchorLeft + columnWidth,
                    anchorY);
                choiceRect.pivot = new Vector2(0f, 1f);
                choiceRect.anchoredPosition = Vector2.zero;
                choiceRect.sizeDelta = new Vector2(0f, _choiceHeight);
                JxqyDialogueChoice choice = page.Choices[index];
                bool selected = IsSelected(
                    dialogue.SelectedChoiceValues,
                    choice.Value);
                view.Label.text = selected
                    ? "● " + choice.Text
                    : choice.Text;
                view.Label.color = selected
                    ? new Color(1f, 1f, 0f, 0.8f)
                    : new Color(0f, 1f, 0f, 0.8f);
                Image selectionBackground =
                    view.Root.GetComponent<Image>();
                selectionBackground.color = selected
                    ? new Color(1f, 1f, 0f, 0.2f)
                    : Color.clear;
                view.Hover.Configure(
                    view.Label,
                    view.Label.color,
                    new Color(1f, 1f, 0f, 0.8f));
            }
        }

        protected override void OnDestroy()
        {
            for (int index = 0; index < _choices.Count; index++)
                ClearButton(_choices[index].Button);
            _choices.Clear();
        }

        private void EnsureChoiceCount(int count)
        {
            while (_choices.Count < count)
            {
                int index = _choices.Count;
                GameObject root = UnityEngine.Object.Instantiate(
                    _choiceTemplate,
                    _panel,
                    false);
                root.name = $"m_item_Choice{index}";
                root.SetActive(true);
                RectTransform choiceRect =
                    root.GetComponent<RectTransform>();
                Image image = root.GetComponent<Image>();
                Button button = root.GetComponent<Button>();
                Text label = FindChild(root.transform, "m_text_Name")?
                    .GetComponent<Text>();
                var hover =
                    root.GetComponent<JxqyChoiceButtonEventRelay>();
                if (choiceRect == null || image == null || button == null ||
                    label == null || hover == null)
                {
                    throw new InvalidOperationException(
                        "Selection choice template is incomplete.");
                }
                int choiceIndex = index;
                button.onClick.AddListener(
                    () => SelectChoice(choiceIndex));
                _choices.Add(new ChoiceView
                {
                    Root = root,
                    Button = button,
                    Label = label,
                    Hover = hover,
                });
            }
        }

        private void SelectChoice(int index)
        {
            Session?.Select(index);
            Session?.Confirm();
        }

        private static bool IsSelected(
            IReadOnlyList<string> selectedValues,
            string value)
        {
            for (int index = 0; index < selectedValues.Count; index++)
            {
                if (string.Equals(
                        selectedValues[index],
                        value,
                        StringComparison.Ordinal))
                    return true;
            }
            return false;
        }
    }

    [Window(
        UILayer.UI,
        location: "jxqy/ui/prefabs/jxqystatusui.prefab",
        packageName: "JxqyPackage")]
    public sealed class JxqyStatusUI : JxqySessionWindow
    {
        private Text _level;
        private Text _experience;
        private Text _levelUp;
        private Text _life;
        private Text _thew;
        private Text _mana;
        private Text _attack;
        private Text _defend;
        private Text _evade;
        protected override void ScriptGenerator()
        {
            _level = FindChildComponent<Text>("m_text_Level");
            _experience = FindChildComponent<Text>("m_text_Experience");
            _levelUp = FindChildComponent<Text>("m_text_LevelUp");
            _life = FindChildComponent<Text>("m_text_Life");
            _thew = FindChildComponent<Text>("m_text_Thew");
            _mana = FindChildComponent<Text>("m_text_Mana");
            _attack = FindChildComponent<Text>("m_text_Attack");
            _defend = FindChildComponent<Text>("m_text_Defend");
            _evade = FindChildComponent<Text>("m_text_Evade");
        }

        protected override void RefreshView()
        {
            JxqyPlayer player = Session?.Player;
            if (player == null)
                return;
            Set(_level, player.Level.ToString());
            Set(_experience, player.Experience.ToString());
            Set(_levelUp, player.LevelUpExperience.ToString());
            Set(_life, $"{player.Life}/{player.LifeMax}");
            Set(_thew, $"{player.Thew}/{player.ThewMax}");
            Set(_mana, $"{player.Mana}/{player.ManaMax}");
            Set(_attack, player.Attack.ToString());
            Set(_defend, player.Defend.ToString());
            Set(_evade, player.Evade.ToString());
        }

        private static void Set(Text text, string value)
        {
            if (text != null)
                text.text = value;
        }
    }

    [Window(
        UILayer.UI,
        location: "jxqy/ui/prefabs/jxqymemoui.prefab",
        packageName: "JxqyPackage")]
    public sealed class JxqyMemoUI : JxqySessionWindow
    {
        private const int VisibleLineCount = 10;
        private const int LegacyLineLength = 10;
        private readonly List<string> _lines = new();
        private Text _text;
        private JxqyLegacyVerticalScrollBinding _scroll;
        private int _topLine;

        protected override void ScriptGenerator()
        {
            _text = FindChildComponent<Text>("m_text_Memo");
            RectTransform track =
                FindChildComponent<RectTransform>("m_img_ScrollTrack");
            RectTransform thumb =
                FindChildComponent<RectTransform>("m_raw_ScrollThumb");
            if (track != null && thumb != null)
            {
                _scroll = new JxqyLegacyVerticalScrollBinding(
                    track,
                    thumb,
                    rectTransform,
                    OnScrolled);
            }
        }

        protected override void RefreshView()
        {
            BuildLines();
            _scroll?.SetRange(Math.Max(0, _lines.Count - 1));
            _topLine = _scroll?.Value ?? 0;
            RefreshText();
        }

        protected override void OnDestroy()
        {
            _scroll?.Dispose();
            _scroll = null;
        }

        private void BuildLines()
        {
            _lines.Clear();
            IReadOnlyList<string> memos = Session?.Memos;
            if (memos == null)
                return;
            // The original MemoListManager uses AddFirst, so index zero is
            // always the newest memo. Runtime saves keep entries in event
            // order; enumerate them backwards to preserve that presentation
            // without invalidating existing acceptance saves.
            for (int memoIndex = memos.Count - 1;
                 memoIndex >= 0;
                 memoIndex--)
            {
                string memo = memos[memoIndex];
                string value = (memo ?? string.Empty).Trim();
                if (value.Length == 0)
                    continue;
                value = value[0] == '●' ? value : $"●{value}";
                string[] paragraphs = value
                    .Replace("\r\n", "\n")
                    .Replace('\r', '\n')
                    .Split('\n');
                foreach (string paragraph in paragraphs)
                {
                    if (paragraph.Length == 0)
                    {
                        _lines.Add(string.Empty);
                        continue;
                    }
                    for (int offset = 0;
                         offset < paragraph.Length;
                         offset += LegacyLineLength)
                    {
                        _lines.Add(paragraph.Substring(
                            offset,
                            Math.Min(
                                LegacyLineLength,
                                paragraph.Length - offset)));
                    }
                }
            }
        }

        private void OnScrolled(int value)
        {
            _topLine = value;
            RefreshText();
        }

        private void RefreshText()
        {
            if (_text == null)
                return;
            var visible = new List<string>(VisibleLineCount);
            for (int index = 0; index < VisibleLineCount; index++)
            {
                int lineIndex = _topLine + index;
                visible.Add(lineIndex >= 0 && lineIndex < _lines.Count
                    ? _lines[lineIndex]
                    : string.Empty);
            }
            _text.text = string.Join("\n", visible);
        }

        private void Close()
        {
            Session?.Cancel();
        }
    }

    [Window(
        UILayer.UI,
        location: "jxqy/ui/prefabs/jxqyinventoryui.prefab",
        packageName: "JxqyPackage")]
    public sealed class JxqyInventoryUI : JxqySessionWindow
    {
        private const int PageSize = 9;
        private const int Capacity = 198;
        private const int Columns = 3;
        private const int VisibleRows = PageSize / Columns;
        private readonly List<JxqyListSlotWidget> _slots = new();
        private Text _money;
        private Text _description;
        private Button _previous;
        private Button _next;
        private Button _use;
        private JxqyLegacyVerticalScrollBinding _inventoryScroll;
        private int _topRow;

        protected override void ScriptGenerator()
        {
            _money = FindChildComponent<Text>("m_text_Money");
            _description = FindChildComponent<Text>("m_text_Description");
            _previous = FindChildComponent<Button>("m_btn_PreviousPage");
            _next = FindChildComponent<Button>("m_btn_NextPage");
            _use = FindChildComponent<Button>("m_btn_Use");
            SetActive(_previous, false);
            SetActive(_next, false);
            SetActive(_use, false);
            BuildInventoryScrollBar();
            for (int index = 0; index < PageSize; index++)
            {
                JxqyListSlotWidget slot =
                    CreateWidget<JxqyListSlotWidget>(
                        $"m_item_Slot{index + 1}");
                if (slot != null)
                    _slots.Add(slot);
            }
        }

        protected override void RefreshView()
        {
            IReadOnlyList<JxqyInventoryEntry> entries =
                Session?.Inventory?.Entries;
            int count = GetInventoryStoreCount(entries);
            int selection = count == 0
                ? 0
                : Mathf.Clamp(Session.Selection, 0, count - 1);
            int maximumTopRow =
                Capacity / Columns - VisibleRows;
            _topRow = Mathf.Clamp(_topRow, 0, maximumTopRow);
            _inventoryScroll?.SetRange(maximumTopRow);
            _inventoryScroll?.SetValue(_topRow, false);
            int pageStart = _topRow * Columns;
            for (int index = 0; index < _slots.Count; index++)
            {
                int targetLegacyIndex = pageStart + index + 1;
                JxqyInventoryEntry entry =
                    Session?.Inventory?.FindAtLegacyIndex(
                        targetLegacyIndex);
                int dataIndex = FindInventoryIndex(entries, entry);
                bool occupied = dataIndex >= 0 &&
                                targetLegacyIndex <= Capacity;
                _slots[index].gameObject.SetActive(true);
                _slots[index].Bind(
                    dataIndex,
                    occupied ? entry.Definition.Name : string.Empty,
                    occupied ? entry.Count.ToString() : string.Empty,
                    occupied && dataIndex == selection,
                    occupied,
                    Select,
                    Activate,
                    iconCategory: "goods",
                    iconFileName: occupied
                        ? entry.Definition.ImageFileName
                        : null,
                    dragData: new JxqyListSlotWidget.DragData(
                        JxqyListSlotWidget.SlotKind.Inventory,
                        targetLegacyIndex),
                    dropped: OnInventoryDrop,
                    cooldownMilliseconds: occupied
                        ? entry.CooldownMilliseconds
                        : 0f,
                    soundRequested: RequestUiSound,
                    hovered: PreviewItem,
                    hoverExited: HideItemPreview);
            }
            if (_money != null)
                _money.text = (Session?.Player?.Money ?? 0).ToString();
            if (_description != null)
            {
                _description.text = count == 0
                    ? "（空）"
                    : entries[selection].Definition.Introduction;
            }
        }

        protected override void OnDestroy()
        {
            ClearButton(_previous);
            ClearButton(_next);
            ClearButton(_use);
            _inventoryScroll?.Dispose();
            _inventoryScroll = null;
            GameModule.UI.CloseUI<JxqyItemDetailUI>();
        }

        private void Select(int index)
        {
            IReadOnlyList<JxqyInventoryEntry> entries =
                Session?.Inventory?.Entries;
            if (entries == null || index < 0 || index >= entries.Count)
                return;
            Session.Select(index);
            GameModule.UI.ShowUIAsync<JxqyItemDetailUI>(
                entries[index].Definition);
        }

        private void PreviewItem(int index)
        {
            IReadOnlyList<JxqyInventoryEntry> entries =
                Session?.Inventory?.Entries;
            if (entries == null || index < 0 || index >= entries.Count)
                return;
            GameModule.UI.ShowUIAsync<JxqyItemDetailUI>(
                JxqyLegacyDetailRequest.Preview(
                    entries[index].Definition));
        }

        private static void HideItemPreview()
        {
            GameModule.UI.CloseUI<JxqyItemDetailUI>();
        }

        private void BuildInventoryScrollBar()
        {
            RectTransform thumb =
                FindChildComponent<RectTransform>(
                    "m_raw_ScrollThumb");
            if (thumb == null)
                return;
            RectTransform track =
                FindChildComponent<RectTransform>(
                    "m_img_ScrollTrack");
            if (track == null)
                throw new InvalidOperationException(
                    "JxqyInventoryUI prefab scroll track is missing.");
            _inventoryScroll =
                new JxqyLegacyVerticalScrollBinding(
                    track,
                    thumb,
                    rectTransform,
                    OnInventoryScrolled);
            _inventoryScroll.SetRange(
                Capacity / Columns - VisibleRows);
        }

        private void OnInventoryScrolled(int topRow)
        {
            _topRow = topRow;
            RefreshView();
        }

        private void Activate(int index)
        {
            IReadOnlyList<JxqyInventoryEntry> entries =
                Session?.Inventory?.Entries;
            if (entries == null || index < 0 || index >= entries.Count)
                return;
            if (entries[index].Definition.Kind ==
                JxqyItemKind.Equipment)
            {
                Session.EquipInventoryItem(index);
            }
            else
            {
                Session.UseInventoryItem(index);
            }
        }

        private void OnInventoryDrop(
            JxqyListSlotWidget.DragData source,
            JxqyListSlotWidget.DragData target)
        {
            if (source?.Kind ==
                    JxqyListSlotWidget.SlotKind.Equipment &&
                target?.Kind ==
                    JxqyListSlotWidget.SlotKind.Inventory &&
                JxqyEquipmentManager.TryGetSlotByLegacyListIndex(
                    source.Index,
                    out JxqyEquipmentSlot equipmentSlot))
            {
                Session?.ExchangeEquipmentWithInventory(
                    equipmentSlot,
                    target.Index);
                return;
            }
            if (source?.Kind ==
                    JxqyListSlotWidget.SlotKind.Inventory ||
                source?.Kind ==
                    JxqyListSlotWidget.SlotKind.GoodsShortcut)
            {
                if (target?.Kind !=
                    JxqyListSlotWidget.SlotKind.Inventory)
                {
                    return;
                }
                int sourceIndex = FindInventoryIndex(
                    Session?.Inventory?.Entries,
                    Session?.Inventory?.FindAtLegacyIndex(source.Index));
                if (sourceIndex < 0)
                    return;
                Session?.MoveInventoryEntryToLegacyIndex(
                    sourceIndex,
                    target.Index);
            }
        }

        private static void SetActive(
            Component component,
            bool active)
        {
            if (component != null)
                component.gameObject.SetActive(active);
        }

        private static int GetInventoryStoreCount(
            IReadOnlyList<JxqyInventoryEntry> entries)
        {
            if (entries == null)
                return 0;
            int count = 0;
            while (count < entries.Count &&
                   entries[count].LegacyListIndex <= 198)
            {
                count++;
            }
            return count;
        }

        private static int FindInventoryIndex(
            IReadOnlyList<JxqyInventoryEntry> entries,
            JxqyInventoryEntry target)
        {
            if (entries == null || target == null)
                return -1;
            for (int index = 0; index < entries.Count; index++)
            {
                if (ReferenceEquals(entries[index], target))
                    return index;
            }
            return -1;
        }
    }

    internal sealed class JxqyLegacyDetailRequest
    {
        private JxqyLegacyDetailRequest(object value, bool isPreview)
        {
            Value = value;
            IsPreview = isPreview;
        }

        public object Value { get; }
        public bool IsPreview { get; }

        public static JxqyLegacyDetailRequest Preview(object value)
        {
            return new JxqyLegacyDetailRequest(value, true);
        }
    }

    public abstract class JxqyLegacyDetailWindow : UIWindow
    {
        private JxqyLegacyTooltipBinding _detail;
        private Button _mask;
        private JxqyPointerClickRelay _maskClickRelay;
        private Graphic[] _graphics;
        private bool[] _defaultRaycastTargets;

        protected JxqyLegacyTooltipBinding Detail => _detail;
        protected object DetailData { get; private set; }

        protected override void ScriptGenerator()
        {
            _detail = new JxqyLegacyTooltipBinding(transform);
            _mask = FindChildComponent<Button>("m_btn_Mask");
            _mask?.onClick.AddListener(CloseDetail);
            if (_mask != null)
            {
                _maskClickRelay =
                    _mask.GetComponent<JxqyPointerClickRelay>() ??
                    _mask.gameObject.AddComponent<JxqyPointerClickRelay>();
                _maskClickRelay.Clicked = ForwardMaskRightClick;
            }
            _graphics = gameObject.GetComponentsInChildren<Graphic>(true);
            _defaultRaycastTargets = new bool[_graphics.Length];
            for (int index = 0; index < _graphics.Length; index++)
                _defaultRaycastTargets[index] =
                    _graphics[index].raycastTarget;
        }

        protected override void OnCreate()
        {
            RefreshRequest();
        }

        protected override void OnRefresh()
        {
            RefreshRequest();
        }

        protected abstract void RefreshDetail();

        protected override void OnDestroy()
        {
            ClearCloseButton();
            _detail?.Dispose();
            _detail = null;
            _graphics = null;
            _defaultRaycastTargets = null;
            DetailData = null;
        }

        private void CloseDetail()
        {
            GameModule.UI.CloseUI(GetType());
        }

        private void ForwardMaskRightClick(PointerEventData eventData)
        {
            if (eventData == null ||
                eventData.button != PointerEventData.InputButton.Right ||
                EventSystem.current == null ||
                _graphics == null)
            {
                return;
            }

            var currentRaycastTargets = new bool[_graphics.Length];
            var results = new List<RaycastResult>();
            try
            {
                for (int index = 0; index < _graphics.Length; index++)
                {
                    Graphic graphic = _graphics[index];
                    if (graphic == null)
                        continue;
                    currentRaycastTargets[index] = graphic.raycastTarget;
                    graphic.raycastTarget = false;
                }
                EventSystem.current.RaycastAll(eventData, results);
            }
            finally
            {
                for (int index = 0; index < _graphics.Length; index++)
                {
                    if (_graphics[index] != null)
                    {
                        _graphics[index].raycastTarget =
                            currentRaycastTargets[index];
                    }
                }
            }

            foreach (RaycastResult result in results)
            {
                JxqyListSlotEventRelay slot =
                    result.gameObject.GetComponentInParent<
                        JxqyListSlotEventRelay>();
                if (slot == null)
                    continue;
                slot.OnPointerClick(eventData);
                CloseDetail();
                return;
            }
        }

        private void RefreshRequest()
        {
            var request = UserData as JxqyLegacyDetailRequest;
            bool isPreview = request?.IsPreview == true;
            DetailData = request?.Value ?? UserData;
            RefreshDetail();
            SetInteractionEnabled(!isPreview);
        }

        private void SetInteractionEnabled(bool enabled)
        {
            if (_graphics != null && _defaultRaycastTargets != null)
            {
                int count = Math.Min(
                    _graphics.Length,
                    _defaultRaycastTargets.Length);
                for (int index = 0; index < count; index++)
                {
                    if (_graphics[index] != null)
                    {
                        _graphics[index].raycastTarget =
                            enabled && _defaultRaycastTargets[index];
                    }
                }
            }
            if (_mask != null)
                _mask.interactable = enabled;
        }

        private void ClearCloseButton()
        {
            _mask?.onClick.RemoveListener(CloseDetail);
            if (_maskClickRelay != null)
                _maskClickRelay.Clicked = null;
            _maskClickRelay = null;
            _mask = null;
        }

    }

    [Window(
        UILayer.Top,
        location: "jxqy/ui/prefabs/jxqyitemdetailui.prefab",
        packageName: "JxqyPackage")]
    public sealed class JxqyItemDetailUI : JxqyLegacyDetailWindow
    {
        protected override void RefreshDetail()
        {
            Detail?.ShowItem(DetailData as JxqyItemDefinition);
        }
    }

    [Window(
        UILayer.Top,
        location: "jxqy/ui/prefabs/jxqymagicdetailui.prefab",
        packageName: "JxqyPackage")]
    public sealed class JxqyMagicDetailUI : JxqyLegacyDetailWindow
    {
        protected override void RefreshDetail()
        {
            Detail?.ShowMagic(DetailData as JxqySkillEntry);
        }
    }

    [Window(
        UILayer.UI,
        location: "jxqy/ui/prefabs/jxqyequipmentui.prefab",
        packageName: "JxqyPackage")]
    public sealed class JxqyEquipmentUI : JxqySessionWindow
    {
        private static readonly JxqyEquipmentSlot[] EquipmentSlots =
        {
            // m_item_Equipped1..7 follow the original UI_Settings.ini coordinates,
            // not the enum declaration order.
            JxqyEquipmentSlot.Head,
            JxqyEquipmentSlot.Neck,
            JxqyEquipmentSlot.Wrist,
            JxqyEquipmentSlot.Body,
            JxqyEquipmentSlot.Hand,
            JxqyEquipmentSlot.Foot,
            JxqyEquipmentSlot.Back,
        };

        private readonly List<JxqyListSlotWidget> _equipped = new();
        private RawImage _equipmentPanel;
        private Texture _defaultEquipmentPanelTexture;
        private Rect _defaultEquipmentPanelUv;
        private JxqyUiAnimationBinding _partnerEquipmentPanelBinding;
        private string _partnerEquipmentPanelFile = string.Empty;

        protected override void ScriptGenerator()
        {
            RemoveEmbeddedInventoryPanel();
            _equipmentPanel =
                FindChildComponent<RawImage>("m_raw_EquipmentPanel");
            if (_equipmentPanel != null)
            {
                _defaultEquipmentPanelTexture = _equipmentPanel.texture;
                _defaultEquipmentPanelUv = _equipmentPanel.uvRect;
            }
            for (int index = 0; index < EquipmentSlots.Length; index++)
            {
                JxqyListSlotWidget slot =
                    CreateWidget<JxqyListSlotWidget>(
                        $"m_item_Equipped{index + 1}");
                if (slot != null)
                    _equipped.Add(slot);
            }
        }

        protected override void RefreshView()
        {
            RefreshPartnerEquipmentPanel();
            for (int index = 0; index < _equipped.Count; index++)
            {
                JxqyEquipmentSlot slot = EquipmentSlots[index];
                JxqyItemDefinition item = null;
                bool hasItem = Session?.ActiveEquipment != null &&
                    Session.ActiveEquipment.Equipped.TryGetValue(
                        slot,
                        out item);
                _equipped[index].Bind(
                    index,
                    hasItem ? item.Name : SlotName(slot),
                    hasItem ? "点击卸下" : "空",
                    false,
                    hasItem,
                    OpenEquippedDetail,
                    Unequip,
                    iconCategory: "goods",
                    iconFileName:
                        hasItem ? item.ImageFileName : null,
                    dragData: new JxqyListSlotWidget.DragData(
                        JxqyListSlotWidget.SlotKind.Equipment,
                        JxqyEquipmentManager.GetLegacyListIndex(slot)),
                    dropped: OnEquipmentDrop,
                    soundRequested: RequestUiSound,
                    hovered: PreviewEquippedDetail,
                    hoverExited: HideEquipmentPreview);
            }

        }

        protected override void OnDestroy()
        {
            _partnerEquipmentPanelBinding?.Dispose();
            _partnerEquipmentPanelBinding = null;
            GameModule.UI.CloseUI<JxqyItemDetailUI>();
        }

        protected override void OnUpdate()
        {
            _partnerEquipmentPanelBinding?.Tick(Time.unscaledDeltaTime);
        }

        private void RefreshPartnerEquipmentPanel()
        {
            if (_equipmentPanel == null)
                return;
            string file = Session?.PartnerEquipmentTarget?
                .EquipmentBackgroundFileName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(file) ||
                !file.EndsWith(".asf", StringComparison.OrdinalIgnoreCase))
            {
                if (_partnerEquipmentPanelBinding != null)
                {
                    _partnerEquipmentPanelBinding.Dispose();
                    _partnerEquipmentPanelBinding = null;
                }
                _partnerEquipmentPanelFile = string.Empty;
                _equipmentPanel.texture = _defaultEquipmentPanelTexture;
                _equipmentPanel.uvRect = _defaultEquipmentPanelUv;
                _equipmentPanel.color = Color.white;
                return;
            }
            if (string.Equals(
                    _partnerEquipmentPanelFile,
                    file,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            _partnerEquipmentPanelFile = file;
            string normalized = file.Replace('\\', '/');
            int slash = normalized.LastIndexOf('/');
            string category = "common";
            if (slash > 0)
            {
                int previousSlash = normalized.LastIndexOf('/', slash - 1);
                category = normalized.Substring(previousSlash + 1,
                    slash - previousSlash - 1);
            }
            _partnerEquipmentPanelBinding?.Dispose();
            _partnerEquipmentPanelBinding =
                new JxqyUiAnimationBinding(_equipmentPanel);
            _partnerEquipmentPanelBinding.Set(
                category,
                slash >= 0 ? normalized.Substring(slash + 1) : normalized,
                false);
        }

        private void Unequip(int index)
        {
            if (index >= 0 && index < EquipmentSlots.Length)
                Session?.Unequip(EquipmentSlots[index]);
        }

        private void OpenEquippedDetail(int index)
        {
            ShowEquippedDetail(index, false);
        }

        private void PreviewEquippedDetail(int index)
        {
            ShowEquippedDetail(index, true);
        }

        private void ShowEquippedDetail(int index, bool isPreview)
        {
            if (Session?.ActiveEquipment == null ||
                index < 0 ||
                index >= EquipmentSlots.Length ||
                !Session.ActiveEquipment.Equipped.TryGetValue(
                    EquipmentSlots[index],
                    out JxqyItemDefinition item))
            {
                return;
            }
            object data = isPreview
                ? JxqyLegacyDetailRequest.Preview(item)
                : item;
            GameModule.UI.ShowUIAsync<JxqyItemDetailUI>(data);
        }

        private static void HideEquipmentPreview()
        {
            GameModule.UI.CloseUI<JxqyItemDetailUI>();
        }

        private void OnEquipmentDrop(
            JxqyListSlotWidget.DragData source,
            JxqyListSlotWidget.DragData target)
        {
            if (source == null || target == null || Session == null)
                return;
            if (source.Kind ==
                    JxqyListSlotWidget.SlotKind.Inventory &&
                target.Kind ==
                    JxqyListSlotWidget.SlotKind.Equipment)
            {
                JxqyInventoryEntry entry =
                    Session.Inventory?.FindAtLegacyIndex(source.Index);
                if (entry == null ||
                    !JxqyEquipmentManager.TryGetSlotByLegacyListIndex(
                        target.Index,
                        out JxqyEquipmentSlot targetSlot) ||
                    entry.Definition.Slot !=
                    targetSlot)
                {
                    return;
                }
                Session.ExchangeEquipmentWithInventory(
                    targetSlot,
                    source.Index);
            }
            else if (source.Kind ==
                         JxqyListSlotWidget.SlotKind.Equipment &&
                     target.Kind ==
                         JxqyListSlotWidget.SlotKind.Inventory &&
                     JxqyEquipmentManager.TryGetSlotByLegacyListIndex(
                         source.Index,
                         out JxqyEquipmentSlot sourceSlot))
            {
                Session.ExchangeEquipmentWithInventory(
                    sourceSlot,
                    target.Index);
            }
            else if (source.Kind ==
                         JxqyListSlotWidget.SlotKind.Inventory &&
                     target.Kind ==
                         JxqyListSlotWidget.SlotKind.Inventory)
            {
                int sourceDataIndex = FindInventoryIndex(
                    Session.Inventory?.Entries,
                    Session.Inventory?.FindAtLegacyIndex(source.Index));
                if (sourceDataIndex < 0)
                    return;
                Session.MoveInventoryEntryToLegacyIndex(
                    sourceDataIndex,
                    target.Index);
            }
        }

        private static string SlotName(JxqyEquipmentSlot slot)
        {
            return slot switch
            {
                JxqyEquipmentSlot.Head => "头部",
                JxqyEquipmentSlot.Neck => "颈部",
                JxqyEquipmentSlot.Body => "衣甲",
                JxqyEquipmentSlot.Back => "背部",
                JxqyEquipmentSlot.Hand => "武器",
                JxqyEquipmentSlot.Wrist => "护腕",
                JxqyEquipmentSlot.Foot => "鞋靴",
                _ => "装备",
            };
        }

        private void RemoveEmbeddedInventoryPanel()
        {
            string[] names =
            {
                "m_raw_InventoryPanel",
                "m_raw_InventoryScrollThumb",
                "m_btn_Equip",
            };
            foreach (string name in names)
            {
                Transform child = FindChild(name);
                if (child != null)
                    UnityEngine.Object.Destroy(child.gameObject);
            }
            for (int index = 1; index <= 9; index++)
            {
                Transform child = FindChild($"m_item_Inventory{index}");
                if (child != null)
                    UnityEngine.Object.Destroy(child.gameObject);
            }
        }

        private static int GetInventoryStoreCount(
            IReadOnlyList<JxqyInventoryEntry> entries)
        {
            if (entries == null)
                return 0;
            int count = 0;
            while (count < entries.Count &&
                   entries[count].LegacyListIndex <= 198)
            {
                count++;
            }
            return count;
        }

        private static int FindInventoryIndex(
            IReadOnlyList<JxqyInventoryEntry> entries,
            JxqyInventoryEntry target)
        {
            if (entries == null || target == null)
                return -1;
            for (int index = 0; index < entries.Count; index++)
            {
                if (ReferenceEquals(entries[index], target))
                    return index;
            }
            return -1;
        }
    }

    [Window(
        UILayer.UI,
        location: "jxqy/ui/prefabs/jxqytrainingui.prefab",
        packageName: "JxqyPackage")]
    public sealed class JxqyTrainingUI : JxqySessionWindow
    {
        private const int CultivationLegacyListIndex = 49;
        private JxqyListSlotWidget _cultivation;
        private Text _level;
        private Text _experience;
        private Text _name;
        private Text _introduction;

        protected override void ScriptGenerator()
        {
            _cultivation =
                CreateWidget<JxqyListSlotWidget>(
                    "m_item_Cultivation");
            _level = FindChildComponent<Text>("m_text_Level");
            _experience =
                FindChildComponent<Text>("m_text_Experience");
            _name = FindChildComponent<Text>("m_text_MagicName");
            _introduction =
                FindChildComponent<Text>("m_text_Introduction");
        }

        protected override void RefreshView()
        {
            JxqySkillEntry entry =
                Session?.Skills?.FindAtLegacyIndex(
                    CultivationLegacyListIndex);
            int dataIndex = FindSkillIndex(entry);
            bool occupied = entry != null && dataIndex >= 0;
            _cultivation?.Bind(
                dataIndex,
                string.Empty,
                string.Empty,
                false,
                occupied,
                null,
                null,
                iconCategory: "magic",
                iconFileName: occupied
                    ? entry.Magic.ImageFileName
                    : null,
                dragData: new JxqyListSlotWidget.DragData(
                    JxqyListSlotWidget.SlotKind.Cultivation,
                    CultivationLegacyListIndex),
                dropped: OnCultivationDrop,
                soundRequested: RequestUiSound);

            if (!occupied)
            {
                Set(_level, "1/10");
                Set(_experience, "0/0");
                Set(_name, string.Empty);
                Set(_introduction, string.Empty);
                return;
            }

            int threshold =
                entry.Magic.GetLevelUpExperience(entry.Level);
            Set(
                _level,
                $"{entry.Level}/{entry.Magic.MaximumLevel}");
            Set(
                _experience,
                $"{entry.Experience}/{Math.Max(0, threshold)}");
            Set(
                _name,
                string.IsNullOrWhiteSpace(entry.Magic.Name)
                    ? entry.Magic.Id
                    : entry.Magic.Name);
            Set(_introduction, entry.Magic.Introduction);
        }

        private void OnCultivationDrop(
            JxqyListSlotWidget.DragData source,
            JxqyListSlotWidget.DragData target)
        {
            if (source == null ||
                target?.Kind !=
                    JxqyListSlotWidget.SlotKind.Cultivation ||
                !IsSkillSource(source.Kind))
            {
                return;
            }
            int sourceIndex = FindSkillIndex(
                Session?.Skills?.FindAtLegacyIndex(source.Index));
            if (sourceIndex < 0)
                return;
            Session?.MoveSkillEntryToLegacyIndex(
                sourceIndex,
                CultivationLegacyListIndex);
        }

        private int FindSkillIndex(JxqySkillEntry target)
        {
            IReadOnlyList<JxqySkillEntry> skills =
                Session?.Skills?.Skills;
            if (skills == null || target == null)
                return -1;
            for (int index = 0; index < skills.Count; index++)
            {
                if (ReferenceEquals(skills[index], target))
                    return index;
            }
            return -1;
        }

        private static bool IsSkillSource(
            JxqyListSlotWidget.SlotKind kind)
        {
            return kind == JxqyListSlotWidget.SlotKind.Skill ||
                   kind ==
                       JxqyListSlotWidget.SlotKind.MagicShortcut ||
                   kind ==
                       JxqyListSlotWidget.SlotKind.Cultivation;
        }

        private static void Set(Text text, string value)
        {
            if (text != null)
                text.text = value ?? string.Empty;
        }
    }

    [Window(
        UILayer.UI,
        location: "jxqy/ui/prefabs/jxqyskillsui.prefab",
        packageName: "JxqyPackage")]
    public sealed class JxqySkillsUI : JxqySessionWindow
    {
        private const int Capacity = 36;
        private const int Columns = 3;
        private const int VisibleSlotCount = 9;
        private const int VisibleRows = VisibleSlotCount / Columns;
        private readonly List<JxqyListSlotWidget> _slots = new();
        private Text _description;
        private Text _level;
        private Button _select;
        private JxqyLegacyVerticalScrollBinding _skillScroll;
        private int _topRow;
        private int _lastSelectedLegacyIndex = -1;

        protected override void ScriptGenerator()
        {
            BuildSkillScrollBar();
            _description = FindChildComponent<Text>("m_text_Description");
            _level = FindChildComponent<Text>("m_text_Level");
            _select = FindChildComponent<Button>("m_btn_Select");
            if (_select != null)
                _select.gameObject.SetActive(false);
            for (int index = 0; index < VisibleSlotCount; index++)
            {
                JxqyListSlotWidget slot =
                    CreateWidget<JxqyListSlotWidget>(
                        $"m_item_Slot{index + 1}");
                if (slot != null)
                    _slots.Add(slot);
            }
        }

        protected override void RefreshView()
        {
            IReadOnlyList<JxqySkillEntry> skills =
                Session?.Skills?.Skills;
            int count = GetSkillStoreCount(skills);
            int selection = count == 0
                ? 0
                : Mathf.Clamp(Session.Selection, 0, count - 1);
            int selectedLegacyIndex = count == 0
                ? 1
                : skills[selection].LegacyListIndex;
            int maximumTopRow = Capacity / Columns - VisibleRows;
            int selectedRow =
                (Mathf.Clamp(selectedLegacyIndex, 1, Capacity) - 1) /
                Columns;
            if (selectedLegacyIndex != _lastSelectedLegacyIndex)
            {
                if (selectedRow < _topRow)
                    _topRow = selectedRow;
                else if (selectedRow >= _topRow + VisibleRows)
                    _topRow = selectedRow - VisibleRows + 1;
                _lastSelectedLegacyIndex = selectedLegacyIndex;
            }
            _topRow = Mathf.Clamp(_topRow, 0, maximumTopRow);
            _skillScroll?.SetRange(maximumTopRow);
            _skillScroll?.SetValue(_topRow, false);

            for (int index = 0; index < _slots.Count; index++)
            {
                int targetLegacyIndex =
                    _topRow * Columns + index + 1;
                JxqySkillEntry entry =
                    Session?.Skills?.FindAtLegacyIndex(
                        targetLegacyIndex);
                int dataIndex = FindSkillIndex(skills, entry);
                bool occupied = dataIndex >= 0 &&
                                targetLegacyIndex <= Capacity;
                _slots[index].Bind(
                    dataIndex,
                    occupied
                        ? string.IsNullOrWhiteSpace(entry.Magic.Name)
                            ? entry.Magic.Id
                            : entry.Magic.Name
                        : string.Empty,
                    occupied ? $"Lv.{entry.Level}" : string.Empty,
                    occupied && dataIndex == selection,
                    occupied,
                    Select,
                    AssignFirstShortcut,
                    iconCategory: "magic",
                    iconFileName: occupied
                        ? entry.Magic.ImageFileName
                        : null,
                    dragData: new JxqyListSlotWidget.DragData(
                        JxqyListSlotWidget.SlotKind.Skill,
                        targetLegacyIndex),
                    dropped: OnSkillDrop,
                    soundRequested: RequestUiSound,
                    hovered: PreviewSkill,
                    hoverExited: HideSkillPreview);
            }
            if (_description != null)
            {
                _description.text = count == 0
                    ? "（尚未习得武功）"
                    : $"伤害 {skills[selection].Magic.Effect}  " +
                      $"内力 {skills[selection].Magic.ManaCost}  " +
                      $"体力 {skills[selection].Magic.ThewCost}  " +
                      $"范围 {skills[selection].Magic.Range:0}";
            }
            if (_level != null)
            {
                if (count == 0)
                {
                    _level.text = string.Empty;
                }
                else
                {
                    JxqySkillEntry selected = skills[selection];
                    int threshold =
                        selected.Magic.GetLevelUpExperience(
                            selected.Level);
                    string experience = threshold <= 0
                        ? "已满"
                        : $"{selected.Experience}/{threshold}";
                    _level.text =
                        $"等级 {selected.Level}  经验 {experience}";
                }
            }
            if (_select != null)
                _select.interactable = count > 0;
        }

        protected override void OnDestroy()
        {
            ClearButton(_select);
            _skillScroll?.Dispose();
            _skillScroll = null;
            GameModule.UI.CloseUI<JxqyMagicDetailUI>();
        }

        private void BuildSkillScrollBar()
        {
            RectTransform track =
                FindChildComponent<RectTransform>(
                    "m_img_ScrollTrack");
            RectTransform thumb =
                FindChildComponent<RectTransform>(
                    "m_raw_ScrollThumb");
            if (track == null || thumb == null)
                return;
            _skillScroll = new JxqyLegacyVerticalScrollBinding(
                track,
                thumb,
                rectTransform,
                OnSkillScrolled);
            _skillScroll.SetRange(Capacity / Columns - VisibleRows);
        }

        private void OnSkillScrolled(int topRow)
        {
            _topRow = topRow;
            RefreshView();
        }

        private void Select(int index)
        {
            IReadOnlyList<JxqySkillEntry> skills =
                Session?.Skills?.Skills;
            if (skills == null || index < 0 || index >= skills.Count)
                return;
            Session.Select(index);
            GameModule.UI.ShowUIAsync<JxqyMagicDetailUI>(
                skills[index]);
        }

        private void PreviewSkill(int index)
        {
            IReadOnlyList<JxqySkillEntry> skills =
                Session?.Skills?.Skills;
            if (skills == null || index < 0 || index >= skills.Count)
                return;
            GameModule.UI.ShowUIAsync<JxqyMagicDetailUI>(
                JxqyLegacyDetailRequest.Preview(skills[index]));
        }

        private static void HideSkillPreview()
        {
            GameModule.UI.CloseUI<JxqyMagicDetailUI>();
        }

        private void SelectCurrent()
        {
            if (Session?.SelectSkill(Session.Selection) == true)
                Session.Cancel();
        }

        private void AssignFirstShortcut(int index)
        {
            if (Session?.Skills == null)
                return;
            for (int shortcut = 40; shortcut <= 44; shortcut++)
            {
                if (Session.Skills.FindAtLegacyIndex(shortcut) != null)
                    continue;
                Session.MoveSkillEntryToLegacyIndex(index, shortcut);
                return;
            }
        }

        private void OnSkillDrop(
            JxqyListSlotWidget.DragData source,
            JxqyListSlotWidget.DragData target)
        {
            if (source?.Kind ==
                    JxqyListSlotWidget.SlotKind.Skill ||
                source?.Kind ==
                    JxqyListSlotWidget.SlotKind.MagicShortcut ||
                source?.Kind ==
                    JxqyListSlotWidget.SlotKind.Cultivation)
            {
                if (target?.Kind !=
                    JxqyListSlotWidget.SlotKind.Skill)
                {
                    return;
                }
                int sourceIndex = FindSkillIndex(
                    Session?.Skills?.Skills,
                    Session?.Skills?.FindAtLegacyIndex(source.Index));
                if (sourceIndex < 0)
                    return;
                Session?.MoveSkillEntryToLegacyIndex(
                    sourceIndex,
                    target.Index);
            }
        }

        private void Close()
        {
            Session?.Cancel();
        }

        private static int GetSkillStoreCount(
            IReadOnlyList<JxqySkillEntry> skills)
        {
            if (skills == null)
                return 0;
            int count = 0;
            while (count < skills.Count &&
                   skills[count].LegacyListIndex <= 36)
            {
                count++;
            }
            return count;
        }

        private static int GetHighestSkillLegacyIndex(
            IReadOnlyList<JxqySkillEntry> skills)
        {
            int maximumLegacyIndex = 0;
            if (skills != null)
            {
                for (int index = 0; index < skills.Count; index++)
                {
                    int legacyIndex = skills[index].LegacyListIndex;
                    if (legacyIndex > 0 && legacyIndex <= Capacity)
                    {
                        maximumLegacyIndex = Math.Max(
                            maximumLegacyIndex,
                            legacyIndex);
                    }
                }
            }
            return maximumLegacyIndex;
        }

        private static int FindSkillIndex(
            IReadOnlyList<JxqySkillEntry> skills,
            JxqySkillEntry target)
        {
            if (skills == null || target == null)
                return -1;
            for (int index = 0; index < skills.Count; index++)
            {
                if (ReferenceEquals(skills[index], target))
                    return index;
            }
            return -1;
        }
    }

    [Window(
        UILayer.Top,
        location: "jxqy/ui/prefabs/jxqytradeui.prefab",
        fullScreen: true,
        packageName: "JxqyPackage")]
    public sealed class JxqyTradeUI : JxqySessionWindow
    {
        private const int PageSize = 9;
        private const int Columns = 3;
        private const int VisibleRows = PageSize / Columns;
        private const int OriginalRowCount = 27;
        private readonly List<JxqyListSlotWidget> _shopSlots = new();
        private int _shopSelection;
        private int _topRow;
        private Button _buy;
        private Button _close;
        private JxqyLegacyVerticalScrollBinding _shopScroll;

        protected override void ScriptGenerator()
        {
            _buy = FindChildComponent<Button>("m_btn_Buy");
            _close = FindChildComponent<Button>("m_btn_Close");
            BindButtonSound(_close, JxqyUiSound.LargeButton);
            _buy?.onClick.AddListener(Buy);
            _close?.onClick.AddListener(Close);
            RemoveGoodsPanel();
            BuildShopScrollBar();
            for (int index = 0; index < PageSize; index++)
            {
                JxqyListSlotWidget shop =
                    CreateWidget<JxqyListSlotWidget>(
                        $"m_item_Shop{index + 1}");
                if (shop != null)
                    _shopSlots.Add(shop);
            }
        }

        protected override void RefreshView()
        {
            var stock = Session?.Shop == null
                ? new List<JxqyShopStock>()
                : new List<JxqyShopStock>(Session.Shop.Stock);
            _shopSelection = stock.Count == 0
                ? 0
                : Mathf.Clamp(_shopSelection, 0, stock.Count - 1);
            int maximumTopRow = OriginalRowCount - VisibleRows;
            _topRow = Mathf.Clamp(_topRow, 0, maximumTopRow);
            _shopScroll?.SetRange(maximumTopRow);
            _shopScroll?.SetValue(_topRow, false);
            int pageStart = _topRow * Columns;

            for (int index = 0; index < _shopSlots.Count; index++)
            {
                int dataIndex = pageStart + index;
                bool visible = dataIndex < stock.Count;
                _shopSlots[index].gameObject.SetActive(visible);
                if (visible)
                {
                    JxqyShopStock item = stock[dataIndex];
                    // The legacy buy panel only writes the finite stock count
                    // in the slot corner. Price remains in the item tooltip;
                    // combining them as "price / count" changes the original
                    // meaning and is especially ambiguous for unlimited stock.
                    string count = item.IsUnlimited
                        ? string.Empty
                        : item.Count.ToString();
                    _shopSlots[index].Bind(
                        dataIndex,
                        item.Item.Name,
                        count,
                        dataIndex == _shopSelection,
                        true,
                        SelectShop,
                        BuyIndex,
                        iconCategory: "goods",
                        iconFileName: item.Item.ImageFileName,
                        soundRequested: RequestUiSound,
                        hovered: PreviewShopItem,
                        hoverExited: HideTradeItemPreview);
                }
            }
            if (_buy != null)
                _buy.interactable = stock.Count > 0;
        }

        protected override void OnDestroy()
        {
            ClearButton(_buy);
            ClearButton(_close);
            _shopScroll?.Dispose();
            _shopScroll = null;
            GameModule.UI.CloseUI<JxqyItemDetailUI>();
        }

        private void BuildShopScrollBar()
        {
            RectTransform thumb = FindChildComponent<RectTransform>(
                "m_raw_ShopScrollThumb");
            if (thumb == null)
                return;
            RectTransform track = FindChildComponent<RectTransform>(
                "m_img_ShopScrollTrack");
            if (track == null)
                throw new InvalidOperationException(
                    "JxqyTradeUI prefab scroll track is missing.");
            _shopScroll = new JxqyLegacyVerticalScrollBinding(
                track,
                thumb,
                rectTransform,
                OnShopScrolled);
            _shopScroll.SetRange(OriginalRowCount - VisibleRows);
        }

        private void OnShopScrolled(int topRow)
        {
            _topRow = topRow;
            RefreshView();
        }

        private void SelectShop(int index)
        {
            _shopSelection = index;
            RefreshView();
            ShowShopItem(index, false);
        }

        private void PreviewShopItem(int index)
        {
            ShowShopItem(index, true);
        }

        private void ShowShopItem(int index, bool isPreview)
        {
            if (Session?.Shop == null)
                return;
            var stock = new List<JxqyShopStock>(Session.Shop.Stock);
            if (index < 0 || index >= stock.Count)
                return;
            object data = isPreview
                ? JxqyLegacyDetailRequest.Preview(stock[index].Item)
                : stock[index].Item;
            GameModule.UI.ShowUIAsync<JxqyItemDetailUI>(data);
        }

        private static void HideTradeItemPreview()
        {
            GameModule.UI.CloseUI<JxqyItemDetailUI>();
        }

        private void Buy()
        {
            Session?.BuyShopItem(_shopSelection);
        }

        private void BuyIndex(int index)
        {
            _shopSelection = index;
            Session?.BuyShopItem(index);
        }

        private void Close()
        {
            Session?.Cancel();
        }

        private void RemoveGoodsPanel()
        {
            string[] names =
            {
                "m_raw_InventoryPanel",
                "m_img_InventoryScrollTrack",
                "m_raw_InventoryScrollThumb",
                "m_btn_Sell",
                "m_text_Money",
            };
            foreach (string name in names)
            {
                Transform child = FindChild(name);
                if (child != null)
                    UnityEngine.Object.Destroy(child.gameObject);
            }
            for (int index = 1; index <= PageSize; index++)
            {
                Transform child = FindChild($"m_item_Inventory{index}");
                if (child != null)
                    UnityEngine.Object.Destroy(child.gameObject);
            }
        }
    }

    [Window(
        UILayer.Top,
        location: "jxqy/ui/prefabs/jxqytradegoodsui.prefab",
        packageName: "JxqyPackage")]
    public sealed class JxqyTradeGoodsUI : JxqySessionWindow
    {
        private const int PageSize = 9;
        private const int Capacity = 198;
        private const int Columns = 3;
        private const int VisibleRows = PageSize / Columns;
        private readonly List<JxqyListSlotWidget> _slots = new();
        private int _selection;
        private int _topRow;
        private Button _sell;
        private Text _money;
        private JxqyLegacyVerticalScrollBinding _inventoryScroll;

        protected override void ScriptGenerator()
        {
            _sell = FindChildComponent<Button>("m_btn_Sell");
            _money = FindChildComponent<Text>("m_text_Money");
            _sell?.onClick.AddListener(Sell);
            RemoveBuyPanel();
            BuildInventoryScrollBar();
            for (int index = 0; index < PageSize; index++)
            {
                JxqyListSlotWidget slot =
                    CreateWidget<JxqyListSlotWidget>(
                        $"m_item_Inventory{index + 1}");
                if (slot != null)
                    _slots.Add(slot);
            }
        }

        protected override void RefreshView()
        {
            IReadOnlyList<JxqyInventoryEntry> inventory =
                Session?.Inventory?.Entries;
            int storeCount = GetStoreCount(inventory);
            _selection = storeCount == 0
                ? 0
                : Mathf.Clamp(_selection, 0, (inventory?.Count ?? 1) - 1);
            if (storeCount > 0 &&
                !IsStoreEntry(inventory[_selection]))
            {
                _selection = FindFirstStoreIndex(inventory);
            }
            int maximumTopRow = Capacity / Columns - VisibleRows;
            _topRow = Mathf.Clamp(_topRow, 0, maximumTopRow);
            _inventoryScroll?.SetRange(maximumTopRow);
            _inventoryScroll?.SetValue(_topRow, false);
            int pageStart = _topRow * Columns;
            for (int index = 0; index < _slots.Count; index++)
            {
                int targetLegacyIndex = pageStart + index + 1;
                JxqyInventoryEntry entry =
                    Session?.Inventory?.FindAtLegacyIndex(targetLegacyIndex);
                int dataIndex = FindInventoryIndex(inventory, entry);
                bool occupied = dataIndex >= 0 &&
                                targetLegacyIndex <= Capacity;
                _slots[index].gameObject.SetActive(true);
                _slots[index].Bind(
                    dataIndex,
                    occupied ? entry.Definition.Name : string.Empty,
                    occupied ? entry.Count.ToString() : string.Empty,
                    occupied && dataIndex == _selection,
                    occupied && Session.Shop.CanSellPlayerGoods,
                    Select,
                    SellIndex,
                    iconCategory: "goods",
                    iconFileName: occupied
                        ? entry.Definition.ImageFileName
                        : null,
                    soundRequested: RequestUiSound,
                    hovered: Preview,
                    hoverExited: HidePreview);
            }
            if (_money != null)
                // GoodsGui in the original draws only the current numeric
                // money value in this fixed-width field.
                _money.text = (Session?.Player?.Money ?? 0).ToString();
            if (_sell != null)
            {
                _sell.interactable = storeCount > 0 &&
                                     Session.Shop.CanSellPlayerGoods;
            }
        }

        protected override void OnDestroy()
        {
            ClearButton(_sell);
            _inventoryScroll?.Dispose();
            _inventoryScroll = null;
            HidePreview();
        }

        private void BuildInventoryScrollBar()
        {
            RectTransform thumb = FindChildComponent<RectTransform>(
                "m_raw_InventoryScrollThumb");
            if (thumb == null)
                return;
            RectTransform track = FindChildComponent<RectTransform>(
                "m_img_InventoryScrollTrack");
            if (track == null)
                throw new InvalidOperationException(
                    "JxqyTradeGoodsUI prefab scroll track is missing.");
            _inventoryScroll = new JxqyLegacyVerticalScrollBinding(
                track,
                thumb,
                rectTransform,
                OnInventoryScrolled);
            _inventoryScroll.SetRange(
                Capacity / Columns - VisibleRows);
        }

        private void OnInventoryScrolled(int topRow)
        {
            _topRow = topRow;
            RefreshView();
        }

        private static int GetStoreCount(
            IReadOnlyList<JxqyInventoryEntry> entries)
        {
            int count = 0;
            if (entries == null)
                return count;
            foreach (JxqyInventoryEntry entry in entries)
            {
                if (IsStoreEntry(entry))
                    count++;
            }
            return count;
        }

        private static bool IsStoreEntry(JxqyInventoryEntry entry)
        {
            return entry != null && entry.LegacyListIndex >= 1 &&
                   entry.LegacyListIndex <= Capacity;
        }

        private static int FindFirstStoreIndex(
            IReadOnlyList<JxqyInventoryEntry> entries)
        {
            if (entries == null)
                return 0;
            for (int index = 0; index < entries.Count; index++)
            {
                if (IsStoreEntry(entries[index]))
                    return index;
            }
            return 0;
        }

        private static int FindInventoryIndex(
            IReadOnlyList<JxqyInventoryEntry> entries,
            JxqyInventoryEntry target)
        {
            if (entries == null || target == null)
                return -1;
            for (int index = 0; index < entries.Count; index++)
            {
                if (ReferenceEquals(entries[index], target))
                    return index;
            }
            return -1;
        }

        private void Select(int index)
        {
            _selection = index;
            RefreshView();
            ShowItem(index, false);
        }

        private void Preview(int index)
        {
            ShowItem(index, true);
        }

        private void ShowItem(int index, bool isPreview)
        {
            IReadOnlyList<JxqyInventoryEntry> entries =
                Session?.Inventory?.Entries;
            if (entries == null || index < 0 || index >= entries.Count)
                return;
            object data = isPreview
                ? JxqyLegacyDetailRequest.Preview(
                    entries[index].Definition)
                : entries[index].Definition;
            GameModule.UI.ShowUIAsync<JxqyItemDetailUI>(data);
        }

        private void Sell()
        {
            Session?.SellInventoryItem(_selection);
        }

        private void SellIndex(int index)
        {
            _selection = index;
            Session?.SellInventoryItem(index);
        }

        private static void HidePreview()
        {
            GameModule.UI.CloseUI<JxqyItemDetailUI>();
        }

        private void RemoveBuyPanel()
        {
            string[] names =
            {
                "m_raw_ShopPanel",
                "m_img_ShopScrollTrack",
                "m_raw_ShopScrollThumb",
                "m_btn_Buy",
            };
            foreach (string name in names)
            {
                Transform child = FindChild(name);
                if (child != null)
                    UnityEngine.Object.Destroy(child.gameObject);
            }
            for (int index = 1; index <= PageSize; index++)
            {
                Transform child = FindChild($"m_item_Shop{index}");
                if (child != null)
                    UnityEngine.Object.Destroy(child.gameObject);
            }
        }
    }

    [Window(
        UILayer.Top,
        location: "jxqy/ui/prefabs/jxqymenuui.prefab",
        packageName: "JxqyPackage")]
    public sealed class JxqyMenuUI : JxqySessionWindow
    {
        private Button _saveLoad;
        private Button _option;
        private Button _quit;
        private Button _return;
        private Text _message;

        protected override JxqyUiSound? DefaultButtonSound =>
            JxqyUiSound.LargeButton;

        protected override void ScriptGenerator()
        {
            _saveLoad = FindChildComponent<Button>("m_btn_SaveLoad");
            _option = FindChildComponent<Button>("m_btn_Option");
            _quit = FindChildComponent<Button>("m_btn_Quit");
            _return = FindChildComponent<Button>("m_btn_Return");
            _message = FindChildComponent<Text>("m_text_Message");
            _saveLoad?.onClick.AddListener(
                () => Session?.OpenSaveLoad(JxqySaveUiAction.Load));
            _option?.onClick.AddListener(
                () => Session?.ShowOptionsNotice());
            _quit?.onClick.AddListener(() => Session?.ReturnToTitle());
            _return?.onClick.AddListener(() => Session?.Cancel());
            ConfigureMenuButton(_saveLoad);
            ConfigureMenuButton(_option);
            ConfigureMenuButton(_quit);
            ConfigureMenuButton(_return);
        }

        protected override void RefreshView()
        {
            if (_message != null)
            {
                _message.text = Session?.Notice ?? string.Empty;
                _message.gameObject.SetActive(
                    !string.IsNullOrEmpty(_message.text));
            }
        }

        protected override void OnDestroy()
        {
            ClearButton(_saveLoad);
            ClearButton(_option);
            ClearButton(_quit);
            ClearButton(_return);
        }

        private static void ConfigureMenuButton(Button button)
        {
            if (button == null)
                return;
            button.transition = Selectable.Transition.None;
            RawImage image = button.targetGraphic as RawImage ??
                             button.GetComponent<RawImage>();
            if (image == null)
                return;
            var relay = button.GetComponent<JxqyMenuButtonStateRelay>() ??
                        button.gameObject.AddComponent<
                            JxqyMenuButtonStateRelay>();
            relay.Configure(image);
        }
    }

    [Window(
        UILayer.Top,
        location: "jxqy/ui/prefabs/jxqysaveloadui.prefab",
        fullScreen: true,
        packageName: "JxqyPackage")]
    public sealed class JxqySaveLoadUI : JxqySessionWindow
    {
        private readonly List<JxqyListSlotWidget> _slots = new();
        private RawImage _snapshot;
        private Texture2D _snapshotTexture;
        private Text _description;
        private Text _savedAt;
        private Text _message;
        private Button _load;
        private Button _save;
        private Button _exit;

        protected override JxqyUiSound? DefaultButtonSound =>
            JxqyUiSound.LargeButton;

        protected override void ScriptGenerator()
        {
            _snapshot =
                FindChildComponent<RawImage>("m_raw_Snapshot");
            _description = FindChildComponent<Text>("m_text_Description");
            _savedAt = FindChildComponent<Text>("m_text_SavedAt");
            _message = FindChildComponent<Text>("m_text_Message");
            _load = FindChildComponent<Button>("m_btn_Load");
            _save = FindChildComponent<Button>("m_btn_Save");
            _exit = FindChildComponent<Button>("m_btn_Exit");
            _load?.onClick.AddListener(Load);
            _save?.onClick.AddListener(Save);
            _exit?.onClick.AddListener(Close);
            for (int index = 0; index < 7; index++)
            {
                JxqyListSlotWidget slot =
                    CreateWidget<JxqyListSlotWidget>(
                        $"m_item_Slot{index + 1}");
                if (slot != null)
                    _slots.Add(slot);
            }
        }

        protected override void RefreshView()
        {
            IReadOnlyList<JxqySaveSlotView> slots = Session?.SaveSlots;
            int count = slots?.Count ?? 0;
            int selection = count == 0
                ? 0
                : Mathf.Clamp(Session.Selection, 0, count - 1);
            for (int index = 0; index < _slots.Count; index++)
            {
                bool visible = index < count;
                _slots[index].gameObject.SetActive(visible);
                if (!visible)
                    continue;
                _slots[index].Bind(
                    index,
                    $"进度{ToChinese(index + 1)}",
                    slots[index].Exists ? "有存档" : "空",
                    index == selection,
                    true,
                    Select,
                    soundRequested: RequestUiSound);
            }
            JxqySaveSlotView selected = count == 0
                ? null
                : slots[selection];
            RefreshSnapshot(selected?.SnapshotPng);
            if (_description != null)
                _description.text = selected?.Description ?? "空存档";
            if (_savedAt != null)
                _savedAt.text = selected?.SavedAt ?? string.Empty;
            if (_message != null)
            {
                _message.text = !string.IsNullOrWhiteSpace(
                    Session?.Notice)
                    ? Session.Notice
                    : Session?.SaveAction == JxqySaveUiAction.Save
                        ? "请选择进度并保存"
                        : "请选择已有进度读取";
            }
            if (_load != null)
                _load.interactable = selected?.Exists == true;
            if (_save != null)
                _save.interactable =
                    count > 0 && Session?.IsSaveAllowed == true;
        }

        protected override void OnDestroy()
        {
            ReleaseSnapshot();
            ClearButton(_load);
            ClearButton(_save);
            ClearButton(_exit);
        }

        private void Select(int index)
        {
            Session?.Select(index);
        }

        private void Load()
        {
            Session?.RequestLoad(Session.Selection);
        }

        private void Save()
        {
            Session?.RequestSave(Session.Selection);
        }

        private void Close()
        {
            Session?.Cancel();
        }

        private void RefreshSnapshot(byte[] pngBytes)
        {
            ReleaseSnapshot();
            if (_snapshot == null ||
                pngBytes == null ||
                pngBytes.Length == 0)
            {
                if (_snapshot != null)
                    _snapshot.texture = null;
                return;
            }

            var texture = new Texture2D(
                2,
                2,
                TextureFormat.RGB24,
                false)
            {
                name = "JxqySaveSnapshot",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            if (!ImageConversion.LoadImage(texture, pngBytes, true))
            {
                UnityEngine.Object.Destroy(texture);
                return;
            }
            _snapshotTexture = texture;
            _snapshot.texture = texture;
            _snapshot.color = Color.white;
        }

        private void ReleaseSnapshot()
        {
            if (_snapshot != null &&
                _snapshot.texture == _snapshotTexture)
                _snapshot.texture = null;
            if (_snapshotTexture != null)
                UnityEngine.Object.Destroy(_snapshotTexture);
            _snapshotTexture = null;
        }

        private static string ToChinese(int value)
        {
            string[] values =
            {
                "零", "一", "二", "三", "四", "五", "六", "七",
            };
            return value >= 0 && value < values.Length
                ? values[value]
                : value.ToString();
        }
    }

    public sealed class JxqyUiRouter : IDisposable
    {
        private readonly JxqyUiSession _session;
        private JxqyUiScreen? _shownModal;
        private JxqyUiScreen? _shownLeftPanel;
        private JxqyUiScreen? _shownRightPanel;
        private bool _fadeShown;
        private int _noticeSequence = -1;

        public JxqyUiRouter(JxqyUiSession session)
        {
            _session = session ??
                       throw new ArgumentNullException(nameof(session));
        }

        public void Start()
        {
            _session.Changed += Synchronize;
            GameModule.UI.ShowUIAsync<JxqyHudUI>(_session);
            GameModule.UI.ShowUIAsync<JxqyPartnerHeadsUI>(_session);
            GameModule.UI.ShowUIAsync<JxqyTargetLifeUI>(_session);
            GameModule.UI.ShowUIAsync<JxqyTimerUI>(_session);
            GameModule.UI.ShowUIAsync<JxqyMessageUI>(_session);
            GameModule.UI.ShowUIAsync<JxqySystemMessageUI>(_session);
            Synchronize();
        }

        public void Dispose()
        {
            _session.Changed -= Synchronize;
            CloseModal(_shownModal);
            CloseModal(_shownLeftPanel);
            CloseModal(_shownRightPanel);
            GameModule.UI.CloseUI<JxqyNoticeUI>();
            GameModule.UI.CloseUI<JxqyFadeUI>();
            GameModule.UI.CloseUI<JxqySystemMessageUI>();
            GameModule.UI.CloseUI<JxqyMessageUI>();
            GameModule.UI.CloseUI<JxqyTimerUI>();
            GameModule.UI.CloseUI<JxqyTargetLifeUI>();
            GameModule.UI.CloseUI<JxqyPartnerHeadsUI>();
            GameModule.UI.CloseUI<JxqyHudUI>();
        }

        private void Synchronize()
        {
            GameEvent.Get<IJxqyUI>().OnJxqyUiChanged();
            if (_session.FadeVisible && !_fadeShown)
            {
                _fadeShown = true;
                GameModule.UI.ShowUIAsync<JxqyFadeUI>(_session);
                // A parallel script can start a fade while dialogue is
                // already waiting for input. Re-push the interaction window
                // so System-layer insertion order cannot put the fade above
                // it. Sequential FadeOut -> Say is already ordered correctly.
                if (_shownModal == JxqyUiScreen.Dialogue)
                    GameModule.UI.ShowUIAsync<JxqyDialogueUI>(_session);
                else if (_shownModal == JxqyUiScreen.Selection)
                    GameModule.UI.ShowUIAsync<JxqySelectionUI>(_session);
            }
            else if (!_session.FadeVisible && _fadeShown)
            {
                _fadeShown = false;
                GameModule.UI.CloseUI<JxqyFadeUI>();
            }
            if (_session.NoticeSequence != _noticeSequence)
            {
                _noticeSequence = _session.NoticeSequence;
                if (string.IsNullOrWhiteSpace(_session.Notice))
                    GameModule.UI.CloseUI<JxqyNoticeUI>();
                else
                    GameModule.UI.ShowUIAsync<JxqyNoticeUI>(_session);
            }
            JxqyUiScreen? desiredModal =
                _session.ActiveModalScreen;
            bool modalVisible = desiredModal.HasValue;
            SynchronizeWindow(
                ref _shownLeftPanel,
                modalVisible ? null : _session.LeftPanelScreen);
            SynchronizeWindow(
                ref _shownRightPanel,
                modalVisible ? null : _session.RightPanelScreen);
            SynchronizeWindow(ref _shownModal, desiredModal);
        }

        private void SynchronizeWindow(
            ref JxqyUiScreen? shown,
            JxqyUiScreen? desired)
        {
            if (shown == desired)
                return;
            CloseModal(shown);
            shown = desired;
            if (desired.HasValue)
                ShowModal(desired.Value);
        }

        private void ShowModal(JxqyUiScreen screen)
        {
            switch (screen)
            {
                case JxqyUiScreen.Title:
                    GameModule.UI.ShowUIAsync<JxqyTitleUI>(_session);
                    break;
                case JxqyUiScreen.Dialogue:
                    GameModule.UI.ShowUIAsync<JxqyDialogueUI>(_session);
                    break;
                case JxqyUiScreen.Selection:
                    GameModule.UI.ShowUIAsync<JxqySelectionUI>(_session);
                    break;
                case JxqyUiScreen.Status:
                    GameModule.UI.ShowUIAsync<JxqyStatusUI>(_session);
                    break;
                case JxqyUiScreen.Inventory:
                    GameModule.UI.ShowUIAsync<JxqyInventoryUI>(_session);
                    break;
                case JxqyUiScreen.Equipment:
                    GameModule.UI.ShowUIAsync<JxqyEquipmentUI>(_session);
                    break;
                case JxqyUiScreen.Training:
                    GameModule.UI.ShowUIAsync<JxqyTrainingUI>(_session);
                    break;
                case JxqyUiScreen.Skills:
                    GameModule.UI.ShowUIAsync<JxqySkillsUI>(_session);
                    break;
                case JxqyUiScreen.Memo:
                    GameModule.UI.ShowUIAsync<JxqyMemoUI>(_session);
                    break;
                case JxqyUiScreen.Trade:
                    GameModule.UI.ShowUIAsync<JxqyTradeUI>(_session);
                    GameModule.UI.ShowUIAsync<JxqyTradeGoodsUI>(_session);
                    break;
                case JxqyUiScreen.Menu:
                    GameModule.UI.ShowUIAsync<JxqyMenuUI>(_session);
                    break;
                case JxqyUiScreen.SaveLoad:
                    GameModule.UI.ShowUIAsync<JxqySaveLoadUI>(_session);
                    break;
                case JxqyUiScreen.LittleMap:
                    GameModule.UI.ShowUIAsync<JxqyLittleMapUI>(_session);
                    break;
            }
        }

        private static void CloseModal(JxqyUiScreen? screen)
        {
            if (!screen.HasValue)
                return;
            switch (screen.Value)
            {
                case JxqyUiScreen.Title:
                    GameModule.UI.CloseUI<JxqyTitleUI>();
                    break;
                case JxqyUiScreen.Dialogue:
                    GameModule.UI.CloseUI<JxqyDialogueUI>();
                    break;
                case JxqyUiScreen.Selection:
                    GameModule.UI.CloseUI<JxqySelectionUI>();
                    break;
                case JxqyUiScreen.Status:
                    GameModule.UI.CloseUI<JxqyStatusUI>();
                    break;
                case JxqyUiScreen.Inventory:
                    GameModule.UI.CloseUI<JxqyInventoryUI>();
                    break;
                case JxqyUiScreen.Equipment:
                    GameModule.UI.CloseUI<JxqyEquipmentUI>();
                    break;
                case JxqyUiScreen.Training:
                    GameModule.UI.CloseUI<JxqyTrainingUI>();
                    break;
                case JxqyUiScreen.Skills:
                    GameModule.UI.CloseUI<JxqySkillsUI>();
                    break;
                case JxqyUiScreen.Memo:
                    GameModule.UI.CloseUI<JxqyMemoUI>();
                    break;
                case JxqyUiScreen.Trade:
                    GameModule.UI.CloseUI<JxqyTradeGoodsUI>();
                    GameModule.UI.CloseUI<JxqyTradeUI>();
                    break;
                case JxqyUiScreen.Menu:
                    GameModule.UI.CloseUI<JxqyMenuUI>();
                    break;
                case JxqyUiScreen.SaveLoad:
                    GameModule.UI.CloseUI<JxqySaveLoadUI>();
                    break;
                case JxqyUiScreen.LittleMap:
                    GameModule.UI.CloseUI<JxqyLittleMapUI>();
                    break;
            }
        }
    }
}
