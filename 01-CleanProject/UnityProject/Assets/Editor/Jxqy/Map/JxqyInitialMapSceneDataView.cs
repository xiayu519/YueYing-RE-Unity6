using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Jxqy.Domain.Content;
using Jxqy.Domain.Persistence;
using Jxqy.Domain.World;
using Jxqy.UnityAdapters;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Jxqy.Editor.Map
{
    /// <summary>
    /// Editor-only preview of the original tile data for every generated map.
    /// It never creates scene objects or participates in player builds.
    /// </summary>
    [InitializeOnLoad]
    public static class JxqyInitialMapSceneDataView
    {
        private const string MapStableIdPrefix = "map:map/";
        private const string InitialMapName = "map_002_凌绝峰峰顶";
        private const string TrapsAssetPath =
            "Assets/AssetRaw/Jxqy/Text/ini/save/traps.ini/content.txt";
        private const string MapScriptRoot =
            "Assets/AssetRaw/Jxqy/Text/script/map";
        private const string EnabledSessionKey =
            "Jxqy.InitialMapSceneData.EnabledForSession";
        private const string ToggleMenuPath =
            "TEngine/Jxqy/Scene Map Data Preview";
        private const string GridKey =
            "Jxqy.InitialMapSceneData.Grid";
        private const string BarrierKey =
            "Jxqy.InitialMapSceneData.Barrier";
        private const string TrapKey =
            "Jxqy.InitialMapSceneData.Trap";
        private const string LabelKey =
            "Jxqy.InitialMapSceneData.Label";

        private static readonly Color GridColor =
            new(0.15f, 0.9f, 1f, 0.42f);
        private static readonly Color SolidBarrierColor =
            new(1f, 0.18f, 0.08f, 0.32f);
        private static readonly Color TransparentBarrierColor =
            new(0.08f, 0.35f, 1f, 0.34f);
        private static readonly Color UnknownBarrierColor =
            new(1f, 0.58f, 0.08f, 0.3f);
        private static readonly Color JumpBarrierMarkerColor =
            new(1f, 0.95f, 0.05f, 0.9f);
        private static readonly Color UnconditionalTrapColor =
            new(0.9f, 0.08f, 1f, 0.58f);
        private static readonly Color ConditionalTrapColor =
            new(1f, 0.62f, 0.05f, 0.62f);
        private static readonly Color UnboundTrapColor =
            new(0.45f, 0.45f, 0.45f, 0.5f);
        private static readonly Color MissingTrapScriptColor =
            new(1f, 0.12f, 0.12f, 0.68f);
        private static readonly Color HoverColor =
            new(1f, 1f, 0.1f, 0.95f);
        private static readonly Color OpeningMarkerColor =
            new(0.15f, 1f, 0.45f, 1f);

        private static readonly Regex FirstIfRegex = new(
            @"^\s*If\s*\((.+)\)\s*@([^;]+);?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex ComparisonRegex = new(
            @"^\s*(.+?)\s*(<>|==|>=|<=|>|<)\s*(.+?)\s*$",
            RegexOptions.Compiled);
        private static readonly Regex LoadMapRegex = new(
            "LoadMap\\s*\\(\\s*\"([^\"]+)\"",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex RunScriptRegex = new(
            "RunScript\\s*\\(\\s*\"([^\"]+)\"",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Vector3[] FillPoints =
            new Vector3[4];
        private static readonly Vector3[] LinePoints =
            new Vector3[5];

        private static JxqyMapSceneIdentity _identity;
        private static CachedMap _cachedMap;
        private static Dictionary<string, Dictionary<string, string>>
            _trapSections;
        private static GUIStyle _trapLabelStyle;
        private static bool _enabled =
            SessionState.GetBool(EnabledSessionKey, false);
        private static bool _showGrid =
            EditorPrefs.GetBool(GridKey, true);
        private static bool _showBarriers =
            EditorPrefs.GetBool(BarrierKey, true);
        private static bool _showTraps =
            EditorPrefs.GetBool(TrapKey, true);
        private static bool _showLabels =
            EditorPrefs.GetBool(LabelKey, true);

        static JxqyInitialMapSceneDataView()
        {
            SceneView.duringSceneGui += OnSceneGui;
            EditorApplication.hierarchyChanged += ClearSceneCache;
            EditorApplication.projectChanged += ClearMapCache;
        }

        /// <summary>
        /// Controls the editor-only Scene view map-data preview. The preview is
        /// disabled by default for every Unity Editor session so its map scans,
        /// data parsing, and Handles drawing have no idle Scene view cost.
        /// </summary>
        public static bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled == value)
                    return;

                _enabled = value;
                SessionState.SetBool(EnabledSessionKey, value);
                if (!value)
                {
                    _identity = null;
                    _cachedMap = null;
                    _trapSections = null;
                }

                SceneView.RepaintAll();
            }
        }

        [MenuItem(ToggleMenuPath)]
        private static void ToggleSceneMapDataPreview()
        {
            Enabled = !Enabled;
        }

        [MenuItem(ToggleMenuPath, true)]
        private static bool ValidateSceneMapDataPreview()
        {
            Menu.SetChecked(ToggleMenuPath, Enabled);
            return true;
        }

        [MenuItem("TEngine/Jxqy/Focus Loaded Map Data")]
        public static void FocusLoadedMapData()
        {
            string error = string.Empty;
            if (!TryGetMapIdentity(out JxqyMapSceneIdentity identity) ||
                !TryGetMap(identity, out CachedMap map, out error))
            {
                EditorUtility.DisplayDialog(
                    "Jxqy Map Data",
                    string.IsNullOrEmpty(error)
                        ? "当前没有已加载的 JXQY 地图场景。"
                        : error,
                    "确定");
                return;
            }

            Enabled = true;
            if (SceneView.lastActiveSceneView == null)
                return;
            FrameWholeMap(
                SceneView.lastActiveSceneView,
                identity,
                map.Data);
            SceneView.lastActiveSceneView.Focus();
        }

        private static void OnSceneGui(SceneView sceneView)
        {
            if (!Enabled)
                return;

            if (!TryGetMapIdentity(out JxqyMapSceneIdentity identity))
                return;

            if (!TryGetMap(identity, out CachedMap map, out string error))
            {
                DrawPanel(sceneView, null, error);
                return;
            }

            DrawPanel(sceneView, map, string.Empty);
            if (!Enabled || !TryCalculateVisibleRange(
                    sceneView,
                    identity.transform,
                    map.Data,
                    out TileRange range))
            {
                return;
            }

            DrawMapData(sceneView, identity.transform, map, range);
        }

        private static void DrawMapData(
            SceneView sceneView,
            Transform root,
            CachedMap map,
            TileRange range)
        {
            Matrix4x4 previousMatrix = Handles.matrix;
            Color previousColor = Handles.color;
            CompareFunction previousZTest = Handles.zTest;
            try
            {
                Handles.matrix = root.localToWorldMatrix;
                Handles.zTest = CompareFunction.Always;

                DrawCellFills(map, range);
                if (_showGrid)
                    DrawGrid(map.Data, range);
                bool shouldDrawLabels =
                    _showLabels && ShouldDrawLabels(sceneView);
                if (shouldDrawLabels && _showTraps)
                    DrawTrapLabels(map, range);
                if (map.IsInitialMap)
                    DrawOpeningMarkers(range, shouldDrawLabels);
                DrawHoveredTile(root, map);
            }
            finally
            {
                Handles.matrix = previousMatrix;
                Handles.color = previousColor;
                Handles.zTest = previousZTest;
            }
        }

        private static void DrawCellFills(
            CachedMap map,
            TileRange range)
        {
            for (int row = range.StartRow; row <= range.EndRow; row++)
            {
                for (int column = range.StartColumn;
                     column <= range.EndColumn;
                     column++)
                {
                    JxqyRuntimeMapTile tile = map.Data.GetTile(column, row);
                    SetDiamondPoints(column, row);
                    if (_showBarriers && tile.BarrierType != 0)
                    {
                        Handles.color = GetBarrierColor(tile.BarrierType);
                        Handles.DrawAAConvexPolygon(FillPoints);
                        if ((tile.BarrierType & 0x20) != 0)
                        {
                            Handles.color = JumpBarrierMarkerColor;
                            Handles.DrawSolidDisc(
                                GetTileCenter(column, row),
                                Vector3.forward,
                                4f);
                        }
                    }

                    if (_showTraps && tile.TrapIndex != 0)
                    {
                        Handles.color = GetTrapColor(
                            map,
                            tile.TrapIndex);
                        Handles.DrawAAConvexPolygon(FillPoints);
                        Handles.DrawAAPolyLine(2f, LinePoints);
                    }
                }
            }
        }

        private static void DrawGrid(
            JxqyRuntimeMapData map,
            TileRange range)
        {
            Handles.color = GridColor;
            for (int row = range.StartRow; row <= range.EndRow; row++)
            {
                for (int column = range.StartColumn;
                     column <= range.EndColumn;
                     column++)
                {
                    SetDiamondPoints(column, row);
                    Handles.DrawAAPolyLine(1f, LinePoints);
                }
            }
        }

        private static void DrawTrapLabels(
            CachedMap map,
            TileRange range)
        {
            foreach (KeyValuePair<int, JxqyIntPoint> pair in
                     map.TrapLabelPositions)
            {
                if (!range.Contains(pair.Value.X, pair.Value.Y))
                    continue;

                TrapPreviewInfo info = GetTrapInfo(map, pair.Key);
                Handles.Label(
                    GetTileCenter(pair.Value.X, pair.Value.Y),
                    info?.Label ?? $"T{pair.Key}\n未绑定",
                    GetTrapLabelStyle());
            }
        }

        private static void DrawOpeningMarkers(
            TileRange range,
            bool shouldDrawLabels)
        {
            DrawOpeningMarker(
                range,
                new JxqyIntPoint(24, 39),
                "START\nBegin.txt",
                shouldDrawLabels);
            DrawOpeningMarker(
                range,
                new JxqyIntPoint(24, 43),
                "Begin 移动终点",
                shouldDrawLabels);
        }

        private static void DrawOpeningMarker(
            TileRange range,
            JxqyIntPoint tile,
            string label,
            bool shouldDrawLabel)
        {
            if (!range.Contains(tile.X, tile.Y))
                return;

            Vector3 center = GetTileCenter(tile.X, tile.Y);
            SetDiamondPoints(tile.X, tile.Y);
            Handles.color = OpeningMarkerColor;
            Handles.DrawAAPolyLine(4f, LinePoints);
            Handles.DrawWireDisc(
                center,
                Vector3.forward,
                JxqyIsometricMapMath.HalfTileHeight * 0.7f);
            if (shouldDrawLabel)
                Handles.Label(center, label, GetTrapLabelStyle());
        }

        private static GUIStyle GetTrapLabelStyle()
        {
            return _trapLabelStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                normal =
                {
                    textColor = Color.white
                }
            };
        }

        private static void DrawHoveredTile(
            Transform root,
            CachedMap map)
        {
            if (!TryGetMouseLocalPoint(root, out Vector3 local))
                return;

            JxqyIntPoint tilePosition =
                JxqyIsometricMapMath.WorldPixelToTile(
                    Mathf.FloorToInt(local.x),
                    Mathf.FloorToInt(-local.y),
                    false);
            if (tilePosition.X < 0 || tilePosition.X >= map.Data.Columns ||
                tilePosition.Y < 0 || tilePosition.Y >= map.Data.Rows)
            {
                return;
            }

            SetDiamondPoints(tilePosition.X, tilePosition.Y);
            Handles.color = HoverColor;
            Handles.DrawAAPolyLine(3f, LinePoints);

            JxqyRuntimeMapTile tile = map.Data.GetTile(
                tilePosition.X,
                tilePosition.Y);
            Handles.BeginGUI();
            try
            {
                Vector2 mouse = Event.current.mousePosition;
                var rect = new Rect(
                    mouse.x + 18f,
                    mouse.y + 18f,
                    340f,
                    tile.TrapIndex == 0 ? 74f : 132f);
                GUI.Box(rect, BuildTileTooltip(map, tilePosition, tile));
            }
            finally
            {
                Handles.EndGUI();
            }
        }

        private static string BuildTileTooltip(
            CachedMap map,
            JxqyIntPoint tilePosition,
            JxqyRuntimeMapTile tile)
        {
            string text =
                $"格子 ({tilePosition.X}, {tilePosition.Y})\n" +
                $"Barrier: 0x{tile.BarrierType:X2} " +
                $"({GetBarrierName(tile.BarrierType)})   " +
                $"TrapIndex: {tile.TrapIndex}\n" +
                $"Layer1 MPC/Frame: {tile.Layer1Mpc}/{tile.Layer1Frame}\n" +
                $"Layer2 MPC/Frame: {tile.Layer2Mpc}/{tile.Layer2Frame}   " +
                $"Layer3: {tile.Layer3Mpc}/{tile.Layer3Frame}";
            if (tile.TrapIndex == 0)
            {
                if (map.IsInitialMap &&
                    tilePosition.Equals(new JxqyIntPoint(24, 39)))
                    text += "\n开幕出生点：由 Begin.txt 直接设置，不是 Trap";
                return text;
            }

            TrapPreviewInfo info = GetTrapInfo(map, tile.TrapIndex);
            if (info == null)
            {
                return text +
                       "\n绑定：无（Traps.ini 未配置）" +
                       "\n结果：默认状态下不会触发脚本";
            }

            text += $"\n脚本：{info.Script}";
            if (!info.ScriptFound)
                return text + "\n状态：Traps.ini 已绑定，但脚本资源缺失";
            text += string.IsNullOrEmpty(info.Condition)
                ? "\n入口条件：未发现"
                : $"\n入口条件：{info.Condition}";
            return text + $"\n作用：{info.Summary}";
        }

        private static TrapPreviewInfo GetTrapInfo(
            CachedMap map,
            int trapIndex)
        {
            return map.TrapInfo.TryGetValue(
                trapIndex,
                out TrapPreviewInfo info)
                ? info
                : null;
        }

        private static Color GetTrapColor(
            CachedMap map,
            int trapIndex)
        {
            TrapPreviewInfo info = GetTrapInfo(map, trapIndex);
            if (info == null)
                return UnboundTrapColor;
            if (!info.ScriptFound)
                return MissingTrapScriptColor;
            return string.IsNullOrEmpty(info.Condition)
                ? UnconditionalTrapColor
                : ConditionalTrapColor;
        }

        private static void LoadTrapInfo(CachedMap map)
        {
            _trapSections ??= LoadTrapSections();
            if (!_trapSections.TryGetValue(
                    map.MapName,
                    out Dictionary<string, string> bindings))
            {
                return;
            }

            foreach (KeyValuePair<string, string> binding in bindings)
            {
                if (!int.TryParse(
                        binding.Key,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int trapIndex) ||
                    trapIndex <= 0 ||
                    string.IsNullOrWhiteSpace(binding.Value))
                {
                    continue;
                }

                string script = binding.Value.Trim();
                string scriptPath =
                    $"{MapScriptRoot}/{map.MapName}/{script}/content.txt";
                TextAsset scriptAsset =
                    AssetDatabase.LoadAssetAtPath<TextAsset>(scriptPath);
                TrapPreviewInfo info = scriptAsset == null
                    ? new TrapPreviewInfo(
                        script,
                        string.Empty,
                        "脚本资源缺失",
                        $"T{trapIndex}\n脚本缺失",
                        false)
                    : AnalyzeTrapScript(
                        trapIndex,
                        script,
                        scriptAsset.text);
                map.TrapInfo[trapIndex] = info;
            }
        }

        private static Dictionary<string, Dictionary<string, string>>
            LoadTrapSections()
        {
            TextAsset trapsAsset =
                AssetDatabase.LoadAssetAtPath<TextAsset>(TrapsAssetPath);
            if (trapsAsset == null)
            {
                throw new FileNotFoundException(
                    $"Trap 配置缺失：{TrapsAssetPath}");
            }

            return JxqyLegacySaveImporter.ParseIni(trapsAsset.text);
        }

        private static TrapPreviewInfo AnalyzeTrapScript(
            int trapIndex,
            string script,
            string scriptText)
        {
            string condition = ExtractEntryCondition(scriptText);
            string summary = ExtractScriptSummary(script, scriptText);
            string labelDetail;
            if (summary.StartsWith("换图", StringComparison.Ordinal))
                labelDetail = "换图";
            else if (summary.Contains("战斗", StringComparison.Ordinal))
                labelDetail = string.IsNullOrEmpty(condition)
                    ? "战斗"
                    : "条件战斗";
            else if (summary.Contains("剧情", StringComparison.Ordinal))
                labelDetail = string.IsNullOrEmpty(condition)
                    ? "剧情"
                    : "条件剧情";
            else if (!string.IsNullOrEmpty(condition))
                labelDetail = "条件触发";
            else
                labelDetail = Path.GetFileNameWithoutExtension(script);

            return new TrapPreviewInfo(
                script,
                condition,
                summary,
                $"T{trapIndex}\n{labelDetail}",
                true);
        }

        private static string ExtractEntryCondition(string scriptText)
        {
            string[] lines = NormalizeLines(scriptText);
            for (int index = 0; index < lines.Length; index++)
            {
                Match match = FirstIfRegex.Match(lines[index]);
                if (!match.Success)
                    continue;

                string expression = match.Groups[1].Value.Trim();
                string target = match.Groups[2].Value.Trim();
                if (target.Equals(
                        "end",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return InvertCondition(expression);
                }

                for (int next = index + 1;
                     next < lines.Length;
                     next++)
                {
                    string nextLine = lines[next].Trim();
                    if (nextLine.Length == 0 ||
                        nextLine.StartsWith(
                            "//",
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (nextLine.StartsWith(
                            "Goto @end",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return NormalizeCondition(expression);
                    }
                    break;
                }

                return $"{NormalizeCondition(expression)} → @{target}";
            }

            return string.Empty;
        }

        private static string InvertCondition(string expression)
        {
            Match match = ComparisonRegex.Match(expression);
            if (!match.Success)
                return $"NOT ({NormalizeCondition(expression)})";

            string invertedOperator = match.Groups[2].Value switch
            {
                "<>" => "==",
                "==" => "!=",
                ">=" => "<",
                "<=" => ">",
                ">" => "<=",
                "<" => ">=",
                _ => "!="
            };
            return $"{match.Groups[1].Value.Trim()} " +
                   $"{invertedOperator} " +
                   $"{match.Groups[3].Value.Trim()}";
        }

        private static string NormalizeCondition(string expression)
        {
            return expression.Replace("<>", "!=").Trim();
        }

        private static string ExtractScriptSummary(
            string script,
            string scriptText)
        {
            Match loadMap = LoadMapRegex.Match(scriptText);
            if (loadMap.Success)
            {
                string mapName = Path.GetFileNameWithoutExtension(
                    loadMap.Groups[1].Value.Replace('\\', '/'));
                return $"换图 → {mapName}";
            }

            bool hasFight =
                scriptText.IndexOf(
                    "EnableFight",
                    StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasDialogue =
                scriptText.IndexOf(
                    "Talk(",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                scriptText.IndexOf(
                    "Say(",
                    StringComparison.OrdinalIgnoreCase) >= 0;
            if (hasDialogue && hasFight)
                return "剧情/战斗";
            if (hasFight)
                return "战斗";
            if (hasDialogue)
                return "剧情/对话";

            Match runScript = RunScriptRegex.Match(scriptText);
            if (runScript.Success)
                return $"运行脚本 → {runScript.Groups[1].Value}";
            if (scriptText.IndexOf(
                    "SetMapTrap",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "更新 Trap 状态";
            }

            return $"执行 {script}";
        }

        private static string[] NormalizeLines(string text)
        {
            return (text ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n');
        }

        private static Color GetBarrierColor(byte barrierType)
        {
            return barrierType switch
            {
                0x40 or 0x60 => TransparentBarrierColor,
                0x80 or 0xA0 => SolidBarrierColor,
                _ => UnknownBarrierColor
            };
        }

        private static string GetBarrierName(byte barrierType)
        {
            return barrierType switch
            {
                0x00 => "无",
                0x40 => "透",
                0x60 => "跳透",
                0x80 => "障",
                0xA0 => "跳障",
                _ => "未知"
            };
        }

        private static void DrawPanel(
            SceneView sceneView,
            CachedMap map,
            string error)
        {
            Handles.BeginGUI();
            try
            {
                GUILayout.BeginArea(
                    new Rect(12f, 12f, 340f, 250f),
                    "JXQY 全地图数据预览",
                    GUI.skin.window);

                EditorGUI.BeginChangeCheck();
                bool enabled = GUILayout.Toggle(
                    Enabled,
                    "启用 Scene 数据层");
                if (enabled != Enabled)
                    Enabled = enabled;
                using (new EditorGUI.DisabledScope(!Enabled))
                {
                    GUILayout.BeginHorizontal();
                    _showGrid = GUILayout.Toggle(_showGrid, "格子");
                    _showBarriers = GUILayout.Toggle(_showBarriers, "障碍");
                    _showTraps = GUILayout.Toggle(_showTraps, "Trap");
                    _showLabels = GUILayout.Toggle(_showLabels, "编号");
                    GUILayout.EndHorizontal();
                }

                if (EditorGUI.EndChangeCheck())
                {
                    SaveSettings();
                    sceneView.Repaint();
                }

                if (!string.IsNullOrEmpty(error))
                {
                    EditorGUILayout.HelpBox(error, MessageType.Error);
                }
                else if (map != null)
                {
                    GUILayout.Label(map.MapName, EditorStyles.boldLabel);
                    GUILayout.Label(
                        $"{map.Data.Columns}×{map.Data.Rows}  " +
                        $"障碍格 {map.BarrierCount}  " +
                        $"Trap格 {map.TrapTileCount} / 编号 {map.TrapIndexCount}");
                    GUILayout.Label(
                        $"Trap：绑定 {map.BoundTrapCount}  " +
                        $"未绑定 {map.UnboundTrapCount}  " +
                        $"脚本缺失 {map.MissingTrapScriptCount}",
                        EditorStyles.miniLabel);
                    GUILayout.Label(
                        "障碍：红=障 蓝=透 黄点=可跳越；Trap：紫=无条件 黄=条件 灰=未绑定 红=脚本缺失",
                        EditorStyles.miniLabel);
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("聚焦第一个 Trap"))
                        FocusFirstTrap(sceneView, _identity, map);
                    if (GUILayout.Button("查看全图"))
                        FrameWholeMap(sceneView, _identity, map.Data);
                    GUILayout.EndHorizontal();
                    if (map.IsInitialMap)
                    {
                        GUILayout.Label(
                            "绿色 START = Begin.txt 开幕位置（不是 Trap）",
                            EditorStyles.miniLabel);
                    }
                    GUILayout.Label(
                        "悬停查看三层 MPC/Frame、脚本、入口条件和作用",
                        EditorStyles.miniLabel);
                    GUILayout.Label(
                        "只读静态数据；运行时脚本仍可动态修改 Trap",
                        EditorStyles.miniLabel);
                }

                GUILayout.EndArea();
            }
            finally
            {
                Handles.EndGUI();
            }
        }

        private static bool TryGetMapIdentity(
            out JxqyMapSceneIdentity identity)
        {
            if (_identity != null &&
                _identity.gameObject.scene.IsValid() &&
                _identity.gameObject.scene.isLoaded &&
                _identity.MapStableId.StartsWith(
                    MapStableIdPrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                identity = _identity;
                return true;
            }

            _identity = null;
            for (int sceneIndex = 0;
                 sceneIndex < SceneManager.sceneCount;
                 sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    JxqyMapSceneIdentity candidate =
                        root.GetComponentInChildren<
                            JxqyMapSceneIdentity>(true);
                    if (candidate == null ||
                        !candidate.MapStableId.StartsWith(
                            MapStableIdPrefix,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    _identity = candidate;
                    identity = candidate;
                    return true;
                }
            }

            identity = null;
            return false;
        }

        private static bool TryGetMap(
            JxqyMapSceneIdentity identity,
            out CachedMap map,
            out string error)
        {
            if (_cachedMap != null &&
                string.Equals(
                    _cachedMap.StableId,
                    identity.MapStableId,
                    StringComparison.OrdinalIgnoreCase))
            {
                map = _cachedMap;
                error = string.Empty;
                return true;
            }

            try
            {
                string relativePath = identity.SourceRelativePath
                    .Replace('\\', '/')
                    .TrimStart('/');
                string metadataPath =
                    $"{JxqyMapSceneBaker.MapRoot}/{relativePath}/map.json";
                string mapDataPath =
                    Path.GetDirectoryName(metadataPath)
                        ?.Replace('\\', '/') + "/map.bytes";
                TextAsset metadataAsset =
                    AssetDatabase.LoadAssetAtPath<TextAsset>(metadataPath);
                TextAsset mapDataAsset =
                    AssetDatabase.LoadAssetAtPath<TextAsset>(mapDataPath);
                if (metadataAsset == null || mapDataAsset == null)
                {
                    throw new FileNotFoundException(
                        $"地图数据缺失：{metadataPath}");
                }

                JxqyMapMetadata metadata =
                    JsonUtility.FromJson<JxqyMapMetadata>(metadataAsset.text);
                JxqyRuntimeMapData data = JxqyRuntimeMapData.Parse(
                    mapDataAsset.bytes,
                    metadata);
                string mapName = Path.GetFileNameWithoutExtension(
                    relativePath);
                var loaded = new CachedMap
                {
                    StableId = identity.MapStableId,
                    MapName = mapName,
                    IsInitialMap = mapName.Equals(
                        InitialMapName,
                        StringComparison.OrdinalIgnoreCase),
                    Data = data
                };
                LoadTrapInfo(loaded);
                var trapCells = new Dictionary<
                    int,
                    List<JxqyIntPoint>>();
                for (int row = 0; row < data.Rows; row++)
                {
                    for (int column = 0; column < data.Columns; column++)
                    {
                        JxqyRuntimeMapTile tile =
                            data.GetTile(column, row);
                        if (tile.BarrierType != 0)
                            loaded.BarrierCount++;
                        if (tile.TrapIndex == 0)
                            continue;

                        loaded.TrapTileCount++;
                        if (!trapCells.TryGetValue(
                                tile.TrapIndex,
                                out List<JxqyIntPoint> cells))
                        {
                            cells = new List<JxqyIntPoint>();
                            trapCells.Add(tile.TrapIndex, cells);
                        }

                        cells.Add(new JxqyIntPoint(column, row));
                    }
                }

                foreach (KeyValuePair<int, List<JxqyIntPoint>> pair in
                         trapCells)
                {
                    loaded.TrapLabelPositions.Add(
                        pair.Key,
                        FindLabelPosition(pair.Value));
                    if (!loaded.TrapInfo.TryGetValue(
                            pair.Key,
                            out TrapPreviewInfo info))
                    {
                        loaded.UnboundTrapCount++;
                    }
                    else if (!info.ScriptFound)
                    {
                        loaded.MissingTrapScriptCount++;
                    }
                    else
                    {
                        loaded.BoundTrapCount++;
                    }
                }
                loaded.TrapIndexCount = trapCells.Count;

                _cachedMap = loaded;
                map = loaded;
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                map = null;
                error = exception.Message;
                return false;
            }
        }

        private static JxqyIntPoint FindLabelPosition(
            IReadOnlyList<JxqyIntPoint> cells)
        {
            float averageX = 0f;
            float averageY = 0f;
            foreach (JxqyIntPoint cell in cells)
            {
                averageX += cell.X;
                averageY += cell.Y;
            }

            averageX /= cells.Count;
            averageY /= cells.Count;
            JxqyIntPoint nearest = cells[0];
            float nearestDistance = float.PositiveInfinity;
            foreach (JxqyIntPoint cell in cells)
            {
                float deltaX = cell.X - averageX;
                float deltaY = cell.Y - averageY;
                float distance = deltaX * deltaX + deltaY * deltaY;
                if (distance >= nearestDistance)
                    continue;
                nearest = cell;
                nearestDistance = distance;
            }

            return nearest;
        }

        private static bool TryCalculateVisibleRange(
            SceneView sceneView,
            Transform root,
            JxqyRuntimeMapData map,
            out TileRange range)
        {
            var plane = new Plane(root.forward, root.position);
            float minimumX = float.PositiveInfinity;
            float maximumX = float.NegativeInfinity;
            float minimumY = float.PositiveInfinity;
            float maximumY = float.NegativeInfinity;
            Vector2[] corners =
            {
                new(0f, 0f),
                new(0f, 1f),
                new(1f, 0f),
                new(1f, 1f)
            };
            foreach (Vector2 corner in corners)
            {
                Ray ray = sceneView.camera.ViewportPointToRay(
                    new Vector3(corner.x, corner.y));
                if (!plane.Raycast(ray, out float distance))
                {
                    range = default;
                    return false;
                }

                Vector3 local = root.InverseTransformPoint(
                    ray.GetPoint(distance));
                minimumX = Mathf.Min(minimumX, local.x);
                maximumX = Mathf.Max(maximumX, local.x);
                minimumY = Mathf.Min(minimumY, local.y);
                maximumY = Mathf.Max(maximumY, local.y);
            }

            int rawStartColumn = Mathf.FloorToInt(
                (minimumX - JxqyIsometricMapMath.HalfTileWidth) /
                JxqyIsometricMapMath.TileWidth) - 1;
            int rawEndColumn = Mathf.CeilToInt(
                (maximumX + JxqyIsometricMapMath.HalfTileWidth) /
                JxqyIsometricMapMath.TileWidth) + 1;
            int rawStartRow = Mathf.FloorToInt(
                -maximumY / JxqyIsometricMapMath.HalfTileHeight) - 1;
            int rawEndRow = Mathf.CeilToInt(
                -minimumY / JxqyIsometricMapMath.HalfTileHeight) + 1;
            if (rawEndColumn < 0 || rawStartColumn >= map.Columns ||
                rawEndRow < 0 || rawStartRow >= map.Rows)
            {
                range = default;
                return false;
            }

            range = new TileRange(
                Mathf.Clamp(rawStartColumn, 0, map.Columns - 1),
                Mathf.Clamp(rawEndColumn, 0, map.Columns - 1),
                Mathf.Clamp(rawStartRow, 0, map.Rows - 1),
                Mathf.Clamp(rawEndRow, 0, map.Rows - 1));
            return true;
        }

        private static bool TryGetMouseLocalPoint(
            Transform root,
            out Vector3 localPoint)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(
                Event.current.mousePosition);
            var plane = new Plane(root.forward, root.position);
            if (!plane.Raycast(ray, out float distance))
            {
                localPoint = default;
                return false;
            }

            localPoint = root.InverseTransformPoint(ray.GetPoint(distance));
            return true;
        }

        private static void SetDiamondPoints(int column, int row)
        {
            Vector3 center = GetTileCenter(column, row);
            float halfWidth = JxqyIsometricMapMath.HalfTileWidth;
            float halfHeight = JxqyIsometricMapMath.HalfTileHeight;
            FillPoints[0] = center + new Vector3(0f, halfHeight);
            FillPoints[1] = center + new Vector3(halfWidth, 0f);
            FillPoints[2] = center + new Vector3(0f, -halfHeight);
            FillPoints[3] = center + new Vector3(-halfWidth, 0f);
            for (int index = 0; index < FillPoints.Length; index++)
                LinePoints[index] = FillPoints[index];
            LinePoints[4] = FillPoints[0];
        }

        private static Vector3 GetTileCenter(int column, int row)
        {
            JxqyIntPoint world = JxqyIsometricMapMath.TileToWorldPixel(
                column,
                row,
                false);
            return new Vector3(world.X, -world.Y, 0f);
        }

        private static bool ShouldDrawLabels(SceneView sceneView)
        {
            if (!sceneView.camera.orthographic)
                return false;
            float worldUnitsPerPixel =
                sceneView.camera.orthographicSize * 2f /
                Mathf.Max(1f, sceneView.camera.pixelHeight);
            return worldUnitsPerPixel <= 2.1f;
        }

        private static void FocusFirstTrap(
            SceneView sceneView,
            JxqyMapSceneIdentity identity,
            CachedMap map)
        {
            for (int row = 0; row < map.Data.Rows; row++)
            {
                for (int column = 0; column < map.Data.Columns; column++)
                {
                    if (map.Data.GetTrapIndex(column, row) == 0)
                        continue;
                    Vector3 center = identity.transform.TransformPoint(
                        GetTileCenter(column, row));
                    sceneView.in2DMode = true;
                    sceneView.Frame(
                        new Bounds(center, new Vector3(640f, 360f, 1f)),
                        false);
                    sceneView.Repaint();
                    return;
                }
            }
        }

        private static void FrameWholeMap(
            SceneView sceneView,
            JxqyMapSceneIdentity identity,
            JxqyRuntimeMapData map)
        {
            Vector3 first = identity.transform.TransformPoint(
                GetTileCenter(0, 0));
            Vector3 last = identity.transform.TransformPoint(
                GetTileCenter(map.Columns - 1, map.Rows - 1));
            var bounds = new Bounds(first, Vector3.zero);
            bounds.Encapsulate(last);
            bounds.Expand(new Vector3(
                JxqyIsometricMapMath.TileWidth * 2f,
                JxqyIsometricMapMath.TileHeight * 2f,
                1f));
            sceneView.in2DMode = true;
            sceneView.Frame(bounds, false);
            sceneView.Repaint();
        }

        private static void SaveSettings()
        {
            EditorPrefs.SetBool(GridKey, _showGrid);
            EditorPrefs.SetBool(BarrierKey, _showBarriers);
            EditorPrefs.SetBool(TrapKey, _showTraps);
            EditorPrefs.SetBool(LabelKey, _showLabels);
        }

        private static void ClearSceneCache()
        {
            _identity = null;
        }

        private static void ClearMapCache()
        {
            _cachedMap = null;
            _trapSections = null;
            SceneView.RepaintAll();
        }

        private sealed class CachedMap
        {
            public string StableId = string.Empty;
            public string MapName = string.Empty;
            public bool IsInitialMap;
            public JxqyRuntimeMapData Data;
            public int BarrierCount;
            public int TrapTileCount;
            public int TrapIndexCount;
            public int BoundTrapCount;
            public int UnboundTrapCount;
            public int MissingTrapScriptCount;
            public readonly Dictionary<int, JxqyIntPoint>
                TrapLabelPositions = new();
            public readonly Dictionary<int, TrapPreviewInfo>
                TrapInfo = new();
        }

        private sealed class TrapPreviewInfo
        {
            public TrapPreviewInfo(
                string script,
                string condition,
                string summary,
                string label,
                bool scriptFound)
            {
                Script = script;
                Condition = condition;
                Summary = summary;
                Label = label;
                ScriptFound = scriptFound;
            }

            public string Script { get; }
            public string Condition { get; }
            public string Summary { get; }
            public string Label { get; }
            public bool ScriptFound { get; }
        }

        private readonly struct TileRange
        {
            public TileRange(
                int startColumn,
                int endColumn,
                int startRow,
                int endRow)
            {
                StartColumn = startColumn;
                EndColumn = endColumn;
                StartRow = startRow;
                EndRow = endRow;
            }

            public int StartColumn { get; }
            public int EndColumn { get; }
            public int StartRow { get; }
            public int EndRow { get; }

            public bool Contains(int column, int row)
            {
                return column >= StartColumn && column <= EndColumn &&
                       row >= StartRow && row <= EndRow;
            }
        }
    }
}
