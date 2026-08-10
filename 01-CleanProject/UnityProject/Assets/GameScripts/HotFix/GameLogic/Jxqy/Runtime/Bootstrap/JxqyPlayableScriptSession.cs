using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jxqy.Domain.Content;
using Jxqy.Domain.Persistence;
using Jxqy.Domain.Presentation;
using Jxqy.Domain.Scripting;
using Jxqy.Domain.Simulation;
using Jxqy.Domain.World;
using Jxqy.Ports;
using Jxqy.UnityAdapters;
using UnityEngine;

namespace Jxqy.Bootstrap
{
    internal sealed class JxqyPlayableScriptBindings
    {
        public Func<string> GetActiveMapName { get; set; }
        public Func<JxqyPlayer> GetPlayer { get; set; }
        public Func<JxqyNpcManager> GetNpcs { get; set; }
        public Func<JxqyObjectManager> GetObjects { get; set; }
        public Func<JxqyInventory> GetInventory { get; set; }
        public Func<JxqySkillManager> GetSkills { get; set; }
        public Func<IJxqyTileCollisionMap> GetCollisionMap { get; set; }
        public Func<UniTask> LoadNewGameAsync { get; set; }
        public Func<JxqyCharacter, int, string, UniTask>
            SetCharacterActionFileAsync { get; set; }
        public Func<string, UniTask> PlayPlayerSpecialActionAsync { get; set; }
        public Func<string, UniTask> LoadMapAsync { get; set; }
        public Func<string, UniTask> LoadNpcAsync { get; set; }
        public Func<IReadOnlyList<string>, UniTask> LoadOneNpcAsync
        {
            get;
            set;
        }
        public Func<string, UniTask> LoadObjAsync { get; set; }
        public Func<string, int, int, int, UniTask> AddNpcAsync
        {
            get;
            set;
        }
        public Func<string, int, int, int, UniTask> AddObjAsync
        {
            get;
            set;
        }
        public Action<string> DeleteNpc { get; set; }
        public Action<string> DeleteObj { get; set; }
        public Action<JxqyWorldObject> DeleteObjectInstance { get; set; }
        public Action ClearBodies { get; set; }
        public Action<string> SaveNpcSnapshot { get; set; }
        public Action<string> SaveObjectSnapshot { get; set; }
        public Func<string, UniTask<JxqyItemDefinition>>
            LoadItemDefinitionAsync { get; set; }
        public Func<string, UniTask<JxqyItemDefinition>>
            LoadRandomItemDefinitionAsync { get; set; }
        public Func<string, UniTask<JxqyMagicDefinition>>
            LoadMagicDefinitionAsync { get; set; }
        public Func<string, int, UniTask<JxqyMagicDefinition>>
            LoadMagicDefinitionAtLevelAsync { get; set; }
        public Func<string, UniTask> MergeNpcAsync { get; set; }
        public Func<JxqyCharacter, string, UniTask>
            SetCharacterResourceAsync { get; set; }
        public Func<JxqyCharacter, string, bool, UniTask>
            PlayCharacterSpecialActionAsync { get; set; }
        public Func<string, bool, UniTask> OpenShopAsync { get; set; }
        public Func<int, UniTask> ChangePlayerAsync { get; set; }
        public Action<bool> SetInputDisabled { get; set; }
        public Action<int, int> SetMapPosition { get; set; }
        public Action<string, int, string> SetNamedMapTrap { get; set; }
        public Action SaveMapTrapSnapshot { get; set; }
        public Action FreeMap { get; set; }
        public Action<int> OpenTimeLimit { get; set; }
        public Action CloseTimeLimit { get; set; }
        public Action HideTimerWindow { get; set; }
        public Action<int, string> SetTimeScript { get; set; }
        public Func<int, string> GetTalkText { get; set; }
        public Action<string> ShowMessage { get; set; }
        public Action<string, int> ShowSystemMessage { get; set; }
        public Action CenterCameraOnPlayer { get; set; }
        public Action HandleScriptedPlayerPositionSet { get; set; }
        public Action<JxqySprite> RefreshActorVisual { get; set; }
        public Func<int, int, IReadOnlyList<JxqyLegacyTalkLine>>
            GetTalkLines { get; set; }
        public Action<int, string> SetMapTrap { get; set; }
        public Action<int> AddMemo { get; set; }
        public Action<string> AddMemoText { get; set; }
        public Action<string> DeleteMemo { get; set; }
        public Action<int, int> EquipGoods { get; set; }
        public Func<int, UniTask> AddPlayerExperienceAsync { get; set; }
        public Func<string, UniTask> SetLevelFileAsync { get; set; }
        public Func<int, UniTask> SetPlayerLevelAsync { get; set; }
        public Action<JxqyNpc, int> SetNpcLevel { get; set; }
        public Action<bool> SetSaveDisabled { get; set; }
        public Func<UniTask> ClearAllSavesAsync { get; set; }
        public Action<bool> SetDropDisabled { get; set; }
        public Action<bool> SetShowMapPosition { get; set; }
        public Action StopSounds { get; set; }
        public Action<string, int, int> UsePlayerMagic { get; set; }
        public Func<JxqyCharacter, JxqyFloat2, bool> PerformNpcAttack
        {
            get;
            set;
        }
        public Func<JxqyCharacter, JxqyFloat2, bool> PerformNpcMagic
        {
            get;
            set;
        }
        public Action ReturnToTitle { get; set; }
    }

    internal sealed class JxqyLegacyTalkLine
    {
        public int Index { get; set; }
        public int PortraitIndex { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    /// <summary>
    /// Executes the converted legacy scripts against the live Unity world.
    /// RunScript keeps the caller suspended until its child script completes,
    /// matching the original engine's nested script behavior.
    /// </summary>
    internal sealed class JxqyPlayableScriptSession : IDisposable,
        IJxqyLegacyScriptCommandPort
    {
        internal const string ScriptCatalogAddress =
            "jxqy/manifests/script-catalog.json";
        internal const string PortraitCatalogAddress =
            "jxqy/text/ini/ui/dialog/headfile.ini/content.txt";

        private readonly IJxqyResourcePort _resources;
        private readonly JxqyResourceScope _scope;
        private readonly JxqyUiSession _ui;
        private readonly JxqyPresentationScriptCommandPort _presentation;
        private readonly JxqyPlayableScriptBindings _bindings;
        private readonly JxqyScriptVariableStore _variables = new();
        private readonly JxqyDeterministicRandom _random =
            new JxqyDeterministicRandom(20260727);
        private readonly Dictionary<int, string> _portraits = new();
        private readonly HashSet<int> _missingPortraitWarnings = new();
        private readonly HashSet<string> _missingScriptWarnings = new(
            StringComparer.OrdinalIgnoreCase);
        private readonly List<ParallelInvocation> _parallelInvocations =
            new List<ParallelInvocation>();
        private readonly Queue<QueuedSerialScript> _queuedSerialScripts =
            new Queue<QueuedSerialScript>();
        private JxqyScriptPathResolver _resolver;
        private JxqyScriptCommandRegistry _registry;
        private ScriptInvocation _root;
        private Action<string> _dialogueCompleted;
        private CancellationToken _runCancellationToken;
        private bool _disposed;

        public JxqyPlayableScriptSession(
            IJxqyResourcePort resources,
            JxqyResourceScope scope,
            JxqyUiSession ui,
            JxqyPresentationScriptCommandPort presentation,
            JxqyPlayableScriptBindings bindings)
        {
            _resources = resources ??
                         throw new ArgumentNullException(nameof(resources));
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
            _ui = ui ?? throw new ArgumentNullException(nameof(ui));
            _presentation = presentation ??
                            throw new ArgumentNullException(
                                nameof(presentation));
            _bindings = bindings ??
                        throw new ArgumentNullException(nameof(bindings));
        }

        public bool IsRunning =>
            (_root != null && !_root.IsFinished) ||
            _queuedSerialScripts.Count > 0;
        public bool IsFaulted =>
            _root?.Runner?.State == JxqyScriptRunnerState.Faulted ||
            _root?.LoadException != null;
        public IReadOnlyList<JxqyScriptDiagnostic> Diagnostics =>
            _root?.Runner?.Diagnostics ??
            Array.Empty<JxqyScriptDiagnostic>();
        public JxqyScriptVariableStore Variables => _variables;

        public IReadOnlyList<JxqySaveParallelScript>
            CaptureParallelScripts()
        {
            return _parallelInvocations
                .Where(item => !item.IsFinished &&
                               !string.IsNullOrWhiteSpace(item.FileName))
                .Select(item => new JxqySaveParallelScript
                {
                    FileName = item.FileName,
                    RemainingDelayMilliseconds =
                        (int)Math.Max(
                            0,
                            Math.Ceiling(item.RemainingDelayMilliseconds)),
                })
                .ToArray();
        }

        public void RestoreParallelScripts(
            IEnumerable<JxqySaveParallelScript> scripts)
        {
            _parallelInvocations.Clear();
            foreach (JxqySaveParallelScript script in
                     scripts ?? Array.Empty<JxqySaveParallelScript>())
            {
                if (script == null ||
                    string.IsNullOrWhiteSpace(script.FileName))
                {
                    continue;
                }
                StartParallel(
                    script.FileName,
                    Math.Max(0, script.RemainingDelayMilliseconds));
            }
        }

        public async UniTask InitializeAsync(
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            using JxqyAssetLease<TextAsset> lease =
                await _resources.LoadAsync<TextAsset>(
                    ScriptCatalogAddress,
                    _scope,
                    cancellationToken);
            JxqyScriptCatalog catalog =
                JsonUtility.FromJson<JxqyScriptCatalog>(lease.Asset.text);
            if (catalog == null || catalog.Errors == null ||
                catalog.Errors.Count != 0)
            {
                throw new InvalidOperationException(
                    "Jxqy script catalog is invalid.");
            }
            _resolver = new JxqyScriptPathResolver(catalog.Entries);
            using JxqyAssetLease<TextAsset> portraitLease =
                await _resources.LoadAsync<TextAsset>(
                    PortraitCatalogAddress,
                    _scope,
                    cancellationToken);
            ParsePortraitCatalog(portraitLease.Asset.text);
            _registry = JxqyLegacyScriptCommands.CreateRegistry(
                this,
                _variables,
                _random);
        }

        public async UniTask StartAsync(
            string fileName,
            CancellationToken cancellationToken,
            bool resetVariables = false,
            object belongObject = null,
            JxqyScriptCategory category = JxqyScriptCategory.Normal)
        {
            ThrowIfDisposed();
            if (_resolver == null || _registry == null)
                throw new InvalidOperationException(
                    "Jxqy script session has not been initialized.");
            CancelDialogueWait();
            if (resetVariables)
                _variables.Clear();
            _runCancellationToken = cancellationToken;
            _root = new ScriptInvocation(
                this,
                null,
                belongObject,
                category);
            await _root.LoadAsync(fileName, cancellationToken);
        }

        public void Tick(double elapsedMilliseconds)
        {
            ThrowIfDisposed();
            _root?.Tick(elapsedMilliseconds);
            TryStartQueuedSerialScript();
            for (int index = _parallelInvocations.Count - 1;
                 index >= 0;
                 index--)
            {
                ParallelInvocation invocation =
                    _parallelInvocations[index];
                invocation.Tick(elapsedMilliseconds);
                if (invocation.IsFinished)
                    _parallelInvocations.RemoveAt(index);
            }
        }

        public void Cancel()
        {
            ThrowIfDisposed();
            CancelDialogueWait();
            _root = null;
            _queuedSerialScripts.Clear();
            _parallelInvocations.Clear();
        }

        public void QueueSerialScript(
            string fileName,
            CancellationToken cancellationToken,
            object belongObject = null)
        {
            ThrowIfDisposed();
            if (_resolver == null || _registry == null)
                throw new InvalidOperationException(
                    "Jxqy script session has not been initialized.");
            if (string.IsNullOrWhiteSpace(fileName))
                return;
            _queuedSerialScripts.Enqueue(
                new QueuedSerialScript(
                    fileName,
                    cancellationToken,
                    belongObject));
            TryStartQueuedSerialScript();
        }

        public void StartParallel(
            string fileName,
            double delayMilliseconds = 0,
            object belongObject = null)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(fileName))
                return;
            var parallel = new ParallelInvocation(
                this,
                belongObject,
                Math.Max(0, delayMilliseconds));
            _parallelInvocations.Add(parallel);
            parallel.LoadAsync(
                    fileName,
                    _runCancellationToken)
                .Forget();
        }

