using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;

namespace TEngine.Editor
{
    internal sealed class AtlasRefWindow : EditorWindow
    {
        private const string DefaultUIPrefabFolderPath = "Assets/AssetRaw/UI";
        private const float ObjectColumnWidth = 210f;
        private const float CountColumnWidth = 90f;
        private const float ActionButtonWidth = 110f;

        private static readonly string[] InspectToolbarLabels =
        {
            "Sprite 引用",
            "Prefab 引用",
            "Atlas 引用",
            "场景选中物体"
        };

        private static readonly List<SpriteRefData> SpriteRefDataList = new List<SpriteRefData>();
        private static readonly Dictionary<Sprite, SpriteRefData> SpriteRefDataBySprite = new Dictionary<Sprite, SpriteRefData>();

        private static readonly List<PrefabRefData> PrefabRefDataList = new List<PrefabRefData>();
        private static readonly Dictionary<GameObject, PrefabRefData> PrefabRefDataByPrefab = new Dictionary<GameObject, PrefabRefData>();

        private static readonly List<AtlasRefData> AtlasRefDataList = new List<AtlasRefData>();
        private static readonly Dictionary<string, AtlasRefData> AtlasRefDataByKey = new Dictionary<string, AtlasRefData>();
        private static readonly StringBuilder HierarchyPathBuilder = new StringBuilder();

        private readonly List<RefStackData> _stackData = new List<RefStackData>();

        private InspectType _activeInspectType;
        private object _lastSelect;
        private Vector2 _scrollPosition;
        private Vector2 _settingsScrollPosition;
        private bool _showSettings;
        private string _searchSpriteName;
        private string _searchPrefabName;
        private string _searchAtlasName;
        private PrefabRefData _selectedSceneObjectReference;

        private GUIStyle _selectStyle;
        private GUIStyle _panelStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _subtitleStyle;
        private GUIStyle _breadcrumbStyle;
        private GUIStyle _tableHeaderStyle;
        private GUIStyle _primaryButtonStyle;
        private GUIStyle _metricValueStyle;
        private GUIStyle _metricLabelStyle;
        private static Texture2D _selectBackground;

        private enum InspectType
        {
            Sprite,
            Prefab,
            Atlas,
            SceneGameObject
        }

        private enum ShowType
        {
            Sprite,
            SpritePrefab,
            SpritePrefabReason,
            Prefab,
            PrefabAtlas,
            PrefabAtlasReason,
            Atlas,
            AtlasSprite,
            AtlasPrefab,
            AtlasPrefabReason,
            SceneGameObject
        }

        private sealed class RefStackData
        {
            public readonly ShowType ShowType;
            public readonly object Data;
            public readonly Vector2 LastScrollPos;

            public RefStackData(ShowType showType, object data, Vector2 lastScrollPos)
            {
                ShowType = showType;
                Data = data;
                LastScrollPos = lastScrollPos;
            }
        }

        private sealed class SpriteRefInfo
        {
            public readonly GameObject Prefab;
            public readonly Sprite Sprite;
            public readonly SpriteAtlas Atlas;
            public readonly string AtlasKey;
            public readonly string HierarchyPath;

            public SpriteRefInfo(Sprite sprite, SpriteAtlas atlas, string atlasKey, GameObject prefab, string hierarchyPath)
            {
                Sprite = sprite;
                Atlas = atlas;
                AtlasKey = atlasKey;
                Prefab = prefab;
                HierarchyPath = hierarchyPath;
            }
        }

        private sealed class SpriteRefData
        {
            public readonly Sprite Sprite;
            public readonly SpriteAtlas Atlas;
            public readonly string AtlasKey;
            public readonly Dictionary<GameObject, SpriteRefPrefabInfo> PrefabInfoList = new Dictionary<GameObject, SpriteRefPrefabInfo>();

            public SpriteRefData(Sprite sprite, SpriteAtlas atlas, string atlasKey)
            {
                Sprite = sprite;
                Atlas = atlas;
                AtlasKey = atlasKey;
            }
        }

        private sealed class SpriteRefPrefabInfo
        {
            public readonly Sprite Sprite;
            public readonly GameObject Prefab;
            public readonly List<SpriteRefInfo> RefList = new List<SpriteRefInfo>();

            public SpriteRefPrefabInfo(Sprite sprite, GameObject prefab)
            {
                Sprite = sprite;
                Prefab = prefab;
            }
        }

        private sealed class PrefabRefData
        {
            public readonly GameObject Prefab;
            public readonly Dictionary<string, PrefabRefAtlasInfo> AtlasInfoList = new Dictionary<string, PrefabRefAtlasInfo>();

            public PrefabRefData(GameObject prefab)
            {
                Prefab = prefab;
            }
        }

        private sealed class PrefabRefAtlasInfo
        {
            public readonly GameObject Prefab;
            public readonly SpriteAtlas Atlas;
            public readonly string AtlasKey;
            public readonly List<SpriteRefInfo> RefList = new List<SpriteRefInfo>();

            public PrefabRefAtlasInfo(GameObject prefab, SpriteAtlas atlas, string atlasKey)
            {
                Prefab = prefab;
                Atlas = atlas;
                AtlasKey = atlasKey;
            }
        }

        private sealed class AtlasRefData
        {
            public readonly string AtlasKey;
            public readonly string AtlasName;
            public readonly string AtlasPath;
            public readonly SpriteAtlas Atlas;
            public readonly HashSet<string> SpriteFolderPaths = new HashSet<string>();
            public readonly List<SpriteRefData> SpriteList = new List<SpriteRefData>();
            public readonly Dictionary<GameObject, PrefabRefAtlasInfo> PrefabInfoList = new Dictionary<GameObject, PrefabRefAtlasInfo>();

            public AtlasRefData(string atlasKey, string atlasName, string atlasPath, SpriteAtlas atlas)
            {
                AtlasKey = atlasKey;
                AtlasName = atlasName;
                AtlasPath = atlasPath;
                Atlas = atlas;
            }
        }

