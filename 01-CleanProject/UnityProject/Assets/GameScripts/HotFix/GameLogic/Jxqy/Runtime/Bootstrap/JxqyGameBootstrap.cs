using Cysharp.Threading.Tasks;
using Jxqy.Domain.Presentation;
using Jxqy.Domain.Simulation;
using Jxqy.UnityAdapters;
using TEngine;
using UnityEngine;
using YooAsset;

namespace Jxqy.Bootstrap
{
    /// <summary>
    /// Hot-update entry point for the migrated Jxqy game.
    /// </summary>
    public static class JxqyGameBootstrap
    {
        private const string PersistenceProbeArgument =
            "-jxqy-persistence-probe";
        private const string PersistenceProbeRelativePath =
            "Validation/windows-player-smoke.txt";
        private static JxqyRuntimeHost _host;

        public static bool IsRunning => _host != null;
        public static bool IsContentReady { get; private set; }
        public static string LastError { get; private set; } = string.Empty;
        public static JxqyUiSession UiSession { get; private set; }
        public static event System.Action<JxqyUiSession> UiSessionReady;
        public static int InputIntentCoverageMask =>
            _host?.InputIntentCoverageMask ?? 0;
#if UNITY_EDITOR
        public const string AcceptanceDrugId =
            "acceptance-runtime-drug";
        public const string AcceptanceEquipmentId =
            "acceptance-runtime-equipment";
        public const string AcceptanceShopItemId =
            "acceptance-runtime-shop-item";
        public static bool AcceptanceVideoPlaying =>
            _host?.AcceptanceVideoPlaying == true;
        public static bool AcceptanceVideoOverlayTopmost =>
            _host?.AcceptanceVideoOverlayTopmost == true;
        public static Jxqy.Domain.World.JxqyIntPoint AcceptancePlayerTile =>
            _host?.AcceptancePlayerTile ??
            new Jxqy.Domain.World.JxqyIntPoint(-1, -1);
        public static Jxqy.Domain.World.JxqyIntPoint
            AcceptancePlayerDestination =>
                _host?.AcceptancePlayerDestination ??
                new Jxqy.Domain.World.JxqyIntPoint(-1, -1);
        public static bool AcceptancePlayerHasPath =>
            _host?.AcceptancePlayerHasPath == true;
        public static Jxqy.Domain.World.JxqyFloat2
            AcceptancePlayerPosition =>
                _host?.AcceptancePlayerPosition ??
                Jxqy.Domain.World.JxqyFloat2.Zero;
        public static int AcceptancePlayerDirection =>
            _host?.AcceptancePlayerDirection ?? -1;
        public static bool AcceptancePlayerIsRunning =>
            _host?.AcceptancePlayerIsRunning == true;
        public static int AcceptanceLastPointerAcceptedFrame =>
            _host?.AcceptanceLastPointerAcceptedFrame ?? -1;
        public static bool AcceptanceLastPointerTurnedImmediately =>
            _host?.AcceptanceLastPointerTurnedImmediately == true;
        public static string AcceptanceActiveMapStableId =>
            _host?.AcceptanceActiveMapStableId ?? string.Empty;
        public static string AcceptanceMapSwitchError =>
            _host?.AcceptanceMapSwitchError ?? string.Empty;
        public static string AcceptanceActorLoadError =>
            _host?.AcceptanceActorLoadError ?? string.Empty;
        public static bool AcceptanceActorLoadFinished =>
            _host?.AcceptanceActorLoadFinished == true;
        public static int AcceptanceNpcCount =>
            _host?.AcceptanceNpcCount ?? 0;
        public static int AcceptanceObjectCount =>
            _host?.AcceptanceObjectCount ?? 0;
        public static bool AcceptanceFirstActTombLoaded =>
            _host?.AcceptanceFirstActTombLoaded == true;
        public static int AcceptanceFirstActChestCount =>
            _host?.AcceptanceFirstActChestCount ?? 0;
        public static int AcceptanceInitialSkillCount =>
            _host?.AcceptanceInitialSkillCount ?? 0;
        public static bool AcceptanceInitialShortcutsEmpty =>
            _host?.AcceptanceInitialShortcutsEmpty == true;
        public static int AcceptanceWorldSoundCount =>
            _host?.AcceptanceWorldSoundCount ?? 0;
        public static bool AcceptanceInteractionStarted =>
            _host?.AcceptanceInteractionStarted == true;
        public static bool AcceptanceInteractionApplied =>
            _host?.AcceptanceInteractionApplied == true;
        public static string AcceptanceInteractionScript =>
            _host?.AcceptanceInteractionScript ?? string.Empty;
        public static int AcceptanceInteractionMoneyDelta =>
            _host?.AcceptanceInteractionMoneyDelta ?? 0;
        public static bool AcceptanceCombatApplied =>
            _host?.AcceptanceCombatApplied == true;
        public static bool AcceptanceCombatDropSpawned =>
            _host?.AcceptanceCombatDropSpawned == true;
        public static int AcceptanceCombatDamage =>
            _host?.AcceptanceCombatDamage ?? 0;
        public static int AcceptanceCombatExperienceDelta =>
            _host?.AcceptanceCombatExperienceDelta ?? 0;
        public static bool AcceptanceCombatLevelUpApplied =>
            _host?.AcceptanceCombatLevelUpApplied == true;
        public static bool AcceptanceCombatLevelUpNoticeShown =>
            _host?.AcceptanceCombatLevelUpNoticeShown == true;
        public static bool AcceptanceSaveLoadFinished =>
            _host?.AcceptanceSaveLoadFinished == true;
        public static bool AcceptanceSaveLoadPassed =>
            _host?.AcceptanceSaveLoadPassed == true;
        public static string AcceptanceSaveLoadError =>
            _host?.AcceptanceSaveLoadError ?? string.Empty;
        public static bool AcceptanceTrapTransitionFinished =>
            _host?.AcceptanceTrapTransitionFinished == true;
        public static string AcceptanceTrapTransitionError =>
            _host?.AcceptanceTrapTransitionError ?? string.Empty;
        public static string AcceptanceActiveNpcFileName =>
            _host?.AcceptanceActiveNpcFileName ?? string.Empty;
        public static bool AcceptanceMagicApplied =>
            _host?.AcceptanceMagicApplied == true;
        public static int AcceptanceMagicDamage =>
            _host?.AcceptanceMagicDamage ?? 0;
        public static bool AcceptanceRepeatedMagicFinished =>
            _host?.AcceptanceRepeatedMagicFinished == true;
        public static bool AcceptanceItemUsed =>
            _host?.AcceptanceItemUsed == true;
        public static bool AcceptanceEquipmentEquipped =>
            _host?.AcceptanceEquipmentEquipped == true;
        public static bool AcceptanceShopBought =>
            _host?.AcceptanceShopBought == true;
        public static bool AcceptanceShopSold =>
            _host?.AcceptanceShopSold == true;
        public static bool AcceptancePresentationApplied =>
            _host?.AcceptancePresentationApplied == true;
        public static int AcceptanceWeatherCommandCount =>
            _host?.AcceptanceWeatherCommandCount ?? 0;
        public static int AcceptanceFadeCommandCount =>
            _host?.AcceptanceFadeCommandCount ?? 0;
        public static bool AcceptanceHotkeyItemUsed =>
            _host?.AcceptanceHotkeyItemUsed == true;
        public static bool AcceptanceStoryFinished =>
            _host?.AcceptanceStoryFinished == true;
        public static bool AcceptanceStoryPassed =>
            _host?.AcceptanceStoryPassed == true;
        public static string AcceptanceStoryCheckpoint =>
            _host?.AcceptanceStoryCheckpoint ?? string.Empty;
        public static string AcceptanceStoryObservedMovie =>
            _host?.AcceptanceStoryObservedMovie ?? string.Empty;
        public static string AcceptanceStoryError =>
            _host?.AcceptanceStoryError ?? string.Empty;
        public static int AcceptanceCrowdCount =>
            _host?.AcceptanceCrowdCount ?? 0;
        public static int AcceptancePathPlansLastTick =>
            _host?.AcceptancePathPlansLastTick ?? 0;
        public static long AcceptancePathPlansTotal =>
            _host?.AcceptancePathPlansTotal ?? 0;
        public static int AcceptanceRendererCount =>
            _host?.AcceptanceRendererCount ?? 0;
        public static int AcceptanceRendererSpawnsLastFrame =>
            _host?.AcceptanceRendererSpawnsLastFrame ?? 0;
        public static int AcceptanceRendererUnspawnsLastFrame =>
            _host?.AcceptanceRendererUnspawnsLastFrame ?? 0;
        public static long AcceptanceManagedBytesLastUpdate =>
            _host?.AcceptanceManagedBytesLastUpdate ?? 0;
        public static long AcceptanceManagedBytesLastActorVisualTick =>
            _host?.AcceptanceManagedBytesLastActorVisualTick ?? 0;
        public static long AcceptanceManagedBytesLastFrameBuild =>
            _host?.AcceptanceManagedBytesLastFrameBuild ?? 0;
        public static long AcceptanceManagedBytesLastFrameSubmit =>
            _host?.AcceptanceManagedBytesLastFrameSubmit ?? 0;

