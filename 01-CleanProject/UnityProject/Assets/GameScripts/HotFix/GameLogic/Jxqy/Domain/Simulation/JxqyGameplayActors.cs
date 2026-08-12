using System;
using System.Collections.Generic;
using Jxqy.Domain.World;

namespace Jxqy.Domain.Simulation
{
    public sealed class JxqyRangedMagicReference
    {
        public JxqyMagicDefinition Magic { get; set; }
        public int Distance { get; set; }
    }

    public enum JxqyCharacterState
    {
        Stand,
        Stand1,
        Walk,
        Run,
        Jump,
        Attack,
        Attack1,
        Attack2,
        Magic,
        Sit,
        Hurt,
        Death,
        FightStand,
        FightWalk,
        FightRun,
        FightJump,
    }

    public enum JxqyCharacterKind
    {
        Normal,
        Fighter,
        Player,
        Follower,
        GroundAnimal,
        Eventer,
        AfraidPlayerAnimal,
        Flyer,
    }

    public enum JxqyRelationType
    {
        Friend,
        Enemy,
        Neutral,
        None,
    }

    public class JxqySprite
    {
        private JxqyFloat2 _positionInWorld;
        private float _velocity;
        private int _directionCount = 8;
        private int _currentDirection;

        public JxqySprite(float velocity = JxqyCharacter.BaseSpeed)
        {
            Velocity = velocity;
        }

        public float Velocity
        {
            get => _velocity;
            set
            {
                if (value < 0 || float.IsNaN(value) || float.IsInfinity(value))
                    throw new ArgumentOutOfRangeException(nameof(value));
                _velocity = value;
            }
        }

        public JxqyFloat2 PositionInWorld
        {
            get => _positionInWorld;
            set
            {
                if (float.IsNaN(value.X) || float.IsInfinity(value.X) ||
                    float.IsNaN(value.Y) || float.IsInfinity(value.Y))
                    throw new ArgumentOutOfRangeException(nameof(value));
                _positionInWorld = value;
            }
        }

        public JxqyIntPoint TilePosition
        {
            get => JxqyIsometricMapMath.WorldPixelToTile(
                (int)_positionInWorld.X,
                (int)_positionInWorld.Y);
            set
            {
                JxqyIntPoint world =
                    JxqyIsometricMapMath.TileToWorldPixel(value.X, value.Y);
                PositionInWorld = new JxqyFloat2(world.X, world.Y);
            }
        }

        public int DirectionCount
        {
            get => _directionCount;
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException(nameof(value));
                _directionCount = value;
                CurrentDirection = _currentDirection;
            }
        }

        public int CurrentDirection
        {
            get => _currentDirection;
            set
            {
                int normalized = value % DirectionCount;
                _currentDirection = normalized < 0
                    ? normalized + DirectionCount
                    : normalized;
            }
        }

        public float MovedDistance { get; protected set; }

        public void SetDirection(JxqyFloat2 direction)
        {
            if (direction != JxqyFloat2.Zero)
                CurrentDirection =
                    JxqyDirection.GetIndex(direction, DirectionCount);
        }

        public virtual void Move(
            JxqyFloat2 direction,
            float elapsedSeconds,
            float speedScale = 1f)
        {
            if (elapsedSeconds < 0 || float.IsNaN(elapsedSeconds) ||
                float.IsInfinity(elapsedSeconds))
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            if (speedScale < 0 || float.IsNaN(speedScale) ||
                float.IsInfinity(speedScale))
                throw new ArgumentOutOfRangeException(nameof(speedScale));
            if (direction == JxqyFloat2.Zero || elapsedSeconds == 0)
                return;

            JxqyFloat2 normalized = direction.Normalized;
            SetDirection(normalized);
            JxqyFloat2 movement =
                normalized * (Velocity * elapsedSeconds * speedScale);
            PositionInWorld += movement;
            MovedDistance += movement.Length;
        }

