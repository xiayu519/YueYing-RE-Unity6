using Cysharp.Threading.Tasks;
using Jxqy.Ports;
using Jxqy.UnityAdapters;
using Jxqy.Domain.Simulation;
using System.Threading;
using UnityEngine;

namespace Jxqy.Bootstrap
{
    internal sealed class JxqyRuntimeHost : MonoBehaviour
    {
        private const int WindowId = 0x4A585159;

        private string _status = string.Empty;
        private string _error = string.Empty;
        private Rect _windowRect = new Rect(16f, 16f, 520f, 160f);
        private JxqyApplicationLifecycle _lifecycle;
        private JxqyPlayableRuntime _playableRuntime;
        private CancellationTokenSource _playableCancellation;
        private JxqyYooAssetResourcePort _mapResources;
        private JxqyMapPreloadCoordinator _maps;
        private bool _applicationQuitting;
        private JxqyUnityAudioPort _audioPort;
        private JxqyUnityVideoPort _videoPort;
        private JxqyYooAssetResourcePort _mediaResources;
        private JxqyCombinedInputPort _combinedInput;

        public JxqyDesktopInputPort DesktopInput { get; private set; }
        public JxqyTouchInputPort TouchInput { get; private set; }
#if UNITY_ANDROID || UNITY_IOS
        public IJxqyInputPort ActiveInput => TouchInput;
#else
        public IJxqyInputPort ActiveInput =>
            _combinedInput ?? (IJxqyInputPort)DesktopInput;
#endif
        public int InputIntentCoverageMask =>
            _playableRuntime?.InputIntentCoverageMask ?? 0;
#if UNITY_EDITOR
        public bool AcceptanceVideoPlaying =>
            _videoPort?.IsPlaying == true;
        public bool AcceptanceVideoOverlayTopmost =>
            _videoPort?.IsOverlayTopmost == true;
        public Jxqy.Domain.World.JxqyIntPoint AcceptancePlayerTile =>
            _playableRuntime?.AcceptancePlayerTile ??
            new Jxqy.Domain.World.JxqyIntPoint(-1, -1);
        public Jxqy.Domain.World.JxqyIntPoint
            AcceptancePlayerDestination =>
                _playableRuntime?.AcceptancePlayerDestination ??
                new Jxqy.Domain.World.JxqyIntPoint(-1, -1);
        public bool AcceptancePlayerHasPath =>
            _playableRuntime?.AcceptancePlayerHasPath == true;
        public Jxqy.Domain.World.JxqyFloat2 AcceptancePlayerPosition =>
            _playableRuntime?.AcceptancePlayerPosition ??
            Jxqy.Domain.World.JxqyFloat2.Zero;
        public int AcceptancePlayerDirection =>
            _playableRuntime?.AcceptancePlayerDirection ?? -1;
        public bool AcceptancePlayerIsRunning =>
            _playableRuntime?.AcceptancePlayerIsRunning == true;
        public int AcceptanceLastPointerAcceptedFrame =>
            _playableRuntime?.AcceptanceLastPointerAcceptedFrame ?? -1;
        public bool AcceptanceLastPointerTurnedImmediately =>
            _playableRuntime?.
                AcceptanceLastPointerTurnedImmediately == true;
        public string AcceptanceActiveMapStableId =>
            _playableRuntime?.ActiveMapStableId ?? string.Empty;
        public string AcceptanceMapSwitchError =>
            _playableRuntime?.AcceptanceMapSwitchError ?? string.Empty;
        public string AcceptanceActorLoadError =>
            _playableRuntime?.AcceptanceActorLoadError ?? string.Empty;
        public bool AcceptanceActorLoadFinished =>
            _playableRuntime?.AcceptanceActorLoadFinished == true;
        public int AcceptanceNpcCount =>
            _playableRuntime?.AcceptanceNpcCount ?? 0;
        public int AcceptanceObjectCount =>
            _playableRuntime?.AcceptanceObjectCount ?? 0;
        public bool AcceptanceFirstActTombLoaded =>
            _playableRuntime?.AcceptanceFirstActTombLoaded == true;
        public int AcceptanceFirstActChestCount =>
            _playableRuntime?.AcceptanceFirstActChestCount ?? 0;
        public int AcceptanceInitialSkillCount =>
            _playableRuntime?.AcceptanceInitialSkillCount ?? 0;
        public bool AcceptanceInitialShortcutsEmpty =>
            _playableRuntime?.AcceptanceInitialShortcutsEmpty == true;
        public int AcceptanceWorldSoundCount =>
            _playableRuntime?.AcceptanceWorldSoundCount ?? 0;
        public bool AcceptanceInteractionStarted =>
            _playableRuntime?.AcceptanceInteractionStarted == true;
        public bool AcceptanceInteractionApplied =>
            _playableRuntime?.AcceptanceInteractionApplied == true;
        public string AcceptanceInteractionScript =>
            _playableRuntime?.AcceptanceInteractionScript ?? string.Empty;
        public int AcceptanceInteractionMoneyDelta =>
            _playableRuntime?.AcceptanceInteractionMoneyDelta ?? 0;
        public bool AcceptanceCombatApplied =>
            _playableRuntime?.AcceptanceCombatApplied == true;
        public bool AcceptanceCombatDropSpawned =>
            _playableRuntime?.AcceptanceCombatDropSpawned == true;
        public int AcceptanceCombatDamage =>
            _playableRuntime?.AcceptanceCombatDamage ?? 0;
        public int AcceptanceCombatExperienceDelta =>
            _playableRuntime?.AcceptanceCombatExperienceDelta ?? 0;
        public bool AcceptanceCombatLevelUpApplied =>
            _playableRuntime?.AcceptanceCombatLevelUpApplied == true;
        public bool AcceptanceCombatLevelUpNoticeShown =>
            _playableRuntime?.AcceptanceCombatLevelUpNoticeShown == true;
        public bool AcceptanceSaveLoadFinished =>
            _playableRuntime?.AcceptanceSaveLoadFinished == true;
        public bool AcceptanceSaveLoadPassed =>
            _playableRuntime?.AcceptanceSaveLoadPassed == true;
        public string AcceptanceSaveLoadError =>
            _playableRuntime?.AcceptanceSaveLoadError ?? string.Empty;
        public bool AcceptanceTrapTransitionFinished =>
            _playableRuntime?.AcceptanceTrapTransitionFinished == true;
        public string AcceptanceTrapTransitionError =>
            _playableRuntime?.AcceptanceTrapTransitionError ?? string.Empty;
        public string AcceptanceActiveNpcFileName =>
            _playableRuntime?.AcceptanceActiveNpcFileName ?? string.Empty;
        public bool AcceptanceMagicApplied =>
            _playableRuntime?.AcceptanceMagicApplied == true;
        public int AcceptanceMagicDamage =>
            _playableRuntime?.AcceptanceMagicDamage ?? 0;
        public bool AcceptanceRepeatedMagicFinished =>
            _playableRuntime?.AcceptanceRepeatedMagicFinished == true;
        public bool AcceptanceItemUsed =>
            _playableRuntime?.AcceptanceItemUsed == true;
        public bool AcceptanceEquipmentEquipped =>
            _playableRuntime?.AcceptanceEquipmentEquipped == true;
        public bool AcceptanceShopBought =>
            _playableRuntime?.AcceptanceShopBought == true;
        public bool AcceptanceShopSold =>
            _playableRuntime?.AcceptanceShopSold == true;
        public bool AcceptancePresentationApplied =>
            _playableRuntime?.AcceptancePresentationApplied == true;
        public int AcceptanceWeatherCommandCount =>
            _playableRuntime?.AcceptanceWeatherCommandCount ?? 0;
        public int AcceptanceFadeCommandCount =>
            _playableRuntime?.AcceptanceFadeCommandCount ?? 0;
        public bool AcceptanceHotkeyItemUsed =>
            _playableRuntime?.AcceptanceHotkeyItemUsed == true;
        public bool AcceptanceStoryFinished =>
            _playableRuntime?.AcceptanceStoryFinished == true;
        public bool AcceptanceStoryPassed =>
            _playableRuntime?.AcceptanceStoryPassed == true;
        public string AcceptanceStoryCheckpoint =>
            _playableRuntime?.AcceptanceStoryCheckpoint ?? string.Empty;
        public string AcceptanceStoryObservedMovie =>
            _playableRuntime?.AcceptanceStoryObservedMovie ?? string.Empty;
        public string AcceptanceStoryError =>
            _playableRuntime?.AcceptanceStoryError ?? string.Empty;
        public int AcceptanceCrowdCount =>
            _playableRuntime?.AcceptanceCrowdCount ?? 0;
        public int AcceptancePathPlansLastTick =>
            _playableRuntime?.AcceptancePathPlansLastTick ?? 0;
        public long AcceptancePathPlansTotal =>
            _playableRuntime?.AcceptancePathPlansTotal ?? 0;
        public int AcceptanceRendererCount =>
            _playableRuntime?.AcceptanceRendererCount ?? 0;
        public int AcceptanceRendererSpawnsLastFrame =>
            _playableRuntime?.AcceptanceRendererSpawnsLastFrame ?? 0;
        public int AcceptanceRendererUnspawnsLastFrame =>
            _playableRuntime?.AcceptanceRendererUnspawnsLastFrame ?? 0;
        public long AcceptanceManagedBytesLastUpdate =>
            _playableRuntime?.AcceptanceManagedBytesLastUpdate ?? 0;
        public long AcceptanceManagedBytesLastActorVisualTick =>
            _playableRuntime?.AcceptanceManagedBytesLastActorVisualTick ??
            0;
        public long AcceptanceManagedBytesLastFrameBuild =>
            _playableRuntime?.AcceptanceManagedBytesLastFrameBuild ?? 0;
        public long AcceptanceManagedBytesLastFrameSubmit =>
            _playableRuntime?.AcceptanceManagedBytesLastFrameSubmit ?? 0;

