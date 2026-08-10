using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jxqy.Domain.Persistence;
using Jxqy.Domain.Simulation;
using Jxqy.UnityAdapters;

namespace Jxqy.Bootstrap
{
    public sealed class JxqyVerticalSliceRuntime
    {
        private readonly JxqyMapPreloadCoordinator _maps;
        private readonly JxqySaveRepository _saves;

        public JxqyVerticalSliceRuntime(
            JxqyMapPreloadCoordinator maps,
            JxqySaveRepository saves,
            JxqyVerticalSlice scenario = null)
        {
            _maps = maps ?? throw new ArgumentNullException(nameof(maps));
            _saves = saves ?? throw new ArgumentNullException(nameof(saves));
            Scenario = scenario ?? new JxqyVerticalSlice();
        }

        public JxqyVerticalSlice Scenario { get; }

        public async UniTask StartNewGameAsync(
            IProgress<JxqyPreloadProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            await _maps.LoadManifestAsync(cancellationToken);
            await _maps.PreloadSharedAsync(
                "SharedCharacters",
                progress,
                cancellationToken);
            await _maps.PreloadSharedAsync(
                "UI",
                progress,
                cancellationToken);
            await _maps.SwitchMapAsync(
                JxqyVerticalSlice.FirstMapStableId,
                progress,
                cancellationToken);
            Scenario.StartNewGame();
        }

        public void CompleteFirstCombatAndDialogue()
        {
            Scenario.BeginFirstCombat();
            while (Scenario.Stage == JxqyVerticalSliceStage.FirstCombat)
                Scenario.AttackFirstEnemy();
            Scenario.Ui.Confirm();
        }

        public async UniTask SwitchToSecondMapAsync(
            IProgress<JxqyPreloadProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            await _maps.SwitchMapAsync(
                JxqyVerticalSlice.SecondMapStableId,
                progress,
                cancellationToken);
            Scenario.CompleteMapSwitch(
                JxqyVerticalSlice.SecondMapStableId);
        }

        public async UniTask SaveMutateAndLoadAsync(
            int slot,
            CancellationToken cancellationToken = default)
        {
            Scenario.CompleteItemSkillAndShopCheckpoints();
            JxqySaveGameData save = Scenario.CreateSave();
            await _saves.SaveAsync(slot, save, cancellationToken);
            Scenario.MutateAfterSave();
            JxqySaveGameData loaded =
                await _saves.LoadAsync(slot, cancellationToken);
            Scenario.RestoreSave(loaded);
        }
    }
}
