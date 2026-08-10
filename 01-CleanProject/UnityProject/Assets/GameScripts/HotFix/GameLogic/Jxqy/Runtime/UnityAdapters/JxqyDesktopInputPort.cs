using System;
using System.Collections.Generic;
using Jxqy.Domain.Input;
using Jxqy.Domain.Presentation;
using Jxqy.Domain.World;
using Jxqy.Ports;
using UnityEngine;

namespace Jxqy.UnityAdapters
{
    public static class JxqyDesktopInputBridge
    {
        /// <summary>
        /// Optional source used by deterministic Editor/Windows validation.
        /// Normal gameplay leaves this null and reads Unity keyboard/mouse input.
        /// </summary>
        public static IJxqyDesktopInputSource OverrideSource { get; set; }
    }

    public enum JxqyDesktopKey
    {
        MoveLeft,
        MoveRight,
        MoveUp,
        MoveDown,
        Interact,
        Attack,
        Skill1,
        Skill2,
        Skill3,
        Skill4,
        Skill5,
        UseItem,
        UseItem2,
        UseItem3,
        Menu,
        Confirm,
        Cancel,
        RunModifier,
        PointerPrimary,
        PointerSecondary,
        Meditate,
        JumpModifier,
        StatusWindow,
        EquipmentWindow,
        TrainingWindow,
        InventoryWindow,
        SkillsWindow,
        MemoWindow,
        LittleMap,
        MoveDirection0,
        MoveDirection1,
        MoveDirection2,
        MoveDirection3,
        MoveDirection4,
        MoveDirection5,
        MoveDirection6,
        MoveDirection7,
        ControlModifier,
        FullscreenEnter,
    }

    public interface IJxqyDesktopInputSource
    {
        int Frame { get; }
        int ScreenWidth { get; }
        int ScreenHeight { get; }
        JxqyIntRect SafeArea { get; }
        JxqyFloat2 PointerScreenPosition { get; }
        bool IsHeld(JxqyDesktopKey key);
        bool WasPressed(JxqyDesktopKey key);
        bool WasReleased(JxqyDesktopKey key);
    }

    public sealed class JxqyDesktopInputPort : IJxqyInputPort
    {
        private static readonly JxqyDesktopKey[] AllKeys =
            (JxqyDesktopKey[])Enum.GetValues(typeof(JxqyDesktopKey));
        private readonly IJxqyDesktopInputSource _source;
        private readonly JxqyInputIntentBuffer _buffer =
            new JxqyInputIntentBuffer();
        private readonly List<JxqyInputIntent> _intentBuffer =
            new List<JxqyInputIntent>(12);
        private IReadOnlyList<JxqyInputIntent> _cachedIntents =
            Array.Empty<JxqyInputIntent>();
        private JxqyInputFrame _cachedFrame;
        private int _sourceFrame = int.MinValue;
        private long _sequence;
        private bool _intentsConsumed;
        private bool _suppressUntilNeutral;

        public JxqyDesktopInputPort(IJxqyDesktopInputSource source = null)
        {
            _source = source ?? new RoutedDesktopInputSource();
        }

        public JxqyInputFrame CaptureFrame()
        {
            Poll();
            return _cachedFrame;
        }

        public IReadOnlyList<JxqyInputIntent> CaptureIntents()
        {
            Poll();
            if (_intentsConsumed)
                return Array.Empty<JxqyInputIntent>();
            _intentsConsumed = true;
            return _cachedIntents;
        }

        public void ResetTransientState()
        {
            _buffer.ResetTransientState();
            _cachedIntents = Array.Empty<JxqyInputIntent>();
            _sourceFrame = int.MinValue;
            _intentsConsumed = false;
            _suppressUntilNeutral = true;
        }