        public void PrepareMovementAcceptance()
        {
            _playableRuntime?.PrepareMovementAcceptance();
        }

        public void BeginAcceptanceMapSwitch(string legacyFileName)
        {
            _playableRuntime?.BeginAcceptanceMapSwitch(legacyFileName);
        }

        public void BeginAcceptanceActorLoad(
            string npcFileName,
            string objectFileName)
        {
            _playableRuntime?.BeginAcceptanceActorLoad(
                npcFileName,
                objectFileName);
        }

        public bool TryPrepareAcceptanceInteraction(
            out Jxqy.Domain.World.JxqyFloat2 pointer)
        {
            if (_playableRuntime == null)
            {
                pointer = default;
                return false;
            }
            return _playableRuntime.TryPrepareAcceptanceInteraction(
                out pointer);
        }

        public bool TryPrepareAcceptanceCombat()
        {
            return _playableRuntime?.TryPrepareAcceptanceCombat() == true;
        }

        public void BeginAcceptanceSaveLoad()
        {
            _playableRuntime?.BeginAcceptanceSaveLoad();
        }

        public void BeginAcceptanceTrapTransition(string scriptFileName)
        {
            _playableRuntime?.BeginAcceptanceTrapTransition(scriptFileName);
        }

        public bool TryPrepareAcceptanceMagic()
        {
            return _playableRuntime?.TryPrepareAcceptanceMagic() == true;
        }

