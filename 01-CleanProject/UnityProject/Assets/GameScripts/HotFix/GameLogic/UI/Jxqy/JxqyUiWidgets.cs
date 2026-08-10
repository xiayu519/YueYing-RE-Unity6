using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Jxqy.Domain.Presentation;
using Jxqy.Domain.Simulation;
using Jxqy.UnityAdapters;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameLogic
{
    [DisallowMultipleComponent]
    public sealed class JxqyPointerClickRelay : MonoBehaviour,
        IPointerClickHandler
    {
        public Action<PointerEventData> Clicked { private get; set; }

        public void OnPointerClick(PointerEventData eventData)
        {
            Clicked?.Invoke(eventData);
        }
    }

    /// <summary>
    /// Reproduces the original game's discrete row scrollbar. The original
    /// thumb keeps its native ASF size and moves over a fixed-height track;
    /// list entries are rebound in place instead of moving a ScrollRect.
    /// </summary>
    public sealed class JxqyLegacyVerticalScrollBinding : IDisposable
    {
        private readonly RectTransform _track;
        private readonly RectTransform _thumb;
        private readonly Vector2 _thumbOrigin;
        private readonly Action<int> _changed;
        private readonly JxqyLegacyScrollEventRelay _trackRelay;
        private readonly JxqyLegacyScrollEventRelay _thumbRelay;
        private readonly JxqyLegacyScrollEventRelay _wheelRelay;
        private float _value;
        private int _maximum;
        private int _publishedValue;
        private bool _dragging;
        private float _dragStartValue;
        private float _dragStartScreenY;
        private bool _disposed;

        public JxqyLegacyVerticalScrollBinding(
            RectTransform track,
            RectTransform thumb,
            RectTransform wheelRoot,
            Action<int> changed)
        {
            _track = track ?? throw new ArgumentNullException(nameof(track));
            _thumb = thumb ?? throw new ArgumentNullException(nameof(thumb));
            _thumbOrigin = thumb.anchoredPosition;
            _changed = changed;
            _publishedValue = 0;

            _trackRelay =
                track.GetComponent<JxqyLegacyScrollEventRelay>() ??
                track.gameObject.AddComponent<JxqyLegacyScrollEventRelay>();
            _trackRelay.Target = this;
            _trackRelay.IsThumb = false;
            _trackRelay.ScrollOnly = false;

            _thumbRelay =
                thumb.GetComponent<JxqyLegacyScrollEventRelay>() ??
                thumb.gameObject.AddComponent<JxqyLegacyScrollEventRelay>();
            _thumbRelay.Target = this;
            _thumbRelay.IsThumb = true;
            _thumbRelay.ScrollOnly = false;

            if (wheelRoot != null &&
                wheelRoot != track &&
                wheelRoot != thumb)
            {
                _wheelRelay =
                    wheelRoot.GetComponent<JxqyLegacyScrollEventRelay>() ??
                    wheelRoot.gameObject
                        .AddComponent<JxqyLegacyScrollEventRelay>();
                _wheelRelay.Target = this;
                _wheelRelay.IsThumb = false;
                _wheelRelay.ScrollOnly = true;
            }
            UpdateThumb();
        }

        public int Value => Mathf.Clamp(
            Mathf.FloorToInt(_value),
            0,
            _maximum);

        public void SetRange(int maximum)
        {
            ThrowIfDisposed();
            _maximum = Mathf.Max(0, maximum);
            SetValue(_value, false);
        }

        public void SetValue(int value, bool notify = true)
        {
            SetValue((float)value, notify);
        }

        public void OnPointerDown(
            PointerEventData eventData,
            bool isThumb)
        {
            if (_disposed || eventData == null)
                return;
            if (isThumb)
            {
                CaptureDrag(eventData);
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _track,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint))
            {
                return;
            }
            Vector3 thumbWorldCenter = _thumb.TransformPoint(
                _thumb.rect.center);
            Vector2 thumbLocalCenter =
                _track.InverseTransformPoint(thumbWorldCenter);
            SetValue(Value + (localPoint.y < thumbLocalCenter.y ? 1 : -1));
        }

        public void OnBeginDrag(
            PointerEventData eventData,
            bool isThumb)
        {
            if (!_disposed && isThumb && eventData != null)
                CaptureDrag(eventData);
        }

        public void OnDrag(
            PointerEventData eventData,
            bool isThumb)
        {
            if (_disposed ||
                !isThumb ||
                !_dragging ||
                eventData == null)
            {
                return;
            }
            float step = StepLength;
            if (step <= 0f)
                return;
            float downwardPixels =
                _dragStartScreenY - eventData.position.y;
            SetValue(_dragStartValue + downwardPixels / step);
        }

        public void OnEndDrag(
            PointerEventData eventData,
            bool isThumb)
        {
            if (isThumb)
                _dragging = false;
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (_disposed ||
                eventData == null ||
                Mathf.Approximately(eventData.scrollDelta.y, 0f))
            {
                return;
            }
            SetValue(Value +
                     (eventData.scrollDelta.y > 0f ? -1 : 1));
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _dragging = false;
            if (_trackRelay != null)
                _trackRelay.Target = null;
            if (_thumbRelay != null)
                _thumbRelay.Target = null;
            if (_wheelRelay != null)
                _wheelRelay.Target = null;
        }

        private float StepLength =>
            _maximum <= 0
                ? 0f
                : Mathf.Max(
                    0f,
                    _track.rect.height - _thumb.rect.height) /
                  _maximum;

        private void CaptureDrag(PointerEventData eventData)
        {
            _dragging = true;
            _dragStartValue = _value;
            _dragStartScreenY = eventData.position.y;
        }

        private void SetValue(float value, bool notify = true)
        {
            ThrowIfDisposed();
            _value = Mathf.Clamp(value, 0f, _maximum);
            UpdateThumb();
            int publishedValue = Value;
            if (!notify)
            {
                _publishedValue = publishedValue;
                return;
            }
            if (publishedValue == _publishedValue)
                return;
            _publishedValue = publishedValue;
            _changed?.Invoke(publishedValue);
        }

        private void UpdateThumb()
        {
            if (_thumb == null)
                return;
            _thumb.anchoredPosition = _thumbOrigin +
                                      Vector2.down *
                                      (_value * StepLength);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(
                    nameof(JxqyLegacyVerticalScrollBinding));
        }
    }

    /// <summary>
    /// Shared binding for the original 336x400 goods/magic detail scroll.
    /// Both lists use the same legacy tipbox layout and frame-loading path.
    /// </summary>
    public sealed class JxqyLegacyTooltipBinding : IDisposable
    {
        private readonly GameObject _group;
        private readonly JxqyUiFrameBinding _iconBinding;
        private readonly Text _name;
        private readonly Text _meta;
        private readonly Text _effect;
        private readonly Text _introduction;

        public JxqyLegacyTooltipBinding(Transform windowRoot)
        {
            Transform group = FindDescendant(
                windowRoot,
                "m_group_Tooltip");
            _group = group?.gameObject;
            RawImage icon = FindDescendant(group, "m_raw_TooltipIcon")
                ?.GetComponent<RawImage>();
            if (icon != null)
                _iconBinding = new JxqyUiFrameBinding(icon);
            _name = FindDescendant(group, "m_text_TooltipName")
                ?.GetComponent<Text>();
            _meta = FindDescendant(group, "m_text_TooltipMeta")
                ?.GetComponent<Text>();
            _effect = FindDescendant(group, "m_text_TooltipEffect")
                ?.GetComponent<Text>();
            _introduction = FindDescendant(group, "m_text_TooltipIntro")
                ?.GetComponent<Text>();
            Hide();
        }

        private static Transform FindDescendant(
            Transform root,
            string name)
        {
            if (root == null || string.IsNullOrEmpty(name))
                return null;
            for (int index = 0; index < root.childCount; index++)
            {
                Transform child = root.GetChild(index);
                if (child.name == name)
                    return child;
                Transform nested = FindDescendant(child, name);
                if (nested != null)
                    return nested;
            }
            return null;
        }

        public void ShowItem(JxqyItemDefinition item)
        {
            if (_group == null || item == null)
            {
                Hide();
                return;
            }
            _group.SetActive(true);
            _iconBinding?.Set("goods", item.ImageFileName);
            Set(_name, item.Name);
            Set(_meta, $"价格： {item.CostRaw}");
            Set(_effect, BuildItemEffect(item));
            Set(_introduction, item.Introduction);
        }

        public void ShowMagic(JxqySkillEntry entry)
        {
            if (_group == null || entry == null)
            {
                Hide();
                return;
            }
            _group.SetActive(true);
            _iconBinding?.Set("magic", entry.Magic.ImageFileName);
            Set(
                _name,
                string.IsNullOrWhiteSpace(entry.Magic.Name)
                    ? entry.Magic.Id
                    : entry.Magic.Name);
            int threshold =
                entry.Magic.GetLevelUpExperience(entry.Level);
            Set(
                _meta,
                threshold <= 0
                    ? $"等级： {entry.Level}  经验：已满"
                    : $"等级： {entry.Level}  " +
                      $"经验：{entry.Experience}/{threshold}");
            Set(
                _effect,
                $"伤害：{entry.Magic.Effect}  " +
                $"内力：{entry.Magic.ManaCost}  " +
                $"体力：{entry.Magic.ThewCost}");
            Set(_introduction, entry.Magic.Introduction);
        }

        public void Hide()
        {
            if (_group != null)
                _group.SetActive(false);
        }

        public void Dispose()
        {
            _iconBinding?.Dispose();
        }

        private static string BuildItemEffect(JxqyItemDefinition item)
        {
            var text = new StringBuilder();
            Append(text, "命", item.Life);
            Append(text, "体", item.Thew);
            Append(text, "气", item.Mana);
            Append(text, "命", item.Modifiers.LifeMax);
            Append(text, "体", item.Modifiers.ThewMax);
            Append(text, "气", item.Modifiers.ManaMax);
            Append(text, "攻", item.Modifiers.Attack);
            Append(text, "防", item.Modifiers.Defend);
            Append(text, "闪", item.Modifiers.Evade);
            return text.ToString();
        }

        private static void Append(
            StringBuilder text,
            string label,
            int value)
        {
            if (value == 0)
                return;
            if (text.Length > 0)
                text.Append("  ");
            text.Append(label);
            text.Append(value > 0 ? " +" : " ");
            text.Append(value);
        }

        private static void Set(Text text, string value)
        {
            if (text != null)
                text.text = value ?? string.Empty;
        }
    }

    /// <summary>
    /// Converts the original stateful TextGui markup to Unity UI Text rich
    /// text. Legacy color tags change the active color instead of closing it.
    /// </summary>
    public static class JxqyLegacyRichText
    {
        public static string ToUnity(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var result = new StringBuilder(value.Length + 16);
            bool colorOpen = false;
            int cursor = 0;
            while (cursor < value.Length)
            {
                int tagStart = value.IndexOf('<', cursor);
                if (tagStart < 0)
                {
                    result.Append(value, cursor, value.Length - cursor);
                    break;
                }
                result.Append(value, cursor, tagStart - cursor);
                int tagEnd = value.IndexOf('>', tagStart + 1);
                if (tagEnd < 0)
                {
                    result.Append(value, tagStart, value.Length - tagStart);
                    break;
                }

                string tag = value.Substring(
                    tagStart + 1,
                    tagEnd - tagStart - 1);
                if (tag.Equals("enter", StringComparison.OrdinalIgnoreCase))
                {
                    result.Append('\n');
                }
                else if (tag.StartsWith(
                             "color=",
                             StringComparison.OrdinalIgnoreCase))
                {
                    string colorValue = tag.Substring("color=".Length).Trim();
                    if (colorOpen)
                    {
                        result.Append("</color>");
                        colorOpen = false;
                    }
                    if (TryResolveColor(colorValue, out Color32 color))
                    {
                        result.Append("<color=#");
                        result.Append(color.r.ToString("X2"));
                        result.Append(color.g.ToString("X2"));
                        result.Append(color.b.ToString("X2"));
                        result.Append(color.a.ToString("X2"));
                        result.Append('>');
                        colorOpen = true;
                    }
                }
                else if (!IsLegacyAlignmentTag(tag))
                {
                    result.Append(value, tagStart, tagEnd - tagStart + 1);
                }
                cursor = tagEnd + 1;
            }
            if (colorOpen)
                result.Append("</color>");
            return result.ToString();
        }

        private static bool TryResolveColor(
            string value,
            out Color32 color)
        {
            if (value.Equals("Default", StringComparison.OrdinalIgnoreCase) ||
                value.Equals(
                    "BeginRangeDefault",
                    StringComparison.OrdinalIgnoreCase) ||
                value.Equals(
                    "EndRangeDefault",
                    StringComparison.OrdinalIgnoreCase))
            {
                color = default;
                return false;
            }
            if (value.Equals("Black", StringComparison.OrdinalIgnoreCase))
            {
                color = new Color32(0, 0, 0, 255);
                return true;
            }
            if (value.Equals("Red", StringComparison.OrdinalIgnoreCase))
            {
                color = new Color32(255, 0, 0, 255);
                return true;
            }
            if (value.Equals("Green", StringComparison.OrdinalIgnoreCase))
            {
                color = new Color32(0, 128, 0, 255);
                return true;
            }
            if (value.Equals("Blue", StringComparison.OrdinalIgnoreCase))
            {
                color = new Color32(0, 0, 255, 255);
                return true;
            }
            if (value.Equals("White", StringComparison.OrdinalIgnoreCase))
            {
                color = new Color32(255, 255, 255, 255);
                return true;
            }

            string[] components = value.Split(',');
            if (components.Length != 3 && components.Length != 4)
            {
                color = default;
                return false;
            }
            byte[] channels = { 0, 0, 0, 255 };
            for (int index = 0; index < components.Length; index++)
            {
                if (!byte.TryParse(
                        components[index].Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out channels[index]))
                {
                    color = default;
                    return false;
                }
            }
            color = new Color32(
                channels[0],
                channels[1],
                channels[2],
                channels[3]);
            return true;
        }

        private static bool IsLegacyAlignmentTag(string tag)
        {
            return tag.Equals(
                       "AlignLeft",
                       StringComparison.OrdinalIgnoreCase) ||
                   tag.Equals(
                       "AlignCenter",
                       StringComparison.OrdinalIgnoreCase) ||
                   tag.Equals(
                       "AlignRight",
                       StringComparison.OrdinalIgnoreCase) ||
                   tag.Equals(
                       "EndAlign",
                       StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Splits one original dialogue entry into visual pages that fit the
    /// original 310x70 text area. The original TextGui performs this paging
    /// before advancing to the next Talk entry; Unity UI Text only truncates.
    /// </summary>
    public static class JxqyDialogueTextPaginator
    {
        // Original FontSize12 has a line spacing of 21 pixels. The original
        // 70-pixel Dialog_Txt area therefore admits exactly three lines.
        private const int OriginalMaximumLinesPerPage = 3;

        private readonly struct Token
        {
            public Token(
                string raw,
                bool isLineBreak,
                string colorTag)
            {
                Raw = raw;
                IsLineBreak = isLineBreak;
                ColorTag = colorTag;
            }

            public string Raw { get; }
            public bool IsLineBreak { get; }
            public string ColorTag { get; }
        }

        public static IReadOnlyList<string> Paginate(
            Text text,
            string legacyText)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));
            if (string.IsNullOrEmpty(legacyText))
                return new[] { string.Empty };

            List<Token> tokens = Tokenize(legacyText);
            if (tokens.Count == 0)
                return new[] { string.Empty };

            Rect rect = text.rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f ||
                text.font == null)
            {
                return new[] { JxqyLegacyRichText.ToUnity(legacyText) };
            }

            TextGenerationSettings settings =
                text.GetGenerationSettings(
                    new Vector2(rect.width, 0f));
            settings.horizontalOverflow = HorizontalWrapMode.Wrap;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            var pages = new List<string>();
            int start = 0;
            while (start < tokens.Count)
            {
                int low = start + 1;
                int high = tokens.Count;
                int best = start;
                while (low <= high)
                {
                    int middle = low + (high - low) / 2;
                    string candidate = BuildLegacyPage(
                        tokens,
                        start,
                        middle);
                    if (Fits(text, settings, candidate, rect.height))
                    {
                        best = middle;
                        low = middle + 1;
                    }
                    else
                    {
                        high = middle - 1;
                    }
                }

                if (best == start)
                    best = start + 1;

                string page = BuildLegacyPage(tokens, start, best);
                string unityPage = JxqyLegacyRichText.ToUnity(page);
                if (unityPage.Length > 0 || pages.Count == 0)
                    pages.Add(unityPage);

                // TextGui consumes the newline that crosses the bottom edge
                // before starting its next visual page.
                if (best < tokens.Count &&
                    tokens[best].IsLineBreak &&
                    !Fits(
                        text,
                        settings,
                        BuildLegacyPage(tokens, start, best + 1),
                        rect.height))
                {
                    best++;
                }
                start = best;
            }

            return pages.Count > 0
                ? pages
                : new[] { string.Empty };
        }

        private static bool Fits(
            Text text,
            TextGenerationSettings settings,
            string legacyText,
            float height)
        {
            string unityText = JxqyLegacyRichText.ToUnity(legacyText);
            float preferredHeight =
                text.cachedTextGeneratorForLayout.GetPreferredHeight(
                    unityText,
                    settings) /
                text.pixelsPerUnit;
            return preferredHeight <= height + 0.01f &&
                   text.cachedTextGeneratorForLayout.lineCount <=
                   OriginalMaximumLinesPerPage;
        }

        private static string BuildLegacyPage(
            IReadOnlyList<Token> tokens,
            int start,
            int end)
        {
            var builder = new StringBuilder();
            string activeColor = null;
            for (int index = 0; index < start; index++)
            {
                if (tokens[index].ColorTag != null)
                    activeColor = tokens[index].ColorTag;
            }
            if (!string.IsNullOrEmpty(activeColor))
                builder.Append(activeColor);
            for (int index = start; index < end; index++)
                builder.Append(tokens[index].Raw);
            return builder.ToString();
        }

        private static List<Token> Tokenize(string value)
        {
            var tokens = new List<Token>(value.Length);
            int cursor = 0;
            while (cursor < value.Length)
            {
                if (value[cursor] == '<')
                {
                    int tagEnd = value.IndexOf('>', cursor + 1);
                    if (tagEnd >= 0)
                    {
                        string raw = value.Substring(
                            cursor,
                            tagEnd - cursor + 1);
                        string tag = value.Substring(
                            cursor + 1,
                            tagEnd - cursor - 1);
                        bool isLineBreak = tag.Equals(
                            "enter",
                            StringComparison.OrdinalIgnoreCase);
                        string colorTag = tag.StartsWith(
                            "color=",
                            StringComparison.OrdinalIgnoreCase)
                                ? raw
                                : null;
                        tokens.Add(new Token(
                            raw,
                            isLineBreak,
                            colorTag));
                        cursor = tagEnd + 1;
                        continue;
                    }
                }

                int length = char.IsHighSurrogate(value[cursor]) &&
                             cursor + 1 < value.Length &&
                             char.IsLowSurrogate(value[cursor + 1])
                    ? 2
                    : 1;
                string character = value.Substring(cursor, length);
                tokens.Add(new Token(
                    character,
                    character == "\n",
                    null));
                cursor += length;
            }
            return tokens;
        }
    }

    /// <summary>
    /// Reusable, prefab-owned list/slot binding used by the original-layout
    /// inventory, equipment, skill, trade and save/load windows.
    /// </summary>
    public sealed class JxqyListSlotWidget : UIWidget,
        IPointerClickHandler,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerMoveHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IDropHandler
    {
        public enum SlotKind
        {
            None,
            Inventory,
            Equipment,
            Skill,
            GoodsShortcut,
            MagicShortcut,
            Cultivation,
        }

        public sealed class DragData
        {
            public DragData(SlotKind kind, int index)
            {
                Kind = kind;
                Index = index;
            }

            public SlotKind Kind { get; }
            public int Index { get; }
        }

        private static JxqyListSlotWidget _dragSource;
        private Button _button;
        private JxqyListSlotEventRelay _eventRelay;
        private Image _selection;
        private RawImage _icon;
        private JxqyUiFrameBinding _iconBinding;
        private Text _name;
        private Text _detail;
        private Image _cooldownOverlay;
        private Text _cooldownText;
        private Color _defaultNameColor;
        private Action<int> _clicked;
        private Action<int> _rightClicked;
        private Action<int> _hovered;
        private Action _hoverExited;
        private Action<DragData, DragData> _dropped;
        private Action<JxqyUiSound> _soundRequested;
        private DragData _dragData;
        private GameObject _dragPreview;
        private RectTransform _dragPreviewRect;
        private Graphic[] _dragGraphics;
        private bool[] _dragRaycastTargets;
        private int _index;
        private bool _selected;
        private float _cooldownMilliseconds;
        private float _hoverSeconds;
        private bool _mouseInside;
        private bool _hoverShown;
        private bool _detailPinned;
        private bool _touchHeld;
        private bool _suppressNextClick;
        private const float HoverDelaySeconds = 0.5f;

        protected override void ScriptGenerator()
        {
            _button = gameObject.GetComponent<Button>();
            _eventRelay =
                gameObject.GetComponent<JxqyListSlotEventRelay>() ??
                gameObject.AddComponent<JxqyListSlotEventRelay>();
            _eventRelay.Target = this;
            _selection = gameObject.GetComponent<Image>();
            _icon = FindChildComponent<RawImage>("m_raw_Icon");
            if (_icon != null)
                _iconBinding = new JxqyUiFrameBinding(
                    _icon,
                    preserveTargetSize: false);
            _name = FindChildComponent<Text>("m_text_Name");
            if (_name != null)
                _defaultNameColor = _name.color;
            _detail = FindChildComponent<Text>("m_text_Detail");
            _cooldownOverlay =
                FindChildComponent<Image>("m_img_Cooldown");
            _cooldownText =
                FindChildComponent<Text>("m_img_Cooldown/m_text_Cooldown");
            if (_cooldownOverlay == null || _cooldownText == null)
            {
                throw new InvalidOperationException(
                    $"Slot prefab '{gameObject.name}' is missing its " +
                    "cooldown overlay nodes.");
            }
            _cooldownOverlay.gameObject.SetActive(false);
            _button?.onClick.AddListener(OnClicked);
        }

        protected override void OnDestroy()
        {
            _button?.onClick.RemoveListener(OnClicked);
            if (_eventRelay != null)
                _eventRelay.Target = null;
            _eventRelay = null;
            _iconBinding?.Dispose();
            _iconBinding = null;
            _clicked = null;
            _rightClicked = null;
            _hovered = null;
            _hoverExited = null;
            _dropped = null;
            _soundRequested = null;
            _dragData = null;
            RestoreDragRaycasts();
            DestroyDragPreview();
            if (ReferenceEquals(_dragSource, this))
                _dragSource = null;
        }

        public void Bind(
            int index,
            string name,
            string detail,
            bool selected,
            bool interactable,
            Action<int> clicked,
            Action<int> rightClicked = null,
            string iconCategory = null,
            string iconFileName = null,
            DragData dragData = null,
            Action<DragData, DragData> dropped = null,
            float cooldownMilliseconds = 0f,
            Action<JxqyUiSound> soundRequested = null,
            Action<int> hovered = null,
            Action hoverExited = null)
        {
            _index = index;
            _clicked = clicked;
            _rightClicked = rightClicked;
            _hovered = hovered;
            _hoverExited = hoverExited;
            _dragData = dragData;
            _dropped = dropped;
            _soundRequested = soundRequested;
            _selected = selected;
            _mouseInside = false;
            _hoverShown = false;
            _detailPinned = false;
            _touchHeld = false;
            _suppressNextClick = false;
            _hoverSeconds = 0f;
            if (_name != null)
            {
                _name.text = name ?? string.Empty;
                RestoreNameColor();
            }
            if (_detail != null)
                _detail.text = detail ?? string.Empty;
            if (_button != null)
                _button.interactable = interactable;
            _iconBinding?.Set(iconCategory, iconFileName);
            _cooldownMilliseconds =
                Mathf.Max(0f, cooldownMilliseconds);
            RefreshCooldownOverlay();
            if (_selection != null)
            {
                _selection.color = selected
                    ? new Color32(172, 124, 40, 52)
                    : Color.clear;
            }
        }

        public void ConfigureName(
            int fontSize,
            Color normalColor)
        {
            if (_name == null)
                return;
            _name.fontSize = Mathf.Max(1, fontSize);
            _name.color = normalColor;
            _defaultNameColor = normalColor;
        }

        private void OnClicked()
        {
            if (_suppressNextClick)
            {
                _suppressNextClick = false;
                return;
            }
            if (_clicked != null)
                _soundRequested?.Invoke(JxqyUiSound.Browse);
            _detailPinned = true;
            _clicked?.Invoke(_index);
        }

        protected override void OnUpdate()
        {
            if (_cooldownMilliseconds > 0f)
            {
                _cooldownMilliseconds = Mathf.Max(
                    0f,
                    _cooldownMilliseconds -
                    Time.unscaledDeltaTime * 1000f);
                RefreshCooldownOverlay();
            }
            if (!_mouseInside || _hoverShown || _hovered == null ||
                _button != null && !_button.interactable)
            {
                if (!_touchHeld || _hoverShown || _hovered == null ||
                    _button != null && !_button.interactable)
                    return;
            }
            _hoverSeconds += Time.unscaledDeltaTime;
            if (_hoverSeconds < HoverDelaySeconds)
                return;
            _hoverShown = true;
            _detailPinned = _touchHeld;
            _suppressNextClick = _touchHeld;
            _hovered.Invoke(_index);
        }

        private void RefreshCooldownOverlay()
        {
            if (_cooldownOverlay == null)
                return;
            bool visible = _cooldownMilliseconds > 0f;
            _cooldownOverlay.gameObject.SetActive(visible);
            if (visible && _cooldownText != null)
            {
                _cooldownText.text =
                    (_cooldownMilliseconds / 1000f)
                    .ToString("0.0");
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null)
                return;
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                if (_rightClicked != null)
                    _soundRequested?.Invoke(JxqyUiSound.Browse);
                _rightClicked?.Invoke(_index);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_name != null &&
                (_button == null || _button.interactable))
                _name.color = new Color32(204, 0, 0, 255);
            // Legacy pointer ids are negative for mouse buttons. Touch ids are
            // non-negative, so a touch-down never starts a fake hover timer.
            _mouseInside = eventData != null && eventData.pointerId < 0;
            _hoverSeconds = 0f;
            _hoverShown = false;
            _detailPinned = false;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            RestoreNameColor();
            _mouseInside = false;
            _touchHeld = false;
            _hoverSeconds = 0f;
            if (_hoverShown && !_detailPinned)
                _hoverExited?.Invoke();
            _hoverShown = false;
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (!_mouseInside || _hoverShown || eventData == null)
                return;
            if (eventData.delta.sqrMagnitude > 0.01f)
                _hoverSeconds = 0f;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null || eventData.pointerId < 0 ||
                eventData.button != PointerEventData.InputButton.Left ||
                _hovered == null)
                return;
            _touchHeld = true;
            _hoverSeconds = 0f;
            _hoverShown = false;
            _detailPinned = false;
            _suppressNextClick = false;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _touchHeld = false;
        }

        private void RestoreNameColor()
        {
            if (_name == null)
                return;
            _name.color = _selected
                ? new Color32(170, 40, 30, 255)
                : _defaultNameColor;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_dragData == null ||
                _button != null && !_button.interactable)
            {
                return;
            }
            _dragSource = this;
            DisableDragRaycasts();
            CreateDragPreview(eventData);
            if (_icon != null)
                _icon.enabled = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!ReferenceEquals(_dragSource, this) ||
                _dragPreviewRect == null ||
                eventData == null)
            {
                return;
            }
            RectTransform parent =
                _dragPreviewRect.parent as RectTransform;
            if (parent != null &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPosition))
            {
                _dragPreviewRect.anchoredPosition = localPosition;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            bool completedDrag = ReferenceEquals(_dragSource, this);
            if (completedDrag)
                _dragSource = null;
            if (_icon != null)
                _icon.enabled = true;
            RestoreDragRaycasts();
            DestroyDragPreview();
            // The original GuiManager plays 界-拖放.wav whenever a drag
            // operation ends with a real source texture, even if the target
            // rejects the drop. Keep the sound on drag completion so invalid
            // targets are not silently different from the original game.
            if (completedDrag && _dragData != null)
                _soundRequested?.Invoke(JxqyUiSound.DragDrop);
        }

        public void OnDrop(PointerEventData eventData)
        {
            JxqyListSlotWidget source = _dragSource;
            if (source == null ||
                ReferenceEquals(source, this) ||
                source._dragData == null ||
                _dragData == null)
            {
                return;
            }
            if (_dropped == null)
                return;
            _dropped.Invoke(source._dragData, _dragData);
        }

        private void CreateDragPreview(PointerEventData eventData)
        {
            if (_icon == null || _icon.texture == null)
                return;
            Canvas canvas = gameObject.GetComponentInParent<Canvas>();
            if (canvas == null)
                return;
            var preview = new GameObject(
                "JxqyDragPreview",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasRenderer),
                typeof(RawImage));
            // Runtime-created objects default to the Default layer. The JXQY
            // UI camera intentionally renders only the UI layer, so inheriting
            // the source slot's layer is required for Game-view rendering.
            preview.layer = gameObject.layer;
            preview.transform.SetParent(canvas.rootCanvas.transform, false);
            preview.transform.SetAsLastSibling();
            _dragPreview = preview;
            _dragPreviewRect = preview.GetComponent<RectTransform>();
            Canvas previewCanvas = preview.GetComponent<Canvas>();
            previewCanvas.overrideSorting = true;
            previewCanvas.sortingLayerID = canvas.rootCanvas.sortingLayerID;
            previewCanvas.sortingOrder = short.MaxValue;
            Vector2 size = _icon.rectTransform != null
                ? _icon.rectTransform.rect.size
                : new Vector2(40f, 40f);
            _dragPreviewRect.sizeDelta = new Vector2(
                Mathf.Max(1f, size.x),
                Mathf.Max(1f, size.y));
            RawImage image = preview.GetComponent<RawImage>();
            image.texture = _icon.texture;
            image.uvRect = _icon.uvRect;
            image.color = Color.white;
            image.raycastTarget = false;
            OnDrag(eventData);
        }

        private void DestroyDragPreview()
        {
            if (_dragPreview != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(_dragPreview);
                else
                    UnityEngine.Object.DestroyImmediate(_dragPreview);
            }
            _dragPreview = null;
            _dragPreviewRect = null;
        }

        private void RestoreDragRaycasts()
        {
            if (_dragGraphics != null &&
                _dragRaycastTargets != null)
            {
                int count = Mathf.Min(
                    _dragGraphics.Length,
                    _dragRaycastTargets.Length);
                for (int index = 0; index < count; index++)
                {
                    if (_dragGraphics[index] != null)
                    {
                        _dragGraphics[index].raycastTarget =
                            _dragRaycastTargets[index];
                    }
                }
            }
            _dragGraphics = null;
            _dragRaycastTargets = null;
        }

        private void DisableDragRaycasts()
        {
            RestoreDragRaycasts();
            _dragGraphics =
                gameObject.GetComponentsInChildren<Graphic>(true);
            _dragRaycastTargets = new bool[_dragGraphics.Length];
            for (int index = 0; index < _dragGraphics.Length; index++)
            {
                Graphic graphic = _dragGraphics[index];
                if (graphic == null)
                    continue;
                _dragRaycastTargets[index] = graphic.raycastTarget;
                graphic.raycastTarget = false;
            }
        }
    }
}
