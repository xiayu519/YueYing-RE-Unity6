using System;
using System.Collections.Generic;
using Jxqy.Domain.World;

namespace Jxqy.Domain.Input
{
    public enum JxqyInputIntentKind
    {
        Move,
        PointerMove,
        PointerPrimary,
        PointerSecondary,
        Interact,
        PrimaryAttack,
        UseSkill,
        UseItem,
        Menu,
        Confirm,
        Cancel,
        Meditate,
        Jump,
        ToggleStatus,
        ToggleEquipment,
        ToggleTraining,
        ToggleInventory,
        ToggleSkills,
        ToggleMemo,
        ToggleLittleMap,
        LegacyMoveForward,
        LegacyMoveDirection,
        LegacyTurnLeft,
        LegacyTurnRight,
        LegacyTurnBack,
        ForceAttack,
        ToggleFullscreen,
    }

    public enum JxqyInputPhase
    {
        Started,
        Performed,
        Canceled,
    }

    public readonly struct JxqyInputIntent
    {
        private JxqyInputIntent(
            long sequence,
            JxqyInputIntentKind kind,
            JxqyInputPhase phase,
            JxqyFloat2 value,
            int slot)
        {
            Sequence = sequence;
            Kind = kind;
            Phase = phase;
            Value = value;
            Slot = slot;
        }

        public long Sequence { get; }
        public JxqyInputIntentKind Kind { get; }
        public JxqyInputPhase Phase { get; }
        public JxqyFloat2 Value { get; }
        public int Slot { get; }

        public static JxqyInputIntent Move(
            long sequence,
            JxqyFloat2 direction,
            JxqyInputPhase phase = JxqyInputPhase.Performed)
        {
            JxqyFloat2 value = direction.LengthSquared > 1
                ? direction.Normalized
                : direction;
            return new JxqyInputIntent(
                sequence,
                JxqyInputIntentKind.Move,
                phase,
                value,
                -1);
        }

        public static JxqyInputIntent Pointer(
            long sequence,
            JxqyFloat2 logicalPosition,
            JxqyInputPhase phase = JxqyInputPhase.Performed)
        {
            return new JxqyInputIntent(
                sequence,
                JxqyInputIntentKind.PointerMove,
                phase,
                logicalPosition,
                -1);
        }

        public static JxqyInputIntent Action(
            long sequence,
            JxqyInputIntentKind kind,
            JxqyInputPhase phase = JxqyInputPhase.Started,
            int slot = -1,
            JxqyFloat2 pointer = default)
        {
            if (kind == JxqyInputIntentKind.Move ||
                kind == JxqyInputIntentKind.PointerMove)
                throw new ArgumentException(
                    "移动和指针意图必须使用专用工厂。",
                    nameof(kind));
            if (kind == JxqyInputIntentKind.UseSkill && slot < 0)
                throw new ArgumentOutOfRangeException(nameof(slot));
            return new JxqyInputIntent(
                sequence,
                kind,
                phase,
                pointer,
                slot);
        }
    }

    public sealed class JxqyInputIntentBuffer
    {
        private readonly List<JxqyInputIntent> _pending =
            new List<JxqyInputIntent>();
        private long _sequence;

        public int Count => _pending.Count;
        public long LastSequence => _sequence;

        public void SetMove(JxqyFloat2 direction)
        {
            Add(JxqyInputIntent.Move(Next(), direction));
        }

        public void SetPointer(JxqyFloat2 logicalPosition)
        {
            Add(JxqyInputIntent.Pointer(Next(), logicalPosition));
        }

        public void Press(
            JxqyInputIntentKind kind,
            int slot = -1,
            JxqyFloat2 pointer = default)
        {
            Add(JxqyInputIntent.Action(
                Next(),
                kind,
                JxqyInputPhase.Started,
                slot,
                pointer));
        }

        public void Release(
            JxqyInputIntentKind kind,
            int slot = -1,
            JxqyFloat2 pointer = default)
        {
            Add(JxqyInputIntent.Action(
                Next(),
                kind,
                JxqyInputPhase.Canceled,
                slot,
                pointer));
        }

        public IReadOnlyList<JxqyInputIntent> Drain()
        {
            if (_pending.Count == 0)
                return Array.Empty<JxqyInputIntent>();
            JxqyInputIntent[] result = _pending.ToArray();
            _pending.Clear();
            return result;
        }

        /// <summary>
        /// Drains into a caller-owned reusable list. Runtime input adapters use
        /// this overload so polling every frame does not allocate an array.
        /// </summary>
        public void Drain(List<JxqyInputIntent> destination)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            if (ReferenceEquals(destination, _pending))
                throw new ArgumentException(
                    "Destination cannot be the internal pending buffer.",
                    nameof(destination));
            destination.Clear();
            destination.AddRange(_pending);
            _pending.Clear();
        }

        public void ResetTransientState()
        {
            _pending.Clear();
            Add(JxqyInputIntent.Move(
                Next(),
                JxqyFloat2.Zero,
                JxqyInputPhase.Canceled));
            Add(JxqyInputIntent.Action(
                Next(),
                JxqyInputIntentKind.PointerPrimary,
                JxqyInputPhase.Canceled));
            Add(JxqyInputIntent.Action(
                Next(),
                JxqyInputIntentKind.PointerSecondary,
                JxqyInputPhase.Canceled));
        }

        private long Next()
        {
            return checked(++_sequence);
        }

        private void Add(JxqyInputIntent intent)
        {
            _pending.Add(intent);
        }
    }

    public sealed class JxqyGameplayIntentState
    {
        public JxqyFloat2 Move { get; private set; }
        public JxqyFloat2 Pointer { get; private set; }
        public bool PointerHeld { get; private set; }
        public int SelectedSkillSlot { get; private set; } = -1;
        public JxqyInputIntentKind? LastAction { get; private set; }

        public void Apply(IEnumerable<JxqyInputIntent> intents)
        {
            if (intents == null)
                throw new ArgumentNullException(nameof(intents));
            LastAction = null;
            foreach (JxqyInputIntent intent in intents)
            {
                switch (intent.Kind)
                {
                    case JxqyInputIntentKind.Move:
                        Move = intent.Phase == JxqyInputPhase.Canceled
                            ? JxqyFloat2.Zero
                            : intent.Value;
                        break;
                    case JxqyInputIntentKind.PointerMove:
                        Pointer = intent.Value;
                        break;
                    case JxqyInputIntentKind.PointerPrimary:
                    case JxqyInputIntentKind.PointerSecondary:
                        Pointer = intent.Value;
                        if (intent.Kind ==
                            JxqyInputIntentKind.PointerPrimary)
                        {
                            PointerHeld =
                                intent.Phase != JxqyInputPhase.Canceled;
                        }
                        if (intent.Phase == JxqyInputPhase.Started)
                            LastAction = intent.Kind;
                        break;
                    case JxqyInputIntentKind.UseSkill:
                        if (intent.Phase == JxqyInputPhase.Started)
                        {
                            SelectedSkillSlot = intent.Slot;
                            LastAction = intent.Kind;
                        }
                        break;
                    default:
                        if (intent.Phase == JxqyInputPhase.Started)
                            LastAction = intent.Kind;
                        break;
                }
            }
        }

        public void Reset()
        {
            Move = JxqyFloat2.Zero;
            Pointer = JxqyFloat2.Zero;
            PointerHeld = false;
            SelectedSkillSlot = -1;
            LastAction = null;
        }
    }
}