        protected void ResetMovedDistance()
        {
            MovedDistance = 0;
        }
    }

    public partial class JxqyCharacter : JxqySprite
    {
        public const float BaseSpeed = 100f;
        public const int DefaultRunSpeedFold = 8;
        public const int MinimumMoveSpeedPercent = -90;

        private readonly List<JxqyFloat2> _path = new List<JxqyFloat2>();
        private int _nextPathIndex;
        private JxqyCharacterState _pathMovementState =
            JxqyCharacterState.Walk;
        private Func<JxqyIntPoint, bool> _canEnterJumpTile;
        private int _life;
        private int _lifeMax;
        private int _thew;
        private int _thewMax;
        private int _mana;
        private int _manaMax;
        private int _walkSpeed = 1;
        private JxqyCharacterState _state;
        private int _stateVersion;

        public JxqyCharacter()
            : base(BaseSpeed)
        {
        }

        public string Name { get; set; } = string.Empty;
        public string ScriptAddress { get; set; } = string.Empty;
        public string DeathScriptAddress { get; set; } = string.Empty;
        public string MagicFileName { get; set; } = string.Empty;
        public string MagicFileName2 { get; set; } = string.Empty;
        public JxqyMagicDefinition BasicMagic { get; set; }
        public JxqyMagicDefinition BasicMagic2 { get; set; }
        public string RetaliationMagicFileName { get; set; } =
            string.Empty;
        public List<JxqyRangedMagicReference> AdditionalBasicMagics
        {
            get;
        } = new List<JxqyRangedMagicReference>();
        public bool IsVisible { get; set; } = true;
        public JxqyCharacterKind Kind { get; set; }
        public JxqyRelationType Relation { get; set; }
        public JxqyCharacterState State
        {
            get => _state;
            private set
            {
                if (_state == value)
                    return;
                _state = value;
                _stateVersion = unchecked(_stateVersion + 1);
            }
        }

        /// <summary>
        /// Changes whenever the logical action changes. Presentation uses
        /// this to distinguish two consecutive attacks even when Stand was
        /// entered and left between visual ticks.
        /// </summary>
        public int StateVersion => _stateVersion;
        public bool IsInFighting { get; private set; }
        public bool IsPetrified { get; set; }
        public bool IsCurrentMagicUninterruptible { get; private set; }
        public bool IsInTransport { get; set; }
        public bool IsMovementDisabled { get; set; }
        public bool IsRunDisabled { get; set; }
        public bool IsJumpDisabled { get; set; }
        public bool IsFightDisabled { get; set; }
        public int AddMoveSpeedPercent { get; set; }
        public int ChangeMoveSpeedPercent { get; set; }
        public int RunSpeedFold { get; set; } = DefaultRunSpeedFold;

        public int WalkSpeed
        {
            get => _walkSpeed;
            set => _walkSpeed = Math.Max(1, value);
        }

        public int Life
        {
            get => _life;
            set => _life = Math.Max(0, Math.Min(value, LifeMax));
        }

        public int LifeMax
        {
            get => _lifeMax;
            set
            {
                _lifeMax = Math.Max(0, value);
                _life = Math.Min(_life, _lifeMax);
            }
        }

        public int Thew
        {
            get => _thew;
            set => _thew = Math.Max(0, Math.Min(value, ThewMax));
        }

        public int ThewMax
        {
            get => _thewMax;
            set
            {
                _thewMax = Math.Max(0, value);
                _thew = Math.Min(_thew, _thewMax);
            }
        }

        public int Mana
        {
            get => _mana;
            set => _mana = Math.Max(0, Math.Min(value, ManaMax));
        }

        public int ManaMax
        {
            get => _manaMax;
            set
            {
                _manaMax = Math.Max(0, value);
                _mana = Math.Min(_mana, _manaMax);
            }
        }

        public JxqyIntPoint DestinationTilePosition { get; private set; }
        public bool HasPath => _nextPathIndex < _path.Count;
        public IReadOnlyList<JxqyFloat2> Path => _path;
        public JxqyIntPoint NextPathTilePosition
        {
            get
            {
                if (!HasPath)
                    return TilePosition;
                JxqyFloat2 next = _path[_nextPathIndex];
                return JxqyIsometricMapMath.WorldPixelToTile(
                    (int)next.X,
                    (int)next.Y);
            }
        }

        public float MoveSpeedScale
        {
            get
            {
                int percent = Math.Max(
                    MinimumMoveSpeedPercent,
                    ChangeMoveSpeedPercent + AddMoveSpeedPercent);
                return 1f + percent / 100f;
            }
        }

        public bool IsStanding =>
            State == JxqyCharacterState.Stand ||
            State == JxqyCharacterState.Stand1 ||
            State == JxqyCharacterState.FightStand;

        public bool IsWalking =>
            State == JxqyCharacterState.Walk ||
            State == JxqyCharacterState.FightWalk;

        public bool IsRunning =>
            State == JxqyCharacterState.Run ||
            State == JxqyCharacterState.FightRun;

        public bool IsJumping =>
            State == JxqyCharacterState.Jump ||
            State == JxqyCharacterState.FightJump;

        public bool CanPerformAction =>
            !IsDead &&
            State != JxqyCharacterState.Jump &&
            State != JxqyCharacterState.Attack &&
            State != JxqyCharacterState.Attack1 &&
            State != JxqyCharacterState.Attack2 &&
            State != JxqyCharacterState.Magic &&
            State != JxqyCharacterState.Hurt &&
            State != JxqyCharacterState.Death &&
            State != JxqyCharacterState.FightJump &&
            !IsPetrified &&
            !IsInTransport;

        public void SetState(JxqyCharacterState state)
        {
            State = state;
            if (state != JxqyCharacterState.Magic)
                IsCurrentMagicUninterruptible = false;
            if (HasPath &&
                (IsWalking || IsRunning || IsJumping))
            {
                _pathMovementState = state;
            }
        }

        public void SetMagicState(bool cannotBeInterrupted)
        {
            State = JxqyCharacterState.Magic;
            IsCurrentMagicUninterruptible = cannotBeInterrupted;
        }

        public void SetFighting(bool fighting)
        {
            IsInFighting = fighting && !IsFightDisabled;
            if (IsStanding)
                State = IsInFighting
                    ? JxqyCharacterState.FightStand
                    : JxqyCharacterState.Stand;
        }

        public bool BeginPath(
            IEnumerable<JxqyFloat2> worldPath,
            bool run = false)
        {
            if (worldPath == null)
                throw new ArgumentNullException(nameof(worldPath));
            if (!CanPerformAction || run && IsRunDisabled)
                return false;

            _path.Clear();
            foreach (JxqyFloat2 point in worldPath)
                _path.Add(point);
            if (_path.Count == 0)
                return false;
            // A fresh click may arrive between tile centers. Start the new
            // route at the actor's exact position so consecutive clicks turn
            // immediately instead of walking back to the old tile center.
            if (_path[0] != PositionInWorld)
                _path[0] = PositionInWorld;
            while (_path.Count > 1 && _path[1] == _path[0])
                _path.RemoveAt(1);
            if (_path.Count < 2)
            {
                Stop();
                return false;
            }

            _nextPathIndex = 1;
            SetDirection(_path[_nextPathIndex] - PositionInWorld);
            JxqyFloat2 destination = _path[_path.Count - 1];
            DestinationTilePosition =
                JxqyIsometricMapMath.WorldPixelToTile(
                    (int)destination.X,
                    (int)destination.Y);
            State = run
                ? IsInFighting
                    ? JxqyCharacterState.FightRun
                    : JxqyCharacterState.Run
                : IsInFighting
                    ? JxqyCharacterState.FightWalk
                    : JxqyCharacterState.Walk;
            _pathMovementState = State;
            ResetMovedDistance();
            return true;
        }

        public bool BeginJump(
            JxqyFloat2 destination,
            Func<JxqyIntPoint, bool> canEnterTile = null)
        {
            if (!CanPerformAction || IsJumpDisabled ||
                destination == PositionInWorld)
            {
                return false;
            }

            JxqyIntPoint destinationTile =
                JxqyIsometricMapMath.WorldPixelToTile(
                    (int)destination.X,
                    (int)destination.Y);
            if (canEnterTile != null && !canEnterTile(destinationTile))
                return false;

            _path.Clear();
            _path.Add(PositionInWorld);
            _path.Add(destination);
            _nextPathIndex = 1;
            _canEnterJumpTile = canEnterTile;
            DestinationTilePosition = destinationTile;
            SetDirection(destination - PositionInWorld);
            State = IsInFighting
                ? JxqyCharacterState.FightJump
                : JxqyCharacterState.Jump;
            _pathMovementState = State;
            ResetMovedDistance();
            return true;
        }

        public bool ResumePathMovement()
        {
            if (!HasPath ||
                IsMovementDisabled ||
                IsPetrified ||
                IsInTransport)
            {
                return false;
            }

            State = _pathMovementState switch
            {
                JxqyCharacterState.Run or
                JxqyCharacterState.FightRun =>
                    IsInFighting
                        ? JxqyCharacterState.FightRun
                        : JxqyCharacterState.Run,
                JxqyCharacterState.Jump or
                JxqyCharacterState.FightJump =>
                    IsInFighting
                        ? JxqyCharacterState.FightJump
                        : JxqyCharacterState.Jump,
                _ => IsInFighting
                    ? JxqyCharacterState.FightWalk
                    : JxqyCharacterState.Walk,
            };
            return true;
        }

        public bool RetargetPath(
            IEnumerable<JxqyFloat2> pathFromNextWaypoint)
        {
            if (pathFromNextWaypoint == null)
            {
                throw new ArgumentNullException(
                    nameof(pathFromNextWaypoint));
            }
            if (!HasPath)
                return BeginPath(pathFromNextWaypoint, IsRunning);

            var replacement = new List<JxqyFloat2>();
            foreach (JxqyFloat2 point in pathFromNextWaypoint)
                replacement.Add(point);
            if (replacement.Count == 0)
                return false;

            JxqyFloat2 nextWaypoint = _path[_nextPathIndex];
            if (replacement[0] != nextWaypoint)
                return false;

            int tailStart = _nextPathIndex + 1;
            if (tailStart < _path.Count)
            {
                _path.RemoveRange(
                    tailStart,
                    _path.Count - tailStart);
            }
            for (int index = 1; index < replacement.Count; index++)
            {
                if (replacement[index] != _path[_path.Count - 1])
                    _path.Add(replacement[index]);
            }

            JxqyFloat2 destination = _path[_path.Count - 1];
            DestinationTilePosition =
                JxqyIsometricMapMath.WorldPixelToTile(
                    (int)destination.X,
                    (int)destination.Y);
            return true;
        }

        public void TickMovement(
            float elapsedSeconds,
            Func<JxqyCharacter, JxqyIntPoint, bool> isTileBlocked = null)
        {
            if (elapsedSeconds < 0 || float.IsNaN(elapsedSeconds) ||
                float.IsInfinity(elapsedSeconds))
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            if (!IsWalking && !IsRunning && !IsJumping)
                return;
            if (!HasPath)
            {
                // Original MoveAlongPath returns a pathless walk/run actor to
                // StandingImmediately on the next update. Save files retain
                // the action state but not the transient path, so this is
                // required for an NPC saved mid-route to resume its AI.
                if (IsWalking || IsRunning)
                    Stop();
                return;
            }
            if (IsMovementDisabled || IsPetrified)
                return;

            float speedFold = IsJumping
                ? DefaultRunSpeedFold
                : IsRunning
                    ? Math.Max(1, RunSpeedFold)
                    : WalkSpeed;
            float remaining =
                Velocity * elapsedSeconds * speedFold * MoveSpeedScale;
            while (remaining > 0 && HasPath)
            {
                JxqyFloat2 target = _path[_nextPathIndex];
                JxqyIntPoint targetTile =
                    JxqyIsometricMapMath.WorldPixelToTile(
                        (int)target.X,
                        (int)target.Y);
                if (!IsJumping &&
                    !targetTile.Equals(TilePosition) &&
                    isTileBlocked?.Invoke(this, targetTile) == true)
                {
                    // The original Character.MoveAlongPath rechecks
                    // HasObstacle(tileTo) at every step boundary. Paths may
                    // intentionally end on an occupied character tile, but
                    // the mover must stop on the preceding tile instead of
                    // sharing the target's logical tile.
                    StopMovementPreservingAction();
                    return;
                }
                JxqyFloat2 offset = target - PositionInWorld;
                float distance = offset.Length;
                if (distance <= float.Epsilon)
                {
                    PositionInWorld = target;
                    _nextPathIndex++;
                    ResetMovedDistance();
                    continue;
                }

                SetDirection(offset);
                float step = Math.Min(distance, remaining);
                JxqyFloat2 nextPosition =
                    PositionInWorld + offset.Normalized * step;
                if (IsJumping && _canEnterJumpTile != null)
                {
                    JxqyIntPoint nextTile =
                        JxqyIsometricMapMath.WorldPixelToTile(
                            (int)nextPosition.X,
                            (int)nextPosition.Y);
                    if (!_canEnterJumpTile(nextTile))
                    {
                        // The original keeps the jump action active until its
                        // animation ends, even when forward movement stops.
                        ClearMovement();
                        return;
                    }
                }
                PositionInWorld = nextPosition;
                MovedDistance += step;
                remaining -= step;
                if (step + 0.0001f < distance)
                    break;

                PositionInWorld = target;
                _nextPathIndex++;
                ResetMovedDistance();
            }

            if (!HasPath)
            {
                if (IsJumping)
                {
                    // Landing finishes movement, not the jump action. The
                    // presentation layer returns to Stand after the last
                    // animation frame, matching Character.JumpAlongPath.
                    ClearMovement();
                }
                else
                {
                    Stop();
                }
            }
        }

        public void Stop()
        {
            ClearMovement();
            if (IsDead)
                return;
            State = IsInFighting
                ? JxqyCharacterState.FightStand
                : JxqyCharacterState.Stand;
        }

        public void StopMovementPreservingAction()
        {
            ClearMovement();
            if (IsWalking || IsRunning || IsJumping)
            {
                State = IsInFighting
                    ? JxqyCharacterState.FightStand
                    : JxqyCharacterState.Stand;
            }
        }

        private void ClearMovement()
        {
            _path.Clear();
            _nextPathIndex = 0;
            _canEnterJumpTile = null;
            ResetMovedDistance();
        }
    }

    public sealed class JxqyPlayer : JxqyCharacter
    {
        private const float OriginalUpdateRate = 60f;
        private const float ThewRestoreIntervalSeconds = 1f;
        private const float ThewRestorePercent = 0.03f;
        private int _money;
        private float _runThewAccumulator;
        private float _thewRestoreAccumulator;
        private float _meditationAccumulator;

        public JxqyPlayer()
        {
            Kind = JxqyCharacterKind.Player;
        }

        public int Money
        {
            get => _money;
            set => _money = Math.Max(0, value);
        }

        public bool WalkIsRun { get; set; }
        public bool IsNotUseThewWhenRun { get; set; }
        public bool IsManaRestore { get; set; }
        public bool ManaLimit { get; set; }
        public int AddLifeRestorePercent { get; set; }
        public int AddThewRestorePercent { get; set; }
        public int AddManaRestorePercent { get; set; }

        public bool WantsToRun(
            bool runModifierHeld,
            bool useThewWhenNormalRun = false)
        {
            bool consumesThew = IsInFighting || useThewWhenNormalRun;
            return (WalkIsRun || runModifierHeld) &&
                   !IsRunDisabled &&
                   (IsNotUseThewWhenRun ||
                    !consumesThew ||
                    Thew > 0);
        }

        public bool TickThew(
            float elapsedSeconds,
            bool moving,
            bool running,
            bool useThewWhenNormalRun = false)
        {
            if (elapsedSeconds < 0 ||
                float.IsNaN(elapsedSeconds) ||
                float.IsInfinity(elapsedSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(elapsedSeconds));
            }

            int before = Thew;
            bool consumesRunThew =
                moving &&
                running &&
                !IsNotUseThewWhenRun &&
                (IsInFighting || useThewWhenNormalRun);
            if (consumesRunThew)
            {
                _thewRestoreAccumulator = 0f;
                _runThewAccumulator += elapsedSeconds *
                                      OriginalUpdateRate;
                int amount = (int)_runThewAccumulator;
                if (amount > 0)
                {
                    _runThewAccumulator -= amount;
                    Thew -= amount;
                }
            }
            else
            {
                _runThewAccumulator = 0f;
                bool canRestore =
                    !running &&
                    (IsStanding || IsWalking) &&
                    !HasStatus(JxqyStatusKind.Frozen) &&
                    !HasStatus(JxqyStatusKind.Poisoned) &&
                    !HasStatus(JxqyStatusKind.Petrified);
                if (canRestore)
                {
                    _thewRestoreAccumulator += elapsedSeconds;
                    while (_thewRestoreAccumulator >=
                           ThewRestoreIntervalSeconds)
                    {
                        _thewRestoreAccumulator -=
                            ThewRestoreIntervalSeconds;
                        Life += (int)(
                            LifeMax *
                            (0.01f +
                             AddLifeRestorePercent / 1000f));
                        Thew += (int)(
                            ThewMax *
                            (ThewRestorePercent +
                             AddThewRestorePercent / 1000f));
                        Mana += (int)(
                            ManaMax *
                            (AddManaRestorePercent / 1000f));
                        if (IsManaRestore)
                            Mana += (int)(ManaMax * 0.02f);
                    }
                }
                else
                {
                    _thewRestoreAccumulator = 0f;
                }
            }
            return Thew != before;
        }

        public void PrepareForRestore()
        {
            ResetDeathStateForRestore();
            Stop();
            _runThewAccumulator = 0f;
            _thewRestoreAccumulator = 0f;
            _meditationAccumulator = 0f;
        }

        public void RestoreActionState(
            JxqyCharacterState state,
            bool isInFighting)
        {
            if (!Enum.IsDefined(typeof(JxqyCharacterState), state))
                state = JxqyCharacterState.Stand;
            SetFighting(isInFighting);
            SetState(state);
        }

        public bool ToggleMeditation()
        {
            if (State == JxqyCharacterState.Sit)
            {
                _meditationAccumulator = 0f;
                Stop();
                return false;
            }
            if (!CanPerformAction)
                return false;
            Stop();
            SetState(JxqyCharacterState.Sit);
            _meditationAccumulator = 0f;
            return true;
        }

        public bool TickMeditation(float elapsedSeconds)
        {
            if (elapsedSeconds < 0 ||
                float.IsNaN(elapsedSeconds) ||
                float.IsInfinity(elapsedSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(elapsedSeconds));
            }
            if (State != JxqyCharacterState.Sit)
            {
                _meditationAccumulator = 0f;
                return false;
            }

            int amount = Math.Max(1, ManaMax / 100);
            if (Mana >= ManaMax || Thew <= amount)
            {
                _meditationAccumulator = 0f;
                Stop();
                return true;
            }

            int manaBefore = Mana;
            int thewBefore = Thew;
            _meditationAccumulator += elapsedSeconds;
            const float intervalSeconds = 0.15f;
            while (_meditationAccumulator >= intervalSeconds &&
                   Mana < ManaMax &&
                   Thew > amount)
            {
                _meditationAccumulator -= intervalSeconds;
                Thew -= amount;
                Mana += amount;
            }
            return Mana != manaBefore || Thew != thewBefore;
        }

        public void AddMoney(int amount)
        {
            Money += amount;
        }

        public void ApplyMagicLevelBonuses(
            JxqyMagicDefinition magic)
        {
            if (magic == null)
                return;
            LifeMax += magic.LifeMax;
            ThewMax += magic.ThewMax;
            ManaMax += magic.ManaMax;
            Attack += magic.Attack;
            Attack2 += magic.Attack2;
            Attack3 += magic.Attack3;
            Defend += magic.Defend;
            Defend2 += magic.Defend2;
            Defend3 += magic.Defend3;
            Evade += magic.Evade;
            AddLifeRestorePercent +=
                magic.AddLifeRestorePercent;
            AddThewRestorePercent +=
                magic.AddThewRestorePercent;
            AddManaRestorePercent +=
                magic.AddManaRestorePercent;
        }
    }
}