        private void Poll()
        {
            if (_sourceFrame == _source.Frame)
                return;
            _sourceFrame = _source.Frame;
            _intentsConsumed = false;
            JxqyFloat2 pointer = ToLogicalPointer();
            _buffer.SetPointer(pointer);
            if (_suppressUntilNeutral)
            {
                if (AnyControlHeld())
                {
                    _cachedFrame = new JxqyInputFrame(
                        checked(++_sequence),
                        0,
                        0,
                        pointer.X,
                        pointer.Y,
                        JxqyInputButtons.None);
                    DrainIntents();
                    return;
                }
                _suppressUntilNeutral = false;
            }

            JxqyFloat2 move = GetMove();
            _buffer.SetMove(move);
            AddLegacyKeyboardMovement();
            AddPointerPrimaryAction(pointer);
            AddFullscreenAction();
            AddAction(
                JxqyDesktopKey.PointerSecondary,
                JxqyInputIntentKind.PointerSecondary,
                pointer: pointer);
            AddAction(
                JxqyDesktopKey.Interact,
                JxqyInputIntentKind.Interact,
                pointer: pointer);
            AddAction(
                JxqyDesktopKey.Attack,
                JxqyInputIntentKind.PrimaryAttack);
            AddAction(
                JxqyDesktopKey.Skill1,
                JxqyInputIntentKind.UseSkill,
                slot: 0);
            AddAction(
                JxqyDesktopKey.Skill2,
                JxqyInputIntentKind.UseSkill,
                slot: 1);
            AddAction(
                JxqyDesktopKey.Skill3,
                JxqyInputIntentKind.UseSkill,
                slot: 2);
            AddAction(
                JxqyDesktopKey.Skill4,
                JxqyInputIntentKind.UseSkill,
                slot: 3);
            AddAction(
                JxqyDesktopKey.Skill5,
                JxqyInputIntentKind.UseSkill,
                slot: 4);
            AddAction(
                JxqyDesktopKey.UseItem,
                JxqyInputIntentKind.UseItem,
                slot: 0);
            AddAction(
                JxqyDesktopKey.UseItem2,
                JxqyInputIntentKind.UseItem,
                slot: 1);
            AddAction(
                JxqyDesktopKey.UseItem3,
                JxqyInputIntentKind.UseItem,
                slot: 2);
            AddAction(
                JxqyDesktopKey.Menu,
                JxqyInputIntentKind.Menu);
            AddAction(
                JxqyDesktopKey.Confirm,
                JxqyInputIntentKind.Confirm);
            AddAction(
                JxqyDesktopKey.Cancel,
                JxqyInputIntentKind.Cancel);
            AddAction(
                JxqyDesktopKey.Meditate,
                JxqyInputIntentKind.Meditate);
            AddAction(
                JxqyDesktopKey.StatusWindow,
                JxqyInputIntentKind.ToggleStatus);
            AddAction(
                JxqyDesktopKey.EquipmentWindow,
                JxqyInputIntentKind.ToggleEquipment);
            AddAction(
                JxqyDesktopKey.TrainingWindow,
                JxqyInputIntentKind.ToggleTraining);
            AddAction(
                JxqyDesktopKey.InventoryWindow,
                JxqyInputIntentKind.ToggleInventory);
            AddAction(
                JxqyDesktopKey.SkillsWindow,
                JxqyInputIntentKind.ToggleSkills);
            AddAction(
                JxqyDesktopKey.MemoWindow,
                JxqyInputIntentKind.ToggleMemo);
            AddAction(
                JxqyDesktopKey.LittleMap,
                JxqyInputIntentKind.ToggleLittleMap);
            _cachedFrame = new JxqyInputFrame(
                checked(++_sequence),
                move.X,
                move.Y,
                pointer.X,
                pointer.Y,
                GetButtons());
            DrainIntents();
        }

        private void DrainIntents()
        {
            _buffer.Drain(_intentBuffer);
            _cachedIntents = _intentBuffer;
        }

        private void AddAction(
            JxqyDesktopKey key,
            JxqyInputIntentKind kind,
            int slot = -1,
            JxqyFloat2 pointer = default)
        {
            if (_source.WasPressed(key))
                _buffer.Press(kind, slot, pointer);
            if (_source.WasReleased(key))
                _buffer.Release(kind, slot, pointer);
        }

        private void AddPointerPrimaryAction(JxqyFloat2 pointer)
        {
            bool pointerPressed =
                _source.WasPressed(JxqyDesktopKey.PointerPrimary);
            bool pointerHeld =
                _source.IsHeld(JxqyDesktopKey.PointerPrimary);
            bool jumpHeld =
                _source.IsHeld(JxqyDesktopKey.JumpModifier);
            bool forceAttackHeld =
                _source.IsHeld(JxqyDesktopKey.ControlModifier);
            bool jumpChordStarted =
                pointerHeld &&
                _source.WasPressed(JxqyDesktopKey.JumpModifier);
            bool forceAttackChordStarted =
                pointerHeld &&
                _source.WasPressed(JxqyDesktopKey.ControlModifier);
            if (pointerPressed)
            {
                _buffer.Press(
                    jumpHeld
                        ? JxqyInputIntentKind.Jump
                        : forceAttackHeld
                            ? JxqyInputIntentKind.ForceAttack
                        : JxqyInputIntentKind.PointerPrimary,
                    pointer: pointer);
            }
            else if (jumpChordStarted)
            {
                // The original evaluates Alt during every frame in which the
                // left button is held, so Alt pressed after an existing click
                // must interrupt that movement immediately.
                _buffer.Press(
                    JxqyInputIntentKind.Jump,
                    pointer: pointer);
            }
            else if (forceAttackChordStarted && !jumpHeld)
            {
                // The original checks Ctrl on every frame while the left
                // mouse button is held, immediately after the Alt branch.
                _buffer.Press(
                    JxqyInputIntentKind.ForceAttack,
                    pointer: pointer);
            }
            if (_source.WasReleased(JxqyDesktopKey.PointerPrimary))
            {
                _buffer.Release(
                    JxqyInputIntentKind.PointerPrimary,
                    pointer: pointer);
            }
        }

