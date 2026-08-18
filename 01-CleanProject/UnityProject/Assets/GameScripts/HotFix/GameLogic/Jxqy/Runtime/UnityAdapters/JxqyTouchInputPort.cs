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
        private JxqyInputIntentKind _worldIntent =
            JxqyInputIntentKind.PointerPrimary;
        private bool _movementEngaged;
        private long _sequence;

        public IReadOnlyDictionary<int, JxqyTouchOwner> ActiveTouches =>
            _owners;
        public bool HasMovementTouch => _movementTouch.HasValue;
        public bool HasWorldTouch => _worldTouch.HasValue;

        public bool IsClaimed(int touchId)
        {
            return _owners.ContainsKey(touchId);
        }

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
            _worldIntent =
                (_buttons & JxqyInputButtons.JumpModifier) != 0
                    ? JxqyInputIntentKind.Jump
                    : JxqyInputIntentKind.PointerPrimary;
            if (_worldIntent == JxqyInputIntentKind.PointerPrimary)
                _buttons |= JxqyInputButtons.PointerPrimary;
            _buffer.SetPointer(logicalPosition);
            _buffer.Press(
                _worldIntent,
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
            bool engaged = _move.LengthSquared > 0.0001f;
            if (engaged && !_movementEngaged)
                _buffer.Press(JxqyInputIntentKind.MobileMove);
            _movementEngaged = engaged;
            _buffer.SetMove(_move);
            return true;
        }

        public bool BeginModifierTouch(
            int touchId,
            JxqyInputButtons modifier)
        {
            if (modifier != JxqyInputButtons.RunModifier &&
                modifier != JxqyInputButtons.JumpModifier)
            {
                return false;
            }
            if (!TryClaim(touchId, JxqyTouchOwner.Action))
                return false;
            _buttons |= modifier;
            return true;
        }

        public bool EndModifierTouch(
            int touchId,
            JxqyInputButtons modifier)
        {
            if (!_owners.TryGetValue(touchId, out JxqyTouchOwner owner) ||
                owner != JxqyTouchOwner.Action)
            {
                return false;
            }
            _owners.Remove(touchId);
            _buttons &= ~modifier;
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
                        _worldIntent,
                        pointer: _pointer);
                    _worldIntent = JxqyInputIntentKind.PointerPrimary;
                    break;
                case JxqyTouchOwner.Movement:
                    _movementTouch = null;
                    _movementEngaged = false;
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
            _worldIntent = JxqyInputIntentKind.PointerPrimary;
            _movementEngaged = false;
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
                case JxqyInputIntentKind.MobileDirectionalAttack:
                    flag = JxqyInputButtons.Attack;
                    break;
                case JxqyInputIntentKind.UseSkill:
                case JxqyInputIntentKind.MobileDirectionalSkill:
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

    public sealed class JxqyCombinedInputPort : IJxqyInputPort
    {
        private readonly IJxqyInputPort _desktop;
        private readonly JxqyTouchInputPort _touch;
        private readonly List<JxqyInputIntent> _intents =
            new List<JxqyInputIntent>(16);
        private long _sequence;

        public JxqyCombinedInputPort(
            IJxqyInputPort desktop,
            JxqyTouchInputPort touch)
        {
            _desktop = desktop ?? throw new ArgumentNullException(
                nameof(desktop));
            _touch = touch ?? throw new ArgumentNullException(nameof(touch));
        }

        public JxqyInputFrame CaptureFrame()
        {
            JxqyInputFrame desktop = _desktop.CaptureFrame();
            JxqyInputFrame touch = _touch.CaptureFrame();
            bool useTouchMove = _touch.HasMovementTouch;
            bool useTouchPointer = _touch.HasWorldTouch;
            return new JxqyInputFrame(
                checked(++_sequence),
                useTouchMove ? touch.MoveX : desktop.MoveX,
                useTouchMove ? touch.MoveY : desktop.MoveY,
                useTouchPointer ? touch.PointerX : desktop.PointerX,
                useTouchPointer ? touch.PointerY : desktop.PointerY,
                desktop.Buttons | touch.Buttons);
        }

        public IReadOnlyList<JxqyInputIntent> CaptureIntents()
        {
            _intents.Clear();
            _intents.AddRange(_desktop.CaptureIntents());
            _intents.AddRange(_touch.CaptureIntents());
            return _intents;
        }

        public void ResetTransientState()
        {
            _desktop.ResetTransientState();
            _touch.ResetTransientState();
            _intents.Clear();
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