        [UnityEditor.FilePath("ProjectSettings/AtlasRefWindowSettings.asset", UnityEditor.FilePathAttribute.Location.ProjectFolder)]
        private sealed class AtlasRefWindowSettings : UnityEditor.ScriptableSingleton<AtlasRefWindowSettings>
        {
            [SerializeField]
            private string _uiPrefabFolderPath = DefaultUIPrefabFolderPath;

            public static AtlasRefWindowSettings Instance => instance;

            public string UIPrefabFolderPath => NormalizeAssetFolderPath(_uiPrefabFolderPath, DefaultUIPrefabFolderPath);

            public void SetUIPrefabFolderPath(string path)
            {
                _uiPrefabFolderPath = NormalizeAssetFolderPath(path, DefaultUIPrefabFolderPath);
                Save(true);
            }

            public void ResetToDefault()
            {
                _uiPrefabFolderPath = DefaultUIPrefabFolderPath;
                Save(true);
            }
        }

        private static string AtlasExtension => AtlasConfiguration.Instance.enableV2 ? ".spriteatlasv2" : ".spriteatlas";

        private static string AtlasFolderPath => NormalizeAssetFolderPath(AtlasConfiguration.Instance.outputAtlasDir, "Assets/AssetArt/Atlas");

        private string UIPrefabFolderPath => AtlasRefWindowSettings.Instance.UIPrefabFolderPath;

        [MenuItem("Tools/图集工具/图集引用分析")]
        public static void OpenWindow()
        {
            AtlasRefWindow window = GetWindow<AtlasRefWindow>();
            window.titleContent = new GUIContent("图集引用分析", EditorGUIUtility.IconContent("SpriteAtlas Icon").image);
            window.minSize = new Vector2(1100f, 600f);
            window.Init();
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("图集引用分析", EditorGUIUtility.IconContent("SpriteAtlas Icon").image);
            minSize = new Vector2(1100f, 600f);
            if (_stackData.Count == 0)
            {
                Init();
            }
        }