        private void AddLegacyKeyboardMovement()
        {
            if (_source.IsHeld(JxqyDesktopKey.MoveUp))
            {
                _buffer.Press(JxqyInputIntentKind.LegacyMoveForward);
            }
            else if (_source.WasPressed(JxqyDesktopKey.MoveLeft))
            {
                _buffer.Press(JxqyInputIntentKind.LegacyTurnLeft);
            }
            else if (_source.WasPressed(JxqyDesktopKey.MoveRight))
            {
                _buffer.Press(JxqyInputIntentKind.LegacyTurnRight);
            }
            else if (_source.WasPressed(JxqyDesktopKey.MoveDown))
            {
                _buffer.Press(JxqyInputIntentKind.LegacyTurnBack);
            }

            for (int direction = 0; direction < 8; direction++)
            {
                JxqyDesktopKey key =
                    (JxqyDesktopKey)(
                        (int)JxqyDesktopKey.MoveDirection0 + direction);
                if (_source.IsHeld(key))
                {
                    _buffer.Press(
                        JxqyInputIntentKind.LegacyMoveDirection,
                        slot: direction);
                    break;
                }
            }
        }

        private void AddFullscreenAction()
        {
            if (_source.WasPressed(JxqyDesktopKey.FullscreenEnter) &&
                _source.IsHeld(JxqyDesktopKey.JumpModifier))
            {
                _buffer.Press(JxqyInputIntentKind.ToggleFullscreen);
            }
        }

        private JxqyFloat2 GetMove()
        {
            float x = (_source.IsHeld(JxqyDesktopKey.MoveRight) ? 1 : 0) -
                      (_source.IsHeld(JxqyDesktopKey.MoveLeft) ? 1 : 0);
            float y = (_source.IsHeld(JxqyDesktopKey.MoveUp) ? 1 : 0) -
                      (_source.IsHeld(JxqyDesktopKey.MoveDown) ? 1 : 0);
            var value = new JxqyFloat2(x, y);
            return value.LengthSquared > 1 ? value.Normalized : value;
        }

        private JxqyFloat2 ToLogicalPointer()
        {
            JxqyViewportLayout layout = JxqyLogicalViewport.Calculate(
                _source.ScreenWidth,
                _source.ScreenHeight,
                _source.SafeArea);
            JxqyLogicalPoint point = JxqyLogicalViewport.ScreenToLogical(
                _source.PointerScreenPosition.X,
                _source.PointerScreenPosition.Y,
                layout);
            return new JxqyFloat2(point.X, point.Y);
        }

        private JxqyInputButtons GetButtons()
        {
            JxqyInputButtons result = JxqyInputButtons.None;
            AddHeld(ref result, JxqyDesktopKey.Interact, JxqyInputButtons.Interact);
            AddHeld(ref result, JxqyDesktopKey.Attack, JxqyInputButtons.Attack);
            AddHeld(ref result, JxqyDesktopKey.Skill1, JxqyInputButtons.Skill1);
            AddHeld(ref result, JxqyDesktopKey.Skill2, JxqyInputButtons.Skill2);
            AddHeld(ref result, JxqyDesktopKey.Skill3, JxqyInputButtons.Skill3);
            AddHeld(ref result, JxqyDesktopKey.UseItem, JxqyInputButtons.UseItem);
            AddHeld(ref result, JxqyDesktopKey.Menu, JxqyInputButtons.Menu);
            AddHeld(ref result, JxqyDesktopKey.Confirm, JxqyInputButtons.Confirm);
            AddHeld(ref result, JxqyDesktopKey.Cancel, JxqyInputButtons.Cancel);
            AddHeld(
                ref result,
                JxqyDesktopKey.RunModifier,
                JxqyInputButtons.RunModifier);
            AddHeld(
                ref result,
                JxqyDesktopKey.JumpModifier,
                JxqyInputButtons.JumpModifier);
            AddHeld(
                ref result,
                JxqyDesktopKey.PointerPrimary,
                JxqyInputButtons.PointerPrimary);
            if (IsLegacyMovementKeyHeld())
                result |= JxqyInputButtons.LegacyKeyboardMovement;
            return result;
        }