        public static void PrepareMovementAcceptance()
        {
            _host?.PrepareMovementAcceptance();
        }

        public static void BeginAcceptanceMapSwitch(
            string legacyFileName)
        {
            _host?.BeginAcceptanceMapSwitch(legacyFileName);
        }

        public static void BeginAcceptanceActorLoad(
            string npcFileName,
            string objectFileName)
        {
            _host?.BeginAcceptanceActorLoad(
                npcFileName,
                objectFileName);
        }

        public static bool TryPrepareAcceptanceInteraction(
            out Jxqy.Domain.World.JxqyFloat2 pointer)
        {
            if (_host == null)
            {
                pointer = default;
                return false;
            }
            return _host.TryPrepareAcceptanceInteraction(out pointer);
        }

        public static bool TryPrepareAcceptanceCombat()
        {
            return _host?.TryPrepareAcceptanceCombat() == true;
        }

        public static void BeginAcceptanceSaveLoad()
        {
            _host?.BeginAcceptanceSaveLoad();
        }

        public static void BeginAcceptanceTrapTransition(
            string scriptFileName)
        {
            _host?.BeginAcceptanceTrapTransition(scriptFileName);
        }

        public static bool TryPrepareAcceptanceMagic()
        {
            return _host?.TryPrepareAcceptanceMagic() == true;
        }