        private void OnGUI()
        {
            InitStyles();
            if (_stackData.Count == 0)
            {
                Init();
            }

            DrawMainToolbar();
            if (_showSettings)
            {
                ShowSettings();
                return;
            }

            switch (PeekStackData().ShowType)
            {
                case ShowType.Sprite:
                    ShowSprite();
                    break;
                case ShowType.SpritePrefab:
                    ShowSpritePrefab();
                    break;
                case ShowType.SpritePrefabReason:
                    ShowSpritePrefabReason();
                    break;
                case ShowType.Prefab:
                    ShowPrefab();
                    break;
                case ShowType.PrefabAtlas:
                    ShowPrefabAtlas();
                    break;
                case ShowType.PrefabAtlasReason:
                    ShowPrefabAtlasReason();
                    break;
                case ShowType.Atlas:
                    ShowAtlas();
                    break;
                case ShowType.AtlasSprite:
                    ShowAtlasSprite();
                    break;
                case ShowType.AtlasPrefab:
                    ShowAtlasPrefab();
                    break;
                case ShowType.AtlasPrefabReason:
                    ShowPrefabAtlasReason();
                    break;
                case ShowType.SceneGameObject:
                    ShowSceneGameObject();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void Init()
        {
            _stackData.Clear();
            _activeInspectType = InspectType.Sprite;
            PushStackData(new RefStackData(ShowType.Sprite, null, Vector2.zero));
        }

        private void InitStyles()
        {
            if (_panelStyle != null)
            {
                return;
            }

            _selectBackground = CreateSolidTexture(58, 114, 176, 75);
            _selectStyle = new GUIStyle
            {
                normal = { background = _selectBackground },
                padding = new RectOffset(0, 0, 1, 1)
            };
            _panelStyle = new GUIStyle("box")
            {
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(6, 6, 6, 6)
            };
            _titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleLeft
            };
            _subtitleStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
            {
                fontSize = 11,
                wordWrap = true
            };
            _breadcrumbStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter
            };
            _tableHeaderStyle = new GUIStyle(EditorStyles.toolbar)
            {
                fixedHeight = 22f,
                fontStyle = FontStyle.Bold
            };
            _primaryButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fixedHeight = 24f
            };
            _metricValueStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter
            };
            _metricLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter
            };
        }

        private void DrawMainToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.Height(26f)))
            {
                if (_stackData.Count > 1 && GUILayout.Button(EditorGUIUtility.IconContent("tab_prev"), EditorStyles.toolbarButton, GUILayout.Width(28f)))
                {
                    PopStackData();
                }

                if (!_showSettings)
                {
                    int newInspectType = GUILayout.Toolbar((int)_activeInspectType, InspectToolbarLabels, EditorStyles.toolbarButton, GUILayout.Width(420f));
                    if (newInspectType != (int)_activeInspectType)
                    {
                        SwitchInspectType((InspectType)newInspectType);
                    }

                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(new GUIContent(" 分析", EditorGUIUtility.IconContent("Refresh").image), EditorStyles.toolbarButton, GUILayout.Width(70f)))
                    {
                        AnalyzeReferences();
                    }
                }
                else
                {
                    GUILayout.FlexibleSpace();
                }

                if (GUILayout.Button(_showSettings ? "返回结果" : "设置", EditorStyles.toolbarButton, GUILayout.Width(80f), GUILayout.Height(22f)))
                {
                    _showSettings = !_showSettings;
                }
            }
        }

        private void SwitchInspectType(InspectType inspectType)
        {
            _activeInspectType = inspectType;
            _lastSelect = null;
            _scrollPosition = Vector2.zero;
            _stackData.Clear();

            switch (inspectType)
            {
                case InspectType.Sprite:
                    PushStackData(new RefStackData(ShowType.Sprite, null, Vector2.zero));
                    break;
                case InspectType.Prefab:
                    PushStackData(new RefStackData(ShowType.Prefab, null, Vector2.zero));
                    break;
                case InspectType.Atlas:
                    PushStackData(new RefStackData(ShowType.Atlas, null, Vector2.zero));
                    break;
                case InspectType.SceneGameObject:
                    PushStackData(new RefStackData(ShowType.SceneGameObject, null, Vector2.zero));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(inspectType), inspectType, null);
            }
        }

        private void ShowSettings()
        {
            AtlasRefWindowSettings settings = AtlasRefWindowSettings.Instance;
            if (settings == null)
            {
                EditorGUILayout.HelpBox("AtlasRefWindow 设置加载失败，请重新打开窗口后重试。", MessageType.Error);
                return;
            }

            _settingsScrollPosition = EditorGUILayout.BeginScrollView(_settingsScrollPosition);
            using (new EditorGUILayout.VerticalScope(_panelStyle))
            {
                GUILayout.Label("Atlas 引用分析设置", _titleStyle);
                GUILayout.Label("设置参与扫描的 UI Prefab 根目录；图集目录和命名规则统一读取图集配置。", _subtitleStyle);
            }

            using (new EditorGUILayout.VerticalScope(_panelStyle))
            {
                GUILayout.Label("扫描目录", EditorStyles.boldLabel);
                EditorGUILayout.Space(3f);

                string uiPrefabFolderPath = DrawFolderField("UI Prefab 目录", settings.UIPrefabFolderPath, DefaultUIPrefabFolderPath);
                if (uiPrefabFolderPath != settings.UIPrefabFolderPath)
                {
                    settings.SetUIPrefabFolderPath(uiPrefabFolderPath);
                }
            }

            using (new EditorGUILayout.VerticalScope(_panelStyle))
            {
                GUILayout.Label("图集格式", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("图集输出目录", AtlasFolderPath);
                EditorGUILayout.LabelField("当前扩展名", AtlasExtension);
                EditorGUILayout.HelpBox("扩展名来源：图集配置窗口中的“启用V2打包”设置。启用时使用 .spriteatlasv2，未启用时使用 .spriteatlas。", MessageType.Info);
                EditorGUILayout.HelpBox("图集资源目录与命名规则来自图集配置窗口中的收集目录、根目录子级图集和单张图集目录。", MessageType.Info);

                if (GUILayout.Button("打开图集配置窗口", GUILayout.Width(140f)))
                {
                    AtlasConfigWindow.ShowWindow();
                }

                if (!IsValidAssetFolder(AtlasFolderPath))
                {
                    EditorGUILayout.HelpBox($"图集目录无效：{AtlasFolderPath}", MessageType.Error);
                }

                if (!IsValidAssetFolder(settings.UIPrefabFolderPath))
                {
                    EditorGUILayout.HelpBox($"UI Prefab 目录无效：{settings.UIPrefabFolderPath}", MessageType.Error);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("恢复默认", GUILayout.Width(140f), GUILayout.Height(28f)))
                {
                    settings.ResetToDefault();
                }

                if (GUILayout.Button("返回分析结果", _primaryButtonStyle, GUILayout.Width(140f)))
                {
                    _showSettings = false;
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private static string DrawFolderField(string label, string folderPath, string defaultPath)
        {
            DefaultAsset folderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folderPath);
            EditorGUI.BeginChangeCheck();
            DefaultAsset newFolderAsset = EditorGUILayout.ObjectField(label, folderAsset, typeof(DefaultAsset), false) as DefaultAsset;
            string result = folderPath;
            if (EditorGUI.EndChangeCheck())
            {
                if (newFolderAsset == null)
                {
                    result = defaultPath;
                }
                else
                {
                    string newPath = AssetDatabase.GetAssetPath(newFolderAsset);
                    if (IsValidAssetFolder(newPath))
                    {
                        result = NormalizeAssetFolderPath(newPath, defaultPath);
                    }
                    else
                    {
                        Debug.LogWarning($"AtlasRefWindow 只支持拖入 Assets 下的文件夹：{newPath}");
                    }
                }
            }

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("路径", result);
            EditorGUI.EndDisabledGroup();
            return result;
        }

        private void DrawAnalysisPanel(string title, string description, int count, Action onAnalyze, string buttonText)
        {
            using (new EditorGUILayout.VerticalScope(_panelStyle))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUILayout.VerticalScope())
                    {
                        GUILayout.Label(title, EditorStyles.boldLabel);
                        EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
                        EditorGUILayout.LabelField($"UI Prefab: {UIPrefabFolderPath}", EditorStyles.miniLabel);
                    }

                    GUILayout.FlexibleSpace();
                    DrawMetric(count.ToString(), "当前数据");
                    if (GUILayout.Button(new GUIContent(buttonText, EditorGUIUtility.IconContent("Refresh").image), _primaryButtonStyle, GUILayout.Width(118f)))
                    {
                        onAnalyze?.Invoke();
                    }
                }
            }
        }

        private void DrawMetric(string value, string label)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(90f)))
            {
                GUILayout.Label(value, _metricValueStyle);
                GUILayout.Label(label, _metricLabelStyle);
            }
        }

        private static void DrawEmptyState(string message)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.HelpBox(message, MessageType.Info);
            }
        }

        private static string DrawSearchToolbar(string value, string placeholder)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(EditorGUIUtility.IconContent("Search Icon"), GUILayout.Width(22f));
                value = GUILayout.TextField(value ?? string.Empty, GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.toolbarTextField);
                if (string.IsNullOrEmpty(value))
                {
                    Rect lastRect = GUILayoutUtility.GetLastRect();
                    using (new EditorGUI.DisabledScope(true))
                    {
                        GUI.Label(lastRect, "  " + placeholder, EditorStyles.centeredGreyMiniLabel);
                    }
                }

                if (GUILayout.Button("清空", EditorStyles.toolbarButton, GUILayout.Width(48f)))
                {
                    value = string.Empty;
                    GUI.FocusControl(null);
                }
            }

            return value;
        }

        private bool ValidateAnalysisSettings()
        {
            if (!IsValidAssetFolder(UIPrefabFolderPath))
            {
                Debug.LogError($"AtlasRefWindow UI Prefab 目录无效：{UIPrefabFolderPath}");
                _showSettings = true;
                return false;
            }

            if (!IsValidAssetFolder(AtlasFolderPath))
            {
                Debug.LogError($"AtlasRefWindow 图集输出目录无效：{AtlasFolderPath}");
                _showSettings = true;
                return false;
            }

            return true;
        }

        private void AnalyzeReferences()
        {
            if (!ValidateAnalysisSettings())
            {
                return;
            }

            try
            {
                SpriteRefDataList.Clear();
                SpriteRefDataBySprite.Clear();
                PrefabRefDataList.Clear();
                PrefabRefDataByPrefab.Clear();
                AtlasRefDataList.Clear();
                AtlasRefDataByKey.Clear();

                string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { UIPrefabFolderPath });
                for (int index = 0; index < prefabGuids.Length; index++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("分析图集引用", $"分析 UI Prefab：{index + 1}/{prefabGuids.Length}", prefabGuids.Length == 0 ? 1f : (float)index / prefabGuids.Length))
                    {
                        break;
                    }

                    string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[index]);
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (prefab != null)
                    {
                        AnalyzePrefab(prefab);
                    }
                }

                AddUnreferencedSpritesFromAtlasFolders();
                SortAnalysisData();
                Debug.Log($"AtlasRefWindow 引用分析完成：Sprite {SpriteRefDataList.Count}，Prefab {PrefabRefDataList.Count}，Atlas {AtlasRefDataList.Count}");
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void AnalyzeSelectedSceneObject()
        {
            _selectedSceneObjectReference = null;
            GameObject selectedObject = Selection.activeGameObject;
            if (selectedObject == null)
            {
                return;
            }

            _selectedSceneObjectReference = new PrefabRefData(selectedObject);
            Image[] images = selectedObject.GetComponentsInChildren<Image>(true);
            foreach (Image image in images)
            {
                if (image == null || image.sprite == null)
                {
                    continue;
                }

                Sprite sprite = image.sprite;
                SpriteAtlas atlas = ResolveSpriteAtlas(sprite, out string atlasKey, out _);
                string hierarchyPath = BuildHierarchyPath(image.transform, selectedObject.transform);
                SpriteRefInfo refInfo = new SpriteRefInfo(sprite, atlas, atlasKey, selectedObject, hierarchyPath);

                if (!_selectedSceneObjectReference.AtlasInfoList.TryGetValue(atlasKey, out PrefabRefAtlasInfo atlasInfo))
                {
                    atlasInfo = new PrefabRefAtlasInfo(selectedObject, atlas, atlasKey);
                    _selectedSceneObjectReference.AtlasInfoList.Add(atlasKey, atlasInfo);
                }

                atlasInfo.RefList.Add(refInfo);
            }
        }

        private static void AnalyzePrefab(GameObject prefab)
        {
            Image[] images = prefab.GetComponentsInChildren<Image>(true);
            foreach (Image image in images)
            {
                if (image == null || image.sprite == null)
                {
                    continue;
                }

                Sprite sprite = image.sprite;
                SpriteAtlas atlas = ResolveSpriteAtlas(sprite, out string atlasKey, out string atlasPath);
                string spritePath = AssetDatabase.GetAssetPath(sprite);
                string spriteFolderPath = NormalizePath(Path.GetDirectoryName(spritePath));
                string hierarchyPath = BuildHierarchyPath(image.transform, prefab.transform);
                SpriteRefInfo refInfo = new SpriteRefInfo(sprite, atlas, atlasKey, prefab, hierarchyPath);

                bool isNewSprite = false;
                if (!SpriteRefDataBySprite.TryGetValue(sprite, out SpriteRefData spriteRefData))
                {
                    isNewSprite = true;
                    spriteRefData = new SpriteRefData(sprite, atlas, atlasKey);
                    SpriteRefDataBySprite.Add(sprite, spriteRefData);
                    SpriteRefDataList.Add(spriteRefData);
                }

                if (!spriteRefData.PrefabInfoList.TryGetValue(prefab, out SpriteRefPrefabInfo spritePrefabInfo))
                {
                    spritePrefabInfo = new SpriteRefPrefabInfo(sprite, prefab);
                    spriteRefData.PrefabInfoList.Add(prefab, spritePrefabInfo);
                }

                spritePrefabInfo.RefList.Add(refInfo);

                if (!PrefabRefDataByPrefab.TryGetValue(prefab, out PrefabRefData prefabRefData))
                {
                    prefabRefData = new PrefabRefData(prefab);
                    PrefabRefDataByPrefab.Add(prefab, prefabRefData);
                    PrefabRefDataList.Add(prefabRefData);
                }

                if (!prefabRefData.AtlasInfoList.TryGetValue(atlasKey, out PrefabRefAtlasInfo atlasRefInfo))
                {
                    atlasRefInfo = new PrefabRefAtlasInfo(prefab, atlas, atlasKey);
                    prefabRefData.AtlasInfoList.Add(atlasKey, atlasRefInfo);
                }

                atlasRefInfo.RefList.Add(refInfo);

                AtlasRefData atlasRefData = GetOrCreateAtlasRefData(atlasKey, atlas, atlasPath);
                if (!string.IsNullOrEmpty(spriteFolderPath))
                {
                    atlasRefData.SpriteFolderPaths.Add(spriteFolderPath);
                }

                if (isNewSprite)
                {
                    atlasRefData.SpriteList.Add(spriteRefData);
                }

                if (!atlasRefData.PrefabInfoList.ContainsKey(prefab))
                {
                    atlasRefData.PrefabInfoList.Add(prefab, atlasRefInfo);
                }
            }
        }

        private static void AddUnreferencedSpritesFromAtlasFolders()
        {
            string[] atlasFolders = GetConfiguredAtlasFolders();
            if (atlasFolders.Length == 0)
            {
                return;
            }

            string[] spriteGuids = AssetDatabase.FindAssets("t:Sprite", atlasFolders);
            for (int index = 0; index < spriteGuids.Length; index++)
            {
                if (EditorUtility.DisplayCancelableProgressBar("分析图集引用", $"补充未引用 Sprite：{index + 1}/{spriteGuids.Length}", spriteGuids.Length == 0 ? 1f : (float)index / spriteGuids.Length))
                {
                    break;
                }

                string assetPath = AssetDatabase.GUIDToAssetPath(spriteGuids[index]);
                if (!ShouldProcessSpritePath(assetPath))
                {
                    continue;
                }

                UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                foreach (UnityEngine.Object asset in assets)
                {
                    if (asset is not Sprite sprite || SpriteRefDataBySprite.ContainsKey(sprite))
                    {
                        continue;
                    }

                    SpriteAtlas atlas = ResolveSpriteAtlas(sprite, out string atlasKey, out string atlasPath);
                    SpriteRefData spriteRefData = new SpriteRefData(sprite, atlas, atlasKey);
                    SpriteRefDataBySprite.Add(sprite, spriteRefData);
                    SpriteRefDataList.Add(spriteRefData);

                    AtlasRefData atlasRefData = GetOrCreateAtlasRefData(atlasKey, atlas, atlasPath);
                    string spriteFolderPath = NormalizePath(Path.GetDirectoryName(assetPath));
                    if (!string.IsNullOrEmpty(spriteFolderPath))
                    {
                        atlasRefData.SpriteFolderPaths.Add(spriteFolderPath);
                    }

                    atlasRefData.SpriteList.Add(spriteRefData);
                }
            }
        }

        private static void SortAnalysisData()
        {
            SpriteRefDataList.Sort((x, y) => y.PrefabInfoList.Count.CompareTo(x.PrefabInfoList.Count));
            PrefabRefDataList.Sort((x, y) => y.AtlasInfoList.Count.CompareTo(x.AtlasInfoList.Count));
            AtlasRefDataList.Sort((x, y) => y.PrefabInfoList.Count.CompareTo(x.PrefabInfoList.Count));
            foreach (AtlasRefData atlasRefData in AtlasRefDataList)
            {
                atlasRefData.SpriteList.Sort((x, y) => GetSpriteArea(y.Sprite).CompareTo(GetSpriteArea(x.Sprite)));
            }
        }

        private static AtlasRefData GetOrCreateAtlasRefData(string atlasKey, SpriteAtlas atlas, string atlasPath)
        {
            if (!AtlasRefDataByKey.TryGetValue(atlasKey, out AtlasRefData atlasRefData))
            {
                string atlasName = !string.IsNullOrEmpty(atlasKey) && !atlasKey.StartsWith("Unpacked/", StringComparison.Ordinal)
                    ? atlasKey
                    : "未匹配图集";
                atlasRefData = new AtlasRefData(atlasKey, atlasName, atlasPath, atlas);
                AtlasRefDataByKey.Add(atlasKey, atlasRefData);
                AtlasRefDataList.Add(atlasRefData);
            }

            return atlasRefData;
        }

        private static SpriteAtlas ResolveSpriteAtlas(Sprite sprite, out string atlasKey, out string atlasPath)
        {
            string assetPath = AssetDatabase.GetAssetPath(sprite);
            atlasKey = string.IsNullOrEmpty(assetPath) ? "Unpacked/" + sprite.name : "Unpacked/" + assetPath;
            atlasPath = string.Empty;
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            string atlasName = EditorSpriteSaveInfo.ResolveAtlasName(assetPath);
            if (string.IsNullOrEmpty(atlasName))
            {
                return null;
            }

            atlasKey = atlasName;
            atlasPath = $"{AtlasFolderPath}/{atlasName}{AtlasExtension}";
            SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
            if (atlas == null)
            {
                string fallbackExtension = AtlasExtension == ".spriteatlasv2" ? ".spriteatlas" : ".spriteatlasv2";
                string fallbackPath = $"{AtlasFolderPath}/{atlasName}{fallbackExtension}";
                atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(fallbackPath);
                if (atlas != null)
                {
                    atlasPath = fallbackPath;
                }
            }

            return atlas;
        }

        private static string[] GetConfiguredAtlasFolders()
        {
            AtlasConfiguration config = AtlasConfiguration.Instance;
            List<string> folders = new List<string>();
            AddValidFolders(folders, config.sourceAtlasRootDir);
            AddValidFolders(folders, config.rootChildAtlasDir);
            return folders.Distinct().ToArray();
        }

        private static void AddValidFolders(List<string> folders, IEnumerable<string> paths)
        {
            if (paths == null)
            {
                return;
            }

            foreach (string path in paths)
            {
                string folder = NormalizeAssetFolderPath(path, string.Empty);
                if (!string.IsNullOrEmpty(folder) && AssetDatabase.IsValidFolder(folder))
                {
                    folders.Add(folder);
                }
            }
        }

        private static bool ShouldProcessSpritePath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            string extension = Path.GetExtension(assetPath).ToLowerInvariant();
            if (extension != ".png" && extension != ".jpg" && extension != ".jpeg")
            {
                return false;
            }

            AtlasConfiguration config = AtlasConfiguration.Instance;
            if (config.excludeFolder != null)
            {
                foreach (string excludeFolder in config.excludeFolder)
                {
                    string folder = NormalizeAssetFolderPath(excludeFolder, string.Empty);
                    if (!string.IsNullOrEmpty(folder) && assetPath.StartsWith(folder.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
            }

            if (config.excludeKeywords != null)
            {
                foreach (string keyword in config.excludeKeywords)
                {
                    if (!string.IsNullOrEmpty(keyword) && assetPath.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private void ShowSprite()
        {
            DrawAnalysisPanel("Sprite 引用", "查看每个 Sprite 被哪些 UI Prefab 引用。", SpriteRefDataList.Count, AnalyzeReferences, SpriteRefDataList.Count > 0 ? "重新分析" : "开始分析");
            if (SpriteRefDataList.Count == 0)
            {
                DrawEmptyState("暂无 Sprite 引用数据，请先点击“开始分析”。");
                return;
            }

            _searchSpriteName = DrawSearchToolbar(_searchSpriteName, "按 Sprite 名称筛选");
            ShowSpriteTitle();
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            foreach (SpriteRefData data in SpriteRefDataList)
            {
                if (!ContainsIgnoreCase(data.Sprite.name, _searchSpriteName))
                {
                    continue;
                }

                BeginSelectableRow(data);
                ShowSpriteItem(data);
                if (GUILayout.Button("引用 Prefab", GUILayout.Width(ActionButtonWidth)))
                {
                    PushStackData(new RefStackData(ShowType.SpritePrefab, data, _scrollPosition));
                }

                GUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        private static void ShowSpriteTitle()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Sprite", GUILayout.Width(ObjectColumnWidth));
                GUILayout.Label("Atlas", GUILayout.Width(ObjectColumnWidth));
                GUILayout.Label("尺寸", GUILayout.Width(96f));
                GUILayout.Label("Prefab 数", GUILayout.Width(CountColumnWidth));
            }
        }

        private static void ShowSpriteItem(SpriteRefData data)
        {
            DrawObjectField(data.Sprite, typeof(Sprite), false, GUILayout.Width(ObjectColumnWidth));
            DrawObjectField(data.Atlas, typeof(SpriteAtlas), false, GUILayout.Width(ObjectColumnWidth));
            GUILayout.Label(FormatSpriteSize(data.Sprite), GUILayout.Width(96f));
            GUILayout.Label(data.PrefabInfoList.Count.ToString(), GUILayout.Width(CountColumnWidth));
        }

        private void ShowSpritePrefab()
        {
            if (PeekStackData().Data is not SpriteRefData curData)
            {
                return;
            }

            DrawBreadcrumb($"Sprite: {curData.Sprite.name}");
            ShowSpritePrefabTitle();
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            foreach (KeyValuePair<GameObject, SpriteRefPrefabInfo> iter in curData.PrefabInfoList)
            {
                SpriteRefPrefabInfo data = iter.Value;
                BeginSelectableRow(data);
                DrawObjectField(data.Prefab, typeof(GameObject), false, GUILayout.Width(ObjectColumnWidth));
                GUILayout.Label(data.RefList.Count.ToString(), GUILayout.Width(CountColumnWidth));
                if (GUILayout.Button("查看原因", GUILayout.Width(ActionButtonWidth)))
                {
                    PushStackData(new RefStackData(ShowType.SpritePrefabReason, data, _scrollPosition));
                }

                GUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        private static void ShowSpritePrefabTitle()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Prefab", GUILayout.Width(ObjectColumnWidth));
                GUILayout.Label("引用数量", GUILayout.Width(CountColumnWidth));
            }
        }

        private void ShowSpritePrefabReason()
        {
            if (PeekStackData().Data is not SpriteRefPrefabInfo curData)
            {
                return;
            }

            DrawBreadcrumb($"{curData.Sprite.name} -> {curData.Prefab.name}");
            ShowReasonTitle();
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            foreach (SpriteRefInfo data in curData.RefList)
            {
                ShowReasonRow(data, true);
            }

            EditorGUILayout.EndScrollView();
        }

        private void ShowPrefab()
        {
            DrawAnalysisPanel("Prefab 引用", "查看每个 UI Prefab 使用的 SpriteAtlas。", PrefabRefDataList.Count, AnalyzeReferences, PrefabRefDataList.Count > 0 ? "重新分析" : "开始分析");
            if (PrefabRefDataList.Count == 0)
            {
                DrawEmptyState("暂无 Prefab 引用数据，请先点击“开始分析”。");
                return;
            }

            _searchPrefabName = DrawSearchToolbar(_searchPrefabName, "按 Prefab 名称筛选");
            ShowPrefabTitle();
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            foreach (PrefabRefData data in PrefabRefDataList)
            {
                if (!ContainsIgnoreCase(data.Prefab.name, _searchPrefabName))
                {
                    continue;
                }

                BeginSelectableRow(data);
                DrawObjectField(data.Prefab, typeof(GameObject), false, GUILayout.Width(ObjectColumnWidth));
                GUILayout.Label(data.AtlasInfoList.Count.ToString(), GUILayout.Width(CountColumnWidth));
                GUILayout.Label(GetPrefabRefCount(data).ToString(), GUILayout.Width(CountColumnWidth));
                if (GUILayout.Button("引用 Atlas", GUILayout.Width(ActionButtonWidth)))
                {
                    PushStackData(new RefStackData(ShowType.PrefabAtlas, data, _scrollPosition));
                }

                GUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        private static void ShowPrefabTitle()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Prefab", GUILayout.Width(ObjectColumnWidth));
                GUILayout.Label("Atlas 数", GUILayout.Width(CountColumnWidth));
                GUILayout.Label("Sprite 引用", GUILayout.Width(CountColumnWidth));
            }
        }

        private void ShowPrefabAtlas()
        {
            if (PeekStackData().Data is not PrefabRefData curData)
            {
                return;
            }

            DrawBreadcrumb($"Prefab: {curData.Prefab.name}");
            ShowPrefabAtlasTitle();
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            foreach (KeyValuePair<string, PrefabRefAtlasInfo> iter in curData.AtlasInfoList)
            {
                PrefabRefAtlasInfo data = iter.Value;
                BeginSelectableRow(data);
                DrawObjectField(data.Atlas, typeof(SpriteAtlas), false, GUILayout.Width(ObjectColumnWidth));
                GUILayout.Label(data.AtlasKey, GUILayout.Width(220f));
                GUILayout.Label(data.RefList.Count.ToString(), GUILayout.Width(CountColumnWidth));
                if (GUILayout.Button("查看原因", GUILayout.Width(ActionButtonWidth)))
                {
                    PushStackData(new RefStackData(ShowType.PrefabAtlasReason, data, _scrollPosition));
                }

                GUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        private static void ShowPrefabAtlasTitle()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Atlas", GUILayout.Width(ObjectColumnWidth));
                GUILayout.Label("Atlas Key", GUILayout.Width(220f));
                GUILayout.Label("引用数量", GUILayout.Width(CountColumnWidth));
            }
        }

        private void ShowPrefabAtlasReason()
        {
            if (PeekStackData().Data is not PrefabRefAtlasInfo curData)
            {
                return;
            }

            DrawBreadcrumb($"{curData.Prefab.name} -> {curData.AtlasKey}");
            ShowReasonTitle();
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            foreach (SpriteRefInfo data in curData.RefList)
            {
                ShowReasonRow(data, true, true);
            }

            EditorGUILayout.EndScrollView();
        }

        private void ShowAtlas()
        {
            DrawAnalysisPanel("Atlas 引用", "查看每个 SpriteAtlas 包含的 Sprite 和引用它的 UI Prefab。", AtlasRefDataList.Count, AnalyzeReferences, AtlasRefDataList.Count > 0 ? "重新分析" : "开始分析");
            if (AtlasRefDataList.Count == 0)
            {
                DrawEmptyState("暂无 Atlas 引用数据，请先点击“开始分析”。");
                return;
            }

            _searchAtlasName = DrawSearchToolbar(_searchAtlasName, "按 Atlas 名称或路径筛选");
            ShowAtlasTitle();
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            foreach (AtlasRefData data in AtlasRefDataList)
            {
                if (!ContainsIgnoreCase(data.AtlasKey + data.AtlasPath, _searchAtlasName))
                {
                    continue;
                }

                BeginSelectableRow(data);
                DrawObjectField(data.Atlas, typeof(SpriteAtlas), false, GUILayout.Width(ObjectColumnWidth));
                GUILayout.Label(data.AtlasKey, GUILayout.Width(220f));
                GUILayout.Label(data.SpriteList.Count.ToString(), GUILayout.Width(CountColumnWidth));
                GUILayout.Label(data.PrefabInfoList.Count.ToString(), GUILayout.Width(CountColumnWidth));
                if (GUILayout.Button("包含 Sprite", GUILayout.Width(ActionButtonWidth)))
                {
                    PushStackData(new RefStackData(ShowType.AtlasSprite, data, _scrollPosition));
                }

                if (GUILayout.Button("引用 Prefab", GUILayout.Width(ActionButtonWidth)))
                {
                    PushStackData(new RefStackData(ShowType.AtlasPrefab, data, _scrollPosition));
                }

                GUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        private static void ShowAtlasTitle()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Atlas", GUILayout.Width(ObjectColumnWidth));
                GUILayout.Label("Atlas Key", GUILayout.Width(220f));
                GUILayout.Label("Sprite 数", GUILayout.Width(CountColumnWidth));
                GUILayout.Label("Prefab 数", GUILayout.Width(CountColumnWidth));
            }
        }

        private void ShowAtlasSprite()
        {
            if (PeekStackData().Data is not AtlasRefData curData)
            {
                return;
            }

            DrawBreadcrumb($"Atlas: {curData.AtlasKey}");
            ShowSpriteTitle();
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            foreach (SpriteRefData data in curData.SpriteList)
            {
                BeginSelectableRow(data);
                ShowSpriteItem(data);
                if (GUILayout.Button("引用 Prefab", GUILayout.Width(ActionButtonWidth)))
                {
                    PushStackData(new RefStackData(ShowType.SpritePrefab, data, _scrollPosition));
                }

                GUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        private void ShowAtlasPrefab()
        {
            if (PeekStackData().Data is not AtlasRefData curData)
            {
                return;
            }

            DrawBreadcrumb($"Atlas: {curData.AtlasKey}");
            ShowAtlasPrefabTitle();
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            foreach (KeyValuePair<GameObject, PrefabRefAtlasInfo> iter in curData.PrefabInfoList)
            {
                PrefabRefAtlasInfo data = iter.Value;
                BeginSelectableRow(data);
                DrawObjectField(data.Prefab, typeof(GameObject), false, GUILayout.Width(ObjectColumnWidth));
                GUILayout.Label(data.RefList.Count.ToString(), GUILayout.Width(CountColumnWidth));
                if (GUILayout.Button("查看原因", GUILayout.Width(ActionButtonWidth)))
                {
                    PushStackData(new RefStackData(ShowType.AtlasPrefabReason, data, _scrollPosition));
                }

                GUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        private static void ShowAtlasPrefabTitle()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Prefab", GUILayout.Width(ObjectColumnWidth));
                GUILayout.Label("引用数量", GUILayout.Width(CountColumnWidth));
            }
        }

        private void ShowSceneGameObject()
        {
            DrawAnalysisPanel("场景物体引用", "分析 Hierarchy 当前选中物体使用的 SpriteAtlas。", _selectedSceneObjectReference?.AtlasInfoList.Count ?? 0, AnalyzeReferences, PrefabRefDataList.Count > 0 ? "刷新索引" : "生成索引");

            using (new EditorGUILayout.VerticalScope(_panelStyle))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("当前选择", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(new GUIContent(" 分析选中物体", EditorGUIUtility.IconContent("d_SceneViewOrtho").image), _primaryButtonStyle, GUILayout.Width(136f)))
                    {
                        AnalyzeSelectedSceneObject();
                    }
                }

                if (Selection.activeGameObject == null)
                {
                    EditorGUILayout.HelpBox("请先在 Hierarchy 中选择一个场景物体。", MessageType.Info);
                }
            }

            ShowSceneGameObjectTitle();
            BeginSelectableRow(_selectedSceneObjectReference);
            DrawObjectField(_selectedSceneObjectReference?.Prefab, typeof(GameObject), true, GUILayout.Width(ObjectColumnWidth));
            GUILayout.Label((_selectedSceneObjectReference?.AtlasInfoList.Count ?? 0).ToString(), GUILayout.Width(CountColumnWidth));
            if (_selectedSceneObjectReference != null && GUILayout.Button("引用 Atlas", GUILayout.Width(ActionButtonWidth)))
            {
                PushStackData(new RefStackData(ShowType.PrefabAtlas, _selectedSceneObjectReference, _scrollPosition));
            }

            GUILayout.EndHorizontal();
        }

        private static void ShowSceneGameObjectTitle()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("当前选中物体", GUILayout.Width(ObjectColumnWidth));
                GUILayout.Label("Atlas 数", GUILayout.Width(CountColumnWidth));
            }
        }

        private void ShowReasonTitle()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Prefab", GUILayout.Width(ObjectColumnWidth));
                GUILayout.Label("Atlas", GUILayout.Width(ObjectColumnWidth));
                GUILayout.Label("Sprite", GUILayout.Width(ObjectColumnWidth));
                GUILayout.Label("路径");
            }
        }

        private void ShowReasonRow(SpriteRefInfo data, bool showLocateButton, bool showSpriteRefButton = false)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawObjectField(data.Prefab, typeof(GameObject), IsSceneObject(data.Prefab), GUILayout.Width(ObjectColumnWidth));
                DrawObjectField(data.Atlas, typeof(SpriteAtlas), false, GUILayout.Width(ObjectColumnWidth));
                DrawObjectField(data.Sprite, typeof(Sprite), false, GUILayout.Width(ObjectColumnWidth));
                EditorGUILayout.SelectableLabel(data.HierarchyPath, GUILayout.Height(EditorGUIUtility.singleLineHeight));

                if (showLocateButton && GUILayout.Button("定位", GUILayout.Width(64f)))
                {
                    LocateReference(data);
                }

                if (showSpriteRefButton && GUILayout.Button("Sprite 引用", GUILayout.Width(90f)))
                {
                    if (SpriteRefDataBySprite.TryGetValue(data.Sprite, out SpriteRefData spriteData))
                    {
                        PushStackData(new RefStackData(ShowType.SpritePrefab, spriteData, _scrollPosition));
                    }
                }
            }
        }

        private void DrawBreadcrumb(string text)
        {
            using (new EditorGUILayout.HorizontalScope(_panelStyle))
            {
                const float backButtonWidth = 30f;
                if (GUILayout.Button(EditorGUIUtility.IconContent("tab_prev"), GUILayout.Width(backButtonWidth)))
                {
                    PopStackData();
                }

                GUILayout.Label(text, _breadcrumbStyle);
                GUILayout.Space(backButtonWidth);
            }
        }

        private void BeginSelectableRow(object data)
        {
            if (data != null && data == _lastSelect)
            {
                GUILayout.BeginHorizontal(_selectStyle);
            }
            else
            {
                GUILayout.BeginHorizontal();
            }
        }

        private RefStackData PopStackData()
        {
            if (_stackData.Count <= 1)
            {
                return PeekStackData();
            }

            RefStackData data = _stackData[_stackData.Count - 1];
            _stackData.RemoveAt(_stackData.Count - 1);
            _lastSelect = data.Data;
            _scrollPosition = data.LastScrollPos;
            return data;
        }

        private RefStackData PeekStackData()
        {
            return _stackData[_stackData.Count - 1];
        }

        private void PushStackData(RefStackData data)
        {
            _stackData.Add(data);
            _lastSelect = null;
            _scrollPosition = Vector2.zero;
        }

        private static void LocateReference(SpriteRefInfo data)
        {
            if (data == null || data.Prefab == null)
            {
                return;
            }

            if (IsSceneObject(data.Prefab))
            {
                Transform target = FindByHierarchyPath(data.Prefab.transform, data.HierarchyPath);
                SelectTransform(target);
                return;
            }

            string prefabPath = AssetDatabase.GetAssetPath(data.Prefab);
            if (string.IsNullOrEmpty(prefabPath))
            {
                return;
            }

            PrefabStageUtility.OpenPrefab(prefabPath);
            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage == null)
            {
                return;
            }

            Transform prefabRoot = prefabStage.prefabContentsRoot.transform;
            Transform prefabTarget = FindByHierarchyPath(prefabRoot, data.HierarchyPath);
            SelectTransform(prefabTarget);
        }

        private static Transform FindByHierarchyPath(Transform root, string hierarchyPath)
        {
            if (root == null)
            {
                return null;
            }

            if (hierarchyPath == root.name)
            {
                return root;
            }

            return root.Find(hierarchyPath);
        }

        private static void SelectTransform(Transform target)
        {
            if (target == null)
            {
                return;
            }

            Selection.activeGameObject = target.gameObject;
            EditorGUIUtility.PingObject(target.gameObject);
        }

        private static bool IsSceneObject(GameObject gameObject)
        {
            return gameObject != null && !EditorUtility.IsPersistent(gameObject);
        }

        private static void DrawObjectField(UnityEngine.Object obj, Type objType, bool allowSceneObjects, params GUILayoutOption[] options)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight, options);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.ObjectField(rect, obj, objType, allowSceneObjects);
            }

            if (obj != null && Event.current.type == EventType.MouseDown && Event.current.button == 0 && rect.Contains(Event.current.mousePosition))
            {
                Selection.activeObject = obj;
                EditorGUIUtility.PingObject(obj);
                Event.current.Use();
            }
        }

        private static string BuildHierarchyPath(Transform target, Transform root = null)
        {
            HierarchyPathBuilder.Clear();
            HierarchyPathBuilder.Append(target.name);

            Transform transform = target.parent;
            while (transform != root && transform != null)
            {
                HierarchyPathBuilder.Insert(0, transform.name + "/");
                transform = transform.parent;
            }

            return HierarchyPathBuilder.ToString();
        }

        private static int GetPrefabRefCount(PrefabRefData data)
        {
            int count = 0;
            foreach (KeyValuePair<string, PrefabRefAtlasInfo> item in data.AtlasInfoList)
            {
                count += item.Value.RefList.Count;
            }

            return count;
        }

        private static int GetSpriteArea(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
            {
                return 0;
            }

            return sprite.texture.width * sprite.texture.height;
        }

        private static string FormatSpriteSize(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
            {
                return "-";
            }

            return sprite.texture.width + " x " + sprite.texture.height;
        }

        private static bool ContainsIgnoreCase(string text, string search)
        {
            return string.IsNullOrEmpty(search) || (!string.IsNullOrEmpty(text) && text.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool IsValidAssetFolder(string assetFolderPath)
        {
            return !string.IsNullOrEmpty(assetFolderPath) && AssetDatabase.IsValidFolder(assetFolderPath);
        }

        private static string NormalizeAssetFolderPath(string path, string defaultPath)
        {
            if (string.IsNullOrEmpty(path))
            {
                return defaultPath;
            }

            path = NormalizePath(path.TrimEnd('/'));
            string dataPath = NormalizePath(Application.dataPath);
            if (path.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
            {
                path = "Assets" + path.Substring(dataPath.Length);
            }

            if (path != "Assets" && !path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return defaultPath;
            }

            return path;
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace("\\", "/");
        }

        private static Texture2D CreateSolidTexture(int r, int g, int b, int a)
        {
            Color color = new Color(r / 255f, g / 255f, b / 255f, a / 255f);
            Texture2D tex = new Texture2D(1, 1)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }
    }
}