        public bool TryGetAcceptanceMagicPointer(
            out Jxqy.Domain.World.JxqyFloat2 pointer)
        {
            pointer = default;
            return _playableRuntime != null &&
                   _playableRuntime.TryGetAcceptanceMagicPointer(
                       out pointer);
        }

        public bool PrepareAcceptanceItemSystems()
        {
            return _playableRuntime?.PrepareAcceptanceItemSystems() == true;
        }

        public bool BeginAcceptancePresentation()
        {
            return _playableRuntime?.BeginAcceptancePresentation() == true;
        }

        public bool PrepareAcceptanceKeyboardInput()
        {
            return _playableRuntime?.PrepareAcceptanceKeyboardInput() == true;
        }

        public bool BeginAcceptanceStoryCheckpoint(string checkpointId)
        {
            return _playableRuntime?.
                BeginAcceptanceStoryCheckpoint(checkpointId) == true;
        }

        public bool PrepareAcceptanceCrowdCombat(int enemyCount)
        {
            return _playableRuntime?.
                       PrepareAcceptanceCrowdCombat(enemyCount) == true;
        }

        public bool TryPrepareAcceptanceOcclusionProbe(
            out Jxqy.Domain.World.JxqyIntPoint playerTile,
            out int occluderScore)
        {
            if (_playableRuntime == null)
            {
                playerTile = default;
                occluderScore = 0;
                return false;
            }
            return _playableRuntime.TryPrepareAcceptanceOcclusionProbe(
                out playerTile,
                out occluderScore);
        }

