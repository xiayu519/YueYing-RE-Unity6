using UnityEngine;
using System.Collections.Generic;

namespace HtmlToUGUI
{
    /// <summary>
    /// UI 分辨率配置数据结构
    /// </summary>
    [System.Serializable]
    public class UIResolutionConfig
    {
        public string displayName;
        public Vector2 resolution;
    }

    /// <summary>
    /// HTML to UGUI 烘焙器全局配置 (ScriptableObject)
    /// 用于统一管理多分辨率预设与对应的 DSL 规范文档模板
    /// </summary>
    [CreateAssetMenu(fileName = "HtmlToUGUIConfig", menuName = "UI Architecture/HtmlToUGUI Config")]
    public class HtmlToUGUIConfig : ScriptableObject
    {
        [Header("支持的分辨率预设")]
        public List<UIResolutionConfig> supportedResolutions = new List<UIResolutionConfig>()
        {
            new UIResolutionConfig { displayName = "PC 横屏 (1920x1080)", resolution = new Vector2(1920, 1080) },
            new UIResolutionConfig { displayName = "Mobile 竖屏 (1080x1920)", resolution = new Vector2(1080, 1920) },
            new UIResolutionConfig { displayName = "Pad 横屏 (2048x1536)", resolution = new Vector2(2048, 1536) }
        };

        [Header("DSL 文档模板 (.md 文件)")]
        [Tooltip("请拖入包含 {WIDTH} 和 {HEIGHT} 占位符的 Markdown 模板文件")]
        public TextAsset dslTemplateAsset;

        [Header("智能 Prefab 烘焙")]
        [Tooltip("HTML 图片相对路径的根目录。留空时优先使用 JSON 文件所在目录。")]
        public string htmlImageSourceRoot = "";

        [Tooltip("HTML 图片导入到 Unity 工程内的目标目录。")]
        public string importedImageFolder = "Assets/AssetRaw/UIRaw/Raw/HtmlToUGUI";

        [Tooltip("启用基于 v2 元数据和几何启发的自适应 RectTransform 生成。")]
        public bool enableAdaptiveLayout = true;

        [Tooltip("当节点声明安全区适配时应用 safe-area 锚点/偏移。")]
        public bool applySafeAreaHints = true;

        [Tooltip("无法判定自适应规则并回退到固定坐标时输出警告。")]
        public bool warnOnSmartLayoutFallback = false;
    }
}