        private bool IsLegacyMovementKeyHeld()
        {
            if (_source.IsHeld(JxqyDesktopKey.MoveLeft) ||
                _source.IsHeld(JxqyDesktopKey.MoveRight) ||
                _source.IsHeld(JxqyDesktopKey.MoveUp) ||
                _source.IsHeld(JxqyDesktopKey.MoveDown))
            {
                return true;
            }
            for (int direction = 0; direction < 8; direction++)
            {
                var key = (JxqyDesktopKey)(
                    (int)JxqyDesktopKey.MoveDirection0 + direction);
                if (_source.IsHeld(key))
                    return true;
            }
            return false;
        }

        private void AddHeld(
            ref JxqyInputButtons value,
            JxqyDesktopKey key,
            JxqyInputButtons flag)
        {
            if (_source.IsHeld(key))
                value |= flag;
        }

        private bool AnyControlHeld()
        {
            foreach (JxqyDesktopKey key in AllKeys)
            {
                if (_source.IsHeld(key))
                    return true;
            }
            return false;
        }

        private sealed class RoutedDesktopInputSource :
            IJxqyDesktopInputSource
        {
            private readonly UnityDesktopInputSource _unity = new();
            private IJxqyDesktopInputSource _lastSource;
            private int _lastSourceFrame = int.MinValue;
            private int _routedFrame;

            private IJxqyDesktopInputSource Current =>
                JxqyDesktopInputBridge.OverrideSource ?? _unity;

            public int Frame
            {
                get
                {
                    IJxqyDesktopInputSource source = Current;
                    int sourceFrame = source.Frame;
                    if (!ReferenceEquals(source, _lastSource) ||
                        sourceFrame != _lastSourceFrame)
                    {
                        _lastSource = source;
                        _lastSourceFrame = sourceFrame;
                        _routedFrame = checked(_routedFrame + 1);
                    }
                    return _routedFrame;
                }
            }
            public int ScreenWidth => Current.ScreenWidth;
            public int ScreenHeight => Current.ScreenHeight;
            public JxqyIntRect SafeArea => Current.SafeArea;
            public JxqyFloat2 PointerScreenPosition =>
                Current.PointerScreenPosition;
            public bool IsHeld(JxqyDesktopKey key) =>
                Current.IsHeld(key);
            public bool WasPressed(JxqyDesktopKey key) =>
                Current.WasPressed(key);
            public bool WasReleased(JxqyDesktopKey key) =>
                Current.WasReleased(key);
        }