        public void DriveAcceptanceStoryCheckpoint()
        {
            _playableRuntime?.DriveAcceptanceStoryCheckpoint(
                _videoPort?.LastRequestedAddress ?? string.Empty);
        }

        public bool TryGetReachableAcceptancePointer(
            out Jxqy.Domain.World.JxqyFloat2 pointer)
        {
            if (_playableRuntime == null)
            {
                pointer = default;
                return false;
            }
            return _playableRuntime.TryGetReachableAcceptancePointer(
                out pointer);
        }

        public bool TryGetBlockedAcceptancePointer(
            out Jxqy.Domain.World.JxqyFloat2 pointer,
            out Jxqy.Domain.World.JxqyIntPoint requestedDestination,
            out Jxqy.Domain.World.JxqyIntPoint resolvedDestination)
        {
            if (_playableRuntime == null)
            {
                pointer = default;
                requestedDestination = default;
                resolvedDestination = default;
                return false;
            }
            return _playableRuntime.TryGetBlockedAcceptancePointer(
                out pointer,
                out requestedDestination,
                out resolvedDestination);
        }

        public bool TryGetReachableAcceptancePointer(
            Jxqy.Domain.World.JxqyIntPoint excludedDestination,
            Jxqy.Domain.World.JxqyFloat2 excludedPointer,
            int excludedInitialDirection,
            out Jxqy.Domain.World.JxqyFloat2 pointer)
        {
            if (_playableRuntime == null)
            {
                pointer = default;
                return false;
            }
            return _playableRuntime.TryGetReachableAcceptancePointer(
                excludedDestination,
                excludedPointer,
                excludedInitialDirection,
                out pointer);
        }

        public byte[] CaptureAcceptanceWorldPng()
        {
            return _playableRuntime?.CaptureAcceptanceWorldPng() ??
                   System.Array.Empty<byte>();
        }
#endif

        public void InitializeLifecycle(
            IJxqyAudioPort audio = null,
            IJxqyVideoPort video = null,
            bool initializeMediaPorts = true)
        {
            DesktopInput ??= new JxqyDesktopInputPort();
            TouchInput ??= new JxqyTouchInputPort();
            _combinedInput ??= new JxqyCombinedInputPort(
                DesktopInput,
                TouchInput);
            JxqyTouchInputBridge.Port = TouchInput;
            if (initializeMediaPorts && audio == null)
            {
                _audioPort ??=
                    gameObject.AddComponent<JxqyUnityAudioPort>();
                _audioPort.Initialize(
                    TEngine.ModuleSystem.GetModule<
                        TEngine.IAudioModule>());
                audio = _audioPort;
            }
            if (initializeMediaPorts && video == null)
            {
                _mediaResources ??=
                    new JxqyYooAssetResourcePort();
                _videoPort ??=
                    gameObject.AddComponent<JxqyUnityVideoPort>();
                _videoPort.Initialize(_mediaResources);
                video = _videoPort;
            }
            _lifecycle ??= gameObject.AddComponent<
                JxqyApplicationLifecycle>();
            _lifecycle.Initialize(
                new JxqyUnityClock(),
                new[] { ActiveInput },
                audio,
                video);
        }

