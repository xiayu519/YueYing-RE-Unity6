using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using TEngine.Editor.UI;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace HtmlToUGUI
{
    [System.Serializable]
    public class UIDataNode
    {
        public string name;
        public string type;
        public string dir;
        public float value;
        public bool isChecked;
        public List<string> options;
        public float x;
        public float y;
        public float width;
        public float height;
        public string color;
        public string fontColor;
        public int fontSize;
        public string textAlign;
        public string text;
        public List<UIDataNode> children;

        // JSON v2 additive metadata. Missing values keep v1 behavior.
        public int schemaVersion;
        public float designWidth;
        public float designHeight;
        public string sourcePath;
        public string sourceDirectory;
        public string htmlFilePath;
        public string imageSrc;
        public string backgroundImageSrc;
        public string imageFit;
        public string layoutHint;
        public string safeArea;
        public string anchorPreset;
        public string anchorMin;
        public string anchorMax;
        public string pivot;
        public string offsetMin;
        public string offsetMax;
        public string cssPosition;
        public string cssLeft;
        public string cssRight;
        public string cssTop;
        public string cssBottom;
        public string cssWidth;
        public string cssHeight;
        public string cssObjectFit;
        public string cssBackgroundSize;
    }

    public class HtmlToUGUIBaker : EditorWindow
    {
        private class BakeContext
        {
            public int schemaVersion;
            public bool isV2;
            public Vector2 designSize;
            public string jsonDirectory;
            public string sourceDirectory;
            public bool enableAdaptiveLayout;
            public bool applySafeAreaHints;
            public bool warnOnSmartLayoutFallback;
        }

        private enum InputMode
        {
            FileAsset,
            RawString
        }

        private InputMode currentMode = InputMode.FileAsset;
        private TextAsset jsonAsset;
        private string rawJsonString = "";
        private Vector2 mainScrollPosition;
        private Vector2 scrollPosition;
        private Canvas targetCanvas;
        private bool autoFindCanvas = true;
        private bool clearSameNameBeforeBake = false;
        private bool selectGeneratedRoot = true;
        private bool useScriptGeneratorNaming = true;
        private string lastJsonSummary = "";
        private MessageType lastJsonSummaryType = MessageType.None;

        // 外部工具链配置
        private string converterUrl = "";
        private const string PREFS_URL_KEY = "HtmlToUGUIBaker_ConverterUrl";

        // 分辨率与 DSL 配置
        private HtmlToUGUIConfig config;
        private int selectedResolutionIndex = 0;
        private const string PREFS_CONFIG_PATH_KEY = "HtmlToUGUIBaker_ConfigPath";
        private const string PREFS_RES_INDEX_KEY = "HtmlToUGUIBaker_ResIndex";
        private const string PREFS_AUTO_FIND_CANVAS_KEY = "HtmlToUGUIBaker_AutoFindCanvas";
        private const string PREFS_CLEAR_SAME_NAME_KEY = "HtmlToUGUIBaker_ClearSameName";
        private const string PREFS_SELECT_GENERATED_KEY = "HtmlToUGUIBaker_SelectGenerated";
        private const string PREFS_USE_SCRIPT_GENERATOR_NAMING_KEY = "HtmlToUGUIBaker_UseScriptGeneratorNaming";

        // 文本组件偏好配置
        private bool useLegacyText = false;
        private const string PREFS_USE_LEGACY_TEXT_KEY = "HtmlToUGUIBaker_UseLegacyText";

        // 用于在当前窗口内嵌绘制 SO 属性的序列化对象
        private SerializedObject configSO;
        private SerializedProperty resolutionsProp;
        private SerializedProperty dslTemplateAssetProp;
        private SerializedProperty htmlImageSourceRootProp;
        private SerializedProperty importedImageFolderProp;
        private SerializedProperty enableAdaptiveLayoutProp;
        private SerializedProperty applySafeAreaHintsProp;
        private SerializedProperty warnOnSmartLayoutFallbackProp;

        [MenuItem("Tools/UI Architecture/HTML to UGUI Baker (Full Controls)")]
        public static void ShowWindow()
        {
            GetWindow<HtmlToUGUIBaker>("UI 原型烘焙器");
        }

        private void OnEnable()
        {
            converterUrl = EditorPrefs.GetString(PREFS_URL_KEY, "");
            string configPath = EditorPrefs.GetString(PREFS_CONFIG_PATH_KEY, "");
            if (!string.IsNullOrEmpty(configPath))
            {
                config = AssetDatabase.LoadAssetAtPath<HtmlToUGUIConfig>(configPath);
            }

            selectedResolutionIndex = EditorPrefs.GetInt(PREFS_RES_INDEX_KEY, 0);
            useLegacyText = EditorPrefs.GetBool(PREFS_USE_LEGACY_TEXT_KEY, false);
            autoFindCanvas = EditorPrefs.GetBool(PREFS_AUTO_FIND_CANVAS_KEY, true);
            clearSameNameBeforeBake = EditorPrefs.GetBool(PREFS_CLEAR_SAME_NAME_KEY, false);
            selectGeneratedRoot = EditorPrefs.GetBool(PREFS_SELECT_GENERATED_KEY, true);
            useScriptGeneratorNaming = EditorPrefs.GetBool(PREFS_USE_SCRIPT_GENERATOR_NAMING_KEY, true);

            if (config == null)
            {
                AutoFindConfig();
            }

            if (string.IsNullOrWhiteSpace(converterUrl))
            {
                AutoFindConverterPage();
            }

            if (autoFindCanvas && targetCanvas == null)
            {
                targetCanvas = FindBestCanvas();
            }
        }

        private void OnGUI()
        {
            mainScrollPosition = EditorGUILayout.BeginScrollView(mainScrollPosition);

            EditorGUILayout.LabelField("HTML To UGUI Baker", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("将 UI-DSL JSON 烘焙为 UGUI 节点树。v2 JSON 会启用图片绑定和自适应布局，v1 JSON 保持固定坐标兼容。", MessageType.Info);

            DrawConfigUI();
            DrawExternalToolchainUI();
            DrawTargetCanvasUI();
            DrawBakeOptionsUI();
            DrawJsonInputUI();
            DrawJsonSummaryUI();
            DrawBakeActionsUI();

            EditorGUILayout.EndScrollView();
        }

        #region UI 绘制逻辑

        private void DrawConfigUI()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("配置", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUI.BeginChangeCheck();
            config = (HtmlToUGUIConfig)EditorGUILayout.ObjectField("配置文件 (SO)", config, typeof(HtmlToUGUIConfig),
                false);
            if (EditorGUI.EndChangeCheck())
            {
                string path = config != null ? AssetDatabase.GetAssetPath(config) : "";
                EditorPrefs.SetString(PREFS_CONFIG_PATH_KEY, path);
                selectedResolutionIndex = 0;
                EditorPrefs.SetInt(PREFS_RES_INDEX_KEY, selectedResolutionIndex);
                configSO = null;
            }

            if (config == null)
            {
                EditorGUILayout.HelpBox("未找到 HtmlToUGUIConfig。可以自动查找现有配置，或在 Project 中创建一个配置资源。", MessageType.Warning);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("自动查找配置"))
                {
                    AutoFindConfig();
                }

                if (GUILayout.Button("创建配置"))
                {
                    CreateConfigAsset();
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }

            if (configSO == null || configSO.targetObject != config)
            {
                configSO = new SerializedObject(config);
                resolutionsProp = configSO.FindProperty("supportedResolutions");
                dslTemplateAssetProp = configSO.FindProperty("dslTemplateAsset");
                htmlImageSourceRootProp = configSO.FindProperty("htmlImageSourceRoot");
                importedImageFolderProp = configSO.FindProperty("importedImageFolder");
                enableAdaptiveLayoutProp = configSO.FindProperty("enableAdaptiveLayout");
                applySafeAreaHintsProp = configSO.FindProperty("applySafeAreaHints");
                warnOnSmartLayoutFallbackProp = configSO.FindProperty("warnOnSmartLayoutFallback");
            }

            configSO.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(resolutionsProp, new GUIContent("分辨率预设"), true);
            EditorGUILayout.PropertyField(dslTemplateAssetProp, new GUIContent("DSL 模板"));
            EditorGUILayout.PropertyField(htmlImageSourceRootProp, new GUIContent("HTML 图片根目录"));
            EditorGUILayout.PropertyField(importedImageFolderProp, new GUIContent("图片导入目录"));
            EditorGUILayout.PropertyField(enableAdaptiveLayoutProp, new GUIContent("启用自适应布局"));
            EditorGUILayout.PropertyField(applySafeAreaHintsProp, new GUIContent("应用安全区标记"));
            EditorGUILayout.PropertyField(warnOnSmartLayoutFallbackProp, new GUIContent("回退固定坐标时警告"));
            if (EditorGUI.EndChangeCheck())
            {
                configSO.ApplyModifiedProperties();
                EditorUtility.SetDirty(config);
            }

            if (config.supportedResolutions == null || config.supportedResolutions.Count == 0)
            {
                EditorGUILayout.HelpBox("配置文件中未定义任何分辨率数据，请点击上方列表的 '+' 号添加。", MessageType.Error);
                EditorGUILayout.EndVertical();
                return;
            }

            selectedResolutionIndex = Mathf.Clamp(selectedResolutionIndex, 0, config.supportedResolutions.Count - 1);
            string[] resNames = new string[config.supportedResolutions.Count];
            for (int i = 0; i < config.supportedResolutions.Count; i++)
            {
                resNames[i] = config.supportedResolutions[i].displayName;
            }

            EditorGUI.BeginChangeCheck();
            selectedResolutionIndex = EditorGUILayout.Popup("目标分辨率", selectedResolutionIndex, resNames);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetInt(PREFS_RES_INDEX_KEY, selectedResolutionIndex);
            }

            if (GUILayout.Button("复制对应分辨率的 DSL 规范文档", GUILayout.Height(25)))
            {
                CopyDSLToClipboard();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawExternalToolchainUI()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("HTML 转换器", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUI.BeginChangeCheck();
            converterUrl = EditorGUILayout.TextField("路径 / URL", converterUrl);
            if (EditorGUI.EndChangeCheck()) EditorPrefs.SetString(PREFS_URL_KEY, converterUrl);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("浏览...", GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFilePanel("选择 HTML 转换器", "", "html");
                if (!string.IsNullOrEmpty(path))
                {
                    converterUrl = "file:///" + path.Replace("\\", "/");
                    EditorPrefs.SetString(PREFS_URL_KEY, converterUrl);
                    GUI.FocusControl(null);
                }
            }

            if (GUILayout.Button("自动查找", GUILayout.Width(80)))
            {
                AutoFindConverterPage();
            }

            if (GUILayout.Button("在浏览器中打开", GUILayout.Width(120)))
            {
                if (string.IsNullOrWhiteSpace(converterUrl))
                {
                    Debug.LogError("[HtmlToUGUIBaker] 唤起中断: 转换器路径或 URL 为空，请先配置路径或点击浏览选择文件。");
                    return;
                }

                Application.OpenURL(converterUrl);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawTargetCanvasUI()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("目标 Canvas", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            targetCanvas = (Canvas)EditorGUILayout.ObjectField("Canvas", targetCanvas, typeof(Canvas), true);

            EditorGUI.BeginChangeCheck();
            autoFindCanvas = EditorGUILayout.ToggleLeft("烘焙前自动查找场景 Canvas", autoFindCanvas);
            if (EditorGUI.EndChangeCheck()) EditorPrefs.SetBool(PREFS_AUTO_FIND_CANVAS_KEY, autoFindCanvas);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("查找场景 Canvas"))
            {
                targetCanvas = FindBestCanvas();
                if (targetCanvas == null) Debug.LogWarning("[HtmlToUGUIBaker] 当前场景未找到 Canvas。");
            }

            if (GUILayout.Button("创建默认 Canvas"))
            {
                targetCanvas = CreateDefaultCanvas();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawBakeOptionsUI()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("烘焙选项", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUI.BeginChangeCheck();
            useLegacyText = EditorGUILayout.ToggleLeft("使用旧版 Text (Legacy)", useLegacyText);
            useScriptGeneratorNaming = EditorGUILayout.ToggleLeft("按 ScriptGeneratorSetting 生成节点名称", useScriptGeneratorNaming);
            clearSameNameBeforeBake = EditorGUILayout.ToggleLeft("烘焙前清理 Canvas 下同名根节点", clearSameNameBeforeBake);
            selectGeneratedRoot = EditorGUILayout.ToggleLeft("烘焙完成后选中新根节点", selectGeneratedRoot);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool(PREFS_USE_LEGACY_TEXT_KEY, useLegacyText);
                EditorPrefs.SetBool(PREFS_USE_SCRIPT_GENERATOR_NAMING_KEY, useScriptGeneratorNaming);
                EditorPrefs.SetBool(PREFS_CLEAR_SAME_NAME_KEY, clearSameNameBeforeBake);
                EditorPrefs.SetBool(PREFS_SELECT_GENERATED_KEY, selectGeneratedRoot);
            }

            ScriptGeneratorSetting setting = ScriptGeneratorSetting.Instance;
            if (useScriptGeneratorNaming)
            {
                if (setting == null)
                {
                    EditorGUILayout.HelpBox("未找到 ScriptGeneratorSetting，命名会回退到内置前缀。", MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox($"已读取 ScriptGeneratorSetting。字段风格: {setting.CodeStyle}，绑定组件: {(setting.UseBindComponent ? "开启" : "关闭")}", MessageType.None);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawJsonInputUI()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("JSON 输入", EditorStyles.boldLabel);
            currentMode = (InputMode)GUILayout.Toolbar((int)currentMode, new string[] { "JSON 文件", "粘贴 JSON" });
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (currentMode == InputMode.FileAsset)
            {
                DrawFileModeUI();
            }
            else
            {
                DrawStringModeUI();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawJsonSummaryUI()
        {
            if (string.IsNullOrEmpty(lastJsonSummary) || lastJsonSummaryType == MessageType.None) return;
            EditorGUILayout.HelpBox(lastJsonSummary, lastJsonSummaryType);
        }

        private void DrawBakeActionsUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("校验 JSON", GUILayout.Height(28)))
            {
                ValidateCurrentJson(showLog: true);
            }

            using (new EditorGUI.DisabledScope(!CanBake()))
            {
                if (GUILayout.Button("执行烘焙生成", GUILayout.Height(28)))
                {
                    ExecuteBake();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawFileModeUI()
        {
            jsonAsset = (TextAsset)EditorGUILayout.ObjectField("JSON 数据源", jsonAsset, typeof(TextAsset), false);
            EditorGUILayout.HelpBox("请将工程目录下的 .json 文件拖拽至此。", MessageType.Info);
        }

        private void DrawStringModeUI()
        {
            GUILayout.Label("在此粘贴 JSON 文本:", EditorStyles.label);
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));
            rawJsonString = EditorGUILayout.TextArea(rawJsonString, GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();
            GUILayout.Space(5);

            if (GUILayout.Button("将当前 JSON 保存为文件到工程目录..."))
            {
                SaveRawJsonToProject();
            }
        }

        #endregion

        #region 核心业务逻辑

        private bool CanBake()
        {
            if (targetCanvas == null && !autoFindCanvas) return false;
            if (currentMode == InputMode.FileAsset) return jsonAsset != null;
            return !string.IsNullOrWhiteSpace(rawJsonString);
        }

        private void AutoFindConfig()
        {
            string[] guids = AssetDatabase.FindAssets("t:HtmlToUGUIConfig");
            if (guids.Length == 0) return;

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            config = AssetDatabase.LoadAssetAtPath<HtmlToUGUIConfig>(path);
            if (config == null) return;

            EditorPrefs.SetString(PREFS_CONFIG_PATH_KEY, path);
            configSO = null;
        }

        private void CreateConfigAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "创建 HtmlToUGUIConfig",
                "HtmlToUGUIConfig",
                "asset",
                "请选择配置保存位置");
            if (string.IsNullOrEmpty(path)) return;

            config = CreateInstance<HtmlToUGUIConfig>();
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorPrefs.SetString(PREFS_CONFIG_PATH_KEY, path);
            configSO = null;
        }

        private void AutoFindConverterPage()
        {
            string[] guids = AssetDatabase.FindAssets("HTML 转 JSON 坐标烘焙器 t:DefaultAsset");
            string path = guids.Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => p.EndsWith(".html", StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrEmpty(path))
            {
                path = "Assets/TEngine/Extension/HtmlToUGUI/HtmlToJson/HTML 转 JSON 坐标烘焙器.html";
            }

            if (!File.Exists(ToFullPath(path))) return;

            converterUrl = "file:///" + ToFullPath(path).Replace("\\", "/");
            EditorPrefs.SetString(PREFS_URL_KEY, converterUrl);
        }

        private Canvas FindBestCanvas()
        {
            Canvas selectedCanvas = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<Canvas>()
                : null;
            if (selectedCanvas != null) return selectedCanvas;

#if UNITY_6000_0_OR_NEWER
            Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);
#else
            Canvas[] canvases = FindObjectsOfType<Canvas>(true);
#endif
            return canvases
                .OrderByDescending(c => c.isRootCanvas)
                .ThenBy(c => c.gameObject.scene.IsValid() ? 0 : 1)
                .FirstOrDefault();
        }

        private Canvas CreateDefaultCanvas()
        {
            GameObject canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create HtmlToUGUI Canvas");

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = GetSelectedReferenceResolution();
            scaler.matchWidthOrHeight = 0.5f;

#if UNITY_6000_0_OR_NEWER
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
#else
            if (FindObjectOfType<EventSystem>() == null)
#endif
            {
                GameObject eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                Undo.RegisterCreatedObjectUndo(eventSystemGo, "Create EventSystem");
            }

            Selection.activeGameObject = canvasGo;
            return canvas;
        }

        private bool ValidateCurrentJson(bool showLog)
        {
            string jsonContent = GetCurrentJsonContent(out string error);
            if (!string.IsNullOrEmpty(error))
            {
                lastJsonSummary = error;
                lastJsonSummaryType = MessageType.Error;
                if (showLog) Debug.LogError($"[HtmlToUGUIBaker] {error}");
                return false;
            }

            try
            {
                UIDataNode rootNode = JsonConvert.DeserializeObject<UIDataNode>(jsonContent);
                if (rootNode == null)
                {
                    lastJsonSummary = "JSON 解析结果为空。";
                    lastJsonSummaryType = MessageType.Error;
                    return false;
                }

                int nodeCount = CountNodes(rootNode);
                int imageCount = CountImageNodes(rootNode);
                string version = rootNode.schemaVersion >= 2 ? $"v{rootNode.schemaVersion}" : "v1";
                lastJsonSummary = $"JSON {version} 校验通过。根节点: {rootNode.name}，节点数: {nodeCount}，图片节点: {imageCount}。";
                lastJsonSummaryType = MessageType.Info;
                if (showLog) Debug.Log($"[HtmlToUGUIBaker] {lastJsonSummary}");
                return true;
            }
            catch (Exception e)
            {
                lastJsonSummary = $"JSON 解析失败: {e.Message}";
                lastJsonSummaryType = MessageType.Error;
                if (showLog) Debug.LogError($"[HtmlToUGUIBaker] {lastJsonSummary}");
                return false;
            }
        }

        private string GetCurrentJsonContent(out string error)
        {
            error = string.Empty;
            if (currentMode == InputMode.FileAsset)
            {
                if (jsonAsset == null)
                {
                    error = "文件模式下未指定 JSON 数据源。";
                    return string.Empty;
                }

                return jsonAsset.text;
            }

            if (string.IsNullOrWhiteSpace(rawJsonString))
            {
                error = "字符串模式下 JSON 内容为空。";
                return string.Empty;
            }

            return rawJsonString;
        }

        private int CountNodes(UIDataNode node)
        {
            if (node == null) return 0;
            int count = 1;
            if (node.children == null) return count;
            foreach (UIDataNode child in node.children) count += CountNodes(child);
            return count;
        }

        private int CountImageNodes(UIDataNode node)
        {
            if (node == null) return 0;
            int count = string.Equals(node.type, "image", StringComparison.OrdinalIgnoreCase) ||
                        !string.IsNullOrWhiteSpace(node.imageSrc) ||
                        !string.IsNullOrWhiteSpace(node.backgroundImageSrc)
                ? 1
                : 0;
            if (node.children == null) return count;
            foreach (UIDataNode child in node.children) count += CountImageNodes(child);
            return count;
        }

        private void CopyDSLToClipboard()
        {
            if (config == null || config.supportedResolutions.Count <= selectedResolutionIndex)
            {
                Debug.LogError("[HtmlToUGUIBaker] 复制失败: 配置文件缺失或分辨率索引越界。");
                return;
            }

            if (config.dslTemplateAsset == null)
            {
                Debug.LogError("[HtmlToUGUIBaker] 复制失败: 配置文件中未指定 DSL 模板文件 (TextAsset)，请在 SO 面板中拖入 .md 模板文件。");
                return;
            }

            Vector2 res = config.supportedResolutions[selectedResolutionIndex].resolution;
            string dsl = config.dslTemplateAsset.text.Replace("{WIDTH}", res.x.ToString())
                .Replace("{HEIGHT}", res.y.ToString());
            GUIUtility.systemCopyBuffer = dsl;
            Debug.Log($"[HtmlToUGUIBaker] 已成功复制分辨率为 {res.x}x{res.y} 的 DSL 规范文档到剪贴板。");
        }

        private void SaveRawJsonToProject()
        {
            if (string.IsNullOrWhiteSpace(rawJsonString))
            {
                Debug.LogError("[HtmlToUGUIBaker] 保存失败: 当前 JSON 字符串为空，请先粘贴数据。");
                return;
            }

            string savePath = EditorUtility.SaveFilePanelInProject(
                "保存 JSON 数据",
                "NewUIWindow.json",
                "json",
                "请选择要保存的目录"
            );

            if (string.IsNullOrEmpty(savePath)) return;

            try
            {
                File.WriteAllText(savePath, rawJsonString);
                AssetDatabase.Refresh();
                TextAsset savedAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(savePath);
                if (savedAsset != null)
                {
                    jsonAsset = savedAsset;
                    currentMode = InputMode.FileAsset;
                    Debug.Log($"[HtmlToUGUIBaker] JSON 文件已成功保存至: {savePath}，并已自动切换至文件模式。");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[HtmlToUGUIBaker] 文件写入失败: 路径 {savePath}，错误信息: {e.Message}");
            }
        }

        private void ExecuteBake()
        {
            if (targetCanvas == null)
            {
                targetCanvas = autoFindCanvas ? FindBestCanvas() : null;
                if (targetCanvas == null)
                {
                    Debug.LogError("[HtmlToUGUIBaker] 烘焙中断: 未指定目标 Canvas，且当前场景未找到 Canvas。");
                    return;
                }
            }

            string jsonContent = GetCurrentJsonContent(out string jsonError);
            if (!string.IsNullOrEmpty(jsonError))
            {
                Debug.LogError($"[HtmlToUGUIBaker] 烘焙中断: {jsonError}");
                return;
            }

            UIDataNode rootNode = null;
            try
            {
                rootNode = JsonConvert.DeserializeObject<UIDataNode>(jsonContent);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[HtmlToUGUIBaker] 烘焙中断: JSON 解析异常，错误信息: {e.Message}");
                return;
            }

            if (rootNode == null)
            {
                Debug.LogError("[HtmlToUGUIBaker] 烘焙中断: JSON 解析结果为空，请检查数据格式是否符合规范。");
                return;
            }

            if (useScriptGeneratorNaming)
            {
                NormalizeNodeNamesByScriptGenerator(rootNode);
            }

            BakeContext bakeContext = CreateBakeContext(rootNode);
            ConfigureCanvasScaler(targetCanvas, bakeContext);

            if (clearSameNameBeforeBake)
            {
                RemoveExistingRoot(targetCanvas.transform, rootNode.name);
            }

            GameObject rootGo = CreateUINode(rootNode, targetCanvas.transform, null, bakeContext, 0f, 0f);
            Undo.RegisterCreatedObjectUndo(rootGo, "Bake UI Prototype");
            if (selectGeneratedRoot) Selection.activeGameObject = rootGo;
            lastJsonSummary = $"烘焙完成: {rootGo.name}，节点数: {CountNodes(rootNode)}。";
            lastJsonSummaryType = MessageType.Info;

            Debug.Log($"[HtmlToUGUIBaker] 烘焙完成: 成功生成 UI 树 [{rootGo.name}]，当前基准分辨率已适配。");
        }

        private void RemoveExistingRoot(Transform canvasTransform, string rootName)
        {
            if (canvasTransform == null || string.IsNullOrWhiteSpace(rootName)) return;

            Transform existing = canvasTransform.Find(rootName);
            if (existing == null) return;

            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        private void NormalizeNodeNamesByScriptGenerator(UIDataNode rootNode)
        {
            if (rootNode?.children == null) return;

            NormalizeNodeNamesRecursive(rootNode.children);
        }

        private void NormalizeNodeNamesRecursive(List<UIDataNode> nodes)
        {
            if (nodes == null) return;

            HashSet<string> usedNames = new HashSet<string>();
            foreach (UIDataNode node in nodes)
            {
                node.name = MakeScriptGeneratorNodeName(node);
                node.name = EnsureUniqueName(node.name, usedNames);
                NormalizeNodeNamesRecursive(node.children);
            }
        }

        private string MakeScriptGeneratorNodeName(UIDataNode node)
        {
            if (node == null) return "m_goNode";

            UIComponentName componentName = GetExpectedComponentName(node);
            string prefix = GetScriptGeneratorPrefix(componentName, GetFallbackPrefix(componentName));
            string body = ExtractNodeNameBody(node.name);
            body = ToPascalName(body);
            if (string.IsNullOrEmpty(body)) body = componentName.ToString();
            return prefix + body;
        }

        private UIComponentName GetExpectedComponentName(UIDataNode node)
        {
            string type = string.IsNullOrEmpty(node.type) ? string.Empty : node.type.ToLowerInvariant();
            switch (type)
            {
                case "image":
                    return UIComponentName.Image;
                case "text":
                    return useLegacyText ? UIComponentName.Text : UIComponentName.TextMeshProUGUI;
                case "button":
                    return UIComponentName.Button;
                case "input":
                    return useLegacyText ? UIComponentName.InputField : UIComponentName.TMP_InputField;
                case "scroll":
                    return UIComponentName.ScrollRect;
                case "toggle":
                    return UIComponentName.Toggle;
                case "slider":
                    return UIComponentName.Slider;
                case "dropdown":
                    return useLegacyText ? UIComponentName.Dropdown : UIComponentName.TMP_Dropdown;
                case "div":
                default:
                    return UIComponentName.GameObject;
            }
        }

        private string GetScriptGeneratorPrefix(UIComponentName componentName, string fallback)
        {
            List<ScriptGenerateRuler> rules = ScriptGeneratorSetting.GetScriptGenerateRule();
            ScriptGenerateRuler rule = rules?.FirstOrDefault(r => r.componentName == componentName && !r.isUIWidget)
                                      ?? rules?.FirstOrDefault(r => r.componentName == componentName);
            if (rule != null && !string.IsNullOrWhiteSpace(rule.uiElementRegex))
            {
                return rule.uiElementRegex;
            }

            return fallback;
        }

        private string GetFallbackPrefix(UIComponentName componentName)
        {
            switch (componentName)
            {
                case UIComponentName.Button:
                    return "m_btn";
                case UIComponentName.Image:
                    return "m_img";
                case UIComponentName.Text:
                    return "m_text";
                case UIComponentName.TextMeshProUGUI:
                    return "m_tmp";
                case UIComponentName.InputField:
                    return "m_input";
                case UIComponentName.TMP_InputField:
                    return "m_tmpInput";
                case UIComponentName.ScrollRect:
                    return "m_scroll";
                case UIComponentName.Toggle:
                    return "m_toggle";
                case UIComponentName.Slider:
                    return "m_slider";
                case UIComponentName.Dropdown:
                    return "m_dropdown";
                case UIComponentName.TMP_Dropdown:
                    return "m_tmpDropdown";
                default:
                    return "m_go";
            }
        }

        private string ExtractNodeNameBody(string nodeName)
        {
            if (string.IsNullOrWhiteSpace(nodeName)) return string.Empty;

            IEnumerable<string> prefixes = GetKnownScriptGeneratorPrefixes()
                .Concat(new[]
                {
                    "m_tmpDropdown", "m_tmpInput", "m_canvasGroup", "m_scrollBar", "m_richText",
                    "m_dropdown", "m_toggle", "m_slider", "m_scroll", "m_input", "m_text",
                    "m_btn", "m_img", "m_rimg", "m_rect", "m_go", "m_tf", "m_tmp", "m_"
                })
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct()
                .OrderByDescending(p => p.Length);

            foreach (string prefix in prefixes)
            {
                if (nodeName.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return nodeName.Substring(prefix.Length);
                }
            }

            return nodeName;
        }

        private IEnumerable<string> GetKnownScriptGeneratorPrefixes()
        {
            List<ScriptGenerateRuler> rules = ScriptGeneratorSetting.GetScriptGenerateRule();
            if (rules == null) yield break;

            foreach (ScriptGenerateRuler rule in rules)
            {
                if (!string.IsNullOrWhiteSpace(rule.uiElementRegex)) yield return rule.uiElementRegex;
            }
        }

        private string ToPascalName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            string[] parts = value
                .Replace("-", "_")
                .Replace(" ", "_")
                .Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return string.Empty;

            string result = string.Empty;
            foreach (string part in parts)
            {
                string clean = new string(part.Where(char.IsLetterOrDigit).ToArray());
                if (string.IsNullOrEmpty(clean)) continue;
                result += char.ToUpperInvariant(clean[0]) + (clean.Length > 1 ? clean.Substring(1) : string.Empty);
            }

            return result;
        }

        private string EnsureUniqueName(string name, HashSet<string> usedNames)
        {
            if (usedNames.Add(name)) return name;

            int index = 2;
            string candidate;
            do
            {
                candidate = $"{name}{index}";
                index++;
            } while (!usedNames.Add(candidate));

            return candidate;
        }

        private BakeContext CreateBakeContext(UIDataNode rootNode)
        {
            Vector2 targetRes = GetSelectedReferenceResolution();
            bool isV2 = rootNode.schemaVersion >= 2;
            if (isV2 && rootNode.designWidth > 0 && rootNode.designHeight > 0)
            {
                targetRes = new Vector2(rootNode.designWidth, rootNode.designHeight);
            }
            else if (isV2 && rootNode.width > 0 && rootNode.height > 0)
            {
                targetRes = new Vector2(rootNode.width, rootNode.height);
            }

            string jsonDirectory = GetCurrentJsonDirectory();
            string sourceDirectory = ResolveSourceDirectory(rootNode, jsonDirectory);

            return new BakeContext
            {
                schemaVersion = rootNode.schemaVersion,
                isV2 = isV2,
                designSize = targetRes,
                jsonDirectory = jsonDirectory,
                sourceDirectory = sourceDirectory,
                enableAdaptiveLayout = config == null || config.enableAdaptiveLayout,
                applySafeAreaHints = config == null || config.applySafeAreaHints,
                warnOnSmartLayoutFallback = config != null && config.warnOnSmartLayoutFallback
            };
        }

        private Vector2 GetSelectedReferenceResolution()
        {
            if (config != null && config.supportedResolutions != null &&
                config.supportedResolutions.Count > selectedResolutionIndex)
            {
                return config.supportedResolutions[selectedResolutionIndex].resolution;
            }

            return new Vector2(1920, 1080);
        }

        private string GetCurrentJsonDirectory()
        {
            if (currentMode != InputMode.FileAsset || jsonAsset == null) return string.Empty;

            string assetPath = AssetDatabase.GetAssetPath(jsonAsset);
            if (string.IsNullOrEmpty(assetPath)) return string.Empty;

            string fullPath = ToFullPath(assetPath);
            return Path.GetDirectoryName(fullPath);
        }

        private string ResolveSourceDirectory(UIDataNode rootNode, string jsonDirectory)
        {
            if (config != null && !string.IsNullOrWhiteSpace(config.htmlImageSourceRoot))
            {
                return ToFullPath(config.htmlImageSourceRoot);
            }

            string metadataPath = FirstNonEmpty(rootNode.sourceDirectory, rootNode.htmlFilePath, rootNode.sourcePath);
            if (!string.IsNullOrWhiteSpace(metadataPath))
            {
                string fullPath = ToFullPath(metadataPath);
                return Directory.Exists(fullPath) ? fullPath : Path.GetDirectoryName(fullPath);
            }

            return jsonDirectory;
        }

        private void ConfigureCanvasScaler(Canvas canvas, BakeContext bakeContext)
        {
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = bakeContext.designSize;
            scaler.matchWidthOrHeight = 0.5f;
        }

        #endregion

        #region 节点生成与组件挂载逻辑

        private void ApplyRectTransform(RectTransform rect, UIDataNode nodeData, UIDataNode parentData,
            BakeContext bakeContext, float parentAbsX, float parentAbsY)
        {
            if (!bakeContext.isV2 || !bakeContext.enableAdaptiveLayout)
            {
                ApplyFixedRectTransform(rect, nodeData, parentAbsX, parentAbsY);
                return;
            }

            Vector2 parentSize = GetParentSize(parentData, bakeContext);
            if (TryApplyExplicitRectTransform(rect, nodeData))
            {
                return;
            }

            string layoutHint = ResolveLayoutHint(nodeData, bakeContext, parentAbsX, parentAbsY, parentSize);
            switch (layoutHint)
            {
                case "stretch":
                case "fill":
                case "full":
                case "fullscreen":
                case "full-screen":
                case "safe-area":
                    ApplyStretchRectTransform(rect, nodeData, parentAbsX, parentAbsY, parentSize);
                    return;

                case "top":
                case "top-bar":
                case "stretch-x":
                case "stretch-x-top":
                    ApplyStretchXTopRectTransform(rect, nodeData, parentAbsX, parentAbsY, parentSize);
                    return;

                case "bottom":
                case "bottom-bar":
                case "stretch-x-bottom":
                    ApplyStretchXBottomRectTransform(rect, nodeData, parentAbsX, parentAbsY, parentSize);
                    return;

                case "left":
                case "left-panel":
                case "stretch-y":
                case "stretch-y-left":
                    ApplyStretchYLeftRectTransform(rect, nodeData, parentAbsX, parentAbsY, parentSize);
                    return;

                case "right":
                case "right-panel":
                case "stretch-y-right":
                    ApplyStretchYRightRectTransform(rect, nodeData, parentAbsX, parentAbsY, parentSize);
                    return;

                case "center":
                case "centered":
                case "dialog":
                    ApplyCenteredRectTransform(rect, nodeData, parentAbsX, parentAbsY, parentSize);
                    return;
            }

            if (bakeContext.warnOnSmartLayoutFallback)
            {
                Debug.LogWarning($"[HtmlToUGUIBaker] 节点 {nodeData.name} 未匹配自适应规则，已回退固定坐标。");
            }

            ApplyFixedRectTransform(rect, nodeData, parentAbsX, parentAbsY);
        }

        private void ApplyFixedRectTransform(RectTransform rect, UIDataNode nodeData, float parentAbsX, float parentAbsY)
        {
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);

            float localX = nodeData.x - parentAbsX;
            float localY = nodeData.y - parentAbsY;

            rect.anchoredPosition = new Vector2(localX, -localY);
            rect.sizeDelta = new Vector2(nodeData.width, nodeData.height);
        }

        private bool TryApplyExplicitRectTransform(RectTransform rect, UIDataNode nodeData)
        {
            bool hasAnchorMin = TryParseVector2(nodeData.anchorMin, out Vector2 anchorMin);
            bool hasAnchorMax = TryParseVector2(nodeData.anchorMax, out Vector2 anchorMax);
            bool hasOffsetMin = TryParseVector2(nodeData.offsetMin, out Vector2 offsetMin);
            bool hasOffsetMax = TryParseVector2(nodeData.offsetMax, out Vector2 offsetMax);
            if (!hasAnchorMin || !hasAnchorMax || !hasOffsetMin || !hasOffsetMax) return false;

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = TryParseVector2(nodeData.pivot, out Vector2 pivot) ? pivot : new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return true;
        }

        private Vector2 GetParentSize(UIDataNode parentData, BakeContext bakeContext)
        {
            if (parentData != null && parentData.width > 0 && parentData.height > 0)
            {
                return new Vector2(parentData.width, parentData.height);
            }

            return bakeContext.designSize;
        }

        private string ResolveLayoutHint(UIDataNode nodeData, BakeContext bakeContext, float parentAbsX,
            float parentAbsY, Vector2 parentSize)
        {
            string explicitHint = FirstNonEmpty(nodeData.layoutHint, nodeData.anchorPreset);
            if (bakeContext.applySafeAreaHints &&
                !string.IsNullOrWhiteSpace(nodeData.safeArea) &&
                !nodeData.safeArea.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                explicitHint = "safe-area";
            }

            if (!string.IsNullOrWhiteSpace(explicitHint))
            {
                return NormalizeLayoutHint(explicitHint);
            }

            return InferLayoutHint(nodeData, parentAbsX, parentAbsY, parentSize);
        }

        private string InferLayoutHint(UIDataNode nodeData, float parentAbsX, float parentAbsY, Vector2 parentSize)
        {
            RectMetrics metrics = GetRectMetrics(nodeData, parentAbsX, parentAbsY, parentSize);
            float tolerance = Mathf.Max(2f, Mathf.Min(parentSize.x, parentSize.y) * 0.01f);

            if (metrics.left <= tolerance && metrics.top <= tolerance &&
                metrics.right <= tolerance && metrics.bottom <= tolerance)
            {
                return "stretch";
            }

            if (metrics.top <= tolerance && metrics.left <= tolerance && metrics.right <= tolerance)
            {
                return "top-bar";
            }

            if (metrics.bottom <= tolerance && metrics.left <= tolerance && metrics.right <= tolerance)
            {
                return "bottom-bar";
            }

            if (metrics.left <= tolerance && metrics.top <= tolerance && metrics.bottom <= tolerance)
            {
                return "left-panel";
            }

            if (metrics.right <= tolerance && metrics.top <= tolerance && metrics.bottom <= tolerance)
            {
                return "right-panel";
            }

            Vector2 center = new Vector2(metrics.left + metrics.width * 0.5f, metrics.top + metrics.height * 0.5f);
            if (Mathf.Abs(center.x - parentSize.x * 0.5f) <= tolerance * 2f &&
                Mathf.Abs(center.y - parentSize.y * 0.5f) <= tolerance * 2f &&
                metrics.width < parentSize.x * 0.95f &&
                metrics.height < parentSize.y * 0.95f)
            {
                return "center";
            }

            return "fixed";
        }

        private string NormalizeLayoutHint(string hint)
        {
            return hint.Trim().ToLowerInvariant()
                .Replace("_", "-")
                .Replace(" ", "-");
        }

        private void ApplyStretchRectTransform(RectTransform rect, UIDataNode nodeData, float parentAbsX,
            float parentAbsY, Vector2 parentSize)
        {
            RectMetrics metrics = GetRectMetrics(nodeData, parentAbsX, parentAbsY, parentSize);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(metrics.left, metrics.bottom);
            rect.offsetMax = new Vector2(-metrics.right, -metrics.top);
        }

        private void ApplyStretchXTopRectTransform(RectTransform rect, UIDataNode nodeData, float parentAbsX,
            float parentAbsY, Vector2 parentSize)
        {
            RectMetrics metrics = GetRectMetrics(nodeData, parentAbsX, parentAbsY, parentSize);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(metrics.left, -metrics.top - metrics.height);
            rect.offsetMax = new Vector2(-metrics.right, -metrics.top);
        }

        private void ApplyStretchXBottomRectTransform(RectTransform rect, UIDataNode nodeData, float parentAbsX,
            float parentAbsY, Vector2 parentSize)
        {
            RectMetrics metrics = GetRectMetrics(nodeData, parentAbsX, parentAbsY, parentSize);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(metrics.left, metrics.bottom);
            rect.offsetMax = new Vector2(-metrics.right, metrics.bottom + metrics.height);
        }

        private void ApplyStretchYLeftRectTransform(RectTransform rect, UIDataNode nodeData, float parentAbsX,
            float parentAbsY, Vector2 parentSize)
        {
            RectMetrics metrics = GetRectMetrics(nodeData, parentAbsX, parentAbsY, parentSize);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.offsetMin = new Vector2(metrics.left, metrics.bottom);
            rect.offsetMax = new Vector2(metrics.left + metrics.width, -metrics.top);
        }

        private void ApplyStretchYRightRectTransform(RectTransform rect, UIDataNode nodeData, float parentAbsX,
            float parentAbsY, Vector2 parentSize)
        {
            RectMetrics metrics = GetRectMetrics(nodeData, parentAbsX, parentAbsY, parentSize);
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.offsetMin = new Vector2(-metrics.right - metrics.width, metrics.bottom);
            rect.offsetMax = new Vector2(-metrics.right, -metrics.top);
        }

        private void ApplyCenteredRectTransform(RectTransform rect, UIDataNode nodeData, float parentAbsX,
            float parentAbsY, Vector2 parentSize)
        {
            RectMetrics metrics = GetRectMetrics(nodeData, parentAbsX, parentAbsY, parentSize);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(metrics.width, metrics.height);
            rect.anchoredPosition = new Vector2(
                metrics.left + metrics.width * 0.5f - parentSize.x * 0.5f,
                parentSize.y * 0.5f - (metrics.top + metrics.height * 0.5f));
        }

        private RectMetrics GetRectMetrics(UIDataNode nodeData, float parentAbsX, float parentAbsY,
            Vector2 parentSize)
        {
            float left = nodeData.x - parentAbsX;
            float top = nodeData.y - parentAbsY;
            float width = nodeData.width;
            float height = nodeData.height;
            return new RectMetrics
            {
                left = left,
                top = top,
                width = width,
                height = height,
                right = parentSize.x - left - width,
                bottom = parentSize.y - top - height
            };
        }

        private bool TryParseVector2(string value, out Vector2 result)
        {
            result = Vector2.zero;
            if (string.IsNullOrWhiteSpace(value)) return false;

            string[] parts = value.Trim()
                .Trim('(', ')')
                .Replace(";", ",")
                .Replace(" ", ",")
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) return false;

            if (!float.TryParse(parts[0], out float x)) return false;
            if (!float.TryParse(parts[1], out float y)) return false;

            result = new Vector2(x, y);
            return true;
        }

        private struct RectMetrics
        {
            public float left;
            public float top;
            public float right;
            public float bottom;
            public float width;
            public float height;
        }

        private void TryBindNodeImage(Image image, UIDataNode nodeData, BakeContext bakeContext)
        {
            string source = FirstNonEmpty(nodeData.imageSrc, nodeData.backgroundImageSrc);
            if (string.IsNullOrWhiteSpace(source)) return;

            string assetPath = ResolveImageAssetPath(source, nodeData, bakeContext);
            if (string.IsNullOrEmpty(assetPath)) return;

            Sprite sprite = LoadSpriteAsset(assetPath);
            if (sprite == null)
            {
                Debug.LogWarning($"[HtmlToUGUIBaker] 节点 {nodeData.name} 的图片未能作为 Sprite 加载: {assetPath}");
                return;
            }

            image.sprite = sprite;
            if (image.color.a <= 0.01f) image.color = Color.white;

            string fit = FirstNonEmpty(nodeData.imageFit, nodeData.cssObjectFit, nodeData.cssBackgroundSize)
                .ToLowerInvariant();
            image.preserveAspect = fit.Contains("contain") || fit.Contains("cover") || fit.Contains("scale-down");
            image.type = fit.Contains("slice") && sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        }

        private string ResolveImageAssetPath(string source, UIDataNode nodeData, BakeContext bakeContext)
        {
            string cleanedSource = CleanImageSource(source);
            if (string.IsNullOrEmpty(cleanedSource)) return string.Empty;

            if (cleanedSource.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                cleanedSource.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                cleanedSource.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning($"[HtmlToUGUIBaker] 节点 {nodeData.name} 使用了暂不支持的一键导入图片源: {cleanedSource}");
                return string.Empty;
            }

            string directAssetPath = ToAssetPath(cleanedSource);
            if (!string.IsNullOrEmpty(directAssetPath))
            {
                EnsureTextureImportedAsSprite(directAssetPath);
                return directAssetPath;
            }

            if (Path.IsPathRooted(cleanedSource) && File.Exists(cleanedSource))
            {
                return ImportLocalImage(cleanedSource);
            }

            foreach (string baseDirectory in new[] { bakeContext.sourceDirectory, bakeContext.jsonDirectory, GetProjectRoot() })
            {
                if (string.IsNullOrWhiteSpace(baseDirectory)) continue;
                string candidate = Path.GetFullPath(Path.Combine(baseDirectory, cleanedSource));
                if (File.Exists(candidate))
                {
                    string candidateAssetPath = ToAssetPath(candidate);
                    if (!string.IsNullOrEmpty(candidateAssetPath))
                    {
                        EnsureTextureImportedAsSprite(candidateAssetPath);
                        return candidateAssetPath;
                    }

                    return ImportLocalImage(candidate);
                }
            }

            Debug.LogWarning($"[HtmlToUGUIBaker] 节点 {nodeData.name} 未找到图片源: {source}");
            return string.Empty;
        }

        private string ImportLocalImage(string fullPath)
        {
            string importFolder = config != null && !string.IsNullOrWhiteSpace(config.importedImageFolder)
                ? config.importedImageFolder
                : "Assets/AssetRaw/UIRaw/Raw/HtmlToUGUI";
            importFolder = NormalizeAssetPath(importFolder);
            if (!importFolder.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) && importFolder != "Assets")
            {
                Debug.LogWarning($"[HtmlToUGUIBaker] 图片导入目录必须位于 Assets 下，已回退默认目录: {importFolder}");
                importFolder = "Assets/AssetRaw/UIRaw/Raw/HtmlToUGUI";
            }

            EnsureAssetFolder(importFolder);

            string targetAssetPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{importFolder}/{Path.GetFileName(fullPath)}");
            string targetFullPath = ToFullPath(targetAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFullPath));
            File.Copy(fullPath, targetFullPath, false);
            AssetDatabase.ImportAsset(targetAssetPath);
            EnsureTextureImportedAsSprite(targetAssetPath);
            return targetAssetPath;
        }

        private Sprite LoadSpriteAsset(string assetPath)
        {
            EnsureTextureImportedAsSprite(assetPath);

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite != null) return sprite;

            return AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>().FirstOrDefault();
        }

        private void EnsureTextureImportedAsSprite(string assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (importer.spriteImportMode == SpriteImportMode.None)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                changed = true;
            }

            if (changed) importer.SaveAndReimport();
        }

        private void EnsureAssetFolder(string assetFolder)
        {
            assetFolder = NormalizeAssetPath(assetFolder);
            if (AssetDatabase.IsValidFolder(assetFolder)) return;

            string[] parts = assetFolder.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
            {
                Debug.LogWarning($"[HtmlToUGUIBaker] 图片导入目录必须位于 Assets 下，已跳过创建: {assetFolder}");
                return;
            }

            string current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private string CleanImageSource(string source)
        {
            if (string.IsNullOrWhiteSpace(source)) return string.Empty;

            string value = source.Trim().Trim('\'', '"');
            if (value.StartsWith("url(", StringComparison.OrdinalIgnoreCase) && value.EndsWith(")"))
            {
                value = value.Substring(4, value.Length - 5).Trim().Trim('\'', '"');
            }

            if (value.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                value = new Uri(value).LocalPath;
            }

            int queryIndex = value.IndexOfAny(new[] { '?', '#' });
            if (queryIndex >= 0) value = value.Substring(0, queryIndex);
            return value;
        }

        private string ToAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;

            string normalized = NormalizePath(path);
            if (normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            string projectRoot = NormalizePath(GetProjectRoot());
            if (normalized.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                string relative = normalized.Substring(projectRoot.Length + 1);
                if (relative.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                    relative.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
                {
                    return relative;
                }
            }

            return string.Empty;
        }

        private string NormalizeAssetPath(string path)
        {
            return NormalizePath(path).TrimEnd('/');
        }

        private string NormalizePath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace("\\", "/");
        }

        private GameObject CreateUINode(UIDataNode nodeData, Transform parent, UIDataNode parentData,
            BakeContext bakeContext, float parentAbsX, float parentAbsY)
        {
            GameObject go = new GameObject(nodeData.name);
            go.transform.SetParent(parent, false);

            RectTransform rect = go.AddComponent<RectTransform>();
            ApplyRectTransform(rect, nodeData, parentData, bakeContext, parentAbsX, parentAbsY);

            Transform childrenContainer = ApplyComponentByType(go, nodeData, bakeContext);

            if (nodeData.children != null && nodeData.children.Count > 0)
            {
                foreach (var childNode in nodeData.children)
                {
                    CreateUINode(childNode, childrenContainer, nodeData, bakeContext, nodeData.x, nodeData.y);
                }
            }

            return go;
        }

        private Transform ApplyComponentByType(GameObject go, UIDataNode nodeData, BakeContext bakeContext)
        {
            Color bgColor = ParseHexColor(nodeData.color, Color.white);
            Color fontColor = ParseHexColor(nodeData.fontColor, Color.black);
            int fontSize = nodeData.fontSize > 0 ? nodeData.fontSize : 24;
            bool isMultiLine = nodeData.height > (fontSize * 1.5f);

            switch (nodeData.type.ToLower())
            {
                case "div":
                case "image":
                    Image img = go.AddComponent<Image>();
                    img.color = bgColor;
                    TryBindNodeImage(img, nodeData, bakeContext);
                    if (img.color.a <= 0.01f) img.raycastTarget = false;
                    return go.transform;

                case "text":
                    if (useLegacyText)
                    {
                        Text txt = go.AddComponent<Text>();
                        txt.text = nodeData.text;
                        txt.color = fontColor;
                        txt.fontSize = fontSize;
                        txt.alignment = ParseLegacyTextAlign(nodeData.textAlign);
                        txt.horizontalOverflow = isMultiLine ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
                        txt.verticalOverflow = isMultiLine ? VerticalWrapMode.Truncate : VerticalWrapMode.Overflow;
                        txt.raycastTarget = false;
                    }
                    else
                    {
                        TextMeshProUGUI txt = go.AddComponent<TextMeshProUGUI>();
                        txt.text = nodeData.text;
                        txt.color = fontColor;
                        txt.fontSize = fontSize;
                        txt.alignment = ParseTextAlign(nodeData.textAlign);
                        SetWordWrapping(txt, isMultiLine);
                        txt.overflowMode = isMultiLine ? TextOverflowModes.Truncate : TextOverflowModes.Overflow;
                        txt.raycastTarget = false;
                    }

                    return go.transform;

                case "button":
                    Image btnImg = go.AddComponent<Image>();
                    btnImg.color = bgColor;
                    Button btn = go.AddComponent<Button>();
                    btn.targetGraphic = btnImg;

                    GameObject btnTxtGo = CreateChildRect(go, useLegacyText ? "Text" : "Text (TMP)", Vector2.zero,
                        Vector2.one);
                    if (useLegacyText)
                    {
                        Text btnTxt = btnTxtGo.AddComponent<Text>();
                        btnTxt.text = nodeData.text;
                        btnTxt.color = fontColor;
                        btnTxt.fontSize = fontSize;
                        btnTxt.alignment = ParseLegacyTextAlign(nodeData.textAlign);
                        btnTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
                        btnTxt.verticalOverflow = VerticalWrapMode.Overflow;
                        btnTxt.raycastTarget = false;
                    }
                    else
                    {
                        TextMeshProUGUI btnTxt = btnTxtGo.AddComponent<TextMeshProUGUI>();
                        btnTxt.text = nodeData.text;
                        btnTxt.color = fontColor;
                        btnTxt.fontSize = fontSize;
                        btnTxt.alignment = ParseTextAlign(nodeData.textAlign);
                        SetWordWrapping(btnTxt, false);
                        btnTxt.overflowMode = TextOverflowModes.Overflow;
                        btnTxt.raycastTarget = false;
                    }

                    return go.transform;

                case "input":
                    Image inputBg = go.AddComponent<Image>();
                    inputBg.color = bgColor;

                    GameObject textAreaGo = CreateChildRect(go, "Text Area", Vector2.zero, Vector2.one,
                        new Vector2(10, 5), new Vector2(-10, -5));
                    textAreaGo.AddComponent<RectMask2D>();

                    GameObject phGo = CreateChildRect(textAreaGo, "Placeholder", Vector2.zero, Vector2.one);
                    GameObject textGo = CreateChildRect(textAreaGo, "Text", Vector2.zero, Vector2.one);

                    Color phColor = fontColor;
                    phColor.a = 0.5f;

                    if (useLegacyText)
                    {
                        InputField inputField = go.AddComponent<InputField>();
                        inputField.targetGraphic = inputBg;

                        Text phTxt = phGo.AddComponent<Text>();
                        phTxt.text = nodeData.text;
                        phTxt.color = phColor;
                        phTxt.fontSize = fontSize;
                        phTxt.alignment = ParseLegacyTextAlign(nodeData.textAlign);
                        phTxt.raycastTarget = false;

                        Text inTxt = textGo.AddComponent<Text>();
                        inTxt.color = fontColor;
                        inTxt.fontSize = fontSize;
                        inTxt.alignment = ParseLegacyTextAlign(nodeData.textAlign);
                        inTxt.raycastTarget = false;

                        inputField.textComponent = inTxt;
                        inputField.placeholder = phTxt;
                    }
                    else
                    {
                        TMP_InputField inputField = go.AddComponent<TMP_InputField>();
                        inputField.targetGraphic = inputBg;

                        TextMeshProUGUI phTxt = phGo.AddComponent<TextMeshProUGUI>();
                        phTxt.text = nodeData.text;
                        phTxt.color = phColor;
                        phTxt.fontSize = fontSize;
                        phTxt.alignment = ParseTextAlign(nodeData.textAlign);
                        SetWordWrapping(phTxt, false);
                        phTxt.raycastTarget = false;

                        TextMeshProUGUI inTxt = textGo.AddComponent<TextMeshProUGUI>();
                        inTxt.color = fontColor;
                        inTxt.fontSize = fontSize;
                        inTxt.alignment = ParseTextAlign(nodeData.textAlign);
                        SetWordWrapping(inTxt, false);
                        inTxt.raycastTarget = false;

                        inputField.textViewport = textAreaGo.GetComponent<RectTransform>();
                        inputField.textComponent = inTxt;
                        inputField.placeholder = phTxt;
                    }

                    return go.transform;

                case "scroll":
                    Image scrollBg = go.AddComponent<Image>();
                    scrollBg.color = bgColor;
                    if (scrollBg.color.a <= 0.01f) scrollBg.raycastTarget = false;

                    ScrollRect scrollRect = go.AddComponent<ScrollRect>();
                    bool isVertical = string.IsNullOrEmpty(nodeData.dir) || nodeData.dir.ToLower() == "v";
                    scrollRect.horizontal = !isVertical;
                    scrollRect.vertical = isVertical;

                    GameObject viewportGo = CreateChildRect(go, "Viewport", Vector2.zero, Vector2.one);
                    viewportGo.AddComponent<RectMask2D>();

                    GameObject contentGo = CreateChildRect(viewportGo, "Content", new Vector2(0, 1), new Vector2(0, 1));
                    RectTransform contentRect = contentGo.GetComponent<RectTransform>();
                    contentRect.pivot = new Vector2(0, 1);
                    contentRect.sizeDelta = new Vector2(nodeData.width, nodeData.height);

                    scrollRect.viewport = viewportGo.GetComponent<RectTransform>();
                    scrollRect.content = contentRect;
                    return contentGo.transform;

                case "toggle":
                    Toggle toggle = go.AddComponent<Toggle>();
                    toggle.isOn = nodeData.isChecked;

                    float boxSize = Mathf.Min(nodeData.height, 30f);
                    GameObject tBgGo = CreateChildRect(go, "Background", new Vector2(0, 0.5f), new Vector2(0, 0.5f));
                    RectTransform tBgRect = tBgGo.GetComponent<RectTransform>();
                    tBgRect.sizeDelta = new Vector2(boxSize, boxSize);
                    tBgRect.anchoredPosition = new Vector2(boxSize / 2, 0);
                    Image tBgImg = tBgGo.AddComponent<Image>();
                    tBgImg.color = Color.white;

                    GameObject checkGo = CreateChildRect(tBgGo, "Checkmark", Vector2.zero, Vector2.one);
                    Image checkImg = checkGo.AddComponent<Image>();
                    checkImg.color = Color.black;
                    RectTransform checkRect = checkGo.GetComponent<RectTransform>();
                    checkRect.offsetMin = new Vector2(4, 4);
                    checkRect.offsetMax = new Vector2(-4, -4);

                    GameObject tLblGo = CreateChildRect(go, "Label", Vector2.zero, Vector2.one);
                    RectTransform tLblRect = tLblGo.GetComponent<RectTransform>();
                    tLblRect.offsetMin = new Vector2(boxSize + 10, 0);

                    if (useLegacyText)
                    {
                        Text tLblTxt = tLblGo.AddComponent<Text>();
                        tLblTxt.text = nodeData.text;
                        tLblTxt.color = fontColor;
                        tLblTxt.fontSize = fontSize;
                        tLblTxt.alignment = TextAnchor.MiddleLeft;
                    }
                    else
                    {
                        TextMeshProUGUI tLblTxt = tLblGo.AddComponent<TextMeshProUGUI>();
                        tLblTxt.text = nodeData.text;
                        tLblTxt.color = fontColor;
                        tLblTxt.fontSize = fontSize;
                        tLblTxt.alignment = TextAlignmentOptions.MidlineLeft;
                        SetWordWrapping(tLblTxt, false);
                    }

                    toggle.targetGraphic = tBgImg;
                    toggle.graphic = checkImg;
                    return go.transform;

                case "slider":
                    Slider slider = go.AddComponent<Slider>();
                    slider.value = Mathf.Clamp01(nodeData.value);

                    GameObject sBgGo = CreateChildRect(go, "Background", new Vector2(0, 0.25f), new Vector2(1, 0.75f));
                    Image sBgImg = sBgGo.AddComponent<Image>();
                    sBgImg.color = bgColor;

                    GameObject fillAreaGo = CreateChildRect(go, "Fill Area", Vector2.zero, Vector2.one,
                        new Vector2(5, 0), new Vector2(-15, 0));
                    GameObject fillGo = CreateChildRect(fillAreaGo, "Fill", Vector2.zero, Vector2.one);
                    Image fillImg = fillGo.AddComponent<Image>();
                    fillImg.color = fontColor;

                    GameObject handleAreaGo = CreateChildRect(go, "Handle Slide Area", Vector2.zero, Vector2.one,
                        new Vector2(10, 0), new Vector2(-10, 0));
                    GameObject handleGo = CreateChildRect(handleAreaGo, "Handle", Vector2.zero, Vector2.one);
                    RectTransform handleRect = handleGo.GetComponent<RectTransform>();
                    handleRect.sizeDelta = new Vector2(20, 0);
                    Image handleImg = handleGo.AddComponent<Image>();
                    handleImg.color = Color.white;

                    slider.targetGraphic = handleImg;
                    slider.fillRect = fillGo.GetComponent<RectTransform>();
                    slider.handleRect = handleRect;
                    return go.transform;

                case "dropdown":
                    Image dBgImg = go.AddComponent<Image>();
                    dBgImg.color = bgColor;

                    GameObject dLblGo = CreateChildRect(go, "Label", Vector2.zero, Vector2.one, new Vector2(10, 0),
                        new Vector2(-30, 0));
                    GameObject arrowGo = CreateChildRect(go, "Arrow", new Vector2(1, 0.5f), new Vector2(1, 0.5f));
                    RectTransform arrowRect = arrowGo.GetComponent<RectTransform>();
                    arrowRect.sizeDelta = new Vector2(20, 20);
                    arrowRect.anchoredPosition = new Vector2(-15, 0);
                    Image arrowImg = arrowGo.AddComponent<Image>();
                    arrowImg.color = fontColor;

                    GameObject templateGo = CreateChildRect(go, "Template", new Vector2(0, 0), new Vector2(1, 0));
                    RectTransform templateRect = templateGo.GetComponent<RectTransform>();
                    templateRect.pivot = new Vector2(0.5f, 1);
                    templateRect.sizeDelta = new Vector2(0, 150);
                    templateRect.anchoredPosition = new Vector2(0, -2);
                    Image tempImg = templateGo.AddComponent<Image>();
                    tempImg.color = Color.white;

                    ScrollRect tempScroll = templateGo.AddComponent<ScrollRect>();
                    tempScroll.horizontal = false;
                    tempScroll.vertical = true;
                    templateGo.SetActive(false);

                    GameObject dViewportGo = CreateChildRect(templateGo, "Viewport", Vector2.zero, Vector2.one);
                    dViewportGo.AddComponent<Image>().color = Color.white;
                    dViewportGo.AddComponent<Mask>();

                    GameObject dContentGo =
                        CreateChildRect(dViewportGo, "Content", new Vector2(0, 1), new Vector2(1, 1));
                    RectTransform dContentRect = dContentGo.GetComponent<RectTransform>();
                    dContentRect.pivot = new Vector2(0.5f, 1);
                    dContentRect.sizeDelta = new Vector2(0, 28);

                    GameObject itemGo = CreateChildRect(dContentGo, "Item", new Vector2(0, 0.5f), new Vector2(1, 0.5f));
                    RectTransform itemRect = itemGo.GetComponent<RectTransform>();
                    itemRect.sizeDelta = new Vector2(0, 28);
                    Toggle itemToggle = itemGo.AddComponent<Toggle>();

                    GameObject itemBgGo = CreateChildRect(itemGo, "Item Background", Vector2.zero, Vector2.one);
                    Image itemBgImg = itemBgGo.AddComponent<Image>();
                    itemBgImg.color = Color.white;

                    GameObject itemCheckGo = CreateChildRect(itemGo, "Item Checkmark", new Vector2(0, 0.5f),
                        new Vector2(0, 0.5f));
                    RectTransform itemCheckRect = itemCheckGo.GetComponent<RectTransform>();
                    itemCheckRect.sizeDelta = new Vector2(20, 20);
                    itemCheckRect.anchoredPosition = new Vector2(15, 0);
                    Image itemCheckImg = itemCheckGo.AddComponent<Image>();
                    itemCheckImg.color = Color.black;

                    GameObject itemLblGo = CreateChildRect(itemGo, "Item Label", Vector2.zero, Vector2.one,
                        new Vector2(30, 0), new Vector2(-10, 0));

                    itemToggle.targetGraphic = itemBgImg;
                    itemToggle.graphic = itemCheckImg;
                    tempScroll.viewport = dViewportGo.GetComponent<RectTransform>();
                    tempScroll.content = dContentRect;

                    if (useLegacyText)
                    {
                        Dropdown dropdown = go.AddComponent<Dropdown>();

                        Text dLblTxt = dLblGo.AddComponent<Text>();
                        dLblTxt.color = fontColor;
                        dLblTxt.fontSize = fontSize;
                        dLblTxt.alignment = TextAnchor.MiddleLeft;

                        Text itemLblTxt = itemLblGo.AddComponent<Text>();
                        itemLblTxt.color = Color.black;
                        itemLblTxt.fontSize = fontSize;
                        itemLblTxt.alignment = TextAnchor.MiddleLeft;

                        dropdown.targetGraphic = dBgImg;
                        dropdown.template = templateRect;
                        dropdown.captionText = dLblTxt;
                        dropdown.itemText = itemLblTxt;

                        if (nodeData.options != null && nodeData.options.Count > 0)
                        {
                            dropdown.ClearOptions();
                            List<Dropdown.OptionData> optList = new List<Dropdown.OptionData>();
                            foreach (var opt in nodeData.options) optList.Add(new Dropdown.OptionData(opt));
                            dropdown.AddOptions(optList);
                        }
                    }
                    else
                    {
                        TMP_Dropdown dropdown = go.AddComponent<TMP_Dropdown>();

                        TextMeshProUGUI dLblTxt = dLblGo.AddComponent<TextMeshProUGUI>();
                        dLblTxt.color = fontColor;
                        dLblTxt.fontSize = fontSize;
                        dLblTxt.alignment = TextAlignmentOptions.MidlineLeft;
                        SetWordWrapping(dLblTxt, false);

                        TextMeshProUGUI itemLblTxt = itemLblGo.AddComponent<TextMeshProUGUI>();
                        itemLblTxt.color = Color.black;
                        itemLblTxt.fontSize = fontSize;
                        itemLblTxt.alignment = TextAlignmentOptions.MidlineLeft;
                        SetWordWrapping(itemLblTxt, false);

                        dropdown.targetGraphic = dBgImg;
                        dropdown.template = templateRect;
                        dropdown.captionText = dLblTxt;
                        dropdown.itemText = itemLblTxt;

                        if (nodeData.options != null && nodeData.options.Count > 0)
                        {
                            dropdown.ClearOptions();
                            List<TMP_Dropdown.OptionData> optList = new List<TMP_Dropdown.OptionData>();
                            foreach (var opt in nodeData.options) optList.Add(new TMP_Dropdown.OptionData(opt));
                            dropdown.AddOptions(optList);
                        }
                    }

                    return go.transform;

                default:
                    Debug.LogWarning($"[HtmlToUGUIBaker] 未知节点类型: {nodeData.type}");
                    return go.transform;
            }
        }

        private static void SetWordWrapping(TMP_Text text, bool enabled)
        {
#if UNITY_6000_0_OR_NEWER
            text.textWrappingMode = enabled ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
#else
            text.enableWordWrapping = enabled;
#endif
        }

        private TextAlignmentOptions ParseTextAlign(string alignStr)
        {
            if (string.IsNullOrEmpty(alignStr)) return TextAlignmentOptions.Midline;
            switch (alignStr.ToLower())
            {
                case "left":
                case "start":
                    return TextAlignmentOptions.MidlineLeft;
                case "right":
                case "end":
                    return TextAlignmentOptions.MidlineRight;
                case "center":
                default:
                    return TextAlignmentOptions.Midline;
            }
        }

        private string FirstNonEmpty(params string[] values)
        {
            if (values == null) return string.Empty;
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
            }

            return string.Empty;
        }

        private string GetProjectRoot()
        {
            return Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory();
        }

        private string ToFullPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;

            string normalized = path.Trim().Replace("\\", "/");
            if (normalized.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                normalized = new Uri(normalized).LocalPath;
            }

            if (normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            if (Path.IsPathRooted(normalized)) return Path.GetFullPath(normalized);
            return Path.GetFullPath(Path.Combine(GetProjectRoot(), normalized));
        }

        private TextAnchor ParseLegacyTextAlign(string alignStr)
        {
            if (string.IsNullOrEmpty(alignStr)) return TextAnchor.MiddleCenter;
            switch (alignStr.ToLower())
            {
                case "left":
                case "start":
                    return TextAnchor.MiddleLeft;
                case "right":
                case "end":
                    return TextAnchor.MiddleRight;
                case "center":
                default:
                    return TextAnchor.MiddleCenter;
            }
        }

        private GameObject CreateChildRect(GameObject parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2? offsetMin = null, Vector2? offsetMax = null)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin ?? Vector2.zero;
            rect.offsetMax = offsetMax ?? Vector2.zero;
            return go;
        }

        private Color ParseHexColor(string hex, Color defaultColor)
        {
            if (string.IsNullOrEmpty(hex)) return defaultColor;
            if (ColorUtility.TryParseHtmlString(hex, out Color color)) return color;
            return defaultColor;
        }

        #endregion
    }
}