        private sealed class UnityDesktopInputSource :
            IJxqyDesktopInputSource
        {
            public int Frame => Time.frameCount;
            public int ScreenWidth => Screen.width;
            public int ScreenHeight => Screen.height;
            public JxqyIntRect SafeArea
            {
                get
                {
                    Rect value = Screen.safeArea;
                    return new JxqyIntRect(
                        Mathf.RoundToInt(value.x),
                        Mathf.RoundToInt(value.y),
                        Mathf.RoundToInt(value.width),
                        Mathf.RoundToInt(value.height));
                }
            }
            public JxqyFloat2 PointerScreenPosition =>
                new JxqyFloat2(Input.mousePosition.x, Input.mousePosition.y);

            public bool IsHeld(JxqyDesktopKey key)
            {
                return GetKeys(key, Input.GetKey, Input.GetMouseButton);
            }

            public bool WasPressed(JxqyDesktopKey key)
            {
                return GetKeys(key, Input.GetKeyDown, Input.GetMouseButtonDown);
            }

            public bool WasReleased(JxqyDesktopKey key)
            {
                return GetKeys(key, Input.GetKeyUp, Input.GetMouseButtonUp);
            }

            private static bool GetKeys(
                JxqyDesktopKey key,
                Func<KeyCode, bool> keyboard,
                Func<int, bool> mouse)
            {
                switch (key)
                {
                    case JxqyDesktopKey.MoveLeft:
                        return keyboard(KeyCode.LeftArrow);
                    case JxqyDesktopKey.MoveRight:
                        return keyboard(KeyCode.RightArrow);
                    case JxqyDesktopKey.MoveUp:
                        return keyboard(KeyCode.UpArrow);
                    case JxqyDesktopKey.MoveDown:
                        return keyboard(KeyCode.DownArrow);
                    case JxqyDesktopKey.Interact:
                        return keyboard(KeyCode.Q) || keyboard(KeyCode.E);
                    case JxqyDesktopKey.Attack:
                        // The original desktop game starts basic auto-attack
                        // only after the player clicks an enemy. Keep this
                        // action available to injected/touch input, but do not
                        // let a keyboard key silently select the nearest NPC.
                        return false;
                    case JxqyDesktopKey.Skill1:
                        return keyboard(KeyCode.A);
                    case JxqyDesktopKey.Skill2:
                        return keyboard(KeyCode.S);
                    case JxqyDesktopKey.Skill3:
                        return keyboard(KeyCode.D);
                    case JxqyDesktopKey.Skill4:
                        return keyboard(KeyCode.F);
                    case JxqyDesktopKey.Skill5:
                        return keyboard(KeyCode.G);
                    case JxqyDesktopKey.UseItem:
                        return keyboard(KeyCode.Z);
                    case JxqyDesktopKey.UseItem2:
                        return keyboard(KeyCode.X);
                    case JxqyDesktopKey.UseItem3:
                        return keyboard(KeyCode.C);
                    case JxqyDesktopKey.Menu:
                        return keyboard(KeyCode.Escape);
                    case JxqyDesktopKey.Confirm:
                        return keyboard(KeyCode.Space);
                    case JxqyDesktopKey.Cancel:
                        // Desktop Escape is handled as the original
                        // context-sensitive system/panel key above. Keep
                        // Cancel available to injected and touch input.
                        return false;
                    case JxqyDesktopKey.RunModifier:
                        return keyboard(KeyCode.LeftShift) ||
                               keyboard(KeyCode.RightShift);
                    case JxqyDesktopKey.Meditate:
                        return keyboard(KeyCode.V);
                    case JxqyDesktopKey.JumpModifier:
                        return keyboard(KeyCode.LeftAlt) ||
                               keyboard(KeyCode.RightAlt);
                    case JxqyDesktopKey.PointerPrimary:
                        return mouse(0);
                    case JxqyDesktopKey.PointerSecondary:
                        return mouse(1);
                    case JxqyDesktopKey.StatusWindow:
                        return keyboard(KeyCode.F1);
                    case JxqyDesktopKey.EquipmentWindow:
                        return keyboard(KeyCode.F2);
                    case JxqyDesktopKey.TrainingWindow:
                        return keyboard(KeyCode.F3);
                    case JxqyDesktopKey.InventoryWindow:
                        return keyboard(KeyCode.F5);
                    case JxqyDesktopKey.SkillsWindow:
                        return keyboard(KeyCode.F6);
                    case JxqyDesktopKey.MemoWindow:
                        return keyboard(KeyCode.F7);
                    case JxqyDesktopKey.LittleMap:
                        return keyboard(KeyCode.Tab);
                    case JxqyDesktopKey.MoveDirection0:
                        return keyboard(KeyCode.Keypad2);
                    case JxqyDesktopKey.MoveDirection1:
                        return keyboard(KeyCode.Keypad1);
                    case JxqyDesktopKey.MoveDirection2:
                        return keyboard(KeyCode.Keypad4);
                    case JxqyDesktopKey.MoveDirection3:
                        return keyboard(KeyCode.Keypad7);
                    case JxqyDesktopKey.MoveDirection4:
                        return keyboard(KeyCode.Keypad8);
                    case JxqyDesktopKey.MoveDirection5:
                        return keyboard(KeyCode.Keypad9);
                    case JxqyDesktopKey.MoveDirection6:
                        return keyboard(KeyCode.Keypad6);
                    case JxqyDesktopKey.MoveDirection7:
                        return keyboard(KeyCode.Keypad3);
                    case JxqyDesktopKey.ControlModifier:
                        return keyboard(KeyCode.LeftControl) ||
                               keyboard(KeyCode.RightControl);
                    case JxqyDesktopKey.FullscreenEnter:
                        return keyboard(KeyCode.Return) ||
                               keyboard(KeyCode.KeypadEnter);
                    default:
                        return false;
                }
            }
        }
    }
}
