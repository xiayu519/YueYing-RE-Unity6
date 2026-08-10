using System;
using System.Collections.Generic;
using Jxqy.Domain.Input;
using Jxqy.Domain.Presentation;
using Jxqy.Domain.World;
using Jxqy.Ports;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Jxqy.UnityAdapters
{
    public enum JxqyTouchOwner
    {
        World,
        Movement,
        Action,
        DirectUi,
    }

    public sealed class JxqyTouchInputPort : IJxqyInputPort
    {
        private readonly Dictionary<int, JxqyTouchOwner> _owners =
            new Dictionary<int, JxqyTouchOwner>();
        private readonly JxqyInputIntentBuffer _buffer =
            new JxqyInputIntentBuffer();
        private readonly List<JxqyInputIntent> _intentBuffer =
            new List<JxqyInputIntent>(8);
        private JxqyFloat2 _move;
        private JxqyFloat2 _pointer;
        private JxqyInputButtons _buttons;
        private int? _movementTouch;
        private int? _worldTouch;
        private long _sequence;

        public IReadOnlyDictionary<int, JxqyTouchOwner> ActiveTouches =>
            _owners;

        public JxqyInputFrame CaptureFrame()
        {
            return new JxqyInputFrame(
                checked(++_sequence),
                _move.X,
                _move.Y,
                _pointer.X,
                _pointer.Y,
                _buttons);
        }

        public IReadOnlyList<JxqyInputIntent> CaptureIntents()
        {
            _buffer.Drain(_intentBuffer);
            return _intentBuffer;
        }

        public bool BeginWorldTouch(int touchId, JxqyFloat2 logicalPosition)
        {
            if (_worldTouch.HasValue ||
                !TryClaim(touchId, JxqyTouchOwner.World))
                return false;
            _worldTouch = touchId;
            _pointer = logicalPosition;
            _buttons |= JxqyInputButtons.PointerPrimary;
            _buffer.SetPointer(logicalPosition);
            _buffer.Press(
                JxqyInputIntentKind.PointerPrimary,
                pointer: logicalPosition);
            return true;
        }

        public bool MoveWorldTouch(int touchId, JxqyFloat2 logicalPosition)
        {
            if (_worldTouch != touchId ||
                !_owners.TryGetValue(touchId, out JxqyTouchOwner owner) ||
                owner != JxqyTouchOwner.World)
                return false;
            _pointer = logicalPosition;
            _buffer.SetPointer(logicalPosition);
            return true;
        }

        public bool BeginMovementTouch(int touchId)
        {
            if (_movementTouch.HasValue ||
                !TryClaim(touchId, JxqyTouchOwner.Movement))
                return false;
            _movementTouch = touchId;
            return true;
        }

        public bool SetVirtualMove(int touchId, JxqyFloat2 direction)
        {
            if (_movementTouch != touchId)
                return false;
            _move = direction.LengthSquared > 1
                ? direction.Normalized
                : direction;
            _buffer.SetMove(_move);
            return true;
        }

        public bool BeginActionTouch(
            int touchId,
            JxqyInputIntentKind kind,
            int slot = -1)
        {
            if (!TryClaim(touchId, JxqyTouchOwner.Action))
                return false;
            SetButton(kind, true, slot);
            _buffer.Press(kind, slot);
            return true;
        }

        public bool BeginDirectUiTouch(int touchId)
        {
            return TryClaim(touchId, JxqyTouchOwner.DirectUi);
        }

        public bool EndTouch(
            int touchId,
            JxqyInputIntentKind action =
                JxqyInputIntentKind.PointerPrimary,
            int slot = -1)
        {
            if (!_owners.TryGetValue(touchId, out JxqyTouchOwner owner))
                return false;
            _owners.Remove(touchId);
            switch (owner)
            {
                case JxqyTouchOwner.World:
                    _worldTouch = null;
                    _buttons &= ~JxqyInputButtons.PointerPrimary;
                    _buffer.Release(
                        JxqyInputIntentKind.PointerPrimary,
                        pointer: _pointer);
                    break;
                case JxqyTouchOwner.Movement:
                    _movementTouch = null;
                    _move = JxqyFloat2.Zero;
                    _buffer.SetMove(_move);
                    break;
                case JxqyTouchOwner.Action:
                    SetButton(action, false, slot);
                    _buffer.Release(action, slot);
                    break;
            }
            return true;
        }

        public void ResetTransientState()
        {
            _owners.Clear();
            _movementTouch = null;
            _worldTouch = null;
            _move = JxqyFloat2.Zero;
            _buttons = JxqyInputButtons.None;
            _buffer.ResetTransientState();
        }

        private bool TryClaim(int touchId, JxqyTouchOwner owner)
        {
            if (_owners.ContainsKey(touchId))
                return false;
            _owners.Add(touchId, owner);
            return true;
        }

        private void SetButton(
            JxqyInputIntentKind kind,
            bool enabled,
            int slot)
        {
            JxqyInputButtons flag;
            switch (kind)
            {
                case JxqyInputIntentKind.Interact:
                    flag = JxqyInputButtons.Interact;
                    break;
                case JxqyInputIntentKind.PrimaryAttack:
                    flag = JxqyInputButtons.Attack;
                    break;
                case JxqyInputIntentKind.UseSkill:
                    flag = slot == 0
                        ? JxqyInputButtons.Skill1
                        : slot == 1
                            ? JxqyInputButtons.Skill2
                            : JxqyInputButtons.Skill3;
                    break;
                case JxqyInputIntentKind.UseItem:
                    flag = JxqyInputButtons.UseItem;
                    break;
                case JxqyInputIntentKind.Menu:
                    flag = JxqyInputButtons.Menu;
                    break;
                case JxqyInputIntentKind.Confirm:
                    flag = JxqyInputButtons.Confirm;
                    break;
                case JxqyInputIntentKind.Cancel:
                    flag = JxqyInputButtons.Cancel;
                    break;
                default:
                    flag = JxqyInputButtons.None;
                    break;
            }
            if (enabled)
                _buttons |= flag;
            else
                _buttons &= ~flag;
        }
    }

    public static class JxqyTouchInputBridge
    {
        public static JxqyTouchInputPort Port { get; set; }

        public static JxqyFloat2 ScreenToLogical(Vector2 screenPosition)
        {
            Rect safe = Screen.safeArea;
            JxqyViewportLayout layout = JxqyLogicalViewport.Calculate(
                Screen.width,
                Screen.height,
                new JxqyIntRect(
                    Mathf.RoundToInt(safe.x),
                    Mathf.RoundToInt(safe.y),
                    Mathf.RoundToInt(safe.width),
                    Mathf.RoundToInt(safe.height)));
            JxqyLogicalPoint value = JxqyLogicalViewport.ScreenToLogical(
                screenPosition.x,
                screenPosition.y,
                layout);
            return new JxqyFloat2(value.X, value.Y);
        }
    }

    public sealed class JxqyWorldTouchSurface : MonoBehaviour,
        IPointerDownHandler,
        IDragHandler,
        IPointerUpHandler
    {
        public void OnPointerDown(PointerEventData eventData)
        {
            JxqyTouchInputBridge.Port?.BeginWorldTouch(
                eventData.pointerId,
                JxqyTouchInputBridge.ScreenToLogical(eventData.position));
        }

        public void OnDrag(PointerEventData eventData)
        {
            JxqyTouchInputBridge.Port?.MoveWorldTouch(
                eventData.pointerId,
                JxqyTouchInputBridge.ScreenToLogical(eventData.position));
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            JxqyTouchInputBridge.Port?.EndTouch(eventData.pointerId);
        }
    }

    public sealed class JxqyVirtualJoystickInput : MonoBehaviour,
        IPointerDownHandler,
        IDragHandler,
        IPointerUpHandler
    {
        [SerializeField] private RectTransform _movementArea;
        [SerializeField, Min(1)] private float _radius = 80f;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (JxqyTouchInputBridge.Port?.BeginMovementTouch(
                    eventData.pointerId) == true)
                UpdateMove(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            UpdateMove(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            JxqyTouchInputBridge.Port?.EndTouch(eventData.pointerId);
        }

        private void UpdateMove(PointerEventData eventData)
        {
            RectTransform area = _movementArea != null
                ? _movementArea
                : transform as RectTransform;
            if (area == null ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    area,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 local))
                return;
            Vector2 direction = local / Mathf.Max(1, _radius);
            JxqyTouchInputBridge.Port?.SetVirtualMove(
                eventData.pointerId,
                new JxqyFloat2(direction.x, direction.y));
        }
    }

    public sealed class JxqyActionButtonInput : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler
    {
        [SerializeField] private JxqyInputIntentKind _intent =
            JxqyInputIntentKind.PrimaryAttack;
        [SerializeField] private int _slot = -1;

        public void OnPointerDown(PointerEventData eventData)
        {
            JxqyTouchInputBridge.Port?.BeginActionTouch(
                eventData.pointerId,
                _intent,
                _slot);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            JxqyTouchInputBridge.Port?.EndTouch(
                eventData.pointerId,
                _intent,
                _slot);
        }
    }

    public sealed class JxqyDirectUiTouchGuard : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler
    {
        public void OnPointerDown(PointerEventData eventData)
        {
            JxqyTouchInputBridge.Port?.BeginDirectUiTouch(
                eventData.pointerId);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            JxqyTouchInputBridge.Port?.EndTouch(eventData.pointerId);
        }
    }
}