        public void SetStatus(string status)
        {
            _status = status ?? string.Empty;
        }

        public void SetError(string error)
        {
            _error = error ?? string.Empty;
        }

        public async UniTask StartPlayableRuntimeAsync()
        {
            if (_playableRuntime != null)
                return;
            TEngine.Debugger debugger =
                Object.FindAnyObjectByType<TEngine.Debugger>();
            if (debugger != null)
                debugger.enabled = false;
            _playableCancellation = new CancellationTokenSource();
            _mapResources = new JxqyYooAssetResourcePort();
            _maps = new JxqyMapPreloadCoordinator(
                _mapResources,
                new JxqyTengineMapScenePort(),
                keepLoadedSceneAsRuntimeShell: true);
            await _maps.LoadManifestAsync(_playableCancellation.Token);
            await _maps.SwitchMapAsync(
                JxqyPlayableRuntime.InitialMapStableId,
                new System.Progress<JxqyPreloadProgress>(value =>
                    SetStatus(
                        $"Loading {value.Phase} " +
                        $"{value.Normalized:P0}: " +
                        $"{value.CurrentAddress}")),
                _playableCancellation.Token);
            _playableRuntime =
                gameObject.AddComponent<JxqyPlayableRuntime>();
            await _playableRuntime.InitializeAsync(
                _maps,
                ActiveInput,
                SetStatus,
                _audioPort,
                _videoPort,
                _playableCancellation.Token);
        }

        public async UniTask<JxqyVerticalSlice>
            RunVerticalSliceValidationAsync(
                CancellationToken cancellationToken = default)
        {
            if (_maps == null)
                throw new System.InvalidOperationException(
                    "Jxqy map coordinator is not ready.");
            string validationRoot = System.IO.Path.Combine(
                Application.temporaryCachePath,
                "JxqyValidation",
                "VerticalSlice");
            var runtime = new JxqyVerticalSliceRuntime(
                _maps,
                new JxqySaveRepository(
                    new JxqyFilePersistencePort(validationRoot)));
            var progress =
                new System.Progress<JxqyPreloadProgress>(value =>
                    SetStatus(
                        $"Vertical slice {value.Phase} " +
                        $"{value.Normalized:P0}"));
            await runtime.StartNewGameAsync(
                progress,
                cancellationToken);
            runtime.CompleteFirstCombatAndDialogue();
            await runtime.SwitchToSecondMapAsync(
                progress,
                cancellationToken);
            await runtime.SaveMutateAndLoadAsync(
                7,
                cancellationToken);
            if (!runtime.Scenario.IsComplete)
                throw new System.InvalidOperationException(
                    "Vertical slice did not complete every checkpoint.");
            return runtime.Scenario;
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(_error))
                return;

            _windowRect = GUILayout.Window(WindowId, _windowRect, DrawWindow, "Jxqy Runtime");
        }

        private void OnApplicationQuit()
        {
            _applicationQuitting = true;
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(JxqyTouchInputBridge.Port, TouchInput))
                JxqyTouchInputBridge.Port = null;
            _combinedInput?.ResetTransientState();
            _combinedInput = null;
            TouchInput = null;
            _playableCancellation?.Cancel();
            _playableCancellation?.Dispose();
            _playableCancellation = null;
            if (_applicationQuitting)
                _maps?.DisposeForApplicationShutdown();
            else
                _maps?.Dispose();
            _maps = null;
            _mapResources?.Dispose();
            _mapResources = null;
            _videoPort?.Stop();
            _videoPort = null;
            _mediaResources?.Dispose();
            _mediaResources = null;
            _audioPort = null;
        }

        private void DrawWindow(int id)
        {
            GUILayout.Label(_status);
            if (!string.IsNullOrEmpty(_error))
            {
                Color previous = GUI.color;
                GUI.color = new Color(1f, 0.45f, 0.45f);
                GUILayout.Label(_error);
                GUI.color = previous;
            }

            GUI.DragWindow(new Rect(0f, 0f, _windowRect.width, 24f));
        }
    }
}