        public static bool TryGetAcceptanceMagicPointer(
            out Jxqy.Domain.World.JxqyFloat2 pointer)
        {
            pointer = default;
            return _host != null &&
                   _host.TryGetAcceptanceMagicPointer(out pointer);
        }

        public static bool PrepareAcceptanceItemSystems()
        {
            return _host?.PrepareAcceptanceItemSystems() == true;
        }

        public static bool BeginAcceptancePresentation()
        {
            return _host?.BeginAcceptancePresentation() == true;
        }

        public static bool PrepareAcceptanceKeyboardInput()
        {
            return _host?.PrepareAcceptanceKeyboardInput() == true;
        }

        public static bool BeginAcceptanceStoryCheckpoint(
            string checkpointId)
        {
            return _host?.BeginAcceptanceStoryCheckpoint(checkpointId) ==
                   true;
        }

        public static void DriveAcceptanceStoryCheckpoint()
        {
            _host?.DriveAcceptanceStoryCheckpoint();
        }

        public static bool PrepareAcceptanceCrowdCombat(int enemyCount)
        {
            return _host?.PrepareAcceptanceCrowdCombat(enemyCount) == true;
        }

        public static bool TryPrepareAcceptanceOcclusionProbe(
            out Jxqy.Domain.World.JxqyIntPoint playerTile,
            out int occluderScore)
        {
            if (_host == null)
            {
                playerTile = default;
                occluderScore = 0;
                return false;
            }
            return _host.TryPrepareAcceptanceOcclusionProbe(
                out playerTile,
                out occluderScore);
        }

        public static bool TryGetReachableAcceptancePointer(
            out Jxqy.Domain.World.JxqyFloat2 pointer)
        {
            if (_host == null)
            {
                pointer = default;
                return false;
            }
            return _host.TryGetReachableAcceptancePointer(out pointer);
        }

        public static bool TryGetReachableAcceptancePointer(
            Jxqy.Domain.World.JxqyIntPoint excludedDestination,
            Jxqy.Domain.World.JxqyFloat2 excludedPointer,
            int excludedInitialDirection,
            out Jxqy.Domain.World.JxqyFloat2 pointer)
        {
            if (_host == null)
            {
                pointer = default;
                return false;
            }
            return _host.TryGetReachableAcceptancePointer(
                excludedDestination,
                excludedPointer,
                excludedInitialDirection,
                out pointer);
        }

        public static bool TryGetBlockedAcceptancePointer(
            out Jxqy.Domain.World.JxqyFloat2 pointer,
            out Jxqy.Domain.World.JxqyIntPoint requestedDestination,
            out Jxqy.Domain.World.JxqyIntPoint resolvedDestination)
        {
            if (_host == null)
            {
                pointer = default;
                requestedDestination = default;
                resolvedDestination = default;
                return false;
            }
            return _host.TryGetBlockedAcceptancePointer(
                out pointer,
                out requestedDestination,
                out resolvedDestination);
        }

        public static byte[] CaptureAcceptanceWorldPng()
        {
            return _host?.CaptureAcceptanceWorldPng() ??
                   System.Array.Empty<byte>();
        }
#endif

