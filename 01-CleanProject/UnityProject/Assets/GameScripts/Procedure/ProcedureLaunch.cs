using Launcher;
using TEngine;
using YooAsset;
using ProcedureOwner = TEngine.IFsm<TEngine.IProcedureModule>;

namespace Procedure
{
    /// <summary>
    /// 流程 => 启动器。
    /// </summary>
    public class ProcedureLaunch : ProcedureBase
    {
        public override bool UseNativeDialog => true;
        
        private IAudioModule _audioModule;

        protected override void OnInit(ProcedureOwner procedureOwner)
        {
            _audioModule = ModuleSystem.GetModule<IAudioModule>();
            base.OnInit(procedureOwner);
        }

        protected override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);
            
            //热更新UI初始化
            LauncherMgr.Initialize();

            // 语言配置：设置当前使用的语言，如果不设置，则默认使用操作系统语言
            InitLanguageSettings();

            // 声音配置：根据用户配置数据，设置即将使用的声音选项
            InitSoundSettings();
        }

        protected override void OnUpdate(ProcedureOwner procedureOwner, float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(procedureOwner, elapseSeconds, realElapseSeconds);

            // 运行一帧即切换到 Splash 展示流程
            ChangeState<ProcedureSplash>(procedureOwner);
        }

        private void InitLanguageSettings()
        {
            if (_resourceModule.PlayMode == EPlayMode.EditorSimulateMode && RootModule.Instance.EditorLanguage == Language.Unspecified)
            {
                // 编辑器资源模式直接使用 Inspector 上设置的语言
                return;
            }

            ILocalizationModule localizationModule = ModuleSystem.GetModule<ILocalizationModule>();

            // 月影传说当前交付内容只有简体中文。不要跟随系统语言，
            // 也不要沿用模板工程遗留的 English PlayerPrefs，否则打包后
            // 会请求不存在的语言表并产生误导性警告。
            Language language = Language.ChineseSimplified;
            Utility.PlayerPrefs.SetString(
                Constant.Setting.Language,
                language.ToString());
            Utility.PlayerPrefs.Save();

            localizationModule.Language = language;
            Log.Info("Init language settings complete, current language is '{0}'.", language.ToString());
        }

        private void InitSoundSettings()
        {
            _audioModule.MusicEnable = !Utility.PlayerPrefs.GetBool(Constant.Setting.MusicMuted, false);
            _audioModule.MusicVolume = Utility.PlayerPrefs.GetFloat(Constant.Setting.MusicVolume, 1f);
            _audioModule.SoundEnable = !Utility.PlayerPrefs.GetBool(Constant.Setting.SoundMuted, false);
            _audioModule.SoundVolume = Utility.PlayerPrefs.GetFloat(Constant.Setting.SoundVolume, 1f);
            _audioModule.UISoundEnable = !Utility.PlayerPrefs.GetBool(Constant.Setting.UISoundMuted, false);
            _audioModule.UISoundVolume = Utility.PlayerPrefs.GetFloat(Constant.Setting.UISoundVolume, 1f);
            Log.Info("Init sound settings complete.");
        }
    }
}