        public JxqyScriptStep Execute(
            JxqyScriptContext context,
            JxqyScriptInstruction instruction)
        {
            ThrowIfDisposed();
            if (instruction == null)
                throw new ArgumentNullException(nameof(instruction));

            switch (instruction.Name.ToLowerInvariant())
            {
                case "loadgame":
                    return LoadGame(instruction);
                case "loadmap":
                    return WaitFor(
                        Required(_bindings.LoadMapAsync)(
                            Parameter(instruction, 0)));
                case "loadnpc":
                    return WaitFor(
                        Required(_bindings.LoadNpcAsync)(
                            Parameter(instruction, 0)));
                case "loadonenpc":
                    return WaitFor(
                        Required(_bindings.LoadOneNpcAsync)(
                            instruction.Parameters));
                case "mergenpc":
                    return WaitFor(
                        Required(_bindings.MergeNpcAsync)(
                            Parameter(instruction, 0)));
                case "loadobj":
                    return WaitFor(
                        Required(_bindings.LoadObjAsync)(
                            Parameter(instruction, 0)));
                case "addnpc":
                    return WaitFor(
                        Required(_bindings.AddNpcAsync)(
                            Parameter(instruction, 0),
                            Integer(instruction, 1),
                            Integer(instruction, 2),
                            Integer(instruction, 3)));
                case "addobj":
                    if (instruction.Parameters.Count != 3 &&
                        instruction.Parameters.Count != 4)
                    {
                        throw new InvalidOperationException(
                            "AddObj expects three or four parameters.");
                    }
                    return WaitFor(
                        Required(_bindings.AddObjAsync)(
                            Parameter(instruction, 0),
                            Integer(instruction, 1),
                            Integer(instruction, 2),
                            instruction.Parameters.Count == 4
                                ? Integer(instruction, 3)
                                : 0));
                case "delnpc":
                    Required(_bindings.DeleteNpc)(
                        Parameter(instruction, 0));
                    return JxqyScriptStep.Continue();
                case "delobj":
                    Required(_bindings.DeleteObj)(
                        Parameter(instruction, 0));
                    return JxqyScriptStep.Continue();
                case "saveobj":
                    Required(_bindings.SaveObjectSnapshot)(
                        OptionalParameter(instruction, 0));
                    return JxqyScriptStep.Continue();
                case "savenpc":
                    Required(_bindings.SaveNpcSnapshot)(
                        OptionalParameter(instruction, 0));
                    return JxqyScriptStep.Continue();
                case "clearbody":
                    Required(_bindings.ClearBodies)();
                    return JxqyScriptStep.Continue();
                case "delcurobj":
                {
                    var target =
                        GetBelongObject(context) as JxqyWorldObject;
                    if (target != null)
                        Required(_bindings.DeleteObjectInstance)(target);
                    return JxqyScriptStep.Continue();
                }
                case "setnpcdir":
                    SetNpcDirection(context, instruction);
                    return JxqyScriptStep.Continue();
                case "setnpcpos":
                    SetNpcPosition(context, instruction);
                    return JxqyScriptStep.Continue();
                case "setnpckind":
                    SetNpcKind(context, instruction);
                    return JxqyScriptStep.Continue();
                case "setnpcrelation":
                    SetNpcRelation(context, instruction);
                    return JxqyScriptStep.Continue();
                case "setnpcscript":
                    SetCharacterScript(context, instruction, false);
                    return JxqyScriptStep.Continue();
                case "setallnpcscript":
                    SetAllNpcScript(instruction, false);
                    return JxqyScriptStep.Continue();
                case "setnpcdeathscript":
                    SetCharacterScript(context, instruction, true);
                    return JxqyScriptStep.Continue();
                case "setallnpcdeathscript":
                    SetAllNpcScript(instruction, true);
                    return JxqyScriptStep.Continue();
                case "setnpcres":
                    return SetNpcResourceAsync(context, instruction);
                case "setnpcmagicfile":
                    return SetNpcMagicAsync(context, instruction);
                case "changeflyini":
                    return SetCharacterMagicAsync(
                        Npcs.FindAll(Parameter(instruction, 0))
                            .Cast<JxqyCharacter>()
                            .ToArray(),
                        Parameter(instruction, 1),
                        secondary: false);
                case "changeflyini2":
                    return SetCharacterMagicAsync(
                        Npcs.FindAll(Parameter(instruction, 0))
                            .Cast<JxqyCharacter>()
                            .ToArray(),
                        Parameter(instruction, 1),
                        secondary: true);
                case "addflyinis":
                    return AddNpcRangedMagicAsync(instruction);
                case "setnpcdestination":
                    SetNpcDestination(instruction);
                    return JxqyScriptStep.Continue();
                case "setkeepattack":
                    SetKeepAttack(instruction);
                    return JxqyScriptStep.Continue();
                case "addnpcproperty":
                    AddNpcProperty(instruction);
                    return JxqyScriptStep.Continue();
                case "setplayermagictousewhenbeattacked":
                    return SetRetaliationMagicAsync(
                        new[] { Player },
                        Parameter(instruction, 0),
                        Integer(instruction, 1));
                case "setnpcmagictousewhenbeattacked":
                    return SetRetaliationMagicAsync(
                        Npcs.FindAll(Parameter(instruction, 0))
                            .Cast<JxqyCharacter>()
                            .ToArray(),
                        Parameter(instruction, 1),
                        Integer(instruction, 2));
                case "setnpcaction":
                    return SetNpcAction(instruction);
                case "setnpcactiontype":
                    SetNpcActionType(context, instruction);
                    return JxqyScriptStep.Continue();
                case "setobjofs":
                    SetObjectOffset(instruction);
                    return JxqyScriptStep.Continue();
                case "setobjscript":
                    SetObjectScript(context, instruction);
                    return JxqyScriptStep.Continue();
                case "openbox":
                    SetObjectOpen(context, instruction, true);
                    return JxqyScriptStep.Continue();
                case "openobj":
                    SetObjectOpen(context, instruction, true);
                    return JxqyScriptStep.Continue();
                case "closebox":
                    SetObjectOpen(context, instruction, false);
                    return JxqyScriptStep.Continue();
                case "shownpc":
                    ShowNpc(instruction);
                    return JxqyScriptStep.Continue();
                case "enablenpcai":
                    Npcs.IsAiDisabled = false;
                    return JxqyScriptStep.Continue();
                case "disablenpcai":
                    Npcs.IsAiDisabled = true;
                    return JxqyScriptStep.Continue();
                case "npcgoto":
                    return NpcGoto(context, instruction);
                case "npcgotoex":
                    ResolveCharacterAndPair(
                        context,
                        instruction,
                        out JxqyCharacter gotoExTarget,
                        out int gotoExX,
                        out int gotoExY);
                    if (gotoExTarget == null)
                        return JxqyScriptStep.Continue();
                    return MoveCharacter(
                        gotoExTarget,
                        new JxqyIntPoint(gotoExX, gotoExY),
                        run: false,
                        wait: false);
                case "npcgotodir":
                    ResolveCharacterAndPair(
                        context,
                        instruction,
                        out JxqyCharacter gotoDirTarget,
                        out int gotoDirection,
                        out int gotoSteps);
                    if (gotoDirTarget == null)
                        return JxqyScriptStep.Continue();
                    return MoveInDirection(
                        gotoDirTarget,
                        gotoDirection,
                        gotoSteps,
                        run: false,
                        wait: true);
                case "npcattack":
                    NpcAttack(context, instruction);
                    return JxqyScriptStep.Continue();
                case "follownpc":
                    FollowNpc(context, instruction);
                    return JxqyScriptStep.Continue();
                case "watch":
                    Watch(instruction);
                    return JxqyScriptStep.Continue();
                case "runscript":
                    return RunScript(context, instruction);
                case "runparallelscript":
                    RunParallelScript(context, instruction);
                    return JxqyScriptStep.Continue();
                case "randrun":
                    return RunRandomScript(context, instruction);
                case "say":
                    return Say(instruction);
                case "talk":
                    return Talk(instruction);
                case "choose":
                    return Choose(instruction);
                case "chooseex":
                    return ChooseEx(instruction);
                case "choosemultiple":
                    return ChooseMultiple(instruction);
                case "select":
                    return Select(instruction);
                case "setplayerdir":
                    Player.CurrentDirection = Integer(instruction, 0);
                    return JxqyScriptStep.Continue();
                case "setplayerpos":
                    SetPlayerPosition(instruction);
                    return JxqyScriptStep.Continue();
                case "setplayerstate":
                    SetPlayerState(instruction);
                    return JxqyScriptStep.Continue();
                case "setplayerscn":
                    Required(_bindings.CenterCameraOnPlayer)();
                    return JxqyScriptStep.Continue();
                case "setmappos":
                    Required(_bindings.SetMapPosition)(
                        Integer(instruction, 0),
                        Integer(instruction, 1));
                    return JxqyScriptStep.Continue();
                case "disableinput":
                    Required(_bindings.SetInputDisabled)(true);
                    return JxqyScriptStep.Continue();
                case "enableinput":
                    Required(_bindings.SetInputDisabled)(false);
                    return JxqyScriptStep.Continue();
                case "setmaptrap":
                    Required(_bindings.SetMapTrap)(
                        Integer(instruction, 0),
                        Parameter(instruction, 1));
                    return JxqyScriptStep.Continue();
                case "settrap":
                    Required(_bindings.SetNamedMapTrap)(
                        Parameter(instruction, 0),
                        Integer(instruction, 1),
                        Parameter(instruction, 2));
                    return JxqyScriptStep.Continue();
                case "savemaptrap":
                    Required(_bindings.SaveMapTrapSnapshot)();
                    return JxqyScriptStep.Continue();
                case "freemap":
                    Required(_bindings.FreeMap)();
                    return JxqyScriptStep.Continue();
                case "playergoto":
                    return PlayerGoto(instruction);
                case "playergotoex":
                    return MoveCharacter(
                        Player,
                        new JxqyIntPoint(
                            Integer(instruction, 0),
                            Integer(instruction, 1)),
                        run: false,
                        wait: false);
                case "playergotodir":
                    return MoveInDirection(
                        Player,
                        Integer(instruction, 0),
                        Integer(instruction, 1),
                        run: false,
                        wait: true);
                case "playerrunto":
                    return MoveCharacter(
                        Player,
                        new JxqyIntPoint(
                            Integer(instruction, 0),
                            Integer(instruction, 1)),
                        run: true,
                        wait: true);
                case "playerruntoex":
                    return MoveCharacter(
                        Player,
                        new JxqyIntPoint(
                            Integer(instruction, 0),
                            Integer(instruction, 1)),
                        run: true,
                        wait: false);
                case "playerjumpto":
                    return JumpCharacter(
                        Player,
                        new JxqyIntPoint(
                            Integer(instruction, 0),
                            Integer(instruction, 1)),
                        wait: true);
                case "fulllife":
                    if (Player.IsDead)
                        Player.Revive();
                    else
                        Player.Life = Player.LifeMax;
                    return JxqyScriptStep.Continue();
                case "fullthew":
                    Player.Thew = Player.ThewMax;
                    return JxqyScriptStep.Continue();
                case "fullmana":
                    Player.Mana = Player.ManaMax;
                    return JxqyScriptStep.Continue();
                case "addlife":
                    Player.AddLife(Integer(instruction, 0));
                    return JxqyScriptStep.Continue();
                case "addthew":
                    Player.Thew += Integer(instruction, 0);
                    return JxqyScriptStep.Continue();
                case "addmana":
                    Player.Mana += Integer(instruction, 0);
                    return JxqyScriptStep.Continue();
                case "enablerun":
                    Player.IsRunDisabled = false;
                    return JxqyScriptStep.Continue();
                case "enablejump":
                    Player.IsJumpDisabled = false;
                    return JxqyScriptStep.Continue();
                case "enablefight":
                    Player.IsFightDisabled = false;
                    return JxqyScriptStep.Continue();
                case "disablerun":
                    Player.IsRunDisabled = true;
                    return JxqyScriptStep.Continue();
                case "disablejump":
                    Player.IsJumpDisabled = true;
                    return JxqyScriptStep.Continue();
                case "disablefight":
                    Player.IsFightDisabled = true;
                    return JxqyScriptStep.Continue();
                case "addattack":
                    AddAttack(
                        Player,
                        Integer(instruction, 0),
                        instruction.Parameters.Count > 1
                            ? Integer(instruction, 1)
                            : 1);
                    return JxqyScriptStep.Continue();
                case "adddefend":
                    AddDefend(
                        Player,
                        Integer(instruction, 0),
                        instruction.Parameters.Count > 1
                            ? Integer(instruction, 1)
                            : 1);
                    return JxqyScriptStep.Continue();
                case "addevade":
                    Player.Evade = Math.Max(
                        0,
                        Player.Evade + Integer(instruction, 0));
                    return JxqyScriptStep.Continue();
                case "addlifemax":
                    Player.LifeMax = Math.Max(
                        1,
                        Player.LifeMax + Integer(instruction, 0));
                    return JxqyScriptStep.Continue();
                case "addthewmax":
                    Player.ThewMax = Math.Max(
                        1,
                        Player.ThewMax + Integer(instruction, 0));
                    return JxqyScriptStep.Continue();
                case "addmanamax":
                    Player.ManaMax = Math.Max(
                        1,
                        Player.ManaMax + Integer(instruction, 0));
                    return JxqyScriptStep.Continue();
                case "addmovespeedpercent":
                    Player.AddMoveSpeedPercent += Integer(instruction, 0);
                    return JxqyScriptStep.Continue();
                case "addmoney":
                    AddMoney(Integer(instruction, 0));
                    return JxqyScriptStep.Continue();
                case "addrandmoney":
                    AddMoney(_random.Next(
                        Integer(instruction, 0),
                        checked(Integer(instruction, 1) + 1)));
                    return JxqyScriptStep.Continue();
                case "addexp":
                    return WaitFor(
                        Required(_bindings.AddPlayerExperienceAsync)(
                            Integer(instruction, 0)));
                case "getexp":
                    _variables.Set(
                        Parameter(instruction, 0),
                        Player.Experience);
                    return JxqyScriptStep.Continue();
                case "addgoods":
                    return AddGoodsAsync(
                        Required(_bindings.LoadItemDefinitionAsync)(
                            Parameter(instruction, 0)));
                case "addrandgoods":
                    return AddGoodsAsync(
                        Required(_bindings.LoadRandomItemDefinitionAsync)(
                            Parameter(instruction, 0)));
                case "delgoods":
                    DeleteGoods(context, instruction);
                    return JxqyScriptStep.Continue();
                case "getnpccount":
                    GetNpcCount(instruction);
                    return JxqyScriptStep.Continue();
                case "getgoodsnum":
                    _variables.Set(
                        "$GoodsNum",
                        Inventory.Count(Parameter(instruction, 0)));
                    return JxqyScriptStep.Continue();
                case "getgoodsnumbyname":
                    _variables.Set(
                        "$GoodsNum",
                        GetGoodsCountByName(Parameter(instruction, 0)));
                    return JxqyScriptStep.Continue();
                case "delgoodbyname":
                    DeleteGoodsByName(instruction);
                    return JxqyScriptStep.Continue();
                case "cleargoods":
                    bool hadGoods = Inventory.Entries.Count > 0;
                    Inventory.Clear();
                    if (hadGoods)
                        _ui.NotifyInventoryChanged();
                    return JxqyScriptStep.Continue();
                case "addmagic":
                    return LearnMagicAsync(
                        Required(_bindings.LoadMagicDefinitionAsync)(
                            Parameter(instruction, 0)));
                case "setmagiclevel":
                    SetMagicLevel(instruction);
                    return JxqyScriptStep.Continue();
                case "getplayermagiclevel":
                    _variables.Set(
                        Parameter(instruction, 1),
                        FindSkill(Parameter(instruction, 0))?.Level ?? 0);
                    return JxqyScriptStep.Continue();
                case "delmagic":
                {
                    JxqySkillEntry skill =
                        FindSkill(Parameter(instruction, 0));
                    if (skill != null)
                        Skills.Forget(skill.Magic.Id);
                    return JxqyScriptStep.Continue();
                }
                case "clearmagic":
                    Skills.Clear();
                    return JxqyScriptStep.Continue();
                case "limitmana":
                    Player.ManaLimit = Integer(instruction, 0) != 0;
                    return JxqyScriptStep.Continue();
                case "buygoods":
                case "sellgoods":
                    return WaitFor(
                        Required(_bindings.OpenShopAsync)(
                            OptionalParameter(instruction, 0),
                            true));
                case "buygoodsonly":
                    return WaitFor(
                        Required(_bindings.OpenShopAsync)(
                            OptionalParameter(instruction, 0),
                            false));
                case "getpartneridx":
                    GetPartnerIndex(instruction);
                    return JxqyScriptStep.Continue();
                case "setmoneynum":
                    Player.Money = Integer(instruction, 0);
                    return JxqyScriptStep.Continue();
                case "getmoneynum":
                    _variables.Set(
                        instruction.Parameters.Count == 0
                            ? "$MoneyNum"
                            : Parameter(instruction, 0),
                        Player.Money);
                    return JxqyScriptStep.Continue();
                case "setnpcactionfile":
                    return SetNpcActionFileAsync(instruction);
                case "npcspecialaction":
                    return PlayNpcSpecialActionAsync(
                        context,
                        instruction,
                        wait: false);
                case "npcspecialactionex":
                    return PlayNpcSpecialActionAsync(
                        context,
                        instruction,
                        wait: true);
                case "addtomemo":
                    Required(_bindings.AddMemo)(Integer(instruction, 0));
                    return JxqyScriptStep.Continue();
                case "memo":
                    Required(_bindings.AddMemoText)(
                        Parameter(instruction, 0));
                    return JxqyScriptStep.Continue();
                case "delmemo":
                    Required(_bindings.DeleteMemo)(
                        Parameter(instruction, 0));
                    return JxqyScriptStep.Continue();
                case "equipgoods":
                    Required(_bindings.EquipGoods)(
                        Integer(instruction, 0),
                        Integer(instruction, 1));
                    return JxqyScriptStep.Continue();
                case "setlevelfile":
                    return WaitFor(
                        Required(_bindings.SetLevelFileAsync)(
                            Parameter(instruction, 0)));
                case "setnpclevel":
                {
                    string name = Parameter(instruction, 0);
                    JxqyCharacter target = string.IsNullOrEmpty(name)
                        ? GetBelongObject(context) as JxqyCharacter
                        : FindCharacter(name);
                    if (target == null)
                        return JxqyScriptStep.Continue();
                    int level = Integer(instruction, 1);
                    if (ReferenceEquals(target, Player))
                    {
                        return WaitFor(
                            Required(_bindings.SetPlayerLevelAsync)(level));
                    }
                    if (!(target is JxqyNpc npc))
                    {
                        throw new NotSupportedException(
                            "SetNpcLevel requires an NPC target.");
                    }
                    Required(_bindings.SetNpcLevel)(npc, level);
                    return JxqyScriptStep.Continue();
                }
                case "enablesave":
                    Required(_bindings.SetSaveDisabled)(false);
                    return JxqyScriptStep.Continue();
                case "disablesave":
                    Required(_bindings.SetSaveDisabled)(true);
                    return JxqyScriptStep.Continue();
                case "clearallsave":
                    return WaitFor(
                        Required(_bindings.ClearAllSavesAsync)());
                case "enabeldrop":
                    Required(_bindings.SetDropDisabled)(false);
                    return JxqyScriptStep.Continue();
                case "disabledrop":
                    Required(_bindings.SetDropDisabled)(true);
                    return JxqyScriptStep.Continue();
                case "checkfreegoodsspace":
                    _variables.Set(
                        Parameter(instruction, 0),
                        Inventory.Entries.Count < Inventory.Capacity ? 1 : 0);
                    return JxqyScriptStep.Continue();
                case "checkfreemagicspace":
                    _variables.Set(
                        Parameter(instruction, 0),
                        Skills.Skills.Count < Skills.Capacity ? 1 : 0);
                    return JxqyScriptStep.Continue();
                case "getplayerstate":
                    _variables.Set(
                        Parameter(instruction, 1),
                        GetPlayerState(Parameter(instruction, 0)));
                    return JxqyScriptStep.Continue();
                case "isequipweapon":
                    _variables.Set(
                        Parameter(instruction, 0),
                        _ui.Equipment.Equipped.ContainsKey(
                            JxqyEquipmentSlot.Hand)
                            ? 1
                            : 0);
                    return JxqyScriptStep.Continue();
                case "clearallvar":
                    ClearAllVariables(instruction);
                    return JxqyScriptStep.Continue();
                case "setdropini":
                {
                    JxqyCharacter target =
                        FindCharacter(Parameter(instruction, 0));
                    if (target != null)
                        target.DropIni = Parameter(instruction, 1);
                    return JxqyScriptStep.Continue();
                }
                case "setshowmappos":
                    Required(_bindings.SetShowMapPosition)(
                        Integer(instruction, 0) > 0);
                    return JxqyScriptStep.Continue();
                case "setwalkisrun":
                    Player.WalkIsRun = Integer(instruction, 0) != 0;
                    return JxqyScriptStep.Continue();
                case "usemagic":
                    UseMagic(instruction);
                    return JxqyScriptStep.Continue();
                case "frozenmillisecond":
                    ApplyStatusMilliseconds(
                        JxqyStatusKind.Frozen,
                        Integer(instruction, 0));
                    return JxqyScriptStep.Continue();
                case "poisonmillisecond":
                    ApplyStatusMilliseconds(
                        JxqyStatusKind.Poisoned,
                        Integer(instruction, 0));
                    return JxqyScriptStep.Continue();
                case "petrifymillisecond":
                    ApplyStatusMilliseconds(
                        JxqyStatusKind.Petrified,
                        Integer(instruction, 0));
                    return JxqyScriptStep.Continue();
                case "displaymessage":
                    Required(_bindings.ShowMessage)(
                        Parameter(instruction, 0));
                    return JxqyScriptStep.Continue();
                case "showsystemmsg":
                    Required(_bindings.ShowSystemMessage)(
                        Parameter(instruction, 0),
                        instruction.Parameters.Count > 1
                            ? Integer(instruction, 1)
                            : 3000);
                    return JxqyScriptStep.Continue();
                case "stopsound":
                    Required(_bindings.StopSounds)();
                    return JxqyScriptStep.Continue();
                case "playerchange":
                    return WaitFor(
                        Required(_bindings.ChangePlayerAsync)(
                            Integer(instruction, 0)));
                case "opentimelimit":
                    Required(_bindings.OpenTimeLimit)(
                        Integer(instruction, 0));
                    return JxqyScriptStep.Continue();
                case "closetimelimit":
                    Required(_bindings.CloseTimeLimit)();
                    return JxqyScriptStep.Continue();
                case "hidetimerwnd":
                    Required(_bindings.HideTimerWindow)();
                    return JxqyScriptStep.Continue();
                case "settimescript":
                    Required(_bindings.SetTimeScript)(
                        Integer(instruction, 0),
                        Parameter(instruction, 1));
                    return JxqyScriptStep.Continue();
                case "showmessage":
                    Required(_bindings.ShowMessage)(
                        Required(_bindings.GetTalkText)(
                            Integer(instruction, 0)));
                    return JxqyScriptStep.Continue();
                case "returntotitle":
                    Required(_bindings.ReturnToTitle)();
                    return JxqyScriptStep.Return();
                default:
                    return _presentation.Execute(context, instruction);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            CancelDialogueWait();
            _root = null;
            _queuedSerialScripts.Clear();
            _parallelInvocations.Clear();
        }

        private void TryStartQueuedSerialScript()
        {
            if (IsFaulted ||
                (_root != null && !_root.IsFinished) ||
                _queuedSerialScripts.Count == 0)
            {
                return;
            }

            QueuedSerialScript queued = _queuedSerialScripts.Dequeue();
            _runCancellationToken = queued.CancellationToken;
            _root = new ScriptInvocation(
                this,
                null,
                queued.BelongObject);
            StartQueuedSerialScriptAsync(
                    _root,
                    queued.FileName,
                    queued.CancellationToken)
                .Forget();
        }

        private static async UniTaskVoid StartQueuedSerialScriptAsync(
            ScriptInvocation invocation,
            string fileName,
            CancellationToken cancellationToken)
        {
            try
            {
                await invocation.LoadAsync(fileName, cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private JxqyPlayer Player =>
            Required(_bindings.GetPlayer)() ??
            throw new InvalidOperationException(
                "The playable script has no active player.");

        private void AddMoney(int amount)
        {
            Player.AddMoney(amount);
            if (amount > 0)
            {
                _bindings.ShowMessage?.Invoke(
                    $"你得到了 {amount} 两银子。");
            }
            else if (amount < 0)
            {
                _bindings.ShowMessage?.Invoke(
                    $"你失去了 {-amount} 两银子。");
            }
        }

        private JxqyNpcManager Npcs =>
            Required(_bindings.GetNpcs)() ??
            throw new InvalidOperationException(
                "The playable script has no active NPC manager.");

        private JxqyObjectManager Objects =>
            Required(_bindings.GetObjects)() ??
            throw new InvalidOperationException(
                "The playable script has no active object manager.");

        private JxqyInventory Inventory =>
            Required(_bindings.GetInventory)() ??
            throw new InvalidOperationException(
                "The playable script has no active inventory.");

        private JxqySkillManager Skills =>
            Required(_bindings.GetSkills)() ??
            throw new InvalidOperationException(
                "The playable script has no active skill manager.");

        private static void AddAttack(
            JxqyCharacter character,
            int amount,
            int type)
        {
            switch (type)
            {
                case 1:
                    character.Attack += amount;
                    break;
                case 2:
                    character.Attack2 += amount;
                    break;
                case 3:
                    character.Attack3 += amount;
                    break;
            }
        }

        private static void AddDefend(
            JxqyCharacter character,
            int amount,
            int type)
        {
            switch (type)
            {
                case 1:
                    character.Defend = Math.Max(
                        0,
                        character.Defend + amount);
                    break;
                case 2:
                    character.Defend2 = Math.Max(
                        0,
                        character.Defend2 + amount);
                    break;
                case 3:
                    character.Defend3 = Math.Max(
                        0,
                        character.Defend3 + amount);
                    break;
            }
        }

        private JxqySkillEntry FindSkill(string fileNameOrName)
        {
            if (string.IsNullOrWhiteSpace(fileNameOrName))
                return null;
            return Skills.Skills.FirstOrDefault(entry =>
                string.Equals(
                    entry.Magic.Id,
                    fileNameOrName,
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    entry.Magic.Name,
                    fileNameOrName,
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    System.IO.Path.GetFileNameWithoutExtension(
                        entry.Magic.Id),
                    System.IO.Path.GetFileNameWithoutExtension(
                        fileNameOrName),
                    StringComparison.OrdinalIgnoreCase));
        }

        private int GetGoodsCountByName(string name)
        {
            return Inventory.Entries
                .Where(entry =>
                    string.Equals(
                        entry.Definition.Name,
                        name,
                        StringComparison.Ordinal))
                .Sum(entry => entry.Count);
        }

        private void DeleteGoodsByName(
            JxqyScriptInstruction instruction)
        {
            string name = Parameter(instruction, 0);
            int remaining = instruction.Parameters.Count > 1
                ? Math.Max(0, Integer(instruction, 1))
                : 0;
            bool deleteAll = remaining == 0;
            bool changed = false;
            foreach (JxqyInventoryEntry entry in
                     Inventory.Entries.ToArray())
            {
                if (!string.Equals(
                        entry.Definition.Name,
                        name,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                int amount = deleteAll
                    ? entry.Count
                    : Math.Min(entry.Count, remaining);
                if (amount > 0)
                {
                    changed |= Inventory.Remove(
                        entry.Definition.Id,
                        amount);
                }
                if (!deleteAll)
                {
                    remaining -= amount;
                    if (remaining <= 0)
                        break;
                }
            }
            if (changed)
                _ui.NotifyInventoryChanged();
        }

        private int GetPlayerState(string name)
        {
            switch (name)
            {
                case "Level":
                    return Player.Level;
                case "Attack":
                    return Player.Attack;
                case "Defend":
                    return Player.Defend;
                case "Evade":
                    return Player.Evade;
                case "Life":
                    return Player.Life;
                case "Thew":
                    return Player.Thew;
                case "Mana":
                    return Player.Mana;
                default:
                    return 0;
            }
        }

        private void ClearAllVariables(
            JxqyScriptInstruction instruction)
        {
            var keeps = new Dictionary<string, int>(
                StringComparer.Ordinal);
            foreach (string parameter in instruction.Parameters)
            {
                if (_variables.Values.ContainsKey(parameter))
                    keeps[parameter] = _variables.Get(parameter);
            }
            _variables.Clear();
            foreach (KeyValuePair<string, int> keep in keeps)
                _variables.Set(keep.Key, keep.Value);
        }

        private void ApplyStatusMilliseconds(
            JxqyStatusKind kind,
            int milliseconds)
        {
            Player.ApplyStatus(
                kind,
                Math.Max(0, milliseconds) / 1000f);
        }

        private void UseMagic(JxqyScriptInstruction instruction)
        {
            string magicFileName = Parameter(instruction, 0);
            JxqyIntPoint destination;
            if (instruction.Parameters.Count >= 3)
            {
                destination = new JxqyIntPoint(
                    Integer(instruction, 1),
                    Integer(instruction, 2));
            }
            else
            {
                destination = GetNeighborTile(
                    Player.TilePosition,
                    Player.CurrentDirection);
            }
            Required(_bindings.UsePlayerMagic)(
                magicFileName,
                destination.X,
                destination.Y);
        }

        private static JxqyIntPoint GetNeighborTile(
            JxqyIntPoint tile,
            int direction)
        {
            int normalized = ((direction % 8) + 8) % 8;
            bool evenRow = tile.Y % 2 == 0;
            switch (normalized)
            {
                case 0:
                    return new JxqyIntPoint(tile.X, tile.Y + 2);
                case 1:
                    return evenRow
                        ? new JxqyIntPoint(tile.X - 1, tile.Y + 1)
                        : new JxqyIntPoint(tile.X, tile.Y + 1);
                case 2:
                    return new JxqyIntPoint(tile.X - 1, tile.Y);
                case 3:
                    return evenRow
                        ? new JxqyIntPoint(tile.X - 1, tile.Y - 1)
                        : new JxqyIntPoint(tile.X, tile.Y - 1);
                case 4:
                    return new JxqyIntPoint(tile.X, tile.Y - 2);
                case 5:
                    return evenRow
                        ? new JxqyIntPoint(tile.X, tile.Y - 1)
                        : new JxqyIntPoint(tile.X + 1, tile.Y - 1);
                case 6:
                    return new JxqyIntPoint(tile.X + 1, tile.Y);
                default:
                    return evenRow
                        ? new JxqyIntPoint(tile.X, tile.Y + 1)
                        : new JxqyIntPoint(tile.X + 1, tile.Y + 1);
            }
        }

        private JxqyCharacter FindCharacter(string name)
        {
            if (string.Equals(
                    name,
                    Player.Name,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Player;
            }
            JxqyNpc exact = Npcs.Find(name);
            if (exact != null)
                return exact;

            JxqyNpc best = null;
            int bestScore = 0;
            foreach (JxqyNpc npc in Npcs.Npcs)
            {
                int score = LegacyNameMatchScore(name, npc.Name);
                if (score <= bestScore)
                    continue;
                best = npc;
                bestScore = score;
            }
            if (bestScore < 3)
                return null;
            Debug.LogWarning(
                $"JXQY-SCRIPT matched legacy character alias " +
                $"'{name}' to '{best.Name}'.");
            return best;
        }

        private static int LegacyNameMatchScore(
            string requested,
            string candidate)
        {
            if (string.IsNullOrWhiteSpace(requested) ||
                string.IsNullOrWhiteSpace(candidate))
            {
                return 0;
            }
            if (requested.StartsWith(
                    candidate,
                    StringComparison.OrdinalIgnoreCase) ||
                candidate.StartsWith(
                    requested,
                    StringComparison.OrdinalIgnoreCase))
            {
                return 100 + Math.Min(
                    requested.Length,
                    candidate.Length);
            }
            int length = Math.Min(requested.Length, candidate.Length);
            int commonPrefix = 0;
            while (commonPrefix < length &&
                   char.ToUpperInvariant(requested[commonPrefix]) ==
                   char.ToUpperInvariant(candidate[commonPrefix]))
            {
                commonPrefix++;
            }
            return commonPrefix;
        }

        private JxqyScriptStep LoadGame(JxqyScriptInstruction instruction)
        {
            int slot = Integer(instruction, 0);
            if (slot != 0)
            {
                return JxqyScriptStep.Fault(
                    $"Legacy LoadGame({slot}) is not a new-game slot.");
            }
            return WaitFor(Required(_bindings.LoadNewGameAsync)());
        }

        private JxqyScriptStep RunScript(
            JxqyScriptContext context,
            JxqyScriptInstruction instruction)
        {
            if (!(context?.Owner is ScriptInvocation owner))
            {
                return JxqyScriptStep.Fault(
                    "RunScript has no owning script invocation.");
            }
            ScriptInvocation child = owner.BeginChild(Parameter(instruction, 0));
            return JxqyScriptStep.WaitFor(
                new JxqyPredicateScriptWait(
                    child.ThrowIfFailedOrReturnCompleted));
        }

        private JxqyScriptStep RunRandomScript(
            JxqyScriptContext context,
            JxqyScriptInstruction instruction)
        {
            RequireParameterCount(instruction, 3);
            string selected = _random.Next(0, 100) <=
                              _variables.Get(
                                  Parameter(instruction, 0))
                ? Parameter(instruction, 1)
                : Parameter(instruction, 2);
            if (!(context?.Owner is ScriptInvocation owner))
            {
                return JxqyScriptStep.Fault(
                    "RandRun has no owning script invocation.");
            }
            ScriptInvocation child = owner.BeginChild(selected);
            return JxqyScriptStep.WaitFor(
                new JxqyPredicateScriptWait(
                    child.ThrowIfFailedOrReturnCompleted));
        }

        private void RunParallelScript(
            JxqyScriptContext context,
            JxqyScriptInstruction instruction)
        {
            if (instruction.Parameters.Count != 1 &&
                instruction.Parameters.Count != 2)
            {
                throw new InvalidOperationException(
                    "RunParallelScript expects a script and optional delay.");
            }
            double delay = instruction.Parameters.Count == 2
                ? Math.Max(0, Integer(instruction, 1))
                : 0;
            object belongObject = GetBelongObject(context);
            StartParallel(
                Parameter(instruction, 0),
                delay,
                belongObject);
        }

        private JxqyScriptStep Say(JxqyScriptInstruction instruction)
        {
            EnsureDialogueAvailable();
            var dialogue = new JxqyDialogue();
            dialogue.Add(new JxqyDialoguePage
            {
                Text = Parameter(instruction, 0),
                PortraitFileName = ResolvePortrait(instruction),
            });
            bool completed = false;
            _dialogueCompleted = _ =>
            {
                completed = true;
                CancelDialogueWait();
            };
            _ui.DialogueCompleted += _dialogueCompleted;
            Player.SetFighting(false);
            _ui.StartDialogue(dialogue);
            return JxqyScriptStep.WaitFor(
                new JxqyPredicateScriptWait(() => completed));
        }

        private JxqyScriptStep Talk(
            JxqyScriptInstruction instruction)
        {
            EnsureDialogueAvailable();
            int from = Integer(instruction, 0);
            int to = Integer(instruction, 1);
            IReadOnlyList<JxqyLegacyTalkLine> lines =
                Required(_bindings.GetTalkLines)(from, to);
            if (lines == null || lines.Count == 0)
            {
                Debug.LogWarning(
                    $"JXQY-SCRIPT Talk({from}, {to}) has no text.");
                return JxqyScriptStep.Continue();
            }
            var dialogue = new JxqyDialogue();
            for (int index = 0; index < lines.Count; index++)
            {
                JxqyLegacyTalkLine line = lines[index];
                dialogue.Add(new JxqyDialoguePage
                {
                    Text = (line.Text ?? string.Empty)
                        .Replace("<enter>", "\n"),
                    PortraitFileName =
                        ResolvePortrait(line.PortraitIndex),
                });
            }
            bool completed = false;
            _dialogueCompleted = _ =>
            {
                completed = true;
                CancelDialogueWait();
            };
            _ui.DialogueCompleted += _dialogueCompleted;
            Player.SetFighting(false);
            _ui.StartDialogue(dialogue);
            return JxqyScriptStep.WaitFor(
                new JxqyPredicateScriptWait(() => completed));
        }

        private JxqyScriptStep Choose(JxqyScriptInstruction instruction)
        {
            EnsureDialogueAvailable();
            string variable = Parameter(instruction, 3);
            var dialogue = new JxqyDialogue();
            var page = new JxqyDialoguePage
            {
                Text = Parameter(instruction, 0),
            };
            page.Choices.Add(
                new JxqyDialogueChoice(Parameter(instruction, 1), "0"));
            page.Choices.Add(
                new JxqyDialogueChoice(Parameter(instruction, 2), "1"));
            dialogue.Add(page);
            bool completed = false;
            _dialogueCompleted = choice =>
            {
                _variables.Set(
                    variable,
                    int.TryParse(
                        choice,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int selected)
                        ? selected
                        : 0);
                completed = true;
                CancelDialogueWait();
            };
            _ui.DialogueCompleted += _dialogueCompleted;
            _ui.StartDialogue(dialogue);
            return JxqyScriptStep.WaitFor(
                new JxqyPredicateScriptWait(() => completed));
        }

        private JxqyScriptStep ChooseEx(
            JxqyScriptInstruction instruction)
        {
            if (instruction.Parameters.Count < 3)
            {
                throw new InvalidOperationException(
                    "ChooseEx expects a message, choices, and a variable.");
            }
            EnsureDialogueAvailable();
            string variable =
                Parameter(instruction, instruction.Parameters.Count - 1);
            var dialogue = new JxqyDialogue();
            var page = new JxqyDialoguePage
            {
                Text = Parameter(instruction, 0),
                Presentation = JxqyDialoguePresentation.Selection,
            };
            for (int index = 1;
                 index < instruction.Parameters.Count - 1;
                 index++)
            {
                string text = Parameter(instruction, index);
                if (!TryStripSatisfiedConditions(ref text))
                    continue;
                page.Choices.Add(new JxqyDialogueChoice(
                    text,
                    (index - 1).ToString(
                        CultureInfo.InvariantCulture)));
            }
            if (page.Choices.Count == 0)
            {
                _variables.Set(variable, 0);
                return JxqyScriptStep.Continue();
            }
            dialogue.Add(page);
            bool completed = false;
            _dialogueCompleted = choice =>
            {
                _variables.Set(
                    variable,
                    int.TryParse(
                        choice,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int selected)
                        ? selected
                        : 0);
                completed = true;
                CancelDialogueWait();
            };
            _ui.DialogueCompleted += _dialogueCompleted;
            _ui.StartDialogue(dialogue);
            return JxqyScriptStep.WaitFor(
                new JxqyPredicateScriptWait(() => completed));
        }

        private JxqyScriptStep ChooseMultiple(
            JxqyScriptInstruction instruction)
        {
            if (instruction.Parameters.Count < 5)
            {
                throw new InvalidOperationException(
                    "ChooseMultiple expects column, count, variable, " +
                    "message, and choices.");
            }
            EnsureDialogueAvailable();
            int selectionCount = Math.Max(1, Integer(instruction, 1));
            string variable = Parameter(instruction, 2);
            var dialogue = new JxqyDialogue();
            var page = new JxqyDialoguePage
            {
                Text = Parameter(instruction, 3),
                SelectionCount = selectionCount,
                SelectionColumns = Math.Max(1, Integer(instruction, 0)),
                Presentation = JxqyDialoguePresentation.Selection,
            };
            for (int index = 4;
                 index < instruction.Parameters.Count;
                 index++)
            {
                string text = Parameter(instruction, index);
                if (!TryStripSatisfiedConditions(ref text))
                    continue;
                page.Choices.Add(new JxqyDialogueChoice(
                    text,
                    (index - 4).ToString(
                        CultureInfo.InvariantCulture)));
            }
            if (page.Choices.Count == 0)
                return JxqyScriptStep.Continue();
            page.SelectionCount = Math.Min(
                page.SelectionCount,
                page.Choices.Count);
            dialogue.Add(page);
            bool completed = false;
            _dialogueCompleted = choice =>
            {
                string prefix = variable.StartsWith(
                        "$",
                        StringComparison.Ordinal)
                    ? variable
                    : "$" + variable;
                string[] values = (choice ?? string.Empty)
                    .Split(
                        new[] { ',' },
                        StringSplitOptions.RemoveEmptyEntries);
                for (int index = 0; index < values.Length; index++)
                {
                    _variables.Set(
                        prefix + index.ToString(
                            CultureInfo.InvariantCulture),
                        int.TryParse(
                            values[index],
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out int selected)
                            ? selected
                            : 0);
                }
                completed = true;
                CancelDialogueWait();
            };
            _ui.DialogueCompleted += _dialogueCompleted;
            _ui.StartDialogue(dialogue);
            return JxqyScriptStep.WaitFor(
                new JxqyPredicateScriptWait(() => completed));
        }

        private bool TryStripSatisfiedConditions(ref string text)
        {
            bool satisfied = true;
            int cursor = 0;
            var visible = new System.Text.StringBuilder();
            while (cursor < text.Length)
            {
                int open = text.IndexOf('{', cursor);
                if (open < 0)
                {
                    visible.Append(text, cursor, text.Length - cursor);
                    break;
                }
                visible.Append(text, cursor, open - cursor);
                int close = text.IndexOf('}', open + 1);
                if (close < 0)
                {
                    visible.Append(text, open, text.Length - open);
                    break;
                }
                if (!EvaluateChoiceCondition(
                        text.Substring(open + 1, close - open - 1)))
                {
                    satisfied = false;
                }
                cursor = close + 1;
            }
            text = visible.ToString();
            return satisfied;
        }

        private bool EvaluateChoiceCondition(string expression)
        {
            string[] operators =
                { "==", ">=", "<=", "<>", ">>", "<<", ">", "<" };
            foreach (string comparison in operators)
            {
                int index = expression.IndexOf(
                    comparison,
                    StringComparison.Ordinal);
                if (index < 0)
                    continue;
                string variable = expression.Substring(0, index).Trim();
                string expectedText =
                    expression.Substring(index + comparison.Length).Trim();
                if (!int.TryParse(
                        expectedText,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int expected))
                {
                    return false;
                }
                int actual = _variables.Get(variable);
                switch (comparison)
                {
                    case "==":
                        return actual == expected;
                    case ">=":
                        return actual >= expected;
                    case "<=":
                        return actual <= expected;
                    case "<>":
                        return actual != expected;
                    case ">":
                    case ">>":
                        return actual > expected;
                    case "<":
                    case "<<":
                        return actual < expected;
                }
            }
            return false;
        }

        private JxqyScriptStep Select(
            JxqyScriptInstruction instruction)
        {
            RequireParameterCount(instruction, 4);
            EnsureDialogueAvailable();
            string variable = Parameter(instruction, 3);
            var dialogue = new JxqyDialogue();
            var page = new JxqyDialoguePage
            {
                Text = ResolveTalkTextParameter(instruction, 0),
            };
            page.Choices.Add(new JxqyDialogueChoice(
                ResolveTalkTextParameter(instruction, 1),
                "0"));
            page.Choices.Add(new JxqyDialogueChoice(
                ResolveTalkTextParameter(instruction, 2),
                "1"));
            dialogue.Add(page);
            bool completed = false;
            _dialogueCompleted = choice =>
            {
                _variables.Set(
                    variable,
                    int.TryParse(
                        choice,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int selected)
                        ? selected
                        : 0);
                completed = true;
                CancelDialogueWait();
            };
            _ui.DialogueCompleted += _dialogueCompleted;
            _ui.StartDialogue(dialogue);
            return JxqyScriptStep.WaitFor(
                new JxqyPredicateScriptWait(() => completed));
        }

        private string ResolveTalkTextParameter(
            JxqyScriptInstruction instruction,
            int index)
        {
            string value = Parameter(instruction, index);
            if (value.StartsWith("$", StringComparison.Ordinal))
                value = _variables.Get(value).ToString(
                    CultureInfo.InvariantCulture);
            if (!int.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int textId))
            {
                throw new FormatException(
                    $"{instruction.Name} parameter {index + 1} " +
                    "must resolve to a talk text id.");
            }
            return Required(_bindings.GetTalkText)(textId);
        }

        private void SetPlayerPosition(JxqyScriptInstruction instruction)
        {
            JxqyCharacter target;
            int offset;
            if (instruction.Parameters.Count == 2)
            {
                target = Player;
                offset = 0;
            }
            else if (instruction.Parameters.Count == 3)
            {
                target = FindCharacter(Parameter(instruction, 0));
                offset = 1;
            }
            else
            {
                throw new InvalidOperationException(
                    "SetPlayerPos expects two or three parameters.");
            }
            if (target == null)
                return;
            target.Stop();
            target.TilePosition = new JxqyIntPoint(
                Integer(instruction, offset),
                Integer(instruction, offset + 1));
            Required(_bindings.RefreshActorVisual)(target);
            Required(_bindings.HandleScriptedPlayerPositionSet)();
        }

        private void SetPlayerState(JxqyScriptInstruction instruction)
        {
            RequireParameterCount(instruction, 1);
            bool fighting = Integer(instruction, 0) != 0;
            Player.SetFighting(fighting);
            Player.SetState(
                fighting
                    ? JxqyCharacterState.FightStand
                    : JxqyCharacterState.Stand);
        }

        private JxqyScriptStep PlayerGoto(
            JxqyScriptInstruction instruction)
        {
            var destination = new JxqyIntPoint(
                Integer(instruction, 0),
                Integer(instruction, 1));
            if (Player.TilePosition.Equals(destination))
                return JxqyScriptStep.Continue();
            IJxqyTileCollisionMap collision =
                Required(_bindings.GetCollisionMap)() ??
                throw new InvalidOperationException(
                    "The playable script has no collision map.");
            IReadOnlyList<JxqyFloat2> path = JxqyPathfinder.FindPath(
                collision,
                Player.TilePosition,
                destination,
                tile => IsOccupiedByOtherNpc(tile, Player));
            if (path.Count < 2 || !Player.BeginPath(path))
            {
                Player.Stop();
                return JxqyScriptStep.Continue();
            }
            return JxqyScriptStep.WaitFor(
                new JxqyPredicateScriptWait(() => !Player.HasPath));
        }

        private void SetNpcDirection(
            JxqyScriptContext context,
            JxqyScriptInstruction instruction)
        {
            JxqyCharacter target;
            int direction;
            if (instruction.Parameters.Count == 1)
            {
                target = GetBelongObject(context) as JxqyCharacter;
                direction = Integer(instruction, 0);
            }
            else if (instruction.Parameters.Count == 2)
            {
                target = FindCharacter(Parameter(instruction, 0));
                direction = Integer(instruction, 1);
            }
            else
            {
                throw new InvalidOperationException(
                    "SetNpcDir expects one or two parameters.");
            }
            if (target == null)
                return;
            target.CurrentDirection = direction;
            Required(_bindings.RefreshActorVisual)(target);
        }

        private void SetNpcPosition(
            JxqyScriptContext context,
            JxqyScriptInstruction instruction)
        {
            JxqyCharacter target =
                GetBelongObject(context) as JxqyCharacter;
            int x = 0;
            int y = 0;
            if (instruction.Parameters.Count == 3)
            {
                target = FindCharacter(Parameter(instruction, 0));
                x = Integer(instruction, 1);
                y = Integer(instruction, 2);
            }
            else if (instruction.Parameters.Count == 2)
            {
                x = Integer(instruction, 0);
                y = Integer(instruction, 1);
            }
            // The original GetTargetAndValue3 helper leaves other arities at
            // their defaults. In ownerless trap scripts that is a soft no-op,
            // including the shipped SetNpcPos(name, x, y, direction) call.
            if (target == null)
                return;
            target.Stop();
            target.TilePosition = new JxqyIntPoint(x, y);
            Required(_bindings.RefreshActorVisual)(target);
        }

        private void SetNpcKind(
            JxqyScriptContext context,
            JxqyScriptInstruction instruction)
        {
            RequireParameterCount(instruction, 2);
            string name = Parameter(instruction, 0);
            var kind =
                (JxqyCharacterKind)Integer(instruction, 1);
            if (string.IsNullOrEmpty(name))
            {
                JxqyCharacter owner =
                    GetBelongObject(context) as JxqyCharacter;
                if (owner != null)
                {
                    owner.Kind = kind;
                    Required(_bindings.RefreshActorVisual)(owner);
                }
                return;
            }
            if (string.Equals(
                    name,
                    Player.Name,
                    StringComparison.OrdinalIgnoreCase))
            {
                Player.Kind = kind;
                Required(_bindings.RefreshActorVisual)(Player);
                return;
            }
            IReadOnlyList<JxqyNpc> targets = Npcs.FindAll(name);
            foreach (JxqyNpc target in targets)
            {
                target.Kind = kind;
                Required(_bindings.RefreshActorVisual)(target);
            }
        }

        private void SetNpcRelation(
            JxqyScriptContext context,
            JxqyScriptInstruction instruction)
        {
            RequireParameterCount(instruction, 2);
            string name = Parameter(instruction, 0);
            var relation =
                (JxqyRelationType)Integer(instruction, 1);

            if (string.IsNullOrEmpty(name))
            {
                ApplyRelation(
                    GetBelongObject(context) as JxqyCharacter,
                    relation);
                return;
            }

            // Legacy ScriptExecuter.SetNpcRelation applies the relation to
            // every NPC with the requested name. Several maps intentionally
            // place multiple guards with the same display name and switch
            // the whole group hostile with one command.
            foreach (JxqyNpc npc in Npcs.FindAll(name))
                ApplyRelation(npc, relation);

            if (string.Equals(
                    name,
                    Player.Name,
                    StringComparison.OrdinalIgnoreCase))
            {
                ApplyRelation(Player, relation);
            }
        }

        private static void ApplyRelation(
            JxqyCharacter target,
            JxqyRelationType relation)
        {
            if (target == null)
                return;
            if (target is JxqyNpc npc &&
                ((target.Relation == JxqyRelationType.Friend &&
                  relation == JxqyRelationType.Enemy) ||
                 (target.Relation == JxqyRelationType.Enemy &&
                  relation != JxqyRelationType.Enemy)))
            {
                // Character.SetRelation in the original clears a follow
                // target that is no longer valid after a camp transition.
                npc.Follow(null);
            }
            target.Relation = relation;
        }

        private void SetCharacterScript(
            JxqyScriptContext context,
            JxqyScriptInstruction instruction,
            bool deathScript)
        {
            RequireParameterCount(instruction, 2);
            string name = Parameter(instruction, 0);
            JxqyCharacter target = string.IsNullOrEmpty(name)
                ? GetBelongObject(context) as JxqyCharacter
                : FindCharacter(name);
            if (target == null)
                return;
            string address = Parameter(instruction, 1);
            if (deathScript)
                target.DeathScriptAddress = address;
            else
                target.ScriptAddress = address;
        }

        private void SetObjectOffset(
            JxqyScriptInstruction instruction)
        {
            RequireParameterCount(instruction, 3);
            JxqyWorldObject target =
                Required(_bindings.GetObjects)()
                    ?.Find(Parameter(instruction, 0));
            if (target == null)
                return;
            target.OffsetX = Integer(instruction, 1);
            target.OffsetY = Integer(instruction, 2);
            Required(_bindings.RefreshActorVisual)(target);
        }

        private JxqyScriptStep NpcGoto(
            JxqyScriptContext context,
            JxqyScriptInstruction instruction)
        {
            ResolveCharacterAndPair(
                context,
                instruction,
                out JxqyCharacter target,
                out int destinationX,
                out int destinationY);
            if (target == null)
                return JxqyScriptStep.Continue();
            var destination = new JxqyIntPoint(destinationX, destinationY);
            if (target.TilePosition.Equals(destination))
                return JxqyScriptStep.Continue();
            IJxqyTileCollisionMap collision =
                Required(_bindings.GetCollisionMap)() ??
                throw new InvalidOperationException(
                    "The playable script has no collision map.");
            IReadOnlyList<JxqyFloat2> path =
                JxqyPathfinder.FindPath(
                collision,
                target.TilePosition,
                destination,
                tile => IsOccupiedByOtherNpc(tile, target));
            if (path.Count < 2 || !target.BeginPath(path))
            {
                target.Stop();
                return JxqyScriptStep.Continue();
            }
            return JxqyScriptStep.WaitFor(
                new JxqyPredicateScriptWait(() => !target.HasPath));
        }

        private JxqyScriptStep MoveCharacter(
            JxqyCharacter target,
            JxqyIntPoint destination,
            bool run,
            bool wait)
        {
            if (target.TilePosition.Equals(destination))
                return JxqyScriptStep.Continue();
            IJxqyTileCollisionMap collision =
                Required(_bindings.GetCollisionMap)() ??
                throw new InvalidOperationException(
                    "The playable script has no collision map.");
            IReadOnlyList<JxqyFloat2> path =
                JxqyPathfinder.FindPath(
                collision,
                target.TilePosition,
                destination,
                tile => IsOccupiedByOtherNpc(tile, target));
            if (path.Count < 2 || !target.BeginPath(path, run))
            {
                target.Stop();
                return JxqyScriptStep.Continue();
            }
            if (!wait)
                return JxqyScriptStep.Continue();
            return JxqyScriptStep.WaitFor(
                new JxqyPredicateScriptWait(() => !target.HasPath));
        }

        private JxqyScriptStep JumpCharacter(
            JxqyCharacter target,
            JxqyIntPoint destination,
            bool wait)
        {
            if (target.TilePosition.Equals(destination))
                return JxqyScriptStep.Continue();
            IJxqyTileCollisionMap collision =
                Required(_bindings.GetCollisionMap)() ??
                throw new InvalidOperationException(
                    "The playable script has no collision map.");
            if (collision.IsObstacleForCharacter(destination) ||
                collision.IsObstacleForCharacterJump(destination))
            {
                return JxqyScriptStep.Continue();
            }
            if (target is JxqyPlayer player && player.Thew < 10)
            {
                _bindings.ShowMessage?.Invoke("体力不足!");
                return JxqyScriptStep.Continue();
            }

            JxqyIntPoint destinationWorld =
                JxqyIsometricMapMath.TileToWorldPixel(
                    destination.X,
                    destination.Y);
            bool started = target.BeginJump(
                new JxqyFloat2(
                    destinationWorld.X,
                    destinationWorld.Y),
                tile =>
                    tile.Equals(target.TilePosition) ||
                    !collision.IsObstacleForCharacterJump(tile));
            if (started && target is JxqyPlayer jumpingPlayer)
                jumpingPlayer.Thew -= 10;
            if (!started || !wait)
                return JxqyScriptStep.Continue();
            return JxqyScriptStep.WaitFor(
                new JxqyPredicateScriptWait(() =>
                    !target.IsJumping && !target.HasPath));
        }

        private JxqyScriptStep MoveInDirection(
            JxqyCharacter target,
            int direction,
            int steps,
            bool run,
            bool wait)
        {
            if (direction < 0 || direction > 7)
                throw new ArgumentOutOfRangeException(nameof(direction));
            if (steps < 0)
                throw new ArgumentOutOfRangeException(nameof(steps));
            IJxqyTileCollisionMap collision =
                Required(_bindings.GetCollisionMap)() ??
                throw new InvalidOperationException(
                    "The playable script has no collision map.");
            JxqyIntPoint destination = target.TilePosition;
            var path = new List<JxqyFloat2>
            {
                target.PositionInWorld,
            };
            for (int index = 0; index < steps; index++)
            {
                JxqyIntPoint next =
                    JxqyPathfinder.GetAllNeighbors(destination)[direction];
                if (collision.IsObstacleForCharacter(next) ||
                    IsOccupiedByOtherNpc(next, target))
                {
                    break;
                }
                destination = next;
                JxqyIntPoint world =
                    JxqyIsometricMapMath.TileToWorldPixel(
                        destination.X,
                        destination.Y);
                path.Add(new JxqyFloat2(world.X, world.Y));
            }
            if (path.Count < 2 || !target.BeginPath(path, run))
            {
                target.Stop();
                return JxqyScriptStep.Continue();
            }
            return wait
                ? JxqyScriptStep.WaitFor(
                    new JxqyPredicateScriptWait(() => !target.HasPath))
                : JxqyScriptStep.Continue();
        }

        private void SetObjectScript(
            JxqyScriptContext context,
            JxqyScriptInstruction instruction)
        {
            RequireParameterCount(instruction, 2);
            string name = Parameter(instruction, 0);
            JxqyWorldObject target = string.IsNullOrEmpty(name)
                ? GetBelongObject(context) as JxqyWorldObject
                : Objects.Find(name);
            if (target == null)
                return;
            target.ScriptAddress = Parameter(instruction, 1);
        }

        private void SetObjectOpen(
            JxqyScriptContext context,
            JxqyScriptInstruction instruction,
            bool open)
        {
            if (instruction.Parameters.Count > 1)
                throw new InvalidOperationException(
                    $"{instruction.Name} expects zero or one parameter.");
            JxqyWorldObject target =
                instruction.Parameters.Count == 0
                    ? GetBelongObject(context) as JxqyWorldObject
                    : Objects.Find(Parameter(instruction, 0));
            if (target == null)
                return;
            target.IsOpen = open;
            Required(_bindings.RefreshActorVisual)(target);
        }

        private void ShowNpc(JxqyScriptInstruction instruction)
        {
            RequireParameterCount(instruction, 2);
            string name = Parameter(instruction, 0);
            bool visible = Integer(instruction, 1) != 0;
            if (string.Equals(
                    name,
                    Player.Name,
                    StringComparison.OrdinalIgnoreCase))
            {
                Player.IsVisible = visible;
                Required(_bindings.RefreshActorVisual)(Player);
                return;
            }
            IReadOnlyList<JxqyNpc> exactTargets = Npcs.FindAll(name);
            JxqyNpc target = exactTargets.Count == 0
                ? null
                : exactTargets[exactTargets.Count - 1];
            if (target == null)
            {
                JxqyCharacter fallback = FindCharacter(name);
                target = fallback as JxqyNpc;
            }
            if (target == null)
                return;
            target.IsVisible = visible;
            Required(_bindings.RefreshActorVisual)(target);
        }

        private void NpcAttack(
            JxqyScriptContext context,
            JxqyScriptInstruction instruction)
        {
            ResolveCharacterAndPair(
                context,
                instruction,
                out JxqyCharacter target,
                out int tileX,
                out int tileY);
            if (target == null)
                return;
            JxqyIntPoint world = JxqyIsometricMapMath.TileToWorldPixel(
                tileX,
                tileY);
            Required(_bindings.PerformNpcAttack)(
                target,
                new JxqyFloat2(world.X, world.Y));
            Required(_bindings.RefreshActorVisual)(target);
        }

        private void FollowNpc(
            JxqyScriptContext context,
            JxqyScriptInstruction instruction)
        {
            JxqyCharacter follower;
            JxqyCharacter target;
            if (instruction.Parameters.Count == 1)
            {
                follower =
                    GetBelongObject(context) as JxqyCharacter;
                target = FindCharacter(Parameter(instruction, 0));
            }
            else if (instruction.Parameters.Count == 2)
            {
                follower = FindCharacter(Parameter(instruction, 0));
                target = FindCharacter(Parameter(instruction, 1));
            }
            else
            {
                throw new InvalidOperationException(
                    "FollowNpc expects one or two parameters.");
            }
            if (follower == null || target == null)
                return;
            if (follower is JxqyNpc npc)
                npc.Follow(target);
        }

        private void SetNpcMagicFile(
            JxqyScriptInstruction instruction)
        {
            RequireParameterCount(instruction, 2);
            JxqyCharacter target =
                FindCharacter(Parameter(instruction, 0));
            if (target == null)
                return;
            target.MagicFileName = Parameter(instruction, 1);
        }

        private JxqyScriptStep SetNpcResourceAsync(
            JxqyScriptContext context,
            JxqyScriptInstruction instruction)
        {
            ResolveCharacterAndString(
                context,
                instruction,
                out JxqyCharacter target,
                out string resourceFileName);
            if (target == null)
                return JxqyScriptStep.Continue();
            return WaitFor(
                Required(_bindings.SetCharacterResourceAsync)(
                    target,
                    resourceFileName));
        }

        private JxqyScriptStep SetNpcMagicAsync(
            JxqyScriptContext context,
            JxqyScriptInstruction instruction)
        {
            ResolveCharacterAndString(
                context,
                instruction,
                out JxqyCharacter target,
                out string magicFileName);
            if (target == null)
                return JxqyScriptStep.Continue();
            return SetCharacterMagicAsync(
                new[] { target },
                magicFileName,
                secondary: false);
        }

        private JxqyScriptStep SetNpcActionFileAsync(
            JxqyScriptInstruction instruction)
        {
            RequireParameterCount(instruction, 3);
            JxqyCharacter target =
                FindCharacter(Parameter(instruction, 0));
            if (target == null)
                return JxqyScriptStep.Continue();
            return WaitFor(
                Required(_bindings.SetCharacterActionFileAsync)(
                    target,
                    Integer(instruction, 1),
                    Parameter(instruction, 2)));
        }

        private JxqyScriptStep PlayNpcSpecialActionAsync(
            JxqyScriptContext context,
            JxqyScriptInstruction instruction,
            bool wait)
        {
            ResolveCharacterAndString(
                context,
                instruction,
                out JxqyCharacter target,
                out string actionFileName);
            if (target == null)
            {
                if (wait)
                    Required(_bindings.SetInputDisabled)(false);
                return JxqyScriptStep.Continue();
            }
            if (wait)
            {
                return WaitFor(
                    PlayNpcSpecialActionAndRestoreInputAsync(
                        target,
                        actionFileName));
            }
            return WaitFor(
                Required(_bindings.PlayCharacterSpecialActionAsync)(
                    target,
                    actionFileName,
                    false));
        }

        private async UniTask PlayNpcSpecialActionAndRestoreInputAsync(
            JxqyCharacter target,
            string actionFileName)
        {
            Required(_bindings.SetInputDisabled)(true);
            try
            {
                await Required(
                    _bindings.PlayCharacterSpecialActionAsync)(
                    target,
                    actionFileName,
                    true);
            }
            finally
            {
                Required(_bindings.SetInputDisabled)(false);
            }
        }

        private void ResolveCharacterAndString(
            JxqyScriptContext context,
            JxqyScriptInstruction instruction,
            out JxqyCharacter target,
            out string value)
        {
            if (instruction.Parameters.Count == 1)
            {
                target = GetBelongObject(context) as JxqyCharacter;
                value = Parameter(instruction, 0);
                return;
            }
            if (instruction.Parameters.Count == 2)
            {
                target = FindCharacter(Parameter(instruction, 0));
                value = Parameter(instruction, 1);
                return;
            }
            throw new InvalidOperationException(
                $"{instruction.Name} expects one or two parameters.");
        }

        private JxqyScriptStep SetCharacterMagicAsync(
            IReadOnlyList<JxqyCharacter> targets,
            string magicFileName,
            bool secondary)
        {
            var operation = new AsyncCommandOperation();
            operation.RunAsync(SetCharacterMagicCoreAsync(
                    targets,
                    magicFileName,
                    secondary))
                .Forget();
            return JxqyScriptStep.WaitFor(
                new JxqyPredicateScriptWait(
                    operation.ThrowIfFailedOrReturnCompleted));
        }

        private async UniTask SetCharacterMagicCoreAsync(
            IReadOnlyList<JxqyCharacter> targets,
            string magicFileName,
            bool secondary)
        {
            if (targets == null || targets.Count == 0)
                return;
            var byLevel = new Dictionary<int, JxqyMagicDefinition>();
            foreach (JxqyCharacter target in targets)
            {
                if (target == null)
                    continue;
                int level = Math.Max(1, target.AttackLevel);
                if (!byLevel.TryGetValue(
                        level,
                        out JxqyMagicDefinition magic))
                {
                    magic = await Required(
                        _bindings.LoadMagicDefinitionAtLevelAsync)(
                        magicFileName,
                        level);
                    byLevel[level] = magic;
                }
                if (secondary)
                {
                    target.MagicFileName2 = magicFileName;
                    target.BasicMagic2 = magic.CreateRuntimeSnapshot();
                }
                else
                {
                    target.MagicFileName = magicFileName;
                    target.BasicMagic = magic.CreateRuntimeSnapshot();
                }
            }
        }

        private JxqyScriptStep AddNpcRangedMagicAsync(
            JxqyScriptInstruction instruction)
        {
            RequireParameterCount(instruction, 3);
            IReadOnlyList<JxqyCharacter> targets =
                Npcs.FindAll(Parameter(instruction, 0))
                    .Cast<JxqyCharacter>()
                    .ToArray();
            var operation = new AsyncCommandOperation();
            operation.RunAsync(AddNpcRangedMagicCoreAsync(
                    targets,
                    Parameter(instruction, 1),
                    Integer(instruction, 2)))
                .Forget();
            return JxqyScriptStep.WaitFor(
                new JxqyPredicateScriptWait(
                    operation.ThrowIfFailedOrReturnCompleted));
        }

        private async UniTask AddNpcRangedMagicCoreAsync(
            IReadOnlyList<JxqyCharacter> targets,
            string magicFileName,
            int distance)
        {
            if (targets == null || targets.Count == 0)
                return;
            var byLevel = new Dictionary<int, JxqyMagicDefinition>();
            foreach (JxqyCharacter target in targets)
            {
                int level = Math.Max(1, target.AttackLevel);
                if (!byLevel.TryGetValue(
                        level,
                        out JxqyMagicDefinition magic))
                {
                    magic = await Required(
                        _bindings.LoadMagicDefinitionAtLevelAsync)(
                        magicFileName,
                        level);
                    byLevel[level] = magic;
                }
                target.AdditionalBasicMagics.Add(
                    new JxqyRangedMagicReference
                    {
                        Magic = magic.CreateRuntimeSnapshot(),
                        Distance = distance,
                    });
            }
        }

        private void SetNpcDestination(
            JxqyScriptInstruction instruction)
        {
            RequireParameterCount(instruction, 3);
            foreach (JxqyNpc npc in
                     Npcs.FindAll(Parameter(instruction, 0)))
            {
                npc.DestinationMapPosX = Integer(instruction, 1);
                npc.DestinationMapPosY = Integer(instruction, 2);
            }
        }

        private void SetKeepAttack(
            JxqyScriptInstruction instruction)
        {
            RequireParameterCount(instruction, 3);
            foreach (JxqyNpc npc in
                     Npcs.FindAll(Parameter(instruction, 0)))
            {
                npc.KeepAttackX = Integer(instruction, 1);
                npc.KeepAttackY = Integer(instruction, 2);
            }
        }

        private void SetAllNpcScript(
            JxqyScriptInstruction instruction,
            bool death)
        {
            RequireParameterCount(instruction, 2);
            foreach (JxqyNpc npc in
                     Npcs.FindAll(Parameter(instruction, 0)))
            {
                if (death)
                    npc.DeathScriptAddress = Parameter(instruction, 1);
                else
                    npc.ScriptAddress = Parameter(instruction, 1);
            }
        }

        private void AddNpcProperty(
            JxqyScriptInstruction instruction)
        {
            RequireParameterCount(instruction, 3);
            string name = Parameter(instruction, 0);
            string property = Parameter(instruction, 1);
            int amount = Integer(instruction, 2);
            var targets = new List<JxqyCharacter>(
                Npcs.FindAll(name).Cast<JxqyCharacter>());
            if (string.Equals(
                    Player.Name,
                    name,
                    StringComparison.Ordinal))
            {
                targets.Add(Player);
            }
            foreach (JxqyCharacter target in targets)
                AddCharacterProperty(target, property, amount);
        }

        private static void AddCharacterProperty(
            JxqyCharacter character,
            string property,
            int amount)
        {
            switch (property)
            {
                case "Life":
                    character.Life += amount;
                    break;
                case "LifeMax":
                    character.LifeMax += amount;
                    break;
                case "Thew":
                    character.Thew += amount;
                    break;
                case "ThewMax":
                    character.ThewMax += amount;
                    break;
                case "Mana":
                    character.Mana += amount;
                    break;
                case "ManaMax":
                    character.ManaMax += amount;
                    break;
                case "Attack":
                    character.Attack += amount;
                    break;
                case "Attack2":
                    character.Attack2 += amount;
                    break;
                case "Attack3":
                    character.Attack3 += amount;
                    break;
                case "Defend":
                    character.Defend += amount;
                    break;
                case "Defend2":
                    character.Defend2 += amount;
                    break;
                case "Defend3":
                    character.Defend3 += amount;
                    break;
                case "Evade":
                    character.Evade += amount;
                    break;
                case "Level":
                    character.Level += amount;
                    break;
                case "Experience":
                case "Exp":
                    character.Experience += amount;
                    break;
                case "ExpBonus":
                    character.ExpBonus += amount;
                    break;
                case "AddMoveSpeedPercent":
                    character.AddMoveSpeedPercent += amount;
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Character property '{property}' is not numeric " +
                        "or does not exist in the original character model.");
            }
        }

        private JxqyScriptStep SetRetaliationMagicAsync(
            IReadOnlyList<JxqyCharacter> targets,
            string magicFileName,
            int direction)
        {
            var operation = new AsyncCommandOperation();
            operation.RunAsync(SetRetaliationMagicCoreAsync(
                    targets,
                    magicFileName,
                    direction))
                .Forget();
            return JxqyScriptStep.WaitFor(
                new JxqyPredicateScriptWait(
                    operation.ThrowIfFailedOrReturnCompleted));
        }

        private async UniTask SetRetaliationMagicCoreAsync(
            IReadOnlyList<JxqyCharacter> targets,
            string magicFileName,
            int direction)
        {
            if (targets == null || targets.Count == 0)
                return;
            var byLevel = new Dictionary<int, JxqyMagicDefinition>();
            foreach (JxqyCharacter target in targets)
            {
                int level = Math.Max(1, target.AttackLevel);
                if (!byLevel.TryGetValue(
                        level,
                        out JxqyMagicDefinition magic))
                {
                    magic = await Required(
                        _bindings.LoadMagicDefinitionAtLevelAsync)(
                        magicFileName,
                        level);
                    byLevel[level] = magic;
                }
                target.RetaliationMagicFileName = magicFileName;
                target.MagicToUseWhenBeAttacked =
                    magic.CreateRuntimeSnapshot();
                target.MagicDirectionWhenBeAttacked = direction;
            }
        }

        private JxqyScriptStep SetNpcAction(
            JxqyScriptInstruction instruction)
        {
            if (instruction.Parameters.Count != 2 &&
                instruction.Parameters.Count != 4)
            {
                throw new InvalidOperationException(
                    "SetNpcAction expects two or four parameters.");
            }
            JxqyCharacter target =
                FindCharacter(Parameter(instruction, 0));
            if (target == null)
                return JxqyScriptStep.Continue();
            JxqyCharacterState action =
                (JxqyCharacterState)Integer(instruction, 1);
            JxqyIntPoint destination = instruction.Parameters.Count == 4
                ? new JxqyIntPoint(
                    Integer(instruction, 2),
                    Integer(instruction, 3))
                : new JxqyIntPoint(0, 0);
            switch (action)
            {
                case JxqyCharacterState.Stand:
                case JxqyCharacterState.Stand1:
                    target.Stop();
                    target.SetState(action);
                    break;
                case JxqyCharacterState.Walk:
                    return MoveCharacter(
                        target,
                        destination,
                        run: false,
                        wait: false);
                case JxqyCharacterState.Run:
                    return MoveCharacter(
                        target,
                        destination,
                        run: true,
                        wait: false);
                case JxqyCharacterState.Jump:
                    return JumpCharacter(target, destination, wait: false);
                case JxqyCharacterState.Attack:
                case JxqyCharacterState.Attack1:
                case JxqyCharacterState.Attack2:
                {
                    // ScriptExecuter passes SetNpcAction attack coordinates
                    // directly as world pixels (unlike its Magic branch).
                    Required(_bindings.PerformNpcAttack)(
                        target,
                        new JxqyFloat2(destination.X, destination.Y));
                    return JxqyScriptStep.Continue();
                }
                case JxqyCharacterState.Magic:
                {
                    JxqyIntPoint world =
                        JxqyIsometricMapMath.TileToWorldPixel(
                            destination.X,
                            destination.Y);
                    Required(_bindings.PerformNpcMagic)(
                        target,
                        new JxqyFloat2(world.X, world.Y));
                    return JxqyScriptStep.Continue();
                }
                case JxqyCharacterState.Sit:
                    target.Stop();
                    target.SetState(action);
                    break;
                case JxqyCharacterState.Hurt:
                    if (JxqyDamageCalculator.ShouldEnterHurtState(
                            target,
                            _random.Next(0, 4)))
                    {
                        target.Stop();
                        target.SetState(action);
                    }
                    break;
                case JxqyCharacterState.Death:
                    target.Die();
                    break;
                case JxqyCharacterState.FightStand:
                    target.Stop();
                    target.SetFighting(true);
                    break;
                case JxqyCharacterState.FightWalk:
                    target.SetFighting(true);
                    return MoveCharacter(
                        target,
                        destination,
                        run: false,
                        wait: false);
                case JxqyCharacterState.FightRun:
                    target.SetFighting(true);
                    return MoveCharacter(
                        target,
                        destination,
                        run: true,
                        wait: false);
                case JxqyCharacterState.FightJump:
                    target.SetFighting(true);
                    return JumpCharacter(target, destination, wait: false);
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(action),
                        action,
                        "Unknown character action.");
            }
            Required(_bindings.RefreshActorVisual)(target);
            return JxqyScriptStep.Continue();
        }

        private void SetNpcActionType(
            JxqyScriptContext context,
            JxqyScriptInstruction instruction)
        {
            RequireParameterCount(instruction, 2);
            string name = Parameter(instruction, 0);
            JxqyCharacter target = string.IsNullOrEmpty(name)
                ? GetBelongObject(context) as JxqyCharacter
                : FindCharacter(name);
            if (target is JxqyNpc npc)
                npc.ActionType = Integer(instruction, 1);
        }

        private JxqyScriptStep AddGoodsAsync(
            UniTask<JxqyItemDefinition> loadTask)
        {
            var operation = new AsyncCommandOperation();
            operation.RunAsync(AddGoodsCoreAsync(loadTask)).Forget();
            return JxqyScriptStep.WaitFor(
                new JxqyPredicateScriptWait(
                    operation.ThrowIfFailedOrReturnCompleted));
        }

        private async UniTask AddGoodsCoreAsync(
            UniTask<JxqyItemDefinition> loadTask)
        {
            JxqyItemDefinition item = await loadTask;
            if (Inventory.Add(item))
            {
                _ui.NotifyInventoryChanged();
                _bindings.ShowMessage?.Invoke($"你获得了{item.Name}");
            }
        }

        private void DeleteGoods(
            JxqyScriptContext context,
            JxqyScriptInstruction instruction)
        {
            if (instruction.Parameters.Count > 1)
                throw new InvalidOperationException(
                    "DelGoods expects zero or one parameter.");
            string itemId;
            if (instruction.Parameters.Count == 1)
            {
                itemId = Parameter(instruction, 0);
            }
            else
            {
                object owner = GetBelongObject(context);
                itemId = owner switch
                {
                    JxqyInventoryEntry entry => entry.Definition.Id,
                    JxqyItemDefinition item => item.Id,
                    _ => string.Empty,
                };
            }
            if (string.IsNullOrEmpty(itemId))
                return;
            if (Inventory.Remove(itemId))
                _ui.NotifyInventoryChanged();
        }

        private void GetNpcCount(JxqyScriptInstruction instruction)
        {
            RequireParameterCount(instruction, 2);
            var kind = (JxqyCharacterKind)Integer(instruction, 0);
            var relation = (JxqyRelationType)Integer(instruction, 1);
            int count =
                Player.Kind == kind && Player.Relation == relation ? 1 : 0;
            count += Npcs.Npcs.Count(npc =>
                npc.Kind == kind &&
                npc.Relation == relation);
            _variables.Set("$NpcCount", count);
        }

        private JxqyScriptStep LearnMagicAsync(
            UniTask<JxqyMagicDefinition> loadTask)
        {
            var operation = new AsyncCommandOperation();
            operation.RunAsync(LearnMagicCoreAsync(loadTask)).Forget();
            return JxqyScriptStep.WaitFor(
                new JxqyPredicateScriptWait(
                    operation.ThrowIfFailedOrReturnCompleted));
        }

        private async UniTask LearnMagicCoreAsync(
            UniTask<JxqyMagicDefinition> loadTask)
        {
            JxqyMagicDefinition magic = await loadTask;
            if (Skills.Find(magic.Id) != null)
            {
                _bindings.ShowMessage?.Invoke(
                    $"你已经学会了{magic.Name}");
                return;
            }
            if (Skills.Learn(magic))
            {
                _bindings.ShowMessage?.Invoke(
                    $"你学会了{magic.Name}");
                return;
            }
            _bindings.ShowMessage?.Invoke("武功栏已满");
        }

        private void SetMagicLevel(
            JxqyScriptInstruction instruction)
        {
            RequireParameterCount(instruction, 2);
            // MagicListManager.SetNonReplaceMagicLevel silently returns when
            // the skill is absent; optional-route scripts rely on that.
            Skills.SetLevel(
                Parameter(instruction, 0),
                Integer(instruction, 1));
        }

        private void GetPartnerIndex(
            JxqyScriptInstruction instruction)
        {
            RequireParameterCount(instruction, 1);
            JxqyNpc partner = Npcs.Npcs.FirstOrDefault(npc =>
                npc.Kind == JxqyCharacterKind.Follower);
            int index = partner?.Name switch
            {
                "纳兰真" => 1,
                "月眉儿" => 2,
                "紫轩" => 3,
                "蔷薇" => 4,
                _ => 0,
            };
            _variables.Set(Parameter(instruction, 0), index);
        }

        private void Watch(JxqyScriptInstruction instruction)
        {
            if (instruction.Parameters.Count < 2 ||
                instruction.Parameters.Count > 3)
            {
                throw new InvalidOperationException(
                    "Watch expects two character names and an optional mode.");
            }
            JxqyCharacter first =
                FindCharacter(Parameter(instruction, 0));
            JxqyCharacter second =
                FindCharacter(Parameter(instruction, 1));
            // Legacy ScriptExecuter.Watch silently returns when either
            // character name cannot be resolved. The shipped 剑气峰 finale
            // intentionally contains Watch("杨影枫", "ALLNPC", X); its
            // second target is not an NPC and therefore acts as a no-op in
            // the original runtime. Treating it as a command fault prevents
            // the following 卓非凡 relation switch from starting the boss
            // fight at all.
            if (first == null || second == null)
                return;
            int mode = instruction.Parameters.Count == 3
                ? Integer(instruction, 2)
                : 0;
            if (mode == 0 || mode == 1)
                first.SetDirection(
                    second.PositionInWorld - first.PositionInWorld);
            if (mode == 0)
                second.SetDirection(
                    first.PositionInWorld - second.PositionInWorld);
            Required(_bindings.RefreshActorVisual)(first);
            Required(_bindings.RefreshActorVisual)(second);
        }

        private bool IsOccupiedByOtherNpc(
            JxqyIntPoint tile,
            JxqyCharacter moving)
        {
            if (!ReferenceEquals(moving, Player) &&
                Player.TilePosition.Equals(tile))
            {
                return true;
            }
            foreach (JxqyNpc npc in Npcs.Npcs)
            {
                if (!ReferenceEquals(npc, moving) &&
                    npc.Life > 0 &&
                    npc.Kind != JxqyCharacterKind.Flyer &&
                    npc.TilePosition.Equals(tile))
                {
                    return true;
                }
            }
            return false;
        }

        private void ResolveCharacterAndPair(
            JxqyScriptContext context,
            JxqyScriptInstruction instruction,
            out JxqyCharacter target,
            out int first,
            out int second)
        {
            if (instruction.Parameters.Count == 2)
            {
                target = GetBelongObject(context) as JxqyCharacter;
                first = Integer(instruction, 0);
                second = Integer(instruction, 1);
                return;
            }
            if (instruction.Parameters.Count == 3)
            {
                target = FindCharacter(Parameter(instruction, 0));
                first = Integer(instruction, 1);
                second = Integer(instruction, 2);
                return;
            }
            throw new InvalidOperationException(
                $"{instruction.Name} expects two or three parameters.");
        }

        private static void RequireParameterCount(
            JxqyScriptInstruction instruction,
            int count)
        {
            if (instruction.Parameters.Count != count)
            {
                throw new InvalidOperationException(
                    $"{instruction.Name} expects {count} parameters, got " +
                    $"{instruction.Parameters.Count}.");
            }
        }

        private JxqyScriptStep WaitFor(UniTask task)
        {
            var operation = new AsyncCommandOperation();
            operation.RunAsync(task).Forget();
            return JxqyScriptStep.WaitFor(
                new JxqyPredicateScriptWait(
                    operation.ThrowIfFailedOrReturnCompleted));
        }

        private void RequirePlayerTarget(
            JxqyScriptInstruction instruction,
            int index)
        {
            string name = Parameter(instruction, index);
            if (!string.Equals(
                    name,
                    Player.Name,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException(
                    $"{instruction.Name} target '{name}' is not the player.");
            }
        }

        private void EnsureDialogueAvailable()
        {
            if (_dialogueCompleted != null)
            {
                throw new InvalidOperationException(
                    "A legacy dialogue command is already waiting.");
            }
        }

        private void CancelDialogueWait()
        {
            if (_dialogueCompleted == null)
                return;
            _ui.DialogueCompleted -= _dialogueCompleted;
            _dialogueCompleted = null;
        }

        private void ParsePortraitCatalog(string text)
        {
            _portraits.Clear();
            string normalized = (text ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace('\r', '\n');
            bool inPortraitSection = false;
            foreach (string rawLine in normalized.Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 ||
                    line.StartsWith(
                        ";",
                        StringComparison.Ordinal) ||
                    line.StartsWith(
                        "//",
                        StringComparison.Ordinal))
                {
                    continue;
                }
                if (line.StartsWith(
                        "[",
                        StringComparison.Ordinal))
                {
                    inPortraitSection = string.Equals(
                        line,
                        "[PORTRAIT]",
                        StringComparison.OrdinalIgnoreCase);
                    continue;
                }
                if (!inPortraitSection)
                    continue;
                int separator = line.IndexOf('=');
                if (separator <= 0 ||
                    !int.TryParse(
                        line.Substring(0, separator).Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int id))
                {
                    continue;
                }
                string fileName =
                    line.Substring(separator + 1).Trim();
                if (id > 0 &&
                    fileName.EndsWith(
                        ".asf",
                        StringComparison.OrdinalIgnoreCase))
                {
                    _portraits[id] = fileName;
                }
            }
            if (_portraits.Count == 0)
            {
                throw new InvalidOperationException(
                    "Legacy portrait catalog contains no entries.");
            }
        }

        private string ResolvePortrait(
            JxqyScriptInstruction instruction)
        {
            if (instruction.Parameters.Count <= 1)
                return string.Empty;
            string value = Parameter(instruction, 1);
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            if (value.EndsWith(
                    ".asf",
                    StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
            string numeric = value.TrimEnd('_');
            if (!int.TryParse(
                    numeric,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int id) ||
                id <= 0)
            {
                return string.Empty;
            }
            if (_portraits.TryGetValue(id, out string fileName))
            {
                if (JxqyResourceAddressCatalog.TryResolveAnimationAddress(
                        fileName,
                        out _,
                        "portrait"))
                {
                    return fileName;
                }
                JxqyResourceAddressCatalog.ReportMissing(
                    "Say portrait",
                    $"{id} -> {fileName}");
                return string.Empty;
            }
            if (_missingPortraitWarnings.Add(id))
            {
                Debug.LogWarning(
                    $"JXQY-SCRIPT portrait id {id} is absent from " +
                    "HeadFile.ini; rendering dialogue without a portrait.");
            }
            return string.Empty;
        }

        private string ResolvePortrait(int id)
        {
            if (id <= 0)
                return string.Empty;
            if (_portraits.TryGetValue(id, out string fileName))
            {
                if (JxqyResourceAddressCatalog.TryResolveAnimationAddress(
                        fileName,
                        out _,
                        "portrait"))
                {
                    return fileName;
                }
                JxqyResourceAddressCatalog.ReportMissing(
                    "Talk portrait",
                    $"{id} -> {fileName}");
                return string.Empty;
            }
            if (_missingPortraitWarnings.Add(id))
            {
                Debug.LogWarning(
                    $"JXQY-SCRIPT portrait id {id} is absent from " +
                    "HeadFile.ini; rendering dialogue without a portrait.");
            }
            return string.Empty;
        }

        private async UniTask<JxqyScriptDocument> LoadDocumentAsync(
            string fileName,
            CancellationToken cancellationToken,
            JxqyScriptCategory category)
        {
            JxqyScriptResolution resolution = _resolver.Resolve(
                fileName,
                Required(_bindings.GetActiveMapName)(),
                category);
            if (!resolution.Found)
            {
                // Original ScriptParser logs a failed file read, returns an
                // invalid parser, and ScriptRunner immediately completes it.
                // Missing map-local hooks (notably shared NPC death-script
                // names) therefore must not fault the whole serial session.
                string attempted = string.Join(", ", resolution.AttemptedPaths);
                string warningKey = $"{fileName}|{attempted}";
                if (_missingScriptWarnings.Add(warningKey))
                {
                    Debug.LogWarning(
                        $"JXQY-SCRIPT missing legacy script treated as " +
                        $"an original-engine no-op: '{fileName}'. Tried: " +
                        attempted);
                }
                return JxqyScriptParser.Parse(
                    string.Empty,
                    resolution.AttemptedPaths.FirstOrDefault() ?? fileName);
            }
            using JxqyAssetLease<TextAsset> lease =
                await _resources.LoadAsync<TextAsset>(
                    resolution.ContentAddress,
                    _scope,
                    cancellationToken);
            return JxqyScriptParser.Parse(
                lease.Asset.text,
                resolution.RelativePath);
        }

        private static string Parameter(
            JxqyScriptInstruction instruction,
            int index)
        {
            if (instruction.Parameters.Count <= index)
            {
                throw new FormatException(
                    $"{instruction.Name} requires parameter {index + 1}.");
            }
            string value = instruction.Parameters[index].Trim();
            return value.Length >= 2 &&
                   value[0] == '"' &&
                   value[value.Length - 1] == '"'
                ? value.Substring(1, value.Length - 2)
                : value;
        }

        private static string OptionalParameter(
            JxqyScriptInstruction instruction,
            int index)
        {
            return instruction.Parameters.Count <= index
                ? string.Empty
                : Parameter(instruction, index);
        }

        private static object GetBelongObject(
            JxqyScriptContext context)
        {
            return (context?.Owner as ScriptInvocation)?.BelongObject;
        }

        private static int Integer(
            JxqyScriptInstruction instruction,
            int index)
        {
            return int.Parse(
                Parameter(instruction, index),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture);
        }

        private static T Required<T>(T value) where T : class
        {
            return value ?? throw new InvalidOperationException(
                $"Playable script binding '{typeof(T).Name}' is missing.");
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(
                    nameof(JxqyPlayableScriptSession));
        }

        private readonly struct QueuedSerialScript
        {
            public QueuedSerialScript(
                string fileName,
                CancellationToken cancellationToken,
                object belongObject)
            {
                FileName = fileName;
                CancellationToken = cancellationToken;
                BelongObject = belongObject;
            }

            public string FileName { get; }
            public CancellationToken CancellationToken { get; }
            public object BelongObject { get; }
        }

        private sealed class ScriptInvocation
        {
            private readonly JxqyPlayableScriptSession _session;
            private readonly ScriptInvocation _parent;
            private readonly JxqyScriptCategory _category;
            private ScriptInvocation _child;

            public ScriptInvocation(
                JxqyPlayableScriptSession session,
                ScriptInvocation parent,
                object belongObject,
                JxqyScriptCategory category = JxqyScriptCategory.Normal)
            {
                _session = session;
                _parent = parent;
                _category = category;
                BelongObject = belongObject;
            }

            public JxqyScriptRunner Runner { get; private set; }
            public Exception LoadException { get; private set; }
            public object BelongObject { get; }
            public bool IsFinished =>
                LoadException != null || Runner?.IsFinished == true;

            public async UniTask LoadAsync(
                string fileName,
                CancellationToken cancellationToken)
            {
                try
                {
                    JxqyScriptDocument document =
                        await _session.LoadDocumentAsync(
                            fileName,
                            cancellationToken,
                            _category);
                    var context = new JxqyScriptContext
                    {
                        Owner = this,
                    };
                    Runner = new JxqyScriptRunner(
                        document,
                        _session._registry,
                        context);
                }
                catch (Exception exception)
                {
                    LoadException = exception;
                    if (_parent == null)
                        throw;
                }
            }

            public ScriptInvocation BeginChild(string fileName)
            {
                if (_child != null && !_child.IsFinished)
                {
                    throw new InvalidOperationException(
                        "A nested legacy script is already running.");
                }
                _child = new ScriptInvocation(
                    _session,
                    this,
                    BelongObject);
                _child.LoadAsync(
                        fileName,
                        _session._runCancellationToken)
                    .Forget();
                return _child;
            }

            public void Tick(double elapsedMilliseconds)
            {
                if (LoadException != null || Runner == null ||
                    Runner.IsFinished)
                    return;
                _child?.Tick(elapsedMilliseconds);
                Runner.Tick(elapsedMilliseconds);
            }

            public bool ThrowIfFailedOrReturnCompleted()
            {
                if (LoadException != null)
                    throw new InvalidOperationException(
                        "Nested script failed to load.",
                        LoadException);
                if (Runner == null)
                    return false;
                if (Runner.State == JxqyScriptRunnerState.Faulted)
                {
                    string detail = string.Join(
                        Environment.NewLine,
                        Runner.Diagnostics
                            .Where(item =>
                                item.Severity ==
                                JxqyScriptDiagnosticSeverity.Error)
                            .Select(item => item.ToString()));
                    throw new InvalidOperationException(
                        $"Nested script faulted.{Environment.NewLine}{detail}");
                }
                return Runner.State == JxqyScriptRunnerState.Completed;
            }
        }

        private sealed class ParallelInvocation
        {
            private readonly ScriptInvocation _invocation;
            private double _delayMilliseconds;
            private bool _loadFinished;

            public ParallelInvocation(
                JxqyPlayableScriptSession session,
                object belongObject,
                double delayMilliseconds)
            {
                _invocation = new ScriptInvocation(
                    session,
                    null,
                    belongObject);
                _delayMilliseconds = delayMilliseconds;
            }

            public bool IsFinished =>
                _loadFinished && _invocation.IsFinished;
            public string FileName { get; private set; } = string.Empty;
            public double RemainingDelayMilliseconds =>
                Math.Max(0, _delayMilliseconds);

            public async UniTask LoadAsync(
                string fileName,
                CancellationToken cancellationToken)
            {
                FileName = fileName ?? string.Empty;
                try
                {
                    await _invocation.LoadAsync(
                        fileName,
                        cancellationToken);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
                finally
                {
                    _loadFinished = true;
                }
            }

            public void Tick(double elapsedMilliseconds)
            {
                if (!_loadFinished || IsFinished)
                    return;
                _delayMilliseconds -= elapsedMilliseconds;
                if (_delayMilliseconds > 0)
                    return;
                _invocation.Tick(elapsedMilliseconds);
            }
        }

        private sealed class AsyncCommandOperation
        {
            private Exception _exception;
            private bool _completed;

            public async UniTask RunAsync(UniTask task)
            {
                try
                {
                    await task;
                }
                catch (Exception exception)
                {
                    _exception = exception;
                }
                finally
                {
                    _completed = true;
                }
            }

            public bool ThrowIfFailedOrReturnCompleted()
            {
                if (_exception != null)
                {
                    Exception root = _exception.GetBaseException();
                    throw new InvalidOperationException(
                        $"Legacy script command failed: " +
                        $"{root.GetType().Name}: {root.Message}",
                        _exception);
                }
                return _completed;
            }
        }
    }
}