        public static UniTask<JxqyVerticalSlice>
            RunVerticalSliceValidationAsync(
                System.Threading.CancellationToken cancellationToken =
                    default)
        {
            if (_host == null)
                throw new System.InvalidOperationException(
                    "Jxqy runtime is not running.");
            return _host.RunVerticalSliceValidationAsync(
                cancellationToken);
        }

        internal static void NotifyUiSessionReady(JxqyUiSession session)
        {
            UiSession = session;
            UiSessionReady?.Invoke(session);
        }

        public static void Start()
        {
            if (_host != null)
                return;

            IsContentReady = false;
            LastError = string.Empty;
            var root = new GameObject("[JxqyRuntime]");
            if (Application.isPlaying)
                Object.DontDestroyOnLoad(root);
            _host = root.AddComponent<JxqyRuntimeHost>();
            _host.InitializeLifecycle(
                initializeMediaPorts: Application.isPlaying);
            StartPersistenceProbeIfRequested();
            if (Application.isPlaying)
            {
                _host.SetStatus(
                    "Initializing Jxqy YooAsset package...");
                InitializePackageAsync().Forget();
            }
            else
            {
                _host.SetStatus(
                    "Jxqy edit-mode bootstrap harness is ready.");
            }
        }

        private static void StartPersistenceProbeIfRequested()
        {
            if (!Application.isPlaying ||
                !HasCommandLineArgument(PersistenceProbeArgument))
                return;
            RunPersistenceProbeAsync(
                    new JxqyFilePersistencePort())
                .Forget();
        }

        private static bool HasCommandLineArgument(string expected)
        {
            foreach (string argument in
                     System.Environment.GetCommandLineArgs())
            {
                if (string.Equals(
                        argument,
                        expected,
                        System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static async UniTaskVoid RunPersistenceProbeAsync(
            JxqyFilePersistencePort persistence)
        {
            try
            {
                byte[] payload = System.Text.Encoding.UTF8.GetBytes(
                    $"Jxqy Windows player persistence probe " +
                    $"{System.DateTime.UtcNow:O}\n");
                await persistence.WriteAtomicAsync(
                    PersistenceProbeRelativePath,
                    payload);
                byte[] reloaded = await persistence.ReadAsync(
                    PersistenceProbeRelativePath);
                if (!System.Linq.Enumerable.SequenceEqual(
                        payload,
                        reloaded))
                    throw new System.IO.IOException(
                        "Persistence probe read-back differed.");
                Log.Info(
                    "Jxqy persistence validation succeeded: " +
                    $"{persistence.RootPath}/" +
                    $"{PersistenceProbeRelativePath}");
            }
            catch (System.Exception exception)
            {
                ReportFatalError(
                    "Jxqy persistence validation failed: " +
                    exception.Message);
            }
        }

        public static void Shutdown()
        {
            IsContentReady = false;
            LastError = string.Empty;
            UiSession = null;
            if (_host == null)
                return;

            if (Application.isPlaying)
                Object.Destroy(_host.gameObject);
            else
                Object.DestroyImmediate(_host.gameObject);
            _host = null;
        }

        public static void ReportFatalError(string message)
        {
            if (_host == null)
                Start();

            IsContentReady = false;
            LastError = message ?? string.Empty;
            _host.SetError(LastError);
            Log.Error(LastError);
        }

        private static async UniTaskVoid InitializePackageAsync()
        {
            try
            {
                ResourcePackage existing = YooAssets.TryGetPackage(
                    JxqyResourceLocations.PackageName);
                if (existing == null ||
                    existing.InitializeStatus != EOperationStatus.Succeed)
                {
                    IResourceModule resources =
                        ModuleSystem.GetModule<IResourceModule>();
                    if (resources == null)
                        throw new System.InvalidOperationException(
                            "TEngine resource module is unavailable.");
                    InitializationOperation operation =
                        await resources.InitPackage(
                            JxqyResourceLocations.PackageName,
                            needInitMainFest: true);
                    if (operation == null ||
                        operation.Status != EOperationStatus.Succeed)
                        throw new System.InvalidOperationException(
                            $"YooAsset package " +
                            $"'{JxqyResourceLocations.PackageName}' " +
                            $"failed to initialize: " +
                            $"{operation?.Error ?? "no operation"}");
                }

                if (_host == null)
                    return;
                await _host.StartPlayableRuntimeAsync();
                if (_host == null)
                    return;
                _host.SetStatus(
                    "Jxqy Unity Editor playable runtime is ready.");
                IsContentReady = true;
                LastError = string.Empty;
            }
            catch (System.Exception exception)
            {
                ReportFatalError(
                    $"Jxqy content package initialization failed: " +
                    exception.Message);
            }
        }
    }
}
